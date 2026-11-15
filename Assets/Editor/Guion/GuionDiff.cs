#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// Compara el horneado que hay en disco con uno recién hecho en memoria y cuenta, en castellano, qué
/// cambia. El taller lo enseña antes de aceptar: re-hornear vuelve a pasar el solver de cámaras y
/// puede mover planos que no se querían tocar (TDD § 25.5). Ver INC-1230.
internal static class GuionDiff
{
    public static List<string> Comparar(GuionHorneado antes, GuionHorneado despues)
    {
        var cambios = new List<string>();
        if (despues == null) { cambios.Add("El horneado no ha producido nada."); return cambios; }
        if (antes == null)
        {
            cambios.Add($"Primer horneado: {despues.duracion:0.0} s, {despues.planos.Count} planos, {despues.lineas.Count} frases.");
            return cambios;
        }

        if (Mathf.Abs(antes.duracion - despues.duracion) > 0.05f)
            cambios.Add($"Duración: {antes.duracion:0.0} s → {despues.duracion:0.0} s");

        if (antes.planos.Count != despues.planos.Count)
            cambios.Add($"Planos: {antes.planos.Count} → {despues.planos.Count}");

        int comunes = Mathf.Min(antes.planos.Count, despues.planos.Count);
        for (int i = 0; i < comunes; i++)
        {
            PlanoHorneado a = antes.planos[i];
            PlanoHorneado b = despues.planos[i];

            if (Mathf.Abs(a.t0 - b.t0) > 0.02f || Mathf.Abs(a.t1 - b.t1) > 0.02f)
            {
                cambios.Add($"Plano {i + 1}: {a.t0:0.0}-{a.t1:0.0} s → {b.t0:0.0}-{b.t1:0.0} s");
                continue;
            }
            if (a.tipo != b.tipo)
            {
                cambios.Add($"Plano {i + 1}: {a.tipo} → {b.tipo}");
                continue;
            }
            if (Mathf.Abs(a.lente - b.lente) > 0.1f)
                cambios.Add($"Plano {i + 1}: lente {a.lente:0.#} → {b.lente:0.#}");

            if (a.sujetos.Count != b.sujetos.Count)
            {
                cambios.Add($"Plano {i + 1}: sujetos {string.Join(", ", a.sujetos)} → {string.Join(", ", b.sujetos)}");
            }
            else
            {
                for (int s = 0; s < a.sujetos.Count; s++)
                {
                    if (a.sujetos[s] == b.sujetos[s]) continue;
                    cambios.Add($"Plano {i + 1}: sujetos {string.Join(", ", a.sujetos)} → {string.Join(", ", b.sujetos)}");
                    break;
                }
            }

            float t = Mathf.Lerp(b.t0, b.t1, 0.5f);
            bool hayA = antes.PoseDeCamara(t, out Vector3 pa, out Quaternion ra, out float fa);
            bool hayB = despues.PoseDeCamara(t, out Vector3 pb, out Quaternion rb, out float fb);
            if (!hayA || !hayB) continue;

            float metros = Vector3.Distance(pa, pb);
            float grados = Quaternion.Angle(ra, rb);
            if (metros > 0.08f || grados > 0.5f)
                cambios.Add($"Plano {i + 1}: cámara movida ({metros:0.00} m, {grados:0.#}°)");
        }

        if (antes.lineas.Count != despues.lineas.Count)
            cambios.Add($"Frases: {antes.lineas.Count} → {despues.lineas.Count}");
        if (antes.efectos.Count != despues.efectos.Count)
            cambios.Add($"Efectos: {antes.efectos.Count} → {despues.efectos.Count}");

        if (cambios.Count == 0) cambios.Add("Sin cambios.");
        return cambios;
    }
}
#endif
