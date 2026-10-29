using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Game.NPC.Common;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// Vida del grupo mientras sigue al jugador (TDD, parte D.2): de vez en cuando un compañero
    /// suelta un comentario sin dejar de andar, se para un momento (con un gesto o sentándose) o se
    /// planta delante del jugador, y puede reaccionar a lo que hace otro compañero (decirle algo,
    /// oír su réplica y cortarle el numerito).
    ///
    /// Qué hace cada personaje son datos: los numeritos y las reacciones de este componente, con
    /// claves de localización. Las frases salen por <see cref="PresentadorDeLinea"/>.
    ///
    /// Solo actúa mientras el compañero va siguiendo al jugador con normalidad: en el grupo, sin
    /// que el jugador lo lleve (<see cref="ControlDelJugador"/>), con el equipo en modo Siguiendo,
    /// en FollowPlayerState, sobre el NavMesh y sin menús abiertos. Lo que no es un comentario
    /// suelto corre como una secuencia de CinematicState y no de IdleState: NPCPartyMember vuelve a
    /// poner a seguir a cualquier compañero que vea en «Idle», y desharía el numerito a media
    /// frase; al terminar la secuencia, ese mismo sondeo le pone a seguir otra vez.
    ///
    /// Se monta en el GameObject del compañero (junto a NPCBehaviourManagerV2). A propósito sin
    /// [RequireComponent(typeof(NPCPartyMember))]: NPCBehaviourManagerV2 lo añade a mano con su
    /// configuración, y si Unity lo crease antes se quedaría sin ella (INC-149/INC-151).
    /// </summary>
    [RequireComponent(typeof(NPCBehaviourManagerV2))]
    public class ComentariosEnReposo : MonoBehaviour
    {
        [Header("Frecuencia")]
        [Tooltip("Segundos mínimos entre numeritos mientras sigue al jugador con normalidad.")]
        [FormerlySerializedAs("minInterval")]
        [SerializeField] protected float intervaloMinimo = 30f;
        [Tooltip("Segundos máximos entre numeritos mientras sigue al jugador con normalidad.")]
        [FormerlySerializedAs("maxInterval")]
        [SerializeField] protected float intervaloMaximo = 60f;

        [Tooltip("Segundos mínimos que está cada frase en pantalla (nunca menos de lo que se tarda en leerla ni de lo que dura su voz).")]
        [FormerlySerializedAs("bubbleDuration")]
        [SerializeField] protected float duracionDeLaFrase = 3f;

        [Tooltip("Lo que puede hacer por su cuenta. Cada vez toca uno al azar, según su peso.")]
        [SerializeField] protected List<NumeritoEnReposo> numeritos = new();

        [Tooltip("Cómo responde cuando otro compañero empieza un numerito con cierta etiqueta.")]
        [SerializeField] protected List<ReaccionEnReposo> reacciones = new();

        [Tooltip("Escribe en la consola cada numerito que empieza (solo en el editor y en builds de desarrollo).")]
        [FormerlySerializedAs("debugMode")]
        [SerializeField] protected bool depurar;

        /// Un compañero acaba de empezar un numerito con etiqueta. Lo escuchan los demás para
        /// reaccionar (ver <see cref="ReaccionEnReposo"/>).
        public static event Action<ComentariosEnReposo, string> NumeritoEmpezado;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { NumeritoEmpezado = null; }
#endif

        private NPCPartyMember _miembro;
        private NPCBehaviourManagerV2 _npc;
        private float _siguienteNumerito;
        private bool _relojArmado;
        private AccionDeNumerito _accionEnCurso;
        private readonly Dictionary<ReaccionEnReposo, float> _siguienteReaccion = new();

        /// Corta ya el numerito que esté haciendo (lo pide otro compañero al reaccionar). Seguro de
        /// llamar siempre: si no está haciendo ninguno, no pasa nada.
        public void CortarNumerito() => _accionEnCurso?.Cortar();

        // NPCPartyMember se añade dentro del Awake de NPCBehaviourManagerV2, y Unity no garantiza
        // el orden de los Awake de un mismo GameObject: se cachea en Start, cuando ya han corrido todos.
        protected virtual void Start()
        {
            _miembro = GetComponent<NPCPartyMember>();
            _npc = GetComponent<NPCBehaviourManagerV2>();
        }

        protected virtual void OnEnable() => NumeritoEmpezado += AlEmpezarOtroNumerito;
        protected virtual void OnDisable() => NumeritoEmpezado -= AlEmpezarOtroNumerito;

        protected virtual void Update()
        {
            if (!PuedeActuar())
            {
                // Se rearma al volver a cumplir las condiciones (tras un combate, por ejemplo).
                _relojArmado = false;
                return;
            }

            if (!_relojArmado)
            {
                ArmarReloj();
                return;
            }

            if (Time.time < _siguienteNumerito) return;
            HacerNumerito(Elegir());
            ArmarReloj();
        }

        private void ArmarReloj()
        {
            _relojArmado = true;
            float lo = Mathf.Min(intervaloMinimo, intervaloMaximo);
            float hi = Mathf.Max(intervaloMinimo, intervaloMaximo);
            _siguienteNumerito = Time.time + UnityEngine.Random.Range(lo, hi);
        }

        /// Va siguiendo al jugador con total normalidad: cualquier otra cosa (combate, diálogo,
        /// cinemática, llevado por el jugador, modo Libre, fuera del NavMesh por ir volando,
        /// nadando o escalando junto al jugador, un menú abierto) le deja sin hacer nada.
        private bool PuedeActuar()
        {
            if (_miembro == null || _npc == null) return false;
            if (!_miembro.IsActiveInParty) return false;
            if (ControlDelJugador.RepresentadoPorElJugador(_miembro)) return false;
            if (!(PartyControlManager.Instance?.IsPartyFollowing ?? true)) return false;
            if (!(_npc.Brain?.CurrentState is FollowPlayerState)) return false;
            if (_npc.Agent == null || !_npc.Agent.isOnNavMesh) return false;
            return !MenuManager.AnyOpen();
        }

        private NumeritoEnReposo Elegir()
        {
            if (numeritos == null || numeritos.Count == 0) return null;
            float total = 0f;
            foreach (var n in numeritos) if (n != null) total += Mathf.Max(0f, n.peso);
            if (total <= 0f) return null;

            float tirada = UnityEngine.Random.Range(0f, total);
            foreach (var n in numeritos)
            {
                if (n == null) continue;
                tirada -= Mathf.Max(0f, n.peso);
                if (tirada <= 0f) return n;
            }
            return numeritos[numeritos.Count - 1];
        }

        private void HacerNumerito(NumeritoEnReposo numerito)
        {
            if (numerito == null) return;
            string clave = numerito.ClaveAlAzar();
            Log($"numerito {numerito.tipo} '{numerito.etiqueta}' con «{clave}»");

            switch (numerito.tipo)
            {
                case TipoDeNumerito.Comentario:
                    DecirFrase(transform, clave, duracionDeLaFrase);
                    break;

                case TipoDeNumerito.Pararse:
                    Empezar(new AccionPararse(numerito.duracion, numerito.gesto, numerito.actividad, clave, duracionDeLaFrase),
                        numerito.etiqueta);
                    break;

                case TipoDeNumerito.PlantarseDelante:
                    var jugador = _npc.Player;
                    if (jugador == null ||
                        !NavMesh.SamplePosition(jugador.position + jugador.forward * numerito.distanciaDelante,
                            out var punto, 3f, NavMesh.AllAreas))
                    {
                        // Sin sitio en el NavMesh delante del jugador (un muelle, un escalón): no se
                        // le manda a andar a ciegas, se queda en un comentario.
                        DecirFrase(transform, clave, duracionDeLaFrase);
                        break;
                    }
                    Empezar(new AccionPlantarseDelante(punto.position, jugador, numerito.duracion, clave,
                        duracionDeLaFrase, numerito.tiempoMaximoAndando), numerito.etiqueta);
                    break;
            }
        }

        private void Empezar(AccionDeNumerito accion, string etiqueta)
        {
            _accionEnCurso = accion;
            var secuencia = new CompositeSequence();
            secuencia.AddAction(accion);
            _npc.StartCinematicSequence(secuencia);
            if (!string.IsNullOrEmpty(etiqueta)) NumeritoEmpezado?.Invoke(this, etiqueta);
        }

        private void AlEmpezarOtroNumerito(ComentariosEnReposo otro, string etiqueta)
        {
            if (otro == this || otro == null || reacciones == null) return;
            foreach (var reaccion in reacciones)
            {
                if (reaccion == null || reaccion.etiquetaDelOtro != etiqueta) continue;
                if (!PuedeActuar()) return;
                if (_siguienteReaccion.TryGetValue(reaccion, out float siguiente) && Time.time < siguiente) continue;
                if (UnityEngine.Random.value > reaccion.probabilidad) continue;

                _siguienteReaccion[reaccion] = Time.time + reaccion.esperaEntreReacciones;
                string propia = AlAzar(reaccion.clavesPropias);
                string respuesta = AlAzar(reaccion.clavesDeRespuesta);
                Log($"reacciona a '{etiqueta}' de {otro.name}: «{propia}» → «{respuesta}»");
                Empezar(new AccionReaccionar(otro, propia, respuesta, duracionDeLaFrase,
                    reaccion.pausaHastaLaRespuesta, reaccion.pausaHastaCortar, reaccion.cortarSuNumerito), null);
                return;
            }
        }

        /// Dice una frase sobre ese personaje por el camino único de las frases.
        internal static void DecirFrase(Transform quien, string clave, float duracion)
        {
            if (quien == null || string.IsNullOrEmpty(clave)) return;
            PresentadorDeLinea.Decir(new FraseADecir { clave = clave, ancla = quien, duracion = duracion });
        }

        private static string AlAzar(string[] claves)
            => claves == null || claves.Length == 0 ? null : claves[UnityEngine.Random.Range(0, claves.Length)];

        private void Log(string que)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (depurar) Debug.Log($"[ComentariosEnReposo:{name}] {que}");
#endif
        }
    }

    public enum TipoDeNumerito
    {
        /// Una frase sin dejar de andar.
        Comentario,
        /// Se para en el sitio un rato: con un gesto, haciendo una actividad (sentarse...) o las dos.
        Pararse,
        /// Va hasta delante del jugador, se vuelve hacia él y se queda plantado un rato.
        PlantarseDelante,
    }

    /// Algo que un compañero hace por su cuenta mientras sigue al jugador.
    [Serializable]
    public class NumeritoEnReposo
    {
        [Tooltip("Nombre del numerito para que otros compañeros reaccionen a él (ver Reacciones). Vacío = nadie reacciona.")]
        public string etiqueta;
        public TipoDeNumerito tipo;
        [Tooltip("Peso relativo frente a los demás numeritos (no hace falta que sumen 100).")]
        [Min(0f)] public float peso = 1f;
        [Tooltip("Claves de localización de lo que puede decir; se elige una al azar.")]
        public string[] claves = Array.Empty<string>();

        [Header("Pararse / plantarse")]
        [Tooltip("Segundos que se queda parado, sentado o plantado antes de seguir andando.")]
        [Min(0.1f)] public float duracion = 3f;
        [Tooltip("Pararse: estado del Animator (capa UpperBody) del gesto que hace. Vacío = sin gesto.")]
        public string gesto;
        [Tooltip("Pararse: actividad que hace mientras está parado (sentarse en el suelo...). None = de pie.")]
        public NPCAmbientActivity actividad = NPCAmbientActivity.None;

        [Header("Plantarse delante")]
        [Tooltip("Distancia delante del jugador a la que intenta plantarse, en metros.")]
        [Min(0.5f)] public float distanciaDelante = 2.2f;
        [Tooltip("Segundos máximos andando hasta ese punto antes de rendirse y hablar desde donde esté.")]
        [Min(0.5f)] public float tiempoMaximoAndando = 6f;

        public string ClaveAlAzar()
            => claves == null || claves.Length == 0 ? null : claves[UnityEngine.Random.Range(0, claves.Length)];

        public static NumeritoEnReposo Comentario(float peso, params string[] claves)
            => new() { tipo = TipoDeNumerito.Comentario, peso = peso, claves = claves };

        public static NumeritoEnReposo Pararse(float peso, float duracion, string gesto,
            NPCAmbientActivity actividad, string etiqueta, params string[] claves)
            => new()
            {
                tipo = TipoDeNumerito.Pararse, peso = peso, duracion = duracion, gesto = gesto,
                actividad = actividad, etiqueta = etiqueta, claves = claves
            };

        public static NumeritoEnReposo PlantarseDelante(float peso, float duracion, float distanciaDelante,
            float tiempoMaximoAndando, params string[] claves)
            => new()
            {
                tipo = TipoDeNumerito.PlantarseDelante, peso = peso, duracion = duracion,
                distanciaDelante = distanciaDelante, tiempoMaximoAndando = tiempoMaximoAndando, claves = claves
            };
    }

    /// Cómo responde un compañero cuando otro empieza un numerito con cierta etiqueta: se para, se
    /// vuelve hacia él, le dice algo, el otro replica y, si se pide, el otro corta su numerito.
    [Serializable]
    public class ReaccionEnReposo
    {
        [Tooltip("Etiqueta del numerito del otro compañero al que reacciona.")]
        public string etiquetaDelOtro;
        [Tooltip("Probabilidad (0-1) de reaccionar cada vez: no siempre, para que no canse.")]
        [Range(0f, 1f)] public float probabilidad = 0.6f;
        [Tooltip("Segundos mínimos entre dos reacciones de este tipo.")]
        [Min(0f)] public float esperaEntreReacciones = 90f;
        [Tooltip("Claves de localización de lo que dice este compañero; se elige una al azar.")]
        public string[] clavesPropias = Array.Empty<string>();
        [Tooltip("Claves de localización de la réplica, que dice el otro; se elige una al azar. Vacío = no replica.")]
        public string[] clavesDeRespuesta = Array.Empty<string>();
        [Tooltip("Segundos entre su frase y la réplica del otro.")]
        [Min(0.1f)] public float pausaHastaLaRespuesta = 1.6f;
        [Tooltip("Segundos entre la réplica y el final de la reacción.")]
        [Min(0.1f)] public float pausaHastaCortar = 1f;
        [Tooltip("Al terminar, el otro corta su numerito (se levanta, sigue andando).")]
        public bool cortarSuNumerito = true;
    }

    /// Acción de numerito que otro compañero puede cortar antes de tiempo. Cleanup se llama
    /// termine como termine, así que cortar basta para deshacer lo que haya hecho.
    internal abstract class AccionDeNumerito : CinematicAction
    {
        internal void Cortar() => IsCompleted = true;

        protected static void Parar(NPCStateContext context)
        {
            if (context.Agent != null) NavMeshAgentUtility.HardStop(context.Agent);
            context.Animator?.ResetMovement();
        }
    }

    /// Se para en el sitio, hace su gesto o su actividad, dice su frase y espera un rato.
    internal sealed class AccionPararse : AccionDeNumerito
    {
        private readonly float _duracion;
        private readonly string _gesto;
        private readonly NPCAmbientActivity _actividad;
        private readonly string _clave;
        private readonly float _duracionDeLaFrase;
        private bool _empezada;
        private float _tiempo;

        public AccionPararse(float duracion, string gesto, NPCAmbientActivity actividad, string clave, float duracionDeLaFrase)
        {
            _duracion = Mathf.Max(0.1f, duracion);
            _gesto = gesto;
            _actividad = actividad;
            _clave = clave;
            _duracionDeLaFrase = duracionDeLaFrase;
        }

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;
            if (!_empezada)
            {
                _empezada = true;
                Parar(context);
                if (_actividad != NPCAmbientActivity.None) context.Animator?.PlayAmbientActivity(_actividad);
                if (!string.IsNullOrEmpty(_gesto)) context.Animator?.PlaySocialGesture(_gesto, null);
                ComentariosEnReposo.DecirFrase(context.Transform, _clave, _duracionDeLaFrase);
            }

            _tiempo += Time.deltaTime;
            if (_tiempo >= _duracion) IsCompleted = true;
        }

        public override void Cleanup(NPCStateContext context)
        {
            // Termine solo o lo corten (un combate, otro compañero), se levanta.
            if (_empezada && _actividad != NPCAmbientActivity.None)
                context.Animator?.StopAmbientActivity(_actividad);
        }
    }

    /// Anda hasta un punto delante del jugador, se vuelve hacia él, dice su frase y se queda
    /// plantado un rato, cortándole el paso.
    internal sealed class AccionPlantarseDelante : AccionDeNumerito
    {
        private const float ToleranciaDeLlegada = 0.15f;

        private readonly Vector3 _destino;
        private readonly Transform _jugador;
        private readonly float _duracion;
        private readonly string _clave;
        private readonly float _duracionDeLaFrase;
        private readonly float _tiempoMaximoAndando;
        private bool _andando;
        private bool _plantado;
        private float _tiempoAndando;
        private float _tiempoPlantado;

        public AccionPlantarseDelante(Vector3 destino, Transform jugador, float duracion, string clave,
            float duracionDeLaFrase, float tiempoMaximoAndando)
        {
            _destino = destino;
            _jugador = jugador;
            _duracion = Mathf.Max(0.1f, duracion);
            _clave = clave;
            _duracionDeLaFrase = duracionDeLaFrase;
            _tiempoMaximoAndando = Mathf.Max(0.5f, tiempoMaximoAndando);
        }

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;

            if (!_plantado)
            {
                var agente = context.Agent;
                if (!_andando)
                {
                    if (agente == null || !agente.isOnNavMesh)
                    {
                        // Sin agente con el que andar: habla desde donde está.
                        Plantarse(context);
                        return;
                    }
                    context.Animator?.TransitionToLocomotion();
                    NavMeshAgentUtility.SetDestination(agente, _destino);
                    _andando = true;
                }

                _tiempoAndando += Time.deltaTime;
                bool llegado = agente != null && agente.enabled && agente.isOnNavMesh && !agente.pathPending
                    && agente.remainingDistance <= agente.stoppingDistance + ToleranciaDeLlegada;
                if (llegado || _tiempoAndando >= _tiempoMaximoAndando)
                {
                    Plantarse(context);
                    return;
                }

                if (agente != null && context.Animator != null)
                {
                    context.Animator.SetMovementSpeed(NavMeshAgentUtility.FactorDeLocomocion(agente));
                    if (agente.velocity.sqrMagnitude > 0.01f)
                        context.Animator.FaceDirection(agente.velocity.normalized);
                }
                return;
            }

            _tiempoPlantado += Time.deltaTime;
            if (_tiempoPlantado >= _duracion) IsCompleted = true;
        }

        private void Plantarse(NPCStateContext context)
        {
            _plantado = true;
            Parar(context);
            if (_jugador != null)
            {
                Vector3 direccion = _jugador.position - context.Transform.position;
                direccion.y = 0f;
                if (direccion.sqrMagnitude > 0.01f) context.Animator?.FaceDirection(direccion.normalized);
            }
            ComentariosEnReposo.DecirFrase(context.Transform, _clave, _duracionDeLaFrase);
        }

        public override void Cleanup(NPCStateContext context) => Parar(context);
    }

    /// Corre en la secuencia de quien reacciona: se para mirando al otro, dice su frase, pone la
    /// réplica sobre el otro (sin tocar su secuencia: solo le sale el bocadillo encima) y, si se
    /// pide, le corta el numerito. Si el otro desaparece a medias, termina sin más.
    internal sealed class AccionReaccionar : AccionDeNumerito
    {
        private enum Fase { Hablar, EsperarRespuesta, Responder, EsperarCorte, Fin }

        private readonly ComentariosEnReposo _otro;
        private readonly string _clavePropia;
        private readonly string _claveDeRespuesta;
        private readonly float _duracionDeLaFrase;
        private readonly float _pausaHastaLaRespuesta;
        private readonly float _pausaHastaCortar;
        private readonly bool _cortarSuNumerito;
        private Fase _fase = Fase.Hablar;
        private float _tiempo;

        public AccionReaccionar(ComentariosEnReposo otro, string clavePropia, string claveDeRespuesta,
            float duracionDeLaFrase, float pausaHastaLaRespuesta, float pausaHastaCortar, bool cortarSuNumerito)
        {
            _otro = otro;
            _clavePropia = clavePropia;
            _claveDeRespuesta = claveDeRespuesta;
            _duracionDeLaFrase = duracionDeLaFrase;
            _pausaHastaLaRespuesta = Mathf.Max(0.1f, pausaHastaLaRespuesta);
            _pausaHastaCortar = Mathf.Max(0.1f, pausaHastaCortar);
            _cortarSuNumerito = cortarSuNumerito;
        }

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;

            // Mira al otro durante toda la reacción.
            if (_otro != null)
            {
                Vector3 direccion = _otro.transform.position - context.Transform.position;
                direccion.y = 0f;
                if (direccion.sqrMagnitude > 0.01f)
                    context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation,
                        Quaternion.LookRotation(direccion), Time.deltaTime * 5f);
            }

            switch (_fase)
            {
                case Fase.Hablar:
                    Parar(context);
                    ComentariosEnReposo.DecirFrase(context.Transform, _clavePropia, _duracionDeLaFrase);
                    _tiempo = 0f;
                    _fase = Fase.EsperarRespuesta;
                    break;

                case Fase.EsperarRespuesta:
                    _tiempo += Time.deltaTime;
                    if (_tiempo >= _pausaHastaLaRespuesta) _fase = Fase.Responder;
                    break;

                case Fase.Responder:
                    if (_otro != null) ComentariosEnReposo.DecirFrase(_otro.transform, _claveDeRespuesta, _duracionDeLaFrase);
                    _tiempo = 0f;
                    _fase = Fase.EsperarCorte;
                    break;

                case Fase.EsperarCorte:
                    _tiempo += Time.deltaTime;
                    if (_tiempo < _pausaHastaCortar) break;
                    if (_cortarSuNumerito && _otro != null) _otro.CortarNumerito();
                    _fase = Fase.Fin;
                    break;

                case Fase.Fin:
                    IsCompleted = true;
                    break;
            }
        }
    }
}
