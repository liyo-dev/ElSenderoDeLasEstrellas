using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone los sets de magia de 4 y 4 decididos por Raúl el 20 nov 2026 (pendientes de combate en
/// grupo, D): cada personaje con 4 rápidos (X) y 4 combos (Y) de sus elementos, más un especial
/// con misión aparte (Will: Amanecer).
/// <list type="bullet">
/// <item>Will (Luz): Llama Astral, Bola Prisma, Estrella Fugaz, Lluvia de Chispas · Corazón
/// Estelar, Cúpula Estelar, Meteoro, Nova de Luz · Amanecer. La Llama Astral pasa a Luz.</item>
/// <item>Estela (Fuego y Viento): Bola de Fuego, Chispa Ígnea, Ráfaga, Ciclón · Brisa Sanadora,
/// Muro de Fuego, Remolino, Tormenta de Fuego.</item>
/// <item>Liam (Mente y Oscuro): Levitación, Empujar (nuevo), Eco, Garra del Pacto · Velo Mental,
/// Paso Sombrío, Cadenas del Pacto, Juicio del Pacto.</item>
/// <item>Salen del grimorio (se quedan como hechizos de enemigo, enGrimorio = false): Huracán,
/// Aura Estelar, Dardo Mental y Sello del Pacto. La Levitación pasa de Will a Liam.</item>
/// <item>La lección del escudo de Erika (Cap. 2) pasa a un escudo de Luz.</item>
/// </list>
/// Se puede repetir. Menú de un solo uso: tras ejecutarlo, su archivo va a «Versiones antiguas».
/// </summary>
public static class SetsDeMagia4y4
{
    private const string Carpeta = "Assets/_SPELLS";
    private const string Biblioteca = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";
    private const string Grafo = "Assets/NarrativeGraph/Cap2.asset";

    [MenuItem("El Sendero/Magia/Sets de magia de 4 y 4 (20 nov)")]
    public static void Aplicar()
    {
        var log = new StringBuilder();
        MagicSpellSO H(string nombre)
        {
            var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"{Carpeta}/{nombre}.asset");
            if (s == null) log.Append($"✗ falta {nombre} · ");
            return s;
        }

        // Elementos, dueños y quién sale del grimorio.
        var llama = H("LlamaAstral");
        if (llama != null) { llama.element = MagicElement.Light; EditorUtility.SetDirty(llama); }
        var levitacion = H("Levitation");
        if (levitacion != null) { levitacion.caster = PartyControlManager.CharacterSlot.Liam; levitacion.enGrimorio = true; EditorUtility.SetDirty(levitacion); }
        foreach (var fuera in new[] { "Huracan", "AuraEstelar", "DardoMental", "SelloDelPacto" })
        {
            var s = H(fuera);
            if (s == null) continue;
            s.enGrimorio = false;
            EditorUtility.SetDirty(s);
        }
        var amanecer = H("Amanecer");
        if (amanecer != null) { amanecer.especialConMision = true; EditorUtility.SetDirty(amanecer); }

        var empujar = CrearEmpujar(log);
        AnadirALaBiblioteca(empujar, log);

        // Las fichas: los 4 y 4 de cada uno, en el orden en que rotan con LB.
        PonerFicha(PartyControlManager.CharacterSlot.Will,
            new[] { llama, H("BolaPrisma"), H("EstrellaFugaz"), H("LluviaDeChispas") },
            new[] { H("CorazonEstelar"), H("CupulaEstelar"), H("Meteoro"), H("NovaDeLuz") }, null, log);
        PonerFicha(PartyControlManager.CharacterSlot.Estela,
            new[] { H("BolaFuego"), H("ChispaIgnea"), H("Rafaga"), H("Tornado") },
            new[] { H("BrisaSanadora"), H("MuroDeFuego"), H("Remolino"), H("TormentaDeFuego") }, null, log);
        var juicio = H("JuicioDelPacto");
        PonerFicha(PartyControlManager.CharacterSlot.Liam,
            new[] { levitacion, empujar, H("Eco"), H("GarraDelPacto") },
            new[] { H("Invisibilidad"), H("PasoSombrio"), H("CadenasDelPacto"), juicio }, juicio, log);

        LeccionDeErikaDeLuz(log);

        AssetDatabase.SaveAssets();
        string texto = "Sets de 4 y 4: " + log;
        Debug.Log("[Magia] " + texto);
        OrdenesDelEditor.Avisar("Magia", texto);
    }

    private static MagicSpellSO CrearEmpujar(StringBuilder log)
    {
        string ruta = $"{Carpeta}/Empujar.asset";
        var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(ruta);
        if (s == null)
        {
            // Parte del Aura Estelar (bola lavanda, Mente), que sale de Liam y deja libre su aspecto.
            if (!AssetDatabase.CopyAsset($"{Carpeta}/AuraEstelar.asset", ruta)) { log.Append("✗ no he podido crear Empujar · "); return null; }
            s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(ruta);
        }
        s.spellId = SpellId.Empujar;
        s.displayNameId = "SPELL_EMPUJAR_NAME";
        s.displayName = "Empujar";
        s.lore = "Liam aparta lo que tiene delante con la mente. Si flota, sale volando.";
        s.loreId = "";
        s.kind = MagicKind.Projectile;
        s.element = MagicElement.Mind;
        s.caster = PartyControlManager.CharacterSlot.Liam;
        s.enGrimorio = true;
        s.especialConMision = false;
        s.slotType = SpellSlotType.Any;
        s.damage = 10f;
        s.manaCost = 8f;
        s.initialSpeed = 16f;
        s.lifeTime = 1.5f;
        s.statusEffect = EstadoDeCombate.Empujar;
        s.statusDuration = 0.35f;
        s.statusStrength = 11f;   // m/s hacia atrás, contra lo que tenga detrás
        s.empujeEnElAire = 16f;   // impulso si levita o va por el aire
        s.remateAereo = null;
        s.comboSequence = new ComboButton[0];
        EditorUtility.SetDirty(s);
        log.Append("✓ Empujar · ");
        return s;
    }

    private static void AnadirALaBiblioteca(MagicSpellSO s, StringBuilder log)
    {
        if (s == null) return;
        var lib = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(Biblioteca);
        if (lib == null) { log.Append("✗ no encuentro la biblioteca · "); return; }
        var so = new SerializedObject(lib);
        var lista = so.FindProperty("spells");
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).objectReferenceValue == s) return;
        lista.arraySize++;
        lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = s;
        so.ApplyModifiedPropertiesWithoutUndo();
        lib.Rebuild();
        EditorUtility.SetDirty(lib);
        log.Append("✓ Empujar en la biblioteca · ");
    }

    private static void PonerFicha(PartyControlManager.CharacterSlot quien, MagicSpellSO[] rapidos, MagicSpellSO[] combos,
                                   MagicSpellSO especialDeLaIA, StringBuilder log)
    {
        FichaDePersonaje ficha = null;
        foreach (var guid in AssetDatabase.FindAssets("t:FichaDePersonaje"))
        {
            var f = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null && f.Personaje == quien) { ficha = f; break; }
        }
        if (ficha == null) { log.Append($"✗ sin ficha de {quien} · "); return; }
        var so = new SerializedObject(ficha);
        Lista(so.FindProperty("basicos"), rapidos);
        Lista(so.FindProperty("combos"), combos);
        if (quien == PartyControlManager.CharacterSlot.Liam)
        {
            // La IA usa los de mano y el especial: fuera Aura Estelar y Sello del Pacto.
            so.FindProperty("hechizoIzquierdo").objectReferenceValue = rapidos[3];
            so.FindProperty("hechizoDerecho").objectReferenceValue = rapidos[2];
            if (especialDeLaIA != null) so.FindProperty("hechizoEspecial").objectReferenceValue = especialDeLaIA;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ficha);
        log.Append($"✓ ficha de {quien} · ");
    }

    private static void Lista(SerializedProperty p, MagicSpellSO[] hechizos)
    {
        var validos = new List<MagicSpellSO>();
        foreach (var h in hechizos) if (h != null) validos.Add(h);
        p.arraySize = validos.Count;
        for (int i = 0; i < validos.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = validos[i];
    }

    private static void LeccionDeErikaDeLuz(StringBuilder log)
    {
        var grafo = AssetDatabase.LoadMainAssetAtPath(Grafo);
        if (grafo == null) { log.Append("✗ no encuentro el Cap. 2 · "); return; }
        var so = new SerializedObject(grafo);
        var it = so.GetIterator();
        int cambiados = 0;
        while (it.Next(true))
        {
            if (it.name != "elementoDelEscudo" || it.propertyType != SerializedPropertyType.Enum) continue;
            string padre = it.propertyPath.Substring(0, it.propertyPath.Length - "elementoDelEscudo".Length);
            var npc = so.FindProperty(padre + "npcId");
            var objetivo = so.FindProperty(padre + "objetivo");
            if (npc == null || npc.stringValue != "NPC_Erika" || objetivo == null) continue;
            if (objetivo.intValue != (int)PedirLeccionDeCombate.Objetivo.RomperEscudo) continue;
            it.intValue = (int)MagicElement.Light;
            Poner(so, padre + "fraseAlEmpezar", "Mi escudo es de luz, ¿ves el color? Solo lo rompe otra luz. Usa la Llama Astral.");
            Poner(so, padre + "fraseAlRebotar", "¡Eso no lo rompe! Un escudo de luz solo cede a la luz.");
            cambiados++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(grafo);
        log.Append(cambiados > 0 ? $"✓ lección de Erika con escudo de Luz ({cambiados}) · " : "✗ no encuentro la lección del escudo de Erika · ");
    }

    private static void Poner(SerializedObject so, string ruta, string texto)
    {
        var p = so.FindProperty(ruta);
        if (p != null && p.propertyType == SerializedPropertyType.String) p.stringValue = texto;
    }
}
