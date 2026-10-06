using System;
using UnityEngine;

/// <summary>Comunica el habla sin depender de su presentación.</summary>
public static class SenalesDeHabla
{
    public static event Action<Transform, float> OnEmpiezaAHablar;
    public static event Action<Transform> OnDejaDeHablar;
    public static Transform HablanteActual { get; private set; }
    public static void Empieza(Transform quien, float segundos)
    {
        if (quien == null) return;
        if (HablanteActual != null && HablanteActual != quien) Para(HablanteActual);
        HablanteActual = quien;
        OnEmpiezaAHablar?.Invoke(quien, segundos);
    }
    public static void Para(Transform quien)
    {
        if (quien == null) return;
        if (HablanteActual == quien) HablanteActual = null;
        OnDejaDeHablar?.Invoke(quien);
    }
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        HablanteActual = null;
        OnEmpiezaAHablar = null;
        OnDejaDeHablar = null;
    }
#endif
}
