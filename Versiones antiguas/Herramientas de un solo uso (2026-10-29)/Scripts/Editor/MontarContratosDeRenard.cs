#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Contratos de caza de Renard (Secundary.asset), detrás de su presentación:
/// «Patas de sobra» (8 arañas → 20 Esencia) y «Donde no llega la luz» (15 arañas → 40 Esencia
/// y el parche pirata, que deja de venderse en su tienda). Las arañas avisan al morir con
/// ENEMIGO_DERROTADO_ARANA (SignalEmitter, solo si alguien escucha) y WaitSignalCountNode cuenta.
/// También importa los iconos de la Esencia y de las bolsas de monedas. Por AssetDatabase.
/// Idempotente. Requiere haber montado antes la tienda de Renard. Ver INC-637.
/// </summary>
public static class MontarContratosDeRenard
{
    const string Script = "Assets/Scripts/Editor/MontarContratosDeRenard.cs";
    const string Grafo = "Assets/NarrativeGraph/Secundary.asset";
    const string NodoConocido = "25b810b1-9ddd-48bd-9474-773389932a04";
    const string Prefijo = "25b810b1-9ddd-48bd-9474-77338993c0";
    const string Renard = "NPC_Renard";
    const string Hablante = "CHAR_RENARD";
    const string Senal = "ENEMIGO_DERROTADO_ARANA";
    const string Arana = "Assets/Prefabs/Enemy/Spider1.prefab";
    const string Tienda = "Assets/_SHOPS/ShopMenu_Renard.prefab";
    const string Parche = "Assets/_WARDROBE ITEMS/WardrobeItem_AC07_PiratePatch.asset";
    const string CarpetaQuests = "Assets/_QUEST/SECUNDARIA/RENARD";
    const string CarpetaDialogos = "Assets/_DIALOGUES/Renard";
    const string RutaStart = "Assets/Scenes/Systems/Start.unity";

