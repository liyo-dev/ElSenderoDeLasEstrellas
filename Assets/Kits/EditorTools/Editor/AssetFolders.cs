using System.IO;
using UnityEditor;

namespace Kits.EditorTools
{
    /// <summary>Utilidades de carpetas del proyecto para las herramientas de Editor.</summary>
    public static class AssetFolders
    {
        /// <summary>
        /// Crea la carpeta de assets y las que le falten por encima («Assets/A/B/C»). No hace
        /// nada si ya existe o si la ruta está vacía.
        /// </summary>
        public static void Ensure(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
