using UnityEngine;

/// <summary>
/// Rayo generado entero por código (sin assets de terceros): un trazo en zigzag desde arriba hasta
/// el punto de impacto, ramas secundarias más finas, halo translúcido opcional, parpadeo opcional y
/// un destello de luz puntual que decae. Lo usan los rayos de la secuencia del prólogo
/// (VFX_RayoColision_Claro / _Oscuro, cada uno con su color) y los rayos de tormenta de
/// DayNightCycle.
///
/// Está hecho para VfxPoolService: el pool crea las instancias por adelantado y luego solo las
/// activa y desactiva, así que el rayo nunca se destruye a sí mismo. Los LineRenderer y la luz se
/// construyen una vez en Awake y la forma se regenera en OnEnable (cada vez que el pool reactiva la
/// instancia). Cuándo desaparece lo decide quien llama a VfxPoolService.Play(..., lifetime).
///
/// Dos formas de lanzarlo:
///   - Tal cual: VfxPoolService.Play(prefab, impacto, rot) → rayo vertical de boltHeight sobre el
///     punto, con el aspecto del prefab.
///   - Entre dos puntos: tras Play, <see cref="Lanzar"/>(desde, hasta, ancho, color) recoloca el
///     rayo, escala su forma al largo pedido y le da ancho y color propios.
/// Sin prefab, <see cref="CrearPlantilla"/> construye en código una plantilla inactiva que se pasa a
/// VfxPoolService.Play igual que un prefab.
/// </summary>
[DisallowMultipleComponent]
public class ProceduralLightningBoltVfx : MonoBehaviour
{
    [Header("Forma")]
    [Tooltip("Altura (m) del rayo cuando se lanza tal cual. Con Lanzar, la forma entera se escala al largo pedido tomando esta altura como referencia.")]
    [SerializeField] private float boltHeight = 2.2f;
    [SerializeField] private int segments = 9;
    [Tooltip("Desvío lateral máximo de cada quiebro del zigzag (m), para un rayo de boltHeight de alto.")]
    [SerializeField] private float jitterAmount = 0.22f;
    [SerializeField] private int branchCount = 2;
    [Tooltip("Cuánto se abren las ramas hacia un lado según bajan, en fracción de su largo. 0 = bajan rectas.")]
    [SerializeField, Range(0f, 1f)] private float aperturaDeRamas = 0f;

    [Header("Aspecto")]
    [SerializeField] private Color boltColor = new Color(1f, 0.95f, 0.8f, 1f);
    [SerializeField] private float boltWidth = 0.06f;
    [Tooltip("Opcional — si se deja vacío, usa un material additivo compartido generado en runtime, sin depender de ningún asset del proyecto.")]
    [SerializeField] private Material boltMaterial;
    [Tooltip("Ancho del trazo en el punto de impacto, en fracción del ancho de arriba (1 = igual de ancho todo el rayo).")]
    [SerializeField, Range(0.05f, 1f)] private float anchoEnElImpacto = 1f;
    [Tooltip("Opacidad del trazo en el punto de impacto, en fracción de la de arriba (0 = se desvanece al llegar).")]
    [SerializeField, Range(0f, 1f)] private float opacidadEnElImpacto = 0f;
    [Tooltip("Ancho del halo translúcido de cada trazo, en veces el ancho del trazo. 0 = sin halo.")]
    [SerializeField, Min(0f)] private float anchoDelHalo = 0f;
    [Tooltip("Color (con su transparencia) del halo.")]
    [SerializeField] private Color colorDelHalo = new Color(0.55f, 0.65f, 1f, 0.22f);

    [Header("Parpadeo")]
    [Tooltip("Tramos (segundos de tiempo real) en los que el rayo se alterna encendido y apagado, empezando encendido; tras el último queda apagado. Vacío = visible todo el rato.")]
    [SerializeField] private float[] parpadeo = new float[0];

    [Header("Luz")]
    [Tooltip("Intensidad del destello de luz puntual en el impacto. 0 = sin luz.")]
    [SerializeField] private float flashLightIntensity = 6f;
    [SerializeField] private float flashLightRange = 5f;
    [Tooltip("Velocidad (por segundo) a la que decae la intensidad de la luz tras cada reactivación — no controla cuánto dura el VFX en pantalla, eso lo decide quien llama a VfxPoolService.Play(..., lifetime).")]
    [SerializeField] private float flashDecaySpeed = 12f;

