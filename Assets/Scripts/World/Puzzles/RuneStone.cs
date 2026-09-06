using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Una piedra rúnica individual del puzle "Sello de las Piedras" (Bosque Prohibido).
/// No sabe nada de la secuencia/solución — solo expone "me han activado" (evento
/// <see cref="Activated"/>) y sabe encenderse/apagarse visualmente. Toda la lógica de
/// orden correcto/incorrecto vive en <see cref="RuneSequencePuzzle"/>, que es quien
/// escucha a todas las piedras del grupo.
///
/// Se apoya en el mismo patrón que ya usa ChestInteractable.cs: un Interactable en modo
/// por defecto (OpenDialogue) SIN ningún DialogueAsset asignado. Interactable.Interact()
/// dispara igualmente OnInteract antes de intentar abrir diálogo, y si no hay
/// DialogueAsset simplemente registra un aviso en consola y no abre nada — no hace falta
/// ningún modo especial nuevo para "botón sin diálogo" (confirmado leyendo
/// Interactable.cs, StartDialogue()).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public class RuneStone : MonoBehaviour
{
    [Tooltip("Índice de esta piedra dentro del puzle (0-based). RuneSequencePuzzle lo reasigna " +
             "automáticamente en su propio Awake() según la posición en su lista, así que no " +
             "hace falta rellenarlo a mano si la piedra cuelga de un RuneSequencePuzzle.")]
    public int stoneIndex;

    [Header("Feedback visual")]
    [Tooltip("Luz que se enciende cuando la piedra está 'activa'. Si se deja vacía, se crea una " +
             "automáticamente en Awake (punto de luz simple, sin depender de ningún shader nuevo).")]
    [SerializeField] private Light glowLight;
    [SerializeField] private Color litColor = new Color(0.55f, 0.35f, 1f); // violeta, línea de Liam/magia
    [Tooltip("Radio de la luz. Deliberadamente pequeño: solo debe leerse como un brillo " +
             "propio de la piedra, no como una luz de ambiente que ilumine el resto del claro " +
             "ni las piedras vecinas.")]
    [SerializeField] private float litRange = 1.4f;
    [SerializeField] private float litIntensity = 3f;
    [SerializeField] private float pulseScaleMultiplier = 1.08f;
    [SerializeField] private float pulseDuration = 0.22f;

    [Tooltip("Color al que cambia la PIEDRA MISMA (no solo la luz) mientras está encendida. Es " +
             "el indicador principal de qué piedra está activa — un Light por sí solo depende " +
             "demasiado de la iluminación de la escena (de día, al aire libre, apenas se nota) " +
             "y de si el material tiene emisión habilitada (el de estas piedras no la tiene). " +
             "Cambiar el color base vía MaterialPropertyBlock siempre se ve, sea cual sea la " +
             "luz ambiente, y no toca el material compartido de los demás props que usen el " +
             "mismo asset.")]
    [SerializeField] private Color litBaseColorOverride = new Color(0.85f, 0.5f, 1f);

    [Header("Audio")]
    [Tooltip("Clave de SFX (ver AudioGraphProfile) que suena al encender esta piedra. Vacío = sin sonido.")]
    [SerializeField] private string activateSfxKey = "RuneActivate";

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Interactable _interactable;
    Vector3 _baseScale;
    bool _isLit;
    Renderer[] _renderers;
    MaterialPropertyBlock _propBlock;

    /// <summary>Se dispara cada vez que el jugador interactúa con la piedra, esté o no encendida.</summary>
    public event Action<RuneStone> Activated;

    public bool IsLit => _isLit;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
        _baseScale = transform.localScale;
        _renderers = GetComponentsInChildren<Renderer>();
        _propBlock = new MaterialPropertyBlock();

        if (glowLight == null)
        {
            var lightGO = new GameObject("RuneGlow");
            lightGO.transform.SetParent(transform, false);
            glowLight = lightGO.AddComponent<Light>();
            glowLight.type = LightType.Point;
        }

        PositionGlowAboveMesh();

        glowLight.color = litColor;
        glowLight.range = litRange;
        glowLight.intensity = 0f;
        glowLight.enabled = false;
    }

    /// <summary>
    /// Coloca la luz justo encima del mesh real de la piedra, usando los bounds combinados de
    /// todos sus Renderer, en vez de un offset local fijo. Los distintos prefabs de piedra que
    /// usa ForbiddenForestPuzzleBuilder (Stone01_a01/a02, Stone02_a01) no comparten pivote ni
    /// altura — un desplazamiento fijo (p.ej. "0.6 por encima del origen") dejaba la luz bien
    /// colocada en un tipo de piedra y flotando/enterrada en los otros, según dónde cayera el
    /// pivote de cada mesh. Calculando el punto a partir de los bounds reales, la luz siempre
    /// queda apoyada sobre la piedra que se ve, sea cual sea el prefab.
    /// </summary>
    void PositionGlowAboveMesh()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            glowLight.transform.localPosition = Vector3.up * 0.4f;
            return;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        const float margin = 0.12f;
        glowLight.transform.position = new Vector3(bounds.center.x, bounds.max.y + margin, bounds.center.z);
    }

    /// <summary>Aplica el color de "encendido" a todos los Renderer de la piedra vía
    /// MaterialPropertyBlock (no clona ni modifica el material compartido — cada piedra
    /// mantiene su propio override por Renderer).</summary>
    void ApplyLitTint()
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, litBaseColorOverride);
            r.SetPropertyBlock(_propBlock);
        }
    }

    /// <summary>Quita el override de color, devolviendo cada Renderer a su material tal cual.</summary>
    void ClearLitTint()
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            _propBlock.Clear();
            r.SetPropertyBlock(_propBlock);
        }
    }

    void OnEnable()
    {
        if (_interactable == null) _interactable = GetComponent<Interactable>();
        _interactable.OnInteract.AddListener(HandleInteract);
    }

    void OnDisable()
    {
        if (_interactable != null) _interactable.OnInteract.RemoveListener(HandleInteract);
    }

    void HandleInteract(GameObject interactor)
    {
        Activated?.Invoke(this);
    }

    /// <summary>
    /// Enciende/apaga la piedra. <paramref name="playFeedback"/> a false se usa para apagados
    /// "silenciosos" (reinicio tras fallo, limpieza) donde no queremos SFX/pulso otra vez.
    /// </summary>
    public void SetLit(bool lit, bool playFeedback = true)
    {
        _isLit = lit;
        glowLight.enabled = lit;
        glowLight.intensity = lit ? litIntensity : 0f;

        if (lit) ApplyLitTint();
        else ClearLitTint();

        transform.DOKill();
        if (!lit)
        {
            transform.localScale = _baseScale;
            return;
        }

        if (playFeedback)
        {
            transform.DOScale(_baseScale * pulseScaleMultiplier, pulseDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() => transform.DOScale(_baseScale, pulseDuration));

            if (!string.IsNullOrEmpty(activateSfxKey))
                AudioService.Instance?.PlaySFX(activateSfxKey, 1f, transform.position);
        }
    }
}
