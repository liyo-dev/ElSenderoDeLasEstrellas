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

    /// En Play, los datos del jugador tal y como van ahora, del mismo tipo que la ficha. Sirve para
    /// fijar un punto de arranque con lo que se está jugando.
    public interface INarrativePlayerLive
    {
        UnityEngine.Object CaptureLive();
    }
}
