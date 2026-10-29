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

    private static Material CrearCielo(string nombre, Color cenit, Color horizonte, Color suelo,
        Color resplandor, float fuerza, float cobertura, Color nube, Color sombra)
    {
        string ruta = "Assets/Art/Cielo/Cielo_" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null) return material;
        Shader shader = Shader.Find("El Sendero/Cielo");
        if (shader == null)
        {
            Debug.LogWarning("[Clima] No se encuentra El Sendero/Cielo; revisa la compilación del shader.");
            return null;
        }
        material = new Material(shader);
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
        AssetDatabase.CreateAsset(material, ruta);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static int CompletarCielos()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Cielo")) AssetDatabase.CreateFolder("Assets/Art", "Cielo");
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
                for (int j = 0; j < franjas.arraySize; j++)
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
