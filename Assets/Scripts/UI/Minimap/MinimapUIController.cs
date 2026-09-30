using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja los marcadores (<see cref="MinimapMarker"/>) sobre el minimapa.
///
/// - Marcador dentro del área visible: icono en su posición proporcional, dentro de la
///   máscara circular y por encima de la flecha del jugador.
/// - Marcador fuera del área visible: indicador de borde (icono con halo y una punta que
///   señala la dirección), en una capa propia fuera de la máscara y por encima del aro,
///   para que no quede recortado ni tapado. Es más grande que el icono interior y late
///   suavemente: se lee como «está lejos, por allí».
///
/// En el mapa grande los iconos no crecen con el mapa: mantienen el tamaño que indica
/// <see cref="MinimapController.MarkerScale"/>.
///
/// Setup: minimapRect = RawImage del minimapa; iconsParent = hijo vacío centrado dentro de
/// ella. La capa de borde se crea sola al arrancar.
/// </summary>
[DisallowMultipleComponent]
public class MinimapUIController : MonoBehaviour
{
    public static MinimapUIController Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        _pointerSprite = null;
        _haloSprite = null;
    }
#endif

    [Header("Referencias UI")]
    [Tooltip("RectTransform del círculo del minimapa (la RawImage).")]
    [SerializeField] RectTransform minimapRect;

    [Tooltip("Hijo vacío centrado dentro del minimapRect donde se crean los iconos interiores.")]
    [SerializeField] RectTransform iconsParent;

    [Header("Iconos")]
    [Tooltip("Sprite usado cuando el marcador no tiene icono asignado.")]
    [SerializeField] Sprite defaultMarkerSprite;

    [Tooltip("Lado del icono cuando el objetivo está dentro del minimapa (px).")]
    [SerializeField] float iconSize = 42f;

    [Header("Indicador de borde (objetivo lejos)")]
    [Tooltip("Lado del icono cuando el objetivo está fuera del minimapa (px). Mayor que el interior para que se vea.")]
    [SerializeField] float edgeIconSize = 50f;

    [Tooltip("Lado de la punta que señala la dirección del objetivo (px).")]
    [SerializeField] float pointerSize = 22f;

    [Tooltip("Distancia del centro del indicador al borde del círculo, hacia dentro (px). Con la mitad del icono o poco menos, el icono queda dentro y la punta asoma sobre el aro.")]
    [SerializeField] float edgeInset = 20f;

    [Tooltip("Amplitud del latido del indicador de borde (0 = sin latido).")]
    [Range(0f, 0.4f)]
    [SerializeField] float pulseAmount = 0.12f;

    [Tooltip("Latidos por segundo del indicador de borde.")]
    [SerializeField] float pulseSpeed = 1.4f;

    [Tooltip("Opacidad del halo que rodea al indicador de borde.")]
    [Range(0f, 1f)]
    [SerializeField] float haloAlpha = 0.45f;

    // ── Datos internos ───────────────────────────────────────────────────────

    class Entry
    {
        public MinimapMarker marker;
        public Image         icon;        // interior (enmascarado)
        public RectTransform edge;        // contenedor del indicador de borde (gira hacia fuera)
        public RectTransform edgeUpright; // hijo que deshace el giro para que el icono quede derecho
        public Image         edgeIcon;
        public Image         edgeHalo;
        public Image         edgePointer;
        public bool          insideShown;
        public bool          edgeShown;
    }

    readonly List<Entry> _entries = new();
    RectTransform _edgeLayer;
    float _radius;

    static Sprite _pointerSprite;
    static Sprite _haloSprite;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureEdgeLayer();

        // Iconos por encima de la flecha del jugador: la flecha no debe tapar el objetivo.
        if (iconsParent != null) iconsParent.SetAsLastSibling();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        var ctrl = MinimapController.Instance;
        if (ctrl == null) return;

        float orthoSize = ctrl.OrthoSize;
        if (orthoSize <= 0f) return;

        RefreshRadius();
        if (_edgeLayer != null && minimapRect != null)
            _edgeLayer.position = minimapRect.position;

        Vector3 center = ctrl.ViewCenter;
        float scale = ctrl.MarkerScale;
        float pulse = 1f + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);

        // El icono pasa al borde en cuanto dejaría de caber entero dentro del círculo.
        float insideLimit = Mathf.Max(0f, _radius - iconSize * 0.5f * scale);

        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.marker == null || entry.icon == null) continue;

            bool visible = entry.marker.IsVisible;
            if (!visible)
            {
                SetInside(entry, false);
                SetEdge(entry, false);
                continue;
            }

            Vector3 wp = entry.marker.WorldPosition;
            var offset = new Vector2(wp.x - center.x, wp.z - center.z) * (_radius / orthoSize);
            float dist = offset.magnitude;

            if (dist <= insideLimit)
            {
                SetEdge(entry, false);
                SetInside(entry, true);
                entry.icon.rectTransform.anchoredPosition = offset;
                entry.icon.rectTransform.localScale = Vector3.one * scale;
            }
            else
            {
                SetInside(entry, false);
                SetEdge(entry, true);

                Vector2 dir = dist > 0.0001f ? offset / dist : Vector2.up;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // +Y del contenedor = hacia fuera

                entry.edge.anchoredPosition = dir * (_radius - edgeInset * scale);
                entry.edge.localRotation = Quaternion.Euler(0f, 0f, angle);
                entry.edge.localScale = Vector3.one * (scale * pulse);
                entry.edgeUpright.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }
    }

    // ── API pública ──────────────────────────────────────────────────────────

    public void Register(MinimapMarker marker)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].marker == marker) return;

        if (iconsParent == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[MinimapUIController] iconsParent no asignado. Asígnalo en el Inspector.");
#endif
            return;
        }

        EnsureEdgeLayer();

        var entry = new Entry { marker = marker };

        entry.icon = CreateImage($"MapIcon_{marker.name}", iconsParent, iconSize);

        // Orden de dibujo: halo, punta, icono. El halo es redondo y puede girar con el contenedor;
        // el icono va en un hijo que deshace el giro para quedar derecho.
        entry.edge = CreateRect($"MapEdge_{marker.name}", _edgeLayer);
        entry.edgeHalo = CreateImage("Halo", entry.edge, edgeIconSize * 1.35f);
        entry.edgeHalo.sprite = HaloSprite;
        entry.edgePointer = CreateImage("Punta", entry.edge, pointerSize);
        entry.edgePointer.sprite = PointerSprite;
        entry.edgePointer.rectTransform.anchoredPosition =
            new Vector2(0f, edgeIconSize * 0.5f - pointerSize * 0.1f);
        entry.edgeUpright = CreateRect("Derecho", entry.edge);
        entry.edgeIcon = CreateImage("Icono", entry.edgeUpright, edgeIconSize);

        entry.icon.gameObject.SetActive(false);
        entry.edge.gameObject.SetActive(false);

        _entries.Add(entry);
        ApplyLook(entry);
    }

    public void Unregister(MinimapMarker marker)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.marker != marker) continue;
            if (e.icon != null) Destroy(e.icon.gameObject);
            if (e.edge != null) Destroy(e.edge.gameObject);
            _entries.RemoveAt(i);
        }
    }

    public void RefreshIcon(MinimapMarker marker)
    {
        // Si todavía no está registrado (SetIcon antes de Start), se registra ahora.
        Register(marker);

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].marker != marker) continue;
            ApplyLook(_entries[i]);
            break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void ApplyLook(Entry e)
    {
        if (e.icon == null) return;

        var sprite = e.marker.Icon != null ? e.marker.Icon : defaultMarkerSprite;
        var color = e.marker.MarkerColor;
        color.a = 1f;

        e.icon.sprite = sprite;
        e.icon.color = color;

        e.edgeIcon.sprite = sprite;
        e.edgeIcon.color = color;
        e.edgePointer.color = color;
        e.edgeHalo.color = new Color(color.r, color.g, color.b, haloAlpha);
    }

    static void SetInside(Entry e, bool shown)
    {
        if (e.insideShown == shown) return;
        e.insideShown = shown;
        e.icon.gameObject.SetActive(shown);
    }

    static void SetEdge(Entry e, bool shown)
    {
        if (e.edgeShown == shown) return;
        e.edgeShown = shown;
        e.edge.gameObject.SetActive(shown);
    }

    /// <summary>
    /// Capa de indicadores de borde: hermana de la máscara circular (no la recorta) y
    /// última en el orden de dibujo (queda por encima del aro del minimapa).
    /// </summary>
    void EnsureEdgeLayer()
    {
        if (_edgeLayer != null || minimapRect == null) return;

        var mask = minimapRect.GetComponentInParent<Mask>();
        var host = mask != null && mask.transform.parent != null ? mask.transform.parent : minimapRect.parent;

        _edgeLayer = CreateRect("EdgeIcons", host);
        _edgeLayer.SetAsLastSibling();
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static Image CreateImage(string name, Transform parent, float size)
    {
        var rt = CreateRect(name, parent);
        rt.sizeDelta = Vector2.one * size;
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }

    void RefreshRadius()
    {
        if (minimapRect != null)
            _radius = minimapRect.rect.width * 0.5f;
    }

    // ── Sprites generados (punta y halo) ────────────────────────────────────

    static Sprite PointerSprite => _pointerSprite != null ? _pointerSprite : (_pointerSprite = BuildPointerSprite());
    static Sprite HaloSprite    => _haloSprite    != null ? _haloSprite    : (_haloSprite    = BuildHaloSprite());

    /// <summary>Triángulo que apunta hacia +Y, relleno blanco (se tiñe) con borde oscuro.</summary>
    static Sprite BuildPointerSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];

        Vector2 a = new(size * 0.5f, size - 3f), b = new(4f, 6f), c = new(size - 4f, 6f);
        const float border = 5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = SignedDistanceTriangle(p, a, b, c); // < 0 dentro
            float outer = Mathf.Clamp01(0.5f - d);
            float inner = Mathf.Clamp01(0.5f - (d + border));
            byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(40f, 255f, inner));
            pixels[y * size + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(outer * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Círculo blanco con borde difuminado.</summary>
    static Sprite BuildHaloSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude / r;
            float alpha = Mathf.Clamp01((1f - d) / 0.45f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * alpha * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static float SignedDistanceTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(DistSegment(p, a, b), Mathf.Min(DistSegment(p, b, c), DistSegment(p, c, a)));
        bool inside = Cross(b - a, p - a) <= 0f && Cross(c - b, p - b) <= 0f && Cross(a - c, p - c) <= 0f
                   || Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;
        return inside ? -d : d;
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    static float DistSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

#if UNITY_EDITOR
    public struct DebugEntry
    {
        public MinimapMarker marker;
        public bool          hasUIIcon;
    }

    public List<DebugEntry> Editor_GetEntries()
    {
        var result = new List<DebugEntry>(_entries.Count);
        foreach (var e in _entries)
            result.Add(new DebugEntry { marker = e.marker, hasUIIcon = e.icon != null });
        return result;
    }
#endif
}
