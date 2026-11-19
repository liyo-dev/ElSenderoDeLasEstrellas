#if UNITY_EDITOR
using UnityEngine;

/// Sonda de diagnóstico (solo Editor): la pose del cuello y la cabeza justo después de
/// CabezaQuietaAlHablar (orden -20). Ver SondaDeCabeza.
[DefaultExecutionOrder(-15)]
public sealed class SondaDeCabezaTrasQuieta : SondaDeCabeza { }
#endif
