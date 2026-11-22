using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC;
using Game.NPC.Combate;
using Game.NPC.Modules;
using Liyodev.NarrativeGraph;

/// Lección de combate contra un NPC, sin daño para nadie. La historia sigue por «Hecho» cuando el
/// jugador ha hecho lo que se enseña las veces pedidas: devolver (una bola suya, B en el momento
/// justo), protegerse (mantener B bajo una lluvia de hechizos), romper su escudo elemental o
/// golpearle. Todo lo que dice el NPC sale en la caja de diálogo y la lección se para mientras se
/// lee: los hechizos de «Devolver» y «Protegerse» los lanza la propia lección cuando se cierra la
/// caja, así lo que se explica y lo que pasa van a la vez. Al acabar el NPC vuelve a estar en paz.
[Serializable]
[NarrativeNodeInfo("Combate", "Lección de combate", "Combate de práctica sin daño contra un NPC que acaba cuando el jugador se protege, devuelve o golpea las veces pedidas.")]
[NarrativeNoWorldEffect]
public sealed class PedirLeccionDeCombate : NarrativeRequest
{
    public enum Objetivo
    {
        [Tooltip("Mantener el escudo bajo una lluvia de hechizos del NPC.")]
        Protegerse = 0,
        [Tooltip("Devolverle una bola suya con la B en el momento justo.")]
        Devolver = 1,
        // El 2 queda reservado (era una lección retirada) y no se reutiliza.
        [Tooltip("Romper el escudo elemental del NPC con el elemento que lo vence.")]
        RomperEscudo = 3,
    }

    static readonly string[] Salidas = { "Hecho" };

    [NarrativeKey(NarrativeKeyKind.Actor)]
    [Tooltip("persistenceId del NPC que enseña.")]
    public string npcId;

    [Tooltip("Configuración de combate de la lección. Vacía = la que tenga el NPC.")]
    public NPCCombatConfig combatConfig;

    [Tooltip("Perfil de combate de la lección (ritmo, avisos, si se defiende). Vacío = el de la configuración.")]
    public PerfilDeCombate perfil;

    [Tooltip("Qué tiene que hacer el jugador.")]
    public Objetivo objetivo = Objetivo.Protegerse;

    [Min(1), Tooltip("Veces que tiene que hacerlo.")]
    public int veces = 2;

    [Header("Ritmo")]
    [Min(0f), Tooltip("Segundos entre cerrar la caja de diálogo y el primer hechizo: tiempo para ponerse en guardia.")]
    public float segundosDePreparacion = 1f;

    [Min(0f), Tooltip("Segundos entre un hechizo y el siguiente. 0 = 1 s en la lluvia y 2,5 s entre bolas.")]
    public float segundosEntreHechizos;

    [Min(0f), Tooltip("Segundos entre el gesto del NPC y la salida de la bola. 0 = 0,8 s.")]
    public float segundosDeAviso;

    [Header("Hechizos de la lección (Devolver y Protegerse)")]
    [Tooltip("Proyectil con EnemyProjectile que lanza el NPC. Vacío = el del primer ataque del perfil.")]
    public GameObject proyectil;
    [Tooltip("Estado del Animator (capa de medio cuerpo) del gesto al lanzar. Vacío = el del primer ataque del perfil.")]
    public string animacionDelHechizo;
    [Min(1), Tooltip("Hechizos que caen en cada tanda de la lluvia (Protegerse).")]
    public int hechizosPorTanda = 5;
    [Min(0f), Tooltip("Segundos de descanso entre una tanda de lluvia y la siguiente.")]
    public float segundosEntreTandas = 2.5f;
    [Min(1f), Tooltip("Altura sobre el jugador desde la que cae la lluvia.")]
    public float alturaDeLaLluvia = 8f;
    [Min(0f), Tooltip("Radio alrededor del jugador en el que cae cada hechizo de la lluvia.")]
    public float radioDeLaLluvia = 1.2f;
    [Min(0f), Tooltip("Velocidad de los hechizos de la lluvia. 0 = la del proyectil.")]
    public float velocidadDeLaLluvia = 9f;
    [Tooltip("Color de la lluvia (solo el tinte; no da elemento al golpe).")]
    public MagicElement colorDeLaLluvia = MagicElement.Fire;

