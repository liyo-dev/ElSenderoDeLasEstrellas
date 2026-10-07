using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Completa el clima de las escenas abiertas: prefab de lluvia del ciclo día/noche (INC-377) y
/// valores del techo de nubes de tormenta (CloudCoverSpawner) que dejan huecos. Ver INC-660.
///
/// «Cuando salió el Mago Oscuro no empezó a llover.» El beat de clima está puesto y llega a
/// ejecutarse — `CinematicWeather` llama a `DayNightCycle.StartRain()` —, pero ese método arranca
/// con esto:
///
///     if (rainPrefab == null) { Debug.LogWarning("StartRain() no ha hecho nada: rainPrefab no
///     está asignado en este GameObject/escena."); return; }
///
/// Es un campo del componente en la ESCENA, así que no hay forma de rellenarlo desde el montaje ni
/// desde el prefab: hay que asignarlo en cada escena que tenga ciclo. Esto lo hace por su cuenta y
/// sin pisar nada que ya estuviera puesto a mano.
public static class ClimaDelCicloWiring
{
    /// Se prueban por orden; el proyecto tiene el pack RealisticRain en dos sitios según cómo se
    /// importó, así que se busca en los dos. Sin colisiones y sin vapor: es un prólogo, no una
    /// simulación.
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
        int n = Ejecutar(avisar: true) + AjustarTechosDeNubes();
        if (n > 0) EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Clima",
            n == 0
                ? "No he tocado nada: el clima de las escenas abiertas ya estaba completo."
                : $"Clima completado ({n} cambio(s)) y escenas guardadas.",
            "Vale");
    }

    /// Valores del techo de nubes de tormenta con los que las nubes se tocan desde abajo. Una
    /// escala por encima de 5 es el despiste ya visto en MainWorld (10-18 tecleado como porcentaje,
    /// nubes de 250-600 m); una huella menor o un recorte de alfa mayor dejan cielo entre nubes.
    private const float HuellaMinima = 2f;
    private const float RecorteMaximo = 0.4f;

    public static int AjustarTechosDeNubes()
    {
        int tocadas = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (var raiz in escena.GetRootGameObjects())
            {
                foreach (var techo in raiz.GetComponentsInChildren<CloudCoverSpawner>(true))
                {
                    var so = new SerializedObject(techo);
                    bool cambio = false;

                    var escala = so.FindProperty("scaleRange");
                    if (escala != null && (escala.vector2Value.x > 5f || escala.vector2Value.y > 5f))
                    {
                        escala.vector2Value = new Vector2(0.8f, 1.5f);
                        var altura = so.FindProperty("cloudHeight");
                        if (altura != null && altura.floatValue < 140f) altura.floatValue = 140f;
                        cambio = true;
                    }

                    var huella = so.FindProperty("minFootprintInCells");
                    if (huella != null && huella.floatValue < HuellaMinima)
                    {
                        huella.floatValue = HuellaMinima;
                        cambio = true;
                    }

                    var recorte = so.FindProperty("visibleAlphaThreshold");
                    if (recorte != null && recorte.floatValue > RecorteMaximo)
                    {
                        recorte.floatValue = RecorteMaximo;
                        cambio = true;
                    }

                    if (!cambio) continue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(techo);
                    EditorSceneManager.MarkSceneDirty(escena);
                    tocadas++;
                    Debug.Log($"[Clima] Techo de nubes de '{escena.name}' ajustado para que no queden huecos.");
                }
            }
        }
        return tocadas;
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
