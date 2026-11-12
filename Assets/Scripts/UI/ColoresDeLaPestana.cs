using System;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Color de la pestaña con el nombre de quien habla o va al mando (caja de diálogo, pregunta de
/// Sí/No, HUD). Los del grupo llevan el color de su ficha (<see cref="FichaDePersonaje.ColorDelPersonaje"/>),
/// el mismo de su halo; el resto, noche con letra dorada. La letra sale clara u oscura según
/// lo claro que sea el fondo.
/// </summary>
public static class ColoresDeLaPestana
{
    static readonly Color[] s_colores = new Color[3];
    static readonly bool[] s_buscado = new bool[3];

    /// Colores para la pestaña de <paramref name="nombre"/> (tal como se ve escrito).
    public static void De(string nombre, out Color fondo, out Color letra)
    {
        for (int i = 0; i < 3; i++)
        {
            var slot = (Slot)i;
            if (string.Equals(nombre, ControlDelJugador.Nombre(slot), StringComparison.OrdinalIgnoreCase))
            {
                De(slot, out fondo, out letra);
                return;
            }
        }
        fondo = PaletaUI.Noche;
        letra = PaletaUI.Oro;
    }

    /// Colores para la pestaña de un personaje del grupo.
    public static void De(Slot personaje, out Color fondo, out Color letra)
    {
        int i = (int)personaje;
        if (!s_buscado[i] || s_colores[i].a <= 0f)
        {
            var ficha = FichaDePersonaje.Buscar(personaje);
            s_colores[i] = ficha != null ? ficha.ColorDelPersonaje : Color.clear;
            s_buscado[i] = true;
        }
        if (s_colores[i].a <= 0f)
        {
            fondo = PaletaUI.Noche;
            letra = PaletaUI.Oro;
            return;
        }
        fondo = s_colores[i];
        letra = LetraSobre(fondo);
    }

    /// Tinta sobre fondos claros y papel claro sobre los oscuros.
    public static Color LetraSobre(Color fondo)
    {
        float luz = 0.299f * fondo.r + 0.587f * fondo.g + 0.114f * fondo.b;
        return luz > 0.55f ? PaletaUI.TintaDeTexto : PaletaUI.PapelClaro;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Array.Clear(s_buscado, 0, s_buscado.Length);
#endif
}
