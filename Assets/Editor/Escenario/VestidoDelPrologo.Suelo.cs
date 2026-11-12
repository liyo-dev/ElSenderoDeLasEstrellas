#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// El suelo del pueblo del prólogo pintado (INC-1081): en vez de la senda de triángulos planos del
/// terreno, una textura de tierra batida con los bordes comidos por la hierba y piedrecillas, sobre
/// la pradera (con sus manchas de sol y sombra suavizadas). Se pinta un cuadrado alrededor de la
/// plaza: los triángulos de pradera y senda del terreno que caen dentro pasan a una malla aparte con
/// la textura (sin solaparse con el terreno, que se queda sin ellos). El borde del cuadrado se funde
/// con el color liso de la pradera, así que no se nota dónde acaba.
///
/// La colisión del terreno no cambia (sigue con su malla entera). «Quitar» deja el terreno como estaba.
/// </summary>
public static partial class VestidoDelPrologo
{
    const string Terreno = "Terreno_Costa_Quibli";
    const string NombreDelSueloPintado = "Suelo pintado del pueblo";
    const float LadoDelSuelo = 150f;
    const int PixelesDelSuelo = 2048;
    static readonly Regex EsPrado = new Regex("pradera|hierba|pasto|grass|c[eé]sped|prado", RegexOptions.IgnoreCase);
    static readonly Regex EsSenda = new Regex("senda|camino|path|tierra", RegexOptions.IgnoreCase);

    static readonly Color TierraClara = new Color(0.78f, 0.63f, 0.43f);
    static readonly Color TierraTostada = new Color(0.66f, 0.49f, 0.31f);
    static readonly Color Piedrecilla = new Color(0.55f, 0.50f, 0.46f);

    /// Pinta el suelo de la plaza y las calles. Devuelve las piezas puestas (0 o 1).
    static int PintarSuelo(Scene escena, Transform raiz, StringBuilder informe)
    {
        var terreno = Buscar(escena, Terreno);
        var mf = terreno != null ? terreno.GetComponent<MeshFilter>() : null;
        var mr = terreno != null ? terreno.GetComponent<MeshRenderer>() : null;
        var mc = terreno != null ? terreno.GetComponent<MeshCollider>() : null;
        if (mf == null || mr == null) { informe.AppendLine("Suelo: no encuentro " + Terreno + "."); return 0; }
        Mesh original = mc != null && mc.sharedMesh != null ? mc.sharedMesh : mf.sharedMesh;
        if (original == null || !original.isReadable) { informe.AppendLine("Suelo: la malla del terreno no se puede leer."); return 0; }

        // Lo que se pinta: la pradera (con sus manchas) y la senda. La senda da la máscara de la tierra.
        var mats = mr.sharedMaterials;
        var m = terreno.localToWorldMatrix;
        var verts = original.vertices;
        var mundo = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) mundo[i] = m.MultiplyPoint3x4(verts[i]);
        Vector2 min = new Vector2(Plaza.x - LadoDelSuelo * 0.5f, Plaza.z - LadoDelSuelo * 0.5f);
        bool Dentro(Vector3 c) => c.x >= min.x && c.x <= min.x + LadoDelSuelo && c.z >= min.y && c.z <= min.y + LadoDelSuelo;

        var pintables = new List<int>();
        int prado = -1;
        var nombres = new StringBuilder();
        for (int s = 0; s < original.subMeshCount && s < mats.Length; s++)
        {
            string nombre = mats[s] != null ? mats[s].name : "";
            nombres.Append(nombre).Append(s + 1 < mats.Length ? ", " : "");
            bool esPrado = EsPrado.IsMatch(nombre), esSenda = EsSenda.IsMatch(nombre);
            if (esPrado || esSenda) pintables.Add(s);
            if (esPrado && (prado < 0 || nombre.Length < mats[prado].name.Length)) prado = s;
        }
        if (prado < 0) { informe.AppendLine("Suelo: no distingo la pradera del terreno (" + nombres + ")."); return 0; }

