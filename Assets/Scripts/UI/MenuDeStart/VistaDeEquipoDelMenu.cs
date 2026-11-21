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
    /// Pestaña Equipo del menú de Start (maqueta «3 · Equipo»): a la izquierda lo que lleva el
    /// personaje elegido (cabeza, cuerpo, capa, accesorio), en el centro el personaje de cuerpo
    /// entero sobre su peana (copia del <see cref="EstudioDeRetratos"/>; gira con el stick derecho),
    /// a la derecha los rasgos de la prenda que se mira y abajo lo que hay en el armario para ese
    /// hueco. Al pasar por una prenda se la prueba la copia; A se la pone de verdad, X la quita.
    /// La ropa se pone en el cuerpo de ese personaje y pasa al registro de apariencias del grupo; la
    /// de Will se guarda además en la partida.
    /// </summary>
    public sealed class VistaDeEquipoDelMenu : IVistaDelMenuDeStart
    {
        private enum Zona { Huecos, Armario }
        private static readonly PartCategory[] Categorias = { PartCategory.Hat, PartCategory.Body, PartCategory.Cloak, PartCategory.Accessory };
        private static readonly string[] NombresDeCategoria = { "Cabeza", "Cuerpo", "Capa", "Accesorio" };
        private const int OpcionesVisibles = 3, RasgosVisibles = 2;

        private static readonly int IdTexturaSobreFondo = Shader.PropertyToID("_TexturaSobreFondo");
        private static readonly int IdUsarTexturaSobreFondo = Shader.PropertyToID("_UsarTexturaSobreFondo");

        private MenuDeStart _menu;
        private readonly List<P.Casilla> _huecos = new(), _opcionesVistas = new(), _rasgos = new();
        private readonly List<Image> _iconosDeHueco = new(), _iconosDeOpcion = new(), _iconosDeRasgo = new();
        private readonly List<TextMeshProUGUI> _prendasDeHueco = new(), _nombresDeOpcion = new(), _nombresDeRasgo = new(), _textosDeRasgo = new();
        private readonly List<GameObject> _puestas = new();
        private TextMeshProUGUI _nombrePeana, _sinFigura, _nombrePrenda, _categoriaPrenda, _tituloArmario, _nota;
        private Image _iconoPrenda;
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
        public bool UsaGatillos => true;

        public string Pistas
        {
            get
            {
                if (_zona == Zona.Huecos) return P.Boton(InputGlyphNames.Confirm) + " Cambiar     " + P.Boton(InputGlyphNames.MenuGirar) + " Girar";
                string p = P.Boton(InputGlyphNames.Confirm) + " Poner     ";
                if (CategoriaActual != PartCategory.Body) p += P.Boton(InputGlyphNames.MenuX) + " Quitar     ";
                return p + P.Boton(InputGlyphNames.MenuGirar) + " Girar";
            }
        }

        private PartCategory CategoriaActual => _visibles.Count > 0 ? Categorias[_visibles[Mathf.Clamp(_hueco, 0, _visibles.Count - 1)]] : PartCategory.Body;
        private int IndiceDeCategoriaActual => _visibles.Count > 0 ? _visibles[Mathf.Clamp(_hueco, 0, _visibles.Count - 1)] : 1;

        // ─── Montaje ───────────────────────────────────────────────────────────

        public void Montar(RectTransform cuerpo, MenuDeStart menu)
        {
            _menu = menu;
            for (int i = 0; i < Categorias.Length; i++)
            {
                int indice = i;
                var h = P.Casilla.Crear("Hueco " + NombresDeCategoria[i], cuerpo, 20f, 34f, () => AlClicHueco(indice), () => AlClicHueco(indice, true));
                PiezasDeUI.ColocarArribaIzquierda(h.raiz, 54f, 166f + i * 136f, 520f, 112f);
                var icono = PiezasDeUI.Imagen("Icono", h.contenido, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                PiezasDeUI.ColocarArribaIzquierda(icono.rectTransform, 16f, 16f, 80f, 80f);
                var cat = P.Txt("Categoría", h.contenido, NombresDeCategoria[i], 20f, P.TextoMaqueta, false);
                PiezasDeUI.ColocarArribaIzquierda(cat.rectTransform, 112f, 18f, 390f, 28f);
                var prenda = P.Titulo("Prenda", h.contenido, "", 28f, PaletaUI.TintaDeTexto);
                PiezasDeUI.ColocarArribaIzquierda(prenda.rectTransform, 112f, 46f, 390f, 44f);
                prenda.enableAutoSizing = true; prenda.fontSizeMin = 18f; prenda.fontSizeMax = 28f;
                _huecos.Add(h); _iconosDeHueco.Add(icono); _prendasDeHueco.Add(prenda);
            }

            // Personaje en el centro, sobre la peana.
            var halo = PiezasDeUI.Imagen("Resplandor", cuerpo, PiezasDeUI.Resplandor, new Color(P.Oro.r, P.Oro.g, P.Oro.b, 0.55f));
            halo.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(halo.rectTransform, 690f, 200f, 500f, 500f);
            PiezasDeUI.EstrellaEn("Chispa", cuerpo, 707f, 247f, 34f, 10f);
            PiezasDeUI.EstrellaEn("Chispa", cuerpo, 1153f, 313f, 26f, 0f);
            PiezasDeUI.EstrellaEn("Chispa", cuerpo, 671f, 501f, 22f, 20f);
            PiezasDeUI.EstrellaEn("Chispa", cuerpo, 1196f, 556f, 32f, 8f);
            PiezasDeUI.EstrellaEn("Chispa", cuerpo, 780f, 190f, 20f, 0f);
            PiezasDeUI.EstrellaEn("Chispa", cuerpo, 1101f, 211f, 22f, 14f);
            Elipse("Peana", cuerpo, 680f, 860f, 520f, 80f, PaletaUI.Papel, 8f, 1f);
            Elipse("Placa", cuerpo, 730f, 876f, 420f, 46f, PaletaUI.PapelApagado, 0f, 0.8f);
            _nombrePeana = P.Titulo("Nombre", cuerpo, "", 34f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center);
            PiezasDeUI.ColocarArribaIzquierda(_nombrePeana.rectTransform, 740f, 876f, 400f, 50f);
            var fig = new GameObject("Figura", typeof(RectTransform), typeof(RawImage));
            fig.transform.SetParent(cuerpo, false);
            fig.transform.SetSiblingIndex(_nombrePeana.transform.GetSiblingIndex() - 2);
            _figura = fig.GetComponent<RawImage>();
            _figura.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_figura.rectTransform, 678f, 196f, 525f, 700f);
            var shader = Shader.Find("UI/RetratoDelGrupo");
            if (shader != null) { _material = new Material(shader) { name = "Figura del menú de Equipo" }; _figura.material = _material; }
            _sinFigura = P.Txt("Sin figura", cuerpo, "", 26f, P.SobreNoche, false, TextAlignmentOptions.Center, envolver: true);
            PiezasDeUI.ColocarArribaIzquierda(_sinFigura.rectTransform, 680f, 400f, 520f, 200f);

            // Rasgos de la prenda que se mira.
            P.Hoja("Rasgos", cuerpo, 1352f, 160f, 460f, 520f);
            P.Etiqueta("Título rasgos", cuerpo, "Rasgos", 1376f, 136f, 230f, 52f, 30f);
            _iconoPrenda = PiezasDeUI.Imagen("Icono", cuerpo, null, Color.white);
            _iconoPrenda.preserveAspect = true; _iconoPrenda.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(_iconoPrenda.rectTransform, 1382f, 210f, 84f, 84f);
            _nombrePrenda = P.Titulo("Prenda", cuerpo, "", 32f, PaletaUI.TintaDeTexto);
            PiezasDeUI.ColocarArribaIzquierda(_nombrePrenda.rectTransform, 1480f, 216f, 310f, 44f);
            _nombrePrenda.enableAutoSizing = true; _nombrePrenda.fontSizeMin = 20f; _nombrePrenda.fontSizeMax = 32f;
            _categoriaPrenda = P.Txt("Categoría", cuerpo, "", 20f, P.TextoMaqueta, false);
            PiezasDeUI.ColocarArribaIzquierda(_categoriaPrenda.rectTransform, 1480f, 258f, 310f, 28f);
            for (int i = 0; i < RasgosVisibles; i++)
            {
                var r = P.Casilla.Crear("Rasgo " + i, cuerpo, 18f, 30f, null);
                PiezasDeUI.ColocarArribaIzquierda(r.raiz, 1380f, 324f + i * 158f, 404f, 138f);
                r.carta.Caida(0f);
                var icono = PiezasDeUI.Imagen("Icono", r.contenido, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                PiezasDeUI.ColocarArribaIzquierda(icono.rectTransform, 16f, 20f, 56f, 56f);
                var n = P.Titulo("Nombre", r.contenido, "", 26f, PaletaUI.TintaDeTexto);
                PiezasDeUI.ColocarArribaIzquierda(n.rectTransform, 92f, 14f, 296f, 36f);
                n.enableAutoSizing = true; n.fontSizeMin = 18f; n.fontSizeMax = 26f;
                var t = P.Txt("Texto", r.contenido, "", 20f, PaletaUI.TintaDeTexto, false, TextAlignmentOptions.TopLeft, envolver: true);
                PiezasDeUI.ColocarArribaIzquierda(t.rectTransform, 92f, 50f, 296f, 80f);
                t.enableAutoSizing = true; t.fontSizeMin = 15f; t.fontSizeMax = 20f;
                _rasgos.Add(r); _iconosDeRasgo.Add(icono); _nombresDeRasgo.Add(n); _textosDeRasgo.Add(t);
            }

            // El armario: lo que hay para ese hueco.
            P.Hoja("Armario", cuerpo, 1352f, 720f, 460f, 240f);
            _tituloArmario = P.Titulo("Título armario", cuerpo, "", 26f, PaletaUI.TintaDeTexto, TextAlignmentOptions.TopLeft);
            PiezasDeUI.ColocarArribaIzquierda(_tituloArmario.rectTransform, 1382f, 734f, 400f, 36f);
            for (int i = 0; i < OpcionesVisibles; i++)
            {
                int indice = i;
                var o = P.Casilla.Crear("Opción " + i, cuerpo, 20f, 36f, () => AlClicOpcion(indice), () => AlClicOpcion(indice, true));
                PiezasDeUI.ColocarArribaIzquierda(o.raiz, 1382f + i * 142f, 786f, 118f, 118f);
                var icono = PiezasDeUI.Imagen("Icono", o.contenido, null, Color.white);
                icono.preserveAspect = true; icono.raycastTarget = false;
                PiezasDeUI.ColocarArribaIzquierda(icono.rectTransform, 19f, 15f, 80f, 80f);
                var n = P.Titulo("Nombre", o.contenido, "", 20f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center, envolver: true);
                P.Rellenar(n.rectTransform, 10f);
                n.enableAutoSizing = true; n.fontSizeMin = 13f; n.fontSizeMax = 20f;
                var puesta = P.Pastilla("Puesta", o.raiz, new Color32(0x7E, 0xD0, 0x8A, 0xFF), 24f, 104f, 70f, 24f, 15f, out _);
                puesta.text = "puesta";
                _opcionesVistas.Add(o); _iconosDeOpcion.Add(icono); _nombresDeOpcion.Add(n); _puestas.Add(puesta.transform.parent.gameObject);
            }
            _nota = P.Txt("Nota", cuerpo, "", 18f, P.TextoMaqueta, false, TextAlignmentOptions.TopLeft);
            PiezasDeUI.ColocarArribaIzquierda(_nota.rectTransform, 1382f, 912f, 410f, 40f);
            _nota.enableAutoSizing = true; _nota.fontSizeMin = 13f; _nota.fontSizeMax = 18f;
        }

        /// Óvalo de papel (la peana): sombra, borde de tinta, filo claro y relleno.
        private static void Elipse(string nombre, Transform padre, float x, float y, float ancho, float alto, Color relleno, float caida, float opacidad)
        {
            var raiz = P.Vacio(nombre, padre);
            PiezasDeUI.ColocarArribaIzquierda(raiz, x, y, ancho, alto);
            if (caida > 0f)
            {
                var sombra = PiezasDeUI.Imagen("Sombra", raiz, PiezasDeUI.Circulo, PiezasDeUI.SombraDeCarta);
                sombra.raycastTarget = false;
                PiezasDeUI.Estirar(sombra.rectTransform);
                sombra.rectTransform.offsetMin = new Vector2(0f, -caida);
                sombra.rectTransform.offsetMax = new Vector2(0f, -caida);
            }
            var borde = PiezasDeUI.Imagen("Borde", raiz, PiezasDeUI.Circulo, new Color(PaletaUI.Tinta.r, PaletaUI.Tinta.g, PaletaUI.Tinta.b, opacidad));
            borde.raycastTarget = false;
            PiezasDeUI.Estirar(borde.rectTransform);
            var filo = PiezasDeUI.Imagen("Filo", raiz, PiezasDeUI.Circulo, new Color(PaletaUI.PapelClaro.r, PaletaUI.PapelClaro.g, PaletaUI.PapelClaro.b, opacidad));
            filo.raycastTarget = false;
            PiezasDeUI.Margen(filo.rectTransform, 3f);
            var dentro = PiezasDeUI.Imagen("Relleno", raiz, PiezasDeUI.Circulo, new Color(relleno.r, relleno.g, relleno.b, opacidad));
            dentro.raycastTarget = false;
            PiezasDeUI.Margen(dentro.rectTransform, caida > 0f ? 6f : 3f);
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
            _nombrePeana.text = ControlDelJugador.Nombre(Personaje);
        }

        private void Pintar()
        {
            string nombre = ControlDelJugador.Nombre(Personaje);
            _nombrePeana.text = nombre;
            if (Cuerpo() == null)
            {
                // Sin ropa por piezas: nada que cambiar.
                foreach (var h in _huecos) P.Activar(h.raiz.gameObject, false);
                foreach (var o in _opcionesVistas) P.Activar(o.raiz.gameObject, false);
                foreach (var r in _rasgos) P.Activar(r.raiz.gameObject, false);
                _iconoPrenda.enabled = false;
                _nombrePrenda.text = "";
                _categoriaPrenda.text = "";
                _tituloArmario.text = "En el armario";
                _nota.text = "La ropa de " + nombre + " todavía no se puede cambiar.";
                _menu.RefrescarPistas();
                return;
            }
            var arte = _menu.Arte;
            for (int i = 0; i < Categorias.Length; i++)
            {
                int k = _visibles.IndexOf(i);
                bool hay = k >= 0;
                P.Activar(_huecos[i].raiz.gameObject, hay);
                if (!hay) continue;
                PiezasDeUI.ColocarArribaIzquierda(_huecos[i].raiz, 54f, 166f + k * 136f, 520f, 112f);
                var cat = Categorias[i];
                string pieza = Puesta(cat);
                string n = "Nada puesto";
                Sprite ico = null;
                if (pieza != null) { var datos = Datos(cat, pieza); n = datos.nombre; ico = datos.icono; }
                if (ico == null && arte != null && i < arte.huecos.Length) ico = arte.huecos[i];
                _huecos[i].Poner(k == _hueco ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
                _prendasDeHueco[i].text = n;
                _prendasDeHueco[i].color = pieza != null ? PaletaUI.TintaDeTexto : P.TextoMaqueta;
                _iconosDeHueco[i].sprite = ico;
                _iconosDeHueco[i].enabled = ico != null;
            }

            LeerOpciones();
            string categoria = NombresDeCategoria[IndiceDeCategoriaActual];
            _tituloArmario.text = "Probar otra prenda: " + categoria.ToLowerInvariant();
            int puesta = IndiceDePuesta();
            int elegido = _zona == Zona.Armario ? _opcion : puesta;
            if (elegido < _primera) _primera = elegido;
            if (elegido >= _primera + OpcionesVisibles) _primera = elegido - OpcionesVisibles + 1;
            _primera = Mathf.Clamp(_primera, 0, Mathf.Max(0, _opciones.Count - OpcionesVisibles));
            for (int i = 0; i < OpcionesVisibles; i++)
            {
                int k = _primera + i;
                bool hay = k < _opciones.Count;
                var o = _opcionesVistas[i];
                P.Activar(o.raiz.gameObject, true);
                if (!hay) { o.Poner(P.EstadoDeCasilla.Vacia); P.Activar(_puestas[i], false); _iconosDeOpcion[i].enabled = false; _nombresDeOpcion[i].text = ""; continue; }
                var op = _opciones[k];
                o.Poner(_zona == Zona.Armario && k == _opcion ? P.EstadoDeCasilla.Elegida : P.EstadoDeCasilla.Llena);
                _iconosDeOpcion[i].sprite = op.icono;
                _iconosDeOpcion[i].enabled = op.icono != null;
                _nombresDeOpcion[i].text = op.icono != null ? "" : op.nombre;
                P.Activar(_puestas[i], k == puesta && (op.pieza != null || Puesta(CategoriaActual) == null));
            }

            // La prenda que se mira: la puesta, o la del armario si se está eligiendo.
            var vista = elegido < _opciones.Count ? _opciones[elegido] : default;
            string piezaVista = _zona == Zona.Armario ? vista.pieza : Puesta(CategoriaActual);
            Sprite iconoVista = _zona == Zona.Armario ? vista.icono : null;
            string nombreVista = _zona == Zona.Armario ? vista.nombre : "Nada puesto";
            string descVista = _zona == Zona.Armario ? vista.descripcion : null;
            if (_zona == Zona.Huecos && piezaVista != null)
            {
                var d = Datos(CategoriaActual, piezaVista);
                nombreVista = d.nombre; iconoVista = d.icono; descVista = d.descripcion;
            }
            if (iconoVista == null && arte != null && IndiceDeCategoriaActual < arte.huecos.Length) iconoVista = arte.huecos[IndiceDeCategoriaActual];
            _iconoPrenda.sprite = iconoVista;
            _iconoPrenda.enabled = iconoVista != null;
            _nombrePrenda.text = nombreVista;
            _categoriaPrenda.text = categoria;
            PintarRasgos(CategoriaActual, Puesta(CategoriaActual), piezaVista, descVista);
            _menu.RefrescarPistas();
        }

        /// Las cartas de rasgos de la prenda vista (con «+» los que se ganan frente a la puesta) y,
        /// abajo, los que se pierden.
        private void PintarRasgos(PartCategory cat, string puesta, string vista, string descripcion)
        {
            var antes = puesta != null ? WardrobeItemSO.FindPart(cat, puesta) : null;
            var despues = vista != null ? WardrobeItemSO.FindPart(cat, vista) : null;
            var arte = _menu.Arte;
            int n = 0;
            if (despues != null)
                foreach (var r in despues.Rasgos)
                {
                    if (n >= RasgosVisibles) { _textosDeRasgo[RasgosVisibles - 1].text += "\n" + RasgosDePrenda.Texto(r); continue; }
                    bool nuevo = antes != despues && (antes == null || !Contiene(antes.Rasgos, r));
                    _nombresDeRasgo[n].text = (nuevo ? "<color=#257A3A>+ </color>" : "") + NombreDeRasgo(r);
                    _textosDeRasgo[n].text = RasgosDePrenda.Texto(r) + ".";
                    _iconosDeRasgo[n].sprite = arte != null ? arte.IconoDe(r) : null;
                    _iconosDeRasgo[n].enabled = _iconosDeRasgo[n].sprite != null;
                    n++;
                }
            if (n == 0 && vista != null)
            {
                _nombresDeRasgo[0].text = "Solo aspecto";
                _textosDeRasgo[0].text = string.IsNullOrWhiteSpace(descripcion) ? "Cambia cómo se ve, no cómo juegas." : descripcion;
                _iconosDeRasgo[0].sprite = arte != null ? arte.rasgoPorDefecto : null;
                _iconosDeRasgo[0].enabled = _iconosDeRasgo[0].sprite != null;
                n = 1;
            }
            for (int i = 0; i < RasgosVisibles; i++)
            {
                P.Activar(_rasgos[i].raiz.gameObject, i < n);
                _rasgos[i].Poner(P.EstadoDeCasilla.Llena);
            }

            var pierde = new List<string>();
            if (antes != null && antes != despues)
                foreach (var r in antes.Rasgos)
                    if (despues == null || !Contiene(despues.Rasgos, r)) pierde.Add(NombreDeRasgo(r));
            _nota.text = pierde.Count > 0
                ? "<color=#B3261E>Pierdes: " + string.Join(", ", pierde) + "</color>"
                : "Sin números fríos: cada prenda cambia cómo juegas.";
        }

        private static string NombreDeRasgo(RasgoDePrenda r)
        {
            string porDefecto = r switch
            {
                RasgoDePrenda.VientoMasBarato => "Viento ligero",
                RasgoDePrenda.ContraataqueMasAmplio => "Buen reflejo",
                RasgoDePrenda.Planear => "Planear",
                RasgoDePrenda.ReboteExtra => "Rebote extra",
                _ => r.ToString()
            };
            string clave = "RASGO_NOMBRE_" + r.ToString().ToUpperInvariant();
            return LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, porDefecto) : porDefecto;
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
            // Lo que lleva su cuerpo pasa a ser su ropa; la de Will se guarda además en la partida.
            var registro = CharacterAppearanceRegistry.Instance;
            registro?.CaptureCurrentAppearance(Personaje);
            if (Personaje == Slot.Will && registro != null && GameBootService.IsAvailable && GameBootService.Profile != null)
                Partida.Preset.appearance = registro.GetAppearanceAsList(Slot.Will);
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
                // El armario es una fila: izquierda y derecha (o arriba y abajo) pasan de prenda; a la
                // izquierda de la primera se vuelve a los huecos.
                int paso = h != 0 ? h : v;
                if (paso < 0 && _opcion == 0 && h < 0) { VolverAHuecos(); return; }
                if (paso != 0)
                {
                    int n = Mathf.Clamp(_opcion + paso, 0, Mathf.Max(0, _opciones.Count - 1));
                    if (n != _opcion) { _opcion = n; EntradaDelMenu.Sonido("UI_Navigate"); Probar(); Pintar(); }
                }
                else if (EntradaDelMenu.Aceptar && _opcion < _opciones.Count) Ponerse(_opciones[_opcion].pieza);
                else if (EntradaDelMenu.Quitar && CategoriaActual != PartCategory.Body) Ponerse(null);
            }
        }

        /// Clic: elige el hueco o, si ya lo estaba, abre el armario. Al pasar el ratón, solo lo elige.
        private void AlClicHueco(int categoria, bool soloElegir = false)
        {
            int k = _visibles.IndexOf(categoria);
            if (k < 0) return;
            if (_zona == Zona.Huecos && k == _hueco) { if (!soloElegir) IrAlArmario(); return; }
            if (soloElegir && _zona == Zona.Armario) return; // pasar por la izquierda no deja el armario a medias
            if (_zona == Zona.Armario) EstudioDeRetratos.Activo?.QuitarPruebas(Personaje);
            _zona = Zona.Huecos; _hueco = k; _primera = 0;
            EntradaDelMenu.Sonido("UI_Navigate");
            Pintar();
        }

        /// Clic: se prueba la prenda o, si ya se estaba probando, se la pone. Al pasar el ratón, solo se la prueba.
        private void AlClicOpcion(int fila, bool soloProbar = false)
        {
            int k = _primera + fila;
            if (k >= _opciones.Count) return;
            if (_zona == Zona.Armario && k == _opcion) { if (!soloProbar) Ponerse(_opciones[k].pieza); return; }
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
