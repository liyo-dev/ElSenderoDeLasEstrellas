using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// Selecciona gestos disponibles y conserva la variedad entre líneas y hablantes.
public static class ActuacionAutomatica
{
    private static readonly Dictionary<string, string> UltimoPorActor = new();
    private static string _ultimoHablante;
    private static int _variacion;
    private static readonly string[] Hablar = { "Talk01", "Talk02", "Talk03" };
    private static readonly string[] Suaves = { "HeadNod01", "Question01", "Talk02", "Talk03", "Talk01" };
    private static readonly string[] Pregunta = { "Question01", "Question02" };
    private static readonly string[] Senalar = { "Senalar", "FoundSomething_NoWeapon" };
    private static readonly string[] Negar = { "HeadShake01", "HeadShake02" };
    private static readonly string[] Gracias = { "Reverence01", "HeadNod01" };
    private static readonly string[] Afirmar = { "HeadNod01" };
    private static readonly string[] Suplicar = { "Beg01" };
    private static readonly string[] Risa = { "Laugh01" };
    private static readonly string[] Saludo = { "HandWave01", "Greeting01_NoWeapon" };
    private static readonly string[] Alegria = { "Cheer01", "Cheer02" };

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UltimoPorActor.Clear();
        _ultimoHablante = null;
        _variacion = 0;
    }
#endif

    // Se clasifica una vez por línea, nunca en el bucle de actualización del bocadillo.
    private static bool Palabra(string texto, string palabra)
        => Regex.IsMatch(texto ?? "", @"(?<!\p{L})" + Regex.Escape(palabra) + @"(?!\p{L})", RegexOptions.IgnoreCase);

    public static bool EsRisa(string texto) => Palabra(texto, "ja") || Palabra(texto, "jaja") || Palabra(texto, "jajaja");
    public static bool EsPregunta(string texto) => texto != null && (texto.Contains("¿") || texto.Contains("?"));
    public static bool EsAlarma(string texto)
        => texto != null && texto.Contains("!") &&
           (Palabra(texto, "cuidado") || Palabra(texto, "peligro") || Palabra(texto, "socorro") ||
            Palabra(texto, "ayuda") || Palabra(texto, "corred") || Palabra(texto, "huye") || Palabra(texto, "no"));
    private static bool EsAfirmacion(string texto)
        => Palabra(texto, "sí") || Palabra(texto, "vale") || Palabra(texto, "claro");

    public static string[] Preparar(string texto)
    {
        string t = (texto ?? "").ToLowerInvariant();
        if (EsPregunta(t)) return Pregunta;
        if (t.Contains("!") && (Palabra(t, "mira") || Palabra(t, "eh") || Palabra(t, "allí") || Palabra(t, "ahí"))) return Senalar;
        if (Palabra(t, "no") || Palabra(t, "nunca") || Palabra(t, "jamás") || Palabra(t, "tampoco")) return Negar;
        if (Palabra(t, "gracias")) return Gracias;
        if (EsAfirmacion(t)) return Afirmar;
        if (t.Contains("por favor") || t.Contains("te lo ruego") || t.Contains("te lo pido")) return Suplicar;
        if (EsRisa(t)) return Risa;
        if (t.Contains("buenos días") || t.Contains("muy buenas") || Palabra(t, "hola")) return Saludo;
        if (t.Contains("!") && !EsAlarma(t)) return Alegria;
        return Hablar;
    }

    public static string Elegir(SequenceActor actor, string[] primeros, int indice, string anteriorActor, string anteriorHablante)
    {
        if (actor == null || actor.SosteniendoPose) return null;
        string[] opciones = indice == 0 ? primeros : indice % 2 == 0 ? Suaves : Hablar;
        string elegido = Buscar(actor, opciones, anteriorActor, anteriorHablante);
        if (elegido == null) elegido = Buscar(actor, Hablar, anteriorActor, anteriorHablante);
        if (elegido == null) elegido = Buscar(actor, Suaves, anteriorActor, anteriorHablante);
        if (elegido != null)
        {
            UltimoPorActor[actor.Id] = elegido;
            _ultimoHablante = elegido;
        }
        return elegido;
    }

    private static string Buscar(SequenceActor actor, string[] opciones, string anteriorActor, string anteriorHablante)
    {
        if (opciones == null || opciones.Length == 0) return null;
        UltimoPorActor.TryGetValue(actor.Id, out string reciente);
        int inicio = _variacion++ % opciones.Length;
        for (int i = 0; i < opciones.Length; i++)
        {
            string gesto = opciones[(inicio + i) % opciones.Length];
            if (gesto == anteriorActor || gesto == anteriorHablante || gesto == reciente || gesto == _ultimoHablante) continue;
            if (actor.TieneEstado(gesto)) return gesto;
        }
        return null;
    }

    public static void RecordarExplicito(SequenceActor actor, string gesto)
    {
        if (actor == null || string.IsNullOrEmpty(gesto)) return;
        UltimoPorActor[actor.Id] = gesto;
        _ultimoHablante = gesto;
    }

    public static void Memoria(SequenceActor actor, out string anteriorActor, out string anteriorHablante)
    {
        anteriorActor = null;
        if (actor != null) UltimoPorActor.TryGetValue(actor.Id, out anteriorActor);
        anteriorHablante = _ultimoHablante;
    }

    public static string Reaccion(string texto, ReaccionDeOyente reaccion, AnimoDeAccion animo = AnimoDeAccion.Neutral)
    {
        if (reaccion == ReaccionDeOyente.Ninguna) return null;
        if (animo == AnimoDeAccion.Amenaza || animo == AnimoDeAccion.Miedo ||
            animo == AnimoDeAccion.Triste || animo == AnimoDeAccion.Enfado || animo == AnimoDeAccion.Sorpresa)
        {
            var opciones = ReaccionesDeEscena.Gestos(animo);
            return opciones.Length > 0 ? opciones[0] : null;
        }
        if (reaccion == ReaccionDeOyente.Automatica)
        {
            if (EsPregunta(texto)) return null;
            if (EsRisa(texto)) return "Laugh01";
            if (EsAlarma(texto)) return "SenseSomethingStart_NoWeapon";
            bool negativa = Palabra(texto, "no") || Palabra(texto, "nunca") || Palabra(texto, "jamás") || Palabra(texto, "tampoco");
            return !string.IsNullOrWhiteSpace(texto) && !negativa ? "HeadNod01" : null;
        }
        return reaccion switch
        {
            ReaccionDeOyente.Asentir => "HeadNod01",
            ReaccionDeOyente.Reir => "Laugh01",
            ReaccionDeOyente.Sorpresa => "SenseSomethingStart_NoWeapon",
            ReaccionDeOyente.Aplaudir => "HandClap01",
            _ => null
        };
    }
}

public enum ReaccionDeOyente { Ninguna, Automatica, Asentir, Reir, Sorpresa, Aplaudir }
