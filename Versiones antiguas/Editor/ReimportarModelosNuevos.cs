using UnityEditor;
using UnityEngine;

/// <summary>
/// Fuerza la reimportación de los FBX de personajes copiados por fuera del Editor, para que la build
/// lleve la malla nueva. Menú de un solo uso. Ver INC-1412.
/// </summary>
public static class ReimportarModelosNuevos
{
    private static readonly string[] Rutas =
    {
        "Assets/Art/Characters/NPCs/Vestuario/Victoria/AC13_Mono.fbx",
        "Assets/Art/Characters/Protagonistas/Will/Body23_Aventurero.fbx",
        "Assets/Art/Characters/Protagonistas/Will/Body24_AventureroChaqueta.fbx",
        "Assets/Art/Characters/NPCs/Vestuario/Victoria/Body27_Costurera.fbx",
        "Assets/Art/Characters/NPCs/Vestuario/Erika/Body28_GuerreraLigera.fbx",
        "Assets/Art/Characters/NPCs/Vestuario/Posadero/Body31_Posadero.fbx",
        "Assets/Art/Characters/NPCs/Vestuario/Archimago/Hair20_Archimago.fbx",
        "Assets/Art/Characters/NPCs/Vestuario/Liora/Hair21_Liora.fbx",
    };

    [MenuItem("El Sendero/Personajes/Modelos: reimportar los FBX nuevos (INC-1412)")]
    private static void Reimportar()
    {
        int hechos = 0;
        foreach (var ruta in Rutas)
        {
            if (AssetImporter.GetAtPath(ruta) == null) { Debug.LogWarning("[Modelos] No está: " + ruta); continue; }
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            hechos++;
        }
        AssetDatabase.Refresh();
        Debug.Log("[Modelos] Reimportados " + hechos + " de " + Rutas.Length + " FBX.");
    }
}
