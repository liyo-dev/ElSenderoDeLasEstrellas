using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ver contexto en claude/propuesta-separacion-zonas-lore-vegetacion-isla-piedra-ancestral-2026-09-11.md
/// (proyecto de Claude).
///
/// v4 — dos correcciones sobre la v3, descubiertas al revisar el pack desde fuera de Unity:
///
/// 1. Las texturas del pack son .TGA, no PNG: `Texture2D.LoadImage` solo entiende PNG/JPG, así que
///    la v3 habría fallado en todos los prefabs ("LoadImage falló"). Esta versión trae un decodificador
///    TGA mínimo propio (24/32 bpp, sin comprimir y RLE, que es lo que usa el pack).
///
/// 2. El atlas `Tree_D` NO es una rejilla de muestras de color: son "islas" con forma de hoja, tronco,
///    setas… de distintos colores repartidas por toda la textura. Muestrear la caja envolvente de las UV
///    (v3) mezcla islas vecinas y da colores falsos. Ahora se muestrea DENTRO de cada triángulo de la
///    malla (puntos baricéntricos aleatorios, ponderados por área UV) y se agrupan los colores en 3
///    clústeres (k-means), de forma que el informe distingue p. ej. "copa turquesa 70 % + tronco marrón 30 %".
///
/// Sigue sin tocar ningún ajuste de import: lee el archivo de imagen original del disco a una textura
/// temporal en memoria, la muestrea y la destruye. Solo lectura. Genera
/// Assets/Art/World/Fantasy_Kingdom_Pack/VegetacionPaletaInforme.md, agrupado por familia.
///
/// Uso: El Sendero → Mundo → Catalogar Paleta de Vegetación (Fantasy Kingdom Pack)
/// Idempotente (solo lee y sobrescribe el mismo informe cada vez).
/// </summary>
public static class VegetacionPaletaCatalogador
{
    private const string CarpetaVegetacion = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation";
    private const string RutaInforme = "Assets/Art/World/Fantasy_Kingdom_Pack/VegetacionPaletaInforme.md";

    private static readonly string[] PropiedadesTexturaMaterial = { "_BaseMap", "_MainTex", "_BaseColorMap" };

    private const int MuestrasPorTriangulo = 6;
    private const int Clusteres = 3;

    private struct Muestra { public Color C; public float Peso; }

    private struct Fila
    {
        public string Familia;
        public string Prefab;
        public string Texturas;
        public List<(Color color, float fraccion)> Clusteres;
        public string Nota;
    }

    // Cache de texturas cargadas desde disco (una por ruta de asset), para no releer/decodificar el
    // mismo archivo cientos de veces cuando muchos prefabs comparten la misma textura-atlas.
    private static readonly Dictionary<string, Texture2D> _cacheTexturas = new Dictionary<string, Texture2D>();

