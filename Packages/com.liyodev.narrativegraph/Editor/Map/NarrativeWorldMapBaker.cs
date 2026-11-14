using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NarrativeGraphEditor
{
    /// Hace el fondo del mapa de la historia: una foto desde arriba de las escenas abiertas
    /// (encuadra sus terrenos o, si no hay, todo lo que se ve) y la guarda junto al mapa.
    public static class NarrativeWorldMapBaker
    {
        const int Size = 2048;

        public static Texture2D TopDown(NarrativeWorldMap map)
        {
            if (map == null) return null;
            var bounds = WorldBounds();
            if (bounds.size == Vector3.zero) return null;

            var go = new GameObject("Vista del mapa") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z);
                cam.transform.SetPositionAndRotation(bounds.center + Vector3.up * (bounds.extents.y + 200f), Quaternion.Euler(90f, 0f, 0f));
                cam.nearClipPlane = 1f;
                cam.farClipPlane = bounds.size.y + 600f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.13f, 0.24f);
                cam.targetTexture = rt;
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var mapPath = AssetDatabase.GetAssetPath(map);
                var path = Path.Combine(Path.GetDirectoryName(mapPath) ?? "Assets", Path.GetFileNameWithoutExtension(mapPath) + " - vista.png").Replace('\\', '/');
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Undo.RecordObject(map, "Vista del mapa");
                map.background = asset;
                float half = cam.orthographicSize;
                map.hasWorldRect = true;
                map.worldRect = new Rect(bounds.center.x - half, bounds.center.z - half, half * 2f, half * 2f);
                NarrativeWorldPositions.Remember(map);
                EditorUtility.SetDirty(map);
                AssetDatabase.SaveAssetIfDirty(map);
                return asset;
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(go);
            }
        }

        static Bounds WorldBounds()
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
            Bounds? b = null;
            void Grow(Bounds x) { if (b == null) b = x; else { var c = b.Value; c.Encapsulate(x); b = c; } }
            foreach (var t in terrains)
                if (t.terrainData != null) Grow(new Bounds(t.transform.position + t.terrainData.size / 2f, t.terrainData.size));
            if (b == null)
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.enabled && r.gameObject.scene.IsValid()))
                    Grow(r.bounds);
            return b ?? new Bounds();
        }
    }
}
