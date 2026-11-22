#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using Game.NPC;
using Game.NPC
;
using UnityEngine;

/// En Play, escribe cómo está el grupo: situaciones abiertas (y quién las tiene) y, por cada
/// compañero, su estado, distancia al jugador y si va por NavMesh. Con «muestreo» guarda una línea
/// cada 0,1 s durante 8 s, para ver qué hace el grupo mientras el jugador salta.
/// Salida en «Guiones/_ordenes/diag_grupo.txt» y «diag_grupo_muestreo.txt».
public static class DiagnosticoDelGrupo
{
    private static string Carpeta => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes");
    private static double s_finMuestreo, s_siguiente;
    private static StringBuilder s_muestreo;

    [MenuItem("El Sendero/Diagnóstico/Grupo: situaciones y estados")]
    public static void Foto()
    {
        File.WriteAllText(Path.Combine(Carpeta, "diag_grupo.txt"), Describir(true));
        OrdenesDelEditor.Avisar("Grupo", Describir(true));
    }

    [MenuItem("El Sendero/Diagnóstico/Grupo: muestreo de 8 s")]
    public static void Muestreo()
    {
        if (!Application.isPlaying) { OrdenesDelEditor.Avisar("Grupo", "solo en Play"); return; }
        Application.runInBackground = true;
        s_muestreo = new StringBuilder();
        s_finMuestreo = EditorApplication.timeSinceStartup + 9.0;
        s_siguiente = 0;
        EditorApplication.update -= Paso;
        EditorApplication.update += Paso;
        OrdenesDelEditor.Avisar("Grupo", "muestreo en marcha (8 s)");
    }

