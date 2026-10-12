using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Abre o cierra las piernas de clips humanoides sin tocar los originales ni el avatar.
///
/// Los clips de los packs están hechos sobre un cuerpo de proporciones normales. Con el músculo
/// «Upper Leg In-Out» en posición neutra las piernas cuelgan rectas, y en un chibi de muslos anchos
/// eso las deja pegadas o cruzadas. Ver INC-701. Recortar rangos del avatar no sirve
/// aquí: el neutro (0) sigue siendo 0 con cualquier rango. Lo que funciona es sumar apertura a ese
/// músculo en el propio clip.
///
/// Por cada clip de origen se genera una copia en <see cref="CarpetaDeCopias"/> con la apertura
/// sumada (siempre a partir del original, así que aplicar varias veces no acumula) y todos los
/// Animator Controller del proyecto que usaban el original pasan a usar la copia. La copia conserva
/// su GUID entre aplicaciones, de modo que retocar la apertura no rompe referencias.
/// </summary>
public class AperturaDePiernasWindow : EditorWindow
{
    private const string CarpetaDeCopias = "Assets/Art/Animations/Ajustadas";
    private const string SufijoDeCopia = " (piernas ajustadas)";
    private const string PrefsApertura = "ElSendero.AperturaDePiernas.Apertura";

    private static readonly string[] MusculosDeApertura = { "Left Upper Leg In-Out", "Right Upper Leg In-Out" };

    /// Clips con los que se abre la ventana: el vuelo del jugador.
    private static readonly string[] ClipsPorDefecto =
    {
        "Assets/Art/Fly Animations/Animations/Fly/fly_idle.anim",
        "Assets/Art/Fly Animations/Animations/Fly/fly_dive.anim",
    };

    private readonly List<AnimationClip> _origenes = new List<AnimationClip>();
    private float _apertura = 0.25f;
    private string _informe;
    private float _aplicada = float.NaN;   // apertura que llevan las copias; se recalcula al abrir y al aplicar
    private Vector2 _scroll;

    [MenuItem("El Sendero/Personajes/Apertura de piernas en animaciones")]
    public static void Abrir()
    {
        var w = GetWindow<AperturaDePiernasWindow>("Apertura de piernas");
        w.minSize = new Vector2(420, 320);
        w.Show();
    }

