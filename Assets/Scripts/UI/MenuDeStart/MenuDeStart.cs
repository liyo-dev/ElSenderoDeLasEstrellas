using System;
using System.Collections.Generic;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = Sendero.UI.PiezasDelMenuDeStart;
using Slot = PartyControlManager.CharacterSlot;

namespace Sendero.UI
{
    /// <summary>
    /// Una pestaña del menú de Start. Se monta una vez dentro del cuerpo del menú y el menú le
    /// pasa la entrada mientras está a la vista.
    /// </summary>
    public interface IVistaDelMenuDeStart
    {
        string Titulo { get; }
        /// Usa el selector de personaje de la cabecera (LT/RT).
        bool UsaPersonaje { get; }
        /// Usa el botón X (si no, X abre los controles).
        bool UsaX { get; }
        /// Usa LT/RT (Q/E en teclado) para cambiar de personaje o de categoría: en teclado, Q no sale al menú.
        bool UsaGatillos { get; }
        /// Texto de los botones de abajo (con iconos de <see cref="PiezasDelMenuDeStart.Boton"/>).
        string Pistas { get; }
        void Montar(RectTransform cuerpo, MenuDeStart menu);
        void Mostrar();
        void Ocultar();
        void Tick();
        /// B dentro de la pestaña (salir de un modo). False si no había nada que cancelar.
        bool Cancelar();
        void AlCambiarPersonaje();
    }

    /// <summary>
    /// El menú de Start a pantalla completa con la disposición de la maqueta «Menú de Start del
    /// Sendero: propuesta nueva» (lienzo de 1920×1080): fondo de noche con estrellas, pestañas de
    /// papel con icono Inventario · Magia · Equipo · Sintonía (LB/RB), selector de personaje
    /// (LT/RT) en las pestañas que lo usan o las monedas en las demás, y abajo a la derecha los
    /// botones de la pestaña con los iconos del mando activo. Las vistas se colocan con las medidas
    /// de la maqueta dentro de <see cref="Lienzo"/>. Se monta por código dentro del lienzo del menú;
    /// <see cref="PlayerEquipmentMenuController"/> lo abre, lo cierra y le pasa la entrada con <see cref="Tick"/>.
    /// </summary>
    public sealed class MenuDeStart : MonoBehaviour
    {
        // Pestañas: x de cada una, giro cuando no está abierta (la abierta baja 14 y gira −2).
        private static readonly float[] XPestana = { 124f, 370f, 616f, 862f };
        private static readonly float[] GiroPestana = { -1.2f, 1f, -0.8f, 1.2f };

        private readonly List<IVistaDelMenuDeStart> _vistas = new();
        private readonly List<RectTransform> _huecos = new();
        private readonly List<PiezasDeUI.Carta> _pestanas = new();
        private readonly List<TextMeshProUGUI> _textosDePestana = new();
        private readonly List<PiezasDeUI.Carta> _fichasDePersonaje = new();
        private readonly List<TextMeshProUGUI> _nombresDePersonaje = new();
        private readonly Slot[] _personajes = { Slot.Will, Slot.Estela, Slot.Liam };
        private int _actual;
        private RectTransform _lienzo, _selector, _monedas, _estrellaDePestana, _avisoSintonia;
        private TextMeshProUGUI _pistas, _textoMonedas, _textoAvisoSintonia;
        private Image _iconoMonedas;
        private string _pistasPuestas;

        /// El personaje que se está mirando en Magia y Equipo.
        public Slot Personaje { get; private set; } = Slot.Will;
        public bool Abierto => gameObject.activeSelf;
        public IVistaDelMenuDeStart Vista => _vistas.Count > 0 ? _vistas[_actual] : null;
        public bool UsaX => Vista != null && Vista.UsaX;
        public bool UsaGatillos => Vista != null && Vista.UsaGatillos;
        private static bool ConTeclado => InputGlyphService.CurrentFamily == InputGlyphDeviceFamily.KeyboardMouse;
        public bool EnMagia => Vista is VistaDeMagiaDelMenu;
        public SpellId HechizoResaltado => Vista is VistaDeMagiaDelMenu m ? m.Resaltado : SpellId.None;
        /// Dibujos y letras del menú.
        public ArteDelMenuDeStart Arte { get; private set; }

