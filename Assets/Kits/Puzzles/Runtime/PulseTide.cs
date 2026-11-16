using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kits.Puzzles
{
    /// <summary>
    /// Marea que persigue al compás: avanza con el reloj, se puede parar (en una sala donde hay que
    /// pensar, o con un sello) y avisa cuando alcanza a quien no debía. Quién cuenta como alcanzado
    /// lo decide el juego con <see cref="IsTarget"/>.
    ///
    /// Lo que está medido es cuántos metros por compás, no por segundo: si el tramo acelera, la
    /// marea acelera con él.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PulseTide : MonoBehaviour
    {
        /// <summary>¿Este collider es de quien la marea puede alcanzar?</summary>
        public static Func<Collider, bool> IsTarget;

        [Header("Ritmo")]
        [Tooltip("Reloj del tramo. Si se deja vacío, se usa el de la escena.")]
        [SerializeField] private TempoClock pulse;
        [Tooltip("Hacia dónde avanza (se usa su dirección; se normaliza).")]
        [SerializeField] private Vector3 advanceDirection = Vector3.forward;
        [Tooltip("Metros que avanza en cada compás.")]
        [SerializeField, Min(0f)] private float metersPerMeasure = 3.5f;
        [Tooltip("Empieza a avanzar al activarse.")]
        [SerializeField] private bool advanceAtStart;

        [Header("Eventos")]
        [Tooltip("Ha alcanzado a quien no debía.")]
        [SerializeField] private UnityEvent alAlcanzar = new UnityEvent();

        /// <summary>Ha alcanzado a quien no debía (para quien prefiera escuchar por código).</summary>
        public event Action<PulseTide> Caught;

        public bool IsAdvancing { get; private set; }
        public int PausedMeasuresRemaining { get; private set; }

        private TempoClock _pulse;
        private Vector3 _inicio;
        private bool _enfriada;
        private float _siguienteEnganche;

        private void Awake()
        {
            _inicio = transform.position;
            var zona = GetComponent<Collider>();
            if (zona != null) zona.isTrigger = true;
        }

        private void OnEnable()
        {
            _pulse = pulse != null ? pulse : TempoClock.Current;
            if (_pulse != null) _pulse.Downbeat += AlPasarCompas;
            if (advanceAtStart) Resume();
        }

        private void OnDisable()
        {
            if (_pulse != null) _pulse.Downbeat -= AlPasarCompas;
            _pulse = null;
        }

        public void Resume()
        {
            if (_enfriada) return;
            IsAdvancing = true;
            PausedMeasuresRemaining = 0;
        }

        public void Pause() => IsAdvancing = false;

        /// <summary>Se para los compases que se digan (el sello de mente que la sujeta).</summary>
        public void PauseForMeasures(int measures)
        {
            IsAdvancing = false;
            PausedMeasuresRemaining = Mathf.Max(0, measures);
        }

        /// <summary>Se enfría: deja de avanzar para siempre y no vuelve a alcanzar a nadie.</summary>
        public void CoolDown()
        {
            _enfriada = true;
            IsAdvancing = false;
            alAlcanzar.Invoke();
        }

        /// <summary>Vuelve al sitio de partida y se para, para repetir el tramo.</summary>
        public void ResetTide()
        {
            transform.position = _inicio;
            _enfriada = false;
            IsAdvancing = advanceAtStart;
            PausedMeasuresRemaining = 0;
        }

        private void AlPasarCompas(int compas)
        {
            if (PausedMeasuresRemaining <= 0) return;
            PausedMeasuresRemaining--;
            if (PausedMeasuresRemaining <= 0) Resume();
        }

        private void Update()
        {
            if (_pulse == null && Time.time >= _siguienteEnganche)
            {
                _siguienteEnganche = Time.time + 0.5f;
                _pulse = pulse != null ? pulse : TempoClock.Current;
                if (_pulse != null) _pulse.Downbeat += AlPasarCompas;
            }

            if (!IsAdvancing || _pulse == null) return;
            float metrosPorSegundo = metersPerMeasure / Mathf.Max(0.01f, _pulse.SecondsPerMeasure);
            Vector3 direccion = advanceDirection.sqrMagnitude > 0.0001f ? advanceDirection.normalized : Vector3.forward;
            transform.position += direccion * (metrosPorSegundo * Time.deltaTime);
        }

        private void OnTriggerStay(Collider otro)
        {
            if (_enfriada || IsTarget == null || otro == null) return;
            if (!IsTarget(otro)) return;
            IsAdvancing = false;
            _enfriada = true;
            alAlcanzar.Invoke();
            Caught?.Invoke(this);
        }
    }
}
