using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// El telón negro entre las dos partes y los rótulos («Unas semanas después…», «Fin»). Es del
/// módulo y no del fundido de las secuencias: al encadenar la parte 2 la secuencia nueva quitaba
/// el negro y el rótulo salía encima del río. El telón sobrevive al cambio de parte y lo quita el
/// guion de la parte 2 cuando entra el primer plano.
public partial class ModuloCinematicaFinal
{
    /// Por encima de todo, también del fundido de las secuencias (9998) y del destello (9999): si
    /// no, «fundido cubre» tapaba el rótulo y «Fin» no se veía.
    private const int OrdenDelTelon = 10010;

    private static GameObject s_telon;
    private static Image s_negro;
    private static TextMeshProUGUI s_rotulo;

    private void AsegurarTelon(float opacidadInicial)
    {
        if (s_telon != null) return;
        s_telon = new GameObject("Telon_Negro_Del_Final");
        var canvas = s_telon.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenDelTelon;
        var escalador = s_telon.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 1f;

        s_negro = new GameObject("Negro").AddComponent<Image>();
        s_negro.transform.SetParent(s_telon.transform, false);
        s_negro.raycastTarget = false;
        var rn = s_negro.rectTransform;
        rn.anchorMin = Vector2.zero; rn.anchorMax = Vector2.one; rn.offsetMin = rn.offsetMax = Vector2.zero;
        s_negro.color = new Color(0f, 0f, 0f, opacidadInicial);

        s_rotulo = new GameObject("Rotulo").AddComponent<TextMeshProUGUI>();
        s_rotulo.transform.SetParent(s_telon.transform, false);
        if (_fuenteDeRotulo != null) s_rotulo.font = _fuenteDeRotulo;
        s_rotulo.alignment = TextAlignmentOptions.Center;
        s_rotulo.raycastTarget = false;
        s_rotulo.color = new Color(1f, 0.83f, 0.45f, 0f);
        var rr = s_rotulo.rectTransform;
        rr.anchorMin = new Vector2(0.1f, 0.3f); rr.anchorMax = new Vector2(0.9f, 0.7f); rr.offsetMin = rr.offsetMax = Vector2.zero;
    }

    /// Final de la parte 1: la pantalla se va a negro y se queda así para la parte 2.
    private IEnumerator Co_TelonANegro(float segundos)
    {
        AsegurarTelon(0f);
        float desde = s_negro.color.a;
        for (float t = 0f; t < segundos && s_negro != null; t += Time.unscaledDeltaTime)
        {
            s_negro.color = new Color(0f, 0f, 0f, Mathf.Lerp(desde, 1f, Mathf.SmoothStep(0f, 1f, t / segundos)));
            yield return null;
        }
        if (s_negro != null) s_negro.color = Color.black;
    }

    /// Un rótulo dorado en el centro, sobre negro.
    private IEnumerator Co_RotuloEnNegro(string texto, float tamano, float aparecer, float mantener, float desaparecer)
    {
        AsegurarTelon(1f);
        s_negro.color = Color.black;
        s_rotulo.text = texto;
        s_rotulo.fontSize = tamano;
        Color c = s_rotulo.color;
        for (float t = 0f; t < aparecer && s_rotulo != null; t += Time.unscaledDeltaTime)
        {
            c.a = t / aparecer; s_rotulo.color = c;
            yield return null;
        }
        c.a = 1f; if (s_rotulo != null) s_rotulo.color = c;
        yield return new WaitForSecondsRealtime(mantener);
        for (float t = 0f; t < desaparecer && s_rotulo != null; t += Time.unscaledDeltaTime)
        {
            c.a = 1f - t / desaparecer; s_rotulo.color = c;
            yield return null;
        }
        if (s_rotulo != null) { c.a = 0f; s_rotulo.color = c; }
    }

    /// Entra el primer plano de la parte 2: el negro se abre.
    private IEnumerator Co_QuitarTelon(float segundos)
    {
        if (s_negro == null) yield break;
        float desde = s_negro.color.a;
        for (float t = 0f; t < segundos && s_negro != null; t += Time.unscaledDeltaTime)
        {
            s_negro.color = new Color(0f, 0f, 0f, Mathf.Lerp(desde, 0f, Mathf.SmoothStep(0f, 1f, t / segundos)));
            yield return null;
        }
        DestruirTelon();
    }

    private static void DestruirTelon()
    {
        if (s_telon != null) Destroy(s_telon);
        s_telon = null;
        s_negro = null;
        s_rotulo = null;
    }
}
