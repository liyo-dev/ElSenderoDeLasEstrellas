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

/// El informe de fin de batalla: «¡Victoria!», el nombre del encuentro, los retratos vivos de quien
/// sale en la foto (los modelos reales, filmados por el EstudioDeRetratos; si no está, el retrato
/// fijo de su ficha), la Sintonía Estelar ganada (la barra se llena hasta el próximo hito y, si se
/// completa, avisa de que se puede cambiar por más vida o más magia) y el botín, si lo hay. Se
/// cierra con el botón de confirmar; mientras está en pantalla el HUD se oculta. Lo abre
/// PasoInformeDeBatalla. Ver INC-543 e INC-950.
///
/// El prefab (Resources/UI/InformeDeVictoria) lo construye el menú
/// «El Sendero/UI/Crear o regenerar el informe de victoria»; este componente solo lo rellena y
/// lo anima.
public sealed class InformeDeVictoriaUI : MonoBehaviour
{
    public const string RutaEnResources = "UI/InformeDeVictoria";

    [System.Serializable]
    public sealed class FilaDeSintonia
    {
        public RectTransform raiz;
        public TMP_Text nombre;
        public TMP_Text valores;
        public Image barraBase;
        public Image barraGanada;
        public RectTransform pastilla;
        public TMP_Text textoPastilla;
        [Tooltip("Línea bajo la barra que sale cuando se completa un hito.")]
        public TMP_Text aviso;
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
    [SerializeField] private TMP_Text separadorProgreso;
    [Tooltip("Retrato fijo de la ficha: solo si no hay retrato vivo.")]
    [SerializeField] private Image retrato;
    [SerializeField] private RawImage retratoVivo;
    [SerializeField] private Image[] companeros;
    [SerializeField] private RawImage[] companerosVivos;
    [SerializeField] private FilaDeSintonia sintonia;
    [SerializeField] private RectTransform bloqueBotin;
    [SerializeField] private TMP_Text separadorBotin;
    [SerializeField] private Hueco[] huecos;
    [SerializeField] private TMP_Text continuar;

    [Header("Medidas del panel")]
    [Tooltip("Distancia del borde de arriba del panel a la fila de Sintonía.")]
    [SerializeField] private float primeraFila = 192f;
    [SerializeField] private float altoDeFila = 94f;
    [Tooltip("Lo que crece la fila de Sintonía cuando sale el aviso de hito.")]
    [SerializeField] private float altoDelAviso = 40f;
    [SerializeField] private float altoDelBotin = 200f;
    [Tooltip("Distancia entre los centros de dos huecos del botín; los que se ven quedan centrados en el bloque.")]
    [SerializeField] private float pasoDeHueco = 150f;
    [Tooltip("Lo que queda debajo de la última fila o del botín: el «Continuar» y el margen.")]
    [SerializeField] private float altoDelPie = 84f;

    [Header("Animación (tiempo real)")]
    [SerializeField] private float duracionEntrada = 0.35f;
    [SerializeField] private float duracionSalida = 0.25f;
    [SerializeField] private float desplazamientoEntrada = 120f;
    [SerializeField] private float esperaAntesDeLasBarras = 0.25f;
    [SerializeField] private float duracionBarra = 0.8f;
    [Tooltip("Tiempo al abrirse en el que el botón no hace nada, para que la pulsación que venía de antes no lo cierre.")]
    [SerializeField] private float graciaDeEntrada = 0.6f;

    private static readonly int IdTexturaSobreFondo = Shader.PropertyToID("_TexturaSobreFondo");
    private static readonly int IdUsarTexturaSobreFondo = Shader.PropertyToID("_UsarTexturaSobreFondo");

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
    private SintoniaGanada? _sintonia;
    private readonly Slot?[] _vivos = new Slot?[3];
    private Material[] _materialesVivos;

    private void Awake()
    {
        if (panel != null) _posicionDelPanel = panel.anchoredPosition;
        BrilloDelTitulo();
        PrepararMaterialesVivos();
    }

    private void OnDisable()
    {
        // Si se apaga a medias (cambio de escena), el HUD no se queda escondido ni el estudio filmando.
        OcultarHud(false);
        SoltarRetratosVivos();
    }

    private void OnDestroy()
    {
        if (_materialesVivos == null) return;
        for (int i = 0; i < _materialesVivos.Length; i++)
            if (_materialesVivos[i] != null) Destroy(_materialesVivos[i]);
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
        if (ocultar) MinimapController.Ocultar(this);
        else MinimapController.Mostrar(this);
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
        SoltarRetratosVivos();
        gameObject.SetActive(false);
    }

    // ── Contenido ─────────────────────────────────────────────────────────────────

