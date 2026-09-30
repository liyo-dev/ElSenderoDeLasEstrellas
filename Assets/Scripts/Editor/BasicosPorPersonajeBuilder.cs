using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Cuatro básicos por personaje (INC-498). Raúl: «yo haría a los 3 iguales, con 4 básicos».
/// Rellena la lista de básicos de las fichas de Estela y Liam (rotan con LB igual que los de Will y
/// los usa también su IA) y pone de quién es cada básico antiguo, según el documento del grimorio.
/// Liam se queda con 3 hasta que llegue Dardo Mental (paso 3 del grimorio).
/// Idempotente: si una ficha ya tiene básicos, no la toca.
/// </summary>
public static class BasicosPorPersonajeBuilder
{
    private const string SpellFolder = "Assets/_SPELLS";

    [MenuItem("El Sendero/Archivo/Magia/Cuatro básicos por personaje (INC-498)")]
    public static void Build()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        SetCaster("BolaFuego", Slot.Estela, log, warnings);
        SetCaster("Tornado", Slot.Estela, log, warnings);
        SetCaster("AuraEstelar", Slot.Liam, log, warnings);
        SetCaster("GarraDelPacto", Slot.Liam, log, warnings);

        FillBasics("Assets/_PERSONAJES/Ficha_Estela.asset", new[] { "BolaFuego", "Tornado", "Rafaga", "ChispaIgnea" }, log, warnings);
        FillBasics("Assets/_PERSONAJES/Ficha_Liam.asset", new[] { "GarraDelPacto", "AuraEstelar", "Eco" }, log, warnings);

        AssetDatabase.SaveAssets();

        var final = new StringBuilder("=== Cuatro básicos por personaje (INC-498) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static void SetCaster(string asset, Slot caster, StringBuilder log, List<string> warnings)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"{SpellFolder}/{asset}.asset");
        if (spell == null) { warnings.Add($"No encuentro {asset}."); return; }
        if (spell.caster == caster) return;
        spell.caster = caster;
        EditorUtility.SetDirty(spell);
        log.AppendLine($"{spell.displayName}: de {caster}.");
    }

    private static void FillBasics(string fichaPath, string[] assets, StringBuilder log, List<string> warnings)
    {
        var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(fichaPath);
        if (ficha == null) { warnings.Add($"No encuentro {fichaPath}."); return; }
        var so = new SerializedObject(ficha);
        var list = so.FindProperty("basicos");
        if (list == null) { warnings.Add($"{ficha.name} no tiene el campo 'basicos' (¿sin compilar?)."); return; }
        if (list.arraySize > 0) { log.AppendLine($"{ficha.name}: ya tenía básicos, no se toca."); return; }

        var names = new List<string>();
        foreach (var a in assets)
        {
            var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"{SpellFolder}/{a}.asset");
            if (s == null) { warnings.Add($"No encuentro {a} para {ficha.name} (¿falta pasar el grimorio paso 1 o 2?)."); continue; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            names.Add(s.displayName);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"{ficha.name}: {string.Join(", ", names)}.");
    }
}
