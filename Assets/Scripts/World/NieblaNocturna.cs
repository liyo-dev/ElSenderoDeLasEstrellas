using UnityEngine;

/// Niebla baja y azulada que se arrastra entre los árboles de noche: nubecillas grandes, lentas y
/// casi transparentes pegadas al suelo. Va en las mismas zonas del bosque que las luciérnagas
/// (las coloca «El Sendero/Mundo/Noche: luces de casas, faroles y luciérnagas»). Ver INC-657.
[DisallowMultipleComponent]
public sealed class NieblaNocturna : ParticulasDeAmbiente
{
    [Tooltip("Tamaño de la zona (ancho, alto, fondo). La base de la caja está en la posición del objeto.")]
    public Vector3 tamano = new Vector3(24f, 1f, 24f);

    [Tooltip("Nubes de niebla a la vez a plena noche.")]
    [Range(1, 100)] public int cantidad = 18;

    [Tooltip("Parte de la noche (0-1) a partir de la que empieza a formarse.")]
    [Range(0f, 1f)] public float desde = 0.3f;

    [Tooltip("Color de la niebla (la luna la tiñe de azul).")]
    public Color color = new Color(0.55f, 0.65f, 0.85f);

    [Tooltip("Opacidad máxima de cada nube (0-1); muchas superpuestas se suman.")]
    [Range(0f, 1f)] public float opacidad = 0.22f;

    [Tooltip("Tamaño de cada nube en metros (mínimo y máximo).")]
    public Vector2 tamanoDeNube = new Vector2(4f, 7f);

    private const float Vida = 16f;

    protected override Bounds Zona => new Bounds(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);

    protected override float Ritmo(float noche)
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo != null && ciclo.IsRaining) return 0f;   // con lluvia ya manda la niebla del clima
        return Mathf.InverseLerp(desde, 1f, noche) * cantidad / Vida;
    }

    protected override void Configurar(ParticleSystem sistema, ParticleSystemRenderer render)
    {
        var main = sistema.main;
        main.prewarm = true;
        main.duration = Vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Vida * 0.8f, Vida * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoDeNube.x, tamanoDeNube.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.CeilToInt(cantidad * 1.5f);

        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(tamano.x, Mathf.Max(0.1f, tamano.y), tamano.z);
        forma.position = new Vector3(0f, tamano.y * 0.5f + 0.4f, 0f);
        forma.randomDirectionAmount = 1f;

        var giro = sistema.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var crecer = sistema.sizeOverLifetime;
        crecer.enabled = true;
        crecer.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.3f));

        var alfa = sistema.colorOverLifetime;
        alfa.enabled = true;
        alfa.color = Fundido(opacidad, 0.3f, 0.7f);

        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sortMode = ParticleSystemSortMode.Distance;
        Tintar(Color.white);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.65f, 0.85f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);
    }
#endif
}
