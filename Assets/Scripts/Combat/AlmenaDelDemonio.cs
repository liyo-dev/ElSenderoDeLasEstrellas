using System.Collections.Generic;
using UnityEngine;

/// Punto alto de una arena (una almena del castillo, lo alto de una torre) donde el Demonio 2 se
/// posa en la fase de la guerra en el aire. Basta con colocarlo donde debe quedarse flotando; si
/// no hay ninguno cerca, el Demonio vuela en círculo sobre la arena. Ver INC-723.
public sealed class AlmenaDelDemonio : MonoBehaviour
{
    private static readonly List<AlmenaDelDemonio> s_activas = new List<AlmenaDelDemonio>();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_activas.Clear();
#endif

    public static IReadOnlyList<AlmenaDelDemonio> Activas => s_activas;

    void OnEnable() { if (!s_activas.Contains(this)) s_activas.Add(this); }
    void OnDisable() => s_activas.Remove(this);

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.2f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 2f);
    }
#endif
}
