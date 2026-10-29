using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Cap1, tras probar la escolta de Eldran (24 oct 2026, INC-842):
///  - «cuando hacemos lo de seguir a un npc vamos demasiado lentos, un poco más rápido»: los dos
///    GuiarJugadorNode del capítulo suben de 2,6 a 3,0 m/s; el jugador ya puede igualar su paso
///    (GuiarJugadorNode.FraccionVelocidadJugador = 1) sin adelantarle, porque la separación detrás
///    del guía sigue mandando (INC-736).
///  - «Oliver debe salir del grupo cuando hablamos con Victoria y Eldran y actualmente me sigue»:
///    el nodo 14c (OLIVER_JOIN_PARTY) se había movido detrás de la compra de la estrella (INC-837),
///    así que el PERAS_START que lo saca del grupo ya había pasado. Vuelve delante de la discusión
///    de las peras: 14b → 14c → 14d y 20b → 20c. Oliver sigue a Will hasta Victoria y Eldran y, al
///    empezar la secuencia de las peras, se queda con Victoria y no acompaña a Eldran al bosque.
/// Se hace por AssetDatabase, no editando YAML (INC-441). Idempotente.
public static class Cap1EscoltaYOliver
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string RutaOliver = "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab";
    private const string RutaCharlaDelCamino = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DG_ELDRAN_CHARLA_VICTORIA.asset";
    private const float VelocidadDeEscolta = 3f;
    private const string SenalSalidaDeOliver = "PERAS_START";

    private const string Nodo14b = "aaccfcd4-a7bc-4732-b4b6-86e6c183db03"; // Busca a Eldran
    private const string Nodo14c = "486dfd45-dc63-438c-ae05-f2f8a045b36a"; // Oliver se une al grupo
    private const string Nodo14d = "36c16794-69b8-4e90-bb85-dc80d58b76ae"; // Tutorial minimapa
    private const string Nodo20b = "f393b1c0-87f6-4793-a705-a151a01af989"; // Completa WILL_MISSION1
    private const string Nodo20c = "5b0e2c8a-3f41-4d7e-9a1c-6e2f8d4b7c13"; // Eldran le llama

    [MenuItem("El Sendero/Archivo/Capítulo 1/Cap1: escolta más ágil y Oliver se queda con Victoria")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Cap1EscoltaYOliver] Sal de Play antes de ejecutar el menú: toca el grafo y el prefab.");
            return;
        }

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo == null)
        {
            Debug.LogError($"[Cap1EscoltaYOliver] Falta {RutaGrafo}.");
            return;
        }

        var informe = new List<string>();
        Undo.RecordObject(grafo, "Cap1: escolta más ágil y Oliver se queda con Victoria");

        AcelerarEscolta(grafo, informe);
        DevolverOliverAntesDeLasPeras(grafo, informe);
        QuitarLineaDeOliverDeLaCharla(informe);

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        ComprobarSalidaDeOliver(informe);

        Debug.Log("[Cap1EscoltaYOliver] ✓ Hecho:\n- " + string.Join("\n- ", informe) +
                  "\nSi tienes abierta la ventana del grafo, ciérrala y vuelve a abrirla.");
    }

    private static void AcelerarEscolta(NarrativeGraph grafo, List<string> informe)
    {
        int cambiados = 0;
        foreach (var nodo in grafo.nodes)
        {
            if (nodo is not GuiarJugadorNode guia || guia.npcId != "NPC_Eldran") continue;
            if (guia.velocidad >= VelocidadDeEscolta) continue;
            guia.velocidad = VelocidadDeEscolta;
            cambiados++;
        }

        if (cambiados == 0)
            informe.Add($"= La escolta de Eldran ya va a {VelocidadDeEscolta:0.#} m/s.");
        else
            informe.Add($"✓ Escolta de Eldran a {VelocidadDeEscolta:0.#} m/s ({cambiados} nodo(s)).");
    }

    /// Devuelve OLIVER_JOIN_PARTY a su sitio: antes de la discusión de las peras, para que
    /// PERAS_START lo saque del grupo y no siga al jugador por el bosque.
    private static void DevolverOliverAntesDeLasPeras(NarrativeGraph grafo, List<string> informe)
    {
        var n14b = grafo.FindNode(Nodo14b);
        var n14c = grafo.FindNode(Nodo14c);
        var n14d = grafo.FindNode(Nodo14d);
        var n20b = grafo.FindNode(Nodo20b);
        var n20c = grafo.FindNode(Nodo20c);

        if (n14b == null || n14c == null || n14d == null || n20b == null || n20c == null)
        {
            informe.Add("⚠ No encuentro 14b, 14c, 14d, 20b o 20c: no he tocado la unión de Oliver.");
            return;
        }

        bool yaBien = n14b.outputs.Contains(Nodo14c)
                      && n14c.outputs.Contains(Nodo14d)
                      && n20b.outputs.Contains(Nodo20c);
        if (yaBien)
        {
            informe.Add("= Oliver ya se une antes de la discusión de las peras (14c).");
            return;
        }

        bool movido = n14b.outputs.Contains(Nodo14d)
                      && n14c.outputs.Contains(Nodo20c)
                      && n14b.outputs.Count == 1
                      && n14c.outputs.Count == 1
                      && n20b.outputs.Count == 1
                      && n20b.outputs.Contains(Nodo14c);
        if (!movido)
        {
            informe.Add("⚠ El cableado de 14b/14c/14d/20b/20c no es el esperado: no he tocado la unión de Oliver. " +
                        "Revisa el grafo a mano.");
            return;
        }

        n14b.outputs[0] = Nodo14c;
        n14c.outputs[0] = Nodo14d;
        n20b.outputs[0] = Nodo20c;
        n14c.position = n14b.position + new Vector2(380f, 0f);

        informe.Add("✓ 14c vuelve delante de las peras: 14b → 14c → 14d y 20b → 20c. Oliver se queda " +
                    "con Victoria al empezar la discusión.");
        informe.Add("El menú antiguo de INC-837 está archivado en «Versiones antiguas»: no lo ejecutes, " +
                    "deshace este cambio.");
    }

    /// La charla del camino (DG_ELDRAN_CHARLA_VICTORIA) tenía una réplica de Oliver. Como se queda
    /// con Victoria, su bocadillo y su voz saldrían desde el mercado mientras Will va al bosque:
    /// se quita la línea. La charla se queda con Will y Eldran.
    private static void QuitarLineaDeOliverDeLaCharla(List<string> informe)
    {
        var charla = AssetDatabase.LoadAssetAtPath<DialogueAsset>(RutaCharlaDelCamino);
        if (charla?.lines == null)
        {
            informe.Add($"⚠ No encuentro {RutaCharlaDelCamino}: revisa que la charla del camino no tenga a Oliver.");
            return;
        }

        var quedan = new List<DialogueLine>();
        foreach (var linea in charla.lines)
            if (linea.speakerNameId != "CHAR_OLIVER") quedan.Add(linea);

        if (quedan.Count == charla.lines.Length)
        {
            informe.Add("= La charla del camino ya no tenía a Oliver.");
            return;
        }

        Undo.RecordObject(charla, "Cap1: charla del camino sin Oliver");
        charla.lines = quedan.ToArray();
        EditorUtility.SetDirty(charla);
        informe.Add("✓ Quitada la réplica de Oliver de DG_ELDRAN_CHARLA_VICTORIA (no viene en la escolta).");
    }

    /// La salida la dispara el PartyMembershipSignal del prefab de Oliver. Se comprueba (y se añade
    /// si falta) para que el arreglo no dependa de que alguien la haya borrado sin querer.
    private static void ComprobarSalidaDeOliver(List<string> informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaOliver);
        if (prefab == null)
        {
            informe.Add($"⚠ No encuentro {RutaOliver}: comprueba a mano que Oliver sale del grupo con {SenalSalidaDeOliver}.");
            return;
        }

        if (TieneSalida(prefab))
        {
            informe.Add($"= Oliver ya sale del grupo con {SenalSalidaDeOliver}.");
            return;
        }

        var raiz = PrefabUtility.LoadPrefabContents(RutaOliver);
        try
        {
            var senal = raiz.AddComponent<Game.NPC.PartyMembershipSignal>();
            var so = new SerializedObject(senal);
            so.FindProperty("narrativeEventKey").stringValue = SenalSalidaDeOliver;
            so.FindProperty("action").enumValueIndex = (int)Game.NPC.PartyMembershipSignal.SignalAction.Leave;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaOliver);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }

        informe.Add($"✓ Añadida a Oliver la salida del grupo con {SenalSalidaDeOliver}.");
    }

    private static bool TieneSalida(GameObject prefab)
    {
        foreach (var senal in prefab.GetComponentsInChildren<Game.NPC.PartyMembershipSignal>(true))
        {
            var so = new SerializedObject(senal);
            if (so.FindProperty("narrativeEventKey")?.stringValue != SenalSalidaDeOliver) continue;
            if (so.FindProperty("action")?.enumValueIndex == (int)Game.NPC.PartyMembershipSignal.SignalAction.Leave)
                return true;
        }
        return false;
    }
}
