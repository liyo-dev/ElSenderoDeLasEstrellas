#if UNITY_EDITOR
using UnityEngine;

/// Sonda de diagnóstico (solo Editor): la pose del cuello y la cabeza al final del LateUpdate, con
/// todo lo que la mueve por código ya aplicado (mirada, sentado, gestos…). Ver SondaDeCabeza.
[DefaultExecutionOrder(32000)]
public sealed class SondaDeCabezaFinal : SondaDeCabeza { }
#endif
