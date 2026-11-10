using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Capítulo 3, «La pista del mar» (GDD § 5.7, novelas V–VI): Liam habla con Tobías por el espejo,
/// Will reúne tres pistas en la aldea del mar (el pueblo pesquero) y sigue de lejos al carro de los
/// contrabandistas; en el claro decide jugando: coger la pluma y avisar a la guardia del cruce, o
/// entrar y pelear (las jaulas arden y queda el flag <see cref="FlagJaulasQuemadas"/>).
/// Adaptaciones del juego: el molino y el establo de la propuesta son la cerca del huerto y el
/// cobertizo de las redes del puerto; el puesto de guardia es el descanso del cruce.
/// </summary>
public static partial class MontajeDeLaDemo
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
        public const string Decide = "CAP3_DECIDE_EMPIEZA";
        public const string ContrabandistasFin = "CAP3_CONTRABANDISTAS_FIN";
        public const string Vencido = "CAP3_CONTRABANDISTA_VENCIDO";
    }

    private const string Posadero = "NPC_Posadero", Lucia = "NPC_Lucia", Sargento = "NPC_Sargento";
    private const string Guardia1 = "NPC_GuardiaCruce1", Guardia2 = "NPC_GuardiaCruce2";
    private const string Contra1 = "NPC_Contrabandista1", Contra2 = "NPC_Contrabandista2", Jefe = "NPC_ContrabandistaJefe";
    private const string RutaHoja = CarpetaItems + "/IT_HojaDeAvistamientos.asset";
    private const string RutaPluma = CarpetaItems + "/IT_PlumaBrillante.asset";
    private const string CombateJefe = "Assets/_NPCs/Combat/NPC_Combat_Config_BoyPirate.asset";

    private static Capitulo MontarAssetsCap3(StringBuilder informe)
    {

        // Nombres de los personajes nuevos.
        foreach (var (clave, es, en) in new[]
        {
            ("CHAR_POSADERO", "Posadero", "Innkeeper"), ("CHAR_LUCIA", "Lucía", "Lucía"),
            ("CHAR_SARGENTO", "Sargento", "Sergeant"), ("CHAR_GUARDIA_CRUCE", "Guardia", "Guard"),
            ("CHAR_CONTRABANDISTA", "Contrabandista", "Smuggler"), ("CHAR_TOBI", "Tobías", "Tobías"),
        }) TextosDeLaDemo.Poner(TextosDeLaDemo.Tabla.Dialogos, clave, es, en);

        Objeto("IT_HojaDeAvistamientos", "hoja_avistamientos", "Hoja de avistamientos", "Sightings sheet",
            "La hoja donde Will apunta lo que cuentan en la aldea del mar de los animales con aro.",
            "The sheet where Will writes down what the seaside villagers tell him about the collared animals.");

        Mision("Q_CAP3_MARCAS_DE_HIERRO", MisionCap3, "Las marcas de hierro", "The iron marks",
            "El demonio que atacó el pueblo llevaba un aro de hierro. En la aldea del mar quizá sepan algo.",
            "The demon that attacked the village wore an iron collar. The seaside village might know something.",
            ("CAP3_LLEGAR", "Llega a la aldea del mar", "Reach the seaside village"),
            ("CAP3_POSADA", "Pregunta en la posada", "Ask at the inn"),
            ("CAP3_PISTA_1", "Escucha lo que vio el posadero", "Hear what the innkeeper saw"),
            ("CAP3_PISTA_2", "Mira la cerca del huerto", "Look at the garden fence"),
            ("CAP3_PISTA_3", "Mira el cobertizo de las redes", "Look at the net shed"),
            ("CAP3_HOJA", "Cuéntaselo al posadero", "Tell the innkeeper"),
            ("CAP3_CARRO", "Sigue al carro sin que te vea", "Follow the cart without being seen"),
            ("CAP3_CLARO", "Decide qué hacer en el claro", "Decide what to do in the clearing"));

        // ── Diálogos ────────────────────────────────────────────────────────────────────────
        string dlgLuces = DialogoDeCaja("DLG_CAP3_POSADERO_LUCES", "DLG_CAP3_LUCES",
            W("¿Y usted? ¿Ha visto algo raro?", "And you? Have you seen anything strange?"),
            L("CHAR_POSADERO", "Hace tres semanas, de noche, vi luces bajas moviéndose entre los árboles del bosque.", "Three weeks ago, at night, I saw low lights moving between the forest trees."),
            L("CHAR_POSADERO", "No eran faroles. Iban a la altura de la rodilla.", "They weren't lanterns. They were knee-high."),
            W("¿Y qué hacían?", "And what were they doing?"),
            L("CHAR_POSADERO", "Se encendían y se apagaban, despacio. Como algo que respira dormido.", "They lit up and dimmed, slowly. Like something breathing in its sleep.", NPCEmotion.Worried));
        string dlgZorro = DialogoDeCaja("DLG_CAP3_LUCIA_ZORRO", "DLG_CAP3_ZORRO",
            L("CHAR_LUCIA", "¡Yo vi al zorro! Junto a la cerca del huerto.", "I saw the fox! By the garden fence.", NPCEmotion.Excited),
            L("CHAR_LUCIA", "Llevaba un collar que brillaba. No me quiso morder.", "It had a glowing collar. It didn't want to bite me."),
            L("CHAR_LUCIA", "Tenía más miedo que yo.", "It was more scared than me.", NPCEmotion.Sad));
        string dlgHuerto = DialogoDeCaja("DLG_CAP3_PISTA_HUERTO", "DLG_CAP3_HUERTO",
            W("Huellas pequeñas y pelo rojizo enganchado en la madera.", "Small tracks, and reddish fur caught on the wood.", NPCEmotion.Thinking),
            W("Se ha rascado aquí con fuerza. Como si el collar le doliera.", "It scratched itself hard here. As if the collar hurt."));
        string dlgCobertizo = DialogoDeCaja("DLG_CAP3_PISTA_COBERTIZO", "DLG_CAP3_COBERTIZO",
            W("Arañazos en la madera, a la altura del cuello de un perro.", "Scratches on the wood, at the height of a dog's neck.", NPCEmotion.Thinking),
            W("Ha dejado restos de hierro en la madera. Con las mismas líneas que el trozo del aro.", "It left traces of iron on the wood. With the same lines as the collar piece."),
            W("Y las líneas todavía brillan un poco.", "And the lines still glow a little.", NPCEmotion.Surprised));
        string dlgHoja = DialogoDeCaja("DLG_CAP3_POSADERO_HOJA", "DLG_CAP3_HOJA",
            W("Un zorro asustado, un perro que se rascaba y luces que respiran. Lo he apuntado todo.", "A frightened fox, a dog that kept scratching, and lights that breathe. I wrote it all down."),
            L("CHAR_POSADERO", "Se lo he contado a dos guardias y a un viajero. Ninguno me hizo caso.", "I told two guards and a traveller. None of them listened."),
            L("CHAR_POSADERO", "Y tú eres el primero que trae un trozo de esos aros.", "And you're the first to bring a piece of one of those collars."),
            L("CHAR_POSADERO", "Si buscas a quien hace esos aros, mira hacia el Bosque Prohibido.", "If you're looking for whoever makes those collars, look toward the Forbidden Forest."),
            L("CHAR_POSADERO", "Y no te fíes del camino de noche. Por ahí pasan carros que no paran en ningún sitio.", "And don't trust the road at night. Carts go by there that never stop anywhere.", NPCEmotion.Worried));
        string dlgSargento = DialogoDeCaja("DLG_CAP3_SARGENTO_PLUMA", "DLG_CAP3_SARGENTO",
            L("CHAR_SARGENTO", "¿Contrabandistas? ¿Por aquí? Chico, vete a dormir.", "Smugglers? Around here? Go to bed, kid.", NPCEmotion.Annoyed),
            W("Llevan pájaros en jaulas. Mire esto.", "They're carrying birds in cages. Look at this."),
            L("CHAR_SARGENTO", "Esto es una pluma de pájaro de luz. No se venden. No se pueden vender.", "This is a lightbird feather. They aren't sold. They can't be sold.", NPCEmotion.Surprised),
            L("CHAR_SARGENTO", "Vosotros dos, conmigo. Y tú, chico, enséñanos el camino.", "You two, with me. And you, kid, show us the way.", NPCEmotion.Determined));
        string fPosadero = DialogoDeCaja("DLG_CAP3_POSADERO_DESPUES", "DLG_CAP3_POSADERO_DESPUES",
            L("CHAR_POSADERO", "Si das con quien hace los aros, vuelve y cuéntamelo. Invita la casa.", "If you find whoever makes the collars, come back and tell me. On the house."));
        string fLucia = DialogoDeCaja("DLG_CAP3_LUCIA_DESPUES", "DLG_CAP3_LUCIA_DESPUES",
            L("CHAR_LUCIA", "¿Viste lo de la cuchara? Casi. Mañana llega al pulgar.", "Did you see the spoon? Almost. Tomorrow it'll reach my thumb.", NPCEmotion.Happy));
        string fSargento = DialogoDeCaja("DLG_CAP3_SARGENTO_DESPUES", "DLG_CAP3_SARGENTO_DESPUES",
            L("CHAR_SARGENTO", "Gracias a ti esos pájaros vuelan. Si pasas por el cruce, aquí estamos.", "Thanks to you those birds are flying. If you pass the crossroads, we're here."));
        string fGuardia = DialogoDeCaja("DLG_CAP3_GUARDIA", "DLG_CAP3_GUARDIA",
            L("CHAR_GUARDIA_CRUCE", "Sin novedad en el cruce.", "All quiet at the crossroads."));
        TextosDeLaDemo.Poner(TextosDeLaDemo.Tabla.Dialogos, "CAP3_AVISO_QUIEN", "¿Quién anda ahí?", "Who's there?");

        // ── Cinemáticas ─────────────────────────────────────────────────────────────────────
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_ESPEJO", Guion = "Cap3_01_ElEspejo", Escena = "TorreDeLiam",
            Nombre = "Cap. 3: Tobías en el espejo", Resumen = "Media sopa, la pesca y el dibujo de cuatro figuras. La llave del aro se calentará cerca de Will." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_POSADA", Guion = "Cap3_02_LaPosada", Texto = PresentacionDeTexto.Bocadillo,
            Nombre = "Cap. 3: la posada", Resumen = "Will pregunta por el trozo del aro, le suenan las tripas (una sopa, si alcanza) y Lucía intenta subir la cuchara." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_CARRO", Guion = "Cap3_03_ElCarro", Texto = PresentacionDeTexto.Bocadillo,
            Nombre = "Cap. 3: el carro de noche", Resumen = "Un carro con jaulas tapadas pasa junto al fuego; por la manta sale una luz que respira." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_CLARO", Guion = "Cap3_04_ElClaro", Texto = PresentacionDeTexto.Bocadillo,
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
        TextoDeCine("CAP3_ESPEJO_08", "Te he hecho un dibujo. Somos nosotros pescando. Al cuarto no le he puesto cara: todavía no sé quién es.", "I made you a drawing. It's us, fishing. I didn't give the fourth one a face: I don't know who he is yet.");
        TextoDeCine("CAP3_ESPEJO_09", "Ya lo sabrás.", "You will.");
        TextoDeCine("CAP3_ESPEJO_10", "La llave del aro. Se calentará cada vez que el chico esté cerca.", "The collar's key. It'll grow warm whenever the boy is near.");
        TextoDeCine("CAP3_ESPEJO_11", "Aguanta un poco más, Tobi.", "Hold on a little longer, Tobi.");
        TextoDeCine("CAP3_POSADA_01", "Perdone. ¿Sabe usted qué es esto?", "Excuse me. Do you know what this is?");
        TextoDeCine("CAP3_POSADA_02", "¿De dónde has sacado eso?", "Where did you get that?");
        TextoDeCine("CAP3_POSADA_03", "Lo llevaba el demonio que atacó mi pueblo.", "The demon that attacked my village was wearing it.");
        TextoDeCine("CAP3_POSADA_04", "Por aquí no han venido demonios. Pero animales con aros, sí. Varios.", "No demons have come this way. But collared animals have. Several.");
        TextoDeCine("CAP3_POSADA_05", "…¿Eso han sido tus tripas?", "…Was that your stomach?");
        TextoDeCine("CAP3_POSADA_06", "La comida de la mochila se me acabó ayer. ¿Me alcanza esto para una sopa?", "The food in my bag ran out yesterday. Is this enough for a soup?");
        TextoDeCine("CAP3_POSADA_07", "Alcanza. Pero no para pan.", "It's enough. But not for bread.");
        TextoDeCine("CAP3_POSADA_08", "¡Yo sé hacer magia! Mira la cuchara.", "I can do magic! Watch the spoon.");
        TextoDeCine("CAP3_POSADA_09", "Va a subir… subir… hasta el pulgar.", "It's going to rise… rise… all the way to my thumb.");
        TextoDeCine("CAP3_POSADA_10", "Casi.", "Almost.");
        TextoDeCine("CAP3_POSADA_11", "¿Casi? Yo a tu edad no movía ni una miga.", "Almost? At your age I couldn't move a crumb.");
        TextoDeCine("CAP3_POSADA_12", "Come algo y luego pregunta por la aldea. La gente ve cosas, aunque no las cuente.", "Eat something, then ask around the village. People see things, even if they don't say.");
        TextoDeCine("CAP3_CARRO_01", "¿Un carro, a estas horas?", "A cart, at this hour?");
        TextoDeCine("CAP3_CARRO_02", "Esa luz… sube y baja. Como si respirara.", "That light… it rises and falls. As if it were breathing.");
        TextoDeCine("CAP3_CARRO_03", "No es un farol. Vamos a ver adónde va. De lejos.", "That's no lantern. Let's see where it goes. From a distance.");
        TextoDeCine("CAP3_CLARO_01", "Doce jaulas. En la capital pagan bien por plumas que brillan.", "Twelve cages. They pay well in the capital for feathers that glow.");
        TextoDeCine("CAP3_CLARO_02", "No paran de brillar. Me ponen nervioso.", "They won't stop glowing. They make me nervous.");
        TextoDeCine("CAP3_CLARO_03", "Pues tápalos. Al amanecer, al barco.", "Then cover them. At dawn, to the boat.");
        TextoDeCine("CAP3_CLARO_04", "El perro no deja de gruñir al bosque.", "The dog won't stop growling at the forest.");
        TextoDeCine("CAP3_CLARO_05", "Me arden las manos…", "My hands are burning…");
        TextoDeCine("CAP3_CLARO_06", "«¿Debes hacerlo?»", "\"Should you?\"");
        TextoDeCine("CAP3_CLARO_07", "Eso me preguntaría Eldran.", "That's what Eldran would ask me.");
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
        M(Cap3, "Cap3_LlegadaCosta", 222f, -378f, 150f);
        M(Cap3, "Cap3_Aldea", 262f, -414f, 180f);
        M(Cap3, "Cap3_Posada", 279.0f, -425.4f, 90f);
        M(Cap3, "Cap3_Acampada", -122.5f, -214.5f, 300f);
        M(Cap3, "Cap3_Seguir_Vuelta", -112f, -224f, 300f);
        M(Cap3, "Cap3_Ruta1", -150f, -192f);
        M(Cap3, "Cap3_Ruta2", -182f, -162f);
        M(Cap3, "Cap3_Ruta3", -203f, -140f);
        M(Cap3, "Cap3_Claro", -215f, -125f, 315f);
        M(Cap3, "Cap3_Claro_Llegada", -212f, -129f, 315f);
        M(Cap3, "Cap3_Matorral", -205f, -137f, 315f);
        M(Cap3, "Cap3_Puesto", -90f, -124f, 270f);
        M(Cap3, "Cap3_Amanecer", -96f, -126f, 270f);

        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Posadero", Pos = new Vector3(281.2f, float.NaN, -424.6f), Rumbo = 250f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Lucia", Pos = new Vector3(277.2f, float.NaN, -425.0f), Rumbo = 100f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Sargento", Pos = new Vector3(-87f, float.NaN, -121f), Rumbo = 270f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_GuardiaCruce1", Pos = new Vector3(-92f, float.NaN, -118f), Rumbo = 250f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_GuardiaCruce2", Pos = new Vector3(-93f, float.NaN, -129f), Rumbo = 290f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Contrabandista1", Pos = new Vector3(-105f, float.NaN, -228f), Rumbo = 300f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Contrabandista2", Pos = new Vector3(-218f, float.NaN, -127f), Rumbo = 90f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_ContrabandistaJefe", Pos = new Vector3(-213f, float.NaN, -128f), Rumbo = 0f });

        s_papeles.Add(new Papel { SpawnId = "SPAWN_Posadero", Prefab = "Assets/_NPCs/Manuel.prefab", Nombre = "Posadero", Id = Posadero, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Lucia", Prefab = "Assets/_NPCs/AmigaDelNiñoPez.prefab", Nombre = "Lucia", Id = Lucia, Voz = "Vecina", Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Sargento", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "Sargento", Id = Sargento, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_GuardiaCruce1", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "GuardiaCruce1", Id = Guardia1, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_GuardiaCruce2", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "GuardiaCruce2", Id = Guardia2, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Contrabandista1", Prefab = "Assets/_NPCs/Pueblo/Jorge.prefab", Nombre = "Contrabandista1", Id = Contra1, Flag = FlagCap3Iniciado, NavMesh = false });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Contrabandista2", Prefab = "Assets/_NPCs/Combat/Boy_Pirate.prefab", Nombre = "Contrabandista2", Id = Contra2, Flag = FlagCap3Iniciado, NavMesh = false });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_ContrabandistaJefe", Prefab = "Assets/_NPCs/Combat/Boy_Pirate.prefab", Nombre = "ContrabandistaJefe", Id = Jefe, Flag = FlagCap3Iniciado, NavMesh = false });

        // Los contrabandistas solo están de noche; después, detenidos o huidos.
        foreach (var c in new[] { Contra1, Contra2, Jefe })
            s_agendas.Add(new Agenda
            {
                Actor = c,
                Entradas =
                {
                    ("Cap. 3 · antes de la noche", Tramo(null, S3.Noche), PresenciaEnAgenda.NoEsta, null, null),
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
        s_inspecciones.Add(new Inspeccion { Cap = Cap3, Nombre = "Pista · la cerca del huerto", Dialogo = dlgHuerto, Senal = S3.PistaHuerto,
            Pos = new Vector3(238f, float.NaN, -401f), Tramo = Tramo(S3.Pistas, S3.Noche) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap3, Nombre = "Pista · el cobertizo de las redes", Dialogo = dlgCobertizo, Senal = S3.PistaCobertizo,
            Pos = new Vector3(244f, float.NaN, -438f), Tramo = Tramo(S3.Pistas, S3.Noche) });

        // La posada es la casa de pescadores 3; la mesa es la «Mesa de encuentro» de la plaza (279, −427).
        s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "Hoguera de la acampada", Prefab = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Campfire01_a01.prefab",
            Pos = new Vector3(-124f, float.NaN, -213f), Rumbo = 0f, Tamano = 1.2f, Tramo = Tramo(S3.Noche, FinCap3) });

        // ── El grafo ────────────────────────────────────────────────────────────────────────
        var c3 = new Capitulo { Etiqueta = "Cap3", Ruta = "Assets/NarrativeGraph/Cap3.asset", Prefijo = "cap3-", Nombre = "Cap. 3" };
        c3.Recorrido = new List<Paso>
        {
            P("inicio", "Inicio del Cap. 3", new StartNode()),
            P("tras-cap2", "Tras el Cap. 2 (" + FinCap2 + ")", Esperar(FinCap2)),
            P("flag", "Comienza el Cap. 3 (aparece el reparto de la costa y del cruce)", Flag(FlagCap3Iniciado)),
            // 3.1 · Tobías
            P("espejo-carga", "3.1 · Carga la torre de Liam (probar desde aquí)", new AdditiveSceneNode { operacion = AdditiveSceneNode.Operacion.Cargar, sceneName = "TorreDeLiam", waitForCompletion = true }),
            P("espejo", "3.1 · Cinemática: Tobías en el espejo", Cinematica("CAP3_ESPEJO")),
            P("espejo-descarga", "3.1 · Descarga la torre de Liam", new AdditiveSceneNode { operacion = AdditiveSceneNode.Operacion.Descargar, sceneName = "TorreDeLiam", waitForCompletion = true }),
            // 3.2 · La aldea del mar
            P("rotulo-costa", "3.2 · Rótulo: dos días de camino", Rotulo("CAP3_ROTULO_COSTA", "Dos días de camino hacia la costa…", "Two days' walk to the coast…")),
            P("a-la-costa", "3.2 · Will llega a la costa", Llevar("Player", "Cap3_LlegadaCosta")),
            P("costa-destapa", "3.2 · Se destapa la pantalla en la costa", Destapar()),
            P("aldea", "3.2 · Hacia la aldea del mar (probar desde aquí)", Emitir(S3.Aldea)),
            P("mision", "Misión: las marcas de hierro (aviso de misión nueva)", new StartQuestNode { questId = MisionCap3, noGiver = true }),
            P("punto-aldea", "3.2 · Punto de guardado: la costa", Punto("CAP3_COSTA", "Cap3_LlegadaCosta")),
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
            P("rotulo-noche", "3.3 · Rótulo: de vuelta al camino", Rotulo("CAP3_ROTULO_NOCHE", "Esa noche, camino del bosque…", "That night, on the forest road…")),
            P("noche-hora", "3.3 · Se hace de noche", new SetTimeOfDayNode { targetTime = DayNightCycle.TimeOfDay.Night, immediate = true }),
            P("noche", "3.3 · La acampada (probar desde aquí)", Emitir(S3.Noche)),
            P("a-la-acampada", "3.3 · Will acampa junto al camino", Llevar("Player", "Cap3_Acampada")),
            P("punto-noche", "3.3 · Punto de guardado: la acampada", Punto("CAP3_ACAMPADA", "Cap3_Acampada")),
            P("carro", "3.3 · Cinemática: el carro con jaulas", Cinematica("CAP3_CARRO")),
            P("seguir-tuto", "3.3 · Aviso: seguir de lejos", Tutorial("CAP3_TUTO_SEGUIR", "Sigue al carro sin acercarte: si te ven, tendrás que volver atrás.", "Follow the cart without getting close: if they see you, you'll have to go back.")),
            P("seguir", "3.3 · Seguir al contrabandista de lejos hasta el claro", new GuiarJugadorNode
            {
                npcId = Contra1, anchorId = "Cap3_Claro_Llegada",
                puntosDePaso = new List<string> { "Cap3_Ruta1", "Cap3_Ruta2", "Cap3_Ruta3" },
                distanciaMinima = 7f, avisoAlAcercarseKey = "CAP3_AVISO_QUIEN", anclaDeVuelta = "Cap3_Seguir_Vuelta",
                distanciaMaxima = 32f, distanciaReanudar = 24f, radioLlegada = 26f, velocidad = 2.2f, duracionMaxima = 420f,
            }),
            P("paso-carro", "Paso: seguir al carro", Completar(MisionCap3, "CAP3_CARRO")),
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
        c3.Notas.Add(("nota", "Cap. 3 · La pista del mar (GDD § 5.7). Lo monta El Sendero ▸ Demo ▸ Montar los capítulos 3 y 4: los nodos «cap3-…» se rehacen. " +
                              "Escena: «Capítulos de la demo (generado)» en MainWorld (marcas Cap3_*, puntos del roster, pistas, el campamento de los contrabandistas)."));
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
    }
}
