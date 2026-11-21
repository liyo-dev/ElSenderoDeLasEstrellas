using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pone a los actores que ya están en una escena el prefab que pide su guion. El REPARTO de un
/// guion puede llevar «prefab=Assets/…prefab»; si en la escena del guion hay un actor con ese id
/// (su persistenceId) hecho de otro prefab, en juego sale el de la escena y no el del guion. Este
/// menú recorre todos los guiones, cambia el prefab de esos actores (conservando sus cambios en la
/// escena) y guarda las escenas. Ver INC-1338.
/// </summary>
public static class RepartoDeEscenaSegunElGuion
{
    private const string CarpetaDeGuiones = "Assets/_SEQUENCES/Guiones";

    [MenuItem("El Sendero/Cinemáticas/Reparto: actores de escena con el prefab del guion")]
    public static void Aplicar()
    {
        var informe = new StringBuilder();
        int cambiados = 0;
        foreach (var grupo in LeerRepartos().GroupBy(r => r.escena))
        {
            string rutaEscena = RutaDeEscena(grupo.Key);
            if (rutaEscena == null) { informe.AppendLine($"No encuentro la escena «{grupo.Key}»."); continue; }

            var escena = SceneManager.GetSceneByPath(rutaEscena);
            bool abiertaAqui = !escena.isLoaded;
            if (abiertaAqui) escena = EditorSceneManager.OpenScene(rutaEscena, OpenSceneMode.Additive);

            int antes = cambiados;
            foreach (var r in grupo)
                cambiados += Cambiar(escena, r, informe);

            if (cambiados > antes) EditorSceneManager.SaveScene(escena);
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
        informe.Insert(0, $"Reparto de escena: {cambiados} actor(es) cambiados al prefab de su guion.\n");
        Debug.Log("[Reparto] " + informe);
    }

    private struct Entrada
    {
        public string guion, escena, id, prefab;
    }

    /// Las entradas del REPARTO con «prefab=» de todos los guiones (sin el jugador).
    private static List<Entrada> LeerRepartos()
    {
        var lista = new List<Entrada>();
        foreach (var ruta in Directory.GetFiles(CarpetaDeGuiones, "*.guion.txt"))
        {
            string escena = null;
            bool enReparto = false;
            foreach (var bruta in File.ReadAllLines(ruta))
            {
                string l = bruta.Trim();
                if (l.StartsWith("ESCENA ")) escena = l.Substring(7).Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l == "REPARTO") { enReparto = true; continue; }
                if (enReparto && (l == l.ToUpperInvariant() && !l.Contains(' '))) { enReparto = false; continue; }
                if (!enReparto) continue;
                var partes = l.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length < 2 || partes[1] == "Player") continue;
                string prefab = partes.Skip(2).FirstOrDefault(p => p.StartsWith("prefab="))?.Substring(7);
                if (string.IsNullOrEmpty(prefab) || escena == null) continue;
                lista.Add(new Entrada { guion = Path.GetFileName(ruta), escena = escena, id = partes[1], prefab = prefab });
            }
        }
        return lista;
    }

    private static string RutaDeEscena(string nombre)
        => AssetDatabase.FindAssets("t:Scene " + nombre).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == nombre);

    /// Cambia el prefab del actor con ese id si es de otro; 1 si lo ha cambiado.
    private static int Cambiar(Scene escena, Entrada r, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab);
        if (prefab == null) { informe.AppendLine($"{r.guion}: no existe {r.prefab}."); return 0; }

        var raiz = BuscarActor(escena, r.id, out System.Type tipoDelId);
        if (raiz == null) return RenombrarSiFalta(escena, r, prefab, informe);
        var origen = PrefabUtility.GetCorrespondingObjectFromSource(raiz);
        if (origen != null && AssetDatabase.GetAssetPath(origen) == r.prefab)
        {
            if (raiz.name != prefab.name || raiz.name == r.id) return 0;
            raiz.name = r.id;
            EditorSceneManager.MarkSceneDirty(escena);
            return 1;
        }

        string antes = origen != null ? AssetDatabase.GetAssetPath(origen) : "(sin prefab)";
        if (origen == null)
        {
            informe.AppendLine($"{r.guion}: «{r.id}» en {escena.name} no es un prefab; se deja como está.");
            return 0;
        }
        var ajustes = new PrefabReplacingSettings
        {
            logInfo = false,
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
        };
        string nombre = raiz.name;
        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(raiz, prefab, ajustes, InteractionMode.AutomatedAction);
        raiz.name = nombre;
        PonerId(raiz, r.id, tipoDelId);
        EditorSceneManager.MarkSceneDirty(escena);
        informe.AppendLine($"{escena.name}: «{r.id}» {antes} → {r.prefab} ({r.guion}).");
        return 1;
    }

    /// Un actor sin id de persistencia que ya es del prefab del guion pero ha perdido su nombre (el
    /// que le identifica en la cinemática): si es el único de ese prefab en la escena, recupera el id.
    private static int RenombrarSiFalta(Scene escena, Entrada r, GameObject prefab, StringBuilder informe)
    {
        var candidatos = new List<GameObject>();
        foreach (var root in escena.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                var origen = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (origen != null && AssetDatabase.GetAssetPath(origen) == r.prefab && go.name == prefab.name) candidatos.Add(go);
            }
        if (candidatos.Count != 1) return 0;
        candidatos[0].name = r.id;
        EditorSceneManager.MarkSceneDirty(escena);
        informe.AppendLine($"{escena.name}: «{prefab.name}» vuelve a llamarse «{r.id}» ({r.guion}).");
        return 1;
    }

    /// La raíz del prefab del actor de la escena cuyo persistenceId (o, si no lo tiene, su nombre) es ese id.
    private static GameObject BuscarActor(Scene escena, string id, out System.Type tipoDelId)
    {
        tipoDelId = null;
        foreach (var root in escena.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var prop = new SerializedObject(mb).FindProperty("persistenceId");
                if (prop == null || prop.propertyType != SerializedPropertyType.String || prop.stringValue != id) continue;
                tipoDelId = mb.GetType();
                var r = PrefabUtility.GetOutermostPrefabInstanceRoot(mb.gameObject);
                return r != null ? r : mb.gameObject;
            }
        // Actores sin id de persistencia (figurantes de cinemática): se resuelven por el nombre.
        foreach (var root in escena.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == id && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) return t.gameObject;
        return null;
    }

    /// El id del actor en el mismo componente que lo llevaba (el prefab nuevo trae el suyo).
    private static void PonerId(GameObject raiz, string id, System.Type tipo)
    {
        if (tipo == null) return;
        foreach (var mb in raiz.GetComponentsInChildren(tipo, true))
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("persistenceId");
            if (prop == null || prop.propertyType != SerializedPropertyType.String || prop.stringValue == id) continue;
            prop.stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
