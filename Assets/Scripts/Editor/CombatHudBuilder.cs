using System.Collections.Generic;
using System.Text;
using Core.InputGlyphs;
using Sendero.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta en Start.unity la cruz de combate del HUD (INC-485), según la maqueta aprobada:
/// <list type="bullet">
/// <item>Quita la fila de tres círculos del panel de estado (Main/BG/PanelDown) y encoge el panel
/// a la mitad de alto; los retratos bajan con él.</item>
/// <item>Crea "CombatButtons" abajo a la derecha: X (hechizo activo grande, los otros básicos en
/// pequeño, LB y los puntos de la serie), Y (combo) y B (defensa), cada uno con el icono de su
/// botón según el mando.</item>
/// <item>Añade y enlaza CombatButtonsHUD.</item>
/// </list>
/// Si CombatButtons ya existe no lo vuelve a crear (así no se pierden ajustes hechos a mano).
/// </summary>
public static class CombatHudBuilder
{
    private const string ScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string RingPath = "Assets/Art/UI/HUD/ability_socket_ring.png";
    private const string CooldownRingPath = "Assets/Art/UI/HUD/cooldown_ring_fill.png";
    private const string SigilPath = "Assets/Art/UI/Símbolo arcano luminoso.png";
    private const string ShieldSourcePath = "Assets/Plugins/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Textures/Human_Spell_Shield.png";
    private const string ShieldPath = "Assets/Art/UI/HUD/escudo_hud.png";

    private const float PanelFullHeight = 572f;
    private const float PanelHalfHeight = 286f;

    // Caja de la cruz, en unidades locales (se escala igual que el panel de estado).
    private const float BoxWidth = 1100f;
    private const float BoxHeight = 950f;
    private const float BoxScale = 0.38067f;
    private static readonly Vector2 BoxMargin = new Vector2(-50f, 30f);

    private static readonly Vector2 XCenter = new Vector2(560f, 330f);
    private const float XSize = 400f;
    private const float MiniSize = 130f;
    private const float MiniOrbit = 295f;
    private static readonly Vector2 YCenter = new Vector2(790f, 770f);
    private static readonly Vector2 BCenter = new Vector2(950f, 400f);
    private const float SideSize = 280f;
    private const float IconFactor = 0.8f;
    private const float BadgeSize = 115f;
    private const float DotSize = 30f;

    private static readonly Color ComboGold = new Color(0.95f, 0.76f, 0.31f, 1f);

    [MenuItem("El Sendero/Archivo/UI/Montar HUD de combate (INC-485)")]
    public static void Build()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        Sprite shield = EnsureShieldSprite(log, warnings);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            GameObject canvas = FindRoot(scene, "PlayerHUD_Canvas");
            if (canvas == null)
            {
                warnings.Add("No encuentro PlayerHUD_Canvas en Start.unity.");
                return;
            }

            ShrinkStatusPanel(canvas.transform, log, warnings);
            BuildCluster(canvas.transform, shield, log, warnings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("Start.unity guardada.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Report(log, warnings);
        }
    }

    // ── Panel de estado ─────────────────────────────────────────────────────────────

    private static void ShrinkStatusPanel(Transform canvas, StringBuilder log, List<string> warnings)
    {
        var main = canvas.Find("Main") as RectTransform;
        var bg = canvas.Find("Main/BG") as RectTransform;
        if (main == null || bg == null)
        {
            warnings.Add("No encuentro Main/BG en PlayerHUD_Canvas; el panel de estado no se toca.");
            return;
        }

        var panelDown = bg.Find("PanelDown");
        if (panelDown != null)
        {
            Undo.DestroyObjectImmediate(panelDown.gameObject);
            log.AppendLine("Quitada la fila de círculos (Main/BG/PanelDown).");
        }

        if (Mathf.Approximately(main.sizeDelta.y, PanelFullHeight))
        {
            Undo.RecordObject(main, "Encoger panel de estado");
            main.sizeDelta = new Vector2(main.sizeDelta.x, PanelHalfHeight);
            log.AppendLine("Main: alto 572 → 286.");
        }
        if (Mathf.Approximately(bg.sizeDelta.y, PanelFullHeight))
        {
            Undo.RecordObject(bg, "Encoger panel de estado");
            bg.sizeDelta = new Vector2(bg.sizeDelta.x, PanelHalfHeight);
            log.AppendLine("BG: alto 572 → 286.");
        }
    }

    // ── Cruz de combate ─────────────────────────────────────────────────────────────

    private static void BuildCluster(Transform canvas, Sprite shield, StringBuilder log, List<string> warnings)
    {
        if (canvas.Find("CombatButtons") != null)
        {
            log.AppendLine("CombatButtons ya existe: no se vuelve a crear.");
            return;
        }

        Sprite ring = Load<Sprite>(RingPath, warnings);
        Sprite cooldownRing = Load<Sprite>(CooldownRingPath, warnings);
        Sprite sigil = Load<Sprite>(SigilPath, warnings);
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        var box = NewRect("CombatButtons", canvas);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 0f);
        box.sizeDelta = new Vector2(BoxWidth, BoxHeight);
        box.anchoredPosition = BoxMargin;
        box.localScale = Vector3.one * BoxScale;
        box.SetAsLastSibling();

        // X · hechizo activo
        var x = Circle("ButtonX", box, XCenter, XSize, ring);
        var activeIcon = Icon("Icon", x, XSize * IconFactor, null);
        Badge(x, XSize, InputGlyphNames.West);

