using UnityEngine;
using UnityEngine.UI;

/// Rayas radiales reutilizables para acentuar la concentración.
public sealed class LineasDeConcentracionUI : MonoBehaviour
{
    public static LineasDeConcentracionUI Instance { get; private set; }
    public bool Ocupado => _visible || _animando;
    RawImage _imagen;
    Texture2D _textura;
    Material _material;
    static readonly int HuecoCentralId = Shader.PropertyToID("_HuecoCentral");
    public bool Animando => _animando;
    float _alpha, _origen, _destino, _fundido, _tiempo, _restante, _vibracion;
    bool _visible, _animando, _grande;
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif
    public static LineasDeConcentracionUI Obtener(Transform propietario)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("LineasDeConcentracion", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(propietario, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9994;
        return go.AddComponent<LineasDeConcentracionUI>();
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        var go = new GameObject("Rayas", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        _imagen = go.GetComponent<RawImage>();
        _imagen.raycastTarget = false;
        _imagen.rectTransform.anchorMin = _imagen.rectTransform.anchorMax = new Vector2(.5f, .5f);
        // Se rasterizan una sola vez cuñas con punta hacia el centro.
        const int tamano = 1024, cantidad = 90;
        var pixeles = new Color32[tamano * tamano];
        var aleatorio = new System.Random(568);
        var inicios = new float[cantidad];
        var grosores = new float[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            inicios[i] = .35f + (float)aleatorio.NextDouble() * .28f;
            grosores[i] = .08f + (float)aleatorio.NextDouble() * .22f;
        }
        for (int y = 0; y < tamano; y++)
        for (int x = 0; x < tamano; x++)
        {
            float dx = (x + .5f - tamano / 2f) / (tamano / 2f);
            float dy = (y + .5f - tamano / 2f) / (tamano / 2f);
            float radio = Mathf.Sqrt(dx * dx + dy * dy);
            float sector = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 1f) * cantidad;
            int indice = Mathf.FloorToInt(sector) % cantidad;
            float ancho = grosores[indice] * Mathf.Clamp01((radio - inicios[indice]) / .4f);
            if (Mathf.Abs(sector - Mathf.Floor(sector) - .5f) < ancho)
                pixeles[y * tamano + x] = new Color32(255, 255, 255, 255);
        }
        _textura = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false);
        _textura.wrapMode = TextureWrapMode.Clamp;
        _textura.SetPixels32(pixeles);
        _textura.Apply(false, true);
        _imagen.texture = _textura;
        _material = new Material(Resources.Load<Shader>("LineasDeConcentracionUI"));
        _imagen.material = _material;
        OcultarInmediato();
    }
    public void Mostrar(bool mostrar, Color color, float huecoCentral, float fundido, float duracion)
    {
        _origen = _alpha;
        _destino = mostrar ? Mathf.Clamp01(color.a) : 0f;
        _fundido = Mathf.Max(0f, fundido);
        _tiempo = 0f;
        _restante = mostrar && duracion > 0f ? duracion : -1f;
        _visible = mostrar;
        if (mostrar)
        {
            _imagen.color = new Color(color.r, color.g, color.b, _alpha);
            // El material adapta el radio sin regenerar la textura cacheada.
            _material.SetFloat(HuecoCentralId, Mathf.Clamp01(huecoCentral));
        }
        _animando = _fundido > 0f;
        if (!_animando) Aplicar(_destino);
    }
    void Update()
    {
        if (!Ocupado) return;
        float dt = Time.unscaledDeltaTime;
        float lado = Mathf.Sqrt(Screen.width * (float)Screen.width + Screen.height * (float)Screen.height);
        _imagen.rectTransform.sizeDelta = new Vector2(lado, lado);
        if (_animando)
        {
            _tiempo += dt;
            float t = Mathf.Clamp01(_tiempo / _fundido);
            Aplicar(Mathf.Lerp(_origen, _destino, t));
            if (t >= 1f) _animando = false;
        }
        if (_visible && _restante >= 0f)
        {
            _restante -= dt;
            if (_restante <= 0f) Mostrar(false, _imagen.color, .35f, _fundido, 0f);
        }
        _vibracion += dt;
        if (_vibracion >= .05f)
        {
            _vibracion %= .05f;
            _imagen.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-180f, 180f));
            _grande = !_grande;
            _imagen.rectTransform.localScale = Vector3.one * (_grande ? 1.04f : 1f);
        }
    }
    void Aplicar(float alpha)
    {
        _alpha = alpha;
        var color = _imagen.color;
        color.a = alpha;
        _imagen.color = color;
        _imagen.enabled = alpha > 0f;
    }
    public void OcultarInmediato()
    {
        _visible = _animando = false;
        if (_imagen != null) Aplicar(0f);
    }
    void OnDisable() { OcultarInmediato(); }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_textura != null) Destroy(_textura);
        if (_material != null) Destroy(_material);
    }
}
