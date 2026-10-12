using UnityEngine;

/// De vez en cuando, en noche despejada, una estrella fugaz cruza el cielo: una estela dorada que
/// aparece, recorre un trozo de cielo en menos de un segundo y se apaga.
///
/// Va en el mismo objeto que el DayNightCycle. Usa un par de estelas (TrailRenderer) reutilizadas,
/// sin Instantiate por cada estrella. Se dibujan al borde del farClipPlane de la cámara, como el
/// domo de estrellas (NightSkyStarSpawner), para quedar siempre detrás de montañas y árboles. No
/// salen con lluvia, con el cielo nublado ni dentro de un interior. Ver INC-657.
[DisallowMultipleComponent]
public sealed class EstrellasFugaces : MonoBehaviour
{
    [Tooltip("Segundos entre una estrella fugaz y la siguiente (mínimo y máximo).")]
    public Vector2 intervalo = new Vector2(8f, 25f);

    [Tooltip("Parte de la noche (0-1) a partir de la que pueden salir.")]
    [Range(0f, 1f)] public float desde = 0.75f;

    [Tooltip("Altura sobre el horizonte, en grados, donde empiezan (mínimo y máximo).")]
    public Vector2 elevacion = new Vector2(10f, 35f);

    [Tooltip("Duración del recorrido en segundos (mínimo y máximo).")]
    public Vector2 duracion = new Vector2(0.6f, 1.1f);

    [Tooltip("Ángulo de cielo que recorre, en grados (mínimo y máximo).")]
    public Vector2 recorrido = new Vector2(10f, 22f);

    [Tooltip("Color de la estela (por encima de 1 brilla con el Bloom).")]
    [ColorUsage(false, true)] public Color color = new Color(4f, 3.4f, 2f);

    [Tooltip("Grosor de la cabeza de la estela, en grados de cielo.")]
    [Min(0.01f)] public float grosor = 0.25f;

    private const int Estelas = 2;
    private const float FraccionDelFarClip = 0.92f;

    private TrailRenderer[] _estelas, _cabezas;
    private MaterialPropertyBlock _mpb;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Vector3[] _origen, _destino;
    private float[] _inicio, _duracion;
    private Material _material;
    private Camera _camara;
    private float _siguiente;

