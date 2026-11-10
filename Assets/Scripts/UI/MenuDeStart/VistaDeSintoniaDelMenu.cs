using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = Sendero.UI.PiezasDelMenuDeStart;
using Tipo = SintoniaEstelarService.TipoEleccionSintonia;

namespace Sendero.UI
{
    /// <summary>
    /// Pestaña Sintonía del menú de Start: el medidor de Sintonía Estelar, los dones por elegir
    /// (estrellas) y la elección entre Vigor (+vida máxima) y Flujo (+maná máximo) con la barra
    /// apilada de ahora y de después (<see cref="EstiloDeBarrasApiladas"/>). El don es para todo el
    /// grupo (<see cref="SintoniaEstelarService.AplicarEleccion"/>). La cruceta elige, A confirma;
    /// con ratón, un clic elige y otro confirma.
    /// </summary>
    public sealed class VistaDeSintoniaDelMenu : IVistaDelMenuDeStart
    {
        private static readonly Color ColorVigor = new Color32(0x4C, 0xD0, 0x7D, 0xFF);
        private static readonly Color ColorFlujo = new Color32(0x78, 0xA6, 0xFF, 0xFF);
        private const float AnchoDeBarra = 520f;

        private MenuDeStart _menu;
        private TextMeshProUGUI _estrellas, _progresoTexto, _hitosTexto, _nota;
        private RectTransform _progreso;
        private Opcion _vigor, _flujo;
        private int _elegido;

        private sealed class Opcion
        {
            public Color color;
            public Image borde, fondo;
            public CanvasGroup grupo;
            public Fila ahora, despues;
        }

        private sealed class Fila
        {
            public Image debajo, arriba;
            public TextMeshProUGUI capas, maximo;
        }

        public string Titulo => "Sintonía";
        public bool UsaPersonaje => false;
        public bool UsaX => false;
        private static int Pendientes => SintoniaEstelarService.Instance != null ? SintoniaEstelarService.Instance.HitosPendientes : 0;

        public string Pistas => Pendientes > 0
            ? P.Boton(InputGlyphNames.South) + " Elegir este don     " + P.Boton(InputGlyphNames.Dpad) + " Cambiar"
            : "";

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            var cab = P.CajaConBorde("Medidor", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)cab.transform.parent, 0f, 0f, 1812f, 180f);
            var c = cab.transform;
            var t = P.Txt("Título", c, "Sintonía Estelar", 42f, P.Texto, true);
            P.Colocar(t.rectTransform, 36f, 34f, 380f, 56f);
            var sub = P.Txt("Subtítulo", c, "Se llena combatiendo y purificando", 23f, P.TextoApagado);
            P.Colocar(sub.rectTransform, 36f, 98f, 380f, 34f);
            var fondoBarra = P.Caja("Barra", c, P.FichaOscura, 16f);
            P.Colocar(fondoBarra.rectTransform, 440f, 52f, 1020f, 32f);
            var relleno = P.Caja("Relleno", fondoBarra.transform, P.Oro, 13f);
            _progreso = relleno.rectTransform;
            _progreso.anchorMin = Vector2.zero; _progreso.anchorMax = new Vector2(0f, 1f); _progreso.pivot = new Vector2(0f, 0.5f);
            _progreso.offsetMin = new Vector2(3f, 3f); _progreso.offsetMax = new Vector2(3f, -3f);
            _progresoTexto = P.Txt("Progreso", c, "", 23f, P.TextoSuave, true);
            P.Colocar(_progresoTexto.rectTransform, 440f, 100f, 600f, 34f);
            _hitosTexto = P.Txt("Hitos", c, "", 23f, P.TextoSuave, true, TextAlignmentOptions.TopRight);
            P.Colocar(_hitosTexto.rectTransform, 860f, 100f, 600f, 34f);
            _estrellas = P.Txt("Estrellas", c, "", 26f, P.Oro, true, TextAlignmentOptions.Center, envolver: true);
            P.ColocarDerecha(_estrellas.rectTransform, 30f, 28f, 260f, 120f);

