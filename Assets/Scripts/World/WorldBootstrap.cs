using UnityEngine;
using UnityEngine.SceneManagement;
using Sendero.Core.Feedback;

/// <summary>
/// Inicializa el mundo cuando MainWorld se carga.
/// Se ejecuta DESPUÉS de SpawnManager para usar el anchor ya establecido.
/// </summary>
[DefaultExecutionOrder(200)]
public class WorldBootstrap : MonoBehaviour
{
    [Header("Fade al colocar al jugador (arranque)")]
    [Tooltip("Duración del fundido de ENTRADA (de negro a visible) una vez el jugador ya está colocado " +
             "(y, si el anchor inicial vive en su propia escena de interior — ver AnchorHomeScene —, esa " +
             "escena ya está cargada). La pantalla se cubre de negro de forma INSTANTÁNEA en OnEnable, " +
             "antes de que se procese nada más, así que nunca se llega a ver al jugador cayendo al vacío, " +
             "el 'pop' de una escena aditiva recién cargada, ni el corte de cámara entre pasos — mismo " +
             "recurso (FeedbackService.ScreenFade) que ya usan las cinemáticas, para que el arranque se " +
             "sienta consistente con el resto del juego.")]
    [SerializeField] private float bootFadeInDuration = 0.35f;

    // Anchors que viven en su propia escena de interior (extraída de MainWorld) y que por tanto
    // necesitan esa escena cargada en ADITIVO antes de poder teletransportar al jugador a ellos.
    // Se añade una entrada aquí cada vez que se separa un interior a su propia escena — mismo
    // patrón que InteriorPortalTrigger usa para las puertas normales durante el gameplay, pero
    // aplicado también al arranque inicial (Play / Nueva Partida / Continuar), que no pasa por
    // ningún trigger de puerta.
    private static readonly System.Collections.Generic.Dictionary<string, string> AnchorHomeScene =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "Bedroom", "WillHouse" },
    };

    // Anchors cuyo arranque normal desemboca en una cinemática narrativa que se encarga ELLA
    // MISMA de revelar la pantalla (ej. "Bedroom" → PrologueDreamSequencer, disparada por el
    // grafo con la señal PROLOGUE_DREAM_START). Si el anchor está aquí, el fade final de este
    // script se OMITE — la pantalla se queda en negro (ya cubierta desde OnEnable) hasta que esa
    // cinemática la revele con su propia transición, en vez de destaparla nosotros un instante
    // y dejar ver la escena real (cámara/HUD/debug) antes de que la cinemática vuelva a cubrirla.
    // Ver WaitForBootCinematicOrTimeout(): si la cinemática no llega a arrancar (p. ej. el grafo
    // no dispara la señal por algún fallo), se revela de todas formas como red de seguridad.
    private static readonly System.Collections.Generic.HashSet<string> AnchorAwaitsBootCinematic =
        new System.Collections.Generic.HashSet<string> { "Bedroom" };

    [Tooltip("Máximo tiempo de espera (segundos) a que arranque la cinemática de arranque narrativa " +
             "de un anchor listado en AnchorAwaitsBootCinematic, antes de revelar la pantalla de " +
             "todas formas por si el grafo no llegara a dispararla (red de seguridad).")]
    [SerializeField] private float bootCinematicTimeout = 5f;

    private bool _initialized;

    void OnEnable()
    {
        // Cubrir la pantalla de negro YA, antes de que se procese nada más este frame — ver el
        // tooltip de bootFadeInDuration más arriba. EnsureAnchorSceneAndSpawn() se encarga de
        // hacer el fade de entrada cuando el jugador ya está colocado (o de restaurar la
        // visibilidad igualmente si algo falla antes de llegar ahí, ver los early-return de
        // InitializeWorld()).
        FeedbackService.SetScreenFadeImmediate(Color.black);

        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(WorldBootstrap));
        
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
        else
        {
            // ✅ En editor, Start puede cargarse aditivamente - Esperar un momento antes de asumir que no existe
            #if UNITY_EDITOR
            Debug.Log("[WorldBootstrap] ⏳ GameBootService no disponible aún - Esperando por si Start se carga aditivamente...");
            StartCoroutine(WaitForGameBootServiceOrFallback());
            #else
            // En build, si no hay GameBootService es un error grave - no se puede continuar
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[WorldBootstrap] ❌ FATAL: GameBootService no disponible en build. La escena 'Start' debe cargarse primero.");
            #endif
            #endif
        }
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// En editor, espera un tiempo razonable por si Start se está cargando aditivamente.
    /// Si después de esperar no hay GameBootService, usa valores por defecto.
    /// </summary>
    private System.Collections.IEnumerator WaitForGameBootServiceOrFallback()
    {
        int framesWaited = 0;
        
        // Esperar indefinidamente hasta que GameBootService esté disponible
        // (con timeout de seguridad de 30 segundos para detectar errores graves)
        while (!GameBootService.IsAvailable)
        {
            yield return null;
            framesWaited++;
            
            // Timeout de seguridad: 1800 frames = 30 segundos a 60 FPS
            if (framesWaited > 1800)
            {
                Debug.LogError($"[WorldBootstrap] ❌ FATAL: GameBootService no disponible después de 30 segundos ({framesWaited} frames)");
                Debug.LogError($"[WorldBootstrap] ❌ Verifica que la escena 'Start' esté en Build Settings y se haya cargado correctamente");
                Debug.LogError($"[WorldBootstrap] ❌ AutoBootstrapOnPlay debe haber cargado 'Start' aditivamente antes de PlayMode");
                yield break;
            }
        }
        
        Debug.Log($"[WorldBootstrap] ✅ GameBootService disponible después de {framesWaited} frame(s) - Inicializando normalmente");
        HandleProfileReady();
    }
    #endif

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;
        // Usar corutina para dar tiempo a que los SpawnAnchor se registren en OnEnable
        StartCoroutine(InitializeWorldDelayed());
        _initialized = true;
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private System.Collections.IEnumerator InitializeWorldDelayed()
    {
        // Esperar un frame para que todos los OnEnable de los SpawnAnchor se ejecuten
        yield return null;
        InitializeWorld();
    }

    private void InitializeWorld()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WorldBootstrap] 🌍 InitializeWorld() - Iniciando configuración del mundo");
