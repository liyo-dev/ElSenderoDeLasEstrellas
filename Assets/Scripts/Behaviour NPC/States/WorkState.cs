using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Oficio ambiental: el NPC recoge una caja de la pila de origen, la lleva a la de destino,
    /// la deja y vuelve a por otra. La ruta y las pilas las define <see cref="NPCWorkRoute"/>.
    ///
    /// El estado no toca físicas ni objetos persistentes: solo mueve al NPC con su NavMeshAgent,
    /// reproduce las animaciones de coger/dejar y reparenta la caja de la pila a la mano y de
    /// vuelta. Si lo interrumpen (diálogo, combate, cinemática, party), devuelve la caja y sale.
    /// </summary>
    public class WorkState : NPCStateBase
    {
        public override string StateName => "Work";

        private enum Fase
        {
            IrARecoger,
            Recoger,
            IrAEntregar,
            Entregar,
            Pausa,
            IrAPuestoFinal
        }

        private readonly NPCWorkRoute _ruta;

        private Fase _fase;
        private float _temporizador;
        private float _tiempoSinProgreso;
        private Vector3 _ultimaPosicion;
        private Transform _carryPoint;
        private Transform _cajaEnMano;
        private bool _ocupada;
        private float _velocidad;
        private float _stoppingDistanceOriginal = -1f;

        public WorkState(NPCWorkRoute ruta)
        {
            _ruta = ruta;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            _ocupada = false;
            _cajaEnMano = null;
            _temporizador = 0f;
            _tiempoSinProgreso = 0f;

            if (_ruta == null || !_ruta.isActiveAndEnabled || !_ruta.EstaDisponible() ||
                !_ruta.TryOcupar(context.Transform))
            {
                context.Brain?.ChangeState(new IdleState());
                return;
            }

            var ambient = context.Config?.ambientConfig;
            if (ambient == null || !ambient.permitirTrabajo ||
                !string.Equals(ambient.puestoId, _ruta.puestoId))
            {
                _ruta.Soltar(context.Transform);
                context.Brain?.ChangeState(new IdleState());
                return;
            }

            _ocupada = true;
            _carryPoint = ObtenerOCrearCarryPoint(context);

            _velocidad = ambient.walkSpeed;

            if (context.Agent != null)
            {
                _stoppingDistanceOriginal = context.Agent.stoppingDistance;
                context.Agent.speed = Mathf.Max(0.1f, _velocidad);
            }

            IrARecoger(context);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (!_ocupada || _ruta == null || !_ruta.isActiveAndEnabled) return;

            // Mantener la caja estable horizontalmente (yaw del NPC) como en PlayerCarrySystem
            if (_cajaEnMano != null && _carryPoint != null)
            {
                float npcYaw = context.Transform.eulerAngles.y;
                float rotExtra = _ruta.rotacionCargaFrontal.y;
                _cajaEnMano.position = _carryPoint.position;
                _cajaEnMano.rotation = Quaternion.Euler(0f, npcYaw + rotExtra, 0f);
            }

            switch (_fase)
            {
                case Fase.IrARecoger:
                    ComprobarAtasco(context);
                    UpdateMovementAnimation(context);
                    if (HaLlegado(context, _ruta.PuntoRecogida)) LlegarARecoger(context);
                    break;

                case Fase.Recoger:
                    _temporizador += Time.deltaTime;
                    if (_temporizador >= _ruta.tiempoRecoger) CogerCajaYMarchar(context);
                    break;

                case Fase.IrAEntregar:
                    ComprobarAtasco(context);
                    UpdateMovementAnimation(context);
                    if (HaLlegado(context, _ruta.PuntoEntrega)) LlegarAEntregar(context);
                    break;

                case Fase.Entregar:
                    _temporizador += Time.deltaTime;
                    if (_temporizador >= _ruta.tiempoDejar) DejarCajaYEsperar(context);
                    break;

                case Fase.Pausa:
                    _temporizador += Time.deltaTime;
                    if (_temporizador >= _ruta.pausaAntesDeVolver) EmpezarSiguienteViaje(context);
                    break;

                case Fase.IrAPuestoFinal:
                    ComprobarAtasco(context);
                    UpdateMovementAnimation(context);
                    if (HaLlegado(context, _ruta.puntoAlTerminar)) LlegarAPuestoFinal(context);
                    break;
            }
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.WasDefeatedInCombat) return new DeadState();
            if (context.IsInCombat) return new CombatState();
            if (context.IsInteracting) return new IdleState();
            if (context.EstaAnclado) return new IdleState();

            var ambient = context.Config?.ambientConfig;
            if (ambient == null || !ambient.permitirTrabajo ||
                !string.Equals(ambient.puestoId, _ruta != null ? _ruta.puestoId : null))
                return new IdleState();

            if (context.ShouldSeekShelter && context.CurrentShelter == null &&
                context.Config != null && context.Config.canSeekShelter)
                return new SeekShelterState();

            if (context.PendingSocialPartner != null)
                return new NPCSocialEncounterState();

            if (_ruta == null || !_ruta.isActiveAndEnabled || !_ruta.EstaDisponible())
                return new IdleState();

            if (_fase == Fase.IrARecoger || _fase == Fase.IrAEntregar || _fase == Fase.IrAPuestoFinal)
            {
                if (IsPathBlocked(context)) return new IdleState();
                if (_tiempoSinProgreso > 3f) return new IdleState();
            }

            return null;
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_ruta != null)
            {
                if (_cajaEnMano != null) _ruta.DevolverCajaALaPila();
                if (_ocupada) _ruta.Soltar(context.Transform);
            }

            _cajaEnMano = null;
            _ocupada = false;

            SoltarPoseDeCarga(context);

            if (context.Agent != null && _stoppingDistanceOriginal >= 0f)
            {
                context.Agent.stoppingDistance = _stoppingDistanceOriginal;
                _stoppingDistanceOriginal = -1f;
            }

            StopMovement(context);
            context.Animator?.TransitionToIdle();
            base.OnExit(context);
        }

        // ── Fases ───────────────────────────────────────────────────────────────

        private void IrARecoger(NPCStateContext context)
        {
            _fase = Fase.IrARecoger;
            _temporizador = 0f;
            _tiempoSinProgreso = 0f;
            _ultimaPosicion = context.Transform.position;
            IrA(context, _ruta.PuntoRecogida);
        }

        private void IrA(NPCStateContext context, Transform punto)
        {
            if (punto == null)
            {
                context.Brain?.ChangeState(new IdleState());
                return;
            }

            if (context.Agent != null && context.Agent.enabled && context.Agent.isOnNavMesh)
            {
                context.Agent.speed = Mathf.Max(0.1f, _velocidad);
                context.Agent.stoppingDistance = 0.05f;
                // isStopped llama a Resume por dentro y revienta si el agente no está sobre el
                // NavMesh (recién salido de una cinemática, desactivado o fuera de la malla).
                context.Agent.isStopped = false;
            }

            if (!SetDestination(context, punto.position))
                context.Brain?.ChangeState(new IdleState());
        }

        private void LlegarARecoger(NPCStateContext context)
        {
            StopMovement(context);
            SoltarPoseDeCarga(context);
            MirarA(context, _ruta.PuntoRecogida);

            if (!string.IsNullOrEmpty(_ruta.animacionRecoger))
                context.Animator?.PlaySocialGesture(_ruta.animacionRecoger);

            _temporizador = 0f;
            _fase = Fase.Recoger;
        }

        private void CogerCajaYMarchar(NPCStateContext context)
        {
            if (_carryPoint != null)
            {
                _cajaEnMano = _ruta.CogerCaja(_carryPoint);
            }
            else
            {
                context.LogWarning("[WorkState] El NPC no tiene CarryPoint; sigue la rutina sin caja visible.");
            }

            if (_cajaEnMano != null) ActivarPoseDeCarga(context);

            _fase = Fase.IrAEntregar;
            _temporizador = 0f;
            _tiempoSinProgreso = 0f;
            _ultimaPosicion = context.Transform.position;
            IrA(context, _ruta.PuntoEntrega);
        }

        private void LlegarAEntregar(NPCStateContext context)
        {
            StopMovement(context);
            SoltarPoseDeCarga(context);
            MirarA(context, _ruta.PuntoEntrega);

            if (!string.IsNullOrEmpty(_ruta.animacionDejar))
                context.Animator?.PlaySocialGesture(_ruta.animacionDejar);

            _temporizador = 0f;
            _fase = Fase.Entregar;
        }

        private void DejarCajaYEsperar(NPCStateContext context)
        {
            _ruta.DejarCajaEnPila();
            _cajaEnMano = null;
            _temporizador = 0f;
            _fase = Fase.Pausa;
        }

        private void EmpezarSiguienteViaje(NPCStateContext context)
        {
            _ruta.SiguienteViaje();

            if (_ruta.EstaTerminada || !_ruta.QuedanCajas)
            {
                if (_ruta.puntoAlTerminar != null)
                {
                    IrAPuestoFinal(context);
                    return;
                }

                context.Brain?.ChangeState(new IdleState());
                return;
            }

            IrARecoger(context);
        }

        private void IrAPuestoFinal(NPCStateContext context)
        {
            _fase = Fase.IrAPuestoFinal;
            _temporizador = 0f;
            _tiempoSinProgreso = 0f;
            _ultimaPosicion = context.Transform.position;
            IrA(context, _ruta.puntoAlTerminar);
        }

        private void LlegarAPuestoFinal(NPCStateContext context)
        {
            StopMovement(context);
            MirarA(context, _ruta.puntoAlTerminar);
            if (_ocupada)
            {
                _ruta.Soltar(context.Transform);
                _ocupada = false;
            }
            context.Brain?.ChangeState(new IdleState());
        }

        // ── Utilidades ──────────────────────────────────────────────────────────

        /// <summary>
        /// Mantiene la pose de dos manos mientras el NPC camina con la caja. Vive en la capa
        /// UpperBody; la locomoción de la capa base sigue funcionando por debajo.
        /// </summary>
        private void ActivarPoseDeCarga(NPCStateContext context)
        {
            if (_ruta == null || string.IsNullOrEmpty(_ruta.poseDeCarga)) return;
            context.Animator?.HoldPose(_ruta.poseDeCarga);
        }

        /// <summary>Suelta la pose de carga sin devolver todo el personaje a idle.</summary>
        private void SoltarPoseDeCarga(NPCStateContext context)
        {
            context.Animator?.SoltarPoseDeCarga();
        }

        private void MirarA(NPCStateContext context, Transform punto)
        {
            if (punto == null) return;
            context.Transform.rotation = punto.rotation;
            context.Animator?.SyncTargetRotation();
        }

        private void ComprobarAtasco(NPCStateContext context)
        {
            if (IsAgentValid(context) && context.Agent.pathPending) return;

            float avance = Vector3.Distance(context.Transform.position, _ultimaPosicion);
            if (avance > 0.05f)
            {
                _ultimaPosicion = context.Transform.position;
                _tiempoSinProgreso = 0f;
                return;
            }

            _tiempoSinProgreso += Time.deltaTime;
        }

        /// <summary>
        /// Llegada propia del oficio: el NPC tiene que acercarse de verdad al punto para que la
        /// caja quede a mano.
        /// </summary>
        private bool HaLlegado(NPCStateContext context, Transform punto)
        {
            if (punto == null || !IsAgentValid(context)) return false;
            if (context.Agent.pathPending) return false;

            Vector3 a = context.Transform.position;
            Vector3 b = punto.position;
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude <= 0.09f; // 0,3 m
        }

        private Transform ObtenerOCrearCarryPoint(NPCStateContext context)
        {
            if (context.Transform == null) return null;
            var cp = context.Transform.Find("CarryPoint");
            if (cp == null)
            {
                var go = new GameObject("CarryPoint");
                cp = go.transform;
                cp.SetParent(context.Transform, false);
            }
            Vector3 offset = _ruta != null ? _ruta.offsetCargaFrontal : new Vector3(0f, 0.65f, 0.42f);
            cp.localPosition = offset;
            cp.localRotation = Quaternion.identity;
            return cp;
        }
    }
}
