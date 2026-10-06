using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sendero.Core.Feedback;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reproduce la salida del menú y espera a que la pantalla quede cubierta para cargar la partida.</summary>
[DisallowMultipleComponent]
public class EfectoDeComenzarPartida : MonoBehaviour
{
    [Tooltip("Clave del sonido de comienzo con eco incorporado.")]
    [SerializeField] private string claveSfx = "UI_ComenzarPartida";
    [Tooltip("Color que cubre la pantalla antes de cargar.")]
    [SerializeField] private Color colorFundido = Color.white;
    [Tooltip("Duración del fundido de pantalla en segundos reales.")]
    [SerializeField, Min(0.01f)] private float duracionFundido = 1.1f;
    [Tooltip("Espera mínima antes de cubrir la pantalla.")]
    [SerializeField, Min(0f)] private float retardoMinimo = 0.3f;
    [Tooltip("Tiempo que permanece la pantalla cubierta antes de cargar.")]
    [SerializeField, Min(0f)] private float sostenEnBlanco = 0.25f;
    [Tooltip("Duración del apagado de la música.")]
    [SerializeField, Min(0f)] private float fundidoMusica = 1.6f;
    [Tooltip("Multiplicador de escala máxima del destello del botón.")]
    [SerializeField, Min(1f)] private float escalaPunch = 1.12f;
    [Tooltip("Duración del apagado de los demás botones.")]
    [SerializeField, Min(0.01f)] private float duracionApagado = 0.4f;
    [Tooltip("Diámetro final del resplandor en unidades del canvas.")]
    [SerializeField, Min(1f)] private float tamanoResplandor = 1400f;
    [Tooltip("Duración de la expansión del resplandor.")]
    [SerializeField, Min(0.01f)] private float duracionResplandor = 1.2f;
    [Tooltip("Contenedor de botones; vacío utiliza el padre del botón pulsado.")]
    [SerializeField] private Transform contenedorBotones;

    private Texture2D _textura;
    private Sprite _sprite;
    private Image _resplandor;
    private bool _reproduciendo;
    private Coroutine _fundidoRoutine;
    private readonly List<System.Action> _restauraciones = new();