    [MenuItem("El Sendero/Archivo/Montar los contratos de caza de Renard")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play para montar los contratos.");
        var grafo = Exigir<NarrativeGraph>(Grafo);
        var conocido = grafo.FindNode(NodoConocido) as SetFlagNode
            ?? throw new InvalidOperationException("Falta el primer encuentro con Renard: ejecuta antes «Montar la tienda de Renard».");
        var esencia = Exigir<ItemData>("Assets/_ITEMS/IT_Esencia.asset");
        var parche = Exigir<WardrobeItemSO>(Parche);
        var informe = new List<string>();

        Iconos(esencia, informe);
        AranaAvisaAlMorir(informe);
        var q1 = Quest("RENARD_CONTRATO_1", 8);
        var q2 = Quest("RENARD_CONTRATO_2", 15);
        informe.Add("✓ Quests RENARD_CONTRATO_1 (8 pasos) y RENARD_CONTRATO_2 (15 pasos).");

        var oferta1 = Dialogo("DG_RENARD_CONTRATO1_OFERTA", "DLG_RENARD_CONTRATO1_OFERTA_01", "DLG_RENARD_CONTRATO1_OFERTA_02");
        var mientras1 = Dialogo("DG_RENARD_CONTRATO1_MIENTRAS", "DLG_RENARD_CONTRATO1_MIENTRAS_01");
        var hecho1 = Dialogo("DG_RENARD_CONTRATO1_HECHO", "DLG_RENARD_CONTRATO1_HECHO_01");
        var oferta2 = Dialogo("DG_RENARD_CONTRATO2_OFERTA", "DLG_RENARD_CONTRATO2_OFERTA_01");
        var mientras2 = Dialogo("DG_RENARD_CONTRATO2_MIENTRAS", "DLG_RENARD_CONTRATO2_MIENTRAS_01");
        var hecho2 = Dialogo("DG_RENARD_CONTRATO2_HECHO", "DLG_RENARD_CONTRATO2_HECHO_01", "DLG_RENARD_CONTRATO2_HECHO_02");

        Undo.RecordObject(grafo, "Contratos de caza de Renard");
        var cadena = new List<NarrativeNode>
        {
            Nodo(grafo, 1, "Renard ofrece el primer contrato", new PlayDialogueNode { npcId = Renard, dialogue = oferta1 }),
            Nodo(grafo, 2, "Contrato: Patas de sobra", new StartQuestNode { questId = q1.questId, giverId = Renard, inProgressDialogue = mientras1 }),
            Nodo(grafo, 3, "Ocho arañas", new WaitSignalCountNode { eventKey = Senal, count = 8, questId = q1.questId }),
            Nodo(grafo, 4, "Volver con Renard", new WaitNpcInteractionNode { npcId = Renard, acceptEarlierInteraction = false }),
            Nodo(grafo, 5, "Renard paga el primer contrato", new PlayDialogueNode { npcId = Renard, dialogue = hecho1 }),
            Nodo(grafo, 6, "Completa Patas de sobra", new CompleteQuestStepsNode { questId = q1.questId, completeQuest = true }),
            Nodo(grafo, 7, "20 de Esencia", new GiveInventoryItemNode { item = esencia, amount = 20 }),
            Nodo(grafo, 8, "Renard ofrece el segundo contrato", new PlayDialogueNode { npcId = Renard, dialogue = oferta2 }),
            Nodo(grafo, 9, "Contrato: Donde no llega la luz", new StartQuestNode { questId = q2.questId, giverId = Renard, inProgressDialogue = mientras2 }),
            Nodo(grafo, 10, "Quince arañas", new WaitSignalCountNode { eventKey = Senal, count = 15, questId = q2.questId }),
            Nodo(grafo, 11, "Volver con Renard", new WaitNpcInteractionNode { npcId = Renard, acceptEarlierInteraction = false }),
            Nodo(grafo, 12, "Renard paga el segundo contrato", new PlayDialogueNode { npcId = Renard, dialogue = hecho2 }),
            Nodo(grafo, 13, "Completa Donde no llega la luz", new CompleteQuestStepsNode { questId = q2.questId, completeQuest = true }),
            Nodo(grafo, 14, "40 de Esencia", new GiveInventoryItemNode { item = esencia, amount = 40 }),
            Nodo(grafo, 15, "El parche de Renard", new UnlockWardrobeItemNode { wardrobeItem = parche }),
        };
        for (int i = 0; i < cadena.Count - 1; i++) cadena[i].outputs = new List<string> { cadena[i + 1].guid };
        cadena[cadena.Count - 1].outputs = new List<string>();
        if (conocido.outputs.Count > 0 && conocido.outputs[0] != cadena[0].guid)
            informe.Add("⚠ El nodo «Renard conocido» ya salía hacia otro nodo; se sustituye por los contratos.");
        conocido.outputs = new List<string> { cadena[0].guid };
        EditorUtility.SetDirty(grafo);
        informe.Add("✓ Cadena de contratos tras «Renard conocido» en Secundary.asset.");

        QuitarDeLaTienda(parche, informe);
        AssetDatabase.SaveAssets();
        Catalogo(informe);
        Archivar();
        Debug.Log("[Contratos de Renard]\n- " + string.Join("\n- ", informe) +
                  "\nSi la ventana del grafo está abierta, ciérrala y vuelve a abrirla.");
    }

    static T Exigir<T>(string ruta) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("Falta " + ruta);

    static NarrativeNode Nodo(NarrativeGraph grafo, int n, string titulo, NarrativeNode nuevo)
    {
        string guid = Prefijo + n.ToString("00");
        var existente = grafo.FindNode(guid);
        if (existente != null && existente.GetType() != nuevo.GetType())
            throw new InvalidOperationException($"El GUID {guid} ya es de otro tipo de nodo.");
        int indice = existente != null ? grafo.nodes.IndexOf(existente) : -1;
        nuevo.guid = guid;
        nuevo.displayTitle = titulo;
        nuevo.chapter = "Renard";
        nuevo.position = new Vector2(400f + ((n - 1) % 8) * 330f, 2950f + ((n - 1) / 8) * 260f);
        if (indice >= 0) grafo.nodes[indice] = nuevo;
        else grafo.nodes.Add(nuevo);
        return nuevo;
    }

