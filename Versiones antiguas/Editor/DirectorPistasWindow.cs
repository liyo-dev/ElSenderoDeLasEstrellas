using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// <summary>
    /// Vista «Pistas»: la cinemática elegida en la Mesa con UNA FILA POR PERSONAJE, como el dibujo
    /// de Raúl (19 nov 2026): arriba la tira de planos, debajo cada personaje con lo que hace en
    /// cada momento (acción en azul, reacción en naranja, reacción automática en gris, vida de
    /// fondo en verde, lo que dice en morado), y abajo la música. Pinchar un bloque lleva el
    /// cabezal de la Mesa a su segundo y enseña la línea del guion que lo pide.
    /// Solo lee el puente (CinemaDetail.pistas): no sabe nada del juego.
    /// </summary>
    public sealed class DirectorPistasWindow : EditorWindow
    {
        private const float AnchoNombre = 130f, AltoFila = 46f, AltoPlanos = 22f, AltoMusica = 22f;
        private static readonly Color Accion = new(0.29f, 0.56f, 0.91f), Reaccion = new(0.94f, 0.54f, 0.24f),
            Auto = new(0.54f, 0.56f, 0.6f), Vida = new(0.37f, 0.68f, 0.43f), Voz = new(0.55f, 0.42f, 0.78f),
            Movimiento = new(0.3f, 0.3f, 0.34f), Fondo = new(0.16f, 0.17f, 0.19f), Linea = new(1f, 1f, 1f, 0.08f);

        private string _id;
        private CinemaDetail _detalle;
        private float _zoom = 60f;     // píxeles por segundo
        private Vector2 _scroll;
        private string _info;

        public static void Abrir()
        {
            var v = GetWindow<DirectorPistasWindow>("Pistas");
            v.minSize = new Vector2(900, 320);
            v.Show();
        }

        private void OnEnable()
        {
            DirectorWindow.SeleccionCambiada += Repaint;
            CinematicsEditorServices.Cambiaron += Recargar;
        }

        private void OnDisable()
        {
            DirectorWindow.SeleccionCambiada -= Repaint;
            CinematicsEditorServices.Cambiaron -= Recargar;
        }

        private void Recargar() { _id = null; Repaint(); }

        private void OnGUI()
        {
            string id = DirectorWindow.CinematicaActivaId;
            if (string.IsNullOrEmpty(id)) { EditorGUILayout.HelpBox("Elige una cinemática en la Mesa del Director.", MessageType.Info); return; }
            if (id != _id)
            {
                _id = id;
                try { _detalle = CinematicsEditorServices.Bridge?.Leer(id); } catch { _detalle = null; }
            }
            if (_detalle == null) { EditorGUILayout.HelpBox("No hay horneado de esta cinemática.", MessageType.Warning); return; }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(_detalle.entrada?.nombre ?? id, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                Leyenda(Accion, "acción"); Leyenda(Reaccion, "reacción"); Leyenda(Auto, "automática"); Leyenda(Vida, "vida de fondo"); Leyenda(Voz, "habla");
                GUILayout.Space(12);
                GUILayout.Label("zoom", GUILayout.Width(36));
                _zoom = GUILayout.HorizontalSlider(_zoom, 15f, 200f, GUILayout.Width(120));
            }
            if (!string.IsNullOrEmpty(_info)) EditorGUILayout.LabelField(_info, EditorStyles.miniLabel);

            var filas = Filas();
            float total = Mathf.Max(1f, _detalle.planos.Count > 0 ? _detalle.planos.Max(p => p.t1) : 1f);
            foreach (var f in filas) foreach (var c in f.clips) total = Mathf.Max(total, c.t1);
            float ancho = AnchoNombre + total * _zoom + 20f;
            float alto = AltoPlanos + filas.Count * AltoFila + AltoMusica + 30f;

            _scroll = GUILayout.BeginScrollView(_scroll);
            var area = GUILayoutUtility.GetRect(ancho, alto);
            EditorGUI.DrawRect(area, Fondo);
            float x0 = area.x + AnchoNombre, y = area.y;

            // Planos.
            foreach (var p in _detalle.planos)
            {
                var r = new Rect(x0 + p.t0 * _zoom, y, Mathf.Max(2f, (p.t1 - p.t0) * _zoom - 1f), AltoPlanos - 2f);
                EditorGUI.DrawRect(r, new Color(0.24f, 0.25f, 0.28f));
                GUI.Label(r, " " + (p.descripcion ?? ""), EditorStyles.miniLabel);
            }
            GUI.Label(new Rect(area.x + 4, y, AnchoNombre, AltoPlanos), "Cámara", EditorStyles.miniBoldLabel);
            y += AltoPlanos;

            // Una fila por personaje.
            foreach (var f in filas)
            {
                EditorGUI.DrawRect(new Rect(area.x, y + AltoFila - 1, ancho, 1), Linea);
                GUI.Label(new Rect(area.x + 4, y + 4, AnchoNombre - 8, AltoFila - 8), f.nombre, EditorStyles.boldLabel);
                foreach (var c in f.clips.OrderBy(c => c.capa))
                {
                    float alto2 = c.capa == 0 ? AltoFila - 8 : (c.capa == 1 ? 12 : 10);
                    float dy = c.capa == 0 ? 4 : (c.capa == 1 ? AltoFila - 18 : 2);
                    var r = new Rect(x0 + c.t0 * _zoom, y + dy, Mathf.Max(3f, (c.t1 - c.t0) * _zoom - 1f), alto2);
                    EditorGUI.DrawRect(r, c.color);
                    if (c.capa == 0 && r.width > 24) GUI.Label(r, " " + c.etiqueta, EditorStyles.whiteMiniLabel);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        _info = $"{c.etiqueta} · {c.t0:0.0}–{c.t1:0.0} s" + (c.linea > 0 ? $" · línea {c.linea} del guion" : " · puesto solo por el horno");
                        DirectorWindow.PedirSegundo(c.t0);
                        Event.current.Use();
                    }
                }
                y += AltoFila;
            }

            // Música y efectos de sonido.
            GUI.Label(new Rect(area.x + 4, y + 2, AnchoNombre, AltoMusica), "Música", EditorStyles.miniBoldLabel);
            foreach (var pista in _detalle.pistas.Where(p => p.tipo == CinemaTrackKind.Musica))
                foreach (var c in pista.clips)
                {
                    var r = new Rect(x0 + c.t0 * _zoom, y + 2, Mathf.Max(3f, (c.t1 - c.t0) * _zoom - 1f), AltoMusica - 4);
                    EditorGUI.DrawRect(r, new Color(0.42f, 0.31f, 0.64f));
                    if (r.width > 30) GUI.Label(r, " " + c.etiqueta, EditorStyles.whiteMiniLabel);
                }

            // Cabezal de la Mesa.
            float t = DirectorWindow.SegundoActivo;
            if (t >= 0f) EditorGUI.DrawRect(new Rect(x0 + t * _zoom, area.y, 2, alto), new Color(1f, 0.85f, 0.3f));
            GUILayout.EndScrollView();
        }

        private static void Leyenda(Color c, string texto)
        {
            var r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
            r.y += 3; r.height = 10;
            EditorGUI.DrawRect(r, c);
            GUILayout.Label(texto, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        }

        private sealed class Bloque { public float t0, t1; public string etiqueta; public int linea; public Color color; public int capa; }
        private sealed class Fila { public string nombre; public readonly List<Bloque> clips = new(); }

        /// Junta las pistas del puente por personaje («Animación · X», «Voz · X», «Movimiento · X»…).
        private List<Fila> Filas()
        {
            var porNombre = new Dictionary<string, Fila>();
            var orden = new List<string>();
            foreach (var pista in _detalle.pistas)
            {
                int punto = pista.nombre.IndexOf('·');
                if (punto < 0) continue;
                string tipo = pista.nombre.Substring(0, punto).Trim();
                string quien = pista.nombre.Substring(punto + 1).Trim();
                if (tipo == "En escena") continue;
                if (!porNombre.TryGetValue(quien, out var fila))
                {
                    fila = porNombre[quien] = new Fila { nombre = quien };
                    orden.Add(quien);
                }
                foreach (var c in pista.clips)
                {
                    var b = new Bloque { t0 = c.t0, t1 = c.t1, etiqueta = c.etiqueta, linea = c.linea };
                    switch (tipo)
                    {
                        case "Voz": b.color = Voz; b.capa = 2; break;
                        case "Movimiento": b.color = Movimiento; b.capa = 1; break;
                        case "Cara": b.color = new Color(0.9f, 0.8f, 0.4f, 0.8f); b.capa = 1; b.etiqueta = "cara " + c.etiqueta; break;
                        default:
                            b.capa = 0;
                            b.color = c.clase switch
                            {
                                CinemaClipClase.Reaccion => Reaccion,
                                CinemaClipClase.Automatica => Auto,
                                CinemaClipClase.Vida => Vida,
                                _ => Accion,
                            };
                            break;
                    }
                    fila.clips.Add(b);
                }
            }
            // Los que más hacen, arriba (los protagonistas del momento antes que el fondo).
            return orden.Select(n => porNombre[n])
                        .Where(f => f.clips.Any(c => c.capa == 0 || c.capa == 2))
                        .OrderByDescending(f => f.clips.Count(c => c.capa != 1))
                        .ToList();
        }
    }
}
