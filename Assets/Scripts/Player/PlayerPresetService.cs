using System;
using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Pone la partida en curso (<see cref="Partida.Preset"/>) en el grupo: inventario, ropa que se
/// tiene, habilidades, la apariencia y los hechizos de Will y los de Estela y Liam, y el maná del
/// que va al mando. Va en <see cref="GrupoDelJugador"/>: hay uno, aunque cada personaje tenga su
/// cuerpo. Lo de cada personaje se pone en su cuerpo (<see cref="ControlDelJugador.PilotoDe"/>), lo
/// lleve el jugador o la IA; un cuerpo que aparece después (carga de escena) recibe lo suyo al
/// aparecer. La vida y el maná de cada uno los lleva <see cref="SaludDelGrupo"/>.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerPresetService : MonoBehaviour
{
    [Header("Librería de hechizos (ID → SO)")]
    [SerializeField] private SpellLibrarySO spellLibrary;
    public SpellLibrarySO SpellLibrary => spellLibrary;

    [Header("Opciones")]
    [Tooltip("Completa los básicos de Will con los hechizos desbloqueados aunque el preset traiga los suyos.")]
    [SerializeField] private bool autoFillEmptySlotsFromUnlocked;

    // Evitar inicialización doble si el evento llega más de una vez o ya está listo al habilitar
    bool _initialized;

    public static bool HasAppliedPreset { get; private set; }

    // Evento público para notificar re-aplicación de preset (subscriptores UI o sistemas)
    public static Action OnPresetApplying;
    public static Action OnPresetApplied;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        HasAppliedPreset = false;
        OnPresetApplying = null;
        OnPresetApplied = null;
    }
#endif

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        PilotoDelCuerpo.Aparece += AlAparecerCuerpo;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(PlayerPresetService));
        if (GameBootService.IsAvailable)
            HandleProfileReady();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        PilotoDelCuerpo.Aparece -= AlAparecerCuerpo;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;
        _initialized = true;
        GameBootService.OnProfileReady -= HandleProfileReady;
        InitializePresetService();
    }

    private void InitializePresetService()
    {
        if (!GameBootService.Profile)
        {
            enabled = false;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogError("[PlayerPresetService] Falta GameBootProfile.");
#endif
            return;
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (!spellLibrary)
            Debug.LogWarning("[PlayerPresetService] SpellLibrary no asignada en el inspector: no se resolverán los SpellId.");
#endif

        var preset = Partida.Preset;
        if (!preset)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerPresetService] Sin preset activo.");
#endif
            return;
        }

        if (!string.IsNullOrEmpty(preset.spawnAnchorId))
            SpawnManager.SetCurrentAnchor(preset.spawnAnchorId);

        Aplicar(preset, includeInventory: true, includeAbilities: true);
    }

    /// <summary>Vuelve a poner la partida en curso en el grupo (tras cambiar algo en el preset).</summary>
    public void ApplyCurrentPreset(bool includeInventory = false, bool includeAbilities = true)
    {
        if (!GameBootService.IsAvailable)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerPresetService] GameBootService aún no está disponible");
#endif
            return;
        }
        var preset = Partida.Preset;
        if (!preset)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerPresetService] No hay preset activo para aplicar");
