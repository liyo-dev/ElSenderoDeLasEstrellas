using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta el grimorio en forma de libro (INC-506) en el menú de Start: el libro (GrimorioLibroUI)
/// encima de todo el menú, el botón «Grimorio» en la pestaña Hechizos y la línea de ayuda de esa
/// pestaña con iconos de botón. Pone además la frase de cada hechizo (MagicSpellSO.lore) si aún
/// no la tiene. El libro se vuelve a montar entero cada vez (todo él sale de aquí); lo demás, si
/// ya está, no se toca.
/// </summary>
public static class GrimorioLibroBuilder
{
    private const string StartScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string ArtFolder = "Assets/Art/UI/Grimorio/";
    private const string SoundFolder = "Assets/Audio/FREE SOUND PACK_TM(355)/Misc(58)/";

    private static readonly Color Ink = new Color(0.24f, 0.14f, 0.08f);
    private static readonly Color Muted = new Color(0.55f, 0.4f, 0.2f);

    [MenuItem("El Sendero/UI/Montar el grimorio en libro (INC-506)")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder();
        var warnings = new List<string>();
        Montar(log, warnings);
        var final = new StringBuilder("=== Grimorio en libro (INC-506) ===\n").Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    /// Lo llama también «Grimorio · montar todo lo que queda».
    public static void Montar(StringBuilder log, List<string> warnings)
    {
        ImportSprites(log);
        Lore(log);
        AssetDatabase.SaveAssets();

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
        if (menu == null) { warnings.Add("Start.unity no tiene PlayerEquipmentMenuController."); if (opened) EditorSceneManager.CloseScene(scene, true); return; }

        var so = new SerializedObject(menu);
        var libroProp = so.FindProperty("grimorioLibro");
        if (libroProp == null) { warnings.Add("PlayerEquipmentMenuController no tiene 'grimorioLibro' (¿sin compilar?)."); if (opened) EditorSceneManager.CloseScene(scene, true); return; }

        var font = (so.FindProperty("levelText")?.objectReferenceValue as TextMeshProUGUI)?.font;
        var canvas = so.FindProperty("canvas")?.objectReferenceValue as Canvas;
        Transform parent = canvas != null ? canvas.transform : menu.transform;

        if (libroProp.objectReferenceValue is Component viejo)
        {
            Undo.DestroyObjectImmediate(viejo.gameObject);
            log.AppendLine("   Libro anterior quitado para montarlo de nuevo.");
        }
        libroProp.objectReferenceValue = BuildBook(parent, font, warnings);
        log.AppendLine("   Libro del grimorio montado encima del menú de Start.");

        PestanaHechizosBuilder.ConectarAyuda(so, log, warnings);

        var spellRoot = so.FindProperty("spellUI.root")?.objectReferenceValue as GameObject;
        if (spellRoot == null) warnings.Add("No encuentro la raíz de la pestaña Hechizos (spellUI.root).");
        else if (spellRoot.transform.Find("BotonGrimorio") == null)
        {
            BuildButton(spellRoot.transform, menu, font);
            log.AppendLine("   Botón «Grimorio» en la pestaña Hechizos.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    // ── Sprites ───────────────────────────────────────────────────────────

    private static void ImportSprites(StringBuilder log)
    {
        int n = 0;
        foreach (var name in new[] { "libro_abierto", "marco_icono", "fondo_medallon", "sello_desconocido", "separador", "flecha_pagina", "cinta_will", "cinta_estela", "cinta_liam" })
        {
            string path = ArtFolder + name + ".png";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = name == "libro_abierto" ? 2048 : 1024;
                ti.SaveAndReimport();
                n++;
            }
        }
        if (n > 0) log.AppendLine($"   {n} imagen(es) del libro importadas como sprite.");
    }

    private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");

    // ── Frases del grimorio ───────────────────────────────────────────────

    private static readonly (string asset, string lore)[] Lores =
    {
        ("LlamaAstral", "La primera llama que Will aprendió a encender en la palma de la mano."),
        ("BolaPrisma", "Luz de estrellas apretada hasta que cabe en un puño."),
        ("CorazonEstelar", "Late como una estrella que aún no ha decidido apagarse."),
        ("Levitation", "El suelo sigue ahí; simplemente deja de importar un rato."),
        ("EstrellaFugaz", "Nunca cae dos veces en el mismo sitio: salta de enemigo en enemigo."),
        ("LluviaDeChispas", "Tres chispas que se reparten el trabajo."),
        ("Meteoro", "Cuando las estrellas caen, no piden permiso."),
        ("CupulaEstelar", "Un trozo de cielo nocturno para que nadie se quede fuera."),
        ("NovaDeLuz", "Un instante de mediodía en mitad del combate."),
        ("BolaFuego", "El fuego de Estela: rápido, cálido y sin remordimientos."),
        ("Tornado", "Un remolino pequeño con muy mal carácter."),
        ("Rafaga", "Un golpe de viento que aparta lo que estorba."),
        ("ChispaIgnea", "Donde cae, el suelo recuerda el fuego un buen rato."),
        ("MuroDeFuego", "Una línea que nadie en su sano juicio cruza."),
        ("Remolino", "El viento gira, y todo lo que está cerca gira con él."),
        ("BrisaSanadora", "Una brisa que huele a hierba nueva y cierra las heridas."),
        ("TormentaDeFuego", "El remate de Estela: el cielo arde y después llueve fuego."),
        ("GarraDelPacto", "El pacto tiene uñas, y Liam sabe dónde clavarlas."),
        ("AuraEstelar", "Un resplandor prestado que Liam lanza como una daga."),
        ("DardoMental", "Un pensamiento afilado que vuelve lentos los pies del enemigo."),
        ("Eco", "Lo que Liam dice una vez lo oyen todos los que están en fila."),
        ("SelloDelPacto", "Un círculo en el suelo que recuerda a todos las condiciones del pacto."),
        ("CadenasDelPacto", "Las cláusulas del pacto, convertidas en cadenas."),
        ("PasoSombrio", "Un paso por la sombra, y ya está más allá."),
        ("JuicioDelPacto", "El pacto dicta sentencia sobre todo lo que pisa su círculo."),
    };

    private static void Lore(StringBuilder log)
    {
        int n = 0;
        foreach (var (asset, lore) in Lores)
        {
            var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"Assets/_SPELLS/{asset}.asset");
            if (s == null || !string.IsNullOrEmpty(s.lore)) continue;
            s.lore = lore;
            EditorUtility.SetDirty(s);
            n++;
        }
        if (n > 0) log.AppendLine($"   Frase del grimorio en {n} hechizo(s).");
    }

    // ── El libro ──────────────────────────────────────────────────────────

    private static GrimorioLibroUI BuildBook(Transform parent, TMP_FontAsset font, List<string> warnings)
    {
        var root = NewRect("Grimorio (libro)", parent);
        Stretch(root);
        root.SetAsLastSibling();
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var ui = root.gameObject.AddComponent<GrimorioLibroUI>();
        var audio = root.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        // Fondo oscuro: un clic fuera del libro lo cierra.
        var fondo = NewImage("Fondo", root, null, new Color(0.02f, 0.01f, 0.03f, 0.8f));
        Stretch(fondo.rectTransform);
        var fondoButton = fondo.gameObject.AddComponent<Button>();
        fondoButton.transition = Selectable.Transition.None;
        var nav = fondoButton.navigation; nav.mode = Navigation.Mode.None; fondoButton.navigation = nav;

        // Pestañas de cada personaje, asomando por el canto derecho (detrás del libro).
        var ribbons = new Image[3];
        string[] tabNames = { "Will", "Estela", "Liam" };
        Color[] tabColors = { new Color(0.85f, 0.64f, 0.2f), new Color(0.75f, 0.25f, 0.16f), new Color(0.5f, 0.26f, 0.72f) };
        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        for (int i = 0; i < 3; i++)
        {
            var tab = NewImage("Pestana_" + tabNames[i], root, rounded, tabColors[i]);
            tab.type = Image.Type.Sliced;
            var rt = tab.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            Place(rt, new Vector2(760f, 300f - i * 90f), new Vector2(175f, 70f));
            var label = NewText("Nombre", rt, font, 26f, Color.white, FontStyles.Bold, Vector2.zero, new Vector2(120f, 60f));
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(-68f, 0f);
            label.text = tabNames[i];
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            ribbons[i] = tab;
        }

        var book = NewImage("Libro", root, Art("libro_abierto"), Color.white);
        Place(book.rectTransform, Vector2.zero, new Vector2(1600f, 1000f));
        book.preserveAspect = true;
        book.raycastTarget = true; // que un clic en el libro no llegue al fondo (que cierra)

        // Página izquierda: medallón, nombre, de quién es y etiquetas de tipo y elemento.
        var left = NewRect("PaginaIzquierda", book.rectTransform);
        Place(left, new Vector2(-356f, 10f), new Vector2(620f, 760f));
        left.gameObject.AddComponent<CanvasGroup>();
        var medallon = NewImage("Medallon", left, Art("fondo_medallon"), Color.white);
        Place(medallon.rectTransform, new Vector2(0f, 140f), new Vector2(330f, 330f));
        var icon = NewImage("Icono", left, null, Color.white);
        Place(icon.rectTransform, new Vector2(0f, 140f), new Vector2(290f, 290f));
        icon.preserveAspect = true;
        var marco = NewImage("Marco", left, Art("marco_icono"), Color.white);
        Place(marco.rectTransform, new Vector2(0f, 140f), new Vector2(430f, 430f));
        var seal = NewImage("Sello", left, Art("sello_desconocido"), Color.white);
        Place(seal.rectTransform, new Vector2(95f, 55f), new Vector2(170f, 170f));
        var name = NewText("Nombre", left, font, 58f, Ink, FontStyles.Bold, new Vector2(0f, -112f), new Vector2(600f, 80f));
        AutoSize(name, 40f);
        var owner = NewText("Personaje", left, font, 28f, Ink, FontStyles.Italic, new Vector2(0f, -170f), new Vector2(600f, 44f));
        var sepL = NewImage("Separador", left, Art("separador"), Color.white);
        Place(sepL.rectTransform, new Vector2(0f, -218f), new Vector2(460f, 30f));
        var tags = NewRow("Etiquetas", left, new Vector2(0f, -272f), new Vector2(560f, 48f), 14f, TextAnchor.MiddleCenter);
        var (kindTag, kindText) = NewPill("Tipo", tags, font, 21f);
        var (elementTag, elementText) = NewPill("Elemento", tags, font, 21f);

        // Página derecha: cómo se lanza, frase, datos, lo que hace y si está equipado.
        var right = NewRect("PaginaDerecha", book.rectTransform);
        Place(right, new Vector2(356f, 10f), new Vector2(620f, 760f));
        right.gameObject.AddComponent<CanvasGroup>();

        var castTitle = NewText("TituloLanzar", right, font, 22f, Muted, FontStyles.Bold | FontStyles.UpperCase, new Vector2(0f, 318f), new Vector2(560f, 36f));
        castTitle.characterSpacing = 8f;

        var castRow = NewRow("Lanzar", right, new Vector2(0f, 236f), new Vector2(580f, 110f), 26f, TextAnchor.UpperCenter);
        var stepIcons = new TextMeshProUGUI[2];
        var stepCaptions = new TextMeshProUGUI[2];
        GameObject arrow = null;
        for (int i = 0; i < 2; i++)
        {
            if (i == 1)
            {
                var a = NewImage("Flecha", castRow, Art("flecha_pagina"), Color.white);
                a.preserveAspect = true;
                a.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // la flecha del arte mira a la izquierda
                var le = a.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 34f;
                le.preferredHeight = 64f;
                arrow = a.gameObject;
            }
            var step = NewColumn("Paso" + (i + 1), castRow, 2f);
            stepIcons[i] = NewText("Iconos", step, font, 52f, Ink, FontStyles.Normal, Vector2.zero, new Vector2(200f, 64f));
            stepIcons[i].textWrappingMode = TextWrappingModes.NoWrap;
            stepCaptions[i] = NewText("Que", step, font, 21f, Muted, FontStyles.Italic, Vector2.zero, new Vector2(200f, 30f));
            stepCaptions[i].textWrappingMode = TextWrappingModes.NoWrap;
        }

        var sep1 = NewImage("Separador1", right, Art("separador"), Color.white);
        Place(sep1.rectTransform, new Vector2(0f, 150f), new Vector2(460f, 30f));
        var lore = NewText("Frase", right, font, 30f, Ink, FontStyles.Normal, new Vector2(0f, 70f), new Vector2(540f, 124f));
        AutoSize(lore, 22f);
        var sep2 = NewImage("Separador2", right, Art("separador"), Color.white);
        Place(sep2.rectTransform, new Vector2(0f, -10f), new Vector2(460f, 30f));

        var statRow = NewRow("Datos", right, new Vector2(0f, -82f), new Vector2(580f, 86f), 34f, TextAnchor.MiddleCenter);
        var statValues = new TextMeshProUGUI[5];
        var statLabels = new TextMeshProUGUI[5];
        for (int i = 0; i < 5; i++)
        {
            var cell = NewColumn("Dato" + (i + 1), statRow, -4f);
            statValues[i] = NewText("Valor", cell, font, 44f, Ink, FontStyles.Bold, Vector2.zero, new Vector2(120f, 54f));
            statValues[i].textWrappingMode = TextWrappingModes.NoWrap;
            statLabels[i] = NewText("Nombre", cell, font, 17f, Muted, FontStyles.Bold | FontStyles.UpperCase, Vector2.zero, new Vector2(120f, 24f));
            statLabels[i].characterSpacing = 4f;
            statLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
        }

        var effect = NewText("Efecto", right, font, 25f, new Color(0.5f, 0.16f, 0.08f), FontStyles.Normal, new Vector2(0f, -172f), new Vector2(540f, 64f));
        AutoSize(effect, 19f);

        var equippedHolder = NewRow("Equipado", right, new Vector2(0f, -248f), new Vector2(560f, 48f), 0f, TextAnchor.MiddleCenter);
        var (equippedTag, equippedText) = NewPill("Etiqueta", equippedHolder, font, 21f);

        // Hoja que gira (se ve solo al pasar página)
        var leaf = NewImage("HojaQueGira", book.rectTransform, null, new Color(0.95f, 0.91f, 0.8f, 1f));
        Place(leaf.rectTransform, new Vector2(0f, 10f), new Vector2(700f, 780f));
        leaf.rectTransform.pivot = new Vector2(0f, 0.5f);
        leaf.rectTransform.anchoredPosition = Vector2.zero + new Vector2(0f, 10f);
        leaf.raycastTarget = false;
        var shadow = leaf.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(6f, -6f);
        leaf.gameObject.SetActive(false);

        // Flechas
        var prev = NewButton("Anterior", book.rectTransform, Art("flecha_pagina"), new Vector2(-770f, -20f), new Vector2(90f, 90f));
        var next = NewButton("Siguiente", book.rectTransform, Art("flecha_pagina"), new Vector2(770f, -20f), new Vector2(90f, 90f));
        next.transform.localScale = new Vector3(-1f, 1f, 1f);

        var counter = NewText("Contador", book.rectTransform, font, 24f, new Color(0.96f, 0.88f, 0.7f), FontStyles.Normal, new Vector2(0f, -470f), new Vector2(1200f, 40f));
        var hint = NewText("Pistas", root, font, 26f, new Color(1f, 1f, 1f, 0.85f), FontStyles.Normal, new Vector2(0f, 44f), new Vector2(1600f, 48f));
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);

        // Referencias del componente
        var so = new SerializedObject(ui);
        Set(so, "group", group);
        Set(so, "book", book.rectTransform);
        Set(so, "leftPage", left);
        Set(so, "icon", icon);
        Set(so, "seal", seal);
        Set(so, "nameText", name);
        Set(so, "ownerText", owner);
        Set(so, "kindTag", kindTag);
        Set(so, "kindTagText", kindText);
        Set(so, "elementTag", elementTag);
        Set(so, "elementTagText", elementText);
        Set(so, "rightPage", right);
        Set(so, "castTitleText", castTitle);
        Set(so, "castRow", castRow.gameObject);
        SetArray(so, "stepIcons", stepIcons);
        SetArray(so, "stepCaptions", stepCaptions);
        Set(so, "castArrow", arrow);
        Set(so, "loreText", lore);
        Set(so, "statRow", statRow.gameObject);
        SetArray(so, "statValues", statValues);
        SetArray(so, "statLabels", statLabels);
        Set(so, "effectText", effect);
        Set(so, "equippedTag", equippedTag);
        Set(so, "equippedTagText", equippedText);
        Set(so, "pageCounterText", counter);
        Set(so, "hintText", hint);
        Set(so, "turningLeaf", leaf.rectTransform);
        Set(so, "prevButton", prev);
        Set(so, "nextButton", next);
        Set(so, "closeButton", fondoButton);
        Set(so, "audioSource", audio);
        var openClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + "Book_Page_Turning-008.wav");
        var pageClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + "Book_Page_Turning-002.wav");
        if (openClip == null || pageClip == null) warnings.Add("No encuentro los sonidos de pasar página; el libro irá en silencio.");
        Set(so, "openClip", openClip);
        Set(so, "pageClip", pageClip);
        var arr = so.FindProperty("ribbons");
        arr.arraySize = 3;
        for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ribbons[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        group.alpha = 0f;
        group.blocksRaycasts = false;
        root.gameObject.SetActive(false);
        return ui;
    }

