using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Renderiza a PNG todos los planos de una secuencia, SIN entrar en Play.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// Los planos de este juego no se colocan: se calculan (ver ShotComposer). Eso es lo que hace que
/// no caduquen cuando algo se mueve, pero tiene una consecuencia incómoda: mientras no le des a
/// Play no sabes qué encuadra cada corte, y por tanto no sabes qué parte del decorado se ve. Se
/// puede vestir un valle entero y que la cámara no mire a ninguna de esas cosas ni una vez — que
/// es exactamente lo que había pasado en el prólogo: ciento veinte objetos colocados, y detrás del
/// Archimago, hierba.
///
/// Esta ventana hace una pasada en seco de la secuencia: recorre los beats en orden, aplica los
/// que cambian dónde está o hacia dónde mira cada actor (colocar, mover, girar), y cada vez que
/// encuentra un plano lo resuelve CON EL MISMO ShotComposer que usa el juego y lo guarda como PNG.
/// No es una aproximación: es el encuadre de verdad.
///
/// Al terminar deja la escena exactamente como estaba — posiciones y rotaciones incluidas.
public class SequenceShotCapture : EditorWindow
{
    private SequencePlayer _player;
    private int _width = 1920;
    private int _height = 1080;
    private string _folder = "Claude outputs/Planos";
    private bool _includeConditionalPhases = true;
    private Vector2 _scroll;
    private readonly List<string> _log = new();

    [MenuItem("El Sendero/Secuencias/Capturar los planos de una secuencia...")]
    public static void Open()
    {
        var window = GetWindow<SequenceShotCapture>(true, "Capturar planos");
        window.minSize = new Vector2(440, 420);
        window.AutoPick();
        window.Show();
    }

    private void AutoPick()
    {
        if (_player != null) return;
        var candidatos = FindObjectsByType<SequencePlayer>(FindObjectsInactive.Include);
        if (candidatos.Length > 0) _player = candidatos[0];
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Recorre la secuencia en seco y guarda un PNG por cada plano, con el encuadre real.\n\n" +
            "No entra en Play y no cambia la escena: al terminar devuelve a cada actor a donde estaba.",
            MessageType.Info);

        _player = (SequencePlayer)EditorGUILayout.ObjectField("Secuencia en escena", _player, typeof(SequencePlayer), true);

        using (new EditorGUI.DisabledScope(true))
        {
            var def = _player != null ? _player.Definition : null;
            EditorGUILayout.ObjectField("Asset de la secuencia", def, typeof(SequenceDefinition), false);
        }

