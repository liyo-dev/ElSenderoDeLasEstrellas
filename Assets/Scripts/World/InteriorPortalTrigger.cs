using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Invector.vCharacterController;

/// <summary>
/// Portal de interior con carga ADITIVA de escena (a diferencia de PortalTrigger, que reemplaza
/// la escena activa entera vía SceneTransitionLoader). Pensado para interiores pequeños (casas,
/// tiendas...) que se cargan/descargan encima de la escena de mundo persistente (p. ej.
/// Eldoria_Codex), en vez de recargar todo el mundo abierto solo para entrar en una habitación.
///
/// Dos usos típicos, con el mismo componente:
/// - Portal de ENTRADA (en el mundo, en la puerta): sceneToLoad = escena del interior,
///   targetAnchorId = anchor de entrada DENTRO de esa escena, sceneToUnload vacío.
/// - Portal de SALIDA (dentro del interior, en la puerta): sceneToLoad vacío,
///   targetAnchorId = anchor exterior (ya en la escena de mundo, siempre cargada),
///   sceneToUnload = la propia escena del interior (se descarga tras teleportar, liberando memoria).
///
/// Reutiliza TeleportService (fade, teleport de compañeros, aplica AnchorEnvironment del anchor
/// destino → esto ya oculta/muestra el mundo exterior vía zonesToHideOnEnter/hideExteriorWorld,
/// ver EnvironmentController.ApplyZoneVisibility) y PlayerLockService (congelar input mientras
/// se carga/descarga), igual que el resto de triggers de mundo del proyecto — no reinventa esas
/// partes, solo añade el paso de carga/descarga aditiva que no existía.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteriorPortalTrigger : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Anchor al que teletransportar tras la carga. Debe existir ya, o quedar registrado en AnchorRegistry en cuanto sceneToLoad termine de cargar (SpawnAnchor.OnEnable se registra solo).")]
    public string targetAnchorId;

    [Header("Escenas (aditivo)")]
    [Tooltip("Escena a cargar en aditivo (LoadSceneMode.Additive) antes de teletransportar. Vacío = no cargar nada (el destino ya está cargado).")]
    public string sceneToLoad;
    [Tooltip("Escena a descargar DESPUÉS de teletransportar (p. ej. el propio interior, al salir). Vacío = no descargar nada.")]
    public string sceneToUnload;

    [Header("Comportamiento")]
    [Tooltip("Si está marcado, el trigger solo se dispara una vez en toda la sesión.")]
    public bool singleUse = false;
    [Tooltip("Segundos de margen tras completar un teleport antes de poder volver a disparar ESTE trigger (evita rebotes si el jugador queda cerca del collider de destino, p. ej. dos puertas encaradas).")]
    public float reArmDelay = 1f;

    bool _busy;
    bool _used;
    float _lastCompletedAt = -999f;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (_busy) return;
        if (_used && singleUse) return;
        if (Time.unscaledTime - _lastCompletedAt < reArmDelay) return;
        if (!other.CompareTag("Player")) return;

        if (string.IsNullOrEmpty(targetAnchorId))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[InteriorPortalTrigger] {name}: targetAnchorId vacío.");
