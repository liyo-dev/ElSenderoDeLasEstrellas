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
    /// Colores y piezas del menú de Start (<see cref="MenuDeStart"/>), las de su maqueta: hojas,
    /// etiquetas, píldoras, casillas (llena, elegida, vacía con raya discontinua), textos en Nunito
    /// y en Baloo 2, iconos del mando activo y botones sin navegación (el menú lleva la suya). Se
    /// colocan con <see cref="PiezasDeUI.ColocarArribaIzquierda"/> y las medidas de la maqueta.
    /// Lo que va directamente sobre el fondo de noche usa <see cref="SobreNoche"/>.
    /// </summary>
    public static class PiezasDelMenuDeStart
    {
        public static readonly Color Oro = PaletaUI.Oro;
        /// Dorado oscuro para escribir sobre papel (el dorado de <see cref="Oro"/> es para rellenos y bordes).
        public static readonly Color OroDeTexto = new Color32(0x8A, 0x5A, 0x00, 0xFF);
        /// Texto escrito directamente sobre el fondo de noche (cabecera, pistas, notas).
        public static readonly Color SobreNoche = new Color32(0xEE, 0xE6, 0xD8, 0xFF);

        /// Fuente de los textos del menú (Nunito; la pone el menú al montarse).
        public static TMP_FontAsset Fuente;
        /// Fuente de los títulos, nombres y botones (Baloo 2; la pone el menú al montarse).
        public static TMP_FontAsset FuenteTitulos;

        // Colores de las maquetas que no son de la paleta.
        public static readonly Color TextoMaqueta = new Color32(0x6B, 0x58, 0x44, 0xFF);
        public static readonly Color OroBorde = new Color32(0xE0, 0xA1, 0x00, 0xFF);
        public static readonly Color FondoElegida = new Color32(0xFF, 0xF3, 0xC4, 0xFF);
        public static readonly Color Discontinua = new Color32(0xA0, 0x8C, 0x74, 0xFF);
        public static readonly Color FondoHueco = new Color32(0x61, 0x4D, 0x3B, 0x1A);

        public static TextMeshProUGUI Txt(string nombre, Transform padre, string texto, float tamano, Color color,
            bool negrita = false, TextAlignmentOptions alineacion = TextAlignmentOptions.TopLeft, bool envolver = false)
        {
            var t = PiezasDeUI.Texto(nombre, padre, texto, Fuente, tamano, color);
            t.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = alineacion;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = envolver ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            // Nunca recortado: la letra de los títulos tiene mucho interlineado y, en huecos justos de
            // alto, el recorte se comería la línea entera.
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// Texto que puede llevar iconos de botones (<see cref="Boton"/>).
        public static TextMeshProUGUI TxtConIconos(string nombre, Transform padre, string texto, float tamano, Color color,
            TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft)
        {
            var t = Txt(nombre, padre, texto, tamano, color, false, alineacion);
            InputGlyphService.UsarIconos(t);
            t.gameObject.AddComponent<IconoDeBotonVivo>();
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
        public static ClicSinFoco Clicable(Image imagen, Action alPulsar, Action alPasar = null)
        {
            imagen.raycastTarget = true;
            var c = imagen.gameObject.AddComponent<ClicSinFoco>();
            c.AlPulsar += alPulsar;
            if (alPasar != null) c.AlPasar += alPasar;
            return c;
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

        // ─── Piezas de las maquetas nuevas (papel con borde, filo y sombra) ───────

        /// Título, nombre o botón en Baloo 2.
        public static TextMeshProUGUI Titulo(string nombre, Transform padre, string texto, float tamano, Color color,
            TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft, bool envolver = false)
        {
            var t = Txt(nombre, padre, texto, tamano, color, false, alineacion, envolver);
            if (FuenteTitulos != null) t.font = FuenteTitulos;
            return t;
        }

        /// Hoja de papel grande (paneles de las pestañas): radio 22 y sombra de 7.
        public static PiezasDeUI.Carta Hoja(string nombre, Transform padre, float x, float y, float ancho, float alto)
        {
            var c = PiezasDeUI.NuevaCarta(nombre, padre, PaletaUI.Papel, 22f);
            PiezasDeUI.ColocarArribaIzquierda(c.raiz, x, y, ancho, alto);
            return c;
        }

        /// Etiqueta dorada que cuelga sobre el borde de una hoja, con su título.
        public static TextMeshProUGUI Etiqueta(string nombre, Transform padre, string texto, float x, float y, float ancho, float alto, float tamano, float giro = -2f)
        {
            var c = PiezasDeUI.NuevaCarta(nombre, padre, PaletaUI.Oro, 16f);
            PiezasDeUI.ColocarArribaIzquierda(c.raiz, x, y, ancho, alto, giro);
            var t = Titulo("Texto", c.contenido, texto, tamano, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center);
            Rellenar(t.rectTransform);
            t.margin = new Vector4(6f, -3f, 6f, 0f);
            return t;
        }

        /// Píldora con borde oscuro y texto centrado (elemento de un hechizo, maná, tipo de objeto).
        public static TextMeshProUGUI Pastilla(string nombre, Transform padre, Color fondo, float x, float y, float ancho, float alto, float tamano, out Image relleno)
        {
            var borde = PiezasDeUI.Redondeado(nombre, padre, PiezasDeUI.TintaOscura, alto * 0.5f);
            PiezasDeUI.ColocarArribaIzquierda(borde.rectTransform, x, y, ancho, alto);
            relleno = PiezasDeUI.Redondeado("Relleno", borde.transform, fondo, alto * 0.5f - 3f);
            PiezasDeUI.Margen(relleno.rectTransform, 3f);
            var t = Txt("Texto", borde.transform, "", tamano, PiezasDeUI.TintaOscura, true, TextAlignmentOptions.Center);
            Rellenar(t.rectTransform);
            return t;
        }

        /// Icono del botón del mando o teclado activo, solo, de <paramref name="lado"/> unidades (cabeceras).
        public static TextMeshProUGUI Glifo(string nombre, Transform padre, string glifo, float x, float y, float lado)
        {
            var t = TxtConIconos(nombre, padre, Boton(glifo), lado * 0.82f, SobreNoche, TextAlignmentOptions.Center);
            PiezasDeUI.ColocarArribaIzquierda(t.rectTransform, x, y, lado, lado);
            return t;
        }

        /// Marco de raya discontinua (los huecos vacíos de las maquetas): cuatro tiras de trazos.
        public static RectTransform Discontinuo(string nombre, Transform padre)
        {
            var rt = Vacio(nombre, padre);
            var fondo = PiezasDeUI.Redondeado("Fondo", rt, FondoHueco, 20f);
            PiezasDeUI.Estirar(fondo.rectTransform);
            for (int i = 0; i < 4; i++)
            {
                var tira = PiezasDeUI.Imagen("Tira", rt, Trazos, Discontinua);
                tira.type = Image.Type.Tiled;
                tira.raycastTarget = false;
                var r = tira.rectTransform;
                bool horizontal = i < 2;
                r.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                r.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                r.pivot = new Vector2(horizontal ? 0.5f : (i == 2 ? 0f : 1f), horizontal ? (i == 0 ? 1f : 0f) : 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = horizontal ? new Vector2(-28f, 3f) : new Vector2(3f, -28f);
                if (!horizontal) { tira.sprite = TrazosVerticales; }
            }
            return rt;
        }

        private static Sprite s_puntitos;

        /// Puntos de 4 cada 18 (las líneas discontinuas de la constelación), para repetir en mosaico a lo largo.
        public static Sprite Puntitos => s_puntitos != null ? s_puntitos : (s_puntitos = CrearTrazos(false, 4, 18, 4));

        private static Sprite s_trazos, s_trazosV;
        private static Sprite Trazos => s_trazos != null ? s_trazos : (s_trazos = CrearTrazos(false, 10, 18, 3));
        private static Sprite TrazosVerticales => s_trazosV != null ? s_trazosV : (s_trazosV = CrearTrazos(true, 10, 18, 3));

        private static Sprite CrearTrazos(bool vertical, int encendido, int largo, int grueso)
        {
            int w = vertical ? grueso : largo, h = vertical ? largo : grueso;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Trazos" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = (vertical ? y : x) < encendido ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }

        /// Aspecto de una casilla de las maquetas.
        public enum EstadoDeCasilla { Vacia, Llena, Elegida, Apagada }

        /// <summary>
        /// Casilla de las maquetas (objetos, hechizos, ranuras, prendas): llena es papel claro con borde
        /// de tinta y sombra; elegida, crema dorada con borde ancho de oro y una estrella en la esquina;
        /// vacía, un hueco con raya discontinua.
        /// </summary>
        public sealed class Casilla
        {
            public RectTransform raiz;
            public Image boton;
            public PiezasDeUI.Carta carta;
            public RectTransform discontinuo;
            public GameObject estrella;
            public RectTransform contenido;
            public EstadoDeCasilla estado = (EstadoDeCasilla)(-1);

            public static Casilla Crear(string nombre, Transform padre, float radio, float ladoEstrella, Action alPulsar, Action alPasar = null)
            {
                var c = new Casilla();
                c.raiz = Vacio(nombre, padre);
                c.boton = c.raiz.gameObject.AddComponent<Image>();
                c.boton.color = new Color(1f, 1f, 1f, 0f);
                if (alPulsar != null) Clicable(c.boton, alPulsar, alPasar);
                c.discontinuo = Discontinuo("Vacía", c.raiz);
                PiezasDeUI.Estirar(c.discontinuo);
                c.carta = PiezasDeUI.NuevaCarta("Carta", c.raiz, PaletaUI.PapelClaro, radio, 5f);
                PiezasDeUI.Estirar(c.carta.raiz);
                c.contenido = c.carta.contenido;
                c.estrella = PiezasDeUI.EstrellaEn("Estrella", c.raiz, 4f, 2f, ladoEstrella, 12f).gameObject;
                c.Poner(EstadoDeCasilla.Llena);
                return c;
            }

            public void Poner(EstadoDeCasilla e)
            {
                if (e == estado) return;
                estado = e;
                bool vacia = e == EstadoDeCasilla.Vacia;
                Activar(discontinuo.gameObject, vacia);
                Activar(carta.raiz.gameObject, !vacia);
                Activar(estrella, e == EstadoDeCasilla.Elegida);
                if (vacia) return;
                bool elegida = e == EstadoDeCasilla.Elegida;
                float grosor = elegida ? 5f : 3f;
                carta.borde.color = elegida ? OroBorde : PaletaUI.Tinta;
                carta.filo.color = PaletaUI.Papel;
                PiezasDeUI.Margen(carta.filo.rectTransform, grosor);
                PiezasDeUI.Margen(carta.relleno.rectTransform, grosor + 3f);
                carta.relleno.color = elegida ? FondoElegida : e == EstadoDeCasilla.Apagada ? PaletaUI.PapelApagado : PaletaUI.PapelClaro;
                carta.sombra.GetComponent<Image>().color = new Color(PiezasDeUI.SombraDeCarta.r, PiezasDeUI.SombraDeCarta.g, PiezasDeUI.SombraDeCarta.b, 0.55f);
            }
        }
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
