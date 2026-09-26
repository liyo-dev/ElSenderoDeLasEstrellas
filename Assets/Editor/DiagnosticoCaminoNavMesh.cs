using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// Dice dónde se corta el NavMesh entre dos puntos y qué hay allí.
///
/// Con dos objetos seleccionados en la escena, mide el camino entre ellos. Sin selección, entre
/// la puerta de la casa de Will (House_FrontDoor) y la parada de Eldran en el punto de guardado
/// (Eldran_PuntoGuardado), que es el camino que hace Eldran al guiar a Will. Ver INC-463.
///
/// El informe sale en la consola y deja seleccionados los obstáculos que tallan el hueco, para
/// verlos en la escena.
public static class DiagnosticoCaminoNavMesh
{
    private const string AnchorOrigen = "House_FrontDoor";
    private const string AnchorDestino = "Eldran_PuntoGuardado";

    [MenuItem("El Sendero/Navegación/Diagnóstico: ¿dónde se corta el camino?")]
    public static void Diagnosticar()
    {
        if (!Resolver(out Vector3 desde, out Vector3 hasta, out string nombres)) return;

        var informe = new StringBuilder();
        informe.AppendLine($"[DiagnosticoCaminoNavMesh] Camino {nombres}");

        if (!NavMesh.SamplePosition(desde, out var hDesde, 5f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(hasta, out var hHasta, 5f, NavMesh.AllAreas))
        {
            informe.AppendLine("  Uno de los dos extremos no tiene NavMesh a menos de 5 m.");
            Debug.LogWarning(informe.ToString());
            return;
        }

        var path = new NavMeshPath();
        NavMesh.CalculatePath(hDesde.position, hHasta.position, NavMesh.AllAreas, path);
        float largo = 0f;
        for (int i = 1; i < path.corners.Length; i++) largo += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        float recto = DistanciaPlana(hDesde.position, hHasta.position);
        informe.AppendLine($"  Resultado: {path.status}, {path.corners.Length} esquinas, {largo:F0} m de camino para {recto:F0} m en línea recta.");

        bool rodeo = path.status == NavMeshPathStatus.PathComplete && largo > recto * 1.5f;
        if (path.status == NavMeshPathStatus.PathComplete && !rodeo)
        {
            informe.AppendLine("  El camino está entero y va bastante directo: el NavMesh no es el problema.");
            Debug.Log(informe.ToString());
            return;
        }

        Vector3 corte;
        if (rodeo)
        {
            informe.AppendLine("  Es un RODEO: el paso directo está cortado. Se mira la línea recta entre los dos puntos.");
            informe.AppendLine("  (Para mirar un paso concreto, como una puerta, selecciona dos objetos a un lado y otro y vuelve a pulsar.)");
            corte = hDesde.position;
        }
        else
        {
            corte = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : hDesde.position;
            informe.AppendLine($"  El camino se corta en {corte}.");
        }

        // Recorrer en línea recta desde el corte hacia el destino: dónde falta NavMesh.
        Vector3 dir = hHasta.position - corte; dir.y = 0f;
        float tramo = Mathf.Min(dir.magnitude, 60f);
        dir.Normalize();
        var huecos = new List<Vector3>();
        for (float d = 0.5f; d <= tramo; d += 0.5f)
        {
            Vector3 p = corte + dir * d;
            if (!NavMesh.SamplePosition(p, out _, 0.6f, NavMesh.AllAreas)) huecos.Add(p);
            else if (huecos.Count > 0) break;
        }
        if (huecos.Count > 0)
            informe.AppendLine($"  Sin NavMesh en línea recta hacia el destino: de {huecos[0]} a {huecos[huecos.Count - 1]} ({huecos.Count * 0.5f:F1} m).");

        Vector3 centro = huecos.Count > 0 ? huecos[huecos.Count / 2] : corte;

        // Obstáculos cuyo hueco tallado (con el radio del agente) cubre algún punto sin NavMesh.
        // Si no hay puntos sin NavMesh en la recta, los que tallan cerca del corte.
        var todos = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None)
            .Where(o => o.isActiveAndEnabled && o.carving).ToList();
        var talladores = huecos.Count > 0
            ? todos.Where(o => huecos.Any(h => Cubre(o, h, 0.6f))).ToList()
            : todos.Where(o => DistanciaPlana(o.transform.position, centro) < 12f).ToList();
        talladores = talladores.OrderBy(o => DistanciaPlana(o.transform.position, centro)).ToList();

        if (talladores.Count > 0)
        {
            informe.AppendLine(huecos.Count > 0
                ? $"  Obstáculos que tapan el hueco ({talladores.Count}):"
                : $"  Obstáculos que tallan el NavMesh a menos de 12 m ({talladores.Count}):");
            foreach (var o in talladores)
            {
                Vector3 escala = o.transform.lossyScale;
                Vector3 tam = o.shape == NavMeshObstacleShape.Box
                    ? Vector3.Scale(o.size, escala)
                    : new Vector3(o.radius * 2f * Mathf.Max(escala.x, escala.z), o.height * escala.y, o.radius * 2f * Mathf.Max(escala.x, escala.z));
                var col = o.GetComponent<Collider>();
                informe.AppendLine($"   - {Ruta(o.transform)}  forma={o.shape} tamaño≈{tam.x:F1}×{tam.y:F1}×{tam.z:F1} m  collider={(col != null ? col.GetType().Name : "ninguno")}  a {DistanciaPlana(o.transform.position, centro):F1} m");
            }
            Selection.objects = talladores.Select(o => (Object)o.gameObject).ToArray();
            informe.AppendLine("  Quedan seleccionados en la jerarquía.");
        }
        else
        {
            informe.AppendLine("  No hay obstáculos que tallen cerca: el hueco es del bakeado (el suelo de ahí no está en el layer Floor, o no se ha vuelto a bakear).");
        }

        // Qué hay debajo a lo largo del hueco, de arriba abajo: si falta el suelo en el layer Floor,
        // el hueco es del bakeado.
        var muestras = huecos.Count > 0
            ? new[] { huecos[0], huecos[huecos.Count / 2], huecos[huecos.Count - 1] }
            : new[] { centro };
        foreach (var m in muestras)
        {
            var hits = Physics.RaycastAll(m + Vector3.up * 10f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance).ToArray();
            string lista = hits.Length == 0 ? "nada"
                : string.Join(" · ", hits.Take(6).Select(h =>
                    $"'{h.collider.name}' ({LayerMask.LayerToName(h.collider.gameObject.layer)}, y={h.point.y:F2})"));
            informe.AppendLine($"  Debajo de {m}: {lista}");
        }

        Debug.LogWarning(informe.ToString());
    }

