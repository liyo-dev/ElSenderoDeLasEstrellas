using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kits.WorldWrap.Editor
{
    /// <summary>
    /// Revisa la costura de un <see cref="WorldWrapper"/>: el kit no duplica nada, así que lo que se
    /// vea desde el borde (a menos de la distancia de vista) aparecerá o desaparecerá de golpe al
    /// cruzar, y lo que quede fuera del rectángulo no se alcanza nunca. Mira renderers y terrenos
    /// activos de la escena del componente por el lado de su caja más cercano al borde; los planos
    /// con <see cref="EndlessSea"/> no cuentan.
    /// </summary>
    public static class WorldWrapSeamCheck
    {
        public struct Result
        {
            public int Outside;
            public int Near;
            public float ClosestDistance;
            public string ClosestName;
            public List<string> Examples;

            public bool Clean => Outside == 0 && Near == 0;
        }

        /// <summary>Distancia de vista por defecto: el plano lejano de la cámara principal (o 1000 m).</summary>
        public static float DefaultVisibleDistance()
        {
            var cam = Camera.main;
            return cam != null ? cam.farClipPlane : 1000f;
        }

        public static Result Run(WorldWrapper wrapper, float visibleDistance, ICollection<GameObject> ignore = null)
        {
            var result = new Result { Examples = new List<string>(), ClosestDistance = float.MaxValue };
            Scene scene = wrapper.gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (r.GetComponent<EndlessSea>() != null || Ignored(r.gameObject, ignore)) continue;
                    Tally(ref result, wrapper, r.bounds, r.name);
                }
                foreach (var t in root.GetComponentsInChildren<Terrain>(false))
                {
                    if (t.terrainData == null || !t.enabled || Ignored(t.gameObject, ignore)) continue;
                    var b = t.terrainData.bounds;
                    b.center += t.transform.position;
                    Tally(ref result, wrapper, b, t.name);
                }
            }
            if (result.ClosestDistance == float.MaxValue) result.ClosestDistance = 0f;
            CountNear(ref result, visibleDistance);
            return result;
        }

        private static readonly List<(string name, float distance)> s_found = new List<(string, float)>();

        private static void Tally(ref Result result, WorldWrapper w, Bounds b, string name)
        {
            float halfX = w.Size.x * 0.5f, halfZ = w.Size.y * 0.5f;
            float farX = Mathf.Max(Mathf.Abs(b.min.x - w.Center.x), Mathf.Abs(b.max.x - w.Center.x));
            float farZ = Mathf.Max(Mathf.Abs(b.min.z - w.Center.y), Mathf.Abs(b.max.z - w.Center.y));
            float toEdge = Mathf.Min(halfX - farX, halfZ - farZ);
            s_found.Add((name, toEdge));
            if (toEdge < result.ClosestDistance)
            {
                result.ClosestDistance = toEdge;
                result.ClosestName = name;
            }
        }

        private static void CountNear(ref Result result, float visibleDistance)
        {
            foreach (var (name, d) in s_found)
            {
                if (d < 0f) result.Outside++;
                else if (d < visibleDistance) result.Near++;
                else continue;
                if (result.Examples.Count < 10) result.Examples.Add($"{name} ({d:0} m)");
            }
            s_found.Clear();
        }

        private static bool Ignored(GameObject go, ICollection<GameObject> ignore)
        {
            if (ignore == null) return false;
            for (Transform t = go.transform; t != null; t = t.parent)
                if (ignore.Contains(t.gameObject)) return true;
            return false;
        }

        /// <summary>Texto para el registro con el resultado de <see cref="Run"/>.</summary>
        public static string Describe(Result r, float visibleDistance)
        {
            if (r.Clean)
                return $"[WorldWrap] Costura limpia: nada a menos de {visibleDistance:0} m del borde (lo más cerca: {r.ClosestName}, a {r.ClosestDistance:0} m).";
            return $"[WorldWrap] {r.Outside} objetos fuera del mundo y {r.Near} a menos de {visibleDistance:0} m del borde " +
                   $"(se verían aparecer o desaparecer al cruzar). Lo más cerca: {r.ClosestName} ({r.ClosestDistance:0} m). " +
                   "Ejemplos: " + string.Join(", ", r.Examples);
        }

        [MenuItem("Tools/WorldWrap/Revisar la costura de las escenas abiertas")]
        private static void CheckOpenScenes()
        {
            var wrappers = Object.FindObjectsByType<WorldWrapper>(FindObjectsInactive.Include);
            if (wrappers.Length == 0)
            {
                Debug.LogWarning("[WorldWrap] No hay ningún WorldWrapper en las escenas abiertas.");
                return;
            }
            float visible = DefaultVisibleDistance();
            foreach (var w in wrappers)
                Debug.Log($"{w.gameObject.scene.name}: " + Describe(Run(w, visible), visible), w);
        }
    }

    [CustomEditor(typeof(WorldWrapper))]
    public class WorldWrapperEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Revisar la costura"))
            {
                float visible = WorldWrapSeamCheck.DefaultVisibleDistance();
                var w = (WorldWrapper)target;
                Debug.Log(WorldWrapSeamCheck.Describe(WorldWrapSeamCheck.Run(w, visible), visible), w);
            }
        }
    }
}
