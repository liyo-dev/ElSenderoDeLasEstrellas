using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Sendero.UI
{
    /// <summary>
    /// Sistema de HUD del jugador V2 - Con referencias desde Inspector.
    /// Usa arte personalizado en lugar de crear UI dinámicamente.
    /// Version: 2025-12-24
    /// 
    /// Vida, maná y visibilidad del HUD. Los círculos de combate (X, Y, B) los lleva
    /// CombatButtonsHUD (INC-485).
    /// </summary>
    public class PlayerHUDV2 : MonoBehaviour
    {
        // Singleton instance
        public static PlayerHUDV2 Instance { get; private set; }
        
        #if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
        #endif
        
        [Header("Referencias de Vida")]
        [Tooltip("Imagen fill para la barra de vida")]
        [SerializeField] private Image healthFillImage;
        
        [Tooltip("Texto opcional para mostrar HP numérico (ej: 100/100)")]
        [SerializeField] private TextMeshProUGUI healthText;
        
        [Header("Referencias de Magia")]
        [Tooltip("Imagen fill para la barra de maná")]
        [SerializeField] private Image manaFillImage;
        
        [Tooltip("Texto opcional para mostrar MP numérico (ej: 50/50)")]
        [SerializeField] private TextMeshProUGUI manaText;
        
        [Header("Configuración de Fade")]
        [Tooltip("Duración del fade in/out en segundos")]
        [SerializeField] private float fadeDuration = 0.5f;
        
        // Referencias a sistemas del juego
        private PlayerHealthSystem _healthSystem;
        private ManaPool _manaPool;
        
        // Control de visibilidad
        private CanvasGroup _canvasGroup;
        private Tween _currentFadeTween;
        private bool _isVisible = true;
        // Quién tiene pedido el HUD oculto (ver HideHUD/ShowHUD).
        private readonly System.Collections.Generic.HashSet<object> _ocultadoPor = new();
        private readonly System.Collections.Generic.List<object> _ocultadoresDestruidos = new();
        private float _siguienteRevision;
        private const float IntervaloRevision = 1f;
        
        private void Awake()
        {
            // Configurar Singleton
            if (Instance != null && Instance != this)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerHUDV2] Ya existe una instancia. Destruyendo duplicado.");
#endif
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Registrar en ServiceLocator
            ServiceLocator.Register(this);
            
            // Obtener o añadir CanvasGroup para el fade
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Asegurar que empieza visible
            _canvasGroup.alpha = 1f;
            _isVisible = true;
            _ocultadoPor.Clear();

            // FIX: este Canvas también lleva un SceneBoundUI (para ocultarse/mostrarse según la
            // escena activa), y SceneBoundUI.BeginBossIntro/EndBossIntro operan sobre el MISMO
            // CanvasGroup capturando su alpha actual y restaurándolo después. Si BeginBossIntro
            // se disparaba mientras HideHUD()/ShowHUD() todavía tenía un fade a medias (ej: justo
            // al entrar en combate contra un boss que arranca pegado a una cinemática, como el
            // Gólem), capturaba un alpha intermedio y lo dejaba "restaurado" ahí para siempre —
            // el HUD se quedaba prácticamente invisible el resto del combate. Excluimos este
            // Canvas del snapshot genérico de SceneBoundUI (mismo patrón que
            // DramaticTextOverlayUI.Awake()) y dejamos que HideHUD()/ShowHUD() sean la única
            // fuente de verdad sobre su visibilidad.
            GetComponent<SceneBoundUI>()?.ExcludeFromBossIntro();
            
            // Validar referencias críticas
            ValidateReferences();
        }
        
        private void Start()
        {
            // Obtener el jugador
            var player = PlayerService.Player;
            if (player == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogError("[PlayerHUDV2] ❌ No se pudo obtener el jugador desde PlayerService");
#endif
                return;
            }
            
            // Obtener componentes del jugador (buscar en hijos para soportar jerarquías anidadas)
            _healthSystem = player.GetComponentInChildren<PlayerHealthSystem>(true);
            _manaPool = player.GetComponentInChildren<ManaPool>(true);

            // Validar componentes críticos
            if (_healthSystem == null)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró PlayerHealthSystem en el jugador");
                #endif
            }
            if (_manaPool == null)
            {
                #if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró ManaPool en el jugador");
                #endif
            }
            // Suscribirse a eventos
            SubscribeToEvents();

            // Suscribirse al evento OnPresetApplied para refrescar cuando se carga partida
            PlayerPresetService.OnPresetApplied += OnPresetApplied;
            
            // Marcar como inicializado
            _hasStarted = true;
            
            // Actualización inicial
            RefreshHealthBar();
            RefreshManaBar();
            
            // Debug.Log($"[PlayerHUDV2] ✅ Start completado - Mana: {_manaPool?.Current ?? 0}/{_manaPool?.Max ?? 0}, HP: {_healthSystem?.CurrentHealth ?? 0}");
        }
        
        private void OnDestroy()
        {
            // Limpiar Singleton
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister(this);
            }
            
            UnsubscribeFromEvents();
            
            // ✅ Desuscribirse del evento de preset
            PlayerPresetService.OnPresetApplied -= OnPresetApplied;
            
            // Limpiar todos los tweens (incluido el punch de escala del daño, que corre sobre el Transform, no sobre la Image)
            if (healthFillImage != null)
            {
                healthFillImage.DOKill();
                healthFillImage.transform.DOKill(true);
            }
            if (manaFillImage != null) manaFillImage.DOKill();
            _currentFadeTween?.Kill();
        }
        
        #region Validación y Setup
        
        private void ValidateReferences()
        {
            bool hasErrors = false;
            
            if (healthFillImage == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogError("[PlayerHUDV2] ❌ healthFillImage no está asignado en el Inspector!");
#endif
                hasErrors = true;
            }
            
            if (manaFillImage == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogError("[PlayerHUDV2] ❌ manaFillImage no está asignado en el Inspector!");
#endif
                hasErrors = true;
            }
            
            if (hasErrors)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogError("[PlayerHUDV2] ⚠️ El HUD no funcionará correctamente sin las referencias necesarias.");
#endif
            }
        }
        
        #endregion
        
        #region Eventos
        
        private void SubscribeToEvents()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged.AddListener(OnHealthChanged);
                // Debug.Log("[PlayerHUDV2] ✅ Suscrito a OnHealthChanged");
            }
            
            if (_manaPool != null)
            {
                _manaPool.OnManaChanged.AddListener(OnManaChanged);
                // Debug.Log($"[PlayerHUDV2] ✅ Suscrito a OnManaChanged de ManaPool en '{_manaPool.gameObject.name}'");
            }
            else
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No hay ManaPool para suscribirse a OnManaChanged");
#endif
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged.RemoveListener(OnHealthChanged);
            }
            
            if (_manaPool != null)
            {
                _manaPool.OnManaChanged.RemoveListener(OnManaChanged);
            }
        }
        
        /// <summary>
        /// Bandera para indicar si ya se ejecutó Start()
        /// </summary>
        private bool _hasStarted = false;

        /// <summary>
        /// Callback cuando se aplica el preset (al cargar partida o cambiar preset)
        /// </summary>
        private void OnPresetApplied()
        {
            // Si Start() aún no se ha ejecutado, las referencias se configurarán ahí
            if (!_hasStarted)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.Log("[PlayerHUDV2] ⏭️ OnPresetApplied llamado antes de Start() - Se refrescará en Start()");
#endif
                return;
            }
            
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("[PlayerHUDV2] 🔄 OnPresetApplied - Refrescando HUD completo tras cargar partida");
#endif
            
            // RE-OBTENER referencias del player actual (pueden haber cambiado)
            var player = PlayerService.Player;
            if (player != null)
            {
                // Desuscribir de eventos anteriores
                UnsubscribeFromEvents();
                
                // Obtener nuevas referencias (buscar en hijos para soportar jerarquías anidadas)
                _healthSystem = player.GetComponentInChildren<PlayerHealthSystem>(true);
                _manaPool = player.GetComponentInChildren<ManaPool>(true);
                
                // Re-suscribirse a eventos
                SubscribeToEvents();
            }
            
            // Validar que tenemos las referencias
            if (_healthSystem == null || _manaPool == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[PlayerHUDV2] ⚠️ OnPresetApplied - Faltan referencias de componentes del player");
#endif
                return;
            }
            
            // Refrescar todo el HUD porque el preset puede haber cambiado
            RefreshHealthBar();
            RefreshManaBar();

            // Debug.Log($"[PlayerHUDV2] ✅ HUD refrescado completamente tras aplicar preset (Mana: {_manaPool.Current}/{_manaPool.Max})");
        }

        #endregion
        
        #region Actualización de Vida
        
        private float _lastHealthFillAmount = 1f;
        
        private void OnHealthChanged(float healthPercent)
        {
            RefreshHealthBar();
        }
        
        private void RefreshHealthBar()
        {
            if (healthFillImage == null) return;
            
            float currentHp = 0f;
            float maxHp = 1f;
            
            if (_healthSystem != null)
            {
                currentHp = _healthSystem.CurrentHealth;
                maxHp = _healthSystem.MaxHealth;
            }
            
            float targetFillAmount = maxHp > 0 ? currentHp / maxHp : 0f;
            float currentFillAmount = healthFillImage.fillAmount;
            
            // Detectar si es daño o curación
            bool isDamage = targetFillAmount < currentFillAmount;
            bool isHealing = targetFillAmount > currentFillAmount;
            
            // Cancelar tweens previos: el fillAmount (sobre la Image) Y el punch de escala anterior
            // (sobre el Transform, un target DISTINTO — DOKill() en la Image no lo mataba, así que
            // si un golpe llegaba antes de que el punch anterior (0.3s) terminase, se apilaban dos
            // tweens de escala a la vez y la barra se quedaba desajustada/creciendo fuera de su caja).
            healthFillImage.DOKill();
            healthFillImage.transform.DOKill(true); // true = completa el punch en curso (vuelve a escala 1) antes de matarlo
            healthFillImage.transform.localScale = Vector3.one; // red de seguridad por si ya venía desajustada de antes de este fix
            
            if (isDamage)
            {
                // DAÑO: Animación rápida e impactante con punch
                healthFillImage.DOFillAmount(targetFillAmount, 0.15f)
                    .SetEase(Ease.OutQuad);
                
                // Efecto de shake/punch en la escala
                healthFillImage.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5, 0.5f);
            }
            else if (isHealing)
            {
                // CURACIÓN: Animación suave y gradual
                healthFillImage.DOFillAmount(targetFillAmount, 0.4f)
                    .SetEase(Ease.OutCubic);
            }
            else
            {
                // Sin cambio, solo actualizar
                healthFillImage.fillAmount = targetFillAmount;
            }
            
            _lastHealthFillAmount = targetFillAmount;
            
            // Actualizar texto si existe
            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}";
            }
        }
        
        #endregion
        
        #region Actualización de Maná
        
        private float _lastManaFillAmount = 1f;
        private float _manaRegenStartTime = -1f;
        private bool _isRegenerating = false;
        // Objetivo de fillAmount para la regeneración. Se actualiza cada vez que ManaPool
        // notifica un cambio, pero quien realmente hace avanzar la interpolación es
        // TickManaRegen() desde Update() (ver comentario ahí abajo).
        private float _manaTargetFillAmount = 1f;

        private void OnManaChanged(float manaPercent)
        {
            RefreshManaBar();
        }

        private void RefreshManaBar()
        {
            if (manaFillImage == null) return;

            float currentMana = 0f;
            float maxMana = 1f;

            if (_manaPool != null)
            {
                currentMana = _manaPool.Current;
                maxMana = _manaPool.Max;
            }

            float targetFillAmount = maxMana > 0 ? currentMana / maxMana : 0f;
            float currentFillAmount = manaFillImage.fillAmount;

            // Detectar si es gasto o regeneración
            bool isSpending = targetFillAmount < currentFillAmount;
            bool isRegenerating = targetFillAmount > currentFillAmount;

            _manaTargetFillAmount = targetFillAmount;

            if (isSpending)
            {
                // GASTO: Animación rápida y cancelar cualquier regeneración
                manaFillImage.DOKill();
                _isRegenerating = false;

                manaFillImage.DOFillAmount(targetFillAmount, 0.2f)
                    .SetEase(Ease.OutQuad);
            }
            else if (isRegenerating)
            {
                // REGENERACIÓN: solo activamos la bandera aquí. La interpolación cuadro a cuadro
                // ocurre en TickManaRegen() (llamado desde Update()), NO en este método.
                //
                // FIX: antes el propio Lerp vivía aquí dentro, así que solo avanzaba un paso cada
                // vez que ManaPool disparaba OnManaChanged. En cuanto el maná llegaba al máximo,
                // ManaPool.Update() dejaba de notificar (early-return con "current >= max") y ese
                // último paso de Lerp casi nunca aterrizaba exactamente en 1 (un Lerp es asintótico:
                // se acerca pero no llega). Con fillAmount < 1 el Image tipo "Filled" recorta el
                // sprite de la barra por su borde derecho, así que la puntita redondeada del arte
                // (que solo existe completa en el sprite sin recortar, ver mp_bar_fill.png) se
                // quedaba mostrando un corte recto para siempre, aunque el maná ya estuviera lleno.
                // Al mover el Lerp a Update(), sigue avanzando cuadro a cuadro aunque no lleguen más
                // eventos de ManaPool, así que sí converge y encaja en el valor objetivo.
                if (!_isRegenerating)
                {
                    // Primera vez regenerando - cancelar tweens previos
                    manaFillImage.DOKill();
                    _isRegenerating = true;
                    _manaRegenStartTime = Time.time;
                }
            }
            else
            {
                // Sin cambio o ya completo
                _isRegenerating = false;
                manaFillImage.fillAmount = targetFillAmount;
            }

            _lastManaFillAmount = targetFillAmount;

            // Actualizar texto si existe
            if (manaText != null)
            {
                manaText.text = $"{Mathf.CeilToInt(currentMana)}/{Mathf.CeilToInt(maxMana)}";
            }
        }

        /// <summary>
        /// Avanza la interpolación de regeneración de maná un cuadro. Vive en Update() (no dentro
        /// de RefreshManaBar) precisamente para no depender de que ManaPool siga disparando
        /// OnManaChanged: así la barra siempre termina de converger a su valor final, incluyendo
        /// el "encaje" a fillAmount exacto que hace falta para que el sprite muestre su puntita
        /// redondeada completa. Ver el FIX comentado en RefreshManaBar().
        /// </summary>
        private void TickManaRegen()
        {
            if (!_isRegenerating || manaFillImage == null) return;

            float currentFillAmount = manaFillImage.fillAmount;
            float lerpSpeed = 5f; // Velocidad de interpolación
            float newFillAmount = Mathf.Lerp(currentFillAmount, _manaTargetFillAmount, lerpSpeed * Time.deltaTime);

            if (Mathf.Abs(_manaTargetFillAmount - newFillAmount) < 0.001f)
            {
                newFillAmount = _manaTargetFillAmount;
                _isRegenerating = false;
            }

            manaFillImage.fillAmount = newFillAmount;
        }

        #endregion
        
        private void Update()
        {
            // Sigue interpolando la barra de maná hacia su objetivo aunque ManaPool ya no esté
            // notificando cambios (ver comentario en RefreshManaBar() / TickManaRegen()).
            TickManaRegen();

            // Si quien ocultó el HUD se destruye sin soltarlo, se suelta solo.
            if (!_isVisible && Time.unscaledTime >= _siguienteRevision)
            {
                _siguienteRevision = Time.unscaledTime + IntervaloRevision;
                if (SoltarOcultadoresDestruidos()) MostrarSiNadieLoOculta(-1f);
            }
        }
        
        #region API Pública
        
        /// <summary>
        /// Fuerza una actualización completa del HUD
        /// </summary>
        public void ForceRefresh()
        {
            RefreshHealthBar();
            RefreshManaBar();
        }
        
        /// <summary>
        /// Muestra/oculta todo el HUD
        /// </summary>
        public void SetHUDVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        /// <summary>
        /// Pide ocultar el HUD (con fundido). Cada sistema lo pide y lo suelta con su propia clave,
        /// normalmente 'this': el HUD solo vuelve cuando todos los que lo pidieron lo han soltado.
        /// Pedirlo dos veces con la misma clave cuenta una sola vez, soltarlo sin haberlo pedido no
        /// hace nada, y si quien lo pidió (un objeto de Unity) se destruye sin soltarlo, se suelta
        /// solo. Ver INC-538.
        /// </summary>
        public void HideHUD(object quien, float duration = -1f)
        {
            if (quien == null) return;
            SoltarOcultadoresDestruidos();
            bool nuevo = _ocultadoPor.Add(quien);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            // Diagnóstico de INC-538 (HUD que no vuelve). Se quita al cerrar la incidencia.
            if (nuevo) Debug.Log($"[PlayerHUDV2:DIAG] Oculta: {Nombre(quien)}. Lo tienen oculto: {QuienLoOculta}");
#endif
            if (!_isVisible) return;

            _isVisible = false;
            float useDuration = duration > 0 ? duration : fadeDuration;
            _currentFadeTween?.Kill();

            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return;

            _currentFadeTween = _canvasGroup.DOFade(0f, useDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.interactable = false;
                        _canvasGroup.blocksRaycasts = false;
                    }
                });
        }

        /// <summary>Suelta lo pedido con <see cref="HideHUD"/>; el HUD vuelve si ya nadie lo oculta.</summary>
        public void ShowHUD(object quien, float duration = -1f)
        {
            if (quien == null || !_ocultadoPor.Remove(quien)) return;
            SoltarOcultadoresDestruidos();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlayerHUDV2:DIAG] Suelta: {Nombre(quien)}. Lo tienen oculto: {QuienLoOculta}");
