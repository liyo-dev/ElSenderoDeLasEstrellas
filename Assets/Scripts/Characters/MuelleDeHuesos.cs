using UnityEngine;

/// <summary>
/// Huesos que siguen el movimiento del personaje con un muelle (una coleta, una cola): al moverse se
/// levantan hacia atrás según la velocidad, con algo de rebote al arrancar y al parar, la punta un poco
/// por detrás y un vaivén con el paso. Así la coleta de Estela no se mete en la capa al correr.
/// Los huesos de la cadena no los anima el Animator: cada frame se parte de su pose de reposo.
/// Con «sobreLaAnimacion» (la capa, que el Animator sí coloca) el giro se suma a la pose del frame.
/// Un personaje puede llevar varios, uno por cadena (la coleta y la capa).
/// Si el modelo no tiene esos huesos, se apaga solo. Ver INC-925.
/// </summary>
public class MuelleDeHuesos : MonoBehaviour
{
    private static readonly string[] HuesosDeLaCapa = { "CloakBone01", "CloakBone02", "CloakBone03" };
    private const string MallaDeCapa = "Cloak";
    private const float SegundosEntreMiradasALaMalla = 1f;

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

    [Tooltip("El Animator coloca estos huesos cada frame (la capa): el giro se suma a su pose en vez de partir de la de reposo.")]
    public bool sobreLaAnimacion;

    [Tooltip("Solo trabaja mientras el personaje lleve puesta una malla cuyo nombre empiece así (la capa: «Cloak»). Vacío = siempre.")]
    public string mallaQueLoLleva = "";

    Transform[] _huesos;
    Quaternion[] _reposo;
    Quaternion[] _poseDelFrame;   // con sobreLaAnimacion: la pose sobre la que se giró el último frame
    Quaternion[] _dejada;         // y cómo quedó el hueso, para saber si el Animator lo ha vuelto a colocar
    Renderer[] _mallas;
    bool _llevaLaMalla = true;
    float _proximaMirada;
    Vector3 _posicionAnterior;
    float _velocidad;       // horizontal, suavizada
    float _angulo;          // elevación actual de la raíz (muelle)
    float _velocidadAngular;
    float _fase;

    // En Start y no en Awake: quien lo añade por código le da la cadena después de añadirlo.
    void Start()
    {
        var todos = GetComponentsInChildren<Transform>(true);
        _huesos = new Transform[huesos.Length];
        _reposo = new Quaternion[huesos.Length];
        _poseDelFrame = new Quaternion[huesos.Length];
        _dejada = new Quaternion[huesos.Length];
        for (int i = 0; i < huesos.Length; i++)
        {
            foreach (var t in todos)
                if (t.name == huesos[i]) { _huesos[i] = t; break; }
            if (_huesos[i] == null) { enabled = false; return; }
            _reposo[i] = _poseDelFrame[i] = _dejada[i] = _huesos[i].localRotation;
        }
        if (!string.IsNullOrEmpty(mallaQueLoLleva))
        {
            var todas = GetComponentsInChildren<Renderer>(true);
            int n = 0;
            foreach (var r in todas) if (r.name.StartsWith(mallaQueLoLleva)) n++;
            _mallas = new Renderer[n];
            n = 0;
            foreach (var r in todas) if (r.name.StartsWith(mallaQueLoLleva)) _mallas[n++] = r;
        }
    }

    /// <summary>
    /// Pone el muelle de la capa en un personaje que tenga sus huesos (los del pack: CloakBone01–03).
    /// Solo trabaja mientras lleve una capa puesta y a la vista.
    /// </summary>
    public static void AsegurarCapa(GameObject personaje)
    {
        if (personaje == null) return;
        foreach (var m in personaje.GetComponents<MuelleDeHuesos>())
            if (m.huesos.Length > 0 && m.huesos[0] == HuesosDeLaCapa[0]) return;
        bool tieneHuesos = false;
        foreach (var t in personaje.GetComponentsInChildren<Transform>(true))
            if (t.name == HuesosDeLaCapa[0]) { tieneHuesos = true; break; }
        if (!tieneHuesos) return;

        var capa = personaje.AddComponent<MuelleDeHuesos>();
        capa.huesos = HuesosDeLaCapa;
        capa.sobreLaAnimacion = true;
        capa.mallaQueLoLleva = MallaDeCapa;
        capa.elevacionMaxima = 14f;
        capa.elevacionDeLaPunta = 0.5f;
        capa.rigidez = 45f;
        capa.amortiguacion = 7f;
        capa.vaiven = 4f;
        capa.frecuenciaDeVaiven = 2.2f;
    }

    /// ¿Lleva puesta y a la vista la malla que mueve esta cadena? Se mira una vez por segundo.
    bool LlevaLaMalla()
    {
        if (_mallas == null) return true;
        if (Time.unscaledTime < _proximaMirada) return _llevaLaMalla;
        _proximaMirada = Time.unscaledTime + SegundosEntreMiradasALaMalla;
        _llevaLaMalla = false;
        for (int i = 0; i < _mallas.Length; i++)
            if (_mallas[i] != null && _mallas[i].enabled && _mallas[i].gameObject.activeInHierarchy && _mallas[i].isVisible)
            { _llevaLaMalla = true; break; }
        return _llevaLaMalla;
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
            if (_huesos[i] != null) _huesos[i].localRotation = sobreLaAnimacion ? _poseDelFrame[i] : _reposo[i];
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || _huesos == null) return;
        if (!LlevaLaMalla())
        {
            _posicionAnterior = transform.position;
            _velocidad = _angulo = _velocidadAngular = 0f;
            return;
        }

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
            if (!sobreLaAnimacion) h.localRotation = _reposo[i];
            // Si el Animator no ha vuelto a colocar el hueso este frame (personaje fuera de cámara, Animator
            // parado), sigue como se dejó: se parte de la pose de antes para no sumar giro sobre giro.
            else if (h.localRotation == _dejada[i]) h.localRotation = _poseDelFrame[i];
            else _poseDelFrame[i] = h.localRotation;
            h.rotation = Quaternion.AngleAxis(a, eje) * h.rotation;
            _dejada[i] = h.localRotation;
        }
    }
}
