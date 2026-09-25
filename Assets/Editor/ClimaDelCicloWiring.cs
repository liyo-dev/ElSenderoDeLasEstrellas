using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Le pone al ciclo día/noche el prefab de lluvia, si no lo tiene (INC-377).
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

    [MenuItem("El Sendero/Mundo: asignar la lluvia al ciclo día-noche")]
    public static void Menu()
    {
        int n = Ejecutar(avisar: true);
        EditorUtility.DisplayDialog("Lluvia",
            n == 0
                ? "No he tocado nada: o el ciclo ya tenía su prefab de lluvia, o en las escenas abiertas no hay ningún DayNightCycle."
                : $"Lluvia asignada en {n} escena(s). Guarda con Ctrl+S.",
            "Vale");
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
