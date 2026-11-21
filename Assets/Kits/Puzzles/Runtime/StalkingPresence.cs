using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>Algo que piensa el sujeto en voz baja (un bocadillo de pensamiento): clave de texto, texto si no hay traducción y gesto.</summary>
    [Serializable]
    public struct SubjectThought
    {
        [Tooltip("Clave de texto traducible. Vacío = no piensa nada.")]
        public string key;
        [Tooltip("Texto si la clave no está traducida.")]
        public string fallback;
        [Tooltip("Gesto (estado del Animator) mientras lo piensa. Vacío = ninguno.")]
        public string gesture;

        public bool IsEmpty => string.IsNullOrEmpty(key) && string.IsNullOrEmpty(fallback);
    }

    /// <summary>
    /// Algo que acecha a quien lleva un objeto, sin combate ni derrota: mientras se lleva, se anda
    /// más despacio y, en cada <see cref="RustlePoint"/> hijo, algo cruje entre los arbustos. Tras
    /// el aviso hay que quedarse quieto hasta que el ruido se aleje; si se sigue andando, la
    /// presencia tira el objeto a un sitio cercano y se recoge para seguir. Al llegar al punto de
    /// alivio deja de actuar.
    ///
    /// Quién es el sujeto, si lleva el objeto, cómo se le tira, hacia dónde mira la cámara y el tope
    /// de velocidad los pone el juego con los enganches estáticos (un kit no conoce al jugador).
    /// Cuándo existe (el tramo de la historia) también lo decide el juego, encendiendo o apagando
    /// este objeto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StalkingPresence : MonoBehaviour
    {
        /// <summary>Raíz del sujeto vigilado (el jugador).</summary>
        public static Func<Transform> ResolveSubject;
        /// <summary>¿El sujeto lleva este objeto? Con null, ¿lleva algo?</summary>
        public static Func<GameObject, bool> IsCarrying;
        /// <summary>Tira el objeto que lleva el sujeto (null = lo que lleve) para que caiga en el punto, en esos segundos. Devuelve si lo ha tirado.</summary>
        public static Func<GameObject, Vector3, float, bool> Knock;
        /// <summary>Gira la mirada (la cámara) hacia el punto; la clave es quien lo pide.</summary>
        public static Action<object, Transform> DrawAttention;
        /// <summary>Suelta la mirada pedida con esa clave.</summary>
        public static Action<object> ReleaseAttention;
        /// <summary>Pone un tope de velocidad (m/s) con esa clave.</summary>
        public static Action<object, float> SetSpeedCap;
        /// <summary>Quita el tope de velocidad de esa clave.</summary>
        public static Action<object> ClearSpeedCap;
        /// <summary>Pulso físico (vibración del mando): intensidad 0..1 y segundos.</summary>
        public static Action<float, float> Pulse;
        /// <summary>El sujeto piensa algo (bocadillo de pensamiento sobre él).</summary>
        public static Action<SubjectThought> Think;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ResolveSubject = null; IsCarrying = null; Knock = null;
            DrawAttention = null; ReleaseAttention = null;
            SetSpeedCap = null; ClearSpeedCap = null; Pulse = null; Think = null;
        }
