#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Ajusta el postprocesado general del juego (el perfil de la calidad) al aspecto de dibujo y lo
/// devuelve a como estaba. El perfil venía de la época de Quibli: grano de película, un mezclador que
/// quitaba un 20 % de rojo, una corrección de color que empujaba las luces al rojo y el «detalle
/// estilizado», todo pensado para una imagen con degradados. Con colores planos y contorno ensucian:
/// el grano mete ruido en las zonas lisas y las dos correcciones de rojo se pisan y tiñen la piel.
///
/// Antes de tocar nada guarda una copia del perfil; la vuelta atrás copia sus valores de vuelta.
/// Los perfiles propios de momentos concretos (sueño del prólogo, golpes, interiores) no se tocan.
/// Ver INC-1058.
public static class PostprocesoDeDibujo
{
    private const string RutaDelPerfil = "Assets/Settings/Volumenes/Volumen Profile.asset";
    private const string CarpetaDeLaCopia = "Assets/Settings/Volumenes/Antes del dibujo";
    private const string RutaDeLaCopia = CarpetaDeLaCopia + "/Volumen Profile (antes del dibujo).asset";

    [MenuItem("El Sendero/Aspecto de dibujo/Postprocesado: pasar al de dibujo", priority = 20)]
    public static void Pasar()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfil);
        if (perfil == null) { OrdenesDelEditor.Avisar("Postprocesado de dibujo", "No encuentro " + RutaDelPerfil); return; }

        var hecho = new StringBuilder();
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDeLaCopia) == null)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaDeLaCopia)) AssetDatabase.CreateFolder("Assets/Settings/Volumenes", "Antes del dibujo");
            hecho.Append(AssetDatabase.CopyAsset(RutaDelPerfil, RutaDeLaCopia) ? "Copia guardada. " : "NO he podido guardar la copia. ");
        }

        // Fuera lo que ensucia los colores planos.
        hecho.Append(Activar(perfil, "FilmGrain", false));
        hecho.Append(Activar(perfil, "ChannelMixer", false));
        hecho.Append(Activar(perfil, "StylizedDetail", false));
        // La corrección de color se queda en lo que le sienta bien a un dibujo: algo de contraste y de
        // viveza, sin teñir luces ni sombras (el color de la sombra ya lo pone el shader).
        hecho.Append(Poner(perfil, "ColorGrading", "redHighlights", 0f));
        hecho.Append(Poner(perfil, "ColorGrading", "greenShadows", 0f));
        hecho.Append(Poner(perfil, "ColorGrading", "blueShadows", 0f));
        hecho.Append(Poner(perfil, "ColorGrading", "contrast", 0.15f));
        hecho.Append(Poner(perfil, "ColorGrading", "vibrance", 0.15f));
        hecho.Append(Poner(perfil, "ColorGrading", "saturation", 0.08f));
        // El resplandor, solo para lo que brilla de verdad (magia, faroles, cielo): con el umbral en 1
        // la piel clara y las paredes blancas a pleno sol no resplandecen.
        hecho.Append(Poner(perfil, "Bloom", "threshold", 1.0f));
        hecho.Append(Poner(perfil, "Bloom", "intensity", 0.8f));
        hecho.Append(Poner(perfil, "Bloom", "scatter", 0.5f));

        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Postprocesado de dibujo", hecho.ToString());
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Postprocesado: volver al de antes", priority = 21)]
    public static void Volver()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfil);
        var copia = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDeLaCopia);
        if (perfil == null || copia == null) { OrdenesDelEditor.Avisar("Postprocesado de dibujo", "No hay copia de antes: no hay nada que devolver."); return; }
        int devueltos = 0;
        foreach (var antes in copia.components.Where(c => c != null))
        {
            var ahora = perfil.components.FirstOrDefault(c => c != null && c.GetType() == antes.GetType());
            if (ahora == null) continue;
            string nombre = ahora.name;
            EditorUtility.CopySerialized(antes, ahora);
            ahora.name = nombre;
            EditorUtility.SetDirty(ahora);
            devueltos++;
        }
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Postprocesado de dibujo", $"Devueltos {devueltos} efectos a como estaban.");
    }

    private static VolumeComponent Efecto(VolumeProfile perfil, string tipo)
        => perfil.components.FirstOrDefault(c => c != null && c.GetType().Name == tipo);

    private static string Activar(VolumeProfile perfil, string tipo, bool activo)
    {
        var efecto = Efecto(perfil, tipo);
        if (efecto == null) return $"{tipo}: no está. ";
        if (efecto.active == activo) return "";
        efecto.active = activo;
        EditorUtility.SetDirty(efecto);
        return $"{tipo}: {(activo ? "encendido" : "apagado")}. ";
    }

    private static string Poner(VolumeProfile perfil, string tipo, string campo, float valor)
    {
        var efecto = Efecto(perfil, tipo);
        if (efecto == null) return $"{tipo}: no está. ";
        var f = efecto.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null || !(f.GetValue(efecto) is VolumeParameter<float> p)) return $"{tipo}.{campo}: no existe. ";
        float antes = p.value;
        p.overrideState = true;
        p.value = valor;
        EditorUtility.SetDirty(efecto);
        return Mathf.Approximately(antes, valor) ? "" : $"{tipo}.{campo}: {antes:0.##} → {valor:0.##}. ";
    }
}
#endif