    private void Rellenar(ResultadoDeBatalla r)
    {
        if (titulo != null) titulo.text = Texto("BATTLE_REPORT_TITLE", "¡Victoria!");
        if (separadorProgreso != null) separadorProgreso.text = Texto("BATTLE_REPORT_PROGRESS", "Progreso");
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

        _sintonia = r.Sintonia;
        float alto = primeraFila;
        bool haySintonia = sintonia != null && sintonia.raiz != null && _sintonia.HasValue;
        if (sintonia != null && sintonia.raiz != null) sintonia.raiz.gameObject.SetActive(haySintonia);
        if (haySintonia)
        {
            var g = _sintonia.Value;
            sintonia.nombre.text = Texto("BATTLE_REPORT_SINTONIA", "Sintonía Estelar");
            sintonia.textoPastilla.text = "+" + g.puntos;
            bool hito = g.hitosNuevos > 0;
            if (sintonia.aviso != null)
            {
                sintonia.aviso.gameObject.SetActive(hito);
                // El aviso se ajusta al ancho de la fila: parte en dos líneas y, si aún no cabe, encoge.
                sintonia.aviso.textWrappingMode = TextWrappingModes.Normal;
                if (!sintonia.aviso.enableAutoSizing)
                {
                    sintonia.aviso.fontSizeMax = sintonia.aviso.fontSize;
                    sintonia.aviso.fontSizeMin = Mathf.Min(10f, sintonia.aviso.fontSize);
                    sintonia.aviso.enableAutoSizing = true;
                }
                sintonia.aviso.text = Texto("BATTLE_REPORT_SINTONIA_HITO", "¡Sintonía completa! Elige Vida o Magia en el menú");
            }
            sintonia.raiz.anchoredPosition = new Vector2(sintonia.raiz.anchoredPosition.x, -primeraFila);
            alto += altoDeFila + (hito ? altoDelAviso : 0f);
        }
        if (separadorProgreso != null) separadorProgreso.gameObject.SetActive(haySintonia);

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
            // Los huecos que se ven, centrados en el ancho del bloque (no pegados a la izquierda).
            float centro = bloqueBotin.rect.width * 0.5f;
            for (int i = 0, visto = 0; i < huecos.Length && visto < botin; i++)
            {
                if (!huecos[i].raiz.gameObject.activeSelf) continue;
                var p = huecos[i].raiz.anchoredPosition;
                huecos[i].raiz.anchoredPosition = new Vector2(centro + (visto - (botin - 1) * 0.5f) * pasoDeHueco, p.y);
                visto++;
            }
            bloqueBotin.gameObject.SetActive(botin > 0);
            // Sin Sintonía, el botín sube a su sitio (la cabecera deja el mismo hueco).
            float y = haySintonia ? alto : primeraFila - 30f;
            bloqueBotin.anchoredPosition = new Vector2(bloqueBotin.anchoredPosition.x, -y);
            if (!haySintonia) alto = y;
        }

