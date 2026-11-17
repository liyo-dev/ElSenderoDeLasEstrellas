using UnityEngine;

/// <summary>
/// Voltea un cartel del blockout para que su cara (+Z) mire siempre a la cámara de juego.
/// Los carteles tumbados en el suelo se leen en la vista de planta, pero desde el juego no:
/// este componente pone la versión que flota y se ve al jugar.
/// </summary>
[DisallowMultipleComponent]
public sealed class CartelQueMiraALaCamara : MonoBehaviour
{
    private Camera _camara;

    private void LateUpdate()
    {
        if (_camara == null)
        {
            _camara = Camera.main;
            if (_camara == null) return;
        }

        Vector3 desdeLaCamara = transform.position - _camara.transform.position;
        if (desdeLaCamara.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(desdeLaCamara, _camara.transform.up);
    }
}
