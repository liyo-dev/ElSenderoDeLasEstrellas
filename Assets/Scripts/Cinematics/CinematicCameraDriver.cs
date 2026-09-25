using System.Collections;
using DG.Tweening;
using UnityEngine;

/// Controlador de cámara cinemático reutilizable.
///
/// Uso:
///   1. Añade este componente a cualquier GameObject de la escena.
///   2. Crea GameObjects vacíos como hijos o donde quieras los planos y asígnalos
///      como "shot points" en los scripts que orquesten la cinemática.
///   3. Llama Activate() al inicio de la cinemática y Deactivate() al final.
///
/// Internamente congela vThirdPersonCamera y mueve Camera.main directamente,
/// igual que hace SimpleCinematicDirector en modo forceDirectCameraControl.
[DisallowMultipleComponent]
public class CinematicCameraDriver : MonoBehaviour
{
    [Tooltip("Duración por defecto de los movimientos suaves cuando no se especifica.")]
    [SerializeField] private float defaultMoveDuration = 0.5f;
    [Tooltip("Ease por defecto para movimientos suaves.")]
    [SerializeField] private Ease  defaultMoveEase     = Ease.InOutSine;

    private Coroutine _followRoutine;

    // FIX (auditoría 17 sep 2026): el handle del movimiento suave en vuelo. Antes MoveTo() devolvía
    // la corrutina y nadie la guardaba, así que Deactivate() no podía pararla: un plano con
    // movimiento largo y 'esperar a que llegue' desmarcado seguía escribiendo en la cámara DESPUÉS
    // de que la secuencia hubiera terminado, peleándose con la cámara de juego que ya había
    // recuperado el control.
    private Coroutine _moveRoutine;

    private bool      _active;

    // Camera.main hace una búsqueda por tag cada vez que se consulta. Los planos calculados en
    // modo 'live' aplican una pose por frame, así que aquí se cachea (CLAUDE.md § 2). Se vuelve a
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

    // ── API pública ───────────────────────────────────────────────────────────

    /// Dónde está la cámara ahora mismo.
    ///
    /// Lo necesita ShotBeat para decidir si el movimiento hasta el plano siguiente atravesaría el
    /// escenario (ver ShotComposer.IsPathClear): un travelling entre dos puntos lejanos del pueblo
    /// pasa por dentro de las casas, y eso no se puede saber sin conocer el punto de partida.
    public Vector3 CurrentPosition
        => MainCamera != null ? MainCamera.transform.position : transform.position;

