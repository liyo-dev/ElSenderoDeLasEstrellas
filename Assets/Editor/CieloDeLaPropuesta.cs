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

    [MenuItem("El Sendero/Cielo/Taller/Probar lluvia que llega (Play)", priority = 30)]
    static void ProbarLluviaQueLlega()
    {
        if (!Application.isPlaying) return;
        DayNightCycle.Instance?.StartRain(120f);
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

    // Prueba de color en Play, sin guardar nada: cambia solo el cielo que está en pantalla.
    [MenuItem("El Sendero/Cielo/Taller/Probar cielo de dibujo (Play)", priority = 41)]
    static void ProbarCieloDeDibujo()
    {
        var cielo = RenderSettings.skybox;
        if (!Application.isPlaying || cielo == null || !cielo.HasProperty("_ColorCenit")) return;
        cielo.SetColor("_ColorCenit", new Color(0.10f, 0.36f, 0.88f));
        cielo.SetColor("_ColorHorizonte", new Color(0.55f, 0.82f, 0.96f));
        cielo.SetColor("_ColorDeBrumaDelHorizonte", new Color(0.70f, 0.88f, 0.97f));
        cielo.SetFloat("_CaidaDelHorizonte", 0.45f);
        cielo.SetFloat("_GrosorDeLaBruma", 0.05f);
        cielo.SetColor("_ColorDeNubes", new Color(1.0f, 0.99f, 0.96f));
        cielo.SetColor("_ColorDeNubesEnSombra", new Color(0.66f, 0.66f, 0.86f));
        cielo.SetFloat("_NubosidadBase", 0.5f);
        cielo.SetFloat("_EscalaDeNubes", 1.1f);
        cielo.SetFloat("_NubesDeDibujo", 1f);
    }

    [MenuItem("El Sendero/Cielo/Taller/Franja: amanecer (Play)", priority = 50)] static void IrAmanecer() => IrAFranja(DayNightCycle.TimeOfDay.Morning);
    [MenuItem("El Sendero/Cielo/Taller/Franja: día (Play)", priority = 51)] static void IrDia() => IrAFranja(DayNightCycle.TimeOfDay.AfterNoon);
    [MenuItem("El Sendero/Cielo/Taller/Franja: atardecer (Play)", priority = 52)] static void IrAtardecer() => IrAFranja(DayNightCycle.TimeOfDay.Sunset);
    [MenuItem("El Sendero/Cielo/Taller/Franja: noche (Play)", priority = 53)] static void IrNoche() => IrAFranja(DayNightCycle.TimeOfDay.Night);

    static void IrAFranja(DayNightCycle.TimeOfDay franja)
    {
        if (Application.isPlaying) DayNightCycle.Instance?.SetTimeOfDay(franja, immediate: true);
    }

    [MenuItem("El Sendero/Cielo/Taller/Informe del cielo (Play)", priority = 40)]
    static void InformeDelCielo()
    {
        var ciclo = DayNightCycle.Instance;
        var cielo = RenderSettings.skybox;
        var sb = new System.Text.StringBuilder("[Cielo] Informe: ");
        if (ciclo != null)
            sb.Append($"activo={ciclo.isActiveAndEnabled} lluvia={ciclo.IsRaining}/{ciclo.LluviaPedida} escala={Time.timeScale} franja={ciclo.CurrentTimeOfDay} nublado={ciclo.Nublado:0.00} noche={ciclo.PesoDeNoche:0.00} sol={ciclo.DireccionDelSol} ");
        sb.Append($"skybox={(cielo != null ? cielo.name + " (" + cielo.shader.name + ")" : "ninguno")} ");
        if (cielo != null)
            foreach (var p in new[] { "_ColorCenit", "_ColorHorizonte", "_ColorDeBrumaDelHorizonte", "_ColorDeNubes", "_ColorDeNubesEnSombra" })
                if (cielo.HasProperty(p)) sb.Append($"{p}={cielo.GetColor(p)} ");
        if (cielo != null)
            foreach (var p in new[] { "_NubosidadBase", "_Nubosidad", "_NubesDeDibujo", "_EscalaDeNubes", "_GrosorDeLaBruma" })
                if (cielo.HasProperty(p)) sb.Append($"{p}={cielo.GetFloat(p):0.00} ");
        sb.Append($"niebla={RenderSettings.fog} {RenderSettings.fogMode} dens={RenderSettings.fogDensity:0.0000} color={RenderSettings.fogColor}");
        foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>())
        {
            if (!v.isActiveAndEnabled || v.sharedProfile == null) continue;
            sb.Append($"\n  volumen {v.name} global={v.isGlobal} peso={v.weight:0.00} prioridad={v.priority} perfil={v.sharedProfile.name}:");
            foreach (var c in v.sharedProfile.components) if (c.active) sb.Append(" " + c.GetType().Name);
        }
        var ajustes = UnityEngine.Rendering.VolumeManager.instance.stack?.GetComponent<UnityEngine.Rendering.Universal.ColorAdjustments>();
        if (ajustes != null) sb.Append($"\n  pila: exposicion={ajustes.postExposure.value:0.00} contraste={ajustes.contrast.value:0} saturacion={ajustes.saturation.value:0} filtro={ajustes.colorFilter.value}");
        var tono = UnityEngine.Rendering.VolumeManager.instance.stack?.GetComponent<UnityEngine.Rendering.Universal.Tonemapping>();
        if (tono != null) sb.Append($" tonemapping={tono.mode.value}");
        var enUso = ciclo != null ? ciclo.SkyboxEnUso : null;
        var asset = AssetDatabase.LoadAssetAtPath<Material>(CarpetaCielo + "Cielo_Dia.mat");
        if (enUso != null && asset != null)
        {
            sb.Append($"\n  diferencias {enUso.name} vs Cielo_Dia (keywords: {string.Join(",", enUso.shaderKeywords)} / {string.Join(",", asset.shaderKeywords)}):");
            var sh = enUso.shader;
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                string n = sh.GetPropertyName(i);
                var t = sh.GetPropertyType(i);
                string a1 = t == UnityEngine.Rendering.ShaderPropertyType.Color ? enUso.GetColor(n).ToString() : t == UnityEngine.Rendering.ShaderPropertyType.Float || t == UnityEngine.Rendering.ShaderPropertyType.Range ? enUso.GetFloat(n).ToString("0.000") : "";
                string a2 = t == UnityEngine.Rendering.ShaderPropertyType.Color ? asset.GetColor(n).ToString() : t == UnityEngine.Rendering.ShaderPropertyType.Float || t == UnityEngine.Rendering.ShaderPropertyType.Range ? asset.GetFloat(n).ToString("0.000") : "";
                if (a1 != a2) sb.Append($" {n}: {a1} / {a2};");
            }
        }
        Debug.Log(sb.ToString());
        System.IO.File.AppendAllText("Guiones/_ordenes/cielo.log", System.DateTime.Now.ToString("HH:mm:ss ") + sb + "\n");
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

    // Paleta de cada cielo (aprobada por Raúl el 18 nov: azul vivo, nubes grandes, cuerpo blanco y panza lila).
    // Cielo == null deja el degradado del material como está y solo toca las nubes.
    sealed class Paleta
    {
        public string ruta; public float cobertura, escala = 1.1f, velocidad = 0.020f;
        public Color nubes, sombra, suelo;
        public Color? cenit, horizonte, bruma; public float caida = 0.45f, grosorDeBruma = 0.05f;
    }

    static readonly Paleta[] Paletas =
    {
        new Paleta { ruta = CarpetaCielo + "Cielo_Dia.mat", cobertura = 0.50f, nubes = new Color(1f, 0.99f, 0.96f), sombra = new Color(0.66f, 0.66f, 0.86f), suelo = new Color(0.55f, 0.70f, 0.82f),
            cenit = new Color(0.10f, 0.36f, 0.88f), horizonte = new Color(0.55f, 0.82f, 0.96f), bruma = new Color(0.70f, 0.88f, 0.97f) },
        new Paleta { ruta = CarpetaCielo + "Cielo_Amanecer.mat", cobertura = 0.42f, nubes = new Color(1f, 0.86f, 0.78f), sombra = new Color(0.62f, 0.52f, 0.72f), suelo = new Color(0.66f, 0.60f, 0.68f),
            cenit = new Color(0.30f, 0.45f, 0.80f), horizonte = new Color(1f, 0.72f, 0.55f), bruma = new Color(1f, 0.76f, 0.60f) },
        new Paleta { ruta = CarpetaCielo + "Cielo_Atardecer.mat", cobertura = 0.45f, nubes = new Color(1f, 0.72f, 0.52f), sombra = new Color(0.52f, 0.36f, 0.56f), suelo = new Color(0.52f, 0.42f, 0.50f),
            cenit = new Color(0.22f, 0.24f, 0.55f), horizonte = new Color(1f, 0.55f, 0.30f), bruma = new Color(1f, 0.60f, 0.36f) },
        new Paleta { ruta = CarpetaCielo + "Cielo_Noche.mat", cobertura = 0.30f, velocidad = 0.018f, nubes = new Color(0.22f, 0.25f, 0.36f), sombra = new Color(0.08f, 0.09f, 0.16f), suelo = new Color(0.10f, 0.12f, 0.22f),
            cenit = new Color(0.02f, 0.03f, 0.09f), horizonte = new Color(0.08f, 0.10f, 0.22f), bruma = new Color(0.08f, 0.10f, 0.22f) },
        new Paleta { ruta = CarpetaCielo + "Cielo_Prologo.mat", cobertura = 0.50f, nubes = new Color(1f, 0.99f, 0.96f), sombra = new Color(0.66f, 0.66f, 0.86f), suelo = new Color(0.55f, 0.70f, 0.82f),
            cenit = new Color(0.10f, 0.36f, 0.88f), horizonte = new Color(0.55f, 0.82f, 0.96f), bruma = new Color(0.70f, 0.88f, 0.97f) },
        new Paleta { ruta = CarpetaCielo + "Cielo_Chuchelandia.mat", cobertura = 0.38f, escala = 1.4f, nubes = new Color(1.05f, 0.98f, 1f), sombra = new Color(0.96f, 0.74f, 0.88f), suelo = new Color(0.78f, 0.66f, 0.82f) },
        new Paleta { ruta = CarpetaMenu + "CieloDiaMenu.mat", cobertura = 0.50f, nubes = new Color(1f, 0.99f, 0.96f), sombra = new Color(0.66f, 0.66f, 0.86f), suelo = new Color(0.55f, 0.70f, 0.82f),
            cenit = new Color(0.10f, 0.36f, 0.88f), horizonte = new Color(0.55f, 0.82f, 0.96f), bruma = new Color(0.70f, 0.88f, 0.97f) },
        new Paleta { ruta = CarpetaMenu + "CieloNocheMenu.mat", cobertura = 0.30f, velocidad = 0.018f, nubes = new Color(0.22f, 0.25f, 0.36f), sombra = new Color(0.08f, 0.09f, 0.16f), suelo = new Color(0.10f, 0.12f, 0.22f),
            cenit = new Color(0.02f, 0.03f, 0.09f), horizonte = new Color(0.08f, 0.10f, 0.22f), bruma = new Color(0.08f, 0.10f, 0.22f) },
    };

    static int AplicarMateriales()
    {
        int n = 0;
        foreach (var paleta in Paletas) n += Aplicar(paleta);
        return n;
    }

    static int Aplicar(Paleta p)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p.ruta);
        if (mat == null)
        {
            Debug.LogWarning("[Cielo] No encuentro " + p.ruta);
            return 0;
        }
        mat.SetFloat("_NubosidadBase", p.cobertura);
        mat.SetColor("_ColorDeNubes", p.nubes);
        mat.SetColor("_ColorDeNubesEnSombra", p.sombra);
        mat.SetColor("_ColorSuelo", p.suelo);
        mat.SetFloat("_EscalaDeNubes", p.escala);
        mat.SetFloat("_VelocidadDeNubes", p.velocidad);
        mat.SetFloat("_NubesDeDibujo", 1f);
        if (p.cenit.HasValue)
        {
            mat.SetColor("_ColorCenit", p.cenit.Value);
            mat.SetColor("_ColorHorizonte", p.horizonte.Value);
            mat.SetColor("_ColorDeBrumaDelHorizonte", p.bruma.Value);
            mat.SetFloat("_CaidaDelHorizonte", p.caida);
            mat.SetFloat("_GrosorDeLaBruma", p.grosorDeBruma);
        }
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
            cambios += Fijar(so, "oscurecerNubesDeLluvia", p => p.floatValue = 0.32f);
            cambios += Fijar(so, "oscurecerNubesDeTormenta", p => p.floatValue = 0.22f);
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

