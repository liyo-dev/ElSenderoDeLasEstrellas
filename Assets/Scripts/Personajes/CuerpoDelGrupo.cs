using System;
using Game.NPC;
using Game.NPC.States;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// El cuerpo de un personaje del grupo mientras lo lleva la IA: su vida (un Damageable con los
/// valores de ese personaje, que guarda SaludDelGrupo), el estado de caído y la regla de no
/// recibir daño de los compañeros (el fuego amigo llega con INC-727). Lo pone SaludDelGrupo en el
/// NPC de cada compañero con <see cref="Preparar"/>. Ver INC-717.
[DisallowMultipleComponent]
public sealed class CuerpoDelGrupo : MonoBehaviour, IFiltroDeDano
{
    public Slot Personaje { get; private set; }
    public NPCPartyMember Miembro { get; private set; }
    public Damageable Vida { get; private set; }
    /// En el suelo: no lucha, no sigue y espera a que lo levanten.
    public bool Caido { get; private set; }
    /// Se le puede hacer daño ahora (vivo y visible).
    public bool PuedeRecibirDano => !Caido && Vida != null && Vida.IsAlive && isActiveAndEnabled;

    /// Ha cambiado su vida (golpe, curación, caída o levantarse).
    public event Action<CuerpoDelGrupo> VidaCambiada;
    /// Ha recibido un golpe (cantidad aplicada).
    public event Action<CuerpoDelGrupo, float> Golpeado;
    /// Ha llegado a 0 y ha caído.
    public event Action<CuerpoDelGrupo> HaCaido;

    private Interactable _charla;
    private bool _charlaApagada;
    private Rigidbody _rb;
    private bool _rbEraCinematico;
    private Collider _colisionador;

    /// Pone (o reutiliza) el cuerpo del grupo en el NPC de un compañero.
    public static CuerpoDelGrupo Preparar(NPCPartyMember npc, Slot personaje)
    {
        if (npc == null) return null;
        var cuerpo = npc.GetComponent<CuerpoDelGrupo>();
        if (cuerpo == null) cuerpo = npc.gameObject.AddComponent<CuerpoDelGrupo>();
        cuerpo.Configurar(npc, personaje);
        return cuerpo;
    }

    private void Configurar(NPCPartyMember npc, Slot personaje)
    {
        Miembro = npc;
        Personaje = personaje;
        if (Vida != null) return;

        _rb = GetComponent<Rigidbody>();
        _rbEraCinematico = _rb != null && _rb.isKinematic;
        _colisionador = GetComponent<Collider>();
        _charla = GetComponent<Interactable>();
        if (_charla == null) _charla = GetComponentInChildren<Interactable>(true);

        Vida = GetComponent<Damageable>();
        if (Vida == null) Vida = gameObject.AddComponent<Damageable>();
        Vida.SetDestroyOnDeath(false);
        Vida.RefrescarFiltros();
        Vida.OnDamaged += AlDanar;
        Vida.OnHealed += AlCurar;
        Vida.OnDied += AlMorir;
    }

    private void OnDestroy()
    {
        if (Vida == null) return;
        Vida.OnDamaged -= AlDanar;
        Vida.OnHealed -= AlCurar;
        Vida.OnDied -= AlMorir;
    }

    /// Pone la vida guardada del personaje en este cuerpo. Si estaba caído, lo deja en el suelo.
    public void Aplicar(float vidaMaxima, float vida, bool caido)
    {
        if (Vida == null) return;
        if (caido || vida <= 0f)
        {
            // Ya en el suelo: solo se actualiza el máximo, para levantarse con la vida que toca.
            if (Caido) { Vida.SetMaxHealth(vidaMaxima); return; }
            Vida.SetMaxAndCurrent(vidaMaxima, vidaMaxima);
            Vida.Kill();   // pasa por AlMorir: suelo, colisionador apagado y aviso
            return;
        }
        if (Caido) Levantar(vida / Mathf.Max(1f, vidaMaxima));
        Vida.SetMaxAndCurrent(vidaMaxima, vida);
        VidaCambiada?.Invoke(this);
    }

    /// Recibe un golpe de un enemigo que no usa Damageable por su cuenta (p. ej. EnemyProjectile).
    public void RecibirGolpe(float cantidad, GameObject instigador)
    {
        if (PuedeRecibirDano) Vida.TakeDamage(cantidad, instigador);
    }

    /// Lo levanta con esa parte de su vida máxima (0..1).
    public void Levantar(float fraccionDeVida)
    {
        if (!Caido || Vida == null) return;
        Caido = false;

        Vida.Revive(Mathf.Max(1f, Vida.Max * Mathf.Clamp01(fraccionDeVida)));
        // Damageable.Revive deja el Rigidbody dinámico: los NPCs lo llevan cinemático.
        if (_rb != null) _rb.isKinematic = _rbEraCinematico;
        if (_colisionador != null) _colisionador.enabled = true;
        if (_charlaApagada && _charla != null) _charla.EnableInteraction(true);
        _charlaApagada = false;

        var gestor = Miembro != null ? Miembro.NPCManager : null;
        var animador = gestor != null ? gestor.Context?.Animator : GetComponent<NPCSimpleAnimator>();
        animador?.Levantarse();
        if (gestor != null && gestor.Brain != null) gestor.Brain.ChangeState(new IdleState());

        VidaCambiada?.Invoke(this);
        Miembro?.VolverAlCombateOSeguir();
    }

    private void Update()
    {
        // Mientras está caído, nada lo saca del suelo (la IA de combate, el seguimiento o el
        // cerebro que aún se está iniciando en el Will recién creado).
        if (!Caido || Miembro == null) return;
        var gestor = Miembro.NPCManager;
        var brain = gestor != null ? gestor.Brain : null;
        if (brain == null || gestor.Context.IsInCinematic) return;
        if (!(brain.CurrentState is CaidoState)) brain.ChangeState(new CaidoState());
    }

    private void AlDanar(float cantidad)
    {
        Golpeado?.Invoke(this, cantidad);
        VidaCambiada?.Invoke(this);
    }

    private void AlCurar(float _) => VidaCambiada?.Invoke(this);

    private void AlMorir()
    {
        if (Caido) return;
        Caido = true;
        if (_rb != null) _rbEraCinematico = _rb.isKinematic;
        if (_charla != null && _charla.HabilitadoParaUsar)
        {
            _charla.EnableInteraction(false);
            _charlaApagada = true;
        }
        ActiveCombatRegistry.UnregisterNPC(gameObject);
        var brain = Miembro != null && Miembro.NPCManager != null ? Miembro.NPCManager.Brain : null;
        if (brain != null) brain.ChangeState(new CaidoState());
        HaCaido?.Invoke(this);
        VidaCambiada?.Invoke(this);
    }

    /// Sin fuego amigo: los golpes del propio grupo no le hacen daño (INC-727 lo decidirá).
    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (Caido) return 0f;
        if (instigador == null) return cantidad;
        if (instigador.GetComponentInParent<NPCPartyMember>() != null) return 0f;
        if (ControlDelJugador.PersonajeDe(instigador, out _)) return 0f;
        return cantidad;
    }
}
