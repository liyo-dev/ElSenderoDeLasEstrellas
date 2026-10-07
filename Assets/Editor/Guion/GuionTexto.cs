#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// El guion de una secuencia, tal como se escribe en texto. Lo lee HorneadorDeGuion.
///
/// FORMATO (una orden por línea; # empieza un comentario):
///
///   GUION Prologo_UltimaNoche          nombre
///   ESCENA Prologo_Valle               escena de Unity donde pasa
///   SECUENCIA Assets/_SEQUENCES/X.asset el SequenceDefinition que lo reproducirá
///   PREPARA RecursosDelPrologo         clase de Editor con Asegurar() que se llama antes de hornear
///   TEXTO subtitulo                    bocadillo | subtitulo | subtitulo_grande
///   RITMO entre_frases=0.3 mismo_hablante=0.12
///
///   REPARTO                            alias  id  [opciones]
///   archimago NPC_Archimago nombre=NPC_ARCHIMAGO color=FFD27A
///
///   PUNTOS                             nombre  marca | x z
///   plaza 6002.5 6000.5
///   tras_casa M_TrasCasa_Archimago
///
///   PISTAS                             alias  cuándo  acción
///   v01 0 bucle Dance_NoWeapon         cuándo: 0 | @etiqueta[+s] | - (tras la anterior) | +s
///   v01 @alarma huye a puente_1
///
///   HILO                               cada orden empieza cuando acaba la anterior
///   @inicio                            define una etiqueta en este punto
///   archimago anda a esquina, plaza ritmo=paseo
///   & plano sigue archimago            & = a la vez que la anterior, sin esperarla
///   && v01 gesto Cheer01               && = a la vez, y la siguiente espera a las dos
///   +1.5 v02 gesto HandWave01          +s = s segundos después de que empiece la anterior
///   -0.4 liora dice CLAVE              -s = empieza s segundos antes de que acabe la anterior
///   desde @saludo+2 v03 mira archimago = a s segundos de una etiqueta, sin esperar
///   espera 1.5
///
/// Órdenes de personaje: en P [mirando X] · anda|corre|huye a P1, P2 [ritmo=paseo|normal|prisa|trote|corre]
///   [llega_mirando=X] · mira X · gesto G [veces=N] [dura=s] · bucle G [congela] · reposo ·
///   cara EMOCION [dura=s] · dice CLAVE [gesto=G] [mira=X] [oyentes=a,b|todos] [cara=E] [sin_pose] ·
///   aparece [en P] [mirando X] · desaparece · charla con X [cada=s] · pasea por P1, P2 [ritmo=..] [pausa=s]
/// Otras: plano TIPO sujetos [opciones] · efecto ClaseDeBeat campo=valor ... [dura=s] · espera s
/// Frases y efectos admiten onlyIfFlag=marca / skipIfFlag=marca; las alternativas pueden compartir instante con &.
/// En «dice», dura=s reserva un tiempo de lectura y no recorta una voz más larga.
public sealed class GuionTexto
{
    public string nombre, escena, secuencia, prepara;
    /// Trozo de una escena grande (MainWorld): «ZONA nombre x z mitad». Vacío = la escena entera.
    public string zona;
    public Rect zonaRect;
    public PresentacionDeTexto texto = PresentacionDeTexto.Subtitulo;
    public float pausaEntreFrases = 0.3f;
    public float pausaMismoHablante = 0.12f;
    public readonly List<Reparto> reparto = new();
    public readonly List<PuntoDef> puntos = new();
    public readonly List<Orden> hilo = new();
    public readonly List<Orden> pistas = new();
    public readonly List<string> errores = new();
    public string huella;

    public sealed class Reparto
    {
        public int linea;
        public string alias, id;
        public Dictionary<string, string> opciones = new();
    }

    public sealed class PuntoDef
    {
        public int linea;
        public string nombre;
        public string marca;     // si no es null, el punto es esta marca/objeto de la escena
        public float x, z;
        public bool tieneAltura;
        public float y;
    }

    public enum Arranque { Seguido, ALaVez, ALaVezYEspera, TrasInicio, Solape, DesdeEtiqueta, TrasAnteriorDePista, Absoluto }

    public sealed class Orden
    {
        public int linea;
        public string texto;
        public Arranque arranque = Arranque.Seguido;
        public float desfase;
        public string etiqueta;          // para DesdeEtiqueta
        public string defineEtiqueta;    // líneas "@nombre"
        public string actor;             // alias, o null
        public string verbo;
        public List<string> args = new();
        public Dictionary<string, string> opciones = new();

