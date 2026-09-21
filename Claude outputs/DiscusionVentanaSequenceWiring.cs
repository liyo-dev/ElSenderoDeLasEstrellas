using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta en WillHouse.unity la secuencia de datos SEQ_DiscusionVentana (Paso 2 del refactor
/// Tramo 1, análisis claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §3). Esto nunca
/// se llegó a montar en su momento -- se dejó explícitamente "para el script final de montaje" --
/// y es la razón de que no salga ningún bocadillo por la ventana al probarlo: el
/// SequencePlayer/SequenceStage, las dos marcas de voz (VentanaIzq/VentanaDcha) y los dos planos
/// de cámara (ventanaShot/willClose) que pide el asset no existen todavía en la escena.
///
/// Igual que OliverSequenceWiring/StarAwakeningSequenceWiring: aunque WillHouse.unity está
/// guardada en texto plano YAML (a diferencia de MainWorld.unity, que está en binario), sigue
/// siendo más seguro crear Transforms y Cameras nuevas con el serializador del propio Editor que
/// a mano sobre miles de líneas de YAML -- un fileID mal puesto a mano sí puede corromper la
/// escena, y esto no se puede probar en Play hasta que Raúl lo ejecute.
///
/// QUÉ HACE (idempotente -- se puede volver a ejecutar sin duplicar nada):
///   1) Busca la escena 'WillHouse' entre las escenas abiertas (tiene que estar abierta).
///   2) Crea (o reutiliza) el GameObject "SEQ_DiscusionVentana" con SequencePlayer + SequenceStage,
///      y lo enlaza al asset de la secuencia.
///   3) Busca el SpawnAnchor "Bedroom" (de donde aparece Will al despertar) y el prop
///      "Window10_a02" más cercano a él, para partir de una posición real de la habitación en vez
///      de (0,0,0). Si no encuentra alguno de los dos, avisa y usa un valor por defecto.
///   4) Crea (o reutiliza) las dos marcas de voz VentanaIzq/VentanaDcha a los lados de la ventana
///      (a lo largo de la pared, no atravesándola -- se calcula con el vector perpendicular a la
///      línea Will-ventana) y los dos planos ventanaShot (plano abierto, Will + ventana) y
///      willClose (primer plano de Will), cada uno con Camera + CinematicShot.
///   5) Reutiliza el CinematicCameraDriver que ya existe en la escena (el de
///      PrologueDreamSequencer): el driver solo mueve Camera.main, así que compartirlo entre
///      secuencias no tiene ningún efecto secundario -- es justo para eso.
///
/// Posiciones y encuadres son un punto de partida a ojo, como el resto de este refactor: fácil de
/// arrastrar en la Scene View en cuanto se vea en Play. Al terminar deja un resumen en consola.
/// </summary>
public static class DiscusionVentanaSequenceWiring
{
    private const string DefinitionPath = "Assets/_SEQUENCES/SEQ_DiscusionVentana.asset";
    private const string SceneName = "WillHouse";
    private const string PlayerObjectName = "SEQ_DiscusionVentana";
    private const string BedroomAnchorId = "Bedroom";
    private const string WindowNameHint = "Window10_a02";

    private const string MarkIzqName = "VentanaIzq";
    private const string MarkDchaName = "VentanaDcha";
    private const string ShotVentanaName = "ventanaShot";
    private const string ShotWillCloseName = "willClose";

    private const float MarkSideOffset = 0.6f;
    private const float MarkHeight = 1.6f;
    private const float VentanaShotSideDistance = 1.8f;
    private const float VentanaShotHeight = 1.7f;
    private const float VentanaShotFov = 45f;
    private const float WillCloseDistance = 1.6f;
    private const float WillCloseHeight = 1.5f;
    private const float WillCloseHeadHeight = 1.2f;
    private const float WillCloseFov = 30f;

    [MenuItem("El Sendero/Secuencias/Montar SEQ_DiscusionVentana (WillHouse)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        Scene sc = EditorSceneManager.GetSceneByName(SceneName);
        if (!sc.IsValid() || !sc.isLoaded)
        {
            EditorUtility.DisplayDialog("Montar SEQ_DiscusionVentana",
                $"No encuentro la escena '{SceneName}' abierta.\n\n" +
                $"Ábrela (Assets/Scenes/Interior/{SceneName}.unity) y vuelve a ejecutar esto desde el menú.",
                "Vale");
            return;
        }
        log.AppendLine($"Escena encontrada: '{sc.name}'.");

        var definition = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(DefinitionPath);
        if (definition == null)
        {
            EditorUtility.DisplayDialog("Montar SEQ_DiscusionVentana",
                $"No encuentro el asset de la secuencia en:\n{DefinitionPath}\n\nSin él no hay nada que montar.",
                "Vale");
            return;
        }
        log.AppendLine($"Definición: '{definition.displayName}'.");

        // ── Puntos de referencia en el mundo ──────────────────────────────────
        Transform bedroomAnchor = FindAnchorByStringField<SpawnAnchor>(sc, "anchorId", BedroomAnchorId);
        if (bedroomAnchor == null)
            warnings.Add($"No encuentro el SpawnAnchor '{BedroomAnchorId}' -- coloco todo con valores " +
                         "por defecto, ajústalo a mano en la Scene View.");

        Vector3 anchorPos = bedroomAnchor != null ? bedroomAnchor.position : Vector3.zero;

        Transform window = FindNearestByNameHint(sc, WindowNameHint, anchorPos);
        if (window == null)
            warnings.Add($"No encuentro ningún objeto '{WindowNameHint}' en la escena -- coloco las " +
                         "marcas de la ventana junto a Will, ajústalas a mano.");

        Vector3 windowPos = window != null ? window.position : anchorPos + Vector3.forward * 2f;

        Vector3 flat = windowPos - anchorPos;
        flat.y = 0f;
        Vector3 viewDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        Vector3 sideDir = Vector3.Cross(Vector3.up, viewDir).normalized;

        // ── GameObject raíz: SequencePlayer + SequenceStage ──────────────────
        GameObject go = FindRoot(sc, PlayerObjectName);
        bool isNewRoot = go == null;
        if (isNewRoot)
        {
            go = new GameObject(PlayerObjectName);
            SceneManager.MoveGameObjectToScene(go, sc);
            Undo.RegisterCreatedObjectUndo(go, "Montar SEQ_DiscusionVentana");
            log.AppendLine($"GameObject '{PlayerObjectName}' creado en la raíz de '{sc.name}'.");
        }
        else
        {
            log.AppendLine($"GameObject '{PlayerObjectName}' ya existía -- se reutiliza y se actualiza.");
        }
        go.transform.position = windowPos;

        var player = go.GetComponent<SequencePlayer>() ?? Undo.AddComponent<SequencePlayer>(go);
        var stage  = go.GetComponent<SequenceStage>()  ?? Undo.AddComponent<SequenceStage>(go);

        // Cada pieza se crea y se enlaza EN SU PROPIO try/catch: si algo revienta a mitad
        // (pasó una vez sin dejar rastro claro en consola -- INC posterior a este comentario),
        // que se sepa exactamente en qué paso fue y que lo que sí se pudo montar se guarde igual,
        // en vez de perder todo el trabajo en silencio.
        Transform markIzq = null, markDcha = null;
        GameObject ventanaShot = null, willClose = null;

        try
        {
            // ── Marcas de voz, a los lados de la ventana ─────────────────────
            markIzq  = FindOrCreateChild(go.transform, MarkIzqName,
                windowPos - sideDir * MarkSideOffset + Vector3.up * MarkHeight);
            markDcha = FindOrCreateChild(go.transform, MarkDchaName,
                windowPos + sideDir * MarkSideOffset + Vector3.up * MarkHeight);
            log.AppendLine($"Marcas '{MarkIzqName}'/'{MarkDchaName}' colocadas junto a la ventana.");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo creando las marcas de voz: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }

        try
        {
            // ── Plano abierto (Will + ventana) ───────────────────────────────
            Vector3 mid = Vector3.Lerp(anchorPos, windowPos, 0.5f);
            Vector3 ventanaShotPos = mid + sideDir * VentanaShotSideDistance + Vector3.up * VentanaShotHeight;
            Vector3 ventanaShotLookAt = mid + Vector3.up * MarkHeight;
            ventanaShot = FindOrCreateShot(go.transform, ShotVentanaName, ventanaShotPos, ventanaShotLookAt, VentanaShotFov);
            log.AppendLine($"Plano '{ShotVentanaName}' creado (Camera + CinematicShot).");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo creando el plano '{ShotVentanaName}': {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }

        try
        {
            // ── Primer plano de Will ─────────────────────────────────────────
            Vector3 willCloseHead = anchorPos + Vector3.up * WillCloseHeadHeight;
            Vector3 willClosePos = anchorPos - viewDir * WillCloseDistance + Vector3.up * WillCloseHeight;
            willClose = FindOrCreateShot(go.transform, ShotWillCloseName, willClosePos, willCloseHead, WillCloseFov);
            log.AppendLine($"Plano '{ShotWillCloseName}' creado (Camera + CinematicShot).");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo creando el plano '{ShotWillCloseName}': {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }

        // ── Enlazar SequenceStage -- cada campo se aplica al momento, nunca todo junto al final,
        // para que un fallo en un campo no se lleve por delante los que sí se pudieron enlazar ──
        var soStage = new SerializedObject(stage);
        if (markIzq != null) { SetNamedTransform(soStage, "_marks", 0, MarkIzqName, markIzq); soStage.ApplyModifiedProperties(); }
        if (markDcha != null) { SetNamedTransform(soStage, "_marks", 1, MarkDchaName, markDcha); soStage.ApplyModifiedProperties(); }
        if (ventanaShot != null) { SetNamedTransform(soStage, "_shots", 0, ShotVentanaName, ventanaShot.transform); soStage.ApplyModifiedProperties(); }
        if (willClose != null) { SetNamedTransform(soStage, "_shots", 1, ShotWillCloseName, willClose.transform); soStage.ApplyModifiedProperties(); }

        try
        {
            var existingDriver = Object.FindFirstObjectByType<CinematicCameraDriver>(FindObjectsInactive.Include);
            soStage.FindProperty("_cameraDriver").objectReferenceValue = existingDriver;
            soStage.ApplyModifiedProperties();
            if (existingDriver == null)
                warnings.Add("No hay ningún CinematicCameraDriver en la escena -- ningún corte de cámara " +
                             "hará nada. Añade uno o vuelve a ejecutar esto tras crear uno.");
            else
                log.AppendLine($"Camera driver reutilizado de '{existingDriver.name}' (el mismo que usa " +
                               "PrologueDreamSequencer -- solo mueve Camera.main, compartirlo no tiene " +
                               "efectos secundarios).");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo buscando/enlazando el CinematicCameraDriver: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }

        // ── Enlazar SequencePlayer ────────────────────────────────────────────
        try
        {
            var soPlayer = new SerializedObject(player);
            soPlayer.FindProperty("_definition").objectReferenceValue = definition;
            soPlayer.FindProperty("_stage").objectReferenceValue = stage;
            soPlayer.ApplyModifiedProperties();
            log.AppendLine("Sequence Player: definición y stage enlazados. (Sin música ni transiciones -- " +
                           "el asset no las pide: musicId vacío, sin fundidos definidos.)");
        }
        catch (System.Exception e)
        {
            warnings.Add($"Fallo enlazando el SequencePlayer: {e.Message} (ver stack trace en consola).");
            Debug.LogException(e);
        }

        EditorSceneManager.MarkSceneDirty(sc);
        Selection.activeGameObject = go;

        var final = new StringBuilder();
        final.AppendLine("=== Montaje de SEQ_DiscusionVentana en WillHouse ===");
        final.Append(log);

        if (warnings.Count == 0)
        {
            final.AppendLine();
            final.AppendLine("Sin avisos: todo enlazado. Guarda la escena (Ctrl+S) y dale a Play.");
            Debug.Log(final.ToString(), go);
        }
        else
        {
            final.AppendLine();
            final.AppendLine($"--- {warnings.Count} aviso(s), revisar: ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            final.AppendLine();
            final.AppendLine("Guarda la escena (Ctrl+S) igualmente: lo demás sí ha quedado montado.");
            Debug.LogWarning(final.ToString(), go);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GameObject FindRoot(Scene sc, string name)
    {
        foreach (var root in sc.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static Transform FindOrCreateChild(Transform parent, string name, Vector3 worldPos)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            existing.position = worldPos;
            return existing;
        }
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Montar SEQ_DiscusionVentana");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = worldPos;
        return go.transform;
    }

    private static GameObject FindOrCreateShot(Transform parent, string name, Vector3 worldPos, Vector3 lookAt, float fov)
    {
        var existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Montar SEQ_DiscusionVentana");
            go.transform.SetParent(parent, worldPositionStays: false);
        }
        go.transform.position = worldPos;
        Vector3 dir = lookAt - worldPos;
        go.transform.rotation = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;

        // Camera es concreta (a diferencia del Collider abstracto del aro de runas): añadirla
        // explícitamente antes que CinematicShot es solo por claridad, [RequireComponent] la
        // habría creado igual sola. Undo.AddComponent en vez de AddComponent a secas -- por si
        // acaso un Ctrl+Z después de ejecutar esto se lleva el GameObject pero no el componente
        // (o viceversa), que es exactamente el tipo de estado a medias que ya se vio una vez.
        var cam = go.GetComponent<Camera>();
        if (cam == null) cam = Undo.AddComponent<Camera>(go);
        cam.fieldOfView = fov;
        cam.enabled = false;

        var shot = go.GetComponent<CinematicShot>();
        if (shot == null) shot = Undo.AddComponent<CinematicShot>(go);
        if (string.IsNullOrEmpty(shot.label)) shot.label = name;

        return go;
    }

    private static void SetNamedTransform(SerializedObject so, string listProp, int index, string name, Transform target)
    {
        var prop = so.FindProperty(listProp);
        if (prop.arraySize <= index) prop.arraySize = index + 1;
        var entry = prop.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("name").stringValue = name;
        entry.FindPropertyRelative("target").objectReferenceValue = target;
    }

    /// Busca un componente de tipo T en la escena cuyo campo serializado 'fieldName' (string)
    /// valga 'wanted', y devuelve su Transform. Usa SerializedObject para no depender de si el
    /// campo es público o privado.
    private static Transform FindAnchorByStringField<T>(Scene sc, string fieldName, string wanted) where T : Component
    {
        foreach (var root in sc.GetRootGameObjects())
        foreach (var comp in root.GetComponentsInChildren<T>(true))
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop != null && prop.propertyType == SerializedPropertyType.String && prop.stringValue == wanted)
                return comp.transform;
        }
        return null;
    }

    private static Transform FindNearestByNameHint(Scene sc, string nameHint, Vector3 referencePos)
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var root in sc.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.Contains(nameHint)) continue;
            float d = (t.position - referencePos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }
}