        // Cada triángulo lleva el color de su material en su sitio de la paleta (UV de su centro).
        var uvs = original.uv;
        var paletas = new Dictionary<Texture, (Color[] px, int w, int h)>();
        Color ColorDe(int s, int a, int b, int c)
        {
            var mat = mats[s];
            Color tinte = ColorDelMaterial(mat);
            var tex = mat != null && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") as Texture2D : null;
            if (tex == null || uvs.Length == 0) return tinte;
            if (!paletas.TryGetValue(tex, out var p)) paletas[tex] = p = LeerTextura(tex);
            Vector2 uv = (uvs[a] + uvs[b] + uvs[c]) / 3f;
            int x = Mathf.Clamp((int)(Mathf.Repeat(uv.x, 1f) * p.w), 0, p.w - 1), y = Mathf.Clamp((int)(Mathf.Repeat(uv.y, 1f) * p.h), 0, p.h - 1);
            Color col = p.px[y * p.w + x] * tinte;
            col.a = 1f;
            return col;
        }
        var triDePrado = original.GetTriangles(prado);
        Color hierba = triDePrado.Length >= 3 ? ColorDe(prado, triDePrado[0], triDePrado[1], triDePrado[2]) : ColorDelMaterial(mats[prado]);
        Color tierraMedia = Color.clear; int nTierra = 0;

        int n2 = PixelesDelSuelo * PixelesDelSuelo;
        var mascara = new float[n2];
        var colorR = new float[n2]; var colorG = new float[n2]; var colorB = new float[n2];
        for (int k = 0; k < n2; k++) { colorR[k] = hierba.r; colorG[k] = hierba.g; colorB[k] = hierba.b; }
        int triangulosDeSenda = 0;
        foreach (int s in pintables)
        {
            bool senda = EsSenda.IsMatch(mats[s].name) && !EsPrado.IsMatch(mats[s].name);
            var tri = original.GetTriangles(s);
            for (int t = 0; t < tri.Length; t += 3)
            {
                if (!Dentro((mundo[tri[t]] + mundo[tri[t + 1]] + mundo[tri[t + 2]]) / 3f)) continue;
                Vector2 a = Pixel(mundo[tri[t]], min), b = Pixel(mundo[tri[t + 1]], min), c = Pixel(mundo[tri[t + 2]], min);
                Color col = ColorDe(s, tri[t], tri[t + 1], tri[t + 2]);
                if (senda)
                {
                    Rasterizar(mascara, a, b, c, 1f);
                    triangulosDeSenda++;
                    tierraMedia += col; nTierra++;
                    col = hierba;
                }
                Rasterizar(colorR, a, b, c, col.r); Rasterizar(colorG, a, b, c, col.g); Rasterizar(colorB, a, b, c, col.b);
            }
        }
        // Las manchas de sol y sombra, suaves (los triángulos se notaban).
        colorR = Desenfocar(Desenfocar(colorR, PixelesDelSuelo, 10), PixelesDelSuelo, 10);
        colorG = Desenfocar(Desenfocar(colorG, PixelesDelSuelo, 10), PixelesDelSuelo, 10);
        colorB = Desenfocar(Desenfocar(colorB, PixelesDelSuelo, 10), PixelesDelSuelo, 10);

