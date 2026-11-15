using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone los rasgos a las capas de mago (INC-1001): la de principiante solo cambia el aspecto; la
/// avanzada abarata el Viento; la experta, además, da más margen para devolver ataques.
/// Idempotente.
/// </summary>
public static class RasgosDeLasCapas
{
    private const string Carpeta = "Assets/_WARDROBE ITEMS/";

    [MenuItem("El Sendero/Archivo/Equipo/Poner rasgos a las capas de mago (INC-1001)")]
    public static void Poner()
    {
        var tabla = new Dictionary<string, RasgoDePrenda[]>
        {
            { "WardrobeItem_Cloak02", new RasgoDePrenda[0] },
            { "WardrobeItem_Cloak03", new[] { RasgoDePrenda.VientoMasBarato } },
            { "WardrobeItem_Cloak01", new[] { RasgoDePrenda.VientoMasBarato, RasgoDePrenda.ContraataqueMasAmplio } },
        };
        int hechas = 0;
        foreach (var par in tabla)
        {
            var prenda = AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(Carpeta + par.Key + ".asset");
            if (prenda == null) { Debug.LogWarning("[Rasgos] No está " + par.Key); continue; }
            var so = new SerializedObject(prenda);
            var lista = so.FindProperty("rasgos");
            lista.arraySize = par.Value.Length;
            for (int i = 0; i < par.Value.Length; i++) lista.GetArrayElementAtIndex(i).enumValueIndex = (int)par.Value[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prenda);
            hechas++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Rasgos] Rasgos puestos en {hechas} capas.");
    }
}
