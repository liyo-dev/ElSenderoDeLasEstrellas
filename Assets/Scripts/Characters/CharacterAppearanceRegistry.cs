using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Apariencia de cada personaje del grupo: qué piezas lleva puestas (la ropa de debajo, la que se
/// guarda) y lo que se ve con los atuendos de escena encima (CharacterAppearanceRegistry.Atuendos.cs).
///
/// Cada personaje tiene su cuerpo con su <see cref="ModularAutoBuilder"/>
/// (<see cref="ControlDelJugador.PilotoDe"/>): la apariencia de un personaje se pone siempre en su
/// cuerpo, lo lleve el jugador o la IA. Al aparecer un cuerpo en escena se viste con la suya.
///
/// Uso típico:
///   CharacterAppearanceRegistry.Instance.ApplyAppearance(CharacterSlot.Liam);
///   CharacterAppearanceRegistry.Instance.UpdatePart(CharacterSlot.Estela, PartCategory.Hat, "Hat03");
/// </summary>
[DefaultExecutionOrder(-100)]
public partial class CharacterAppearanceRegistry : MonoBehaviour
{
    #region Singleton
    public static CharacterAppearanceRegistry Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
    #endregion

    [Header("Apariencia inicial de Liam")]
    [Tooltip("Partes activas de Liam. Deben coincidir con los nombres de las piezas de su cuerpo.")]
    [SerializeField] private List<AppearanceEntry> liamDefault = new();

    [Header("Apariencia inicial de Estela")]
    [Tooltip("Partes activas de Estela. Deben coincidir con los nombres de las piezas de su cuerpo.")]
    [SerializeField] private List<AppearanceEntry> estelaDefault = new();

    [Tooltip("Juego de caras (perfil de emociones) de cada personaje, por su número de slot (Liam, Will, Estela). " +
             "Se le pone a su cuerpo al vestirlo. Vacío: el que traiga el cuerpo.")]
    [SerializeField] private EmotionProfile[] perfilesDeCara = new EmotionProfile[3];

    /// <summary>El juego de caras del personaje; null si no tiene uno propio.</summary>
    public EmotionProfile PerfilDeCara(Slot slot)
        => perfilesDeCara != null && (int)slot < perfilesDeCara.Length ? perfilesDeCara[(int)slot] : null;

    // La ropa de debajo de cada personaje, por su número de slot. La de Will sale del preset; si no
    // hay, de lo que lleva su cuerpo.
    private readonly Dictionary<PartCategory, string>[] _appearances = new Dictionary<PartCategory, string>[ControlDelJugador.Personajes];

    // Este registro ya ha vestido ese cuerpo: lo que enseña es suyo y se puede leer de vuelta.
    private readonly bool[] _aplicada = new bool[ControlDelJugador.Personajes];

    // Materiales originales de cada cuerpo que han cambiado los tintes de los atuendos.
    private readonly Dictionary<Renderer, Material[]>[] _materiales =
    {
        new Dictionary<Renderer, Material[]>(), new Dictionary<Renderer, Material[]>(), new Dictionary<Renderer, Material[]>()
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _appearances[(int)Slot.Liam]   = ToDict(liamDefault);
        _appearances[(int)Slot.Estela] = ToDict(estelaDefault);
    }

    // ─── API ──────────────────────────────────────────────────────────────────

    /// <summary>El vestidor del cuerpo de ese personaje. Null si no está en escena.</summary>
    public static ModularAutoBuilder RopaDe(Slot slot)
    {
        var piloto = ControlDelJugador.PilotoDe(slot);
        return piloto != null ? piloto.GetComponentInChildren<ModularAutoBuilder>(true) : null;
    }

    /// <summary>
    /// Viste el cuerpo del personaje con su apariencia y los atuendos de escena que lleve encima.
    /// </summary>
    public void ApplyAppearance(Slot slot)
    {
        var b = RopaDe(slot);
        if (b == null) return;

        var app = AparienciaVisible(slot);
        // Sin apariencia guardada (Will antes de leer el preset) no se desviste a nadie: la suya es
        // la que trae su cuerpo.
        if (app == null || app.Count == 0)
        {
            var actual = b.GetSelection();
            if (actual.Count == 0) return;
            _appearances[(int)slot] = actual;
            app = AparienciaVisible(slot);
        }

        b.DeactivateAllCategories();
        b.ApplySelection(app);

        // Si los nombres no existen en este cuerpo, mejor lo que traía el prefab que un cuerpo vacío.
        if (b.GetSelection().Count == 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[CharacterAppearanceRegistry] La apariencia de {slot} no tiene piezas de su cuerpo: se deja la del prefab.");
#endif
            b.RestoreInitialSelection();
            _appearances[(int)slot] = b.GetSelection();
        }

        _aplicada[(int)slot] = true;
        RecordarLoMostrado(slot, b);
        Tenir(b, slot, _materiales[(int)slot]);
        PonerPrendasSueltas(b, slot);
        PonerSuCara(b, slot);
        b.RefreshRendererBoundsAfterAppearanceChange();
    }

    /// <summary>
    /// Lee lo que lleva el cuerpo del personaje y lo guarda como su apariencia (cambios del menú de
    /// equipo). Con un atuendo de escena puesto solo se guarda lo que haya cambiado el jugador,
    /// nunca el atuendo.
    /// </summary>
    public void CaptureCurrentAppearance(Slot slot)
    {
        var b = RopaDe(slot);
        if (b == null) return;
        var selection = b.GetSelection();
        if (selection.Count == 0) return;
        _appearances[(int)slot] = QuitarLoDelAtuendo(slot, selection);
        _aplicada[(int)slot] = true;
    }

    /// <summary>
    /// Actualiza una pieza individual para un personaje.
    /// Pasar partName vacío o null elimina la categoría (la desactiva).
    /// </summary>
    public void UpdatePart(Slot slot, PartCategory category, string partName)
    {
        var dict = _appearances[(int)slot] ??= new Dictionary<PartCategory, string>();
        if (string.IsNullOrEmpty(partName))
            dict.Remove(category);
        else
            dict[category] = partName;
    }

    /// <summary>Guarda lo que lleva puesto el personaje al mando (tras cambiarse en el menú).</summary>
    public void CaptureActiveCharacterAppearance() => CaptureCurrentAppearance(ControlDelJugador.Activo);

    /// <summary>Devuelve una copia de la apariencia del slot: la ropa de debajo, sin atuendos de escena.</summary>
    public Dictionary<PartCategory, string> GetAppearance(Slot slot)
    {
        var src = _appearances[(int)slot];
        return src != null
            ? new Dictionary<PartCategory, string>(src)
            : new Dictionary<PartCategory, string>();
    }

    /// <summary>
    /// Devuelve la apariencia como List&lt;AppearanceEntry&gt; para serialización en save data.
    /// </summary>
    public List<AppearanceEntry> GetAppearanceAsList(Slot slot)
    {
        var list = new List<AppearanceEntry>();
        var dict = _appearances[(int)slot];
        if (dict == null) return list;
        foreach (var kv in dict)
            list.Add(new AppearanceEntry { category = kv.Key, partName = kv.Value });
        return list;
    }

    /// <summary>
    /// Carga una apariencia desde datos guardados (p.ej. al continuar una partida). No viste el
    /// cuerpo: llama a ApplyAppearance() después si hace falta.
    /// </summary>
    public void SetAppearanceFromList(Slot slot, List<AppearanceEntry> entries)
    {
        _appearances[(int)slot] = ToDict(entries);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>El cuerpo que se acaba de vestir pone las caras del personaje (sus ojos y bocas por emoción).</summary>
    private void PonerSuCara(ModularAutoBuilder cuerpo, Slot slot)
    {
        var perfil = PerfilDeCara(slot);
        if (perfil == null || cuerpo == null) return;
        var cara = cuerpo.GetComponentInParent<NPCEmotionController>() ?? cuerpo.GetComponentInChildren<NPCEmotionController>(true);
        if (cara != null && cara.EmotionProfile != perfil) cara.SetEmotionProfile(perfil);
    }

    private static Dictionary<PartCategory, string> ToDict(List<AppearanceEntry> entries)
    {
        var dict = new Dictionary<PartCategory, string>();
        if (entries == null) return dict;
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.partName))
                dict[e.category] = e.partName;
        return dict;
    }
}
