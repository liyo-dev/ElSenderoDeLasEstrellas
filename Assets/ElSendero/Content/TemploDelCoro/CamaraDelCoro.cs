using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// Cámara del Templo del Coro. Dos planos sobre la misma cámara virtual:
/// <list type="bullet">
/// <item><b>Presentación</b>: mientras el Tenor afina, un plano alto y fijo con la sala entera; en
/// cada golpe de <see cref="cortes"/> (do, re, mi) corta a otro plano.</item>
/// <item><b>Huida</b>: detrás y por encima del jugador, mirando corredor adelante, alto para ver lo
/// que cae y lo bastante atrás para que en la parte de abajo del cuadro se vea la mancha de la
/// sombra lamiendo los talones. Sigue el eje del corredor (el «adelante» de este objeto), así que
/// el stick hacia arriba es correr.</item>
/// <item><b>Planos de escena</b>: en tramos de la canción (una frase sin peligros, el golpe final), un
/// plano que va de un sitio a otro despacio; al acabar vuelve sola a la huida.</item>
/// <item><b>Despedida</b>: al ganar, una vuelta alrededor del grupo subiendo y alejándose.</item>
/// </list>
/// Es cámara de juego (no de cine): no oculta el HUD. Mueve la cámara principal con el mando pedido a
/// <see cref="CameraDirectorService"/>, como la cámara lateral del Templo. La gobierna <see cref="TramoDelCoro"/>.
/// Ver INC-1393.
/// </summary>
[DisallowMultipleComponent]
public sealed class CamaraDelCoro : MonoBehaviour
{
    [Header("Presentación")]
    [Tooltip("Sitio y giro del plano de la presentación.")]
    [SerializeField] private Transform plano;
    [SerializeField, Range(20f, 80f)] private float campoDelPlano = 60f;
    [Tooltip("Golpes que cortan a otro plano durante la presentación (do, re, mi): uno por golpe, en orden.")]
    [SerializeField] private ScoreCueTrigger cortes;
    [Tooltip("Planos a los que se corta en cada golpe de «cortes», en orden.")]
    [SerializeField] private Transform[] planosDeCorte = System.Array.Empty<Transform>();

    [Header("Huida")]
    [Tooltip("Dónde va la cámara respecto al jugador, en ejes del corredor (X a un lado, Y arriba, Z adelante).")]
    [SerializeField] private Vector3 desdeElJugador = new Vector3(0f, 7f, -7.5f);
    [Tooltip("Metros por delante del jugador a los que mira.")]
    [SerializeField, Min(0f)] private float mirarAdelante = 5f;
    [Tooltip("Cuánto sigue al jugador de lado (0 = fija en el centro del corredor, 1 = pegada a él).")]
    [SerializeField, Range(0f, 1f)] private float seguirDeLado = 0.4f;
    [SerializeField, Range(20f, 80f)] private float campoDeLaHuida = 58f;
    [Tooltip("Rapidez con la que alcanza su sitio (más alto, más pegada).")]
    [SerializeField, Min(0.1f)] private float seguimiento = 6f;
    [Tooltip("Capas que tapan la cámara (muros): si se meten entre el jugador y la cámara, se acerca.")]
    [SerializeField] private LayerMask capasQueTapan = 1;

    [System.Serializable]
    public struct PlanoDeEscena
    {
        [Tooltip("Segundos de la canción en los que dura (X: desde; Y: hasta).")]
        public Vector2 segundos;
        [Tooltip("Sitio y giro al empezar.")]
        public Transform desde;
        [Tooltip("Sitio y giro al acabar (vacío: se queda en el de empezar).")]
        public Transform hasta;
        [Range(20f, 80f)] public float campo;
    }

    [Header("Planos de escena")]
    [SerializeField] private PlanoDeEscena[] planosDeEscena = System.Array.Empty<PlanoDeEscena>();

    [Header("Despedida (al ganar)")]
    [SerializeField, Min(0.5f)] private float segundosDeLaDespedida = 4.5f;
    [Tooltip("Grados que gira alrededor del grupo.")]
    [SerializeField] private float vueltaDeLaDespedida = 200f;
    [Tooltip("Metros que sube durante la vuelta.")]
    [SerializeField] private float subidaDeLaDespedida = 8f;
    [Tooltip("El Tenor al que acaba mirando la despedida (vacío: acaba mirando al grupo).")]
    [SerializeField] private Transform tenorDeLaDespedida;
    [Tooltip("Dónde queda el Tenor al acabar, de izquierda (0) a derecha (1) de la pantalla: a un lado de la pregunta de repetir.")]
    [SerializeField, Range(0.5f, 0.95f)] private float tenorEnPantalla = 0.82f;

    private enum Modo { Ninguno, Presentacion, Huida, Despedida }

    private System.Action _alAcabarLaDespedida;
    private float _despedidaDesde, _angulo0, _radio0, _altura0, _vuelta;
    private Vector3 _centro;

    private Camera _camara;
    private TempoClock _reloj;
    private Modo _modo;
    private float _hasta;
    private bool _tieneLaCamara;
    private readonly RaycastHit[] _golpes = new RaycastHit[8];

