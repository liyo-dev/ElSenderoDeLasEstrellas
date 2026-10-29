using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Vida, maná y caídas de los tres personajes del grupo (GDD § 6.1, «El grupo en combate»).
///
/// Cada personaje tiene su vida y su maná, igual al mando que con IA:
/// - el que lleva el jugador los tiene en el cuerpo del jugador (PlayerHealthSystem y ManaPool);
///   al cambiar de personaje se guardan los del que sale y se cargan los del que entra;
/// - los demás, en el Damageable de su NPC (<see cref="CuerpoDelGrupo"/>); su maná se regenera aquí
///   y lo gasta su IA al lanzar (<see cref="GastarManaDe"/>).
/// En el preset, los de Will son maxHP/currentHP/maxMP/currentMP y los de Estela y Liam van en
/// companionHealth (sin entrada = los máximos de su ficha).
///
/// Caídas: un compañero a 0 se queda en el suelo. Si cae el que va al mando y queda alguien en
/// pie, el mando pasa a él (<see cref="IntentarRelevo"/>, desde PlayerHealthSystem); si no queda
/// nadie, Game Over. Se levanta manteniendo A al lado (<see cref="ReanimarManteniendoA"/>), con un
/// objeto (PickupEffectType.Revivir), con Brisa Sanadora (<see cref="LevantarEnZona"/>) y al
/// acabar el combate. Va en GrupoDelJugador, que lo añade si su prefab no lo trae. Ver INC-717.
[DisallowMultipleComponent]
public sealed class SaludDelGrupo : MonoBehaviour
{
    public static SaludDelGrupo Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    [Header("Levantarse")]
    [Tooltip("Vida (parte de la máxima) con la que se levantan los caídos al acabar el combate.")]
    [SerializeField, Range(0.05f, 1f)] private float vidaAlGanar = 0.25f;
    [Tooltip("Vida con la que se levanta un caído al reanimarlo manteniendo A a su lado.")]
    [SerializeField, Range(0.05f, 1f)] private float vidaAlReanimar = 0.3f;
    [Tooltip("Vida con la que Brisa Sanadora levanta a los caídos de su zona.")]
    [SerializeField, Range(0.05f, 1f)] private float vidaConBrisa = 0.25f;
    [Tooltip("Vida con la que levanta un objeto que no dice cuánta (su 'amount' a 0).")]
    [SerializeField, Range(0.05f, 1f)] private float vidaConObjeto = 0.5f;

    [Header("Relevo al caer el que va al mando")]
    [Tooltip("Segundos sin recibir daño del personaje que recibe el mando.")]
    [SerializeField, Min(0f)] private float invulnerableAlRelevar = 1.5f;

    [Header("Maná de los compañeros con IA")]
    [Tooltip("Maná por segundo que recuperan los compañeros que lleva la IA.")]
    [SerializeField, Min(0f)] private float regeneracionDeMana = 5f;

    private struct Estado
    {
        public float vidaMax, vida, manaMax, mana;
        public bool caido;
    }

    private readonly Estado[] _estado = new Estado[ControlDelJugador.Personajes];
    private readonly CuerpoDelGrupo[] _cuerpos = new CuerpoDelGrupo[ControlDelJugador.Personajes];
    private readonly float[] _manaAvisado = new float[ControlDelJugador.Personajes];
    private readonly List<CuerpoDelGrupo> _caidos = new List<CuerpoDelGrupo>(2);
    private PlayerHealthSystem _salud;
    private ManaPool _mana;
    private Slot _activo = Slot.Will;
    private bool _iniciado;
    private bool _relevoEnCurso;
    private float _sinEnemigosDesde = -1f;
    private float _proximoEnlace;

    /// Ha cambiado la vida, el maná o el estado de caído de ese personaje.
    public event Action<Slot> SaludCambiada;
    /// Le acaban de dar a ese personaje.
    public event Action<Slot> Golpeado;
    /// Ese personaje ha caído.
    public event Action<Slot> Cayo;
    /// Ese personaje se ha levantado.
    public event Action<Slot> SeLevanto;

    // ─── Consultas ─────────────────────────────────────────────────────────

