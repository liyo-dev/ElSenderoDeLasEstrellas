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
    /// Pestaña Magia del menú de Start: a la izquierda las 4 ranuras de la X (en combate LB pasa
    /// a la siguiente) y los combos de la Y con su secuencia; en el centro los hechizos básicos
    /// que conoce el personaje (los que faltan, sellados); a la derecha el hechizo resaltado:
    /// elemento, tipo, frase, maná, fuerza, cómo se lanza y lo que hace de especial.
    /// Se elige ranura y se pone un hechizo con A; X lo quita. Guarda como la pestaña vieja: Will
    /// en <c>basicSpellIds</c> del preset, Estela y Liam con <see cref="GrimorioDelPersonaje"/>.
    /// </summary>
    public sealed class VistaDeMagiaDelMenu : IVistaDelMenuDeStart
    {
        private enum Zona { Ranuras, Lista, Combos }
        private const int Ranuras = MagicCaster.BasicSlotCount, ColumnasDeLista = 3, MaxCombos = 6, MaxLista = 12;

        private MenuDeStart _menu;
        private readonly List<Image> _ranuras = new(), _bordesDeRanura = new(), _iconosDeRanura = new();
        private readonly List<Image> _cartas = new(), _bordesDeCarta = new(), _iconosDeCarta = new();
        private readonly List<TextMeshProUGUI> _nombresDeCarta = new(), _numerosDeCarta = new();
        private readonly List<Image> _combos = new(), _iconosDeCombo = new();
        private readonly List<TextMeshProUGUI> _nombresDeCombo = new(), _secuencias = new();
        private TextMeshProUGUI _tituloLista, _cuentaLista, _nombre, _pildoras, _lore, _mana, _fuerzaEtiqueta, _fuerza, _lanza, _especial, _sellado, _ponerTexto;
        private Image _iconoGrande, _marcoGrande, _poner;
        private GameObject _datos;

        private readonly List<SpellId> _equipados = new();
        private readonly List<(MagicSpellSO s, bool conocido)> _lista = new();
        private readonly List<(MagicSpellSO s, bool conocido)> _listaCombos = new();
        private Zona _zona = Zona.Lista;
        private int _ranura, _indice, _combo;
        private PlayerPresetSO _preset;

        public string Titulo => "Magia";
        public bool UsaPersonaje => true;
        public bool UsaX => true;

        /// El hechizo resaltado (el Grimorio se abre por él).
        public SpellId Resaltado { get { var s = SpellResaltado(out _); return s != null ? s.spellId : SpellId.None; } }

        public string Pistas
        {
            get
            {
                string p = "";
                var s = SpellResaltado(out bool conocido);
                if (_zona == Zona.Lista && s != null && conocido) p += P.Boton(InputGlyphNames.South) + " Poner en la ranura " + (_ranura + 1) + "     ";
                if (_zona == Zona.Ranuras) p += P.Boton(InputGlyphNames.South) + " Elegir hechizo     ";
                if ((_zona == Zona.Ranuras && _ranura < _equipados.Count) || (_zona == Zona.Lista && s != null && _equipados.Contains(s.spellId)))
                    p += P.Boton(InputGlyphNames.West) + " Quitar     ";
                if (GrimorioDelPersonaje.TieneGrimorio) p += "<b>Select</b> Grimorio";
                return p;
            }
        }

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            MontarIzquierda(cuerpo);
            MontarCentro(cuerpo);
            MontarDerecha(cuerpo);
        }

        private void MontarIzquierda(RectTransform cuerpo)
        {
            var izq = P.CajaConBorde("Lo que lleva", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)izq.transform.parent, 0f, 0f, 500f, 886f);
            var c = izq.transform;
            var t = P.TxtConIconos("Título X", c, P.Boton(InputGlyphNames.West) + "  <b>HECHIZOS A MANO</b>", 22f, P.TextoApagado);
            P.Colocar(t.rectTransform, 24f, 20f, 440f, 44f);
            for (int i = 0; i < Ranuras; i++)
            {
                int indice = i;
                var r = P.CajaConBorde("Ranura " + (i + 1), c, P.Ficha, P.Ficha, 4f, 22f);
                var borde = r.transform.parent.GetComponent<Image>();
                P.Colocar(borde.rectTransform, 24f + i * 114f, 72f, 104f, 104f);
                P.Clicable(borde, () => AlClicRanura(indice));
                var icono = PiezasDeUI.Imagen("Icono", r.transform, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                P.Rellenar(icono.rectTransform, 16f);
                var n = P.Txt("Número", r.transform, (i + 1).ToString(), 20f, P.OroDeTexto, true);
                P.Colocar(n.rectTransform, 8f, 4f, 30f, 26f);
                _ranuras.Add(r); _bordesDeRanura.Add(borde); _iconosDeRanura.Add(icono);
            }
            var nota = P.TxtConIconos("Nota", c, "En combate, " + P.Boton(InputGlyphNames.ShoulderLeft) + " pasa al siguiente de la fila.", 20f, P.TextoApagado);
            P.Colocar(nota.rectTransform, 24f, 190f, 450f, 40f);
            var linea = P.Caja("Línea", c, P.BordeApagado, 1f);
            P.Colocar(linea.rectTransform, 24f, 244f, 446f, 2f);
            var tc = P.TxtConIconos("Título Y", c, P.Boton(InputGlyphNames.North) + "  <b>COMBOS</b>", 22f, P.TextoApagado);
            P.Colocar(tc.rectTransform, 24f, 262f, 440f, 44f);
            for (int i = 0; i < MaxCombos; i++)
            {
                int indice = i;
                var f = P.Caja("Combo " + i, c, P.Ficha, 16f);
                P.Colocar(f.rectTransform, 24f, 316f + i * 84f, 446f, 74f);
                P.Clicable(f, () => AlClicCombo(indice));
                var icono = PiezasDeUI.Imagen("Icono", f.transform, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                P.Colocar(icono.rectTransform, 10f, 10f, 54f, 54f);
                var n = P.Txt("Nombre", f.transform, "", 22f, P.Texto, true, TextAlignmentOptions.MidlineLeft);
                P.Colocar(n.rectTransform, 74f, 0f, 200f, 74f);
                var sq = P.TxtConIconos("Secuencia", f.transform, "", 26f, P.TextoSuave, TextAlignmentOptions.MidlineRight);
                P.ColocarDerecha(sq.rectTransform, 12f, 0f, 170f, 74f);
                _combos.Add(f); _iconosDeCombo.Add(icono); _nombresDeCombo.Add(n); _secuencias.Add(sq);
            }
        }

        private void MontarCentro(RectTransform cuerpo)
        {
            var cen = P.CajaConBorde("Hechizos", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)cen.transform.parent, 518f, 0f, 736f, 886f);
            var c = cen.transform;
            _tituloLista = P.Txt("Título", c, "", 22f, P.TextoApagado, true);
            P.Colocar(_tituloLista.rectTransform, 24f, 26f, 520f, 34f);
            _cuentaLista = P.Txt("Cuenta", c, "", 22f, P.TextoSuave, true, TextAlignmentOptions.TopRight);
            P.ColocarDerecha(_cuentaLista.rectTransform, 24f, 26f, 160f, 34f);
            for (int i = 0; i < MaxLista; i++)
            {
                int indice = i;
                int col = i % ColumnasDeLista, fila = i / ColumnasDeLista;
                var carta = P.CajaConBorde("Hechizo " + i, c, P.Ficha, P.Ficha, 4f, 20f);
                var borde = carta.transform.parent.GetComponent<Image>();
                P.Colocar(borde.rectTransform, 22f + col * 230f, 76f + fila * 198f, 216f, 186f);
                P.Clicable(borde, () => AlClicCarta(indice));
                var icono = PiezasDeUI.Imagen("Icono", carta.transform, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                P.Colocar(icono.rectTransform, 62f, 18f, 88f, 88f);
                var n = P.Txt("Nombre", carta.transform, "", 21f, P.Texto, true, TextAlignmentOptions.Top, envolver: true);
                P.Colocar(n.rectTransform, 8f, 116f, 196f, 60f);
                var num = P.Txt("Ranura", carta.transform, "", 20f, P.OroOscuro, true, TextAlignmentOptions.Center);
                P.ColocarDerecha(num.rectTransform, 8f, 8f, 34f, 34f);
                _cartas.Add(carta); _bordesDeCarta.Add(borde); _iconosDeCarta.Add(icono); _nombresDeCarta.Add(n); _numerosDeCarta.Add(num);
            }
        }

        private void MontarDerecha(RectTransform cuerpo)
        {
            var der = P.CajaConBorde("Detalle", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)der.transform.parent, 1272f, 0f, 540f, 886f);
            var c = der.transform;
            _marcoGrande = P.Caja("Marco", c, P.Borde, 70f);
            P.Colocar(_marcoGrande.rectTransform, 30f, 30f, 140f, 140f);
            var dentro = P.Caja("Dentro", _marcoGrande.transform, P.FichaOscura, 67f);
            P.Rellenar(dentro.rectTransform, 3f);
            _iconoGrande = PiezasDeUI.Imagen("Icono", _marcoGrande.transform, null, Color.white);
            _iconoGrande.preserveAspect = true; _iconoGrande.raycastTarget = false;
            P.Rellenar(_iconoGrande.rectTransform, 20f);
            _nombre = P.Txt("Nombre", c, "", 36f, P.Texto, true, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_nombre.rectTransform, 190f, 40f, 320f, 90f);
            _pildoras = P.Txt("Elemento y tipo", c, "", 19f, P.TextoSuave, true);
            P.Colocar(_pildoras.rectTransform, 190f, 132f, 320f, 30f);
            _lore = P.Txt("Frase", c, "", 23f, P.Texto, false, TextAlignmentOptions.TopLeft, envolver: true);
            _lore.fontStyle = FontStyles.Italic;
            P.Colocar(_lore.rectTransform, 30f, 196f, 480f, 120f);

            _datos = P.Vacio("Datos", c).gameObject;
            P.Rellenar((RectTransform)_datos.transform);
            _mana = Fila(_datos.transform, 332f, "Maná", out _);
            _fuerza = Fila(_datos.transform, 402f, "Fuerza", out _fuerzaEtiqueta);
            _lanza = Fila(_datos.transform, 472f, "Se lanza", out _);
            _especial = P.Txt("Especial", _datos.transform, "", 21f, P.Verde, true, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_especial.rectTransform, 30f, 546f, 480f, 90f);
            _sellado = P.Txt("Sellado", c, "", 22f, P.TextoSuave, false, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_sellado.rectTransform, 30f, 340f, 480f, 120f);

            _poner = P.Caja("Poner", c, P.Oro, 18f);
            P.Colocar(_poner.rectTransform, 30f, 786f, 340f, 70f);
            P.Clicable(_poner, Poner);
            _ponerTexto = P.TxtConIconos("Texto", _poner.transform, "", 25f, P.OroOscuro, TextAlignmentOptions.Center);
            _ponerTexto.fontStyle = FontStyles.Bold;
            P.Rellenar(_ponerTexto.rectTransform);
        }

        private static TextMeshProUGUI Fila(Transform padre, float y, string etiqueta, out TextMeshProUGUI textoEtiqueta)
        {
            var caja = P.Caja("Fila " + etiqueta, padre, P.FichaOscura, 14f);
            P.Colocar(caja.rectTransform, 30f, y, 480f, 58f);
            textoEtiqueta = P.Txt("Etiqueta", caja.transform, etiqueta, 22f, P.TextoSuave, true, TextAlignmentOptions.MidlineLeft);
            P.Colocar(textoEtiqueta.rectTransform, 18f, 0f, 260f, 58f);
            var valor = P.TxtConIconos("Valor", caja.transform, "", 24f, P.Texto, TextAlignmentOptions.MidlineRight);
            valor.fontStyle = FontStyles.Bold;
            P.ColocarDerecha(valor.rectTransform, 18f, 0f, 260f, 58f);
            return valor;
        }

        // ─── Datos ────────────────────────────────────────────────────────────

        private Slot Personaje => _menu.Personaje;
        private bool EsWill => Personaje == Slot.Will;

        private void Leer()
        {
            _preset = GameBootService.IsAvailable && GameBootService.Profile != null ? Partida.Preset : null;
            _equipados.Clear();
            if (_preset != null)
            {
                var src = EsWill ? _preset.basicSpellIds : GrimorioDelPersonaje.IdsEquipados(_preset, Personaje, crear: false);
                if (src == null)
                {
                    foreach (var s in GrimorioDelPersonaje.BasicosEquipados(Personaje))
                        if (s != null) _equipados.Add(s.spellId);
                }
                else
                {
                    foreach (var id in src)
                        if (_equipados.Count < Ranuras && id != SpellId.None && !_equipados.Contains(id) && Equipable(GrimorioDelPersonaje.Hechizo(id)))
                            _equipados.Add(id);
                }
            }

            _lista.Clear();
            var disponibles = GrimorioDelPersonaje.BasicosDisponibles(Personaje);
            foreach (var s in disponibles) _lista.Add((s, true));
            _listaCombos.Clear();
            foreach (var s in GrimorioDelPersonaje.CombosDisponibles(Personaje)) _listaCombos.Add((s, true));

            var lib = GrimorioDelPersonaje.Biblioteca;
            if (lib != null)
                foreach (var s in lib.Spells)
                {
                    if (!GrimorioDelPersonaje.EsDelGrimorio(s) || s.caster != Personaje || s.spellId == SpellId.Amanecer) continue;
                    if (GrimorioDelPersonaje.EsBasico(s) && !disponibles.Contains(s) && _lista.Count < MaxLista) _lista.Add((s, false));
                    else if (s.HasCombo && !_listaCombos.Exists(x => x.s == s) && _listaCombos.Count < MaxCombos) _listaCombos.Add((s, false));
                }
        }

        private bool Equipable(MagicSpellSO s) => s != null && s.caster == Personaje && GrimorioDelPersonaje.EsBasico(s);

        private void Guardar()
        {
            if (_preset == null) return;
            List<SpellId> dst = EsWill ? (_preset.basicSpellIds ??= new List<SpellId>()) : GrimorioDelPersonaje.IdsEquipados(_preset, Personaje, crear: true);
            if (dst == null) return;
            dst.Clear();
            dst.AddRange(_equipados);
            if (EsWill)
            {
                if (ControlDelJugador.Activo == Slot.Will
                    && PlayerService.TryGetComponent<PlayerPresetService>(out var pps, includeInactive: true, allowSceneLookup: true) && pps != null)
                    pps.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);
            }
            else GrimorioDelPersonaje.Aplicar(Personaje);
        }

        private MagicSpellSO SpellResaltado(out bool conocido)
        {
            conocido = false;
            switch (_zona)
            {
                case Zona.Ranuras:
                    if (_ranura < _equipados.Count) { conocido = true; return GrimorioDelPersonaje.Hechizo(_equipados[_ranura]); }
                    return null;
                case Zona.Lista:
                    if (_indice < _lista.Count) { conocido = _lista[_indice].conocido; return _lista[_indice].s; }
                    return null;
                default:
                    if (_combo < _listaCombos.Count) { conocido = _listaCombos[_combo].conocido; return _listaCombos[_combo].s; }
                    return null;
            }
        }

        // ─── Mostrar ──────────────────────────────────────────────────────────

        public void Mostrar()
        {
            Leer();
            _ranura = Mathf.Clamp(_equipados.Count, 0, Ranuras - 1);
            _indice = Mathf.Clamp(_indice, 0, Mathf.Max(0, _lista.Count - 1));
            var nuevo = GrimorioDelPersonaje.UltimoAprendido;
            if (nuevo != SpellId.None)
            {
                int k = _lista.FindIndex(x => x.s != null && x.s.spellId == nuevo);
                if (k >= 0) { _indice = k; _zona = Zona.Lista; GrimorioDelPersonaje.UltimoAprendido = SpellId.None; }
            }
            Pintar();
        }

        public void Ocultar() { }

        public void AlCambiarPersonaje()
        {
            _indice = 0; _combo = 0; _zona = Zona.Lista;
            Mostrar();
        }

        private void Pintar()
        {
            for (int i = 0; i < Ranuras; i++)
            {
                var s = i < _equipados.Count ? GrimorioDelPersonaje.Hechizo(_equipados[i]) : null;
                bool foco = _zona == Zona.Ranuras && i == _ranura;
                bool destino = _zona == Zona.Lista && i == _ranura;
                var fondo = s != null ? P.Ficha : P.Hueco;
                _ranuras[i].color = foco ? P.Elegida : fondo;
                _bordesDeRanura[i].color = foco ? P.Oro : destino ? new Color(P.Oro.r, P.Oro.g, P.Oro.b, 0.45f) : fondo;
                _iconosDeRanura[i].enabled = s != null && s.attackIcon != null;
                _iconosDeRanura[i].sprite = s != null ? s.attackIcon : null;
            }

            _tituloLista.text = "HECHIZOS QUE CONOCE " + ControlDelJugador.Nombre(Personaje).ToUpperInvariant();
            int conocidos = 0;
            foreach (var x in _lista) if (x.conocido) conocidos++;
            _cuentaLista.text = conocidos + " de " + _lista.Count;
            for (int i = 0; i < MaxLista; i++)
            {
                bool hay = i < _lista.Count;
                P.Activar(_bordesDeCarta[i].gameObject, hay);
                if (!hay) continue;
                var (s, conocido) = _lista[i];
                bool foco = _zona == Zona.Lista && i == _indice;
                var fondo = conocido ? P.Ficha : P.Hueco;
                _cartas[i].color = foco ? P.Elegida : fondo;
                _bordesDeCarta[i].color = foco ? P.Oro : fondo;
                _iconosDeCarta[i].sprite = s.attackIcon;
                _iconosDeCarta[i].enabled = s.attackIcon != null;
                _iconosDeCarta[i].color = conocido ? Color.white : new Color(0.2f, 0.2f, 0.32f, 0.85f);
                _nombresDeCarta[i].text = conocido ? s.GetLocalizedName() : "¿?";
                _nombresDeCarta[i].color = conocido ? P.Texto : P.TextoApagado;
                int k = _equipados.IndexOf(s.spellId);
                _numerosDeCarta[i].text = k >= 0 ? (k + 1).ToString() : "";
                var marcaNumero = _numerosDeCarta[i].transform.parent.Find("Fondo número");
                if (k >= 0 && marcaNumero == null)
                {
                    var m = P.Caja("Fondo número", _numerosDeCarta[i].transform.parent, P.Oro, 17f);
                    P.ColocarDerecha(m.rectTransform, 8f, 8f, 34f, 34f);
                    m.transform.SetSiblingIndex(_numerosDeCarta[i].transform.GetSiblingIndex());
                    marcaNumero = m.transform;
                }
                if (marcaNumero != null) P.Activar(marcaNumero.gameObject, k >= 0);
            }

            for (int i = 0; i < MaxCombos; i++)
            {
                bool hay = i < _listaCombos.Count;
                P.Activar(_combos[i].gameObject, hay);
                if (!hay) continue;
                var (s, conocido) = _listaCombos[i];
                bool foco = _zona == Zona.Combos && i == _combo;
                _combos[i].color = foco ? P.Elegida : P.Ficha;
                _iconosDeCombo[i].sprite = s.attackIcon;
                _iconosDeCombo[i].enabled = s.attackIcon != null;
                _iconosDeCombo[i].color = conocido ? Color.white : new Color(0.2f, 0.2f, 0.32f, 0.85f);
                _nombresDeCombo[i].text = conocido ? s.GetLocalizedName() : "¿?";
                _nombresDeCombo[i].color = conocido ? P.Texto : P.TextoApagado;
                _secuencias[i].text = conocido ? ComboButtonGlyphs.SequenceSprites(s.comboSequence) : "<color=#776450>? ? ?</color>";
            }

            PintarDetalle();
            _menu.RefrescarPistas();
        }

        private void PintarDetalle()
        {
            var s = SpellResaltado(out bool conocido);
            bool hay = s != null;
            P.Activar(_marcoGrande.gameObject, hay);
            if (!hay)
            {
                _nombre.text = _zona == Zona.Ranuras ? "Ranura " + (_ranura + 1) + " vacía" : "";
                _pildoras.text = ""; _lore.text = "";
                P.Activar(_datos, false);
                _sellado.text = _zona == Zona.Ranuras ? "Pulsa A y elige qué hechizo llevar aquí." : "";
                P.Activar(_poner.gameObject, false);
                return;
            }
            var color = ColorDeElemento(s.element);
            _marcoGrande.color = conocido ? color : P.BordeApagado;
            _iconoGrande.sprite = s.attackIcon;
            _iconoGrande.enabled = s.attackIcon != null;
            _iconoGrande.color = conocido ? Color.white : new Color(0.2f, 0.2f, 0.32f, 0.85f);
            _nombre.text = conocido ? s.GetLocalizedName() : "Por descubrir";
            _pildoras.text = conocido
                ? "<color=" + P.Hex(P.SobrePapel(color)) + ">" + NombreDeElemento(s.element).ToUpperInvariant() + "</color>    " + NombreDeTipo(s).ToUpperInvariant()
                : "";
            _lore.text = conocido ? Lore(s) : "Una página sellada.";
            P.Activar(_datos, conocido);
            _sellado.text = conocido ? "" : (s.HasCombo ? "Alguien te lo enseñará más adelante." : "Hay una página del grimorio escondida en algún lugar del mundo.");
            if (conocido)
            {
                _mana.text = "<color=#2F5FB8>" + Mathf.RoundToInt(s.manaCost) + "</color>";
                bool apoyo = s.damage <= 0f;
                _fuerzaEtiqueta.text = apoyo ? "Apoyo (no hace daño)" : "Fuerza";
                int nivel = apoyo ? 0 : s.damage <= 10f ? 1 : s.damage <= 20f ? 2 : s.damage <= 40f ? 3 : 4;
                string puntos = "";
                for (int i = 1; i <= 4; i++) puntos += (i <= nivel ? "<color=#C98A00>" : "<color=#CDBB9C>") + "<size=170%>•</size></color>"; // «●» no está en la fuente (INC-1098)
                _fuerza.text = puntos;
                _lanza.text = s.HasCombo
                    ? P.Boton(InputGlyphNames.North) + " " + ComboButtonGlyphs.SequenceSprites(s.comboSequence)
                    : P.Boton(InputGlyphNames.West);
                _especial.text = GrimorioDelPersonaje.Efectos(s);
            }
            bool sePuedePoner = conocido && _zona == Zona.Lista && !s.HasCombo;
            P.Activar(_poner.gameObject, sePuedePoner);
            if (sePuedePoner) _ponerTexto.text = P.Boton(InputGlyphNames.South) + "  Poner en la ranura " + (_ranura + 1);
        }

        private static string Lore(MagicSpellSO s)
        {
            if (!string.IsNullOrEmpty(s.loreId) && LocalizationManager.Instance != null)
                return LocalizationManager.Instance.Get(s.loreId, s.lore);
            return s.lore;
        }

        private static string NombreDeElemento(MagicElement e) => e switch
        {
            MagicElement.Fire => "Fuego",
            MagicElement.Storm => "Viento",
            MagicElement.Light => "Luz",
            MagicElement.Mind => "Mente",
            MagicElement.Dark => "Oscuro",
            _ => ""
        };

        private static Color ColorDeElemento(MagicElement e) => e switch
        {
            MagicElement.Fire => new Color32(0xFF, 0x9A, 0x5C, 0xFF),
            MagicElement.Storm => new Color32(0x6F, 0xE0, 0xC8, 0xFF),
            MagicElement.Light => new Color32(0xF5, 0xD7, 0x6E, 0xFF),
            MagicElement.Mind => new Color32(0xC5, 0x9B, 0xFF, 0xFF),
            _ => new Color32(0x9A, 0x8C, 0xB8, 0xFF)
        };

        private static string NombreDeTipo(MagicSpellSO s)
        {
            if (s.damage <= 0f && s.kind == MagicKind.Zone) return "Apoyo · zona";
            return s.kind switch
            {
                MagicKind.Projectile => "Proyectil",
                MagicKind.Zone => "Zona",
                MagicKind.Special => "Especial",
                MagicKind.Levitation => "Levitación",
                MagicKind.Teleport => "Teletransporte",
                _ => ""
            };
        }

        // ─── Acciones ─────────────────────────────────────────────────────────

        private void Poner()
        {
            var s = SpellResaltado(out bool conocido);
            if (s == null || !conocido || !Equipable(s)) return;
            int antes = _equipados.IndexOf(s.spellId);
            if (antes == _ranura) return;
            bool alFinal = _ranura >= _equipados.Count;
            if (antes >= 0 && !alFinal) (_equipados[antes], _equipados[_ranura]) = (_equipados[_ranura], _equipados[antes]);
            else if (antes >= 0) { _equipados.RemoveAt(antes); _equipados.Add(s.spellId); }
            else if (!alFinal) _equipados[_ranura] = s.spellId;
            else _equipados.Add(s.spellId);
            Guardar();
            EntradaDelMenu.Sonido("UI_Submit");
            if (alFinal) _ranura = Mathf.Min(_equipados.Count, Ranuras - 1);
            Pintar();
        }

        private void Quitar()
        {
            int k = -1;
            if (_zona == Zona.Ranuras && _ranura < _equipados.Count) k = _ranura;
            else if (_zona == Zona.Lista && _indice < _lista.Count) k = _equipados.IndexOf(_lista[_indice].s.spellId);
            if (k < 0) return;
            _equipados.RemoveAt(k);
            Guardar();
            EntradaDelMenu.Sonido("UI_Cancel");
            Pintar();
        }

        // ─── Entrada ──────────────────────────────────────────────────────────

        public void Tick()
        {
            int h = EntradaDelMenu.Horizontal(), v = EntradaDelMenu.Vertical();
            if (h != 0 || v != 0) Mover(h, v);
            if (EntradaDelMenu.Aceptar)
            {
                if (_zona == Zona.Ranuras) { _zona = Zona.Lista; EntradaDelMenu.Sonido("UI_Navigate"); Pintar(); }
                else if (_zona == Zona.Lista) Poner();
            }
            if (EntradaDelMenu.Quitar) Quitar();
        }

        private void Mover(int h, int v)
        {
            var antes = (_zona, _ranura, _indice, _combo);
            switch (_zona)
            {
                case Zona.Ranuras:
                    if (v > 0 && _listaCombos.Count > 0) { _zona = Zona.Combos; _combo = 0; }
                    else if (h > 0 && _ranura == Ranuras - 1 && _lista.Count > 0) _zona = Zona.Lista;
                    else _ranura = Mathf.Clamp(_ranura + h, 0, Ranuras - 1);
                    break;
                case Zona.Lista:
                    if (h < 0 && _indice % ColumnasDeLista == 0) _zona = Zona.Ranuras;
                    else
                    {
                        int n = _indice + h + v * ColumnasDeLista;
                        if (h != 0 && n / ColumnasDeLista != _indice / ColumnasDeLista) n = _indice;
                        _indice = Mathf.Clamp(n, 0, Mathf.Max(0, _lista.Count - 1));
                    }
                    break;
                case Zona.Combos:
                    if (v < 0 && _combo == 0) _zona = Zona.Ranuras;
                    else if (h > 0 && _lista.Count > 0) _zona = Zona.Lista;
                    else _combo = Mathf.Clamp(_combo + v, 0, Mathf.Max(0, _listaCombos.Count - 1));
                    break;
            }
            if (antes != (_zona, _ranura, _indice, _combo)) { EntradaDelMenu.Sonido("UI_Navigate"); Pintar(); }
        }

        private void AlClicRanura(int i)
        {
            if (_zona == Zona.Ranuras && _ranura == i) { _zona = Zona.Lista; Pintar(); return; }
            _zona = Zona.Ranuras; _ranura = i;
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        private void AlClicCarta(int i)
        {
            if (i >= _lista.Count) return;
            if (_zona == Zona.Lista && _indice == i) { Poner(); return; }
            _zona = Zona.Lista; _indice = i;
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        private void AlClicCombo(int i)
        {
            if (i >= _listaCombos.Count) return;
            _zona = Zona.Combos; _combo = i;
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        public bool Cancelar()
        {
            if (_zona == Zona.Lista) return false;
            _zona = Zona.Lista;
            Pintar();
            return true;
        }
    }
}
