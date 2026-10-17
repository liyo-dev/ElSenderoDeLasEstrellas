using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ordena el panel de detalle de las tiendas (Shop_Canvas*): precio en una línea con el icono de la
/// moneda al lado, chip de stock ajustado a su texto y botón de compra centrado con las puntas en
/// proporción. Y las tarjetas de la lista (prefabs con ShopItemCard): el nombre usa todo el ancho
/// libre y, si ocupa dos líneas, crece hacia arriba sin pisar el precio. Las tiendas nuevas se
/// copian de Shop_Canvas, así que salen ya ordenadas. Repetible.
/// </summary>
public static class PanelDeDetalleDeTiendaBuilder
{
    const string Carpeta = "Assets/_SHOPS";

    [MenuItem("El Sendero/UI/Tiendas · ordenar el panel de detalle y las tarjetas")]
    public static void Ordenar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El panel de las tiendas se ordena fuera de Play."); return; }
        var hechos = new List<string>();
        var avisos = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI", Carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var tarjeta = raiz.GetComponent<ShopItemCard>();
                if (tarjeta == null || !OrdenarTarjeta(tarjeta, ruta, avisos)) continue;
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                hechos.Add(ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var ui = raiz.GetComponentInChildren<ShopUI>(true);
                var panel = Buscar(raiz.transform, "DetailPanel") as RectTransform;
                if (ui == null || panel == null) continue;
                if (OrdenarPanel(ui, panel, ruta, avisos))
                {
                    PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                    hechos.Add(ruta);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        AssetDatabase.SaveAssets();
        string resultado = "Tiendas: ordenado en " + (hechos.Count == 0 ? "ninguna" : string.Join(", ", hechos));
        if (avisos.Count == 0) Debug.Log(resultado);
        else Debug.LogWarning(resultado + "\n" + string.Join("\n", avisos));
    }

    static bool OrdenarPanel(ShopUI ui, RectTransform panel, string ruta, List<string> avisos)
    {
        var precio = Buscar(panel, "DetailPrice")?.GetComponent<TextMeshProUGUI>();
        var moneda = Buscar(panel, "DetailPriceCoinIcon")?.GetComponent<Image>();
        var chip = Buscar(panel, "DetailStockChipBg") as RectTransform;
        var stock = Buscar(panel, "DetailStock")?.GetComponent<TextMeshProUGUI>();
        var comprar = Buscar(panel, "BuyButton") as RectTransform;
        var vender = Buscar(panel, "SellButton") as RectTransform;
        var descripcion = Buscar(panel, "DetailDescription")?.GetComponent<TextMeshProUGUI>();
        var nombre = Buscar(panel, "DetailName") as RectTransform;
        var icono = Buscar(panel, "DetailIcon") as RectTransform;
        if (precio == null || moneda == null || chip == null || stock == null || comprar == null)
        { avisos.Add($"{ruta}: le falta alguna pieza del panel de detalle."); return false; }

        // La comparación de equipo y la moneda que falta comparten el espacio de descripción.
        // El texto se ajusta dentro de su zona y nunca invade los controles de compra.
        if (nombre != null) Estirar(nombre, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.74f));
        if (icono != null)
        {
            icono.anchorMin = icono.anchorMax = new Vector2(0.5f, 0.85f);
            icono.anchoredPosition = Vector2.zero;
        }
        if (descripcion != null)
        {
            Estirar(descripcion.rectTransform, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.63f));
            descripcion.textWrappingMode = TextWrappingModes.Normal;
            descripcion.verticalAlignment = VerticalAlignmentOptions.Top;
            descripcion.enableAutoSizing = true;
            descripcion.fontSizeMin = 16f;
            descripcion.fontSizeMax = 20f;
            descripcion.overflowMode = TextOverflowModes.Ellipsis;
            descripcion.margin = Vector4.zero;
            EditorUtility.SetDirty(descripcion);
        }

