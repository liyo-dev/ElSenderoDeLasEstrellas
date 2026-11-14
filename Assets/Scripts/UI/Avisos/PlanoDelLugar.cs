using UnityEngine;

/// <summary>
/// Plano de cámara que acompaña a la presentación de un lugar nuevo: la cámara sube primero en
/// vertical (sale de detrás de tejados y aleros), se abre en picado sobre el lugar, gira despacio
/// alrededor mientras se lee el nombre y baja a la espalda del jugador para devolverle el mando.
///
/// No conoce lugares, escenas ni zonas: recibe una caja (la del lugar) y el jugador, y todo lo que
/// hace es función del tiempo (<see cref="Evaluar"/>), como la cinta que acompaña. Mientras dura,
/// tiene la cámara (<see cref="CameraDirectorService"/>), bloquea al jugador
/// (<see cref="PlayerLockService"/>) y esconde lo de pantalla (<see cref="OcultablesDeCinematica"/>)
/// con la clave de quien lo pide. Si otro sistema le quita la cámara, deja de moverla.
/// </summary>
public sealed class PlanoDelLugar
{
    // Tiempos, en segundos.
    const float Subida = 1.5f;
    const float Bajada = 1.2f;
    const float DuracionMinima = 3.2f;

    // Encuadre.
    const float RadioMinimo = 14f;
    const float RadioMaximo = 30f;
    const float AlejamientoDelJugador = 30f;
    const float HaciaElCentro = 0.6f;
    const float AlturaDelFoco = 1.5f;
    const float AlturaPorRadio = 0.8f;
    const float DistanciaPorRadio = 0.85f;
    const float GiroDuranteElPlano = 10f;
    const float AcercamientoDuranteElPlano = 0.08f;

    // Vuelta: aproximadamente donde se pone la cámara de juego (el resto lo suaviza ella al soltarla).
    const float DistanciaDeVuelta = 4.5f;
    const float AlturaDeVuelta = 2.4f;

    // Comprobaciones del sitio de la cámara.
    const float RadioDeLaLente = 0.6f;
    const float SubidaPorIntento = 5f;
    const float GiroPorIntento = 30f;
    const int Intentos = 6;

    readonly object _quien;
    Camera _camara;
    Vector3 _desde;
    Quaternion _rotacionDesde;
    Vector3 _foco;
    Vector3 _alto;
    Vector3 _vuelta;
    Vector3 _focoDeVuelta;
    float _duracion;
    bool _activo;

    public PlanoDelLugar(object quien) => _quien = quien;

    /// True mientras el plano tiene la cámara.
    public bool Activo => _activo;

    /// <summary>
    /// Empieza el plano sobre <paramref name="lugar"/>, con el jugador en <paramref name="jugador"/>.
    /// No hace nada (y devuelve false) si no hay cámara o si otro sistema ya la tiene.
    /// </summary>
    /// <param name="duracion">Lo que dura entero (lo que dura la cinta que acompaña).</param>
    public bool Empezar(Bounds lugar, Transform jugador, float duracion)
    {
        if (_activo) return true;
        if (jugador == null || CameraDirectorService.HasOwner) return false;
        _camara = Camera.main;
        if (_camara == null) return false;

        _desde = _camara.transform.position;
        _rotacionDesde = _camara.transform.rotation;
        _duracion = Mathf.Max(duracion, DuracionMinima);
        Encuadrar(lugar, jugador);

        _activo = true;
        CameraDirectorService.Claim(_quien);
        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Acquire(_quien);
        OcultablesDeCinematica.OcultarTodos(_quien);
        return true;
    }

