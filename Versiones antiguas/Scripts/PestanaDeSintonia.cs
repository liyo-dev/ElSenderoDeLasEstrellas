using System;
using TMPro;
using Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Tipo = SintoniaEstelarService.TipoEleccionSintonia;

namespace Sendero.UI
{
    /// <summary>
    /// Pestaña Sintonía del menú de Start: el medidor de Sintonía Estelar, los dones por elegir
    /// y la elección entre Vigor (+vida máxima) y Flujo (+maná máximo) con la barra
    /// apilada de ahora y de después. El don es para todo el grupo
    /// (<see cref="SintoniaEstelarService.AplicarEleccion"/>).
    ///
    /// Se monta por código dentro del panel de las pestañas, copiando el fondo y la fuente de las
    /// demás (<see cref="Crear"/>); el menú la enseña, la esconde y le pasa la entrada con
    /// <see cref="Tick"/> mientras está a la vista. La cruceta elige, A confirma; con ratón, un clic
    /// elige y otro confirma.
    /// </summary>
    public sealed class PestanaDeSintonia : MonoBehaviour
    {
        private static readonly Color Texto = new Color32(0xEE, 0xF0, 0xFF, 0xFF);
        private static readonly Color TextoSuave = new Color32(0xC9, 0xCD, 0xF0, 0xFF);
        private static readonly Color Oro = new Color32(0xF5, 0xC5, 0x42, 0xFF);
        private static readonly Color Tarjeta = new Color32(0x1D, 0x1F, 0x42, 0xE6);
        private static readonly Color FondoDeBarra = new Color32(0x12, 0x11, 0x2E, 0xFF);
        private static readonly Color ColorVigor = new Color32(0x4C, 0xD0, 0x7D, 0xFF);
        private static readonly Color ColorFlujo = new Color32(0x78, 0xA6, 0xFF, 0xFF);

        private TMP_FontAsset _fuente;
        private TextMeshProUGUI _estrellas, _progresoTexto, _hitosTexto, _pista;
        private Image _progreso;
        private OpcionDeDon _vigor, _flujo;
        private int _elegido;
        private Action _alElegir;

        /// Una de las dos tarjetas (Vigor o Flujo).
        private sealed class OpcionDeDon
        {
            public Tipo tipo;
            public Color color;
            public Image marco;
            public CanvasGroup grupo;
            public FilaDeBarra ahora, despues;
        }

        /// Barra apilada pequeña con su «×N» y el máximo.
        private sealed class FilaDeBarra
        {
            public Image debajo, arriba;
            public RectTransform rellenoDeArriba;
            public TextMeshProUGUI capas, maximo;
        }

