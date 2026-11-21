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
    
    [Header("Suelo")]
    [Tooltip("Capas de suelo que la cámara no atraviesa: si el suelo se mete entre el personaje y la cámara, la cámara se acerca. Vacío = capa Floor. Ver INC-947")]
    public LayerMask capasDeSuelo = 0;
    [Tooltip("Holgura (radio de la esfera) que la cámara guarda con el suelo, en metros.")]
    public float holguraConElSuelo = 0.3f;
    [Tooltip("Segundos que tarda la cámara en acercarse o subirse cuando el suelo se le mete delante o debajo.")]
    public float suavizadoAlChocarConElSuelo = 0.08f;
    [Tooltip("Segundos que tarda en volver a su distancia y altura cuando el suelo deja de estorbar.")]
    public float suavizadoAlSoltarElSuelo = 0.45f;
    [Tooltip("Piezas finas del suelo (barandillas, pilotes, postes) que no acercan la cámara: lo que mide menos que esto de grueso en planta y es bajo, o es fino en las dos direcciones. 0 = todas cuentan.")]
    public float grosorDePiezaFina = 0.4f;
    [Tooltip("Altura por debajo de la cual una pieza fina en una sola dirección (una barandilla) no acerca la cámara, en metros. Una pared fina y alta sí la acerca.")]
    public float alturaDePiezaFina = 1.5f;

    [Header("Distancia")]
    [Tooltip("Multiplica la distancia de la cámara de juego (defaultDistance, también la que dejan las zonas de ambiente). No toca los planos fijos ni las cinemáticas. 1 = la de siempre.")]
    public float multiplicadorDeDistancia = 1.2f;

    [Header("Agua")]
    [Tooltip("Capas del agua (volúmenes con la superficie arriba): la cámara no se mete por debajo de su superficie. Vacío = capa Water. Ver INC-1331")]
    public LayerMask capasDeAgua = 0;
    [Tooltip("Altura mínima de la cámara sobre la superficie del agua, en metros.")]
    public float holguraConElAgua = 0.35f;

    [Header("Anulación de Lock-On")]
    public float overrideTargetHeightOffset = 1.5f;

    #endregion

    #region Estado oculto
    
    [HideInInspector] public Transform currentTarget;

    private readonly RaycastHit[] _golpesDelSuelo = new RaycastHit[8];
    
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
    private int _mascaraDeSuelo;
    private int _mascaraDeAgua;
    // Ajuste por el suelo, suavizado (ver AjustarAlSuelo): distancia acortada y elevación de la cámara.
    private float _distanciaPorSuelo = -1f, _velocidadDistanciaPorSuelo;
    private float _elevacionPorSuelo, _velocidadElevacionPorSuelo;
    // Altura del pivote bajo un techo bajo (suavizada, ver AlturaBajoTecho); -1 = sin calcular.
    private float _alturaBajoTecho = -1f, _velocidadAlturaBajoTecho;
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

    // Plano fijo (un asiento que se mueve, como un columpio): la cámara deja de seguir al objetivo y
    // encuadra un punto quieto desde un ángulo dado; entra y sale mezclando. Ver INC-1327.
    private bool _planoFijo;
    private Vector3 _pivoteFijo;
    private float _giroFijo, _inclinacionFija, _distanciaFija;
    private float _mezclaDelPlano;
    private const float DuracionMezclaDelPlano = 1.2f;
    private const float RitmoDeGiroDelPlano = 2.5f;

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
        _distanciaPorSuelo = -1f;
        _elevacionPorSuelo = 0f;
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
        _mascaraDeSuelo = capasDeSuelo.value != 0 ? capasDeSuelo.value : LayerMask.GetMask("Floor");
        _mascaraDeAgua = capasDeAgua.value != 0 ? capasDeAgua.value : LayerMask.GetMask("Water");
        // El suelo nunca se disuelve: la cámara se queda por delante de él (ver AjustarAlSuelo).
        _occlusionFader.SetMask(obstructionMask & ~_mascaraDeSuelo);
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

    /// <summary>Encuadra <paramref name="pivote"/> desde un ángulo fijo (giro e inclinación de la cámara, en
    /// grados) a <paramref name="distancia"/> metros, sin seguir al objetivo ni atender al ratón, hasta
    /// <see cref="SoltarPlano"/>. Entra mezclando desde donde esté.</summary>
    public void FijarPlano(Vector3 pivote, float giro, float inclinacion, float distancia)
    {
        _planoFijo = true;
        _pivoteFijo = pivote;
        _giroFijo = giro;
        _inclinacionFija = vExtensions.ClampAngle(inclinacion, yMinLimit, yMaxLimit);
        _distanciaFija = distancia;
    }

    /// <summary>Vuelve a seguir al objetivo desde el ángulo en que quedó el plano, mezclando.</summary>
    public void SoltarPlano() => _planoFijo = false;

    public bool HayPlanoFijo => _planoFijo;

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
        // En lock-on no bloqueamos el input del jugador: le permitimos ajustar suavemente la orientación
        if (!isActiveAndEnabled || lockCameraForCinematic || zoneRotationLocked || _planoFijo) return;

        mouseX += x * xMouseSensitivity;
        mouseY -= y * yMouseSensitivity;
        mouseY = vExtensions.ClampAngle(mouseY, yMinLimit, yMaxLimit);
    }

    /// <summary>
    /// La cámara frente al suelo (terreno, calles, cuestas): si el suelo se mete entre el pivote y la
    /// cámara, se acerca; si queda por encima del suelo que tiene debajo con menos holgura de la
    /// debida, se eleva. Las dos cosas van suavizadas (rápido al chocar, despacio al soltar), para
    /// que el suelo irregular no la haga dar tirones. Devuelve la distancia que hay que usar y deja
    /// en <see cref="_elevacionPorSuelo"/> cuánto se sube. Ver INC-947 e INC-969.
    /// </summary>
    private float AjustarAlSuelo(Vector3 pivote, Vector3 direccion, float distanciaDeseada)
    {
        if (_mascaraDeSuelo == 0 || distanciaDeseada <= 0f) return distanciaDeseada;
        float dt = Time.deltaTime;

        float objetivo = distanciaDeseada;
        int golpes = Physics.SphereCastNonAlloc(pivote, holguraConElSuelo, direccion, _golpesDelSuelo,
                                                distanciaDeseada, _mascaraDeSuelo, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < golpes; i++)
        {
            var g = _golpesDelSuelo[i];
            // distance 0 = la esfera ya empieza tocándolo: no da dónde, se ignora como antes.
            if (g.distance <= 0f || EsPiezaFina(g.collider)) continue;
            objetivo = Mathf.Min(objetivo, Mathf.Max(0.5f, g.distance));
        }
        if (_distanciaPorSuelo < 0f) _distanciaPorSuelo = objetivo;
        _distanciaPorSuelo = Mathf.SmoothDamp(_distanciaPorSuelo, objetivo, ref _velocidadDistanciaPorSuelo,
            objetivo < _distanciaPorSuelo ? suavizadoAlChocarConElSuelo : suavizadoAlSoltarElSuelo, Mathf.Infinity, dt);
        float distancia = Mathf.Min(distanciaDeseada, _distanciaPorSuelo);

        // Elevación: el suelo justo debajo de la cámara. El rayo sale de poco por encima de la
        // cámara, pero nunca de más arriba que el pivote: lo que queda por encima de la cabeza del
        // personaje (el tablero de un puente con él debajo, un piso de arriba) es techo, no suelo,
        // y subir la cámara por encima la dejaba al otro lado del tablero (INC-1331).
        Vector3 camara = pivote + direccion * distancia;
        float subir = 0f;
        float origenY = Mathf.Min(camara.y + 1f, pivote.y);
        if (Physics.Raycast(new Vector3(camara.x, origenY, camara.z), Vector3.down, out RaycastHit abajo, origenY - camara.y + 2f, _mascaraDeSuelo, QueryTriggerInteraction.Ignore))
            subir = Mathf.Max(0f, abajo.point.y + holguraConElSuelo - camara.y);
        // El agua: la cámara se queda por encima de la superficie (nadando, o con el personaje en la
        // orilla y la cámara sobre el río); bajo el agua solo se veía el fondo oscuro.
        if (_mascaraDeAgua != 0)
        {
            float alto = Mathf.Max(pivote.y, camara.y) + 2f;
            if (Physics.Raycast(new Vector3(camara.x, alto, camara.z), Vector3.down, out RaycastHit agua, alto - camara.y + holguraConElAgua, _mascaraDeAgua, QueryTriggerInteraction.Collide))
                subir = Mathf.Max(subir, agua.point.y + holguraConElAgua - camara.y);
        }
        _elevacionPorSuelo = Mathf.SmoothDamp(_elevacionPorSuelo, subir, ref _velocidadElevacionPorSuelo,
            subir > _elevacionPorSuelo ? suavizadoAlChocarConElSuelo : suavizadoAlSoltarElSuelo, Mathf.Infinity, dt);
        return distancia;
    }

    // Barandillas, pilotes y postes: pasan por delante un momento y no deben pegar la cámara al
    // personaje. Se mira la caja del collider, sin buscar componentes.
    private bool EsPiezaFina(Collider c)
    {
        if (c == null || grosorDePiezaFina <= 0f) return false;
        Vector3 t = c.bounds.size;
        bool finoEnX = t.x < grosorDePiezaFina, finoEnZ = t.z < grosorDePiezaFina;
        if (finoEnX && finoEnZ) return true;
        return (finoEnX || finoEnZ) && t.y < alturaDePiezaFina;
    }

    /// <summary>
    /// Con un techo bajo encima del personaje (nadando bajo un puente, un pasadizo), el pivote baja
    /// para quedarse por debajo: si no, la cámara miraba desde encima del tablero y el personaje
    /// quedaba tapado. Suavizado como el ajuste al suelo. Ver INC-1331.
    /// </summary>
    private float AlturaBajoTecho(Vector3 pies, float alturaDeseada)
    {
        if (_mascaraDeSuelo == 0) return alturaDeseada;
        const float desdeElPecho = 0.5f, alturaMinima = 0.6f;
        float objetivo = alturaDeseada;
        float alcance = alturaDeseada - desdeElPecho + holguraConElSuelo;
        if (alcance > 0f && Physics.Raycast(pies + Vector3.up * desdeElPecho, Vector3.up, out RaycastHit techo, alcance, _mascaraDeSuelo, QueryTriggerInteraction.Ignore))
            objetivo = Mathf.Clamp(desdeElPecho + techo.distance - holguraConElSuelo, alturaMinima, alturaDeseada);
        if (_alturaBajoTecho < 0f) _alturaBajoTecho = objetivo;
        _alturaBajoTecho = Mathf.SmoothDamp(_alturaBajoTecho, objetivo, ref _velocidadAlturaBajoTecho,
            objetivo < _alturaBajoTecho ? suavizadoAlChocarConElSuelo : suavizadoAlSoltarElSuelo, Mathf.Infinity, Time.deltaTime);
        return _alturaBajoTecho;
    }

    void CameraMovement()
    {
        if (currentTarget == null) return;

        _mezclaDelPlano = Mathf.MoveTowards(_mezclaDelPlano, _planoFijo ? 1f : 0f, Time.deltaTime / DuracionMezclaDelPlano);
        float pesoDelPlano = _mezclaDelPlano * _mezclaDelPlano * (3f - 2f * _mezclaDelPlano);

        float distanciaDeJuego = defaultDistance * multiplicadorDeDistancia;
        float distanciaDeseada = pesoDelPlano > 0f ? Mathf.Lerp(distanciaDeJuego, _distanciaFija, pesoDelPlano) : distanciaDeJuego;
        distance = Mathf.Lerp(distance, distanciaDeseada, smoothFollow * Time.deltaTime);
        currentHeight = AlturaBajoTecho(currentTarget.position, height);
        
        Vector3 targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y, currentTarget.position.z);
        Vector3 current_cPos = targetPos + new Vector3(0, currentHeight, 0);
        if (pesoDelPlano > 0f) current_cPos = Vector3.Lerp(current_cPos, _pivoteFijo, pesoDelPlano);
        
        Quaternion newRot;

        if (_lockTarget != null)
        {
            // --- MODO LOCK-ON ---
            Vector3 lookAtPoint = _lockTarget.position + new Vector3(0, overrideTargetHeightOffset, 0);
            Vector3 dirToTarget = lookAtPoint - current_cPos;

            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
                
                // Seguimiento suave del objetivo para no dar latigazos si el enemigo se mueve o desliza rápido
                float targetY = targetRot.eulerAngles.y;
                float targetX = targetRot.eulerAngles.x;
                mouseX = Mathf.LerpAngle(mouseX, targetY, Mathf.Min(1f, 4.5f * Time.deltaTime));
                mouseY = Mathf.LerpAngle(mouseY, targetX, Mathf.Min(1f, 4.5f * Time.deltaTime));
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
            if (_planoFijo)
            {
                float k = 1f - Mathf.Exp(-RitmoDeGiroDelPlano * Time.deltaTime);
                mouseX = Mathf.LerpAngle(mouseX, _giroFijo, k);
                mouseY = Mathf.LerpAngle(mouseY, _inclinacionFija, k);
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
        
        float distanciaFinal = AjustarAlSuelo(current_cPos, finalCamDir, distance);
        Vector3 camPos = current_cPos + (finalCamDir * distanciaFinal) + Vector3.up * _elevacionPorSuelo;

        var lookPoint = current_cPos + targetLookAt.forward * 2f;
        lookPoint += (targetLookAt.right * Vector3.Dot(finalCamDir * distanciaFinal, targetLookAt.right));
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
