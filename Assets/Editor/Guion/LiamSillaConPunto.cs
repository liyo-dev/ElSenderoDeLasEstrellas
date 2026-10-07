#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// La silla de Liam en su torre pasa a ser una Chair12_a08 a su tamaño, que trae su punto de
/// sentarse (NPCWorldPoint): el guion la usa con «liam sienta silla ya», igual que Will y los NPCs.
/// Antes era una Chair12_a01 escalada sin punto y el sitio se ponía a mano en el guion (0,29 m).
/// Hace en la escena lo mismo que ConstruirTorreDeLiam ya hace al construirla, sin reconstruir nada más.
public static class LiamSillaConPunto
{
    const string Escena = "Assets/Scenes/Interior/TorreDeLiam.unity";

    [MenuItem("El Sendero/Archivo/Liam/Silla con punto de sentarse")]
    public static void Aplicar()
    {
        var escena = SceneManager.GetSceneByPath(Escena);
        if (!escena.IsValid() || !escena.isLoaded) escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Additive);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var vieja = todos.FirstOrDefault(t => t.name == ConstruirTorreDeLiam.SillaDeLiam) ?? todos.FirstOrDefault(t => t.name == "Silla")
                    ?? throw new InvalidOperationException("No encuentro la silla de Liam en TorreDeLiam.");
        var padre = vieja.parent;
        UnityEngine.Object.DestroyImmediate(vieja.gameObject);
        // Mismo sitio que en ConstruirTorreDeLiam (local a la torre).
        ConstruirTorreDeLiam.PropTalCual(ConstruirTorreDeLiam.SillaDeLiam,
            "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Furniture/Chair/Chair12_a08.prefab",
            new Vector3(0f, 0f, 0.95f), new Vector3(0f, 180f, 0f), padre);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Liam] Silla de Liam cambiada por Chair12_a08 con su punto de sentarse.");
    }
}
#endif