    static QuestData Quest(string id, int pasos)
    {
        Carpeta(CarpetaQuests);
        string ruta = $"{CarpetaQuests}/Q_{id}.asset";
        var quest = AssetDatabase.LoadAssetAtPath<QuestData>(ruta);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestData>();
            AssetDatabase.CreateAsset(quest, ruta);
        }
        quest.questId = id;
        quest.displayNameId = $"QUEST_{id}_NAME";
        quest.displayName = quest.displayNameId;
        quest.descriptionId = $"QUEST_{id}_DESC";
        quest.detailedDescriptionId = $"QUEST_{id}_DETAIL";
        quest.steps = new QuestData.Step[pasos];
        for (int i = 0; i < pasos; i++) quest.steps[i] = new QuestData.Step { descriptionId = $"QUEST_{id}_STEP" };
        EditorUtility.SetDirty(quest);
        return quest;
    }

    static DialogueAsset Dialogo(string nombre, params string[] claves)
    {
        Carpeta(CarpetaDialogos);
        string ruta = $"{CarpetaDialogos}/{nombre}.asset";
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dialogo == null)
        {
            dialogo = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dialogo, ruta);
        }
        var lineas = new DialogueLine[claves.Length];
        for (int i = 0; i < claves.Length; i++)
            lineas[i] = new DialogueLine { speakerNameId = Hablante, textId = claves[i], emotion = NPCEmotion.Neutral };
        dialogo.lines = lineas;
        dialogo.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dialogo);
        return dialogo;
    }

    static void AranaAvisaAlMorir(List<string> informe)
    {
        var raiz = PrefabUtility.LoadPrefabContents(Arana);
        try
        {
            var emisor = raiz.GetComponent<SignalEmitter>();
            if (emisor != null && emisor.eventKey != Senal)
                throw new InvalidOperationException($"Spider1 ya emite «{emisor.eventKey}»; no se sobrescribe.");
            if (emisor == null) emisor = raiz.AddComponent<SignalEmitter>();
            emisor.eventKey = Senal;
            emisor.trigger = SignalEmitter.TriggerType.OnEnemyDied;
            emisor.once = true;
            emisor.onlyIfListening = true;
            PrefabUtility.SaveAsPrefabAsset(raiz, Arana);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        informe.Add($"✓ Spider1 emite {Senal} al morir, solo si un contrato está contando.");
    }

    static void QuitarDeLaTienda(WardrobeItemSO prenda, List<string> informe)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Tienda) == null)
        {
            informe.Add("⚠ No existe ShopMenu_Renard: no se ha podido retirar el parche de la tienda.");
            return;
        }
        var raiz = PrefabUtility.LoadPrefabContents(Tienda);
        try
        {
            var controlador = raiz.GetComponentInChildren<ShopController>(true);
            var so = new SerializedObject(controlador);
            var stock = so.FindProperty("stock");
            int quitados = 0;
            for (int i = stock.arraySize - 1; i >= 0; i--)
            {
                var item = stock.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue as ItemData;
                if (item == null || item.wardrobeUnlock != prenda) continue;
                stock.DeleteArrayElementAtIndex(i);
                quitados++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, Tienda);
            informe.Add(quitados > 0 ? "✓ El parche pirata deja de venderse: es la recompensa del segundo contrato."
                                     : "= El parche pirata no estaba en la tienda de Renard.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static void Iconos(ItemData esencia, List<string> informe)
    {
        esencia.icon = CargarSprite("Assets/Art/UI/Items/esencia.png");
        EditorUtility.SetDirty(esencia);
        foreach (var (item, png) in new[] { ("Pequena", "pequena"), ("Grande", "grande") })
        {
            var bolsa = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/_ITEMS/IT_BolsaMonedas{item}.asset");
            if (bolsa == null) continue;
            bolsa.icon = CargarSprite($"Assets/Art/UI/Items/bolsa_monedas_{png}.png");
            EditorUtility.SetDirty(bolsa);
        }
        informe.Add("✓ Iconos de la Esencia y de las bolsas de monedas.");
    }

    static Sprite CargarSprite(string ruta)
    {
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var importador = AssetImporter.GetAtPath(ruta) as TextureImporter
            ?? throw new InvalidOperationException("Falta la imagen " + ruta);
        if (importador.textureType != TextureImporterType.Sprite || importador.spriteImportMode != SpriteImportMode.Single)
        {
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.alphaIsTransparency = true;
            importador.mipmapEnabled = false;
            importador.SaveAndReimport();
        }
        return Exigir<Sprite>(ruta);
    }

    static void Catalogo(List<string> informe)
    {
        var start = EditorSceneManager.GetSceneByPath(RutaStart);
        bool abiertaAqui = !start.isLoaded;
        if (abiertaAqui) start = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
        QuestCatalogAudit.AuditAndFix();
        if (start.isDirty) EditorSceneManager.SaveScene(start);
        if (abiertaAqui) EditorSceneManager.CloseScene(start, true);
        informe.Add("✓ Quests en el catálogo del QuestManager de Start.");
    }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    static void Archivar()
    {
        string texto = File.ReadAllText(Script);
        texto = texto.Replace("[MenuItem(\"El Sendero/Tiendas/Montar los contratos de caza de Renard\")]",
                              "[MenuItem(\"El Sendero/Archivo/Montar los contratos de caza de Renard\")]");
        File.WriteAllText(Script, texto, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(Script);
    }
}
#endif
