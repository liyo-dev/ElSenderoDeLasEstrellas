using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eleva el modelo visual de un personaje humanoide (desde la cadera) sin mover su cápsula ni su
/// física: conjurar flotando, levitar en una cinemática… Varios sistemas pueden pedir altura a la
/// vez, cada uno con su clave (<c>quien</c>); gana la mayor. Mientras está elevado, se balancea.
/// <para>El desplazamiento se suma a la cadera en <c>LateUpdate</c>, después del Animator. Si en un
/// fotograma el Animator no reescribe la cadera (p. ej. Animate Physics sin FixedUpdate), primero se
/// quita lo aplicado en el anterior, así que nunca se acumula y al soltar vuelve a su sitio exacto.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class ElevacionVisual : MonoBehaviour
{
    [Tooltip("Animator humanoide del personaje. Vacío: el de este objeto o el de su padre.")]
    [SerializeField] private Animator animator;
    [Tooltip("Amplitud del balanceo vertical mientras está elevado, en metros.")]
    [SerializeField, Min(0f)] private float amplitudBalanceo = 0.04f;
    [Tooltip("Ciclos de balanceo por segundo.")]
    [SerializeField, Min(0f)] private float frecuenciaBalanceo = 0.8f;
    [Tooltip("Rebote al terminar de subir (0 = sin rebote; 1,7 = rebote marcado).")]
    [SerializeField, Min(0f)] private float rebote = 1.7f;

    private struct Peticion { public object quien; public float altura; }

    private const float Epsilon = 0.0001f;

    private readonly List<Peticion> _peticiones = new List<Peticion>(4);
    private Transform _cadera;

    // Transición de la altura base en curso.
    private float _desde, _hasta, _duracion, _t;
    private bool _subiendo, _tiempoReal;
    private float _alturaBase;

    // Lo aplicado en el último LateUpdate.
    private float _alturaAplicada;
    private Vector3 _ultimaEscrita;
    private float _faseBalanceo;

    /// <summary>Hay alguna petición activa o el modelo aún no ha vuelto al suelo.</summary>
    public bool EstaElevado => _peticiones.Count > 0 || Mathf.Abs(_alturaAplicada) > Epsilon;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
    }

    /// <summary>
    /// Pide elevar el modelo <paramref name="altura"/> metros, subiendo en <paramref name="segundos"/>.
    /// Repetir con el mismo <paramref name="quien"/> actualiza su altura.
    /// </summary>
    public void Elevar(object quien, float altura, float segundos, bool tiempoReal = false)
    {
        if (quien == null) return;
        if (Mathf.Abs(_alturaAplicada) <= Epsilon) ResolverCadera();

        int i = Buscar(quien);
        var p = new Peticion { quien = quien, altura = Mathf.Max(0f, altura) };
        if (i >= 0) _peticiones[i] = p; else _peticiones.Add(p);
        IrA(AlturaPedida(), segundos, tiempoReal);
    }

    /// <summary>Retira la petición de <paramref name="quien"/>; baja en <paramref name="segundos"/>.</summary>
    public void Soltar(object quien, float segundos, bool tiempoReal = false)
    {
        int i = Buscar(quien);
        if (i < 0) return;
        _peticiones.RemoveAt(i);
        IrA(AlturaPedida(), segundos, tiempoReal);
    }

    void LateUpdate()
    {
        if (_cadera == null) return;

        float dt = _tiempoReal ? Time.unscaledDeltaTime : Time.deltaTime;
        _t += dt;
        float k = _duracion <= 0f ? 1f : Mathf.Clamp01(_t / _duracion);
        float e = _subiendo ? EaseOutBack(k, rebote) : k * k;
        _alturaBase = Mathf.LerpUnclamped(_desde, _hasta, e);

        // El balanceo crece con la altura, para que entre y salga sin saltos.
        float balanceo = 0f;
        if (amplitudBalanceo > 0f && _alturaBase > Epsilon)
        {
            _faseBalanceo += Time.deltaTime * frecuenciaBalanceo * Mathf.PI * 2f;
            if (_faseBalanceo > Mathf.PI * 2f) _faseBalanceo -= Mathf.PI * 2f;
            balanceo = Mathf.Sin(_faseBalanceo) * amplitudBalanceo * Mathf.Clamp01(_alturaBase / 0.15f);
        }
        else _faseBalanceo = 0f;

        float altura = _alturaBase + balanceo;
        if (Mathf.Abs(altura) <= Epsilon && Mathf.Abs(_alturaAplicada) <= Epsilon) return;

        Aplicar(altura);
    }

    void OnDisable()
    {
        _peticiones.Clear();
        if (_cadera != null && Mathf.Abs(_alturaAplicada) > Epsilon) Aplicar(0f);
        _alturaBase = _desde = _hasta = 0f;
        _duracion = _t = 0f;
        _faseBalanceo = 0f;
    }

    private void Aplicar(float altura)
    {
        Vector3 p = _cadera.position;
        // El Animator no la ha reescrito este fotograma: quitar lo aplicado antes de sumar de nuevo.
        if (Mathf.Abs(_alturaAplicada) > Epsilon && (p - _ultimaEscrita).sqrMagnitude < 1e-10f)
            p.y -= _alturaAplicada;
        p.y += altura;
        _cadera.position = p;
        _ultimaEscrita = p;
        _alturaAplicada = Mathf.Abs(altura) <= Epsilon ? 0f : altura;
    }

    private void IrA(float altura, float segundos, bool tiempoReal)
    {
        if (Mathf.Approximately(altura, _hasta) && _t < _duracion) return;
        _desde = _alturaBase;
        _hasta = altura;
        _duracion = Mathf.Max(0f, segundos);
        _t = 0f;
        _subiendo = _hasta > _desde;
        _tiempoReal = tiempoReal;
    }

    private void ResolverCadera()
    {
        if (!animator) animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
        _cadera = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
    }

    private float AlturaPedida()
    {
        float max = 0f;
        for (int i = 0; i < _peticiones.Count; i++)
            if (_peticiones[i].altura > max) max = _peticiones[i].altura;
        return max;
    }

    private int Buscar(object quien)
    {
        for (int i = 0; i < _peticiones.Count; i++)
            if (ReferenceEquals(_peticiones[i].quien, quien)) return i;
        return -1;
    }

    private static float EaseOutBack(float k, float s)
    {
        float c3 = s + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + s * x * x;
    }
}