        /// Monta la pestaña dentro de <paramref name="padre"/> con el fondo de <paramref name="fondoDeReferencia"/>.
        public static PestanaDeSintonia Crear(RectTransform padre, Image fondoDeReferencia, TMP_FontAsset fuente, Action alElegir)
        {
            var go = new GameObject("SintoniaPanel", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            PiezasDeUI.Estirar(rt);
            var fondo = go.GetComponent<Image>();
            if (fondoDeReferencia != null)
            {
                fondo.sprite = fondoDeReferencia.sprite;
                fondo.type = fondoDeReferencia.type;
                fondo.color = fondoDeReferencia.color;
                fondo.pixelsPerUnitMultiplier = fondoDeReferencia.pixelsPerUnitMultiplier;
                fondo.material = fondoDeReferencia.material;
            }
            else fondo.color = new Color(0.07f, 0.07f, 0.18f, 0.85f);
            var pestana = go.AddComponent<PestanaDeSintonia>();
            pestana._fuente = fuente != null ? fuente : TMP_Settings.defaultFontAsset;
            pestana._alElegir = alElegir;
            pestana.Montar(rt);
            go.SetActive(false);
            return pestana;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (visible) Refrescar();
        }

        // ─── Montaje ───────────────────────────────────────────────────────────

        private void Montar(RectTransform raiz)
        {
            var titulo = Txt("Titulo", raiz, "Sintonía Estelar", 40f, Texto, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Arriba(titulo.rectTransform, 30f, 24f, 600f, 50f);
            var sub = Txt("Subtitulo", raiz, "Se llena combatiendo y purificando", 22f, TextoSuave, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Arriba(sub.rectTransform, 30f, 74f, 600f, 30f);
            _estrellas = Txt("Estrellas", raiz, "", 26f, Oro, FontStyles.Bold, TextAlignmentOptions.TopRight);
            ArribaDerecha(_estrellas.rectTransform, 30f, 30f, 400f, 70f);

            var barra = PiezasDeUI.Imagen("Progreso", raiz, PiezasDeUI.RectanguloRedondeado, FondoDeBarra);
            barra.type = Image.Type.Sliced;
            barra.pixelsPerUnitMultiplier = 3f;
            Arriba(barra.rectTransform, 30f, 118f, 890f, 22f);
            _progreso = PiezasDeUI.Imagen("Relleno", barra.transform, PiezasDeUI.RectanguloRedondeado, Oro);
            _progreso.type = Image.Type.Sliced;
            _progreso.pixelsPerUnitMultiplier = 3f;
            var rtp = _progreso.rectTransform;
            rtp.anchorMin = Vector2.zero; rtp.anchorMax = new Vector2(0f, 1f); rtp.pivot = new Vector2(0f, 0.5f);
            rtp.offsetMin = new Vector2(3f, 3f); rtp.offsetMax = new Vector2(3f, -3f);
            _progresoTexto = Txt("ProgresoTexto", raiz, "", 20f, TextoSuave, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Arriba(_progresoTexto.rectTransform, 30f, 148f, 500f, 28f);
            _hitosTexto = Txt("HitosTexto", raiz, "", 20f, TextoSuave, FontStyles.Normal, TextAlignmentOptions.TopRight);
            ArribaDerecha(_hitosTexto.rectTransform, 30f, 148f, 400f, 28f);

            _vigor = CrearTarjeta(raiz, 30f, Tipo.Vigor, ColorVigor, "Vigor",
                "+" + SintoniaEstelarService.VidaPorVigor + " de vida máxima",
                "Aguantas más golpes y más errores.", "Vida");
            _flujo = CrearTarjeta(raiz, 30f + 425f + 40f, Tipo.Flujo, ColorFlujo, "Flujo",
                "+" + SintoniaEstelarService.ManaPorFlujo + " de magia máxima",
                "Más reserva para el escudo, los combos y los hechizos potentes.", "Magia");

            _pista = Txt("Pista", raiz, "", 20f, TextoSuave, FontStyles.Normal, TextAlignmentOptions.Bottom);
            var rtPista = _pista.rectTransform;
            rtPista.anchorMin = new Vector2(0f, 0f); rtPista.anchorMax = new Vector2(1f, 0f); rtPista.pivot = new Vector2(0.5f, 0f);
            rtPista.anchoredPosition = new Vector2(0f, 16f); rtPista.sizeDelta = new Vector2(-60f, 30f);
        }

        private OpcionDeDon CrearTarjeta(RectTransform raiz, float x, Tipo tipo, Color color, string nombre, string ganancia, string descripcion, string queSube)
        {
            var op = new OpcionDeDon { tipo = tipo, color = color };
            var marco = PiezasDeUI.Imagen(nombre, raiz, PiezasDeUI.RectanguloRedondeado, color);
            marco.type = Image.Type.Sliced;
            marco.pixelsPerUnitMultiplier = 1.5f;
            Arriba(marco.rectTransform, x, 196f, 425f, 290f);
            op.marco = marco;
            op.grupo = marco.gameObject.AddComponent<CanvasGroup>();
            var boton = marco.gameObject.AddComponent<Button>();
            boton.transition = Selectable.Transition.None;
            boton.navigation = new Navigation { mode = Navigation.Mode.None };
            int indice = tipo == Tipo.Vigor ? 0 : 1;
            boton.onClick.AddListener(() => AlClic(indice));

            var dentro = PiezasDeUI.Imagen("Dentro", marco.transform, PiezasDeUI.RectanguloRedondeado, Tarjeta);
            dentro.type = Image.Type.Sliced;
            dentro.pixelsPerUnitMultiplier = 1.5f;
            dentro.raycastTarget = false;
            var rtd = dentro.rectTransform;
            rtd.anchorMin = Vector2.zero; rtd.anchorMax = Vector2.one;
            rtd.offsetMin = new Vector2(4f, 4f); rtd.offsetMax = new Vector2(-4f, -4f);
            var c = (RectTransform)dentro.transform;

            var t = Txt("Nombre", c, nombre, 34f, Texto, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Arriba(t.rectTransform, 22f, 16f, 380f, 44f);
            var g = Txt("Ganancia", c, ganancia, 22f, color, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Arriba(g.rectTransform, 22f, 60f, 380f, 30f);
            var d = Txt("Descripcion", c, descripcion, 18f, TextoSuave, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            d.textWrappingMode = TextWrappingModes.Normal;
            Arriba(d.rectTransform, 22f, 94f, 380f, 50f);
            var cq = Txt("ComoQueda", c, "CÓMO QUEDA LA BARRA DE " + queSube.ToUpperInvariant(), 15f, TextoSuave, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Arriba(cq.rectTransform, 22f, 158f, 380f, 22f);
            op.ahora = Fila(c, "Ahora", 188f);
            op.despues = Fila(c, "Después", 226f);
            return op;
        }

        private FilaDeBarra Fila(RectTransform padre, string etiqueta, float y)
        {
            var f = new FilaDeBarra();
            var e = Txt("Etiqueta", padre, etiqueta, 17f, TextoSuave, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Arriba(e.rectTransform, 22f, y, 80f, 24f);
            var fondo = PiezasDeUI.Imagen("Barra " + etiqueta, padre, null, FondoDeBarra);
            fondo.raycastTarget = false;
            Arriba(fondo.rectTransform, 104f, y + 3f, 200f, 18f);
            f.debajo = PiezasDeUI.Imagen("Debajo", fondo.transform, null, Color.white);
            PiezasDeUI.Estirar(f.debajo.rectTransform);
            f.debajo.raycastTarget = false;
            f.arriba = PiezasDeUI.Imagen("Arriba", fondo.transform, null, Color.white);
            f.arriba.raycastTarget = false;
            f.rellenoDeArriba = f.arriba.rectTransform;
            f.rellenoDeArriba.anchorMin = Vector2.zero; f.rellenoDeArriba.anchorMax = new Vector2(0f, 1f);
            f.rellenoDeArriba.pivot = new Vector2(0f, 0.5f);
            f.rellenoDeArriba.offsetMin = Vector2.zero; f.rellenoDeArriba.offsetMax = Vector2.zero;
            f.capas = Txt("Capas", padre, "", 17f, Texto, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Arriba(f.capas.rectTransform, 312f, y, 40f, 24f);
            f.maximo = Txt("Maximo", padre, "", 17f, TextoSuave, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            Arriba(f.maximo.rectTransform, 340f, y, 56f, 24f);
            return f;
        }

        // ─── Estado ────────────────────────────────────────────────────────────

        public void Refrescar()
        {
            var s = SintoniaEstelarService.Instance;
            int pendientes = s != null ? s.HitosPendientes : 0;
            int puntos = s != null ? s.PuntosActuales : 0;
            int umbral = s != null ? s.PuntosParaSiguienteHito : 100;
            int alcanzados = s != null ? s.HitosAlcanzados : 0;

            _estrellas.text = pendientes > 0
                ? "<size=150%>" + pendientes + "</size>\n<size=75%>" + (pendientes == 1 ? "don por elegir" : "dones por elegir") + "</size>"
                : "<color=#A9AED6><size=80%>Nada por elegir</size></color>";
            float fraccion = umbral > 0 ? Mathf.Clamp01((float)puntos / umbral) : 0f;
            var rtp = _progreso.rectTransform;
            float ancho = ((RectTransform)rtp.parent).rect.width - 6f;
            rtp.sizeDelta = new Vector2(Mathf.Max(0f, ancho * fraccion), rtp.sizeDelta.y);
            _progreso.enabled = fraccion > 0.001f;
            _progresoTexto.text = puntos + " / " + umbral + " para el siguiente hito";
            _hitosTexto.text = "Hitos alcanzados: " + alcanzados;

            LeerMaximos(out float vidaMax, out float manaMax);
            var estilo = EstiloDeBarrasApiladas.Actual;
            PonerFila(_vigor.ahora, vidaMax, estilo, estilo.coloresDeVida);
            PonerFila(_vigor.despues, vidaMax + SintoniaEstelarService.VidaPorVigor, estilo, estilo.coloresDeVida);
            PonerFila(_flujo.ahora, manaMax, estilo, estilo.coloresDeMana);
            PonerFila(_flujo.despues, manaMax + SintoniaEstelarService.ManaPorFlujo, estilo, estilo.coloresDeMana);

            bool hay = pendientes > 0;
            PintarSeleccion(hay);
            _pista.text = hay
                ? "<b>A</b>  Elegir este don      <b>Cruceta</b>  Cambiar      Es para todo el grupo"
                : "Gana combates y purifica amenazas para llenar la Sintonía.";
        }

        private void PintarSeleccion(bool hay)
        {
            Pintar(_vigor, hay, _elegido == 0);
            Pintar(_flujo, hay, _elegido == 1);
        }

        private static void Pintar(OpcionDeDon op, bool hay, bool elegido)
        {
            op.grupo.alpha = hay ? 1f : 0.55f;
            op.marco.color = hay && elegido ? op.color : new Color32(0x4B, 0x4F, 0x8C, 0xFF);
        }

        private static void PonerFila(FilaDeBarra f, float valor, EstiloDeBarrasApiladas estilo, Color[] colores)
        {
            int capas = estilo.Capas(valor);
            f.arriba.color = EstiloDeBarrasApiladas.ColorDeCapa(colores, capas - 1);
            f.debajo.color = capas > 1 ? EstiloDeBarrasApiladas.ColorDeCapa(colores, capas - 2) : FondoDeBarra;
            float ancho = ((RectTransform)f.rellenoDeArriba.parent).rect.width;
            f.rellenoDeArriba.sizeDelta = new Vector2(ancho * estilo.ParteDeArriba(valor), 0f);
            f.capas.text = capas > 1 ? "×" + capas : "";
            f.maximo.text = Mathf.RoundToInt(valor).ToString();
        }

        private static void LeerMaximos(out float vida, out float mana)
        {
            vida = 0f; mana = 0f;
            if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var salud, includeInactive: true, allowSceneLookup: true))
                vida = salud.MaxHealth;
            if (PlayerService.TryGetComponent<ManaPool>(out var pool, includeInactive: true, allowSceneLookup: true))
                mana = pool.Max;
        }

        // ─── Entrada ───────────────────────────────────────────────────────────

        /// Lo llama el menú cada frame mientras esta pestaña está a la vista.
        public void Tick()
        {
            if (SintoniaEstelarService.Instance == null || SintoniaEstelarService.Instance.HitosPendientes <= 0) return;
            int dir = 0;
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame) dir = -1;
                else if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame) dir = 1;
            }
            var kb = Keyboard.current;
            if (dir == 0 && kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dir = -1;
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dir = 1;
            }
            if (dir != 0)
            {
                int nuevo = Mathf.Clamp(_elegido + dir, 0, 1);
                if (nuevo != _elegido)
                {
                    _elegido = nuevo;
                    GamepadInputReader.PlayUISound("UI_Navigate");
                    PintarSeleccion(true);
                }
            }
            if (GamepadInputReader.SubmitPressed) Elegir();
        }

        private void AlClic(int indice)
        {
            if (SintoniaEstelarService.Instance == null || SintoniaEstelarService.Instance.HitosPendientes <= 0) return;
            if (_elegido != indice)
            {
                _elegido = indice;
                GamepadInputReader.PlayUISound("UI_Navigate");
                PintarSeleccion(true);
                return;
            }
            Elegir();
        }

        private void Elegir()
        {
            var s = SintoniaEstelarService.Instance;
            if (s == null || s.HitosPendientes <= 0) return;
            s.AplicarEleccion(_elegido == 0 ? Tipo.Vigor : Tipo.Flujo);
            GamepadInputReader.PlayUISound("UI_Submit");
            Refrescar();
            _alElegir?.Invoke();
        }

        // ─── Piezas ────────────────────────────────────────────────────────────

        private TextMeshProUGUI Txt(string nombre, Transform padre, string texto, float tamano, Color color, FontStyles estilo, TextAlignmentOptions alineacion)
        {
            var t = PiezasDeUI.Texto(nombre, padre, texto, _fuente, tamano, color);
            t.fontStyle = estilo;
            t.alignment = alineacion;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// Coloca arriba a la izquierda del padre, a <paramref name="x"/> del borde izquierdo y <paramref name="y"/> del de arriba.
        private static void Arriba(RectTransform rt, float x, float y, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(ancho, alto);
        }

        /// Coloca arriba a la derecha del padre.
        private static void ArribaDerecha(RectTransform rt, float x, float y, float ancho, float alto)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-x, -y);
            rt.sizeDelta = new Vector2(ancho, alto);
        }
    }
}