        /// Para usar un objeto con el mismo camino que el resto del juego (lo da el controlador).
        public Func<ItemData, string> UsarObjeto;
        /// Tras cambiar algo que mueve la vida o el maná (pociones, Sintonía).
        public Action AlCambiarEstadisticas;

        public static MenuDeStart Crear(RectTransform padre, int indice, TMP_FontAsset fuente)
        {
            var arte = ArteDelMenuDeStart.Cargado;
            var estilo = arte != null ? arte.estilo : null;
            P.Fuente = estilo != null && estilo.fuenteTexto != null ? estilo.fuenteTexto : fuente != null ? fuente : TMP_Settings.defaultFontAsset;
            P.FuenteTitulos = estilo != null && estilo.fuenteTitulo != null ? estilo.fuenteTitulo : P.Fuente;
            var go = new GameObject("Menú de Start (nuevo)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.SetSiblingIndex(Mathf.Clamp(indice, 0, padre.childCount - 1));
            P.Rellenar(rt);
            var menu = go.AddComponent<MenuDeStart>();
            menu.Arte = arte;
            menu.Montar(rt);
            go.SetActive(false);
            return menu;
        }

        // ─── Montaje ───────────────────────────────────────────────────────────

        private void Montar(RectTransform raiz)
        {
            _lienzo = PiezasDeUI.LienzoDeMaqueta(raiz, "Maqueta");
            PiezasDeUI.FondoDeNoche(_lienzo); // tapa el mundo y lo que haya detrás

            _vistas.Add(new VistaDeInventarioDelMenu());
            _vistas.Add(new VistaDeMagiaDelMenu());
            _vistas.Add(new VistaDeEquipoDelMenu());
            _vistas.Add(new VistaDeSintoniaDelMenu());

            foreach (var v in _vistas)
            {
                var hueco = P.Vacio(v.Titulo, _lienzo);
                P.Rellenar(hueco);
                v.Montar(hueco, this);
                hueco.gameObject.SetActive(false);
                _huecos.Add(hueco);
            }
            MontarCabecera();

            _pistas = P.TxtConIconos("Pistas", _lienzo, "", 25f, P.SobreNoche, TextAlignmentOptions.MidlineRight);
            _pistas.font = P.FuenteTitulos;
            PiezasDeUI.ColocarArribaIzquierda(_pistas.rectTransform, 54f, 1006f, 1812f, 48f);
        }

        private void MontarCabecera()
        {
            P.Glifo("LB", _lienzo, InputGlyphNames.ShoulderLeft, 56f, 40f, 50f);
            for (int i = 0; i < _vistas.Count; i++)
            {
                int indice = i;
                var carta = PiezasDeUI.NuevaCarta("Pestaña " + _vistas[i].Titulo, _lienzo, PaletaUI.PapelApagado, 18f, 5f);
                var tapa = carta.raiz.gameObject.AddComponent<Image>();
                tapa.color = new Color(1f, 1f, 1f, 0f);
                P.Clicable(tapa, () => { if (indice != _actual) { EntradaDelMenu.Sonido("UI_Navigate"); MostrarPestana(indice); } });
                var icono = PiezasDeUI.Imagen("Icono", carta.contenido, Arte != null && i < Arte.pestanas.Length ? Arte.pestanas[i] : null, Color.white);
                icono.raycastTarget = false;
                icono.enabled = icono.sprite != null;
                PiezasDeUI.ColocarArribaIzquierda(icono.rectTransform, 13f, 12f, 50f, 50f);
                var t = P.Titulo("Texto", carta.contenido, _vistas[i].Titulo, 30f, P.TextoMaqueta);
                PiezasDeUI.ColocarArribaIzquierda(t.rectTransform, 70f, 0f, 150f, 72f);
                _pestanas.Add(carta);
                _textosDePestana.Add(t);
            }
            _estrellaDePestana = PiezasDeUI.EstrellaEn("Estrella", _lienzo, 0f, 0f, 30f, 12f).rectTransform;
            // Dones de Sintonía por elegir: insignia dorada con el número en la esquina de su pestaña.
            var aviso = PiezasDeUI.Imagen("Aviso de Sintonía", _lienzo, PiezasDeUI.Circulo, PiezasDeUI.TintaOscura);
            aviso.raycastTarget = false;
            var dentro = PiezasDeUI.Imagen("Dentro", aviso.transform, PiezasDeUI.Circulo, PaletaUI.Oro);
            dentro.raycastTarget = false;
            PiezasDeUI.Margen(dentro.rectTransform, 3f);
            _textoAvisoSintonia = P.Titulo("Número", aviso.transform, "", 22f, PiezasDeUI.TintaOscura, TextAlignmentOptions.Center);
            P.Rellenar(_textoAvisoSintonia.rectTransform);
            _avisoSintonia = aviso.rectTransform;
            PiezasDeUI.ColocarArribaIzquierda(_avisoSintonia, XPestana[3] + 206f, 30f, 36f, 36f);
            P.Glifo("RB", _lienzo, InputGlyphNames.ShoulderRight, 1112f, 40f, 50f);

            // Selector de personaje (Magia y Equipo).
            _selector = P.Vacio("Personajes", _lienzo);
            P.Rellenar(_selector);
            P.Glifo("LT", _selector, InputGlyphNames.MenuLT, 1222f, 40f, 50f);
            for (int i = 0; i < _personajes.Length; i++)
            {
                var slot = _personajes[i];
                var ficha = PiezasDeUI.NuevaCarta("Personaje " + slot, _selector, PaletaUI.PapelApagado, 36f);
                PiezasDeUI.ColocarArribaIzquierda(ficha.raiz, 1286f + i * 156f, 44f, 146f, 72f);
                var tapa = ficha.raiz.gameObject.AddComponent<Image>();
                tapa.color = new Color(1f, 1f, 1f, 0f);
                P.Clicable(tapa, () => ElegirPersonaje(slot));
                Avatar(ficha.contenido, slot, 8f, 7f, 52f);
                _avatares.Add((ficha.contenido, slot, 8f, 7f, 52f));
                var n = P.Titulo("Nombre", ficha.contenido, ControlDelJugador.Nombre(slot), 24f, P.TextoMaqueta);
                PiezasDeUI.ColocarArribaIzquierda(n.rectTransform, 66f, 14f, 76f, 36f);
                n.overflowMode = TextOverflowModes.Overflow;
                _fichasDePersonaje.Add(ficha);
                _nombresDePersonaje.Add(n);
            }
            P.Glifo("RT", _selector, InputGlyphNames.MenuRT, 1756f, 40f, 50f);

            // Monedas (las demás pestañas).
            _monedas = P.Vacio("Monedas", _lienzo);
            P.Rellenar(_monedas);
            var pastilla = PiezasDeUI.NuevaCarta("Pastilla", _monedas, PaletaUI.PapelClaro, 30f);
            PiezasDeUI.ColocarArribaIzquierda(pastilla.raiz, 1650f, 50f, 216f, 60f);
            _iconoMonedas = PiezasDeUI.Imagen("Icono", pastilla.contenido, Arte != null ? Arte.moneda : null, Color.white);
            _iconoMonedas.preserveAspect = true;
            _iconoMonedas.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_iconoMonedas.rectTransform, 8f, 6f, 48f, 48f);
            _textoMonedas = P.Titulo("Texto", pastilla.contenido, "", 30f, PaletaUI.TintaDeTexto);
            PiezasDeUI.ColocarArribaIzquierda(_textoMonedas.rectTransform, 66f, 8f, 140f, 44f);
        }

