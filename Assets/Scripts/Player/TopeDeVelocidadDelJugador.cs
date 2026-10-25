using System.Collections.Generic;
using Game.NPC.Common;
using Invector.vCharacterController;
using UnityEngine;

/// Tope de velocidad del jugador pedido por otros sistemas (p. ej. al seguir a un NPC, para no
/// adelantarle). Cada sistema pone y quita su propio tope; manda el más bajo de los que haya.
/// Lo aplica el motor del jugador (vThirdPersonMotor.topeDeVelocidad). La animación se ajusta a esa
/// velocidad con el MISMO criterio que los NPCs (NavMeshAgentUtility.FactorDeLocomocion), así que
/// al seguir a un NPC el jugador se mueve a su misma velocidad, sin adelantarle. Ver INC-536 e INC-545.
public static class TopeDeVelocidadDelJugador
{
    private static readonly Dictionary<object, float> Topes = new();
    private readonly struct Seguimiento
    {
        public readonly Transform guia;
        public readonly Vector3 direccion;
        public readonly float velocidad, separacion;
        public Seguimiento(Transform guia, Vector3 direccion, float velocidad, float separacion)
        { this.guia = guia; this.direccion = direccion; this.velocidad = velocidad; this.separacion = separacion; }
    }
    private static readonly Dictionary<object, Seguimiento> Seguimientos = new();
    private static vThirdPersonMotor _motor;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Topes.Clear();
        Seguimientos.Clear();
        _motor = null;
        PlayerService.OnPlayerRegistered -= AlCambiarJugador;
    }
#endif

    /// Pone (o cambia) el tope de 'quien', en m/s.
    public static void Poner(object quien, float metrosPorSegundo)
    {
        if (quien == null) return;
        if (Topes.Count == 0) PlayerService.OnPlayerRegistered += AlCambiarJugador;
        Topes[quien] = Mathf.Max(0f, metrosPorSegundo);
        Aplicar();
    }

    /// Conserva un espacio detrás del guía sin impedir retroceder ni apartarse a los lados.
    public static void PonerSeguimiento(object quien, float tope, Transform guia, Vector3 direccion,
        float velocidad, float separacion)
    {
        if (quien == null || guia == null) return;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return;
        Seguimientos[quien] = new Seguimiento(guia, direccion.normalized, Mathf.Max(0f, velocidad), Mathf.Max(0f, separacion));
        Poner(quien, tope);
    }

    /// Quita el tope de 'quien'. No hace nada si no tenía.
    public static void Quitar(object quien)
    {
        if (quien == null || !Topes.Remove(quien)) return;
        Seguimientos.Remove(quien);
        if (Topes.Count == 0) PlayerService.OnPlayerRegistered -= AlCambiarJugador;
        Aplicar();
    }

    private static void AlCambiarJugador(GameObject _) => Aplicar();

    private static void Aplicar()
    {
        float tope = float.PositiveInfinity;
        foreach (var v in Topes.Values) tope = Mathf.Min(tope, v);

        PlayerService.TryGetComponent<vThirdPersonMotor>(out var motor);
        if (_motor != null && _motor != motor)
        {
            _motor.topeDeVelocidad = float.PositiveInfinity;
            _motor.topeDeAnimacion = float.PositiveInfinity;
            _motor.filtroDeTopesDeVelocidad = null;
        }
        _motor = motor;
        if (_motor == null) return;

        _motor.topeDeVelocidad = tope;
        _motor.filtroDeTopesDeVelocidad = Seguimientos.Count > 0 ? LimitarSeguimientos : null;
        _motor.topeDeAnimacion = float.IsPositiveInfinity(tope)
            ? float.PositiveInfinity
            : NavMeshAgentUtility.FactorDeLocomocion(tope);
    }

    private static Vector3 LimitarSeguimientos(Vector3 velocidad)
    {
        if (_motor == null) return velocidad;
        foreach (var seguimiento in Seguimientos.Values)
        {
            if (seguimiento.guia == null) continue;
            float hueco = Vector3.Dot(seguimiento.guia.position - _motor.transform.position, seguimiento.direccion) - seguimiento.separacion;
            float avance = Vector3.Dot(velocidad, seguimiento.direccion);
            // Frena al acercarse al guía; el límite por paso evita cruzar su posición por inercia.
            float permitido = Mathf.Max(0f, seguimiento.velocidad + hueco * 2f);
            permitido = Mathf.Min(permitido, Mathf.Max(0f, seguimiento.velocidad + hueco / Mathf.Max(0.001f, Time.fixedDeltaTime)));
            if (avance > permitido) velocidad -= seguimiento.direccion * (avance - permitido);
        }
        return velocidad;
    }
}
