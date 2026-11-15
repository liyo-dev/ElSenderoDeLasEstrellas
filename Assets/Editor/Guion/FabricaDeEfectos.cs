#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Crea los SequenceBeat que pide una línea «efecto Clase campo=valor» y alimenta el catálogo del
/// taller. Es el único sitio que sabe qué beats existen y cómo se escribe cada campo: el horneado y
/// la ventana de efectos usan esta misma fábrica, así que el catálogo no puede ofrecer algo que el
/// horneador no sepa crear. Ver INC-1230 y TDD § 33.
internal static class FabricaDeEfectos
{
    private static Dictionary<string, Type> _tipos;

    /// Todos los beats no abstractos, por nombre (sin distinguir mayúsculas). Incluye los tres que
    /// no valen dentro de un guion para poder dar el error concreto al escribirlos.
    public static Dictionary<string, Type> Tipos
    {
        get
        {
            if (_tipos != null) return _tipos;

            _tipos = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in TypeCache.GetTypesDerivedFrom<SequenceBeat>())
                if (!t.IsAbstract) _tipos[t.Name] = t;
            return _tipos;
        }
    }

    /// Los que se pueden escribir en un guion, ordenados para el catálogo.
    public static IEnumerable<Type> TiposDeCatalogo()
        => Tipos.Values
            .Where(t => t != typeof(GuionBeat) && t != typeof(WaitBeat) && t != typeof(ParallelBeat))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);

    /// Crea el beat y le pone los campos. Devuelve null (y avisa por «error») si la clase o un campo
    /// no existen. «resolverAlias» traduce los alias del guion a los ids de la escena; puede devolver
    /// null si el alias no está en el reparto.
    public static SequenceBeat Crear(string clase, Dictionary<string, string> campos, GuionTexto.Orden orden,
        Action<string> error, Func<string, string> resolverAlias)
    {
        if (string.IsNullOrEmpty(clase)
            || (!Tipos.TryGetValue(clase, out var tipo) && !Tipos.TryGetValue(clase + "Beat", out tipo)))
        {
            error?.Invoke($"{orden}: no existe el beat '{clase}'");
            return null;
        }
        if (tipo == typeof(GuionBeat) || tipo == typeof(WaitBeat) || tipo == typeof(ParallelBeat))
        {
            error?.Invoke($"{orden}: '{tipo.Name}' no se usa dentro de un guion (el guion ya hace eso a su manera)");
            return null;
        }

        var beat = (SequenceBeat)Activator.CreateInstance(tipo);
        foreach (var kv in campos)
        {
            if (kv.Key == "dura" || kv.Key == "onlyIfFlag" || kv.Key == "skipIfFlag") continue;

            if (beat is IConfigurableDesdeGuion configurable
                && configurable.Configurar(kv.Key, kv.Value, alias => resolverAlias?.Invoke(alias) ?? alias))
                continue;

            var campo = tipo.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
            if (campo == null) { error?.Invoke($"{orden}: {tipo.Name} no tiene el campo '{kv.Key}'"); continue; }
            if (!Convertir(kv.Value, campo.FieldType, out object valor, orden, error)) continue;
            campo.SetValue(beat, valor);
        }

        // Los ids de actor en un efecto pueden escribirse con su alias del guion.
        foreach (var campo in tipo.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (campo.FieldType != typeof(string)) continue;
            if (!(campo.Name.ToLowerInvariant().Contains("actor") || campo.Name.EndsWith("Id"))) continue;
            string valor = campo.GetValue(beat) as string;
            if (string.IsNullOrEmpty(valor)) continue;
            string id = resolverAlias?.Invoke(valor);
            if (!string.IsNullOrEmpty(id)) campo.SetValue(beat, id);
        }
        return beat;
    }

    /// Campos que se pueden escribir desde el guion, para el catálogo y la línea base.
    public static IEnumerable<FieldInfo> CamposDe(Type tipo)
    {
        foreach (var campo in tipo.GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (campo.Name != "note" && EsTipoDeGuion(campo.FieldType))
                yield return campo;
    }

    /// Una línea «efecto Clase ...» con los valores por defecto, lista para pegar en un guion.
    public static string LineaBase(Type tipo)
    {
        var partes = new List<string> { "efecto " + tipo.Name };
        try
        {
            var beat = (SequenceBeat)Activator.CreateInstance(tipo);
            foreach (var campo in CamposDe(tipo))
            {
                string valor = ValorAGuion(campo.GetValue(beat), campo.FieldType);
                if (!string.IsNullOrEmpty(valor)) partes.Add(campo.Name + "=" + valor);
            }
        }
        catch { /* un beat con valores por defecto raros no rompe el catálogo */ }
        return string.Join(" ", partes);
    }

    /// El valor de un campo tal como se escribe en el guion (o null si no tiene forma de escribirse).
    public static string ValorAGuion(object valor, Type tipo)
    {
        if (valor == null) return null;
        if (tipo == typeof(string))
        {
            var s = (string)valor;
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Any(char.IsWhiteSpace) ? "\"" + s + "\"" : s;
        }
        if (tipo == typeof(float)) return ((float)valor).ToString("0.###", CultureInfo.InvariantCulture);
        if (tipo == typeof(int)) return ((int)valor).ToString(CultureInfo.InvariantCulture);
        if (tipo == typeof(bool)) return (bool)valor ? "si" : "no";
        if (tipo.IsEnum) return valor.ToString();
        if (tipo == typeof(Vector3))
        {
            var v = (Vector3)valor;
            return $"{F(v.x)};{F(v.y)};{F(v.z)}";
        }
        if (tipo == typeof(Color))
            return ColorUtility.ToHtmlStringRGB((Color)valor);
        if (tipo == typeof(List<string>))
        {
            var lista = (List<string>)valor;
            return lista != null && lista.Count > 0 ? string.Join("|", lista) : null;
        }
        if (valor is UnityEngine.Object objeto)
        {
            string ruta = AssetDatabase.GetAssetPath(objeto);
            return string.IsNullOrEmpty(ruta) ? null : ruta;
        }
        return null;
    }

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool EsTipoDeGuion(Type tipo)
        => tipo == typeof(string) || tipo == typeof(float) || tipo == typeof(int) || tipo == typeof(bool)
           || tipo.IsEnum || tipo == typeof(Vector3) || tipo == typeof(Color) || tipo == typeof(List<string>)
           || typeof(UnityEngine.Object).IsAssignableFrom(tipo);

    /// Convierte un texto del guion al tipo de un campo. La usan también los beats que escriben
    /// valores compuestos desde una orden de actor (offset, giro).
    public static bool Convertir(string texto, Type tipo, out object valor, GuionTexto.Orden orden)
        => Convertir(texto, tipo, out valor, orden, null);

    public static bool Convertir(string texto, Type tipo, out object valor, GuionTexto.Orden orden, Action<string> error)
    {
        valor = null;
        try
        {
            if (tipo == typeof(string)) { valor = texto; return true; }
            if (tipo == typeof(float)) { valor = float.Parse(texto, CultureInfo.InvariantCulture); return true; }
            if (tipo == typeof(int)) { valor = int.Parse(texto, CultureInfo.InvariantCulture); return true; }
            if (tipo == typeof(bool)) { valor = texto == "si" || texto == "sí" || texto == "true" || texto == "1"; return true; }
            if (tipo.IsEnum) { valor = Enum.Parse(tipo, texto, true); return true; }
            if (tipo == typeof(Vector3))
            {
                var p = texto.Split(';', '|');
                if (p.Length != 3) p = texto.Split(',');
                valor = new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture));
                return true;
            }
            if (tipo == typeof(Color))
            {
                if (ColorUtility.TryParseHtmlString(texto.StartsWith("#") ? texto : "#" + texto, out var c)) { valor = c; return true; }
                var p = texto.Split(';', '|');
                valor = new Color(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture),
                    float.Parse(p[2], CultureInfo.InvariantCulture), p.Length > 3 ? float.Parse(p[3], CultureInfo.InvariantCulture) : 1f);
                return true;
            }
            if (tipo == typeof(List<string>)) { valor = texto.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(); return true; }
            if (typeof(UnityEngine.Object).IsAssignableFrom(tipo))
            {
                string ruta = texto;
                if (!ruta.StartsWith("Assets/") && !ruta.StartsWith("Packages/"))
                {
                    var guid = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(texto) + " t:" + tipo.Name)
                        .FirstOrDefault(gd => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(gd)) == Path.GetFileNameWithoutExtension(texto));
                    if (guid == null && texto.Length == 32) guid = texto;
                    ruta = guid != null ? AssetDatabase.GUIDToAssetPath(guid) : null;
                }
                valor = ruta != null ? AssetDatabase.LoadAssetAtPath(ruta, tipo) : null;
                if (valor == null) { error?.Invoke($"{orden}: no encuentro el asset '{texto}' ({tipo.Name})"); return false; }
                return true;
            }
        }
        catch (Exception ex)
        {
            error?.Invoke($"{orden}: no entiendo '{texto}' como {tipo.Name} ({ex.Message})");
            return false;
        }
        error?.Invoke($"{orden}: campos de tipo {tipo.Name} no se pueden escribir en el guion");
        return false;
    }
}
#endif
