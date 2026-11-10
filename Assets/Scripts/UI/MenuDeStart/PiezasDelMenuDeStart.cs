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
    /// Colores y piezas del menú de Start (<see cref="MenuDeStart"/>): cajas de esquinas redondas,
    /// textos, botones sin navegación (el menú lleva la suya) y colocación desde la esquina de
    /// arriba a la izquierda del padre, en unidades de un lienzo de 1920×1080.
    /// </summary>
    public static class PiezasDelMenuDeStart
    {
        public static readonly Color Fondo = new Color32(0x0D, 0x0F, 0x22, 0xFF);
        public static readonly Color FondoAlto = new Color32(0x24, 0x27, 0x50, 0xFF);
        public static readonly Color Panel = new Color32(0x12, 0x11, 0x2E, 0xDC);
        public static readonly Color Borde = new Color32(0x8A, 0x8C, 0xF2, 0xFF);
        public static readonly Color BordeApagado = new Color32(0x4B, 0x4F, 0x8C, 0xFF);
        public static readonly Color Oro = new Color32(0xF5, 0xC5, 0x42, 0xFF);
        public static readonly Color Texto = new Color32(0xEE, 0xF0, 0xFF, 0xFF);
        public static readonly Color TextoSuave = new Color32(0xC9, 0xCD, 0xF0, 0xFF);
        public static readonly Color TextoApagado = new Color32(0xA9, 0xAE, 0xD6, 0xFF);
        public static readonly Color Ficha = new Color32(0x26, 0x28, 0x4F, 0xFF);
        public static readonly Color FichaOscura = new Color32(0x1D, 0x1F, 0x42, 0xFF);
        public static readonly Color Hueco = new Color32(0x26, 0x28, 0x4F, 0x59);
        public static readonly Color Elegida = new Color32(0x3A, 0x34, 0x70, 0xFF);
        public static readonly Color OroOscuro = new Color32(0x1D, 0x1A, 0x3A, 0xFF);
        public static readonly Color Verde = new Color32(0x6F, 0xDC, 0x8C, 0xFF);
        public static readonly Color Rojo = new Color32(0xFF, 0x8A, 0x80, 0xFF);

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
