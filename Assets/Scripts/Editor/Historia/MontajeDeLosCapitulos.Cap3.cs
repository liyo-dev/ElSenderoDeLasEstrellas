using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Liyodev.NarrativeGraph;

/// <summary>
/// Capítulo 3, «La pista del mar» (GDD § 5.7, novelas V–VI): Liam habla con Tobías por el espejo,
/// Will reúne pistas en la aldea del mar (el pueblo pesquero: lo que vio el posadero, la cerca y la casa de las nasas) y sigue a escondidas al
/// los dos contrabandistas por el puente del camino 5, el caserío abandonado y la linde del bosque (si le
/// ven, pelea contra los dos: ganar sigue la historia, perder repite el seguimiento); en el claro Will se
/// contiene y se lleva la pluma como prueba para Estela.
/// Adaptaciones del juego: el molino y el establo de la propuesta son la cerca del huerto y el
/// cobertizo de las redes del puerto; el puesto de guardia es el descanso del cruce.
/// </summary>
public static partial class MontajeDeLosCapitulos
{

    [MenuItem("El Sendero/Historia/Fijar arranque en la aldea del mar")]
    public static void FijarArranqueAldeaDelMar()
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap3.asset");
        const string id = "cap3-punto-aldea";
        if (grafo.FindNode(id) != null)
            return;

        var llegada = grafo.FindNode("cap3-llegada");
        if (llegada == null || llegada.Outputs.Count != 1 || llegada.Outputs[0] != "cap3-paso-llegar")
            throw new System.InvalidOperationException("La salida de la llegada a la aldea ha cambiado.");

        var punto = Punto("CAP3_ALDEA", "Cap3_Aldea");
        punto.Guid = id;
        punto.DisplayTitle = "3.2 · Punto de arranque: la aldea del mar";
        punto.Chapter = llegada.Chapter;
        punto.Position = llegada.Position + new Vector2(0f, 176f);
        punto.Outputs = new List<string>(llegada.Outputs);