    /// Si el hueco que talla el obstáculo (más un margen, el radio del agente) cubre el punto.
    private static bool Cubre(NavMeshObstacle o, Vector3 punto, float margen)
    {
        var t = o.transform;
        if (o.shape == NavMeshObstacleShape.Box)
        {
            Vector3 local = t.InverseTransformPoint(punto) - o.center;
            Vector3 esc = t.lossyScale;
            Vector3 medio = o.size * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                if (k == 1) continue; // la altura no cuenta para el tallado
                float m = margen / Mathf.Max(0.0001f, Mathf.Abs(esc[k]));
                if (Mathf.Abs(local[k]) > medio[k] + m) return false;
            }
            return true;
        }
        Vector3 c = t.TransformPoint(o.center);
        float r = o.radius * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z)) + margen;
        return DistanciaPlana(c, punto) <= r;
    }

    private static bool Resolver(out Vector3 desde, out Vector3 hasta, out string nombres)
    {
        desde = hasta = default;
        nombres = "";
        var sel = Selection.transforms;
        if (sel != null && sel.Length == 2)
        {
            desde = sel[0].position; hasta = sel[1].position;
            nombres = $"'{sel[0].name}' → '{sel[1].name}'";
            return true;
        }

        var a = BuscarAnchor(AnchorOrigen);
        var b = BuscarAnchor(AnchorDestino);
        if (a == null || b == null)
        {
            EditorUtility.DisplayDialog("Diagnóstico del camino",
                $"No encuentro los anchors '{AnchorOrigen}' y '{AnchorDestino}' en las escenas abiertas.\n\n" +
                "Abre MainWorld, o selecciona dos objetos de la escena y vuelve a pulsar el menú.", "Vale");
            return false;
        }
        desde = a.transform.position; hasta = b.transform.position;
        nombres = $"'{AnchorOrigen}' → '{AnchorDestino}'";
        return true;
    }

    private static SpawnAnchor BuscarAnchor(string id) =>
        Object.FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(s => s.anchorId == id);

    private static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static string Ruta(Transform t)
    {
        var partes = new List<string>();
        for (var x = t; x != null && partes.Count < 5; x = x.parent) partes.Add(x.name);
        partes.Reverse();
        return string.Join("/", partes);
    }
}
