using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Saca del juego lo último que venía de Quibli (Raúl, 18 nov: «no quiero tener nada de Quibli»).
/// 1. El material de nube 3D pasa al shader propio «El Sendero/Nube de dibujo» y se muda a Art/Cielo.
/// 2. Los dos materiales de hierba del prólogo pasan al shader Dibujo y se mudan, con sus mallas y prefabs.
/// 3. Las mallas de nube que usa el juego se mudan a Art/Cielo/Mallas de nube.
/// 4. Los prefabs QuibliRainCloud3D pasan a llamarse NubeDeLluvia3D.
/// Todo con AssetDatabase.MoveAsset: el GUID no cambia, así que escenas y prefabs siguen apuntando bien.
/// Después, el informe de dependencias debería salir vacío y las carpetas del pack se pueden sacar.
public static class MudanzaFueraDeQuibli
{
    const string Nube = "Assets/Art/World/Quibli/Sample Scene with Quibli/Materials/SampleScene_Cloud3D.mat";
    const string NubeNueva = "Assets/Art/Cielo/Nube_Dibujo.mat";
    const string CarpetaMallasDeNube = "Assets/Art/Cielo/Mallas de nube";
    const string CarpetaHierba = "Assets/Art/World/Prologo_Valle/Hierba";

    static readonly string[] Hierbas =
    {
        "Assets/Art/World/Quibli/Nature/Materials/NatureScene_Grass_Long.mat",
        "Assets/Art/World/Quibli/Nature/Materials/NatureScene_Grass_Short.mat",
    };

    [MenuItem("El Sendero/Aspecto de dibujo/Quibli: mudar nubes y hierba a lo nuestro")]
    static void Mudar()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Quibli] Sal de Play antes."); return; }
        var informe = new StringBuilder("# Mudanza fuera de Quibli\n\n");
        Carpeta(CarpetaMallasDeNube);
        Carpeta(CarpetaHierba);

        var nube = AssetDatabase.LoadAssetAtPath<Material>(Nube);
        if (nube != null)
        {
            nube.shader = Shader.Find("El Sendero/Nube de dibujo");
            nube.SetColor("_Color", new Color(1f, 0.99f, 0.96f));
            nube.SetColor("_ColorEnSombra", new Color(0.66f, 0.66f, 0.86f));
            nube.SetFloat("_AlphaThreshold", 0.5f);
            LimpiarPropiedades(nube);
            informe.Append(Mover(Nube, NubeNueva));
        }

        foreach (string ruta in Hierbas)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) continue;
            Color arriba = mat.HasProperty("_ColorTop") ? mat.GetColor("_ColorTop") : new Color(0.44f, 0.82f, 0.09f);
            mat.shader = Shader.Find("El Sendero/Dibujo");
            mat.SetColor("_Tinte", arriba);
            mat.SetTexture("_BaseMap", null);
            LimpiarPropiedades(mat);
            informe.Append(Mover(ruta, $"{CarpetaHierba}/{Path.GetFileName(ruta).Replace("NatureScene_", "")}"));
        }

        // Lo que aún cuelga de las carpetas del pack y usa el juego: mallas de nube, hierba y sus prefabs.
        var raices = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        foreach (string dep in AssetDatabase.GetDependencies(raices, true).Where(EnCarpetaDeQuibli).ToList())
        {
            if (dep.EndsWith(".cs") || dep.EndsWith(".shader") || dep.EndsWith(".shadergraph") || dep.EndsWith(".shadersubgraph") || dep.EndsWith(".hlsl")) continue;
            string destino = dep.Contains("Foliage Generator Exported Meshes") ? CarpetaMallasDeNube : CarpetaHierba;
            informe.Append(Mover(dep, $"{destino}/{Path.GetFileName(dep)}"));
        }

        foreach (string guid in AssetDatabase.FindAssets("QuibliRainCloud3D t:Prefab"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            string nuevo = Path.GetFileNameWithoutExtension(ruta).Replace("QuibliRainCloud3D", "NubeDeLluvia3D");
            string error = AssetDatabase.RenameAsset(ruta, nuevo);
            informe.Append(string.IsNullOrEmpty(error) ? $"- {ruta} → {nuevo}\n" : $"- NO renombrado {ruta}: {error}\n");
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "quibli_mudanza.md"), informe.ToString());
        Debug.Log("[Quibli] " + informe);
    }

    static bool EnCarpetaDeQuibli(string ruta) => ruta.StartsWith("Assets/Plugins/Quibli/") || ruta.StartsWith("Assets/Art/World/Quibli/");

    static string Mover(string desde, string hasta)
    {
        if (desde == hasta) return "";
        hasta = AssetDatabase.GenerateUniqueAssetPath(hasta);
        string error = AssetDatabase.MoveAsset(desde, hasta);
        return string.IsNullOrEmpty(error) ? $"- {desde} → {hasta}\n" : $"- NO movido {desde}: {error}\n";
    }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(ruta).Replace('\\', '/'), Path.GetFileName(ruta));
    }

    /// Quita del material las texturas, colores y números que su shader ya no tiene: si no, el material
    /// sigue arrastrando las texturas del shader viejo como dependencias.
    static void LimpiarPropiedades(Material mat)
    {
        var propias = new HashSet<string>();
        for (int i = 0; i < mat.shader.GetPropertyCount(); i++) propias.Add(mat.shader.GetPropertyName(i));
        var so = new SerializedObject(mat);
        foreach (string lista in new[] { "m_SavedProperties.m_TexEnvs", "m_SavedProperties.m_Floats", "m_SavedProperties.m_Colors", "m_SavedProperties.m_Ints" })
        {
            var p = so.FindProperty(lista);
            if (p == null) continue;
            for (int i = p.arraySize - 1; i >= 0; i--)
                if (!propias.Contains(p.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue)) p.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mat);
    }
}
