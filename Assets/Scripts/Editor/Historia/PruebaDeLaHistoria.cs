using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.NPC;
using Sendero.Narrative.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba de los capítulos de la historia sin mando: se usa desde el buzón de órdenes del Editor
/// (OrdenesDelEditor). Lee órdenes de «Guiones/Historia/prueba.txt», una por línea:
///   lanzar Cap3 cap3-aldea [ancla] [grupo]   entra en Play desde ese nodo (como Quick Test)
///   senal CLAVE                              emite una señal del grafo
///   hablar NPC_Id                            como si el jugador hablara con ese NPC
///   avanzar                                  avanza la caja de diálogo abierta hasta cerrarla
///   ir Marca                                 lleva al jugador a un SpawnAnchor
///   foto nombre                              captura del Game view en Guiones/Historia/fotos/
///   estado                                   escribe Guiones/Historia/estado.md
///   invulnerable                             el jugador no puede morir (God Mode de PlayerHealthSystem)
///   guardar Escena                           guarda una escena abierta (fuera de Play)
/// Tras cada tanda escribe el estado: dónde está cada grafo, diálogo y cinemática en curso,
/// grupo, actores de los capítulos y los avisos y errores de la consola desde la última vez.
/// </summary>
public static class PruebaDeLaHistoria
{
    private static string Carpeta => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "Historia");
    private static readonly List<string> s_log = new();
    private static bool s_escuchando;

    private static readonly string[] Actores =
    {
        "_ESTELA", "_LIAM", "NPC_Posadero", "NPC_Lucia", "NPC_Sargento", "NPC_GuardiaCruce1", "NPC_GuardiaCruce2",
        "NPC_Contrabandista1", "NPC_Contrabandista2", "NPC_ContrabandistaJefe", "NPC_Eldran", "NPC_Oliver",
    };

    [MenuItem("El Sendero/Historia/Prueba: ejecutar prueba.txt")]
    public static void Ejecutar()
    {
        Escuchar();
        string ruta = Path.Combine(Carpeta, "prueba.txt");
        var sb = new StringBuilder();
        if (!File.Exists(ruta)) { OrdenesDelEditor.Avisar("Prueba", "Falta " + ruta); return; }
        foreach (var bruta in File.ReadAllLines(ruta))
        {
            var l = bruta.Trim();
            if (l.Length == 0 || l.StartsWith("#")) continue;
            var p = l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            try { sb.AppendLine("· " + l + " → " + Orden(p)); }
            catch (Exception e) { sb.AppendLine("· " + l + " → ERROR " + e.Message); }
        }
        EscribirEstado(sb.ToString());
        OrdenesDelEditor.Avisar("Prueba", sb.ToString().Replace('\n', ' '));
    }

    private static string Orden(string[] p)
    {
        switch (p[0])
        {
            case "lanzar":
            {
                var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>($"Assets/NarrativeGraph/{p[1]}.asset");
                var cfg = NarrativeTestConfig.Load();
                cfg.graphLabel = p[1];
                cfg.fastForward = true;
                string ancla = p.Length > 3 ? p[3] : null;
                bool grupo = p.Length > 4 && p[4] == "grupo";
                bool ok = NarrativeTestLauncher.Launch(grafo, p[2], cfg, out var status, out var prev, preset =>
                {
                    if (!string.IsNullOrEmpty(ancla)) preset.spawnAnchorId = ancla;
                    if (grupo) preset.partyMemberIds = new List<string> { "_ESTELA", "_LIAM" };
                    preset.abilities ??= new PlayerAbilities();
                    preset.abilities.magic = preset.abilities.jump = preset.abilities.sprint = preset.abilities.shield = preset.abilities.swim = true;
                });
                s_log.Clear();
                return (ok ? "OK " : "NO ") + status + (prev.warnings.Count > 0 ? " · avisos: " + string.Join(" | ", prev.warnings) : "");
            }
            case "senal":
                DefaultNarrativeSignals.EnsureInstance().RaiseCustom(p[1], "[PruebaDeLaHistoria]");
                return "emitida";
            case "hablar":
                DefaultNarrativeSignals.EnsureInstance().RaiseCustom(WaitNpcInteractionNode.SignalKeyFor(p[1]), "[PruebaDeLaHistoria]");
                return "hablado";
            case "avanzar":
            {
                var dm = DialogueManager.Instance;
                int n = 0;
                while (dm != null && dm.IsOpen && n < 40) { dm.Advance(); n++; }
                return n == 0 ? "no había diálogo abierto" : $"{n} pasos";
            }
            case "ir":
            {
                var jugador = PlayerService.Player;
                if (jugador == null) return "sin jugador";
                if (p.Length >= 3 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                {
                    float y = Terrain.activeTerrain != null ? Terrain.activeTerrain.SampleHeight(new Vector3(x, 0f, z)) + Terrain.activeTerrain.transform.position.y + 0.3f : jugador.transform.position.y;
                    var rb = jugador.GetComponent<Rigidbody>();
                    jugador.transform.position = new Vector3(x, y, z);
                    if (rb != null) rb.position = jugador.transform.position;
                    return "ok " + jugador.transform.position.ToString("F1");
                }
                TeleportService.TeleportToAnchor(jugador, p[1], false);
                return "ok";
            }
            case "foto":
            {
                Directory.CreateDirectory(Path.Combine(Carpeta, "fotos"));
                string f = Path.Combine(Carpeta, "fotos", p[1] + ".png");
                ScreenCapture.CaptureScreenshot(f);
                return f;
            }
            case "estado":
                return "ok";
            case "invulnerable":
            {
                var jugador = PlayerService.Player;
                var salud = jugador != null ? jugador.GetComponentInChildren<PlayerHealthSystem>(true) : null;
                if (salud == null) return "sin jugador";
                salud.SetGodMode(true);
                return "invulnerable";
            }
            case "mirar":
            {
                // Lo generado de los capítulos cuyo nombre contiene el texto: si está encendido y dónde.
                var raiz = GameObject.Find(MontajeDeLosCapitulos.NombreGenerado);
                if (raiz == null) return "no encuentro la raíz generada";
                string texto = string.Join(" ", p.Skip(1));
                var hallados = raiz.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains(texto)).Take(12)
                    .Select(t => $"{t.name}={(t.gameObject.activeInHierarchy ? "on" : "off")}@{t.position:F0}");
                return string.Join(" ; ", hallados);
            }
            case "dar":
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/_ITEMS/{p[1]}.asset");
                if (item == null) return "no existe";
                if (!NarrativeServices.TryGet(out INarrativeInventory inv, nameof(PruebaDeLaHistoria))) return "sin inventario";
                inv.Add(item, 1);
                return "dado";
            }
            case "roster":
            {
                // Por qué aparece o no cada entrada del roster cuyo spawnId contiene el texto.
                var sb = new StringBuilder();
                foreach (var r in Resources.LoadAll<NpcRosterSO>(NpcSpawner.RostersResourcePath))
                    foreach (var e in r.entries)
                    {
                        if (e == null || !e.spawnId.Contains(p[1])) continue;
                        bool valida = e.Validate(out string err);
                        var punto = NpcSpawnRegistry.Get(e.spawnId);
                        NpcSpawner.Spawned.TryGetValue(e.spawnId, out var go);
                        sb.Append($"[{e.spawnId}: válida={valida}{(valida ? "" : " " + err)} condición={e.ConditionMet()} punto={(punto != null ? punto.transform.position.ToString("F0") : "NO")} " +
                                  $"instancia={(go != null ? go.name + (go.activeInHierarchy ? " on " : " off ") + go.transform.position.ToString("F0") : "NO")}] ");
                    }
                return sb.ToString();
            }
            case "pulsar":
            {
                // Pulsa y suelta la barra espaciadora (aceptar en paneles como el informe de victoria).
                var teclado = UnityEngine.InputSystem.Keyboard.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Space));
                int fotogramas = 3;
                void Soltar()
                {
                    if (--fotogramas > 0) return;
                    EditorApplication.update -= Soltar;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                }
                EditorApplication.update += Soltar;
                return "pulsado";
            }
            case "unir":
            {
                if (!ControlDelJugador.SlotDeNombre(p[1], out var slot)) return "no es del grupo";
                // Antes de unir: el estado del compañero que se busca (por qué puede fallar).
                string nombre = ControlDelJugador.Nombre(slot);
                var diag = new StringBuilder();
                foreach (var m in UnityEngine.Object.FindObjectsByType<NPCPartyMember>(FindObjectsInactive.Include))
                {
                    if (m == null) continue;
                    var tipo = typeof(NPCPartyMember);
                    object Campo(string c) => tipo.GetField(c, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(m);
                    var agente = Campo("_agent") as UnityEngine.AI.NavMeshAgent;
                    diag.Append($"[{m.name} «{m.DisplayName}» activo={m.gameObject.activeInHierarchy} enGrupo={m.IsInParty} uniendo={Campo("_isJoining")} " +
                                $"manager={(Campo("_npcManager") != null)} agente={(agente == null ? "NO" : agente.enabled ? (agente.isOnNavMesh ? "en NavMesh" : "fuera del NavMesh") : "apagado")}] ");
                }
                if (PlayerParty.HasInstance) diag.Append($"grupo {PlayerParty.Instance.Members.Count} lleno={PlayerParty.Instance.IsFull} ");
                bool ok = SalidasDelGrupo.Unir(slot);
                return (ok ? "unido · " : "no se ha podido unir · ") + nombre + " · " + diag;
            }
            case "suelo":
            {
                // Altura del terreno y todo lo que corta un rayo vertical (agua incluida) en x z.
                float x = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                float z = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
                var t = Terrain.activeTerrain;
                float alto = t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;
                string ter = t != null ? alto.ToString("F1") : "-";
                var cortes = Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Collide)
                    .OrderByDescending(h => h.point.y).Take(6).Select(h => $"{h.collider.name}@{h.point.y:F1}");
                bool nav = UnityEngine.AI.NavMesh.SamplePosition(new Vector3(x, alto, z), out var nh, 5f, UnityEngine.AI.NavMesh.AllAreas);
                return $"terreno {ter} · NavMesh {(nav ? nh.position.ToString("F1") : "no")} · {string.Join(" ; ", cortes)}";
            }
            case "vencer":
            {
                // Derrota a los jefes vivos (los que llevan barra de jefe) como si el jugador los hubiera vencido.
                int n = 0;
                foreach (var barra in UnityEngine.Object.FindObjectsByType<BossHealthBar>())
                {
                    var d = barra.GetComponent<Damageable>();
                    if (d == null || !barra.gameObject.activeInHierarchy) continue;
                    d.Kill();
                    n++;
                }
                return n == 0 ? "no hay jefes vivos" : $"{n} jefe(s) vencidos";
            }
            case "mision":
                return DefaultNarrativeSignals.EnsureInstance().GetQuestState(p[1]).ToString();
            case "historial":
                return DefaultNarrativeSignals.EnsureInstance().HasEverRaised(p[1]) ? "emitida alguna vez" : "nunca emitida";
            case "rayo":
            {
                // Lo que corta la línea entre dos puntos (x y z x y z), sin disparadores ni la capa de suelo,
                // y lo que envuelve al primero: lo mismo que mira el horneado de planos.
                var v = p.Skip(1).Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Vector3 a = new Vector3(v[0], v[1], v[2]), b = new Vector3(v[3], v[4], v[5]);
                int mascara = ~(1 << 10);
                var cortes = Physics.RaycastAll(a, (b - a).normalized, (b - a).magnitude, mascara, QueryTriggerInteraction.Ignore)
                    .OrderBy(h => h.distance).Select(h => $"{h.collider.name}({LayerMask.LayerToName(h.collider.gameObject.layer)})@{h.distance:F1}");
                var dentro = Physics.OverlapSphere(a, 0.3f, mascara, QueryTriggerInteraction.Ignore).Select(c => c.name);
                return $"corta: {string.Join(" ; ", cortes)} · envuelve: {string.Join(" ; ", dentro)}";
            }
            case "menulog":
            {
                // Ejecuta un menú y devuelve lo que escriba en la consola que contenga un texto
                // (menulog <filtro> <ruta del menú>): para validadores que solo hablan por consola.
                string filtro = p[1];
                string ruta = string.Join(" ", p.Skip(2));
                var lineas = new List<string>();
                Application.LogCallback oyente = (msg, pila, tipo) =>
                {
                    foreach (var l in msg.Split('\n'))
                        if (filtro == "*" || l.Contains(filtro) || l.Contains("[ValidarGestos]")) lineas.Add(l.Trim());
                };
                Application.logMessageReceived += oyente;
                bool hecho;
                try
                {
                    // «#Tipo.Metodo»: llama al método estático sin pasar por el menú.
                    if (ruta.StartsWith("#"))
                    {
                        // El método va tras el último punto: el tipo puede llevar espacio de nombres.
                        string completo = ruta.Substring(1);
                        int punto = completo.LastIndexOf('.');
                        string[] tm = { completo.Substring(0, punto), completo.Substring(punto + 1) };
                        var tipo = TypeCache.GetTypesDerivedFrom<object>().FirstOrDefault(t => t.FullName == tm[0]);
                        var metodo = tipo?.GetMethod(tm[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        hecho = metodo != null;
                        metodo?.Invoke(null, null);
                    }
                    else hecho = EditorApplication.ExecuteMenuItem(ruta);
                }
                finally { Application.logMessageReceived -= oyente; }
                File.WriteAllLines(Path.Combine(Carpeta, "menulog.txt"), lineas);
                return (hecho ? "" : "no encuentro el menú «" + ruta + "» · ") + $"{lineas.Count} líneas en menulog.txt";
            }
            case "cerrar":
            {
                // Cierra una escena abierta en aditivo (fuera de Play y sin cambios sin guardar), para
                // que no se cuele en las pruebas: al entrar en Play se cargan todas las abiertas.
                if (Application.isPlaying) return "no se puede en Play";
                var escena = SceneManager.GetSceneByName(p[1]);
                if (!escena.IsValid() || !escena.isLoaded) return "no está abierta";
                if (escena.isDirty) return "tiene cambios sin guardar; no la cierro";
                if (SceneManager.sceneCount < 2) return "es la única abierta";
                return UnityEditor.SceneManagement.EditorSceneManager.CloseScene(escena, true) ? "cerrada" : "no se pudo cerrar";
            }
            case "guardar":
            {
                var escena = SceneManager.GetSceneByName(p[1]);
                if (!escena.IsValid() || !escena.isLoaded) return "no está abierta";
                return UnityEditor.SceneManagement.EditorSceneManager.SaveScene(escena) ? "guardada" : "no se pudo guardar";
            }
        }
        return "orden desconocida";
    }

    [InitializeOnLoadMethod]
    private static void Escuchar()
    {
        if (s_escuchando) return;
        s_escuchando = true;
        // Las pruebas por el buzón corren con Unity en segundo plano: sin esto, Play no avanza
        // (Run In Background está apagado en el proyecto). Solo se enciende si existe la marca
        // «Guiones/Historia/en_segundo_plano.on», y solo en esta sesión de Play.
        if (EditorApplication.isPlaying && File.Exists(Path.Combine(Carpeta, "en_segundo_plano.on")))
            Application.runInBackground = true;
        Application.logMessageReceived += (msg, pila, tipo) =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool importante = tipo != LogType.Log || msg.StartsWith("[");
            if (!importante) return;
            if (s_log.Count > 300) s_log.RemoveAt(0);
            s_log.Add($"{tipo}: {msg.Split('\n')[0]}");
        };
    }

    private static void EscribirEstado(string ordenes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Estado de la prueba ({DateTime.Now:HH:mm:ss})").AppendLine();
        sb.AppendLine("## Órdenes").AppendLine(ordenes);
        if (EditorApplication.isPlaying && File.Exists(Path.Combine(Carpeta, "en_segundo_plano.on"))) Application.runInBackground = true;
        sb.AppendLine($"Fotograma {Time.frameCount} · t={Time.realtimeSinceStartup:0.0} s · segundo plano {Application.runInBackground}");
        sb.AppendLine($"Play: {EditorApplication.isPlaying} · escenas: {string.Join(", ", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name))}");
        if (EditorApplication.isPlaying)
        {
            var hub = NarrativeGraphHub.Instance;
            sb.AppendLine().AppendLine("## Grafos");
            var campo = typeof(NarrativeRunner).GetField("_activeBranchNodes", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hub != null)
                foreach (var r in hub.GetAllRunners())
                {
                    if (r == null || r.graph == null) continue;
                    var ramas = (campo?.GetValue(r) as HashSet<NarrativeNode>)?.Select(n => n.displayTitle) ?? Enumerable.Empty<string>();
                    sb.AppendLine($"- {r.graph.name}: {(r.CurrentNode != null ? r.CurrentNode.displayTitle : "(nada)")}" +
                                  (ramas.Any() ? " · ramas: " + string.Join(" | ", ramas) : ""));
                }
            var j = PlayerService.Player;
            sb.AppendLine().AppendLine($"Jugador: {(j != null ? j.transform.position.ToString("F1") : "-")}");
            sb.AppendLine($"Diálogo abierto: {DialogueManager.Instance != null && DialogueManager.Instance.IsOpen}");
            sb.AppendLine($"Cinemáticas en curso: {string.Join(", ", CinematicSequencerBase.RunningSequences.Select(s => s != null ? s.name : "?"))}");
            if (PlayerParty.HasInstance) sb.AppendLine($"Grupo: {string.Join(", ", PlayerParty.Instance.Members.Where(m => m != null).Select(m => m.DisplayName))}");
            sb.AppendLine().AppendLine("## Actores");
            foreach (var id in Actores)
            {
                NPCBehaviourManagerV2 npc = null;
                bool hay = NPCRegistry.HasInstance && NPCRegistry.Instance.TryGetNPCByID(id, out npc) && npc != null;
                if (!hay)
                {
                    var go = GameObject.Find(id);
                    if (go == null)
                        foreach (var par in NpcSpawner.Spawned)
                            if (par.Value != null && par.Value.GetComponent<NPCBehaviourManagerV2>() is var m && m != null
                                && (par.Key == "SPAWN_" + id.Replace("NPC_", "") || par.Value.name == id.Replace("NPC_", ""))) go = par.Value;
                    sb.AppendLine($"- {id}: {(go != null ? "objeto " + go.transform.position.ToString("F1") + (go.activeInHierarchy ? "" : " (inactivo)") : "no está")}");
                }
                else
                {
                    var miembro = npc.GetComponent<NPCPartyMember>();
                    var agente = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    string grupo = miembro == null ? "" : miembro.IsInParty ? " · en el grupo" : " · fuera del grupo";
                    string nav = agente == null ? "" : !agente.enabled ? " · agente apagado" : agente.isOnNavMesh ? "" : " · fuera del NavMesh";
                    sb.AppendLine($"- {id}: {npc.transform.position.ToString("F1")}{(npc.gameObject.activeInHierarchy ? "" : " (inactivo)")}{grupo}{nav}");
                }
            }
        }
        sb.AppendLine().AppendLine("## Consola (avisos, errores y pasos del grafo)");
        foreach (var l in s_log) sb.AppendLine("- " + l);
        s_log.Clear();
        Directory.CreateDirectory(Carpeta);
        File.WriteAllText(Path.Combine(Carpeta, "estado.md"), sb.ToString());
    }
}
