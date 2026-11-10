using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Mientras algún sistema lo pide, el grupo va agachado y despacio: el cuerpo del jugador y los
/// compañeros ponen el parámetro «IsCrouching» de su animator y el jugador no pasa de la velocidad
/// pedida (TopeDeVelocidadDelJugador). Cada sistema empieza y termina con su clave; el grupo se
/// levanta cuando no queda ninguna. Quien lo pida llama a <see cref="Refrescar"/> de vez en cuando
/// para que lo herede el cuerpo nuevo si cambia el personaje al mando.
public static class SigiloDelGrupo
{
    private static readonly int HashAgachado = Animator.StringToHash("IsCrouching");
    private static readonly HashSet<object> s_quienes = new();
    // Si el animator tiene el parámetro: se mira una vez por animator (leer parameters reserva memoria).
    private static readonly Dictionary<Animator, bool> s_tieneParametro = new();
    private static readonly List<Animator> s_agachados = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_quienes.Clear();
        s_tieneParametro.Clear();
        s_agachados.Clear();
    }
#endif

    /// Algún sistema tiene al grupo en sigilo.
    public static bool Activo => s_quienes.Count > 0;

    /// El grupo va agachado y el jugador, como mucho, a 'velocidadDelJugador' m/s.
    public static void Empezar(object quien, float velocidadDelJugador)
    {
        if (quien == null) return;
        s_quienes.Add(quien);
        TopeDeVelocidadDelJugador.Poner(quien, velocidadDelJugador);
        Refrescar();
    }

    /// Quita el sigilo de 'quien'. No hace nada si no lo tenía.
    public static void Terminar(object quien)
    {
        if (quien == null || !s_quienes.Remove(quien)) return;
        TopeDeVelocidadDelJugador.Quitar(quien);
        if (s_quienes.Count > 0) return;
        foreach (var a in s_agachados)
            if (a != null) a.SetBool(HashAgachado, false);
        s_agachados.Clear();
    }

    /// Vuelve a agachar los cuerpos que representan ahora al grupo.
    public static void Refrescar()
    {
        if (s_quienes.Count == 0) return;
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
            Agachar(ControlDelJugador.CuerpoDe((Slot)i));
    }

    private static void Agachar(GameObject cuerpo)
    {
        if (cuerpo == null) return;
        var animator = cuerpo.GetComponentInChildren<Animator>();
        if (animator == null || !TieneParametro(animator)) return;
        animator.SetBool(HashAgachado, true);
        if (!s_agachados.Contains(animator)) s_agachados.Add(animator);
    }

    private static bool TieneParametro(Animator animator)
    {
        if (s_tieneParametro.TryGetValue(animator, out bool tiene)) return tiene;
        tiene = false;
        foreach (var p in animator.parameters)
            if (p.nameHash == HashAgachado && p.type == AnimatorControllerParameterType.Bool) { tiene = true; break; }
        s_tieneParametro[animator] = tiene;
        return tiene;
    }
}
