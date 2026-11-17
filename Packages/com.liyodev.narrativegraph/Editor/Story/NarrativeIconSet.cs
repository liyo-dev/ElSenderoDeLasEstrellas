using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeGraphEditor.Story
{
    /// Los dibujos propios del grafo: una textura por categoría (en el hueco de la antigua foto)
    /// y una por personaje (en la tarjeta y en el mapa). Lo que falte se queda con el dibujo
    /// vectorial de la categoría o con el retrato del juego. Es opcional: sin asset, el grafo
    /// funciona igual.
    [CreateAssetMenu(menuName = "Narrative Graph/Dibujos del grafo", fileName = "Dibujos del grafo")]
    public sealed class NarrativeIconSet : ScriptableObject
    {
        [Serializable]
        public sealed class CategoryArt
        {
            [Tooltip("Nombre de la categoría tal y como lo declara el paso: Diálogo, Quests, Combate…")]
            public string category;
            [Tooltip("Dibujo de la categoría. Mejor 2:1 (por ejemplo 440 × 224) y con fondo transparente.")]
            public Texture2D image;
        }

        [Serializable]
        public sealed class ActorArt
        {
            [Tooltip("Id del personaje tal y como lo nombra el grafo.")]
            public string actorId;
            [Tooltip("Icono del personaje. Mejor cuadrado (por ejemplo 128 × 128) y con fondo transparente.")]
            public Texture2D image;
        }

        public List<CategoryArt> categories = new();
        public List<ActorArt> actors = new();

        public Texture2D CategoryOf(string category)
        {
            if (string.IsNullOrEmpty(category)) return null;
            foreach (var entry in categories)
                if (entry != null && entry.category == category && entry.image != null) return entry.image;
            return null;
        }

        public Texture2D ActorOf(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            foreach (var entry in actors)
                if (entry != null && entry.actorId == actorId && entry.image != null) return entry.image;
            return null;
        }

        /// Las categorías que conoce el proyecto, conservando los dibujos ya asignados.
        public void FillCategories()
        {
            var names = new List<string>();
            foreach (var c in NarrativeNodeCatalog.Categories)
                if (!names.Contains(c.name)) names.Add(c.name);
            foreach (var e in NarrativeNodeCatalog.All)
                if (!string.IsNullOrEmpty(e.category) && !names.Contains(e.category)) names.Add(e.category);

            var old = categories;
            categories = new List<CategoryArt>();
            foreach (var name in names)
            {
                CategoryArt found = null;
                if (old != null) foreach (var entry in old)
                    if (entry != null && entry.category == name) { found = entry; break; }
                categories.Add(new CategoryArt { category = name, image = found?.image });
            }
            // Lo que el proyecto ya tuviera y no conozca el catálogo se queda.
            if (old != null) foreach (var entry in old)
                if (entry != null && !string.IsNullOrEmpty(entry.category) && !names.Contains(entry.category))
                    categories.Add(new CategoryArt { category = entry.category, image = entry.image });
        }

        /// Los personajes que conoce el proyecto, conservando los iconos ya asignados.
        public void FillActors()
        {
            var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in NarrativeProjectIndex.Actors.Keys) ids.Add(id);
            var map = NarrativeWorldMap.Find();
            if (map != null)
                foreach (var c in map.characters)
                    if (c != null && !string.IsNullOrEmpty(c.actorId)) ids.Add(c.actorId);
            foreach (var graph in NarrativeProjectIndex.Graphs)
                foreach (var node in graph.nodes)
                    if (node != null)
                        foreach (var actor in NarrativeNodeReferences.Of(node).Actors)
                            if (!string.IsNullOrEmpty(actor.ActorId)) ids.Add(actor.ActorId);

            var old = actors;
            actors = new List<ActorArt>();
            foreach (var id in ids)
            {
                ActorArt found = null;
                if (old != null) foreach (var entry in old)
                    if (entry != null && entry.actorId == id) { found = entry; break; }
                actors.Add(new ActorArt { actorId = id, image = found?.image });
            }
            // Los actores que ya estuvieran y no salgan del proyecto se quedan.
            if (old != null) foreach (var entry in old)
                if (entry != null && !string.IsNullOrEmpty(entry.actorId) && !ids.Contains(entry.actorId))
                    actors.Add(new ActorArt { actorId = entry.actorId, image = entry.image });
        }

        public void ClearImages()
        {
            foreach (var entry in categories) if (entry != null) entry.image = null;
            foreach (var entry in actors) if (entry != null) entry.image = null;
        }
    }
}
