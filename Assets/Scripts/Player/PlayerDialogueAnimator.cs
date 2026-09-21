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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug")]
    // FIX (30/08/2026, Raul: "en el pensamiento de will solo hace animaciones el mago, will
    // ni una"): activado temporalmente para diagnosticar por que PlayGesture() no produce
    // ningun cambio visible en la vision -- si el problema es que "Question02"/"Talk01" no se
    // encuentran en el Animator Controller de Will, este flag hara que salga un aviso claro en
    // consola (antes silencioso salvo que debugMode ya estuviera a true a mano). Volver a false
    // cuando se confirme la causa real.
    [SerializeField] private bool debugMode = true;
#endif

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
        // compartida que usan NPCSimpleAnimator.PlaySocialGesture() y PromoVideo01Sequencer).
        int capa = AnimatorLayerUtil.ResolveLayer(animator, stateHash, CapaUpperBody);
        if (capa < 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Diagnóstico SIEMPRE visible, no detrás de debugMode (20 sep 2026, INC-273).
            // Un estado que no existe es un no-op silencioso: quien lo pide da el gesto por
            // reproducido y el personaje no hace nada. Así se perdió INC-214, donde una secuencia
            // le pedía a Will `Attack2` — que es un estado del controller de los NPCs, no del suyo —
            // y el gag empezaba sin que nadie lanzara ningún hechizo. El gemelo de esto en los NPCs
            // (NPCSimpleAnimator.PlaySocialGesture) ya avisaba sin condiciones; aquí no, y por eso el
            // mismo fallo se veía en un sitio y en el otro no.
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

    /// Estado de locomoción de la capa base del jugador — el mismo nombre que usan los NPCs
    /// (NPCSimpleAnimator.locomotionState) porque los dos Animator Controllers comparten el árbol.
    private const string LocomotionState = "Free Locomotion";

    /// Cambia la cara del jugador. None = sin cambio.
    public void SetFaceEmotion(NPCEmotion emotion)
    {
        if (emotion == NPCEmotion.None) return;
        ResolvedEmotionController?.SetEmotion(emotion);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Diagnóstico SIEMPRE visible, no detrás de debugMode (20 sep 2026, INC-273).
            // Un estado que no existe es un no-op silencioso: quien lo pide da el gesto por
            // reproducido y el personaje no hace nada. Así se perdió INC-214, donde una secuencia
            // le pedía a Will `Attack2` — que es un estado del controller de los NPCs, no del suyo —
            // y el gag empezaba sin que nadie lanzara ningún hechizo. El gemelo de esto en los NPCs
            // (NPCSimpleAnimator.PlaySocialGesture) ya avisaba sin condiciones; aquí no, y por eso el
            // mismo fallo se veía en un sitio y en el otro no.
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
