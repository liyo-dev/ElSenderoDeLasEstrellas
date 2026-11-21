using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla breve del arranque que explica el símbolo de guardado: papel crema sobre la noche de
/// los menús, el símbolo animado (<see cref="SimboloDeGuardado"/>) y «cuando veas este símbolo, no
/// apagues ni cierres el juego». Sale una vez por ejecución, unos segundos, y se pasa con A (o un
/// clic). Se monta por código; quien la enseña (el menú principal) decide cuándo y sigue en
/// <c>alAcabar</c>, que se llama al empezar el fundido de salida.
/// </summary>
public sealed class AvisoDeGuardadoAlArrancar : MonoBehaviour
{
    const int OrdenDelLienzo = 500;
    const string TextoPorDefecto = "Este juego guarda automáticamente. Cuando veas este símbolo, no apagues ni cierres el juego.";
    const string PasarPorDefecto = "Continuar";

    private static bool s_visto;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_visto = false;
#endif

    /// <summary>Si aún no se ha enseñado en esta ejecución del juego.</summary>
    public static bool Pendiente => !s_visto;

    private EstiloDelGuardado _ajustes;
    private CanvasGroup _grupo;
    private CanvasGroup _contenido;
    private Button _boton;
    private Action _alAcabar;
    private float _tiempo;
    private float _saliendoDesde = -1f;
    private bool _pasar;

    /// <summary>
    /// Enseña el aviso en <paramref name="escena"/> (se va con ella si se descarga antes de acabar).
    /// Lo marca como visto en esta ejecución.
    /// </summary>
    public static void Mostrar(Scene escena, Action alAcabar)
    {
        s_visto = true;
        var go = new GameObject("[AvisoDeGuardadoAlArrancar]", typeof(RectTransform));
        if (escena.IsValid() && escena.isLoaded) SceneManager.MoveGameObjectToScene(go, escena);
        var aviso = go.AddComponent<AvisoDeGuardadoAlArrancar>();
        aviso._alAcabar = alAcabar;
        aviso.Montar();
    }

    private void Montar()
    {
        _ajustes = EstiloDelGuardado.Cargado;
        var estilo = _ajustes.estilo;
        var titulos = estilo != null && estilo.fuenteTitulo != null ? estilo.fuenteTitulo : TMP_Settings.defaultFontAsset;
        var letra = estilo != null && estilo.fuenteTexto != null ? estilo.fuenteTexto : TMP_Settings.defaultFontAsset;

        PiezasDeUI.Lienzo(gameObject, OrdenDelLienzo, conClic: true);
        _grupo = gameObject.AddComponent<CanvasGroup>();
        var lienzo = PiezasDeUI.LienzoDeMaqueta((RectTransform)transform, "Aviso");
        PiezasDeUI.FondoDeNoche(lienzo);

        // Todo el fondo es el botón: A (Submit) o un clic pasan el aviso.
        var tapa = PiezasDeUI.Imagen("Pasar", lienzo, null, new Color(1f, 1f, 1f, 0f));
        PiezasDeUI.ColocarArribaIzquierda(tapa.rectTransform, 0f, 0f, 1920f, 1080f);
        _boton = tapa.gameObject.AddComponent<Button>();
        _boton.targetGraphic = tapa;
        _boton.transition = Selectable.Transition.None;
        _boton.navigation = new Navigation { mode = Navigation.Mode.None };
        _boton.onClick.AddListener(() => _pasar = true);

        var contenido = new GameObject("Contenido", typeof(RectTransform)).transform as RectTransform;
        contenido.SetParent(lienzo, false);
        PiezasDeUI.ColocarArribaIzquierda(contenido, 0f, 0f, 1920f, 1080f);
        _contenido = contenido.gameObject.AddComponent<CanvasGroup>();
        _contenido.alpha = 0f;
        _contenido.blocksRaycasts = false;

        var carta = PiezasDeUI.NuevaCarta("Carta", contenido, PaletaUI.Papel, 28f);
        PiezasDeUI.ColocarArribaIzquierda(carta.raiz, 410f, 390f, 1100f, 300f, -0.6f);
        PiezasDeUI.EstrellaEn("Estrella", carta.contenido, 4f, 12f, 56f);

        var hueco = Hueco(carta.contenido, 70f, 70f, 160f, 160f);
        SimboloDeGuardado.Crear(hueco, 150f, _ajustes);

        var texto = PiezasDeUI.Texto("Texto", Hueco(carta.contenido, 280f, 40f, 760f, 220f),
            Traducir(_ajustes.claveDelTexto, TextoPorDefecto), letra, 36f, PaletaUI.TintaDeTexto);
        texto.alignment = TextAlignmentOptions.MidlineLeft;
        PiezasDeUI.Estirar(texto.rectTransform);

        EstiloDePapelDelMenu.Ayuda(contenido, titulos, 1726f, Core.InputGlyphs.InputGlyphNames.Confirm).text =
            Traducir(_ajustes.claveDePasar, PasarPorDefecto);

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_boton.gameObject);
    }

    private static RectTransform Hueco(Transform padre, float x, float y, float ancho, float alto)
    {
        var rt = new GameObject("Hueco", typeof(RectTransform)).transform as RectTransform;
        rt.SetParent(padre, false);
        PiezasDeUI.ColocarArribaIzquierda(rt, x, y, ancho, alto);
        return rt;
    }

    private static string Traducir(string clave, string porDefecto)
    {
        var loc = LocalizationManager.Instance;
        return loc != null && !string.IsNullOrEmpty(clave) ? loc.Get(clave, porDefecto) : porDefecto;
    }

    void Update()
    {
        _tiempo += Time.unscaledDeltaTime;
        float fundido = _ajustes.fundidoDelAviso;

        if (_saliendoDesde < 0f)
        {
            _contenido.alpha = Mathf.Clamp01(_tiempo / fundido);
            // El menú de detrás puede llevarse el foco al arrancar; A tiene que llegar al aviso.
            var sistema = EventSystem.current;
            if (sistema != null && sistema.currentSelectedGameObject != _boton.gameObject)
                sistema.SetSelectedGameObject(_boton.gameObject);
            bool puedePasar = _tiempo >= _ajustes.segundosAntesDePoderPasar;
            if (_pasar && !puedePasar) _pasar = false;
            if (_tiempo >= _ajustes.segundosDelAviso || _pasar) EmpezarASalir();
            return;
        }

        _grupo.alpha = 1f - Mathf.Clamp01((_tiempo - _saliendoDesde) / fundido);
        if (_grupo.alpha <= 0f) Destroy(gameObject);
    }

    private void EmpezarASalir()
    {
        _saliendoDesde = _tiempo;
        if (_pasar) AudioService.Instance?.PlaySFX("UI_Submit");
        _grupo.interactable = false;
        _grupo.blocksRaycasts = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _boton.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        var alAcabar = _alAcabar;
        _alAcabar = null;
        alAcabar?.Invoke();
    }
}
