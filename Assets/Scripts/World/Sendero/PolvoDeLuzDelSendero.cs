using UnityEngine;

/// <summary>
/// Polvo de luz que flota alrededor del camino del Sendero y motas que caen por los bordes hacia el
/// vacío. No depende de la noche: en el Sendero siempre es la misma hora. Lejos se apaga
/// (ParticulasDeAmbiente).
/// </summary>
[DisallowMultipleComponent]
public sealed class PolvoDeLuzDelSendero : ParticulasDeAmbiente
{
    [Tooltip("Tamaño de la zona (ancho, alto, fondo); la base de la caja está en el objeto.")]
    public Vector3 tamano = new(14f, 6f, 60f);
    [Range(1, 200)] public int cantidad = 45;
    [Tooltip("Velocidad vertical: positiva sube (polvo), negativa cae (motas del borde).")]
    public float subida = 0.25f;
    public Color colorA = new(1f, 0.85f, 0.5f);
    public Color colorB = new(0.6f, 0.8f, 1f);
    [Min(0f)] public float brillo = 3f;
    public Vector2 tamanoDeMota = new(0.04f, 0.1f);

    private const float Vida = 8f;

    protected override Bounds Zona => new(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);

    protected override float Ritmo(float noche) => cantidad / Vida;

    protected override void Configurar(ParticleSystem sistema, ParticleSystemRenderer render)
    {
        var main = sistema.main;
        main.prewarm = true;
        main.duration = Vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Vida * 0.7f, Vida * 1.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoDeMota.x, tamanoDeMota.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = Mathf.CeilToInt(cantidad * 1.5f);

        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano;
        forma.position = new Vector3(0f, tamano.y * 0.5f, 0f);

        var vel = sistema.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        vel.y = new ParticleSystem.MinMaxCurve(subida * 0.6f, subida * 1.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        var color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = Fundido(1f, 0.2f, 0.75f);

        render.renderMode = ParticleSystemRenderMode.Billboard;
        Color tinte = Color.white * brillo;
        tinte.a = 1f;
        Tintar(tinte);
    }
}
