using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Capítulo 3, «La pista del mar» (GDD § 5.7, novelas V–VI): Liam habla con Tobías por el espejo,
/// Will reúne tres pistas en la aldea del mar (el pueblo pesquero) y sigue a escondidas al
/// contrabandista por el puente del camino 5, el caserío abandonado y la linde del bosque (si le ve,
/// pelean: ganar sigue la historia, perder repite el seguimiento); en el claro decide jugando: coger la pluma y avisar a la guardia del cruce, o
/// entrar y pelear (las jaulas arden y queda el flag <see cref="FlagJaulasQuemadas"/>).
/// Adaptaciones del juego: el molino y el establo de la propuesta son la cerca del huerto y el
/// cobertizo de las redes del puerto; el puesto de guardia es el descanso del cruce.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    private const string Cap3 = "Cap3";
    public const string MisionCap3 = "CAP3_MARCAS_DE_HIERRO";

    public const string FlagCap3Iniciado = "CAP3_INICIADO";
    public const string FlagSigilo = "CAP3_AVISO_A_LA_GUARDIA";
    public const string FlagCombate = "CAP3_PELEA_EN_EL_CLARO";
    public const string FlagJaulasQuemadas = "CAP3_JAULAS_QUEMADAS";

    private static class S3
    {
        public const string Aldea = "CAP3_ALDEA_EMPIEZA";
        public const string LlegadaAldea = "CAP3_LLEGADA_ALDEA";
        public const string Pistas = "CAP3_PISTAS_EMPIEZA";
        public const string PistaPosadero = "CAP3_PISTA_POSADERO";
        public const string PistaHuerto = "CAP3_PISTA_HUERTO";
        public const string PistaCobertizo = "CAP3_PISTA_COBERTIZO";
        public const string PistaHecha = "CAP3_PISTA_HECHA";
        public const string Noche = "CAP3_NOCHE_EMPIEZA";
        public const string LlegadaBosque = "CAP3_LLEGADA_BOSQUE";
        public const string CaeLaNoche = "CAP3_CAE_LA_NOCHE";
        public const string Decide = "CAP3_DECIDE_EMPIEZA";
        public const string ContrabandistasFin = "CAP3_CONTRABANDISTAS_FIN";
        public const string Vencido = "CAP3_CONTRABANDISTA_VENCIDO";
        public const string CarreteroVencido = "CAP3_CARRETERO_VENCIDO";
        public const string LlegaAlClaro = "CAP3_LLEGA_AL_CLARO";
        public const string EnElClaro = "CAP3_EN_EL_CLARO";
    }

    private const string Posadero = "NPC_Posadero", Lucia = "NPC_Lucia", Sargento = "NPC_Sargento";
    private const string Guardia1 = "NPC_GuardiaCruce1", Guardia2 = "NPC_GuardiaCruce2";
    private const string Contra1 = "NPC_Contrabandista1", Contra2 = "NPC_Contrabandista2", Jefe = "NPC_ContrabandistaJefe";
    private const string RutaHoja = CarpetaItems + "/IT_HojaDeAvistamientos.asset";
    private const string RutaPluma = CarpetaItems + "/IT_PlumaBrillante.asset";
    private const string CombateJefe = "Assets/_NPCs/Combat/NPC_Combat_Config_BoyPirate.asset";
    /// Música del seguimiento a escondidas. Si aún no está en el proyecto, el tramo no cambia la música.
    private const string MusicaDelSigilo = "Assets/Audio/Music/Zonas/Tras el carro.mp3";
    private const string CarroDeLasJaulas = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Cart07.prefab";
    private const string Matorral = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Tree07_b01.prefab";

    private static Capitulo MontarAssetsCap3(StringBuilder informe)
    {

        // Nombres de los personajes nuevos.
        foreach (var (clave, es, en) in new[]
        {
            ("CHAR_POSADERO", "Posadero", "Innkeeper"), ("CHAR_LUCIA", "Lucía", "Lucía"),
            ("CHAR_SARGENTO", "Sargento", "Sergeant"), ("CHAR_GUARDIA_CRUCE", "Guardia", "Guard"),
            ("CHAR_CONTRABANDISTA", "Contrabandista", "Smuggler"), ("CHAR_TOBI", "Tobías", "Tobías"),
        }) TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, clave, es, en);

        Objeto("IT_HojaDeAvistamientos", "hoja_avistamientos", "Hoja de avistamientos", "Sightings sheet",
            "La hoja donde Will apunta lo que cuentan en la aldea del mar de los animales con aro.",
            "The sheet where Will writes down what the seaside villagers tell him about the collared animals.");

        Mision("Q_CAP3_MARCAS_DE_HIERRO", MisionCap3, "Las marcas de hierro", "The iron marks",
            "Estela no acepta aprendices. Eldran cree que te escuchará si le llevas pruebas de los aros, y en la aldea del mar han visto animales con aro.",
            "Estela doesn't take apprentices. Eldran thinks she'll listen if you bring her proof about the collars, and at the seaside village they've seen collared animals.",
            ("CAP3_LLEGAR", "Llega a la aldea del mar", "Reach the seaside village"),
            ("CAP3_POSADA", "Pregunta en la posada", "Ask at the inn"),
            ("CAP3_PISTA_1", "Escucha lo que vio el posadero", "Hear what the innkeeper saw"),
            ("CAP3_PISTA_2", "Mira la cerca del huerto", "Look at the garden fence"),
            ("CAP3_PISTA_3", "Mira la casa de las nasas", "Look at the house with the lobster pots"),
            ("CAP3_HOJA", "Cuéntaselo al posadero", "Tell the innkeeper"),
            ("CAP3_BOSQUE", "Ve por el camino hacia el Bosque Prohibido", "Take the road toward the Forbidden Forest"),
            ("CAP3_NOCHE", "Espera el carro de noche junto al fuego", "Wait for the cart by the fire at night"),
            ("CAP3_CARRO", "Sigue al carro sin que te vea", "Follow the cart without being seen"),
            ("CAP3_CLARO", "Decide qué hacer en el claro", "Decide what to do in the clearing"));

        // ── Diálogos ────────────────────────────────────────────────────────────────────────
        string dlgLuces = DialogoDeCaja("DLG_CAP3_POSADERO_LUCES", "DLG_CAP3_LUCES",
            W("¿Y usted ha visto alguno de esos animales?", "Have you seen any of those animals yourself?"),
            L("CHAR_POSADERO", "Animales no. Pero hace unas semanas, una noche que no podía dormir, vi luces en el camino del bosque.", "Animals, no. But a few weeks back, one night I couldn't sleep, I saw lights on the forest road."),
            L("CHAR_POSADERO", "No eran faroles. Iban casi a ras de suelo, todas juntas.", "They weren't lanterns. They were almost at ground level, all bunched together."),
            W("¿Luciérnagas?", "Fireflies?", NPCEmotion.Thinking),
            L("CHAR_POSADERO", "Las luciérnagas no se encienden todas a la vez. Aquello subía y bajaba despacio… como algo que respira dormido.", "Fireflies don't all light up at once. That rose and fell, slowly… like something breathing in its sleep.", NPCEmotion.Worried));
        string dlgZorro = DialogoDeCaja("DLG_CAP3_LUCIA_ZORRO", "DLG_CAP3_ZORRO",
            L("CHAR_LUCIA", "¿Tú también buscas al zorro del collar? ¡Yo lo vi!", "Are you looking for the fox with the collar too? I saw it!", NPCEmotion.Excited),
            L("CHAR_LUCIA", "Estaba en la cerca del huerto, rascándose el cuello contra la madera. Así, mira.", "It was at the garden fence, rubbing its neck against the wood. Like this, look."),
            L("CHAR_LUCIA", "Le dije «ven, bonito» y salió corriendo. Creo que tenía más miedo que yo.", "I said \"here, boy\" and it ran off. I think it was more scared than me.", NPCEmotion.Sad));
        string dlgHuerto = DialogoDeCaja("DLG_CAP3_PISTA_HUERTO", "DLG_CAP3_HUERTO",
            W("Pelo rojizo enganchado en la madera… Aquí es donde Lucía vio al zorro.", "Reddish fur caught on the wood… This is where Lucía saw the fox.", NPCEmotion.Thinking),
            W("Se ha rascado aquí hasta arrancarse el pelo. Como si el collar le hiciera daño.", "It rubbed itself here until the fur came off. As if the collar hurt it.", NPCEmotion.Sad));
        string dlgCobertizo = DialogoDeCaja("DLG_CAP3_PISTA_COBERTIZO", "DLG_CAP3_COBERTIZO",
            W("Mira qué arañazos. Aquí se ha rascado otro animal, uno más grande que el zorro.", "Look at these scratches. Another animal rubbed itself here, one bigger than the fox.", NPCEmotion.Thinking),
            W("Y esto… son restos de hierro. Tienen las mismas rayas que mi trozo de aro.", "And this… it's bits of iron. They have the same lines as my piece of the collar."),
            W("Todavía brillan.", "They're still glowing.", NPCEmotion.Surprised));
        string dlgHoja = DialogoDeCaja("DLG_CAP3_POSADERO_HOJA", "DLG_CAP3_HOJA",
            L("CHAR_POSADERO", "¿Qué, chico? ¿Has sacado algo en claro?", "Well, lad? Found anything?"),
            W("El zorro de Lucía se rascaba contra la cerca hasta arrancarse el pelo. Y en la casa de las nasas, otro animal más grande ha dejado restos de hierro.", "Lucía's fox rubbed itself on the fence until its fur came off. And at the house with the lobster pots, a bigger animal left bits of iron.", NPCEmotion.Thinking),
            W("Con las mismas rayas que esto. Y todavía brillan.", "With the same lines as this. And they're still glowing."),
            L("CHAR_POSADERO", "Llevo semanas contándolo y nadie me hace caso. Ni los guardias del cruce.", "I've been telling people for weeks and nobody listens. Not even the guards at the crossroads.", NPCEmotion.Annoyed),
            L("CHAR_POSADERO", "Y ahora apareces tú con un trozo de esos aros en el bolsillo.", "And now you turn up with a piece of one of those collars in your pocket."),
            W("Se lo voy a llevar a Estela, la hechicera del Bosque Prohibido. Con todo esto, a lo mejor me escucha.", "I'm taking it to Estela, the sorceress of the Forbidden Forest. With all this, maybe she'll listen to me.", NPCEmotion.Determined),
            L("CHAR_POSADERO", "¿Al Bosque Prohibido? Entonces vas por el camino del bosque… ¿Te acuerdas de las luces que te conté?", "The Forbidden Forest? Then you'll take the forest road… Remember the lights I told you about?", NPCEmotion.Worried),
            L("CHAR_POSADERO", "Desde entonces he visto pasar carros por ese camino, siempre de noche y con la carga tapada. Las luces iban con ellos.", "Since then I've seen carts on that road, always at night and with their loads covered. The lights went with them.", NPCEmotion.Worried),
            L("CHAR_POSADERO", "Si quieres saber de dónde salen esos aros, espera junto al fuego del camino y míralo con tus propios ojos.", "If you want to know where those collars come from, wait by the fire on the road and see it with your own eyes."),
            W("Si esos carros llevan animales con aro, a Estela le llevaré algo más que un trozo de hierro.", "If those carts carry collared animals, I'll bring Estela more than a piece of iron.", NPCEmotion.Determined));
        string dlgSargento = DialogoDeCaja("DLG_CAP3_SARGENTO_PLUMA", "DLG_CAP3_SARGENTO",
            L("CHAR_SARGENTO", "¿Contrabandistas? ¿Por aquí? Chico, vete a dormir.", "Smugglers? Around here? Go to bed, kid.", NPCEmotion.Annoyed),
            W("Llevan pájaros en jaulas. Mire esto.", "They're carrying birds in cages. Look at this."),
            L("CHAR_SARGENTO", "Esto es una pluma de pájaro de luz. No se venden. No se pueden vender.", "This is a lightbird feather. They aren't sold. They can't be sold.", NPCEmotion.Surprised),
            L("CHAR_SARGENTO", "Vosotros dos, conmigo. Y tú, chico, enséñanos el camino.", "You two, with me. And you, kid, show us the way.", NPCEmotion.Determined));
        string fPosadero = DialogoDeCaja("DLG_CAP3_POSADERO_DESPUES", "DLG_CAP3_POSADERO_DESPUES",
            L("CHAR_POSADERO", "Cuando sepas quién hace esos aros, vuelve y me lo cuentas. La sopa la pongo yo.", "When you find out who makes those collars, come back and tell me. The soup's on me."));
        string fLucia = DialogoDeCaja("DLG_CAP3_LUCIA_DESPUES", "DLG_CAP3_LUCIA_DESPUES",
            L("CHAR_LUCIA", "¡Esta mañana la cuchara ha subido un dedo más! ¡Un dedo entero!", "This morning the spoon went up one more finger! A whole finger!", NPCEmotion.Excited));
        string fSargento = DialogoDeCaja("DLG_CAP3_SARGENTO_DESPUES", "DLG_CAP3_SARGENTO_DESPUES",
            L("CHAR_SARGENTO", "Gracias a ti esos pájaros vuelan. Si pasas por el cruce, aquí estamos.", "Thanks to you those birds are flying. If you pass the crossroads, we're here."));
        string fGuardia = DialogoDeCaja("DLG_CAP3_GUARDIA", "DLG_CAP3_GUARDIA",
            L("CHAR_GUARDIA_CRUCE", "Sin novedad en el cruce.", "All quiet at the crossroads."));
        string dlgPillado = DialogoDeCaja("DLG_CAP3_CARRETERO_PILLADO", "DLG_CAP3_PILLADO",
            L("CHAR_CONTRABANDISTA", "¿Me venías siguiendo, crío? Mala idea.", "Were you following me, kid? Bad idea.", NPCEmotion.Angry),
            W("Esa luz que llevas no es un farol. ¿Qué escondes?", "That light you're carrying is no lantern. What are you hiding?", NPCEmotion.Determined));
        string dlgVencido = DialogoDeCaja("DLG_CAP3_CARRETERO_VENCIDO", "DLG_CAP3_CARRETERO_VENCIDO",
            L("CHAR_CONTRABANDISTA", "Ay… mi cabeza… Vale, vale, me quedo aquí quieto.", "Ow… my head… Fine, fine, I'll stay right here.", NPCEmotion.Confused),
            W("Venía del bosque. Los demás tienen que estar por ahí.", "He came from the forest. The others must be out there.", NPCEmotion.Determined));

        // ── Cinemáticas ─────────────────────────────────────────────────────────────────────
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_ESPEJO", Guion = "Cap3_01_ElEspejo", Escena = "TorreDeLiam",
            Nombre = "Cap. 3: Tobías en el espejo", Resumen = "Media sopa y la promesa de ir a pescar. La llave del aro se calentará cerca de Will." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_POSADA", Guion = "Cap3_02_LaPosada", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: la posada", Resumen = "Will pregunta por el trozo del aro, le suenan las tripas (una sopa, si alcanza) y Lucía intenta subir la cuchara." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_CARRO", Guion = "Cap3_03_ElCarro", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: el carro de noche", Resumen = "Un carro con jaulas tapadas pasa junto al fuego; por la manta sale una luz que respira." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_CLARO", Guion = "Cap3_04_ElClaro", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: el claro", Resumen = "Doce jaulas de pájaros de luz. A Will le arden las manos: «¿Debes hacerlo?»." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_PAJAROS", Guion = "Cap3_05_LosPajaros",
            Nombre = "Cap. 3: los pájaros", Resumen = "La guardia detiene a los contrabandistas; Will abre las jaulas y los pájaros suben como chispas." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_JAULAS", Guion = "Cap3_06_LasJaulas",
            Nombre = "Cap. 3: las jaulas quemadas", Resumen = "Tras la pelea, el humo trae a la guardia. «Habría bastado con avisar»." });

        // Textos de las cinemáticas (tabla de subtítulos).
        TextoDeCine("CAP3_ESPEJO_01", "Hoy me he tomado media sopa.", "I had half my soup today.");
        TextoDeCine("CAP3_ESPEJO_02", "¿Y la otra media?", "And the other half?");
        TextoDeCine("CAP3_ESPEJO_03", "La otra media ha ganado.", "The other half won.");
        TextoDeCine("CAP3_ESPEJO_04", "Tobi… esa tos.", "Tobi… that cough.");
        TextoDeCine("CAP3_ESPEJO_05", "Cuando vuelva iremos a pescar. Te lo prometo.", "When I get back we'll go fishing. I promise.");
        TextoDeCine("CAP3_ESPEJO_06", "No sabes pescar.", "You don't know how to fish.");
        TextoDeCine("CAP3_ESPEJO_07", "Aprenderé. Dicen que el truco es tener paciencia.", "I'll learn. They say the trick is patience.");
        TextoDeCine("CAP3_ESPEJO_10", "La llave del aro. Se calentará cada vez que el chico esté cerca.", "The collar's key. It'll grow warm whenever the boy is near.");
        TextoDeCine("CAP3_ESPEJO_11", "Aguanta un poco más, Tobi.", "Hold on a little longer, Tobi.");
        TextoDeCine("CAP3_POSADA_01", "Perdone… ¿sabe qué es esto?", "Excuse me… do you know what this is?");
        TextoDeCine("CAP3_POSADA_02", "¿De dónde has sacado eso?", "Where did you get that?");
        TextoDeCine("CAP3_POSADA_03", "Lo llevaba puesto el demonio que atacó mi pueblo.", "The demon that attacked my village was wearing it.");
        TextoDeCine("CAP3_POSADA_04", "Demonios por aquí, no. Pero animales con un aro como ese… unos cuantos.", "No demons around here. But animals with a collar like that… quite a few.");
        TextoDeCine("CAP3_POSADA_05", "…¿Eso han sido tus tripas?", "…Was that your stomach?");
        TextoDeCine("CAP3_POSADA_06", "Se me acabó la comida ayer… ¿Me llega con esto para una sopa?", "My food ran out yesterday… Is this enough for a soup?");
        TextoDeCine("CAP3_POSADA_07", "Para sopa, sí. Para pan, no.", "For soup, yes. For bread, no.");
        TextoDeCine("CAP3_POSADA_08", "¿Quieres ver magia? Mira la cuchara.", "Want to see some magic? Watch the spoon.");
        TextoDeCine("CAP3_POSADA_09", "Sube… sube… hasta el pulgar…", "Up… up… all the way to my thumb…");
        TextoDeCine("CAP3_POSADA_10", "Casi.", "Almost.");
        TextoDeCine("CAP3_POSADA_11", "¿Casi? Yo a tu edad no movía ni una miga.", "Almost? At your age I couldn't move a crumb.");
        TextoDeCine("CAP3_POSADA_12", "Tómate la sopa y luego pregunta por ahí. Aquí la gente ve muchas cosas, aunque luego no las cuente.", "Have your soup, then ask around. People here see plenty, even if they don't say so.");
        TextoDeCine("CAP3_POSADA_13", "¿De verdad? ¡Pues mañana llego al pulgar!", "Really? Then tomorrow I'll reach my thumb!");
        TextoDeCine("CAP3_CARRO_01", "Ahí viene. Un carro de noche, con la carga tapada… lo que contó el posadero.", "Here it comes. A cart at night, with its load covered… just like the innkeeper said.");
        TextoDeCine("CAP3_CARRO_02", "Esa luz que sale de la paja… sube y baja despacio, como si respirara. Son las luces del camino.", "That light coming from the straw… it rises and falls slowly, as if it were breathing. Those are the lights on the road.");
        TextoDeCine("CAP3_CARRO_03", "Ahí dentro van los animales de los aros. Voy a ver adónde los lleva. De lejos, sin que me vea.", "The collared animals are in there. I'm going to see where he takes them. From a distance, without being seen.");
        TextoDeCine("CAP3_CLARO_01", "Doce jaulas. En la capital pagan bien por plumas que brillan.", "Twelve cages. They pay well in the capital for feathers that glow.");
        TextoDeCine("CAP3_CLARO_02", "No paran de brillar. Me ponen nervioso.", "They won't stop glowing. They make me nervous.");
        TextoDeCine("CAP3_CLARO_03", "Pues tápalos. Al amanecer, al barco.", "Then cover them. At dawn, to the boat.");
        TextoDeCine("CAP3_CLARO_04", "El perro no deja de gruñir al bosque.", "The dog won't stop growling at the forest.");
        TextoDeCine("CAP3_CLARO_05", "Podría entrar ahora mismo y sacarlos de ahí…", "I could go in right now and get them out of there…");
        TextoDeCine("CAP3_CLARO_06", "Pero son tres. Y si el fuego se me escapa, las jaulas arden con los pájaros dentro.", "But there are three of them. And if the fire gets away from me, the cages burn with the birds inside.");
        TextoDeCine("CAP3_CLARO_07", "Eldran siempre lo dice: que puedas hacer algo no quiere decir que debas.", "Eldran always says it: being able to do something doesn't mean you should.");
        TextoDeCine("CAP3_PAJAROS_01", "¡Guardia del cruce! Que nadie se mueva.", "Crossroads guard! Nobody move.");
        TextoDeCine("CAP3_PAJAROS_02", "Solo son pájaros…", "They're only birds…");
        TextoDeCine("CAP3_PAJAROS_03", "Pájaros que no se venden. Atadlos.", "Birds that aren't for sale. Tie them up.");
        TextoDeCine("CAP3_PAJAROS_04", "Ya está. Volad.", "There. Fly.");
        TextoDeCine("CAP3_PAJAROS_05", "Avisaste en vez de liarte a golpes. Eso, chico, es lo difícil.", "You warned us instead of throwing punches. That, kid, is the hard part.");
        TextoDeCine("CAP3_PAJAROS_06", "Si vas al Bosque Prohibido, ve de día. Y no toques nada que brille.", "If you're going to the Forbidden Forest, go by day. And don't touch anything that glows.");
        TextoDeCine("CAP3_PAJAROS_07", "Gracias. Lo haré.", "Thanks. I will.");
        TextoDeCine("CAP3_JAULAS_01", "Se acabó…", "It's over…");
        TextoDeCine("CAP3_JAULAS_02", "Las jaulas… no, no, no.", "The cages… no, no, no.");
        TextoDeCine("CAP3_JAULAS_03", "Hemos visto el humo desde el cruce.", "We saw the smoke from the crossroads.");
        TextoDeCine("CAP3_JAULAS_04", "Tres jaulas quemadas. Los demás pájaros se salvarán.", "Three cages burnt. The other birds will make it.");
        TextoDeCine("CAP3_JAULAS_05", "Habría bastado con avisar, chico.", "Warning us would have been enough, kid.");
        TextoDeCine("CAP3_JAULAS_06", "Lo sé.", "I know.");
        TextoDeCine("CAP3_JAULAS_07", "Ve de día al Bosque Prohibido. Y piensa antes de arder.", "Go to the Forbidden Forest by day. And think before you burn.");

        // ── Marcas, roster y escena ─────────────────────────────────────────────────────────
        M(Cap3, "Cap3_Aldea", 262f, -414f, 180f);
        M(Cap3, "Cap3_Posada", 279.0f, -425.4f, 90f);
        // La acampada, en la orilla este junto al camino que cruza el puente del camino 5 (el cauce queda al oeste). Ver INC-1022.
        M(Cap3, "Cap3_Acampada", -101f, -208.5f, 40f);
        M(Cap3, "Cap3_Seguir_Vuelta", -98f, -210f, 235f);
        // El seguimiento a escondidas: el puente (lo abierto), el caserío abandonado (paredes y casas sin
        // tejado) y la linde del Bosque Prohibido (troncos gordos). En las cuatro paradas mira atrás.
        M(Cap3, "Cap3_Sigilo_SalidaNpc", -104.5f, -218.5f, 235f);
        M(Cap3, "Cap3_Puente", -129f, -235f);
        M(Cap3, "Cap3_Ruta1", -150f, -222f);
        M(Cap3, "Cap3_Caserio", -171f, -200f);
        M(Cap3, "Cap3_Ruta2", -186f, -186f);
        M(Cap3, "Cap3_Linde", -193f, -168f);
        M(Cap3, "Cap3_Ruta3", -203f, -150f);
        // Donde se queda el del seguimiento, a la entrada del claro y fuera de los planos de su cinemática.
        M(Cap3, "Cap3_Sigilo_Fin", -217f, -143f, 330f);
        M(Cap3, "Cap3_Claro", -215f, -125f, 315f);
        // Fuera de la mata de hojas grandes del suelo vegetal: si no, la cámara del primer plano se mete en ella (INC-1038).
        M(Cap3, "Cap3_Matorral", -208f, -134f, 315f);
        M(Cap3, "Cap3_Puesto", -90f, -124f, 270f);
        M(Cap3, "Cap3_Amanecer", -96f, -126f, 270f);

        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Posadero", Pos = new Vector3(281.2f, float.NaN, -424.6f), Rumbo = 250f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Lucia", Pos = new Vector3(277.2f, float.NaN, -425.0f), Rumbo = 100f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Sargento", Pos = new Vector3(-87f, float.NaN, -121f), Rumbo = 270f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_GuardiaCruce1", Pos = new Vector3(-92f, float.NaN, -118f), Rumbo = 250f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_GuardiaCruce2", Pos = new Vector3(-93f, float.NaN, -129f), Rumbo = 290f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Contrabandista1", Pos = new Vector3(-86f, float.NaN, -204.6f), Rumbo = 232f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Contrabandista2", Pos = new Vector3(-216.5f, float.NaN, -129.5f), Rumbo = 90f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_ContrabandistaJefe", Pos = new Vector3(-213f, float.NaN, -128f), Rumbo = 0f });

        s_papeles.Add(new Papel { SpawnId = "SPAWN_Posadero", Prefab = CarpetaAldea + "/Posadero.prefab", Nombre = "Posadero", Id = Posadero, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Lucia", Prefab = CarpetaAldea + "/Lucia.prefab", Nombre = "Lucia", Id = Lucia, Voz = "Vecina", Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Sargento", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "Sargento", Id = Sargento, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_GuardiaCruce1", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "GuardiaCruce1", Id = Guardia1, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_GuardiaCruce2", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "GuardiaCruce2", Id = Guardia2, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Contrabandista1", Prefab = "Assets/_NPCs/Pueblo/Jorge.prefab", Nombre = "Contrabandista1", Id = Contra1, Flag = FlagCap3Iniciado, NavMesh = false });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Contrabandista2", Prefab = "Assets/_NPCs/Combat/Boy_Pirate.prefab", Nombre = "Contrabandista2", Id = Contra2, Flag = FlagCap3Iniciado, NavMesh = false });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_ContrabandistaJefe", Prefab = "Assets/_NPCs/Combat/Boy_Pirate.prefab", Nombre = "ContrabandistaJefe", Id = Jefe, Flag = FlagCap3Iniciado, NavMesh = false });

        // Los contrabandistas aparecen cuando anochece con Will junto al fuego; después, detenidos o huidos.
        // El del seguimiento no está en el claro: se queda descargando (o aturdido en el camino si Will le ganó).
        s_agendas.Add(new Agenda
        {
            Actor = Contra1,
            Entradas =
            {
                ("Cap. 3 · antes de la noche", Tramo(null, S3.CaeLaNoche), PresenciaEnAgenda.NoEsta, null, null),
                ("Cap. 3 · fuera del claro", Tramo(S3.EnElClaro), PresenciaEnAgenda.NoEsta, null, null),
            },
        });
        foreach (var c in new[] { Contra2, Jefe })
            s_agendas.Add(new Agenda
            {
                Actor = c,
                Entradas =
                {
                    ("Cap. 3 · antes de la noche", Tramo(null, S3.CaeLaNoche), PresenciaEnAgenda.NoEsta, null, null),
                    ("Cap. 3 · detenidos", Tramo(S3.ContrabandistasFin), PresenciaEnAgenda.NoEsta, null, null),
                },
            });
        s_agendas.Add(new Agenda { Actor = Posadero, Entradas = { ("Cap. 3 · tras la hoja", Tramo(S3.Noche), PresenciaEnAgenda.SinCambios, null, fPosadero) } });
        s_agendas.Add(new Agenda
        {
            Actor = Lucia,
            Entradas =
            {
                ("Cap. 3 · las pistas", Tramo(S3.Pistas, S3.Noche), PresenciaEnAgenda.SinCambios, null, dlgZorro),
                ("Cap. 3 · después", Tramo(S3.Noche), PresenciaEnAgenda.SinCambios, null, fLucia),
            },
        });
        s_agendas.Add(new Agenda
        {
            Actor = Sargento,
            Entradas =
            {
                ("Cap. 3 · vuelve al cruce", Tramo(S3.ContrabandistasFin), PresenciaEnAgenda.EnUnaMarca, "Cap3_Puesto", fSargento),
            },
        });
        foreach (var g in new[] { Guardia1, Guardia2 })
            s_agendas.Add(new Agenda { Actor = g, Entradas = { ("Cap. 3 · el cruce", Tramo(null), PresenciaEnAgenda.SinCambios, null, fGuardia) } });

        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Llegada a la aldea del mar", Senal = S3.LlegadaAldea, Mision = MisionCap3,
            Pos = new Vector3(262f, float.NaN, -414f), Radio = 16f, Tramo = Tramo(S3.Aldea, S3.Pistas) });
        // El sitio junto al fuego del camino del bosque; el minimapa lo marca hasta que anochece con Will allí.
        // Tras ganar al contrabandista del seguimiento, Will sigue solo hasta el claro.
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Llegada al claro de los contrabandistas", Senal = S3.LlegaAlClaro, Mision = MisionCap3,
            Pos = new Vector3(-207f, float.NaN, -138f), Radio = 12f, Tramo = Tramo(S3.CarreteroVencido, S3.EnElClaro) });
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Llegada al camino del bosque", Senal = S3.LlegadaBosque, Mision = MisionCap3,
            Pos = new Vector3(-102f, float.NaN, -209f), Radio = 18f, Tramo = Tramo(S3.Noche, S3.CaeLaNoche) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap3, Nombre = "Pista · la cerca del huerto", Dialogo = dlgHuerto, Senal = S3.PistaHuerto, Mision = MisionCap3, Paso = 3,
            Pos = new Vector3(231f, float.NaN, -399.6f), Tramo = Tramo(S3.Pistas, S3.Noche) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap3, Nombre = "Pista · el cobertizo de las redes", Dialogo = dlgCobertizo, Senal = S3.PistaCobertizo, Mision = MisionCap3, Paso = 4,
            Pos = new Vector3(299.6f, float.NaN, -415.9f), Tramo = Tramo(S3.Pistas, S3.Noche) });

        // La posada es la casa de pescadores 3; la mesa es la «Mesa de encuentro» de la plaza (279, −427).
        // Hoguera de descanso (CampfireRestInteractable): descansar en ella hace pasar el tiempo hasta la noche.
        s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "Hoguera de la acampada", Prefab = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/BuildingUtilityDeco/Fire01.prefab",
            Pos = new Vector3(-99.5f, float.NaN, -206.5f), Rumbo = 0f, Tamano = 1.7f, Colision = true, Tramo = Tramo(S3.Noche, FinCap3) });
        // El carro de las jaulas: lo lleva el contrabandista del seguimiento tirando de él (CargaArrastrada,
        // en PonerExtrasCap3), con la carga tapada con paja y una luz que respira en ella. Desde que anochece junto al
        // fuego hasta que Will llega al claro, donde está el carro del campamento (INC-1036).
        s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "PROP_Carro Cap3", Prefab = CarroDeLasJaulas,
            Pos = new Vector3(-83.9f, float.NaN, -203.0f), Rumbo = 321f, Tamano = 4.2f, Tramo = Tramo(S3.CaeLaNoche, S3.EnElClaro) });
        // Matorrales donde no se ve al jugador, en los huecos sin pared ni árbol del seguimiento.
        foreach (var (nombre, x, z, rumbo) in new[]
        {
            ("antes del puente", -108f, -215f, 20f),
            ("bosquete tras el puente", -144f, -219f, 70f),
            ("camino del caserío", -158f, -212f, 140f),
            ("dentro del caserío", -176f, -203f, 200f),
            ("salida del caserío", -187f, -180f, 260f),
            ("linde del bosque", -197f, -158f, 310f),
            ("junto al claro", -207f, -143f, 30f),
        })
            s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "Matorral escondite · " + nombre, Prefab = Matorral,
                Pos = new Vector3(x, float.NaN, z), Rumbo = rumbo, Tamano = 2.6f, Escondite = true });

        // ── El grafo ────────────────────────────────────────────────────────────────────────
        var c3 = new Capitulo { Etiqueta = "Cap3", Ruta = "Assets/NarrativeGraph/Cap3.asset", Prefijo = "cap3-", Nombre = "Cap. 3" };
        c3.Recorrido = new List<Paso>
        {
            P("inicio", "Inicio del Cap. 3", new StartNode()),
            P("tras-cap2", "Tras el Cap. 2 (" + FinCap2 + ")", Esperar(FinCap2)),
            P("flag", "Comienza el Cap. 3 (aparece el reparto de la costa y del cruce)", Flag(FlagCap3Iniciado)),
            // 3.2 · La aldea del mar
            P("aldea", "3.2 · Sigue hacia la aldea del mar (probar desde aquí)", Emitir(S3.Aldea)),
            P("llegada", "3.2 · Llega a la aldea del mar", Esperar(S3.LlegadaAldea)),
            P("paso-llegar", "Paso: llegar a la aldea", Completar(MisionCap3, "CAP3_LLEGAR")),
            P("posadero", "3.2 · Hablar con el posadero", Hablar(Posadero)),
            P("posada", "3.2 · Cinemática: la posada, la cuchara y el trozo del aro", Cinematica("CAP3_POSADA")),
            P("paso-posada", "Paso: preguntar en la posada", Completar(MisionCap3, "CAP3_POSADA")),
            P("hoja", "3.2 · Will saca su hoja para apuntar lo que vea", Dar(RutaHoja)),
            P("pistas", "3.2 · Las tres pistas, en el orden que quiera (probar desde aquí)", Emitir(S3.Pistas)),
            P("pistas-fork", "3.2 · Pistas en paralelo", new ForkNode(), "pista1-hablar", "pista2", "pista3", "pistas-espera"),
            P("pistas-espera", "3.2 · Esperar las tres pistas", new WaitSignalCountNode { eventKey = S3.PistaHecha, count = 3 }),
            P("hoja-volver", "3.2 · Volver con el posadero", Hablar(Posadero)),
            P("hoja-dlg", "3.2 · El posadero: «Tú eres el primero que trae uno encima»", Dialogo(dlgHoja, Posadero)),
            P("paso-hoja", "Paso: contárselo al posadero", Completar(MisionCap3, "CAP3_HOJA")),
            // 3.3 · Los contrabandistas
            // Will va andando hasta el fuego del camino del bosque y espera la noche (pasa sola o descansando en la hoguera).
            P("noche", "3.3 · Ir andando al camino del bosque (probar desde aquí)", Emitir(S3.Noche)),
            P("bosque-llega", "3.3 · Llega al camino del bosque", Esperar(S3.LlegadaBosque)),
            P("paso-bosque", "Paso: ir hacia el bosque", Completar(MisionCap3, "CAP3_BOSQUE")),
            P("bosque-dice", "3.3 · Will: los carros pasan de noche", Bocadillo("CAP3_BOC_ESPERAR", "El posadero dijo que esos carros pasan de noche. Esperaré aquí, junto al fuego.", "The innkeeper said those carts come by at night. I'll wait here, by the fire.", 3.5f)),
            P("hora", "3.3 · ¿Ya es de noche al llegar?", new SegunHoraDelDiaNode { franja = DayNightCycle.TimeOfDay.Night }, "tuto-noche", "hoguera-tuto"),
            // De noche: el carro viene en cuanto Will va al fuego; el aviso se lee entero (espera a que lo cierre).
            P("tuto-noche", "3.3 · Tutorial (de noche): ve al fuego, o descansa para que se haga de día", Tutorial("TUTORIAL_CAP3_HOGUERA_NOCHE",
                "Ya es de noche: acércate a la hoguera para esperar el carro. Si prefieres seguir explorando de día, pulsa {BOTON} junto a cualquier hoguera para descansar hasta la mañana.",
                "It's already night: go to the campfire to wait for the cart. If you'd rather keep exploring by day, press {BOTON} by any campfire to rest until morning.",
                Core.InputGlyphs.InputGlyphNames.South, esperar: true), "esperar-noche"),
            // De día: se aprende a descansar en la hoguera para que pase el tiempo.
            P("hoguera-tuto", "3.3 · Tutorial (de día): las hogueras hacen pasar el tiempo", Tutorial("TUTORIAL_CAP3_HOGUERA",
                "Pulsa {BOTON} junto a una hoguera para descansar: de día pasa el tiempo hasta la noche, y de noche hasta la mañana.",
                "Press {BOTON} by a campfire to rest: by day, time passes until night; at night, until morning.",
                Core.InputGlyphs.InputGlyphNames.South)),
            P("esperar-noche", "3.3 · Esperar la noche junto al fuego", new EsperarHoraDelDiaNode { franja = DayNightCycle.TimeOfDay.Night, anchorId = "Cap3_Acampada", radio = 6f }),
            P("cae-noche", "3.3 · Anochece junto al fuego (aparecen los contrabandistas)", Emitir(S3.CaeLaNoche)),
            P("paso-noche", "Paso: esperar la noche", Completar(MisionCap3, "CAP3_NOCHE")),
            P("punto-noche", "3.3 · Punto de guardado: el camino del bosque", Punto("CAP3_ACAMPADA", "Cap3_Acampada")),
            P("carro", "3.3 · Cinemática: el carro con jaulas", Cinematica("CAP3_CARRO")),
            P("seguir-tuto", "3.3 · Aviso: seguir sin que te vea", Tutorial("CAP3_TUTO_SEGUIR",
                "Síguele sin que te vea. Cuando se pare a mirar atrás, escóndete en un matorral o detrás de una pared.",
                "Follow him without being seen. When he stops to look back, hide in a bush or behind a wall.")),
            P("seguir", "3.3 · Seguir al contrabandista a escondidas hasta el claro (probar desde aquí)", new SeguirSinSerVistoNode
            {
                npcId = Contra1, anchorId = "Cap3_Sigilo_Fin",
                puntosDePaso = new List<string> { "Cap3_Puente", "Cap3_Ruta1", "Cap3_Caserio", "Cap3_Ruta2", "Cap3_Linde", "Cap3_Ruta3" },
                paradasParaMirar = new List<string> { "Cap3_Puente", "Cap3_Caserio", "Cap3_Linde", "Cap3_Ruta3" },
                segundosMirando = 3f, velocidad = 2.2f, velocidadDelJugador = 1.7f,
                alcanceDeLaVista = 14f, anguloDeLaVista = 110f, distanciaDeOido = 4f, segundosHastaVerle = 1f,
                distanciaParaEsperar = 30f, radioDeLlegada = 25f, duracionMaxima = 420f,
                salidaDelNpc = "Cap3_Sigilo_SalidaNpc", salidaDelJugador = "Cap3_Seguir_Vuelta", distanciaParaRecolocar = 20f,
                musica = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaDelSigilo),
            }, "paso-carro", "pillado"),
            P("paso-carro", "Paso: seguir al carro", Completar(MisionCap3, "CAP3_CARRO")),
            P("en-el-claro", "3.3 · Will llega al claro (el del seguimiento se queda fuera)", Emitir(S3.EnElClaro)),
            P("claro", "3.3 · Cinemática: el claro y «¿Debes hacerlo?»", Cinematica("CAP3_CLARO")),
            P("decide", "3.3 · La decisión, jugando (probar desde aquí)", Emitir(S3.Decide)),
            P("pista-pluma", "3.3 · Will ve la pluma de la zarza", Bocadillo("CAP3_BOC_ZARZA", "Hay una pluma enganchada en esa zarza…", "There's a feather caught in that bramble…")),
            P("decision", "3.3 · ¿Coge la pluma o entra en el claro? Gana lo primero que haga", new ForkNode(), "pluma-0", "pelea-0"),
            // Rama A · la pluma y la guardia
            P("pluma-0", "3.3 · A · Coge la pluma brillante", new WaitForItemAddedNode { itemToWaitFor = Cargar<ItemData>(RutaPluma), completeIfAlreadyOwned = true }),
            P("pluma-1", "3.3 · A · ¿Ya había empezado a pelear?", new BranchFlagNode { flagKey = FlagCombate }, "", "pluma-2"),
            P("pluma-2", "3.3 · A · Elige avisar a la guardia", Flag(FlagSigilo)),
            P("pluma-3", "3.3 · A · «Tres piedras… y al cruce»", Bocadillo("CAP3_BOC_PIEDRAS", "Tres piedras apiladas, para encontrar el sitio. Y ahora, al puesto de guardia.", "Three stacked stones, to find the spot. Now, to the guard post.", 3.5f)),
            P("pluma-4", "3.3 · A · Hablar con el sargento del cruce", Hablar(Sargento)),
            P("pluma-5", "3.3 · A · El sargento ve la pluma", Dialogo(dlgSargento, Sargento)),
            P("pluma-6", "3.3 · A · Cinemática: los pájaros suben como chispas", Cinematica("CAP3_PAJAROS"), "fin-contrabandistas"),
            // Rama B · la pelea
            P("pelea-0", "3.3 · B · Will entra en el claro", Esperar(Cap2Extras.SenalAtaque)),
            P("pelea-1", "3.3 · B · ¿Ya había elegido avisar?", new BranchFlagNode { flagKey = FlagSigilo }, "", "pelea-2"),
            P("pelea-2", "3.3 · B · Elige pelear", Flag(FlagCombate)),
            P("pelea-3", "3.3 · B · En la pelea arden jaulas", Flag(FlagJaulasQuemadas)),
            P("pelea-4", "3.3 · B · Combate contra el jefe de los contrabandistas", new StartNpcCombatNode
            {
                npcId = Jefe, combatConfig = Cargar<Game.NPC.Modules.NPCCombatConfig>(CombateJefe), defeatEventKey = S3.Vencido, waitForDefeat = true, restorePeacefulOnDefeat = true,
            }),
            P("pelea-5", "3.3 · B · Cinemática: el humo trae a la guardia", Cinematica("CAP3_JAULAS"), "fin-contrabandistas"),
            // Final del capítulo
            P("fin-contrabandistas", "3.3 · Los contrabandistas, detenidos", Emitir(S3.ContrabandistasFin)),
            P("paso-claro", "Paso: el claro (cierra la misión)", Completar(MisionCap3, "CAP3_CLARO", cerrar: true)),
            P("manana-rotulo", "Rótulo: a la mañana siguiente", Rotulo("CAP3_ROTULO_MANANA", "A la mañana siguiente…", "The next morning…")),
            P("manana-hora", "Amanece", new SetTimeOfDayNode { targetTime = DayNightCycle.TimeOfDay.Morning, immediate = true }),
            P("manana-will", "Will amanece en el cruce", Llevar("Player", "Cap3_Amanecer")),
            P("manana-destapa", "Se destapa la pantalla en el cruce", Destapar()),
            P("fin-punto", "Cap. 3 · Fin (punto de guardado)", Punto("CAP3_FIN", "Cap3_Amanecer")),
            P("fin", "Fin del Cap. 3 (" + FinCap3 + ")", Emitir(FinCap3)),
        };
        // La despedida en la salida del pueblo, el camino a pie y la acampada con Tobías (INC-967).
        c3.Recorrido.InsertRange(c3.Recorrido.FindIndex(p => p.Id == "aldea"), PasosDeLaDespedidaYElCamino());
        // Si el contrabandista le ve: pelean. Ganar deja al contrabandista aturdido y Will sigue solo
        // hasta el claro; perder repite el seguimiento desde el puente.
        c3.Ramas.Add(new List<Paso>
        {
            P("pillado", "3.3 · Le ha visto: «¿Me venías siguiendo?»", Dialogo(dlgPillado, Contra1)),
            P("pillado-combate", "3.3 · Combate contra el contrabandista del seguimiento", new StartNpcCombatNode
            {
                npcId = Contra1, combatConfig = Cargar<Game.NPC.Modules.NPCCombatConfig>(CombateJefe), defeatEventKey = S3.CarreteroVencido,
                waitForDefeat = true, restorePeacefulOnDefeat = true,
            }, "pillado-gana", "seguir"),
            P("pillado-gana", "3.3 · Le gana: el contrabandista, aturdido", Dialogo(dlgVencido, Contra1)),
            P("pillado-claro", "3.3 · Will sigue solo hasta el claro", Esperar(S3.LlegaAlClaro), "paso-carro"),
        });
        c3.Ramas.Add(new List<Paso>
        {
            P("pista1-hablar", "3.2 · Pista 1 · hablar con el posadero", Hablar(Posadero)),
            P("pista1-dlg", "3.2 · Pista 1 · las luces que respiran", Dialogo(dlgLuces, Posadero)),
            P("pista1-paso", "Paso: lo que vio el posadero", Completar(MisionCap3, "CAP3_PISTA_1")),
            P("pista1-hecha", "3.2 · Pista 1 hecha", Emitir(S3.PistaHecha)),
        });
        c3.Ramas.Add(new List<Paso>
        {
            P("pista2", "3.2 · Pista 2 · la cerca del huerto (Lucía dice dónde vio al zorro)", Esperar(S3.PistaHuerto)),
            P("pista2-paso", "Paso: la cerca del huerto", Completar(MisionCap3, "CAP3_PISTA_2")),
            P("pista2-hecha", "3.2 · Pista 2 hecha", Emitir(S3.PistaHecha)),
        });
        c3.Ramas.Add(new List<Paso>
        {
            P("pista3", "3.2 · Pista 3 · el cobertizo de las redes", Esperar(S3.PistaCobertizo)),
            P("pista3-paso", "Paso: el cobertizo", Completar(MisionCap3, "CAP3_PISTA_3")),
            P("pista3-hecha", "3.2 · Pista 3 hecha", Emitir(S3.PistaHecha)),
        });
        c3.Notas.Add(("nota", "Cap. 3 · La pista del mar (GDD § 5.7). Lo monta El Sendero ▸ Historia ▸ Montar los capítulos 3 y 4: los nodos «cap3-…» se rehacen. " +
                              "Escena: «Capítulos (generado)» en MainWorld (marcas Cap3_*, puntos del roster, pistas, el campamento de los contrabandistas)."));
        MontarAldeaDelMar(informe);
        informe.AppendLine("Cap. 3: assets y tablas listos.");
        return c3;
    }

    /// <summary>El campamento de los contrabandistas en el claro, solo de noche hasta su detención.</summary>
    static partial void PonerExtrasCap3(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Cap2ExtrasPrefabs.Campamento);
        if (prefab == null || !marcas.TryGetValue("Cap3_Claro", out var claro)) { informe.AppendLine("AVISO: falta el campamento o la marca del claro."); return; }
        var padre = Hijo(raiz, Cap3 + " · campamento");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.gameObject.scene);
        // Se desempaqueta para poder separar la pluma y el claro en sus tramos.
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(claro.position, claro.rotation);
        EnTramo(padre, Tramo(S3.Noche, S3.ContrabandistasFin), go);
        // La pluma y el claro solo cuentan cuando ya se ha visto el campamento.
        var pluma = go.transform.Find("PlumaBrillante");
        var zona = go.transform.Find("Claro");
        foreach (var t in new[] { pluma, zona })
        {
            if (t == null) continue;
            var tramo = Hijo(padre, t.name + " (tramo de la decisión)");
            tramo.position = t.position;
            t.SetParent(tramo, true);
            EnTramo(tramo, Tramo(S3.Decide, S3.ContrabandistasFin), t.gameObject);
        }
        informe.AppendLine("Cap. 3: campamento de los contrabandistas en el claro.");

        PrepararCarroDeLasJaulas(raiz, informe);
        // La hoguera de la acampada, el sitio de Will y el claro, sin flores altas: si no, tapan el
        // fuego, la cara de Will y las jaulas (INC-1035, INC-1038).
        int despejadas = DespejarHierba(new Vector3(-99.5f, 0f, -207.5f), 5.5f)
            + DespejarHierba(new Vector3(-215f, 0f, -125f), 12f)
            + DespejarHierba(new Vector3(-208f, 0f, -134f), 3f);
        informe.AppendLine($"Cap. 3: acampada y claro despejados de flores ({despejadas} celdas de hierba).");
    }

    /// El carro que pasa junto al fuego: va tras el contrabandista del seguimiento, sin colisiones (se
    /// sigue a escondidas a su lado), con una luz cálida bajo la carga tapada que sube y baja despacio.
    private static void PrepararCarroDeLasJaulas(Transform raiz, StringBuilder informe)
    {
        var carro = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PROP_Carro Cap3");
        if (carro == null) { informe.AppendLine("AVISO: falta el carro de las jaulas (PROP_Carro Cap3)."); return; }
        var arrastre = carro.GetComponent<CargaArrastrada>() ?? carro.gameObject.AddComponent<CargaArrastrada>();
        arrastre.Configurar(Contra1, 2.9f, 90f);
        EditorUtility.SetDirty(arrastre);

        var previa = carro.Find("Luz de la carga");
        if (previa != null) Object.DestroyImmediate(previa.gameObject);
        var limites = carro.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        var luzGo = new GameObject("Luz de la carga");
        luzGo.transform.SetParent(carro, false);
        // Justo encima de la paja: la ilumina por fuera (desde dentro no se vería) y alumbra alrededor.
        luzGo.transform.position = new Vector3(limites.center.x, limites.max.y + 0.4f, limites.center.z);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.86f, 0.45f);
        luz.range = 5f;
        luz.shadows = LightShadows.None;
        var respira = luzGo.AddComponent<RespiracionDeLuz>();
        respira.Configurar(luz, new Renderer[0]);
        informe.AppendLine("Cap. 3: carro de las jaulas tras el contrabandista, con la luz que respira.");
    }
}
