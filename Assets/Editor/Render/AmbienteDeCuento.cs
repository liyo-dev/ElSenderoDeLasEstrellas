#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Ambiente «de cuento» (INC-1353): luz del día cálida con ambiente algo más frío (la sombra tira a
/// azul, como en una ilustración), bruma azulada a lo lejos que separa los planos, y en el postprocesado
/// general los verdes menos chillones (curva de saturación por tono), un punto de calidez y las sombras
/// del color un poco hacia el azul. Se aplica a la paleta de franjas del DayNightCycle de MainWorld (la
/// escena tiene que estar abierta; solo se guarda esa escena) y al perfil general de volumen. Antes de
/// tocar nada guarda una copia; «volver» la repone.
public static class AmbienteDeCuento
{
    private const string RutaDelPerfil = "Assets/Settings/Volumenes/Volumen Profile.asset";
    private const string Carpeta = "Assets/Settings/AspectoDeDibujo/Antes del cuento";
    private const string CopiaDelPerfil = Carpeta + "/Volumen Profile (antes del cuento).asset";
    private const string CopiaDelCiclo = Carpeta + "/Ciclo de MainWorld (antes del cuento).json";

    private struct Franja { public DayNightCycle.TimeOfDay cual; public Color luz; public float intensidad; public Color ambiente; public float intensidadDelAmbiente; public Color niebla; public float densidad; }

    private static readonly Franja[] Paleta =
    {
        new Franja { cual = DayNightCycle.TimeOfDay.Morning, luz = new Color(1f, 0.88f, 0.74f), intensidad = 1.1f,
            ambiente = new Color(0.36f, 0.36f, 0.48f), intensidadDelAmbiente = 0.9f, niebla = new Color(0.80f, 0.83f, 0.95f), densidad = 0.0065f },
        new Franja { cual = DayNightCycle.TimeOfDay.AfterNoon, luz = new Color(1f, 0.93f, 0.80f), intensidad = 1.3f,
            ambiente = new Color(0.40f, 0.41f, 0.46f), intensidadDelAmbiente = 1.1f, niebla = new Color(0.72f, 0.82f, 0.95f), densidad = 0.0045f },
        new Franja { cual = DayNightCycle.TimeOfDay.Sunset, luz = new Color(1f, 0.70f, 0.48f), intensidad = 0.9f,
            ambiente = new Color(0.40f, 0.30f, 0.38f), intensidadDelAmbiente = 0.85f, niebla = new Color(0.92f, 0.66f, 0.55f), densidad = 0.011f },
        new Franja { cual = DayNightCycle.TimeOfDay.Night, luz = new Color(0.60f, 0.70f, 1f), intensidad = 0.6f,
            ambiente = new Color(0.18f, 0.19f, 0.36f), intensidadDelAmbiente = 0.85f, niebla = new Color(0.12f, 0.13f, 0.26f), densidad = 0.013f },
    };

    [MenuItem("El Sendero/Aspecto de dibujo/Ambiente de cuento: aplicar")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { OrdenesDelEditor.Avisar("Cuento", "sal de Play antes"); return; }
        var ciclo = CicloDeMainWorld();
        if (ciclo == null) { OrdenesDelEditor.Avisar("Cuento", "abre MainWorld (también aditiva) antes"); return; }
        Directory.CreateDirectory(Carpeta);
        if (!File.Exists(CopiaDelCiclo)) File.WriteAllText(CopiaDelCiclo, EditorJsonUtility.ToJson(ciclo));
        if (!File.Exists(CopiaDelPerfil)) AssetDatabase.CopyAsset(RutaDelPerfil, CopiaDelPerfil);

        var so = new SerializedObject(ciclo);
        var franjas = so.FindProperty("timeSettings");
        for (int i = 0; i < franjas.arraySize; i++)
        {
            var f = franjas.GetArrayElementAtIndex(i);
            var cual = (DayNightCycle.TimeOfDay)f.FindPropertyRelative("timeOfDay").intValue;
            foreach (var p in Paleta)
            {
                if (p.cual != cual) continue;
                f.FindPropertyRelative("lightColor").colorValue = p.luz;
                f.FindPropertyRelative("lightIntensity").floatValue = p.intensidad;
                f.FindPropertyRelative("ambientColor").colorValue = p.ambiente;
                f.FindPropertyRelative("ambientIntensity").floatValue = p.intensidadDelAmbiente;
                f.FindPropertyRelative("fogColor").colorValue = p.niebla;
                f.FindPropertyRelative("fogDensity").floatValue = p.densidad;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ciclo);
        EditorSceneManager.MarkSceneDirty(ciclo.gameObject.scene);
        EditorSceneManager.SaveScene(ciclo.gameObject.scene);

        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfil);
        PostprocesoDeCuento(perfil);
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Cuento", "ambiente de cuento aplicado (ciclo de MainWorld y perfil general)");
    }

