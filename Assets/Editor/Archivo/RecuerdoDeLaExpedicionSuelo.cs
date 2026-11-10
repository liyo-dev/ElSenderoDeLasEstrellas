#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Recuerdo de la expedición (WillHouse): rehace el suelo con una losa por cada 8 m (las losas de
/// floor2 miden 8 × 8 y estaban cada 4 m, solapadas a la misma altura: el suelo parpadeaba) y
/// separa las columnas de la derecha para que el pasillo quede igual de ancho a los dos lados.
/// Mismo reparto que Cap2MananaDespues.MontarExpedicion. Ver INC-953. Se puede ejecutar varias veces.
public static class RecuerdoDeLaExpedicionSuelo
{
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Suelo = "Assets/Art/World/Modular Castle/Assets/prefabs/floor2.prefab";

    [MenuItem("El Sendero/Archivo/Capítulo 2/Recuerdo de la expedición: suelo sin parpadeo y columnas (INC-953)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Recuerdo", "Sal del modo Play antes de ejecutarlo.");
            return;
        }
        var casa = EditorSceneManager.GetSceneByPath(Casa);
        bool abierta = casa.isLoaded;
        if (!abierta) casa = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        try
        {
            var arq = casa.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Arquitectura" && t.parent != null && t.parent.name == "EXPEDICION_FLASHBACK");
            if (arq == null) { OrdenesDelEditor.Avisar("Recuerdo", "No encuentro EXPEDICION_FLASHBACK/Arquitectura en WillHouse."); return; }
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Suelo);
            if (pf == null) { OrdenesDelEditor.Avisar("Recuerdo", "Falta " + Suelo); return; }

            int quitadas = 0;
            for (int i = arq.childCount - 1; i >= 0; i--)
                if (arq.GetChild(i).name.StartsWith("Floor_")) { Object.DestroyImmediate(arq.GetChild(i).gameObject); quitadas++; }
            int puestas = 0;
            foreach (var (i, x) in Cap2ExpedicionMedidas.XsDelSuelo.Select((x, i) => (i, x)))
                foreach (var (j, z) in Cap2ExpedicionMedidas.ZsDelSuelo.Select((z, j) => (j, z)))
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, casa);
                    go.name = $"Floor_{i}_{j}";
                    go.transform.SetParent(arq, false);
                    go.transform.SetPositionAndRotation(new Vector3(x, -2f, z), Quaternion.identity);
                    go.transform.SetSiblingIndex(puestas++);
                }

            int movidas = 0;
            foreach (Transform t in arq)
            {
                if (!t.name.StartsWith("Pillar_")) continue;
                if (!int.TryParse(t.name.Substring(7), out int k) || k % 2 == 0) continue;
                var p = t.position;
                t.position = new Vector3(Cap2ExpedicionMedidas.XColumnasDerecha, p.y, p.z);
                movidas++;
            }
            EditorSceneManager.MarkSceneDirty(casa);
            EditorSceneManager.SaveScene(casa);
            OrdenesDelEditor.Avisar("Recuerdo", $"Suelo: {quitadas} losas solapadas fuera, {puestas} puestas cada 8 m. Columnas de la derecha a x={Cap2ExpedicionMedidas.XColumnasDerecha}: {movidas}.");
        }
        finally
        {
            if (!abierta) EditorSceneManager.CloseScene(casa, true);
        }
    }
}
#endif
