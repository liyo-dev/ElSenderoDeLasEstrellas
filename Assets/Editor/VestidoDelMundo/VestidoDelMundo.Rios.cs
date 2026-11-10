using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// Agua para nadar de los ríos y arroyos de MainWorld. Su lámina es una malla hecha a mano sin
/// colliders, así que el jugador andaba por el fondo. Se mide la malla en una rejilla en planta
/// (la altura del agua en cada celda) y se ponen:
///
/// · Cajas trigger en la capa Water (lo que busca PlayerSwimmingController) con la cara de arriba
///   en la lámina, agrupadas por escalones de altura porque el río baja. Con la habilidad de nadar
///   se nada; sin ella, el agua honda hace de orilla.
/// · Volúmenes «Not Walkable» en la capa Floor donde el agua cubre FondoNoCaminable o más, para que
///   los NPCs no crucen por el agua (hace falta volver a hornear la navegación).
public static partial class VestidoDelMundo
{
    private static partial class Aguas
    {
        /// Objetos de la escena cuya malla es la lámina de un río.
        private static readonly string[] RiosDeLaEscena = { "Río del bosque", "Arroyo de la ladera este" };

        private const float CeldaDeRio = 2f;
        private const float EscalonDeRio = 0.25f;

        public static void PonerRios(Obra o)
        {
            foreach (string nombre in RiosDeLaEscena)
            {
                var lamina = BuscarEnLaEscena(o, nombre);
                var malla = lamina != null ? lamina.GetComponent<MeshFilter>() : null;
                if (malla == null || malla.sharedMesh == null)
                {
                    o.Informe.Add($"Río «{nombre}»: no está en la escena o no tiene malla; sin agua para nadar.");
                    continue;
                }
                Transform grupo = Grupo(o.Raiz, "Agua — " + nombre);
                int cajas = PonerAguaDeRio(o, grupo, malla, out int volumenes, out float area);
                o.Informe.Add($"Río «{nombre}»: {area:0} m² de agua, {cajas} cajas para nadar en la capa Water y {volumenes} volúmenes «Not Walkable» donde hay {FondoNoCaminable:0.0} m o más de fondo.");
            }
        }

        private static Transform BuscarEnLaEscena(Obra o, string nombre)
        {
            foreach (GameObject raiz in o.Raiz.gameObject.scene.GetRootGameObjects())
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    if (t.name == nombre && t.GetComponent<MeshFilter>() != null) return t;
            return null;
        }

        private static int PonerAguaDeRio(Obra o, Transform grupo, MeshFilter malla, out int volumenes, out float area)
        {
            Mesh mesh = malla.sharedMesh;
            Matrix4x4 aMundo = malla.transform.localToWorldMatrix;
            Vector3[] v = mesh.vertices;
            for (int j = 0; j < v.Length; j++) v[j] = aMundo.MultiplyPoint3x4(v[j]);
            Bounds b = malla.GetComponent<Renderer>().bounds;
            float x0 = b.min.x, z0 = b.min.z;
            int ni = Mathf.CeilToInt(b.size.x / CeldaDeRio) + 1, nk = Mathf.CeilToInt(b.size.z / CeldaDeRio) + 1;

            // Altura de la lámina en el centro de cada celda (la más alta si se pisan triángulos).
            var lamina = new float[nk, ni];
            for (int k = 0; k < nk; k++) for (int i = 0; i < ni; i++) lamina[k, i] = float.NegativeInfinity;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] t = mesh.GetTriangles(sub);
                for (int j = 0; j + 2 < t.Length; j += 3)
                    Rasterizar(v[t[j]], v[t[j + 1]], v[t[j + 2]], x0, z0, lamina);
            }

            // Escalón de cada celda con agua y fondo bajo ella.
            var escalon = new int[nk, ni];
            var fondo = new float[nk, ni];
            var escalones = new SortedSet<int>();
            var hondo = new bool[nk, ni];
            int celdas = 0;
            for (int k = 0; k < nk; k++)
                for (int i = 0; i < ni; i++)
                {
                    if (float.IsNegativeInfinity(lamina[k, i])) continue;
                    celdas++;
                    escalon[k, i] = Mathf.RoundToInt(lamina[k, i] / EscalonDeRio);
                    escalones.Add(escalon[k, i]);
                    Vector3 c = new Vector3(x0 + (i + 0.5f) * CeldaDeRio, 0f, z0 + (k + 0.5f) * CeldaDeRio);
                    fondo[k, i] = o.Terreno != null ? o.Terreno.SampleHeight(c) + o.Terreno.transform.position.y : lamina[k, i] - 2f;
                    hondo[k, i] = lamina[k, i] - fondo[k, i] >= FondoNoCaminable;
                }
            area = celdas * CeldaDeRio * CeldaDeRio;

