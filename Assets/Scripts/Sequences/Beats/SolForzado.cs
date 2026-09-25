using UnityEngine;

/// Mantiene la luz del sol en una rotación dada por encima del ciclo día/noche mientras hace
/// falta. Corre en LateUpdate, después de las transiciones del ciclo (que son corrutinas), así
/// que manda sin tener que pararlas; al soltarse se funde con lo que el ciclo tenga puesto.
///
/// ── En cuadro (INC-411) ───────────────────────────────────────────────────────────────────────
/// En prologo17 el sol del río estaba «al fondo» en el mundo — a 7°, en el lado contrario al eje —
/// pero no en el PLANO: a 7° lo tapaban las montañas del fondo, y la conversación no enseñaba ni
/// un trozo de sol. El modo en cuadro lo coloca respecto a la CÁMARA que está rodando: en un punto
/// del encuadre, cerca del borde de arriba, y lo va BAJANDO por el cuadro en `puesta` segundos
/// hasta que las montañas lo tapan. Es una puesta de sol que se ve pasar mientras hablan.
///
/// En cada corte el sol se recoloca para el plano nuevo (como se hace en cine: la luz se hace
/// trampa plano a plano), pero no salta de altura: la puesta sigue su reloj.
[DefaultExecutionOrder(900)]   // antes que SolYLunaEnElCielo (1000): ver INC-419 allí
public class SolForzado : MonoBehaviour
{
    /// Opacidad del DISCO del sol mientras hay un sol forzado soltándose: se apaga con él, en vez
    /// de verse cómo sube hasta donde lo tenga el ciclo. La lee SolYLunaEnElCielo.
    public static float OpacidadDelSol { get; private set; } = 1f;

    /// Hacia dónde está el DISCO del sol cuando va en cuadro (null si no hay sol en cuadro). La
    /// luz tarda `segundos` en girar hasta su sitio (si girara de golpe, las sombras de todo el
    /// plano saltarían); el disco, no: aparece ya en su sitio y se funde. Antes el disco seguía a
    /// la luz y se le veía cruzar el cielo corriendo antes de empezar a ponerse — «algo pasa
    /// corriendo por el mismo sitio, creo que es otro sol» (prologo20, INC-431).
    public static Vector3? DireccionDelDisco { get; private set; }

    private Quaternion _objetivo;
    private float _peso;
    private float _pesoDestino;
    private float _velocidad = 1f;

    // Modo en cuadro.
    private bool _enCuadro;
    private float _posicionX = 0.72f;
    private float _alturaDeSalida = 0.84f;
    private float _alturaDePuesta = 0.42f;
    private float _puesta = 20f;
    private float _inicio;
    private Camera _ultimaCamara;
    private float _siguienteLog;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { OpacidadDelSol = 1f; DireccionDelDisco = null; }
#endif

    public void Colocar(Quaternion rotacion, float segundos)
    {
        _enCuadro = false;
        DireccionDelDisco = null;
        _objetivo = rotacion;
        Subir(segundos);
    }

    /// Sol en el plano: a `posicionX` del ancho del encuadre (0 izquierda, 1 derecha), saliendo a
    /// `alturaDeSalida` del alto del encuadre y bajando hasta `alturaDePuesta` en `puesta` segundos.
    public void ColocarEnCuadro(float posicionX, float alturaDeSalida, float alturaDePuesta, float puesta, float segundos)
    {
        _enCuadro = true;
        _posicionX = Mathf.Clamp01(posicionX);
        _alturaDeSalida = Mathf.Clamp01(alturaDeSalida);
        _alturaDePuesta = Mathf.Clamp01(alturaDePuesta);
        _puesta = Mathf.Max(0.1f, puesta);
        _inicio = Time.time;
        _ultimaCamara = null;
        _objetivo = transform.rotation;
        Subir(segundos);
    }

