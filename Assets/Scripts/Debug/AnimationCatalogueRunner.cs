using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reproduce, una detrás de otra, TODAS las animaciones del personaje y después todas las piezas
/// de cara, con el nombre real de cada una escrito en pantalla.
///
/// PARA QUÉ SIRVE. Los nombres del pack de animaciones no dicen gran cosa (`Flourish`,
/// `Challenging_NoWeapon`, `SenseSomethingStart_NoWeapon`…), así que a la hora de montar una
/// secuencia no hay forma de saber cuál pedir sin ir probando. Esto se graba UNA vez y a partir de
/// ahí existe un catálogo escrito: nombre real ↔ qué hace. Lo mismo con `Eye01…Eye12` y
/// `Mouth01…Mouth12`, que son todavía menos deductivos.
///
/// La lista y el orden NO se calculan aquí: los rellena AnimationCatalogueBuilder al crear la
/// escena, leyendo el Animator Controller desde el Editor. Así el fichero de texto que se genera
/// y lo que se ve en el vídeo van en el MISMO orden, que es lo que permite emparejarlos después.
/// </summary>
[DisallowMultipleComponent]
public class AnimationCatalogueRunner : MonoBehaviour
{
    public enum TipoEntrada { Animacion, Ojos, Boca }

    [System.Serializable]
    public struct Entrada
    {
        public TipoEntrada tipo;
        public string nombre;
        public int capa;
        public string nombreCapa;
        public float duracion;
    }

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private new Camera camera;

    [Header("Catálogo (lo rellena la herramienta del Editor)")]
    [SerializeField] private List<Entrada> entradas = new List<Entrada>();

    [Header("Ritmo")]
    [Tooltip("Mínimo que se queda cada animación en pantalla, aunque sea cortísima.")]
    [SerializeField] private float duracionMinima = 2f;
    [Tooltip("Máximo, para que una animación larga no eternice la grabación.")]
    [SerializeField] private float duracionMaxima = 5f;
    [Tooltip("Respiro en blanco entre una y la siguiente, para que en el vídeo se vea el corte.")]
    [SerializeField] private float pausaEntreEntradas = 0.35f;
    [Tooltip("Lo que se queda cada pieza de cara en pantalla.")]
    [SerializeField] private float duracionCara = 2f;

    [Header("Cámara")]
    [Tooltip("A qué distancia se pone la cámara para el cuerpo entero.")]
    [SerializeField] private float distanciaCuerpo = 3.2f;
    [Tooltip("A qué distancia se pone para los primeros planos de cara. Ojo: estas cabezas miden " +
             "más de medio metro, así que por debajo de 1,2 la cámara se mete DENTRO de la cara.")]
    [SerializeField] private float distanciaCara = 1.45f;

    private readonly Dictionary<string, GameObject> _ojos  = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> _bocas = new Dictionary<string, GameObject>();

    private int   _indice;
    private bool  _pausado;
    private float _restante;
    private bool  _saltarAdelante;
    private bool  _saltarAtras;
    private bool  _repetir;
    private bool  _terminado;

    private Transform _cabeza;
    private bool _tieneIdleBase;
    private static readonly int IdleBaseHash = Animator.StringToHash("Idle01");
    private GUIStyle  _estiloGrande;
    private GUIStyle  _estiloMedio;
    private GUIStyle  _estiloPie;
    private Texture2D _fondo;

    public int Total => entradas != null ? entradas.Count : 0;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (camera == null)   camera   = Camera.main;

        CachearPiezasDeCara(transform);