        /// Medallón redondo con el retrato del personaje (o su color si no tiene).
        // Las caras de las pestañas: al montar el menú las fichas pueden no estar cargadas todavía (salía
        // el mismo círculo morado en las tres); se rehacen al abrirlo.
        private readonly List<(Transform padre, Slot personaje, float x, float y, float lado)> _avatares = new();

        private void RehacerAvatares()
        {
            foreach (var a in _avatares)
            {
                if (a.padre == null) continue;
                var viejo = a.padre.Find("Avatar");
                if (viejo != null && viejo.Find("Fondo/Cara") != null) continue;
                var ficha = GrimorioDelPersonaje.FichaDelSet(a.personaje) ?? FichaDePersonaje.Buscar(a.personaje);
                if (ficha == null) continue;
                if (viejo != null) Destroy(viejo.gameObject);
                Avatar(a.padre, a.personaje, a.x, a.y, a.lado, ficha);
            }
        }

        public static void Avatar(Transform padre, Slot personaje, float x, float y, float lado) => Avatar(padre, personaje, x, y, lado, null);

        public static void Avatar(Transform padre, Slot personaje, float x, float y, float lado, FichaDePersonaje fichaDada)
        {
            var ficha = fichaDada != null ? fichaDada : FichaDePersonaje.Buscar(personaje);
            var borde = PiezasDeUI.Imagen("Avatar", padre, PiezasDeUI.Circulo, PiezasDeUI.TintaOscura);
            borde.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(borde.rectTransform, x, y, lado, lado);
            var fondo = PiezasDeUI.Imagen("Fondo", borde.transform, PiezasDeUI.Circulo,
                ficha != null && ficha.ColorDelPersonaje.a > 0f ? ficha.ColorDelPersonaje : PaletaUI.Lavanda);
            fondo.raycastTarget = false;
            PiezasDeUI.Margen(fondo.rectTransform, 3f);
            if (ficha == null || ficha.Retrato == null) return;
            fondo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var cara = PiezasDeUI.Imagen("Cara", fondo.transform, ficha.Retrato, Color.white);
            cara.preserveAspect = true;
            cara.raycastTarget = false;
            PiezasDeUI.Estirar(cara.rectTransform);
        }

