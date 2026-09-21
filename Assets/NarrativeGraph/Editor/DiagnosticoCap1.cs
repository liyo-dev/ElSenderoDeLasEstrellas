using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dice qué está viendo Unity DE VERDAD dentro de Cap1.asset, y permite forzar su reimportación.
///
/// Existe porque el 17 sep 2026 se dio esta situación: el archivo `Cap1.asset` en disco tenía dos
/// nodos nuevos (`AdditiveSceneNode`, INC-249) correctamente cableados —verificado leyendo el YAML—
/// pero en Play el `NarrativeRunner` seguía yendo del `StartNode` directo al nodo siguiente, como si
/// no existieran. Con solo el YAML y el `Editor.log` no se puede distinguir entre tres causas:
///
///   a) Unity importó el asset ANTES de que existiera el tipo `AdditiveSceneNode`, dejó las
///      `[SerializeReference]` a null y se quedó con ese resultado cacheado en `Library/`.
///   b) Unity no llegó a ver el cambio del archivo (edición externa que su watcher no detectó).
///   c) El runner está usando otra copia del grafo.
///
/// Este diagnóstico las separa: imprime lo que `AssetDatabase` devuelve AHORA, fuerza la
/// reimportación, y vuelve a imprimir. Si los dos informes son distintos, era (a) o (b) y ya está
/// resuelto. Si el primero ya es correcto y aun así Play va por el camino viejo, es (c).
/// </summary>
public static class DiagnosticoCap1
{
    private const string Ruta = "Assets/NarrativeGraph/Cap1.asset";

    [MenuItem("El Sendero/Narrativa/Diagnóstico: qué ve Unity en Cap1.asset")]
    public static void Diagnosticar()
    {
        Debug.Log("=== ANTES de forzar la reimportación ===\n" + Informe());

        AssetDatabase.ImportAsset(Ruta,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Debug.Log("=== DESPUÉS de forzar la reimportación ===\n" + Informe() +
                  "\nSi el 'después' ya es correcto, guarda el proyecto y vuelve a dar a Play.");
    }

    private static string Informe()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Ruta);
        if (grafo == null) return "No se ha podido cargar " + Ruta;

        var sb = new StringBuilder();
        sb.AppendLine($"Nodos en la lista: {grafo.nodes.Count}");

        int nulos = 0, additive = 0;
        foreach (var n in grafo.nodes)
        {
            if (n == null) { nulos++; continue; }
            if (n is AdditiveSceneNode) additive++;
        }
        sb.AppendLine($"Nodos NULL (referencia perdida): {nulos}   ← si no es 0, el tipo no existía al importar");
        sb.AppendLine($"AdditiveSceneNode encontrados: {additive}   ← deberían ser 2");

        var inicio = grafo.FindNode(grafo.startNodeGuid);
        if (inicio == null)
        {
            sb.AppendLine("No se encuentra el StartNode.");
            return sb.ToString();
        }

        sb.AppendLine($"\nCadena desde '{inicio.displayTitle}':");
        var actual = inicio;
        for (int i = 0; i < 8 && actual != null; i++)
        {
            string extra = actual is AdditiveSceneNode a ? $"  [{a.operacion} '{a.sceneName}']" : "";
            sb.AppendLine($"  {i + 1}. {actual.GetType().Name} — {actual.displayTitle}{extra}");
            if (actual.outputs == null || actual.outputs.Count == 0 ||
                string.IsNullOrEmpty(actual.outputs[0])) break;
            actual = grafo.FindNode(actual.outputs[0]);
        }

        return sb.ToString();
    }
}
