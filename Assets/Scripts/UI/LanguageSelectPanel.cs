using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using M = EstiloDePapelDelMenu;

/// <summary>
/// Selector de idioma del primer arranque, con la disposición de la maqueta «Primer arranque:
/// idioma»: fondo de noche, el título del juego, «Elige tu idioma · Choose your language» y una
/// carta por idioma (la elegida, dorada e inclinada con su estrella y la A). Se monta por código.
/// A diferencia de Ajustes o Controles no se cierra con B ni Start: hay que elegir.
/// </summary>
public class LanguageSelectPanel : MonoBehaviour
{
    [Serializable]
    private class Idioma
    {
        [Tooltip("Locale tal y como lo esperan LocalizationManager/PlayerSettings (\"es\", \"en\"...).")]
        public string locale;
        [Tooltip("Nombre del idioma escrito en ese idioma.")]
        public string nombre;
        [Tooltip("Frase corta en ese idioma bajo el nombre.")]
        public string frase;
    }

    [SerializeField] private GameObject root;
    [Tooltip("Fuentes de los menús de papel.")]
    [SerializeField] private EstiloDeMenuSencillo estilo;
    [Tooltip("Título del juego.")]
    [SerializeField] private Sprite logo;
    [Tooltip("Parte visible del sprite del título (fracciones, desde abajo a la izquierda); la pone el montaje de los menús.")]
    [SerializeField] private Rect zonaVisibleDelTitulo = new Rect(0f, 0f, 1f, 1f);
    [Tooltip("Una carta por idioma, de izquierda a derecha.")]
    [SerializeField] private Idioma[] idiomas =
    {
        new Idioma { locale = "es", nombre = "Español", frase = "Jugar en español" },
        new Idioma { locale = "en", nombre = "English", frase = "Play in English" },
    };

    private readonly List<Button> _botones = new();
    private Action _onClosed;
    private bool _picking, _montado;

    public bool IsVisible => root != null && root.activeInHierarchy;

    private TMP_FontAsset Titulos => estilo != null && estilo.fuenteTitulo != null ? estilo.fuenteTitulo : TMP_Settings.defaultFontAsset;
    private TMP_FontAsset Letra => estilo != null && estilo.fuenteTexto != null ? estilo.fuenteTexto : TMP_Settings.defaultFontAsset;

    void Awake()
    {
        if (!root) root = gameObject;
        Montar();
    }

