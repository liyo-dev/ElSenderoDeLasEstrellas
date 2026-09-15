using UnityEngine;

/// <summary>
/// Explosión de chispas/rayos radiales — pensada como el "boom" real de Co_Collision en
/// PrologueDreamSequencer, distinto de los dos ProceduralLightningBoltVfx (que son cada rayo
/// individual, uno por bando). Pedido de Raúl (12/09/2026): "cuando ambos hechizos se unen debe
/// ocurrir una explosion y lo que vemos es el mismo prefab" — antes la colisión solo mostraba los
/// dos mismos rayos de siempre, sin ningún efecto distinto que se leyera como impacto/explosión.
///
/// Genera N streaks cortos que salen disparados en direcciones aleatorias desde el centro (en vez
/// de un único zigzag vertical como ProceduralLightningBoltVfx) más un flash de luz blanco/cálido
/// muy intenso y de decaimiento rápido — mismo enfoque 100% por código, sin dependencias externas.
///
/// FIX pooling (mismo aprendizaje que ProceduralLightningBoltVfx, ver su cabecera): nada se
/// destruye a sí mismo. Awake() crea los LineRenderer/luz una vez; OnEnable() — que es lo que
/// dispara el pool cada vez que reactiva la instancia — regenera direcciones/longitudes nuevas y
/// resetea la luz a intensidad completa.
/// </summary>
[DisallowMultipleComponent]
public class ProceduralSparkBurstVfx : MonoBehaviour
{
    [Header("Forma")]
    [SerializeField] private int sparkCount = 10;
    [SerializeField] private float minLength = 0.5f;
    [SerializeField] private float maxLength = 1.6f;
    [Tooltip("0 = esfera completa, 1 = todas las chispas hacia arriba.")]
    [Range(0f, 1f)] [SerializeField] private float upwardBias = 0.35f;

    [Header("Aspecto")]
    // FIX (12/09/2026, pedido de Raúl: "vemos una explosion (fuego u otra que usemos)"): antes
    // era un blanco cálido casi neutro (chispa de energía); ahora naranja/rojo de verdad para que
    // se lea como fuego/explosión, no como otro rayo más.
    [SerializeField] private Color coreColor = new Color(1f, 0.42f, 0.08f, 1f);
    [SerializeField] private float sparkWidth = 0.05f;
    [SerializeField] private Material sparkMaterial;

    [Header("Luz")]
    [SerializeField] private float flashLightIntensity = 12f;
    [SerializeField] private float flashLightRange = 8f;
    [SerializeField] private float flashDecaySpeed = 7f;

    private Light _flashLight;
    private LineRenderer[] _sparks;
    private static Material _sharedRuntimeMat;

    void Awake()
    {
        _sparks = new LineRenderer[Mathf.Max(1, sparkCount)];
        for (int i = 0; i < _sparks.Length; i++)
        {
            var go = new GameObject("Spark");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = sparkMaterial != null ? sparkMaterial : GetRuntimeMaterial();
            lr.startColor = coreColor;
            lr.endColor = new Color(coreColor.r, coreColor.g, coreColor.b, 0f);
            lr.numCapVertices = 2;
            lr.positionCount = 2;
            _sparks[i] = lr;
        }

        _flashLight = gameObject.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.range = flashLightRange;
        _flashLight.shadows = LightShadows.None;
    }

    // Ver comentario de cabecera — esto es lo que el pool dispara en cada reactivación, no Awake().
    void OnEnable()
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * upwardBias + dir.y * (1f - upwardBias);
            dir.Normalize();
            float len = Random.Range(minLength, maxLength);

            var lr = _sparks[i];
            lr.widthMultiplier = sparkWidth;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, dir * len);
        }

        if (_flashLight != null)
        {
            _flashLight.color = coreColor;
            _flashLight.intensity = flashLightIntensity;
        }
    }

    void Update()
    {
        if (_flashLight != null && _flashLight.intensity > 0f)
            _flashLight.intensity = Mathf.Lerp(_flashLight.intensity, 0f, Time.deltaTime * flashDecaySpeed);
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
            if (_sharedRuntimeMat.HasProperty("_Surface")) _sharedRuntimeMat.SetFloat("_Surface", 1f);
            if (_sharedRuntimeMat.HasProperty("_Blend")) _sharedRuntimeMat.SetFloat("_Blend", 1f);
        }
        return _sharedRuntimeMat;
    }
}
