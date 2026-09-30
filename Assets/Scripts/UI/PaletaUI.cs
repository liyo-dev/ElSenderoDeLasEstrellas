using UnityEngine;

/// <summary>
/// Colores de la interfaz del juego, sacados del arte de los menús y popups (cristal oscuro con
/// borde lavanda y los anillos lavanda-azul de los iconos de botón). La UI que se construye por
/// código toma los colores de aquí en vez de escribir los suyos, para que todo hable el mismo
/// idioma visual.
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
}
