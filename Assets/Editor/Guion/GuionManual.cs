#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// El manual del lenguaje de guiones, para la IA y para quien lo lea. Se genera de las mismas
/// tablas que usa el horneador (GuionTexto, HornoCamaras y FabricaDeEfectos) para que no ofrezca
/// una orden que el horneado no entienda.
public static class GuionManual
{
    private static readonly Dictionary<string, string> Verbos = new()
    {
        ["en"] = "en <punto> [mirando <actor|punto|rumbo>]",
        ["anda"] = "anda a <p1>[, <p2>...] [ritmo=paseo|normal|prisa|trote|corre|<m/s>] [llega_mirando=<obj>] [formacion=<actor>] [sin_esperar]",
        ["corre"] = "corre a ... (igual que anda)",
        ["huye"] = "huye a ... (igual que anda)",
        ["pasea"] = "pasea por <p1>, <p2>... [ritmo=...] [pausa=<s>]",
        ["desliza"] = "desliza [<punto>|aqui] [altura=<m>] [dura=<s>] [seco] [mirando=<obj>]",
        ["encima"] = "encima <a1> <a2>... [altura=<m>] [entra=<s>] [dura=<s>]",
        ["de_la_mano"] = "de_la_mano <guia> [lado=der|izq] [dura=<s>]",
        ["suelta"] = "suelta",
        ["sienta"] = "sienta <punto> [ya] [por=<p1>,<p2>] [ritmo=...]",
        ["levanta"] = "levanta",
        ["aparece"] = "aparece [en <punto>] [mirando <obj>]",
        ["desaparece"] = "desaparece",
        ["mira"] = "mira <actor|punto|PROP_id|grados>",
        ["cabeza"] = "cabeza <actor|punto|nadie|frente>",
        ["cara"] = "cara <emocion> [dura=<s>]",
        ["gesto"] = "gesto <Estado> [veces=<N>] [dura=<s>] [voz=no|<clave>]",
        ["bucle"] = "bucle <Estado> [congela] [al_final|ya] [fundido=<s>] [dura=<s>]",
        ["reposo"] = "reposo [brazos]",
        ["charla"] = "charla con <actor> [cada=<s>]",
        ["dice"] = "dice <CLAVE> [gesto=<Estado>] [mira=<obj>] [cabeza=<a1,a2>] [oyentes=todos|<a1,a2>] [cara=<emocion>] [dura=<s>] [sin_pose] [onlyIfFlag=...] [skipIfFlag=...]",
        ["coge"] = "coge <idProp> [mano=derecha|izquierda|cabeza] [offset=x;y;z] [giro=x;y;z]",
        ["deja"] = "deja <idProp> en <punto> [mano=...] [gesto=<Estado>]",
    };

    private static readonly Dictionary<string, string> Azucar = new()
    {
        ["musica"] = "musica <musicId>",
        ["silencio"] = "silencio [fadeOut]",
        ["sonido"] = "sonido <eventKey>",
        ["ambiente"] = "ambiente <loopId>",
        ["hora"] = "hora <timeOfDay> [duracion=N] (N = segundos del cambio; transitionSeconds solo es la espera)",
        ["pasa_el_tiempo"] = "pasa_el_tiempo <timeOfDay> duracion=6 [modo=Animado|Fundido|Rotulo] [clima=Tormenta|Lluvia|Viento|Niebla|Nieve|Despejado] [musica=<musicId>] (luz, cielo, clima y música a la vez; nunca de golpe)",
        ["fundido"] = "fundido entra|sale",
        ["destello"] = "destello [color] [duración]",
        ["sacudida"] = "sacudida [intensidad] [duración]",
        ["cutin"] = "cutin <texto|...> (ver CutInBeat)",
        ["vfx"] = "vfx <prefab> [actor]",
        ["bandas"] = "bandas [quita]",
        ["lento"] = "lento <timeScale> [rampDuration]",
        ["postproceso"] = "postproceso <perfil>",
        ["prop"] = "prop <propId> [oculto]",
        ["clima"] = "clima <fenomeno> [fin]",
        ["flag"] = "flag <flag>",
    };

    public static string Generar()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Guion de cinemáticas · manual");
        sb.AppendLine();
        sb.AppendLine("Un guion es un archivo de texto `Assets/_SEQUENCES/Guiones/<nombre>.guion.txt`. Es la única");
        sb.AppendLine("fuente de verdad: la escena, el horneado y el storyboard salen de él. Una orden por línea;");
        sb.AppendLine("`#` empieza un comentario. Las secciones se abren con `REPARTO`, `PUNTOS`, `PISTAS` o `HILO`.");
        sb.AppendLine("El estado que acompaña a este manual numera las líneas como `L1`, `L2`…; cita esa línea.");
        sb.AppendLine();

