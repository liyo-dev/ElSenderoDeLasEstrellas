using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Pruebas en Play de los cuerpos propios del grupo (INC-717, tanda 2): informe de vida, maná,
/// ataque, defensa y habilidades de cada personaje; traer y unir a Estela y Liam, pasar el mando,
/// gastar y dañar; volcar la partida a un guardado y volver a cargarlo (también como si fuera
/// una partida vieja, sin habilidades propias de Estela y Liam y con el campo de escalar). No
/// escribe en disco. Se retira al cerrar la prueba.
[InitializeOnLoad]
public static class PruebaDeCuerposPropios
{
    const string Menu = "El Sendero/Pruebas/Cuerpos propios/";
    const string Titulo = "Cuerpos propios";

    static readonly System.Collections.Generic.List<string> s_errores = new System.Collections.Generic.List<string>();

    static PruebaDeCuerposPropios()
    {
        Application.logMessageReceived += (texto, pila, tipo) =>
        {
            if (tipo != LogType.Error && tipo != LogType.Exception && tipo != LogType.Assert) return;
            if (s_errores.Count >= 40) return;
            var linea = pila != null ? pila.Split('\n') : new string[0];
            s_errores.Add(tipo + ": " + texto + (linea.Length > 0 ? " @ " + linea[0] : ""));
        };
    }

    [MenuItem(Menu + "Errores de la consola")]
    static void Errores()
    {
        OrdenesDelEditor.Avisar(Titulo, s_errores.Count == 0 ? "sin errores" : string.Join(" || ", s_errores));
        s_errores.Clear();
    }

    static string Hab(PlayerAbilities h) =>
        h == null ? "-" : $"{(h.swim ? "N" : "n")}{(h.jump ? "S" : "s")}{(h.magic ? "M" : "m")}{(h.fly ? "V" : "v")}{(h.sprint ? "E" : "e")}{(h.shield ? "P" : "p")}";

    static string HabDelCuerpo(PlayerActionManager a) =>
        a == null ? "-" : $"{(a.AllowSwim ? "N" : "n")}{(a.AllowJump ? "S" : "s")}{(a.AllowMagic ? "M" : "m")}{(a.AllowFly ? "V" : "v")}{(a.AllowSprint ? "E" : "e")}{(a.AllowShield ? "P" : "p")}";

    [MenuItem(Menu + "Informe")]
    static void Informe() => OrdenesDelEditor.Avisar(Titulo, TextoDelInforme());

