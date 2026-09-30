using Invector;
using UnityEngine;

public class vThirdPersonCamera : MonoBehaviour
{
    #region Inspector

    public Transform target;
    [Tooltip("Lerp speed entre estados de cámara")]
    public float smoothCameraRotation = 12f;
    [Tooltip("Capas que bloquean (para raycasts de oclusión/altura)")]
    public LayerMask cullingLayer = 1 << 0;
    [Tooltip("Fijar cámara detrás del personaje (debug/alineación)")]
    public bool lockCamera;

    [Header("Seguimiento básico")]
    public float rightOffset = 0f;
    public float defaultDistance = 2.5f;
    public float height = 1.4f;
    public float smoothFollow = 10f;
    public float xMouseSensitivity = 3f;
    public float yMouseSensitivity = 3f;
    public float yMinLimit = -40f;
    public float yMaxLimit = 80f;

    [Header("Oclusión → Sombras (alternativa)")]
    public bool useShadowsOnlyOcclusion = false;
    public LayerMask obstructionMask = ~0;
    public float obstructionCheckRadius = 0.15f;
    
    [Header("Anulación de Lock-On")]
    public float overrideTargetHeightOffset = 1.5f;

    #endregion

    #region Estado oculto
    
    [HideInInspector] public Transform currentTarget;
    
    // Variable privada para el target de bloqueo
    [SerializeField]
    private Transform _lockTarget;
    public Transform LockTarget => _lockTarget;
    
    private Transform targetLookAt;
    private Camera _camera;
    private float distance;
    private float mouseY;
    private float mouseX;
    private float currentHeight;
    private float forward = -1f;
    private CameraOcclusionShadowsOnly _occlusionFader;
    public static bool lockCameraForCinematic = false;

    // Override de zona ambiental
    [HideInInspector] public bool zoneRotationLocked;
    private float _zoneLockedMouseX;
    private float _zoneLockedMouseY;

    // Vuelta suave al gameplay al re-activar el componente (menú de equipo, sueño) o al soltar
    // lockCameraForCinematic (diálogos, tienda, cinemáticas): mezcla posición Y rotación desde
    // donde estaba la cámara hasta su sitio de gameplay en un tiempo FIJO y luego queda anclada.
    private bool _doSmoothSnap;
    private float _snapElapsed;
    private Vector3 _snapFromPos;
    private Quaternion _snapFromRot;
    private bool _wasCinematicLocked;
    private const float SnapBlendDuration = 0.35f;

    #endregion

    void Start() => Init();

    void OnEnable()
    {
        // Si ya estaba inicializado (re-activación tras dormir u otra desactivación),
        // activar suavizado para no dar el golpe seco de posición.
        if (targetLookAt != null)
            BeginSmoothSnap();
    }

    private void BeginSmoothSnap()
    {
        _doSmoothSnap = true;
        _snapElapsed = 0f;
        _snapFromPos = transform.position;
        _snapFromRot = transform.rotation;
    }

    public void Init()
    {
        if (target == null) return;

        _camera = GetComponent<Camera>();
        currentTarget = target;

        if (targetLookAt == null)
        {
            targetLookAt = new GameObject("targetLookAt").transform;
            targetLookAt.hideFlags = HideFlags.HideInHierarchy;
        }

        mouseY = currentTarget.eulerAngles.x;
        mouseX = currentTarget.eulerAngles.y;
        distance = defaultDistance;
        currentHeight = height;

        _occlusionFader = GetComponent<CameraOcclusionShadowsOnly>();
        if (_occlusionFader == null) _occlusionFader = gameObject.AddComponent<CameraOcclusionShadowsOnly>();
        _occlusionFader.SetMask(obstructionMask);
        _occlusionFader.SetRadius(obstructionCheckRadius);

        // Cambio de target implica teleport de cámara; no suavizar.
        _doSmoothSnap = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (lockCameraForCinematic)
        {
            _wasCinematicLocked = true;
            return;
        }

        // Detectar salida de bloqueo cinematic para aplicar suavizado
        if (_wasCinematicLocked)
        {
            _wasCinematicLocked = false;
            if (targetLookAt != null)
                BeginSmoothSnap();
        }

        CameraMovement();
    }

