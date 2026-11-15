using UnityEngine;

/// <summary>
/// Estrellas con volumen que suben despacio girando sobre sí mismas, como adorno de ambiente.
/// Cada partícula es una malla de estrella biselada (las caras alternan dos tonos para que se lea el
/// volumen). Son opacas: aparecen creciendo y se van encogiendo, así escriben profundidad y normales
/// y el contorno de tinta las dibuja como al resto del mundo. El color multiplica al material (por
/// encima de 1 brilla con el Bloom) y se puede cambiar en juego con <see cref="CambiarColor"/>.
/// Lejos se apagan (ParticulasDeAmbiente).
/// </summary>
[DisallowMultipleComponent]
public sealed class EstrellasQueSuben : ParticulasDeAmbiente
{
    [Tooltip("Tamaño de la zona (ancho, alto, fondo); la base de la caja está en el objeto.")]
    public Vector3 tamano = new(40f, 14f, 60f);
    [Tooltip("Estrellas a la vez en la zona.")]
    [Range(1, 100)] public int cantidad = 12;
    [Tooltip("Velocidad de subida en m/s (mínima y máxima).")]
    public Vector2 subida = new(0.35f, 0.8f);
    [Tooltip("Tamaño de cada estrella en metros (mínimo y máximo).")]
    public Vector2 tamanoDeEstrella = new(0.25f, 0.6f);
    [Tooltip("Segundos que dura cada estrella.")]
    [Min(1f)] public float vida = 14f;
    [Tooltip("Giro máximo sobre su eje vertical, en grados por segundo.")]
    [Min(0f)] public float giro = 70f;
    [Tooltip("Vaivén lateral mientras suben (fuerza del ruido).")]
    [Min(0f)] public float vaiven = 0.25f;
    [Tooltip("Color de las estrellas; se multiplica por el brillo.")]
    public Color color = new(1f, 0.88f, 0.6f);
    [Tooltip("Por encima de 1 brillan con el Bloom.")]
    [Min(0f)] public float brillo = 2f;
    [Tooltip("Número de puntas de la estrella.")]
    [Range(4, 8)] public int puntas = 5;
    [Tooltip("Radio de los valles entre puntas respecto al de las puntas.")]
    [Range(0.25f, 0.8f)] public float valle = 0.45f;
    [Tooltip("Grosor del bisel (altura del centro) respecto al radio.")]
    [Range(0.05f, 0.6f)] public float bisel = 0.3f;

    private Mesh _malla;

    protected override Bounds Zona => new(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);

    protected override float Ritmo(float noche) => cantidad / vida;

    /// Cambia el color de todas las estrellas a la vez (las que ya están subiendo incluidas).
    public void CambiarColor(Color nuevo)
    {
        color = nuevo;
        if (Render != null) AplicarColor();
    }

    protected override void Configurar(ParticleSystem sistema, ParticleSystemRenderer render)
    {
        var main = sistema.main;
        main.prewarm = true;
        main.duration = vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.75f, vida * 1.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoDeEstrella.x, tamanoDeEstrella.y);
        main.startColor = Color.white;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        main.maxParticles = Mathf.CeilToInt(cantidad * 1.5f);

        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano;
        forma.position = new Vector3(0f, tamano.y * 0.5f, 0f);

        var vel = sistema.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(subida.x, subida.y);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var ruido = sistema.noise;
        ruido.enabled = vaiven > 0f;
        ruido.strength = vaiven;
        ruido.frequency = 0.25f;
        ruido.scrollSpeed = 0.1f;
        ruido.quality = ParticleSystemNoiseQuality.Low;

        float g = giro * Mathf.Deg2Rad;
        var rot = sistema.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-g * 0.15f, g * 0.15f);
        rot.y = new ParticleSystem.MinMaxCurve(-g, g);
        rot.z = new ParticleSystem.MinMaxCurve(-g * 0.25f, g * 0.25f);

        // Opacas: aparecen y se van con el tamaño, no con transparencia.
        var talla = sistema.sizeOverLifetime;
        talla.enabled = true;
        var curva = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
        talla.size = new ParticleSystem.MinMaxCurve(1f, curva);

        _malla = Estrella(puntas, valle, bisel);
        render.renderMode = ParticleSystemRenderMode.Mesh;
        render.mesh = _malla;
        render.alignment = ParticleSystemRenderSpace.World;
        AplicarColor();
    }

    private void OnDestroy()
    {
        if (_malla != null) Destroy(_malla);
    }

    private void AplicarColor()
    {
        Color tinte = color * brillo;
        tinte.a = 1f;
        Tintar(tinte);
    }

    /// Estrella biselada de radio 1: cada tramo entre punta y valle es una cara plana que sube hacia
    /// el centro, por delante y por detrás. Las caras alternan tono (U 0,25 claro, U 0,75 oscuro:
    /// el material lleva una textura de dos tonos).
    private static Mesh Estrella(int n, float valle, float bisel)
    {
        int lados = n * 2;
        var v = new Vector3[lados * 6];
        var uv = new Vector2[lados * 6];
        var tri = new int[lados * 6];
        var frente = new Vector3(0f, 0f, -bisel);
        var dorso = new Vector3(0f, 0f, bisel);
        int k = 0;
        for (int i = 0; i < lados; i++)
        {
            Vector3 a = Borde(i, n, valle), b = Borde(i + 1, n, valle);
            bool clara = i % 2 == 0;
            // Delante (mirando hacia −Z) y detrás, con el tono cambiado para que al girar se note.
            Cara(v, uv, tri, ref k, frente, b, a, clara);
            Cara(v, uv, tri, ref k, dorso, a, b, !clara);
        }
        var malla = new Mesh { name = "Estrella con volumen (generada)", vertices = v, uv = uv, triangles = tri };
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    private static Vector3 Borde(int i, int n, float valle)
    {
        float ang = Mathf.PI * 0.5f + i * Mathf.PI / n;
        float r = i % 2 == 0 ? 1f : valle;
        return new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
    }

    private static void Cara(Vector3[] v, Vector2[] uv, int[] tri, ref int k, Vector3 c, Vector3 p, Vector3 q, bool clara)
    {
        var t = new Vector2(clara ? 0.25f : 0.75f, 0.5f);
        v[k] = c; v[k + 1] = p; v[k + 2] = q;
        uv[k] = uv[k + 1] = uv[k + 2] = t;
        tri[k] = k; tri[k + 1] = k + 1; tri[k + 2] = k + 2;
        k += 3;
    }
}
