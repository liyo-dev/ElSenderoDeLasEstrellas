using System.Linq;
using Game.NPC.Common;
using Liyodev.NarrativeGraph;
using Sendero.Narrative.Editor;
using UnityEditor;
using UnityEngine;

internal static class ComprobarRecadoDePez
{
    [MenuItem("El Sendero/Historia/Probar entrega de Rosario")]
    private static void Probar()
    {
        const string titulo = "Prueba del recado de Pez";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar(titulo, "Hay una sesión de Play abierta.");
            return;
        }

        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap3.asset");
        var cfg = NarrativeTestConfig.Load();
        cfg.fastForward = true;
        bool ok = NarrativeTestLauncher.Launch(grafo, "cap3-lubina", cfg, out var estado, out _, preset => preset.spawnAnchorId = "Cap3_Aldea");
        SessionState.SetInt("Sendero.PruebaDelRecado", ok ? 1 : 0);
        OrdenesDelEditor.Avisar(titulo, estado);
    }

    [InitializeOnLoadMethod]
    private static void Escuchar() => EditorApplication.update += Revisar;

    private static void Revisar()
    {
        if (!Application.isPlaying) return;
        int fase = SessionState.GetInt("Sendero.PruebaDelRecado", 0);
        if (fase == 0) return;

        var senales = DefaultNarrativeSignals.Instance;
        var dialogo = DialogueManager.Instance;
        if (senales == null || dialogo == null) return;
        string rosario = WaitNpcInteractionNode.SignalKeyFor("NPC_Rosario");
        string pez = WaitNpcInteractionNode.SignalKeyFor("NPC_Pez");
        if (fase == 1 && senales.HasCustomListener(rosario))
        {
            SessionState.SetInt("Sendero.PruebaDelRecado", 2);
            NarrativeActor.AvisarDeLaCharla("NPC_Rosario", "prueba del recado");
        }
        else if (fase == 2 && dialogo.IsOpen)
        {
            for (int i = 0; i < 30 && dialogo.IsOpen; i++) dialogo.Advance();
        }
        else if (fase == 2 && senales.HasCustomListener(pez))
        {
            SessionState.SetInt("Sendero.PruebaDelRecado", 3);
            SessionState.SetFloat("Sendero.PruebaDelRecadoHasta", Time.realtimeSinceStartup + 1f);
        }
        else if (fase == 3 && Time.realtimeSinceStartup >= SessionState.GetFloat("Sendero.PruebaDelRecadoHasta", 0f))
        {
            SessionState.EraseInt("Sendero.PruebaDelRecado");
            var actores = NarrativeServices.Get<INarrativeActorDirectory>();
            var madre = actores.FindActor("NPC_Rosario");
            var hijo = actores.FindActor("NPC_Pez");
            var item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_JugueteDePez.asset");
            var inventario = NarrativeServices.Get<INarrativeInventory>();
            var resultado = $"Juguetes={inventario.Count(item)}; Rosario espera={senales.HasCustomListener(rosario)}, icono={Icono(madre)}; Pez espera={senales.HasCustomListener(pez)}, icono={Icono(hijo)}.";
            System.IO.File.WriteAllText("Guiones/_ordenes/prueba-recado-pez.txt", resultado);
            EditorApplication.isPlaying = false;
        }
    }

    private static bool Icono(Transform actor)
        => actor != null && actor.GetComponentsInChildren<NPCAlertIconController>().Any(icono => icono.HasPersistentIcon);
}