        // ─── Abrir, cerrar, pestañas ──────────────────────────────────────────

        public void Abrir()
        {
            Personaje = ControlDelJugador.Activo;
            if (!ControlDelJugador.EnElGrupo(Personaje)) Personaje = Slot.Will;
            RehacerAvatares();
            gameObject.SetActive(true);
            MostrarPestana(_actual);
        }

        public void Cerrar()
        {
            Vista?.Ocultar();
            gameObject.SetActive(false);
        }

        /// Abre el menú (si hace falta) por una pestaña concreta.
        public void IrA<T>() where T : IVistaDelMenuDeStart
        {
            int i = _vistas.FindIndex(v => v is T);
            if (i >= 0) MostrarPestana(i);
        }

        public void CambiarPestana(int delta)
        {
            if (_vistas.Count == 0) return;
            MostrarPestana((_actual + delta + _vistas.Count) % _vistas.Count);
        }

        private void MostrarPestana(int indice)
        {
            if (indice != _actual) Vista?.Ocultar();
            for (int i = 0; i < _huecos.Count; i++) P.Activar(_huecos[i].gameObject, i == indice);
            _actual = indice;
            PintarPestanas();
            Vista.Mostrar();
            RefrescarCabecera();
            RefrescarPistas();
        }

        private void PintarPestanas()
        {
            int pendientes = SintoniaEstelarService.Instance != null ? SintoniaEstelarService.Instance.HitosPendientes : 0;
            for (int i = 0; i < _vistas.Count; i++)
            {
                bool activa = i == _actual;
                var c = _pestanas[i];
                PiezasDeUI.ColocarArribaIzquierda(c.raiz, XPestana[i], activa ? 52f : 38f, 232f, 78f, activa ? -2f : GiroPestana[i]);
                c.Pintar(activa ? PaletaUI.Oro : PaletaUI.PapelApagado);
                c.Caida(activa ? 7f : 5f);
                _textosDePestana[i].color = activa ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
                if (activa) c.raiz.SetAsLastSibling();
            }
            PiezasDeUI.ColocarArribaIzquierda(_estrellaDePestana, XPestana[_actual] + 198f, 36f, 30f, 30f, 12f);
            _estrellaDePestana.SetAsLastSibling();
            P.Activar(_avisoSintonia.gameObject, pendientes > 0);
            _textoAvisoSintonia.text = pendientes.ToString();
            _avisoSintonia.SetAsLastSibling();
        }

