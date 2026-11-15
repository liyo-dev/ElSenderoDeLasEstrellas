using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Pasa los pasos propios de El Sendero (cinemática, combate, grupo, hora…) al paso único del
    /// kit «Pedir al juego» con su petición. Conserva el guid, el sitio, la sección, el título y
    /// las conexiones de cada paso; los datos pasan por nombre de campo. Un paso de salida única
    /// con varias conexiones (un fork sin nodo) pasa a llevar un Fork detrás, para que siga en
    /// paralelo. Informe en Logs/claude/pasos_a_peticiones.txt.
    public static class PasarPasosDelJuegoAPeticiones
    {
        static readonly Dictionary<string, Type> Equivalencias = new()
        {
            { "PlayCinematicNode", typeof(PedirCinematica) },
            { "SetTimeOfDayNode", typeof(PedirHoraDelDia) },
            { "SegunHoraDelDiaNode", typeof(PedirSegunHoraDelDia) },
            { "EsperarHoraDelDiaNode", typeof(PedirEsperarHoraDelDia) },
            { "UnlockAbilitiesNode", typeof(PedirDesbloqueo) },
            { "UnlockWardrobeItemNode", typeof(PedirVestuario) },
            { "GrupoNode", typeof(PedirGrupo) },
            { "DramaticTextNode", typeof(PedirTextoDramatico) },
            { "ShowLorePopupNode", typeof(PedirPopupDeLore) },
            { "FinDeLaDemoNode", typeof(PedirFinDeLaDemo) },
            { "TutorialPromptNode", typeof(PedirAvisoDeTutorial) },
            { "WaitItemDeliveryNode", typeof(PedirEntregaDeObjeto) },
            { "StartBattleNode", typeof(PedirBatalla) },
            { "StartNpcCombatNode", typeof(PedirCombateContraNpc) },
            { "StartTagMinigameNode", typeof(PedirPillaPilla) },
            { "GuiarJugadorNode", typeof(PedirGuiarAlJugador) },
            { "SeguirSinSerVistoNode", typeof(PedirSeguirSinSerVisto) },
        };

        [MenuItem("El Sendero/Archivo/Narrativa/Grafo: pasar los pasos del juego a «Pedir al juego»")]
        public static void Pasar()
        {
            var informe = new StringBuilder("[Pasos del juego → «Pedir al juego»]\n");
            int total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(ruta);
                if (grafo == null || grafo.nodes == null) continue;
                var cuenta = new Dictionary<string, int>();
                var nuevos = new List<NarrativeNode>();
                for (int i = 0; i < grafo.nodes.Count; i++)
                {
                    var viejo = grafo.nodes[i];
                    if (viejo == null || !Equivalencias.TryGetValue(viejo.GetType().Name, out var tipo)) continue;
                    grafo.nodes[i] = Convertir(viejo, tipo, nuevos);
                    cuenta.TryGetValue(viejo.GetType().Name, out var n);
                    cuenta[viejo.GetType().Name] = n + 1;
                    total++;
                }
                if (cuenta.Count == 0) continue;
                grafo.nodes.AddRange(nuevos);
                EditorUtility.SetDirty(grafo);
                informe.AppendLine($"{grafo.name}: {string.Join(", ", cuenta.Select(c => $"{c.Key} ×{c.Value}"))}" +
                                   (nuevos.Count > 0 ? $" · {nuevos.Count} fork(s) añadidos" : string.Empty));
            }
            AssetDatabase.SaveAssets();

            // Comprobación: ningún paso viejo y ningún paso sin tipo.
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(AssetDatabase.GUIDToAssetPath(guid));
                if (grafo?.nodes == null) continue;
                int viejos = grafo.nodes.Count(n => n != null && Equivalencias.ContainsKey(n.GetType().Name));
                int nulos = grafo.nodes.Count(n => n == null);
                int sinPeticion = grafo.nodes.OfType<GameStepNode>().Count(n => n.request == null);
                if (viejos + nulos + sinPeticion > 0)
                    informe.AppendLine($"ATENCIÓN {grafo.name}: {viejos} viejos, {nulos} nulos, {sinPeticion} sin petición");
            }
            informe.AppendLine($"Total: {total} pasos.");
            Directory.CreateDirectory("Logs/claude");
            File.WriteAllText("Logs/claude/pasos_a_peticiones.txt", informe.ToString());
            Debug.Log(informe.ToString());
        }

        static NarrativeNode Convertir(NarrativeNode viejo, Type tipo, List<NarrativeNode> nuevos)
        {
            var peticion = (NarrativeRequest)Activator.CreateInstance(tipo);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(viejo), peticion);
            var paso = GameStepNode.For(peticion);
            paso.guid = viejo.guid;
            paso.position = viejo.position;
            paso.section = viejo.section;
            paso.chapter = viejo.chapter;
            paso.displayTitle = viejo.displayTitle;
            paso.blockSaving = viejo.blockSaving;

            var salidas = paso.GetOutputPorts();
            var conectadas = (viejo.outputs ?? new List<string>()).ToList();
            paso.outputs = new List<string>();
            if (!viejo.HasNamedOutputs && conectadas.Count(g => !string.IsNullOrEmpty(g)) > 1)
            {
                var fork = new ForkNode
                {
                    position = viejo.position + new Vector2(0f, 160f),
                    section = string.Empty,
                    chapter = viejo.chapter,
                    displayTitle = "En paralelo",
                    outputs = conectadas.Where(g => !string.IsNullOrEmpty(g)).ToList(),
                };
                nuevos.Add(fork);
                conectadas = new List<string> { fork.guid };
            }
            for (int i = 0; i < salidas.Length; i++) paso.outputs.Add(i < conectadas.Count ? conectadas[i] ?? string.Empty : string.Empty);
            return paso;
        }
    }
}
