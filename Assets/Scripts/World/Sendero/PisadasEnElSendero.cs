using UnityEngine;

/// <summary>
/// Publica las últimas pisadas del jugador para que el suelo de luz del Sendero
/// (shader Sendero/SueloDeLuz) encienda las constelaciones bajo sus pasos. Guarda 16 puntos
/// (posición y momento) en el global _SenderoPisadas; se apuntan cada `paso` metros recorridos.
/// </summary>
public class PisadasEnElSendero : MonoBehaviour
{
    private const int Cuantas = 16;
    private static readonly int PisadasId = Shader.PropertyToID("_SenderoPisadas");

    [Tooltip("Metros entre una pisada y la siguiente.")]
    [SerializeField, Min(0.1f)] private float paso = 0.7f;

    private readonly Vector4[] _pisadas = new Vector4[Cuantas];
    private Transform _jugador;
    private Vector3 _ultima;
    private int _siguiente;
    private bool _hayUltima;

    private void OnEnable()
    {
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        PlayerService.OnPlayerUnregistered += AlQuitarJugador;
        AlRegistrarJugador(PlayerService.Player);
        Limpiar();
    }

    private void OnDisable()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        PlayerService.OnPlayerUnregistered -= AlQuitarJugador;
        Limpiar();
    }

    private void AlRegistrarJugador(GameObject jugador)
    {
        _jugador = jugador != null ? jugador.transform : null;
        _hayUltima = false;
    }

    private void AlQuitarJugador() => _jugador = null;

    private void Limpiar()
    {
        for (int i = 0; i < Cuantas; i++) _pisadas[i] = Vector4.zero;
        Shader.SetGlobalVectorArray(PisadasId, _pisadas);
    }

    private void Update()
    {
        if (_jugador == null) return;
        Vector3 p = _jugador.position;
        if (_hayUltima && (p - _ultima).sqrMagnitude < paso * paso) return;
        _hayUltima = true;
        _ultima = p;
        _pisadas[_siguiente] = new Vector4(p.x, p.y, p.z, Time.timeSinceLevelLoad);
        _siguiente = (_siguiente + 1) % Cuantas;
        Shader.SetGlobalVectorArray(PisadasId, _pisadas);
    }
}
