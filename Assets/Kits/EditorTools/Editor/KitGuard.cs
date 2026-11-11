using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Kits.EditorTools
{
    /// <summary>
    /// Reglas que debe cumplir la carpeta de kits de un proyecto. Los valores propios de cada
    /// juego (sus nombres, sus menús) los pone quien llama; el guardián no conoce ninguno.
    /// </summary>
    public sealed class KitRuleSet
    {
        /// <summary>Carpeta que contiene un kit por subcarpeta.</summary>
        public string KitsRoot = "Assets/Kits";

        /// <summary>Prefijo de los ensamblados y de los namespaces: «Kits.» da Kits.Core, Kits.Core.Editor…</summary>
        public string AssemblyPrefix = "Kits.";

        /// <summary>
        /// Palabras que no pueden salir en rutas, código ni textos de un kit. Se buscan como parte de
        /// un nombre en PascalCase («Hero» encuentra HeroCamera y soloHero, no Heroic). Con «=» delante
        /// solo vale la palabra suelta («=Hero» no encuentra HeroCamera).
        /// </summary>
        public string[] ForbiddenTerms = new string[0];

        /// <summary>Primer tramo permitido en un MenuItem.</summary>
        public string[] AllowedMenuRoots = { "Tools", "Assets", "GameObject", "CONTEXT" };

        /// <summary>Ensamblados de fuera de los kits que un asmdef puede referenciar por nombre.</summary>
        public string[] AllowedReferencePrefixes = { "Unity.", "UnityEngine.", "UnityEditor." };

        /// <summary>Carpetas de fuera de los kits cuyos asmdef se pueden referenciar por GUID.</summary>
        public string[] AllowedReferenceFolders = { "Packages/" };
    }

    /// <summary>Un incumplimiento de las reglas, con el archivo y la línea donde está.</summary>
    public sealed class KitViolation
    {
        public readonly string Path;
        public readonly int Line;
        public readonly string Message;

        public KitViolation(string path, int line, string message)
        {
            Path = path;
            Line = line;
            Message = message;
        }

        public override string ToString()
        {
            return Line > 0 ? Path + ":" + Line + "  " + Message : Path + "  " + Message;
        }
    }

    /// <summary>
    /// Comprueba que los kits no dependen del juego que los usa: sin palabras del juego, menús solo
    /// en las raíces permitidas, todo el código bajo un asmdef del kit con su namespace, y asmdef que
    /// solo referencian otros kits o paquetes.
    /// </summary>
    public static class KitGuard
    {
        static readonly string[] TextExtensions =
        {
            ".cs", ".asmdef", ".asmref", ".shader", ".hlsl", ".cginc", ".uxml", ".uss", ".json", ".txt", ".md"
        };

        // Cadenas y caracteres se conservan; los comentarios se cambian por espacios.
        static readonly Regex CommentsAndLiterals = new Regex(
            "@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\\\n])+'|//[^\\n]*|/\\*.*?\\*/",
            RegexOptions.Singleline);

        static readonly Regex MenuItemRoot = new Regex("\\bMenuItem\\s*\\(\\s*@?\"([^\"/]*)");
        static readonly Regex JsonName = new Regex("\"name\"\\s*:\\s*\"([^\"]*)\"");
        static readonly Regex JsonString = new Regex("\"([^\"]*)\"");

        /// <param name="rules">Reglas del proyecto.</param>
        /// <param name="guidToAssetPath">
        /// Traduce el GUID de un asmdef a su ruta dentro del proyecto. Puede ser null si los asmdef
        /// no usan referencias por GUID.
        /// </param>
        public static List<KitViolation> Check(KitRuleSet rules, Func<string, string> guidToAssetPath)
        {
            var found = new List<KitViolation>();
            string root = Normalize(rules.KitsRoot).TrimEnd('/');
            if (!Directory.Exists(root))
                return found;

            var files = new List<string>();
            CollectFiles(root, files);

            var termPatterns = BuildTermPatterns(rules.ForbiddenTerms);
            var asmdefs = new Dictionary<string, AsmdefInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
                if (file.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    asmdefs[file] = AsmdefInfo.Read(file);

            foreach (string file in files)
            {
                string inRoot = file.Substring(root.Length + 1);
                string kit = KitOf(inRoot);

                CheckTerms(file, 0, inRoot, termPatterns, "en la ruta", found);

                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(TextExtensions, extension) < 0)
                    continue;

                string text = File.ReadAllText(file);
                bool isCode = extension == ".cs";
                if (isCode)
                    text = CommentsAndLiterals.Replace(text, BlankComment);

                CheckTermsByLine(file, text, termPatterns, found);

                if (isCode)
                    CheckCode(rules, root, file, kit, text, asmdefs, found);
                else if (extension == ".asmdef")
                    CheckAsmdef(rules, root, file, kit, asmdefs, guidToAssetPath, found);
            }

            return found;
        }

        static void CheckCode(KitRuleSet rules, string root, string file, string kit, string code,
            Dictionary<string, AsmdefInfo> asmdefs, List<KitViolation> found)
        {
            if (kit == null)
            {
                found.Add(new KitViolation(file, 0, "código fuera de la carpeta de un kit"));
                return;
            }

            foreach (Match m in MenuItemRoot.Matches(code))
            {
                string menuRoot = m.Groups[1].Value;
                if (Array.IndexOf(rules.AllowedMenuRoots, menuRoot) < 0)
                    found.Add(new KitViolation(file, LineOf(code, m.Index),
                        "menú «" + menuRoot + "/…»: los kits usan " + string.Join(", ", rules.AllowedMenuRoots)));
            }

            string expected = rules.AssemblyPrefix + kit;
            if (!Regex.IsMatch(code, "\\bnamespace\\s+" + Regex.Escape(expected) + "(?![A-Za-z0-9_])"))
                found.Add(new KitViolation(file, 0, "falta el namespace " + expected));

            AsmdefInfo owner = OwnerOf(file, root, asmdefs);
            if (owner == null)
                found.Add(new KitViolation(file, 0, "no está bajo ningún asmdef del kit"));
            else if (!owner.EditorOnly && ("/" + file + "/").IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                found.Add(new KitViolation(file, 0,
                    "carpeta Editor dentro de " + owner.Name + ", que no es solo de editor: entraría en la build"));
        }

        static void CheckAsmdef(KitRuleSet rules, string root, string file, string kit,
            Dictionary<string, AsmdefInfo> asmdefs, Func<string, string> guidToAssetPath, List<KitViolation> found)
        {
            AsmdefInfo info = asmdefs[file];
            if (kit == null)
            {
                found.Add(new KitViolation(file, 0, "asmdef fuera de la carpeta de un kit"));
                return;
            }

            string expected = rules.AssemblyPrefix + kit;
            if (info.Name != expected && !info.Name.StartsWith(expected + ".", StringComparison.Ordinal))
                found.Add(new KitViolation(file, 0,
                    "el asmdef se llama «" + info.Name + "»; debe ser " + expected + " o " + expected + ".<Parte>"));

            bool namedEditor = info.Name.EndsWith(".Editor", StringComparison.Ordinal)
                               || info.Name.IndexOf(".Editor.", StringComparison.Ordinal) >= 0;
            if (namedEditor && !info.EditorOnly)
                found.Add(new KitViolation(file, 0, "un asmdef de editor debe llevar includePlatforms: [\"Editor\"]"));

            foreach (string reference in info.References)
            {
                AsmdefInfo target = null;
                string targetName = reference;

                if (reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                {
                    string path = guidToAssetPath != null ? guidToAssetPath(reference.Substring(5)) : null;
                    if (string.IsNullOrEmpty(path))
                    {
                        found.Add(new KitViolation(file, 0, "referencia sin resolver: " + reference));
                        continue;
                    }

                    path = Normalize(path);
                    if (StartsWithAny(path, rules.AllowedReferenceFolders))
                        continue;
                    if (!path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(new KitViolation(file, 0, "referencia a algo que no es un kit: " + path));
                        continue;
                    }

                    asmdefs.TryGetValue(path, out target);
                    targetName = target != null ? target.Name : path;
                }
                else if (StartsWithAny(reference, rules.AllowedReferencePrefixes))
                {
                    continue;
                }
                else if (!reference.StartsWith(rules.AssemblyPrefix, StringComparison.Ordinal))
                {
                    found.Add(new KitViolation(file, 0, "referencia a algo que no es un kit: " + reference));
                    continue;
                }
                else
                {
                    foreach (AsmdefInfo other in asmdefs.Values)
                        if (other.Name == reference)
                            target = other;
                }

                if (target != null && target.EditorOnly && !info.EditorOnly)
                    found.Add(new KitViolation(file, 0,
                        "un asmdef de juego no puede referenciar uno de editor: " + targetName));
            }
        }

        static List<KeyValuePair<string, Regex>> BuildTermPatterns(string[] terms)
        {
            var patterns = new List<KeyValuePair<string, Regex>>();
            if (terms == null)
                return patterns;

            foreach (string raw in terms)
            {
                if (string.IsNullOrEmpty(raw))
                    continue;

                bool wholeWord = raw[0] == '=';
                string term = wholeWord ? raw.Substring(1) : raw;
                string pattern = wholeWord
                    ? "(?<![A-Za-z0-9_])" + Regex.Escape(term) + "(?![A-Za-z0-9_])"
                    : "(?<![A-Z])" + Regex.Escape(term) + "(?![a-z])";
                patterns.Add(new KeyValuePair<string, Regex>(term, new Regex(pattern)));
            }

            return patterns;
        }

        static void CheckTermsByLine(string file, string text, List<KeyValuePair<string, Regex>> patterns,
            List<KitViolation> found)
        {
            if (patterns.Count == 0)
                return;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                CheckTerms(file, i + 1, lines[i], patterns, "en el contenido", found);
        }

        static void CheckTerms(string file, int line, string text, List<KeyValuePair<string, Regex>> patterns,
            string where, List<KitViolation> found)
        {
            foreach (KeyValuePair<string, Regex> pattern in patterns)
                if (pattern.Value.IsMatch(text))
                    found.Add(new KitViolation(file, line, "«" + pattern.Key + "» " + where + ": es del juego, no del kit"));
        }

        static AsmdefInfo OwnerOf(string file, string root, Dictionary<string, AsmdefInfo> asmdefs)
        {
            string folder = ParentOf(file);
            while (folder.Length > root.Length)
            {
                foreach (KeyValuePair<string, AsmdefInfo> pair in asmdefs)
                    if (string.Equals(ParentOf(pair.Key), folder, StringComparison.OrdinalIgnoreCase))
                        return pair.Value;
                folder = ParentOf(folder);
            }

            return null;
        }

        static void CollectFiles(string folder, List<string> files)
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(Normalize(file));
            }

            foreach (string sub in Directory.GetDirectories(folder))
            {
                string name = Path.GetFileName(sub);
                // Unity no importa las carpetas ocultas ni las que acaban en «~».
                if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal))
                    continue;
                CollectFiles(sub, files);
            }
        }

        static string BlankComment(Match m)
        {
            if (m.Value[0] != '/')
                return m.Value;

            char[] blank = m.Value.ToCharArray();
            for (int i = 0; i < blank.Length; i++)
                if (blank[i] != '\n' && blank[i] != '\r')
                    blank[i] = ' ';
            return new string(blank);
        }

        static string KitOf(string pathInRoot)
        {
            int slash = pathInRoot.IndexOf('/');
            return slash > 0 ? pathInRoot.Substring(0, slash) : null;
        }

        static string ParentOf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : string.Empty;
        }

        static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index; i++)
                if (text[i] == '\n')
                    line++;
            return line;
        }

        static bool StartsWithAny(string value, string[] prefixes)
        {
            foreach (string prefix in prefixes)
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        sealed class AsmdefInfo
        {
            public string Name = string.Empty;
            public bool EditorOnly;
            public readonly List<string> References = new List<string>();

            public static AsmdefInfo Read(string file)
            {
                string json = File.ReadAllText(file);
                var info = new AsmdefInfo();

                Match name = JsonName.Match(json);
                if (name.Success)
                    info.Name = name.Groups[1].Value;

                info.References.AddRange(ArrayOf(json, "references"));

                List<string> platforms = ArrayOf(json, "includePlatforms");
                info.EditorOnly = platforms.Count == 1 && platforms[0] == "Editor";
                return info;
            }

            static List<string> ArrayOf(string json, string key)
            {
                var values = new List<string>();
                Match array = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]");
                if (array.Success)
                    foreach (Match value in JsonString.Matches(array.Groups[1].Value))
                        values.Add(value.Groups[1].Value);
                return values;
            }
        }
    }
}
