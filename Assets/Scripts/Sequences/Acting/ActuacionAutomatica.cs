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
            if (EsPregunta(texto)) return "Question01";
            if (EsRisa(texto)) return "Laugh01";
            if (EsAlarma(texto)) return "SenseSomethingStart_NoWeapon";
            bool negativa = Palabra(texto, "no") || Palabra(texto, "nunca") || Palabra(texto, "jamás") ||
                            Palabra(texto, "tampoco") || Palabra(texto, "imposible");
            if (negativa) return "HeadShake01";
            return !string.IsNullOrWhiteSpace(texto) ? "HeadNod01" : null;
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

    /// <summary>
    /// Devuelve la emoción facial que corresponde al oyente según el texto y la actitud del hablante.
    /// </summary>
    public static NPCEmotion ReaccionCara(string texto, NPCEmotion emocionHablante = NPCEmotion.None, AnimoDeAccion animo = AnimoDeAccion.Neutral)
    {
        if (animo != AnimoDeAccion.Neutral && animo != AnimoDeAccion.PorAccion)
            return ReaccionesDeEscena.Cara(animo);

        if (emocionHablante == NPCEmotion.Happy || EsRisa(texto)) return NPCEmotion.Happy;
        if (emocionHablante == NPCEmotion.Angry) return NPCEmotion.Surprised;
        if (emocionHablante == NPCEmotion.Sad) return NPCEmotion.Sad;
        if (emocionHablante == NPCEmotion.Scared || EsAlarma(texto)) return NPCEmotion.Surprised;
        if (EsPregunta(texto)) return NPCEmotion.Surprised;

        return NPCEmotion.Neutral;
    }

    /// <summary>
    /// Evalúa la reacción corporal y facial de un oyente equilibrando expresividad y cadencia natural.
    /// Si el oyente ya reaccionó en la frase anterior y la frase actual es informativa normal, devuelve false
    /// para que permanezca escuchando con atención en reposo ("en algún momento si pega que esté idle, no siempre").
    /// </summary>
    public static bool CalcularReaccionOyente(
        string texto,
        NPCEmotion emocionHablante,
        int lineasDesdeUltimaReaccion,
        string anteriorGesto,
        out string gesto,
        out NPCEmotion cara)
    {
        gesto = null;
        cara = NPCEmotion.None;

        bool esPregunta = EsPregunta(texto);
        bool esAlarma = EsAlarma(texto);
        bool esRisa = EsRisa(texto) || emocionHablante == NPCEmotion.Happy;
        bool esNegativa = Palabra(texto, "no") || Palabra(texto, "nunca") || Palabra(texto, "jamás") ||
                          Palabra(texto, "tampoco") || Palabra(texto, "imposible");

        // Casos de reacción enfática inmediata: siempre disparan
        if (esPregunta)
        {
            gesto = anteriorGesto == "Question01" ? "Question02" : "Question01";
            cara = NPCEmotion.Surprised;
            return true;
        }
        if (esAlarma)
        {
            gesto = "SenseSomethingStart_NoWeapon";
            cara = NPCEmotion.Surprised;
            return true;
        }
        if (esRisa)
        {
            gesto = "Laugh01";
            cara = NPCEmotion.Happy;
            return true;
        }
        if (esNegativa)
        {
            gesto = anteriorGesto == "HeadShake01" ? "HeadShake02" : "HeadShake01";
            cara = NPCEmotion.Neutral;
            return true;
        }
        if (emocionHablante == NPCEmotion.Angry)
        {
            gesto = "HeadShake01";
            cara = NPCEmotion.Surprised;
            return true;
        }
        if (emocionHablante == NPCEmotion.Sad)
        {
            gesto = "HeadNod01";
            cara = NPCEmotion.Sad;
            return true;
        }

        // Frases normales o explicativas: respetar cadencia natural.
        // Si el personaje acaba de reaccionar en la línea previa, escucha en idle atento sin nuevo gesto
        if (lineasDesdeUltimaReaccion == 0)
        {
            cara = NPCEmotion.Neutral;
            return false;
        }

        // Si ha habido margen de escucha previa, asiente con la cabeza
        gesto = "HeadNod01";
        cara = NPCEmotion.Neutral;
        return true;
    }
}

public enum ReaccionDeOyente { Ninguna, Automatica, Asentir, Reir, Sorpresa, Aplaudir }
