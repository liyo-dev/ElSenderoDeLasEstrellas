using System;
using System.Collections;
using UnityEngine;
using Invector.vCharacterController;
using Unity.Cinemachine;

namespace Game.Player
{
    /// <summary>
    /// Gestiona el estado de batalla del jugador.
    /// Detecta cuando hay NPCs enemigos cerca y activa la pose de batalla en la parte superior del cuerpo.
    /// Usa Layer 1 (UpperBody) con Avatar Mask para que los brazos estén en pose de combate
    /// mientras las piernas siguen la locomoción normal.
    /// </summary>
    public class PlayerBattleModeController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator animator;
        [SerializeField] private vThirdPersonController controller;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private PlayerActionManager actionManager;
        
        [Header("Configuración de Capas del Animator")]
        [Tooltip("Índice de la capa UpperBody en el Animator (normalmente 1)")]
        [SerializeField] private int upperBodyLayerIndex = 1;
        
        [Tooltip("Nombre del estado Battle Idle en la capa UpperBody")]
        [SerializeField] private string battleIdleStateName = "Idle_Battle_NoWeapon";
        
        [Tooltip("Path completo del estado Battle Idle (ej: UpperBody.Idle_Battle_NoWeapon). Dejar vacío para usar solo el nombre.")]
        [SerializeField] private string battleIdleFullPath = "UpperBody.Idle_Battle_NoWeapon";
        
        [Tooltip("Nombre del estado de Victoria en el Animator del player")]
        [SerializeField] private string victoryStateName = "Victory_NoWeapon";
        
        [Header("Detección de Combate")]
#if UNITY_EDITOR
        [Tooltip("Radio de detección de enemigos para activar Battle Mode")]
        [SerializeField] private float enemyDetectionRadius = 15f;
#endif
        
        [Tooltip("Layer de enemigos (Enemy)")]
        [SerializeField] private LayerMask enemyLayer = ~0;
        
        [Tooltip("Tiempo sin enemigos cerca para desactivar Battle Mode")]
        [SerializeField] private float exitBattleDelay = 3f;
        
        [Header("Transiciones")]
        [Tooltip("Duración del fade para activar/desactivar la capa UpperBody")]
        [SerializeField] private float layerFadeDuration = 0.3f;
        
        [Tooltip("Duración de la animación de victoria en segundos")]
        [SerializeField] private float victoryAnimationDuration = 3f;
        
        [Header("Audio")]
        [Tooltip("Clave del evento de audio para victoria (configurado en AudioGraphProfile)")]
        [SerializeField] private string victorySfxKey = "Npc_Battle_Victory";

        [Header("Cámara de Victoria")]
        [Tooltip("Si está activo, durante la animación de victoria la cámara enfoca al jugador en vez de dejar la cámara de gameplay tal cual. Deja espacio en pantalla para los pop-ups de recompensa (próximo scope).")]
        [SerializeField] private bool enableVictoryCamera = true;
        [Tooltip("Distancia de la cámara de victoria al jugador")]
        [SerializeField] private float victoryCamDistance = 2.8f;
        [Tooltip("Altura de la cámara de victoria respecto al suelo del jugador")]
        [SerializeField] private float victoryCamHeight = 1.6f;
        [Tooltip("Ángulo (grados) respecto al forward del jugador en el momento de la victoria. Negativo = frente-izquierda, dejando el lado derecho de la pantalla libre para pop-ups.")]
        [SerializeField] private float victoryCamYawOffsetDeg = -35f;
        [Tooltip("Altura del punto al que mira la cámara (relativa a los pies del jugador)")]
        [SerializeField] private float victoryCamLookHeight = 1.3f;
        [Tooltip("FOV de la cámara de victoria (más cerrado que el de gameplay = más protagonismo del jugador)")]
        [SerializeField] private float victoryCamFOV = 38f;
        [Tooltip("Duración del blend de entrada/salida de la cámara de victoria")]
        [SerializeField] private float victoryCamBlendSeconds = 0.4f;

