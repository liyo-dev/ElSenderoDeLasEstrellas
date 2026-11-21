using UnityEngine;

/// Niebla que se arrastra pegada al suelo alrededor de un punto, con una luz que brilla debajo (la
/// llegada de alguien con magia, un círculo de un ritual, una cripta). A diferencia de la niebla
/// nocturna del bosque no depende de la hora ni de que el jugador esté cerca: aparece cuando se le
/// pide con <see cref="Encender"/> y se va con <see cref="Apagar"/>. La pone una cinemática con
/// <c>NieblaDeSueloBeat</c> o un nivel a mano. Ver INC-1341.
[DisallowMultipleComponent]
public sealed class NieblaDeSuelo : ParticulasDeAmbiente
{
    [Tooltip("Radio (m) del círculo de niebla alrededor del objeto.")]
    [Min(0.5f)] public float radio = 6f;

    [Tooltip("Nubes de niebla a la vez con la niebla entera.")]
    [Range(1, 120)] public int cantidad = 40;

    [Tooltip("Color de la niebla.")]
    public Color color = new Color(0.62f, 0.55f, 0.85f);

    [Tooltip("Opacidad máxima de cada nube (0-1); muchas superpuestas se suman.")]
    [Range(0f, 1f)] public float opacidad = 0.3f;

    [Tooltip("Tamaño de cada nube en metros (mínimo y máximo).")]
    public Vector2 tamanoDeNube = new Vector2(2.5f, 4.5f);

    [Tooltip("Color de la luz que brilla debajo de la niebla. Alfa 0 = sin luz.")]
    public Color colorDeLuz = new Color(0.65f, 0.45f, 1f, 1f);

    [Tooltip("Intensidad de la luz de debajo con la niebla entera.")]
    [Min(0f)] public float intensidadDeLuz = 3f;

    [Tooltip("Cuánto tiñen la luz de debajo las nubes que están más cerca del centro (0 = nada).")]
    [Range(0f, 1f)] public float tinteDeLaLuz = 0.5f;

    [Tooltip("Si tiene algo que seguir (un personaje), la niebla va con él por el suelo.")]
    public Transform seguir;

    private const float Vida = 9f;
    private float _fuerza, _objetivo, _velocidad;
    private bool _destruirAlApagar;
    private Light _luz;

    protected override bool ActivaSinJugador => true;
    protected override Bounds Zona => new Bounds(transform.position, new Vector3(radio * 2f, 2f, radio * 2f));
    protected override float Ritmo(float noche) => _fuerza > 0.01f ? _fuerza * cantidad / Vida : 0f;

    /// Aparece en <paramref name="segundos"/>.
    public void Encender(float segundos)
    {
        _objetivo = 1f;
        _destruirAlApagar = false;
        _velocidad = 1f / Mathf.Max(0.01f, segundos);
    }

    /// Se va en <paramref name="segundos"/>; con <paramref name="destruir"/>, el objeto se borra al acabar.
    public void Apagar(float segundos, bool destruir)
    {
        _objetivo = 0f;
        _destruirAlApagar = destruir;
        _velocidad = 1f / Mathf.Max(0.01f, segundos);
    }

    protected override void Configurar(ParticleSystem sistema, ParticleSystemRenderer render)
    {
        var main = sistema.main;
        main.duration = Vida;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Vida * 0.7f, Vida * 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoDeNube.x, tamanoDeNube.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var tinte = Color.Lerp(color, new Color(colorDeLuz.r, colorDeLuz.g, colorDeLuz.b), tinteDeLaLuz * colorDeLuz.a);
        main.startColor = new ParticleSystem.MinMaxGradient(color, tinte);
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.CeilToInt(cantidad * 1.6f);

        // Círculo tumbado en el suelo, con las nubes algo levantadas para que no corten con él.
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = radio;
        forma.radiusThickness = 1f;
        forma.rotation = new Vector3(90f, 0f, 0f);
        forma.position = new Vector3(0f, 0.35f, 0f);
        forma.randomDirectionAmount = 0.3f;

        var giro = sistema.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

        var crecer = sistema.sizeOverLifetime;
        crecer.enabled = true;
        crecer.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));

        var alfa = sistema.colorOverLifetime;
        alfa.enabled = true;
        alfa.color = Fundido(opacidad, 0.25f, 0.7f);

        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sortMode = ParticleSystemSortMode.Distance;
        Tintar(new Color(1f, 1f, 1f, 0f));

        if (colorDeLuz.a > 0f && intensidadDeLuz > 0f)
        {
            var go = new GameObject("Luz de debajo");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            _luz = go.AddComponent<Light>();
            _luz.type = LightType.Point;
            _luz.color = new Color(colorDeLuz.r, colorDeLuz.g, colorDeLuz.b);
            _luz.range = radio * 1.4f;
            _luz.intensity = 0f;
            _luz.shadows = LightShadows.None;
        }
    }

    private void LateUpdate()
    {
        if (seguir != null) transform.position = EnElSuelo(seguir.position);
        if (Mathf.Approximately(_fuerza, _objetivo))
        {
            if (_objetivo <= 0f && _destruirAlApagar && Sistema.particleCount == 0) Destroy(gameObject);
            return;
        }
        _fuerza = Mathf.MoveTowards(_fuerza, _objetivo, _velocidad * Time.deltaTime);
        Tintar(new Color(1f, 1f, 1f, _fuerza));
        if (_luz != null) _luz.intensity = intensidadDeLuz * _fuerza;
    }

    /// El punto del suelo que hay bajo <paramref name="punto"/> (o el propio punto si no hay suelo).
    public static Vector3 EnElSuelo(Vector3 punto)
    {
        int suelo = LayerMask.GetMask("Floor", "Default");
        return Physics.Raycast(punto + Vector3.up * 3f, Vector3.down, out var golpe, 60f, suelo, QueryTriggerInteraction.Ignore)
            ? golpe.point : punto;
    }

    /// Crea una niebla de suelo apagada en <paramref name="punto"/> (al suelo); se configura y se enciende después.
    public static NieblaDeSuelo Crear(Vector3 punto, System.Action<NieblaDeSuelo> configurar)
    {
        var go = new GameObject("[Niebla de suelo]");
        go.SetActive(false);
        go.transform.position = EnElSuelo(punto);
        var niebla = go.AddComponent<NieblaDeSuelo>();
        configurar?.Invoke(niebla);
        go.SetActive(true);
        return niebla;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(colorDeLuz.r, colorDeLuz.g, colorDeLuz.b, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radio);
    }
#endif
}
