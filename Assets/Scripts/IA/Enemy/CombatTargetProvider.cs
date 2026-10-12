using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// A quién ataca un enemigo: a cualquiera de los tres personajes del grupo que siga en pie (el
/// que lleva el jugador o los que lleva la IA, cada uno con su vida; ver SaludDelGrupo). Los
/// caídos no cuentan. Un jefe puede ir un rato a por un aliado que le provoca
/// (SenueloDeCombate); eso lo decide la IA del jefe, no este proveedor. Ver INC-717.
/// </summary>
public static class CombatTargetProvider
{
    /// Cuánto «pesa» en metros cada enemigo que ya va a por un personaje: con varios enemigos, el
    /// siguiente prefiere a otro personaje aunque esté un poco más lejos.
    private const float PenalizacionPorAtacante = 4f;
    /// Segundos que un enemigo mantiene su objetivo antes de volver a elegir.
    private const float SegundosConElMismoObjetivo = 4f;

    private struct Eleccion
    {
        public Transform objetivo;
        public float hasta;
    }

    private static readonly Dictionary<int, Eleccion> s_elecciones = new Dictionary<int, Eleccion>();
    private static readonly List<int> s_caducadas = new List<int>();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_elecciones.Clear();
#endif

    /// <summary>
    /// El personaje en pie más cercano a esa posición (el jugador si no hay compañeros). Sin
    /// memoria: para los enemigos que vuelven a preguntar a menudo.
    /// </summary>
    public static Transform GetNearestTarget(Vector3 fromPosition)
    {
        Transform mejor = null;
        float mejorDist = float.MaxValue;
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var t = ObjetivoValido((Slot)i);
            if (t == null) continue;
            float d = (t.position - fromPosition).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; mejor = t; }
        }
        return mejor != null ? mejor : Jugador();
    }

    /// <summary>
    /// Objetivo para un enemigo concreto, repartiendo: cada personaje cuenta su distancia más
    /// <see cref="PenalizacionPorAtacante"/> por cada enemigo que ya va a por él. El enemigo
    /// conserva su objetivo unos segundos (o hasta que cae) para no cambiar a cada momento.
    /// </summary>
    public static Transform ElegirObjetivo(Vector3 desde, Object atacante)
    {
        if (atacante == null) return GetNearestTarget(desde);
        int id = atacante.GetHashCode();
        LimpiarCaducadas();

        if (s_elecciones.TryGetValue(id, out var actual) && Time.time < actual.hasta && SigueEnPie(actual.objetivo))
            return actual.objetivo;

        Transform mejor = null;
        float mejorPuntos = float.MaxValue;
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var t = ObjetivoValido((Slot)i);
            if (t == null) continue;
            float puntos = Vector3.Distance(t.position, desde) + PenalizacionPorAtacante * AtacantesDe(t, id);
            if (puntos < mejorPuntos) { mejorPuntos = puntos; mejor = t; }
        }
        if (mejor == null) mejor = Jugador();
        s_elecciones[id] = new Eleccion { objetivo = mejor, hasta = Time.time + SegundosConElMismoObjetivo };
        return mejor;
    }

    /// El enemigo deja de atacar (muere o sale del combate): su elección deja de contar.
    public static void Olvidar(Object atacante)
    {
        if (atacante != null) s_elecciones.Remove(atacante.GetHashCode());
    }

    private static Transform ObjetivoValido(Slot p)
    {
        if (SaludDelGrupo.EstaCaido(p)) return null;
        var cuerpo = ControlDelJugador.CuerpoDe(p);
        if (cuerpo == null || !cuerpo.activeInHierarchy) return null;
        if (p != ControlDelJugador.Activo)
        {
            // Los compañeros solo son objetivo si tienen vida propia (CuerpoDelGrupo).
            var vida = cuerpo.GetComponent<CuerpoDelGrupo>();
            if (vida == null || !vida.PuedeRecibirDano) return null;
        }
        return cuerpo.transform;
    }

    private static bool SigueEnPie(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        var jugador = Jugador();
        if (t == jugador) return true;
        var vida = t.GetComponent<CuerpoDelGrupo>();
        return vida != null && vida.PuedeRecibirDano;
    }

    private static int AtacantesDe(Transform t, int salvo)
    {
        int n = 0;
        foreach (var par in s_elecciones)
            if (par.Key != salvo && par.Value.objetivo == t && Time.time < par.Value.hasta) n++;
        return n;
    }

    private static void LimpiarCaducadas()
    {
        s_caducadas.Clear();
        float ahora = Time.time;
        foreach (var par in s_elecciones)
            if (ahora >= par.Value.hasta + SegundosConElMismoObjetivo || par.Value.objetivo == null) s_caducadas.Add(par.Key);
        for (int i = 0; i < s_caducadas.Count; i++) s_elecciones.Remove(s_caducadas[i]);
    }

    private static Transform Jugador() =>
        PlayerService.TryGetPlayer(out var playerGO) && playerGO != null ? playerGO.transform : null;
}
