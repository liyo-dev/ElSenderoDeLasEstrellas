﻿using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.NPC;

namespace Game.UI
{
    /// <summary>
    /// Script simple que va en el prefab de la barra de vida.
    /// Se auto-configura y actualiza automáticamente cuando el NPC recibe daño.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class NPCHealthBarUI : MonoBehaviour
    {
        [Header("Referencias Automáticas")]
        [Tooltip("Se busca automáticamente si no se asigna")]
        [SerializeField] private Image fillImage;
        
        [Tooltip("Opcional - se busca automáticamente si no se asigna")]
        [SerializeField] private TextMeshProUGUI healthText;
        
        [Header("Configuración")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private float hideDelay = 3f;
        
        [Header("Animación")]
        [SerializeField] private float lerpSpeed = 8f;
        [Tooltip("Si está activado, la barra hace lerp suave al recibir daño")]
        [SerializeField] private bool smoothTransition = true;
        
        [Header("Colores")]
        [SerializeField] private Color healthyColor = new Color(0f, 1f, 0f);
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float warningThreshold = 0.5f;
        [SerializeField] private float criticalThreshold = 0.25f;

        [Tooltip("Viste la barra con el arte de papel del juego (marco de tinta y relleno con los colores de la paleta). " +
                 "Además, un relleno sin sprite no se recorta (Unity lo pinta entero): por eso la barra cambiaba de color pero no bajaba. Ver INC-1161.")]
        [SerializeField] private bool vestirDePapel = true;

        // Radio de las esquinas del marco, en fracción del alto de la barra.
        private const float RadioDeEsquina = 0.35f;
        // Radio en píxeles de la esquina dibujada en PiezasDeUI.RectanguloRedondeado.
        private const float RadioDelSpriteEnPixeles = 30f;

        private static Sprite s_blanco;
        
        private Canvas _canvas;
        private Vector3 _escalaDelPrefab = Vector3.one;
        private bool _marcasPuestas;
        private Damageable _npcDamageable;
        private Transform _npcTransform;
        private float _hideTimer;
        private float _currentFillAmount;
        private float _targetFillAmount;
        
        private void Awake()
        {
            // Obtener canvas
            _canvas = GetComponent<Canvas>();
            _escalaDelPrefab = transform.localScale;
            
            // Auto-buscar fillImage si no está asignado
            if (fillImage == null)
            {
                fillImage = GetComponentInChildren<Image>();
            }
            
            if (vestirDePapel) VestirDePapel();

            // Auto-buscar healthText si no está asignado (opcional)
            if (healthText == null)
            {
                healthText = GetComponentInChildren<TextMeshProUGUI>();
            }
            
            // Asegurar que tiene BillboardUI para mirar a la cámara
            if (GetComponent<BillboardUI>() == null)
            {
                gameObject.AddComponent<BillboardUI>();
            }
            
            // Ocultar al inicio
            if (_canvas != null)
                _canvas.enabled = false;
        }
        
        private void Start()
        {
            // Buscar el NPCBehaviourManagerV2 en el padre
            _npcTransform = transform.parent;
            if (_npcTransform != null)
            {
                // Intentar obtener Damageable desde el NPCBehaviourManagerV2
                var behaviourManager = _npcTransform.GetComponent<NPCBehaviourManagerV2>();
                if (behaviourManager != null)
                {
                    _npcDamageable = behaviourManager.GetComponent<Damageable>();
                    
                    if (_npcDamageable != null)
                    {
                        // Suscribirse a eventos
                        _npcDamageable.OnDamaged += OnNPCDamaged;
                        _npcDamageable.OnDied += OnNPCDied;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                        Debug.Log($"[NPCHealthBarUI] Conectado a Damageable de {_npcTransform.name}");
#endif
                    }
                    else
                    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                        Debug.LogWarning($"[NPCHealthBarUI] No se encontró Damageable en {_npcTransform.name}. Asegúrate de que el NPC está en combate.");
#endif
                    }
                }
                else
                {
                    // Fallback: buscar Damageable directamente en el padre
                    _npcDamageable = _npcTransform.GetComponent<Damageable>();
                    if (_npcDamageable != null)
                    {
                        _npcDamageable.OnDamaged += OnNPCDamaged;
                        _npcDamageable.OnDied += OnNPCDied;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                        Debug.Log($"[NPCHealthBarUI] Conectado a Damageable (fallback) de {_npcTransform.name}");
#endif
                    }
                    else
                    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                        Debug.LogWarning($"[NPCHealthBarUI] No se encontró NPCBehaviourManagerV2 ni Damageable en {_npcTransform.name}");
#endif
                    }
                }
            }
            
            // Aplicar offset local
            transform.localPosition = offset;
            CompensarEscalaDelPadre();
            
            // Inicializar fill amounts
            _currentFillAmount = 1f;
            _targetFillAmount = 1f;
            if (fillImage != null)
            {
                fillImage.fillAmount = 1f;
            }
        }
        