        sb.AppendLine("## Cabecera (antes de la primera sección)");
        sb.AppendLine("```");
        sb.AppendLine("GUION <nombre>");
        sb.AppendLine("ESCENA <escena de Unity>");
        sb.AppendLine("ZONA <nombre> <x> <z> <mitad>          (opcional: trozo de una escena grande)");
        sb.AppendLine("SECUENCIA Assets/_SEQUENCES/SEQ_<nombre>.asset");
        sb.AppendLine("PREPARA <clase>                         (opcional)");
        sb.AppendLine("TEXTO subtitulo | bocadillo | subtitulo_grande");
        sb.AppendLine("RITMO entre_frases=<s> mismo_hablante=<s>");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## REPARTO");
        sb.AppendLine("`<alias> <idEnEscena|Player> [nombre=<clave>] [color=RRGGBB] [prefab=Assets/...] [oculto]`");
        sb.AppendLine("El alias es el nombre que se usa en el resto del guion. `Player` es el jugador.");
        sb.AppendLine();

        sb.AppendLine("## PUNTOS");
        sb.AppendLine("`<nombre> <x> <z> [y]` o `<nombre> <marca de la escena>`");
        sb.AppendLine("Sitios a los que se manda a un actor o desde donde se coloca una cámara.");
        sb.AppendLine();

        sb.AppendLine("## PISTAS");
        sb.AppendLine("`<alias> <cuándo> <orden de personaje>`");
        sb.AppendLine("Mueve a un actor en paralelo al HILO. Cuándo: `0`/`inicio`, `t=<s>` (absoluto),");
        sb.AppendLine("`@etiqueta[+s|-s]`, `-` (tras la anterior de la pista) o `+s` (s segundos tras la anterior).");
        sb.AppendLine();

        sb.AppendLine("## HILO");
        sb.AppendLine("Cada orden empieza cuando acaba la anterior. Prefijos:");
        sb.AppendLine("- `@nombre`: define una etiqueta en ese punto.");
        sb.AppendLine("- `&`: empieza a la vez que la anterior, sin esperarla.");
        sb.AppendLine("- `&&`: a la vez, y la siguiente espera a las dos.");
        sb.AppendLine("- `+s`: s segundos después de que empiece la anterior.");
        sb.AppendLine("- `-s`: empieza s segundos antes de que acabe la anterior.");
        sb.AppendLine("- `desde @etiqueta[+s|-s]`: en ese momento, sin esperar.");
        sb.AppendLine("- `espera <s>`: pausa.");
        sb.AppendLine();

