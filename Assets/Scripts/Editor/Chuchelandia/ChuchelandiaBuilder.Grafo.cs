using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El flujo de Chuchelandia: el grafo «Chuchelandia» (Assets/NarrativeGraph/Chuchelandia.asset) con
/// la misión de llegada (investigar el reino hasta que el heraldo cuenta la prueba), la misión de la
/// prueba, una señal por tramo, las cinemáticas (tabla en .Cinematicas.cs), la rama en
/// paralelo de la misión opcional (.Reino.cs); las misiones (QuestData) y su registro en Start.unity
/// (hub de grafos y catálogo del QuestManager).
///
/// Conexión con el Sendero, solo por señales: el grafo arranca con la escena (su NarrativeGraphStarter),
/// emite <see cref="Senal.PruebaEmpieza"/> para quien quiera saber que la prueba de Estela ha empezado
/// y emite <see cref="Senal.PruebaSuperada"/> al superarla. La vuelta es el portal de salida (PortalTrigger a «Sendero», ancla
/// <see cref="SenderoProgressionController.AnclaVueltaEstela"/>, flag
/// <see cref="SenderoProgressionController.FlagPruebaEstelaHecha"/>), como en la prueba de Will.
///
/// Los nodos que monta el menú llevan guid «chuche-…» y se rehacen en cada ejecución; los que se
/// añadan a mano con otro guid se conservan.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    public const string EtiquetaDelGrafo = "Chuchelandia";
    private const string RutaGrafo = "Assets/NarrativeGraph/Chuchelandia.asset";
    private const string RutaMision = "Assets/_QUEST/PRINCIPALES/CHUCHELANDIA/Q_CHUCHE_PRUEBA_ESTELA.asset";
    private const string RutaMisionBienvenida = "Assets/_QUEST/PRINCIPALES/CHUCHELANDIA/Q_CHUCHE_BIENVENIDA.asset";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string PrefijoDeNodo = "chuche-";
    private const string Capitulo = "Chuchelandia";

    public const string IdMision = "CHUCHE_PRUEBA_ESTELA";
    /// <summary>Misión de la llegada: investigar el reino hasta dar con el heraldo, que cuenta la prueba.</summary>
    public const string IdMisionBienvenida = "CHUCHE_BIENVENIDA";
    private const string PasoInvestigar = "CHUCHE_INVESTIGAR";
    private const string IdDelHeraldo = "Actor_Heraldo";

    /// <summary>
    /// Señales de la prueba. Las «*_EMPIEZA» (y los atuendos) las emite el grafo al empezar cada tramo:
    /// encienden sus disparadores y son el nodo desde el que se prueba ese tramo. Las «*_LLEGADA»,
    /// «*_GANADA», «*_HECHO(S)», «*_TERMINADO» y la salida las emite la escena.
    /// </summary>
    public static class Senal
    {
        /// <summary>Entrada: el Sendero abre la prueba de Estela (la emite el grafo del Sendero).</summary>
        public const string PruebaEmpieza = "PRUEBA_ESTELA_EMPIEZA";
        /// <summary>Salida: la prueba está superada (la espera el Sendero; abre el portal de vuelta).</summary>
        public const string PruebaSuperada = "PRUEBA_ESTELA_SUPERADA";

        public const string Llegada = "CHUCHE_LLEGADA";
        public const string PuertaEmpieza = "CHUCHE_PUERTA_EMPIEZA";
        public const string PuertaIntentos = "CHUCHE_PUERTA_INTENTOS";
        public const string VestidorEmpieza = "CHUCHE_VESTIDOR_EMPIEZA";
        public const string VestidorLlegada = "CHUCHE_VESTIDOR_LLEGADA";
        public const string AtuendoCandidata = "CHUCHE_ATUENDO_CANDIDATA";
        public const string CarreraEmpieza = "CHUCHE_CARRERA_EMPIEZA";
        public const string CarreraGanada = "CHUCHE_CARRERA_GANADA";
        public const string EscaladaEmpieza = "CHUCHE_ESCALADA_EMPIEZA";
        public const string EscaladaGanada = "CHUCHE_ESCALADA_GANADA";
        public const string PunteriaEmpieza = "CHUCHE_PUNTERIA_EMPIEZA";
        public const string PunteriaGanada = "CHUCHE_PUNTERIA_GANADA";
        public const string ClasesEmpieza = "CHUCHE_CLASES_EMPIEZA";
        public const string ClasesLlegada = "CHUCHE_CLASES_LLEGADA";
        public const string ClasesHechas = "CHUCHE_CLASES_HECHAS";
        public const string ReinoLibre = "CHUCHE_REINO_LIBRE";
        public const string SalonLlegada = "CHUCHE_SALON_LLEGADA";
        public const string DueloEmpieza = "CHUCHE_DUELO_EMPIEZA";
        public const string DueloTerminado = "CHUCHE_DUELO_TERMINADO";
        public const string TeQuieroHecho = "CHUCHE_TE_QUIERO_HECHO";
        public const string AtuendoVictoria = "CHUCHE_ATUENDO_VICTORIA";
        /// <summary>Al cruzar la Puerta del Corazón el vestido vuelve a ser purpurina: acaba el atuendo de victoria (la emite la cinemática de victoria).</summary>
        public const string VestidoPurpurina = "CHUCHE_VESTIDO_PURPURINA";
        public const string Salida = "CHUCHE_SALIDA";

        /// <summary>Misión opcional: tras el encargo aparecen las trompetillas por el reino (la emite el grafo).</summary>
        public const string TrompetillasBuscar = "CHUCHE_TROMPETILLAS_BUSCAR";
        /// <summary>Misión opcional: se ha recogido una trompetilla (la emite cada una; el grafo las cuenta).</summary>
        public const string Trompetilla = "CHUCHE_TROMPETILLA";
    }

    /// <summary>Pasos de la misión: id de condición y clave de su texto (quests_es/en.json).</summary>
    private static readonly (string condicion, string texto)[] PasosDeLaMision =
    {
        ("CHUCHE_PUERTA", "CHUCHE_PASO_PUERTA"),
        ("CHUCHE_CARRERA", "CHUCHE_PASO_CARRERA"),
        ("CHUCHE_ESCALADA", "CHUCHE_PASO_ESCALADA"),
        ("CHUCHE_PUNTERIA", "CHUCHE_PASO_PUNTERIA"),
        ("CHUCHE_CLASES", "CHUCHE_PASO_CLASES"),
        ("CHUCHE_DUELO", "CHUCHE_PASO_DUELO"),
        ("CHUCHE_VUELTA", "CHUCHE_PASO_VUELTA"),
    };

    // ── El grafo ────────────────────────────────────────────────────────────────────────────

    /// <summary>Un nodo del recorrido; sin salidas explícitas, sigue al siguiente de la lista.</summary>
    private sealed class Paso
    {
        public string Id, Titulo;
        public NarrativeNode Nodo;
        public string[] Salidas;
    }

    private static List<Paso> Recorrido() => new List<Paso>
    {
        P("inicio", "Inicio de Chuchelandia", new StartNode()),
        P("tras-sendero", "Puente con el Sendero: empieza la prueba de Estela (aviso para el resto del juego)", Emitir(Senal.PruebaEmpieza)),
        // 1 · Llegada
        P("llegada", "1 · Llegada a Chuchelandia (probar desde aquí)", Esperar(Senal.Llegada)),
        P("cine-llegada", "1 · Cinemática: la puerta rosa, la purpurina y «Oh, no»", Cinematica("cine-llegada")),
        // Al acabar la llegada (como en Cap. 1 y 2) empieza una misión que no destripa la prueba: todavía
        // nadie sabe de qué va. Su paso, investigar el reino, lleva al heraldo (marcado en el minimapa).
        P("bienvenida", "Misión: bienvenidos a Chuchelandia (aviso de misión nueva; paso: investigad el reino, el heraldo en el minimapa)",
          new StartQuestNode { questId = IdMisionBienvenida, noGiver = true }),
        // 2 · El heraldo y la Puerta del Corazón
        P("puerta-empieza", "2 · El heraldo y la Puerta del Corazón (probar desde aquí)", Emitir(Senal.PuertaEmpieza)),
        P("heraldo-hablar", "2 · Hablar con el heraldo, junto a la Puerta del Corazón", new WaitNpcInteractionNode { npcId = IdDelHeraldo }),
        P("mando-will-pergamino", "2 · Will al mando (la cinemática cuenta con él)", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
        P("cine-heraldo", "2 · Cinemática: el heraldo lee el pergamino", Cinematica("cine-heraldo")),
        P("paso-investigar", "Paso: investigar el reino (cierra la misión de la llegada)", Completar(PasoInvestigar, cerrar: true, mision: IdMisionBienvenida)),
        // Con la prueba ya contada sale el aviso de la misión de verdad; su primer paso, abrir la Puerta.
        P("mision", "Misión: la princesa más adorable (aviso de misión nueva; primer paso: intentad abrir la Puerta del Corazón)",
          new StartQuestNode { questId = IdMision, noGiver = true }),
        P("puerta-intentos", "2 · Los intentos con el candado: magia de Estela, ganzúa de Liam y empujón de Will, cambiando con la cruceta (o hablar con el heraldo)", Esperar(Senal.PuertaIntentos)),
        P("paso-puerta", "Paso: la Puerta del Corazón", Completar("CHUCHE_PUERTA")),
        // 3 · La candidata
        P("mando-will-ejercito", "3 · Will al mando (la cinemática cuenta con él)", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
        P("cine-ejercito", "3 · Cinemática: ¡una candidata! El ejército se lleva a Estela y viste a Will y a Liam", Cinematica("cine-ejercito")),
        P("estela-sale", "3 · Estela sale del grupo y de la escena: se la ha llevado el ejército", Grupo(GrupoNode.Accion.SaleDelGrupo, "Estela")),
        P("atuendo-candidata", "3 · Atuendos de candidata (también los emiten las cinemáticas de la candidata y del vestidor)", Emitir(Senal.AtuendoCandidata)),
        P("vestidor-empieza", "3 · Hacia el vestidor real (probar desde aquí)", Emitir(Senal.VestidorEmpieza)),
        P("vestidor-llegar", "3 · Will y Liam llegan al vestidor real", Esperar(Senal.VestidorLlegada)),
        P("estela-vuelve", "3 · Estela vuelve al grupo (sale del vestidor en la cinemática)", Grupo(GrupoNode.Accion.VuelveAlGrupo, "Estela")),
        P("mando-will-vestidor", "3 · Will al mando (la cinemática cuenta con él)", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
        P("cine-vestidor", "3 · Cinemática: el vestidor, el lazo y «con mucha rabia»", Cinematica("cine-vestidor")),
        // 4 · Pruebas de candidata
        P("carrera-empieza", "4 · Empieza la carrera (probar desde aquí)", Emitir(Senal.CarreraEmpieza)),
        P("carrera", "4 · Carrera de obstáculos (tanda 3)", Esperar(Senal.CarreraGanada)),
        P("paso-carrera", "Paso: la carrera", Completar("CHUCHE_CARRERA")),
        P("escalada-empieza", "4 · Empieza la escalada (probar desde aquí)", Emitir(Senal.EscaladaEmpieza)),
        P("escalada", "4 · Escalada de la torre (tanda 3)", Esperar(Senal.EscaladaGanada)),
        P("paso-escalada", "Paso: la escalada", Completar("CHUCHE_ESCALADA")),
        P("punteria-empieza", "4 · Empieza la puntería (probar desde aquí)", Emitir(Senal.PunteriaEmpieza)),
        P("punteria", "4 · Puntería (tanda 3)", Esperar(Senal.PunteriaGanada)),
        P("paso-punteria", "Paso: la puntería", Completar("CHUCHE_PUNTERIA")),
        // 5 · Clases de la Condesa
        P("clases-empieza", "5 · Hacia las clases de la Condesa (probar desde aquí)", Emitir(Senal.ClasesEmpieza)),
        P("clases-llegar", "5 · Llegan a las clases de la Condesa Merengue", Esperar(Senal.ClasesLlegada)),
        P("clases", "5 · Clases de la Condesa: reverencia, saludo, risita y cariño (concurso de práctica, como Estela)", Esperar(Senal.ClasesHechas)),
        P("paso-clases", "Paso: las clases", Completar("CHUCHE_CLASES")),
        // 6 · El reino libre
        P("reino-libre", "6 · El reino, libre: los habitantes hablan (probar desde aquí)", Emitir(Senal.ReinoLibre)),
        P("reino-fork", "6 · En paralelo: la misión opcional de las trompetillas (no bloquea el recorrido)", new ForkNode(), "salon-llegar", "tromp-hablar"),
        // 7-9 · Pompón, el duelo y «¡TE QUIERO!»
        P("salon-llegar", "7 · Entran en el salón del duelo", Esperar(Senal.SalonLlegada)),
        P("mando-will-pompon", "7 · Will al mando (la cinemática cuenta con él)", Grupo(GrupoNode.Accion.PasaAlMando, "Will")),
        P("cine-pompon", "7 · Cinemática: Su Alteza Pompón, «es un oso» y el heraldo que explota", Cinematica("cine-pompon")),
        P("duelo-empieza", "8 · Empieza el Duelo de Ternura (probar desde aquí)", Emitir(Senal.DueloEmpieza)),
        P("duelo", "8 · Duelo de Ternura: reverencia, canción de cuna y ojitos, como Estela (concurso por rondas, sin derrota)", Esperar(Senal.DueloTerminado)),
        P("te-quiero", "9 · «¡TE QUIERO!» como Will: el mantel, el sirope y la silla", Esperar(Senal.TeQuieroHecho)),
        P("cine-te-quiero", "9 · Cinemática: la caída en la fuente, la pancarta y la risa de Estela", Cinematica("cine-te-quiero")),
        P("paso-duelo", "Paso: el duelo", Completar("CHUCHE_DUELO")),
        // 10 · Victoria y vuelta
        P("cine-victoria", "10 · Cinemática: la tiara, «Uno», la Puerta hace clic y las collejas pegadas", Cinematica("cine-victoria")),
        P("atuendo-victoria", "10 · Atuendo de victoria (también lo emite la cinemática de victoria al coronarla)", Emitir(Senal.AtuendoVictoria)),
        P("superada", "10 · Prueba de Estela superada: se abre el portal al Sendero", Emitir(Senal.PruebaSuperada)),
        P("salida", "10 · Cruzan el portal de vuelta al Sendero", Esperar(Senal.Salida)),
        P("cierre", "Paso: la vuelta (cierra la misión)", Completar("CHUCHE_VUELTA", cerrar: true)),
    };

    /// <summary>Notas del lienzo: (id, texto, columna, fila).</summary>
    private static readonly (string id, string texto, int columna, int fila)[] Notas =
    {
        ("nota-sendero", "Conexión con el Sendero: el grafo arranca al cargar Chuchelandia (su NarrativeGraphStarter) y emite " + Senal.PruebaEmpieza +
                         " (el grupo llega al ancla «" + AnclaLlegada + "»). Al final se emite " + Senal.PruebaSuperada +
                         ", que abre el portal de vuelta (flag SENDERO_PRUEBA_ESTELA_HECHA, ancla SenderoTrasPruebaEstela).", 0, -1),
        ("nota-maqueta", "Escena: las llegadas son disparadores en «Chuchelandia (generado)/Disparadores»; lo jugable está en «Pruebas» " +
                         "(carrera, escalada, puntería, candado, clases, duelo y «¡TE QUIERO!»), cada cosa activa solo en su tramo y emitiendo su señal al acabar. " +
                         "Las cinemáticas son guiones horneados (Chuche_*.guion.txt, menú «Chuchelandia: hornear las cinemáticas»). " +
                         "Cada tramo empieza en un nodo «(probar desde aquí)» que emite su señal *_EMPIEZA. Antes de las cinemáticas que cuentan con Will, un nodo de grupo le pasa el mando.", 3, -1),
    };

    private const int Columnas = 6;
    private const float AnchoDeColumna = 380f, AltoDeFila = 240f;

    private static NarrativeGraph AsegurarGrafo(StringBuilder informe)
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (g == null)
        {
            g = ScriptableObject.CreateInstance<NarrativeGraph>();
            AssetDatabase.CreateAsset(g, RutaGrafo);
            informe.AppendLine("Grafo «Chuchelandia» creado.");
        }

        var recorrido = Recorrido();
        var rama = RamaDeLasTrompetillas();
        var ids = new HashSet<string>();
        PonerTramo(g, recorrido, 0, ids);
        PonerTramo(g, rama, (recorrido.Count + Columnas - 1) / Columnas + 1, ids);
        foreach (var (id, texto, columna, fila) in Notas)
        {
            Poner(g, PrefijoDeNodo + id, "Nota", new Vector2(columna * AnchoDeColumna, fila * AltoDeFila),
                new GraphNoteNode { note = texto }, null);
            ids.Add(PrefijoDeNodo + id);
        }
        // Lo que montó una versión anterior del menú y ya no está en el recorrido.
        g.nodes.RemoveAll(n => n != null && n.guid != null && n.guid.StartsWith(PrefijoDeNodo) && !ids.Contains(n.guid));

        g.startNodeGuid = PrefijoDeNodo + "inicio";
        EditorUtility.SetDirty(g);
        informe.AppendLine($"Grafo: {recorrido.Count} nodos del recorrido, {rama.Count} de la misión opcional y {Notas.Length} notas; " +
                           $"emite {Senal.PruebaEmpieza} al empezar y {Senal.PruebaSuperada} al superarla.");
        return g;
    }

    /// <summary>Pone una lista de nodos encadenados (cada uno sigue al siguiente, salvo los que declaran sus salidas) desde la fila dada.</summary>
    private static void PonerTramo(NarrativeGraph g, List<Paso> pasos, int filaInicial, HashSet<string> ids)
    {
        for (int i = 0; i < pasos.Count; i++)
        {
            var p = pasos[i];
            List<string> salidas = null;
            if (p.Salidas != null) salidas = p.Salidas.Select(s => PrefijoDeNodo + s).ToList();
            else if (i + 1 < pasos.Count) salidas = new List<string> { PrefijoDeNodo + pasos[i + 1].Id };
            var posicion = new Vector2((i % Columnas) * AnchoDeColumna, (filaInicial + i / Columnas) * AltoDeFila);
            Poner(g, PrefijoDeNodo + p.Id, p.Titulo, posicion, p.Nodo, salidas);
            ids.Add(PrefijoDeNodo + p.Id);
        }
    }

    /// <summary>Pone el nodo con ese guid (sustituye al que hubiera). Si el último no trae salidas, conserva las que tenía.</summary>
    private static void Poner(NarrativeGraph g, string id, string titulo, Vector2 posicion, NarrativeNode n, List<string> salidas)
    {
        var anterior = g.FindNode(id);
        n.guid = id;
        n.displayTitle = titulo;
        n.chapter = Capitulo;
        n.position = posicion;
        n.outputs = salidas != null ? salidas
                  : anterior?.outputs != null ? new List<string>(anterior.outputs) : new List<string>();
        if (anterior == null) g.nodes.Add(n);
        else g.nodes[g.nodes.IndexOf(anterior)] = n;
    }

    private static Paso P(string id, string titulo, NarrativeNode nodo, params string[] salidas) =>
        new Paso { Id = id, Titulo = titulo, Nodo = nodo, Salidas = salidas.Length > 0 ? salidas : null };

    private static NarrativeNode Esperar(string senal) => new WaitCustomEventNode { eventKey = senal };

    private static NarrativeNode Emitir(string senal) => new RaiseCustomEventNode { eventKey = senal };

    private static NarrativeNode Grupo(GrupoNode.Accion accion, string personaje) =>
        new GrupoNode { accion = accion, personaje = personaje, retirarDeLaEscena = true };

    private static NarrativeNode Completar(string condicion, bool cerrar = false, string mision = IdMision) => new CompleteQuestStepsNode
    {
        questId = mision,
        stepConditionIds = new List<string> { condicion },
        completeQuest = cerrar,
    };

    // ── La misión ───────────────────────────────────────────────────────────────────────────

    private static QuestData AsegurarMision(StringBuilder informe)
    {
        var mision = AssetDatabase.LoadAssetAtPath<QuestData>(RutaMision);
        if (mision == null)
        {
            AsegurarCarpeta(System.IO.Path.GetDirectoryName(RutaMision).Replace('\\', '/'));
            mision = ScriptableObject.CreateInstance<QuestData>();
            AssetDatabase.CreateAsset(mision, RutaMision);
            informe.AppendLine("Misión " + IdMision + " creada.");
        }
        mision.questId = IdMision;
        mision.displayNameId = "CHUCHE_PRUEBA_NAME";
        mision.displayName = "La princesa más adorable";
        mision.descriptionId = "CHUCHE_PRUEBA_DESC";
        mision.description = "Nadie sale de Chuchelandia hasta que se abra la Puerta del Corazón.";
        mision.detailedDescriptionId = "CHUCHE_PRUEBA_DETAIL";
        mision.detailedDescription = "";
        mision.steps = new QuestData.Step[PasosDeLaMision.Length];
        for (int i = 0; i < PasosDeLaMision.Length; i++)
            mision.steps[i] = new QuestData.Step { conditionId = PasosDeLaMision[i].condicion, descriptionId = PasosDeLaMision[i].texto, description = "" };
        EditorUtility.SetDirty(mision);
        return mision;
    }

    /// <summary>La misión de la llegada: un solo paso, investigar el reino (hablar con el heraldo).</summary>
    private static QuestData AsegurarMisionDeBienvenida(StringBuilder informe)
    {
        var mision = AssetDatabase.LoadAssetAtPath<QuestData>(RutaMisionBienvenida);
        if (mision == null)
        {
            AsegurarCarpeta(System.IO.Path.GetDirectoryName(RutaMisionBienvenida).Replace('\\', '/'));
            mision = ScriptableObject.CreateInstance<QuestData>();
            AssetDatabase.CreateAsset(mision, RutaMisionBienvenida);
            informe.AppendLine("Misión " + IdMisionBienvenida + " creada.");
        }
        mision.questId = IdMisionBienvenida;
        mision.displayNameId = "CHUCHE_BIENVENIDA_NAME";
        mision.displayName = "Bienvenidos a Chuchelandia";
        mision.descriptionId = "CHUCHE_BIENVENIDA_DESC";
        mision.description = "";
        mision.detailedDescriptionId = "CHUCHE_BIENVENIDA_DETAIL";
        mision.detailedDescription = "";
        mision.steps = new[] { new QuestData.Step { conditionId = PasoInvestigar, descriptionId = "CHUCHE_PASO_INVESTIGAR", description = "" } };
        EditorUtility.SetDirty(mision);
        return mision;
    }

    // ── Start.unity ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pone el grafo en el hub (etiqueta «Chuchelandia»), las misiones en el catálogo del QuestManager
    /// y Chuchelandia en las escenas donde se ve la interfaz de juego (EscenaJugable.PermitirInterfazDeJuego).
    /// Si Start ya estaba abierta con cambios sin guardar, se deja marcada para que la guarde Raúl.
    /// </summary>
    private static void RegistrarEnStart(NarrativeGraph grafo, StringBuilder informe, params QuestData[] misiones)
    {
        var escena = SceneManager.GetSceneByPath(RutaStart);
        bool cerrar = !escena.isLoaded;
        bool teniaCambios = !cerrar && escena.isDirty;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);

        bool cambia = false;
        var hub = Buscar<NarrativeGraphHub>(escena);
        if (hub == null) informe.AppendLine("AVISO: Start.unity no tiene NarrativeGraphHub.");
        else
        {
            var so = new SerializedObject(hub);
            var lista = so.FindProperty("graphs");
            bool esta = false;
            for (int i = 0; i < lista.arraySize; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("label").stringValue != EtiquetaDelGrafo) continue;
                esta = true;
                if (e.FindPropertyRelative("graph").objectReferenceValue != grafo)
                {
                    e.FindPropertyRelative("graph").objectReferenceValue = grafo;
                    cambia = true;
                }
            }
            if (!esta)
            {
                lista.arraySize++;
                var e = lista.GetArrayElementAtIndex(lista.arraySize - 1);
                e.FindPropertyRelative("label").stringValue = EtiquetaDelGrafo;
                e.FindPropertyRelative("graph").objectReferenceValue = grafo;
                e.FindPropertyRelative("initialBlackboardValues").arraySize = 0;
                cambia = true;
                informe.AppendLine($"Start: grafo «{EtiquetaDelGrafo}» en el hub.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        int interfaz = EscenaJugable.PermitirInterfazDeJuego(escena, System.IO.Path.GetFileNameWithoutExtension(RutaEscena));
        if (interfaz > 0)
        {
            cambia = true;
            informe.AppendLine($"Start: la interfaz de juego (HUD, aviso de misión nueva, menús) se ve también en Chuchelandia ({interfaz} piezas).");
        }

        var quests = Buscar<QuestManager>(escena);
        if (quests == null) informe.AppendLine("AVISO: Start.unity no tiene QuestManager.");
        else
        {
            var so = new SerializedObject(quests);
            var catalogo = so.FindProperty("questCatalog");
            foreach (var mision in misiones)
            {
                if (mision == null) continue;
                bool esta = false;
                for (int i = 0; i < catalogo.arraySize; i++)
                    if (catalogo.GetArrayElementAtIndex(i).objectReferenceValue == mision) esta = true;
                if (esta) continue;
                catalogo.arraySize++;
                catalogo.GetArrayElementAtIndex(catalogo.arraySize - 1).objectReferenceValue = mision;
                cambia = true;
                informe.AppendLine($"Start: misión {mision.questId} en el catálogo del QuestManager.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (cambia)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            if (teniaCambios) informe.AppendLine("AVISO: Start.unity tenía cambios sin guardar; no la guardo yo: guárdala tú.");
            else EditorSceneManager.SaveScene(escena);
        }
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
    }

    private static T Buscar<T>(Scene escena) where T : Component
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var c = raiz.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, System.IO.Path.GetFileName(ruta));
    }
}
