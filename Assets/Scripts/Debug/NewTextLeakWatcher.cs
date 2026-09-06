using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// DIAGNÓSTICO TEMPORAL (INC-180, 6 sept 2026): dos fixes ya aplicados (evento
/// OnAnyBattleStarted en TagMinigameController + estado activo por defecto de
/// PanelInstructions en MainWorld.unity) no bastaron — Raúl confirma que el placeholder sin
/// traducir "New Text" sigue apareciendo, solo durante las fases de batalla de un boss. Búsqueda
/// exhaustiva (todas las escenas + todos los .prefab del proyecto) encuentra el literal
/// "New Text" en exactamente 3 sitios: el ya arreglado (TagMinigameController/instructionsText,
/// MainWorld.unity) y dos más no investigados hasta ahora en Start.unity — la etiqueta de nombre
/// de hablante del DialogueManager principal (SpeakerName) y el texto de cuerpo del subtítulo de
/// lore ambiental (LorePopupUI/BodyText). Ninguno de los dos tiene, por lectura de código, una
/// vía de disparo claramente ligada a un boss — así que en vez de aplicar un tercer fix a
/// ciegas, este watcher vigila los tres candidatos (y cualquier otro TextMeshProUGUI con ese
/// mismo texto sin editar) y registra en el log, con ruta completa de jerarquía y timestamp, el
/// instante exacto en que se vuelve realmente visible en pantalla. Con eso, la próxima vez que
/// se reproduzca (Editor o build de desarrollo, con la consola abierta) sabremos la causa real
/// en vez de seguir adivinando.
///
/// Seguro de dejar en el proyecto: no hace nada en builds de release (guard UNITY_EDITOR ||
/// DEVELOPMENT_BUILD), se instala solo (RuntimeInitializeOnLoadMethod, sin tocar ninguna
/// escena), y solo lee estado — nunca modifica nada. Quitar este archivo (y su .meta) una vez
/// resuelto INC-180 de verdad.
/// </summary>
public class NewTextLeakWatcher : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    const string PlaceholderText = "New Text";
    const float PollIntervalSeconds = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("~NewTextLeakWatcher (diagnóstico temporal INC-180)");
        go.AddComponent<NewTextLeakWatcher>();
        DontDestroyOnLoad(go);
        Debug.Log("[NewTextLeakWatcher] Instalado — vigilando apariciones del placeholder " +
                  $"\"{PlaceholderText}\" (diagnóstico temporal de INC-180, quitar tras resolverlo).");
    }

    bool _wasVisibleLastCheck;
    float _timer;

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < PollIntervalSeconds) return;
        _timer = 0f;

        var all = FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
        bool anyVisibleNow = false;

        foreach (var tmp in all)
        {
            if (tmp == null || tmp.text != PlaceholderText) continue;
            if (!IsEffectivelyVisible(tmp)) continue;

            anyVisibleNow = true;

            if (!_wasVisibleLastCheck)
            {
                Debug.LogWarning(
                    $"[NewTextLeakWatcher] ⚠️ Placeholder \"{PlaceholderText}\" VISIBLE en pantalla — " +
                    $"objeto: '{GetPath(tmp.transform)}' | frame={Time.frameCount} | t={Time.time:F1}s | " +
                    $"escena={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} | " +
                    $"BossIntroActivo={SceneBoundUI.IsBossIntroActive}");
            }
        }

        _wasVisibleLastCheck = anyVisibleNow;
    }

    static bool IsEffectivelyVisible(TextMeshProUGUI tmp)
    {
        if (!tmp.gameObject.activeInHierarchy) return false;
        if (tmp.canvasRenderer != null && tmp.canvasRenderer.GetAlpha() <= 0.01f) return false;

        // Recorrer CanvasGroups ancestro: si alguno tiene alpha ~0 (y los de más abajo no
        // marcan ignoreParentGroups), el texto no se ve aunque su propio alpha sea 1.
        var t = tmp.transform;
        while (t != null)
        {
            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                if (cg.alpha <= 0.01f) return false;
                if (cg.ignoreParentGroups) break;
            }
            t = t.parent;
        }
        return true;
    }

    static string GetPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        t = t.parent;
        while (t != null)
        {
            sb.Insert(0, t.name + "/");
            t = t.parent;
        }
        return sb.ToString();
    }
#endif
}
