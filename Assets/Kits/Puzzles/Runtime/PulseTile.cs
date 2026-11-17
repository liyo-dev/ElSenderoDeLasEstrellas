using UnityEngine;

namespace Kits.Puzzles
{
    /// <summary>
    /// Losa que se quita y se pone con un ciclo constante de tiempos, y avisa antes de cada cambio:
    /// se aplasta, tiembla y se enciende su marco, para que se pueda cruzar de oído y de vista sin
    /// adivinar cuándo va a caer el suelo.
    ///
    /// El ciclo es «tiempos puesta + tiempos quitada», y <see cref="desfaseEnTiempos"/> retrasa el
    /// ciclo de cada losa: con una losa un tiempo por detrás de la siguiente, el hueco va recorriendo
    /// la fila como una ola y siempre hay por dónde pasar. El estado se lee del reloj en cada
    /// fotograma, así que no se desfasa aunque se reinicie el tramo o cambie el tempo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseTile : MonoBehaviour
    {
        [Header("Ciclo")]
        [Tooltip("Reloj de la escena. Si se deja vacío, se usa el de la escena.")]
        [SerializeField] private TempoClock pulse;
        [Tooltip("Tiempos que está puesta antes de quitarse.")]
        [SerializeField, Min(1)] private int beatsUp = 4;
        [Tooltip("Tiempos que está quitada antes de volver.")]
        [SerializeField, Min(1)] private int beatsDown = 4;
        [Tooltip("Tiempos de retraso en el ciclo. Con una menos que la siguiente, el hueco va recorriendo la fila como una ola: es lo que deja pasar de una en una.")]
        [SerializeField] private int desfaseEnTiempos;
        [Tooltip("Segundos de aviso antes de cada cambio (antes de quitarse y antes de volver).")]
        [SerializeField, Min(0f)] private float warningSeconds = 0.5f;

        [Header("Aspecto")]
        [Tooltip("Lo que baja al quitarse. Si se deja vacío, baja este mismo objeto.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Lo que se ve de la losa: se apaga al quitarse y se enciende al volver.")]
        [SerializeField] private Renderer[] visuales;
        [Tooltip("El marco del hueco: no se va nunca y es el que avisa cuando la losa está quitada. Puede ir vacío.")]
        [SerializeField] private Renderer[] marco;
        [Tooltip("Color con el que se enciende en el aviso.")]
        [SerializeField] private Color colorDeAviso = new Color(1f, 0.55f, 0.18f);
        [Tooltip("Cuánto baja el visual al quitarse (m).")]
        [SerializeField, Min(0f)] private float sinkDepth = 0.55f;
        [Tooltip("Cuánto se aplasta en el aviso (fracción: 0,08 = un 8 %).")]
        [SerializeField, Range(0f, 0.5f)] private float warnSquash = 0.08f;
        [Tooltip("Cuánto tiembla de lado en el aviso (m).")]
        [SerializeField, Min(0f)] private float warnShake = 0.03f;
        [Tooltip("Velocidad con la que sube y baja el visual (m/s).")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 9f;

        [Header("Colisión")]
        [Tooltip("Lo que deja de sujetar al quitarse. Si se deja vacío, se cogen los colisionadores de este objeto y sus hijos.")]
        [SerializeField] private Collider[] solidColliders;

        /// <summary>Está quitada ahora mismo (no se puede pisar).</summary>
        public bool IsSunk { get; private set; }
        /// <summary>Está avisando de que cambia en el siguiente tiempo.</summary>
        public bool IsWarning { get; private set; }

        private TempoClock _clock;
        private Transform _visual;
        private Vector3 _visualLocalBase;
        private Vector3 _visualLocalScale;
        private float _shakeTime;
        private MaterialPropertyBlock _bloque;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private bool _avisandoLos, _avisandoElMarco;

        private void Awake()
        {
            _visual = visualRoot != null ? visualRoot : transform;
            _visualLocalBase = _visual.localPosition;
            _visualLocalScale = _visual.localScale;
            if (solidColliders == null || solidColliders.Length == 0)
                solidColliders = GetComponentsInChildren<Collider>(true);
            if (visuales == null || visuales.Length == 0)
                visuales = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>La deja como estaba, para volver a empezar el tramo.</summary>
        public void ResetTile()
        {
            IsWarning = false;
            _shakeTime = 0f;
            PonerEstado(false);
            QuitarAviso();
            _visual.localPosition = _visualLocalBase;
            _visual.localScale = _visualLocalScale;
        }

        private void Update()
        {
            if (_clock == null)
            {
                _clock = pulse != null ? pulse : TempoClock.Current;
                if (_clock == null) return;
            }

            int ciclo = Mathf.Max(2, beatsUp + beatsDown);
            int posicion = _clock.TotalBeats + desfaseEnTiempos;
            posicion = ((posicion % ciclo) + ciclo) % ciclo;

            bool quitada = posicion >= beatsUp;
            bool cambiaAlSiguiente = (((posicion + 1) % ciclo) >= beatsUp) != quitada;
            PonerEstado(quitada);

            // El aviso se enciende en los últimos segundos de cada estado, mire por donde mire el reloj.
            IsWarning = _clock.IsRunning && cambiaAlSiguiente && _clock.SecondsToNextBeat <= warningSeconds;

            Vector3 objetivo = quitada ? _visualLocalBase + Vector3.down * sinkDepth : _visualLocalBase;
            if (IsWarning)
            {
                _shakeTime += Time.deltaTime;
                float temblor = Mathf.Sin(_shakeTime * 42f) * warnShake;
                objetivo += new Vector3(temblor, 0f, temblor * 0.5f);
            }
            _visual.localPosition = Vector3.MoveTowards(_visual.localPosition, objetivo, moveSpeed * Time.deltaTime);

            // La escala se multiplica sobre la suya: si se pusiera a 1, la losa volvería a ser un cubo.
            Vector3 aviso = IsWarning
                ? new Vector3(1f + warnSquash, 1f - warnSquash, 1f + warnSquash)
                : Vector3.one;
            _visual.localScale = Vector3.Scale(_visualLocalScale, aviso);

            // El marco se ve siempre, así que es el único que puede avisar de que la losa vuelve.
            Pintar(visuales, IsWarning && !quitada, ref _avisandoLos);
            Pintar(marco, IsWarning, ref _avisandoElMarco);
        }

        /// <summary>Se va o vuelve: quita o pone la colisión y enseña u oculta la losa.</summary>
        private void PonerEstado(bool quitada)
        {
            if (IsSunk == quitada) return;
            IsSunk = quitada;
            if (solidColliders != null)
                for (int i = 0; i < solidColliders.Length; i++)
                    if (solidColliders[i] != null) solidColliders[i].enabled = !quitada;
            if (visuales != null)
                for (int i = 0; i < visuales.Length; i++)
                    if (visuales[i] != null) visuales[i].enabled = !quitada;
        }

        /// <summary>Enciende o apaga el color del aviso en lo que se le diga.</summary>
        private void Pintar(Renderer[] cuales, bool aviso, ref bool yaPintado)
        {
            if (cuales == null || aviso == yaPintado) return;
            yaPintado = aviso;
            _bloque ??= new MaterialPropertyBlock();
            for (int i = 0; i < cuales.Length; i++)
            {
                var r = cuales[i];
                if (r == null) continue;
                if (!aviso) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(_bloque);
                _bloque.SetColor(BaseColorId, colorDeAviso);
                r.SetPropertyBlock(_bloque);
            }
        }

        /// <summary>Deja los dos sin el color del aviso (al reiniciar el tramo).</summary>
        private void QuitarAviso()
        {
            _avisandoLos = false;
            _avisandoElMarco = false;
            Quitar(visuales);
            Quitar(marco);
        }

        private static void Quitar(Renderer[] cuales)
        {
            if (cuales == null) return;
            for (int i = 0; i < cuales.Length; i++)
                if (cuales[i] != null) cuales[i].SetPropertyBlock(null);
        }
    }
}
