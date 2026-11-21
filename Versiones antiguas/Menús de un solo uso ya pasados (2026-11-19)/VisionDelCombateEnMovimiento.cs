#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// Pone la grabación del combate (Will contra el demonio) en la visión de la bola de la torre de
/// Liam (PROP_VisionDelCombate), para que se vea en movimiento. La imagen fija se queda como primer
/// fotograma mientras la grabación se prepara. Ver INC-1335.
public static class VisionDelCombateEnMovimiento
{
    const string RutaTorre = "Assets/Scenes/Interior/TorreDeLiam.unity";
    const string RutaGrabacion = "Assets/Art/World/Visiones/Vision_WillContraElDemonio.webm";
    const string Prop = "PROP_VisionDelCombate";

    [MenuItem("El Sendero/Cinemáticas/Torre de Liam: visión del combate en movimiento")]
    public static void Poner()
    {
        if (Application.isPlaying) { Debug.LogWarning("[VisionDelCombate] Fuera de Play."); return; }
        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(RutaGrabacion);
        if (clip == null) { Debug.LogError("[VisionDelCombate] Falta la grabación " + RutaGrabacion); return; }

        var escena = SceneManager.GetSceneByPath(RutaTorre);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaTorre, OpenSceneMode.Additive);

        var vision = escena.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<VentanaDeVision>(true))
            .FirstOrDefault(v => v.name == Prop);
        if (vision == null)
        {
            Debug.LogError("[VisionDelCombate] No encuentro " + Prop + " en la torre.");
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
            return;
        }

        var so = new SerializedObject(vision);
        so.FindProperty("grabacion").objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        Debug.Log($"[VisionDelCombate] {Prop}: grabación {clip.name} ({clip.width}×{clip.height}, {clip.length:0.0} s) puesta y torre guardada.");
    }
}
#endif
