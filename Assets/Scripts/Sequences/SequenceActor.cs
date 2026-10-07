using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Game.NPC.Common;

/// Envoltorio de un actor dentro de una secuencia cinemática.
///
/// Existe por una razón concreta: hoy la diferencia entre "animar a un NPC" y "animar a Will"
/// está repartida a mano por todo el proyecto (cada sequencer resuelve su propio
/// NPCSimpleAnimator o su propio PlayerDialogueAnimator, y SpeechBubbleUI.Show() tiene su propia
/// copia de esa decisión escondida dentro). Eso obliga a escribir cada beat dos veces, una para
/// NPCs y otra para el jugador, y es de donde salen la mitad de los bugs de "a Will no le sale la
/// animación". Aquí se resuelve UNA vez, al empezar la secuencia, y a partir de ahí todos los
/// beats llaman a los mismos métodos sin saber ni preguntar de qué tipo de actor se trata.
///
/// Los actores se nombran por ID de texto, nunca por referencia de escena:
///   - "Player" (constante PlayerId) → el jugador, vía PlayerLocator.
///   - cualquier otro → NPCRegistry.GetNPCByID(id), el mismo ID que ya usa el grafo narrativo
///     (p. ej. "NPC_Oliver", "NPC_Eldran").
/// Por eso una SequenceDefinition se puede escribir entera como texto, sin abrir el Editor.
public class SequenceActor
{
    private Renderer[] _renderersDeVisibilidad;

    /// Una escena va a mover al jugador ella misma: quien lo tuviera sujeto en un sitio (p. ej.
    /// dormido en una cama) tiene que soltarlo, o lo devolvería allí cada fotograma.
    public static event System.Action<SequenceActor> JugadorTomadoPorEscena;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarEstaticos() => JugadorTomadoPorEscena = null;
#endif

    public void AvisarDeQueLoTomaUnaEscena()
    {
        if (IsPlayer) JugadorTomadoPorEscena?.Invoke(this);
    }

    /// Muestra u oculta el actor sin pisar las mallas de expresión facial.
    public void SetRenderingVisible(bool visible)
    {
        if (Transform == null) return;
        _renderersDeVisibilidad ??= Transform.GetComponentsInChildren<Renderer>(true);
        foreach (var r in _renderersDeVisibilidad)
            if (r != null) r.forceRenderingOff = !visible;
    }

    public const string PlayerId = "Player";

    public string Id { get; private set; }
    public Transform Transform { get; private set; }
    public bool IsPlayer { get; private set; }

    public NPCBehaviourManagerV2 Manager { get; private set; }
    public NPCSimpleAnimator NpcAnimator { get; private set; }
    public PlayerDialogueAnimator PlayerAnimator { get; private set; }
    public NPCEmotionController Emotion { get; private set; }
    public NavMeshAgent Agent { get; private set; }

    /// Reserva de llegada compartida por los movimientos paralelos de la secuencia.
    public Vector3? DestinoReservado { get; set; }

    /// Candado que mantiene al NPC en CinematicState (y por tanto con su comportamiento ambiental
    /// Wander/Idle desactivado) mientras la secuencia lo controla a mano. Null = no retenido.
    private HoldSequence _hold;

    public bool IsHeld => _hold != null;

    /// Otro sistema ha puesto al NPC en otro estado mientras la secuencia lo retenía (por ejemplo,
    /// el grafo lo manda a guiar al jugador justo al acabar la escena). Al soltarlo, la secuencia
    /// no debe pararlo: lo que hace ahora ya no es suyo. Ver INC-461.
    public bool LoHaTomadoOtroSistema => _hold != null && _hold.Desalojada;

    /// Secuencia "vacía" de CinematicState: no hace nada cada frame, solo existe para que la FSM
    /// del NPC sepa que está ocupado y no vuelva a Idle/Wander por su cuenta. Mismo truco que
    /// usaba OliverSaludoSequencer a mano; aquí vive una sola vez para todas las secuencias.
    private class HoldSequence : Game.NPC.States.CinematicSequence
    {
        /// La FSM ha salido de este CinematicState sin que la secuencia lo soltara.
        public bool Desalojada { get; private set; }

        public override void Update(Game.NPC.Common.NPCStateContext context) { }
        public override void Cleanup(Game.NPC.Common.NPCStateContext context)
        {
            if (!IsCompleted) Desalojada = true;
        }
        public void Finish() => IsCompleted = true;
    }

    // ── Resolución ───────────────────────────────────────────────────────────

