using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// El órgano del Tenor, con cara («¡os presento a mi órgano!»): aparece al fondo de la nave, espera
/// y luego va saltando hacia el grupo, un salto en cada tiempo de <see cref="caidas"/>, cayendo encima
/// de donde va a estar el jugador para aplastarlo: hay que apartarse. Su sombra en el suelo avisa de
/// dónde cae. Tras el último salto se hunde. Su estado sale del segundo de la canción: al reintentar
/// vuelve a empezar. Ver INC-1393.
/// </summary>
public sealed class OrganoQueSalta : MonoBehaviour
{
    [SerializeField] private TempoClock reloj;
    [Tooltip("Segundo de la canción en el que sale (sube del suelo) al fondo.")]
    [SerializeField] private float segundoDeAparecer;
    [Tooltip("Segundos de la canción en los que cae al suelo, uno por salto (cada tiempo).")]
    [SerializeField] private float[] caidas = System.Array.Empty<float>();
    [Tooltip("La sombra del sitio donde va a caer (se copia en su sitio y crece hasta el golpe).")]
    [SerializeField] private Transform sombra;
    [SerializeField, Min(0f)] private float altura = 3f;
    [Tooltip("Metros que avanza como mucho en un salto.")]
    [SerializeField, Min(0.5f)] private float pasoMaximo = 3f;
    [Tooltip("Metros a cada lado del eje del corredor por los que puede caer.")]
    [SerializeField, Min(0f)] private float medioAncho = 3.5f;
    [Tooltip("Cuánto cuenta la velocidad del jugador al apuntar (1 = donde estará si sigue igual).")]
    [SerializeField, Range(0f, 1.5f)] private float anticipacion = 0.7f;
    [Tooltip("Dónde cae en cada salto (en el orden de las caídas; la altura no cuenta). Vacío: a por el jugador.")]
    [SerializeField] private Vector3[] sitios = System.Array.Empty<Vector3>();
    [SerializeField, Min(0f)] private float radio = 2f;
    [SerializeField, Min(0f)] private float dano = 12f;
    [SerializeField] private AudioClip golpe;
    [SerializeField, Range(0f, 1f)] private float volumen = 1f;
    [Tooltip("Segundos que tarda en salir del suelo y en hundirse al acabar.")]
    [SerializeField, Min(0.05f)] private float segundosDeEntrarYSalir = 0.4f;

    private Vector3 _salida, _escala, _desde, _hasta, _sombraBase, _antes, _velocidad;
    private Renderer[] _renderers;
    private bool _visible = true, _golpeado = true, _sonado = true;
    private int _salto = -1;
    private double _ultimo = -1d;

    private void Awake()
    {
        _salida = transform.position;
        _escala = transform.localScale;
        _renderers = GetComponentsInChildren<Renderer>(true);
        if (sombra != null) _sombraBase = sombra.localScale;
        Ver(false);
    }

