using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Juego/Wardrobe Item", fileName = "WardrobeItem_01")]
public class WardrobeItemSO : ScriptableObject
{
    [SerializeField] private string wardrobeId;
    [SerializeField] private PartCategory category = PartCategory.Body;
    [SerializeField] private string partName;
    [Tooltip("ID de localización para el nombre (ej: 'WARDROBE_CLOAK01_NAME'). Si está vacío, usa displayName.")]
    [SerializeField] private string displayNameId;
    [SerializeField] private string displayName;
    [Tooltip("ID de localización para la descripción (ej: 'WARDROBE_CLOAK01_DESC'). Si está vacío, usa description.")]
    [SerializeField] private string descriptionId;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Estadísticas que aporta esta prenda mientras está equipada.")]
    [SerializeField] private Estadisticas bonos;

    static readonly Dictionary<string, WardrobeItemSO> Registry = new Dictionary<string, WardrobeItemSO>();

    public string WardrobeId => string.IsNullOrEmpty(wardrobeId) ? name : wardrobeId;
    public PartCategory Category => category;
    public string PartName => partName;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? partName : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public Estadisticas Bonos => bonos;

    public static WardrobeItemSO FindPart(PartCategory category, string part)
    {
        var item = BuscarRegistrada(category, part);
        // Una prenda solo se registra cuando su asset está cargado. Si falta, se carga el registro
        // de objetos (cada ItemData de prenda referencia su WardrobeItemSO) y se busca otra vez.
        if (item == null && CargarCatalogo()) item = BuscarRegistrada(category, part);
        return item;
    }

    static WardrobeItemSO BuscarRegistrada(PartCategory category, string part)
    {
        foreach (var item in Registry.Values)
            if (item != null && item.Category == category &&
                string.Equals(item.PartName, part, System.StringComparison.OrdinalIgnoreCase)) return item;
        return null;
    }

    static bool _catalogoCargado;

    static bool CargarCatalogo()
    {
        if (_catalogoCargado) return false;
        _catalogoCargado = true;
        var registro = ItemRegistrySO.LoadDefault();
        if (registro == null) return false;
        foreach (var objeto in registro.Items)
            if (objeto != null && objeto.wardrobeUnlock != null) objeto.wardrobeUnlock.Register();
        return true;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _catalogoCargado = false;
#endif

    /// <summary>Obtiene el nombre localizado del item de armario (usa displayNameId si está definido).</summary>
    public string GetLocalizedDisplayName()
    {
        if (!string.IsNullOrEmpty(displayNameId) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(displayNameId, DisplayName);
        return DisplayName;
    }

    /// <summary>Obtiene la descripción localizada del item de armario (usa descriptionId si está definido).</summary>
    public string GetLocalizedDescription()
    {
        if (!string.IsNullOrEmpty(descriptionId) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(descriptionId, description);
        return description;
    }

    void OnEnable() => Register();
#if UNITY_EDITOR
    void OnValidate() => Register();
#endif

    void Register()
    {
        if (string.IsNullOrEmpty(partName)) return;
        if (string.IsNullOrEmpty(wardrobeId)) wardrobeId = name;
        Registry[WardrobeId] = this;
    }

    public static WardrobeItemSO Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Registry.TryGetValue(id, out var item);
        return item;
    }
}
