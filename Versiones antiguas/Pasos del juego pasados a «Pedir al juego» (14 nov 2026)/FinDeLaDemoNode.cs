using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fin de la demo: si la build es la demo (<see cref="ConfiguracionDeLaDemo.EsDemo"/>), guarda la
/// partida y lleva a los créditos; si no, sigue sin hacer nada, para que la historia continúe en
/// el juego completo. Va detrás del último nodo del tramo que entra en la demo.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Mundo", "Fin de la demo", "En la build de la demo, guarda y lleva a los créditos. En el juego completo no hace nada y sigue.")]
[NarrativeNoWorldEffect]
public sealed class FinDeLaDemoNode : NarrativeNode
{
    [Tooltip("Escena a la que se va al terminar la demo.")]
    public string escenaFinal = "Credits";

    [Min(0f)]
    [Tooltip("Segundos de espera antes de cambiar de escena (con la pantalla ya tapada).")]
    public float espera = 0.8f;

    public override void Describe(INarrativeNodeDescriber d)
        => d.Line("🏁 Fin de la demo → " + escenaFinal, NarrativeTextTone.Strong);

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (!ConfiguracionDeLaDemo.EsDemo || ctx?.Runner == null)
        {
            onReadyToAdvance?.Invoke();
            return;
        }
        ctx.Runner.StartCoroutine(Co_Terminar());
    }

    private IEnumerator Co_Terminar()
    {
        var guardado = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        if (guardado != null) Partida.Guardar(guardado);
        yield return new WaitForSecondsRealtime(espera);
        if (Application.CanStreamedLevelBeLoaded(escenaFinal)) SceneManager.LoadScene(escenaFinal);
        else Debug.LogWarning($"[FinDeLaDemoNode] La escena «{escenaFinal}» no está en la build.");
    }
}
