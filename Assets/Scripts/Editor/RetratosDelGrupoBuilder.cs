using System.IO;
using System.Text;
using Core.InputGlyphs;
using Sendero.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Monta los retratos de busto del grupo en el HUD de Start.unity (ver TDD «Retratos del grupo en
/// el HUD»): el contenedor con RectMask2D cuyo borde inferior es el borde superior del panel de
/// estado, un <see cref="RetratoDelGrupo"/> por personaje, el nombre del que va al mando, la luz
/// del borde, el aviso del trío y el estado (<see cref="EstadoDelGrupoParaRetratos"/>). Crea
/// también el material, el halo, la línea de luz y el juego de retratos provisional (las
/// imágenes de cuerpo entero de siempre, recortadas por el borde), y pone el color de cada ficha
/// de personaje si no lo tiene. Quita los retratos antiguos del panel.
///
/// Idempotente: lo que ya existe se reutiliza y se recoloca; no duplica nada.
/// </summary>
public static class RetratosDelGrupoBuilder
{
    private const string StartPath = "Assets/Scenes/Systems/Start.unity";
    private const string Carpeta = "Assets/Art/UI/Retratos/Grupo";
    private const string RutaMaterial = Carpeta + "/RetratoDelGrupo.mat";
    private const string RutaHalo = Carpeta + "/halo_retrato.png";
    private const string RutaLinea = Carpeta + "/linea_retrato.png";
    /// Juego de retratos del grupo: se crea con las imágenes provisionales y «Hornear retratos del grupo» lo rellena con las fotos de los modelos.
    public const string RutaJuego = Carpeta + "/Retratos_DelGrupo.asset";
    private const string NombreContenedor = "Retratos";

    private const float AltoContenedor = 420f;
    private const float AltoVisible = 300f;
    private const float Separacion = 290f;
    private const float FraccionProvisional = 0.40f;

    private static readonly string[] SpritesProvisionales =
    {
        "Assets/Art/UI/HUD/liam_hud.png",
        "Assets/Art/UI/HUD/will_hud.png",
        "Assets/Art/UI/HUD/estela_hud.png",
    };
    private static readonly string[] Nombres = { "Liam", "Will", "Estela" };
    private static readonly Color[] Colores =
    {
        new Color(0.482f, 0.749f, 0.353f, 1f),
        new Color(1f, 0.604f, 0.235f, 1f),
        new Color(0.310f, 0.553f, 1f, 1f),
    };

    [MenuItem("El Sendero/UI/Montar retratos del grupo (INC-719)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El montaje de los retratos del grupo se ejecuta fuera de Play."); return; }

        var log = new StringBuilder("=== Retratos del grupo (INC-719) ===\n");
        CrearCarpeta(Carpeta);
        var halo = AsegurarSprite(RutaHalo, CrearHalo, log);
        var linea = AsegurarSprite(RutaLinea, CrearLinea, log);
        var material = AsegurarMaterial(log);
        if (material == null) return;
        var juego = AsegurarJuegoProvisional(log);
        ColorearFichas(log);

        var scene = SceneManager.GetSceneByPath(StartPath);
        bool abierta = !scene.isLoaded;
        if (abierta) scene = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Additive);
        try
        {
            if (!MontarEnEscena(scene, material, halo, linea, juego, log)) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            log.AppendLine("Start.unity guardada.");
            Debug.Log(log.ToString());
        }
        finally
        {
            if (abierta) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // ─── Escena ────────────────────────────────────────────────────────────

    private static bool MontarEnEscena(Scene scene, Material material, Sprite halo, Sprite linea, JuegoDeRetratos juego, StringBuilder log)
    {
        Transform canvas = null;
        foreach (var raiz in scene.GetRootGameObjects())
            if (raiz.name == "PlayerHUD_Canvas") { canvas = raiz.transform; break; }
        var main = canvas != null ? canvas.Find("Main") : null;
        var personajes = main != null ? main.Find("MainCharacters") as RectTransform : null;
        var bg = main != null ? main.Find("BG") as RectTransform : null;
        // PartyHUDPanel vive en la raíz del canvas del HUD; los retratos, en Main/MainCharacters.
        var panel = canvas != null ? canvas.GetComponentInChildren<PartyHUDPanel>(true) : null;
        if (panel == null || personajes == null || bg == null)
        {
            Debug.LogError("[Retratos del grupo] No encuentro en Start.unity PartyHUDPanel bajo PlayerHUD_Canvas, ni Main/MainCharacters y Main/BG.");
            return false;
        }
        var fuente = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        var fuenteTmp = fuente != null ? fuente.font : null;

        // Borde superior del panel de estado, en el espacio de MainCharacters.
        var esquinas = new Vector3[4];
        bg.GetWorldCorners(esquinas);
        Vector3 bordeLocal = personajes.InverseTransformPoint((esquinas[1] + esquinas[2]) * 0.5f);

        var contenedor = Hijo(personajes, NombreContenedor);
        contenedor.anchorMin = contenedor.anchorMax = new Vector2(0.5f, 1f);
        contenedor.pivot = new Vector2(0.5f, 0f);
        // Con el ancla en la parte de arriba de MainCharacters (que es su pivote), la posición
        // anclada coincide con la local respecto al pivote.
        var anclaLocal = new Vector2((0.5f - personajes.pivot.x) * personajes.rect.width, (1f - personajes.pivot.y) * personajes.rect.height);
        contenedor.anchoredPosition = new Vector2(0f, bordeLocal.y - anclaLocal.y);
        contenedor.sizeDelta = new Vector2(personajes.rect.width, AltoContenedor);
        contenedor.localScale = Vector3.one;
        Asegurar<RectMask2D>(contenedor.gameObject);
        contenedor.SetSiblingIndex(0);

        var retratos = new RetratoDelGrupo[3];
        for (int i = 0; i < 3; i++)
            retratos[i] = MontarRetrato(contenedor, i, material, halo, juego, fuenteTmp);
        retratos[(int)Slot.Will].transform.SetAsLastSibling();

        // Retratos antiguos de cuerpo entero.
        foreach (var viejo in new[] { "PanelCharacterLeft", "PanelCharacterCenter", "PanelCharacterRight" })
        {
            var t = personajes.Find(viejo);
            if (t == null) continue;
            Object.DestroyImmediate(t.gameObject);
            log.AppendLine("Quitado el retrato antiguo " + viejo + ".");
        }

        // Luz del borde y nombre del que va al mando: por encima del marco del panel.
        int detrasDelPanel = bg.GetSiblingIndex() + 1;
        var luz = Hijo(main, "LuzDelBorde", typeof(Image));
        Arriba(luz, new Vector2(800f, 12f), Vector2.zero, new Vector2(0.5f, 0.5f));
        var imgLuz = luz.GetComponent<Image>();
        imgLuz.sprite = linea;
        imgLuz.color = new Color(Colores[1].r, Colores[1].g, Colores[1].b, 0.9f);
        imgLuz.raycastTarget = false;
        luz.SetSiblingIndex(detrasDelPanel);

        var placa = Hijo(main, "NombreDelRetrato", typeof(Image));
        Arriba(placa, new Vector2(240f, 70f), Vector2.zero, new Vector2(0.5f, 0.5f));
        var imgPlaca = placa.GetComponent<Image>();
        imgPlaca.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        imgPlaca.type = Image.Type.Sliced;
        imgPlaca.color = PaletaUI.Lavanda;
        imgPlaca.raycastTarget = false;
        placa.SetSiblingIndex(luz.GetSiblingIndex() + 1);
        var nombre = Texto(placa, "Texto", fuenteTmp, "WILL", 40f, Color.white);
        Estirar((RectTransform)nombre.transform);
        nombre.characterSpacing = 6f;

        var aviso = MontarAvisoDelTrio(main, fuenteTmp);
        aviso.transform.SetAsLastSibling();

        var estado = Asegurar<EstadoDelGrupoParaRetratos>(panel.gameObject);

        var so = new SerializedObject(panel);
        so.FindProperty("retratoDeLiam").objectReferenceValue = retratos[0];
        so.FindProperty("retratoDeWill").objectReferenceValue = retratos[1];
        so.FindProperty("retratoDeEstela").objectReferenceValue = retratos[2];
        so.FindProperty("estado").objectReferenceValue = estado;
        var fuenteActual = so.FindProperty("fuenteDeRetratos");
        if (fuenteActual.objectReferenceValue == null) fuenteActual.objectReferenceValue = juego;
        so.FindProperty("retratosDeRespaldo").objectReferenceValue = juego;
        so.FindProperty("separacionLateral").floatValue = Separacion;
        so.FindProperty("nombreDelActivo").objectReferenceValue = nombre;
        so.FindProperty("lineaDeLuz").objectReferenceValue = imgLuz;
        so.FindProperty("avisoDelTrio").objectReferenceValue = aviso;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("Retratos montados bajo MainCharacters/" + NombreContenedor + " (borde del panel en y = " + bordeLocal.y.ToString("0.#") + ").");
        return true;
    }

    private static RetratoDelGrupo MontarRetrato(RectTransform contenedor, int slot, Material material, Sprite halo, JuegoDeRetratos juego, TMP_FontAsset fuenteTmp)
    {
        var raiz = Hijo(contenedor, "Retrato_" + Nombres[slot]);
        Abajo(raiz, new Vector2(420f, AltoContenedor), new Vector2(slot == 1 ? 0f : (slot == 0 ? -Separacion : Separacion), 0f));
        raiz.localScale = Vector3.one * (slot == 1 ? 1f : 0.68f);

        var rtHalo = Hijo(raiz, "Halo", typeof(Image));
        Abajo(rtHalo, new Vector2(460f, 460f), new Vector2(0f, 170f), new Vector2(0.5f, 0.5f));
        var imgHalo = rtHalo.GetComponent<Image>();
        imgHalo.sprite = halo;
        imgHalo.raycastTarget = false;
        var c = Colores[slot];
        imgHalo.color = new Color(c.r, c.g, c.b, slot == 1 ? 0.75f : 0f);
        rtHalo.SetSiblingIndex(0);

        var cuerpo = Hijo(raiz, "Cuerpo");
        Abajo(cuerpo, Vector2.zero, Vector2.zero);
        var vaiven = Hijo(cuerpo, "Vaiven");
        Abajo(vaiven, Vector2.zero, Vector2.zero);
        var rtImagen = Hijo(vaiven, "Imagen", typeof(RawImage));
        Abajo(rtImagen, new Vector2(300f, 300f), Vector2.zero);
        var raw = rtImagen.GetComponent<RawImage>();
        raw.material = material;
        raw.raycastTarget = false;
        if (juego != null && juego.TryObtener((Slot)slot, ExpresionDeRetrato.Normal, out var img))
        {
            float alto = AltoVisible / Mathf.Max(0.1f, 1f - img.FraccionBajoElBorde);
            raw.texture = img.Textura;
            raw.uvRect = img.Uv;
            rtImagen.sizeDelta = new Vector2(alto * img.Aspecto, alto);
            rtImagen.anchoredPosition = new Vector2(0f, -alto * img.FraccionBajoElBorde);
        }

        var ko = Texto(raiz, "KO", fuenteTmp, "K.O.", 56f, new Color(1f, 0.42f, 0.42f, 1f));
        Abajo((RectTransform)ko.transform, new Vector2(220f, 70f), new Vector2(0f, 24f));
        var loc = Asegurar<LocalizedText>(ko.gameObject);
        var soLoc = new SerializedObject(loc);
        soLoc.FindProperty("_key").stringValue = "HUD_RETRATO_KO";
        soLoc.ApplyModifiedPropertiesWithoutUndo();
        ko.gameObject.SetActive(false);
        ko.transform.SetAsLastSibling();

        var retrato = Asegurar<RetratoDelGrupo>(raiz.gameObject);
        var so = new SerializedObject(retrato);
        so.FindProperty("imagen").objectReferenceValue = raw;
        so.FindProperty("halo").objectReferenceValue = imgHalo;
        so.FindProperty("cuerpo").objectReferenceValue = cuerpo;
        so.FindProperty("vaiven").objectReferenceValue = vaiven;
        so.FindProperty("etiquetaCaido").objectReferenceValue = ko;
        so.FindProperty("altoVisible").floatValue = AltoVisible;
        so.ApplyModifiedPropertiesWithoutUndo();
        return retrato;
    }

    private static CanvasGroup MontarAvisoDelTrio(Transform main, TMP_FontAsset fuenteTmp)
    {
        var aviso = Hijo(main, "AvisoDelTrio", typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        Arriba(aviso, new Vector2(420f, 80f), new Vector2(0f, AltoVisible + 130f), new Vector2(0.5f, 0f));
        var fondo = aviso.GetComponent<Image>();
        fondo.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fondo.type = Image.Type.Sliced;
        fondo.color = new Color(1f, 0.87f, 0.45f, 1f);
        fondo.raycastTarget = false;
        var grupo = aviso.GetComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        var fila = aviso.GetComponent<HorizontalLayoutGroup>();
        fila.padding = new RectOffset(28, 28, 12, 12);
        fila.spacing = 14f;
        fila.childAlignment = TextAnchor.MiddleCenter;
        fila.childControlWidth = fila.childControlHeight = true;
        fila.childForceExpandWidth = fila.childForceExpandHeight = false;
        var ajuste = aviso.GetComponent<ContentSizeFitter>();
        ajuste.horizontalFit = ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var oscuro = new Color(0.23f, 0.15f, 0f, 1f);
        var texto = Texto(aviso, "Texto", fuenteTmp, "¡TRÍO LISTO!", 42f, oscuro);
        var loc = Asegurar<LocalizedText>(texto.gameObject);
        var soLoc = new SerializedObject(loc);
        soLoc.FindProperty("_key").stringValue = "HUD_TRIO_LISTO";
        soLoc.ApplyModifiedPropertiesWithoutUndo();
        texto.transform.SetSiblingIndex(0);
        Glifo(aviso, "LT", InputGlyphNames.TriggerLeft).SetSiblingIndex(1);
        var mas = Texto(aviso, "Mas", fuenteTmp, "+", 42f, oscuro);
        mas.transform.SetSiblingIndex(2);
        Glifo(aviso, "RT", InputGlyphNames.TriggerRight).SetSiblingIndex(3);
        aviso.gameObject.SetActive(false);
        return grupo;
    }

    private static RectTransform Glifo(Transform padre, string nombre, string glifo)
    {
        var rt = Hijo(padre, nombre, typeof(Image), typeof(LayoutElement), typeof(InputGlyphIcon));
        var img = rt.GetComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        var el = rt.GetComponent<LayoutElement>();
        el.preferredWidth = el.preferredHeight = 56f;
        var so = new SerializedObject(rt.GetComponent<InputGlyphIcon>());
        so.FindProperty("glyphName").stringValue = glifo;
        so.FindProperty("icon").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
        return rt;
    }

    // ─── Assets ────────────────────────────────────────────────────────────

    private static Material AsegurarMaterial(StringBuilder log)
    {
        var shader = Shader.Find("UI/RetratoDelGrupo");
        if (shader == null)
        {
            Debug.LogError("[Retratos del grupo] Falta el shader UI/RetratoDelGrupo (Assets/Scripts/Shaders/UI-RetratoDelGrupo.shader).");
            return null;
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (mat == null)
        {
            mat = new Material(shader) { name = "RetratoDelGrupo" };
            AssetDatabase.CreateAsset(mat, RutaMaterial);
            log.AppendLine("Creado " + RutaMaterial + ".");
        }
        else if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); }
        return mat;
    }

    private static JuegoDeRetratos AsegurarJuegoProvisional(StringBuilder log)
    {
        var juego = AssetDatabase.LoadAssetAtPath<JuegoDeRetratos>(RutaJuego);
        if (juego == null)
        {
            juego = ScriptableObject.CreateInstance<JuegoDeRetratos>();
            AssetDatabase.CreateAsset(juego, RutaJuego);
            log.AppendLine("Creado " + RutaJuego + ".");
        }
        var so = new SerializedObject(juego);
        var lista = so.FindProperty("retratos");
        for (int slot = 0; slot < 3; slot++)
        {
            bool esta = false;
            for (int i = 0; i < lista.arraySize; i++)
                if (lista.GetArrayElementAtIndex(i).FindPropertyRelative("personaje").enumValueIndex == slot) { esta = true; break; }
            if (esta) continue;
            lista.arraySize++;
            var e = lista.GetArrayElementAtIndex(lista.arraySize - 1);
            e.FindPropertyRelative("personaje").enumValueIndex = slot;
            e.FindPropertyRelative("normal").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesProvisionales[slot]);
            e.FindPropertyRelative("dolorido").objectReferenceValue = null;
            e.FindPropertyRelative("caido").objectReferenceValue = null;
            e.FindPropertyRelative("contento").objectReferenceValue = null;
            e.FindPropertyRelative("fraccionBajoElBorde").floatValue = FraccionProvisional;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(juego);
        return juego;
    }

    private static void ColorearFichas(StringBuilder log)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:FichaDePersonaje"))
        {
            var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(AssetDatabase.GUIDToAssetPath(guid));
            if (ficha == null || ficha.ColorDelPersonaje.a > 0f) continue;
            var so = new SerializedObject(ficha);
            so.FindProperty("colorDelPersonaje").colorValue = Colores[(int)ficha.Personaje];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ficha);
            log.AppendLine("Color de " + ficha.name + " puesto.");
        }
    }

    private static Sprite AsegurarSprite(string ruta, System.Func<Texture2D> crear, StringBuilder log)
    {
        if (!File.Exists(ruta))
        {
            var tex = crear();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("Creado " + ruta + ".");
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled || !imp.alphaIsTransparency))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    /// Mancha de luz redonda, blanca, que se apaga hacia el borde (se tiñe desde el Image).
    private static Texture2D CrearHalo()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            float a = Mathf.Pow(1f - r, 2.2f);
            px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// Línea fina de luz que se apaga hacia los extremos y hacia arriba y abajo.
    private static Texture2D CrearLinea()
    {
        const int w = 256, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
            float a = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.5f) * Mathf.Pow(Mathf.Sin(Mathf.PI * v), 2f);
            px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ─── Ayudas ────────────────────────────────────────────────────────────

    private static RectTransform Hijo(Transform padre, string nombre, params System.Type[] componentes)
    {
        var t = padre.Find(nombre);
        if (t == null)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.layer = padre.gameObject.layer;
            go.transform.SetParent(padre, false);
            t = go.transform;
        }
        foreach (var c in componentes)
            if (t.GetComponent(c) == null) t.gameObject.AddComponent(c);
        return (RectTransform)t;
    }

    private static T Asegurar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static TextMeshProUGUI Texto(Transform padre, string nombre, TMP_FontAsset fuente, string texto, float tamano, Color color)
    {
        var rt = Hijo(padre, nombre, typeof(TextMeshProUGUI));
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        if (fuente != null) tmp.font = fuente;
        tmp.text = texto;
        tmp.fontSize = tamano;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// Anclado abajo en el centro del padre.
    private static void Abajo(RectTransform rt, Vector2 tamano, Vector2 posicion, Vector2? pivote = null)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = pivote ?? new Vector2(0.5f, 0f);
        rt.sizeDelta = tamano;
        rt.anchoredPosition = posicion;
        rt.localRotation = Quaternion.identity;
    }

    /// Anclado arriba en el centro del padre.
    private static void Arriba(RectTransform rt, Vector2 tamano, Vector2 posicion, Vector2 pivote)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = pivote;
        rt.sizeDelta = tamano;
        rt.anchoredPosition = posicion;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int i = ruta.LastIndexOf('/');
        CrearCarpeta(ruta.Substring(0, i));
        AssetDatabase.CreateFolder(ruta.Substring(0, i), ruta.Substring(i + 1));
    }
}
