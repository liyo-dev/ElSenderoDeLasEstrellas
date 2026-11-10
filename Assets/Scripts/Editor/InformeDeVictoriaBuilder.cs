using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Construye el prefab del informe de victoria (Resources/UI/InformeDeVictoria) con el aspecto de
/// la maqueta aprobada (Claude outputs/victoria-informe-maqueta): panel de cristal de los menús,
/// medallón con el aro lavanda y el retrato vivo del personaje (recortado en círculo, con el fijo
/// de la ficha debajo como respaldo), la fila de Sintonía Estelar con la barra del HUD, pastilla
/// dorada y aviso de hito, botín con el marco dorado del grimorio y «Continuar». Deja además los
/// sprites nuevos importados como Sprite y el retrato de cada ficha de personaje puesto.
/// Idempotente: rehace el prefab entero cada vez. Ver INC-543 e INC-950.
/// </summary>
public static class InformeDeVictoriaBuilder
{
    public const string PrefabPath = "Assets/Resources/UI/InformeDeVictoria.prefab";

    private const string Arte = "Assets/Art/UI/InformeDeVictoria/";
    private const string Retratos = "Assets/Art/UI/Retratos/";
    private const string PanelPath = "Assets/Art/UI/Menu/menu_panel_glass.png";
    private const string AroPath = "Assets/Art/UI/Menu/menu_window_ring.png";
    private const string BarraFondoPath = "Assets/Art/UI/HUD/hp_bar_bg.png";
    private const string BarraVidaPath = "Assets/Art/UI/HUD/hp_bar_fill.png";
    private const string BarraMagiaPath = "Assets/Art/UI/HUD/mp_bar_fill.png";
    private const string EstrellaPath = "Assets/Art/UI/HUD/estrella_equipo.png";
    private const string MarcoPath = "Assets/Art/UI/Grimorio/marco_icono.png";
    private const string MaterialRetratoVivoPath = "Assets/Art/UI/Retratos/Grupo/RetratoDelGrupo.mat";
    private const string FuenteGruesaPath = "Assets/Plugins/Fonts/Nunito-ExtraBold SDF.asset";
    private const string FuentePath = "Assets/Plugins/Fonts/Nunito-Bold SDF.asset";

    // Medidas del panel (lienzo de referencia 1920×1080), las de la maqueta.
    private const float AnchoPanel = 660f;
    private const float PrimeraFila = 192f;
    private const float AltoDeFila = 94f;
    private const float AnchoFila = 580f;

    private static readonly Color Tinta = new Color32(0xF4, 0xF0, 0xFF, 0xFF);
    private static readonly Color Suave = new Color32(0xB9, 0xB0, 0xD8, 0xFF);
    private static readonly Color Lila = new Color32(0xCB, 0xB9, 0xFF, 0xFF);
    private static readonly Color FondoCirculo = new Color32(0x1D, 0x18, 0x38, 0xFF);
    private static readonly Color TextoPastilla = new Color32(0x3A, 0x24, 0x08, 0xFF);
    private static readonly Color Oro = new Color32(0xFF, 0xD9, 0x8A, 0xFF);

    private static TMP_FontAsset _gruesa, _normal;

