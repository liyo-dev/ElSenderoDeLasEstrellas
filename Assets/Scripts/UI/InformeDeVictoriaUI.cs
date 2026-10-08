using System.Collections;
using Core.InputGlyphs;
using Sendero.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Slot = PartyControlManager.CharacterSlot;

/// El informe de fin de batalla: «¡Victoria!», el nombre del encuentro, los retratos de quien sale
/// en la foto, una fila por estadística que ha subido (la barra se llena del valor de antes al
/// nuevo y salta la pastilla dorada) y el botín, si lo hay. Se cierra con el botón de confirmar;
/// mientras está en pantalla el HUD se oculta. Lo abre PasoInformeDeBatalla. Ver INC-543.
///
/// El prefab (Resources/UI/InformeDeVictoria) lo construye el menú
/// «El Sendero/UI/Crear o regenerar el informe de victoria»; este componente solo lo rellena y
/// lo anima.
public sealed class InformeDeVictoriaUI : MonoBehaviour
{
    public const string RutaEnResources = "UI/InformeDeVictoria";

    [System.Serializable]
    public sealed class Fila
    {
        public TipoDeEstadistica tipo;
        public RectTransform raiz;
        public TMP_Text nombre;
        public TMP_Text valores;
        public Image barraBase;
        public Image barraGanada;
        public RectTransform pastilla;
        public TMP_Text textoPastilla;
        [Tooltip("Valor que llena la barra entera. Si el valor nuevo lo pasa, la barra toma como tope un poco más que él.")]
        public float techo = 100f;
    }

    [System.Serializable]
    public sealed class Hueco
    {
        public RectTransform raiz;
        public Image icono;
        public TMP_Text cantidad;
        public TMP_Text nombre;
    }

    [Header("Piezas")]
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private TMP_Text subtitulo;
    [SerializeField] private TMP_Text separadorEstadisticas;
    [SerializeField] private Image retrato;
    [SerializeField] private Image[] companeros;
    [SerializeField] private Fila[] filas;
    [SerializeField] private RectTransform bloqueBotin;
    [SerializeField] private TMP_Text separadorBotin;
    [SerializeField] private Hueco[] huecos;
    [SerializeField] private TMP_Text continuar;

    [Header("Medidas del panel")]
    [Tooltip("Distancia del borde de arriba del panel a la primera fila.")]
    [SerializeField] private float primeraFila = 192f;
    [SerializeField] private float pasoDeFila = 94f;
    [SerializeField] private float altoDelBotin = 200f;
    [Tooltip("Lo que queda debajo de la última fila o del botín: el «Continuar» y el margen.")]
    [SerializeField] private float altoDelPie = 84f;

    [Header("Animación (tiempo real)")]
    [SerializeField] private float duracionEntrada = 0.35f;
    [SerializeField] private float duracionSalida = 0.25f;
    [SerializeField] private float desplazamientoEntrada = 120f;
    [SerializeField] private float esperaAntesDeLasBarras = 0.25f;
    [SerializeField] private float escalonadoEntreFilas = 0.18f;
    [SerializeField] private float duracionBarra = 0.55f;
    [Tooltip("Tiempo al abrirse en el que el botón no hace nada, para que la pulsación que venía de antes no lo cierre.")]
    [SerializeField] private float graciaDeEntrada = 0.6f;

    private static InformeDeVictoriaUI _instancia;
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instancia = null;
#endif

    /// El informe, creado la primera vez desde su prefab. Null si el prefab no existe (falta
    /// ejecutar el menú que lo crea).
    public static InformeDeVictoriaUI Obtener()
    {
        if (_instancia != null) return _instancia;
        var prefab = Resources.Load<InformeDeVictoriaUI>(RutaEnResources);
        if (prefab == null) return null;
        _instancia = Instantiate(prefab);
        _instancia.name = prefab.name;
        DontDestroyOnLoad(_instancia.gameObject);
        _instancia.gameObject.SetActive(false);
        return _instancia;
    }

    private Vector2 _posicionDelPanel;
    private bool _animando;
    private bool _saltarAnimacion;
    private readonly SubidaDeEstadistica?[] _subidaDeCadaFila = new SubidaDeEstadistica?[8];

    private void Awake()
    {
        if (panel != null) _posicionDelPanel = panel.anchoredPosition;
        BrilloDelTitulo();
    }

    private void OnDisable()
    {
        // Si se apaga a medias (cambio de escena), el HUD no se queda escondido.
        OcultarHud(false);
    }

    private bool _hudOculto;

    /// El HUD y el minimapa se quitan mientras está el informe: ocupan el mismo lado de la pantalla.
    private void OcultarHud(bool ocultar)
    {
        if (_hudOculto == ocultar) return;
        _hudOculto = ocultar;
        if (PlayerHUDV2.Instance != null)
        {
            if (ocultar) PlayerHUDV2.Instance.HideHUD(this);
            else PlayerHUDV2.Instance.ShowHUD(this);
        }
        if (MinimapController.Instance != null) MinimapController.Instance.SetHiddenByCinematic(ocultar);
    }

