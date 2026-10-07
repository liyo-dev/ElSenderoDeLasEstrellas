using System;
using UnityEngine;

/// <summary>Qué acción se avisa.</summary>
public enum TipoDeAviso
{
    Ninguno,
    /// Defensa (B): devolver un proyectil o desviar un golpe.
    Defensa,
    /// Salto (A) en el aire tras un derribo: caer de pie.
    Recuperacion,
    /// Golpe que no se para: apartarse.
    NoBloqueable,
}

/// <summary>Cuánto falta.</summary>
public enum EstadoDeAviso
{
    Nada,
    /// Viene: el icono aparece tenue.
    Aviso,
    /// Es el momento: pulsar ahora funciona.
    Ahora,
}

/// <summary>
/// Avisa al jugador del momento justo para una acción de combate (como en Hogwarts Legacy):
/// <list type="bullet">
/// <item>Defensa: un proyectil hostil de <see cref="AmenazasAlJugador"/> va a entrar en el radio del
/// contraataque, o un golpe anunciado va a llegar. «Ahora» cuando pulsar la B ya lo atrapa
/// (dentro de su ventana). Solo con la defensa disponible.</item>
/// <item>Recuperación: durante el vuelo tras un golpe, mientras saltar haría la voltereta.</item>
/// <item>No bloqueable: golpes anunciados que hay que esquivar.</item>
/// </list>
/// Lo pinta <see cref="Sendero.UI.AvisoDeAmenazaUI"/> (lo crea este componente desde Resources) y
/// lo escucha el HUD de botones. Se apaga en Ajustes (<see cref="PlayerSettings.AvisosDeCombate"/>),
/// en cinemáticas y con el HUD oculto. Ver INC-666.
/// </summary>
[DisallowMultipleComponent]
public class AvisoDeAmenazas : MonoBehaviour
{
    [Header("Cuándo avisar")]
    [Tooltip("Segundos antes del momento justo en los que el icono empieza a verse (tenue).")]
    [SerializeField, Min(0.1f)] private float anticipacion = 0.6f;
    [Tooltip("Metros máximos entre el jugador y la línea de vuelo de un proyectil para considerar que va a por él.")]
    [SerializeField, Min(0.1f)] private float margenDeAcierto = 1.2f;
    [Tooltip("Altura (m) sobre los pies del punto al que apuntan los proyectiles (el pecho).")]
    [SerializeField, Min(0f)] private float alturaDelPecho = 1f;

    [Header("Icono")]
    [Tooltip("Prefab de la UI del aviso en Resources (sin extensión). Vacío = sin icono (el HUD sigue latiendo).")]
    [SerializeField] private string rutaDelIcono = "UI/AvisoDeAmenaza";

    private PlayerShieldController _escudo;
    private PlayerActionManager _acciones;
    private AerialKnockbackReceiver _lanzado;
    private Animator _animator;

    /// <summary>Lo que se avisa ahora mismo.</summary>
    public TipoDeAviso Tipo { get; private set; }
    public EstadoDeAviso Estado { get; private set; }

    /// <summary>Ha cambiado el aviso (tipo o estado).</summary>
    public event Action<TipoDeAviso, EstadoDeAviso> OnAviso;

    /// <summary>Punto sobre el que se dibuja el icono (la cabeza si es humanoide).</summary>
    public Transform Cabeza { get; private set; }

    void Awake()
    {
        _escudo = GetComponentInParent<PlayerShieldController>();
        _acciones = GetComponentInParent<PlayerActionManager>();
        TryGetComponent(out _lanzado);
        _animator = GetComponentInChildren<Animator>();
        Cabeza = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (Cabeza == null) Cabeza = transform;
    }