        public string Op(string clave, string porDefecto = null)
            => opciones.TryGetValue(clave, out var v) ? v : porDefecto;
        public bool Tiene(string clave) => opciones.ContainsKey(clave) || args.Contains(clave);
        public float OpF(string clave, float porDefecto)
            => opciones.TryGetValue(clave, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : porDefecto;
        public override string ToString() => $"línea {linea}: {texto}";
    }

    private static readonly HashSet<string> VerbosDeActor = new()
    {
        "en", "anda", "corre", "huye", "mira", "gesto", "bucle", "reposo", "cara", "dice",
        "aparece", "desaparece", "charla", "pasea", "desliza", "sienta", "levanta"
    };

    public static bool EsVerboDeActor(string v) => VerbosDeActor.Contains(v);

    public static GuionTexto Leer(string fuente)
    {
        var g = new GuionTexto();
        g.huella = Hash128.Compute(fuente).ToString();
        string seccion = "CABECERA";
        var lineas = fuente.Replace("\r\n", "\n").Split('\n');
        var aliasConocidos = new HashSet<string>();
        for (int n = 0; n < lineas.Length; n++)
        {
            string crudo = lineas[n];
            int almohadilla = IndiceComentario(crudo);
            string l = (almohadilla >= 0 ? crudo.Substring(0, almohadilla) : crudo).Trim();
            if (l.Length == 0) continue;
            int numero = n + 1;
            var tok = Trocear(l);
            string primera = tok[0];

            switch (primera)
            {
                case "REPARTO": case "PUNTOS": case "PISTAS": case "HILO":
                    seccion = primera; continue;
                case "GUION": g.nombre = Resto(tok); continue;
                case "ESCENA": g.escena = Resto(tok); continue;
                case "ZONA":
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (tok.Count < 5 || !float.TryParse(tok[2], System.Globalization.NumberStyles.Float, ci, out float zx)
                        || !float.TryParse(tok[3], System.Globalization.NumberStyles.Float, ci, out float zz)
                        || !float.TryParse(tok[4], System.Globalization.NumberStyles.Float, ci, out float zm))
                    { g.errores.Add($"línea {numero}: ZONA es «ZONA nombre x z mitad» ({l})"); continue; }
                    g.zona = tok[1];
                    g.zonaRect = Rect.MinMaxRect(zx - zm, zz - zm, zx + zm, zz + zm);
                    continue;
                }
                case "SECUENCIA": g.secuencia = Resto(tok); continue;
                case "PREPARA": g.prepara = Resto(tok); continue;
                case "TEXTO":
                    switch (Resto(tok).ToLowerInvariant())
                    {
                        case "bocadillo": g.texto = PresentacionDeTexto.Bocadillo; break;
                        case "subtitulo": g.texto = PresentacionDeTexto.Subtitulo; break;
                        case "subtitulo_grande": g.texto = PresentacionDeTexto.SubtituloGrande; break;
                        default: g.errores.Add($"línea {numero}: TEXTO no reconocido ({l})"); break;
                    }
                    continue;
                case "RITMO":
                    var o = Opciones(tok, 1, out _);
                    if (o.TryGetValue("entre_frases", out var a)) g.pausaEntreFrases = Num(a, g.pausaEntreFrases);
                    if (o.TryGetValue("mismo_hablante", out var b)) g.pausaMismoHablante = Num(b, g.pausaMismoHablante);
                    continue;
            }

            switch (seccion)
            {
                case "REPARTO":
                {
                    if (tok.Count < 2) { g.errores.Add($"línea {numero}: reparto necesita «alias id»"); break; }
                    var r = new Reparto { linea = numero, alias = tok[0], id = tok[1], opciones = Opciones(tok, 2, out var sueltos) };
                    foreach (var s in sueltos) r.opciones[s] = "si";
                    g.reparto.Add(r);
                    aliasConocidos.Add(r.alias);
                    break;
                }
                case "PUNTOS":
                {
                    var p = new PuntoDef { linea = numero, nombre = tok[0] };
                    if (tok.Count >= 3 && EsNumero(tok[1]) && EsNumero(tok[2]))
                    {
                        p.x = Num(tok[1], 0); p.z = Num(tok[2], 0);
                        if (tok.Count >= 4 && EsNumero(tok[3])) { p.tieneAltura = true; p.y = Num(tok[3], 0); }
                    }
                    else if (tok.Count >= 2) p.marca = tok[1];
                    else { g.errores.Add($"línea {numero}: punto sin posición ({l})"); break; }
                    g.puntos.Add(p);
                    break;
                }
                case "PISTAS":
                {
                    if (tok.Count < 3) { g.errores.Add($"línea {numero}: pista necesita «alias cuándo acción»"); break; }
                    var orden = new Orden { linea = numero, texto = l, actor = tok[0] };
                    if (!LeerCuandoDePista(tok[1], orden)) { g.errores.Add($"línea {numero}: «{tok[1]}» no es un cuándo válido (0, @etiqueta, @etiqueta+2, -, +2, t=12)"); break; }
                    if (!LeerCuerpo(tok, 2, orden, out string error)) { g.errores.Add($"línea {numero}: {error}"); break; }
                    if (!EsVerboDeActor(orden.verbo)) { g.errores.Add($"línea {numero}: en PISTAS solo valen órdenes de personaje ({orden.verbo})"); break; }
                    g.pistas.Add(orden);
                    break;
                }
                case "HILO":
                {
                    var orden = new Orden { linea = numero, texto = l };
                    if (primera.StartsWith("@") && tok.Count == 1)
                    {
                        orden.defineEtiqueta = primera.Substring(1);
                        orden.verbo = "etiqueta";
                        g.hilo.Add(orden);
                        break;
                    }
                    int i = 0;
                    if (tok[0] == "&") { orden.arranque = Arranque.ALaVez; i = 1; }
                    else if (tok[0] == "&&") { orden.arranque = Arranque.ALaVezYEspera; i = 1; }
                    else if (tok[0].Length > 1 && tok[0][0] == '+' && EsNumero(tok[0].Substring(1))) { orden.arranque = Arranque.TrasInicio; orden.desfase = Num(tok[0].Substring(1), 0); i = 1; }
                    else if (tok[0].Length > 1 && tok[0][0] == '-' && EsNumero(tok[0].Substring(1))) { orden.arranque = Arranque.Solape; orden.desfase = Num(tok[0].Substring(1), 0); i = 1; }
                    else if (tok[0] == "desde" && tok.Count > 2 && tok[1].StartsWith("@"))
                    {
                        orden.arranque = Arranque.DesdeEtiqueta;
                        LeerEtiquetaConDesfase(tok[1].Substring(1), out orden.etiqueta, out orden.desfase);
                        i = 2;
                    }
                    if (i >= tok.Count) { g.errores.Add($"línea {numero}: orden vacía"); break; }
                    if (aliasConocidos.Contains(tok[i]))
                    {
                        orden.actor = tok[i];
                        if (!LeerCuerpo(tok, i + 1, orden, out string error)) { g.errores.Add($"línea {numero}: {error}"); break; }
                    }
                    else
                    {
                        orden.verbo = tok[i].ToLowerInvariant();
                        orden.opciones = Opciones(tok, i + 1, out orden.args);
                        if (orden.verbo != "plano" && orden.verbo != "efecto" && orden.verbo != "espera"
                            && !Azucar.Contains(orden.verbo))
                        {
                            g.errores.Add($"línea {numero}: no sé qué es «{tok[i]}» (¿un alias que falta en REPARTO?)");
                            break;
                        }
                    }
                    g.hilo.Add(orden);
                    break;
                }
                default:
                    g.errores.Add($"línea {numero}: fuera de sección ({l})");
                    break;
            }
        }
        if (string.IsNullOrEmpty(g.nombre)) g.errores.Add("falta la cabecera GUION");
        if (string.IsNullOrEmpty(g.escena)) g.errores.Add("falta la cabecera ESCENA");
        return g;
    }