            _vigor = Tarjeta(cuerpo, 0f, ColorVigor, "Vigor",
                "+" + SintoniaEstelarService.VidaPorVigor + " de vida máxima",
                "Aguantas más golpes y más errores. Para quien prefiere ir al cuerpo a cuerpo con margen.", "VIDA", 0);
            _flujo = Tarjeta(cuerpo, 924f, ColorFlujo, "Flujo",
                "+" + SintoniaEstelarService.ManaPorFlujo + " de magia máxima",
                "Más reserva para sostener el escudo y encadenar combos y hechizos potentes.", "MAGIA", 1);

            _nota = P.Txt("Nota", cuerpo, "", 23f, P.TextoApagado, false, TextAlignmentOptions.Center);
            P.Colocar(_nota.rectTransform, 0f, 840f, 1812f, 40f);
        }

        private Opcion Tarjeta(RectTransform padre, float x, Color color, string nombre, string ganancia, string descripcion, string queSube, int indice)
        {
            var op = new Opcion { color = color };
            op.fondo = P.CajaConBorde(nombre, padre, P.BordeApagado, P.Panel, 4f, 26f);
            op.borde = op.fondo.transform.parent.GetComponent<Image>();
            P.Colocar(op.borde.rectTransform, x, 214f, 888f, 610f);
            op.grupo = op.borde.gameObject.AddComponent<CanvasGroup>();
            P.Clicable(op.borde, () => AlClic(indice));
            var c = op.fondo.transform;
            var circulo = P.Caja("Círculo", c, color, 45f);
            P.Colocar(circulo.rectTransform, 40f, 40f, 90f, 90f);
            var signo = P.Txt("Signo", circulo.transform, "+", 56f, new Color32(0x14, 0x12, 0x2F, 0xFF), true, TextAlignmentOptions.Center);
            P.Rellenar(signo.rectTransform);
            var n = P.Txt("Nombre", c, nombre, 48f, P.Texto, true);
            P.Colocar(n.rectTransform, 152f, 36f, 680f, 60f);
            var g = P.Txt("Ganancia", c, ganancia, 30f, color, true);
            P.Colocar(g.rectTransform, 152f, 96f, 680f, 40f);
            var d = P.Txt("Descripción", c, descripcion, 26f, new Color32(0xDF, 0xE2, 0xFF, 0xFF), false, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(d.rectTransform, 40f, 170f, 800f, 110f);
            var cq = P.Txt("Cómo queda", c, "CÓMO QUEDA LA BARRA DE " + queSube, 20f, P.TextoApagado, true);
            P.Colocar(cq.rectTransform, 40f, 320f, 800f, 30f);
            op.ahora = CrearFila(c, "Ahora", 372f);
            op.despues = CrearFila(c, "Después", 440f);
            return op;
        }

        private static Fila CrearFila(Transform padre, string etiqueta, float y)
        {
            var f = new Fila();
            var e = P.Txt("Etiqueta", padre, etiqueta, 24f, P.TextoApagado, true, TextAlignmentOptions.MidlineLeft);
            P.Colocar(e.rectTransform, 40f, y, 140f, 40f);
            var fondo = P.Caja("Barra " + etiqueta, padre, new Color32(0x0D, 0x0F, 0x22, 0xFF), 8f);
            P.Colocar(fondo.rectTransform, 190f, y + 6f, AnchoDeBarra, 28f);
            f.debajo = P.Caja("Debajo", fondo.transform, Color.white, 6f);
            P.Rellenar(f.debajo.rectTransform, 2f);
            f.arriba = P.Caja("Arriba", fondo.transform, Color.white, 6f);
            var rt = f.arriba.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(2f, 2f); rt.offsetMax = new Vector2(2f, -2f);
            f.capas = P.Txt("Capas", padre, "", 24f, P.Texto, true, TextAlignmentOptions.MidlineLeft);
            P.Colocar(f.capas.rectTransform, 190f + AnchoDeBarra + 14f, y, 60f, 40f);
            f.maximo = P.Txt("Máximo", padre, "", 24f, P.TextoSuave, true, TextAlignmentOptions.MidlineRight);
            P.Colocar(f.maximo.rectTransform, 190f + AnchoDeBarra + 70f, y, 80f, 40f);
            return f;
        }

        public void Mostrar() => Refrescar();
        public void Ocultar() { }
        public void AlCambiarPersonaje() { }
        public bool Cancelar() => false;

        private void Refrescar()
        {
            var s = SintoniaEstelarService.Instance;
            int pendientes = Pendientes;
            int puntos = s != null ? s.PuntosActuales : 0;
            int umbral = s != null ? s.PuntosParaSiguienteHito : 100;
            _estrellas.text = pendientes > 0
                ? "<size=150%>" + new string('★', Mathf.Min(pendientes, 5)) + "</size>\n" + pendientes + (pendientes == 1 ? " don por elegir" : " dones por elegir")
                : "<color=#A9AED6>Nada por elegir</color>";
            float fraccion = umbral > 0 ? Mathf.Clamp01((float)puntos / umbral) : 0f;
            _progreso.sizeDelta = new Vector2(Mathf.Max(0f, 1014f * fraccion), _progreso.sizeDelta.y);
            P.Activar(_progreso.gameObject, fraccion > 0.001f);
            _progresoTexto.text = puntos + " / " + umbral + " para el siguiente hito";
            _hitosTexto.text = "Hitos alcanzados: " + (s != null ? s.HitosAlcanzados : 0);

            float vida = 0f, mana = 0f;
            if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var salud, includeInactive: true, allowSceneLookup: true)) vida = salud.MaxHealth;
            if (PlayerService.TryGetComponent<ManaPool>(out var pool, includeInactive: true, allowSceneLookup: true)) mana = pool.Max;
            var e = EstiloDeBarrasApiladas.Actual;
            PonerFila(_vigor.ahora, vida, e, e.coloresDeVida);
            PonerFila(_vigor.despues, vida + SintoniaEstelarService.VidaPorVigor, e, e.coloresDeVida);
            PonerFila(_flujo.ahora, mana, e, e.coloresDeMana);
            PonerFila(_flujo.despues, mana + SintoniaEstelarService.ManaPorFlujo, e, e.coloresDeMana);
            PintarSeleccion();
            _nota.text = pendientes > 0 ? "El don es para todo el grupo." : "Gana combates y purifica amenazas para llenar la Sintonía.";
            _menu.RefrescarCabecera();
            _menu.RefrescarPistas();
        }

        private void PintarSeleccion()
        {
            bool hay = Pendientes > 0;
            Pintar(_vigor, hay, _elegido == 0);
            Pintar(_flujo, hay, _elegido == 1);
        }

        private static void Pintar(Opcion op, bool hay, bool elegido)
        {
            op.grupo.alpha = hay ? 1f : 0.55f;
            op.borde.color = hay && elegido ? op.color : P.BordeApagado;
            op.fondo.color = hay && elegido ? new Color32(0x28, 0x22, 0x50, 0xEB) : P.Panel;
        }

        private static void PonerFila(Fila f, float valor, EstiloDeBarrasApiladas estilo, Color[] colores)
        {
            int capas = estilo.Capas(valor);
            f.arriba.color = EstiloDeBarrasApiladas.ColorDeCapa(colores, capas - 1);
            f.debajo.color = capas > 1 ? EstiloDeBarrasApiladas.ColorDeCapa(colores, capas - 2) : new Color32(0x0D, 0x0F, 0x22, 0xFF);
            var rt = f.arriba.rectTransform;
            rt.sizeDelta = new Vector2((AnchoDeBarra - 4f) * estilo.ParteDeArriba(valor), rt.sizeDelta.y);
            f.capas.text = capas > 1 ? "×" + capas : "";
            f.maximo.text = Mathf.RoundToInt(valor).ToString();
        }

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

        private void AlClic(int indice)
        {
            if (Pendientes <= 0) return;
            if (_elegido != indice) { _elegido = indice; EntradaDelMenu.Sonido("UI_Navigate"); PintarSeleccion(); return; }
            Elegir();
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
