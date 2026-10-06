using UnityEngine;

namespace Game.NPC.Common
{
    /// <summary>
    /// Elige un controlador facial entre los componentes del personaje y sus hijos.
    /// Prefiere perfil con mallas de ojos o boca, después mallas, luego activo y habilitado
    /// y, como último recurso, el primer controlador disponible.
    /// Se resuelve de forma perezosa: cada controlador cachea sus mallas en Awake y el orden
    /// de Awake entre componentes del mismo GameObject no está garantizado.
    /// </summary>
    public static class EmotionControllerResolver
    {
        public static NPCEmotionController Resolve(GameObject root)
        {
            if (root == null) return null;

            var all = root.GetComponentsInChildren<NPCEmotionController>(true);
            if (all == null || all.Length == 0) return null;
            if (all.Length == 1) return all[0];

            // Prioriza el controlador que puede aplicar un perfil a las mallas disponibles.
            foreach (var c in all)
                if (c != null && c.EmotionProfile != null && (c.EyeMeshCount > 0 || c.MouthMeshCount > 0))
                    return c;

            // Si no hay perfil, conserva el criterio de mallas disponibles.
            foreach (var c in all)
                if (c != null && (c.EyeMeshCount > 0 || c.MouthMeshCount > 0))
                    return c;

            // Sin mallas cacheadas, prefiere un controlador activo y habilitado.
            foreach (var c in all)
                if (c != null && c.isActiveAndEnabled)
                    return c;

            // Devuelve el primero si no se cumple ninguno de los criterios.
            return all[0];
        }
    }
}