    static string TextoDelInforme()
    {
        var p = Partida.Preset;
        var salud = SaludDelGrupo.Instance;
        var sb = new StringBuilder("Escenas:");
        for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount; k++) sb.Append(" " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(k).name);
        sb.Append($" | Activo={ControlDelJugador.Activo} Jugador={(PlayerService.Player != null ? PlayerService.Player.name : "-")} (habilidades NSMVEP: nadar saltar magia volar esprintar escudo) | ");
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var s = (Slot)i;
            var cuerpo = ControlDelJugador.PilotoDe(s);
            var (at, de) = p != null ? EstadisticasDelPersonaje.AtaqueYDefensa(p, s) : (0f, 0f);
            sb.Append($"{s}: cuerpo={(cuerpo != null ? cuerpo.name : "-")} jugador={(cuerpo != null && cuerpo.LoLlevaElJugador)} enGrupo={ControlDelJugador.EnElGrupo(s)} ");
            if (salud != null && salud.EstaListo) sb.Append($"vida={salud.Vida(s):F0}/{salud.VidaMax(s):F0} maná={salud.Mana(s):F0}/{salud.ManaMax(s):F0} ");
            sb.Append($"ataque={at:F1} defensa={de:F1} hab(partida)={Hab(p != null ? HabilidadesDelPersonaje.De(p, s) : null)} hab(cuerpo)={HabDelCuerpo(cuerpo != null ? cuerpo.GetComponent<PlayerActionManager>() : null)} | ");
        }
        if (p != null)
        {
            sb.Append($"preset Will vida {p.currentHP:F0}/{p.maxHP:F0} maná {p.currentMP:F0}/{p.maxMP:F0} at {p.ataque:F1} de {p.defensa:F1}");
            foreach (var c in p.companionHealth) if (c != null) sb.Append($"; {c.personaje} vida {c.currentHP:F0}/{c.maxHP:F0} maná {c.currentMP:F0}/{c.maxMP:F0} at {c.ataque:F1} de {c.defensa:F1} aprendidas {Hab(c.habilidadesAprendidas)}");
        }
        return sb.ToString();
    }

    [MenuItem(Menu + "Continuar la partida guardada")]
    static void Continuar()
    {
        var menu = Object.FindAnyObjectByType<MainMenuController>();
        if (menu == null) { OrdenesDelEditor.Avisar(Titulo, "No hay menú principal."); return; }
        menu.OnClickContinue();
        OrdenesDelEditor.Avisar(Titulo, "Continuar pulsado.");
    }

    [MenuItem(Menu + "Traer a Estela y Liam")]
    static void Traer()
    {
        var jugador = PlayerService.Player;
        if (!Application.isPlaying || jugador == null) { OrdenesDelEditor.Avisar(Titulo, "Solo en Play con jugador."); return; }
        var sb = new StringBuilder();
        foreach (var (slot, ruta, lado) in new[] { (Slot.Estela, "Assets/Prefabs/_ESTELA.prefab", 2f), (Slot.Liam, "Assets/Prefabs/_LIAM.prefab", -2f) })
        {
            if (ControlDelJugador.PilotoDe(slot) != null) { sb.Append($"{slot} ya está. "); continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            var pos = jugador.transform.position + jugador.transform.right * lado - jugador.transform.forward;
            var go = Object.Instantiate(prefab, pos, jugador.transform.rotation);
            go.name = prefab.name;
            sb.Append($"{slot} creado. ");
        }
        OrdenesDelEditor.Avisar(Titulo, sb.ToString());
    }

    [MenuItem(Menu + "Unir a Estela y Liam")]
    static void Unir() => OrdenesDelEditor.Avisar(Titulo, $"Estela={SalidasDelGrupo.Unir(Slot.Estela)} Liam={SalidasDelGrupo.Unir(Slot.Liam)}");

    [MenuItem(Menu + "Pasar a Will")] static void AWill() => Pasar(Slot.Will);
    [MenuItem(Menu + "Pasar a Estela")] static void AEstela() => Pasar(Slot.Estela);
    [MenuItem(Menu + "Pasar a Liam")] static void ALiam() => Pasar(Slot.Liam);

    static void Pasar(Slot s)
    {
        var pcm = PartyControlManager.Instance;
        if (pcm == null) { OrdenesDelEditor.Avisar(Titulo, "Sin PartyControlManager."); return; }
        pcm.ForceSwitch(s);
        OrdenesDelEditor.Avisar(Titulo, $"Pedido {s}; activo={ControlDelJugador.Activo}");
    }

    [MenuItem(Menu + "Dañar al que va al mando (30 %)")]
    static void Danar() => SaludDelGrupo.Instance?.Danar(ControlDelJugador.Activo, 0.3f);

    // El que va al mando gasta de la reserva de su cuerpo (como al lanzar); un compañero, de SaludDelGrupo.
    [MenuItem(Menu + "Gastar 20 de maná al que va al mando")] static void GastarActivo() => Gastar(ControlDelJugador.Activo);
    [MenuItem(Menu + "Gastar 20 de maná a Estela")] static void GastarEstela() => Gastar(Slot.Estela);

    static void Gastar(Slot s)
    {
        var cuerpo = ControlDelJugador.PilotoDe(s);
        bool ok = false;
        if (cuerpo != null)
        {
            var reserva = cuerpo.GetComponent<ManaPool>();
            ok = s == ControlDelJugador.Activo ? reserva != null && reserva.TrySpend(20f) : SaludDelGrupo.GastarManaDe(cuerpo.gameObject, 20f);
        }
        OrdenesDelEditor.Avisar(Titulo, $"{s} gastado={ok}");
    }

    [MenuItem(Menu + "Celebrar victoria")]
    static void Celebrar()
    {
        bool hay = PlayerService.TryGetComponent(out Game.Player.CelebracionDeVictoria c, allowSceneLookup: false);
        if (hay) c.PlayVictory("prueba-cuerpos-propios");
        OrdenesDelEditor.Avisar(Titulo, $"celebración={(hay ? c.gameObject.name : "-")} celebrando={(hay && c.IsPlayingVictory)}");
    }

    [MenuItem(Menu + "Estado de la celebración")]
    static void EstadoCelebracion()
    {
        bool hay = PlayerService.TryGetComponent(out Game.Player.CelebracionDeVictoria c, allowSceneLookup: false);
        var jugador = PlayerService.Player;
        var inv = jugador != null ? jugador.GetComponent<Invector.vCharacterController.vThirdPersonController>() : null;
        var acc = jugador != null ? jugador.GetComponent<PlayerActionManager>() : null;
        OrdenesDelEditor.Avisar(Titulo, $"celebrando={(hay && c.IsPlayingVictory)} invector={(inv != null && inv.enabled)} modo={(acc != null ? acc.Top.ToString() : "-")}");
    }

    [MenuItem(Menu + "Enseñar a volar al grupo")]
    static void Volar() => OrdenesDelEditor.Avisar(Titulo, $"aprendida={UnlockService.UnlockAbility(AbilityKey.Fly)} activo puede={UnlockService.HasAbility(AbilityKey.Fly)}");

    static string s_guardado;

    [MenuItem(Menu + "Volcar la partida a un guardado en memoria")]
    static void Volcar()
    {
        Partida.VolcarEstadoDelJuego();
        var aGuardado = typeof(Partida).GetMethod("AGuardado", BindingFlags.NonPublic | BindingFlags.Static);
        var datos = (PlayerSaveData)aGuardado.Invoke(null, new object[] { Partida.Preset });
        s_guardado = JsonUtility.ToJson(datos);
        int i = s_guardado.IndexOf("\"companionHealth\"", System.StringComparison.Ordinal);
        OrdenesDelEditor.Avisar(Titulo, $"{s_guardado.Length} caracteres; {(i >= 0 ? s_guardado.Substring(i, Mathf.Min(600, s_guardado.Length - i)) : "sin companionHealth")}");
    }

    [MenuItem(Menu + "Cargar el guardado en memoria")]
    static void Cargar() => CargarJson(s_guardado, "guardado");

    [MenuItem(Menu + "Cargar el guardado en memoria como partida vieja")]
    static void CargarViejo()
    {
        if (string.IsNullOrEmpty(s_guardado)) { OrdenesDelEditor.Avisar(Titulo, "Primero volcar."); return; }
        // Como lo escribía la versión anterior: sin habilidades aprendidas y con canClimb.
        var viejo = System.Text.RegularExpressions.Regex.Replace(s_guardado, ",\"habilidadesAprendidas\":\\{[^}]*\\}", "");
        viejo = viejo.Replace("\"canSwim\":", "\"canClimb\":true,\"canSwim\":");
        CargarJson(viejo, $"partida vieja (quitadas {(s_guardado.Length - viejo.Length)} letras netas)");
    }

    static void CargarJson(string json, string que)
    {
        if (string.IsNullOrEmpty(json)) { OrdenesDelEditor.Avisar(Titulo, "Primero volcar."); return; }
        var datos = JsonUtility.FromJson<PlayerSaveData>(json);
        Partida.CargarDesdeGuardado(datos);
        Partida.AplicarPartidaCargada(aplicarPosicionesDeNpcs: false);
        if (PlayerService.TryGetComponent<PlayerPresetService>(out var pps, includeInactive: false, allowSceneLookup: true))
            pps.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
        var aviso = typeof(GameBootService).GetField("OnProfileReady", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as System.Action;
        aviso?.Invoke();
        OrdenesDelEditor.Avisar(Titulo, $"Cargada {que}.");
    }
}
