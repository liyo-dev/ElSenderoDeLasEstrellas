#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// La llama de las velas de «La mañana después» (VFX_VelaPequena, VFX_VelaGrande, VFX_Manga) usa
/// el material «Fire 1» del pack Universal FX, que traía la prueba de profundidad en «Always»:
/// la llama se pintaba encima de todo, también de los personajes que tiene delante. Se deja en
/// «LessEqual», el valor por defecto del shader. Informa de los demás prefabs que lo usan.
/// Ver INC-902. Se puede ejecutar varias veces.
public static class Cap2LlamaDeLaVela
{
    const string Material = "Assets/VFX/Univeral FX Shader/Material/FX/Fire 1.mat";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Llama de la vela detrás de los personajes (INC-902)")]
    public static void Aplicar()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Material);
        if (mat == null) { OrdenesDelEditor.Avisar("Llama de la vela", "No se encuentra " + Material); return; }
        float antes = mat.HasProperty("_ZTestMode") ? mat.GetFloat("_ZTestMode") : -1f;
        mat.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var usos = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.GetDependencies(ruta, false).Contains(Material)) usos.Add(System.IO.Path.GetFileNameWithoutExtension(ruta));
        }
        OrdenesDelEditor.Avisar("Llama de la vela",
            $"Fire 1: ZTest {(CompareFunction)(int)antes} → LessEqual. Prefabs que lo usan ({usos.Count}): {string.Join(", ", usos)}");
    }
}
#endif
