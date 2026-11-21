using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// La raya de un corista: una línea del pentagrama que se enciende a lo largo del suelo y en cada
/// tiempo de la canción salta al carril de al lado, de un lado a otro y vuelta. Golpea la línea entera
/// al llegar a su carril; el carril al que irá en el siguiente tiempo se ve tenue (el aviso). Solo
/// actúa mientras su objeto está activo (el corista en pie). Ver INC-1393.
/// </summary>
public sealed class RayaQueBarre : MonoBehaviour
{
    [SerializeField] private TempoClock reloj;
    [Tooltip("X de cada carril, en orden de un lado al otro.")]
    [SerializeField] private float[] carriles = { -4f, -2f, 0f, 2f, 4f };
    [Tooltip("Carril en el que empieza.")]
    [SerializeField, Min(0)] private int primerCarril;
    [Tooltip("Z del centro de la línea y su largo (a lo largo del corredor).")]
    [SerializeField] private float zDelCentro;
    [SerializeField, Min(1f)] private float largo = 16f;
    [Tooltip("Segundos de la canción entre los que barre.")]
    [SerializeField] private float desde;
    [SerializeField] private float hasta = 100000f;
    [Tooltip("La raya encendida (tumbada en el suelo).")]
    [SerializeField] private Transform raya;
    [Tooltip("La raya tenue del carril siguiente.")]
    [SerializeField] private Transform aviso;
    [SerializeField, Min(0.1f)] private float radio = 0.7f;
    [SerializeField, Min(0f)] private float dano = 8f;
    [SerializeField] private float altura = 0.06f;

    private int _ultimoPaso = int.MinValue;
    private bool _visible = true;

    private void OnEnable() => _ultimoPaso = int.MinValue;

    private void Update()
    {
        var partitura = reloj != null ? reloj.Score : null;
        if (partitura == null || !reloj.IsRunning || carriles.Length == 0) { Ver(false); return; }
        float t = (float)reloj.SongTime;
        if (t < desde || t >= hasta) { Ver(false); _ultimoPaso = int.MinValue; return; }
        int paso = partitura.BeatIndexAt(t) - partitura.BeatIndexAt(desde);
        Ver(true);
        float x = carriles[Carril(paso)];
        Colocar(raya, x);
        Colocar(aviso, carriles[Carril(paso + 1)]);
        if (paso == _ultimoPaso) return;
        // Ha llegado a un carril nuevo (o se ha vuelto atrás al reintentar): golpe a lo largo.
        bool avanza = paso > _ultimoPaso && _ultimoPaso != int.MinValue;
        _ultimoPaso = paso;
        if (avanza && dano > 0f) Golpear(x);
    }

    /// <summary>De un lado a otro y vuelta, empezando en <see cref="primerCarril"/>.</summary>
    private int Carril(int paso)
    {
        int n = carriles.Length;
        if (n == 1) return 0;
        int periodo = 2 * (n - 1);
        int i = ((paso + primerCarril) % periodo + periodo) % periodo;
        return i < n ? i : periodo - i;
    }

    private void Golpear(float x)
    {
        int n = Mathf.CeilToInt(largo / (radio * 1.5f));
        for (int i = 0; i <= n; i++)
        {
            float z = zDelCentro + largo * ((float)i / n - 0.5f);
            DanoEnZona.GolpearEnCilindro(new Vector3(x, transform.position.y, z), radio, 2.2f, 0.3f, dano, 5f);
        }
    }

    private void Colocar(Transform que, float x)
    {
        if (que == null) return;
        que.position = new Vector3(x, transform.position.y + altura, zDelCentro);
    }

    private void Ver(bool si)
    {
        if (si == _visible) return;
        _visible = si;
        if (raya != null) raya.gameObject.SetActive(si);
        if (aviso != null) aviso.gameObject.SetActive(si);
    }
}
