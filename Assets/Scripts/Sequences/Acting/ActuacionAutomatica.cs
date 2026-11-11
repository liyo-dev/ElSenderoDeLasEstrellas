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

    // Lo que pide la frase (INC-1068). El primero que el actor tenga; los demás son el plan B.
    private static readonly string[] Ninguno = new string[0];
    private static readonly string[] Aviso = { "Aviso01", "SenseSomethingStart_NoWeapon" };
    private static readonly string[] Alto = { "Alto01", "HeadShake01" };
    private static readonly string[] Sobresalto = { "Sobresalto01", "SenseSomethingStart_NoWeapon" };
    private static readonly string[] Grito = { "Grito01", "Angry02" };
    private static readonly string[] NegarFuerte = { "NegarConManos01", "HeadShake01", "HeadShake02" };
    private static readonly string[] Decidido = { "Decidido01", "HeadNod01" };
    private static readonly string[] Dudar = { "Dudar01", "Question02" };
    private static readonly string[] Suspiro = { "Suspiro01" };
    private static readonly string[] Hombros = { "Shrug01" };
    private static readonly string[] Miedo = { "Fear01" };
    private static readonly string[] Enfado = { "Angry01", "Angry02" };

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
            Palabra(texto, "ayuda") || Palabra(texto, "corred") || Palabra(texto, "huye") || Palabra(texto, "no") ||
            Palabra(texto, "apártate") || Palabra(texto, "apartaos") || Palabra(texto, "atrás") || Palabra(texto, "huid"));
    private static bool EsAfirmacion(string texto)
        => Palabra(texto, "sí") || Palabra(texto, "vale") || Palabra(texto, "claro");

    private static bool SinPalabras(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return true;
        foreach (char c in texto) if (char.IsLetter(c)) return false;
        return true;
    }

    /// Dicho a voces: con signos dobles o casi todo en mayúsculas («¡¡WIIILL!!», «¡NOO!»).
    private static bool EsGrito(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return false;
        if (texto.Contains("¡¡") || texto.Contains("!!")) return true;
        int letras = 0, mayusculas = 0;
        foreach (char c in texto)
        {
            if (!char.IsLetter(c)) continue;
            letras++;
            if (char.IsUpper(c)) mayusculas++;
        }
        return letras >= 3 && mayusculas * 10 >= letras * 7;
    }

    /// Alguna de las palabras o expresiones (las de varias palabras, tal cual; las sueltas, enteras).
    private static bool Alguna(string texto, params string[] expresiones)
    {
        foreach (string e in expresiones)
            if (e.IndexOf(' ') >= 0 ? texto.Contains(e) : Palabra(texto, e)) return true;
        return false;
    }

    /// La frase empieza por una de esas palabras o expresiones.
    private static bool Empieza(string texto, params string[] expresiones)
    {
        string inicio = texto.TrimStart('¡', '¿', ' ', '.', '…', '—', '-', '«', '"', '\'');
        foreach (string e in expresiones)
            if (inicio.StartsWith(e, System.StringComparison.Ordinal) &&
                (inicio.Length == e.Length || !char.IsLetter(inicio[e.Length]))) return true;
        return false;
    }

    /// Qué gesto pide una frase por lo que dice y por la cara con que se dice. Devuelve las opciones por
    /// orden de preferencia (se usa la primera que el actor tenga); una lista vacía si la frase no lleva
    /// gesto (un silencio: «...»); null si no pide nada en particular y vale un gesto de charla.
    /// Va de lo más urgente a lo más suave: un aviso manda sobre una pregunta, y la cara solo decide
    /// cuando el texto no dice nada.
    public static string[] ParaDecir(string texto, NPCEmotion cara = NPCEmotion.None)
    {
        string limpio = Regex.Replace(texto ?? "", "<[^>]+>", " ");
        string t = limpio.ToLowerInvariant();
        bool exclama = t.Contains("!");

        if (SinPalabras(t)) return exclama ? Sobresalto : Ninguno;
        if (exclama && Alguna(t, "cuidado", "peligro", "socorro", "corred", "corre", "huid", "huye", "apártate",
                                 "apartaos", "atrás", "al suelo", "agáchate", "agachaos")) return Aviso;
        if (exclama && Empieza(t, "espera", "esperad", "alto", "para", "parad", "quieto", "quieta", "quietos",
                                  "detente", "deteneos", "basta", "un momento")) return Alto;
        if (EsRisa(t)) return Risa;
        if (EsGrito(limpio)) return Grito;
        if (EsPregunta(t)) return Pregunta;
        if (exclama && Alguna(t, "mira", "mirad", "allí", "ahí")) return Senalar;
        if (Alguna(t, "gracias")) return Gracias;
        if (Alguna(t, "por favor", "te lo ruego", "te lo pido", "os lo ruego", "os lo pido")) return Suplicar;
        if (Alguna(t, "qué le vamos a hacer", "qué remedio", "ni idea", "yo qué sé", "quién sabe", "qué más da")) return Hombros;
        if (Empieza(t, "mmm", "hmm", "no sé", "no lo sé", "quizá", "quizás", "supongo", "a ver", "veamos")) return Dudar;
        if (Empieza(t, "no") || Alguna(t, "nunca", "jamás", "ni hablar", "de eso nada", "de ninguna manera"))
            return exclama ? NegarFuerte : Negar;
        if (exclama && (Empieza(t, "vamos", "adelante", "a por", "hecho", "prometido") ||
                        Alguna(t, "lo haré", "lo haremos", "lo conseguiremos", "puedo hacerlo"))) return Decidido;
        if (Empieza(t, "sí", "vale", "claro", "de acuerdo", "hecho", "prometido")) return Afirmar;
        if (Empieza(t, "uf", "ay", "en fin", "menos mal", "por fin")) return Suspiro;
        if (t.Contains("buenos días") || t.Contains("muy buenas") || Empieza(t, "hola")) return Saludo;

        switch (cara)
        {
            case NPCEmotion.Scared: return Miedo;
            case NPCEmotion.Angry: return Enfado;
            case NPCEmotion.Thinking:
            case NPCEmotion.Confused: return Dudar;
            case NPCEmotion.Determined: return Decidido;
            case NPCEmotion.Relieved:
            case NPCEmotion.Tired:
            case NPCEmotion.Sad: return Suspiro;
            case NPCEmotion.Excited: return exclama ? Alegria : null;
            default: return null;
        }
    }

    public static string[] Preparar(string texto)
    {
        var pedido = ParaDecir(texto);
        if (pedido != null && pedido.Length > 0) return pedido;
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
        // A un grito ahogado o a un silencio («¡...!», «...») no se le asiente.
        if (SinPalabras(texto)) return false;

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
