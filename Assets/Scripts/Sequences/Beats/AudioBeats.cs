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
