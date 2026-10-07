#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Tobi (Tobías), el hermano pequeño de Liam: mismo pelo y cara que Liam, más pequeño y con la
/// túnica humilde de estar en casa.
public static class CrearTobi
{
    const string Origen = "Assets/Prefabs/_LIAM.prefab";
    public const string Prefab = "Assets/_NPCs/_GrafoNarrativo/Tobi.prefab";
    const float Escala = 0.86f;

    [MenuItem("El Sendero/Archivo/Liam/Crear prefab de Tobi")]
    public static void Crear()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) == null && !AssetDatabase.CopyAsset(Origen, Prefab))
            throw new InvalidOperationException("No se pudo copiar " + Origen);

        var go = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            go.name = "Tobi";
            go.transform.localScale = Vector3.one * Escala;
            var partes = go.GetComponentsInChildren<Transform>(true);
            Transform Parte(string nombre) => partes.FirstOrDefault(t => t.name == nombre) ?? throw new InvalidOperationException("Falta la parte " + nombre);
            Parte("Body12").gameObject.SetActive(false);
            Parte("Body03").gameObject.SetActive(true);
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                var id = so.FindProperty("dialogueCharacterId");
                if (id == null || id.propertyType != SerializedPropertyType.String) continue;
                id.stringValue = "CHAR_TOBI";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(go, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
        Debug.Log("[Tobi] Prefab listo en " + Prefab);
    }
}
#endif
