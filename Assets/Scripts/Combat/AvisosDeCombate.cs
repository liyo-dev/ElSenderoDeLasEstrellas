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

    /// El jugador ha hecho algo que da fuerza al jefe (fuego de frente contra el Gólem).
    public static event Action<GameObject> AlFortalecerse;

    /// El terreno ha atrapado al jefe (el Gólem hundido en el barro): es el momento de atacar.
    public static event Action<GameObject> AlQuedarAtrapado;

    /// Se ha roto una parte de la defensa del jefe (una runa del aro del Demonio 2).
    public static event Action<GameObject> AlRomperseDefensa;

    /// La defensa del jefe se ha vuelto a cerrar entera (el aro se recompone).
    public static event Action<GameObject> AlRecomponerseDefensa;

    public static void GolpeMalDado(GameObject jefe) => AlGolpeMalDado?.Invoke(jefe);
    public static void PedirRemate(GameObject jefe) => AlPedirseRemate?.Invoke(jefe);
    public static void Fortalecerse(GameObject jefe) => AlFortalecerse?.Invoke(jefe);
    public static void QuedarAtrapado(GameObject jefe) => AlQuedarAtrapado?.Invoke(jefe);
    public static void RomperseDefensa(GameObject jefe) => AlRomperseDefensa?.Invoke(jefe);
    public static void RecomponerseDefensa(GameObject jefe) => AlRecomponerseDefensa?.Invoke(jefe);

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        AlGolpeMalDado = null; AlPedirseRemate = null;
        AlFortalecerse = null; AlQuedarAtrapado = null; AlRomperseDefensa = null; AlRecomponerseDefensa = null;
    }
#endif
}
