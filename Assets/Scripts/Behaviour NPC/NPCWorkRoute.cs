using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oficio ambiental de un NPC: dos pilas de cajas y el recorrido de ida y vuelta entre ellas.
///
/// La usa <see cref="Game.NPC.States.WorkState"/> cuando el NPC lleva este <see cref="puestoId"/>
/// en su <see cref="Game.NPC.Modules.NPCAmbientConfig"/>. El NPC coge una caja de la pila de
/// recogida, la lleva a la de entrega y vuelve; cuando la pila de origen se vacía, el recorrido se
/// invierte. Las cajas se reparentan de la pila a la mano y de vuelta a la pila: no hay físicas,
/// interacción ni persistencia. Si el NPC se interrumpe, la caja vuelve a su pila.
///
/// Las pilas se construyen al arrancar a partir de <see cref="prefabCaja"/> (o de cubos
/// provisionales con <see cref="materialCaja"/>), así que la escena no necesita tener las cajas
/// colocadas a mano.
/// </summary>
[DisallowMultipleComponent]
public class NPCWorkRoute : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("ID que debe llevar el NPC en NPCAmbientConfig.puestoId para usar esta ruta.")]
    public string puestoId = "";

    [Header("Puntos de trabajo")]
    [Tooltip("Donde el NPC se para a recoger la caja. Su forward debe mirar hacia la pila.")]
    public Transform puntoRecogida;

    [Tooltip("Donde el NPC se para a dejar la caja. Su forward debe mirar hacia la pila.")]
    public Transform puntoEntrega;

    [Tooltip("Desplazamiento de la pila respecto al punto, en espacio local del punto. La pila queda delante del NPC.")]
    public Vector3 offsetPila = new Vector3(0f, 0f, 0.45f);

    [Header("Cajas")]
    [Tooltip("Prefab de la caja. Si es null se crea un cubo provisional con materialCaja.")]
    public GameObject prefabCaja;

    [Tooltip("Material del cubo provisional.")]
    public Material materialCaja;

    [Min(1)]
    [Tooltip("Cajas que empiezan en cada pila.")]
    public int cajasPorPila = 3;

    [Tooltip("Tamaño del cubo provisional. No afecta a un prefabCaja asignado.")]
    public Vector3 tamanoCaja = new Vector3(0.3f, 0.26f, 0.3f);

    [Tooltip("Separación entre cajas dentro de una pila (columnas en X, filas en Y).")]
    public Vector2 separacionCajas = new Vector2(0.34f, 0.27f);

    [Header("Animación y ritmo")]
    [Tooltip("Estado de Animator al recoger. Se reproduce una vez.")]
    public string animacionRecoger = "CarryStart_NoWeapon";

    [Tooltip("Estado de Animator al dejar. Se reproduce una vez.")]
    public string animacionDejar = "CarryThrow_NoWeapon";

    [Min(0f)]
    [Tooltip("Segundos que el NPC se queda en la animación de recoger antes de coger la caja.")]
    public float tiempoRecoger = 1.1f;

    [Min(0f)]
    [Tooltip("Segundos que el NPC se queda en la animación de dejar antes de soltar la caja.")]
    public float tiempoDejar = 0.9f;

    [Tooltip("Pose de UpperBody que mantiene la caja con las dos manos mientras camina. Vacío = no se cambia la pose.")]
    public string poseDeCarga = "CarryMoveIdle_NoWeapon";

    [Min(0f)]
    [Tooltip("Pausa después de dejar la caja antes de volver a por otra.")]
    public float pausaAntesDeVolver = 0.6f;

    [Header("Narrativa")]
    [Tooltip("Si tiene texto, la ruta solo está disponible cuando esa señal se ha levantado alguna vez.")]
    public string soloSiSenal;

    [Tooltip("Si tiene texto, la ruta deja de estar disponible cuando esa señal se ha levantado.")]
    public string dejaDeSiSenal;

    [Tooltip("Si tiene texto, la ruta deja de estar disponible cuando esa quest está activa o completada.")]
    public string dejaDeSiQuest;

    [Header("Caja en la mano")]
    [Tooltip("Posición local de la caja respecto al hueso de la mano derecha.")]
    public Vector3 offsetEnMano = Vector3.zero;

    [Tooltip("Rotación local de la caja respecto al hueso de la mano derecha.")]
    public Vector3 giroEnMano = Vector3.zero;

    // ── Estado de runtime ───────────────────────────────────────────────────────
    private List<Transform> _cajasOrigen = new();
    private List<Transform> _cajasDestino = new();

    private Transform _puntoOrigen;
    private Transform _puntoDestino;
    private Transform _pilaOrigen;
    private Transform _pilaDestino;
    private Transform _ocupante;
    private Transform _cajaEnMano;
    private bool _construida;

    public bool IsOccupied => _ocupante != null;
    public bool QuedanCajas => _cajasOrigen.Count > 0;
    public Transform PuntoRecogida => _puntoOrigen != null ? _puntoOrigen : puntoRecogida;
    public Transform PuntoEntrega => _puntoDestino != null ? _puntoDestino : puntoEntrega;

    // ── Registro estático ───────────────────────────────────────────────────────
    private static readonly List<NPCWorkRoute> _rutas = new();
    public static IReadOnlyList<NPCWorkRoute> All => _rutas;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _rutas.Clear();
