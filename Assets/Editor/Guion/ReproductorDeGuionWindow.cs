#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Reproductor de un guion horneado, con barra de tiempo.
///
/// Sirve para revisar una cinemática larga sin verla entera: se arrastra la barra y la escena
/// salta a ese segundo, con los actores colocados, el plano montado y el estado del mundo
/// aplicado. También sirve para pausar, para leer en qué plano y qué frase se está, y para
/// fotografiar el fotograma exacto que se está viendo.
///
/// Se engancha sola al guion que esté en marcha: no hay que elegir nada.
///
/// Un límite que conviene tener presente: al saltar, el estado del mundo solo avanza hacia
/// delante. Un efecto de clima o de hora que ya se aplicó sigue puesto aunque se salte hacia
/// atrás, porque en la información horneada no hay operaciones inversas. El salto es de cámara
/// y de puesta en escena.
public class ReproductorDeGuionWindow : EditorWindow
{
    private const float AltoDeLaBarra = 40f;
    private const float AltoDeLasMarcas = 9f;
    private const float Margen = 2f;

    private static readonly Color ColorDeFondo = new Color(0.13f, 0.13f, 0.14f);
    private static readonly Color ColorDeFrases = new Color(0.30f, 0.55f, 0.95f, 0.75f);
    private static readonly Color ColorDePlanos = new Color(0.35f, 0.80f, 0.45f, 0.75f);
    private static readonly Color ColorDeEfectos = new Color(0.95f, 0.65f, 0.25f, 0.9f);
    private static readonly Color ColorDelCabezal = new Color(1f, 0.90f, 0.35f);

    private float _entrada;
    private string _entradaTexto = "";
    private GuionBeat _beatDeLaEntrada;
    private bool _arrastrando;

    [MenuItem("El Sendero/Guion/Reproductor", false, 20)]
    private static void Abrir() => GetWindow<ReproductorDeGuionWindow>("Reproductor");

    private void OnEnable()
    {
        EditorApplication.update += AlActualizarElEditor;
    }

    private void OnDisable()
    {
        EditorApplication.update -= AlActualizarElEditor;
    }

    private void AlActualizarElEditor()
    {
        if (GuionEnMarcha.Actual == null) return;
        Repaint();
    }

    private void OnGUI()
    {
        var reproductor = GuionEnMarcha.Actual;

        if (reproductor == null || !reproductor.EnMarcha)
        {
            EditorGUILayout.HelpBox(
                "No hay ningún guion en marcha.\n\n" +
                "Dale a Play y arranca la cinemática: la ventana se engancha sola. " +
                "Si lo que quieres es entrar directamente en un punto, pon el «punto de entrada» " +
                "del asset (abajo) y la secuencia empezará ahí.",
                MessageType.Info);
            DibujarElPuntoDeEntrada(null);
            return;
        }

        var guion = reproductor.Guion;
        float duracion = Mathf.Max(0.01f, reproductor.Duracion);

        EditorGUILayout.LabelField(guion != null ? guion.nombre : "(guion sin nombre)", EditorStyles.boldLabel);

        var barra = GUILayoutUtility.GetRect(0f, AltoDeLaBarra, GUILayout.ExpandWidth(true));
        DibujarLaBarra(barra, reproductor, duracion);

        EditorGUILayout.Space(2f);
        DibujarLosMandos(reproductor);
        EditorGUILayout.Space(4f);
        DibujarLaLectura(reproductor, guion, duracion);
        EditorGUILayout.Space(6f);
        DibujarElPuntoDeEntrada(reproductor);
    }

    // ── La barra ───────────────────────────────────────────────────────────────────────────────

