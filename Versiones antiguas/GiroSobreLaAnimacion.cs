using UnityEngine;

/// Un hueso que un sistema gira por código encima de la pose del Animator (mirada, postura de sentado,
/// abrazo…), multiplicando su rotación en LateUpdate.
///
/// Si el Animator no ha escrito la pose en este fotograma (fuera de cámara con «Cull Update Transforms»,
/// lejos con «Cull Completely», parado), el hueso conserva el giro que se le dio en el anterior y volver a
/// girarlo lo suma: la cabeza da la vuelta poco a poco y, cuando el personaje sale en el plano, está boca
/// abajo o mirando atrás (INC-1308). Antes de girar se llama a <see cref="PoseAnimada"/>: si el hueso sigue
/// exactamente como se dejó, se devuelve a la pose de la que se partió; después, <see cref="Dejar"/>.
/// Vale para cualquier esqueleto y cualquier sistema que gire huesos encima de la animación.
public struct GiroSobreLaAnimacion
{
    private Transform _hueso;
    private Quaternion _deLaAnimacion, _dejada;
    private bool _girado;

    public GiroSobreLaAnimacion(Transform hueso)
    {
        _hueso = hueso;
        _deLaAnimacion = _dejada = Quaternion.identity;
        _girado = false;
    }

    public Transform Hueso => _hueso;

    /// Deja el hueso en la pose de la animación de este fotograma (o en la del último que escribió el Animator).
    public void PoseAnimada()
    {
        if (_hueso == null) return;
        if (_girado && _hueso.localRotation == _dejada) _hueso.localRotation = _deLaAnimacion;
        _deLaAnimacion = _hueso.localRotation;
    }

    /// Apunta cómo queda el hueso tras girarlo este fotograma.
    public void Dejar()
    {
        if (_hueso == null) return;
        _dejada = _hueso.localRotation;
        _girado = true;
    }

    /// El sistema deja de girar el hueso: lo que quede ya no es suyo.
    public void Soltar() => _girado = false;
}
