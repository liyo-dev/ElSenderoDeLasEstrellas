using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Saca un aviso de «objeto conseguido» cada vez que entra algo en el inventario del jugador (o
/// una prenda en el armario). Varias entradas seguidas del mismo objeto se juntan en un solo
/// aviso con la cantidad sumada.
///
/// Es un aviso del canal Objetos (<see cref="ArbitroDeAvisos"/>): mientras hay un menú, una
/// cinemática o la intro de un jefe, lo que llega espera y los avisos que ya estaban se esconden
/// con la cuenta atrás parada. Lo ganado en batalla no saca aviso (lo cuenta el informe de
/// victoria): mientras dura, la batalla tiene cerrado el canal y lo que llega se descarta.
///
/// La cola coloca ella misma los avisos en pila (uno debajo de otro, o encima con
/// <see cref="spawnAtTop"/>) y, si llegan varios a la vez, los saca de uno en uno con
/// <see cref="esperaEntreAvisos"/> de separación. Ver INC-642, INC-880.
/// </summary>
public class CollectiblePopupQueue : MonoBehaviour, IAvisoEnPantalla
{
    [Header("References")]
    [Tooltip("Prefab con un CollectiblePopupPanel.")]
    public GameObject popupPrefab;

    [Tooltip("Contenedor donde se crean los avisos. La cola los coloca en pila dentro de él; un LayoutGroup en el contenedor se apaga.")]
    public Transform popupParent;

    [Header("Settings")]
    [Tooltip("Segundos que se ve cada aviso (solo cuentan mientras está a la vista).")]
    public float displayDuration = 1.25f;

    [Tooltip("True: los avisos nuevos se colocan arriba de la pila y empujan a los demás hacia abajo. False: debajo del último.")]
    public bool spawnAtTop = false;

    [Header("Pila")]
    [Tooltip("Distancia vertical entre un aviso y el siguiente de la pila.")]
    public float separacionVertical = 110f;

    [Tooltip("Segundos entre la entrada de un aviso y la del siguiente cuando llegan varios a la vez.")]
    public float esperaEntreAvisos = 0.35f;

    [Header("Grouping")]
    [Tooltip("Ventana (segundos) en la que varias entradas seguidas del mismo objeto se juntan en un solo aviso.")]
    public float groupingWindow = 0.4f;

    [Header("Turno")]
    [Tooltip("Prioridad dentro del canal Objetos.")]
    [SerializeField] private int prioridad = 0;

    private struct Pendiente
    {
        public ItemData item;
        public int cantidad;
    }

    private Inventory _inventory;
    private bool _popupsEnabled;
    private Coroutine _enableRoutine;
    private bool _pendingEnable;
    private CanvasGroup _grupoDelContenedor;
    private bool _aLaVista;

    // Cantidades que se están juntando durante la ventana de agrupado, por objeto.
    private readonly Dictionary<string, int> _agrupando = new();
    // Avisos que esperan turno (menú, cinemática…), en orden de llegada.
    private readonly List<Pendiente> _esperando = new();
    // Avisos con turno que esperan su hueco para entrar, de uno en uno.
    private readonly List<Pendiente> _porSacar = new();
    // Avisos en pantalla, en orden de llegada.
    private readonly List<CollectiblePopupPanel> _vivos = new();
    private Coroutine _sacando;
    private float _proximaEntrada;

    private static CollectiblePopupQueue _activa;

    /// <summary>La cola que está activa en escena, o null. Una copia desactivada no puede lanzar avisos.</summary>
    public static CollectiblePopupQueue Activa => _activa != null && _activa.isActiveAndEnabled ? _activa : null;

    public CanalDeAviso Canal => CanalDeAviso.Objetos;
    public int Prioridad => prioridad;
    public bool Pasajero => true;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _activa = null; }
#endif

    void Awake()
    {
        PlayerPresetService.OnPresetApplying += HandlePresetApplying;
        PlayerPresetService.OnPresetApplied += HandlePresetApplied;

        Transform contenedor = popupParent != null ? popupParent : transform;
        _grupoDelContenedor = contenedor.GetComponent<CanvasGroup>();
        if (_grupoDelContenedor == null) _grupoDelContenedor = contenedor.gameObject.AddComponent<CanvasGroup>();
        _grupoDelContenedor.alpha = 0f;
        // La pila la coloca la cola: un LayoutGroup movería los avisos por debajo de ella.
        var layout = contenedor.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;

        // Habilitada por defecto (también en escenas que no aplican preset, como el CombatLab);
        // solo se apaga mientras se aplica un preset o se carga partida. Ver INC-642.
        _popupsEnabled = false;
        _pendingEnable = false;
        StartEnableRoutine();
        TryBindToPlayer();
        PlayerService.OnPlayerRegistered += OnPlayerRegistered;
    }

    void OnEnable()
    {
        _activa = this;
        if (_pendingEnable && _enableRoutine == null)
        {
            _pendingEnable = false;
            StartEnableRoutine();
        }
        if (_esperando.Count > 0 || _porSacar.Count > 0 || _vivos.Count > 0) ArbitroDeAvisos.Pedir(this);
    }

    void OnDisable()
    {
        if (_activa == this) _activa = null;
        if (_enableRoutine != null)
        {
            StopCoroutine(_enableRoutine);
            _enableRoutine = null;
        }
        // Las corrutinas se paran con el objeto: lo que se estaba juntando se pierde y lo que
        // esperaba su hueco vuelve a esperar turno.
        _agrupando.Clear();
        DevolverPorSacar();
        ArbitroDeAvisos.Terminar(this);
        _aLaVista = false;
    }

    void OnDestroy()
    {
        UnbindFromInventory();
        PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
        PlayerPresetService.OnPresetApplying -= HandlePresetApplying;
        PlayerPresetService.OnPresetApplied -= HandlePresetApplied;
        ArbitroDeAvisos.Terminar(this);
    }

    private void OnPlayerRegistered(GameObject player) => TryBindToPlayer();

    private void TryBindToPlayer()
    {
        UnbindFromInventory();
        if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
        {
            _inventory = inv;
            _inventory.OnItemAdded += OnItemAdded;
        }
    }

    private void UnbindFromInventory()
    {
        if (_inventory == null) return;
        _inventory.OnItemAdded -= OnItemAdded;
        _inventory = null;
    }

    private void OnItemAdded(ItemData item, int addedAmount, int newTotal)
    {
        if (!_popupsEnabled || !ArbitroDeAvisos.CanalAbierto(CanalDeAviso.Objetos) || !isActiveAndEnabled) return;
        if (item == null || addedAmount <= 0) return;

        var id = Clave(item);
        if (_agrupando.TryGetValue(id, out var cur))
        {
            _agrupando[id] = cur + addedAmount;
        }
        else
        {
            _agrupando[id] = addedAmount;
            StartCoroutine(FlushGroupedPopup(id, item, groupingWindow));
        }
    }

    void HandlePresetApplying()
    {
        _popupsEnabled = false;
        if (_enableRoutine != null)
        {
            StopCoroutine(_enableRoutine);
            _enableRoutine = null;
        }
        _agrupando.Clear();
        _esperando.Clear();
        PararSacado();
        _porSacar.Clear();
        DestroyActivePopups();
        ArbitroDeAvisos.Terminar(this);
        _aLaVista = false;
    }

    void HandlePresetApplied() => StartEnableRoutine();

    void StartEnableRoutine()
    {
        if (_enableRoutine != null)
            StopCoroutine(_enableRoutine);
        if (!isActiveAndEnabled)
        {
            _pendingEnable = true;
            return;
        }
        _enableRoutine = StartCoroutine(EnablePopupsDelayed());
    }

    IEnumerator EnablePopupsDelayed()
    {
        yield return null;
        yield return null;
        _popupsEnabled = true;
        _enableRoutine = null;
    }

    private IEnumerator FlushGroupedPopup(string itemId, ItemData item, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (!_agrupando.TryGetValue(itemId, out var amount)) yield break;
        _agrupando.Remove(itemId);

        if (amount > 0 && ArbitroDeAvisos.CanalAbierto(CanalDeAviso.Objetos))
            Encolar(item, amount);
    }

    public void TestSpawn(ItemData item, int amount) => Encolar(item, amount);

    /// <summary>
    /// Saca un aviso con los datos de una prenda del armario, sin necesitar un ItemData real.
    /// </summary>
    public void SpawnWardrobePopup(WardrobeItemSO wardrobeItem, int amount = 1)
    {
        if (wardrobeItem == null || !isActiveAndEnabled) return;

        // ItemData efímero para reutilizar el mismo aviso.
        var temp = ScriptableObject.CreateInstance<ItemData>();
        temp.itemId = $"WARDROBE:{wardrobeItem.WardrobeId}";
        temp.displayName = wardrobeItem.GetLocalizedDisplayName();
        temp.icon = wardrobeItem.Icon;

        Encolar(temp, amount);
    }

    // ── IAvisoEnPantalla ───────────────────────────────────────────────────────

    public void AlTenerTurno()
    {
        _aLaVista = true;
        _grupoDelContenedor.alpha = 1f;
        for (int i = 0; i < _vivos.Count; i++)
            if (_vivos[i] != null) _vivos[i].Congelar(false);

        for (int i = 0; i < _esperando.Count; i++)
            PonerPorSacar(_esperando[i].item, _esperando[i].cantidad);
        _esperando.Clear();
        if (_porSacar.Count > 0 && _sacando == null) _sacando = StartCoroutine(Co_Sacar());

        if (_vivos.Count == 0 && _porSacar.Count == 0) ArbitroDeAvisos.Terminar(this);
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        _grupoDelContenedor.alpha = 0f;
        for (int i = 0; i < _vivos.Count; i++)
            if (_vivos[i] != null) _vivos[i].Congelar(true);
        DevolverPorSacar();
    }

    public void AlDescartar()
    {
        _aLaVista = false;
        _esperando.Clear();
        PararSacado();
        _porSacar.Clear();
        DestroyActivePopups();
    }

    // ── Avisos ─────────────────────────────────────────────────────────────────

    private void Encolar(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (_aLaVista)
        {
            PonerPorSacar(item, amount);
            return;
        }

        var key = Clave(item);
        for (int i = 0; i < _esperando.Count; i++)
        {
            if (Clave(_esperando[i].item) != key) continue;
            var p = _esperando[i];
            p.cantidad += amount;
            _esperando[i] = p;
            ArbitroDeAvisos.Pedir(this);
            return;
        }
        _esperando.Add(new Pendiente { item = item, cantidad = amount });
        ArbitroDeAvisos.Pedir(this);
    }

    /// Con turno: se suma a su aviso si ya está en pantalla o en la fila; si no, entra en la fila
    /// para salir en su hueco.
    private void PonerPorSacar(ItemData item, int amount)
    {
        var key = Clave(item);
        for (int i = 0; i < _vivos.Count; i++)
        {
            var vivo = _vivos[i];
            if (vivo != null && vivo.ItemId == key)
            {
                vivo.AddAmount(amount);
                return;
            }
        }
        for (int i = 0; i < _porSacar.Count; i++)
        {
            if (Clave(_porSacar[i].item) != key) continue;
            var p = _porSacar[i];
            p.cantidad += amount;
            _porSacar[i] = p;
            return;
        }
        _porSacar.Add(new Pendiente { item = item, cantidad = amount });
        if (_sacando == null && isActiveAndEnabled) _sacando = StartCoroutine(Co_Sacar());
    }

    private IEnumerator Co_Sacar()
    {
        // Un frame de margen: así la corrutina nunca acaba dentro de su propio StartCoroutine y
        // _sacando no se queda apuntando a una que ya terminó.
        yield return null;
        while (_porSacar.Count > 0)
        {
            while (Time.unscaledTime < _proximaEntrada) yield return null;
            var p = _porSacar[0];
            _porSacar.RemoveAt(0);
            SpawnPopup(p.item, p.cantidad);
            _proximaEntrada = Time.unscaledTime + esperaEntreAvisos;
        }
        _sacando = null;
        if (_vivos.Count == 0 && _esperando.Count == 0) ArbitroDeAvisos.Terminar(this);
    }

    private void PararSacado()
    {
        if (_sacando != null) StopCoroutine(_sacando);
        _sacando = null;
    }

    /// Sin turno, lo que esperaba su hueco vuelve a esperar turno, delante de lo que llegue.
    private void DevolverPorSacar()
    {
        PararSacado();
        if (_porSacar.Count == 0) return;
        _esperando.InsertRange(0, _porSacar);
        _porSacar.Clear();
    }

    private void SpawnPopup(ItemData item, int amountToShow)
    {
        if (popupPrefab == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[CollectiblePopupQueue] popupPrefab es null: no se puede sacar el aviso.");
#endif
            return;
        }

        Transform parent = popupParent != null ? popupParent : transform;
        var go = Instantiate(popupPrefab, parent);
        var panel = go.GetComponent<CollectiblePopupPanel>();
        if (panel == null) panel = go.GetComponentInChildren<CollectiblePopupPanel>();
        if (panel == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[CollectiblePopupQueue] El prefab del aviso no tiene CollectiblePopupPanel.");
#endif
            Destroy(go);
            return;
        }

        if (spawnAtTop) go.transform.SetAsFirstSibling();
        else go.transform.SetAsLastSibling();

        if (go.transform is RectTransform rt)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
        if (!go.activeSelf) go.SetActive(true);

        panel.Acabado += AlAcabarUnAviso;
        _vivos.Add(panel);
        panel.Init(item, amountToShow, displayDuration, SitioEnLaPila(spawnAtTop ? 0 : _vivos.Count - 1));
        RecolocarPila();
    }

    /// Sitio del aviso que ocupa el puesto <paramref name="puesto"/> de la pila (0 = el primero).
    private Vector2 SitioEnLaPila(int puesto) => new Vector2(0f, -puesto * separacionVertical);

    private void RecolocarPila()
    {
        int puesto = 0;
        for (int i = 0; i < _vivos.Count; i++)
        {
            int indice = spawnAtTop ? _vivos.Count - 1 - i : i;
            var panel = _vivos[indice];
            if (panel == null) continue;
            panel.ColocarEn(SitioEnLaPila(puesto));
            puesto++;
        }
    }

    private void AlAcabarUnAviso(CollectiblePopupPanel panel)
    {
        _vivos.Remove(panel);
        for (int i = _vivos.Count - 1; i >= 0; i--)
            if (_vivos[i] == null) _vivos.RemoveAt(i);
        RecolocarPila();
        if (_vivos.Count == 0 && _esperando.Count == 0 && _porSacar.Count == 0) ArbitroDeAvisos.Terminar(this);
    }

    void DestroyActivePopups()
    {
        for (int i = 0; i < _vivos.Count; i++)
        {
            var panel = _vivos[i];
            if (panel == null) continue;
            panel.Acabado -= AlAcabarUnAviso;
            Destroy(panel.gameObject);
        }
        _vivos.Clear();
    }

    private static string Clave(ItemData item) => item != null ? (item.itemId ?? item.displayName ?? "__null__") : "__null__";
}
