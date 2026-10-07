using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pestaña Hechizos del menú de Start (INC-515): conecta la línea de ayuda de abajo del panel
/// (<c>spellUI.hintLabel</c>), añade la tarjeta «Grimorio» a la barra de pistas de abajo (con el icono
/// del botón que lo abre, InputGlyphNames.Select) y reserva el hueco del contador de monedas.
/// Reutiliza el helper de tarjetas para mantener el arte y las dimensiones de la barra.
/// </summary>
public static class PestanaHechizosBuilder
{
    private const string StartScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string CardName = "PanelInfoGrimorio";

    [MenuItem("El Sendero/Archivo/UI/Pestaña Hechizos: equipar con A y tarjeta del grimorio (INC-515)")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder("=== Pestaña Hechizos (INC-515) ===\n");
        var warnings = new List<string>();

        var scene = SceneManager.GetSceneByPath(StartScenePath);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        PlayerEquipmentMenuController menu = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            menu = go.GetComponentInChildren<PlayerEquipmentMenuController>(true);
            if (menu != null) break;
        }

        if (menu == null) warnings.Add("Start.unity no tiene PlayerEquipmentMenuController.");
        else
        {
            var so = new SerializedObject(menu);
            ConectarAyuda(so, log, warnings);
            TarjetaGrimorio(so, menu, log, warnings);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (opened) EditorSceneManager.CloseScene(scene, true);

        if (warnings.Count == 0) { log.AppendLine("Sin avisos."); Debug.Log(log.ToString()); }
        else
        {
            log.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) log.AppendLine("  • " + w);
            Debug.LogWarning(log.ToString());
        }
    }

    /// Conecta la línea de ayuda (spellUI.hintLabel). Es un texto TMP para poder llevar iconos de
    /// botón; si en la escena aún es un Text de los antiguos, lo cambia por uno TMP con el mismo
    /// tamaño, color y alineación. Lo llama también el montaje del grimorio.
    public static void ConectarAyuda(SerializedObject so, StringBuilder log, List<string> warnings)
    {
        var prop = so.FindProperty("spellUI.hintLabel");
        if (prop == null) { warnings.Add("spellUI no tiene 'hintLabel' (¿sin compilar?)."); return; }

        var root = so.FindProperty("spellUI.root")?.objectReferenceValue as GameObject;
        var hint = root != null ? root.transform.Find("HintLabel") : null;
        if (hint == null) { warnings.Add("No encuentro 'HintLabel' en la pestaña Hechizos."); return; }

        var tmp = hint.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            var legacy = hint.GetComponent<Text>();
            if (legacy == null) { warnings.Add("'HintLabel' no tiene texto."); return; }
            int size = legacy.fontSize;
            Color color = legacy.color;
            TextAnchor anchor = legacy.alignment;
            Undo.DestroyObjectImmediate(legacy);

            var t = hint.gameObject.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HintFontPath);
            if (font != null) t.font = font; else warnings.Add("No encuentro " + HintFontPath + "; la ayuda usa la fuente por defecto.");
            t.fontSize = size;
            t.enableAutoSizing = true;          // si una ayuda no cabe, encoge un poco antes que salirse
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Round(size * 0.7f);
            t.color = color;
            t.alignment = Alineacion(anchor);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            t.raycastTarget = false;
            t.text = "";
            tmp = t;
            log.AppendLine("   Línea de ayuda de la pestaña Hechizos pasada a TMP (lleva iconos de botón).");
        }

        if (prop.objectReferenceValue != tmp)
        {
            prop.objectReferenceValue = tmp;
            log.AppendLine("   Línea de ayuda de la pestaña Hechizos conectada (la escribe el código según el foco).");
        }
    }

    private const string HintFontPath = "Assets/Plugins/Fonts/Nunito-Bold SDF.asset";

    private static TextAlignmentOptions Alineacion(TextAnchor a) => a switch
    {
        TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
        TextAnchor.UpperCenter => TextAlignmentOptions.Top,
        TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
        TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
        TextAnchor.MiddleRight => TextAlignmentOptions.Right,
        TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
        TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
        TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
        _ => TextAlignmentOptions.Center,
    };

    private static void TarjetaGrimorio(SerializedObject so, PlayerEquipmentMenuController menu, StringBuilder log, List<string> warnings)
    {
        var prop = so.FindProperty("grimorioHintCard");
        if (prop == null) { warnings.Add("El menú no tiene grimorioHintCard."); return; }
        var bar = TarjetasDeAyudaBuilder.Buscar(menu.transform, "PanelInfo");
        if (bar == null) { warnings.Add("No se encuentra PanelInfo."); return; }
        var card = TarjetasDeAyudaBuilder.Montar(bar, CardName, Core.InputGlyphs.InputGlyphNames.Select,
            "GRIMOIRE_BUTTON", "Grimorio", false, warnings);
        if (card != null) prop.objectReferenceValue = card;
        TarjetasDeAyudaBuilder.AjustarBarra(bar, warnings);
        log.AppendLine("   Tarjeta Grimorio reparada con el helper de tarjetas de ayuda.");
    }
}
