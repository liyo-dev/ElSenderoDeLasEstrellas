using UnityEditor;
using UnityEngine;

namespace Kits.StylizedVfx.Editor
{
    /// Ayudas de editor para montar efectos con el shader de energía: materiales con sus tres
    /// colores y mallas sencillas (toro) guardadas como asset. El juego decide rutas y colores.
    public static class StylizedVfxAssets
    {
        public const string EnergyShaderName = "Kits/StylizedVfx/Energy";

        /// Colores y cortes de un material de energía. Los cortes de brillo y sombra van de -1 a 1
        /// (cuánto mira la superficie hacia la luz); el del borde, de 0 a 1 (1,01 = sin borde).
        public struct EnergyLook
        {
            public Color Core, Mid, Shadow, Rim;
            public float CoreBand, ShadowBand, RimBand, NoiseAmount, NoiseScale;

            public EnergyLook(Color core, Color mid, Color shadow, Color rim, float coreBand = 0.45f, float shadowBand = -0.15f,
                              float rimBand = 0.72f, float noiseAmount = 0.12f, float noiseScale = 3f)
            {
                Core = core; Mid = mid; Shadow = shadow; Rim = rim;
                CoreBand = coreBand; ShadowBand = shadowBand; RimBand = rimBand;
                NoiseAmount = noiseAmount; NoiseScale = noiseScale;
            }
        }

        /// Crea o actualiza el material en la ruta dada (conserva el GUID si ya existe).
        public static Material EnergyMaterial(string path, EnergyLook look)
        {
            var shader = Shader.Find(EnergyShaderName);
            if (shader == null)
            {
                Debug.LogError("[StylizedVfx] No se encuentra el shader " + EnergyShaderName);
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_CoreColor", look.Core);
            mat.SetColor("_MidColor", look.Mid);
            mat.SetColor("_ShadowColor", look.Shadow);
            mat.SetColor("_RimColor", look.Rim);
            mat.SetFloat("_CoreBand", look.CoreBand);
            mat.SetFloat("_ShadowBand", look.ShadowBand);
            mat.SetFloat("_RimBand", look.RimBand);
            mat.SetFloat("_NoiseAmount", look.NoiseAmount);
            mat.SetFloat("_NoiseScale", look.NoiseScale);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public const string ShellShaderName = "Kits/StylizedVfx/EnergyShell";

        /// Crea o actualiza un material de burbuja (escudos, columnas): transparente por dentro, borde opaco.
        public static Material ShellMaterial(string path, EnergyLook look, float innerAlpha = 0.12f, float midAlpha = 0.35f,
                                             float midBand = 0.45f)
        {
            var shader = Shader.Find(ShellShaderName);
            if (shader == null)
            {
                Debug.LogError("[StylizedVfx] No se encuentra el shader " + ShellShaderName);
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_CoreColor", look.Core);
            mat.SetColor("_MidColor", look.Mid);
            mat.SetColor("_RimColor", look.Rim);
            mat.SetFloat("_CoreBand", look.CoreBand);
            mat.SetFloat("_RimBand", look.RimBand);
            mat.SetFloat("_NoiseAmount", look.NoiseAmount);
            mat.SetFloat("_NoiseScale", look.NoiseScale);
            mat.SetFloat("_InnerAlpha", innerAlpha);
            mat.SetFloat("_MidAlpha", midAlpha);
            mat.SetFloat("_MidBand", midBand);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// Cono con la base en el origen y la punta hacia +Y, guardado como asset (pinchos, cristales).
        public static Mesh Cone(string path, float radius, float height, int sides = 7)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool nueva = mesh == null;
            if (nueva) mesh = new Mesh();
            mesh.Clear();
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);

            // Caras planas: cada cara con sus tres vértices, para que el dibujo marque las aristas.
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            var punta = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var n = Vector3.Cross(punta - p0, p1 - p0).normalized;
                if (Vector3.Dot(n, (p0 + p1) * 0.5f) < 0f) n = -n;
                int b0 = vertices.Count;
                vertices.Add(p0); vertices.Add(punta); vertices.Add(p1);
                normals.Add(n); normals.Add(n); normals.Add(n);
                triangles.Add(b0); triangles.Add(b0 + 1); triangles.Add(b0 + 2);
                // Base
                int c0 = vertices.Count;
                vertices.Add(Vector3.zero); vertices.Add(p1); vertices.Add(p0);
                normals.Add(Vector3.down); normals.Add(Vector3.down); normals.Add(Vector3.down);
                triangles.Add(c0); triangles.Add(c0 + 2); triangles.Add(c0 + 1);
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // Que las caras miren hacia fuera: si el orden quedó al revés, se da la vuelta.
            var tris = mesh.triangles;
            var vs = mesh.vertices;
            var ns = mesh.normals;
            for (int t = 0; t < tris.Length; t += 3)
            {
                var cara = Vector3.Cross(vs[tris[t + 1]] - vs[tris[t]], vs[tris[t + 2]] - vs[tris[t]]);
                if (Vector3.Dot(cara, ns[tris[t]]) < 0f) { int x = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = x; }
            }
            mesh.triangles = tris;

            if (nueva)
            {
                EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// Toro en el plano XZ (eje Y), de radio mayor y menor dados, guardado como asset.
        public static Mesh Torus(string path, float majorRadius, float minorRadius, int segments = 48, int sides = 12)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool nueva = mesh == null;
            if (nueva) mesh = new Mesh();
            mesh.Clear();
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);

            var vertices = new Vector3[(segments + 1) * (sides + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * sides * 6];
            int v = 0;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var centro = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k <= sides; k++)
                {
                    float b = k / (float)sides * Mathf.PI * 2f;
                    var n = centro * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices[v] = centro * majorRadius + n * minorRadius;
                    normals[v] = n;
                    v++;
                }
            }
            int t = 0;
            for (int s = 0; s < segments; s++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a0 = s * (sides + 1) + k;
                    int b0 = (s + 1) * (sides + 1) + k;
                    triangles[t++] = a0; triangles[t++] = a0 + 1; triangles[t++] = b0;
                    triangles[t++] = b0; triangles[t++] = a0 + 1; triangles[t++] = b0 + 1;
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            if (nueva)
            {
                EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int corte = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, corte));
            AssetDatabase.CreateFolder(path.Substring(0, corte), path.Substring(corte + 1));
        }
    }
}
