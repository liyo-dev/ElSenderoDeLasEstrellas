using System.Collections;
using UnityEngine;

/// <summary>
/// Controla las animaciones corporales del jugador durante los diálogos.
/// Escucha DialogueManager.OnDialogueLineChanged y reproduce gestos cuando
/// la línea activa pertenece al jugador (isPlayerSpeaking = true).
/// Añadir este componente al GameObject raíz del player junto al Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerDialogueAnimator : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Animator del personaje jugador")]
    [SerializeField] private Animator animator;

    [Tooltip("Perfil de emociones compartido con los NPCs — define cara y animación corporal")]
    [SerializeField] private EmotionProfile emotionProfile;

    [Tooltip("Tiempo de blend al entrar en un gesto")]
    [SerializeField, Range(0f, 0.3f)] private float blendTime = 0.1f;

    // Índice de la layer "UpperBody" (Avatar Mask sin piernas) del Animator del jugador — mismo
    // índice que NPCSimpleAnimator.upperBodyLayer. La mayoría de los gestos de diálogo (Talk01-03,
    // Angry01-02, etc.) viven aquí desde la migración fuera del Base Layer (30 ago 2026).
    private const int CapaUpperBody = 1;

    // Estado
    private Coroutine _gestureCoroutine;
    private int _lastTalkIndex = -1;
    // ✅ FIX: si el jugador está sentado/tumbado en un NPCWorldPoint, un gesto de diálogo
    // sobreescribe el layer 0 con su propio clip y no vuelve solo al loop de la actividad al
    // terminar. Referencia cacheada para poder restaurarlo (ver PlayGestureCoroutine).
    private PlayerAmbientActivityHandler _ambientActivity;

    #region Unity Lifecycle

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        _ambientActivity = GetComponent<PlayerAmbientActivityHandler>();

        // Al hablar solo se mueve la boca: la cabeza no cabecea con Talk01-03 (INC-1144).
        CabezaQuietaAlHablar.Asegurar(gameObject);
    }

    void OnEnable()
    {
        DialogueManager.OnDialogueLineChanged += OnDialogueLineChanged;
        DialogueManager.OnDialogueClosed      += OnDialogueClosed;
    }

    void OnDisable()
    {
        DialogueManager.OnDialogueLineChanged -= OnDialogueLineChanged;
        DialogueManager.OnDialogueClosed      -= OnDialogueClosed;
    }

    #endregion

    #region Event Handlers

    private void OnDialogueLineChanged(DialogueLine line, Transform npcInvolved)
    {
        if (!line.isPlayerSpeaking)
            return;

        PlayBodyEmotion(line.emotion);
    }

    private void OnDialogueClosed(Transform npcInvolved)
    {
        if (_gestureCoroutine != null)
        {
            StopCoroutine(_gestureCoroutine);
            _gestureCoroutine = null;
        }
    }

    #endregion

    #region Animation

    /// <summary>
    /// Reproduce un gesto por nombre de estado. Uso desde sistemas externos (ej: ShowSpeechBubbleNode).
    /// </summary>
    public void PlayGesture(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        // AnimatorLayerUtil resuelve en qué layer vive realmente el estado (misma utilidad
        // compartida que usa NPCSimpleAnimator.PlaySocialGesture()).
        int capa = AnimatorLayerUtil.ResolveLayer(animator, stateHash, CapaUpperBody);
        if (capa < 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            // Avisa si el perfil pide un estado corporal que el Animator no contiene.
            Debug.LogWarning($"[PlayerDialogueAnimator] ⚠️ PlayGesture('{stateName}'): ese estado no " +
                $"existe en ningún layer del Animator Controller " +
                $"'{(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}'. " +
                "El jugador se quedará quieto en este gesto.");
#endif
            return;
        }

        if (_gestureCoroutine != null)
            StopCoroutine(_gestureCoroutine);

        _gestureCoroutine = StartCoroutine(PlayGestureCoroutine(stateHash, stateName, capa));
    }

    /// Cancela el gesto pendiente sin insertar una locomoción entre dos poses.
    public void CancelarGesto()
    {
        if (_gestureCoroutine != null) StopCoroutine(_gestureCoroutine);
        _gestureCoroutine = null;
    }

    // ── La cara del jugador (FIX 16 sep 2026) ────────────────────────────────
    // Mismo arreglo que en NPCSimpleAnimator.PlayBodyEmotion: hasta hoy esto solo movía el cuerpo
    // de Will y su cara no cambiaba nunca en ningún diálogo. Ver el comentario largo allí y
    // EmotionControllerResolver (Will lleva DOS NPCEmotionController en el mismo GameObject y solo
    // uno tiene los meshes puestos, así que un GetComponent a secas cogía el vacío).
    private NPCEmotionController _emotionControllerCached;
    private bool _emotionControllerResolved;

    private NPCEmotionController ResolvedEmotionController
    {
        get
        {
            if (!_emotionControllerResolved)
            {
                _emotionControllerResolved = true;
                _emotionControllerCached = Game.NPC.Common.EmotionControllerResolver.Resolve(gameObject);
            }
            return _emotionControllerCached;
        }
    }

    /// Devuelve la CAPA BASE del jugador a su locomoción normal.
    ///
    /// FIX 16 sep 2026 (Raúl: "cuando termina la secuencia Will se queda con la animación de
    /// mareo"). Causa raíz, confirmada leyendo Invector@BasicLocomotion.controller: casi todos los
    /// gestos de diálogo de Will viven en la capa UpperBody y tienen transición de salida propia
    /// (Laugh01, Cheer01/02, HeadNod01, Question01, Beg01, Talk01...), pero unos pocos viven en la
    /// CAPA BASE — y "Dizzy_NoWeapon" además NO TIENE NINGUNA TRANSICIÓN DE SALIDA. Una vez que
    /// entra ahí, la capa base se queda en mareo para siempre: los gestos siguientes se reproducen
    /// encima, en UpperBody, y no la limpian nunca. De ahí que Will acabe la escena mareado aunque
    /// después se haya reído y haya asentido.
    ///
    /// Esto lo arregla de forma general: cualquier gesto de cuerpo entero sin salida se puede
    /// cerrar llamando aquí. El SequencePlayer lo llama al terminar toda secuencia.
    public void ReturnToLocomotion(float blend = 0.25f)
    {
        if (animator == null) return;

        if (_gestureCoroutine != null)
        {
            StopCoroutine(_gestureCoroutine);
            _gestureCoroutine = null;
        }

        int hash = Animator.StringToHash(LocomotionState);
        if (animator.HasState(0, hash))
            animator.CrossFadeInFixedTime(hash, blend, 0, 0f);
    }

    /// Devuelve la capa base a la locomoción sin cortar el gesto que suene en UpperBody: cierra un
    /// gesto de cuerpo entero sin salida (Dizzy_NoWeapon) en mitad de una escena. Sentado no hace nada.
    public void CerrarGestoDeCapaBase(float blend = 0.25f)
    {
        if (animator == null) return;
        if (_ambientActivity != null && _ambientActivity.IsSeated) return;
        int hash = Animator.StringToHash(LocomotionState);
        if (animator.HasState(0, hash) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash)
            animator.CrossFadeInFixedTime(hash, blend, 0, 0f);
    }

    /// Deja el cuerpo listo para andar cuando el jugador recupera el control (fin de un diálogo o
    /// de una cinemática). Un gesto o una emoción de la capa base sigue sonando hasta acabar su clip
    /// aunque ya se pueda mover: durante esos segundos el cuerpo se desliza sin animar las piernas.
    /// Si la capa base no está en la locomoción (ni en el aire, ni sentado), vuelve a ella; la capa
    /// UpperBody vuelve a su reposo. Ver INC-901.
    public void AlRecuperarElControl()
    {
        if (animator == null) return;
        if (_ambientActivity != null && _ambientActivity.IsSeated) return;

        if (_gestureCoroutine != null)
        {
            StopCoroutine(_gestureCoroutine);
            _gestureCoroutine = null;
        }

        var actual = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        int hash = actual.shortNameHash;
        if (hash != LocomocionHash && !EsEstadoDelAire(hash))
            ReturnToLocomotion(0.15f);

        if (animator.layerCount > CapaUpperBody && animator.HasState(CapaUpperBody, UpperIdleHash))
        {
            var arriba = animator.GetCurrentAnimatorStateInfo(CapaUpperBody);
            if (arriba.shortNameHash != UpperIdleHash) animator.CrossFadeInFixedTime(UpperIdleHash, 0.15f, CapaUpperBody);
        }
    }

    private static readonly int LocomocionHash = Animator.StringToHash(LocomotionState);
    private static readonly int UpperIdleHash = Animator.StringToHash("UpperIdle");
    private static readonly int[] EstadosDelAire =
    {
        Animator.StringToHash("Falling"), Animator.StringToHash("Jump"),
        Animator.StringToHash("JumpMove"), Animator.StringToHash("Airborne"),
    };

    private static bool EsEstadoDelAire(int hash)
    {
        for (int i = 0; i < EstadosDelAire.Length; i++)
            if (EstadosDelAire[i] == hash) return true;
        return false;
    }

    /// Estado de locomoción de la capa base del jugador — el mismo nombre que usan los NPCs
    /// (NPCSimpleAnimator.locomotionState) porque los dos Animator Controllers comparten el árbol.
    private const string LocomotionState = "Free Locomotion";

    /// Cambia la cara del jugador. None = sin cambio.
    public void SetFaceEmotion(NPCEmotion emotion)
    {
        if (emotion == NPCEmotion.None) return;
        var controlador = ResolvedEmotionController;
        if (controlador != null) controlador.Reaccionar(emotion);
    }

    private void PlayBodyEmotion(NPCEmotion emotion)
    {
        if (animator == null)
            return;

        // La cara, siempre — aunque la emoción no tenga animación corporal asignada.
        SetFaceEmotion(emotion);

        string stateName = ResolveStateName(emotion);
        if (string.IsNullOrEmpty(stateName))
            return; // Emoción sin animación corporal asignada: el jugador mantiene el gesto actual

        int stateHash = Animator.StringToHash(stateName);
        int capa = AnimatorLayerUtil.ResolveLayer(animator, stateHash, CapaUpperBody);
        if (capa < 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            // Avisa si el perfil pide un estado corporal que el Animator no contiene.
            Debug.LogWarning($"[PlayerDialogueAnimator] ⚠️ PlayBodyEmotion('{stateName}'): ese estado no " +
                $"existe en ningún layer del Animator Controller " +
                $"'{(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}'. " +
                "La cara cambia pero el cuerpo se queda quieto.");
#endif
            return;
        }

        if (_gestureCoroutine != null)
            StopCoroutine(_gestureCoroutine);

        _gestureCoroutine = StartCoroutine(PlayGestureCoroutine(stateHash, stateName, capa));
    }

    /// <summary>
    /// Igual que NPCSimpleAnimator.ResolveBodyAnimStateName: para emociones no neutrales devuelve
    /// el bodyAnimStateName tal cual esté en el EmotionProfile (vacío = sin cambio de animación,
    /// para emociones que solo deben cambiar la cara del jugador).
    /// </summary>
    private string ResolveStateName(NPCEmotion emotion)
    {
        string[] neutralAnims = (emotionProfile != null && emotionProfile.neutralBodyAnims is { Length: > 0 })
            ? emotionProfile.neutralBodyAnims
            : new[] { "Talk01", "Talk02", "Talk03" };

        if (emotion == NPCEmotion.None || emotion == NPCEmotion.Neutral)
        {
            _lastTalkIndex = (_lastTalkIndex + 1) % neutralAnims.Length;
            return neutralAnims[_lastTalkIndex];
        }

        if (emotionProfile != null)
        {
            var data = emotionProfile.GetEmotionData(emotion);
            return data.bodyAnimStateName; // puede venir vacío a propósito: "sin cambio"
        }

        return neutralAnims[0];
    }

    private IEnumerator PlayGestureCoroutine(int stateHash, string stateName, int capa)
    {
        if (capa == CapaUpperBody)
            animator.SetLayerWeight(CapaUpperBody, 1f); // si no, el gesto se reproduce pero no se ve

        animator.CrossFadeInFixedTime(stateHash, blendTime, capa, 0f);

        yield return null; // esperar un frame para que comience la transición

        // Esperar a que termine el clip
        float elapsed = 0f;
        float maxWait = 5f; // timeout de seguridad

        while (elapsed < maxWait)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(capa);
            if (stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 0.95f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _gestureCoroutine = null;

        // ✅ FIX: si el jugador sigue sentado/tumbado (actividad ambiental activa), el gesto
        // acaba de sobreescribir el layer 0 con su propio clip — volver al loop de la actividad
        // para no dejarlo de pie/flotando tras terminar el gesto.
        if (_ambientActivity != null && _ambientActivity.IsSeated)
            _ambientActivity.ResumeActivityLoop();
    }

    #endregion
}
