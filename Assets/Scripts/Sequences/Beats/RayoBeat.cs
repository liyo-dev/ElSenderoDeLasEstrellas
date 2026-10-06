using System;
using System.Collections;
using UnityEngine;

/// Coloca los rayos del clima donde los necesita el plano.
[Serializable]
public class RayoBeat : SequenceBeat
{
    [Min(1)] public int cantidad = 1;
    [Min(0f)] public float intervalo = 1f;
    public string marca;
    public bool alFondoDelPlano = true;
    [Tooltip("Vacío usa el trueno del ciclo; conTrueno permite silenciarlo.")]
    public string trueno;
    public bool conTrueno = true;
    [Range(0f, 1f)] public float volumen = 1f;
    public bool destello = true;
    public override string Describe() => $"Rayo: {cantidad} × {(alFondoDelPlano ? "fondo del plano" : marca)}";
    public override IEnumerator Run(SequenceContext ctx)
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[RayoBeat] No hay DayNightCycle; se omite el rayo.");
#endif
            yield break;
        }
        for (int i = 0; i < Mathf.Max(1, cantidad); i++)
        {
            if (ciclo == null) yield break;
            Vector3 punto;
            if (!string.IsNullOrWhiteSpace(marca))
            {
                var objetivo = ctx.Stage?.GetMark(marca);
                if (objetivo == null) yield break;
                punto = objetivo.position;
            }
            else
            {
                var camara = SolYLunaEnElCielo.CamaraActual() ?? ctx.Player?.CachedCamera;
                if (!alFondoDelPlano || camara == null) yield break;
                float minimo = 40f;
                if (ctx.TryGetPreviousShot(out var plano))
                    minimo = Mathf.Max(minimo, camara.transform.InverseTransformPoint(plano.lookAt).z + 10f);
                float maximo = Mathf.Min(90f, camara.farClipPlane * 0.85f);
                if (minimo > maximo)
                {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    Debug.LogWarning("[RayoBeat] El plano no permite un rayo detrás del sujeto dentro de 40–90 m; usa una marca.");
#endif
                    yield break;
                }
                float distancia = UnityEngine.Random.Range(minimo, maximo);
                punto = camara.ViewportToWorldPoint(new Vector3(UnityEngine.Random.value < 0.5f ? 0.22f : 0.78f, 0.38f, distancia));
            }
            ciclo.LanzarRayo(punto, conTrueno, trueno, volumen, destello);
            if (i + 1 < Mathf.Max(1, cantidad)) yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, intervalo));
        }
    }
}
