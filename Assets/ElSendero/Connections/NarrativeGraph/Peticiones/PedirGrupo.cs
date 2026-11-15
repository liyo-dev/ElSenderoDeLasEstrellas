using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Cambia el grupo por historia: un compañero sale del grupo (se lo llevan; con
/// <see cref="retirarDeLaEscena"/> deja de estar en la escena) o vuelve a él, o un personaje pasa a
/// ir al mando (antes de una cinemática que cuenta con quién lleva el jugador). Lo hacen
/// <see cref="SalidasDelGrupo"/> y <see cref="ControlDelJugador"/>; si ahora no se puede (el
/// relevo de personaje aún no ha acabado), se reintenta unos segundos y sigue.
[Serializable]
[NarrativeNodeInfo("Grupo", "Entrar o salir del grupo", "Un compañero sale del grupo (y, si se pide, de la escena) o vuelve a él; o un personaje pasa a ir al mando.")]
public sealed class PedirGrupo : NarrativeRequest, INarrativeStateEffect
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

    public Accion accion = Accion.SaleDelGrupo;
    [Tooltip("Will, Estela o Liam.")]
    public string personaje = "Estela";
    [Tooltip("Al salir del grupo, deja también de estar en la escena hasta que vuelva.")]
    public bool retirarDeLaEscena = true;

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

    public void Project(INarrativeStateWriter state)
    {
        if (string.IsNullOrWhiteSpace(personaje)) return;
        string estado = accion switch
        {
            Accion.VuelveAlGrupo or Accion.EntraEnElGrupo => HechosDelSendero.EnElGrupo,
            Accion.PasaAlMando => HechosDelSendero.AlMando,
            _ => retirarDeLaEscena ? HechosDelSendero.FueraDeLaEscena : HechosDelSendero.FueraDelGrupo,
        };
        state.SetFact(HechosDelSendero.Grupo, personaje, estado);
    }
}

public sealed class GrupoEnCurso : NarrativeRequestRun<PedirGrupo>
{
    const float SegundosDeReintento = 3f;
    Coroutine _rutina;

    protected override void Start()
    {
        if (!ControlDelJugador.SlotDeNombre(R.personaje, out var slot))
        {
            Debug.LogWarning($"[Grupo] «{R.personaje}» no es del grupo.");
            Finish(0);
            return;
        }
        if (Hacer(slot) || Runner == null)
        {
            Finish(0);
            return;
        }
        _rutina = Runner.StartCoroutine(Co_Reintentar(slot));
    }

    protected override void Stop()
    {
        if (_rutina != null && Runner != null) Runner.StopCoroutine(_rutina);
        _rutina = null;
    }

    bool Hacer(Slot slot) => R.accion switch
    {
        PedirGrupo.Accion.VuelveAlGrupo => SalidasDelGrupo.Devolver(slot),
        PedirGrupo.Accion.EntraEnElGrupo => SalidasDelGrupo.Unir(slot),
        PedirGrupo.Accion.PasaAlMando => ControlDelJugador.PasarElMandoA(slot) && ControlDelJugador.Activo == slot,
        _ => SalidasDelGrupo.Sacar(slot, R.retirarDeLaEscena),
    };

    IEnumerator Co_Reintentar(Slot slot)
    {
        float limite = Time.unscaledTime + SegundosDeReintento;
        while (!Hacer(slot) && Time.unscaledTime < limite) yield return null;
        if (ControlDelJugador.Activo != slot && R.accion == PedirGrupo.Accion.PasaAlMando)
            Debug.LogWarning($"[Grupo] No se ha podido pasar el mando a {R.personaje}.");
        if (R.accion == PedirGrupo.Accion.EntraEnElGrupo && !ControlDelJugador.EnElGrupo(slot))
            Debug.LogWarning($"[Grupo] {R.personaje} no ha podido entrar en el grupo: no está en la escena.");
        _rutina = null;
        Finish(0);
    }
}