        [Header("Celebración de victoria (INC-470)")]
        [Tooltip("Salto antes de la pose de victoria. Vacío = sin salto. Si el Animator no tiene el estado, se salta.")]
        [SerializeField] private string victoryJumpStateName = "JumpFullSpin_InPlace_NoWeapon";
        [Tooltip("Tope de segundos del salto (por si el estado no termina nunca).")]
        [SerializeField] private float victoryJumpMaxSeconds = 1.8f;
        [Tooltip("La cámara de victoria arranca más lejos y más alta, de lado, y se acerca girando hasta el plano final en estos segundos.")]
        [SerializeField] private float victoryCamMoveSeconds = 3f;
        [SerializeField] private float victoryCamStartDistance = 6.5f;
        [SerializeField] private float victoryCamStartHeight = 3f;
        [Tooltip("Ángulo de salida respecto al jugador (el de llegada es victoryCamYawOffsetDeg).")]
        [SerializeField] private float victoryCamStartYawOffsetDeg = -110f;
        [Tooltip("En las batallas con cierre (jefes), segundos que se deja ver la derrota del enemigo antes de celebrarlo.")]
        [SerializeField] private float victoryDelayAfterDefeat = 1.5f;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool debugMode;
#endif
        
        private bool _isInBattleMode;
        private bool _isPlayingVictory;
        private float _timeSinceLastEnemyDetected;
        private int _battleIdleHash;
        
        // Estado de la capa
        private float _currentLayerWeight;
        private float _targetLayerWeight;
        
        /// <summary>
        /// Indica si actualmente se está reproduciendo la secuencia de victoria
        /// </summary>
        public bool IsPlayingVictory => _isPlayingVictory;

        // --- Cámara de victoria ---
        private CinemachineCamera _victoryVcam;
        private bool _victoryVcamReady;
        private CinemachineCamera _gameplayVcam;
        private int _gameplayVcamOriginalPriority;
        private CinemachineBrain _mainBrain;
        private float _mainBrainOriginalBlendTime;
        private bool _victoryCameraActive;

        /// <summary>
        /// Se dispara cuando la cámara de victoria ya está enfocando al jugador (tras el blend de entrada).
        /// El sistema de recompensas post-batalla (próximo scope) puede escuchar este evento para
        /// lanzar los pop-ups de items/XP conseguidos en la batalla, ya con el encuadre definitivo.
        /// </summary>
        public event Action OnVictoryCameraFocused;
        
        void Awake()
        {
            // Auto-encontrar referencias
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            
            if (controller == null)
                controller = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
            
            if (playerRigidbody == null)
                playerRigidbody = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

            if (actionManager == null)
                actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();
            
            // Cachear hashes de estados
            _battleIdleHash = Animator.StringToHash(battleIdleStateName);
            
            // Asegurar que la capa empieza desactivada
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                animator.SetLayerWeight(upperBodyLayerIndex, 0f);
            }
            
            _currentLayerWeight = 0f;
            _targetLayerWeight = 0f;
        }
        
        private PasoCelebracionDeVictoria _pasoDeCierre;

        void OnEnable()
        {
            // El NPCCombatLifecycleHandler llamará directamente a PlayVictory(). En las batallas
            // de arena, la celebración es un paso del cierre de batalla (INC-470).
            _pasoDeCierre ??= new PasoCelebracionDeVictoria(this, victoryDelayAfterDefeat);
            CierreDeBatalla.Registrar(_pasoDeCierre);
            
            // Suscribirse al evento de fin de animación de magia para restaurar battle idle
            if (controller != null)
            {
                controller.OnMagicCastAnimationEnded += OnMagicAnimationEnded;
            }
        }
        
        void OnDisable()
        {
            if (_pasoDeCierre != null) CierreDeBatalla.Quitar(_pasoDeCierre);

            // Desactivar la capa al deshabilitarse
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                animator.SetLayerWeight(upperBodyLayerIndex, 0f);
            }

            if (_isInBattleMode && actionManager != null)
            {
                actionManager.PopMode(ActionMode.Combat);
            }
            _isInBattleMode = false;
            _targetLayerWeight = 0f;
            _currentLayerWeight = 0f;
            
            // Desuscribirse del evento
            if (controller != null)
            {
                controller.OnMagicCastAnimationEnded -= OnMagicAnimationEnded;
            }
        }
        
        /// <summary>
        /// Callback cuando termina una animación de magia.
        /// Si estamos en modo batalla, restauramos el battle idle.
        /// </summary>
        private void OnMagicAnimationEnded()
        {
            if (_isInBattleMode && !_isPlayingVictory)
            {
                RestoreBattleIdle();
            }
        }
        
        /// <summary>
        /// Restaura el battle idle en la capa UpperBody
        /// </summary>
        private void RestoreBattleIdle()
        {
            if (animator == null || animator.layerCount <= upperBodyLayerIndex) return;
            
            // IMPORTANTE: Sincronizar _currentLayerWeight con el valor real del animator
            // ya que vThirdPersonController puede haber modificado el peso directamente
            _currentLayerWeight = animator.GetLayerWeight(upperBodyLayerIndex);
            
            // Establecer el objetivo para que la transición suave funcione
            _targetLayerWeight = 1f;
            
            if (animator.HasState(upperBodyLayerIndex, _battleIdleHash))
            {
                // Usar el full path si está definido, sino el nombre simple
                string statePath = !string.IsNullOrEmpty(battleIdleFullPath) ? battleIdleFullPath : battleIdleStateName;
                
                // Forzar la animación de battle idle
                animator.CrossFadeInFixedTime(statePath, 0.2f, upperBodyLayerIndex);
                
#if UNITY_EDITOR
                if (debugMode)
                    Debug.Log($"[PlayerBattleMode] 🗡️ Battle Idle RESTAURADO después de animación de magia (weight actual: {_currentLayerWeight:F2} → 1.0)");
#endif
            }
        }
        
        // Guarda el battleId del último combate para reproducir la música correcta
        private string _currentBattleId;
        
        /// <summary>
        /// Método público para que el NPC llame cuando el player gana
        /// </summary>
        /// <param name="battleId">ID del combate para restaurar la música después de la victoria</param>
        public void PlayVictory(string battleId = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PlayerBattleMode] 🎯 PlayVictory() LLAMADO - _isPlayingVictory: {_isPlayingVictory}, battleId: {battleId ?? "null"}");
