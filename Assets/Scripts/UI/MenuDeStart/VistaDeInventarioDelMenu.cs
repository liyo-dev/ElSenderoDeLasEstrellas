using System.Collections.Generic;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = Sendero.UI.PiezasDelMenuDeStart;

namespace Sendero.UI
{
    /// <summary>
    /// Pestaña Inventario del menú de Start (maqueta «1 · Inventario»): a la izquierda las
    /// categorías (Misión, Consumibles, Recuerdos y Tesoros, con su cuenta; LT/RT o clic), en el
    /// centro la hoja con la cuadrícula de la categoría (5 × 5, la casilla elegida en dorado y los
    /// huecos con raya discontinua) y a la derecha el objeto elegido en grande con su tipo, su
    /// descripción y lo que hace. A usa el objeto si se puede usar.
    /// La categoría sale del tipo de uso del objeto (<see cref="ItemData.usageKind"/>): los de
    /// misión son Quest; los consumibles que se usan desde el inventario, Consumibles; el resto,
    /// Recuerdos. Las monedas no salen (van en la cabecera). Tesoros es el registro de tesoros por
    /// región (<see cref="RegistroDeTesoros"/>).
    /// </summary>
    public sealed class VistaDeInventarioDelMenu : IVistaDelMenuDeStart
    {
        private enum Categoria { Mision, Consumibles, Recuerdos, Tesoros }
        private const int Categorias = 4;
        private static readonly string[] NombresDeCategoria = { "Misión", "Consumibles", "Recuerdos", "Tesoros" };
        private static readonly string[] Etiquetas = { "Objeto de misión", "Consumible", "Recuerdo", "" };

        private const int Columnas = 5, FilasVisibles = 5;
        private const float Lado = 128f, Paso = 150f;

        private MenuDeStart _menu;
        private readonly List<PiezasDeUI.Carta> _chips = new();
        private readonly List<TextMeshProUGUI> _textosDeChip = new(), _cuentasDeChip = new();
        private readonly List<P.Casilla> _casillas = new();
        private readonly List<Image> _iconos = new();
        private readonly List<TextMeshProUGUI> _cantidades = new();
        private readonly List<Inventory.Entry>[] _porCategoria = { new(), new(), new(), new() };
        private Categoria _categoria;
        private int _elegido, _primeraFila;

        private Image _iconoGrande;
        private TextMeshProUGUI _tituloHoja, _etiqueta, _nombre, _descripcion, _efecto, _vacio;
        private GameObject _detalle, _chipEtiqueta;
        private string _aviso = "";

        public string Titulo => "Inventario";
        public bool UsaPersonaje => false;
        public bool UsaX => false;
        public bool UsaGatillos => true;

