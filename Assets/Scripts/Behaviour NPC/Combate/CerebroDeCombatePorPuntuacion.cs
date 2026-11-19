using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Cerebro de combate de NPC que decide por puntuación (utility AI). Cada pocas décimas mira la
    /// situación (<see cref="SituacionDeCombate"/>), puntúa todas sus opciones (atacar con cada
    /// acción del catálogo, esquivar, escudo, cubrirse y recargar, recolocarse, rodear, buscar,
    /// esperar) y ejecuta la mejor. Lo que está haciendo se puede cambiar a medias por algo
    /// claramente mejor (no en pleno ataque ni en plena esquiva), sin frenos fijos entre estados.
    /// En grupo se reparte con <see cref="CoordinacionDeGrupo"/>: el que tiene el turno de
    /// <see cref="NPCAttackCoordinator"/> presiona y los demás rodean.
    /// Convive con <see cref="NPCCombatBrain"/>: lo usa el NPC cuya Combat Config dice
    /// <see cref="TipoDeCerebroDeCombate.PorPuntuacion"/>; su perfil, <see cref="PerfilDeCombate"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    public sealed class CerebroDeCombatePorPuntuacion : MonoBehaviour, ICerebroDeCombate, ICombatiente
    {
        private const int CapaDeMedioCuerpo = 1;
        private const float IntervaloDeSondeo = 0.1f;
        private const float Inercia = 0.15f;
        private const float RadioDeHechizos = 10f;
        private const float SegundosDeDevoluciones = 20f;
        private const float EpsilonDeMana = 0.001f;

        [Tooltip("Escribe en consola cada cambio de decisión (solo Editor).")]
        [SerializeField] private bool registrarDecisiones;

        [Tooltip("Solo lectura: lo que está haciendo ahora (depuración).")]
        [SerializeField] private string decisionActual;

        // Dependencias
        private NPCBehaviourManagerV2 _manager;
        private NPCStateContext _ctx;
        private NavMeshAgent _agente;
        private NPCSimpleAnimator _animador;
        private Animator _animator;
        private Damageable _vida;
        private NPCShieldController _escudo;
        private NPCCombatLifecycleHandler _ciclo;
        private Transform _boca;
        private NavMeshPath _camino;       // para probar puntos
        private NavMeshPath _caminoEnUso;  // el que sigue el agente
        private SondeoDelTerreno _sondeo;
        private LanzadorDeAcciones _lanzador;
        private readonly MemoriaDeCombate _memoria = new MemoriaDeCombate();

        // Combate en curso
        private PerfilEnUso _perfil;
        private readonly List<OpcionDeCombate> _opciones = new List<OpcionDeCombate>();
        private OpcionDeCombate _actual;
        private float[] _enfriamientos = Array.Empty<float>();
        private SituacionDeCombate _s;
        private Transform _objetivo;
        private bool _activo;
        private bool _incapacitado;
        private bool _aCubierto;
        private bool _moviendo;
        private float _mana;
        private float _ultimoGasto = -999f;
        private float _escudoListoEn;
        private float _esquivaListaEn;
        private float _siguienteDecision;
        private float _siguienteSondeo;
        private float _defendiendoDesde = -1f;

        private readonly List<Transform> _amenazas = new List<Transform>();
        private readonly List<Transform> _companerosT = new List<Transform>();
        private readonly List<CerebroDeCombatePorPuntuacion> _companeros = new List<CerebroDeCombatePorPuntuacion>();
        private readonly List<GameObject> _grupo = new List<GameObject>();

        public event Action<float> OnManaChanged;

        // --- Para CoordinacionDeGrupo ---------------------------------------------------------
        public Transform Objetivo => _objetivo;
        public float SegundosDesdeMiAtaque => _memoria.SegundosDesdeMiAtaque;
        public int ClaveDeOrden => GetHashCode();

        // --- ICerebroDeCombate ----------------------------------------------------------------

        public void Inicializar(NPCBehaviourManagerV2 manager)
        {
            _manager = manager;
            _ctx = manager.Context;
            _agente = _ctx.Agent;
            _animador = _ctx.Animator;
            _animator = _ctx.UnityAnimator;
            _escudo = GetComponent<NPCShieldController>();
            _ciclo = GetComponent<NPCCombatLifecycleHandler>();

            if (_vida != null) _vida.OnDamaged -= AlDanarse;
            _vida = GetComponent<Damageable>();
            if (_vida != null) _vida.OnDamaged += AlDanarse;

            if (_agente != null)
            {
                _agente.updateRotation = false;
                _agente.updatePosition = true;
                _agente.acceleration = 12f;
            }
            if (_camino == null) _camino = new NavMeshPath();
            if (_caminoEnUso == null) _caminoEnUso = new NavMeshPath();
            if (_sondeo == null) _sondeo = new SondeoDelTerreno(transform);
            if (_boca == null)
            {
                var boca = new GameObject("BocaDeCombate").transform;
                boca.SetParent(transform, false);
                boca.localPosition = new Vector3(0f, 1.5f, 0.9f);
                _boca = boca;
            }
            if (_lanzador == null)
            {
                var carga = GetComponent<CargaDeDisparo>();
                if (carga == null) carga = gameObject.AddComponent<CargaDeDisparo>();
                _lanzador = new LanzadorDeAcciones(gameObject, _boca, carga, Gastar, Enfriar, AnimarAtaque);
            }
        }

        public void Comenzar(NPCCombatConfig config)
        {
            if (_manager == null) return;
            if (_activo) Detener();

            _perfil = PerfilEnUso.Montar(config != null ? config.perfilDeCombate : null, config);
            MontarOpciones();

            _escudo = GetComponent<NPCShieldController>();
            _ciclo = GetComponent<NPCCombatLifecycleHandler>();
            if (_vida == null) { _vida = GetComponent<Damageable>(); if (_vida != null) _vida.OnDamaged += AlDanarse; }

            _memoria.Olvidar();
            _mana = _perfil.MaxMana;
            _ultimoGasto = -999f;
            _escudoListoEn = 0f;
            _esquivaListaEn = 0f;
            _aCubierto = false;
            _incapacitado = false;
            _actual = null;
            _s = default;

            if (_ctx.Player == null) _ctx.Player = PlayerService.PlayerTransform;
            ElegirObjetivo();
            if (_objetivo != null) _memoria.Visto(_objetivo.position);

            LecturaDelJugador.Asegurar();
            PlayerShieldController.AlDevolverAtaque -= AlDevolverleUnAtaque;
            PlayerShieldController.AlDevolverAtaque += AlDevolverleUnAtaque;
            _defendiendoDesde = -1f;
            CoordinacionDeGrupo.Entrar(this);
            _ctx.CerebroDeCombate = this;
            _animador.SetBattleMode(true);
            _activo = true;
            _siguienteDecision = 0f;
            _siguienteSondeo = 0f;
            NotificarMana();
        }

        public void Detener()
        {
            if (!_activo) return;
            _activo = false;
            TerminarLaActual();
            _lanzador?.Cancelar();
            Parar();
            BajarEscudo();
            _aCubierto = false;
            PlayerShieldController.AlDevolverAtaque -= AlDevolverleUnAtaque;
            if (_objetivo != null) NPCAttackCoordinator.Release(_objetivo, this);
            CoordinacionDeGrupo.Salir(this);
            CombatTargetProvider.Olvidar(this);
            if (_animador != null) _animador.SetBattleMode(false);
            decisionActual = string.Empty;
        }

        public void InterrumpirLanzamiento()
        {
            if (!_activo) return;
            _lanzador.Cancelar();
            TerminarLaActual();
            _siguienteDecision = 0f;
        }

        public void AlRecibirDano(Vector3 origen)
        {
            if (!_activo) return;
            // Sabe de dónde le han dado aunque no lo viera: lo busca por ahí.
            _memoria.Visto(origen);
        }

        public float GetCurrentMana() => _mana;
        public float GetMaxMana() => _perfil != null ? _perfil.MaxMana : 1f;
        public float GetManaPercent() => _perfil != null && _perfil.MaxMana > 0f ? _mana / _perfil.MaxMana : 0f;

        // --- ICombatiente ---------------------------------------------------------------------

        public Transform Transform => transform;
        public PerfilEnUso Perfil => _perfil;
        public MemoriaDeCombate Memoria => _memoria;
        public SondeoDelTerreno Sondeo => _sondeo;
        public IReadOnlyList<Transform> Amenazas => _amenazas;
        public IReadOnlyList<Transform> Companeros => _companerosT;
        public LanzadorDeAcciones Lanzador => _lanzador;

        private bool AgenteListo => _agente != null && _agente.enabled && _agente.isOnNavMesh;

        public bool MoverA(Vector3 destino, bool corriendo)
        {
            if (!AgenteListo) return false;
            if (!NavMesh.SamplePosition(destino, out NavMeshHit enMalla, 2f, NavMesh.AllAreas)) return false;
            if (!_agente.CalculatePath(enMalla.position, _caminoEnUso) || _caminoEnUso.status == NavMeshPathStatus.PathInvalid) return false;
            float velocidad = corriendo ? _perfil.Caracter.velocidadCorriendo : _perfil.Caracter.velocidadAndando;
            _agente.isStopped = false;
            _agente.speed = velocidad;
            _agente.SetPath(_caminoEnUso);
            _moviendo = true;
            _animador.SetMovementSpeed(NavMeshAgentUtility.FactorDeLocomocion(velocidad), 0.1f);
            return true;
        }

        public void Parar()
        {
            _moviendo = false;
            if (AgenteListo) NavMeshAgentUtility.HardStop(_agente);
            if (_animador != null) _animador.SetMovementSpeed(0f, 0.1f);
        }

        public bool HaLlegado(float margen)
        {
            if (!AgenteListo || !_moviendo) return true;
            if (_agente.pathPending) return false;
            return _agente.remainingDistance <= margen + _agente.stoppingDistance;
        }

        public bool PuntoAlcanzable(Vector3 punto, out Vector3 enMalla)
        {
            enMalla = punto;
            if (!AgenteListo || !NavMesh.SamplePosition(punto, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return false;
            enMalla = hit.position;
            return _agente.CalculatePath(enMalla, _camino) && _camino.status == NavMeshPathStatus.PathComplete;
        }

        public void Mirar(Vector3 punto) => _animador.FaceTarget(punto);

        public bool AccionLista(int indice) => indice >= 0 && indice < _enfriamientos.Length && _enfriamientos[indice] <= 0f;

        public bool PuedePagar(float coste) => _mana + EpsilonDeMana >= coste;

        public bool PedirTurno() => _objetivo == null || NPCAttackCoordinator.TryReserve(_objetivo, this, _perfil.Caracter.atacantesALaVez);

        public void SoltarTurno()
        {
            if (_objetivo != null) NPCAttackCoordinator.Release(_objetivo, this);
        }

        public bool EscudoListo => _escudo != null && Time.time >= _escudoListoEn;

        public void LevantarEscudo(float segundos)
        {
            if (_escudo == null) return;
            _escudo.StartDefending(segundos);
            _escudoListoEn = Time.time + segundos + _perfil.Defensas.enfriamientoDelEscudo;
        }

        public void BajarEscudo()
        {
            if (_escudo != null) _escudo.StopDefending();
        }

        public bool EsquivaLista => Time.time >= _esquivaListaEn;
        public void EsquivaHecha() => _esquivaListaEn = Time.time + _perfil.Defensas.recuperacionDeEsquiva;

        public bool ACubierto
        {
            get => _aCubierto;
            set => _aCubierto = value;
        }

        public bool BuscarCobertura(out Vector3 sitio) =>
            _sondeo.BuscarCobertura(_objetivo, _amenazas, _perfil.Defensas.radioDeCobertura, _agente, _camino, _memoria, _companerosT, out sitio);

        public Vector3 PuntoDeRodeo() =>
            _objetivo == null ? transform.position : CoordinacionDeGrupo.PuntoDeRodeo(this, _objetivo, _companeros, _perfil.Caracter.distanciaPreferida);

        public void AbandonarCombate()
        {
            if (_ctx != null) _ctx.IsInCombat = false;
        }

        // --- Bucle ----------------------------------------------------------------------------

        private void OnDisable()
        {
            if (_activo) Detener();
        }

        private void OnDestroy()
        {
            if (_vida != null) _vida.OnDamaged -= AlDanarse;
            PlayerShieldController.AlDevolverAtaque -= AlDevolverleUnAtaque;
        }

        private void Update()
        {
            if (!_activo) return;
            ActualizarIncapacidad();
            if (_incapacitado) return;
            if (_ciclo != null && _ciclo.IsStunned) return;

            // Con una charla abierta el jugador no puede moverse: se espera sin decidir nada.
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            {
                if (_actual != null) TerminarLaActual();
                if (_moviendo) Parar();
                return;
            }

            float dt = Time.deltaTime;
            RecargarMana(dt);
            for (int i = 0; i < _enfriamientos.Length; i++)
                if (_enfriamientos[i] > 0f) _enfriamientos[i] -= dt;
            _lanzador.Actualizar();

            if (Time.time >= _siguienteSondeo) Sondear();
            if (Time.time >= _siguienteDecision) Decidir();

            if (_actual != null && !_actual.Actualizar(in _s))
            {
                TerminarLaActual();
                Decidir();
            }

            if (_objetivo != null && (!_moviendo || HaLlegado(0.2f)))
                _animador.FaceTarget(_s.veAlObjetivo ? _objetivo.position : _memoria.UltimaPosicionVista);
        }

        private void Sondear()
        {
            _siguienteSondeo = Time.time + IntervaloDeSondeo;
            if (_objetivo == null) ElegirObjetivo();
            _s.objetivo = _objetivo;
            if (_objetivo == null) { _s.veAlObjetivo = false; _s.lineaDeFuego = false; return; }

            Vector3 yo = transform.position;
            Vector3 hacia = _objetivo.position - yo;
            _s.distancia = hacia.magnitude;
            _s.veAlObjetivo = _sondeo.Ve(yo, _objetivo);
            if (_s.veAlObjetivo) _memoria.Visto(_objetivo.position);
            hacia.y = 0f;
            Vector3 boca = SondeoDelTerreno.Ojos(yo) + (hacia.sqrMagnitude > 0.01f ? hacia.normalized * 0.6f : Vector3.zero);
            _s.lineaDeFuego = _s.veAlObjetivo && _sondeo.LineaDeFuego(boca, _objetivo);
            _s.segundosSinVer = _memoria.SegundosSinVer;

            _s.hechizoEntrante = _sondeo.HechizoEntrante(RadioDeHechizos, out _s.segundosHastaElHechizo, out _s.deDondeViene, out int id);
            if (_s.hechizoEntrante)
            {
                float probabilidad = Mathf.Lerp(0.2f, 0.75f, _perfil.Dificultad) * (0.6f + 0.4f * _perfil.Caracter.movilidad);
                _s.decideEsquivarlo = _memoria.DecideEsquivar(id, probabilidad);
            }
            else _s.decideEsquivarlo = false;
        }

        private void Decidir()
        {
            _siguienteDecision = Time.time + Mathf.Lerp(0.45f, 0.15f, _perfil.Dificultad);
            ElegirObjetivo();
            CompletarSituacion();

            // Quien está a media acción renueva su turno para que no caduque mientras ataca.
            if (_lanzador.Ocupado && _objetivo != null)
                NPCAttackCoordinator.TryReserve(_objetivo, this, _perfil.Caracter.atacantesALaVez);

            float ruido = 0.08f * (1f - _perfil.Dificultad);
            OpcionDeCombate mejor = null;
            float mejores = float.MinValue;
            for (int i = 0; i < _opciones.Count; i++)
            {
                var o = _opciones[i];
                float p = o.Puntuar(in _s);
                if (p <= 0f) continue;
                p += UnityEngine.Random.Range(-ruido, ruido);
                if (p > mejores) { mejores = p; mejor = o; }
            }
            if (mejor == null || mejor == _actual) return;

            if (_actual != null)
            {
                if (!_actual.PuedeInterrumpirse) return;
                if (mejores <= _actual.Puntuar(in _s) + Inercia) return;
                TerminarLaActual();
            }
            Empezar(mejor);
        }

        private void Empezar(OpcionDeCombate opcion)
        {
            _actual = opcion;
            decisionActual = opcion.Nombre;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (registrarDecisiones) Debug.Log($"[Cerebro:{name}] {opcion.Nombre} (rol {_s.rol}, maná {_s.mana:P0}, dist {_s.distancia:F1})");
#endif
            opcion.Empezar(in _s);
        }

        private void TerminarLaActual()
        {
            if (_actual == null) return;
            var a = _actual;
            _actual = null;
            a.Terminar();
        }

        private void CompletarSituacion()
        {
            _s.objetivo = _objetivo;
            _s.vida = _vida != null && _vida.Max > 0f ? _vida.Current / _vida.Max : 1f;
            _s.mana = GetManaPercent();
            _s.manaParaAlgunAtaque = false;
            for (int i = 0; i < _perfil.Acciones.Count; i++)
                if (PuedePagar(_perfil.Acciones[i].costeDeMana)) { _s.manaParaAlgunAtaque = true; break; }

            _s.jugadorCargando = LecturaDelJugador.Cargando;
            _s.jugadorMeApunta = LecturaDelJugador.ApuntaA(transform);
            _s.segundosDesdeQueLanzo = LecturaDelJugador.SegundosDesdeQueLanzo;
            _s.jugadorDefendiendo = LecturaDelJugador.Defendiendo;
            if (!_s.jugadorDefendiendo) _defendiendoDesde = -1f;
            else if (_defendiendoDesde < 0f) _defendiendoDesde = Time.time;
            _s.segundosDefendiendo = _defendiendoDesde < 0f ? 0f : Time.time - _defendiendoDesde;
            CoordinacionDeGrupo.TodosLosCuerpos(_grupo);
            _s.devolucionesRecientes = LecturaDelJugador.DevolucionesRecientes(_grupo, SegundosDeDevoluciones);

            CoordinacionDeGrupo.CompanerosContra(_objetivo, this, _companeros);
            _companerosT.Clear();
            for (int i = 0; i < _companeros.Count; i++) _companerosT.Add(_companeros[i].transform);
            _s.companeros = _companeros.Count;
            _s.rol = CoordinacionDeGrupo.Rol(this, _objetivo, _companeros, _perfil.Caracter.atacantesALaVez);
            _s.descansandoTurno = _s.companeros > 0 && Time.time < _memoria.DescansoDeTurnoHasta;

            Vector3 yo = transform.position;
            _s.encierro = _sondeo.Encierro(yo, 2.5f);
            CombatTargetProvider.ObjetivosEnPie(_amenazas);
            _s.amenazasQueMeVen = 0;
            for (int i = 0; i < _amenazas.Count; i++)
                if (_amenazas[i] != null && _sondeo.Ve(_amenazas[i].position, transform)) _s.amenazasQueMeVen++;
            _s.aCubierto = _aCubierto;

            _s.segundosDesdeMiAtaque = _memoria.SegundosDesdeMiAtaque;
            _s.segundosDesdeElGolpe = _memoria.SegundosDesdeElGolpe;
            _s.danoReciente = _memoria.DanoReciente(3f);
        }

        private void ElegirObjetivo()
        {
            var nuevo = CombatTargetProvider.ElegirObjetivo(transform.position, this);
            if (nuevo == null && _ctx != null) nuevo = _ctx.Player;
            if (nuevo == _objetivo) return;
            if (_objetivo != null) NPCAttackCoordinator.Release(_objetivo, this);
            _objetivo = nuevo;
        }

        private void MontarOpciones()
        {
            _opciones.Clear();
            var acciones = _perfil.Acciones;
            float pesoMedio = 0f;
            for (int i = 0; i < acciones.Count; i++) pesoMedio += acciones[i].peso;
            pesoMedio = acciones.Count > 0 ? pesoMedio / acciones.Count : 1f;

            _enfriamientos = new float[acciones.Count];
            for (int i = 0; i < acciones.Count; i++) _opciones.Add(new OpcionAtacar(this, i, acciones[i], pesoMedio));
            _opciones.Add(new OpcionEsquivar(this));
            _opciones.Add(new OpcionEscudo(this));
            _opciones.Add(new OpcionCubrirseYRecargar(this));
            _opciones.Add(new OpcionRecolocarse(this));
            _opciones.Add(new OpcionRodear(this));
            _opciones.Add(new OpcionBuscar(this));
            _opciones.Add(new OpcionEsperar(this));
        }

        // --- Recursos -------------------------------------------------------------------------

        private bool Gastar(float coste)
        {
            if (coste <= 0f) return true;
            if (_mana + EpsilonDeMana < coste) return false;
            _mana = Mathf.Max(0f, _mana - coste);
            _ultimoGasto = Time.time;
            NotificarMana();
            return true;
        }

        private void Enfriar(int indice, float segundos)
        {
            if (indice >= 0 && indice < _enfriamientos.Length) _enfriamientos[indice] = Mathf.Max(0f, segundos);
        }

        private void RecargarMana(float dt)
        {
            if (_mana >= _perfil.MaxMana || Time.time - _ultimoGasto < _perfil.EsperaParaRecargar) return;
            float ritmo = _perfil.RecargaDeMana * (_aCubierto ? _perfil.Defensas.recargaACubierto : 1f);
            float antes = _mana;
            _mana = Mathf.Min(_perfil.MaxMana, _mana + ritmo * dt);
            if (_mana > antes + 0.01f) NotificarMana();
        }

        private void NotificarMana() => OnManaChanged?.Invoke(GetManaPercent());

        private void AnimarAtaque(string estado)
        {
            if (_animator == null || string.IsNullOrEmpty(estado)) return;
            _animator.SetLayerWeight(CapaDeMedioCuerpo, 1f);
            _animator.Play(estado, CapaDeMedioCuerpo);
        }

        // Se lo atribuye a la última acción que lanzó: lanza de una en una y la siguiente no sale
        // hasta pasada la recuperación, así que casi siempre es la que vuela.
        private void AlDevolverleUnAtaque(GameObject lanzador)
        {
            if (!_activo || lanzador != gameObject || _lanzador == null) return;
            _memoria.Devuelta(_lanzador.UltimaAccionSalida);
        }

        private void AlDanarse(float cuanto)
        {
            if (!_activo) return;
            _memoria.Golpeado(cuanto, _memoria.UltimaPosicionVista);
        }

        // Aturdido o dormido (EstadosDeCombate): deja de decidir, se marea y al pasársele vuelve a
        // decidir desde cero. Ver INC-1165.
        private void ActualizarIncapacidad()
        {
            bool ahora = EstadosDeCombate.Incapacitado(gameObject);
            if (ahora == _incapacitado) return;
            _incapacitado = ahora;
            if (ahora)
            {
                TerminarLaActual();
                _lanzador.Cancelar();
                Parar();
                BajarEscudo();
                _animador.PlayDizzy();
            }
            else
            {
                _animador.PlayIdleNormal();
                _siguienteDecision = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_activo || _perfil == null || _objetivo == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_objetivo.position, _perfil.Caracter.distanciaPreferida);
            if (_s.rol == RolEnElGrupo.Rodea)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(PuntoDeRodeo(), 0.3f);
            }
        }
    }
}
