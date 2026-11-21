using System.Collections.Generic;
using System.IO;
using System.Text;
using Kits.Core;
using UnityEditor;
using UnityEngine;

namespace Kits.EditorTools
{
    /// <summary>
    /// Saca las ruedas de un vehículo hecho de una sola malla (carros, carretillas de packs) para que
    /// puedan girar: busca en la malla las piezas sueltas con forma de rueda (un disco de canto, que
    /// toca el suelo), las separa en objetos propios con el pivote en su centro, junta con cada una
    /// lo que va pegado a su buje y deja el resto como cuerpo. El resultado es un prefab nuevo con
    /// <see cref="WheelSpin"/> ya configurado; el prefab original no se toca.
    /// </summary>
    public static class WheelSeparator
    {
        [MenuItem("Tools/EditorTools/Separar las ruedas del prefab seleccionado")]
        private static void FromSelection()
        {
            var source = Selection.activeObject as GameObject;
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                Debug.LogWarning("[WheelSeparator] Selecciona un prefab en la ventana Project.");
                return;
            }
            string output = Path.Combine(Path.GetDirectoryName(path) ?? "Assets", Path.GetFileNameWithoutExtension(path) + "_Wheels.prefab").Replace('\\', '/');
            Separate(source, output, out string report);
            Debug.Log(report);
        }

        /// <summary>
        /// Crea (o rehace) en <paramref name="outputPrefabPath"/> una copia de <paramref name="sourcePrefab"/>
        /// con las ruedas separadas y <see cref="WheelSpin"/>. Las mallas nuevas van en un asset al lado
        /// («…_Meshes.asset»). Devuelve el prefab nuevo, o null si no encuentra ruedas.
        /// </summary>
        public static GameObject Separate(GameObject sourcePrefab, string outputPrefabPath, out string report)
        {
            var sb = new StringBuilder();
            report = "";
            if (sourcePrefab == null) { report = "[WheelSeparator] Falta el prefab de origen."; return null; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = Path.GetFileNameWithoutExtension(outputPrefabPath);
            try
            {
                MeshFilter mf = null;
                foreach (var f in inst.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null &&
                        (mf == null || f.sharedMesh.vertexCount > mf.sharedMesh.vertexCount)) mf = f;
                if (mf == null) { report = $"[WheelSeparator] {sourcePrefab.name}: no tiene ninguna malla visible."; return null; }

                var found = FindWheels(mf.sharedMesh);
                if (found.Count == 0) { report = $"[WheelSeparator] {sourcePrefab.name}: no se encuentra ninguna pieza con forma de rueda."; return null; }

                string folder = Path.GetDirectoryName(outputPrefabPath)?.Replace('\\', '/');
                AssetFolders.Ensure(folder);
                string meshesPath = $"{folder}/{Path.GetFileNameWithoutExtension(outputPrefabPath)}_Meshes.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(meshesPath) != null) AssetDatabase.DeleteAsset(meshesPath);

                var source = mf.sharedMesh;
                var wheelTris = new HashSet<int>();
                foreach (var w in found) wheelTris.UnionWith(w.triangles);
                Mesh body = Build(source, null, wheelTris, invert: true, Vector3.zero);
                body.name = source.name + "_Body";
                AssetDatabase.CreateAsset(body, meshesPath);

                var renderer = mf.GetComponent<MeshRenderer>();
                var spin = new WheelSpin.Wheel[found.Count];
                for (int i = 0; i < found.Count; i++)
                {
                    var w = found[i];
                    Mesh m = Build(source, w.triangles, null, invert: false, w.center);
                    m.name = $"{source.name}_Wheel{i}";
                    AssetDatabase.AddObjectToAsset(m, meshesPath);
                    var go = new GameObject($"Wheel {i}");
                    go.transform.SetParent(mf.transform, false);
                    go.transform.localPosition = w.center;
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = renderer.sharedMaterials;
                    r.shadowCastingMode = renderer.shadowCastingMode;
                    r.receiveShadows = renderer.receiveShadows;
                    spin[i] = new WheelSpin.Wheel { transform = go.transform, radius = w.radius, axle = w.axle };
                    sb.AppendLine($"  rueda {i}: centro {w.center}, radio {w.radius:0.###}, eje {w.axle}, {w.triangles.Count} triángulos");
                }
                mf.sharedMesh = body;
                var wheelSpin = inst.GetComponent<WheelSpin>();
                if (wheelSpin == null) wheelSpin = inst.AddComponent<WheelSpin>();
                wheelSpin.Configure(spin);

                AssetDatabase.SaveAssets();
                var prefab = PrefabUtility.SaveAsPrefabAsset(inst, outputPrefabPath);
                report = $"[WheelSeparator] {sourcePrefab.name} → {outputPrefabPath}: {found.Count} rueda(s).\n" + sb;
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        private sealed class FoundWheel
        {
            public readonly List<int> triangles = new();
            public Vector3 center, axle;
            public float radius;
        }

        private sealed class Island
        {
            public readonly List<int> triangles = new();   // índice del primer vértice del triángulo (sobre la lista global)
            public Bounds bounds;
            public bool hasBounds;
            public bool used;
        }

        /// Las piezas sueltas (vértices soldados por posición) con forma de rueda: un disco de canto
        /// (grosor < 45 % del diámetro, ancho y alto parecidos), con el eje horizontal y tocando el suelo
        /// de la malla. Lo que queda dentro del buje de una rueda (tapacubos, ejes cortos) va con ella.
        private static List<FoundWheel> FindWheels(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            var parent = new int[v.Length];
            for (int i = 0; i < v.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Join(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

            float weld = Mathf.Max(mesh.bounds.size.magnitude * 1e-4f, 1e-5f);
            var byPos = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x / weld), Mathf.RoundToInt(v[i].y / weld), Mathf.RoundToInt(v[i].z / weld));
                if (byPos.TryGetValue(k, out int j)) Join(i, j); else byPos[k] = i;
            }

            // Triángulos de todas las submallas, numerados en una lista global: (submalla, índice).
            var all = new List<(int sub, int a, int b, int c)>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] t = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    all.Add((s, t[i], t[i + 1], t[i + 2]));
                    Join(t[i], t[i + 1]); Join(t[i], t[i + 2]);
                }
            }

