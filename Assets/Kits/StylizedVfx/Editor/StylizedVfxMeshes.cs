using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kits.StylizedVfx.Editor
{
    /// Mallas de forma para las recetas, guardadas como asset: cristal facetado y media luna.
    /// Las de siempre (toro y cono) están en StylizedVfxAssets.
    public static class StylizedVfxMeshes
    {
        /// Cristal de dos puntas (bipirámide) de caras planas, con el centro en el origen y las puntas
        /// en ±Y: las aristas se marcan con el sombreado de dibujo.
        public static Mesh Crystal(string path, float radius, float height, int sides = 6)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var arriba = new Vector3(0f, height * 0.5f, 0f);
            var abajo = new Vector3(0f, -height * 0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                FlatTriangle(vertices, normals, triangles, p0, arriba, p1);
                FlatTriangle(vertices, normals, triangles, p1, abajo, p0);
            }
            return Save(path, vertices, normals, triangles);
        }

        /// Media luna en el plano XZ: un arco de «degrees» grados y radio «radius» que adelgaza hasta
        /// las puntas. El medio del arco está en el origen y abomba hacia +Z (las puntas quedan atrás),
        /// como un tajo que avanza.
        public static Mesh Arc(string path, float radius, float thickness, float degrees = 140f, int segments = 28, int sides = 8)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float media = degrees * 0.5f * Mathf.Deg2Rad;
            for (int s = 0; s <= segments; s++)
            {
                float k = s / (float)segments;
                float a = Mathf.Lerp(-media, media, k);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var centro = dir * radius - new Vector3(0f, 0f, radius);
                // Grueso en el medio, aplastado en vertical y en punta en los extremos.
                float grosor = thickness * Mathf.Max(0.04f, Mathf.Sin(k * Mathf.PI));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    var n = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices.Add(centro + dir * (Mathf.Cos(b) * grosor) + Vector3.up * (Mathf.Sin(b) * grosor * 0.45f));
                    normals.Add(n.normalized);
                }
            }
            for (int s = 0; s < segments; s++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a0 = s * (sides + 1) + j;
                    int b0 = (s + 1) * (sides + 1) + j;
                    triangles.Add(a0); triangles.Add(a0 + 1); triangles.Add(b0);
                    triangles.Add(b0); triangles.Add(a0 + 1); triangles.Add(b0 + 1);
                }
            }
            var mesh = Save(path, vertices, normals, triangles);
            FaceOutwards(mesh);
            return mesh;
        }

        private static void FlatTriangle(List<Vector3> vs, List<Vector3> ns, List<int> ts, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, (a + b + c) / 3f) < 0f) { n = -n; var x = b; b = c; c = x; }
            int i = vs.Count;
            vs.Add(a); vs.Add(b); vs.Add(c);
            ns.Add(n); ns.Add(n); ns.Add(n);
            ts.Add(i); ts.Add(i + 1); ts.Add(i + 2);
        }

        /// Da la vuelta a los triángulos cuyo orden no coincide con su normal.
        private static void FaceOutwards(Mesh mesh)
        {
            var tris = mesh.triangles;
            var vs = mesh.vertices;
            var ns = mesh.normals;
            for (int t = 0; t < tris.Length; t += 3)
            {
                var cara = Vector3.Cross(vs[tris[t + 1]] - vs[tris[t]], vs[tris[t + 2]] - vs[tris[t]]);
                var n = ns[tris[t]] + ns[tris[t + 1]] + ns[tris[t + 2]];
                if (Vector3.Dot(cara, n) < 0f) { int x = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = x; }
            }
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        private static Mesh Save(string path, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool nueva = mesh == null;
            if (nueva) mesh = new Mesh();
            mesh.Clear();
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (nueva)
            {
                StylizedVfxAssets.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
