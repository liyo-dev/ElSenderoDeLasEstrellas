using System;
using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Detector de entrega de objetos por proximidad, de uso exclusivo de
    /// <see cref="WaitItemDeliveryNode"/> (grafo narrativo).
    ///
    /// A diferencia del sistema legacy (<see cref="NPCItemDetector"/>, atado a
    /// <c>NPCQuestConfig.enableItemDetection</c> y configurado a mano en el Inspector de cada
    /// NPC), este componente no se asigna nunca en un prefab: el propio nodo del grafo lo añade
    /// dinámicamente al NPC en tiempo de ejecución vía <see cref="Setup"/>, con los datos de la
    /// misión concreta, y lo destruye al salir del nodo (ver <c>WaitItemDeliveryNode.Exit</c>).
    /// Así "cualquier misión del mismo tipo" reutiliza la misma pieza de código sin tener que
    /// tocar ningún prefab de NPC ni volver a montar el sistema de detección para cada uno.
    ///
    /// Igual que el sistema legacy, la entrega se detecta cuando el jugador SUELTA el objeto
    /// (<see cref="PlayerCarrySystem.OnObjectDropped"/>) estando dentro del radio del NPC — no
    /// por el mero hecho de acercarse llevándolo en brazos.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcItemDeliveryDetector : MonoBehaviour
    {
        private ObjectType _expectedItemType;
        private Action<GameObject> _onDelivered;

        private bool _playerInRange;
        private bool _delivered;
        private PlayerCarrySystem _carrySystem;
        private bool _subscribed;

        /// <summary>
        /// Prepara el detector para una entrega concreta. Debe llamarse justo tras
        /// <c>gameObject.AddComponent&lt;NpcItemDeliveryDetector&gt;()</c>.
        /// </summary>
        public void Setup(float detectionRadius, ObjectType expectedItemType, Action<GameObject> onDelivered)
        {
            _expectedItemType = expectedItemType;
            _onDelivered = onDelivered;

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.1f, detectionRadius);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_delivered || !other.CompareTag("Player")) return;

            _playerInRange = true;

            if (!_subscribed && PlayerService.TryGetComponent(out PlayerCarrySystem carry))
            {
                _carrySystem = carry;
                _carrySystem.OnObjectDropped += OnPlayerDroppedObject;
                _subscribed = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = false;
            Unsubscribe();
        }

        private void OnPlayerDroppedObject(GameObject droppedObject)
        {
            if (_delivered || !_playerInRange || droppedObject == null) return;

            var pickup = droppedObject.GetComponent<PickupObject>();
            if (pickup == null || pickup.GetObjectType() != _expectedItemType) return;

            _delivered = true;
            _onDelivered?.Invoke(droppedObject);
        }

        private void Unsubscribe()
        {
            if (_subscribed && _carrySystem != null)
                _carrySystem.OnObjectDropped -= OnPlayerDroppedObject;

            _subscribed = false;
            _carrySystem = null;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
