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
            if (_colliderDeApuntado == null) _colliderDeApuntado = ColliderDelCuerpo(transform);
            return _colliderDeApuntado;
        }
    }

    static readonly List<Collider> _collidersDelCuerpo = new();

    /// <summary>
    /// Raíz de lo que se apunta: el Targetable o el Damageable del que cuelga 't'. La búsqueda
    /// de objetivos encuentra colliders sueltos (un arma, un trigger de detección, un hueso);
    /// apuntar a la raíz evita tomar uno de ellos por el objetivo.
    /// </summary>
    public static Transform RaizDelObjetivo(Transform t)
    {
        if (t == null) return null;
        var targetable = t.GetComponentInParent<Targetable>();
        if (targetable != null) return targetable.transform;
        var damageable = t.GetComponentInParent<Damageable>();
        return damageable != null ? damageable.transform : t;
    }

    /// <summary>
    /// Collider que representa el cuerpo de 'raiz': el suyo propio si es sólido; si no, el sólido
    /// más grande de sus hijos (el cuerpo, no el arma); sin sólidos, el primer collider que tenga.
    /// Nunca sube a los padres: un trigger de zona o de combate no es el cuerpo del objetivo.
    /// </summary>
    public static Collider ColliderDelCuerpo(Transform raiz)
    {
        if (raiz == null) return null;
        if (raiz.TryGetComponent<Collider>(out var propio) && propio.enabled && !propio.isTrigger) return propio;

        raiz.GetComponentsInChildren(false, _collidersDelCuerpo);
        Collider mejor = null, primero = null;
        float mejorVolumen = -1f;
        for (int i = 0; i < _collidersDelCuerpo.Count; i++)
        {
            var c = _collidersDelCuerpo[i];
            if (!c.enabled) continue;
            if (primero == null) primero = c;
            if (c.isTrigger) continue;
            Vector3 tam = c.bounds.size;
            float volumen = tam.x * tam.y * tam.z;
            if (volumen > mejorVolumen) { mejorVolumen = volumen; mejor = c; }
        }
        _collidersDelCuerpo.Clear();
        return mejor != null ? mejor : primero;
    }

    /// <summary>
    /// Centro del cuerpo de lo que se apunta (Targetable, Damageable o el propio transform):
    /// adonde van los hechizos y donde se coloca el marcador. Sin collider, un metro sobre los pies.
    /// </summary>
    public static Vector3 CentroDelCuerpo(Transform objetivo)
    {
        Transform raiz = RaizDelObjetivo(objetivo);
        if (raiz == null) return Vector3.zero;
        var targetable = raiz.GetComponent<Targetable>();
        Collider col = targetable != null ? targetable.ColliderDeApuntado : ColliderDelCuerpo(raiz);
        return col != null ? col.bounds.center : raiz.position + Vector3.up;
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

