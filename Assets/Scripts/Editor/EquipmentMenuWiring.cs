using System.Collections.Generic;
using System.Text;
using Core.InputGlyphs;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú de equipo y pantalla de Controles para el nuevo reparto de combate (INC-485). Idempotente.
/// <list type="bullet">
/// <item>Start.unity · SpellsPanel/SlotPanel: cuatro filas de básicos, de arriba abajo en el orden
/// de rotación con LB (añade la cuarta, copia de la tercera) y la enlaza en
/// PlayerEquipmentMenuController (spellUI.fourthSlot*, emptySlotSprite).</item>
/// <item>_UI/ControlsSchemeConfig.asset: rehace la lista con ControlsSchemeConfig.BuildDefaultEntries().</item>
/// </list>
/// </summary>
public static class EquipmentMenuWiring
{
    private const string ScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string ControlsPath = "Assets/_UI/ControlsSchemeConfig.asset";
    private const string EmptySlotPath = "Assets/Art/UI/HUD/SLOT_Empty.png";
    private const float RowHeight = 77f;
    private const float ButtonSize = 64f;

    [MenuItem("El Sendero/UI/Menú de equipo con 4 básicos y Controles nuevos (INC-485)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        WireControls(log, warnings);
        WireMenu(log, warnings);

        var final = new StringBuilder("=== Menú de equipo y Controles (INC-485) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static void WireControls(StringBuilder log, List<string> warnings)
    {
        var scheme = AssetDatabase.LoadAssetAtPath<ControlsSchemeConfig>(ControlsPath);
        if (scheme == null) { warnings.Add($"No encuentro {ControlsPath}."); return; }
        scheme.entries = ControlsSchemeConfig.BuildDefaultEntries();
        EditorUtility.SetDirty(scheme);
        AssetDatabase.SaveAssets();
        log.AppendLine($"Controles: {scheme.entries.Count} filas con el reparto nuevo.");
    }

    private static void WireMenu(StringBuilder log, List<string> warnings)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        try
        {
            PlayerEquipmentMenuController menu = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                menu = r.GetComponentInChildren<PlayerEquipmentMenuController>(true);
                if (menu != null) break;
            }
            if (menu == null) { warnings.Add("No encuentro PlayerEquipmentMenuController en Start.unity."); return; }

            var so = new SerializedObject(menu);
            var ui = so.FindProperty("spellUI");
            var left = ui.FindPropertyRelative("leftSlotButton").objectReferenceValue as UnityEngine.UI.Button;
            var right = ui.FindPropertyRelative("rightSlotButton").objectReferenceValue as UnityEngine.UI.Button;
            var special = ui.FindPropertyRelative("specialSlotButton").objectReferenceValue as UnityEngine.UI.Button;
            var specialLabel = ui.FindPropertyRelative("specialSlotLabel").objectReferenceValue as UnityEngine.UI.Text;
            if (left == null || right == null || special == null || specialLabel == null)
            { warnings.Add("Faltan las ranuras 1-3 en spellUI."); return; }

            var rows = new List<RectTransform>
            {
                left.transform.parent as RectTransform,
                right.transform.parent as RectTransform,
                special.transform.parent as RectTransform,
            };

            var fourthButtonProp = ui.FindPropertyRelative("fourthSlotButton");
            var fourthLabelProp = ui.FindPropertyRelative("fourthSlotLabel");
            var fourthButton = fourthButtonProp.objectReferenceValue as UnityEngine.UI.Button;
            if (fourthButton == null)
            {
                var copy = Object.Instantiate(rows[2].gameObject, rows[2].parent);
                copy.name = "Row (3)";
                fourthButton = copy.GetComponentInChildren<UnityEngine.UI.Button>(true);
                var fourthLabel = copy.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (fourthButton == null || fourthLabel == null) { warnings.Add("La copia de la fila 3 no tiene botón o texto."); Object.DestroyImmediate(copy); return; }
                fourthButton.name = "FourthSlotButton";
                fourthLabel.name = "FourthSlotLabel";
                int n = fourthButton.onClick.GetPersistentEventCount();
                for (int i = n - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(fourthButton.onClick, i);
                fourthButtonProp.objectReferenceValue = fourthButton;
                fourthLabelProp.objectReferenceValue = fourthLabel;
                log.AppendLine("Menú: añadida la ranura del básico 4.");
            }
            rows.Add(fourthButton.transform.parent as RectTransform);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(0f, -i * RowHeight);
                row.sizeDelta = new Vector2(row.sizeDelta.x, RowHeight);
                var b = row.GetComponentInChildren<UnityEngine.UI.Button>(true);
                if (b != null) ((RectTransform)b.transform).sizeDelta = new Vector2(ButtonSize, ButtonSize);
            }

            var empty = AssetDatabase.LoadAssetAtPath<Sprite>(EmptySlotPath);
            if (empty != null) ui.FindPropertyRelative("emptySlotSprite").objectReferenceValue = empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("Menú: cuatro filas en orden de rotación (1 arriba, 4 abajo). Start.unity guardada.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