        sb.AppendLine("## Órdenes de personaje");
        sb.AppendLine("`<alias> <verbo> ...` en el HILO; en PISTAS, el alias va primero.");
        foreach (string verbo in GuionTexto.VerbosDeActorLista.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine("- " + (Verbos.TryGetValue(verbo, out var sintaxis) ? sintaxis : $"{verbo} (sin descripción; mirar GuionTexto)"));
        sb.AppendLine();

        sb.AppendLine("## Planos");
        sb.AppendLine("`plano <tipo> <sujetos|puntos...> [opciones]`");
        sb.AppendLine("Tipos: `general`, `conjunto`, `medio`, `primer`/`primerplano`, `detalle`, `dos A B`,");
        sb.AppendLine("`hombro A B` (A de espaldas, B en foco), `sigue A`, `fijo`.");
        sb.AppendLine("Un `medio` de alguien que habla cara a cara con otro se convierte en `hombro` salvo que");
        sb.AppendLine("lleve `solo` o `desde=`. `dos` y `hombro` necesitan dos personajes.");
        sb.AppendLine("Opciones: `desde=<punto>` (sitio de la cámara), `mira=<punto>`, `altura=<m>`,");
        sb.AppendLine("`altura_foco=<m>`, `distancia=<m>`, `lente=<grados>` (menos = más cerrado, más = más abierto),");
        sb.AppendLine("`zoom=<grados>` (lente al final), `lado=izq|der|frente|espalda`, `rumbo=<grados>`,");
        sb.AppendLine("`encuadre=izq|centro|der`, `mover=<punto>`, `altura_final=<m>`, `gira=<grados>`,");
        sb.AppendLine("`mezcla=<s>`, `suave=<s>`, `mano=0..1`, `solo`, `picado`, `contrapicado`, `cruza`, `quieta`.");
        sb.AppendLine("`eje <A> <B>` fija la dirección de pantalla (A a la izquierda); `eje libre` la suelta.");
        sb.AppendLine();

        sb.AppendLine("## Efectos");
        sb.AppendLine("`efecto <Clase> campo=valor ... [dura=<s>]` (o `efecto <Clase>` con los valores por defecto).");
        sb.AppendLine("Clases disponibles y campos que aceptan:");
        foreach (var tipo in FabricaDeEfectos.TiposDeCatalogo())
        {
            var campos = FabricaDeEfectos.CamposDe(tipo)
                .Select(c => $"{c.Name}:{Tipo(c.FieldType)}")
                .ToList();
            sb.AppendLine($"- `{tipo.Name}` ({(campos.Count == 0 ? "sin campos" : string.Join(", ", campos))})");
        }
        sb.AppendLine();
        sb.AppendLine("Ejemplos: `efecto SfxBeat eventKey=Wood_Scratching-009` ·");
        sb.AppendLine("`efecto MusicBeat musicId=PROLOGUE_DREAM fadeIn=0.5`.");
        sb.AppendLine();

        sb.AppendLine("## Órdenes cortas (azúcar)");
        foreach (string clave in GuionTexto.Azucar.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine("- " + (Azucar.TryGetValue(clave, out var sintaxis) ? sintaxis : $"{clave} (sin ejemplo)"));
        sb.AppendLine();

        sb.AppendLine("## Caras");
        var caras = CarasDelGuion.Palabras.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        sb.AppendLine("`cara <emocion> [dura=<s>]`. Emociones: " + string.Join(", ", caras) + ".");
        sb.AppendLine();

        sb.AppendLine("## Marcas y opciones comunes");
        sb.AppendLine("Palabras sueltas que son opción, no argumento: " +
                      string.Join(", ", GuionTexto.Marcas.OrderBy(m => m, StringComparer.OrdinalIgnoreCase)) + ".");
        sb.AppendLine("`onlyIfFlag=<marca>` y `skipIfFlag=<marca>` en frases y efectos: la línea solo pasa si la");
        sb.AppendLine("historia tiene (o no) esa marca. `dura=<s>` en `dice` reserva tiempo de lectura.");
        sb.AppendLine();

        sb.AppendLine("## Reglas para la IA");
        sb.AppendLine("- El texto es la única verdad. No se edita la escena, el prefab, el `.asset` horneado ni el");
        sb.AppendLine("  `SequenceDefinition` a mano: se cambia el `.guion.txt` y se hornea.");
        sb.AppendLine("- Usa solo alias del REPARTO, puntos del PUNTOS y etiquetas del HILO. Si renombras uno, rompes");
        sb.AppendLine("  todas las líneas que lo citan.");
        sb.AppendLine("- `gesto` y `bucle` solo aceptan estados del Animator del actor; están en el estado que acompaña");
        sb.AppendLine("  a este manual. No inventes nombres.");
        sb.AppendLine("- Para hablar usa `dice`; `efecto SayBeat` es solo para voces fuera de escena");
        sb.AppendLine("  (`fueraDeEscena=si`). Las bandas de cine las pone `TEXTO subtitulo`: no hace falta `bandas`");
        sb.AppendLine("  salvo para quitarlas.");
        sb.AppendLine("- Cita la línea (`L12`) al proponer un cambio. Para corregir, cambia lo mínimo: una línea o una");
        sb.AppendLine("  opción.");
        sb.AppendLine("- Después de cambiar el guion hay que hornear y leer el parte. Nunca se deja un guion que no");
        sb.AppendLine("  hornea.");
        sb.AppendLine();

        sb.AppendLine("## Ejemplo mínimo");
        sb.AppendLine("```");
        sb.AppendLine("GUION Ejemplo");
        sb.AppendLine("ESCENA MainWorld");
        sb.AppendLine("SECUENCIA Assets/_SEQUENCES/SEQ_Ejemplo.asset");
        sb.AppendLine("TEXTO subtitulo");
        sb.AppendLine("REPARTO");
        sb.AppendLine("will Player nombre=Will");
        sb.AppendLine("c1 NPC_Contrabandista1 nombre=CHAR_CONTRABANDISTA");
        sb.AppendLine("PUNTOS");
        sb.AppendLine("inicio 0 0");
        sb.AppendLine("HILO");
        sb.AppendLine("@inicio");
        sb.AppendLine("will en inicio mirando c1");
        sb.AppendLine("plano medio will lente=30");
        sb.AppendLine("will dice SALUDO");
        sb.AppendLine("espera 1");
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string Tipo(Type t)
    {
        if (t == typeof(string)) return "texto";
        if (t == typeof(float)) return "número";
        if (t == typeof(int)) return "entero";
        if (t == typeof(bool)) return "si/no";
        if (t == typeof(Color)) return "color";
        if (t.IsEnum) return string.Join("|", Enum.GetNames(t));
        return t.Name;
    }
}
#endif
