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
    /// El menú de Start a pantalla completa (maqueta «Menús del Sendero», INC-964/INC-1000): fondo
    /// oscuro fijo, pestañas Inventario · Magia · Equipo · Sintonía (LB/RB), selector de personaje
    /// (LT/RT) en las pestañas que lo usan, monedas, y abajo los botones de la pestaña. Se monta
    /// por código dentro del lienzo del menú; <see cref="PlayerEquipmentMenuController"/> lo abre,
    /// lo cierra y le pasa la entrada con <see cref="Tick"/>.
    /// </summary>
    public sealed class MenuDeStart : MonoBehaviour
    {
        public const float Ancho = 1920f, Alto = 1080f, Margen = 54f;
        public const float AltoCabecera = 120f, AltoPie = 74f;

        private readonly List<IVistaDelMenuDeStart> _vistas = new();
        private readonly List<RectTransform> _huecos = new();
        private readonly List<Image> _fondosDePestana = new();
        private readonly List<TextMeshProUGUI> _textosDePestana = new();
        private readonly List<Image> _fichasDePersonaje = new();
        private readonly List<TextMeshProUGUI> _nombresDePersonaje = new();
        private readonly Slot[] _personajes = { Slot.Will, Slot.Estela, Slot.Liam };
        private int _actual;
        private RectTransform _selector, _monedas;
        private TextMeshProUGUI _pistas, _avisoSintonia, _textoMonedas;
        private string _pistasPuestas;

        /// El personaje que se está mirando en Magia y Equipo.
        public Slot Personaje { get; private set; } = Slot.Will;
        public bool Abierto => gameObject.activeSelf;
        public IVistaDelMenuDeStart Vista => _vistas.Count > 0 ? _vistas[_actual] : null;
        public bool UsaX => Vista != null && Vista.UsaX;
        public bool EnMagia => Vista is VistaDeMagiaDelMenu;
        public SpellId HechizoResaltado => Vista is VistaDeMagiaDelMenu m ? m.Resaltado : SpellId.None;

        /// Para usar un objeto con el mismo camino que el resto del juego (lo da el controlador).
        public Func<ItemData, string> UsarObjeto;
        /// Tras cambiar algo que mueve la vida o el maná (pociones, Sintonía).
        public Action AlCambiarEstadisticas;

        public static MenuDeStart Crear(RectTransform padre, int indice, TMP_FontAsset fuente)
        {
            P.Fuente = fuente != null ? fuente : TMP_Settings.defaultFontAsset;
            var go = new GameObject("Menú de Start (nuevo)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.SetSiblingIndex(Mathf.Clamp(indice, 0, padre.childCount - 1));
            P.Rellenar(rt);
            var menu = go.AddComponent<MenuDeStart>();
            menu.Montar(rt);
            go.SetActive(false);
            return menu;
        }

        // ─── Montaje ───────────────────────────────────────────────────────────

        private void Montar(RectTransform raiz)
        {
            var fondo = PiezasDeUI.Imagen("Fondo", raiz, FondoRadial(), Color.white);
            P.Rellenar(fondo.rectTransform);
            fondo.raycastTarget = true; // tapa el menú viejo y el mundo

            _vistas.Add(new VistaDeInventarioDelMenu());
            _vistas.Add(new VistaDeMagiaDelMenu());
            _vistas.Add(new VistaDeEquipoDelMenu());
            _vistas.Add(new VistaDeSintoniaDelMenu());

            MontarCabecera(raiz);

            var cuerpo = P.Vacio("Cuerpo", raiz);
            cuerpo.anchorMin = Vector2.zero; cuerpo.anchorMax = Vector2.one;
            cuerpo.offsetMin = new Vector2(Margen, AltoPie);
            cuerpo.offsetMax = new Vector2(-Margen, -AltoCabecera);
            foreach (var v in _vistas)
            {
                var hueco = P.Vacio(v.Titulo, cuerpo);
                P.Rellenar(hueco);
                v.Montar(hueco, this);
                hueco.gameObject.SetActive(false);
                _huecos.Add(hueco);
            }

            _pistas = P.TxtConIconos("Pistas", raiz, "", 24f, P.SobreNoche, TextAlignmentOptions.MidlineRight);
            var rtp = _pistas.rectTransform;
            rtp.anchorMin = new Vector2(0f, 0f); rtp.anchorMax = new Vector2(1f, 0f); rtp.pivot = new Vector2(0.5f, 0f);
            rtp.offsetMin = new Vector2(Margen, 14f); rtp.offsetMax = new Vector2(-Margen, AltoPie - 14f);
        }

        private void MontarCabecera(RectTransform raiz)
        {
            var lb = P.TxtConIconos("LB", raiz, P.Boton(InputGlyphNames.ShoulderLeft), 34f, P.SobreNoche, TextAlignmentOptions.Center);
            P.Colocar(lb.rectTransform, Margen, 40f, 54f, 56f);
            var barra = P.Caja("Pestañas", raiz, new Color(0.05f, 0.05f, 0.13f, 0.75f), 30f);
            float x = 8f;
            var anchos = new[] { 190f, 140f, 150f, 210f };
            for (int i = 0; i < _vistas.Count; i++)
            {
                int indice = i;
                var f = P.Caja("Pestaña " + _vistas[i].Titulo, barra.transform, Color.clear, 26f);
                P.Colocar(f.rectTransform, x, 7f, anchos[i], 52f);
                P.Clicable(f, () => { if (indice != _actual) { EntradaDelMenu.Sonido("UI_Navigate"); MostrarPestana(indice); } });
                var t = P.Txt("Texto", f.transform, _vistas[i].Titulo, 25f, P.SobreNoche, true, TextAlignmentOptions.Center);
                P.Rellenar(t.rectTransform);
                _fondosDePestana.Add(f);
                _textosDePestana.Add(t);
                x += anchos[i] + 6f;
            }
            P.Colocar(barra.rectTransform, Margen + 60f, 35f, x + 2f, 66f);
            var rb = P.TxtConIconos("RB", raiz, P.Boton(InputGlyphNames.ShoulderRight), 34f, P.SobreNoche, TextAlignmentOptions.Center);
            P.Colocar(rb.rectTransform, Margen + 60f + x + 8f, 40f, 54f, 56f);

            // Selector de personaje (Magia y Equipo) o monedas (el resto).
            _selector = P.Vacio("Personajes", raiz);
            P.ColocarDerecha(_selector, Margen, 35f, 620f, 66f);
            float xs = 620f;
            var rt = P.TxtConIconos("RT", _selector, P.Boton(InputGlyphNames.TriggerRight), 30f, P.SobreNoche, TextAlignmentOptions.Center);
            xs -= 50f; P.Colocar(rt.rectTransform, xs, 6f, 50f, 54f);
            for (int i = _personajes.Length - 1; i >= 0; i--)
            {
                int indice = i;
                var ficha = P.Caja("Personaje " + _personajes[i], _selector, P.Ficha, 30f);
                xs -= 160f;
                P.Colocar(ficha.rectTransform, xs, 4f, 152f, 58f);
                P.Clicable(ficha, () => ElegirPersonaje(_personajes[indice]));
                var n = P.Txt("Nombre", ficha.transform, ControlDelJugador.Nombre(_personajes[i]), 24f, P.TextoSuave, true, TextAlignmentOptions.Center);
                P.Rellenar(n.rectTransform);
                _fichasDePersonaje.Insert(0, ficha);
                _nombresDePersonaje.Insert(0, n);
            }
            var lt = P.TxtConIconos("LT", _selector, P.Boton(InputGlyphNames.TriggerLeft), 30f, P.SobreNoche, TextAlignmentOptions.Center);
            xs -= 56f; P.Colocar(lt.rectTransform, xs, 6f, 50f, 54f);

            _monedas = P.Vacio("Monedas", raiz);
            P.ColocarDerecha(_monedas, Margen, 35f, 620f, 66f);
            _textoMonedas = P.Txt("Texto", _monedas, "", 28f, P.SobreNoche, true, TextAlignmentOptions.MidlineRight);
            P.Rellenar(_textoMonedas.rectTransform);
        }

        // ─── Abrir, cerrar, pestañas ──────────────────────────────────────────

        public void Abrir()
        {
            Personaje = ControlDelJugador.Activo;
            if (!ControlDelJugador.EnElGrupo(Personaje)) Personaje = Slot.Will;
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
                _fondosDePestana[i].color = activa ? P.Oro : Color.clear;
                _textosDePestana[i].color = activa ? P.OroOscuro : P.SobreNoche;
                string titulo = _vistas[i].Titulo;
                if (_vistas[i] is VistaDeSintoniaDelMenu && pendientes > 0)
                    titulo += activa ? "  <size=80%>★" + pendientes + "</size>" : "  <color=#F5C542><size=80%>★" + pendientes + "</size></color>";
                _textosDePestana[i].text = titulo;
            }
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
                    P.Activar(_fichasDePersonaje[i].gameObject, esta);
                    bool elegido = _personajes[i] == Personaje;
                    _fichasDePersonaje[i].color = elegido ? P.Oro : P.Ficha;
                    _nombresDePersonaje[i].color = elegido ? P.OroOscuro : P.TextoSuave;
                }
            }
            else _textoMonedas.text = TextoDeMonedas();
        }

        public void RefrescarPistas()
        {
            string p = Vista != null ? Vista.Pistas : "";
            string comunes = (UsaX ? "" : "     " + P.Boton(InputGlyphNames.West) + " Controles")
                             + "     " + P.Boton(InputGlyphNames.North) + " Salir al menú"
                             + "     " + P.Boton(InputGlyphNames.East) + " Volver";
            string todo = p + comunes;
            if (todo == _pistasPuestas) return;
            _pistasPuestas = todo;
            _pistas.text = todo;
        }

        private static string TextoDeMonedas()
        {
            if (!PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true) || inv == null) return "";
            var partes = new List<string>();
            foreach (var e in inv.GetAllItems())
                if (e.item != null && e.item.usageKind == ItemData.ItemUsageKind.Currency && e.count > 0)
                    partes.Add(e.item.GetLocalizedName(e.count) + "  <color=#F5C542>" + e.count + "</color>");
            return string.Join("        ", partes);
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

        // ─── Fondo ────────────────────────────────────────────────────────────

        private static Sprite s_fondo;

        private static Sprite FondoRadial()
        {
            if (s_fondo != null) return s_fondo;
            const int ancho = 192, alto = 108;
            var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[ancho * alto];
            var centro = new Vector2(ancho * 0.42f, alto * 0.78f);
            for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
            {
                float d = Vector2.Distance(new Vector2(x / 1.0f, y * 1.55f), new Vector2(centro.x, centro.y * 1.55f)) / (ancho * 0.75f);
                px[y * ancho + x] = Color.Lerp(P.FondoAlto, P.Fondo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d)));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            s_fondo = Sprite.Create(tex, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
            return s_fondo;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_fondo = null; }
#endif
    }
}
