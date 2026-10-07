using System.Collections.Generic;
using UnityEngine;

/// <summary>Qué se puede hacer contra una amenaza.</summary>
public enum TipoDeAmenaza
{
    /// Se devuelve o se desvía con la defensa en el momento justo.
    Devolvible,
    /// No se para: hay que apartarse.
    NoBloqueable,
}

/// <summary>Algo en vuelo que puede alcanzar al jugador (un proyectil enemigo).</summary>
public interface IAmenazaEntrante
{
    Vector3 PosicionDeAmenaza { get; }
    Vector3 VelocidadDeAmenaza { get; }
    TipoDeAmenaza TipoDeAmenaza { get; }
}

/// <summary>
/// Registro de lo que puede alcanzar al jugador, para avisarle en el momento justo
/// (<see cref="AvisoDeAmenazas"/>) sin que el aviso conozca a cada enemigo:
/// <list type="bullet">
/// <item>Los proyectiles se registran solos mientras vuelan (<see cref="IAmenazaEntrante"/>).</item>
/// <item>Los golpes con preparación (cuerpo a cuerpo) los anuncia su atacante al empezar, con los
/// segundos que faltan para el impacto (<see cref="Anunciar"/>); caducan solos.</item>
/// </list>
/// Ver INC-666.
/// </summary>
public static class AmenazasAlJugador
{
    /// <summary>Golpe anunciado: quién lo da, cuándo llega (Time.time) y de qué tipo es.</summary>
    public struct Anuncio
    {
        public Transform origen;
        public float impactoEn;
        public TipoDeAmenaza tipo;
    }

    private const float Caducidad = 0.15f;   // segundos tras el impacto en los que se descarta

    private static readonly List<IAmenazaEntrante> _proyectiles = new List<IAmenazaEntrante>(32);
    private static readonly List<Anuncio> _anuncios = new List<Anuncio>(8);

    /// <summary>Proyectiles hostiles en vuelo.</summary>
    public static IReadOnlyList<IAmenazaEntrante> Proyectiles => _proyectiles;

    /// <summary>Golpes anunciados aún vigentes (llamar antes a <see cref="LimpiarCaducados"/>).</summary>
    public static IReadOnlyList<Anuncio> Anuncios => _anuncios;

    public static void Registrar(IAmenazaEntrante amenaza)
    {
        if (amenaza != null && !_proyectiles.Contains(amenaza)) _proyectiles.Add(amenaza);
    }

    public static void Quitar(IAmenazaEntrante amenaza)
    {
        if (amenaza != null) _proyectiles.Remove(amenaza);
    }

    /// <summary>
    /// Un golpe va a llegar al jugador dentro de 'segundosHastaImpacto'. Un nuevo anuncio del
    /// mismo origen sustituye al anterior.
    /// </summary>
    public static void Anunciar(Transform origen, float segundosHastaImpacto, TipoDeAmenaza tipo)
    {
        if (origen == null) return;
        var anuncio = new Anuncio { origen = origen, impactoEn = Time.time + Mathf.Max(0f, segundosHastaImpacto), tipo = tipo };
        for (int i = 0; i < _anuncios.Count; i++)
        {
            if (_anuncios[i].origen != origen) continue;
            _anuncios[i] = anuncio;
            return;
        }
        _anuncios.Add(anuncio);
    }

    /// <summary>Retira el anuncio de un origen (p. ej. si su golpe se cancela).</summary>
    public static void RetirarAnuncio(Transform origen)
    {
        for (int i = _anuncios.Count - 1; i >= 0; i--)
            if (_anuncios[i].origen == origen) _anuncios.RemoveAt(i);
    }

    /// <summary>Quita los anuncios cuyo golpe ya pasó o cuyo atacante ya no existe.</summary>
    public static void LimpiarCaducados()
    {
        float ahora = Time.time;
        for (int i = _anuncios.Count - 1; i >= 0; i--)
            if (_anuncios[i].origen == null || _anuncios[i].impactoEn < ahora - Caducidad) _anuncios.RemoveAt(i);
    }

    /// <summary>
    /// ¿Es el cuerpo del jugador o un compañero del grupo? Para no tratar como amenaza (ni
    /// devolver) lo que lanza el propio grupo.
    /// </summary>
    public static bool EsDelGrupoDelJugador(GameObject go)
    {
        if (go == null) return false;
        var cuerpo = PlayerService.Player;
        if (cuerpo != null && (go == cuerpo || go.transform.IsChildOf(cuerpo.transform))) return true;
        return go.GetComponentInParent<Game.NPC.NPCPartyMember>() != null;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _proyectiles.Clear();
        _anuncios.Clear();
    }
#endif
}
