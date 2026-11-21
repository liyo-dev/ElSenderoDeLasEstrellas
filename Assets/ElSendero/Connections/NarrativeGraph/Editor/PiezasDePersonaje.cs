using System.Collections.Generic;
using System.Linq;
using System.Text;
using Liyodev.NarrativeGraph.Editor;
using UnityEditor;
using UnityEngine;
using Liyodev.NarrativeGraph;

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
                var piezas = par.Value.OrderBy(p => p.PieceLayer == ActorStateNode.Layer.BackToUsual ? 0 : p.PieceLayer == ActorStateNode.Layer.Usual ? 1 : 2).ToList();
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
                    // La levanta el mundo (una zona, un objeto, una cinemática): tras el paso que
                    // la espera, por su salida buena.
                    if (sitios.Count == 0)
                        foreach (var g in grafos)
                            foreach (var n in g.nodes)
                                if (n != null && Usa(n, par.Key, SignalRole.Escucha)) sitios.Add((g, n));
                    if (sitios.Count == 0) { informe.AppendLine($"  ⚠ Ningún paso emite «{par.Key}»: quedan sin poner {string.Join(", ", piezas.Select(p => p.DisplayTitle))}."); continue; }
                }

                foreach (var (grafo, tras) in sitios)
                {
                    var nuevas = piezas.Select(p => Copia(p, grafo, tras)).Where(p => grafo.FindNode(p.Guid) == null).ToList();
                    if (nuevas.Count == 0) continue;
                    Insertar(grafo, tras, nuevas, informe);
                    tocados.Add(grafo);
                    foreach (var p in nuevas) informe.AppendLine($"  {grafo.name} · tras «{tras.DisplayTitle}»: {p.DisplayTitle}");
                }
            }

            // Un montaje que rehace las conexiones de sus pasos deja sueltas las piezas que ya
            // estaban (no se vuelven a crear): se enganchan otra vez tras su paso.
            foreach (var g in grafos)
                if (Reenganchar(g, informe) > 0) tocados.Add(g);

            foreach (var g in tocados) EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
        }

        /// Quita del grafo las piezas que ya no salen de ninguna situación (el personaje, el nombre o
        /// la capa han cambiado, o el paso tras el que iban ya no existe) y empalma lo que venía
        /// detrás. Solo para grafos cuyas piezas pone entero quien llama, con todas sus situaciones.
        /// Devuelve cuántas ha quitado.
        public static int QuitarSobrantes(NarrativeGraph grafo, IEnumerable<Situacion> situaciones, StringBuilder informe)
        {
            if (grafo?.nodes == null) return 0;
            var bases = new HashSet<string>();
            foreach (var s in situaciones)
            {
                bases.Add(Pieza(s, s.acaba.Count > 0 ? ActorStateNode.Layer.FromHere : ActorStateNode.Layer.Usual).Guid);
                if (s.acaba.Count > 0) bases.Add(Pieza(s, ActorStateNode.Layer.BackToUsual).Guid);
            }
            bool Vale(ActorStateNode p)
            {
                foreach (var b in bases)
                {
                    if (!p.Guid.StartsWith(b + "-")) continue;
                    if (grafo.FindNode(p.Guid.Substring(b.Length + 1)) is { } tras && tras is not ActorStateNode) return true;
                }
                return false;
            }
            int quitadas = 0;
            ActorStateNode sobra;
            while ((sobra = grafo.nodes.OfType<ActorStateNode>().FirstOrDefault(p => p.Guid != null && p.Guid.StartsWith("pieza-") && !Vale(p))) != null)
            {
                string siguiente = sobra.Outputs != null && sobra.Outputs.Count > 0 ? sobra.Outputs[0] : string.Empty;
                foreach (var n in grafo.nodes)
                {
                    if (n?.Outputs == null || n == sobra) continue;
                    for (int i = n.Outputs.Count - 1; i >= 0; i--)
                    {
                        if (n.Outputs[i] != sobra.Guid) continue;
                        // Con salidas con nombre se conserva el puerto (vacío si no había nada detrás).
                        if (!string.IsNullOrEmpty(siguiente) || n.HasNamedOutputs) n.Outputs[i] = siguiente;
                        else n.Outputs.RemoveAt(i);
                    }
                }
                grafo.nodes.Remove(sobra);
                informe?.AppendLine($"  {grafo.name} · quitada (ya no sale de ninguna situación): {sobra.DisplayTitle}");
                quitadas++;
            }
            if (quitadas > 0) EditorUtility.SetDirty(grafo);
            return quitadas;
        }

        /// Vuelve a meter en la historia las piezas sueltas (ningún paso lleva a ellas): cada cadena
        /// de piezas va tras el paso cuyo guid lleva al final la suya. Devuelve cuántas cadenas.
        public static int Reenganchar(NarrativeGraph grafo, StringBuilder informe)
        {
            if (grafo?.nodes == null) return 0;
            var conEntrada = new HashSet<string>(grafo.nodes.Where(n => n?.Outputs != null).SelectMany(n => n.Outputs).Where(g => !string.IsNullOrEmpty(g)));
            var alcanzables = Alcanzables(grafo);
            var sueltas = grafo.nodes.OfType<ActorStateNode>()
                .Where(p => p.Guid.StartsWith("pieza-") && p.Guid != grafo.startNodeGuid && !conEntrada.Contains(p.Guid)).ToList();
            int hechas = SacarDePuertosDeMas(grafo, informe);
            foreach (var cabeza in sueltas)
            {
                var tras = grafo.nodes.Where(n => n != null && n is not ActorStateNode && cabeza.Guid.EndsWith("-" + n.Guid))
                    .OrderByDescending(n => n.Guid.Length).FirstOrDefault();
                if (tras == null) continue;
                var cadena = new List<ActorStateNode> { cabeza };
                // La cadena sigue mientras el paso siguiente tampoco esté en la historia (otra pieza,
                // o algo que se metió entre ellas, como un punto de arranque).
                var enCadena = new List<NarrativeNode> { cabeza };
                NarrativeNode actual = cabeza;
                while (actual.Outputs != null && actual.Outputs.Count == 1 && grafo.FindNode(actual.Outputs[0]) is { } siguiente
                       && !alcanzables.Contains(siguiente.Guid) && !enCadena.Contains(siguiente))
                {
                    enCadena.Add(siguiente);
                    actual = siguiente;
                    if (siguiente is ActorStateNode pieza) cadena.Add(pieza);
                }
                tras.Outputs ??= new List<string>();
                if (tras.HasNamedOutputs)
                {
                    // Salidas con nombre: la cadena va en la primera (la buena), como en Insertar.
                    actual.Outputs = tras.Outputs.Count > 0 && !string.IsNullOrEmpty(tras.Outputs[0]) ? new List<string> { tras.Outputs[0] } : new List<string>();
                    if (tras.Outputs.Count > 0) tras.Outputs[0] = cabeza.Guid; else tras.Outputs.Add(cabeza.Guid);
                }
                else if (tras.Outputs.Count <= 1)
                {
                    actual.Outputs = new List<string>(tras.Outputs);
                    tras.Outputs = new List<string> { cabeza.Guid };
                }
                else tras.Outputs.Add(cabeza.Guid);
                informe?.AppendLine($"  {grafo.name} · enganchadas tras «{tras.DisplayTitle}»: {string.Join(", ", cadena.Select(c => c.DisplayTitle))}");
                hechas++;
            }
            return hechas;
        }

        /// Un paso con salidas con nombre («Pedir al juego») solo sigue por sus puertos: lo que cuelgue
        /// de un puerto de más no se ejecuta nunca (una pieza así deja al personaje plantado). Las
        /// cadenas de piezas que estén ahí pasan a la primera salida. Devuelve cuántas ha movido.
        static int SacarDePuertosDeMas(NarrativeGraph grafo, StringBuilder informe)
        {
            int movidas = 0;
            foreach (var tras in grafo.nodes.ToList())
            {
                if (tras == null || tras is ActorStateNode || !tras.HasNamedOutputs || tras.Outputs == null) continue;
                int puertos = tras.GetOutputPorts().Length;
                for (int i = tras.Outputs.Count - 1; i >= puertos && i > 0; i--)
                {
                    if (grafo.FindNode(tras.Outputs[i]) is not ActorStateNode cabeza || !cabeza.Guid.StartsWith("pieza-")) continue;
                    NarrativeNode actual = cabeza;
                    var vistos = new HashSet<NarrativeNode> { cabeza };
                    while (actual.Outputs != null && actual.Outputs.Count == 1 && grafo.FindNode(actual.Outputs[0]) is ActorStateNode siguiente
                           && siguiente.Guid.StartsWith("pieza-") && vistos.Add(siguiente))
                        actual = siguiente;
                    tras.Outputs.RemoveAt(i);
                    string buena = tras.Outputs.Count > 0 ? tras.Outputs[0] : null;
                    actual.Outputs = string.IsNullOrEmpty(buena) ? new List<string>() : new List<string> { buena };
                    if (tras.Outputs.Count > 0) tras.Outputs[0] = cabeza.Guid; else tras.Outputs.Add(cabeza.Guid);
                    informe?.AppendLine($"  {grafo.name} · «{tras.DisplayTitle}»: «{cabeza.DisplayTitle}» colgaba de un puerto que no existe; pasa a la primera salida.");
                    movidas++;
                }
            }
            return movidas;
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
                if (n?.Outputs != null) foreach (var o in n.Outputs) pila.Push(o);
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
                ActorId = s.actor,
                PieceLayer = capa,
                Presence = capa == ActorStateNode.Layer.BackToUsual ? ActorPresence.Unchanged : s.presencia,
                MarkId = capa == ActorStateNode.Layer.BackToUsual ? string.Empty : s.marca,
                Lines = capa == ActorStateNode.Layer.BackToUsual ? new List<NarrativeDialogueAsset>() : new List<NarrativeDialogueAsset>(s.frases),
                DisplayTitle = titulo,
                Guid = $"pieza-{s.actor}-{capa}-{Slug(s.nombre)}".ToLowerInvariant(),
            };
        }

        // Una copia por sitio (una señal puede sonar en más de un paso); el guid lleva el del paso.
        static ActorStateNode Copia(ActorStateNode p, NarrativeGraph grafo, NarrativeNode tras)
        {
            var c = (ActorStateNode)JsonUtility.FromJson(JsonUtility.ToJson(p), typeof(ActorStateNode));
            c.Lines = new List<NarrativeDialogueAsset>(p.Lines);
            c.Guid = $"{p.Guid}-{tras.Guid}";
            c.Chapter = tras.Chapter;
            c.Section = tras.Section;
            c.Outputs = new List<string>();
            return c;
        }

        /// Encadena las piezas detrás de <paramref name="tras"/>. Con una salida (o ninguna) van en
        /// medio; si el paso tiene salidas con nombre —los pasos «Pedir al juego»: hecho, fallo…—
        /// van en la primera, que es la buena, y los demás puertos se quedan como estaban. Con
        /// varias ramas sueltas, las piezas son una rama más al final (las que ya había conservan
        /// su número, que guardan las partidas).
        static void Insertar(NarrativeGraph grafo, NarrativeNode tras, List<ActorStateNode> piezas, StringBuilder informe)
        {
            tras.Outputs ??= new List<string>();
            bool salidasConNombre = tras.HasNamedOutputs;
            if (!salidasConNombre && tras.Outputs.Count > 1)
                informe.AppendLine($"  ⚠ «{tras.DisplayTitle}» abre varias ramas: las piezas van como rama aparte.");
            for (int i = 0; i < piezas.Count; i++)
            {
                var p = piezas[i];
                p.Position = tras.Position + new Vector2(40f, 150f + 110f * i);
                if (i + 1 < piezas.Count) p.Outputs = new List<string> { piezas[i + 1].Guid };
                grafo.nodes.Add(p);
            }
            var ultima = piezas[piezas.Count - 1];
            if (salidasConNombre)
            {
                // La salida buena es la primera; por las demás (fallo, sin respuesta) no ha pasado
                // nada que cambie el reparto.
                string buena = tras.Outputs.Count > 0 ? tras.Outputs[0] : null;
                ultima.Outputs = string.IsNullOrEmpty(buena) ? new List<string>() : new List<string> { buena };
                if (tras.Outputs.Count > 0) tras.Outputs[0] = piezas[0].Guid;
                else tras.Outputs.Add(piezas[0].Guid);
                return;
            }
            if (tras.Outputs.Count <= 1)
            {
                ultima.Outputs = new List<string>(tras.Outputs);
                tras.Outputs = new List<string> { piezas[0].Guid };
            }
            else tras.Outputs.Add(piezas[0].Guid);
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

