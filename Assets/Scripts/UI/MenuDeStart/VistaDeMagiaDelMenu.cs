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
    /// Pestaña Magia del menú de Start con los sets fijos (decidido por Raúl el 20 nov 2026): cada
    /// personaje tiene 4 rápidos (X, LB pasa al siguiente) y 4 combos (Y), siempre puestos, y aparte
    /// un especial que se aprende con una misión. Ya no hay lista para elegir ni huecos vacíos.
    /// <list type="bullet">
    /// <item>Izquierda: los 4 rápidos en su orden, los 4 combos con su combinación completa (la Y
    /// que abre el círculo y lo que se teclea, igual que en el grimorio) y el especial. Lo que aún
    /// no se sabe sale sellado.</item>
    /// <item>Arriba a la derecha: el hechizo resaltado en grande (elemento, maná, frase y lo que
    /// hace, con el icono del botón).</item>
    /// <item>Abajo a la derecha: el escudo (B) del personaje con su color y la regla de los
    /// escudos del mismo color, y los cinco elementos con su color.</item>
    /// </list>
    /// </summary>
    public sealed class VistaDeMagiaDelMenu : IVistaDelMenuDeStart
    {
        private enum Zona { Rapidos, Combos, Especial }
        private const int Rapidos = MagicCaster.BasicSlotCount, Combos = 4;

        private MenuDeStart _menu;
        private readonly List<P.Casilla> _cartas = new(), _filas = new();
        private readonly List<Image> _iconosDeCarta = new(), _chipsDeCarta = new();
        private readonly List<TextMeshProUGUI> _nombresDeCarta = new(), _elementosDeCarta = new(), _numerosDeCarta = new();
        private readonly List<TextMeshProUGUI> _nombresDeCombo = new(), _secuencias = new();
        private P.Casilla _filaEspecial;
        private Image _iconoEspecial;
        private TextMeshProUGUI _nombreEspecial, _notaEspecial;
        private TextMeshProUGUI _nombre, _elemento, _mana, _lore, _especial;
        private Image _iconoGrande, _halo, _chipElemento;
        private TextMeshProUGUI _escudo, _escudoNota;
        private Image _chipEscudo;
        private GameObject _hojaDelEscudo, _hojaDeHabilidades;
        private readonly List<TextMeshProUGUI> _nombresDeHabilidad = new(), _frasesDeHabilidad = new();
        private TextMeshProUGUI _sinHabilidades;
        // Abajo a la derecha se ve la hoja de habilidades en vez de la del escudo (X cambia).
        private bool _verHabilidades;
        private static readonly AbilityKey[] s_habilidades =
            { AbilityKey.Swim, AbilityKey.Jump, AbilityKey.Sprint, AbilityKey.Fly, AbilityKey.Magic, AbilityKey.Shield };

        private readonly List<(MagicSpellSO s, bool sabe)> _rapidos = new(), _combos = new();
        private (MagicSpellSO s, bool sabe) _especialDelSet;
        private Zona _zona = Zona.Rapidos;
        private int _indice;

        public string Titulo => "Magia";
        public bool UsaPersonaje => true;
        public bool UsaX => true;
        public bool UsaGatillos => true;

        /// El hechizo resaltado (el Grimorio se abre por él).
        public SpellId Resaltado { get { var s = SpellResaltado(out _); return s != null ? s.spellId : SpellId.None; } }

        public string Pistas => P.Boton(InputGlyphNames.MenuX) + (_verHabilidades ? " Escudo" : " Habilidades")
                                + (GrimorioDelPersonaje.TieneGrimorio ? "     " + P.Boton(InputGlyphNames.Select) + " Grimorio" : "");

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            MontarSet(cuerpo);
            MontarDetalle(cuerpo);
            MontarEscudo(cuerpo);
            MontarHabilidades(cuerpo);
        }

        /// Hoja «Habilidades» (INC-1397): lo que el personaje ya sabe hacer (nadar, volar…), con su
        /// nombre y una frase, en el sitio de la hoja del escudo. Dos columnas de tres.
        private void MontarHabilidades(RectTransform raiz)
        {
            var cuerpo = P.Vacio("Hoja de habilidades", raiz);
            P.Rellenar(cuerpo);
            _hojaDeHabilidades = cuerpo.gameObject;
            P.Hoja("Habilidades", cuerpo, 910f, 630f, 902f, 330f);
            var t = P.Titulo("Título habilidades", cuerpo, "Habilidades", 30f, PaletaUI.TintaDeTexto, TextAlignmentOptions.MidlineLeft);
            PiezasDeUI.ColocarArribaIzquierda(t.rectTransform, 946f, 648f, 400f, 42f);
            for (int i = 0; i < s_habilidades.Length; i++)
            {
                float x = 946f + (i % 2) * 424f, y = 702f + (i / 2) * 82f;
                var n = P.Titulo("Habilidad " + i, cuerpo, "", 24f, PaletaUI.TintaDeTexto, TextAlignmentOptions.TopLeft);
                PiezasDeUI.ColocarArribaIzquierda(n.rectTransform, x, y, 404f, 32f);
                var f = P.TxtConIconos("Frase " + i, cuerpo, "", 19f, P.TextoMaqueta, TextAlignmentOptions.TopLeft);
                f.textWrappingMode = TextWrappingModes.Normal;
                PiezasDeUI.ColocarArribaIzquierda(f.rectTransform, x, y + 32f, 404f, 46f);
                f.enableAutoSizing = true; f.fontSizeMin = 15f; f.fontSizeMax = 19f;
                _nombresDeHabilidad.Add(n); _frasesDeHabilidad.Add(f);
            }
            _sinHabilidades = P.Txt("Sin habilidades", cuerpo, "Todavía no ha aprendido ninguna.", 22f, P.TextoMaqueta, false, TextAlignmentOptions.TopLeft, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_sinHabilidades.rectTransform, 946f, 706f, 836f, 40f);
            P.Activar(_hojaDeHabilidades, false);
        }

        // Se pinta al abrir el menú, al cambiar de personaje y al cambiar de hoja; nunca por fotograma.
        private void PintarHabilidades()
        {
            P.Activar(_hojaDelEscudo, !_verHabilidades);
            P.Activar(_hojaDeHabilidades, _verHabilidades);
            if (!_verHabilidades) return;
            var habilidades = HabilidadesDelPersonaje.De(Partida.Preset, Personaje);
            int k = 0;
            for (int i = 0; i < s_habilidades.Length; i++)
            {
                if (!habilidades.Tiene(s_habilidades[i])) continue;
                var info = AbilityPresentationKeyLookup.Resolve(s_habilidades[i], null);
                _nombresDeHabilidad[k].text = info.title;
                _frasesDeHabilidad[k].text = info.description;
                k++;
            }
            for (int i = 0; i < _nombresDeHabilidad.Count; i++)
            {
                P.Activar(_nombresDeHabilidad[i].gameObject, i < k);
                P.Activar(_frasesDeHabilidad[i].gameObject, i < k);
            }
            P.Activar(_sinHabilidades.gameObject, k == 0);
        }

        private void MontarSet(RectTransform cuerpo)
        {
            P.Hoja("Hechizos", cuerpo, 54f, 160f, 820f, 800f);
            P.Etiqueta("Título", cuerpo, "Rápidos", 84f, 136f, 240f, 56f, 30f);
            var notaX = P.TxtConIconos("Nota rápidos", cuerpo, P.Boton(InputGlyphNames.West) + " lanza · " + P.Boton(InputGlyphNames.ShoulderLeft) + " pasa al siguiente",
                                       20f, P.TextoMaqueta, TextAlignmentOptions.MidlineRight);
            PiezasDeUI.ColocarArribaIzquierda(notaX.rectTransform, 380f, 172f, 466f, 36f);
            for (int i = 0; i < Rapidos; i++)
            {
                int indice = i;
                var carta = P.Casilla.Crear("Rápido " + (i + 1), cuerpo, 20f, 36f, () => AlClic(Zona.Rapidos, indice), () => AlClic(Zona.Rapidos, indice, true));
                PiezasDeUI.ColocarArribaIzquierda(carta.raiz, 82f + i * 196f, 216f, 184f, 196f);
                var icono = PiezasDeUI.Imagen("Icono", carta.contenido, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                PiezasDeUI.ColocarArribaIzquierda(icono.rectTransform, 56f, 14f, 72f, 72f);
                var n = P.Titulo("Nombre", carta.contenido, "", 24f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center);
                PiezasDeUI.ColocarArribaIzquierda(n.rectTransform, 6f, 92f, 172f, 36f);
                n.enableAutoSizing = true; n.fontSizeMin = 18f; n.fontSizeMax = 24f;
                var chip = P.Pastilla("Elemento", carta.contenido, Color.white, 36f, 140f, 112f, 28f, 16f, out var relleno);
                var num = P.Titulo("Orden", carta.contenido, (i + 1).ToString(), 22f, P.TextoMaqueta, TextAlignmentOptions.Center);
                PiezasDeUI.ColocarArribaIzquierda(num.rectTransform, 144f, 6f, 34f, 34f);
                _cartas.Add(carta); _iconosDeCarta.Add(icono); _nombresDeCarta.Add(n); _elementosDeCarta.Add(chip);
                _chipsDeCarta.Add(relleno); _numerosDeCarta.Add(num);
            }

            var tc = P.Titulo("Título combos", cuerpo, "Combos", 30f, PaletaUI.TintaDeTexto, TextAlignmentOptions.TopLeft);
            PiezasDeUI.ColocarArribaIzquierda(tc.rectTransform, 84f, 432f, 300f, 42f);
            var notaY = P.TxtConIconos("Nota combos", cuerpo, P.Boton(InputGlyphNames.North) + " abre el círculo y se teclea el resto",
                                       20f, P.TextoMaqueta, TextAlignmentOptions.MidlineRight);
            PiezasDeUI.ColocarArribaIzquierda(notaY.rectTransform, 380f, 436f, 466f, 36f);
            for (int i = 0; i < Combos; i++)
            {
                int indice = i;
                var f = P.Casilla.Crear("Combo " + (i + 1), cuerpo, 18f, 28f, () => AlClic(Zona.Combos, indice), () => AlClic(Zona.Combos, indice, true));
                PiezasDeUI.ColocarArribaIzquierda(f.raiz, 82f, 484f + i * 74f, 764f, 64f);
                f.carta.Caida(0f);
                // Solo lo que se teclea (hasta cuatro botones): el botón que abre el círculo ya lo dice la nota de arriba.
                var sq = P.TxtConIconos("Secuencia", f.contenido, "", 30f, P.TextoMaqueta, TextAlignmentOptions.MidlineLeft);
                PiezasDeUI.ColocarArribaIzquierda(sq.rectTransform, 16f, 0f, 330f, 64f);
                var n = P.Titulo("Nombre", f.contenido, "", 26f, PaletaUI.TintaDeTexto, TextAlignmentOptions.MidlineLeft);
                PiezasDeUI.ColocarArribaIzquierda(n.rectTransform, 360f, 0f, 390f, 64f);
                n.enableAutoSizing = true; n.fontSizeMin = 20f; n.fontSizeMax = 26f;
                _filas.Add(f); _nombresDeCombo.Add(n); _secuencias.Add(sq);
            }

            var te = P.Titulo("Título especial", cuerpo, "Especial", 30f, PaletaUI.TintaDeTexto, TextAlignmentOptions.TopLeft);
            PiezasDeUI.ColocarArribaIzquierda(te.rectTransform, 84f, 788f, 300f, 42f);
            _filaEspecial = P.Casilla.Crear("Especial", cuerpo, 18f, 28f, () => AlClic(Zona.Especial, 0), () => AlClic(Zona.Especial, 0, true));
            PiezasDeUI.ColocarArribaIzquierda(_filaEspecial.raiz, 82f, 836f, 764f, 96f);
            _filaEspecial.carta.Caida(0f);
            _iconoEspecial = PiezasDeUI.Imagen("Icono", _filaEspecial.contenido, null, Color.white);
            _iconoEspecial.preserveAspect = true; _iconoEspecial.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_iconoEspecial.rectTransform, 14f, 12f, 72f, 72f);
            _nombreEspecial = P.Titulo("Nombre", _filaEspecial.contenido, "", 28f, PaletaUI.TintaDeTexto, TextAlignmentOptions.MidlineLeft);
            PiezasDeUI.ColocarArribaIzquierda(_nombreEspecial.rectTransform, 100f, 0f, 300f, 96f);
            _nombreEspecial.enableAutoSizing = true; _nombreEspecial.fontSizeMin = 20f; _nombreEspecial.fontSizeMax = 28f;
            _notaEspecial = P.TxtConIconos("Nota", _filaEspecial.contenido, "", 22f, P.TextoMaqueta, TextAlignmentOptions.MidlineRight);
            PiezasDeUI.ColocarArribaIzquierda(_notaEspecial.rectTransform, 400f, 0f, 350f, 96f);
        }

        private void MontarDetalle(RectTransform cuerpo)
        {
            P.Hoja("Detalle", cuerpo, 910f, 160f, 902f, 440f);
            var c = P.Vacio("Contenido", cuerpo);
            P.Rellenar(c);
            _halo = PiezasDeUI.Imagen("Halo", c, PiezasDeUI.Resplandor, Color.white);
            _halo.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_halo.rectTransform, 950f, 210f, 260f, 260f);
            _iconoGrande = PiezasDeUI.Imagen("Icono", c, null, Color.white);
            _iconoGrande.preserveAspect = true; _iconoGrande.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_iconoGrande.rectTransform, 980f, 240f, 200f, 200f);
            PiezasDeUI.EstrellaEn("Chispa", c, 959f, 231f, 30f, 10f);
            PiezasDeUI.EstrellaEn("Chispa", c, 1184f, 292f, 24f, 10f);
            _nombre = P.Titulo("Nombre", c, "", 52f, PaletaUI.TintaDeTexto, TextAlignmentOptions.TopLeft);
            PiezasDeUI.ColocarArribaIzquierda(_nombre.rectTransform, 1240f, 202f, 540f, 66f);
            _nombre.enableAutoSizing = true; _nombre.fontSizeMin = 32f; _nombre.fontSizeMax = 52f;
            _elemento = P.Pastilla("Elemento", c, Color.white, 1240f, 276f, 120f, 36f, 20f, out _chipElemento);
            _mana = P.Pastilla("Maná", c, new Color32(0x9E, 0xD0, 0xFF, 0xFF), 1374f, 276f, 150f, 36f, 20f, out _);
            _lore = P.Txt("Frase", c, "", 25f, PaletaUI.TintaDeTexto, false, TextAlignmentOptions.TopLeft, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_lore.rectTransform, 1240f, 336f, 540f, 150f);
            _lore.enableAutoSizing = true; _lore.fontSizeMin = 19f; _lore.fontSizeMax = 25f;
            // Con iconos: «Se lanza con» enseña el botón, no un «?».
            _especial = P.TxtConIconos("Especial", c, "", 20f, P.TextoMaqueta, TextAlignmentOptions.TopLeft);
            _especial.textWrappingMode = TextWrappingModes.Normal;
            PiezasDeUI.ColocarArribaIzquierda(_especial.rectTransform, 950f, 500f, 830f, 84f);
            _especial.enableAutoSizing = true; _especial.fontSizeMin = 16f; _especial.fontSizeMax = 20f;
        }

        private void MontarEscudo(RectTransform raiz)
        {
            var cuerpo = P.Vacio("Hoja del escudo", raiz);
            P.Rellenar(cuerpo);
            _hojaDelEscudo = cuerpo.gameObject;
            P.Hoja("Escudo", cuerpo, 910f, 630f, 902f, 330f);
            var t = P.TxtConIconos("Título escudo", cuerpo, "Escudo  " + P.Boton(InputGlyphNames.East), 30f, PaletaUI.TintaDeTexto, TextAlignmentOptions.MidlineLeft);
            if (P.FuenteTitulos != null) t.font = P.FuenteTitulos;
            PiezasDeUI.ColocarArribaIzquierda(t.rectTransform, 946f, 648f, 400f, 42f);
            _escudo = P.Pastilla("Color del escudo", cuerpo, Color.white, 946f, 704f, 250f, 40f, 22f, out _chipEscudo);
            _escudoNota = P.Txt("Cómo sale", cuerpo, "", 22f, PaletaUI.TintaDeTexto, false, TextAlignmentOptions.MidlineLeft, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_escudoNota.rectTransform, 1214f, 696f, 570f, 56f);
            _escudoNota.enableAutoSizing = true; _escudoNota.fontSizeMin = 17f; _escudoNota.fontSizeMax = 22f;
            var regla = P.Txt("Regla", cuerpo,
                "Un escudo solo lo rompe la magia de su mismo color, y quien lo pierde se queda aturdido 2 s. " +
                "Lo de otro color lo para; los golpes y las rocas, siempre. ¿Quién de tu grupo puede parar lo que llega? " +
                "Cambia de personaje o de rápido.",
                20f, P.TextoMaqueta, false, TextAlignmentOptions.TopLeft, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(regla.rectTransform, 946f, 764f, 836f, 96f);
            regla.enableAutoSizing = true; regla.fontSizeMin = 16f; regla.fontSizeMax = 20f;
            var elementos = new[] { MagicElement.Fire, MagicElement.Storm, MagicElement.Light, MagicElement.Mind, MagicElement.Dark };
            for (int i = 0; i < elementos.Length; i++)
            {
                var e = elementos[i];
                P.Pastilla("Elemento " + i, cuerpo, ColorDeElemento(e), 946f + i * 168f, 884f, 156f, 36f, 19f, out _).text = NombreDeElemento(e);
            }
        }

        // ─── Datos ────────────────────────────────────────────────────────────

        private Slot Personaje => _menu.Personaje;

        private void Leer()
        {
            _rapidos.Clear();
            _combos.Clear();
            var setRapidos = GrimorioDelPersonaje.RapidosDelSet(Personaje);
            if (setRapidos.Count == 0) setRapidos = GrimorioDelPersonaje.BasicosEquipados(Personaje);
            foreach (var s in setRapidos)
                if (_rapidos.Count < Rapidos) _rapidos.Add((s, GrimorioDelPersonaje.LoSabe(Personaje, s)));
            var setCombos = GrimorioDelPersonaje.CombosDelSet(Personaje);
            if (setCombos.Count == 0)
                foreach (var s in GrimorioDelPersonaje.CombosDisponibles(Personaje))
                    if (!s.especialConMision) setCombos.Add(s);
            foreach (var s in setCombos)
                if (_combos.Count < Combos) _combos.Add((s, GrimorioDelPersonaje.LoSabe(Personaje, s)));
            var especial = GrimorioDelPersonaje.EspecialDe(Personaje);
            _especialDelSet = (especial, especial != null && GrimorioDelPersonaje.LoSabe(Personaje, especial));
        }

        private MagicSpellSO SpellResaltado(out bool sabe)
        {
            sabe = false;
            switch (_zona)
            {
                case Zona.Rapidos:
                    if (_indice < _rapidos.Count) { sabe = _rapidos[_indice].sabe; return _rapidos[_indice].s; }
                    return null;
                case Zona.Combos:
                    if (_indice < _combos.Count) { sabe = _combos[_indice].sabe; return _combos[_indice].s; }
                    return null;
                default:
                    sabe = _especialDelSet.sabe;
                    return _especialDelSet.s;
            }
        }

        /// La combinación completa de un combo: la Y que abre el círculo y lo que se teclea. Igual
        /// que en el grimorio.
        public static string Combinacion(MagicSpellSO s) =>
            ComboButtonGlyphs.SpriteTag(ComboButton.Y) + " <color=#A08C74>›</color> " + ComboButtonGlyphs.SequenceSprites(s.comboSequence);

        // ─── Mostrar ──────────────────────────────────────────────────────────

        public void Mostrar()
        {
            Leer();
            var nuevo = GrimorioDelPersonaje.UltimoAprendido;
            if (nuevo != SpellId.None)
            {
                int k = _rapidos.FindIndex(x => x.s != null && x.s.spellId == nuevo);
                if (k >= 0) { _zona = Zona.Rapidos; _indice = k; }
                else if ((k = _combos.FindIndex(x => x.s != null && x.s.spellId == nuevo)) >= 0) { _zona = Zona.Combos; _indice = k; }
                else if (_especialDelSet.s != null && _especialDelSet.s.spellId == nuevo) { _zona = Zona.Especial; _indice = 0; }
            }
            AjustarIndice();
            Pintar();
            PintarHabilidades();
        }

        public void Ocultar() { }

        public void AlCambiarPersonaje()
        {
            _indice = 0; _zona = Zona.Rapidos;
            Mostrar();
        }

        private void AjustarIndice()
        {
            int n = _zona == Zona.Rapidos ? _rapidos.Count : _zona == Zona.Combos ? _combos.Count : (_especialDelSet.s != null ? 1 : 0);
            if (n == 0) { _zona = Zona.Rapidos; _indice = 0; return; }
            _indice = Mathf.Clamp(_indice, 0, n - 1);
        }

        private void Pintar()
        {
            for (int i = 0; i < Rapidos; i++)
            {
                bool hay = i < _rapidos.Count;
                var carta = _cartas[i];
                P.Activar(carta.raiz.gameObject, hay);
                if (!hay) continue;
                var (s, sabe) = _rapidos[i];
                bool foco = _zona == Zona.Rapidos && i == _indice;
                carta.Poner(foco ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
                _iconosDeCarta[i].sprite = s.attackIcon;
                _iconosDeCarta[i].enabled = s.attackIcon != null;
                _iconosDeCarta[i].color = sabe ? Color.white : new Color(0.2f, 0.2f, 0.32f, 0.85f);
                _nombresDeCarta[i].text = sabe ? s.GetLocalizedName() : "¿?";
                _nombresDeCarta[i].color = sabe ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
                _elementosDeCarta[i].text = NombreDeElemento(s.element);
                _chipsDeCarta[i].color = ColorDeElemento(s.element);
            }

            for (int i = 0; i < Combos; i++)
            {
                bool hay = i < _combos.Count;
                P.Activar(_filas[i].raiz.gameObject, hay);
                if (!hay) continue;
                var (s, sabe) = _combos[i];
                bool foco = _zona == Zona.Combos && i == _indice;
                _filas[i].Poner(foco ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
                _nombresDeCombo[i].text = sabe ? s.GetLocalizedName() : "¿?";
                _nombresDeCombo[i].color = sabe ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
                _secuencias[i].text = sabe ? ComboButtonGlyphs.SequenceSprites(s.comboSequence) : "<color=#A08C74>? ? ?</color>";
            }

            var (esp, sabeEsp) = _especialDelSet;
            P.Activar(_filaEspecial.raiz.gameObject, true);
            _filaEspecial.Poner(_zona == Zona.Especial ? P.EstadoDeCasilla.Elegida : esp != null ? P.EstadoDeCasilla.Llena : P.EstadoDeCasilla.Vacia);
            _iconoEspecial.enabled = esp != null && esp.attackIcon != null;
            _iconoEspecial.sprite = esp != null ? esp.attackIcon : null;
            _iconoEspecial.color = sabeEsp ? Color.white : new Color(0.2f, 0.2f, 0.32f, 0.85f);
            _nombreEspecial.text = esp == null ? "Por descubrir" : sabeEsp ? esp.GetLocalizedName() : "¿?";
            _nombreEspecial.color = sabeEsp ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
            _notaEspecial.text = sabeEsp && esp.HasCombo ? Combinacion(esp) : "Se aprende con una misión";

            PintarEscudo();
            PintarDetalle();
            _menu.RefrescarPistas();
        }

        /// El color del escudo del personaje: Will, siempre Luz; Estela y Liam, el del rápido que
        /// llevan elegido (el de su cuerpo; si no, el primero).
        private void PintarEscudo()
        {
            MagicElement e = MagicElement.Light;
            string nota = "Siempre de Luz.";
            if (Personaje != Slot.Will)
            {
                MagicSpellSO activo = null;
                var cuerpo = ControlDelJugador.PilotoDe(Personaje);
                var caster = cuerpo != null ? cuerpo.GetComponent<MagicCaster>() : null;
                if (caster != null) activo = caster.ActiveBasic;
                if (activo == null && _rapidos.Count > 0) activo = _rapidos[0].s;
                if (activo != null) e = activo.element;
                nota = "Del color del rápido elegido" + (activo != null ? " (ahora " + activo.GetLocalizedName() + ")" : "") + ". Cámbialo con LB.";
            }
            _escudo.text = "Escudo de " + NombreDeElemento(e);
            _chipEscudo.color = ColorDeElemento(e);
            _escudoNota.text = nota;
        }

        private void PintarDetalle()
        {
            var s = SpellResaltado(out bool sabe);
            bool hay = s != null;
            P.Activar(_iconoGrande.gameObject, hay);
            P.Activar(_halo.gameObject, hay);
            if (!hay)
            {
                _nombre.text = _zona == Zona.Especial ? "Por descubrir" : "";
                P.Activar(_elemento.transform.parent.gameObject, false);
                P.Activar(_mana.transform.parent.gameObject, false);
                _lore.text = _zona == Zona.Especial ? "Un hechizo que se aprende con una misión." : "";
                _especial.text = "";
                return;
            }
            var color = ColorDeElemento(s.element);
            _halo.color = new Color(color.r, color.g, color.b, 0.7f);
            _iconoGrande.sprite = s.attackIcon;
            _iconoGrande.enabled = s.attackIcon != null;
            _iconoGrande.color = sabe ? Color.white : new Color(0.2f, 0.2f, 0.32f, 0.85f);
            _nombre.text = sabe ? s.GetLocalizedName() : "Por descubrir";
            P.Activar(_elemento.transform.parent.gameObject, true);
            _elemento.text = NombreDeElemento(s.element);
            _chipElemento.color = color;
            P.Activar(_mana.transform.parent.gameObject, sabe);
            if (!sabe)
            {
                _lore.text = "Una página sellada.";
                _especial.text = s.especialConMision ? "Se aprende con una misión." : "Alguien te lo enseñará más adelante.";
                return;
            }
            _mana.text = "Maná " + Mathf.RoundToInt(s.manaCost);
            _lore.text = Lore(s);
            bool apoyo = s.damage <= 0f;
            int nivel = apoyo ? 0 : s.damage <= 10f ? 1 : s.damage <= 20f ? 2 : s.damage <= 40f ? 3 : 4;
            string lanza = s.HasCombo ? Combinacion(s)
                : s.kind == MagicKind.Levitation ? P.Boton(InputGlyphNames.West) + " mantenido (levanta) y suelta (lanza)"
                : P.Boton(InputGlyphNames.West);
            string efectos = GrimorioDelPersonaje.Efectos(s);
            _especial.text = NombreDeTipo(s) + " · " + (apoyo ? "Apoyo, no hace daño" : "Fuerza " + nivel + " de 4") + " · Se lanza con " + lanza
                             + (string.IsNullOrEmpty(efectos) ? "" : "\n" + efectos);
        }

        private static string Lore(MagicSpellSO s)
        {
            if (!string.IsNullOrEmpty(s.loreId) && LocalizationManager.Instance != null)
                return LocalizationManager.Instance.Get(s.loreId, s.lore);
            return s.lore;
        }

        public static string NombreDeElemento(MagicElement e) => e switch
        {
            MagicElement.Fire => "Fuego",
            MagicElement.Storm => "Viento",
            MagicElement.Light => "Luz",
            MagicElement.Mind => "Mente",
            MagicElement.Dark => "Oscuro",
            _ => ""
        };

        public static Color ColorDeElemento(MagicElement e) => e switch
        {
            MagicElement.Fire => new Color32(0xFF, 0xB2, 0x7A, 0xFF),
            MagicElement.Storm => new Color32(0xB8, 0xF0, 0xD8, 0xFF),
            MagicElement.Light => new Color32(0xFF, 0xF0, 0xA8, 0xFF),
            MagicElement.Mind => new Color32(0xD5, 0xC8, 0xFF, 0xFF),
            _ => new Color32(0xB7, 0x86, 0xB0, 0xFF)
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
                MagicKind.Invisibility => "Invisibilidad",
                _ => ""
            };
        }

        // ─── Entrada ──────────────────────────────────────────────────────────

        public void Tick()
        {
            int h = EntradaDelMenu.Horizontal(), v = EntradaDelMenu.Vertical();
            if (h != 0 || v != 0) Mover(h, v);
            if (EntradaDelMenu.Quitar)
            {
                _verHabilidades = !_verHabilidades;
                EntradaDelMenu.Sonido("UI_Navigate");
                PintarHabilidades();
                _menu.RefrescarPistas();
            }
        }

        private void Mover(int h, int v)
        {
            var antes = (_zona, _indice);
            switch (_zona)
            {
                case Zona.Rapidos:
                    if (v > 0 && _combos.Count > 0) { _zona = Zona.Combos; _indice = 0; }
                    else if (v > 0) _zona = Zona.Especial;
                    else _indice = Mathf.Clamp(_indice + h, 0, Mathf.Max(0, _rapidos.Count - 1));
                    break;
                case Zona.Combos:
                    if (v < 0 && _indice == 0) { _zona = Zona.Rapidos; _indice = 0; }
                    else if (v > 0 && _indice >= _combos.Count - 1) { _zona = Zona.Especial; _indice = 0; }
                    else _indice = Mathf.Clamp(_indice + v, 0, Mathf.Max(0, _combos.Count - 1));
                    break;
                case Zona.Especial:
                    if (v < 0)
                    {
                        if (_combos.Count > 0) { _zona = Zona.Combos; _indice = _combos.Count - 1; }
                        else { _zona = Zona.Rapidos; _indice = 0; }
                    }
                    break;
            }
            if (antes != (_zona, _indice)) { EntradaDelMenu.Sonido("UI_Navigate"); Pintar(); }
        }

        private void AlClic(Zona zona, int i, bool soloPasar = false)
        {
            if (_zona == zona && _indice == i) return;
            _zona = zona; _indice = i;
            if (!soloPasar) EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        public bool Cancelar() => false;
    }
}