    /// Órdenes cortas que se traducen a efectos (ver HorneadorDeGuion.Azucar).
    public static readonly HashSet<string> Azucar = new()
    {
        "musica", "silencio", "sonido", "ambiente", "hora", "fundido", "destello", "sacudida", "cutin",
        "vfx", "bandas", "lento", "postproceso", "prop", "clima", "flag"
    };

    private static bool LeerCuerpo(List<string> tok, int desde, Orden orden, out string error)
    {
        error = null;
        if (desde >= tok.Count) { error = "falta la acción"; return false; }
        orden.verbo = tok[desde].ToLowerInvariant();
        if (!EsVerboDeActor(orden.verbo)) { error = $"acción desconocida «{tok[desde]}»"; return false; }
        orden.opciones = Opciones(tok, desde + 1, out orden.args);
        // Las marcas sueltas (sin_esperar, seco...) no son argumentos: van a opciones.
        for (int i = orden.args.Count - 1; i >= 0; i--)
            if (Marcas.Contains(orden.args[i])) { orden.opciones[orden.args[i]] = "si"; orden.args.RemoveAt(i); }
        // «anda a X» / «charla con X» / «pasea por X»: la preposición sobra.
        if (orden.args.Count > 0 && (orden.args[0] == "a" || orden.args[0] == "con" || orden.args[0] == "por" || orden.args[0] == "hacia"))
            orden.args.RemoveAt(0);
        // «en P mirando X»
        int m = orden.args.IndexOf("mirando");
        if (m >= 0 && m + 1 < orden.args.Count)
        {
            orden.opciones["mirando"] = orden.args[m + 1];
            orden.args.RemoveRange(m, 2);
        }
        if (orden.verbo == "aparece" && orden.args.Count >= 2 && orden.args[0] == "en")
        {
            orden.opciones["en"] = orden.args[1];
            orden.args.RemoveRange(0, 2);
        }
        return true;
    }

