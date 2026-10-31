using UnityEngine;

/// <summary>
/// Marca a un personaje como niño: se le dibuja más pequeño, con la misma escala para todos los
/// niños del juego (<see cref="Escala"/>). No hay modelos de niño; los muñecos son los mismos y lo
/// que los distingue es la talla. Va en la raíz del personaje; la navegación, los colisionadores y
/// los iconos sobre la cabeza (que buscan el hueso de la cabeza) se ajustan solos, y el bocadillo
/// baja con <see cref="FactorDe"/>. Para cambiar la talla de todos, se cambia <see cref="Escala"/>.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TallaDeNino : MonoBehaviour
{
    /// <summary>Escala de todos los niños respecto a un adulto.</summary>
    public const float Escala = 0.72f;

    private void OnEnable()
    {
        var talla = Vector3.one * Escala;
        if (transform.localScale != talla) transform.localScale = talla;
    }

    /// <summary>
    /// Cuánto hay que encoger lo que se coloca a la altura de la cabeza de <paramref name="t"/> (o de
    /// quien lo contiene): <see cref="Escala"/> si es un niño, 1 si no.
    /// </summary>
    public static float FactorDe(Transform t)
    {
        if (t == null) return 1f;
        var talla = t.GetComponentInParent<TallaDeNino>();
        return talla != null && talla.isActiveAndEnabled ? Escala : 1f;
    }
}
