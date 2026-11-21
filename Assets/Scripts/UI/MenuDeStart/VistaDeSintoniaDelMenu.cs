using System.Collections.Generic;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = Sendero.UI.PiezasDelMenuDeStart;
using Tipo = SintoniaEstelarService.TipoEleccionSintonia;

namespace Sendero.UI
{
    /// <summary>
    /// Pestaña Sintonía del menú de Start (maqueta «4 · Sintonía»): una constelación que crece hacia
    /// la izquierda con cada don de Vida (Vigor) y hacia la derecha con cada don de Maná (Flujo), su
    /// cuenta en dos fichas, y en el centro la hoja «Sintonía Estelar» con las barras apiladas de vida
    /// y maná (<see cref="EstiloDeBarrasApiladas"/>, «×N» las capas; la raya fina es cómo quedaría) y
    /// los botones «+ Vida» / «+ Maná». El don es para todo el grupo
    /// (<see cref="SintoniaEstelarService.AplicarEleccion"/>). La cruceta elige, A confirma; con ratón,
    /// un clic elige y otro confirma. Sin dones por elegir, la hoja dice cuánto falta para el siguiente.
    /// </summary>
    public sealed class VistaDeSintoniaDelMenu : IVistaDelMenuDeStart
    {
        private static readonly Vector2 Centro = new Vector2(960f, 470f);
        // Puntos de cada rama, en el orden en que se encienden: el camino y luego el ramal de arriba.
        private static readonly Vector2[] RamaDeVida =
            { new(760f, 380f), new(560f, 330f), new(380f, 400f), new(260f, 520f), new(220f, 650f), new(700f, 250f), new(820f, 190f) };
        private static readonly Vector2[] RamaDeMana =
            { new(1160f, 380f), new(1360f, 330f), new(1540f, 400f), new(1660f, 520f), new(1700f, 650f), new(1220f, 250f), new(1100f, 190f) };
        private static readonly Color Apagada = new Color32(0x3A, 0x2E, 0x63, 0xFF);
        private static readonly Color FondoVida = new Color32(0xFF, 0xD0, 0xC8, 0xFF);
        private static readonly Color FondoMana = new Color32(0xCF, 0xE4, 0xFF, 0xFF);
        private const float AnchoDeBarra = 294f;

        private MenuDeStart _menu;
        private readonly List<(Image estrella, Image halo)> _vida = new(), _mana = new();
        private TextMeshProUGUI _cuentaVida, _cuentaMana, _pregunta;
        private Barra _barraVida, _barraMana;
        private P.Casilla _botonVida, _botonMana;
        private CanvasGroup _botones;
        private int _elegido;

        private sealed class Barra
        {
            public Image relleno, despues;
            public TextMeshProUGUI capas;
        }

        public string Titulo => "Sintonía";
        public bool UsaPersonaje => false;
        public bool UsaX => false;
        public bool UsaGatillos => false;
        private static int Pendientes => SintoniaEstelarService.Instance != null ? SintoniaEstelarService.Instance.HitosPendientes : 0;

        public string Pistas => Pendientes > 0
            ? P.Boton(InputGlyphNames.Confirm) + " Elegir     " + P.Boton(InputGlyphNames.Dpad) + " Cambiar"
            : "";

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            Rama(cuerpo, RamaDeVida, _vida);
            Rama(cuerpo, RamaDeMana, _mana);
            Estrella(cuerpo, Centro, 64f, 136f, true);

            var arte = menu.Arte;
            _cuentaVida = Ficha(cuerpo, 180f, arte != null ? arte.corazon : null, -1.5f);
            _cuentaMana = Ficha(cuerpo, 1380f, arte != null ? arte.gota : null, 1.5f);

            P.Hoja("Sintonía", cuerpo, 560f, 610f, 800f, 330f);
            P.Etiqueta("Título", cuerpo, "Sintonía Estelar", 810f, 580f, 300f, 60f, 32f, -1.5f);
            _pregunta = P.Titulo("Pregunta", cuerpo, "", 30f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center);
            PiezasDeUI.ColocarArribaIzquierda(_pregunta.rectTransform, 600f, 650f, 720f, 50f);
            _pregunta.enableAutoSizing = true; _pregunta.fontSizeMin = 20f; _pregunta.fontSizeMax = 30f;
            _barraVida = CrearBarra(cuerpo, 620f, "Vida");
            _barraMana = CrearBarra(cuerpo, 1000f, "Maná");

            var grupo = P.Vacio("Botones", cuerpo);
            P.Rellenar(grupo);
            _botones = grupo.gameObject.AddComponent<CanvasGroup>();
            _botonVida = Boton(grupo, 620f, "+ Vida", 0);
            _botonMana = Boton(grupo, 970f, "+ Maná", 1);
        }

        private static void Rama(Transform padre, Vector2[] puntos, List<(Image, Image)> estrellas)
        {
            // Camino dorado hasta el cuarto punto; de ahí al quinto y el ramal de arriba, a puntos.
            Linea(padre, Centro, puntos[0], false);
            for (int i = 0; i < 3; i++) Linea(padre, puntos[i], puntos[i + 1], false);
            Linea(padre, puntos[3], puntos[4], true);
            Linea(padre, puntos[0], puntos[5], true);
            Linea(padre, puntos[5], puntos[6], true);
            foreach (var p in puntos) estrellas.Add(Estrella(padre, p, 46f, 118f, false));
        }