    /// Aspecto de una plantilla creada en código (<see cref="CrearPlantilla"/>). El largo, el ancho y
    /// el color se dan en cada <see cref="Lanzar"/>; el desvío va en fracción del largo.
    public struct Ajustes
    {
        public Material material;
        public int segmentos;
        public float desvioRelativo;
        public int ramas;
        public float aperturaDeRamas;
        public float anchoEnElImpacto;
        public float opacidadEnElImpacto;
        public float anchoDelHalo;
        public Color colorDelHalo;
        public float[] parpadeo;
        public float intensidadDeLuz;
    }

    private struct Trazo
    {
        public LineRenderer nucleo;
        public LineRenderer halo;
    }

    private Light _flashLight;
    private Trazo _principal;
    private Trazo[] _ramas;
    private float _tiempoEncendido;
    private bool _visible = true;
    private static Material _sharedRuntimeMat;

    /// Crea una plantilla inactiva (fuera de escena, sobrevive a cambios de escena) con el aspecto
    /// indicado, para usarla como prefab en VfxPoolService.Play. Al estar inactiva no construye
    /// nada: cada copia del pool lo hace en su propio Awake.
    public static GameObject CrearPlantilla(string nombre, in Ajustes ajustes)
    {
        var go = new GameObject(nombre);
        go.SetActive(false);
        DontDestroyOnLoad(go);

        var vfx = go.AddComponent<ProceduralLightningBoltVfx>();
        vfx.boltHeight = 1f;
        vfx.jitterAmount = ajustes.desvioRelativo;
        vfx.segments = Mathf.Max(2, ajustes.segmentos);
        vfx.branchCount = Mathf.Max(0, ajustes.ramas);
        vfx.aperturaDeRamas = Mathf.Clamp01(ajustes.aperturaDeRamas);
        vfx.boltMaterial = ajustes.material;
        vfx.anchoEnElImpacto = Mathf.Clamp(ajustes.anchoEnElImpacto, 0.05f, 1f);
        vfx.opacidadEnElImpacto = Mathf.Clamp01(ajustes.opacidadEnElImpacto);
        vfx.anchoDelHalo = Mathf.Max(0f, ajustes.anchoDelHalo);
        vfx.colorDelHalo = ajustes.colorDelHalo;
        vfx.parpadeo = ajustes.parpadeo ?? new float[0];
        vfx.flashLightIntensity = Mathf.Max(0f, ajustes.intensidadDeLuz);
        return go;
    }

    void Awake()
    {
        _principal = CrearTrazo(transform, "Bolt");

        _ramas = new Trazo[Mathf.Max(0, branchCount)];
        for (int b = 0; b < _ramas.Length; b++)
            _ramas[b] = CrearTrazo(transform, "BranchBolt");

        if (flashLightIntensity > 0f)
        {
            _flashLight = gameObject.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = flashLightRange;
            _flashLight.shadows = LightShadows.None;
        }
    }

    // Se dispara cada vez que el pool reactiva esta instancia (SetActive(true)) — aquí es donde
    // hay que relanzar el efecto, no en Awake (eso solo pasa una vez, al crear la instancia).
    void OnEnable() => Generar(boltHeight, boltWidth, boltColor);

    /// Coloca el rayo entre dos puntos (cae de `desde` a `hasta`), escala su forma a ese largo y lo
    /// regenera con el ancho y el color dados. Se llama justo después de VfxPoolService.Play.
    public void Lanzar(Vector3 desde, Vector3 hasta, float ancho, Color color)
    {
        Vector3 eje = desde - hasta;
        float largo = eje.magnitude;
        if (largo < 0.01f) return;

        transform.SetPositionAndRotation(hasta, Quaternion.FromToRotation(Vector3.up, eje / largo));
        Generar(largo, ancho, color);
    }

    void Update()
    {
        if (_flashLight != null && _flashLight.intensity > 0f)
            _flashLight.intensity = Mathf.Lerp(_flashLight.intensity, 0f, Time.deltaTime * flashDecaySpeed);

        if (parpadeo == null || parpadeo.Length == 0) return;
        _tiempoEncendido += Time.unscaledDeltaTime;
        PonerVisible(VisibleEnElParpadeo(_tiempoEncendido));
    }

    private bool VisibleEnElParpadeo(float t)
    {
        bool encendido = true;
        for (int i = 0; i < parpadeo.Length; i++)
        {
            t -= parpadeo[i];
            if (t < 0f) return encendido;
            encendido = !encendido;
        }
        return false;
    }

