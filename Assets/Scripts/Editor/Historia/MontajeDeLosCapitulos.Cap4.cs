using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Capítulo 4, «El Bosque Prohibido y el Gólem» (GDD § 5.8, novelas VII–VIII). En la build de la demo, aquí se corta.
/// 4.1 Estela en su claro lee el aro y entra en el grupo · 4.2 la casa entre raíces y la primera
/// receta del combo (Cúpula Estelar) · 4.3 la prueba de la campana (llegar sin chamuscar las
/// figuras de barro; Levitación por instinto) · 4.4 el Gólem (Encuentro_Golem1, con el barro del
/// arroyo: <see cref="ZonaDeBarro"/>) · 4.5 la manzana harinosa · 4.6 la bifurcación, Liam entra en
/// el grupo y la bota del vado · fin de la demo (<see cref="FinDeLaDemoNode"/>).
/// Escenario: el claro de Estela (−330, 65) reúne la entrada de Estela, su casa (una tienda entre
/// las raíces) y la campana; la arena del Gólem es el descanso del camino (−95, 65); la bifurcación,
/// el puente del camino 8 sobre el río del bosque.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    private const string Cap4 = "Cap4";
    public const string MisionEstela = "CAP4_ENCUENTRA_A_ESTELA";
    public const string MisionReino = "CAP4_RUMBO_AL_REINO";
    public const string FlagEstelaAparece = "ESTELA_APARECE";
    public const string FlagLiamAparece = "LIAM_APARECE";
    public const string FlagFigurasChamuscadas = "CAP4_FIGURAS_CHAMUSCADAS";
    private const string RutaEsquirla = CarpetaItems + "/IT_EsquirlaDeCristal.asset";

    private static class S4
    {
        public const string Bosque = "CAP4_BOSQUE_EMPIEZA";
        public const string LlegadaClaro = "CAP4_LLEGADA_CLARO";
        public const string Casa = "CAP4_CASA_EMPIEZA";
        public const string Dormir = "CAP4_DORMIR";
        public const string Campana = "CAP4_CAMPANA_EMPIEZA";
        public const string Figura = "CAP4_FIGURA_CHAMUSCADA";
        public const string Tocada = "CAP4_CAMPANA_TOCADA";
        public const string CampanaFin = "CAP4_CAMPANA_FIN";
        public const string Golem = "CAP4_GOLEM_EMPIEZA";
        public const string LlegadaArena = "CAP4_LLEGADA_ARENA";
        public const string Bifurcacion = "CAP4_BIFURCACION_EMPIEZA";
        public const string LlegadaBifurcacion = "CAP4_LLEGADA_BIFURCACION";
        public const string LiamEnGrupo = "CAP4_LIAM_EN_EL_GRUPO";
        public const string Bota = "CAP4_BOTA_RECOGIDA";
    }

    private const string Estela = "_ESTELA", Liam = "_LIAM";

    private static Capitulo MontarAssetsCap4(StringBuilder informe)
    {
        Objeto("IT_EsquirlaDeCristal", "esquirla_cristal", "Esquirla de cristal", "Crystal shard",
            "Estaba entre las rocas del Gólem. Por un instante mostró un rostro.",
            "It was among the Golem's rocks. For a moment it showed a face.");

        Mision("Q_CAP4_ENCUENTRA_A_ESTELA", MisionEstela, "La hechicera del bosque", "The forest sorceress",
            "En el Bosque Prohibido vive una hechicera que quizá sepa leer el aro.",
            "A sorceress who might be able to read the collar lives in the Forbidden Forest.",
            ("CAP4_ESTELA", "Encuentra a Estela en el Bosque Prohibido", "Find Estela in the Forbidden Forest"));
        Mision("Q_CAP4_RUMBO_AL_REINO", MisionReino, "Rumbo al Reino", "Bound for the Kingdom",
            "Estela ayuda a Will. Primero, su entrenamiento; después, el camino del Reino.",
            "Estela is helping Will. First, his training; then, the road to the Kingdom.",
            ("CAP4_CAMPANA", "Supera la prueba de la campana", "Pass the bell trial"),
            ("CAP4_GOLEM", "Cruza el bosque hacia el Reino", "Cross the forest toward the Kingdom"),
            ("CAP4_VADO", "Sigue el camino del río", "Follow the river road"));

        // ── Diálogos ────────────────────────────────────────────────────────────────────────
        string dCarta = DialogoDeCaja("DLG_CAP4_CARTA", "DLG_CAP4_CARTA",
            W("Una carta sin abrir para «Estela Marael». Remite: «Mamá».", "An unopened letter for \"Estela Marael\". Sender: \"Mum\".", NPCEmotion.Thinking),
            W("Mejor no preguntar.", "Better not to ask."));
        string dApodos = DialogoDeCaja("DLG_CAP4_APODOS", "DLG_CAP4_APODOS",
            W("Una tabla junto a la puerta con tres apodos tachados. El último dice «la Bruja del Postre».", "A board by the door with three crossed-out nicknames. The last one reads \"the Dessert Witch\"."));
        string dCena = DialogoDeCaja("DLG_CAP4_CENA", "DLG_CAP4_CENA",
            W("La cena. Negra por fuera y… negra por dentro.", "Dinner. Black on the outside and… black on the inside."),
            L("CHAR_ESTELA", "¡Está quemada a propósito! Así sabe a hoguera.", "It's burnt on purpose! It tastes like a campfire.", NPCEmotion.Annoyed));
        string dCama = DialogoDeCaja("DLG_CAP4_CAMA", "DLG_CAP4_CAMA",
            W("Un jergón entre las raíces. Huele a humo y a manzana.", "A straw mattress among the roots. It smells of smoke and apples."),
            L("CHAR_ESTELA", "Duerme. Mañana empieza tu entrenamiento.", "Sleep. Your training starts tomorrow."));
        string dBota = DialogoDeCaja("DLG_CAP4_BOTA", "DLG_CAP4_BOTA",
            W("¡La tengo! Enganchada entre dos ramas.", "Got it! Caught between two branches.", NPCEmotion.Happy));

        // ── Cinemáticas ─────────────────────────────────────────────────────────────────────
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_ESTELA", Guion = "Cap4_01_Estela",
            Nombre = "Cap. 4: Estela", Resumen = "La explosión, «¿otra princesita perdida?», el aro leído y «ya me lo cobraré»." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_MANANA", Guion = "Cap4_02_LaManana", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 4: la mañana en la casa", Resumen = "El cartel del FUTURO GRAN HECHICERO y la primera receta: protegerte y proteger a otro." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_CAMPANA", Guion = "Cap4_03_LaCampana", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 4: las reglas de la campana", Resumen = "Llega a la campana sin chamuscar a nadie. Estela, mientras, molesta." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_FIGURA", Guion = "Cap4_04_LaFigura",
            Nombre = "Cap. 4: la figura que resbala", Resumen = "Will sostiene una figura en el aire sin saber cómo; rehacen las figuras; alguien les observa." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_GOLEM", Guion = "Cap4_05_ElGolem",
            Nombre = "Cap. 4: el Gólem", Resumen = "Liam, desde el saliente, traza el pacto y el Gólem sale de la tierra." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_TRAS_GOLEM", Guion = "Cap4_06_TrasElGolem",
            Nombre = "Cap. 4: después del Gólem", Resumen = "Agotados; la esquirla con un rostro; Liam escribe «No volver a utilizar seres vivos»." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_MANZANA", Guion = "Cap4_07_LaManzana", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 4: la manzana harinosa", Resumen = "Estela no quiere ayuda; comparten la última manzana. «Gracias, idiota»." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_BIFURCACION", Guion = "Cap4_08_LaBifurcacion",
            Nombre = "Cap. 4: la bifurcación", Resumen = "Liam aparece, lee el trozo de lejos y se une hasta el Reino. En el vado, la corriente se lleva su bota." });
        s_cines.Add(new Cine { Cap = Cap4, Clave = "CAP4_BOTA", Guion = "Cap4_09_LaBota",
            Nombre = "Cap. 4: la bota", Resumen = "«Tampoco salía en el mapa que alguien iría a por mi bota». Fin de la Primera Parte." });

        foreach (var (k, es, en) in TextosDeCineCap4()) TextoDeCine(k, es, en);
        // La frase de villano de Liam al invocar al Gólem se cambia por el tono de la novela (propuesta del 6 oct, § 3).
        TextoDeCine("EVT_GOLEM_01", "Lo siento. Necesito que pelees.", "I'm sorry. I need you to fight.");

        // ── Marcas, roster y escena ─────────────────────────────────────────────────────────
        M(Cap4, "Cap4_Bosque_Entrada", -140f, -110f, 300f);
        M(Cap4, "Cap4_Claro_Will", -313f, 62f, 270f);
        M(Cap4, "Cap4_Claro_Estela", -322f, 66f, 90f);
        M(Cap4, "Cap4_Casa", -346f, 62f, 60f);
        M(Cap4, "Cap4_Campana", -330f, 86f, 180f);
        M(Cap4, "Cap4_Campana_Salida", -330f, 60f, 0f);
        M(Cap4, "Cap4_LiamLoma", -300f, 98f, 240f);
        M(Cap4, "Cap4_Golem_Entrada", -125f, 60f, 90f);
        M(Cap4, "Cap4_LiamSaliente", -110f, 86f, 170f);
        M(Cap4, "Cap4_Manzana", -104f, 52f, 270f);
        M(Cap4, "Cap4_Bifurcacion", -180f, 40f, 250f);
        M(Cap4, "Cap4_LiamBifurcacion", -176.5f, 49.5f, 200f);
        M(Cap4, "Cap4_Bota", -184f, -24f, 0f);

        s_puntos.Add(new PuntoDeRoster { Cap = Cap4, SpawnId = "SPAWN__ESTELA", Pos = new Vector3(-322f, float.NaN, 66f), Rumbo = 90f });
        s_puntos.Add(new PuntoDeRoster { Cap = Cap4, SpawnId = "SPAWN__LIAM", Pos = new Vector3(-300f, float.NaN, 98f), Rumbo = 240f });
        s_papeles.Add(new Papel { SpawnId = "SPAWN__ESTELA", Prefab = "Assets/Prefabs/_ESTELA.prefab", Nombre = "_ESTELA", Id = Estela, Flag = FlagEstelaAparece, NavMesh = false });
        s_papeles.Add(new Papel { SpawnId = "SPAWN__LIAM", Prefab = "Assets/Prefabs/_LIAM.prefab", Nombre = "_LIAM", Id = Liam, Flag = FlagLiamAparece, NavMesh = false });

        // Liam les sigue de lejos antes de unirse: la loma de la campana, el saliente del Gólem y la bifurcación.
        s_personajes.Add(new SituacionesDe
        {
            Actor = Liam,
            Entradas =
            {
                ("Cap. 4 · en la loma de la campana", Tramo(S4.CampanaFin, S4.Golem), ActorPresence.AtMark, "Cap4_LiamLoma", null),
                ("Cap. 4 · en el saliente del Gólem", Tramo(S4.Golem, S4.Bifurcacion), ActorPresence.AtMark, "Cap4_LiamSaliente", null),
                ("Cap. 4 · en la bifurcación", Tramo(S4.Bifurcacion, S4.LiamEnGrupo), ActorPresence.AtMark, "Cap4_LiamBifurcacion", null),
            },
        });

        s_llegadas.Add(new Llegada { Cap = Cap4, Nombre = "Llegada al claro de Estela", Senal = S4.LlegadaClaro, Mision = MisionEstela,
            Pos = new Vector3(-312f, float.NaN, 62f), Radio = 12f, Tramo = Tramo(S4.Bosque, S4.Casa) });
        s_llegadas.Add(new Llegada { Cap = Cap4, Nombre = "Llegada a la arena del Gólem", Senal = S4.LlegadaArena,
            Pos = new Vector3(-105f, float.NaN, 63f), Radio = 12f, Tramo = Tramo(S4.Golem, S4.Bifurcacion) });
        s_llegadas.Add(new Llegada { Cap = Cap4, Nombre = "Llegada a la bifurcación", Senal = S4.LlegadaBifurcacion,
            Pos = new Vector3(-180f, float.NaN, 40f), Radio = 10f, Tramo = Tramo(S4.Bifurcacion, S4.LiamEnGrupo) });

        s_inspecciones.Add(new Inspeccion { Cap = Cap4, Nombre = "Casa · la carta", Dialogo = dCarta, Pos = new Vector3(-343f, float.NaN, 66f), Tramo = Tramo(S4.Casa, S4.Campana) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap4, Nombre = "Casa · los apodos", Dialogo = dApodos, Pos = new Vector3(-349f, float.NaN, 64f), Tramo = Tramo(S4.Casa, S4.Campana) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap4, Nombre = "Casa · la cena", Dialogo = dCena, Pos = new Vector3(-340f, float.NaN, 60f), Tramo = Tramo(S4.Casa, S4.Campana) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap4, Nombre = "Casa · la cama", Dialogo = dCama, Senal = S4.Dormir, Pos = new Vector3(-345f, float.NaN, 59f), Tramo = Tramo(S4.Casa, S4.Campana) });
        s_inspecciones.Add(new Inspeccion { Cap = Cap4, Nombre = "Vado · la bota de Liam", Dialogo = dBota, Senal = S4.Bota, Pos = new Vector3(-184f, float.NaN, -24f), Tramo = Tramo(S4.LiamEnGrupo, FinCap4) });

        const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs";
        s_atrezo.Add(new Atrezo { Cap = Cap4, Nombre = "La casa entre raíces", Prefab = Pack + "/Main Structures/Tent/Tent02_a01.prefab",
            Pos = new Vector3(-346f, float.NaN, 62f), Rumbo = 60f, Tamano = 6f, Colision = true });
        s_atrezo.Add(new Atrezo { Cap = Cap4, Nombre = "Hoguera de Estela", Prefab = Pack + "/Props/Goods/Campfire01_a01.prefab",
            Pos = new Vector3(-338f, float.NaN, 64f), Rumbo = 0f, Tamano = 1.2f });
        s_atrezo.Add(new Atrezo { Cap = Cap4, Nombre = "Cartel del entrenamiento", Prefab = Pack + "/Props/Sign/Shop_sign01_a01.prefab",
            Pos = new Vector3(-334f, float.NaN, 64f), Rumbo = 270f, Tamano = 1.6f, Tramo = Tramo(S4.Campana, S4.CampanaFin) });
        s_atrezo.Add(new Atrezo { Cap = Cap4, Nombre = "La bota de Liam", Prefab = Pack + "/Props/Goods/Rope01_a01.prefab",
            Pos = new Vector3(-184f, float.NaN, -24f), Rumbo = 0f, Tamano = 0.5f, Tramo = Tramo(S4.LiamEnGrupo, S4.Bota) });

        var c4 = new Capitulo { Etiqueta = "Cap4", Ruta = "Assets/NarrativeGraph/Cap4.asset", Prefijo = "cap4-", Nombre = "Cap. 4" };
        c4.Recorrido = new List<Paso>
        {
            P("inicio", "Inicio del Cap. 4", new StartNode()),
            P("tras-cap3", "Tras el Cap. 3 (" + FinCap3 + ")", Esperar(FinCap3)),
            P("flag", "Comienza el Cap. 4", Flag("CAP4_INICIADO")),
            P("estela-aparece", "Estela está en su claro (el roster la pone)", Flag(FlagEstelaAparece)),
            // 4.1 · El bosque y Estela
            P("bosque", "4.1 · Hacia el Bosque Prohibido (probar desde aquí)", Emitir(S4.Bosque)),
            P("mision-estela", "Misión: la hechicera del bosque (aviso de misión nueva)", new StartQuestNode { questId = MisionEstela, noGiver = true }),
            P("punto-bosque", "4.1 · Punto de guardado: el cruce", Punto("CAP4_INICIO", "Cap3_Amanecer")),
            P("llegada-claro", "4.1 · Llega al claro de los troncos quemados", Esperar(S4.LlegadaClaro)),
            P("mando-estela", "4.1 · Will al mando (la cinemática cuenta con él)", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
            P("estela", "4.1 · Cinemática: Estela, el aro y «ya me lo cobraré»", Cinematica("CAP4_ESTELA")),
            P("estela-grupo", "4.1 · Estela entra en el grupo", Grupo(GrupoNode.Accion.EntraEnElGrupo, "Estela")),
            P("paso-estela", "Paso: encontrar a Estela (cierra la misión)", Completar(MisionEstela, "CAP4_ESTELA", cerrar: true)),
            // 4.2 · La casa entre raíces
            P("casa", "4.2 · La casa entre raíces (probar desde aquí)", Emitir(S4.Casa)),
            P("casa-noche", "4.2 · Anochece en el claro", new SetTimeOfDayNode { targetTime = DayNightCycle.TimeOfDay.Night }),
            P("casa-boc", "4.2 · «Puedes mirar, pero no toques la cena»", Bocadillo("CAP4_BOC_CASA", "Esta es mi casa. Mira lo que quieras, pero la cena no se toca. Duerme en el jergón.", "This is my home. Look around all you like, but don't touch dinner. Sleep on the straw mattress.", 4f)),
            P("dormir", "4.2 · Will se acuesta en el jergón", Esperar(S4.Dormir)),
            P("rotulo-manana", "4.2 · Rótulo: a la mañana siguiente", Rotulo("CAP4_ROTULO_MANANA", "A la mañana siguiente…", "The next morning…")),
            P("manana-hora", "4.2 · Amanece", new SetTimeOfDayNode { targetTime = DayNightCycle.TimeOfDay.Morning, immediate = true }),
            P("manana-will", "4.2 · Will se despierta junto a la casa", Llevar("Player", "Cap4_Campana_Salida")),
            P("punto-casa", "4.2 · Punto de guardado: la casa de Estela", Punto("CAP4_CASA", "Cap4_Campana_Salida")),
            P("mision-reino", "Misión: rumbo al Reino (aviso de misión nueva)", new StartQuestNode { questId = MisionReino, noGiver = true }),
            P("campana", "4.3 · La prueba de la campana (probar desde aquí)", Emitir(S4.Campana)),
            P("mando-manana", "4.2 · Will al mando", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
            P("manana", "4.2 · Cinemática: el cartel y la primera receta", Cinematica("CAP4_MANANA")),
            P("cupula", "4.2 · Aprende Cúpula Estelar (primera receta del combo)", Desbloquear(SpellId.CupulaEstelar, equipar: false)),
            P("cupula-tuto", "4.2 · Tutorial: el combo mágico", Tutorial("TUTORIAL_CAP4_CUPULA_ESTELAR", "Pulsa {BOTON} y teclea X, B, A: la Cúpula Estelar protege a quien esté debajo.", "Press {BOTON} and type X, B, A: the Star Dome protects whoever is beneath it.", esperar: true)),
            // 4.3 · La campana
            P("reglas", "4.3 · Cinemática: las reglas de la campana", Cinematica("CAP4_CAMPANA")),
            P("campana-fork", "4.3 · La prueba (y en paralelo, las figuras chamuscadas)", new ForkNode(), "figura-0", "tocada"),
            P("tocada", "4.3 · Will toca la campana", Esperar(S4.Tocada)),
            P("liam-aparece", "4.3 · Liam les observa (el roster le pone en la loma)", Flag(FlagLiamAparece)),
            P("campana-fin", "4.3 · Fin de la prueba (Liam, en la loma)", Emitir(S4.CampanaFin)),
            P("figura", "4.3 · Cinemática: la figura que resbala, las figuras rehechas y el que observa", Cinematica("CAP4_FIGURA")),
            P("figura-destapa", "4.3 · Se destapa la pantalla tras la cinemática", Destapar()),
            P("levitacion", "4.3 · Aprende Levitación por instinto", Desbloquear(SpellId.Levitation, equipar: true)),
            P("levitacion-tuto", "4.3 · Tutorial: Levitación", Tutorial("TUTORIAL_CAP4_LEVITACION",
                "Nuevo hechizo: Levitación. Ya está entre tus hechizos: lánzalo para flotar sobre el agua, el barro y los huecos.",
                "New spell: Levitation. It's now among your spells: cast it to float over water, mud and gaps.", esperar: true)),
            P("paso-campana", "Paso: la prueba de la campana", Completar(MisionReino, "CAP4_CAMPANA")),
            // 4.4 · El Gólem
            P("golem", "4.4 · Hacia el Reino: el Gólem (probar desde aquí)", Emitir(S4.Golem)),
            P("punto-golem", "4.4 · Punto de guardado: el camino del Reino", Punto("CAP4_CAMINO", "Cap4_Golem_Entrada")),
            P("llegada-arena", "4.4 · Llegan al descanso del camino", Esperar(S4.LlegadaArena)),
            P("mando-golem", "4.4 · Will al mando", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
            P("golem-cine", "4.4 · Cinemática: Liam traza el pacto y el Gólem sale de la tierra", Cinematica("CAP4_GOLEM")),
            P("golem-batalla", "4.4 · Batalla: el Gólem (fuego de frente lo alimenta, el barro lo hunde, remate en dúo)", new StartBattleNode
            {
                battleId = "Golem_1", useBattleById = true, encounter = Cargar<BattleEncounterSO>("Assets/BOSSBATTLES/Encuentro_Golem1.asset"),
            }),
            P("tras-golem", "4.4 · Cinemática: agotados, la esquirla y el cuaderno de Liam", Cinematica("CAP4_TRAS_GOLEM")),
            P("esquirla", "4.4 · La esquirla de cristal", Dar(RutaEsquirla)),
            P("paso-golem", "Paso: cruzar el bosque", Completar(MisionReino, "CAP4_GOLEM")),
            // 4.5 · La manzana
            P("manzana", "4.5 · Cinemática: la manzana harinosa", Cinematica("CAP4_MANZANA")),
            P("punto-manzana", "4.5 · Punto de guardado: el campamento", Punto("CAP4_MANZANA", "Cap4_Manzana")),
            // 4.6 · La bifurcación y la bota
            P("bifurcacion", "4.6 · Hacia la bifurcación del río (probar desde aquí)", Emitir(S4.Bifurcacion)),
            P("llegada-bifurcacion", "4.6 · Llegan a la bifurcación", Esperar(S4.LlegadaBifurcacion)),
            P("mando-bifurcacion", "4.6 · Will al mando", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
            P("bifurcacion-cine", "4.6 · Cinemática: Liam aparece y se une; la corriente se lleva su bota", Cinematica("CAP4_BIFURCACION")),
            P("liam-grupo", "4.6 · Liam entra en el grupo", Grupo(GrupoNode.Accion.EntraEnElGrupo, "Liam")),
            P("liam-grupo-senal", "4.6 · Liam ya va en el grupo", Emitir(S4.LiamEnGrupo)),
            P("bota-boc", "4.6 · «¡Mi bota!»", Bocadillo("CAP4_BOC_BOTA", "¡Se la lleva el río! Voy a por ella, río abajo.", "The river's taking it! I'll go after it, downstream.", 3.5f)),
            P("bota", "4.6 · Will recoge la bota río abajo", Esperar(S4.Bota)),
            P("mando-bota", "4.6 · Will al mando", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
            P("bota-cine", "4.6 · Cinemática: «Tampoco salía en el mapa que alguien iría a por mi bota»", Cinematica("CAP4_BOTA")),
            P("paso-vado", "Paso: el camino del río (cierra la misión)", Completar(MisionReino, "CAP4_VADO", cerrar: true)),
            P("fin-punto", "Cap. 4 · Fin (punto de guardado)", Punto("CAP4_FIN", "Cap4_Bota")),
            P("fin", "Fin del Cap. 4 (" + FinCap4 + ")", Emitir(FinCap4)),
            P("demo-rotulo", "Rótulo: fin de la Primera Parte", Rotulo("DEMO_FIN_ROTULO", "Fin de la Primera Parte. Gracias por jugar a la demo.", "End of Part One. Thank you for playing the demo.")),
            P("demo-fin", "Fin de la demo (en la build de la demo, a los créditos)", new FinDeLaDemoNode()),
            P("demo-destapa", "Fuera de la build de la demo: se destapa la pantalla y sigue el juego", Destapar()),
        };
        c4.Ramas.Add(new List<Paso>
        {
            P("figura-0", "4.3 · Una figura de barro, chamuscada", Esperar(S4.Figura)),
            P("figura-1", "4.3 · Queda apuntado", Flag(FlagFigurasChamuscadas)),
            P("figura-2", "4.3 · Estela: «¡Las tenías que proteger!»", Bocadillo("CAP4_BOC_FIGURA", "¡Te has salvado tú y has chamuscado a quien tenías que proteger!", "You saved yourself and scorched the ones you were meant to protect!", 3.5f), "figura-0"),
        });
        c4.Notas.Add(("nota", "Cap. 4 · El Bosque Prohibido y el Gólem (GDD § 5.8). Lo monta El Sendero ▸ Historia ▸ Montar los capítulos 3 y 4: los nodos «cap4-…» se rehacen. " +
                              "Escena: claro de Estela (−330, 65), arena del Gólem (−95, 65) con el barro, bifurcación junto al puente del camino 8. Al acabar: en la build de la demo, el corte; fuera de ella, sigue el juego."));
        informe.AppendLine("Cap. 4: assets y tablas listos.");
        return c4;
    }

    /// <summary>Piezas hechas en Blender (Tools/Modelos/Cap4/cap4.py).</summary>
    const string CarpetaCap4 = "Assets/Art/World/Cap4";
    /// <summary>Armazón de dos postes con tejadillo; la pieza «Campana» lleva el origen en su eje de giro.</summary>
    const string FbxCampana = CarpetaCap4 + "/CampanaDeLaPrueba.fbx";
    /// <summary>Mancha de barro de 4 m de radio (el de <see cref="ZonaDeBarro"/>), apoyada en y = 0.</summary>
    const string FbxCharco = CarpetaCap4 + "/CharcoDeBarro.fbx";
    static readonly (string material, Color color)[] ColoresCap4 =
    {
        ("Madera", new Color(0.42f, 0.28f, 0.16f)), ("MaderaOscura", new Color(0.30f, 0.19f, 0.11f)), ("Oro", new Color(0.95f, 0.75f, 0.25f)),
        ("Hierro", new Color(0.16f, 0.14f, 0.13f)), ("Cuerda", new Color(0.78f, 0.66f, 0.45f)), ("Piedra", new Color(0.56f, 0.54f, 0.50f)),
        ("Barro", new Color(0.36f, 0.26f, 0.16f)), ("BarroSeco", new Color(0.50f, 0.38f, 0.25f)), ("BarroMojado", new Color(0.22f, 0.16f, 0.10f)),
    };

    /// <summary>Pone el FBX de Blender dentro de <paramref name="padre"/>, en su origen.</summary>
    private static GameObject PiezaCap4(string fbx, Transform padre, StringBuilder informe)
    {
        if (!PiezaDeBlender.PrepararMateriales(fbx, CarpetaCap4 + "/Materiales", "Mat_Cap4_", ColoresCap4, informe)) return null;
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre.gameObject.scene);
        go.name = "Modelo";
        go.transform.SetParent(padre, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    /// <summary>La campana, las figuras de barro y el barro del arroyo de la arena del Gólem.</summary>
    static partial void PonerExtrasCap4(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var padre = Hijo(raiz, Cap4 + " · campana y barro");

        // La campana: su armazón con la campana dorada y el botón de interactuar.
        if (marcas.TryGetValue("Cap4_Campana", out var sitio))
        {
            var tramo = Hijo(padre, "La campana (tramo)");
            var campana = new GameObject("La campana");
            campana.transform.SetParent(tramo, false);
            campana.transform.SetPositionAndRotation(sitio.position, sitio.rotation);
            PiezaCap4(FbxCampana, campana.transform, informe);
            // Choque solo en los dos postes (a 0,72 m del centro): la cuerda y la campana no estorban.
            foreach (float x in new[] { -0.72f, 0.72f })
            {
                var poste = campana.AddComponent<CapsuleCollider>();
                poste.direction = 1; poste.center = new Vector3(x, 1.35f, 0f); poste.height = 2.7f; poste.radius = 0.14f;
            }
            var interactuable = PuntoDeInteraccion(campana.transform, 1.8f);
            var s = campana.AddComponent<SignalEmitter>();
            s.eventKey = S4.Tocada; s.trigger = SignalEmitter.TriggerType.OnInteract; s.once = true;
            EnTramo(tramo, Tramo(S4.Campana, S4.CampanaFin), campana);
        }

        // Las figuras de barro: arden con el fuego (Burnable) y avisan al grafo.
        var figuras = Hijo(padre, "Figuras de barro (tramo)");
        var grupo = new GameObject("Figuras");
        grupo.transform.SetParent(figuras, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Weapon/Scarecrow01_a01.prefab");
        var barroMat = MaterialDeColor(new Color(0.55f, 0.38f, 0.24f), "Cap4_Barro");
        var posiciones = new[] { new Vector2(-334f, 72f), new Vector2(-326f, 73f), new Vector2(-330f, 78f), new Vector2(-336f, 80f), new Vector2(-323f, 80f) };
        for (int i = 0; i < posiciones.Length && prefab != null; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = $"Figura de barro {i + 1}";
            go.transform.SetParent(grupo.transform, true);
            go.transform.position = AlSuelo(new Vector3(posiciones[i].x, float.NaN, posiciones[i].y));
            go.transform.rotation = Quaternion.Euler(0f, 180f + i * 23f, 0f);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = barroMat;
            if (go.GetComponentInChildren<Collider>() == null) go.AddComponent<CapsuleCollider>().height = 1.6f;
            var arde = go.AddComponent<Burnable>();
            arde.onBurned ??= new UnityEvent();
            var aviso = go.AddComponent<SignalEmitter>();
            aviso.eventKey = S4.Figura; aviso.trigger = SignalEmitter.TriggerType.Manual; aviso.once = true;
            UnityEventTools.AddPersistentListener(arde.onBurned, aviso.Send);
        }
        EnTramo(figuras, Tramo(S4.Campana, S4.CampanaFin), grupo);

        // El barro del arroyo, a un lado de la arena del Gólem.
        var charco = new GameObject("Barro del arroyo");
        charco.transform.SetParent(padre, false);
        charco.transform.position = AlSuelo(new Vector3(-86f, float.NaN, 74f)) + Vector3.up * 0.03f;
        var zona = charco.AddComponent<ZonaDeBarro>();
        var so = new SerializedObject(zona);
        so.FindProperty("radio").floatValue = 4f;
        so.ApplyModifiedPropertiesWithoutUndo();
        PiezaCap4(FbxCharco, charco.transform, informe);
        informe.AppendLine("Cap. 4: campana, figuras de barro y barro del arroyo.");
    }

    private static GameObject Primitiva(PrimitiveType tipo, Transform padre, string nombre, Vector3 local, Vector3 escala, Material mat)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (tipo == PrimitiveType.Cylinder && escala.y < 0.1f) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Material MaterialDeColor(Color color, string nombre, float brillo = 0f)
    {
        string ruta = "Assets/Art/Capitulos/" + nombre + ".mat";
        AsegurarCarpeta("Assets/Art/Capitulos");
        // URP Lit: un Quibli creado por código sin pasar por su inspector sale negro.
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = m == null;
        if (nuevo) m = new Material(shader);
        else m.shader = shader;
        m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", brillo);
        if (nuevo) AssetDatabase.CreateAsset(m, ruta);
        else EditorUtility.SetDirty(m);
        return m;
    }
}
