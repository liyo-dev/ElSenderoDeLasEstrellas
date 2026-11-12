using Core.InputGlyphs;
using UnityEditor;

namespace Sendero.EditorTools.InputGlyphs
{
    /// <summary>
    /// Menú para forzar los iconos de mando cuando se juega con un mando que llega al juego
    /// "disfrazado" (p. ej. mando de Switch 2 a través de Switch 2 Connect, que el juego ve como
    /// Xbox o PlayStation). Funciona dentro y fuera de Play; se guarda en PlayerPrefs.
    /// </summary>
    static class ForcedGamepadGlyphsMenu
    {
        const string Root = "El Sendero/Mando/Iconos de mando: ";
        const string Auto = Root + "Automático";
        const string Xbox = Root + "Xbox";
        const string Ps = Root + "PlayStation";
        const string Nintendo = Root + "Nintendo Switch";

        [MenuItem(Auto, priority = 0)] static void SetAuto() => InputGlyphService.SetForcedGamepadFamily(null);
        [MenuItem(Xbox, priority = 1)] static void SetXbox() => InputGlyphService.SetForcedGamepadFamily(InputGlyphDeviceFamily.Xbox);
        [MenuItem(Ps, priority = 2)] static void SetPs() => InputGlyphService.SetForcedGamepadFamily(InputGlyphDeviceFamily.PlayStation);
        [MenuItem(Nintendo, priority = 3)] static void SetNintendo() => InputGlyphService.SetForcedGamepadFamily(InputGlyphDeviceFamily.Switch);

        [MenuItem(Auto, true)] static bool CheckAuto() => Mark(Auto, null);
        [MenuItem(Xbox, true)] static bool CheckXbox() => Mark(Xbox, InputGlyphDeviceFamily.Xbox);
        [MenuItem(Ps, true)] static bool CheckPs() => Mark(Ps, InputGlyphDeviceFamily.PlayStation);
        [MenuItem(Nintendo, true)] static bool CheckNintendo() => Mark(Nintendo, InputGlyphDeviceFamily.Switch);

        static bool Mark(string path, InputGlyphDeviceFamily? family)
        {
            Menu.SetChecked(path, InputGlyphService.ForcedGamepadFamily == family);
            return true;
        }
    }
}
