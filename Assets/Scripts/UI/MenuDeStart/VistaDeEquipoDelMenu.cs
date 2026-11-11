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
    /// Pestaña Equipo del menú de Start: a la izquierda lo que lleva el personaje elegido (ropa,
    /// capa, accesorio…), en el centro el personaje de cuerpo entero (copia del
    /// <see cref="EstudioDeRetratos"/>; gira con el stick derecho) y a la derecha lo que hay en el
    /// armario para ese hueco, con su descripción. Al pasar por una prenda se la prueba la copia;
    /// A se la pone de verdad, X la quita.
    /// La ropa se pone en el cuerpo de ese personaje: si va al mando, se guarda en la partida como
    /// siempre (<see cref="PlayerPresetService.SnapshotAppearanceToPreset"/>); si no, en el
    /// registro de apariencias del grupo.
    /// </summary>
    public sealed class VistaDeEquipoDelMenu : IVistaDelMenuDeStart
    {
        private enum Zona { Huecos, Armario }
        private static readonly PartCategory[] Categorias = { PartCategory.Body, PartCategory.Cloak, PartCategory.Hat, PartCategory.Accessory };
        private static readonly string[] NombresDeCategoria = { "Ropa", "Capa", "Sombrero", "Accesorio" };
        private const int FilasDeArmario = 7;

        private static readonly int IdTexturaSobreFondo = Shader.PropertyToID("_TexturaSobreFondo");
        private static readonly int IdUsarTexturaSobreFondo = Shader.PropertyToID("_UsarTexturaSobreFondo");

        private MenuDeStart _menu;
        private readonly List<Image> _huecos = new(), _bordesDeHueco = new(), _iconosDeHueco = new();
        private readonly List<TextMeshProUGUI> _categoriasDeHueco = new(), _prendasDeHueco = new();
        private readonly List<Image> _filas = new(), _iconosDeFila = new();
        private readonly List<TextMeshProUGUI> _nombresDeFila = new();
        private readonly List<GameObject> _puestas = new();
        private TextMeshProUGUI _tituloHuecos, _tituloArmario, _descripcion, _rasgo, _sinFigura;
        private RawImage _figura;
        private Material _material;

        private readonly List<int> _visibles = new();               // índices de Categorias que se enseñan
        private readonly List<(string pieza, string nombre, string descripcion, Sprite icono)> _opciones = new();
        private Zona _zona;
        private int _hueco, _opcion, _primera;
        private Slot _mirando = (Slot)(-1);

        public string Titulo => "Equipo";
        public bool UsaPersonaje => true;
        public bool UsaX => true;

        public string Pistas
        {
            get
            {
                if (_zona == Zona.Huecos) return P.Boton(InputGlyphNames.South) + " Cambiar     " + P.Boton(InputGlyphNames.Stick) + " Girar";
                string p = P.Boton(InputGlyphNames.South) + " Ponerse     ";
                if (CategoriaActual != PartCategory.Body) p += P.Boton(InputGlyphNames.West) + " Quitar     ";
                return p + P.Boton(InputGlyphNames.Stick) + " Girar";
            }
        }

        private PartCategory CategoriaActual => _visibles.Count > 0 ? Categorias[_visibles[Mathf.Clamp(_hueco, 0, _visibles.Count - 1)]] : PartCategory.Body;

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            var izq = P.CajaConBorde("Lo que lleva", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)izq.transform.parent, 0f, 0f, 495f, 886f);
            var c = izq.transform;
            _tituloHuecos = P.Txt("Título", c, "", 22f, P.TextoApagado, true);
            P.Colocar(_tituloHuecos.rectTransform, 28f, 26f, 440f, 34f);
            for (int i = 0; i < Categorias.Length; i++)
            {
                int indice = i;
                var h = P.CajaConBorde("Hueco " + NombresDeCategoria[i], c, P.Ficha, P.Ficha, 4f, 22f);
                var borde = h.transform.parent.GetComponent<Image>();
                P.Colocar(borde.rectTransform, 24f, 76f + i * 124f, 441f, 112f);
                P.Clicable(borde, () => AlClicHueco(indice));
                var marco = P.Caja("Marco", h.transform, P.FichaOscura, 18f);
                P.Colocar(marco.rectTransform, 12f, 12f, 84f, 84f);
                var icono = PiezasDeUI.Imagen("Icono", marco.transform, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                P.Rellenar(icono.rectTransform, 8f);
                var cat = P.Txt("Categoría", h.transform, NombresDeCategoria[i].ToUpperInvariant(), 19f, P.TextoApagado, true);
                P.Colocar(cat.rectTransform, 112f, 20f, 310f, 26f);
                var prenda = P.Txt("Prenda", h.transform, "", 25f, P.Texto, true, TextAlignmentOptions.TopLeft, envolver: true);
                P.Colocar(prenda.rectTransform, 112f, 48f, 310f, 60f);
                _huecos.Add(h); _bordesDeHueco.Add(borde); _iconosDeHueco.Add(icono); _categoriasDeHueco.Add(cat); _prendasDeHueco.Add(prenda);
            }

            // Personaje en el centro.
            var centro = P.Vacio("Personaje", cuerpo);
            P.Colocar(centro, 522f, 0f, 663f, 886f);
            var resplandor = PiezasDeUI.Imagen("Resplandor", centro, PiezasDeUI.Resplandor, new Color(P.Oro.r, P.Oro.g, P.Oro.b, 0.32f));
            resplandor.raycastTarget = false;
            resplandor.rectTransform.anchorMin = resplandor.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            resplandor.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            resplandor.rectTransform.anchoredPosition = new Vector2(0f, 480f);
            resplandor.rectTransform.sizeDelta = new Vector2(820f, 820f);
            var peana = (RectTransform)P.CajaConBorde("Peana", centro, P.Borde, P.Panel, 5f, 40f).transform.parent;
            peana.anchorMin = peana.anchorMax = new Vector2(0.5f, 0f);
            peana.pivot = new Vector2(0.5f, 0.5f);
            peana.anchoredPosition = new Vector2(0f, 70f);
            peana.sizeDelta = new Vector2(420f, 80f);
            var fig = new GameObject("Figura", typeof(RectTransform), typeof(RawImage));
            fig.transform.SetParent(centro, false);
            _figura = fig.GetComponent<RawImage>();
            _figura.raycastTarget = false;
            var rtf = _figura.rectTransform;
            rtf.anchorMin = rtf.anchorMax = new Vector2(0.5f, 0f);
            rtf.pivot = new Vector2(0.5f, 0f);
            rtf.anchoredPosition = new Vector2(0f, 50f);
            rtf.sizeDelta = new Vector2(600f, 800f);
            var shader = Shader.Find("UI/RetratoDelGrupo");
            if (shader != null) { _material = new Material(shader) { name = "Figura del menú de Equipo" }; _figura.material = _material; }
            _sinFigura = P.Txt("Sin figura", centro, "", 26f, P.SobreNoche, false, TextAlignmentOptions.Center, envolver: true);
            P.Rellenar(_sinFigura.rectTransform, 60f);

            var der = P.CajaConBorde("Armario", cuerpo, P.Borde, P.Panel);
            P.Colocar((RectTransform)der.transform.parent, 1212f, 0f, 600f, 886f);
            var d = der.transform;
            _tituloArmario = P.Txt("Título", d, "", 22f, P.TextoApagado, true);
            P.Colocar(_tituloArmario.rectTransform, 28f, 26f, 540f, 34f);
            for (int i = 0; i < FilasDeArmario; i++)
            {
                int indice = i;
                var f = P.Caja("Opción " + i, d, P.Ficha, 16f);
                P.Colocar(f.rectTransform, 24f, 74f + i * 80f, 546f, 70f);
                P.Clicable(f, () => AlClicOpcion(indice));
                var icono = PiezasDeUI.Imagen("Icono", f.transform, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                P.Colocar(icono.rectTransform, 10f, 8f, 54f, 54f);
                var n = P.Txt("Nombre", f.transform, "", 23f, P.Texto, true, TextAlignmentOptions.MidlineLeft);
                P.Colocar(n.rectTransform, 76f, 0f, 340f, 70f);
                var puesta = P.Caja("Puesta", f.transform, P.Verde, 14f);
                P.ColocarDerecha(puesta.rectTransform, 12f, 19f, 108f, 32f);
                var tp = P.Txt("Texto", puesta.transform, "PUESTA", 17f, PaletaUI.PapelClaro, true, TextAlignmentOptions.Center);
                P.Rellenar(tp.rectTransform);
                _filas.Add(f); _iconosDeFila.Add(icono); _nombresDeFila.Add(n); _puestas.Add(puesta.gameObject);
            }
            var caja = P.Caja("Comparación", d, P.FichaOscura, 18f);
            P.Colocar(caja.rectTransform, 24f, 652f, 546f, 206f);
            _descripcion = P.Txt("Descripción", caja.transform, "", 22f, P.Texto, false, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_descripcion.rectTransform, 20f, 14f, 506f, 84f);
            _rasgo = P.Txt("Rasgos", caja.transform, "", 21f, P.TextoApagado, true, TextAlignmentOptions.TopLeft, envolver: true);
            P.Colocar(_rasgo.rectTransform, 20f, 104f, 506f, 92f);
        }

        // ─── Datos ────────────────────────────────────────────────────────────

        private Slot Personaje => _menu.Personaje;

        private GameObject _cuerpoVisto;
        private ModularAutoBuilder _ropaVista;

        /// La ropa por piezas del personaje elegido (se busca una vez por cuerpo).
        private ModularAutoBuilder Cuerpo()
        {
            var cuerpo = ControlDelJugador.CuerpoDe(Personaje);
            if (cuerpo != _cuerpoVisto)
            {
                _cuerpoVisto = cuerpo;
                _ropaVista = cuerpo != null ? cuerpo.GetComponentInChildren<ModularAutoBuilder>(true) : null;
            }
            return _ropaVista;
        }

        private static WardrobeInventory Armario()
            => PlayerService.TryGetComponent<WardrobeInventory>(out var w, includeInactive: true, allowSceneLookup: true) ? w : null;

        private string Puesta(PartCategory cat)
        {
            var sel = Cuerpo()?.GetSelection();
            return sel != null && sel.TryGetValue(cat, out var p) && !string.IsNullOrEmpty(p) ? p : null;
        }

        private static (string nombre, string descripcion, Sprite icono) Datos(PartCategory cat, string pieza, string nombreRespaldo = null, string descRespaldo = null, Sprite iconoRespaldo = null)
        {
            var item = WardrobeItemSO.FindPart(cat, pieza);
            string nombre = item != null ? item.GetLocalizedDisplayName() : (string.IsNullOrEmpty(nombreRespaldo) ? pieza : nombreRespaldo);
            return (Limpio(nombre), item != null ? item.GetLocalizedDescription() : descRespaldo, item != null && item.Icon != null ? item.Icon : iconoRespaldo);
        }

        /// Nombre para enseñar: sin punto final y sin el prefijo interno del pack («Body23_Aventurero» → «Aventurero»).
        private static string Limpio(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "";
            nombre = nombre.Trim().TrimEnd('.');
            int guion = nombre.IndexOf('_');
            if (guion > 0 && nombre.IndexOf(' ') < 0) nombre = nombre.Substring(guion + 1).Replace('_', ' ');
            return nombre;
        }

        private void LeerHuecos()
        {
            _visibles.Clear();
            var armario = Armario();
            var cuerpo = Cuerpo();
            for (int i = 0; i < Categorias.Length; i++)
            {
                bool tiene = Puesta(Categorias[i]) != null;
                if (!tiene && armario != null && cuerpo != null) tiene = armario.GetOptionsFor(Categorias[i], cuerpo, Personaje).Count > 0;
                if (tiene || Categorias[i] == PartCategory.Body) _visibles.Add(i);
            }
            _hueco = Mathf.Clamp(_hueco, 0, Mathf.Max(0, _visibles.Count - 1));
        }

        private void LeerOpciones()
        {
            _opciones.Clear();
            var armario = Armario();
            var cuerpo = Cuerpo();
            var cat = CategoriaActual;
            if (armario != null && cuerpo != null)
                foreach (var e in armario.GetOptionsFor(cat, cuerpo, Personaje))
                {
                    var (n, desc, ico) = Datos(cat, e.partName, e.displayName, e.description, e.icon);
                    _opciones.Add((e.partName, n, desc, ico));
                }
            if (cat != PartCategory.Body) _opciones.Add((null, "Nada", "Quitarse la prenda.", null));
        }

        private int IndiceDePuesta()
        {
            string puesta = Puesta(CategoriaActual);
            int i = _opciones.FindIndex(o => string.Equals(o.pieza, puesta, System.StringComparison.OrdinalIgnoreCase));
            return i >= 0 ? i : 0;
        }

        // ─── Mostrar ──────────────────────────────────────────────────────────

        public void Mostrar()
        {
            _zona = Zona.Huecos;
            LeerHuecos();
            Figura(true);
            Pintar();
        }

        public void Ocultar()
        {
            Figura(false);
        }

        public void AlCambiarPersonaje()
        {
            Figura(false);
            _zona = Zona.Huecos;
            _hueco = 0;
            LeerHuecos();
            Figura(true);
            Pintar();
        }

        private void Figura(bool ver)
        {
            var estudio = EstudioDeRetratos.Activo;
            if (!ver)
            {
                if (estudio != null && (int)_mirando >= 0) estudio.MostrarCuerpoEnteroPara(_mirando, this, false);
                _mirando = (Slot)(-1);
                return;
            }
            _mirando = Personaje;
            bool hay = estudio != null && estudio.TryObtenerCuerpoEntero(Personaje, out var img) && img.Valida;
            if (hay)
            {
                estudio.MostrarCuerpoEnteroPara(Personaje, this, true);
                estudio.TryObtenerCuerpoEntero(Personaje, out img);
                _figura.texture = img.Textura;
                if (_material != null)
                {
                    _material.SetTexture(IdTexturaSobreFondo, img.TexturaSobreFondo);
                    _material.SetFloat(IdUsarTexturaSobreFondo, img.TexturaSobreFondo != null ? 1f : 0f);
                }
            }
            _figura.enabled = hay;
            _sinFigura.text = hay ? "" : ControlDelJugador.Nombre(Personaje);
        }

        private void Pintar()
        {
            string nombre = ControlDelJugador.Nombre(Personaje);
            _tituloHuecos.text = "LO QUE LLEVA " + nombre.ToUpperInvariant();
            if (Cuerpo() == null)
            {
                // Sin ropa por piezas (Estela y Liam hasta INC-726): nada que cambiar.
                foreach (var b in _bordesDeHueco) P.Activar(b.gameObject, false);
                foreach (var f in _filas) P.Activar(f.gameObject, false);
                _tituloArmario.text = "EN EL ARMARIO";
                _descripcion.text = "La ropa de " + nombre + " todavía no se puede cambiar.";
                _rasgo.text = "";
                _menu.RefrescarPistas();
                return;
            }
            for (int i = 0; i < Categorias.Length; i++)
            {
                int k = _visibles.IndexOf(i);
                bool hay = k >= 0;
                P.Activar(_bordesDeHueco[i].gameObject, hay);
                if (!hay) continue;
                P.Colocar(_bordesDeHueco[i].rectTransform, 24f, 76f + k * 124f, 441f, 112f);
                var cat = Categorias[i];
                string pieza = Puesta(cat);
                string n = "Nada puesto";
                Sprite ico = null;
                if (pieza != null) { var datos = Datos(cat, pieza); n = datos.nombre; ico = datos.icono; }
                bool foco = k == _hueco;
                _huecos[i].color = foco && _zona == Zona.Huecos ? P.Elegida : P.Ficha;
                _bordesDeHueco[i].color = foco ? P.Oro : P.Ficha;
                _prendasDeHueco[i].text = n;
                _prendasDeHueco[i].color = pieza != null ? P.Texto : P.TextoApagado;
                _iconosDeHueco[i].sprite = ico;
                _iconosDeHueco[i].enabled = ico != null;
            }

            LeerOpciones();
            _tituloArmario.text = NombresDeCategoria[_visibles.Count > 0 ? _visibles[_hueco] : 0].ToUpperInvariant() + " · EN EL ARMARIO";
            int puesta = IndiceDePuesta();
            int elegido = _zona == Zona.Armario ? _opcion : puesta;
            if (elegido < _primera) _primera = elegido;
            if (elegido >= _primera + FilasDeArmario) _primera = elegido - FilasDeArmario + 1;
            if (_zona == Zona.Huecos) _primera = Mathf.Max(0, Mathf.Min(_primera, _opciones.Count - FilasDeArmario));
            for (int i = 0; i < FilasDeArmario; i++)
            {
                int k = _primera + i;
                bool hay = k < _opciones.Count;
                P.Activar(_filas[i].gameObject, hay);
                if (!hay) continue;
                var o = _opciones[k];
                bool foco = _zona == Zona.Armario && k == _opcion;
                _filas[i].color = foco ? P.Elegida : P.Ficha;
                _nombresDeFila[i].text = o.nombre;
                _iconosDeFila[i].sprite = o.icono;
                _iconosDeFila[i].enabled = o.icono != null;
                P.Activar(_puestas[i], k == puesta && (o.pieza != null || Puesta(CategoriaActual) == null));
            }

            var vista = elegido < _opciones.Count ? _opciones[elegido] : default;
            _descripcion.text = vista.descripcion ?? "";
            _rasgo.text = TextoDeRasgos(CategoriaActual, Puesta(CategoriaActual), vista.pieza);
            _menu.RefrescarPistas();
        }

        /// Lo que da la prenda vista frente a la puesta: + lo que gana, • lo que conserva, − lo que pierde.
        private static string TextoDeRasgos(PartCategory cat, string puesta, string vista)
        {
            var antes = puesta != null ? WardrobeItemSO.FindPart(cat, puesta) : null;
            var despues = vista != null ? WardrobeItemSO.FindPart(cat, vista) : null;
            var lineas = new List<string>();
            if (despues != null)
                foreach (var r in despues.Rasgos)
                {
                    bool yaLoTiene = antes != null && Contiene(antes.Rasgos, r);
                    lineas.Add(yaLoTiene
                        ? "<color=#614D3B>•  " + RasgosDePrenda.Texto(r) + "</color>"
                        : "<color=#257A3A>+  " + RasgosDePrenda.Texto(r) + "</color>");
                }
            if (antes != null && antes != despues)
                foreach (var r in antes.Rasgos)
                    if (despues == null || !Contiene(despues.Rasgos, r))
                        lineas.Add("<color=#B3261E>−  " + RasgosDePrenda.Texto(r) + "</color>");
            if (lineas.Count == 0) return vista != null ? "Solo cambia el aspecto" : "";
            return string.Join("\n", lineas);
        }

        private static bool Contiene(IReadOnlyList<RasgoDePrenda> lista, RasgoDePrenda r)
        {
            for (int i = 0; i < lista.Count; i++) if (lista[i] == r) return true;
            return false;
        }

        // ─── Acciones ─────────────────────────────────────────────────────────

        private void Probar()
        {
            var estudio = EstudioDeRetratos.Activo;
            if (estudio == null || _opcion >= _opciones.Count) return;
            estudio.ProbarPrenda(Personaje, CategoriaActual, _opciones[_opcion].pieza);
        }

        private void Ponerse(string pieza)
        {
            var cuerpo = Cuerpo();
            if (cuerpo == null) return;
            var cat = CategoriaActual;
            cuerpo.SetByName(cat, pieza);
            if (Personaje == ControlDelJugador.Activo)
            {
                if (PlayerService.TryGetComponent<PlayerPresetService>(out var pps, includeInactive: true, allowSceneLookup: true) && pps != null)
                {
                    pps.SnapshotAppearanceToPreset();
                    pps.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);
                }
            }
            else
            {
                var registro = CharacterAppearanceRegistry.Instance;
                registro?.UpdatePart(Personaje, cat, pieza);
                // La ropa de Will se guarda en la partida aunque no vaya al mando.
                if (Personaje == Slot.Will && registro != null && GameBootService.IsAvailable && GameBootService.Profile != null)
                    Partida.Preset.appearance = registro.GetAppearanceAsList(Slot.Will);
            }
            EstudioDeRetratos.Activo?.QuitarPruebas(Personaje);
            EntradaDelMenu.Sonido("UI_Submit");
            Pintar();
        }

        private void IrAlArmario()
        {
            if (_visibles.Count == 0) return;
            LeerOpciones();
            if (_opciones.Count == 0) return;
            _zona = Zona.Armario;
            _opcion = IndiceDePuesta();
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        private void VolverAHuecos()
        {
            _zona = Zona.Huecos;
            EstudioDeRetratos.Activo?.QuitarPruebas(Personaje);
            Pintar();
        }

        // ─── Entrada ──────────────────────────────────────────────────────────

        public void Tick()
        {
            if (Cuerpo() == null) return;
            float giro = EntradaDelMenu.Giro();
            if (giro != 0f) EstudioDeRetratos.Activo?.GirarCuerpoEntero(Personaje, giro * Time.unscaledDeltaTime);

            int h = EntradaDelMenu.Horizontal(), v = EntradaDelMenu.Vertical();
            if (_zona == Zona.Huecos)
            {
                if (v != 0)
                {
                    int n = Mathf.Clamp(_hueco + v, 0, Mathf.Max(0, _visibles.Count - 1));
                    if (n != _hueco) { _hueco = n; _primera = 0; EntradaDelMenu.Sonido("UI_Navigate"); Pintar(); }
                }
                if (h > 0 || EntradaDelMenu.Aceptar) IrAlArmario();
                else if (EntradaDelMenu.Quitar && CategoriaActual != PartCategory.Body && Puesta(CategoriaActual) != null) Ponerse(null);
            }
            else
            {
                if (v != 0)
                {
                    int n = Mathf.Clamp(_opcion + v, 0, Mathf.Max(0, _opciones.Count - 1));
                    if (n != _opcion) { _opcion = n; EntradaDelMenu.Sonido("UI_Navigate"); Probar(); Pintar(); }
                }
                if (h < 0) VolverAHuecos();
                else if (EntradaDelMenu.Aceptar && _opcion < _opciones.Count) Ponerse(_opciones[_opcion].pieza);
                else if (EntradaDelMenu.Quitar && CategoriaActual != PartCategory.Body) Ponerse(null);
            }
        }

        private void AlClicHueco(int categoria)
        {
            int k = _visibles.IndexOf(categoria);
            if (k < 0) return;
            if (_zona == Zona.Huecos && k == _hueco) { IrAlArmario(); return; }
            if (_zona == Zona.Armario) EstudioDeRetratos.Activo?.QuitarPruebas(Personaje);
            _zona = Zona.Huecos; _hueco = k; _primera = 0;
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        private void AlClicOpcion(int fila)
        {
            int k = _primera + fila;
            if (k >= _opciones.Count) return;
            if (_zona == Zona.Armario && k == _opcion) { Ponerse(_opciones[k].pieza); return; }
            _zona = Zona.Armario; _opcion = k;
            EntradaDelMenu.Sonido("UI_Navigate");
            Probar();
            Pintar();
        }

        public bool Cancelar()
        {
            if (_zona != Zona.Armario) return false;
            VolverAHuecos();
            return true;
        }
    }
}
