using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kits.InkOutline.Editor
{
    /// Añade <see cref="InkOutlineFeature"/> a los renderers del pipeline activo que no la tengan.
    public static class InkOutlineInstaller
    {
        // El shader va referenciado en la feature para que entre en la build.
        private static void AssignShader(InkOutlineFeature feature)
        {
            var so = new SerializedObject(feature);
            var prop = so.FindProperty("shader");
            if (prop.objectReferenceValue != null) return;
            var shader = Shader.Find(InkOutlineFeature.ShaderName);
            if (shader == null)
                foreach (var guid in AssetDatabase.FindAssets("InkOutline t:Shader"))
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader == null) { Debug.LogWarning("[InkOutline] No encuentro el shader " + InkOutlineFeature.ShaderName); return; }
            prop.objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(feature);
        }

        [MenuItem("Tools/InkOutline/Add the ink outline to the active pipeline renderers")]
        public static void Install()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) { Debug.LogWarning("[InkOutline] El pipeline activo no es URP."); return; }
            var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
            int added = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                var data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                if (data == null) continue;
                var existing = data.rendererFeatures.OfType<InkOutlineFeature>().FirstOrDefault();
                if (existing != null) { AssignShader(existing); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); continue; }
                var feature = ScriptableObject.CreateInstance<InkOutlineFeature>();
                feature.name = "InkOutline";
                AssignShader(feature);
                AssetDatabase.AddObjectToAsset(feature, data);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
                so.ApplyModifiedProperties();
                data.SetDirty();
                EditorUtility.SetDirty(data);
                added++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[InkOutline] Línea de tinta añadida a {added} renderer(s).");
        }
    }
}
