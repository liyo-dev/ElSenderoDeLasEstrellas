using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Personaje colocado en una portada del menú principal (ver <see cref="PortadaDelMenu"/>): se
/// queda en una pose en bucle (sentado, de pie en cubierta…), hace de vez en cuando algún gesto
/// (señalar, reír, hablar) y, al pulsar Nueva Partida o Continuar, puede reproducir una animación
/// de salida (levantarse del banco).
/// Los estados se piden por nombre a los Animators del propio personaje que los tengan (el rig
/// modular lleva varios); si otro sistema reinicia el Animator (al aplicar la apariencia, por
/// ejemplo), la pose se vuelve a poner.
/// </summary>
[DisallowMultipleComponent]
public class ActorDePortada : MonoBehaviour
{
    [Tooltip("Estado del Animator que se queda en bucle mientras se ve la portada (p. ej. SitMedium_Loop). Vacío = el reposo del controlador.")]
    [SerializeField] private string estadoEnBucle = "";

    [Tooltip("Estado que se reproduce al pulsar Nueva Partida o Continuar (p. ej. SitMedium_Exit). Vacío = no hace nada al salir.")]
    [SerializeField] private string estadoAlSalir = "";

    [Tooltip("Fundido entre estados, en segundos.")]
    [Min(0f)] [SerializeField] private float fundido = 0.2f;

    [Header("Gestos de vez en cuando")]
    [Tooltip("Estados que el personaje hace de vez en cuando, al azar (p. ej. FoundSomething_NoWeapon, Laugh01, Talk01). Vacío = no hace gestos.")]
    [SerializeField] private string[] gestos = new string[0];
    [Tooltip("Segundos entre gesto y gesto (mínimo y máximo).")]
    [SerializeField] private Vector2 cadaCuanto = new Vector2(4f, 9f);

    struct Pista { public Animator animator; public int capaBucle; public int capaSalida; public DecoradoDeMenu.ParametrosDeSuelo suelo; }

    struct Gesto { public int hash; public int capa; }

    readonly List<Pista> _pistas = new List<Pista>();
    readonly List<Gesto> _gestos = new List<Gesto>();
    int _hashBucle;
    bool _saliendo;

    // Gesto en curso, sobre la pista del cuerpo.
    int _cuerpo = -1;
    Gesto _gestoActual;
    bool _haciendoGesto;
    int _estadoAntesDelGesto;
    float _pesoAntesDelGesto;
    float _proximoGesto;

    void Awake()
    {
        DecoradoDeMenu.ApagarSistemasDeJuego(gameObject);
        _hashBucle = Animator.StringToHash(estadoEnBucle ?? "");
        RecogerAnimators();
    }

    void RecogerAnimators()
    {
        _pistas.Clear();
        foreach (var anim in GetComponentsInChildren<Animator>(true))
        {
            if (anim == null || anim.runtimeAnimatorController == null) continue;
            int capaBucle = DecoradoDeMenu.CapaDelEstado(anim, estadoEnBucle);
            int capaSalida = DecoradoDeMenu.CapaDelEstado(anim, estadoAlSalir);
            if (capaBucle < 0 && capaSalida < 0 && !string.IsNullOrEmpty(estadoEnBucle)) continue;
            // La pose no desplaza al personaje: se queda donde lo colocó la portada.
            anim.applyRootMotion = false;
            _pistas.Add(new Pista { animator = anim, capaBucle = capaBucle, capaSalida = capaSalida,
                                  suelo = DecoradoDeMenu.LeerParametrosDeSuelo(anim) });
        }

        // Los gestos van en el primer Animator que tenga alguno (el del cuerpo).
        _gestos.Clear();
        _cuerpo = -1;
        if (gestos == null) return;
        for (int i = 0; i < _pistas.Count && _cuerpo < 0; i++)
        {
            foreach (var gesto in gestos)
            {
                int capa = DecoradoDeMenu.CapaDelEstado(_pistas[i].animator, gesto);
                if (capa >= 0) _gestos.Add(new Gesto { hash = Animator.StringToHash(gesto), capa = capa });
            }
            if (_gestos.Count > 0) _cuerpo = i;
        }
    }

