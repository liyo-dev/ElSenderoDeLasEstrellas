using System.Linq;
using UnityEditor;
using UnityEngine;

// Temporal (8 nov 2026): retira la chaqueta como pieza de capa (Cloak05_Chaqueta), que pasa a ser Body24_AventureroChaqueta.
// Se ejecuta una vez por el buzón y el script se retira a Versiones antiguas.
public static class RetirarCloak05Temporal
{
    [MenuItem("El Sendero/Personajes/_Temporal: retirar Cloak05_Chaqueta")]
    static void Retirar()
    {
        var informe = new System.Text.StringBuilder();
        foreach (var ruta in new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab" })
        {
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var piezas = raiz.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Cloak05_Chaqueta").ToArray();
                foreach (var t in piezas) Object.DestroyImmediate(t.gameObject);
                if (piezas.Length > 0) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                informe.Append($"{ruta}: {piezas.Length} fuera. ");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        const string grupo = "Assets/Prefabs/GrupoDelJugador.prefab";
        var item = AssetDatabase.LoadAssetAtPath<Object>("Assets/_WARDROBE ITEMS/WardrobeItem_Cloak05_Chaqueta.asset");
        var g = PrefabUtility.LoadPrefabContents(grupo);
        try
        {
            var so = new SerializedObject(g.GetComponentInChildren<WardrobeInventory>(true));
            var lista = so.FindProperty("initialUnlocked");
            for (int i = lista.arraySize - 1; i >= 0; i--)
            {
                var e = lista.GetArrayElementAtIndex(i);
                if (e.objectReferenceValue == item || e.objectReferenceValue == null) { e.objectReferenceValue = null; lista.DeleteArrayElementAtIndex(i); informe.Append("quitado del armario inicial. "); }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(g, grupo);
        }
        finally { PrefabUtility.UnloadPrefabContents(g); }
        AssetDatabase.DeleteAsset("Assets/_WARDROBE ITEMS/WardrobeItem_Cloak05_Chaqueta.asset");
        AssetDatabase.DeleteAsset("Assets/Art/Characters/Protagonistas/Will/Cloak05_Chaqueta.fbx");
        foreach (var guid in AssetDatabase.FindAssets("Mat_Cloak05_Chaqueta", new[] { "Assets/Art/Characters/Protagonistas/Will/Materiales" }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Retirar Cloak05", informe.ToString());
    }
}
