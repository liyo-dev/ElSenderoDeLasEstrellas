using UnityEngine;

/// <summary>
/// Regla de daño del jugador que aplica la defensa de la B (INC-493): dentro de la ventana de
/// contraataque el golpe se anula; con el escudo levantado pasa solo una parte. Lo añade
/// PlayerShieldController en el objeto de PlayerHealthSystem.
/// </summary>
[DisallowMultipleComponent]
public class FiltroDeDefensa : MonoBehaviour, IFiltroDeDano
{
    public PlayerShieldController Owner { get; set; }

    public float Filtrar(float cantidad, GameObject instigador)
        => Owner != null && Owner.isActiveAndEnabled ? Owner.FilterIncomingDamage(cantidad) : cantidad;
}
