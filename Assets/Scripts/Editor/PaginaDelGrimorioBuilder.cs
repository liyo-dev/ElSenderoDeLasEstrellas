using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aspecto del prefab de la página del grimorio: una hoja suelta que flota y gira, polvo dorado
/// alrededor y el destello al recogerla. Es la única forma de montarlo: la usan este menú y el
/// montaje de los VFX. Idempotente: rehace el visual y el brillo cada vez.
/// El brillo es un efecto propio (VfxProcedural), no de un pack: los brillos de estrellas de los
/// packs comparten material con efectos de hechizos y harían que la página pareciera uno.
/// Ver INC-523 e INC-646.
/// </summary>
public static class PaginaDelGrimorioBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Grimorio/PaginaDelGrimorio.prefab";
    public const string HojaPath = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Book/Leaflet01_a01.prefab";
    public const string CarpetaVfx = "Assets/_VFX/Mundo";
    public const string BrilloPath = CarpetaVfx + "/VFX_Pagina_Brillo.prefab";
    public const string AlRecogerPath = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab";

    /// Lado largo de la hoja, en metros.
    private const float LargoDeLaHoja = 0.55f;
    /// Inclinación hacia atrás de la hoja, para que se vea desde la cámara, que mira desde arriba.
    private const float Inclinacion = 15f;
    private static readonly Vector3 AlturaDelVisual = new Vector3(0f, 1f, 0f);
    private static readonly Color DoradoPalido = new Color(1f, 0.88f, 0.60f);

    [MenuItem("El Sendero/Magia/Página del grimorio: hoja y polvo dorado (INC-646)")]
    public static void MontarMenu()
    {
        var log = new StringBuilder("=== Página del grimorio (INC-646) ===\n");
        var warnings = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            warnings.Add($"No encuentro {PrefabPath}. Pasa antes el montaje del grimorio.");
        else
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Montar(root, warnings);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                log.AppendLine("   Hoja suelta, polvo dorado propio y destello al recogerla.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        if (warnings.Count == 0) { log.AppendLine("Sin avisos."); Debug.Log(log.ToString()); return; }
        foreach (var w in warnings) log.AppendLine("  • " + w);
        Debug.LogWarning(log.ToString());
    }

    /// Monta el visual (hoja), el brillo y el efecto al recoger en la raíz de una página.
    public static void Montar(GameObject root, List<string> warnings)
    {
        var page = root.GetComponent<PaginaDelGrimorio>();
        if (page == null) { warnings.Add("La raíz no tiene PaginaDelGrimorio."); return; }

        var brillo = ConstruirBrillo(warnings);
        var alRecoger = Load(AlRecogerPath, warnings);
        var hoja = Load(HojaPath, warnings);

        page.efectoAlRecoger = alRecoger;

        var visual = page.visual != null && page.visual != root.transform ? page.visual : null;
        if (visual == null)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = AlturaDelVisual;
            page.visual = visual;
        }
        for (int i = visual.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(visual.GetChild(i).gameObject);
        if (hoja != null) MontarHoja(hoja, visual);

        // Brillos anteriores: cualquier prefab colgado de la raíz que no sea el visual.
        foreach (Transform child in root.transform.Cast<Transform>().ToList())
            if (child != visual && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                Object.DestroyImmediate(child.gameObject);
        if (brillo != null)
        {
            var b = (GameObject)PrefabUtility.InstantiatePrefab(brillo, root.transform);
            b.transform.localPosition = Vector3.zero;
            b.transform.localScale = Vector3.one;
        }

        EditorUtility.SetDirty(page);
    }

    /// Polvo dorado de la página (a ras de suelo, bajo la hoja): motas de luz cálida que suben
    /// despacio alrededor de la hoja y un halo suave en el suelo que late. Colores sin pasar de 1,2
    /// para que el bloom no los convierta en círculos borrosos.
    private static GameObject ConstruirBrillo(List<string> warnings)
    {
        var t = new VfxProcedural.Texturas(CarpetaVfx + "/Texturas", CarpetaVfx + "/Materiales", "Mundo");
        VfxProcedural.GuardarPrefab("VFX_Pagina_Brillo", BrilloPath, root =>
        {
            Halo(root.transform, t);
            Motas(root.transform, t);
        });
        return Load(BrilloPath, warnings);
    }

    /// Mancha de luz tumbada en el suelo. Dos partículas que se solapan a destiempo hacen que lata
    /// sin huecos.
    private static void Halo(Transform padre, VfxProcedural.Texturas t)
    {
        var halo = VfxProcedural.Sistema(padre, "Halo", t.Mat("Brillo", true), ParticleSystemRenderMode.HorizontalBillboard, 1f);
        halo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        var main = halo.main;
        main.startLifetime = 2.5f;
        main.startSize = 1.1f;
        main.startColor = new Color(DoradoPalido.r, DoradoPalido.g, DoradoPalido.b, 0.22f);
        main.prewarm = true;
        VfxProcedural.Emitir(halo, 0.6f);
        VfxProcedural.Desvanecer(halo, 0.45f, 0.55f);
        var tam = halo.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.85f, 1f, 1f));
    }

    /// Motas pequeñas que salen de un anillo alrededor de la hoja y suben con una deriva suave.
    private static void Motas(Transform padre, VfxProcedural.Texturas t)
    {
        var motas = VfxProcedural.Sistema(padre, "Motas", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        motas.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var main = motas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(DoradoPalido.r * 1.2f, DoradoPalido.g * 1.2f, DoradoPalido.b * 1.2f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        VfxProcedural.Emitir(motas, 6f);
        VfxProcedural.Forma(motas, ParticleSystemShapeType.Circle, 0.35f, 1f);
        VfxProcedural.Velocidad(motas, new Vector3(0f, 0.25f, 0f));
        var ruido = motas.noise;
        ruido.enabled = true;
        ruido.strength = 0.1f;
        ruido.frequency = 0.4f;
        ruido.scrollSpeed = 0.2f;
        ruido.quality = ParticleSystemNoiseQuality.Low;
        VfxProcedural.Desvanecer(motas, 0.2f, 0.65f);
    }

    /// Hoja de pie, con el lado largo en vertical, centrada en el visual e inclinada un poco hacia
    /// atrás. La malla es de una sola cara: se pone otra copia de espaldas para que no desaparezca
    /// al girar.
    private static void MontarHoja(GameObject hoja, Transform visual)
    {
        var pivote = new GameObject("Hoja").transform;
        pivote.SetParent(visual, false);
        pivote.localRotation = Quaternion.Euler(-Inclinacion, 0f, 0f);

        var cara = (GameObject)PrefabUtility.InstantiatePrefab(hoja, pivote);
        cara.name = "Cara";
        QuitarColliders(cara);
        var t = cara.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // De pie: el eje más fino pasa a mirar hacia delante (Z) y el más largo hacia arriba (Y).
        var size = Tamano(cara, pivote);
        if (size.y <= size.x && size.y <= size.z) t.localRotation = Quaternion.Euler(-90f, 0f, 0f) * t.localRotation;
        else if (size.x <= size.y && size.x <= size.z) t.localRotation = Quaternion.Euler(0f, 90f, 0f) * t.localRotation;
        size = Tamano(cara, pivote);
        if (size.x > size.y) t.localRotation = Quaternion.Euler(0f, 0f, 90f) * t.localRotation;

        size = Tamano(cara, pivote);
        float largo = Mathf.Max(size.x, size.y, size.z);
        if (largo > 0f) t.localScale = Vector3.one * (LargoDeLaHoja / largo);
        t.localPosition = -Centro(cara, pivote);

        var dorso = (GameObject)PrefabUtility.InstantiatePrefab(hoja, pivote);
        dorso.name = "Dorso";
        QuitarColliders(dorso);
        dorso.transform.localScale = t.localScale;
        dorso.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * t.localRotation;
        dorso.transform.localPosition = Quaternion.Euler(0f, 180f, 0f) * t.localPosition;
    }

    /// Caja de los renderers en el espacio del pivote.
    private static Bounds Caja(GameObject go, Transform espacio)
    {
        var b = new Bounds();
        bool first = true;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            var m = espacio.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    private static Vector3 Tamano(GameObject go, Transform espacio) => Caja(go, espacio).size;
    private static Vector3 Centro(GameObject go, Transform espacio) => Caja(go, espacio).center - go.transform.localPosition;

    private static void QuitarColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    private static GameObject Load(string path, List<string> warnings)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) warnings.Add($"No encuentro {path}.");
        return go;
    }
}
