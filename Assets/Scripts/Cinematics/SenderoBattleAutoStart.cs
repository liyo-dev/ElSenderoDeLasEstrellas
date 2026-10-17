using System.Collections;
using UnityEngine;

/// <summary>
/// Arranca automáticamente la secuencia de apertura de la Batalla Final al cargar la escena de
/// prueba o de combate, disparando la señal de entrada configurada en el `SequencePlayer`.
/// </summary>
public class SenderoBattleAutoStart : MonoBehaviour
{
    [Tooltip("Señal que dispara el SequencePlayer de la Batalla Final al arrancar la escena.")]
    [SerializeField] private string _startSignal = "SENDERO_FINAL_START";

    [Tooltip("Margen antes de lanzar la señal, para dar tiempo a que el jugador/cámara terminen de colocarse (spawn de WorldBootstrap) antes de que la cinemática tome el control. Sube este valor si la cinemática arranca demasiado pronto.")]
    [SerializeField] private float _startDelaySeconds = 0.3f;

    private bool _fired;

    private void Start()
    {
        StartCoroutine(Co_FireStartSignal());
    }

    private IEnumerator Co_FireStartSignal()
    {
        if (_fired) yield break;

        if (_startDelaySeconds > 0f)
            yield return new WaitForSeconds(_startDelaySeconds);

        if (_fired) yield break; // por si algo más ya la disparó mientras esperábamos
        _fired = true;

        if (string.IsNullOrEmpty(_startSignal))
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[SenderoBattleAutoStart] _startSignal está vacío — no se lanza ninguna señal. Rellénalo en el Inspector (por defecto \"SENDERO_FINAL_START\").");
#endif
            yield break;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[SenderoBattleAutoStart] Lanzando señal de entrada '{_startSignal}' para arrancar la Batalla Final.");
#endif
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_startSignal);
    }
}
