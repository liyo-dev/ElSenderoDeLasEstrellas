using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sendero.Core.Feedback;
using UnityEngine.AI;

/// <summary>
/// Componente para plataformas que pueden elevarse o hundirse.
/// Usado por PressurePlate u otros mecanismos de puzzle.
///
/// Se mueve por fÃ­sica: un Rigidbody cinemÃ¡tico (lo aÃ±ade si falta) que avanza en el paso de fÃ­sica
/// con MovePosition. AsÃ­ el jugador, que tambiÃ©n es un Rigidbody, sube empujado por
/// la plataforma en cada paso en vez de recolocarse a tirones contra un collider que se movÃ­a por
/// Transform en Update. Ver INC-524.
/// </summary>
public class PlatformElevator : MonoBehaviour
{
    [Header("ConfiguraciÃ³n de Movimiento")]
    [Tooltip("Altura a la que se eleva la plataforma (relativa a la posiciÃ³n inicial)")]
    [SerializeField] private float raiseHeight = 3f;
    
    [Tooltip("Velocidad de movimiento de la plataforma")]
    [SerializeField] private float moveSpeed = 2f;
    
    [Tooltip("Curva de animaciÃ³n para suavizar el movimiento")]
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Encadenamiento")]
    [Tooltip("Delay antes de que esta plataforma comience a moverse (Ãºtil para efectos en cascada)")]
    [SerializeField] private float delayBeforeMoving;
    
    [Tooltip("Plataformas que se activarÃ¡n despuÃ©s de que esta termine de moverse")]
    [SerializeField] private PlatformElevator[] chainedPlatforms;
    
    [Header("Feedback")]
    [Tooltip("Clave de SFX al comenzar a moverse")]
    [SerializeField] private string movementStartSfxKey = "Platform_Move_Start";
    
    [Tooltip("Clave de SFX al terminar de moverse")]
    [SerializeField] private string movementStopSfxKey = "Platform_Move_Stop";
    
    [Tooltip("VFX que acompaÃ±a a la plataforma mientras se mueve (del pool de VFX)")]
    [SerializeField] private GameObject movementVFX;
    
    [Tooltip("Transform donde se instancia el VFX (si es null, usa la posiciÃ³n de la plataforma)")]
    [SerializeField] private Transform vfxSpawnPoint;
    
    [Tooltip("Segundos que sigue el VFX tras parar la plataforma, para que termine su animaciÃ³n, antes de volver al pool")]
    [SerializeField] private float vfxLifetime = 3f;
    
    [Header("Estado")]
    [SerializeField] private bool isRaised;
    
    private Vector3 _originalPosition;
    private Vector3 _raisedPosition;
    private Rigidbody _rb;
    private bool _isMoving;
    private Coroutine _moveCoroutine;
    private Transform _currentVFX; // VFX del movimiento en curso
    private ulong _usoDelVFX;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;

        // Posiciones en Awake (no en Start): una restauraciÃ³n de partida (EstadoPersistente,
        // ActivationCounter) puede llamar a TeleportToRaised antes del Start de este objeto.
        _originalPosition = transform.position;
        _raisedPosition = _originalPosition + Vector3.up * raiseHeight;
    }

    private void Start()
    {
        // La misma interpolaciÃ³n que el jugador: si solo uno de los dos interpolara, al subir juntos
        // se verÃ­an desfasados unos centÃ­metros que cambian cada fotograma.
        var jugador = PlayerService.Player;
        var rbJugador = jugador != null ? jugador.GetComponent<Rigidbody>() : null;
        _rb.interpolation = rbJugador != null ? rbJugador.interpolation : RigidbodyInterpolation.Interpolate;

        // Sincronizar el flag isRaised con la posiciÃ³n real
        // Si estÃ¡ marcado en Inspector pero no estÃ¡ en posiciÃ³n elevada, teletransportar
        if (isRaised && (transform.position - _raisedPosition).sqrMagnitude > 0.0001f)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlatformElevator] {name} estÃ¡ marcado como 'Is Raised', teletransportando a posiciÃ³n elevada");