        _width = EditorGUILayout.IntField("Ancho", Mathf.Clamp(_width, 320, 3840));
        _height = EditorGUILayout.IntField("Alto", Mathf.Clamp(_height, 240, 2160));
        _folder = EditorGUILayout.TextField("Carpeta (en el proyecto)", _folder);
        _includeConditionalPhases = EditorGUILayout.Toggle(
            new GUIContent("Incluir fases condicionales",
                "Las fases con 'onlyIfFlag' (las ramas alternativas) también se capturan, porque " +
                "sus planos también hay que mirarlos."),
            _includeConditionalPhases);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(_player == null || _player.Definition == null))
        {
            if (GUILayout.Button("Capturar planos", GUILayout.Height(34))) Capture();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Además, para ver el decorado como un todo:", EditorStyles.miniLabel);
        if (GUILayout.Button("Capturar vistas generales del decorado", GUILayout.Height(24)))
            CaptureOverviews();

        if (_log.Count == 0) return;

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var line in _log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    /// Cuatro vistas del decorado entero: cenital, y tres a la altura de un pájaro desde el este,
    /// el oeste y el sur. Los planos de la secuencia dicen si cada encuadre funciona; estas dicen
    /// si el sitio se sostiene como sitio, que es la otra mitad del problema.
    private void CaptureOverviews()
    {
        _log.Clear();

        Vector3 centro = _player != null ? _player.transform.position : Vector3.zero;
        if (_player != null && _player.Stage != null)
        {
            var marca = _player.Stage.GetMark("M_Apertura");
            if (marca != null) centro = marca.position;
        }

        var vistas = new (string nombre, Vector3 desplazamiento, float fov, bool orto, float tamOrto)[]
        {
            ("cenital",       new Vector3(0f, 78f, 0.01f), 60f, true,  34f),
            ("desde_el_este", new Vector3(34f, 17f, -6f),  50f, false, 0f),
            ("desde_el_oeste",new Vector3(-34f, 17f, 8f),  50f, false, 0f),
            ("desde_el_sur",  new Vector3(4f, 14f, -34f),  50f, false, 0f),
        };

        string raiz = Path.Combine(Directory.GetCurrentDirectory(), _folder);
        Directory.CreateDirectory(raiz);

        var camGo = new GameObject("~VistaGeneral") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 900f;
        cam.enabled = false;

        var rt = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var png = new Texture2D(_width, _height, TextureFormat.RGB24, false);

        try
        {
            foreach (var v in vistas)
            {
                Vector3 pos = centro + v.desplazamiento;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((centro - pos).normalized, Vector3.up));
                cam.orthographic = v.orto;
                cam.orthographicSize = v.tamOrto;
                cam.fieldOfView = v.fov;
                cam.targetTexture = rt;

                var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, peticion)) RenderPipeline.SubmitRenderRequest(cam, peticion);
                else cam.Render();

                var previo = RenderTexture.active;
                RenderTexture.active = rt;
                png.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
                png.Apply();
                RenderTexture.active = previo;
                cam.targetTexture = null;

                File.WriteAllBytes(Path.Combine(raiz, "00_vista_" + v.nombre + ".png"), png.EncodeToPNG());
                _log.Add("vista " + v.nombre);
            }
        }
        finally
        {
            DestroyImmediate(camGo);
            DestroyImmediate(png);
            rt.Release();
            DestroyImmediate(rt);
        }

        _log.Insert(0, "■ 4 vistas generales guardadas en: " + _folder);
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(raiz);
    }

    private void Capture()
    {
        _log.Clear();

        var def = _player.Definition;
        var stage = _player.Stage;
        var ctx = new SequenceContext(_player, stage);

        // Los objetos del decorado dados de alta como actores (el horno, la carreta): el juego los
        // registra al empezar la secuencia, así que aquí hay que hacer lo mismo o los planos que
        // los nombran no se pueden resolver.
        if (stage != null && stage.Props != null)
        {
            foreach (var prop in stage.Props)
                if (prop.target != null && !string.IsNullOrWhiteSpace(prop.id))
                    ctx.RegisterActor(prop.id.Trim(), prop.target, prop.eyeHeight);
        }

        // Estado original de todo lo que la pasada en seco pueda mover.
        var originales = new Dictionary<Transform, (Vector3 pos, Quaternion rot)>();
        void Recordar(Transform t)
        {
            if (t != null && !originales.ContainsKey(t)) originales[t] = (t.position, t.rotation);
        }

        string raiz = Path.Combine(Directory.GetCurrentDirectory(), _folder);
        Directory.CreateDirectory(raiz);

        // La luz: si hay un ciclo día/noche abierto, los beats de hora del día se aplican de verdad
        // para que el PNG tenga el cielo y el sol que tendrá el plano en partida. Se guarda todo lo
        // que se toca y se restaura al final: esta herramienta no puede dejar la escena cambiada.
        var ciclo = FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        bool avisadoSinCiclo = false;

        var skyboxPrevio = RenderSettings.skybox;
        var fogPrevio = RenderSettings.fog;
        var fogColorPrevio = RenderSettings.fogColor;
        var fogDensidadPrevia = RenderSettings.fogDensity;
        var ambientePrevio = RenderSettings.ambientLight;
        var modoAmbientePrevio = RenderSettings.ambientMode;

        Light solPrevio = null;
        Color solColorPrevio = Color.white;
        float solIntensidadPrevia = 1f;
        Quaternion solRotacionPrevia = Quaternion.identity;
        foreach (var luz in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (luz.type != LightType.Directional) continue;
            solPrevio = luz;
            solColorPrevio = luz.color;
            solIntensidadPrevia = luz.intensity;
            solRotacionPrevia = luz.transform.rotation;
            break;
        }

        var camGo = new GameObject("~CapturaDePlanos") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 900f;
        cam.enabled = false;

        var rt = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var png = new Texture2D(_width, _height, TextureFormat.RGB24, false);
        float aspect = (float)_width / _height;

        int indice = 0;
        int guardados = 0;

        try
        {
            foreach (var fase in def.phases)
            {
                if (fase == null || fase.beats == null) continue;
                if (!_includeConditionalPhases && !string.IsNullOrWhiteSpace(fase.onlyIfFlag)) continue;

                foreach (var beat in fase.beats)
                {
                    if (beat == null) continue;
                    indice++;

                    // ── beats que cambian dónde está la gente ──────────────────
                    if (beat is PlaceAtMarkBeat place)
                    {
                        var actor = ctx.GetActor(place.actorId);
                        var mark = stage != null ? stage.GetMark(place.markName) : null;
                        if (actor?.Transform != null && mark != null)
                        {
                            Recordar(actor.Transform);
                            actor.Transform.SetPositionAndRotation(mark.position, mark.rotation);

                            Transform objetivo = null;
                            if (!string.IsNullOrWhiteSpace(place.faceTowardsActor))
                                objetivo = ctx.GetActor(place.faceTowardsActor)?.Transform;
                            if (objetivo == null && !string.IsNullOrWhiteSpace(place.faceTowardsMark) && stage != null)
                                objetivo = stage.GetMark(place.faceTowardsMark);
                            if (objetivo != null) Encarar(actor.Transform, objetivo.position);
                        }
                        continue;
                    }

                    if (beat is MoveToBeat move)
                    {
                        var actor = ctx.GetActor(move.actorId);
                        if (actor?.Transform != null)
                        {
                            Recordar(actor.Transform);
                            var destino = ctx.GetActor(move.towardsActorId)?.Transform;
                            if (destino != null)
                            {
                                Vector3 punto = destino.position - destino.forward * move.stopDistance;
                                actor.Transform.position = punto;
                                Encarar(actor.Transform, destino.position);
                            }
                            else
                            {
                                var mark = stage != null ? stage.GetMark(move.markName) : null;
                                if (mark != null) actor.Transform.position = mark.position;
                            }
                        }
                        continue;
                    }

                    if (beat is FaceBeat face)
                    {
                        var actor = ctx.GetActor(face.actorId);
                        if (actor?.Transform != null)
                        {
                            Recordar(actor.Transform);
                            Transform objetivo = ctx.GetActor(face.targetActorId)?.Transform;
                            Vector3 punto = objetivo != null
                                ? objetivo.position
                                : (stage != null && stage.GetMark(face.markName) != null
                                    ? stage.GetMark(face.markName).position
                                    : actor.Transform.position + actor.Transform.forward);
                            if (face.lookAway) punto = actor.Transform.position * 2f - punto;
                            Encarar(actor.Transform, punto);

                            if (face.mutual && objetivo != null) { Recordar(objetivo); Encarar(objetivo, actor.Transform.position); }
                        }
                        continue;
                    }

                    if (beat is TimeOfDayBeat hora)
                    {
                        if (ciclo != null) ciclo.PreviewTimeOfDay(hora.timeOfDay);
                        else if (!avisadoSinCiclo)
                        {
                            avisadoSinCiclo = true;
                            _log.Add("· Sin DayNightCycle en las escenas abiertas: los planos se capturan " +
                                     "con la luz guardada en la escena, no con la que tendrán en partida. " +
                                     "Abre MainWorld y carga el prólogo encima para verlo de verdad.");
                        }
                        continue;
                    }

                    if (beat is SetActionAxisBeat eje)
                    {
                        float rad = eje.sideDegrees * Mathf.Deg2Rad;
                        ctx.SetActionSide(new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)));
                        continue;
                    }

                    // ── el plano ───────────────────────────────────────────────
                    if (beat is not ShotBeat shot || shot.framing == null) continue;

                    if (!ShotComposer.TrySolve(ctx, shot.framing, aspect, out var solucion))
                    {
                        _log.Add($"✗ beat {indice} ({fase.name}): no se pudo resolver '{shot.framing.Describe()}'");
                        continue;
                    }

                    cam.transform.SetPositionAndRotation(solucion.position, solucion.rotation);
                    cam.fieldOfView = solucion.fieldOfView;
                    cam.targetTexture = rt;

                    // En URP la forma soportada de renderizar una cámara a mano es la petición de
                    // render; Camera.Render() a secas se queda corto (no pasa por el renderer del
                    // pipeline) y devuelve una imagen sin post ni sombras. Se deja el Render() de
                    // toda la vida como red de seguridad por si el pipeline no acepta la petición.
                    var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, peticion))
                        RenderPipeline.SubmitRenderRequest(cam, peticion);
                    else
                        cam.Render();

                    var previo = RenderTexture.active;
                    RenderTexture.active = rt;
                    png.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
                    png.Apply();
                    RenderTexture.active = previo;
                    cam.targetTexture = null;

                    guardados++;
                    string nombre = $"{guardados:D2}_{Limpiar(fase.name)}_{Limpiar(shot.framing.Describe())}.png";
                    File.WriteAllBytes(Path.Combine(raiz, nombre), png.EncodeToPNG());

                    _log.Add($"{guardados:D2}  {fase.name} · {shot.framing.Describe()}  " +
                             $"[cám {solucion.position.x:F1},{solucion.position.y:F1},{solucion.position.z:F1} · {solucion.fieldOfView:F0}°]");
                }
            }
        }
        finally
        {
            foreach (var kv in originales)
                if (kv.Key != null) kv.Key.SetPositionAndRotation(kv.Value.pos, kv.Value.rot);

            RenderSettings.skybox = skyboxPrevio;
            RenderSettings.fog = fogPrevio;
            RenderSettings.fogColor = fogColorPrevio;
            RenderSettings.fogDensity = fogDensidadPrevia;
            RenderSettings.ambientLight = ambientePrevio;
            RenderSettings.ambientMode = modoAmbientePrevio;

            if (solPrevio != null)
            {
                solPrevio.color = solColorPrevio;
                solPrevio.intensity = solIntensidadPrevia;
                solPrevio.transform.rotation = solRotacionPrevia;
            }

            DestroyImmediate(camGo);
            DestroyImmediate(png);
            rt.Release();
            DestroyImmediate(rt);
        }

        _log.Insert(0, $"■ {guardados} planos guardados en: {_folder}");
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(raiz);
        Debug.Log($"[SequenceShotCapture] {guardados} planos guardados en {raiz}");
    }

    /// Gira solo en horizontal: inclinar a un personaje para mirar algo más alto lo deja torcido,
    /// mismo criterio que FaceBeat.
    private static void Encarar(Transform quien, Vector3 punto)
    {
        Vector3 dir = punto - quien.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) quien.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private static string Limpiar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "plano";
        foreach (char c in Path.GetInvalidFileNameChars()) texto = texto.Replace(c, '_');
        return texto.Replace(' ', '_').Replace("'", "");
    }
}
