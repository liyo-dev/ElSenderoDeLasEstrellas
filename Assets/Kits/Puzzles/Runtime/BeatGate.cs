using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Rejilla o plataforma que dura un número de compases de la música: se abre o aparece al
    /// pedírselo y vuelve a su sitio cuando se cumplen. Sirve para la rejilla que abre un
    /// interruptor durante dos compases y para las plataformas que enciende un faro mientras suenan.
    ///
    /// Quien la dispara la llama desde un evento (un receptor de magia, un plato de peso, un
    /// interruptor) y ella sola lleva la cuenta con el pulso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeatGate : MonoBehaviour
    {
        [Serializable] public sealed class MedidaEvent : UnityEvent<int> { }

        public enum Modo
        {
            /// <summary>Empieza puesta y se aparta mientras dura (rejilla que se abre, puente que baja).</summary>
            SeAbre,
            /// <summary>Empieza quitada y aparece mientras dura (plataforma que se enciende).</summary>
            Aparece
        }

        [Header("Ritmo")]
        [Tooltip("Pulso de la música. Si se deja vacío, se usa el de la escena.")]
        [SerializeField] private MusicPulse pulse;
        [Tooltip("Compases que dura abierta o presente.")]
        [SerializeField, Min(1)] private int durationMeasures = 2;
        [Tooltip("Cómo se comporta: se aparta al abrirse o aparece al activarse.")]
        [SerializeField] private Modo modo = Modo.SeAbre;
        [Tooltip("Cómo está al empezar (antes de que nadie la dispare).")]
        [SerializeField] private bool activeAtStart;
        [Tooltip("No se cierra mientras quede alguien del grupo del lado de donde se viene: se cierra cuando han pasado todos.")]
        [SerializeField] private bool esperarAlGrupo = true;

        [Header("Aspecto")]
        [Tooltip("Lo que se ve de la pieza: en el modo «Aparece» se enseña solo mientras está activa.")]
        [SerializeField] private Renderer[] visuales;

        /// <summary>
        /// ¿Queda alguien del grupo del lado de atrás de la puerta? Lo dice el juego: (punto de la
        /// puerta, hacia donde se sale). El kit no sabe quién es el grupo.
        /// </summary>
        public static Func<Vector3, Vector3, bool> QuedaAlguienDetras;

        [Header("Aspecto")]
        [Tooltip("Lo que se mueve al abrirse o aparecer. Si se deja vacío, se mueve este objeto.")]
        [SerializeField] private Transform visual;
        [Tooltip("Desplazamiento del visual al estar activa, en ejes locales (m).")]
        [SerializeField] private Vector3 activeOffset = new Vector3(0f, -3f, 0f);
        [SerializeField, Min(0.1f)] private float moveSpeed = 8f;
        [Tooltip("Lo que deja de sujetar al abrirse. Si se deja vacío, se cogen los colisionadores de este objeto y sus hijos.")]
        [SerializeField] private Collider[] colliders;

        [Header("Eventos")]
        [Tooltip("Al abrirse o aparecer.")]
        [SerializeField] private UnityEvent alActivar = new UnityEvent();
        [Tooltip("Al volver a su sitio.")]
        [SerializeField] private UnityEvent alCerrar = new UnityEvent();
        [Tooltip("Los compases que le quedan, uno por compás mientras está activa.")]
        [SerializeField] private MedidaEvent alPasarCompas = new MedidaEvent();

        public bool IsActive { get; private set; }
        public int RemainingBeats { get; private set; }

        private MusicPulse _pulse;
        private Transform _visual;
        private Vector3 _visualLocalBase;
        private bool _permanente;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => QuedaAlguienDetras = null;
#endif

        private void Awake()
        {
            _visual = visual != null ? visual : transform;
            _visualLocalBase = _visual.localPosition;
            if (colliders == null || colliders.Length == 0)
                colliders = GetComponentsInChildren<Collider>(true);
            if (visuales == null || visuales.Length == 0)
                visuales = GetComponentsInChildren<Renderer>(true);
            Colocar(activeAtStart);
        }

        private void OnEnable()
        {
            _pulse = pulse != null ? pulse : MusicPulse.Current;
            if (_pulse == null) return;
            _pulse.Beat += AlPasarUnTiempo;
            _pulse.Downbeat += AlCambiarCompas;
        }

        private void OnDisable()
        {
            if (_pulse == null) return;
            _pulse.Beat -= AlPasarUnTiempo;
            _pulse.Downbeat -= AlCambiarCompas;
            _pulse = null;
        }

        /// <summary>Se abre (o aparece) los compases de su configuración.</summary>
        public void TriggerForMeasures() => TriggerForMeasures(durationMeasures);

        /// <summary>Se abre (o aparece) el número de compases que se diga; si ya estaba, se renueva.</summary>
        public void TriggerForMeasures(int measures)
        {
            if (_permanente) return;
            int tiempos = _pulse != null ? _pulse.BeatsPerMeasure : 4;
            RemainingBeats = Mathf.Max(1, measures) * Mathf.Max(1, tiempos);
            Colocar(true);
            alActivar.Invoke();
        }

        /// <summary>Abierta para siempre (hasta <see cref="ResetGate"/>) o cerrada.</summary>
        public void SetPermanent(bool open)
        {
            _permanente = open;
            RemainingBeats = 0;
            Colocar(open);
            if (open) alActivar.Invoke();
            else alCerrar.Invoke();
        }

        /// <summary>Vuelve a como estaba al empezar el tramo.</summary>
        public void ResetGate()
        {
            _permanente = false;
            RemainingBeats = 0;
            Colocar(activeAtStart);
            _visual.localPosition = _visualLocalBase + (activeAtStart ? activeOffset : Vector3.zero);
        }

        private void AlPasarUnTiempo(int compas, int tiempo)
        {
            if (!IsActive || _permanente) return;
            RemainingBeats--;
            if (RemainingBeats > 0) return;

            // Se cierra cuando le toca… salvo que quede alguien del grupo por pasar.
            if (esperarAlGrupo && QuedaAlguienDetras != null && QuedaAlguienDetras(transform.position, transform.forward))
            {
                RemainingBeats = 1;
                return;
            }

            Colocar(false);
            alCerrar.Invoke();
        }

        private void AlCambiarCompas(int compas)
        {
            if (!IsActive || _permanente) return;
            int tiemposPorCompas = Mathf.Max(1, _pulse != null ? _pulse.BeatsPerMeasure : 4);
            alPasarCompas.Invoke(Mathf.CeilToInt(RemainingBeats / (float)tiemposPorCompas));
        }

        /// <summary>Los colisionadores sujetan cuando la pieza está en su sitio, según el modo.</summary>
        private void Colocar(bool activa)
        {
            IsActive = activa;
            bool sujetan = modo == Modo.SeAbre ? !activa : activa;
            if (colliders != null)
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null) colliders[i].enabled = sujetan;
            // Lo que aparece se ve cuando está: si no, se queda una plataforma fantasma a la vista.
            if (modo == Modo.Aparece && visuales != null)
                for (int i = 0; i < visuales.Length; i++)
                    if (visuales[i] != null) visuales[i].enabled = activa;
        }

        private void Update()
        {
            AsegurarPulso();
            Vector3 objetivo = _visualLocalBase + (IsActive ? activeOffset : Vector3.zero);
            _visual.localPosition = Vector3.MoveTowards(_visual.localPosition, objetivo, moveSpeed * Time.deltaTime);
        }

        /// <summary>Se engancha al pulso en cuanto esté: al cargar la escena puede activarse antes.</summary>
        private void AsegurarPulso()
        {
            if (_pulse != null) return;
            _pulse = pulse != null ? pulse : MusicPulse.Current;
            if (_pulse == null) return;
            _pulse.Beat += AlPasarUnTiempo;
            _pulse.Downbeat += AlCambiarCompas;
        }
    }
}
