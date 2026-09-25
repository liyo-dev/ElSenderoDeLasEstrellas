using UnityEngine;

/// El sol y la luna, visibles y en movimiento (INC-371).
///
/// «Me gustaría añadir un sol y una luna. Igual que tenemos el cielo estrellado, añadir esto al
/// sistema y que se vayan moviendo como si fueran de verdad, generando amaneceres y atardeceres
/// bonitos.»
///
/// ── Cómo se mueven ────────────────────────────────────────────────────────────────────────────
/// No llevan reloj propio, y esa es la gracia: van colgados de la LUZ DIRECCIONAL del ciclo
/// día/noche (`DayNightCycle.Sun`). El ciclo ya gira esa luz —cada periodo tiene su
/// `sunRotationX/Y` y las transiciones las interpolan durante dieciséis segundos—, así que el sol
/// aparece por donde entra la luz y se pone por donde se va. El amanecer no hay que programarlo:
/// es la misma rotación que ya pinta el cielo, ahora con un disco delante.
///
/// El sol va SIEMPRE en el lado contrario a donde apunta la luz (una direccional ilumina hacia su
/// `forward`, así que el astro está en `-forward`) y la luna, en el opuesto del sol: cuando uno se
/// pone, el otro sale, como pasa de verdad.
///
/// ── Cómo se ven ───────────────────────────────────────────────────────────────────────────────
/// Dos quads que miran siempre a la cámara, colgados de ella a una distancia fija (por defecto,
/// justo dentro del plano lejano de la cámara), así que no se pueden alcanzar ni tapar: están
/// pegados al cielo. Las texturas se GENERAN aquí, sin assets nuevos: un disco con halo suave.
///
/// El color del sol depende de su altura: blanco cálido arriba, naranja fuerte pegado al
/// horizonte. Eso es lo que hace bonitos el amanecer y el atardecer, y sale gratis porque la
/// altura ya la da la rotación de la luz.
///
/// Los dos se DESVANECEN bajo el horizonte, así que de día solo se ve el sol y de noche solo la
/// luna, con los dos a la vez un rato en los cambios — que es justo el momento bonito.
/// `ExecuteAlways` a propósito: así el sol y la luna se ven TAMBIÉN en la Scene View, sin darle a
/// Play. Los dos objetos se crean en tiempo de ejecución con `DontSave`, así que nunca se guardan
/// en la escena ni ensucian el .unity: si no los ves en la jerarquía, es que el componente no está
/// puesto o no hay ningún DayNightCycle cargado.
[ExecuteAlways]
[DisallowMultipleComponent]
// DESPUÉS de SolForzado (INC-419): el disco se coloca con la rotación que tiene la luz al final del
// fotograma. Sin orden fijo, este LateUpdate corría antes que el de SolForzado y pintaba el sol
// donde lo dejaba el ciclo (en el atardecer, casi en lo alto: fuera de cuadro), no donde lo ponía
// el forzado — y por eso el sol del río no se vio en ninguna de las cuatro grabaciones.
[DefaultExecutionOrder(1000)]
public class SolYLunaEnElCielo : MonoBehaviour
{
    [Header("Tamaño y distancia")]
    [Tooltip("Distancia a la que se cuelgan del ojo. 0 = automático: justo dentro del plano " +
             "lejano de la cámara, que es lo más lejos que se puede sin que los recorte.")]
    [SerializeField] private float distancia = 0f;

    [Tooltip("Diámetro del sol, en grados de cielo. El sol de verdad mide medio grado; aquí " +
             "conviene exagerar, como en cualquier juego con este estilo.")]
    [Range(1f, 30f)] [SerializeField] private float tamanoDelSol = 9f;

    [Range(1f, 30f)] [SerializeField] private float tamanoDeLaLuna = 7f;

    [Header("Colores")]
    [Tooltip("Color del sol cuando está alto.")]
    [SerializeField] private Color solAlto = new Color(1f, 0.97f, 0.85f);

