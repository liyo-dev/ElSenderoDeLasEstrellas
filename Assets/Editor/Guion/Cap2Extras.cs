#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Extras del Cap. 2 (INC-725): los objetos del bolsillo de los extras (pluma, cuerda y clavos)
/// y el contrato de señales del campamento de los contrabandistas (Cap2ExtrasPrefabs).
/// Los bloques que antes se colgaban de Cap2.asset (contrabandistas, Cúpula Estelar y Levitación)
/// viven ahora en Cap3.asset y Cap4.asset, que monta El Sendero ▸ Historia ▸ Montar los capítulos 3 y 4
/// (MontajeDeLosCapitulos). Ver INC-725 e INC-922.
public static class Cap2Extras
{
    const string CarpetaItems = "Assets/_ITEMS";

    // Contrato con la escena (prefab Campamento de contrabandistas).
    public const string SenalAtaque = "CAP2_CONTRABANDISTAS_ATAQUE";
    public const string SenalVencidos = "CAP2_CONTRABANDISTAS_VENCIDOS";
    public const string SenalJaulaQuemada = "CAP2_CONTRABANDISTAS_JAULA_QUEMADA";

    /// Objetos del bolsillo de los extras: la pluma (contrabandistas) y la cuerda y los clavos
    /// (puente sin magia). El ItemRegistry se sincroniza solo al importarlos.
    public static void AsegurarObjetos()
    {
        Objeto("IT_PlumaBrillante", "pluma_brillante", "ITEM_PLUMA_BRILLANTE", "Pluma brillante",
            "Se quedó enganchada en una zarza. Todavía guarda un poco de luz blanca.");
        Objeto("IT_Cuerda", "cuerda", "ITEM_CUERDA", "Cuerda", "Una cuerda fuerte. Sirve para atar una tabla suelta.");
        Objeto("IT_Clavos", "clavos", "ITEM_CLAVOS", "Clavos", "Dos clavos algo torcidos. Todavía sujetan.");
        AssetDatabase.SaveAssets();
    }

    static void Objeto(string archivo, string id, string clave, string nombre, string descripcion)
    {
        string ruta = CarpetaItems + "/" + archivo + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);
        if (item == null) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, ruta); }
        item.itemId = id;
        item.displayNameId = clave + "_NAME"; item.displayName = nombre;
        item.useDescriptionId = clave + "_DESC"; item.useDescription = descripcion;
        item.usableFromInventory = false;
        item.usageKind = ItemData.ItemUsageKind.Quest;
        item.buyPrice = 0; item.sellValue = 0;
        EditorUtility.SetDirty(item);
    }
}
#endif