#endif

        [Header("Qué se lleva")]
        [Tooltip("Objeto que hay que llevar para que la presencia actúe. Vacío = cualquier cosa que se lleve.")]
        [SerializeField] private GameObject objeto;
        [Tooltip("Velocidad máxima (m/s) mientras se lleva el objeto. 0 = sin tope.")]
        [SerializeField, Min(0f)] private float topeDeVelocidad = 3f;

        [Header("Quieto")]
        [Tooltip("Por debajo de esta velocidad en el suelo (m/s) se cuenta como quieto.")]
        [SerializeField, Min(0.05f)] private float velocidadDeQuieto = 0.4f;
        [Tooltip("Segundos seguidos moviéndose, ya en la ventana de quieto, que hacen falta para que tire el objeto.")]
        [SerializeField, Min(0f)] private float margen = 0.3f;

        [Header("Alivio")]
        [Tooltip("Al llegar cerca de este punto la presencia se va para siempre (la llegada a un sitio seguro).")]
        [SerializeField] private Transform alivio;
        [SerializeField, Min(0f)] private float radioDelAlivio = 12f;
        [SerializeField] private UnityEvent alLlegarAlAlivio = new();

        [Header("Pulso")]
        [Tooltip("Vibración durante el aviso (0 = nada).")]
        [SerializeField, Range(0f, 1f)] private float pulsoDelAviso = 0.25f;
        [Tooltip("Vibración al tirar el objeto (0 = nada).")]
        [SerializeField, Range(0f, 1f)] private float pulsoAlTirar = 0.7f;

        [Header("La primera vez (se explica sin carteles)")]
        [Tooltip("Lo que piensa el sujeto al empezar el primer crujido (p. ej. sobresaltado: «¿Qué ha sido eso?»).")]
        [SerializeField] private SubjectThought alPrimerAviso;
        [Tooltip("Lo que piensa la primera vez que le tiran el objeto: la regla dicha por él (p. ej. «si vuelve a sonar, me quedo quieto»).")]
        [SerializeField] private SubjectThought alPrimerGolpe;
        [Tooltip("Lo que piensa la primera vez que se queda quieto y el ruido se va.")]
        [SerializeField] private SubjectThought alPrimerAlivio;

        /// <summary>Un crujido ha empezado.</summary>
        public event Action<RustlePoint> Warned;
        /// <summary>Ha tirado el objeto.</summary>
        public event Action<RustlePoint> Knocked;
        /// <summary>Se ha estado quieto y ha pasado.</summary>
        public event Action<RustlePoint> Passed;

        public bool IsRelieved { get; private set; }
        public RustlePoint Current => _actual;

        private enum Estado { Esperando, Aviso, Quieto, Yendose }

        private RustlePoint[] _puntos;
        private RustlePoint _actual;
        private Estado _estado;
        private float _t, _moviendose, _velocidad;
        private Vector3 _ultima;
        private bool _hayUltima, _conTope;
        private bool _yaAvisado, _yaGolpeado, _yaAliviado;

        private void Awake()
        {
            _puntos = GetComponentsInChildren<RustlePoint>(true);
        }

        private void OnDisable()
        {
            if (_actual != null && (_estado == Estado.Aviso || _estado == Estado.Quieto)) _actual.Abort();
            SoltarMirada();
            QuitarTope();
            _actual = null;
            _estado = Estado.Esperando;
            _hayUltima = false;
        }

        private void Update()
        {
            if (IsRelieved) return;
            Transform sujeto = ResolveSubject != null ? ResolveSubject() : null;
            if (sujeto == null) { _hayUltima = false; return; }

            float dt = Time.deltaTime;
            Vector3 pos = sujeto.position;
            MedirVelocidad(pos, dt);

            if (alivio != null)
            {
                Vector3 d = pos - alivio.position; d.y = 0f;
                if (d.sqrMagnitude <= radioDelAlivio * radioDelAlivio) { Aliviar(); return; }
            }

            bool lleva = IsCarrying != null && IsCarrying(objeto);
            if (lleva && !_conTope && topeDeVelocidad > 0f && SetSpeedCap != null) { SetSpeedCap(this, topeDeVelocidad); _conTope = true; }
            else if (!lleva && _conTope) QuitarTope();

            switch (_estado)
            {
                case Estado.Esperando:
                    if (lleva) BuscarCrujido(pos);
                    break;

                case Estado.Aviso:
                    if (!lleva) { Cortar(); break; }
                    _t += dt;
                    if (_t < _actual.WarningSeconds) break;
                    if (_actual.AlwaysKnocks) { Tirar(); break; }
                    _estado = Estado.Quieto;
                    _t = 0f;
                    _moviendose = 0f;
                    break;

                case Estado.Quieto:
                    if (!lleva) { Cortar(); break; }
                    _t += dt;
                    _moviendose = _velocidad > velocidadDeQuieto ? _moviendose + dt : 0f;
                    if (_moviendose > margen) { Tirar(); break; }
                    if (_t >= _actual.FreezeSeconds) Pasar();
                    break;

                case Estado.Yendose:
                    _t += dt;
                    if (_t >= _actual.LeavingSeconds) { _actual = null; _estado = Estado.Esperando; }
                    break;
            }
        }

        private void MedirVelocidad(Vector3 pos, float dt)
        {
            if (!_hayUltima || dt <= 0f) { _ultima = pos; _hayUltima = true; _velocidad = 0f; return; }
            Vector3 d = pos - _ultima; d.y = 0f;
            _ultima = pos;
            float v = d.magnitude / dt;
            if (v > 30f) return; // salto de sitio (carga, recolocación): no es andar
            _velocidad = Mathf.Lerp(_velocidad, v, 1f - Mathf.Exp(-12f * dt));
        }

        private void BuscarCrujido(Vector3 pos)
        {
            float ahora = Time.time;
            int ordenMinimo = int.MaxValue;
            for (int i = 0; i < _puntos.Length; i++)
            {
                var p = _puntos[i];
                if (p != null && !p.Repeats && !p.Done && p.Order >= 0 && p.Order < ordenMinimo) ordenMinimo = p.Order;
            }
            for (int i = 0; i < _puntos.Length; i++)
            {
                var p = _puntos[i];
                if (p == null || !p.gameObject.activeInHierarchy || !p.CanTrigger(ahora)) continue;
                if (!p.Repeats && p.Order >= 0 && p.Order > ordenMinimo) continue;
                Vector3 d = pos - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude > p.Radius * p.Radius) continue;
                Empezar(p);
                return;
            }
        }

        private void Empezar(RustlePoint p)
        {
            _actual = p;
            _estado = Estado.Aviso;
            _t = 0f;
            _moviendose = 0f;
            p.BeginWarning();
            DrawAttention?.Invoke(this, p.LookTarget);
            if (pulsoDelAviso > 0f) Pulse?.Invoke(pulsoDelAviso, p.WarningSeconds);
            if (!_yaAvisado) { _yaAvisado = true; Pensar(alPrimerAviso); }
            Warned?.Invoke(p);
        }

        private void Tirar()
        {
            var p = _actual;
            bool tirado = p.Landing != null && Knock != null && Knock(objeto, p.Landing.position, p.FlightSeconds);
            if (!tirado) { Cortar(); return; }
            p.Knocked();
            SoltarMirada();
            if (pulsoAlTirar > 0f) Pulse?.Invoke(pulsoAlTirar, 0.35f);
            if (!_yaGolpeado) { _yaGolpeado = true; Pensar(alPrimerGolpe); }
            _estado = Estado.Yendose;
            _t = 0f;
            Knocked?.Invoke(p);
        }

        private void Pasar()
        {
            var p = _actual;
            p.Pass();
            SoltarMirada();
            if (!_yaAliviado) { _yaAliviado = true; Pensar(alPrimerAlivio); }
            _estado = Estado.Yendose;
            _t = 0f;
            Passed?.Invoke(p);
        }

        private void Cortar()
        {
            if (_actual != null) _actual.Abort();
            SoltarMirada();
            _actual = null;
            _estado = Estado.Esperando;
        }

        private void Aliviar()
        {
            if (_actual != null && (_estado == Estado.Aviso || _estado == Estado.Quieto)) _actual.Abort();
            SoltarMirada();
            QuitarTope();
            _actual = null;
            _estado = Estado.Esperando;
            IsRelieved = true;
            alLlegarAlAlivio.Invoke();
        }

        private void SoltarMirada() => ReleaseAttention?.Invoke(this);

        private static void Pensar(in SubjectThought pensamiento)
        {
            if (!pensamiento.IsEmpty) Think?.Invoke(pensamiento);
        }

        private void QuitarTope()
        {
            if (!_conTope) return;
            _conTope = false;
            ClearSpeedCap?.Invoke(this);
        }

        private void OnDrawGizmosSelected()
        {
            if (alivio == null) return;
            Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(alivio.position, radioDelAlivio);
        }
    }
}
