#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.InputGlyphs;
using UnityEditor;
using UnityEngine;

/// Extras del Cap. 2 en los grafos (INC-725): desbloqueos de hechizos, decisión de los
/// contrabandistas y título del desbloqueo del Cap. 1. Idempotente: cada nodo tiene un id
/// estable y volver a ejecutarlo reemplaza los nodos por los mismos datos.
///
/// Los bloques 2.3d (puntería con Eldran), 2.7 (contrabandistas), 2.9 (casa entre raíces) y
/// 2.10 (campana) aún no están montados: sus nodos quedan como anclas sin entrada. La tanda
/// que monte cada bloque conecta su último nodo con el ancla (ids en las constantes de abajo)
/// y conserva sus salidas.
public static class Cap2Extras
{
    const string GrafoCap1 = "Assets/NarrativeGraph/Cap1.asset";
    const string GrafoCap2 = "Assets/NarrativeGraph/Cap2.asset";
    const string CarpetaItems = "Assets/_ITEMS";
    const string Informe = "Guiones/MainWorld_Cap2Extras/grafo-guardado.md";

    // Nodo del Cap. 1 que desbloquea la magia (spellsToUnlock = Llama Astral).
    const string NodoDesbloqueoCap1 = "e7e8e2f2-6e54-4490-97b1-fd48323945a4";
    const string TituloDesbloqueoCap1 = "30.- SE DESBLOQUEA EL ATAQUE MÁGICO Y LLAMA ASTRAL";

    // Anclas que conectan las tandas de cada bloque.
    public const string AnclaBolaPrisma = "cap2-desbloqueo-bola-prisma";
    public const string AnclaCupula = "cap2-desbloqueo-cupula-estelar";
    public const string AnclaLevitacion = "cap2-desbloqueo-levitacion";
    public const string AnclaContrabandistas = "cap2-contrabandistas-0";
    public const string SalidaContrabandistas = "cap2-contrabandistas-fin";

    // Contrato con la escena (prefab Campamento de contrabandistas).
    public const string SenalAtaque = "CAP2_CONTRABANDISTAS_ATAQUE";
    public const string SenalVencidos = "CAP2_CONTRABANDISTAS_VENCIDOS";
    public const string SenalJaulaQuemada = "CAP2_CONTRABANDISTAS_JAULA_QUEMADA";
    public const string FlagCombate = "CAP2_CONTRABANDISTAS_COMBATE";
    public const string FlagSigilo = "CAP2_CONTRABANDISTAS_SIGILO";
    public const string FlagJaulasQuemadas = "CAP2_JAULAS_QUEMADAS";

    [MenuItem("El Sendero/Capítulo 2/Extras INC-725: montar en los grafos")]
    public static void Montar()
    {
        if (Application.isPlaying) throw new InvalidOperationException("El montaje necesita el Editor fuera de Play.");
        AsegurarObjetos();
        var pluma = Cargar<ItemData>(CarpetaItems + "/IT_PlumaBrillante.asset");

        var cap1 = Cargar<NarrativeGraph>(GrafoCap1);
        var desbloqueo = cap1.FindNode(NodoDesbloqueoCap1) as UnlockAbilitiesNode
            ?? throw new InvalidOperationException("Cap1: falta el nodo de desbloqueo de la magia.");
        if (!desbloqueo.spellsToUnlock.Contains(SpellId.Fireball))
            throw new InvalidOperationException("Cap1: el nodo de desbloqueo ya no da Llama Astral; revisar antes de renombrarlo.");
        desbloqueo.displayTitle = TituloDesbloqueoCap1;
        EditorUtility.SetDirty(cap1);

        var g = Cargar<NarrativeGraph>(GrafoCap2);

        // 2.3d · Eldran: al acabar los cubos y las tres piedras.
        Poner(g, AnclaBolaPrisma, "2.3d · Aprende Bola Prisma (tras los cubos de Eldran)", new Vector2(8400, 900),
            Desbloqueo(SpellId.Plasmaball, equipar: true), Salidas(g, AnclaBolaPrisma));

        // 2.9 · Casa entre raíces: primera receta del combo mágico.
        Poner(g, AnclaCupula, "2.9 · Primera receta: Cúpula Estelar", new Vector2(8400, 2700),
            Desbloqueo(SpellId.CupulaEstelar, equipar: false), new List<string> { "cap2-tutorial-cupula-estelar" });
        Poner(g, "cap2-tutorial-cupula-estelar", "2.9 · Tutorial · combo mágico (Y → X → B → A)", new Vector2(8780, 2700),
            new TutorialPromptNode
            {
                textId = "TUTORIAL_CAP2_CUPULA_ESTELAR",
                text = "Pulsa {BOTON} y teclea X, B, A: Cúpula Estelar protege a quien esté debajo.",
                buttonName = InputGlyphNames.North, dismissWithCancel = true, cerrarAlEmpezarDialogo = true
            }, Salidas(g, "cap2-tutorial-cupula-estelar"));

        // 2.10 · Campana, ronda 4: Will sostiene la figura que resbala.
        Poner(g, AnclaLevitacion, "2.10 · Will sostiene la figura: aprende Levitación", new Vector2(8400, 3300),
            Desbloqueo(SpellId.Levitation, equipar: true), Salidas(g, AnclaLevitacion));

        MontarContrabandistas(g, pluma);

        EditorUtility.SetDirty(g);
        AssetDatabase.SaveAssets();
        Verificar();
    }