#endif
            
            if (_isPlayingVictory)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Victoria ya en reproducción - ignorando llamada duplicada (battleId: {battleId ?? "null"})");
#endif
                return;
            }
            
            _currentBattleId = battleId;
            StartCoroutine(PlayVictorySequence());
        }
        
        /// <summary>
        /// Suprime temporalmente el Battle Mode (tras diálogos de combate, etc.)
        /// </summary>
        public void SuppressBattleMode(float duration = 2f)
        {
            StartCoroutine(SuppressBattleModeRoutine(duration));
        }
        
        private IEnumerator SuppressBattleModeRoutine(float duration)
        {
            _targetLayerWeight = 0f;
            yield return new WaitForSeconds(duration);
        }
        
        void Update()
        {
            if (animator == null) return;
            
            // No hacer nada si está reproduciendo victoria
            if (_isPlayingVictory) return;
            
            // Detectar enemigos cercanos
            bool enemiesNearby = DetectEnemiesNearby();
            
            if (enemiesNearby)
            {
                _timeSinceLastEnemyDetected = 0f;
                
                if (!_isInBattleMode)
                {
                    EnterBattleMode();
                }
            }
            else
            {
                _timeSinceLastEnemyDetected += Time.deltaTime;
                
                // Salir del modo batalla después del delay
                if (_isInBattleMode && _timeSinceLastEnemyDetected >= exitBattleDelay)
                {
                    ExitBattleMode();
                }
            }
            
            // Actualizar peso de la capa con transición suave
            UpdateLayerWeight();
        }
        
        /// <summary>
        /// Actualiza el peso de la capa UpperBody con transición suave
        /// </summary>
        void UpdateLayerWeight()
        {
            if (animator == null || animator.layerCount <= upperBodyLayerIndex) return;
            
            // Interpolar hacia el peso objetivo
            if (!Mathf.Approximately(_currentLayerWeight, _targetLayerWeight))
            {
                float speed = 1f / Mathf.Max(0.01f, layerFadeDuration);
                _currentLayerWeight = Mathf.MoveTowards(_currentLayerWeight, _targetLayerWeight, speed * Time.deltaTime);
                animator.SetLayerWeight(upperBodyLayerIndex, _currentLayerWeight);
                
#if UNITY_EDITOR
                if (debugMode && Mathf.Approximately(_currentLayerWeight, _targetLayerWeight))
                {
                    Debug.Log($"[PlayerBattleMode] Capa UpperBody peso = {_currentLayerWeight:F2}");
                }
#endif
            }
        }
        
        /// <summary>
        /// Detecta si hay enemigos en combate activo consultando ActiveCombatRegistry.
        /// O(1) — no physics queries, no GetComponentInChildren.
        /// </summary>
        bool DetectEnemiesNearby()
        {
            return ActiveCombatRegistry.Count > 0;
        }
        
        /// <summary>
        /// Entra en modo batalla - Activa la capa UpperBody con pose de combate
        /// </summary>
        void EnterBattleMode()
        {
            if (_isInBattleMode) return;
            
            _isInBattleMode = true;
            _targetLayerWeight = 1f;
            GameplayEventLog.Log("BatallaInicio");

            if (actionManager != null)
                actionManager.PushMode(ActionMode.Combat);
            
            // Asegurar que la animación de batalla esté reproduciéndose en la capa
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                // Verificar si el estado existe en la capa
                if (animator.HasState(upperBodyLayerIndex, _battleIdleHash))
                {
                    // Usar el full path si está definido, sino el nombre simple
                    string statePath = !string.IsNullOrEmpty(battleIdleFullPath) ? battleIdleFullPath : battleIdleStateName;
                    animator.CrossFadeInFixedTime(statePath, 0.2f, upperBodyLayerIndex);
                }
            }
            
