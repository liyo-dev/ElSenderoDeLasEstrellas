using UnityEngine;
using UnityEngine.UI;

/// Bandas de cine compartidas por las secuencias, creadas en el reproductor que las solicita.
public sealed class BandasDeCineUI : MonoBehaviour
{
    public static BandasDeCineUI Instance { get; private set; }
    public float AlturaVisible { get; private set; }
    public bool Visibles => AlturaVisible > 0f;
    public float AlturaObjetivo => _destino;
    public bool Animando => _animando;
    RectTransform _superior, _inferior;
    float _origen, _destino, _duracion, _tiempo;
    bool _animando;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    public static BandasDeCineUI Obtener(Transform propietario)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("BandasDeCine", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(propietario, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9995;
        return go.AddComponent<BandasDeCineUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _superior = CrearBanda("Superior", true);
        _inferior = CrearBanda("Inferior", false);
        Aplicar(0f);
    }

    RectTransform CrearBanda(string nombre, bool arriba)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        var rect = (RectTransform)go.transform;
        rect.pivot = new Vector2(0.5f, arriba ? 1f : 0f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public void Mostrar(bool mostrar, float altura = 0.12f, float duracion = 0.5f)
    {
        _origen = AlturaVisible;
        _destino = mostrar ? Mathf.Clamp(altura, 0f, 0.45f) : 0f;
        _duracion = Mathf.Max(0f, duracion);
        _tiempo = 0f;
        _animando = _duracion > 0f && !Mathf.Approximately(_origen, _destino);
        if (!_animando) Aplicar(_destino);
    }

    void Update()
    {
        if (!_animando) return;
        _tiempo += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_tiempo / _duracion);
        Aplicar(Mathf.Lerp(_origen, _destino, t * t * (3f - 2f * t)));
        if (t >= 1f) _animando = false;
    }

    void Aplicar(float altura)
    {
        AlturaVisible = altura;
        _inferior.anchorMin = Vector2.zero;
        _inferior.anchorMax = new Vector2(1f, altura);
        _superior.anchorMin = new Vector2(0f, 1f - altura);
        _superior.anchorMax = Vector2.one;
        _superior.offsetMin = _superior.offsetMax = Vector2.zero;
        _inferior.offsetMin = _inferior.offsetMax = Vector2.zero;
    }

    void OnDisable() { Mostrar(false, duracion: 0f); }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
