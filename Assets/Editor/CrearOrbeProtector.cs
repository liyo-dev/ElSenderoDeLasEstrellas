#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// El orbe del hechizo protector del Archimago en el prólogo: nace entre sus manos y crece sobre
/// su cabeza mientras reza. Más vivo que la bola de conjuro corriente (VFX_BolaDeConjuro, que se
/// queda pequeña a propósito): núcleo blanco, dos halos que laten, chispas que caen hacia dentro
/// (se ve que lo está creando) y una luz que ilumina al Archimago. Ver INC-980.
public static class CrearOrbeProtector
{
    public const string RutaPrefab = "Assets/_VFX/Prologo/VFX_OrbeProtector.prefab";
    private const string CarpetaTexturas = "Assets/_VFX/Prologo/Texturas";
    private const string CarpetaMateriales = "Assets/_VFX/Prologo/Materiales";

    [MenuItem("El Sendero/VFX/Crear orbe protector del prólogo (INC-980)")]
    public static void Menu() => Asegurar();

    public static string Asegurar()
    {
        var t = new VfxProcedural.Texturas(CarpetaTexturas, CarpetaMateriales, "Prologo");
        VfxProcedural.GuardarPrefab("VFX_OrbeProtector", RutaPrefab, root => Construir(root, t));
        AssetDatabase.SaveAssets();
        return RutaPrefab;
    }

    private static void Construir(GameObject root, VfxProcedural.Texturas t)
    {
        // Una esfera de verdad en el centro: el brillo de las partículas se difumina hacia fuera y,
        // al escalarlo, el ojo seguía viendo un punto; la esfera sí se ve crecer (y el bloom la hace brillar).
        var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(esfera.GetComponent<Collider>());
        esfera.name = "Esfera";
        esfera.transform.SetParent(root.transform, false);
        esfera.transform.localScale = Vector3.one * 0.2f;
        var r = esfera.GetComponent<MeshRenderer>();
        r.sharedMaterial = MaterialDeLaEsfera();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        VfxProcedural.Nucleo(root.transform, t, "Nucleo", new Color(1f, 0.98f, 0.92f) * 6f, 0.26f);
        VfxProcedural.Nucleo(root.transform, t, "Halo", new Color(0.7f, 0.88f, 1f) * 2.6f, 0.75f);
        Latido(VfxProcedural.Sistema(root.transform, "Resplandor", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f),
               new Color(0.55f, 0.78f, 1f) * 1.1f, 1.5f);

        // Chispas que vienen de fuera y se meten en el orbe: la luz se está juntando.
        var chispas = VfxProcedural.Sistema(root.transform, "Absorcion", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = chispas.main;
        main.startLifetime = 0.45f;
        main.startSpeed = -0.85f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
        main.startColor = new Color(0.85f, 0.95f, 1f) * 3f;
        VfxProcedural.Emitir(chispas, 45f);
        VfxProcedural.Forma(chispas, ParticleSystemShapeType.Sphere, 0.4f, 0f);
        VfxProcedural.Desvanecer(chispas, 0.3f, 0.8f);

        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(root.transform, false);
        luz.type = LightType.Point;
        luz.color = new Color(0.72f, 0.86f, 1f);
        luz.intensity = 4f;
        luz.range = 3.5f;
        luz.shadows = LightShadows.None;
    }

    private static Material MaterialDeLaEsfera()
    {
        const string ruta = "Assets/_VFX/Prologo/Materiales/OrbeProtector_Esfera.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.SetColor("_BaseColor", new Color(2.6f, 3.0f, 3.4f, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// Una partícula grande que se enciende y se apaga despacio: el orbe respira.
    private static void Latido(ParticleSystem ps, Color color, float tam)
    {
        var main = ps.main;
        main.startLifetime = 0.9f;
        main.startSize = tam;
        main.startColor = color;
        VfxProcedural.Emitir(ps, 3f);
        var tamaño = ps.sizeOverLifetime;
        tamaño.enabled = true;
        tamaño.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.75f)));
        VfxProcedural.Desvanecer(ps, 0.4f, 0.5f);
    }
}
#endif
