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
    const string PrefabSilla = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Furniture/Chair/Chair12_a08.prefab";
    static readonly Vector3 Sitio = new Vector3(113.95f, 0f, 1.80f);
    const float Giro = 45f;
    const string MarcaRecostado = "M_Manana_WillRecostado";
    const float AlturaRecostado = 0.89f;

    [MenuItem("El Sendero/Archivo/Capítulo 2/Girar la silla de Eldran hacia Will")]
    public static void Aplicar()
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSilla);
        if (pf != null)
        {
            var wpPrefab = pf.GetComponent<NPCWorldPoint>();
            if (wpPrefab != null && wpPrefab.activityType != NPCAmbientActivity.SitMedium)
            {
                wpPrefab.activityType = NPCAmbientActivity.SitMedium;
                EditorUtility.SetDirty(pf);
                AssetDatabase.SaveAssets();
                Debug.Log("[Cap2] Prefab Chair12_a08 actualizado a SitMedium.");
            }
        }

        var escena = SceneManager.GetSceneByPath(Casa);
        if (!escena.IsValid() || !escena.isLoaded) escena = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var silla = todos.FirstOrDefault(t => t.name == Silla || t.name == NombreNuevo) ?? throw new InvalidOperationException($"No encuentro '{Silla}' en WillHouse.");

        Undo.RecordObject(silla, "Girar la silla de Eldran");
        silla.SetPositionAndRotation(new Vector3(Sitio.x, silla.position.y, Sitio.z), Quaternion.Euler(0f, Giro, 0f));
        silla.gameObject.name = NombreNuevo;

        var wpSilla = silla.GetComponent<NPCWorldPoint>();
        if (wpSilla != null)
        {
            Undo.RecordObject(wpSilla, "Silla de Eldran SitMedium");
            wpSilla.activityType = NPCAmbientActivity.SitMedium;
        }

        var recostado = todos.FirstOrDefault(t => t.name == MarcaRecostado);
        if (recostado != null)
        {
            Undo.RecordObject(recostado, "Ajustar altura de Will recostado");
            recostado.position = new Vector3(recostado.position.x, AlturaRecostado, recostado.position.z);
            Debug.Log($"[Cap2] {MarcaRecostado} ajustado a Y={AlturaRecostado:F2} para apoyar sobre el colchón.");
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log($"[Cap2] Silla de Eldran girada {Giro}° hacia Will en ({Sitio.x}, {Sitio.z}) con SitMedium.");
    }
}
#endif
