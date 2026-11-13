#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Enciende las pinceladas del shader de dibujo en el escenario de color liso (los modelos propios hechos en
/// Blender, con un color plano o una textura de 4×4), para que no salten al lado de los packs, que llevan
/// textura pintada. Cuánto se notan se ajusta en AspectoDeDibujo ▸ Pinceladas. Personajes, efectos,
/// interfaz y plugins no se tocan. Ver INC-1138.
public static class PinceladasDeLosModelosPropios
{
    private const string Dibujo = "El Sendero/Dibujo";
    private const int LadoDeUnColorLiso = 16;
    private const string RutaDelRegistro = "Assets/Settings/AspectoDeDibujo/PinceladasPuestas.json";

    private static readonly string[] Excluidas =
    {
        "Assets/Art/Characters/", "Assets/Art/UI/", "Assets/VFX/", "Assets/_VFX/", "Assets/Art/VFX/",
        "Assets/Plugins/", "Assets/TextMesh Pro/", "Assets/_NPCs/", "Assets/Kits/",
    };

    [Serializable] private sealed class Registro { public List<string> materiales = new List<string>(); }

    [MenuItem("El Sendero/Aspecto de dibujo/Pinceladas en el escenario de color liso")]
    public static void Poner()
    {
        var shader = Shader.Find(Dibujo);
        if (shader == null) return;
        var registro = Leer();
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!ruta.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            if (Excluidas.Any(c => ruta.StartsWith(c, StringComparison.OrdinalIgnoreCase))) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader != shader || mat.IsKeywordEnabled("_PINCEL")) continue;
            if (mat.IsKeywordEnabled("_ALPHATEST_ON")) continue;   // hojas y flores recortadas: ya llevan su dibujo
            var tex = mat.GetTexture("_BaseMap");
            if (tex != null && (tex.width > LadoDeUnColorLiso || tex.height > LadoDeUnColorLiso)) continue;
            mat.EnableKeyword("_PINCEL"); mat.SetFloat("_Pincel", 1f);
            EditorUtility.SetDirty(mat);
            if (!registro.materiales.Contains(ruta)) registro.materiales.Add(ruta);
            n++;
        }
        Guardar(registro);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Pinceladas] Puestas en {n} materiales de color liso.");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Pinceladas: quitarlas")]
    public static void Quitar()
    {
        var registro = Leer();
        foreach (var ruta in registro.materiales)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) continue;
            mat.DisableKeyword("_PINCEL"); mat.SetFloat("_Pincel", 0f);
            EditorUtility.SetDirty(mat);
        }
        Guardar(new Registro());
        AssetDatabase.SaveAssets();
    }

    private static Registro Leer() =>
        File.Exists(RutaDelRegistro) ? JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro)) ?? new Registro() : new Registro();

    private static void Guardar(Registro r)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelRegistro));
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(r, true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
    }
}
#endif
