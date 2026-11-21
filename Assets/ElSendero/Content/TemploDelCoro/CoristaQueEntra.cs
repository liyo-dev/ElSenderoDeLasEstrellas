using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Un corista que espera en la sala junto al Tenor y, cuando este grita «¡que vienen!», sale corriendo
/// a dar saltitos por la boca del templo (pasando por <see cref="por"/>) hasta su sitio, y desaparece:
/// empieza la función. Su estado sale del segundo de la canción: al volver atrás, vuelve a estar ahí.
/// Ver INC-1393.
/// </summary>
public sealed class CoristaQueEntra : MonoBehaviour
{
    [SerializeField] private TempoClock reloj;
    [Tooltip("Segundo de la canción en el que sale corriendo.")]
    [SerializeField] private float segundoDeSalir;
    [Tooltip("Punto de paso (la boca del templo).")]
    [SerializeField] private Vector3 por;
    [Tooltip("Adónde va; al llegar desaparece.")]
    [SerializeField] private Vector3 destino;
    [SerializeField, Min(0.1f)] private float segundos = 1.1f;
    [Tooltip("Altura de los saltitos al correr.")]
    [SerializeField, Min(0f)] private float saltito = 0.25f;

    private Vector3 _salida;
    private Renderer[] _renderers;
    private bool _visible = true;

    private void Awake()
    {
        _salida = transform.position;
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (reloj == null || !reloj.IsRunning) return;
        float t = (float)reloj.SongTime;
        if (t < segundoDeSalir)
        {
            transform.position = _salida;
            Ver(true);
            return;
        }
        float u = (t - segundoDeSalir) / segundos;
        if (u >= 1f) { Ver(false); return; }
        Ver(true);
        Vector3 sitio = u < 0.5f ? Vector3.Lerp(_salida, por, u * 2f) : Vector3.Lerp(por, destino, u * 2f - 1f);
        transform.position = sitio + Vector3.up * (Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5f)) * saltito);
    }

    private void Ver(bool si)
    {
        if (si == _visible) return;
        _visible = si;
        for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = si;
    }
}
