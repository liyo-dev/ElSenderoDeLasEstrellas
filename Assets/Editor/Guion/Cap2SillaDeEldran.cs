#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// «La mañana después»: la silla de Eldran mira a Will. Mirando a la mesa, Eldran se sentaba de
/// lado para hablar con Will (o de perfil a la cámara); girando la silla 45° hacia Will se sienta
/// derecho y de cara a él. Se aparta un poco de la mesa para que la esquina no la atraviese.
///
/// Dónde y cómo se sienta no lo decide este menú: lo dice el punto de sentarse del prefab
/// (NPCWorldPoint → «InteractablePoint»), el mismo que usan Will y los NPCs. La silla se renombra
/// «Silla_Eldran» para que el guion la nombre («silla Silla_Eldran» y «eldran sienta silla»).
public static class Cap2SillaDeEldran
{
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Silla = "Chair12_a08 (3)";
    const string NombreNuevo = "Silla_Eldran";
    static readonly Vector3 Sitio = new Vector3(114.1f, 0f, 1.95f);
    const float Giro = 45f;

    [MenuItem("El Sendero/Archivo/Capítulo 2/Girar la silla de Eldran hacia Will")]
    public static void Aplicar()
    {
        var escena = SceneManager.GetSceneByPath(Casa);
        if (!escena.IsValid() || !escena.isLoaded) escena = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var silla = todos.FirstOrDefault(t => t.name == Silla || t.name == NombreNuevo) ?? throw new InvalidOperationException($"No encuentro '{Silla}' en WillHouse.");

        Undo.RecordObject(silla, "Girar la silla de Eldran");
        silla.SetPositionAndRotation(new Vector3(Sitio.x, silla.position.y, Sitio.z), Quaternion.Euler(0f, Giro, 0f));
        silla.gameObject.name = NombreNuevo;

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log($"[Cap2] Silla de Eldran girada {Giro}° hacia Will en ({Sitio.x}, {Sitio.z}).");
    }
}
#endif
