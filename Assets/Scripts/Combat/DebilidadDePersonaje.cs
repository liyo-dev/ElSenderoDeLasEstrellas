using Game.NPC;
using UnityEngine;

/// Algo que recibe más daño de un personaje concreto: cuando golpea el jugador llevando a ese
/// personaje, o ese personaje como aliado. Ejemplo: las anclas del Mago Oscuro se rompen antes
/// con la magia del pacto de Liam (GDD: «Garra / Sello del Pacto… anclajes del Mago Oscuro»).
/// Va junto al Damageable. Ver INC-509.
[DisallowMultipleComponent]
public sealed class DebilidadDePersonaje : MonoBehaviour, IFiltroDeDano
{
    [SerializeField] private PartyControlManager.CharacterSlot personaje = PartyControlManager.CharacterSlot.Liam;
    [Tooltip("Nombre del aliado en el grupo (NPCPartyConfig.displayName) para cuando golpea con la IA.")]
    [SerializeField] private string nombreAliado = "Liam";
    [SerializeField, Min(1f)] private float multiplicador = 2.5f;

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (instigador == null) return cantidad;

        bool jugadorConEse = PlayerService.Player != null
                             && instigador.transform.root == PlayerService.Player.transform.root
                             && PartyControlManager.Instance != null
                             && PartyControlManager.Instance.ActiveSlot == personaje;
        var aliado = instigador.GetComponentInParent<NPCPartyMember>();
        bool aliadoEse = aliado != null && aliado.DisplayName == nombreAliado;

        return jugadorConEse || aliadoEse ? cantidad * multiplicador : cantidad;
    }
}
