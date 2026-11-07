using UnityEngine;

/// <summary>
/// Huesos que siguen el movimiento del personaje con un muelle (una coleta, una cola): al moverse se
/// levantan hacia atrás según la velocidad, con algo de rebote al arrancar y al parar, la punta un poco
/// por detrás y un vaivén con el paso. Así la coleta de Estela no se mete en la capa al correr.
/// Los huesos de la cadena no los anima el Animator: cada frame se parte de su pose de reposo.
/// Si el modelo no tiene esos huesos, se apaga solo. Ver INC-925.
/// </summary>
[DisallowMultipleComponent]
public class MuelleDeHuesos : MonoBehaviour
{
    [Tooltip("Huesos de la cadena, de la raíz a la punta (se buscan por nombre bajo este objeto).")]
    public string[] huesos = { "ColetaBone01", "ColetaBone02" };

    [Tooltip("Velocidad horizontal (m/s) a la que la cadena llega a su elevación máxima.")]
    public float velocidadMaxima = 5f;

    [Tooltip("Grados que se levanta hacia atrás la raíz de la cadena a la velocidad máxima.")]
    [Range(0f, 80f)] public float elevacionMaxima = 32f;

    [Tooltip("Grados que se levanta, además, cada hueso siguiente por cada grado de la raíz (la punta ondea algo más).")]
    [Range(0f, 1f)] public float elevacionDeLaPunta = 0.3f;

    [Tooltip("Rigidez del muelle: más alto, sigue antes a la velocidad.")]
    public float rigidez = 70f;

    [Tooltip("Amortiguación del muelle: más bajo, más rebote al arrancar y al parar.")]
    public float amortiguacion = 10f;

    [Tooltip("Grados de vaivén de la punta al correr (el bote del paso).")]
    [Range(0f, 20f)] public float vaiven = 5f;

    [Tooltip("Vaivenes por segundo a la velocidad máxima.")]
    public float frecuenciaDeVaiven = 2.6f;

    [Tooltip("Un salto mayor que esto (m) en un frame es un teletransporte o un cambio de personaje: no cuenta como velocidad.")]
    public float saltoMaximo = 1.5f;

    Transform[] _huesos;
    Quaternion[] _reposo;
    Vector3 _posicionAnterior;
    float _velocidad;       // horizontal, suavizada
    float _angulo;          // elevación actual de la raíz (muelle)
    float _velocidadAngular;
    float _fase;

    void Awake()
    {
        var todos = GetComponentsInChildren<Transform>(true);
        _huesos = new Transform[huesos.Length];
        _reposo = new Quaternion[huesos.Length];
        for (int i = 0; i < huesos.Length; i++)
        {
            foreach (var t in todos)
                if (t.name == huesos[i]) { _huesos[i] = t; break; }
            if (_huesos[i] == null) { enabled = false; return; }
            _reposo[i] = _huesos[i].localRotation;
        }
    }

    void OnEnable()
    {
        _posicionAnterior = transform.position;
        _velocidad = _angulo = _velocidadAngular = 0f;
    }

    void OnDisable()
    {
        if (_huesos == null) return;
        for (int i = 0; i < _huesos.Length; i++)
            if (_huesos[i] != null) _huesos[i].localRotation = _reposo[i];
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 posicion = transform.position;
        Vector3 delta = posicion - _posicionAnterior;
        _posicionAnterior = posicion;
        delta.y = 0f;
        float medida = delta.magnitude > saltoMaximo ? _velocidad : delta.magnitude / dt;
        _velocidad = Mathf.Lerp(_velocidad, Mathf.Min(medida, velocidadMaxima * 1.5f), 1f - Mathf.Exp(-12f * dt));

        float carrera = Mathf.Clamp01(_velocidad / Mathf.Max(0.01f, velocidadMaxima));
        float objetivo = carrera * elevacionMaxima;
        float aceleracion = rigidez * (objetivo - _angulo) - amortiguacion * _velocidadAngular;
        _velocidadAngular += aceleracion * dt;
        _angulo = Mathf.Clamp(_angulo + _velocidadAngular * dt, -15f, elevacionMaxima + 20f);

        _fase += dt * frecuenciaDeVaiven * carrera * Mathf.PI * 2f;
        float vaivenAhora = Mathf.Sin(_fase) * vaiven * carrera;

        // Girar alrededor de la derecha del personaje con ángulo positivo lleva lo que cuelga hacia atrás
        Vector3 eje = transform.right;
        int ultimo = _huesos.Length - 1;
        for (int i = 0; i < _huesos.Length; i++)
        {
            float a = i == 0 ? _angulo : _angulo * elevacionDeLaPunta;
            if (i == ultimo) a += vaivenAhora;
            var h = _huesos[i];
            h.localRotation = _reposo[i];
            h.rotation = Quaternion.AngleAxis(a, eje) * h.rotation;
        }
    }
}