    private void Awake()
    {
        _material = new Material(Shader.Find("Sprites/Default")) { name = "Estrella fugaz (generado)", color = color };
        _estelas = new TrailRenderer[Estelas];
        _cabezas = new TrailRenderer[Estelas];
        _mpb = new MaterialPropertyBlock();
        _camara = SolYLunaEnElCielo.CamaraActual();
        var curvaDeEstela = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.15f, 0.65f), new Keyframe(1f, 0f));
        var curvaDeCabeza = new AnimationCurve(new Keyframe(0f, 1.7f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));
        _origen = new Vector3[Estelas];
        _destino = new Vector3[Estelas];
        _inicio = new float[Estelas];
        _duracion = new float[Estelas];

        for (int i = 0; i < Estelas; i++)
        {
            var go = new GameObject($"Estrella fugaz {i + 1}");
            go.transform.SetParent(transform, false);
            var estela = go.AddComponent<TrailRenderer>();
            estela.sharedMaterial = _material;
            estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            estela.receiveShadows = false;
            estela.minVertexDistance = 0.5f;
            estela.numCapVertices = 2;
            estela.alignment = LineAlignment.View;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            estela.colorGradient = g;
            estela.widthCurve = curvaDeEstela;
            estela.emitting = false;
            estela.enabled = false;
            _estelas[i] = estela;
            var cabezaObj = new GameObject($"Cabeza de estrella fugaz {i + 1}");
            cabezaObj.transform.SetParent(transform, false);
            var cabeza = cabezaObj.AddComponent<TrailRenderer>();
            cabeza.sharedMaterial = _material;
            cabeza.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cabeza.receiveShadows = false;
            cabeza.alignment = LineAlignment.View;
            cabeza.numCapVertices = 4;
            cabeza.colorGradient = g;
            cabeza.widthCurve = curvaDeCabeza;
            cabeza.emitting = false;
            cabeza.enabled = false;
            _cabezas[i] = cabeza;
        }
        _siguiente = Time.time + Random.Range(intervalo.x, intervalo.y);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private void OnDisable()
    {
        if (_estelas == null) return;
        for (int i = 0; i < Estelas; i++) Apagar(i);
    }

    private void Update()
    {
        _camara = SolYLunaEnElCielo.CamaraActual();
        bool despejado = CieloDespejado();
        for (int i = 0; i < Estelas; i++)
        {
            if (!_estelas[i].enabled) continue;
            if (!despejado) Apagar(i);
            else Mover(i);
        }

        if (Time.time < _siguiente) return;
        _siguiente = Time.time + Random.Range(intervalo.x, intervalo.y);
        if (despejado) Lanzar();
    }

    private bool CieloDespejado()
    {
        if (DayNightCycle.NocheActual < desde) return false;
        var ciclo = DayNightCycle.Instance;
        if (ciclo != null && (ciclo.PesoDePrecipitacion >= 0.1f || ciclo.Nublado >= 0.35f)) return false;
        var entorno = EnvironmentController.Instance;
        return entorno == null || !entorno.IsEffectivelyInterior;
    }

    private void Lanzar()
    {
        if (_camara == null) return;

        int i = _estelas[0].enabled ? 1 : 0;
        if (_estelas[i].enabled) return;

        // El arco nace cerca de la dirección horizontal de la cámara para verse al explorar.
        Vector3 frente = _camara.transform.forward;
        float rumboDeCamara = frente.x * frente.x + frente.z * frente.z > 0.0001f
            ? Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg : _camara.transform.eulerAngles.y;
        float rumbo = rumboDeCamara + Random.Range(-60f, 60f);
        float alto = Random.Range(elevacion.x, elevacion.y);
        Vector3 desdeDir = Quaternion.Euler(-alto, rumbo, 0f) * Vector3.forward;
        Vector3 lado = Vector3.Cross(Vector3.up, desdeDir).normalized;
        if (Random.value < 0.5f) lado = -lado;
        Vector3 eje = Vector3.Cross(desdeDir, lado + Vector3.down * Random.Range(0.3f, 0.8f)).normalized;
        Vector3 hastaDir = Quaternion.AngleAxis(Random.Range(recorrido.x, recorrido.y), eje) * desdeDir;

        float distancia = _camara.farClipPlane * FraccionDelFarClip;
        Vector3 ojo = _camara.transform.position;
        _origen[i] = desdeDir * distancia;
        _destino[i] = hastaDir * distancia;
        _inicio[i] = Time.time;
        _duracion[i] = Random.Range(duracion.x, duracion.y);

        var estela = _estelas[i];
        float ancho = distancia * Mathf.Deg2Rad * grosor;
        estela.widthMultiplier = ancho;
        estela.time = _duracion[i] * 0.45f;
        estela.minVertexDistance = distancia * 0.002f;
        estela.transform.position = ojo + _origen[i];
        estela.Clear();
        estela.enabled = true;
        estela.emitting = true;
        TrailRenderer cabeza = _cabezas[i];
        cabeza.widthMultiplier = ancho;
        cabeza.time = 0.06f;
        cabeza.minVertexDistance = distancia * 0.0005f;
        cabeza.transform.position = estela.transform.position;
        cabeza.Clear();
        cabeza.enabled = true;
        cabeza.emitting = true;
    }

    private void Apagar(int i)
    {
        TrailRenderer estela = _estelas[i];
        TrailRenderer cabeza = _cabezas[i];
        if (estela != null) { estela.emitting = false; estela.Clear(); estela.enabled = false; }
        if (cabeza != null) { cabeza.emitting = false; cabeza.Clear(); cabeza.enabled = false; }
    }

    /// La estela sigue a la cámara (está «en el cielo», no en el mundo) y deja de emitir al llegar.
    private void Mover(int i)
    {
        var estela = _estelas[i];
        float t = (Time.time - _inicio[i]) / _duracion[i];
        if (_camara == null) { Apagar(i); return; }

        if (t <= 1f)
        {
            Vector3 dir = Vector3.Slerp(_origen[i], _destino[i], t);
            estela.transform.position = _camara.transform.position + dir;
            TrailRenderer cabeza = _cabezas[i];
            cabeza.transform.position = estela.transform.position;
            float destello = 1.15f + 0.45f * Mathf.Sin(t * Mathf.PI * 12f);
            _mpb.SetColor(ColorId, color * destello);
            cabeza.SetPropertyBlock(_mpb);
        }
        else if (estela.emitting)
        {
            estela.emitting = false;
            _cabezas[i].emitting = false;
        }
        else if (t > 1f + estela.time / _duracion[i] + 0.1f)
        {
            Apagar(i);
        }
    }
}