        /// Marco de tinta redondeado y relleno de color plano, con los tonos del juego.
        private void VestirDePapel()
        {
            if (fillImage == null) return;
            if (s_blanco == null)
            {
                var tex = Texture2D.whiteTexture;
                s_blanco = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                s_blanco.name = "BarraBlanca";
            }
            fillImage.sprite = s_blanco;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            healthyColor = new Color(0.48f, 0.78f, 0.42f);   // verde hoja
            warningColor = new Color(0.95f, 0.70f, 0.24f);   // ámbar
            criticalColor = new Color(0.91f, 0.38f, 0.30f);  // coral
            fillImage.color = healthyColor;

            // El marco: la otra imagen de la barra (el fondo), en tinta y con esquinas redondas.
            var rtRelleno = fillImage.rectTransform;
            foreach (var imagen in GetComponentsInChildren<Image>(true))
            {
                if (imagen == fillImage) continue;
                imagen.sprite = PiezasDeUI.RectanguloRedondeado;
                imagen.type = Image.Type.Sliced;
                imagen.color = PaletaUI.Tinta;
                float alto = imagen.rectTransform.rect.height > 0f ? imagen.rectTransform.rect.height : 0.2f;
                imagen.pixelsPerUnitMultiplier = MultiplicadorParaEsquinas(imagen.sprite, _canvas, alto * RadioDeEsquina);
                // El relleno, metido dentro del marco.
                var marco = imagen.rectTransform;
                rtRelleno.anchorMin = marco.anchorMin; rtRelleno.anchorMax = marco.anchorMax; rtRelleno.pivot = marco.pivot;
                rtRelleno.anchoredPosition = marco.anchoredPosition;
                rtRelleno.sizeDelta = marco.sizeDelta - new Vector2(alto * 0.3f, alto * 0.3f);
                break;
            }
        }

        /// <summary>
        /// Multiplicador de píxeles por unidad para que la esquina del sprite troceado mida 'radio'
        /// unidades del lienzo. Si el borde del sprite no cabe en la barra, Unity lo encoge en
        /// proporción al tamaño y la esquina sale estirada (un óvalo a lo ancho).
        /// </summary>
        private static float MultiplicadorParaEsquinas(Sprite sprite, Canvas lienzo, float radio)
        {
            if (sprite == null || radio <= 0f) return 1f;
            float referencia = lienzo != null && lienzo.referencePixelsPerUnit > 0f ? lienzo.referencePixelsPerUnit : 100f;
            float ppuDelSprite = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
            // Unidades de lienzo de un píxel del sprite = referencia / (ppuDelSprite * multiplicador).
            return Mathf.Max(0.01f, RadioDelSpriteEnPixeles * referencia / (ppuDelSprite * radio));
        }

        /// <summary>
        /// La barra cuelga del NPC: si su raíz tiene escala (talla, un modelo escalado a mano, una
        /// escala desigual), la barra la heredaría y saldría encogida o estirada. Se deshace para que
        /// se vea igual sobre cualquier personaje.
        /// </summary>
        private void CompensarEscalaDelPadre()
        {
            var padre = transform.parent;
            if (padre == null) return;
            Vector3 escala = padre.lossyScale;
            if (Mathf.Abs(escala.x) < 0.0001f || Mathf.Abs(escala.y) < 0.0001f || Mathf.Abs(escala.z) < 0.0001f) return;
            Vector3 propia = transform.localScale;
            // La escala propia del prefab manda; lo heredado se divide.
            float x = _escalaDelPrefab.x / escala.x, y = _escalaDelPrefab.y / escala.y, z = _escalaDelPrefab.z / escala.z;
            if (Mathf.Abs(propia.x - x) > 0.0001f || Mathf.Abs(propia.y - y) > 0.0001f || Mathf.Abs(propia.z - z) > 0.0001f)
                transform.localScale = new Vector3(x, y, z);
        }

        private void OnDestroy()
        {
            // Desuscribirse de eventos
            if (_npcDamageable != null)
            {
                _npcDamageable.OnDamaged -= OnNPCDamaged;
                _npcDamageable.OnDied -= OnNPCDied;
            }
        }
        
