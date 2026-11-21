using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Diagnóstico del viaje rápido del LAB (pendientes de combate en grupo, A.2: Raúl no ve la zona
/// «Lucha contra personas (oeste)»). Apunta en el registro cuántos ViajeDelLab hay, si están
/// activos, y cada destino con su punto (vacío, apagado o dónde está), y si la zona de lucha
/// existe y está encendida. No cambia nada.
/// </summary>
public static class ComprobarViajeDelLab
{
    private const string ScenePath = "Assets/Scenes/Test/Lab.unity";

    [MenuItem("El Sendero/Combate/LAB: comprobar el viaje rápido")]
    public static void Comprobar()
    {
        var sb = new StringBuilder();
        Scene escena = SceneManager.GetSceneByPath(ScenePath);
        bool abiertaAqui = false;
        if (!escena.IsValid() || !escena.isLoaded)
        {
            if (Application.isPlaying) { OrdenesDelEditor.Avisar("LAB", "Sal de Play o abre el LAB."); return; }
            escena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            abiertaAqui = true;
        }
        try
        {
            int viajes = 0;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                foreach (var viaje in raiz.GetComponentsInChildren<ViajeDelLab>(true))
                {
                    viajes++;
                    sb.Append($"Viaje «{Ruta(viaje.transform)}» activo={viaje.isActiveAndEnabled || viaje.gameObject.activeInHierarchy}: ");
                    var so = new SerializedObject(viaje);
                    var destinos = so.FindProperty("destinos");
                    for (int i = 0; i < destinos.arraySize; i++)
                    {
                        var d = destinos.GetArrayElementAtIndex(i);
                        string nombre = d.FindPropertyRelative("nombre").stringValue;
                        var punto = d.FindPropertyRelative("punto").objectReferenceValue as Transform;
                        sb.Append(punto == null ? $"[{nombre}: SIN PUNTO] "
                            : $"[{nombre}: {punto.position} {(punto.gameObject.activeInHierarchy ? "" : "APAGADO ")}en «{Ruta(punto)}»] ");
                    }
                    sb.Append(" · ");
                }
                if (raiz.name.Contains("Lucha contra personas") || raiz.name.Contains("LuchaContraNpcs"))
                    sb.Append($"Zona «{raiz.name}» activa={raiz.activeInHierarchy} hijos={raiz.transform.childCount} · ");
                foreach (Transform hijo in raiz.transform)
                    if (hijo.name.Contains("Lucha contra personas"))
                        sb.Append($"Zona «{Ruta(hijo)}» activa={hijo.gameObject.activeInHierarchy} en {hijo.position} · ");
            }
            sb.Insert(0, $"ViajeDelLab en la escena: {viajes} · ");
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
        Debug.Log("[LAB] " + sb);
        OrdenesDelEditor.Avisar("LAB", sb.ToString());
    }

    private static string Ruta(Transform t)
    {
        string r = t.name;
        while (t.parent != null) { t = t.parent; r = t.name + "/" + r; }
        return r;
    }
}
