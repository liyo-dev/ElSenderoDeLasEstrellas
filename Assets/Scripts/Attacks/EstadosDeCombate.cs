using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Estados que la magia pone a un enemigo (INC-499): ralentizar, inmovilizar y atraer. Grimorio,
/// paso 3. Se añade solo, la primera vez que un hechizo le pone un estado, al objeto que lleva el
/// NavMeshAgent del enemigo (o al del Damageable si no tiene). Sirve igual para enemigos normales y
/// jefes; a los jefes les dura la mitad (<see cref="ResistenciaDeJefe"/>).
///
/// No toca la IA de cada enemigo: al principio del fotograma apunta dónde estaba y, cuando la IA,
/// el NavMeshAgent y la animación ya lo han movido, recorta ese avance horizontal (a la fracción de
/// la ralentización, o a cero si está inmovilizado) y le suma el tirón hacia el centro si lo están
/// atrayendo. Así da igual cómo se mueva cada enemigo (SetDestination, Move, cambios de speed en
/// sus corrutinas). La animación se ralentiza con Animator.speed, respetando los valores que ponga
/// la propia IA. Con el agente apagado (saltos y movimientos guionizados de los jefes) no interviene.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public class EstadosDeCombate : MonoBehaviour
{
    /// <summary>Fracción de la duración que les dura un estado a los jefes.</summary>
    public const float ResistenciaDeJefe = 0.5f;

    // Más que esto en un solo fotograma es un teletransporte de la IA: no se recorta.
    private const float MaxDesplazamientoPorFotograma = 3f;
    // Distancia al centro a la que se detiene el tirón de «atraer».
    private const float DistanciaMinimaAlCentro = 1f;

    private NavMeshAgent _agent;
    private Animator _animator;
    private bool _esJefe;

    private float _slowUntil, _slowFactor = 1f;
    private float _rootUntil;
    private float _pullUntil, _pullSpeed;
    private Vector3 _pullCenter;
    private float _pushUntil, _pushSpeed;
    private Vector3 _pushCenter;

    private Vector3 _frameStart;
    private bool _frameValid;

    private float _animBase = 1f;
    private float _animWritten = float.NaN;

    private readonly Dictionary<EstadoDeCombate, GameObject> _vfx = new Dictionary<EstadoDeCombate, GameObject>();

    public bool EstaRalentizado => Time.time < _slowUntil;
    public bool EstaInmovilizado => Time.time < _rootUntil;
    public bool EstaSiendoAtraido => Time.time < _pullUntil;
    public bool EstaSiendoEmpujado => Time.time < _pushUntil;

    /// <summary>
    /// Pone un estado al enemigo de ese Damageable. 'fuerza': para ralentizar, la fracción de
    /// velocidad que le queda (0,4 = va al 40 %); para atraer, metros por segundo hacia 'centro'.
    /// </summary>
    public static EstadosDeCombate Aplicar(Damageable objetivo, EstadoDeCombate estado, float duracion,
                                           float fuerza, Vector3 centro, GameObject vfx)
    {
        if (objetivo == null || !objetivo.IsAlive || estado == EstadoDeCombate.Ninguno || duracion <= 0f) return null;
        var estados = De(objetivo);
        estados.Poner(estado, duracion, fuerza, centro, vfx);
        return estados;
    }

    /// <summary>El componente de estados del enemigo al que pertenece 'parte' (lo crea si no hay).</summary>
    public static EstadosDeCombate De(Component parte)
    {
        var agent = parte.GetComponentInParent<NavMeshAgent>();
        if (agent == null) agent = parte.GetComponentInChildren<NavMeshAgent>();
        GameObject host = agent != null ? agent.gameObject : parte.gameObject;
        if (!host.TryGetComponent(out EstadosDeCombate estados))
            estados = host.AddComponent<EstadosDeCombate>();
        return estados;
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();

        int boss = LayerMask.NameToLayer("Boss");
        _esJefe = boss >= 0 && gameObject.layer == boss;
        if (!_esJefe && boss >= 0)
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c.gameObject.layer == boss) { _esJefe = true; break; }
    }

    private void Poner(EstadoDeCombate estado, float duracion, float fuerza, Vector3 centro, GameObject vfx)
    {
        if (_esJefe) duracion *= ResistenciaDeJefe;
        float hasta = Time.time + duracion;

        switch (estado)
        {
            case EstadoDeCombate.Ralentizar:
                float factor = Mathf.Clamp(fuerza, 0.05f, 1f);
                // Si ya estaba ralentizado se queda con la más fuerte.
                _slowFactor = EstaRalentizado ? Mathf.Min(_slowFactor, factor) : factor;
                _slowUntil = Mathf.Max(_slowUntil, hasta);
                break;
            case EstadoDeCombate.Inmovilizar:
                _rootUntil = Mathf.Max(_rootUntil, hasta);
                break;
            case EstadoDeCombate.Atraer:
                _pullCenter = centro;
                _pullSpeed = Mathf.Max(0f, fuerza);
                _pullUntil = Mathf.Max(_pullUntil, hasta);
                break;
            case EstadoDeCombate.Empujar:
                _pushCenter = centro;
                _pushSpeed = Mathf.Max(0f, fuerza);
                _pushUntil = Mathf.Max(_pushUntil, hasta);
                break;
        }

        if (vfx != null && (!_vfx.TryGetValue(estado, out var go) || go == null))
        {
            var inst = Instantiate(vfx, transform.position, Quaternion.identity, transform);
            _vfx[estado] = inst;
        }

        if (!enabled)
        {
            _frameValid = false; // el punto de partida se toma en el próximo Update
            enabled = true;
        }
    }

    private void Update()
    {
        _frameStart = transform.position;
        _frameValid = true;
    }

    private void LateUpdate()
    {
        bool slow = EstaRalentizado, root = EstaInmovilizado, pull = EstaSiendoAtraido, push = EstaSiendoEmpujado;

        if (!slow) ClearVfx(EstadoDeCombate.Ralentizar);
        if (!root) ClearVfx(EstadoDeCombate.Inmovilizar);
        if (!pull) ClearVfx(EstadoDeCombate.Atraer);
        if (!push) ClearVfx(EstadoDeCombate.Empujar);

        if (!slow && !root && !pull && !push)
        {
            RestoreAnimator();
            _frameValid = false;
            enabled = false;
            return;
        }

        bool puedeMoverse = _agent == null || (_agent.enabled && _agent.isOnNavMesh);
        if (_frameValid && puedeMoverse)
        {
            Vector3 now = transform.position;
            Vector3 delta = now - _frameStart;
            Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);

            if (horizontal.magnitude <= MaxDesplazamientoPorFotograma)
            {
                float f = root ? 0f : slow ? _slowFactor : 1f;
                Vector3 target = _frameStart + horizontal * f + Vector3.up * delta.y;

                if (pull && !root)
                {
                    Vector3 to = _pullCenter - target;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist > DistanciaMinimaAlCentro)
                        target += to / dist * Mathf.Min(_pullSpeed * Time.deltaTime, dist - DistanciaMinimaAlCentro);
                }

                if (push && !root)
                {
                    Vector3 away = target - _pushCenter;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f) away = transform.forward * -1f;
                    target += away.normalized * (_pushSpeed * Time.deltaTime);
                }

                if ((target - now).sqrMagnitude > 1e-8f)
                {
                    if (_agent != null)
                    {
                        _agent.nextPosition = target;
                        Vector3 np = _agent.nextPosition;
                        transform.position = new Vector3(np.x, now.y, np.z);
                    }
                    else
                    {
                        transform.position = target;
                    }
                }

                // Que la animación de andar vaya acorde con lo que se mueve de verdad.
                if (_agent != null && _agent.enabled)
                {
                    if (root) _agent.velocity = Vector3.zero;
                    else if (slow)
                    {
                        float max = _agent.speed * _slowFactor;
                        if (_agent.velocity.sqrMagnitude > max * max)
                            _agent.velocity = _agent.velocity.normalized * max;
                    }
                }
            }
        }
        _frameValid = false;

        if (slow && !root) ApplyAnimator(_slowFactor);
        else RestoreAnimator();
    }

    private void ApplyAnimator(float factor)
    {
        if (_animator == null) return;
        // Si la IA ha cambiado Animator.speed desde la última vez, ese es el nuevo valor base.
        if (float.IsNaN(_animWritten) || !Mathf.Approximately(_animator.speed, _animWritten))
            _animBase = _animator.speed;
        _animWritten = _animBase * factor;
        _animator.speed = _animWritten;
    }

    private void RestoreAnimator()
    {
        if (_animator == null || float.IsNaN(_animWritten)) return;
        if (Mathf.Approximately(_animator.speed, _animWritten)) _animator.speed = _animBase;
        _animWritten = float.NaN;
    }

    private void ClearVfx(EstadoDeCombate estado)
    {
        if (_vfx.TryGetValue(estado, out var go))
        {
            if (go != null) Destroy(go);
            _vfx.Remove(estado);
        }
    }

    private void OnDisable()
    {
        RestoreAnimator();
        foreach (var go in _vfx.Values) if (go != null) Destroy(go);
        _vfx.Clear();
    }
}
