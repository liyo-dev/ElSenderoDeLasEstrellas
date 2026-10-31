using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica las agendas de los actores (<see cref="AgendaDelActor"/>): cada segundo mira qué entrada
/// vale para cada actor y, si ha cambiado, lo retira, lo devuelve o lo coloca (siempre sin que el
/// jugador lo vea, a través de <see cref="INarrativeActorPresence"/>). Una entrada «no está» se
/// mantiene: si la escena vuelve a crear al actor, se retira otra vez.
///
/// También da la frase de la agenda cuando el jugador le habla (<see cref="NarrativeStandingLines"/>
/// la consulta después de las frases del grafo).
/// </summary>
public static class AgendaDeActores
{
    /// <summary>Carpeta (dentro de cualquier Resources) de la que se cargan las agendas.</summary>
    public const string CarpetaEnResources = "Agendas";

    static readonly List<AgendaDelActor> s_agendas = new();
    static readonly Dictionary<string, EntradaDeAgenda> s_aplicada = new();
    static readonly Dictionary<EntradaDeAgenda, int> s_siguienteFrase = new();
    static bool s_cargadas;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_agendas.Clear();
        s_aplicada.Clear();
        s_siguienteFrase.Clear();
        s_cargadas = false;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        Cargar();
        if (s_agendas.Count == 0) return;
        var go = new GameObject("[Agenda de actores]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<RelojDeAgendas>();
    }

    static void Cargar()
    {
        if (s_cargadas) return;
        s_cargadas = true;
        s_agendas.AddRange(Resources.LoadAll<AgendaDelActor>(CarpetaEnResources));
    }

    /// <summary>La agenda de un actor, o null si no tiene.</summary>
    public static AgendaDelActor DeActor(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return null;
        Cargar();
        foreach (var a in s_agendas)
            if (a != null && a.actorId == actorId) return a;
        return null;
    }

    /// <summary>La frase que le toca decir ahora al actor según su agenda. No la gasta.</summary>
    public static bool TryFrase(string actorId, out NarrativeDialogueAsset frase)
    {
        frase = null;
        var e = DeActor(actorId)?.EntradaActual();
        if (e == null || e.frases == null || e.frases.Count == 0) return false;
        frase = e.frases[Indice(e)];
        return frase != null;
    }

    /// <summary>Apunta que el actor ha dicho <paramref name="frase"/>: la próxima vez dirá la siguiente.</summary>
    public static void FraseDicha(string actorId, NarrativeDialogueAsset frase)
    {
        var e = DeActor(actorId)?.EntradaActual();
        if (e == null || frase == null || e.frases == null || e.frases.Count == 0) return;
        int i = Indice(e);
        if (e.frases[i] == frase) s_siguienteFrase[e] = (i + 1) % e.frases.Count;
    }

    static int Indice(EntradaDeAgenda e) =>
        s_siguienteFrase.TryGetValue(e, out var i) ? i % e.frases.Count : 0;

    /// <summary>Aplica las entradas que han cambiado. La llama <see cref="RelojDeAgendas"/>.</summary>
    internal static void Revisar()
    {
        if (!NarrativeServices.TryGet(out INarrativeActorPresence presencia, nameof(AgendaDeActores))) return;

        foreach (var agenda in s_agendas)
        {
            if (agenda == null || string.IsNullOrEmpty(agenda.actorId)) continue;
            string id = agenda.actorId;
            var entrada = agenda.EntradaActual();
            s_aplicada.TryGetValue(id, out var anterior);
            bool cambia = entrada != anterior;
            bool veniaRetirado = anterior != null && anterior.presencia == PresenciaEnAgenda.NoEsta;

            if (entrada == null)
            {
                if (!cambia) continue;
                if (veniaRetirado && !presencia.TrySetPresent(id, true)) continue;
                s_aplicada.Remove(id);
                continue;
            }

            switch (entrada.presencia)
            {
                case PresenciaEnAgenda.NoEsta:
                    // Se comprueba siempre: la escena puede volver a crear al actor.
                    if (presencia.IsPresent(id) && !presencia.TrySetPresent(id, false)) continue;
                    break;
                case PresenciaEnAgenda.EnUnaMarca:
                    if (cambia && !presencia.TryPlaceUnseen(id, entrada.marca)) continue;
                    break;
                default:
                    if (cambia && veniaRetirado && !presencia.TrySetPresent(id, true)) continue;
                    break;
            }
            s_aplicada[id] = entrada;
        }
    }
}
