using System.Text;

/// <summary>Comparte los nombres y formatos de estadísticas entre los menús.</summary>
public static class TextoDeEstadisticas
{
    public const string Verde = "#7CE38B";
    public const string Rojo = "#EF7777";

    public static string Nombre(TipoDeEstadistica tipo)
    {
        string clave = tipo switch
        {
            TipoDeEstadistica.Vida => "STAT_VIDA",
            TipoDeEstadistica.Magia => "STAT_MAGIA",
            TipoDeEstadistica.Ataque => "STAT_ATAQUE",
            _ => "STAT_DEFENSA"
        };
        string defecto = tipo switch
        {
            TipoDeEstadistica.Vida => "Vida",
            TipoDeEstadistica.Magia => "Magia",
            TipoDeEstadistica.Ataque => "Ataque",
            _ => "Defensa"
        };
        return LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, defecto) : defecto;
    }

    public static float Valor(Estadisticas valores, TipoDeEstadistica tipo) => tipo switch
    {
        TipoDeEstadistica.Vida => valores.vida,
        TipoDeEstadistica.Magia => valores.magia,
        TipoDeEstadistica.Ataque => valores.ataque,
        _ => valores.defensa
    };

    public static string ConColor(string texto, float cambio) =>
        $"<color={(cambio > 0f ? Verde : Rojo)}>{texto}</color>";

    public static string Bono(TipoDeEstadistica tipo, float valor) => valor == 0f ? "" :
        ConColor($"{Nombre(tipo)} {valor:+0;-0;0}", valor);

    public static string Bonos(Estadisticas valores)
    {
        var texto = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            var tipo = (TipoDeEstadistica)i;
            float valor = Valor(valores, tipo);
            if (valor == 0f) continue;
            if (texto.Length > 0) texto.Append("  ");
            texto.Append(Bono(tipo, valor));
        }
        return texto.ToString();
    }

    public static string Comparacion(Estadisticas antes, Estadisticas despues)
    {
        var texto = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            var tipo = (TipoDeEstadistica)i;
            float a = Valor(antes, tipo), b = Valor(despues, tipo);
            if (a == b) continue;
            if (texto.Length > 0) texto.Append('\n');
            texto.Append($"{Nombre(tipo)} {a:0} → {ConColor(b.ToString("0"), b - a)}");
        }
        return texto.ToString();
    }
}