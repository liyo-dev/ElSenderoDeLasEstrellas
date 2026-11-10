using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una hoja que gira sobre su bisagra: contraventana, puerta, portillo. El objeto con este
/// componente es la bisagra (su pivote está en el canto que gira) y gira sobre su eje Y local.
/// Cerrada, la hoja tiene el giro que trae de la escena; abierta, ese giro más <see cref="anguloAbierta"/>.
///
/// Las hojas se apuntan a un registro estático por <see cref="grupo"/>, así una secuencia puede
/// abrir o cerrar todas las de un pueblo o una sola por su nombre (<c>HojasConBisagraBeat</c>).
/// Fuera de una secuencia no hace nada por sí misma: no tiene Update.
/// </summary>
[DisallowMultipleComponent]
public class HojaConBisagra : MonoBehaviour
{
    [Tooltip("Grupo al que pertenece (p. ej. «Pueblo del prólogo»). Una secuencia puede mover todas las de un grupo.")]
    public string grupo;

    [Tooltip("Grados que gira sobre su eje Y local al abrirse, desde como está cerrada. El signo elige hacia qué lado.")]
    public float anguloAbierta = 105f;

    [Tooltip("Cómo empieza la escena: abierta o cerrada.")]
    public bool abiertaAlEmpezar = true;

    private static readonly List<HojaConBisagra> s_todas = new();
    private Quaternion _giroCerrada;
    private bool _medida;
    private Coroutine _mov;

    /// <summary>Todas las hojas cargadas ahora mismo.</summary>
    public static IReadOnlyList<HojaConBisagra> Todas => s_todas;

    /// <summary>¿Está abierta (o abriéndose)?</summary>
    public bool Abierta { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_todas.Clear();
#endif

    void Awake()
    {
        Medir();
        Poner(abiertaAlEmpezar);
    }

    void OnEnable() { if (!s_todas.Contains(this)) s_todas.Add(this); }

    void OnDisable()
    {
        s_todas.Remove(this);
        if (_mov != null) { StopCoroutine(_mov); _mov = null; Poner(Abierta); }
    }

    private void Medir()
    {
        if (_medida) return;
        _giroCerrada = transform.localRotation;
        _medida = true;
    }

    private Quaternion GiroDe(bool abierta) => abierta ? _giroCerrada * Quaternion.Euler(0f, anguloAbierta, 0f) : _giroCerrada;

    /// <summary>La deja abierta o cerrada al instante.</summary>
    public void Poner(bool abierta)
    {
        Medir();
        if (_mov != null) { StopCoroutine(_mov); _mov = null; }
        Abierta = abierta;
        transform.localRotation = GiroDe(abierta);
    }

    /// <summary>
    /// La abre o la cierra en <paramref name="segundos"/>, empezando dentro de <paramref name="retraso"/>.
    /// Con <paramref name="golpe"/>, el viento la cierra de un portazo: acelera hasta el final y rebota un poco.
    /// </summary>
    public void Mover(bool abrir, float segundos, float retraso = 0f, bool golpe = false)
    {
        Medir();
        if (!isActiveAndEnabled || (segundos <= 0f && retraso <= 0f)) { Poner(abrir); return; }
        if (_mov != null) StopCoroutine(_mov);
        Abierta = abrir;
        _mov = StartCoroutine(Moviendo(GiroDe(abrir), segundos, retraso, golpe));
    }

    private IEnumerator Moviendo(Quaternion hasta, float segundos, float retraso, bool golpe)
    {
        if (retraso > 0f) yield return new WaitForSeconds(retraso);
        Quaternion desde = transform.localRotation;
        float dura = Mathf.Max(0.01f, segundos);
        for (float t = 0f; t < dura; t += Time.deltaTime)
        {
            float k = t / dura;
            // Portazo: entra acelerando (cuadrática) y rebota al tocar el marco. Normal: suave.
            float f = golpe ? Rebote(k) : k * k * (3f - 2f * k);
            transform.localRotation = Quaternion.SlerpUnclamped(desde, hasta, f);
            yield return null;
        }
        transform.localRotation = hasta;
        _mov = null;
    }

    private static float Rebote(float k)
    {
        const float choque = 0.72f;
        if (k < choque) { float a = k / choque; return a * a; }
        float r = (k - choque) / (1f - choque);
        return 1f - 0.12f * Mathf.Sin(r * Mathf.PI) * (1f - r);
    }
}
