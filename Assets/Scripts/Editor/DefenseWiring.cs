using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone el efecto del contraataque de la B (INC-493) en el PlayerShieldController de _WILL.prefab.
/// Idempotente: solo lo asigna si está vacío.
/// </summary>
public static class DefenseWiring
{
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string CounterVfxPath = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Star hit.prefab";

    [MenuItem("El Sendero/Archivo/Combate/Montar contraataque de la B (INC-493)")]
    public static void Wire()
    {
        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(CounterVfxPath);
        if (vfx == null) { Debug.LogWarning($"[INC-493] No encuentro '{CounterVfxPath}'."); return; }

        var root = PrefabUtility.LoadPrefabContents(WillPrefabPath);
        try
        {
            var shield = root.GetComponentInChildren<PlayerShieldController>(true);
            if (shield == null) { Debug.LogWarning("[INC-493] _WILL.prefab no tiene PlayerShieldController."); return; }

            var so = new SerializedObject(shield);
            var prop = so.FindProperty("counterVfx");
            if (prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = vfx;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
                Debug.Log("[INC-493] Efecto del contraataque (Star hit) asignado en _WILL.prefab.");
            }
            else
            {
                Debug.Log("[INC-493] El contraataque ya tenía efecto; no se toca.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
