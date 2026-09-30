using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aspecto del prefab de la página del grimorio: una hoja suelta que flota y gira, un brillo
/// pequeño alrededor y el destello al recogerla. Es la única forma de montarlo: la usan este menú,
/// el montaje del grimorio y el de los VFX. Idempotente: rehace el visual y el brillo cada vez.
/// Ver INC-523.
/// </summary>
public static class PaginaDelGrimorioBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Grimorio/PaginaDelGrimorio.prefab";
    public const string HojaPath = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Book/Leaflet01_a01.prefab";
    public const string BrilloPath = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Sparkle/Sparkle_ellow.prefab";
    public const string AlRecogerPath = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab";

    /// Lado largo de la hoja, en metros.
    private const float LargoDeLaHoja = 0.55f;
    /// Inclinación hacia atrás de la hoja, para que se vea desde la cámara, que mira desde arriba.
    private const float Inclinacion = 15f;
    /// El brillo de Lana Studio está hecho para verse de lejos (estrellas de 2 a 20 m en un radio de 3 m).
    private const float EscalaDelBrillo = 0.2f;
    private static readonly Vector3 AlturaDelVisual = new Vector3(0f, 1f, 0f);

    [MenuItem("El Sendero/Magia/Página del grimorio: hoja y brillo pequeño (INC-523)")]
    public static void MontarMenu()
    {
        var log = new StringBuilder("=== Página del grimorio (INC-523) ===\n");
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
                log.AppendLine("   Hoja suelta, brillo a escala " + EscalaDelBrillo + " y destello al recogerla.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
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

        var brillo = Load(BrilloPath, warnings);
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
            b.transform.localPosition = AlturaDelVisual;
            b.transform.localScale = Vector3.one * EscalaDelBrillo;
        }

        EditorUtility.SetDirty(page);
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