        public string Pistas
        {
            get
            {
                var e = Elegido;
                string p = P.Boton(InputGlyphNames.MenuLT) + P.Boton(InputGlyphNames.MenuRT) + " Categoría";
                if (e.item != null && Usable(e.item)) p = P.Boton(InputGlyphNames.Confirm) + " Usar     " + p;
                return p;
            }
        }

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            for (int i = 0; i < NombresDeCategoria.Length; i++)
            {
                int indice = i;
                var chip = PiezasDeUI.NuevaCarta("Categoría " + NombresDeCategoria[i], cuerpo, PaletaUI.PapelApagado, 16f, 5f);
                PiezasDeUI.ColocarArribaIzquierda(chip.raiz, 54f, 160f + i * 94f, 300f, 70f);
                var tapa = chip.raiz.gameObject.AddComponent<Image>();
                tapa.color = new Color(1f, 1f, 1f, 0f);
                P.Clicable(tapa, () => PonerCategoria((Categoria)indice));
                var t = P.Titulo("Texto", chip.contenido, NombresDeCategoria[i], 28f, P.TextoMaqueta);
                P.Rellenar(t.rectTransform);
                t.margin = new Vector4(22f, -4f, 60f, 0f);
                var cuenta = P.Pastilla("Cuenta", chip.contenido, PaletaUI.PapelClaro, 250f, 14f, 34f, 34f, 20f, out _);
                cuenta.font = P.FuenteTitulos;
                _chips.Add(chip);
                _textosDeChip.Add(t);
                _cuentasDeChip.Add(cuenta);
            }
            var nota = P.Txt("Nota", cuerpo, "Los objetos de misión no se pueden tirar.", 21f,
                new Color(P.SobreNoche.r, P.SobreNoche.g, P.SobreNoche.b, 0.85f), false, TextAlignmentOptions.TopLeft, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(nota.rectTransform, 54f, 160f + NombresDeCategoria.Length * 94f + 38f, 300f, 120f);

            P.Hoja("Objetos", cuerpo, 400f, 160f, 800f, 800f);
            _tituloHoja = P.Etiqueta("Título", cuerpo, "", 430f, 136f, 260f, 56f, 34f);
            for (int i = 0; i < Columnas * FilasVisibles; i++)
            {
                int indice = i;
                int col = i % Columnas, fila = i / Columnas;
                var casilla = P.Casilla.Crear("Casilla " + i, cuerpo, 20f, 36f, () => AlClic(indice), () => AlClic(indice, true));
                PiezasDeUI.ColocarArribaIzquierda(casilla.raiz, 438f + col * Paso, 224f + fila * Paso, Lado, Lado, Giro(i));
                var icono = PiezasDeUI.Imagen("Icono", casilla.contenido, null, Color.white);
                icono.preserveAspect = true;
                icono.raycastTarget = false;
                P.Rellenar(icono.rectTransform, 20f);
                var n = P.Titulo("Cantidad", casilla.contenido, "", 24f, PaletaUI.TintaDeTexto, TextAlignmentOptions.BottomRight);
                P.Rellenar(n.rectTransform, 12f);
                _casillas.Add(casilla);
                _iconos.Add(icono);
                _cantidades.Add(n);
            }

            P.Hoja("Detalle", cuerpo, 1250f, 160f, 562f, 800f);
            _detalle = P.Vacio("Contenido", cuerpo).gameObject;
            P.Rellenar((RectTransform)_detalle.transform);
            var dc = _detalle.transform;
            var halo = PiezasDeUI.Imagen("Halo", dc, PiezasDeUI.Resplandor, new Color32(0xFF, 0xE9, 0xA3, 0xFF));
            halo.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(halo.rectTransform, 1381f, 212f, 300f, 300f);
            _iconoGrande = PiezasDeUI.Imagen("Icono", dc, null, Color.white);
            _iconoGrande.preserveAspect = true;
            _iconoGrande.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_iconoGrande.rectTransform, 1431f, 260f, 200f, 200f);
            PiezasDeUI.EstrellaEn("Chispa", dc, 1387f, 263f, 34f, 12f);
            PiezasDeUI.EstrellaEn("Chispa", dc, 1683f, 303f, 26f, 12f);
            PiezasDeUI.EstrellaEn("Chispa", dc, 1660f, 430f, 20f, 12f);
            PiezasDeUI.EstrellaEn("Chispa", dc, 1411f, 441f, 22f, 12f);
            _nombre = P.Titulo("Nombre", dc, "", 46f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Top);
            PiezasDeUI.ColocarArribaIzquierda(_nombre.rectTransform, 1280f, 526f, 502f, 62f);
            _nombre.enableAutoSizing = true; _nombre.fontSizeMin = 30f; _nombre.fontSizeMax = 46f;
            var chipTipo = PiezasDeUI.NuevaCarta("Tipo", dc, PaletaUI.PapelApagado, 20f, 0f);
            PiezasDeUI.ColocarArribaIzquierda(chipTipo.raiz, 1431f, 596f, 200f, 40f);
            _chipEtiqueta = chipTipo.raiz.gameObject;
            _etiqueta = P.Txt("Texto", chipTipo.contenido, "", 20f, P.TextoMaqueta, false, TextAlignmentOptions.Center);
            P.Rellenar(_etiqueta.rectTransform);
            _descripcion = P.Txt("Descripción", dc, "", 26f, PaletaUI.TintaDeTexto, false, TextAlignmentOptions.Top, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_descripcion.rectTransform, 1294f, 660f, 474f, 160f);
            _descripcion.enableAutoSizing = true; _descripcion.fontSizeMin = 20f; _descripcion.fontSizeMax = 26f;
            var raya = PiezasDeUI.Imagen("Raya", dc, null, new Color(PaletaUI.Tinta.r, PaletaUI.Tinta.g, PaletaUI.Tinta.b, 0.35f));
            raya.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(raya.rectTransform, 1294f, 836f, 474f, 3f);
            _efecto = P.Txt("Efecto", dc, "", 22f, P.OroDeTexto, false, TextAlignmentOptions.Top, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_efecto.rectTransform, 1294f, 856f, 474f, 90f);