    private void OnEnable()
    {
        if (cortes != null) cortes.Hit += AlCorte;
    }

    private void AlCorte(int indice, float segundo)
    {
        if (_modo != Modo.Presentacion || _camara == null || indice >= planosDeCorte.Length) return;
        var plano = planosDeCorte[indice];
        if (plano != null) _camara.transform.SetPositionAndRotation(plano.position, plano.rotation);
    }

    /// <summary>Plano de la presentación hasta ese segundo de la canción; luego pasa sola a la huida.</summary>
    public void Presentar(TempoClock reloj, float hastaSegundo)
    {
        _reloj = reloj;
        _hasta = hastaSegundo;
        if (plano == null || !Tomar()) return;
        _modo = Modo.Presentacion;
        _camara.fieldOfView = campoDelPlano;
        _camara.transform.SetPositionAndRotation(plano.position, plano.rotation);
    }

    /// <summary>Cámara de la huida. <paramref name="deGolpe"/>: sin acercarse poco a poco (con la pantalla tapada).</summary>
    public void Huir(bool deGolpe)
    {
        if (!Tomar()) return;
        _modo = Modo.Huida;
        _camara.fieldOfView = campoDeLaHuida;
        if (deGolpe && ControlDelJugador.CuerpoDelJugador != null)
        {
            SitioDeHuida(ControlDelJugador.CuerpoDelJugador.transform.position, out var pos, out var rot);
            _camara.transform.SetPositionAndRotation(pos, rot);
        }
    }

    /// <summary>
    /// Vuelta de despedida alrededor del grupo, desde donde esté la cámara: gira, sube y se aleja.
    /// Al acabar avisa (la cámara se queda ahí hasta <see cref="Soltar"/>).
    /// </summary>
    public void Despedir(System.Action alAcabar)
    {
        if (!Tomar()) { alAcabar?.Invoke(); return; }
        var jugador = ControlDelJugador.CuerpoDelJugador;
        _centro = jugador != null ? jugador.transform.position : _camara.transform.position + _camara.transform.forward * 8f;
        Vector3 d = _camara.transform.position - _centro;
        _altura0 = d.y;
        d.y = 0f;
        _radio0 = Mathf.Max(4f, d.magnitude);
        _angulo0 = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        _vuelta = vueltaDeLaDespedida;
        if (tenorDeLaDespedida != null)
        {
            // Acaba detrás del grupo, de espaldas al Tenor: la vuelta más parecida a la pedida que deja ahí.
            Vector3 desdeElTenor = _centro - tenorDeLaDespedida.position;
            desdeElTenor.y = 0f;
            float anguloFinal = Mathf.Atan2(desdeElTenor.x, desdeElTenor.z) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(_angulo0, anguloFinal);
            _vuelta = delta + 360f * Mathf.Round((vueltaDeLaDespedida - delta) / 360f);
        }
        _despedidaDesde = Time.time;
        _alAcabarLaDespedida = alAcabar;
        _modo = Modo.Despedida;
    }

    private void Despedida()
    {
        float u = Mathf.Clamp01((Time.time - _despedidaDesde) / segundosDeLaDespedida);
        float s = Mathf.SmoothStep(0f, 1f, u);
        float angulo = (_angulo0 + _vuelta * s) * Mathf.Deg2Rad;
        float radio = Mathf.Lerp(_radio0, _radio0 * 2.2f, s);
        Vector3 pos = _centro + new Vector3(Mathf.Sin(angulo) * radio, Mathf.Lerp(_altura0, _altura0 + subidaDeLaDespedida, s), Mathf.Cos(angulo) * radio);
        Quaternion giro = Quaternion.LookRotation(_centro + Vector3.up * 1.5f - pos, Vector3.up);
        if (tenorDeLaDespedida != null)
        {
            // Al final, el Tenor a un lado del cuadro: se mira a él y se gira a la izquierda lo justo.
            float medioCampo = Mathf.Atan(Mathf.Tan(_camara.fieldOfView * 0.5f * Mathf.Deg2Rad) * _camara.aspect);
            float hacia = Mathf.Atan((tenorEnPantalla * 2f - 1f) * Mathf.Tan(medioCampo)) * Mathf.Rad2Deg;
            Quaternion alTenor = Quaternion.AngleAxis(-hacia, Vector3.up) * Quaternion.LookRotation(tenorDeLaDespedida.position - pos, Vector3.up);
            giro = Quaternion.Slerp(giro, alTenor, s * s);
        }
        _camara.transform.SetPositionAndRotation(pos, giro);
        if (u < 1f || _alAcabarLaDespedida == null) return;
        var alAcabar = _alAcabarLaDespedida;
        _alAcabarLaDespedida = null;
        alAcabar();
    }

    /// <summary>Devuelve la cámara al juego.</summary>
    public void Soltar()
    {
        _modo = Modo.Ninguno;
        _alAcabarLaDespedida = null;
        if (!_tieneLaCamara) return;
        _tieneLaCamara = false;
        CameraDirectorService.Release(this);
    }