    [Header("Romper el escudo")]
    [Tooltip("Elemento del escudo del NPC (solo con «Romper escudo»). Lo rompe el que dicta RuptoresDeEscudo.")]
    public MagicElement elementoDelEscudo = MagicElement.Storm;
    [NarrativeKey(NarrativeKeyKind.LocKey)] public string claveAlRebotar;
    [TextArea(1, 2), Tooltip("Cuando el jugador tira un elemento que no rompe el escudo. Vacía = nada.")] public string fraseAlRebotar;

    [Header("Frases del NPC (caja de diálogo)")]
    [NarrativeKey(NarrativeKeyKind.LocKey)] public string claveAlEmpezar;
    [TextArea(1, 3)] public string fraseAlEmpezar;
    [NarrativeKey(NarrativeKeyKind.LocKey)] public string claveAlAcertar;
    [TextArea(1, 2), Tooltip("Al acertar sin haber acabado todavía. Vacía = nada.")] public string fraseAlAcertar;
    [NarrativeKey(NarrativeKeyKind.LocKey)] public string claveAlAcabar;
    [TextArea(1, 3)] public string fraseAlAcabar;

    public override string[] Exits => Salidas;

    public override string DescribeDecision(NarrativeNode step)
        => $"Lección «{step?.DisplayTitle}» (guid {step?.Guid}) con '{npcId}': {objetivo} ×{veces}. Salida: Hecho.";

    public override void Describe(INarrativeNodeDescriber d)
    {
        d.Actor(npcId, "enseña");
        d.Line($"{objetivo} ×{veces}", NarrativeTextTone.Strong);
        if (objetivo == Objetivo.RomperEscudo) d.Line($"Escudo de {elementoDelEscudo}");
        if (!string.IsNullOrEmpty(fraseAlEmpezar) || !string.IsNullOrEmpty(claveAlEmpezar))
            d.Line(d.Text(claveAlEmpezar, fraseAlEmpezar) ?? string.Empty);
        if (perfil != null) d.Asset(perfil, "perfil");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(npcId)) warnings.Add("Falta el NPC de la lección.");
    }
}

public sealed class LeccionDeCombateEnCurso : NarrativeRequestRun<PedirLeccionDeCombate>
{
    const int SalidaHecho = 0;
    const int CapaDeMedioCuerpo = 1;

    NPCBehaviourManagerV2 _npc;
    SequenceActor _actor;
    Damageable _receptor;
    GameObject _bolaDePractica;
    ProyectilDevuelto _devolucion;
    PresentacionDelCombo _comboVisual;
    PresentacionDelCombo _modeloDelCombo;
    Coroutine _hechizos;
    Coroutine _recuperacion;
    int _turnoDePreparacion = -1;
    static readonly ComboButton[] PasosDeLaLluvia = { ComboButton.X, ComboButton.Y, ComboButton.B };
    static readonly string[] GestosDeLaLluvia = { "MagicLeft", "CastingIdle01", "MagicRight" };
    NPCCombatConfig _configDeAntes;
    NPCCombatConfig _copiaDeConfig;
    PerfilDeCombate _copiaDePerfil;
    GolpesDePractica _golpes;
    NPCShieldController _escudo;
    GameObject _prefabDeEscudoDeAntes;
    bool _prefabDeEscudoCambiado;
    Animator _animator;
    GameObject _proyectil;
    string _animacion;
    bool _parado;
    bool _enCaja;
    bool _terminado;
    float _ultimoRebote = -99f;
    bool _golpesPuestos;
    int _aciertos;
    bool _acabando;

