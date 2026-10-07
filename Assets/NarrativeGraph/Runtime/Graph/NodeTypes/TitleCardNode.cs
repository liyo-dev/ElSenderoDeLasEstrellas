using System;
using System.Collections;
using Sendero.Core.Feedback;
using TMPro;
using UnityEngine;

/// Rótulo centrado sobre la pantalla tapada, del tipo «A la mañana siguiente…».
///
/// Si la pantalla no está tapada, primero funde a 'fondo'. El rótulo aparece, se mantiene y se
/// desvanece; la pantalla se queda tapada para quien venga después (una cinemática la destapa
/// en su primer plano, o un ScreenFadeNode).
[Serializable]
[NarrativeNodeInfo("Mundo", "Rótulo sobre la pantalla", "Texto centrado sobre la pantalla tapada, p. ej. «A la mañana siguiente…».")]
public sealed class TitleCardNode : NarrativeNode
{
    [Tooltip("Texto de respaldo si no hay ID de localización o no se encuentra.")]
    public string text;
    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    [NarrativeKey(NarrativeKeyKind.LocKey)]
    public string textId;

    public Color color = new Color(1f, 0.83f, 0.45f, 1f);
    [Tooltip("Color con el que se tapa la pantalla si no lo estaba ya.")]
    public Color fondo = Color.black;
    [Tooltip("Vacío = fuente por defecto de TextMeshPro.")]
    public TMP_FontAsset fuente;
    [Min(1f)] public float tamano = 64f;

    [Min(0f)] public float aparecer = 1f;
    [Min(0f)] public float mantener = 2.2f;
    [Min(0f)] public float desaparecer = 1f;

    // Encima del fundido de pantalla (FeedbackService, 9998).
    const int OrdenDeDibujo = 9999;

    [NonSerialized] GameObject _rotulo;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        ctx.Runner.StartCoroutine(Co_Rotulo(onReadyToAdvance));
    }

    public override void Exit(NarrativeContext ctx) => Quitar();

    IEnumerator Co_Rotulo(Action onReadyToAdvance)
    {
        if (!FeedbackService.IsScreenFaded)
            yield return FeedbackService.ScreenFadeAsync(fondo, 0.5f, fadeIn: true);

        string resuelto = string.IsNullOrEmpty(textId) ? text : LocalizationManager.Instance?.Get(textId, text) ?? text;
        var etiqueta = Crear(resuelto);

        yield return Fundir(etiqueta, 0f, 1f, aparecer);
        yield return new WaitForSecondsRealtime(mantener);
        yield return Fundir(etiqueta, 1f, 0f, desaparecer);

        Quitar();
        onReadyToAdvance?.Invoke();
    }

    TextMeshProUGUI Crear(string contenido)
    {
        Quitar();
        _rotulo = new GameObject("Rotulo");
        var canvas = _rotulo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenDeDibujo;
        var escalado = _rotulo.AddComponent<UnityEngine.UI.CanvasScaler>();
        escalado.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalado.referenceResolution = new Vector2(1920f, 1080f);
        escalado.matchWidthOrHeight = 0.5f;

        var textoGo = new GameObject("Texto");
        textoGo.transform.SetParent(_rotulo.transform, false);
        var etiqueta = textoGo.AddComponent<TextMeshProUGUI>();
        if (fuente != null) etiqueta.font = fuente;
        etiqueta.text = contenido;
        etiqueta.fontSize = tamano;
        etiqueta.characterSpacing = 4f;
        etiqueta.alignment = TextAlignmentOptions.Center;
        etiqueta.color = new Color(color.r, color.g, color.b, 0f);
        etiqueta.raycastTarget = false;
        var rt = etiqueta.rectTransform;
        rt.anchorMin = new Vector2(0.1f, 0.3f);
        rt.anchorMax = new Vector2(0.9f, 0.7f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return etiqueta;
    }

    IEnumerator Fundir(TextMeshProUGUI etiqueta, float desde, float hasta, float segundos)
    {
        for (float t = 0f; t < segundos && etiqueta != null; t += Time.unscaledDeltaTime)
        {
            etiqueta.alpha = Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / segundos));
            yield return null;
        }
        if (etiqueta != null) etiqueta.alpha = hasta;
    }

    void Quitar()
    {
        if (_rotulo != null) UnityEngine.Object.Destroy(_rotulo);
        _rotulo = null;
    }
}
