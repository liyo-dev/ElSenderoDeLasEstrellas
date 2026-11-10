using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// El campo del valle del prólogo (INC-1006): matas de hierba y flores por el prado, y árboles en
/// corros por las lomas, con algún árbol en flor (rosa) cerca del pueblo y del río. Todo del Fantasy
/// Kingdom Pack, como las casas; el árbol en flor es el Tree04 del pack con su textura pasada a rosa
/// (Assets/Art/World/Pueblo/ArbolEnFlor_D.png). Se rehace con el resto del vestido.
/// Nada se pone en el camino, en el río, en la plaza ni pegado a una casa o a una marca del guion.
/// </summary>
public static partial class VestidoDelPrologo
{
    const string Vegetacion = Pack + "Vegetation/";
    // Solo las matas bajas: las «a02» son hojas altas en espiral que, en cantidad, parecen helechos.
    static readonly string[] Hierbas = { "Grass01_a01", "Grass02_a01", "Grass03_a01" };
    static readonly string[] Flores = { "Flower01_a01", "Flower01_b01", "Flower01_c01", "Flower02_a01", "Flower02_b01", "Flower03_a01", "Flower04_a01", "Flower05_a01" };
    static readonly string[] Matas = { "Plant01_a01", "Plant01_b01", "Plant02_a01", "Plant03_a01", "Plant04_a01" };
    // Los Tree03 redondos verdes y los pinos pequeños Tree01 (el Tree02_a01 es un árbol gigante y los Tree04 son turquesa).
    static readonly string[] Arboles = { "Tree03_a01", "Tree03_a02", "Tree03_c01", "Tree03_c02", "Tree03_a01", "Tree03_c01", "Tree01_a01", "Tree01_b01" };
    static readonly string[] ArbolesEnFlor = { "Tree04_a01", "Tree04_a02", "Tree04_a03" };
    const string MaterialEnFlor = CarpetaPueblo + "/ArbolEnFlor.mat";
    const string TexturaEnFlor = CarpetaPueblo + "/ArbolEnFlor_D.png";

    const float RadioDelPueblo = 17f;      // dentro, solo la plaza y las calles: ni árboles ni matas
    const float RadioDeLaHierba = 8f;      // la hierba y las flores llegan hasta las casas (no a la plaza)
    const float RadioDelCampo = 85f;

    static int PonerCampo(Transform raiz, StringBuilder informe)
    {
        s_caminos.Clear();
        var campo = new GameObject("Campo del valle").transform;
        campo.SetParent(raiz, false);
        var rnd = new System.Random(1406);
        var marcas = new List<Vector3>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name.StartsWith("M_") || t.GetComponent<NpcSpawnPoint>() != null) marcas.Add(t.position);
        var enFlor = MaterialDelArbolEnFlor(informe);

        int n = 0;
        // Hierba y flores: matas sueltas por el prado, más espesas en corros.
        n += Esparcir(campo, "Hierba", Hierbas, 3400, 1.3f, 2.0f, 0.4f, marcas, rnd, null, sombra: false, radioMin: RadioDeLaHierba);
        n += Esparcir(campo, "Flores", Flores, 700, 1.1f, 1.7f, 0.7f, marcas, rnd, null, sombra: false, radioMin: RadioDeLaHierba);
        n += Esparcir(campo, "Matas", Matas, 90, 0.45f, 0.75f, 1.6f, marcas, rnd, null, sombra: true);
        // Árboles: corros por las lomas; los rosas, más cerca del pueblo y del río.
        n += Esparcir(campo, "Árboles", Arboles, 80, 1.3f, 1.9f, 4.0f, marcas, rnd, null, sombra: true, radioMin: RadioDelPueblo + 10f);
        n += Esparcir(campo, "Árboles en flor", ArbolesEnFlor, 18, 1.0f, 1.3f, 4.0f, marcas, rnd, enFlor, sombra: true,
                      radioMin: RadioDelPueblo + 4f, radioMax: 45f);

        foreach (var t in campo.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        informe.AppendLine($"Campo del valle: {n} piezas (hierba, flores, matas y árboles; {(enFlor != null ? "con" : "sin")} árboles en flor).");
        return n;
    }

