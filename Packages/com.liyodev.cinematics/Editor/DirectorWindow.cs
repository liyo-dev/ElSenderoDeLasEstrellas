using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liyodev.Cinematics
{
    /// La mesa del Director: la lista de cinemáticas del proyecto, la cinemática como una película que
    /// se reproduce y se arrastra sin entrar en PlayMode (en vivo, con la escena colocada en cada
    /// segundo, o con los fotogramas del storyboard; con subtítulos y voces) y las puertas a lo que ya
    /// sabe editarlas (el taller de rodaje, el .guion.txt, el storyboard).
    ///
    /// Disposición: a la izquierda los planos; en el centro el visor y el transporte (planos y frases); a la
    /// derecha las marcas para la IA (señalar, dirección, dibujo, nota), y abajo la caja de la IA, que
    /// es el chat compartido del asistente con el contexto de la mesa (plano, segundo y marcas).
    ///
    /// No sabe nada del proyecto ni del grafo: todo lo pregunta al puente
    /// (<see cref="ICinematicsEditorBridge"/>). Ver `docs/plan-director-2026-11-16.md`.
    public class DirectorWindow : EditorWindow
    {
        /// La cinemática que el Director tiene abierta y el segundo que se mira. Lo lee
        /// el asistente de IA desde fuera; la ventana no necesita saber quién lo escucha.
        public static string CinematicaActivaId { get; private set; }
        public static int PlanoActivo { get; private set; } = -1;
        public static float SegundoActivo { get; private set; } = -1f;
        public static event Action SeleccionCambiada;

        private const float AnchoPlanos = 196f;
        private const float AnchoMarcas = 252f;
        private const float AltoRespuestasIA = 240f;
        private const string ClaveIaPlegada = "Liyodev.Cinematics.Director.IaPlegada";

        private readonly List<CinemaEntry> _entradas = new();
        private readonly List<CinemaEntry> _visibles = new();
        private readonly Dictionary<string, Texture2D> _texturas = new();

        private string _busqueda = string.Empty;
        private Vector2 _scrollLista;
        private Vector2 _scrollPlanos;
        private Vector2 _scrollMarcas;
        private Vector2 _scrollAvisos;

        private CinemaPizarra _pizarra;
        private string _elementoElegido = "camara";
        private string _textoNota = string.Empty;
        private Herramienta _herramienta;
        private CinemaTrazo _trazoEnCurso;

        private CinemaEntry _elegida;
        private string _ultimaElegidaId;
        private CinemaDetail _detalle;
        private string _mensaje;

        private bool _reproduciendo;
        private float _t;
        private double _ultimoTick;
        private double _ultimaRevisionDeEncargos;
        private bool _verEncargos;
        private Vector2 _scrollEncargos;
        private List<CinemaEncargo> _encargos = new();
        private const double IntervaloRevisionDeEncargos = 2.0;
        private string _vozSonando;
        private int _plano = -1;
        private bool _verAvisos;
        private float _tAnterior;
        private CinemaClip _musicaDePreviz;
        private float _vozHasta;

        // Vista en vivo: la escena colocada en el segundo del cabezal y rendida en una textura.
        private const string ClaveEnVivo = "Liyodev.Cinematics.Director.EnVivo";
        private const float IntervaloEnVivo = 1f / 30f;
        private const int AnchoMaximoEnVivo = 1280;
        private bool _enVivo = true;
        private ICinematicLiveView _vista;
        private string _errorVista;
        private RenderTexture _rtVivo;
        private Vector2Int _tamanoVivo = new(960, 540);
        private float _tRendido = -1f;
        private double _ultimoRender;
        private bool _vivoRendido;
        // La escena no se abre solo por elegir una cinemática: hasta que se reproduce, se arrastra o
        // se enciende «En vivo», el visor enseña los fotogramas del storyboard.
        private bool _vivoPedido;

        // En PlayMode la mesa manda sobre la cinemática que rueda de verdad (si el puente sabe).
        private bool _siguiendoPlay;
        private bool _pausadaEnPlay;
        private static readonly int CabezalId = "Director.Cabezal".GetHashCode();

        // La mesa es IMGUI dentro de un contenedor; debajo, la caja de la IA (UI Toolkit).
        private IMGUIContainer _mesaIMGUI;
        private VisualElement _cajaIA;
        private Label _chipsIA;
        private Button _plegarIA;
        private Button _conectarIA;
        private VisualElement _vistaIA;
        private IDirectorAssistant _ia;
        private bool _iaPlegada;
        private Rect _area;
        private float _anchoCentro = 640f;

        /// Lo que hace un clic en el visor: nada (ver) o dejar una marca para la IA.
        private enum Herramienta { Ver, Senalar, Direccion, Dibujar }

        [NonSerialized] private GUIStyle _estiloSeccion;
        [NonSerialized] private GUIStyle _estiloTramo;
        [NonSerialized] private GUIStyle _estiloNotaTexto;
        [NonSerialized] private GUIStyle _estiloChip;
        [NonSerialized] private GUIStyle _estiloChipElegido;

        [NonSerialized] private GUIStyle _estiloNombreFila;
        [NonSerialized] private GUIStyle _estiloNombreElegido;
        [NonSerialized] private GUIStyle _estiloMetaFila;
        [NonSerialized] private GUIStyle _estiloInsignia;
        [NonSerialized] private GUIStyle _estiloSubtitulo;
        [NonSerialized] private GUIStyle _estiloSobre;

        [MenuItem("Liyodev/Director/Mesa del Director", false, 1)]
        public static void Abrir()
        {
            var ventana = GetWindow<DirectorWindow>("Director");
            ventana.minSize = new Vector2(1180, 820);
            ventana.titleContent = new GUIContent("Director");
            ventana.Show();
        }

        /// Abre la mesa si hace falta y elige una cinemática por id. Lo usa el puente del asistente.
        public static bool SeleccionarPorId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var ventana = GetWindow<DirectorWindow>("Director");
            ventana.minSize = new Vector2(1180, 820);
            ventana.RefrescarLista();
            var entrada = ventana._entradas.Find(e => e.id == id);
            if (entrada == null) return false;

            ventana.Seleccionar(entrada, true);
            ventana.RepintarMesa();
            return true;
        }

        private void OnEnable()
        {
            _enVivo = EditorPrefs.GetBool(ClaveEnVivo, true);
            _iaPlegada = EditorPrefs.GetBool(ClaveIaPlegada, false);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += AlCambiarElModoDeJuego;
            CinematicsEditorServices.Cambiaron += RefrescarLista;
            RefrescarLista();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= AlCambiarElModoDeJuego;
            CinematicsEditorServices.Cambiaron -= RefrescarLista;
            if (_ia != null)
            {
                _ia.SettingsChanged -= ActualizarBotonDeIA;
                _ia.Dispose();
                _ia = null;
                _vistaIA = null;
            }
            Parar();
            CerrarVista();
            LiberarTexturaEnVivo();
            DestruirTexturas();
            CinematicaActivaId = null;
            PlanoActivo = -1;
            SegundoActivo = -1f;
            SeleccionCambiada?.Invoke();
        }

        // ── Datos ────────────────────────────────────────────────────────────────────────────

        private void RefrescarLista()
        {
            var puente = CinematicsEditorServices.Bridge;
            _entradas.Clear();

            if (puente == null)
            {
                _mensaje = "No hay puente de cinemáticas en este proyecto: el Director no sabe qué cinemáticas hay.";
                _visibles.Clear();
                return;
            }

            try { _entradas.AddRange(puente.Listar()); }
            catch (System.Exception e) { _mensaje = "Al listar las cinemáticas: " + e.Message; }

            Filtrar();

            if (_elegida != null)
            {
                float segundo = _t;
                var misma = _entradas.Find(x => x.id == _elegida.id);
                if (misma != null)
                {
                    Parar();
                    Seleccionar(misma, true);
                    _t = Mathf.Clamp(segundo, 0f, Total());
                    SincronizarPlano();
                }
                else { _elegida = null; _detalle = null; }
            }

            RepintarMesa();
        }

        /// Los encargos de la IA se cierran fuera de la ventana (buzón, otra sesión, edición a mano del
        /// archivo): se vuelve a contar cada pocos segundos y al recuperar el foco, y solo se repinta
        /// si el número ha cambiado.
        private void RevisarEncargosPendientes(bool ya = false)
        {
            double ahora = EditorApplication.timeSinceStartup;
            if (!ya && ahora - _ultimaRevisionDeEncargos < IntervaloRevisionDeEncargos) return;
            _ultimaRevisionDeEncargos = ahora;

            var puente = CinematicsEditorServices.Bridge;
            if (puente == null || _entradas.Count == 0) return;

            bool cambio = false;
            try
            {
                foreach (var e in _entradas)
                {
                    int ahoraN = puente.EncargosPendientes(e.id);
                    if (ahoraN == e.encargos) continue;
                    e.encargos = ahoraN;
                    if (e == _elegida)
                    {
                        _encargos = puente.LeerEncargos(e.id);
                        _pizarra = puente.LeerPizarra(e.id);
                    }
                    cambio = true;
                }
            }
            catch { return; }

            if (_detalle?.entrada != null && _elegida != null && _detalle.entrada.encargos != _elegida.encargos)
            {
                _detalle.entrada.encargos = _elegida.encargos;
                cambio = true;
            }

            if (cambio) RepintarMesa();
        }

        private void OnFocus() => RevisarEncargosPendientes(true);

        private void Filtrar()
        {
            _visibles.Clear();
            string busca = (_busqueda ?? string.Empty).Trim();
            foreach (var e in _entradas)
            {
                if (busca.Length > 0)
                {
                    string texto = (e.nombre + " " + e.escena).ToLowerInvariant();
                    if (!texto.Contains(busca.ToLowerInvariant())) continue;
                }
                _visibles.Add(e);
            }
        }

        private void Seleccionar(CinemaEntry entrada, bool cargar = true)
        {
            if (entrada == null) return;
            if (_elegida?.id != entrada.id)
            {
                Parar();
                _verEncargos = false;
                _scrollEncargos = Vector2.zero;
            }
            _elegida = entrada;
            CinematicaActivaId = entrada.id;
            PlanoActivo = -1;
            SegundoActivo = 0f;
            SeleccionCambiada?.Invoke();
            if (cargar) CargarDetalle();
        }

        private void CargarDetalle()
        {
            CerrarVista();
            _vivoPedido = false;
            _detalle = null;
            _plano = -1;
            PlanoActivo = -1;
            SegundoActivo = 0f;
            _verAvisos = false;
            DescartarTexturas();

            var puente = CinematicsEditorServices.Bridge;
            if (puente == null || _elegida == null) return;

            _encargos.Clear();
            try
            {
                _detalle = puente.Leer(_elegida.id);
                _encargos = puente.LeerEncargos(_elegida.id);
            }
            catch (System.Exception e) { _mensaje = "Al abrir la cinemática: " + e.Message; }

            try { _pizarra = puente.LeerPizarra(_elegida.id) ?? new CinemaPizarra { guion = _elegida.id }; }
            catch { _pizarra = new CinemaPizarra { guion = _elegida.id }; }

            _t = 0f;
            _trazoEnCurso = null;
            _textoNota = string.Empty;
            CalcularTramosSinPlano();
            SincronizarPlano();
        }

        // ── Tramos sin plano ─────────────────────────────────────────────────────────────────

        /// Un trozo de la cinemática sin plano del guion: lo monta un efecto que pone su propia
        /// cámara al jugar (una apertura, un vuelo). Ni el storyboard ni la vista en vivo lo pueden
        /// enseñar; la mesa lo marca y deja verlo en Play desde ahí.
        private sealed class TramoSinPlano
        {
            public float t0;
            public float t1;
            public string que;
        }

        private readonly List<TramoSinPlano> _tramosSinPlano = new();

        private void CalcularTramosSinPlano()
        {
            _tramosSinPlano.Clear();
            if (_detalle == null || _detalle.planos.Count == 0) return;

            const float Minimo = 0.75f;
            float desde = 0f;
            foreach (var plano in _detalle.planos)
            {
                if (plano.t0 - desde > Minimo) AnadirTramo(desde, plano.t0);
                desde = Mathf.Max(desde, plano.t1);
            }
            float total = Total();
            if (total - desde > Minimo) AnadirTramo(desde, total);
        }

        private void AnadirTramo(float t0, float t1)
        {
            // Los efectos largos que empiezan en el tramo son los que lo montan.
            var nombres = new List<string>();
            foreach (var pista in _detalle.pistas)
            {
                if (pista.tipo != CinemaTrackKind.Vfx && pista.tipo != CinemaTrackKind.Escena) continue;
                foreach (var clip in pista.clips)
                    if (clip.t0 >= t0 - 0.05f && clip.t0 < t1 && clip.t1 - clip.t0 >= 2f && !string.IsNullOrEmpty(clip.etiqueta)
                        && !nombres.Contains(clip.etiqueta))
                        nombres.Add(clip.etiqueta);
            }
            _tramosSinPlano.Add(new TramoSinPlano
            {
                t0 = t0,
                t1 = t1,
                que = nombres.Count > 0 ? string.Join("\n", nombres.GetRange(0, Mathf.Min(3, nombres.Count))) : "un efecto",
            });
        }

        /// Elemento de las notas de un tramo sin plano: el efecto que lo monta.
        private const string ElementoEfecto = "efecto";

        /// El nombre de un tramo en la pizarra: «T01», «T02»… (los planos son «P01»…).
        private string PanelDeTramo(TramoSinPlano tramo) => $"T{_tramosSinPlano.IndexOf(tramo) + 1:00}";

        private TramoSinPlano TramoEn(float t)
        {
            foreach (var tramo in _tramosSinPlano)
                if (t >= tramo.t0 && t < tramo.t1) return tramo;
            return null;
        }

        private void DibujarTramoSinPlano(Rect marco, TramoSinPlano tramo)
        {
            var imagen = Encajar(marco, 16f / 9f);
            EditorGUI.DrawRect(imagen, new Color(0.08f, 0.09f, 0.12f));

            var texto = new Rect(imagen.x + 24f, imagen.y + imagen.height * 0.22f, imagen.width - 48f, imagen.height * 0.4f);
            GUI.Label(texto,
                $"Este trozo ({Reloj(tramo.t0)} – {Reloj(tramo.t1)}) lo monta un efecto con su propia cámara, " +
                $"y solo se ve al jugar:\n\n{tramo.que}", _estiloTramo);

            var boton = new Rect(imagen.center.x - 110f, imagen.y + imagen.height * 0.68f, 220f, 28f);
            if (GUI.Button(boton, $"▶  Rodar tramo desde {Reloj(tramo.t0)}"))
                RodarDesdeElCabezal();

            DibujarSubtitulo(imagen);
            var reloj = new Rect(imagen.x, imagen.y + 6f, imagen.width - 8f, 18f);
            _estiloSobre.alignment = TextAnchor.UpperRight;
            GUI.Label(reloj, $"{Reloj(_t)} / {Reloj(Total())} · sin plano", _estiloSobre);
            _estiloSobre.alignment = TextAnchor.UpperLeft;
        }

        private float Total()
        {
            if (_detalle == null) return 0f;
            if (_detalle.entrada != null && _detalle.entrada.duracion > 0.01f) return _detalle.entrada.duracion;
            float total = 0f;
            foreach (var p in _detalle.planos) total = Mathf.Max(total, p.t1);
            return total;
        }

        // ── Reloj del flipbook ───────────────────────────────────────────────────────────────

        private void Tick()
        {
            RevisarEncargosPendientes();
            if (SeguirLaDePlay()) return;
            ActualizarVistaEnVivo();
            if (!_reproduciendo || _detalle == null) return;

            double ahora = EditorApplication.timeSinceStartup;
            float dt = (float)(ahora - _ultimoTick);
            _ultimoTick = ahora;

            float total = Total();
            if (total <= 0f) { Parar(); return; }

            _t += dt;
            if (_t >= total)
            {
                _t = total;
                Parar();
            }
            else
            {
                SonarLaFraseQueToca();
                SonarEfectosQueTocan();
            }
            SincronizarPlano();
            RepintarMesa();
        }

        private void Reproducir()
        {
            if (_detalle == null) return;
            if (_t >= Total() - 0.01f) _t = 0f;
            if (TramoEn(_t) != null)
            {
                RodarDesdeElCabezal();
                return;
            }
            _vivoPedido = true;
            _reproduciendo = true;
            _ultimoTick = EditorApplication.timeSinceStartup;
            _vozSonando = null;
            _vozHasta = 0f;
            _tAnterior = _t - 0.0001f;
            SonarLaFraseQueToca();
            SonarEfectosQueTocan();
            SincronizarPlano();
        }

        private void RodarDesdeElCabezal()
        {
            if (_elegida == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Los efectos con cámara propia necesitan ejecutar su entrada para montar el plano.
            float inicio = TramoEn(_t)?.t0 ?? _t;
            Parar();
            CerrarVista();
            _mensaje = null;
            try
            {
                if (Puente()?.RodarEnPlay(_elegida.id, inicio) != true)
                    _mensaje = "No se pudo rodar en Play. Revisa la consola de Unity.";
            }
            catch (Exception e) { _mensaje = e.Message; }
            RepintarMesa();
        }

        private void Parar()
        {
            _reproduciendo = false;
            _musicaDePreviz = null;
            EditorAudioPreview.StopFondo();
            EditorAudioPreview.Stop();
            _vozSonando = null;
            _vozHasta = 0f;
        }

        private void IrA(float t)
        {
            if (_siguiendoPlay && ControlEnPlay() is ICinematicsPlayControl control)
            {
                float destino = Mathf.Clamp(t, 0f, Total());
                if (TramoEn(_t) != null || TramoEn(destino) != null)
                {
                    _mensaje = "Este tramo usa una cámara propia. Sal de Play y vuelve a rodar desde el punto elegido.";
                    RepintarMesa();
                    return;
                }

                _mensaje = null;
                control.Saltar(destino);
                RepintarMesa();
                return;
            }

            _t = Mathf.Clamp(t, 0f, Total());
            _vivoPedido = true;
            _vozSonando = null;
            _vozHasta = 0f;
            _musicaDePreviz = null;
            EditorAudioPreview.Stop();
            EditorAudioPreview.StopFondo();
            _tAnterior = _t;
            SincronizarPlano();
            RepintarMesa();
        }

        private void SincronizarPlano()
        {
            if (_detalle == null || _detalle.planos.Count == 0)
            {
                _plano = -1;
                SegundoActivo = _t;
                if (PlanoActivo != -1)
                {
                    PlanoActivo = -1;
                    SeleccionCambiada?.Invoke();
                }
                return;
            }
            int indice = 0;
            for (int i = 0; i < _detalle.planos.Count; i++)
                if (_detalle.planos[i].t0 <= _t + 0.0001f) indice = i;
            // En un tramo sin plano no se mira ningún plano: la IA no debe creer que se habla del P01.
            int activo = TramoEn(_t) != null ? -1 : indice;
            bool cambio = PlanoActivo != activo;
            _plano = indice;
            PlanoActivo = activo;
            SegundoActivo = _t;
            if (cambio) SeleccionCambiada?.Invoke();
        }

        private CinemaLine FraseActiva()
        {
            if (_detalle == null) return null;
            CinemaLine activa = null;
            foreach (var l in _detalle.lineas)
                if (l.t0 <= _t && _t < l.t1 && (activa == null || l.t0 >= activa.t0)) activa = l;
            return activa;
        }

        private void SonarLaFraseQueToca()
        {
            var frase = FraseActiva();
            if (frase == null)
            {
                if (_vozSonando != null)
                {
                    if (EditorAudioPreview.HaySegundoCanal || _musicaDePreviz == null)
                        EditorAudioPreview.Stop();
                    _vozSonando = null;
                    _vozHasta = 0f;
                }
                return;
            }
            if (frase.clave == _vozSonando) return;

            _vozSonando = frase.clave;
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null) return;

            AudioClip clip = null;
            try { clip = puente.VozDe(frase.clave); }
            catch { /* sin voz: el flipbook sigue mudo, no es un error */ }

            float desde = Mathf.Max(0f, _t - frase.t0);
            _vozHasta = clip != null ? Mathf.Min(frase.t1, frase.t0 + clip.length) : _t;
            if (_t < _vozHasta) EditorAudioPreview.Play(clip, desde);
        }

        // ── Pintura ──────────────────────────────────────────────────────────────────────────

        /// El fondo usa el canal del Editor que esté disponible. En un canal compartido vuelve a
        /// su posición del guion en cuanto termina la voz.
        private void SonarEfectosQueTocan()
        {
            if (_detalle == null) return;
            bool segundoCanal = EditorAudioPreview.HaySegundoCanal;
            if (!segundoCanal && _t < _vozHasta)
            {
                _musicaDePreviz = null;
                _tAnterior = _t;
                return;
            }

            CinemaClip musica = null;
            CinemaClip sonido = null;
            foreach (var pista in _detalle.pistas)
            {
                if (pista.tipo != CinemaTrackKind.Musica && pista.tipo != CinemaTrackKind.Sonido) continue;

                foreach (var clip in pista.clips)
                {
                    if (clip.t0 > _t) continue;
                    if (pista.tipo == CinemaTrackKind.Musica)
                    {
                        if (musica == null || clip.t0 >= musica.t0) musica = clip;
                    }
                    else if (clip.clip != null && clip.t0 > _tAnterior)
                    {
                        sonido = clip;
                    }
                }
            }
            if (musica != _musicaDePreviz)
            {
                _musicaDePreviz = musica;
                if (segundoCanal) EditorAudioPreview.StopFondo();
                else EditorAudioPreview.Stop();
                if (musica?.clip != null)
                {
                    float desde = _t - musica.t0 + musica.desdeSegundo;
                    if (musica.enBucle && musica.clip.length > 0f) desde %= musica.clip.length;
                    if (desde < musica.clip.length)
                    {
                        if (segundoCanal) EditorAudioPreview.PlayFondo(musica.clip, desde, musica.enBucle);
                        else EditorAudioPreview.Play(musica.clip, desde, musica.enBucle);
                    }
                }
            }
            // El fondo es un único canal. Los SFX se oyen ahí cuando el guion deja hueco.
            if (sonido != null && (musica?.clip == null ||
                !musica.enBucle && _t - musica.t0 + musica.desdeSegundo >= musica.clip.length))
            {
                if (segundoCanal) EditorAudioPreview.PlayFondo(sonido.clip, _t - sonido.t0);
                else EditorAudioPreview.Play(sonido.clip, _t - sonido.t0);
            }
            _tAnterior = _t;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("assistant-root");
            root.style.flexDirection = FlexDirection.Column;

            _ia ??= DirectorAssistant.Create();
            string hojaDeEstilo = _ia?.StyleSheetPath;
            var hoja = string.IsNullOrEmpty(hojaDeEstilo) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(hojaDeEstilo);
            if (hoja != null) root.styleSheets.Add(hoja);

            _mesaIMGUI = new IMGUIContainer(DibujarMesaCompleta) { style = { flexGrow = 1, flexShrink = 1 } };
            root.Add(_mesaIMGUI);

            root.Add(CrearCajaIA());
            AplicarPliegueIA();
        }

        // ── La caja de la IA ─────────────────────────────────────────────────────────────────

        /// El chat compartido del asistente, con el contexto de cinemáticas. Lo que ve la IA es el
        /// estado que da el puente: la cinemática, el plano y el segundo de la mesa, y las marcas.
        private VisualElement CrearCajaIA()
        {
            _cajaIA = new VisualElement();
            _cajaIA.style.flexShrink = 0;
            _cajaIA.style.borderTopWidth = 1;
            _cajaIA.style.borderTopColor = new Color(0.21f, 0.31f, 0.42f);
            _cajaIA.style.backgroundColor = new Color(0.14f, 0.19f, 0.24f);
            _cajaIA.style.paddingLeft = 8;
            _cajaIA.style.paddingRight = 8;
            _cajaIA.style.paddingTop = 4;
            _cajaIA.style.paddingBottom = 4;

            var cabecera = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var titulo = new Label("PARA LA IA");
            titulo.style.unityFontStyleAndWeight = FontStyle.Bold;
            titulo.style.color = new Color(0.66f, 0.77f, 0.87f);
            titulo.style.marginRight = 8;
            cabecera.Add(titulo);

            _chipsIA = new Label();
            _chipsIA.style.flexGrow = 1;
            _chipsIA.style.color = new Color(0.66f, 0.77f, 0.87f);
            cabecera.Add(_chipsIA);

            _conectarIA = new Button(() => _ia?.OpenConnect())
            {
                tooltip = "Elegir o cambiar la IA (tu cuenta de ChatGPT, Google o Claude).",
            };
            cabecera.Add(_conectarIA);
            ActualizarBotonDeIA();
            if (_ia != null)
            {
                _ia.SettingsChanged -= ActualizarBotonDeIA;
                _ia.SettingsChanged += ActualizarBotonDeIA;
            }

            _plegarIA = new Button(() =>
            {
                _iaPlegada = !_iaPlegada;
                EditorPrefs.SetBool(ClaveIaPlegada, _iaPlegada);
                AplicarPliegueIA();
            });
            cabecera.Add(_plegarIA);
            _cajaIA.Add(cabecera);

            _vistaIA = _ia?.CreateView(() => { RefrescarLista(); RepintarMesa(); }, RepintarMesa);
            if (_vistaIA != null)
            {
                var respuestas = _vistaIA.Q<ScrollView>();
                if (respuestas != null) respuestas.style.maxHeight = AltoRespuestasIA;
                _cajaIA.Add(_vistaIA);
            }
            else
            {
                _cajaIA.Add(new Label("Para hablar con la IA desde aquí, instala el paquete Liyodev Assistant (com.liyodev.assistant). El Director funciona entero sin él."));
            }
            return _cajaIA;
        }

        /// La mesa vive en un IMGUIContainer: hay que marcarlo para que vuelva a pintarse.
        private void RepintarMesa()
        {
            _mesaIMGUI?.MarkDirtyRepaint();
            Repaint();
        }

        private void AplicarPliegueIA()
        {
            if (_vistaIA != null) _vistaIA.style.display = _iaPlegada ? DisplayStyle.None : DisplayStyle.Flex;
            if (_plegarIA != null)
            {
                _plegarIA.text = _iaPlegada ? "Abrir" : "Plegar";
                _plegarIA.tooltip = _iaPlegada ? "Abrir la caja de la IA" : "Plegar la caja de la IA";
            }
        }

        /// El botón dice qué IA está conectada; sin ninguna, invita a conectarla.
        private void ActualizarBotonDeIA()
        {
            if (_conectarIA == null) return;
            _conectarIA.style.display = _ia != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_ia != null) _conectarIA.text = _ia.ConnectLabel;
        }

        /// La línea de contexto de la caja: lo que la IA sabe que se está mirando.
        private void ActualizarChipsIA()
        {
            if (_chipsIA == null) return;
            string texto;
            if (_elegida == null || _detalle == null)
            {
                texto = "elige una cinemática";
            }
            else if (_detalle.planos.Count == 0)
            {
                texto = $"{_elegida.nombre} · {Reloj(_t)}";
            }
            else
            {
                var tramoActual = TramoEn(_t);
                string panel = tramoActual != null ? PanelDeTramo(tramoActual) : Panel(_plano);
                int senaladas = 0, direcciones = 0, dibujos = 0, notas = 0;
                if (_pizarra != null)
                    foreach (var nota in _pizarra.notas)
                    {
                        if (nota.panel != panel) continue;
                        if (!string.IsNullOrEmpty(nota.texto)) notas++;
                        foreach (var trazo in nota.trazos)
                        {
                            if (trazo.tipo == CinemaTrazo.Punto) senaladas++;
                            else if (trazo.tipo == CinemaTrazo.Flecha) direcciones++;
                            else dibujos++;
                        }
                    }

                texto = tramoActual != null ? $"efecto ({panel}) · {Reloj(_t)}" : $"{panel} · {Reloj(_t)}";
                if (senaladas > 0) texto += $" · {senaladas} señalado(s)";
                if (direcciones > 0) texto += $" · {direcciones} dirección(es)";
                if (dibujos > 0) texto += $" · {dibujos} dibujo(s)";
                if (notas > 0) texto += $" · {notas} nota(s)";
            }
            if (_ia != null && _ia.Busy) texto += " · la IA está trabajando…";
            if (_chipsIA.text != texto) _chipsIA.text = texto;
        }

        // ── La mesa ──────────────────────────────────────────────────────────────────────────

        private void DibujarMesaCompleta()
        {
            if (_mesaIMGUI != null) _area = _mesaIMGUI.contentRect;
            InitEstilos();
            DibujarBarra();

            if (CinematicsEditorServices.Bridge == null)
            {
                EditorGUILayout.HelpBox(
                    _mensaje ?? "Este proyecto no tiene puente de cinemáticas: añade un " +
                    "ICinematicsEditorBridge en su carpeta de Connections.", MessageType.Warning);
                return;
            }

            if (_elegida == null)
            {
                DibujarLista();
                ActualizarChipsIA();
                return;
            }

            _anchoCentro = Mathf.Max(360f, _area.width - AnchoPlanos - AnchoMarcas - 16f);

            EditorGUILayout.BeginHorizontal();
            DibujarColumnaDePlanos();
            DibujarMesa();
            DibujarColumnaDeMarcas();
            EditorGUILayout.EndHorizontal();

            ActualizarChipsIA();
        }

        private void DibujarBarra()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Director", EditorStyles.boldLabel, GUILayout.Width(60));

            if (_elegida != null)
            {
                if (GUILayout.Button(_elegida.nombre + "  ▾", EditorStyles.toolbarDropDown, GUILayout.Width(260)))
                    MenuDeCinematicas();
                GUILayout.Label(
                    $"{_elegida.escena} · {Reloj(Total())} · {(_detalle != null ? _detalle.planos.Count : _elegida.planos)} planos · " +
                    $"{(_detalle != null ? _detalle.lineas.Count : _elegida.frases)} frases · {_elegida.estado}",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                _busqueda = GUILayout.TextField(_busqueda, EditorStyles.toolbarSearchField, GUILayout.Width(240));
                if (EditorGUI.EndChangeCheck()) Filtrar();
                GUILayout.Label($"{_entradas.Count} cinemáticas · pincha una para abrirla", EditorStyles.miniLabel);

                var anterior = string.IsNullOrEmpty(_ultimaElegidaId) ? null : _entradas.Find(e => e.id == _ultimaElegidaId);
                if (anterior != null && GUILayout.Button("← Volver a " + anterior.nombre, EditorStyles.toolbarButton))
                    Seleccionar(anterior);
            }

            if (GUILayout.Button("Refrescar", EditorStyles.toolbarButton, GUILayout.Width(70))) RefrescarLista();

            GUILayout.FlexibleSpace();

            if (!EditorAudioPreview.Disponible)
                GUILayout.Label("sin audio de previz en este Editor", EditorStyles.miniLabel);
            else if (!EditorAudioPreview.HaySegundoCanal)
                GUILayout.Label(new GUIContent("Audio de un canal", "La voz tiene prioridad. La mezcla completa se oye en Play."),
                    EditorStyles.miniLabel);

            if (_elegida != null)
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_elegida.rutaGuion)))
                    if (GUILayout.Button("Guion", EditorStyles.toolbarButton, GUILayout.Width(52)))
                        Ejecutar(() => Puente().AbrirGuion(_elegida.id));
                if (GUILayout.Button("Acciones", EditorStyles.toolbarDropDown, GUILayout.Width(84)))
                    MenuDeAcciones();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(new GUIContent("▶ Rodar en Play", "Arranca desde el cabezal en la ventana Game."),
                            EditorStyles.toolbarButton, GUILayout.Width(110)))
                        RodarDesdeElCabezal();
            }
            else if (GUILayout.Button("Nueva cinemática…", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                NuevaCinematicaWindow.Abrir();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// Cambiar de cinemática sin volver a la lista: por escena, con la elegida marcada.
        private void MenuDeCinematicas()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Ver la lista"), false, () =>
            {
                _ultimaElegidaId = _elegida?.id;
                Parar();
                CerrarVista();
                _elegida = null;
                _detalle = null;
                CinematicaActivaId = null;
                PlanoActivo = -1;
                SegundoActivo = -1f;
                SeleccionCambiada?.Invoke();
                RepintarMesa();
            });
            menu.AddSeparator(string.Empty);
            foreach (var e in _entradas)
            {
                var entrada = e;
                string escena = string.IsNullOrEmpty(e.escena) ? "(sin escena)" : e.escena;
                menu.AddItem(new GUIContent($"{escena}/{e.nombre}"), _elegida != null && _elegida.id == e.id,
                    () => { Seleccionar(entrada); RepintarMesa(); });
            }
            menu.ShowAsContext();
        }

        private void MenuDeAcciones()
        {
            var menu = new GenericMenu();
            string id = _elegida.id;
            bool hayGuion = !string.IsNullOrEmpty(_elegida.rutaGuion);
            bool sePuedeHornear = hayGuion && !EditorApplication.isPlayingOrWillChangePlaymode;
            Agregar("Actualizar cinemática", sePuedeHornear, () => Puente().Hornear(id, true));
            menu.AddSeparator(string.Empty);
            Agregar("Exportar storyboard detallado…", sePuedeHornear, () => Exportar(id, true));
            Agregar("Exportar guion…", hayGuion, () => Exportar(id, false));
            menu.ShowAsContext();

            void Agregar(string texto, bool disponible, System.Action accion)
            {
                var contenido = new GUIContent(texto);
                if (disponible) menu.AddItem(contenido, false, () => Ejecutar(accion));
                else menu.AddDisabledItem(contenido);
            }
        }

        private void Exportar(string id, bool storyboard)
        {
            string ruta = EditorUtility.SaveFilePanel(storyboard ? "Exportar storyboard" : "Exportar guion",
                string.Empty, storyboard ? id + "-storyboard" : id + ".guion", storyboard ? "zip" : "txt");
            if (string.IsNullOrEmpty(ruta)) return;

            if (storyboard) Puente().ExportarStoryboard(id, ruta);
            else Puente().ExportarGuion(id, ruta);
            _mensaje = "Exportado: " + Path.GetFileName(ruta);
        }

        private void DibujarLista()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _scrollLista = EditorGUILayout.BeginScrollView(_scrollLista, false, false);

            foreach (var e in _visibles)
            {
                var fila = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
                bool sel = _elegida != null && _elegida.id == e.id;
                var aviso = ColorDeAviso(e);
                if (Event.current.type == EventType.Repaint)
                {
                    if (sel)
                    {
                        var fondo = GUI.skin.settings.selectionColor;
                        fondo.a *= 0.55f;
                        EditorGUI.DrawRect(fila, fondo);
                    }

                    // Rayita de aviso solo a la izquierda, y solo cuando hay algo que mirar.
                    if (aviso.a > 0f)
                        EditorGUI.DrawRect(new Rect(fila.x, fila.y + 4f, 2f, fila.height - 9f), aviso);

                    var linea = EditorGUIUtility.isProSkin
                        ? new Color(1f, 1f, 1f, 0.06f)
                        : new Color(0f, 0f, 0f, 0.1f);
                    EditorGUI.DrawRect(new Rect(fila.x, fila.yMax - 1f, fila.width, 1f), linea);
                }

                GUI.Label(new Rect(fila.x + 9f, fila.y + 4f, fila.width - 18f, 21f), e.nombre,
                    sel ? _estiloNombreElegido : _estiloNombreFila);
                GUI.Label(new Rect(fila.x + 9f, fila.y + 25f, fila.width - 104f, 17f),
                    $"{e.escena} · {Reloj(e.duracion)} · {e.planos} planos · {e.frases} frases", _estiloMetaFila);

                _estiloInsignia.normal.textColor = aviso.a > 0f ? aviso : EditorStyles.miniLabel.normal.textColor;
                GUI.Label(new Rect(fila.xMax - 96f, fila.y + 25f, 88f, 17f), Insignia(e), _estiloInsignia);

                if (Event.current.type == EventType.MouseDown && fila.Contains(Event.current.mousePosition))
                {
                    Seleccionar(e);
                    Event.current.Use();
                    RepintarMesa();
                }
            }

            if (_visibles.Count == 0)
                EditorGUILayout.HelpBox(_entradas.Count == 0
                    ? "No hay guiones de cinemática en este proyecto."
                    : "Ninguna coincide con la búsqueda.", MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DibujarMesa()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_anchoCentro));

            DibujarCabecera();
            if (_detalle == null)
            {
                EditorGUILayout.HelpBox(_mensaje ?? "No se pudo leer esta cinemática.", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            DibujarVisor();
            DibujarTransporte();
            DibujarAvisos();

            EditorGUILayout.EndVertical();
        }

        private void DibujarCabecera()
        {
            if (!string.IsNullOrEmpty(_mensaje))
                EditorGUILayout.HelpBox(_mensaje, MessageType.None);
        }

        private void DibujarVisor()
        {
            // Lo que queda tras la barra, el transporte y el pie de avisos.
            float ancho = Mathf.Max(320f, _anchoCentro);
            float alto = Mathf.Clamp(_area.height - 96f, 160f, ancho * 9f / 16f);
            var marco = GUILayoutUtility.GetRect(ancho, alto, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(marco, new Color(0.06f, 0.07f, 0.09f));
            if (_siguiendoPlay)
            {
                GUI.Label(marco, "La cinemática se está viendo en la ventana Game.", _estiloTramo);
                return;
            }

            if (_plano < 0 && _detalle.planos.Count > 0) _plano = 0;
            var plano = (_plano >= 0 && _plano < _detalle.planos.Count) ? _detalle.planos[_plano] : null;

            var tramo = TramoEn(_t);
            if (tramo != null)
            {
                DibujarTramoSinPlano(marco, tramo);
                return;
            }

            if (VistaEnVivoActiva())
            {
                DibujarVisorEnVivo(marco, plano);
                return;
            }

            var textura = plano != null ? TexturaDelPlano(plano) : null;

            if (textura == null)
            {
                GUI.Label(marco, _detalle.planos.Count == 0
                    ? "Sin planos horneados."
                    : "Sin fotograma de este plano: hornea con storyboard para verlo aquí.",
                    _estiloSobre);
            }
            else
            {
                var imagen = Encajar(marco, textura.width / (float)textura.height);
                GUI.DrawTexture(imagen, textura, ScaleMode.ScaleToFit, false);

                if (_detalle.subtitulos) DibujarBandas(imagen);
                DibujarSubtitulo(imagen);
                DibujarPizarraSobre(imagen, plano);

                var etiqueta = new Rect(imagen.x + 8f, imagen.y + 6f, imagen.width - 16f, 18f);
                GUI.Label(etiqueta, $"P{_plano + 1:00} · {plano.descripcion}", _estiloSobre);

                var reloj = new Rect(imagen.x, imagen.y + 6f, imagen.width - 8f, 18f);
                _estiloSobre.alignment = TextAnchor.UpperRight;
                GUI.Label(reloj, $"{Reloj(_t)} / {Reloj(Total())} · {plano.origen}", _estiloSobre);
                _estiloSobre.alignment = TextAnchor.UpperLeft;
            }
        }

        // ── En PlayMode ──────────────────────────────────────────────────────────────────────

        private static ICinematicsPlayControl ControlEnPlay() =>
            EditorApplication.isPlaying ? CinematicsEditorServices.Bridge as ICinematicsPlayControl : null;

        /// En Play, la mesa enseña la cinemática que rueda de verdad: la elige si no lo estaba y el
        /// cabezal va con ella. Sus voces y su música suenan en el juego, no en la mesa.
        private bool SeguirLaDePlay()
        {
            var control = ControlEnPlay();
            if (control == null || !control.EnMarcha(out string id, out float segundo, out _, out bool pausada))
            {
                if (_siguiendoPlay) { _siguiendoPlay = false; RepintarMesa(); }
                return false;
            }

            if (!_siguiendoPlay) Parar();
            _siguiendoPlay = true;
            _pausadaEnPlay = pausada;

            if (_elegida == null || _elegida.id != id)
            {
                var entrada = _entradas.Find(e => e.id == id);
                if (entrada != null) Seleccionar(entrada);
            }

            if (Mathf.Abs(segundo - _t) > 0.0001f)
            {
                _t = segundo;
                SincronizarPlano();
                RepintarMesa();
            }
            return true;
        }

        // ── Vista en vivo ────────────────────────────────────────────────────────────────────

        /// El puente sabe abrir cinemáticas en vivo y el Editor no está en Play.
        private bool PuedeVerseEnVivo() =>
            CinematicsEditorServices.Bridge is ICinematicsLivePreview && !EditorApplication.isPlayingOrWillChangePlaymode;

        private bool VistaEnVivoActiva() =>
            _enVivo && _vivoPedido && _detalle != null && _detalle.planos.Count > 0 && PuedeVerseEnVivo();

        private void DibujarVisorEnVivo(Rect marco, CinemaShot plano)
        {
            var imagen = Encajar(marco, 16f / 9f);
            float ppp = EditorGUIUtility.pixelsPerPoint;
            int ancho = Mathf.Clamp(Mathf.RoundToInt(imagen.width * ppp), 64, AnchoMaximoEnVivo);
            _tamanoVivo = new Vector2Int(ancho, Mathf.Max(36, Mathf.RoundToInt(ancho * 9f / 16f)));

            if (_rtVivo != null && _vivoRendido)
                GUI.DrawTexture(imagen, _rtVivo, ScaleMode.ScaleToFit, false);
            else
                GUI.Label(imagen, string.IsNullOrEmpty(_errorVista) ? "Colocando la escena…" : _errorVista, _estiloSobre);

            if (_detalle.subtitulos) DibujarBandas(imagen);
            DibujarSubtitulo(imagen);
            if (plano != null) DibujarPizarraSobre(imagen, plano);

            var etiqueta = new Rect(imagen.x + 8f, imagen.y + 6f, imagen.width - 16f, 18f);
            if (plano != null) GUI.Label(etiqueta, $"P{_plano + 1:00} · {plano.descripcion}", _estiloSobre);

            var reloj = new Rect(imagen.x, imagen.y + 6f, imagen.width - 8f, 18f);
            _estiloSobre.alignment = TextAnchor.UpperRight;
            GUI.Label(reloj, $"{Reloj(_t)} / {Reloj(Total())} · en vivo", _estiloSobre);
            _estiloSobre.alignment = TextAnchor.UpperLeft;

            if (!string.IsNullOrEmpty(_errorVista) && _vivoRendido)
            {
                var aviso = new Rect(imagen.x + 8f, imagen.yMax - 22f, imagen.width - 16f, 18f);
                GUI.Label(aviso, _errorVista, _estiloSobre);
            }
        }

        /// Fuera de OnGUI: abre la vista si hace falta y vuelve a rendir cuando el cabezal se ha
        /// movido (como mucho 30 veces por segundo mientras se reproduce).
        private void ActualizarVistaEnVivo()
        {
            if (!VistaEnVivoActiva())
            {
                if (_vista != null) CerrarVista();
                return;
            }

            if (TramoEn(_t) != null) return;

            bool cambioDeTamano = _rtVivo == null || _rtVivo.width != _tamanoVivo.x || _rtVivo.height != _tamanoVivo.y;
            bool cabezalMovido = Mathf.Abs(_t - _tRendido) > 0.0001f;
            if (!cambioDeTamano && !cabezalMovido) return;

            double ahora = EditorApplication.timeSinceStartup;
            if (_reproduciendo && !cambioDeTamano && ahora - _ultimoRender < IntervaloEnVivo) return;

            if (_vista == null)
            {
                if (!string.IsNullOrEmpty(_errorVista) && _tRendido >= 0f) return;
                AbrirVista();
                if (_vista == null) { _tRendido = _t; RepintarMesa(); return; }
            }

            if (cambioDeTamano) CrearTexturaEnVivo();

            try
            {
                bool hecho = _vista.Rendir(_t, _rtVivo);
                _errorVista = hecho ? null : _vista.Error;
                _vivoRendido |= hecho;
            }
            catch (Exception e)
            {
                CerrarVista();
                _errorVista = "La vista en vivo falló: " + e.Message;
            }

            _tRendido = _t;
            _ultimoRender = ahora;
            RepintarMesa();
        }

        private void AbrirVista()
        {
            _errorVista = null;
            if (!(CinematicsEditorServices.Bridge is ICinematicsLivePreview vivo) || _elegida == null) return;

            try
            {
                _vista = vivo.AbrirVistaEnVivo(_elegida.id, out string error);
                if (_vista == null) _errorVista = string.IsNullOrEmpty(error) ? "No se puede ver en vivo." : error;
            }
            catch (Exception e)
            {
                _vista = null;
                _errorVista = "No se pudo abrir la vista en vivo: " + e.Message;
            }
        }

        /// Suelta la escena y libera la vista. La siguiente vez que haga falta se vuelve a abrir.
        private void CerrarVista()
        {
            if (_vista != null)
            {
                try { _vista.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
                _vista = null;
            }
            _errorVista = null;
            _tRendido = -1f;
            _vivoRendido = false;
        }

        private void AlCambiarElModoDeJuego(PlayModeStateChange cambio)
        {
            // La escena no puede entrar en Play colocada: se suelta antes.
            if (cambio == PlayModeStateChange.ExitingEditMode)
            {
                Parar();
                CerrarVista();
            }
            RepintarMesa();
        }

        private void CrearTexturaEnVivo()
        {
            LiberarTexturaEnVivo();
            _rtVivo = new RenderTexture(_tamanoVivo.x, _tamanoVivo.y, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                hideFlags = HideFlags.HideAndDontSave,
                name = "Director · en vivo",
            };
            _rtVivo.Create();
            _vivoRendido = false;
        }

        private void LiberarTexturaEnVivo()
        {
            if (_rtVivo == null) return;
            _rtVivo.Release();
            DestroyImmediate(_rtVivo);
            _rtVivo = null;
        }

        private void DibujarBandas(Rect imagen)
        {
            float alto = imagen.height * 0.13f;
            EditorGUI.DrawRect(new Rect(imagen.x, imagen.y, imagen.width, alto), Color.black);
            EditorGUI.DrawRect(new Rect(imagen.x, imagen.yMax - alto, imagen.width, alto), Color.black);
        }

        private void DibujarSubtitulo(Rect imagen)
        {
            var frase = FraseActiva();
            if (frase == null || string.IsNullOrEmpty(frase.texto)) return;

            string texto = frase.deFueraDeEscena && !string.IsNullOrEmpty(frase.quien)
                ? $"{frase.quien}: {frase.texto}"
                : frase.texto;

            float alto = imagen.height * 0.13f;
            var caja = new Rect(imagen.x + imagen.width * 0.1f, imagen.yMax - alto + 2f,
                                imagen.width * 0.8f, alto - 4f);
            GUI.Label(caja, texto, _estiloSubtitulo);
        }

        /// La columna de la izquierda: los planos, de arriba abajo. Pinchar uno lleva el cabezal a su
        /// principio.
        private void DibujarColumnaDePlanos()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(AnchoPlanos));
            GUILayout.Label("PLANOS", _estiloSeccion);

            _scrollPlanos = EditorGUILayout.BeginScrollView(_scrollPlanos, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);

            if (_detalle != null)
            {
                float ancho = AnchoPlanos - 22f;
                float altoFoto = ancho * 9f / 16f;
                int tramo = 0;
                for (int i = 0; i < _detalle.planos.Count; i++)
                {
                    var p = _detalle.planos[i];
                    while (tramo < _tramosSinPlano.Count && _tramosSinPlano[tramo].t0 < p.t0)
                        FichaDeTramo(_tramosSinPlano[tramo++], ancho);

                    var fila = GUILayoutUtility.GetRect(ancho, altoFoto + 24f, GUILayout.Width(ancho));
                    var foto = new Rect(fila.x, fila.y, ancho, altoFoto);

                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(foto, new Color(0.1f, 0.11f, 0.13f));
                        var textura = TexturaDe(p.imagen);
                        if (textura != null) GUI.DrawTexture(foto, textura, ScaleMode.ScaleAndCrop, false);
                        else GUI.Label(foto, "sin foto", _estiloSobre);

                        if (i == _plano) Marco(foto, new Color(1f, 0.8f, 0.3f), 2f);

                        var etiqueta = new Rect(fila.x, foto.yMax + 3f, ancho - 40f, 16f);
                        GUI.Label(etiqueta, $"P{i + 1:00} · {p.descripcion}", i == _plano ? _estiloNombreElegido : _estiloMetaFila);
                        var reloj = new Rect(fila.xMax - 40f, foto.yMax + 3f, 40f, 16f);
                        _estiloInsignia.normal.textColor = EditorStyles.miniLabel.normal.textColor;
                        GUI.Label(reloj, Reloj(p.t0), _estiloInsignia);
                    }
                    else if (Event.current.type == EventType.MouseDown && fila.Contains(Event.current.mousePosition))
                    {
                        Parar();
                        IrA(p.t0);
                        Event.current.Use();
                    }
                }
                while (tramo < _tramosSinPlano.Count) FichaDeTramo(_tramosSinPlano[tramo++], ancho);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// Un tramo sin plano en la columna: una ficha oscura con lo que lo monta.
        private void FichaDeTramo(TramoSinPlano tramo, float ancho)
        {
            var fila = GUILayoutUtility.GetRect(ancho, 58f, GUILayout.Width(ancho));
            var caja = new Rect(fila.x, fila.y, fila.width, fila.height - 6f);
            bool dentro = _t >= tramo.t0 && _t < tramo.t1;
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(caja, new Color(0.11f, 0.12f, 0.16f));
                if (dentro) Marco(caja, new Color(1f, 0.8f, 0.3f), 2f);
                GUI.Label(new Rect(caja.x + 6f, caja.y + 3f, caja.width - 12f, 16f),
                    $"Efecto · {Reloj(tramo.t0)}–{Reloj(tramo.t1)}", _estiloMetaFila);
                GUI.Label(new Rect(caja.x + 6f, caja.y + 19f, caja.width - 12f, 30f),
                    "Solo en Play: " + tramo.que.Replace('\n', ' '), EditorStyles.wordWrappedMiniLabel);
            }
            else if (Event.current.type == EventType.MouseDown && caja.Contains(Event.current.mousePosition))
            {
                Parar();
                IrA(tramo.t0);
                Event.current.Use();
            }
        }

        private static void Marco(Rect r, Color color, float grosor)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, grosor), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - grosor, r.width, grosor), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, grosor, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - grosor, r.y, grosor, r.height), color);
        }

        /// La columna de la derecha: las marcas para la IA. No cambian nada por sí solas; son el
        /// contexto de lo que se le pide en la caja de abajo (sobre quién, dónde y en qué segundo).
        private void DibujarColumnaDeMarcas()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(AnchoMarcas));
            GUILayout.Label("MARCAS PARA LA IA", _estiloSeccion);

            if (_detalle == null || _detalle.planos.Count == 0)
            {
                GUILayout.Label("Sin planos horneados.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            var plano = _detalle.planos[Mathf.Clamp(_plano, 0, _detalle.planos.Count - 1)];
            var tramo = TramoEn(_t);
            string panel = tramo != null ? PanelDeTramo(tramo) : Panel(_plano);
            string elemento = tramo != null ? ElementoEfecto : _elementoElegido;

            if (tramo != null)
            {
                GUILayout.Label("Sobre el efecto de este trozo. No hay imagen que marcar: escribe lo que quieres cambiar.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                GUILayout.Label("Sobre", EditorStyles.miniBoldLabel);
                DibujarChipsDeElementos(plano);

                GUILayout.Space(6f);
                GUILayout.Label("En la imagen", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                BotonDeHerramienta(Herramienta.Ver, "Ver", "Los clics en la imagen no dejan marca.");
                BotonDeHerramienta(Herramienta.Senalar, "Señalar", "Un clic marca un punto: «aquí».");
                BotonDeHerramienta(Herramienta.Direccion, "Dirección", "Arrastra: de dónde a dónde.");
                BotonDeHerramienta(Herramienta.Dibujar, "Dibujar", "Dibuja a mano: una forma, un efecto, un camino.");
                EditorGUILayout.EndHorizontal();
            }
            if (tramo == null && _herramienta != Herramienta.Ver)
                GUILayout.Label(_herramienta == Herramienta.Senalar
                        ? "Pincha en la imagen para señalar."
                        : _herramienta == Herramienta.Direccion
                            ? "Arrastra en la imagen de dónde a dónde."
                            : "Dibuja en la imagen.",
                    EditorStyles.wordWrappedMiniLabel);

            GUILayout.Space(6f);
            GUILayout.Label($"Nota en {Reloj(_t)}", EditorStyles.miniBoldLabel);
            _textoNota = EditorGUILayout.TextArea(_textoNota, _estiloNotaTexto, GUILayout.Height(44f));
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_textoNota)))
                if (GUILayout.Button("Añadir nota", EditorStyles.miniButton))
                {
                    var nota = NotaDe(panel, elemento);
                    nota.texto = string.IsNullOrEmpty(nota.texto) ? _textoNota.Trim() : nota.texto + " · " + _textoNota.Trim();
                    if (nota.t < 0f) nota.t = _t;
                    _textoNota = string.Empty;
                    GUI.FocusControl(null);
                    GuardarPizarra();
                }

            GUILayout.Space(6f);
            var notas = _pizarra != null ? _pizarra.notas.FindAll(n => n.panel == panel) : new List<CinemaNotaPizarra>();
            GUILayout.Label(notas.Count > 0 ? $"En este plano ({notas.Count})" : "En este plano: nada", EditorStyles.miniBoldLabel);
            _scrollMarcas = EditorGUILayout.BeginScrollView(_scrollMarcas, GUILayout.ExpandHeight(true));
            foreach (var nota in notas.ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                string cuando = nota.t >= 0f ? $" · {Reloj(nota.t)}" : string.Empty;
                GUILayout.Label($"{EtiquetaDe(plano, nota.elemento)}{cuando}: {Recorta(ResumenDe(nota), 80)}",
                    EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(new GUIContent("×", "Quitar"), EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    _pizarra.notas.Remove(nota);
                    GuardarPizarra();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (_encargos.Count > 0 || _verEncargos)
            {
                _verEncargos = EditorGUILayout.Foldout(_verEncargos, $"Encargos pendientes ({_encargos.Count})", true);
                if (_verEncargos) DibujarEncargos();
            }

            if (_ia != null && _ia.Busy)
                GUILayout.Label("La IA está preparando la propuesta…", EditorStyles.wordWrappedMiniLabel);
            else if (_encargos.Count > 0 && _ia != null && _vistaIA != null)
            {
                if (GUILayout.Button("Pedir propuesta a la IA", EditorStyles.miniButton))
                {
                    if (_iaPlegada) { _iaPlegada = false; EditorPrefs.SetBool(ClaveIaPlegada, false); AplicarPliegueIA(); }
                    _ia.SendQuickAction();
                }
                GUILayout.Label("Revisa la propuesta y pulsa Aplicar. Los encargos se cierran al guardar el cambio.",
                    EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUI.DisabledScope(_pizarra == null || _pizarra.notas.Count == 0))
                if (GUILayout.Button(new GUIContent("Pasar las notas a encargos",
                        "Lo que el juego sabe aplicar solo se aplica; el resto queda en la cola de la IA."), EditorStyles.miniButton))
                    AplicarLaPizarra();

            GUILayout.Label("Las marcas no cambian nada: dicen a la IA sobre quién, dónde y en qué segundo. Escribe abajo lo que quieres.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DibujarEncargos()
        {
            CinemaEncargo cerrar = null;
            _scrollEncargos = EditorGUILayout.BeginScrollView(_scrollEncargos, GUILayout.MaxHeight(165));
            foreach (var encargo in _encargos)
            {
                if (GUILayout.Button($"{encargo.panel} · {Reloj(encargo.t)} · {encargo.etiquetaElemento}", EditorStyles.miniButton))
                    IrA(encargo.t);
                GUILayout.Label(encargo.texto, EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUI.DisabledScope(_ia?.Busy == true))
                    if (GUILayout.Button("Marcar como resuelto", EditorStyles.miniButton)) cerrar = encargo;
                GUILayout.Space(5);
            }
            EditorGUILayout.EndScrollView();

            if (cerrar != null && EditorUtility.DisplayDialog("Cerrar encargo", cerrar.texto, "Marcar como resuelto", "Cancelar"))
                Ejecutar(() => _mensaje = Puente().CerrarEncargo(_elegida.id, cerrar.clave));
        }

        private void BotonDeHerramienta(Herramienta herramienta, string texto, string ayuda)
        {
            bool elegida = _herramienta == herramienta;
            if (GUILayout.Toggle(elegida, new GUIContent(texto, ayuda), EditorStyles.miniButton) && !elegida)
            {
                _herramienta = herramienta;
                _trazoEnCurso = null;
            }
        }

        /// Los elementos del plano como fichas que saltan de línea cuando no caben.
        private void DibujarChipsDeElementos(CinemaShot plano)
        {
            float anchoDisponible = AnchoMarcas - 8f;
            float usado = 0f;
            EditorGUILayout.BeginHorizontal();
            foreach (var elemento in plano.elementos)
            {
                bool elegido = elemento.id == _elementoElegido;
                var estilo = elegido ? _estiloChipElegido : _estiloChip;
                var contenido = new GUIContent(elemento.etiqueta);
                float ancho = estilo.CalcSize(contenido).x + 4f;
                if (usado > 0f && usado + ancho > anchoDisponible)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    usado = 0f;
                }
                if (GUILayout.Button(contenido, estilo, GUILayout.Height(19f))) _elementoElegido = elemento.id;
                usado += ancho;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static string ResumenDe(CinemaNotaPizarra nota)
        {
            string texto = nota.Describir();
            return string.IsNullOrEmpty(texto) ? "(vacía)" : texto;
        }

        private static string Panel(int indice) => $"P{indice + 1:00}";

        private static string Recorta(string texto, int largo)
            => string.IsNullOrEmpty(texto) || texto.Length <= largo ? texto : texto.Substring(0, largo).TrimEnd() + "…";

        private static string EtiquetaDe(CinemaShot plano, string id)
            => plano.elementos.Find(e => e.id == id)?.etiqueta ?? id;

        /// La nota de ese plano y ese elemento; la crea si no está.
        private CinemaNotaPizarra NotaDe(string panel, string elemento)
        {
            var nota = _pizarra.notas.Find(n => n.panel == panel && n.elemento == elemento);
            if (nota == null)
            {
                nota = new CinemaNotaPizarra { panel = panel, elemento = elemento };
                _pizarra.notas.Add(nota);
            }
            return nota;
        }

        private void GuardarPizarra()
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null || _pizarra == null) return;
            try { puente.GuardarPizarra(_elegida.id, _pizarra); }
            catch (System.Exception e) { _mensaje = "No pude guardar la pizarra: " + e.Message; }
            RepintarMesa();
        }

        private void AplicarLaPizarra()
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null || _pizarra == null) return;

            var notas = new List<CinemaNota>();
            var enviadas = new Dictionary<CinemaNotaPizarra, string>();
            foreach (var nota in _pizarra.notas)
            {
                string texto = nota.Describir();
                if (string.IsNullOrEmpty(texto)) continue;
                enviadas[nota] = texto;
                notas.Add(new CinemaNota
                {
                    panel = nota.panel,
                    elemento = nota.elemento,
                    tipo = string.IsNullOrEmpty(nota.texto) ? "trazo" : "nota",
                    texto = texto,
                    t = nota.t,
                });
            }

            if (notas.Count == 0) { _mensaje = "La pizarra no tiene notas con texto que aplicar."; return; }

            string resumen = string.Empty;
            CinemaAplicado resultado = null;
            try { resultado = puente.AplicarRevisiones(_elegida.id, notas); }
            catch (System.Exception e) { _mensaje = e.Message; }

            if (resultado != null)
            {
                _mensaje = resultado.resumen;

                // Las que se aplicaron desaparecen de la pizarra: que se vayan es el acuse.
                if (resultado.claves != null && resultado.claves.Count > 0)
                {
                    int antes = _pizarra.notas.Count;
                    _pizarra.notas.RemoveAll(n => enviadas.TryGetValue(n, out string texto)
                                                  && resultado.claves.Contains(ClaveDeNota(n.panel, n.elemento, texto)));
                    if (_pizarra.notas.Count != antes) GuardarPizarra();
                }
            }

            RefrescarLista();
        }

        /// La misma huella que usa el proyecto para saber si una nota ya se aplicó.
        private static string ClaveDeNota(string panel, string elemento, string texto)
            => CinemaNota.CalcularClave(panel, elemento, texto);

        /// Pinta lo dibujado sobre el fotograma y recoge el trazo nuevo cuando está el lápiz.
        private void DibujarPizarraSobre(Rect imagen, CinemaShot plano)
        {
            if (_pizarra == null) return;
            string panel = Panel(_plano);

            var previo = Handles.matrix;
            Handles.BeginGUI();
            foreach (var nota in _pizarra.notas)
            {
                if (nota.panel != panel) continue;
                Handles.color = ColorDeElemento(nota.elemento);
                foreach (var trazo in nota.trazos) PintarTrazo(trazo, imagen);
            }
            if (_trazoEnCurso != null && _trazoEnCurso.puntos.Count > 0)
            {
                Handles.color = ColorDeElemento(_elementoElegido);
                PintarTrazo(_trazoEnCurso, imagen);
            }
            Handles.EndGUI();
            Handles.matrix = previo;

            if (_herramienta == Herramienta.Ver) return;

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && imagen.Contains(e.mousePosition))
            {
                Parar();
                Vector2 punto = Normalizado(e.mousePosition, imagen);
                if (_herramienta == Herramienta.Senalar)
                {
                    var marca = new CinemaTrazo { tipo = CinemaTrazo.Punto, t = _t };
                    marca.puntos.Add(punto);
                    NotaDe(panel, _elementoElegido).trazos.Add(marca);
                    GuardarPizarra();
                }
                else
                {
                    _trazoEnCurso = new CinemaTrazo
                    {
                        tipo = _herramienta == Herramienta.Direccion ? CinemaTrazo.Flecha : CinemaTrazo.Trazo,
                        t = _t,
                    };
                    _trazoEnCurso.puntos.Add(punto);
                    if (_herramienta == Herramienta.Direccion) _trazoEnCurso.puntos.Add(punto);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _trazoEnCurso != null)
            {
                Vector2 punto = Normalizado(e.mousePosition, imagen);
                if (_trazoEnCurso.tipo == CinemaTrazo.Flecha) _trazoEnCurso.puntos[1] = punto;
                else _trazoEnCurso.puntos.Add(punto);
                RepintarMesa();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _trazoEnCurso != null)
            {
                bool vale = _trazoEnCurso.puntos.Count > 1
                            && (_trazoEnCurso.tipo != CinemaTrazo.Flecha
                                || (_trazoEnCurso.puntos[1] - _trazoEnCurso.puntos[0]).sqrMagnitude > 0.0004f);
                if (vale)
                {
                    NotaDe(panel, _elementoElegido).trazos.Add(_trazoEnCurso);
                    GuardarPizarra();
                }
                _trazoEnCurso = null;
                e.Use();
            }

            if (imagen.Contains(e.mousePosition))
                EditorGUIUtility.AddCursorRect(imagen, MouseCursor.ArrowPlus);
        }

        private static void PintarTrazo(CinemaTrazo trazo, Rect imagen)
        {
            if (trazo.puntos.Count == 0) return;

            Vector3 EnPantalla(Vector2 p) => new Vector3(imagen.x + p.x * imagen.width, imagen.y + p.y * imagen.height, 0f);

            if (trazo.tipo == CinemaTrazo.Punto)
            {
                // Un aro alrededor del punto.
                Vector3 centro = EnPantalla(trazo.puntos[0]);
                float radio = Mathf.Max(10f, imagen.height * 0.045f);
                var aro = new Vector3[25];
                for (int i = 0; i < aro.Length; i++)
                {
                    float a = i / (float)(aro.Length - 1) * Mathf.PI * 2f;
                    aro[i] = centro + new Vector3(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio, 0f);
                }
                Handles.DrawAAPolyLine(3f, aro);
                return;
            }

            if (trazo.tipo == CinemaTrazo.Flecha)
            {
                if (trazo.puntos.Count < 2) return;
                Vector3 desde = EnPantalla(trazo.puntos[0]);
                Vector3 hasta = EnPantalla(trazo.puntos[trazo.puntos.Count - 1]);
                Handles.DrawAAPolyLine(3f, desde, hasta);
                Vector3 dir = hasta - desde;
                if (dir.sqrMagnitude < 1f) return;
                dir.Normalize();
                Vector3 lado = new Vector3(-dir.y, dir.x, 0f);
                float punta = Mathf.Max(8f, imagen.height * 0.03f);
                Handles.DrawAAPolyLine(3f, hasta - dir * punta + lado * punta * 0.6f, hasta, hasta - dir * punta - lado * punta * 0.6f);
                return;
            }

            if (trazo.puntos.Count < 2) return;
            var puntos = new Vector3[trazo.puntos.Count];
            for (int i = 0; i < trazo.puntos.Count; i++) puntos[i] = EnPantalla(trazo.puntos[i]);
            Handles.DrawAAPolyLine(2.5f, puntos);
        }

        private static Vector2 Normalizado(Vector2 posicion, Rect imagen)
            => new Vector2(Mathf.Clamp01((posicion.x - imagen.x) / imagen.width),
                           Mathf.Clamp01((posicion.y - imagen.y) / imagen.height));

        private static Color ColorDeElemento(string id)
        {
            if (id == "camara") return new Color(1f, 0.62f, 0.25f);
            float t = Mathf.Abs((id ?? "x").GetHashCode() % 100) / 100f;
            return Color.HSVToRGB(t, 0.5f, 1f);
        }

        private void DibujarTransporte()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("|◀", EditorStyles.miniButton, GUILayout.Width(32))) IrA(0f);
            if (GUILayout.Button("◀", EditorStyles.miniButton, GUILayout.Width(32))) IrA(PlanoAnterior());
            bool enMarcha = _siguiendoPlay ? !_pausadaEnPlay : _reproduciendo;
            if (GUILayout.Button(enMarcha ? "❚❚" : "▶", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                if (_siguiendoPlay && ControlEnPlay() is ICinematicsPlayControl control) control.Pausar(!_pausadaEnPlay);
                else if (_reproduciendo) Parar();
                else Reproducir();
            }
            if (GUILayout.Button("▶", EditorStyles.miniButton, GUILayout.Width(32))) IrA(PlanoSiguiente());
            if (GUILayout.Button("▶|", EditorStyles.miniButton, GUILayout.Width(32))) IrA(Total());

            GUILayout.Space(8f);
            DibujarBarraDeTiempo();

            GUILayout.Label($"{Reloj(_t)} / {Reloj(Total())}", EditorStyles.miniLabel, GUILayout.Width(110));

            using (new EditorGUI.DisabledScope(!PuedeVerseEnVivo()))
            {
                bool enVivo = GUILayout.Toggle(_enVivo, new GUIContent("En vivo",
                    "Coloca la escena en cada segundo y la rinde aquí. Apagado: fotogramas del storyboard."),
                    EditorStyles.miniButton, GUILayout.Width(64));
                if (enVivo != _enVivo)
                {
                    _enVivo = enVivo;
                    _vivoPedido = enVivo;
                    EditorPrefs.SetBool(ClaveEnVivo, enVivo);
                    if (!enVivo) CerrarVista();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private float PlanoAnterior()
        {
            if (_detalle == null || _detalle.planos.Count == 0) return 0f;
            int i = Mathf.Max(0, _plano);
            if (i > 0 && Mathf.Abs(_detalle.planos[i].t0 - _t) < 0.05f) i--;
            else if (_plano > 0) i = _plano - 1;
            return _detalle.planos[Mathf.Clamp(i, 0, _detalle.planos.Count - 1)].t0;
        }

        private float PlanoSiguiente()
        {
            if (_detalle == null || _detalle.planos.Count == 0) return 0f;
            int i = Mathf.Clamp(_plano + 1, 0, _detalle.planos.Count - 1);
            return _detalle.planos[i].t0;
        }

        private void DibujarBarraDeTiempo()
        {
            var rect = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
            float total = Mathf.Max(0.01f, Total());
            int id = GUIUtility.GetControlID(CabezalId, FocusType.Passive, rect);
            var evento = Event.current;

            if (evento.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.17f, 0.2f));
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.SlideArrow);

                for (int i = 0; i < _detalle.planos.Count; i++)
                {
                    var p = _detalle.planos[i];
                    float x0 = rect.x + Mathf.Clamp01(p.t0 / total) * rect.width;
                    float x1 = rect.x + Mathf.Clamp01(p.t1 / total) * rect.width;
                    var color = i % 2 == 0 ? new Color(0.24f, 0.28f, 0.34f) : new Color(0.28f, 0.32f, 0.38f);
                    if (i == _plano) color = new Color(0.34f, 0.44f, 0.56f);
                    EditorGUI.DrawRect(new Rect(x0, rect.y, Mathf.Max(1f, x1 - x0), rect.height - 8f), color);
                }

                foreach (var l in _detalle.lineas)
                {
                    float x0 = rect.x + Mathf.Clamp01(l.t0 / total) * rect.width;
                    float x1 = rect.x + Mathf.Clamp01(l.t1 / total) * rect.width;
                    EditorGUI.DrawRect(new Rect(x0, rect.yMax - 7f, Mathf.Max(2f, x1 - x0), 6f),
                        new Color(0.36f, 0.62f, 0.86f));
                }

                float x = rect.x + Mathf.Clamp01(_t / total) * rect.width;
                EditorGUI.DrawRect(new Rect(x - 1f, rect.y, 2f, rect.height), new Color(1f, 0.85f, 0.3f));
            }
            else if (evento.GetTypeForControl(id) == EventType.MouseDown &&
                     evento.button == 0 && rect.Contains(evento.mousePosition))
            {
                GUIUtility.hotControl = id;
                if (!_siguiendoPlay) Parar();
                IrA(Mathf.InverseLerp(rect.xMin, rect.xMax, evento.mousePosition.x) * total);
                evento.Use();
            }
            else if (GUIUtility.hotControl == id && evento.type == EventType.MouseDrag)
            {
                IrA(Mathf.InverseLerp(rect.xMin, rect.xMax, evento.mousePosition.x) * total);
                evento.Use();
            }
            else if (GUIUtility.hotControl == id && evento.type == EventType.MouseUp && evento.button == 0)
            {
                GUIUtility.hotControl = 0;
                evento.Use();
            }
        }

        private void DibujarAvisos()
        {
            int errores = _detalle.errores.Count;
            int avisos = _detalle.avisos.Count;
            _verAvisos = EditorGUILayout.Foldout(_verAvisos,
                errores > 0
                    ? $"Último horneado: {errores} errores · {avisos} avisos"
                    : $"Último horneado: sin errores · {avisos} avisos", true);

            if (!_verAvisos) return;

            _scrollAvisos = EditorGUILayout.BeginScrollView(_scrollAvisos, GUILayout.Height(90f));
            foreach (var e in _detalle.errores) EditorGUILayout.LabelField("✗ " + e, EditorStyles.wordWrappedMiniLabel);
            foreach (var a in _detalle.avisos) EditorGUILayout.LabelField("· " + a, EditorStyles.wordWrappedMiniLabel);
            if (errores == 0 && avisos == 0) EditorGUILayout.LabelField("Nada que avisar.", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ── Auxiliares ───────────────────────────────────────────────────────────────────────

        private ICinematicsEditorBridge Puente()
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null) _mensaje = "No hay puente de cinemáticas.";
            return puente;
        }

        private void Ejecutar(System.Action accion)
        {
            _mensaje = null;
            try { accion(); }
            catch (System.Exception e) { _mensaje = e.Message; }
            RevisarEncargosPendientes(true);
            RepintarMesa();
        }

        /// El fotograma que toca dentro del plano: si el horneado dejó varios (animatic), el que
        /// corresponde al segundo que se está viendo; si no, el póster del plano.
        private Texture2D TexturaDelPlano(CinemaShot plano)
        {
            var fotos = plano.fotogramas;
            if (fotos == null || fotos.Count == 0) return TexturaDe(plano.imagen);

            float largo = Mathf.Max(0.01f, plano.t1 - plano.t0);
            float avance = Mathf.Clamp01((_t - plano.t0) / largo);
            int cual = Mathf.Clamp((int)(avance * fotos.Count), 0, fotos.Count - 1);
            return TexturaDe(fotos[cual]) ?? TexturaDe(plano.imagen);
        }

        private Texture2D TexturaDe(string ruta)        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return null;
            if (_texturas.TryGetValue(ruta, out var guardada) && guardada != null) return guardada;

            var textura = new Texture2D(2, 2, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!textura.LoadImage(File.ReadAllBytes(ruta)))
            {
                DestroyImmediate(textura);
                return null;
            }
            textura.wrapMode = TextureWrapMode.Clamp;
            _texturas[ruta] = textura;
            return textura;
        }

        private void DescartarTexturas()
        {
            foreach (var t in _texturas.Values)
                if (t != null) DestroyImmediate(t);
            _texturas.Clear();
        }

        private void DestruirTexturas() => DescartarTexturas();

        private static Rect Encajar(Rect marco, float aspecto)
        {
            if (aspecto <= 0.0001f) return marco;

            float ancho = marco.width;
            float alto = ancho / aspecto;
            if (alto > marco.height)
            {
                alto = marco.height;
                ancho = alto * aspecto;
            }
            return new Rect(marco.x + (marco.width - ancho) * 0.5f, marco.y + (marco.height - alto) * 0.5f,
                            ancho, alto);
        }

        private static string Reloj(float segundos)
        {
            int minutos = (int)(segundos / 60f);
            float resto = segundos - minutos * 60f;
            return $"{minutos}:{resto.ToString("00.0", CultureInfo.GetCultureInfo("es-ES"))}";
        }

        private static string Insignia(CinemaEntry e)
        {
            if (e.errores > 0) return $"✗ {e.errores}";
            if (e.encargos > 0) return $"IA · {e.encargos}";
            if (!string.IsNullOrEmpty(e.estado)) return e.estado;
            return "sin horneado";
        }

        /// El color de aviso de una cinemática: nada si está al día, ámbar si hay que rehornear,
        /// rojo suave si el último horneado dio errores y azul si espera correcciones de la IA.
        private static Color ColorDeAviso(CinemaEntry e)
        {
            if (e.errores > 0) return new Color(0.86f, 0.47f, 0.47f);
            if (e.encargos > 0) return new Color(0.45f, 0.74f, 0.95f);
            if (e.estado == "Al día" || string.IsNullOrEmpty(e.estado)) return new Color(0f, 0f, 0f, 0f);
            return new Color(0.86f, 0.72f, 0.4f);
        }

        private void InitEstilos()
        {
            if (_estiloNombreFila == null)
            {
                _estiloNombreFila = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Ellipsis,
                };
            }

            if (_estiloNombreElegido == null)
                _estiloNombreElegido = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Ellipsis,
                };

            if (_estiloMetaFila == null)
                _estiloMetaFila = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Ellipsis,
                };

            if (_estiloInsignia == null)
                _estiloInsignia = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleRight,
                };

            if (_estiloSeccion == null)
            {
                _estiloSeccion = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 10, padding = new RectOffset(2, 2, 6, 4) };
                _estiloSeccion.normal.textColor = new Color(0.6f, 0.62f, 0.66f);
            }

            if (_estiloTramo == null)
            {
                _estiloTramo = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                };
                _estiloTramo.normal.textColor = new Color(0.85f, 0.87f, 0.9f);
            }

            if (_estiloNotaTexto == null)
                _estiloNotaTexto = new GUIStyle(EditorStyles.textArea) { wordWrap = true };

            if (_estiloChip == null)
                _estiloChip = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 1, 1),
                };

            if (_estiloChipElegido == null)
            {
                _estiloChipElegido = new GUIStyle(_estiloChip) { fontStyle = FontStyle.Bold };
                _estiloChipElegido.normal.textColor = new Color(1f, 0.86f, 0.45f);
            }

            if (_estiloSobre == null)
            {
                _estiloSobre = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 11,
                };
                _estiloSobre.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }

            if (_estiloSubtitulo == null)
            {
                _estiloSubtitulo = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 14,
                };
                _estiloSubtitulo.normal.textColor = Color.white;
            }
        }
    }
}
