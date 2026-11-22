using UnityEngine;

/// <summary>
/// Una ráfaga que recorre un tramo: al llamar a <see cref="Soplar"/> aparece en <see cref="desde"/>,
/// viaja hasta <see cref="hasta"/> creciendo y desaparece. Adorno (la bocanada del órgano en
/// «¡qué pulmones tiene el bicho!»). Sin Update cuando está quieta. Ver INC-1393.
/// </summary>
public sealed class RafagaQueRecorre : MonoBehaviour
{
    [SerializeField] private Transform desde;
    [SerializeField] private Transform hasta;
    [SerializeField, Min(0.05f)] private float segundos = 1.2f;
    [Tooltip("Escala al salir y al llegar.")]
    [SerializeField] private Vector2 escala = new Vector2(0.4f, 1.4f);

    private Renderer[] _renderers;
    private Vector3 _escalaBase;
    private float _empezo;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _escalaBase = transform.localScale;
        Mostrar(false);
        enabled = false;
    }

    /// <summary>Echa a andar la ráfaga desde el principio.</summary>
    public void Soplar()
    {
        if (desde == null || hasta == null) return;
        _empezo = Time.time;
        Mostrar(true);
        enabled = true;
        Colocar(0f);
    }

    private void Update()
    {
        float u = (Time.time - _empezo) / segundos;
        if (u >= 1f)
        {
            Mostrar(false);
            enabled = false;
            return;
        }
        Colocar(u);
    }

    private void Colocar(float u)
    {
        transform.position = Vector3.Lerp(desde.position, hasta.position, u);
        transform.localScale = _escalaBase * Mathf.Lerp(escala.x, escala.y, u);
    }

    private void Mostrar(bool si)
    {
        for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = si;
    }
}
