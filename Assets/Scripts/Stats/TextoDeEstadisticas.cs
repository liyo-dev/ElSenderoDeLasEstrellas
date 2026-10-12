using System.Text;
using UnityEngine;

/// <summary>Comparte los nombres, formatos y efectos en palabras de las estadísticas entre los menús.</summary>
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

    /// Explica el efecto del valor total de ataque o defensa sobre el daño.
    public static string Efecto(TipoDeEstadistica tipo, float valor, bool corto)
    {
        string clave, defecto, numero;
        int porcentaje;
        switch (tipo)
        {
            case TipoDeEstadistica.Ataque:
                porcentaje = Mathf.RoundToInt((FormulasDeCombate.DanoConAtaque(1f, valor) - 1f) * 100f);
                numero = corto ? porcentaje.ToString("+0;-0;0") : Mathf.Abs(porcentaje).ToString();
                if (corto)
                {
                    clave = porcentaje == 0 ? "STAT_EFECTO_ATAQUE_CORTO_NORMAL" : "STAT_EFECTO_ATAQUE_CORTO";
                    defecto = porcentaje == 0 ? "Daño normal" : "Daño {0} %";
                }
                else if (porcentaje == 0)
                {
                    clave = "STAT_EFECTO_ATAQUE_NORMAL";
                    defecto = "Tus hechizos hacen su daño normal";
                }
                else
                {
                    clave = porcentaje > 0 ? "STAT_EFECTO_ATAQUE_MAS" : "STAT_EFECTO_ATAQUE_MENOS";
                    defecto = porcentaje > 0 ? "Tus hechizos hacen un {0} % más de daño" : "Tus hechizos hacen un {0} % menos de daño";
                }
                break;
            case TipoDeEstadistica.Defensa:
                porcentaje = Mathf.RoundToInt((1f - FormulasDeCombate.DanoTrasDefensa(1f, valor)) * 100f);
                numero = porcentaje.ToString();
                if (corto)
                {
                    clave = porcentaje == 0 ? "STAT_EFECTO_DEFENSA_CORTO_NORMAL" : "STAT_EFECTO_DEFENSA_CORTO";
                    defecto = porcentaje == 0 ? "Daño recibido normal" : "Daño recibido -{0} %";
                }
                else
                {
                    clave = porcentaje == 0 ? "STAT_EFECTO_DEFENSA_NORMAL" : "STAT_EFECTO_DEFENSA";
                    defecto = porcentaje == 0 ? "Recibes el daño normal" : "Recibes un {0} % menos de daño";
                }
                break;
            default:
                return "";
        }
        string formato = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, defecto) : defecto;
        return string.Format(formato, numero);
    }

    /// Explica los efectos totales de las estadísticas de combate que cambian al equiparse.
    public static string Efectos(Estadisticas antes, Estadisticas despues)
    {
        var texto = new StringBuilder();
        for (int i = (int)TipoDeEstadistica.Ataque; i <= (int)TipoDeEstadistica.Defensa; i++)
        {
            var tipo = (TipoDeEstadistica)i;
            if (Valor(antes, tipo) == Valor(despues, tipo)) continue;
            if (texto.Length > 0) texto.Append('\n');
            texto.Append(Efecto(tipo, Valor(despues, tipo), corto: false));
        }
        return texto.ToString();
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
            // Nunito no tiene la flecha U+2192 (saldría un cuadrado); se usa el signo ».
            texto.Append($"{Nombre(tipo)} {a:0} » {ConColor(b.ToString("0"), b - a)}");
        }
        return texto.ToString();
    }
}