    void Awake()
    {
        const int lado = 128;
        _textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        _textura.name = "Resplandor de comienzo";
        _textura.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            float radio = (new Vector2(x, y) - Vector2.one * ((lado - 1) * 0.5f)).magnitude / ((lado - 1) * 0.5f);
            float alfa = Mathf.Pow(Mathf.Clamp01(1f - radio), 2f);
            pixels[y * lado + x] = new Color(1f, 1f, 1f, alfa);
        }
        _textura.SetPixels(pixels);
        _textura.Apply(false, true);
        _sprite = Sprite.Create(_textura, new Rect(0, 0, lado, lado), Vector2.one * 0.5f);
    }

    public IEnumerator Reproducir(Selectable botonPulsado, float esperaMinima)
    {
        if (_reproduciendo || !isActiveAndEnabled) yield break;
        _reproduciendo = true;
        AudioService.Instance?.PlaySFX(claveSfx);
        AudioService.Instance?.StopMusic(fundidoMusica);
        if (botonPulsado) PrepararBotones(botonPulsado);

        float espera = Mathf.Max(retardoMinimo, esperaMinima - duracionFundido);
        float t = 0f;
        while (t < espera && _reproduciendo)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!_reproduciendo) yield break;
        Color color = colorFundido;
        color.a = 1f;
        _fundidoRoutine = StartCoroutine(FeedbackService.ScreenFadeAsync(color, Mathf.Max(0.01f, duracionFundido), fadeIn: true));
        yield return _fundidoRoutine;
        _fundidoRoutine = null;
        if (!_reproduciendo) yield break;
        t = 0f;
        while (t < sostenEnBlanco && _reproduciendo)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        DOTween.Kill(this);
        DestruirResplandor();
        _reproduciendo = false;
    }

    private void PrepararBotones(Selectable pulsado)
    {
        Transform contenedor = contenedorBotones ? contenedorBotones : pulsado.transform.parent;
        var botones = contenedor ? contenedor.GetComponentsInChildren<Selectable>(true) : new[] { pulsado };
        foreach (var boton in botones)
        {
            var visual = boton.GetComponent<UISelectVisual>();
            if (visual && visual.enabled)
            {
                visual.enabled = false;
                _restauraciones.Add(() => { if (visual) visual.enabled = true; });
            }
            if (boton == pulsado) continue;
            var grupo = boton.GetComponent<CanvasGroup>();
            bool creado = !grupo;
            if (creado) grupo = boton.gameObject.AddComponent<CanvasGroup>();
            float alfa = grupo.alpha;
            _restauraciones.Add(() =>
            {
                if (!grupo) return;
                if (creado) Destroy(grupo);
                else grupo.alpha = alfa;
            });
            grupo.DOFade(0f, duracionApagado).SetUpdate(true).SetId(this);
        }

        var transicion = pulsado.transition;
        pulsado.transition = Selectable.Transition.None;
        _restauraciones.Add(() => { if (pulsado) pulsado.transition = transicion; });
        if (pulsado.targetGraphic) pulsado.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
        var transformBoton = pulsado.transform;
        Vector3 escala = transformBoton.localScale;
        _restauraciones.Add(() => { if (transformBoton) transformBoton.localScale = escala; });
        DOTween.Sequence().SetUpdate(true).SetId(this)
            .Append(transformBoton.DOScale(escala * escalaPunch, 0.12f).SetEase(Ease.OutQuad))
            .Append(transformBoton.DOScale(escala, 0.22f).SetEase(Ease.OutCubic));
        foreach (var grafico in pulsado.GetComponentsInChildren<Graphic>(true))
        {
            Color original = grafico.color;
            _restauraciones.Add(() => { if (grafico) grafico.color = original; });
            if (grafico is TMPro.TMP_Text || grafico is Text)
                grafico.DOColor(Color.white, 0.12f).SetUpdate(true).SetId(this);
        }

        var canvas = pulsado.GetComponentInParent<Canvas>();
        if (!canvas) return;
        var go = new GameObject("Resplandor de comienzo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _resplandor = go.GetComponent<Image>();
        var rect = _resplandor.rectTransform;
        rect.SetParent(canvas.transform, false);
        rect.SetAsLastSibling();
        var rectBoton = pulsado.transform as RectTransform;
        rect.position = rectBoton ? rectBoton.TransformPoint(rectBoton.rect.center) : pulsado.transform.position;
        rect.sizeDelta = Vector2.one * tamanoResplandor;
        rect.localScale = Vector3.one * 0.03f;
        _resplandor.sprite = _sprite;
        _resplandor.raycastTarget = false;
        _resplandor.color = new Color(1f, 1f, 1f, 0.8f);
        rect.DOScale(Vector3.one, duracionResplandor).SetEase(Ease.OutQuad).SetUpdate(true).SetId(this);
        _resplandor.DOFade(0f, duracionResplandor).SetEase(Ease.InQuad).SetUpdate(true).SetId(this);
    }

    private void DestruirResplandor()
    {
        if (_resplandor) Destroy(_resplandor.gameObject);
        _resplandor = null;
    }

    void OnDisable()
    {
        _reproduciendo = false;
        if (_fundidoRoutine != null)
        {
            StopCoroutine(_fundidoRoutine);
            _fundidoRoutine = null;
        }
        DOTween.Kill(this);
        DestruirResplandor();
        for (int i = _restauraciones.Count - 1; i >= 0; i--) _restauraciones[i]();
        _restauraciones.Clear();
    }

    void OnDestroy()
    {
        if (_sprite) Destroy(_sprite);
        if (_textura) Destroy(_textura);
    }
}
