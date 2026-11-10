using UnityEngine;

/// <summary>
/// Pone un atuendo de escena (<see cref="AtuendoDeEscena"/>) mientras dura un tramo de la historia
/// y este objeto está activo, y lo quita al acabar el tramo o al descargarse la escena. El tramo se
/// consulta (como en <see cref="ActivoEnTramo"/>): se revisa al empezar y cada vez que cambia el
/// historial de señales, así que al cargar una partida a mitad del tramo el atuendo vuelve a estar
/// puesto aunque no se guarde. Para vestir a alguien en mitad de una cinemática basta con emitir
/// la señal de inicio del tramo en ese plano (<c>SenalBeat</c>).
/// </summary>
public sealed class AtuendoEnTramo : MonoBehaviour
{
    [Tooltip("Atuendo que se pone.")]
    [SerializeField] private AtuendoDeEscena atuendo;

    [Tooltip("Tramo de la historia durante el que se lleva. Sin condiciones, se lleva mientras este objeto esté activo.")]
    [SerializeField] private TramoDeHistoria tramo = new();

    private string _clave;
    private bool _puesto;

    public AtuendoDeEscena Atuendo => atuendo;
    public TramoDeHistoria Tramo => tramo;

    private void Awake() => _clave = "AtuendoEnTramo:" + GetEntityId();

    private void OnEnable()
    {
        DefaultNarrativeSignals.OnStateChanged += Revisar;
        DefaultNarrativeSignals.OnAfterReset += Revisar;
        GameBootService.OnProfileReady += Revisar;
        Revisar();
    }

    private void OnDisable()
    {
        DefaultNarrativeSignals.OnStateChanged -= Revisar;
        DefaultNarrativeSignals.OnAfterReset -= Revisar;
        GameBootService.OnProfileReady -= Revisar;
        Quitar();
    }

    private void Revisar()
    {
        var registro = CharacterAppearanceRegistry.Instance;
        if (registro == null) return;
        bool poner = atuendo != null && tramo.EnCurso();
        if (poner == _puesto) return;
        if (poner) registro.PonerAtuendo(_clave, atuendo);
        else registro.QuitarAtuendo(_clave);
        _puesto = poner;
    }

    private void Quitar()
    {
        if (!_puesto) return;
        _puesto = false;
        CharacterAppearanceRegistry.Instance?.QuitarAtuendo(_clave);
    }
}
