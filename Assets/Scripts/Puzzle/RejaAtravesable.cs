using UnityEngine;

/// <summary>
/// Obstáculo que no deja pasar andando pero sí con un teletransporte corto (Paso Sombrío): una
/// reja, unos barrotes, una grieta estrecha. El teletransporte no lo toma por pared al buscar
/// dónde aparecer; el resto de la física lo trata como un muro normal.
/// Va en el objeto del collider o en uno de sus padres.
/// </summary>
[DisallowMultipleComponent]
public sealed class RejaAtravesable : MonoBehaviour
{
    /// <summary>true si el collider pertenece a una reja que el teletransporte puede cruzar.</summary>
    public static bool EsAtravesable(Collider col)
        => col != null && col.GetComponentInParent<RejaAtravesable>() != null;
}
