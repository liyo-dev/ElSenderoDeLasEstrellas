using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CastleDoorController : MonoBehaviour
{
    [Header("Puertas")]
    [SerializeField] private DoorGate leftDoor;
    [SerializeField] private DoorGate rightDoor;
    [Tooltip("Si true, las puertas arrancan cerradas al iniciar la escena (a menos que haya una flag de apertura guardada).")]
    [SerializeField] private bool startsLocked = true;

    [Header("Persistencia")]
    [Tooltip("Si está activo, el estado abierto se guarda en la partida y se restaura al cargar.")]
    [SerializeField] private bool persistState = true;
    [Tooltip("Override manual del ID de persistencia. Vacío = se genera automáticamente (escena + nombre + posición).")]
    [SerializeField] private string persistenceId;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando las puertas se abren durante la sesión activa.")]
    public UnityEvent OnDoorsOpened;
    [Tooltip("Se dispara cuando las puertas se restauran abiertas al cargar partida (sin animación).")]
    public UnityEvent OnDoorsOpenedRestored;

    bool _isOpen;
    FlagDePartida _abierta;

    void Awake()
    {
        _abierta = new FlagDePartida(nameof(CastleDoorController), () => $"DOOR_OPEN_{ResolveId()}", AlCargarAbierta);
        if (startsLocked)
        {
            leftDoor?.Close();
            rightDoor?.Close();
        }
        else
        {
            _isOpen = true;
        }
    }

    void OnEnable()
    {
        if (persistState) _abierta.Escuchar();
    }

    void OnDisable() => _abierta.DejarDeEscuchar();

    void AlCargarAbierta()
    {
        if (!_isOpen) ApplyOpenInstant();
    }

    public void OpenDoors()
    {
        if (_isOpen) return;
        _isOpen = true;
        leftDoor?.Open();
        rightDoor?.Open();
        if (persistState) _abierta.Marcar();
        OnDoorsOpened?.Invoke();
    }

    public void CloseDoors()
    {
        _isOpen = false;
        leftDoor?.Close();
        rightDoor?.Close();
        if (persistState) _abierta.Borrar();
    }

    void ApplyOpenInstant()
    {
        _isOpen = true;
        leftDoor?.OpenInstant();
        rightDoor?.OpenInstant();
        OnDoorsOpenedRestored?.Invoke();
    }

    string ResolveId()
    {
        if (!string.IsNullOrEmpty(persistenceId)) return persistenceId;
        return IdDePersistencia.DeObjeto(gameObject);
    }
}
