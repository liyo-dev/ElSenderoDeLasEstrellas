using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Viste la caja de diálogo con el papel de la interfaz (<see cref="PiezasDeUI.Papel"/>): una hoja
/// con borde de tinta bajo el texto, una pestaña inclinada con el nombre de quien habla asomando
/// por arriba y las opciones de respuesta en fichas de papel. La monta <see cref="DialogueManager"/>
/// sobre el panel de la escena.
/// </summary>
public sealed class PapelDeLaCajaDeDialogo
{
    const float MargenLateral = 58f;
    const float MargenSuperior = 54f;
    const float MargenInferior = 24f;
    const float AltoDeLaPestana = 68f;
    const float AnchoMinimoDeLaPestana = 150f;
    const float MargenDeLaPestana = 32f;
    const float InclinacionDeLaPestana = 2f;
    const float LetraMaximaDelNombre = 40f;
    const float LetraMaximaDeLaOpcion = 40f;
    /// Las piezas pequeñas afinan esquinas, borde y sombra del papel en esta proporción.
    const float FinuraDeLasPiezas = 1.6f;
    static readonly Vector2 TamanoDeLaFicha = new Vector2(176f, 78f);

    readonly RectTransform _pestana;
    readonly Image _fondoDeLaPestana;
    readonly TextMeshProUGUI _nombre;

    public PapelDeLaCajaDeDialogo(RectTransform panel, TextMeshProUGUI nombre, TextMeshProUGUI cuerpo)
    {
        var hoja = PiezasDeUI.Imagen("Papel", panel, PiezasDeUI.Papel(PaletaUI.Papel, PaletaUI.Tinta, PaletaUI.PapelClaro), Color.white);
        hoja.raycastTarget = false;
        PiezasDeUI.Estirar(hoja.rectTransform);
        hoja.rectTransform.offsetMin = new Vector2(0f, -PiezasDeUI.SombraDelPapel);
        hoja.transform.SetAsFirstSibling();

        if (cuerpo != null)
        {
            var rt = cuerpo.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(MargenLateral, MargenInferior);
            rt.offsetMax = new Vector2(-MargenLateral, -MargenSuperior);
            cuerpo.alignment = TextAlignmentOptions.TopLeft;
            // El mismo aire a los dos lados: el hueco del botón de pasar página solo se reserva
            // cuando una línea lo pisaría (DialogueManager). Ver INC-1110
            var margen = cuerpo.margin;
            margen.z = margen.x;
            cuerpo.margin = margen;
            cuerpo.color = PaletaUI.TintaDeTexto;
        }

        if (nombre == null) return;
        _nombre = nombre;
        var go = new GameObject("PestanaDelNombre", typeof(RectTransform));
        _pestana = (RectTransform)go.transform;
        _pestana.SetParent(panel, false);
        _pestana.anchorMin = _pestana.anchorMax = new Vector2(0f, 1f);
        _pestana.pivot = new Vector2(0f, 0.5f);
        _pestana.anchoredPosition = new Vector2(44f, 6f);
        _pestana.sizeDelta = new Vector2(AnchoMinimoDeLaPestana, AltoDeLaPestana);
        _pestana.localRotation = Quaternion.Euler(0f, 0f, InclinacionDeLaPestana);

        _fondoDeLaPestana = PiezasDeUI.Imagen("Fondo", _pestana, null, Color.white);
        _fondoDeLaPestana.raycastTarget = false;
        _fondoDeLaPestana.type = Image.Type.Sliced;
        _fondoDeLaPestana.pixelsPerUnitMultiplier = FinuraDeLasPiezas;
        PiezasDeUI.Estirar(_fondoDeLaPestana.rectTransform);
        _fondoDeLaPestana.rectTransform.offsetMin = new Vector2(0f, -PiezasDeUI.SombraDelPapel / FinuraDeLasPiezas);

        _nombre.transform.SetParent(_pestana, false);
        PiezasDeUI.Estirar(_nombre.rectTransform);
        _nombre.margin = Vector4.zero;
        _nombre.alignment = TextAlignmentOptions.Center;
        _nombre.textWrappingMode = TextWrappingModes.NoWrap;
        _nombre.overflowMode = TextOverflowModes.Overflow;
        _nombre.fontSize = Mathf.Min(_nombre.fontSize, LetraMaximaDelNombre);
        go.SetActive(false);
    }

    /// <summary>
    /// Escribe el nombre en la pestaña, del color de quien habla, y la ajusta a su ancho. Sin
    /// nombre, la pestaña se esconde.
    /// </summary>
    public void PonerNombre(string nombre, Color fondo, Color letra)
    {
        if (_pestana == null) return;
        bool hay = !string.IsNullOrEmpty(nombre);
        if (_pestana.gameObject.activeSelf != hay) _pestana.gameObject.SetActive(hay);
        if (!hay)
        {
            _nombre.text = string.Empty;
            return;
        }
        _nombre.text = nombre;
        _nombre.color = letra;
        _fondoDeLaPestana.sprite = PiezasDeUI.Papel(fondo, PaletaUI.Tinta, fondo, 0.5f);
        float ancho = _nombre.GetPreferredValues(nombre).x + MargenDeLaPestana * 2f;
        _pestana.sizeDelta = new Vector2(Mathf.Max(AnchoMinimoDeLaPestana, ancho), AltoDeLaPestana);
    }

    /// <summary>
    /// Pone una ficha de papel bajo el texto de una opción: apagada en reposo y dorada con el foco.
    /// </summary>
    public void VestirOpcion(Button opcion)
    {
        if (opcion == null) return;
        var ficha = PiezasDeUI.Imagen("Ficha", opcion.transform, PiezasDeUI.Papel(Color.white, PaletaUI.Tinta, Color.white, 0.5f), PaletaUI.PapelApagado);
        ficha.raycastTarget = false;
        ficha.pixelsPerUnitMultiplier = FinuraDeLasPiezas;
        PiezasDeUI.Colocar(ficha.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, TamanoDeLaFicha);
        ficha.transform.SetAsFirstSibling();

        var etiqueta = opcion.GetComponentInChildren<TextMeshProUGUI>(true);
        if (etiqueta != null)
        {
            etiqueta.color = PaletaUI.TintaDeTexto;
            etiqueta.fontSize = Mathf.Min(etiqueta.fontSize, LetraMaximaDeLaOpcion);
        }

        var fx = opcion.GetComponent<ChoiceButtonFx>();
        if (fx == null) fx = opcion.gameObject.AddComponent<ChoiceButtonFx>();
        fx.targetGraphic = ficha;
        fx.normalColor = PaletaUI.PapelApagado;
        fx.selectedColor = PaletaUI.Oro;
    }
}
