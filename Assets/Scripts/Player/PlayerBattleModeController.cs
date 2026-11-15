using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

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
        [Tooltip("Voltereta del jugador (la del salto de victoria). Se busca sola si está vacía.")]
        [SerializeField] private VolteretaDelJugador voltereta;
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
        
        [Header("Celebración de victoria (INC-470)")]
        [Tooltip("Voltereta antes de la pose de victoria. Vacío = sin salto. Si el Animator no tiene el estado, se salta.")]
        [SerializeField] private string victoryJumpStateName = "JumpFullSpin_InPlace_NoWeapon";
        [Tooltip("Altura del salto, en alturas de la cabeza del personaje (1 = lo que mide de los pies a la cabeza). La da VolteretaDelJugador.")]
        [SerializeField] private float victoryJumpHeight = 1.4f;

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
        private float _brazosSueltosHasta;
        
        /// <summary>
        /// Indica si actualmente se está reproduciendo la secuencia de victoria
        /// </summary>
        public bool IsPlayingVictory => _isPlayingVictory;

        
        void Awake()
        {
            // Auto-encontrar referencias
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            
            if (controller == null)
                controller = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
            
            if (voltereta == null)
                voltereta = GetComponentInParent<VolteretaDelJugador>();

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
        
        void OnEnable()
        {
            // Suscribirse al evento de fin de animación de magia para restaurar battle idle
            if (controller != null)
            {
                controller.OnUpperBodyActionEnded += OnMagicAnimationEnded;
            }
        }
        
        void OnDisable()
        {
            // Una corrutina parada no llega a su final: se devuelve aquí lo que tomó la celebración.
            TerminarCelebracion();

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
                controller.OnUpperBodyActionEnded -= OnMagicAnimationEnded;
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
                    Debug.Log($"[PlayerBattleMode] Battle Idle RESTAURADO después de animación de magia (weight actual: {_currentLayerWeight:F2} → 1.0)");
#endif
            }
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
            
            // Interpolar hacia el peso objetivo (cero mientras los brazos están sueltos por un golpe)
            float objetivo = Time.time < _brazosSueltosHasta ? 0f : _targetLayerWeight;
            if (!Mathf.Approximately(_currentLayerWeight, objetivo))
            {
                float speed = 1f / Mathf.Max(0.01f, layerFadeDuration);
                _currentLayerWeight = Mathf.MoveTowards(_currentLayerWeight, objetivo, speed * Time.deltaTime);
                animator.SetLayerWeight(upperBodyLayerIndex, _currentLayerWeight);
                
#if UNITY_EDITOR
                if (debugMode && Mathf.Approximately(_currentLayerWeight, objetivo))
                {
                    Debug.Log($"[PlayerBattleMode] Capa UpperBody peso = {_currentLayerWeight:F2}");
                }
#endif
            }
        }
        
        /// <summary>
        /// Quita la pose de combate de los brazos durante unos segundos, para que se vea entera una
        /// animación de cuerpo completo (el golpe al recibir daño); después vuelve con su fundido.
        /// </summary>
        public void SoltarBrazos(float segundos)
        {
            _brazosSueltosHasta = Mathf.Max(_brazosSueltosHasta, Time.time + segundos);
            if (animator == null || animator.layerCount <= upperBodyLayerIndex) return;
            _currentLayerWeight = 0f;
            animator.SetLayerWeight(upperBodyLayerIndex, 0f);
        }

        /// <summary>
        /// Detecta si hay enemigos en combate activo consultando ActiveCombatRegistry.
        /// O(1) — no physics queries, no GetComponentInChildren.
        /// </summary>
        bool DetectEnemiesNearby()
        {
            return Kits.Core.GameContext.Has(Situaciones.Combate);
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
                Debug.Log($"[PlayerBattleMode] ENTRANDO en Battle Mode - UpperBody Layer activándose");
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
                Debug.Log($"[PlayerBattleMode] SALIENDO de Battle Mode - UpperBody Layer desactivándose");
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
        
        // ── Su parte de la celebración de victoria (la dirige CelebracionDeVictoria) ─────────

        /// Lo que dura su pose de victoria como mínimo.
        public float DuracionDeLaPose => victoryAnimationDuration;

        /// Empieza su parte de la celebración: deja el control, bloquea la entrada (pila de modos) y
        /// suelta la pose de combate de los brazos, que taparía la voltereta y los gestos.
        public void EmpezarCelebracion()
        {
            if (_isPlayingVictory) return;
            _isPlayingVictory = true;
            if (controller != null) controller.enabled = false;
            if (actionManager != null) actionManager.PushMode(ActionMode.Cinematic);
            ApagarCapaDeBrazos();
        }

        /// El salto con voltereta de la victoria; termina al aterrizar.
        public IEnumerator SaltoConVoltereta() => Co_SaltoConVoltereta();

        /// Un gesto de la celebración (vacío: su pose de victoria); termina al acabar, con tope.
        public IEnumerator Gesto(string estado, float tope) =>
            Co_Gesto(string.IsNullOrEmpty(estado) ? victoryStateName : estado, tope);

        /// <summary>
        /// Devuelve lo que tomó <see cref="EmpezarCelebracion"/>. Idempotente. Si el cuerpo ya no
        /// lo lleva el jugador, el controlador lo enciende su piloto, no esto.
        /// </summary>
        public void TerminarCelebracion()
        {
            if (!_isPlayingVictory) return;
            // IMPORTANTE: resetear el flag ANTES de re-habilitar el control (Update vuelve a correr)
            _isPlayingVictory = false;

            if (voltereta != null && voltereta.EnCursoDesdeElSuelo) voltereta.Cancelar();
            if (actionManager != null) actionManager.PopMode(ActionMode.Cinematic);

            // La capa de brazos se quedó con el último gesto: vuelve a su pose con la transición
            // suave de UpdateLayerWeight (a la de combate si seguimos en combate).
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                _currentLayerWeight = animator.GetLayerWeight(upperBodyLayerIndex);
                if (_isInBattleMode && isActiveAndEnabled) RestoreBattleIdle();
                else _targetLayerWeight = 0f;
            }

            // La pose de victoria tiene exit time a locomoción en el Animator: no hace falta forzarla.
            if (controller != null && isActiveAndEnabled) controller.enabled = true;
        }

        private void ApagarCapaDeBrazos()
        {
            if (animator == null || animator.layerCount <= upperBodyLayerIndex) return;
            animator.SetLayerWeight(upperBodyLayerIndex, 0f);
            _currentLayerWeight = 0f;
            _targetLayerWeight = 0f;
        }

        // ── Salto con voltereta ─────────────────────────────────────────────────────────────

        /// Salto con voltereta de la victoria (VolteretaDelJugador, en el sitio) y espera a que
        /// aterrice.
        private IEnumerator Co_SaltoConVoltereta()
        {
            if (voltereta == null || string.IsNullOrEmpty(victoryJumpStateName)) yield break;
            if (!voltereta.DesdeElSuelo(victoryJumpHeight, Vector3.zero, victoryJumpStateName)) yield break;
            while (voltereta.EnCurso) yield return null;
        }

        /// Tiempo normalizado del estado en la capa (también mientras se entra en él), o -1.
        private float TiempoNormalizado(int capa, int hash)
        {
            if (animator.IsInTransition(capa))
            {
                var siguiente = animator.GetNextAnimatorStateInfo(capa);
                if (siguiente.shortNameHash == hash) return siguiente.normalizedTime;
            }
            var actual = animator.GetCurrentAnimatorStateInfo(capa);
            return actual.shortNameHash == hash ? actual.normalizedTime : -1f;
        }

        /// Reproduce un gesto (en la capa que lo tenga; si es la de brazos, con peso 1 para que se
        /// vea) y espera a que acabe, con tope.
        private IEnumerator Co_Gesto(string estado, float tope)
        {
            if (animator == null || !TryPlayState(estado, 0.2f, out int capa)) yield break;
            if (capa == upperBodyLayerIndex) animator.SetLayerWeight(capa, 1f);

            int hash = Animator.StringToHash(estado);
            float limite = Time.time + Mathf.Max(0.5f, tope);

            float empezar = Time.time + 0.3f;
            while (Time.time < limite)
            {
                float t = TiempoNormalizado(capa, hash);
                if (t >= 0.95f) break;
                if (t < 0f && Time.time > empezar) break;   // ya ha salido del gesto
                yield return null;
            }
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