    private void Subir(float segundos)
    {
        _pesoDestino = 1f;
        _velocidad = segundos > 0.01f ? 1f / segundos : 1000f;
        OpacidadDelSol = 1f;
        enabled = true;
    }

    public void Soltar(float segundos)
    {
        _pesoDestino = 0f;
        _velocidad = segundos > 0.01f ? 1f / segundos : 1000f;
        if (segundos <= 0.01f) { _peso = 0f; OpacidadDelSol = 1f; DireccionDelDisco = null; Destroy(this); }
    }

    void OnDestroy() { OpacidadDelSol = 1f; DireccionDelDisco = null; }

    void LateUpdate()
    {
        _peso = Mathf.MoveTowards(_peso, _pesoDestino, _velocidad * Time.deltaTime);
        if (_peso <= 0f && _pesoDestino <= 0f) { OpacidadDelSol = 1f; DireccionDelDisco = null; Destroy(this); return; }

        if (_enCuadro && ObjetivoEnCuadro(out var q)) _objetivo = q;

        transform.rotation = Quaternion.Slerp(transform.rotation, _objetivo, _peso);

        if (_enCuadro)
        {
            // El disco va SIEMPRE donde tiene que estar; lo que sube y baja es su opacidad.
            DireccionDelDisco = -(_objetivo * Vector3.forward);
            float k = Mathf.SmoothStep(0f, 1f, _peso);
            OpacidadDelSol = _pesoDestino > 0f ? k : k * k;
        }
        else
        {
            // Al soltarse, el disco se va con el peso: el sol no «sale» hacia donde lo tenga el ciclo.
            OpacidadDelSol = _pesoDestino > 0f ? 1f : _peso * _peso;
        }
    }

    /// El sol se coloca en un punto DEL ENCUADRE, no del mundo (INC-415).
    ///
    /// La versión anterior buscaba la cresta de las montañas del fondo para ponerlo justo encima.
    /// Las montañas son decorado sin collider, y su caja (lo único que se puede medir sin leer la
    /// malla) llega al pico más alto: en prologo18 la cresta salía a 12° y el borde de arriba del
    /// cuadro estaba a 10°, así que el sol arrancaba ya escondido. Aquí no se mide nada: el sol
    /// sale cerca del borde de arriba, donde en un plano de conversación siempre hay cielo, y baja
    /// por el encuadre. Donde se cruce con el perfil de las montañas, la profundidad lo esconde
    /// detrás (el disco está más lejos que ellas, INC-413). Eso ES la puesta de sol.
    private bool ObjetivoEnCuadro(out Quaternion rotacion)
    {
        rotacion = _objetivo;
        var cam = SolYLunaEnElCielo.CamaraActual();
        if (cam == null) return false;

        float k = Mathf.Clamp01((Time.time - _inicio) / _puesta);
        k = k * k * (3f - 2f * k);
        float y = Mathf.Lerp(_alturaDeSalida, _alturaDePuesta, k);

        Vector3 d = cam.ViewportPointToRay(new Vector3(_posicionX, y, 0f)).direction.normalized;

        // Nunca más de 4° bajo el horizonte: más abajo la luz entraría desde debajo del suelo.
        float elev = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
        if (elev < -4f)
        {
            Vector3 plano = new Vector3(d.x, 0f, d.z);
            if (plano.sqrMagnitude < 0.0001f) return false;
            plano.Normalize();
            Vector3 eje = Vector3.Cross(Vector3.up, plano);
            d = Quaternion.AngleAxis(4f, eje) * plano;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cam != _ultimaCamara || Time.time >= _siguienteLog)
        {
            _ultimaCamara = cam;
            _siguienteLog = Time.time + 2f;
            Debug.Log($"[SolForzado] Sol en cuadro ('{cam.name}'): {elev:F0}° sobre el horizonte, al {y * 100f:F0} % del alto del cuadro.");
        }
#endif
        rotacion = Quaternion.LookRotation(-d, Vector3.up);
        return true;
    }
}
