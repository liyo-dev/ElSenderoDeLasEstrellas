#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// El botón de saltar (HoldToSkipUI) tenía una llamada persistente a
/// `LoadScene.LoadTargetScene` sin objeto destino. Al saltar, Unity loguea un error (el destino
/// de la llamada es null) y ese método es de instancia, así que no puede invocarse así; además
/// la escena destino estaba vacía. No aporta nada: el salto lo gestiona `CinematicSequencerBase`
/// (que levanta la señal de fin de la secuencia) y `NarrativeSkipHub`.
///
/// Menú de un solo uso (ejecutado el 22 de octubre de 2026):
/// *El Sendero ▸ Archivo ▸ Quitar la llamada huérfana del botón de saltar (INC-822)*.
public static class QuitarLlamadaHuerfanaDelSalto
{
    private const string Prefab = "Assets/Prefabs/HoldToSkipUI.prefab";

    [MenuItem("El Sendero/Archivo/Quitar la llamada huérfana del botón de saltar (INC-822)")]
    public static void Ejecutar()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (prefab == null)
        {
            Debug.LogError($"[Salto] No encuentro el prefab en {Prefab}");
            return;
        }
        var ui = prefab.GetComponent<HoldToSkipUI>();
        if (ui == null)
        {
            Debug.LogError("[Salto] El prefab no tiene el componente HoldToSkipUI");
            return;
        }

        // Se vacía la lista de llamadas persistentes del UnityEvent por serialización: es la vía
        // estable y no depende de la API de AssetDatabase para eventos.
        var so = new SerializedObject(ui);
        var evento = so.FindProperty("OnSkipCompleted");
        if (evento == null)
        {
            Debug.LogError("[Salto] No encuentro la propiedad OnSkipCompleted");
            return;
        }
        var persistentes = evento.FindPropertyRelative("m_PersistentCalls");
        var llamadas = persistentes != null ? persistentes.FindPropertyRelative("m_Calls") : null;
        if (llamadas == null)
        {
            Debug.Log("[Salto] No hay llamadas persistentes que quitar: no se toca nada.");
            return;
        }
        int antes = llamadas.arraySize;
        if (antes == 0)
        {
            Debug.Log("[Salto] No había ninguna llamada persistente: no se toca nada.");
            return;
        }
        llamadas.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Salto] Quitadas {antes} llamadas persistentes de OnSkipCompleted en {Prefab}.");
    }
}
#endif
