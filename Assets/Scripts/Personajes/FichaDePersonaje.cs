using UnityEngine;

/// Quién es un personaje del grupo: con qué estadísticas y hechizos empieza. Es la identidad del
/// personaje, independiente de quién lo mueva (el jugador o la IA). Una por personaje
/// (`Assets/_PERSONAJES/`), referenciada desde su cuerpo con el componente `Personaje`.
///
/// Lo que el personaje va ganando durante la partida (subidas de estadísticas, hechizos
/// equipados) no va aquí: esto son solo los valores de partida. Ver INC-483.
[CreateAssetMenu(menuName = "El Sendero/Personajes/Ficha de personaje", fileName = "Ficha_")]
public sealed class FichaDePersonaje : ScriptableObject
{
    [Tooltip("Qué personaje es. Mismo valor que el hueco del grupo (PartyControlManager).")]
    [SerializeField] private PartyControlManager.CharacterSlot personaje = PartyControlManager.CharacterSlot.Will;

    [Tooltip("Retrato redondo del personaje (cara y hombros), para los paneles que lo presentan: el informe de fin de batalla.")]
    [SerializeField] private Sprite retrato;

    [Tooltip("Vida y magia máximas, ataque y defensa con los que empieza.")]
    [SerializeField] private Estadisticas estadisticasIniciales = new Estadisticas(100f, 50f, EstadisticasDelPersonaje.AtaqueInicial, EstadisticasDelPersonaje.DefensaInicial);

    [Tooltip("Hechizo del botón izquierdo con el que empieza.")]
    [SerializeField] private MagicSpellSO hechizoIzquierdo;

    [Tooltip("Hechizo del botón derecho con el que empieza.")]
    [SerializeField] private MagicSpellSO hechizoDerecho;

    [Tooltip("Hechizo especial con el que empieza (lo usa la IA).")]
    [SerializeField] private MagicSpellSO hechizoEspecial;

    [Tooltip("Básicos que lleva (hasta 4, en el orden en que rotan con LB). Los usa al mando y también la IA. " +
             "Vacío = los de mano izquierda y derecha de arriba. INC-498.")]
    [SerializeField] private System.Collections.Generic.List<MagicSpellSO> basicos = new System.Collections.Generic.List<MagicSpellSO>();

    [Tooltip("Combos que conoce (se lanzan con Y cuando es el personaje activo). INC-496.")]
    [SerializeField] private System.Collections.Generic.List<MagicSpellSO> combos = new System.Collections.Generic.List<MagicSpellSO>();

    public System.Collections.Generic.IReadOnlyList<MagicSpellSO> Combos => combos;

    /// <summary>Los básicos del personaje, sin huecos (hasta 4). Si no hay lista, los de mano.</summary>
    public System.Collections.Generic.IReadOnlyList<MagicSpellSO> Basicos
    {
        get
        {
            var result = new System.Collections.Generic.List<MagicSpellSO>(4);
            if (basicos != null && basicos.Count > 0)
            {
                foreach (var s in basicos)
                    if (s != null && !result.Contains(s) && result.Count < 4) result.Add(s);
            }
            else
            {
                if (hechizoIzquierdo != null) result.Add(hechizoIzquierdo);
                if (hechizoDerecho != null && hechizoDerecho != hechizoIzquierdo) result.Add(hechizoDerecho);
            }
            return result;
        }
    }

    public PartyControlManager.CharacterSlot Personaje => personaje;
    public Sprite Retrato => retrato;

    /// La ficha de un personaje del grupo, entre las cargadas (las lleva el cuerpo de cada uno en
    /// su componente Personaje). Null si no hay ninguna cargada para ese hueco.
    public static FichaDePersonaje Buscar(PartyControlManager.CharacterSlot slot)
    {
        foreach (var f in Resources.FindObjectsOfTypeAll<FichaDePersonaje>())
            if (f != null && f.personaje == slot) return f;
        return null;
    }
    public Estadisticas EstadisticasIniciales => estadisticasIniciales;

    /// 0 = izquierdo, 1 = derecho, 2 = especial.
    public MagicSpellSO Hechizo(int indice) => indice switch
    {
        0 => hechizoIzquierdo,
        1 => hechizoDerecho,
        2 => hechizoEspecial,
        _ => null
    };
}