    /// 2.7 · Decisión jugada: coger la pluma (sigilo) o entrar en el claro (combate). Gana lo
    /// primero que ocurra; la otra rama se corta. Una tercera rama anota en la partida si arde
    /// alguna jaula (en la pelea o cuando sea): la cinemática de los pájaros y lo que venga
    /// después leen ese flag.
    static void MontarContrabandistas(NarrativeGraph g, ItemData pluma)
    {
        const string P = "cap2-contrabandistas-";
        const float X = 8400, Y = 1500;
        Poner(g, AnclaContrabandistas, "2.7 · Contrabandistas: ¿cómo actúa Will?", new Vector2(X, Y),
            new ForkNode(), new List<string> { P + "ataque-0", P + "pluma-0", P + "jaula-0" });

        Poner(g, P + "ataque-0", "2.7 · Will entra en el claro", new Vector2(X + 380, Y - 250),
            new WaitCustomEventNode { eventKey = SenalAtaque }, new List<string> { P + "ataque-1" });
        Poner(g, P + "ataque-1", "2.7 · ¿Ya había elegido el sigilo?", new Vector2(X + 760, Y - 250),
            new BranchFlagNode { flagKey = FlagSigilo }, new List<string> { "", P + "ataque-2" });
        Poner(g, P + "ataque-2", "2.7 · Elige pelear", new Vector2(X + 1140, Y - 250),
            new SetFlagNode { flagKey = FlagCombate }, new List<string> { P + "ataque-3" });
        Poner(g, P + "ataque-3", "2.7 · Contrabandistas vencidos", new Vector2(X + 1520, Y - 250),
            new WaitCustomEventNode { eventKey = SenalVencidos }, new List<string> { SalidaContrabandistas });

        Poner(g, P + "pluma-0", "2.7 · Coge la pluma brillante", new Vector2(X + 380, Y + 250),
            new WaitForItemAddedNode { itemToWaitFor = pluma, completeIfAlreadyOwned = true }, new List<string> { P + "pluma-1" });
        Poner(g, P + "pluma-1", "2.7 · ¿Ya había empezado a pelear?", new Vector2(X + 760, Y + 250),
            new BranchFlagNode { flagKey = FlagCombate }, new List<string> { "", P + "pluma-2" });
        // El aviso al puesto de guardia (sargento y guardias que siguen) se inserta aquí.
        Poner(g, P + "pluma-2", "2.7 · Elige avisar a la guardia", new Vector2(X + 1140, Y + 250),
            new SetFlagNode { flagKey = FlagSigilo }, new List<string> { SalidaContrabandistas });

        Poner(g, P + "jaula-0", "2.7 · Arde una jaula", new Vector2(X + 380, Y + 650),
            new WaitCustomEventNode { eventKey = SenalJaulaQuemada }, new List<string> { P + "jaula-1" });
        Poner(g, P + "jaula-1", "2.7 · Consecuencia: jaulas quemadas", new Vector2(X + 760, Y + 650),
            new SetFlagNode { flagKey = FlagJaulasQuemadas }, new List<string>());

        // La cinemática de los pájaros (con más o menos chispas según el flag) va después.
        Poner(g, SalidaContrabandistas, "2.7 · Cap. 2 · Tras los contrabandistas", new Vector2(X + 1900, Y),
            new CheckpointNode { checkpointId = "CAP2_TRAS_CONTRABANDISTAS" }, Salidas(g, SalidaContrabandistas));
    }

    static UnlockAbilitiesNode Desbloqueo(SpellId hechizo, bool equipar) => new UnlockAbilitiesNode
    {
        spellsToUnlock = new List<SpellId> { hechizo },
        assignSpellsToEmptySlot = equipar,
        forceWillBeforeSpellUnlock = true,
        applyPresetAfterUnlock = true,
        saveAfterUnlock = false
    };

    /// Conserva las salidas que ya tenga el nodo (las pone la tanda que monta el bloque).
    static List<string> Salidas(NarrativeGraph g, string id) => g.FindNode(id)?.outputs ?? new List<string>();

