using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Devuelve a MainWorld el ancla «House_FrontDoor» (INC-1272). La salida de WillHouse teletransporta
/// ahí y los presets de la carta la usan como punto de aparición. Sin ella el teletransporte falla
/// pero el portal descarga WillHouse igual: el jugador se queda cayendo en el vacío con todo negro.
/// Menú: «El Sendero/Escenario/Mundo/Anclas: reponer House_FrontDoor (INC-1272)».
/// </summary>
public static class ReponerAnclaCasaDeWill
{
    const string Ruta = "Assets/Scenes/Worlds/MainWorld.unity";
    const string Id = "House_FrontDoor";
    const string Grupo = "ANCHORS";

    // Donde estaba: en el grupo ANCHORS, sobre el suelo de la puerta y bajo el aviso de salida.
    static readonly Vector3 Posicion = new(-0.18f, 26.96f, -132.51f);
    const float Rumbo = 90f;

    [MenuItem("El Sendero/Escenario/Mundo/Anclas: reponer House_FrontDoor (INC-1272)")]
    public static void Ejecutar()
    {
        var informe = new StringBuilder();
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Ancla de la casa", "Hazlo fuera de Play."); return; }

        var escena = SceneManager.GetSceneByPath(Ruta);
        bool abierta = escena.IsValid() && escena.isLoaded;
        try
        {
            if (abierta && escena.isDirty)
            {
                informe.AppendLine($"{Ruta}: tiene cambios sin guardar; no se toca.");
                return;
            }
            if (!abierta) escena = EditorSceneManager.OpenScene(Ruta, OpenSceneMode.Additive);

            foreach (var a in Object.FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include))
                if (a != null && a.anchorId == Id)
                {
                    informe.AppendLine($"Ya hay un ancla «{Id}»: {a.name} en {a.gameObject.scene.name}.");
                    return;
                }

            Transform grupo = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (raiz.name == Grupo) { grupo = raiz.transform; break; }
                var h = raiz.transform.Find(Grupo);
                if (h != null) { grupo = h; break; }
            }
            if (grupo == null) { informe.AppendLine($"No encuentro el grupo «{Grupo}» en {Ruta}."); return; }

            var go = new GameObject(Id);
            go.transform.SetParent(grupo, false);
            go.transform.localPosition = Posicion;
            go.transform.localRotation = Quaternion.Euler(0f, Rumbo, 0f);
            go.transform.localScale = Vector3.one;
            var ancla = go.AddComponent<SpawnAnchor>();
            ancla.anchorId = Id;
            ancla.faceDoor = false;

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            informe.AppendLine($"Ancla «{Id}» puesta en {Grupo} a {Posicion} mirando {Rumbo}°; escena guardada.");
        }
        catch (System.Exception e) { informe.AppendLine("ERROR: " + e); }
        finally
        {
            if (!abierta && escena.IsValid() && escena.isLoaded) EditorSceneManager.CloseScene(escena, true);
            Debug.Log("[ReponerAnclaCasaDeWill] " + informe.ToString().Replace('\n', ' '));
            OrdenesDelEditor.Avisar("Ancla de la casa", informe.ToString());
        }
    }
}
