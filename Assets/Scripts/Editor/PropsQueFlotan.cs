using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Busca en las escenas abiertas los props pequeños que se han quedado en el aire (el suelo se ha
/// repintado o rebajado por debajo de ellos, o lo que tenían debajo se ha movido) y apoya en el
/// suelo los que son de los que siempre van en el suelo: cajas, cestas, sacos, barriles. El resto
/// (faroles colgados, carteles, macetas en ventanas) solo se lista, para mirarlo a mano. Ver INC-1118.
public static class PropsQueFlotan
{
    private const float HuecoMinimo = 0.08f;
    // Más alto que esto no es un suelo que se ha movido: es algo que flota a propósito (en el agua).
    private const float HuecoMaximo = 1.5f;
    private const float TamanoMaximo = 2.2f;

    private static readonly Regex DeSuelo = new(
        "crate|box|caja|goods|fruit|fruta|basket|cesta|barrel|barril|sack|saco|bucket|cubo|pumpkin|calabaza",
        RegexOptions.IgnoreCase);

    [MenuItem("El Sendero/Escenario/Apoyar en el suelo los props que flotan")]
    private static void Apoyar()
    {
        var apoyados = new List<string>();
        var aRevisar = new List<string>();
        var vistos = new HashSet<GameObject>();
        Physics.SyncTransforms();

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var escena = SceneManager.GetSceneAt(s);
            if (!escena.isLoaded) continue;
            bool cambiada = false;
            foreach (var raiz in escena.GetRootGameObjects())
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(false))
            {
                var prop = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
                if (prop == null || !vistos.Add(prop)) continue;
                if (prop.GetComponentInParent<Animator>() != null) continue;
                if (!Caja(prop, out var limites)) continue;
                if (limites.size.x > TamanoMaximo || limites.size.y > TamanoMaximo || limites.size.z > TamanoMaximo) continue;

                float hueco = HuecoBajo(prop, limites);
                if (hueco < HuecoMinimo || hueco > HuecoMaximo) continue;

                string ruta = $"{escena.name}/{Ruta(prop.transform)} ({hueco:0.00} m)";
                if (DeSuelo.IsMatch(prop.name))
                {
                    Undo.RecordObject(prop.transform, "Apoyar prop en el suelo");
                    prop.transform.position -= Vector3.up * hueco;
                    apoyados.Add(ruta);
                    cambiada = true;
                }
                else aRevisar.Add(ruta);
            }
            if (cambiada)
            {
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
        }

        var sb = new StringBuilder();
        sb.Append($"Apoyados {apoyados.Count}: ").Append(string.Join("; ", apoyados)).Append(". ");
        sb.Append($"En el aire sin tocar (mirar a mano) {aRevisar.Count}: ").Append(string.Join("; ", aRevisar)).Append('.');
        Debug.Log("[PropsQueFlotan] " + sb);
        OrdenesDelEditor.Avisar("Props que flotan", sb.ToString());
    }

    private static bool Caja(GameObject prop, out Bounds limites)
    {
        limites = default;
        bool hay = false;
        foreach (var r in prop.GetComponentsInChildren<Renderer>(false))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!hay) { limites = r.bounds; hay = true; }
            else limites.Encapsulate(r.bounds);
        }
        return hay;
    }

    /// Distancia entre la base del prop y lo primero sólido que hay debajo (sin contarse a sí mismo).
    /// Mide en el centro y en las cuatro esquinas y se queda con la menor: si una esquina apoya en
    /// algo (una mesa, otra caja), el prop no flota.
    private static float HuecoBajo(GameObject prop, Bounds b)
    {
        float menor = float.MaxValue;
        Vector3 e = b.extents * 0.8f;
        Vector3[] puntos =
        {
            b.center,
            b.center + new Vector3(e.x, 0f, e.z), b.center + new Vector3(-e.x, 0f, e.z),
            b.center + new Vector3(e.x, 0f, -e.z), b.center + new Vector3(-e.x, 0f, -e.z),
        };
        foreach (var p in puntos)
        {
            var origen = new Vector3(p.x, b.min.y + 0.05f, p.z);
            var golpes = Physics.RaycastAll(origen, Vector3.down, HuecoMaximo + 1f, ~0, QueryTriggerInteraction.Ignore);
            float cerca = float.MaxValue;
            foreach (var h in golpes)
            {
                if (h.collider.transform.IsChildOf(prop.transform)) continue;
                cerca = Mathf.Min(cerca, h.distance - 0.05f);
            }
            if (cerca == float.MaxValue) return 0f;
            menor = Mathf.Min(menor, cerca);
        }
        return menor;
    }

    private static string Ruta(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
