using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Aro de runas del primer jefe (Paso 6 del refactor Tramo 1, análisis
/// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §6): "las runas se iluminaban cada
/// vez que el monstruo atacaba". Un hijo con collider propio (trigger) del prefab del jefe que:
///
///  - se ilumina SOLO mientras ImpDemonAI.Expuesto es true (aviso, ataque y agotamiento de
///    después; nunca en el rugido de fase): el ritmo lo marca la IA, el aro solo lo enseña,
///  - solo puede recibir daño de disparos en modo preciso (MagicProjectile.IsPrecise, ver Paso 5 /
///    MagicSpellSO.BuildPreciseVariant) llegados DURANTE esa ventana -- cualquier otro impacto
///    (normal, o preciso pero fuera de la ventana) lo ignora sin consumir el proyectil,
///  - tras 'hitsToBreak' impactos válidos, se rompe: apaga su propio brillo, desactiva su
///    collider y llama a ImpDemonAI.ForceFallenByCollarBreak() -- el final alternativo del
///    combate ("el demonio caído", sin orbes, sin destruir el GameObject),
///  - lleva el modelo del aro (hierro con runas) al cuello: las runas pasan de rescoldo a brillo
///    con la ventana y, cuando el jefe cae por cualquier camino, el aro se parte en sus piezas
///    (PiezaQueCae), que caen al suelo y se apagan -- es el aro roto que Eldran guarda después.
///
/// No compite con el daño normal del cuerpo: el Damageable principal del jefe sigue funcionando
/// exactamente igual que hoy en paralelo -- matarlo a golpes (con o sin modo preciso) sigue siendo
/// un final válido y llega antes si el aro no se rompe a tiempo. Este componente es un sistema
/// opcional aparte, nunca sustituye a Damageable ni reduce su vida.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class RuneCollar : MonoBehaviour, IExpuestoAlDano
{
    /// El aro brilla: es el momento de hacerle daño al jefe (ver SoloDanoCuandoExpuesto, INC-469).
    public bool Expuesto => !_broken && demonAI != null && !demonAI.IsDead && demonAI.Expuesto;

    [Header("Referencias")]
    [Tooltip("Si se deja vacío, se busca en los padres (el aro es normalmente un hijo del jefe).")]
    [SerializeField] private ImpDemonAI demonAI;

    [Header("Config")]
    [Tooltip("Impactos precisos con el aro encendido necesarios para romperlo.")]
    [SerializeField, Min(1)] private int hitsToBreak = 3;

    [Header("Brillo extra")]
    [Tooltip("Objetos que se activan SOLO mientras el aro está 'encendido' (ventana de ataque). " +
             "Por ejemplo una luz o un VFX de brillo ya existente en el proyecto, colocado como " +
             "hijo de este mismo GameObject. Vacío = sin señal visual propia (el aro sigue " +
             "funcionando por lógica, solo que no se ve iluminarse).")]
    [SerializeField] private GameObject[] glowVisuals;

    [Header("Modelo del aro")]
    [Tooltip("Renderers de las runas del aro: su _BaseColor va del color apagado al encendido con la ventana.")]
    [SerializeField] private Renderer[] runas;
    [Tooltip("Color de las runas fuera de la ventana: rescoldo.")]
    [SerializeField, ColorUsage(false, true)] private Color runasApagadas = new Color(0.32f, 0.1f, 0.04f);
    [Tooltip("Color de las runas en la ventana (HDR: por encima de 1 da bloom).")]
    [SerializeField, ColorUsage(false, true)] private Color runasEncendidas = new Color(3.2f, 1.25f, 0.45f);
    [Tooltip("Velocidad a la que se encienden y se apagan las runas (unidades de brillo por segundo).")]
    [SerializeField, Min(0.1f)] private float velocidadDelBrillo = 7f;
    [Tooltip("Luz que acompaña al brillo de las runas: ilumina el cuello y la cara del jefe en la ventana. Vacío = sin luz.")]
    [SerializeField] private Light luzDelBrillo;
    [Tooltip("Intensidad de esa luz con las runas encendidas del todo.")]
    [SerializeField, Min(0f)] private float intensidadDeLaLuz = 3f;
    [Tooltip("Piezas en que se parte el aro al caer el jefe. Cada una sale despedida hacia fuera desde el centro del aro.")]
    [SerializeField] private PiezaQueCae[] piezas;
    [Tooltip("Velocidad con la que salen despedidas las piezas (m/s).")]
    [SerializeField, Min(0f)] private float empujeDeLasPiezas = 1.6f;

    [Header("Eventos")]
    [Tooltip("Se dispara UNA vez, al romperse el aro (hitsToBreak alcanzado). ImpDemonAI ya reacciona " +
             "solo (ForceFallenByCollarBreak se llama directamente desde código, no desde este evento) " +
             "-- esto es para que además reaccione cualquier otra cosa en la escena (p. ej. una " +
             "secuencia de Eldran) sin tener que engancharse a ImpDemonAI.OnFellByCollarBreak.")]
    public UnityEvent OnCollarBroken;
    [Tooltip("(hits actuales, hits necesarios) cada vez que un impacto preciso válido cuenta.")]
    public UnityEvent<int, int> OnPreciseHitRegistered;

    private int _hits;
    private bool _broken;
    private bool _wasGlowing;
    private Collider _collider;
    private Damageable _vida;
    private float _brillo;
    private float _brilloPintado = -1f;
    private bool _soltado;
    private MaterialPropertyBlock _mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    // Evita contar el mismo proyectil dos veces si sus varios colliders (poco habitual, pero
    // MagicProjectile busca en GetComponentsInChildren<Collider> al ignorar colisiones con el
    // instigador) llegaran a solapar el trigger del aro en el mismo frame.
    private readonly HashSet<MagicProjectile> _consumedProjectiles = new HashSet<MagicProjectile>();

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (!demonAI) demonAI = GetComponentInParent<ImpDemonAI>();
        _vida = GetComponentInParent<Damageable>();
        _mpb = new MaterialPropertyBlock();
        SetGlow(false);
        PintarRunas();
    }

    // Damageable destruye al jefe en el mismo frame de su muerte: el aro se suelta en el propio
    // aviso, no en el Update siguiente.
    void OnEnable()  { if (_vida) _vida.OnDied += SoltarAro; }
    void OnDisable() { if (_vida) _vida.OnDied -= SoltarAro; }

    void Update()
    {
        // demonAI.IsDead cubre el caso de que el jefe muera por HP normal antes de que se rompa
        // el aro: gana quien llegue primero, y en cuanto eso pasa este componente deja de tener
        // nada que hacer (no hace falta un evento propio para enterarse -- isDead ya lo dice todo).
        if (_soltado || demonAI == null) return;
        if (demonAI.IsDead) { SoltarAro(); return; }
        if (_broken) return;

        bool glowing = demonAI.Expuesto;
        if (glowing != _wasGlowing)
        {
            _wasGlowing = glowing;
            SetGlow(glowing);
        }

        _brillo = Mathf.MoveTowards(_brillo, glowing ? 1f : 0f, velocidadDelBrillo * Time.deltaTime);
        if (!Mathf.Approximately(_brillo, _brilloPintado)) PintarRunas();
    }

    private Color ColorDeRunas => Color.Lerp(runasApagadas, runasEncendidas, _brillo);

    private void PintarRunas()
    {
        _brilloPintado = _brillo;
        if (luzDelBrillo)
        {
            luzDelBrillo.intensity = _brillo * intensidadDeLaLuz;
            bool encendida = _brillo > 0.001f;
            if (luzDelBrillo.enabled != encendida) luzDelBrillo.enabled = encendida;
        }
        if (runas == null) return;
        var color = ColorDeRunas;
        for (int i = 0; i < runas.Length; i++)
        {
            var r = runas[i];
            if (!r) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, color);
            r.SetPropertyBlock(_mpb);
        }
    }

    /// El jefe ha caído: el aro se parte y sus piezas caen al suelo. Sale con un último destello.
    private void SoltarAro()
    {
        if (_soltado) return;
        _soltado = true;
        SetGlow(false);
        if (luzDelBrillo) luzDelBrillo.enabled = false;
        if (_collider) _collider.enabled = false;
        if (piezas == null) return;

        Vector3 centro = transform.position;
        Vector3 eje = transform.up;
        for (int i = 0; i < piezas.Length; i++)
        {
            var pieza = piezas[i];
            if (!pieza) continue;
            var renderer = pieza.GetComponent<Renderer>();
            Vector3 mitad = renderer ? renderer.bounds.center : pieza.transform.position;
            Vector3 fuera = Vector3.ProjectOnPlane(mitad - centro, eje);
            if (fuera.sqrMagnitude < 1e-6f) fuera = transform.forward;
            fuera.Normalize();
            Vector3 velocidad = (fuera + Vector3.up * 0.6f) * empujeDeLasPiezas;
            Vector3 giro = Vector3.Cross(Vector3.up, fuera) * (3f + i * 2f);
            pieza.Soltar(velocidad, giro, runasEncendidas);
        }
    }

    private void SetGlow(bool on)
    {
        if (glowVisuals == null) return;
        for (int i = 0; i < glowVisuals.Length; i++)
            if (glowVisuals[i]) glowVisuals[i].SetActive(on);
    }

    void OnTriggerEnter(Collider other) => TryRegisterHit(other);

    private void TryRegisterHit(Collider other)
    {
        if (_broken || other == null || demonAI == null || demonAI.IsDead) return;

        var projectile = other.GetComponent<MagicProjectile>();
        if (projectile == null) projectile = other.GetComponentInParent<MagicProjectile>();
        if (projectile == null) return;

        if (_consumedProjectiles.Contains(projectile)) return;

        // El aro ignora todo lo que no sea un disparo preciso llegado en plena ventana de ataque
        // -- ni cuenta ni consume el proyectil, que sigue su camino normal (por ejemplo, seguirá
        // haciendo el daño de cuerpo habitual vía MagicProjectile.ResolveHit contra el Damageable
        // principal del jefe, exactamente igual que si el aro no existiera).
        if (!projectile.IsPrecise || !demonAI.Expuesto) return;

        _consumedProjectiles.Add(projectile);
        _hits++;
        OnPreciseHitRegistered?.Invoke(_hits, hitsToBreak);

        if (_hits >= hitsToBreak)
            Break();
    }

    private void Break()
    {
        if (_broken) return;
        _broken = true;

        SetGlow(false);
        if (_collider) _collider.enabled = false;

        if (demonAI != null) demonAI.ForceFallenByCollarBreak();
        SoltarAro();

        OnCollarBroken?.Invoke();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!demonAI) demonAI = GetComponentInParent<ImpDemonAI>();
    }
#endif
}