    public float Vida(Slot p) => p == _activo && _salud != null ? _salud.CurrentHealth : _estado[(int)p].vida;
    public float VidaMax(Slot p) => p == _activo && _salud != null ? _salud.MaxHealth : _estado[(int)p].vidaMax;
    public float Mana(Slot p) => p == _activo && _mana != null ? _mana.Current : _estado[(int)p].mana;
    public float ManaMax(Slot p) => p == _activo && _mana != null ? _mana.Max : _estado[(int)p].manaMax;
    public bool Caido(Slot p) => _estado[(int)p].caido;
    /// Está en el grupo y no ha caído.
    public bool EnPie(Slot p) => !_estado[(int)p].caido && ControlDelJugador.EnElGrupo(p);
    /// Los cuerpos caídos que hay ahora en escena.
    public IReadOnlyList<CuerpoDelGrupo> Caidos => _caidos;

    public static bool EstaCaido(Slot p) => Instance != null && Instance.Caido(p);

    /// El caído más cercano a un punto, dentro del radio. Null si no hay.
    public CuerpoDelGrupo CaidoMasCercano(Vector3 punto, float radio)
    {
        CuerpoDelGrupo mejor = null;
        float mejorDist = radio * radio;
        for (int i = 0; i < _caidos.Count; i++)
        {
            var c = _caidos[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            float d = (c.transform.position - punto).sqrMagnitude;
            if (d <= mejorDist) { mejorDist = d; mejor = c; }
        }
        return mejor;
    }

    // ─── Ciclo de vida ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        PartyControlManager.OnActiveCharacterChanged += AlCambiarActivo;
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        PlayerParty.OnPartyChanged += AlCambiarGrupo;
        GameBootService.OnProfileReady += AlCargarPerfil;
        ActiveCombatRegistry.OnNPCExitedCombat += AlSalirDelCombate;
        BossArenaController.OnAnyBattleEnded += AlGanarBatalla;
    }

    private void OnDisable()
    {
        PartyControlManager.OnActiveCharacterChanged -= AlCambiarActivo;
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        PlayerParty.OnPartyChanged -= AlCambiarGrupo;
        GameBootService.OnProfileReady -= AlCargarPerfil;
        ActiveCombatRegistry.OnNPCExitedCombat -= AlSalirDelCombate;
        BossArenaController.OnAnyBattleEnded -= AlGanarBatalla;
        SoltarJugador();
        for (int i = 0; i < _cuerpos.Length; i++) SoltarCuerpo(i);
        _relevoEnCurso = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (GameBootService.IsAvailable) Inicializar();
    }

    private void Update()
    {
        if (!_iniciado)
        {
            if (GameBootService.IsAvailable) Inicializar();
            return;
        }

        RegenerarManaDeCompaneros(Time.deltaTime);

        // Un cuerpo puede llegar un poco después (el NPC de Will, un compañero que se une).
        if (Time.unscaledTime >= _proximoEnlace)
        {
            _proximoEnlace = Time.unscaledTime + 1f;
            if (FaltaAlgo()) Enlazar();
            ComprobarFinDelCombate();
        }
    }

    // Se vuelve a leer el preset en el siguiente frame, cuando PartyControlManager ya ha vuelto a
    // poner a Will al mando (también escucha este aviso).
    private void AlCargarPerfil()
    {
        _iniciado = false;
        _relevoEnCurso = false;
    }

    private void AlRegistrarJugador(GameObject _) { if (_iniciado) Enlazar(); }
    private void AlCambiarGrupo(IReadOnlyList<NPCPartyMember> _) { if (_iniciado) Enlazar(); }

    private void Inicializar()
    {
        var preset = Preset();
        for (int i = 0; i < _estado.Length; i++) _estado[i] = LeerDelPreset((Slot)i, preset);
        _activo = ControlDelJugador.Activo;
        _iniciado = true;
        EnlazarJugador();
        // Con Will al mando, PlayerHealthSystem y ManaPool ya cargan su vida del preset.
        if (_activo != Slot.Will) CargarEnElJugador(_activo);
        EnlazarCuerpos();
        for (int i = 0; i < _estado.Length; i++) SaludCambiada?.Invoke((Slot)i);
    }

