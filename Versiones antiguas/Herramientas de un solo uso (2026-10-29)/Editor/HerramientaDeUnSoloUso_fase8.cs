#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Comprobación de la fase 8: todos los grafos cargan sus nodos (ningún tipo perdido al mover archivos).
public static class HerramientaDeUnSoloUso
{
    [MenuItem("El Sendero/Archivo/Rediseño 29 oct/Fase 8 comprobar grafos")]
    public static void Fase8()
    {
        var informe = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(ruta);
            if (g == null) { informe.AppendLine($"{ruta}: no carga"); continue; }
            int nulos = 0;
            foreach (var n in g.nodes) if (n == null) nulos++;
            bool perdidos = SerializationUtility.HasManagedReferencesWithMissingTypes(g);
            informe.AppendLine($"{ruta}: nodos={g.nodes.Count} nulos={nulos} tiposPerdidos={perdidos}");
            if (perdidos)
                foreach (var m in SerializationUtility.GetManagedReferencesWithMissingTypes(g))
                    informe.AppendLine($"   perdido {m.namespaceName}.{m.className} ({m.assemblyName})");
        }
        File.WriteAllText(Path.Combine("Logs", "claude", "herramienta.txt"), informe.ToString());
    }
}
#endif
