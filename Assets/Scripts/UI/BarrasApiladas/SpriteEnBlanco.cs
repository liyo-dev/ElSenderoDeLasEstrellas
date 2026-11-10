using System.Collections.Generic;
using UnityEngine;

namespace Sendero.UI
{
    /// <summary>
    /// Copia en gris de un sprite de color, con el mismo recorte, pivote y bordes: el tono más claro
    /// queda blanco y el resto conserva su luz y sus sombras. Sirve para teñir de cualquier color una
    /// barra dibujada en un color concreto (las barras apiladas cambian de color por capa). Se hace
    /// una vez por sprite; funciona aunque la textura no sea legible.
    /// </summary>
    public static class SpriteEnBlanco
    {
        private static readonly Dictionary<Sprite, Sprite> s_cache = new();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_cache.Clear(); }
#endif

        public static Sprite De(Sprite origen)
        {
            if (origen == null) return null;
            if (s_cache.TryGetValue(origen, out var hecho) && hecho != null) return hecho;

            var tex = origen.texture;
            var zona = origen.textureRect;
            int ancho = Mathf.RoundToInt(zona.width), alto = Mathf.RoundToInt(zona.height);
            if (tex == null || ancho <= 0 || alto <= 0) return null;

            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var anterior = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copia = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
            copia.ReadPixels(new Rect(zona.x, zona.y, ancho, alto), 0, 0);
            RenderTexture.active = anterior;
            RenderTexture.ReleaseTemporary(rt);

            var px = copia.GetPixels32();
            float maximo = 0f;
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 8) maximo = Mathf.Max(maximo, Luz(px[i]));
            if (maximo <= 0f) maximo = 1f;
            for (int i = 0; i < px.Length; i++)
            {
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Luz(px[i]) / maximo * 255f), 0, 255);
                px[i] = new Color32(g, g, g, px[i].a);
            }
            copia.SetPixels32(px);
            copia.wrapMode = TextureWrapMode.Clamp;
            copia.filterMode = tex.filterMode;
            copia.Apply(false, true);

            var pivote = new Vector2(origen.pivot.x / origen.rect.width, origen.pivot.y / origen.rect.height);
            var blanco = Sprite.Create(copia, new Rect(0, 0, ancho, alto), pivote, origen.pixelsPerUnit, 0,
                                       SpriteMeshType.FullRect, origen.border);
            blanco.name = origen.name + " (blanco)";
            s_cache[origen] = blanco;
            return blanco;
        }

        private static float Luz(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
