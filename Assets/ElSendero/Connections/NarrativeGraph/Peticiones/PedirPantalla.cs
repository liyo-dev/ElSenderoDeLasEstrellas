using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Texto a pantalla completa (recuerdos, momentos épicos, urgencia), con las frases, estilos,
/// animaciones y audio de un DramaticPhraseConfig.
[Serializable]
[NarrativeNodeInfo("Diálogo", "Texto dramático", "Texto a pantalla completa (sueños, títulos).")]
[NarrativeNoWorldEffect]
public sealed class PedirTextoDramatico : NarrativeRequest
{
    [Tooltip("Secuencia de frases dramáticas a mostrar.")]
    public DramaticPhraseConfig config;

    [Tooltip("Si true, la historia espera a que terminen todas las frases. Si false, sigue en el acto mientras se ven.")]
    public bool waitForCompletion = true;

    [Header("Música")]
    [Tooltip("Al terminar las frases, pide la música del lugar (zona, interior o escena). Si en ese momento otro sistema pone la suya (una cinemática que viene detrás), manda la suya.")]
    public bool restoreSceneMusicWhenDone = false;

    [Min(0f)]
    [Tooltip("Segundos de fundido al volver la música del lugar.")]
    public float restoreMusicFade = 2f;

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (config != null) d.Asset(config, "frases");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (config == null) warnings.Add("Falta el DramaticPhraseConfig");
        else if (config.phrases == null || config.phrases.Length == 0) warnings.Add($"'{config.name}' no tiene frases");
    }
}

public sealed class TextoDramaticoEnCurso : NarrativeRequestRun<PedirTextoDramatico>
{
    protected override void Start()
    {
        var ui = DramaticTextOverlayUI.Instance;
        if (ui == null || R.config == null || R.config.phrases == null || R.config.phrases.Length == 0)
        {
            Debug.LogError($"[Texto dramático:{StepGuid}] Falta DramaticTextOverlayUI en Start.unity o el config no tiene frases.");
            Finish(0);
            return;
        }
        if (R.waitForCompletion)
        {
            ui.Play(R.config, () => { PedirMusicaDelLugar(); Finish(0); });
            return;
        }
        ui.Play(R.config, R.restoreSceneMusicWhenDone ? (Action)PedirMusicaDelLugar : null);
        Finish(0);
    }

    void PedirMusicaDelLugar()
    {
        if (R.restoreSceneMusicWhenDone) AudioService.Instance?.PedirMusicaDelLugar(R.restoreMusicFade);
    }
}

/// Popup de lore (retratos y textos de un LorePopupConfig). La historia espera a que termine.
[Serializable]
[NarrativeNodeInfo("Diálogo", "Popup de lore", "Muestra un popup de lore con retratos y textos, y espera a que termine.")]
[NarrativeNoWorldEffect]
public sealed class PedirPopupDeLore : NarrativeRequest
{
    [Tooltip("Configuración del popup: entradas con retrato, hablante y texto.")]
    public LorePopupConfig config;

    public override void Describe(INarrativeNodeDescriber d)
    {
        if (config != null) d.Asset(config, "popup");
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (config == null) warnings.Add("Falta el LorePopupConfig");
        else if (config.entries == null || config.entries.Length == 0) warnings.Add($"'{config.name}' no tiene entradas");
    }
}

public sealed class PopupDeLoreEnCurso : NarrativeRequestRun<PedirPopupDeLore>
{
    protected override void Start()
    {
        var ui = LorePopupUI.Instance;
        if (ui == null || R.config == null || R.config.entries == null || R.config.entries.Length == 0)
        {
            Debug.LogError($"[Popup de lore:{StepGuid}] Falta LorePopupUI en el Canvas o el config no tiene entradas.");
            Finish(0);
            return;
        }
        ui.Show(R.config, () => Finish(0));
    }
}

/// Fin de la demo: en la build de la demo (<see cref="ConfiguracionDeLaDemo.EsDemo"/>) guarda la
/// partida y lleva a los créditos; en el juego completo no hace nada y la historia sigue.
[Serializable]
[NarrativeNodeInfo("Mundo", "Fin de la demo", "En la build de la demo, guarda y lleva a los créditos. En el juego completo no hace nada y sigue.")]
[NarrativeNoWorldEffect]
public sealed class PedirFinDeLaDemo : NarrativeRequest
{
    [Tooltip("Escena a la que se va al terminar la demo.")]
    public string escenaFinal = "Credits";

    [Min(0f)]
    [Tooltip("Segundos de espera antes de cambiar de escena (con la pantalla ya tapada).")]
    public float espera = 0.8f;

    public override void Describe(INarrativeNodeDescriber d)
        => d.Line("Fin de la demo → " + escenaFinal, NarrativeTextTone.Strong);
}

public sealed class FinDeLaDemoEnCurso : NarrativeRequestRun<PedirFinDeLaDemo>
{
    protected override void Start()
    {
        if (!ConfiguracionDeLaDemo.EsDemo || Runner == null)
        {
            Finish(0);
            return;
        }
        Runner.StartCoroutine(Co_Terminar());
    }

    IEnumerator Co_Terminar()
    {
        var guardado = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        if (guardado != null) Partida.Guardar(guardado);
        yield return new WaitForSecondsRealtime(R.espera);
        if (Application.CanStreamedLevelBeLoaded(R.escenaFinal)) SceneManager.LoadScene(R.escenaFinal);
        else Debug.LogWarning($"[Fin de la demo] La escena «{R.escenaFinal}» no está en la build.");
    }
}
