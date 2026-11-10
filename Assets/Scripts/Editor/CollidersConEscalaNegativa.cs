using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Los BoxCollider con escala o tamaño negativos: PhysX los fuerza a positivo y la caja acaba en otro sitio
/// (la zona de ambiente de la aldea del mar no se activaba: ni su música ni su ambiente). Este menú
/// les quita el signo al tamaño, y a la escala y al centro, de modo que la caja ocupa el mismo sitio que se ve en
/// el editor. Solo toca objetos sin hijos: con hijos, voltear la escala los movería; esos se listan.
/// Ver INC-973.
/// </summary>
public static class CollidersConEscalaNegativa
{
    private const string Titulo = "Colliders con escala negativa";

    [MenuItem("El Sendero/Escenario/Colliders con escala negativa: arreglar en las escenas abiertas")]
    public static void Arreglar()
    {
        var informe = new StringBuilder();
        int arreglados = 0, conHijos = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            bool tocada = false;
            foreach (var raiz in escena.GetRootGameObjects())
            foreach (var caja in raiz.GetComponentsInChildren<BoxCollider>(true))
            {
                var t = caja.transform;
                var escala = t.localScale;
                string ruta = Ruta(t);
                var tam = caja.size;
                if (tam.x < 0f || tam.y < 0f || tam.z < 0f)
                {
                    // Un tamaño negativo ocupa lo mismo que su valor absoluto: la caja es simétrica.
                    Undo.RecordObject(caja, Titulo);
                    caja.size = new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z));
                    EditorUtility.SetDirty(caja);
                    tocada = true;
                    arreglados++;
                    informe.AppendLine($"Tamaño negativo arreglado: {escena.name}/{ruta}");
                }
                if (escala.x >= 0f && escala.y >= 0f && escala.z >= 0f)
                {
                    var global = t.lossyScale;
                    if (global.x < 0f || global.y < 0f || global.z < 0f)
                        informe.AppendLine($"Escala negativa heredada del padre, sin tocar: {escena.name}/{ruta}");
                    continue;
                }
                if (t.childCount > 0) { conHijos++; informe.AppendLine($"Con hijos, sin tocar: {escena.name}/{ruta}"); continue; }

                Undo.RecordObject(t, Titulo);
                Undo.RecordObject(caja, Titulo);
                var centro = caja.center;
                if (escala.x < 0f) { escala.x = -escala.x; centro.x = -centro.x; }
                if (escala.y < 0f) { escala.y = -escala.y; centro.y = -centro.y; }
                if (escala.z < 0f) { escala.z = -escala.z; centro.z = -centro.z; }
                t.localScale = escala;
                caja.center = centro;
                EditorUtility.SetDirty(t);
                EditorUtility.SetDirty(caja);
                tocada = true;
                arreglados++;
                informe.AppendLine($"Arreglado: {escena.name}/{ruta}");
            }
            if (tocada) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
        }
        informe.Insert(0, $"{arreglados} arreglados, {conHijos} con hijos sin tocar.\n");
        OrdenesDelEditor.Avisar(Titulo, informe.ToString());
    }

    private static string Ruta(Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
