#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// Diagnóstico temporal del NavMesh del seguimiento del Cap. 3 (puente y ruta).
public static class TmpNavSigilo
{
    [MenuItem("Tools/Tmp/NavSigilo")]
    private static void Run()
    {
        var sb = new StringBuilder("NavMesh del seguimiento (Cap. 3)\n");
        var puntos = new (string nombre, float x, float z)[]
        {
            ("salida", -110f, -222.3f),
            ("salida+3", -113f, -224.5f),
            ("medio1", -118.8f, -228f),
            ("medio2", -124f, -231.5f),
            ("puente", -129f, -235f),
            ("ruta1", -150f, -222f),
            ("caserio", -171f, -200f),
            ("ruta2", -186f, -186f),
            ("linde", -193f, -168f),
            ("ruta3", -203f, -150f),
            ("fin", -217f, -143f),
        };

        foreach (var p in puntos)
        {
            var golpes = Physics.RaycastAll(new Vector3(p.x, 200f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
            float alto = float.NegativeInfinity;
            string quien = "nada";
            foreach (var g in golpes)
                if (g.point.y > alto) { alto = g.point.y; quien = g.collider.name; }

            bool hay = NavMesh.SamplePosition(new Vector3(p.x, alto, p.z), out var hit, 2f, NavMesh.AllAreas);
            float dist = hay ? Vector3.Distance(new Vector3(p.x, alto, p.z), hit.position) : -1f;
            sb.AppendLine($"{p.nombre} ({p.x}, {p.z}): suelo {alto:F2} ({quien}) · NavMesh " +
                          (hay ? $"({hit.position.x:F2}, {hit.position.y:F2}, {hit.position.z:F2}), d={dist:F2}" : "NO"));
        }

        Camino(sb, "salida -> puente", new Vector3(-110f, 19.74f, -222.3f), new Vector3(-129f, 18.6f, -235f));
        Camino(sb, "puente -> ruta1", new Vector3(-129f, 18.6f, -235f), new Vector3(-150f, 17.9f, -222f));
        Camino(sb, "salida -> ruta1", new Vector3(-110f, 19.74f, -222.3f), new Vector3(-150f, 17.9f, -222f));

        OrdenesDelEditor.Avisar("NavSigilo", sb.ToString());
    }

    private static void Camino(StringBuilder sb, string nombre, Vector3 a, Vector3 b)
    {
        var path = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path);
        sb.Append($"{nombre}: ok={ok} estado={path.status} esquinas={path.corners.Length}");
        for (int i = 0; i < path.corners.Length; i++) sb.Append($" [{path.corners[i].x:F1},{path.corners[i].y:F1},{path.corners[i].z:F1}]");
        sb.AppendLine();
    }
}
#endif