    /// Palabras sueltas que son opciones, no argumentos.
    public static readonly HashSet<string> Marcas = new()
    {
        "sin_esperar", "seco", "congela", "aqui", "sin_pose", "picado", "contrapicado", "cruza", "oculto", "quieta", "solo"
    };

    private static bool LeerCuandoDePista(string t, Orden o)
    {
        if (t == "0" || t == "inicio") { o.arranque = Arranque.Absoluto; o.desfase = 0f; return true; }
        if (t == "-") { o.arranque = Arranque.TrasAnteriorDePista; o.desfase = 0f; return true; }
        if (t.StartsWith("+") && EsNumero(t.Substring(1))) { o.arranque = Arranque.TrasAnteriorDePista; o.desfase = Num(t.Substring(1), 0); return true; }
        if (t.StartsWith("t=") && EsNumero(t.Substring(2))) { o.arranque = Arranque.Absoluto; o.desfase = Num(t.Substring(2), 0); return true; }
        if (t.StartsWith("@"))
        {
            o.arranque = Arranque.DesdeEtiqueta;
            LeerEtiquetaConDesfase(t.Substring(1), out o.etiqueta, out o.desfase);
            return !string.IsNullOrEmpty(o.etiqueta);
        }
        return false;
    }

    private static void LeerEtiquetaConDesfase(string t, out string etiqueta, out float desfase)
    {
        desfase = 0f;
        int mas = t.IndexOfAny(new[] { '+', '-' });
        if (mas > 0 && EsNumero(t.Substring(mas + 1)))
        {
            desfase = Num(t.Substring(mas + 1), 0) * (t[mas] == '-' ? -1f : 1f);
            etiqueta = t.Substring(0, mas);
        }
        else etiqueta = t;
    }

    /// Separa «clave=valor» de las palabras sueltas. Las comas separan listas: «a, b, c».
    public static Dictionary<string, string> Opciones(List<string> tok, int desde, out List<string> sueltos)
    {
        var o = new Dictionary<string, string>();
        sueltos = new List<string>();
        for (int i = desde; i < tok.Count; i++)
        {
            string t = tok[i];
            int igual = t.IndexOf('=');
            if (igual > 0 && !t.StartsWith("\""))
            {
                string clave = t.Substring(0, igual);
                string valor = t.Substring(igual + 1);
                // Listas con espacios: «oyentes=a, b, c»
                while (valor.EndsWith(",") && i + 1 < tok.Count) valor += tok[++i];
                o[clave] = valor.Trim('"');
            }
            else
            {
                foreach (var trozo in t.Split(','))
                    if (trozo.Length > 0) sueltos.Add(trozo.Trim('"'));
            }
        }
        return o;
    }

    private static List<string> Trocear(string l)
    {
        var lista = new List<string>();
        var sb = new StringBuilder();
        bool comillas = false;
        foreach (char c in l)
        {
            if (c == '"') { comillas = !comillas; sb.Append(c); continue; }
            if (!comillas && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { lista.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) lista.Add(sb.ToString());
        return lista;
    }

    private static int IndiceComentario(string l)
    {
        bool comillas = false;
        for (int i = 0; i < l.Length; i++)
        {
            if (l[i] == '"') comillas = !comillas;
            else if (l[i] == '#' && !comillas) return i;
        }
        return -1;
    }

    private static string Resto(List<string> tok) => tok.Count > 1 ? string.Join(" ", tok.GetRange(1, tok.Count - 1)) : "";

    public static bool EsNumero(string s)
        => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    public static float Num(string s, float porDefecto)
        => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : porDefecto;
}
#endif
