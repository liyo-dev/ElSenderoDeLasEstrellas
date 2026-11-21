using UnityEngine;

/// <summary>
/// Símbolo de «guardando» en una esquina de la pantalla mientras se escribe la partida
/// (<see cref="Partida.GuardadoEmpieza"/> / <see cref="Partida.GuardadoAcaba"/>): auto-guardado,
/// farol o cualquier otro guardado. Se ve al menos <see cref="EstiloDelGuardado.segundosMinimos"/>
/// aunque el guardado sea instantáneo, con fundido de entrada y de salida. Es un indicador del
/// sistema, no un aviso: no pasa por el árbitro de avisos y sale por encima de todo (también de
/// menús, cinemáticas y fundidos). Se auto-crea al arrancar el juego; no requiere setup en escena.
/// </summary>
public sealed class IndicadorDeGuardado : MonoBehaviour
{
    const int OrdenDelLienzo = 9999;

    public static IndicadorDeGuardado Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    private EstiloDelGuardado _ajustes;
    private CanvasGroup _grupo;
    private GameObject _esquina;
    private int _guardadosEnCurso;
    private float _visibleHasta;
    private float _alfa;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[IndicadorDeGuardado]");
        DontDestroyOnLoad(go);
        go.AddComponent<IndicadorDeGuardado>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _ajustes = EstiloDelGuardado.Cargado;
        Montar();
        // Se escucha desde Awake y no desde OnEnable: el componente se apaga mientras no se ve
        // (para no correr Update) y tiene que seguir oyendo los guardados. Se suelta al destruirse.
        Partida.GuardadoEmpieza += AlEmpezarAGuardar;
        Partida.GuardadoAcaba += AlAcabarDeGuardar;
        enabled = false;
    }

    void OnDestroy()
    {
        Partida.GuardadoEmpieza -= AlEmpezarAGuardar;
        Partida.GuardadoAcaba -= AlAcabarDeGuardar;
        if (Instance == this) Instance = null;
    }

    private void Montar()
    {
        PiezasDeUI.Lienzo(gameObject, OrdenDelLienzo, conClic: false);
        _grupo = gameObject.AddComponent<CanvasGroup>();
        _grupo.interactable = false;
        _grupo.blocksRaycasts = false;
        _grupo.alpha = 0f;

        var esquina = new GameObject("Esquina", typeof(RectTransform)).transform as RectTransform;
        esquina.SetParent(transform, false);
        bool derecha = _ajustes.esquina == EstiloDelGuardado.Esquina.AbajoDerecha || _ajustes.esquina == EstiloDelGuardado.Esquina.ArribaDerecha;
        bool abajo = _ajustes.esquina == EstiloDelGuardado.Esquina.AbajoDerecha || _ajustes.esquina == EstiloDelGuardado.Esquina.AbajoIzquierda;
        float mitad = _ajustes.lado * 0.5f;
        var ancla = new Vector2(derecha ? 1f : 0f, abajo ? 0f : 1f);
        var posicion = new Vector2(derecha ? -_ajustes.margen.x - mitad : _ajustes.margen.x + mitad,
                                   abajo ? _ajustes.margen.y + mitad : -_ajustes.margen.y - mitad);
        PiezasDeUI.Colocar(esquina, ancla, posicion, new Vector2(_ajustes.lado, _ajustes.lado));
        SimboloDeGuardado.Crear(esquina, _ajustes.lado, _ajustes);
        _esquina = esquina.gameObject;
        _esquina.SetActive(false);
    }

    private void AlEmpezarAGuardar()
    {
        _guardadosEnCurso++;
        _visibleHasta = Mathf.Max(_visibleHasta, Time.unscaledTime + _ajustes.segundosMinimos);
        if (!_esquina.activeSelf) _esquina.SetActive(true);
        enabled = true;
    }

    private void AlAcabarDeGuardar(bool ok)
    {
        if (_guardadosEnCurso > 0) _guardadosEnCurso--;
    }

    void Update()
    {
        bool debeVerse = _guardadosEnCurso > 0 || Time.unscaledTime < _visibleHasta;
        float paso = Time.unscaledDeltaTime / (debeVerse ? _ajustes.fundidoDeEntrada : _ajustes.fundidoDeSalida);
        _alfa = Mathf.MoveTowards(_alfa, debeVerse ? 1f : 0f, paso);
        _grupo.alpha = _alfa;

        if (!debeVerse && _alfa <= 0f)
        {
            if (_esquina.activeSelf) _esquina.SetActive(false);
            enabled = false;
        }
    }
}
