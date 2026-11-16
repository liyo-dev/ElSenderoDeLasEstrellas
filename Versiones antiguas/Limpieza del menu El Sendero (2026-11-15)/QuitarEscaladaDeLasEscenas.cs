using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Paso único de INC-717 (tanda 2): quita de las escenas los datos guardados de la escalada.
/// En Start, la ficha «Escalar» del aviso de habilidad nueva (y Volar pasa a su propio icono,
/// que tenía el de escalar); en Chuchelandia, la escalada que concedía la zona de la torre.
/// Abre en aditivo la escena que no esté cargada y la cierra al acabar; una escena cargada con
/// cambios sin guardar se deja tal cual y se avisa. Se retira después de usarlo.
public static class QuitarEscaladaDeLasEscenas
{
    const string Titulo = "Quitar escalada";
    const int EscalarEnAbilityKey = 2;
    const int EscalarEnPlayerAbility = 10;
    const string IconoDeEscalar = "Assets/Art/UI/Abilities/climb.png";
    const string IconoDeVolar = "Assets/Art/UI/Abilities/fly.png";

    [MenuItem("El Sendero/Pruebas/Cuerpos propios/Quitar escalada de Start y Chuchelandia")]
    static void Ejecutar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar(Titulo, "Fuera de Play."); return; }
        var sb = new StringBuilder();
        EnEscena("Assets/Scenes/Systems/Start.unity", sb, LimpiarStart);
        EnEscena("Assets/Scenes/Worlds/Chuchelandia.unity", sb, LimpiarChuchelandia);
        OrdenesDelEditor.Avisar(Titulo, sb.ToString());
    }

    static void EnEscena(string ruta, StringBuilder sb, System.Func<Scene, StringBuilder, int> limpiar)
    {
        var escena = SceneManager.GetSceneByPath(ruta);
        bool abierta = escena.IsValid() && escena.isLoaded;
        if (abierta && escena.isDirty) { sb.Append($"{escena.name}: tiene cambios sin guardar de otro trabajo, no la toco. "); return; }
        if (!abierta) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        int cambios = limpiar(escena, sb);
        if (cambios > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            bool ok = EditorSceneManager.SaveScene(escena);
            sb.Append($"{escena.name}: {cambios} cambios, guardada={ok}. ");
        }
        else sb.Append($"{escena.name}: nada que quitar. ");
        if (!abierta) EditorSceneManager.CloseScene(escena, true);
    }

    static int LimpiarStart(Scene escena, StringBuilder sb)
    {
        int cambios = 0;
        var escalar = AssetDatabase.LoadAssetAtPath<Sprite>(IconoDeEscalar);
        var volar = AssetDatabase.LoadAssetAtPath<Sprite>(IconoDeVolar);
        foreach (var raiz in escena.GetRootGameObjects())
        foreach (var aviso in raiz.GetComponentsInChildren<AbilityUnlockPopupUI>(true))
        {
            var so = new SerializedObject(aviso);
            var lista = so.FindProperty("abilityKeyPresentations");
            for (int i = lista.arraySize - 1; i >= 0; i--)
            {
                var e = lista.GetArrayElementAtIndex(i);
                int clave = e.FindPropertyRelative("abilityKey").intValue;
                if (clave == EscalarEnAbilityKey) { lista.DeleteArrayElementAtIndex(i); cambios++; sb.Append("ficha Escalar quitada; "); continue; }
                var icono = e.FindPropertyRelative("icon");
                if (clave == (int)AbilityKey.Fly && escalar != null && volar != null && icono.objectReferenceValue == escalar)
                { icono.objectReferenceValue = volar; cambios++; sb.Append("Volar con su icono; "); }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return cambios;
    }

    static int LimpiarChuchelandia(Scene escena, StringBuilder sb)
    {
        int cambios = 0;
        foreach (var raiz in escena.GetRootGameObjects())
        foreach (var zona in raiz.GetComponentsInChildren<ZonaQueConcedeHabilidades>(true))
        {
            var so = new SerializedObject(zona);
            var lista = so.FindProperty("habilidadesConcedidas");
            for (int i = lista.arraySize - 1; i >= 0; i--)
            {
                if (lista.GetArrayElementAtIndex(i).intValue != EscalarEnPlayerAbility) continue;
                lista.DeleteArrayElementAtIndex(i); cambios++;
                sb.Append($"«{zona.name}» ya no concede escalar; ");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return cambios;
    }
}