    public Quaternion CurrentRotation
        => MainCamera != null ? MainCamera.transform.rotation : transform.rotation;


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
        StopFollow();
        StopMove();
        CameraDirectorService.Release(this);
    }

    /// Corte instantáneo: mueve la cámara al shot point sin transición.
    public void Cut(Transform shot)
    {
        if (!shot) return;
        StopFollow();
        StopMove();
        var cam = MainCamera;
        if (!cam) return;
        cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
        if (shot.TryGetComponent(out Camera shotCam))
            cam.fieldOfView = shotCam.fieldOfView;
    }

    /// Pan suave hacia un shot point en tiempo real (funciona en slow motion).
    /// Devuelve la coroutine para poder hacer yield si se necesita esperar.
    public Coroutine MoveTo(Transform shot, float duration = -1f, Ease ease = Ease.Unset)
    {
        StopFollow();
        StopMove();
        _moveRoutine = StartCoroutine(Co_MoveTo(shot,
            duration < 0f ? defaultMoveDuration : duration,
            ease == Ease.Unset ? defaultMoveEase : ease));
        return _moveRoutine;
    }

    /// Sigue a un Transform en tiempo real, aplicando un offset fijo en espacio mundo.
    /// Útil para seguir proyectiles u objetos en movimiento.
    public void StartFollowing(Transform target, Vector3 worldOffset = default, bool lookAt = false)
    {
        StopFollow();
        StopMove();
        _followRoutine = StartCoroutine(Co_Follow(target, worldOffset, lookAt));
    }

    public void StopFollowing() => StopFollow();

    // ── Planos calculados ─────────────────────────────────────────────────────
    //
    // Las tres sobrecargas de abajo hacen lo mismo que Cut/MoveTo pero recibiendo una pose suelta
    // en vez de un Transform de la escena. Existen porque un plano ya no tiene por qué estar
    // colocado a mano: ShotComposer lo calcula a partir de dónde están los actores, y el
    // resultado son unas coordenadas que no pertenecen a ningún GameObject.
    //
    // Nada de lo de arriba cambia: las secuencias que usan shot points colocados siguen igual.

    /// Corte instantáneo a una pose calculada.
    public void Cut(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        StopFollow();
        StopMove();
        SetPose(position, rotation, fieldOfView);
    }

    /// Aplica una pose sin tocar nada más. Es lo que usan los planos que se recalculan cada frame
    /// (un personaje corriendo, un proyectil en vuelo): a diferencia de Cut(), no cancela el
    /// seguimiento ni los tweens, porque quien la llama ya sabe lo que está haciendo.
    public void SetPose(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        var cam = MainCamera;
        if (!cam) return;
        cam.transform.SetPositionAndRotation(position, rotation);
        if (fieldOfView > 0f) cam.fieldOfView = fieldOfView;
    }

    /// Movimiento suave hasta una pose calculada. Igual que MoveTo(Transform) pero con destino
    /// fijo en coordenadas.
    ///
    /// `arrancaLanzado` (INC-395): el movimiento normal arranca y frena despacio (smoothstep), así
    /// que durante el primer segundo de un travelling largo la cámara casi no se mueve. Para una
    /// apertura que «ya viene bajando» eso se lee como un plano quieto. Marcado, la cámara sale
    /// del primer fotograma ya en marcha y solo frena al llegar.
    ///
    /// `porEncima` (INC-397): si se da, el recorrido no es recto sino una curva (Bézier cuadrática)
    /// que tira hacia ese punto — para entrar en un plano desde arriba cuando la recta atraviesa
    /// el decorado.
    public Coroutine MoveTo(Vector3 position, Quaternion rotation, float fieldOfView,
        float duration = -1f, bool arrancaLanzado = false, Vector3? porEncima = null)
    {
        StopFollow();
        StopMove();
        _moveRoutine = StartCoroutine(Co_MoveToPose(position, rotation, fieldOfView,
            duration < 0f ? defaultMoveDuration : duration, arrancaLanzado, porEncima));
        return _moveRoutine;
    }

    private IEnumerator Co_MoveToPose(Vector3 targetPos, Quaternion targetRot, float targetFov,
        float duration, bool arrancaLanzado = false, Vector3? porEncima = null)
    {
        var cam = MainCamera;
        if (!cam) yield break;

        Vector3    startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        float      startFov = cam.fieldOfView;
        if (targetFov <= 0f) targetFov = startFov;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Tiempo sin escalar, igual que Co_MoveTo: hay secuencias enteras en cámara lenta
            // (el Despertar de la Estrella) y un movimiento de cámara que se ralentice con ellas
            // tarda cinco veces más de lo escrito.
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float st = arrancaLanzado
                ? 1f - (1f - t) * (1f - t)   // sale en marcha y frena al llegar
                : t * t * (3f - 2f * t);
            if (!cam) yield break;
            if (porEncima.HasValue)
            {
                float u = 1f - st;
                cam.transform.position = u * u * startPos + 2f * u * st * porEncima.Value + st * st * targetPos;
            }
            else
            {
                cam.transform.position = Vector3.Lerp(startPos, targetPos, st);
            }
            cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, st);
            cam.fieldOfView        = Mathf.Lerp(startFov, targetFov, st);
            yield return null;
        }

        if (cam)
        {
            cam.transform.SetPositionAndRotation(targetPos, targetRot);
            cam.fieldOfView = targetFov;
        }
    }

    // ── Coroutines internas ───────────────────────────────────────────────────

    private IEnumerator Co_MoveTo(Transform shot, float duration, Ease ease)
    {
        var cam = MainCamera;
        if (!shot || !cam) yield break;

        Vector3    startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        float      startFov = cam.fieldOfView;
        float      targetFov = shot.TryGetComponent(out Camera shotCam) ? shotCam.fieldOfView : startFov;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float st = t * t * (3f - 2f * t);
            if (!shot || !cam) yield break;
            cam.transform.position = Vector3.Lerp(startPos, shot.position, st);
            cam.transform.rotation = Quaternion.Slerp(startRot, shot.rotation, st);
            cam.fieldOfView        = Mathf.Lerp(startFov, targetFov, st);
            yield return null;
        }

        if (shot && cam)
        {
            cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
            cam.fieldOfView = targetFov;
        }
    }

    private IEnumerator Co_Follow(Transform target, Vector3 offset, bool lookAt)
    {
        var cam = MainCamera;
        while (target && cam)
        {
            Vector3 desiredPos = target.position + offset;
            cam.transform.position = desiredPos;
            if (lookAt) cam.transform.LookAt(target.position);
            yield return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StopFollow()
    {
        if (_followRoutine != null)
        {
            StopCoroutine(_followRoutine);
            _followRoutine = null;
        }
    }

    /// Para el movimiento suave que hubiera en vuelo.
    private void StopMove()
    {
        if (_moveRoutine == null) return;
        StopCoroutine(_moveRoutine);
        _moveRoutine = null;
    }

    void OnDestroy()
    {
        StopMove();
        if (_active)
            CameraDirectorService.Release(this);
    }
}
