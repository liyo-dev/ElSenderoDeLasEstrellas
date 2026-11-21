using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.NarrativeGraph.Editor.Story
{
    /// Retrato del personaje de un id, si el juego tiene los suyos (si no, se saca del prefab).
    public interface INarrativePortraitSource
    {
        Texture2D PortraitOf(string actorId);
    }

    /// Las imágenes de las tarjetas, sin que nadie tenga que ponerlas: el retrato del personaje,
    /// que sale del juego (INarrativePortraitSource) o de la miniatura de su prefab, y el dibujo
    /// de la categoría en el hueco de la antigua foto. Sin capturas: una foto por paso costaría un
    /// render, una lectura y un JPEG, y llenaría Library. Ajuste por usuario: dibujos y retratos,
    /// solo retratos o sin imágenes.
    public static class StoryCardImages
    {
        public enum Mode { ArtAndPortraits = 0, Portraits = 1, None = 2 }

        const string PrefMode = "NarrativeGraph.Cards.Images";

        public static event Action Changed;

        public static Mode Current
        {
            // Los valores 0/1/2 son los de siempre: 0 era «Fotos y retratos» y ahora es «Dibujos y retratos».
            get
            {
                int value = EditorPrefs.GetInt(PrefMode, (int)Mode.ArtAndPortraits);
                return value == (int)Mode.None ? Mode.None : value == (int)Mode.Portraits ? Mode.Portraits : Mode.ArtAndPortraits;
            }
            set { EditorPrefs.SetInt(PrefMode, (int)value); Changed?.Invoke(); }
        }

        public static bool ShowCardArt => Current == Mode.ArtAndPortraits;
        public static bool ShowPortraits => Current != Mode.None;

        /// Un dibujo propio para la categoría del paso, o null para el dibujo vectorial de serie.
        public static Texture2D CardArtOf(NarrativeNode node)
            => node == null || node.IsAnnotation ? null : NarrativeIconImages.CategoryArt(NarrativeNodeCatalog.CategoryOf(node.KindType));

        /// Un icono propio para el personaje, o null para usar su retrato.
        public static Texture2D ActorArtOf(string actorId) => NarrativeIconImages.ActorArt(actorId);

        public static void NotifyChanged() => Changed?.Invoke();

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

        /// <param name="prefabFallback">Si no hay retrato del juego, usar la miniatura del prefab.
        /// El mapa pide false: si no hay retrato, enseña la inicial.</param>
        public static Texture2D PortraitOf(string actorId, bool prefabFallback = true)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            if (s_portraits.TryGetValue(actorId, out var tex) && tex != null) return tex;
            if (!s_sourceLooked) { s_source = NarrativeEditorExtensions.CreateFirst<INarrativePortraitSource>(); s_sourceLooked = true; }
            tex = s_source?.PortraitOf(actorId);
            if (tex != null)
            {
                s_portraits[actorId] = tex;
                return tex;
            }
            if (!prefabFallback) return null;

            var path = NarrativeProjectIndex.FindActor(actorId)?.source;
            var prefab = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab") ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (prefab != null)
            {
                tex = AssetPreview.GetAssetPreview(prefab);
                // La miniatura del prefab llega más tarde: se vuelve a pintar cuando esté.
                if (tex == null && AssetPreview.IsLoadingAssetPreview(prefab.GetEntityId())) EditorApplication.delayCall += () => Changed?.Invoke();
            }
            return tex;
        }
    }
}
