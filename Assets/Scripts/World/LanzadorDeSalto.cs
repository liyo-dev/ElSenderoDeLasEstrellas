using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Runa en el suelo que lanza al jugador hacia arriba con una voltereta y le devuelve el doble
/// salto, para alcanzar sitios altos. Genérica: se coloca donde haga falta. Necesita un collider
/// en modo trigger. Ver INC-655.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LanzadorDeSalto : MonoBehaviour
{
    [Header("Impulso")]
    [Tooltip("Velocidad vertical (m/s) con la que sale el jugador. 22 sube unos 8 m.")]
    [SerializeField, Min(1f)] private float velocidadVertical = 22f;
    [Tooltip("Velocidad (m/s) hacia donde mira el lanzador (su eje Z azul), para llevar al jugador hasta lo alto. 0 = solo hacia arriba.")]
    [SerializeField, Min(0f)] private float empujeHaciaDelante = 4f;
    [Tooltip("Al salir del lanzador el doble salto vuelve a estar disponible.")]
    [SerializeField] private bool devolverDobleSalto = true;
    [Tooltip("Segundos tras el despegue en los que empieza la voltereta.")]
    [SerializeField, Min(0f)] private float retrasoDeLaVoltereta = 0.12f;
    [Tooltip("Segundos en los que el lanzador no vuelve a dispararse.")]
    [SerializeField, Min(0.1f)] private float enfriamiento = 0.8f;

    [Header("Efectos")]
    [Tooltip("Efecto de un solo uso al lanzar (pool de VFX). Vacío = nada.")]
    [SerializeField] private GameObject vfxDeImpulso;
    [SerializeField, Min(0.1f)] private float duracionDelVfx = 2f;
    [Tooltip("Clave de SFX al lanzar. Vacío = sin sonido.")]
    [SerializeField] private string sfxDeImpulso = "";

    private float _listoEn;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < _listoEn) return;

        var controller = other.GetComponentInParent<vThirdPersonController>();
        if (controller == null || !controller.enabled) return;
        var jugador = PlayerService.Player;
        if (jugador == null || (controller.gameObject != jugador && !controller.transform.IsChildOf(jugador.transform))) return;

        _listoEn = Time.time + enfriamiento;
        Vector3 frente = transform.forward; frente.y = 0f;
        Vector3 empuje = frente.sqrMagnitude > 0.0001f ? frente.normalized * empujeHaciaDelante : Vector3.zero;
        controller.Impulsar(velocidadVertical, empuje, devolverDobleSalto);

        if (vfxDeImpulso != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxDeImpulso, transform.position, transform.rotation, duracionDelVfx);
        if (!string.IsNullOrEmpty(sfxDeImpulso) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxDeImpulso, 1f, transform.position);

        var voltereta = controller.GetComponent<VolteretaDelJugador>();
        if (voltereta != null) StartCoroutine(Co_Voltereta(voltereta));
    }

    private IEnumerator Co_Voltereta(VolteretaDelJugador voltereta)
    {
        if (retrasoDeLaVoltereta > 0f) yield return new WaitForSeconds(retrasoDeLaVoltereta);
        if (voltereta != null) voltereta.EnElAire();
    }
}