    private static void PostprocesoDeCuento(VolumeProfile perfil)
    {
        // Verdes menos chillones: la saturación baja en torno al verde (tono 0,33) y nada en el resto.
        var curvas = Obtener<ColorCurves>(perfil);
        var claves = new[] { new Keyframe(0.18f, 0.5f), new Keyframe(0.28f, 0.45f), new Keyframe(0.36f, 0.45f), new Keyframe(0.46f, 0.5f) };
        curvas.hueVsSat.Override(new TextureCurve(claves, 0.5f, true, new Vector2(0f, 1f)));
        // Un punto de calidez en la luz y las sombras del color algo hacia el azul.
        var blanco = Obtener<WhiteBalance>(perfil);
        blanco.temperature.Override(5f);
        var reparto = Obtener<SplitToning>(perfil);
        reparto.shadows.Override(new Color(0.42f, 0.47f, 0.62f));
        reparto.highlights.Override(new Color(0.62f, 0.55f, 0.46f));
        reparto.balance.Override(-25f);
        var ajustes = Obtener<ColorAdjustments>(perfil);
        ajustes.saturation.Override(0f);
        ajustes.contrast.Override(0f);
        // El verde puro del pack tira un poco hacia el amarillo: verde de prado, no de neón.
        var tonos = curvas.hueVsHue;
        tonos.Override(new TextureCurve(new[] { new Keyframe(0.20f, 0.5f), new Keyframe(0.33f, 0.485f), new Keyframe(0.45f, 0.5f) }, 0.5f, true, new Vector2(0f, 1f)));
    }

    private static T Obtener<T>(VolumeProfile perfil) where T : VolumeComponent
    {
        if (!perfil.TryGet(out T c)) c = perfil.Add<T>(false);
        c.active = true;
        return c;
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Ambiente de cuento: volver al de antes")]
    public static void Volver()
    {
        var ciclo = CicloDeMainWorld();
        if (ciclo != null && File.Exists(CopiaDelCiclo))
        {
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(CopiaDelCiclo), ciclo);
            EditorUtility.SetDirty(ciclo);
            EditorSceneManager.MarkSceneDirty(ciclo.gameObject.scene);
            EditorSceneManager.SaveScene(ciclo.gameObject.scene);
        }
        var copia = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CopiaDelPerfil);
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfil);
        if (copia != null && perfil != null)
        {
            EditorUtility.CopySerialized(copia, perfil);
            perfil.name = Path.GetFileNameWithoutExtension(RutaDelPerfil);
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
        }
        OrdenesDelEditor.Avisar("Cuento", "vuelto al ambiente de antes");
    }

    /// Para fotos en modo edición: pone en la escena la luz, el ambiente y la niebla de la tarde, como en juego.
    private static bool _hayVista;
    private static bool _fog; private static FogMode _modo; private static Color _colorNiebla; private static float _densidad;
    private static AmbientMode _modoAmbiente; private static Color _ambiente;
    private static Light _luz; private static Color _colorLuz; private static float _intensidadLuz;

    [MenuItem("El Sendero/Aspecto de dibujo/Ambiente de cuento: ver la tarde en el editor")]
    public static void VerLaTarde()
    {
        var ciclo = CicloDeMainWorld();
        if (ciclo == null) return;
        if (!_hayVista)
        {
            _hayVista = true;
            _fog = RenderSettings.fog; _modo = RenderSettings.fogMode; _colorNiebla = RenderSettings.fogColor; _densidad = RenderSettings.fogDensity;
            _modoAmbiente = RenderSettings.ambientMode; _ambiente = RenderSettings.ambientLight;
            _luz = new SerializedObject(ciclo).FindProperty("directionalLight").objectReferenceValue as Light;
            if (_luz != null) { _colorLuz = _luz.color; _intensidadLuz = _luz.intensity; }
        }
        var so = new SerializedObject(ciclo);
        var franjas = so.FindProperty("timeSettings");
        for (int i = 0; i < franjas.arraySize; i++)
        {
            var f = franjas.GetArrayElementAtIndex(i);
            if ((DayNightCycle.TimeOfDay)f.FindPropertyRelative("timeOfDay").intValue != DayNightCycle.TimeOfDay.AfterNoon) continue;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = f.FindPropertyRelative("fogColor").colorValue;
            RenderSettings.fogDensity = f.FindPropertyRelative("fogDensity").floatValue;
            var luz = so.FindProperty("directionalLight").objectReferenceValue as Light;
            if (luz != null) { luz.color = f.FindPropertyRelative("lightColor").colorValue; luz.intensity = f.FindPropertyRelative("lightIntensity").floatValue; }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = f.FindPropertyRelative("ambientColor").colorValue * f.FindPropertyRelative("ambientIntensity").floatValue;
        }
        OrdenesDelEditor.Avisar("Cuento", "tarde puesta en el editor (sin guardar)");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Ambiente de cuento: quitar la vista de la tarde")]
    public static void QuitarLaVista()
    {
        if (!_hayVista) return;
        RenderSettings.fog = _fog; RenderSettings.fogMode = _modo; RenderSettings.fogColor = _colorNiebla; RenderSettings.fogDensity = _densidad;
        RenderSettings.ambientMode = _modoAmbiente; RenderSettings.ambientLight = _ambiente;
        if (_luz != null) { _luz.color = _colorLuz; _luz.intensity = _intensidadLuz; }
        _hayVista = false;
        OrdenesDelEditor.Avisar("Cuento", "vista de la tarde quitada");
    }

    private static DayNightCycle CicloDeMainWorld()
    {
        var escena = EditorSceneManager.GetSceneByPath("Assets/Scenes/Worlds/MainWorld.unity");
        if (!escena.IsValid() || !escena.isLoaded) return null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var c = raiz.GetComponentInChildren<DayNightCycle>(true);
            if (c != null) return c;
        }
        return null;
    }
}
#endif
