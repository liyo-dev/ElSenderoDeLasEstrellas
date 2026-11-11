using Game.NPC;
using UnityEngine;

/// <summary>
/// Algo que un personaje lleva tirando de ello (un carro): va detrás de él a una distancia fija,
/// con el frente hacia él, y apoyado en el suelo. Como un remolque: si el personaje se para o se da
/// la vuelta a mirar, la carga se queda donde está; solo se mueve cuando él tira.
/// El personaje se busca por su id de NPC, así que sirve también en cinemáticas (le mueve el guion)
/// y en los tramos jugables (le mueve su agente). Cuándo existe lo decide su tramo (ActivoEnTramo).
/// </summary>
[DisallowMultipleComponent]
public class CargaArrastrada : MonoBehaviour
{
    [Tooltip("Id del NPC que tira de la carga (su persistenceId).")]
    [SerializeField] private string actorId;
    [Tooltip("Metros entre el personaje y el centro de la carga.")]
    [SerializeField, Min(0.5f)] private float distancia = 2.6f;
    [Tooltip("Grados que hay que girar el modelo para que su frente quede hacia quien tira.")]
    [SerializeField] private float giroDelModelo;
    [Tooltip("Más lejos que esto (un teletransporte, una carga recién encendida) la carga se coloca detrás de él de golpe.")]
    [SerializeField, Min(1f)] private float distanciaParaColocar = 12f;

    private Transform _actor;
    private float _siguienteBusqueda;
    private int _capaSuelo;
    private const float AlturaDelRayo = 3f;
    private const float LargoDelRayo = 8f;

    public void Configurar(string actor, float metros, float giro)
    {
        actorId = actor;
        distancia = metros;
        giroDelModelo = giro;
    }

    private void Awake()
    {
        _capaSuelo = LayerMask.GetMask("Floor");
    }

    private void OnEnable() => _actor = null;

    private void LateUpdate()
    {
        if (_actor == null || !_actor.gameObject.activeInHierarchy)
        {
            if (Time.time < _siguienteBusqueda) return;
            _siguienteBusqueda = Time.time + 1f;
            _actor = Buscar();
            if (_actor == null) return;
        }

        Vector3 delActor = _actor.position;
        Vector3 aqui = transform.position;
        Vector3 haciaElActor = delActor - aqui;
        haciaElActor.y = 0f;
        float separacion = haciaElActor.magnitude;
        if (separacion < 0.01f) return;

        Vector3 frente = haciaElActor / separacion;
        if (separacion > distanciaParaColocar)
        {
            frente = _actor.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) return;
            frente.Normalize();
        }
        else if (separacion <= distancia) return;

        Vector3 sitio = delActor - frente * distancia;
        sitio.y = delActor.y;
        if (Physics.Raycast(sitio + Vector3.up * AlturaDelRayo, Vector3.down, out var suelo, LargoDelRayo, _capaSuelo, QueryTriggerInteraction.Ignore))
            sitio.y = suelo.point.y;
        transform.SetPositionAndRotation(sitio, Quaternion.LookRotation(frente) * Quaternion.Euler(0f, giroDelModelo, 0f));
    }

    private Transform Buscar()
    {
        if (string.IsNullOrEmpty(actorId) || !NPCRegistry.HasInstance) return null;
        return NPCRegistry.Instance.TryGetNPCByID(actorId, out var npc) && npc != null && npc.isActiveAndEnabled ? npc.transform : null;
    }
}
