using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Espera a que una señal custom llegue N veces (p. ej. «derrota ocho enemigos de un tipo»).
/// Solo cuenta las señales que llegan mientras el nodo está activo; la cuenta se guarda en el
/// blackboard, así que sobrevive a guardar y cargar. Opcionalmente marca un paso de una quest
/// por cada señal, para que el diario muestre el progreso (3/8).
/// </summary>
[Serializable]
[NarrativeNodeInfo("Señales", "Esperar señal N veces", "Cuenta las veces que llega una señal y avanza al llegar al total. Puede marcar un paso de quest por cada una.")]
[SavePoint("Seguro guardar mientras cuenta")]
public sealed class WaitSignalCountNode : NarrativeNode
{
    [NarrativeKey(NarrativeKeyKind.Signal)]
    [Tooltip("Señal que se cuenta. Quien la emite debe hacerlo en cada ocasión (no «una sola vez»).")]
    public string eventKey;

    [Min(1)]
    [Tooltip("Veces que tiene que llegar la señal para avanzar.")]
    public int count = 1;

    [Header("Progreso en el diario (opcional)")]
    [NarrativeKey(NarrativeKeyKind.Quest)]
    [Tooltip("Si se indica, cada señal marca como hecho el siguiente paso de esta quest (paso 0, 1, 2…).")]
    public string questId;

    [NonSerialized] private Action _handler;
    [NonSerialized] private NarrativeContext _ctx;

    private string CountKey => $"__count_{guid}";

    public override void CollectWarnings(List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) warnings.Add("No hay señal que contar");
        if (count < 1) warnings.Add("El total debe ser al menos 1");
    }

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        _ctx = ctx;
        if (string.IsNullOrWhiteSpace(eventKey) || ctx?.Signals == null)
        {
            Debug.LogWarning($"[WaitSignalCountNode:{guid}] Sin señal o sin sistema de señales: el nodo no puede contar.");
            return;
        }

        if (ctx.Blackboard.Get(CountKey, 0) >= count)
        {
            Terminar(ctx, ready);
            return;
        }

        Desuscribir();
        _handler = () =>
        {
            int actual = ctx.Blackboard.Get(CountKey, 0) + 1;
            ctx.Blackboard.Set(CountKey, actual);
            if (!string.IsNullOrWhiteSpace(questId)) ctx.Signals.CompleteQuestStep(questId, actual - 1);
            if (actual >= count)
            {
                Terminar(ctx, ready);
                return;
            }
            // OnCustom no deja registrado el oyente si consume una señal pendiente al suscribirse;
            // volver a suscribir garantiza que se siguen contando las siguientes.
            Suscribir(ctx);
        };
        // Una señal que quedó pendiente antes de llegar aquí no cuenta: solo valen las de ahora.
        ctx.Signals.ClearPendingCustom(eventKey);
        Suscribir(ctx);
    }

    public override void Exit(NarrativeContext ctx) => Desuscribir();

    private void Suscribir(NarrativeContext ctx)
    {
        ctx.Signals.OffCustom(eventKey, _handler);
        ctx.Signals.OnCustom(eventKey, _handler);
    }

    private void Desuscribir()
    {
        if (_handler != null && _ctx?.Signals != null) _ctx.Signals.OffCustom(eventKey, _handler);
    }

    private void Terminar(NarrativeContext ctx, Action ready)
    {
        Desuscribir();
        _handler = null;
        ctx.Blackboard.Set(CountKey, 0);
        ready?.Invoke();
    }
}
