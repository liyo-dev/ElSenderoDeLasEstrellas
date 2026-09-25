using System.Collections.Generic;
using UnityEngine;

/// Una secuencia cinemática escrita como DATOS, no como código.
///
/// Es el equivalente, para cinemáticas, de lo que NarrativeGraph.asset es para la narrativa: un
/// asset que se rellena y se reordena sin tocar C#, con la misma técnica de serialización
/// ([SerializeReference]) y por tanto con el mismo formato de texto legible en el .asset.
///
/// Reparto de trabajo con la escena: aquí va TODO lo que se puede describir con palabras (quién
/// dice qué, con qué gesto y qué cara, quién reacciona, qué VFX, en qué orden). Lo único que NO
/// cabe aquí son las posiciones del mundo — los planos de cámara y las marcas de suelo — que viven
/// en el SequenceStage de la escena y se referencian desde los beats por su nombre.
[CreateAssetMenu(menuName = "El Sendero/Secuencias/Definición de secuencia", fileName = "SEQ_NuevaSecuencia")]
public class SequenceDefinition : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("Nombre legible de la secuencia, para logs y para el Inspector.")]
    public string displayName = "Secuencia sin nombre";

    [TextArea(2, 5)]
    [Tooltip("Qué ocurre en esta escena, en una o dos frases. Para poder saber de qué va sin leerse los beats.")]
    public string summary;

    [Header("Enganche con el grafo narrativo")]
    [Tooltip("Señal que ARRANCA la secuencia. Es la que levanta el PlayCinematicNode del grafo " +
             "(su campo 'signalIn'). Si se deja vacío, el SequencePlayer usa la señal que tenga " +
             "puesta en su propio Inspector.")]
    public string signalIn;

    [Tooltip("Señal que se levanta al TERMINAR la secuencia — la que espera el grafo para seguir " +
             "(el 'signalDone' del PlayCinematicNode). Si se deja vacío, se usa la del Inspector " +
             "del SequencePlayer. OJO: sin esto el grafo se queda esperando para siempre.")]
    public string signalOut;

    [Header("Música")]
    [Tooltip("ID de regla de secuencia dentro del AudioGraphProfile (p. ej. 'OLIVER_1'). Vacío = " +
             "no cambiar la música. Al terminar se restaura sola la música de zona/escena anterior.")]
    public string musicId;

    [Header("Puesta en escena")]
    [Tooltip("Plano de cámara al que se corta DURANTE el fundido de entrada, con la pantalla ya " +
             "cubierta, para que la escena aparezca ya encuadrada en vez de verse el corte. " +
             "Nombre tal como está en el SequenceStage. Vacío = la cámara se queda donde estaba.")]
    public string openingShotName;

    [Tooltip("Plano de apertura CALCULADO, para cuando no hay ninguno colocado a mano. Se resuelve " +
             "y se aplica en el mismo punto que el anterior — con la pantalla ya cubierta — así que " +
             "la escena aparece encuadrada en vez de verse el corte. Si 'openingShotName' tiene " +
             "algo, manda aquel.")]
    public ShotFraming openingShot = new();

    [Tooltip("Marcado = al terminar, la secuencia deja la pantalla CUBIERTA en vez de descubrirla. " +
             "Se usa cuando lo que viene detrás gestiona su propia entrada — el caso real es el " +
             "Despertar de la Estrella, cuya señal de salida arranca la intro del jefe, que trae su " +
             "propio fundido: descubrir aquí pisaría el suyo y se vería un enganchón. Si no hay " +
             "nada detrás que revele, esto deja la pantalla en negro para siempre.")]
    public bool endStayBlack = false;

    [Tooltip("Prefab con los SequenceModule que necesita esta secuencia (mecánicas en C#, p. ej. el " +
             "panic input del Despertar). Solo se usa cuando la secuencia se monta en vivo desde " +
             "PlayCinematicNode: se instancia dentro del SequencePlayer y sus módulos se añaden al " +
             "escenario. Vacío = la secuencia no usa módulos.")]
    public UnityEngine.GameObject modulos;

    [Header("Contenido")]
    [Tooltip("Los tramos de la escena, en orden. Los beats de cada fase se ejecutan en orden y las " +
             "fases se encadenan: dividir en fases no cambia el resultado, solo hace la secuencia " +
             "legible y permite arrancarla por la mitad al probar.")]
    public List<SequencePhase> phases = new();

    /// Total de beats de todas las fases — para avisos y para el Inspector.
    public int TotalBeats
    {
        get
        {
            int n = 0;
            if (phases != null)
                foreach (var p in phases)
                    if (p?.beats != null) n += p.beats.Count;
            return n;
        }
    }

    /// Índice de la fase con ese nombre, o -1. Comparación sin distinguir mayúsculas ni espacios
    /// sobrantes, para que arrancar por la mitad al probar no falle por una tilde de más.
    public int IndexOfPhase(string phaseName)
    {
        if (string.IsNullOrWhiteSpace(phaseName) || phases == null) return -1;
        string wanted = phaseName.Trim();
        for (int i = 0; i < phases.Count; i++)
            if (phases[i] != null && string.Equals(phases[i].name?.Trim(), wanted,
                    System.StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}
