using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// El tablón de avisos de la plaza (INC-1012): en la tabla del poste indicador, por la cara que se ve
/// al llegar al pueblo (la de fuera, no la de la plaza; INC-1078), tres papeles clavados: se busca ayuda para la siega, el dibujo del globo que ha hecho
/// Nina y la gallina perdida de la panadería. Las texturas salen de Tools/Texturas/tablon_de_avisos.py.
/// </summary>
public static partial class VestidoDelPrologo
{
    const string Poste = "Poste_Indicador";
    const string CarpetaTablon = CarpetaPueblo + "/Tablon";
    // nombre, ancho (m), posición en la tabla (0-1 a lo ancho y a lo alto) y giro en grados. El palo pasa
    // por delante del centro de la tabla, así que los papeles van a los lados.
    static readonly (string tex, float ancho, float u, float v, float giro)[] Papeles =
    {
        ("Aviso_Siega", 0.26f, 0.21f, 0.68f, 3f),
        ("Aviso_Gallina", 0.21f, 0.25f, 0.24f, -5f),
        ("Dibujo_Nina", 0.34f, 0.77f, 0.55f, -4f),
    };

    static int PonerTablon(Transform raiz, StringBuilder informe)
    {
        MeshFilter poste = null;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            if (mf.name == Poste && mf.sharedMesh != null) { poste = mf; break; }
        if (poste == null) { informe.AppendLine("Tablón: no encuentro " + Poste + "."); return 0; }

        // La tabla es lo que sobresale del palo a los lados: sus vértices dan el ancho, el alto y la cara.
        var m = poste.transform.localToWorldMatrix;
        var todos = new List<Vector3>();
        foreach (var v in poste.sharedMesh.vertices) todos.Add(m.MultiplyPoint3x4(v));
        var caja = new Bounds(todos[0], Vector3.zero);
        foreach (var w in todos) caja.Encapsulate(w);
        bool caraEnX = caja.size.x < caja.size.z;
        Vector3 haciaElCamino = caja.center - Plaza; haciaElCamino.y = 0f;
        Vector3 fuera = caraEnX ? new Vector3(Mathf.Sign(haciaElCamino.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(haciaElCamino.z));
        Vector3 lado = Vector3.Cross(Vector3.up, fuera);
        bool hay = false;
        var b = new Bounds();
        foreach (var w in todos)
        {
            if (Mathf.Abs(Vector3.Dot(w - caja.center, lado)) < 0.15f) continue;
            if (!hay) { b = new Bounds(w, Vector3.zero); hay = true; } else b.Encapsulate(w);
        }
        if (!hay) { informe.AppendLine("Tablón: no distingo la tabla del palo."); return 0; }
        float ancho = Mathf.Abs(Vector3.Dot(b.size, lado));
        float alto = b.size.y;
        Vector3 cara = new Vector3(b.center.x, b.center.y, b.center.z);
        cara += fuera * (Vector3.Dot(b.max - b.center, fuera) >= 0f ? Mathf.Abs(Vector3.Dot(b.extents, fuera)) : 0f) + fuera * 0.006f;
        float escala = Mathf.Min(1f, ancho * 0.9f / 0.83f, alto * 0.85f / 0.4f);

        // La tabla puede estar algo girada: cada papel se apoya donde le dé un rayo contra la malla.
        var choque = poste.gameObject.AddComponent<MeshCollider>();
        choque.sharedMesh = poste.sharedMesh;
        Physics.SyncTransforms();

        var grupo = new GameObject("Tablón de avisos").transform;
        grupo.SetParent(raiz, false);
        int n = 0;
        foreach (var (tex, anchoPapel, u, v, giro) in Papeles)
        {
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaTablon}/{tex}.png");
            if (textura == null) { informe.AppendLine($"Tablón: falta {tex}.png."); continue; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Papel · " + tex;
            go.transform.SetParent(grupo, false);
            float anchoReal = anchoPapel * escala, altoPapel = anchoReal * textura.height / textura.width;
            Vector3 punto = cara + lado * ((u - 0.5f) * ancho) + Vector3.up * ((v - 0.5f) * alto * 0.9f);
            Vector3 normal = fuera;
            if (choque.Raycast(new Ray(punto + fuera * 1f, -fuera), out var golpe, 2f))
            {
                normal = Vector3.ProjectOnPlane(golpe.normal, Vector3.up).normalized;
                if (normal.sqrMagnitude < 0.5f) normal = fuera;
                punto = golpe.point;
            }
            go.transform.position = punto + normal * (0.006f + 0.002f * n);
            // El Quad mira hacia su -Z: con +Z hacia la tabla, el dibujo queda hacia fuera.
            go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(0f, 0f, giro);
            go.transform.localScale = new Vector3(anchoReal, altoPapel, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialDePapel(tex, textura);
            r.shadowCastingMode = ShadowCastingMode.Off;
            n++;
        }
        Object.DestroyImmediate(choque);
        informe.AppendLine($"Tablón de avisos: {n} papeles en {Poste} (tabla de {ancho:0.00} × {alto:0.00} m).");
        return n;
    }

    static Material MaterialDePapel(string nombre, Texture2D textura)
    {
        string ruta = $"{CarpetaTablon}/{nombre}.mat";
        // Con el material del pack (el mismo sombreado que las casas), como el árbol en flor.
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Tree.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, ruta); }
        else { mat.shader = baseMat.shader; mat.CopyPropertiesFromMaterial(baseMat); }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", textura);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", textura);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_AlphaClip")) { mat.SetFloat("_AlphaClip", 1f); mat.EnableKeyword("_ALPHATEST_ON"); }
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
