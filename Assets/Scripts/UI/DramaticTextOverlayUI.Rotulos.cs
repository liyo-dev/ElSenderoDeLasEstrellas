using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Las otras dos formas de texto en pantalla que dibuja este mismo servicio, para que nadie más
/// monte su propio Canvas de texto:
/// - El rótulo sobre la pantalla tapada del grafo («A la mañana siguiente…»), como
///   <see cref="INarrativeTitleCardPresenter"/>. Se dibuja por encima del fundido de pantalla.
/// - El rótulo breve de abajo (mensajes cortos del juego), que pinta <see cref="HudToastService"/>
///   cuando el árbitro de avisos le da el turno.
public partial class DramaticTextOverlayUI : INarrativeTitleCardPresenter
{
    // Encima del fundido de pantalla de FeedbackService (9998).
    const int OrdenSobreElFundido = 9999;
    // Por encima del HUD y de los banners de aviso.
    const int OrdenDelRotuloBreve = 120;

    // ── Rótulo sobre la pantalla (grafo) ─────────────────────────────────────────────────────

    bool _rotuloEnCurso;
    Action _alAcabarRotulo;
    Action _rotuloCortado;
    TMP_FontAsset _fuenteOriginal;
    float _espaciadoOriginal;

    Canvas _lienzoPropio;
    int _ordenOriginal;
    bool _sobreescribiaOrden;

    public void ShowTitleCard(NarrativeTitleCard card, Action onDone)
    {
        if (_label == null || _textContainer == null || _rootGroup == null)
        {
            onDone?.Invoke();
            return;
        }
        gameObject.SetActive(true);
        CortarLoQueSuena();
        _playRoutine = StartCoroutine(Co_Rotulo(card, onDone));
        AvisarRotuloCortado();
    }

    public void CancelTitleCard()
    {
        if (!_rotuloEnCurso) return;
        _alAcabarRotulo = null;
        ForceStop();
    }

    IEnumerator Co_Rotulo(NarrativeTitleCard card, Action onDone)
    {
        _isPlaying = true;
        _rotuloEnCurso = true;
        _alAcabarRotulo = onDone;
        _fuenteOriginal = _label.font;
        _espaciadoOriginal = _label.characterSpacing;
        SubirSobreElFundido(true);

        SetBackground(DramaticTextBackground.None);
        if (card.Font != null) _label.font = card.Font;
        _label.fontSize = card.Size;
        _label.fontStyle = FontStyles.Normal;
        _label.characterSpacing = 4f;
        _label.color = card.Color;
        _label.text = card.Text;
        _textContainer.localScale = Vector3.one;
        _textContainer.anchoredPosition = Vector2.zero;
        _rootGroup.blocksRaycasts = false;

        yield return FundirRaiz(0f, 1f, card.FadeIn);
        yield return new WaitForSecondsRealtime(card.Hold);
        yield return FundirRaiz(1f, 0f, card.FadeOut);

        _isPlaying = false;
        _playRoutine = null;
        gameObject.SetActive(false);
        TerminarRotulo()?.Invoke();
    }