    private Estado LeerDelPreset(Slot p, PlayerPresetSO preset)
    {
        var e = new Estado();
        if (p == Slot.Will)
        {
            var bonos = EstadisticasDelPersonaje.Bonos;
            e.vidaMax = preset != null ? preset.maxHP + bonos.vida : 100f;
            e.vida = preset != null ? preset.currentHP : e.vidaMax;
            e.manaMax = preset != null ? Mathf.Max(0f, preset.maxMP + bonos.magia) : 50f;
            e.mana = preset != null ? preset.currentMP : e.manaMax;
        }
        else
        {
            var guardado = BuscarEntrada(preset, p, crear: false);
            if (guardado != null && guardado.maxHP > 0f)
            {
                e.vidaMax = guardado.maxHP; e.vida = guardado.currentHP;
                e.manaMax = guardado.maxMP; e.mana = guardado.currentMP;
            }
            else
            {
                var ficha = FichaDePersonaje.Buscar(p);
                var stats = ficha != null ? ficha.EstadisticasIniciales : new Estadisticas(100f, 50f, 0f, 0f);
                e.vidaMax = Mathf.Max(1f, stats.vida); e.vida = e.vidaMax;
                e.manaMax = Mathf.Max(0f, stats.magia); e.mana = e.manaMax;
            }
        }
        e.vidaMax = Mathf.Max(1f, e.vidaMax);
        // Nadie empieza caído: quien se guardó a 0 se levanta con poca vida.
        e.vida = e.vida <= 0f ? e.vidaMax * vidaAlGanar : Mathf.Min(e.vida, e.vidaMax);
        e.mana = Mathf.Clamp(e.mana, 0f, e.manaMax);
        e.caido = false;
        return e;
    }

    // ─── Enlaces con los cuerpos ───────────────────────────────────────────

    private void Enlazar()
    {
        EnlazarJugador();
        EnlazarCuerpos();
    }

    private bool FaltaAlgo()
    {
        if (_salud == null || _mana == null) return true;
        for (int i = 0; i < _cuerpos.Length; i++)
        {
            if (i == (int)_activo) continue;
            if (_cuerpos[i] == null && ControlDelJugador.NpcDe((Slot)i) != null) return true;
        }
        return false;
    }

    private void EnlazarJugador()
    {
        PlayerService.TryGetComponent(out PlayerHealthSystem salud, allowSceneLookup: false);
        if (salud != _salud)
        {
            SoltarJugadorSalud();
            _salud = salud;
            if (_salud != null)
            {
                _salud.OnHealthPercentageChanged += AlCambiarVidaDelJugador;
                _salud.OnDamageReceived += AlDanarJugador;
            }
        }
        PlayerService.TryGetComponent(out ManaPool mana, allowSceneLookup: false);
        if (mana != _mana)
        {
            SoltarJugadorMana();
            _mana = mana;
            if (_mana != null)
            {
                _mana.OnManaChanged ??= new UnityEngine.Events.UnityEvent<float>();
                _mana.OnManaChanged.AddListener(AlCambiarManaDelJugador);
            }
        }
    }

    private void SoltarJugador()
    {
        SoltarJugadorSalud();
        SoltarJugadorMana();
    }

    private void SoltarJugadorSalud()
    {
        if (_salud == null) return;
        _salud.OnHealthPercentageChanged -= AlCambiarVidaDelJugador;
        _salud.OnDamageReceived -= AlDanarJugador;
        _salud = null;
    }

    private void SoltarJugadorMana()
    {
        if (_mana == null) return;
        _mana.OnManaChanged?.RemoveListener(AlCambiarManaDelJugador);
        _mana = null;
    }

    private void EnlazarCuerpos()
    {
        for (int i = 0; i < _cuerpos.Length; i++)
        {
            var slot = (Slot)i;
            if (slot == _activo) { SoltarCuerpo(i); continue; }

            var npc = ControlDelJugador.NpcDe(slot);
            if (npc == null) { SoltarCuerpo(i); continue; }

            var cuerpo = CuerpoDelGrupo.Preparar(npc, slot);
            if (cuerpo != _cuerpos[i])
            {
                SoltarCuerpo(i);
                _cuerpos[i] = cuerpo;
                cuerpo.VidaCambiada += AlCambiarVidaDeCuerpo;
                cuerpo.Golpeado += AlGolpearCuerpo;
                cuerpo.HaCaido += AlCaerCuerpo;
            }
            var e = _estado[i];
            cuerpo.Aplicar(e.vidaMax, e.vida, e.caido);
            ActualizarListaDeCaidos(cuerpo);
        }
    }

    private void SoltarCuerpo(int i)
    {
        var c = _cuerpos[i];
        _cuerpos[i] = null;
        if (c == null) return;
        c.VidaCambiada -= AlCambiarVidaDeCuerpo;
        c.Golpeado -= AlGolpearCuerpo;
        c.HaCaido -= AlCaerCuerpo;
        _caidos.Remove(c);
    }

