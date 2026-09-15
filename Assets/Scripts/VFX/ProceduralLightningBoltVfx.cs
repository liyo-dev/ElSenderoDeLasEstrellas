using UnityEngine;

/// <summary>
/// VFX de rayo/trueno generado ENTERAMENTE por código, sin depender de ningún asset de terceros —
/// pensado para sustituir a lightImpactVfx/darkImpactVfx en PrologueDreamSequencer (pedido de
/// Raúl, 12/09/2026: "en el centro debe salir el rayo", en vez del orbe actual). Genera un
/// LineRenderer en zigzag desde justo encima del punto de impacto hasta su posición, más 1-2
/// ramas secundarias más finas, y un flash de luz puntual que decae.
///
/// FIX (12 sep 2026): "MissingReferenceException... ObjectPool.Get()" — reportado justo después
/// de que el rayo ya se viera bien en el centro. Causa: lightImpactVfx/darkImpactVfx se reproducen
/// vía VfxPoolService (ver Co_Collision/Co_WarFlashes en PrologueDreamSequencer), que NUNCA
/// destruye sus instancias — las pre-crea una vez (ObjectPool.CreateNewObject) y luego solo hace
/// SetActive(true)/SetActive(false) para reutilizarlas. La primera versión de este script llamaba
/// Destroy(gameObject, lifetime) en Awake() — pero Awake() ya se dispara en el PRE-WARM del pool
/// (4 instancias creadas por adelantado antes de que nadie las "reproduzca"), así que esas 4
/// instancias se autodestruían solas ~0.35s después de crearse el pool, mucho antes de que
/// Co_Collision/Co_WarFlashes llegaran a pedir una — pool.Get() intentaba reactivar un Transform ya
/// destruido y explotaba en el SetActive(true) de ObjectPool.cs:100. Ahora el prefab NUNCA se
/// destruye a sí mismo: construye los LineRenderer/luz UNA vez en Awake() y los REGENERA (nueva
/// forma en zigzag, luz a intensidad completa) en OnEnable() — que es lo que de verdad se dispara
/// cada vez que el pool lo reactiva —, dejando que sea VfxPoolService quien decida cuándo
/// desactivarlo (SetActive(false) vía pool.Return()), exactamente igual que espera de cualquier
/// prefab pooled.
///
/// Se generan dos prefabs con este mismo script (VFX_RayoColision_Claro / _Oscuro, en
/// Assets/_VFX/Prologue), cada uno con su propio 'boltColor' — así se conserva la distinción
/// visual "energía clara de Will" / "energía oscura del Mago Oscuro" que ya tenían lightFlashColor/
/// darkFlashColor en el sequencer, ahora también en el propio rayo.
/// </summary>
[DisallowMultipleComponent]
public class ProceduralLightningBoltVfx : MonoBehaviour
{
    [Header("Forma")]
    [SerializeField] private float boltHeight = 2.2f;
    [SerializeField] private int segments = 9;
    [SerializeField] private float jitterAmount = 0.22f;
    [SerializeField] private int branchCount = 2;

    [Header("Aspecto")]
    [SerializeField] private Color boltColor = new Color(1f, 0.95f, 0.8f, 1f);
    [SerializeField] private float boltWidth = 0.06f;
    [Tooltip("Opcional — si se deja vacío, usa un material additivo compartido generado en runtime (Sprites/Default o similar), sin depender de ningún asset del proyecto.")]
    [SerializeField] private Material boltMaterial;

    [Header("Luz")]
    [SerializeField] private float flashLightIntensity = 6f;
    [SerializeField] private float flashLightRange = 5f;
    [Tooltip("Velocidad (por segundo) a la que decae la intensidad de la luz tras cada reactivación — no controla cuánto dura el VFX en pantalla, eso lo decide quien llama a VfxPoolService.Play(..., lifetime).")]
    [SerializeField] private float flashDecaySpeed = 12f;

    private Light _flashLight;
    private LineRenderer _mainBoltLr;
    private LineRenderer[] _branchRenderers;
    private Transform[] _branchRoots;
    private static Material _sharedRuntimeMat;

    void Awake()
    {
        _mainBoltLr = CreateBoltRenderer(transform, "Bolt");

        _branchRenderers = new LineRenderer[Mathf.Max(0, branchCount)];
        _branchRoots = new Transform[_branchRenderers.Length];
        for (int b = 0; b < _branchRenderers.Length; b++)
        {
            var branchRoot = new GameObject("Branch").transform;
            branchRoot.SetParent(transform, false);
            _branchRoots[b] = branchRoot;
            _branchRenderers[b] = CreateBoltRenderer(branchRoot, "BranchBolt");
        }

        _flashLight = gameObject.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.range = flashLightRange;
        _flashLight.shadows = LightShadows.None;
    }

    // Se dispara cada vez que el pool reactiva esta instancia (SetActive(true)) — aquí es donde
    // hay que "relanzar" el efecto, no en Awake() (eso solo pasa una vez, en el pre-warm).
    void OnEnable()
    {
        RandomizeBolt(_mainBoltLr, boltHeight, jitterAmount, 1f);

        for (int b = 0; b < _branchRenderers.Length; b++)
        {
            float t = Random.Range(0.25f, 0.7f);
            _branchRoots[b].localPosition = Vector3.up * boltHeight * (1f - t);
            RandomizeBolt(_branchRenderers[b], Mathf.Max(0.3f, boltHeight * (1f - t) * 0.5f),
                jitterAmount * 0.6f, 0.55f);
        }

        if (_flashLight != null)
        {
            _flashLight.color = boltColor;
            _flashLight.intensity = flashLightIntensity;
        }
    }

    void Update()
    {
        if (_flashLight != null && _flashLight.intensity > 0f)
            _flashLight.intensity = Mathf.Lerp(_flashLight.intensity, 0f, Time.deltaTime * flashDecaySpeed);
    }

    private LineRenderer CreateBoltRenderer(Transform root, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = boltMaterial != null ? boltMaterial : GetRuntimeMaterial();
        lr.startColor = boltColor;
        lr.endColor = new Color(boltColor.r, boltColor.g, boltColor.b, 0f);
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        return lr;
    }

    private void RandomizeBolt(LineRenderer lr, float height, float jitter, float widthScale)
    {
        int segCount = Mathf.Max(2, segments);
        lr.widthMultiplier = boltWidth * widthScale;
        lr.positionCount = segCount + 1;

        Vector3 start = Vector3.up * height;
        Vector3 end = Vector3.zero;
        for (int i = 0; i <= segCount; i++)
        {
            float t = (float)i / segCount;
            Vector3 p = Vector3.Lerp(start, end, t);
            if (i != 0 && i != segCount)
                p += new Vector3(Random.Range(-jitter, jitter), 0f, Random.Range(-jitter, jitter));
            lr.SetPosition(i, p);
        }
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
