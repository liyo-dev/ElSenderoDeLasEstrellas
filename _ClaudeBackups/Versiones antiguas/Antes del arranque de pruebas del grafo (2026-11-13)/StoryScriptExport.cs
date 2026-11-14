using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace NarrativeGraphEditor.Story
{
    /// Exports a chapter's lines and on-screen texts in story order, as a spreadsheet (CSV) for
    /// translators and voice actors: who says it, in which step, its key and its text.
    public static class StoryScriptExport
    {
        public static void ExportWithDialog(NarrativeGraph graph)
        {
            if (graph == null) return;
            var path = EditorUtility.SaveFilePanel(StoryText.T("Exportar textos", "Export texts"), string.Empty, graph.name + "_textos.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            int count = Export(graph, path);
            EditorUtility.RevealInFinder(path);
            UnityEngine.Debug.Log(StoryText.T($"[Historia] {count} textos exportados a {path}", $"[Story] {count} texts exported to {path}"));
        }

        /// Writes the CSV and returns how many texts it has.
        public static int Export(NarrativeGraph graph, string path)
        {
            var structure = StoryStructure.Build(graph);
            string first = NarrativeProjectIndex.PrimaryLanguage ?? "es";
            string second = NarrativeProjectIndex.SecondaryLanguage ?? "en";
            var csv = new StringBuilder();
            Row(csv, StoryText.T("Paso", "Step"), StoryText.T("Título del paso", "Step title"), StoryText.T("Tipo", "Kind"),
                StoryText.T("Habla", "Speaker"), StoryText.T("Clave", "Key"), first, second, StoryText.T("Notas", "Notes"));

            int count = 0;
            var seen = new HashSet<string>();
            foreach (var node in structure.StoryOrder)
            {
                NarrativeNodeReferences refs;
                try { refs = NarrativeNodeReferences.Of(node); }
                catch (System.Exception) { continue; }
                string number = structure.Number(node);
                string title = node.displayTitle ?? string.Empty;

                foreach (var line in refs.Lines)
                {
                    string key = line.TextId ?? string.Empty;
                    if (key.Length > 0 && !seen.Add(key)) continue;
                    Row(csv, number, title, StoryText.T("Diálogo", "Line"),
                        NarrativeProjectIndex.SpeakerName(line.SpeakerId, line.IsPlayer), key,
                        NarrativeProjectIndex.LineText(line.TextId, line.Text),
                        NarrativeProjectIndex.LocEn(line.TextId, string.Empty), string.Empty);
                    count++;
                }
                foreach (var text in refs.Texts)
                {
                    string key = text.Key ?? string.Empty;
                    if (key.Length > 0 && !seen.Add(key)) continue;
                    Row(csv, number, title, StoryText.T("Texto", "Text"), string.Empty, key,
                        NarrativeProjectIndex.Loc(text.Key, text.Fallback), NarrativeProjectIndex.LocEn(text.Key, string.Empty), string.Empty);
                    count++;
                }
            }
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
            return count;
        }

        static void Row(StringBuilder csv, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) csv.Append(',');
                var c = cells[i] ?? string.Empty;
                csv.Append('"').Append(c.Replace("\"", "\"\"")).Append('"');
            }
            csv.Append("\r\n");
        }
    }
}