#endif

        // BUGFIX: al inicializar el mundo siempre estamos en gameplay normal (nunca a mitad de
        // una cinemática), así que este es un punto seguro para tirar de golpe cualquier flag de
        // sesión estático que pudiera haber quedado "colgado" en true/incrementado por una sesión
        // anterior cortada abruptamente (p.ej. cerrar la demo a mitad de una cinemática para ir a
        // Créditos). Ver GameBootService.ResetTransientSessionState() para el detalle de qué se
        // resetea y por qué.
        GameBootService.ResetTransientSessionState();

        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[WorldBootstrap] ¡No se encontró GameBootProfile en GameBootService!");
#endif
            // No vamos a llegar a EnsureAnchorSceneAndSpawn (que es quien normalmente deshace el
            // fade de OnEnable) — restaurar la visibilidad aquí para no dejar la pantalla en negro
            // para siempre ante este error.
            FeedbackService.ScreenFade(Color.black, bootFadeInDuration, fadeIn: false);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WorldBootstrap] Profile encontrado - ShouldBootFromPreset: {bootProfile.ShouldBootFromPreset()}");
#endif

        // Refugio de lluvia: (re)enganchar el relay de clima al DayNightCycle de esta escena.
        // Ver NPCWeatherAwareness — evita que cada NPC haga su propio FindAnyObjectByType.
        NPCWeatherAwareness.Resubscribe();

        // 1) Modo PRESET (test): SIEMPRE tiene prioridad sobre saves
        if (bootProfile.ShouldBootFromPreset())
        {
            bootProfile.EnsureRuntimePresetFromTemplate(bootProfile.bootPreset);

            // ✅ El anchor ya fue establecido por SpawnManager.HandleProfileReady()
            var anchor = bootProfile.GetStartAnchorOrDefault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WorldBootstrap] 📍 Modo PRESET - Anchor desde profile: '{anchor}', CurrentAnchorId: '{SpawnManager.CurrentAnchorId}'");
