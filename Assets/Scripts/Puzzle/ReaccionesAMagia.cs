using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Impacto de magia sobre una pieza del mundo (no sobre un combatiente): qué elemento, dónde,
/// quién la lanzó y si era una bola hostil devuelta con la B (INC-689).
/// </summary>
public readonly struct ImpactoDeMagia
{
    public readonly MagicElement Elemento;
    public readonly Vector3 Punto;
    public readonly GameObject Lanzador;
    public readonly bool Devuelto;

    public ImpactoDeMagia(MagicElement elemento, Vector3 punto, GameObject lanzador, bool devuelto)
    {
        Elemento = elemento;
        Punto = punto;
        Lanzador = lanzador;
        Devuelto = devuelto;
    }
}

/// <summary>
/// Cualquier pieza del mundo que reacciona cuando la alcanza la magia (lo que arde, un cristal,
/// un molinete…). Los proyectiles, las zonas y las bolas devueltas avisan a través de
/// <see cref="ReaccionesAMagia"/>, sin conocer los tipos concretos.
/// </summary>
public interface IReaccionaAMagia
{
    void RecibirMagia(in ImpactoDeMagia impacto);
}

/// <summary>Punto único por el que la magia avisa a las piezas del mundo.</summary>
public static class ReaccionesAMagia
{
    /// <summary>true si el collider (o algún padre) tiene algo que reaccione a la magia.</summary>
    public static bool Reacciona(Collider col)
        => col != null && col.GetComponentInParent<IReaccionaAMagia>() != null;

    /// <summary>
    /// Avisa a todo lo que reacciona a la magia en el collider y sus padres.
    /// Devuelve true si había alguna pieza que avisar.
    /// </summary>
    public static bool Notificar(Collider col, in ImpactoDeMagia impacto)
    {
        if (col == null) return false;
        var piezas = ListPool<IReaccionaAMagia>.Get();
        try
        {
            col.GetComponentsInParent(false, piezas);
            for (int i = 0; i < piezas.Count; i++)
            {
                if (piezas[i] is Behaviour b && !b.isActiveAndEnabled) continue;
                piezas[i].RecibirMagia(in impacto);
            }
            return piezas.Count > 0;
        }
        finally
        {
            ListPool<IReaccionaAMagia>.Release(piezas);
        }
    }

    /// <summary>
    /// Igual que <see cref="Notificar"/>, pero solo la primera vez para cada pieza raíz que se
    /// registre en <paramref name="yaAvisadas"/> (zonas que pulsan varias veces).
    /// </summary>
    public static bool NotificarUnaVez(Collider col, in ImpactoDeMagia impacto, HashSet<Component> yaAvisadas)
    {
        if (col == null || yaAvisadas == null) return false;
        var primera = col.GetComponentInParent<IReaccionaAMagia>() as Component;
        if (primera == null || !yaAvisadas.Add(primera)) return false;
        return Notificar(col, in impacto);
    }
}
