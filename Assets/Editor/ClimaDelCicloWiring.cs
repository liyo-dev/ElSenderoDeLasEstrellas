using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClimaDelCicloWiring
{
    private static readonly string[] RutasDeLluvia =
    {
        "Assets/VFX/RealisticRain/Prefabs/VFX_NoScript/VFX_Realistic_Rain_Unlit_(NoColision).prefab",
        "Assets/RealisticRain/Prefabs/VFX_NoScript/VFX_Realistic_Rain_Unlit_(NoColision).prefab",
        "Assets/RealisticRain/Prefabs/VFX_NoScript/VFX_Realistic_Rain_Unlit.prefab",
        "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Rain_01.prefab",
    };

    [MenuItem("El Sendero/Mundo: completar el clima de las escenas abiertas")]
    public static void Menu()
    {
        int n = Ejecutar(avisar: true) + CompletarCielos() + AsegurarEstadoDeTaparse();
        if (n > 0) EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Clima",
            n == 0
                ? "No he tocado nada: el clima de las escenas abiertas ya estaba completo."
                : $"Clima completado ({n} cambio(s)) y escenas guardadas.",
            "Vale");
    }

    private const string ControladorDelJugador = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";

    /// Estado en bucle de la capa UpperBody para taparse del viento (ClimaSobreElJugador): el mismo
    /// clip que Fear01, pero sin salida, para que dure mientras sople. Ver INC-699.
    public static int AsegurarEstadoDeTaparse()
    {
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControladorDelJugador);
        var capa = controlador != null ? controlador.layers.FirstOrDefault(l => l.name == "UpperBody") : null;
        if (capa == null)
        {
            Debug.LogWarning("[Clima] No encuentro la capa UpperBody del Animator del jugador.");
            return 0;
        }
        var maquina = capa.stateMachine;
        if (maquina.states.Any(s => s.state.name == ClimaSobreElJugador.EstadoDeTaparse)) return 0;

        AnimatorState miedo = AnimatorControllerEstados.Buscar(controlador, "Fear01");
        if (miedo == null || miedo.motion == null)
        {
            Debug.LogWarning("[Clima] No encuentro el estado Fear01 de UpperBody para copiar su clip.");
            return 0;
        }
        var estado = maquina.AddState(ClimaSobreElJugador.EstadoDeTaparse, new Vector3(50f, 120f, 0f));
        estado.motion = miedo.motion;
        estado.writeDefaultValues = miedo.writeDefaultValues;
        EditorUtility.SetDirty(controlador);
        AssetDatabase.SaveAssets();
        Debug.Log("[Clima] Añadido UpperBody." + ClimaSobreElJugador.EstadoDeTaparse + " (taparse del viento en bucle).");
        return 1;
    }

    private const string CarpetaDeCielos = "Assets/Art/Cielo";
    private const string ShaderDelCielo = "El Sendero/Cielo";

    /// <summary>
    /// El material de cielo <c>Assets/Art/Cielo/Cielo_&lt;nombre&gt;.mat</c> con el shader <c>El Sendero/Cielo</c>.
    /// Si no existe se crea y <paramref name="colores"/> le pone sus valores; si existe se devuelve tal cual,
    /// para que los retoques hechos en el Inspector se conserven.
    /// </summary>
    public static Material CieloDeEscena(string nombre, System.Action<Material> colores)
    {
        string ruta = CarpetaDeCielos + "/Cielo_" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null) return material;
        Shader shader = Shader.Find(ShaderDelCielo);
        if (shader == null)
        {
            Debug.LogWarning("[Clima] No se encuentra " + ShaderDelCielo + "; revisa la compilación del shader.");
            return null;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(CarpetaDeCielos)) AssetDatabase.CreateFolder("Assets/Art", "Cielo");
        material = new Material(shader);
        colores?.Invoke(material);
        AssetDatabase.CreateAsset(material, ruta);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CrearCielo(string nombre, Color cenit, Color horizonte, Color suelo,
        Color resplandor, float fuerza, float cobertura, Color nube, Color sombra)
    {
        return CieloDeEscena(nombre, material =>
        {
            material.SetColor("_ColorCenit", cenit);
            material.SetColor("_ColorHorizonte", horizonte);
            material.SetColor("_ColorSuelo", suelo);
            material.SetColor("_ColorDeBrumaDelHorizonte", horizonte);
            material.SetColor("_ColorDelResplandor", resplandor);
            material.SetFloat("_FuerzaDelResplandor", fuerza);
            material.SetFloat("_TamanoDelResplandor", 0.45f);
            material.SetFloat("_NubosidadBase", cobertura);
            material.SetColor("_ColorDeNubes", nube);
            material.SetColor("_ColorDeNubesEnSombra", sombra);
        });
    }

    /// <summary>
    /// Deja el ciclo de una escena en una hora fija y sin clima: una sola franja (con su cielo, su luz,
    /// su ambiente y su niebla de distancia), el ciclo parado y el sorteo de clima apagado.
    /// <paramref name="progreso"/> es el punto de la franja en el que se queda la hora (0 = su principio,
    /// 1 = su final; ver <c>HoraDelDia</c>). Pone también el cielo, el sol, el ambiente y la niebla en
    /// <see cref="RenderSettings"/> para que la vista del Editor se vea como en juego.
    /// </summary>
    public static void FijarHoraSinClima(DayNightCycle ciclo, DayNightCycle.TimeOfDaySettings franja, float progreso)
    {
        var so = new SerializedObject(ciclo);
        SerializedProperty franjas = so.FindProperty("timeSettings");
        franjas.arraySize = 1;
        SerializedProperty unica = franjas.GetArrayElementAtIndex(0);
        unica.FindPropertyRelative("timeOfDay").intValue = (int)franja.timeOfDay;
        unica.FindPropertyRelative("skybox").objectReferenceValue = franja.skybox;
        unica.FindPropertyRelative("lightColor").colorValue = franja.lightColor;
        unica.FindPropertyRelative("lightIntensity").floatValue = franja.lightIntensity;
        unica.FindPropertyRelative("ambientColor").colorValue = franja.ambientColor;
        unica.FindPropertyRelative("ambientIntensity").floatValue = franja.ambientIntensity;
        unica.FindPropertyRelative("fogColor").colorValue = franja.fogColor;
        unica.FindPropertyRelative("fogDensity").floatValue = franja.fogDensity;
        unica.FindPropertyRelative("duration").floatValue = franja.duration;
        so.FindProperty("autoAdvance").boolValue = false;
        so.FindProperty("sortearClima").boolValue = false;
        so.FindProperty("startingTimeIndex").intValue = 0;
        so.FindProperty("progresoInicialDeLaFranja").floatValue = Mathf.Clamp01(progreso);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ciclo);

        if (franja.skybox != null) RenderSettings.skybox = franja.skybox;
        var luz = so.FindProperty("directionalLight").objectReferenceValue as Light;
        if (luz != null) RenderSettings.sun = luz;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = franja.ambientColor * franja.ambientIntensity;
        RenderSettings.fogColor = franja.fogColor;
        RenderSettings.fogDensity = franja.fogDensity;
        EditorSceneManager.MarkSceneDirty(ciclo.gameObject.scene);
    }

    /// <summary>Si el ciclo es de hora fija (una sola franja y parado): su cielo es el de la escena y no se cambia por el de la franja.</summary>
    public static bool EsHoraFija(SerializedObject ciclo)
    {
        return !ciclo.FindProperty("autoAdvance").boolValue && ciclo.FindProperty("timeSettings").arraySize == 1;
    }

    public static int CompletarCielos()
    {
        Material amanecer = CrearCielo("Amanecer", new Color(0.36f,0.50f,0.78f), new Color(1f,0.72f,0.55f),
            new Color(0.55f,0.50f,0.55f), new Color(1f,0.60f,0.35f), 0.8f, 0.25f, new Color(1f,0.86f,0.78f), new Color(0.55f,0.50f,0.62f));
        Material dia = CrearCielo("Dia", new Color(0.25f,0.52f,0.92f), new Color(0.72f,0.86f,0.98f),
            new Color(0.60f,0.68f,0.75f), new Color(1f,0.95f,0.85f), 0.15f, 0.30f, Color.white, new Color(0.68f,0.74f,0.86f));
        Material atardecer = CrearCielo("Atardecer", new Color(0.25f,0.27f,0.55f), new Color(1f,0.55f,0.30f),
            new Color(0.40f,0.30f,0.35f), new Color(1f,0.45f,0.20f), 0.9f, 0.30f, new Color(1f,0.70f,0.50f), new Color(0.45f,0.32f,0.45f));
        Material noche = CrearCielo("Noche", new Color(0.02f,0.03f,0.09f), new Color(0.08f,0.10f,0.22f),
            new Color(0.03f,0.03f,0.06f), new Color(0.50f,0.60f,0.90f), 0.06f, 0.15f, new Color(0.22f,0.25f,0.36f), new Color(0.06f,0.07f,0.12f));
        int cambios = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (DayNightCycle ciclo in raiz.GetComponentsInChildren<DayNightCycle>(true))
            {
                var so = new SerializedObject(ciclo);
                var franjas = so.FindProperty("timeSettings");
                bool cambio = CompletarAstros(ciclo);
                // Una escena de hora fija lleva su propio cielo en su única franja.
                int franjasConCieloPorHora = EsHoraFija(so) ? 0 : franjas.arraySize;
                for (int j = 0; j < franjasConCieloPorHora; j++)
                {
                    var franja = franjas.GetArrayElementAtIndex(j);
                    var tipo = (DayNightCycle.TimeOfDay)franja.FindPropertyRelative("timeOfDay").intValue;
                    Material cielo = tipo == DayNightCycle.TimeOfDay.Morning ? amanecer
                        : tipo == DayNightCycle.TimeOfDay.AfterNoon ? dia
                        : tipo == DayNightCycle.TimeOfDay.Sunset ? atardecer
                        : tipo == DayNightCycle.TimeOfDay.Night ? noche : null;
                    var campo = franja.FindPropertyRelative("skybox");
                    if (cielo == null || campo.objectReferenceValue == cielo) continue;
                    campo.objectReferenceValue = cielo;
                    cambio = true;
                }
                if (!cambio) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ciclo);
                EditorSceneManager.MarkSceneDirty(escena);
                cambios++;
            }
        }
        AssetDatabase.SaveAssets();
        return cambios;
    }

    private static bool CompletarAstros(DayNightCycle ciclo)
    {
        bool cambio = false;
        if (ciclo.GetComponent<SolYLunaEnElCielo>() == null)
        {
            Undo.AddComponent<SolYLunaEnElCielo>(ciclo.gameObject);
            cambio = true;
        }
        var fugaces = ciclo.GetComponent<EstrellasFugaces>();
        if (fugaces == null)
        {
            fugaces = Undo.AddComponent<EstrellasFugaces>(ciclo.gameObject);
            cambio = true;
        }
        var so = new SerializedObject(fugaces);
        var intervalo = so.FindProperty("intervalo");
        var desde = so.FindProperty("desde");
        var elevacion = so.FindProperty("elevacion");
        var color = so.FindProperty("color");
        // Solo se ajustan los valores de partida; las elecciones personalizadas permanecen.
        if (intervalo.vector2Value == new Vector2(12f, 40f))
        {
            intervalo.vector2Value = new Vector2(8f, 25f);
            cambio = true;
        }
        if (Mathf.Approximately(desde.floatValue, 0.9f))
        {
            desde.floatValue = 0.75f;
            cambio = true;
        }
        if (elevacion.vector2Value == new Vector2(30f, 65f))
        {
            elevacion.vector2Value = new Vector2(10f, 35f);
            cambio = true;
        }
        if (color.colorValue == new Color(3f, 2.6f, 1.6f))
        {
            color.colorValue = new Color(4f, 3.4f, 2f);
            cambio = true;
        }
        if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(fugaces);
        return cambio;
    }

    public static int Ejecutar(bool avisar)
    {
        GameObject lluvia = null;
        foreach (string ruta in RutasDeLluvia)
        {
            lluvia = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (lluvia != null) break;
        }

        if (lluvia == null)
        {
            if (avisar)
                Debug.LogWarning("[Clima] No encuentro ningún prefab de lluvia en las rutas conocidas. " +
                    "Si el pack se ha movido, hay que actualizar RutasDeLluvia.");
            return 0;
        }

        int tocadas = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (var raiz in escena.GetRootGameObjects())
            {
                var ciclo = raiz.GetComponentInChildren<DayNightCycle>(true);
                if (ciclo == null) continue;

                var so = new SerializedObject(ciclo);
                var campo = so.FindProperty("rainPrefab");
                if (campo == null)
                {
                    Debug.LogWarning("[Clima] DayNightCycle ya no tiene 'rainPrefab'.");
                    break;
                }

                if (campo.objectReferenceValue != null) break;   // puesto a mano: no se toca

                campo.objectReferenceValue = lluvia;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ciclo);
                EditorSceneManager.MarkSceneDirty(escena);
                tocadas++;

                Debug.Log($"[Clima] Lluvia asignada al ciclo día/noche de '{escena.name}': " +
                    $"'{lluvia.name}'. Sin esto, StartRain() no hacía nada.");
                break;
            }
        }

        return tocadas;
    }
}
