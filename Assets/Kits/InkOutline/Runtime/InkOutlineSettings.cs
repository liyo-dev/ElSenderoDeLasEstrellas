using UnityEngine;

namespace Kits.InkOutline
{
    /// Qué modelos llevan normales suavizadas para la línea de tinta. Cada juego crea uno con sus
    /// carpetas de personajes; el importador (<c>InkOutlineSmoothNormals</c>) las calcula al importar.
    [CreateAssetMenu(menuName = "Kits/Ink Outline Settings", fileName = "InkOutlineSettings")]
    public sealed class InkOutlineSettings : ScriptableObject
    {
        [Tooltip("Carpetas (ruta desde Assets/) cuyos modelos llevan normales suavizadas para la línea.")]
        [SerializeField] private string[] modelFolders = new string[0];

        public bool Covers(string assetPath)
        {
            foreach (var folder in modelFolders)
                if (!string.IsNullOrEmpty(folder) && assetPath.StartsWith(folder.TrimEnd('/') + "/")) return true;
            return false;
        }
    }
}
