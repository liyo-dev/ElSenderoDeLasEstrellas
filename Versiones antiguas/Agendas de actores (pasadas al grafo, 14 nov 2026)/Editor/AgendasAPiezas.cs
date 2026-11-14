
namespace Sendero.Narrative.Editor
{
    /// Pasa las agendas de los actores (Resources/Agendas) a piezas «Personaje desde aquí» en los
    /// grafos. Informe en Logs/claude/agendas_a_piezas.txt.
    public static class AgendasAPiezas
    {
        [UnityEditor.MenuItem("El Sendero/Archivo/Narrativa/Grafo: pasar las agendas a piezas del grafo")]
        public static void Pasar()
        {
            var informe = new System.Text.StringBuilder("[Agendas → piezas del grafo]\n");
            var situaciones = new System.Collections.Generic.List<PiezasDePersonaje.Situacion>();
            foreach (var agenda in UnityEngine.Resources.LoadAll<AgendaDelActor>(AgendaDeActores.CarpetaEnResources))
            {
                if (agenda == null || agenda.entradas == null) continue;
                foreach (var e in agenda.entradas)
                {
                    if (e == null) continue;
                    var presencia = e.presencia switch
                    {
                        PresenciaEnAgenda.EnUnaMarca => ActorPresence.AtMark,
                        PresenciaEnAgenda.NoEsta => ActorPresence.Away,
                        _ => ActorPresence.Unchanged,
                    };
                    situaciones.Add(PiezasDePersonaje.Desde(agenda.actorId, e.nombre, e.tramo, presencia, e.marca, e.frases, informe));
                }
            }
            informe.AppendLine($"{situaciones.Count} situaciones.");
            PiezasDePersonaje.Poner(situaciones, informe);
            System.IO.File.WriteAllText("Logs/claude/agendas_a_piezas.txt", informe.ToString());
        }
    }
}
