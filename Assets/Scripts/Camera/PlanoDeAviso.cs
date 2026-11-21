using Unity.Cinemachine;
using UnityEngine;

/// Plano breve de juego que enseña a este personaje junto al jugador (por encima del hombro del
/// jugador, mirando hacia él): para que se vea venir un ataque grande que se prepara lejos o fuera
/// de cuadro, o cómo cae. No quita el mando: el jugador sigue moviéndose y el plano dura poco. Es
/// una cámara de juego (no de cine): no oculta el HUD ni el marcador. Si una cinemática tiene la
/// cámara, no hace nada. Ver INC-1333.
[DisallowMultipleComponent]
public sealed class PlanoDeAviso : MonoBehaviour
{
    [Tooltip("Metros detrás del jugador (crece solo si el personaje está lejos, para que quepan los dos).")]
    [SerializeField, Min(1f)] private float distancia = 5.5f;
    [SerializeField, Min(0f)] private float altura = 2.4f;
    [Tooltip("Metros hacia un lado: el jugador queda a un lado del cuadro y el personaje al otro.")]
    [SerializeField] private float lado = 1.4f;
    [Tooltip("Altura del punto al que mira, sobre el suelo del personaje.")]
    [SerializeField] private float alturaDeMira = 1.4f;
    [Tooltip("Cuánto se va la mira del jugador al personaje (0 = jugador, 1 = personaje).")]
    [SerializeField, Range(0f, 1f)] private float pesoDelPersonaje = 0.7f;
    [SerializeField, Range(20f, 80f)] private float campoDeVision = 50f;
    [Tooltip("Segundos de mezcla al entrar y al salir del plano.")]
    [SerializeField, Min(0f)] private float mezcla = 0.35f;

    private CinemachineCamera _vcam;
    private CinemachineBrain _brain;
    private float _mezclaOriginal;
    private float _hasta = -1f;
    private float _devolverMezclaEn = -1f;
    private bool _activo;

    /// El plano está en pantalla.
    public bool Activo => _activo;

    void Awake() => enabled = false;

    /// Enseña al personaje durante estos segundos (si ya lo enseña, alarga el plano).
    public void Ensenar(float segundos)
    {
        if (segundos <= 0f || !gameObject.activeInHierarchy) return;
        if (!_activo)
        {
            if (CameraDirectorService.HayCine || (CameraDirectorService.Ocupada && !CameraDirectorService.EsDueno(this))) return;
            if (!Empezar()) return;
        }
        _hasta = Mathf.Max(_hasta, Time.time + segundos);
        enabled = true;
    }

    /// Corta el plano ya.
    public void Soltar()
    {
        if (!_activo) return;
        _activo = false;
        _hasta = -1f;
        if (_vcam != null) _vcam.Priority.Value = 0;
        CameraDirectorService.Release(this);
        _devolverMezclaEn = Time.time + mezcla + 0.05f;
    }

    private bool Empezar()
    {
        if (!PlayerService.TryGetPlayer(out var jugador, false) || jugador == null) return false;
        var cam = Camera.main;
        if (cam == null) return false;
        _brain = cam.GetComponent<CinemachineBrain>();
        if (_vcam == null)
        {
            var go = new GameObject("PlanoDeAviso_" + name);
            _vcam = go.AddComponent<CinemachineCamera>();
            _vcam.Lens.NearClipPlane = 0.1f;
            _vcam.Lens.FarClipPlane = 1000f;
        }
        _vcam.Lens.FieldOfView = campoDeVision;
        Colocar(jugador.transform);

        var juego = ServiceLocator.Get<CinemachineCamera>(logIfMissing: false);
        if (_brain != null && _devolverMezclaEn < 0f)
        {
            _mezclaOriginal = _brain.DefaultBlend.Time;
            _brain.DefaultBlend.Time = mezcla;
        }
        _devolverMezclaEn = -1f;
        _vcam.Priority.Value = (juego != null ? juego.Priority.Value : 10) + 15;
        CameraDirectorService.Claim(this, esCinematica: false);
        _activo = true;
        return true;
    }

    void LateUpdate()
    {
        if (_activo)
        {
            if (Time.time >= _hasta || !CameraDirectorService.EsDueno(this)) Soltar();
            else if (PlayerService.TryGetPlayer(out var jugador, false) && jugador != null) Colocar(jugador.transform);
        }
        if (!_activo && _devolverMezclaEn > 0f && Time.time >= _devolverMezclaEn)
        {
            if (_brain != null) _brain.DefaultBlend.Time = _mezclaOriginal;
            _devolverMezclaEn = -1f;
        }
        if (!_activo && _devolverMezclaEn < 0f) enabled = false;
    }

    private void Colocar(Transform jugador)
    {
        Vector3 desde = jugador.position, hacia = transform.position;
        Vector3 dir = hacia - desde;
        dir.y = 0f;
        float separacion = dir.magnitude;
        dir = separacion > 0.01f ? dir / separacion : jugador.forward;
        Vector3 derecha = Vector3.Cross(Vector3.up, dir);
        float d = Mathf.Max(distancia, separacion * 0.45f);
        Vector3 pos = desde - dir * d + Vector3.up * (altura + separacion * 0.06f) + derecha * lado;
        Vector3 mira = Vector3.Lerp(desde, hacia, pesoDelPersonaje) + Vector3.up * alturaDeMira;
        _vcam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira - pos, Vector3.up));
    }

    void OnDisable()
    {
        if (!_activo && _devolverMezclaEn < 0f) return;
        Soltar();
        if (_brain != null) _brain.DefaultBlend.Time = _mezclaOriginal;
        _devolverMezclaEn = -1f;
    }

    void OnDestroy()
    {
        if (_vcam != null) Destroy(_vcam.gameObject);
    }
}
