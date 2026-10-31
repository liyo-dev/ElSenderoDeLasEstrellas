using UnityEngine;

/// El dueño de la cámara principal durante una secuencia. Activate() al empezar la cinemática y
/// Deactivate() al acabar, a través de CameraDirectorService, que es el protocolo de propiedad de
/// la cámara (congela vThirdPersonCamera mientras tanto). El guion horneado lleva la cámara con
/// Cinemachine: mientras dura, el driver le cede el control (CederElControl / DevolverElControl),
/// y antes de empezar coloca la cámara en su primer plano con Cut().
[DisallowMultipleComponent]
public class CinematicCameraDriver : MonoBehaviour
{
    private bool _active;

    /// Cuando otro sistema lleva la cámara (el guion horneado, que usa Cinemachine), el driver
    /// deja de escribir en ella.
    private bool _cedido;

    // Camera.main hace una búsqueda por tag cada vez que se consulta (CLAUDE.md § 2). Se vuelve a
    // resolver sola si la cámara se destruye al cambiar de escena.
    private Camera _cam;

    private Camera MainCamera
    {
        get
        {
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }
    }

    /// Congela vThirdPersonCamera y toma control de la cámara.
    /// Pasa por CameraDirectorService (en vez de tocar el flag directamente) para que, si el
    /// sistema que suelta la cámara justo antes todavía está en su ventana de gracia de
    /// liberación, este Activate() coalesque con ese handoff sin que vThirdPersonCamera llegue
    /// a meter un frame de gameplay en medio.
    public void Activate()
    {
        if (_active) return;
        _active = true;
        CameraDirectorService.Claim(this);
    }

    /// Libera la cámara y devuelve el control al juego (con la ventana de gracia de
    /// CameraDirectorService, no al instante).
    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        CameraDirectorService.Release(this);
    }

    /// Cede la cámara a otro sistema: el driver deja de escribir en ella hasta que se la
    /// devuelvan. Lo usa el guion horneado, que lleva la cámara con Cinemachine.
    public void CederElControl() => _cedido = true;

    /// Vuelve a tomar la cámara el driver, que sigue siendo su dueño.
    public void DevolverElControl() => _cedido = false;

    /// Corte instantáneo a una pose. No hace nada mientras otro sistema lleva la cámara.
    public void Cut(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        var cam = MainCamera;
        if (!cam || _cedido) return;
        cam.transform.SetPositionAndRotation(position, rotation);
        if (fieldOfView > 0f) cam.fieldOfView = fieldOfView;
    }

    void OnDestroy()
    {
        if (_active)
            CameraDirectorService.Release(this);
    }
}