#endif

            var testPreset = bootProfile.GetActivePresetResolved();
            if (testPreset != null)
            {
                // Restaurar quests
                var qm = QuestManager.Instance;
                if (qm != null)
                {
                    qm.RestoreFromProfileFlags(testPreset.flags);
                }

                // ✅ CRÍTICO: Aplicar posiciones de NPCs AQUÍ (cuando MainWorld ya está cargada)
                // GameBootService.ApplyPresetAsLoadedGame() se ejecuta ANTES de que MainWorld cargue
                // En ese momento los NPCs no existen, por lo que NO se pueden posicionar
                // AHORA es el momento correcto porque MainWorld está cargada y los NPCs existen
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[WorldBootstrap] 🎯 Aplicando posiciones de {testPreset.npcPositions?.Count ?? 0} NPCs desde preset (modo testeo)");
#endif
                bootProfile.ApplyNpcPositionsToScene(testPreset);
            }

            // Aplicar entorno del anchor de spawn antes de esperar al jugador
            // Así el skybox/interior es correcto desde el primer frame visible
            StartCoroutine(EnsureAnchorSceneAndSpawn(anchor));
            // Debug.Log("[WorldBootstrap] Iniciado en modo PRESET (testing)");
            return;
        }

        // 2) Flujo normal: El runtimePreset ya está configurado por GameBootService.PrepareActivePreset()
        // Simplemente aplicar el preset al jugador sin recargar el save
        string anchorId = bootProfile.GetStartAnchorOrDefault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WorldBootstrap] 📍 Modo NORMAL - Anchor desde profile: '{anchorId}', CurrentAnchorId: '{SpawnManager.CurrentAnchorId}'");