#if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🗡️ ENTRANDO en Battle Mode - UpperBody Layer activándose");
#endif
        }
        
        /// <summary>
        /// Sale del modo batalla - Desactiva la capa UpperBody
        /// </summary>
        void ExitBattleMode()
        {
            if (!_isInBattleMode) return;
            
            _isInBattleMode = false;
            _targetLayerWeight = 0f;
            GameplayEventLog.Log("BatallaFin");

            if (actionManager != null)
                actionManager.PopMode(ActionMode.Combat);
            
#if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🏡 SALIENDO de Battle Mode - UpperBody Layer desactivándose");
#endif
        }
        
        /// <summary>
        /// Fuerza la entrada/salida del modo batalla (para uso externo)
        /// </summary>
        public void SetBattleMode(bool active)
        {
            if (active)
                EnterBattleMode();
            else
                ExitBattleMode();
        }
        
        /// <summary>
        /// Verifica si está en modo batalla
        /// </summary>
        public bool IsInBattleMode => _isInBattleMode;
        
        /// <summary>
        /// Crea (una única vez, en runtime) la cámara virtual de victoria. No requiere ninguna
        /// referencia asignada a mano en el Inspector: se construye y configura por código,
        /// igual que hace DialogueCinematicController con su pool de cámaras.
        /// </summary>
        private void EnsureVictoryCamera()
        {
            if (_victoryVcamReady) return;

            _gameplayVcam = ServiceLocator.Get<CinemachineCamera>(logIfMissing: false);

            var camGo = new GameObject("PlayerVictoryVCam");
            camGo.transform.SetParent(transform);
            _victoryVcam = camGo.AddComponent<CinemachineCamera>();
            _victoryVcam.Priority.Value = 0; // inactiva hasta que se active la victoria
            _victoryVcam.Lens.FieldOfView = victoryCamFOV;
            _victoryVcam.Lens.NearClipPlane = 0.1f;
            _victoryVcam.Lens.FarClipPlane = 1000f;

            if (Camera.main != null)
                _mainBrain = Camera.main.GetComponent<CinemachineBrain>();

            _victoryVcamReady = true;
        }

        /// <summary>
        /// Activa la cámara de victoria enfocando al jugador. Encuadre en 3/4 lateral con espacio
        /// libre en pantalla (ver victoryCamYawOffsetDeg) pensado para los pop-ups de recompensa
        /// que se añadirán en el próximo scope.
        /// </summary>
        private void ActivateVictoryCamera()
        {
            if (!enableVictoryCamera) return;

            EnsureVictoryCamera();
            if (_victoryVcam == null) return;

            // Girar al jugador para que quede de cara a la cámara de gameplay activa en el
            // momento de la victoria, en vez de dejarlo con la orientación que tuviera al
            // terminar el combate (que puede ser cualquiera: de lado, de espaldas, a medio
            // esquivar...). Sin esto, colocar la cámara relativa a un "forward" arbitrario no
            // garantiza una pose de cara -- solo evita quedar exactamente detrás (bug anterior).
            // Con el jugador ya orientado hacia la cámara, el offset de victoryCamYawOffsetDeg
            // sí produce el 3/4 de cara buscado de forma consistente.
            Camera refCam = Camera.main;
            if (refCam != null)
            {
                Vector3 camForward = refCam.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.0001f)
                {
                    camForward.Normalize();
                    transform.rotation = Quaternion.LookRotation(-camForward, Vector3.up);
                }
            }

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            // La cámara se coloca EN LA DIRECCIÓN a la que mira el jugador (ya girado hacia
            // cámara arriba), para que el plano quede de frente/3-4 y se vea la cara durante la
            // pose de victoria. Con "-flatForward" (bug histórico, INC pendiente) la cámara
            // quedaba detrás del personaje mirando en la misma dirección que él, dejándolo de
            // espaldas a cámara.
            Quaternion yaw = Quaternion.AngleAxis(victoryCamYawOffsetDeg, Vector3.up);
            Vector3 offsetDir = yaw * flatForward;
            Vector3 camPos = transform.position + offsetDir * victoryCamDistance + Vector3.up * victoryCamHeight;
            Vector3 lookAt = transform.position + Vector3.up * victoryCamLookHeight;

            _victoryVcam.transform.position = camPos;
            _victoryVcam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            _victoryVcam.Target.TrackingTarget = transform;

            if (_gameplayVcam != null)
            {
                _gameplayVcamOriginalPriority = _gameplayVcam.Priority.Value;
            }

            if (_mainBrain != null)
            {
                _mainBrainOriginalBlendTime = _mainBrain.DefaultBlend.Time;
                _mainBrain.DefaultBlend.Time = victoryCamBlendSeconds;
            }

            _victoryVcam.Priority.Value = (_gameplayVcam != null ? _gameplayVcam.Priority.Value : 10) + 10;
            _victoryCameraActive = true;