    [MenuItem("El Sendero/Mundo/Catalogar Paleta de Vegetación (Fantasy Kingdom Pack)")]
    public static void Catalogar()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CarpetaVegetacion });
        if (guids.Length == 0)
        {
            Debug.LogError($"[VegetacionPaletaCatalogador] No se encontró ningún prefab en '{CarpetaVegetacion}'. Revisa la ruta.");
            return;
        }

        _cacheTexturas.Clear();
        var filas = new List<Fila>();

        try
        {
            foreach (string guid in guids)
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (prefab == null) continue;

                var fila = new Fila
                {
                    Familia = ObtenerFamilia(prefab.name),
                    Prefab = prefab.name,
                    Clusteres = new List<(Color, float)>()
                };

                var muestras = new List<Muestra>();
                var texturas = new List<string>();
                var notas = new List<string>();
                int partes = 0;

                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var renderer = mf.GetComponent<Renderer>();
                    var mat = renderer != null ? renderer.sharedMaterial : null;
                    if (mat == null) continue;
                    partes++;

                    string nota = MuestrearMalla(mf.sharedMesh, mat, muestras, out string nombreTextura);
                    if (nota != null) notas.Add(nota);
                    if (nombreTextura != null && !texturas.Contains(nombreTextura)) texturas.Add(nombreTextura);
                }

                if (partes == 0)
                {
                    fila.Nota = "sin MeshFilter+Renderer con material";
                }
                else
                {
                    fila.Clusteres = KMeans(muestras, Clusteres);
                    fila.Nota = $"{partes} parte(s)" + (notas.Count > 0 ? ": " + string.Join(" | ", notas) : "");
                }
                fila.Texturas = string.Join("+", texturas);
                filas.Add(fila);
            }
        }
        finally
        {
            foreach (var tex in _cacheTexturas.Values)
            {
                if (tex != null) Object.DestroyImmediate(tex);
            }
            _cacheTexturas.Clear();
        }

        filas = filas.OrderBy(f => f.Familia).ThenBy(f => f.Prefab).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Paleta de vegetación — Fantasy_Kingdom_Pack (catálogo automático v4)");
        sb.AppendLine();
        sb.AppendLine($"Generado por `VegetacionPaletaCatalogador`. {filas.Count} prefabs encontrados en `{CarpetaVegetacion}`.");
        sb.AppendLine();
        sb.AppendLine("Cada fila muestrea la textura base del material DENTRO de los triángulos de la malla (no la caja");
        sb.AppendLine("envolvente de las UV) y agrupa los colores en 3 clústeres con su porcentaje de superficie UV.");
        sb.AppendLine("Las texturas se leen del archivo original en disco (TGA), sin tocar el import de Unity.");
        sb.AppendLine();
        sb.AppendLine("| Familia | Prefab | Textura | Color dominante | % | 2º color | % | 3º color | % | Nota |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var f in filas)
        {
            var celdas = new List<string>();
            foreach (var (color, fraccion) in f.Clusteres.Take(3))
            {
                celdas.Add($"{ColorAHex(color)} {NombreColor(color)}");
                celdas.Add($"{fraccion * 100f:F0}");
            }
            while (celdas.Count < 6) celdas.Add("");
            sb.AppendLine($"| {f.Familia} | {f.Prefab} | {f.Texturas} | {string.Join(" | ", celdas)} | {f.Nota} |");
        }

        File.WriteAllText(RutaInforme, sb.ToString());
        AssetDatabase.ImportAsset(RutaInforme);
        Debug.Log($"[VegetacionPaletaCatalogador] Informe generado con {filas.Count} prefabs → {RutaInforme}");
    }

    private static string ObtenerFamilia(string nombrePrefab)
    {
        int idx = nombrePrefab.IndexOf('_');
        return idx > 0 ? nombrePrefab.Substring(0, idx) : nombrePrefab;
    }

    /// <summary>
    /// Añade a <paramref name="muestras"/> puntos de color tomados dentro de cada triángulo de la malla.
    /// Devuelve una nota de error (o null si todo fue bien) y el nombre de la textura usada.
    /// </summary>
    private static string MuestrearMalla(Mesh mesh, Material mat, List<Muestra> muestras, out string nombreTextura)
    {
        nombreTextura = null;

        Texture materialTex = null;
        foreach (var prop in PropiedadesTexturaMaterial)
        {
            if (mat.HasProperty(prop) && mat.GetTexture(prop) != null)
            {
                materialTex = mat.GetTexture(prop);
                break;
            }
        }
        if (materialTex == null) return $"material '{mat.name}' sin textura base reconocida";
        nombreTextura = materialTex.name;

        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0) return $"malla '{mesh.name}' sin UV";
        int[] tris = mesh.triangles;
        if (tris == null || tris.Length < 3) return $"malla '{mesh.name}' sin triángulos";

        Texture2D tex = CargarTexturaDesdeDisco(materialTex, out string errorCarga);
        if (tex == null) return $"'{materialTex.name}': {errorCarga}";

        var rnd = new System.Random(1);
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            Vector2 a = uvs[tris[t]], b = uvs[tris[t + 1]], c = uvs[tris[t + 2]];
            float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            if (area <= 0f) continue;

            for (int i = 0; i < MuestrasPorTriangulo; i++)
            {
                float r1 = (float)rnd.NextDouble(), r2 = (float)rnd.NextDouble();
                if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                Vector2 uv = a + r1 * (b - a) + r2 * (c - a);
                Color col = tex.GetPixelBilinear(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f));
                if (col.a < 0.5f) continue; // zonas transparentes del atlas no cuentan
                muestras.Add(new Muestra { C = col, Peso = area / MuestrasPorTriangulo });
            }
        }
        return null;
    }

    /// <summary>k-means sencillo en RGB, ponderado por área UV. Devuelve clústeres ordenados por peso.</summary>
    private static List<(Color, float)> KMeans(List<Muestra> muestras, int k)
    {
        var resultado = new List<(Color, float)>();
        if (muestras.Count == 0) return resultado;

        var rnd = new System.Random(2);
        int n = Mathf.Min(k, muestras.Count);
        var centros = new List<Color>();
        for (int i = 0; i < n; i++) centros.Add(muestras[rnd.Next(muestras.Count)].C);

        var asignacion = new int[muestras.Count];
        for (int iter = 0; iter < 12; iter++)
        {
            for (int m = 0; m < muestras.Count; m++)
            {
                float mejor = float.MaxValue; int idx = 0;
                for (int c = 0; c < centros.Count; c++)
                {
                    float d = Dist2(muestras[m].C, centros[c]);
                    if (d < mejor) { mejor = d; idx = c; }
                }
                asignacion[m] = idx;
            }
            for (int c = 0; c < centros.Count; c++)
            {
                float r = 0, g = 0, b = 0, w = 0;
                for (int m = 0; m < muestras.Count; m++)
                {
                    if (asignacion[m] != c) continue;
                    r += muestras[m].C.r * muestras[m].Peso;
                    g += muestras[m].C.g * muestras[m].Peso;
                    b += muestras[m].C.b * muestras[m].Peso;
                    w += muestras[m].Peso;
                }
                if (w > 0f) centros[c] = new Color(r / w, g / w, b / w);
            }
        }

        float total = muestras.Sum(m => m.Peso);
        for (int c = 0; c < centros.Count; c++)
        {
            float w = 0f;
            for (int m = 0; m < muestras.Count; m++) if (asignacion[m] == c) w += muestras[m].Peso;
            if (w > 0f) resultado.Add((centros[c], w / total));
        }
        return resultado.OrderByDescending(x => x.Item2).ToList();
    }

    private static float Dist2(Color a, Color b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    /// <summary>
    /// Carga la imagen original de un asset de textura directamente del disco, evitando el
    /// isReadable del asset importado. PNG/JPG vía LoadImage; TGA con el decodificador propio de abajo.
    /// Cachea por ruta para no releer el mismo archivo varias veces.
    /// </summary>
    private static Texture2D CargarTexturaDesdeDisco(Texture materialTex, out string error)
    {
        error = null;
        string rutaAsset = AssetDatabase.GetAssetPath(materialTex);
        if (string.IsNullOrEmpty(rutaAsset))
        {
            error = "sin ruta de asset (¿textura generada en memoria?)";
            return null;
        }

        if (_cacheTexturas.TryGetValue(rutaAsset, out Texture2D cacheada))
        {
            return cacheada;
        }

        if (!rutaAsset.StartsWith("Assets/"))
        {
            error = $"ruta fuera de Assets/ ('{rutaAsset}'), no se puede resolver a disco";
            return null;
        }

        string rutaAbsoluta = Path.Combine(Application.dataPath, rutaAsset.Substring("Assets/".Length));
        if (!File.Exists(rutaAbsoluta))
        {
            error = $"archivo no encontrado en disco ('{rutaAbsoluta}')";
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(rutaAbsoluta);
        }
        catch (System.Exception e)
        {
            error = $"error leyendo archivo: {e.Message}";
            return null;
        }

        Texture2D temp;
        string ext = Path.GetExtension(rutaAbsoluta).ToLowerInvariant();
        if (ext == ".tga")
        {
            temp = DecodificarTga(bytes, out string errorTga);
            if (temp == null)
            {
                error = $"TGA no soportado: {errorTga}";
                return null;
            }
        }
        else
        {
            temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!temp.LoadImage(bytes))
            {
                Object.DestroyImmediate(temp);
                error = "LoadImage falló (formato no soportado o archivo corrupto)";
                return null;
            }
        }

        _cacheTexturas[rutaAsset] = temp;
        return temp;
    }

    /// <summary>
    /// Decodificador TGA mínimo: tipo 2 (truecolor sin comprimir) y tipo 10 (truecolor RLE), 24 o 32 bpp,
    /// con origen abajo-izquierda (por defecto) o arriba-izquierda (bit 5 del descriptor). Es lo que usa
    /// el pack; cualquier otra variante devuelve null con explicación.
    /// </summary>
    private static Texture2D DecodificarTga(byte[] d, out string error)
    {
        error = null;
        if (d.Length < 18) { error = "cabecera incompleta"; return null; }

        int idLength = d[0];
        int colorMapType = d[1];
        int imageType = d[2];
        int width = d[12] | (d[13] << 8);
        int height = d[14] | (d[15] << 8);
        int bpp = d[16];
        bool origenArriba = (d[17] & 0x20) != 0;

        if (colorMapType != 0) { error = "con paleta de colores"; return null; }
        if (imageType != 2 && imageType != 10) { error = $"tipo de imagen {imageType}"; return null; }
        if (bpp != 24 && bpp != 32) { error = $"{bpp} bpp"; return null; }
        if (width <= 0 || height <= 0) { error = "dimensiones inválidas"; return null; }

        int bytesPorPixel = bpp / 8;
        int pos = 18 + idLength;
        var pixeles = new Color32[width * height];
        int total = width * height;

        // Los píxeles vienen en orden de filas desde el origen indicado; Unity espera la fila 0 abajo.
        int i = 0;
        if (imageType == 2)
        {
            while (i < total)
            {
                if (pos + bytesPorPixel > d.Length) { error = "datos truncados"; return null; }
                pixeles[i++] = LeerPixel(d, pos, bytesPorPixel);
                pos += bytesPorPixel;
            }
        }
        else
        {
            while (i < total)
            {
                if (pos >= d.Length) { error = "datos RLE truncados"; return null; }
                int cabecera = d[pos++];
                int cuenta = (cabecera & 0x7F) + 1;
                if ((cabecera & 0x80) != 0)
                {
                    if (pos + bytesPorPixel > d.Length) { error = "datos RLE truncados"; return null; }
                    Color32 p = LeerPixel(d, pos, bytesPorPixel);
                    pos += bytesPorPixel;
                    for (int r = 0; r < cuenta && i < total; r++) pixeles[i++] = p;
                }
                else
                {
                    for (int r = 0; r < cuenta && i < total; r++)
                    {
                        if (pos + bytesPorPixel > d.Length) { error = "datos RLE truncados"; return null; }
                        pixeles[i++] = LeerPixel(d, pos, bytesPorPixel);
                        pos += bytesPorPixel;
                    }
                }
            }
        }

        if (origenArriba)
        {
            var volteado = new Color32[total];
            for (int y = 0; y < height; y++)
                System.Array.Copy(pixeles, y * width, volteado, (height - 1 - y) * width, width);
            pixeles = volteado;
        }

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixeles);
        tex.Apply(false, false);
        return tex;
    }

    private static Color32 LeerPixel(byte[] d, int pos, int bytesPorPixel)
    {
        // TGA guarda BGR(A).
        byte b = d[pos], g = d[pos + 1], r = d[pos + 2];
        byte a = bytesPorPixel == 4 ? d[pos + 3] : (byte)255;
        return new Color32(r, g, b, a);
    }

    private static string NombreColor(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h *= 360f;
        if (v < 0.18f) return "negro/muy oscuro";
        if (s < 0.15f) return v > 0.6f ? "gris/blanco" : "gris oscuro";
        if (h < 15f || h >= 345f) return "rojo";
        if (h < 40f) return v < 0.75f ? "marrón/naranja" : "naranja";
        if (h < 65f) return (s < 0.7f || v < 0.7f) ? "amarillo/mostaza" : "amarillo";
        if (h < 95f) return "verde amarillento (lima)";
        if (h < 150f) return "verde";
        if (h < 185f) return "verde azulado (turquesa)";
        if (h < 200f) return "cian";
        if (h < 255f) return "azul";
        if (h < 290f) return "violeta/morado";
        return "rosa/magenta";
    }

    private static string ColorAHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
