using System;
using UnityEngine;
using Game.NPC; // Para NPCBehaviourManagerV2 (PersistenceId)
using Game.NPC.Modules; // Para acceder a los Executors

namespace Game.NPC.Common
{
    /// <summary>
    /// El "Cerebro" de la FSM. Gestiona las transiciones de estado y delega la interacción.
    /// No contiene lógica de juego, solo lógica de flujo.
    /// </summary>
    public class NPCBrain
    {
        // Eventos
        public event Action<INPCState, INPCState> OnStateChanged;

        // Estado interno
        private INPCState _currentState;
        private INPCState _previousState;
        private readonly NPCStateContext _context;

        // Propiedades públicas
        public INPCState CurrentState => _currentState;
        public INPCState PreviousState => _previousState;
        public NPCStateContext Context => _context;

        public NPCBrain(NPCStateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // =================================================================================
        // 🔄 STATE MANAGEMENT
        // =================================================================================

        public void ChangeState(INPCState newState)
        {
            if (newState == null)
            {
                _context.LogError("[NPCBrain] Intento de cambiar a estado NULL. Cancelado.");
                return;
            }

            // 1. Salir del estado anterior
            if (_currentState != null)
            {
                // _context.Log($"[FSM] Salida: {_currentState.StateName}");
                try
                {
                    _currentState.OnExit(_context);
                }
                catch (Exception ex)
                {
                    _context.LogError($"[NPCBrain] ❌ Error en OnExit ({_currentState.StateName}): {ex.Message}");
                }
            }

            // 2. Cambiar referencia
            _previousState = _currentState;
            _currentState = newState;

            // 3. Entrar al estado nuevo
            // _context.Log($"[FSM] Entrada: {_currentState.StateName}");
            try
            {
                _currentState.OnEnter(_context);
                OnStateChanged?.Invoke(_previousState, _currentState);
            }
            catch (Exception ex)
            {
                _context.LogError($"[NPCBrain] ❌ Error en OnEnter ({_currentState.StateName}): {ex.Message}");
                // Fallback de seguridad: Si falla el Enter, volver a Idle para no romper la IA
                if (!(_currentState is Game.NPC.States.IdleState))
                {
                    ChangeState(new Game.NPC.States.IdleState());
                }
            }
        }

        public void Update()
        {
            if (_currentState == null) return;

            try
            {
                // 1. Ejecutar lógica del frame
                _currentState.OnUpdate(_context);

                // 2. Verificar si el estado quiere cambiar
                var nextState = _currentState.CheckTransitions(_context);
                
                if (nextState != null && nextState.GetType() != _currentState.GetType())
                {
                    ChangeState(nextState);
                }
            }
            catch (Exception ex)
            {
                _context.LogError($"[NPCBrain] ❌ Error Crítico en Update ({_currentState.StateName}): {ex.Message}");
            }
        }

        public void ForceState(INPCState newState)
        {
            ChangeState(newState);
        }

        public bool ReturnToPreviousState()
        {
            if (_previousState == null) return false;
            ChangeState(_previousState);
            return true;
        }

        // =================================================================================
        // 🤝 INTERACTION HANDLING
        // =================================================================================

        /// <summary>
        /// Centraliza la lógica de interacción. El Brain decide qué subsistema responde.
        /// </summary>
        /// <summary>Da prioridad al grafo sin activar encargos o servicios del NPC.</summary>
        /// <summary>Avisa al grafo de la charla. True si el grafo la estaba esperando y se queda con ella.</summary>
        public bool TryHandleNarrativeInteraction()
        {
            if (_context?.Config == null) return false;
            return EmitirInteraccionNarrativa(out _);
        }

        bool EmitirInteraccionNarrativa(out string actorId)
        {
            var manager = _context.Transform.GetComponent<NPCBehaviourManagerV2>();
            actorId = manager != null ? manager.PersistenceId : null;
            if (string.IsNullOrEmpty(actorId)) return false;
            string clave = WaitNpcInteractionNode.SignalKeyFor(actorId);
            var senales = DefaultNarrativeSignals.Instance;
            // La reserva se consulta antes de emitir: el oyente puede retirarse al recibirla.
            bool reservada = senales != null && senales.HasCustomListener(clave);
            senales?.RaiseCustom(clave, $"[NPCBrain] Interacción con {_context.Transform.name}");
            return reservada;
        }
        public bool HandleInteraction(GameObject interactor)
        {
            if (_context?.Config == null) return false;
            var config = _context.Config;

            bool grafoEsperaEstaCharla = EmitirInteraccionNarrativa(out string actorId);

            // PRIORIDAD 1: COMBATE (Post-Derrota)
            // Si el NPC fue derrotado, tiene prioridad sobre cualquier quest o narrativa normal.
            if (config.HasBehaviour(NPCBehaviourType.Combat))
            {
                var lifecycle = _context.Transform.GetComponent<NPCCombatLifecycleHandler>();
                
                // Si existe el componente y el NPC está derrotado
                if (lifecycle != null && lifecycle.IsDefeatedAndInactive)
                {
                    if (lifecycle.HandlePostDefeatInteraction(interactor))
                    {
                        return true; // Interacción manejada por el sistema de muerte
                    }
                }
            }

            // Una charla del grafo tiene un único propietario. Los encargos congelados no
            // abren otro diálogo encima de la conversación ni de su frase posterior.
            if (grafoEsperaEstaCharla) return true;
            if (NarrativeStandingLines.TryPlay(actorId, _context.Transform)) return true;

            // PRIORIDAD 2: NARRATIVA INTERACTIVA (Sistema Principal)
            // Este es el flujo normal para hablar con NPCs
            if (config.interactiveNarrativeConfig != null)
            {
                var executor = _context.Transform.GetComponent<NPCInteractiveNarrativeExecutor>();
                if (executor != null)
                {
                    // Intentamos ejecutar. Si devuelve true, es que la narrativa arrancó.
                    if (executor.TryExecuteNarrative())
                    {
                        // IMPORTANTE: No seteamos IsInteracting = true aquí manualmente.
                        // El Executor iniciará una secuencia Cinemática que cambiará el estado del Brain a CinematicState.
                        return true; 
                    }
                }
            }

            // PRIORIDAD 3: QUESTS (Sistema Legacy o Simple)
            if (config.HasBehaviour(NPCBehaviourType.Quest) && config.questConfig != null)
            {
                // Para el sistema de Quest simple, sí marcamos el flag para pausar el movimiento
                _context.IsInteracting = true;
                bool questHandled = config.questConfig.ProcessInteraction(interactor, _context);
                
                if (questHandled) return true;
                _context.IsInteracting = false; // Revertir si falló
            }

            return false;
        }
        
        /// <summary>
        /// Método auxiliar para finalizar interacciones simples que no usan estados cinemáticos
        /// </summary>
        public void EndInteraction()
        {
            _context.IsInteracting = false;
        }
    }
}