        private static void Linea(Transform padre, Vector2 a, Vector2 b, bool aPuntos)
        {
            var img = PiezasDeUI.Imagen(aPuntos ? "Raya a puntos" : "Raya", padre, aPuntos ? P.Puntitos : null,
                aPuntos ? new Color(P.SobreNoche.r, P.SobreNoche.g, P.SobreNoche.b, 0.55f) : new Color(P.Oro.r, P.Oro.g, P.Oro.b, 0.95f));
            img.raycastTarget = false;
            if (aPuntos) img.type = Image.Type.Tiled;
            var d = b - a;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(d.magnitude, aPuntos ? 4f : 6f);
            rt.anchoredPosition = new Vector2((a.x + b.x) * 0.5f, -(a.y + b.y) * 0.5f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private static (Image, Image) Estrella(Transform padre, Vector2 c, float lado, float halo, bool encendida)
        {
            var h = PiezasDeUI.Imagen("Halo", padre, PiezasDeUI.Resplandor, new Color(P.Oro.r, P.Oro.g, P.Oro.b, 0.5f));
            h.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(h.rectTransform, c.x - halo * 0.5f, c.y - halo * 0.5f, halo, halo);
            var e = PiezasDeUI.EstrellaEn("Estrella", padre, c.x, c.y, lado);
            h.enabled = encendida;
            return (e, h);
        }

        private static TextMeshProUGUI Ficha(Transform padre, float x, Sprite icono, float giro)
        {
            var c = PiezasDeUI.NuevaCarta("Cuenta", padre, PaletaUI.PapelClaro, 20f);
            PiezasDeUI.ColocarArribaIzquierda(c.raiz, x, 740f, 360f, 80f, giro);
            var img = PiezasDeUI.Imagen("Icono", c.contenido, icono, Color.white);
            img.preserveAspect = true; img.raycastTarget = false;
            img.enabled = icono != null;
            PiezasDeUI.ColocarArribaIzquierda(img.rectTransform, 16f, 16f, 48f, 48f);
            var t = P.Titulo("Texto", c.contenido, "", 26f, PaletaUI.TintaDeTexto);
            PiezasDeUI.ColocarArribaIzquierda(t.rectTransform, 76f, 16f, 274f, 44f);
            return t;
        }

        private static Barra CrearBarra(Transform padre, float x, string etiqueta)
        {
            var b = new Barra();
            var e = P.Txt("Etiqueta " + etiqueta, padre, etiqueta, 22f, P.TextoMaqueta, false);
            PiezasDeUI.ColocarArribaIzquierda(e.rectTransform, x, 712f, 300f, 30f);
            var pista = PiezasDeUI.Redondeado("Barra " + etiqueta, padre, PiezasDeUI.TintaOscura, 17f);
            PiezasDeUI.ColocarArribaIzquierda(pista.rectTransform, x, 744f, 300f, 34f);
            b.relleno = PiezasDeUI.Redondeado("Relleno", pista.transform, Color.white, 14f);
            b.despues = PiezasDeUI.Redondeado("Después", pista.transform, Color.white, 5f);
            foreach (var img in new[] { b.relleno, b.despues })
            {
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(3f, -3f);
            }
            var caja = PiezasDeUI.Redondeado("Capas " + etiqueta, padre, PiezasDeUI.TintaOscura, 14f);
            PiezasDeUI.ColocarArribaIzquierda(caja.rectTransform, x + 230f, 736f, 78f, 46f);
            var dentro = PiezasDeUI.Redondeado("Dentro", caja.transform, PaletaUI.PapelClaro, 11f);
            PiezasDeUI.Margen(dentro.rectTransform, 3f);
            b.capas = P.Titulo("Número", caja.transform, "", 26f, PiezasDeUI.TintaOscura, TextAlignmentOptions.Center);
            P.Rellenar(b.capas.rectTransform);
            return b;
        }

        private P.Casilla Boton(Transform padre, float x, string texto, int indice)
        {
            var c = P.Casilla.Crear(texto, padre, 22f, 36f, () => AlClic(indice), () => AlClic(indice, true));
            PiezasDeUI.ColocarArribaIzquierda(c.raiz, x, 820f, 330f, 82f);
            c.carta.Caida(6f);
            var t = P.Titulo("Texto", c.contenido, texto, 32f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center);
            P.Rellenar(t.rectTransform);
            return c;
        }

        // ─── Mostrar ──────────────────────────────────────────────────────────

        public void Mostrar() => Refrescar();
        public void Ocultar() { }
        public void AlCambiarPersonaje() { }
        public bool Cancelar() => false;

        private void Refrescar()
        {
            var s = SintoniaEstelarService.Instance;
            int pendientes = Pendientes;
            var base_ = EstadisticasDelPersonaje.Base;
            var iniciales = EstadisticasDelPersonaje.Iniciales;
            int vigor = Mathf.Max(0, Mathf.RoundToInt((base_.vida - iniciales.vida) / SintoniaEstelarService.VidaPorVigor));
            int flujo = Mathf.Max(0, Mathf.RoundToInt((base_.magia - iniciales.magia) / SintoniaEstelarService.ManaPorFlujo));
            Encender(_vida, vigor);
            Encender(_mana, flujo);
            _cuentaVida.text = "Vida · " + vigor + (vigor == 1 ? " estrella" : " estrellas");
            _cuentaMana.text = "Maná · " + flujo + (flujo == 1 ? " estrella" : " estrellas");

            if (pendientes > 0)
                _pregunta.text = pendientes == 1
                    ? "Una estrella nueva brilla. ¿Qué quieres que crezca?"
                    : pendientes + " estrellas nuevas brillan. ¿Qué quieres que crezca?";
            else
            {
                int puntos = s != null ? s.PuntosActuales : 0, umbral = s != null ? s.PuntosParaSiguienteHito : 100;
                _pregunta.text = "Siguiente estrella: " + puntos + " / " + umbral + "  <size=70%><color=#6B5844>(combate y purifica)</color></size>";
            }

            float vida = 0f, mana = 0f;
            if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var salud, includeInactive: true, allowSceneLookup: true)) vida = salud.MaxHealth;
            if (PlayerService.TryGetComponent<ManaPool>(out var pool, includeInactive: true, allowSceneLookup: true)) mana = pool.Max;
            var e = EstiloDeBarrasApiladas.Actual;
            PonerBarra(_barraVida, vida, vida + SintoniaEstelarService.VidaPorVigor, e, e.coloresDeVida);
            PonerBarra(_barraMana, mana, mana + SintoniaEstelarService.ManaPorFlujo, e, e.coloresDeMana);
            PintarSeleccion();
            _menu.RefrescarCabecera();
            _menu.RefrescarPistas();
        }

