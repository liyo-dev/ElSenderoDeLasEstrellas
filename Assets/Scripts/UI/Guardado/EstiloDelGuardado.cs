using UnityEngine;

/// <summary>
/// Cómo se ve y cuánto dura el símbolo de guardado: el indicador de la esquina
/// (<see cref="IndicadorDeGuardado"/>), el propio símbolo (<see cref="SimboloDeGuardado"/>) y el
/// aviso que lo explica al arrancar (<see cref="AvisoDeGuardadoAlArrancar"/>). Vive en Resources
/// porque las tres piezas se montan por código; si falta, se usan los valores por defecto.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/UI/Estilo del guardado", fileName = "EstiloDelGuardado")]
public sealed class EstiloDelGuardado : ScriptableObject
{
    public const string RutaEnResources = "UI/EstiloDelGuardado";

    public enum Esquina { AbajoDerecha, ArribaDerecha, AbajoIzquierda, ArribaIzquierda }

    [Tooltip("Fuentes de los menús de papel (el aviso usa la del texto y la de los títulos).")]
    public EstiloDeMenuSencillo estilo;

    [Header("Indicador de la esquina")]
    [Tooltip("Segundos que se ve como mínimo, aunque el guardado sea instantáneo.")]
    [Min(0f)] public float segundosMinimos = 1.5f;
    [Tooltip("Segundos del fundido de entrada.")]
    [Min(0.01f)] public float fundidoDeEntrada = 0.25f;
    [Tooltip("Segundos del fundido de salida.")]
    [Min(0.01f)] public float fundidoDeSalida = 0.45f;
    [Tooltip("Esquina de la pantalla donde sale.")]
    public Esquina esquina = Esquina.AbajoDerecha;
    [Tooltip("Separación del borde de la pantalla (unidades del lienzo de 1920×1080).")]
    public Vector2 margen = new Vector2(56f, 52f);
    [Tooltip("Lado del símbolo en la esquina (unidades del lienzo de 1920×1080).")]
    [Min(16f)] public float lado = 84f;

    [Header("Símbolo")]
    [Tooltip("Grados por segundo que gira la estrella.")]
    public float gradosPorSegundo = 120f;
    [Tooltip("Latidos por segundo de la estrella y su halo.")]
    [Min(0f)] public float latidosPorSegundo = 1.2f;
    [Tooltip("Cuánto crece la estrella en cada latido (fracción de su tamaño).")]
    [Range(0f, 0.4f)] public float latido = 0.1f;

    [Header("Aviso al arrancar")]
    [Tooltip("Segundos que se ve el aviso si no se pasa con A.")]
    [Min(0.5f)] public float segundosDelAviso = 4f;
    [Tooltip("Segundos antes de que A deje pasar el aviso (para que no se lo coma una pulsación anterior).")]
    [Min(0f)] public float segundosAntesDePoderPasar = 0.6f;
    [Tooltip("Segundos del fundido de entrada y de salida del aviso.")]
    [Min(0.01f)] public float fundidoDelAviso = 0.4f;
    [Tooltip("Clave de localización del texto del aviso.")]
    public string claveDelTexto = "AVISO_AUTOGUARDADO";
    [Tooltip("Clave de localización de la ayuda del botón A.")]
    public string claveDePasar = "AVISO_AUTOGUARDADO_PASAR";

    private static EstiloDelGuardado s_cargado;

    /// <summary>El de Resources, o uno con los valores por defecto si no está montado.</summary>
    public static EstiloDelGuardado Cargado
    {
        get
        {
            if (s_cargado != null) return s_cargado;
            s_cargado = Resources.Load<EstiloDelGuardado>(RutaEnResources);
            if (s_cargado == null)
            {
                s_cargado = CreateInstance<EstiloDelGuardado>();
                s_cargado.hideFlags = HideFlags.DontSave;
            }
            return s_cargado;
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s_cargado = null; }
#endif
}
