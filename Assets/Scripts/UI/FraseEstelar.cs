using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una frase que se escribe sola en dorado, letra a letra, como polvo de estrellas: cada letra
/// aparece con un brillo que se apaga, suelta un par de chispas que suben, y al final la frase
/// entera respira un momento y se desvanece. Para el cierre de una cinemática (la frase final
/// del libro). Se crea, se ve y se destruye sola.
/// </summary>
public class FraseEstelar : MonoBehaviour
{
    private static readonly Color32 OroArriba = new(255, 236, 170, 255);
    private static readonly Color32 OroAbajo = new(232, 168, 64, 255);
    private const int OrdenDelLienzo = 500;

    private TextMeshProUGUI _texto;
    private CanvasGroup _grupo;
    private RectTransform _capaDeChispas;
    private Sprite _chispa;
    private float _aparecer, _mantener, _desaparecer;

    /// <summary>
    /// Muestra <paramref name="frase"/>: se escribe en <paramref name="aparecer"/> segundos, se
    /// queda <paramref name="mantener"/> y se apaga en <paramref name="desaparecer"/>. Segundos reales.
    /// </summary>
    public static FraseEstelar Mostrar(string frase, TMP_FontAsset fuente, float aparecer, float mantener, float desaparecer)
    {
        var go = new GameObject("Frase_Estelar");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenDelLienzo;
        var escalador = go.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        var f = go.AddComponent<FraseEstelar>();
        f.Construir(frase, fuente, aparecer, mantener, desaparecer);
        return f;
    }

    private void Construir(string frase, TMP_FontAsset fuente, float aparecer, float mantener, float desaparecer)
    {
        _aparecer = Mathf.Max(0.5f, aparecer);
        _mantener = Mathf.Max(0f, mantener);
        _desaparecer = Mathf.Max(0.1f, desaparecer);
        _grupo = gameObject.AddComponent<CanvasGroup>();
        _grupo.blocksRaycasts = false;

        var goTexto = new GameObject("Texto");
        goTexto.transform.SetParent(transform, false);
        _texto = goTexto.AddComponent<TextMeshProUGUI>();
        if (fuente != null) _texto.font = fuente;
        _texto.text = frase;
        _texto.fontSize = 46f;
        _texto.alignment = TextAlignmentOptions.Center;
        _texto.characterSpacing = 1.5f;
        _texto.lineSpacing = 12f;
        _texto.textWrappingMode = TextWrappingModes.Normal;
        _texto.raycastTarget = false;
        _texto.enableVertexGradient = true;
        _texto.colorGradient = new VertexGradient(OroArriba, OroArriba, OroAbajo, OroAbajo);
        var rt = _texto.rectTransform;
        // En el tercio de abajo, por encima de la banda de cine: la constelación queda libre en el centro.
        rt.anchorMin = new Vector2(0.1f, 0.16f);
        rt.anchorMax = new Vector2(0.9f, 0.36f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Un halo dorado suave detrás de las letras (si el material de la fuente lo admite).
        var mat = _texto.fontMaterial;
        if (mat != null && mat.HasProperty(ShaderUtilities.ID_GlowColor))
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
            mat.SetColor(ShaderUtilities.ID_GlowColor, new Color(1f, 0.78f, 0.35f, 0.55f));
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.35f);
            mat.SetFloat(ShaderUtilities.ID_GlowPower, 0.6f);
        }
        // Una sombra oscura y difusa debajo de las letras para que se lean sobre el cielo.
        if (mat != null && mat.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.03f, 0.03f, 0.08f, 0.85f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
        }

        var capa = new GameObject("Chispas");
        capa.transform.SetParent(transform, false);
        _capaDeChispas = capa.AddComponent<RectTransform>();
        _capaDeChispas.anchorMin = Vector2.zero;
        _capaDeChispas.anchorMax = Vector2.one;
        _capaDeChispas.offsetMin = _capaDeChispas.offsetMax = Vector2.zero;
        _chispa = CrearSpriteDeChispa();