#endif
            return;
        }

        StartCoroutine(Run(other.gameObject));
    }

    IEnumerator Run(GameObject player)
    {
        _busy = true;
        _used = true;

        var lockService = PlayerLockService.Instance;
        lockService?.Acquire(this);

        // FIX (12 sep 2026, pedido de Raúl — "cuando salimos al exterior will se queda con la
        // animacion de caminar y dejamos de poder controlarle"): a diferencia de PortalTrigger (el
        // portal de cambio de escena COMPLETO, que ya hace esto en FreezePlayerMovement/
        // RestorePlayerMovement), este componente delegaba el freeze/restore únicamente en
        // PlayerLockService.Acquire/Release — que congela input/CC pero NO toca los parámetros de
        // locomoción del Animator (InputMagnitude/Horizontal/Vertical). Si el jugador entraba al
        // trigger de salida EN MOVIMIENTO (andando hacia la puerta), esos parámetros se quedaban
        // congelados con el último valor "caminando": el Animator seguía en el ciclo de andar
        // aunque el CharacterController ya estuviera desactivado, y al soltar el lock el input no
        // siempre recuperaba un estado limpio. Mismo patrón que PortalTrigger, adaptado aquí.
        // Todo el cuerpo va dentro de un try/finally para no dejar al jugador bloqueado para
        // siempre si algo de en medio (carga de escena, teleport) lanza una excepción.
        FreezePlayerLocomotion(player);

        string pendingSceneToUnload = null;

        try
        {
            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                var existing = SceneManager.GetSceneByName(sceneToLoad);
                if (!existing.IsValid() || !existing.isLoaded)
                {
                    var op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
                    if (op != null)
                    {
                        while (!op.isDone) yield return null;
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    else
                    {
                        Debug.LogError($"[InteriorPortalTrigger] {name}: no se pudo iniciar la carga aditiva de '{sceneToLoad}'.");
                    }
#endif
                }
                // Margen de un frame para que los SpawnAnchor de la escena recién cargada terminen
                // su OnEnable y se registren en AnchorRegistry antes de buscar targetAnchorId.
                yield return null;
            }

            bool teleportDone = false;
            void OnEnded() { teleportDone = true; }
            TeleportService.OnTeleportEnded += OnEnded;
            TeleportService.TeleportToAnchor(player, targetAnchorId);
            // TeleportToAnchor emite OnTeleportEnded incluso en sus casos de fallo (anchor no
            // encontrado, servicio no disponible, etc. — ver comentario "FIX C3" en TeleportService),
            // así que esta espera siempre termina, nunca se queda colgada.
            while (!teleportDone) yield return null;
            TeleportService.OnTeleportEnded -= OnEnded;

            // No descargamos sceneToUnload aquí dentro del try — ver el FIX del 12 sep 2026 más
            // abajo, en el finally, para el motivo. Solo anotamos que toca descargarla.
            pendingSceneToUnload = sceneToUnload;
        }
        finally
        {
            // FIX (12 sep 2026, reporte de Raúl — "no se mueve el player al salir de la casa"):
            // antes, el unload de sceneToUnload ocurría DENTRO de este try, antes de llegar aquí.
            // Cuando este trigger es el de SALIDA de un interior, sceneToUnload es la propia
            // escena donde vive este GameObject (p. ej. WillHouse.unity descargándose a sí misma
            // tras teleportar al jugador afuera). En cuanto SceneManager.UnloadSceneAsync termina,
            // Unity destruye los GameObjects de esa escena — incluido el que ejecuta esta
            // corrutina — y simplemente deja de invocarla: NO lanza ninguna excepción, así que
            // este finally nunca llegaba a ejecutarse. Resultado: RestorePlayerLocomotion y
            // lockService.Release nunca se llamaban, el lock de movimiento (PlayerLockService)
            // quedaba adquirido para siempre y el jugador se quedaba congelado nada más cruzar la
            // puerta — encaja con el log de Raúl, que corta justo después de
            // "PlayerLockService Acquire de InteriorPortalTrigger" sin ningún "Release" posterior.
            // Ahora el cleanup que el jugador necesita (locomoción + lock) se hace SIEMPRE aquí,
            // antes de tocar sceneToUnload; el unload de la escena se dispara después, fuera de
            // este finally, en modo "fire-and-forget" sin depender de que esta corrutina siga viva
            // para completarlo.
            RestorePlayerLocomotion(player);
            lockService?.Release(this);
            _lastCompletedAt = Time.unscaledTime;
            _busy = false;
        }

        if (!string.IsNullOrEmpty(pendingSceneToUnload))
        {
            var scene = SceneManager.GetSceneByName(pendingSceneToUnload);
            if (scene.IsValid() && scene.isLoaded)
            {
                // Fire-and-forget a propósito (ver comentario del finally): si pendingSceneToUnload
                // es la propia escena de este trigger, esta corrutina puede morir a mitad del
                // unload — ya no importa, todo el cleanup relevante para el jugador se hizo arriba.
                SceneManager.UnloadSceneAsync(scene);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[InteriorPortalTrigger] {name}: teleport a '{targetAnchorId}' completado" +
                  (string.IsNullOrEmpty(sceneToLoad) ? "" : $" (cargada '{sceneToLoad}')") +
                  (string.IsNullOrEmpty(pendingSceneToUnload) ? "" : $" (descargando '{pendingSceneToUnload}')") + ".");
#endif
    }

    // ---- Freeze/Restore de locomoción del Animator (12 sep 2026) ----
    // No toca CharacterController/input — eso ya lo gestiona PlayerLockService.Acquire/Release,
    // esto SOLO cubre lo que ese servicio no cubre: los parámetros de locomoción del Animator (si
    // no se ponen a 0, el ciclo de "andar" se queda pegado en pantalla aunque el CC esté
    // desactivado) y reciclar vThirdPersonInput para que recupere un estado interno limpio.

    private static void FreezePlayerLocomotion(GameObject player)
    {
        if (!player) return;

        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            try
            {
                animator.SetFloat(vAnimatorParameters.InputMagnitude, 0f);
                animator.SetFloat(vAnimatorParameters.InputHorizontal, 0f);
                animator.SetFloat(vAnimatorParameters.InputVertical, 0f);
                animator.SetBool(vAnimatorParameters.IsSprinting, false);
            }
            catch { /* parámetro no presente en este controller — no crítico */ }
        }

        var input = player.GetComponent<vThirdPersonInput>() ?? player.GetComponentInChildren<vThirdPersonInput>(true);
        if (input != null) input.enabled = false;
    }

    private static void RestorePlayerLocomotion(GameObject player)
    {
        if (!player) return;

        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            try
            {
                animator.SetFloat(vAnimatorParameters.InputMagnitude, 0f);
                animator.SetFloat(vAnimatorParameters.InputHorizontal, 0f);
                animator.SetFloat(vAnimatorParameters.InputVertical, 0f);
                animator.SetBool(vAnimatorParameters.IsSprinting, false);
            }
            catch { /* parámetro no presente en este controller — no crítico */ }
        }

        // Reiniciar el ciclo enable/disable, igual que PortalTrigger.RestorePlayerMovement — evita
        // que vThirdPersonInput arranque con lecturas residuales del frame en que se desactivó.
        var input = player.GetComponent<vThirdPersonInput>() ?? player.GetComponentInChildren<vThirdPersonInput>(true);
        if (input != null)
        {
            input.enabled = false;
            input.enabled = true;
        }
    }
}
