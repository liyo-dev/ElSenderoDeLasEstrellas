using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Rayitas de velocidad alrededor del personaje cuando gira deprisa hacia el objetivo al atacar o
/// defender (CommitToAction). Solo en giros grandes, para que no salgan en cada disparo.
/// Ver INC-486.
/// </summary>
[DisallowMultipleComponent]
public class TurnStreakFeedback : MonoBehaviour
{
    [SerializeField] private vThirdPersonController controller;

    [Tooltip("Efecto de las rayitas (un solo uso; sale por VfxPoolService).")]
    [SerializeField] private GameObject streakPrefab;

    [Tooltip("Grados mínimos de giro para mostrar las rayitas.")]
    [SerializeField, Range(0f, 180f)] private float minAngle = 90f;

    [Tooltip("Altura (m) sobre los pies a la que salen.")]
    [SerializeField] private float heightOffset = 1.1f;

    [Tooltip("Segundos que vive el efecto antes de volver al pool.")]
    [SerializeField, Min(0.1f)] private float lifetime = 0.5f;

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
    }

    void OnEnable()
    {
        if (controller) controller.OnCommitTurnStarted += HandleTurn;
    }

    void OnDisable()
    {
        if (controller) controller.OnCommitTurnStarted -= HandleTurn;
    }

    private void HandleTurn(Vector3 direction, float angle)
    {
        if (angle < minAngle || streakPrefab == null || VfxPoolService.Instance == null) return;
        Vector3 position = controller.transform.position + Vector3.up * heightOffset;
        VfxPoolService.Instance.Play(streakPrefab, position, Quaternion.LookRotation(direction, Vector3.up), lifetime);
    }
}
