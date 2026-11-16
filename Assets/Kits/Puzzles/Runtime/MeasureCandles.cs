using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Cuenta atrás en compases que se ve en el escenario: una vela por compás y cada fuerte apaga
    /// una. Se usa en las salas donde el jugador tiene que pensar mientras el reloj corre; al
    /// apagarse la última, avisa al mecanismo que corresponda.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeasureCandles : MonoBehaviour
    {
        [Serializable] public sealed class CompasesEvent : UnityEvent<int> { }

        [Header("Ritmo")]
        [Tooltip("Reloj del tramo. Si se deja vacío, se usa el de la escena.")]
        [SerializeField] private TempoClock pulse;
        [Tooltip("Compases que dura la cuenta.")]
        [SerializeField, Min(1)] private int totalMeasures = 24;

        [Header("Escenario")]
        [Tooltip("Llamas que se apagan, una por compás (la última, al agotarse). Si hay menos que compases, se reparten.")]
        [SerializeField] private GameObject[] flames;
        [Tooltip("Empieza a contar sola al activarse.")]
        [SerializeField] private bool startOnEnable;

        [Header("Eventos")]
        [Tooltip("Los compases que le quedan, uno por compás.")]
        [SerializeField] private CompasesEvent alPasarCompas = new CompasesEvent();
        [Tooltip("Se apagó la última vela.")]
        [SerializeField] private UnityEvent alAgotarse = new UnityEvent();

        public int RemainingMeasures { get; private set; }
        public bool IsCounting { get; private set; }

        private TempoClock _pulse;
        private bool[] _llamasEncendidas;
        private float _siguienteEnganche;

        private void Awake()
        {
            _llamasEncendidas = new bool[flames != null ? flames.Length : 0];
            Refill();
        }

        private void OnEnable()
        {
            _pulse = pulse != null ? pulse : TempoClock.Current;
            if (_pulse == null) return;
            _pulse.Downbeat += AlLlegarElFuerte;
            if (startOnEnable) BeginCountdown();
        }

        private void OnDisable()
        {
            if (_pulse == null) return;
            _pulse.Downbeat -= AlLlegarElFuerte;
            _pulse = null;
        }

        /// <summary>Enciende todas las velas y deja la cuenta llena, sin empezar.</summary>
        public void Refill()
        {
            RemainingMeasures = Mathf.Max(1, totalMeasures);
            IsCounting = false;
            if (flames == null) return;
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] != null) flames[i].SetActive(true);
                if (_llamasEncendidas != null) _llamasEncendidas[i] = true;
            }
        }

        public void BeginCountdown()
        {
            if (RemainingMeasures <= 0) Refill();
            IsCounting = true;
        }

        public void StopCountdown() => IsCounting = false;

        private void Update()
        {
            // Se engancha al pulso en cuanto esté: al cargar la escena puede activarse antes.
            if (_pulse != null || Time.time < _siguienteEnganche) return;
            _siguienteEnganche = Time.time + 0.5f;
            _pulse = pulse != null ? pulse : TempoClock.Current;
            if (_pulse != null) _pulse.Downbeat += AlLlegarElFuerte;
        }

        private void AlLlegarElFuerte(int compas)
        {
            if (!IsCounting) return;
            RemainingMeasures--;
            ApagarLaQueToca();
            alPasarCompas.Invoke(RemainingMeasures);
            if (RemainingMeasures > 0) return;
            IsCounting = false;
            alAgotarse.Invoke();
        }

        /// <summary>Apaga las que ya no tocan: de las 24 velas, se van una por compás de la primera a la última.</summary>
        private void ApagarLaQueToca()
        {
            if (flames == null || flames.Length == 0) return;
            int encendidasQueTocan = Mathf.CeilToInt(flames.Length * (RemainingMeasures / (float)Mathf.Max(1, totalMeasures)));
            for (int i = encendidasQueTocan; i < flames.Length; i++)
            {
                if (_llamasEncendidas[i])
                {
                    _llamasEncendidas[i] = false;
                    if (flames[i] != null) flames[i].SetActive(false);
                }
            }
        }
    }
}
