using System.Collections;
using UnityEngine;

/// Paso del cierre de batalla: suma a Will el premio de estadísticas del encuentro
/// (BattleEncounterSO.premioEstadisticas) y anota en el informe lo que ha subido. Ver INC-470.
public sealed class PasoPremioDeEstadisticas : IPasoDeCierre
{
    public int Orden => 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoPremioDeEstadisticas());

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        var premio = resultado.Encuentro != null ? resultado.Encuentro.premioEstadisticas : default;
        if (premio.EsCero) yield break;

        var (antes, despues) = EstadisticasDeWill.Sumar(premio);
        Anotar(resultado, "STAT_VIDA", "Vida", antes.vida, despues.vida);
        Anotar(resultado, "STAT_MAGIA", "Magia", antes.magia, despues.magia);
        Anotar(resultado, "STAT_ATAQUE", "Ataque", antes.ataque, despues.ataque);
        Anotar(resultado, "STAT_DEFENSA", "Defensa", antes.defensa, despues.defensa);
    }

    public void Terminar(ResultadoDeBatalla resultado) { }

    private static void Anotar(ResultadoDeBatalla resultado, string clave, string porDefecto, float antes, float despues)
    {
        float sube = despues - antes;
        if (Mathf.Abs(sube) < 0.01f) return;
        string nombre = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, porDefecto) : porDefecto;
        string signo = sube > 0f ? "+" : "−";
        resultado.Anotar(new LineaDeInforme($"{nombre}  {antes:0} → <b>{despues:0}</b>  <color=#FFD27A>({signo}{Mathf.Abs(sube):0})</color>"));
    }
}
