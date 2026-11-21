using System;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using M = EstiloDePapelDelMenu;

/// <summary>
/// Ajustes del menú principal con la disposición de la maqueta «Menú principal: Ajustes»: una
/// hoja grande con su etiqueta, pestañas a la izquierda (Sonido, Imagen, Idioma, Juego) y a la
/// derecha las filas de la pestaña abierta (deslizadores con la estrella y su número, interruptores
/// Sí/No). Se monta por código al despertar; los cambios se guardan en el acto en
/// <see cref="PlayerSettings"/>. B, Esc o Start cierran.
/// </summary>
public class SettingsMenuController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [Tooltip("Fuentes de los menús de papel.")]
    [SerializeField] private EstiloDeMenuSencillo estilo;
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar una orden de cierre.")]
    private float cancelInputGracePeriod = 0.25f;

    // Medidas de la maqueta (lienzo de 1920×1080).
    const float XPestana = 366f, YPestana = 180f, PasoPestana = 92f;
    const float XEtiqueta = 710f, YFila = 190f, PasoFila = 90f;
    const float XPista = 980f, AnchoPista = 450f, XValor = 1450f, XInterruptor = 1430f;

    private sealed class Fila
    {
        public Selectable control;
        public TextMeshProUGUI etiqueta, valor, debajo;
        public Image pista, bola;
        public Func<bool> leer;
        public Action<bool> escribir;
        public Func<float> leerValor;
    }

    private sealed class Pestana
    {
        public string clave, porDefecto;
        public Button boton;
        public FichaElegible ficha;
        public RectTransform cuerpo;
        public readonly List<Fila> filas = new();
    }

    private readonly List<Pestana> _pestanas = new();
    private readonly List<(TextMeshProUGUI texto, string clave, string porDefecto)> _traducibles = new();
    private RectTransform _lienzo;
    private Pestana _abierta;
    private Action _onClosed;
    private float _openedAt = -999f;
    private bool _montado;

    public bool IsVisible => root != null && root.activeInHierarchy;

    private TMP_FontAsset Titulos => estilo != null && estilo.fuenteTitulo != null ? estilo.fuenteTitulo : TMP_Settings.defaultFontAsset;
    private TMP_FontAsset Letra => estilo != null && estilo.fuenteTexto != null ? estilo.fuenteTexto : TMP_Settings.defaultFontAsset;

    void Awake()
    {
        if (!root) root = gameObject;
        Montar();
    }

    void OnEnable()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLocaleChanged += Traducir;
        Refrescar();
    }

    void OnDisable()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLocaleChanged -= Traducir;
    }

    // ─── Montaje ───────────────────────────────────────────────────────────

    private void Montar()
    {
        if (_montado) return;
        _montado = true;
        _lienzo = PiezasDeUI.LienzoDeMaqueta((RectTransform)root.transform, "Ajustes");

        var noche = PiezasDeUI.Imagen("Fondo", _lienzo, null, new Color(M.NocheProfunda.r, M.NocheProfunda.g, M.NocheProfunda.b, 0.45f));
        PiezasDeUI.ColocarArribaIzquierda(noche.rectTransform, 0f, 0f, 1920f, 1080f);
        noche.raycastTarget = true; // tapa el menú de detrás
        var izquierda = PiezasDeUI.Velo("VeloIzquierdo", _lienzo, M.NocheProfunda, 0.86f, 0.62f, 0.55f, false);
        PiezasDeUI.ColocarArribaIzquierda(izquierda.rectTransform, 0f, 0f, 1000f, 1080f);
        var abajo = PiezasDeUI.Velo("VeloDeAbajo", _lienzo, M.NocheProfunda, 0.95f, 0.6f, 0.5f, true);
        PiezasDeUI.ColocarArribaIzquierda(abajo.rectTransform, 0f, 880f, 1920f, 200f);

        var hoja = PiezasDeUI.NuevaCarta("Hoja", _lienzo, PaletaUI.Papel, 30f);
        PiezasDeUI.ColocarArribaIzquierda(hoja.raiz, 330f, 110f, 1260f, 820f);
        var etiqueta = PiezasDeUI.NuevaCarta("Etiqueta", _lienzo, PaletaUI.Oro, 18f);
        PiezasDeUI.ColocarArribaIzquierda(etiqueta.raiz, 390f, 80f, 300f, 62f, -2f);
        Traducible(Texto("Titulo", etiqueta.contenido, true, 40f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center), "SETTINGS_TITLE", "Ajustes");

        var division = PiezasDeUI.Imagen("Division", _lienzo, null, new Color(PaletaUI.Tinta.r, PaletaUI.Tinta.g, PaletaUI.Tinta.b, 0.3f));
        division.raycastTarget = false;
        PiezasDeUI.ColocarArribaIzquierda(division.rectTransform, 662f, 160f, 3f, 630f);

        var sonido = NuevaPestana("AJUSTES_PESTANA_SONIDO", "Sonido");
        Deslizador(sonido, "AJUSTES_MUSICA", "Música", () => PlayerSettings.MusicVolume, PlayerSettings.SetMusicVolume);
        Deslizador(sonido, "AJUSTES_EFECTOS", "Efectos", () => PlayerSettings.SfxVolume, PlayerSettings.SetSfxVolume);
        Deslizador(sonido, "AJUSTES_VOCES", "Voces", () => PlayerSettings.DialogueVolume, PlayerSettings.SetDialogueVolume);
        Deslizador(sonido, "SETTINGS_MASTER_VOLUME", "Volumen general", () => PlayerSettings.MasterVolume, PlayerSettings.SetMasterVolume);
        Interruptor(sonido, "AJUSTES_SUBTITULOS", "Subtítulos en las charlas", () => PlayerSettings.Subtitles, PlayerSettings.SetSubtitles);
        Interruptor(sonido, "AJUSTES_VIBRACION", "Vibración del mando", () => PlayerSettings.Vibration, PlayerSettings.SetVibration);

        var imagen = NuevaPestana("AJUSTES_PESTANA_IMAGEN", "Imagen");
        Interruptor(imagen, "SETTINGS_FULLSCREEN", "Pantalla completa", () => PlayerSettings.Fullscreen, PlayerSettings.SetFullscreen);

        var idioma = NuevaPestana("AJUSTES_PESTANA_IDIOMA", "Idioma");
        Interruptor(idioma, null, "Español", () => PlayerSettings.Language == "es", si => { if (si) PlayerSettings.SetLanguage("es"); });
        Interruptor(idioma, null, "English", () => PlayerSettings.Language == "en", si => { if (si) PlayerSettings.SetLanguage("en"); });

        var juego = NuevaPestana("AJUSTES_PESTANA_JUEGO", "Juego");
        Deslizador(juego, "AJUSTES_SENSIBILIDAD", "Sensibilidad",
                   () => Mathf.InverseLerp(0.1f, 3f, PlayerSettings.LookSensitivity),
                   v => PlayerSettings.SetLookSensitivity(Mathf.Lerp(0.1f, 3f, v)));
        Interruptor(juego, "AJUSTES_INVERTIR_Y", "Invertir la cámara (eje Y)", () => PlayerSettings.InvertLook, PlayerSettings.SetInvertLook);
        Interruptor(juego, "SETTINGS_COMBAT_HINTS", "Avisos de combate", () => PlayerSettings.AvisosDeCombate, PlayerSettings.SetAvisosDeCombate);

        var nota = Texto("Nota", Hueco("Nota", XEtiqueta, 810f, 830f, 30f), false, 22f, M.TextoSuave);
        Traducible(nota, "AJUSTES_GUARDADO_SOLO", "Los cambios se guardan solos.");

        Traducible(M.Ayuda(_lienzo, Titulos, 1528f, Core.InputGlyphs.InputGlyphNames.Confirm), "MENU_CAMBIAR", "Cambiar");
        Traducible(M.Ayuda(_lienzo, Titulos, 1716f, Core.InputGlyphs.InputGlyphNames.Cancel), "MENU_VOLVER", "Volver");

        Enlazar();
        root.AddComponent<SeleccionConTeclado>().Configurar(_pestanas[0].boton);
        AbrirPestana(_pestanas[0]);
        Traducir();
    }

    private RectTransform Hueco(string nombre, float x, float y, float ancho, float alto, Transform padre = null)
    {
        var rt = new GameObject(nombre, typeof(RectTransform)).transform as RectTransform;
        rt.SetParent(padre != null ? padre : _lienzo, false);
        PiezasDeUI.ColocarArribaIzquierda(rt, x, y, ancho, alto);
        return rt;
    }

    private TextMeshProUGUI Texto(string nombre, Transform padre, bool titulo, float tamano, Color color,
        TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft)
    {
        var t = PiezasDeUI.Texto(nombre, padre, "", titulo ? Titulos : Letra, tamano, color);
        t.alignment = alineacion;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        PiezasDeUI.Estirar(t.rectTransform);
        return t;
    }

    private TextMeshProUGUI Traducible(TextMeshProUGUI t, string clave, string porDefecto)
    {
        if (string.IsNullOrEmpty(clave)) t.text = porDefecto;
        else _traducibles.Add((t, clave, porDefecto));
        return t;
    }

    private void Traducir()
    {
        var loc = LocalizationManager.Instance;
        foreach (var (t, clave, porDefecto) in _traducibles)
            if (t != null) t.text = loc != null ? loc.Get(clave, porDefecto) : porDefecto;
        Refrescar();
    }

    private Pestana NuevaPestana(string clave, string porDefecto)
    {
        int i = _pestanas.Count;
        var p = new Pestana { clave = clave, porDefecto = porDefecto };
        var hueco = Hueco("Pestaña " + porDefecto, XPestana, YPestana + i * PasoPestana, 260f, 72f);
        var tapa = hueco.gameObject.AddComponent<Image>();
        tapa.color = new Color(1f, 1f, 1f, 0f);
        p.boton = hueco.gameObject.AddComponent<Button>();
        p.boton.targetGraphic = tapa;
        p.boton.transition = Selectable.Transition.None;
        p.boton.onClick.AddListener(() => AbrirPestana(p));
        hueco.gameObject.AddComponent<UIButtonAudio>();
        hueco.gameObject.AddComponent<FocoAlPasarElRaton>();
        var carta = PiezasDeUI.NuevaCarta("Carta", hueco, PaletaUI.PapelApagado, 18f, 5f);
        PiezasDeUI.Estirar(carta.raiz);
        var texto = Texto("Texto", carta.contenido, true, 30f, M.TextoSuave);
        texto.margin = new Vector4(24f, -4f, 8f, 0f);
        Traducible(texto, clave, porDefecto);
        var estrella = PiezasDeUI.EstrellaEn("Estrella", carta.contenido, 0f, 24f, 36f, 12f);
        p.ficha = hueco.gameObject.AddComponent<FichaElegible>();
        p.ficha.Configurar(p.boton, carta, texto, new[] { estrella.gameObject });
        p.ficha.Colores(PaletaUI.PapelApagado, PaletaUI.Oro, M.TextoSuave, PaletaUI.TintaDeTexto);
        p.ficha.Movimiento(0.6f, -1.4f, Vector2.zero, 5f, 5f);
        p.ficha.SigueElFoco = false;

        p.cuerpo = Hueco("Filas " + porDefecto, 0f, 0f, 1920f, 1080f);
        _pestanas.Add(p);
        return p;
    }

    private float YSiguiente(Pestana p, bool interruptor)
    {
        // Deslizadores arriba, de 90 en 90; los interruptores, tras una raya si hay deslizadores encima.
        int deslizadores = 0, interruptores = 0;
        foreach (var f in p.filas) { if (f.control is Slider) deslizadores++; else interruptores++; }
        if (!interruptor) return YFila + deslizadores * PasoFila;
        if (deslizadores == 0) return YFila + interruptores * PasoFila;
        return YRaya(deslizadores) + 40f + interruptores * PasoFila;
    }

    // La raya que separa los deslizadores de los interruptores (en la maqueta, 550 tras cuatro).
    private static float YRaya(int deslizadores) => YFila + deslizadores * PasoFila;

    private void Deslizador(Pestana p, string clave, string porDefecto, Func<float> leer, Action<float> escribir)
    {
        float y = YSiguiente(p, false);
        var fila = new Fila { leerValor = leer };
        Traducible(fila.etiqueta = Texto("Etiqueta", Hueco("Etiqueta " + porDefecto, XEtiqueta, y, 260f, 40f, p.cuerpo), true, 28f, PaletaUI.TintaDeTexto), clave, porDefecto);

        var pista = PiezasDeUI.Redondeado("Pista " + porDefecto, p.cuerpo, PiezasDeUI.TintaOscura, 14f);
        PiezasDeUI.ColocarArribaIzquierda(pista.rectTransform, XPista, y + 8f, AnchoPista, 24f);
        var dentro = PiezasDeUI.Redondeado("Dentro", pista.transform, PaletaUI.PapelApagado, 11f);
        PiezasDeUI.Margen(dentro.rectTransform, 3f);
        var hundido = PiezasDeUI.Redondeado("Hundido", dentro.transform, new Color(PaletaUI.Tinta.r, PaletaUI.Tinta.g, PaletaUI.Tinta.b, 0.25f), 11f);
        var rh = hundido.rectTransform;
        rh.anchorMin = new Vector2(0f, 1f); rh.anchorMax = Vector2.one; rh.pivot = new Vector2(0.5f, 1f);
        rh.offsetMin = new Vector2(0f, -3f); rh.offsetMax = Vector2.zero;
        pista.raycastTarget = true;

        var zona = new GameObject("Relleno", typeof(RectTransform)).transform as RectTransform;
        zona.SetParent(pista.transform, false);
        PiezasDeUI.Margen(zona, 3f);
        var relleno = PiezasDeUI.Redondeado("Oro", zona, PaletaUI.Oro, 10f);
        PiezasDeUI.Estirar(relleno.rectTransform);
        var mango = new GameObject("Mango", typeof(RectTransform)).transform as RectTransform;
        mango.SetParent(pista.transform, false);
        PiezasDeUI.Margen(mango, 3f);
        var estrella = PiezasDeUI.Imagen("Estrella", mango, PiezasDeUI.Estrella, PaletaUI.Oro);
        estrella.raycastTarget = false;
        // El deslizador estira el mango a lo alto de su zona (18): se le suma lo que falta para 52.
        estrella.rectTransform.sizeDelta = new Vector2(52f, 52f - 18f);

        var slider = pista.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.fillRect = relleno.rectTransform;
        slider.handleRect = estrella.rectTransform;
        slider.targetGraphic = estrella;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(leer());
        slider.onValueChanged.AddListener(v =>
        {
            // Pasos de 5 en 5 con el mando y el teclado (el deslizador da pasos de una décima).
            float redondeado = Mathf.Round(v * 20f) / 20f;
            escribir(redondeado);
            Refrescar();
        });
        pista.gameObject.AddComponent<UISliderAudio>();
        fila.control = slider;

        var caja = PiezasDeUI.Redondeado("Valor " + porDefecto, p.cuerpo, PiezasDeUI.TintaOscura, 14f);
        PiezasDeUI.ColocarArribaIzquierda(caja.rectTransform, XValor, y - 2f, 90f, 42f);
        var fondoCaja = PiezasDeUI.Redondeado("Dentro", caja.transform, PaletaUI.PapelClaro, 11f);
        PiezasDeUI.Margen(fondoCaja.rectTransform, 3f);
        fila.valor = Texto("Numero", caja.transform, true, 24f, PiezasDeUI.TintaOscura, TextAlignmentOptions.Center);

        Foco(p, fila, y);
        p.filas.Add(fila);
    }

    private void Interruptor(Pestana p, string clave, string porDefecto, Func<bool> leer, Action<bool> escribir)
    {
        float y = YSiguiente(p, true);
        bool primeroTrasDeslizadores = p.filas.Count > 0 && p.filas[p.filas.Count - 1].control is Slider;
        if (primeroTrasDeslizadores)
        {
            var raya = PiezasDeUI.Imagen("Raya", p.cuerpo, null, new Color(PaletaUI.Tinta.r, PaletaUI.Tinta.g, PaletaUI.Tinta.b, 0.25f));
            raya.raycastTarget = false;
            int deslizadores = p.filas.FindAll(f => f.control is Slider).Count;
            PiezasDeUI.ColocarArribaIzquierda(raya.rectTransform, XEtiqueta, YRaya(deslizadores), 830f, 3f);
        }
        var fila = new Fila { leer = leer, escribir = escribir };
        Traducible(fila.etiqueta = Texto("Etiqueta", Hueco("Etiqueta " + porDefecto, XEtiqueta, y, 700f, 40f, p.cuerpo), true, 28f, PaletaUI.TintaDeTexto), clave, porDefecto);

        var pista = PiezasDeUI.Redondeado("Interruptor " + porDefecto, p.cuerpo, PiezasDeUI.TintaOscura, 22f);
        PiezasDeUI.ColocarArribaIzquierda(pista.rectTransform, XInterruptor, y, 110f, 44f);
        pista.raycastTarget = true;
        fila.pista = PiezasDeUI.Redondeado("Dentro", pista.transform, PaletaUI.PapelApagado, 19f);
        PiezasDeUI.Margen(fila.pista.rectTransform, 3f);
        var hundido = PiezasDeUI.Redondeado("Hundido", fila.pista.transform, new Color(PiezasDeUI.TintaOscura.r, PiezasDeUI.TintaOscura.g, PiezasDeUI.TintaOscura.b, 0.18f), 19f);
        var rh = hundido.rectTransform;
        rh.anchorMin = new Vector2(0f, 1f); rh.anchorMax = Vector2.one; rh.pivot = new Vector2(0.5f, 1f);
        rh.offsetMin = new Vector2(0f, -3f); rh.offsetMax = Vector2.zero;
        fila.bola = PiezasDeUI.Imagen("Bola", pista.transform, PiezasDeUI.Circulo, PiezasDeUI.TintaOscura);
        fila.bola.raycastTarget = false;
        var dentroBola = PiezasDeUI.Imagen("Dentro", fila.bola.transform, PiezasDeUI.Circulo, PaletaUI.PapelClaro);
        dentroBola.raycastTarget = false;
        PiezasDeUI.Margen(dentroBola.rectTransform, 3f);
        fila.debajo = Texto("SiNo", Hueco("SiNo " + porDefecto, XInterruptor, y + 46f, 110f, 24f, p.cuerpo), false, 18f, M.TextoSuave, TextAlignmentOptions.Center);

        var boton = pista.gameObject.AddComponent<Button>();
        boton.targetGraphic = pista;
        boton.transition = Selectable.Transition.None;
        boton.onClick.AddListener(() => { fila.escribir(!fila.leer()); Refrescar(); });
        pista.gameObject.AddComponent<UIButtonAudio>();
        fila.control = boton;

        Foco(p, fila, y);
        p.filas.Add(fila);
    }

    // Estrella a la izquierda de la fila con el foco: así se ve dónde está el mando.
    private void Foco(Pestana p, Fila fila, float y)
    {
        fila.control.gameObject.AddComponent<FocoAlPasarElRaton>();
        var estrella = PiezasDeUI.EstrellaEn("Foco", p.cuerpo, XEtiqueta - 24f, y + 20f, 30f, 12f);
        var ficha = fila.control.gameObject.AddComponent<FichaElegible>();
        ficha.Configurar(fila.control, new PiezasDeUI.Carta(), null, new[] { estrella.gameObject });
        ficha.Movimiento(0f, 0f, Vector2.zero, 0f, 0f);
    }

    // Navegación con el mando: pestañas de arriba abajo; a la derecha, las filas de la pestaña; a
    // la izquierda desde una fila, vuelta a su pestaña (los deslizadores usan izquierda y derecha).
    private void Enlazar()
    {
        for (int i = 0; i < _pestanas.Count; i++)
        {
            var p = _pestanas[i];
            for (int j = 0; j < p.filas.Count; j++)
            {
                var c = p.filas[j].control;
                bool deslizador = c is Slider;
                c.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = j > 0 ? p.filas[j - 1].control : null,
                    selectOnDown = j < p.filas.Count - 1 ? p.filas[j + 1].control : null,
                    selectOnLeft = deslizador ? null : p.boton,
                };
            }
        }
        NavegacionDePestanas();
    }

    private void NavegacionDePestanas()
    {
        for (int i = 0; i < _pestanas.Count; i++)
        {
            var p = _pestanas[i];
            p.boton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? _pestanas[i - 1].boton : null,
                selectOnDown = i < _pestanas.Count - 1 ? _pestanas[i + 1].boton : null,
                selectOnRight = p.filas.Count > 0 ? p.filas[0].control : null,
            };
        }
    }

    private void AbrirPestana(Pestana p)
    {
        _abierta = p;
        foreach (var q in _pestanas)
        {
            q.ficha.Marcada = q == p;
            if (q.cuerpo.gameObject.activeSelf != (q == p)) q.cuerpo.gameObject.SetActive(q == p);
        }
        Refrescar();
    }

    // ─── Estado ────────────────────────────────────────────────────────────

    private void Refrescar()
    {
        if (!_montado) return;
        PlayerSettings.EnsureLoaded();
        var loc = LocalizationManager.Instance;
        string si = loc != null ? loc.Get("COMMON_YES", "Sí") : "Sí", no = loc != null ? loc.Get("COMMON_NO", "No") : "No";
        foreach (var p in _pestanas)
        foreach (var f in p.filas)
        {
            if (f.control is Slider s)
            {
                float v = f.leerValor();
                s.SetValueWithoutNotify(v);
                if (f.valor != null) f.valor.text = Mathf.RoundToInt(v * 100f).ToString();
            }
            else
            {
                bool encendido = f.leer();
                f.pista.color = encendido ? new Color32(0x7E, 0xD0, 0x8A, 0xFF) : PaletaUI.PapelApagado;
                PiezasDeUI.ColocarArribaIzquierda(f.bola.rectTransform, encendido ? 64f : 4f, 4f, 36f, 36f);
                f.debajo.text = encendido ? si : no;
            }
        }
    }

    // ─── Abrir y cerrar ──────────────────────────────────────────────────

    public void Show(GameObject initialSelection = null, Action onClosed = null)
    {
        _onClosed = onClosed;
        Montar();
        AbrirPestana(_pestanas[0]);
        if (root && !root.activeSelf) root.SetActive(true);
        var objetivo = initialSelection != null ? initialSelection : GetDefaultSelection();
        if (EventSystem.current != null && objetivo != null) EventSystem.current.SetSelectedGameObject(objetivo);
        _openedAt = Time.unscaledTime;
    }

    public void Close(bool silent = false)
    {
        bool estabaVisible = root && root.activeSelf;
        if (!silent && estabaVisible) AudioService.Instance?.PlaySFX("UI_Cancel", 1f);
        if (estabaVisible) root.SetActive(false);
        _onClosed?.Invoke();
        _onClosed = null;
    }

    public void ToggleVisibility()
    {
        if (root && root.activeSelf) Close();
        else Show();
    }

    public GameObject GetDefaultSelection()
    {
        Montar();
        return _pestanas.Count > 0 ? _pestanas[0].boton.gameObject : null;
    }

    void Update()
    {
        if (root == null || !root.activeInHierarchy) return;

        // La pestaña con el foco es la que se ve (como LB/RB en el menú de Start).
        var elegido = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (elegido != null && (_abierta == null || elegido != _abierta.boton.gameObject))
            foreach (var p in _pestanas)
                if (p.boton.gameObject == elegido) { AbrirPestana(p); break; }

        if (Time.unscaledTime - _openedAt < cancelInputGracePeriod) return;
        if (GamepadInputReader.CancelPressed || GamepadInputReader.StartPressed) Close();
    }
}
