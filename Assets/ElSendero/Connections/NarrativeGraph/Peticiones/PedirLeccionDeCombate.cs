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
        Runner.StartCoroutine(Co_Hechizos());
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

        while (!_parado && !_acabando)
        {
            while (_enCaja && !_parado) yield return null;
            if (_parado || _acabando) yield break;
            if (!PlayerService.TryGetPlayer(out jugador) || jugador == null) { yield return esperaCorta; continue; }

            Mirar(jugador.transform.position);
            Gesto();
            yield return gesto;
            if (_parado || _acabando) yield break;

            if (lluvia)
            {
                int n = Mathf.Max(1, R.hechizosPorTanda);
                for (int i = 0; i < n && !_parado && !_acabando; i++)
                {
                    while (_enCaja && !_parado) yield return null;
                    if (PlayerService.TryGetPlayer(out jugador) && jugador != null) LanzarDesdeArriba(jugador.transform.position);
                    yield return esperaEntre;
                }
                yield return descanso;
            }
            else
            {
                if (!PlayerService.TryGetPlayer(out jugador) || jugador == null) continue;
                var bola = LanzarBola(jugador.transform.position);
                float hasta = Time.time + 5f;
                while (bola != null && Time.time < hasta && !_parado) yield return null;
                yield return esperaEntre;
            }
        }
    }

    void Mirar(Vector3 donde)
    {
        if (_npc != null && _npc.SimpleAnimator != null) _npc.SimpleAnimator.FaceTarget(donde);
    }

    void Gesto()
    {
        if (_animator == null || string.IsNullOrEmpty(_animacion) || _animator.layerCount <= CapaDeMedioCuerpo) return;
        if (!_animator.HasState(CapaDeMedioCuerpo, Animator.StringToHash(_animacion))) return;
        _animator.SetLayerWeight(CapaDeMedioCuerpo, 1f);
        _animator.Play(_animacion, CapaDeMedioCuerpo, 0f);
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

    void LanzarDesdeArriba(Vector3 piesDelJugador)
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
    }

    void Preparar(GameObject bola, Vector3 direccion, bool sePuedeDevolver, float velocidad)
    {
        var proyectil = bola.GetComponentInChildren<EnemyProjectile>();
        if (proyectil == null) return;
        var rb = proyectil.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        proyectil.AsignarLanzador(_npc.gameObject);
        proyectil.PermitirDevolucion(sePuedeDevolver);
        if (velocidad > 0f) proyectil.SetSpeed(velocidad);
        proyectil.Initialize(direccion, 0f);
    }

    protected override void Stop()
    {
        _parado = true;
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
        if (_escudo != null)
        {
            _escudo.AlRomperse -= AlRomperElEscudo;
            _escudo.AlRebotar -= AlRebotarElemento;
        }
    }

    void AlParar(GameObject lanzador) { if (_npc != null && lanzador == _npc.gameObject) Acierto(); }
    void AlDevolver(GameObject lanzador) { if (_npc != null && lanzador == _npc.gameObject) Acierto(); }
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
