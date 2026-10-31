using System.Collections;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Álbum de una <see cref="ColeccionDeObjetos"/>: las piezas de cada página (las que faltan, en
/// sombra), la frase del coleccionista sobre la pieza elegida y el premio de la página. Se monta
/// por código con un <see cref="EstiloDeMenuSencillo"/> al abrirse y se destruye al cerrarse.
/// Mando: izquierda/derecha elige pieza, LB/RB cambia de página, B o Start cierra. Ratón: clic en
/// las piezas, las flechas y Cerrar.
/// </summary>
public sealed class ColeccionUI : MonoBehaviour
{
    private const float BloqueoAlAbrir = 0.25f;
    private const float RetrasoAlSoltarElJuego = 0.2f;
    private const float RepeticionDeNavegacion = 0.22f;
    private const int PiezasPorFila = 3;

    private class Ranura
    {
        public RectTransform raiz;
        public Image tarjeta, icono, chipNuevo;
        public TextMeshProUGUI nombre;
    }

    private ColeccionDeObjetos _coleccion;
    private EstiloDeMenuSencillo _estilo;
    private readonly HashSet<PiezaDeColeccion> _nuevas = new();
    private readonly List<Ranura> _ranuras = new();
    private RectTransform _rejilla;
    private TextMeshProUGUI _textoPagina, _frase, _premio;
    private GameObject _flechaIzquierda, _flechaDerecha;
    private Canvas _canvas;
    private int _pagina, _seleccion;
    private float _ignorarHasta, _siguienteNavegacion;
    private bool _bloqueado;

    /// <summary>Abre el álbum. <paramref name="nuevas"/>: piezas recién entregadas (salen marcadas).</summary>
    public static ColeccionUI Abrir(ColeccionDeObjetos coleccion, EstiloDeMenuSencillo estilo, ICollection<PiezaDeColeccion> nuevas)
    {
        if (coleccion == null || coleccion.paginas == null || coleccion.paginas.Count == 0) return null;
        if (!MenuManager.TryOpen(MenuKind.Collection)) return null;
        var ui = new GameObject("Coleccion (menú)").AddComponent<ColeccionUI>();
        ui.Montar(coleccion, estilo, nuevas);
        return ui;
    }

    private void Montar(ColeccionDeObjetos coleccion, EstiloDeMenuSencillo estilo, ICollection<PiezaDeColeccion> nuevas)
    {
        _coleccion = coleccion;
        _estilo = estilo != null ? estilo : ScriptableObject.CreateInstance<EstiloDeMenuSencillo>();
        if (nuevas != null) foreach (var p in nuevas) _nuevas.Add(p);

        ConstruirInterfaz();
        ElegirPrimeraNovedad();
        Pintar();
        Bloquear();
    }

    // ── Bloqueo del juego (mismo patrón que la tienda) ───────────────────────────────────────

    private void Bloquear()
    {
        _bloqueado = true;
        _ignorarHasta = Time.unscaledTime + BloqueoAlAbrir;
        TimeScaleArbiterService.Request(this, 0f);
        Core.PlayerInputManager.Instance?.PushUIMode();
        GamepadInputReader.PushGameplaySuppression(this);
        GamepadInputReader.PushUiNavigationScope();
        GamepadInputReader.OnInput += AlPulsar;
    }

    private void Soltar()
    {
        if (!_bloqueado) return;
        _bloqueado = false;
        GamepadInputReader.OnInput -= AlPulsar;
        TimeScaleArbiterService.Release(this);
        Core.PlayerInputManager.Instance?.PopUIMode();
        MenuManager.Close(MenuKind.Collection);
        GamepadInputReader.PopUiNavigationScope();
        GamepadInputReader.PopGameplaySuppression(this);
    }

    public void Cerrar()
    {
        if (!_bloqueado) return;
        if (_canvas != null) _canvas.enabled = false;
        GamepadInputReader.IgnoreCancelButton(0.3f);
        StartCoroutine(Co_CerrarTrasUnMomento());
    }

    // El botón que cierra no debe llegar al juego: se suelta el bloqueo un poco después.
    private IEnumerator Co_CerrarTrasUnMomento()
    {
        yield return new WaitForSecondsRealtime(RetrasoAlSoltarElJuego);
        Soltar();
        Destroy(gameObject);
    }

    private void OnDestroy() => Soltar();

    // ── Entrada ──────────────────────────────────────────────────────────────────────────────

    private void AlPulsar(GamepadInputReader.InputEvent e)
    {
        if (Time.unscaledTime < _ignorarHasta || _canvas == null || !_canvas.enabled) return;
        bool hecho = e.Phase == InputActionPhase.Performed;
        switch (e.Type)
        {
            case GamepadInputReader.InputEventType.Cancel when hecho:
            case GamepadInputReader.InputEventType.Start when hecho:
                Cerrar();
                break;
            case GamepadInputReader.InputEventType.LeftShoulder when hecho:
                CambiarPagina(-1);
                break;
            case GamepadInputReader.InputEventType.RightShoulder when hecho:
                CambiarPagina(+1);
                break;
            case GamepadInputReader.InputEventType.DpadLeft:
                Mover(-1);
                break;
            case GamepadInputReader.InputEventType.DpadRight:
                Mover(+1);
                break;
            case GamepadInputReader.InputEventType.Navigate when hecho:
                if (Mathf.Abs(e.Value.x) > 0.5f) Mover(e.Value.x > 0f ? +1 : -1);
                break;
        }
    }

    private void Mover(int paso)
    {
        if (Time.unscaledTime < _siguienteNavegacion) return;
        _siguienteNavegacion = Time.unscaledTime + RepeticionDeNavegacion;
        int total = PiezasDe(_pagina).Count;
        int nueva = _seleccion + paso;
        if (nueva < 0 && _pagina > 0) { _pagina--; _seleccion = PiezasDe(_pagina).Count - 1; }
        else if (nueva >= total && _pagina < _coleccion.paginas.Count - 1) { _pagina++; _seleccion = 0; }
        else _seleccion = Mathf.Clamp(nueva, 0, Mathf.Max(0, total - 1));
        Pintar();
    }

    private void CambiarPagina(int paso)
    {
        int nueva = Mathf.Clamp(_pagina + paso, 0, _coleccion.paginas.Count - 1);
        if (nueva == _pagina) return;
        _pagina = nueva;
        _seleccion = 0;
        Pintar();
    }

    private void Elegir(int indice)
    {
        _seleccion = indice;
        Pintar();
    }

    private List<PiezaDeColeccion> PiezasDe(int pagina) =>
        _coleccion.paginas[pagina]?.piezas ?? new List<PiezaDeColeccion>();

    private void ElegirPrimeraNovedad()
    {
        for (int p = 0; p < _coleccion.paginas.Count; p++)
        {
            var piezas = PiezasDe(p);
            for (int i = 0; i < piezas.Count; i++)
                if (_nuevas.Contains(piezas[i])) { _pagina = p; _seleccion = i; return; }
        }
    }

    // ── Pintado ──────────────────────────────────────────────────────────────────────────────

    private void Pintar()
    {
        var pagina = _coleccion.paginas[_pagina];
        var piezas = PiezasDe(_pagina);
        int conseguidas = 0;
        foreach (var p in piezas) if (_coleccion.Tiene(p)) conseguidas++;

        _textoPagina.text = $"{pagina.Titulo}   ·   {conseguidas}/{piezas.Count}" +
                            (_coleccion.paginas.Count > 1 ? $"   ·   {_pagina + 1}/{_coleccion.paginas.Count}" : "");
        _flechaIzquierda.SetActive(_pagina > 0);
        _flechaDerecha.SetActive(_pagina < _coleccion.paginas.Count - 1);

        AsegurarRanuras(piezas.Count);
        for (int i = 0; i < _ranuras.Count; i++)
        {
            var r = _ranuras[i];
            bool usada = i < piezas.Count;
            r.raiz.gameObject.SetActive(usada);
            if (!usada) continue;

            var pieza = piezas[i];
            bool tiene = _coleccion.Tiene(pieza);
            bool elegida = i == _seleccion;
            r.icono.sprite = pieza.objeto != null ? pieza.objeto.icon : null;
            r.icono.enabled = r.icono.sprite != null;
            r.icono.color = tiene ? Color.white : new Color(0f, 0f, 0f, 0.45f);
            r.nombre.text = tiene && pieza.objeto != null ? pieza.objeto.GetLocalizedName() : "???";
            r.nombre.color = tiene ? _estilo.colorTexto : _estilo.colorTextoSuave;
            r.chipNuevo.gameObject.SetActive(_nuevas.Contains(pieza));
            r.tarjeta.color = elegida ? _estilo.colorResalte : Color.white;
            r.raiz.localScale = Vector3.one * (elegida ? 1.06f : 1f);
        }

        var actual = _seleccion < piezas.Count ? piezas[_seleccion] : null;
        _frase.text = actual == null ? ""
            : _coleccion.Tiene(actual) ? $"«{actual.Frase}»"
            : ColeccionDeObjetos.Texto("COLECCION_PIEZA_QUE_FALTA", "Todavía no la tiene. Quizá esté por ahí, esperando a que alguien la encuentre.");

        if (pagina.premio == null) _premio.text = "";
        else if (FlagDePartida.EstaPuesto(_coleccion.ClaveDePremio(_pagina)))
            _premio.text = ColeccionDeObjetos.Texto("COLECCION_PREMIO_CONSEGUIDO", "Premio de la página conseguido");
        else
            _premio.text = string.Format(ColeccionDeObjetos.Texto("COLECCION_PREMIO_DE_LA_PAGINA", "Premio al completar la página: {0} ×{1}"),
                                         pagina.premio.GetLocalizedName(), pagina.cantidadDelPremio);
    }

    // ── Construcción ─────────────────────────────────────────────────────────────────────────

    private void ConstruirInterfaz()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;
        var escalador = gameObject.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var fondo = Imagen("Fondo", transform, null, new Color(0f, 0f, 0f, 0.6f));
        Estirar(fondo.rectTransform);

        var panel = Imagen("Panel", transform, _estilo.panel, Color.white);
        Colocar(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 720f));

        var titulo = Texto("Titulo", panel.transform, _coleccion.Titulo, _estilo.fuenteTitulo, 46f, _estilo.colorResalte);
        Colocar(titulo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 64f));

        _textoPagina = Texto("Pagina", panel.transform, "", _estilo.fuenteTexto, 26f, _estilo.colorTextoSuave);
        Colocar(_textoPagina.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1000f, 40f));

        _rejilla = new GameObject("Piezas", typeof(RectTransform)).GetComponent<RectTransform>();
        _rejilla.SetParent(panel.transform, false);
        Colocar(_rejilla, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1020f, 320f));

        _frase = Texto("Frase", panel.transform, "", _estilo.fuenteTexto, 28f, _estilo.colorTexto);
        _frase.fontStyle = FontStyles.Italic;
        Colocar(_frase.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(980f, 90f));

        _premio = Texto("Premio", panel.transform, "", _estilo.fuenteTexto, 24f, _estilo.colorTextoSuave);
        Colocar(_premio.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(980f, 36f));

        _flechaIzquierda = Boton("Pagina anterior", panel.transform, "<", new Vector2(0f, 0.5f), new Vector2(60f, 40f), new Vector2(80f, 80f), () => CambiarPagina(-1));
        _flechaDerecha = Boton("Pagina siguiente", panel.transform, ">", new Vector2(1f, 0.5f), new Vector2(-60f, 40f), new Vector2(80f, 80f), () => CambiarPagina(+1));
        Boton("Cerrar", panel.transform, ColeccionDeObjetos.Texto("UI_CERRAR", "Cerrar"), new Vector2(1f, 0f), new Vector2(-130f, 50f), new Vector2(200f, 64f), Cerrar);
    }

    private void AsegurarRanuras(int cuantas)
    {
        while (_ranuras.Count < cuantas) _ranuras.Add(CrearRanura(_ranuras.Count));
        int filas = Mathf.Max(1, Mathf.CeilToInt(cuantas / (float)PiezasPorFila));
        float lado = filas > 1 ? 150f : 280f, separacion = 36f;
        for (int i = 0; i < _ranuras.Count; i++)
        {
            int fila = i / PiezasPorFila, columna = i % PiezasPorFila;
            int enFila = Mathf.Min(PiezasPorFila, cuantas - fila * PiezasPorFila);
            float x = (columna - (enFila - 1) * 0.5f) * (lado + separacion);
            float y = ((filas - 1) * 0.5f - fila) * (lado + separacion);
            var r = _ranuras[i];
            Colocar(r.raiz, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(lado, lado));
            Colocar(r.icono.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, lado * 0.08f), Vector2.one * lado * 0.58f);
            Colocar(r.nombre.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, lado * 0.11f), new Vector2(lado - 16f, 34f));
            r.nombre.fontSize = filas > 1 ? 18f : 24f;
        }
    }

    private Ranura CrearRanura(int indice)
    {
        var r = new Ranura();
        r.tarjeta = Imagen($"Pieza {indice + 1}", _rejilla, _estilo.tarjeta, Color.white);
        r.raiz = r.tarjeta.rectTransform;
        var boton = r.tarjeta.gameObject.AddComponent<Button>();
        boton.transition = Selectable.Transition.None;
        boton.onClick.AddListener(() => Elegir(indice));
        r.icono = Imagen("Icono", r.raiz, null, Color.white);
        r.icono.preserveAspect = true;
        r.icono.raycastTarget = false;
        r.nombre = Texto("Nombre", r.raiz, "", _estilo.fuenteTexto, 24f, _estilo.colorTexto);
        r.chipNuevo = Imagen("Nuevo", r.raiz, _estilo.chip, _estilo.colorResalte);
        r.chipNuevo.raycastTarget = false;
        Colocar(r.chipNuevo.rectTransform, new Vector2(1f, 1f), new Vector2(-58f, -26f), new Vector2(110f, 38f));
        var textoNuevo = Texto("Texto", r.chipNuevo.transform, ColeccionDeObjetos.Texto("COLECCION_NUEVO", "¡Nuevo!"), _estilo.fuenteTitulo, 20f, new Color(0.15f, 0.1f, 0.25f, 1f));
        Estirar(textoNuevo.rectTransform);
        return r;
    }

    private GameObject Boton(string nombre, Transform padre, string texto, Vector2 ancla, Vector2 posicion, Vector2 tamano, UnityEngine.Events.UnityAction alPulsar)
    {
        var imagen = Imagen(nombre, padre, _estilo.boton, Color.white);
        Colocar(imagen.rectTransform, ancla, posicion, tamano);
        imagen.gameObject.AddComponent<Button>().onClick.AddListener(alPulsar);
        var t = Texto("Texto", imagen.transform, texto, _estilo.fuenteTitulo, 26f, _estilo.colorTexto);
        Estirar(t.rectTransform);
        return imagen.gameObject;
    }

    private static Image Imagen(string nombre, Transform padre, Sprite sprite, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var imagen = go.GetComponent<Image>();
        imagen.sprite = sprite;
        imagen.color = color;
        if (sprite != null && sprite.border != Vector4.zero) imagen.type = Image.Type.Sliced;
        return imagen;
    }

    private static TextMeshProUGUI Texto(string nombre, Transform padre, string texto, TMP_FontAsset fuente, float tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static void Colocar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
