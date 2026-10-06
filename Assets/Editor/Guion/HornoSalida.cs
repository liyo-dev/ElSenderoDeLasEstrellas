#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// Salida del horneado: comprobaciones, el asset, el parte y los dibujos.
internal sealed partial class Horno
{
    private const string RuidoDeMano = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";

    // ── Comprobaciones ──────────────────────────────────────────────────────────────────────

    private readonly List<string> _huecos = new();

    private void Validar()
    {
        // Choques que hayan quedado (p. ej. alguien colocado de golpe encima del camino de otro).
        var vistos = new HashSet<string>();
        var lista = _actores.Values.ToList();
        for (float t = 0f; t <= _fin; t += 0.2f)
            for (int i = 0; i < lista.Count; i++)
                for (int j = i + 1; j < lista.Count; j++)
                {
                    var a = lista[i]; var b = lista[j];
                    if (!a.VisibleEn(t) || !b.VisibleEn(t)) continue;
                    if (!a.AndandoEn(t) && !b.AndandoEn(t)) continue;
                    if (Plano(a.PosEn(t) - b.PosEn(t)).magnitude >= 0.5f) continue;
                    string clave = $"{a.alias}-{b.alias}-{(int)(t / 3f)}";
                    if (vistos.Add(clave)) Aviso($"{Tiempo(t)}: {a.alias} y {b.alias} se atraviesan.");
                }

        // Frases que se pisan (una voz corta a la otra).
        for (int i = 1; i < _lineas.Count; i++)
            if (_lineas[i].t0 < _lineas[i - 1].t1 - 0.05f)
                Aviso($"{Tiempo(_lineas[i].t0)}: {_lineas[i].clave} empieza antes de que acabe {_lineas[i - 1].clave}; la segunda voz corta a la primera.");

        // Huecos de ritmo entre frases (informativo).
        for (int i = 1; i < _lineas.Count; i++)
        {
            float hueco = _lineas[i].t0 - _lineas[i - 1].t1;
            if (hueco > 1.6f) _huecos.Add($"{Tiempo(_lineas[i - 1].t1)} → {Tiempo(_lineas[i].t0)}: {hueco:0.0} s sin voz entre {_lineas[i - 1].clave} y {_lineas[i].clave}");
        }

        // Planos.
        foreach (var p in _salida.planos)
        {
            float dura = p.t1 - p.t0;
            if (dura < 0.8f) Aviso($"{Tiempo(p.t0)}: el plano «{p.descripcion}» dura {dura:0.0} s (un corte tan corto se lee como un salto).");
            bool hayVoz = _lineas.Any(l => l.t1 > p.t0 && l.t0 < p.t1);
            if (dura > 9f && !hayVoz && p.posiciones.Count < 2 && p.tipo != TipoDePlano.Sigue)
                Aviso($"{Tiempo(p.t0)}: «{p.descripcion}» dura {dura:0.0} s sin voz y con la cámara quieta. ¿Pasa algo dentro?");
            Parados(p);
        }
    }

    /// Gente quieta en cuadro sin hacer nada durante mucho rato.
    private void Parados(PlanoHorneado p)
    {
        if (p.posiciones.Count == 0) return;
        float tanH = Mathf.Tan(p.lente * 0.5f * Mathf.Deg2Rad) * 16f / 9f;
        float mitadH = Mathf.Atan(tanH) * Mathf.Rad2Deg;
        foreach (var a in _actores.Values)
        {
            if (p.sujetos.Contains(a.id)) continue;
            float quieto = 0f, maximo = 0f;
            for (float t = p.t0; t < p.t1; t += 0.5f)
            {
                Vector3 cam = p.PosicionEn(t);
                Vector3 foco = FocoDePlano(p, t);
                Vector3 pa = a.PosEn(t);
                bool enCuadro = a.VisibleEn(t) && Plano(pa - cam).magnitude < 22f
                    && Mathf.Abs(Mathf.DeltaAngle(Rumbo(foco - cam), Rumbo(pa - cam))) < mitadH;
                bool haceAlgo = a.AndandoEn(t) || BucleEn(a, t) != null || HablaEn(a, t)
                                || a.anim.Any(o => o.tipo == TipoDeAnimacion.Gesto && t >= o.t && t < o.t + 2.5f);
                if (enCuadro && !haceAlgo) { quieto += 0.5f; maximo = Mathf.Max(maximo, quieto); }
                else quieto = 0f;
            }
            if (maximo >= 6f)
                Aviso($"{Tiempo(p.t0)}: en «{p.descripcion}» {a.alias} está {maximo:0} s en cuadro sin hacer nada. Dale algo en su pista (charla, bucle, pasea…).");
        }
    }