    IEnumerator FundirRaiz(float desde, float hasta, float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            _rootGroup.alpha = Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / segundos));
            yield return null;
        }
        _rootGroup.alpha = hasta;
    }

    /// Deja la etiqueta y el orden de dibujo como estaban antes del rótulo. Devuelve el aviso de
    /// fin para quien lo pidió (se llama cuando ya no queda nada a medias).
    Action TerminarRotulo()
    {
        if (!_rotuloEnCurso) return null;
        _rotuloEnCurso = false;
        var alAcabar = _alAcabarRotulo;
        _alAcabarRotulo = null;
        if (_label != null)
        {
            _label.font = _fuenteOriginal;
            _label.characterSpacing = _espaciadoOriginal;
        }
        if (_rootGroup != null) _rootGroup.alpha = 0f;
        SubirSobreElFundido(false);
        return alAcabar;
    }

    // Si otro texto corta el rótulo, también se avisa a quien lo pidió: el grafo no puede
    // quedarse esperando un rótulo que ya no se va a ver. Se llama al final de cada método
    // público, con el texto nuevo ya en marcha.
    void AvisarRotuloCortado()
    {
        var aviso = _rotuloCortado;
        _rotuloCortado = null;
        aviso?.Invoke();
    }

    // El rótulo se lee sobre la pantalla ya tapada por FeedbackService, que se dibuja por encima
    // del resto de la interfaz: mientras dura, este overlay sube por encima del fundido.
    void SubirSobreElFundido(bool subir)
    {
        if (_lienzoPropio == null)
        {
            if (!subir) return;
            _lienzoPropio = GetComponent<Canvas>();
            if (_lienzoPropio == null)
            {
                // Lienzo anidado: hereda el escalado del padre; con su propio raycaster las frases
                // siguen bloqueando clics como antes.
                _lienzoPropio = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            _ordenOriginal = _lienzoPropio.sortingOrder;
            _sobreescribiaOrden = _lienzoPropio.overrideSorting;
        }
        _lienzoPropio.overrideSorting = subir || _sobreescribiaOrden;
        _lienzoPropio.sortingOrder = subir ? OrdenSobreElFundido : _ordenOriginal;
    }

    // ── Rótulo breve (abajo) ─────────────────────────────────────────────────────────────────

    CanvasGroup _rotuloBreve;
    TextMeshProUGUI _textoDelRotuloBreve;
    Tween _fundidoDelRotuloBreve;

    /// Pone el texto del rótulo breve (sin cambiar si se ve o no).
    public void PonerRotuloBreve(string texto)
    {
        ConstruirRotuloBreve();
        _textoDelRotuloBreve.text = texto;
    }

    /// Funde el rótulo breve hasta <paramref name="alfa"/> en <paramref name="segundos"/> (0 = al instante).
    public void FundirRotuloBreve(float alfa, float segundos)
    {
        ConstruirRotuloBreve();
        _fundidoDelRotuloBreve?.Kill();
        _fundidoDelRotuloBreve = null;
        if (alfa > 0f && !_rotuloBreve.gameObject.activeSelf) _rotuloBreve.gameObject.SetActive(true);

        if (segundos <= 0f)
        {
            _rotuloBreve.alpha = alfa;
            if (alfa <= 0f) _rotuloBreve.gameObject.SetActive(false);
            return;
        }
        _fundidoDelRotuloBreve = _rotuloBreve.DOFade(alfa, segundos).SetEase(Ease.Linear).SetUpdate(true)
            .OnComplete(() =>
            {
                _fundidoDelRotuloBreve = null;
                if (alfa <= 0f && _rotuloBreve != null) _rotuloBreve.gameObject.SetActive(false);
            });
    }

    // Franja semitransparente centrada en la parte baja. Cuelga de la misma raíz persistente que
    // este overlay (no de él, que se apaga cuando no hay frases), con su propio orden de dibujo.
    void ConstruirRotuloBreve()
    {
        if (_rotuloBreve != null) return;

        var raiz = new GameObject("RotuloBreve", typeof(RectTransform));
        if (transform.parent != null) raiz.transform.SetParent(transform.root, false);
        else DontDestroyOnLoad(raiz);

        var lienzo = raiz.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.overrideSorting = true;
        lienzo.sortingOrder = OrdenDelRotuloBreve;
        // Solo actúa si la raíz no tiene Canvas propio (entonces este es el lienzo raíz).
        var escalado = raiz.AddComponent<CanvasScaler>();
        escalado.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalado.referenceResolution = new Vector2(1920f, 1080f);
        escalado.matchWidthOrHeight = 0.5f;

        var rtRaiz = (RectTransform)raiz.transform;
        rtRaiz.anchorMin = Vector2.zero;
        rtRaiz.anchorMax = Vector2.one;
        rtRaiz.offsetMin = rtRaiz.offsetMax = Vector2.zero;

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(raiz.transform, false);
        var rtPanel = (RectTransform)panel.transform;
        rtPanel.anchorMin = new Vector2(0.2f, 0.06f);
        rtPanel.anchorMax = new Vector2(0.8f, 0.13f);
        rtPanel.offsetMin = rtPanel.offsetMax = Vector2.zero;
        var fondo = panel.AddComponent<Image>();
        fondo.color = new Color(0f, 0f, 0f, 0.65f);
        fondo.raycastTarget = false;

        var textoGo = new GameObject("Texto", typeof(RectTransform));
        textoGo.transform.SetParent(panel.transform, false);
        var rtTexto = (RectTransform)textoGo.transform;
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = new Vector2(12f, 4f);
        rtTexto.offsetMax = new Vector2(-12f, -4f);
        _textoDelRotuloBreve = textoGo.AddComponent<TextMeshProUGUI>();
        _textoDelRotuloBreve.alignment = TextAlignmentOptions.Center;
        _textoDelRotuloBreve.fontSize = 22f;
        _textoDelRotuloBreve.color = Color.white;
        _textoDelRotuloBreve.raycastTarget = false;

        _rotuloBreve = raiz.AddComponent<CanvasGroup>();
        _rotuloBreve.alpha = 0f;
        _rotuloBreve.blocksRaycasts = false;
        _rotuloBreve.interactable = false;
        raiz.SetActive(false);
    }

    void DestruirRotuloBreve()
    {
        _fundidoDelRotuloBreve?.Kill();
        if (_rotuloBreve != null) Destroy(_rotuloBreve.gameObject);
        _rotuloBreve = null;
    }
}
