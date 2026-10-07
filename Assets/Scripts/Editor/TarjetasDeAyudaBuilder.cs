using System.Collections.Generic;
using Core.InputGlyphs;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Construye y repara tarjetas de ayuda a partir del arte de la barra existente.</summary>
public static class TarjetasDeAyudaBuilder
{
    public const string FondoGuid = "550ba9fb6d7b439da282fe1d35c2e0ce";

    public static Transform Buscar(Transform root, string nombre)
    {
        if (root.name == nombre) return root;
        foreach (Transform child in root)
        {
            var encontrado = Buscar(child, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    public static GameObject Montar(Transform barra, string nombre, string glyph, string clave, string texto,
                                   bool visible, List<string> avisos)
    {
        var card = barra.Find(nombre);
        if (card == null)
        {
            Transform plantilla = null;
            foreach (Transform child in barra)
                if (child.GetComponentsInChildren<InputGlyphIcon>(true).Length == 1)
                { plantilla = child; break; }
            if (plantilla == null) { avisos.Add("PanelInfo no tiene una tarjeta con un solo glyph."); return null; }
            card = Object.Instantiate(plantilla.gameObject, barra, false).transform;
            card.name = nombre;
        }
        card.localScale = Vector3.one;
        var fondo = card.GetComponent<Image>();
        if (fondo != null)
        {
            fondo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(FondoGuid));
            fondo.raycastTarget = true;
        }
        var icon = card.GetComponentInChildren<InputGlyphIcon>(true);
        var tmp = card.GetComponentInChildren<TextMeshProUGUI>(true);
        if (icon == null || tmp == null) { avisos.Add($"La tarjeta {nombre} no tiene glyph o texto."); return null; }
        var iconSo = new SerializedObject(icon);
        iconSo.FindProperty("glyphName").stringValue = glyph;
        iconSo.FindProperty("icon").objectReferenceValue = icon.GetComponent<Image>();
        iconSo.ApplyModifiedPropertiesWithoutUndo();
        var image = icon.GetComponent<Image>();
        if (image != null) image.sprite = InputGlyphService.GetSprite(glyph);
        tmp.text = texto;
        var loc = tmp.GetComponent<LocalizedText>();
        if (loc == null) loc = tmp.gameObject.AddComponent<LocalizedText>();
        var locSo = new SerializedObject(loc);
        locSo.FindProperty("_key").stringValue = clave;
        locSo.ApplyModifiedPropertiesWithoutUndo();
        card.gameObject.SetActive(visible);
        return card.gameObject;
    }

    private const float MargenDerecho = 24f;
    private const float SeparacionTrasFijos = 16f;
    private const float AnchoMaximoTarjeta = 273f;
    private const float AnchoMinimoLegible = 180f;

    /// <summary>
    /// Alinea las tarjetas a la izquierda, después de los elementos fijos de la barra (los que
    /// ignoran el layout, como los contadores), y les da a todas el mismo ancho calculado para
    /// que quepan también las contextuales (inactivas al montar, se encienden según la pestaña).
    /// Las contextuales van al final, así al encenderlas ninguna otra tarjeta se mueve.
    /// </summary>
    public static void AjustarBarra(Transform barra, List<string> avisos)
    {
        var layout = barra.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) { avisos.Add("PanelInfo no tiene HorizontalLayoutGroup."); return; }
        var rect = (RectTransform)barra;
        float anchoBarra = AnchoDeReferencia(rect);
        float finDeFijos = 0f;
        var fijas = new List<Transform>();
        var contextuales = new List<Transform>();
        foreach (Transform child in barra)
        {
            var element = child.GetComponent<LayoutElement>();
            if (element != null && element.ignoreLayout)
            {
                var hijo = (RectTransform)child;
                float izquierda = hijo.anchorMin.x * anchoBarra + hijo.anchoredPosition.x - hijo.pivot.x * hijo.sizeDelta.x;
                finDeFijos = Mathf.Max(finDeFijos, izquierda + hijo.sizeDelta.x);
                continue;
            }
            if (child.GetComponent<Image>() == null) continue;
            (child.gameObject.activeSelf ? fijas : contextuales).Add(child);
        }
        foreach (var tarjeta in contextuales) tarjeta.SetAsLastSibling();

        int total = fijas.Count + contextuales.Count;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding.left = Mathf.CeilToInt(finDeFijos > 0f ? finDeFijos + SeparacionTrasFijos : 0f);
        layout.padding.right = 0;
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        float disponible = anchoBarra - layout.padding.left - MargenDerecho - layout.spacing * Mathf.Max(0, total - 1);
        float ancho = total > 0 ? Mathf.Min(AnchoMaximoTarjeta, Mathf.Floor(disponible / total)) : AnchoMaximoTarjeta;
        if (ancho < AnchoMinimoLegible)
            avisos.Add($"PanelInfo: {total} tarjetas quedan a {ancho} px; el texto puede no caber.");
        foreach (var lista in new[] { fijas, contextuales })
            foreach (var tarjeta in lista)
            {
                var element = tarjeta.GetComponent<LayoutElement>();
                if (element == null) element = tarjeta.gameObject.AddComponent<LayoutElement>();
                element.minWidth = element.preferredWidth = ancho;
                element.flexibleWidth = 0;
                element.layoutPriority = 10;
                EditorUtility.SetDirty(element);
            }
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        EditorUtility.SetDirty(layout);
    }

    /// <summary>
    /// Ancho del RectTransform en unidades de la resolución de referencia del CanvasScaler raíz,
    /// para no depender del tamaño que tenga la Game view al montar.
    /// </summary>
    private static float AnchoDeReferencia(RectTransform rect)
    {
        var canvas = rect.GetComponent<Canvas>();
        if (canvas != null && canvas.isRootCanvas)
        {
            var scaler = rect.GetComponent<CanvasScaler>();
            return scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                ? scaler.referenceResolution.x
                : rect.rect.width;
        }
        if (rect.parent is not RectTransform padre) return rect.rect.width;
        return AnchoDeReferencia(padre) * (rect.anchorMax.x - rect.anchorMin.x) + rect.sizeDelta.x;
    }
}
