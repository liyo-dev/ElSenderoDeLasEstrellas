using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Piezas del kit de laberintos de espejos, para montar escenas desde un menú de Editor: muros de
/// plata con luz en lo alto, espejos vivos (<see cref="EspejoVivo"/>), marcos dorados, suelo de
/// luz y sondas de reflejos. Es genérico: lo usa la Prueba de Will y lo podrá usar la Caja de Liam
/// con otros materiales (los recibe en <see cref="Materiales"/>). Los materiales y texturas se
/// crean una vez en la carpeta indicada y se reutilizan al regenerar. Ver INC-870.
/// </summary>
public static class KitDeLaberintoDeEspejos
{
    public const float AltoMuro = 14f;
    public const float GrosorMuro = 0.4f;
    public const float AltoEspejo = 4.5f;
    public const float BaseEspejo = 0.25f;

    /// <summary>Una baldosa del suelo cubre un número entero de repeticiones del dibujo (15 × 9), para que no haya cortes entre baldosas.</summary>
    private static readonly Vector2 BaldosaDeSuelo = new Vector2(60f, 62.354f);

    /// <summary>Cómo acaba un callejón sin salida.</summary>
    public enum FinDeCallejon { Plata, EspejoVivo, EspejoEnAngulo }

    /// <summary>Materiales con los que se viste un laberinto.</summary>
    public sealed class Materiales
    {
        public Material Plata;
        public Material Vivo;
        public Material Marco;
        public Material Suelo;
        public Material Cielo;
    }

    // ── Materiales ──────────────────────────────────────────────────────────────