#endif
            return;
        }
        // Solo al cargar partida o empezar una nueva se rehace el inventario: al cambiar el equipo en
        // juego se perderían los objetos recogidos desde entonces.
        Aplicar(preset, includeInventory, includeAbilities);
    }

    private void Aplicar(PlayerPresetSO preset, bool includeInventory, bool includeAbilities)
    {
        OnPresetApplying?.Invoke();
        HasAppliedPreset = false;

        if (includeAbilities && preset.abilities == null) preset.abilities = new PlayerAbilities();

        ApplyManaFromPreset(preset);
        if (includeInventory) ApplyInventoryFromPreset(preset);
        ApplyWardrobeFromPreset(preset);
        ApplyAppearanceFromPreset(preset);
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
            AplicarAlCuerpo((Slot)i, preset, includeAbilities);

        HasAppliedPreset = true;
        OnPresetApplied?.Invoke();
    }

    // Un cuerpo que aparece después de leer la partida (carga de escena, un compañero que entra en
    // ella) recibe sus hechizos y sus habilidades.
    private void AlAparecerCuerpo(PilotoDelCuerpo cuerpo)
    {
        if (!HasAppliedPreset || cuerpo == null) return;
        var preset = Partida.Preset;
        if (!preset || !ControlDelJugador.PersonajeDe(cuerpo.gameObject, out var p)) return;
        AplicarAlCuerpo(p, preset, includeAbilities: true);
    }

    // Lo de cada personaje en su cuerpo: sus básicos (en su MagicCaster y en su IA) y sus
    // habilidades (nadar, volar, magia…; ver HabilidadesDelPersonaje).
    private void AplicarAlCuerpo(Slot p, PlayerPresetSO preset, bool includeAbilities)
    {
        var cuerpo = ControlDelJugador.PilotoDe(p);
        if (cuerpo == null) return;

        if (p == Slot.Will) PonerBasicosDeWill(cuerpo, preset);
        else GrimorioDelPersonaje.Aplicar(p);

        if (!includeAbilities) return;
        var acciones = cuerpo.GetComponent<PlayerActionManager>();
        if (acciones != null) acciones.ApplyAbilities(HabilidadesDelPersonaje.De(preset, p));
    }

    // ─── Apariencia de Will ───────────────────────────────────────────────

    /// La apariencia guardada de Will se pone en su cuerpo. Sin apariencia guardada se deja la que
    /// trae su cuerpo: un cuerpo vacío no se viste nunca.
    private static void ApplyAppearanceFromPreset(PlayerPresetSO preset)
    {
        var registro = CharacterAppearanceRegistry.Instance;
        if (registro == null) return;
        if (preset.appearance != null && preset.appearance.Count > 0)
            registro.SetAppearanceFromList(Slot.Will, preset.appearance);
        registro.ApplyAppearance(Slot.Will);
    }

    /// <summary>Guarda en el preset lo que lleva puesto Will (la ropa de debajo, sin atuendos).</summary>
    public void SnapshotAppearanceToPreset()
    {
        var preset = GameBootService.IsAvailable ? Partida.Preset : null;
        var registro = CharacterAppearanceRegistry.Instance;
        if (preset == null || registro == null) return;

        var seleccion = registro.RopaDeDebajo(Slot.Will);
        if (seleccion.Count == 0) return;
        preset.appearance = registro.GetAppearanceAsList(Slot.Will);
    }

    // ─── Grupo ────────────────────────────────────────────────────────────

    void ApplyWardrobeFromPreset(PlayerPresetSO preset)
    {
        if (PlayerService.TryGetComponent(out WardrobeInventory armario, includeInactive: true, allowSceneLookup: true))
            armario.ApplyFromPreset(preset);
    }

    private void ApplyInventoryFromPreset(PlayerPresetSO preset)
    {
        if (!PlayerService.TryGetComponent(out Inventory inventario, includeInactive: true, allowSceneLookup: true))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerPresetService] No se encontró componente Inventory para cargar el inventario del preset");
