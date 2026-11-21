using System;
using System.Collections;
using UnityEngine;

/// Niebla de suelo con luz debajo durante una cinemática, alrededor de un personaje (y siguiéndole)
/// o de una marca. No espera: aparece mientras la escena sigue. Se va a los «segundos» que se le
/// digan o, con 0, al terminar la cinemática (también si se salta). En el guion:
/// <c>efecto NieblaDeSueloBeat actor=mago seguir=si radio=6</c>. Ver INC-1341.
[Serializable]
public class NieblaDeSueloBeat : SequenceBeat
{
    [Tooltip("Personaje alrededor del que sale (alias del guion). Vacío: usa la marca.")]
    public string actor;

    [Tooltip("Marca del escenario alrededor de la que sale, si no hay personaje.")]
    public string marca;

    [Tooltip("La niebla va con el personaje por el suelo.")]
    public bool seguir = true;

    [Min(0.5f)] public float radio = 6f;
    [Tooltip("Color de la niebla.")]
    public Color color = new Color(0.62f, 0.55f, 0.85f);
    [Range(0f, 1f)] public float opacidad = 0.3f;
    [Tooltip("Color de la luz que brilla debajo. Alfa 0 = sin luz.")]
    public Color colorDeLuz = new Color(0.65f, 0.45f, 1f, 1f);
    [Min(0f)] public float intensidadDeLuz = 3f;

    [Tooltip("Segundos que tarda en aparecer.")]
    [Min(0f)] public float entrada = 2.5f;
    [Tooltip("Segundos que dura entera; 0 = hasta el final de la cinemática.")]
    [Min(0f)] public float segundos = 0f;
    [Tooltip("Segundos que tarda en irse.")]
    [Min(0f)] public float salida = 2f;

    public override string Describe() =>
        $"Niebla de suelo en {(string.IsNullOrEmpty(actor) ? marca : actor)} ({radio:0.#} m{(segundos > 0f ? $", {segundos:0.#} s" : "")})";

    public override IEnumerator Run(SequenceContext ctx)
    {
        Transform objetivo = null;
        if (!string.IsNullOrEmpty(actor)) objetivo = ctx?.GetActor(actor)?.Transform;
        if (objetivo == null && !string.IsNullOrEmpty(marca)) objetivo = ctx?.Stage?.GetMark(marca);
        if (objetivo == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[NieblaDeSueloBeat] No encuentro «{actor}{marca}»; sin niebla.");
#endif
            yield break;
        }

        var niebla = NieblaDeSuelo.Crear(objetivo.position, n =>
        {
            n.radio = radio;
            n.color = color;
            n.opacidad = opacidad;
            n.colorDeLuz = colorDeLuz;
            n.intensidadDeLuz = intensidadDeLuz;
            n.seguir = seguir && !string.IsNullOrEmpty(actor) ? objetivo : null;
        });
        niebla.Encender(entrada);

        // Al cerrar la cinemática (acabe, se salte o falle) se va sola y se borra.
        ctx.Player?.RegisterCleanup(() => { if (niebla != null) niebla.Apagar(salida, true); });
        if (segundos > 0f && ctx.Player != null) ctx.Player.StartCoroutine(IrseA(niebla, entrada + segundos));
    }

    private IEnumerator IrseA(NieblaDeSuelo niebla, float tras)
    {
        yield return new WaitForSeconds(tras);
        if (niebla != null) niebla.Apagar(salida, true);
    }
}
