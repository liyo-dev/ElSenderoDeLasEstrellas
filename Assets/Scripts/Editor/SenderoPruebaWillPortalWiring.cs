using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Engancha la "Puerta_Will_Dorada" del hub (creada por SenderoHubSetDressing — hasta ahora solo
/// decorativa, sin collider ni lógica) al portal de verdad hacia la Prueba de Will: añade un
/// trigger invisible con PortalTrigger apuntando a Sendero_PruebaWill.unity /
/// SpawnAnchor "SENDERO_PRUEBAWILL_START" (el mismo id que crea WillTrialMazeBuilder en la celda
/// de inicio del laberinto), y da de alta esa escena en Build Settings si todavía no lo estaba —
/// sin esto, `SceneManager.LoadScene` por nombre funciona en el Editor pero falla en silencio en
/// una build real.
///
/// Colocar al jugador Y a todo el grupo (Estela/Liam, más el NPC de Will si aplica) en el punto de
/// inicio NO necesita ningún script a medida: ya lo hace el sistema genérico del proyecto
/// (SpawnAnchor/AnchorRegistry/SpawnManager/TeleportService, el mismo que usa cualquier otra puerta
/// del juego) — TeleportService.TeleportCompanionsToPlayer() se llama solo justo después de mover
/// al jugador. Lo único que faltaba era enganchar la puerta dorada (hoy solo set dressing) a un
/// PortalTrigger de verdad, que es justo lo que hace este menú.
///
/// Requiere haber ejecutado antes "El Sendero → Escena → Crear Set Dressing del Hub (Altar,
/// Puertas, Camino)" (crea la puerta) y, en algún momento, "Generar Laberinto de la Prueba de
/// Will" (crea el SpawnAnchor de destino — no hace falta en ese orden exacto, el anchor solo se
/// resuelve en tiempo de ejecución al cruzar el portal). Idempotente.
/// </summary>
public static class SenderoPruebaWillPortalWiring
{
    private const string HubSceneName = "Sendero";
    private const string DoorObjectName = "Puerta_Will_Dorada";
    private const string TargetScenePath = "Assets/Scenes/Worlds/Sendero_PruebaWill.unity";
    private const string TargetSceneName = "Sendero_PruebaWill";
    private const string TriggerChildName = "Trigger_EntradaPruebaWill";

    [MenuItem("El Sendero/Archivo/Escena/Enganchar Portal de la Prueba de Will")]
    public static void WirePortal()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != HubSceneName)
        {
            Debug.LogWarning($"[SenderoPruebaWillPortalWiring] La escena activa no es '{HubSceneName}'. Abre Assets/Scenes/Worlds/Sendero.unity y vuelve a ejecutar.");
            return;
        }

        var door = GameObject.Find(DoorObjectName);
        if (door == null)
        {
            Debug.LogError($"[SenderoPruebaWillPortalWiring] No se encontró '{DoorObjectName}' — ejecuta antes 'El Sendero → Escena → Crear Set Dressing del Hub (Altar, Puertas, Camino)'.");
            return;
        }

        EnsureSceneInBuildSettings();

        var existingTrigger = door.transform.Find(TriggerChildName);
        GameObject triggerGo = existingTrigger != null ? existingTrigger.gameObject : null;
        if (triggerGo == null)
        {
            triggerGo = new GameObject(TriggerChildName);
            Undo.RegisterCreatedObjectUndo(triggerGo, "Crear trigger portal Prueba de Will");
            triggerGo.transform.SetParent(door.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;
        }

        var col = triggerGo.GetComponent<BoxCollider>();
        if (col == null) col = triggerGo.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(3f, 3f, 2f); // hueco de puerta razonable — ajusta a ojo si no encaja

        var portal = triggerGo.GetComponent<PortalTrigger>();
        if (portal == null) portal = triggerGo.AddComponent<PortalTrigger>();
        portal.targetSceneName = TargetSceneName;
        portal.targetAnchorId = WillTrialMazeBuilder.StartAnchorId;

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SenderoPruebaWillPortalWiring] '{DoorObjectName}' enganchada: al cruzar el trigger ({TriggerChildName}) carga '{TargetSceneName}' y coloca a todo el grupo en el anchor '{WillTrialMazeBuilder.StartAnchorId}'. Revisa a ojo el tamaño/posición del trigger (hijo de la puerta) y guarda la escena (Ctrl+S).");
    }

    // Sin esto, SceneTransitionLoader/SceneManager.LoadScene(nombre) funciona en el Editor (donde
    // cualquier escena del proyecto es cargable por nombre) pero falla en silencio en una build real
    // si la escena no está registrada en Build Settings — un fallo clásico y difícil de diagnosticar
    // porque en el Editor todo parece funcionar.
    private static void EnsureSceneInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
        {
            if (s.path == TargetScenePath)
            {
                if (!s.enabled)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = scenes;
                    Debug.Log($"[SenderoPruebaWillPortalWiring] '{TargetScenePath}' ya estaba en Build Settings pero desactivada — activada.");
                }
                return;
            }
        }

        var list = new List<EditorBuildSettingsScene>(scenes)
        {
            new EditorBuildSettingsScene(TargetScenePath, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[SenderoPruebaWillPortalWiring] '{TargetScenePath}' añadida a Build Settings (antes no estaba).");
    }
}
