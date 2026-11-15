/// <summary>Traduce los textos de los nodos con <see cref="LocalizationManager"/>.</summary>
public sealed class LocalizationNarrativeAdapter : INarrativeTextLocalizer
{
    public string Localize(string key, string fallback)
    {
        var lm = LocalizationManager.Instance;
        return lm != null ? lm.Get(key, fallback) : fallback;
    }
}
