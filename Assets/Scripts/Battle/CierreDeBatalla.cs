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
    public static event Action<ResultadoDeBatalla> AlIniciarBatalla;
    public static event Action<ResultadoDeBatalla> AlCancelarBatalla;

    public static void Iniciar(ResultadoDeBatalla resultado) => AlIniciarBatalla?.Invoke(resultado);
    public static void Cancelar(ResultadoDeBatalla resultado)
    {
        if (resultado != null) AlCancelarBatalla?.Invoke(resultado);
    }

    // Pasos que ya han llamado a Ejecutar en la pasada actual, para poder terminarlos si
    // la corrutina muere antes de llegar al bucle de Terminar.
    private static List<IPasoDeCierre> _activosActual;
    private static ResultadoDeBatalla _resultadoActual;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _pasos.Clear();
        _activosActual = null;
        _resultadoActual = null;
        AlIniciarBatalla = null;
        AlCancelarBatalla = null;
    }
#endif

    public static void Registrar(IPasoDeCierre paso)
    {
        if (paso == null || _pasos.Contains(paso)) return;
        _pasos.Add(paso);
        if (paso is IPasoConInicioDeBatalla inicio)
        {
            AlIniciarBatalla += inicio.Iniciar;
            AlCancelarBatalla += inicio.Cancelar;
        }
    }

    public static void Quitar(IPasoDeCierre paso)
    {
        if (!_pasos.Remove(paso)) return;
        if (paso is IPasoConInicioDeBatalla inicio)
        {
            AlIniciarBatalla -= inicio.Iniciar;
            AlCancelarBatalla -= inicio.Cancelar;
        }
    }

    /// Llama Terminar en orden inverso sobre los pasos que ya ejecutaron, y limpia el
    /// tracking. Es idempotente: si no hay pasada en curso, no hace nada.
    /// Lo llama el bucle normal al acabar y BossArenaController.OnDisable si la corrutina
    /// muere antes de terminar.
    public static void TerminarForzado()
    {
        if (_activosActual == null || _resultadoActual == null) return;
        var hechos = _activosActual;
        var resultado = _resultadoActual;
        _activosActual = null;
        _resultadoActual = null;

        for (int i = hechos.Count - 1; i >= 0; i--)
        {
            try { hechos[i].Terminar(resultado); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// Ejecuta los pasos por orden (Orden de menor a mayor) y, al final, les deja recoger en
    /// orden inverso (devolver la cámara, el control...). Un paso que falla no deja la batalla
    /// sin cerrar: se registra el error y se sigue.
    public static IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        var pasos = new List<IPasoDeCierre>(_pasos);
        pasos.Sort((a, b) => a.Orden.CompareTo(b.Orden));

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[CierreDeBatalla] '{resultado.BattleId}': {pasos.Count} paso(s).");
#endif
        _activosActual = new List<IPasoDeCierre>(pasos.Count);
        _resultadoActual = resultado;

        foreach (var paso in pasos)
        {
            IEnumerator rutina = null;
            try { rutina = paso.Ejecutar(resultado); }
            catch (Exception e) { Debug.LogException(e); }
            _activosActual.Add(paso);
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

        TerminarForzado();
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

/// <summary>Ciclo opcional para pasos que necesitan tomar datos al empezar y descartarlos al cancelar.</summary>
public interface IPasoConInicioDeBatalla
{
    void Iniciar(ResultadoDeBatalla resultado);
    void Cancelar(ResultadoDeBatalla resultado);
}

/// Lo que sale de una batalla ganada: qué batalla era, qué se ha ganado y quién sale en la foto.
/// Los pasos de premios anotan lo ganado; la celebración, quién está; el informe lo enseña todo.
public sealed class ResultadoDeBatalla
{
    public string BattleId { get; }
    public BattleEncounterSO Encuentro { get; }
    public IReadOnlyList<SubidaDeEstadistica> Subidas => _subidas;
    public IReadOnlyList<PremioDelBotin> Botin => _botin;
    /// Quién sale en la foto de victoria: primero el personaje al mando, después los compañeros.
    public IReadOnlyList<PartyControlManager.CharacterSlot> EnLaFoto => _enLaFoto;

    /// Hay algo que contar en el informe.
    public bool HayPremios => _subidas.Count > 0 || _botin.Count > 0;

    private readonly List<SubidaDeEstadistica> _subidas = new();
    private readonly List<PremioDelBotin> _botin = new();
    private readonly List<PartyControlManager.CharacterSlot> _enLaFoto = new();

    public ResultadoDeBatalla(string battleId, BattleEncounterSO encuentro)
    {
        BattleId = battleId;
        Encuentro = encuentro;
    }

    public void AnotarSubida(SubidaDeEstadistica subida)
    {
        if (!Mathf.Approximately(subida.antes, subida.despues)) _subidas.Add(subida);
    }

    public void AnotarBotin(PremioDelBotin premio)
    {
        if (premio.cantidad > 0) _botin.Add(premio);
    }

    public void AnotarEnLaFoto(IReadOnlyList<PartyControlManager.CharacterSlot> personajes)
    {
        _enLaFoto.Clear();
        if (personajes == null) return;
        for (int i = 0; i < personajes.Count; i++)
            if (!_enLaFoto.Contains(personajes[i])) _enLaFoto.Add(personajes[i]);
    }

    /// El nombre del encuentro, traducido si tiene clave. Vacío si no hay encuentro.
    public string NombreDelEncuentro
    {
        get
        {
            if (Encuentro == null) return "";
            if (!string.IsNullOrEmpty(Encuentro.displayNameId) && LocalizationManager.Instance != null)
                return LocalizationManager.Instance.Get(Encuentro.displayNameId, Encuentro.displayName);
            return Encuentro.displayName;
        }
    }
}

public enum TipoDeEstadistica { Vida, Magia, Ataque, Defensa }

/// Una estadística que ha cambiado con la batalla: cuánto valía antes y cuánto después.
public readonly struct SubidaDeEstadistica
{
    public readonly TipoDeEstadistica tipo;
    public readonly float antes;
    public readonly float despues;

    public SubidaDeEstadistica(TipoDeEstadistica tipo, float antes, float despues)
    {
        this.tipo = tipo;
        this.antes = antes;
        this.despues = despues;
    }
}

/// Algo del botín: un objeto o monedas, con su icono y cuántos.
public readonly struct PremioDelBotin
{
    public readonly string nombre;
    public readonly Sprite icono;
    public readonly int cantidad;

    public PremioDelBotin(string nombre, Sprite icono, int cantidad)
    {
        this.nombre = nombre;
        this.icono = icono;
        this.cantidad = cantidad;
    }
}
