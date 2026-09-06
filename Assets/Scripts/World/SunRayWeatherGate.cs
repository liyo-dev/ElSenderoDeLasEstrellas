using System.Collections;
using UnityEngine;

/// <summary>
/// Controla la visibilidad del grupo de rayos de sol de Quibli: solo deben verse de día y con el
/// cielo despejado (ni lluvia ni nubosidad previa a la lluvia) — a petición explícita de Raúl
/// (4 sep 2026): "si conseguimos que se vean lo deben hacer solo si es de dia y no esta nublado o
/// llueve". Se engancha a los eventos públicos de DayNightCycle: no existe un único "IsClearSky" en
/// esa clase, así que se combina TimeOfDayChanged + RainStarted/RainStopped + CloudsBuildingUp.
///
/// CloudsBuildingUp se dispara justo ANTES de que empiece a llover (ver DayNightCycle.
/// CloudBuildUpRoutine) y dura poco (rainDarkenTransitionDuration, unos segundos) — normalmente
/// desemboca en RainStarted, que ya oculta los rayos por su cuenta. El único caso raro en que NO
/// desemboca en lluvia es que algo cancele el clima a mitad de esa nubosidad (StopRain llamado
/// mientras el cielo se está nublando): ese caso concreto no dispara RainStarted NI RainStopped (ver
/// el propio StopRain: "revertimos el look sin disparar onRainStarted/onRainStopped"), así que aquí
/// nos protegemos solos con un margen de seguridad (SegundosMargenNubosidad) tras el cual se vuelve
/// a comprobar el estado real (IsRaining) en vez de quedarnos "nublado" para siempre por ese caso.
///
/// Importante: este componente vive en el GameObject RAÍZ del grupo de rayos, y oculta/muestra sus
/// HIJOS uno a uno — nunca se desactiva a sí mismo (si se desactivara el propio GameObject raíz,
/// OnDisable se desuscribiría de los eventos y ya no podría volver a activarse solo).
/// </summary>
[DisallowMultipleComponent]
public class SunRayWeatherGate : MonoBehaviour
{
    [Tooltip("Periodos del día (DayNightCycle.TimeOfDay) en los que se permite ver los rayos de sol.")]
    public DayNightCycle.TimeOfDay[] periodosPermitidos =
    {
        DayNightCycle.TimeOfDay.Morning,
        DayNightCycle.TimeOfDay.AfterNoon,
        DayNightCycle.TimeOfDay.Sunset
    };

    private const float SegundosMargenNubosidad = 12f;

    private DayNightCycle _cicloDiaNoche;
    private bool _cieloNublado;
    private Coroutine _margenNubosidad;

    void OnEnable()
    {
        _cicloDiaNoche = Object.FindAnyObjectByType<DayNightCycle>();
        if (_cicloDiaNoche == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SunRayWeatherGate] No se encontró DayNightCycle en la escena; los rayos de sol se quedan siempre visibles.");
#endif
            return;
        }

        _cicloDiaNoche.TimeOfDayChanged += OnTimeOfDayChanged;
        _cicloDiaNoche.RainStarted += OnRainStarted;
        _cicloDiaNoche.RainStopped += OnRainStopped;
        _cicloDiaNoche.CloudsBuildingUp += OnCloudsBuildingUp;

        _cieloNublado = false;
        ActualizarVisibilidad();
    }

    void OnDisable()
    {
        if (_cicloDiaNoche == null) return;
        _cicloDiaNoche.TimeOfDayChanged -= OnTimeOfDayChanged;
        _cicloDiaNoche.RainStarted -= OnRainStarted;
        _cicloDiaNoche.RainStopped -= OnRainStopped;
        _cicloDiaNoche.CloudsBuildingUp -= OnCloudsBuildingUp;

        if (_margenNubosidad != null)
        {
            StopCoroutine(_margenNubosidad);
            _margenNubosidad = null;
        }
    }

    void OnTimeOfDayChanged(DayNightCycle.TimeOfDay _) => ActualizarVisibilidad();

    void OnRainStarted()
    {
        _cieloNublado = false; // a partir de ahora lo cubre IsRaining
        ActualizarVisibilidad();
    }

    void OnRainStopped()
    {
        _cieloNublado = false;
        ActualizarVisibilidad();
    }

    void OnCloudsBuildingUp()
    {
        _cieloNublado = true;
        ActualizarVisibilidad();

        if (_margenNubosidad != null) StopCoroutine(_margenNubosidad);
        _margenNubosidad = StartCoroutine(LimpiarNubosidadTrasMargen());
    }

    IEnumerator LimpiarNubosidadTrasMargen()
    {
        yield return new WaitForSeconds(SegundosMargenNubosidad);
        if (_cicloDiaNoche != null && !_cicloDiaNoche.IsRaining)
        {
            _cieloNublado = false;
            ActualizarVisibilidad();
        }
        _margenNubosidad = null;
    }

    void ActualizarVisibilidad()
    {
        if (_cicloDiaNoche == null) return;

        bool esHorarioValido = false;
        foreach (var periodo in periodosPermitidos)
        {
            if (_cicloDiaNoche.CurrentTimeOfDay == periodo) { esHorarioValido = true; break; }
        }

        bool visible = esHorarioValido && !_cicloDiaNoche.IsRaining && !_cieloNublado;

        foreach (Transform hijo in transform)
        {
            if (hijo.gameObject.activeSelf != visible)
                hijo.gameObject.SetActive(visible);
        }
    }
}
