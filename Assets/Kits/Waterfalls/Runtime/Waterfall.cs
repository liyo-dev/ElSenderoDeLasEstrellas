using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kits.Waterfalls
{
    /// Cortina de agua de una cascada montada a partir de un recorrido (de arriba abajo) y sus anchos,
    /// en espacio local. La malla es una cinta que sigue el recorrido un poco separada de él (lift);
    /// en cada vértice guarda en el color cuánto cae (R), lo cerca que está del labio (G) y del pie (B),
    /// y en la segunda UV el ancho en tanto por uno, para que el shader pinte franjas, espuma y borde.
    /// Los tramos llanos quedan invisibles: así puede seguir un río entero y salir solo donde cae.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Waterfall : MonoBehaviour
    {
        /// Un tramo que cae: del labio (arriba) al pie (abajo).
        [Serializable]
        public struct Run
        {
            public Vector3 Top;
            public Vector3 Foot;
            public float Width;
            /// Hacia dónde va el agua en horizontal (normalizado).
            public Vector3 Flow;
            public float Height => Top.y - Foot.y;
        }

        [Tooltip("Puntos del recorrido, de arriba abajo, en espacio local.")]
        [SerializeField] private Vector3[] path = new Vector3[0];

        [Tooltip("Ancho de la cortina en cada punto (metros). Si hay menos que puntos, se repite el último.")]
        [SerializeField] private float[] widths = { 4f };

        [Tooltip("Separación de la cortina respecto al recorrido, hacia fuera (metros).")]
        [SerializeField, Min(0f)] private float lift = 0.25f;

        [Tooltip("Pendiente (grados bajo la horizontal) desde la que empieza a verse la cortina.")]
        [SerializeField, Range(0f, 89f)] private float minSlope = 28f;

        [Tooltip("Pendiente desde la que la cortina se ve entera.")]
        [SerializeField, Range(1f, 90f)] private float fullSlope = 48f;

        [Tooltip("Metros de espuma en el labio, donde el agua empieza a caer.")]
        [SerializeField, Min(0.01f)] private float lipLength = 0.9f;

        [Tooltip("Metros de espuma al pie, donde el agua llega abajo.")]
        [SerializeField, Min(0.01f)] private float footLength = 1.6f;

        [Tooltip("Subdivisiones entre dos puntos del recorrido (cortina más suave en las curvas).")]
        [SerializeField, Range(1, 8)] private int subdivisions = 2;

        [SerializeField, HideInInspector] private List<Run> runs = new List<Run>();

        private Mesh _mesh;

        /// Los tramos que caen, del primero al último (espacio local).
        public IReadOnlyList<Run> Runs => runs;

        public float Lift => lift;

        /// Cambia el recorrido y rehace la cortina.
        public void SetPath(Vector3[] localPath, float[] localWidths)
        {
            path = localPath ?? new Vector3[0];
            widths = localWidths != null && localWidths.Length > 0 ? localWidths : new[] { 4f };
            Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnValidate()
        {
            if (fullSlope <= minSlope) fullSlope = minSlope + 1f;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }

        /// Rehace la malla de la cortina y la lista de tramos.
        public void Rebuild()
        {
            var filter = GetComponent<MeshFilter>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Waterfall curtain" };
                _mesh.MarkDynamic();
            }
            filter.sharedMesh = _mesh;
            _mesh.Clear();
            runs.Clear();
            if (path == null || path.Length < 2) return;

            // Recorrido subdividido (Catmull-Rom) con su ancho.
            var pts = new List<Vector3>();
            var ws = new List<float>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                for (int s = 0; s < subdivisions; s++)
                {
                    float t = s / (float)subdivisions;
                    pts.Add(CatmullRom(P(i - 1), P(i), P(i + 1), P(i + 2), t));
                    ws.Add(Mathf.Lerp(W(i), W(i + 1), t));
                }
            }
            pts.Add(path[path.Length - 1]);
            ws.Add(W(path.Length - 1));
            int n = pts.Count;

            // Pendiente, dirección y distancia recorrida en cada punto.
            var steep = new float[n];
            var flow = new Vector3[n];
            var along = new float[n];
            Vector3 lastFlow = Vector3.forward;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                Vector3 h = new Vector3(d.x, 0f, d.z);
                if (h.sqrMagnitude > 1e-4f) lastFlow = h.normalized;
                flow[i] = lastFlow;
                float slope = Mathf.Atan2(-d.y, Mathf.Max(h.magnitude, 1e-4f)) * Mathf.Rad2Deg;
                steep[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minSlope, fullSlope, slope));
                along[i] = i == 0 ? 0f : along[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            }

            // Tramos que caen y, para cada punto, distancia al labio y al pie de su tramo.
            var lip = new float[n];
            var foot = new float[n];
            int start = -1;
            for (int i = 0; i <= n; i++)
            {
                bool falling = i < n && steep[i] > 0.5f;
                if (falling && start < 0) start = i;
                if (falling || start < 0) continue;
                int end = i - 1;
                int top = Mathf.Max(start - 1, 0), bottom = Mathf.Min(end + 1, n - 1);
                for (int k = top; k <= bottom; k++)
                {
                    lip[k] = Mathf.Max(lip[k], 1f - Mathf.Clamp01((along[k] - along[top]) / lipLength));
                    foot[k] = Mathf.Max(foot[k], 1f - Mathf.Clamp01((along[bottom] - along[k]) / footLength));
                }
                float w = 0f;
                for (int k = start; k <= end; k++) w = Mathf.Max(w, ws[k]);
                runs.Add(new Run { Top = pts[top], Foot = pts[bottom], Width = w, Flow = flow[top] });
                start = -1;
            }

            // Cinta: dos vértices por punto, separada del recorrido hacia fuera.
            var verts = new Vector3[n * 2];
            var colors = new Color[n * 2];
            var uv0 = new Vector2[n * 2];
            var uv1 = new Vector2[n * 2];
            var tris = new List<int>((n - 1) * 6);
            for (int i = 0; i < n; i++)
            {
                Vector3 right = Vector3.Cross(Vector3.up, flow[i]).normalized;
                // Separación «hacia fuera»: en la caída, hacia donde va el agua (despega de la pared);
                // en llano, hacia arriba.
                Vector3 c = pts[i] + Vector3.Slerp(Vector3.up, flow[i], steep[i]).normalized * lift;
                float half = ws[i] * 0.5f;
                verts[i * 2] = c - right * half;
                verts[i * 2 + 1] = c + right * half;
                var col = new Color(steep[i], lip[i], foot[i], 1f);
                colors[i * 2] = col;
                colors[i * 2 + 1] = col;
                uv0[i * 2] = new Vector2(-half, along[i]);
                uv0[i * 2 + 1] = new Vector2(half, along[i]);
                uv1[i * 2] = new Vector2(0f, ws[i]);
                uv1[i * 2 + 1] = new Vector2(1f, ws[i]);
                if (i == n - 1) continue;
                int a = i * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
            _mesh.vertices = verts;
            _mesh.colors = colors;
            _mesh.uv = uv0;
            _mesh.uv2 = uv1;
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        private Vector3 P(int i) => path[Mathf.Clamp(i, 0, path.Length - 1)];

        private float W(int i) => widths[Mathf.Clamp(i, 0, widths.Length - 1)];

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
