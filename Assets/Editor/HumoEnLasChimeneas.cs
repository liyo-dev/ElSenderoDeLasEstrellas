using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Que el humo salga de las chimeneas (INC-1164). Al cambiar las casas del pack por las nuevas
/// (casas de campo, casas de la aldea…), el humo que estaba suelto en la escena se quedó flotando donde
/// estaban las chimeneas viejas, y las casas nuevas, que llevan la chimenea dentro de su malla, no
/// echaban humo.
///
/// · Casas nuevas: las que llevan el hijo «Colisión (suelo)» de <c>ModelosDeLasZonas</c>. Su chimenea es lo
///   más alto de la casa si es una pieza pequeña (un caballete largo no cuenta); se busca con rayos contra su
///   colisión, que es su propia malla.
/// · Cada humo que no está sobre una chimenea del pack a la vista se lleva a la chimenea nueva más cercana
///   (hasta 7 m); si no hay ninguna, se apaga.
/// · Cada chimenea nueva que se quede sin humo recibe uno (<see cref="HumoDeChimenea"/>).
/// Se puede pasar las veces que haga falta.
/// </summary>
public static class HumoEnLasChimeneas
{
    private const string NombreDeLaColision = "Colisión (suelo)";
    private const string MaterialDeHumo = "Assets/_VFX/Hechizos/Materiales/M_Hechizo_Humo_Alfa.mat";
    private const float Paso = 0.2f;              // separación de los rayos (m)
    private const float BandaDeLoAlto = 0.05f;    // lo que cuenta como «lo más alto» (m)
    private const float AnchoMaximo = 1.8f;       // una chimenea no pasa de esto; un caballete, sí (m)
    private const float AlcanceParaLlevar = 7f;   // hasta dónde se busca chimenea nueva para un humo suelto (m)

