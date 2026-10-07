using UnityEngine;

/// Base de los efectos de ambiente hechos con un sistema de partículas fijo en el mundo
/// (luciérnagas, niebla del bosque, humo de chimenea).
///
/// Construye un único ParticleSystem en Awake (la subclase lo configura) y decide cada frame su
/// ritmo de emisión. Solo emite con el jugador a menos de `distanciaDeActivacion` de la zona
/// (revisado cada 0,5 s) y fuera de un interior; lejos se vacía. Avisa a la subclase cuando cambia
/// la noche (DayNightCycle.NocheActual) para que ajuste color o cantidad. Ver INC-657.
public abstract class ParticulasDeAmbiente : MonoBehaviour
{
    [Tooltip("Distancia del jugador al borde de la zona a partir de la que se apaga.")]
    [Min(1f)] public float distanciaDeActivacion = 70f;

    [Tooltip("Material de partícula. Vacío: punto suave generado en código.")]
    public Material material;

    private const float Revision = 0.5f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static Material s_materialGenerado;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_materialGenerado = null;
#endif

    protected ParticleSystem Sistema { get; private set; }
    protected ParticleSystemRenderer Render { get; private set; }

    private ExteriorWorldRoot _exterior;
    private MaterialPropertyBlock _bloque;
    private float _siguienteRevision;
    private bool _cerca;
    private bool _emitiendo;
    private float _ritmoAplicado = -1f;
    private float _nocheAplicada = -1f;

    /// Caja en el mundo que ocupan las partículas (para medir la distancia al jugador).
    protected abstract Bounds Zona { get; }

    /// Configura el sistema recién creado (ya parado y vacío).
    protected abstract void Configurar(ParticleSystem sistema, ParticleSystemRenderer render);

    /// Partículas por segundo con esta noche (0-1); 0 apaga la emisión.
    protected abstract float Ritmo(float noche);

    /// Se llama al arrancar y cada vez que la noche cambia de forma apreciable.
    protected virtual void AlCambiarLaNoche(float noche) { }

    protected virtual void Awake()
    {
        _exterior = GetComponentInParent<ExteriorWorldRoot>(true);

        var go = new GameObject(GetType().Name);
        go.transform.SetParent(transform, false);
        Sistema = go.AddComponent<ParticleSystem>();
        Sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Render = go.GetComponent<ParticleSystemRenderer>();
        Render.sharedMaterial = material != null ? material : PuntoSuave();
        Render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Render.receiveShadows = false;

        var main = Sistema.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        var emision = Sistema.emission;
        emision.rateOverTime = 0f;

        Configurar(Sistema, Render);
    }

    protected virtual void OnDisable()
    {
        if (Sistema != null) Sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        float noche = DayNightCycle.NocheActual;
        if (Mathf.Abs(noche - _nocheAplicada) > 0.01f || (noche == 0f) != (_nocheAplicada == 0f))
        {
            _nocheAplicada = noche;
            AlCambiarLaNoche(noche);
        }

        float ritmo = _cerca ? Ritmo(noche) : 0f;
        if (ritmo <= 0f)
        {
            // Si deja de tocar se dejan morir solas; lejos o en un interior se vacía al instante.
            if (_emitiendo)
            {
                Sistema.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _emitiendo = false;
                _ritmoAplicado = -1f;
            }
            if (!_cerca && Sistema.particleCount > 0)
                Sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        if (Mathf.Abs(ritmo - _ritmoAplicado) > 0.05f)
        {
            var emision = Sistema.emission;
            emision.rateOverTime = ritmo;
            _ritmoAplicado = ritmo;
        }
        if (!_emitiendo)
        {
            Sistema.Play(true);
            _emitiendo = true;
        }
    }

    private bool JugadorCerca()
    {
        if (!PlayerService.TryGetComponent(out Transform jugador, true, false) || jugador == null) return false;
        return Zona.SqrDistance(jugador.position) <= distanciaDeActivacion * distanciaDeActivacion;
    }

    /// Multiplica el color del material (por encima de 1 brilla con el Bloom).
    protected void Tintar(Color color)
    {
        _bloque ??= new MaterialPropertyBlock();
        _bloque.SetColor(BaseColorId, color);
        _bloque.SetColor(ColorId, color);
        Render.SetPropertyBlock(_bloque);
    }

    /// Curva de alfa: aparece, se mantiene y desaparece.
    protected static ParticleSystem.MinMaxGradient Fundido(float alfa, float entrada, float salida)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alfa, entrada), new GradientAlphaKey(alfa, salida), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    /// Punto de luz suave generado una sola vez y compartido por todos los efectos sin material.
    private static Material PuntoSuave()
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

        s_materialGenerado = new Material(Shader.Find("Sprites/Default")) { mainTexture = textura, name = "Punto suave (generado)" };
        return s_materialGenerado;
    }
}
