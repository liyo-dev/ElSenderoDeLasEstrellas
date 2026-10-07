using UnityEngine;

/// Humo que sale de una chimenea: sube despacio, se ensancha y se deshace. Echa algo más de humo
/// de noche (hay fuego encendido dentro) y se oscurece con ella para no brillar en la oscuridad,
/// porque las partículas no reciben luz. Lo coloca en lo alto de cada chimenea del pack
/// «El Sendero/Mundo/Noche: luces de casas, faroles y luciérnagas». Ver INC-657.
[DisallowMultipleComponent]
public sealed class HumoDeChimenea : ParticulasDeAmbiente
{
    [Tooltip("Bocanadas por segundo de día.")]
    [Min(0f)] public float ritmoDeDia = 1.5f;

    [Tooltip("Bocanadas por segundo a plena noche.")]
    [Min(0f)] public float ritmoDeNoche = 3f;

    [Tooltip("Color del humo de día.")]
    public Color colorDeDia = new Color(0.85f, 0.85f, 0.88f);

    [Tooltip("Color del humo a plena noche (oscuro: lo ilumina solo la luna).")]
    public Color colorDeNoche = new Color(0.22f, 0.24f, 0.3f);

    [Tooltip("Opacidad máxima de cada bocanada (0-1).")]
    [Range(0f, 1f)] public float opacidad = 0.45f;

    private const float Vida = 7f;

    protected override Bounds Zona => new Bounds(transform.position + Vector3.up * 3f, new Vector3(4f, 6f, 4f));

    protected override float Ritmo(float noche) => Mathf.Lerp(ritmoDeDia, ritmoDeNoche, noche);

    protected override void AlCambiarLaNoche(float noche)
    {
        Color tinte = Color.Lerp(colorDeDia, colorDeNoche, noche);
        tinte.a = 1f;
        Tintar(tinte);
    }

    protected override void Configurar(ParticleSystem sistema, ParticleSystemRenderer render)
    {
        var main = sistema.main;
        main.prewarm = true;
        main.duration = Vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Vida * 0.8f, Vida * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.01f;
        main.maxParticles = Mathf.CeilToInt(ritmoDeNoche * Vida * 1.5f) + 1;

        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 8f;
        forma.radius = 0.15f;
        forma.rotation = new Vector3(-90f, 0f, 0f);   // el cono apunta hacia arriba

        // Deriva un poco con el aire y se va abriendo al subir.
        var ruido = sistema.noise;
        ruido.enabled = true;
        ruido.strength = 0.35f;
        ruido.frequency = 0.3f;
        ruido.scrollSpeed = 0.2f;
        ruido.quality = ParticleSystemNoiseQuality.Low;

        var giro = sistema.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var crecer = sistema.sizeOverLifetime;
        crecer.enabled = true;
        crecer.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 3.5f));

        var alfa = sistema.colorOverLifetime;
        alfa.enabled = true;
        alfa.color = Fundido(opacidad, 0.15f, 0.45f);

        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sortMode = ParticleSystemSortMode.OldestInFront;
        AlCambiarLaNoche(DayNightCycle.NocheActual);
    }
}
