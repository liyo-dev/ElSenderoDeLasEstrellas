using UnityEngine;

/// <summary>
/// Marca a un personaje como niño. Dos formas:
/// - Con cuerpo de niño propio (<see cref="cuerpoPropio"/>, lo pone el menú de los niños hechos en Blender con
///   <c>CuerpoDeNino</c>): el esqueleto ya es de niño, así que no se encoge; <see cref="altura"/> dice cuánto
///   mide respecto a un adulto, para lo que se coloca a la altura de su cabeza (el bocadillo).
/// - Sin él: se le dibuja más pequeño, con la misma escala para todos (<see cref="Escala"/>). Va en la raíz;
///   la navegación, los colisionadores y los iconos sobre la cabeza (que buscan el hueso de la cabeza) se
///   ajustan solos.
/// El bocadillo baja con <see cref="FactorDe"/> en los dos casos.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TallaDeNino : MonoBehaviour
{
    /// <summary>Escala de los niños sin cuerpo propio respecto a un adulto.</summary>
    public const float Escala = 0.72f;

    [Tooltip("Lleva un cuerpo de niño propio (esqueleto de niño): no se encoge.")]
    [SerializeField] private bool cuerpoPropio;
    [Tooltip("Con cuerpo propio: lo que mide respecto a un adulto (alto de la cabeza).")]
    [SerializeField, Range(0.4f, 1f)] private float altura = Escala;

    /// <summary>Lo que mide respecto a un adulto.</summary>
    public float Factor => cuerpoPropio ? altura : Escala;

    private void OnEnable()
    {
        if (cuerpoPropio) return;
        var talla = Vector3.one * Escala;
        if (transform.localScale != talla) transform.localScale = talla;
    }

    /// <summary>
    /// Cuánto hay que encoger lo que se coloca a la altura de la cabeza de <paramref name="t"/> (o de
    /// quien lo contiene): su <see cref="Factor"/> si es un niño, 1 si no.
    /// </summary>
    public static float FactorDe(Transform t)
    {
        if (t == null) return 1f;
        var talla = t.GetComponentInParent<TallaDeNino>();
        return talla != null && talla.isActiveAndEnabled ? talla.Factor : 1f;
    }
}