        // La senda de la paleta es muy clara: la tierra batida va algo más tostada para que se lea como camino.
        Color tierra = Color.Lerp(nTierra > 0 ? tierraMedia / nTierra : TierraClara, TierraTostada, 0.4f);
        tierra.a = 1f;
        var textura = PintarTextura(mascara, colorR, colorG, colorB, hierba, tierra);
        string rutaTextura = CarpetaPueblo + "/SueloDelPueblo.png";
        File.WriteAllBytes(rutaTextura, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(rutaTextura, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(rutaTextura) is TextureImporter imp)
        {
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = PixelesDelSuelo;
            imp.anisoLevel = 4;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura);

        // Las mallas: el terreno sin la hierba del cuadrado, y la hierba del cuadrado con la textura.
        var resto = Object.Instantiate(original);
        resto.name = original.name + " (sin el pueblo)";
        var dentroIdx = new List<int>();
        foreach (int s in pintables)
        {
            var fuera = new List<int>();
            var tri = original.GetTriangles(s);
            for (int t = 0; t < tri.Length; t += 3)
            {
                var lista = Dentro((mundo[tri[t]] + mundo[tri[t + 1]] + mundo[tri[t + 2]]) / 3f) ? dentroIdx : fuera;
                lista.Add(tri[t]); lista.Add(tri[t + 1]); lista.Add(tri[t + 2]);
            }
            resto.SetTriangles(fuera, s);
        }
        resto.RecalculateBounds();

        var mapa = new Dictionary<int, int>();
        var pv = new List<Vector3>(); var pn = new List<Vector3>(); var puv = new List<Vector2>(); var pt = new List<int>();
        var normales = original.normals;
        foreach (int i in dentroIdx)
        {
            if (!mapa.TryGetValue(i, out int j))
            {
                j = pv.Count;
                mapa[i] = j;
                pv.Add(mundo[i]);
                pn.Add(normales.Length > i ? terreno.TransformDirection(normales[i]).normalized : Vector3.up);
                puv.Add(new Vector2((mundo[i].x - min.x) / LadoDelSuelo, (mundo[i].z - min.y) / LadoDelSuelo));
            }
            pt.Add(j);
        }
        var pintado = new Mesh { name = "SueloDelPueblo", indexFormat = pv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        pintado.SetVertices(pv); pintado.SetNormals(pn); pintado.SetUVs(0, puv); pintado.SetTriangles(pt, 0);
        pintado.RecalculateBounds();

        GuardarMalla(resto, CarpetaPueblo + "/SueloDelPueblo_Terreno.asset");
        GuardarMalla(pintado, CarpetaPueblo + "/SueloDelPueblo.asset");
        resto = AssetDatabase.LoadAssetAtPath<Mesh>(CarpetaPueblo + "/SueloDelPueblo_Terreno.asset");
        pintado = AssetDatabase.LoadAssetAtPath<Mesh>(CarpetaPueblo + "/SueloDelPueblo.asset");

        if (mc != null && mc.sharedMesh == null) mc.sharedMesh = original;
        mf.sharedMesh = resto;

        var go = new GameObject(NombreDelSueloPintado);
        go.transform.SetParent(raiz, false);
        go.layer = terreno.gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = pintado;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = MaterialDelSuelo(mats[prado], tex);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = true;

        informe.AppendLine($"Suelo pintado: {LadoDelSuelo:0} m alrededor de la plaza, {pt.Count / 3} triángulos pasados a la textura " +
                           $"({triangulosDeSenda} de senda en total). Materiales del terreno: {nombres}.");
        return 1;
    }

    /// Deja el terreno con su malla (lo pintado cuelga del vestido y se va con él).
    static void DevolverSuelo(Scene escena)
    {
        var terreno = Buscar(escena, Terreno);
        if (terreno == null) return;
        var mf = terreno.GetComponent<MeshFilter>();
        var mc = terreno.GetComponent<MeshCollider>();
        if (mf != null && mc != null && mc.sharedMesh != null && mf.sharedMesh != mc.sharedMesh) mf.sharedMesh = mc.sharedMesh;
    }

    static Vector2 Pixel(Vector3 p, Vector2 min) =>
        new Vector2((p.x - min.x) / LadoDelSuelo * PixelesDelSuelo, (p.z - min.y) / LadoDelSuelo * PixelesDelSuelo);

    static void Rasterizar(float[] mascara, Vector2 a, Vector2 b, Vector2 c, float valor)
    {
        int n = PixelesDelSuelo;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int x1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int y1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
        float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        if (Mathf.Abs(area) < 1e-6f) return;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = ((b.x - p.x) * (c.y - p.y) - (b.y - p.y) * (c.x - p.x)) / area;
                float w1 = ((c.x - p.x) * (a.y - p.y) - (c.y - p.y) * (a.x - p.x)) / area;
                float w2 = 1f - w0 - w1;
                if (w0 >= 0f && w1 >= 0f && w2 >= 0f) mascara[y * n + x] = valor;
            }
    }

    /// Hierba con manchas suaves y, donde iba el camino, tierra batida con el borde irregular,
    /// matas de hierba que se meten y piedrecillas. El borde del cuadrado queda del color liso.
    static Texture2D PintarTextura(float[] mascara, float[] colorR, float[] colorG, float[] colorB, Color hierba, Color tierraBase)
    {
        int n = PixelesDelSuelo;
        var suave = Desenfocar(Desenfocar(mascara, n, 6), n, 6);
        var px = new Color32[n * n];
        var rnd = new System.Random(1081);
        Color hierbaOscura = Color.Lerp(hierba, Color.black, 0.12f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                var aqui = new Color(colorR[y * n + x], colorG[y * n + x], colorB[y * n + x]);
                Color hierbaClara = Color.Lerp(aqui, Color.white, 0.08f), hierbaAqui = aqui, sombraAqui = Color.Lerp(aqui, Color.black, 0.10f);
                float manchas = Mathf.PerlinNoise(u * 18f + 3.1f, v * 18f + 7.7f) * 0.65f + Mathf.PerlinNoise(u * 60f, v * 60f) * 0.35f;
                Color c = manchas < 0.5f ? Color.Lerp(sombraAqui, hierbaAqui, manchas * 2f) : Color.Lerp(hierbaAqui, hierbaClara, (manchas - 0.5f) * 2f);

                float borde = (Mathf.PerlinNoise(u * 90f + 11f, v * 90f + 5f) - 0.5f) * 0.38f + (Mathf.PerlinNoise(u * 300f, v * 300f) - 0.5f) * 0.12f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.56f, suave[y * n + x] + borde));
                if (t > 0f)
                {
                    float grano = Mathf.PerlinNoise(u * 140f + 2f, v * 140f + 9f);
                    Color tierra = Color.Lerp(Color.Lerp(tierraBase, Color.black, 0.18f), Color.Lerp(tierraBase, Color.white, 0.10f),
                                              Mathf.Clamp01(grano * 0.8f + suave[y * n + x] * 0.35f));
                    c = Color.Lerp(c, tierra, t);
                }

