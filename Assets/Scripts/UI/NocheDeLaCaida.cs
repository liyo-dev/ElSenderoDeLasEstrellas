using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El momento de caer en combate: la pantalla se va a una noche azul con viñeta y polvo de estrellas
/// que sube despacio desde abajo, mientras la cámara rodea al caído. Lo monta todo por código en un
/// lienzo propio, justo por debajo del de las preguntas (para que la pregunta de continuar quede
/// encima). Ver INC-1101.
/// </summary>
public sealed class NocheDeLaCaida : MonoBehaviour
{
    const int Estrellas = 46;
    static readonly Color Noche = new Color(0.05f, 0.07f, 0.20f, 1f);
    static readonly Color Estrella = new Color(1f, 0.93f, 0.70f, 1f);

    Image _velo, _vineta;
    RectTransform _estrellas;
    Sequence _entrada;
    Texture2D _texVineta, _texPunto;

    /// Crea la noche (sin enseñarla) bajo <paramref name="padre"/>. <paramref name="ordenPorDebajoDe"/>:
    /// el lienzo queda justo debajo de ese orden.
    public static NocheDeLaCaida Crear(Transform padre, int ordenPorDebajoDe)
    {
        var go = new GameObject("[Noche de la caída]", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var lienzo = go.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = ordenPorDebajoDe - 1;
        var escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;
        var noche = go.AddComponent<NocheDeLaCaida>();
        noche.Montar();
        return noche;
    }

    void Montar()
    {
        _texVineta = Vineta(256);
        _texPunto = Punto(32);
        _velo = Capa("Velo", null, Noche);
        _vineta = Capa("Viñeta", Sprite.Create(_texVineta, new Rect(0, 0, _texVineta.width, _texVineta.height), new Vector2(0.5f, 0.5f)), Color.black);
        _estrellas = new GameObject("Estrellas", typeof(RectTransform)).GetComponent<RectTransform>();
        _estrellas.SetParent(transform, false);
        Estirar(_estrellas);
        _velo.color = Transparente(Noche);
        _vineta.color = Transparente(Color.black);
    }

    /// Enseña la noche en <paramref name="segundos"/> (tiempo real).
    public void Mostrar(float segundos)
    {
        _entrada?.Kill();
        _entrada = DOTween.Sequence().SetUpdate(true)
            .Join(_velo.DOFade(0.55f, segundos).SetEase(Ease.InOutSine))
            .Join(_vineta.DOFade(0.9f, segundos * 1.2f).SetEase(Ease.InOutSine));

        var punto = Sprite.Create(_texPunto, new Rect(0, 0, _texPunto.width, _texPunto.height), new Vector2(0.5f, 0.5f));
        var tam = ((RectTransform)transform).rect.size;
        var rnd = new System.Random(1101);
        for (int i = 0; i < Estrellas; i++)
        {
            var img = new GameObject("Estrella", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(_estrellas, false);
            img.sprite = punto;
            img.raycastTarget = false;
            float lado = 6f + (float)rnd.NextDouble() * 12f;
            img.rectTransform.sizeDelta = new Vector2(lado, lado);
            float x = ((float)rnd.NextDouble() - 0.5f) * tam.x * 0.9f;
            float y = -tam.y * 0.5f + (float)rnd.NextDouble() * tam.y * 0.55f;
            img.rectTransform.anchoredPosition = new Vector2(x, y);
            img.color = Transparente(Estrella);
            float retraso = (float)rnd.NextDouble() * segundos;
            float subida = 120f + (float)rnd.NextDouble() * 260f;
            float vida = 3.5f + (float)rnd.NextDouble() * 3f;
            float brillo = 0.45f + (float)rnd.NextDouble() * 0.5f;
            img.DOFade(brillo, 0.8f).SetDelay(retraso).SetUpdate(true);
            img.rectTransform.DOAnchorPosY(y + subida, vida).SetDelay(retraso).SetEase(Ease.OutSine).SetUpdate(true);
            img.DOFade(brillo * 0.35f, 0.6f).SetDelay(retraso + 0.8f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
    }

    void OnDestroy()
    {
        _entrada?.Kill();
        foreach (var g in GetComponentsInChildren<Graphic>(true)) g.DOKill();
        foreach (var r in GetComponentsInChildren<RectTransform>(true)) r.DOKill();
        if (_texVineta != null) Destroy(_texVineta);
        if (_texPunto != null) Destroy(_texPunto);
    }

    Image Capa(string nombre, Sprite sprite, Color color)
    {
        var img = new GameObject(nombre, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(transform, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        Estirar(img.rectTransform);
        return img;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Color Transparente(Color c) { c.a = 0f; return c; }

    /// Negro en los bordes que se aclara hacia el centro.
    static Texture2D Vineta(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                px[y * n + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// Un punto de luz suave.
    static Texture2D Punto(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
