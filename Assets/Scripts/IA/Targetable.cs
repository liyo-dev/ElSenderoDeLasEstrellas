using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Targetable : MonoBehaviour
{
    [Tooltip("Radio en el que este objetivo puede ser auto-apuntado (si 0 usa el radio global del PlayerTargeting).")]
    public float targetingRadius = 8f;
    
    [Tooltip("Si está activo, el marker de target se mostrará. Desactivar durante alertas pre-combate.")]
    public bool isInActiveCombat = false;

    [Tooltip("Si está activo, el marcador también aparece fuera de combate y sin vida: piezas de puzle que se apuntan con magia.")]
    public bool esMarcableSiempre = false;

    // Registro de todos los Targetable activos. PlayerTargeting lo usa para encontrar las
    // piezas marcables (cristales, molinetes, braseros) sin depender de su capa ni de que
    // tengan vida o estén en combate.
    static readonly List<Targetable> _activos = new();

    public static int ActivosCount => _activos.Count;

    public static Targetable ActivoEn(int indice) =>
        indice >= 0 && indice < _activos.Count ? _activos[indice] : null;

    Collider _colliderDeApuntado;

    /// Collider con el que se calcula el centro del marcador. Se cachea en la primera consulta.
    public Collider ColliderDeApuntado
    {
        get
        {
            if (_colliderDeApuntado == null)
            {
                _colliderDeApuntado = GetComponent<Collider>();
                if (_colliderDeApuntado == null) _colliderDeApuntado = GetComponentInChildren<Collider>();
            }
            return _colliderDeApuntado;
        }
    }

    void OnEnable()
    {
        if (!_activos.Contains(this)) _activos.Add(this);
    }

    void OnDisable() => _activos.Remove(this);

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _activos.Clear();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Vector3 center = transform.position;
        var col = ColliderDeApuntado;
        if (col) center = col.bounds.center;
        Gizmos.DrawWireSphere(center, Mathf.Max(0.01f, targetingRadius));
    }
#endif
}

