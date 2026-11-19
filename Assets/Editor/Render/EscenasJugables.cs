#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// Escenas sobre las que trabajan las herramientas de render: las abiertas, o todas las jugables (las de
/// Assets/Scenes/Worlds e Interior; un nivel nuevo entra solo). Las que no estaban abiertas se abren en
/// aditivo, se guardan si cambian y se vuelven a cerrar. Una escena que ya estaba abierta con cambios sin
/// guardar se cambia pero no se guarda: esos cambios no son de la herramienta y se avisa en el informe.
public static class EscenasJugables
{
    private static readonly string[] Carpetas = { "Assets/Scenes/Worlds", "Assets/Scenes/Interior" };

    public static string[] Rutas() =>
        AssetDatabase.FindAssets("t:Scene", Carpetas)
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(r => r.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();

    /// Aplica <paramref name="accion"/> a cada escena; la acción escribe su línea en el informe y devuelve si
    /// ha cambiado algo.
    public static void ParaCada(bool todasLasJugables, Func<Scene, StringBuilder, bool> accion, StringBuilder informe)
    {
        var rutas = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var abierta = SceneManager.GetSceneAt(i);
            if (abierta.isLoaded) rutas.Add(abierta.path);
        }
        if (todasLasJugables)
            foreach (var ruta in Rutas())
                if (!rutas.Contains(ruta)) rutas.Add(ruta);

        foreach (var ruta in rutas)
        {
            var escena = SceneManager.GetSceneByPath(ruta);
            bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
            if (abiertaAqui) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            bool teniaCambios = !abiertaAqui && escena.isDirty;

            if (accion(escena, informe))
            {
                EditorSceneManager.MarkSceneDirty(escena);
                if (teniaCambios) informe.Append($"  - {escena.name} tenía cambios sin guardar de antes: queda cambiada, sin guardar\n");
                else EditorSceneManager.SaveScene(escena);
            }
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }
}
#endif