    static void Poner(NarrativeGraph g, string id, string titulo, Vector2 posicion, NarrativeNode n, List<string> salidas)
    {
        n.guid = id; n.displayTitle = titulo; n.chapter = "Cap. 2"; n.position = posicion;
        n.outputs = new List<string>(salidas);
        var anterior = g.FindNode(id);
        if (anterior == null) g.nodes.Add(n); else g.nodes[g.nodes.IndexOf(anterior)] = n;
    }

    /// Objetos del bolsillo de los extras: la pluma (contrabandistas) y la cuerda y los clavos
    /// (puente sin magia). El ItemRegistry se sincroniza solo al importarlos.
    public static void AsegurarObjetos()
    {
        Objeto("IT_PlumaBrillante", "pluma_brillante", "ITEM_PLUMA_BRILLANTE", "Pluma brillante",
            "Se quedó enganchada en una zarza. Todavía guarda un poco de luz blanca.");
        Objeto("IT_Cuerda", "cuerda", "ITEM_CUERDA", "Cuerda", "Una cuerda fuerte. Sirve para atar una tabla suelta.");
        Objeto("IT_Clavos", "clavos", "ITEM_CLAVOS", "Clavos", "Dos clavos algo torcidos. Todavía sujetan.");
        AssetDatabase.SaveAssets();
    }

    static void Objeto(string archivo, string id, string clave, string nombre, string descripcion)
    {
        string ruta = CarpetaItems + "/" + archivo + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);
        if (item == null) { item = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(item, ruta); }
        item.itemId = id;
        item.displayNameId = clave + "_NAME"; item.displayName = nombre;
        item.useDescriptionId = clave + "_DESC"; item.useDescription = descripcion;
        item.usableFromInventory = false;
        item.usageKind = ItemData.ItemUsageKind.Quest;
        item.buyPrice = 0; item.sellValue = 0;
        EditorUtility.SetDirty(item);
    }

    [MenuItem("El Sendero/Capítulo 2/Extras INC-725: verificar")]
    public static void Verificar()
    {
        var errores = new List<string>();
        var lineas = new List<string>();
        var cap1 = Cargar<NarrativeGraph>(GrafoCap1);
        if (cap1.FindNode(NodoDesbloqueoCap1)?.displayTitle != TituloDesbloqueoCap1)
            errores.Add("Cap1: el título del desbloqueo de la magia no está corregido.");

        var g = Cargar<NarrativeGraph>(GrafoCap2);
        var esperados = new (string id, SpellId hechizo)[]
        { (AnclaBolaPrisma, SpellId.Plasmaball), (AnclaCupula, SpellId.CupulaEstelar), (AnclaLevitacion, SpellId.Levitation) };
        foreach (var (id, hechizo) in esperados)
            if (!(g.FindNode(id) is UnlockAbilitiesNode u) || !u.spellsToUnlock.SequenceEqual(new[] { hechizo }))
                errores.Add("Cap2: falta el desbloqueo " + hechizo + " en " + id + ".");

        var propios = g.nodes.Where(n => n.guid.StartsWith("cap2-desbloqueo-") || n.guid.StartsWith("cap2-contrabandistas-")
            || n.guid == "cap2-tutorial-cupula-estelar").ToArray();
        if (propios.Length != 15) errores.Add("Cap2: se esperaban 15 nodos de los extras y hay " + propios.Length + ".");
        if (g.nodes.Select(n => n.guid).Distinct().Count() != g.nodes.Count) errores.Add("Cap2: hay guids duplicados.");
        foreach (var n in propios)
        {
            foreach (var salida in n.outputs)
                if (!string.IsNullOrEmpty(salida) && g.FindNode(salida) == null) errores.Add("Arista sin destino: " + n.guid + " → " + salida);
            int entradas = g.nodes.Count(o => o.outputs.Contains(n.guid));
            lineas.Add(n.displayTitle + " · " + n.GetType().Name + " · " + n.guid + " · entradas " + entradas + " → " + string.Join(", ", n.outputs.Select(s => s == "" ? "(fin)" : s)));
        }
        foreach (var i in new[] { "IT_PlumaBrillante", "IT_Cuerda", "IT_Clavos" })
            if (AssetDatabase.LoadAssetAtPath<ItemData>(CarpetaItems + "/" + i + ".asset") == null) errores.Add("Falta " + i + ".");

        Directory.CreateDirectory(Path.GetDirectoryName(Informe));
        File.WriteAllLines(Informe, lineas.Concat(errores.Select(e => "ERROR · " + e)));
        if (errores.Count > 0) throw new InvalidOperationException("[Cap2Extras] " + string.Join("\n", errores));
        Debug.Log("[Cap2Extras] Grafos verificados (" + propios.Length + " nodos). Informe en " + Informe);
    }

    static T Cargar<T>(string ruta) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("Falta " + ruta);
}
#endif
