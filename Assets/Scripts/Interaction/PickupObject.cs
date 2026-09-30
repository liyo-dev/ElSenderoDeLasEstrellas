﻿using UnityEngine;

[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class PickupObject : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private ObjectType objectType = ObjectType.Caja;
    [SerializeField] private bool canBeDropped = true;

    private Interactable _interactable;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();

        // Lo que se lleva en brazos se mueve o se entrega: su estado va en la partida (INC-540).
        if (!TryGetComponent<ObjetoPersistente>(out _))
            gameObject.AddComponent<ObjetoPersistente>();
    }

    void Start()
    {
        if (_interactable == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError($"[PickupObject] Falta Interactable en {name}");
#endif
            return;
        }

        // Nos suscribimos una sola vez
        _interactable.OnInteract.AddListener(OnPickup);
    }

    // ¡OJO! Este método puede recibir un hijo golpeado por el raycast o el propio Player
    private void OnPickup(GameObject whoCalled)
    {
        // Intentar obtener del objeto que llamó, sino usar PlayerService
        var carry = whoCalled?.GetComponentInParent<PlayerCarrySystem>();
        
        if (carry == null)
        {
            // Fallback: buscar en el jugador usando PlayerService
            PlayerService.TryGetComponent<PlayerCarrySystem>(out carry);
        }

        if (carry == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[PickupObject] No se encuentra PlayerCarrySystem en {name}");
#endif
            return;
        }

        _interactable.SetHintVisible(false);
        _interactable.EnableInteraction(false);
        carry.PickupObject(gameObject);
    }

    public void OnDropped()
    {
        if (canBeDropped && _interactable != null)
            _interactable.EnableInteraction(true);
    }

    public ObjectType GetObjectType() => objectType;
    public bool CanBeDropped => canBeDropped;
}