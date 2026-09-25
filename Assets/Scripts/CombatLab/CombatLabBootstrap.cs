using System.Collections;
using UnityEngine;

/// <summary>
/// Conecta el jugador de prueba a los servicios persistentes y aplica el preset activo sin
/// ejecutar WorldBootstrap (que también poblaría la escena con los NPCs de mundo).
/// </summary>
[DefaultExecutionOrder(-850)]
public sealed class CombatLabBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject player;

    private void Awake()
    {
        if (player != null)
        {
            PlayerService.RegisterPlayer(player);
            ConservarEscuchaDelJugador();
        }
    }

    private void ConservarEscuchaDelJugador()
    {
        var escuchaPrincipal = player.GetComponentInChildren<AudioListener>(true);
        if (escuchaPrincipal == null) return;

        var escuchas = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < escuchas.Length; i++)
        {
            var escucha = escuchas[i];
            if (escucha != null && escucha != escuchaPrincipal && escucha.enabled)
                escucha.enabled = false;
        }
    }

    private IEnumerator Start()
    {
        yield return null;
        if (player == null) yield break;

        var presetService = player.GetComponentInChildren<PlayerPresetService>(true);
        if (presetService != null)
            presetService.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);

        // El preset puede dejar Magia bloqueada por progresión. En CombatLab se habilita
        // únicamente sobre la instancia de Will para que el laboratorio pueda probar ataques.
        var actionManager = player.GetComponentInChildren<PlayerActionManager>(true);
        if (actionManager != null)
        {
            actionManager.ApplyAbilities(new PlayerAbilities
            {
                swim = actionManager.AllowSwim,
                jump = actionManager.AllowJump,
                climb = actionManager.AllowClimb,
                fly = actionManager.AllowFly,
                sprint = actionManager.AllowSprint,
                magic = true,
                shield = true
            });
        }

        var magicPanel = GetComponent<CombatLabMagicPanel>();
        if (magicPanel == null)
            magicPanel = gameObject.AddComponent<CombatLabMagicPanel>();
        magicPanel.Initialize(player);

        var partyPanel = GetComponent<CombatLabPartyPanel>();
        if (partyPanel == null)
            partyPanel = gameObject.AddComponent<CombatLabPartyPanel>();
        partyPanel.Initialize(player);
    }
}