#if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🎥 Cámara de victoria activada (pos: {camPos})");
#endif
        }

        /// <summary>
        /// Restaura la prioridad de la cámara de gameplay y el blend por defecto del brain.
        /// </summary>
        private void DeactivateVictoryCamera()
        {
            if (!_victoryCameraActive) return;

            if (_victoryVcam != null)
                _victoryVcam.Priority.Value = 0;

            if (_gameplayVcam != null)
                _gameplayVcam.Priority.Value = _gameplayVcamOriginalPriority;

            if (_mainBrain != null)
                _mainBrain.DefaultBlend.Time = _mainBrainOriginalBlendTime;

            _victoryCameraActive = false;
        }

        /// <summary>
        /// Secuencia de victoria con animación y música
        /// </summary>
        IEnumerator PlayVictorySequence()
        {
            yield return CelebrarVictoria(_currentBattleId);
            TerminarVictoria();
        }

        /// <summary>
        /// La celebración de victoria sin devolver nada: bloquea el control, cámara que se acerca
        /// girando hasta Will, salto, pose de victoria y música. Termina cuando acaba la pose,
        /// con la cámara aún enfocando a Will. Lo devuelve todo TerminarVictoria(). Así el cierre
        /// de batalla (CierreDeBatalla) puede enseñar el informe con este mismo plano antes de
        /// devolver el control. INC-470.
        /// </summary>
        public IEnumerator CelebrarVictoria(string battleId)
        {
            _isPlayingVictory = true;
            _currentBattleId = battleId;
            GameplayEventLog.Log("Victoria", _currentBattleId);

            // Deshabilitar control del jugador mientras dura (campos públicos de Invector)
            if (controller != null) controller.enabled = false;

            // Bloquear input mientras dura la victoria (patrón oficial del proyecto: pila de modos)
            if (actionManager != null)
                actionManager.PushMode(ActionMode.Cinematic);

            // Cámara: arranca lejos y de lado y se acerca girando hasta el plano de 3/4 de Will.
            ActivateVictoryCamera();
            if (_victoryCameraActive) _victoryCamMove = StartCoroutine(Co_MoverCamaraDeVictoria());

            // Reproducir música de victoria usando el sistema de audio centralizado
            if (!string.IsNullOrEmpty(victorySfxKey) && AudioService.Instance != null)
            {
                // Usar PlayVictoryForBattle para reproducir la música de victoria correctamente
                // IMPORTANTE: holdSeconds = 0 significa que NO se restaura automáticamente
                // El NPCCombatLifecycleHandler se encargará de restaurar la música después del diálogo post-derrota
                AudioService.Instance.PlayVictoryForBattle(_currentBattleId ?? "", victorySfxKey, holdSeconds: 0f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[PlayerBattleMode] 🎵 ✅ Reproduciendo música de victoria: {victorySfxKey} (battleId: {_currentBattleId ?? "null"}) - Restauración manual por lifecycle handler");
#endif
            }
            else if (string.IsNullOrEmpty(victorySfxKey))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ victorySfxKey está vacío - no se reproduce audio");
#endif
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ AudioService.Instance es NULL - no se puede reproducir música");
#endif
            }
            
            // Salto y, al caer, la pose de victoria.
            if (animator != null && !string.IsNullOrEmpty(victoryJumpStateName) && TryPlayState(victoryJumpStateName, 0.15f, out int capaSalto))
            {
                int hashSalto = Animator.StringToHash(victoryJumpStateName);
                float tope = Time.time + Mathf.Max(0.3f, victoryJumpMaxSeconds);
                float empezar = Time.time + 0.3f;
                while (Time.time < tope)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(capaSalto);
                    bool enSalto = info.shortNameHash == hashSalto;
                    if (enSalto && info.normalizedTime >= 0.92f) break;
                    if (!enSalto && Time.time > empezar) break;   // ya ha salido del salto
                    yield return null;
                }
            }

            float empiezaPose = Time.time;
            if (animator == null || !TryPlayState(victoryStateName, 0.2f, out _))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Estado '{victoryStateName}' NO encontrado en el Animator");