#endif
            MostrarSiNadieLoOculta(duration);
        }

        /// Quién tiene pedido el HUD oculto ahora mismo (para depurar).
        public string QuienLoOculta
        {
            get
            {
                if (_ocultadoPor.Count == 0) return "nadie";
                var nombres = new System.Collections.Generic.List<string>(_ocultadoPor.Count);
                foreach (var q in _ocultadoPor) nombres.Add(Nombre(q));
                return string.Join(", ", nombres);
            }
        }

        private static string Nombre(object quien) => quien switch
        {
            UnityEngine.Object o when o == null => "(destruido)",
            UnityEngine.Object o => $"{o.GetType().Name} '{o.name}'",
            System.Type t => t.Name,
            _ => quien.GetType().Name,
        };

        private void MostrarSiNadieLoOculta(float duration)
        {
            if (_ocultadoPor.Count > 0 || _isVisible) return;

            _isVisible = true;
            float useDuration = duration > 0 ? duration : fadeDuration;
            _currentFadeTween?.Kill();

            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _currentFadeTween = _canvasGroup.DOFade(1f, useDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        /// Quita de la lista a los objetos de Unity ya destruidos. Devuelve true si quitó alguno.
        private bool SoltarOcultadoresDestruidos()
        {
            _ocultadoresDestruidos.Clear();
            foreach (var q in _ocultadoPor)
                if (q is UnityEngine.Object o && o == null) _ocultadoresDestruidos.Add(q);
            foreach (var q in _ocultadoresDestruidos) _ocultadoPor.Remove(q);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (_ocultadoresDestruidos.Count > 0)
                Debug.LogWarning($"[PlayerHUDV2:DIAG] {_ocultadoresDestruidos.Count} sistema(s) se destruyeron con el HUD oculto sin soltarlo. Lo tienen oculto: {QuienLoOculta}");
#endif
            return _ocultadoresDestruidos.Count > 0;
        }
        
        /// <summary>
        /// Verifica si el HUD está visible
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Olvida quién tenía pedido el HUD oculto y lo deja visible. Este componente vive en
        /// Start (DontDestroyOnLoad), así que al cargar partida o volver al menú principal puede
        /// quedar pedido por sistemas que ya no van a soltarlo. Lo llama
        /// GameBootService.ResetTransientSessionState().
        /// </summary>
        public static void ForceResetHideState()
        {
            var hud = Instance;
            if (hud == null) return;

            hud._currentFadeTween?.Kill();
            hud._ocultadoPor.Clear();
            hud._isVisible = true;

            if (hud._canvasGroup == null) hud._canvasGroup = hud.GetComponent<CanvasGroup>();
            if (hud._canvasGroup != null)
            {
                hud._canvasGroup.alpha = 1f;
                hud._canvasGroup.interactable = true;
                hud._canvasGroup.blocksRaycasts = true;
            }
        }

        #endregion
        
        #region Editor Helpers
        
        #if UNITY_EDITOR
        [ContextMenu("Force Refresh All")]
        private void EditorForceRefresh()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PlayerHUDV2] Force Refresh solo funciona en Play Mode");
                return;
            }
            
            ForceRefresh();
            // Debug.Log("[PlayerHUDV2] ✅ HUD actualizado manualmente");
        }
        
        [ContextMenu("Validate Setup")]
        private void EditorValidateSetup()
        {
            ValidateReferences();
        }
        
        [ContextMenu("Test Fill Amounts")]
        private void EditorTestFillAmounts()
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = 0.75f;
                Debug.Log("[PlayerHUDV2] Health fill set to 75%");
            }
            
            if (manaFillImage != null)
            {
                manaFillImage.fillAmount = 0.5f;
                Debug.Log("[PlayerHUDV2] Mana fill set to 50%");
            }
        }
        #endif
        
        #endregion
    }
}