    [Tooltip("Color del sol pegado al horizonte: es el del amanecer y el atardecer.")]
    [SerializeField] private Color solEnElHorizonte = new Color(1f, 0.55f, 0.25f);

    [SerializeField] private Color colorDeLaLuna = new Color(0.93f, 0.95f, 1f);

    [Header("Desvanecido")]
    [Tooltip("Grados por debajo del horizonte a los que el astro ya no se ve del todo.")]
    [SerializeField] private float margenBajoElHorizonte = 8f;

    [Tooltip("Opacidad máxima de la luna. Baja, para que no compita con las estrellas.")]
    [Range(0f, 1f)] [SerializeField] private float opacidadDeLaLuna = 0.9f;

    private Transform _sol, _luna;
    private Material _matSol, _matLuna;
    private Camera _camara;
    private bool _contado;

    private void OnEnable()
    {
        if (_sol == null) Crear();
    }

    private void OnDisable()
    {
        // Se rehacen al volver a encender: son de usar y tirar (DontSave).
        if (_sol != null) Destruir(_sol.gameObject);
        if (_luna != null) Destruir(_luna.gameObject);
        _sol = _luna = null;
    }

    private void OnDestroy()
    {
        Destruir(_matSol);
        Destruir(_matLuna);
    }

    /// Con `ExecuteAlways` esto corre también fuera de Play, y ahí `Destroy` no vale: Unity avisa
    /// («Destroy may not be called from edit mode») y no borra nada. Un solo sitio que elige.
    private static void Destruir(Object cosa)
    {
        if (cosa == null) return;
        if (Application.isPlaying) Destroy(cosa);
        else DestroyImmediate(cosa);
    }

    private void Crear()
    {
        _matSol = NuevoMaterial(Disco(256, 0.34f, 0.72f));
        _matLuna = NuevoMaterial(Disco(256, 0.40f, 0.60f));

        _sol = NuevoAstro("Sol", _matSol);
        _luna = NuevoAstro("Luna", _matLuna);
    }

    private Transform NuevoAstro(string nombre, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = nombre;
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.DontSave;

        // Sin collider: nadie tiene que poder chocarse con el sol, ni el ratón seleccionarlo.
        var col = go.GetComponent<Collider>();
        if (col != null) Destruir(col);

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        return go.transform;
    }

    /// Material transparente y sin luz, en URP si lo hay y en el de toda la vida si no.
    private static Material NuevoMaterial(Texture2D textura)
    {
        // «Sprites/Default» primero, y no es capricho (INC-379): los shaders Unlit de URP APLICAN
        // NIEBLA, y estos dos discos van colgados lejísimos. Con la niebla del prólogo
        // (densidad 0,016 de noche) un quad a 250 m se borra entero — no es que el sol no
        // estuviera, es que estaba detrás de kilómetro y medio de niebla. Sprites/Default no lee
        // niebla ni luz: pinta la textura tal cual, que es justo lo que tiene que hacer un astro.
        Shader shader = Shader.Find("Sprites/Default")
                        ?? Shader.Find("Unlit/Transparent")
                        ?? Shader.Find("Universal Render Pipeline/Unlit");

        var m = new Material(shader) { hideFlags = HideFlags.DontSave };

        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", textura);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", textura);