        public void RefrescarCabecera()
        {
            bool usa = Vista != null && Vista.UsaPersonaje;
            P.Activar(_selector.gameObject, usa);
            P.Activar(_monedas.gameObject, !usa);
            PintarPestanas();
            if (usa)
            {
                for (int i = 0; i < _personajes.Length; i++)
                {
                    bool esta = ControlDelJugador.EnElGrupo(_personajes[i]);
                    P.Activar(_fichasDePersonaje[i].raiz.gameObject, esta);
                    bool elegido = _personajes[i] == Personaje;
                    _fichasDePersonaje[i].Pintar(elegido ? PaletaUI.Oro : PaletaUI.PapelApagado);
                    _nombresDePersonaje[i].color = elegido ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
                }
            }
            else PonerMonedas();
        }

        private static readonly System.Text.RegularExpressions.Regex s_iconos =
            new System.Text.RegularExpressions.Regex("(<sprite[^>]*>)", System.Text.RegularExpressions.RegexOptions.Compiled);

        public void RefrescarPistas()
        {
            string p = Vista != null ? Vista.Pistas : "";
            // Con teclado, la Q de «Salir al menú» es la de LT en las pestañas que usan Q/E.
            bool salir = !(ConTeclado && UsaGatillos);
            string comunes = (p.Length > 0 ? "     " : "")
                             + (salir ? P.Boton(InputGlyphNames.North) + " Salir al menú     " : "")
                             + P.Boton(InputGlyphNames.Cancel) + " Cerrar"
                             + (UsaX ? "" : "     " + P.Boton(InputGlyphNames.MenuX) + " Controles");
            string todo = p + comunes;
            // Con el grimorio abierto, sus pistas son las que valen: las del menú no se montan encima.
            bool tapado = GrimorioLibroUI.AlgunoAbierto;
            if (_pistas.gameObject.activeSelf == tapado) _pistas.gameObject.SetActive(!tapado);
            if (todo == _pistasPuestas) return;
            _pistasPuestas = todo;
            // Los iconos de los botones, del tamaño de las insignias de la maqueta (40 sobre letra de 25).
            _pistas.text = s_iconos.Replace(todo, "<size=160%><voffset=-0.12em>$1</voffset></size>");
        }

        private void PonerMonedas()
        {
            _textoMonedas.text = "0";
            if (!PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true) || inv == null) return;
            // La moneda principal (la primera que no sea la Esencia de los enemigos).
            foreach (var e in inv.GetAllItems())
            {
                if (e.item == null || e.item.usageKind != ItemData.ItemUsageKind.Currency) continue;
                if (e.item.name.IndexOf("Esencia", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                _textoMonedas.text = ConEspacios(e.count);
                return;
            }
        }

        /// «1240» → «1 240», como en la maqueta.
        public static string ConEspacios(int n)
        {
            var formato = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
            formato.NumberGroupSeparator = " ";
            return n.ToString("#,0", formato);
        }

        // ─── Personaje ────────────────────────────────────────────────────────

        private void ElegirPersonaje(Slot personaje)
        {
            if (personaje == Personaje || !ControlDelJugador.EnElGrupo(personaje)) return;
            Personaje = personaje;
            EntradaDelMenu.Sonido("UI_Navigate");
            RefrescarCabecera();
            Vista?.AlCambiarPersonaje();
            RefrescarPistas();
        }

        private void PasarPersonaje(int delta)
        {
            int i = Array.IndexOf(_personajes, Personaje);
            for (int paso = 1; paso <= _personajes.Length; paso++)
            {
                var s = _personajes[(i + delta * paso + _personajes.Length * 3) % _personajes.Length];
                if (ControlDelJugador.EnElGrupo(s)) { ElegirPersonaje(s); return; }
            }
        }

        // ─── Entrada ──────────────────────────────────────────────────────────

        public void Tick()
        {
            if (!Abierto || Vista == null) return;
            if (Vista.UsaPersonaje)
            {
                int g = EntradaDelMenu.Gatillos();
                if (g != 0) PasarPersonaje(g);
            }
            Vista.Tick();
            RefrescarPistas();
        }

        public bool Cancelar() => Vista != null && Vista.Cancelar();
    }
}
