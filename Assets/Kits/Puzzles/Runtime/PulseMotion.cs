using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Empujón de una pieza que vuelve a su sitio: un cabeceo, una mandíbula que baja, unas cejas que
    /// suben. Se dispara en cada tiempo del reloj, en cada fuerte o desde un evento (<see cref="Kick"/>),
    /// y se va apagando en <see cref="decaySeconds"/>. Sirve para mover recortes y piezas al compás sin
    /// animar a mano.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseMotion : MonoBehaviour
    {
        [Tooltip("Reloj. Vacío: el de la escena.")]
        [SerializeField] private TempoClock clock;
        [Tooltip("Empuja en cada tiempo.")]
        [SerializeField] private bool onEveryBeat;
        [Tooltip("Empuja en cada fuerte (el 1 del compás).")]
        [SerializeField] private bool onDownbeat;
        [Tooltip("Cada empujón va al lado contrario del anterior (un cabeceo de lado a lado, unas manos que suben y bajan).")]
        [SerializeField] private bool alternate;

        [Header("Empujón (local)")]
        [SerializeField] private Vector3 positionKick;
        [SerializeField] private Vector3 rotationKick;
        [SerializeField] private Vector3 scaleKick;
        [Tooltip("Segundos que tarda en volver a su sitio.")]
        [SerializeField, Min(0.01f)] private float decaySeconds = 0.25f;

        private Vector3 _posicion, _escala;
        private Quaternion _giro;
        private float _fuerza;
        private float _lado = 1f;
        private bool _enReposo = true;
        private TempoClock _suscrito;

        private void Awake()
        {
            _posicion = transform.localPosition;
            _giro = transform.localRotation;
            _escala = transform.localScale;
        }

        private void OnEnable()
        {
            if (clock == null) clock = TempoClock.Current;
            Suscribir(clock);
        }

        private void Start()
        {
            // El reloj de la escena puede activarse después que esta pieza.
            if (_suscrito == null) Suscribir(clock != null ? clock : TempoClock.Current);
        }

        private void OnDisable()
        {
            Suscribir(null);
            Reposo();
        }

        private void Suscribir(TempoClock reloj)
        {
            if (_suscrito != null)
            {
                _suscrito.Beat -= AlTiempo;
                _suscrito.Downbeat -= AlFuerte;
            }
            _suscrito = reloj;
            if (_suscrito == null) return;
            if (onEveryBeat) _suscrito.Beat += AlTiempo;
            if (onDownbeat) _suscrito.Downbeat += AlFuerte;
        }

        private void AlTiempo(int compas, int tiempo) => Kick();
        private void AlFuerte(int compas) => Kick();

        /// <summary>Un empujón: la pieza salta y vuelve sola.</summary>
        public void Kick()
        {
            if (alternate) _lado = -_lado;
            _fuerza = 1f;
            _enReposo = false;
        }

        private void LateUpdate()
        {
            if (_enReposo) return;
            _fuerza = Mathf.Max(0f, _fuerza - Time.deltaTime / decaySeconds);
            float k = _fuerza * _fuerza * _lado;
            transform.localPosition = _posicion + positionKick * k;
            transform.localRotation = _giro * Quaternion.Euler(rotationKick * k);
            transform.localScale = _escala + scaleKick * Mathf.Abs(k);
            if (_fuerza <= 0f) Reposo();
        }

        private void Reposo()
        {
            _enReposo = true;
            transform.localPosition = _posicion;
            transform.localRotation = _giro;
            transform.localScale = _escala;
        }
    }
}