                // Fundido al color liso del terreno en el borde del cuadrado.
                float alBorde = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                c = Color.Lerp(hierba, c, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.10f, alBorde)));
                c.a = 1f;
                px[y * n + x] = c;
            }

        // Piedrecillas en la tierra y matas oscuras donde la hierba se mete en el camino.
        for (int k = 0; k < 9000; k++)
        {
            int x = rnd.Next(n), y = rnd.Next(n);
            float s = suave[y * n + x];
            if (s > 0.6f && rnd.NextDouble() < 0.5)
                Punto(px, n, x, y, 1 + rnd.Next(2), Color.Lerp(Piedrecilla, Color.white, (float)rnd.NextDouble() * 0.25f));
            else if (s > 0.25f && s < 0.55f)
                Punto(px, n, x, y, 1 + rnd.Next(3), Color.Lerp(hierbaOscura, hierba, (float)rnd.NextDouble() * 0.4f));
        }

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void Punto(Color32[] px, int n, int cx, int cy, int r, Color c)
    {
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if (x >= 0 && y >= 0 && x < n && y < n && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                    px[y * n + x] = c;
    }

    static float[] Desenfocar(float[] src, int n, int r)
    {
        var tmp = new float[src.Length];
        var dst = new float[src.Length];
        float k = 1f / (2 * r + 1);
        for (int y = 0; y < n; y++)
        {
            float s = 0f;
            for (int x = -r; x <= r; x++) s += src[y * n + Mathf.Clamp(x, 0, n - 1)];
            for (int x = 0; x < n; x++)
            {
                tmp[y * n + x] = s * k;
                s += src[y * n + Mathf.Min(x + r + 1, n - 1)] - src[y * n + Mathf.Max(x - r, 0)];
            }
        }
        for (int x = 0; x < n; x++)
        {
            float s = 0f;
            for (int y = -r; y <= r; y++) s += tmp[Mathf.Clamp(y, 0, n - 1) * n + x];
            for (int y = 0; y < n; y++)
            {
                dst[y * n + x] = s * k;
                s += tmp[Mathf.Min(y + r + 1, n - 1) * n + x] - tmp[Mathf.Max(y - r, 0) * n + x];
            }
        }
        return dst;
    }

    static Color ColorDelMaterial(Material mat) => PasoAlAspectoDeDibujo.ColorPlanoDe(mat);

    /// Los píxeles de una textura aunque no se pueda leer (se copia a una de render).
    static (Color[] px, int w, int h) LeerTextura(Texture2D tex)
    {
        if (tex.isReadable) return (tex.GetPixels(), tex.width, tex.height);
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previo = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var copia = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        copia.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        copia.Apply();
        RenderTexture.active = previo;
        RenderTexture.ReleaseTemporary(rt);
        var px = copia.GetPixels();
        Object.DestroyImmediate(copia);
        return (px, tex.width, tex.height);
    }

    /// Copia del material de la hierba (mismo shader y sombreado) con la textura pintada y sin tinte.
    static Material MaterialDelSuelo(Material hierba, Texture2D tex)
    {
        string ruta = CarpetaPueblo + "/SueloDelPueblo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null) { mat = new Material(hierba) { name = "SueloDelPueblo" }; AssetDatabase.CreateAsset(mat, ruta); }
        else { mat.shader = hierba.shader; mat.CopyPropertiesFromMaterial(hierba); }
        var dibujo = Shader.Find("El Sendero/Dibujo");
        if (dibujo != null) mat.shader = dibujo;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Tinte")) mat.SetColor("_Tinte", Color.white);
        if (mat.HasProperty("_TextureImpact")) mat.SetFloat("_TextureImpact", 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void GuardarMalla(Mesh malla, string ruta)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(malla, ruta);
    }
}
#endif
