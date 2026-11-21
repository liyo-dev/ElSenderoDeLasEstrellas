#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Arreglo de una sola vez (INC-1398): agranda en MainWorld (abierta) la caja «Agua del puerto
/// (para nadar)» a 180 × 180 m, como la deja ya VestidoDelMundo, sin tener que volver a vestir el
/// mundo. Se ejecuta una vez y este archivo sale a «Versiones antiguas/».
public static class AgrandarAguaDelPuerto
{
    [MenuItem("El Sendero/Pruebas/Arreglo de una vez: agua del puerto para nadar en toda la aldea (INC-1398)")]
    public static void Ejecutar()
    {
        var go = GameObject.Find("Agua del puerto (para nadar)");
        var caja = go != null ? go.GetComponent<BoxCollider>() : null;
        if (caja == null) { Debug.LogError("[INC-1398] Abre MainWorld: no encuentro «Agua del puerto (para nadar)»."); return; }
        Undo.RecordObject(caja, "Agrandar el agua del puerto");
        float hondo = caja.size.y;
        float fondo = caja.center.y - hondo * 0.5f - 4f;   // margen: más fondo bajo el farol
        float superficie = caja.center.y + hondo * 0.5f;
        caja.size = new Vector3(180f, superficie - fondo, 180f);
        caja.center = new Vector3(caja.center.x, (superficie + fondo) * 0.5f, caja.center.z);
        EditorUtility.SetDirty(caja);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Debug.Log($"[INC-1398] Agua del puerto: {caja.size} en {caja.center}. Escena guardada.");
    }
}
#endif