    private Vector3 FocoDePlano(PlanoHorneado p, float t)
    {
        if (p.sujetos.Count == 0 || p.sujetos[0].StartsWith("PROP_")) return p.focoFijo;
        Vector3 c = Vector3.zero; int n = 0;
        foreach (var id in p.sujetos)
        {
            var a = _actores.Values.FirstOrDefault(x => x.id == id);
            if (a == null) continue;
            c += a.PosEn(t); n++;
        }
        if (n > 0 && p.extras != null)
            foreach (var x in p.extras) { c += x - Vector3.up * p.alturaDelFoco; n++; }
        return n > 0 ? c / n + Vector3.up * p.alturaDelFoco : p.focoFijo;
    }

    // ── El asset ────────────────────────────────────────────────────────────────────────────

    private GuionHorneado Guardar()
    {
        _salida.nombre = _g.nombre;
        _salida.duracion = _fin;
        _salida.presentacion = _g.texto;
        _salida.huella = $"{DateTime.Now:yyyy-MM-dd HH:mm} · {_g.huella}";
        _salida.ruidoDeMano = AssetDatabase.LoadAssetAtPath<Unity.Cinemachine.NoiseSettings>(RuidoDeMano);
        _salida.lineas = _lineas;
        _salida.puntos = _puntos.Select(kv => new PuntoHorneado { nombre = kv.Key, posicion = kv.Value }).ToList();
        _salida.efectos = _efectos;
        _salida.etiquetas = _etiquetas.OrderBy(kv => kv.Value).Select(kv => new EtiquetaHorneada { nombre = kv.Key, t = kv.Value }).ToList();
        foreach (var a in _actores.Values)
        {
            Color color = default;
            if (a.reparto.opciones.TryGetValue("color", out var hex))
                ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out color);
            var h = new ActorHorneado
            {
                id = a.id, alias = a.alias, colorDelNombre = color,
                claveDelNombre = a.reparto.opciones.TryGetValue("nombre", out var n) ? n : null,
            };
            h.posiciones.AddRange(Comprimir(a.pos));
            h.miradas.AddRange(a.miradas);
            h.animacion.AddRange(a.anim);
            h.caras.AddRange(a.caras);
            foreach (var d in a.deslizando) h.deslizando.Add(new TramoVisible { desde = d.t0, hasta = d.t1 });
            bool visible = !a.ocultoAlEmpezar;
            float desde = 0f;
            bool cambia = a.ocultoAlEmpezar || a.visibilidad.Count > 0;
            if (cambia)
            {
                foreach (var c in a.visibilidad)
                {
                    if (c.visible == visible) continue;
                    if (visible) h.visible.Add(new TramoVisible { desde = desde, hasta = c.t });
                    else desde = c.t;
                    visible = c.visible;
                }
                if (visible) h.visible.Add(new TramoVisible { desde = desde, hasta = _fin + 60f });
                if (h.visible.Count == 0) h.visible.Add(new TramoVisible { desde = -1f, hasta = -0.5f });
            }
            _salida.actores.Add(h);
        }