    void OnEnable()
    {
        _saliendo = false;
        _haciendoGesto = false;
        _proximoGesto = Time.time + Random.Range(cadaCuanto.x * 0.3f, cadaCuanto.y);
        PonerPose();
    }

    void PonerPose()
    {
        foreach (var p in _pistas)
        {
            if (!p.animator.isActiveAndEnabled) continue;
            DecoradoDeMenu.ForzarEnSuelo(p.animator, p.suelo);
            if (p.capaBucle >= 0) p.animator.Play(_hashBucle, p.capaBucle, Random.value);
        }
    }

    void Update()
    {
        if (_saliendo || _cuerpo < 0) return;
        var cuerpo = _pistas[_cuerpo].animator;
        if (!cuerpo.isActiveAndEnabled) return;

        if (_haciendoGesto)
        {
            int capa = _gestoActual.capa;
            if (cuerpo.IsInTransition(capa)) return;
            var info = cuerpo.GetCurrentAnimatorStateInfo(capa);
            bool sigue = info.shortNameHash == _gestoActual.hash;
            if (sigue && info.normalizedTime < 1f) return;
            // Acabado: si el controlador no lo ha devuelto solo a su estado, se devuelve aquí.
            if (sigue) cuerpo.CrossFadeInFixedTime(_estadoAntesDelGesto, fundido, capa);
            if (capa > 0) cuerpo.SetLayerWeight(capa, _pesoAntesDelGesto);
            _haciendoGesto = false;
            _proximoGesto = Time.time + Random.Range(cadaCuanto.x, cadaCuanto.y);
            return;
        }

        if (Time.time < _proximoGesto) return;
        _gestoActual = _gestos[Random.Range(0, _gestos.Count)];
        _estadoAntesDelGesto = cuerpo.GetCurrentAnimatorStateInfo(_gestoActual.capa).shortNameHash;
        _pesoAntesDelGesto = cuerpo.GetLayerWeight(_gestoActual.capa);
        if (_gestoActual.capa > 0) cuerpo.SetLayerWeight(_gestoActual.capa, 1f);
        cuerpo.CrossFadeInFixedTime(_gestoActual.hash, fundido, _gestoActual.capa);
        _haciendoGesto = true;
    }

    void LateUpdate()
    {
        if (_saliendo || string.IsNullOrEmpty(estadoEnBucle)) return;
        for (int i = 0; i < _pistas.Count; i++)
        {
            var p = _pistas[i];
            if (p.capaBucle < 0 || !p.animator.isActiveAndEnabled) continue;
            // Un gesto en la misma capa que la pose no se pisa.
            if (_haciendoGesto && i == _cuerpo && _gestoActual.capa == p.capaBucle) continue;
            if (p.animator.IsInTransition(p.capaBucle)) continue;
            if (p.animator.GetCurrentAnimatorStateInfo(p.capaBucle).shortNameHash == _hashBucle) continue;
            DecoradoDeMenu.ForzarEnSuelo(p.animator, p.suelo);
            p.animator.Play(_hashBucle, p.capaBucle, 0f);
        }
    }

    /// <summary>Reproduce la animación de salida. Devuelve false si no tiene.</summary>
    public bool Salir()
    {
        _haciendoGesto = false;
        bool alguno = false;
        foreach (var p in _pistas)
        {
            if (p.capaSalida < 0 || !p.animator.isActiveAndEnabled) continue;
            p.animator.CrossFadeInFixedTime(estadoAlSalir, fundido, p.capaSalida);
            alguno = true;
        }
        _saliendo = alguno;
        return alguno;
    }
}