    private void ActualizarListaDeCaidos(CuerpoDelGrupo c)
    {
        if (c == null) return;
        bool esta = _caidos.Contains(c);
        if (c.Caido && !esta) _caidos.Add(c);
        else if (!c.Caido && esta) _caidos.Remove(c);
    }

    // ─── Lo que pasa en los cuerpos ────────────────────────────────────────

    private void AlCambiarVidaDelJugador(float _)
    {
        if (!_iniciado || _salud == null) return;
        int i = (int)_activo;
        _estado[i].vidaMax = _salud.MaxHealth;
        _estado[i].vida = _salud.CurrentHealth;
        EscribirEnPreset(_activo);
        SaludCambiada?.Invoke(_activo);
    }

    private void AlDanarJugador(float _)
    {
        if (_iniciado) Golpeado?.Invoke(_activo);
    }

    private void AlCambiarManaDelJugador(float _)
    {
        if (!_iniciado || _mana == null) return;
        int i = (int)_activo;
        _estado[i].manaMax = _mana.Max;
        _estado[i].mana = _mana.Current;
        if (_activo != Slot.Will) EscribirEnPreset(_activo);
        SaludCambiada?.Invoke(_activo);
    }

    private void AlCambiarVidaDeCuerpo(CuerpoDelGrupo c)
    {
        int i = (int)c.Personaje;
        if (c != _cuerpos[i] || c.Vida == null) return;
        bool estabaCaido = _estado[i].caido;
        _estado[i].vidaMax = c.Vida.Max;
        _estado[i].vida = c.Vida.Current;
        _estado[i].caido = c.Caido;
        ActualizarListaDeCaidos(c);
        EscribirEnPreset(c.Personaje);
        SaludCambiada?.Invoke(c.Personaje);
        if (estabaCaido && !c.Caido) SeLevanto?.Invoke(c.Personaje);
    }

    private void AlGolpearCuerpo(CuerpoDelGrupo c, float _)
    {
        if (c == _cuerpos[(int)c.Personaje]) Golpeado?.Invoke(c.Personaje);
    }

    private void AlCaerCuerpo(CuerpoDelGrupo c)
    {
        int i = (int)c.Personaje;
        if (c != _cuerpos[i]) return;
        bool yaLoEstaba = _estado[i].caido;
        _estado[i].caido = true;
        _estado[i].vida = 0f;
        ActualizarListaDeCaidos(c);
        if (!yaLoEstaba) Cayo?.Invoke(c.Personaje);
    }

    private void RegenerarManaDeCompaneros(float dt)
    {
        if (regeneracionDeMana <= 0f || dt <= 0f) return;
        for (int i = 0; i < _estado.Length; i++)
        {
            if (i == (int)_activo || _estado[i].caido) continue;
            if (_estado[i].mana >= _estado[i].manaMax) continue;
            _estado[i].mana = Mathf.Min(_estado[i].manaMax, _estado[i].mana + regeneracionDeMana * dt);
            bool lleno = _estado[i].mana >= _estado[i].manaMax;
            if (lleno || Mathf.Abs(_estado[i].mana - _manaAvisado[i]) >= 1f)
            {
                _manaAvisado[i] = _estado[i].mana;
                EscribirEnPreset((Slot)i);
                SaludCambiada?.Invoke((Slot)i);
            }
        }
    }

    /// La IA de un compañero gasta maná para lanzar. False si no le llega. El que va al mando
    /// gasta del ManaPool del cuerpo del jugador (MagicCaster), no de aquí.
    public static bool GastarManaDe(GameObject cuerpo, float coste)
    {
        var g = Instance;
        if (g == null || !g._iniciado || coste <= 0f) return true;
        if (!ControlDelJugador.PersonajeDe(cuerpo, out var p) || p == g._activo) return true;
        int i = (int)p;
        if (g._estado[i].mana < coste) return false;
        g._estado[i].mana -= coste;
        g._manaAvisado[i] = g._estado[i].mana;
        g.EscribirEnPreset(p);
        g.SaludCambiada?.Invoke(p);
        return true;
    }

    // ─── Cambio de personaje ───────────────────────────────────────────────

