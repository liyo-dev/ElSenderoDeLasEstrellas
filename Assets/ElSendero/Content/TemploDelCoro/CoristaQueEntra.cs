using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Un corista que espera en la sala junto al Tenor y, cuando este grita «¡que vienen!», sale corriendo
/// a dar saltitos por la boca del templo (pasando por <see cref="por"/>) hasta su sitio, y desaparece:
/// empieza la función. Corre con gracia: un respingo del susto al empezar, saltitos con los que se
/// aplasta y se estira, contoneándose de un lado a otro, y al final se encoge de golpe al colarse.
/// Su estado sale del segundo de la canción: al volver atrás, vuelve a estar ahí. Ver INC-1393.
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
    [SerializeField, Min(0f)] private float saltito = 0.45f;
    [Tooltip("Saltitos que da hasta su sitio.")]
    [SerializeField, Min(1)] private int saltitos = 6;
    [Tooltip("Grados que se contonea de un lado a otro en cada saltito.")]
    [SerializeField, Min(0f)] private float contoneo = 16f;
    [Tooltip("Parte del recorrido que dura el respingo del susto, antes de echar a correr.")]
    [SerializeField, Range(0f, 0.5f)] private float respingo = 0.18f;

    private Vector3 _salida;
    private Vector3 _escala;
    private Quaternion _giro;
    private Renderer[] _renderers;
    private bool _visible = true;

    private void Awake()
    {
        _salida = transform.position;
        _escala = transform.localScale;
        _giro = transform.localRotation;
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (reloj == null || !reloj.IsRunning) return;
        float t = (float)reloj.SongTime;
        if (t < segundoDeSalir)
        {
            Colocar(_salida, Vector3.one, 0f);
            Ver(true);
            return;
        }
        float u = (t - segundoDeSalir) / segundos;
        if (u >= 1f) { Ver(false); return; }
        Ver(true);
        if (u < respingo)
        {
            // El susto: un bote en el sitio, estirándose del todo.
            float r = u / respingo;
            float alto = Mathf.Sin(r * Mathf.PI);
            Colocar(_salida + Vector3.up * (alto * saltito * 1.4f), new Vector3(1f - 0.25f * alto, 1f + 0.35f * alto, 1f), 0f);
            return;
        }
        float c = (u - respingo) / (1f - respingo);
        Vector3 sitio = c < 0.5f ? Vector3.Lerp(_salida, por, c * 2f) : Vector3.Lerp(por, destino, c * 2f - 1f);
        float fase = c * saltitos;
        float enElSalto = fase - Mathf.Floor(fase);
        float arriba = Mathf.Sin(enElSalto * Mathf.PI);
        // Abajo se aplasta, arriba se estira; cada saltito, hacia un lado.
        float aplaste = 1f - arriba;
        Vector3 forma = new Vector3(1f + 0.22f * aplaste - 0.1f * arriba, 1f - 0.25f * aplaste + 0.15f * arriba, 1f);
        float lado = ((int)fase % 2 == 0 ? 1f : -1f) * contoneo * arriba;
        // Los últimos instantes se encoge de golpe al colarse por la puerta.
        if (c > 0.88f) forma *= Mathf.Lerp(1f, 0.2f, (c - 0.88f) / 0.12f);
        Colocar(sitio + Vector3.up * (arriba * saltito), forma, lado);
    }

    private void Colocar(Vector3 sitio, Vector3 forma, float grados)
    {
        transform.position = sitio;
        transform.localScale = Vector3.Scale(_escala, forma);
        transform.localRotation = _giro * Quaternion.Euler(0f, 0f, grados);
    }

    private void Ver(bool si)
    {
        if (si == _visible) return;
        _visible = si;
        for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = si;
    }
}
