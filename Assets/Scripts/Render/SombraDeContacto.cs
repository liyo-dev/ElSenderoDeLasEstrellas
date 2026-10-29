using UnityEngine;
using UnityEngine.Rendering;

/// Sombra suave bajo los pies de un personaje: un disco que se apoya en el suelo que tiene debajo,
/// se encoge y se aclara al despegarse (saltos, vuelo) y desaparece si no hay suelo cerca. Ancla a
/// los personajes al escenario sin sombras en tiempo real (la cámara del juego no las dibuja y la
/// mayoría de materiales Quibli no las reciben). Se añade sola desde NPCSimpleAnimator y
/// EnemigoDelMundo; un personaje que no la quiera la lleva puesta y desactivada. Ver INC-865.
[DisallowMultipleComponent]
public sealed class SombraDeContacto : MonoBehaviour
{
    private const string NombreDelShader = "El Sendero/Sombra de contacto";
    private const float AlturaDelRayo = 0.5f;

    [Tooltip("Radio del disco en metros. 0 = según el collider o el tamaño del personaje.")]
    [SerializeField, Min(0f)] private float radio;
    [SerializeField, Range(0f, 1f)] private float opacidad = 0.55f;
    [Tooltip("Altura sobre el suelo a la que la sombra ya no se ve.")]
    [SerializeField, Min(0.1f)] private float alturaMaxima = 2.5f;

    private static Mesh s_disco;
    private static Material s_material;
    private static readonly int OpacidadId = Shader.PropertyToID("_Opacidad");
    private static readonly RaycastHit[] s_impactos = new RaycastHit[4];

    private Transform _sombra;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _bloque;
    private float _radio;
    private float _opacidadAplicada = -1f;

    /// Añade la sombra si el personaje aún no tiene una (activa o desactivada a propósito).
    public static void Asegurar(GameObject personaje)
    {
        if (personaje != null && !personaje.TryGetComponent<SombraDeContacto>(out _))
            personaje.AddComponent<SombraDeContacto>();
    }

    private void OnEnable()
    {
        if (_sombra == null && !Crear()) { enabled = false; return; }
        _sombra.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_sombra != null && _sombra.gameObject.activeSelf) _sombra.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_sombra != null) Destroy(_sombra.gameObject);
    }

    private bool Crear()
    {
        if (s_material == null)
        {
            var shader = Shader.Find(NombreDelShader);
            if (shader == null) return false;
            s_material = new Material(shader) { name = "Sombra de contacto", enableInstancing = true };
        }
        if (s_disco == null) s_disco = CrearDisco();

        var go = new GameObject("Sombra de contacto");
        go.layer = gameObject.layer;
        _sombra = go.transform;
        _sombra.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = s_disco;
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = s_material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _bloque = new MaterialPropertyBlock();
        _radio = radio > 0f ? radio : RadioAutomatico();
        return true;
    }

    private float RadioAutomatico()
    {
        float escala = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        if (TryGetComponent<CharacterController>(out var cc)) return Mathf.Clamp(cc.radius * escala * 1.6f, 0.25f, 3f);
        if (TryGetComponent<CapsuleCollider>(out var cap)) return Mathf.Clamp(cap.radius * escala * 1.6f, 0.25f, 3f);
        var renderers = GetComponentsInChildren<Renderer>();
        var limites = new Bounds(transform.position, Vector3.zero);
        bool hay = false;
        foreach (var r in renderers)
        {
            if (r == _renderer || r is ParticleSystemRenderer) continue;
            if (!hay) { limites = r.bounds; hay = true; } else limites.Encapsulate(r.bounds);
        }
        return hay ? Mathf.Clamp(Mathf.Max(limites.extents.x, limites.extents.z) * 0.8f, 0.25f, 3f) : 0.45f;
    }

    private void LateUpdate()
    {
        if (_sombra == null) return;
        // Fuera de pantalla basta con revisar de vez en cuando si ha vuelto a verse.
        if (_sombra.gameObject.activeSelf && !_renderer.isVisible && (Time.frameCount & 7) != 0) return;

        Vector3 origen = transform.position + Vector3.up * AlturaDelRayo;
        int n = Physics.RaycastNonAlloc(origen, Vector3.down, s_impactos, AlturaDelRayo + alturaMaxima,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int mejor = -1;
        for (int i = 0; i < n; i++)
        {
            if (s_impactos[i].transform.IsChildOf(transform)) continue;
            if (mejor < 0 || s_impactos[i].distance < s_impactos[mejor].distance) mejor = i;
        }
        if (mejor < 0)
        {
            if (_sombra.gameObject.activeSelf) _sombra.gameObject.SetActive(false);
            return;
        }

        var impacto = s_impactos[mejor];
        float altura = Mathf.Max(0f, impacto.distance - AlturaDelRayo);
        float cercania = 1f - Mathf.Clamp01(altura / alturaMaxima);
        if (!_sombra.gameObject.activeSelf) _sombra.gameObject.SetActive(true);

        _sombra.SetPositionAndRotation(impacto.point + impacto.normal * 0.02f,
            Quaternion.FromToRotation(Vector3.up, impacto.normal));
        float diametro = _radio * 2f * Mathf.Lerp(0.6f, 1f, cercania);
        Vector3 padre = transform.lossyScale;
        _sombra.localScale = new Vector3(diametro / Mathf.Max(padre.x, 1e-4f), 1f / Mathf.Max(padre.y, 1e-4f), diametro / Mathf.Max(padre.z, 1e-4f));

        float valor = opacidad * cercania;
        if (Mathf.Abs(valor - _opacidadAplicada) > 0.01f)
        {
            _opacidadAplicada = valor;
            _bloque.SetFloat(OpacidadId, valor);
            _renderer.SetPropertyBlock(_bloque);
        }
    }

    /// Cuadrado de 1 m en el plano XZ; el shader dibuja dentro el disco difuminado.
    private static Mesh CrearDisco()
    {
        var mesh = new Mesh { name = "Disco de sombra" };
        mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
