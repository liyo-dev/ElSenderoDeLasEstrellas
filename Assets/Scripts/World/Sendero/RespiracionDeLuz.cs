using UnityEngine;

/// <summary>
/// Luz que sube y baja despacio, «igual que una respiración» (el altar del Sendero). Mueve la
/// intensidad de una luz y el _Intensidad de unos renderers con un MaterialPropertyBlock.
/// </summary>
public class RespiracionDeLuz : MonoBehaviour
{
    private static readonly int IntensidadId = Shader.PropertyToID("_Intensidad");

    [SerializeField] private Light luz;
    [SerializeField] private Renderer[] renderers = new Renderer[0];
    [Tooltip("Segundos de una respiración completa.")]
    [SerializeField, Min(0.5f)] private float periodo = 4.5f;
    [SerializeField] private Vector2 intensidadDeLuz = new(1.2f, 3.2f);
    [SerializeField] private Vector2 intensidadDeBrillo = new(0.5f, 1.4f);

    private MaterialPropertyBlock _bloque;

    public void Configurar(Light luz, Renderer[] renderers)
    {
        this.luz = luz;
        this.renderers = renderers;
    }

    private void Update()
    {
        float r = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / periodo);
        r = r * r * (3f - 2f * r);
        if (luz != null) luz.intensity = Mathf.Lerp(intensidadDeLuz.x, intensidadDeLuz.y, r);
        if (renderers.Length == 0) return;
        _bloque ??= new MaterialPropertyBlock();
        float brillo = Mathf.Lerp(intensidadDeBrillo.x, intensidadDeBrillo.y, r);
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(_bloque);
            _bloque.SetFloat(IntensidadId, brillo);
            rend.SetPropertyBlock(_bloque);
        }
    }
}
