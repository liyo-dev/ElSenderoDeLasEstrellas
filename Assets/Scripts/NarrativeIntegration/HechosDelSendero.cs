/// Categorías de hechos que El Sendero añade al estado proyectado del grafo (el núcleo no las
/// conoce). Las escribe cada nodo del juego con SetFact y las traduce a la partida
/// PresetNarrativeStateWriter.
public static class HechosDelSendero
{
    /// Clave: Will, Estela o Liam. Valor: EnElGrupo, FueraDelGrupo, FueraDeLaEscena o AlMando.
    public const string Grupo = "Grupo";
    public const string EnElGrupo = "EnElGrupo";
    public const string FueraDelGrupo = "FueraDelGrupo";
    public const string FueraDeLaEscena = "FueraDeLaEscena";
    public const string AlMando = "AlMando";

    /// Clave: Actual. Valor: DayNightCycle.TimeOfDay.
    public const string HoraDelDia = "HoraDelDia";
    public const string Actual = "Actual";

    /// Clave: id de la batalla.
    public const string BatallaGanada = "BatallaGanada";

    /// Clave: id de la prenda.
    public const string Vestuario = "Vestuario";

    /// Clave: NPC que recibe el objeto llevado en brazos. Valor: tipo de objeto.
    public const string Entrega = "Entrega";

    public const string Si = "Si";
}
