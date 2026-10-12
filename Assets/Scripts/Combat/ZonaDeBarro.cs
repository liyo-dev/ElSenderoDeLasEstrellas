using System.Collections.Generic;
using UnityEngine;

/// Barro en el suelo de una arena (el del arroyo, en la pelea del Gólem): un círculo en el plano
/// del suelo. No usa física: quien deba hundirse pregunta con Contiene si está dentro (el Gólem
/// en su vuelta de IA). El barro no hace nada por sí solo; el aspecto (charco, rótulo) lo pone
/// quien lo coloca. Ver INC-723.
public sealed class ZonaDeBarro : MonoBehaviour
{
    [Tooltip("Radio del charco en metros, en el plano del suelo, desde este objeto.")]
    [SerializeField, Min(0.5f)] private float radio = 3.5f;
    [Tooltip("Diferencia de altura máxima para contar como dentro (no hunde a quien pasa por encima volando).")]
    [SerializeField, Min(0.1f)] private float alturaMaxima = 1.5f;
    [Tooltip("Salpicadura al hundirse algo. Vacío = sin efecto.")]
    [SerializeField] private GameObject vfxChapoteo;

    private static readonly List<ZonaDeBarro> s_activas = new List<ZonaDeBarro>();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_activas.Clear();
#endif

    public float Radio => radio;

    void OnEnable() { if (!s_activas.Contains(this)) s_activas.Add(this); }
    void OnDisable() => s_activas.Remove(this);

    /// true si la posición está dentro de algún barro activo.
    public static bool Contiene(Vector3 posicion, out ZonaDeBarro zona)
    {
        for (int i = 0; i < s_activas.Count; i++)
        {
            var z = s_activas[i];
            if (z == null) continue;
            Vector3 d = posicion - z.transform.position;
            if (Mathf.Abs(d.y) > z.alturaMaxima) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= z.radio * z.radio) { zona = z; return true; }
        }
        zona = null;
        return false;
    }

    /// Salpicadura en el punto donde algo se hunde.
    public void Chapotear(Vector3 punto)
    {
        if (vfxChapoteo != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxChapoteo, punto, Quaternion.identity, 2f);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.45f, 0.3f, 0.15f, 0.8f);
        const int pasos = 32;
        Vector3 c = transform.position;
        for (int i = 0; i < pasos; i++)
        {
            float a0 = i * Mathf.PI * 2f / pasos, a1 = (i + 1) * Mathf.PI * 2f / pasos;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radio,
                            c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radio);
        }
    }
#endif
}
