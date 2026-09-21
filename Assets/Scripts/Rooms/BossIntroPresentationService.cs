using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;
using Sendero.UI;

/// <summary>
/// Servicio global de presentación cinemática de bosses (INC-207). Se auto-crea al arrancar el
/// juego, mismo patrón que <c>HudToastService</c> — no requiere prefab ni setup en escena.
///
/// Sustituye a tener que colocar un <c>BossIntroPresentation</c> a mano en cada arena y
/// asignarlo en el Inspector del <c>BossArenaController</c> correspondiente. Las arenas que YA
/// tienen un <c>BossIntroPresentation</c> propio (con sus colores/tiempos ya ajustados a mano en
/// la escena) siguen usando ese camino sin cambios — <c>BossArenaController</c> solo recurre a
/// este servicio cuando ese campo se deja vacío, así que no hay ninguna migración obligatoria.
///
/// Uso:
///   yield return StartCoroutine(BossIntroPresentationService.Instance.PlayIntroduction(bossTransform, bossCamera, "DEMONIO"));
/// </summary>
public class BossIntroPresentationService : MonoBehaviour
{
    public static BossIntroPresentationService Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;

        var root = new GameObject("[BossIntroPresentationService]");
        DontDestroyOnLoad(root);
        Instance = root.AddComponent<BossIntroPresentationService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Configuración por defecto (mismos valores que tenía BossIntroPresentation.cs) ──
    private const float IntroDuration = 3f;
    private const float ShakeDelay = 0.5f;
    private static readonly Color BossNameGradientLeft = new Color(1f, 0.75f, 0.1f, 1f);
    private static readonly Color BossNameGradientRight = new Color(0.9f, 0.12f, 0.05f, 1f);
    private const float ShakeIntensity = 0.3f;
    private const float ShakeDuration = 0.5f;
    private const float CameraFadeDuration = 0.3f;
    private static readonly Color FadeColor = Color.black;
    private const string BossRoarSfxKey = "Boss_Roar";

    private Camera _mainCamera;
    private bool _isPlaying;

    /// <summary>
    /// Reproduce la presentación cinemática del boss: fade a la cámara del boss, nombre en
    /// pantalla, roar/shake, y vuelta a la cámara principal. Misma coreografía que tenía
    /// <c>BossIntroPresentation.PlayIntroduction()</c>, con los parámetros pasados directamente
    /// en vez de por <c>SetupBoss()</c> separado.
    /// </summary>
    public IEnumerator PlayIntroduction(Transform bossTransform, Camera bossCamera, string bossName, bool isIndoor = false)
    {
        if (_isPlaying || bossCamera == null) yield break;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) yield break;

        _isPlaying = true;
        bool cameraSwapped = false;

        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Acquire(this);

        // Reclamar el candado de cámara (ver CameraDirectorService), mismo criterio que ya usaba
        // BossIntroPresentation: evita que vThirdPersonCamera retome el control de gameplay
        // durante los SetActive() de cámara de esta presentación.
        CameraDirectorService.Claim(this);

        SceneBoundUI.BeginBossIntro(0.25f);
        PlayerHUDV2.Instance?.HideHUD(0.25f);

        // Todo lo que sigue va envuelto en try/finally: si un boss concreto lanza una excepción a
        // mitad de la presentación, el HUD/UI se restaura igualmente en vez de quedar oculto para
        // siempre (mismo criterio que ya tenía BossIntroPresentation.cs).
        try
        {
            if (!FeedbackService.IsScreenFaded)
                yield return FeedbackService.ScreenFadeAsync(FadeColor, CameraFadeDuration, fadeIn: true);

            if (isIndoor)
            {
                bossCamera.clearFlags = CameraClearFlags.SolidColor;
                bossCamera.backgroundColor = Color.black;
            }
            _mainCamera.gameObject.SetActive(false);
            bossCamera.gameObject.SetActive(true);
            cameraSwapped = true;

            yield return FeedbackService.ScreenFadeAsync(FadeColor, CameraFadeDuration, fadeIn: false);

            float nameDuration = Mathf.Max(IntroDuration - CameraFadeDuration - 0.5f, 0.5f);
            bool usedDramaticText = false;

            if (DramaticTextOverlayUI.Instance != null)
            {
                DramaticTextOverlayUI.Instance.PlayBossName(
                    bossName, BossNameGradientLeft, BossNameGradientRight, nameDuration, null);
                usedDramaticText = true;
            }

            if (!string.IsNullOrWhiteSpace(BossRoarSfxKey) && AudioService.Instance != null)
            {
                Vector3 roarPos = bossTransform != null ? bossTransform.position : transform.position;
                AudioService.Instance.PlaySFX(BossRoarSfxKey, 1f, roarPos);
            }

            float elapsed = 0f;
            bool shakeDone = false;

            while (elapsed < IntroDuration)
            {
                elapsed += Time.deltaTime;
                if (!shakeDone && elapsed >= ShakeDelay)
                {
                    shakeDone = true;
                    FeedbackService.CameraShake(bossCamera, ShakeIntensity, ShakeDuration);
                    FeedbackService.ScreenFlash(Color.white, 0.1f);
                }
                yield return null;
            }

            if (usedDramaticText && DramaticTextOverlayUI.Instance != null
                && DramaticTextOverlayUI.Instance.IsPlaying)
            {
                DramaticTextOverlayUI.Instance.ForceStop();
            }

            yield return FeedbackService.ScreenFadeAsync(FadeColor, CameraFadeDuration, fadeIn: true);

            bossCamera.gameObject.SetActive(false);
            _mainCamera.gameObject.SetActive(true);
            cameraSwapped = false;

            yield return FeedbackService.ScreenFadeAsync(FadeColor, CameraFadeDuration, fadeIn: false);
        }
        finally
        {
            // Red de seguridad (INC-207 hotfix 2026-09-15): si cualquier paso anterior lanzó una
            // excepción a mitad de la presentación (p. ej. una referencia nula específica de un
            // boss concreto), NUNCA dejar la pantalla en negro ni la cámara del boss activa para
            // siempre. yield no está permitido dentro de un finally, así que la restauración aquí
            // es siempre instantánea (sin fundido) — mejor un corte brusco que una pantalla negra
            // permanente que obligue a reiniciar el juego.
            if (cameraSwapped)
            {
                if (bossCamera != null) bossCamera.gameObject.SetActive(false);
                if (_mainCamera != null) _mainCamera.gameObject.SetActive(true);
            }
            if (FeedbackService.IsScreenFaded)
            {
                FeedbackService.SetScreenFadeImmediate(Color.clear);
            }

            SceneBoundUI.EndBossIntro(0.35f);
            PlayerHUDV2.Instance?.ShowHUD(0.35f);

            if (PlayerLockService.HasInstance) PlayerLockService.Instance.Release(this);

            CameraDirectorService.Release(this);

            _isPlaying = false;
        }
    }
}
