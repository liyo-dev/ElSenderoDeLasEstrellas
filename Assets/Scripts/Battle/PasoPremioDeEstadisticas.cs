using System.Collections;
using UnityEngine;

/// Paso del cierre de batalla: suma a las estadísticas del grupo el premio de estadísticas del encuentro
/// (BattleEncounterSO.premioEstadisticas) y anota en el resultado lo que ha subido, para el informe. Ver INC-470.
public sealed class PasoPremioDeEstadisticas : IPasoDeCierre
{
    public int Orden => 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoPremioDeEstadisticas());

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        var premio = resultado.Encuentro != null ? resultado.Encuentro.premioEstadisticas : default;
        if (premio.EsCero) yield break;

        var (antes, despues) = EstadisticasDelPersonaje.Sumar(premio);
        resultado.AnotarSubida(new SubidaDeEstadistica(TipoDeEstadistica.Vida, antes.vida, despues.vida));
        resultado.AnotarSubida(new SubidaDeEstadistica(TipoDeEstadistica.Magia, antes.magia, despues.magia));
        resultado.AnotarSubida(new SubidaDeEstadistica(TipoDeEstadistica.Ataque, antes.ataque, despues.ataque));
        resultado.AnotarSubida(new SubidaDeEstadistica(TipoDeEstadistica.Defensa, antes.defensa, despues.defensa));
    }

    public void Terminar(ResultadoDeBatalla resultado) { }
}
