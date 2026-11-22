using UnityEngine;
using Liyodev.NarrativeGraph;

/// Guardar y cargar la partida, y aplicar una partida recién cargada a los sistemas.
public static partial class Partida
{
    /// <summary>
    /// Empieza a escribirse la partida en disco (cualquier guardado: automático, el farol o un
    /// final). Lo escucha el indicador de guardado de la pantalla.
    /// </summary>
    public static event System.Action GuardadoEmpieza;

    /// <summary>Acaba de escribirse la partida; el valor dice si ha salido bien.</summary>
    public static event System.Action<bool> GuardadoAcaba;

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

        // Un auto-guardado recuerda dónde está el jugador; un guardado manual vuelve a su ancla.
        var posicion = contexto == SaveRequestContext.Auto ? CapturarPosicionDelJugador() : default;
        bool ok = Escribir(guardado, contexto, posicion);
        if (ok && contexto == SaveRequestContext.Auto && posicion.valida) s_ultimaPosicionSegura = posicion;
        return ok;
    }

    // Lugar que pide quien guarda (un farol); vacío = el de la zona donde está el jugador.
    static string LugarDelGuardado;

    /// <summary>
    /// Guardado manual en un punto de guardado: la ranura dirá el lugar del farol, no el último
    /// lugar conocido. Vacío = el lugar de la zona en la que está el jugador.
    /// </summary>
    public static bool GuardarEnPuntoDeGuardado(SaveSystem guardado, string claveDelLugar)
    {
        LugarDelGuardado = claveDelLugar;
        try { return Guardar(guardado, SaveRequestContext.Manual); }
        finally { LugarDelGuardado = null; }
    }

    /// <summary>
    /// Guardado manual en un momento en que el jugador no está en un sitio jugable (un final, a
    /// mitad de una escena): al cargar aparece donde el último auto-guardado de esta sesión (o el
    /// de la partida cargada) si lo hay; si no, en su ancla.
    /// </summary>
    public static bool GuardarEnLaUltimaPosicionSegura(SaveSystem guardado)
    {
        if (!guardado)
        {
            GameBootProfileDebugger.Log("Guardar", "No hay SaveSystem", LogType.Error);
            return false;
        }
        return Escribir(guardado, SaveRequestContext.Manual, s_ultimaPosicionSegura);
    }

    // La posición solo va al archivo: en memoria el preset la deja vacía para que un cambio de
    // escena no la use.
    static bool Escribir(SaveSystem guardado, SaveRequestContext contexto, PlayerSaveData.PosicionGuardada posicion)
    {
        GuardadoEmpieza?.Invoke();
        bool ok = false;
        try
        {
            VolcarEstadoDelJuego();
            var datos = AGuardado(Preset);
            datos.posicionDeAutoguardado = posicion;
            // Mejor no guardar que dejar en disco una partida que no se puede continuar: se
            // conserva el guardado anterior.
            if (SePuedeContinuar(datos, out var motivo)) ok = guardado.Save(datos, contexto);
            else Debug.LogError($"[Partida] Guardado ({contexto}) descartado: {motivo}.");
        }
        finally
        {
            GuardadoAcaba?.Invoke(ok);
        }
        GameBootProfileDebugger.Log("Guardar", ok ? $"Guardado ({contexto})" : "Error al guardar", ok ? LogType.Log : LogType.Error);
        return ok;
    }

    /// La partida se podrá continuar: cada grafo guardado sigue en un paso que existe en su grafo, y
    /// si la historia ya ha avanzado (hay señales emitidas) hay estado de los grafos guardado.
    static bool SePuedeContinuar(PlayerSaveData d, out string motivo)
    {
        motivo = null;
        var hub = NarrativeGraphHub.Instance;
        if (hub == null) return true;

        int conPaso = 0;
        if (d.narrativeBlackboards != null)
        {
            foreach (var bb in d.narrativeBlackboards)
            {
                string paso = null;
                bool terminado = false;
                if (bb.blackboardData != null)
                    foreach (var e in bb.blackboardData)
                    {
                        if (e == null) continue;
                        if (e.key == NarrativeRunner.CurrentNodeKey) paso = e.value;
                        else if (e.key == NarrativeRunner.FlowEndedKey) terminado = e.value == "1" || e.value == "True" || e.value == "true";
                    }
                if (terminado) conPaso++;
                if (string.IsNullOrEmpty(paso)) continue;
                conPaso++;
                var runner = hub.GetRunner(bb.graphLabel);
                if (runner == null || runner.graph == null) continue;
                if (runner.graph.FindNode(paso) == null)
                {
                    motivo = $"el grafo «{bb.graphLabel}» quedaría en un paso que no existe ({paso})";
                    return false;
                }
            }
        }

        if (conPaso == 0 && d.senalesEmitidas != null && d.senalesEmitidas.Count > 0 && hub.GetAllRunners().Count > 0)
        {
            motivo = "la historia ha avanzado pero ningún grafo tiene paso guardado (¿grafos sin arrancar?)";
            return false;
        }
        return true;
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
        if (paso != null) motivo = $"el paso «{paso.DisplayTitle}» no deja guardar";
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
