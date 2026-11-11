using System.Collections.Generic;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using P = Sendero.UI.PiezasDelMenuDeStart;

namespace Sendero.UI
{
    /// <summary>
    /// Pestaña Inventario del menú de Start: objetos por categoría (Consumibles, Misión, Recuerdos;
    /// LT/RT o clic), cuadrícula de iconos con la cantidad solo si hay más de uno, y a la derecha
    /// el objeto elegido en grande con su descripción, lo que hace y «Usar» si se puede usar.
    /// La categoría sale del tipo de uso del objeto (<see cref="ItemData.usageKind"/>): los de
    /// misión son Quest; los consumibles que se usan desde el inventario, Consumibles; el resto,
    /// Recuerdos. Las monedas no salen (van en la cabecera). La última categoría, Tesoros, es el
    /// registro de tesoros por región (<see cref="RegistroDeTesoros"/>).
    /// </summary>
    public sealed class VistaDeInventarioDelMenu : IVistaDelMenuDeStart
    {
        private enum Categoria { Consumibles, Mision, Recuerdos, Tesoros }
        private const int Categorias = 4;
        private static readonly string[] NombresDeCategoria = { "Consumibles", "Misión", "Recuerdos", "Tesoros" };
        private static readonly string[] Etiquetas = { "Consumible", "Objeto de misión", "Recuerdo", "" };
        private static readonly Color[] ColoresDeEtiqueta = { P.Verde, P.Oro, new Color32(0xB9, 0xA3, 0xFF, 0xFF), P.Oro };

        private const int Columnas = 5, FilasVisibles = 4;
        private const float Lado = 160f, Hueco = 18f;

        private MenuDeStart _menu;
        private readonly List<Image> _chips = new();
        private readonly List<TextMeshProUGUI> _textosDeChip = new();
        private readonly List<Image> _casillas = new();
        private readonly List<Image> _bordes = new();
        private readonly List<Image> _iconos = new();
        private readonly List<TextMeshProUGUI> _cantidades = new();
        private readonly List<Inventory.Entry>[] _porCategoria = { new(), new(), new(), new() };
        private Categoria _categoria;
        private int _elegido, _primeraFila;

        private Image _iconoGrande, _usar;
        private TextMeshProUGUI _etiqueta, _nombre, _tienes, _descripcion, _efecto, _aviso, _vacio;
        private GameObject _cajaDeEfecto, _detalle;

        public string Titulo => "Inventario";
        public bool UsaPersonaje => false;
        public bool UsaX => false;

        public string Pistas
        {
            get
            {
                var e = Elegido;
                string p = P.Boton(InputGlyphNames.TriggerLeft) + P.Boton(InputGlyphNames.TriggerRight) + " Categoría";
                if (e.item != null && Usable(e.item)) p = P.Boton(InputGlyphNames.South) + " Usar     " + p;
                return p;
            }
        }

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            var izq = P.CajaConBorde("Objetos", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)izq.transform.parent, 0f, 0f, 960f, 886f);
            var c = izq.transform;

            float x = 30f;
            for (int i = 0; i < NombresDeCategoria.Length; i++)
            {
                int indice = i;
                var chip = P.Caja("Categoría " + NombresDeCategoria[i], c, P.Ficha, 16f);
                P.Colocar(chip.rectTransform, x, 26f, 210f, 58f);
                P.Clicable(chip, () => PonerCategoria((Categoria)indice));
                var t = P.Txt("Texto", chip.transform, NombresDeCategoria[i], 24f, P.TextoSuave, true, TextAlignmentOptions.Center);
                P.Rellenar(t.rectTransform);
                _chips.Add(chip);
                _textosDeChip.Add(t);
                x += 222f;
            }

            float x0 = (954f - (Columnas * Lado + (Columnas - 1) * Hueco)) * 0.5f;
            for (int i = 0; i < Columnas * FilasVisibles; i++)
            {
                int indice = i;
                int col = i % Columnas, fila = i / Columnas;
                var casilla = P.CajaConBorde("Casilla " + i, c, P.Ficha, P.Ficha, 4f, 22f);
                var borde = (Image)casilla.transform.parent.GetComponent<Image>();
                P.Colocar(borde.rectTransform, x0 + col * (Lado + Hueco), 116f + fila * (Lado + Hueco), Lado, Lado);
                P.Clicable(borde, () => AlClic(indice));
                _bordes.Add(borde);
                var icono = PiezasDeUI.Imagen("Icono", casilla.transform, null, Color.white);
                icono.preserveAspect = true;
                icono.raycastTarget = false;
                P.Rellenar(icono.rectTransform, 22f);
                var n = P.Txt("Cantidad", casilla.transform, "", 22f, Color.white, true, TextAlignmentOptions.BottomRight);
                P.Rellenar(n.rectTransform, 10f);
                _casillas.Add(casilla);
                _iconos.Add(icono);
                _cantidades.Add(n);
            }

