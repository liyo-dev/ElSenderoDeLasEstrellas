#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Foto de lo que se ve en Play (mundo e interfaz) sin depender de que el Game view se repinte:
/// la cámara principal se dibuja en una textura y los lienzos de pantalla, en otra con fondo
/// transparente, pasados un momento a «Screen Space - Camera»; luego se juntan. Sirve con el
/// Editor en segundo plano (orden «pantallazo» del buzón).
/// </summary>
public static class PantallazoDeJuego
{
    public static string Capturar(string destino, int ancho = 1920, int alto = 1080)
    {
        var mundo = Dibujar(Camera.main, ancho, alto, Color.black);
        var interfaz = DibujarInterfaz(ancho, alto);
        var px = mundo.GetPixels32();
        var ui = interfaz.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            // La interfaz sale premultiplicada sobre transparente: color + mundo × (1 − alfa).
            float a = ui[i].a / 255f;
            px[i] = new Color32((byte)Mathf.Min(255, ui[i].r + px[i].r * (1f - a)),
                                (byte)Mathf.Min(255, ui[i].g + px[i].g * (1f - a)),
                                (byte)Mathf.Min(255, ui[i].b + px[i].b * (1f - a)), 255);
        }
        mundo.SetPixels32(px);
        File.WriteAllBytes(destino, mundo.EncodeToPNG());
        Object.DestroyImmediate(mundo);
        Object.DestroyImmediate(interfaz);
        return destino;
    }

    private static Texture2D Dibujar(Camera camara, int ancho, int alto, Color fondo)
    {
        var rt = RenderTexture.GetTemporary(ancho, alto, 24, RenderTextureFormat.ARGB32);
        var anterior = RenderTexture.active;
        if (camara != null)
        {
            var destinoAntes = camara.targetTexture;
            camara.targetTexture = rt;
            camara.Render();
            camara.targetTexture = destinoAntes;
        }
        else
        {
            RenderTexture.active = rt;
            GL.Clear(true, true, fondo);
        }
        RenderTexture.active = rt;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        tex.Apply();
        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    private static Texture2D DibujarInterfaz(int ancho, int alto)
    {
        var go = new GameObject("Cámara del pantallazo");
        var camara = go.AddComponent<Camera>();
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camara.orthographic = true;
        camara.nearClipPlane = 0.01f;
        camara.farClipPlane = 100f;
        camara.enabled = false;
        go.transform.position = new Vector3(0f, -10000f, 0f);

        var cambiados = new List<(Canvas lienzo, RenderMode modo, Camera camara, float distancia)>();
        int mascara = 0;
        var lienzos = Object.FindObjectsByType<Canvas>();
        System.Array.Sort(lienzos, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        foreach (var c in lienzos)
        {
            if (!c.isRootCanvas || !c.isActiveAndEnabled || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            cambiados.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = camara;
            c.planeDistance = 1f + (1000 - Mathf.Clamp(c.sortingOrder, -999, 999)) * 0.01f;
            mascara |= 1 << c.gameObject.layer;
        }
        camara.cullingMask = mascara;
        var rt = RenderTexture.GetTemporary(ancho, alto, 24, RenderTextureFormat.ARGB32);
        camara.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        camara.Render();
        var anterior = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        tex.Apply();
        RenderTexture.active = anterior;
        camara.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        foreach (var (lienzo, modo, cam, distancia) in cambiados)
        {
            if (lienzo == null) continue;
            lienzo.renderMode = modo;
            lienzo.worldCamera = cam;
            lienzo.planeDistance = distancia;
        }
        Object.DestroyImmediate(go);
        return tex;
    }
}
#endif