    // ===================================================================================
    // API PÚBLICA PARA LOCK-ON
    // ===================================================================================

    public void SetLockTarget(Transform target)
    {
        // Debug.Log($"[vThirdPersonCamera] SetLockTarget LLAMADO en '{this.gameObject.name}'. Nuevo target: {(target != null ? target.name : "NULL")}");
        _lockTarget = target;
    }

    public void ClearLockTarget()
    {
        // if (_lockTarget != null) Debug.Log($"[vThirdPersonCamera] ClearLockTarget LLAMADO en '{this.gameObject.name}'. Liberando target: {_lockTarget.name}");
        _lockTarget = null;
    }

    /// <summary>Bloquea la cámara en el ángulo indicado; la zona ambiental lo llama al entrar.</summary>
    public void SetZoneRotation(float lockedMouseX, float lockedMouseY)
    {
        _zoneLockedMouseX = lockedMouseX;
        _zoneLockedMouseY = lockedMouseY;
        zoneRotationLocked = true;
    }

    public void ClearZoneRotation()
    {
        zoneRotationLocked = false;
    }

    public void SetMainTarget(Transform newTarget)
    {
        target = newTarget;
        currentTarget = newTarget;
        mouseY = currentTarget.rotation.eulerAngles.x;
        mouseX = currentTarget.rotation.eulerAngles.y;
        Init();
    }

    /// <summary>Fuerza los ángulos de órbita de la cámara (mouseX = horizontal, mouseY = vertical).
    /// Usar antes de re-habilitar la cámara tras una cinemática para evitar que aparezca detrás de una pared.</summary>
    public void SetAngles(float mouseXVal, float mouseYVal)
    {
        mouseX = mouseXVal;
        mouseY = vExtensions.ClampAngle(mouseYVal, yMinLimit, yMaxLimit);
        if (targetLookAt != null)
            targetLookAt.rotation = Quaternion.Euler(mouseY, mouseX, 0);
    }

    public void RotateCamera(float x, float y)
    {
        // FIX (cámara a trompicones al cerrar el menú de equipamiento, 27/08): mientras este
        // componente está deshabilitado (PlayerEquipmentMenuController.cs pone
        // mainThirdPersonCamera.enabled = false durante el desplazamiento/retorno de cámara del
        // menú), CameraMovement() no corre en LateUpdate — pero vThirdPersonInput.CameraInput()
        // sigue llamando a RotateCamera() todos los frames en cuanto el input de mirar se
        // reactiva (justo al cerrar el menú, antes de que termine el tween de retorno de ~0.4s).
        // Sin este guard, mouseX/mouseY seguían acumulando la rotación real del jugador durante
        // ese margen sin efecto visible, y al reactivarse el componente el Slerp de
        // CameraMovement() tenía que "recuperar" de golpe todo ese input acumulado — eso es lo
        // que se veía como cámara a trompicones justo tras cerrar el inventario, hasta que dejar
        // de mover la cámara le daba tiempo a converger.
        if (!isActiveAndEnabled || lockCameraForCinematic || _lockTarget != null || zoneRotationLocked) return;

        mouseX += x * xMouseSensitivity;
        mouseY -= y * yMouseSensitivity;
        mouseY = vExtensions.ClampAngle(mouseY, yMinLimit, yMaxLimit);
    }

    void CameraMovement()
    {
        if (currentTarget == null) return;

        distance = Mathf.Lerp(distance, defaultDistance, smoothFollow * Time.deltaTime);
        currentHeight = height;
        
        Vector3 targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y, currentTarget.position.z);
        Vector3 current_cPos = targetPos + new Vector3(0, currentHeight, 0);
        
        Quaternion newRot;

