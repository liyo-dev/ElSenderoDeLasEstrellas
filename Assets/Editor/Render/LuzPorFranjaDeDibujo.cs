#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Paleta de luz de cada franja del día pensada para el aspecto de dibujo (INC-1138): sol de color en el
/// amanecer y el atardecer, día casi blanco y cálido, luna azul de noche con ambiente lila suficiente para que
/// se lean las caras y los colores. El shader de dibujo solo toma el 35 % del tono de la luz
/// (AspectoDeDibujo.tinteDeLaLuz), así que estos colores se notan sin teñirlo todo.
/// Se aplica a los DayNightCycle con el ciclo completo (las escenas de hora fija tienen su luz propia) de las
/// escenas de la lista; lo de antes se guarda en un registro para volver atrás.
public static class LuzPorFranjaDeDibujo
{
    private static readonly string[] Escenas =
    {
        "Assets/Scenes/Worlds/MainWorld.unity",
        "Assets/Scenes/Worlds/Chuchelandia.unity",
        "Assets/Scenes/Test/Lab.unity",
    };
    private const string RutaDelRegistro = "Assets/Settings/AspectoDeDibujo/LuzPorFranjaAntes.json";

    private struct Franja
    {
        public DayNightCycle.TimeOfDay cual;
        public Color luz; public float intensidad;
        public Color ambiente; public float intensidadDelAmbiente;
        public Color niebla; public float densidad;
    }

    private static readonly Franja[] Paleta =
    {
        new Franja { cual = DayNightCycle.TimeOfDay.Morning, luz = new Color(1f, 0.86f, 0.74f), intensidad = 1.1f,
            ambiente = new Color(0.34f, 0.33f, 0.44f), intensidadDelAmbiente = 0.85f, niebla = new Color(0.80f, 0.80f, 0.90f), densidad = 0.007f },
        new Franja { cual = DayNightCycle.TimeOfDay.AfterNoon, luz = new Color(1f, 0.97f, 0.90f), intensidad = 1.3f,
            ambiente = new Color(0.40f, 0.39f, 0.36f), intensidadDelAmbiente = 1.1f, niebla = new Color(0.85f, 0.87f, 0.90f), densidad = 0.004f },
        new Franja { cual = DayNightCycle.TimeOfDay.Sunset, luz = new Color(1f, 0.72f, 0.50f), intensidad = 0.85f,
            ambiente = new Color(0.38f, 0.27f, 0.30f), intensidadDelAmbiente = 0.8f, niebla = new Color(0.90f, 0.62f, 0.42f), densidad = 0.011f },
        new Franja { cual = DayNightCycle.TimeOfDay.Night, luz = new Color(0.60f, 0.70f, 1f), intensidad = 0.6f,
            ambiente = new Color(0.17f, 0.18f, 0.34f), intensidadDelAmbiente = 0.8f, niebla = new Color(0.10f, 0.10f, 0.22f), densidad = 0.014f },
    };

    [Serializable] private sealed class Antes { public string escena; public string json; }
    [Serializable] private sealed class Registro { public List<Antes> ciclos = new List<Antes>(); }

    [MenuItem("El Sendero/Mundo/Luz por franja: paleta de dibujo")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Sal de Play antes de cambiar la luz."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var registro = File.Exists(RutaDelRegistro) ? JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro)) ?? new Registro() : new Registro();
        var antes = EditorSceneManager.GetSceneManagerSetup();
        int cambiadas = 0;
        foreach (var ruta in Escenas)
        {
            if (!File.Exists(ruta)) continue;
            var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            bool tocada = false;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var ciclo in raiz.GetComponentsInChildren<DayNightCycle>(true))
                {
                    var so = new SerializedObject(ciclo);
                    if (ClimaDelCicloWiring.EsHoraFija(so)) continue;
                    if (!registro.ciclos.Exists(a => a.escena == ruta))
                        registro.ciclos.Add(new Antes { escena = ruta, json = EditorJsonUtility.ToJson(ciclo) });
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
                    tocada = true;
                }
            if (tocada) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); cambiadas++; }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelRegistro));
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(registro, true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
        if (antes != null && antes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(antes);
        Debug.Log($"[LuzPorFranjaDeDibujo] Paleta de dibujo puesta en {cambiadas} escenas.");
    }

    [MenuItem("El Sendero/Mundo/Luz por franja: volver a la de antes")]
    public static void Volver()
    {
        if (!File.Exists(RutaDelRegistro) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var registro = JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro)) ?? new Registro();
        var antes = EditorSceneManager.GetSceneManagerSetup();
        foreach (var a in registro.ciclos)
        {
            var escena = EditorSceneManager.OpenScene(a.escena, OpenSceneMode.Single);
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var ciclo in raiz.GetComponentsInChildren<DayNightCycle>(true))
                {
                    if (ClimaDelCicloWiring.EsHoraFija(new SerializedObject(ciclo))) continue;
                    EditorJsonUtility.FromJsonOverwrite(a.json, ciclo);
                    EditorUtility.SetDirty(ciclo);
                }
            EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena);
        }
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(new Registro(), true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
        if (antes != null && antes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(antes);
    }
}
#endif