        private static void Encender(List<(Image estrella, Image halo)> rama, int cuantas)
        {
            for (int i = 0; i < rama.Count; i++)
            {
                bool si = i < cuantas;
                var (estrella, halo) = rama[i];
                estrella.sprite = si ? PiezasDeUI.Estrella : PiezasDeUI.EstrellaLisa;
                estrella.color = si ? P.Oro : Apagada;
                float lado = si ? 46f : 38f;
                var rt = estrella.rectTransform;
                rt.sizeDelta = new Vector2(lado, lado);
                halo.enabled = si;
            }
        }

        private void PintarSeleccion()
        {
            bool hay = Pendientes > 0;
            _botones.alpha = hay ? 1f : 0.55f;
            _botonVida.Poner(hay && _elegido == 0 ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
            _botonMana.Poner(hay && _elegido == 1 ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
            _botonVida.carta.relleno.color = FondoVida;
            _botonMana.carta.relleno.color = FondoMana;
        }

        private static void PonerBarra(Barra b, float valor, float despues, EstiloDeBarrasApiladas estilo, Color[] colores)
        {
            int capas = estilo.Capas(valor);
            var color = EstiloDeBarrasApiladas.ColorDeCapa(colores, capas - 1);
            b.relleno.color = color;
            b.relleno.rectTransform.sizeDelta = new Vector2(AnchoDeBarra * estilo.ParteDeArriba(valor), 28f);
            int capasDespues = estilo.Capas(despues);
            var claro = Color.Lerp(EstiloDeBarrasApiladas.ColorDeCapa(colores, capasDespues - 1), Color.white, 0.55f);
            b.despues.color = claro;
            b.despues.rectTransform.sizeDelta = new Vector2(AnchoDeBarra * estilo.ParteDeArriba(despues), 10f);
            b.despues.transform.SetAsLastSibling();
            b.capas.text = "×" + capas;
        }

        // ─── Entrada ──────────────────────────────────────────────────────────

        public void Tick()
        {
            if (Pendientes <= 0) return;
            int h = EntradaDelMenu.Horizontal();
            if (h != 0)
            {
                int n = Mathf.Clamp(_elegido + h, 0, 1);
                if (n != _elegido) { _elegido = n; EntradaDelMenu.Sonido("UI_Navigate"); PintarSeleccion(); }
            }
            if (EntradaDelMenu.Aceptar) Elegir();
        }

        /// Clic: elige el don o, si ya estaba elegido, lo confirma. Al pasar el ratón, solo lo elige.
        private void AlClic(int indice, bool soloElegir = false)
        {
            if (Pendientes <= 0) return;
            if (_elegido != indice) { _elegido = indice; EntradaDelMenu.Sonido("UI_Navigate"); PintarSeleccion(); return; }
            if (!soloElegir) Elegir();
        }

        private void Elegir()
        {
            var s = SintoniaEstelarService.Instance;
            if (s == null || s.HitosPendientes <= 0) return;
            s.AplicarEleccion(_elegido == 0 ? Tipo.Vigor : Tipo.Flujo);
            EntradaDelMenu.Sonido("UI_Submit");
            _menu.AlCambiarEstadisticas?.Invoke();
            Refrescar();
        }
    }
}
