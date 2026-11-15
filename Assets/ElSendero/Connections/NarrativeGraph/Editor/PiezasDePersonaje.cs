using System.Collections.Generic;
using System.Linq;
using System.Text;
using NarrativeGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Pone en los grafos las piezas «Personaje desde aquí» (ActorStateNode) de una situación de
    /// un personaje descrita por señales: empieza cuando suena una, acaba cuando suena otra.
    ///   · sin principio ni fin → «Lo de siempre» al empezar el grafo de secundarias;
    ///   · con principio y sin fin → «Lo de siempre» desde donde suena el principio;
    ///   · con fin → «Desde aquí» donde suena el principio (o al empezar las secundarias) y
    ///     «Vuelve a lo de siempre» donde suena el fin.
    /// Cada pieza va justo después del paso que emite la señal (o, si la levanta el mundo, del que la espera). Es idempotente: una pieza que ya
    /// está (mismo guid) no se repite.
    public static class PiezasDePersonaje
    {
        public const string GrafoDeSecundarias = "Secundary";

        public sealed class Situacion
        {
            public string actor;
            public string nombre;
            public List<string> empieza = new();
            public List<string> acaba = new();
            public ActorPresence presencia;
            public string marca;
            public List<NarrativeDialogueAsset> frases = new();
        }

        /// Una situación a partir de un tramo de historia (solo cuentan sus señales).
        public static Situacion Desde(string actor, string nombre, TramoDeHistoria tramo, ActorPresence presencia, string marca,
                                      IEnumerable<NarrativeDialogueAsset> frases, StringBuilder informe)
        {
            var s = new Situacion { actor = actor, nombre = nombre, presencia = presencia, marca = marca ?? string.Empty };
            if (tramo != null)
            {
                s.empieza.AddRange(tramo.empiezaConSenal ?? new List<string>());
                s.acaba.AddRange(tramo.acabaConSenal ?? new List<string>());
                if ((tramo.empiezaConMisionActiva?.Count ?? 0) + (tramo.acabaConMisionIniciada?.Count ?? 0) + (tramo.acabaConMisionCompletada?.Count ?? 0) > 0)
                    informe?.AppendLine($"  ⚠ {actor} · {nombre}: el tramo usa misiones; solo se pasan sus señales.");
            }
            if (frases != null) s.frases.AddRange(frases.Where(f => f != null));
            return s;
        }

        /// Pone las piezas de todas las situaciones y guarda los grafos tocados.
        public static void Poner(IEnumerable<Situacion> situaciones, StringBuilder informe)
        {
            var grafos = AssetDatabase.FindAssets("t:NarrativeGraph")
                .Select(g => AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null && g.nodes != null).ToList();
            var secundarias = grafos.FirstOrDefault(g => g.name == GrafoDeSecundarias);

            // Dónde va cada pieza: (señal o null = inicio de secundarias) → piezas en orden.
            var porSitio = new Dictionary<string, List<ActorStateNode>>();
            void En(string senal, ActorStateNode pieza)
            {
                var clave = senal ?? string.Empty;
                if (!porSitio.TryGetValue(clave, out var lista)) porSitio[clave] = lista = new List<ActorStateNode>();
                lista.Add(pieza);
            }

            foreach (var s in situaciones)
            {
                bool conFin = s.acaba.Count > 0;
                var capa = conFin ? ActorStateNode.Layer.FromHere : ActorStateNode.Layer.Usual;
                if (s.empieza.Count == 0) En(null, Pieza(s, capa));
                foreach (var senal in s.empieza) En(senal, Pieza(s, capa));
                foreach (var senal in s.acaba) En(senal, Pieza(s, ActorStateNode.Layer.BackToUsual));
            }

            var tocados = new HashSet<NarrativeGraph>();
            foreach (var par in porSitio)
            {
                // En un mismo sitio, primero se acaba lo anterior y luego empieza lo nuevo.
                var piezas = par.Value.OrderBy(p => p.layer == ActorStateNode.Layer.BackToUsual ? 0 : p.layer == ActorStateNode.Layer.Usual ? 1 : 2).ToList();
                var sitios = new List<(NarrativeGraph grafo, NarrativeNode tras)>();
                if (par.Key.Length == 0)
                {
                    var inicio = secundarias != null ? secundarias.FindNode(secundarias.startNodeGuid) : null;
                    if (inicio == null) { informe.AppendLine($"  ⚠ No hay grafo «{GrafoDeSecundarias}» con inicio: quedan sin poner {piezas.Count} piezas de «siempre»."); continue; }
                    sitios.Add((secundarias, inicio));
                }
                else
                {
                    foreach (var g in grafos)
                        foreach (var n in g.nodes)
                            if (n != null && Usa(n, par.Key, SignalRole.Emite)) sitios.Add((g, n));
                    // La levanta el mundo (una zona, un objeto): tras el paso que la espera.
                    if (sitios.Count == 0)
                        foreach (var g in grafos)
                            foreach (var n in g.nodes)
                                if (n != null && Usa(n, par.Key, SignalRole.Escucha) && !n.HasNamedOutputs) sitios.Add((g, n));
                    if (sitios.Count == 0) { informe.AppendLine($"  ⚠ Ningún paso emite «{par.Key}»: quedan sin poner {string.Join(", ", piezas.Select(p => p.displayTitle))}."); continue; }
                }

                foreach (var (grafo, tras) in sitios)
                {
                    var nuevas = piezas.Select(p => Copia(p, grafo, tras)).Where(p => grafo.FindNode(p.guid) == null).ToList();
                    if (nuevas.Count == 0) continue;
                    Insertar(grafo, tras, nuevas, informe);
                    tocados.Add(grafo);
                    foreach (var p in nuevas) informe.AppendLine($"  {grafo.name} · tras «{tras.displayTitle}»: {p.displayTitle}");
                }
            }

            // Un montaje que rehace las conexiones de sus pasos deja sueltas las piezas que ya
            // estaban (no se vuelven a crear): se enganchan otra vez tras su paso.
            foreach (var g in grafos)
                if (Reenganchar(g, informe) > 0) tocados.Add(g);

            foreach (var g in tocados) EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
        }

        /// Vuelve a meter en la historia las piezas sueltas (ningún paso lleva a ellas): cada cadena
        /// de piezas va tras el paso cuyo guid lleva al final la suya. Devuelve cuántas cadenas.
        public static int Reenganchar(NarrativeGraph grafo, StringBuilder informe)
        {
            if (grafo?.nodes == null) return 0;
            var conEntrada = new HashSet<string>(grafo.nodes.Where(n => n?.outputs != null).SelectMany(n => n.outputs).Where(g => !string.IsNullOrEmpty(g)));
            var alcanzables = Alcanzables(grafo);
            var sueltas = grafo.nodes.OfType<ActorStateNode>()
                .Where(p => p.guid.StartsWith("pieza-") && p.guid != grafo.startNodeGuid && !conEntrada.Contains(p.guid)).ToList();
            int hechas = 0;
            foreach (var cabeza in sueltas)
            {
                var tras = grafo.nodes.Where(n => n != null && n is not ActorStateNode && cabeza.guid.EndsWith("-" + n.guid))
                    .OrderByDescending(n => n.guid.Length).FirstOrDefault();
                if (tras == null) continue;
                var cadena = new List<ActorStateNode> { cabeza };
                // La cadena sigue mientras el paso siguiente tampoco esté en la historia (otra pieza,
                // o algo que se metió entre ellas, como un punto de arranque).
                var enCadena = new List<NarrativeNode> { cabeza };
                NarrativeNode actual = cabeza;
                while (actual.outputs != null && actual.outputs.Count == 1 && grafo.FindNode(actual.outputs[0]) is { } siguiente
                       && !alcanzables.Contains(siguiente.guid) && !enCadena.Contains(siguiente))
                {
                    enCadena.Add(siguiente);
                    actual = siguiente;
                    if (siguiente is ActorStateNode pieza) cadena.Add(pieza);
                }
                tras.outputs ??= new List<string>();
                if (!tras.HasNamedOutputs && tras.outputs.Count <= 1)
                {
                    actual.outputs = new List<string>(tras.outputs);
                    tras.outputs = new List<string> { cabeza.guid };
                }
                else tras.outputs.Add(cabeza.guid);
                informe?.AppendLine($"  {grafo.name} · enganchadas tras «{tras.displayTitle}»: {string.Join(", ", cadena.Select(c => c.displayTitle))}");
                hechas++;
            }
            return hechas;
        }

        static HashSet<string> Alcanzables(NarrativeGraph grafo)
        {
            var vistos = new HashSet<string>();
            if (string.IsNullOrEmpty(grafo.startNodeGuid)) return vistos;
            var pila = new Stack<string>();
            pila.Push(grafo.startNodeGuid);
            while (pila.Count > 0)
            {
                var g = pila.Pop();
                if (string.IsNullOrEmpty(g) || !vistos.Add(g)) continue;
                var n = grafo.FindNode(g);
                if (n?.outputs != null) foreach (var o in n.outputs) pila.Push(o);
            }
            return vistos;
        }

        static bool Usa(NarrativeNode n, string senal, SignalRole papel)
        {
            try { return NarrativeNodeReferences.Of(n).Signals.Any(r => r.Role == papel && r.Key == senal); }
            catch (System.Exception) { return false; }
        }

        static ActorStateNode Pieza(Situacion s, ActorStateNode.Layer capa)
        {
            string quien = Nombre(s.actor);
            string titulo = capa switch
            {
                ActorStateNode.Layer.BackToUsual => $"{quien}: vuelve a lo de siempre (acaba «{s.nombre}»)",
                ActorStateNode.Layer.Usual => $"{quien}, lo de siempre: {s.nombre}",
                _ => $"{quien} desde aquí: {s.nombre}",
            };
            return new ActorStateNode
            {
                actorId = s.actor,
                layer = capa,
                presence = capa == ActorStateNode.Layer.BackToUsual ? ActorPresence.Unchanged : s.presencia,
                markId = capa == ActorStateNode.Layer.BackToUsual ? string.Empty : s.marca,
                lines = capa == ActorStateNode.Layer.BackToUsual ? new List<NarrativeDialogueAsset>() : new List<NarrativeDialogueAsset>(s.frases),
                displayTitle = titulo,
                guid = $"pieza-{s.actor}-{capa}-{Slug(s.nombre)}".ToLowerInvariant(),
            };
        }

        // Una copia por sitio (una señal puede sonar en más de un paso); el guid lleva el del paso.
        static ActorStateNode Copia(ActorStateNode p, NarrativeGraph grafo, NarrativeNode tras)
        {
            var c = (ActorStateNode)JsonUtility.FromJson(JsonUtility.ToJson(p), typeof(ActorStateNode));
            c.lines = new List<NarrativeDialogueAsset>(p.lines);
            c.guid = $"{p.guid}-{tras.guid}";
            c.chapter = tras.chapter;
            c.section = tras.section;
            c.outputs = new List<string>();
            return c;
        }

        /// Encadena las piezas detrás de <paramref name="tras"/>. Con una salida (o ninguna) van en
        /// medio; si el paso abre varias ramas en paralelo, las piezas son una rama más al final
        /// (las que ya había conservan su número, que guardan las partidas).
        static void Insertar(NarrativeGraph grafo, NarrativeNode tras, List<ActorStateNode> piezas, StringBuilder informe)
        {
            tras.outputs ??= new List<string>();
            if (tras.HasNamedOutputs) { informe.AppendLine($"  ⚠ «{tras.displayTitle}» tiene salidas con nombre: las piezas van como rama aparte."); }
            bool enMedio = !tras.HasNamedOutputs && tras.outputs.Count <= 1;
            for (int i = 0; i < piezas.Count; i++)
            {
                var p = piezas[i];
                p.position = tras.position + new Vector2(40f, 150f + 110f * i);
                if (i + 1 < piezas.Count) p.outputs = new List<string> { piezas[i + 1].guid };
                grafo.nodes.Add(p);
            }
            if (enMedio)
            {
                piezas[piezas.Count - 1].outputs = new List<string>(tras.outputs);
                tras.outputs = new List<string> { piezas[0].guid };
            }
            else tras.outputs.Add(piezas[0].guid);
        }

        /// «NPC_Eldran» → «Eldran»; «_LIAM» → «Liam».
        public static string Nombre(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return "?";
            var n = actorId.StartsWith("NPC_") ? actorId.Substring(4) : actorId.TrimStart('_');
            if (n.Length > 1 && n == n.ToUpperInvariant()) n = n[0] + n.Substring(1).ToLowerInvariant();
            return n;
        }

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in (s ?? "").Normalize(NormalizationForm.FormD))
            {
                if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(ch);
                else if ((ch == ' ' || ch == '-' || ch == '·') && sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }
    }
}

