/// <summary>
/// Lo que el jugador ya ha descubierto de cada criatura en esta partida: qué elemento le duele más
/// y cuál resiste. Vive como marcas de la partida (<see cref="FlagDePartida"/>): se guarda y se
/// carga con ella y la partida nueva lo olvida. Lo apunta <see cref="AfinidadElemental"/> la
/// primera vez que un golpe entra con debilidad o resistencia; lo lee el Grimorio.
/// La clave de una criatura es la de su nombre en los textos del juego.
/// </summary>
public static class CriaturasConocidas
{
    public const string PrefijoDebil = "CRIATURA_DEBIL:";
    public const string PrefijoResiste = "CRIATURA_RESISTE:";

    /// Un golpe con esta afinidad se ha visto ya en esta criatura.
    public static bool Conocida(string claveDeCriatura, MagicElement elemento, AfinidadAlElemento afinidad)
    {
        string clave = Clave(claveDeCriatura, elemento, afinidad);
        return clave != null && FlagDePartida.EstaPuesto(clave);
    }

    /// Se ha descubierto algo de esta criatura (alguna debilidad o resistencia).
    public static bool AlgoConocido(string claveDeCriatura, MagicElement[] debilidades, MagicElement[] resistencias)
    {
        if (debilidades != null)
            foreach (var e in debilidades) if (Conocida(claveDeCriatura, e, AfinidadAlElemento.Debil)) return true;
        if (resistencias != null)
            foreach (var e in resistencias) if (Conocida(claveDeCriatura, e, AfinidadAlElemento.Resiste)) return true;
        return false;
    }

    /// <summary>Lo apunta. True si es nuevo (no lo era y había partida donde apuntarlo).</summary>
    public static bool Apuntar(string claveDeCriatura, MagicElement elemento, AfinidadAlElemento afinidad)
    {
        string clave = Clave(claveDeCriatura, elemento, afinidad);
        return clave != null && FlagDePartida.Marcar(clave);
    }

    /// <summary>Olvida todo lo descubierto. Devuelve cuántas marcas había.</summary>
    public static int OlvidarTodo() => FlagDePartida.BorrarConPrefijo(PrefijoDebil) + FlagDePartida.BorrarConPrefijo(PrefijoResiste);

    private static string Clave(string criatura, MagicElement elemento, AfinidadAlElemento afinidad)
    {
        if (string.IsNullOrEmpty(criatura) || afinidad == AfinidadAlElemento.Normal) return null;
        return (afinidad == AfinidadAlElemento.Debil ? PrefijoDebil : PrefijoResiste) + criatura + ":" + elemento;
    }
}