        // Transparente aditivo suave: un astro no oscurece lo que tiene detrás, se suma al cielo.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);          // 1 = Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);              // 0 = Alpha
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return m;
    }

    /// Un disco con el borde difuminado, hecho aquí mismo: ni textura que importar ni prefab que
    /// mantener. `nucleo` es hasta dónde llega el blanco puro y `borde` dónde se apaga del todo,
    /// los dos en fracción del radio.
    private static Texture2D Disco(int lado, float nucleo, float borde)
    {
        var t = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        float centro = (lado - 1) * 0.5f;
        var pixeles = new Color[lado * lado];

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float dx = (x - centro) / centro;
                float dy = (y - centro) / centro;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a = 1f - Mathf.InverseLerp(nucleo, borde, d);
                a = Mathf.Clamp01(a);
                a *= a;   // el halo cae más deprisa: disco nítido y aura suave alrededor

                pixeles[y * lado + x] = new Color(1f, 1f, 1f, a);
            }
        }

        t.SetPixels(pixeles);
        t.Apply(false, false);
        return t;
    }

    private void LateUpdate()
    {
        if (_sol == null) return;

        Transform luz = DayNightCycle.Sun;
        if (luz == null)
        {
            // Fuera de Play, DayNightCycle.Sun no está puesta (se rellena en Awake), así que se
            // busca la luz direccional de la escena para poder verlos mientras se compone.
            luz = LuzDeLaEscena();
        }
        if (luz == null) { Mostrar(false); return; }

#if UNITY_EDITOR
        // En la Scene View manda la cámara de la vista: así se pueden colocar y ver sin darle a Play.
        if (!Application.isPlaying)
        {
            var vista = UnityEditor.SceneView.lastActiveSceneView;
            if (vista != null && vista.camera != null) _camara = vista.camera;
        }
#endif
        if (_camara == null || !_camara.isActiveAndEnabled) _camara = Camara();
        if (_camara == null) { Mostrar(false); return; }

        Mostrar(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_contado && Application.isPlaying)
        {
            _contado = true;
            Debug.Log($"[SolYLuna] Pintando con la cámara '{_camara.name}' (lejos={_camara.farClipPlane:F0}) " +
                      $"y la luz '{luz.name}'. Si esto no aparece, el componente no está en ninguna escena cargada.");
        }
#endif

        // Una luz direccional ILUMINA hacia su forward, así que el astro está en el lado contrario.
        Vector3 haciaLaLuz = -luz.forward;

        // Con un sol en cuadro, el disco (y la luna, en su contrario) van donde está el sol, no
        // donde va la luz mientras gira hasta allí: si no, se le ve cruzar el cielo corriendo
        // (prologo20, INC-431).
        if (Application.isPlaying && SolForzado.DireccionDelDisco.HasValue)
            haciaLaLuz = SolForzado.DireccionDelDisco.Value.normalized;

        // Hasta el 90 % del plano lejano (antes, tope de 300 m). Con el tope, las montañas de más
        // allá quedaban DETRÁS del disco y no lo podían tapar: el sol no se ponía detrás de nada
        // (prologo18, INC-413). El tamaño va en grados, así que la distancia no lo cambia.
        float lejos = distancia > 0.1f ? distancia : Mathf.Max(_camara.farClipPlane * 0.9f, 50f);
        LejosActual = lejos;
        Vector3 ojo = _camara.transform.position;

        // Altura sobre el horizonte, de -1 (debajo del todo) a 1 (en lo más alto).
        float seno = Mathf.Sin(margenBajoElHorizonte * Mathf.Deg2Rad);
        float SobreElHorizonte(Vector3 d) => Mathf.Clamp01(Mathf.InverseLerp(-seno, seno, d.y));

        // ── De día manda el sol; de noche, la luna (INC-410) ──────────────────────────────────
        // De noche el ciclo deja la luz direccional ARRIBA (a 30° en el periodo Night): es la luz
        // de la luna, no la del sol. Antes el sol se pintaba siempre donde entra la luz, así que de
        // noche había un sol a media altura. Ahora el peso de la noche reparte: el sol se apaga, y
        // la luna pasa a estar donde entra la luz.
        // Y con tormenta no se ve ninguno de los dos: las nubes los tapan. Sin esto, al soltar el
        // sol del atardecer del prólogo se le veía subir por el cielo en plena tormenta, «como si
        // amaneciera» (grabación prologo17).
        var ciclo = DayNightCycle.Instance;
        float noche = ciclo != null ? Mathf.Clamp01(ciclo.PesoDeNoche) : 0f;
        float despejado = ciclo != null ? 1f - Mathf.Clamp01(ciclo.Nublado) : 1f;

        Vector3 haciaElSol = haciaLaLuz;
        float aSol = SobreElHorizonte(haciaElSol) * (1f - noche) * despejado * SolForzado.OpacidadDelSol;

        // La luna: de día, en el lado opuesto al sol (sale cuando él se pone); de noche, donde
        // entra la luz. Un solo disco, así que va donde más se vea.
        float aLunaDeDia = SobreElHorizonte(-haciaLaLuz) * (1f - noche);
        float aLunaDeNoche = SobreElHorizonte(haciaLaLuz) * noche;
        Vector3 haciaLaLuna = aLunaDeNoche >= aLunaDeDia ? haciaLaLuz : -haciaLaLuz;
        float aLuna = Mathf.Max(aLunaDeDia, aLunaDeNoche) * opacidadDeLaLuna * despejado;

        Colocar(_sol, ojo, haciaElSol, lejos, tamanoDelSol);
        Colocar(_luna, ojo, haciaLaLuna, lejos, tamanoDeLaLuna);

        float alturaSol = haciaElSol.y;

        // Cuanto más bajo, más naranja: el amanecer y el atardecer se pintan solos.
        Color color = Color.Lerp(solEnElHorizonte, solAlto, Mathf.Clamp01(alturaSol * 2.2f));
        color.a = aSol;
        Pintar(_matSol, color);

        Color luna = colorDeLaLuna;
        luna.a = aLuna;
        Pintar(_matLuna, luna);
    }

    /// A qué distancia se están pintando el sol y la luna ahora mismo. SolForzado la usa para saber
    /// qué montañas quedan DELANTE del disco (las que lo pueden tapar).
    public static float LejosActual { get; private set; } = 300f;

    /// La cámara con la que se rueda ahora mismo (la misma que usa este componente).
    public static Camera CamaraActual() => Camara();

    /// La luz direccional de las escenas cargadas. Solo se usa fuera de Play, para la vista previa.
    private Transform LuzDeLaEscena()
    {
        if (!Application.isPlaying)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && l.isActiveAndEnabled) return l.transform;
        }
        return null;
    }

    /// La cámara con la que se está rodando AHORA. `Camera.main` solo devuelve cámaras activas
    /// con la etiqueta MainCamera, y durante una cinemática la que rueda puede ser otra (o la de
    /// siempre con la etiqueta quitada). Sin esto, el sol y la luna simplemente no se dibujaban.
    private static Camera Camara()
    {
        var c = Camera.main;
        if (c != null && c.isActiveAndEnabled) return c;

        Camera mejor = null;
        foreach (var cam in Camera.allCameras)
        {
            if (cam == null || !cam.isActiveAndEnabled) continue;
            if (cam.cameraType != CameraType.Game) continue;
            if (cam.orthographic) continue;                 // minimapa
            if (mejor == null || cam.depth > mejor.depth) mejor = cam;
        }
        return mejor;
    }

    private void Mostrar(bool si)
    {
        if (_sol != null && _sol.gameObject.activeSelf != si) _sol.gameObject.SetActive(si);
        if (_luna != null && _luna.gameObject.activeSelf != si) _luna.gameObject.SetActive(si);
    }

    /// Lo pone a `lejos` metros en esa dirección, mirando a la cámara y del tamaño angular pedido.
    private static void Colocar(Transform astro, Vector3 ojo, Vector3 direccion, float lejos, float grados)
    {
        if (direccion.sqrMagnitude < 0.0001f) return;
        direccion.Normalize();

        astro.position = ojo + direccion * lejos;
        astro.rotation = Quaternion.LookRotation(direccion, Vector3.up);

        // El tamaño se pide en grados de cielo para que no dependa de la distancia elegida.
        float lado = 2f * lejos * Mathf.Tan(grados * 0.5f * Mathf.Deg2Rad);
        astro.localScale = new Vector3(lado, lado, 1f);
    }

    private static void Pintar(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }
}
