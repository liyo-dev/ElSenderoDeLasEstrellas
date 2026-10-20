using System;
using System.Collections;
using UnityEngine;

public enum PresetDeVoz { Ninguno, ExteriorDia, ExteriorNoche, Plegaria, Epico }

/// Mantiene la postproducción de la voz hasta cambiarla o cerrar la secuencia.
[Serializable]
public class EfectoDeVozBeat : SequenceBeat
{
    public PresetDeVoz preset = PresetDeVoz.Ninguno;
    [Range(0f, 20f)] public float gananciaDb = 0f;

    public override string Describe() => $"Voz: {preset}, +{gananciaDb:F1} dB";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var audio = AudioService.Instance;
        if (audio == null || ctx?.Player == null) yield break;
        ctx.Player.RegisterCleanup(() => { if (audio != null) audio.QuitarEfectoDeVoz(); });
        audio.AplicarEfectoDeVoz(preset, gananciaDb);
        yield break;
    }
}

/// Compone una mezcla temporal sin modificar los volúmenes del usuario.
[Serializable]
public class MezclaBeat : SequenceBeat
{
    public string fuente = "mezcla";
    [Range(-80f, 0f)] public float musicaDb = 0f;
    [Range(-80f, 0f)] public float sfxDb = 0f;
    [Range(-80f, 0f)] public float ambienteDb = 0f;
    [Min(0f)] public float fundido = 0.4f;
    public bool quitar = false;

    public override string Describe() => $"Mezcla: {fuente}" + (quitar ? " (quitar)" : "");

    public override IEnumerator Run(SequenceContext ctx)
    {
        var audio = AudioService.Instance;
        if (audio == null || ctx?.Player == null || string.IsNullOrWhiteSpace(fuente)) yield break;
        // El propietario incluye al reproductor para no retirar mezclas de otras secuencias.
        string propietario = $"secuencia:{ctx.Player.GetEntityId()}:{fuente}";
        if (quitar) audio.EndDuck(propietario, fundido);
        else
        {
            ctx.Player.RegisterCleanup(() => { if (audio != null) audio.EndDuck(propietario, 0f); });
            audio.BeginDuck(propietario, musicaDb, sfxDb, ambienteDb, fundido);
        }
        yield break;
    }
}

/// El respiro: la mezcla se aparta un instante y vuelve sola. Es el silencio que da peso
/// al golpe que viene detrás, el recurso que más se nota en una cinemática de acción.
[Serializable]
public class RespiroBeat : SequenceBeat
{
    [Range(-80f, 0f)] public float musicaDb = -10f;
    [Range(-80f, 0f)] public float ambienteDb = -16f;
    [Range(-80f, 0f)] public float sfxDb = -8f;
    [Tooltip("Segundos que dura el respiro.")]
    [Min(0.1f)] public float duracion = 1f;
    [Tooltip("Segundos que tarda la mezcla en bajar.")]
    [Min(0f)] public float ataque = 0.25f;
    [Tooltip("Segundos que tarda la mezcla en volver.")]
    [Min(0f)] public float fundido = 0.35f;
    [Tooltip("Si está activo, la secuencia se para durante el respiro.")]
    public bool esperar = true;

    public override string Describe() => $"Respiro: {duracion:F1} s (música {musicaDb:F0} dB)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var audio = AudioService.Instance;
        if (audio == null || ctx?.Player == null) yield break;
        string propietario = $"secuencia:{ctx.Player.GetEntityId()}:respiro";
        ctx.Player.RegisterCleanup(() => { if (audio != null) audio.EndDuck(propietario, 0f); });
        audio.IniciarRespiro(propietario, musicaDb, sfxDb, ambienteDb, duracion, ataque, fundido);
        if (esperar) yield return new WaitForSecondsRealtime(Mathf.Max(0f, duracion));
    }
}

/// El golpe musical: un stinger en el punto exacto y la música que se aparta un momento
/// para que se oiga el impacto.
[Serializable]
public class GolpeMusicalBeat : SequenceBeat
{
    [Tooltip("Clave del SFX del golpe (stinger). Vacío = solo se aparta la música.")]
    public string eventKey = "";
    [Range(0f, 2f)] public float volumen = 1f;
    [Tooltip("Cuánto baja la música para que se oiga el golpe.")]
    [Range(-80f, 0f)] public float musicaDb = -6f;
    [Tooltip("Segundos que la música se queda apartada.")]
    [Min(0f)] public float duracion = 0.3f;
    [Tooltip("Si está activo, la secuencia se para durante el golpe.")]
    public bool esperar = false;

    public override string Describe() => string.IsNullOrWhiteSpace(eventKey)
        ? $"Golpe musical (música {musicaDb:F0} dB)"
        : $"Golpe musical: {eventKey}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var audio = AudioService.Instance;
        if (audio == null || ctx?.Player == null) yield break;
        if (!string.IsNullOrWhiteSpace(eventKey)) audio.PlaySFX(eventKey, volumen);
        string propietario = $"secuencia:{ctx.Player.GetEntityId()}:golpe";
        ctx.Player.RegisterCleanup(() => { if (audio != null) audio.EndDuck(propietario, 0f); });
        audio.GolpeMusical(propietario, musicaDb, duracion);
        if (esperar) yield return new WaitForSecondsRealtime(Mathf.Max(0f, duracion));
    }
}