    private void AlCambiarActivo(int indice)
    {
        if (!_iniciado) return;
        var nuevo = (Slot)indice;
        if (nuevo == _activo) { Enlazar(); return; }

        GuardarDelJugador(_activo);
        _activo = nuevo;
        EnlazarJugador();
        CargarEnElJugador(nuevo);
        EnlazarCuerpos();
        SaludCambiada?.Invoke(nuevo);
    }

    private void GuardarDelJugador(Slot p)
    {
        int i = (int)p;
        if (_salud != null)
        {
            _estado[i].vidaMax = _salud.MaxHealth;
            _estado[i].vida = _salud.CurrentHealth;
            if (_estado[i].vida <= 0f) _estado[i].caido = true;
        }
        if (_mana != null)
        {
            _estado[i].manaMax = _mana.Max;
            _estado[i].mana = _mana.Current;
        }
        _manaAvisado[i] = _estado[i].mana;
        EscribirEnPreset(p);
    }

    private void CargarEnElJugador(Slot p)
    {
        var e = _estado[(int)p];
        if (_salud != null) _salud.CargarPersonaje(e.vidaMax, e.vida);
        if (_mana != null) _mana.Init(e.manaMax, e.mana);
    }

    /// Vuelve a poner en el ManaPool el maná del personaje al mando (lo pide PlayerPresetService
    /// al reaplicar el preset con Estela o Liam al mando: el maná del preset es el de Will).
    public void CargarManaDelActivo()
    {
        if (!_iniciado) return;
        EnlazarJugador();
        var e = _estado[(int)_activo];
        if (_mana != null) _mana.Init(e.manaMax, e.mana);
    }

    // ─── Preset ────────────────────────────────────────────────────────────

    private static PlayerPresetSO Preset() => GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;

    private static PlayerPresetSO.SaludDeCompanero BuscarEntrada(PlayerPresetSO preset, Slot p, bool crear)
    {
        if (preset == null) return null;
        preset.companionHealth ??= new List<PlayerPresetSO.SaludDeCompanero>();
        foreach (var e in preset.companionHealth)
            if (e != null && e.personaje == p) return e;
        if (!crear) return null;
        var nueva = new PlayerPresetSO.SaludDeCompanero { personaje = p };
        preset.companionHealth.Add(nueva);
        return nueva;
    }

    private void EscribirEnPreset(Slot p) => EscribirEnPreset(p, Preset());

    private void EscribirEnPreset(Slot p, PlayerPresetSO preset)
    {
        if (preset == null) return;
        var e = _estado[(int)p];
        if (p == Slot.Will)
        {
            // Con Will al mando, PlayerHealthSystem escribe su vida y GameBootProfile su maná.
            if (_activo == Slot.Will) return;
            var bonos = EstadisticasDelPersonaje.Bonos;
            preset.maxHP = Mathf.Max(1f, e.vidaMax - bonos.vida);
            preset.currentHP = e.caido ? 0f : e.vida;
            preset.maxMP = Mathf.Max(0f, e.manaMax - bonos.magia);
            preset.currentMP = e.mana;
            return;
        }
        var entrada = BuscarEntrada(preset, p, crear: true);
        entrada.maxHP = e.vidaMax;
        entrada.currentHP = e.caido ? 0f : e.vida;
        entrada.maxMP = e.manaMax;
        entrada.currentMP = e.mana;
    }

    /// Deja en el preset la vida y el maná de cada uno. Lo llama GameBootProfile al guardar,
    /// después de copiar los del cuerpo del jugador en los campos de Will.
    public void VolcarEnPreset(PlayerPresetSO preset)
    {
        if (!_iniciado || preset == null) return;
        int a = (int)_activo;
        if (_salud != null) { _estado[a].vidaMax = _salud.MaxHealth; _estado[a].vida = _salud.CurrentHealth; }
        if (_mana != null) { _estado[a].manaMax = _mana.Max; _estado[a].mana = _mana.Current; }
        for (int i = 0; i < _estado.Length; i++) EscribirEnPreset((Slot)i, preset);
    }

