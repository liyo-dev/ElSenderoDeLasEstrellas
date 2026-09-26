using System.Collections.Generic;
using System.Linq;
using Game.NPC.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Cap1: Eldran ya no pide la caja en mitad del pueblo. Dice «Ven, sígueme» y el jugador le sigue.
/// Primero paran en el punto de guardado de la entrada del reino, donde Eldran lo explica y
/// Oliver hace una réplica. Después siguen hasta la marca del bosque, donde Eldran le pide la caja.
/// Así la entrega y el Despertar de la Estrella ocurren fuera del pueblo. Ver INC-460.
///
/// Menús:
///  - «poner aquí la parada del punto de guardado» y «poner aquí la marca de Eldran en el bosque»:
///    crean o mueven el SpawnAnchor correspondiente en MainWorld, en el objeto seleccionado o, si
///    no hay ninguno (o es otra marca), donde mira la vista de escena.
///  - «añadir aquí un punto de paso»: pone una marca nueva y la añade a la ruta de 20d o 20d3, para
///    que Eldran vaya por el camino y no por el atajo del NavMesh (INC-465). «quitar los puntos
///    de paso» las saca de la ruta.
///  - «Eldran te lleva al bosque»: reparte los diálogos, monta en el grafo
///    20c → 20d → 20d2 → 20d3 → 20e → 21 y quita de la configuración antigua de Eldran la escolta
///    al punto de guardado de después del Demonio, que ya se explica aquí.
/// Se hace por AssetDatabase y no escribiendo el YAML: Unity tiene los assets en memoria (INC-441).
/// Idempotente.
public static class Cap1EldranTeLlevaAlBosque
{
    public const string AnchorBosque = "Eldran_Bosque";
    public const string AnchorParada = "Eldran_PuntoGuardado";
    private const string EscenaMundo = "MainWorld";

    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string Nodo20c = "5b0e2c8a-3f41-4d7e-9a1c-6e2f8d4b7c13";
    private const string Nodo21 = "18dc4ae9-acd5-4ec7-b3ec-389ed5af4fba";
    private const string Nodo20d = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e01";
    private const string Nodo20e = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e02";
    private const string Nodo20d2 = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e03";
    private const string Nodo20d3 = "c7d1e2a4-5b36-4f18-9e20-3a4b5c6d7e04";

    private const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_EldranCaja.asset";
    private const string CarpetaEldran = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/";
    private const string RutaDialogoCaja = CarpetaEldran + "DG_ELDRAN_CAJA.asset";
    private const string RutaDialogoSigueme = CarpetaEldran + "DG_ELDRAN_SIGUEME.asset";
    private const string RutaDialogoParada = CarpetaEldran + "DG_ELDRAN_PUNTO_GUARDADO.asset";

    private const string RutaConfigAntigua = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_Eldran.asset";
    private const string AnchorGuardadoAntiguo = "Woods_Entrance_SavePoint";
    private const string DialogoVenConmigo = "DLG_ELDRAN_MISSION3_COMEWITHME_01";
    private const string DialogoGuardadoAntiguo = "DLG_ELDRAN_SAVEPOINT_01";

    // ── Marcas ──────────────────────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Narrativa/Cap1: poner aquí la parada del punto de guardado (entrada del reino)")]
    public static void PonerParada() => PonerMarca(AnchorParada, "Anchor_Eldran_PuntoGuardado");

    [MenuItem("El Sendero/Narrativa/Cap1: poner aquí la marca de Eldran en el bosque")]
    public static void PonerMarcaBosque() => PonerMarca(AnchorBosque, "Anchor_Eldran_Bosque");

    [MenuItem("El Sendero/Narrativa/Cap1: añadir aquí un punto de paso (camino al punto de guardado)")]
    public static void PasoHaciaParada() => AnadirPuntoDePaso(Nodo20d, AnchorParada);

    [MenuItem("El Sendero/Narrativa/Cap1: añadir aquí un punto de paso (camino al bosque)")]
    public static void PasoHaciaBosque() => AnadirPuntoDePaso(Nodo20d3, AnchorBosque);

