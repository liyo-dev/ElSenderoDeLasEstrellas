using UnityEngine;

/// <summary>
/// Bloqueador físico de zona gateado por una habilidad del jugador (ver AbilityKey / PlayerAbilities).
/// Pensado en origen para "no se puede entrar al agua hasta aprender a nadar", pero es genérico:
/// cualquier zona (agua, un saliente que requiere Climb, un hueco que requiere Fly, etc.) puede
/// usar este mismo componente.
///
/// Uso: colocar en un GameObject bajo BLOCKERS con un Collider NO-trigger sólido que actúe de muro
/// invisible en el borde de la zona restringida (p.ej. sellando la entrada al agua). Mientras la
/// habilidad indicada (requiredAbility) no esté desbloqueada, el collider permanece activo y
/// bloquea el paso; en cuanto se desbloquea —ya sea durante la partida (evento
/// UnlockService.OnAbilityUnlockedKey) o porque el arranque ya viene con ella activada (partida
/// guardada, o el checkbox correspondiente del Quick Test)— el collider se desactiva y deja pasar.
///
/// NO hace falta ningún ajuste aparte en NarrativeQuickTestWindow: sus checkboxes de habilidades ya
/// rellenan PlayerAbilities del preset temporal, y PlayerActionManager.ApplyAbilities() lo aplica
/// igual que en una partida normal (ver ApplyAbilities() → _allowSwim/_allowJump/_allowClimb/_allowFly).
/// Este bloqueador simplemente lee ese mismo estado a través de
/// PlayerActionManager.CanSwim()/CanClimb()/CanFly()/CanJump() — una sola fuente de verdad, nada
/// que duplicar ni sincronizar a mano entre el tool y el gameplay real.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AbilityGatedBlocker : MonoBehaviour
{
    [Tooltip("Habilidad que, una vez desbloqueada, retira este bloqueo.")]
    [SerializeField] private AbilityKey requiredAbility = AbilityKey.Swim;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private Collider _collider;
    private bool _subscribed;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = false; // debe bloquear físicamente, no solo detectar
        _collider.enabled = true;    // bloqueado por defecto hasta confirmar lo contrario
    }

    private void OnEnable()
    {
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
        GameBootService.OnProfileReady += HandleProfileReady;
        _subscribed = true;

        // Por si el profile ya estaba listo antes de que este objeto se activara (p.ej. una zona
        // que empieza inactiva y se activa más tarde).
        if (GameBootService.IsAvailable)
            StartCoroutine(WaitForPlayerAndApply());
    }

    private void OnDisable()
    {
        if (!_subscribed) return;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
        GameBootService.OnProfileReady -= HandleProfileReady;
        _subscribed = false;
    }

    private void HandleProfileReady()
    {
        StartCoroutine(WaitForPlayerAndApply());
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        if (key != requiredAbility) return;
        ApplyCurrentState();
    }

    /// <summary>
    /// Espera a que el player esté disponible (puede no estarlo aún si esta zona se activa en el
    /// primer frame de carga de escena, mismo patrón que NarrativeGraphStarter/SpawnManager) y
    /// aplica el estado real de la habilidad una vez se pueda consultar.
    /// </summary>
    private System.Collections.IEnumerator WaitForPlayerAndApply()
    {
        int attempts = 0;
        while (!PlayerService.TryGetPlayer(out _) && attempts < 100)
        {
            attempts++;
            yield return null;
        }
        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        bool unlocked = IsAbilityCurrentlyAllowed(requiredAbility);
        if (_collider != null)
            _collider.enabled = !unlocked;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logDebug)
            Debug.Log($"[AbilityGatedBlocker] '{name}' ({requiredAbility}): {(unlocked ? "desbloqueado, paso libre" : "bloqueando paso")}");
#endif
    }

    private static bool IsAbilityCurrentlyAllowed(AbilityKey key)
    {
        if (!PlayerService.TryGetPlayer(out var playerRoot) || playerRoot == null)
            return false;

        var actionManager = playerRoot.GetComponent<PlayerActionManager>()
            ?? playerRoot.GetComponentInChildren<PlayerActionManager>(true);
        if (actionManager == null)
            return false;

        return key switch
        {
            AbilityKey.Swim  => actionManager.CanSwim(),
            AbilityKey.Jump  => actionManager.CanJump(),
            AbilityKey.Climb => actionManager.CanClimb(),
            AbilityKey.Fly   => actionManager.CanFly(),
            // Magic/Sprint/Shield no bloquean paso físico por diseño actual — tratarlas como
            // "desbloqueadas" para este componente (no tendría sentido un muro gateado por magia).
            _ => true
        };
    }
}
