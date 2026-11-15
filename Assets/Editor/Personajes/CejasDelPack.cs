using System.Collections.Generic;
using UnityEngine;

/// Las cejas que traen modeladas los pelos y sombreros del pack: dónde están dentro de su malla.
/// Lo usa CarasDeLosNPCs para quitarlas, porque las cejas van en las caras nuevas.
internal static class CejasDelPack
{
    /// Caja que ocupan los ojos de referencia dentro del espacio de la malla del pelo.
    public static Bounds BoundsDeOjosEnEspacio(Renderer ojos, Transform espacioDeLasCejas)
    {
        if (ojos == null || espacioDeLasCejas == null) return default;
        var filtro = ojos.GetComponent<MeshFilter>();
        if (filtro == null || filtro.sharedMesh == null) return default;
        Bounds enOjos = filtro.sharedMesh.bounds;
        Vector3 centro = espacioDeLasCejas.InverseTransformPoint(
            ojos.transform.TransformPoint(enOjos.center));
        Vector3 tam = espacioDeLasCejas.InverseTransformVector(
            ojos.transform.TransformVector(enOjos.size));
        return new Bounds(centro, new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z)));
    }

    /// Localiza el par de cejas del pack dentro de la malla del pelo. Las cejas son dos islas sueltas
    /// que se distinguen por estar sobre los ojos, en dos lados simétricos de la cara.
    public static bool BuscarIslasDeCejas(Vector3[] vertices, int[] indices, Bounds ojos,
        out int[] izquierda, out int[] derecha, out Vector3 centroIzquierdo, out Vector3 centroDerecho)
    {
        izquierda = null;
        derecha = null;
        centroIzquierdo = Vector3.zero;
        centroDerecho = Vector3.zero;

        var representante = new int[vertices.Length];
        var porPosicion = new Dictionary<Vector3Int, int>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            var clave = new Vector3Int(Mathf.RoundToInt(v.x * 10000f),
                Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
            if (!porPosicion.TryGetValue(clave, out int r))
            {
                r = i;
                porPosicion[clave] = i;
            }
            representante[i] = r;
        }

        var padre = new int[vertices.Length];
        for (int i = 0; i < padre.Length; i++) padre[i] = i;

        int Raiz(int x)
        {
            while (padre[x] != x)
            {
                padre[x] = padre[padre[x]];
                x = padre[x];
            }
            return x;
        }
        void Unir(int a, int b)
        {
            a = Raiz(a);
            b = Raiz(b);
            if (a != b) padre[b] = a;
        }

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Unir(representante[indices[i]], representante[indices[i + 1]]);
            Unir(representante[indices[i]], representante[indices[i + 2]]);
        }

        var grupos = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
        {
            int raiz = Raiz(representante[i]);
            if (!grupos.TryGetValue(raiz, out var grupo))
            {
                grupo = new List<int>();
                grupos[raiz] = grupo;
            }
            grupo.Add(i);
        }

        bool hayOjos = ojos.size.sqrMagnitude > 0.0001f;
        var candidatos = new List<(List<int> vertices, Bounds caja)>();
        foreach (var grupo in grupos.Values)
        {
            if (grupo.Count < 8) continue;
            var caja = new Bounds(vertices[grupo[0]], Vector3.zero);
            for (int i = 1; i < grupo.Count; i++) caja.Encapsulate(vertices[grupo[i]]);
            if (caja.size.x > 0.30f || caja.size.y > 0.25f || caja.size.z > 0.25f) continue;
            if (Mathf.Abs(caja.center.x) < 0.005f) continue;
            if (hayOjos)
            {
                // Las cejas van por encima de los ojos y por delante de la cara.
                if (caja.center.y < ojos.max.y - 0.005f) continue;
                if (caja.center.z < ojos.center.z - 0.03f) continue;
                if (caja.size.x > ojos.size.x * 0.85f) continue;
            }
            candidatos.Add((grupo, caja));
        }

        float mejor = float.PositiveInfinity;
        (List<int> vertices, Bounds caja) mejorA = default;
        (List<int> vertices, Bounds caja) mejorB = default;
        for (int i = 0; i < candidatos.Count; i++)
        {
            for (int j = i + 1; j < candidatos.Count; j++)
            {
                Bounds a = candidatos[i].caja, b = candidatos[j].caja;
                if (Mathf.Sign(a.center.x) == Mathf.Sign(b.center.x)) continue;
                float dx = Mathf.Abs(Mathf.Abs(a.center.x) - Mathf.Abs(b.center.x));
                float dy = Mathf.Abs(a.center.y - b.center.y);
                float dz = Mathf.Abs(a.center.z - b.center.z);
                float dtam = Mathf.Abs(a.size.x - b.size.x) + Mathf.Abs(a.size.y - b.size.y) + Mathf.Abs(a.size.z - b.size.z);
                if (dx > 0.03f || dy > 0.02f || dz > 0.03f || dtam > 0.10f) continue;
                float nota = dx + dy + dz + dtam * 0.5f;
                if (nota >= mejor) continue;
                mejor = nota;
                mejorA = candidatos[i];
                mejorB = candidatos[j];
            }
        }

        if (mejorA.vertices == null || mejorB.vertices == null) return false;
        if (mejorA.caja.center.x < mejorB.caja.center.x)
        {
            izquierda = mejorA.vertices.ToArray();
            derecha = mejorB.vertices.ToArray();
            centroIzquierdo = mejorA.caja.center;
            centroDerecho = mejorB.caja.center;
        }
        else
        {
            izquierda = mejorB.vertices.ToArray();
            derecha = mejorA.vertices.ToArray();
            centroIzquierdo = mejorB.caja.center;
            centroDerecho = mejorA.caja.center;
        }
        return true;
    }
}
