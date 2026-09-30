using System;
using UnityEngine;

/// Avisos genéricos de lo que pasa en un combate contra un jefe, para quien tenga que contarlo
/// (la guía de combate) sin conocer las reglas concretas de cada jefe. Quien aplica una regla
/// avisa aquí; quien comenta escucha aquí. Ver INC-489.
public static class AvisosDeCombate
{
    /// Un golpe al jefe no ha servido: se ha curado (sin el aro), ha rebotado en la coraza...
    public static event Action<GameObject> AlGolpeMalDado;

    /// El jefe ya solo cae con un golpe especial (un dúo). Quien pueda darlo, que se prepare.
    public static event Action<GameObject> AlPedirseRemate;

    public static void GolpeMalDado(GameObject jefe) => AlGolpeMalDado?.Invoke(jefe);
    public static void PedirRemate(GameObject jefe) => AlPedirseRemate?.Invoke(jefe);

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AlGolpeMalDado = null; AlPedirseRemate = null; }
#endif
}
