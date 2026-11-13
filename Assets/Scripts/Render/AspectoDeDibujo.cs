using UnityEngine;

/// Los ajustes del aspecto de dibujo animado de todo el juego: cómo es la sombra, dónde cae su
/// borde, cuánto tiñe la luz y cómo brilla el pelo. El shader «El Sendero/Dibujo» los lee como
/// valores globales, así que un cambio aquí se ve en todos los personajes y en todo el mundo.
/// El asset vive en Resources para aplicarse al arrancar el juego. Ver INC-1042.
[CreateAssetMenu(menuName = "El Sendero/Aspecto de dibujo", fileName = "AspectoDeDibujo")]
public sealed class AspectoDeDibujo : ScriptableObject
{
    public const string RutaEnResources = "Dibujo/AspectoDeDibujo";

    [Header("Sombra")]
    [Tooltip("Color por el que se multiplica el tono iluminado para dar el tono de sombra. Más claro = sombra más suave; con algo de violeta o rosa la sombra tiene color en vez de ensuciar.")]
    public Color tinteDeSombra = new Color(0.72f, 0.62f, 0.80f, 1f);

    [Tooltip("Tinte de la sombra en los colores cálidos (piel, pelo rubio o castaño, madera, rojos): con el violeta de arriba la piel en sombra sale lila. Los colores fríos siguen con el otro.")]
    public Color tinteDeSombraCalida = new Color(0.84f, 0.66f, 0.66f, 1f);

    [Tooltip("Dónde cae el borde entre luz y sombra. 0 = justo donde la superficie deja de mirar a la luz; más alto = más sombra.")]
    [Range(-0.5f, 0.5f)] public float umbral = 0.05f;

    [Tooltip("Ancho del borde entre luz y sombra. Muy bajo = borde duro de dibujo.")]
    [Range(0.001f, 0.3f)] public float suavidad = 0.02f;

    [Tooltip("Viveza del color en la sombra. 1 = la del tinte; más alto = sombra más viva.")]
    [Range(0f, 2f)] public float saturacionDeSombra = 1.15f;

    [Header("Luz")]
    [Tooltip("Cuánto tiñe el color de la luz (el sol amarillo del atardecer, la luna azul). 0 = la luz solo aclara u oscurece y los colores quedan limpios.")]
    [Range(0f, 1f)] public float tinteDeLaLuz = 0.35f;

    [Tooltip("Luz máxima sobre una superficie. Con 1, la parte iluminada sale del color de la pieza a pleno día; más alto deja que el sol la aclare (y la lave).")]
    [Range(0.5f, 3f)] public float topeDeLuz = 1f;

    [Tooltip("Peso de la luz ambiente.")]
    [Range(0f, 2f)] public float ambiente = 1f;

    [Tooltip("Peso de las luces puntuales y los focos (antorchas, hechizos).")]
    [Range(0f, 2f)] public float lucesPuntuales = 1f;

    [Tooltip("Cuánto oscurece la oclusión ambiental (SSAO) los rincones. Bajo = colores más planos.")]
    [Range(0f, 1f)] public float oclusion = 0.4f;

    [Header("Brillo del pelo")]
    [Tooltip("Tamaño del brillo en los materiales que lo llevan (campo «Brillo de pelo»). Más bajo = brillo más grande.")]
    [Range(0.5f, 1f)] public float umbralDelBrillo = 0.92f;

    [Tooltip("Cuánto aclara el brillo.")]
    [Range(0f, 1f)] public float fuerzaDelBrillo = 0.35f;

    [Header("Suelo")]
    [Tooltip("Cuánto se ve el dibujo de las texturas del terreno. 0 = cada capa de un color plano (su color medio); 1 = la textura entera.")]
    [Range(0f, 1f)] public float texturaDelSuelo = 0.35f;

    [Header("Pinceladas")]
    [Tooltip("Cuánto se notan las pinceladas en los modelos propios de color liso (los que llevan «Pinceladas» en el material). 0 = color liso.")]
    [Range(0f, 0.4f)] public float pinceladas = 0.12f;