        if (panel != null)
        {
            alto += (botin > 0 ? altoDelBotin : 0f) + altoDelPie;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, alto);
        }
    }

    /// El personaje al mando en grande y los compañeros de la foto en pequeño: vivos si el estudio
    /// de retratos está en marcha (el del mando celebra), y si no, el retrato fijo de su ficha.
    private void RellenarRetratos(ResultadoDeBatalla r)
    {
        var foto = r.EnLaFoto;
        Slot principal = foto.Count > 0 ? foto[0]
            : PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;

        PonerRetrato(0, principal, retrato, retratoVivo);
        if (_vivos[0].HasValue && EstudioDeRetratos.Activo != null)
            EstudioDeRetratos.Activo.Reaccionar(principal, ReaccionDeRetrato.Victoria);

        for (int i = 0; i < companeros.Length; i++)
        {
            bool hay = i + 1 < foto.Count;
            var vivo = companerosVivos != null && i < companerosVivos.Length ? companerosVivos[i] : null;
            if (hay) hay = PonerRetrato(i + 1, foto[i + 1], companeros[i], vivo);
            // El retrato va dentro de su aro: se apaga el aro entero.
            companeros[i].transform.parent.gameObject.SetActive(hay);
        }
    }

    /// Pone a un personaje en un hueco de retrato. False si no hay imagen ni viva ni fija.
    private bool PonerRetrato(int hueco, Slot personaje, Image fijo, RawImage vivo)
    {
        var estudio = EstudioDeRetratos.Activo;
        if (vivo != null && estudio != null && hueco < _vivos.Length)
        {
            estudio.MostrarPara(personaje, this, true);
            if (estudio.TryObtenerSinCambiarCara(personaje, out var img) && img.Valida)
            {
                _vivos[hueco] = personaje;
                vivo.texture = img.Textura;
                vivo.uvRect = img.Uv;
                var m = vivo.material;
                if (m != null && _materialesVivos != null && System.Array.IndexOf(_materialesVivos, m) >= 0)
                {
                    m.SetTexture(IdTexturaSobreFondo, img.TexturaSobreFondo);
                    m.SetFloat(IdUsarTexturaSobreFondo, img.TexturaSobreFondo != null ? 1f : 0f);
                }
                vivo.enabled = true;
                vivo.SetMaterialDirty();
                if (fijo != null) fijo.enabled = false;
                return true;
            }
            estudio.MostrarPara(personaje, this, false);
        }

        if (vivo != null) vivo.enabled = false;
        var ficha = FichaDePersonaje.Buscar(personaje);
        var sprite = ficha != null ? ficha.Retrato : null;
        if (fijo != null)
        {
            fijo.sprite = sprite;
            fijo.enabled = sprite != null;
        }
        return sprite != null;
    }

    /// Deja de pedir al estudio los retratos que se estaban enseñando.
    private void SoltarRetratosVivos()
    {
        var estudio = EstudioDeRetratos.Activo;
        for (int i = 0; i < _vivos.Length; i++)
        {
            if (!_vivos[i].HasValue) continue;
            if (estudio != null) estudio.MostrarPara(_vivos[i].Value, this, false);
            _vivos[i] = null;
        }
    }

    /// Cada retrato vivo con su copia del material (cada uno lleva su textura sobre fondo blanco).
    private void PrepararMaterialesVivos()
    {
        int n = 1 + (companerosVivos != null ? companerosVivos.Length : 0);
        _materialesVivos = new Material[n];
        for (int i = 0; i < n; i++)
        {
            var vivo = i == 0 ? retratoVivo : companerosVivos[i - 1];
            if (vivo == null || vivo.material == null || vivo.material == vivo.defaultMaterial) continue;
            _materialesVivos[i] = new Material(vivo.material) { name = vivo.material.name + " (informe)" };
            vivo.material = _materialesVivos[i];
            vivo.enabled = false;
        }
    }

    // ── Animación ────────────────────────────────────────────────────────────────

    private void PrepararAnimacion()
    {
        if (_sintonia.HasValue && sintonia != null && sintonia.raiz != null)
        {
            var g = _sintonia.Value;
            PonerBarra(g.antes, g.antes, g.umbralAntes);
            sintonia.pastilla.localScale = Vector3.zero;
            if (sintonia.aviso != null) sintonia.aviso.rectTransform.localScale = Vector3.zero;
        }
        for (int i = 0; i < huecos.Length; i++) huecos[i].raiz.localScale = Vector3.zero;
    }

    private IEnumerator Co_AnimarContenido()
    {
        _animando = true;
        yield return Esperar(esperaAntesDeLasBarras);

        if (_sintonia.HasValue && sintonia != null && sintonia.raiz != null)
        {
            var g = _sintonia.Value;
            if (g.hitosNuevos > 0)
            {
                // Se llena hasta el hito, avisa, y empieza la barra siguiente con lo que sobra.
                yield return Co_LlenarBarra(g.antes, g.antes, g.umbralAntes, g.umbralAntes);
                if (sintonia.aviso != null) yield return Co_Saltar(sintonia.aviso.rectTransform);
                yield return Esperar(0.35f);
                yield return Co_LlenarBarra(0, 0, g.despues, g.umbralDespues);
            }
            else
            {
                yield return Co_LlenarBarra(g.antes, g.antes, g.despues, g.umbralAntes);
            }
            yield return Co_Saltar(sintonia.pastilla);
            yield return Esperar(0.25f);
        }

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

    /// La barra de Sintonía sube de 'desde' a 'hasta' (puntos sobre 'umbral'); 'base' es lo que ya había.
    private IEnumerator Co_LlenarBarra(int baseAntes, int desde, int hasta, int umbral)
    {
        float t = 0f;
        while (t < 1f && !_saltarAnimacion)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.05f, duracionBarra));
            float k = 1f - (1f - t) * (1f - t);
            PonerBarra(baseAntes, Mathf.Lerp(desde, hasta, k), umbral);
            yield return null;
        }
        PonerBarra(baseAntes, hasta, umbral);
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
        if (_sintonia.HasValue && sintonia != null && sintonia.raiz != null)
        {
            var g = _sintonia.Value;
            if (g.hitosNuevos > 0) PonerBarra(0, g.despues, g.umbralDespues);
            else PonerBarra(g.antes, g.despues, g.umbralAntes);
            sintonia.pastilla.localScale = Vector3.one;
            if (sintonia.aviso != null) sintonia.aviso.rectTransform.localScale = Vector3.one;
        }
        for (int i = 0; i < huecos.Length; i++) huecos[i].raiz.localScale = Vector3.one;
    }

    /// Lo que ya había en la barra base y lo ganado hasta ahora en la que brilla; «actual / umbral».
    private void PonerBarra(int baseAntes, float actual, int umbral)
    {
        float tope = Mathf.Max(1f, umbral);
        sintonia.barraBase.fillAmount = Mathf.Clamp01(Mathf.Min(baseAntes, actual) / tope);
        sintonia.barraGanada.fillAmount = Mathf.Clamp01(actual / tope);
        sintonia.valores.text = $"{actual:0}<color=#B9B0D8> / {umbral}</color>";
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
