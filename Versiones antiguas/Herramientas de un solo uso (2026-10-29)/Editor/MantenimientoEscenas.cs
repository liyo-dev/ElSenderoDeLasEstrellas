using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dos ajustes de proyecto de la auditoría INC-447/448, desde el Editor.
///
/// 1. Start.unity tenía como Lighting Data el de MainWorld_old: la escena persistente de managers
///    cargaba los lightmaps y sondas del mundo viejo en cada partida, y además impedía sacar
///    MainWorld_old del proyecto. Se le quita (Start no tiene geometría que iluminar).
/// 2. MainWorld.unity y Sendero_PruebaWill.unity están guardadas en BINARIO aunque el proyecto
///    guarda en texto (ForceText): no se pueden revisar ni comparar, y git las trata como texto.
///    Se vuelven a guardar en texto.
/// </summary>
public static class MantenimientoEscenas
{
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private static readonly string[] EscenasBinarias =
    {
        "Assets/Scenes/Worlds/MainWorld.unity",
        "Assets/Scenes/Worlds/Sendero_PruebaWill.unity",
    };

    [MenuItem("El Sendero/Archivo/Mantenimiento/Quitar a Start la iluminación de MainWorld_old")]
    public static void QuitarLuzViejaDeStart()
    {
        var start = SceneManager.GetSceneByPath(RutaStart);
        bool abiertaAqui = false;
        if (!start.isLoaded)
        {
            start = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
            abiertaAqui = true;
        }

        var activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(start);
        var antes = Lightmapping.lightingDataAsset;
        if (antes == null)
        {
            Debug.Log("[Mantenimiento] Start ya no tiene Lighting Data: nada que hacer.");
        }
        else
        {
            Lightmapping.lightingDataAsset = null;
            EditorSceneManager.MarkSceneDirty(start);
            EditorSceneManager.SaveScene(start);
            Debug.Log($"[Mantenimiento] ✓ Start ya no usa '{AssetDatabase.GetAssetPath(antes)}'. " +
                      "Ahora MainWorld_old se puede sacar con Auditoría ▸ Sacar de Assets….");
        }

        if (activa.IsValid() && activa.isLoaded && activa != start) SceneManager.SetActiveScene(activa);
        if (abiertaAqui) EditorSceneManager.CloseScene(start, true);
    }

