using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzle "Sello de las Piedras" — variante tipo Simón dice pensada para el Bosque Prohibido
/// (o cualquier otro claro apartado del camino principal): un grupo de <see cref="RuneStone"/>
/// muestra una vez una secuencia de encendido (la "solución"), y el jugador debe repetirla
/// interactuando con las mismas piedras en el mismo orden. Acertar dispara <see cref="OnSolved"/>
/// (pensado para mover/hundir la roca que bloquea el paso al cofre — ver
/// ForbiddenForestPuzzleBuilder.cs); fallar reinicia visualmente y repite la demostración.
///
/// Es opcional y no bloquea nada de la ruta principal — no toca NavMesh, no depende de ningún
/// sistema narrativo (NarrativeGraph/Quest) para funcionar, y no requiere arte nuevo: usa
/// piedras ya existentes en el proyecto (Fantasy_Kingdom_Pack) con una luz simple como feedback.
///
/// PERSISTENCIA (importante, ver documento de la propuesta): este componente NO guarda su
/// propio estado de "ya resuelto". La persistencia real vive en el cofre premio: si el
/// WorldPickup del cofre ya está marcado como recogido (partida guardada), lo normal es meter
/// la piedra bloqueadora en su lista "Disable On Collect" para que aparezca ya movida sin haber
/// que resolver el puzle otra vez. Si el jugador resuelve el puzle pero se va sin coger el
/// cofre, al volver a cargar la escena tendría que resolverlo de nuevo — limitación conocida y
/// aceptada para esta primera versión (ver documento).
/// </summary>
[DisallowMultipleComponent]
public class RuneSequencePuzzle : MonoBehaviour
{
    [Header("Piedras (en el orden físico que tengan en la escena — NO tiene que ser el orden de la solución)")]
    [SerializeField] private RuneStone[] stones;

    [Header("Solución")]
    [Tooltip("Índices dentro de 'stones' que hay que activar, en este orden exacto. " +
             "Ej.: {2, 0, 3} enciende primero la piedra stones[2], luego stones[0], luego stones[3].")]
    [SerializeField] private int[] solutionOrder = { 0, 2, 1 };

    [Header("Disparo de la demostración")]
    [Tooltip("Radio del trigger esférico que arranca la demostración la primera vez que el " +
             "jugador se acerca. Se añade un SphereCollider(isTrigger) en Awake si no hay ya uno.")]
    [SerializeField] private float triggerRadius = 6f;
    [SerializeField] private float delayBeforeDemo = 0.6f;
    [SerializeField] private float demoStepLitDuration = 0.5f;
    [SerializeField] private float demoStepGapDuration = 0.35f;
    [SerializeField] private bool replayDemoOnFail = true;

    [Header("Audio (claves de AudioGraphProfile — añadir las claves reales cuando existan)")]
    [SerializeField] private string failSfxKey = "RuneFail";
    [SerializeField] private string solvedSfxKey = "RuneSolved";

    [Header("Eventos")]
    [Tooltip("Se dispara una única vez, al completar la secuencia correcta.")]
    public UnityEvent OnSolved;
    [Tooltip("Se dispara cada vez que el jugador se equivoca de piedra u orden.")]
    public UnityEvent OnFailedAttempt;

    readonly List<int> _playerInput = new List<int>();
    bool _solved;
    bool _demoPlayed;
    bool _acceptingInput;
    Coroutine _demoRoutine;
    SphereCollider _trigger;

    /// <summary>
    /// Helper para herramientas de Editor (ver ForbiddenForestPuzzleBuilder.cs): rellena las
    /// piedras y la solución sin tener que forcejear con SerializedProperty para un array de
    /// referencias a componentes. Debe llamarse antes de que el objeto entre en Play/Awake real
    /// (uso normal: justo después de AddComponent, en tiempo de Editor).
    /// </summary>
    public void Configure(RuneStone[] stonesToUse, int[] solution)
    {
        stones = stonesToUse;
        solutionOrder = solution;
    }

    void Awake()
    {
        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i] == null) continue;
            stones[i].stoneIndex = i;
            stones[i].Activated += HandleStoneActivated;
        }

        _trigger = GetComponent<SphereCollider>();
        if (_trigger == null) _trigger = gameObject.AddComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.radius = triggerRadius;
    }

    void OnDestroy()
    {
        if (stones == null) return;
        foreach (var s in stones)
            if (s != null) s.Activated -= HandleStoneActivated;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_solved || _demoPlayed) return;
        if (!other.CompareTag("Player")) return;

        _demoPlayed = true;
        _demoRoutine = StartCoroutine(PlayDemo());
    }

    IEnumerator PlayDemo()
    {
        _acceptingInput = false;
        _playerInput.Clear();
        SetAllLit(false, playFeedback: false);

        yield return new WaitForSeconds(delayBeforeDemo);

        foreach (var idx in solutionOrder)
        {
            if (idx < 0 || idx >= stones.Length || stones[idx] == null) continue;
            stones[idx].SetLit(true);
            yield return new WaitForSeconds(demoStepLitDuration);
            stones[idx].SetLit(false, playFeedback: false);
            yield return new WaitForSeconds(demoStepGapDuration);
        }

        _acceptingInput = true;
    }

    void HandleStoneActivated(RuneStone stone)
    {
        if (_solved || !_acceptingInput) return;

        int step = _playerInput.Count;
        bool correctSoFar = step < solutionOrder.Length && solutionOrder[step] == stone.stoneIndex;

        if (!correctSoFar)
        {
            HandleFail();
            return;
        }

        _playerInput.Add(stone.stoneIndex);
        stone.SetLit(true);

        if (_playerInput.Count == solutionOrder.Length)
        {
            HandleSolved();
        }
    }

    void HandleFail()
    {
        _playerInput.Clear();
        SetAllLit(false, playFeedback: false);

        if (!string.IsNullOrEmpty(failSfxKey))
            AudioService.Instance?.PlaySFX(failSfxKey, 1f, transform.position);

        OnFailedAttempt?.Invoke();

        if (replayDemoOnFail)
        {
            _acceptingInput = false;
            if (_demoRoutine != null) StopCoroutine(_demoRoutine);
            _demoRoutine = StartCoroutine(PlayDemo());
        }
    }

    void HandleSolved()
    {
        _solved = true;
        _acceptingInput = false;

        if (!string.IsNullOrEmpty(solvedSfxKey))
            AudioService.Instance?.PlaySFX(solvedSfxKey, 1f, transform.position);

        OnSolved?.Invoke();
    }

    void SetAllLit(bool lit, bool playFeedback)
    {
        if (stones == null) return;
        foreach (var s in stones)
            if (s != null) s.SetLit(lit, playFeedback);
    }

    /// <summary>
    /// Fuerza el puzle a estado "ya resuelto" sin animación ni eventos — pensado para cuando el
    /// cofre premio ya consta como recogido en la partida guardada (ver nota de persistencia en
    /// la cabecera de esta clase) y no queremos que el jugador tenga que resolverlo otra vez.
    /// </summary>
    public void MarkAlreadySolved()
    {
        _solved = true;
        _demoPlayed = true;
        _acceptingInput = false;
        SetAllLit(true, playFeedback: false);
    }
}