    private void Generar(float altura, float ancho, Color color)
    {
        if (_principal.nucleo == null) return;

        float escala = altura / Mathf.Max(0.01f, boltHeight);
        float desvio = jitterAmount * escala;
        int tramos = Mathf.Max(2, segments);

        // Trazo principal: de arriba (altura) al impacto (origen local).
        PrepararTrazo(_principal, tramos + 1, ancho, color);
        for (int i = 0; i <= tramos; i++)
        {
            float t = (float)i / tramos;
            Vector3 p = Vector3.up * (altura * (1f - t));
            if (i != 0 && i != tramos)
                p += new Vector3(Random.Range(-desvio, desvio), 0f, Random.Range(-desvio, desvio));
            PonerPunto(_principal, i, p);
        }

        // Ramas: salen de un quiebro del trazo principal y bajan abriéndose hacia un lado.
        for (int b = 0; b < _ramas.Length; b++)
        {
            int desde = Mathf.Clamp(Mathf.RoundToInt(Random.Range(0.25f, 0.7f) * tramos), 1, tramos - 1);
            Vector3 inicio = _principal.nucleo.GetPosition(desde);
            float largo = Mathf.Max(0.3f * escala, inicio.y * 0.5f);
            Vector2 lado2D = Random.insideUnitCircle.normalized;
            Vector3 lado = new Vector3(lado2D.x, 0f, lado2D.y);

            int tramosDeRama = Mathf.Max(2, tramos / 2);
            PrepararTrazo(_ramas[b], tramosDeRama + 1, ancho * 0.55f, color);
            for (int i = 0; i <= tramosDeRama; i++)
            {
                float t = (float)i / tramosDeRama;
                Vector3 p = inicio + Vector3.down * (largo * t) + lado * (largo * aperturaDeRamas * t);
                if (i != 0)
                    p += new Vector3(Random.Range(-desvio, desvio), 0f, Random.Range(-desvio, desvio)) * 0.6f;
                PonerPunto(_ramas[b], i, p);
            }
        }

        if (_flashLight != null)
        {
            _flashLight.color = color;
            _flashLight.intensity = flashLightIntensity;
        }

        _tiempoEncendido = 0f;
        _visible = false;
        PonerVisible(true);
    }

    private void PrepararTrazo(Trazo trazo, int puntos, float ancho, Color color)
    {
        trazo.nucleo.positionCount = puntos;
        trazo.nucleo.widthMultiplier = ancho;
        trazo.nucleo.startColor = color;
        trazo.nucleo.endColor = new Color(color.r, color.g, color.b, color.a * opacidadEnElImpacto);

        if (trazo.halo == null) return;
        trazo.halo.positionCount = puntos;
        trazo.halo.widthMultiplier = ancho * anchoDelHalo;
        trazo.halo.startColor = colorDelHalo;
        trazo.halo.endColor = new Color(colorDelHalo.r, colorDelHalo.g, colorDelHalo.b, colorDelHalo.a * opacidadEnElImpacto);
    }

    private static void PonerPunto(Trazo trazo, int indice, Vector3 punto)
    {
        trazo.nucleo.SetPosition(indice, punto);
        if (trazo.halo != null) trazo.halo.SetPosition(indice, punto);
    }

    private void PonerVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        Mostrar(_principal, visible);
        for (int b = 0; b < _ramas.Length; b++) Mostrar(_ramas[b], visible);
    }

    private static void Mostrar(Trazo trazo, bool visible)
    {
        if (trazo.nucleo != null) trazo.nucleo.enabled = visible;
        if (trazo.halo != null) trazo.halo.enabled = visible;
    }

    private Trazo CrearTrazo(Transform raiz, string nombre)
    {
        var trazo = new Trazo { nucleo = CrearLinea(raiz, nombre, 0) };
        if (anchoDelHalo > 0f) trazo.halo = CrearLinea(raiz, nombre + "Halo", -1);
        return trazo;
    }

    private LineRenderer CrearLinea(Transform raiz, string nombre, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(raiz, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = boltMaterial != null ? boltMaterial : GetRuntimeMaterial();
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = orden;
        if (anchoEnElImpacto < 1f)
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, anchoEnElImpacto));
        return lr;
    }

    private static Material GetRuntimeMaterial()
    {
        if (_sharedRuntimeMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            _sharedRuntimeMat = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (_sharedRuntimeMat.HasProperty("_Surface")) _sharedRuntimeMat.SetFloat("_Surface", 1f); // Transparent (URP Unlit)
            if (_sharedRuntimeMat.HasProperty("_Blend")) _sharedRuntimeMat.SetFloat("_Blend", 1f); // Additive (URP Unlit)
        }
        return _sharedRuntimeMat;
    }
}
