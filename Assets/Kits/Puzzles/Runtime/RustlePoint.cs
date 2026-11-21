using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Un crujido de una <see cref="StalkingPresence"/>: un sitio del recorrido donde, al llegar
    /// quien lleva el objeto, algo se mueve entre los arbustos. Aviso (sonido que se acerca, matas
    /// que se agitan en ola, la mirada hacia el ruido), ventana en la que hay que quedarse quieto y,
    /// al acabar, el ruido se aleja. Si quien lleva el objeto sigue andando, la presencia se lo tira
    /// a <see cref="Landing"/>. Todo en datos: tiempos, sonidos, matas y sitio donde cae.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RustlePoint : MonoBehaviour
    {
        [Header("Cuándo salta")]
        [Tooltip("Metros alrededor de este punto en los que salta el crujido.")]
        [SerializeField, Min(0.5f)] private float radio = 6f;
        [Tooltip("Orden en el recorrido. Un crujido no salta hasta que han salido los de orden menor (los que repiten no cuentan). -1 = sin orden.")]
        [SerializeField] private int orden = -1;
        [Tooltip("Segundos hasta poder volver a saltar. 0 = salta una sola vez (un tramo del camino); mayor que 0 = repite (un sitio por el que la presencia no deja pasar tranquilo).")]
        [SerializeField, Min(0f)] private float repetirCada;

        [Header("Tiempos")]
        [Tooltip("Segundos de aviso: da tiempo a pararse. Moverse aún no cuenta.")]
        [SerializeField, Min(0.1f)] private float segundosDeAviso = 1.2f;
        [Tooltip("Segundos que hay que quedarse quieto.")]
        [SerializeField, Min(0.1f)] private float segundosQuieto = 2.5f;
        [Tooltip("Segundos que tarda el ruido en alejarse al acabar.")]
        [SerializeField, Min(0.1f)] private float segundosAlejandose = 1.6f;
        [Tooltip("Demostración: al acabar el aviso, tira el objeto aunque se esté quieto (la primera vez, para enseñar sin texto qué pasa).")]
        [SerializeField] private bool tiraSiempre;

        [Header("Dónde se mira y dónde cae")]
        [Tooltip("Hacia dónde se gira la mirada durante el aviso. Vacío = este punto.")]
        [SerializeField] private Transform mirarA;
        [Tooltip("Sitio, cerca y a la vista, donde acaba el objeto tirado.")]
        [SerializeField] private Transform caeEn;
        [Tooltip("Segundos de vuelo del objeto tirado.")]
        [SerializeField, Min(0.2f)] private float segundosDeVuelo = 0.7f;

        [Header("Matas")]
        [Tooltip("Matas que se agitan, en el orden de la ola. Vacío = las hijas de este punto.")]
        [SerializeField] private RustleShaker[] matas;
        [Tooltip("Intensidad del agitado.")]
        [SerializeField, Min(0f)] private float intensidad = 1f;
        [Tooltip("Segundos entre una mata y la siguiente (la ola que se acerca).")]
        [SerializeField, Min(0f)] private float retrasoDeLaOla = 0.15f;

        [Header("Sonido")]
        [Tooltip("Fuente 3D del ruido, en un objeto hijo: viaja de 'llegaDesde' a este punto y luego a 'seVaHacia'. Vacío = la de este objeto (no viaja).")]
        [SerializeField] private AudioSource fuente;
        [SerializeField] private Transform llegaDesde;
        [SerializeField] private Transform seVaHacia;
        [Tooltip("Golpe al empezar el aviso (una rama que cruje).")]
        [SerializeField] private AudioClip alAvisar;
        [Tooltip("Ruido continuo mientras dura (hojas que se mueven).")]
        [SerializeField] private AudioClip mientrasDura;
        [Tooltip("Golpe al tirar el objeto.")]
        [SerializeField] private AudioClip alTirar;
        [SerializeField, Range(0f, 1f)] private float volumen = 0.9f;

        [Header("Lo que se ve un instante")]
        [Tooltip("Algo que se ve al irse la presencia (unos ojos, una sombra). Se enciende un momento.")]
        [SerializeField] private GameObject vislumbre;
        [SerializeField, Min(0f)] private float segundosDelVislumbre = 0.8f;
        [Tooltip("También se ve cuando tira el objeto.")]
        [SerializeField] private bool vislumbreAlTirar = true;

        [Header("Eventos")]
        [SerializeField] private UnityEvent alEmpezar = new();
        [SerializeField] private UnityEvent alTirarElObjeto = new();
        [SerializeField] private UnityEvent alPasar = new();

        public float Radius => radio;
        public int Order => orden;
        public bool Repeats => repetirCada > 0f;
        public float WarningSeconds => segundosDeAviso;
        public float FreezeSeconds => segundosQuieto;
        public float LeavingSeconds => segundosAlejandose;
        public bool AlwaysKnocks => tiraSiempre;
        public Transform LookTarget => mirarA != null ? mirarA : transform;
        public Transform Landing => caeEn;
        public float FlightSeconds => segundosDeVuelo;
        public bool Done { get; private set; }

        private enum Fase { Quieto, Llegando, Yendose }
        private Fase _fase;
        private float _desde, _hasta;
        private float _siguiente;
        private float _apagarVislumbre = -1f;
        private bool _moverFuente;

        private void Awake()
        {
            if (fuente == null) fuente = GetComponent<AudioSource>();
            if (matas == null || matas.Length == 0) matas = GetComponentsInChildren<RustleShaker>(true);
            if (fuente != null) { fuente.playOnAwake = false; fuente.loop = true; fuente.spatialBlend = 1f; }
            // La fuente solo viaja si va en su propio objeto: moverla con el punto movería el punto.
            _moverFuente = fuente != null && fuente.transform != transform;
            if (vislumbre != null && vislumbre.activeSelf) vislumbre.SetActive(false);
            enabled = false;
        }

        public bool CanTrigger(float ahora) => !Done && ahora >= _siguiente;

        /// <summary>Empieza el aviso: la ola de matas, el golpe y el ruido que se acerca.</summary>
        public void BeginWarning()
        {
            float dura = segundosDeAviso + segundosQuieto;
            if (matas != null)
                for (int i = 0; i < matas.Length; i++)
                    if (matas[i] != null) matas[i].Shake(intensidad, dura - i * retrasoDeLaOla, i * retrasoDeLaOla);
            if (fuente != null)
            {
                if (_moverFuente) fuente.transform.position = llegaDesde != null ? llegaDesde.position : transform.position;
                if (alAvisar != null) fuente.PlayOneShot(alAvisar, volumen);
                if (mientrasDura != null) { fuente.clip = mientrasDura; fuente.volume = 0f; fuente.Play(); }
            }
            _fase = Fase.Llegando;
            _desde = Time.time;
            _hasta = _desde + dura;
            enabled = true;
            alEmpezar.Invoke();
        }

        /// <summary>Se ha estado quieto: el ruido se aleja y, si hay, se ve algo un instante.</summary>
        public void Pass()
        {
            Terminar();
            Vislumbre();
            alPasar.Invoke();
        }

        /// <summary>Se ha seguido andando (o es la demostración): golpe, matas que saltan y se va.</summary>
        public void Knocked()
        {
            if (fuente != null && alTirar != null) fuente.PlayOneShot(alTirar, volumen);
            if (matas != null)
                for (int i = 0; i < matas.Length; i++)
                    if (matas[i] != null) matas[i].Shake(intensidad * 1.8f, 0.6f);
            Terminar();
            if (vislumbreAlTirar) Vislumbre();
            alTirarElObjeto.Invoke();
        }

        /// <summary>Se corta sin más (ya no se lleva el objeto): se calma y puede volver a saltar.</summary>
        public void Abort()
        {
            if (matas != null)
                for (int i = 0; i < matas.Length; i++)
                    if (matas[i] != null) matas[i].Calm();
            _fase = Fase.Yendose;
            _desde = Time.time;
            _hasta = _desde + 0.4f;
            _siguiente = Time.time + 2f;
        }

        private void Terminar()
        {
            if (matas != null)
                for (int i = 0; i < matas.Length; i++)
                    if (matas[i] != null) matas[i].Calm();
            _fase = Fase.Yendose;
            _desde = Time.time;
            _hasta = _desde + segundosAlejandose;
            if (repetirCada > 0f) _siguiente = Time.time + repetirCada;
            else Done = true;
        }

        private void Vislumbre()
        {
            if (vislumbre == null || segundosDelVislumbre <= 0f) return;
            if (!vislumbre.activeSelf) vislumbre.SetActive(true);
            _apagarVislumbre = Time.time + segundosDelVislumbre;
            enabled = true;
        }

        private void Update()
        {
            float ahora = Time.time;
            if (_apagarVislumbre > 0f && ahora >= _apagarVislumbre)
            {
                _apagarVislumbre = -1f;
                if (vislumbre != null && vislumbre.activeSelf) vislumbre.SetActive(false);
            }

            float t = _hasta > _desde ? Mathf.Clamp01((ahora - _desde) / (_hasta - _desde)) : 1f;
            if (fuente != null && fuente.isPlaying)
            {
                if (_fase == Fase.Llegando)
                {
                    Vector3 a = llegaDesde != null ? llegaDesde.position : transform.position;
                    if (_moverFuente) fuente.transform.position = Vector3.Lerp(a, transform.position, Mathf.Min(1f, t * 2f));
                    fuente.volume = volumen * Mathf.Min(1f, t * 4f);
                }
                else if (_fase == Fase.Yendose)
                {
                    Vector3 b = seVaHacia != null ? seVaHacia.position : transform.position;
                    if (_moverFuente) fuente.transform.position = Vector3.Lerp(transform.position, b, t);
                    fuente.volume = volumen * (1f - t);
                }
            }

            if (_fase == Fase.Yendose && t >= 1f)
            {
                _fase = Fase.Quieto;
                if (fuente != null && fuente.isPlaying && fuente.clip == mientrasDura) fuente.Stop();
            }
            if (_fase == Fase.Quieto && _apagarVislumbre < 0f) enabled = false;
        }

        private void OnDisable()
        {
            if (_fase != Fase.Quieto && fuente != null && fuente.isPlaying) fuente.Stop();
            if (_fase != Fase.Quieto) _fase = Fase.Quieto;
            if (_apagarVislumbre > 0f) { _apagarVislumbre = -1f; if (vislumbre != null) vislumbre.SetActive(false); }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.2f, 0.9f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radio);
            if (caeEn != null) { Gizmos.DrawLine(transform.position, caeEn.position); Gizmos.DrawWireCube(caeEn.position, Vector3.one * 0.5f); }
        }
    }
}
