using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Arregla el aviso "Quaternion To Matrix conversion failed ... l=1.000010" que sale al abrir
/// Prologo_Valle (18 sep 2026, a petición de Raúl).
///
/// Causa verificada en el log de Unity: al guardar una escena en texto, Unity escribe los floats
/// de cada rotación con precisión limitada. Un cuaternión de rotación en Y, al redondearse así,
/// deja de tener magnitud exactamente 1 (sale 1.000010, 0.999987, etc. en vez de 1.0 exacto).
/// El bake de NavMesh, al añadir sus datos con NavMesh.AddNavMeshData en cada OnEnable de
/// NavMeshSurface, hace una comprobación de normalización más estricta que el resto del motor, y
/// esos objetos concretos (Arbol_03, Arbol_22, Arbol_25, Loma_08, Ribera_Junco_04, confirmados
/// por el propio valor del cuaternión en el log) caen justo al otro lado de esa comprobación.
///
/// No es un fallo de cómo se colocaron esos objetos ni afecta a su orientación visual (el error
/// angular es de milésimas de grado) — es puramente un residuo de la serialización de texto de
/// Unity, que puede aparecer en cualquier objeto rotado. Esta herramienta recorre TODOS los
/// Transform de la escena activa y reescribe con Quaternion.Normalize cualquier rotación cuya
/// magnitud se haya desviado de 1 más de lo normal, no solo los 5 que ya salieron en el log —
/// así no hace falta repetir esto cada vez que aparezca uno nuevo por el mismo motivo.
///
/// Menú: "El Sendero/Navegación/...".
/// </summary>
public static class NormalizarRotacionesEscena
{
    // Un cuaternión recién normalizado por el motor se desvía de 1 en, como mucho, el épsilon de
    // un float (~1e-7). Cualquier cosa por encima de esto es redondeo de la serialización de texto
    // de Unity acumulado tras guardar la escena unas cuantas veces, y merece la pena limpiarlo.
    private const float ToleranciaMagnitud = 1e-6f;

    [MenuItem("El Sendero/Navegación/Normalizar rotaciones de la escena")]
    public static void Normalizar()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[NormalizarRotacionesEscena] No hay ninguna escena activa válida.");
            return;
        }

        int corregidos = 0;
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                var q = t.localRotation;
                float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (Mathf.Abs(mag - 1f) > ToleranciaMagnitud)
                {
                    Undo.RecordObject(t, "Normalizar rotación");
                    float inv = 1f / mag;
                    t.localRotation = new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
                    corregidos++;
                }
            }
        }

        if (corregidos > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(corregidos > 0
            ? $"[NormalizarRotacionesEscena] {corregidos} rotación(es) renormalizada(s) en '{scene.name}'. Guarda la escena (Ctrl+S) para que quede fijo."
            : $"[NormalizarRotacionesEscena] Nada que normalizar en '{scene.name}' — todas las rotaciones ya están dentro de tolerancia.");
    }
}
