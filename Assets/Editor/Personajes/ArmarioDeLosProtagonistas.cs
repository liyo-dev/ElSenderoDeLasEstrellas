using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// El armario de los protagonistas (Raúl, 8 nov 2026: «los trajes nuevos no están en el armario y la ropa que
/// compramos no se puede poner, ni a Will ni a Liam ni a Estela»). Dos reglas:
/// 1. Cada prenda hecha a la medida de un protagonista (las piezas de su <see cref="AspectoDeProtagonista.Ficha"/>
///    con <c>NombreEnArmario</c>) es una prenda del armario solo para él (<see cref="WardrobeItemSO"/> con
///    <c>soloPara</c>), y se tiene desde el principio (initialUnlocked de <see cref="WardrobeInventory"/>).
/// 2. Toda prenda del armario que un personaje puede ponerse está en su cuerpo: las que le faltan (la ropa de la
///    tienda en Estela y Liam) se le añaden con <see cref="PiezaNuevaDelModelo"/>, copiadas del modelo que la tenga.
/// Se puede repetir; solo añade lo que falte.
/// </summary>
public static class ArmarioDeLosProtagonistas
{
    const string CarpetaDePrendas = "Assets/_WARDROBE ITEMS";
    const string Grupo = "Assets/Prefabs/GrupoDelJugador.prefab";

    /// <summary>Los modelos de cada protagonista que se visten con el armario (su cuerpo, su NPC, su retrato).</summary>
    static readonly (string ruta, Slot quien)[] Modelos =
    {
        ("Assets/Prefabs/_WILL.prefab", Slot.Will),
        ("Assets/Prefabs/_WILL_NPC.prefab", Slot.Will),
        ("Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab", Slot.Will),
        ("Assets/Prefabs/_ESTELA.prefab", Slot.Estela),
        ("Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab", Slot.Estela),
        ("Assets/Prefabs/_LIAM.prefab", Slot.Liam),
        ("Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab", Slot.Liam),
    };

    [MenuItem("El Sendero/Personajes/Armario de los protagonistas")]
    public static void Actualizar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Armario", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        foreach (var f in new[] { AspectoDeProtagonista.FichaWill, AspectoDeProtagonista.FichaEstela, AspectoDeProtagonista.FichaLiam })
            PrendasDe(f, informe);
        RopaEnSuCuerpo(informe);
        AssetDatabase.SaveAssets();
        Debug.Log("[ArmarioDeLosProtagonistas] " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar("Armario de los protagonistas", informe.ToString());
    }

    /// <summary>Regla 1: las prendas propias de la ficha, en el armario, solo para él y desde el principio.</summary>
    public static void PrendasDe(AspectoDeProtagonista.Ficha f, StringBuilder informe)
    {
        var nuevas = new List<WardrobeItemSO>();
        foreach (var p in f.Piezas.Where(p => !string.IsNullOrEmpty(p.NombreEnArmario)))
        {
            string ruta = $"{CarpetaDePrendas}/WardrobeItem_{p.Nombre}.asset";
            var item = AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(ruta);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<WardrobeItemSO>();
                AssetDatabase.CreateAsset(item, ruta);
            }
            var so = new SerializedObject(item);
            so.FindProperty("wardrobeId").stringValue = p.Nombre;
            so.FindProperty("category").intValue = (int)p.Categoria;
            so.FindProperty("partName").stringValue = p.Nombre;
            so.FindProperty("displayName").stringValue = p.NombreEnArmario;
            so.FindProperty("description").stringValue = p.Descripcion ?? "";
            var solo = so.FindProperty("soloPara");
            solo.arraySize = 1;
            solo.GetArrayElementAtIndex(0).intValue = (int)f.Personaje;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            nuevas.Add(item);
        }
        if (nuevas.Count == 0) return;

        var raiz = PrefabUtility.LoadPrefabContents(Grupo);
        try
        {
            var armario = raiz.GetComponentInChildren<WardrobeInventory>(true);
            if (armario == null) { informe.AppendLine($"No encuentro WardrobeInventory en {Grupo}."); return; }
            var so = new SerializedObject(armario);
            var lista = so.FindProperty("initialUnlocked");
            var ya = new HashSet<Object>();
            for (int i = 0; i < lista.arraySize; i++) ya.Add(lista.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var item in nuevas.Where(n => !ya.Contains(n)))
            {
                lista.arraySize++;
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = item;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, Grupo);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        informe.AppendLine($"{f.Nombre}: en su armario {string.Join(", ", nuevas.Select(n => n.DisplayName))}.");
    }

    /// <summary>Regla 2: toda prenda que un personaje puede ponerse está en su cuerpo.</summary>
    public static void RopaEnSuCuerpo(StringBuilder informe)
    {
        var prendas = AssetDatabase.FindAssets("t:WardrobeItemSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null && !string.IsNullOrEmpty(i.PartName)).ToList();

        // De dónde se copia cada pieza: del primer modelo que la tenga
        var fuentes = new Dictionary<string, GameObject>();
        foreach (var (ruta, _) in Modelos)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (modelo == null) continue;
            foreach (var r in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!fuentes.ContainsKey(r.name)) fuentes[r.name] = modelo;
        }

        foreach (var (ruta, quien) in Modelos)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) { informe.AppendLine($"No encuentro {ruta}."); continue; }
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var tiene = new HashSet<string>(raiz.GetComponentsInChildren<Renderer>(true).Select(r => r.name));
                var anadidas = new List<string>();
                var sinFuente = new List<string>();
                foreach (var item in prendas)
                {
                    if (!item.PuedeLlevarla(quien) || tiene.Contains(item.PartName)) continue;
                    if (!fuentes.TryGetValue(item.PartName, out var fuente)) { sinFuente.Add(item.PartName); continue; }
                    string prefijo = AspectoDeProtagonista.Prefijo(item.Category);
                    var vecina = raiz.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith(prefijo))
                              ?? raiz.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Body"));
                    if (vecina == null) { sinFuente.Add(item.PartName); continue; }
                    var nota = new StringBuilder();
                    if (PiezaNuevaDelModelo.Anadir(raiz, fuente, item.PartName, vecina.name, nota))
                    {
                        anadidas.Add(item.PartName);
                        tiene.Add(item.PartName);
                    }
                    else informe.Append(nota);
                }
                if (anadidas.Count > 0) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                informe.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(ruta)}: {(anadidas.Count > 0 ? $"{anadidas.Count} prendas añadidas ({string.Join(", ", anadidas)})" : "ya tenía toda su ropa")}" +
                                   (sinFuente.Count > 0 ? $"; sin modelo de donde copiar: {string.Join(", ", sinFuente)}" : "") + ".");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
    }
}
