#if UNITY_EDITOR
using UnityEditor;

/// Temporal: sale de Play al recargar el dominio (para recuperar el buzón de órdenes).
[InitializeOnLoad]
internal static class TmpPararPlay
{
    static TmpPararPlay()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            UnityEngine.Debug.Log("[TmpPararPlay] saliendo de Play para recargar el dominio.");
        }
    }
}
#endif
