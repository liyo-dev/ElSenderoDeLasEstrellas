using System.Collections;
using UnityEngine;

/// Algo que un personaje hace aparecer y deshace con magia: pilares, plataformas, espinas,
/// runas... Al aparecer crece desde su pivote (ponerlo en la base para que salga del suelo) y al
/// deshacerse se encoge y se apaga. Mientras está, sus colliders cuentan y, si lleva un
/// NavMeshObstacle con «carve», recorta la malla de navegación. Si su GameObject empieza
/// desactivado en la escena, empieza oculto. Ver INC-661.
[DisallowMultipleComponent]
public sealed class ObjetoConjurado : MonoBehaviour
{
    [Tooltip("Segundos que tarda en crecer al aparecer.")]
    [SerializeField, Min(0.05f)] private float duracionAparecer = 0.6f;
    [Tooltip("Segundos que tarda en encogerse al deshacerse.")]
    [SerializeField, Min(0.05f)] private float duracionDeshacer = 0.5f;
    [Tooltip("Efecto de un solo uso al aparecer. Vacío = nada.")]
    [SerializeField] private GameObject vfxAparecer;
    [Tooltip("Efecto de un solo uso al deshacerse. Vacío = nada.")]
    [SerializeField] private GameObject vfxDeshacer;
    [SerializeField, Min(0.1f)] private float duracionVfx = 1.5f;

    private const float EscalaMinima = 0.01f;

    private Vector3 _escala;
    private Coroutine _rutina;

    /// Está en escena (o creciendo). Falso mientras se deshace y cuando está oculto.
    public bool Presente { get; private set; }

    void Awake() => _escala = transform.localScale;

    public void Aparecer()
    {
        if (Presente) return;
        Presente = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        Efecto(vfxAparecer);
        Arrancar(Escalar(EscalaMinima, 1f, duracionAparecer, ocultarAlFinal: false));
    }

    public void Deshacer()
    {
        if (!Presente) return;
        Presente = false;
        if (!gameObject.activeInHierarchy) return;
        Efecto(vfxDeshacer);
        Arrancar(Escalar(1f, EscalaMinima, duracionDeshacer, ocultarAlFinal: true));
    }

    void OnDisable()
    {
        _rutina = null;
        transform.localScale = _escala;
    }

    private void Arrancar(IEnumerator rutina)
    {
        if (_rutina != null) StopCoroutine(_rutina);
        _rutina = StartCoroutine(rutina);
    }

    private IEnumerator Escalar(float desde, float hasta, float duracion, bool ocultarAlFinal)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duracion);
            transform.localScale = _escala * Mathf.Lerp(desde, hasta, k);
            yield return null;
        }
        transform.localScale = _escala * hasta;
        _rutina = null;
        if (ocultarAlFinal && gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void Efecto(GameObject prefab)
    {
        if (prefab != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(prefab, transform.position, Quaternion.identity, duracionVfx);
    }
}
