using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PiezasDeUI;

/// <summary>
/// Lo que se ve de un <see cref="ConcursoPorRondas"/>: el marcador arriba (rival y jugador), el
/// título de la ronda y, abajo, el panel donde se pinta la prueba del jugador con su instrucción.
/// Se monta por código (<see cref="PiezasDeUI"/>) con el <see cref="EstiloDeMenuSencillo"/> del
/// concurso y se destruye al acabar. Se esconde mientras los avisos están callados (un menú, una
/// cinemática: <see cref="ArbitroDeAvisos.Silenciado"/>).
/// </summary>
public sealed class ConcursoUI : MonoBehaviour
{
    private EstiloDeMenuSencillo _estilo;
    private Canvas _canvas;
    private GameObject _marcador, _panel;
    private TextMeshProUGUI _textoDelMarcador, _titulo, _instruccion;

    /// <summary>Donde pinta cada ronda sus piezas.</summary>
    public RectTransform Zona { get; private set; }
    public EstiloDeMenuSencillo Estilo => _estilo;

    public static ConcursoUI Crear(EstiloDeMenuSencillo estilo, bool conMarcador)
    {
        var ui = new GameObject("Concurso (pantalla)").AddComponent<ConcursoUI>();
        ui.Montar(estilo != null ? estilo : ScriptableObject.CreateInstance<EstiloDeMenuSencillo>(), conMarcador);
        return ui;
    }

    private void Montar(EstiloDeMenuSencillo estilo, bool conMarcador)
    {
        _estilo = estilo;
        _canvas = Lienzo(gameObject, 350, conClic: false);

        _marcador = Imagen("Marcador", transform, _estilo.panel, Color.white).gameObject;
        Colocar((RectTransform)_marcador.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(720f, 84f));
        _textoDelMarcador = Texto("Puntos", _marcador.transform, "", _estilo.fuenteTitulo, 38f, _estilo.colorTexto);
        Estirar(_textoDelMarcador.rectTransform);
        _marcador.SetActive(conMarcador);

        _titulo = Texto("Ronda", transform, "", _estilo.fuenteTitulo, 34f, _estilo.colorResalte);
        Colocar(_titulo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, conMarcador ? -138f : -70f), new Vector2(1200f, 48f));

        _panel = Imagen("Panel", transform, _estilo.panel, Color.white).gameObject;
        Colocar((RectTransform)_panel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(980f, 290f));
        _instruccion = Texto("Instrucción", _panel.transform, "", _estilo.fuenteTexto, 28f, _estilo.colorTexto);
        Colocar(_instruccion.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(900f, 60f));
        Zona = new GameObject("Zona de la ronda", typeof(RectTransform)).GetComponent<RectTransform>();
        Zona.SetParent(_panel.transform, false);
        Colocar(Zona, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(900f, 190f));
        _panel.SetActive(false);
    }

    private void Update()
    {
        bool ver = !ArbitroDeAvisos.Silenciado;
        if (_canvas.enabled != ver) _canvas.enabled = ver;
    }

    public void Marcador(string rival, int puntosDelRival, string jugador, int puntosDelJugador) =>
        _textoDelMarcador.text = $"{rival}  {puntosDelRival}   —   {puntosDelJugador}  {jugador}";

    public void Titulo(string texto) => _titulo.text = texto ?? "";

    /// <summary>Enseña el panel de la prueba con su instrucción, o lo esconde (con null).</summary>
    public void Panel(string instruccion)
    {
        bool ver = instruccion != null;
        if (_panel.activeSelf != ver) _panel.SetActive(ver);
        if (ver) _instruccion.text = instruccion;
    }
}
