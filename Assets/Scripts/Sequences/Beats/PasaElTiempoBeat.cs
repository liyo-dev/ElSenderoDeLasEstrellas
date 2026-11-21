using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// «Pasa el tiempo» en una cinemática (19 nov 2026, regla de Raúl: el cielo nunca cambia de golpe).
/// Encadena en la MISMA duración la luz, la niebla y el cielo de la franja nueva, el clima y, si se
/// pide, la música. Tres maneras:
///   animado  → el cambio se ve en cámara (lo normal; mejor sobre un plano de cielo o general).
///   fundido  → a negro, cambia y vuelve (saltos de tiempo cortos).
///   rotulo   → como fundido, para acompañar un RotuloBeat («Esa noche…») con «&».
/// En el guion: «pasa_el_tiempo Night duracion=6 clima=Tormenta musica=TEMA modo=animado».
[Serializable]
public class PasaElTiempoBeat : SequenceBeat
{
    public enum Modo { Animado = 0, Fundido = 1, Rotulo = 2 }
    public enum Clima { SinCambio = 0, Despejado = 1, Viento = 2, Lluvia = 3, Tormenta = 4, Niebla = 5, Nieve = 6 }

    [Tooltip("La franja a la que pasa.")]
    public DayNightCycle.TimeOfDay timeOfDay = DayNightCycle.TimeOfDay.Night;

    [Tooltip("Segundos que tarda todo el cambio (luz, niebla, cielo y música a la vez).")]
    [Min(0.5f)] public float duracion = 6f;

    public Modo modo = Modo.Animado;

    [Tooltip("Clima que entra con el cambio (con su transición, nunca de golpe).")]
    public Clima clima = Clima.SinCambio;

    [Tooltip("Tema que entra en fundido cruzado con el cambio (vacío = la música sigue).")]
    public string musica;

    [Tooltip("Esperar a que acabe antes del siguiente beat. Lo normal: no (la escena sigue).")]
    public bool esperar = false;

    public override string Describe() => $"Pasa el tiempo: {timeOfDay} en {duracion:0.#} s ({modo})" +
        (clima != Clima.SinCambio ? $" · {clima}" : "") + (string.IsNullOrEmpty(musica) ? "" : $" · música {musica}");

    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        // Al saltar dentro de la cinemática no hay nada que fundir: se coloca el estado final.
        if (ctx != null && ctx.Saltando)
        {
            Aplicar(ctx, inmediato: true, fundidoDeMusica: 0f);
            yield break;
        }

        if (modo == Modo.Animado)
        {
            Aplicar(ctx, inmediato: false, fundidoDeMusica: duracion);
            if (esperar) yield return new WaitForSeconds(duracion);
            yield break;
        }

        // Fundido / rótulo: un tercio cubrir, el cambio en negro, un tercio descubrir.
        float cubrir = Mathf.Clamp(duracion * 0.3f, 0.3f, 1.2f);
        yield return FeedbackService.ScreenFadeAsync(Color.black, cubrir, true);
        Aplicar(ctx, inmediato: true, fundidoDeMusica: duracion);
        yield return new WaitForSeconds(Mathf.Max(0.2f, duracion - 2f * cubrir));
        var descubrir = FeedbackService.ScreenFadeAsync(Color.black, cubrir, false);
        if (esperar) { yield return descubrir; yield break; }
        var player = ctx?.Player;
        if (player != null) player.TrackBackgroundRoutine(player.StartCoroutine(descubrir));
    }

    private void Aplicar(SequenceContext ctx, bool inmediato, float fundidoDeMusica)
    {
        CinematicTimeOfDay.Apply(timeOfDay, inmediato, inmediato ? 0f : duracion);

        switch (clima)
        {
            case Clima.Despejado:
                CinematicWeather.Apply(WeatherBeat.Fenomeno.Tormenta, false, inmediato);
                CinematicWeather.Apply(WeatherBeat.Fenomeno.Lluvia, false, inmediato);
                CinematicWeather.Apply(WeatherBeat.Fenomeno.Niebla, false, inmediato);
                CinematicWeather.Apply(WeatherBeat.Fenomeno.Nieve, false, inmediato);
                CinematicWeather.Apply(WeatherBeat.Fenomeno.Viento, false, inmediato);
                break;
            case Clima.Viento: CinematicWeather.Apply(WeatherBeat.Fenomeno.Viento, true, inmediato); break;
            case Clima.Lluvia: CinematicWeather.Apply(WeatherBeat.Fenomeno.Lluvia, true, inmediato); break;
            case Clima.Tormenta: CinematicWeather.Apply(WeatherBeat.Fenomeno.Tormenta, true, inmediato); break;
            case Clima.Niebla: CinematicWeather.Apply(WeatherBeat.Fenomeno.Niebla, true, inmediato); break;
            case Clima.Nieve: CinematicWeather.Apply(WeatherBeat.Fenomeno.Nieve, true, inmediato); break;
        }

        if (!string.IsNullOrEmpty(musica) && ctx?.Player != null)
            ctx.Player.PlayMusic(musica, false, Mathf.Max(0f, fundidoDeMusica), -1f);
    }
}
