using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Montador de los capítulos 3 y 4 (el tramo de la historia que faltaba: de la despedida del pueblo a
/// Liam en el vado). Con el patrón de Chuchelandia: cada capítulo es un grafo
/// (Assets/NarrativeGraph/Cap3.asset, Cap4.asset) cuyos nodos llevan guid «cap3-…»/«cap4-…» y se
/// rehacen en cada ejecución (los que se añadan a mano con otro guid se conservan); misiones,
/// objetos, diálogos y textos ES/EN; las cinemáticas (guiones horneados, una SequenceDefinition y
/// un SequencePlayer por guion); y en MainWorld, bajo «Capítulos (generado)», las marcas,
/// los puntos del roster, los disparadores y los puntos de inspección, que se rehacen enteros.
///
/// Encadenado solo por señales: el Cap. 2 emite CH_Cap2_BACK_1 → Cap. 3 → CH_Cap3_BACK_1 → Cap. 4
/// → CH_Cap4_BACK_1 → fin de la demo (<see cref="FinDeLaDemoNode"/>). Cada tramo empieza en un nodo
/// «(probar desde aquí)» que emite su señal *_EMPIEZA.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string RutaRoster = "Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset";
    private const string CarpetaMisiones = "Assets/_QUEST/PRINCIPALES";
    private const string CarpetaDialogos = "Assets/_DIALOGUES";
    private const string CarpetaItems = "Assets/_ITEMS";
    private const string CarpetaSecuencias = "Assets/_SEQUENCES";
    private const string CarpetaGuiones = "Assets/_SEQUENCES/Guiones";
    private const string CarpetaAgendas = "Assets/Resources/Agendas";
    private const string PerfilDeAudio = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    public const string NombreGenerado = "Capítulos (generado)";
    private const string TituloMenu = "Historia: montar los capítulos 3 y 4";

    public const string FinCap2 = "CH_Cap2_BACK_1";
    public const string FinCap3 = "CH_Cap3_BACK_1";
    public const string FinCap4 = "CH_Cap4_BACK_1";

    // ── Menú ────────────────────────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Historia/Montar los capítulos 3 y 4")]
    public static void Montar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar(TituloMenu, "Sal de Play antes de montar."); return; }
        var informe = new StringBuilder();
        LimpiarTablas();

        Cap2ExtrasPrefabs.Asegurar();
        AsegurarConfiguracionDeLaDemo(informe);
        MontarAssetsDespedida(informe);
        var cap3 = MontarAssetsCap3(informe);
        var cap4 = MontarAssetsCap4(informe);
        AsegurarSecuencias(informe);
        var g3 = AsegurarGrafo(cap3, informe);
        var g4 = AsegurarGrafo(cap4, informe);
        AjustarCap2(informe);
        AjustarSendero(informe);
        AsegurarRoster(informe);
        AsegurarAgendas(informe);
        RegistrarEnStart(informe, (cap3.Etiqueta, g3), (cap4.Etiqueta, g4));
        MontarEnMainWorld(informe, cap3.Etiqueta, cap4.Etiqueta);
        int textos = TextosDeLaHistoria.Escribir();
        informe.AppendLine($"Textos: {textos} claves en las tablas ES/EN.");
        AssetDatabase.SaveAssets();

        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "Historia");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "montaje.md"), "# Montaje de los capítulos 3 y 4\n\n" + informe);
        Debug.Log("[MontajeDeLosCapitulos] " + informe);
        OrdenesDelEditor.Avisar(TituloMenu, informe.ToString());
    }

    // ── El grafo ────────────────────────────────────────────────────────────────────────────

    /// <summary>Un capítulo: su grafo, su prefijo de nodos y su recorrido.</summary>
    private sealed class Capitulo
    {
        public string Etiqueta, Ruta, Prefijo, Nombre;
        public List<Paso> Recorrido = new();
        public List<List<Paso>> Ramas = new();
        public List<(string id, string texto)> Notas = new();
    }

    /// <summary>Un nodo del recorrido; sin salidas explícitas, sigue al siguiente de su lista.</summary>
    private sealed class Paso
    {
        public string Id, Titulo;
        public NarrativeNode Nodo;
        public string[] Salidas;
    }

    private const int Columnas = 6;
    private const float AnchoDeColumna = 380f, AltoDeFila = 240f;

    private static Paso P(string id, string titulo, NarrativeNode nodo, params string[] salidas) =>
        new Paso { Id = id, Titulo = titulo, Nodo = nodo, Salidas = salidas.Length > 0 ? salidas : null };

    private static NarrativeGraph AsegurarGrafo(Capitulo c, StringBuilder informe)
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(c.Ruta);
        if (g == null)
        {
            g = ScriptableObject.CreateInstance<NarrativeGraph>();
            AssetDatabase.CreateAsset(g, c.Ruta);
            informe.AppendLine($"Grafo «{c.Etiqueta}» creado.");
        }
        var ids = new HashSet<string>();
        int fila = 0;
        PonerTramo(g, c, c.Recorrido, ref fila, ids);
        foreach (var rama in c.Ramas) { fila++; PonerTramo(g, c, rama, ref fila, ids); }
        for (int i = 0; i < c.Notas.Count; i++)
        {
            string id = c.Prefijo + c.Notas[i].id;
            Poner(g, c, id, "Nota", new Vector2(i * 3 * AnchoDeColumna, -AltoDeFila), new GraphNoteNode { note = c.Notas[i].texto }, null);
            ids.Add(id);
        }
        // Lo que montó una versión anterior del menú y ya no está en el recorrido.
        g.nodes.RemoveAll(n => n != null && n.guid != null && n.guid.StartsWith(c.Prefijo) && !ids.Contains(n.guid));
        g.startNodeGuid = c.Prefijo + c.Recorrido[0].Id;
        EditorUtility.SetDirty(g);

        // Aristas que apuntan a nodos que no existen: error de la tabla.
        foreach (var n in g.nodes)
            foreach (var s in n.outputs)
                if (!string.IsNullOrEmpty(s) && g.FindNode(s) == null)
                    informe.AppendLine($"ERROR · {c.Etiqueta}: «{n.displayTitle}» lleva a «{s}», que no existe.");
        informe.AppendLine($"Grafo «{c.Etiqueta}»: {ids.Count} nodos.");
        return g;
    }

    private static void PonerTramo(NarrativeGraph g, Capitulo c, List<Paso> pasos, ref int fila, HashSet<string> ids)
    {
        int filaInicial = fila;
        for (int i = 0; i < pasos.Count; i++)
        {
            var p = pasos[i];
            List<string> salidas;
            if (p.Salidas != null) salidas = p.Salidas.Select(s => s == "" ? "" : c.Prefijo + s).ToList();
            else if (i + 1 < pasos.Count) salidas = new List<string> { c.Prefijo + pasos[i + 1].Id };
            else salidas = new List<string>();
            var posicion = new Vector2((i % Columnas) * AnchoDeColumna, (filaInicial + i / Columnas) * AltoDeFila);
            Poner(g, c, c.Prefijo + p.Id, p.Titulo, posicion, p.Nodo, salidas);
            ids.Add(c.Prefijo + p.Id);
        }
        fila = filaInicial + (pasos.Count + Columnas - 1) / Columnas;
    }

    private static void Poner(NarrativeGraph g, Capitulo c, string id, string titulo, Vector2 posicion, NarrativeNode n, List<string> salidas)
    {
        var anterior = g.FindNode(id);
        n.guid = id;
        n.displayTitle = titulo;
        n.chapter = c.Nombre;
        n.position = posicion;
        n.outputs = salidas ?? new List<string>();
        if (anterior == null) g.nodes.Add(n);
        else g.nodes[g.nodes.IndexOf(anterior)] = n;
    }

    // Nodos frecuentes.
    private static NarrativeNode Esperar(string senal, bool antes = true) => new WaitCustomEventNode { eventKey = senal, acceptEarlierEvent = antes };
    private static NarrativeNode Emitir(string senal) => new RaiseCustomEventNode { eventKey = senal };
    private static NarrativeNode Flag(string flag) => new SetFlagNode { flagKey = flag };
    private static NarrativeNode Hablar(string npc) => new WaitNpcInteractionNode { npcId = npc };
    private static NarrativeNode Dialogo(string ruta, string npc = "") => new PlayDialogueNode { dialogue = Cargar<DialogueAsset>(ruta), npcId = npc };
    private static NarrativeNode Punto(string id, string ancla) => new CheckpointNode { checkpointId = id, spawnAnchorId = ancla, sceneName = "MainWorld" };
    private static NarrativeNode Llevar(string actor, string marca) => new PlaceActorNode { actorId = actor, markId = marca, applyPlayerEnvironment = actor == "Player" };
    private static NarrativeNode Grupo(GrupoNode.Accion accion, string personaje, bool retirar = false) =>
        new GrupoNode { accion = accion, personaje = personaje, retirarDeLaEscena = retirar };
    /// <summary>
    /// Destapa la pantalla. Un rótulo, o una cinemática que acaba en «fundido cubre», dejan la
    /// pantalla tapada para quien venga después; si lo que viene es jugar, hay que destaparla.
    /// </summary>
    private static NarrativeNode Destapar() => new ScreenFadeNode { fadeIn = false, duration = 1f, restaurarMusicaDeEscena = true };
    private static NarrativeNode Rotulo(string clave, string es, string en)
    {
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, clave, es, en);
        return new TitleCardNode { textId = clave, text = es };
    }
    /// <summary>
    /// Aviso de tutorial. Sin <paramref name="esperar"/> no para el juego y se quita solo al empezar
    /// un diálogo o una cinemática; con él, el grafo espera a que el jugador lo cierre (para un aviso
    /// al que sigue enseguida una cinemática, que si no lo quitaría antes de leerlo).
    /// </summary>
    private static NarrativeNode Tutorial(string clave, string es, string en, string boton = Core.InputGlyphs.InputGlyphNames.North, bool esperar = false)
    {
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, clave, es, en);
        return new TutorialPromptNode { textId = clave, text = es, buttonName = boton, dismissWithCancel = !esperar, cerrarAlEmpezarDialogo = !esperar };
    }
    private static NarrativeNode Bocadillo(string clave, string es, string en, float dura = 3f)
    {
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, clave, es, en);
        // Que dé tiempo a leerlo: unas 14 letras por segundo y un respiro, aunque se pida menos.
        float paraLeer = Mathf.Max(es.Length, en.Length) / 14f + 0.8f;
        return new ShowSpeechBubbleNode { textId = clave, text = es, duration = Mathf.Max(dura, paraLeer), waitForCompletion = true };
    }
    private static NarrativeNode Completar(string mision, string paso, bool cerrar = false) => new CompleteQuestStepsNode
    {
        questId = mision, stepConditionIds = new List<string> { paso }, completeQuest = cerrar,
    };
    private static NarrativeNode Desbloquear(SpellId hechizo, bool equipar) => new UnlockAbilitiesNode
    {
        spellsToUnlock = new List<SpellId> { hechizo }, assignSpellsToEmptySlot = equipar,
        forceWillBeforeSpellUnlock = true, applyPresetAfterUnlock = true, saveAfterUnlock = false,
    };
    private static NarrativeNode Dar(string rutaItem) => new GiveInventoryItemNode { item = Cargar<ItemData>(rutaItem) };

    // ── Misiones, objetos y diálogos ────────────────────────────────────────────────────────

    private static QuestData Mision(string archivo, string id, string nombreEs, string nombreEn, string descEs, string descEn,
                                    params (string condicion, string es, string en)[] pasos)
    {
        string carpeta = $"{CarpetaMisiones}/{CarpetaDeCapitulo(archivo).ToUpperInvariant()}";
        AsegurarCarpeta(carpeta);
        string ruta = $"{carpeta}/{archivo}.asset";
        var m = AssetDatabase.LoadAssetAtPath<QuestData>(ruta);
        if (m == null) { m = ScriptableObject.CreateInstance<QuestData>(); AssetDatabase.CreateAsset(m, ruta); }
        m.questId = id;
        m.displayNameId = id + "_NAME"; m.displayName = nombreEs;
        m.descriptionId = id + "_DESC"; m.description = descEs;
        m.detailedDescriptionId = id + "_DETAIL"; m.detailedDescription = descEs;
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Misiones, m.displayNameId, nombreEs, nombreEn);
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Misiones, m.descriptionId, descEs, descEn);
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Misiones, m.detailedDescriptionId, descEs, descEn);
        m.steps = new QuestData.Step[pasos.Length];
        for (int i = 0; i < pasos.Length; i++)
        {
            string clave = pasos[i].condicion + "_PASO";
            m.steps[i] = new QuestData.Step { conditionId = pasos[i].condicion, descriptionId = clave, description = pasos[i].es };
            TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Misiones, clave, pasos[i].es, pasos[i].en);
        }
        EditorUtility.SetDirty(m);
        s_misiones.Add(m);
        return m;
    }

    private static readonly List<QuestData> s_misiones = new();

    /// <summary>Carpeta del capítulo de un asset por su nombre («Q_CAP3_…», «DLG_CAP4_…» → «Cap3», «Cap4»; «DLG_ALDEA_…» → «AldeaDelMar»).</summary>
    public static string CarpetaDeCapitulo(string archivo)
    {
        var m = System.Text.RegularExpressions.Regex.Match(archivo, @"CAP(\d+)");
        if (m.Success) return "Cap" + m.Groups[1].Value;
        return archivo.Contains("_ALDEA_") ? "AldeaDelMar" : "Capitulos";
    }

    private static ItemData Objeto(string archivo, string id, string nombreEs, string nombreEn, string descEs, string descEn, string icono = null)
    {
        string ruta = $"{CarpetaItems}/{archivo}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);
        if (item == null) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, ruta); }
        string clave = "ITEM_" + id.ToUpperInvariant();
        item.itemId = id;
        item.displayNameId = clave + "_NAME"; item.displayName = nombreEs;
        item.useDescriptionId = clave + "_DESC"; item.useDescription = descEs;
        item.usableFromInventory = false;
        item.usageKind = ItemData.ItemUsageKind.Quest;
        item.buyPrice = 0; item.sellValue = 0;
        if (!string.IsNullOrEmpty(icono) && item.icon == null) item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(icono);
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, item.displayNameId, nombreEs, nombreEn);
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, item.useDescriptionId, descEs, descEn);
        EditorUtility.SetDirty(item);
        return item;
    }

    /// <summary>Una línea de diálogo: quién habla (id de personaje o vacío = Will) y su texto ES/EN.</summary>
    private readonly struct Linea
    {
        public readonly string Hablante, Es, En;
        public readonly NPCEmotion Emocion;
        public Linea(string hablante, string es, string en, NPCEmotion emocion = NPCEmotion.Neutral)
        { Hablante = hablante; Es = es; En = en; Emocion = emocion; }
    }

    private static Linea W(string es, string en, NPCEmotion e = NPCEmotion.Neutral) => new Linea("", es, en, e);
    private static Linea L(string hablante, string es, string en, NPCEmotion e = NPCEmotion.Neutral) => new Linea(hablante, es, en, e);

    /// <summary>Crea o rehace un diálogo de caja (DialogueAsset) con claves «{clave}_NN».</summary>
    private static string DialogoDeCaja(string archivo, string clave, params Linea[] lineas)
    {
        string carpeta = $"{CarpetaDialogos}/{CarpetaDeCapitulo(archivo)}";
        AsegurarCarpeta(carpeta);
        string ruta = $"{carpeta}/{archivo}.asset";
        var d = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (d == null) { d = ScriptableObject.CreateInstance<DialogueAsset>(); AssetDatabase.CreateAsset(d, ruta); }
        d.lines = new DialogueLine[lineas.Length];
        bool grupal = false;
        for (int i = 0; i < lineas.Length; i++)
        {
            string id = $"{clave}_{i + 1:00}";
            var l = lineas[i];
            bool esWill = string.IsNullOrEmpty(l.Hablante);
            d.lines[i] = new DialogueLine
            {
                speakerNameId = esWill ? "CHAR_WILL" : l.Hablante,
                textId = id,
                text = l.Es,
                isPlayerSpeaking = esWill,
                emotion = l.Emocion,
            };
            if (!esWill && i > 0 && !string.IsNullOrEmpty(lineas[i - 1].Hablante) && lineas[i - 1].Hablante != l.Hablante) grupal = true;
            TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, id, l.Es, l.En);
        }
        d.isGroupConversation = grupal;
        EditorUtility.SetDirty(d);
        return ruta;
    }

    /// <summary>Apunta los textos de una cinemática (tabla de subtítulos).</summary>
    private static void TextoDeCine(string clave, string es, string en) => TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Cinematicas, clave, es, en);

    private static void AsegurarConfiguracionDeLaDemo(StringBuilder informe)
    {
        const string ruta = "Assets/Resources/Demo/ConfiguracionDeLaDemo.asset";
        if (AssetDatabase.LoadAssetAtPath<ConfiguracionDeLaDemo>(ruta) != null) return;
        AsegurarCarpeta("Assets/Resources/Demo");
        var c = ScriptableObject.CreateInstance<ConfiguracionDeLaDemo>();
        c.esDemo = true;
        AssetDatabase.CreateAsset(c, ruta);
        informe.AppendLine("Configuración de la demo creada (es la demo: el Cap. 4 acaba en los créditos).");
    }

    // ── Utilidades ──────────────────────────────────────────────────────────────────────────

    private static T Cargar<T>(string ruta) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null) Debug.LogWarning($"[MontajeDeLosCapitulos] Falta {ruta}");
        return a;
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
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
}
