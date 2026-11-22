using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Lo que cae del techo justo en un golpe de la canción: aparece arriba, cae acelerando y toca el
    /// suelo en el segundo exacto de la marca, mientras su sombra crece en el sitio donde va a caer.
    /// Cada marca de sus voces cae en el siguiente punto de la lista (en bucle). Al tocar el suelo
    /// golpea lo que haya debajo con <see cref="Strike"/>, que enchufa el juego (el kit no sabe a quién
    /// hace daño). Lee el segundo de la canción del reloj: al reintentar, se recoloca solo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CueDrop : MonoBehaviour
    {
        /// <summary>Golpe al tocar el suelo: (centro, radio, daño, alto; alto 0 = el de siempre del juego). Lo pone el juego.</summary>
        public static Action<Vector3, float, float, float> Strike;
        /// <summary>
        /// Suena el golpe: (sonido, sitio, volumen). Lo pone el juego para que pase por su mezcla;
        /// sin él, suena con <see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3, float)"/>.
        /// </summary>
        public static Action<AudioClip, Vector3, float> PlaySound;
        /// <summary>Dónde está lo que se apunta (el jugador), para las caídas que apuntan. Lo pone el juego.</summary>
        public static Func<Vector3?> TargetPosition;

        [Tooltip("Reloj que sigue la canción. Vacío: el de la escena.")]
        [SerializeField] private TempoClock clock;
        [Tooltip("Voces de la partitura que caen. Vacío: todas.")]
        [SerializeField] private string[] voices = Array.Empty<string>();
        [SerializeField, Min(0f)] private float fromSeconds;
        [SerializeField, Min(0f)] private float toSeconds = 100000f;
        [Tooltip("Dónde cae cada marca, en orden; al acabarse la lista vuelve a empezar.")]
        [SerializeField] private Transform[] landingPoints = Array.Empty<Transform>();

        [Header("Aspecto")]
        [Tooltip("Lo que cae (se copia; el original se apaga).")]
        [SerializeField] private GameObject fallingTemplate;
        [Tooltip("La sombra en el suelo (se copia; el original se apaga). Puede ir vacía.")]
        [SerializeField] private Transform shadowTemplate;
        [SerializeField] private float shadowStartScale = 0.25f;
        [SerializeField] private float shadowEndScale = 1f;

        [Header("Caída")]
        [Tooltip("Segundos que tarda en caer: es lo que dura el aviso de la sombra.")]
        [SerializeField, Min(0.05f)] private float fallSeconds = 0.8f;
        [SerializeField, Min(0f)] private float dropHeight = 14f;
        [Tooltip("Sale del suelo en vez de caer del techo (columnas): espera escondida mientras avisa su marca y sube de golpe para llegar arriba justo en el golpe; al irse, vuelve a bajar.")]
        [SerializeField] private bool fromBelow;
        [Tooltip("Lo que cae, de dónde viene (en ejes de este objeto; se normaliza): arriba, o inclinado para que caiga en diagonal, como un meteorito.")]
        [SerializeField] private Vector3 fallFrom = Vector3.up;
        [Tooltip("Segundos que tarda en subir lo que sale del suelo (acaba justo en el golpe).")]
        [SerializeField, Min(0.02f)] private float riseSeconds = 0.08f;
        [Tooltip("Con «fromBelow», sale de la pared de su lado (a izquierda o derecha de este objeto) en vez del suelo: llamaradas, lanzallamas.")]
        [SerializeField] private bool fromWall;
        [Tooltip("Segundos que se queda en el suelo antes de irse.")]
        [SerializeField, Min(0f)] private float lingerSeconds = 0.5f;

        [Header("Puntería")]
        [Tooltip("Cae donde va a estar el objetivo al tocar el suelo (su sitio más su velocidad), decidido al soltarse: la sombra avisa y da tiempo a apartarse. Sin objetivo, en su punto de la lista.")]
        [SerializeField] private bool aimAtTarget;
        [Tooltip("Metros que puede apartarse de su punto de la lista como mucho (para no salirse del sitio).")]
        [SerializeField, Min(0f)] private float aimMaxDistance = 5f;
        [Tooltip("Cuánto cuenta la velocidad del objetivo al apuntar (1 = donde estará si sigue igual).")]
        [SerializeField, Range(0f, 1.5f)] private float aimLead = 1f;

        [Header("Golpe")]
        [SerializeField, Min(0f)] private float radius = 1.3f;
        [Tooltip("Largo del golpe a lo largo del «adelante» de este objeto (0 = solo un círculo): una franja, como un rayo que barre una línea.")]
        [SerializeField, Min(0f)] private float strikeLength;
        [Tooltip("Daño al tocar el suelo. 0: solo se ve (una presentación).")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [Tooltip("Altura del golpe sobre el suelo. 0: la de siempre (alcanza aunque se salte). Bajo: algo que barre el suelo y se salta.")]
        [SerializeField, Min(0f)] private float strikeHeight;

        [Header("Sonido")]
        [Tooltip("Lo que suena al tocar el suelo. Se lanza con el adelanto del retardo de audio del reloj, para oírse justo en el golpe.")]
        [SerializeField] private AudioClip landingSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        [Tooltip("Al tocar el suelo (una sacudida, un efecto).")]
        public UnityEvent onLand = new UnityEvent();

        private const int Copias = 6;

        private struct Caida
        {
            public GameObject cuerpo;
            public Transform sombra;
            public float segundo;
            public Vector3 punto;
            public bool activa;
            public bool tocada;
            public bool sonada;
        }

        private readonly List<float> _tiempos = new List<float>(32);
        private readonly Caida[] _caidas = new Caida[Copias];
        private int _siguiente;
        private double _ultimoSegundo = -1d;
        private bool _preparado;
        private Vector3 _objetivoAntes;
        private Vector3 _velocidadDelObjetivo;
        private bool _hayObjetivoAntes;

        private void OnEnable()
        {
            if (clock == null) clock = TempoClock.Current;
            Preparar();
            _tiempos.Clear();
            if (clock != null && clock.Score != null)
                clock.Score.CollectCueTimes(voices, fromSeconds, toSeconds, _tiempos);
            _ultimoSegundo = -1d;
        }

        private void OnDisable()
        {
            for (int i = 0; i < _caidas.Length; i++) Apagar(i);
        }

        private void Preparar()
        {
            if (_preparado) return;
            _preparado = true;
            for (int i = 0; i < Copias; i++)
            {
                if (fallingTemplate != null)
                {
                    _caidas[i].cuerpo = Instantiate(fallingTemplate, transform);
                    _caidas[i].cuerpo.SetActive(false);
                }
                if (shadowTemplate != null)
                {
                    _caidas[i].sombra = Instantiate(shadowTemplate, transform);
                    _caidas[i].sombra.gameObject.SetActive(false);
                }
            }
            if (fallingTemplate != null) fallingTemplate.SetActive(false);
            if (shadowTemplate != null) shadowTemplate.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (clock == null || !clock.IsRunning || !clock.FollowsSong || landingPoints.Length == 0) return;
            double t = clock.SongTime;
            if (t < _ultimoSegundo - 0.05d || _ultimoSegundo < 0d) Recolocar(t);
            _ultimoSegundo = t;
            if (aimAtTarget) SeguirAlObjetivo();

            while (_siguiente < _tiempos.Count && t >= _tiempos[_siguiente] - fallSeconds)
            {
                Soltar(_siguiente, t);
                _siguiente++;
            }

            for (int i = 0; i < _caidas.Length; i++)
            {
                if (!_caidas[i].activa) continue;
                ref var c = ref _caidas[i];
                float falta = (float)(c.segundo - t);
                if (!c.sonada && landingSound != null && falta <= clock.OutputLatency)
                {
                    c.sonada = true;
                    Sonar(c.punto);
                }
                if (falta > 0f)
                {
                    float u = 1f - Mathf.Clamp01(falta / fallSeconds);
                    // Lo que cae acelera todo el camino; lo que sale del suelo espera escondido y sube de golpe.
                    float queda = fromBelow ? Mathf.Clamp01(falta / riseSeconds) : 1f - u * u;
                    Colocar(ref c, c.punto + Escondido(c.punto) * queda, u);
                    continue;
                }
                if (!c.tocada)
                {
                    c.tocada = true;
                    Colocar(ref c, c.punto, 1f);
                    if (damage > 0f) Golpear(c.punto);
                    onLand.Invoke();
                }
                if (fromBelow)
                {
                    // Las que salen del suelo se vuelven a meter en su último cuarto de segundo.
                    float bajando = Mathf.Clamp01((-falta - (lingerSeconds - 0.25f)) / 0.25f);
                    if (bajando > 0f) Colocar(ref c, c.punto + Escondido(c.punto) * bajando, 1f);
                }
                if (-falta >= lingerSeconds) Apagar(i);
            }
        }

        /// <summary>Un círculo en el punto o, con <see cref="strikeLength"/>, círculos seguidos a lo largo de la franja.</summary>
        private void Golpear(Vector3 punto)
        {
            if (Strike == null) return;
            if (strikeLength <= radius * 2f || radius <= 0f) { Strike(punto, radius, damage, strikeHeight); return; }
            Vector3 adelante = transform.forward;
            int n = Mathf.CeilToInt(strikeLength / (radius * 1.5f));
            for (int i = 0; i <= n; i++)
                Strike(punto + adelante * (strikeLength * ((float)i / n - 0.5f)), radius, damage, strikeHeight);
        }

        private void SeguirAlObjetivo()
        {
            var donde = TargetPosition != null ? TargetPosition() : null;
            if (!donde.HasValue || Time.deltaTime <= 0f) { _hayObjetivoAntes = false; return; }
            Vector3 p = donde.Value;
            if (_hayObjetivoAntes)
            {
                Vector3 v = (p - _objetivoAntes) / Time.deltaTime;
                v.y = 0f;
                // Un salto (teletransporte, reintento) no es velocidad.
                if (v.sqrMagnitude > 400f) v = Vector3.zero;
                _velocidadDelObjetivo = Vector3.Lerp(_velocidadDelObjetivo, v, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            _objetivoAntes = p;
            _hayObjetivoAntes = true;
        }

        private void Soltar(int marca, double ahora)
        {
            int libre = -1;
            for (int i = 0; i < _caidas.Length; i++) if (!_caidas[i].activa) { libre = i; break; }
            if (libre < 0) return;
            var punto = landingPoints[marca % landingPoints.Length];
            if (punto == null) return;
            ref var c = ref _caidas[libre];
            c.activa = true;
            c.tocada = false;
            c.sonada = false;
            c.segundo = _tiempos[marca];
            c.punto = punto.position;
            if (aimAtTarget && _hayObjetivoAntes)
            {
                float falta = Mathf.Max(0f, (float)(c.segundo - ahora));
                Vector3 apunta = _objetivoAntes + _velocidadDelObjetivo * (falta * aimLead);
                Vector3 desvio = apunta - c.punto;
                desvio.y = 0f;
                c.punto += Vector3.ClampMagnitude(desvio, aimMaxDistance);
            }
            if (c.cuerpo != null) c.cuerpo.SetActive(true);
            if (c.sombra != null) c.sombra.gameObject.SetActive(true);
            Colocar(ref c, c.punto + Escondido(c.punto), 0f);
        }

        /// <summary>
        /// Dónde está la pieza antes de llegar, respecto a su sitio: arriba (o de donde diga «fallFrom») lo que cae; abajo lo que sale
        /// del suelo; dentro del muro de su lado lo que sale de la pared.
        /// </summary>
        private Vector3 Escondido(Vector3 punto)
        {
            if (!fromBelow)
                return (fallFrom.sqrMagnitude > 0.0001f ? transform.TransformDirection(fallFrom).normalized : Vector3.up) * dropHeight;
            if (!fromWall) return Vector3.down * dropHeight;
            float lado = Vector3.Dot(punto - transform.position, transform.right) >= 0f ? 1f : -1f;
            return transform.right * (lado * dropHeight);
        }

        private void Colocar(ref Caida c, Vector3 donde, float avance)
        {
            if (c.cuerpo != null) c.cuerpo.transform.position = donde;
            if (c.sombra != null)
            {
                c.sombra.position = c.punto + Vector3.up * 0.03f;
                c.sombra.localScale = shadowTemplate.localScale * Mathf.Lerp(shadowStartScale, shadowEndScale, avance);
            }
        }

        private void Apagar(int i)
        {
            _caidas[i].activa = false;
            if (_caidas[i].cuerpo != null) _caidas[i].cuerpo.SetActive(false);
            if (_caidas[i].sombra != null) _caidas[i].sombra.gameObject.SetActive(false);
        }

        private void Sonar(Vector3 donde)
        {
            if (PlaySound != null) PlaySound(landingSound, donde, soundVolume);
            else AudioSource.PlayClipAtPoint(landingSound, donde, soundVolume);
        }

        /// <summary>Tras un salto de la canción, se recogen las caídas y se sigue desde ahí.</summary>
        private void Recolocar(double t)
        {
            for (int i = 0; i < _caidas.Length; i++) Apagar(i);
            _siguiente = 0;
            while (_siguiente < _tiempos.Count && _tiempos[_siguiente] - fallSeconds < t) _siguiente++;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Strike = null;
            PlaySound = null;
            TargetPosition = null;
        }
#endif
    }
}
