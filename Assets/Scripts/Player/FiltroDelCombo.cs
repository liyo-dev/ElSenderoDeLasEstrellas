using UnityEngine;

/// <summary>
/// Anula el daño del jugador mientras el círculo del combo está abierto.
/// </summary>
[DisallowMultipleComponent]
public class FiltroDelCombo : MonoBehaviour, IFiltroDeDano
{
    public ComboCastController Owner { get; set; }

    public float Filtrar(float cantidad, GameObject instigador)
        => Owner != null && Owner.isActiveAndEnabled && Owner.IsOpen ? 0f : cantidad;
}
