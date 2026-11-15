using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarrativeGraphEditor.Story
{
    /// Retrato del personaje de un id, si el juego tiene los suyos (si no, se saca del prefab).
    public interface INarrativePortraitSource
    {
        Texture2D PortraitOf(string actorId);
    }

    /// Las imágenes de las tarjetas, sin que nadie tenga que ponerlas:
    ///   · retratos: los del juego (INarrativePortraitSource) o la miniatura del prefab del actor;
    ///   · fotos: se hacen solas en Play al entrar en cada paso (una vez por paso) y se guardan en
    ///     Library/NarrativeGraph/Fotos (no se suben al repositorio).
    /// Ajuste por usuario: con fotos, solo retratos o sin imágenes.
    public static class StoryCardImages
    {
        public enum Mode { Photos, Portraits, None }

        const string PrefMode = "NarrativeGraph.Cards.Images";
        const string PhotoFolder = "Library/NarrativeGraph/Fotos";
        const float PhotoDelay = 1.2f;

        public static event Action Changed;

        public static Mode Current
        {
            get => (Mode)EditorPrefs.GetInt(PrefMode, (int)Mode.Portraits);
            set { EditorPrefs.SetInt(PrefMode, (int)value); Changed?.Invoke(); }
        }

        public static bool ShowPhotos => Current == Mode.Photos;
        public static bool ShowPortraits => Current != Mode.None;

        // ── Retratos ────────────────────────────────────────────────────────

        static readonly Dictionary<string, Texture2D> s_portraits = new();
        static INarrativePortraitSource s_source;
        static bool s_sourceLooked;

        /// El personaje que protagoniza el paso (el primero que nombra), o null.
        public static string ActorOf(NarrativeNode node)
        {
            if (node == null) return null;
            try { return NarrativeNodeReferences.Of(node).Actors.Select(a => a.ActorId).FirstOrDefault(a => !string.IsNullOrEmpty(a)); }
            catch (Exception) { return null; }
        }

        public static Texture2D PortraitOf(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            if (s_portraits.TryGetValue(actorId, out var tex) && tex != null) return tex;
            if (!s_sourceLooked) { s_source = NarrativeEditorExtensions.CreateFirst<INarrativePortraitSource>(); s_sourceLooked = true; }
            tex = s_source?.PortraitOf(actorId);
            if (tex == null)
            {
                var path = NarrativeProjectIndex.FindActor(actorId)?.source;
                var prefab = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab") ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                if (prefab != null)
                {
                    tex = AssetPreview.GetAssetPreview(prefab);
                    // La miniatura del prefab llega más tarde: se vuelve a pintar cuando esté.
                    if (tex == null && AssetPreview.IsLoadingAssetPreview(prefab.GetEntityId())) EditorApplication.delayCall += () => Changed?.Invoke();
                }
            }
            if (tex != null) s_portraits[actorId] = tex;
            return tex;
        }

        // ── Fotos ───────────────────────────────────────────────────────────

        static readonly Dictionary<string, Texture2D> s_photos = new();
        static readonly HashSet<string> s_missing = new();

        static string PhotoPath(string guid) => Path.Combine(PhotoFolder, guid + ".jpg");

        public static bool HasPhoto(NarrativeNode node)
            => node != null && !string.IsNullOrEmpty(node.guid) && !s_missing.Contains(node.guid) && (s_photos.ContainsKey(node.guid) || File.Exists(PhotoPath(node.guid)));

        public static Texture2D PhotoOf(NarrativeNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.guid)) return null;
            if (s_photos.TryGetValue(node.guid, out var tex) && tex != null) return tex;
            var path = PhotoPath(node.guid);
            if (!File.Exists(path)) { s_missing.Add(node.guid); return null; }
            tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.LoadImage(File.ReadAllBytes(path));
            s_photos[node.guid] = tex;
            return tex;
        }

        /// Borra la foto del paso: se vuelve a hacer la próxima vez que se juegue.
        public static void ForgetPhoto(NarrativeNode node)
        {
            if (node == null) return;
            var path = PhotoPath(node.guid);
            if (File.Exists(path)) File.Delete(path);
            s_photos.Remove(node.guid);
            s_missing.Add(node.guid);
            Changed?.Invoke();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            NarrativeRunner.StepEntered -= OnStepEntered;
            NarrativeRunner.StepEntered += OnStepEntered;
        }

        static void OnStepEntered(NarrativeRunner runner, NarrativeNode node)
        {
            if (!EditorApplication.isPlaying || node == null || node.IsAnnotation || string.IsNullOrEmpty(node.guid)) return;
            if (File.Exists(PhotoPath(node.guid))) return;
            double at = EditorApplication.timeSinceStartup + PhotoDelay;
            void Later()
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= Later;
                if (EditorApplication.isPlaying) TakePhoto(node.guid);
            }
            EditorApplication.update += Later;
        }

        /// Lo que ve la cámara principal (sin la interfaz), pequeño.
        static void TakePhoto(string guid)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rt = RenderTexture.GetTemporary(480, 270, 24, RenderTextureFormat.ARGB32);
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else { var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old; }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(480, 270, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(PhotoFolder);
                File.WriteAllBytes(PhotoPath(guid), tex.EncodeToJPG(78));
                UnityEngine.Object.DestroyImmediate(tex);
                s_missing.Remove(guid);
                s_photos.Remove(guid);
                Changed?.Invoke();
            }
            catch (Exception) { }
            finally { RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
