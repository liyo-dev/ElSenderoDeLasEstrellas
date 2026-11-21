using UnityEngine;

/// Pareja de <see cref="GiroSobreLaAnimacion"/>: corre la última del LateUpdate y le apunta cómo quedan
/// los huesos al final del fotograma, ya con todos los giros por código. Ver INC-1308.
[DisallowMultipleComponent]
[DefaultExecutionOrder(32000)]
public sealed class GiroSobreLaAnimacionAlFinal : MonoBehaviour
{
    private GiroSobreLaAnimacion _giro;

    private void Awake() => _giro = GetComponent<GiroSobreLaAnimacion>();

    private void LateUpdate()
    {
        if (_giro != null) _giro.ApuntarFinal();
    }
}