    [MenuItem("El Sendero/Diagnóstico/Grupo: LAB con los tres")]
    public static void LabConLosTres()
    {
        // Para probar por el buzón con el Editor detrás de otras ventanas (solo dura este Play).
        Application.runInBackground = true;
        var panel = Object.FindAnyObjectByType<CombatLabPartyPanel>();
        if (panel == null) { OrdenesDelEditor.Avisar("Grupo", "no hay panel de grupo del LAB"); return; }
        typeof(CombatLabPartyPanel).GetMethod("ApplyComposition",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(panel, new object[] { 3 });
        PanelDelLab.Cerrar();
        OrdenesDelEditor.Avisar("Grupo", "los tres pedidos");
    }

    [MenuItem("El Sendero/Diagnóstico/Grupo: saltar en el sitio")]
    public static void SaltarEnElSitio() => Saltar(0f);

    [MenuItem("El Sendero/Diagnóstico/Grupo: saltar hacia delante")]
    public static void SaltarAdelante() => Saltar(5f);

    private static void Saltar(float empuje)
    {
        var jugador = PlayerService.Player;
        var control = jugador != null ? jugador.GetComponentInChildren<Invector.vCharacterController.vThirdPersonController>() : null;
        if (control == null) { OrdenesDelEditor.Avisar("Grupo", "sin controlador del jugador"); return; }
        if (empuje <= 0f) control.Jump();
        else control.Impulsar(6.5f, control.transform.forward * empuje, false);
        Muestreo();
    }

    /// <summary>Entrada del jugador por guion: tramos de stick y momentos de salto, en segundos desde que empieza.</summary>
    private sealed class EntradaGuionizada : ICharacterInputSource
    {
        private readonly (float desde, float hasta, Vector2 stick)[] _tramos;
        private readonly float[] _saltos;
        private readonly bool[] _hechos;
        private readonly float _inicio;
        public readonly float Duracion;

        public EntradaGuionizada((float, float, Vector2)[] tramos, float[] saltos, float duracion)
        {
            _tramos = tramos; _saltos = saltos; _hechos = new bool[saltos.Length];
            _inicio = Time.time; Duracion = duracion;
        }

        private float T => Time.time - _inicio;
        public Vector2 Move
        {
            get
            {
                float t = T;
                foreach (var tr in _tramos) if (t >= tr.desde && t < tr.hasta) return tr.stick;
                return Vector2.zero;
            }
        }
        public Vector2 CameraLook => Vector2.zero;
        public bool SprintHeld => false;
        public bool JumpHeld => true;
        public bool JumpPressed
        {
            get
            {
                for (int i = 0; i < _saltos.Length; i++)
                    if (!_hechos[i] && T >= _saltos[i]) { _hechos[i] = true; return true; }
                return false;
            }
        }
    }

    private static ICharacterInputSource s_entradaOriginal;
    private static float s_finPrueba;

    [MenuItem("El Sendero/Diagnóstico/Grupo: prueba A (correr y saltar en llano)")]
    public static void PruebaA() => Probar(new[] { (0f, 1.6f, Vector2.up) }, new[] { 0.9f }, 8f);

    [MenuItem("El Sendero/Diagnóstico/Grupo: prueba B (saltar en el sitio)")]
    public static void PruebaB() => Probar(new (float, float, Vector2)[0], new[] { 0.5f, 2.5f }, 6f);

    [MenuItem("El Sendero/Diagnóstico/Grupo: prueba C (saltar, apartarse y seguir)")]
    public static void PruebaC() => Probar(new[] { (0f, 1.6f, Vector2.up), (3.0f, 4.4f, Vector2.left) }, new[] { 0.9f }, 9f);

    [MenuItem("El Sendero/Diagnóstico/Grupo: prueba D (andar y pararse)")]
    public static void PruebaD() => Probar(new[] { (0f, 2.5f, new Vector2(0f, 0.6f)), (2.5f, 3.5f, new Vector2(-0.6f, 0f)) }, new float[0], 8f);

    private static void Probar((float, float, Vector2)[] tramos, float[] saltos, float duracion)
    {
        if (!Application.isPlaying) { OrdenesDelEditor.Avisar("Grupo", "solo en Play"); return; }
        if (s_entradaOriginal == null) s_entradaOriginal = CharacterControllerBridge.Input;
        CharacterControllerBridge.Input = new EntradaGuionizada(tramos, saltos, duracion);
        s_finPrueba = Time.time + duracion;
        EditorApplication.update -= DevolverEntrada;
        EditorApplication.update += DevolverEntrada;
        Muestreo();
    }

    private static void DevolverEntrada()
    {
        if (Application.isPlaying && Time.time < s_finPrueba) return;
        EditorApplication.update -= DevolverEntrada;
        if (s_entradaOriginal != null) CharacterControllerBridge.Input = s_entradaOriginal;
        s_entradaOriginal = null;
    }

    /// <summary>Lleva al jugador a (x, z) mirando a rumbo; el grupo se queda donde esté.</summary>
    public static void Colocar(float x, float z, float rumbo)
    {
        var jugador = PlayerService.PlayerTransform;
        if (jugador == null) return;
        var rb = jugador.GetComponent<Rigidbody>();
        float y = jugador.position.y;
        if (Physics.Raycast(new Vector3(x, y + 30f, z), Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y;
        var pos = new Vector3(x, y + 0.05f, z);
        var rot = Quaternion.Euler(0f, rumbo, 0f);
        jugador.SetPositionAndRotation(pos, rot);
        if (rb != null) { rb.position = pos; rb.rotation = rot; rb.linearVelocity = Vector3.zero; }
    }

    private static void Paso()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup > s_finMuestreo)
        {
            EditorApplication.update -= Paso;
            if (s_muestreo != null)
                File.WriteAllText(Path.Combine(Carpeta, "diag_grupo_muestreo.txt"), s_muestreo.ToString());
            s_muestreo = null;
            return;
        }
        if (EditorApplication.timeSinceStartup < s_siguiente) return;
        s_siguiente = EditorApplication.timeSinceStartup + 0.1;
        s_muestreo.Append(Time.time.ToString("F1")).Append(' ').Append(Describir(false).Replace("\n", " | ")).AppendLine();
    }

    private static string Campos(object o, params string[] nombres)
    {
        if (o == null) return "";
        var sb = new StringBuilder();
        foreach (var n in nombres)
        {
            var f = o.GetType().GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f == null) continue;
            object v = f.GetValue(o);
            sb.Append(' ').Append(n.TrimStart('_')).Append('=').Append(v is float fl ? fl.ToString("F1") : v);
        }
        return sb.ToString();
    }

