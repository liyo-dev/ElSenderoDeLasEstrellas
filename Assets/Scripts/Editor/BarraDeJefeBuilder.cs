using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye el prefab de la barra de vida de los jefes (Resources/UI/BarraDeJefe) con el arte
/// del HUD: la pastilla lavanda con borde dorado (hp_bar_bg), el relleno dorado (hp_bar_fill,
/// teñido por BarraDeJefeUI) y las fuentes Nunito con contorno oscuro. Idempotente: rehace el
/// prefab entero cada vez. Ver INC-645.
/// </summary>
public static class BarraDeJefeBuilder
{
    public const string PrefabPath = "Assets/Resources/UI/BarraDeJefe.prefab";

    private const string FondoPath = "Assets/Art/UI/HUD/hp_bar_bg.png";
    private const string RellenoPath = "Assets/Art/UI/HUD/hp_bar_fill.png";
    private const string FuenteGruesaPath = "Assets/Plugins/Fonts/Nunito-ExtraBold SDF.asset";
    private const string FuentePath = "Assets/Plugins/Fonts/Nunito-Bold SDF.asset";
    private const string CarpetaMateriales = "Assets/Art/UI/HUD/";

    private const float AnchoBarra = 620f;
    private const float AltoBarra = 48f;
    private const float AltoNombre = 34f;
    private const float MargenRelleno = 6f;
    // Alto del sprite de la pastilla: con él se escalan sus bordes de 40 px para que los extremos
    // redondeados no se deformen en una barra más baja.
    private const float AltoSprite = 80f;

    private static readonly Color Crema = new Color32(0xFF, 0xF4, 0xDC, 0xFF);

