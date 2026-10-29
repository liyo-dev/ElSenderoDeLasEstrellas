using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElSendero.Editor.Capitulo2
{
    /// <summary>
    /// Corrige la posición local Y del SpawnAnchor 'Bedroom' en WillHouse.unity a 0 (estaba a 0.55m).
    /// Ver INC-843.
    /// </summary>
    public static class Cap2CorregirAnchorBedroom
    {
        private const string RutaEscena = "Assets/Scenes/Interior/WillHouse.unity";

        [InitializeOnLoadMethod]
        private static void EjecutarAlCargar()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                CorregirAnchor();
            };
        }

        [MenuItem("El Sendero/Archivo/Capítulo 2/Corregir anchor Bedroom de WillHouse")]
        public static void CorregirAnchor()
        {
            Scene escena = EditorSceneManager.GetSceneByPath(RutaEscena);
            bool abiertaPorNosotros = false;

            if (!escena.isLoaded)
            {
                if (!System.IO.File.Exists(RutaEscena)) return;
                escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
                abiertaPorNosotros = true;
            }

            if (!escena.IsValid() || !escena.isLoaded) return;

            var todos = escena.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<SpawnAnchor>(true));
            var bedroom = todos.FirstOrDefault(a => a.anchorId == "Bedroom");

            if (bedroom != null)
            {
                if (Mathf.Abs(bedroom.transform.localPosition.y) > 0.001f)
                {
                    var pos = bedroom.transform.localPosition;
                    pos.y = 0f;
                    bedroom.transform.localPosition = pos;
                    EditorUtility.SetDirty(bedroom.gameObject);
                    EditorSceneManager.MarkSceneDirty(escena);
                    EditorSceneManager.SaveScene(escena);
                    Debug.Log($"[Cap2CorregirAnchorBedroom] SpawnAnchor 'Bedroom' ajustado a Y=0 local en {RutaEscena} y guardado. (INC-843)");
                }
            }

            if (abiertaPorNosotros)
            {
                EditorSceneManager.CloseScene(escena, true);
            }
        }
    }
}
