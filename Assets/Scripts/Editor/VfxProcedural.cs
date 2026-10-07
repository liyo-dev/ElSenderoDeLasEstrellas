using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Piezas para generar efectos de partículas a medida desde el Editor, sin packs: sistemas de
/// partículas configurados por código, texturas pintadas por fórmula (blanco con alfa; el color lo
/// ponen las partículas) y sus materiales URP. Lo usan los efectos propios de los hechizos
/// (VfxPropiosDeHechizosBuilder) y los del mundo (p. ej. PaginaDelGrimorioBuilder). Ver INC-646.
/// </summary>
public static class VfxProcedural
{
    // ── Prefabs y carpetas ────────────────────────────────────────────────

    /// Crea la raíz, la monta y la guarda como prefab. Regenera en el sitio y conserva el GUID.
    public static void GuardarPrefab(string nombreRaiz, string ruta, Action<GameObject> construir)
    {
        var root = new GameObject(nombreRaiz);
        try
        {
            construir(root);
            PrefabUtility.SaveAsPrefabAsset(root, ruta);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// Crea la carpeta y las que falten por encima.
    public static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int corte = ruta.LastIndexOf('/');
        string padre = ruta.Substring(0, corte);
        CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, ruta.Substring(corte + 1));
    }

    // ── Partículas ────────────────────────────────────────────────────────

