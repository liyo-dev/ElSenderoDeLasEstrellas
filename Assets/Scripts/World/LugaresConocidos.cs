/// <summary>
/// Los lugares con nombre que el jugador ya ha pisado en esta partida. Viven como marcas de la
/// partida (<see cref="FlagDePartida"/>), así que se guardan y se cargan con ella; la partida
/// nueva los olvida. La clave de un lugar es la de su nombre en los textos del juego.
/// </summary>
public static class LugaresConocidos
{
    public const string Prefijo = "LUGAR_CONOCIDO:";

    public static bool EsConocido(string claveDelLugar)
        => !string.IsNullOrEmpty(claveDelLugar) && FlagDePartida.EstaPuesto(Prefijo + claveDelLugar);

    /// <summary>Lo apunta como conocido. False si ya lo era o si no hay partida donde apuntarlo.</summary>
    public static bool Marcar(string claveDelLugar)
        => !string.IsNullOrEmpty(claveDelLugar) && FlagDePartida.Marcar(Prefijo + claveDelLugar);

    /// <summary>Olvida todos los lugares conocidos. Devuelve cuántos había.</summary>
    public static int OlvidarTodos() => FlagDePartida.BorrarConPrefijo(Prefijo);
}
