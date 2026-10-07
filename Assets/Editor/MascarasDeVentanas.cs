using System.IO;
using UnityEditor;
using UnityEngine;

/// Genera las máscaras de emisión de ventanas (INC-657) a partir de los atlas del pack Fantasy
/// Kingdom: dentro de los rectángulos donde el atlas pinta ventanas, puertas y faroles, marca los
/// píxeles de cristal. Cristal azul, morado o amarillo → luz de vela; vidriera → su propio color
/// saturado; el resto, negro. Después dilata 1 px y suaviza para que el borde no haga escalera, y
/// guarda un PNG de 1024² con el mismo UV que el atlas. Lo usa NocheDelMundoWiring.
public static class MascarasDeVentanas
{
    private delegate bool Criterio(int r, int g, int b);

    private readonly struct Zona
    {
        public readonly RectInt rect;   // en píxeles de un atlas de 2048, origen arriba a la izquierda
        public readonly Criterio esCristal;
        public readonly bool colorPropio;

        public Zona(int x0, int y0, int x1, int y1, Criterio esCristal, bool colorPropio = false)
        {
            rect = new RectInt(x0, y0, x1 - x0, y1 - y0);
            this.esCristal = esCristal;
            this.colorPropio = colorPropio;
        }
    }

    private static readonly Color32 Vela = new Color32(255, 179, 92, 255);
    private const int LadoDeReferencia = 2048;
    private const int LadoDeSalida = 1024;

    private static int Max(int r, int g, int b) => Mathf.Max(r, Mathf.Max(g, b));
    private static int Min(int r, int g, int b) => Mathf.Min(r, Mathf.Min(g, b));

    private static bool Azul(int r, int g, int b) => b > r + 30 && Max(r, g, b) - Min(r, g, b) > 45;
    private static bool Madera(int r, int g, int b) => r >= g && g >= b && r - b > 50 && Max(r, g, b) < 215;
    private static bool Vidriera(int r, int g, int b) => Max(r, g, b) - Min(r, g, b) > 90 && Max(r, g, b) > 175 && !Madera(r, g, b);
    private static bool Amarillo(int r, int g, int b) => r > 200 && g > 150 && b < 170 && r - b > 90;
    private static bool Morado(int r, int g, int b) => b > r + 35 && b > g + 35;
    private static bool Escaparate(int r, int g, int b) => !Madera(r, g, b) && Max(r, g, b) > 150;

    /// Zonas de cristal de cada atlas, por nombre de textura. Las de color propio van al final:
    /// donde se solapan, mandan.
    private static Zona[] ZonasDe(string atlas)
    {
        switch (atlas)
        {
            case "FK01":
                return new[]
                {
                    new Zona(0, 512, 512, 1024, Azul),        // ventanas y puertas
                    new Zona(512, 1536, 1024, 1792, Azul),    // celosías y cristaleras
                    new Zona(512, 1536, 760, 2048, Azul),     // puertas con ojo de buey
                    new Zona(222, 767, 368, 1024, Vidriera, true),
                    new Zona(540, 1820, 625, 1910, Vidriera, true),
                    new Zona(660, 1910, 725, 2030, Vidriera, true),
                };
            case "FK02":
                return new[]
                {
                    new Zona(140, 680, 235, 760, Amarillo),   // farol de calle
                    new Zona(0, 1024, 170, 1536, Amarillo),   // faroles colgantes
                    new Zona(512, 1280, 690, 1470, Morado),   // ventanas
                    new Zona(940, 1075, 1125, 1265, Morado),
                };
            case "FK04":
                return new[]
                {
                    new Zona(0, 0, 2048, 340, Escaparate),    // escaparates de las tiendas
                    new Zona(0, 512, 512, 850, Escaparate),
                };
            default:
                return null;
        }
    }

