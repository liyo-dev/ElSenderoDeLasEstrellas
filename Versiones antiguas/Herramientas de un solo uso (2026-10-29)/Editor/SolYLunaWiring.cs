using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Le pone el sol y la luna al ciclo día/noche de las escenas abiertas (INC-371).
///
/// El componente (`SolYLunaEnElCielo`) va en el mismo GameObject que el `DayNightCycle`, porque es
/// de ahí de donde saca la luz direccional que los mueve. No hay nada que configurar: las texturas
/// se generan solas y el tamaño se pide en grados de cielo.
///
/// Idempotente, y se ejecuta también desde PREPARAR TODO para que el prólogo lo tenga.
public static class SolYLunaWiring
{
    [MenuItem("El Sendero/Mundo: añadir Sol y Luna al ciclo día-noche")]
    public static void Menu()
    {
        int n = Ejecutar(avisar: true);
        EditorUtility.DisplayDialog("Sol y Luna",
            n == 0
                ? "No he tocado nada: o ya lo tenían, o en las escenas abiertas no hay ningún DayNightCycle."
                : $"Sol y luna añadidos en {n} escena(s). Guarda con Ctrl+S.",
            "Vale");
    }

    public static int Ejecutar(bool avisar)
    {
        int tocadas = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (var raiz in escena.GetRootGameObjects())
            {
                var ciclo = raiz.GetComponentInChildren<DayNightCycle>(true);
                if (ciclo == null) continue;

                if (ciclo.GetComponent<SolYLunaEnElCielo>() != null) continue;

                Undo.AddComponent<SolYLunaEnElCielo>(ciclo.gameObject);
                EditorUtility.SetDirty(ciclo.gameObject);
                EditorSceneManager.MarkSceneDirty(escena);
                tocadas++;

                Debug.Log($"[SolYLuna] Sol y luna añadidos al ciclo día/noche de '{escena.name}' " +
                    $"(objeto '{ciclo.name}'). Se mueven con la luz direccional del propio ciclo.");
                break;
            }
        }

        if (tocadas == 0 && avisar)
            Debug.Log("[SolYLuna] Nada que hacer: las escenas abiertas ya lo tenían o no tienen ciclo día/noche.");

        return tocadas;
    }
}