        Undo.RecordObject(grafo, "Fijar arranque en la aldea del mar");
        grafo.nodes.Insert(grafo.nodes.IndexOf(llegada) + 1, punto);
        llegada.Outputs[0] = id;
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
    }

    private const string Cap3 = "Cap3";
    public const string MisionCap3 = "CAP3_MARCAS_DE_HIERRO";
    private const string MisionDeLaAldea = "CAP3_PISTAS_ALDEA";

    private static void MontarMisionesDeLaAldea()
    {
        Mision("Q_CAP3_PISTAS_ALDEA", MisionDeLaAldea, "Pistas en la Aldea del Mar", "Clues in the seaside village",
            "Eldran cree que Estela te escuchará si le llevas pruebas de los aros. Pregunta en la Aldea del Mar qué han visto.",
            "Eldran thinks Estela will listen if you bring proof about the collars. Ask around the seaside village to find out what people have seen.",
            ("CAP3_LLEGAR", "Llega a la aldea del mar", "Reach the seaside village"),
            ("CAP3_POSADA", "Pregunta en la posada", "Ask at the inn"),
            ("CAP3_PEZ", "Busca a Pez en la playa", "Find Pez on the beach"),
            ("CAP3_LUBINA", "Recoge el juguete de Pez en la entrada de la posada y llévaselo", "Pick up Pez's toy at the inn entrance and bring it back to him"),
            ("CAP3_FAROL", "Nada hasta la casa del farol y habla con Candela", "Swim to the lantern house and speak to Candela"));

        Mision("Q_CAP3_MARCAS_DE_HIERRO", MisionCap3, "Las marcas de hierro", "The iron marks",
            "Candela vio un carro con jaulas que brillaban. Examina la cerca del huerto y sigue el rastro hacia el bosque.",
            "Candela saw a cart carrying glowing cages. Check the garden fence and follow the trail toward the forest.",
            ("CAP3_CERCA", "Mira la cerca del huerto, camino del bosque", "Look at the garden fence, on the way to the forest"),
            ("CAP3_BOSQUE", "Ve por el camino hacia el Bosque Prohibido", "Take the road toward the Forbidden Forest"),
            ("CAP3_NOCHE", "Espera el carro de noche junto al fuego", "Wait for the cart by the fire at night"),
            ("CAP3_CARRO", "Sigue al carro sin que te vea", "Follow the cart without being seen"),
            ("CAP3_CLARO", "Averigua qué llevan en el claro", "Find out what they're carrying in the clearing"));
    }

    public const string FlagCap3Iniciado = "CAP3_INICIADO";

    private static class S3
    {
        public const string Aldea = "CAP3_ALDEA_EMPIEZA";
        public const string LlegadaAldea = "CAP3_LLEGADA_ALDEA";
        public const string Pistas = "CAP3_PISTAS_EMPIEZA";
        public const string Recado = "CAP3_RECADO_DE_PEZ";
        public const string Lubina = "CAP3_RECADO_LUBINA";
        public const string Nada = "CAP3_NADA_AL_FAROL";
        public const string Boya1 = "CAP3_BOYA_1";
        public const string Boya2 = "CAP3_BOYA_2";
        public const string Cerca = "CAP3_CERCA_EMPIEZA";
        public const string PistaHuerto = "CAP3_PISTA_HUERTO";
        public const string Noche = "CAP3_NOCHE_EMPIEZA";
        public const string LlegadaBosque = "CAP3_LLEGADA_BOSQUE";
        public const string CaeLaNoche = "CAP3_CAE_LA_NOCHE";
        public const string Decide = "CAP3_DECIDE_EMPIEZA";
        public const string ContrabandistasFin = "CAP3_CONTRABANDISTAS_FIN";
        public const string CarreteroVencido = "CAP3_CARRETERO_VENCIDO";
        public const string Huyen = "CAP3_HUYEN_AL_CAMPAMENTO";
        public const string LlegaAlClaro = "CAP3_LLEGA_AL_CLARO";
        public const string EnElClaro = "CAP3_EN_EL_CLARO";
    }

    private const string Posadero = "NPC_Posadero", Lucia = "NPC_Lucia", Sargento = "NPC_Sargento";
    private const string Pez = "NPC_Pez", Candela = "NPC_Candela";
    private const string Guardia1 = "NPC_GuardiaCruce1", Guardia2 = "NPC_GuardiaCruce2";
    private const string Contra1 = "NPC_Contrabandista1", Contra2 = "NPC_Contrabandista2";
    private const string MadreDePez = "NPC_Rosario";
    private const string RutaJugueteDePez = CarpetaItems + "/IT_JugueteDePez.asset";
    private const string RutaHoja = CarpetaItems + "/IT_HojaDeAvistamientos.asset";
    private const string IconoHoja = "Assets/Art/UI/Items/hoja_de_avistamientos.png";
    private const string RutaPluma = CarpetaItems + "/IT_PlumaBrillante.asset";
    private const string CombateDeLosContrabandistas = "Assets/_NPCs/Combat/NPC_Combat_Config_BoyPirate.asset";
    /// Música del seguimiento a escondidas. Si aún no está en el proyecto, el tramo no cambia la música.
    private const string MusicaDelSigilo = "Assets/Audio/Music/Zonas/Tras el carro.mp3";
    /// El carro del pack es una sola malla: el montaje saca de él una copia con las ruedas sueltas
    /// (que giran al avanzar, Kits.Core.WheelSpin) y es esa la que se pone en la escena.
    private const string CarroDelPack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Cart07.prefab";
    private const string CarroDeLasJaulas = "Assets/ElSendero/Content/Mundo/Atrezo/Carro de las jaulas (ruedas sueltas).prefab";
    private const string Matorral = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Tree07_b01.prefab";

    private static Capitulo MontarAssetsCap3(StringBuilder informe)
    {
        AsegurarContrabandistas(informe);
        AsegurarCarroConRuedas(informe);

        // Nombres de los personajes nuevos.
        foreach (var (clave, es, en) in new[]
        {
            ("CHAR_POSADERO", "Posadero", "Innkeeper"), ("CHAR_LUCIA", "Lucía", "Lucía"),
            ("CHAR_SARGENTO", "Sargento", "Sergeant"), ("CHAR_GUARDIA_CRUCE", "Guardia", "Guard"),
            ("CHAR_CONTRABANDISTA", "Contrabandista", "Smuggler"), ("CHAR_TOBI", "Tobías", "Tobías"),
            ("CHAR_PEZ", "Pez", "Fish"), ("CHAR_CANDELA", "Candela", "Candela"),
        }) TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, clave, es, en);

        Objeto("IT_HojaDeAvistamientos", "hoja_avistamientos", "Hoja de avistamientos", "Sightings sheet",
            "La hoja donde Will apunta lo que cuentan en la aldea del mar de los animales con aro.",
            "The sheet where Will writes down what the seaside villagers tell him about the collared animals.", IconoHoja);

        var jugueteDePez = Objeto("IT_JugueteDePez", "juguete_de_pez", "Lubina de madera", "Wooden sea bass",
            "El pez de madera que Rosario guarda para su hijo.", "The wooden fish Rosario keeps for her son.");

        MontarMisionesDeLaAldea();

        // ── Diálogos ────────────────────────────────────────────────────────────────────────
        // La charla sigue a la cinemática sin volver a hablarle (INC-1320): lo que vio, los carros y a quién preguntar.
        string dlgLuces = DialogoDeCaja("DLG_CAP3_POSADERO_LUCES", "DLG_CAP3_LUCES",
            W("¿Qué cosas raras?", "What strange things?", NPCEmotion.Thinking),
            L("CHAR_POSADERO", "Hace unas semanas, una noche que no podía dormir, vi luces en el camino del bosque. No eran faroles. Iban casi a ras de suelo, todas juntas.", "A few weeks back, one night I couldn't sleep, I saw lights on the forest road. They weren't lanterns. They were almost at ground level, all bunched together."),
            W("¿Luciérnagas?", "Fireflies?", NPCEmotion.Thinking),
            L("CHAR_POSADERO", "Las luciérnagas no se encienden todas a la vez. Aquello subía y bajaba despacio… como algo que respira dormido.", "Fireflies don't all light up at once. That rose and fell, slowly… like something breathing in its sleep.", NPCEmotion.Worried),
            L("CHAR_POSADERO", "Llevo semanas contándolo y nadie me hace caso. Ni los guardias del cruce.", "I've been telling people for weeks and nobody listens. Not even the guards at the crossroads.", NPCEmotion.Annoyed),
            W("Yo sí le creo. Se lo voy a llevar a Estela, la hechicera del Bosque Prohibido… pero antes quiero estar seguro.", "I believe you. I'm taking this to Estela, the sorceress of the Forbidden Forest… but first I want to be sure.", NPCEmotion.Determined),
            L("CHAR_POSADERO", "Entonces habla con Candela, la del farol. No duerme: cada noche enciende un farol en la ventana, por si vuelve la barca de su Andrés. Si algo pasó por ese camino, ella lo vio.", "Then talk to Candela, the one with the lantern. She doesn't sleep: every night she lights a lantern in her window, in case her Andrés's boat comes back. If something went down that road, she saw it."),
            L("CHAR_POSADERO", "Vive en la casa del agua, y allí no hay pasarela. O nadas, o nada.", "She lives in the house on the water, and there's no walkway out there. Either you swim, or nothing."),
            L("CHAR_POSADERO", "Pez te enseña, si le aguantas. Está en la playa, junto al embarcadero.", "Pez will teach you, if you can put up with him. He's on the beach by the pier."));
        string dlgLuciaCerca = DialogoDeCaja("DLG_CAP3_LUCIA_CERCA", "DLG_CAP3_LUCIA_CERCA",
            L("CHAR_LUCIA", "¡Yo también vi un carro! Bueno… vi la cerca rota por la mañana.", "I saw a cart too! Well… I saw the broken fence in the morning.", NPCEmotion.Excited),
            L("CHAR_LUCIA", "Mi padre dice que fue una vaca. ¡Las vacas no dejan paja que brilla!", "My dad says it was a cow. Cows don't leave glowing straw!", NPCEmotion.Annoyed));
        string dlgPez = DialogoDeCaja("DLG_CAP3_PEZ", "DLG_CAP3_PEZ",
            L("CHAR_PEZ", "¿Sabías que las lubinas se acercan a la orilla cuando sube la marea? Hoy he contado tres.", "Did you know sea bass come closer to shore at high tide? I counted three today."),
            W("Necesito nadar hasta la casa del farol.", "I need to swim to the lantern house."),
            L("CHAR_PEZ", "¡Te enseño con mi lubina de madera! Mi madre, Rosario, lo tiene. Está sentada junto a la puerta de la posada.", "I can teach you with my wooden sea bass! My mum, Rosario, has it. She's sitting by the inn door.", NPCEmotion.Excited));
        string dlgMadreDePez = DialogoDeCaja("DLG_CAP3_MADRE_DE_PEZ", "DLG_CAP3_MADRE_DE_PEZ",
            W("¿Me dejas la lubina de madera de Pez? Quiere enseñarme a nadar con ella.", "Can I take Pez's wooden sea bass? He wants to use it to teach me to swim."),
            L("CHAR_ROSARIO", "Toma. Dile que la pintura ya está seca y que esta vez puede mojarla.", "Here. Tell him the paint is dry and he can get it wet this time."));
        string dlgLeccion = DialogoDeCaja("DLG_CAP3_PEZ_LECCION", "DLG_CAP3_PEZ_LECCION",
            W("Aquí tienes tu lubina.", "Here's your sea bass."),
            L("CHAR_PEZ", "¡Capitana! El alumno está listo. ¿Cómo dice? ¿Pies de sardina y brazos de pulpo? ¡Pero solo tiene dos!", "Captain! Your pupil is ready. What's that? Sardine feet and octopus arms? But he only has two!", NPCEmotion.Excited),
            L("CHAR_PEZ", "Dos bastan. Estira un brazo cada vez, da pataditas y gira la cabeza para respirar. La capitana dice que puedes.", "Two will do. Reach forward with one arm at a time, kick gently, and turn your head to breathe. The captain says you can do it.", NPCEmotion.Happy));
        string dlgPezAdios = DialogoDeCaja("DLG_CAP3_PEZ_ADIOS", "DLG_CAP3_PEZ_ADIOS",
            L("CHAR_PEZ", "Sigue los barriles que flotan: te llevan a la casa del farol. Los peces los llaman boyas. Los peces son muy finos.", "Follow the floating barrels: they'll take you to the lantern house. The fish call them buoys. Fish are very refined."),
            L("CHAR_PEZ", "Candela lleva diez años encendiendo ese farol. Nadie ha vuelto a ver su barca.", "Candela has been lighting that lantern for ten years. Nobody has seen his boat since.", NPCEmotion.Sad),
            L("CHAR_PEZ", "Yo no se lo digo. Hay cosas que se oyen mejor con la luz encendida.", "I don't tell her. Some things are easier to hear with the light on."));
        // Candela, la del farol: la pista fuerte. Vio el carro cruzar hacia el bosque con algo que brillaba en las jaulas.
        string dlgCandela = DialogoDeCaja("DLG_CAP3_CANDELA", "DLG_CAP3_CANDELA",
            L("CHAR_CANDELA", "¿Andrés?… No. Perdona, muchacho. Cada vez que alguien sube esos escalones pienso que es él.", "Andrés?… No. Forgive me, lad. Every time someone climbs those steps I think it's him.", NPCEmotion.Sad),
            W("El posadero dice que usted ve el camino del bosque de noche.", "The innkeeper says you can see the forest road at night."),
            L("CHAR_CANDELA", "Desde aquí se ve todo: el mar, el puerto y el camino que sube al bosque. Enciendo el farol cada noche, por si Andrés busca la casa.", "From here you can see everything: the sea, the harbour and the road up to the forest. I light the lantern every night, in case Andrés is looking for the house."),
            L("CHAR_CANDELA", "Hace tres noches pasó un carro sin farol. Dos hombres: uno grande, metido entre las varas, y otro delante, mirando a todos lados.", "Three nights ago a cart went by with no lantern. Two men: a big one, between the shafts, and another in front, looking every which way.", NPCEmotion.Worried),
            L("CHAR_CANDELA", "Bajo la manta llevaban jaulas. Y dentro algo brillaba, se apagaba y volvía a brillar. Como mi farol cuando le falta aceite.", "Under the blanket they had cages. And inside, something glowed, went dark and glowed again. Like my lantern when it's low on oil.", NPCEmotion.Worried),
            L("CHAR_CANDELA", "Salieron por el huerto, por donde la cerca, y subieron hacia el fuego del camino del bosque.", "They went out past the garden, by the fence, and up toward the fire on the forest road."),
            W("Esta noche iré a verlo.", "Tonight I'll go and see.", NPCEmotion.Determined),
            L("CHAR_CANDELA", "Ve. Y vuelve. Que aquí ya esperamos bastante.", "Go. And come back. We've done enough waiting here."));
        string dlgHuerto = DialogoDeCaja("DLG_CAP3_PISTA_HUERTO", "DLG_CAP3_HUERTO",
            W("Rodadas hondas que salen hacia el camino del bosque… Un carro muy cargado, y sin camino.", "Deep ruts heading out toward the forest road… A heavily loaded cart, and off the track.", NPCEmotion.Thinking),
            W("Paja enganchada en la tabla rota. Todavía brilla un poco… como respira la luz que vio el posadero.", "Straw caught on the broken board. It's still glowing a little… the way the light the innkeeper saw breathes.", NPCEmotion.Surprised));
        // Al ver la cerca Will ata cabos allí mismo, ya camino del bosque (sin volver a la posada, INC-1311).
        string dlgCabos = DialogoDeCaja("DLG_CAP3_ATA_CABOS", "DLG_CAP3_CABOS",
            W("Lo que vio Candela. Las luces del posadero viajan en esas jaulas.", "What Candela saw. The innkeeper's lights travel in those cages.", NPCEmotion.Determined),
            W("Lo apunto todo en la hoja. Esta noche espero el carro junto al fuego del camino del bosque.", "I'll write it all down on the sheet. Tonight I'll wait for the cart by the fire on the forest road.", NPCEmotion.Determined));
        string fPosaderoPistas = DialogoDeCaja("DLG_CAP3_POSADERO_PISTAS", "DLG_CAP3_POSADERO_PISTAS",
            L("CHAR_POSADERO", "Pez está en el muelle, hablándole a los peces. Si te enseña, nada hasta la casa del farol. Candela no muerde… casi nunca.", "Pez is on the pier, talking to the fish. If he teaches you, swim to the lantern house. Candela doesn't bite… hardly ever."));
        string fPezRecado = DialogoDeCaja("DLG_CAP3_PEZ_RECADO", "DLG_CAP3_PEZ_RECADO",
            L("CHAR_PEZ", "Mi madre está sentada junto a la puerta de la posada. Pídele mi lubina de madera.", "My mum is sitting by the inn door. Ask her for my wooden sea bass."));
        string fPezBoyas = DialogoDeCaja("DLG_CAP3_PEZ_BOYAS", "DLG_CAP3_PEZ_BOYAS",
            L("CHAR_PEZ", "¡Pez de honor! Sigue los barriles hasta la casa del farol. Y no le hables a Candela de peces: no le gustan.", "Honorary fish! Follow the barrels to the lantern house. And don't talk to Candela about fish: she doesn't like them.", NPCEmotion.Happy));
        string fPezDespues = DialogoDeCaja("DLG_CAP3_PEZ_DESPUES", "DLG_CAP3_PEZ_DESPUES",
            L("CHAR_PEZ", "Los peces dicen que esta noche vas a hacer algo valiente. Los peces exageran mucho.", "The fish say you're going to do something brave tonight. Fish exaggerate a lot."));
        string fCandela = DialogoDeCaja("DLG_CAP3_CANDELA_DESPUES", "DLG_CAP3_CANDELA_DESPUES",
            L("CHAR_CANDELA", "Ve. Y vuelve. Que aquí ya esperamos bastante.", "Go. And come back. We've done enough waiting here."));
        string fPosadero = DialogoDeCaja("DLG_CAP3_POSADERO_DESPUES", "DLG_CAP3_POSADERO_DESPUES",
            L("CHAR_POSADERO", "Cuando sepas quién hace esos aros, vuelve y me lo cuentas. La sopa la pongo yo.", "When you find out who makes those collars, come back and tell me. The soup's on me."));
        string fLucia = DialogoDeCaja("DLG_CAP3_LUCIA_DESPUES", "DLG_CAP3_LUCIA_DESPUES",
            L("CHAR_LUCIA", "¡Esta mañana la cuchara ha subido un dedo más! ¡Un dedo entero!", "This morning the spoon went up one more finger! A whole finger!", NPCEmotion.Excited));
        string fSargento = DialogoDeCaja("DLG_CAP3_SARGENTO_DESPUES", "DLG_CAP3_SARGENTO_DESPUES",
            L("CHAR_SARGENTO", "¿Contrabandistas por el camino del bosque? Lo que nos faltaba. Si los vuelves a ver, avísanos.", "Smugglers on the forest road? That's all we needed. If you see them again, let us know."));
        string fGuardia = DialogoDeCaja("DLG_CAP3_GUARDIA", "DLG_CAP3_GUARDIA",
            L("CHAR_GUARDIA_CRUCE", "Sin novedad en el cruce.", "All quiet at the crossroads."));
        string dlgPillado = DialogoDeCaja("DLG_CAP3_CARRETERO_PILLADO", "DLG_CAP3_PILLADO",
            L("CHAR_CONTRABANDISTA", "¿Nos venías siguiendo, crío? Mala idea.", "Were you following us, kid? Bad idea.", NPCEmotion.Angry),
            W("Esa luz de la paja no es un farol. ¿Qué lleváis ahí?", "That light in the straw is no lantern. What are you carrying?", NPCEmotion.Determined));
        string dlgVencido = DialogoDeCaja("DLG_CAP3_CARRETERO_VENCIDO", "DLG_CAP3_CARRETERO_VENCIDO",
            L("CHAR_CONTRABANDISTA", "Ay… mi cabeza… ¡Vámonos, al campamento!", "Ow… my head… Let's go, back to camp!", NPCEmotion.Confused),
            W("Van hacia el bosque. Ahí tienen que tener algo más.", "They're heading into the forest. They must have something more there.", NPCEmotion.Determined));

        // ── Cinemáticas ─────────────────────────────────────────────────────────────────────
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_ESPEJO", Guion = "Cap3_01_ElEspejo", Escena = "TorreDeLiam",
            Nombre = "Cap. 3: Tobías en el espejo", Resumen = "Media sopa y la promesa de ir a pescar. Will se ha llevado un trozo del aro de Liam: tendrá que seguirle de cerca." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_POSADA", Guion = "Cap3_02_LaPosada", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: la posada", Resumen = "Will pregunta por el trozo del aro, le suenan las tripas (una sopa, si alcanza) y Lucía intenta subir la cuchara." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_CARRO", Guion = "Cap3_03_ElCarro", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: llegan los contrabandistas", Resumen = "Will espera al carro junto al fuego; al oír las ruedas se esconde tras un matorral. Llegan los dos contrabandistas con el carro tapado; el vigía ve las brasas y mira alrededor, y siguen hacia el puente. Will decide seguirlos para ver si los animales llevan aro." });
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_CLARO", Guion = "Cap3_04_ElClaro", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: el claro", Resumen = "Doce jaulas de animales con aro. A Will le arden las manos: «¿Debes hacerlo?»." });

        // Textos de las cinemáticas (tabla de subtítulos).
        TextoDeCine("CAP3_ESPEJO_01", "Hoy me he tomado media sopa.", "I had half my soup today.");
        TextoDeCine("CAP3_ESPEJO_02", "¿Y la otra media?", "And the other half?");
        TextoDeCine("CAP3_ESPEJO_03", "La otra media ha ganado.", "The other half won.");
        TextoDeCine("CAP3_ESPEJO_04", "Tobi… esa tos.", "Tobi… that cough.");
        TextoDeCine("CAP3_ESPEJO_05", "Cuando vuelva iremos a pescar. Te lo prometo.", "When I get back we'll go fishing. I promise.");
        TextoDeCine("CAP3_ESPEJO_06", "No sabes pescar.", "You don't know how to fish.");
        TextoDeCine("CAP3_ESPEJO_07", "Aprenderé. Dicen que el truco es tener paciencia.", "I'll learn. They say the trick is patience.");
        TextoDeCine("CAP3_ESPEJO_10", "El chico se ha llevado un trozo de mi aro. Tendré que seguirle de cerca.", "The boy took a piece of my collar. I'll have to follow him closely.");
        TextoDeCine("CAP3_ESPEJO_11", "Aguanta un poco más, Tobi.", "Hold on a little longer, Tobi.");
        TextoDeCine("CAP3_POSADA_01", "Perdone… ¿sabe qué es esto?", "Excuse me… do you know what this is?");
        TextoDeCine("CAP3_POSADA_02", "¿De dónde has sacado eso?", "Where did you get that?");
        TextoDeCine("CAP3_POSADA_03", "Lo llevaba puesto el demonio que atacó mi pueblo.", "The demon that attacked my village was wearing it.");
        TextoDeCine("CAP3_POSADA_04", "Demonios por aquí, no. Pero cosas raras de noche… unas cuantas.", "No demons around here. But strange things at night… quite a few.");
        TextoDeCine("CAP3_POSADA_05", "…¿Eso han sido tus tripas?", "…Was that your stomach?");
        TextoDeCine("CAP3_POSADA_06", "Se me acabó la comida ayer… ¿Me llega con esto para una sopa?", "My food ran out yesterday… Is this enough for a soup?");
        TextoDeCine("CAP3_POSADA_07", "Para sopa, sí. Para pan, no.", "For soup, yes. For bread, no.");
        TextoDeCine("CAP3_POSADA_08", "¿Quieres ver magia? Mira la cuchara.", "Want to see some magic? Watch the spoon.");
        TextoDeCine("CAP3_POSADA_09", "Sube… sube… hasta el pulgar…", "Up… up… all the way to my thumb…");
        TextoDeCine("CAP3_POSADA_10", "Casi.", "Almost.");
        TextoDeCine("CAP3_POSADA_11", "¿Casi? Yo a tu edad no movía ni una miga.", "Almost? At your age I couldn't move a crumb.");
        TextoDeCine("CAP3_POSADA_12", "Tómate la sopa y luego pregunta por ahí. Aquí la gente ve muchas cosas, aunque luego no las cuente.", "Have your soup, then ask around. People here see plenty, even if they don't say so.");
        TextoDeCine("CAP3_POSADA_13", "¿De verdad? ¡Pues mañana llego al pulgar!", "Really? Then tomorrow I'll reach my thumb!");
        TextoDeCine("CAP3_CARRO_01", "Ahí vienen. El posadero tenía razón.", "Here they come. The innkeeper was right.");
        TextoDeCine("CAP3_CARRO_04", "Brasas recientes. Aquí ha estado alguien.", "Fresh embers. Someone's been here.");
        TextoDeCine("CAP3_CARRO_05", "Algún pastor. Vamos, que nos esperan antes del alba.", "Some shepherd. Come on, they're expecting us before dawn.");
        TextoDeCine("CAP3_CARRO_02", "Algo brilla bajo la paja… Si llevan animales, quiero ver si tienen aro.", "Something's glowing under the straw… If they're carrying animals, I want to see if they wear collars.");
        TextoDeCine("CAP3_CARRO_03", "Tengo que ver adónde van. Sin que me vean.", "I have to see where they're going. Without being seen.");
        TextoDeCine("CAP3_CLARO_01", "Doce jaulas. En la capital pagan bien por animales que brillan.", "Twelve cages. They pay well in the capital for animals that glow.");
        TextoDeCine("CAP3_CLARO_02", "No paran de brillar. Me ponen nervioso.", "They won't stop glowing. They make me nervous.");
        TextoDeCine("CAP3_CLARO_03", "Pues tápalos. Al amanecer, al barco.", "Then cover them. At dawn, to the boat.");
        // INC-1287: fuera el perro (no hay perro en el claro). Will confirma lo que venía a ver: llevan aro.
        TextoDeCine("CAP3_CLARO_08", "Llevan aro. Todos.", "They're collared. All of them.");
        TextoDeCine("CAP3_CLARO_05", "Podría entrar ahora mismo y sacarlos de ahí…", "I could go in right now and get them out of there…");
        TextoDeCine("CAP3_CLARO_06", "Pero son dos. Y si el fuego se me escapa, las jaulas arden con los animales dentro.", "But there are two of them. And if the fire gets away from me, the cages burn with the animals inside.");
        TextoDeCine("CAP3_CLARO_07", "Eldran siempre lo dice: que puedas hacer algo no quiere decir que debas.", "Eldran always says it: being able to do something doesn't mean you should.");

        // ── Marcas, roster y escena ─────────────────────────────────────────────────────────
        M(Cap3, "Cap3_Aldea", 262f, -414f, 180f);
        M(Cap3, "Cap3_Posada", 279.0f, -425.4f, 90f);
        // La acampada, en la orilla este junto al camino que cruza el puente del camino 5 (el cauce queda al oeste). Ver INC-1022.
        M(Cap3, "Cap3_Acampada", -101f, -208.5f, 40f);
        // Detrás del matorral de antes del puente, al otro lado del camino: ahí se esconde Will en la
        // cinemática de la llegada y ahí empieza (y vuelve a empezar, si le pillan) el seguimiento.
        M(Cap3, "Cap3_Escondite_Hoguera", -109.5f, -212.5f, 145f);
        // El seguimiento a escondidas: el puente (lo abierto), el caserío abandonado (paredes y casas sin
        // tejado) y la linde del Bosque Prohibido (troncos gordos). En las cuatro paradas miran atrás.
        // Donde acaban los dos al salir de la cinemática, camino del puente: el del carro y, delante, el vigía.
        M(Cap3, "Cap3_Sigilo_SalidaNpc", -110f, -222.3f, 235f);
        M(Cap3, "Cap3_Sigilo_SalidaVigia", -112.5f, -224.1f, 235f);
        // Rondas del vigía: en el caserío rodea la casa del este (la que da al camino por el que se
        // llega); en la linde vuelve sobre sus pasos y pasa junto al matorral de la salida del caserío.
        M(Cap3, "Cap3_Ronda_Caserio1", -159.5f, -200.5f);
        M(Cap3, "Cap3_Ronda_Caserio2", -159.5f, -187f);
        M(Cap3, "Cap3_Ronda_Caserio3", -172.5f, -186.5f);
        M(Cap3, "Cap3_Ronda_Linde1", -185f, -177.5f);
        M(Cap3, "Cap3_Puente", -129f, -235f);
        // El puente se cruza a nado (INC-1320): pasado el puente se paran mirando atrás, hacia él, hasta que
        // Will llega a la orilla de enfrente (a su escondite) pasando por debajo del tablero, que le tapa.
        // La parada y la orilla, a comprobar en Play; el hueco bajo el puente lo mide ColocarLoDelAgua
        // sobre el tablero (la marca Cap3_BajoElPuente solo vale si no encuentra el puente).
        M(Cap3, "Cap3_TrasElPuente", -138f, -233f);
        M(Cap3, "Cap3_OrillaOeste", -135f, -240f);
        M(Cap3, "Cap3_BajoElPuente", -126f, -235f, 0f);
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
        // Su sitio en el campamento, el mismo que les da la cinemática del claro (p_c1, p_c2).
        M(Cap3, "Cap3_Campamento_Grandote", -213f, -128.5f, 254f);
        M(Cap3, "Cap3_Campamento_Vigia", -216.5f, -129.5f, 74f);
        M(Cap3, "Cap3_Puesto", -90f, -124f, 270f);

        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Posadero", Pos = new Vector3(281.2f, float.NaN, -424.6f), Rumbo = 250f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Lucia", Pos = new Vector3(277.2f, float.NaN, -425.0f), Rumbo = 100f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Sargento", Pos = new Vector3(-87f, float.NaN, -121f), Rumbo = 270f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_GuardiaCruce1", Pos = new Vector3(-92f, float.NaN, -118f), Rumbo = 250f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_GuardiaCruce2", Pos = new Vector3(-93f, float.NaN, -129f), Rumbo = 290f });
        // Los dos contrabandistas aparecen en su campamento del claro, lejos del fuego: la historia solo
        // trae a un personaje donde el jugador no le ve aparecer (a más de 25 m). La cinemática de la
        // llegada los pone después en el camino.
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Contrabandista1", Pos = new Vector3(-213f, float.NaN, -128f), Rumbo = 0f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap3, SpawnId = "SPAWN_Contrabandista2", Pos = new Vector3(-216.5f, float.NaN, -129.5f), Rumbo = 90f });

        s_papeles.Add(new Papel { SpawnId = "SPAWN_Posadero", Prefab = CarpetaAldea + "/Posadero.prefab", Nombre = "Posadero", Id = Posadero, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Lucia", Prefab = CarpetaAldea + "/Lucia.prefab", Nombre = "Lucia", Id = Lucia, Voz = "Vecina", Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Sargento", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "Sargento", Id = Sargento, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_GuardiaCruce1", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "GuardiaCruce1", Id = Guardia1, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_GuardiaCruce2", Prefab = "Assets/_NPCs/Guard.prefab", Nombre = "GuardiaCruce2", Id = Guardia2, Flag = FlagCap3Iniciado });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Contrabandista1", Prefab = GrandoteContrabandista, Nombre = "Contrabandista1", Id = Contra1, Flag = FlagCap3Iniciado, NavMesh = false });
        s_papeles.Add(new Papel { SpawnId = "SPAWN_Contrabandista2", Prefab = VigiaContrabandista, Nombre = "Contrabandista2", Id = Contra2, Flag = FlagCap3Iniciado, NavMesh = false });

        // Los dos contrabandistas aparecen cuando anochece con Will junto al fuego. Si le pillan y pierden,
        // huyen a su campamento; se van del claro cuando Will ya está lejos (al llegar al claro de Estela).
        foreach (var c in new[] { Contra1, Contra2 })
            s_personajes.Add(new SituacionesDe
            {
                Actor = c,
                Entradas =
                {
                    ("Cap. 3 · antes de la noche", Tramo(null, S3.CaeLaNoche), ActorPresence.Away, null, null),
                    ("Cap. 3 · huyen al campamento", Tramo(S3.Huyen, S3.EnElClaro), ActorPresence.AtMark,
                        c == Contra1 ? "Cap3_Campamento_Grandote" : "Cap3_Campamento_Vigia", null),
                    ("Cap. 3–4 · se han ido del claro", Tramo(S3.ContrabandistasFin), ActorPresence.Away, null, null),
                },
            });
        s_personajes.Add(new SituacionesDe
        {
            Actor = Posadero,
            Entradas =
            {
                // Mientras Will busca pistas, el posadero recuerda dónde mirar (quien encarga tiene frase en curso).
                ("Cap. 3 · las pistas", Tramo(S3.Pistas, S3.Noche), ActorPresence.Unchanged, null, fPosaderoPistas),
                ("Cap. 3 · camino del bosque", Tramo(S3.Noche), ActorPresence.Unchanged, null, fPosadero),
            },
        });
        s_personajes.Add(new SituacionesDe
        {
            Actor = Lucia,
            Entradas =
            {
                ("Cap. 3 · las pistas", Tramo(S3.Pistas, S3.Noche), ActorPresence.Unchanged, null, dlgLuciaCerca),
                ("Cap. 3 · después", Tramo(S3.Noche), ActorPresence.Unchanged, null, fLucia),
            },
        });
        // Quien encarga tiene frase mientras dura lo suyo: Pez, hasta el recado y luego con las boyas; Candela, después.
        s_personajes.Add(new SituacionesDe
        {
            Actor = Pez,
            Entradas =
            {
                ("Cap. 3 · el recado de Pez", Tramo(S3.Recado, S3.Lubina), ActorPresence.Unchanged, null, fPezRecado),
                ("Cap. 3 · pez de honor", Tramo(S3.Nada, S3.Noche), ActorPresence.Unchanged, null, fPezBoyas),
                ("Cap. 3 · esta noche", Tramo(S3.Noche, FinCap3), ActorPresence.Unchanged, null, fPezDespues),
            },
        });
        s_personajes.Add(new SituacionesDe { Actor = Candela, Entradas = { ("Cap. 3 · tras su pista", Tramo(S3.Cerca, FinCap3), ActorPresence.Unchanged, null, fCandela) } });
        s_personajes.Add(new SituacionesDe
        {
            Actor = Sargento,
            Entradas =
            {
                ("Cap. 3 · vuelve al cruce", Tramo(S3.ContrabandistasFin), ActorPresence.AtMark, "Cap3_Puesto", fSargento),
            },
        });
        foreach (var g in new[] { Guardia1, Guardia2 })
            s_personajes.Add(new SituacionesDe { Actor = g, Entradas = { ("Cap. 3 · el cruce", Tramo(null), ActorPresence.Unchanged, null, fGuardia) } });

        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Llegada a la aldea del mar", Senal = S3.LlegadaAldea, Mision = MisionDeLaAldea,
            Pos = new Vector3(262f, float.NaN, -414f), Radio = 16f, Tramo = Tramo(S3.Aldea, S3.Pistas) });
        // El sitio junto al fuego del camino del bosque; el minimapa lo marca hasta que anochece con Will allí.
        // Tras ganar al contrabandista del seguimiento, Will sigue solo hasta el claro.
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Llegada al claro de los contrabandistas", Senal = S3.LlegaAlClaro, Mision = MisionCap3,
            Pos = new Vector3(-207f, float.NaN, -138f), Radio = 12f, Tramo = Tramo(S3.CarreteroVencido, S3.EnElClaro) });
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Llegada al camino del bosque", Senal = S3.LlegadaBosque, Mision = MisionCap3,
            Pos = new Vector3(-102f, float.NaN, -209f), Radio = 18f, Tramo = Tramo(S3.Noche, S3.CaeLaNoche) });
        // La cerca del huerto: la salida de la aldea hacia el bosque, la última pista (tras la de Candela).
        s_inspecciones.Add(new Inspeccion { Cap = Cap3, Nombre = "Pista · la cerca del huerto", Dialogo = dlgHuerto, Senal = S3.PistaHuerto, Mision = MisionCap3, Paso = 0,
            Pos = new Vector3(231f, float.NaN, -399.6f), Tramo = Tramo(S3.Cerca, S3.Noche) });
        // Las boyas de Pez, camino de la casa del farol: una tras otra en el minimapa. Su sitio lo pone ColocarLoDelAgua.
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Boya 1 (Pez)", Senal = S3.Boya1, Mision = MisionDeLaAldea,
            Pos = new Vector3(PuntaDelMuelle.x - 8f, 0f, PuntaDelMuelle.y), Radio = 3.5f, Tramo = Tramo(S3.Nada, S3.Boya1, S3.Cerca) });
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "Boya 2 (Pez)", Senal = S3.Boya2, Mision = MisionDeLaAldea,
            Pos = new Vector3(PuntaDelMuelle.x - 16f, 0f, PuntaDelMuelle.y), Radio = 3.5f, Tramo = Tramo(S3.Boya1, S3.Boya2, S3.Cerca) });

        // La posada es la casa de pescadores 3; la mesa es la «Mesa de encuentro» de la plaza (279, −427).
        // Hoguera de descanso (CampfireRestInteractable): descansar en ella hace pasar el tiempo hasta la noche.
        s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "Hoguera de la acampada", Prefab = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/BuildingUtilityDeco/Fire01.prefab",
            Pos = new Vector3(-99.5f, float.NaN, -206.5f), Rumbo = 0f, Tamano = 1.7f, Colision = true, Tramo = Tramo(S3.Noche, FinCap3) });
        // El carro de las jaulas: lo lleva el contrabandista del seguimiento tirando de él (CargaArrastrada,
        // en PonerExtrasCap3), con la carga tapada con paja y una luz que respira en ella. Desde que anochece junto al
        // fuego hasta que Will llega al claro, donde está el carro del campamento (INC-1036).
        s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "PROP_Carro Cap3", Prefab = CarroDeLasJaulas,
            Pos = new Vector3(-77.6f, float.NaN, -198.6f), Rumbo = 235f, Tamano = 4.2f, Tramo = Tramo(S3.CaeLaNoche, S3.EnElClaro) });
        // Matorrales donde no se ve al jugador, en los huecos sin pared ni árbol del seguimiento.
        foreach (var (nombre, x, z, rumbo) in new[]
        {
            ("antes del puente", -108f, -215f, 20f),
            ("orilla oeste del puente", -135f, -240f, 120f),
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
            P("punto-aldea", "3.2 · Punto de arranque: la aldea del mar", Punto("CAP3_ALDEA", "Cap3_Aldea")),
            P("paso-llegar", "Paso: llegar a la aldea", Completar(MisionDeLaAldea, "CAP3_LLEGAR")),
            P("posadero", "3.2 · Hablar con el posadero", Hablar(Posadero)),
            P("posada", "3.2 · Cinemática: la posada, la cuchara y el trozo del aro", Cinematica("CAP3_POSADA")),
            // La charla sigue sin volver a hablarle: lo que vio, los carros y dónde mirar (INC-1320).
            P("posada-luces", "3.2 · El posadero sigue: las luces que respiran, los carros de noche y dónde mirar", Dialogo(dlgLuces, Posadero)),
            P("paso-posada", "Paso: preguntar en la posada", Completar(MisionDeLaAldea, "CAP3_POSADA")),
            P("hoja", "3.2 · Will saca su hoja para apuntar lo que vea", Dar(RutaHoja)),
            // El juguete vuelve a Pez antes de la lección y del cruce a nado.
            P("pistas", "3.2 · Pez, Candela y la cerca (probar desde aquí)", Emitir(S3.Pistas)),
            P("pez-hablar", "3.2 · Hablar con Pez en la playa", Hablar(Pez)),
            P("pez-dlg", "3.2 · Pez pide su lubina de madera", Dialogo(dlgPez, Pez)),
            P("paso-pez", "Paso: buscar a Pez", Completar(MisionDeLaAldea, "CAP3_PEZ")),
            P("recado", "3.2 · El juguete de Pez", Emitir(S3.Recado)),
            P("lubina", "3.2 · Hablar con Rosario en la entrada de la posada", Hablar(MadreDePez)),
            P("madre-pez-dlg", "3.2 · Rosario entrega el juguete", Dialogo(dlgMadreDePez, MadreDePez)),
            P("juguete-pez", "3.2 · Recoger la lubina de madera", Dar(RutaJugueteDePez)),
            P("juguete-recogido", "3.2 · Ya tiene el juguete", Emitir(S3.Lubina)),
            P("pez-entrega", "3.2 · Volver a hablar con Pez", Hablar(Pez)),
            P("pez-recibe-juguete", "3.2 · Entregar la lubina de madera",
                new RequireInventoryItemNode { Item = jugueteDePez }, "paso-lubina", "lubina"),
            P("paso-lubina", "Paso: devolver el juguete", Completar(MisionDeLaAldea, "CAP3_LUBINA")),
            P("leccion", "3.2 · Pez juega con su lubina y le enseña a nadar", Dialogo(dlgLeccion, Pez)),
            P("nadar", "3.2 · Pez de honor: aprende a nadar (aviso de habilidad)", DesbloquearHabilidad(AbilityKey.Swim, "CAP3_NADAR_APRENDIDO")),
            P("pez-adios", "3.2 · Pez se despide: las boyas y el farol de Candela", Dialogo(dlgPezAdios, Pez)),
            P("nada", "3.2 · A nado, siguiendo las boyas", Emitir(S3.Nada)),
            P("candela-hablar", "3.2 · Hablar con Candela en su porche", Hablar(Candela)),
            P("candela-dlg", "3.2 · Candela: el carro sin farol y las jaulas que brillaban", Dialogo(dlgCandela, Candela)),
            P("paso-farol", "Paso: hablar con Candela (cierra Pistas en la Aldea del Mar)", Completar(MisionDeLaAldea, "CAP3_FAROL", cerrar: true)),
            P("mision-marcas", "Misión: las marcas de hierro", new StartQuestNode { QuestId = MisionCap3, NoGiver = true }),
            P("cerca-empieza", "3.2 · La cerca del huerto, de salida hacia el bosque", Emitir(S3.Cerca)),
            P("cerca", "3.2 · La cerca rota: rodadas y paja que brilla", Esperar(S3.PistaHuerto)),
            P("paso-cerca", "Paso: la cerca", Completar(MisionCap3, "CAP3_CERCA")),
            P("cabos", "3.2 · Will ata cabos donde está (sin volver a la posada)", Dialogo(dlgCabos)),
            // 3.3 · Los contrabandistas
            // Will va andando hasta el fuego del camino del bosque y espera la noche (pasa sola o descansando en la hoguera).
            P("noche", "3.3 · Ir andando al camino del bosque (probar desde aquí)", Emitir(S3.Noche)),
            P("bosque-llega", "3.3 · Llega al camino del bosque", Esperar(S3.LlegadaBosque)),
            P("paso-bosque", "Paso: ir hacia el bosque", Completar(MisionCap3, "CAP3_BOSQUE")),
            P("bosque-dice", "3.3 · Will: los carros pasan de noche", Bocadillo("CAP3_BOC_ESPERAR", "El posadero dijo que esos carros pasan de noche. Esperaré aquí, junto al fuego.", "The innkeeper said those carts come by at night. I'll wait here, by the fire.", 3.5f)),
            P("hora", "3.3 · ¿Ya es de noche al llegar?", GameStepNode.For(new PedirSegunHoraDelDia { franja = DayNightCycle.TimeOfDay.Night }), "tuto-noche", "hoguera-tuto"),
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
            P("esperar-noche", "3.3 · Esperar la noche junto al fuego", GameStepNode.For(new PedirEsperarHoraDelDia { franja = DayNightCycle.TimeOfDay.Night, anchorId = "Cap3_Acampada", radio = 6f })),
            P("cae-noche", "3.3 · Anochece junto al fuego (aparecen los contrabandistas)", Emitir(S3.CaeLaNoche)),
            P("paso-noche", "Paso: esperar la noche", Completar(MisionCap3, "CAP3_NOCHE")),
            P("punto-noche", "3.3 · Punto de guardado: el camino del bosque", Punto("CAP3_ACAMPADA", "Cap3_Acampada")),
            P("carro", "3.3 · Cinemática: llega el carro que esperaba", Cinematica("CAP3_CARRO")),
            P("seguir-tuto", "3.3 · Aviso: seguir sin que te vean", Tutorial("CAP3_TUTO_SEGUIR",
                "Síguelos sin que te vean. Cuando se paren a mirar atrás, o si a uno le sale un «?» sobre la cabeza, escóndete en un matorral o detrás de una pared antes de que se apague.",
                "Follow them without being seen. When they stop to look back, or one of them gets a «?» over his head, hide in a bush or behind a wall before it fades.")),
            // Los dos vigilan: el del carro (al que se sigue) y el vigía, que va delante. Tramos: el
            // puente (aprender a esperar la mirada), el caserío (el vigía rodea una casa), la linde (el
            // vigía vuelve sobre sus pasos y pasa junto a un escondite) y la llegada al claro.
            // Si te pillan, el tramo vuelve a empezar desde el escondite de la hoguera.
            P("seguir", "3.3 · Seguir a los contrabandistas a escondidas hasta el claro (probar desde aquí)", GameStepNode.For(new PedirSeguirSinSerVisto
            {
                npcId = Contra1, anchorId = "Cap3_Sigilo_Fin",
                puntosDePaso = new List<string> { "Cap3_Puente", "Cap3_TrasElPuente", "Cap3_Ruta1", "Cap3_Caserio", "Cap3_Ruta2", "Cap3_Linde", "Cap3_Ruta3" },
                paradasParaMirar = new List<string> { "Cap3_TrasElPuente", "Cap3_Caserio", "Cap3_Linde", "Cap3_Ruta3" },
                acompanantes = new List<PedirSeguirSinSerVisto.Acompanante>
                {
                    new PedirSeguirSinSerVisto.Acompanante
                    {
                        npcId = Contra2, adelanto = 3f, salida = "Cap3_Sigilo_SalidaVigia",
                        rondas = new List<PedirSeguirSinSerVisto.Ronda>
                        {
                            new PedirSeguirSinSerVisto.Ronda { enLaParada = "Cap3_Caserio", marcas = new List<string> { "Cap3_Ronda_Caserio1", "Cap3_Ronda_Caserio2", "Cap3_Ronda_Caserio3" } },
                            new PedirSeguirSinSerVisto.Ronda { enLaParada = "Cap3_Linde", marcas = new List<string> { "Cap3_Ronda_Linde1" } },
                        },
                    },
                },
                // Tras el puente miran atrás hasta que Will llega a la orilla de enfrente: por encima le verían.
                esperas = new List<PedirSeguirSinSerVisto.EsperaEnParada>
                {
                    new PedirSeguirSinSerVisto.EsperaEnParada { enLaParada = "Cap3_TrasElPuente", hastaQueElJugadorLlegueA = "Cap3_OrillaOeste", radio = 3f },
                },
                // El del carro va entre las varas: en las paradas mira atrás el vigía.
                elQueSeSigueNoSeVuelve = true,
                // El carro va a paso de carro. Will solo se agacha: va a su velocidad de siempre y nadie
                // le espera. Si notan algo, el aviso «?» sobre la cabeza se apaga en 3 s: tiempo para
                // esconderse. Si Will llega antes al claro, el tramo acaba igual (INC-1402, INC-1404).
                segundosMirando = 3f, velocidad = 1.8f, velocidadDelJugador = 0f,
                alcanceDeLaVista = 14f, anguloDeLaVista = 110f, distanciaDeOido = 4f,
                segundosDeAlerta = 3f, iconoDeAlerta = Cargar<Game.NPC.Modules.NPCCombatConfig>(ConfigDeLosContrabandistas)?.questionIconPrefab,
                distanciaParaEsperar = 0f, radioDeLlegada = 25f, radioDelJugadorEnLaMarca = 8f, duracionMaxima = 600f,
                salidaDelNpc = "Cap3_Sigilo_SalidaNpc", salidaDelJugador = "Cap3_Escondite_Hoguera", distanciaParaRecolocar = 20f,
                musica = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaDelSigilo),
            }), "paso-carro", "pillado"),
            P("paso-carro", "Paso: seguir al carro", Completar(MisionCap3, "CAP3_CARRO")),
            P("en-el-claro", "3.3 · Will llega al claro", Emitir(S3.EnElClaro)),
            P("claro", "3.3 · Cinemática: el claro y «¿Debes hacerlo?»", Cinematica("CAP3_CLARO")),
            P("decide", "3.3 · Tras el claro: la pluma (probar desde aquí)", Emitir(S3.Decide)),
            P("pista-pluma", "3.3 · Will ve la pluma de la zarza", Bocadillo("CAP3_BOC_ZARZA", "Hay una pluma enganchada en esa zarza…", "There's a feather caught in that bramble…")),
            // Will se contiene: coge la pluma, la prueba que lleva a Estela.
            P("pluma-0", "3.3 · Coge la pluma brillante", new WaitForItemAddedNode { ItemToWaitFor = Cargar<ItemData>(RutaPluma), CompleteIfAlreadyOwned = true }),
            P("pluma-1", "3.3 · «Con esto, Estela tendrá que escucharme»", Bocadillo("CAP3_BOC_PRUEBA", "Una pluma de pájaro de luz. Con esto, Estela tendrá que escucharme.", "A lightbird feather. With this, Estela will have to listen to me.", 3.5f)),
            // Final del capítulo: sigue de seguido, sin rótulo ni salto de hora; el Cap. 4 arranca donde está Will.
            // Los contrabandistas se quedan en su campamento («al amanecer, al barco»): no desaparecen
            // delante de Will. Se van cuando él ya está lejos (CAP3_CONTRABANDISTAS_FIN, en el Cap. 4).
            P("paso-claro", "Paso: el claro (cierra la misión)", Completar(MisionCap3, "CAP3_CLARO", cerrar: true)),
            P("fin-punto", "Cap. 3 · Fin (punto de guardado)", Punto("CAP3_FIN", "Cap3_Matorral")),
            P("fin", "Fin del Cap. 3 (" + FinCap3 + ")", Emitir(FinCap3)),
        };
        // La despedida en la salida del pueblo, el camino a pie y la acampada con Tobías (INC-967).
        c3.Recorrido.InsertRange(c3.Recorrido.FindIndex(p => p.Id == "aldea"), PasosDeLaDespedidaYElCamino());
        // Si le ven: pelean contra los dos. Ganar los hace huir al campamento y Will sigue solo hasta el
        // claro; perder repite el seguimiento desde el escondite de la hoguera.
        c3.Ramas.Add(new List<Paso>
        {
            P("pillado", "3.3 · Le han visto: «¿Nos venías siguiendo?»", Dialogo(dlgPillado, Contra1)),
            P("pillado-aviso", "3.3 · Aviso: varios enemigos y cómo cambiar de objetivo", Tutorial("CAP3_TUTO_DOS",
                "Son dos. Cambia de objetivo con {BOTON}.",
                "There are two of them. Switch targets with {BOTON}.",
                Core.InputGlyphs.InputGlyphNames.SwitchTarget)),
            P("pillado-combate", "3.3 · Combate contra los dos contrabandistas", GameStepNode.For(new PedirCombateContraNpc
            {
                npcId = Contra1, otrosNpcs = new List<string> { Contra2 },
                combatConfig = Cargar<Game.NPC.Modules.NPCCombatConfig>(ConfigDeLosContrabandistas), defeatEventKey = S3.CarreteroVencido,
                waitForDefeat = true, restorePeacefulOnDefeat = true, radioDeArena = 25f,
            }), "pillado-gana", "seguir"),
            P("pillado-gana", "3.3 · Les gana: huyen al campamento", Dialogo(dlgVencido, Contra1)),
            P("pillado-huyen", "3.3 · Vuelven a su campamento del claro", Emitir(S3.Huyen)),
            P("pillado-claro", "3.3 · Will sigue solo hasta el claro", Esperar(S3.LlegaAlClaro), "paso-carro"),
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
        // Se desempaqueta para poder separar la pluma en su tramo. La zona del claro (entrar a pelear)
        // no se usa en el capítulo 3: se quita.
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(claro.position, claro.rotation);
        EnTramo(padre, Tramo(S3.Noche, S3.ContrabandistasFin), go);
        var zona = go.transform.Find("Claro");
        if (zona != null) Object.DestroyImmediate(zona.gameObject);
        // La pluma solo cuenta cuando ya se ha visto el campamento.
        var pluma = go.transform.Find("PlumaBrillante");
        if (pluma != null)
        {
            var tramo = Hijo(padre, pluma.name + " (tramo de la pluma)");
            tramo.position = pluma.position;
            pluma.SetParent(tramo, true);
            EnTramo(tramo, Tramo(S3.Decide, S3.ContrabandistasFin), pluma.gameObject);
        }
        informe.AppendLine("Cap. 3: campamento de los contrabandistas en el claro.");

        PrepararCarroDeLasJaulas(raiz, informe);
        // La hoguera de la acampada, el escondite de Will tras el matorral, el sitio de Will y el claro,
        // sin flores altas: si no, tapan el fuego, la cara de Will y las jaulas (INC-1035, INC-1038).
        int despejadas = DespejarHierba(new Vector3(-99.5f, 0f, -207.5f), 5.5f)
            + DespejarHierba(new Vector3(-104f, 0f, -212f), 9f)
            + DespejarHierba(new Vector3(-215f, 0f, -125f), 12f)
            + DespejarHierba(new Vector3(-208f, 0f, -134f), 3f);
        informe.AppendLine($"Cap. 3: acampada y claro despejados de flores ({despejadas} celdas de hierba).");
        ColocarLoDelAgua(raiz, marcas, informe);
    }

    /// Coloca el farol, las boyas y el escondite bajo el puente sobre la geometría de la escena.
    private static void ColocarLoDelAgua(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        foreach (var objeto in raiz.gameObject.scene.GetRootGameObjects())
        {
            var recadoRetirado = objeto.transform.Find(NombreRescatado + "/Recado · Doña Lubina");
            if (objeto.name == NombreRescatado) recadoRetirado = objeto.transform.Find("Recado · Doña Lubina");
            if (recadoRetirado != null) recadoRetirado.gameObject.SetActive(false);
        }

        Physics.SyncTransforms();
        var punta = new Vector3(PuntaDelMuelle.x, 0f, PuntaDelMuelle.y);
        // La casa del farol y el borde de su porche que mira al muelle.
        Transform casa = null;
        foreach (var r in raiz.gameObject.scene.GetRootGameObjects())
        {
            casa = r.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == CasaDelFarol);
            if (casa != null) break;
        }
        if (casa == null) { informe.AppendLine($"AVISO: no está «{CasaDelFarol}» en la escena: Candela, su farol y las boyas se quedan en su sitio de tabla."); }
        else
        {
            var b = Limites(casa.gameObject);
            var centro = new Vector3(b.center.x, 0f, b.center.z);
            var hacia = punta - centro; hacia.y = 0f; hacia.Normalize();
            Vector3 borde = centro; bool hayPorche = false;
            for (float d = 0f; d < b.extents.magnitude + 4f; d += 0.25f)
            {
                var q = centro + hacia * d;
                if (Tablero(q, 3f, out float y, casa) && y > 0.6f) { borde = new Vector3(q.x, y, q.z); hayPorche = true; }
                else if (hayPorche) break;
            }
            if (!hayPorche) borde = new Vector3(b.center.x, 1.6f, b.center.z) + hacia * Mathf.Max(b.extents.x, b.extents.z);
            var candela = raiz.Find(Aldea + " · roster/SP_SPAWN_Candela");
            if (candela != null) candela.SetPositionAndRotation(borde - hacia * 0.9f, Quaternion.LookRotation(hacia));

            // El farol queda junto al marco de la puerta, visible desde el agua.
            var padre = Hijo(raiz, Cap3 + " · el agua");
            PonerRampaDelFarol(casa, padre, borde.y, informe);
            var farolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarolDeCandela);
            if (farolPrefab != null)
            {
                var farol = (GameObject)PrefabUtility.InstantiatePrefab(farolPrefab, raiz.gameObject.scene);
                farol.name = "Farol de Candela";
                farol.transform.SetParent(padre, true);
                ColocarFarolJuntoALaPuerta(casa, farol.transform);
                var lf = Limites(farol);
                var luzGo = new GameObject("Luz del farol");
                luzGo.transform.SetParent(farol.transform, true);
                luzGo.transform.position = new Vector3(lf.center.x, lf.max.y - lf.size.y * 0.3f, lf.center.z);
                var luz = luzGo.AddComponent<Light>();
                luz.type = LightType.Point; luz.color = new Color(1f, 0.78f, 0.42f); luz.range = 9f; luz.shadows = LightShadows.None; luz.enabled = false;
                var nocturna = luzGo.AddComponent<LuzNocturna>();
                nocturna.intensidad = 3f; nocturna.umbral = 0.02f; nocturna.parpadeo = 0.15f;
            }
            else informe.AppendLine($"AVISO: falta el farol ({FarolDeCandela}).");

            // Dos boyas (barriles a flote) entre la punta del muelle y el porche; con ellas, sus llegadas.
            var barril = AssetDatabase.LoadAssetAtPath<GameObject>(BarrilDeBoya);
            var desde = punta; var hasta = new Vector3(borde.x, 0f, borde.z);
            for (int i = 1; i <= 2; i++)
            {
                var sitio = Vector3.Lerp(desde, hasta, i / 3f);
                var llegada = raiz.Find($"{Cap3} · llegadas/Boya {i} (Pez)/Disparador");
                if (llegada != null) llegada.position = sitio;
                if (barril == null) continue;
                var tramo = Hijo(padre, $"Boya {i} (tramo)");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(barril, raiz.gameObject.scene);
                go.name = $"Boya {i}";
                go.transform.SetParent(tramo, true);
                go.transform.SetPositionAndRotation(sitio, Quaternion.Euler(8f, i * 70f, 0f));
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var lb = Limites(go);
                go.transform.position += Vector3.up * (-0.25f - lb.min.y);
                EnTramo(tramo, Tramo(S3.Nada, S3.Noche), go);
            }
            informe.AppendLine($"Cap. 3: la casa del farol en {casa.position:F1}; porche {(hayPorche ? "medido" : "supuesto")} en {borde:F1}; Candela, su farol y dos boyas.");
        }

        // El hueco bajo el puente: quien pasa nadando por debajo no se ve. Se mide en el puente de
        // verdad (el tramo del tablero que va sobre el cauce); si no está, se usa la marca.
        Vector3 centroHueco, tamanoHueco; Quaternion giroHueco;
        bool medido = MedirHuecoBajoElPuente(raiz.gameObject.scene, out centroHueco, out giroHueco, out tamanoHueco);
        if (!medido && marcas.TryGetValue("Cap3_BajoElPuente", out var bajo))
        {
            centroHueco = bajo.position - Vector3.up * (HuecoBajoElPuente.y * 0.5f + 0.3f);
            giroHueco = bajo.rotation; tamanoHueco = HuecoBajoElPuente;
            medido = true;
            informe.AppendLine($"AVISO: no encuentro «{PuenteDelSeguimiento}»: el hueco bajo el puente va en la marca Cap3_BajoElPuente.");
        }
        if (medido)
        {
            var tramo = Hijo(Hijo(raiz, Cap3 + " · el agua"), "Escondite bajo el puente (tramo)");
            var go = new GameObject("Escondite bajo el puente");
            go.transform.SetParent(tramo, false);
            go.transform.SetPositionAndRotation(centroHueco, giroHueco);
            var caja = go.AddComponent<BoxCollider>();
            caja.isTrigger = true; caja.size = tamanoHueco;
            go.AddComponent<Escondite>();
            EnTramo(tramo, Tramo(S3.CaeLaNoche, S3.EnElClaro), go);
            informe.AppendLine($"Cap. 3: hueco bajo el puente en {centroHueco:F1}, {tamanoHueco.x:F1} × {tamanoHueco.y:F1} × {tamanoHueco.z:F1} m (ancho × alto × largo sobre el cauce).");
        }
    }

    private static void PonerRampaDelFarol(Transform casa, Transform padre, float alturaPorche, StringBuilder informe)
    {
        // La fachada es perpendicular al eje del modelo. Se busca el primer peldaño a ambos lados
        // del centro para que la colisión quede alineada con el hueco real de la barandilla.
        var frente = -casa.forward;
        var transversal = Vector3.Cross(Vector3.up, frente);
        const float bordeDeTablas = 3.3f;
        const float centroDeEscalera = 0.375f;
        var prueba = casa.position + frente * (bordeDeTablas + 0.17f);
        var lado = transversal * centroDeEscalera;
        if (!Tablero(prueba + lado, alturaPorche + 0.12f, out _, casa))
        {
            lado = -lado;
            if (!Tablero(prueba + lado, alturaPorche + 0.12f, out _, casa))
            {
                informe.AppendLine("AVISO: no se encuentra el peldaño de la casa del farol.");
                return;
            }
        }

        var arriba = casa.position + lado + frente * (bordeDeTablas - 0.12f);
        arriba.y = alturaPorche + 0.025f;
        var abajo = arriba + frente * 3.05f;
        abajo.y = casa.position.y - 0.58f;
        var eje = abajo - arriba;
        var rampa = Hijo(padre, "Colisión continua de la escalera del farol");
        var suelo = casa.GetComponentInChildren<MeshCollider>();
        rampa.gameObject.layer = suelo != null ? suelo.gameObject.layer : casa.gameObject.layer;
        rampa.rotation = Quaternion.LookRotation(eje, Vector3.up);
        rampa.position = (arriba + abajo) * 0.5f - rampa.up * 0.06f;
        rampa.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.02f, 0.12f, eje.magnitude);
        informe.AppendLine($"Cap. 3: rampa del farol de {abajo:F2} a {arriba:F2}.");
    }

    /// Recorre el tablero del puente del seguimiento y busca el tramo donde queda hueco debajo (el
    /// cauce): ahí va el escondite, de lo que hay debajo hasta el tablero, a lo ancho del puente.
    private static bool MedirHuecoBajoElPuente(UnityEngine.SceneManagement.Scene escena, out Vector3 centro, out Quaternion giro, out Vector3 tamano)
    {
        centro = Vector3.zero; giro = Quaternion.identity; tamano = Vector3.zero;
        Transform puente = null;
        foreach (var r in escena.GetRootGameObjects())
        {
            puente = r.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(PuenteDelSeguimiento));
            if (puente != null) break;
        }
        if (puente == null) return false;
        var tablas = puente.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Tabl")).Select(r => r.bounds.center).ToList();
        if (tablas.Count < 2) return false;
        Vector3 a = tablas[0], b = tablas[1];
        foreach (var p in tablas) foreach (var q in tablas)
            if ((new Vector2(p.x - q.x, p.z - q.z)).sqrMagnitude > (new Vector2(a.x - b.x, a.z - b.z)).sqrMagnitude) { a = p; b = q; }
        var eje = b - a; eje.y = 0f;
        float largo = eje.magnitude;
        if (largo < 1f) return false;
        eje /= largo;
        const float paso = 0.5f, huecoMinimo = 1.5f;
        float desde = -1f, hasta = -1f, sueloMasBajo = float.PositiveInfinity, tableroMasBajo = float.PositiveInfinity;
        for (float t = 0f; t <= largo; t += paso)
        {
            var p = a + eje * t;
            if (!Tablero(p, p.y + 5f, out float tablero, puente)) continue;
            float suelo = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, tablero - 0.3f, p.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(puente) && hit.point.y > suelo) suelo = hit.point.y;
            if (float.IsNegativeInfinity(suelo) || tablero - suelo < huecoMinimo) continue;
            if (desde < 0f) desde = t;
            hasta = t;
            sueloMasBajo = Mathf.Min(sueloMasBajo, suelo);
            tableroMasBajo = Mathf.Min(tableroMasBajo, tablero);
        }
        if (desde < 0f) return false;
        var medio = a + eje * ((desde + hasta) * 0.5f);
        float abajo = sueloMasBajo - 0.5f, arriba = tableroMasBajo - 0.25f;
        centro = new Vector3(medio.x, (abajo + arriba) * 0.5f, medio.z);
        giro = Quaternion.LookRotation(eje, Vector3.up);
        tamano = new Vector3(AnchoDelHueco, arriba - abajo, hasta - desde + 1f);
        return true;
    }

    /// La superficie más alta bajo un punto (de «por encima» metros hacia abajo), sin triggers. Con
    /// «solo», solo cuenta lo que cuelga de ese objeto.
    private static bool Tablero(Vector3 punto, float porEncima, out float y, Transform solo = null)
    {
        y = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(new Vector3(punto.x, porEncima, punto.z), Vector3.down, porEncima + 1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (solo != null && !hit.collider.transform.IsChildOf(solo)) continue;
            if (hit.point.y > y) y = hit.point.y;
        }
        return !float.IsNegativeInfinity(y);
    }

    /// Como <see cref="Tablero"/>, pero si el punto cae en la rendija entre dos tablones busca el
    /// tablón más cercano en un radio. Solo vale una superficie por encima del agua (y > 1).
    private static bool TableroCerca(Vector3 punto, float porEncima, float radio, out Vector3 sobre)
    {
        sobre = punto;
        float mejor = float.PositiveInfinity;
        for (float dx = -radio; dx <= radio + 0.001f; dx += 0.25f)
            for (float dz = -radio; dz <= radio + 0.001f; dz += 0.25f)
            {
                float d = dx * dx + dz * dz;
                if (d > radio * radio || d >= mejor) continue;
                var q = new Vector3(punto.x + dx, 0f, punto.z + dz);
                if (!Tablero(q, porEncima, out float y) || y <= 1f) continue;
                mejor = d; sobre = new Vector3(q.x, y, q.z);
            }
        return !float.IsPositiveInfinity(mejor);
    }

    /// La punta del embarcadero de la aldea (x, z): ahí está Pez y de ahí salen las boyas hacia la casa del farol.
    private static readonly Vector2 PuntaDelMuelle = new Vector2(270f, -491.5f);
    private const string CasaDelFarol = "Casa apartada (se llega nadando)";
    private const string BarrilDeBoya = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Barrel01_a01.prefab";
    private const string FarolDeCandela = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Lighting/Light01_a01.prefab";

    private static void ColocarFarolJuntoALaPuerta(Transform casa, Transform farol)
    {
        // La fachada de CasaDeLaAldea_Amarilla está en z = −1,8 y el porche a 1,6 m.
        var pie = casa.TransformPoint(new Vector3(0f, 1.6f, -2.15f));
        farol.SetPositionAndRotation(pie, Quaternion.LookRotation(-casa.forward, casa.up));
        farol.position += Vector3.up * (pie.y - Limites(farol.gameObject).min.y);
    }
    /// Hueco bajo el tablero del puente donde no se ve a quien nada (ancho, alto, largo) si no se puede medir el puente.
    private static readonly Vector3 HuecoBajoElPuente = new Vector3(5f, 2.4f, 9f);
    /// El puente del camino que cruzan los contrabandistas en el seguimiento, y el ancho del escondite
    /// bajo él (el tablero mide 4 m; un poco más para quien nada pegado al borde).
    private const string PuenteDelSeguimiento = "Puente_camino_5";
    private const float AnchoDelHueco = 6f;

    private static NarrativeNode DesbloquearHabilidad(AbilityKey habilidad, string marca) => GameStepNode.For(new PedirDesbloqueo
    {
        abilityKeysToUnlock = new List<AbilityKey> { habilidad }, applyPresetAfterUnlock = true, oneShotFlag = marca,
    });

    private const string ConfigDeLosContrabandistas = "Assets/_NPCs/Combat/NPC_Combat_Config_Contrabandistas.asset";
    private const string VigiaContrabandista = "Assets/_NPCs/_GrafoNarrativo/Contrabandista_Vigia.prefab";
    /// El del carro: más corpulento que el vigía. Los NPC del pack comparten el mismo cuerpo de chibi
    /// (ninguno es más grueso), así que es una variante de un vecino con la planta ensanchada.
    private const string GrandoteContrabandista = "Assets/_NPCs/_GrafoNarrativo/Contrabandista_Grandote.prefab";
    private const string BaseDelGrandote = "Assets/_NPCs/Pueblo/Jorge.prefab";
    private static readonly Vector3 PlantaDelGrandote = new Vector3(1.22f, 1.1f, 1.22f);

    private static readonly string[] PiezasDelGrandote = { "Head01_Male", "Hair12", "Body15", "AC11_Mustache02", "Eye01", "Mouth02" };
    private static readonly string[] PiezasDelVigia = { "Head01_Male", "Hair05", "Body16", "AC10_Mustache02", "Eye01", "Mouth02" };

    /// Enciende las piezas del pack modular de un contrabandista (cara de hombre, pelo corto, bigote).
    private static void VestirContrabandista(string ruta, string[] piezas, StringBuilder informe)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) return;
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            GeneradorDeNpcs.SeleccionarPiezas(raiz, piezas);
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            informe.AppendLine($"Cap. 3: «{ruta}» con {string.Join(", ", piezas)} (pasar después «Caras de los NPCs»).");
        }
        catch (System.InvalidOperationException e)
        {
            informe.AppendLine($"AVISO: «{ruta}»: {e.Message}");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    /// Los contrabandistas no son enemigos de paso (INC-1287): el vigía usaba el prefab del pirata, que
    /// ataca al ver al jugador y suelta su «¿Ves este ojo?» en cuanto Will se acerca al claro. Tienen
    /// su propia config, copia de la del pirata pero sin atacar por su cuenta ni charla de alerta y con
    /// el cerebro por puntuación (pelean en grupo, INC-1283); la pelea la pide la historia
    /// (PedirCombateContraNpc). El vigía es una variante del pirata con esa config.
    private static void AsegurarContrabandistas(StringBuilder informe)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<Game.NPC.Modules.NPCCombatConfig>(ConfigDeLosContrabandistas);
        if (cfg == null)
        {
            AssetDatabase.CopyAsset(CombateDeLosContrabandistas, ConfigDeLosContrabandistas);
            cfg = AssetDatabase.LoadAssetAtPath<Game.NPC.Modules.NPCCombatConfig>(ConfigDeLosContrabandistas);
        }
        if (cfg == null) { informe.AppendLine("AVISO: no se pudo crear la config de los contrabandistas."); return; }
        cfg.isAggressive = false;
        cfg.dialogueOnAlert = null;
        cfg.dialogueOnAlertRepeat = null;
        cfg.waitForAlertDialogue = false;
        cfg.freezePlayerOnAlert = false;
        cfg.cerebro = Game.NPC.TipoDeCerebroDeCombate.PorPuntuacion;
        EditorUtility.SetDirty(cfg);

        var vigia = AssetDatabase.LoadAssetAtPath<GameObject>(VigiaContrabandista);
        if (vigia == null)
        {
            var origen = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/Combat/Boy_Pirate.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(origen);
            vigia = PrefabUtility.SaveAsPrefabAsset(inst, VigiaContrabandista);
            Object.DestroyImmediate(inst);
        }
        int puestas = 0;
        foreach (var mb in vigia.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            bool cambio = false;
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.name == "combatConfig" && it.objectReferenceValue != cfg)
                { it.objectReferenceValue = cfg; cambio = true; puestas++; }
            if (cambio) so.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SavePrefabAsset(vigia);

        var grandote = AssetDatabase.LoadAssetAtPath<GameObject>(GrandoteContrabandista);
        if (grandote == null)
        {
            var origen = AssetDatabase.LoadAssetAtPath<GameObject>(BaseDelGrandote);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(origen);
            grandote = PrefabUtility.SaveAsPrefabAsset(inst, GrandoteContrabandista);
            Object.DestroyImmediate(inst);
        }
        if (grandote.transform.localScale != PlantaDelGrandote)
        {
            grandote.transform.localScale = PlantaDelGrandote;
            PrefabUtility.SavePrefabAsset(grandote);
        }
        // Cara y pelo de hombre, con bigote: con el pelo largo del pack parecían mujeres (INC-1406).
        VestirContrabandista(GrandoteContrabandista, PiezasDelGrandote, informe);
        VestirContrabandista(VigiaContrabandista, PiezasDelVigia, informe);
        informe.AppendLine($"Cap. 3: el del carro, «{GrandoteContrabandista}» (variante de {BaseDelGrandote}, planta {PlantaDelGrandote}).");
        informe.AppendLine($"Cap. 3: contrabandistas con config propia (no atacan por su cuenta, cerebro por puntuación); vigía «{VigiaContrabandista}» ({puestas} referencia(s) a la config).");
    }

    /// La copia del carro del pack con las ruedas sueltas y WheelSpin; solo se hace si falta.
    private static void AsegurarCarroConRuedas(StringBuilder informe)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CarroDeLasJaulas) != null) return;
        var origen = AssetDatabase.LoadAssetAtPath<GameObject>(CarroDelPack);
        Kits.EditorTools.WheelSeparator.Separate(origen, CarroDeLasJaulas, out string parte);
        informe.AppendLine(parte);
    }

    /// El carro que pasa junto al fuego: va tras el contrabandista del seguimiento, sin colisiones (se
    /// sigue a escondidas a su lado), con una luz cálida bajo la carga tapada que sube y baja despacio.
    private static void PrepararCarroDeLasJaulas(Transform raiz, StringBuilder informe)
    {
        var carro = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PROP_Carro Cap3");
        if (carro == null) { informe.AppendLine("AVISO: falta el carro de las jaulas (PROP_Carro Cap3)."); return; }
        var arrastre = carro.GetComponent<CargaArrastrada>() ?? carro.gameObject.AddComponent<CargaArrastrada>();
        // Va sin colliders: que las cámaras de charla lo vean como algo que tapa (INC-1331).
        if (carro.GetComponent<Kits.Core.CameraOccluder>() == null) carro.gameObject.AddComponent<Kits.Core.CameraOccluder>();
        // Las varas del modelo van hacia su +Z (giro 0: con 90° o 270° el carro iba de lado). A 1,47 m
        // del centro del carro, quien tira queda metido entre las varas, entre la caja y el travesaño
        // de las puntas, agarrándolas (pose «TirarDeLasVaras»). Ver INC-1322.
        arrastre.Configurar(Contra1, 1.47f, 0f, "TirarDeLasVaras");
        // Las manos en las varas (INC-1345): un agarre en cada vara, cerca de las puntas y por encima
        // (espacio del modelo Cart07, en metros: varas a ±0,57 m del centro, arriba a 0,62 m, puntas a
        // 1,47–1,66 m). Al engancharse, el carro se escala para que queden a la altura de su cadera.
        arrastre.ConfigurarAgarres(new Vector3(-0.565f, 0.62f, 1.38f), new Vector3(0.565f, 0.62f, 1.38f));
        // Sólido: la caja (ruedas incluidas, sin las varas) choca y la rodean los demás agentes.
        arrastre.HacerSolida(new Bounds(new Vector3(0f, 1.175f, -0.49f), new Vector3(2.3f, 2.35f, 2.18f)));
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
