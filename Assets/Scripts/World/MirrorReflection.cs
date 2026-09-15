using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reflejo plano en tiempo real para un panel de muro (Prueba de Will — laberinto de espejos,
/// petición de Raúl 8 sep 2026). Solo se coloca en ~8-10 puntos clave del laberinto — el resto de
/// muros usan un material de "espejo oscuro" barato sin este componente, por rendimiento.
///
/// *** FIX 9 sep 2026: se quita el enganche a RenderPipelineManager.beginCameraRendering ***
/// La primera versión actualizaba y renderizaba la cámara de reflejo llamando manualmente a
/// UniversalRenderPipeline.RenderSingleCamera desde dentro de beginCameraRendering de la cámara
/// principal. En Unity 6 / URP con RenderGraph esa llamada anidada (renderizar una cámara desde
/// dentro del render de otra, en el mismo frame) choca con el contenedor de datos de frame
/// (ContextContainer) que usa internamente el pipeline y lanza
/// "InvalidOperationException: Type UniversalCameraData has already been created" — es justo el
/// error que viste. Es una limitación conocida del RenderGraph de URP 17, no un fallo de nuestra
/// lógica de reflejo en sí.
///
/// Arreglo: la cámara de reflejo ahora es una cámara NORMAL y habilitada de la escena — actualizamos
/// su posición/rotación/matriz de proyección cada frame en LateUpdate() (relativa a Camera.main) y
/// dejamos que el propio motor la renderice sola, como a cualquier otra cámara, en vez de invocar
/// manualmente el render desde dentro de otra cámara. Al ser una cámara más en la lista de cámaras
/// del frame (no una llamada anidada), no hay colisión posible con el ContextContainer de nadie.
/// Se activa/desactiva sola según si el muro está visible, para no gastar nada cuando no hace falta.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class MirrorReflection : MonoBehaviour
{
    [SerializeField] private LayerMask reflectLayers = ~0;
    [SerializeField] private float clipPlaneOffset = 0.02f;
    [SerializeField] private int textureSize = 512;

    private Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private MeshRenderer _renderer;
    private Material _material;

    private void OnEnable()
    {
        _renderer = GetComponent<MeshRenderer>();
        _material = _renderer.sharedMaterial;
    }

    private void OnDisable()
    {
        CleanUp();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            if (_reflectionCamera != null) _reflectionCamera.enabled = false;
            return;
        }

        Camera mainCam = Camera.main;
        bool shouldRender = _renderer != null && _renderer.isVisible && mainCam != null;

        if (!shouldRender)
        {
            if (_reflectionCamera != null) _reflectionCamera.enabled = false;
            return;
        }

        UpdateReflectionCamera(mainCam);
        _reflectionCamera.enabled = true;
    }

    private void UpdateReflectionCamera(Camera mainCam)
    {
        EnsureReflectionCamera();

        Vector3 planeNormal = transform.forward;
        Vector3 planePos = transform.position;

        Vector3 camPos = mainCam.transform.position;
        Vector3 toCam = camPos - planePos;
        float distance = Vector3.Dot(toCam, planeNormal);
        Vector3 reflectedPos = camPos - 2f * distance * planeNormal;

        Vector3 forward = mainCam.transform.forward;
        Vector3 reflectedForward = Vector3.Reflect(forward, planeNormal);
        Vector3 up = Vector3.Reflect(mainCam.transform.up, planeNormal);

        _reflectionCamera.transform.position = reflectedPos;
        _reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, up);
        _reflectionCamera.fieldOfView = mainCam.fieldOfView;
        _reflectionCamera.aspect = mainCam.aspect;
        _reflectionCamera.farClipPlane = mainCam.farClipPlane;
        _reflectionCamera.nearClipPlane = mainCam.nearClipPlane;

        Vector4 clipPlaneCameraSpace = CameraSpacePlane(_reflectionCamera, planePos, planeNormal, clipPlaneOffset);
        _reflectionCamera.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlaneCameraSpace);

        if (_material != null) _material.SetTexture("_ReflectionTex", _reflectionTexture);
    }

    private void EnsureReflectionCamera()
    {
        if (_reflectionTexture == null || _reflectionTexture.width != textureSize)
        {
            if (_reflectionTexture != null) _reflectionTexture.Release();
            _reflectionTexture = new RenderTexture(textureSize, textureSize, 16);
            _reflectionTexture.name = "MirrorReflectionRT_" + name;
            if (_reflectionCamera != null) _reflectionCamera.targetTexture = _reflectionTexture;
        }

        if (_reflectionCamera == null)
        {
            var go = new GameObject("MirrorReflectionCamera_" + name) { hideFlags = HideFlags.HideAndDontSave };
            _reflectionCamera = go.AddComponent<Camera>();
            _reflectionCamera.cameraType = CameraType.Reflection;
            _reflectionCamera.targetTexture = _reflectionTexture;
            _reflectionCamera.enabled = false; // LateUpdate la activa solo cuando hace falta
            // Se renderiza ANTES que la cámara principal (menor depth = antes), para que la textura
            // ya esté lista en el frame en que la cámara principal la muestrea.
            _reflectionCamera.depth = -100f;
            var camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = false;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.requiresDepthOption = CameraOverrideOption.Off;
        }
        _reflectionCamera.cullingMask = reflectLayers;
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float offset)
    {
        Vector3 offsetPos = pos + normal * offset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    private void CleanUp()
    {
        if (_reflectionCamera != null)
        {
            if (Application.isPlaying) Destroy(_reflectionCamera.gameObject);
            else DestroyImmediate(_reflectionCamera.gameObject);
            _reflectionCamera = null;
        }
        if (_reflectionTexture != null)
        {
            _reflectionTexture.Release();
            _reflectionTexture = null;
        }
    }
}