        // Fila de precio: texto en una línea y la moneda a su derecha, centrados juntos.
        var fila = Buscar(panel, "FilaDePrecio") as RectTransform;
        if (fila == null)
        {
            fila = new GameObject("FilaDePrecio", typeof(RectTransform)).GetComponent<RectTransform>();
            fila.SetParent(panel, false);
            fila.SetSiblingIndex(precio.transform.GetSiblingIndex());
        }
        Estirar(fila, new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.32f));
        var horizontal = Asegurar<HorizontalLayoutGroup>(fila.gameObject);
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.spacing = 12f;
        horizontal.childControlWidth = horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;
        precio.rectTransform.SetParent(fila, false);
        precio.textWrappingMode = TextWrappingModes.NoWrap;
        precio.horizontalAlignment = HorizontalAlignmentOptions.Center;
        precio.verticalAlignment = VerticalAlignmentOptions.Middle;
        moneda.rectTransform.SetParent(fila, false);
        moneda.preserveAspect = true;
        var tamMoneda = Asegurar<LayoutElement>(moneda.gameObject);
        tamMoneda.minWidth = tamMoneda.preferredWidth = 34f;
        tamMoneda.minHeight = tamMoneda.preferredHeight = 34f;

        // Chip de stock: tan ancho como su texto más el relleno, centrado.
        chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.235f);
        chip.pivot = new Vector2(0.5f, 0.5f);
        chip.anchoredPosition = Vector2.zero;
        chip.sizeDelta = new Vector2(chip.sizeDelta.x, 36f);
        var rellenoChip = Asegurar<HorizontalLayoutGroup>(chip.gameObject);
        rellenoChip.padding = new RectOffset(22, 22, 2, 2);
        rellenoChip.childAlignment = TextAnchor.MiddleCenter;
        rellenoChip.childControlWidth = rellenoChip.childControlHeight = true;
        rellenoChip.childForceExpandWidth = rellenoChip.childForceExpandHeight = false;
        var ajusteChip = Asegurar<ContentSizeFitter>(chip.gameObject);
        ajusteChip.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        ajusteChip.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        stock.rectTransform.SetParent(chip, false);
        stock.textWrappingMode = TextWrappingModes.NoWrap;
        stock.margin = Vector4.zero;
        stock.horizontalAlignment = HorizontalAlignmentOptions.Center;
        stock.verticalAlignment = VerticalAlignmentOptions.Middle;
        Asegurar<BordesProporcionales>(chip.gameObject);

        // Botón de compra centrado, más corto; las puntas en proporción a su alto.
        Estirar(comprar, new Vector2(0.225f, 0.08f), new Vector2(0.775f, 0.16f));
        Asegurar<BordesProporcionales>(comprar.gameObject);
        if (vender != null)
        {
            Estirar(vender, new Vector2(0.225f, 0f), new Vector2(0.775f, 0.06f));
            Asegurar<BordesProporcionales>(vender.gameObject);
        }

        var so = new SerializedObject(ui);
        so.FindProperty("detailPriceCurrencyIcon").objectReferenceValue = moneda;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
        return true;
    }

    static bool OrdenarTarjeta(ShopItemCard tarjeta, string ruta, List<string> avisos)
    {
        var nombre = new SerializedObject(tarjeta).FindProperty("nameText").objectReferenceValue as TextMeshProUGUI;
        if (nombre == null) { avisos.Add($"{ruta}: la tarjeta no tiene nameText."); return false; }
        // Encima de la fila del precio, hasta el borde derecho (el chip de stock va abajo).
        Estirar(nombre.rectTransform, new Vector2(0.25f, 0.5f), new Vector2(0.95f, 0.92f));
        nombre.textWrappingMode = TextWrappingModes.Normal;
        nombre.verticalAlignment = VerticalAlignmentOptions.Bottom;
        nombre.enableAutoSizing = true;
        nombre.fontSizeMax = Mathf.Max(nombre.fontSize, 20f);
        nombre.fontSizeMin = 14f;
        nombre.lineSpacing = -18f;
        nombre.margin = Vector4.zero;
        EditorUtility.SetDirty(nombre);
        return true;
    }

    static void Estirar(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    static T Asegurar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