        StartCoroutine(Co_Escribir());
    }

    private IEnumerator Co_Escribir()
    {
        _texto.ForceMeshUpdate();
        var info = _texto.textInfo;
        int letras = info.characterCount;
        var alfas = new float[letras];
        float porLetra = _aparecer / Mathf.Max(1, letras);
        const float fundidoDeLetra = 0.6f;
        int ultimaConChispa = -1;

        float t = 0f;
        while (t < _aparecer + fundidoDeLetra)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < letras; i++)
            {
                float inicio = i * porLetra;
                alfas[i] = Mathf.Clamp01((t - inicio) / fundidoDeLetra);
                if (alfas[i] > 0f && i > ultimaConChispa)
                {
                    ultimaConChispa = i;
                    if (info.characterInfo[i].isVisible && i % 2 == 0) SoltarChispas(info.characterInfo[i]);
                }
            }
            PintarLetras(alfas, t);
            yield return null;
        }
        for (int i = 0; i < letras; i++) alfas[i] = 1f;

        for (float m = 0f; m < _mantener; m += Time.unscaledDeltaTime)
        {
            PintarLetras(alfas, t + m);
            yield return null;
        }
        for (float d = 0f; d < _desaparecer; d += Time.unscaledDeltaTime)
        {
            _grupo.alpha = 1f - Mathf.SmoothStep(0f, 1f, d / _desaparecer);
            PintarLetras(alfas, t + _mantener + d);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// Cada letra con su alfa; las que acaban de salir brillan más (blanco) y se asientan en oro.
    private void PintarLetras(float[] alfas, float tiempo)
    {
        var info = _texto.textInfo;
        for (int i = 0; i < alfas.Length && i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            var colores = info.meshInfo[c.materialReferenceIndex].colors32;
            float a = alfas[i];
            float brillo = Mathf.Clamp01(1f - a) * 0.8f + 0.06f * Mathf.Sin(tiempo * 2.4f + i * 0.35f);
            Color32 arriba = Color32.Lerp(OroArriba, new Color32(255, 255, 255, 255), Mathf.Clamp01(brillo));
            Color32 abajo = Color32.Lerp(OroAbajo, new Color32(255, 240, 200, 255), Mathf.Clamp01(brillo));
            byte alfa = (byte)(255 * Mathf.SmoothStep(0f, 1f, a));
            arriba.a = alfa; abajo.a = alfa;
            int v = c.vertexIndex;
            colores[v] = abajo;
            colores[v + 1] = arriba;
            colores[v + 2] = arriba;
            colores[v + 3] = abajo;
        }
        _texto.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private void SoltarChispas(TMP_CharacterInfo letra)
    {
        Vector3 centroLocal = (letra.topLeft + letra.bottomRight) * 0.5f;
        Vector3 mundo = _texto.rectTransform.TransformPoint(centroLocal);
        for (int k = 0; k < 2; k++) StartCoroutine(Co_Chispa(mundo));
    }

    private IEnumerator Co_Chispa(Vector3 mundo)
    {
        var go = new GameObject("Chispa");
        go.transform.SetParent(_capaDeChispas, false);
        var img = go.AddComponent<Image>();
        img.sprite = _chispa;
        img.raycastTarget = false;
        float tam = Random.Range(10f, 22f);
        img.rectTransform.sizeDelta = new Vector2(tam, tam);
        go.transform.position = mundo;
        Vector3 deriva = new Vector3(Random.Range(-30f, 30f), Random.Range(30f, 80f), 0f);
        float vida = Random.Range(0.9f, 1.6f);
        Color color = Random.value < 0.5f ? new Color(1f, 0.92f, 0.62f) : new Color(1f, 1f, 0.9f);
        for (float t = 0f; t < vida; t += Time.unscaledDeltaTime)
        {
            float k = t / vida;
            go.transform.position = mundo + deriva * k;
            img.color = new Color(color.r, color.g, color.b, Mathf.Sin(k * Mathf.PI));
            go.transform.localScale = Vector3.one * (1f - k * 0.5f);
            yield return null;
        }
        Destroy(go);
    }

    private static Sprite CrearSpriteDeChispa()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Chispa de frase", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float cruz = Mathf.Clamp01(1f - Mathf.Abs(dx) * 10f) * Mathf.Clamp01(1f - Mathf.Abs(dy)) + Mathf.Clamp01(1f - Mathf.Abs(dy) * 10f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 3f) + cruz * 0.6f));
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (_chispa != null)
        {
            Destroy(_chispa.texture);
            Destroy(_chispa);
        }
    }
}
