using System.Collections;
using UnityEngine;

/// Paso del cierre de batalla: da la Sintonía Estelar del encuentro (BattleEncounterSO.premioSintonia)
/// y anota en el resultado cómo estaba la barra antes y cómo queda, para el informe de victoria.
/// Las estadísticas no suben por ganar: cada hito de Sintonía se cambia en Equipo por más vida o
/// más magia (GDD § 6.1). Ver INC-950.
public sealed class PasoPremioDeSintonia : IPasoDeCierre
{
    private const int PremioSinEncuentro = 100;

    public int Orden => 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoPremioDeSintonia());

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        int puntos = resultado.Encuentro != null ? resultado.Encuentro.premioSintonia : PremioSinEncuentro;
        var sintonia = SintoniaEstelarService.Instance;
        if (puntos <= 0 || sintonia == null) yield break;

        int antes = sintonia.PuntosActuales;
        int umbralAntes = sintonia.PuntosParaSiguienteHito;
        int hitosAntes = sintonia.HitosAlcanzados;

        // El informe cuenta el hito nuevo: sin el aviso suelto en el HUD.
        sintonia.SumarSintonia(puntos, avisar: false);

        resultado.AnotarSintonia(new SintoniaGanada(puntos, antes, umbralAntes,
            sintonia.PuntosActuales, sintonia.PuntosParaSiguienteHito, sintonia.HitosAlcanzados - hitosAntes));
    }

    public void Terminar(ResultadoDeBatalla resultado) { }
}
