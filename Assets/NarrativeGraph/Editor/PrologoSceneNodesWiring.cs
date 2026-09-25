using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inserta en Cap1.asset los dos nodos de escena aditiva del prólogo (INC-249), pero **desde el
/// propio Editor**, no escribiendo el YAML por fuera.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El 17 sep 2026 los dos nodos se escribieron a mano en el YAML de `Cap1.asset`. El archivo en
/// disco quedaba correcto (40 nodos, los dos `AdditiveSceneNode`, el `StartNode` apuntando al de
/// carga — verificado varias veces), pero Unity seguía viendo **38 nodos y cero
/// `AdditiveSceneNode`**, incluso después de un `ImportAsset(ForceUpdate | ForceSynchronousImport)`.
/// Es decir: Unity tenía el ScriptableObject cargado en memoria y esa copia mandaba sobre el
/// archivo. Mientras eso siga así, cualquier edición externa de ese asset es papel mojado — y, peor,
/// el primer "Save Project" la sobrescribe con la versión de memoria.
///
/// La lección, y la razón de este script: **un `.asset` que Unity puede tener cargado se modifica
/// con el AssetDatabase, no escribiendo su YAML por fuera.** El mismo patrón que
/// `SequenceInputActionWiring`, que sí funcionó a la primera.
///
/// QUÉ HACE (idempotente — se puede ejecutar dos veces sin duplicar nada):
///   1. Busca el `StartNode`, el nodo que emite `PROLOGUE_START` y el que espera `PROLOGUE_DONE`.
///   2. Si ya hay un `AdditiveSceneNode` para 'Prologo_Valle', no crea nada y lo dice.
///   3. Inserta el de CARGA entre el StartNode y el que emite `PROLOGUE_START`, y el de DESCARGA
///      entre el que espera `PROLOGUE_DONE` y el nodo que venía después.
///   4. Guarda con `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()` e imprime la cadena
///      resultante para poder comprobarla de un vistazo.
/// </summary>
public static class PrologoSceneNodesWiring
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";
    private const string Escena = "Prologo_Valle";
    private const string SenalInicio = "PROLOGUE_START";
    private const string SenalFin = "PROLOGUE_DONE";

    [MenuItem("El Sendero/Archivo/Narrativa/Prólogo: insertar nodos de carga/descarga de Prologo_Valle")]
    public static void Insertar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        if (grafo == null) { Debug.LogError($"[PrologoSceneNodes] No se pudo cargar {Ruta}"); return; }

        if (grafo.nodes.OfType<AdditiveSceneNode>().Any(n => n.sceneName == Escena))
        {
            Debug.Log("[PrologoSceneNodes] Ya existen los nodos de '" + Escena + "'. No se toca nada.\n" + Cadena(grafo));
            return;
        }

        var inicio = grafo.FindNode(grafo.startNodeGuid);
        var emite = grafo.nodes.OfType<RaiseCustomEventNode>().FirstOrDefault(n => n.eventKey == SenalInicio);
        var espera = grafo.nodes.OfType<WaitCustomEventNode>().FirstOrDefault(n => n.eventKey == SenalFin);

        if (inicio == null || emite == null || espera == null)
        {
            Debug.LogError($"[PrologoSceneNodes] Falta algún nodo de referencia — " +
                $"StartNode: {(inicio != null)}, emisor de {SenalInicio}: {(emite != null)}, " +
                $"espera de {SenalFin}: {(espera != null)}. No se toca nada.");
            return;
        }

        Undo.RecordObject(grafo, "Insertar nodos de escena del prólogo");

        var cargar = new AdditiveSceneNode
        {
            displayTitle = "1.- Carga el valle del prologo (Prologo_Valle)",
            chapter = inicio.chapter,
            blockSaving = true,
            operacion = AdditiveSceneNode.Operacion.Cargar,
            sceneName = Escena,
            waitForCompletion = true,
            position = new Vector2(190f, -260f),
        };
        cargar.outputs.Add(emite.guid);

        var descargar = new AdditiveSceneNode
        {
            displayTitle = "3b.- Descarga el valle del prologo",
            chapter = inicio.chapter,
            operacion = AdditiveSceneNode.Operacion.Descargar,
            sceneName = Escena,
            waitForCompletion = true,
            position = new Vector2(950f, -260f),
        };
        // Lo que venía después de la espera pasa a colgar del nodo de descarga.
        string siguiente = (espera.outputs != null && espera.outputs.Count > 0) ? espera.outputs[0] : null;
        if (!string.IsNullOrEmpty(siguiente)) descargar.outputs.Add(siguiente);

        // Recablear: StartNode → cargar, y espera → descargar.
        ReemplazarSalida(inicio, emite.guid, cargar.guid);
        ReemplazarSalida(espera, siguiente, descargar.guid);

        grafo.nodes.Add(cargar);
        grafo.nodes.Add(descargar);

        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PrologoSceneNodes] ✅ Insertados. Nodos en el grafo: {grafo.nodes.Count} " +
                  $"(antes {grafo.nodes.Count - 2}).\n" + Cadena(grafo));
    }

    private static void ReemplazarSalida(NarrativeNode nodo, string viejo, string nuevo)
    {
        if (nodo.outputs == null || nodo.outputs.Count == 0) { nodo.outputs = new() { nuevo }; return; }
        int i = string.IsNullOrEmpty(viejo) ? 0 : nodo.outputs.IndexOf(viejo);
        if (i < 0) i = 0;
        nodo.outputs[i] = nuevo;
    }

    private static string Cadena(NarrativeGraph grafo)
    {
        var sb = new StringBuilder("Cadena desde el StartNode:\n");
        var actual = grafo.FindNode(grafo.startNodeGuid);
        for (int i = 0; i < 8 && actual != null; i++)
        {
            string extra = actual is AdditiveSceneNode a ? $"  [{a.operacion} '{a.sceneName}']" : "";
            sb.AppendLine($"  {i + 1}. {actual.GetType().Name} — {actual.displayTitle}{extra}");
            if (actual.outputs == null || actual.outputs.Count == 0 || string.IsNullOrEmpty(actual.outputs[0])) break;
            actual = grafo.FindNode(actual.outputs[0]);
        }
        return sb.ToString();
    }
}
