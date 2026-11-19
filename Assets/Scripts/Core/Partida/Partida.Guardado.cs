using UnityEngine;

/// Guardar y cargar la partida, y aplicar una partida recién cargada a los sistemas.
public static partial class Partida
{
    /// <summary>
    /// Vuelca el estado del juego en el preset y lo escribe en disco. Los auto-guardados solo se
    /// hacen si la configuración los permite.
    /// </summary>
    public static bool Guardar(SaveSystem guardado, SaveRequestContext contexto = SaveRequestContext.Manual)
    {
        if (!guardado)
        {
            GameBootProfileDebugger.Log("Guardar", "No hay SaveSystem", LogType.Error);
            return false;
        }
        if (contexto == SaveRequestContext.Auto && (Config == null || !Config.allowAutoSaves))
        {
            GameBootProfileDebugger.Log("Guardar", "Auto-guardado omitido (allowAutoSaves = false)", LogType.Warning);
            return false;
        }

        VolcarEstadoDelJuego();
        // Un auto-guardado recuerda dónde está el jugador; un guardado manual vuelve a su ancla.
        // Solo va al archivo: en memoria se queda vacía para que un cambio de escena no la use.
        var datos = AGuardado(Preset);
        datos.posicionDeAutoguardado = contexto == SaveRequestContext.Auto ? CapturarPosicionDelJugador() : default;
        bool ok = guardado.Save(datos, contexto);
        GameBootProfileDebugger.Log("Guardar", ok ? $"Guardado ({contexto})" : "Error al guardar", ok ? LogType.Log : LogType.Error);
        return ok;
    }

    /// <summary>
    /// Si ahora se puede guardar: no hay teletransporte en marcha, ningún paso en curso de los
    /// grafos lo impide (<see cref="NarrativeNode.blockSaving"/>, también en las ramas de un fork)
    /// y no hay combate ni cinemática. <paramref name="motivo"/> dice qué lo impide (diagnóstico).
    /// Lo consultan los puntos de guardado y lo consultará cualquier otra forma de guardar.
    /// </summary>
    public static bool PuedeGuardarAhora(out string motivo)
    {
        motivo = QueImpideGuardar(out var paso);
        if (paso != null) motivo = $"el paso «{paso.displayTitle}» no deja guardar";
        return motivo == null;
    }

    /// <summary>Lo mismo sin explicar el motivo (no reserva memoria: se puede consultar a menudo).</summary>
    public static bool PuedeGuardarAhora() => QueImpideGuardar(out _) == null;

    static string QueImpideGuardar(out NarrativeNode paso)
    {
        paso = null;
        if (TeleportSystem.IsTeleporting) return "teletransporte en marcha";

        paso = NarrativeGraphHub.Instance != null ? NarrativeGraphHub.Instance.FindSaveBlocker() : null;
        if (paso != null) return "paso que no deja guardar";

        if (Kits.Core.GameContext.Has(Situaciones.Combate)) return "combate en curso";
        if (Kits.Core.GameContext.Has(Situaciones.Cinematica)) return "cinemática en curso";
        return null;
    }

    /// <summary>
    /// Lee el guardado del disco, lo deja como preset de la partida y lo aplica a los sistemas.
    /// Devuelve false si no hay guardado o no se puede leer.
    /// </summary>
    public static bool Cargar(SaveSystem guardado)
    {
        if (!guardado || !guardado.HasSave() || !guardado.Load(out var datos))
        {
            GameBootProfileDebugger.Log("Cargar", "No hay guardado que cargar", LogType.Warning);
            return false;
        }

        CargarDesdeGuardado(datos);
        AplicarPartidaCargada(aplicarPosicionesDeNpcs: true);
        GameBootProfileDebugger.Log("Cargar", $"Cargada - ancla {datos.lastSpawnAnchorId}", LogType.Log);
        return true;
    }

    /// <summary>
    /// Deja los sistemas como dice el preset de la partida, como al cargar un guardado. Sirve
    /// también para el preset de pruebas. Las posiciones de los NPCs solo se aplican si ya está
    /// cargada la escena del mundo; si no, las aplica <see cref="WorldBootstrap"/> al cargarla.
    /// </summary>
    public static void AplicarPartidaCargada(bool aplicarPosicionesDeNpcs)
    {
        var p = Preset;
        if (p == null) return;

        ReiniciarSesionNarrativa();
        // Lo que «ya pasó» vuelve con la partida: los tramos de historia lo consultan.
        (DefaultNarrativeSignals.Instance ?? DefaultNarrativeSignals.EnsureInstance())?.RestoreEverRaised(p.senalesEmitidas);

        if (!string.IsNullOrEmpty(p.spawnAnchorId))
            SpawnManager.SetCurrentAnchor(p.spawnAnchorId);

        RestaurarMisiones(p.flags);
        TeleportRegistry.LoadFromSaveData(p.unlockedTeleportPoints);
        NPCRelationshipRegistry.LoadFromSaveEntries(p.npcRelationships);

        // El progreso de jefes lo carga BossProgressPersistenceBridge con OnProfileReady, cuando
        // las arenas ya están escuchando.

        if (p.narrativeBlackboards != null && p.narrativeBlackboards.Count > 0)
            NarrativeGraphHub.Instance?.RestoreBlackboards(p.narrativeBlackboards);

        if (aplicarPosicionesDeNpcs)
            AplicarPosicionesDeNpcs(p);
    }

    /// Las misiones se restauran en cuanto existe el QuestManager: al arrancar el juego el
    /// preset se prepara antes de que despierte, y entonces se espera a OnProfileReady.
    private static void RestaurarMisiones(System.Collections.Generic.List<string> flags)
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.RestoreFromProfileFlags(flags);
            return;
        }
        void AlEstarListo()
        {
            GameBootService.OnProfileReady -= AlEstarListo;
            QuestManager.Instance?.RestoreFromProfileFlags(Preset != null ? Preset.flags : flags);
        }
        GameBootService.OnProfileReady += AlEstarListo;
    }
}