    private void DibujarLaBarra(Rect rect, ReproductorDeGuion reproductor, float duracion)
    {
        EditorGUI.DrawRect(rect, ColorDeFondo);

        var guion = reproductor.Guion;
        float ancho = Mathf.Max(1f, rect.width - Margen * 2f);
        float x0 = rect.x + Margen;

        float yMarcas = rect.y + 1f;
        float yFrases = rect.y + 1f + AltoDeLasMarcas;
        float altoFrases = Mathf.Max(3f, rect.height - (AltoDeLasMarcas + 3f));

        if (guion != null)
        {
            // Los planos, arriba: son el esqueleto de la escena.
            if (guion.planos != null)
                foreach (var plano in guion.planos)
                {
                    float a = x0 + Mathf.Clamp01(plano.t0 / duracion) * ancho;
                    float b = x0 + Mathf.Clamp01(plano.t1 / duracion) * ancho;
                    EditorGUI.DrawRect(new Rect(a, yMarcas, Mathf.Max(1.5f, b - a), AltoDeLasMarcas - 1f), ColorDePlanos);
                }

            // Las frases, debajo: cuándo se está hablando.
            if (guion.lineas != null)
                foreach (var linea in guion.lineas)
                {
                    float a = x0 + Mathf.Clamp01(linea.t0 / duracion) * ancho;
                    float b = x0 + Mathf.Clamp01(linea.t1 / duracion) * ancho;
                    EditorGUI.DrawRect(new Rect(a, yFrases, Mathf.Max(1.5f, b - a), altoFrases), ColorDeFrases);
                }

            // Los efectos, como muescas.
            if (guion.efectos != null)
                foreach (var efecto in guion.efectos)
                {
                    float x = x0 + Mathf.Clamp01(efecto.t / duracion) * ancho;
                    EditorGUI.DrawRect(new Rect(x - 0.5f, yFrases, 1.5f, altoFrases), ColorDeEfectos);
                }
        }

        // El cabezal.
        float xCabeza = x0 + Mathf.Clamp01(reproductor.Tiempo / duracion) * ancho;
        EditorGUI.DrawRect(new Rect(xCabeza - 1f, rect.y, 2f, rect.height), ColorDelCabezal);

        // Arrastrar la barra salta a ese segundo. El arrastre se mantiene aunque el ratón se
        // salga de la barra, para poder llegar al final del guion sin que se escape.
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            _arrastrando = true;
            reproductor.PedirSalto(SegundoEn(x0, ancho, duracion, e.mousePosition.x));
        }
        else if (e.type == EventType.MouseDrag && _arrastrando)
        {
            reproductor.PedirSalto(SegundoEn(x0, ancho, duracion, e.mousePosition.x));
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            _arrastrando = false;
        }
    }

    private static float SegundoEn(float x0, float ancho, float duracion, float xDelRaton)
    {
        float u = Mathf.Clamp01((xDelRaton - x0) / ancho);
        return u * duracion;
    }

    // ── Los mandos ───────────────────────────────────────────────────────────────────────────

    private void DibujarLosMandos(ReproductorDeGuion reproductor)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("|◀", GUILayout.Width(40f))) reproductor.PedirSalto(0f);
            if (GUILayout.Button("◀◀", GUILayout.Width(40f))) reproductor.PedirSalto(reproductor.Tiempo - 5f);
            if (GUILayout.Button(reproductor.Pausado ? "▶" : "❚❚", GUILayout.Width(40f)))
            {
                if (reproductor.Pausado) reproductor.Reanudar(); else reproductor.Pausar();
            }
            if (GUILayout.Button("▶▶", GUILayout.Width(40f))) reproductor.PedirSalto(reproductor.Tiempo + 5f);
            if (GUILayout.Button("▶|", GUILayout.Width(40f))) reproductor.PedirSalto(reproductor.Duracion);

            GUILayout.Space(8f);

            if (GUILayout.Button("Fotograma", GUILayout.Width(90f))) CapturarElFotograma(reproductor);
        }

        // Un campo para saltar a un segundo concreto, que es lo que se usa al afinar el final.
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Ir a (mm:ss)");
            _entradaTexto = EditorGUILayout.TextField(_entradaTexto, GUILayout.Width(90f));
            if (GUILayout.Button("Ir", GUILayout.Width(40f)))
            {
                if (float.TryParse(_entradaTexto, out float segundos)) reproductor.PedirSalto(segundos);
                else if (TryParseMinutosYSegundos(_entradaTexto, out float mmss)) reproductor.PedirSalto(mmss);
            }
        }
    }

    private static bool TryParseMinutosYSegundos(string texto, out float segundos)
    {
        segundos = 0f;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var partes = texto.Split(':');
        if (partes.Length != 2) return false;
        if (!float.TryParse(partes[0], out float min) || !float.TryParse(partes[1], out float seg)) return false;
        segundos = min * 60f + seg;
        return true;
    }

    // ── La lectura ───────────────────────────────────────────────────────────────────────────

    private void DibujarLaLectura(ReproductorDeGuion reproductor, GuionHorneado guion, float duracion)
    {
        var plano = reproductor.PlanoActual;
        var linea = reproductor.LineaActual;

        EditorGUILayout.LabelField("Tiempo", $"{Formato(reproductor.Tiempo)} / {Formato(duracion)}");

        string numeroDelPlano = "—";
        if (plano != null && guion?.planos != null)
        {
            int indice = guion.planos.IndexOf(plano);
            if (indice >= 0) numeroDelPlano = $"{indice + 1} de {guion.planos.Count}";
        }
        EditorGUILayout.LabelField("Plano", plano != null
            ? $"{numeroDelPlano} · {plano.descripcion}" : "—");

        EditorGUILayout.LabelField("Frase", linea != null ? linea.clave : "—");
    }

    private static string Formato(float segundos)
    {
        if (segundos < 0f) segundos = 0f;
        int minutos = (int)(segundos / 60f);
        float resto = segundos - minutos * 60f;
        return $"{minutos:0}:{resto:00.0}";
    }

    // ── El punto de entrada ──────────────────────────────────────────────────────────────────

    /// El punto de entrada es el segundo por el que la secuencia empieza. Sirve para no verse
    /// la cinemática entera cada vez que hay que retocar el final: se deja puesto el segundo
    /// que interesa y la secuencia entra ahí, con el mundo ya en su estado.
    private void DibujarElPuntoDeEntrada(ReproductorDeGuion reproductor)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Punto de entrada", EditorStyles.boldLabel);

        var (definicion, beat) = BuscarElBeatDelGuion(reproductor);
        if (beat == null)
        {
            _beatDeLaEntrada = null;
            EditorGUILayout.HelpBox(
                "No hay ningún guion en marcha, así que no se puede ver su punto de entrada. " +
                "Se puede cambiar a mano en el asset de la secuencia (campo 'Empezar en', dentro " +
                "del beat del guion).",
                MessageType.None);
            return;
        }

        // El campo refleja lo que hay en el asset, y solo se sincroniza cuando cambia el beat:
        // si no, escribiría sobre él lo que se está tecleando.
        if (_beatDeLaEntrada != beat)
        {
            _beatDeLaEntrada = beat;
            _entrada = beat.empezarEn;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _entrada = EditorGUILayout.FloatField("Empezar en (s)", _entrada, GUILayout.Width(160f));
            if (GUILayout.Button("Usar el momento actual", GUILayout.Width(170f)))
                _entrada = reproductor != null ? reproductor.Tiempo : beat.empezarEn;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !Mathf.Approximately(_entrada, beat.empezarEn);
            if (GUILayout.Button("Guardar en el asset"))
                GuardarElPuntoDeEntrada(definicion, beat, _entrada);
            GUI.enabled = true;
        }

        EditorGUILayout.HelpBox(
            "El punto de entrada solo se usa en el Editor: en el juego la secuencia siempre " +
            "empieza por el principio. Acuérdate de vaciarlo antes de dar la escena por buena.",
            MessageType.None);
    }

    /// Guardar un asset desde el dibujado de la ventana puede fallar: se hace en el siguiente
    /// ciclo del Editor, fuera del OnGUI.
    private static void GuardarElPuntoDeEntrada(SequenceDefinition definicion, GuionBeat beat, float segundo)
    {
        EditorApplication.delayCall += () =>
        {
            if (definicion == null || beat == null) return;
            float tope = beat.guion != null ? beat.guion.duracion : 99999f;
            Undo.RecordObject(definicion, "Punto de entrada del guion");
            beat.empezarEn = Mathf.Clamp(segundo, 0f, tope);
            EditorUtility.SetDirty(definicion);
            AssetDatabase.SaveAssets();
        };
    }

    /// Busca el beat del guion que está en marcha dentro de su definición.
    private static (SequenceDefinition, GuionBeat) BuscarElBeatDelGuion(ReproductorDeGuion reproductor)
    {
        if (reproductor == null) return (null, null);
        var definicion = reproductor.Player?.Definition;
        if (definicion?.phases == null) return (null, null);

        foreach (var fase in definicion.phases)
        {
            if (fase?.beats == null) continue;
            foreach (var beat in fase.beats)
            {
                if (beat is GuionBeat gb && gb.guion == reproductor.Guion) return (definicion, gb);
            }
        }
        return (definicion, null);
    }

    // ── El fotograma ─────────────────────────────────────────────────────────────────────────

    private void CapturarElFotograma(ReproductorDeGuion reproductor)
    {
        try
        {
            string carpeta = Path.Combine(Application.dataPath, "..", "Logs", "Reproductor");
            Directory.CreateDirectory(carpeta);
            string nombre = (reproductor.Guion != null ? reproductor.Guion.nombre : "guion") + "_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string ruta = Path.GetFullPath(Path.Combine(carpeta, nombre));
            ScreenCapture.CaptureScreenshot(ruta);
            Debug.Log($"[Reproductor] Fotograma: {ruta}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Reproductor] No se pudo capturar el fotograma: {ex.Message}");
        }
    }
}
#endif