        if (animator != null)
        {
            _cabeza = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            _tieneIdleBase = animator.HasState(0, IdleBaseHash);
        }
    }

    private void Start()
    {
        if (animator == null || entradas == null || entradas.Count == 0)
        {
            Debug.LogError("[Catálogo] No hay Animator o la lista de entradas está vacía. " +
                "Regenera la escena con El Sendero ▸ Personajes ▸ Catálogo de animaciones.", this);
            enabled = false;
            return;
        }

        // Todas las capas al máximo: si no, los gestos que viven en UpperBody no se ven, porque
        // esa capa arranca con peso 0 y es el juego quien se lo sube cuando toca.
        for (int i = 0; i < animator.layerCount; i++)
            animator.SetLayerWeight(i, 1f);

        Debug.Log($"[Catálogo] Arrancando: {entradas.Count} entradas. " +
                  "ESPACIO pausa · → siguiente · ← anterior · R repetir.", this);

        StartCoroutine(Co_Recorrer());
    }

    private void CachearPiezasDeCara(Transform raiz)
    {
        foreach (Transform hijo in raiz)
        {
            if (hijo.name.StartsWith("Eye")   && !_ojos.ContainsKey(hijo.name))  _ojos[hijo.name]  = hijo.gameObject;
            if (hijo.name.StartsWith("Mouth") && !_bocas.ContainsKey(hijo.name)) _bocas[hijo.name] = hijo.gameObject;
            CachearPiezasDeCara(hijo);
        }
    }

    private IEnumerator Co_Recorrer()
    {
        while (_indice < entradas.Count)
        {
            var entrada = entradas[_indice];

            ColocarCamara(entrada.tipo == TipoEntrada.Animacion);
            Aplicar(entrada);

            Debug.Log($"[Catálogo] {_indice + 1}/{entradas.Count} · {entrada.tipo} · '{entrada.nombre}'" +
                      (entrada.tipo == TipoEntrada.Animacion
                          ? $" (capa {entrada.nombreCapa}, {entrada.duracion:0.0}s)" : ""));

            float espera = entrada.tipo == TipoEntrada.Animacion
                ? Mathf.Clamp(entrada.duracion, duracionMinima, duracionMaxima)
                : duracionCara;

            _restante = espera;
            _saltarAdelante = _saltarAtras = _repetir = false;

            while (_restante > 0f && !_saltarAdelante && !_saltarAtras && !_repetir)
            {
                if (!_pausado) _restante -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (_saltarAtras)      _indice = Mathf.Max(0, _indice - 1);
            else if (!_repetir)    _indice++;

            if (pausaEntreEntradas > 0f && !_repetir)
                yield return new WaitForSecondsRealtime(pausaEntreEntradas);
        }

        _terminado = true;
        Debug.Log("[Catálogo] Terminado. Ya puedes parar el Play.", this);
    }

    private void Aplicar(Entrada entrada)
    {
        switch (entrada.tipo)
        {
            case TipoEntrada.Animacion:
                // Solo la capa de esta animación pesa. Con todas a 1, un gesto que se quedara
                // puesto en UpperBody taparía el torso de las animaciones de Base Layer y se
                // catalogarían mal — que es justo lo que este catálogo viene a evitar.
                for (int i = 1; i < animator.layerCount; i++)
                    animator.SetLayerWeight(i, i == entrada.capa ? 1f : 0f);

                // La Base Layer, cuando el gesto vive arriba, se queda en un idle quieto: así las
                // piernas no hacen nada raro por debajo y se ve solo lo que se está catalogando.
                if (entrada.capa != 0 && _tieneIdleBase)
                    animator.Play(IdleBaseHash, 0, 0f);

                animator.Play(Animator.StringToHash(entrada.nombre), entrada.capa, 0f);
                break;

            case TipoEntrada.Ojos:
                PonerCuerpoQuieto();
                ActivarSolo(_ojos, entrada.nombre);
                break;

            case TipoEntrada.Boca:
                PonerCuerpoQuieto();
                ActivarSolo(_bocas, entrada.nombre);
                break;
        }
    }

    /// En el repaso de caras lo único que debe moverse es la cara: el cuerpo se deja en un idle
    /// y todas las capas de gesto a cero.
    private void PonerCuerpoQuieto()
    {
        for (int i = 1; i < animator.layerCount; i++)
            animator.SetLayerWeight(i, 0f);

        if (_tieneIdleBase) animator.Play(IdleBaseHash, 0, 0f);
    }

    private static void ActivarSolo(Dictionary<string, GameObject> piezas, string nombre)
    {
        foreach (var par in piezas)
        {
            if (par.Value == null) continue;
            bool activa = par.Key == nombre;
            if (par.Value.activeSelf != activa) par.Value.SetActive(activa);
        }
    }

    private void ColocarCamara(bool cuerpoEntero)
    {
        if (camera == null) return;

        // En los primeros planos se apunta un poco por DEBAJO de la cabeza: si se apunta al hueso
        // Head, que en estos cuerpos está a la altura de los ojos, la boca se queda en el borde
        // inferior del cuadro o directamente fuera.
        Vector3 objetivo = cuerpoEntero || _cabeza == null
            ? transform.position + Vector3.up * 0.65f
            : _cabeza.position - Vector3.up * 0.12f;

        float distancia = cuerpoEntero ? distanciaCuerpo : distanciaCara;

        // DELANTE del personaje, que es donde está su cara. Ligeramente escorada y no de frente
        // plano, para que se aprecien los brazos y el perfil.
        Vector3 direccion = Quaternion.AngleAxis(20f, Vector3.up) * transform.forward;

        camera.transform.position = objetivo + direccion.normalized * distancia + Vector3.up * 0.1f;
        camera.transform.rotation = Quaternion.LookRotation(objetivo - camera.transform.position);
        camera.fieldOfView = cuerpoEntero ? 45f : 38f;
    }

    private void Update()
    {
        // Input System nuevo: este proyecto tiene desactivada la clase Input de siempre, y usarla
        // lanza excepción en cuanto se toca una tecla.
        var teclado = Keyboard.current;
        if (teclado == null) return;

        if (teclado.spaceKey.wasPressedThisFrame)      _pausado = !_pausado;
        if (teclado.rightArrowKey.wasPressedThisFrame) _saltarAdelante = true;
        if (teclado.leftArrowKey.wasPressedThisFrame)  _saltarAtras = true;
        if (teclado.rKey.wasPressedThisFrame)          _repetir = true;
    }

    private void OnGUI()
    {
        PrepararEstilos();

        if (_terminado)
        {
            // A pantalla completa y sin ambigüedad: en la grabación tiene que quedar claro dónde
            // acaba, sin depender de lo claro que sea el fondo de la escena.
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _fondo);
            GUI.Label(new Rect(40, Screen.height * 0.4f, Screen.width - 80, 70),
                "CATÁLOGO TERMINADO", _estiloGrande);
            GUI.Label(new Rect(40, Screen.height * 0.4f + 60, Screen.width - 80, 40),
                $"Las {entradas.Count} entradas están hechas. Ya puedes parar la grabación y el Play.",
                _estiloMedio);
            return;
        }

        if (entradas == null || _indice >= entradas.Count) return;
        var entrada = entradas[_indice];

        // Caja de fondo para que el texto se lea sobre cualquier escenario en el vídeo.
        GUI.DrawTexture(new Rect(0, 0, Screen.width, 150), _fondo);

        GUI.Label(new Rect(40, 18, Screen.width - 80, 40),
            $"{_indice + 1} / {entradas.Count}   ·   {Etiqueta(entrada.tipo)}", _estiloMedio);

        GUI.Label(new Rect(40, 52, Screen.width - 80, 60), entrada.nombre, _estiloGrande);

        string detalle = entrada.tipo == TipoEntrada.Animacion
            ? $"capa {entrada.nombreCapa}  ·  {entrada.duracion:0.0} s" + (_pausado ? "   ·   PAUSA" : "")
            : (_pausado ? "PAUSA" : "");
        GUI.Label(new Rect(40, 108, Screen.width - 80, 30), detalle, _estiloMedio);

        GUI.Label(new Rect(40, Screen.height - 42, Screen.width - 80, 30),
            "ESPACIO pausa   ·   →  siguiente   ·   ←  anterior   ·   R  repetir", _estiloPie);
    }

    private static string Etiqueta(TipoEntrada tipo) => tipo switch
    {
        TipoEntrada.Ojos => "PIEZA DE OJOS",
        TipoEntrada.Boca => "PIEZA DE BOCA",
        _                => "ANIMACIÓN",
    };

    private void PrepararEstilos()
    {
        if (_estiloGrande != null) return;

        _fondo = new Texture2D(1, 1);
        _fondo.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
        _fondo.Apply();

        _estiloGrande = new GUIStyle(GUI.skin.label)
        {
            fontSize = 44, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        _estiloMedio = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            normal = { textColor = new Color(1f, 0.85f, 0.4f) },
        };
        _estiloPie = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = new Color(1f, 1f, 1f, 0.7f) },
        };
    }
}
