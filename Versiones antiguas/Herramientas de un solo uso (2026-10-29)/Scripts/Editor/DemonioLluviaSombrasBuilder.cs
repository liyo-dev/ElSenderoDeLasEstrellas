#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static VfxProcedural;

/// <summary>
/// Construye y asigna los VFX procedurales de la «Lluvia de sombras» del Demonio (INC-855).
/// Sustituye las esferas de plasma deformadas por un cometa sombrío celestial, marcas de aviso rúnicas
/// de vacío en el suelo y detonaciones de impacto con onda expansiva y humo oscuro.
/// Idempotente. Se ejecuta automáticamente si detecta cambios o mediante el menú.
/// </summary>
public static class DemonioLluviaSombrasBuilder
{
    public const string CarpetaVfx = "Assets/_VFX/Jefes/Demonio";
    public const string CarpetaMateriales = CarpetaVfx + "/Materiales";
    public const string CarpetaTexturas = CarpetaVfx + "/Texturas";

    public const string RutaMatAviso = CarpetaMateriales + "/M_Demonio_LluviaSombras_Aviso.mat";
    public const string RutaAviso = CarpetaVfx + "/VFX_Demonio_LluviaSombras_Aviso.prefab";
    public const string RutaProyectil = CarpetaVfx + "/VFX_Demonio_LluviaSombras_Proyectil.prefab";
    public const string RutaImpacto = CarpetaVfx + "/VFX_Demonio_LluviaSombras_Impacto.prefab";

    private const string Demonio1 = "Assets/Prefabs/Enemy/Demon.prefab";
    private const string Demonio2 = "Assets/Prefabs/Enemy/Demon2.prefab";

    private const string ClaveHash = "ElSendero.Combate.DemonioLluviaSombras.v1";

