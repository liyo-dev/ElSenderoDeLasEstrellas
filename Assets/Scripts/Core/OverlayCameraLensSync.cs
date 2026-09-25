using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Las cámaras overlay de una pila URP usan la MISMA lente que su cámara base (INC-415).
///
/// Caso real: `_WILL.prefab` lleva «Hint Camera», overlay hija de vThirdPersonCamera que pinta solo
/// la capa InteractHint (icono de interactuar, bocadillos de pelea…) por encima de todo. Su FOV
/// estaba fijo en 60°, y la cámara base —que TAMBIÉN pinta esa capa— cambia de FOV en cada plano de
/// cinemática (y hasta INC-415 se quedaba con el del último plano). En cuanto los dos FOV no
/// coinciden, cada icono se pinta dos veces, una con cada lente, más separadas cuanto más lejos del
/// centro de la pantalla. Esos eran los «iconos dobles» de INC-344/357/364/366/375/381/388: no había
/// dos iconos, había dos cámaras pintando el mismo.
///
/// Justo antes de que una cámara base empiece a renderizar (después de todos los LateUpdate, así
/// que da igual quién haya tocado el FOV este frame), se copia su FOV a cada overlay en perspectiva
/// de su pila. La posición no hace falta: la overlay es hija de la base con pose local cero.
public static class OverlayCameraLensSync
{
    // El UniversalAdditionalCameraData, cacheado por cámara: esto corre una vez por cámara y frame
    // (CLAUDE.md § 2: nada de GetComponent sin cachear en caminos por frame).
    private static readonly Dictionary<Camera, UniversalAdditionalCameraData> s_datos = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_datos.Clear(); }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        RenderPipelineManager.beginCameraRendering -= AntesDeRenderizar;
        RenderPipelineManager.beginCameraRendering += AntesDeRenderizar;
    }

    private static void AntesDeRenderizar(ScriptableRenderContext _, Camera cam)
    {
        if (cam == null || cam.cameraType != CameraType.Game || cam.orthographic) return;

        if (!s_datos.TryGetValue(cam, out var datos))
        {
            // Las cámaras destruidas al cambiar de escena se quedan de clave: se vacía de vez en cuando.
            if (s_datos.Count > 32) s_datos.Clear();
            cam.TryGetComponent(out datos);
            s_datos[cam] = datos; // null también se guarda: esa cámara no tiene pila
        }
        if (datos == null || datos.renderType != CameraRenderType.Base) return;

        var pila = datos.cameraStack;
        if (pila == null) return;

        float fov = cam.fieldOfView;
        for (int i = 0; i < pila.Count; i++)
        {
            var overlay = pila[i];
            if (overlay == null || overlay.orthographic) continue;
            if (!Mathf.Approximately(overlay.fieldOfView, fov)) overlay.fieldOfView = fov;
        }
    }
}
