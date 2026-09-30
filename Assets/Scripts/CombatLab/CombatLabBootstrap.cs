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
    [Tooltip("Con quién empieza el jugador: 0 solo, 1 con Estela, 2 con Liam, 3 con los dos.")]
    [SerializeField, Range(0, 3)] private int grupoInicial = 0;

    /// Identificadores de SceneBoundUI (Start.unity) que el laboratorio muestra aunque no sea escena de campaña.
    private static readonly string[] InterfazDelLaboratorio = { "PlayerHUD_UI", "PlayerEquipementMenu_UI" };

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
        var camara = Camera.main;   // CamaraDelJugador: la cámara no va dentro del jugador (INC-482)
        var escuchaPrincipal = camara != null ? camara.GetComponent<AudioListener>() : null;
        if (escuchaPrincipal == null) return;

        var escuchas = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
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

        // El HUD y el menú de Start se ocultan en escenas fuera de campaña. Se habilitan aquí en
        // runtime, sin cambiar su configuración persistente ni guardar esta excepción en la partida.
        for (int i = 0; i < InterfazDelLaboratorio.Length; i++)
            SceneBoundUI.AllowSceneFor(InterfazDelLaboratorio[i], gameObject.scene.name);

        if (player == null) yield break;

        var presetService = player.GetComponentInChildren<PlayerPresetService>(true);
        if (presetService != null)
        {
            PrepararPresetDeSesion(presetService.SpellLibrary);
            presetService.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
        }

        var magicPanel = GetComponent<CombatLabMagicPanel>();
        if (magicPanel == null)
            magicPanel = gameObject.AddComponent<CombatLabMagicPanel>();
        magicPanel.Initialize(player);

        var partyPanel = GetComponent<CombatLabPartyPanel>();
        if (partyPanel == null)
            partyPanel = gameObject.AddComponent<CombatLabPartyPanel>();
        partyPanel.Initialize(player, grupoInicial);
    }

    /// Maná mínimo del laboratorio: cubre dos lanzamientos del hechizo más caro de la biblioteca.
    private const float ManaMinimoDePrueba = 50f;

    /// El laboratorio trabaja con todas las habilidades y con maná. Se escribe en el preset de la
    /// sesión (copia en memoria: no toca el perfil ni la partida) y no en los componentes, porque
    /// cualquier re-aplicación posterior del preset (equipar en el menú de Start, cambiar de ropa,
    /// recoger una página) vuelve a leerlo y, con la magia apagada, deja el maná a 0. Ver INC-522.
    private static void PrepararPresetDeSesion(SpellLibrarySO biblioteca)
    {
        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;

        preset.abilities = new PlayerAbilities
        {
            swim = true,
            jump = true,
            climb = true,
            fly = true,
            sprint = true,
            magic = true,
            shield = true
        };

        if (preset.maxMP <= 0f)
        {
            float masCaro = 0f;
            if (biblioteca != null && biblioteca.Spells != null)
                foreach (var spell in biblioteca.Spells)
                    if (spell != null) masCaro = Mathf.Max(masCaro, spell.manaCost);
            preset.maxMP = Mathf.Max(ManaMinimoDePrueba, masCaro * 2f);
        }
        preset.currentMP = preset.maxMP;
    }
}