    /// Coloca la cámara en el momento <paramref name="t"/> (segundos desde que empezó).
    public void Evaluar(float t)
    {
        if (!_activo) return;
        if (_camara == null || !CameraDirectorService.EsDueno(_quien)) { Soltar(); return; }

        Vector3 pos;
        Vector3 mira;
        float inicioDeLaBajada = _duracion - Bajada;

        if (t < Subida)
        {
            // Sube primero en vertical (punto de control encima de donde estaba) y luego se abre.
            float k = Suave(t / Subida);
            Vector3 control = new Vector3(_desde.x, _alto.y, _desde.z);
            pos = Bezier(_desde, control, _alto, k);
            Quaternion r = Quaternion.Slerp(_rotacionDesde, Quaternion.LookRotation(_foco - pos), Suave(Mathf.Clamp01(t / (Subida * 0.8f))));
            _camara.transform.SetPositionAndRotation(pos, r);
            return;
        }

        if (t < inicioDeLaBajada)
        {
            // Gira despacio alrededor del foco y se acerca un poco.
            float k = (t - Subida) / Mathf.Max(0.01f, inicioDeLaBajada - Subida);
            pos = Orbitar(k);
            mira = _foco;
        }
        else
        {
            // Baja a la espalda del jugador mirando hacia él.
            float k = Suave(Mathf.Clamp01((t - inicioDeLaBajada) / Bajada));
            Vector3 arriba = Orbitar(1f);
            Vector3 control = new Vector3(_vuelta.x, arriba.y, _vuelta.z);
            pos = Bezier(arriba, control, _vuelta, k);
            mira = Vector3.Lerp(_foco, _focoDeVuelta, k);
        }
        _camara.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira - pos));
    }

    /// Devuelve la cámara, el mando y lo de pantalla. Llamarlo de más no hace nada.
    public void Soltar()
    {
        if (!_activo) return;
        _activo = false;
        OcultablesDeCinematica.MostrarTodos(_quien);
        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Release(_quien);
        if (CameraDirectorService.EsDueno(_quien)) CameraDirectorService.Release(_quien);
    }

    // ────────────────────────────────────────────────────────────────────────

    // Foco entre el jugador y el centro del lugar (sin irse lejos en lugares grandes) y la cámara
    // en alto del lado en que ya miraba, para que la subida se lea como continuación del juego.
    void Encuadrar(Bounds lugar, Transform jugador)
    {
        Vector3 pj = jugador.position;
        Vector3 haciaElCentro = lugar.center - pj;
        haciaElCentro.y = 0f;
        haciaElCentro = Vector3.ClampMagnitude(haciaElCentro, AlejamientoDelJugador);
        _foco = pj + haciaElCentro * HaciaElCentro + Vector3.up * AlturaDelFoco;

        float radio = Mathf.Clamp(Mathf.Max(lugar.extents.x, lugar.extents.z) * 0.5f, RadioMinimo, RadioMaximo);

        Vector3 lado = _desde - _foco;
        lado.y = 0f;
        if (lado.sqrMagnitude < 0.01f) lado = -Vector3.ProjectOnPlane(_camara.transform.forward, Vector3.up);
        if (lado.sqrMagnitude < 0.01f) lado = -jugador.forward;
        lado.Normalize();

        _alto = BuscarSitioLibre(lado, radio);

        // Vuelta: detrás del jugador según mira la cámara de juego al acabar (la de antes).
        Vector3 atras = -Vector3.ProjectOnPlane(_rotacionDesde * Vector3.forward, Vector3.up);
        if (atras.sqrMagnitude < 0.01f) atras = -jugador.forward;
        atras.Normalize();
        _focoDeVuelta = pj + Vector3.up * AlturaDelFoco;
        _vuelta = _focoDeVuelta + atras * DistanciaDeVuelta + Vector3.up * (AlturaDeVuelta - AlturaDelFoco);
    }

    // Prueba el sitio ideal y, si choca con algo o no ve el foco, más alto y girando a los lados.
    Vector3 BuscarSitioLibre(Vector3 lado, float radio)
    {
        Vector3 primero = Vector3.zero;
        for (int i = 0; i < Intentos; i++)
        {
            float giro = (i == 0) ? 0f : GiroPorIntento * ((i + 1) / 2) * ((i % 2 == 0) ? -1f : 1f);
            Vector3 dir = Quaternion.AngleAxis(giro, Vector3.up) * lado;
            Vector3 p = _foco + dir * (radio * DistanciaPorRadio) + Vector3.up * (radio * AlturaPorRadio + SubidaPorIntento * (i / 2));
            if (i == 0) primero = p;
            if (Libre(p)) return p;
        }
        return primero + Vector3.up * SubidaPorIntento * Intentos;
    }

    bool Libre(Vector3 p)
    {
        if (Physics.CheckSphere(p, RadioDeLaLente, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        if (!Physics.Linecast(p, _foco, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        // Un personaje delante no tapa el lugar.
        return hit.collider.transform.root.GetComponent<NPCSimpleAnimator>() != null;
    }

    Vector3 Orbitar(float k)
    {
        Vector3 rel = _alto - _foco;
        rel = Quaternion.AngleAxis(GiroDuranteElPlano * k, Vector3.up) * rel;
        return _foco + rel * (1f - AcercamientoDuranteElPlano * k);
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static float Suave(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
