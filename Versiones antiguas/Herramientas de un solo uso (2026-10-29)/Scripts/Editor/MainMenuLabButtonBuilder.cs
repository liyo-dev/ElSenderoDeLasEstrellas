using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Añade al MainMenu la fila LABORATORIO, que carga el LAB de pruebas (Assets/Scenes/Test/Lab.unity)
/// para los testers. Mismo patrón que MainMenuPatchNotesBugReportBuilder: clona BotonControles para
/// heredar su estilo, le cambia texto y clave de LocalizedText (MainMenu_Lab, ya en ui_es/ui_en) y la
/// coloca justo antes de SALIR. Después engancha el botón al campo labButton de MainMenuController.
///
/// Reparador: si BotonLab ya existe, no lo duplica; solo rehace texto, clave y cableado.
/// Uso: «El Sendero ▸ Combate ▸ Añadir «Laboratorio» al menú principal». Tras ejecutarlo, va a Archivo.
/// </summary>
public static class MainMenuLabButtonBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string Plantilla = "BotonControles";
    const string Salir = "BotonSalir";
    const string Nombre = "BotonLab";

    [MenuItem("El Sendero/Combate/Añadir «Laboratorio» al menú principal")]
    public static void Anadir()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("LAB", "Sal de Play para tocar el menú principal.", "Aceptar");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("LAB", "No se pudo abrir " + ScenePath, "Aceptar");
            return;
        }

        var plantilla = Buscar(Plantilla);
        var salir = Buscar(Salir);
        if (plantilla == null)
        {
            EditorUtility.DisplayDialog("LAB", "No encuentro el botón «" + Plantilla + "» para clonar su estilo.", "Aceptar");
            return;
        }

        var boton = Buscar(Nombre);
        bool creado = boton == null;
        if (creado)
        {
            var clon = Object.Instantiate(plantilla.gameObject, plantilla.transform.parent);
            clon.name = Nombre;
            boton = clon.GetComponent<Button>();
        }

        // Sin listeners heredados de Controles: MainMenuController lo engancha en runtime.
        var so = new SerializedObject(boton);
        so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Justo encima de SALIR (que sigue siendo la última fila).
        if (salir != null && salir.transform.parent == boton.transform.parent
            && boton.transform.GetSiblingIndex() > salir.transform.GetSiblingIndex())
            boton.transform.SetSiblingIndex(salir.transform.GetSiblingIndex());

        var tmp = boton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp) tmp.text = "Laboratorio";
        var loc = boton.GetComponentInChildren<LocalizedText>(true);
        if (loc) loc.key = "MainMenu_Lab";

        var menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("LAB", "No hay MainMenuController en la escena; el botón queda sin función.", "Aceptar");
        }
        else
        {
            var m = new SerializedObject(menu);
            m.FindProperty("labButton").objectReferenceValue = boton;
            m.FindProperty("escenaDelLab").stringValue = "Lab";
            m.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        bool labEnBuild = false;
        foreach (var e in EditorBuildSettings.scenes)
            if (e.enabled && e.path == "Assets/Scenes/Test/Lab.unity") labEnBuild = true;

        EditorUtility.DisplayDialog("LAB",
            (creado ? "Botón «Laboratorio» añadido" : "Botón «Laboratorio» reparado") + " en el menú principal, justo encima de Salir." +
            (labEnBuild ? "" : "\n\nOjo: el LAB aún no está en Build Settings. Ejecuta antes «Crear o regenerar el LAB»."),
            "Aceptar");
    }

    static Button Buscar(string nombre)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
            if (b.gameObject.name == nombre) return b;
        return null;
    }
}
