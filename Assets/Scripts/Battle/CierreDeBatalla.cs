using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Lo que pasa al ganar una batalla, antes de que el grafo siga (INC-470).
///
/// Cada cosa es un paso independiente (IPasoDeCierre) que se registra aquí: dar los premios,
/// celebrarlo (cámara, salto, música), enseñar el informe... BossArenaController solo llama a
/// Ejecutar y, cuando termina, avisa al grafo de que la batalla está ganada. Para añadir algo
/// nuevo al final de las batallas (un objeto, monedas) se crea otro paso, sin tocar la arena
/// ni los pasos que ya hay.
public static class CierreDeBatalla
{
    private static readonly List<IPasoDeCierre> _pasos = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _pasos.Clear();
#endif

    public static void Registrar(IPasoDeCierre paso)
    {
        if (paso != null && !_pasos.Contains(paso)) _pasos.Add(paso);
    }

    public static void Quitar(IPasoDeCierre paso) => _pasos.Remove(paso);

    /// Ejecuta los pasos por orden (Orden de menor a mayor) y, al final, les deja recoger en
    /// orden inverso (devolver la cámara, el control...). Un paso que falla no deja la batalla
    /// sin cerrar: se registra el error y se sigue.
    public static IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        var pasos = new List<IPasoDeCierre>(_pasos);
        pasos.Sort((a, b) => a.Orden.CompareTo(b.Orden));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CierreDeBatalla] '{resultado.BattleId}': {pasos.Count} paso(s).");
#endif
        var hechos = new List<IPasoDeCierre>(pasos.Count);
        foreach (var paso in pasos)
        {
            IEnumerator rutina = null;
            try { rutina = paso.Ejecutar(resultado); }
            catch (Exception e) { Debug.LogException(e); }
            hechos.Add(paso);
            if (rutina == null) continue;

            while (true)
            {
                object actual;
                try
                {
                    if (!rutina.MoveNext()) break;
                    actual = rutina.Current;
                }
                catch (Exception e) { Debug.LogException(e); break; }
                yield return actual;
            }
        }

        for (int i = hechos.Count - 1; i >= 0; i--)
        {
            try { hechos[i].Terminar(resultado); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}

/// Un paso del cierre de batalla. Orden: 0 premios, 100 celebración, 200 informe (deja hueco
/// para meter pasos entre medias).
public interface IPasoDeCierre
{
    int Orden { get; }
    /// Lo que hace el paso. Puede esperar (celebración, informe) o no (dar un premio).
    IEnumerator Ejecutar(ResultadoDeBatalla resultado);
    /// Al acabar todo el cierre, en orden inverso: dejarlo como estaba (cámara, control...).
    void Terminar(ResultadoDeBatalla resultado);
}

/// Lo que sale de una batalla ganada: qué batalla era y qué se ha ganado. Los pasos de premios
/// añaden líneas al informe; el paso del informe las enseña.
public sealed class ResultadoDeBatalla
{
    public string BattleId { get; }
    public BattleEncounterSO Encuentro { get; }
    public IReadOnlyList<LineaDeInforme> Lineas => _lineas;

    private readonly List<LineaDeInforme> _lineas = new();

    public ResultadoDeBatalla(string battleId, BattleEncounterSO encuentro)
    {
        BattleId = battleId;
        Encuentro = encuentro;
    }

    public void Anotar(LineaDeInforme linea)
    {
        if (!string.IsNullOrEmpty(linea.texto)) _lineas.Add(linea);
    }
}

[Serializable]
public struct LineaDeInforme
{
    public string texto;
    public Sprite icono;

    public LineaDeInforme(string texto, Sprite icono = null)
    {
        this.texto = texto;
        this.icono = icono;
    }
}
