using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.NPC.Combate;
using Game.NPC.Modules;
using Liyodev.NarrativeGraph;

/// Crea los perfiles de combate de los dos contrabandistas del Cap. 3 (el grandote, que presiona
/// de cerca, y el flaco, que rodea y tira de lejos) y los engancha a su config y al paso
/// «Iniciar combate NPC» del grafo. Menú de un solo uso. Ver INC-1411.
public static class PerfilesDeLosContrabandistas
{
    const string RutaConfig = "Assets/_NPCs/Combat/NPC_Combat_Config_Contrabandistas.asset";
    const string RutaGrafo = "Assets/NarrativeGraph/Cap3.asset";
    const string Carpeta = "Assets/ElSendero/Data/Combate";
    const string IdGrandote = "NPC_Contrabandista1";
    const string IdFlaco = "NPC_Contrabandista2";

    [MenuItem("El Sendero/NPCs/Contrabandistas: perfiles de combate (INC-1411)")]
    public static void Crear()
    {
        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(RutaConfig);
        if (config == null) { Debug.LogError("[Contrabandistas] Falta " + RutaConfig); return; }
        GameObject p1 = config.GetSpellPrefab(0), p2 = config.GetSpellPrefab(1), p3 = config.GetSpellPrefab(2);

        var grandote = Perfil("PerfilDeCombate_ContrabandistaGrandote");
        grandote.acciones = new List<AccionDeCombate>
        {
            Accion("Empujón", p1, "MagicRight", 0f, 5f, 0.6f, 0.6f, 2.5f, 15f, 10f, 1.2f, FuncionDeAccion.Presion, false),
            Accion("Golpe cargado", p2, "MagicLeft", 1.5f, 7f, 0.9f, 1.1f, 5f, 25f, 18f, 0.8f, FuncionDeAccion.Golpe, true),
            Accion("Embestida", p3, "MagicSpecial", 0f, 6f, 1.2f, 1.6f, 10f, 40f, 28f, 0.4f, FuncionDeAccion.Remate, false),
        };
        var kg = grandote.caracter;
        kg.agresividad = 0.65f; kg.cautela = 0.3f; kg.movilidad = 0.25f; kg.paciencia = 0.35f;
        kg.distanciaPreferida = 3.5f; kg.distanciaMinima = 1.5f; kg.distanciaMaxima = 7f;
        kg.velocidadAndando = 2f; kg.velocidadCorriendo = 4.2f; kg.atacantesALaVez = 1;
        kg.umbralDeVueltasContinuas = 0.8f; kg.leeLaDefensaDelJugador = true;
        var dg = grandote.defensas;
        dg.esquivar = true; dg.distanciaDeEsquiva = 2f; dg.recuperacionDeEsquiva = 3.5f;
        dg.escudo = true; dg.duracionDelEscudo = 2.5f; dg.enfriamientoDelEscudo = 9f; dg.escudoElemental = false;
        dg.cubrirseParaRecargar = true; dg.radioDeCobertura = 12f; dg.recargaACubierto = 1.4f;
        dg.leerLaRepeticion = 0.3f;
        grandote.dificultad = 0.45f;
        EditorUtility.SetDirty(grandote);

        var flaco = Perfil("PerfilDeCombate_ContrabandistaFlaco");
        flaco.acciones = new List<AccionDeCombate>
        {
            Accion("Tiro rápido", p1, "MagicRight", 4f, 14f, 0.5f, 0.45f, 2f, 12f, 8f, 1.3f, FuncionDeAccion.Presion, true),
            Accion("Tiro preciso", p2, "MagicLeft", 6f, 15f, 0.8f, 0.7f, 4.5f, 22f, 15f, 0.9f, FuncionDeAccion.Golpe, true),
            Accion("Tiro sucio", p3, "MagicSpecial", 3f, 12f, 1f, 1.2f, 9f, 35f, 22f, 0.4f, FuncionDeAccion.Remate, false),
        };
        var kf = flaco.caracter;
        kf.agresividad = 0.45f; kf.cautela = 0.7f; kf.movilidad = 0.85f; kf.paciencia = 0.65f;
        kf.distanciaPreferida = 9f; kf.distanciaMinima = 5f; kf.distanciaMaxima = 14f;
        kf.velocidadAndando = 3f; kf.velocidadCorriendo = 6.5f; kf.atacantesALaVez = 1;
        kf.umbralDeVueltasContinuas = 0.7f; kf.leeLaDefensaDelJugador = true;
        var df = flaco.defensas;
        df.esquivar = true; df.distanciaDeEsquiva = 3.5f; df.recuperacionDeEsquiva = 1.4f;
        df.escudo = true; df.duracionDelEscudo = 1.5f; df.enfriamientoDelEscudo = 10f; df.escudoElemental = false;
        df.cubrirseParaRecargar = true; df.radioDeCobertura = 16f; df.recargaACubierto = 1.6f;
        df.leerLaRepeticion = 0.5f;
        flaco.dificultad = 0.5f;
        EditorUtility.SetDirty(flaco);

        var so = new SerializedObject(config);
        so.FindProperty("perfilDeCombate").objectReferenceValue = flaco;
        so.ApplyModifiedPropertiesWithoutUndo();

        int pasos = 0;
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (grafo != null)
        {
            foreach (var n in grafo.nodes)
            {
                if (n is GameStepNode g && g.Request is PedirCombateContraNpc r && r.npcId == IdGrandote)
                {
                    r.perfilesPorNpc = new List<PerfilDeCombatePorNpc>
                    {
                        new PerfilDeCombatePorNpc { npcId = IdGrandote, perfil = grandote },
                        new PerfilDeCombatePorNpc { npcId = IdFlaco, perfil = flaco },
                    };
                    pasos++;
                }
            }
            EditorUtility.SetDirty(grafo);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Contrabandistas] Perfiles creados en {Carpeta}; config enganchada; pasos de combate del Cap. 3 actualizados: {pasos}.");
    }

    static PerfilDeCombate Perfil(string nombre)
    {
        string ruta = $"{Carpeta}/{nombre}.asset";
        var p = AssetDatabase.LoadAssetAtPath<PerfilDeCombate>(ruta);
        if (p == null) { p = ScriptableObject.CreateInstance<PerfilDeCombate>(); AssetDatabase.CreateAsset(p, ruta); }
        return p;
    }

    static AccionDeCombate Accion(string nombre, GameObject proyectil, string anim, float dMin, float dMax,
        float aviso, float recuperacion, float enfriamiento, float coste, float dano, float peso,
        FuncionDeAccion funcion, bool devolver) => new AccionDeCombate
    {
        nombre = nombre, proyectil = proyectil, animacion = anim,
        distanciaMinima = dMin, distanciaMaxima = dMax, segundosDeAviso = aviso,
        segundosDeRecuperacion = recuperacion, enfriamiento = enfriamiento, costeDeMana = coste,
        dano = dano, peso = peso, funcion = funcion, sePuedeDevolver = devolver, conElemento = false,
    };
}
