using System.Text;
using Game.NPC;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Pruebas en Play de los cuerpos propios del grupo (INC-717): informe del estado de cada cuerpo,
/// traer a Estela y Liam junto al jugador y pasar el mando. Se retira al cerrar la prueba.
[InitializeOnLoad]
public static class PruebaDeCuerposPropios
{
    const string Menu = "El Sendero/Pruebas/Cuerpos propios/";

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
        OrdenesDelEditor.Avisar("Cuerpos propios", s_errores.Count == 0 ? "sin errores" : string.Join(" || ", s_errores));
        s_errores.Clear();
    }

    [MenuItem(Menu + "Informe")]
    static void Informe()
    {
        var sb = new StringBuilder();
        sb.Append($"Activo={ControlDelJugador.Activo} Jugador={(PlayerService.Player != null ? PlayerService.Player.name : "-")} ");
        var cam = Object.FindAnyObjectByType<vThirdPersonCamera>();
        sb.Append($"Cámara→{(cam != null && cam.currentTarget != null ? cam.currentTarget.name : "-")} | ");
        foreach (var c in PilotoDelCuerpo.Cuerpos)
        {
            if (c == null) continue;
            ControlDelJugador.PersonajeDe(c.gameObject, out var p);
            var rb = c.GetComponent<Rigidbody>();
            var inv = c.GetComponent<Invector.vCharacterController.vThirdPersonController>();
            var npc = c.GetComponent<NPCBehaviourManagerV2>();
            var ag = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var m = c.GetComponent<NPCPartyMember>();
            var estado = npc != null && npc.Brain != null && npc.Brain.CurrentState != null ? npc.Brain.CurrentState.StateName : "-";
            var pos = c.transform.position;
            sb.Append($"{c.name}({p}) jugador={c.LoLlevaElJugador} tag={c.tag} cinem={(rb != null && rb.isKinematic)} invector={(inv != null && inv.enabled)} ia={(npc != null && npc.enabled)} agente={(ag != null && ag.enabled)} navmesh={(ag != null && ag.enabled && ag.isOnNavMesh)} estado={estado} grupo={(m != null && m.IsInParty)} pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) | ");
        }
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var s = (Slot)i;
            var npc = ControlDelJugador.NpcDe(s);
            var salud = SaludDelGrupo.Instance;
            sb.Append($"{s}: enGrupo={ControlDelJugador.EnElGrupo(s)} npc={(npc != null ? npc.name : "-")} vida={(salud != null ? salud.Vida(s) : -1):F0}/{(salud != null ? salud.VidaMax(s) : -1):F0} | ");
        }
        OrdenesDelEditor.Avisar("Cuerpos propios", sb.ToString());
    }

    [MenuItem(Menu + "Traer a Estela y Liam")]
    static void Traer()
    {
        var jugador = PlayerService.Player;
        if (!Application.isPlaying || jugador == null) { OrdenesDelEditor.Avisar("Cuerpos propios", "Solo en Play con jugador."); return; }
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
        OrdenesDelEditor.Avisar("Cuerpos propios", sb.ToString());
    }

    [MenuItem(Menu + "Unir a Estela y Liam")]
    static void Unir()
    {
        OrdenesDelEditor.Avisar("Cuerpos propios", $"Estela={SalidasDelGrupo.Unir(Slot.Estela)} Liam={SalidasDelGrupo.Unir(Slot.Liam)}");
    }

    [MenuItem(Menu + "Pasar a Will")] static void AWill() => Pasar(Slot.Will);
    [MenuItem(Menu + "Pasar a Estela")] static void AEstela() => Pasar(Slot.Estela);
    [MenuItem(Menu + "Pasar a Liam")] static void ALiam() => Pasar(Slot.Liam);

    static void Pasar(Slot s)
    {
        var pcm = PartyControlManager.Instance;
        if (pcm == null) { OrdenesDelEditor.Avisar("Cuerpos propios", "Sin PartyControlManager."); return; }
        pcm.ForceSwitch(s);
        OrdenesDelEditor.Avisar("Cuerpos propios", $"Pedido {s}; activo={ControlDelJugador.Activo}");
    }

    [MenuItem(Menu + "Dañar al que va al mando (30 %)")]
    static void Danar() => SaludDelGrupo.Instance?.Danar(ControlDelJugador.Activo, 0.3f);

    [MenuItem(Menu + "Tumbar al que va al mando")]
    static void Tumbar() => SaludDelGrupo.Instance?.Tumbar(ControlDelJugador.Activo);

    [MenuItem(Menu + "Curar a todos")]
    static void Curar() => SaludDelGrupo.Instance?.CurarATodos();

    [MenuItem(Menu + "Volcar la partida")]
    static void Volcar()
    {
        Partida.VolcarEstadoDelJuego();
        var p = Partida.Preset;
        var sb = new StringBuilder($"Will vida {p.currentHP}/{p.maxHP} maná {p.currentMP}/{p.maxMP}; apariencia: ");
        foreach (var a in p.appearance) sb.Append($"{a.category}:{a.partName} ");
        sb.Append("; básicos: ");
        foreach (var id in p.basicSpellIds) sb.Append(id + " ");
        foreach (var c in p.companionHealth) sb.Append($"; {c.personaje} vida {c.currentHP}/{c.maxHP}");
        OrdenesDelEditor.Avisar("Cuerpos propios", sb.ToString());
    }
}