    /// <summary>Crea (o recupera) los materiales del palacio luminoso en la carpeta dada.</summary>
    public static Materiales MaterialesDePalacio(string carpeta)
    {
        AsegurarCarpeta(carpeta);
        var shaderEspejo = Shader.Find("Sendero/Espejo");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var defectos = TexturaDeDefectos(carpeta + "/Tex_DefectosDeEspejo.png");
        var triangulos = TexturaDeTriangulos(carpeta + "/Tex_SueloTriangular.png");

        var plata = ObtenerMaterial(carpeta + "/Mat_EspejoPlata.mat", shaderEspejo);
        plata.SetTexture("_Defectos", defectos);
        plata.SetTextureScale("_Defectos", new Vector2(2f, 3f));
        plata.SetFloat("_AlturaLuzInicio", 5f);
        plata.SetFloat("_AlturaLuzFin", 13f);
        plata.SetFloat("_AnchoMarco", 0.01f);
        plata.SetFloat("_OscuroMarco", 0.35f);
        plata.SetColor("_ColorLuz", new Color(1f, 0.97f, 0.91f));
        EditorUtility.SetDirty(plata);

        var vivo = ObtenerMaterial(carpeta + "/Mat_EspejoVivo.mat", shaderEspejo);
        vivo.SetTexture("_Defectos", defectos);
        vivo.SetTextureScale("_Defectos", Vector2.one);
        vivo.SetFloat("_AlturaLuzInicio", 1000f);
        vivo.SetFloat("_AlturaLuzFin", 1001f);
        vivo.SetFloat("_AnchoMarco", 0.03f);
        vivo.SetFloat("_OscuroMarco", 0.55f);
        EditorUtility.SetDirty(vivo);

        var marco = ObtenerMaterial(carpeta + "/Mat_MarcoDorado.mat", lit);
        marco.SetColor("_BaseColor", new Color(0.95f, 0.74f, 0.32f));
        marco.SetFloat("_Metallic", 1f);
        marco.SetFloat("_Smoothness", 0.82f);
        EditorUtility.SetDirty(marco);

        var suelo = ObtenerMaterial(carpeta + "/Mat_SueloDeLuz.mat", lit);
        suelo.SetColor("_BaseColor", new Color(0.8f, 0.77f, 0.71f));
        suelo.SetFloat("_Metallic", 0f);
        suelo.SetFloat("_Smoothness", 0.55f);
        suelo.SetTexture("_EmissionMap", triangulos);
        suelo.SetTextureScale("_EmissionMap", new Vector2(15f, 9f));
        suelo.SetTextureScale("_BaseMap", new Vector2(15f, 9f));
        suelo.SetColor("_EmissionColor", new Color(1.2f, 0.95f, 0.55f));
        suelo.EnableKeyword("_EMISSION");
        suelo.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(suelo);

        var cielo = ObtenerMaterial(carpeta + "/Mat_CieloDeLuz.mat", Shader.Find("Sendero/CieloDeLuz"));
        EditorUtility.SetDirty(cielo);

        AssetDatabase.SaveAssets();
        return new Materiales { Plata = plata, Vivo = vivo, Marco = marco, Suelo = suelo, Cielo = cielo };
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Muro de a a b (en el suelo), de plata y con luz en lo alto, con su marco dorado en la cara que
    /// mira a <paramref name="interior"/>. Si <paramref name="conEspejoVivo"/>, esa cara lleva un
    /// espejo vivo en la parte baja. Devuelve el muro.
    /// </summary>
    public static GameObject Muro(Transform padre, string nombre, Vector3 a, Vector3 b, Vector3 interior,
                                  bool conEspejoVivo, Materiales m)
    {
        a.y = 0f; b.y = 0f;
        Vector3 dir = (b - a).normalized;
        float largo = Vector3.Distance(a, b);
        Vector3 medio = (a + b) * 0.5f;
        Vector3 normal = Vector3.Cross(Vector3.up, dir);
        if (Vector3.Dot(normal, interior - medio) < 0f) normal = -normal;

        var muro = new GameObject(nombre);
        muro.transform.SetParent(padre, false);
        muro.transform.SetPositionAndRotation(medio, Quaternion.LookRotation(dir));
        muro.isStatic = true;

        var plata = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plata.name = "Plata";
        plata.transform.SetParent(muro.transform, false);
        plata.transform.localPosition = Vector3.up * (AltoMuro * 0.5f);
        plata.transform.localScale = new Vector3(GrosorMuro, AltoMuro, largo);
        plata.GetComponent<Renderer>().sharedMaterial = m.Plata;
        plata.isStatic = true;

        if (conEspejoVivo)
        {
            var espejo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            espejo.name = "Espejo vivo";
            Object.DestroyImmediate(espejo.GetComponent<Collider>());
            espejo.transform.SetParent(muro.transform, false);
            espejo.transform.SetPositionAndRotation(
                medio + normal * (GrosorMuro * 0.5f + 0.01f) + Vector3.up * (BaseEspejo + AltoEspejo * 0.5f - 0.125f),
                Quaternion.LookRotation(-normal));
            espejo.transform.localScale = new Vector3(largo - 0.5f, AltoEspejo - 0.25f, 1f);
            espejo.GetComponent<Renderer>().sharedMaterial = m.Vivo;
            espejo.AddComponent<EspejoVivo>();
        }

        Marco(muro.transform, a, b, normal, m.Marco);
        return muro;
    }

    /// <summary>Pilastras doradas cada ~3,6 m, moldura sobre el espejo y zócalo, en la cara indicada.</summary>
    private static void Marco(Transform muro, Vector3 a, Vector3 b, Vector3 normal, Material oro)
    {
        Vector3 dir = (b - a).normalized;
        float largo = Vector3.Distance(a, b);
        Quaternion giro = Quaternion.LookRotation(dir);
        Vector3 cara = normal * (GrosorMuro * 0.5f + 0.06f);
        var raiz = new GameObject("Marco dorado");
        raiz.transform.SetParent(muro, true);

        int tramos = Mathf.Max(1, Mathf.RoundToInt(largo / 3.6f));
        for (int i = 0; i <= tramos; i++)
        {
            float t = Mathf.Lerp(0.11f, largo - 0.11f, i / (float)tramos);
            Pieza(raiz.transform, "Pilastra", a + dir * t + cara + Vector3.up * (AltoMuro * 0.5f), giro,
                  new Vector3(0.12f, AltoMuro, 0.22f), oro);
        }
        Vector3 medio = (a + b) * 0.5f + cara;
        Pieza(raiz.transform, "Moldura", medio + Vector3.up * (BaseEspejo + AltoEspejo + 0.08f), giro,
              new Vector3(0.1f, 0.14f, largo - 0.2f), oro);
        Pieza(raiz.transform, "Zócalo", medio + Vector3.up * 0.12f, giro,
              new Vector3(0.1f, 0.24f, largo - 0.2f), oro);
    }

    private static void Pieza(Transform padre, string nombre, Vector3 pos, Quaternion giro, Vector3 escala, Material mat)
    {
        // Con colisionador: la cámara del jugador choca con él en vez de meterse dentro (se veía
        // un fogonazo dorado al pasar junto a una pilastra).
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetPositionAndRotation(pos, giro);
        go.transform.localScale = escala;
        go.transform.SetParent(padre, true);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    /// <summary>Suelo de luz en baldosas que cubren la zona dada (en planta, X y Z), en la capa Floor.</summary>
    public static GameObject Suelo(Transform padre, Rect zona, Materiales m)
    {
        var raiz = new GameObject("Suelo de luz");
        raiz.transform.SetParent(padre, false);
        raiz.isStatic = true;
        int capaSuelo = LayerMask.NameToLayer("Floor");
        if (capaSuelo < 0) Debug.LogWarning("[KitDeLaberintoDeEspejos] No existe la capa Floor; el suelo se queda en Default.");

        int nx = Mathf.CeilToInt(zona.width / BaldosaDeSuelo.x), nz = Mathf.CeilToInt(zona.height / BaldosaDeSuelo.y);
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            var baldosa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baldosa.name = $"Baldosa {ix},{iz}";
            baldosa.transform.SetParent(raiz.transform, false);
            baldosa.transform.localPosition = new Vector3(zona.xMin + (ix + 0.5f) * BaldosaDeSuelo.x, -0.1f,
                                                          zona.yMin + (iz + 0.5f) * BaldosaDeSuelo.y);
            baldosa.transform.localScale = new Vector3(BaldosaDeSuelo.x, 0.2f, BaldosaDeSuelo.y);
            baldosa.GetComponent<Renderer>().sharedMaterial = m.Suelo;
            baldosa.isStatic = true;
            if (capaSuelo >= 0) baldosa.layer = capaSuelo;
        }
        return raiz;
    }

    // ── Planta: salas hexagonales, pasillos y callejones ───────────────────────

    /// <summary>Normal (hacia fuera) del lado k de una sala hexagonal: 30° + 60°·k en planta (k = 0…5; 1 es el norte).</summary>
    public static Vector3 NormalDeLado(int k) => Direccion(30f + 60f * k);

    /// <summary>Dirección en planta para un ángulo en grados (0° = +X, 90° = +Z).</summary>
    public static Vector3 Direccion(float grados)
    {
        float r = grados * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r));
    }

    /// <summary>Centro del lado k de una sala hexagonal.</summary>
    public static Vector3 CentroDeLado(Vector3 centro, float apotema, int k) => centro + NormalDeLado(k) * apotema;

    /// <summary>
    /// Sala hexagonal. Los lados de <paramref name="aberturas"/> (lado → ancho del hueco) dejan paso con
    /// dos jambas; los de <paramref name="vivos"/> llevan espejo vivo; el resto, plata.
    /// </summary>
    public static Transform SalaHexagonal(Transform padre, string nombre, Vector3 centro, float apotema,
                                          IReadOnlyDictionary<int, float> aberturas, ICollection<int> vivos, Materiales m)
    {
        var sala = new GameObject(nombre).transform;
        sala.SetParent(padre, false);
        float lado = 2f * apotema / Mathf.Sqrt(3f);
        for (int k = 0; k < 6; k++)
        {
            Vector3 c = CentroDeLado(centro, apotema, k), t = Direccion(30f + 60f * k + 90f);
            Vector3 v1 = c + t * (lado * 0.5f), v2 = c - t * (lado * 0.5f);
            if (aberturas != null && aberturas.TryGetValue(k, out float hueco))
            {
                Muro(sala, $"Jamba {k}A", v1, c + t * (hueco * 0.5f), centro, false, m);
                Muro(sala, $"Jamba {k}B", c - t * (hueco * 0.5f), v2, centro, false, m);
            }
            else
            {
                Muro(sala, $"Lado {k}", v1, v2, centro, vivos != null && vivos.Contains(k), m);
            }
        }
        return sala;
    }

    /// <summary>Pasillo recto entre dos bocas (centros de lado), de <paramref name="ancho"/> metros.</summary>
    public static Transform Pasillo(Transform padre, string nombre, Vector3 desde, Vector3 hasta, float ancho, Materiales m)
    {
        var raiz = new GameObject(nombre).transform;
        raiz.SetParent(padre, false);
        Vector3 dir = (hasta - desde).normalized, t = Vector3.Cross(Vector3.up, dir);
        Vector3 interior = (desde + hasta) * 0.5f;
        Muro(raiz, "Pared A", desde + t * (ancho * 0.5f), hasta + t * (ancho * 0.5f), interior, false, m);
        Muro(raiz, "Pared B", desde - t * (ancho * 0.5f), hasta - t * (ancho * 0.5f), interior, false, m);
        return raiz;
    }

    /// <summary>Callejón sin salida que sale de una boca hacia <paramref name="direccion"/>. Devuelve el centro del fondo.</summary>
    public static Vector3 Callejon(Transform padre, string nombre, Vector3 boca, Vector3 direccion, float ancho, float largo,
                                   FinDeCallejon fin, Materiales m)
    {
        var raiz = new GameObject(nombre).transform;
        raiz.SetParent(padre, false);
        Vector3 t = Vector3.Cross(Vector3.up, direccion);
        Vector3 izq = boca + t * (ancho * 0.5f), der = boca - t * (ancho * 0.5f);
        Vector3 izqFin = izq + direccion * largo;
        // En ángulo: el fondo forma 60° con el eje y el reflejo parece un pasillo que sigue y gira.
        Vector3 derFin = der + direccion * (fin == FinDeCallejon.EspejoEnAngulo ? largo - ancho * Mathf.Tan(30f * Mathf.Deg2Rad) : largo);
        Vector3 interior = boca + direccion * (largo * 0.5f);
        Muro(raiz, "Pared A", izq, izqFin, interior, false, m);
        Muro(raiz, "Pared B", der, derFin, interior, false, m);
        Muro(raiz, "Fondo", izqFin, derFin, interior, fin != FinDeCallejon.Plata, m);
        return (izqFin + derFin) * 0.5f;
    }

    /// <summary>Punto con nombre del nivel (guardado, recuerdo, muro que cae…), para que los sistemas de las siguientes tandas lo encuentren.</summary>
    public static Transform Punto(Transform padre, string nombre, Vector3 pos, Vector3 mirar)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mirar.sqrMagnitude > 0f ? mirar : Vector3.forward));
        return go.transform;
    }

    /// <summary>Rótulo de maqueta flotando a 3 m, legible para quien mira hacia <paramref name="mirar"/>.</summary>
    public static void Rotulo(Transform padre, string texto, Vector3 pos, Vector3 mirar, float tamano = 6f)
    {
        var go = new GameObject("Rótulo: " + texto);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos + Vector3.up * 3f, Quaternion.LookRotation(mirar.sqrMagnitude > 0f ? mirar : Vector3.forward));
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = texto;
        tmp.fontSize = tamano;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.35f, 0.25f, 0.1f);
        tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
    }

    /// <summary>Sonda con la caja que envuelve los puntos dados (en planta), de suelo a lo alto del muro.</summary>
    public static ReflectionProbe SondaQueEnvuelve(Transform padre, string nombre, params Vector3[] puntos)
    {
        Vector3 min = puntos[0], max = puntos[0];
        foreach (var p in puntos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        Vector3 centro = (min + max) * 0.5f;
        centro.y = 6f;
        return Sonda(padre, nombre, centro, new Vector3(max.x - min.x + 1f, 14f, max.z - min.z + 1f));
    }

    /// <summary>Sonda de reflejos con proyección de caja (se hornea con <see cref="HornearSondas"/>).</summary>
    public static ReflectionProbe Sonda(Transform padre, string nombre, Vector3 centro, Vector3 tamano)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        var sonda = go.AddComponent<ReflectionProbe>();
        sonda.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
        sonda.boxProjection = true;
        sonda.size = tamano;
        sonda.resolution = 256;
        sonda.hdr = true;
        sonda.blendDistance = 2f;
        return sonda;
    }

    /// <summary>
    /// Hornea las sondas tres veces (cada pasada ve la plata de la anterior) y les asigna su cubemap.
    /// Hay que llamarlo antes de colocar personajes, para que no se queden congelados en la plata.
    /// </summary>
    public static void HornearSondas(ReflectionProbe[] sondas, string carpeta)
    {
        try
        {
            for (int pasada = 0; pasada < 3; pasada++)
            {
                for (int i = 0; i < sondas.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Horneando reflejos", $"Pasada {pasada + 1} de 3 · {sondas[i].name}",
                                                     (pasada * sondas.Length + i) / (3f * sondas.Length));
                    string ruta = $"{carpeta}/Reflejo_{sondas[i].name}.exr";
                    if (!Lightmapping.BakeReflectionProbe(sondas[i], ruta))
                    {
                        Debug.LogWarning($"[KitDeLaberintoDeEspejos] No se pudo hornear la sonda {sondas[i].name}.");
                        continue;
                    }
                    AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
                    var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
                    if (imp != null && imp.textureShape != TextureImporterShape.TextureCube)
                    {
                        imp.textureShape = TextureImporterShape.TextureCube;
                        imp.SaveAndReimport();
                    }
                    sondas[i].customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ruta);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Cubemap pequeño con el degradado del cielo, para usarlo como reflejo del entorno de la escena:
    /// sin él, una escena nueva refleja negro y la plata sale con manchas oscuras.
    /// </summary>
    public static Cubemap ReflejoDelCielo(string carpeta, Material cielo)
    {
        const int n = 32;
        Color cenit = cielo.GetColor("_ColorCenit"), horizonte = cielo.GetColor("_ColorHorizonte"), suelo = cielo.GetColor("_ColorSuelo");
        float suavidad = Mathf.Max(0.01f, cielo.GetFloat("_Suavidad"));
        var cubo = new Cubemap(n, TextureFormat.RGBAHalf, false);
        var caras = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
        var px = new Color[n * n];
        foreach (var cara in caras)
        {
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                Vector3 dir = cara switch
                {
                    CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                    CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                    CubemapFace.PositiveY => new Vector3(u, 1f, v),
                    CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                    CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                    _ => new Vector3(-u, -v, -1f)
                };
                float h = dir.normalized.y;
                px[y * n + x] = h >= 0f ? Color.Lerp(horizonte, cenit, Suave(0f, 1f, h / suavidad))
                                        : Color.Lerp(horizonte, suelo, Suave(0f, 1f, -h / suavidad));
            }
            cubo.SetPixels(px, cara);
        }
        cubo.Apply();

        string ruta = carpeta + "/Reflejo_Cielo.asset";
        var viejo = AssetDatabase.LoadAssetAtPath<Cubemap>(ruta);
        if (viejo != null) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(cubo, ruta);
        return cubo;
    }

    /// <summary>
    /// Quita la sombra de contacto a un personaje dentro de un sitio donde nadie tiene sombra: la deja
    /// puesta y desactivada en cada objeto con <see cref="NPCSimpleAnimator"/> (así no se la añaden).
    /// </summary>
    public static void SinSombra(GameObject personaje)
    {
        foreach (var anim in personaje.GetComponentsInChildren<NPCSimpleAnimator>(true))
        {
            var sombra = anim.GetComponent<SombraDeContacto>();
            if (sombra == null) sombra = anim.gameObject.AddComponent<SombraDeContacto>();
            sombra.enabled = false;
        }
    }

    // ── Texturas generadas ─────────────────────────────────────────────────────

    /// <summary>R: polvo y arañazos finos. G y B: desvío suave alrededor de 0,5. Repetible.</summary>
    private static Texture2D TexturaDeDefectos(string ruta)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (existente != null) return existente;

        const int n = 512;
        var r = new float[n * n];
        var rnd = new System.Random(7);
        for (int k = 0; k < 900; k++)
        {
            int cx = rnd.Next(n), cy = rnd.Next(n);
            float radio = 0.8f + (float)rnd.NextDouble() * 2.2f, fuerza = 0.25f + (float)rnd.NextDouble() * 0.75f;
            int ri = Mathf.CeilToInt(radio * 2f);
            for (int dy = -ri; dy <= ri; dy++)
            for (int dx = -ri; dx <= ri; dx++)
            {
                float d2 = (dx * dx + dy * dy) / (radio * radio);
                int idx = Envolver(cy + dy, n) * n + Envolver(cx + dx, n);
                r[idx] = Mathf.Max(r[idx], fuerza * Mathf.Exp(-d2 * 2f));
            }
        }
        for (int k = 0; k < 40; k++)
        {
            float x = rnd.Next(n), y = rnd.Next(n), ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
            int largo = 20 + rnd.Next(100);
            float fuerza = 0.2f + (float)rnd.NextDouble() * 0.3f;
            for (int s = 0; s < largo; s++)
            {
                int idx = Envolver(Mathf.RoundToInt(y + Mathf.Sin(ang) * s), n) * n + Envolver(Mathf.RoundToInt(x + Mathf.Cos(ang) * s), n);
                r[idx] = Mathf.Max(r[idx], fuerza);
            }
        }

        const int celdas = 16;
        var rejillaG = new float[celdas * celdas];
        var rejillaB = new float[celdas * celdas];
        for (int i = 0; i < rejillaG.Length; i++) { rejillaG[i] = (float)rnd.NextDouble(); rejillaB[i] = (float)rnd.NextDouble(); }

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float g = Ruido(rejillaG, celdas, x / (float)n * celdas, y / (float)n * celdas);
            float b = Ruido(rejillaB, celdas, x / (float)n * celdas, y / (float)n * celdas);
            px[y * n + x] = new Color32((byte)(Mathf.Clamp01(r[y * n + x]) * 255), (byte)(g * 255), (byte)(b * 255), 255);
        }
        tex.SetPixels32(px);
        return GuardarTextura(tex, ruta, false);
    }

    /// <summary>Rejilla de triángulos equiláteros (lado 128 px), repetible: 512 × 887 px.</summary>
    private static Texture2D TexturaDeTriangulos(string ruta)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (existente != null) return existente;

        const float lado = 128f;
        float h = lado * Mathf.Sqrt(3f) * 0.5f;
        int w = 512, alto = Mathf.RoundToInt(8f * h);
        var tex = new Texture2D(w, alto, TextureFormat.RGBA32, false, false);
        var px = new Color32[w * alto];
        float s3 = Mathf.Sqrt(3f) * 0.5f;
        for (int y = 0; y < alto; y++)
        for (int x = 0; x < w; x++)
        {
            float d0 = Distancia(y / h), d1 = Distancia((s3 * x - 0.5f * y) / h), d2 = Distancia((s3 * x + 0.5f * y) / h);
            float d = Mathf.Min(d0, Mathf.Min(d1, d2)) * h;
            // Línea de ~3 cm con un halo suave alrededor; el resto del suelo no brilla.
            float linea = 1f - Suave(1.5f, 3.5f, d);
            float v = Mathf.Clamp01(linea + 0.3f * Mathf.Exp(-d / 6f));
            byte c = (byte)(v * 255);
            px[y * w + x] = new Color32(c, c, c, 255);
        }
        tex.SetPixels32(px);
        return GuardarTextura(tex, ruta, true);
    }

    /// <summary>smoothstep de HLSL (Mathf.SmoothStep es otra cosa: interpola entre sus dos primeros valores).</summary>
    private static float Suave(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Distancia(float v) { float f = v - Mathf.Floor(v); return Mathf.Min(f, 1f - f); }
    private static int Envolver(int v, int n) => ((v % n) + n) % n;

    private static float Ruido(float[] rejilla, int celdas, float x, float y)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = rejilla[Envolver(y0, celdas) * celdas + Envolver(x0, celdas)];
        float b = rejilla[Envolver(y0, celdas) * celdas + Envolver(x0 + 1, celdas)];
        float c = rejilla[Envolver(y0 + 1, celdas) * celdas + Envolver(x0, celdas)];
        float d = rejilla[Envolver(y0 + 1, celdas) * celdas + Envolver(x0 + 1, celdas)];
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static Texture2D GuardarTextura(Texture2D tex, string ruta, bool srgb)
    {
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.sRGBTexture = srgb;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    private static Material ObtenerMaterial(string ruta, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, ruta);
        }
        else if (mat.shader != shader) mat.shader = shader;
        return mat;
    }

    private static void AsegurarCarpeta(string carpeta)
    {
        if (AssetDatabase.IsValidFolder(carpeta)) return;
        string padre = Path.GetDirectoryName(carpeta).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(carpeta));
    }
}
