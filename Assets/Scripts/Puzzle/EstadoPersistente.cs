using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Recuerda en la partida que algo ya ocurrió (un puzle resuelto, un brasero encendido, un
/// cofre abierto…). Quien lo resuelve llama a <see cref="Marcar"/>; al cargar la partida, si ya
/// estaba marcado, lanza <see cref="alRestaurar"/> para dejar el mundo como estaba sin repetir
/// animaciones ni recompensas.
/// </summary>
[DisallowMultipleComponent]
public sealed class EstadoPersistente : MonoBehaviour
{
    /// <summary>Prefijo de los flags de este componente (permite borrarlos en bloque).</summary>
    public const string Prefijo = "ESTADO_";

    [Tooltip("Id fijo. Vacío = automático (escena + nombre + posición).")]
    [SerializeField] private string idManual;

    [Tooltip("Al cargar la partida, si ya estaba marcado: deja el mundo como quedó.")]
    public UnityEvent alRestaurar;

    private FlagDePartida _flag;
    private string _id;
    private bool _hecho;

    public bool Hecho => _hecho;
    public string Clave => Prefijo + Id;

    private string Id
    {
        get
        {
            if (string.IsNullOrEmpty(_id))
                _id = string.IsNullOrWhiteSpace(idManual) ? IdDePersistencia.DeObjeto(gameObject) : idManual;
            return _id;
        }
    }

    private bool _empezado;

    private void Awake() => _flag = new FlagDePartida(nameof(EstadoPersistente), () => Clave, Restaurar);

    // La primera comprobación espera a Start: lo que se restaura (puertas, ascensores…) ya ha
    // hecho su Awake y su OnEnable.
    private void Start()
    {
        _empezado = true;
        _flag.Escuchar();
    }

    private void OnEnable()
    {
        if (_empezado) _flag.Escuchar();
    }

    private void OnDisable() => _flag.DejarDeEscuchar();

    /// <summary>Anota que ya ocurrió (conectar al evento que resuelve el puzle).</summary>
    public void Marcar()
    {
        _hecho = true;
        _flag.Marcar();
    }

    /// <summary>Olvida el estado (para reiniciar pruebas).</summary>
    public void Olvidar()
    {
        _hecho = false;
        _flag.Borrar();
    }

    private void Restaurar()
    {
        if (_hecho) return;
        _hecho = true;
        alRestaurar.Invoke();
    }
}