    /// Intenta resolver un actor por su ID. Devuelve false (con aviso en consola) si no existe,
    /// para que el beat que lo pidió pueda saltarse sin reventar toda la secuencia.
    public static bool TryResolve(string actorId, out SequenceActor actor)
    {
        actor = null;
        if (string.IsNullOrEmpty(actorId)) return false;

        if (actorId == PlayerId)
        {
            var playerTransform = PlayerLocator.ResolvePlayer();
            if (playerTransform == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning("[SequenceActor] No se ha podido resolver al jugador ('Player'). " +
                    "¿Está la escena arrancada desde Start.unity?");
#endif
                return false;
            }

            actor = new SequenceActor
            {
                Id = PlayerId,
                IsPlayer = true,
                Transform = playerTransform,
                PlayerAnimator = playerTransform.GetComponentInChildren<PlayerDialogueAnimator>(true),
                // FIX 16 sep 2026: Will lleva DOS NPCEmotionController en el mismo GameObject y
                // solo uno tiene los meshes de ojos/boca puestos — coger el primero a secas hacía
                // que su cara no cambiara nunca, en silencio. Ver EmotionControllerResolver.
                Emotion = EmotionControllerResolver.Resolve(playerTransform.gameObject),
                NpcAnimator = playerTransform.GetComponentInChildren<NPCSimpleAnimator>(true),
            };
            return true;
        }

        var manager = NPCRegistry.HasInstance ? NPCRegistry.Instance.GetNPCByID(actorId) : null;

#if UNITY_EDITOR
        // FUERA DE PLAY no existe NPCRegistry: lo llena cada NPC en su Awake. Sin esto, cualquier
        // herramienta de Editor que quiera resolver un plano (la previsualización de encuadres,
        // 'El Sendero/Cinemáticas/Capturar planos') no puede saber dónde está ningún actor, y la
        // única forma de ver un encuadre sería darle a Play y pillarlo al vuelo.
        //
        // Así que cuando no hay registro se buscan los NPCs de las escenas abiertas por su
        // Persistence ID, que es exactamente lo que el registro haría al arrancar. En Play esto no
        // se ejecuta nunca (el registro existe desde el primer frame), así que no puede colarse en
        // el juego ni costar nada en runtime.
        if (manager == null && !Application.isPlaying)
        {
            foreach (var candidato in Object.FindObjectsByType<NPCBehaviourManagerV2>(
                         FindObjectsInactive.Include))
            {
                if (candidato != null && candidato.PersistenceId == actorId) { manager = candidato; break; }
            }
        }
#endif

        if (manager == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[SequenceActor] No hay ningún NPC registrado con el ID '{actorId}'. " +
                "Revisa el Persistence ID del NPC en su prefab/escena, o si el NPC está presente en " +
                "esta escena. IDs registrados ahora mismo: " +
                (NPCRegistry.HasInstance ? string.Join(", ", NPCRegistry.Instance.GetAllRegisteredIDs()) : "(sin registro)"));
#endif
            return false;
        }

