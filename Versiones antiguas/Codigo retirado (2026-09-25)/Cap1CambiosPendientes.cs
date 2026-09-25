using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aplica a Cap1.asset los cambios del 25 sep 2026 DESDE EL EDITOR (INC-439 / INC-442).
///
/// Por qué un menú y no el YAML a mano: Unity tiene Cap1 cargado en memoria (la ventana del grafo
/// lo marca como modificado) y esa copia manda sobre el archivo. Los cambios escritos por fuera en
/// el disco no llegaban al juego, y el primer guardado los pisaba (INC-441; misma lección que
/// PrologoSceneNodesWiring, 17 sep). Por el AssetDatabase no hay ese problema.
///
/// Idempotente: si algo ya está aplicado, no lo toca y lo dice.
///
///   1. Nodo 19 → StartQuest WILL_MISSION1 «Algo inútil» (antes, tutorial TUTORIAL_MARKET).
///   2. Nodo 20 → espera ESTRELLA_COMPRADA (antes MARKET_DONE).
///   3. Nodos nuevos 20b (completa WILL_MISSION1) y 20c (Eldran viene a pedir la caja), antes del 21.
///   4. Avisos informativos de tutorial: se quitan solos cuando se cumple lo que dicen.
/// </summary>
public static class Cap1CambiosPendientes
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";
    private const string Nodo19 = "a856e804-7ab5-4aaf-a241-774bce56c27f";
    private const string Nodo20 = "95a7a650-50b9-4797-9dcd-e9bc33ad04dd";
    private const string Nodo20b = "f393b1c0-87f6-4793-a705-a151a01af989";
    private const string Nodo20c = "5b0e2c8a-3f41-4d7e-9a1c-6e2f8d4b7c13";
    private const string Nodo21 = "18dc4ae9-acd5-4ec7-b3ec-389ed5af4fba";
    private const string DialogoCaja = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DG_ELDRAN_CAJA.asset";

    // Aviso informativo → señal que significa «ya está hecho».
    private static readonly (string guid, string senal)[] CierresDeAvisos =
    {
        ("05d8c990-c631-4ddd-8aff-4092490214c8", "WILL_EXITS_HOUSE"),        // 7.- Mueve el joystick
        ("36c16794-69b8-4e90-bb85-dc80d58b76ae", "WILL_REACHED_ELDRAN"),     // 14d.- Minimapa, busca a Eldran
        ("0d50f50f-4be3-434d-a6b4-fa6ce0694d8f", "CAP1_CAJA_ELDRAN"),        // 22.- Minimapa, la caja
        ("da4a9b32-670c-4724-ab44-6743e2c728d1", "NPC_INTERACT_NPC_Eldran"), // 25.- Icono «habla conmigo»
    };

    [MenuItem("El Sendero/Archivo/Narrativa/Cap1: aplicar «Algo inútil» + Eldran pide la caja + avisos que se cierran solos")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        if (grafo == null) { Debug.LogError("[Cap1CambiosPendientes] No se puede cargar " + Ruta); return; }

        Undo.RecordObject(grafo, "Cap1: cambios INC-439/442");
        var log = new StringBuilder("[Cap1CambiosPendientes]\n");

        // 1. Nodo 19
        var n19 = grafo.FindNode(Nodo19);
        if (n19 == null) log.AppendLine("✗ No encuentro el nodo 19.");
        else if (n19 is StartQuestNode sq && sq.questId == "WILL_MISSION1") log.AppendLine("= 19 ya era StartQuest WILL_MISSION1.");
        else
        {
            var nuevo = new StartQuestNode
            {
                guid = n19.guid, position = n19.position, chapter = n19.chapter, blockSaving = true,
                displayTitle = "19.- ALGO INUTIL - compra en el mercado (WILL_MISSION1)",
                questId = "WILL_MISSION1",
            };
            nuevo.outputs = new System.Collections.Generic.List<string> { Nodo20 };
            grafo.nodes[grafo.nodes.IndexOf(n19)] = nuevo;
            log.AppendLine("✓ 19 → StartQuest WILL_MISSION1.");
        }

        // 3. Nodos nuevos (antes que el 2, que apunta a ellos)
        bool creados = false;
        if (grafo.FindNode(Nodo20b) == null)
        {
            // Hueco: todo lo que quedaba a la derecha del 20 se aparta dos casillas.
            var n20pos = grafo.FindNode(Nodo20)?.position ?? new Vector2(9880, 0);
            foreach (var n in grafo.nodes.Where(n => n != null && n.position.x > n20pos.x + 1f))
                n.position += new Vector2(760, 0);

            var c = new CompleteQuestStepsNode
            {
                guid = Nodo20b, chapter = "Cap. 1", position = n20pos + new Vector2(380, 0),
                displayTitle = "20b.- Completa WILL_MISSION1", questId = "WILL_MISSION1", completeQuest = true,
            };
            c.outputs = new System.Collections.Generic.List<string> { Nodo20c };
            grafo.nodes.Add(c);
            creados = true;
            log.AppendLine("✓ Creado 20b (completa WILL_MISSION1).");
        }
        else log.AppendLine("= 20b ya existía.");

        if (grafo.FindNode(Nodo20c) == null)
        {
            var pos = grafo.FindNode(Nodo20b).position + new Vector2(380, 0);
            var e = new NpcAcudeYHablaNode
            {
                guid = Nodo20c, chapter = "Cap. 1", position = pos, blockSaving = true,
                displayTitle = "20c.- Eldran viene a pedir lo de la caja (DG_ELDRAN_CAJA)",
                npcId = "NPC_Eldran", llamadaKey = "DLG_ELDRAN_CAJA_LLAMADA",
                dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DialogoCaja),
            };
            e.outputs = new System.Collections.Generic.List<string> { Nodo21 };
            if (e.dialogo == null) log.AppendLine("✗ No encuentro " + DialogoCaja + " (asígnalo a mano en el nodo 20c).");
            grafo.nodes.Add(e);
            creados = true;
            log.AppendLine("✓ Creado 20c (Eldran viene a pedir la caja).");
        }
        else log.AppendLine("= 20c ya existía.");

        // 2. Nodo 20
        if (grafo.FindNode(Nodo20) is WaitCustomEventNode w)
        {
            w.eventKey = "ESTRELLA_COMPRADA";
            w.displayTitle = "20.- Espera la compra de la estrella (ESTRELLA_COMPRADA)";
            w.outputs = new System.Collections.Generic.List<string> { Nodo20b };
            log.AppendLine("✓ 20 espera ESTRELLA_COMPRADA → 20b.");
        }
        else log.AppendLine("✗ El nodo 20 no es un WaitCustomEventNode.");

        // 4. Avisos que se cierran solos
        foreach (var (guid, senal) in CierresDeAvisos)
        {
            if (grafo.FindNode(guid) is TutorialPromptNode t)
            {
                t.cerrarConSenal = senal;
                t.cerrarAlEmpezarCinematica = true;
                log.AppendLine($"✓ Aviso '{t.displayTitle}' se cierra con {senal}.");
            }
            else log.AppendLine($"✗ No encuentro el aviso {guid}.");
        }

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        log.AppendLine(creados ? "\nNodos nuevos creados y el resto desplazado a la derecha." : "");
        log.AppendLine("Cadena desde el 18:");
        var actual = grafo.FindNode("041162d9-cc08-4d17-ae7c-6067257e952b");
        for (int i = 0; i < 7 && actual != null; i++)
        {
            log.AppendLine($"  {actual.GetType().Name} — {actual.displayTitle}");
            actual = actual.outputs != null && actual.outputs.Count > 0 ? grafo.FindNode(actual.outputs[0]) : null;
        }
        Debug.Log(log.ToString());
    }
}