    [InitializeOnLoadMethod]
    private static void AutoRegenerarSiEsNecesario()
    {
        EditorApplication.delayCall += () =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                ConstruirYGuardar(silencioso: true);
                return;
            }
            var ui = prefab.GetComponent<BarraDeJefeUI>();
            if (ui == null || !ui.TieneMascaraConfigurada)
            {
                ConstruirYGuardar(silencioso: true);
            }
        };
    }

    [MenuItem("El Sendero/UI/Crear o regenerar la barra de jefe (INC-645)")]
    public static void Montar()
    {
        ConstruirYGuardar(silencioso: false);
    }

    [MenuItem("El Sendero/UI/Regenerar barra de jefe (silencioso)")]
    public static void MontarSilencioso()
    {
        ConstruirYGuardar(silencioso: true);
    }

    public static void ConstruirYGuardar(bool silencioso)
    {
        var log = new StringBuilder("=== Barra de jefe (INC-645) ===\n");
        var avisos = new List<string>();

        var gruesa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FuenteGruesaPath);
        var normal = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FuentePath);
        if (gruesa == null || normal == null) avisos.Add("No encuentro las fuentes Nunito SDF en Assets/Plugins/Fonts.");

        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        var raiz = Construir(gruesa, normal, avisos);
        try
        {
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
        if (!silencioso)
        {
            EditorUtility.DisplayDialog("Barra de jefe", avisos.Count == 0
                ? "Listo. Entra en una pelea de jefe para verla."
                : "Hecho con avisos; mira la consola.", "OK");
        }
    }

    private static void AsegurarBordesDeSprite(string ruta, Vector4 bordes)
    {
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer == null) return;
        if (importer.spriteBorder != bordes)
        {
            importer.spriteBorder = bordes;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    private static GameObject Construir(TMP_FontAsset gruesa, TMP_FontAsset normal, List<string> avisos)
    {
        AsegurarBordesDeSprite(RellenoPath, new Vector4(40f, 0f, 40f, 0f));

        var raiz = new GameObject("BarraDeJefe", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = raiz.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Contenedor centro-superior: nombre arriba y la pastilla debajo.
        var contenedorGo = new GameObject("Contenedor", typeof(RectTransform), typeof(CanvasGroup));
        var contenedor = (RectTransform)contenedorGo.transform;
        contenedor.SetParent(raiz.transform, false);
        contenedor.anchorMin = contenedor.anchorMax = new Vector2(0.5f, 1f);
        contenedor.pivot = new Vector2(0.5f, 1f);
        contenedor.sizeDelta = new Vector2(AnchoBarra, AltoNombre + AltoBarra);
        contenedor.anchoredPosition = new Vector2(0f, -30f);
        var grupo = contenedorGo.GetComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        var nombre = Texto("Nombre", contenedor, gruesa, 28f, Crema);
        var rtNombre = nombre.rectTransform;
        rtNombre.anchorMin = new Vector2(0f, 1f);
        rtNombre.anchorMax = new Vector2(1f, 1f);
        rtNombre.pivot = new Vector2(0.5f, 1f);
        rtNombre.sizeDelta = new Vector2(0f, AltoNombre);
        rtNombre.anchoredPosition = Vector2.zero;
        nombre.characterSpacing = 4f;
        nombre.text = "Jefe";

        var fondo = Imagen("Fondo", contenedor, Cargar(FondoPath, avisos), Color.white);
        fondo.type = Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = AltoSprite / AltoBarra;
        var rtFondo = fondo.rectTransform;
        rtFondo.anchorMin = new Vector2(0f, 0f);
        rtFondo.anchorMax = new Vector2(1f, 0f);
        rtFondo.pivot = new Vector2(0.5f, 0f);
        rtFondo.sizeDelta = new Vector2(0f, AltoBarra);
        rtFondo.anchoredPosition = Vector2.zero;
        fondo.gameObject.AddComponent<BordesProporcionales>();

        // Área interior donde vive el relleno (inset MargenRelleno).
        float anchoRelleno = AnchoBarra - 2f * MargenRelleno;
        float altoRelleno = AltoBarra - 2f * MargenRelleno;

        var areaRellenoGo = new GameObject("AreaRelleno", typeof(RectTransform));
        var rtArea = (RectTransform)areaRellenoGo.transform;
        rtArea.SetParent(fondo.transform, false);
        Estirar(rtArea, MargenRelleno);

        // Máscara rectangular que recorta el relleno según la vida sin deformar sus extremos.
        var mascaraGo = new GameObject("MascaraRelleno", typeof(RectTransform), typeof(RectMask2D));
        var rtMascara = (RectTransform)mascaraGo.transform;
        rtMascara.SetParent(rtArea, false);
        rtMascara.anchorMin = new Vector2(0f, 0f);
        rtMascara.anchorMax = new Vector2(1f, 1f);
        rtMascara.pivot = new Vector2(0f, 0.5f);
        rtMascara.offsetMin = Vector2.zero;
        rtMascara.offsetMax = Vector2.zero;

        // Relleno: pastilla Sliced con BordesProporcionales, fija al ancho completo del área.
        var relleno = Imagen("Relleno", rtMascara, Cargar(RellenoPath, avisos), Color.white);
        relleno.type = Image.Type.Sliced;
        relleno.pixelsPerUnitMultiplier = AltoSprite / altoRelleno;
        var rtRelleno = relleno.rectTransform;
        rtRelleno.anchorMin = new Vector2(0f, 0f);
        rtRelleno.anchorMax = new Vector2(0f, 1f);
        rtRelleno.pivot = new Vector2(0f, 0.5f);
        rtRelleno.anchoredPosition = Vector2.zero;
        rtRelleno.sizeDelta = new Vector2(anchoRelleno, 0f);
        relleno.gameObject.AddComponent<BordesProporcionales>();

        // Las marcas de fase comparten el rectángulo del área para caer en su porcentaje exacto.
        var capaGo = new GameObject("Marcas", typeof(RectTransform));
        var capa = (RectTransform)capaGo.transform;
        capa.SetParent(fondo.transform, false);
        Estirar(capa, MargenRelleno);

        var marca = Imagen("PlantillaMarca", capa, null, Color.black);
        var rtMarca = marca.rectTransform;
        rtMarca.anchorMin = new Vector2(0f, 0f);
        rtMarca.anchorMax = new Vector2(0f, 1f);
        rtMarca.pivot = new Vector2(0.5f, 0.5f);
        rtMarca.sizeDelta = new Vector2(4f, 6f); // sobresale un poco por arriba y por abajo
        marca.gameObject.SetActive(false);

        var vida = Texto("Vida", fondo.transform, normal, 18f, Color.white);
        Estirar(vida.rectTransform);

        var ui = raiz.AddComponent<BarraDeJefeUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("contenedor").objectReferenceValue = contenedor;
        so.FindProperty("mascaraRelleno").objectReferenceValue = rtMascara;
        so.FindProperty("relleno").objectReferenceValue = relleno;
        so.FindProperty("nombre").objectReferenceValue = nombre;
        so.FindProperty("vida").objectReferenceValue = vida;
        so.FindProperty("capaDeMarcas").objectReferenceValue = capa;
        so.FindProperty("plantillaDeMarca").objectReferenceValue = marca;
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────

    /// Material de la fuente con contorno y sombra oscuros (lavanda muy oscura), para que el
    /// texto se lea sobre el relleno dorado y sobre cualquier fondo de escena. Uno por fuente,
    /// porque cada material va atado al atlas de su fuente.
    private static Material MaterialDeContorno(TMP_FontAsset fuente)
    {
        string ruta = CarpetaMateriales + "BarraDeJefe_Contorno_" + fuente.name.Replace(" ", "") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(fuente.material);
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.EnableKeyword("OUTLINE_ON");
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_OutlineColor", new Color(0.13f, 0.09f, 0.26f, 1f));
        material.SetFloat("_OutlineWidth", 0.2f);
        material.SetColor("_UnderlayColor", new Color(0.04f, 0.02f, 0.12f, 0.7f));
        material.SetFloat("_UnderlayOffsetX", 0.6f);
        material.SetFloat("_UnderlayOffsetY", -0.8f);
        material.SetFloat("_UnderlaySoftness", 0.5f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Image Imagen(string nombre, Transform padre, Sprite sprite, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Texto(string nombre, Transform padre, TMP_FontAsset fuente, float tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null)
        {
            t.font = fuente;
            t.fontSharedMaterial = MaterialDeContorno(fuente);
        }
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
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
        if (s == null) avisos.Add("No encuentro el sprite " + ruta);
        return s;
    }
}
