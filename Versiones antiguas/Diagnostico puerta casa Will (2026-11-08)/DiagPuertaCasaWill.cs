#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Diagnóstico temporal de la puerta de la casa de Will (se quita al cerrar INC-962).
public static class DiagPuertaCasaWill
{
    [MenuItem("El Sendero/Archivo/Diagnóstico: puerta de la casa de Will (INC-962)")]
    public static void Ver()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            sb.Append($"[escena {s.name} cargada={s.isLoaded} sucia={s.isDirty}] ");
        }
        var mw = SceneManager.GetSceneByPath("Assets/Scenes/Worlds/MainWorld.unity");
        if (!mw.isLoaded) { OrdenesDelEditor.Avisar("Diag", sb + "MainWorld no está abierta."); return; }
        foreach (var r in mw.GetRootGameObjects())
        {
            foreach (var a in r.GetComponentsInChildren<SpawnAnchor>(true))
            {
                if (a.name.Contains("House_FrontDoor") || a.name.Contains("Eldran_Plaza"))
                    sb.Append($"[ancla {a.name} pos={a.transform.position} fwd={a.transform.forward} activo={a.gameObject.activeInHierarchy}] ");
            }
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.Contains("House_FrontDoor") && !t.name.Contains("Eldran_Plaza") && !t.name.ToLower().Contains("willhouse")) continue;
                var cs = t.GetComponents<Component>();
                sb.Append($"[obj {t.name} pos={t.position} rot={t.eulerAngles} activo={t.gameObject.activeInHierarchy} comps=");
                foreach (var c in cs) if (c) sb.Append(c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : "") + ",");
                var col = t.GetComponent<Collider>();
                if (col) sb.Append($" bounds={col.bounds.center}/{col.bounds.size} trig={col.isTrigger}");
                sb.Append("] ");
            }
        }
        Debug.Log("[Diag puerta] " + sb);
        OrdenesDelEditor.Avisar("Diag", sb.ToString());
    }
}
#endif