    [MenuItem("El Sendero/UI/Crear o regenerar el informe de victoria")]
    public static void Montar()
    {
        var log = new StringBuilder("=== Informe de victoria (INC-543) ===\n");
        var avisos = new List<string>();

        PrepararSprites(log);
        PonerRetratosEnLasFichas(log, avisos);

        _gruesa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FuenteGruesaPath);
        _normal = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FuentePath);
        if (_gruesa == null || _normal == null) avisos.Add("No encuentro las fuentes Nunito SDF en Assets/Plugins/Fonts.");

        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        var raiz = Construir(avisos);
        try
        {
            PildorasProporcionadasBuilder.AplicarEn(raiz, PrefabPath, null);
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath);
            log.AppendLine("   Prefab: " + PrefabPath);
        }
        finally { Object.DestroyImmediate(raiz); }

        AssetDatabase.SaveAssets();
        if (avisos.Count == 0) { log.AppendLine("Sin avisos."); Debug.Log(log.ToString()); }
        else
        {
            foreach (var a in avisos) log.AppendLine("  • " + a);
            Debug.LogWarning(log.ToString());
        }
        EditorUtility.DisplayDialog("Informe de victoria", avisos.Count == 0
            ? "Listo. Gana una batalla (el Demonio 1) para verlo."
            : "Hecho con avisos; mira la consola.", "OK");
    }

    // ── Sprites y fichas ────────────────────────────────────────────────────────

    private static void PrepararSprites(StringBuilder log)
    {
        var bordes = new Dictionary<string, Vector4>
        {
            { Arte + "pastilla.png", new Vector4(18, 0, 18, 0) },
            { Arte + "pastilla_oro.png", new Vector4(18, 0, 18, 0) },
        };
        var rutas = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Arte.TrimEnd('/'), Retratos.TrimEnd('/') }))
            rutas.Add(AssetDatabase.GUIDToAssetPath(guid));

        int cambiados = 0;
        foreach (var ruta in rutas)
        {
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp == null) continue;
            var borde = bordes.TryGetValue(ruta, out var b) ? b : Vector4.zero;
            if (imp.textureType == TextureImporterType.Sprite && imp.spriteBorder == borde && !imp.mipmapEnabled) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spriteBorder = borde;
            imp.SaveAndReimport();
            cambiados++;
        }
        log.AppendLine($"   Sprites nuevos listos ({rutas.Count}, {cambiados} reimportados).");
    }

    private static void PonerRetratosEnLasFichas(StringBuilder log, List<string> avisos)
    {
        var retratos = new Dictionary<Slot, string>
        {
            { Slot.Will, Retratos + "retrato_will.png" },
            { Slot.Estela, Retratos + "retrato_estela.png" },
            { Slot.Liam, Retratos + "retrato_liam.png" },
        };
        int puestos = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:FichaDePersonaje"))
        {
            var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(AssetDatabase.GUIDToAssetPath(guid));
            if (ficha == null || !retratos.TryGetValue(ficha.Personaje, out var ruta)) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (sprite == null) { avisos.Add("No encuentro " + ruta); continue; }
            var so = new SerializedObject(ficha);
            so.FindProperty("retrato").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ficha);
            puestos++;
        }
        log.AppendLine($"   Retrato puesto en {puestos} ficha(s) de personaje.");
        if (puestos < 3) avisos.Add("No he encontrado las tres fichas de personaje (Will, Estela, Liam).");
    }

    // ── Prefab ──────────────────────────────────────────────────────────────────

    private static GameObject Construir(List<string> avisos)
    {
        var raiz = new GameObject("InformeDeVictoria", typeof(RectTransform));
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = raiz.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var grupo = raiz.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        var informe = raiz.AddComponent<InformeDeVictoriaUI>();

        // Panel de cristal, a la derecha y centrado en alto.
        var panel = Imagen("Panel", raiz.transform, Cargar(PanelPath, avisos), Color.white, Image.Type.Sliced);
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(1f, 0.5f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.sizeDelta = new Vector2(AnchoPanel, 840f);
        panelRt.anchoredPosition = new Vector2(-90f, 10f);
        var sombra = panel.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0.04f, 0.02f, 0.12f, 0.55f);
        sombra.effectDistance = new Vector2(0f, -12f);

        // Medallón: retrato grande con el aro lavanda, asomando por la esquina.
        var medallon = Vacio("Medallon", panelRt, new Vector2(33f, -21f), new Vector2(180f, 180f), new Vector2(0.5f, 0.5f));
        Imagen("Fondo", medallon, Cargar(Arte + "circulo.png", avisos), FondoCirculo).rectTransform.sizeDelta = new Vector2(152f, 152f);
        var retrato = Imagen("Retrato", medallon, null, Color.white);
        retrato.rectTransform.sizeDelta = new Vector2(152f, 152f);
        retrato.preserveAspect = true;
        var materialVivo = AssetDatabase.LoadAssetAtPath<Material>(MaterialRetratoVivoPath);
        if (materialVivo == null) avisos.Add("No encuentro " + MaterialRetratoVivoPath + " (los retratos vivos saldrían con fondo negro).");
        var retratoVivo = RetratoVivo(medallon, 148f, 176f, -10f, materialVivo, avisos);
        Imagen("Aro", medallon, Cargar(AroPath, avisos), Color.white).rectTransform.sizeDelta = new Vector2(196f, 196f);

        // Compañeros de la foto, en pequeño junto al medallón.
        var companeros = new Image[2];
        var companerosVivos = new RawImage[2];
        for (int i = 0; i < 2; i++)
        {
            var mini = Vacio("Companero" + (i + 1), panelRt, new Vector2(93f + 63f * i, -79f), new Vector2(60f, 60f), new Vector2(0.5f, 0.5f));
            Imagen("Fondo", mini, Cargar(Arte + "circulo.png", avisos), FondoCirculo).rectTransform.sizeDelta = new Vector2(56f, 56f);
            companeros[i] = Imagen("Retrato", mini, null, Color.white);
            companeros[i].rectTransform.sizeDelta = new Vector2(54f, 54f);
            companeros[i].preserveAspect = true;
            companerosVivos[i] = RetratoVivo(mini, 54f, 64f, -4f, materialVivo, avisos);
            Imagen("Aro", mini, Cargar(Arte + "aro_fino.png", avisos), PaletaUI.Lavanda).rectTransform.sizeDelta = new Vector2(60f, 60f);
        }

        // Cabecera.
        var titulo = Texto("Titulo", panelRt, _gruesa, 66f, Tinta, TextAlignmentOptions.Center, new Vector2(350f, -60f), new Vector2(440f, 84f));
        titulo.text = "¡Victoria!";
        var subtitulo = Texto("Subtitulo", panelRt, _normal, 21f, Lila, TextAlignmentOptions.Center, new Vector2(350f, -113f), new Vector2(460f, 30f));
        subtitulo.fontStyle = FontStyles.UpperCase;
        subtitulo.characterSpacing = 12f;
        var estrella = Cargar(EstrellaPath, avisos);
        var dorada = new Color(1f, 0.93f, 0.72f, 1f);
        Imagen("Chispa1", panelRt, estrella, dorada, pos: new Vector2(196f, -28f), tam: new Vector2(30f, 30f), anclaArribaIzq: true);
        Imagen("Chispa2", panelRt, estrella, dorada, pos: new Vector2(560f, -58f), tam: new Vector2(24f, 24f), anclaArribaIzq: true);

        var sepProgreso = Separador("SeparadorProgreso", panelRt, -162f, avisos);

        // Fila de Sintonía Estelar: estrella, nombre, «actual / umbral», barra, pastilla «+N» y aviso de hito.
        var fila = Vacio("FilaSintonia", panelRt, new Vector2(40f, -PrimeraFila), new Vector2(AnchoFila, 84f), new Vector2(0f, 1f));
        Imagen("FondoIcono", fila, Cargar(Arte + "circulo.png", avisos), FondoCirculo, pos: new Vector2(23f, -42f), tam: new Vector2(46f, 46f), anclaArribaIzq: true);
        Imagen("AroIcono", fila, Cargar(Arte + "aro_fino.png", avisos), new Color(PaletaUI.Lavanda.r, PaletaUI.Lavanda.g, PaletaUI.Lavanda.b, 0.8f), pos: new Vector2(23f, -42f), tam: new Vector2(46f, 46f), anclaArribaIzq: true);
        Imagen("Icono", fila, Cargar(EstrellaPath, avisos), Oro, pos: new Vector2(23f, -42f), tam: new Vector2(32f, 32f), anclaArribaIzq: true);

        var nombreSintonia = Texto("Nombre", fila, _gruesa, 27f, Tinta, TextAlignmentOptions.Left, new Vector2(60f, -2f), new Vector2(300f, 36f), new Vector2(0f, 1f));
        nombreSintonia.text = "Sintonía Estelar";
        var valores = Texto("Valores", fila, _gruesa, 27f, Tinta, TextAlignmentOptions.Right, new Vector2(0f, -2f), new Vector2(220f, 36f), new Vector2(1f, 1f), ancla: new Vector2(1f, 1f));

        var barra = Imagen("Barra", fila, Cargar(BarraFondoPath, avisos), Color.white, Image.Type.Sliced, new Vector2(60f, -50f), new Vector2(AnchoFila - 60f - 96f, 22f), anclaArribaIzq: true, pivote: new Vector2(0f, 1f));
        var relleno = Cargar(BarraMagiaPath, avisos);
        var colorSintonia = new Color(0.82f, 0.68f, 1f, 1f);
        var gana = Relleno("Ganado", barra.rectTransform, relleno, Color.Lerp(Oro, Color.white, 0.25f));
        var baseImg = Relleno("Antes", barra.rectTransform, relleno, colorSintonia);

        var pastilla = Imagen("Pastilla", fila, Cargar(Arte + "pastilla_oro.png", avisos), Color.white, Image.Type.Sliced, new Vector2(-40f, -61f), new Vector2(84f, 34f), ancla: new Vector2(1f, 1f));
        var brillo = pastilla.gameObject.AddComponent<Shadow>();
        brillo.effectColor = new Color(1f, 0.82f, 0.48f, 0.45f);
        brillo.effectDistance = new Vector2(0f, -2f);
        var textoPastilla = Texto("Texto", pastilla.rectTransform, _gruesa, 22f, TextoPastilla, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
        Estirar(textoPastilla.rectTransform);
        textoPastilla.text = "+100";

        var aviso = Texto("Aviso", fila, _gruesa, 21f, Oro, TextAlignmentOptions.Left, new Vector2(60f, -96f), new Vector2(AnchoFila - 60f, 30f), new Vector2(0f, 0.5f));
        aviso.text = "¡Sintonía completa! Elige más Vida o más Magia en Equipo";

        // Botín: separador y hasta cuatro huecos con el marco dorado.
        var botin = Vacio("Botin", panelRt, new Vector2(0f, -(PrimeraFila + AltoDeFila)), new Vector2(AnchoPanel, 200f), new Vector2(0f, 1f));
        var sepBotin = Separador("SeparadorBotin", botin, -18f, avisos);
        var huecos = new List<(RectTransform raiz, Image icono, TMP_Text cantidad, TMP_Text nombre)>();
        for (int i = 0; i < 4; i++)
        {
            var hueco = Vacio("Hueco" + (i + 1), botin, new Vector2(106f + 150f * i, -98f), new Vector2(132f, 140f), new Vector2(0.5f, 0.5f));
            Imagen("Fondo", hueco, Cargar(Arte + "circulo.png", avisos), new Color32(0x16, 0x12, 0x2C, 0xFF), pos: new Vector2(0f, 18f), tam: new Vector2(78f, 78f));
            var icono = Imagen("Icono", hueco, null, Color.white, pos: new Vector2(0f, 18f), tam: new Vector2(60f, 60f));
            icono.preserveAspect = true;
            Imagen("Marco", hueco, Cargar(MarcoPath, avisos), Color.white, pos: new Vector2(0f, 18f), tam: new Vector2(98f, 98f));
            var chapa = Imagen("Cantidad", hueco, Cargar(Arte + "pastilla.png", avisos), new Color32(0x2A, 0x23, 0x50, 0xFF), Image.Type.Sliced, new Vector2(32f, -14f), new Vector2(62f, 30f));
            var cantidad = Texto("Texto", chapa.rectTransform, _gruesa, 20f, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
            Estirar(cantidad.rectTransform);
            var nombre = Texto("Nombre", hueco, _normal, 18f, Suave, TextAlignmentOptions.Center, new Vector2(0f, -52f), new Vector2(150f, 26f),
                ancla: new Vector2(0.5f, 0.5f));
            huecos.Add((hueco, icono, cantidad, nombre));
        }

        // Continuar, abajo a la derecha.
        var continuar = Texto("Continuar", panelRt, _normal, 22f, Suave, TextAlignmentOptions.Right, new Vector2(-44f, 30f), new Vector2(320f, 36f), new Vector2(1f, 0f), ancla: new Vector2(1f, 0f));
        continuar.text = "Continuar";

        // Referencias del componente.
        var so = new SerializedObject(informe);
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("panel").objectReferenceValue = panelRt;
        so.FindProperty("titulo").objectReferenceValue = titulo;
        so.FindProperty("subtitulo").objectReferenceValue = subtitulo;
        so.FindProperty("separadorProgreso").objectReferenceValue = sepProgreso;
        so.FindProperty("retrato").objectReferenceValue = retrato;
        so.FindProperty("retratoVivo").objectReferenceValue = retratoVivo;
        var pComp = so.FindProperty("companeros");
        pComp.arraySize = companeros.Length;
        for (int i = 0; i < companeros.Length; i++) pComp.GetArrayElementAtIndex(i).objectReferenceValue = companeros[i];
        var pCompVivos = so.FindProperty("companerosVivos");
        pCompVivos.arraySize = companerosVivos.Length;
        for (int i = 0; i < companerosVivos.Length; i++) pCompVivos.GetArrayElementAtIndex(i).objectReferenceValue = companerosVivos[i];

        var pSintonia = so.FindProperty("sintonia");
        pSintonia.FindPropertyRelative("raiz").objectReferenceValue = fila;
        pSintonia.FindPropertyRelative("nombre").objectReferenceValue = nombreSintonia;
        pSintonia.FindPropertyRelative("valores").objectReferenceValue = valores;
        pSintonia.FindPropertyRelative("barraBase").objectReferenceValue = baseImg;
        pSintonia.FindPropertyRelative("barraGanada").objectReferenceValue = gana;
        pSintonia.FindPropertyRelative("pastilla").objectReferenceValue = pastilla.rectTransform;
        pSintonia.FindPropertyRelative("textoPastilla").objectReferenceValue = textoPastilla;
        pSintonia.FindPropertyRelative("aviso").objectReferenceValue = aviso;

        so.FindProperty("bloqueBotin").objectReferenceValue = botin;
        so.FindProperty("separadorBotin").objectReferenceValue = sepBotin;
        var pHuecos = so.FindProperty("huecos");
        pHuecos.arraySize = huecos.Count;
        for (int i = 0; i < huecos.Count; i++)
        {
            var e = pHuecos.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("raiz").objectReferenceValue = huecos[i].raiz;
            e.FindPropertyRelative("icono").objectReferenceValue = huecos[i].icono;
            e.FindPropertyRelative("cantidad").objectReferenceValue = huecos[i].cantidad;
            e.FindPropertyRelative("nombre").objectReferenceValue = huecos[i].nombre;
        }
        so.FindProperty("continuar").objectReferenceValue = continuar;
        so.FindProperty("primeraFila").floatValue = PrimeraFila;
        so.FindProperty("altoDeFila").floatValue = AltoDeFila;
        so.ApplyModifiedPropertiesWithoutUndo();

        return raiz;
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────

    /// Texto con una línea que se desvanece a cada lado.
    private static TMP_Text Separador(string nombre, RectTransform padre, float y, List<string> avisos)
    {
        var linea = Cargar(Arte + "linea.png", avisos);
        var color = new Color(PaletaUI.Lavanda.r, PaletaUI.Lavanda.g, PaletaUI.Lavanda.b, 0.75f);
        Imagen(nombre + "LineaIzq", padre, linea, color, pos: new Vector2(135f, y), tam: new Vector2(180f, 2f), anclaArribaIzq: true);
        Imagen(nombre + "LineaDer", padre, linea, color, pos: new Vector2(AnchoPanel - 135f, y), tam: new Vector2(180f, 2f), anclaArribaIzq: true);
        var t = Texto(nombre, padre, _normal, 18f, Suave, TextAlignmentOptions.Center, new Vector2(AnchoPanel * 0.5f, y), new Vector2(220f, 26f));
        t.fontStyle = FontStyles.UpperCase;
        t.characterSpacing = 14f;
        return t;
    }

    /// Retrato vivo recortado en círculo: máscara redonda de 'diametro' y, dentro, la imagen del
    /// estudio algo mayor y bajada para que el corte del busto quede fuera del círculo.
    private static RawImage RetratoVivo(RectTransform padre, float diametro, float lado, float bajada, Material material, List<string> avisos)
    {
        var mascara = Imagen("Mascara", padre, Cargar(Arte + "circulo.png", avisos), Color.white);
        mascara.rectTransform.sizeDelta = new Vector2(diametro, diametro);
        mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var go = new GameObject("RetratoVivo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(mascara.rectTransform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(lado, lado);
        rt.anchoredPosition = new Vector2(0f, bajada);
        var raw = go.GetComponent<RawImage>();
        raw.material = material;
        raw.raycastTarget = false;
        raw.enabled = false;
        return raw;
    }

    private static Image Relleno(string nombre, RectTransform barra, Sprite sprite, Color color)
    {
        var img = Imagen(nombre, barra, sprite, color);
        Estirar(img.rectTransform, 4f);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 0.5f;
        return img;
    }

    private static RectTransform Vacio(string nombre, Transform padre, Vector2 pos, Vector2 tam, Vector2 pivote)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivote;
        rt.sizeDelta = tam;
        rt.anchoredPosition = pos;
        return rt;
    }

    /// Imagen centrada en su padre salvo que se pida otra cosa. Con 'anclaArribaIzq' la posición
    /// se cuenta desde la esquina de arriba a la izquierda del padre.
    private static Image Imagen(string nombre, Transform padre, Sprite sprite, Color color,
        Image.Type tipo = Image.Type.Simple, Vector2? pos = null, Vector2? tam = null,
        bool anclaArribaIzq = false, Vector2? ancla = null, Vector2? pivote = null)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        Vector2 a = ancla ?? (anclaArribaIzq ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f));
        rt.anchorMin = rt.anchorMax = a;
        rt.pivot = pivote ?? new Vector2(0.5f, 0.5f);
        if (tam.HasValue) rt.sizeDelta = tam.Value;
        if (pos.HasValue) rt.anchoredPosition = pos.Value;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = tipo;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Texto(string nombre, Transform padre, TMP_FontAsset fuente, float tamano, Color color,
        TextAlignmentOptions alineado, Vector2 pos, Vector2 tam, Vector2? pivote = null, Vector2? ancla = null)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = ancla ?? new Vector2(0f, 1f);
        rt.pivot = pivote ?? new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tam;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = alineado;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Estirar(RectTransform rt, float margen = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margen, margen);
        rt.offsetMax = new Vector2(-margen, -margen);
    }

    private static Sprite Cargar(string ruta, List<string> avisos)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (s == null && !avisos.Contains("No encuentro el sprite " + ruta)) avisos.Add("No encuentro el sprite " + ruta);
        return s;
    }
}