            var der = P.CajaConBorde("Detalle", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)der.transform.parent, 993f, 0f, 819f, 886f);
            var d = der.transform;
            _detalle = P.Vacio("Contenido", d).gameObject;
            P.Rellenar((RectTransform)_detalle.transform);
            var dc = _detalle.transform;
            var marco = P.Caja("Marco del icono", dc, P.FichaOscura, 30f);
            P.Colocar(marco.rectTransform, 36f, 36f, 200f, 200f);
            _iconoGrande = PiezasDeUI.Imagen("Icono", marco.transform, null, Color.white);
            _iconoGrande.preserveAspect = true;
            _iconoGrande.raycastTarget = false;
            P.Rellenar(_iconoGrande.rectTransform, 22f);
            _etiqueta = P.Txt("Etiqueta", dc, "", 20f, P.Oro, true);
            P.Colocar(_etiqueta.rectTransform, 266f, 54f, 510f, 30f);
            _nombre = P.Txt("Nombre", dc, "", 44f, P.Texto, true, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_nombre.rectTransform, 266f, 92f, 510f, 110f);
            _tienes = P.Txt("Cantidad", dc, "", 24f, P.TextoSuave, true);
            P.Colocar(_tienes.rectTransform, 266f, 196f, 510f, 34f);
            _descripcion = P.Txt("Descripción", dc, "", 28f, new Color32(0xDF, 0xE2, 0xFF, 0xFF), false, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_descripcion.rectTransform, 36f, 270f, 747f, 230f);
            var efecto = P.Caja("Efecto", dc, new Color(0.3f, 0.82f, 0.49f, 0.14f), 16f);
            P.Colocar(efecto.rectTransform, 36f, 512f, 747f, 64f);
            _cajaDeEfecto = efecto.gameObject;
            _efecto = P.Txt("Texto", efecto.transform, "", 24f, new Color32(0x9F, 0xF0, 0xBD, 0xFF), true, TextAlignmentOptions.MidlineLeft);
            P.Rellenar(_efecto.rectTransform, 18f);
            _aviso = P.Txt("Aviso", dc, "", 24f, P.Oro, true, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_aviso.rectTransform, 36f, 600f, 747f, 80f);
            _usar = P.Caja("Usar", dc, P.Oro, 18f);
            P.Colocar(_usar.rectTransform, 36f, 780f, 240f, 70f);
            P.Clicable(_usar, Usar);
            var tu = P.TxtConIconos("Texto", _usar.transform, P.Boton(InputGlyphNames.South) + "  Usar", 28f, P.OroOscuro, TextAlignmentOptions.Center);
            tu.fontStyle = FontStyles.Bold;
            P.Rellenar(tu.rectTransform);

            _vacio = P.Txt("Vacío", d, "No hay nada en esta categoría.", 28f, P.TextoApagado, false, TextAlignmentOptions.Center, envolver: true);
            P.Rellenar(_vacio.rectTransform, 60f);
        }

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
            _aviso.text = "";
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
            _aviso.text = "";
            Pintar();
            _menu.RefrescarPistas();
        }

        private void Pintar()
        {
            var lista = _porCategoria[(int)_categoria];
            for (int i = 0; i < _chips.Count; i++)
            {
                bool activa = i == (int)_categoria;
                _chips[i].color = activa ? P.Borde : P.Ficha;
                _textosDeChip[i].color = activa ? new Color32(0x14, 0x12, 0x2F, 0xFF) : P.TextoSuave;
                int cuenta = (Categoria)i == Categoria.Tesoros ? TesorosEncontrados() : _porCategoria[i].Count;
                _textosDeChip[i].text = NombresDeCategoria[i] + "  <size=80%>" + cuenta + "</size>";
            }

            int filaElegida = _elegido / Columnas;
            if (filaElegida < _primeraFila) _primeraFila = filaElegida;
            if (filaElegida >= _primeraFila + FilasVisibles) _primeraFila = filaElegida - FilasVisibles + 1;
            for (int i = 0; i < _casillas.Count; i++)
            {
                int k = _primeraFila * Columnas + i;
                bool hay = k < lista.Count;
                bool elegida = hay && k == _elegido;
                var fondo = elegida ? P.Elegida : hay ? P.Ficha : P.Hueco;
                _casillas[i].color = fondo;
                _bordes[i].color = elegida ? P.Oro : fondo;
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
            _etiqueta.text = Etiquetas[cat].ToUpperInvariant();
            _etiqueta.color = ColoresDeEtiqueta[cat];
            _nombre.text = e.item.GetLocalizedName();
            _tienes.text = e.count > 1 ? "Tienes " + e.count : "";
            string desc = e.item.GetLocalizedDescription();
            _descripcion.text = string.IsNullOrWhiteSpace(desc) ? "" : desc;
            string efecto = Efecto(e.item);
            P.Activar(_cajaDeEfecto, !string.IsNullOrEmpty(efecto));
            _efecto.text = efecto;
            P.Activar(_usar.gameObject, Usable(e.item));
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
                if (nuevo != _elegido) { _elegido = nuevo; _aviso.text = ""; EntradaDelMenu.Sonido("UI_Navigate"); Pintar(); _menu.RefrescarPistas(); }
            }
            if (EntradaDelMenu.Aceptar) Usar();
        }

        private void AlClic(int casilla)
        {
            int k = _primeraFila * Columnas + casilla;
            if (k >= _porCategoria[(int)_categoria].Count) return;
            if (k == _elegido) { Usar(); return; }
            _elegido = k;
            _aviso.text = "";
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
            Pintar();
            _aviso.text = mensaje ?? "";
            _menu.AlCambiarEstadisticas?.Invoke();
            _menu.RefrescarPistas();
        }

        public bool Cancelar() => false;
    }
}
