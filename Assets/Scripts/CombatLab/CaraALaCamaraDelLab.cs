using UnityEngine;

/// <summary>
/// Gira un rótulo del LAB para que se lea desde la cámara (los de encima de los luchadores).
/// Solo para escenas de prueba.
/// </summary>
public sealed class CaraALaCamaraDelLab : MonoBehaviour
{
    private Camera _camara;
    private Transform _t;

    private void Awake() => _t = transform;

    private void LateUpdate()
    {
        if (_camara == null || !_camara.isActiveAndEnabled)
        {
            _camara = Camera.main;
            if (_camara == null) return;
        }
        Vector3 hacia = _t.position - _camara.transform.position;
        hacia.y = 0f;
        if (hacia.sqrMagnitude > 0.0001f) _t.rotation = Quaternion.LookRotation(hacia);
    }
}
