using UnityEngine;

/// <summary>
/// Colores de la interfaz. Lo que convive con el mundo (diálogo, avisos, HUD) va en papel crema
/// con borde de tinta, como el bocadillo; los menús a pantalla completa ponen ese mismo papel
/// sobre el fondo de noche. La UI que se construye por código toma los colores de aquí en vez de
/// escribir los suyos, para que todo hable el mismo idioma visual.
/// </summary>
public static class PaletaUI
{
    /// Lavanda de los bordes de popups y botones seleccionados. Color principal de acento.
    public static readonly Color Lavanda = new Color(0.627f, 0.506f, 0.961f, 1f);   // #A081F5

    /// Azul pervinca del anillo de los iconos de botón. Acento secundario.
    public static readonly Color Pervinca = new Color(0.490f, 0.506f, 0.894f, 1f);  // #7D81E4

    /// Fondo oscuro de los paneles de cristal (popups, menús).
    public static readonly Color FondoPanel = new Color(0.106f, 0.086f, 0.180f, 0.92f); // #1B162E

    /// Pista vacía de una barra de progreso sobre el mundo.
    public static readonly Color PistaBarra = new Color(0.106f, 0.086f, 0.180f, 0.75f);

    /// Papel crema de las hojas, el mismo del bocadillo.
    public static readonly Color Papel = new Color(0.965f, 0.914f, 0.82f, 1f);          // #F6E9D1

    /// Filo claro que recorre el papel por dentro del borde, y texto sobre fondos oscuros.
    public static readonly Color PapelClaro = new Color(1f, 0.973f, 0.91f, 1f);          // #FFF8E8

    /// Papel de lo que no está elegido (una opción sin el foco, una pestaña en reposo).
    public static readonly Color PapelApagado = new Color(0.918f, 0.851f, 0.733f, 1f);   // #EAD9BB

    /// Tinta marrón de los bordes del papel.
    public static readonly Color Tinta = new Color(0.38f, 0.30f, 0.23f, 1f);             // #614D3B

    /// Tinta del texto escrito sobre papel.
    public static readonly Color TintaDeTexto = new Color(0.14f, 0.12f, 0.10f, 1f);      // #241F1A

    /// Dorado de lo elegido y de las estrellas.
    public static readonly Color Oro = new Color(1f, 0.831f, 0.369f, 1f);                // #FFD45E

    /// Dorado oscuro que se lee sobre el papel: títulos y resaltes escritos en la hoja.
    public static readonly Color OroSobrePapel = new Color(0.54f, 0.35f, 0f, 1f);                // #8A5900

    /// Morado de noche: fondo de los menús y de las piezas oscuras que van sobre papel.
    public static readonly Color Noche = new Color(0.169f, 0.137f, 0.267f, 1f);          // #2B2344
}
