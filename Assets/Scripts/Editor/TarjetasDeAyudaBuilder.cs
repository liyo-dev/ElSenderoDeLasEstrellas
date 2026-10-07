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

    public static void AjustarBarra(Transform barra, List<string> avisos)
    {
        var layout = barra.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) { avisos.Add("PanelInfo no tiene HorizontalLayoutGroup."); return; }
        layout.padding.left = 220;
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.padding.right = 0;
        foreach (Transform child in barra)
        {
            if (child.GetComponent<Image>() == null || child.name == "ContadorDeMonedas") continue;
            var element = child.GetComponent<LayoutElement>();
            if (element == null) element = child.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 273;
            element.flexibleWidth = 0;
            element.layoutPriority = 10;
        }
        // Seis tarjetas ocupan 1638 px y cinco separaciones 40 px: caben en los 1700 px disponibles.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)barra);
        EditorUtility.SetDirty(layout);
    }
}