        string dir = Path.GetDirectoryName(_rutaTexto).Replace('\\', '/');
        string ruta = $"{dir}/{_g.nombre}_Horneado.asset";
        var existente = AssetDatabase.LoadAssetAtPath<GuionHorneado>(ruta);
        GuionHorneado asset;
        if (existente != null)
        {
            EditorUtility.CopySerialized(_salida, existente);
            existente.name = Path.GetFileNameWithoutExtension(ruta);
            EditorUtility.SetDirty(existente);
            asset = existente;
        }
        else
        {
            AssetDatabase.CreateAsset(_salida, ruta);
            asset = _salida;
        }
        AssetDatabase.SaveAssets();
        EnlazarSecuencia(asset);
        return asset;
    }

    /// Quita claves que no aportan nada (tramos quietos repetidos).
    private static List<ClaveDePosicion> Comprimir(List<ClaveDePosicion> claves)
    {
        var r = new List<ClaveDePosicion>();
        for (int i = 0; i < claves.Count; i++)
        {
            if (r.Count >= 2 && i < claves.Count)
            {
                var a = r[r.Count - 2]; var b = r[r.Count - 1]; var c = claves[i];
                if ((a.p - b.p).sqrMagnitude < 1e-6f && (b.p - c.p).sqrMagnitude < 1e-6f) { r[r.Count - 1] = c; continue; }
            }
            r.Add(claves[i]);
        }
        return r;
    }

    private void EnlazarSecuencia(GuionHorneado asset)
    {
        if (string.IsNullOrEmpty(_g.secuencia)) return;
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(_g.secuencia);
        if (def == null) { Aviso($"No encuentro la secuencia '{_g.secuencia}' para enlazar el guion."); return; }

        // Secuencia mixta (INC-598): trozos de guion entre fases de jugabilidad (el despertar). Si ya
        // tiene un GuionBeat de este guion, solo se le cambia el horneado; el resto no se toca.
        if (def.phases != null)
            foreach (var fase in def.phases)
                if (fase?.beats != null)
                    foreach (var b in fase.beats)
                        if (b is GuionBeat gb && (gb.guion == asset || (gb.guion != null && gb.guion.name == asset.name) || (gb.guion == null && gb.note == "Guion: " + _g.nombre)))
                        {
                            bool mixta = !(def.phases.Count == 1 && fase.beats.Count == 1);
                            if (mixta)
                            {
                                Undo.RecordObject(def, "Enlazar guion");
                                gb.guion = asset;
                                // La línea TEXTO del guion manda también en las secuencias mixtas.
                                def.presentacionDeTexto = _g.texto;
                                EditorUtility.SetDirty(def);
                                AssetDatabase.SaveAssets();
                                return;
                            }
                        }
        bool tieneGuiones = def.phases != null && def.phases.Any(f => f?.beats != null && f.beats.Any(x => x is GuionBeat));
        bool soloUnGuion = def.phases != null && def.phases.Count == 1 && def.phases[0].beats.Count == 1 && def.phases[0].beats[0] is GuionBeat;
        if (tieneGuiones && !soloUnGuion)
        {
            Aviso($"La secuencia '{_g.secuencia}' mezcla guion y jugabilidad y no tiene un GuionBeat de '{_g.nombre}': no la toco. Ponlo con su menú de montaje.");
            return;
        }
        bool yaEsGuion = def.phases != null && def.phases.Count == 1 && def.phases[0].beats.Count == 1 && def.phases[0].beats[0] is GuionBeat;
        if (!yaEsGuion)
        {
            // La versión de beats se guarda una vez en Versiones antiguas antes de sustituirla.
            string raiz = Directory.GetParent(Application.dataPath).FullName;
            string destino = Path.Combine(raiz, "Versiones antiguas", "Secuencias de beats (antes del guion)");
            Directory.CreateDirectory(destino);
            string copia = Path.Combine(destino, Path.GetFileName(_g.secuencia));
            if (!File.Exists(copia)) File.Copy(Path.Combine(raiz, _g.secuencia), copia);
        }

        float empezarEn = 0f;
        if (yaEsGuion) empezarEn = ((GuionBeat)def.phases[0].beats[0]).empezarEn;
        Undo.RecordObject(def, "Enlazar guion");
        def.phases = new List<SequencePhase>
        {
            new SequencePhase { name = "Guion: " + _g.nombre, beats = new List<SequenceBeat> { new GuionBeat { guion = asset, empezarEn = empezarEn, note = "Generado por el horneado del guion" } } }
        };
        def.presentacionDeTexto = _g.texto;
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
    }

    // ── El parte ────────────────────────────────────────────────────────────────────────────

    public void EscribirInforme()
    {
        if (_carpetaSalida == null)
        {
            _carpetaSalida = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, CapturaDeEscenario.CarpetaDeEscena(_g.escena ?? "sin_escena", _g.zona), _g.nombre ?? "sin_nombre");
            Directory.CreateDirectory(_carpetaSalida);
        }
        var sb = new StringBuilder();
        sb.AppendLine($"# Parte de horneado: {_g.nombre}\n");
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm} · guion `{_rutaTexto}`\n");
        if (_salida != null)
            sb.AppendLine($"**{Tiempo(_salida.duracion)}** de secuencia · {_lineas.Count} frases · {_salida.planos.Count} planos · {_efectos.Count} efectos · {_actores.Count} personajes\n");
        sb.AppendLine("Reparto medido (m): " + string.Join(" · ", _actores.Values.Select(x => $"{x.alias} {x.altura:0.00}/ojos {x.AlturaDeOjos:0.00}")) + "\n");
        sb.AppendLine($"## Errores ({Errores.Count})\n");
        foreach (var e in Errores) sb.AppendLine("- " + e);
        sb.AppendLine($"\n## Avisos ({Avisos.Count})\n");
        foreach (var a in Avisos) sb.AppendLine("- " + a);
        if (_huecos.Count > 0)
        {
            sb.AppendLine($"\n## Silencios largos entre frases ({_huecos.Count})\n");
            foreach (var h in _huecos) sb.AppendLine("- " + h);
        }

        if (_salida != null)
        {
            sb.AppendLine("\n## Línea de tiempo\n");
            sb.AppendLine("| Tiempo | Qué | Detalle |");
            sb.AppendLine("|---|---|---|");
            var filas = new List<(float t, int orden, string que, string detalle)>();
            foreach (var kv in _etiquetas) filas.Add((kv.Value, 0, "**@" + kv.Key + "**", ""));
            int n = 0;
            foreach (var p in _salida.planos) filas.Add((p.t0, 1, $"plano {++n}", $"{p.descripcion} · {p.t1 - p.t0:0.0} s · lente {p.lente:0}°{(p.mezcla > 0 ? $" · mezcla {p.mezcla:0.0} s" : "")}{(NotasDePlano.TryGetValue(p.t0, out var nota) ? " · " + nota : "")}"));
            foreach (var l in _lineas)
            {
                var a = _actores.Values.FirstOrDefault(x => x.id == l.actor);
                filas.Add((l.t0, 2, $"{a?.alias}", $"«{(TextoDe(l.clave) ?? l.clave).Replace("\n", " ")}» ({l.t1 - l.t0:0.0} s){(l.sinPose ? "" : " · pose")}"));
            }
            foreach (var e in _efectos) filas.Add((e.t, 3, "efecto", e.beat.Describe()));
            foreach (var f in filas.OrderBy(f => f.t).ThenBy(f => f.orden))
                sb.AppendLine($"| {Tiempo(f.t)} | {f.que} | {f.detalle.Replace("|", "/")} |");

            sb.AppendLine("\n## Qué hace cada personaje\n");
            foreach (var a in _actores.Values)
            {
                sb.AppendLine($"### {a.alias} ({a.id})\n");
                foreach (var linea in Actividad(a)) sb.AppendLine("- " + linea);
                sb.AppendLine();
            }
        }
        File.WriteAllText(Path.Combine(_carpetaSalida, "informe.md"), sb.ToString());
    }

    private IEnumerable<string> Actividad(ActorH a)
    {
        var eventos = new List<(float t, string s)>();
        bool andando = false; float desde = 0f;
        for (float t = 0f; t <= _fin; t += 0.1f)
        {
            bool ahora = a.AndandoEn(t);
            if (ahora && !andando) desde = t;
            if (!ahora && andando)
            {
                var p0 = a.PosEn(desde); var p1 = a.PosEn(t);
                eventos.Add((desde, $"{Tiempo(desde)}–{Tiempo(t)} anda de ({p0.x:0.0}, {p0.z:0.0}) a ({p1.x:0.0}, {p1.z:0.0})"));
            }
            andando = ahora;
        }
        foreach (var o in a.anim)
            if (o.tipo == TipoDeAnimacion.Bucle || o.tipo == TipoDeAnimacion.Gesto || o.tipo == TipoDeAnimacion.Reposo)
                eventos.Add((o.t, $"{Tiempo(o.t)} {(o.tipo == TipoDeAnimacion.Bucle ? "bucle" : o.tipo == TipoDeAnimacion.Gesto ? "gesto" : "reposo")} {o.estado}"));
        foreach (var (t0, t1) in a.habla) eventos.Add((t0, $"{Tiempo(t0)}–{Tiempo(t1)} habla"));
        foreach (var v in a.visibilidad) eventos.Add((v.t, $"{Tiempo(v.t)} {(v.visible ? "aparece" : "desaparece")}"));
        return eventos.OrderBy(e => e.t).Select(e => e.s);
    }

    private static string Tiempo(float t) => $"{(int)(t / 60f)}:{t % 60f:00.0}";

    // ── Dibujos de los recorridos ───────────────────────────────────────────────────────────

    [Serializable] private class AreaJson { public float xmin, zmin, xmax, zmax; }
    [Serializable] private class EscenarioJson { public AreaJson area_detalle; public AreaJson area_general; }

    private void DibujarRecorridos()
    {
        string carpetaEscenario = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, CapturaDeEscenario.CarpetaDeEscena(_g.escena, _g.zona), "escenario");
        string json = Path.Combine(carpetaEscenario, "escenario.json");
        if (!File.Exists(json))
        {
            Aviso("No hay captura del escenario: usa «El Sendero → Guion → 1. Capturar el escenario» para tener los dibujos.");
            return;
        }
        var esc = JsonUtility.FromJson<EscenarioJson>(File.ReadAllText(json));
        var ad = esc.area_detalle;
        var area = Rect.MinMaxRect(ad.xmin, ad.zmin, ad.xmax, ad.zmax);
        int ancho = 2048, alto = Mathf.RoundToInt(2048 * area.height / area.width);
        if (alto > 2048) { alto = 2048; ancho = Mathf.RoundToInt(2048 * area.width / area.height); }

        // Ventanas: de etiqueta a etiqueta (las muy cortas se juntan con la siguiente).
        var cortes = _etiquetas.Values.Concat(new[] { 0f, _fin }).Distinct().OrderBy(t => t).ToList();
        var ventanas = new List<(float t0, float t1, string nombre)>();
        float inicio = 0f;
        for (int i = 1; i < cortes.Count; i++)
        {
            if (cortes[i] - inicio < 10f && i < cortes.Count - 1) continue;
            string nombre = _etiquetas.Where(kv => Mathf.Approximately(kv.Value, inicio)).Select(kv => kv.Key).FirstOrDefault() ?? "inicio";
            ventanas.Add((inicio, cortes[i], nombre));
            inicio = cortes[i];
        }

        string carpeta = Path.Combine(_carpetaSalida, "recorridos");
        Directory.CreateDirectory(carpeta);
        foreach (var f in Directory.GetFiles(carpeta, "*.svg")) File.Delete(f);
        var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Recorridos</title><body style='background:#111;color:#eee;font-family:Segoe UI,Arial'>\n");
        html.Append($"<h1>Recorridos · {_g.nombre}</h1><p>Cada personaje con su color; un punto cada 2 s (con el segundo cada 10 s); los conos son las cámaras con su número de plano.</p>\n");
        var colores = new Dictionary<ActorH, string>();
        int ci = 0;
        foreach (var a in _actores.Values) colores[a] = CapturaDeEscenario.ColorDe(ci++);

        for (int w = 0; w < ventanas.Count; w++)
        {
            var (t0, t1, nombre) = ventanas[w];
            var svg = new LienzoSvg(area, ancho, alto, "../../escenario/planta_detalle.png");
            svg.Rejilla(2f, "#ffffff", 0.08f);
            foreach (var a in _actores.Values)
            {
                var puntos = new List<Vector3>();
                bool alguno = false;
                for (float t = t0; t <= t1; t += 0.2f)
                {
                    if (!a.VisibleEn(t)) continue;
                    var p = a.PosEn(t);
                    // Un salto de sitio (aparecer en otra marca) no es un camino: se corta la línea.
                    if (puntos.Count > 0 && Plano(p - puntos[puntos.Count - 1]).magnitude > 1.5f)
                    {
                        svg.Linea(puntos, colores[a], 3f, 0.9f);
                        puntos.Clear();
                    }
                    puntos.Add(p);
                    alguno = true;
                }
                if (!alguno) continue;
                svg.Linea(puntos, colores[a], 3f, 0.9f);
                for (float t = Mathf.Ceil(t0 / 2f) * 2f; t <= t1; t += 2f)
                {
                    if (!a.VisibleEn(t)) continue;
                    svg.Punto(a.PosEn(t), 2.5f, colores[a], "#000");
                    if (Mathf.Abs(t % 10f) < 0.01f) svg.TextoEn(a.PosEn(t), $"{t:0}s", 10, colores[a], 4, 12);
                }
                float tv = t0 + 0.01f;
                while (tv < t1 && !a.VisibleEn(tv)) tv += 0.2f;
                svg.Punto(a.PosEn(tv), 6f, colores[a], "#fff");
                svg.Flecha(a.PosEn(tv), a.RumboEn(tv, _actores), 16f, colores[a]);
                svg.TextoEn(a.PosEn(tv), a.alias, 14, colores[a], 8, -8);
                var fin = a.PosEn(t1);
                svg.Punto(fin, 4f, "#000", colores[a]);
            }
            int np = 0;
            foreach (var p in _salida.planos)
            {
                np++;
                if (p.t0 < t0 - 1e-3f || p.t0 >= t1) continue;
                Vector3 c = p.PosicionEn(p.t0);
                svg.Cono(c, FocoDePlano(p, p.t0), p.lente, "#ffffff", 0.12f);
                svg.Punto(c, 5f, "#ffffff", "#000");
                svg.TextoEn(c, $"{np}", 13, "#ffffff", 7, 4);
                if (p.posiciones.Count > 1) svg.Linea(p.posiciones.Select(k => k.p).ToList(), "#ffffff", 1.5f, 0.7f, "4 3");
            }
            svg.Texto(new Vector2(12, 26), $"@{nombre}  {Tiempo(t0)} – {Tiempo(t1)}", 22, "#ffffff");
            string archivo = $"{w + 1:00}_{nombre}.svg";
            File.WriteAllText(Path.Combine(carpeta, archivo), svg.Cerrar());
            html.Append($"<h2>{w + 1}. @{nombre} · {Tiempo(t0)} – {Tiempo(t1)}</h2><img src='{archivo}' style='width:100%;max-width:1600px'>\n");
        }
        File.WriteAllText(Path.Combine(carpeta, "recorridos.html"), html.ToString());
    }

    // ── Storyboard: una foto por plano, con los personajes de verdad en su sitio ────────────

    private void Storyboard(GuionHorneado asset)
    {
        string carpeta = Path.Combine(_carpetaSalida, "storyboard");
        Directory.CreateDirectory(carpeta);
        foreach (var f in Directory.GetFiles(carpeta, "*.png")) File.Delete(f);

        var proxies = new Dictionary<ActorH, (GameObject go, GameObject inst, AnimationClip clip)>();
        var materiales = new List<Material>();
        var go = new GameObject("Storyboard · cámara") { hideFlags = HideFlags.HideAndDontSave };
        const int W = 960, H = 540;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Storyboard</title><body style='background:#111;color:#eee;font-family:Segoe UI,Arial'>\n");
        html.Append($"<h1>Storyboard · {_g.nombre}</h1><div style='display:flex;flex-wrap:wrap;gap:14px'>\n");
        bool modo = false;
        try
        {
            foreach (var a in _actores.Values)
            {
                if (a.prefab == null) continue;
                // El prefab va dentro de un soporte: al muestrear el idle, las curvas de raíz mueven
                // el prefab en local y sin soporte lo mandaban al origen del mundo.
                var soporte = new GameObject("Storyboard · " + a.alias) { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(soporte, _escena);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(a.prefab, _escena);
                inst.hideFlags = HideFlags.DontSave;
                inst.transform.SetParent(soporte.transform, false);
                if (!SeVe(inst)) PonerMuneco(inst, a, materiales);
                var anim = inst.GetComponentInChildren<Animator>(true);
                AnimationClip idle = null;
                if (anim != null && anim.runtimeAnimatorController != null)
                    idle = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name.Contains("Idle_Normal"))
                        ?? anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name.Contains("Idle"));
                proxies[a] = (soporte, inst, idle);
            }
            AnimationMode.StartAnimationMode();
            modo = true;

            var cam = go.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.scene = _escena;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            var datos = go.AddComponent<UniversalAdditionalCameraData>();
            datos.renderPostProcessing = true;
            var png = new Texture2D(W, H, TextureFormat.RGB24, false);

            int n = 0;
            foreach (var p in asset.planos)
            {
                n++;
                EditorUtility.DisplayProgressBar("Storyboard", $"Plano {n} de {asset.planos.Count}", n / (float)asset.planos.Count);
                float t = Mathf.Lerp(p.t0, p.t1, 0.35f);
                AnimationMode.BeginSampling();
                foreach (var kv in proxies)
                {
                    var a = kv.Key; var soporte = kv.Value.go;
                    soporte.SetActive(a.VisibleEn(t));
                    soporte.transform.SetPositionAndRotation(a.PosEn(t), Quaternion.Euler(0f, a.RumboEn(t, _actores), 0f));
                    if (kv.Value.clip != null) AnimationMode.SampleAnimationClip(kv.Value.inst, kv.Value.clip, t % Mathf.Max(0.1f, kv.Value.clip.length));
                    kv.Value.inst.transform.localPosition = Vector3.zero;
                    kv.Value.inst.transform.localRotation = Quaternion.identity;
                }
                AnimationMode.EndSampling();

                Vector3 c = p.PosicionEn(t);
                Vector3 foco = FocoDePlano(p, t);
                float lente = p.lenteFinal > 0.1f ? Mathf.Lerp(p.lente, p.lenteFinal, 0.35f) : p.lente;
                float tanV = Mathf.Tan(lente * 0.5f * Mathf.Deg2Rad), tanH = tanV * W / H;
                var mira = Quaternion.LookRotation((foco - c).sqrMagnitude > 1e-4f ? foco - c : Vector3.forward, Vector3.up);
                var giro = mira * Quaternion.Euler(-Mathf.Atan(2f * p.encuadre.y * tanV) * Mathf.Rad2Deg,
                                                   -Mathf.Atan(2f * p.encuadre.x * tanH) * Mathf.Rad2Deg, 0f);
                cam.transform.SetPositionAndRotation(c, giro);
                cam.fieldOfView = lente;
                var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, peticion)) RenderPipeline.SubmitRenderRequest(cam, peticion);
                else cam.Render();
                var previo = RenderTexture.active;
                RenderTexture.active = rt;
                png.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                // Las bandas de cine, como se verán en el juego.
                int alto = Mathf.RoundToInt(BandaEn(t) * H);
                if (alto > 0)
                {
                    var negro = new Color32[W * alto];
                    for (int k = 0; k < negro.Length; k++) negro[k] = new Color32(0, 0, 0, 255);
                    png.SetPixels32(0, 0, W, alto, negro);
                    png.SetPixels32(0, H - alto, W, alto, negro);
                }
                png.Apply();
                RenderTexture.active = previo;
                string archivo = $"{n:000}.png";
                File.WriteAllBytes(Path.Combine(carpeta, archivo), png.EncodeToPNG());

                var lineas = asset.lineas.Where(l => l.t1 > p.t0 && l.t0 < p.t1)
                    .Select(l => $"<b>{_actores.Values.FirstOrDefault(x => x.id == l.actor)?.alias}</b>: {System.Security.SecurityElement.Escape((TextoDe(l.clave) ?? l.clave).Replace("\n", " "))}");
                html.Append($"<div style='width:480px'><img src='{archivo}' style='width:480px'><div><b>{n}</b> · {Tiempo(p.t0)} · {p.t1 - p.t0:0.0} s · {System.Security.SecurityElement.Escape(p.descripcion)}</div>" +
                            $"<div style='font-size:12px;color:#8ab'>{System.Security.SecurityElement.Escape(NotasDePlano.TryGetValue(p.t0, out var nota) ? nota : "")}</div>" +
                            $"<div style='font-size:13px;color:#bbb'>{string.Join("<br>", lineas)}</div></div>\n");
            }
            UnityEngine.Object.DestroyImmediate(png);
        }
        finally
        {
            if (modo) AnimationMode.StopAnimationMode();
            foreach (var kv in proxies) if (kv.Value.go != null) UnityEngine.Object.DestroyImmediate(kv.Value.go);
            foreach (var m in materiales) if (m != null) UnityEngine.Object.DestroyImmediate(m);
            UnityEngine.Object.DestroyImmediate(go);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
        html.Append("</div>");
        File.WriteAllText(Path.Combine(carpeta, "storyboard.html"), html.ToString());
    }

    /// ¿El prefab se ve tal cual? Los aldeanos modulares montan su cuerpo al jugar y en el
    /// Editor salen vacíos.
    private static bool SeVe(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            if (r.enabled && !(r is ParticleSystemRenderer) && r.bounds.size.y > 0.3f) return true;
        return false;
    }

    /// Muñeco de sustitución para el storyboard: cápsula del alto del personaje con una «nariz»
    /// que señala hacia dónde mira, del color de su nombre.
    private static void PonerMuneco(GameObject inst, ActorH a, List<Material> materiales)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Color color = Color.HSVToRGB(Mathf.Abs((a.alias ?? "x").GetHashCode() % 1000) / 1000f, 0.45f, 0.95f);
        if (a.reparto != null && a.reparto.opciones.TryGetValue("color", out var hex) && ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out var c)) color = c;
        var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        materiales.Add(mat);

        var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        UnityEngine.Object.DestroyImmediate(cuerpo.GetComponent<Collider>());
        cuerpo.name = "Muñeco del storyboard";
        cuerpo.hideFlags = HideFlags.DontSave;
        cuerpo.transform.SetParent(inst.transform, false);
        cuerpo.transform.localPosition = new Vector3(0f, a.altura * 0.5f, 0f);
        cuerpo.transform.localScale = new Vector3(0.42f, a.altura * 0.5f, 0.42f);
        cuerpo.GetComponent<Renderer>().sharedMaterial = mat;

        var nariz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.DestroyImmediate(nariz.GetComponent<Collider>());
        nariz.hideFlags = HideFlags.DontSave;
        nariz.transform.SetParent(inst.transform, false);
        nariz.transform.localPosition = new Vector3(0f, a.AlturaDeOjos, 0.22f);
        nariz.transform.localScale = new Vector3(0.12f, 0.08f, 0.16f);
        nariz.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
#endif