    /// <summary>
    /// Aumenta de forma permanente la vida y/o maná máximos de todos los miembros del grupo (INC-847, Sintonía Estelar).
    /// </summary>
    public void AumentarMaximosDelGrupo(float masVida, float masMana)
    {
        var preset = UnlockService.GetActivePreset();
        for (int i = 0; i < _estado.Length; i++)
        {
            var p = (Slot)i;
            if (p == _activo)
            {
                if (_salud != null)
                {
                    _estado[i].vidaMax = _salud.MaxHealth;
                    _estado[i].vida = _salud.CurrentHealth;
                }
                if (_mana != null)
                {
                    _estado[i].manaMax = _mana.Max;
                    _estado[i].mana = _mana.Current;
                }
            }
            else
            {
                if (masVida > 0f)
                {
                    _estado[i].vidaMax += masVida;
                    _estado[i].vida += masVida;
                    if (_cuerpos[i] != null && _cuerpos[i].Vida != null)
                    {
                        _cuerpos[i].Vida.SetMaxHealth(_estado[i].vidaMax);
                        _cuerpos[i].Vida.Heal(masVida);
                    }
                }
                if (masMana > 0f)
                {
                    _estado[i].manaMax += masMana;
                    _estado[i].mana += masMana;
                }
                if (preset != null)
                {
                    EscribirEnPreset(p, preset);
                }
            }
            SaludCambiada?.Invoke(p);
        }
    }

    // ─── Caídas y relevo ───────────────────────────────────────────────────

    /// El que va al mando ha llegado a 0. Si queda alguien del grupo en pie, cae y el mando pasa
    /// a él (true: no hay Game Over). Lo llama PlayerHealthSystem antes de morir.
    public static bool IntentarRelevo(PlayerHealthSystem salud)
    {
        var g = Instance;
        return g != null && g.isActiveAndEnabled && g.Relevar(salud);
    }

    private bool Relevar(PlayerHealthSystem salud)
    {
        if (!_iniciado || salud == null) return false;
        EnlazarJugador();
        if (salud != _salud) return false;
        if (_relevoEnCurso) return true;
        var pcm = PartyControlManager.Instance;
        if (pcm == null || pcm.IsSwitchingLocked) return false;
        if (!SiguienteEnPie(out var destino)) return false;

        _relevoEnCurso = true;
        int i = (int)_activo;
        _estado[i].vida = 0f;
        _estado[i].caido = true;
        EscribirEnPreset(_activo);
        Cayo?.Invoke(_activo);
        SaludCambiada?.Invoke(_activo);
        StartCoroutine(Co_Relevo(destino));
        return true;
    }

    private IEnumerator Co_Relevo(Slot destino)
    {
        // Deja terminar el golpe que lo ha tumbado antes de cambiar de cuerpo.
        yield return null;
        if (PlayerService.TryGetComponent(out PlayerActionManager modos, allowSceneLookup: false))
            modos.ResetToDefault();

        bool hecho = ControlDelJugador.PasarElMandoA(destino);
        _relevoEnCurso = false;
        if (!hecho)
        {
            // No se ha podido pasar el mando: cae de verdad.
            if (_salud != null) _salud.MorirSinRelevo();
            yield break;
        }
        if (_salud != null) _salud.HacerInvulnerable(invulnerableAlRelevar, parpadeo: true);
    }

    /// El personaje en pie más cercano al que va al mando (sin contarlo a él).
    private bool SiguienteEnPie(out Slot destino)
    {
        destino = _activo;
        var cuerpoActivo = ControlDelJugador.CuerpoDelJugador;
        Vector3 desde = cuerpoActivo != null ? cuerpoActivo.transform.position : Vector3.zero;
        float mejor = float.MaxValue;
        bool hay = false;
        for (int i = 0; i < _estado.Length; i++)
        {
            var p = (Slot)i;
            if (p == _activo || !EnPie(p)) continue;
            var cuerpo = ControlDelJugador.CuerpoDe(p);
            float d = cuerpo != null ? (cuerpo.transform.position - desde).sqrMagnitude : float.MaxValue * 0.5f;
            if (d < mejor) { mejor = d; destino = p; hay = true; }
        }
        return hay;
    }

    // ─── Daño directo (pruebas y efectos que no pasan por un golpe) ─────────

    /// Quita esa parte (0..1) de la vida máxima a ese personaje, esté al mando o con la IA.
    public void Danar(Slot p, float parteDeLaVida)
    {
        float cantidad = VidaMax(p) * Mathf.Clamp01(parteDeLaVida);
        if (cantidad <= 0f) return;
        if (p == _activo)
        {
            if (_salud != null) _salud.TakeDamage(cantidad, ignoreInvulnerability: true);
            return;
        }
        var c = _cuerpos[(int)p];
        if (c != null && c.Vida != null) c.Vida.TakeDamage(cantidad, null);
    }