    private void Montar()
    {
        if (_montado) return;
        _montado = true;
        var grupo = root.GetComponent<CanvasGroup>();
        if (grupo == null) grupo = root.AddComponent<CanvasGroup>();
        grupo.ignoreParentGroups = true;

        var lienzo = PiezasDeUI.LienzoDeMaqueta((RectTransform)root.transform, "Idioma");
        PiezasDeUI.FondoDeNoche(lienzo);
        if (logo != null)
        {
            var img = PiezasDeUI.Imagen("Titulo", lienzo, logo, Color.white);
            img.raycastTarget = false;
            PiezasDeUI.ColocarRecortado(img, zonaVisibleDelTitulo, 637f, 70f, 646f);
        }
        var titulo = Texto("Pregunta", Hueco(lienzo, 460f, 520f, 1000f, 56f), "Elige tu idioma · Choose your language", true, 40f, M.TextoSobreNoche, TextAlignmentOptions.Center);
        titulo.raycastTarget = false;

        for (int i = 0; i < idiomas.Length; i++)
        {
            var idioma = idiomas[i];
            float x = 520f + i * 460f;
            var hueco = Hueco(lienzo, x, 620f, 400f, 220f);
            hueco.name = "Idioma " + idioma.locale;
            var tapa = hueco.gameObject.AddComponent<Image>();
            tapa.color = new Color(1f, 1f, 1f, 0f);
            var boton = hueco.gameObject.AddComponent<Button>();
            boton.targetGraphic = tapa;
            boton.transition = Selectable.Transition.None;
            boton.onClick.AddListener(() => Elegir(idioma.locale));
            hueco.gameObject.AddComponent<UIButtonAudio>();
            hueco.gameObject.AddComponent<FocoAlPasarElRaton>();

            var carta = PiezasDeUI.NuevaCarta("Carta", hueco, PaletaUI.Papel, 28f);
            PiezasDeUI.Estirar(carta.raiz);
            var globo = PiezasDeUI.Imagen("Globo", carta.contenido, PiezasDeUI.Globo, Color.white);
            globo.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(globo.rectTransform, 32f, 30f, 72f, 72f);
            Texto("Nombre", Hueco(carta.contenido, 120f, 40f, 260f, 60f), idioma.nombre, true, 46f, PaletaUI.TintaDeTexto);
            var frase = Texto("Frase", Hueco(carta.contenido, 32f, 136f, 340f, 36f), idioma.frase, false, 26f, M.TextoSuave);
            var estrella = PiezasDeUI.EstrellaEn("Estrella", carta.contenido, 4f, 12f, 56f);
            var insignia = PiezasDeUI.IconoDeBoton("BotonA", carta.contenido, Core.InputGlyphs.InputGlyphNames.Confirm, 44f);
            PiezasDeUI.ColocarArribaIzquierda(insignia, 330f, 170f, 44f, 44f);

            var ficha = hueco.gameObject.AddComponent<FichaElegible>();
            ficha.Configurar(boton, carta, frase, new[] { estrella.gameObject, insignia.gameObject });
            ficha.Colores(PaletaUI.Papel, PaletaUI.Oro, M.TextoSuave, PaletaUI.TintaDeTexto);
            ficha.Movimiento(i == 0 ? -1.4f : 1f, i == 0 ? -1.4f : 1f, Vector2.zero, 7f, 7f);
            _botones.Add(boton);
        }
        root.AddComponent<SeleccionConTeclado>().Configurar(_botones.Count > 0 ? _botones[0] : null);
        for (int i = 0; i < _botones.Count; i++)
            _botones[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = i > 0 ? _botones[i - 1] : null,
                selectOnRight = i < _botones.Count - 1 ? _botones[i + 1] : null,
            };

        M.Ayuda(lienzo, Titulos, 1726f, Core.InputGlyphs.InputGlyphNames.Confirm).text = "Elegir";
    }

    private static RectTransform Hueco(Transform padre, float x, float y, float ancho, float alto)
    {
        var rt = new GameObject("Hueco", typeof(RectTransform)).transform as RectTransform;
        rt.SetParent(padre, false);
        PiezasDeUI.ColocarArribaIzquierda(rt, x, y, ancho, alto);
        return rt;
    }

    private TextMeshProUGUI Texto(string nombre, Transform padre, string texto, bool titulo, float tamano, Color color,
        TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft)
    {
        var t = PiezasDeUI.Texto(nombre, padre, texto, titulo ? Titulos : Letra, tamano, color);
        t.alignment = alineacion;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        PiezasDeUI.Estirar(t.rectTransform);
        return t;
    }

    /// <param name="onClosed">Se invoca justo después de que el jugador elija un idioma.</param>
    public void Show(Action onClosed = null)
    {
        _onClosed = onClosed;
        _picking = false;
        Montar();
        if (root && !root.activeSelf) root.SetActive(true);
        var grupo = root.GetComponent<CanvasGroup>();
        grupo.alpha = 1f;
        grupo.interactable = true;
        grupo.blocksRaycasts = true;
        if (EventSystem.current != null && _botones.Count > 0) EventSystem.current.SetSelectedGameObject(_botones[0].gameObject);
    }

    /// <param name="silent">Sin el sonido de cierre (el cierre normal es una elección, que ya suena).</param>
    public void Close(bool silent = true)
    {
        _picking = false;
        var grupo = root != null ? root.GetComponent<CanvasGroup>() : null;
        if (grupo != null) { grupo.interactable = false; grupo.blocksRaycasts = false; }
        bool estabaVisible = root && root.activeSelf;
        if (!silent && estabaVisible) AudioService.Instance?.PlaySFX("UI_Cancel", 1f);
        if (estabaVisible) root.SetActive(false);
        _onClosed?.Invoke();
        _onClosed = null;
    }

    private void Elegir(string locale)
    {
        if (_picking) return;
        _picking = true;
        AudioService.Instance?.PlaySFX("UI_Submit");
        PlayerSettings.SetLanguage(locale);
        PlayerSettings.MarkLanguageSelected();
        Close(silent: true);
    }
}
