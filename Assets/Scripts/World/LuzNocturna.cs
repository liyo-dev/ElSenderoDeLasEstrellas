using UnityEngine;

/// Luz de farol, antorcha o casa que se enciende al anochecer y se apaga al amanecer.
///
/// Cada luz tiene su propio umbral de noche (sorteado si `umbral` es negativo), así que el pueblo
/// se va encendiendo farol a farol durante la transición del ciclo en vez de todo de golpe. De día
/// el componente Light queda apagado (no cuesta nada en Forward+). Si cuelga de un
/// ExteriorWorldRoot oculto (jugador en un interior) también se apaga. Ver INC-657.
[DisallowMultipleComponent, RequireComponent(typeof(Light))]
public sealed class LuzNocturna : MonoBehaviour
{
    [Tooltip("Intensidad de la luz a plena noche.")]
    [Min(0f)] public float intensidad = 2.5f;

    [Tooltip("Parte de la noche (0-1) a partir de la que se enciende. Negativo: se sortea entre 0,15 y 0,85 para que las luces no se enciendan todas a la vez.")]
    [Range(-1f, 1f)] public float umbral = -1f;

    [Tooltip("Segundos que tarda en encenderse o apagarse.")]
    [Min(0.05f)] public float fundido = 1.5f;

    [Tooltip("Cuánto oscila la intensidad como una llama (0 = luz fija).")]
    [Range(0f, 0.5f)] public float parpadeo = 0.1f;

    [Tooltip("Rapidez de la oscilación de la llama.")]
    [Min(0f)] public float velocidadDeParpadeo = 6f;

    private Light _luz;
    private ExteriorWorldRoot _exterior;
    private float _umbral;
    private float _encendido;
    private float _semilla;

    private void Awake()
    {
        _luz = GetComponent<Light>();
        _exterior = GetComponentInParent<ExteriorWorldRoot>(true);
        _umbral = umbral >= 0f ? umbral : Random.Range(0.15f, 0.85f);
        _semilla = Random.value * 100f;
        _encendido = DayNightCycle.NocheActual >= _umbral ? 1f : 0f;
        _luz.enabled = false;
    }

    private void Update()
    {
        float objetivo = DayNightCycle.NocheActual >= _umbral ? 1f : 0f;
        if (_encendido != objetivo)
            _encendido = Mathf.MoveTowards(_encendido, objetivo, Time.deltaTime / fundido);

        bool visible = _encendido > 0f && (_exterior == null || !_exterior.IsHidden);
        if (_luz.enabled != visible) _luz.enabled = visible;
        if (!visible) return;

        float llama = 1f;
        if (parpadeo > 0f)
            llama = 1f - parpadeo + 2f * parpadeo * Mathf.PerlinNoise(_semilla, Time.time * velocidadDeParpadeo);
        _luz.intensity = intensidad * _encendido * llama;
    }

    private void OnDisable()
    {
        if (_luz != null) _luz.enabled = false;
    }
}