    /// Enseña el informe y espera a que el jugador lo cierre.
    public IEnumerator Mostrar(ResultadoDeBatalla resultado)
    {
        gameObject.SetActive(true);
        Rellenar(resultado);
        OcultarHud(true);

        _saltarAnimacion = false;
        PrepararAnimacion();
        yield return Co_Entrar();

        var animacion = StartCoroutine(Co_AnimarContenido());
        float puedeCerrar = Time.unscaledTime + graciaDeEntrada;
        float t0 = Time.unscaledTime;
        while (true)
        {
            if (Time.unscaledTime >= puedeCerrar && Pulsado())
            {
                // Primera pulsación con la animación en marcha: la termina de golpe.
                if (_animando) { _saltarAnimacion = true; puedeCerrar = Time.unscaledTime + 0.25f; }
                else break;
            }
            if (continuar != null) continuar.alpha = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin((Time.unscaledTime - t0) * 2.4f));
            yield return null;
        }
        if (animacion != null) StopCoroutine(animacion);

        // Que la misma pulsación no haga saltar al personaje al devolverle el control.
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);

        yield return Co_Salir();
        OcultarHud(false);
        gameObject.SetActive(false);
    }

    // ── Contenido ─────────────────────────────────────────────────────────────────

    private void Rellenar(ResultadoDeBatalla r)
    {
        if (titulo != null) titulo.text = Texto("BATTLE_REPORT_TITLE", "¡Victoria!");
        if (separadorEstadisticas != null) separadorEstadisticas.text = Texto("BATTLE_REPORT_STATS", "Estadísticas");
        if (separadorBotin != null) separadorBotin.text = Texto("BATTLE_REPORT_LOOT", "Botín");
        if (continuar != null)
        {
            InputGlyphService.UsarIconos(continuar);
            continuar.text = $"{InputGlyphService.SpriteTag(InputGlyphNames.South)} {Texto("BATTLE_REPORT_CONTINUE", "Continuar")}";
        }

        string encuentro = r.NombreDelEncuentro;
        if (subtitulo != null)
        {
            subtitulo.gameObject.SetActive(!string.IsNullOrEmpty(encuentro));
            subtitulo.text = encuentro;
        }

        RellenarRetratos(r);

        int visibles = 0;
        for (int i = 0; i < filas.Length; i++)
        {
            var fila = filas[i];
            _subidaDeCadaFila[i] = null;
            for (int s = 0; s < r.Subidas.Count; s++)
                if (r.Subidas[s].tipo == fila.tipo) { _subidaDeCadaFila[i] = r.Subidas[s]; break; }

            bool hay = _subidaDeCadaFila[i].HasValue;
            fila.raiz.gameObject.SetActive(hay);
            if (!hay) continue;

            var sub = _subidaDeCadaFila[i].Value;
            fila.nombre.text = TextoDeEstadisticas.Nombre(fila.tipo);
            float sube = sub.despues - sub.antes;
            fila.textoPastilla.text = (sube >= 0f ? "+" : "-") + Mathf.Abs(sube).ToString("0");
            fila.raiz.anchoredPosition = new Vector2(fila.raiz.anchoredPosition.x, -(primeraFila + visibles * pasoDeFila));
            visibles++;
        }

        int botin = 0;
        for (int i = 0; i < huecos.Length; i++)
        {
            bool hay = i < r.Botin.Count;
            huecos[i].raiz.gameObject.SetActive(hay);
            if (!hay) continue;
            var premio = r.Botin[i];
            huecos[i].icono.sprite = premio.icono;
            huecos[i].icono.enabled = premio.icono != null;
            huecos[i].cantidad.text = "x" + premio.cantidad;
            huecos[i].nombre.text = premio.nombre;
            botin++;
        }
        if (bloqueBotin != null)
        {
            bloqueBotin.gameObject.SetActive(botin > 0);
            bloqueBotin.anchoredPosition = new Vector2(bloqueBotin.anchoredPosition.x, -(primeraFila + visibles * pasoDeFila));
        }

        if (panel != null)
        {
            float alto = primeraFila + visibles * pasoDeFila + (botin > 0 ? altoDelBotin : 0f) + altoDelPie;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, alto);
        }
    }

    /// El personaje al mando en grande y los compañeros de la foto en pequeño.
    private void RellenarRetratos(ResultadoDeBatalla r)
    {
        var foto = r.EnLaFoto;
        Slot principal = foto.Count > 0 ? foto[0]
            : PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;
        if (retrato != null)
        {
            var ficha = FichaDePersonaje.Buscar(principal);
            retrato.sprite = ficha != null ? ficha.Retrato : null;
            retrato.enabled = retrato.sprite != null;
        }

        for (int i = 0; i < companeros.Length; i++)
        {
            var ficha = i + 1 < foto.Count ? FichaDePersonaje.Buscar(foto[i + 1]) : null;
            bool hay = ficha != null && ficha.Retrato != null;
            // El retrato va dentro de su aro: se apaga el aro entero.
            companeros[i].transform.parent.gameObject.SetActive(hay);
            if (hay) companeros[i].sprite = ficha.Retrato;
        }
    }

    // ── Animación ────────────────────────────────────────────────────────────────

    private void PrepararAnimacion()
    {
        for (int i = 0; i < filas.Length; i++)
        {
            if (!_subidaDeCadaFila[i].HasValue) continue;
            var sub = _subidaDeCadaFila[i].Value;
            PonerFila(filas[i], sub, sub.antes);
            filas[i].pastilla.localScale = Vector3.zero;
        }
        for (int i = 0; i < huecos.Length; i++) huecos[i].raiz.localScale = Vector3.zero;
    }

    private IEnumerator Co_AnimarContenido()
    {
        _animando = true;
        yield return Esperar(esperaAntesDeLasBarras);

        for (int i = 0; i < filas.Length; i++)
        {
            if (!_subidaDeCadaFila[i].HasValue) continue;
            StartCoroutine(Co_AnimarFila(filas[i], _subidaDeCadaFila[i].Value));
            yield return Esperar(escalonadoEntreFilas);
        }
        yield return Esperar(duracionBarra + 0.25f);

        for (int i = 0; i < huecos.Length; i++)
        {
            if (!huecos[i].raiz.gameObject.activeSelf) continue;
            StartCoroutine(Co_Saltar(huecos[i].raiz));
            yield return Esperar(0.12f);
        }
        yield return Esperar(0.3f);

        TerminarDeGolpe();
        _animando = false;
    }

    private IEnumerator Co_AnimarFila(Fila fila, SubidaDeEstadistica sub)
    {
        float t = 0f;
        while (t < 1f && !_saltarAnimacion)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.05f, duracionBarra));
            float k = 1f - (1f - t) * (1f - t);
            PonerFila(fila, sub, Mathf.Lerp(sub.antes, sub.despues, k));
            yield return null;
        }
        PonerFila(fila, sub, sub.despues);
        yield return Co_Saltar(fila.pastilla);
    }

    /// Aparece con un pequeño rebote (0 → 1,15 → 1).
    private IEnumerator Co_Saltar(RectTransform rt)
    {
        float t = 0f;
        while (t < 1f && !_saltarAnimacion)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.25f);
            float e = t < 0.6f ? Mathf.Lerp(0f, 1.15f, t / 0.6f) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * e;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private void TerminarDeGolpe()
    {
        for (int i = 0; i < filas.Length; i++)
        {
            if (!_subidaDeCadaFila[i].HasValue) continue;
            PonerFila(filas[i], _subidaDeCadaFila[i].Value, _subidaDeCadaFila[i].Value.despues);
            filas[i].pastilla.localScale = Vector3.one;
        }
        for (int i = 0; i < huecos.Length; i++) huecos[i].raiz.localScale = Vector3.one;
    }

    /// Pone la fila con el valor nuevo ya contado hasta 'actual': el tramo de antes en la barra
    /// base y lo ganado hasta ahora en la barra que brilla.
    private static void PonerFila(Fila fila, SubidaDeEstadistica sub, float actual)
    {
        float tope = Mathf.Max(fila.techo, sub.despues * 1.15f, 1f);
        fila.barraBase.fillAmount = Mathf.Clamp01(Mathf.Min(sub.antes, actual) / tope);
        fila.barraGanada.fillAmount = Mathf.Clamp01(actual / tope);
        fila.valores.text = $"<color=#B9B0D8>{sub.antes:0}</color> <color=#CBB9FF>»</color> {actual:0}";
    }

    private IEnumerator Co_Entrar()
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.05f, duracionEntrada));
            float k = 1f - Mathf.Pow(1f - t, 3f);
            if (grupo != null) grupo.alpha = k;
            if (panel != null) panel.anchoredPosition = _posicionDelPanel + new Vector2(desplazamientoEntrada * (1f - k), 0f);
            yield return null;
        }
    }

    private IEnumerator Co_Salir()
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.05f, duracionSalida));
            float k = t * t;
            if (grupo != null) grupo.alpha = 1f - k;
            if (panel != null) panel.anchoredPosition = _posicionDelPanel + new Vector2(desplazamientoEntrada * k, 0f);
            yield return null;
        }
        if (panel != null) panel.anchoredPosition = _posicionDelPanel;
    }

    private IEnumerator Esperar(float segundos)
    {
        float fin = Time.unscaledTime + segundos;
        while (Time.unscaledTime < fin && !_saltarAnimacion) yield return null;
    }

    // ── Utilidades ───────────────────────────────────────────────────────────────

    private static bool Pulsado()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        var raton = Mouse.current;
        return raton != null && raton.leftButton.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
    }

    /// Resplandor lavanda detrás de las letras del título (underlay del material de TMP).
    private void BrilloDelTitulo()
    {
        if (titulo == null) return;
        var m = titulo.fontMaterial;
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(PaletaUI.Lavanda.r, PaletaUI.Lavanda.g, PaletaUI.Lavanda.b, 0.9f));
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.35f);
    }

    private static string Texto(string clave, string porDefecto) =>
        LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, porDefecto) : porDefecto;

}
