using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Arranca el minijuego del pilla-pilla (TagMinigameController) y espera a que el jugador gane. Si
/// se aborta, el propio minijuego hace la vuelta atrás de la historia; aquí solo se suelta.
[Serializable]
[NarrativeNodeInfo("Minijuegos", "Minijuego pilla-pilla", "Arranca el pilla-pilla y espera a que el jugador gane.")]
[NarrativeNoWorldEffect]
public sealed class PedirPillaPilla : NarrativeRequest
{
    [Tooltip("Id del minijuego (TagMinigameController.MinigameId).")]
    public string minigameId = "TAG_MINIGAME_01";

    [Header("Opciones")]
    [Tooltip("Activa el GameObject del minijuego antes de empezar.")]
    public bool activateOnStart = true;

    [Tooltip("Desactiva el GameObject del minijuego al terminar.")]
    public bool deactivateOnEnd = false;

    public override void Describe(INarrativeNodeDescriber d) => d.Line(minigameId, NarrativeTextTone.Strong);
}

public sealed class PillaPillaEnCurso : NarrativeRequestRun<PedirPillaPilla>
{
    INarrativeSignals _signals;
    Action _alGanar;
    Action _alAbortar;
    string _claveGana;
    string _claveAborta;

    protected override void Start()
    {
        _signals = Ctx?.Signals ?? DefaultNarrativeSignals.Instance;
        var minijuego = Buscar(R.minigameId);
        if (_signals == null || minijuego == null)
        {
            Debug.LogError($"[Pilla-pilla:{StepGuid}] Sin señales o sin TagMinigameController con id='{R.minigameId}'. Sigue.");
            Finish(0);
            return;
        }
        if (minijuego.IsAlreadyCompleted())
        {
            Finish(0);
            return;
        }
        if (R.activateOnStart && !minijuego.gameObject.activeInHierarchy) minijuego.gameObject.SetActive(true);

        _claveGana = $"MINIGAME_{R.minigameId}_WON";
        _claveAborta = $"MINIGAME_ABORTED:{R.minigameId}";
        _alGanar = () =>
        {
            if (R.deactivateOnEnd && minijuego != null) minijuego.gameObject.SetActive(false);
            Finish(0);
        };
        // El aborto lo gestiona el minijuego (vuelta atrás y señal): aquí solo se dejan de escuchar.
        _alAbortar = Stop;
        _signals.OnCustom(_claveGana, _alGanar);
        _signals.OnCustom(_claveAborta, _alAbortar);
        minijuego.StartMinigame();
    }

    protected override void Stop()
    {
        if (_signals != null)
        {
            if (_alGanar != null) _signals.OffCustom(_claveGana, _alGanar);
            if (_alAbortar != null) _signals.OffCustom(_claveAborta, _alAbortar);
        }
        _signals = null;
        _alGanar = null;
        _alAbortar = null;
    }

    static TagMinigameController Buscar(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var m in raiz.GetComponentsInChildren<TagMinigameController>(true))
                    if (m.MinigameId == id) return m;
        }
        return null;
    }
}
