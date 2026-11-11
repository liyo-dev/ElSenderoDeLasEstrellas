#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// Pasa los materiales del juego al shader «El Sendero/Dibujo» y los devuelve a como estaban.
/// De cada material solo cambia el shader y añade lo que el shader nuevo necesita (su tinte y si
/// lleva brillo de pelo): las propiedades de antes se quedan guardadas en el material, y qué shader
/// tenía cada uno se apunta en un registro, así que la vuelta atrás es exacta.
/// Se puede repetir: solo toca los materiales que aún no están pasados (packs nuevos, piezas nuevas).
public static class PasoAlAspectoDeDibujo
{
    private const string NombreDelShader = "El Sendero/Dibujo";
    private const string ShaderDeQuibli = "Quibli/Stylized Lit";
    private const string CarpetaDelRegistro = "Assets/Settings/AspectoDeDibujo";
    private const string RutaDelRegistro = CarpetaDelRegistro + "/MaterialesAntesDelDibujo.json";
    private const string RutaDelAspecto = "Assets/Resources/" + AspectoDeDibujo.RutaEnResources + ".asset";
    private const string RutaDelPerfilDelContorno = "Assets/DefaultVolumeProfile.asset";

    /// Shaders de luz corriente cuyos materiales se pasan al de dibujo.
    private static readonly string[] ShadersDeOrigen =
    {
        ShaderDeQuibli, "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit",
    };

    /// Plugins (sus demos) y efectos: sus materiales no son personajes ni escenario.
    private static readonly string[] CarpetasQueNoSeTocan =
    {
        "Assets/Plugins/", "Assets/VFX/", "Assets/_VFX/", "Assets/Art/VFX/", "Assets/TextMesh Pro/",
    };

    private static readonly Regex EsPelo = new Regex("pelo|hair|cabello|coleta|flequillo", RegexOptions.IgnoreCase);

    private static readonly Color TintaDelContorno = new Color(0.20f, 0.15f, 0.22f, 1f);
    private const float IntensidadDelContorno = 1f;
    private const float GrosorDelContorno = 2f;

    [Serializable]
    private sealed class Registro
    {
        public List<Entrada> materiales = new List<Entrada>();
        public bool hayContorno;
        public float contornoIntensidad;
        public float contornoGrosor;
        public Color contornoTinta;
    }

    [Serializable]
    private sealed class Entrada
    {
        public string guid;
        public string shader;
        public int cola;
    }

    [InitializeOnLoadMethod]
    private static void AlCargarElEditor() => EditorApplication.delayCall += () => AspectoDeDibujo.AplicarElDelJuego();