    [Tooltip("Tamaño de cada mancha de pincel, en metros.")]
    [Range(0.1f, 3f)] public float tamanoDeLasPinceladas = 0.6f;

    private static readonly int IdActivo = Shader.PropertyToID("_DibujoActivo");
    private static readonly int IdSombra = Shader.PropertyToID("_DibujoSombra");
    private static readonly int IdSombraCalida = Shader.PropertyToID("_DibujoSombraCalida");
    private static readonly int IdUmbral = Shader.PropertyToID("_DibujoUmbral");
    private static readonly int IdSuavidad = Shader.PropertyToID("_DibujoSuavidad");
    private static readonly int IdSaturacionSombra = Shader.PropertyToID("_DibujoSaturacionSombra");
    private static readonly int IdAmbiente = Shader.PropertyToID("_DibujoAmbiente");
    private static readonly int IdTinteDeLuz = Shader.PropertyToID("_DibujoTinteDeLuz");
    private static readonly int IdOclusion = Shader.PropertyToID("_DibujoOclusion");
    private static readonly int IdBrilloUmbral = Shader.PropertyToID("_DibujoBrilloUmbral");
    private static readonly int IdBrilloFuerza = Shader.PropertyToID("_DibujoBrilloFuerza");
    private static readonly int IdLucesExtra = Shader.PropertyToID("_DibujoLucesExtra");
    private static readonly int IdTopeDeLuz = Shader.PropertyToID("_DibujoTopeDeLuz");
    private static readonly int IdTexturaDelSuelo = Shader.PropertyToID("_DibujoTexturaDelSuelo");
    private static readonly int IdPincel = Shader.PropertyToID("_DibujoPincel");
    private static readonly int IdPincelEscala = Shader.PropertyToID("_DibujoPincelEscala");

    /// Pone los ajustes como valores globales de shader.
    public void Aplicar()
    {
        // El shader trabaja en lineal: el tinte se convierte aquí porque un valor global no pasa por
        // la conversión que Unity hace con los colores de un material.
        Color sombra = QualitySettings.activeColorSpace == ColorSpace.Linear ? tinteDeSombra.linear : tinteDeSombra;
        Shader.SetGlobalVector(IdSombra, new Vector4(sombra.r, sombra.g, sombra.b, 1f));
        Color calida = QualitySettings.activeColorSpace == ColorSpace.Linear ? tinteDeSombraCalida.linear : tinteDeSombraCalida;
        Shader.SetGlobalVector(IdSombraCalida, new Vector4(calida.r, calida.g, calida.b, 1f));
        Shader.SetGlobalFloat(IdUmbral, umbral);
        Shader.SetGlobalFloat(IdSuavidad, suavidad);
        Shader.SetGlobalFloat(IdSaturacionSombra, saturacionDeSombra);
        Shader.SetGlobalFloat(IdAmbiente, ambiente);
        Shader.SetGlobalFloat(IdTinteDeLuz, tinteDeLaLuz);
        Shader.SetGlobalFloat(IdOclusion, oclusion);
        Shader.SetGlobalFloat(IdBrilloUmbral, umbralDelBrillo);
        Shader.SetGlobalFloat(IdBrilloFuerza, fuerzaDelBrillo);
        Shader.SetGlobalFloat(IdLucesExtra, lucesPuntuales);
        Shader.SetGlobalFloat(IdTopeDeLuz, topeDeLuz);
        Shader.SetGlobalFloat(IdTexturaDelSuelo, texturaDelSuelo);
        Shader.SetGlobalFloat(IdPincel, pinceladas);
        Shader.SetGlobalFloat(IdPincelEscala, tamanoDeLasPinceladas);
        Shader.SetGlobalFloat(IdActivo, 1f);
    }

    /// Carga el asset de Resources y lo aplica. Devuelve si lo había.
    public static bool AplicarElDelJuego()
    {
        var aspecto = Resources.Load<AspectoDeDibujo>(RutaEnResources);
        if (aspecto == null) return false;
        aspecto.Aplicar();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AlArrancar() => AplicarElDelJuego();

    private void OnValidate() => Aplicar();
}