    [MenuItem("El Sendero/Mundo/Humo: ponerlo en las chimeneas (INC-1164)")]
    public static void Menu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Humo", "Sal de Play antes de ejecutarlo.");
            return;
        }
        var informe = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (escena.isLoaded && Arreglar(escena, informe)) EditorSceneManager.MarkSceneDirty(escena);
        }
        Debug.Log("[Humo] " + informe);
        OrdenesDelEditor.Avisar("Humo", informe.ToString());
    }

    private sealed class Chimenea
    {
        public Transform Casa;
        public Vector3 Boca;
        public bool ConHumo;
    }

    private static bool Arreglar(Scene escena, StringBuilder informe)
    {
        var chimeneas = new List<Chimenea>();
        var chimeneasDelPack = new List<Renderer>();
        var humos = new List<GameObject>();
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var col in raiz.GetComponentsInChildren<Collider>(false))
                if (col.name == NombreDeLaColision && col.transform.parent != null && TryBoca(col, out var boca))
                    chimeneas.Add(new Chimenea { Casa = col.transform.parent, Boca = boca });
            foreach (var r in raiz.GetComponentsInChildren<MeshFilter>(false))
                if (r.sharedMesh != null && r.sharedMesh.name.StartsWith("Chimney") && r.TryGetComponent<Renderer>(out var rr) && rr.enabled)
                    chimeneasDelPack.Add(rr);
            foreach (var t in raiz.GetComponentsInChildren<Transform>(false))
                if (EsHumo(t)) humos.Add(t.gameObject);
        }
        if (humos.Count == 0 && chimeneas.Count == 0) return false;

        int bien = 0, llevados = 0, apagados = 0, nuevos = 0;
        foreach (var humo in humos)
        {
            Vector3 p = humo.transform.position;
            if (SobreChimeneaDelPack(p, chimeneasDelPack)) { bien++; continue; }

            Chimenea mejor = null;
            float d2 = AlcanceParaLlevar * AlcanceParaLlevar;
            foreach (var c in chimeneas)
            {
                if (c.ConHumo) continue;
                float dx = c.Boca.x - p.x, dz = c.Boca.z - p.z;
                if (dx * dx + dz * dz < d2) { d2 = dx * dx + dz * dz; mejor = c; }
            }
            Undo.RecordObject(humo.transform, "Humo");
            if (mejor != null)
            {
                if (Vector3.Distance(humo.transform.position, mejor.Boca) > 0.05f) llevados++; else bien++;
                humo.transform.position = mejor.Boca;
                mejor.ConHumo = true;
            }
            else
            {
                Undo.RecordObject(humo, "Humo");
                humo.SetActive(false);
                apagados++;
                informe.AppendLine($"  apagado (sin chimenea cerca): {Ruta(humo.transform)}");
            }
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDeHumo);
        foreach (var c in chimeneas)
        {
            if (c.ConHumo) continue;
            var go = new GameObject("Humo");
            Undo.RegisterCreatedObjectUndo(go, "Humo");
            go.transform.SetParent(c.Casa, false);
            go.transform.SetPositionAndRotation(c.Boca, Quaternion.identity);
            var humo = go.AddComponent<HumoDeChimenea>();
            humo.material = material;
            humo.distanciaDeActivacion = 120f;
            c.ConHumo = true;
            nuevos++;
        }
        informe.AppendLine($"{escena.name}: {chimeneas.Count} chimeneas de casas nuevas; humo bien puesto {bien}, llevado a su chimenea {llevados}, " +
                           $"apagado {apagados}, nuevo {nuevos}.");
        return llevados + apagados + nuevos > 0;
    }

    /// Humo de chimenea: el del pack pasado a dibujo (Smoke0x_P, FX_Smoke_*) o el de NocheDelMundoWiring.
    private static bool EsHumo(Transform t)
    {
        if (t.GetComponent<HumoDeChimenea>() != null) return true;
        string n = t.name;
        if (!(n.StartsWith("Smoke0") || n.StartsWith("FX_Smoke"))) return false;
        // Solo la raíz de cada humo (no sus piezas) y nada de efectos de hechizos o atrezo de cinemáticas.
        for (var p = t.parent; p != null; p = p.parent)
            if (p.name.StartsWith("Smoke0") || p.name.StartsWith("FX_Smoke") || p.name.StartsWith("PROP_") || p.name.Contains("Hechizo")) return false;
        return true;
    }

    private static bool SobreChimeneaDelPack(Vector3 p, List<Renderer> chimeneas)
    {
        foreach (var r in chimeneas)
        {
            var b = r.bounds;
            if (Mathf.Abs(b.center.x - p.x) <= b.extents.x + 0.3f && Mathf.Abs(b.center.z - p.z) <= b.extents.z + 0.3f &&
                Mathf.Abs(b.max.y - p.y) < 1f) return true;
        }
        return false;
    }

    /// Lo más alto de la casa, si es una pieza pequeña: la boca de la chimenea (centro de su remate, arriba).
    private static bool TryBoca(Collider col, out Vector3 boca)
    {
        boca = default;
        var b = col.bounds;
        var altos = new List<Vector3>();
        float maximo = float.MinValue;
        for (float x = b.min.x; x <= b.max.x; x += Paso)
        for (float z = b.min.z; z <= b.max.z; z += Paso)
        {
            var rayo = new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down);
            if (!col.Raycast(rayo, out var golpe, b.size.y + 2f)) continue;
            altos.Add(golpe.point);
            if (golpe.point.y > maximo) maximo = golpe.point.y;
        }
        if (altos.Count == 0) return false;
        Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue), max = new Vector3(float.MinValue, 0f, float.MinValue), suma = Vector3.zero;
        int n = 0;
        foreach (var a in altos)
        {
            if (a.y < maximo - BandaDeLoAlto) continue;
            min = Vector3.Min(min, a); max = Vector3.Max(max, a); suma += a; n++;
        }
        if (n == 0 || max.x - min.x > AnchoMaximo || max.z - min.z > AnchoMaximo) return false;
        boca = new Vector3(suma.x / n, maximo + 0.05f, suma.z / n);
        return true;
    }

    private static string Ruta(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