#endif
            }

            // Aviso de «ya enfoca al jugador» (tras el blend de entrada de la cámara)
            if (_victoryCameraActive)
            {
                yield return new WaitForSeconds(victoryCamBlendSeconds);
                OnVictoryCameraFocused?.Invoke();
            }

            // Lo que dura la pose, y que la cámara haya llegado a su sitio.
            float fin = Mathf.Max(empiezaPose + victoryAnimationDuration, Time.time);
            float topeCamara = Time.time + victoryCamMoveSeconds + 2f;
            while (Time.time < fin || (_victoryCamMove != null && Time.time < topeCamara)) yield return null;
        }

        /// <summary>
        /// Devuelve lo que tomó CelebrarVictoria: cámara de gameplay, input y controlador.
        /// Idempotente.
        /// </summary>
        public void TerminarVictoria()
        {
            if (!_isPlayingVictory) return;
            // IMPORTANTE: resetear el flag ANTES de re-habilitar el control (Update vuelve a correr)
            _isPlayingVictory = false;

            if (_victoryCamMove != null) { StopCoroutine(_victoryCamMove); _victoryCamMove = null; }
            DeactivateVictoryCamera();
            if (actionManager != null)
                actionManager.PopMode(ActionMode.Cinematic);

            // La pose de victoria tiene exit time a locomoción en el Animator: no hace falta forzarla.
            if (controller != null) controller.enabled = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[PlayerBattleMode] ✅ Victoria terminada: control devuelto.");
#endif
        }

        private Coroutine _victoryCamMove;

        /// Plano de victoria con movimiento: de lejos, alto y de lado, a 3/4 de frente y cerca,
        /// girando alrededor de Will y sin perderlo de vista.
        private IEnumerator Co_MoverCamaraDeVictoria()
        {
            if (_victoryVcam == null) { _victoryCamMove = null; yield break; }

            Vector3 frente = transform.forward; frente.y = 0f;
            if (frente.sqrMagnitude < 0.0001f) frente = Vector3.forward;
            frente.Normalize();

            float dur = Mathf.Max(0.1f, victoryCamMoveSeconds);
            float t = 0f;
            while (t < 1f && _victoryVcam != null && _victoryCameraActive)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / dur);
                float k = Mathf.SmoothStep(0f, 1f, t);

                float yaw = Mathf.Lerp(victoryCamStartYawOffsetDeg, victoryCamYawOffsetDeg, k);
                float dist = Mathf.Lerp(victoryCamStartDistance, victoryCamDistance, k);
                float alto = Mathf.Lerp(victoryCamStartHeight, victoryCamHeight, k);

                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * frente;
                Vector3 pos = transform.position + dir * dist + Vector3.up * alto;
                Vector3 mira = transform.position + Vector3.up * victoryCamLookHeight;

                _victoryVcam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((mira - pos).normalized, Vector3.up));
                yield return null;
            }
            _victoryCamMove = null;
        }

        /// Reproduce un estado del Animator en la primera capa que lo tenga.
        private bool TryPlayState(string stateName, float fade, out int layer)
        {
            layer = -1;
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;
            int hash = Animator.StringToHash(stateName);
            for (int i = 0; i < animator.layerCount; i++)
            {
                if (!animator.HasState(i, hash)) continue;
                animator.CrossFadeInFixedTime(hash, fade, i);
                layer = i;
                return true;
            }
            return false;
        }
        
        // Debug Gizmos
        void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (!debugMode) return;
            
            Gizmos.color = _isInBattleMode ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, enemyDetectionRadius);
#endif
        }
    }
}