    private void OnEnable()
    {
        if (_origenes.Count == 0)
        {
            foreach (string ruta in ClipsPorDefecto)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
                if (clip) _origenes.Add(clip);
            }
        }
        _apertura = EditorPrefs.GetFloat(PrefsApertura, _apertura);
        _aplicada = AperturaAplicada();
        if (!float.IsNaN(_aplicada)) _apertura = _aplicada;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Abre (o cierra) las piernas de las animaciones de la lista. Los originales no se tocan: " +
            "se crea una copia ajustada y los Animator la usan en su lugar. Aplicar otra vez rehace las " +
            "copias desde el original con el valor nuevo.", MessageType.Info);

        EditorGUILayout.LabelField("Animaciones", EditorStyles.boldLabel);
        for (int i = 0; i < _origenes.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _origenes[i] = (AnimationClip)EditorGUILayout.ObjectField(_origenes[i], typeof(AnimationClip), false);
            if (GUILayout.Button("✕", GUILayout.Width(24))) { _origenes.RemoveAt(i); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Añadir animación")) _origenes.Add(null);

        EditorGUILayout.Space();
        _apertura = EditorGUILayout.Slider(
            new GUIContent("Apertura", "Cuánto se separan las piernas. 0 = como el original; positivo abre, negativo cierra."),
            _apertura, -0.5f, 1f);

        EditorGUILayout.LabelField("Aplicada ahora", float.IsNaN(_aplicada) ? "ninguna (se usan los originales)" : _aplicada.ToString("0.00"));

        EditorGUILayout.Space();
        if (GUILayout.Button("Aplicar", GUILayout.Height(30))) Aplicar();

        if (!string.IsNullOrEmpty(_informe)) EditorGUILayout.HelpBox(_informe, MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    // ── Aplicar ───────────────────────────────────────────────────────────────────────────────

    private void Aplicar()
    {
        EditorPrefs.SetFloat(PrefsApertura, _apertura);
        CrearCarpeta(CarpetaDeCopias);

        var cambios = new Dictionary<AnimationClip, AnimationClip>();
        var informe = new System.Text.StringBuilder();
        foreach (var origen in _origenes)
        {
            if (!origen) continue;
            if (!origen.humanMotion) { informe.AppendLine($"{origen.name}: no es humanoide, se salta."); continue; }
            if (EsCopia(origen)) { informe.AppendLine($"{origen.name}: ya es una copia ajustada; pon el original."); continue; }
            cambios[origen] = CrearOActualizarCopia(origen);
        }

        int estados = SustituirEnControllers(cambios, informe);
        AssetDatabase.SaveAssets();
        _aplicada = AperturaAplicada();

        informe.Insert(0, $"Apertura {_apertura:0.00} aplicada a {cambios.Count} animación(es); {estados} estado(s) de Animator apuntan ahora a la copia.\n");
        _informe = informe.ToString().TrimEnd();
    }

    /// Copia del clip (misma GUID en cada aplicación) con la apertura sumada a los músculos de las piernas.
    private AnimationClip CrearOActualizarCopia(AnimationClip origen)
    {
        string ruta = $"{CarpetaDeCopias}/{origen.name}{SufijoDeCopia}.anim";
        var copia = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        if (!copia)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(origen), ruta);
            copia = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        }
        else
        {
            EditorUtility.CopySerialized(origen, copia);
        }
        copia.name = Path.GetFileNameWithoutExtension(ruta);

        foreach (string musculo in MusculosDeApertura)
        {
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), musculo);
            var curva = AnimationUtility.GetEditorCurve(origen, binding);
            if (curva == null || curva.length == 0)
            {
                curva = AnimationCurve.Constant(0f, Mathf.Max(origen.length, 1f / 30f), _apertura);
            }
            else
            {
                var claves = curva.keys;
                for (int k = 0; k < claves.Length; k++) claves[k].value += _apertura;
                curva.keys = claves;
            }
            AnimationUtility.SetEditorCurve(copia, binding, curva);
        }

        EditorUtility.SetDirty(copia);
        return copia;
    }

    /// Cambia el original por la copia en los estados (y árboles de mezcla) de todos los Animator Controller.
    private static int SustituirEnControllers(Dictionary<AnimationClip, AnimationClip> cambios, System.Text.StringBuilder informe)
    {
        if (cambios.Count == 0) return 0;
        int total = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            if (!controller) continue;
            int enEste = 0;
            foreach (var capa in controller.layers)
                enEste += SustituirEnMaquina(capa.stateMachine, cambios);
            if (enEste == 0) continue;
            EditorUtility.SetDirty(controller);
            total += enEste;
            informe.AppendLine($"{controller.name}: {enEste} estado(s).");
        }
        return total;
    }

    private static int SustituirEnMaquina(AnimatorStateMachine maquina, Dictionary<AnimationClip, AnimationClip> cambios)
    {
        if (!maquina) return 0;
        int n = 0;
        foreach (var hijo in maquina.states)
        {
            var estado = hijo.state;
            if (estado.motion is AnimationClip clip && cambios.TryGetValue(clip, out var copia))
            {
                estado.motion = copia;
                EditorUtility.SetDirty(estado);
                n++;
            }
            else if (estado.motion is BlendTree arbol)
            {
                n += SustituirEnArbol(arbol, cambios);
            }
        }
        foreach (var sub in maquina.stateMachines)
            n += SustituirEnMaquina(sub.stateMachine, cambios);
        return n;
    }

    private static int SustituirEnArbol(BlendTree arbol, Dictionary<AnimationClip, AnimationClip> cambios)
    {
        int n = 0;
        var hijos = arbol.children;
        for (int i = 0; i < hijos.Length; i++)
        {
            if (hijos[i].motion is AnimationClip clip && cambios.TryGetValue(clip, out var copia))
            {
                hijos[i].motion = copia;
                n++;
            }
            else if (hijos[i].motion is BlendTree sub)
            {
                n += SustituirEnArbol(sub, cambios);
            }
        }
        if (n > 0)
        {
            arbol.children = hijos;
            EditorUtility.SetDirty(arbol);
        }
        return n;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────────────────────

    /// Apertura que llevan las copias existentes (diferencia con su original), o NaN si no hay copias.
    private float AperturaAplicada()
    {
        foreach (var origen in _origenes)
        {
            if (!origen) continue;
            var copia = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{CarpetaDeCopias}/{origen.name}{SufijoDeCopia}.anim");
            if (!copia) continue;
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), MusculosDeApertura[0]);
            var enCopia = AnimationUtility.GetEditorCurve(copia, binding);
            if (enCopia == null || enCopia.length == 0) continue;
            var enOrigen = AnimationUtility.GetEditorCurve(origen, binding);
            float base0 = enOrigen != null && enOrigen.length > 0 ? enOrigen.keys[0].value : 0f;
            return enCopia.keys[0].value - base0;
        }
        return float.NaN;
    }

    private static bool EsCopia(AnimationClip clip) =>
        AssetDatabase.GetAssetPath(clip).StartsWith(CarpetaDeCopias + "/");

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
}