    /// Crea (o rehace) la máscara de `atlas` en `rutaSalida`. Devuelve false si no hay zonas o
    /// no se puede leer la textura.
    public static bool Generar(Texture2D atlas, string rutaSalida)
    {
        var zonas = atlas != null ? ZonasDe(atlas.name.Replace("_D", "")) : null;
        if (zonas == null) return false;

        int w = atlas.width, h = atlas.height;
        var origen = Leer(atlas);
        var salida = new Color32[w * h];
        float escala = (float)w / LadoDeReferencia;

        foreach (var zona in zonas)
        {
            int x0 = Mathf.RoundToInt(zona.rect.xMin * escala), x1 = Mathf.RoundToInt(zona.rect.xMax * escala);
            int y0 = Mathf.RoundToInt(zona.rect.yMin * escala), y1 = Mathf.RoundToInt(zona.rect.yMax * escala);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
            {
                int fila = (h - 1 - y) * w;   // las texturas de Unity empiezan por abajo
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                {
                    var c = origen[fila + x];
                    if (!zona.esCristal(c.r, c.g, c.b)) continue;
                    salida[fila + x] = zona.colorPropio ? Saturado(c) : Vela;
                }
            }
        }

        salida = Suavizar(Suavizar(Dilatar(salida, w, h), w, h), w, h);
        while (w > LadoDeSalida) salida = Reducir(salida, ref w, ref h);

        var textura = new Texture2D(w, h, TextureFormat.RGBA32, false);
        textura.SetPixels32(salida);
        textura.Apply();
        string carpeta = Path.GetDirectoryName(rutaSalida).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(carpeta))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(carpeta).Replace('\\', '/'), Path.GetFileName(carpeta));
        File.WriteAllBytes(rutaSalida, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(rutaSalida, ImportAssetOptions.ForceUpdate);
        return true;
    }

    /// Lee los píxeles tal como están guardados (sRGB) aunque la textura no sea legible.
    private static Color32[] Leer(Texture2D atlas)
    {
        var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var anterior = RenderTexture.active;
        Graphics.Blit(atlas, rt);
        RenderTexture.active = rt;
        var copia = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, false);
        copia.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0, false);
        copia.Apply(false);
        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(rt);
        var pixeles = copia.GetPixels32();
        Object.DestroyImmediate(copia);
        return pixeles;
    }

    private static Color32 Saturado(Color32 c)
    {
        float m = Mathf.Max(1, Max(c.r, c.g, c.b));
        return new Color32((byte)(c.r / m * 255f), (byte)(c.g / m * 255f), (byte)(c.b / m * 255f), 255);
    }

    /// Máximo por canal en 3×3.
    private static Color32[] Dilatar(Color32[] p, int w, int h)
    {
        var o = new Color32[p.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte r = 0, g = 0, b = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        if (xx < 0 || xx >= w) continue;
                        var c = p[yy * w + xx];
                        if (c.r > r) r = c.r;
                        if (c.g > g) g = c.g;
                        if (c.b > b) b = c.b;
                    }
                }
                o[y * w + x] = new Color32(r, g, b, 255);
            }
        return o;
    }

    /// Media 3×3 (dos pasadas se parecen a un desenfoque gaussiano de ~1 px).
    private static Color32[] Suavizar(Color32[] p, int w, int h)
    {
        var o = new Color32[p.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0, n = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        if (xx < 0 || xx >= w) continue;
                        var c = p[yy * w + xx];
                        r += c.r; g += c.g; b += c.b; n++;
                    }
                }
                o[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
        return o;
    }

    /// Reduce a la mitad promediando bloques de 2×2.
    private static Color32[] Reducir(Color32[] p, ref int w, ref int h)
    {
        int nw = w / 2, nh = h / 2;
        var o = new Color32[nw * nh];
        for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
            {
                var a = p[(2 * y) * w + 2 * x];
                var b = p[(2 * y) * w + 2 * x + 1];
                var c = p[(2 * y + 1) * w + 2 * x];
                var d = p[(2 * y + 1) * w + 2 * x + 1];
                o[y * nw + x] = new Color32(
                    (byte)((a.r + b.r + c.r + d.r) / 4),
                    (byte)((a.g + b.g + c.g + d.g) / 4),
                    (byte)((a.b + b.b + c.b + d.b) / 4), 255);
            }
        w = nw;
        h = nh;
        return o;
    }
}
