using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Losa que late al ritmo y se hunde en el tiempo fuerte, para cruzar saltando de oído. Avisa en
    /// el tiempo débil anterior (se aplasta y tiembla, sin pintura ni marcas) y al llegar el fuerte
    /// quita su colisión y baja el visual. Vuelve sola a los pocos tiempos.
    ///
    /// El patrón se reparte por losas con <see cref="cycleMeasures"/> y
    /// <see cref="sinkOnMeasureInCycle"/>: con 2 compases, las pares se hunden en el 0 y las impares
    /// en el 1, y así se cruzan en zigzag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseTile : MonoBehaviour
    {
        [Header("Ritmo")]
        [Tooltip("Pulso de la música. Si se deja vacío, se usa el de la escena.")]
        [SerializeField] private MusicPulse pulse;
        [Tooltip("Cada cuántos compases se repite su hundimiento.")]
        [SerializeField, Min(1)] private int cycleMeasures = 2;
        [Tooltip("En qué compás del ciclo se hunde (0 = el primero).")]
        [SerializeField, Min(0)] private int sinkOnMeasureInCycle;
        [Tooltip("Tiempos que dura hundida desde el fuerte.")]
        [SerializeField, Min(1)] private int sunkBeats = 2;

        [Header("Aspecto")]
        [Tooltip("Lo que baja al hundirse. Si se deja vacío, baja este mismo objeto.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Cuánto baja el visual al hundirse (m).")]
        [SerializeField, Min(0f)] private float sinkDepth = 0.55f;
        [Tooltip("Cuánto se aplasta en el aviso (fracción: 0,08 = un 8 %).")]
        [SerializeField, Range(0f, 0.5f)] private float warnSquash = 0.08f;
        [Tooltip("Cuánto tiembla de lado en el aviso (m).")]
        [SerializeField, Min(0f)] private float warnShake = 0.03f;
        [Tooltip("Velocidad con la que sube y baja el visual (m/s).")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 9f;

        [Header("Colisión")]
        [Tooltip("Lo que deja de sujetar al hundirse. Si se deja vacío, se cogen los colisionadores de este objeto y sus hijos.")]
        [SerializeField] private Collider[] solidColliders;

        /// <summary>Está hundida ahora mismo (no se puede pisar).</summary>
        public bool IsSunk { get; private set; }
        /// <summary>Está avisando de que se va a hundir en el siguiente fuerte.</summary>
        public bool IsWarning { get; private set; }

        private MusicPulse _pulse;
        private Transform _visual;
        private Vector3 _visualLocalBase;
        private Vector3 _visualLocalScale;
        private int _beatsLeft;
        private float _shakeTime;

        private void Awake()
        {
            _visual = visualRoot != null ? visualRoot : transform;
            _visualLocalBase = _visual.localPosition;
            _visualLocalScale = _visual.localScale;
            if (solidColliders == null || solidColliders.Length == 0)
                solidColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            _pulse = pulse != null ? pulse : MusicPulse.Current;
            if (_pulse == null) return;
            _pulse.Downbeat += AlLlegarElFuerte;
            _pulse.Upbeat += AlAvisarElDebil;
            _pulse.Beat += AlPasarUnTiempo;
        }

        private void OnDisable()
        {
            if (_pulse == null) return;
            _pulse.Downbeat -= AlLlegarElFuerte;
            _pulse.Upbeat -= AlAvisarElDebil;
            _pulse.Beat -= AlPasarUnTiempo;
            _pulse = null;
        }

        /// <summary>La deja como estaba, para volver a empezar el tramo.</summary>
        public void ResetTile()
        {
            IsSunk = false;
            IsWarning = false;
            _beatsLeft = 0;
            _shakeTime = 0f;
            PonerColision(true);
            _visual.localPosition = _visualLocalBase;
            _visual.localScale = _visualLocalScale;
        }

        /// <summary>Se engancha al pulso en cuanto esté: al cargar la escena puede activarse antes.</summary>
        private void AsegurarPulso()
        {
            if (_pulse != null) return;
            _pulse = pulse != null ? pulse : MusicPulse.Current;
            if (_pulse == null) return;
            _pulse.Downbeat += AlLlegarElFuerte;
            _pulse.Upbeat += AlAvisarElDebil;
            _pulse.Beat += AlPasarUnTiempo;
        }

        private void AlLlegarElFuerte(int compas)
        {
            IsWarning = false;
            if (compas % Mathf.Max(1, cycleMeasures) != sinkOnMeasureInCycle) return;
            IsSunk = true;
            _beatsLeft = Mathf.Max(1, sunkBeats);
            PonerColision(false);
        }

        private void AlAvisarElDebil(int compas)
        {
            // El fuerte que viene es el compás siguiente.
            IsWarning = !IsSunk &&
                        (compas + 1) % Mathf.Max(1, cycleMeasures) == sinkOnMeasureInCycle;
        }

        private void AlPasarUnTiempo(int compas, int tiempo)
        {
            if (!IsSunk) return;
            _beatsLeft--;
            if (_beatsLeft > 0) return;
            IsSunk = false;
            PonerColision(true);
        }

        private void PonerColision(bool activa)
        {
            if (solidColliders == null) return;
            for (int i = 0; i < solidColliders.Length; i++)
                if (solidColliders[i] != null) solidColliders[i].enabled = activa;
        }

        private void Update()
        {
            AsegurarPulso();
            // Baja y sube el visual sin tocar el transform raíz (el collider va con él).
            Vector3 objetivo = IsSunk ? _visualLocalBase + Vector3.down * sinkDepth : _visualLocalBase;
            if (IsWarning)
            {
                _shakeTime += Time.deltaTime;
                float temblor = Mathf.Sin(_shakeTime * 42f) * warnShake;
                objetivo += new Vector3(temblor, 0f, temblor * 0.5f);
            }
            _visual.localPosition = Vector3.MoveTowards(_visual.localPosition, objetivo, moveSpeed * Time.deltaTime);
            _visual.localScale = IsWarning
                ? new Vector3(1f + warnSquash, 1f - warnSquash, 1f + warnSquash)
                : Vector3.one;
        }
    }
}
