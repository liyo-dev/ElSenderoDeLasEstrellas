using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Reparte los golpes de un <see cref="ScoreCueTrigger"/> entre varias piezas, una por golpe y en
    /// orden: el primer golpe mueve la primera, el segundo la segunda… (y vuelve a empezar si hay más
    /// golpes que piezas). Para escalas que se encienden tecla a tecla, filas que saltan una detrás de
    /// otra o planos que cambian en cada sílaba.
    /// </summary>
    public sealed class CueSequence : MonoBehaviour
    {
        [SerializeField] private ScoreCueTrigger trigger;
        [Tooltip("Piezas que se mueven, en el orden de los golpes.")]
        [SerializeField] private PulseMotion[] steps = System.Array.Empty<PulseMotion>();
        [Tooltip("Si hay más golpes que piezas, vuelve a la primera; si no, las de más no mueven nada.")]
        [SerializeField] private bool loop = true;

        /// <summary>Golpe número i (en el orden de la partitura) con su segundo.</summary>
        public event System.Action<int, float> Step;

        private void OnEnable()
        {
            if (trigger != null) trigger.Hit += AlGolpe;
        }

        private void OnDisable()
        {
            if (trigger != null) trigger.Hit -= AlGolpe;
        }

        private void AlGolpe(int indice, float segundo)
        {
            Step?.Invoke(indice, segundo);
            if (steps.Length == 0) return;
            if (!loop && indice >= steps.Length) return;
            var pieza = steps[indice % steps.Length];
            if (pieza != null) pieza.Kick();
        }
    }
}