        actor = new SequenceActor
        {
            Id = actorId,
            IsPlayer = false,
            Transform = manager.transform,
            Manager = manager,
            NpcAnimator = manager.SimpleAnimator,
            Agent = manager.Agent,
            Emotion = EmotionControllerResolver.Resolve(manager.gameObject),
        };
        return true;
    }

    /// Crea un actor a partir de un Transform suelto, para cosas que no son personajes pero que la
    /// escena necesita nombrar: un proyectil en vuelo, un objeto que cae, un punto que se mueve.
    ///
    /// Sirve para que la cámara pueda encuadrarlos y para que un personaje pueda mirarlos, usando
    /// los mismos beats de siempre — un plano "siguiendo a Proyectil" es exactamente igual de
    /// escribible que uno "siguiendo a Oliver". Los registra un SequenceModule mientras el objeto
    /// existe (ver SequenceContext.RegisterActor).
    ///
    /// No tiene animador, ni cara, ni NavMeshAgent, así que los beats que piden esas cosas no
    /// hacen nada con él — sin romperse.
    public static SequenceActor ForTransform(string id, Transform transform, float eyeHeight = 0f)
    {
        if (transform == null) return null;
        return new SequenceActor
        {
            Id = id,
            Transform = transform,
            IsDynamic = true,
            _eyeHeight = Mathf.Max(0f, eyeHeight),
        };
    }

    /// Marca los actores que no son personajes de la escena, sino objetos que un módulo ha
    /// registrado sobre la marcha.
    public bool IsDynamic { get; private set; }

    // ── Animación ────────────────────────────────────────────────────────────

    /// Reproduce un gesto por nombre de estado del Animator. Funciona igual para NPCs y para el
    /// jugador: los dos Animator Controllers comparten casi todo el vocabulario (Laugh01,
    /// Cheer01/02, Angry01/02, TakeDamage_2, Dizzy_NoWeapon...) y PlayerDialogueAnimator.PlayGesture()
    /// resuelve por su cuenta en qué capa vive el estado, así que un gesto de cuerpo entero
    /// también funciona en Will.
    /// Mantiene una pose hasta que alguien la suelte. Ver NPCSimpleAnimator.HoldPose: un gesto es
    /// un disparo que acaba en idle, y hay poses —volar, sostener un hechizo— que tienen que durar.
    private Animator _animadorDeActuacion;
    private bool _animadorDeActuacionResuelto;
    public Animator AnimadorDeActuacion
    {
        get
        {
            if (!_animadorDeActuacionResuelto)
            {
                _animadorDeActuacionResuelto = true;
                _animadorDeActuacion = Transform != null ? Transform.GetComponentInChildren<Animator>(true) : null;
            }
            return _animadorDeActuacion;
        }
    }

    public bool TieneEstado(string nombre)
        => !string.IsNullOrEmpty(nombre) && AnimatorLayerUtil.ResolveLayer(
            AnimadorDeActuacion, nombre, AnimadorDeActuacion != null ? AnimadorDeActuacion.GetLayerIndex("UpperBody") : -1) >= 0;

    /// Evita que la conversación interrumpa desplazamientos o acciones completas.
    public bool PuedeEscuchar
    {
        get
        {
            if (Transform == null || !Transform.gameObject.activeInHierarchy || _agentOverridden ||
                (NpcAnimator != null && NpcAnimator.AllowManualMovement) || SosteniendoPose) return false;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh &&
                (Agent.pathPending || (!Agent.isStopped && Agent.hasPath) || Agent.velocity.sqrMagnitude > 0.01f)) return false;
            var animador = AnimadorDeActuacion;
            if (animador == null || animador.layerCount == 0) return true;
            return !EsAccionCompleta(animador.GetCurrentAnimatorStateInfo(0)) &&
                (!animador.IsInTransition(0) || !EsAccionCompleta(animador.GetNextAnimatorStateInfo(0)));
        }
    }

    private static bool EsAccionCompleta(AnimatorStateInfo estado)
        => estado.IsTag("ActuacionCuerpoEntero") || estado.IsName("Dance_NoWeapon") ||
           estado.IsName("Dizzy_NoWeapon") || estado.IsName("Sleeping_NoWeapon") ||
           estado.IsName("Loot01_Begin") || estado.IsName("Loot01_Loop") || estado.IsName("Loot01_Stop") ||
           estado.IsName("Baile_A") || estado.IsName("Baile_B");

    public System.Collections.IEnumerator GirarSuavemente(Vector3 punto, float segundos)
    {
        if (Transform == null) yield break;
        Vector3 direccion = punto - Transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) yield break;
        Quaternion origen = Transform.rotation;
        Quaternion destino = Quaternion.LookRotation(direccion.normalized, Vector3.up);
        float transcurrido = 0f;
        while (transcurrido < segundos)
        {
            if (Transform == null) yield break;
            transcurrido += Time.unscaledDeltaTime;
            Transform.rotation = Quaternion.Slerp(origen, destino, Mathf.Clamp01(transcurrido / segundos));
            SyncRotation();
            yield return null;
        }
        if (Transform != null) { Transform.rotation = destino; SyncRotation(); }
    }

    private Coroutine _poseDelJugador;
    public void HoldPose(string stateName, bool congelarAlFinal = false)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        if (NpcAnimator != null) { NpcAnimator.HoldPose(stateName, congelarAlFinal: congelarAlFinal); return; }

        // La pose del jugador cancela el gesto puntual para que su cierre no la interrumpa.
        if (PlayerAnimator == null || !PlayerAnimator.isActiveAndEnabled || !TieneEstado(stateName)) return;
        ReleasePose(false);
        PlayerAnimator.CancelarGesto();
        var animador = AnimadorDeActuacion;
        int capa = AnimatorLayerUtil.ResolveLayer(animador, stateName, animador.GetLayerIndex("UpperBody"));
        if (capa > 0) animador.SetLayerWeight(capa, 1f);
        animador.CrossFadeInFixedTime(Animator.StringToHash(stateName), 0.12f, capa, 0f);
        _poseDelJugador = PlayerAnimator.StartCoroutine(SostenerPoseDelJugador(stateName, congelarAlFinal));
    }

    private System.Collections.IEnumerator SostenerPoseDelJugador(string nombre, bool congelar)
    {
        var animador = AnimadorDeActuacion;
        int capa = AnimatorLayerUtil.ResolveLayer(animador, nombre, animador.GetLayerIndex("UpperBody"));
        int hash = Animator.StringToHash(nombre);
        while (animador != null && PlayerAnimator != null && PlayerAnimator.isActiveAndEnabled)
        {
            yield return null;
            if (animador == null) yield break;
            var estado = animador.GetCurrentAnimatorStateInfo(capa);
            if (estado.IsName(nombre))
            {
                bool sale = animador.IsInTransition(capa) && !animador.GetNextAnimatorStateInfo(capa).IsName(nombre);
                if ((sale && (estado.loop || congelar)) || (congelar && !estado.loop && estado.normalizedTime >= 1f))
                    animador.Play(hash, capa, estado.loop ? Mathf.Repeat(estado.normalizedTime, 1f) : Mathf.Min(1f, estado.normalizedTime));
            }
            else if (!animador.IsInTransition(capa)) animador.CrossFadeInFixedTime(hash, 0.12f, capa);
        }
        _poseDelJugador = null;
    }

    /// Suelta la pose sostenida. `aIdle` a false la deja puesta y no devuelve a nadie a su pose
    /// normal: es lo que hace falta cuando lo siguiente es otro gesto y un idle de un fotograma
    /// entre medias se vería.
    public void ReleasePose(bool aIdle = true)
    {
        NpcAnimator?.ReleasePose(aIdle);
        if (_poseDelJugador != null && PlayerAnimator != null) PlayerAnimator.StopCoroutine(_poseDelJugador);
        bool teniaPose = _poseDelJugador != null;
        _poseDelJugador = null;
        if (teniaPose && aIdle)
        {
            var animador = AnimadorDeActuacion;
            int capa = animador != null ? animador.GetLayerIndex("UpperBody") : -1;
            int idle = Animator.StringToHash("UpperIdle");
            if (capa >= 0 && animador.HasState(capa, idle)) animador.CrossFadeInFixedTime(idle, 0.2f, capa);
            PlayerAnimator?.ReturnToLocomotion();
        }
    }

    /// ¿Está sosteniendo una pose? Quien vaya a lanzarle un gesto por su cuenta debería mirarlo
    /// antes: una pose sostenida es una decisión del montaje y no se pisa sola.
    public bool SosteniendoPose => (NpcAnimator != null && NpcAnimator.SosteniendoPose) || _poseDelJugador != null;

    public void PlayGesture(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;

        if (_poseDelJugador != null) ReleasePose(false);

        if (NpcAnimator != null) { NpcAnimator.PlaySocialGesture(stateName); return; }
        if (PlayerAnimator != null) { PlayerAnimator.PlayGesture(stateName); return; }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.LogWarning($"[SequenceActor:{Id}] No tiene NPCSimpleAnimator ni PlayerDialogueAnimator " +
            $"— no se puede reproducir el gesto '{stateName}'.");
#endif
    }

    /// Cambia la cara (meshes de ojos y boca). NPCEmotion.None = sin cambio.
    public void SetEmotion(NPCEmotion emotion)
    {
        if (emotion == NPCEmotion.None) return;

        if (Emotion != null) Emotion.SetEmotion(emotion);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
            Debug.LogWarning($"[SequenceActor:{Id}] No tiene NPCEmotionController — no se puede " +
                $"cambiar la cara a '{emotion}'. (El prefab de Will puede llevar más de uno, uno por " +
                "variante de malla: si la cara no cambia, comprueba cuál está activo.)");
#endif
    }

    /// Aplica una reacción temporal. None deja la cara sin cambios.
    public void Reaccionar(NPCEmotion emotion, float segundos = -1f)
    {
        if (emotion == NPCEmotion.None) return;

        if (Emotion != null) Emotion.Reaccionar(emotion, segundos);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        else
            Debug.LogWarning($"[SequenceActor:{Id}] No tiene NPCEmotionController — no se puede " +
                $"cambiar la cara a '{emotion}'. (El prefab de Will puede llevar más de uno, uno por " +
                "variante de malla: si la cara no cambia, comprueba cuál está activo.)");
#endif
    }

    /// Mantener/soltar la animación continua de "hablando" (parámetro IsTalking, si el Animator
    /// Controller del actor lo tiene — el de los NPCs sin arma no lo tiene y esto es un no-op).
    public void SetTalking(bool talking) => NpcAnimator?.SetTalking(talking);

    /// Entra en pose de conversación. SIN girarse hacia el jugador: quién mira a quién en una
    /// cinemática lo decide el montaje, no dónde esté el jugador (INC-318).
    public void BeginInteraction() => NpcAnimator?.BeginInteraction(girarAlJugador: false);
    public void EndInteraction() => NpcAnimator?.EndInteraction();

    // ── Geometría: a qué altura está la cara ─────────────────────────────────
    //
    // El solver de planos (ShotComposer) necesita saber dónde tiene los ojos un actor, y eso no
    // se puede dar por supuesto: el pivot de un personaje está en los pies, y no todos miden lo
    // mismo. Se resuelve una sola vez por actor, en este orden:
    //
    //   1. El hueso de la cabeza del Animator, si el avatar es humanoide. Es el dato bueno: da la
    //      altura real de ESE personaje, sea un crío o un gigante.
    //   2. La altura del CharacterController o de la cápsula, si no hay avatar humanoide.
    //   3. 1,62 m, que es donde tiene los ojos un adulto de pie.
    //
    // Se guarda la ALTURA, no el hueso: seguir el hueso cada frame haría que la cámara oscilara
    // con el paso al caminar y con el balanceo de las animaciones de hablar. Lo que se quiere es
    // un punto estable a la altura de la cara, no un punto pegado al cráneo.
    //
    // Y se cachea porque en un plano 'live' esto se consulta cada frame: buscar componentes en
    // bucle es justo lo que prohíbe CLAUDE.md § 2.

    /// Altura de los ojos de un adulto de pie, cuando no se puede medir el personaje.
    public const float FallbackEyeHeight = 1.62f;

    private float _eyeHeight = -1f;

    /// Altura de los ojos sobre los pies, en metros.
    public float EyeHeight
    {
        get
        {
            if (_eyeHeight < 0f) _eyeHeight = MeasureEyeHeight();
            return _eyeHeight;
        }
    }

    private bool _caraMedida;
    private float _alturaDeOjos;
    private Vector3 _frenteLocal;

    /// Altura real de ojos sobre los pies; excluye pelo y accesorios y se mide una sola vez.
    public float AlturaDeOjos { get { MedirCara(); return _alturaDeOjos; } }
    /// Direcci?n horizontal hacia la cara, independiente de la ra?z del prefab.
    public Vector3 FrenteDeLaCara
    {
        get
        {
            MedirCara();
            Vector3 frente = Transform != null ? Transform.TransformDirection(_frenteLocal) : Vector3.forward;
            frente.y = 0f;
            return frente.sqrMagnitude > 0.0001f ? frente.normalized : Vector3.forward;
        }
    }
    private void MedirCara()
    {
        if (_caraMedida) return;
        _caraMedida = true;
        _alturaDeOjos = EyeHeight;
        _frenteLocal = Vector3.forward;
        if (Transform == null || IsDynamic) return;
        var animator = Transform.GetComponentInChildren<Animator>(true);
        var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        var left = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftEye) : null;
        var right = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightEye) : null;
        Vector3 centro = Vector3.zero;
        bool medido = false;
        if (left != null && right != null) { centro = (left.position + right.position) * 0.5f; medido = true; }
        if (!medido)
        {
            // Los bounds de piel pueden abarcar el cuerpo: BakeMesh mide la geometr?a de ojos.
            foreach (var renderer in Transform.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.name.StartsWith("Eye", System.StringComparison.OrdinalIgnoreCase)) continue;
                Mesh mesh = null;
                bool temporal = false;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    mesh = new Mesh(); skin.BakeMesh(mesh); mesh.RecalculateBounds(); temporal = true;
                }
                else if (renderer.TryGetComponent<MeshFilter>(out var filtro)) mesh = filtro.sharedMesh;
                if (mesh != null) { centro = renderer.transform.TransformPoint(mesh.bounds.center); medido = true; }
                if (temporal)
                {
                    if (Application.isPlaying) Object.Destroy(mesh);
                    else Object.DestroyImmediate(mesh);
                }
                if (medido) break;
            }
        }
        if (!medido && head != null)
        {
            float baseCabeza = head.position.y - Transform.position.y;
            centro = head.position + Vector3.up * Mathf.Max(0f, MeasureHeadTop() - baseCabeza) * 0.45f;
            medido = true;
        }
        if (medido) _alturaDeOjos = Mathf.Max(0f, centro.y - Transform.position.y);
        Vector3 frente = head != null && medido ? centro - head.position : Vector3.zero;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.0001f) frente = head != null ? head.forward : Transform.forward;
        _frenteLocal = Transform.InverseTransformDirection(frente.normalized);
    }

    /// Punto del mundo a la altura de la cara. Es a donde apuntan los planos.
    public Vector3 EyePosition
        => Transform != null ? Transform.position + Vector3.up * EyeHeight : Vector3.zero;

    /// Punto del mundo a la altura del pecho. Lo usan los planos generales, que encuadran el
    /// cuerpo entero y no la cara.
    public Vector3 ChestPosition
        => Transform != null ? Transform.position + Vector3.up * (EyeHeight * 0.72f) : Vector3.zero;

    /// Altura, sobre los pies, de lo más alto del personaje — pelo incluido. Es donde se apoya el
    /// bocadillo de diálogo.
    ///
    /// Hace falta medirlo porque SpeechBubbleUI llevaba un offset FIJO de 2,2 m para todo el mundo,
    /// que es la altura de la cabeza de una persona de verdad. Estos personajes miden metro y pico:
    /// el bocadillo salía flotando casi un cuerpo entero por encima del pelo, suelto en vez de
    /// apoyado en la cabeza, y en los planos generales se iba además muy hacia un lado, porque un
    /// punto tan alto se separa mucho del personaje en perspectiva. Se ve en toda la grabación del
    /// prólogo del 19 sep 2026. Es la misma familia de error que DefaultFrameHeight y que el plano
    /// sobre el hombro: números de proporciones humanas en un arte que no las tiene.
    ///
    /// Se mide de los Renderer una sola vez y se cachea, igual que SafeRadius.
    public float HeadTopHeight
    {
        get
        {
            if (_headTop < 0f) _headTop = MeasureHeadTop();
            return _headTop;
        }
    }

    private float _headTop = -1f;

    private float MeasureHeadTop()
    {
        if (Transform == null) return EyeHeight + 0.35f;

        var renderers = Transform.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return EyeHeight + 0.35f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float alto = bounds.max.y - Transform.position.y;

        // Sanidad: un brazo levantado en el frame en que se mide, o un efecto pegado al personaje,
        // pueden dar un valor absurdo. Ahí vale más la estimación a partir de los ojos.
        return (alto > 0.3f && alto < 6f) ? alto : EyeHeight + 0.35f;
    }

    /// Radio aproximado del actor, en metros. Es lo que impide que un plano meta la camara DENTRO
    /// del objeto que quiere encuadrar.
    ///
    /// Hace falta porque un actor no siempre es una persona: el proyectil del Despertar de la
    /// Estrella es una esfera de varios metros, y el solver, que calculaba la distancia solo a
    /// partir de cuantos metros de sujeto deben entrar en cuadro, lo dejaba a dos metros y medio
    /// del centro — es decir, dentro. En el video del 16 sep se ve la pantalla entera rosa.
    ///
    /// Se mide de los Renderer del objeto, una sola vez, y se cachea.
    public float SafeRadius
    {
        get
        {
            if (_safeRadius < 0f) _safeRadius = MeasureRadius();
            return _safeRadius;
        }
    }

    private float _safeRadius = -1f;

    private float MeasureRadius()
    {
        if (Transform == null) return 0.4f;

        // Para un personaje no hace falta medir: su silueta es estrecha y lo que manda es la
        // composicion, no el volumen. Medir sus Renderer daria el bounding box del avatar entero
        // (brazos abiertos incluidos) y alejaria los primeros planos sin motivo.
        if (!IsDynamic) return 0.4f;

        var renderers = Transform.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return 0.4f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return Mathf.Clamp(bounds.extents.magnitude, 0.2f, 12f);
    }

    private float MeasureEyeHeight()
    {
        if (Transform == null) return FallbackEyeHeight;

        var animator = Transform.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                float h = head.position.y - Transform.position.y;
                // Sanidad: un valor absurdo (personaje tumbado en el frame en que se mide, rig
                // con escala rara) es peor que el valor por defecto.
                if (h > 0.5f && h < 4f) return h;
            }
        }

        if (Transform.TryGetComponent(out CharacterController cc) && cc.height > 0.5f)
            return cc.height * 0.92f;

        if (Transform.TryGetComponent(out CapsuleCollider capsule) && capsule.height > 0.5f)
            return capsule.height * 0.92f;

        return FallbackEyeHeight;
    }

    // ── Rotación ─────────────────────────────────────────────────────────────

    /// Gira al actor hacia un punto. DE VERDAD y AHORA.
    ///
    /// ── Por qué no basta con FaceTarget (INC-299) ─────────────────────────────────────────────
    /// Antes esto llamaba a `NpcAnimator.FaceTarget(...)`, que NO gira a nadie: solo escribe
    /// `_targetRotation`, y quien gira es `ApplySmoothRotation()` en el Update del animador. Eso
    /// tiene dos agujeros, y los dos se ven en la quinta grabación:
    ///
    ///   1. `ApplySmoothRotation` empieza con `if (_disableAutoRotation || AllowManualRotation)
    ///      return;`. Durante y justo después de una caminata cinemática esas banderas están
    ///      puestas, así que el giro NO OCURRÍA NUNCA. En silencio: ni error ni aviso.
    ///   2. Cuando sí ocurría, tardaba (360°/s) y competía con cualquiera que escribiera la
    ///      rotación directamente — `FaceBeat.Co_TurnSmoothly` lo hace. Dos sistemas escribiendo
    ///      la misma rotación cada frame es exactamente lo que Raúl describió como «da la
    ///      sensación de que hay sistemas en conflicto».
    ///
    /// Así que ahora se escribe la rotación y ADEMÁS se sincroniza el objetivo del animador. Lo
    /// segundo no es opcional: el propio comentario de `SyncTargetRotation` (INC-028) avisa de que
    /// si algo reorienta al NPC por fuera sin llamarla, `ApplySmoothRotation` lo arrastra de vuelta
    /// en los siguientes frames hacia la rotación vieja. Es el mismo fallo, en otro sitio.
    public void Face(Vector3 worldPosition)
    {
        if (Transform == null) return;
        Vector3 dir = worldPosition - Transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        NpcAnimator?.SyncTargetRotation();
    }

    /// Fija la rotación actual como la que el animador debe mantener.
    ///
    /// La usa quien escribe la rotación a mano frame a frame (un giro suave): sin esto,
    /// `ApplySmoothRotation` tira en sentido contrario mientras dura el giro y lo deshace al
    /// acabar. Es un no-op en el jugador y en los actores sin animador de NPC.
    public void SyncRotation() => NpcAnimator?.SyncTargetRotation();

    public void FaceAwayFrom(Vector3 worldPosition)
    {
        if (Transform == null) return;
        Face(Transform.position + (Transform.position - worldPosition));
    }

    // ── Movimiento ───────────────────────────────────────────────────────────

    /// Para en seco y deja al actor en Idle, sin velocidad ni path residual.
    ///
    /// IMPORTANTE (INC-210): se usa ResetMovement(), NO SetMovementSpeed(0f). El segundo escribe
    /// InputMagnitude CON DAMPING, así que una llamada suelta no lo lleva a 0 — y como
    /// Idle_Normal_NoWeapon cae sin condiciones al blend tree "Free Locomotion" al terminar su
    /// ciclo, el actor acaba "andando en el sitio". Este es el bug que costó ocho pasadas de
    /// INC-206; que viva aquí una sola vez es media razón de ser de este sistema.
    public void StopMovement()
    {
        if (Agent != null) NavMeshAgentUtility.HardStop(Agent);
        NpcAnimator?.ResetMovement();
        NpcAnimator?.TransitionToIdle();
    }

    /// Devuelve al actor a su pose normal, cerrando cualquier animación de cuerpo entero que se
    /// haya quedado puesta. Ver PlayerDialogueAnimator.ReturnToLocomotion para el caso que motivó
    /// esto (Dizzy_NoWeapon en la capa base de Will, sin transición de salida).
    public void ReturnToNormalPose()
    {
        ReleasePose(false);
        if (NpcAnimator != null)
        {
            NpcAnimator.EndInteraction();
            NpcAnimator.TransitionToIdle();
        }

        PlayerAnimator?.ReturnToLocomotion();
    }

    // ── Ajustes temporales del NavMeshAgent ──────────────────────────────────
    //
    // Un beat de movimiento cambia temporalmente la velocidad y el obstacle avoidance del agente.
    // Su propio try/finally los restaura al terminar, pero eso NO basta: si la cinemática se salta
    // a mitad, StopCoroutine no garantiza que se ejecute el finally de una corrutina anidada (ver
    // el comentario de RequestSkip en CinematicSequencerBase). Guardarlos en el actor permite que
    // la limpieza del SequencePlayer los restaure también en ese camino, sin depender del finally.

    private float _savedAgentSpeed = -1f;
    private float _savedStoppingDistance;
    private UnityEngine.AI.ObstacleAvoidanceType _savedAvoidance;
    private bool _agentOverridden;

    /// Guarda los ajustes actuales del agente y aplica los de la secuencia. speedOverride <= 0
    /// deja la velocidad del agente intacta (lo normal).
    public void BeginAgentOverride(float speedOverride)
    {
        if (Agent == null || _agentOverridden) return;

        _savedAgentSpeed = Agent.speed;
        _savedStoppingDistance = Agent.stoppingDistance;
        Agent.stoppingDistance = 0.02f;
        _savedAvoidance = Agent.obstacleAvoidanceType;
        _agentOverridden = true;

        if (speedOverride > 0f) Agent.speed = speedOverride;

        // Quien ANDA esquiva; quien está parado no se aparta (INC-400). Antes la caminata iba sin
        // esquivar, y en la grabación del 24 sep un aldeano atraviesa a otro al empezar: «los
        // NPCs no se pueden atravesar entre sí». En el crowd de Unity un agente sin avoidance
        // sigue contando como obstáculo para los que sí esquivan, así que basta con que esquive
        // el que se mueve: rodea a los parados, y los parados (sin avoidance, ver Hold) no se
        // apartan a empujones, que era lo que arreglaba INC-301.
        Agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    /// Devuelve al agente su velocidad y su obstacle avoidance originales. Idempotente: se llama
    /// tanto al terminar el beat como en la limpieza final y en el camino de skip.
    public void EndAgentOverride()
    {
        if (!_agentOverridden) return;
        _agentOverridden = false;

        if (Agent != null)
        {
            Agent.stoppingDistance = _savedStoppingDistance;
            if (_savedAgentSpeed > 0f) Agent.speed = _savedAgentSpeed;

            // El avoidance solo se devuelve si el actor ya no está retenido por la secuencia. Si
            // lo sigue estando, mandan las reglas del candado (ver Hold): nadie se aparta solo.
            if (_hold == null) Agent.obstacleAvoidanceType = _savedAvoidance;
            else Agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        _savedAgentSpeed = -1f;
    }

    // ── Candado de comportamiento ambiental ──────────────────────────────────

    /// Saca al NPC de su comportamiento ambiental (Wander/Idle) y lo deja bajo control de la
    /// secuencia. Idempotente. No aplica al jugador (su input ya lo bloquea ActionMode.Cinematic).
    public void Hold()
    {
        // Fuera de Play no hay corrutinas ni FSM corriendo: retener a un NPC no significa nada y
        // StartCinematicSequence lanzaría una corrutina sobre un objeto que no está en juego. Las
        // herramientas de Editor que resuelven planos (ver SequenceShotCapture) resuelven actores
        // igual que el juego, y pasan por aquí.
        if (!Application.isPlaying) return;

        if (IsPlayer || Manager == null || _hold != null) return;
        _hold = new HoldSequence();
        Manager.StartCinematicSequence(_hold);

        // Y sin empujones (INC-301). El obstacle avoidance del NavMeshAgent solo se apagaba
        // DURANTE una caminata (BeginAgentOverride) y se devolvía al acabarla. El resto del tiempo
        // —que en una cinemática es casi todo— una docena de agentes parados a un metro unos de
        // otros se separan a empujones: es «el Archimago empuja a la del pelo rosa y los NPCs se
        // empujan entre sí» de la sexta grabación. Mientras la secuencia manda, nadie se aparta
        // solo; quien decide dónde está cada uno es el montaje.
        if (Agent != null)
        {
            _avoidanceAntesDelCandado = Agent.obstacleAvoidanceType;
            _avoidanceGuardada = true;
            Agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
        }
    }

    private UnityEngine.AI.ObstacleAvoidanceType _avoidanceAntesDelCandado;
    private bool _avoidanceGuardada;

    /// Devuelve al NPC el control de su comportamiento ambiental. Idempotente.
    public void Release()
    {
        _hold?.Finish();
        _hold = null;

        if (_avoidanceGuardada && Agent != null)
            Agent.obstacleAvoidanceType = _avoidanceAntesDelCandado;
        _avoidanceGuardada = false;
    }
}
