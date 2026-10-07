using UnityEngine;

/// Zona de luciérnagas: puntos de luz que vagan y parpadean entre los árboles cuando es de noche.
///
/// El sistema de partículas se construye en código al arrancar (un solo ParticleSystem por zona,
/// sin Instantiate posterior) y solo emite mientras el jugador está cerca y fuera de un interior;
/// lejos se para y se vacía. La cantidad sube con DayNightCycle.NocheActual a partir de `desde`.
/// Las zonas del bosque las coloca «El Sendero/Mundo/Noche: luces de casas, faroles y
/// luciérnagas»; se pueden mover, copiar o redimensionar a mano. Ver INC-657.
[DisallowMultipleComponent]
public sealed class LuciernagasNocturnas : MonoBehaviour
{
    [Tooltip("Tamaño de la zona (ancho, alto, fondo). La base de la caja está en la posición del objeto.")]
    public Vector3 tamano = new Vector3(24f, 3f, 24f);

    [Tooltip("Luciérnagas a la vez a plena noche.")]
    [Range(1, 200)] public int cantidad = 35;

    [Tooltip("Parte de la noche (0-1) a partir de la que empiezan a salir.")]
    [Range(0f, 1f)] public float desde = 0.5f;

    [Tooltip("Distancia del jugador al borde de la zona a partir de la que se apagan.")]
    [Min(1f)] public float distanciaDeActivacion = 70f;

    [Tooltip("Material de partícula (aditivo, con un punto de luz suave). Vacío: se genera uno en código.")]
    public Material material;

    [Tooltip("Tonos de las luciérnagas; cada una sale con uno entre los dos.")]
    public Color colorA = new Color(0.8f, 1f, 0.3f);
    public Color colorB = new Color(1f, 0.82f, 0.3f);

    [Tooltip("Multiplica el color en el material; por encima de 1 brilla con el Bloom.")]
    [Min(0f)] public float brillo = 4f;

    [Tooltip("Tamaño de cada luciérnaga en metros (mínimo y máximo).")]
    public Vector2 tamanoDeLuz = new Vector2(0.08f, 0.16f);

    private const float Vida = 9f;
    private const float Revision = 0.5f;

    private static Material s_materialGenerado;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_materialGenerado = null;
#endif

    private ParticleSystem _ps;
    private ExteriorWorldRoot _exterior;
    private float _siguienteRevision;
    private bool _cerca;
    private bool _emitiendo;
    private float _ritmoAplicado = -1f;

    private void Awake()
    {
        _exterior = GetComponentInParent<ExteriorWorldRoot>(true);
        Construir();
    }

    private void OnDisable()
    {
        if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _emitiendo = false;
        _ritmoAplicado = -1f;
    }

    private void Update()
    {
        if (Time.time >= _siguienteRevision)
        {
            _siguienteRevision = Time.time + Revision;
            _cerca = JugadorCerca() && (_exterior == null || !_exterior.IsHidden);
        }

        float noche = Mathf.InverseLerp(desde, 1f, DayNightCycle.NocheActual);
        if (!_cerca || noche <= 0f)
        {
            // Al amanecer se dejan apagar solas; lejos o en un interior se vacía al instante.
            if (_emitiendo)
            {
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _emitiendo = false;
                _ritmoAplicado = -1f;
            }
            if (!_cerca && _ps.particleCount > 0)
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        float ritmo = noche * cantidad / Vida;
        if (Mathf.Abs(ritmo - _ritmoAplicado) > 0.05f)
        {
            var emision = _ps.emission;
            emision.rateOverTime = ritmo;
            _ritmoAplicado = ritmo;
        }
        if (!_emitiendo)
        {
            _ps.Play(true);
            _emitiendo = true;
        }
    }

    private bool JugadorCerca()
    {
        if (!PlayerService.TryGetComponent(out Transform jugador, true, false) || jugador == null) return false;
        var caja = new Bounds(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);
        return caja.SqrDistance(jugador.position) <= distanciaDeActivacion * distanciaDeActivacion;
    }

    private void Construir()
    {
        var go = new GameObject("Luciérnagas (partículas)");
        go.transform.SetParent(transform, false);
        _ps = go.AddComponent<ParticleSystem>();
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.duration = Vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Vida * 0.7f, Vida * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoDeLuz.x, tamanoDeLuz.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.CeilToInt(cantidad * 1.5f);
        main.cullingMode = ParticleSystemCullingMode.Pause;

        var emision = _ps.emission;
        emision.rateOverTime = 0f;

        var forma = _ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano;
        forma.position = new Vector3(0f, tamano.y * 0.5f + 0.3f, 0f);

        // Vuelo errático y lento, como el de una luciérnaga.
        var ruido = _ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.6f;
        ruido.frequency = 0.25f;
        ruido.scrollSpeed = 0.15f;
        ruido.damping = true;
        ruido.quality = ParticleSystemNoiseQuality.Medium;

        // Se encienden y apagan varias veces en su vida; con vidas distintas no parpadean a la vez.
        var color = _ps.colorOverLifetime;
        color.enabled = true;
        var parpadeo = new Gradient();
        parpadeo.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.1f, 0.3f),
                new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.15f, 0.62f), new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f),
            });
        color.color = new ParticleSystem.MinMaxGradient(parpadeo);

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sharedMaterial = material != null ? material : MaterialGenerado();
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;

        var bloque = new MaterialPropertyBlock();
        Color tinte = Color.white * brillo;
        tinte.a = 1f;
        bloque.SetColor("_BaseColor", tinte);
        bloque.SetColor("_Color", tinte);
        render.SetPropertyBlock(bloque);
    }

    /// Punto de luz suave generado una sola vez y compartido por todas las zonas sin material.
    private static Material MaterialGenerado()
    {
        if (s_materialGenerado != null) return s_materialGenerado;

        const int lado = 32;
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixeles = new Color32[lado * lado];
        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float dx = (x + 0.5f) / lado * 2f - 1f, dy = (y + 0.5f) / lado * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixeles[y * lado + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        textura.SetPixels32(pixeles);
        textura.Apply(false, true);

        s_materialGenerado = new Material(Shader.Find("Sprites/Default")) { mainTexture = textura, name = "Luciérnaga (generado)" };
        return s_materialGenerado;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 1f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);
    }
#endif
}