    /// ForceReserializeAssets no convierte escenas (se probó: siguen en binario). Lo que sí lo hace
    /// es abrirlas y guardarlas: al guardar, Unity usa el modo del proyecto (Force Text).
    [MenuItem("El Sendero/Mantenimiento/Guardar MainWorld y Sendero_PruebaWill en texto")]
    public static void GuardarEnTexto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Mantenimiento", "Sal del Play antes de lanzar esto.", "Vale");
            return;
        }
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            EditorUtility.DisplayDialog("Mantenimiento", "El proyecto no está en Force Text (Project Settings ▸ Editor ▸ Asset Serialization). No hago nada.", "Vale");
            return;
        }

        // Una escena abierta con cambios sin guardar se guardaría con ellos: primero que los guarde él.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isDirty && EscenasBinarias.Contains(s.path))
            {
                EditorUtility.DisplayDialog("Mantenimiento", $"'{s.path}' tiene cambios sin guardar. Guárdala (Ctrl+S) y vuelve a lanzar esto.", "Vale");
                return;
            }
        }

        if (!EditorUtility.DisplayDialog("Mantenimiento",
                "Se van a abrir y volver a guardar en TEXTO:\n\n" + string.Join("\n", EscenasBinarias) +
                "\n\nEl contenido no cambia, solo el formato del archivo (ocupará más). Conviene tener " +
                "hecho un commit antes. ¿Seguimos?", "Guardar en texto", "Cancelar"))
            return;

        var log = new System.Text.StringBuilder("[Mantenimiento] Escenas en texto\n");
        foreach (var ruta in EscenasBinarias)
        {
            var escena = SceneManager.GetSceneByPath(ruta);
            bool abiertaAqui = false;
            if (!escena.isLoaded)
            {
                escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
                abiertaAqui = true;
            }
            EditorSceneManager.MarkSceneDirty(escena);
            bool ok = EditorSceneManager.SaveScene(escena);
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);

            bool texto = EsTexto(ruta);
            log.AppendLine(ok && texto ? $"✓ {ruta}" : $"✗ {ruta}: {(ok ? "sigue en binario" : "no se pudo guardar")}");
        }
        AssetDatabase.Refresh();
        Debug.Log(log.ToString());
    }

    private static bool EsTexto(string ruta)
    {
        try
        {
            using var f = System.IO.File.OpenRead(ruta);
            var cabecera = new byte[5];
            return f.Read(cabecera, 0, 5) == 5 && System.Text.Encoding.ASCII.GetString(cabecera) == "%YAML";
        }
        catch { return false; }
    }

    /// Lo que el MainWorld de hoy todavía usa de carpetas «viejas», a su sitio (INC-448). Se mueve
    /// con AssetDatabase, así que conservan su GUID y nadie pierde la referencia:
    ///  - Los dos perfiles de Volume que vivían en MainWorld_old (el global de URP, que usan
    ///    MainWorld, CandyLand, Sendero y PC_RPAsset, y el del aturdimiento del Despertar) pasan a
    ///    Assets/Settings/Volumenes.
    ///  - La carpeta EldoriaCodex_20260917_140610 son los materiales y mallas del MainWorld actual:
    ///    pasa a MainWorld_data/Recursos, con nombre genérico (el nombre del reino no es definitivo).
    ///  - Las dos copias de la maqueta del mapa (Eldoria_Codex.unity) no las usa nadie: salen de
    ///    Assets a «Versiones antiguas/Maquetas del mapa (2026-09-17)».
    /// Después, Auditoría ▸ Sacar de Assets… ya puede sacar el resto de MainWorld_old.
    [MenuItem("El Sendero/Archivo/Mantenimiento/Rescatar lo que se usa de MainWorld_old y ordenar los recursos del mundo")]
    public static void RescatarYOrdenar()
    {
        var log = new System.Text.StringBuilder("[Mantenimiento] Rescate y orden\n");

        const string carpetaVolumenes = "Assets/Settings/Volumenes";
        if (!AssetDatabase.IsValidFolder(carpetaVolumenes)) AssetDatabase.CreateFolder("Assets/Settings", "Volumenes");
        foreach (var nombre in new[] { "Volumen Profile.asset", "Shock Volume Profile.asset" })
            Mover($"Assets/Scenes/Worlds/MainWorld_old/{nombre}", $"{carpetaVolumenes}/{nombre}", log);

        const string datos = "Assets/Scenes/Worlds/MainWorld_data";
        const string recursos = datos + "/Recursos";
        // Donde pueda estar todavía con el nombre del reino: la carpeta original o su primer destino.
        string carpetaVieja = new[] { "Assets/Scenes/Worlds/EldoriaCodex_20260917_140610", datos + "/Eldoria" }
            .FirstOrDefault(AssetDatabase.IsValidFolder);
        if (carpetaVieja != null && !AssetDatabase.IsValidFolder(recursos))
        {
            if (!AssetDatabase.IsValidFolder(datos)) AssetDatabase.CreateFolder("Assets/Scenes/Worlds", "MainWorld_data");
            Mover(carpetaVieja, recursos, log);
        }
        else if (carpetaVieja != null) log.AppendLine($"✗ Ya existe {recursos} y también {carpetaVieja}: no muevo nada, míralo a mano.");
        else log.AppendLine("= La carpeta de recursos ya estaba ordenada.");

        // Copias de la maqueta del mapa: fuera de Assets (no se borran).
        AssetDatabase.SaveAssets();
        string raiz = System.IO.Path.GetDirectoryName(Application.dataPath);
        string destino = System.IO.Path.Combine(raiz, "Versiones antiguas", "Maquetas del mapa (2026-09-17)");
        int n = 0;
        foreach (var maqueta in new[] { recursos + "/Eldoria_Codex.unity", datos + "/Eldoria_Codex.unity" })
        {
            if (SceneManager.GetSceneByPath(maqueta).isLoaded) { log.AppendLine($"✗ '{maqueta}' está abierta: ciérrala y vuelve a lanzar esto."); continue; }
            string origen = System.IO.Path.Combine(raiz, maqueta);
            if (!System.IO.File.Exists(origen)) continue;
            System.IO.Directory.CreateDirectory(destino);
            string a = System.IO.Path.Combine(destino, (++n) + "_" + System.IO.Path.GetFileName(maqueta));
            System.IO.File.Move(origen, a);
            if (System.IO.File.Exists(origen + ".meta")) System.IO.File.Move(origen + ".meta", a + ".meta");
            log.AppendLine($"✓ {maqueta} → Versiones antiguas/Maquetas del mapa (2026-09-17)");
        }
        AssetDatabase.Refresh();

        log.AppendLine("Ahora: El Sendero ▸ Auditoría ▸ Sacar de Assets MainWorld_old… para sacar lo que queda.");
        Debug.Log(log.ToString());
    }

    private static void Mover(string desde, string hasta, System.Text.StringBuilder log)
    {
        if (AssetDatabase.LoadMainAssetAtPath(desde) == null && !AssetDatabase.IsValidFolder(desde))
        {
            log.AppendLine($"= '{desde}' ya no está (¿movido antes?).");
            return;
        }
        var error = AssetDatabase.MoveAsset(desde, hasta);
        log.AppendLine(string.IsNullOrEmpty(error) ? $"✓ {desde} → {hasta}" : $"✗ {desde}: {error}");
    }
}
