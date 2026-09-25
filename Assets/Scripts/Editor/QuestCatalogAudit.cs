using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Detecta y arregla el mismo tipo de fallo que dejó "WILL_MISSION0" arrancando en silencio sin
/// hacer nada (INC-344/345): el asset QuestData existe en el proyecto, pero nadie lo arrastró a
/// la lista "questCatalog" del QuestManager de la escena, así que StartQuest(questId) no lo
/// encuentra y no pasa nada (ahora, desde el fix del logging, al menos avisa por consola).
///
/// Existe porque MainWorld.unity está guardada en formato binario y no se puede editar desde
/// fuera del Editor -- mismo patrón que OliverSequenceWiring / DiscusionVentanaSequenceWiring.
///
/// QUÉ HACE (idempotente — se puede volver a ejecutar sin duplicar nada):
///   1) Busca TODOS los QuestData del proyecto (AssetDatabase.FindAssets("t:QuestData")), no solo
///      los de una carpeta -- así detecta también futuras misiones guardadas en otro sitio.
///   2) Busca todos los QuestManager de las escenas abiertas.
///   3) Para cada QuestManager, compara su questCatalog contra la lista completa de assets:
///      - Si un QuestData no está (por referencia), lo añade al final de la lista.
///      - Si hay un questId repetido entre dos assets distintos, avisa (dato mal puesto, hay que
///        revisarlo a mano -- este script no adivina cuál es el bueno).
///      - Si hay un slot vacío (null) en la lista, lo señala pero no lo toca (podría ser
///        intencional mientras se prepara un asset).
///   4) Dejа la escena marcada como modificada (dirty) -- hay que guardarla (Ctrl+S) para que el
///      cambio persista, igual que con el resto de scripts de este estilo.
/// </summary>
public static class QuestCatalogAudit
{
    [MenuItem("El Sendero/Quests/Comprobar y completar catálogo de misiones (QuestManager)")]
    public static void AuditAndFix()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        // ── 1) Todos los QuestData del proyecto ──────────────────────────────
        var guids = AssetDatabase.FindAssets("t:QuestData");
        var allQuests = new List<QuestData>(guids.Length);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var q = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (q != null) allQuests.Add(q);
        }
        log.AppendLine($"QuestData encontrados en el proyecto: {allQuests.Count}");

        // Detectar questId duplicados entre assets distintos (dato mal puesto, no lo tocamos).
        var byId = new Dictionary<string, List<QuestData>>();
        foreach (var q in allQuests)
        {
            if (string.IsNullOrEmpty(q.questId)) continue;
            if (!byId.TryGetValue(q.questId, out var list)) byId[q.questId] = list = new List<QuestData>();
            list.Add(q);
        }
        foreach (var kv in byId)
        {
            if (kv.Value.Count <= 1) continue;
            var paths = kv.Value.Select(AssetDatabase.GetAssetPath);
            warnings.Add($"questId '{kv.Key}' repetido en {kv.Value.Count} assets distintos: " +
                         string.Join(", ", paths) + " -- revisa a mano cuál es el bueno.");
        }

        // ── 2) QuestManager(s) en las escenas abiertas ───────────────────────
        var managers = Object.FindObjectsByType<QuestManager>(FindObjectsInactive.Include);
        if (managers.Length == 0)
        {
            Debug.LogWarning("[QuestCatalogAudit] No se ha encontrado ningún QuestManager en las escenas abiertas.");
            return;
        }

        int totalAdded = 0;
        foreach (var manager in managers)
        {
            var so = new SerializedObject(manager);
            var catalogProp = so.FindProperty("questCatalog");
            if (catalogProp == null)
            {
                warnings.Add($"'{manager.gameObject.name}': no se ha encontrado el campo 'questCatalog' " +
                             "(¿ha cambiado de nombre en QuestManager.cs?).");
                continue;
            }

            var existingRefs = new HashSet<QuestData>();
            int nullSlots = 0;
            for (int i = 0; i < catalogProp.arraySize; i++)
            {
                var elem = catalogProp.GetArrayElementAtIndex(i);
                var val = elem.objectReferenceValue as QuestData;
                if (val == null) { nullSlots++; continue; }
                existingRefs.Add(val);
            }
            if (nullSlots > 0)
                warnings.Add($"'{manager.gameObject.name}': {nullSlots} slot(s) vacío(s) en questCatalog -- no se tocan.");

            int added = 0;
            foreach (var q in allQuests)
            {
                if (existingRefs.Contains(q)) continue;
                int idx = catalogProp.arraySize;
                catalogProp.InsertArrayElementAtIndex(idx);
                catalogProp.GetArrayElementAtIndex(idx).objectReferenceValue = q;
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(manager);
                totalAdded += added;
                log.AppendLine($"'{manager.gameObject.name}': {added} quest(s) añadida(s) al catálogo " +
                                $"(antes tenía {existingRefs.Count}, ahora {existingRefs.Count + added}).");
            }
            else
            {
                log.AppendLine($"'{manager.gameObject.name}': catálogo ya estaba completo " +
                                $"({existingRefs.Count} quest(s)).");
            }
        }

        var final = new StringBuilder();
        final.AppendLine("=== QuestCatalogAudit ===");
        final.Append(log);
        if (totalAdded > 0)
            final.AppendLine($"\nTotal añadidas: {totalAdded}. Guarda la escena (Ctrl+S) para que el cambio persista.");
        else
            final.AppendLine("\nNada que guardar -- ningún catálogo necesitaba cambios.");

        if (warnings.Count > 0)
        {
            final.AppendLine($"\n--- {warnings.Count} aviso(s), revisar a mano: ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
        else
        {
            Debug.Log(final.ToString());
        }
    }
}
