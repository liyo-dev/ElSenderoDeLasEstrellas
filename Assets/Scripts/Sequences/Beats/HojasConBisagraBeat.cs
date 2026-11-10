using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abre o cierra hojas con bisagra (<see cref="HojaConBisagra"/>): todas las de un grupo, o solo
/// las que se llaman como <see cref="nombre"/> (la contraventana de un vecino, una puerta).
/// Con <see cref="escalonado"/>, cada hoja empieza en un momento distinto dentro de ese margen,
/// para que un pueblo entero no se cierre a la vez como un solo mecanismo.
/// </summary>
[Serializable]
public class HojasConBisagraBeat : SequenceBeat
{
    [Tooltip("Grupo de hojas (el campo «grupo» de cada HojaConBisagra). Vacío = cualquiera.")]
    public string grupo;

    [Tooltip("Solo las hojas cuyo objeto empieza por este nombre (p. ej. «Mirador del vecino»). Vacío = todas las del grupo.")]
    public string nombre;

    [Tooltip("true = abrir; false = cerrar.")]
    public bool abrir = true;

    [Tooltip("Lo que tarda cada hoja en girar.")]
    [Min(0f)] public float segundos = 0.6f;

    [Tooltip("Margen, en segundos, en el que van empezando las hojas (0 = todas a la vez). El orden es fijo para cada hoja.")]
    [Min(0f)] public float escalonado = 0f;

    [Tooltip("Portazo: la hoja acelera hasta el marco y rebota (viento, miedo).")]
    public bool golpe = false;

    public override string Describe()
        => $"Hojas: {(abrir ? "abrir" : "cerrar")} {(string.IsNullOrEmpty(nombre) ? (string.IsNullOrEmpty(grupo) ? "todas" : grupo) : nombre)}"
           + (escalonado > 0f ? $" en {escalonado:0.#} s" : "");

    public override bool SoloColoca => segundos <= 0.001f && escalonado <= 0.001f;
    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var hojas = Elegidas();
        for (int i = 0; i < hojas.Count; i++)
        {
            var h = hojas[i];
            // Retraso fijo por hoja (sale de su nombre): repetir la secuencia da el mismo orden.
            float retraso = escalonado > 0f ? escalonado * Fraccion(h.name, i) : 0f;
            h.Mover(abrir, segundos, retraso, golpe);
        }
        yield break;
    }

    public override void CumplirAlSaltar(SequenceContext ctx)
    {
        foreach (var h in Elegidas()) h.Poner(abrir);
    }

    private List<HojaConBisagra> Elegidas()
    {
        var lista = new List<HojaConBisagra>();
        foreach (var h in HojaConBisagra.Todas)
        {
            if (h == null) continue;
            if (!string.IsNullOrEmpty(grupo) && h.grupo != grupo) continue;
            if (!string.IsNullOrEmpty(nombre) && !EmpiezaPor(h.transform, nombre)) continue;
            lista.Add(h);
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (lista.Count == 0) Debug.LogWarning($"[HojasConBisagraBeat] No hay hojas para «{Describe()}». La secuencia sigue.");
#endif
        return lista;
    }

    /// La hoja o alguno de sus padres se llama así (las dos hojas de una ventana cuelgan de ella).
    private static bool EmpiezaPor(Transform t, string prefijo)
    {
        for (var q = t; q != null; q = q.parent)
            if (q.name.StartsWith(prefijo, StringComparison.Ordinal)) return true;
        return false;
    }

    private static float Fraccion(string clave, int i)
    {
        unchecked
        {
            int h = 17 + i * 31;
            foreach (char c in clave) h = h * 31 + c;
            return (h & 0x7fff) / 32767f;
        }
    }
}