    /// Pone <paramref name="cuantas"/> piezas al azar en el prado (entre el pueblo y el borde del campo).
    /// <paramref name="hueco"/>: distancia mínima a cualquier cosa de la escena que no sea el suelo.
    static int Esparcir(Transform padre, string nombre, string[] prefabs, int cuantas, float escalaMin, float escalaMax, float hueco,
        List<Vector3> marcas, System.Random rnd, Material material, bool sombra, float radioMin = RadioDelPueblo, float radioMax = RadioDelCampo)
    {
        var grupo = new GameObject(nombre).transform;
        grupo.SetParent(padre, false);
        var piezas = new List<GameObject>();
        foreach (var p in prefabs)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Vegetacion + p + ".prefab");
            if (go != null) piezas.Add(go);
        }
        if (piezas.Count == 0) return 0;
        int puestas = 0;
        for (int intento = 0; intento < cuantas * 6 && puestas < cuantas; intento++)
        {
            // Más densidad cerca del pueblo: el radio sale de una raíz (reparto uniforme por área) sesgada.
            float r = Mathf.Lerp(radioMin, radioMax, Mathf.Pow((float)rnd.NextDouble(), 0.8f));
            float a = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
            var xz = new Vector3(Plaza.x + Mathf.Cos(a) * r, 0f, Plaza.z + Mathf.Sin(a) * r);
            if (!SueloDelCampo(xz, out var punto, out var normal)) continue;
            if (Vector3.Angle(normal, Vector3.up) > 28f) continue;
            if (CercaDeMarca(punto, marcas, hueco + 1.5f)) continue;
            if (HayAlgoCerca(punto, hueco)) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(piezas[rnd.Next(piezas.Count)], grupo);
            float e = Mathf.Lerp(escalaMin, escalaMax, (float)rnd.NextDouble());
            go.transform.SetPositionAndRotation(punto - Vector3.up * 0.03f, Quaternion.Euler(0f, (float)(rnd.NextDouble() * 360.0), 0f));
            go.transform.localScale *= e;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (var rr in go.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null) rr.sharedMaterial = material;
                if (!sombra) rr.shadowCastingMode = ShadowCastingMode.Off;
            }
            puestas++;
        }
        return puestas;
    }

    /// El suelo del prado en ese punto: el terreno, y no el camino, el río ni el agua.
    static bool SueloDelCampo(Vector3 xz, out Vector3 punto, out Vector3 normal)
    {
        punto = normal = default;
        var hits = Physics.RaycastAll(new Vector3(xz.x, 160f, xz.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        var h = hits[0];
        string n = h.collider.name.ToLowerInvariant();
        if (n.Contains("camino") || n.Contains("rio") || n.Contains("agua") || n.Contains("water") || n.Contains("cauce")) return false;
        if (h.collider.gameObject.layer == LayerMask.NameToLayer("Water")) return false;
        bool esSuelo = h.collider.bounds.size.x > 30f || h.collider.GetComponent<Terrain>() != null || n.Contains("terreno") || n.StartsWith("suelo");
        if (!esSuelo) return false;
        // Lo que no tiene collider (el camino pintado, el agua) se busca por sus mallas.
        punto = h.point; normal = h.normal;
        return !SobreCaminoOAgua(h.point);
    }

    static readonly List<Renderer> s_caminos = new();
    static bool SobreCaminoOAgua(Vector3 p)
    {
        if (s_caminos.Count == 0)
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string n = (r.name + " " + (r.transform.parent != null ? r.transform.parent.name : "")).ToLowerInvariant();
                if (n.Contains("camino") || n.Contains("rio") || n.Contains("agua") || n.Contains("cauce")) s_caminos.Add(r);
            }
        foreach (var r in s_caminos)
        {
            if (r == null || !r.bounds.Contains(new Vector3(p.x, r.bounds.center.y, p.z))) continue;
            // Las mallas del camino y del río son grandes: se comprueba con un rayo contra su malla.
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return true;
            var temp = new GameObject("_Prueba") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                temp.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                temp.transform.localScale = r.transform.lossyScale;
                var mc = temp.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                if (mc.Raycast(new Ray(new Vector3(p.x, p.y + 30f, p.z), Vector3.down), out _, 60f)) return true;
            }
            finally { Object.DestroyImmediate(temp); }
        }
        return false;
    }

    static bool CercaDeMarca(Vector3 p, List<Vector3> marcas, float distancia)
    {
        foreach (var m in marcas)
            if ((new Vector2(m.x - p.x, m.z - p.z)).sqrMagnitude < distancia * distancia) return true;
        return false;
    }

    /// Algo de la escena (casa, valla, roca, otro árbol) a menos de <paramref name="hueco"/>, sin contar el suelo.
    static bool HayAlgoCerca(Vector3 p, float hueco)
    {
        foreach (var col in Physics.OverlapSphere(p + Vector3.up * (hueco * 0.5f + 0.2f), hueco, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = col.name.ToLowerInvariant();
            if (col.bounds.size.x > 30f || col.GetComponent<TerrainCollider>() != null || n.Contains("terreno")) continue;
            return true;
        }
        return false;
    }

    /// El material del árbol en flor: el del pack con la textura pasada a rosa.
    static Material MaterialDelArbolEnFlor(StringBuilder informe)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturaEnFlor);
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Tree.mat");
        if (tex == null || baseMat == null) { informe.AppendLine("Árbol en flor: falta la textura o el material del pack."); return null; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialEnFlor);
        if (mat == null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, MaterialEnFlor); }
        else mat.CopyPropertiesFromMaterial(baseMat);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.66f, 0.56f, 0.6f, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