    void Start()
    {
        if (string.IsNullOrEmpty(rutaDelIcono)) return;
        var prefab = Resources.Load<Sendero.UI.AvisoDeAmenazaUI>(rutaDelIcono);
        if (prefab == null) return;
        var ui = Instantiate(prefab);
        ui.name = prefab.name;
        // Vive en la misma escena que el jugador (se destruye sola si el jugador desaparece).
        if (gameObject.scene.name == "DontDestroyOnLoad") DontDestroyOnLoad(ui.gameObject);
        else if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ui.gameObject, gameObject.scene);
        ui.Seguir(this);
    }

    void OnDisable() => Cambiar(TipoDeAviso.Ninguno, EstadoDeAviso.Nada);

    void Update()
    {
        // El receptor de lanzamientos puede añadirse en juego (choque de hechizos).
        if (_lanzado == null && (Time.frameCount & 15) == 0) TryGetComponent(out _lanzado);

        if (!PuedeAvisar())
        {
            Cambiar(TipoDeAviso.Ninguno, EstadoDeAviso.Nada);
            return;
        }

        if (_lanzado != null && _lanzado.VentanaDeRecuperacionAbierta)
        {
            Cambiar(TipoDeAviso.Recuperacion, EstadoDeAviso.Ahora);
            return;
        }

        float defensa = SegundosHastaElMomentoDeDefensa(out float esquiva);
        float ventana = _escudo != null ? _escudo.VentanaDeContraataque : 0.2f;

        if (defensa <= ventana) Cambiar(TipoDeAviso.Defensa, EstadoDeAviso.Ahora);
        else if (esquiva <= anticipacion) Cambiar(TipoDeAviso.NoBloqueable, EstadoDeAviso.Aviso);
        else if (defensa <= ventana + anticipacion) Cambiar(TipoDeAviso.Defensa, EstadoDeAviso.Aviso);
        else Cambiar(TipoDeAviso.Ninguno, EstadoDeAviso.Nada);
    }

    private bool PuedeAvisar()
    {
        if (!PlayerSettings.AvisosDeCombate) return false;
        if (_acciones != null && _acciones.IsInMode(ActionMode.Cinematic)) return false;
        var hud = Sendero.UI.PlayerHUDV2.Instance;
        return hud == null || hud.IsVisible;
    }

    /// Segundos hasta el momento justo de la defensa (la amenaza devolvible más cercana) y, en
    /// 'esquiva', hasta el golpe no bloqueable más cercano. Infinito si no hay.
    private float SegundosHastaElMomentoDeDefensa(out float esquiva)
    {
        esquiva = float.PositiveInfinity;
        float defensa = float.PositiveInfinity;
        bool puedeDefender = _escudo != null && _escudo.isActiveAndEnabled && (_acciones == null || _acciones.AllowShield);
        float radio = _escudo != null ? _escudo.RadioDeContraataque : 2.6f;
        Vector3 pecho = transform.position + Vector3.up * alturaDelPecho;

        if (puedeDefender)
        {
            var proyectiles = AmenazasAlJugador.Proyectiles;
            for (int i = 0; i < proyectiles.Count; i++)
            {
                var p = proyectiles[i];
                if (p == null || p.TipoDeAmenaza != TipoDeAmenaza.Devolvible) continue;
                float t = SegundosHastaElRadio(p.PosicionDeAmenaza, p.VelocidadDeAmenaza, pecho, radio);
                if (t < defensa) defensa = t;
            }
        }

        AmenazasAlJugador.LimpiarCaducados();
        var anuncios = AmenazasAlJugador.Anuncios;
        float ahora = Time.time;
        for (int i = 0; i < anuncios.Count; i++)
        {
            float t = Mathf.Max(0f, anuncios[i].impactoEn - ahora);
            if (anuncios[i].tipo == TipoDeAmenaza.NoBloqueable) { if (t < esquiva) esquiva = t; }
            else if (puedeDefender && t < defensa) defensa = t;
        }
        return defensa;
    }

    /// Segundos hasta que algo que vuela desde 'pos' a 'vel' entre en la esfera de 'radio' alrededor
    /// de 'centro'. Infinito si no va hacia ella o pasa lejos.
    private float SegundosHastaElRadio(Vector3 pos, Vector3 vel, Vector3 centro, float radio)
    {
        float v = vel.magnitude;
        if (v < 0.5f) return float.PositiveInfinity;
        Vector3 hasta = centro - pos;
        Vector3 dir = vel / v;
        float avance = Vector3.Dot(hasta, dir);
        if (avance <= 0f) return float.PositiveInfinity;                       // ya ha pasado o se aleja
        float lateral = (hasta - dir * avance).magnitude;
        if (lateral > margenDeAcierto) return float.PositiveInfinity;           // no va a por él
        if (hasta.sqrMagnitude <= radio * radio) return 0f;                    // ya está dentro
        return Mathf.Max(0f, avance - radio) / v;
    }

    private void Cambiar(TipoDeAviso tipo, EstadoDeAviso estado)
    {
        if (tipo == TipoDeAviso.Ninguno) estado = EstadoDeAviso.Nada;
        if (tipo == Tipo && estado == Estado) return;
        Tipo = tipo;
        Estado = estado;
        OnAviso?.Invoke(tipo, estado);
    }
}
