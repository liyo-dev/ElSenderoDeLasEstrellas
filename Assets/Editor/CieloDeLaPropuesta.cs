using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Aplica la paleta de nubes y los ajustes de clima a los materiales y a las escenas con clima.
/// Todo por AssetDatabase y por la API de escenas, nunca escribiendo los assets a mano (INC-441).
public static class CieloDeLaPropuesta
{
    const string Menu = "El Sendero/Cielo/Aplicar la propuesta de cielo y clima";

    const string CarpetaCielo = "Assets/Art/Cielo/";
    const string CarpetaMenu = "Assets/Art/MainMenu/";

    [MenuItem(Menu)]
    static void Aplicar()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Cielo", "Sal de Play antes de aplicar la propuesta.", "Vale");
            return;
        }

        int materiales = AplicarMateriales();
        int escenas = AplicarEscenas();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Cielo] Aplicado: {materiales} material(es) y {escenas} escena(s).");
    }

    [MenuItem("El Sendero/Cielo/Taller/Ver la propuesta en el Editor", priority = 20)]
    static void VerEnEditor()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(CarpetaCielo + "Cielo_Dia.mat");
        if (mat == null) return;
        RenderSettings.skybox = mat;
        DynamicGI.UpdateEnvironment();
    }

    [MenuItem("El Sendero/Cielo/Taller/Devolver el cielo del Editor", priority = 21)]
    static void DevolverElCieloDelEditor()
    {
        var porDefecto = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        if (porDefecto != null) RenderSettings.skybox = porDefecto;
        DynamicGI.UpdateEnvironment();
    }

    [MenuItem("El Sendero/Cielo/Taller/Probar lluvia (Play)", priority = 30)]
    static void ProbarLluvia()
    {
        if (!Application.isPlaying) return;
        DayNightCycle.Instance?.StartRain(120f, immediate: true);
    }

    [MenuItem("El Sendero/Cielo/Taller/Probar tormenta (Play)", priority = 31)]
    static void ProbarTormenta()
    {
        if (!Application.isPlaying) return;
        DayNightCycle.Instance?.StartThunderstorm(120f, immediate: true);
    }

    [MenuItem("El Sendero/Cielo/Taller/Quitar el clima (Play)", priority = 32)]
    static void QuitarClima()
    {
        if (!Application.isPlaying) return;
        DayNightCycle.Instance?.StopThunderstorm();
        DayNightCycle.Instance?.StopRain();
    }

    [MenuItem("El Sendero/Cielo/Taller/Probar viento (Play)", priority = 33)]
    static void ProbarViento()
    {
        if (!Application.isPlaying) return;
        var jugador = PlayerService.Player;
        Vector3 direccion = DayNightCycle.Instance != null ? DayNightCycle.Instance.DireccionDelViento : Vector3.right;
        VfxDeClima.CrearViento(jugador != null ? jugador.transform : null, direccion);
    }

    [MenuItem("El Sendero/Cielo/Volver al cielo de antes", priority = 1)]
    static void VolverAlCieloDeAntes()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Cielo", "Sal de Play antes de cambiar el cielo.", "Vale");
            return;
        }

        // Solo los valores de nubes; el suelo del cielo (mar al volar) se queda como está.
        Restaurar(CarpetaCielo + "Cielo_Dia.mat", 0.30f, new Color(1f, 1f, 1f), new Color(0.68f, 0.74f, 0.86f), 1.8f, 0.025f);
        Restaurar(CarpetaCielo + "Cielo_Amanecer.mat", 0.25f, new Color(1f, 0.86f, 0.78f), new Color(0.55f, 0.50f, 0.62f), 1.8f, 0.025f);
        Restaurar(CarpetaCielo + "Cielo_Atardecer.mat", 0.30f, new Color(1f, 0.70f, 0.50f), new Color(0.45f, 0.32f, 0.45f), 1.8f, 0.025f);
        Restaurar(CarpetaCielo + "Cielo_Noche.mat", 0.15f, new Color(0.22f, 0.25f, 0.36f), new Color(0.06f, 0.07f, 0.12f), 1.8f, 0.025f);
        Restaurar(CarpetaCielo + "Cielo_Chuchelandia.mat", 0.38f, new Color(1.05f, 0.98f, 1f), new Color(0.96f, 0.74f, 0.88f), 1.4f, 0.020f);
        Restaurar(CarpetaCielo + "Cielo_Prologo.mat", 0.30f, new Color(1f, 1f, 1f), new Color(0.68f, 0.74f, 0.86f), 1.8f, 0.025f);
        Restaurar(CarpetaMenu + "CieloDiaMenu.mat", 0.30f, new Color(1f, 1f, 1f), new Color(0.68f, 0.74f, 0.86f), 1.8f, 0.025f);
        Restaurar(CarpetaMenu + "CieloNocheMenu.mat", 0.15f, new Color(0.22f, 0.25f, 0.36f), new Color(0.06f, 0.07f, 0.12f), 1.8f, 0.025f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Cielo] Nubes devueltas a los valores de antes.");
    }

    static void Restaurar(string ruta, float cobertura, Color luz, Color sombra, float escala, float velocidad)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null) return;
        mat.SetFloat("_NubosidadBase", cobertura);
        mat.SetColor("_ColorDeNubes", luz);
        mat.SetColor("_ColorDeNubesEnSombra", sombra);
        mat.SetFloat("_EscalaDeNubes", escala);
        mat.SetFloat("_VelocidadDeNubes", velocidad);
        EditorUtility.SetDirty(mat);
    }

    static int AplicarMateriales()
    {
        int n = 0;
        n += Aplicar(CarpetaCielo + "Cielo_Dia.mat",
            0.46f, new Color(1f, 0.98f, 0.93f), new Color(0.62f, 0.66f, 0.90f), new Color(0.55f, 0.70f, 0.82f), 2.6f, 0.020f);
        n += Aplicar(CarpetaCielo + "Cielo_Amanecer.mat",
            0.40f, new Color(1f, 0.92f, 0.84f), new Color(0.70f, 0.58f, 0.74f), new Color(0.66f, 0.60f, 0.68f), 2.8f, 0.020f);
        n += Aplicar(CarpetaCielo + "Cielo_Atardecer.mat",
            0.42f, new Color(1f, 0.78f, 0.60f), new Color(0.58f, 0.42f, 0.60f), new Color(0.52f, 0.42f, 0.50f), 2.8f, 0.020f);
        n += Aplicar(CarpetaCielo + "Cielo_Noche.mat",
            0.32f, new Color(0.30f, 0.34f, 0.50f), new Color(0.12f, 0.14f, 0.26f), new Color(0.10f, 0.12f, 0.22f), 2.6f, 0.018f);
        n += Aplicar(CarpetaCielo + "Cielo_Chuchelandia.mat",
            0.44f, new Color(1.05f, 0.98f, 1f), new Color(0.82f, 0.64f, 0.86f), new Color(0.78f, 0.66f, 0.82f), 2.4f, 0.020f);
        n += Aplicar(CarpetaCielo + "Cielo_Prologo.mat",
            0.44f, new Color(1f, 0.98f, 0.92f), new Color(0.64f, 0.64f, 0.88f), new Color(0.55f, 0.70f, 0.82f), 2.6f, 0.020f);
        n += Aplicar(CarpetaMenu + "CieloDiaMenu.mat",
            0.46f, new Color(1f, 0.98f, 0.93f), new Color(0.62f, 0.66f, 0.90f), new Color(0.55f, 0.70f, 0.82f), 2.6f, 0.020f);
        n += Aplicar(CarpetaMenu + "CieloNocheMenu.mat",
            0.32f, new Color(0.30f, 0.34f, 0.50f), new Color(0.12f, 0.14f, 0.26f), new Color(0.10f, 0.12f, 0.22f), 2.6f, 0.018f);
        return n;
    }

    static int Aplicar(string ruta, float cobertura, Color luz, Color sombra, Color suelo, float escala, float velocidad)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            Debug.LogWarning("[Cielo] No encuentro " + ruta);
            return 0;
        }
        mat.SetFloat("_NubosidadBase", cobertura);
        mat.SetColor("_ColorDeNubes", luz);
        mat.SetColor("_ColorDeNubesEnSombra", sombra);
        mat.SetColor("_ColorSuelo", suelo);
        mat.SetFloat("_EscalaDeNubes", escala);
        mat.SetFloat("_VelocidadDeNubes", velocidad);
        mat.SetFloat("_NubesDeDibujo", 1f);
        EditorUtility.SetDirty(mat);
        return 1;
    }

    static int AplicarEscenas()
    {
        int n = 0;
        n += AplicarEscena("Assets/Scenes/Worlds/MainWorld.unity");
        n += AplicarEscena("Assets/Scenes/Worlds/Chuchelandia.unity");
        return n;
    }

    static int AplicarEscena(string ruta)
    {
        Scene escena = SceneManager.GetSceneByPath(ruta);
        bool abiertaAqui = false;

        if (!escena.IsValid() || !escena.isLoaded)
        {
            escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            abiertaAqui = true;
        }

        int cambios = 0;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var ciclo = raiz.GetComponentInChildren<DayNightCycle>(true);
            if (ciclo == null) continue;

            var so = new SerializedObject(ciclo);
            cambios += Fijar(so, "rainPrefab", p => p.objectReferenceValue = null);
            cambios += Fijar(so, "climaRelativoAlCielo", p => p.boolValue = true);
            cambios += Fijar(so, "rainLightIntensityMultiplier", p => p.floatValue = 0.75f);
            cambios += Fijar(so, "rainFogDensityMultiplier", p => p.floatValue = 1.6f);
            cambios += Fijar(so, "rainMinLightIntensity", p => p.floatValue = 0.5f);
            cambios += Fijar(so, "oscurecerNubesDeLluvia", p => p.floatValue = 0.85f);
            cambios += Fijar(so, "oscurecerNubesDeTormenta", p => p.floatValue = 0.62f);
            cambios += Fijar(so, "tinteDeSombraDeNubes", p => p.colorValue = new Color(0.62f, 0.66f, 0.90f, 1f));
            cambios += Fijar(so, "nieblaDelHorizonte", p => p.floatValue = 0.7f);
            cambios += Fijar(so, "tinteDeLuzDeLluvia", p => p.colorValue = new Color(0.78f, 0.84f, 0.96f, 1f));
            cambios += Fijar(so, "fuerzaDelTinteDeLluvia", p => p.floatValue = 0.5f);
            cambios += Fijar(so, "thunderstormBoltDistanceRange", p => p.vector2Value = new Vector2(35f, 90f));
            cambios += Fijar(so, "thunderstormBoltHeight", p => p.floatValue = 140f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ciclo);
            cambios++;
            break;
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        return cambios > 0 ? 1 : 0;
    }

    static int Fijar(SerializedObject so, string campo, System.Action<SerializedProperty> accion)
    {
        var p = so.FindProperty(campo);
        if (p == null)
        {
            Debug.LogWarning($"[Cielo] {so.targetObject.GetType().Name} ya no tiene el campo '{campo}'.");
            return 0;
        }
        accion(p);
        return 1;
    }
}

