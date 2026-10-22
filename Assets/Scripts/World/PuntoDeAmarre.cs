using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un punto de amarre: el sitio (el muelle, la playa, el embarcadero) donde el barco se arrima y
/// donde se puede bajar a tierra firme.
///
/// El icono de interactuar NO va en el barco, va en el puerto: cuando el jugador va a bordo y
/// el barco se acerca a un amarre, este se enciende y, al pulsarlo, el jugador baja a tierra.
/// </summary>
[DisallowMultipleComponent]
public class PuntoDeAmarre : MonoBehaviour
{
    private static readonly List<PuntoDeAmarre> _todos = new();

    /// <summary>Todos los puntos de amarre de la escena.</summary>
    public static IReadOnlyList<PuntoDeAmarre> Todos => _todos;

    private Interactable _interactuable;
    private BarcoNavegable _barco;
    private bool _suscrito;

    /// <summary>El barco que está atracado aquí ahora mismo (null si no hay ninguno).</summary>
    public BarcoNavegable Barco => _barco;

    /// <summary>Asigna el interactuable (lo llama el generador, que lo crea).</summary>
    public void AsignarInteractuable(Interactable interactuable)
    {
        _interactuable = interactuable;
        if (_interactuable != null)
        {
            _interactuable.SetMode(InteractableMode.Custom);
            _interactuable.OnInteract.AddListener(HandleInteract);
            _suscrito = true;
            _interactuable.EnableInteraction(false);
        }
    }

    /// <summary>El interactuable del punto (el que lleva el icono).</summary>
    public Interactable Interactuable
    {
        get
        {
            if (_interactuable == null) _interactuable = GetComponent<Interactable>();
            return _interactuable;
        }
    }

    void OnEnable() => _todos.Add(this);

    void OnDisable() => _todos.Remove(this);

    void Awake()
    {
        _interactuable = GetComponent<Interactable>();
        if (_interactuable != null)
        {
            _interactuable.SetMode(InteractableMode.Custom);
            // En el Editor el Awake no se ejecuta, así que el generador ya lo enganchó: no se
            // vuelve a suscribir o el evento se dispararía dos veces.
            if (!_suscrito)
            {
                _interactuable.OnInteract.AddListener(HandleInteract);
                _suscrito = true;
            }
            _interactuable.EnableInteraction(false);
        }
    }

    void OnDestroy()
    {
        if (_interactuable != null) _interactuable.OnInteract.RemoveListener(HandleInteract);
    }

    /// <summary>El punto de amarre más cercano a una posición (null si no hay ninguno cerca).</summary>
    public static PuntoDeAmarre MasCerca(Vector3 posicion, float radio)
    {
        PuntoDeAmarre mejor = null;
        float mejorDistancia = radio * radio;
        for (int i = 0; i < _todos.Count; i++)
        {
            var p = _todos[i];
            if (p == null) continue;
            float d = (p.transform.position - posicion).sqrMagnitude;
            if (d < mejorDistancia)
            {
                mejorDistancia = d;
                mejor = p;
            }
        }
        return mejor;
    }

    /// <summary>Enciende el icono: hay un barco con alguien a bordo atracado aquí.</summary>
    public void Activar(BarcoNavegable barco)
    {
        _barco = barco;
        var interactuable = Interactuable;
        if (interactuable != null) interactuable.EnableInteraction(true);
    }

    /// <summary>Apaga el icono: el barco se ha ido.</summary>
    public void Desactivar()
    {
        _barco = null;
        var interactuable = Interactuable;
        if (interactuable != null) interactuable.EnableInteraction(false);
    }

    void HandleInteract(GameObject interactor)
    {
        if (_barco == null) return;

        // Pregunta antes de bajar, que es lo que va a pasar (a la playa, al agua, al muelle).
        var popup = ConfirmationPopupUI.Instance;
        if (popup == null)
        {
            _barco.Desembarcar();
            return;
        }

        string mensaje = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get("BARCO_BAJAR", "¿Bajar del barco?")
            : "¿Bajar del barco?";

        popup.Show(mensaje, onConfirm: () => _barco.Desembarcar());
    }
}
