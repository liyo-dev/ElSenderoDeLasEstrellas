/// Una pestaña del panel del LAB (PanelDelLab). Cada sistema del laboratorio dibuja la suya con
/// GUILayout, con los estilos de EstiloDelLab; el panel pone la ventana, las pestañas y el scroll.
public interface ISeccionDelLab
{
    string Titulo { get; }
    /// Posición de la pestaña (de menor a mayor).
    int Orden { get; }
    void Dibujar();
}
