using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sistema para que un personaje (Estela o Will) cargue en brazos a un compañero caído (Liam).
/// Resuelve la animación de agacharse a cogerlo (Pick Up), el anclaje físico horizontal en brazos,
/// los ojos cerrados del cuerpo y la transición a la postura de porteo en la capa UpperBody.
/// </summary>
[DisallowMultipleComponent]
public class PorteoDeCompanero : MonoBehaviour
{
    [Header("Actores")]
    [Tooltip("Personaje que lleva el cuerpo. Si está vacío, se toma este GameObject.")]
    [SerializeField] private Transform portador;

    [Tooltip("El cuerpo del compañero caído que se va a llevar (p. ej. Liam).")]
    [SerializeField] private Transform cuerpo;

    [Header("Socket de Porteo")]
    [Tooltip("Punto de anclaje en el portador. Si no existe, se genera automáticamente a la altura del pecho.")]
    [SerializeField] private Transform socketPorteo;

    [Tooltip("Posición relativa del cuerpo respecto al socket de porteo (calibrado a las manos del portador).")]
    [SerializeField] private Vector3 offsetPosicion = new Vector3(-0.08f, -0.16f, 0f);

    [Tooltip("Rotación Euler relativa del cuerpo para que repose tumbado boca arriba en brazos.")]
    [SerializeField] private Vector3 offsetRotacion = Vector3.zero;

    [Header("Animaciones y Gestos")]
    [Tooltip("Animación de agacharse a coger al compañero desde el suelo.")]
    [SerializeField] private string animacionPickUp = "CarryStart_NoWeapon";

    [Tooltip("Segundos desde que empieza la animación de pick up hasta que las manos tocan al cuerpo.")]
    [SerializeField] private float tiempoHastaCoger = 0.5f;

    [Tooltip("Estado en la capa UpperBody del portador al caminar/estar de pie llevando el cuerpo.")]
    [SerializeField] private string animacionPortador = "CarryMoveIdle_NoWeapon";

    [Tooltip("Pose del cuerpo mientras es transportado (reposo en brazos).")]
    [SerializeField] private string poseCuerpoTransportado = "Sleeping_NoWeapon";

    [Tooltip("Pose del cuerpo al dejarlo en el suelo.")]
    [SerializeField] private string poseCuerpoEnSuelo = "Die01Stay_NoWeapon";

    [Tooltip("Nombre de la malla de ojos cerrados del cuerpo (en Liam es Eye09).")]
    [SerializeField] private string mallaOjosCerrados = "Eye09";

    [Tooltip("Índice de la capa UpperBody en el Animator (por defecto 1).")]
    [SerializeField] private int upperBodyLayer = 1;

    [Header("Efectos")]
    [Tooltip("Efecto de partículas suave o aura entre los brazos y el cuerpo (opcional).")]
    [SerializeField] private GameObject vfxLechoLuz;

    [Header("Eventos")]
    public UnityEvent alEmpezarPickUp;
    public UnityEvent alCargar;
    public UnityEvent alSoltar;

    private bool _cargando;
    private bool _haciendoPickUp;
    private Animator _animatorPortador;
    private Animator _animatorCuerpo;
    private Collider[] _collidersCuerpo;
    private Rigidbody _rbCuerpo;
    private Transform _padreOriginalCuerpo;
    private PlayerActionManager _playerActionManager;
    private bool _pushedActionMode;

    public bool EstaCargando => _cargando;
    public bool HaciendoPickUp => _haciendoPickUp;
    public Transform Cuerpo => cuerpo;
    public Transform Portador => portador;
    public Vector3 OffsetPosicion { get => offsetPosicion; set => offsetPosicion = value; }
    public Vector3 OffsetRotacion { get => offsetRotacion; set => offsetRotacion = value; }

    private void Awake()
    {
        if (portador == null) portador = transform;
        _animatorPortador = portador.GetComponentInChildren<Animator>(true);
        _playerActionManager = portador.GetComponent<PlayerActionManager>();

        AsegurarSocket();
    }

    private void OnDisable()
    {
        if (_pushedActionMode && _playerActionManager != null)
        {
            _playerActionManager.PopMode(ActionMode.Carrying);
            _pushedActionMode = false;
        }
    }

