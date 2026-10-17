using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Bootstrap ligero y dedicado para el nivel jugable de Escape del Sendero.
/// Registra a Will en PlayerService, configura las habilidades para plataformeo atlético
/// (salto, doble salto, sprint, magia) y DESACTIVA EXPLICITAMENTE el vuelo libre para que
/// el jugador corra, salte y use los lanzadores sin flotar como en modo debug.
/// </summary>
[DefaultExecutionOrder(-850)]
public sealed class EscapeSenderoBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject player;

    private void Awake()
    {
        if (player != null)
        {
            PlayerService.RegisterPlayer(player);
        }
    }

    private void Start()
    {
        if (player == null && PlayerService.Player != null)
            player = PlayerService.Player;

        if (player != null)
        {
            // 1. Desactivar estrictamente el componente de vuelo libre
            var flying = player.GetComponentInChildren<PlayerFlyingController>(true);
            if (flying != null)
            {
                flying.enabled = false;
            }

            // 2. Desactivar el flag de vuelo en el preset activo
            var preset = UnlockService.GetActivePreset();
            if (preset != null)
            {
                preset.abilities = new PlayerAbilities
                {
                    jump = true,
                    climb = true,
                    sprint = true,
                    magic = true,
                    shield = true,
                    fly = false,    // ¡NUNCA volar en este nivel!
                    swim = false
                };

                if (preset.maxMP < 100f) preset.maxMP = 100f;
                preset.currentMP = preset.maxMP;

                var presetService = player.GetComponentInChildren<PlayerPresetService>(true);
                if (presetService != null)
                {
                    presetService.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
                }
            }
        }

        // 3. UI limpia e inmersiva
        MinimapController.Ocultar("EscapeSendero");
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD(this);
    }
}
