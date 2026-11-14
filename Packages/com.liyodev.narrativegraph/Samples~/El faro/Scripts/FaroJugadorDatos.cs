using UnityEngine;

namespace NarrativeGraphSamples.ElFaro
{
    /// Datos del jugador del ejemplo: con qué empieza una partida nueva. Es la plantilla de la
    /// ficha del jugador del ejemplo, así que los puntos de arranque (◆) pueden fijar estos campos.
    [CreateAssetMenu(menuName = "Narrative Graph/Ejemplos/El faro: datos del jugador", fileName = "Jugador")]
    public sealed class FaroJugadorDatos : ScriptableObject
    {
        [Tooltip("Dónde aparece: el nombre de un objeto «Aparecer_<nombre>» de la escena.")]
        public string apareceEn = "Plaza";
        public int vida = 3;
        public bool llevaFarol;
    }
}
