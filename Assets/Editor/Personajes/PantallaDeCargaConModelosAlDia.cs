using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cambia los tres personajes del escenario de la pantalla de carga (LoadingScreen.unity) por instancias
/// enlazadas de los modelos ligeros RetratoVivo_&lt;nombre&gt;.prefab, los mismos que usan los retratos vivos
/// y que mantienen al día los menús de Personajes (aspecto y caras). Así el escenario sigue siempre el
/// aspecto actual de los protagonistas sin copias propias. Conserva el objeto *_LoadingShowcase y su
/// LoadingShowcaseCharacter (los referencia CharacterStage); quita su contenido copiado. Ver INC-915.
/// </summary>
public static class PantallaDeCargaConModelosAlDia
{
    const string Escena = "Assets/Scenes/Systems/LoadingScreen.unity";
    const string CarpetaModelos = "Assets/Prefabs/UI/RetratosVivos";

    [MenuItem("El Sendero/Personajes/Pantalla de carga: modelos al día (INC-915)")]
    public static void Aplicar()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Pantalla de carga", "Hazlo fuera de Play.", "Vale"); return; }
        var abierta = EditorSceneManager.GetSceneByPath(Escena);
        bool cerrar = !abierta.isLoaded;
        if (cerrar && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var escena = cerrar ? EditorSceneManager.OpenScene(Escena, OpenSceneMode.Additive) : abierta;
        var informe = new System.Text.StringBuilder();
        try
        {
            var vitrinas = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LoadingShowcaseCharacter>(true)).ToArray();
            foreach (var v in vitrinas)
            {
                string nombre = v.name.Split('_')[0];
                nombre = nombre.Substring(0, 1).ToUpper() + nombre.Substring(1).ToLower();
                string ruta = $"{CarpetaModelos}/RetratoVivo_{nombre}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (prefab == null) { informe.AppendLine($"{v.name}: no encuentro {ruta}."); continue; }

                // El controller de la vitrina y la colocación del modelo viejo (en la raíz o en un hijo)
                var animViejo = v.GetComponentInChildren<Animator>(true);
                var controlador = animViejo ? animViejo.runtimeAnimatorController : null;
                bool enHijo = animViejo && animViejo.transform != v.transform;
                Vector3 pos = enHijo ? animViejo.transform.localPosition : Vector3.zero;
                Quaternion rot = enHijo ? animViejo.transform.localRotation : Quaternion.identity;
                Vector3 escala = enHijo ? animViejo.transform.localScale : Vector3.one;

                // Fuera la copia: hijos y componentes que no sean Transform ni la propia vitrina
                for (int i = v.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(v.transform.GetChild(i).gameObject);
                foreach (var c in v.GetComponents<Component>())
                    if (!(c is Transform) && !(c is LoadingShowcaseCharacter) && !(c is Animator)) Object.DestroyImmediate(c);
                var animRaiz = v.GetComponent<Animator>();
                if (animRaiz) Object.DestroyImmediate(animRaiz);

                var modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, v.transform);
                modelo.transform.localPosition = pos;
                modelo.transform.localRotation = rot;
                modelo.transform.localScale = escala;
                var anim = modelo.GetComponentInChildren<Animator>(true);
                if (anim && controlador) anim.runtimeAnimatorController = controlador;
                if (anim) anim.applyRootMotion = false;
                informe.AppendLine($"{v.name}: {prefab.name}{(controlador ? $" con {controlador.name}" : " (sin controller)")}.");
            }
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        finally { if (cerrar) EditorSceneManager.CloseScene(escena, true); }
        if (informe.Length == 0) informe.AppendLine("No he encontrado personajes en el escenario.");
        Debug.Log("[PantallaDeCargaConModelosAlDia] " + informe.ToString().Replace('\n', ' '));
        EditorUtility.DisplayDialog("Pantalla de carga", informe.ToString(), "Vale");
    }
}