    [MenuItem("El Sendero/Aspecto de dibujo/Abrir los ajustes del aspecto")]
    private static void AbrirLosAjustes()
    {
        var aspecto = AsegurarElAspecto();
        Selection.activeObject = aspecto;
        EditorGUIUtility.PingObject(aspecto);
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Pasar los materiales al aspecto de dibujo")]
    public static void Pasar()
    {
        var shader = Shader.Find(NombreDelShader);
        if (shader == null)
        {
            OrdenesDelEditor.Avisar("Aspecto de dibujo", $"No encuentro el shader «{NombreDelShader}». No se ha cambiado nada.");
            return;
        }

        var registro = LeerElRegistro();
        var apuntados = new HashSet<string>(registro.materiales.Select(e => e.guid));
        var porOrigen = new Dictionary<string, int>();
        int transparentes = 0, conBrillo = 0, conDegradadoDeAltura = 0, conTexturaSumada = 0;
        double sumaDeSombra = 0; int rampas = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!ruta.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            if (CarpetasQueNoSeTocan.Any(c => ruta.StartsWith(c, StringComparison.OrdinalIgnoreCase))) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader == null) continue;
            string origen = mat.shader.name;
            if (Array.IndexOf(ShadersDeOrigen, origen) < 0) continue;

            bool deQuibli = origen == ShaderDeQuibli;
            if (!deQuibli && mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f) { transparentes++; continue; }

            Color tinte;
            if (deQuibli)
            {
                tinte = ColorIluminadoDeLaRampa(mat, out float sombra);
                if (sombra >= 0f) { sumaDeSombra += sombra; rampas++; }
                // Sin textura, Quibli pinta en negro lo que la textura debía pintar.
                if (mat.GetTexture("_BaseMap") == null)
                    tinte = (tinte.linear * (1f - Mathf.Clamp01(mat.GetFloat("_TextureImpact")))).gamma;
                if (mat.IsKeywordEnabled("DR_GRADIENT_ON")) conDegradadoDeAltura++;
                if (mat.IsKeywordEnabled("_TEXTUREBLENDINGMODE_ADD")) conTexturaSumada++;
            }
            else tinte = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            tinte.a = 1f;

            bool recorte = mat.IsKeywordEnabled("_ALPHATEST_ON");
            bool sinSombras = mat.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF");
            bool coloresDeVertice = mat.IsKeywordEnabled("DR_VERTEX_COLORS_ON");
            bool emision = mat.IsKeywordEnabled("_EMISSION");
            int cola = mat.rawRenderQueue;

            if (apuntados.Add(guid)) registro.materiales.Add(new Entrada { guid = guid, shader = origen, cola = cola });

            mat.shader = shader;
            mat.renderQueue = cola;
            mat.SetColor("_Tinte", tinte);
            if (!deQuibli) mat.SetFloat("_TextureImpact", 1f);
            Poner(mat, "_ALPHATEST_ON", recorte); mat.SetFloat("_AlphaClip", recorte ? 1f : 0f);
            Poner(mat, "_RECEIVE_SHADOWS_OFF", sinSombras); mat.SetFloat("_ReceiveShadows", sinSombras ? 0f : 1f);
            Poner(mat, "DR_VERTEX_COLORS_ON", coloresDeVertice); mat.SetFloat("_VertexColorsEnabled", coloresDeVertice ? 1f : 0f);
            Poner(mat, "_EMISSION", emision);
            bool pelo = EsPelo.IsMatch(mat.name);
            mat.SetFloat("_Brillo", pelo ? 1f : 0f);
            if (pelo) conBrillo++;
            EditorUtility.SetDirty(mat);

            porOrigen.TryGetValue(origen, out int n);
            porOrigen[origen] = n + 1;
        }

        bool contorno = MarcarElContorno(registro);
        GuardarElRegistro(registro);
        AsegurarElAspecto().Aplicar();
        AssetDatabase.SaveAssets();