        if (_lockTarget != null)
        {
            // --- MODO LOCK-ON ---
            Vector3 lookAtPoint = _lockTarget.position + new Vector3(0, overrideTargetHeightOffset, 0);
            Vector3 dirToTarget = lookAtPoint - current_cPos;

            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
                
                // Asignar directamente los ángulos objetivo.
                // La suavidad vendrá del Slerp final de targetLookAt.rotation.
                mouseX = targetRot.eulerAngles.y;
                mouseY = targetRot.eulerAngles.x;
            }
            
            // Asegurar que mouseY respete los límites incluso en lock-on
            // (opcional, pero evita giros extraños si el enemigo está muy arriba/abajo)
            // mouseY = vExtensions.ClampAngle(mouseY, yMinLimit, yMaxLimit); 
            
            newRot = Quaternion.Euler(mouseY, mouseX, 0);
        }
        else
        {
            // --- MODO LIBRE ---
            if (zoneRotationLocked)
            {
                mouseX = Mathf.LerpAngle(mouseX, _zoneLockedMouseX, smoothCameraRotation * Time.deltaTime);
                mouseY = Mathf.Lerp(mouseY, _zoneLockedMouseY, smoothCameraRotation * Time.deltaTime);
            }
            var camDir = (forward * targetLookAt.forward) + (rightOffset * targetLookAt.right);
            camDir = camDir.normalized;
            newRot = Quaternion.Euler(mouseY, mouseX, 0);
        }

        // Aplicar rotación al pivote
        targetLookAt.position = current_cPos;
        targetLookAt.rotation = Quaternion.Slerp(targetLookAt.rotation, newRot, smoothCameraRotation * Time.deltaTime);
        
        // Calcular posición final de la cámara
        var finalCamDir = (forward * targetLookAt.forward) + (rightOffset * targetLookAt.right);
        finalCamDir = finalCamDir.normalized;
        
        Vector3 camPos = current_cPos + (finalCamDir * distance);

        var lookPoint = current_cPos + targetLookAt.forward * 2f;
        lookPoint += (targetLookAt.right * Vector3.Dot(finalCamDir * (distance), targetLookAt.right));
        Quaternion camRot = Quaternion.LookRotation(lookPoint - camPos);

        if (_doSmoothSnap)
        {
            // Mezcla con duración fija (tiempo real, para que acabe aunque el juego esté en
            // pausa). Antes era un SmoothDamp solo de posición que se daba por terminado al
            // quedar a <1 cm del destino: con el jugador en marcha el destino se mueve cada frame
            // y el SmoothDamp va siempre ~v·0,15 m por detrás, así que nunca terminaba — la cámara
            // perseguía a Will a trompicones (su posición avanza a pasos de física) hasta que el
            // jugador se paraba. Y la rotación saltaba de golpe a la final mientras la posición
            // seguía a medio camino. Ahora ambas llegan juntas y la cámara vuelve a quedar anclada.
            _snapElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_snapElapsed / SnapBlendDuration);
            float w = t * t * (3f - 2f * t);
            transform.SetPositionAndRotation(Vector3.Lerp(_snapFromPos, camPos, w), Quaternion.Slerp(_snapFromRot, camRot, w));
            if (t >= 1f) _doSmoothSnap = false;
        }
        else
        {
            transform.SetPositionAndRotation(camPos, camRot);
        }

        // Oclusión (desactivada en interiores: las paredes no deben desaparecer)
        if (_occlusionFader != null)
        {
            // No se puede referenciar EnvironmentController/EnvironmentMode aquí: viven en
            // Assets/Scripts (Assembly-CSharp), que compila DESPUÉS que Assets/Plugins.
            // EnvironmentQuery.IsInterior es el puente que mantiene actualizado ese estado.
            bool isInterior = EnvironmentQuery.IsInterior;

            if (isInterior)
                _occlusionFader.RestoreAll();
            else
                _occlusionFader.Process(transform.position, targetPos + new Vector3(0, currentHeight, 0));
        }
    }
}
