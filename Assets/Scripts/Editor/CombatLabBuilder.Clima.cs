using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Clima del LAB: el mismo sistema que MainWorld (DayNightCycle y los componentes de su objeto:
/// cielo, astros, estrellas y nubes de buen tiempo), copiado con sus valores para que el LAB se vea igual que el
/// mundo. Arranca de día, con el reloj quieto y sin sorteo, y se maneja desde la pestaña «Clima»
/// del panel (ClimaDelLab).
public static partial class CombatLabBuilder
{
    private const string EscenaConClima = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string RaizClima = "LAB_CLIMA";

    /// La escena del LAB tiene que estar guardada: MainWorld se abre a su lado en aditivo.
    private static List<string> CrearClima(Scene lab, Light luz)
    {
        var avisos = new List<string>();
        var raiz = new GameObject(RaizClima);
        SceneManager.MoveGameObjectToScene(raiz, lab);

        Scene mundo = EditorSceneManager.OpenScene(EscenaConClima, OpenSceneMode.Additive);
        try
        {
            DayNightCycle origen = null;
            foreach (var r in mundo.GetRootGameObjects())
            {
                origen = r.GetComponentInChildren<DayNightCycle>(true);
                if (origen != null) break;
            }

            if (origen == null)
            {
                avisos.Add("MainWorld no tiene DayNightCycle: el LAB lleva uno con los valores por defecto.");
                raiz.AddComponent<DayNightCycle>();
            }
            else
            {
                CopiarComponentes(origen.gameObject, raiz);
                // Las referencias a objetos de MainWorld se cambian ANTES de cerrarla.
                RecolocarReferencias(origen.gameObject, raiz, luz, avisos);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(mundo, true);
            SceneManager.SetActiveScene(lab);
        }

        var ciclo = raiz.GetComponent<DayNightCycle>();
        var so = new SerializedObject(ciclo);
        so.FindProperty("directionalLight").objectReferenceValue = luz;
        so.FindProperty("autoAdvance").boolValue = false;
        so.FindProperty("sortearClima").boolValue = false;
        var franjas = so.FindProperty("timeSettings");
        for (int i = 0; i < franjas.arraySize; i++)
            if (franjas.GetArrayElementAtIndex(i).FindPropertyRelative("timeOfDay").intValue == (int)DayNightCycle.TimeOfDay.AfterNoon)
                so.FindProperty("startingTimeIndex").intValue = i;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Lluvia, cielos por franja, sol, luna y fugaces: lo mismo que el menú de clima.
        ClimaDelCicloWiring.Ejecutar(avisar: false);
        ClimaDelCicloWiring.CompletarCielos();
        return avisos;
    }

    private static void CopiarComponentes(GameObject origen, GameObject destino)
    {
        foreach (var componente in origen.GetComponents<MonoBehaviour>())
        {
            if (componente == null) continue;   // script que ya no existe
            var tipo = componente.GetType();
            // RequireComponent puede haberlo añadido ya al copiar otro.
            Component copia = destino.GetComponent(tipo);
            if (copia == null) copia = destino.AddComponent(tipo);
            if (copia != null) EditorUtility.CopySerialized(componente, copia);
        }
    }

    private static Scene EscenaDe(Object valor)
    {
        if (valor is GameObject go) return go.scene;
        if (valor is Component c) return c.gameObject.scene;
        return default;
    }

    /// Lo que apuntaba al objeto de MainWorld pasa al del LAB; las luces, a la luz del LAB; lo demás
    /// de MainWorld no existe aquí y se vacía con aviso.
    private static void RecolocarReferencias(GameObject origen, GameObject destino, Light luz, List<string> avisos)
    {
        foreach (var componente in destino.GetComponents<MonoBehaviour>())
        {
            if (componente == null) continue;
            var so = new SerializedObject(componente);
            var it = so.GetIterator();
            bool cambio = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object valor = it.objectReferenceValue;
                if (valor == null || EditorUtility.IsPersistent(valor)) continue;
                if (EscenaDe(valor) != origen.scene) continue;   // ya es del LAB (m_GameObject y demás)

                Object nuevo = null;
                if (valor is Light) nuevo = luz;
                else if (valor == origen) nuevo = destino;
                else if (valor is Component c && c.gameObject == origen) nuevo = destino.GetComponent(c.GetType());
                else avisos.Add($"{componente.GetType().Name}.{it.propertyPath} apuntaba a '{valor.name}' de MainWorld: queda vacío en el LAB.");

                it.objectReferenceValue = nuevo;
                cambio = true;
            }
            if (cambio) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
