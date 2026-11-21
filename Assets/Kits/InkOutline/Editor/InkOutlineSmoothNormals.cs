using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kits.InkOutline.Editor
{
    /// Al importar un modelo de las carpetas de <see cref="InkOutlineSettings"/>, guarda en la tangente
    /// (w = 2) la normal media de todos los vértices que comparten posición. La línea de tinta infla la
    /// funda por ahí, así que no se abre en las aristas duras. La tangente se deforma con los huesos,
    /// como la normal. El shader de dibujo no usa tangentes.
    public sealed class InkOutlineSmoothNormals : AssetPostprocessor
    {
        private static InkOutlineSettings _settings;
        private static bool _searched;

        public override uint GetVersion() => 1;

        private static InkOutlineSettings Settings
        {
            get
            {
                if (_settings != null || _searched) return _settings;
                _searched = true;
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(InkOutlineSettings)))
                {
                    _settings = AssetDatabase.LoadAssetAtPath<InkOutlineSettings>(AssetDatabase.GUIDToAssetPath(guid));
                    if (_settings != null) break;
                }
                return _settings;
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            var settings = Settings;
            if (settings == null || !settings.Covers(assetPath)) return;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) Smooth(filter.sharedMesh);
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Smooth(skinned.sharedMesh);
        }

        private static void Smooth(Mesh mesh)
        {
            if (mesh == null) return;
            var positions = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != positions.Length) return;
            var sums = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                var key = Key(positions[i]);
                sums[key] = sums.TryGetValue(key, out var n) ? n + normals[i] : normals[i];
            }
            var tangents = new Vector4[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var n = sums[Key(positions[i])];
                n = n.sqrMagnitude > 1e-8f ? n.normalized : normals[i];
                tangents[i] = new Vector4(n.x, n.y, n.z, 2f);
            }
            mesh.tangents = tangents;
        }

        // Posiciones iguales a una décima de milímetro.
        private static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));

        /// Reimporta los modelos de las carpetas configuradas (tras cambiarlas o la primera vez).
        [MenuItem("Tools/InkOutline/Reimport the configured character models")]
        public static void ReimportAll()
        {
            _settings = null; _searched = false;
            var settings = Settings;
            if (settings == null) { Debug.LogWarning("[InkOutline] No hay InkOutlineSettings en el proyecto."); return; }
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!settings.Covers(path)) continue;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[InkOutline] {n} modelos reimportados con normales suavizadas para la línea.");
        }
    }
}
