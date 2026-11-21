#if UNITY_EDITOR
using System.Collections.Generic;
using Game.NPC;
using Game.NPC.Combate;
using Game.NPC.Modules;
using Liyodev.NarrativeGraph;
using UnityEditor;
using UnityEngine;

/// Arreglo de una sola vez (INC-1332): monta el entrenamiento de Erika del Cap. 2 como lo diseñó
/// Raúl. Crea los perfiles de combate de Erika (lecciones y duelo en dos fases) y sus dos charlas,
/// pone su configuración al cerebro por puntuación y rehace el tramo del grafo: hablar con ella →
/// charla → Protección → tres lecciones sin daño (protegerse, devolver, golpear) → elementos y
/// grimorio → Bola Prisma → duelo. Se puede repetir (lo que ya está, lo deja). Se ejecuta una vez
/// y este archivo se retira a «Versiones antiguas/».
public static class MontarCombateDeErika
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap2.asset";
    private const string RutaConfig = "Assets/_NPCs/Combat/NPC_Combat_Config_Erika.asset";
    private const string CarpetaPerfiles = "Assets/ElSendero/Data/Combate";
    private const string RutaPerfilDuelo = CarpetaPerfiles + "/PerfilDeCombate_Erika.asset";
    private const string RutaPerfilLecciones = CarpetaPerfiles + "/PerfilDeCombate_Erika_Lecciones.asset";
    private const string CarpetaCharlas = "Assets/_DIALOGUES/DIALOGUE NPCS/Erika";
    private const string RutaCharlaLecciones = CarpetaCharlas + "/DLG_ERIKA_LECCIONES.asset";
    private const string RutaCharlaElementos = CarpetaCharlas + "/DLG_ERIKA_ELEMENTOS.asset";

    private const string Erika = "NPC_Erika";
    private const string Hablar = "cap2-prepara-viaje-erika-0";
    private const string Grimorio = "cap2-prepara-viaje-erika-grimorio";
    private const string Desbloqueo = "cap2-prepara-viaje-erika-desbloqueo";
    private const string EsperaViejo = "cap2-prepara-viaje-erika-combate";
    private const string Completar = "cap2-prepara-viaje-erika-2";
    private const string Intro = "cap2-erika-lecciones-intro";
    private const string Proteccion = "cap2-erika-desbloqueo-proteccion";
    private const string L1 = "cap2-erika-leccion-protegerse";
    private const string L2 = "cap2-erika-leccion-devolver";
    private const string L3 = "cap2-erika-leccion-golpear";
    private const string Elementos = "cap2-erika-elementos";
    private const string Duelo = "cap2-erika-duelo";

    [MenuItem("El Sendero/Pruebas/Arreglo de una vez: entrenamiento de Erika (lecciones y duelo en dos fases)")]
    public static void Ejecutar()
    {
        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(RutaConfig);
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (config == null || g == null) { Debug.LogError($"[INC-1332] Falta {RutaConfig} o {RutaGrafo}"); return; }

        var lecciones = PerfilDeLecciones(config);
        var duelo = PerfilDelDuelo(config);
        var charlaLecciones = Charla(RutaCharlaLecciones, new[]
        {
            Linea("CHAR_ERIKA", "DLG_ERIKA_LECCIONES_01", "¿Así que te vas del pueblo, Will? Pues por esas puertas no sales sin saber defenderte.", false, NPCEmotion.Smirk),
            Linea("CHAR_ERIKA", "DLG_ERIKA_LECCIONES_02", "Te voy a lanzar unas bolitas flojas. Sin daño, tranquilo. Tú haz lo que te diga.", false, NPCEmotion.Happy),
            Linea("CHAR_WILL", "DLG_ERIKA_LECCIONES_03", "Vale. Enséñame.", true, NPCEmotion.Neutral),
        });
        var charlaElementos = Charla(RutaCharlaElementos, new[]
        {
            Linea("CHAR_ERIKA", "DLG_ERIKA_ELEMENTOS_01", "Una cosa más. En este mundo la magia tiene elementos: Fuego, Viento, Luz, Mente y Oscuro.", false, NPCEmotion.Neutral),
            Linea("CHAR_ERIKA", "DLG_ERIKA_ELEMENTOS_02", "Contra la gente da igual el elemento: lo que cuenta es la fuerza del hechizo. Pero si le devuelves el suyo, le pasa algo: el de luz aturde, el de mente duerme…", false, NPCEmotion.Thinking),
            Linea("CHAR_ERIKA", "DLG_ERIKA_ELEMENTOS_03", "Con las criaturas del camino es otra historia. Ya lo irás viendo.", false, NPCEmotion.Smirk),
            Linea("CHAR_ERIKA", "DLG_ERIKA_ELEMENTOS_04", "Y esto es para ti: Eldran me pidió que te lo diera. Tu grimorio. Ahí viene todo explicado.", false, NPCEmotion.Happy),
            Linea("CHAR_ERIKA", "DLG_ERIKA_ELEMENTOS_05", "Mira: se acaba de despertar una página nueva, la Bola Prisma. Pruébala conmigo.", false, NPCEmotion.Neutral),
            Linea("CHAR_WILL", "DLG_ERIKA_ELEMENTOS_06", "¡Mi propio grimorio! Vale, Erika. Cuando quieras.", true, NPCEmotion.Happy),
            Linea("CHAR_ERIKA", "DLG_ERIKA_ELEMENTOS_07", "¡En guardia, entonces! Ahora sí, de verdad.", false, NPCEmotion.Smirk),
        });

        // Configuración de Erika: cerebro nuevo con el perfil del duelo; sin alerta propia (el
        // combate lo pide el grafo) y con más vida para que haya dos fases.
        config.cerebro = TipoDeCerebroDeCombate.PorPuntuacion;
        config.perfilDeCombate = duelo;
        config.health = 160f;
        config.isAggressive = false;
        config.dialogueOnAlert = null;
        EditorUtility.SetDirty(config);

        MontarGrafo(g, config, lecciones, charlaLecciones, charlaElementos);

        AssetDatabase.SaveAssets();
        Debug.Log("[INC-1332] Entrenamiento de Erika montado: perfiles, charlas, configuración y grafo del Cap. 2.");
    }

    // ── Grafo ─────────────────────────────────────────────────────────────────────────────────

    private static void MontarGrafo(NarrativeGraph g, NPCCombatConfig config, PerfilDeCombate lecciones,
        DialogueAsset charlaLecciones, DialogueAsset charlaElementos)
    {
        if (g.FindNode(Duelo) != null) { Debug.Log("[INC-1332] El grafo ya estaba montado."); return; }
        var hablar = g.FindNode(Hablar);
        var grimorio = g.FindNode(Grimorio);
        var desbloqueo = g.FindNode(Desbloqueo) as GameStepNode;
        if (hablar == null || grimorio == null || desbloqueo == null || g.FindNode(Completar) == null)
        {
            Debug.LogError("[INC-1332] No están los pasos de Erika del Cap. 2: no se toca el grafo.");
            return;
        }

        Vector2 o = hablar.Position + new Vector2(0f, 320f);
        const float paso = 280f;

        var intro = new PlayDialogueNode { Dialogue = charlaLecciones, NpcId = Erika };
        Preparar(intro, Intro, "22b-a.- Erika: «no sales sin saber defenderte»", hablar, o);

        // La Protección se aprende ya; la Bola Prisma, con el grimorio.
        var viejo = desbloqueo.Request as PedirDesbloqueo;
        var proteccion = new PedirDesbloqueo
        {
            abilityKeysToUnlock = viejo != null ? new List<AbilityKey>(viejo.abilityKeysToUnlock) : new List<AbilityKey>(),
            spellsToUnlock = new List<SpellId>(),
            assignSpellsToEmptySlot = true,
            forceWillBeforeSpellUnlock = true,
            applyPresetAfterUnlock = true,
        };
        if (viejo != null) viejo.abilityKeysToUnlock.Clear();
        desbloqueo.DisplayTitle = "22b-1.- Aprender Bola Prisma";
        var nodoProteccion = GameStepNode.For(proteccion);
        Preparar(nodoProteccion, Proteccion, "22b-b.- Aprender Protección", hablar, o + new Vector2(paso, 0f));

        var l1 = GameStepNode.For(Leccion(config, lecciones, PedirLeccionDeCombate.Objetivo.Protegerse, 2,
            "ERIKA_LECCION_PROTEGERSE_INICIO", "Cuando te lance algo, mantén la Protección y aguanta.",
            "ERIKA_LECCION_PROTEGERSE_FIN", "¡Bien aguantado!"));
        Preparar(l1, L1, "22b-c.- Lección 1: protegerse", hablar, o + new Vector2(paso * 2f, 0f));

        var l2 = GameStepNode.For(Leccion(config, lecciones, PedirLeccionDeCombate.Objetivo.Devolver, 1,
            "ERIKA_LECCION_DEVOLVER_INICIO", "Ahora alza la Protección justo cuando te llegue: se la devuelves.",
            "ERIKA_LECCION_DEVOLVER_FIN", "¡Uf! Me ha dejado mareada. Eso es devolver."));
        Preparar(l2, L2, "22b-d.- Lección 2: devolver", hablar, o + new Vector2(paso * 3f, 0f));

        var l3 = GameStepNode.For(Leccion(config, lecciones, PedirLeccionDeCombate.Objetivo.Golpear, 2,
            "ERIKA_LECCION_GOLPEAR_INICIO", "Y ahora pégame tú. Sin miedo, que no me haces nada.",
            "ERIKA_LECCION_GOLPEAR_FIN", "¡Vale, vale! Ya veo que sabes."));
        Preparar(l3, L3, "22b-e.- Lección 3: golpear", hablar, o + new Vector2(paso * 4f, 0f));

        var elementos = new PlayDialogueNode { Dialogue = charlaElementos, NpcId = Erika };
        Preparar(elementos, Elementos, "22b-f.- Erika: los elementos y el grimorio", hablar, o + new Vector2(paso * 5f, 0f));

        var duelo = GameStepNode.For(new PedirCombateContraNpc
        {
            npcId = Erika,
            combatConfig = null,
            defeatEventKey = "ERIKA_ENTRENAMIENTO_DERROTADA",
            waitForDefeat = true,
            restorePeacefulOnDefeat = true,
        });
        Preparar(duelo, Duelo, "23b.- Duelo con Erika (dos fases)", hablar, desbloqueo.Position + new Vector2(paso, 0f));

        // Encadenar.
        string trasHablar = hablar.Outputs != null && hablar.Outputs.Count > 0 ? hablar.Outputs[0] : Grimorio;
        hablar.Outputs = new List<string> { Intro };
        intro.Outputs.Add(Proteccion);
        nodoProteccion.Outputs.Add(L1);
        l1.Outputs.Add(L2);
        l2.Outputs.Add(L3);
        l3.Outputs.Add(Elementos);
        elementos.Outputs.Add(trasHablar == Grimorio ? Grimorio : trasHablar);
        desbloqueo.Outputs = new List<string> { Duelo };
        // «Gana» sigue al encargo hecho; «Pierde» repite el duelo sin pantalla de derrota.
        duelo.Outputs.Add(Completar);
        duelo.Outputs.Add(Duelo);

        // Fuera la espera del evento de derrota: la hace ya el propio paso del duelo.
        var espera = g.FindNode(EsperaViejo);
        if (espera != null)
        {
            foreach (var n in g.nodes)
            {
                if (n?.Outputs == null) continue;
                for (int i = 0; i < n.Outputs.Count; i++)
                    if (n.Outputs[i] == EsperaViejo) n.Outputs[i] = Duelo;
            }
            g.nodes.Remove(espera);
        }

        g.nodes.Add(intro);
        g.nodes.Add(nodoProteccion);
        g.nodes.Add(l1);
        g.nodes.Add(l2);
        g.nodes.Add(l3);
        g.nodes.Add(elementos);
        g.nodes.Add(duelo);
        EditorUtility.SetDirty(g);
    }

    private static void Preparar(NarrativeNode n, string guid, string titulo, NarrativeNode modelo, Vector2 donde)
    {
        n.Guid = guid;
        n.DisplayTitle = titulo;
        n.Chapter = modelo.Chapter;
        n.Section = modelo.Section;
        n.Position = donde;
        n.Outputs = new List<string>();
    }

    private static PedirLeccionDeCombate Leccion(NPCCombatConfig config, PerfilDeCombate perfil,
        PedirLeccionDeCombate.Objetivo objetivo, int veces, string claveInicio, string inicio, string claveFin, string fin)
        => new PedirLeccionDeCombate
        {
            npcId = Erika,
            combatConfig = config,
            perfil = perfil,
            objetivo = objetivo,
            veces = veces,
            claveAlEmpezar = claveInicio,
            fraseAlEmpezar = inicio,
            claveAlAcertar = "ERIKA_LECCION_BIEN",
            fraseAlAcertar = "¡Eso es!",
            claveAlAcabar = claveFin,
            fraseAlAcabar = fin,
            segundosAlAcabar = 1.8f,
        };

    // ── Perfiles ──────────────────────────────────────────────────────────────────────────────

    // Lecciones: una bolita lenta, con mucho aviso y mucha ventana; ni esquiva, ni se protege, ni se
    // esconde, y lanza aunque el jugador tenga el escudo arriba o se la devuelva.
    private static PerfilDeCombate PerfilDeLecciones(NPCCombatConfig config)
    {
        var p = CargarOCrear(RutaPerfilLecciones);
        p.acciones = new List<AccionDeCombate>
        {
            Accion("Bolita de práctica", config.spell1Prefab, "MagicLeft", 1.0f, 1.0f, 3.5f, 0f, 0f, 1f, FuncionDeAccion.Presion),
        };
        p.defensas = new DefensasDeCombate { esquivar = false, escudo = false, cubrirseParaRecargar = false, leerLaRepeticion = 0f };
        p.caracter = new CaracterDeCombate
        {
            agresividad = 0.6f, cautela = 0.1f, movilidad = 0.25f, paciencia = 0.3f,
            distanciaPreferida = 6f, distanciaMinima = 3f, distanciaMaxima = 10f,
            velocidadAndando = 2f, velocidadCorriendo = 4f, atacantesALaVez = 1,
            leeLaDefensaDelJugador = false,
        };
        p.dificultad = 0.2f;
        p.fases = new List<FaseDeCombate>();
        EditorUtility.SetDirty(p);
        return p;
    }

    // Duelo: fase 1 de tanteo (dos ataques, se protege y esquiva, lee el hechizo repetido); a la
    // mitad de la vida cambia: más rápida y con un remate con mucho aviso y mucha ventana.
    private static PerfilDeCombate PerfilDelDuelo(NPCCombatConfig config)
    {
        var p = CargarOCrear(RutaPerfilDuelo);
        p.acciones = new List<AccionDeCombate>
        {
            Accion("Chispa", config.spell1Prefab, "MagicLeft", 0.6f, 0.6f, 2.5f, 15f, 8f, 1f, FuncionDeAccion.Presion),
            Accion("Golpe", config.spell2Prefab, "MagicRight", 0.8f, 0.8f, 5f, 25f, 15f, 0.8f, FuncionDeAccion.Golpe),
        };
        p.defensas = new DefensasDeCombate
        {
            esquivar = true, distanciaDeEsquiva = 3f, recuperacionDeEsquiva = 1.4f,
            escudo = true, duracionDelEscudo = 1.5f, enfriamientoDelEscudo = 7f,
            cubrirseParaRecargar = false, radioDeCobertura = 14f, recargaACubierto = 1.5f,
            leerLaRepeticion = 0.8f, repeticionesParaLeerla = 3, segundosDeRepeticion = 12f,
        };
        p.caracter = new CaracterDeCombate
        {
            agresividad = 0.5f, cautela = 0.5f, movilidad = 0.5f, paciencia = 0.6f,
            distanciaPreferida = 7f, distanciaMinima = 3f, distanciaMaxima = 12f,
            velocidadAndando = 2.5f, velocidadCorriendo = 5.5f, atacantesALaVez = 1,
        };
        p.dificultad = 0.4f;
        p.fases = new List<FaseDeCombate>
        {
            new FaseDeCombate
            {
                nombre = "En serio",
                desdeVida = 0.5f,
                acciones = new List<AccionDeCombate>
                {
                    Accion("Chispa", config.spell1Prefab, "MagicLeft", 0.5f, 0.5f, 2f, 15f, 10f, 1f, FuncionDeAccion.Presion),
                    Accion("Golpe", config.spell2Prefab, "MagicRight", 0.7f, 0.7f, 4f, 25f, 18f, 0.8f, FuncionDeAccion.Golpe),
                    Accion("Remate", config.spell3Prefab, "MagicSpecial", 1.1f, 1.3f, 7f, 35f, 25f, 0.7f, FuncionDeAccion.Remate),
                },
                cambiaCaracter = true,
                caracter = new CaracterDeCombate
                {
                    agresividad = 0.7f, cautela = 0.45f, movilidad = 0.7f, paciencia = 0.45f,
                    distanciaPreferida = 7f, distanciaMinima = 3f, distanciaMaxima = 12f,
                    velocidadAndando = 2.8f, velocidadCorriendo = 6f, atacantesALaVez = 1,
                },
                cambiaDefensas = true,
                defensas = new DefensasDeCombate
                {
                    esquivar = true, distanciaDeEsquiva = 3.2f, recuperacionDeEsquiva = 1.1f,
                    escudo = true, duracionDelEscudo = 1.5f, enfriamientoDelEscudo = 5f,
                    cubrirseParaRecargar = false, radioDeCobertura = 14f, recargaACubierto = 1.5f,
                    leerLaRepeticion = 1f, repeticionesParaLeerla = 3, segundosDeRepeticion = 12f,
                },
                dificultad = 0.55f,
                segundosDeTransicion = 2f,
                animacion = "",
                claveDeFrase = "ERIKA_DUELO_FASE2",
                frase = "¡Vale, se acabó el calentamiento!",
                sacudida = true,
                recuperaVidaHasta = 0f,
            },
        };
        EditorUtility.SetDirty(p);
        return p;
    }

    private static AccionDeCombate Accion(string nombre, GameObject proyectil, string animacion, float aviso,
        float recuperacion, float enfriamiento, float coste, float dano, float peso, FuncionDeAccion funcion)
        => new AccionDeCombate
        {
            nombre = nombre, proyectil = proyectil, animacion = animacion,
            distanciaMinima = 0f, distanciaMaxima = 14f,
            segundosDeAviso = aviso, segundosDeRecuperacion = recuperacion, enfriamiento = enfriamiento,
            costeDeMana = coste, dano = dano, velocidad = 0f, peso = peso, funcion = funcion,
            sePuedeDevolver = true,
        };

    private static PerfilDeCombate CargarOCrear(string ruta)
    {
        var p = AssetDatabase.LoadAssetAtPath<PerfilDeCombate>(ruta);
        if (p != null) return p;
        AsegurarCarpeta(CarpetaPerfiles);
        p = ScriptableObject.CreateInstance<PerfilDeCombate>();
        AssetDatabase.CreateAsset(p, ruta);
        return p;
    }

    // ── Charlas ───────────────────────────────────────────────────────────────────────────────

    private static DialogueAsset Charla(string ruta, DialogueLine[] lineas)
    {
        var d = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(d, ruta);
        }
        d.lines = lineas;
        d.maxLinesPerPage = 3;
        d.isGroupConversation = false;
        EditorUtility.SetDirty(d);
        return d;
    }

    private static DialogueLine Linea(string hablante, string clave, string texto, bool jugador, NPCEmotion emocion)
        => new DialogueLine { speakerNameId = hablante, textId = clave, text = texto, isPlayerSpeaking = jugador, emotion = emocion };

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int barra = ruta.LastIndexOf('/');
        string padre = ruta.Substring(0, barra);
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, ruta.Substring(barra + 1));
    }
}
#endif
