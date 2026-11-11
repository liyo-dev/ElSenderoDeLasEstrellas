using System;
using Core;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sendero.UI
{
    /// <summary>
    /// Colores y piezas del menú de Start (<see cref="MenuDeStart"/>): paneles de papel sobre el
    /// fondo de noche (colores de <see cref="PaletaUI"/>), cajas de esquinas redondas, textos,
    /// botones sin navegación (el menú lleva la suya) y colocación desde la esquina de arriba a la
    /// izquierda del padre, en unidades de un lienzo de 1920×1080. Los colores de texto son para
    /// escribir sobre papel; lo que va directamente sobre el fondo usa <see cref="SobreNoche"/>.
    /// </summary>
    public static class PiezasDelMenuDeStart
    {
        public static readonly Color Fondo = new Color32(0x1B, 0x15, 0x30, 0xFF);
        public static readonly Color FondoAlto = new Color32(0x3A, 0x2E, 0x63, 0xFF);
        public static readonly Color Panel = PaletaUI.Papel;
        public static readonly Color Borde = PaletaUI.Tinta;
        public static readonly Color BordeApagado = new Color32(0xA0, 0x8C, 0x74, 0xFF);
        public static readonly Color Oro = PaletaUI.Oro;
        /// Dorado oscuro para escribir sobre papel (el dorado de <see cref="Oro"/> es para rellenos y bordes).
        public static readonly Color OroDeTexto = new Color32(0x8A, 0x5A, 0x00, 0xFF);
        public static readonly Color Texto = PaletaUI.TintaDeTexto;
        public static readonly Color TextoSuave = PaletaUI.Tinta;
        public static readonly Color TextoApagado = new Color32(0x77, 0x64, 0x50, 0xFF);
        /// Texto escrito directamente sobre el fondo de noche (cabecera, pistas, notas).
        public static readonly Color SobreNoche = new Color32(0xEE, 0xE6, 0xD8, 0xFF);
        public static readonly Color Ficha = PaletaUI.PapelClaro;
        public static readonly Color FichaOscura = PaletaUI.PapelApagado;
        public static readonly Color Hueco = new Color32(0x61, 0x4D, 0x3B, 0x26);
        public static readonly Color Elegida = new Color32(0xFF, 0xE5, 0x96, 0xFF);
        public static readonly Color OroOscuro = PaletaUI.TintaDeTexto;
        public static readonly Color Verde = new Color32(0x25, 0x7A, 0x3A, 0xFF);
        public static readonly Color Rojo = new Color32(0xB3, 0x26, 0x1E, 0xFF);
        /// Pista vacía de una barra dibujada sobre papel.
        public static readonly Color Pista = new Color32(0x3A, 0x2C, 0x22, 0xFF);

        /// Versión oscura de un color claro (el de un elemento, por ejemplo) para escribir con él sobre papel.
        public static Color SobrePapel(Color c) => new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a);

        /// Fuente de los textos del menú (la de los menús del juego; la pone el menú al montarse).
        public static TMP_FontAsset Fuente;

        /// Caja de color con esquinas de <paramref name="radio"/> unidades.
        public static Image Caja(string nombre, Transform padre, Color color, float radio = 18f)
        {
            var img = PiezasDeUI.Imagen(nombre, padre, PiezasDeUI.RectanguloRedondeado, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 30f / Mathf.Max(1f, radio);
            img.raycastTarget = false;
            return img;
        }

        /// Caja con borde: devuelve el interior (donde va el contenido); el borde es su padre.
        public static Image CajaConBorde(string nombre, Transform padre, Color borde, Color relleno, float grosor = 3f, float radio = 24f)
        {
            var fuera = Caja(nombre, padre, borde, radio);
            var dentro = Caja("Dentro", fuera.transform, relleno, Mathf.Max(1f, radio - grosor));
            var rt = dentro.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(grosor, grosor); rt.offsetMax = new Vector2(-grosor, -grosor);
            return dentro;
        }

        public static TextMeshProUGUI Txt(string nombre, Transform padre, string texto, float tamano, Color color,
            bool negrita = false, TextAlignmentOptions alineacion = TextAlignmentOptions.TopLeft, bool envolver = false)
        {
            var t = PiezasDeUI.Texto(nombre, padre, texto, Fuente, tamano, color);
            t.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = alineacion;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = envolver ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = envolver ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            return t;
        }

        /// Texto que puede llevar iconos de botones (<see cref="Boton"/>).
        public static TextMeshProUGUI TxtConIconos(string nombre, Transform padre, string texto, float tamano, Color color,
            TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft)
        {
            var t = Txt(nombre, padre, texto, tamano, color, false, alineacion);
            InputGlyphService.UsarIconos(t);
            return t;
        }

        /// Icono del botón del mando o teclado activo para un texto con iconos.
        public static string Boton(string glifo) => InputGlyphService.SpriteTag(glifo);

        public static RectTransform Vacio(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            return (RectTransform)go.transform;
        }

        /// Hace clicable una imagen sin cambiar su aspecto ni entrar en la navegación de Unity.
        public static Button Clicable(Image imagen, Action alPulsar)
        {
            imagen.raycastTarget = true;
            var b = imagen.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => alPulsar?.Invoke());
            return b;
        }

        /// Coloca desde la esquina de arriba a la izquierda del padre.
        public static void Colocar(RectTransform rt, float x, float y, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(ancho, alto);
        }

        /// Coloca desde la esquina de arriba a la derecha del padre.
        public static void ColocarDerecha(RectTransform rt, float x, float y, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-x, -y);
            rt.sizeDelta = new Vector2(ancho, alto);
        }

        /// Ocupa el padre dejando un margen por cada lado.
        public static void Rellenar(RectTransform rt, float margen = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen); rt.offsetMax = new Vector2(-margen, -margen);
        }

        public static void Activar(GameObject go, bool activo)
        {
            if (go != null && go.activeSelf != activo) go.SetActive(activo);
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>
    /// Botones del menú de Start leídos en el frame (mando, teclado): cruceta o stick para moverse,
    /// confirmar, quitar, gatillos para cambiar de personaje. Los demás (B, LB/RB, Start) los lee
    /// el controlador del menú.
    /// </summary>
    public static class EntradaDelMenu
    {
        public static int Horizontal()
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame) return -1;
                if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame) return 1;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        public static int Vertical()
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame) return -1;
                if (gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame) return 1;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return -1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        public static bool Aceptar => GamepadInputReader.SubmitPressed;
        public static bool Quitar => GamepadInputReader.XButtonPressedUI;

        /// LT (−1) o RT (+1); en teclado, Q y E.
        public static int Gatillos()
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.leftTrigger.wasPressedThisFrame) return -1;
                if (gp.rightTrigger.wasPressedThisFrame) return 1;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) return -1;
                if (kb.eKey.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        /// Giro del personaje con el stick derecho (o el ratón con el botón derecho): grados por segundo.
        public static float Giro()
        {
            var gp = Gamepad.current;
            float x = gp != null ? gp.rightStick.ReadValue().x : 0f;
            var raton = Mouse.current;
            if (Mathf.Abs(x) < 0.2f && raton != null && raton.rightButton.isPressed) x = raton.delta.ReadValue().x * 0.08f;
            return Mathf.Abs(x) < 0.2f ? 0f : x * 180f;
        }

        public static void Sonido(string clave) => GamepadInputReader.PlayUISound(clave);
    }
}
