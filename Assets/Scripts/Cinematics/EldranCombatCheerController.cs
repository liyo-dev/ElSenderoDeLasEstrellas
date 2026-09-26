using System.Collections;
using UnityEngine;

/// Eldran anima a Will durante el combate del despertar:
/// mira hacia Will continuamente y lanza bocadillos con animaciones a intervalos.
/// Añadir al GameObject de Eldran (o a cualquier manager de la escena).
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // ejecutar LateUpdate después de NPCSimpleAnimator para ganar la rotación
public class EldranCombatCheerController : MonoBehaviour
{
    [Header("Personajes")]
    [SerializeField] private Transform eldranTransform;
    [SerializeField] private Transform willTransform;

    [Header("Señales narrativas")]
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Escucha)]
    [SerializeField] private string startSignal = "AWAKEN_DONE";
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Escucha)]
    [SerializeField] private string stopSignal  = "AWAKEN_COMBAT_END";

    [Tooltip("La levanta BossArenaController al TERMINAR la presentación del jefe. Es la que " +
             "dispara la intervención de abajo: antes de esto la pantalla es del jefe y meter " +
             "un bocadillo ahí sería pisarle la entrada.")]
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Escucha)]
    [SerializeField] private string bossIntroDoneSignal = "BOSS_INTRO_DONE";

    [Header("Intervención tras la presentación del jefe")]
    [Tooltip("Las dice UNA vez, en orden, justo después de la presentación del jefe. Aquí es donde " +
             "se enseña la mecánica nueva: primero qué ha pasado, después qué botón hay que pulsar.")]
    [SerializeField] private string[] introKeys  = { "EVT_10", "EVT_08" };

    [Tooltip("Un gesto por línea. Del catálogo: FoundSomething señala hacia delante ('mira lo que " +
             "ha pasado'), Challenging es plantarse ('ve a por él').")]
    [SerializeField] private string[] introAnims = { "FoundSomething_NoWeapon", "Challenging_NoWeapon" };

    [Tooltip("Segundos que se deja respirar entre el final de la presentación y la primera línea.")]
    [SerializeField] private float introDelay = 0.6f;

    [Tooltip("Red de seguridad. Si la presentación del jefe no llega a levantar su señal (combate " +
             "sin jefe, arena que se salta la intro, un fallo), pasados estos segundos desde " +
             "AWAKEN_DONE la intervención se suelta igual. Sin esto, la pista de 'pulsa X' podría " +
             "no llegar NUNCA y el jugador se quedaría sin saber qué hacer en su primer combate.")]
    [SerializeField] private float introFallbackSeconds = 12f;

    [Header("Bocadillos de ánimo")]
    [SerializeField] private string[] cheerKeys  = { "EVT_ELDRAN_CHEER_01", "EVT_ELDRAN_CHEER_02", "EVT_ELDRAN_CHEER_03" };
    [SerializeField] private string[] cheerAnims = { "Cheer01", "Cheer01", "Cheer01" };
    [SerializeField] private float    bubbleDuration = 2.8f;

    [Header("Pista del aro de runas (Paso 6 del refactor Tramo 1)")]
    [Tooltip("La pista del aro la da Eldran, no un tooltip (análisis §6, punto 3): 'no luches " +
             "contra su fuerza, mira el aro'. Se suelta UNA vez, después de la intervención " +
             "inicial (introKeys) para no pisarla, en cuanto ocurra lo que pase antes de estos " +
             "dos: Will recibe el primer golpe del jefe, o pasan 'collarHintFallbackSeconds' desde " +
             "que empiezan los ánimos sueltos. Vacío = sin pista (por si algún combate reutiliza " +
             "este controlador sin RuneCollar).")]
    [SerializeField] private string collarHintKey = "EVT_ELDRAN_HINT_ARO";
    [SerializeField] private string collarHintAnim = "Cheer02";
    [Tooltip("Referencia a la Damageable de Will para detectar 'primer golpe recibido'. Si se " +
             "deja vacío, se busca a partir de willTransform (padre, propio objeto o hijos).")]
    [SerializeField] private Damageable willDamageable;
    [SerializeField] private float collarHintFallbackSeconds = 8f;

    [Header("Pistas de las reglas del combate (INC-469)")]
    [Tooltip("La primera vez que Will le da al jefe cuando NO está expuesto (sin el aro encendido) y " +
             "el jefe se cura. Vacío = sin pista.")]
    [SerializeField] private string golpeSinAroKey = "EVT_ELDRAN_HINT_SIN_ARO";
    [SerializeField] private string golpeSinAroAnim = "HeadShake01";
    [Tooltip("La primera vez que el jefe suelta orbes al recibir un golpe: qué son y para qué sirven.")]
    [SerializeField] private string orbesKey = "EVT_ELDRAN_HINT_ORBES";
    [SerializeField] private string orbesAnim = "FoundSomething_NoWeapon";

    [Header("Timings")]
    [SerializeField] private float firstDelay  = 2.5f;
    [SerializeField] private float minInterval = 6f;
    [SerializeField] private float maxInterval = 14f;

    [Header("Rotación hacia Will")]
    [SerializeField] private float lookAtSpeed = 360f;  // grados/segundo

    private NPCSimpleAnimator _npcAnim;
    private Animator          _animator;
    private Coroutine         _cheerCoroutine;
    private Coroutine         _introCoroutine;
    private Coroutine         _collarHintCoroutine;
    private bool              _lookingAtWill;
    private bool              _introDicha;
    private bool              _collarHintDicha;
    private bool              _golpeSinAroDicho;
    private bool              _orbesDichos;
    private readonly System.Collections.Generic.List<(string key, string anim)> _pistasPendientes = new();
    private Coroutine         _pistasCoroutine;

    void Awake()
    {
        if (eldranTransform != null)
        {
            _npcAnim  = eldranTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _animator = eldranTransform.GetComponentInChildren<Animator>();
        }

        if (!willDamageable && willTransform != null)
        {
            willDamageable = willTransform.GetComponent<Damageable>()
                           ?? willTransform.GetComponentInParent<Damageable>()
                           ?? willTransform.GetComponentInChildren<Damageable>(true);
        }
    }

    void OnEnable()
    {
        var signals = DefaultNarrativeSignals.EnsureInstance();
        signals.OnCustom(startSignal, StartCheering);
        signals.OnCustom(stopSignal,  StopCheering);
        if (!string.IsNullOrEmpty(bossIntroDoneSignal))
            signals.OnCustom(bossIntroDoneSignal, LanzarIntervencion);
        SoloDanoCuandoExpuesto.AlCurarsePorGolpe += AlGolpeSinAro;
        OrbDropper.AlSoltarOrbes += AlSoltarOrbes;
        // En cuanto se gana, se calla: la pantalla es de la celebración de Will (INC-470).
        BossArenaController.OnAnyBattleEnded += StopCheering;
    }

    void OnDisable()
    {
        StopCheering();
        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null)
        {
            signals.OffCustom(startSignal, StartCheering);
            signals.OffCustom(stopSignal,  StopCheering);
            if (!string.IsNullOrEmpty(bossIntroDoneSignal))
                signals.OffCustom(bossIntroDoneSignal, LanzarIntervencion);
        }
        if (willDamageable != null) willDamageable.OnDamaged -= HandleWillDamagedForCollarHint;
        SoloDanoCuandoExpuesto.AlCurarsePorGolpe -= AlGolpeSinAro;
        OrbDropper.AlSoltarOrbes -= AlSoltarOrbes;
        BossArenaController.OnAnyBattleEnded -= StopCheering;
    }

    void OnDestroy() => StopCheering();

    // ── Pistas de las reglas (una vez cada una, solo durante este combate) ─────

    private void AlGolpeSinAro(SoloDanoCuandoExpuesto _, float __)
    {
        if (_golpeSinAroDicho || !_lookingAtWill || string.IsNullOrEmpty(golpeSinAroKey)) return;
        _golpeSinAroDicho = true;
        // Es la regla que explica el aro: si aún no se había dicho la pista del aro, ya no hace falta.
        _collarHintDicha = true;
        EncolarPista(golpeSinAroKey, golpeSinAroAnim);
    }

    private void AlSoltarOrbes(OrbDropper _, OrbType __)
    {
        if (_orbesDichos || !_lookingAtWill || string.IsNullOrEmpty(orbesKey)) return;
        _orbesDichos = true;
        EncolarPista(orbesKey, orbesAnim);
    }

    /// Las pistas esperan a que acabe la intervención inicial (SpeechBubbleUI solo enseña un
    /// bocadillo: si hablaran a la vez, se pisarían) y se dicen una detrás de otra.
    private void EncolarPista(string key, string anim)
    {
        _pistasPendientes.Add((key, anim));
        if (_pistasCoroutine == null) _pistasCoroutine = StartCoroutine(Co_Pistas());
    }

    private IEnumerator Co_Pistas()
    {
        while (_pistasPendientes.Count > 0)
        {
            while (!_introDicha || _introCoroutine != null) yield return null;
            var (key, anim) = _pistasPendientes[0];
            _pistasPendientes.RemoveAt(0);
            Decir(key, anim);
            yield return new WaitForSeconds(bubbleDuration + 0.25f);
        }
        _pistasCoroutine = null;
    }

    // LateUpdate con DefaultExecutionOrder(100) corre después de NPCSimpleAnimator,
    // sobreescribiendo cualquier rotación que el sistema NPC haya aplicado ese frame.
    void LateUpdate()
    {
        if (!_lookingAtWill || eldranTransform == null || willTransform == null) return;

        Vector3 dir = willTransform.position - eldranTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir.normalized);
            eldranTransform.rotation = Quaternion.RotateTowards(
                eldranTransform.rotation, target, lookAtSpeed * Time.deltaTime);
        }
    }

    // ── API ───────────────────────────────────────────────────────────────────

    private void StartCheering()
    {
        StopCheering();

        // Los ánimos sueltos NO arrancan aquí: primero va la intervención (qué ha pasado y qué
        // botón pulsar), y solo cuando esa termina empiezan. Si los dos sistemas hablaran a la vez
        // se pisarían el bocadillo — SpeechBubbleUI solo muestra uno.
        _introDicha = false;
        _golpeSinAroDicho = false;
        _orbesDichos = false;
        _introCoroutine = StartCoroutine(Co_EsperarIntervencion());

        if (willTransform != null && eldranTransform != null)
        {
            _npcAnim?.DisableAutoRotation();
            _lookingAtWill = true;
        }
    }

    private void StopCheering()
    {
        if (_cheerCoroutine != null) { StopCoroutine(_cheerCoroutine); _cheerCoroutine = null; }
        if (_introCoroutine != null) { StopCoroutine(_introCoroutine); _introCoroutine = null; }
        if (_collarHintCoroutine != null) { StopCoroutine(_collarHintCoroutine); _collarHintCoroutine = null; }
        if (_pistasCoroutine != null) { StopCoroutine(_pistasCoroutine); _pistasCoroutine = null; }
        _pistasPendientes.Clear();
        if (willDamageable != null) willDamageable.OnDamaged -= HandleWillDamagedForCollarHint;
        _lookingAtWill = false;
        _npcAnim?.EnableAutoRotation();
    }

    // ── Corrutinas ────────────────────────────────────────────────────────────

    /// La levanta la señal del jefe. Si llega dos veces (o llega cuando ya se dijo), no repite.
    private void LanzarIntervencion()
    {
        if (_introDicha) return;
        if (_introCoroutine != null) { StopCoroutine(_introCoroutine); _introCoroutine = null; }
        _introCoroutine = StartCoroutine(Co_Intervencion());
    }

    /// Espera a la señal del jefe, con tope. El tope existe porque la pista de "pulsa X" es la
    /// ÚNICA forma que tiene el jugador de saber qué hacer en el primer combate del juego: si la
    /// señal no llega, se suelta igual.
    private IEnumerator Co_EsperarIntervencion()
    {
        float esperado = 0f;
        while (!_introDicha && esperado < introFallbackSeconds)
        {
            yield return null;
            esperado += Time.deltaTime;
        }

        _introCoroutine = null;

        if (!_introDicha)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[EldranCheer] La señal '{bossIntroDoneSignal}' no ha llegado en " +
                $"{introFallbackSeconds:0}s. Se suelta la intervención igualmente para no dejar al " +
                "jugador sin la pista de qué botón pulsar.", this);