        var dots = new UnityEngine.UI.Image[3];
        for (int i = 0; i < 3; i++)
        {
            var dot = NewRect("SeriesDot" + (i + 1), box);
            Place(dot, XCenter + new Vector2((i - 1) * (DotSize * 1.6f), XSize * 0.5f + DotSize * 1.2f), DotSize);
            dots[i] = AddImage(dot, knob, new Color(1f, 1f, 1f, 0.25f));
        }

        var miniRoots = new GameObject[3];
        var miniIcons = new UnityEngine.UI.Image[3];
        float[] angles = { 135f, 180f, 225f };
        for (int i = 0; i < 3; i++)
        {
            float a = angles[i] * Mathf.Deg2Rad;
            var pos = XCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * MiniOrbit;
            var mini = Circle("NextSpell" + (i + 1), box, pos, MiniSize, ring);
            miniRoots[i] = mini.gameObject;
            miniIcons[i] = Icon("Icon", mini, MiniSize * IconFactor, null);
        }

        var lb = NewRect("RotateHintLB", box);
        Place(lb, XCenter + new Vector2(-MiniOrbit - MiniSize * 0.5f - 70f, 0f), 110f);
        AddImage(lb, null, Color.white);
        AddGlyph(lb.gameObject, InputGlyphNames.ShoulderLeft);

        // Y · combo
        var y = Circle("ButtonY", box, YCenter, SideSize, ring);
        var sigilImg = Icon("Sigil", y, SideSize * IconFactor, sigil);
        sigilImg.preserveAspect = true;
        var comboFill = Icon("ComboCooldown", y, SideSize * 0.92f, cooldownRing);
        comboFill.color = ComboGold;
        comboFill.type = UnityEngine.UI.Image.Type.Filled;
        comboFill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        comboFill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        comboFill.fillClockwise = false;
        comboFill.fillAmount = 0f;
        comboFill.enabled = false;
        Badge(y, SideSize, InputGlyphNames.North);

        // B · defensa
        var b = Circle("ButtonB", box, BCenter, SideSize, ring);
        var shieldImg = Icon("Shield", b, SideSize * IconFactor, shield);
        shieldImg.preserveAspect = true;
        Badge(b, SideSize, InputGlyphNames.East);

        // Lógica
        var hud = box.gameObject.AddComponent<CombatButtonsHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("activeIcon").objectReferenceValue = activeIcon;
        SetArray(so.FindProperty("rotationIcons"), miniIcons);
        SetArray(so.FindProperty("rotationSlots"), miniRoots);
        SetArray(so.FindProperty("seriesDots"), dots);
        so.FindProperty("rotateHint").objectReferenceValue = lb.gameObject;
        so.FindProperty("comboCooldownFill").objectReferenceValue = comboFill;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(box.gameObject, "Montar HUD de combate");
        log.AppendLine("Creado CombatButtons (X, Y, B, siguientes hechizos, LB y serie) con CombatButtonsHUD.");
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────────

    private static RectTransform Circle(string name, RectTransform parent, Vector2 center, float size, Sprite ring)
    {
        var rt = NewRect(name, parent);
        Place(rt, center, size);
        var img = AddImage(rt, ring, Color.white);
        img.preserveAspect = true;
        return rt;
    }

    private static UnityEngine.UI.Image Icon(string name, RectTransform parent, float size, Sprite sprite)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = AddImage(rt, sprite, Color.white);
        img.preserveAspect = true;
        return img;
    }

    private static void Badge(RectTransform circle, float circleSize, string glyph)
    {
        var rt = NewRect("Glyph", circle);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float off = circleSize * 0.5f * 0.72f;
        rt.anchoredPosition = new Vector2(off, -off);
        rt.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        AddImage(rt, null, Color.white);
        AddGlyph(rt.gameObject, glyph);
    }

    private static void AddGlyph(GameObject go, string glyph)
    {
        var icon = go.AddComponent<InputGlyphIcon>();
        var so = new SerializedObject(icon);
        so.FindProperty("glyphName").stringValue = glyph;
        so.FindProperty("icon").objectReferenceValue = go.GetComponent<UnityEngine.UI.Image>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Coloca un elemento por su centro, en coordenadas desde la esquina inferior izquierda de la caja.</summary>
    private static void Place(RectTransform rt, Vector2 center, float size)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = new Vector2(size, size);
    }

    private static UnityEngine.UI.Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ── Recursos ────────────────────────────────────────────────────────────────────

    private static Sprite EnsureShieldSprite(StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(ShieldPath) == null)
        {
            if (!AssetDatabase.CopyAsset(ShieldSourcePath, ShieldPath))
            {
                warnings.Add($"No he podido copiar '{ShieldSourcePath}' a '{ShieldPath}'. El círculo B se queda sin icono.");
                return null;
            }
            log.AppendLine($"Copiado el icono del escudo a {ShieldPath}.");
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(ShieldPath);
        if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 256)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            log.AppendLine("escudo_hud.png importado como Sprite (256).");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ShieldPath);
    }

    private static T Load<T>(string path, List<string> warnings) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) warnings.Add($"No encuentro '{path}'.");
        return asset;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var t = FindDeep(child, name);
            if (t != null) return t;
        }
        return null;
    }

    private static void Report(StringBuilder log, List<string> warnings)
    {
        var final = new StringBuilder("=== HUD de combate (INC-485) ===\n");
        final.Append(log);
        if (warnings.Count == 0)
        {
            final.AppendLine("Sin avisos.");
            Debug.Log(final.ToString());
        }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }
}