#endif

    private void OnEnable()
    {
        if (!_rutas.Contains(this)) _rutas.Add(this);
        ConstruirSiHaceFalta();
    }

    private void Start()
    {
        ConstruirSiHaceFalta();
    }

    private void OnDisable()
    {
        _rutas.Remove(this);
        if (_ocupante != null) Soltar(_ocupante);
    }

    private void OnValidate()
    {
        cajasPorPila = Mathf.Max(1, cajasPorPila);
        separacionCajas.x = Mathf.Max(0.01f, separacionCajas.x);
        separacionCajas.y = Mathf.Max(0.01f, separacionCajas.y);
        tamanoCaja = new Vector3(
            Mathf.Max(0.01f, tamanoCaja.x),
            Mathf.Max(0.01f, tamanoCaja.y),
            Mathf.Max(0.01f, tamanoCaja.z));
    }

    // ── Búsqueda y ocupación ────────────────────────────────────────────────────

    /// <summary>
    /// Busca una ruta libre con este <paramref name="id"/>. Devuelve false si no existe, está
    /// ocupada por otro NPC o la narrativa la tiene desactivada ahora mismo.
    /// </summary>
    public static bool TryFindFor(string id, out NPCWorkRoute ruta)
    {
        ruta = null;
        if (string.IsNullOrEmpty(id)) return false;

        for (int i = 0; i < _rutas.Count; i++)
        {
            var candidata = _rutas[i];
            if (candidata == null || candidata.IsOccupied) continue;
            if (!string.Equals(candidata.puestoId, id, System.StringComparison.Ordinal)) continue;
            if (!candidata.EstaDisponible()) continue;
            ruta = candidata;
            return true;
        }

        return false;
    }

    public bool EstaDisponible()
    {
        if (puntoRecogida == null || puntoEntrega == null) return false;

        var senales = DefaultNarrativeSignals.Instance;
        if (senales == null) return true;

        if (!string.IsNullOrEmpty(soloSiSenal) && !senales.HasEverRaised(soloSiSenal)) return false;
        if (!string.IsNullOrEmpty(dejaDeSiSenal) && senales.HasEverRaised(dejaDeSiSenal)) return false;

        if (!string.IsNullOrEmpty(dejaDeSiQuest))
        {
            var estado = senales.GetQuestState(dejaDeSiQuest);
            if (estado == NarrativeQuestState.Active || estado == NarrativeQuestState.Completed)
                return false;
        }

        return true;
    }

    public bool TryOcupar(Transform npc)
    {
        if (npc == null) return false;
        if (_ocupante == npc) return true;
        if (_ocupante != null) return false;
        _ocupante = npc;
        return true;
    }

    public void Soltar(Transform npc)
    {
        if (_ocupante != npc) return;
        DevolverCajaALaPila();
        _ocupante = null;
    }

    // ── Cajas ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Coge la última caja de la pila de recogida y la cuelga de la mano. Devuelve null si no
    /// quedan cajas o no hay hueso de mano.
    /// </summary>
    public Transform CogerCaja(Transform mano)
    {
        if (_cajaEnMano != null) return _cajaEnMano;
        if (mano == null) return null;

        if (_cajasOrigen.Count == 0)
        {
            IntercambiarPilas();
            if (_cajasOrigen.Count == 0) return null;
        }

        int ultimo = _cajasOrigen.Count - 1;
        var caja = _cajasOrigen[ultimo];
        _cajasOrigen.RemoveAt(ultimo);
        if (caja == null) return null;

        NPCWorldPoint.ColocarPropEnMano(caja, mano, offsetEnMano, giroEnMano);
        _cajaEnMano = caja;
        return caja;
    }

    /// <summary>Deja la caja que lleva en la mano en la pila de entrega.</summary>
    public void DejarCajaEnPila()
    {
        if (_cajaEnMano == null) return;

        var caja = _cajaEnMano;
        _cajaEnMano = null;
        _cajasDestino.Add(caja);

        if (_pilaDestino == null) return;
        caja.SetParent(_pilaDestino, true);
        caja.localPosition = HuecoLocal(_cajasDestino.Count - 1);
        caja.localRotation = Quaternion.identity;
    }

    /// <summary>Devuelve la caja que lleva en la mano a la pila de recogida (interrupción).</summary>
    public void DevolverCajaALaPila()
    {
        if (_cajaEnMano == null) return;

        var caja = _cajaEnMano;
        _cajaEnMano = null;
        if (_pilaOrigen == null) return;

        _cajasOrigen.Add(caja);
        caja.SetParent(_pilaOrigen, true);
        caja.localPosition = HuecoLocal(_cajasOrigen.Count - 1);
        caja.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Cierra un viaje: si la pila de origen se ha vaciado, invierte el recorrido para que las
    /// cajas vuelvan al otro lado.
    /// </summary>
    public void SiguienteViaje()
    {
        if (_cajasOrigen.Count == 0 && _cajasDestino.Count > 0)
            IntercambiarPilas();
    }

    private void IntercambiarPilas()
    {
        (_puntoOrigen, _puntoDestino) = (_puntoDestino, _puntoOrigen);
        (_pilaOrigen, _pilaDestino) = (_pilaDestino, _pilaOrigen);
        (_cajasOrigen, _cajasDestino) = (_cajasDestino, _cajasOrigen);
    }

    // ── Construcción de las pilas ───────────────────────────────────────────────

    private void ConstruirSiHaceFalta()
    {
        if (!Application.isPlaying || _construida) return;
        if (puntoRecogida == null || puntoEntrega == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[NPCWorkRoute:{name}] Faltan puntoRecogida o puntoEntrega; la ruta no se construye.", this);
#endif
            return;
        }

        _puntoOrigen = puntoRecogida;
        _puntoDestino = puntoEntrega;
        _pilaOrigen = CrearPila(puntoRecogida, "Pila de recogida");
        _pilaDestino = CrearPila(puntoEntrega, "Pila de entrega");
        LlenarPila(_pilaOrigen, _cajasOrigen);
        LlenarPila(_pilaDestino, _cajasDestino);
        _construida = true;
    }

    private Transform CrearPila(Transform punto, string nombre)
    {
        var pila = new GameObject(nombre).transform;
        pila.SetParent(transform, false);
        pila.position = punto.position + punto.TransformVector(offsetPila);
        pila.rotation = punto.rotation;
        return pila;
    }

    private void LlenarPila(Transform pila, List<Transform> cajas)
    {
        for (int i = 0; i < cajasPorPila; i++)
        {
            var caja = CrearCaja(pila, i);
            if (caja != null) cajas.Add(caja);
        }
    }

    private Transform CrearCaja(Transform pila, int indice)
    {
        GameObject go;
        if (prefabCaja != null)
        {
            go = Instantiate(prefabCaja, pila);
            go.name = $"Caja {indice + 1}";
            go.transform.localPosition = HuecoLocal(indice);
            go.transform.localRotation = Quaternion.identity;
            QuitarFisica(go);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Caja {indice + 1}";
            go.transform.SetParent(pila, false);
            go.transform.localPosition = HuecoLocal(indice);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = tamanoCaja;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (materialCaja != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = materialCaja;
            }
        }

        return go.transform;
    }

    private static void QuitarFisica(GameObject go)
    {
        // El prop visual del pack viene marcado como estático; al instanciarlo para moverlo hay
        // que quitarle la marca o el renderizado por lotes puede dejarlo clavado en la pila.
        go.isStatic = false;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
    }

    private Vector3 HuecoLocal(int indice)
    {
        const int columnas = 2;
        int fila = indice / columnas;
        int columna = indice % columnas;
        return new Vector3(columna * separacionCajas.x, fila * separacionCajas.y, 0f);
    }

    // ── Gizmos ──────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (puntoRecogida == null || puntoEntrega == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(puntoRecogida.position, 0.25f);
        Gizmos.DrawWireSphere(puntoEntrega.position, 0.25f);
        Gizmos.DrawLine(puntoRecogida.position, puntoEntrega.position);

        Gizmos.color = new Color(0.85f, 0.65f, 0.2f, 0.9f);
        DibujarPila(puntoRecogida);
        DibujarPila(puntoEntrega);

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(puntoRecogida.position + Vector3.up * 0.6f, $"{puestoId}\nRecogida");
        UnityEditor.Handles.Label(puntoEntrega.position + Vector3.up * 0.6f, $"{puestoId}\nEntrega");
    }

    private void DibujarPila(Transform punto)
    {
        Vector3 centro = punto.position + punto.TransformVector(offsetPila);
        Quaternion rot = punto.rotation;
        Vector3 tamano = TamanoDePreview();
        for (int i = 0; i < cajasPorPila; i++)
            Gizmos.DrawWireCube(centro + rot * HuecoLocal(i), tamano);
    }

    private Vector3 TamanoDePreview()
    {
        if (prefabCaja == null) return tamanoCaja;
        if (prefabCaja.TryGetComponent<BoxCollider>(out var caja))
            return Vector3.Scale(caja.size, prefabCaja.transform.lossyScale);
        return tamanoCaja;
    }
#endif
}