            _vacio = P.Txt("Vacío", cuerpo, "No hay nada en esta categoría.", 26f, P.TextoMaqueta, false, TextAlignmentOptions.Center, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_vacio.rectTransform, 1290f, 200f, 482f, 720f);
        }

        // Cada casilla con un giro pequeño, como dibujadas a mano.
        private static float Giro(int i) => (i * 7 % 5 - 2) * 0.45f;

        // ─── Datos ────────────────────────────────────────────────────────────

        private static Categoria CategoriaDe(ItemData item)
        {
            if (item.usageKind == ItemData.ItemUsageKind.Quest) return Categoria.Mision;
            if (item.usageKind == ItemData.ItemUsageKind.Consumable && item.usableFromInventory) return Categoria.Consumibles;
            return Categoria.Recuerdos;
        }

        private static bool Usable(ItemData item) => item.usableFromInventory;

        private Inventory.Entry Elegido
        {
            get
            {
                var lista = _porCategoria[(int)_categoria];
                return _elegido >= 0 && _elegido < lista.Count ? lista[_elegido] : default;
            }
        }

        private void Leer()
        {
            foreach (var l in _porCategoria) l.Clear();
            if (!PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true) || inv == null) return;
            foreach (var e in inv.GetAllItems())
            {
                if (e.item == null || e.count <= 0 || e.item.usageKind == ItemData.ItemUsageKind.Currency) continue;
                _porCategoria[(int)CategoriaDe(e.item)].Add(e);
            }
            foreach (var l in _porCategoria)
                l.Sort((a, b) => string.Compare(a.item.GetLocalizedName(), b.item.GetLocalizedName(), System.StringComparison.CurrentCultureIgnoreCase));
        }

        // ─── Mostrar ──────────────────────────────────────────────────────────

        public void Mostrar()
        {
            Leer();
            if (_porCategoria[(int)_categoria].Count == 0)
                for (int i = 0; i < _porCategoria.Length; i++)
                    if (_porCategoria[i].Count > 0) { _categoria = (Categoria)i; break; }
            _aviso = "";
            _elegido = Mathf.Clamp(_elegido, 0, Mathf.Max(0, _porCategoria[(int)_categoria].Count - 1));
            Pintar();
        }

        public void Ocultar() { }
        public void AlCambiarPersonaje() { }

        private void PonerCategoria(Categoria c)
        {
            if (c == _categoria) return;
            EntradaDelMenu.Sonido("UI_Navigate");
            _categoria = c;
            _elegido = 0;
            _primeraFila = 0;
            _aviso = "";
            Pintar();
            _menu.RefrescarPistas();
        }

        private void Pintar()
        {
            var lista = _porCategoria[(int)_categoria];
            for (int i = 0; i < _chips.Count; i++)
            {
                bool activa = i == (int)_categoria;
                _chips[i].Pintar(activa ? PaletaUI.Oro : PaletaUI.PapelApagado);
                _chips[i].raiz.localRotation = Quaternion.Euler(0f, 0f, activa ? 1.5f : -0.8f);
                _textosDeChip[i].color = activa ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
                int cuenta = (Categoria)i == Categoria.Tesoros ? TesorosEncontrados() : _porCategoria[i].Count;
                _cuentasDeChip[i].text = cuenta.ToString();
            }
            _tituloHoja.text = NombresDeCategoria[(int)_categoria];

            int filaElegida = _elegido / Columnas;
            if (filaElegida < _primeraFila) _primeraFila = filaElegida;
            if (filaElegida >= _primeraFila + FilasVisibles) _primeraFila = filaElegida - FilasVisibles + 1;
            for (int i = 0; i < _casillas.Count; i++)
            {
                int k = _primeraFila * Columnas + i;
                bool hay = k < lista.Count;
                _casillas[i].Poner(!hay ? P.EstadoDeCasilla.Vacia : k == _elegido ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
                _iconos[i].enabled = hay && lista[k].item.icon != null;
                _iconos[i].sprite = hay ? lista[k].item.icon : null;
                _cantidades[i].text = hay && lista[k].count > 1 ? "×" + lista[k].count : "";
            }

            var e = Elegido;
            bool hayAlgo = e.item != null;
            P.Activar(_detalle, hayAlgo);
            P.Activar(_vacio.gameObject, !hayAlgo);
            if (!hayAlgo)
            {
                bool tesoros = _categoria == Categoria.Tesoros;
                _vacio.alignment = tesoros ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
                _vacio.text = tesoros ? TextoDeTesoros() : "No hay nada en esta categoría.";
                return;
            }
            int cat = (int)_categoria;
            _iconoGrande.sprite = e.item.icon;
            _iconoGrande.enabled = e.item.icon != null;
            P.Activar(_chipEtiqueta, Etiquetas[cat].Length > 0);
            _etiqueta.text = Etiquetas[cat];
            _nombre.text = e.item.GetLocalizedName() + (e.count > 1 ? "  <size=70%><color=#6B5844>×" + e.count + "</color></size>" : "");
            string desc = e.item.GetLocalizedDescription();
            _descripcion.text = string.IsNullOrWhiteSpace(desc) ? "" : desc;
            string efecto = Efecto(e.item);
            _efecto.text = _aviso.Length > 0 ? _aviso : efecto;
        }

        private static int TesorosEncontrados()
        {
            int n = 0;
            foreach (var r in RegistroDeTesoros.Regiones()) n += r.Encontrados;
            return n;
        }

        private static string TextoDeTesoros()
        {
            string Texto(string clave, string porDefecto)
                => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, porDefecto) : porDefecto;
            var regiones = RegistroDeTesoros.Regiones();
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=130%><b>").Append(Texto("TESORO_REGISTRO_TITULO", "Tesoros")).Append("</b></size>\n\n");
            if (regiones.Count == 0) sb.Append(Texto("TESORO_REGISTRO_VACIO", "Aún no sabes de ningún tesoro."));
            string fila = Texto("TESORO_REGISTRO_FILA", "{0}: {1} / {2}");
            for (int i = 0; i < regiones.Count; i++)
                sb.AppendFormat(fila, regiones[i].Nombre, regiones[i].Encontrados, regiones[i].Total).Append('\n');
            return sb.ToString();
        }

        private static string Efecto(ItemData item)
        {
            if (item.useEffects == null) return "";
            var partes = new List<string>();
            foreach (var ef in item.useEffects)
            {
                switch (ef.effectType)
                {
                    case PickupEffectType.HealthRestore: partes.Add("Recupera vida al personaje al mando"); break;
                    case PickupEffectType.ManaRestore: partes.Add("Recupera magia al personaje al mando"); break;
                    case PickupEffectType.Revivir: partes.Add("Levanta a un compañero caído"); break;
                    case PickupEffectType.SpecialCharge: partes.Add("Llena la carga de equipo"); break;
                }
            }
            return string.Join(" · ", partes);
        }

        // ─── Entrada ──────────────────────────────────────────────────────────

        public void Tick()
        {
            int g = EntradaDelMenu.Gatillos();
            if (g != 0) PonerCategoria((Categoria)(((int)_categoria + g + Categorias) % Categorias));
            var lista = _porCategoria[(int)_categoria];
            int h = EntradaDelMenu.Horizontal(), v = EntradaDelMenu.Vertical();
            if ((h != 0 || v != 0) && lista.Count > 0)
            {
                int nuevo = _elegido + h + v * Columnas;
                if (h != 0 && (nuevo / Columnas != _elegido / Columnas)) nuevo = _elegido;
                nuevo = Mathf.Clamp(nuevo, 0, lista.Count - 1);
                if (nuevo != _elegido) { _elegido = nuevo; _aviso = ""; EntradaDelMenu.Sonido("UI_Navigate"); Pintar(); _menu.RefrescarPistas(); }
            }
            if (EntradaDelMenu.Aceptar) Usar();
        }

        /// Clic en una casilla: la elige o, si ya lo estaba, usa el objeto. Al pasar el ratón, solo la elige.
        private void AlClic(int casilla, bool soloElegir = false)
        {
            int k = _primeraFila * Columnas + casilla;
            if (k >= _porCategoria[(int)_categoria].Count) return;
            if (k == _elegido) { if (!soloElegir) Usar(); return; }
            _elegido = k;
            _aviso = "";
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
            _menu.RefrescarPistas();
        }

        private void Usar()
        {
            var e = Elegido;
            if (e.item == null || !Usable(e.item) || _menu.UsarObjeto == null) return;
            string mensaje = _menu.UsarObjeto(e.item);
            Leer();
            _elegido = Mathf.Clamp(_elegido, 0, Mathf.Max(0, _porCategoria[(int)_categoria].Count - 1));
            _aviso = mensaje ?? "";
            Pintar();
            _menu.AlCambiarEstadisticas?.Invoke();
            _menu.RefrescarPistas();
        }

        public bool Cancelar() => false;
    }
}
