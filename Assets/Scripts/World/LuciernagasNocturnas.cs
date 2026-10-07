using UnityEngine;

/// Zona de luciérnagas: puntos de luz que vagan y parpadean entre los árboles cuando es de noche.
/// La cantidad sube con la noche a partir de `desde`. Las zonas del bosque las coloca
/// «El Sendero/Mundo/Noche: luces de casas, faroles y luciérnagas»; se pueden mover, copiar o
/// redimensionar a mano. Ver INC-657.
[DisallowMultipleComponent]
public sealed class LuciernagasNocturnas : ParticulasDeAmbiente
{
    [Tooltip("Tamaño de la zona (ancho, alto, fondo). La base de la caja está en la posición del objeto.")]
    public Vector3 tamano = new Vector3(24f, 3f, 24f);

    [Tooltip("Luciérnagas a la vez a plena noche.")]
    [Range(1, 200)] public int cantidad = 35;

    [Tooltip("Parte de la noche (0-1) a partir de la que empiezan a salir.")]
    [Range(0f, 1f)] public float desde = 0.5f;

    [Tooltip("Tonos de las luciérnagas; cada una sale con uno entre los dos.")]
    public Color colorA = new Color(0.8f, 1f, 0.3f);
    public Color colorB = new Color(1f, 0.82f, 0.3f);

    [Tooltip("Multiplica el color en el material; por encima de 1 brilla con el Bloom.")]
    [Min(0f)] public float brillo = 4f;

    [Tooltip("Tamaño de cada luciérnaga en metros (mínimo y máximo).")]
    public Vector2 tamanoDeLuz = new Vector2(0.08f, 0.16f);

    private const float Vida = 9f;

    protected override Bounds Zona => new Bounds(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);

    protected override float Ritmo(float noche) => Mathf.InverseLerp(desde, 1f, noche) * cantidad / Vida;

    protected override void Configurar(ParticleSystem sistema, ParticleSystemRenderer render)
    {
        var main = sistema.main;
        main.prewarm = true;
        main.duration = Vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Vida * 0.7f, Vida * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoDeLuz.x, tamanoDeLuz.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.CeilToInt(cantidad * 1.5f);

        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = tamano;
        forma.position = new Vector3(0f, tamano.y * 0.5f + 0.3f, 0f);

        // Vuelo errático y lento, como el de una luciérnaga.
        var ruido = sistema.noise;
        ruido.enabled = true;
        ruido.strength = 0.6f;
        ruido.frequency = 0.25f;
        ruido.scrollSpeed = 0.15f;
        ruido.damping = true;
        ruido.quality = ParticleSystemNoiseQuality.Medium;

        // Se encienden y apagan varias veces en su vida; con vidas distintas no parpadean a la vez.
        var color = sistema.colorOverLifetime;
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

        render.renderMode = ParticleSystemRenderMode.Billboard;
        Color tinte = Color.white * brillo;
        tinte.a = 1f;
        Tintar(tinte);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 1f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (tamano.y * 0.5f), tamano);
    }
#endif
}
