using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimoDeAccion { PorAccion, Neutral, Alegre, Divertido, Ternura, Triste, Enfado, Miedo, Sorpresa, Amenaza }
public enum VozDelGesto { Automatica, Ninguna }

/// Catálogo compartido de acciones, expresiones y gestos disponibles en cada actor.
public static class ReaccionesDeEscena
{
    private static NpcRosterSO[] _rosters;
    private static readonly Dictionary<string, AnimoDeAccion> Acciones = CrearCatalogo();
    private static Dictionary<string, AnimoDeAccion> CrearCatalogo()
    {
        var catalogo = new Dictionary<string, AnimoDeAccion>(StringComparer.OrdinalIgnoreCase);
        Agregar(catalogo, AnimoDeAccion.Alegre, "BAILE MUSICA FIESTA SALUDO VICTORIA");
        Agregar(catalogo, AnimoDeAccion.Divertido, "BROMA RISA");
        Agregar(catalogo, AnimoDeAccion.Ternura, "AYUDA REPARACION ABRAZO");
        Agregar(catalogo, AnimoDeAccion.Sorpresa, "MAGIA TRUENO ESTRUENDO");
        Agregar(catalogo, AnimoDeAccion.Amenaza, "APARECE_VILLANO AMENAZA");
        Agregar(catalogo, AnimoDeAccion.Miedo, "ATAQUE EXPLOSION PELIGRO");
        Agregar(catalogo, AnimoDeAccion.Triste, "DESPEDIDA PERDIDA");
        Agregar(catalogo, AnimoDeAccion.Enfado, "INSULTO OFENSA");
        return catalogo;
    }
    private static void Agregar(Dictionary<string, AnimoDeAccion> tabla, AnimoDeAccion animo, string etiquetas)
    {
        foreach (string etiqueta in etiquetas.Split(' ')) tabla[etiqueta] = animo;
    }
    public static void RegistrarAccion(string etiqueta, AnimoDeAccion animo)
    {
        if (!string.IsNullOrWhiteSpace(etiqueta) && animo != AnimoDeAccion.PorAccion) Acciones[etiqueta.Trim()] = animo;
    }
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _rosters = null;
        Acciones.Clear();
        foreach (var entrada in CrearCatalogo()) Acciones.Add(entrada.Key, entrada.Value);
    }
#endif
    public static AnimoDeAccion Resolver(string etiqueta, AnimoDeAccion animo)
        => animo != AnimoDeAccion.PorAccion ? animo :
           !string.IsNullOrWhiteSpace(etiqueta) && Acciones.TryGetValue(etiqueta.Trim(), out var encontrado) ? encontrado : AnimoDeAccion.Neutral;
    public static NPCEmotion Cara(AnimoDeAccion animo) => animo switch
    {
        AnimoDeAccion.Alegre or AnimoDeAccion.Divertido or AnimoDeAccion.Ternura => NPCEmotion.Happy,
        AnimoDeAccion.Triste => NPCEmotion.Sad,
        AnimoDeAccion.Enfado => NPCEmotion.Angry,
        AnimoDeAccion.Miedo or AnimoDeAccion.Amenaza => NPCEmotion.Scared,
        AnimoDeAccion.Sorpresa => NPCEmotion.Surprised,
        _ => NPCEmotion.Neutral
    };
    private static readonly string[] Alegres = { "HandClap01", "Cheer01", "Cheer02", "Laugh01", "HeadNod01" };
    private static readonly string[] Divertidos = { "Laugh01", "Laugh01_Loop" };
    private static readonly string[] Tiernos = { "HeadNod01" };
    private static readonly string[] Tristes = { "Cry01_Loop", "Cry01" };
    private static readonly string[] Enfadados = { "Angry01", "Angry02" };
    private static readonly string[] Asustados = { "SenseSomethingStart_NoWeapon", "StepBack01" };
    private static readonly string[] Sorprendidos = { "SenseSomethingStart_NoWeapon" };
    public static string[] Gestos(AnimoDeAccion animo) => animo switch
    {
        AnimoDeAccion.Alegre => Alegres, AnimoDeAccion.Divertido => Divertidos,
        AnimoDeAccion.Ternura => Tiernos, AnimoDeAccion.Triste => Tristes,
        AnimoDeAccion.Enfado => Enfadados, AnimoDeAccion.Miedo or AnimoDeAccion.Amenaza => Asustados,
        AnimoDeAccion.Sorpresa => Sorprendidos, _ => Array.Empty<string>()
    };
    public static string ElegirGesto(SequenceActor actor, AnimoDeAccion animo, string anterior = null)
    {
        var gestos = Gestos(animo);
        int inicio = UnityEngine.Random.Range(0, Mathf.Max(1, gestos.Length));
        string alternativa = null;
        for (int i = 0; i < gestos.Length; i++)
        {
            string gesto = gestos[(inicio + i) % gestos.Length];
            if (!actor.TieneEstado(gesto)) continue;
            if (gesto != anterior) return gesto;
            alternativa = gesto;
        }
        return alternativa;
    }
    public static string Voz(AnimoDeAccion animo) => animo switch
    {
        AnimoDeAccion.Alegre => "cheer", AnimoDeAccion.Divertido => "laugh",
        AnimoDeAccion.Ternura or AnimoDeAccion.Triste => "sigh", AnimoDeAccion.Enfado => "growl",
        AnimoDeAccion.Miedo or AnimoDeAccion.Amenaza or AnimoDeAccion.Sorpresa => "gasp", _ => null
    };
    public static string VozParaGesto(string gesto)
    {
        if (string.IsNullOrEmpty(gesto)) return null;
        if (gesto.StartsWith("Laugh", StringComparison.Ordinal)) return "laugh";
        if (gesto.StartsWith("SenseSomething", StringComparison.Ordinal)) return "gasp";
        if (gesto.StartsWith("TakeDamage", StringComparison.Ordinal) || gesto == "CastingDamage01") return "hurt";
        return gesto == "Cry01_Loop" ? "sigh" : null;
    }
    public static string Personaje(SequenceActor actor)
    {
        if (actor == null) return null;
        if (actor.IsPlayer) return "Will";
        string id = actor.Id.StartsWith("NPC_", StringComparison.Ordinal) ? actor.Id.Substring(4) : actor.Id;
        if (!id.StartsWith("Aldeano", StringComparison.OrdinalIgnoreCase) &&
            !id.StartsWith("Vecino", StringComparison.OrdinalIgnoreCase) &&
            !id.StartsWith("Vecina", StringComparison.OrdinalIgnoreCase)) return id;
        _rosters ??= Resources.LoadAll<NpcRosterSO>("NpcRosters");
        foreach (var roster in _rosters)
            foreach (var entrada in roster.entries)
                if (entrada != null && (entrada.persistenceId == actor.Id ||
                    (actor.Transform != null && entrada.gameObjectName == actor.Transform.name)))
                    return string.IsNullOrWhiteSpace(entrada.vozDeReacciones) ? "Vecino" : entrada.vozDeReacciones;
        return "Vecino";
    }
    public static void Sonar(SequenceActor actor, string tipo)
    {
        if (actor?.Transform != null && !string.IsNullOrEmpty(tipo))
            AudioService.Instance?.PlayReaction(Personaje(actor), tipo, 1f, actor.Transform.position, actor.Id);
    }
}