    /// Deja a ese personaje a 0: si va al mando, cae y pasa el mando (o Game Over si no queda nadie).
    public void Tumbar(Slot p)
    {
        if (p == _activo)
        {
            if (_salud != null) _salud.Kill();
            return;
        }
        var c = _cuerpos[(int)p];
        if (c != null && c.Vida != null) c.Vida.Kill();
    }

    // ─── Levantar a los caídos ─────────────────────────────────────────────

    /// Levanta a ese personaje con esa parte de su vida (0..1). False si no estaba caído.
    public bool Levantar(Slot p, float fraccionDeVida)
    {
        int i = (int)p;
        if (!_estado[i].caido) return false;
        var c = _cuerpos[i];
        if (c != null && c.Caido)
        {
            c.Levantar(fraccionDeVida);   // avisa por VidaCambiada
            return true;
        }
        _estado[i].caido = false;
        _estado[i].vida = Mathf.Max(1f, _estado[i].vidaMax * Mathf.Clamp01(fraccionDeVida));
        EscribirEnPreset(p);
        SaludCambiada?.Invoke(p);
        SeLevanto?.Invoke(p);
        return true;
    }

    /// Reanimar manteniendo A al lado del caído.
    public bool Reanimar(CuerpoDelGrupo cuerpo) => cuerpo != null && Levantar(cuerpo.Personaje, vidaAlReanimar);

    /// Brisa Sanadora (o cualquier magia de curación de zona) levanta a los caídos de su zona.
    public static void LevantarEnZona(Vector3 centro, float radio)
    {
        var g = Instance;
        if (g == null) return;
        for (int i = g._caidos.Count - 1; i >= 0; i--)
        {
            var c = g._caidos[i];
            if (c == null) continue;
            Vector3 d = c.transform.position - centro;
            d.y = 0f;
            if (d.sqrMagnitude <= radio * radio) g.Levantar(c.Personaje, g.vidaConBrisa);
        }
    }

    /// Un objeto que levanta al caído más cercano al jugador. 'fraccion' 0 = la del objeto por
    /// defecto. False si no hay nadie caído.
    public static bool LevantarConObjeto(float fraccion)
    {
        var g = Instance;
        if (g == null) return false;
        var jugador = ControlDelJugador.CuerpoDelJugador;
        var c = g.CaidoMasCercano(jugador != null ? jugador.transform.position : Vector3.zero, float.MaxValue / 4f);
        if (c != null) return g.Levantar(c.Personaje, fraccion > 0f ? fraccion : g.vidaConObjeto);
        for (int i = 0; i < g._estado.Length; i++)
            if (g._estado[i].caido && g.Levantar((Slot)i, fraccion > 0f ? fraccion : g.vidaConObjeto)) return true;
        return false;
    }

    /// Levanta a todos los caídos con esa parte de su vida.
    public void LevantarATodos(float fraccionDeVida)
    {
        for (int i = 0; i < _estado.Length; i++)
            if (_estado[i].caido) Levantar((Slot)i, fraccionDeVida);
    }

    /// Al acabar el combate (ya no queda ningún enemigo en combate) los caídos se levantan.
    private void AlSalirDelCombate(GameObject _)
    {
        if (!HayCaidos() || HayEnemigosEnCombate()) return;
        LevantarATodos(vidaAlGanar);
    }

    private void AlGanarBatalla()
    {
        if (HayCaidos()) LevantarATodos(vidaAlGanar);
    }

    /// Por si el último enemigo desaparece sin salir del registro de combate: con caídos y sin
    /// enemigos durante unos segundos, se levantan igual.
    private void ComprobarFinDelCombate()
    {
        if (!HayCaidos() || HayEnemigosEnCombate()) { _sinEnemigosDesde = -1f; return; }
        if (_sinEnemigosDesde < 0f) { _sinEnemigosDesde = Time.time; return; }
        if (Time.time - _sinEnemigosDesde < 2f) return;
        _sinEnemigosDesde = -1f;
        LevantarATodos(vidaAlGanar);
    }

    private static bool HayEnemigosEnCombate()
    {
        foreach (var go in ActiveCombatRegistry.GetAllInCombat())
            if (go != null && go.GetComponent<NPCPartyMember>() == null) return true;
        return false;
    }

    private bool HayCaidos()
    {
        for (int i = 0; i < _estado.Length; i++) if (_estado[i].caido) return true;
        return false;
    }
}