        var informe = new StringBuilder();
        int total = porOrigen.Values.Sum();
        informe.Append($"{total} materiales pasados");
        foreach (var par in porOrigen) informe.Append($" · {par.Value} de «{par.Key}»");
        informe.Append($" · {registro.materiales.Count} apuntados para la vuelta atrás · {conBrillo} con brillo de pelo");
        if (rampas > 0) informe.Append($" · sombra media de las rampas de Quibli: {sumaDeSombra / rampas:F2} de la luz");
        if (transparentes > 0) informe.Append($" · {transparentes} transparentes de URP sin tocar");
        if (conDegradadoDeAltura > 0) informe.Append($" · {conDegradadoDeAltura} tenían degradado de altura (no se conserva)");
        if (conTexturaSumada > 0) informe.Append($" · {conTexturaSumada} sumaban la textura (ahora la multiplican)");
        informe.Append(contorno ? " · contorno marcado" : " · no encontré el contorno en el perfil por defecto");
        OrdenesDelEditor.Avisar("Aspecto de dibujo", informe.ToString());
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Volver al aspecto anterior")]
    public static void Volver()
    {
        var registro = LeerElRegistro();
        if (registro.materiales.Count == 0 && !registro.hayContorno)
        {
            OrdenesDelEditor.Avisar("Aspecto de dibujo", "No hay nada apuntado: los materiales ya están como antes.");
            return;
        }
        if (!OrdenesDelEditor.Confirmar("Aspecto de dibujo",
                $"Se devuelven {registro.materiales.Count} materiales al shader que tenían y el contorno a como estaba.", "Volver"))
            return;

        int devueltos = 0, sinShader = 0;
        foreach (var entrada in registro.materiales)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(entrada.guid));
            if (mat == null || mat.shader == null || mat.shader.name != NombreDelShader) continue;
            var anterior = Shader.Find(entrada.shader);
            if (anterior == null) { sinShader++; continue; }
            mat.shader = anterior;
            mat.renderQueue = entrada.cola;
            EditorUtility.SetDirty(mat);
            devueltos++;
        }

        if (registro.hayContorno && Contorno(out var perfil, out var ajustes))
        {
            ajustes.intensidad.value = registro.contornoIntensidad;
            ajustes.grosor.value = registro.contornoGrosor;
            ajustes.tinta.value = registro.contornoTinta;
            EditorUtility.SetDirty(ajustes);
            EditorUtility.SetDirty(perfil);
        }

        GuardarElRegistro(new Registro());
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Aspecto de dibujo",
            $"{devueltos} materiales devueltos a su shader" + (sinShader > 0 ? $" · {sinShader} sin devolver (su shader ya no existe)" : ""));
    }

    private static void Poner(Material mat, string palabra, bool activa)
    {
        if (activa) mat.EnableKeyword(palabra);
        else mat.DisableKeyword(palabra);
    }

    /// El color con que la rampa de Quibli pinta la parte iluminada (su punto más claro), tal como se
    /// escribe en un color de material. <paramref name="sombra"/>: luz del punto más oscuro respecto al
    /// más claro (lineal), o -1 si no hay rampa.
    private static Color ColorIluminadoDeLaRampa(Material mat, out float sombra)
    {
        sombra = -1f;
        if (!mat.HasProperty("_GradientRamp")) return Color.white;
        var rampa = mat.GetTexture("_GradientRamp") as Texture2D;
        if (rampa == null) return Color.white;

        Color[] pixeles;
        if (rampa.isReadable) pixeles = rampa.GetPixels();
        else
        {
            var rt = RenderTexture.GetTemporary(rampa.width, rampa.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previo = RenderTexture.active;
            Graphics.Blit(rampa, rt);
            RenderTexture.active = rt;
            var copia = new Texture2D(rampa.width, rampa.height, TextureFormat.RGBA32, false, true);
            copia.ReadPixels(new Rect(0, 0, rampa.width, rampa.height), 0, 0);
            copia.Apply();
            RenderTexture.active = previo;
            RenderTexture.ReleaseTemporary(rt);
            // Leída así llega ya en lineal: se devuelve a como se escribe un color.
            pixeles = copia.GetPixels().Select(c => c.gamma).ToArray();
            UnityEngine.Object.DestroyImmediate(copia);
        }
        if (pixeles.Length == 0) return Color.white;

        // Los píxeles de una textura sin sRGB son ya valores lineales.
        bool enLineal = rampa.isReadable && !GraphicsFormatUtility.IsSRGBFormat(rampa.graphicsFormat);
        Color claro = pixeles[0]; float luzClara = -1f, luzOscura = float.MaxValue;
        foreach (var p in pixeles)
        {
            Color lineal = enLineal ? p : p.linear;
            float luz = 0.2126f * lineal.r + 0.7152f * lineal.g + 0.0722f * lineal.b;
            if (luz > luzClara) { luzClara = luz; claro = enLineal ? p.gamma : p; }
            if (luz < luzOscura) luzOscura = luz;
        }
        if (luzClara > 1e-4f) sombra = luzOscura / luzClara;
        return claro;
    }

    private static bool Contorno(out VolumeProfile perfil, out ContornoDeTinta ajustes)
    {
        ajustes = null;
        perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfilDelContorno);
        return perfil != null && perfil.TryGet(out ajustes);
    }

    private static bool MarcarElContorno(Registro registro)
    {
        if (!Contorno(out var perfil, out var ajustes)) return false;
        if (!registro.hayContorno)
        {
            registro.hayContorno = true;
            registro.contornoIntensidad = ajustes.intensidad.value;
            registro.contornoGrosor = ajustes.grosor.value;
            registro.contornoTinta = ajustes.tinta.value;
        }
        ajustes.intensidad.value = IntensidadDelContorno;
        ajustes.grosor.value = GrosorDelContorno;
        ajustes.tinta.value = TintaDelContorno;
        EditorUtility.SetDirty(ajustes);
        EditorUtility.SetDirty(perfil);
        return true;
    }

    private static AspectoDeDibujo AsegurarElAspecto()
    {
        var aspecto = AssetDatabase.LoadAssetAtPath<AspectoDeDibujo>(RutaDelAspecto);
        if (aspecto != null) return aspecto;
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelAspecto));
        AssetDatabase.Refresh();
        aspecto = ScriptableObject.CreateInstance<AspectoDeDibujo>();
        AssetDatabase.CreateAsset(aspecto, RutaDelAspecto);
        return aspecto;
    }

    private static Registro LeerElRegistro()
    {
        if (!File.Exists(RutaDelRegistro)) return new Registro();
        var registro = JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro));
        return registro ?? new Registro();
    }

    private static void GuardarElRegistro(Registro registro)
    {
        Directory.CreateDirectory(CarpetaDelRegistro);
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(registro, true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
    }
}
#endif
