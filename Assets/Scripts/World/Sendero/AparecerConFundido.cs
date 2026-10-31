using UnityEngine;

/// <summary>
/// Al encenderse el objeto, sube despacio la propiedad de sus materiales (por defecto _Encendida de
/// las puertas del Sendero) y la intensidad de su luz, en vez de aparecer de golpe.
/// </summary>
public class AparecerConFundido : MonoBehaviour
{
    [SerializeField] private string propiedad = "_Encendida";
    [SerializeField, Min(0.05f)] private float duracion = 1.6f;
    [SerializeField] private Renderer[] renderers = new Renderer[0];
    [SerializeField] private Light luz;

    private MaterialPropertyBlock _bloque;
    private int _id;
    private float _intensidadDeLuz;
    private float _t;

    public void Configurar(Renderer[] renderers, Light luz)
    {
        this.renderers = renderers;
        this.luz = luz;
    }

    private void Awake()
    {
        _id = Shader.PropertyToID(propiedad);
        if (luz != null) _intensidadDeLuz = luz.intensity;
    }

    private void OnEnable()
    {
        _t = 0f;
        Aplicar(0f);
    }

    private void Update()
    {
        if (_t >= duracion) return;
        _t += Time.deltaTime;
        Aplicar(Mathf.SmoothStep(0f, 1f, _t / duracion));
    }

    private void Aplicar(float v)
    {
        _bloque ??= new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_bloque);
            _bloque.SetFloat(_id, v);
            r.SetPropertyBlock(_bloque);
        }
        if (luz != null) luz.intensity = _intensidadDeLuz * v;
    }
}
