using System.Collections;
using UnityEngine;

/// Paso del cierre de batalla: da la Sintonía Estelar del encuentro y suma a las estadísticas el premio
/// del encuentro (BattleEncounterSO.premioEstadisticas) con EstadisticasDelPersonaje.Sumar (la vida y el
/// maná, a los tres personajes por SaludDelGrupo). Anota en el resultado lo que ha subido al que va al
/// mando, para el informe. Ver INC-470.
public sealed class PasoPremioDeEstadisticas : IPasoDeCierre
{
    public int Orden => 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoPremioDeEstadisticas());

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        // Otorga Sintonía Estelar por el encuentro (INC-847)
        int puntosSintonia = resultado.Encuentro != null ? resultado.Encuentro.premioSintonia : 100;
        if (puntosSintonia > 0 && SintoniaEstelarService.Instance != null)
        {
            SintoniaEstelarService.Instance.SumarSintonia(puntosSintonia);
        }

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
