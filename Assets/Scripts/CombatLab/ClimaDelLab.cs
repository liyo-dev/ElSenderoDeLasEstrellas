using UnityEngine;

/// Pestaña «Clima» del LAB: pone y quita la hora del día y cada clima. Trabaja sobre el ciclo
/// cargado (DayNightCycle.Instance), así que también sirve en las zonas a las que se viaja.
public sealed class ClimaDelLab : MonoBehaviour, ISeccionDelLab
{
    /// Un clima puesto desde el LAB dura hasta que se quita.
    private const float HastaQueSeQuite = 3600f;

    private enum Precipitacion { Ninguna, Lluvia, Tormenta, Nieve }

    private static readonly DayNightCycle.TimeOfDay[] Horas =
    {
        DayNightCycle.TimeOfDay.Morning, DayNightCycle.TimeOfDay.AfterNoon,
        DayNightCycle.TimeOfDay.Sunset, DayNightCycle.TimeOfDay.Night,
    };

    private bool _alInstante;

    public string Titulo => "Clima";
    public int Orden => 55;

    public void Dibujar()
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null)
        {
            GUILayout.Label("Esta zona no tiene ciclo de día ni clima.", EstiloDelLab.Etiqueta);
            return;
        }

        GUILayout.Label("Hora del día", EstiloDelLab.Titulo);
        GUILayout.Label($"Ahora: {Nombre(ciclo.CurrentTimeOfDay)} · {Reloj(ciclo.HoraDelDia)}", EstiloDelLab.Etiqueta);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < Horas.Length; i++)
            if (EstiloDelLab.Opcion(Nombre(Horas[i]), ciclo.CurrentTimeOfDay == Horas[i]))
                ciclo.SetTimeOfDay(Horas[i], _alInstante);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (EstiloDelLab.Opcion($"El día avanza solo: {SiNo(ciclo.AutoAdvance)}", ciclo.AutoAdvance))
            ciclo.AutoAdvance = !ciclo.AutoAdvance;
        if (EstiloDelLab.Opcion($"Cambios al instante: {SiNo(_alInstante)}", _alInstante))
            _alInstante = !_alInstante;
        GUILayout.EndHorizontal();

        GUILayout.Label("Clima", EstiloDelLab.Titulo);
        bool tormenta = ciclo.IsThunderstorm;
        bool lluvia = ciclo.LluviaPedida && !tormenta;
        bool nieve = ciclo.NievePedida;
        GUILayout.BeginHorizontal();
        if (EstiloDelLab.Opcion("Lluvia", lluvia)) Poner(ciclo, lluvia ? Precipitacion.Ninguna : Precipitacion.Lluvia);
        if (EstiloDelLab.Opcion("Tormenta", tormenta)) Poner(ciclo, tormenta ? Precipitacion.Ninguna : Precipitacion.Tormenta);
        if (EstiloDelLab.Opcion("Nieve", nieve)) Poner(ciclo, nieve ? Precipitacion.Ninguna : Precipitacion.Nieve);
        if (EstiloDelLab.Opcion("Viento", ciclo.IsWindy))
        {
            if (ciclo.IsWindy) ciclo.StopWind();
            else ciclo.StartWind(HastaQueSeQuite);
        }
        if (EstiloDelLab.Opcion("Niebla", ciclo.IsMisty))
        {
            if (ciclo.IsMisty) ciclo.StopMist();
            else ciclo.StartMist(HastaQueSeQuite);
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Despejar todo", EstiloDelLab.Boton)) Despejar(ciclo);
        if (GUILayout.Button("Lanzar un rayo", EstiloDelLab.Boton)) ciclo.DebugStrikeLightning();
        if (EstiloDelLab.Opcion($"Clima aleatorio: {SiNo(ciclo.SorteoAutomatico)}", ciclo.SorteoAutomatico))
            ciclo.SorteoAutomatico = !ciclo.SorteoAutomatico;
        GUILayout.EndHorizontal();

        GUILayout.Label($"Cielo cubierto: {Mathf.RoundToInt(ciclo.Nublado * 100f)} %", EstiloDelLab.Etiqueta);
        EstiloDelLab.Progreso(ciclo.Nublado);
        GUILayout.Label("El panel pausa el juego: el cielo cambia al cerrarlo. Lluvia, tormenta y nieve tardan " +
            "unos 40 s en cubrir el cielo antes de caer (con «Cambios al instante», de golpe) y duran hasta que " +
            "se quitan. En los interiores el clima no se ve.", EstiloDelLab.Nota);
    }

    /// Lluvia, tormenta y nieve se excluyen: se quita la que haya y se pone la pedida.
    private void Poner(DayNightCycle ciclo, Precipitacion pedida)
    {
        if (ciclo.IsThunderstorm) ciclo.StopThunderstorm();
        else if (ciclo.LluviaPedida) ciclo.StopRain();
        if (ciclo.NievePedida) ciclo.StopSnow();

        switch (pedida)
        {
            case Precipitacion.Lluvia: ciclo.StartRain(HastaQueSeQuite, _alInstante); break;
            case Precipitacion.Tormenta: ciclo.StartThunderstorm(HastaQueSeQuite, _alInstante); break;
            case Precipitacion.Nieve: ciclo.StartSnow(HastaQueSeQuite, _alInstante); break;
        }
    }

    private void Despejar(DayNightCycle ciclo)
    {
        Poner(ciclo, Precipitacion.Ninguna);
        if (ciclo.IsWindy) ciclo.StopWind();
        if (ciclo.IsMisty) ciclo.StopMist();
        PanelDelLab.Aviso("Cielo despejándose");
    }

    /// Hora y clima actuales en una línea, para el reporte de la Ayuda.
    public static string Resumen()
    {
        var ciclo = DayNightCycle.Instance;
        if (ciclo == null) return "sin ciclo";
        string clima = ciclo.IsThunderstorm ? "tormenta" : ciclo.LluviaPedida ? "lluvia" : ciclo.NievePedida ? "nieve" : "sin precipitación";
        if (ciclo.IsWindy) clima += ", viento";
        if (ciclo.IsMisty) clima += ", niebla";
        return $"{Nombre(ciclo.CurrentTimeOfDay)} {Reloj(ciclo.HoraDelDia)} · {clima} · cielo cubierto {Mathf.RoundToInt(ciclo.Nublado * 100f)} %";
    }

    private static string Nombre(DayNightCycle.TimeOfDay hora)
    {
        switch (hora)
        {
            case DayNightCycle.TimeOfDay.Morning: return "Amanecer";
            case DayNightCycle.TimeOfDay.AfterNoon: return "Día";
            case DayNightCycle.TimeOfDay.Sunset: return "Atardecer";
            case DayNightCycle.TimeOfDay.Night: return "Noche";
            default: return hora.ToString();
        }
    }

    private static string Reloj(float hora)
    {
        int minutos = Mathf.FloorToInt(Mathf.Repeat(hora, 24f) * 60f);
        return $"{minutos / 60:00}:{minutos % 60:00}";
    }

    private static string SiNo(bool valor) => valor ? "Sí" : "No";
}