    protected override void Start()
    {
        _npc = string.IsNullOrWhiteSpace(R.npcId) ? null : CombateContraNpcEnCurso.BuscarNpc(R.npcId);
        if (_npc == null || (!_npc.gameObject.activeInHierarchy && !CombateContraNpcEnCurso.TraerAEscena(R.npcId, _npc)))
        {
            Debug.LogWarning($"[Lección:{StepGuid}] No está el NPC '{R.npcId}' → sigue para no bloquear.");
            Terminar();
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            DialogueManager.Instance.Close();

        PrepararAlNpc();
        EnCaja(R.claveAlEmpezar, R.fraseAlEmpezar, Empezar);
    }

    void Empezar()
    {
        if (_parado || _acabando) return;
        switch (R.objetivo)
        {
            case PedirLeccionDeCombate.Objetivo.Protegerse:
                PlayerShieldController.AlPararAtaque += AlParar;
                EmpezarHechizos();
                return;
            case PedirLeccionDeCombate.Objetivo.Devolver:
                PlayerShieldController.AlDevolverAtaque += AlDevolver;
                EmpezarHechizos();
                return;
            case PedirLeccionDeCombate.Objetivo.RomperEscudo:
                _escudo = _npc.GetComponent<NPCShieldController>();
                if (_escudo == null)
                {
                    Debug.LogWarning($"[Lección:{StepGuid}] '{R.npcId}' no tiene NPCShieldController → sigue para no bloquear.");
                    Terminar();
                    return;
                }
                PonerElEscudoElemental();
                _escudo.ConfigurarElemento(R.elementoDelEscudo);
                _escudo.AlRomperse += AlRomperElEscudo;
                _escudo.AlRebotar += AlRebotarElemento;
                _escudo.StartDefending(600f);
                return;
            default:
                Debug.LogWarning($"[Lección:{StepGuid}] Objetivo {R.objetivo} desconocido → sigue para no bloquear.");
                Terminar();
                return;
        }
    }

    // La lección de escudo usa la burbuja del escudo nuevo (la del jugador, teñida del elemento).
    void PonerElEscudoElemental()
    {
        if (!PlayerService.TryGetPlayer(out GameObject jugador) || jugador == null) return;
        var escudoDelJugador = jugador.GetComponentInChildren<PlayerShieldController>(true);
        if (escudoDelJugador == null || escudoDelJugador.PrefabDelEscudo == null) return;
        _prefabDeEscudoDeAntes = _escudo.CambiarPrefab(escudoDelJugador.PrefabDelEscudo);
        _prefabDeEscudoCambiado = true;
    }

    void EmpezarHechizos()
    {
        SequenceActor.TryResolve(R.npcId, out _actor);
        _proyectil = R.proyectil;
        _animacion = R.animacionDelHechizo;
        if (_copiaDePerfil != null)
        {
            for (int i = 0; i < _copiaDePerfil.acciones.Count; i++)
            {
                var accion = _copiaDePerfil.acciones[i];
                if (accion == null || accion.proyectil == null) continue;
                if (_proyectil == null) _proyectil = accion.proyectil;
                if (string.IsNullOrEmpty(_animacion)) _animacion = accion.animacion;
                break;
            }
        }
        if (_proyectil == null || _proyectil.GetComponentInChildren<EnemyProjectile>(true) == null || Runner == null)
        {
            Debug.LogWarning($"[Lección:{StepGuid}] Sin proyectil con EnemyProjectile para '{R.npcId}' → sigue para no bloquear.");
            Terminar();
            return;
        }
        _hechizos = Runner.StartCoroutine(Co_Hechizos());
    }

    // Los hechizos los lanza la lección: nada sale mientras está abierta la caja de diálogo.
    IEnumerator Co_Hechizos()
    {
        if (R.segundosDePreparacion > 0f) yield return new WaitForSeconds(R.segundosDePreparacion);
        bool lluvia = R.objetivo == PedirLeccionDeCombate.Objetivo.Protegerse;
        float entre = R.segundosEntreHechizos > 0f ? R.segundosEntreHechizos : (lluvia ? 1f : 2.5f);
        var esperaEntre = new WaitForSeconds(entre);
        var esperaCorta = new WaitForSeconds(0.2f);
        var gesto = new WaitForSeconds(R.segundosDeAviso > 0f ? R.segundosDeAviso : 0.8f);
        var descanso = new WaitForSeconds(Mathf.Max(0.5f, R.segundosEntreTandas));
        GameObject jugador;
        if (PlayerService.TryGetPlayer(out jugador) && jugador != null)
        {
            _actor?.Hold();
            Vector3 rumbo = _npc.transform.position - jugador.transform.position;
            rumbo.y = 0f;
            if (rumbo.sqrMagnitude < 0.01f) rumbo = _npc.transform.forward;
            float distancia = Mathf.Clamp(5.5f - rumbo.magnitude, 0f, 3.5f);
            if (_actor != null && distancia > 0.25f &&
                UnityEngine.AI.NavMesh.SamplePosition(_npc.transform.position + rumbo.normalized * distancia,
                                                      out var retirada, 0.8f, UnityEngine.AI.NavMesh.AllAreas))
                yield return SequenceMovement.MoveTo(_actor, retirada.position, 0f, 2.5f, esperaMaxima: 2.5f);
            Mirar(jugador.transform.position);
            if (!lluvia)
            {
                var aviso = PresentadorDeLinea.Decir(new FraseADecir
                {
                    clave = "LECCION_COMBATE_PREPARADO",
                    textoPorDefecto = "¿Preparado?",
                    ancla = _npc.transform,
                    duracion = 1.15f
                });
                _turnoDePreparacion = aviso.Turno;
                if (aviso.Duracion > 0f) yield return new WaitForSeconds(aviso.Duracion);
                _turnoDePreparacion = -1;
            }
            else
            {
                _modeloDelCombo = jugador.GetComponentInChildren<PresentacionDelCombo>(true);
                if (_modeloDelCombo != null)
                {
                    _comboVisual = _npc.GetComponent<PresentacionDelCombo>();
                    if (_comboVisual == null) _comboVisual = _npc.gameObject.AddComponent<PresentacionDelCombo>();
                    if (_animator != null && _animator.isHuman && !_animator.TryGetComponent<ManosIK>(out _))
                        _animator.gameObject.AddComponent<ManosIK>();
                }
            }
        }

        while (!_parado && !_acabando)
        {
            while (_enCaja && !_parado) yield return null;
            if (_parado || _acabando) yield break;
            if (!PlayerService.TryGetPlayer(out jugador) || jugador == null) { yield return esperaCorta; continue; }

            Mirar(jugador.transform.position);
            if (lluvia) yield return CargarLaLluvia(jugador.transform);
            else
            {
                Gesto(_animacion);
                yield return gesto;
            }
            if (_parado || _acabando) yield break;

            if (lluvia)
            {
                int n = Mathf.Max(1, R.hechizosPorTanda);
                GameObject ultima = null;
                for (int i = 0; i < n && !_parado && !_acabando; i++)
                {
                    while (_enCaja && !_parado) yield return null;
                    if (PlayerService.TryGetPlayer(out jugador) && jugador != null) ultima = LanzarDesdeArriba(jugador.transform.position);
                    yield return esperaEntre;
                }
                while (ultima != null && !_parado) yield return null;
                if (!_parado && _aciertos >= Mathf.Max(1, R.veces))
                {
                    FinalizarLeccion();
                    yield break;
                }
                yield return descanso;
            }
            else
            {
                if (!PlayerService.TryGetPlayer(out jugador) || jugador == null) continue;
                _bolaDePractica = LanzarBola(jugador.transform.position);
                while (_bolaDePractica != null && !_parado && !_acabando) yield return null;
                yield return esperaEntre;
            }
        }
    }

    IEnumerator CargarLaLluvia(Transform jugador)
    {
        if (_comboVisual != null && _modeloDelCombo != null)
            _comboVisual.AbrirParaNpc(_modeloDelCombo, PasosDeLaLluvia.Length);
        var pausa = new WaitForSeconds(0.45f);
        for (int i = 0; i < PasosDeLaLluvia.Length; i++)
        {
            if (_parado || _acabando || jugador == null) yield break;
            Mirar(jugador.position);
            Gesto(GestosDeLaLluvia[i]);
            _comboVisual?.MostrarPasoDelNpc(PasosDeLaLluvia[i], i);
            yield return pausa;
        }
        _comboVisual?.CerrarParaNpc(true);
        Gesto(_animacion);
    }

    void Mirar(Vector3 donde) => _actor?.Face(donde);

    void Gesto(string animacion)
    {
        if (_animator == null || string.IsNullOrEmpty(animacion) || _animator.layerCount <= CapaDeMedioCuerpo) return;
        int estado = Animator.StringToHash(animacion);
        if (!_animator.HasState(CapaDeMedioCuerpo, estado)) return;
        _animator.SetLayerWeight(CapaDeMedioCuerpo, 1f);
        _animator.CrossFadeInFixedTime(estado, 0.09f, CapaDeMedioCuerpo, 0f);
    }

    GameObject LanzarBola(Vector3 piesDelJugador)
    {
        Transform t = _npc.transform;
        Vector3 origen = t.position + Vector3.up * 1.4f + t.forward * 0.7f;
        Vector3 direccion = (SondeoDelTerreno.Pecho(piesDelJugador) - origen).normalized;
        var bola = UnityEngine.Object.Instantiate(_proyectil, origen, Quaternion.LookRotation(direccion));
        Preparar(bola, direccion, true, 0f);
        return bola;
    }

    GameObject LanzarDesdeArriba(Vector3 piesDelJugador)
    {
        Vector2 azar = UnityEngine.Random.insideUnitCircle * R.radioDeLaLluvia;
        Vector3 destino = SondeoDelTerreno.Pecho(piesDelJugador) + new Vector3(azar.x, 0f, azar.y);
        Vector3 haciaElNpc = _npc.transform.position - destino;
        haciaElNpc.y = 0f;
        Vector3 origen = destino + Vector3.up * R.alturaDeLaLluvia + (haciaElNpc.sqrMagnitude > 0.01f ? haciaElNpc.normalized * 2f : Vector3.zero);
        Vector3 direccion = (destino - origen).normalized;
        var bola = UnityEngine.Object.Instantiate(_proyectil, origen, Quaternion.LookRotation(direccion));
        EnemyProjectile.TenirConElemento(bola, R.colorDeLaLluvia);
        Preparar(bola, direccion, false, R.velocidadDeLaLluvia);
        return bola;
    }

    void Preparar(GameObject bola, Vector3 direccion, bool sePuedeDevolver, float velocidad)
    {
        var proyectil = bola.GetComponentInChildren<EnemyProjectile>();
        if (proyectil == null) return;
        var rb = proyectil.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        proyectil.AsignarLanzador(_npc.gameObject);
        proyectil.PermitirDevolucion(sePuedeDevolver);
        if (sePuedeDevolver) proyectil.AsignarElemento(MagicElement.Light);
        if (velocidad > 0f) proyectil.SetSpeed(velocidad);
        proyectil.Initialize(direccion, 0f);
    }

    protected override void Stop()
    {
        _parado = true;
        if (_hechizos != null && Runner != null) Runner.StopCoroutine(_hechizos);
        if (_recuperacion != null && Runner != null) Runner.StopCoroutine(_recuperacion);
        if (_turnoDePreparacion >= 0) PresentadorDeLinea.Esconder(_turnoDePreparacion);
        if (_bolaDePractica != null) UnityEngine.Object.Destroy(_bolaDePractica);
        Desuscribir();
        DejarAlNpc();
    }

    void Terminar()
    {
        if (_terminado) return;
        _terminado = true;
        Finish(SalidaHecho);
    }

    void Desuscribir()
    {
        PlayerShieldController.AlPararAtaque -= AlParar;
        PlayerShieldController.AlDevolverAtaque -= AlDevolver;
        if (_devolucion != null) _devolucion.AlImpactarEnEnemigo -= AlRecibirLaBola;
        _devolucion = null;
        if (_escudo != null)
        {
            _escudo.AlRomperse -= AlRomperElEscudo;
            _escudo.AlRebotar -= AlRebotarElemento;
        }
    }

    void AlParar(GameObject lanzador)
    {
        if (!_parado && _npc != null && lanzador == _npc.gameObject) _aciertos++;
    }
    void AlDevolver(GameObject lanzador)
    {
        if (_npc == null || lanzador != _npc.gameObject || _bolaDePractica == null) return;
        _devolucion = _bolaDePractica.GetComponentInChildren<ProyectilDevuelto>();
        if (_devolucion != null) _devolucion.AlImpactarEnEnemigo += AlRecibirLaBola;
    }

    void AlRecibirLaBola(Damageable receptor)
    {
        if (receptor == null || receptor != _receptor || _recuperacion != null || _parado) return;
        _recuperacion = Runner.StartCoroutine(Co_Recuperarse(EstadosDeCombate.De(receptor)));
    }

    IEnumerator Co_Recuperarse(EstadosDeCombate estados)
    {
        if (_animator != null && _animator.layerCount > CapaDeMedioCuerpo) _animator.SetLayerWeight(CapaDeMedioCuerpo, 0f);
        _npc.SimpleAnimator?.PlayDizzy();
        yield return null;
        while (estados != null && estados.EstaIncapacitado) yield return null;
        _npc.SimpleAnimator?.TransitionToLocomotion();
        _recuperacion = null;
        Acierto();
    }
    void AlRomperElEscudo() => Acierto();

    void AlRebotarElemento(MagicElement elemento)
    {
        if (_acabando || _enCaja || Time.time - _ultimoRebote < 6f) return;
        _ultimoRebote = Time.time;
        EnCaja(R.claveAlRebotar, R.fraseAlRebotar, null);
    }

    void Acierto()
    {
        if (_acabando || _parado) return;
        _aciertos++;
        if (_aciertos < Mathf.Max(1, R.veces))
        {
            if (!_enCaja) EnCaja(R.claveAlAcertar, R.fraseAlAcertar, null);
            return;
        }
        FinalizarLeccion();
    }

    void FinalizarLeccion()
    {
        _acabando = true;
        Desuscribir();
        DejarAlNpc();
        EnCaja(R.claveAlAcabar, R.fraseAlAcabar, Terminar);
    }

    // Una frase del NPC en la caja de diálogo; mientras está abierta la lección no lanza nada.
    void EnCaja(string clave, string texto, Action alCerrar)
    {
        string frase = Texto(clave, texto);
        var dialogos = DialogueManager.Instance;
        if (_npc == null || string.IsNullOrEmpty(frase)) { alCerrar?.Invoke(); return; }
        if (dialogos == null)
        {
            PresentadorDeLinea.Decir(new FraseADecir { clave = clave, textoPorDefecto = texto, ancla = _npc.transform });
            alCerrar?.Invoke();
            return;
        }
        if (dialogos.IsOpen) dialogos.Close();

        var charla = ScriptableObject.CreateInstance<DialogueAsset>();
        charla.lines = new[] { new DialogueLine { speakerNameId = _npc.DialogueCharacterId, text = frase } };
        _enCaja = true;
        dialogos.StartDialogue(charla, _npc.transform, () =>
        {
            _enCaja = false;
            UnityEngine.Object.Destroy(charla);
            if (!_parado || _acabando) alCerrar?.Invoke();
        });
    }

    static string Texto(string clave, string texto)
    {
        if (string.IsNullOrEmpty(clave) || LocalizationManager.Instance == null) return texto;
        return LocalizationManager.Instance.Get(clave, texto);
    }

    // Copia propia de la configuración (y del perfil, sin daño en sus ataques), capa Enemy y
    // golpes de práctica: el jugador ni el NPC pierden vida.
    void PrepararAlNpc()
    {
        var configuracion = _npc.Configuration;
        var original = R.combatConfig != null ? R.combatConfig : configuracion?.combatConfig;
        if (original != null && configuracion != null)
        {
            _copiaDeConfig = UnityEngine.Object.Instantiate(original);
            _copiaDeConfig.dialogueOnAlert = null;
            _copiaDeConfig.dialogueOnAlertRepeat = null;
            _copiaDeConfig.waitForAlertDialogue = false;
            _copiaDeConfig.freezePlayerOnAlert = false;
            _copiaDeConfig.sendEventOnDefeat = false;
            _copiaDeConfig.cerebro = TipoDeCerebroDeCombate.PorPuntuacion;
            _copiaDeConfig.spell1Damage = 0f;
            _copiaDeConfig.spell2Damage = 0f;
            _copiaDeConfig.spell3Damage = 0f;
            var perfil = R.perfil != null ? R.perfil : original.perfilDeCombate;
            if (perfil != null)
            {
                _copiaDePerfil = UnityEngine.Object.Instantiate(perfil);
                for (int i = 0; i < _copiaDePerfil.acciones.Count; i++)
                {
                    var accion = _copiaDePerfil.acciones[i];
                    if (accion == null) continue;
                    accion.dano = 0f;
                    if (R.segundosEntreHechizos > 0f)
                    {
                        accion.enfriamiento = R.segundosEntreHechizos;
                        accion.segundosDeRecuperacion = Mathf.Min(accion.segundosDeRecuperacion, R.segundosEntreHechizos * 0.5f);
                    }
                    if (R.segundosDeAviso > 0f) accion.segundosDeAviso = R.segundosDeAviso;
                }
                // Una lluvia no espera: sin pausas de paciencia entre hechizo y hechizo.
                if (R.segundosEntreHechizos > 0f && R.segundosEntreHechizos <= 1.5f)
                {
                    _copiaDePerfil.caracter.agresividad = 1f;
                    _copiaDePerfil.caracter.paciencia = 0f;
                }
                _copiaDePerfil.fases.Clear();
            }
            _copiaDeConfig.perfilDeCombate = _copiaDePerfil;
            _configDeAntes = configuracion.combatConfig;
            configuracion.combatConfig = _copiaDeConfig;
        }

        var dmg = _npc.GetComponent<Damageable>();
        if (dmg == null) dmg = _npc.gameObject.AddComponent<Damageable>();
        _receptor = dmg;
        float hp = _copiaDeConfig != null ? _copiaDeConfig.health : dmg.Max;
        dmg.SetMaxAndCurrent(hp, hp);
        dmg.SetDestroyOnDeath(false);

        var ciclo = _npc.GetComponent<NPCCombatLifecycleHandler>();
        if (!ciclo) _npc.gameObject.AddComponent<NPCCombatLifecycleHandler>();
        else if (ciclo.IsDefeatedAndInactive) ciclo.Resurrect();

        _golpes = _npc.GetComponent<GolpesDePractica>();
        if (_golpes == null) { _golpes = _npc.gameObject.AddComponent<GolpesDePractica>(); _golpesPuestos = true; }
        _golpes.SinDano = true;
        _golpes.enabled = true;
        dmg.RefrescarFiltros();

        _animator = _npc.GetComponentInChildren<Animator>();

        int capaEnemigo = LayerMask.NameToLayer("Enemy");
        if (capaEnemigo >= 0) _npc.gameObject.layer = capaEnemigo;
    }

    // En paz, con su configuración de siempre y sin la regla de práctica.
    void DejarAlNpc()
    {
        if (_npc == null) return;
        _comboVisual?.CerrarParaNpc(false);
        _actor?.EndAgentOverride();
        _actor?.StopMovement();
        _actor?.Release();
        _actor = null;
        if (_escudo != null)
        {
            _escudo.StopDefending();
            _escudo.ConfigurarElemento(null);
            if (_prefabDeEscudoCambiado) _escudo.CambiarPrefab(_prefabDeEscudoDeAntes);
            _prefabDeEscudoCambiado = false;
            _escudo = null;
        }
        if (_animator != null && _animator.layerCount > CapaDeMedioCuerpo) _animator.SetLayerWeight(CapaDeMedioCuerpo, 0f);
        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        if (capaInteractuable >= 0) _npc.gameObject.layer = capaInteractuable;
        _npc.ForceIdle();

        if (_golpes != null)
        {
            _golpes.enabled = false;
            if (_golpesPuestos) UnityEngine.Object.Destroy(_golpes);
            _golpes = null;
            _golpesPuestos = false;
        }

        var configuracion = _npc.Configuration;
        if (_copiaDeConfig != null && configuracion != null && configuracion.combatConfig == _copiaDeConfig)
            configuracion.combatConfig = _configDeAntes;
        if (_copiaDeConfig != null) UnityEngine.Object.Destroy(_copiaDeConfig);
        if (_copiaDePerfil != null) UnityEngine.Object.Destroy(_copiaDePerfil);
        _copiaDeConfig = null;
        _copiaDePerfil = null;

        var dmg = _npc.GetComponent<Damageable>();
        if (dmg != null && dmg.IsAlive) dmg.SetMaxAndCurrent(dmg.Max, dmg.Max);
    }
}
