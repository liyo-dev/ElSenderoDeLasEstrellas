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
/// Ver INC-642.
/// </summary>
public class CollectiblePopupQueue : MonoBehaviour, IAvisoEnPantalla
{
    [Header("References")]
    [Tooltip("Prefab con un CollectiblePopupPanel.")]
    public GameObject popupPrefab;

    [Tooltip("Contenedor donde se crean los avisos (normalmente un panel con VerticalLayoutGroup bajo un Canvas).")]
    public Transform popupParent;

    [Header("Settings")]
    [Tooltip("Segundos que se ve cada aviso (solo cuentan mientras está a la vista).")]
    public float displayDuration = 1.25f;

    [Tooltip("True: los avisos nuevos se colocan arriba de la pila. False: abajo (ajustar el VerticalLayoutGroup para apilar desde abajo).")]
    public bool spawnAtTop = false;

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
    // Avisos en pantalla.
    private readonly List<CollectiblePopupPanel> _vivos = new();

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
        if (_esperando.Count > 0 || _vivos.Count > 0) ArbitroDeAvisos.Pedir(this);
    }

    void OnDisable()
    {
        if (_activa == this) _activa = null;
        if (_enableRoutine != null)
        {
            StopCoroutine(_enableRoutine);
            _enableRoutine = null;
        }
        // Las corrutinas de agrupado se paran con el objeto: lo que se estaba juntando se pierde.
        _agrupando.Clear();
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
            SpawnPopup(_esperando[i].item, _esperando[i].cantidad);
        _esperando.Clear();

        if (_vivos.Count == 0) ArbitroDeAvisos.Terminar(this);
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        _grupoDelContenedor.alpha = 0f;
        for (int i = 0; i < _vivos.Count; i++)
            if (_vivos[i] != null) _vivos[i].Congelar(true);
    }

    public void AlDescartar()
    {
        _aLaVista = false;
        _esperando.Clear();
        DestroyActivePopups();
    }

    // ── Avisos ─────────────────────────────────────────────────────────────────

    private void Encolar(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (_aLaVista)
        {
            SpawnPopup(item, amount);
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

    private void SpawnPopup(ItemData item, int amountToShow)
    {
        var key = Clave(item);

        // Si ya hay un aviso de este objeto en pantalla, se le suma la cantidad.
        for (int i = 0; i < _vivos.Count; i++)
        {
            var vivo = _vivos[i];
            if (vivo != null && vivo.ItemId == key)
            {
                vivo.AddAmount(amountToShow);
                return;
            }
        }

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

        // El layout del contenedor decide la posición de cada aviso.
        if (go.transform is RectTransform rt)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
        if (!go.activeSelf) go.SetActive(true);

        panel.Acabado += AlAcabarUnAviso;
        _vivos.Add(panel);
        panel.Init(item, amountToShow, displayDuration);
        if (parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }

    private void AlAcabarUnAviso(CollectiblePopupPanel panel)
    {
        _vivos.Remove(panel);
        for (int i = _vivos.Count - 1; i >= 0; i--)
            if (_vivos[i] == null) _vivos.RemoveAt(i);
        if (_vivos.Count == 0 && _esperando.Count == 0) ArbitroDeAvisos.Terminar(this);
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
