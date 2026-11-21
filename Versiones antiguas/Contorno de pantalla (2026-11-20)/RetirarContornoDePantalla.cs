#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Menú de un solo uso (INC-1347): quita el contorno de pantalla (ContornoDeTinta) de los perfiles de
/// volumen y su feature de los renderers. La línea de tinta de los personajes la dibuja Kits.InkOutline.
public static class RetirarContornoDePantalla
{
    [MenuItem("El Sendero/Aspecto de dibujo/Retirar el contorno de pantalla (un solo uso)")]
    public static void Retirar()
    {
        int perfiles = 0, features = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:VolumeProfile"))
        {
            var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (perfil == null || !perfil.Has<ContornoDeTinta>()) continue;
            var c = perfil.components.OfType<ContornoDeTinta>().First();
            perfil.Remove<ContornoDeTinta>();
            Object.DestroyImmediate(c, true);
            EditorUtility.SetDirty(perfil);
            perfiles++;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var datos = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (datos == null) continue;
            var viejas = datos.rendererFeatures.OfType<ContornoDeTintaFeature>().ToList();
            if (viejas.Count == 0) continue;
            var so = new SerializedObject(datos);
            var lista = so.FindProperty("m_RendererFeatures");
            var mapa = so.FindProperty("m_RendererFeatureMap");
            for (int i = lista.arraySize - 1; i >= 0; i--)
            {
                if (!(lista.GetArrayElementAtIndex(i).objectReferenceValue is ContornoDeTintaFeature)) continue;
                lista.GetArrayElementAtIndex(i).objectReferenceValue = null;
                lista.DeleteArrayElementAtIndex(i);
                if (i < mapa.arraySize) mapa.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var f in viejas) AssetDatabase.RemoveObjectFromAsset(f);
            datos.SetDirty();
            EditorUtility.SetDirty(datos);
            features++;
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Contorno", $"contorno de pantalla retirado de {perfiles} perfil(es) y {features} renderer(s)");
    }
}
#endif