    /// Núcleo brillante que viaja con el proyectil.
    public static void Nucleo(Transform padre, Texturas t, string nombre, Color color, float tam)
    {
        var ps = Sistema(padre, nombre, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = ps.main;
        main.startLifetime = 0.15f;
        main.startSize = tam;
        main.startColor = color;
        Emitir(ps, 30f);
        Desvanecer(ps, 0.2f, 0.6f);
    }

    /// Estela de chispas que se queda atrás (espacio de mundo).
    public static void Estela(Transform padre, Texturas t, string nombre, Color color, float tam, float ritmo)
    {
        var ps = Sistema(padre, nombre, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.4f, tam);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(ps, ritmo);
        Forma(ps, ParticleSystemShapeType.Sphere, 0.12f, 1f);
        var tam2 = ps.sizeOverLifetime;
        tam2.enabled = true;
        tam2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(ps, 0.05f, 0.4f);
    }

    /// Motas que suben desde un círculo.
    public static void Motas(Transform padre, Texturas t, string nombre, Color color, float radio, float ritmo, float subida)
    {
        var ps = Sistema(padre, nombre, t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = color * 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(ps, ritmo);
        Forma(ps, ParticleSystemShapeType.Circle, radio, 1f);
        Velocidad(ps, new Vector3(0f, subida, 0f));
        Desvanecer(ps, 0.2f, 0.6f);
    }

    public static ParticleSystem Sistema(Transform padre, string nombre, Material mat, ParticleSystemRenderMode modo, float duracion, bool bucle = true)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = bucle;
        main.duration = duracion;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 500;

        var emision = ps.emission;
        emision.rateOverTime = 0f;
        var forma = ps.shape;
        forma.enabled = false;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.renderMode = modo;
        render.sharedMaterial = mat;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        return ps;
    }

    /// Una sola partícula que dura toda la zona (la zona se destruye antes de que acabe).
    public static void Unico(ParticleSystem ps, float vida, float tam, Color color)
    {
        var main = ps.main;
        main.startLifetime = vida;
        main.startSize = tam;
        main.startColor = color;
        main.maxParticles = 1;
        Rafaga(ps, 1);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.012f), new GradientAlphaKey(1f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    public static void Rafaga(ParticleSystem ps, int cuantas)
    {
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cuantas) });
    }

    public static void Emitir(ParticleSystem ps, float ritmo)
    {
        var emision = ps.emission;
        emision.rateOverTime = ritmo;
    }

    public static void Forma(ParticleSystem ps, ParticleSystemShapeType tipo, float radio, float grosor)
    {
        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = tipo;
        forma.radius = radio;
        forma.radiusThickness = grosor;
        if (tipo == ParticleSystemShapeType.Circle) forma.rotation = new Vector3(-90f, 0f, 0f);
    }

    public static void Velocidad(ParticleSystem ps, Vector3 v)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // Los tres ejes tienen que ir en el mismo modo (dos constantes).
        vel.x = new ParticleSystem.MinMaxCurve(v.x, v.x);
        vel.y = new ParticleSystem.MinMaxCurve(v.y * 0.7f, v.y * 1.3f);
        vel.z = new ParticleSystem.MinMaxCurve(v.z, v.z);
    }

    public static void Girar(ParticleSystem ps, float gradosPorSegundo)
    {
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(gradosPorSegundo * Mathf.Deg2Rad);
    }

    /// Crece de casi nada a su tamaño en la fracción de vida indicada.
    public static void Crecer(ParticleSystem ps, float hasta)
    {
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        var curva = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(hasta, 0.8f), new Keyframe(1f, 1f));
        tam.size = new ParticleSystem.MinMaxCurve(1f, curva);
    }

    /// Alfa: entra hasta 'entra' y empieza a salir en 'sale' (fracciones de la vida).
    public static void Desvanecer(ParticleSystem ps, float entra, float sale)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, entra), new GradientAlphaKey(1f, sale), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    // ── Formas 2D (coordenadas de -1 a 1 con el centro en 0) ──────────────

    public static float R(float x, float y) => Mathf.Sqrt(x * x + y * y);
    public static float Gauss(float d, float ancho) => Mathf.Exp(-(d * d) / (ancho * ancho));
    public static float Linea(float d, float grosor) => Mathf.Clamp01((grosor - Mathf.Abs(d)) / (grosor * 0.5f) + 0.5f);

    public static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    public static float Estrella(float x, float y)
    {
        float r = R(x, y);
        float nucleo = Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 3f);
        float rayos = Mathf.Max(Gauss(y, 0.035f) * Mathf.Clamp01(1f - Mathf.Abs(x)), Gauss(x, 0.035f) * Mathf.Clamp01(1f - Mathf.Abs(y)));
        float dx = (x + y) * 0.7071f, dy = (x - y) * 0.7071f;
        float diag = Mathf.Max(Gauss(dy, 0.03f) * Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.6f), Gauss(dx, 0.03f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.6f)) * 0.6f;
        return Mathf.Clamp01(nucleo + rayos + diag);
    }

    // ── Texturas y materiales ─────────────────────────────────────────────

    /// Texturas procedurales de una carpeta y sus materiales. Trae las genéricas (Brillo, Anillo,
    /// Estrella, Humo); cada generador registra las suyas con Registrar. Se pintan al pedirlas.
    public sealed class Texturas
    {
        private readonly string _carpetaTexturas;
        private readonly string _carpetaMateriales;
        private readonly string _prefijo;
        private readonly Dictionary<string, Func<Texture2D>> _pintores = new Dictionary<string, Func<Texture2D>>();
        private readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        /// El prefijo va en el nombre de los archivos: T_<prefijo>_<textura>.png y
        /// M_<prefijo>_<textura>_Aditivo|Alfa.mat.
        public Texturas(string carpetaTexturas, string carpetaMateriales, string prefijo)
        {
            _carpetaTexturas = carpetaTexturas;
            _carpetaMateriales = carpetaMateriales;
            _prefijo = prefijo;
            CrearCarpeta(carpetaTexturas);
            CrearCarpeta(carpetaMateriales);

            Registrar("Brillo", 128, 128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - R(x, y)), 2f));
            Registrar("Anillo", 256, 256, (x, y) => Gauss(R(x, y) - 0.86f, 0.045f) + 0.25f * Gauss(R(x, y) - 0.8f, 0.12f));
            Registrar("Estrella", 256, 256, Estrella);
            Registrar("Humo", 128, 128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - R(x, y)), 1.5f) * (0.55f + 0.45f * Mathf.PerlinNoise(x * 5f + 3.1f, y * 5f + 7.7f)));
        }

        /// Textura pintada con una fórmula alfa(x, y).
        public void Registrar(string nombre, int w, int h, Func<float, float, float> f) => _pintores[nombre] = () => Pintar(nombre, w, h, f);

        /// Textura con su propio pintor (p. ej. una que se dibuja a trazos); debe acabar en GuardarAlfa.
        public void Registrar(string nombre, Func<Texturas, Texture2D> pintor) => _pintores[nombre] = () => pintor(this);

        /// Material de partículas URP con esa textura, aditivo o con mezcla alfa.
        public Material Mat(string textura, bool aditivo)
        {
            string ruta = $"{_carpetaMateriales}/M_{_prefijo}_{textura}_{(aditivo ? "Aditivo" : "Alfa")}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, ruta);
            }
            else if (shader != null && mat.shader != shader) mat.shader = shader;

            mat.SetTexture("_BaseMap", Tex(textura));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", aditivo ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Texture2D Tex(string nombre)
        {
            if (_tex.TryGetValue(nombre, out var t)) return t;
            if (_pintores.TryGetValue(nombre, out var pintar)) t = pintar();
            else Debug.LogWarning($"[VFX] No hay ninguna textura «{nombre}» registrada para {_carpetaTexturas}.");
            _tex[nombre] = t;
            return t;
        }

        /// Pinta la fórmula alfa(x, y) y la guarda.
        public Texture2D Pintar(string nombre, int w, int h, Func<float, float, float> f)
        {
            var a = new float[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    a[j * w + i] = f((i + 0.5f) / w * 2f - 1f, (j + 0.5f) / h * 2f - 1f);
            return GuardarAlfa(nombre, w, h, a);
        }

        /// Guarda la máscara como PNG blanco con alfa y la importa con los ajustes de partículas.
        public Texture2D GuardarAlfa(string nombre, int w, int h, float[] a)
        {
            string ruta = $"{_carpetaTexturas}/T_{_prefijo}_{nombre}.png";
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[i]) * 255f);
                px[i] = new Color32(b, b, b, b);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ruta) is TextureImporter imp)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }
    }
}
