#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Prueba de un solo uso (INC-1325): en Play, abre la cámara de diálogo con el NPC con cara más
/// cercano y pasa del plano del jugador al del NPC, para hacer fotos sin jugar hasta una charla.
public static class PruebaDePlanosDeDialogo
{
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: plano de diálogo con el NPC más cercano")]
    static void Empezar()
    {
        var jugador = PlayerService.Player;
        if (jugador == null || DialogueCinematicController.Instance == null) { OrdenesDelEditor.Avisar("Prueba", "sin jugador o sin controlador"); return; }
        var npc = Object.FindObjectsByType<NPCEmotionController>(FindObjectsInactive.Exclude)
            .Where(e => !e.transform.IsChildOf(jugador.transform) && jugador.transform != e.transform)
            .OrderBy(e => (e.transform.position - jugador.transform.position).sqrMagnitude).FirstOrDefault();
        if (npc == null) { OrdenesDelEditor.Avisar("Prueba", "sin NPC"); return; }
        DialogueCinematicController.Instance.StartCinematic(jugador.transform, npc.transform);
        OrdenesDelEditor.Avisar("Prueba", $"diálogo con {npc.name} a {Vector3.Distance(npc.transform.position, jugador.transform.position):F1} m");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: habla el jugador")]
    static void HablaElJugador() => DialogueCinematicController.Instance?.OnDialogueLineAdvanced(1, 4, new DialogueLine { isPlayerSpeaking = true });

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: habla el NPC")]
    static void HablaElNpc() => DialogueCinematicController.Instance?.OnDialogueLineAdvanced(2, 4, new DialogueLine { isPlayerSpeaking = false });

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: terminar el diálogo")]
    static void Terminar() => DialogueCinematicController.Instance?.EndCinematicNow();

    static ContornoDeTinta ContornoDelPerfil() =>
        AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/DefaultVolumeProfile.asset")?.components.OfType<ContornoDeTinta>().FirstOrDefault();

    static void Variante(float suavidad, float pliegue, float peso, float grosor = 2f)
    {
        var c = ContornoDelPerfil();
        if (c == null) { OrdenesDelEditor.Avisar("Prueba", "sin contorno en el perfil"); return; }
        c.suavidadDelTrazo.Override(suavidad); c.umbralDePliegue.Override(pliegue); c.pesoDePliegues.Override(peso); c.grosor.Override(grosor);
        OrdenesDelEditor.Avisar("Prueba", $"contorno: suavidad {suavidad}, pliegue {pliegue} x {peso}, grosor {grosor}");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno como estaba")] static void V0() => Variante(1f, 0.45f, 0.55f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno limpio")] static void V1() => Variante(0.25f, 0.6f, 0.8f);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno solo siluetas")] static void V2() => Variante(0.25f, 0.6f, 0f);

    static void BordeDeLuz(bool encendido)
    {
        int n = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_BordeDeLuz") || m.GetFloat("_BordeDeLuz") <= 0f) continue;
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            if (encendido) b.SetFloat("_BordeDeLuz", m.GetFloat("_BordeDeLuz")); else b.SetFloat("_BordeDeLuz", 0f);
            r.SetPropertyBlock(b);
            n++;
        }
        OrdenesDelEditor.Avisar("Prueba", $"borde de luz {(encendido ? "encendido" : "apagado")} en {n} renderers");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: sin borde de luz")] static void B0() => BordeDeLuz(false);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: con borde de luz")] static void B1() => BordeDeLuz(true);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: contorno limpio y grueso")] static void V3() => Variante(0.25f, 0.6f, 0.8f, 3f);

    static void Emitir(Core.GamepadInputReader.InputEventType tipo)
    {
        var emit = typeof(Core.GamepadInputReader).GetMethod("Emit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        emit?.Invoke(null, new object[] { tipo, Vector2.zero, UnityEngine.InputSystem.InputActionPhase.Performed, false, false });
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: pulsar interactuar")] static void Interactuar() => Emitir(Core.GamepadInputReader.InputEventType.Interact);
    [MenuItem("El Sendero/Aspecto de dibujo/Prueba: pulsar avanzar")] static void Avanzar() => Emitir(Core.GamepadInputReader.InputEventType.Submit);
}
#endif
