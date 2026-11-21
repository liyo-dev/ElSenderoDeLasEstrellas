using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Un vecino que no toma parte en una charla cercana hace algo con sentido mientras dura:
    /// se gira a mirar un momento, se da la vuelta y sigue de espaldas, o se aparta unos pasos
    /// fuera del plano (por el NavMesh) y espera allí con algún gesto. Al acabar la charla espera un
    /// poco, vuelve a su sitio si es de los que no pasean ni tienen oficio, y pasa a Idle, que le
    /// devuelve a lo suyo. Lo pone y lo avisa <see cref="TestigosDeCharla"/>; las reglas, en
    /// <see cref="ReaccionesAnteCharlas"/>.
    /// </summary>
    public class CederSitioALaCharlaState : NPCStateBase
    {
        public override string StateName => "CederSitioALaCharla";

        private enum Fase { Mirar, IrAfuera, Esperar, Retirarse, Volver, Terminado }

        private const float VelocidadDeGiro = 4f;
        private const float SegundosDeVistazo = 1.6f;
        private const float SegundosColocandose = 0.8f;

        private readonly TestigosDeCharla _charla;
        private readonly NPCBehaviourManagerV2 _npc;
        private readonly ReglaDeReaccion _regla;
        private readonly ReaccionesAnteCharlas _ajustes;
        private readonly bool _volverASuSitio;
        private ReaccionAnteCharla _reaccion;

        private Fase _fase;
        private float _temporizador;
        private float _duracion;
        private float _proximoGesto;
        private float _vistazoHasta;
        private float _esperaAlAcabar;
        private bool _seHaMovido;
        private Vector3 _origen;
        private Quaternion _rotacionDeOrigen;
        private Quaternion _orientacionAlEsperar;
        private float _velocidadOriginal = -1f;

        /// Lo que está haciendo ahora (puede pasar a Apartarse si entra en el escenario).
        public ReaccionAnteCharla Reaccion => _reaccion;

        /// Ya se ha apartado o va a hacerlo.
        public bool SeAparta => _reaccion == ReaccionAnteCharla.Apartarse
            || _reaccion == ReaccionAnteCharla.CuriosearYApartarse;

        public CederSitioALaCharlaState(TestigosDeCharla charla, NPCBehaviourManagerV2 npc,
            ReglaDeReaccion regla, ReaccionAnteCharla reaccion, ReaccionesAnteCharlas ajustes, bool volverASuSitio)
        {
            _charla = charla;
            _npc = npc;
            _regla = regla;
            _reaccion = reaccion;
            _ajustes = ajustes;
            _volverASuSitio = volverASuSitio;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            StopMovement(context);
            context.Animator?.TransitionToIdle();

            _origen = context.Transform.position;
            _rotacionDeOrigen = context.Transform.rotation;
            if (context.Agent != null) _velocidadOriginal = context.Agent.speed;

            switch (_reaccion)
            {
                case ReaccionAnteCharla.Curiosear:
                case ReaccionAnteCharla.CuriosearYApartarse:
                    EmpezarAMirar(context);
                    break;
                case ReaccionAnteCharla.DarLaEspalda:
                    EmpezarAEsperar(context, deEspaldas: true);
                    break;
                default:
                    EmpezarAApartarse(context);
                    break;
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            StopMovement(context);
            if (context.Agent != null && _velocidadOriginal >= 0f)
                context.Agent.speed = _velocidadOriginal;
            _charla?.Olvidar(_npc, this);
            base.OnExit(context);
        }

        // ── Avisos de TestigosDeCharla ──────────────────────────────────────────

        /// Ha entrado en el escenario de la charla: se aparta (si no lo estaba haciendo ya).
        public void Apartarse()
        {
            if (SeAparta || _fase == Fase.Terminado) return;
            _reaccion = ReaccionAnteCharla.Apartarse;
            if (_fase == Fase.Retirarse || _fase == Fase.Volver) return;
            EmpezarAApartarse(_npc.Context);
        }

        /// La charla ha acabado: tras <paramref name="espera"/> segundos retoma lo suyo.
        public void AcabarLaCharla(float espera)
        {
            if (_fase == Fase.Terminado || _fase == Fase.Retirarse || _fase == Fase.Volver) return;
            // Quien solo curioseaba termina su mirada y sigue: no hace falta esperar.
            if (_fase == Fase.Mirar && _reaccion == ReaccionAnteCharla.Curiosear) return;
            if (_fase == Fase.IrAfuera) StopMovement(_npc.Context);
            _fase = Fase.Retirarse;
            _temporizador = 0f;
            _esperaAlAcabar = espera;
        }

        /// Empieza otra charla justo después de la anterior: se queda donde está, esperando.
        public void SeguirEnLaCharla()
        {
            if (_fase != Fase.Retirarse && _fase != Fase.Volver) return;
            var context = _npc.Context;
            if (_fase == Fase.Volver) StopMovement(context);
            EmpezarAEsperar(context, deEspaldas: !SeAparta);
        }

        /// Ahora forma parte de la charla: deja de ceder el sitio ya.
        public void AcabarYa() => _fase = Fase.Terminado;

        // ── Fases ───────────────────────────────────────────────────────────────

        private void EmpezarAMirar(NPCStateContext context)
        {
            _fase = Fase.Mirar;
            _temporizador = 0f;
            _duracion = ReglaDeReaccion.Entre(_regla.segundosMirando);
            string gesto = ReglaDeReaccion.Elegir(_regla.gestosAlMirar);
            if (!string.IsNullOrEmpty(gesto)) context.Animator?.PlaySocialGesture(gesto);
        }

        private void EmpezarAApartarse(NPCStateContext context)
        {
            if (context.Agent != null
                && _charla != null
                && _charla.BuscarSitioFuera(context.Agent, context.Transform.position, out var destino)
                && SetDestination(context, destino))
            {
                float velocidad = context.Config?.ambientConfig != null
                    ? context.Config.ambientConfig.walkSpeed
                    : context.Config != null ? context.Config.walkSpeed : 1.5f;
                context.Agent.speed = Mathf.Max(0.1f, velocidad);
                _fase = Fase.IrAfuera;
                _temporizador = 0f;
                _seHaMovido = true;
                return;
            }

            // Sin sitio por el NavMesh al que ir: al menos no mira a la cámara ni a la charla.
            EmpezarAEsperar(context, deEspaldas: true);
        }

        private void EmpezarAEsperar(NPCStateContext context, bool deEspaldas)
        {
            _fase = Fase.Esperar;
            _temporizador = 0f;
            _vistazoHasta = 0f;
            _proximoGesto = ReglaDeReaccion.Entre(_regla.cadaCuantoUnGesto) * 0.5f;

            // De espaldas: la charla detrás, con un poco de variación. Apartado: de lado, como
            // quien está a otra cosa pero puede echar un vistazo.
            Vector3 desdeLaCharla = context.Transform.position - (_charla != null ? _charla.Centro : context.Transform.position);
            desdeLaCharla.y = 0f;
            if (desdeLaCharla.sqrMagnitude < 0.01f) desdeLaCharla = -context.Transform.forward;
            float giro = deEspaldas ? Random.Range(-25f, 25f) : Random.Range(55f, 110f) * (Random.value < 0.5f ? -1f : 1f);
            _orientacionAlEsperar = Quaternion.LookRotation(Quaternion.Euler(0f, giro, 0f) * desdeLaCharla.normalized);
        }

        private void EmpezarAVolver(NPCStateContext context)
        {
            if (SetDestination(context, _origen))
            {
                _fase = Fase.Volver;
                _temporizador = 0f;
                return;
            }
            _fase = Fase.Terminado;
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            _temporizador += Time.deltaTime;

            switch (_fase)
            {
                case Fase.Mirar:
                    GirarHacia(context, _charla != null ? _charla.Centro : context.Transform.position + context.Transform.forward);
                    if (_temporizador >= _duracion)
                    {
                        if (_reaccion == ReaccionAnteCharla.CuriosearYApartarse) EmpezarAApartarse(context);
                        else _fase = Fase.Terminado;
                    }
                    break;

                case Fase.IrAfuera:
                    UpdateMovementAnimation(context);
                    if (HasReachedDestination(context) || IsPathBlocked(context)
                        || _temporizador >= _ajustes.tiempoMaximoAndando)
                    {
                        StopMovement(context);
                        EmpezarAEsperar(context, deEspaldas: false);
                    }
                    break;

                case Fase.Esperar:
                    Esperar(context);
                    break;

                case Fase.Retirarse:
                    if (_temporizador >= _esperaAlAcabar)
                    {
                        if (_seHaMovido && _volverASuSitio && IsAgentValid(context)) EmpezarAVolver(context);
                        else _fase = Fase.Terminado;
                    }
                    break;

                case Fase.Volver:
                    if (_temporizador < _ajustes.tiempoMaximoAndando && !HasReachedDestination(context)
                        && !IsPathBlocked(context))
                    {
                        UpdateMovementAnimation(context);
                        break;
                    }
                    StopMovement(context);
                    context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation, _rotacionDeOrigen,
                        Time.deltaTime * VelocidadDeGiro);
                    if (Quaternion.Angle(context.Transform.rotation, _rotacionDeOrigen) < 3f
                        || _temporizador >= _ajustes.tiempoMaximoAndando + SegundosColocandose)
                        _fase = Fase.Terminado;
                    break;
            }
        }

        private void Esperar(NPCStateContext context)
        {
            if (Time.time < _vistazoHasta)
            {
                GirarHacia(context, _charla != null ? _charla.Centro : context.Transform.position);
                return;
            }

            context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation, _orientacionAlEsperar,
                Time.deltaTime * VelocidadDeGiro);

            if (_temporizador < _proximoGesto) return;
            _temporizador = 0f;
            _proximoGesto = ReglaDeReaccion.Entre(_regla.cadaCuantoUnGesto);

            if (Random.value < _regla.vistazoMientrasEspera)
            {
                _vistazoHasta = Time.time + SegundosDeVistazo;
                return;
            }
            string gesto = ReglaDeReaccion.Elegir(_regla.gestosEsperando);
            if (!string.IsNullOrEmpty(gesto)) context.Animator?.PlaySocialGesture(gesto);
        }

        private static void GirarHacia(NPCStateContext context, Vector3 punto)
        {
            Vector3 dir = punto - context.Transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation,
                Quaternion.LookRotation(dir), Time.deltaTime * VelocidadDeGiro);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.WasDefeatedInCombat) return new DeadState();
            if (context.IsInCombat) return new CombatState();
            if (context.IsInteracting) return new IdleState();
            if (context.EstaAnclado) return new IdleState();
            if (_fase == Fase.Terminado) return new IdleState();
            return null;
        }
    }
}