    private void Update()
    {
        if (reloj == null || !reloj.IsRunning || caidas.Length == 0) return;
        double ahora = reloj.SongTime;
        // La canción ha vuelto atrás (reintento): desde el principio.
        if (ahora < _ultimo - 0.05d) Reiniciar();
        _ultimo = ahora;
        float t = (float)ahora;
        SeguirAlJugador();

        float ultima = caidas[caidas.Length - 1];
        if (t < segundoDeAparecer || t >= ultima + segundosDeEntrarYSalir) { Reiniciar(); Ver(false); return; }
        Ver(true);

        // Salto en curso: el primero cuya caída aún no ha llegado.
        int k = 0;
        while (k < caidas.Length && t >= caidas[k]) k++;
        while (_salto < k)
        {
            // Acaba el salto anterior (golpe en su sitio) y empieza el siguiente.
            if (_salto >= 0 && !_golpeado) Golpear(_hasta);
            _salto++;
            _desde = _salto == 0 ? _salida : _hasta;
            if (_salto < caidas.Length)
                _hasta = _salto < sitios.Length ? new Vector3(sitios[_salto].x, _salida.y, sitios[_salto].z)
                                                : Objetivo(_desde, caidas[_salto] - Inicio(_salto));
            _golpeado = false;
            _sonado = false;
        }

        if (k >= caidas.Length)
        {
            // Tras el último golpe se hunde en el suelo.
            if (!_golpeado) Golpear(_hasta);
            float u = Mathf.Clamp01((t - ultima) / segundosDeEntrarYSalir);
            transform.position = _hasta;
            transform.localScale = Vector3.Scale(_escala, new Vector3(1f + 0.2f * u, 1f - u, 1f));
            Sombra(false, 0f);
            return;
        }

        float inicio = Inicio(k);
        if (t < inicio)
        {
            // Esperando en el fondo: sube del suelo al aparecer.
            float u = Mathf.Clamp01((t - segundoDeAparecer) / segundosDeEntrarYSalir);
            transform.position = _salida;
            transform.localScale = Vector3.Scale(_escala, new Vector3(1f, u, 1f));
            Sombra(false, 0f);
            return;
        }
        float duracion = Mathf.Max(0.05f, caidas[k] - inicio);
        float v = Mathf.Clamp01((t - inicio) / duracion);
        transform.position = Vector3.Lerp(_desde, _hasta, v) + Vector3.up * (altura * 4f * v * (1f - v));
        // Se estira al saltar y se aplasta al caer.
        float estira = 1f + 0.15f * Mathf.Sin(v * Mathf.PI);
        transform.localScale = Vector3.Scale(_escala, new Vector3(2f - estira, estira, 1f));
        Sombra(true, v);
        if (!_sonado && golpe != null && caidas[k] - t <= reloj.OutputLatency)
        {
            _sonado = true;
            AudioService.Instance?.PlaySFX(golpe, volumen);
        }
    }

    /// <summary>Segundo en el que empieza el salto k: la caída anterior (el primero, un tiempo antes de caer).</summary>
    private float Inicio(int k)
    {
        if (k > 0) return caidas[k - 1];
        float tiempo = caidas.Length > 1 ? caidas[1] - caidas[0] : 0.4f;
        return caidas[0] - tiempo;
    }

    /// <summary>Donde va a estar el jugador al caer, sin pasar de un paso ni salirse del corredor.</summary>
    private Vector3 Objetivo(Vector3 desde, float duracion)
    {
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo == null) return desde;
        Vector3 objetivo = cuerpo.transform.position + _velocidad * (duracion * anticipacion);
        objetivo.x = Mathf.Clamp(objetivo.x, _salida.x - medioAncho, _salida.x + medioAncho);
        objetivo.y = _salida.y;
        Vector3 paso = objetivo - desde;
        paso.y = 0f;
        if (paso.magnitude > pasoMaximo) paso = paso.normalized * pasoMaximo;
        return new Vector3(desde.x + paso.x, _salida.y, desde.z + paso.z);
    }

    private void Golpear(Vector3 donde)
    {
        _golpeado = true;
        if (dano > 0f) DanoEnZona.GolpearEnCilindro(donde, radio, 2.2f, 0.3f, dano, 5f);
    }

    private void Sombra(bool ver, float crece)
    {
        if (sombra == null) return;
        if (sombra.gameObject.activeSelf != ver) sombra.gameObject.SetActive(ver);
        if (!ver) return;
        sombra.SetPositionAndRotation(_hasta + Vector3.up * 0.03f, Quaternion.identity);
        sombra.localScale = _sombraBase * Mathf.Lerp(0.4f, 1f, crece);
    }

    private void Reiniciar()
    {
        _salto = -1;
        _golpeado = true;
        _sonado = true;
        transform.position = _salida;
        transform.localScale = _escala;
        _hasta = _salida;
    }

    private void SeguirAlJugador()
    {
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo == null || Time.deltaTime <= 0f) return;
        Vector3 p = cuerpo.transform.position;
        Vector3 v = (p - _antes) / Time.deltaTime;
        v.y = 0f;
        if (v.sqrMagnitude > 400f) v = Vector3.zero;
        _velocidad = Vector3.Lerp(_velocidad, v, 1f - Mathf.Exp(-10f * Time.deltaTime));
        _antes = p;
    }

    private void Ver(bool si)
    {
        if (si == _visible) return;
        _visible = si;
        for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = si;
        if (!si) Sombra(false, 0f);
    }
}