    private static string Describir(bool conSituaciones)
    {
        var sb = new StringBuilder();
        if (!Application.isPlaying) return "solo en Play";
        if (conSituaciones)
        {
            sb.AppendLine("Situaciones:");
            Kits.Core.GameContext.Describe(sb);
            sb.Append("timeScale=").Append(Time.timeScale).Append(" frame=").Append(Time.frameCount)
              .Append(" pausa=").Append(EditorApplication.isPaused).AppendLine();
        }

        Transform jugador = PlayerService.PlayerTransform;
        Vector3 pj = jugador != null ? jugador.position : Vector3.zero;
        bool enAire = Kits.Core.GameContext.Has(Situaciones.Combate);
        sb.Append("jugador ").Append(jugador != null ? jugador.name : "-")
          .Append(" pos ").Append(pj.ToString("F1"))
          .Append(" combate=").Append(enAire)
          .Append(" aire=").Append(PlayerParty.HasInstance && PlayerParty.Instance.JugadorEnElAire)
          .AppendLine();

        if (!PlayerParty.HasInstance) return sb.Append("sin PlayerParty").ToString();
        foreach (var m in PlayerParty.Instance.Members)
        {
            if (m == null) continue;
            var manager = m.GetComponent<NPCBehaviourManagerV2>();
            var estado = manager != null && manager.Brain != null ? manager.Brain.CurrentState : null;
            var agente = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 p = m.transform.position;
            Vector3 d = p - pj; d.y = 0f;
            sb.Append(m.name).Append(": ").Append(estado != null ? estado.GetType().Name : "-")
              .Append(" dist=").Append(d.magnitude.ToString("F2"))
              .Append(" adel=").Append(jugador != null ? Vector3.Dot(d, Vector3.ProjectOnPlane(jugador.forward, Vector3.up).normalized).ToString("F1") : "-")
              .Append(" lado=").Append(jugador != null ? Vector3.Dot(d, Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(jugador.forward, Vector3.up).normalized)).ToString("F1") : "-")
              .Append(" parada=").Append(agente != null ? agente.stoppingDistance.ToString("F1") : "-")
              .Append(" cam=").Append(Camera.main != null && jugador != null ? Vector3.Distance(Camera.main.transform.position, jugador.position).ToString("F1") : "-")
              .Append(" dy=").Append((p.y - pj.y).ToString("F2"))
              .Append(Campos(estado, "_inSpecialFollow", "_enSaltoDeRastro", "_cruzandoSalto", "_cursorDistanciaRastro"))
              .Append(" navmesh=").Append(UnityEngine.AI.NavMesh.SamplePosition(p, out var hitNm, 0.6f, UnityEngine.AI.NavMesh.AllAreas) ? (hitNm.position.y - p.y).ToString("F2") : "no")
              .Append(" total=").Append(PlayerParty.Instance.DistanciaTotalRastro.ToString("F1"))
              .Append(" dest=").Append(agente != null && agente.enabled && agente.hasPath ? Vector3.Distance(new Vector3(agente.destination.x, 0f, agente.destination.z), new Vector3(pj.x, 0f, pj.z)).ToString("F1") : "-")
              .Append(" agente=").Append(agente == null ? "-" : (agente.enabled ? (agente.isOnNavMesh ? (agente.isStopped ? "parado" : "anda") : "fuera") : "apagado"))
              .AppendLine();
        }
        return sb.ToString();
    }
}
#endif