        private void Update()
        {
            // Lerp suave de la barra de vida
            if (smoothTransition && fillImage != null && Mathf.Abs(_currentFillAmount - _targetFillAmount) > 0.001f)
            {
                _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount, Time.deltaTime * lerpSpeed);
                fillImage.fillAmount = _currentFillAmount;
            }
            
            // Timer para ocultar automáticamente
            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f && hideWhenFull)
                {
                    float healthPercent = _npcDamageable != null ? (_npcDamageable.Current / _npcDamageable.Max) : 0f;
                    if (healthPercent >= 0.99f)
                    {
                        Hide();
                    }
                }
            }
        }
        
        /// <summary>
        /// Llamado cuando el NPC recibe daño
        /// </summary>
        private void OnNPCDamaged(float amount)
        {
            Show();
            UpdateBar();
            ResetHideTimer();
        }
        
        /// <summary>
        /// Llamado cuando el NPC muere
        /// </summary>
        private void OnNPCDied()
        {
            UpdateBar();
            // Ocultar después de un delay
            Invoke(nameof(Hide), 2f);
        }
        
        /// <summary>
        /// Actualiza la barra con el estado actual de salud
        /// </summary>
        private void UpdateBar()
        {
            if (_npcDamageable == null || fillImage == null)
                return;
            
            float healthPercent = _npcDamageable.Current / _npcDamageable.Max;
            
            // Actualizar fill amount (con o sin animación)
            _targetFillAmount = Mathf.Clamp01(healthPercent);
            
            if (!smoothTransition)
            {
                // Actualización instantánea
                _currentFillAmount = _targetFillAmount;
                fillImage.fillAmount = _currentFillAmount;
            }
            // Si smoothTransition está activo, el Update() hará el lerp automáticamente
            
            // Cambiar color según salud
            if (healthPercent <= criticalThreshold)
                fillImage.color = criticalColor;
            else if (healthPercent <= warningThreshold)
                fillImage.color = warningColor;
            else
                fillImage.color = healthyColor;
            
            // Actualizar texto si existe
            if (healthText != null)
            {
                healthText.text = $"{Mathf.Ceil(_npcDamageable.Current)} / {_npcDamageable.Max}";
            }
        }
        
        /// <summary>
        /// Muestra la barra de vida
        /// </summary>
        public void Show()
        {
            if (_canvas != null && !_canvas.enabled)
            {
                _canvas.enabled = true;
            }
            PonerMarcasDeFase();
        }

        /// <summary>
        /// Si el NPC pelea por fases (<see cref="IJefeConFases"/>), una raya de tinta en la barra
        /// marca dónde empieza cada una. Se pone una vez, al enseñarse la barra (las fases se conocen
        /// cuando ya ha empezado el combate).
        /// </summary>
        private void PonerMarcasDeFase()
        {
            if (_marcasPuestas || fillImage == null || _npcTransform == null) return;
            var fases = _npcTransform.GetComponent<IJefeConFases>();
            if (fases == null || fases.UmbralesDeFase == null || fases.UmbralesDeFase.Count == 0) return;
            _marcasPuestas = true;
            var relleno = fillImage.rectTransform;
            float alto = relleno.rect.height > 0f ? relleno.rect.height : 0.2f;
            for (int i = 0; i < fases.UmbralesDeFase.Count; i++)
            {
                var marca = new GameObject("MarcaDeFase", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)marca.transform;
                rt.SetParent(relleno.parent, false);
                rt.SetSiblingIndex(relleno.GetSiblingIndex() + 1);
                rt.anchorMin = relleno.anchorMin; rt.anchorMax = relleno.anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
                float x = (fases.UmbralesDeFase[i] - relleno.pivot.x) * relleno.rect.width;
                rt.anchoredPosition = relleno.anchoredPosition + new Vector2(x, 0f);
                rt.sizeDelta = new Vector2(alto * 0.18f, alto * 1.25f);
                var img = marca.GetComponent<Image>();
                img.color = PaletaUI.Tinta;
                img.raycastTarget = false;
                if (fillImage.material != null) img.material = fillImage.material;
            }
        }
        
        /// <summary>
        /// Oculta la barra de vida
        /// </summary>
        public void Hide()
        {
            if (_canvas != null && _canvas.enabled)
            {
                _canvas.enabled = false;
            }
        }
        
        /// <summary>
        /// Resetea el timer para ocultar
        /// </summary>
        private void ResetHideTimer()
        {
            _hideTimer = hideDelay;
        }
        
        #if UNITY_EDITOR
        // Preview en el editor
        private void OnValidate()
        {
            if (!Application.isPlaying && fillImage != null)
            {
                fillImage.fillAmount = 0.75f; // Vista previa al 75%
            }
        }
        #endif
    }
}