            var islands = new Dictionary<int, Island>();
            for (int ti = 0; ti < all.Count; ti++)
            {
                int root = Find(all[ti].a);
                if (!islands.TryGetValue(root, out var isl)) islands[root] = isl = new Island();
                isl.triangles.Add(ti);
                foreach (int vi in new[] { all[ti].a, all[ti].b, all[ti].c })
                {
                    if (!isl.hasBounds) { isl.bounds = new Bounds(v[vi], Vector3.zero); isl.hasBounds = true; }
                    else isl.bounds.Encapsulate(v[vi]);
                }
            }

            float minY = mesh.bounds.min.y, height = Mathf.Max(mesh.bounds.size.y, 1e-4f);
            var wheels = new List<FoundWheel>();
            foreach (var isl in islands.Values)
            {
                Vector3 s = isl.bounds.size;
                int thin = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
                if (thin == 1) continue;                     // eje vertical: no es una rueda
                // Las dos medidas del disco (las que no son el grosor).
                float a = thin == 0 ? s.y : s.x, b = thin == 0 ? s.z : s.y;
                float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                if (lo <= 1e-5f || s[thin] > 0.45f * lo || hi / lo > 1.3f) continue;
                if (isl.bounds.min.y > minY + 0.06f * height) continue;
                var w = new FoundWheel { center = isl.bounds.center, radius = hi * 0.5f };
                w.axle = thin == 0 ? Vector3.right : Vector3.forward;
                isl.used = true;
                w.triangles.AddRange(isl.triangles);
                wheels.Add(w);
            }
            // Lo pegado al buje: piezas pequeñas cuyo centro cae dentro del disco, cerca del eje.
            foreach (var isl in islands.Values)
            {
                if (isl.used) continue;
                foreach (var w in wheels)
                {
                    Vector3 d = isl.bounds.center - w.center;
                    Vector3 inPlane = d - Vector3.Project(d, w.axle);
                    float along = Mathf.Abs(Vector3.Dot(d, w.axle));
                    if (inPlane.magnitude < w.radius * 0.5f && along < w.radius && isl.bounds.size.magnitude < w.radius * 2f)
                    {
                        w.triangles.AddRange(isl.triangles);
                        isl.used = true;
                        break;
                    }
                }
            }
            // Las listas de triángulos guardan índices de la lista global; Build los traduce.
            s_all = all;
            return wheels;
        }

        private static List<(int sub, int a, int b, int c)> s_all;

        /// Una malla con los triángulos pedidos (o todos menos esos, con invert), con los vértices
        /// desplazados para que «origin» quede en el pivote. Conserva las submallas (materiales).
        private static Mesh Build(Mesh src, List<int> take, HashSet<int> skip, bool invert, Vector3 origin)
        {
            var all = s_all;
            Vector3[] v = src.vertices, n = src.normals;
            Vector4[] tg = src.tangents;
            Color[] col = src.colors;
            var uv = new List<Vector2>[4];
            for (int c = 0; c < 4; c++) { uv[c] = new List<Vector2>(); src.GetUVs(c, uv[c]); }

            var remap = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nc = new List<Color>();
            var nuv = new List<Vector2>[4];
            for (int c = 0; c < 4; c++) nuv[c] = new List<Vector2>();
            var subs = new List<int>[src.subMeshCount];
            for (int s = 0; s < subs.Length; s++) subs[s] = new List<int>();

            int Map(int i)
            {
                if (remap.TryGetValue(i, out int j)) return j;
                j = nv.Count; remap[i] = j;
                nv.Add(v[i] - origin);
                if (n.Length == v.Length) nn.Add(n[i]);
                if (tg.Length == v.Length) nt.Add(tg[i]);
                if (col.Length == v.Length) nc.Add(col[i]);
                for (int c = 0; c < 4; c++) if (uv[c].Count == v.Length) nuv[c].Add(uv[c][i]);
                return j;
            }

            IEnumerable<int> chosen;
            if (!invert) chosen = take;
            else
            {
                var rest = new List<int>();
                for (int i = 0; i < all.Count; i++) if (!skip.Contains(i)) rest.Add(i);
                chosen = rest;
            }
            foreach (int ti in chosen)
            {
                var t = all[ti];
                subs[t.sub].Add(Map(t.a)); subs[t.sub].Add(Map(t.b)); subs[t.sub].Add(Map(t.c));
            }

            var m = new Mesh();
            if (nv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(nv);
            if (nn.Count == nv.Count) m.SetNormals(nn);
            if (nt.Count == nv.Count) m.SetTangents(nt);
            if (nc.Count == nv.Count) m.SetColors(nc);
            for (int c = 0; c < 4; c++) if (nuv[c].Count == nv.Count && nuv[c].Count > 0) m.SetUVs(c, nuv[c]);
            m.subMeshCount = subs.Length;
            for (int s = 0; s < subs.Length; s++) m.SetTriangles(subs[s], s);
            m.RecalculateBounds();
            if (nn.Count != nv.Count) m.RecalculateNormals();
            return m;
        }
    }
}