    public void AsegurarSocket()
    {
        if (socketPorteo == null)
        {
            var existente = portador != null ? portador.Find("PorteoPoint") : transform.Find("PorteoPoint");
            if (existente != null)
            {
                socketPorteo = existente;
            }
            else
            {
                var nuevoSocket = new GameObject("PorteoPoint");
                nuevoSocket.transform.SetParent(portador != null ? portador : transform, false);
                socketPorteo = nuevoSocket.transform;
            }
        }
        socketPorteo.localPosition = new Vector3(0f, 0.68f, 0.22f);
        socketPorteo.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Secuencia completa de Pick Up: se agacha con CarryStart_NoWeapon, levanta a Liam
    /// tumbado con ojos cerrados y pasa a CarryMoveIdle_NoWeapon.
    /// </summary>
    public void RecogerYCargar(Transform objetivoCuerpo = null)
    {
        if (objetivoCuerpo != null) cuerpo = objetivoCuerpo;
        if (cuerpo == null) return;

        if (Application.isPlaying)
        {
            StartCoroutine(Co_RecogerYCargar());
        }
        else
        {
            // En EditMode se acopla directamente en brazos
            CargarDirecto();
        }
    }

    private IEnumerator Co_RecogerYCargar()
    {
        _haciendoPickUp = true;
        alEmpezarPickUp?.Invoke();

        if (portador == null) portador = transform;
        if (_animatorPortador == null) _animatorPortador = portador.GetComponentInChildren<Animator>(true);

        // 1. Orientar hacia el cuerpo en el plano horizontal
        if (cuerpo != null)
        {
            Vector3 dir = (cuerpo.position - portador.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                portador.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        // 2. Reproducir animación de agacharse a coger (CarryStart_NoWeapon)
        if (_animatorPortador != null)
        {
            _animatorPortador.CrossFade(animacionPickUp, 0.15f, 0);
        }

        // 3. Esperar al fotograma clave donde las manos llegan abajo
        yield return new WaitForSeconds(tiempoHastaCoger);

        // 4. Acoplar al cuerpo en brazos con la rotación horizontal y cerrar ojos
        CargarDirecto();

        // 5. Esperar el resto de la levantada
        yield return new WaitForSeconds(0.45f);

        _haciendoPickUp = false;
    }

    /// <summary>
    /// Acopla al compañero directamente en brazos (sin esperar animación de agacharse).
    /// </summary>
    public void CargarDirecto(Transform objetivoCuerpo = null)
    {
        if (objetivoCuerpo != null) cuerpo = objetivoCuerpo;
        if (cuerpo == null) return;

        if (portador == null) portador = transform;
        AsegurarSocket();

        _animatorPortador = portador.GetComponentInChildren<Animator>(true);
        _playerActionManager = portador.GetComponent<PlayerActionManager>();
        _animatorCuerpo = cuerpo.GetComponentInChildren<Animator>(true);
        _collidersCuerpo = cuerpo.GetComponentsInChildren<Collider>(true);
        _rbCuerpo = cuerpo.GetComponent<Rigidbody>();
        _padreOriginalCuerpo = cuerpo.parent;

        // 1. Apagar físicas, colisiones y motores de movimiento en el cuerpo
        if (_rbCuerpo != null)
        {
            _rbCuerpo.isKinematic = true;
            _rbCuerpo.useGravity = false;
        }

        if (_collidersCuerpo != null)
        {
            for (int i = 0; i < _collidersCuerpo.Length; i++)
            {
                if (_collidersCuerpo[i] != null)
                    _collidersCuerpo[i].enabled = false;
            }
        }

        var scriptsCuerpo = cuerpo.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scriptsCuerpo.Length; i++)
        {
            var mb = scriptsCuerpo[i];
            if (mb == null || mb == this) continue;
            string n = mb.GetType().Name;
            if (n.Contains("Controller") || n.Contains("Motor") || n.Contains("Brain") || n.Contains("SimpleAnimator") || n.Contains("NavMesh"))
            {
                mb.enabled = false;
            }
        }

        // 2. Emparentar al socket con la orientación horizontal tumbado
        cuerpo.SetParent(socketPorteo, false);
        cuerpo.localPosition = offsetPosicion;
        cuerpo.localRotation = Quaternion.Euler(offsetRotacion);

        // 3. Poner ojos cerrados en Liam
        CerrarOjos(cuerpo, mallaOjosCerrados);

        // 4. Pose del cuerpo en reposo
        if (_animatorCuerpo != null)
        {
            _animatorCuerpo.applyRootMotion = false;
            if (Application.isPlaying)
            {
                _animatorCuerpo.Play(poseCuerpoTransportado, 0, 0f);
                _animatorCuerpo.Update(0f);
            }
        }

        // 5. Activar postura de carga en portador (UpperBody layer 1)
        if (_animatorPortador != null)
        {
            if (upperBodyLayer > 0 && upperBodyLayer < _animatorPortador.layerCount)
            {
                _animatorPortador.SetLayerWeight(upperBodyLayer, 1f);
                _animatorPortador.CrossFade(animacionPortador, 0.2f, upperBodyLayer);
            }
            else
            {
                _animatorPortador.CrossFade(animacionPortador, 0.2f, 0);
            }
        }

        // 6. Bloquear habilidades de combate si es el jugador
        if (_playerActionManager != null && !_pushedActionMode)
        {
            _playerActionManager.PushMode(ActionMode.Carrying);
            _pushedActionMode = true;
        }

        // 7. VFX de luz suave
        if (vfxLechoLuz != null)
            vfxLechoLuz.SetActive(true);

        _cargando = true;
        alCargar?.Invoke();
    }

    /// <summary>
    /// Deposita al compañero en el suelo en la posición y orientación indicadas.
    /// </summary>
    public void Soltar(Vector3? posicionSuelo = null, Quaternion? rotacionSuelo = null)
    {
        if (!_cargando || cuerpo == null) return;

        Vector3 posDestino = posicionSuelo ?? (portador.position + portador.forward * 0.8f);
        Quaternion rotDestino = rotacionSuelo ?? Quaternion.LookRotation(portador.forward, Vector3.up);

        // 1. Desemparentar y colocar en el suelo
        cuerpo.SetParent(_padreOriginalCuerpo, true);
        cuerpo.position = posDestino;
        cuerpo.rotation = rotDestino;

        // 2. Pose en el suelo
        if (_animatorCuerpo != null && Application.isPlaying)
        {
            _animatorCuerpo.Play(poseCuerpoEnSuelo, 0, 0f);
            _animatorCuerpo.Update(0f);
        }

        // 3. Reactivar colliders
        if (_collidersCuerpo != null)
        {
            for (int i = 0; i < _collidersCuerpo.Length; i++)
            {
                if (_collidersCuerpo[i] != null)
                    _collidersCuerpo[i].enabled = true;
            }
        }

        if (_rbCuerpo != null)
        {
            _rbCuerpo.isKinematic = true;
        }

        // 4. Liberar capa UpperBody del portador
        if (_animatorPortador != null && upperBodyLayer > 0 && upperBodyLayer < _animatorPortador.layerCount)
        {
            _animatorPortador.SetLayerWeight(upperBodyLayer, 0f);
        }

        // 5. Restaurar modo del jugador
        if (_pushedActionMode && _playerActionManager != null)
        {
            _playerActionManager.PopMode(ActionMode.Carrying);
            _pushedActionMode = false;
        }

        if (vfxLechoLuz != null)
            vfxLechoLuz.SetActive(false);

        _cargando = false;
        alSoltar?.Invoke();
    }

    /// <summary>
    /// Activa la malla de ojos cerrados (Eye09) y desactiva las demás mallas de ojos del cuerpo.
    /// Funciona tanto en runtime como en EditMode directamente sobre las mallas.
    /// </summary>
    public static void CerrarOjos(Transform objetivoCuerpo, string nombreOjos = "Eye09")
    {
        if (objetivoCuerpo == null) return;

        var emotion = Game.NPC.Common.EmotionControllerResolver.Resolve(objetivoCuerpo.gameObject);
        if (emotion != null)
        {
            emotion.AplicarOjos(nombreOjos);
        }

        // Garantizar activación en EditMode y PlayMode
        var renderers = objetivoCuerpo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string n = renderers[i].gameObject.name;
            if (n.StartsWith("Eye", StringComparison.OrdinalIgnoreCase))
            {
                bool esOjoCerrado = n.Equals(nombreOjos, StringComparison.OrdinalIgnoreCase);
                renderers[i].gameObject.SetActive(esOjoCerrado);
            }
        }
    }

    public static void RestaurarOjos(Transform objetivoCuerpo)
    {
        if (objetivoCuerpo == null) return;

        var emotion = Game.NPC.Common.EmotionControllerResolver.Resolve(objetivoCuerpo.gameObject);
        if (emotion != null)
        {
            emotion.VolverAReposo();
            return;
        }

        var renderers = objetivoCuerpo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string n = renderers[i].gameObject.name;
            if (n.StartsWith("Eye", StringComparison.OrdinalIgnoreCase))
            {
                renderers[i].gameObject.SetActive(n.Equals("Eye01", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Probar Cargar (En Brazos)")]
    private void ProbarCargar() => CargarDirecto();

    [ContextMenu("Probar Recoger (Pick Up)")]
    private void ProbarRecoger() => RecogerYCargar();

    [ContextMenu("Soltar al Suelo")]
    private void ProbarSoltar() => Soltar();

    private void OnDrawGizmosSelected()
    {
        Transform s = socketPorteo != null ? socketPorteo : transform;
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.8f);
        Vector3 posCuerpo = s.TransformPoint(offsetPosicion);
        Gizmos.DrawWireSphere(posCuerpo, 0.12f);

        // Visualizar cuerpo tumbado horizontal
        Quaternion rotCuerpo = s.rotation * Quaternion.Euler(offsetRotacion);
        Vector3 cabeza = posCuerpo - rotCuerpo * Vector3.up * 0.45f;
        Vector3 pies = posCuerpo + rotCuerpo * Vector3.up * 0.45f;
        Vector3 pecho = posCuerpo + rotCuerpo * Vector3.forward * 0.25f;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(cabeza, pies);
        Gizmos.DrawSphere(cabeza, 0.08f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(posCuerpo, pecho);
    }
#endif
}
