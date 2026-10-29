using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sendero.UI
{
    /// <summary>
    /// Muestra un icono en el HUD que cambia según el período del día.
    /// Cuando llueve, sobreescribe el icono con rainSprite hasta que para la lluvia.
    /// Busca DayNightCycle automáticamente cada vez que se carga una escena nueva.
    ///
    /// Se esconde con clave (Ocultar(quien)/Mostrar(quien)), como el HUD y el minimapa: no vuelve
    /// a verse hasta que lo suelten todos los que lo pidieron.
    /// </summary>
    public class TimeOfDayIndicator : MonoBehaviour, IOcultableDuranteCinematica
    {
        public static TimeOfDayIndicator Instance { get; private set; }

        [Serializable]
        public struct TimeIconEntry
        {
            public DayNightCycle.TimeOfDay timeOfDay;
            public Sprite sprite;
        }

        [Header("Referencias")]
        [Tooltip("La Image de UI donde se mostrará el icono")]
        [SerializeField] private Image iconImage;

        [Header("Mapeado de iconos")]
        [Tooltip("Un sprite por cada período de DayNightCycle.TimeOfDay")]
        [SerializeField] private TimeIconEntry[] icons;

        [Tooltip("Icono que se muestra mientras llueve (sobreescribe el del período actual)")]
        [SerializeField] private Sprite rainSprite;

        private DayNightCycle _dayNight;
        private DayNightCycle.TimeOfDay _currentPeriod;
        private Sprite _spriteActual;
        private readonly HashSet<object> _ocultadoPor = new();

        // Claves de quien lo esconde sin pasar una propia.
        private static readonly object PorLlamadaSinClave = new();
        private static readonly object PorAvisoDeViaje = new();

        private void Awake()
        {
            Instance = this;
            OcultablesDeCinematica.Registrar(this);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TeleportHintUI.OnHintShown  += OnTeleportHintShown;
            TeleportHintUI.OnHintHidden += OnTeleportHintHidden;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TeleportHintUI.OnHintShown  -= OnTeleportHintShown;
            TeleportHintUI.OnHintHidden -= OnTeleportHintHidden;
            UnsubscribeFromCycle();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            OcultablesDeCinematica.Quitar(this);
        }

        /// <summary>Esconde el icono hasta que <paramref name="quien"/> llame a <see cref="Mostrar"/>.</summary>
        public void Ocultar(object quien)
        {
            if (quien == null || !_ocultadoPor.Add(quien)) return;
            Refrescar();
        }

        /// <summary>Retira la petición de <paramref name="quien"/>; el icono vuelve si nadie más lo esconde.</summary>
        public void Mostrar(object quien)
        {
            if (quien == null || !_ocultadoPor.Remove(quien)) return;
            Refrescar();
        }

        /// <summary>Esconde el icono con una clave compartida. Mejor <see cref="Ocultar"/> con clave propia.</summary>
        public void Hide() => Ocultar(PorLlamadaSinClave);

        /// <summary>Suelta la clave compartida de <see cref="Hide"/>.</summary>
        public void Show() => Mostrar(PorLlamadaSinClave);

        void IOcultableDuranteCinematica.Ocultar(object quien) => Ocultar(quien);
        void IOcultableDuranteCinematica.Mostrar(object quien) => Mostrar(quien);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Si ya tenemos referencia válida no hace falta buscar de nuevo
            if (_dayNight != null) return;

            var found = FindAnyObjectByType<DayNightCycle>();
            if (found == null) return;

            _dayNight = found;
            _dayNight.TimeOfDayChanged += OnTimeOfDayChanged;
            _dayNight.RainStarted      += OnRainStarted;
            _dayNight.RainStopped      += OnRainStopped;

            OnTimeOfDayChanged(_dayNight.CurrentTimeOfDay);
            if (_dayNight.IsRaining) OnRainStarted();
        }

        private void UnsubscribeFromCycle()
        {
            if (_dayNight == null) return;
            _dayNight.TimeOfDayChanged -= OnTimeOfDayChanged;
            _dayNight.RainStarted      -= OnRainStarted;
            _dayNight.RainStopped      -= OnRainStopped;
            _dayNight = null;
        }

        private void OnTimeOfDayChanged(DayNightCycle.TimeOfDay time)
        {
            _currentPeriod = time;

            // Si está lloviendo mantenemos el rainSprite, no cambiamos
            if (_dayNight != null && _dayNight.IsRaining) return;

            ApplySprite(SpriteForPeriod(time));
        }

        private void OnRainStarted()
        {
            if (rainSprite != null)
                ApplySprite(rainSprite);
        }

        private void OnRainStopped()
        {
            ApplySprite(SpriteForPeriod(_currentPeriod));
        }

        private Sprite SpriteForPeriod(DayNightCycle.TimeOfDay time)
        {
            foreach (var entry in icons)
                if (entry.timeOfDay == time) return entry.sprite;
            return null;
        }

        private void ApplySprite(Sprite sprite)
        {
            _spriteActual = sprite;
            Refrescar();
        }

        // El icono se ve si tiene sprite y nadie lo tiene escondido.
        private void Refrescar()
        {
            if (iconImage == null) return;
            iconImage.sprite  = _spriteActual;
            iconImage.enabled = _spriteActual != null && _ocultadoPor.Count == 0;
        }

        private void OnTeleportHintShown() => Ocultar(PorAvisoDeViaje);

        private void OnTeleportHintHidden() => Mostrar(PorAvisoDeViaje);

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
#endif
    }
}