    [InitializeOnLoadMethod]
    private static void Programar()
    {
        EditorApplication.delayCall += EjecutarSiEsNecesario;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EjecutarSiEsNecesario;
        };
    }

    private static void EjecutarSiEsNecesario()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Comprobación de versión/hash para ejecutar una sola vez al cargar o refrescar
        string hashActual = "v1.0.0_INC855";
        if (EditorPrefs.GetString(ClaveHash, "") == hashActual &&
            File.Exists(RutaAviso) && File.Exists(RutaProyectil) && File.Exists(RutaImpacto))
        {
            return;
        }

        Ejecutar(false);
        EditorPrefs.SetString(ClaveHash, hashActual);
    }

    [MenuItem("El Sendero/Combate/Demonios: actualizar lluvia de sombras épica (INC-855)")]
    public static void MenuEjecutar()
    {
        Ejecutar(true);
    }

    public static void Ejecutar(bool verbose)
    {
        var log = new StringBuilder("=== Demonio: Lluvia de Sombras Épica (INC-855) ===\n");
        var warnings = new List<string>();

        CrearCarpetas();
        Material matAviso = CrearOMaterialAviso();
        var t = new Texturas(CarpetaTexturas, CarpetaMateriales, "DemonioSombras");

        // 1. Prefab de aviso (marca rúnica en el suelo)
        GuardarPrefab("VFX_Demonio_LluviaSombras_Aviso", RutaAviso, root =>
        {
            ConstruirAviso(root, t, matAviso);
        });
        log.AppendLine("✓ Creado prefab de aviso: " + RutaAviso);

        // 2. Prefab del proyectil (cometa sombrío celestial en caída)
        GuardarPrefab("VFX_Demonio_LluviaSombras_Proyectil", RutaProyectil, root =>
        {
            ConstruirProyectil(root, t);
        });
        log.AppendLine("✓ Creado prefab de proyectil: " + RutaProyectil);

        // 3. Prefab de impacto (estallido de choque y humo oscuro)
        GuardarPrefab("VFX_Demonio_LluviaSombras_Impacto", RutaImpacto, root =>
        {
            ConstruirImpacto(root, t);
        });
        log.AppendLine("✓ Creado prefab de impacto: " + RutaImpacto);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 4. Asignar referencias en Demon.prefab y Demon2.prefab
        var vfxAviso = AssetDatabase.LoadAssetAtPath<GameObject>(RutaAviso);
        var vfxProyectil = AssetDatabase.LoadAssetAtPath<GameObject>(RutaProyectil);
        var vfxImpacto = AssetDatabase.LoadAssetAtPath<GameObject>(RutaImpacto);

        if (vfxAviso == null || vfxProyectil == null || vfxImpacto == null)
        {
            Debug.LogError("[DemonioLluviaSombrasBuilder] Error cargando los prefabs generados tras guardarlos.");
            return;
        }

        AsignarPrefabDemonio(Demonio1, vfxAviso, vfxProyectil, vfxImpacto, log);
        AsignarPrefabDemonio(Demonio2, vfxAviso, vfxProyectil, vfxImpacto, log);

        AssetDatabase.SaveAssets();

        if (verbose)
        {
            Debug.Log(log.ToString());
        }
    }

    private static void CrearCarpetas()
    {
        CrearCarpeta(CarpetaVfx);
        CrearCarpeta(CarpetaMateriales);
        CrearCarpeta(CarpetaTexturas);
    }

    private static Material CrearOMaterialAviso()
    {
        var shader = Shader.Find("Liyo/RainShadowURP");
        if (shader == null)
        {
            Debug.LogWarning("[DemonioLluviaSombrasBuilder] Shader Liyo/RainShadowURP no encontrado, usando fallback.");
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMatAviso);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, RutaMatAviso);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        // Tonalidad sombría/vacío: relleno violeta profundo y anillo rúnico brillante
        mat.SetColor("_Color", new Color(0.26f, 0.04f, 0.42f, 0.45f));
        mat.SetColor("_RingColor", new Color(0.72f, 0.16f, 0.96f, 1.0f));
        mat.SetFloat("_RingWidth", 0.08f);
        mat.SetFloat("_FillAlpha", 0.40f);
        mat.SetFloat("_PulseSpeed", 3.2f);
        mat.SetFloat("_GlobalAlpha", 1.0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Mesh ObtenerQuadMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }

    /// Aviso de suelo: Quad decal sombrío con shader RainShadowURP + vaho etéreo ascendente
    private static void ConstruirAviso(GameObject root, Texturas t, Material matAviso)
    {
        var mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = ObtenerQuadMesh();

        var mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matAviso;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Vaho sombrío ascendente desde el centro del aviso
        var psWisps = Sistema(root.transform, "Vaho sombrio", t.Mat("Humo", false), ParticleSystemRenderMode.Billboard, 1.5f);
        var mainW = psWisps.main;
        mainW.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        mainW.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        mainW.startColor = new Color(0.18f, 0.04f, 0.28f, 0.65f);
        mainW.simulationSpace = ParticleSystemSimulationSpace.Local;
        Emitir(psWisps, 15f);

        var formaW = psWisps.shape;
        formaW.enabled = true;
        formaW.shapeType = ParticleSystemShapeType.Circle;
        formaW.radius = 0.85f;
        formaW.radiusThickness = 0.8f;
        formaW.rotation = Vector3.zero; // en el plano local XY del quad

        // En el quad rotado a 90º en X, +Z apunta hacia arriba (world UP)
        Velocidad(psWisps, new Vector3(0f, 0f, 0.45f));
        Desvanecer(psWisps, 0.2f, 0.7f);
    }

    /// Cometa de sombra en caída: núcleo amatista concentrado, ojo de vacío negro, corona de rotación
    /// y estelas en World Space que trazan el rastro hacia el cielo mientras desciende.
    private static void ConstruirProyectil(GameObject root, Texturas t)
    {
        // 1. Núcleo brillante violeta-amatista concentrado
        var psNucleo = Sistema(root.transform, "Nucleo de sombra", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainN = psNucleo.main;
        mainN.startLifetime = 0.2f;
        mainN.startSize = 1.35f;
        mainN.startColor = new Color(0.65f, 0.18f, 1.0f) * 2.5f;
        Emitir(psNucleo, 35f);
        Desvanecer(psNucleo, 0.05f, 0.7f);

        // 2. Centro oscuro/negro que da sensación de abismo/vacío
        var psVacio = Sistema(root.transform, "Ojo de vacio", t.Mat("Brillo", false), ParticleSystemRenderMode.Billboard, 1f);
        var mainV = psVacio.main;
        mainV.startLifetime = 0.2f;
        mainV.startSize = 0.85f;
        mainV.startColor = new Color(0.015f, 0.005f, 0.025f, 0.96f);
        Emitir(psVacio, 35f);
        Desvanecer(psVacio, 0.05f, 0.8f);

        // 3. Corona giratoria de energía oscura
        var psCorona = Sistema(root.transform, "Corona sombria", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainC = psCorona.main;
        mainC.startLifetime = 0.25f;
        mainC.startSize = 1.9f;
        mainC.startColor = new Color(0.75f, 0.15f, 0.95f, 0.85f);
        Emitir(psCorona, 30f);
        Girar(psCorona, 360f);
        Desvanecer(psCorona, 0.05f, 0.65f);

        // 4. Cola de cometa sombrío (World Space: se traza verticalmente hacia arriba según cae)
        var psCola = Sistema(root.transform, "Cola sombria", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainCola = psCola.main;
        mainCola.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        mainCola.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        mainCola.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.75f, 0.25f, 1f) * 2.0f,
            new Color(0.25f, 0.04f, 0.45f, 0.8f));
        mainCola.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(psCola, 130f);
        Forma(psCola, ParticleSystemShapeType.Sphere, 0.2f, 1f);
        var tamCola = psCola.sizeOverLifetime;
        tamCola.enabled = true;
        tamCola.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Desvanecer(psCola, 0.04f, 0.5f);

        // 5. Humo espectral de vacío (World Space)
        var psHumo = Sistema(root.transform, "Humo espectral", t.Mat("Humo", false), ParticleSystemRenderMode.Billboard, 1f);
        var mainH = psHumo.main;
        mainH.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
        mainH.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        mainH.startColor = new Color(0.05f, 0.02f, 0.08f, 0.75f);
        mainH.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(psHumo, 75f);
        Forma(psHumo, ParticleSystemShapeType.Sphere, 0.25f, 1f);
        Desvanecer(psHumo, 0.1f, 0.6f);

        // 6. Esquirlas y chispas oscuras (World Space)
        var psChispas = Sistema(root.transform, "Esquirlas sombrias", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        var mainCh = psChispas.main;
        mainCh.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        mainCh.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        mainCh.startColor = new Color(0.85f, 0.35f, 1f) * 1.8f;
        mainCh.simulationSpace = ParticleSystemSimulationSpace.World;
        Emitir(psChispas, 50f);
        Forma(psChispas, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        Velocidad(psChispas, new Vector3(0.5f, 0.5f, 0.5f));
        Desvanecer(psChispas, 0.05f, 0.6f);
    }

    /// Detonación de impacto: onda de choque violeta en el suelo, estallido central de nova oscura,
    /// nube de humo negro/púrpura y esquirlas explosivas.
    private static void ConstruirImpacto(GameObject root, Texturas t)
    {
        // 1. Onda de choque expansiva
        var psOnda = Sistema(root.transform, "Onda de choque", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        var mainO = psOnda.main;
        mainO.startLifetime = 0.4f;
        mainO.startSize = 4.5f;
        mainO.startColor = new Color(0.75f, 0.2f, 1f) * 2.2f;
        Rafaga(psOnda, 1);
        Crecer(psOnda, 0.85f);
        Desvanecer(psOnda, 0.06f, 0.65f);

        // 2. Estallido estelar oscuro (destello violento de impacto)
        var psEstallido = Sistema(root.transform, "Estallido oscuro", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 0.7f, bucle: false);
        var mainE = psEstallido.main;
        mainE.startLifetime = 0.35f;
        mainE.startSize = 4.5f;
        mainE.startColor = new Color(0.85f, 0.45f, 1f) * 2.5f;
        Rafaga(psEstallido, 1);
        Desvanecer(psEstallido, 0.05f, 0.75f);

        // 3. Detonación de humo de vacío
        var psHumo = Sistema(root.transform, "Humo de detonacion", t.Mat("Humo", false), ParticleSystemRenderMode.Billboard, 1.0f, bucle: false);
        var mainH = psHumo.main;
        mainH.startLifetime = 0.75f;
        mainH.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        mainH.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        mainH.startColor = new Color(0.07f, 0.02f, 0.1f, 0.85f);
        Rafaga(psHumo, 20);
        Forma(psHumo, ParticleSystemShapeType.Sphere, 0.4f, 1f);
        Desvanecer(psHumo, 0.08f, 0.65f);

        // 4. Esquirlas y chispas expulsadas hacia fuera
        var psChispas = Sistema(root.transform, "Esquirlas explosivas", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 0.8f, bucle: false);
        var mainCh = psChispas.main;
        mainCh.startLifetime = 0.45f;
        mainCh.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.0f);
        mainCh.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        mainCh.startColor = new Color(0.85f, 0.35f, 1f) * 2.0f;
        Rafaga(psChispas, 18);
        Forma(psChispas, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        Desvanecer(psChispas, 0.05f, 0.7f);
    }

    private static void AsignarPrefabDemonio(string rutaPrefab, GameObject vfxAviso, GameObject vfxProyectil, GameObject vfxImpacto, StringBuilder log)
    {
        var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var iaComp = raiz.GetComponentInChildren<ImpDemonAI>(true);
            if (iaComp == null)
            {
                log.AppendLine($"⚠ {rutaPrefab}: no contiene ImpDemonAI.");
                return;
            }

            var so = new SerializedObject(iaComp);
            so.FindProperty("rainShadowPrefab").objectReferenceValue = vfxAviso;
            so.FindProperty("rainImpactPrefab").objectReferenceValue = vfxProyectil;
            var propExplosion = so.FindProperty("rainExplosionVFXPrefab");
            if (propExplosion != null)
            {
                propExplosion.objectReferenceValue = vfxImpacto;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            log.AppendLine($"✓ {rutaPrefab}: asignada lluvia de sombras (aviso, proyectil, impacto).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
#endif