    private static void BuildButton(Transform spellRoot, PlayerEquipmentMenuController menu, TMP_FontAsset font)
    {
        var bg = NewImage("BotonGrimorio", spellRoot, null, new Color(0.36f, 0.16f, 0.11f, 0.95f));
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -16f);
        rt.sizeDelta = new Vector2(260f, 64f);
        var outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.84f, 0.67f, 0.31f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);
        var button = bg.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
        colors.selectedColor = new Color(1f, 0.9f, 0.7f);
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(menu.AbrirGrimorio));

        var ribbon = NewImage("Cinta", rt, Art("cinta_will"), Color.white);
        ribbon.rectTransform.anchorMin = ribbon.rectTransform.anchorMax = new Vector2(0f, 1f);
        ribbon.rectTransform.pivot = new Vector2(0.5f, 1f);
        ribbon.rectTransform.anchoredPosition = new Vector2(30f, 0f);
        ribbon.rectTransform.sizeDelta = new Vector2(26f, 90f);
        ribbon.raycastTarget = false;

        var text = NewText("Texto", rt, font, 30f, new Color(0.98f, 0.9f, 0.72f), FontStyles.Bold, new Vector2(12f, 0f), new Vector2(200f, 60f));
        text.text = "Grimorio";
        text.gameObject.AddComponent<GrimorioBotonTexto>();
    }

    // ── Utilidades de UI ──────────────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, FontStyles style, Vector2 pos, Vector2 box)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.text = "";
        Place(rt, pos, box);
        return t;
    }

    private static Button NewButton(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var img = NewImage(name, parent, sprite, Color.white);
        img.raycastTarget = true;
        img.preserveAspect = true;
        Place(img.rectTransform, pos, size);
        var b = img.gameObject.AddComponent<Button>();
        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        return b;
    }

    /// Fila centrada que coloca a sus hijos según su tamaño (los que están apagados no ocupan sitio).
    private static RectTransform NewRow(string name, Transform parent, Vector2 pos, Vector2 size, float spacing, TextAnchor align)
    {
        var rt = NewRect(name, parent);
        Place(rt, pos, size);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return rt;
    }

    /// Columna para dentro de una fila: sus hijos uno debajo de otro, centrados.
    private static RectTransform NewColumn(string name, Transform parent, float spacing)
    {
        var rt = NewRect(name, parent);
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        return rt;
    }

    /// Etiqueta redondeada con texto en mayúsculas; el color lo pone GrimorioLibroUI.
    private static (Image, TextMeshProUGUI) NewPill(string name, Transform parent, TMP_FontAsset font, float size)
    {
        var bg = NewImage(name, parent, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), Ink);
        bg.type = Image.Type.Sliced;
        var h = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(22, 22, 6, 6);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        var t = NewText("Texto", bg.rectTransform, font, size, new Color(1f, 0.97f, 0.9f), FontStyles.Bold | FontStyles.UpperCase, Vector2.zero, new Vector2(200f, 34f));
        t.characterSpacing = 4f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return (bg, t);
    }

    private static void AutoSize(TextMeshProUGUI t, float min)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = min;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        if (rt.pivot == Vector2.zero) rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject so, string field, Object[] values)
    {
        var p = so.FindProperty(field);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
