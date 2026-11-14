using System.Collections.Generic;

namespace NarrativeGraphEditor
{
    /// Una fila del resumen de arranque que aporta el juego (escena, cómo se entra, avisos).
    public sealed class StartLine
    {
        public string Label;
        public string Value;
        /// De dónde sale (texto corto); vacío si no hace falta decirlo.
        public string Source;
        public bool Warning;
    }

    /// Lo que el arranque del juego añade al resumen de la pestaña Arranque. Lo implementa el mismo
    /// INarrativePlayLauncher del juego, que es quien sabe en qué escena se entra y cómo.
    public interface INarrativeLaunchDetails
    {
        /// <paramref name="player"/>: los datos del jugador ya resueltos (partida nueva, puntos y
        /// retoques), o null si el juego no tiene ficha.
        void Describe(NarrativeStartPlan plan, UnityEngine.Object player, List<StartLine> lines);
    }

    /// Lo que la historia anterior al paso deja en los datos del jugador (habilidades, objetos,
    /// hechizos…), con los puntos de arranque anteriores aplicados en su sitio (su escritor
    /// implementa INarrativePlayerValuesState). Solo el juego sabe escribirlo; si no lo implementa,
    /// el resumen de Arranque no puede enseñarlo. Lo implementa el INarrativePlayLauncher del juego.
    public interface INarrativeStoryOnPlayer
    {
        void ApplyStory(NarrativeStartPlan plan, UnityEngine.Object player);
    }

    /// En Play, los datos del jugador tal y como van ahora, del mismo tipo que la ficha. Sirve para
    /// fijar un punto de arranque con lo que se está jugando.
    public interface INarrativePlayerLive
    {
        UnityEngine.Object CaptureLive();
    }
}