#endif
            yield return Co_Intervencion();
        }
    }

    private IEnumerator Co_Intervencion()
    {
        _introDicha = true;

        if (introDelay > 0f) yield return new WaitForSeconds(introDelay);

        if (introKeys != null)
        {
            for (int i = 0; i < introKeys.Length; i++)
            {
                if (string.IsNullOrEmpty(introKeys[i])) continue;

                string anim = introAnims != null && i < introAnims.Length ? introAnims[i] : null;
                Decir(introKeys[i], anim);

                yield return new WaitForSeconds(bubbleDuration + 0.25f);
            }
        }

        _introCoroutine = null;
        _cheerCoroutine = StartCoroutine(Co_Cheer());
        StartCollarHintTracking();
    }

    /// Arranca DESPUÉS de la intervención inicial (nunca antes, para no pisar sus bocadillos con
    /// SpeechBubbleUI, que solo muestra uno a la vez). Dos disparadores, gana el que llegue
    /// primero: el primer golpe que recibe Will del jefe, o el temporizador de seguridad -- mismo
    /// motivo que introFallbackSeconds, la pista del aro no puede depender de que el jugador
    /// encaje un golpe si va sobrado y nunca lo recibe.
    private void StartCollarHintTracking()
    {
        _collarHintDicha = false;

        if (willDamageable != null)
        {
            willDamageable.OnDamaged -= HandleWillDamagedForCollarHint; // guard doble-suscripción
            willDamageable.OnDamaged += HandleWillDamagedForCollarHint;
        }

        if (_collarHintCoroutine != null) StopCoroutine(_collarHintCoroutine);
        _collarHintCoroutine = StartCoroutine(Co_CollarHintFallback());
    }

    private void HandleWillDamagedForCollarHint(float amount) => TriggerCollarHint();

    private IEnumerator Co_CollarHintFallback()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, collarHintFallbackSeconds));
        _collarHintCoroutine = null;
        TriggerCollarHint();
    }

    private void TriggerCollarHint()
    {
        if (_collarHintDicha || string.IsNullOrEmpty(collarHintKey)) return;
        _collarHintDicha = true;

        if (_collarHintCoroutine != null) { StopCoroutine(_collarHintCoroutine); _collarHintCoroutine = null; }
        if (willDamageable != null) willDamageable.OnDamaged -= HandleWillDamagedForCollarHint;

        Decir(collarHintKey, collarHintAnim);
    }

    private IEnumerator Co_Cheer()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            ShowCheer();
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    private void ShowCheer()
    {
        if (cheerKeys == null || cheerKeys.Length == 0) return;

        int idx = Random.Range(0, cheerKeys.Length);
        Decir(cheerKeys[idx], idx < cheerAnims.Length ? cheerAnims[idx] : null);
    }

    /// Un bocadillo de Eldran con su gesto. Compartido por la intervención y por los ánimos, para
    /// que no haya dos formas distintas de hacer lo mismo.
    private void Decir(string key, string anim)
    {
        if (!string.IsNullOrEmpty(anim))
        {
            if (_npcAnim != null)
                _npcAnim.PlaySocialGesture(anim);
            else if (_animator != null)
                _animator.CrossFadeInFixedTime(Animator.StringToHash(anim), 0.1f, 0);
        }

        if (eldranTransform == null || SpeechBubbleUI.Instance == null) return;

        string text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(key, key)
            : key;
        SpeechBubbleUI.Instance.Show(eldranTransform, text, duration: bubbleDuration, speakerName: "Eldran");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular cheer")]
    void SimulateCheer() => ShowCheer();

    [ContextMenu("Iniciar cheers")]
    void EditorStart() => StartCheering();

    [ContextMenu("Detener cheers")]
    void EditorStop() => StopCheering();
#endif
}