    [MenuItem("El Sendero/Narrativa/Cap1: quitar los puntos de paso de Eldran")]
    public static void QuitarPuntosDePaso()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo == null) { Debug.LogError($"[Cap1EldranTeLlevaAlBosque] Falta {RutaGrafo}."); return; }
        Undo.RecordObject(grafo, "Quitar los puntos de paso de Eldran");
        int quitados = 0;
        foreach (var guid in new[] { Nodo20d, Nodo20d3 })
            if (grafo.FindNode(guid) is GuiarJugadorNode n && n.puntosDePaso != null)
            {
                quitados += n.puntosDePaso.Count;
                n.puntosDePaso.Clear();
            }
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cap1EldranTeLlevaAlBosque] ✓ {quitados} punto(s) de paso quitados de 20d y 20d3. " +
                  "Las marcas siguen en la escena por si las quieres volver a usar; bórralas a mano si no.");
    }

    /// Pone una marca nueva donde está lo seleccionado (o donde mira la vista de escena) y la añade
    /// al final de los puntos de paso del nodo: Eldran pasará por ella antes de ir a su destino.
    /// Se ponen en el orden en que se quiere que las recorra.
    private static void AnadirPuntoDePaso(string guidNodo, string destino)
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (!(grafo?.FindNode(guidNodo) is GuiarJugadorNode nodo))
        {
            EditorUtility.DisplayDialog("Punto de paso",
                "No encuentro el nodo de «Guiar al jugador» en Cap1. Pasa antes el menú «Cap1: Eldran te lleva al bosque (caja)».", "Vale");
            return;
        }
        nodo.puntosDePaso ??= new List<string>();

        string id;
        int n = nodo.puntosDePaso.Count + 1;
        do { id = $"{destino}_Paso{n++}"; } while (nodo.puntosDePaso.Contains(id));

        var marca = PonerMarca(id, "Anchor_" + id);
        if (marca == null) return;

        Undo.RecordObject(grafo, "Añadir punto de paso");
        nodo.puntosDePaso.Add(id);
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cap1EldranTeLlevaAlBosque] ✓ Punto de paso {nodo.puntosDePaso.Count} camino de '{destino}': '{id}'. " +
                  $"Ruta de Eldran: {string.Join(" → ", nodo.puntosDePaso)} → {destino}. Guarda {EscenaMundo} (Ctrl+S).");
    }

    private static SpawnAnchor PonerMarca(string anchorId, string nombreObjeto)
    {
        Scene mundo = SceneManager.GetSceneByName(EscenaMundo);
        if (!mundo.IsValid() || !mundo.isLoaded)
        {
            EditorUtility.DisplayDialog("Marca de Eldran",
                $"Abre la escena {EscenaMundo} antes de poner la marca.", "Vale");
            return null;
        }

        Vector3 punto;
        var seleccion = Selection.activeTransform;
        if (seleccion != null && seleccion.gameObject.scene.IsValid() && seleccion.GetComponent<SpawnAnchor>() == null)
        {
            punto = seleccion.position;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            punto = SceneView.lastActiveSceneView.pivot;
        }
        else
        {
            EditorUtility.DisplayDialog("Marca de Eldran",
                "Selecciona en la escena un objeto junto al sitio que quieres, o centra la vista de escena en él, y vuelve a pulsar el menú.", "Vale");
            return null;
        }

        // Al suelo: la marca es un punto de NavMesh para Eldran.
        if (Physics.Raycast(punto + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f,
                ~0, QueryTriggerInteraction.Ignore))
            punto = hit.point;
        if (UnityEngine.AI.NavMesh.SamplePosition(punto, out var nav, 4f, UnityEngine.AI.NavMesh.AllAreas))
            punto = nav.position;
        else if (UnityEngine.AI.NavMesh.SamplePosition(punto, out var lejos, 80f, UnityEngine.AI.NavMesh.AllAreas))
            Debug.LogWarning($"[Cap1EldranTeLlevaAlBosque] '{anchorId}' está fuera del NavMesh: el trozo navegable más cercano " +
                $"queda a {Vector3.Distance(punto, lejos.position):F0} m, en {lejos.position}. Eldran no podrá llegar hasta que " +
                "se amplíe el NavMesh hasta ahí o se mueva la marca más cerca.");
        else
            Debug.LogWarning($"[Cap1EldranTeLlevaAlBosque] '{anchorId}' está fuera del NavMesh y no hay nada navegable a menos de " +
                "80 m. Eldran no podrá llegar hasta que se amplíe el NavMesh hasta ahí.");

        SpawnAnchor anchor = BuscarMarca(mundo, anchorId);
        if (anchor == null)
        {
            var go = new GameObject(nombreObjeto);
            Undo.RegisterCreatedObjectUndo(go, "Marca de Eldran");
            SceneManager.MoveGameObjectToScene(go, mundo);
            anchor = go.AddComponent<SpawnAnchor>();
            anchor.anchorId = anchorId;
        }
        else
        {
            Undo.RecordObject(anchor.transform, "Mover la marca de Eldran");
        }

        anchor.transform.position = punto;
        EditorSceneManager.MarkSceneDirty(mundo);
        Selection.activeGameObject = anchor.gameObject;
        EditorGUIUtility.PingObject(anchor.gameObject);

        Debug.Log($"[Cap1EldranTeLlevaAlBosque] ✓ Marca '{anchorId}' en {punto}. Guarda {EscenaMundo} (Ctrl+S). " +
                  "Si quieres afinarla, arrastra el objeto seleccionado.");
        return anchor;
    }

    private static SpawnAnchor BuscarMarca(Scene escena, string anchorId)
    {
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var a in raiz.GetComponentsInChildren<SpawnAnchor>(true))
                if (a.anchorId == anchorId) return a;
        return null;
    }

    // ── Diálogos, secuencia y grafo ─────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Narrativa/Cap1: Eldran te lleva al bosque (caja)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        var seq = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        var caja = AssetDatabase.LoadAssetAtPath<DialogueAsset>(RutaDialogoCaja);
        if (grafo == null || seq == null || caja == null)
        {
            Debug.LogError("[Cap1EldranTeLlevaAlBosque] Falta " +
                (grafo == null ? RutaGrafo : seq == null ? RutaSecuencia : RutaDialogoCaja) + ".");
            return;
        }

        var informe = new List<string>();

        var sigueme = CrearDialogo(RutaDialogoSigueme, informe,
            ("CHAR_ELDRAN", "DLG_ELDRAN_CAJA_01"),
            ("CHAR_ELDRAN", "DLG_ELDRAN_CAJA_05"));
        var parada = CrearDialogo(RutaDialogoParada, informe,
            ("CHAR_ELDRAN", "DLG_ELDRAN_SAVEPOINT_01"),
            ("CHAR_ELDRAN", "DLG_ELDRAN_SAVEPOINT_02"),
            // Sin la réplica de Oliver: sale del grupo en PERAS_START y no vuelve en el Cap. 1,
            // así que no está en la parada (26 sep 2026).
            ("CHAR_ELDRAN", "DLG_ELDRAN_SAVEPOINT_04"));

        RepartirDialogoCaja(caja, informe);
        CambiarDialogoDeLaSecuencia(seq, sigueme, informe);
        MontarGrafo(grafo, caja, parada, informe);
        QuitarEscoltaAntigua(informe);

        AssetDatabase.SaveAssets();
        Debug.Log("[Cap1EldranTeLlevaAlBosque] ✓ Hecho:\n- " + string.Join("\n- ", informe) +
                  "\nSi tienes abierta la ventana del grafo, ciérrala y vuelve a abrirla. " +
                  "Faltan las marcas si no las has puesto: «Cap1: poner aquí la parada del punto de guardado» " +
                  "y «Cap1: poner aquí la marca de Eldran en el bosque».");
    }

    private static DialogueAsset CrearDialogo(string ruta, List<string> informe, params (string quien, string clave)[] lineas)
    {
        var d = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
        if (d != null)
        {
            // Si el guion ha cambiado, se deja con las líneas de ahora; las que siguen conservan
            // lo que se les haya ajustado a mano (emoción, gesto...).
            var claves = lineas.Select(l => l.clave).ToArray();
            var actuales = (d.lines ?? new DialogueLine[0]).Select(l => l.textId).ToArray();
            if (claves.SequenceEqual(actuales))
            {
                informe.Add($"{nombre} ya existía y está al día.");
                return d;
            }
            Undo.RecordObject(d, "Actualizar diálogo");
            var antes = d.lines ?? new DialogueLine[0];
            d.lines = lineas.Select(l =>
            {
                int i = System.Array.FindIndex(antes, x => x.textId == l.clave);
                return i >= 0 ? antes[i] : new DialogueLine { speakerNameId = l.quien, textId = l.clave };
            }).ToArray();
            EditorUtility.SetDirty(d);
            informe.Add($"{nombre} actualizado: {string.Join(", ", actuales)} → {string.Join(", ", claves)}.");
            return d;
        }

        d = ScriptableObject.CreateInstance<DialogueAsset>();
        d.lines = lineas.Select(l => new DialogueLine { speakerNameId = l.quien, textId = l.clave }).ToArray();
        AssetDatabase.CreateAsset(d, ruta);
        informe.Add($"Creado {nombre} ({lineas.Length} líneas).");
        return d;
    }

    /// La caja ya se pide en el bosque: se quita la línea 01, que pasa al pueblo.
    private static void RepartirDialogoCaja(DialogueAsset caja, List<string> informe)
    {
        if (caja.lines == null || caja.lines.Length == 0 || caja.lines[0].textId != "DLG_ELDRAN_CAJA_01")
        {
            informe.Add("DG_ELDRAN_CAJA ya empezaba en la 02.");
            return;
        }

        Undo.RecordObject(caja, "DG_ELDRAN_CAJA sin la 01");
        caja.lines = caja.lines.Skip(1).ToArray();
        EditorUtility.SetDirty(caja);
        informe.Add("DG_ELDRAN_CAJA: quitada la 01 (queda 02-03-04, se dice en el bosque).");
    }

    private static void CambiarDialogoDeLaSecuencia(SequenceDefinition seq, DialogueAsset sigueme, List<string> informe)
    {
        foreach (var fase in seq.phases)
        {
            foreach (var beat in fase.beats)
            {
                if (beat is not DialogueBeat db) continue;
                if (db.dialogue == sigueme)
                {
                    informe.Add("SEQ_EldranCaja ya usaba DG_ELDRAN_SIGUEME.");
                    return;
                }

                Undo.RecordObject(seq, "SEQ_EldranCaja: Ven, sígueme");
                db.dialogue = sigueme;
                db.note = "Menos mal que te he alcanzado + Ven, sigueme. La caja se pide en el bosque (Cap1 20e). Caja de dialogo, avanza el jugador.";
                db.reactions = new List<DialogueReaction>
                {
                    new DialogueReaction { actorId = "Player", gestures = new[] { "", "HeadNod01" }, emotions = new NPCEmotion[0] },
                    new DialogueReaction { actorId = "NPC_Eldran", gestures = new[] { "", "" }, emotions = new NPCEmotion[0] },
                };
                seq.summary = "Nada mas comprar la estrella, Eldran llama a Will desde lejos, se acerca como Oliver en " +
                    "SEQ_OliverSaludo y le dice que le siga (caja de dialogo, avanza el jugador). Por el camino paran en el " +
                    "punto de guardado de la entrada del reino y la caja se la pide ya en el bosque (Cap1 20d-20e).";
                EditorUtility.SetDirty(seq);
                informe.Add("SEQ_EldranCaja: el diálogo pasa a DG_ELDRAN_SIGUEME (Will asiente al «Ven, sígueme»).");
                return;
            }
        }
        informe.Add("⚠ SEQ_EldranCaja no tiene DialogueBeat: no se ha cambiado.");
    }

    /// Deja la cadena 20c → 20d → 20d2 → 20d3 → 20e → 21, creando los nodos que falten.
    private static void MontarGrafo(NarrativeGraph grafo, DialogueAsset caja, DialogueAsset parada, List<string> informe)
    {
        var n20c = grafo.FindNode(Nodo20c);
        if (n20c == null)
        {
            informe.Add("⚠ No existe el nodo 20c en Cap1: el grafo no se ha tocado.");
            return;
        }

        // Lo que venía después de 20c antes de este cambio (o de 20e, si ya se había montado).
        var n20eExistente = grafo.FindNode(Nodo20e);
        string siguiente = n20eExistente != null
            ? n20eExistente.GetOutputGuid(0) ?? Nodo21
            : n20c.GetOutputGuid(0) ?? Nodo21;

        Undo.RecordObject(grafo, "Cap1: Eldran te lleva al bosque");
        Vector2 p = n20c.position;

        var n20d = Asegurar(grafo, Nodo20d, () => new GuiarJugadorNode { npcId = "NPC_Eldran" });
        n20d.displayTitle = "20d.- Sigue a Eldran hasta el punto de guardado de la entrada";
        ((GuiarJugadorNode)n20d).anchorId = AnchorParada;
        Colocar(n20d, n20c, p + new Vector2(0f, 220f), true);

        var n20d2 = Asegurar(grafo, Nodo20d2, () => new PlayDialogueNode { npcId = "NPC_Eldran" });
        n20d2.displayTitle = "20d2.- Eldran explica el punto de guardado (DG_ELDRAN_PUNTO_GUARDADO)";
        ((PlayDialogueNode)n20d2).dialogue = parada;
        Colocar(n20d2, n20c, p + new Vector2(0f, 440f), false);

        var n20d3 = Asegurar(grafo, Nodo20d3, () => new GuiarJugadorNode { npcId = "NPC_Eldran" });
        n20d3.displayTitle = "20d3.- Sigue a Eldran hasta el bosque";
        ((GuiarJugadorNode)n20d3).anchorId = AnchorBosque;
        Colocar(n20d3, n20c, p + new Vector2(0f, 660f), true);

        var n20e = Asegurar(grafo, Nodo20e, () => new PlayDialogueNode { npcId = "NPC_Eldran" });
        n20e.displayTitle = "20e.- En el bosque, Eldran pide la caja (DG_ELDRAN_CAJA)";
        ((PlayDialogueNode)n20e).dialogue = caja;
        Colocar(n20e, n20c, p + new Vector2(0f, 880f), false);

        n20c.outputs = new List<string> { Nodo20d };
        n20c.displayTitle = "20c.- Eldran le llama: «Ven, sígueme» (SEQ_EldranCaja)";
        n20d.outputs = new List<string> { Nodo20d2 };
        n20d2.outputs = new List<string> { Nodo20d3 };
        n20d3.outputs = new List<string> { Nodo20e };
        n20e.outputs = new List<string> { siguiente };

        EditorUtility.SetDirty(grafo);
        informe.Add("Cap1: 20c → 20d (guía al punto de guardado) → 20d2 (lo explica) → 20d3 (guía al bosque) → 20e (la caja) → 21.");
    }

    private static NarrativeNode Asegurar(NarrativeGraph grafo, string guid, System.Func<NarrativeNode> crear)
    {
        var n = grafo.FindNode(guid);
        if (n != null) return n;
        n = crear();
        n.guid = guid;
        grafo.nodes.Add(n);
        return n;
    }

    private static void Colocar(NarrativeNode n, NarrativeNode referencia, Vector2 posicion, bool bloquearGuardado)
    {
        n.chapter = referencia.chapter;
        n.position = posicion;
        n.blockSaving = bloquearGuardado;
    }

    /// Tras el Demonio, la configuración antigua de Eldran (congelada) le llevaba al punto de
    /// guardado del bosque para explicarlo. Ahora se explica antes, así que se quitan de esa
    /// cadena el «ven conmigo», la escolta y la explicación. Queda el «bien hecho» y la ida a casa.
    private static void QuitarEscoltaAntigua(List<string> informe)
    {
        var config = AssetDatabase.LoadAssetAtPath<NPCInteractiveNarrativeConfig>(RutaConfigAntigua);
        if (config == null || config.conditionalNarratives == null)
        {
            informe.Add("No está la configuración antigua de Eldran: nada que quitar.");
            return;
        }

        int quitadas = 0;
        foreach (var cn in config.conditionalNarratives)
        {
            if (cn?.narrativeChain == null) continue;
            var queda = cn.narrativeChain.Where(e => !EsDeLaExplicacionAntigua(e)).ToArray();
            if (queda.Length == cn.narrativeChain.Length) continue;

            Undo.RecordObject(config, "Eldran: quitar la escolta antigua al punto de guardado");
            quitadas += cn.narrativeChain.Length - queda.Length;
            cn.narrativeChain = queda;
        }

        if (quitadas == 0)
        {
            informe.Add("La escolta antigua al punto de guardado ya estaba quitada.");
            return;
        }

        EditorUtility.SetDirty(config);
        informe.Add($"Configuración antigua de Eldran: quitados {quitadas} pasos (ven conmigo, escolta a {AnchorGuardadoAntiguo} y explicación).");
    }

    private static bool EsDeLaExplicacionAntigua(NarrativeChainEntry e)
    {
        if (e == null) return false;
        if (e.actionType == NarrativeActionType.LeadPlayerToAnchor
            && e.targetAnchorName == AnchorGuardadoAntiguo) return true;
        if (e.actionType != NarrativeActionType.Dialogue || e.dialogue?.lines == null
            || e.dialogue.lines.Length == 0) return false;
        string primera = e.dialogue.lines[0].textId;
        return primera == DialogoVenConmigo || primera == DialogoGuardadoAntiguo;
    }
}
