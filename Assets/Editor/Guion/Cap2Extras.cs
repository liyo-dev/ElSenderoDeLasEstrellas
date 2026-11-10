#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Extras del Cap. 2 (INC-725): los objetos del bolsillo de los extras (pluma, cuerda y clavos),
/// el título del desbloqueo del Cap. 1 y el contrato de señales del campamento de los
/// contrabandistas (Cap2ExtrasPrefabs). Los bloques que este menú colgaba de Cap2.asset
/// (contrabandistas, Cúpula Estelar y Levitación) viven ahora en Cap3.asset y Cap4.asset, que
/// monta El Sendero ▸ Historia ▸ Montar los capítulos 3 y 4 (MontajeDeLosCapitulos). Ver INC-725 e INC-922.
public static class Cap2Extras
{
    const string GrafoCap1 = "Assets/NarrativeGraph/Cap1.asset";
    const string CarpetaItems = "Assets/_ITEMS";
    const string Informe = "Guiones/MainWorld_Cap2Extras/grafo-guardado.md";

    // Nodo del Cap. 1 que desbloquea la magia (spellsToUnlock = Llama Astral).
    const string NodoDesbloqueoCap1 = "e7e8e2f2-6e54-4490-97b1-fd48323945a4";
    const string TituloDesbloqueoCap1 = "30.- SE DESBLOQUEA EL ATAQUE MÁGICO Y LLAMA ASTRAL";

    // Contrato con la escena (prefab Campamento de contrabandistas).
    public const string SenalAtaque = "CAP2_CONTRABANDISTAS_ATAQUE";
    public const string SenalVencidos = "CAP2_CONTRABANDISTAS_VENCIDOS";
    public const string SenalJaulaQuemada = "CAP2_CONTRABANDISTAS_JAULA_QUEMADA";

    [MenuItem("El Sendero/Capítulo 2/Extras INC-725: montar en los grafos")]
    public static void Montar()
    {
        if (Application.isPlaying) throw new InvalidOperationException("El montaje necesita el Editor fuera de Play.");
        AsegurarObjetos();
        var cap1 = Cargar<NarrativeGraph>(GrafoCap1);
        var desbloqueo = cap1.FindNode(NodoDesbloqueoCap1) as UnlockAbilitiesNode
            ?? throw new InvalidOperationException("Cap1: falta el nodo de desbloqueo de la magia.");
        if (!desbloqueo.spellsToUnlock.Contains(SpellId.Fireball))
            throw new InvalidOperationException("Cap1: el nodo de desbloqueo ya no da Llama Astral; revisar antes de renombrarlo.");
        desbloqueo.displayTitle = TituloDesbloqueoCap1;
        EditorUtility.SetDirty(cap1);
        AssetDatabase.SaveAssets();
        Verificar();
    }

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

    [MenuItem("El Sendero/Capítulo 2/Extras INC-725: verificar")]
    public static void Verificar()
    {
        var errores = new List<string>();
        var cap1 = Cargar<NarrativeGraph>(GrafoCap1);
        if (cap1.FindNode(NodoDesbloqueoCap1)?.displayTitle != TituloDesbloqueoCap1)
            errores.Add("Cap1: el título del desbloqueo de la magia no está corregido.");
        foreach (var i in new[] { "IT_PlumaBrillante", "IT_Cuerda", "IT_Clavos" })
            if (AssetDatabase.LoadAssetAtPath<ItemData>(CarpetaItems + "/" + i + ".asset") == null) errores.Add("Falta " + i + ".");
        Directory.CreateDirectory(Path.GetDirectoryName(Informe));
        File.WriteAllLines(Informe, errores.Count == 0 ? new[] { "Extras del Cap. 2 en orden." } : errores.ConvertAll(e => "ERROR · " + e).ToArray());
        if (errores.Count > 0) throw new InvalidOperationException("[Cap2Extras] " + string.Join("\n", errores));
        Debug.Log("[Cap2Extras] Extras verificados. Informe en " + Informe);
    }

    static T Cargar<T>(string ruta) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("Falta " + ruta);
}
#endif