#endif
            return;
        }
        var items = preset.inventoryItems;
        inventario.LoadSnapshot(items != null && items.Count > 0 ? items : null, clearExisting: true, notifyChanges: false);
    }

    // ─── Maná ─────────────────────────────────────────────────────────────

    /// El maná del preset es el de Will y va en su cuerpo cuando él va al mando; con otro al mando
    /// cada uno lleva el suyo (SaludDelGrupo).
    private void ApplyManaFromPreset(PlayerPresetSO preset)
    {
        if (ControlDelJugador.Activo != Slot.Will)
        {
            SaludDelGrupo.Instance?.CargarManaDelActivo();
            return;
        }
        var cuerpo = ControlDelJugador.PilotoDe(Slot.Will);
        var mana = cuerpo != null ? cuerpo.GetComponent<ManaPool>() : null;
        if (mana == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PlayerPresetService] Will no tiene ManaPool en escena para poner el maná del preset.");
#endif
            return;
        }

        // Sin la habilidad de magia no se enseña una barra de maná llena.
        if (preset.abilities != null && !preset.abilities.magic)
        {
            mana.Init(0f, 0f);
            return;
        }

        float maxMP = Mathf.Max(0f, preset.maxMP);
        float currentMP = Mathf.Clamp(preset.currentMP, 0f, maxMP > 0f ? maxMP : float.MaxValue);

        // Sin maná guardado: el justo para lanzar un par de veces el hechizo más caro que conoce. Un
        // preset 0/0 sin hechizos se respeta.
        if (maxMP <= 0f)
        {
            float masCaro = Mathf.Max(CosteMasAlto(preset.basicSpellIds), CosteMasAlto(preset.unlockedSpells));
            if (masCaro > 0f)
            {
                maxMP = Mathf.Max(20f, masCaro * 2f);
                currentMP = Mathf.Max(currentMP, maxMP);
            }
            else if (preset.currentMP <= 0f && preset.maxMP <= 0f)
            {
                maxMP = 0f;
                currentMP = 0f;
            }
            else
            {
                maxMP = 50f;
                if (currentMP <= 0f) currentMP = Mathf.Clamp(preset.currentMP, 0f, maxMP);
            }
        }

        // El preset guarda la magia de base; lo que suma el equipo va encima (EstadisticasDelPersonaje).
        float bonoMagia = EstadisticasDelPersonaje.Bonos.magia;
        if (bonoMagia != 0f)
        {
            maxMP = Mathf.Max(0f, maxMP + bonoMagia);
            currentMP = Mathf.Clamp(Mathf.Max(currentMP, preset.currentMP), 0f, maxMP);
        }

        mana.Init(maxMP, currentMP);
    }

    private float CosteMasAlto(List<SpellId> ids)
    {
        float coste = 0f;
        if (ids == null || spellLibrary == null) return coste;
        foreach (var id in ids)
        {
            if (id == SpellId.None) continue;
            var s = spellLibrary.Get(id);
            if (s != null) coste = Mathf.Max(coste, s.manaCost);
        }
        return coste;
    }

    // ─── Hechizos de Will ─────────────────────────────────────────────────

    /// Los básicos de Will del preset (hasta cuatro, en orden de rotación) en su MagicCaster y en
    /// su IA. Se descartan los que no están en SpellLibrary, los de solo especial, los repetidos y
    /// los de otro personaje. Si el preset no trae ninguno, o autoFillEmptySlotsFromUnlocked está
    /// activo, se completan con los desbloqueados.
    private void PonerBasicosDeWill(PilotoDelCuerpo cuerpo, PlayerPresetSO preset)
    {
        if (spellLibrary == null) return;
        var basics = new List<MagicSpellSO>(MagicCaster.BasicSlotCount);

        void TryAdd(SpellId id)
        {
            if (id == SpellId.None || basics.Count >= MagicCaster.BasicSlotCount) return;
            var spell = spellLibrary.Get(id);
            if (spell == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[PlayerPresetService] El hechizo {id} no está en SpellLibrary");
#endif
                return;
            }
            if (spell.slotType == SpellSlotType.SpecialOnly || basics.Contains(spell)) return;
            if (spell.caster != Slot.Will) return;
            basics.Add(spell);
        }

        if (preset.basicSpellIds != null)
            foreach (var id in preset.basicSpellIds) TryAdd(id);
        if ((autoFillEmptySlotsFromUnlocked || basics.Count == 0) && preset.unlockedSpells != null)
            foreach (var id in preset.unlockedSpells) TryAdd(id);

        var caster = cuerpo.GetComponent<MagicCaster>();
        if (caster != null) caster.SetBasicSpells(basics);
        var ia = cuerpo.GetComponent<Game.NPC.NPCPartyMember>();
        if (ia != null) ia.SetRuntimeBasics(basics);
    }
}
