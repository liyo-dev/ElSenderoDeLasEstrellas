using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class ActiveStateDebugger : MonoBehaviour
{
    void OnEnable()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[ActiveStateDebugger] OnEnable on '{gameObject.name}' (activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy})");
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[ActiveStateDebugger] OnDisable on '{gameObject.name}' (activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy})\nStack:\n" + new StackTrace(true).ToString());
#endif
    }
}
