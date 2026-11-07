using Core;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PiezasDeUI;

/// <summary>
/// Base de las estrategias de ronda (<see cref="IPruebaDeRonda"/>): la instrucción, los sonidos de
/// acierto y fallo, y las piezas con que se pintan (barras, agujas, glifos de botón) dentro de la
/// zona que da el concurso. Cada estrategia es un componente con sus datos en el Inspector.
/// </summary>
public abstract class PruebaDeRonda : MonoBehaviour, IPruebaDeRonda
{
    protected const float AnchoDeBarra = 760f;

    [Header("Instrucción")]
    [Tooltip("Clave del texto que enseña los controles ({0} = el botón, si la ronda usa uno).")]
    [SerializeField] protected string claveDeInstruccion;
    [Tooltip("Texto si la clave no está traducida.")]
    [SerializeField] protected string textoDeInstruccion;

    [Header("Sonido")]
    [SerializeField] protected string sfxAlAcertar = "";
    [SerializeField] protected string sfxAlFallar = "";

    protected RectTransform Zona { get; private set; }
    protected EstiloDeMenuSencillo Estilo { get; private set; }

    public virtual string Instruccion => PresentadorDeLinea.Texto(claveDeInstruccion, textoDeInstruccion);
    public abstract float Rendimiento { get; }

    public void Empezar(RectTransform zona, EstiloDeMenuSencillo estilo)
    {
        Zona = zona;
        Estilo = estilo != null ? estilo : ScriptableObject.CreateInstance<EstiloDeMenuSencillo>();
        Preparar();
    }

    public abstract bool Actualizar(float dt);

    public void Terminar()
    {
        Recoger();
        if (Zona == null) return;
        for (int i = Zona.childCount - 1; i >= 0; i--) Destroy(Zona.GetChild(i).gameObject);
    }

    /// <summary>Deja la ronda como al principio y pinta sus piezas en <see cref="Zona"/>.</summary>
    protected abstract void Preparar();

    /// <summary>Suelta lo que tenga la estrategia (las piezas las quita la base).</summary>
    protected virtual void Recoger() { }

    protected static void Sonar(string sfx)
    {
        if (!string.IsNullOrEmpty(sfx)) AudioService.Instance?.PlaySFX(sfx);
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Nombre del glifo de un botón de cara (el de <see cref="InputGlyphNames"/>).</summary>
    protected static string GlifoDe(GamepadInputReader.BotonDeCara boton) => boton switch
    {
        GamepadInputReader.BotonDeCara.Este => InputGlyphNames.East,
        GamepadInputReader.BotonDeCara.Oeste => InputGlyphNames.West,
        GamepadInputReader.BotonDeCara.Norte => InputGlyphNames.North,
        _ => InputGlyphNames.South,
    };

    /// <summary>Lo que pone en el botón con el mando o teclado en uso («A», «Q», «clic derecho»).</summary>
    protected static string EtiquetaDe(GamepadInputReader.BotonDeCara boton) =>
        InputGlyphLabels.GetLabel(GlifoDe(boton), InputGlyphService.CurrentFamily);

    /// <summary>Barra horizontal centrada en la zona, a la altura <paramref name="y"/>.</summary>
    protected Image Barra(string nombre, float y, float ancho, float alto, Color color, Sprite sprite = null)
    {
        var barra = Imagen(nombre, Zona, sprite != null ? sprite : Estilo.tarjeta, color);
        Colocar(barra.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(ancho, alto));
        return barra;
    }

    /// <summary>Relleno que crece desde el borde izquierdo de <paramref name="barra"/> (se ajusta con <see cref="Llenar"/>).</summary>
    protected Image Relleno(string nombre, Image barra, Color color)
    {
        var relleno = Imagen(nombre, barra.transform, Estilo.tarjeta, color);
        var rt = relleno.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);
        return relleno;
    }

    /// <summary>Llena de 0 a 1 un relleno hecho con <see cref="Relleno"/>.</summary>
    protected static void Llenar(Image relleno, float fraccion)
    {
        var padre = (RectTransform)relleno.rectTransform.parent;
        relleno.rectTransform.sizeDelta = new Vector2(padre.rect.width * Mathf.Clamp01(fraccion), 0f);
    }

    /// <summary>Glifo de un botón (con su etiqueta encima si no hay dibujo para el mando en uso).</summary>
    protected Image Glifo(string nombre, Transform padre, GamepadInputReader.BotonDeCara boton, Vector2 posicion, float lado)
    {
        var sprite = InputGlyphService.GetSprite(GlifoDe(boton));
        var imagen = Imagen(nombre, padre, sprite, Color.white);
        imagen.type = Image.Type.Simple;
        imagen.preserveAspect = true;
        Colocar(imagen.rectTransform, new Vector2(0.5f, 0.5f), posicion, new Vector2(lado, lado));
        if (sprite == null)
        {
            imagen.sprite = Estilo.chip;
            var t = Texto("Etiqueta", imagen.transform, EtiquetaDe(boton), Estilo.fuenteTitulo, lado * 0.4f, Estilo.colorTexto);
            Estirar(t.rectTransform);
        }
        return imagen;
    }

    protected TextMeshProUGUI Rotulo(string nombre, string texto, Vector2 posicion, float tamano)
    {
        var t = Texto(nombre, Zona, texto, Estilo.fuenteTitulo, tamano, Estilo.colorResalte);
        Colocar(t.rectTransform, new Vector2(0.5f, 0.5f), posicion, new Vector2(AnchoDeBarra, tamano * 1.6f));
        return t;
    }
}
