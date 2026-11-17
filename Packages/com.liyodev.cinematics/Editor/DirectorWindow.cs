using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Liyodev.Assistant;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// La mesa del Director: la lista de cinemáticas del proyecto, sus planos como una película que
    /// se puede reproducir sin entrar en PlayMode (fotogramas reales, subtítulos y voces) y las
    /// puertas a lo que ya sabe editarlas (el taller de rodaje, el .guion.txt, el storyboard).
    ///
    /// No sabe nada del proyecto ni del grafo: todo lo pregunta al puente
    /// (<see cref="ICinematicsEditorBridge"/>). Ver `docs/plan-director-2026-11-16.md`.
    public class DirectorWindow : EditorWindow
    {
        /// La cinemática que el Director tiene abierta y el punto del timeline que se mira. Lo lee
        /// el asistente de IA desde fuera; la ventana no necesita saber quién lo escucha.
        public static string CinematicaActivaId { get; private set; }
        public static int PlanoActivo { get; private set; } = -1;
        public static float SegundoActivo { get; private set; } = -1f;
        public static event Action SeleccionCambiada;

        private const float AnchoLista = 350f;
        private const float AltoTira = 96f;

        private readonly List<CinemaEntry> _entradas = new();
        private readonly List<CinemaEntry> _visibles = new();
        private readonly Dictionary<string, Texture2D> _texturas = new();

        private string _busqueda = string.Empty;
        private Vector2 _scrollLista;
        private Vector2 _scrollTira;
        private Vector2 _scrollPistas;
        private Vector2 _scrollAvisos;

        private CinemaClip _clipElegido;
        private CinemaPizarra _pizarra;
        private string _elementoElegido = "camara";
        private string _textoNota = string.Empty;
        private string _textoTimeline = string.Empty;
        private bool _trazando;
        private CinemaTrazo _trazoEnCurso;
        private Rect _rectImagen;

        private CinemaEntry _elegida;
        private CinemaDetail _detalle;
        private string _mensaje;

        private bool _reproduciendo;
        private float _t;
        private double _ultimoTick;
        private string _vozSonando;
        private int _plano = -1;
        private bool _verAvisos;
        private bool _verHerramientas;
        private float _tAnterior;

        private GUIStyle _estiloPrincipal;
        private GUIStyle _estiloChip;
        private GUIStyle _estiloChipElegido;

        private GUIStyle _estiloNombreFila;
        private GUIStyle _estiloNombreElegido;
        private GUIStyle _estiloMetaFila;
        private GUIStyle _estiloInsignia;
        private GUIStyle _estiloNombrePista;
        private GUIStyle _estiloClip;
        private GUIStyle _estiloSubtitulo;
        private GUIStyle _estiloSobre;
        private GUIStyle _estiloTitulo;

        [MenuItem("Liyodev/Director/Mesa del Director", false, 1)]
        public static void Abrir()
        {
            var ventana = GetWindow<DirectorWindow>("Director");
            ventana.minSize = new Vector2(1120, 860);
            ventana.titleContent = new GUIContent("Director");
            ventana.Show();
        }

        /// Abre la mesa si hace falta y elige una cinemática por id. Lo usa el puente del asistente.
        public static bool SeleccionarPorId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            var ventana = GetWindow<DirectorWindow>("Director");
            ventana.minSize = new Vector2(1120, 860);
            ventana.RefrescarLista();
            var entrada = ventana._entradas.Find(e => e.id == id);
            if (entrada == null) return false;

            ventana.Seleccionar(entrada, true);
            ventana.Repaint();
            return true;
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            CinematicsEditorServices.Cambiaron += RefrescarLista;
            RefrescarLista();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            CinematicsEditorServices.Cambiaron -= RefrescarLista;
            Parar();
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
                var misma = _entradas.Find(x => x.id == _elegida.id);
                if (misma != null) Seleccionar(misma, true);
                else { _elegida = null; _detalle = null; }
            }
        }

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
            if (_elegida != null && _elegida.id != entrada.id) Parar();
            _elegida = entrada;
            CinematicaActivaId = entrada.id;
            PlanoActivo = -1;
            SegundoActivo = 0f;
            SeleccionCambiada?.Invoke();
            if (cargar) CargarDetalle();
        }

        private void CargarDetalle()
        {
            _detalle = null;
            _plano = -1;
            PlanoActivo = -1;
            SegundoActivo = 0f;
            _verAvisos = false;
            DescartarTexturas();

            var puente = CinematicsEditorServices.Bridge;
            if (puente == null || _elegida == null) return;

            try { _detalle = puente.Leer(_elegida.id); }
            catch (System.Exception e) { _mensaje = "Al abrir la cinemática: " + e.Message; }

            try { _pizarra = puente.LeerPizarra(_elegida.id) ?? new CinemaPizarra { guion = _elegida.id }; }
            catch { _pizarra = new CinemaPizarra { guion = _elegida.id }; }

            _t = 0f;
            _clipElegido = null;
            _trazoEnCurso = null;
            _textoNota = string.Empty;
            SincronizarPlano();
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
            if (!_reproduciendo || _detalle == null) return;

            double ahora = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.1f, (float)(ahora - _ultimoTick));
            _ultimoTick = ahora;

            float total = Total();
            if (total <= 0f) { Parar(); return; }

            _t += dt;
            if (_t >= total) { _t = total; Parar(); }

            SonarLaFraseQueToca();
            SonarEfectosQueTocan();
            SincronizarPlano();
            Repaint();
        }

        private void Reproducir()
        {
            if (_detalle == null) return;
            if (_t >= Total() - 0.01f) _t = 0f;
            _reproduciendo = true;
            _ultimoTick = EditorApplication.timeSinceStartup;
            _vozSonando = null;
            _tAnterior = _t;
            SincronizarPlano();
        }

        private void Parar()
        {
            _reproduciendo = false;
            EditorAudioPreview.StopFondo();
            if (_vozSonando != null)
            {
                EditorAudioPreview.Stop();
                _vozSonando = null;
            }
        }

        private void IrA(float t)
        {
            _t = Mathf.Clamp(t, 0f, Total());
            _vozSonando = null;
            EditorAudioPreview.Stop();
            EditorAudioPreview.StopFondo();
            _tAnterior = _t;
            SincronizarPlano();
            Repaint();
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
            bool cambio = PlanoActivo != indice;
            _plano = indice;
            PlanoActivo = indice;
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
                if (_vozSonando != null) { EditorAudioPreview.Stop(); _vozSonando = null; }
                return;
            }
            if (frase.clave == _vozSonando) return;

            _vozSonando = frase.clave;
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null) return;

            AudioClip clip = null;
            try { clip = puente.VozDe(frase.clave); }
            catch { /* sin voz: el flipbook sigue mudo, no es un error */ }

            if (clip != null) EditorAudioPreview.Play(clip, Mathf.Max(0f, _t - frase.t0));
        }

        // ── Pintura ──────────────────────────────────────────────────────────────────────────

        /// Música y efectos del animatic: suenan por el segundo canal del Editor, para no cortar la
        /// voz. Si este Editor no lo tiene, el animatic va solo con voces (y se dice en la barra).
        private void SonarEfectosQueTocan()
        {
            if (_detalle == null) return;

            foreach (var pista in _detalle.pistas)
            {
                if (pista.tipo != CinemaTrackKind.Musica && pista.tipo != CinemaTrackKind.Sonido) continue;

                foreach (var clip in pista.clips)
                {
                    if (clip.clip == null) continue;
                    if (clip.t0 <= _tAnterior || clip.t0 > _t) continue;
                    EditorAudioPreview.PlayFondo(clip.clip, Mathf.Max(0f, _t - clip.t0));
                }
            }
            _tAnterior = _t;
        }

        private void OnGUI()
        {
            InitEstilos();
            DibujarBarra();

            if (CinematicsEditorServices.Bridge == null)
            {
                EditorGUILayout.HelpBox(
                    _mensaje ?? "Este proyecto no tiene puente de cinemáticas: añade un " +
                    "ICinematicsEditorBridge en su carpeta de Connections.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DibujarLista();
            DibujarMesa();
            EditorGUILayout.EndHorizontal();
        }

        private void DibujarBarra()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Director", EditorStyles.boldLabel, GUILayout.Width(60));

            EditorGUI.BeginChangeCheck();
            _busqueda = GUILayout.TextField(_busqueda, EditorStyles.toolbarSearchField, GUILayout.Width(240));
            if (EditorGUI.EndChangeCheck()) Filtrar();

            if (GUILayout.Button("Refrescar", EditorStyles.toolbarButton, GUILayout.Width(70))) RefrescarLista();

            if (GUILayout.Button("Asistente", EditorStyles.toolbarButton, GUILayout.Width(80)))
                AssistantWindow.Open("cinematics");

            GUILayout.FlexibleSpace();

            GUILayout.Label($"{_entradas.Count} cinemáticas", EditorStyles.miniLabel);
            if (!EditorAudioPreview.Disponible)
                GUILayout.Label("· sin audio de previz en este Editor", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DibujarLista()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(AnchoLista));
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
                    Repaint();
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
            EditorGUILayout.BeginVertical();

            if (_elegida == null)
            {
                EditorGUILayout.HelpBox(
                    "Elige una cinemática de la lista. Verás sus planos como una película (con " +
                    "subtítulos y voces si el proyecto las tiene) y podrás abrir el taller para cambiarla.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            DibujarCabecera();
            if (_detalle == null)
            {
                EditorGUILayout.HelpBox(_mensaje ?? "No se pudo leer esta cinemática.", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            DibujarVisor();
            DibujarPizarra();
            DibujarTransporte();
            DibujarTira();
            DibujarTimeline();
            DibujarBotonera();
            DibujarAvisos();

            EditorGUILayout.EndVertical();
        }

        private void DibujarCabecera()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(_elegida.nombre, _estiloTitulo);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_elegida.estado ?? string.Empty, EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"{_elegida.escena} · {Reloj(Total())} · {(_detalle != null ? _detalle.planos.Count : _elegida.planos)} planos · " +
                $"{(_detalle != null ? _detalle.lineas.Count : _elegida.frases)} frases", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_mensaje))
                EditorGUILayout.HelpBox(_mensaje, MessageType.None);
        }

        private void DibujarVisor()
        {
            float ancho = Mathf.Max(320f, position.width - AnchoLista - 40f);
            float alto = Mathf.Clamp(position.height - 660f, 160f, ancho * 9f / 16f);
            var marco = GUILayoutUtility.GetRect(ancho, alto, GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(marco, new Color(0.06f, 0.07f, 0.09f));

            if (_plano < 0 && _detalle.planos.Count > 0) _plano = 0;
            var plano = (_plano >= 0 && _plano < _detalle.planos.Count) ? _detalle.planos[_plano] : null;
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

        private void DibujarPizarra()
        {
            if (_detalle == null || _detalle.planos.Count == 0) return;
            var plano = _detalle.planos[Mathf.Clamp(_plano, 0, _detalle.planos.Count - 1)];
            string panel = Panel(_plano);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Anotar en", EditorStyles.miniBoldLabel, GUILayout.Width(56));

            foreach (var elemento in plano.elementos)
            {
                bool elegido = elemento.id == _elementoElegido;
                if (GUILayout.Button((elegido ? "● " : "○ ") + elemento.etiqueta,
                        elegido ? _estiloChipElegido : _estiloChip, GUILayout.Height(19f)))
                    _elementoElegido = elemento.id;
            }

            GUILayout.FlexibleSpace();
            _trazando = GUILayout.Toggle(_trazando, "✏ Trazar", EditorStyles.miniButton, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _textoNota = EditorGUILayout.TextField(_textoNota, GUILayout.Height(19f));
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_textoNota)))
                if (GUILayout.Button("Añadir nota", EditorStyles.miniButton, GUILayout.Width(90)))
                {
                    var nota = NotaDe(panel, _elementoElegido);
                    nota.texto = string.IsNullOrEmpty(nota.texto) ? _textoNota.Trim() : nota.texto + " · " + _textoNota.Trim();
                    _textoNota = string.Empty;
                    GuardarPizarra();
                }

            if (GUILayout.Button("Aplicar correcciones", EditorStyles.miniButton, GUILayout.Width(140)))
                AplicarLaPizarra();
            EditorGUILayout.EndHorizontal();

            var notas = _pizarra.notas.FindAll(n => n.panel == panel);
            if (notas.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var nota in notas.ToArray())
                {
                    string etiqueta = EtiquetaDe(plano, nota.elemento);
                    string texto = string.IsNullOrEmpty(nota.texto) ? $"{nota.trazos.Count} trazo(s)" : nota.texto;
                    if (GUILayout.Button($"{etiqueta}: {Recorta(texto, 46)}  ×", EditorStyles.miniButton))
                    {
                        _pizarra.notas.Remove(nota);
                        GuardarPizarra();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
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
            Repaint();
        }

        private void AplicarLaPizarra()
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null || _pizarra == null) return;

            var notas = new List<CinemaNota>();
            foreach (var nota in _pizarra.notas)
            {
                string texto = nota.TextoEfectivo();
                if (string.IsNullOrEmpty(texto)) continue;
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
                    _pizarra.notas.RemoveAll(n => resultado.claves.Contains(ClaveDeNota(n.panel, n.elemento, n.texto)));
                    if (_pizarra.notas.Count != antes) GuardarPizarra();
                }
            }

            RefrescarLista();
            CargarDetalle();
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
            if (_trazoEnCurso != null && _trazoEnCurso.puntos.Count > 1)
            {
                Handles.color = ColorDeElemento(_elementoElegido);
                PintarTrazo(_trazoEnCurso, imagen);
            }
            Handles.EndGUI();
            Handles.matrix = previo;

            if (!_trazando) return;

            var e = Event.current;
            if (e.type == EventType.MouseDown && imagen.Contains(e.mousePosition))
            {
                _trazoEnCurso = new CinemaTrazo();
                _trazoEnCurso.puntos.Add(Normalizado(e.mousePosition, imagen));
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _trazoEnCurso != null)
            {
                _trazoEnCurso.puntos.Add(Normalizado(e.mousePosition, imagen));
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _trazoEnCurso != null)
            {
                if (_trazoEnCurso.puntos.Count > 1)
                {
                    NotaDe(panel, _elementoElegido).trazos.Add(_trazoEnCurso);
                    GuardarPizarra();
                }
                _trazoEnCurso = null;
                e.Use();
            }
        }

        private static void PintarTrazo(CinemaTrazo trazo, Rect imagen)
        {
            if (trazo.puntos.Count < 2) return;

            var puntos = new Vector3[trazo.puntos.Count];
            for (int i = 0; i < trazo.puntos.Count; i++)
                puntos[i] = new Vector3(imagen.x + trazo.puntos[i].x * imagen.width,
                                        imagen.y + trazo.puntos[i].y * imagen.height, 0f);

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
            if (GUILayout.Button(_reproduciendo ? "❚❚" : "▶", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                if (_reproduciendo) Parar();
                else Reproducir();
            }
            if (GUILayout.Button("▶", EditorStyles.miniButton, GUILayout.Width(32))) IrA(PlanoSiguiente());
            if (GUILayout.Button("▶|", EditorStyles.miniButton, GUILayout.Width(32))) IrA(Total());

            GUILayout.Space(8f);
            DibujarBarraDeTiempo();

            GUILayout.Label($"{Reloj(_t)} / {Reloj(Total())}", EditorStyles.miniLabel, GUILayout.Width(110));

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

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.17f, 0.2f));

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
            else if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
            {
                if (!rect.Contains(Event.current.mousePosition)) return;
                float t = Mathf.Clamp01((Event.current.mousePosition.x - rect.x) / rect.width) * total;
                if (Event.current.type == EventType.MouseDown) Parar();
                IrA(t);
                Event.current.Use();
            }
        }

        private void DibujarTira()
        {
            if (_detalle.planos.Count == 0) return;

            _scrollTira = EditorGUILayout.BeginScrollView(_scrollTira, GUILayout.Height(AltoTira));
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _detalle.planos.Count; i++)
            {
                var p = _detalle.planos[i];
                var textura = TexturaDe(p.imagen);
                var marco = GUILayoutUtility.GetRect(128f, 72f, GUILayout.Width(128f), GUILayout.Height(72f));

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(marco, new Color(0.1f, 0.11f, 0.13f));
                    if (textura != null) GUI.DrawTexture(marco, textura, ScaleMode.ScaleAndCrop, false);
                    if (i == _plano)
                        EditorGUI.DrawRect(new Rect(marco.x, marco.yMax - 3f, marco.width, 3f),
                            new Color(1f, 0.85f, 0.3f));
                    GUI.Label(new Rect(marco.x + 4f, marco.y + 2f, marco.width - 8f, 16f),
                        $"P{i + 1:00} {Reloj(p.t0)}", _estiloSobre);
                }
                else if (Event.current.type == EventType.MouseDown && marco.Contains(Event.current.mousePosition))
                {
                    Parar();
                    IrA(p.t0);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// El timeline: una fila por cosa (cámara, cada personaje, música, efectos, marcas) con sus
        /// clips a la duración real. De momento solo se lee: darle a un clip lleva el cabezal a su
        /// segundo y enseña de qué línea del guion sale.
        /// </summary>
        private void DibujarTimeline()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Timeline", EditorStyles.boldLabel, GUILayout.Width(56));

            if (_clipElegido != null)
            {
                string deDonde = _clipElegido.linea > 0 ? $"L{_clipElegido.linea}" : "sin línea (horneado viejo)";
                GUILayout.Label($"{deDonde} · {_clipElegido.etiqueta}", EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label("pincha un clip para ir a su segundo", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            if (_clipElegido != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Nota en {Reloj(_clipElegido.t0)}:", EditorStyles.miniLabel, GUILayout.Width(96));
                _textoTimeline = EditorGUILayout.TextField(_textoTimeline, GUILayout.Height(19f));
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_textoTimeline)))
                    if (GUILayout.Button("Anotar", EditorStyles.miniButton, GUILayout.Width(64)))
                    {
                        _pizarra.notas.Add(new CinemaNotaPizarra
                        {
                            panel = Panel(_plano),
                            elemento = string.IsNullOrEmpty(_clipElegido.elemento) ? "camara" : _clipElegido.elemento,
                            texto = _textoTimeline.Trim(),
                            t = _clipElegido.t0,
                        });
                        _textoTimeline = string.Empty;
                        GuardarPizarra();
                    }
                EditorGUILayout.EndHorizontal();
            }

            if (_detalle.pistas.Count == 0)
            {
                EditorGUILayout.HelpBox("Este horneado no trae pistas (vuelve a hornear para verlas).", MessageType.None);
                return;
            }

            const float altoFila = 19f;
            const float anchoNombre = 122f;
            float total = Mathf.Max(0.01f, Total());
            float anchoCanvas = Mathf.Max(160f, position.width - AnchoLista - anchoNombre - 60f);

            _scrollPistas = EditorGUILayout.BeginScrollView(_scrollPistas, GUILayout.Height(176f));

            foreach (var pista in _detalle.pistas)
            {
                var fila = GUILayoutUtility.GetRect(anchoNombre + anchoCanvas, altoFila, GUILayout.ExpandWidth(true));
                var lienzo = new Rect(fila.x + anchoNombre, fila.y, anchoCanvas, fila.height);

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(fila, new Color(1f, 1f, 1f, 0.02f));
                    EditorGUI.DrawRect(new Rect(fila.x, fila.yMax - 1f, fila.width, 1f), new Color(0f, 0f, 0f, 0.25f));

                    var color = ColorDePista(pista.tipo);
                    foreach (var clip in pista.clips)
                    {
                        float x0 = lienzo.x + Mathf.Clamp01(clip.t0 / total) * lienzo.width;
                        float x1 = lienzo.x + Mathf.Clamp01(clip.t1 / total) * lienzo.width;
                        var caja = new Rect(x0, lienzo.y + 2f, Mathf.Max(2f, x1 - x0), lienzo.height - 5f);

                        bool elegido = _clipElegido != null && ReferenceEquals(_clipElegido, clip);
                        EditorGUI.DrawRect(caja, elegido ? Color.Lerp(color, Color.white, 0.45f) : color);

                        if (caja.width > 34f)
                        {
                            _estiloClip.normal.textColor = new Color(0.05f, 0.06f, 0.08f);
                            GUI.Label(new Rect(caja.x + 3f, caja.y, caja.width - 6f, caja.height), clip.etiqueta, _estiloClip);
                        }
                    }

                    float x = lienzo.x + Mathf.Clamp01(_t / total) * lienzo.width;
                    EditorGUI.DrawRect(new Rect(x - 1f, lienzo.y, 2f, lienzo.height), new Color(1f, 0.85f, 0.3f));

                    // Las notas del timeline, como marcas blancas en su segundo.
                    if (_pizarra != null)
                        foreach (var nota in _pizarra.notas)
                        {
                            if (nota.t < 0f || nota.panel != Panel(_plano)) continue;
                            float xn = lienzo.x + Mathf.Clamp01(nota.t / total) * lienzo.width;
                            EditorGUI.DrawRect(new Rect(xn - 1f, lienzo.y + 1f, 2f, lienzo.height - 2f),
                                new Color(0.96f, 0.96f, 0.96f, 0.75f));
                        }
                }

                _estiloNombrePista.normal.textColor = pista.tipo == CinemaTrackKind.Voz
                    ? new Color(0.9f, 0.82f, 0.55f)
                    : new Color(0.78f, 0.8f, 0.84f);
                GUI.Label(new Rect(fila.x + 4f, fila.y, anchoNombre - 8f, fila.height), pista.nombre, _estiloNombrePista);

                if (Event.current.type == EventType.MouseDown && lienzo.Contains(Event.current.mousePosition))
                {
                    float t = Mathf.Clamp01((Event.current.mousePosition.x - lienzo.x) / lienzo.width) * total;
                    var tocado = ClipEn(pista, t);
                    _clipElegido = tocado;
                    Parar();
                    IrA(tocado != null ? tocado.t0 : t);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static CinemaClip ClipEn(CinemaTrack pista, float t)
        {
            CinemaClip cerca = null;
            foreach (var clip in pista.clips)
            {
                if (t < clip.t0 || t > clip.t1) continue;
                if (cerca == null || clip.t0 > cerca.t0) cerca = clip;
            }
            return cerca;
        }

        private static Color ColorDePista(CinemaTrackKind tipo) => tipo switch
        {
            CinemaTrackKind.Camara => new Color(0.36f, 0.51f, 0.69f),
            CinemaTrackKind.Voz => new Color(0.78f, 0.68f, 0.32f),
            CinemaTrackKind.Movimiento => new Color(0.36f, 0.62f, 0.46f),
            CinemaTrackKind.Gesto => new Color(0.56f, 0.47f, 0.76f),
            CinemaTrackKind.Musica => new Color(0.76f, 0.42f, 0.61f),
            CinemaTrackKind.Sonido => new Color(0.36f, 0.61f, 0.71f),
            CinemaTrackKind.Vfx => new Color(0.81f, 0.56f, 0.32f),
            CinemaTrackKind.Prop => new Color(0.62f, 0.52f, 0.37f),
            CinemaTrackKind.Marca => new Color(0.55f, 0.55f, 0.58f),
            _ => new Color(0.45f, 0.46f, 0.5f),
        };

        private void DibujarBotonera()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("▶  Ver en Unity", _estiloPrincipal, GUILayout.Height(28f)))
                Accion(() =>
                {
                    if (!Puente().RodarEnPlay(_elegida.id, _t))
                        _mensaje = "No se pudo rodar en Play: mira la consola.";
                });

            if (GUILayout.Button("Ver el guion", EditorStyles.miniButton, GUILayout.Height(28f)))
            {
                if (string.IsNullOrEmpty(_elegida.rutaGuion)) _mensaje = "No encuentro el .guion.txt de esta cinemática.";
                else EditorUtility.RevealInFinder(_elegida.rutaGuion);
            }

            if (GUILayout.Button("Página anotable", EditorStyles.miniButton, GUILayout.Height(28f)))
                Accion(() => Puente().GenerarPaginaAnotable(_elegida.id));

            if (GUILayout.Button("Storyboard", EditorStyles.miniButton, GUILayout.Height(28f)))
                Accion(() => Puente().AbrirStoryboard(_elegida.id));

            if (GUILayout.Button("Aplicar correcciones…", EditorStyles.miniButton, GUILayout.Height(28f)))
                Accion(() => AplicarCorrecciones.Abrir());

            int encargos = _detalle?.entrada?.encargos ?? 0;
            using (new EditorGUI.DisabledScope(encargos <= 0))
            {
                string texto = encargos > 0 ? $"Encargos de la IA ({encargos})" : "Encargos de la IA";
                if (GUILayout.Button(texto, EditorStyles.miniButton, GUILayout.Height(28f)))
                    Accion(() => AssistantWindow.Open("cinematics", runQuickAction: true));
            }

            if (GUILayout.Button("Nueva cinemática…", EditorStyles.miniButton, GUILayout.Height(28f)))
                NuevaCinematicaWindow.Abrir();

            EditorGUILayout.EndHorizontal();

            _verHerramientas = EditorGUILayout.Foldout(_verHerramientas, "Herramientas", true);
            if (!_verHerramientas) return;

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_elegida.rutaGuion)))
            {
                if (GUILayout.Button("Hornear", EditorStyles.miniButton)) Accion(() => Puente().Hornear(_elegida.id, false));
                if (GUILayout.Button("Hornear + storyboard", EditorStyles.miniButton)) Accion(() => Puente().Hornear(_elegida.id, true));
            }
            if (GUILayout.Button("Abrir el taller", EditorStyles.miniButton)) Accion(() => Puente().AbrirTaller(_elegida.id));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(encargos <= 0))
            {
                if (GUILayout.Button("Ver archivo de encargos (.md)", EditorStyles.miniButton))
                    Accion(() => Puente().AbrirEncargos(_elegida.id));
            }
            EditorGUILayout.EndHorizontal();
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

        private void Accion(System.Action accion)
        {
            _mensaje = null;
            try { accion(); }
            catch (System.Exception e) { _mensaje = e.Message; }
            RefrescarLista();
            if (_elegida != null) CargarDetalle();
            Repaint();
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

            if (_estiloNombrePista == null)
                _estiloNombrePista = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Ellipsis,
                };

            if (_estiloClip == null)
                _estiloClip = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 9,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Ellipsis,
                    padding = new RectOffset(0, 0, 0, 0),
                };

            if (_estiloTitulo == null)
                _estiloTitulo = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };

            if (_estiloPrincipal == null)
                _estiloPrincipal = new GUIStyle(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                };

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
