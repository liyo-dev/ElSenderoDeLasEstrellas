using System.Collections;
using UnityEngine;

/// <summary>
/// Mensajes cortos del juego en el rótulo breve de abajo («toast»): no bloquean nada y se van
/// solos. Se auto-crea al arrancar el juego; no requiere prefab ni setup en escena.
///
/// Es un aviso del canal Rotulo (<see cref="ArbitroDeAvisos"/>): mientras hay un menú o una
/// cinemática el mensaje espera con la cuenta atrás parada. Un mensaje nuevo sustituye al que se
/// esté viendo. El texto lo dibuja <see cref="DramaticTextOverlayUI"/>, el servicio único de
/// texto en pantalla.
///
/// Uso:
///   HudToastService.Instance.Show("NPC_REQUIRES_WILL");
///   HudToastService.Instance.Show("NPC_REQUIRES_WILL", duration: 2.5f);
/// </summary>
public class HudToastService : MonoBehaviour, IAvisoEnPantalla
{
    public static HudToastService Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    const float Entrada = 0.25f;
    const float Salida = 0.35f;

    private string _mensaje;
    private float _duracion;
    // Hay un mensaje por enseñar o a medio enseñar.
    private bool _pendiente;
    // Tiene el turno y nada lo tapa: la cuenta atrás corre.
    private bool _aLaVista;
    private float _visto;
    private Coroutine _cuentaCo;

    public CanalDeAviso Canal => CanalDeAviso.Rotulo;
    public int Prioridad => 0;
    public bool Pasajero => true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var root = new GameObject("[HudToastService]");
        DontDestroyOnLoad(root);
        Instance = root.AddComponent<HudToastService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        ArbitroDeAvisos.Terminar(this);
        if (Instance == this) Instance = null;
    }

    // ────────────────────────────────────────────────────────────────────────
    #region API pública

    /// <summary>Muestra un mensaje localizado durante <paramref name="duration"/> segundos.</summary>
    public void Show(string locKey, float duration = 3f)
    {
        string msg = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(locKey, locKey)
            : locKey;

        ShowRaw(msg, duration);
    }

    /// <summary>Muestra un mensaje literal (sin pasar por localización).</summary>
    public void ShowRaw(string message, float duration = 3f)
    {
        _mensaje = message;
        _duracion = duration;
        _pendiente = true;
        _visto = 0f;

        if (_aLaVista) Empezar();
        else ArbitroDeAvisos.Pedir(this);
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region IAvisoEnPantalla

    public void AlTenerTurno()
    {
        _aLaVista = true;
        if (!_pendiente || Rotulo == null)
        {
            _pendiente = false;
            ArbitroDeAvisos.Terminar(this);
            return;
        }
        if (_cuentaCo == null) Empezar();
        else Rotulo.FundirRotuloBreve(1f, Entrada);
    }

    public void AlPerderTurno()
    {
        _aLaVista = false;
        Rotulo?.FundirRotuloBreve(0f, 0f);
    }

    public void AlDescartar()
    {
        _aLaVista = false;
        _pendiente = false;
        Parar();
        Rotulo?.FundirRotuloBreve(0f, Salida);
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Internals

    private static DramaticTextOverlayUI Rotulo => DramaticTextOverlayUI.Instance;

    private void Empezar()
    {
        Parar();
        var rotulo = Rotulo;
        if (rotulo == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[HudToastService] No hay DramaticTextOverlayUI en escena: el mensaje no se ve.");
#endif
            _pendiente = false;
            ArbitroDeAvisos.Terminar(this);
            return;
        }
        rotulo.PonerRotuloBreve(_mensaje);
        rotulo.FundirRotuloBreve(1f, Entrada);
        _cuentaCo = StartCoroutine(Co_Cuenta());
    }

    // El tiempo en pantalla solo corre mientras el mensaje está a la vista.
    private IEnumerator Co_Cuenta()
    {
        while (_visto < _duracion)
        {
            if (_aLaVista) _visto += Time.unscaledDeltaTime;
            yield return null;
        }
        Rotulo?.FundirRotuloBreve(0f, Salida);
        _cuentaCo = null;
        _pendiente = false;
        _aLaVista = false;
        ArbitroDeAvisos.Terminar(this);
    }

    private void Parar()
    {
        if (_cuentaCo != null) { StopCoroutine(_cuentaCo); _cuentaCo = null; }
    }

    #endregion
}
