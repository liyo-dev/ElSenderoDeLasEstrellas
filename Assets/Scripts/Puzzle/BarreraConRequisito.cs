using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Zona (trigger) que comprueba un <see cref="RequisitoDeAcceso"/> cuando entra el jugador.
/// Si no lo cumple, le dice qué le falta («Necesitas volar para llegar aquí») y, si se le
/// asigna, mantiene un bloqueo (un objeto que se apaga al cumplirse). Si lo cumple, retira el
/// bloqueo y lanza el evento.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class BarreraConRequisito : MonoBehaviour
{
    [SerializeField] private RequisitoDeAcceso requisito = new RequisitoDeAcceso();
    [Tooltip("Objeto que impide pasar mientras no se cumpla (opcional): se apaga al cumplirse.")]
    [SerializeField] private GameObject bloqueo;
    [SerializeField, Min(0.5f)] private float duracionDelAviso = 3f;

    [Tooltip("Se lanza al entrar cumpliendo el requisito.")]
    public UnityEvent alCumplirse;

    private readonly HashSet<Collider> _dentro = new HashSet<Collider>();

    public RequisitoDeAcceso Requisito => requisito;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void Start() => ActualizarBloqueo();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(GameTags.Player)) return;
        bool primero = _dentro.Count == 0;
        _dentro.Add(other);
        if (!primero) return;

        if (requisito.Cumple())
        {
            ActualizarBloqueo();
            alCumplirse.Invoke();
            return;
        }

        ActualizarBloqueo();
        requisito.AvisarDeLoQueFalta(duracionDelAviso);
    }

    private void OnTriggerExit(Collider other) => _dentro.Remove(other);

    private void OnDisable() => _dentro.Clear();

    /// <summary>Activa o quita el bloqueo físico según el requisito (llamar si cambia algo fuera).</summary>
    public void ActualizarBloqueo()
    {
        if (bloqueo != null) bloqueo.SetActive(!requisito.Cumple());
    }
}