            var agua = new GameObject("Agua para nadar");
            int capaAgua = LayerMask.NameToLayer("Water");
            if (capaAgua >= 0) agua.layer = capaAgua;
            agua.transform.SetParent(grupo, false);
            int cajas = 0;
            foreach (int e in escalones)
            {
                var mascara = new bool[nk, ni];
                for (int k = 0; k < nk; k++)
                    for (int i = 0; i < ni; i++)
                        mascara[k, i] = !float.IsNegativeInfinity(lamina[k, i]) && escalon[k, i] == e;
                float cota = e * EscalonDeRio;
                foreach (RectInt q in Rectangulos(mascara))
                {
                    float suelo = MinimoDelFondo(fondo, q) - 1f;
                    float alto = Mathf.Max(0.5f, cota - suelo);
                    var caja = agua.AddComponent<BoxCollider>();
                    caja.isTrigger = true;
                    caja.center = new Vector3(x0 + (q.x + q.width * 0.5f) * CeldaDeRio, suelo + alto * 0.5f, z0 + (q.y + q.height * 0.5f) * CeldaDeRio);
                    caja.size = new Vector3(q.width * CeldaDeRio, alto, q.height * CeldaDeRio);
                    cajas++;
                }
            }
            o.SinObstaculo.Add(agua.transform);

            var padre = new GameObject("Fondo no caminable");
            padre.transform.SetParent(grupo, false);
            int capaSuelo = LayerMask.NameToLayer("Floor");
            volumenes = 0;
            foreach (RectInt q in Rectangulos(hondo))
            {
                float suelo = MinimoDelFondo(fondo, q) - 1f;
                float techo = MaximoDeLaLamina(lamina, q) + 0.5f;
                var go = new GameObject($"No caminable {++volumenes}");
                if (capaSuelo >= 0) go.layer = capaSuelo;
                go.transform.SetParent(padre.transform, false);
                go.transform.position = new Vector3(x0 + (q.x + q.width * 0.5f) * CeldaDeRio, (suelo + techo) * 0.5f, z0 + (q.y + q.height * 0.5f) * CeldaDeRio);
                var vol = go.AddComponent<NavMeshModifierVolume>();
                vol.center = Vector3.zero;
                vol.size = new Vector3(q.width * CeldaDeRio, techo - suelo, q.height * CeldaDeRio);
                vol.area = 1;
            }
            return cajas;
        }

        // Marca en 'lamina' la altura del triángulo en los centros de celda que caen dentro de él.
        private static void Rasterizar(Vector3 a, Vector3 b, Vector3 c, float x0, float z0, float[,] lamina)
        {
            int nk = lamina.GetLength(0), ni = lamina.GetLength(1);
            int iMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - x0) / CeldaDeRio));
            int iMax = Mathf.Min(ni - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - x0) / CeldaDeRio));
            int kMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - z0) / CeldaDeRio));
            int kMax = Mathf.Min(nk - 1, Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - z0) / CeldaDeRio));
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-6f) return;
            for (int k = kMin; k <= kMax; k++)
                for (int i = iMin; i <= iMax; i++)
                {
                    float px = x0 + (i + 0.5f) * CeldaDeRio, pz = z0 + (k + 0.5f) * CeldaDeRio;
                    float wa = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / d;
                    float wb = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / d;
                    float wc = 1f - wa - wb;
                    if (wa < 0f || wb < 0f || wc < 0f) continue;
                    float y = wa * a.y + wb * b.y + wc * c.y;
                    if (y > lamina[k, i]) lamina[k, i] = y;
                }
        }

        private static float MinimoDelFondo(float[,] fondo, RectInt q)
        {
            float m = float.PositiveInfinity;
            for (int k = q.y; k < q.y + q.height; k++)
                for (int i = q.x; i < q.x + q.width; i++) m = Mathf.Min(m, fondo[k, i]);
            return m;
        }

        private static float MaximoDeLaLamina(float[,] lamina, RectInt q)
        {
            float m = float.NegativeInfinity;
            for (int k = q.y; k < q.y + q.height; k++)
                for (int i = q.x; i < q.x + q.width; i++) m = Mathf.Max(m, lamina[k, i]);
            return m;
        }
    }
}
