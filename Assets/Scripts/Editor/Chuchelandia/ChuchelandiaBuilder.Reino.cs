using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// El reino libre (tramo 6, de CHUCHE_REINO_LIBRE a la llegada al salón) y su misión opcional,
/// «Las trompetillas perdidas», con las piezas oficiales:
/// - Habitantes que hablan: gominolas del pack (sin su IA de demo) con un <see cref="NarrativeActor"/>
///   sin IA y un <see cref="Interactable"/>; lo que dicen es la frase de su pieza «Personaje desde aquí»
///   del grafo, que solo vale en el tramo. Sus charlas solo están encendidas en el tramo
///   (<see cref="ActivoEnTramo"/>); la población de demo del pack no se toca.
/// - El heraldo suplente encarga la misión: una copia suya ronda por el reino durante el tramo y,
///   con la prueba superada, el del reparto (en el salón) atiende la entrega (los dos con el mismo id
///   de actor). Entre medias (Pompón, el duelo, «¡TE QUIERO!») no se le puede hablar.
/// - La misión es una rama en paralelo del grafo (<see cref="RamaDeLasTrompetillas"/>, tras un
///   ForkNode: no bloquea el recorrido): charla → encargo → misión (con su frase «mientras está en
///   curso») → aparecen las trompetillas → contarlas (un paso por cada una) → volver → entrega →
///   salen del inventario → cierre (con frase después) → premio de Esencia.
/// - Cada trompetilla es un punto de <see cref="Tesoro"/> (pop-up de objeto conseguido, guardado en la
///   partida) que da la trompetilla del inventario, dice la frase de quien la coge
///   (<see cref="ReaccionSegunQuienLoPrueba"/>) y emite CHUCHE_TROMPETILLA.
/// Los sitios están en la tabla de marcas (tramo 6, .Montaje.cs); lo demás, en las tablas de abajo.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    public const string IdMisionTrompetillas = "CHUCHE_TROMPETILLAS";
    private const string IdDelSuplente = "Actor_HeraldoSuplente";
    private const string RutaMisionTrompetillas = "Assets/_QUEST/SECUNDARIA/CHUCHELANDIA/Q_CHUCHE_TROMPETILLAS.asset";
    private const string CarpetaDialogosDelReino = "Assets/_DIALOGUES/DIALOGUE NPCS/Chuchelandia";
    private const string RutaTrompetilla = "Assets/_ITEMS/IT_Trompetilla.asset";
    private const string RutaIconoTrompetilla = "Assets/Art/UI/Items/trompetilla.png";
    private const string RutaEsenciaDelPremio = "Assets/_ITEMS/IT_Esencia.asset";
    /// <summary>NPC del proyecto del que se toma el icono de «tiene algo para ti» (el mismo para todos).</summary>
    private const string NpcConIconoDeMision = "Assets/_NPCs/Victoria.prefab";
    private const string RegionDelReino = "CHUCHELANDIA";

    // ── Tabla de la misión ──────────────────────────────────────────────────────────────────
    /// <summary>Esencia al entregar las trompetillas (las otras secundarias dan 20-50).</summary>
    private const int PremioDeEsencia = 30;
    /// <summary>Trompetilla de juguete hecha para el juego (Tools/Modelos/generar_trompetilla.py): tubo y campana
    /// (submalla 0) y botones de los pistones (submalla 1). Colores lisos con el estilo (Quibli) de los personajes del pack.</summary>
    private const string ModeloDeTrompetilla = "Assets/Art/Items/Trompetilla/Trompetilla.obj";
    private static readonly Color Dorado = new Color(1f, 0.8f, 0.25f);
    private static readonly Color RosaBoton = new Color(1f, 0.45f, 0.7f);
    private const float TamanoDeLaTrompetilla = 0.8f;
    /// <summary>Marcas de las trompetillas (tabla de marcas, tramo 6): una trompetilla por marca.</summary>
    private static readonly string[] MarcasDeTrompetillas =
        { "M_Reino_Trompetilla1", "M_Reino_Trompetilla2", "M_Reino_Trompetilla3", "M_Reino_Trompetilla4", "M_Reino_Trompetilla5" };
    /// <summary>Frase al coger una, según quién vaya al mando.</summary>
    private static readonly (Slot, string)[] FrasesAlRecoger =
    {
        (Slot.Will, "CHUCHELANDIA_TROMPETILLAS_RECOGIDA_WILL"),
        (Slot.Liam, "CHUCHELANDIA_TROMPETILLAS_RECOGIDA_LIAM"),
        (Slot.Estela, "CHUCHELANDIA_TROMPETILLAS_RECOGIDA_ESTELA"),
    };

    // ── Tabla de diálogos ───────────────────────────────────────────────────────────────────
    // Asset (en CarpetaDialogosDelReino) y sus líneas: nombre de quien habla y clave del texto (cinematics_*.json).
    private const string Suplente = "CHAR_HERALDO_SUPLENTE", Gominola = "CHAR_GOMINOLA";
    private static readonly (string asset, (string quien, string clave)[] lineas)[] DialogosDelReino =
    {
        ("DLG_CHUCHE_TROMPETILLAS_ENCARGO", new[] { (Suplente, "CHUCHELANDIA_TROMPETILLAS_ENCARGO_01"),
                                                    (Suplente, "CHUCHELANDIA_TROMPETILLAS_ENCARGO_02"),
                                                    (Suplente, "CHUCHELANDIA_TROMPETILLAS_ENCARGO_03") }),
        ("DLG_CHUCHE_TROMPETILLAS_EN_CURSO", new[] { (Suplente, "CHUCHELANDIA_TROMPETILLAS_EN_CURSO") }),
        ("DLG_CHUCHE_TROMPETILLAS_TODAS", new[] { ("CHAR_WILL", "CHUCHELANDIA_TROMPETILLAS_TODAS") }),
        ("DLG_CHUCHE_TROMPETILLAS_ENTREGA", new[] { (Suplente, "CHUCHELANDIA_TROMPETILLAS_ENTREGA_01"),
                                                    (Suplente, "CHUCHELANDIA_TROMPETILLAS_ENTREGA_02"),
                                                    ("CHAR_ESTELA", "CHUCHELANDIA_TROMPETILLAS_ENTREGA_03") }),
        ("DLG_CHUCHE_TROMPETILLAS_DESPUES", new[] { (Suplente, "CHUCHELANDIA_TROMPETILLAS_DESPUES") }),
        ("DLG_CHUCHE_REINO_01", new[] { (Gominola, "CHUCHELANDIA_REINO_01") }),
        ("DLG_CHUCHE_REINO_02", new[] { (Gominola, "CHUCHELANDIA_REINO_02") }),
        ("DLG_CHUCHE_REINO_03", new[] { (Gominola, "CHUCHELANDIA_REINO_03") }),
        ("DLG_CHUCHE_REINO_04", new[] { (Gominola, "CHUCHELANDIA_REINO_04") }),
        ("DLG_CHUCHE_REINO_05", new[] { (Gominola, "CHUCHELANDIA_REINO_05") }),
        ("DLG_CHUCHE_REINO_06", new[] { (Gominola, "CHUCHELANDIA_REINO_06") }),
        ("DLG_CHUCHE_REINO_07", new[] { (Gominola, "CHUCHELANDIA_REINO_07") }),
        ("DLG_CHUCHE_REINO_08", new[] { (Gominola, "CHUCHELANDIA_REINO_08") }),
    };

    // ── Tabla de habitantes ─────────────────────────────────────────────────────────────────
    // Papel (prefab del pack, id de actor estable de su agenda, controlador o clip de reposo), marca
    // (tabla de marcas, tramo 6) y las frases de su agenda (diálogos de la tabla de arriba; rotan).
    private static readonly (Papel papel, string marca, string[] frases)[] HabitantesDelReino =
    {
        (Habitante(1, "Grummy_02", Demo + "/Animation/Grummy_Controller.controller"), "M_Reino_Gominola1",
            new[] { "DLG_CHUCHE_REINO_01", "DLG_CHUCHE_REINO_08" }),
        (Habitante(2, "Marshie_02", Demo + "/Animation/Marshie_Controller.controller"), "M_Reino_Gominola2", new[] { "DLG_CHUCHE_REINO_02" }),
        (Habitante(3, "Sweetie_02", Demo + "/Animation/Swiitie_Controller.controller"), "M_Reino_Gominola3", new[] { "DLG_CHUCHE_REINO_03" }),
        (Habitante(4, "Grummy_04", Demo + "/Animation/Grummy_Controller.controller"), "M_Reino_Gominola4", new[] { "DLG_CHUCHE_REINO_04" }),
        (Habitante(5, "Sweetbloom_01", null, Pack + "/Characters/Animations/Sweetblooms/Sweetblooms_Idle.anim"), "M_Reino_Gominola5",
            new[] { "DLG_CHUCHE_REINO_06" }),
        (Habitante(6, "EyeBud_02", null, Pack + "/Characters/Animations/Eye-Buds/Eye-Buds_Idle_1.anim"), "M_Reino_Gominola6",
            new[] { "DLG_CHUCHE_REINO_05", "DLG_CHUCHE_REINO_07" }),
    };

    private static Papel Habitante(int n, string modelo, string controlador, string reposo = null) => new Papel
    {
        Nombre = $"Gominola del reino {n}", ActorId = $"Actor_ChucheReino{n}", Prefab = $"{Pack}/Characters/Prefabs/{modelo}.prefab",
        Controlador = controlador, Reposo = reposo,
    };

    // ── Assets ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Diálogos, agendas de los habitantes, la trompetilla del inventario y la misión. Los diálogos, las
    /// agendas y la misión se rehacen con las tablas; la trompetilla se crea una vez (luego se retoca
    /// en su Inspector). Devuelve la misión.
    /// </summary>
    private static QuestData AsegurarReino(StringBuilder informe)
    {
        AsegurarCarpeta(CarpetaDialogosDelReino);
        foreach (var (asset, lineas) in DialogosDelReino)
        {
            string ruta = $"{CarpetaDialogosDelReino}/{asset}.asset";
            var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
            if (dialogo == null)
            {
                dialogo = ScriptableObject.CreateInstance<DialogueAsset>();
                AssetDatabase.CreateAsset(dialogo, ruta);
            }
            dialogo.lines = new DialogueLine[lineas.Length];
            for (int i = 0; i < lineas.Length; i++)
                dialogo.lines[i] = new DialogueLine { speakerNameId = lineas[i].quien, textId = lineas[i].clave };
            EditorUtility.SetDirty(dialogo);
        }

        AsegurarTrompetillaDelInventario(informe);
        var mision = AsegurarMisionDeLasTrompetillas();
        informe.AppendLine($"Reino: {DialogosDelReino.Length} diálogos, {HabitantesDelReino.Length} habitantes con frases, misión {IdMisionTrompetillas}.");
        return mision;
    }

    /// <summary>Lo que dicen los habitantes mientras el reino está libre: piezas «Personaje desde aquí» en el grafo (ya montado).</summary>
    private static void PonerPiezasDelReino(StringBuilder informe)
    {
        var situaciones = new List<Sendero.Narrative.Editor.PiezasDePersonaje.Situacion>();
        foreach (var (papel, _, frases) in HabitantesDelReino)
        {
            var s = new Sendero.Narrative.Editor.PiezasDePersonaje.Situacion { actor = papel.ActorId, nombre = "Chuchelandia · el reino libre", presencia = ActorPresence.Unchanged };
            s.empieza.Add(Senal.ReinoLibre);
            s.acaba.Add(Senal.SalonLlegada);
            foreach (var frase in frases) s.frases.Add(Dialogo(frase));
            situaciones.Add(s);
        }
        Sendero.Narrative.Editor.PiezasDePersonaje.Poner(situaciones, informe);
    }

    private static DialogueAsset Dialogo(string asset) =>
        AssetDatabase.LoadAssetAtPath<DialogueAsset>($"{CarpetaDialogosDelReino}/{asset}.asset");

    private static void AsegurarTrompetillaDelInventario(StringBuilder informe)
    {
        if (AssetDatabase.LoadAssetAtPath<ItemData>(RutaTrompetilla) != null) return;
        if (AssetImporter.GetAtPath(RutaIconoTrompetilla) is TextureImporter importador && importador.textureType != TextureImporterType.Sprite)
        {
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.SaveAndReimport();
        }
        var objeto = ScriptableObject.CreateInstance<ItemData>();
        objeto.itemId = "trompetilla";
        objeto.displayNameId = "ITEM_TROMPETILLA_NAME";
        objeto.displayName = "Trompetilla";
        objeto.pluralDisplayNameId = "ITEM_TROMPETILLA_PLURAL";
        objeto.pluralDisplayName = "Trompetillas";
        objeto.icon = AssetDatabase.LoadAssetAtPath<Sprite>(RutaIconoTrompetilla);
        objeto.useDescriptionId = "ITEM_TROMPETILLA_DESC";
        objeto.useDescription = "Una trompetilla dorada del ejército de gominolas.";
        objeto.usageKind = ItemData.ItemUsageKind.Quest;
        objeto.buyPrice = 0;
        objeto.sellValue = 0;
        AssetDatabase.CreateAsset(objeto, RutaTrompetilla);
        informe.AppendLine($"Objeto creado: {RutaTrompetilla}{(objeto.icon == null ? " (SIN icono: revisa " + RutaIconoTrompetilla + ")" : "")}.");
    }

    private static QuestData AsegurarMisionDeLasTrompetillas()
    {
        var mision = AssetDatabase.LoadAssetAtPath<QuestData>(RutaMisionTrompetillas);
        if (mision == null)
        {
            AsegurarCarpeta(System.IO.Path.GetDirectoryName(RutaMisionTrompetillas).Replace('\\', '/'));
            mision = ScriptableObject.CreateInstance<QuestData>();
            AssetDatabase.CreateAsset(mision, RutaMisionTrompetillas);
        }
        mision.questId = IdMisionTrompetillas;
        mision.displayNameId = "CHUCHE_TROMPETILLAS_NAME";
        mision.displayName = "Las trompetillas perdidas";
        mision.descriptionId = "CHUCHE_TROMPETILLAS_DESC";
        mision.description = "";
        mision.detailedDescriptionId = "CHUCHE_TROMPETILLAS_DETAIL";
        mision.detailedDescription = "";
        // Un paso por trompetilla: el grafo marca uno por cada una que se coge (el diario enseña 2/5).
        mision.steps = new QuestData.Step[MarcasDeTrompetillas.Length];
        for (int i = 0; i < mision.steps.Length; i++)
            mision.steps[i] = new QuestData.Step { descriptionId = "CHUCHE_TROMPETILLAS_STEP", description = "", conditionId = "" };
        EditorUtility.SetDirty(mision);
        return mision;
    }

    // ── El grafo ────────────────────────────────────────────────────────────────────────────

    /// <summary>La rama en paralelo de la misión opcional (sale del ForkNode «reino-fork» del recorrido).</summary>
    private static List<Paso> RamaDeLasTrompetillas()
    {
        int n = MarcasDeTrompetillas.Length;
        return new List<Paso>
        {
            P("tromp-hablar", "6 · Opcional: hablar con el heraldo suplente", new WaitNpcInteractionNode { npcId = IdDelSuplente }),
            P("tromp-encargo", "6 · El suplente encarga las trompetillas",
              new PlayDialogueNode { dialogue = Dialogo("DLG_CHUCHE_TROMPETILLAS_ENCARGO"), npcId = IdDelSuplente }),
            P("tromp-mision", "Misión opcional: las trompetillas perdidas", new StartQuestNode
            {
                questId = IdMisionTrompetillas, giverId = IdDelSuplente, inProgressDialogue = Dialogo("DLG_CHUCHE_TROMPETILLAS_EN_CURSO"),
            }),
            P("tromp-buscar", "6 · Aparecen las trompetillas por el reino", Emitir(Senal.TrompetillasBuscar)),
            P("tromp-recoger", $"6 · Coger las {n} trompetillas (marca un paso de la misión por cada una)",
              new WaitSignalCountNode { eventKey = Senal.Trompetilla, count = n, questId = IdMisionTrompetillas }),
            P("tromp-todas", "6 · «¡Ya están todas!»", new PlayDialogueNode { dialogue = Dialogo("DLG_CHUCHE_TROMPETILLAS_TODAS") }),
            P("tromp-volver", "6 · Volver con el heraldo suplente (en el reino o, con la prueba superada, en el salón)",
              new WaitNpcInteractionNode { npcId = IdDelSuplente }),
            P("tromp-entrega", "6 · La entrega: «¿Decidida? Me vale»",
              new PlayDialogueNode { dialogue = Dialogo("DLG_CHUCHE_TROMPETILLAS_ENTREGA"), npcId = IdDelSuplente }),
            P("tromp-devolver", "6 · Las trompetillas salen del inventario", new RequireInventoryItemNode
            {
                item = AssetDatabase.LoadAssetAtPath<ItemData>(RutaTrompetilla), requiredAmount = n, consumeOnSuccess = true,
                useAlternateOutputOnMissing = false,
            }),
            P("tromp-cierre", "Cierra la misión de las trompetillas (el suplente queda con su frase de después)", new CompleteQuestStepsNode
            {
                questId = IdMisionTrompetillas, completeQuest = true,
                standingActorId = IdDelSuplente, standingDialogue = Dialogo("DLG_CHUCHE_TROMPETILLAS_DESPUES"),
            }),
            P("tromp-premio", $"Premio: {PremioDeEsencia} de Esencia", new GiveInventoryItemNode
            {
                item = AssetDatabase.LoadAssetAtPath<ItemData>(RutaEsenciaDelPremio), amount = PremioDeEsencia, onlyOnce = true,
            }),
        };
    }

    // ── La escena ───────────────────────────────────────────────────────────────────────────

    private static void MontarReino(Scene escena, Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var reino = Hijo(raiz, "Reino libre");
        var icono = IconoDeMision(informe);

        // Habitantes: siempre en su sitio; se les puede hablar solo en el tramo.
        var habitantes = Hijo(reino, "Habitantes");
        var charlas = new List<GameObject>();
        foreach (var (papel, marca, _) in HabitantesDelReino)
        {
            if (!marcas.TryGetValue(marca, out var sitio)) { informe.AppendLine($"Reino: falta la marca {marca} ({papel.Nombre})."); continue; }
            var go = PonerPapel(escena, habitantes, papel, sitio.position, sitio.rotation, informe);
            if (go != null) charlas.Add(Charla(go.transform, papel.ActorId, icono));
        }
        EncenderEnTramo(habitantes, charlas, new[] { Senal.ReinoLibre }, Senal.SalonLlegada);

        // El heraldo suplente: una copia ronda por el reino en el tramo; con la prueba superada, el del reparto.
        var original = System.Array.Find(TablaDeReparto, p => p.ActorId == IdDelSuplente);
        var suplentes = Hijo(reino, "Heraldo suplente");
        if (original != null && marcas.TryGetValue("M_Reino_Suplente", out var sitioDelSuplente))
        {
            var copia = new Papel { Nombre = "Heraldo suplente en el reino", ActorId = IdDelSuplente, Prefab = original.Prefab,
                                    Controlador = original.Controlador, Reposo = original.Reposo, Tinte = original.Tinte };
            var enElReino = PonerPapel(escena, suplentes, copia, sitioDelSuplente.position, sitioDelSuplente.rotation, informe);
            if (enElReino != null)
            {
                Charla(enElReino.transform, IdDelSuplente, icono);
                EncenderEnTramo(suplentes, new List<GameObject> { enElReino }, new[] { Senal.ReinoLibre }, Senal.SalonLlegada);
            }
        }
        else informe.AppendLine("Reino: falta la marca M_Reino_Suplente o el suplente en la tabla de reparto.");
        var delSalon = Actor(raiz.Find("Reparto"), IdDelSuplente);
        if (delSalon != null)
            EncenderEnTramo(suplentes, new List<GameObject> { Charla(delSalon, IdDelSuplente, icono) }, new[] { Senal.PruebaSuperada }, null);
        else informe.AppendLine("Reino: no encuentro al heraldo suplente del reparto; la entrega solo se podrá hacer en el reino.");

        int trompetillas = MontarTrompetillas(Hijo(reino, "Trompetillas"), marcas, informe);
        informe.AppendLine($"Reino: {charlas.Count} habitantes que hablan (de {Senal.ReinoLibre} a {Senal.SalonLlegada}), el heraldo suplente " +
                           $"y {trompetillas} trompetillas (aparecen con {Senal.TrompetillasBuscar}).");
    }

    /// <summary>Cada trompetilla: punto de tesoro con su modelo dorado, la frase de quien la coge y la señal para el grafo.</summary>
    private static int MontarTrompetillas(Transform padre, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var lista = new List<GameObject>();
        var materiales = MaterialesDeTrompetilla(informe);
        for (int i = 0; i < MarcasDeTrompetillas.Length; i++)
        {
            if (!marcas.TryGetValue(MarcasDeTrompetillas[i], out var sitio)) { informe.AppendLine($"Trompetillas: falta la marca {MarcasDeTrompetillas[i]}."); continue; }
            // Tumbada en el suelo, un poco ladeada para que se vean los pistones.
            var modelo = Atrezo(padre, ModeloDeTrompetilla, "Trompetilla", sitio.position + Vector3.up * 0.05f,
                                sitio.rotation * Quaternion.Euler(0f, 0f, 12f), TamanoDeLaTrompetilla, informe);
            if (modelo == null) continue;
            foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (materiales != null)
                foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = r.sharedMaterials.Length > 1 ? materiales : new[] { materiales[0] };

            var tesoro = CombatLabBuilder.CrearTesoro($"Trompetilla {i + 1}", padre, sitio.position, sitio.eulerAngles.y,
                                                      $"CHUCHE_TROMPETILLA_{i + 1}", Tesoro.Forma.Punto, RegionDelReino, visualDelPunto: modelo);
            CombatLabBuilder.Recompensas(tesoro, new CombatLabBuilder.Premio(PickupEffectType.AddToInventory, RutaTrompetilla, 1));

            var reaccion = tesoro.gameObject.AddComponent<ReaccionSegunQuienLoPrueba>();
            var reacciones = new List<ReaccionSegunQuienLoPrueba.Reaccion>();
            foreach (var (quien, clave) in FrasesAlRecoger)
                reacciones.Add(new ReaccionSegunQuienLoPrueba.Reaccion { personaje = quien, como = ReaccionSegunQuienLoPrueba.Como.Interactuando,
                                                                         cuenta = false, frases = Frases((quien, clave)) });
            Asignar(reaccion, "reacciones", reacciones);
            UnityEventTools.AddPersistentListener(tesoro.GetComponent<Interactable>().OnInteract, reaccion.AlInteractuar);

            var emisor = tesoro.gameObject.AddComponent<SignalEmitter>();
            emisor.eventKey = Senal.Trompetilla;
            emisor.trigger = SignalEmitter.TriggerType.Manual;
            emisor.once = true;
            if (tesoro.alAbrirse == null) tesoro.alAbrirse = new UnityEvent();
            UnityEventTools.AddPersistentListener(tesoro.alAbrirse, emisor.Send);
            lista.Add(tesoro.gameObject);
        }
        EncenderEnTramo(padre, lista, new[] { Senal.TrompetillasBuscar }, null);
        return lista.Count;
    }

    /// <summary>Dorado (tubo y campana) y rosa (botones), lisos con el estilo de los personajes del pack.</summary>
    private static Material[] MaterialesDeTrompetilla(StringBuilder informe)
    {
        var estilo = PersonajeHechoEnBlender.MaterialDeEstilo(EstiloDeLosPersonajes);
        if (estilo == null) { informe.AppendLine($"Trompetillas: no encuentro el material de {EstiloDeLosPersonajes}; se quedan con el del modelo."); return null; }
        return new[]
        {
            PersonajeHechoEnBlender.Liso(estilo, $"{CarpetaReparto}/Materiales/Mat_Trompetilla.mat", Dorado),
            PersonajeHechoEnBlender.Liso(estilo, $"{CarpetaReparto}/Materiales/Mat_Trompetilla_Botones.mat", RosaBoton),
        };
    }

    /// <summary>
    /// Lo que hace hablable a un personaje del pack: un hijo con <see cref="NarrativeActor"/> sin IA (su id
    /// de actor) y un <see cref="Interactable"/> con zona e icono de la A. Devuelve ese hijo.
    /// </summary>
    private static GameObject Charla(Transform personaje, string actorId, GameObject iconoDeMision)
    {
        var go = new GameObject("Charla");
        go.transform.SetParent(personaje, false);
        Vector3 escala = personaje.lossyScale;
        go.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, escala.x), 1f / Mathf.Max(0.001f, escala.y), 1f / Mathf.Max(0.001f, escala.z));
        // El icono de A donde lo llevan todos los NPCs del juego (en su base, como Eldran o Tomasa), no sobre la cabeza.
        PuntoDeInteraccion(go.transform, Vector3.zero, 2.2f, 0f, enEsteObjeto: true);
        var actor = go.AddComponent<NarrativeActor>();
        Fijar(actor, ("actorIdSinIA", actorId), ("defaultQuestIconPrefab", iconoDeMision));
        return go;
    }

    /// <summary>Un <see cref="ActivoEnTramo"/> en <paramref name="padre"/> que enciende <paramref name="objetos"/> en el tramo (sin fin si <paramref name="acaba"/> es null).</summary>
    private static void EncenderEnTramo(Transform padre, List<GameObject> objetos, string[] empieza, string acaba)
    {
        if (objetos.Count == 0) return;
        var tramo = padre.gameObject.AddComponent<ActivoEnTramo>();
        tramo.Tramo.empiezaConSenal.AddRange(empieza);
        if (!string.IsNullOrEmpty(acaba)) tramo.Tramo.acabaConSenal.Add(acaba);
        foreach (var o in objetos)
        {
            tramo.Objetos.Add(o);
            o.SetActive(false);
        }
    }

    /// <summary>El icono de «tiene algo para ti» de los NPCs del proyecto (el de <see cref="NpcConIconoDeMision"/>).</summary>
    private static GameObject IconoDeMision(StringBuilder informe)
    {
        var npc = AssetDatabase.LoadAssetAtPath<GameObject>(NpcConIconoDeMision);
        var icono = npc != null ? npc.GetComponent<NarrativeActor>()?.DefaultQuestIconPrefab : null;
        if (icono == null) informe.AppendLine($"AVISO: no encuentro el icono de misión en {NpcConIconoDeMision}; el suplente no enseñará el suyo.");
        return icono;
    }
}