#endif
        
        var runtimePreset = bootProfile.GetActivePresetResolved();
        if (runtimePreset != null)
        {
            // Aplicar posiciones de NPCs usando el método robusto de GameBootProfile
            bootProfile.ApplyNpcPositionsToScene(runtimePreset);
            
            // Aplicar el preset al jugador (incluye inventario y abilities)
            if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: true, allowSceneLookup: true))
                presetService.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
            else
            {
                var svc = ServiceLocator.Get<PlayerPresetService>(false);
                if (svc != null) svc.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
            }
            
            // Aplicar estado de quests desde el preset
            var qm = QuestManager.Instance;
            if (qm != null) qm.RestoreFromProfileFlags(runtimePreset.flags);
            
            // Debug.Log($"[WorldBootstrap] Usando runtimePreset ya configurado → Anchor: '{anchorId}'");
        }

        // 3) Colocar jugador
        SpawnManager.SetCurrentAnchor(anchorId);
        // Aplicar entorno del anchor de spawn antes de esperar al jugador
        StartCoroutine(EnsureAnchorSceneAndSpawn(anchorId));
    }

    /// <summary>
    /// Si el anchor de arranque vive en una escena de interior separada (ver AnchorHomeScene) que
    /// todavía no está cargada, la carga en aditivo y espera a que su SpawnAnchor se registre
    /// antes de continuar. Si el anchor ya está disponible (misma escena, o interior ya cargado
    /// por otro motivo), no hace nada y sigue igual que antes.
    /// </summary>
    private System.Collections.IEnumerator EnsureAnchorSceneAndSpawn(string anchorId)
    {
        yield return EnsureAnchorSceneLoaded(anchorId);
        ApplySpawnEnvironmentNow(anchorId);
        // yield return (no StartCoroutine suelta) — necesitamos saber cuándo termina de verdad,
        // incluida su rama de fallback (TeleportWhenActive), antes de deshacer el fade de OnEnable.
        yield return WaitForPlayerAndTeleport(anchorId);

        if (AnchorAwaitsBootCinematic.Contains(anchorId))
        {
            yield return WaitForBootCinematicOrTimeout();
            if (CinematicSequencerBase.AnySequenceActive)
            {
                // Ya hay una cinemática de arranque en marcha (ej. PrologueDreamSequencer) — es
                // ella quien decide cuándo y cómo revelar la pantalla (fade-in propio si hiciera
                // falta + su propio fade-out cuando toque mostrar la escena). No la pisamos.
                yield break;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[WorldBootstrap] ⚠️ Se esperaba una cinemática de arranque para el anchor '{anchorId}' pero no arrancó en {bootCinematicTimeout}s — revelando la pantalla igualmente (red de seguridad).");
#endif
        }

        yield return FeedbackService.ScreenFadeAsync(Color.black, bootFadeInDuration, fadeIn: false);
    }

    /// <summary>
    /// Espera a que alguna cinemática empiece (CinematicSequencerBase.AnySequenceActive) o a que
    /// pase bootCinematicTimeout segundos, lo que ocurra antes. No asume qué cinemática es —
    /// cualquier CinematicSequencerBase que arranque durante la espera cuenta.
    /// </summary>
    private System.Collections.IEnumerator WaitForBootCinematicOrTimeout()
    {
        float elapsed = 0f;
        while (!CinematicSequencerBase.AnySequenceActive && elapsed < bootCinematicTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private System.Collections.IEnumerator EnsureAnchorSceneLoaded(string anchorId)
    {
        if (string.IsNullOrEmpty(anchorId)) yield break;
        if (AnchorRegistry.Get(anchorId) != null) yield break; // ya registrado, nada que cargar

        if (!AnchorHomeScene.TryGetValue(anchorId, out var sceneName) || string.IsNullOrEmpty(sceneName))
            yield break; // no hay escena de interior conocida para este anchor

        var existing = SceneManager.GetSceneByName(sceneName);
        if (!existing.IsValid() || !existing.isLoaded)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WorldBootstrap] 🚪 Anchor '{anchorId}' vive en '{sceneName}' y no está cargada — cargándola en aditivo antes de teletransportar...");
#endif
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                while (!op.isDone) yield return null;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogError($"[WorldBootstrap] ❌ No se pudo iniciar la carga aditiva de '{sceneName}' (¿está en Build Settings?).");
            }
#endif
        }

        // Margen de un frame para que el SpawnAnchor recién cargado termine su OnEnable
        // y se registre en AnchorRegistry antes de que sigamos.
        yield return null;
    }

    private void ApplySpawnEnvironmentNow(string anchorId)
    {
        if (string.IsNullOrEmpty(anchorId)) return;
        var ec = EnvironmentController.Instance;
        if (ec == null) return;

        SpawnAnchor anchor = AnchorRegistry.Get(anchorId);
        if (anchor == null)
        {
            var all = FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include);
            foreach (var candidate in all)
            {
                if (candidate.anchorId == anchorId) { anchor = candidate; break; }
            }
        }

        if (anchor == null) { ec.ApplyExterior(); return; }

        var env = anchor.GetComponentInParent<AnchorEnvironment>(includeInactive: true);
        if (env != null && env.isInterior) ec.ApplyInterior(env);
        else ec.ApplyExterior();
    }


    private System.Collections.IEnumerator WaitForPlayerAndTeleport(string anchorId)
    {
        GameObject player = null;
        int maxAttempts = 100;
        int attempts = 0;

        // Buscar al jugador usando PlayerService (con scene lookup para encontrarlo si empieza inactivo)
        while (player == null && attempts < maxAttempts)
        {
            PlayerService.TryGetPlayer(out player, allowSceneLookup: true);
            if (player == null)
            {
                yield return new WaitForSeconds(0.05f);
                attempts++;
            }
        }

        if (player == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[WorldBootstrap] No se encontró el jugador via PlayerService.");
#endif
            yield break;
        }

        // Debug.Log($"[WorldBootstrap] 🎮 Jugador encontrado: {player.name}, teletransportando a '{anchorId}'");

        // Esperar a que el jugador esté activo
        attempts = 0;
        while (!player.activeInHierarchy && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.05f);
            attempts++;
        }

        // Teleportar al jugador
        if (player.activeInHierarchy)
        {
            // Debug.Log($"[WorldBootstrap] ✅ Ejecutando TeleportTo('{anchorId}')");
            SpawnManager.TeleportTo(anchorId, false);
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[WorldBootstrap] ⚠️ Jugador no activo, programando teleport diferido a '{anchorId}'");
#endif
            SpawnManager.SetCurrentAnchor(anchorId);
            yield return TeleportWhenActive(player, anchorId);
        }
    }

    private System.Collections.IEnumerator TeleportWhenActive(GameObject player, string anchorId)
    {
        int maxAttempts = 200;
        int attempts = 0;

        while (player != null && !player.activeInHierarchy && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.05f);
            attempts++;
        }

        if (player != null && player.activeInHierarchy)
        {
            SpawnManager.TeleportTo(anchorId, false);
        }
    }
}

