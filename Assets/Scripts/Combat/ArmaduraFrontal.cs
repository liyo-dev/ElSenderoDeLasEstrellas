using UnityEngine;

/// Un enemigo acorazado por delante: los golpes que le llegan de frente o de lado apenas le
/// hacen daño (rebotan con chispas); los que le llegan por la espalda, el daño completo. Se mira
/// desde dónde está quien golpea (el instigador), no por dónde entra el proyectil: rodearle es la
/// forma de hacerle daño. Un golpe sin instigador (un ataque de zona) hace el daño completo.
/// Va junto al Damageable. Ver INC-489.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class ArmaduraFrontal : MonoBehaviour, IFiltroDeDano
{
    [Tooltip("Parte del daño que pasa por delante. 0 = nada.")]
    [SerializeField, Range(0f, 1f)] private float factorDeFrente = 0.2f;
    [Tooltip("A partir de qué ángulo (respecto a hacia dónde mira) un golpe cuenta como por la " +
             "espalda. 110 = un cono trasero de ±70°.")]
    [SerializeField, Range(90f, 180f)] private float anguloEspalda = 110f;
    [Tooltip("Grados que hay que girar el 'delante' del objeto para que coincida con la cara del " +
             "modelo, si el modelo no mira hacia su +Z.")]
    [SerializeField] private float desfaseFrente = 0f;
    [Tooltip("Efecto opcional cuando un golpe rebota en la coraza.")]
    [SerializeField] private GameObject vfxRebote;
    [SerializeField] private float alturaVfx = 2f;

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (instigador == null) return cantidad;

        Vector3 haciaAtacante = instigador.transform.position - transform.position;
        haciaAtacante.y = 0f;
        if (haciaAtacante.sqrMagnitude < 0.01f) return cantidad;

        Vector3 frente = Quaternion.Euler(0f, desfaseFrente, 0f) * transform.forward;
        frente.y = 0f;
        if (Vector3.Angle(frente, haciaAtacante) >= anguloEspalda) return cantidad;

        if (vfxRebote != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxRebote,
                transform.position + Vector3.up * alturaVfx + haciaAtacante.normalized, Quaternion.identity, 1f);
        AvisosDeCombate.GolpeMalDado(gameObject);
        return cantidad * factorDeFrente;
    }
}
