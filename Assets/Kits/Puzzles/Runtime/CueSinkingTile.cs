using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Losa que se hunde en un golpe concreto de la canción y vuelve después: tiembla en el aviso,
    /// se va abajo en el segundo de la marca (sin suelo donde pisar) y sube pasado un rato. El estado
    /// se calcula del segundo de la canción en cada fotograma, así que vale igual al reintentar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CueSinkingTile : MonoBehaviour
    {
        [Tooltip("Reloj que sigue la canción. Vacío: el de la escena.")]
        [SerializeField] private TempoClock clock;
        [Tooltip("Segundo de la canción en el que se hunde.")]
        [SerializeField, Min(0f)] private float cueSeconds;
        [Tooltip("Segundos de aviso (tiembla) antes de hundirse.")]
        [SerializeField, Min(0f)] private float warningSeconds = 0.4f;
        [Tooltip("Segundos que se queda abajo.")]
        [SerializeField, Min(0.1f)] private float downSeconds = 1.6f;
        [Tooltip("Lo que baja.")]
        [SerializeField, Min(0f)] private float sinkDepth = 6f;
        [Tooltip("Lo que tiembla en el aviso.")]
        [SerializeField, Min(0f)] private float shake = 0.06f;
        [Tooltip("Segundos que tarda en bajar y en subir.")]
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.15f;
        [Tooltip("Apaga sus colisionadores mientras está abajo (una losa hundida no se pisa). Sin marcar, los mantiene: algo que baja del techo y se queda colgado estorbando.")]
        [SerializeField] private bool collidersOffWhileDown = true;

        private Vector3 _posicion;
        private Collider[] _colliders;
        private bool _abajo;

        public float CueSeconds => cueSeconds;

        private void Awake()
        {
            _posicion = transform.localPosition;
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            if (clock == null) clock = TempoClock.Current;
        }

        private void LateUpdate()
        {
            if (clock == null || !clock.IsRunning || !clock.FollowsSong) { Poner(Vector3.zero, false); return; }
            float d = (float)(clock.SongTime - cueSeconds);

            if (d < -warningSeconds || d > downSeconds + moveSeconds) { Poner(Vector3.zero, false); return; }
            if (d < 0f)
            {
                float s = Mathf.Sin(Time.time * 60f) * shake;
                Poner(new Vector3(s, 0f, -s), false);
                return;
            }
            float bajada = d < moveSeconds ? d / moveSeconds
                         : d < downSeconds ? 1f
                         : 1f - (d - downSeconds) / moveSeconds;
            Poner(Vector3.down * sinkDepth * Mathf.Clamp01(bajada), collidersOffWhileDown && d < downSeconds);
        }

        private void Poner(Vector3 desplazamiento, bool abajo)
        {
            transform.localPosition = _posicion + desplazamiento;
            if (abajo == _abajo) return;
            _abajo = abajo;
            for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = !abajo;
        }
    }
}
