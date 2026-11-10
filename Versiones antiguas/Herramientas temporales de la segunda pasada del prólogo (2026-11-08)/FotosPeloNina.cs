#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Fotos de un solo uso de la cabeza de Nina alrededor (INC-934): buscar el hueco entre el pelo y la cabeza.
public static class FotosPeloNina
{
    [MenuItem("El Sendero/Archivo/Fotos del pelo de Nina (INC-934)")]
    public static void Hacer()
    {
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_NPCs/NonInteractable/Nina_Prologo.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            var anim = inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController != null);
            var idle = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("Idle_Normal"));
            if (idle != null) idle.SampleAnimation(anim.gameObject, 0.5f);
            var luzGo = new GameObject("luz"); SceneManagementUtil(luzGo, escena);
            var luz = luzGo.AddComponent<Light>(); luz.type = LightType.Directional; luz.intensity = 1.2f;
            luzGo.transform.rotation = Quaternion.Euler(40, 30, 0);
            var camGo = new GameObject("cam"); SceneManagementUtil(camGo, escena);
            var cam = camGo.AddComponent<Camera>(); cam.scene = escena; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.9f, 0.2f); cam.fieldOfView = 30; cam.nearClipPlane = 0.05f;
            var head = inst.GetComponentsInChildren<Transform>(true).First(t => t.name == "head");
            Vector3 c = head.position + Vector3.up * 0.22f;
            var rt = new RenderTexture(512, 512, 24); cam.targetTexture = rt;
            Directory.CreateDirectory("Guiones/_fotos_caras/NinaPelo");
            foreach (int elev in new[] { 0, -20, 20 })
            for (int ang = 0; ang < 360; ang += 30)
            {
                var dir = Quaternion.Euler(-elev, ang, 0) * Vector3.forward;
                cam.transform.position = c + dir * 1.8f; cam.transform.LookAt(c);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(512, 512, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply();
                File.WriteAllBytes($"Guiones/_fotos_caras/NinaPelo/v_{ang:000}_{elev}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt);
        }
        finally { EditorSceneManager.ClosePreviewScene(escena); }
    }
    static void SceneManagementUtil(GameObject go, UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);
}
#endif
