using TMPro;
using UnityEngine;
using Core.InputGlyphs;

/// <summary>
/// Los controles del barco en el HUD. Mientras el jugador va a bordo, la cruz de combate
/// (X hechizo, Y combo, B escudo) se esconde y aparecen los botones del barco: <b>bajar</b>
/// (Y) y <b>acelerar</b> (LT). El icono de cada botón es el del mando que toque en cada
/// dispositivo, con el sistema de glifos del proyecto.
///
/// Se crea al subir al barco y se destruye al bajar, así que no hace falta tocar el HUD del
/// juego en ningún prefab.
/// </summary>
public class ControlesDelBarcoHUD : MonoBehaviour
{
    private Canvas _canvas;
    private GameObject[] _botonesDeCombate = new GameObject[0];
    private GameObject _panel;

    /// <summary>Coloca los controles del barco en el HUD del jugador.</summary>
    public static ControlesDelBarcoHUD Mostrar(GameObject jugador)
    {
        if (jugador == null) return null;
        var canvas = jugador.GetComponentInChildren<Canvas>();
        if (canvas == null) return null;

        var go = new GameObject("Controles del barco");
        var controles = go.AddComponent<ControlesDelBarcoHUD>();
        controles._canvas = canvas;
        controles.Preparar();
        return controles;
    }

    void Preparar()
    {
        // Esconde los botones de combate (X, Y, B) para dejar su sitio a los del barco.
        var botones = new System.Collections.Generic.List<GameObject>();
        foreach (var nombre in new[] { "ButtonX", "ButtonY", "ButtonB" })
        {
            var t = _canvas.transform.Find(nombre);
            if (t != null) botones.Add(t.gameObject);
        }
        _botonesDeCombate = botones.ToArray();
        foreach (var b in _botonesDeCombate) b.SetActive(false);

        _panel = new GameObject("Botones del barco", typeof(RectTransform));
        _panel.transform.SetParent(_canvas.transform, false);
        var rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 230f);
        rt.sizeDelta = new Vector2(340f, 150f);

        CrearBoton("BARCO_BOTON_BAJAR", "Bajar", InputGlyphNames.North, new Vector2(0f, 0f));
        CrearBoton("BARCO_BOTON_ACELERAR", "Acelerar", InputGlyphNames.TriggerLeft, new Vector2(0f, -56f));
    }

    void CrearBoton(string clave, string porDefecto, string glifo, Vector2 posicion)
    {
        var go = new GameObject("Boton " + porDefecto, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_panel.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = posicion;
        rt.sizeDelta = new Vector2(320f, 50f);

        var texto = go.GetComponent<TextMeshProUGUI>();
        texto.text = InputGlyphService.SpriteTag(glifo) + "   " +
                     (LocalizationManager.Instance != null
                         ? LocalizationManager.Instance.Get(clave, porDefecto)
                         : porDefecto);
        texto.alignment = TextAlignmentOptions.Right;
        texto.fontSize = 26f;
        texto.color = Color.white;
        InputGlyphService.UsarIconos(texto);
    }

    /// <summary>Quita los controles del barco y devuelve los de combate.</summary>
    public void Ocultar()
    {
        foreach (var b in _botonesDeCombate)
        {
            if (b != null) b.SetActive(true);
        }
        if (_panel != null) Destroy(_panel);
        Destroy(gameObject);
    }
}
