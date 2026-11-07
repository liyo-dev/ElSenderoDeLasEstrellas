using UnityEngine;

/// Una pieza que lleva puesta un personaje (un aro, un casco, una cadena) y que se suelta al
/// caer él: deja de seguir al esqueleto, cae con física, sus brillos se apagan poco a poco y,
/// cuando se queda quieta en el suelo, se congela para no gastar física.
/// Vive en la propia pieza: sigue funcionando aunque el personaje se destruya al morir.
[DisallowMultipleComponent]
public class PiezaQueCae : MonoBehaviour
{
    [Tooltip("Malla con la que choca la pieza al caer (convexa). Vacío = la del primer MeshFilter de la pieza.")]
    [SerializeField] private Mesh mallaDeChoque;
    [Tooltip("Renderers cuyo color (_BaseColor) pasa al 'color apagado' al caer, como unas runas que se apagan.")]
    [SerializeField] private Renderer[] seApagan;
    [Tooltip("Color final de lo que se apaga.")]
    [SerializeField, ColorUsage(false, true)] private Color colorApagado = new Color(0.08f, 0.04f, 0.03f);
    [Tooltip("Segundos que tarda en apagarse del todo.")]
    [SerializeField, Min(0.01f)] private float segundosEnApagarse = 2.5f;
    [Tooltip("Masa de la pieza (kg).")]
    [SerializeField, Min(0.01f)] private float masa = 1.5f;
    [Tooltip("Segundos máximos con física: pasado este tiempo (o antes, si se queda quieta) se congela.")]
    [SerializeField, Min(0.5f)] private float segundosConFisica = 8f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private Rigidbody _rb;
    private MaterialPropertyBlock _mpb;
    private Color[] _colorInicial;
    private float _tiempo;
    private bool _cayendo;

    /// Configura la pieza desde el montaje del prefab.
    public void Configurar(Mesh malla, Renderer[] queSeApagan, Color apagado)
    {
        mallaDeChoque = malla;
        seApagan = queSeApagan;
        colorApagado = apagado;
    }

    /// Suelta la pieza: sale del esqueleto (se queda en la misma escena), le da cuerpo físico y
    /// un empujón. 'brilloDeSalida' es el color con el que empiezan a apagarse sus brillos.
    public void Soltar(Vector3 velocidad, Vector3 giro, Color brilloDeSalida)
    {
        if (_cayendo) return;
        _cayendo = true;

        transform.SetParent(null, true);
        PonerCapa(transform, 0);

        if (mallaDeChoque == null)
        {
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf) mallaDeChoque = mf.sharedMesh;
        }
        if (mallaDeChoque != null)
        {
            var choque = gameObject.AddComponent<MeshCollider>();
            choque.sharedMesh = mallaDeChoque;
            choque.convex = true;
        }

        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = masa;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _rb.linearVelocity = velocidad;
        _rb.angularVelocity = giro;

        _mpb = new MaterialPropertyBlock();
        int n = seApagan != null ? seApagan.Length : 0;
        _colorInicial = new Color[n];
        for (int i = 0; i < n; i++) _colorInicial[i] = brilloDeSalida;
        PintarApagado(0f);
        enabled = true;
    }

    void Awake()
    {
        // Mientras va puesta no hace nada: solo trabaja al soltarse.
        if (!_cayendo) enabled = false;
    }

    void Update()
    {
        if (!_cayendo) { enabled = false; return; }
        _tiempo += Time.deltaTime;

        if (_tiempo <= segundosEnApagarse) PintarApagado(_tiempo / segundosEnApagarse);

        bool apagada = _tiempo > segundosEnApagarse;
        bool quieta = _rb == null || _rb.isKinematic || (_tiempo > 1f && _rb.IsSleeping()) || _tiempo > segundosConFisica;
        if (quieta && _rb != null && !_rb.isKinematic)
        {
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.None;
        }
        if (apagada && quieta)
        {
            PintarApagado(1f);
            enabled = false;
        }
    }

    private void PintarApagado(float t)
    {
        if (seApagan == null) return;
        // Brilla un instante más al soltarse y luego se apaga: ease-out.
        float k = 1f - (1f - t) * (1f - t);
        for (int i = 0; i < seApagan.Length; i++)
        {
            var r = seApagan[i];
            if (!r) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, Color.Lerp(_colorInicial[i], colorApagado, k));
            r.SetPropertyBlock(_mpb);
        }
    }

    private static void PonerCapa(Transform t, int capa)
    {
        t.gameObject.layer = capa;
        for (int i = 0; i < t.childCount; i++) PonerCapa(t.GetChild(i), capa);
    }
}