    private bool Tomar()
    {
        if (_tieneLaCamara && _camara != null) return true;
        _camara = Camera.main;
        if (_camara == null) return false;
        CameraDirectorService.Claim(this, esCinematica: false);
        _tieneLaCamara = true;
        return true;
    }

    private void LateUpdate()
    {
        if (_modo == Modo.Ninguno || _camara == null) return;
        // Si otro sistema (una cinemática) se ha quedado la cámara, no se pelea con él.
        if (!CameraDirectorService.EsDueno(this)) return;
        if (_modo == Modo.Despedida) { Despedida(); return; }

        if (_modo == Modo.Presentacion)
        {
            float t = _reloj != null ? (float)_reloj.SongTime : _hasta;
            if (t < _hasta) return;
            _modo = Modo.Huida;
            _camara.fieldOfView = campoDeLaHuida;
        }

        var reloj = _reloj != null ? _reloj : TempoClock.Current;
        if (reloj != null && reloj.IsRunning && PlanoEnCurso((float)reloj.SongTime)) return;
        if (_enPlano)
        {
            _enPlano = false;
            _camara.fieldOfView = campoDeLaHuida;
        }

        var jugador = ControlDelJugador.CuerpoDelJugador;
        if (jugador == null) return;
        SitioDeHuida(jugador.transform.position, out var sitio, out var giro);
        float k = 1f - Mathf.Exp(-seguimiento * Time.deltaTime);
        Vector3 pos = Vector3.Lerp(_camara.transform.position, sitio, k);
        // Al pasar por la boca del Tenor, el muro de encima quedaría entre el jugador y la cámara.
        pos = SinMuroDelante(jugador.transform.position + Vector3.up * 1.5f, pos);
        _camara.transform.SetPositionAndRotation(pos, Quaternion.Slerp(_camara.transform.rotation, giro, k));
    }

    private bool _enPlano;

    /// <summary>Si toca un plano de escena, lo pone (de su sitio de empezar al de acabar, suavizado).</summary>
    private bool PlanoEnCurso(float segundo)
    {
        for (int i = 0; i < planosDeEscena.Length; i++)
        {
            var p = planosDeEscena[i];
            if (p.desde == null || segundo < p.segundos.x || segundo >= p.segundos.y) continue;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.segundos.x, p.segundos.y, segundo));
            var hasta = p.hasta != null ? p.hasta : p.desde;
            _camara.transform.SetPositionAndRotation(Vector3.Lerp(p.desde.position, hasta.position, u),
                                                     Quaternion.Slerp(p.desde.rotation, hasta.rotation, u));
            if (p.campo > 0f) _camara.fieldOfView = p.campo;
            _enPlano = true;
            return true;
        }
        return false;
    }

    /// <summary>Acerca la cámara al jugador si un muro se mete entre los dos (los personajes no cuentan).</summary>
    private Vector3 SinMuroDelante(Vector3 cabeza, Vector3 camara)
    {
        Vector3 hacia = camara - cabeza;
        float largo = hacia.magnitude;
        if (largo < 0.01f) return camara;
        hacia /= largo;
        int n = Physics.SphereCastNonAlloc(cabeza, 0.25f, hacia, _golpes, largo, capasQueTapan, QueryTriggerInteraction.Ignore);
        float cerca = largo;
        for (int i = 0; i < n; i++)
        {
            var c = _golpes[i].collider;
            // Los personajes llevan cápsula o cuerpo físico; los muros del templo, no.
            if (c == null || c.attachedRigidbody != null || c is CapsuleCollider || c is CharacterController) continue;
            if (_golpes[i].distance > 0f && _golpes[i].distance < cerca) cerca = _golpes[i].distance;
        }
        return cerca < largo ? cabeza + hacia * Mathf.Max(0.5f, cerca - 0.2f) : camara;
    }

    /// <summary>Sitio y giro de la cámara de la huida para un jugador en <paramref name="pies"/>.</summary>
    private void SitioDeHuida(Vector3 pies, out Vector3 sitio, out Quaternion giro)
    {
        // Ejes del corredor: el «adelante» de este objeto, en horizontal.
        Vector3 adelante = transform.forward;
        adelante.y = 0f;
        adelante = adelante.sqrMagnitude > 0.001f ? adelante.normalized : Vector3.forward;
        Vector3 lado = Vector3.Cross(Vector3.up, adelante);

        Vector3 eje = transform.position;
        float deLado = Vector3.Dot(pies - eje, lado) * seguirDeLado;
        float alFrente = Vector3.Dot(pies - eje, adelante);
        Vector3 centro = eje + adelante * alFrente + lado * deLado;
        centro.y = pies.y;

        sitio = centro + lado * desdeElJugador.x + Vector3.up * desdeElJugador.y + adelante * desdeElJugador.z;
        Vector3 mira = centro + adelante * mirarAdelante;
        giro = Quaternion.LookRotation(mira - sitio, Vector3.up);
    }

    private void OnDisable()
    {
        if (cortes != null) cortes.Hit -= AlCorte;
        Soltar();
    }
}