#endif
            Colocar(_raisedPosition);
            _originalPosition = _raisedPosition - Vector3.up * raiseHeight; // Recalcular posiciÃ³n original
        }
        
        // Debug.Log($"[PlatformElevator] {name} - Inicializado. Original: {_originalPosition}, Raised: {_raisedPosition}, Current: {transform.position}, IsRaised: {isRaised}");
    }

    /// <summary>
    /// Eleva la plataforma
    /// </summary>
    /// <param name="isReversion">Si es true, es una reversiÃ³n (sin VFX, solo shake)</param>
    public void Raise(bool isReversion = false)
    {
        // Si ya estÃ¡ en movimiento hacia arriba, no hacer nada
        if (_isMoving && isRaised) return;
        
        // Si ya estÃ¡ en la posiciÃ³n elevada (tolerancia de 0.1), no mover
        if (Vector3.Distance(transform.position, _raisedPosition) < 0.1f)
        {
            isRaised = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlatformElevator] {name} ya estÃ¡ en posiciÃ³n elevada");
#endif
            return;
        }
        
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            SoltarLoQueLleva();
        }
        
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PlatformElevator] {name} - Elevando a {_raisedPosition} (desde {transform.position})");
#endif
        _moveCoroutine = StartCoroutine(MovePlatform(_raisedPosition, true, isReversion));
    }

    /// <summary>
    /// Hunde la plataforma (vuelve a su posiciÃ³n original)
    /// </summary>
    /// <param name="isReversion">Si es true, es una reversiÃ³n (sin VFX, solo shake)</param>
    public void Lower(bool isReversion = false)
    {
        // Si ya estÃ¡ en movimiento hacia abajo, no hacer nada
        if (_isMoving && !isRaised) return;
        
        // Si ya estÃ¡ en la posiciÃ³n baja (tolerancia de 0.1), no mover
        if (Vector3.Distance(transform.position, _originalPosition) < 0.1f)
        {
            isRaised = false;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[PlatformElevator] {name} ya estÃ¡ en posiciÃ³n original");
#endif
            return;
        }
        
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            SoltarLoQueLleva();
        }
        
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PlatformElevator] {name} - Bajando a {_originalPosition} (desde {transform.position})");
#endif
        _moveCoroutine = StartCoroutine(MovePlatform(_originalPosition, false, isReversion));
    }

    /// <summary>
    /// Corrutina que mueve la plataforma suavemente
    /// </summary>
    private IEnumerator MovePlatform(Vector3 targetPosition, bool raising, bool isReversion = false)
    {
        // Delay antes de moverse
        if (delayBeforeMoving > 0f)
        {
            yield return new WaitForSeconds(delayBeforeMoving);
        }
        
        _isMoving = true;
        Vector3 startPosition = _rb.position;
        float distance = Vector3.Distance(startPosition, targetPosition);
        float duration = distance / moveSpeed;
        float elapsed = 0f;
        
        // Feedback al comenzar (sin VFX si es reversiÃ³n)
        PlayMovementStartFeedback(duration, isReversion);
        
        // Mover la plataforma en el paso de fÃ­sica (lo que va encima la acompaÃ±a sin tirones)
        var paso = new WaitForFixedUpdate();
        Vector3 ultimo = _rb.position;
        RecogerLoQueLleva();
        while (elapsed < duration)
        {
            yield return paso;
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 destino = Vector3.Lerp(startPosition, targetPosition, movementCurve.Evaluate(t));
            _rb.MovePosition(destino);
            LlevarLoQueEsteEncima(destino - ultimo);
            ultimo = destino;
        }
        
        // Esperar a que la fÃ­sica aplique el Ãºltimo paso antes de dar el movimiento por acabado
        yield return paso;
        SoltarLoQueLleva();
        isRaised = raising;
        _isMoving = false;
        
        // Feedback al terminar
        PlayMovementStopFeedback(isReversion);
        
        // Activar plataformas encadenadas
        ActivateChainedPlatforms(raising, isReversion);
        
        // Callback
        OnMovementComplete(raising);
    }

    /// <summary>
    /// Activa las plataformas encadenadas
    /// </summary>
    private void ActivateChainedPlatforms(bool raising, bool isReversion = false)
    {
        if (chainedPlatforms == null || chainedPlatforms.Length == 0) return;
        
        foreach (var platform in chainedPlatforms)
        {
            if (platform != null)
            {
                if (raising)
                {
                    platform.Raise(isReversion);
                }
                else
                {
                    platform.Lower(isReversion);
                }
            }
        }
    }

    /// <summary>
    /// Reproduce feedback al comenzar a moverse
    /// </summary>
    private void PlayMovementStartFeedback(float duracion, bool isReversion = false)
    {
        // SFX (siempre, reversiÃ³n o no)
        if (!string.IsNullOrEmpty(movementStartSfxKey))
        {
            AudioService.Instance?.PlaySFX(movementStartSfxKey, worldPosition: transform.position);
        }
        
        // Camera shake - SOLO en reversiÃ³n (al bajar despuÃ©s de quitar objeto)
        // Al subir, el shake ya se hace en el PressurePlate.Activate()
        if (isReversion)
        {
            FeedbackService.CameraShake(0.2f, 0.3f);
        }
        
        // VFX - Solo si NO es reversiÃ³n. Va colgado de la plataforma y vuelve al pool cuando acaba
        // el movimiento mÃ¡s vfxLifetime; si un movimiento nuevo corta al anterior, se recoge ya.
        if (!isReversion && movementVFX != null && VfxPoolService.Instance != null)
        {
            RecogerVFX();

            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            _currentVFX = VfxPoolService.Instance.Play(movementVFX, spawnPos, Quaternion.identity, duracion + vfxLifetime, transform);
            _usoDelVFX = VfxPoolService.Instance.ObtenerUso(_currentVFX);
        }
    }

    private void RecogerVFX()
    {
        if (_currentVFX != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Recoger(_currentVFX, _usoDelVFX);
        _currentVFX = null;
    }

    /// <summary>
    /// Reproduce feedback al terminar de moverse
    /// </summary>
    private void PlayMovementStopFeedback(bool isReversion = false)
    {
        // SFX
        if (!string.IsNullOrEmpty(movementStopSfxKey))
        {
            AudioService.Instance?.PlaySFX(movementStopSfxKey, worldPosition: transform.position);
        }
        
        // El VFX sigue vfxLifetime mÃ¡s (ya programado en el pool) para que termine su animaciÃ³n.
        _currentVFX = null;
    }

    /// <summary>
    /// Callback que se llama al completar el movimiento
    /// </summary>
    protected virtual void OnMovementComplete(bool wasRaised)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[PlatformElevator] {name} completÃ³ movimiento. Elevada: {wasRaised}");
#endif
    }

    /// <summary>
    /// Teletransporta la plataforma a la posiciÃ³n elevada sin animaciÃ³n
    /// </summary>
    public void TeleportToRaised()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            SoltarLoQueLleva();
        }
        
        Colocar(_raisedPosition);
        isRaised = true;
        _isMoving = false;
    }

    /// <summary>
    /// Teletransporta la plataforma a la posiciÃ³n original sin animaciÃ³n
    /// </summary>
    public void TeleportToLowered()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            SoltarLoQueLleva();
        }
        
        Colocar(_originalPosition);
        isRaised = false;
        _isMoving = false;
    }

    /// Salto sin animaciÃ³n: mueve a la vez el Transform y el Rigidbody para que la fÃ­sica no lo
    /// interpole desde donde estaba.
    private void Colocar(Vector3 posicion)
    {
        transform.position = posicion;
        if (_rb != null) _rb.position = posicion;
    }

    /// <summary>
    /// Actualiza la altura de elevaciÃ³n (Ãºtil para ajustar en runtime)
    /// </summary>
    public void SetRaiseHeight(float height)
    {
        raiseHeight = height;
        _raisedPosition = _originalPosition + Vector3.up * raiseHeight;
    }

    /// <summary>
    /// Eleva sin VFX (para revertir una bajada previa). Usar en onDeactivated de PressurePlate.
    /// </summary>
    public void RaiseRevert() => Raise(isReversion: true);

    /// <summary>
    /// Baja sin VFX (para revertir una subida previa). Usar en onDeactivated de PressurePlate.
    /// </summary>
    public void LowerRevert() => Lower(isReversion: true);

    public bool IsMoving => _isMoving;
    public bool IsRaised => isRaised;

    // â”€â”€ Lo que va encima â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Los compaÃ±eros que lleva la IA van con el cuerpo cinemÃ¡tico y su NavMeshAgent: la fÃ­sica no
    // los empuja, asÃ­ que la plataforma los traslada ella. El jugador lo empuja la fÃ­sica sola.

    private readonly List<Transform> _llevados = new List<Transform>();
    private readonly List<NavMeshAgent> _agentesParados = new List<NavMeshAgent>();

    private void RecogerLoQueLleva()
    {
        SoltarLoQueLleva();
        int cuantos = DanoEnZona.CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < cuantos; i++)
        {
            if (cuerpos[i].EsElJugador) continue;
            var cuerpo = cuerpos[i].Transform;
            if (cuerpo == null || !Encima(cuerpo)) continue;

            _llevados.Add(cuerpo);
            var agente = cuerpo.GetComponent<NavMeshAgent>();
            if (agente == null || !agente.enabled) continue;
            agente.enabled = false;
            _agentesParados.Add(agente);
        }
    }

    private void LlevarLoQueEsteEncima(Vector3 delta)
    {
        if (_llevados.Count == 0 || delta.sqrMagnitude < 0.0000001f) return;
        for (int i = 0; i < _llevados.Count; i++)
            if (_llevados[i] != null) _llevados[i].position += delta;
    }

    private void SoltarLoQueLleva()
    {
        for (int i = 0; i < _agentesParados.Count; i++)
            if (_agentesParados[i] != null) _agentesParados[i].enabled = true;
        _agentesParados.Clear();
        _llevados.Clear();
    }

    /// <summary>Â¿Ese cuerpo estÃ¡ de pie sobre la plataforma? (escala del cubo aparte).</summary>
    private bool Encima(Transform cuerpo)
    {
        Vector3 local = transform.InverseTransformPoint(cuerpo.position);
        if (Mathf.Abs(local.x) > 0.55f || Mathf.Abs(local.z) > 0.55f) return false;
        float techo = transform.position.y + 0.5f * transform.localScale.y;
        return cuerpo.position.y >= techo - 0.4f && cuerpo.position.y <= techo + 2.5f;
    }

    private void OnDisable() => SoltarLoQueLleva();

    /// <summary>Sube la plataforma (para eventos sin parÃ¡metros, p. ej. un interactuable).</summary>
    public void MoveUp() => Raise();

    /// <summary>Baja la plataforma (para eventos sin parÃ¡metros).</summary>
    public void MoveDown() => Lower();

    /// <summary>Si estÃ¡ arriba (o subiendo) la baja; si no, la sube.</summary>
    public void Toggle()
    {
        if (isRaised) Lower();
        else Raise();
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Calcular posiciones
        Vector3 originalPos = Application.isPlaying ? _originalPosition : transform.position;
        Vector3 raisedPos = originalPos + Vector3.up * raiseHeight;
        
        // Dibujar posiciÃ³n original
        Gizmos.color = Color.yellow;
        DrawPlatformGizmo(originalPos);
        
        // Dibujar posiciÃ³n elevada
        Gizmos.color = Color.green;
        DrawPlatformGizmo(raisedPos);
        
        // Dibujar lÃ­nea de recorrido
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(originalPos, raisedPos);
        
        // Dibujar flechas para indicar direcciÃ³n
        DrawArrow(originalPos, Vector3.up, raiseHeight);
        
        // Dibujar lÃ­neas a plataformas encadenadas
        if (chainedPlatforms != null && chainedPlatforms.Length > 0)
        {
            Gizmos.color = Color.magenta;
            foreach (var platform in chainedPlatforms)
            {
                if (platform != null)
                {
                    Gizmos.DrawLine(transform.position, platform.transform.position);
                }
            }
        }
    }

    private void DrawPlatformGizmo(Vector3 position)
    {
        Gizmos.DrawWireCube(position, Vector3.one * 0.5f);
    }

    private void DrawArrow(Vector3 origin, Vector3 direction, float length)
    {
        Vector3 end = origin + direction * length;
        Gizmos.DrawLine(origin, end);
        
        // Puntas de la flecha
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Gizmos.DrawLine(end, end + right * 0.3f);
        Gizmos.DrawLine(end, end + left * 0.3f);
    }
    #endif
}

