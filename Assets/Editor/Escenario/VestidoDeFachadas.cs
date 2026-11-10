#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone piezas en las fachadas de un edificio (puertas, ventanas, jardineras, carteles) pegadas a
/// su pared de verdad: mide la malla del edificio con rayos, busca un trozo de pared plano del
/// tamaño de la pieza y la apoya ahí con su espalda contra la pared. No depende de cómo esté
/// modelado el edificio ni de hacia dónde mire el prefab de la pieza: las dos cosas se miden.
///
/// Es la parte genérica; qué pieza va en qué casa lo decide quien la usa (p. ej. <see cref="VestidoDelPrologo"/>).
/// </summary>
public static class VestidoDeFachadas
{
    // ── El edificio ─────────────────────────────────────────────────────────────────────────

    /// <summary>Un edificio medido: sus triángulos en el mundo y su caja en sus propios ejes.</summary>
    public sealed class Edificio
    {
        public Transform raiz;
        public Bounds local;               // en ejes de la raíz (sin escala de mundo: posiciones locales)
        public readonly List<Vector3> tris = new();   // de tres en tres, en el mundo
        public Vector3 Centro => raiz.TransformPoint(local.center);
        public float BaseY;                // altura del mundo de la parte más baja
    }

    public static Edificio Medir(Transform raiz)
    {
        var e = new Edificio { raiz = raiz };
        bool primero = true;
        float baseY = float.MaxValue;
        foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(false))
        {
            var r = mf.GetComponent<Renderer>();
            if (r == null || !r.enabled || mf.sharedMesh == null) continue;
            var m = mf.sharedMesh;
            if (!m.isReadable && !Application.isEditor) continue;
            var v = m.vertices;
            var aMundo = mf.transform.localToWorldMatrix;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var t = m.GetTriangles(s);
                for (int i = 0; i < t.Length; i++) e.tris.Add(aMundo.MultiplyPoint3x4(v[t[i]]));
            }
            foreach (var p in v)
            {
                Vector3 w = aMundo.MultiplyPoint3x4(p);
                Vector3 l = raiz.InverseTransformPoint(w);
                if (primero) { e.local = new Bounds(l, Vector3.zero); primero = false; }
                else e.local.Encapsulate(l);
                baseY = Mathf.Min(baseY, w.y);
            }
        }
        e.BaseY = baseY;
        return e;
    }

    /// <summary>Primer choque de un rayo con el edificio (distancia y normal de la cara), o false.</summary>
    public static bool Rayo(Edificio e, Vector3 desde, Vector3 dir, float max, out float dist, out Vector3 normal)
    {
        dist = max; normal = Vector3.zero;
        bool hay = false;
        var t = e.tris;
        for (int i = 0; i + 2 < t.Count; i += 3)
        {
            Vector3 a = t[i], b = t[i + 1], c = t[i + 2];
            Vector3 ab = b - a, ac = c - a;
            Vector3 p = Vector3.Cross(dir, ac);
            float det = Vector3.Dot(ab, p);
            if (Mathf.Abs(det) < 1e-7f) continue;
            float inv = 1f / det;
            Vector3 s = desde - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) continue;
            Vector3 q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0f || u + v > 1f) continue;
            float d = Vector3.Dot(ac, q) * inv;
            if (d <= 0f || d >= dist) continue;
            dist = d; normal = Vector3.Cross(ab, ac).normalized; hay = true;
        }
        if (hay && Vector3.Dot(normal, dir) > 0f) normal = -normal;
        return hay;
    }

    // ── Las fachadas ────────────────────────────────────────────────────────────────────────

    /// <summary>Una cara del edificio: hacia dónde mira, cuánto mide y dónde está su centro (en el suelo).</summary>
    public struct Fachada
    {
        public Vector3 fuera;      // horizontal, hacia fuera
        public Vector3 lateral;    // horizontal, a lo largo de la fachada (derecha de quien la mira desde fuera)
        public Vector3 centro;     // centro de la cara de la caja, a la altura de la base
        public float ancho, fondo; // ancho de la fachada; fondo = de la cara de la caja hacia dentro
        public string nombre;
    }

    /// <summary>Las cuatro caras de la caja del edificio, ordenadas de más a menos de cara a <paramref name="mira"/>.</summary>
    public static List<Fachada> Fachadas(Edificio e, Vector3 mira)
    {
        var lista = new List<Fachada>();
        var ejes = new (Vector3 eje, float media, float otra, string n)[]
        {
            (Vector3.right, e.local.extents.x, e.local.extents.z, "x+"), (Vector3.left, e.local.extents.x, e.local.extents.z, "x-"),
            (Vector3.forward, e.local.extents.z, e.local.extents.x, "z+"), (Vector3.back, e.local.extents.z, e.local.extents.x, "z-"),
        };
        foreach (var (eje, media, otra, n) in ejes)
        {
            Vector3 fuera = e.raiz.TransformDirection(eje); fuera.y = 0f; fuera.Normalize();
            Vector3 escala = e.raiz.lossyScale;
            float k = Mathf.Abs(eje.x) > 0.5f ? escala.x : escala.z;
            float kOtra = Mathf.Abs(eje.x) > 0.5f ? escala.z : escala.x;
            Vector3 c = e.raiz.TransformPoint(e.local.center + Vector3.Scale(eje, e.local.extents));
            c.y = e.BaseY;
            lista.Add(new Fachada
            {
                fuera = fuera, lateral = Vector3.Cross(Vector3.up, fuera).normalized * -1f,
                centro = c, ancho = otra * 2f * Mathf.Abs(kOtra), fondo = media * 2f * Mathf.Abs(k), nombre = n,
            });
        }
        Vector3 centro = e.Centro;
        lista.Sort((a, b) => Vector3.Dot(b.fuera, Flat(mira - centro)).CompareTo(Vector3.Dot(a.fuera, Flat(mira - centro))));
        return lista;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

    /// <summary>
    /// ¿Hay pared plana para una pieza de <paramref name="ancho"/>×<paramref name="alto"/> con su base a
    /// <paramref name="altura"/> sobre la base del edificio, centrada a <paramref name="u"/> metros del
    /// centro de la fachada? Devuelve el punto de la pared bajo el centro de la base de la pieza.
    /// </summary>
    public static bool ParedPlana(Edificio e, Fachada f, float u, float altura, float ancho, float alto, float tolerancia, out Vector3 enPared)
    {
        enPared = default;
        float sale = 1.5f, max = f.fondo + sale;
        float minD = float.MaxValue, maxD = float.MinValue;
        int muestras = 0;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                float du = (i - 1) * ancho * 0.45f;
                float dy = 0.06f + j * (alto - 0.12f) * 0.5f;
                Vector3 o = f.centro + f.lateral * (u + du) + Vector3.up * (altura + dy) + f.fuera * sale;
                if (!Rayo(e, o, -f.fuera, max, out float d, out Vector3 n)) return false;
                if (Vector3.Dot(n, f.fuera) < 0.9f) return false;          // no es pared vertical de cara
                if (d > sale + 1.2f) return false;                          // hueco profundo (esquina entrante)
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
                muestras++;
            }
        if (muestras == 0 || maxD - minD > tolerancia) return false;
        enPared = f.centro + f.lateral * u + Vector3.up * altura + f.fuera * (sale - minD);
        return true;
    }

    // ── Las piezas ──────────────────────────────────────────────────────────────────────────

    /// <summary>Una pieza medida en los ejes de su prefab: hacia dónde da su cara, y su caja en esos ejes.</summary>
    public sealed class Pieza
    {
        public GameObject prefab;
        public Vector3 fuera, lateral;           // ejes locales del prefab (horizontales)
        public float dMin, dMax, lMin, lMax, yMin, yMax;
        public float Ancho => lMax - lMin;
        public float Alto => yMax - yMin;
        public float Fondo => dMax - dMin;
    }

    private static readonly Dictionary<GameObject, Pieza> s_piezas = new();

    /// <param name="espaldaEnElPivote">Piezas hechas con el pivote en la pared (las de Blender): la cara
    /// da hacia donde queda el volumen respecto al pivote. Sin esto, hacia donde miran más caras.</param>
    public static Pieza MedirPieza(GameObject prefab, bool espaldaEnElPivote = false)
    {
        if (prefab == null) return null;
        if (s_piezas.TryGetValue(prefab, out var ya) && ya != null) return ya;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var raiz = go.transform;
            var pts = new List<Vector3>();
            Vector3 suma = Vector3.zero;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; var v = m.vertices; var tr = m.triangles;
                var aRaiz = raiz.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var p in v) pts.Add(aRaiz.MultiplyPoint3x4(p));
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    Vector3 a = aRaiz.MultiplyPoint3x4(v[tr[i]]), b = aRaiz.MultiplyPoint3x4(v[tr[i + 1]]), c = aRaiz.MultiplyPoint3x4(v[tr[i + 2]]);
                    suma += Vector3.Cross(b - a, c - a);   // normal × 2·área
                }
            }
            if (pts.Count == 0) return null;
            var caja = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) caja.Encapsulate(p);
            // La cara da hacia el eje horizontal más fino, del lado al que miran más caras.
            Vector3 eje = caja.size.x < caja.size.z ? Vector3.right : Vector3.forward;
            float signo = espaldaEnElPivote ? Vector3.Dot(caja.center, eje) : Vector3.Dot(suma, eje);
            var pz = new Pieza { prefab = prefab, fuera = signo >= 0f ? eje : -eje };
            pz.lateral = Vector3.Cross(Vector3.up, pz.fuera).normalized * -1f;
            pz.dMin = pz.lMin = pz.yMin = float.MaxValue; pz.dMax = pz.lMax = pz.yMax = float.MinValue;
            foreach (var p in pts)
            {
                float d = Vector3.Dot(p, pz.fuera), l = Vector3.Dot(p, pz.lateral);
                pz.dMin = Mathf.Min(pz.dMin, d); pz.dMax = Mathf.Max(pz.dMax, d);
                pz.lMin = Mathf.Min(pz.lMin, l); pz.lMax = Mathf.Max(pz.lMax, l);
                pz.yMin = Mathf.Min(pz.yMin, p.y); pz.yMax = Mathf.Max(pz.yMax, p.y);
            }
            s_piezas[prefab] = pz;
            return pz;
        }
        finally { Object.DestroyImmediate(go); }
    }

    public static void OlvidarMedidas() => s_piezas.Clear();

    /// <summary>
    /// Pone la pieza con el centro de su base en <paramref name="enPared"/> (punto de la pared) y su
    /// espalda metida <paramref name="hundir"/> metros en la pared. <paramref name="saliente"/> la separa
    /// de la pared (jardineras, carteles de bandera).
    /// </summary>
    public static GameObject Poner(Pieza p, Fachada f, Vector3 enPared, Transform padre, string nombre, float hundir = 0.02f, float saliente = 0f, float escala = 1f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p.prefab, padre.gameObject.scene);
        go.name = nombre;
        go.transform.SetParent(padre, true);
        Quaternion giro = Quaternion.FromToRotation(p.fuera, f.fuera);
        // FromToRotation entre dos horizontales opuestas puede girar sobre cualquier eje: forzar el vertical.
        if (Vector3.Dot(p.fuera, f.fuera) < -0.999f) giro = Quaternion.AngleAxis(180f, Vector3.up);
        go.transform.localScale = go.transform.localScale * escala;
        Vector3 ancla = (p.lateral * ((p.lMin + p.lMax) * 0.5f) + Vector3.up * p.yMin + p.fuera * p.dMin) * escala;
        go.transform.SetPositionAndRotation(enPared + f.fuera * (saliente - hundir) - giro * ancla, giro);
        return go;
    }

    /// <summary>Quita los colisionadores de lo generado: son piezas pegadas a la pared o pequeñas, y no deben tocar NavMesh ni cámaras.</summary>
    public static void SinColisiones(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }
}
#endif
