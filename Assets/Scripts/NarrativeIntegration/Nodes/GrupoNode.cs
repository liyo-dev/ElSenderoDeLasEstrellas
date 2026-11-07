using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Cambia el grupo por historia: un compañero sale del grupo (se lo llevan; con
/// <see cref="retirarDeLaEscena"/> deja de estar en la escena) o vuelve a él, o un personaje pasa a
/// ir al mando (antes de una cinemática que cuenta con quién lleva el jugador). Lo hace
/// <see cref="SalidasDelGrupo"/> / <see cref="ControlDelJugador"/>; si ahora no se puede (el
/// relevo de personaje aún no ha acabado), lo reintenta unos segundos y sigue.
/// </summary>
[Serializable]
[NarrativeNodeInfo("Grupo", "Entrar o salir del grupo", "Un compañero sale del grupo (y, si se pide, de la escena) o vuelve a él; o un personaje pasa a ir al mando.")]
public sealed class GrupoNode : NarrativeNode
{
    public enum Accion
    {
        /// <summary>Sale del grupo: no sigue al jugador ni se le puede llevar.</summary>
        SaleDelGrupo,
        /// <summary>Vuelve al grupo (y a la escena, si se retiró).</summary>
        VuelveAlGrupo,
        /// <summary>Pasa a ir al mando.</summary>
        PasaAlMando,
        /// <summary>Entra en el grupo por primera vez (el compañero ya está en la escena, fuera del grupo).</summary>
        EntraEnElGrupo,
    }

    private const float SegundosDeReintento = 3f;

    public Accion accion = Accion.SaleDelGrupo;
    [Tooltip("Will, Estela o Liam.")]
    public string personaje = "Estela";
    [Tooltip("Al salir del grupo, deja también de estar en la escena hasta que vuelva.")]
    public bool retirarDeLaEscena = true;

    [NonSerialized] private Coroutine _rutina;
    [NonSerialized] private NarrativeRunner _runner;

    public override void Describe(INarrativeNodeDescriber d)
    {
        string verbo = accion switch
        {
            Accion.VuelveAlGrupo => "Vuelve al grupo",
            Accion.EntraEnElGrupo => "Entra en el grupo",
            Accion.PasaAlMando => "Pasa al mando",
            _ => retirarDeLaEscena ? "Sale del grupo y de la escena" : "Sale del grupo",
        };
        d.Actor("Actor_" + personaje, verbo);
    }

    public override void CollectWarnings(List<string> warnings)
    {
        if (!ControlDelJugador.SlotDeNombre(personaje, out var slot)) warnings.Add($"«{personaje}» no es del grupo (Will, Estela o Liam)");
        else if (slot == Slot.Will && accion != Accion.PasaAlMando) warnings.Add("Will no sale ni entra en el grupo");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (!ControlDelJugador.SlotDeNombre(personaje, out var slot))
        {
            Debug.LogWarning($"[GrupoNode] «{personaje}» no es del grupo.");
            onReadyToAdvance?.Invoke();
            return;
        }
        if (Hacer(slot) || ctx?.Runner == null)
        {
            onReadyToAdvance?.Invoke();
            return;
        }
        _runner = ctx.Runner;
        _rutina = _runner.StartCoroutine(Co_Reintentar(slot, onReadyToAdvance));
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_rutina != null && _runner != null) _runner.StopCoroutine(_rutina);
        _rutina = null;
        _runner = null;
    }

    private bool Hacer(Slot slot) => accion switch
    {
        Accion.VuelveAlGrupo => SalidasDelGrupo.Devolver(slot),
        Accion.EntraEnElGrupo => SalidasDelGrupo.Unir(slot),
        Accion.PasaAlMando => ControlDelJugador.PasarElMandoA(slot) && ControlDelJugador.Activo == slot
                              && (ActiveCharacterSwapper.Instance == null || ActiveCharacterSwapper.Instance.IsReady),
        _ => SalidasDelGrupo.Sacar(slot, retirarDeLaEscena),
    };

    private IEnumerator Co_Reintentar(Slot slot, Action onReadyToAdvance)
    {
        float limite = Time.unscaledTime + SegundosDeReintento;
        while (!Hacer(slot) && Time.unscaledTime < limite) yield return null;
        if (ControlDelJugador.Activo != slot && accion == Accion.PasaAlMando)
            Debug.LogWarning($"[GrupoNode] No se ha podido pasar el mando a {personaje}.");
        if (accion == Accion.EntraEnElGrupo && !ControlDelJugador.EnElGrupo(slot))
            Debug.LogWarning($"[GrupoNode] {personaje} no ha podido entrar en el grupo: no está en la escena.");
        _rutina = null;
        onReadyToAdvance?.Invoke();
    }
}
