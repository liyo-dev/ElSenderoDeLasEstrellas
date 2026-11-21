#if UNITY_EDITOR
using System.Collections.Generic;
using Game.NPC;
using Game.NPC.Combate;
using Game.NPC.Modules;
using Liyodev.NarrativeGraph;
using UnityEditor;
using UnityEngine;

/// Arreglo de una sola vez (INC-1392): pone el entrenamiento de Erika del Cap. 2 en el orden de
/// Raúl: Protección → 1 contraatacar (una bola, B en el momento justo) → 2 protegerse (lluvia de
/// hechizos, mantener B) → 3 romper escudos → charla del grimorio y la Bola Prisma → duelo. Quita la
/// lección de golpear y pone en la caja de diálogo las frases de cambio de fase del duelo. Se puede
/// repetir. Se ejecuta una vez y este archivo sale a «Versiones antiguas/».
public static class OrdenNuevoDelEntrenamientoDeErika
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap2.asset";
    private const string RutaConfig = "Assets/_NPCs/Combat/NPC_Combat_Config_Erika.asset";

    private const string Proteccion = "cap2-erika-desbloqueo-proteccion";
    private const string Protegerse = "cap2-erika-leccion-protegerse";
    private const string Devolver = "cap2-erika-leccion-devolver";
    private const string Golpear = "cap2-erika-leccion-golpear";
    private const string Escudo = "cap2-erika-leccion-escudo";
    private const string Elementos = "cap2-erika-elementos";

    [MenuItem("El Sendero/Pruebas/Arreglo de una vez: orden nuevo del entrenamiento de Erika (INC-1392)")]
    public static void Ejecutar()
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (g == null) { Debug.LogError($"[INC-1392] Falta {RutaGrafo}"); return; }

        var proteccion = g.FindNode(Proteccion);
        var protegerse = g.FindNode(Protegerse) as GameStepNode;
        var devolver = g.FindNode(Devolver) as GameStepNode;
        var escudo = g.FindNode(Escudo) as GameStepNode;
        var elementos = g.FindNode(Elementos);
        if (proteccion == null || protegerse == null || devolver == null || escudo == null)
        {
            Debug.LogError("[INC-1392] No están los pasos del entrenamiento de Erika en el Cap. 2: no se toca nada.");
            return;
        }

        // Fuera la lección de golpear: quien iba a ella va al escudo.
        var golpear = g.FindNode(Golpear);
        if (golpear != null)
        {
            foreach (var n in g.nodes)
            {
                if (n?.Outputs == null) continue;
                for (int i = 0; i < n.Outputs.Count; i++)
                    if (n.Outputs[i] == Golpear) n.Outputs[i] = Escudo;
            }
            g.nodes.Remove(golpear);
        }

        // Orden: Protección → contraatacar → protegerse → escudo.
        proteccion.Outputs = new List<string> { Devolver };
        devolver.Outputs = new List<string> { Protegerse };
        protegerse.Outputs = new List<string> { Escudo };

        Vector2 o = proteccion.Position;
        const float paso = 280f;
        devolver.Position = o + new Vector2(paso, 0f);
        protegerse.Position = o + new Vector2(paso * 2f, 0f);
        escudo.Position = o + new Vector2(paso * 3f, 0f);
        if (elementos != null) elementos.Position = o + new Vector2(paso * 4f, 0f);

        devolver.DisplayTitle = "22b-c.- Lección 1: contraatacar";
        protegerse.DisplayTitle = "22b-d.- Lección 2: protegerse (lluvia)";
        escudo.DisplayTitle = "22b-e.- Lección 3: romper escudos";
        if (elementos != null) elementos.DisplayTitle = "22b-f.- Erika: el grimorio y la Bola Prisma";

        if (devolver.Request is PedirLeccionDeCombate d)
        {
            d.objetivo = PedirLeccionDeCombate.Objetivo.Devolver;
            d.veces = 1;
            d.segundosDePreparacion = 1f;
            d.segundosEntreHechizos = 2.5f;
            d.segundosDeAviso = 0.8f;
            d.claveAlEmpezar = "ERIKA_LECCION_CONTRAATACAR_INICIO";
            d.fraseAlEmpezar = "Primero, contraatacar. Te voy a lanzar una bola: pulsa B justo cuando te llegue y se la devuelves.";
            d.claveAlAcertar = "ERIKA_LECCION_BIEN";
            d.fraseAlAcertar = "¡Eso es!";
            d.claveAlAcabar = "ERIKA_LECCION_CONTRAATACAR_FIN";
            d.fraseAlAcabar = "¡Uf! Me la has devuelto. Eso es contraatacar.";
        }

        if (protegerse.Request is PedirLeccionDeCombate p)
        {
            p.objetivo = PedirLeccionDeCombate.Objetivo.Protegerse;
            p.veces = 4;
            p.segundosDePreparacion = 1f;
            p.segundosEntreHechizos = 1f;
            p.segundosDeAviso = 0.8f;
            p.hechizosPorTanda = 5;
            p.segundosEntreTandas = 2.5f;
            p.alturaDeLaLluvia = 8f;
            p.radioDeLaLluvia = 1.2f;
            p.velocidadDeLaLluvia = 9f;
            p.colorDeLaLluvia = MagicElement.Fire;
            p.claveAlEmpezar = "ERIKA_LECCION_LLUVIA_INICIO";
            p.fraseAlEmpezar = "Ahora, protegerte. Voy a hacer llover meteoritos: mantén B pulsada con el escudo y aguanta hasta que pase.";
            // Sin frase al acertar: cortaría la lluvia.
            p.claveAlAcertar = null;
            p.fraseAlAcertar = null;
            p.claveAlAcabar = "ERIKA_LECCION_LLUVIA_FIN";
            p.fraseAlAcabar = "¡Bien aguantado! Con el escudo puesto no te tumba ni una lluvia.";
        }

        EditorUtility.SetDirty(g);

        // El cambio de fase del duelo se explica en la caja.
        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(RutaConfig);
        var perfil = config != null ? config.perfilDeCombate : null;
        if (perfil != null && perfil.fases != null)
        {
            foreach (var f in perfil.fases) if (f != null) f.fraseEnCaja = true;
            EditorUtility.SetDirty(perfil);
        }
        else Debug.LogWarning("[INC-1392] No está el perfil del duelo de Erika: su cambio de fase sigue en bocadillo.");

        AssetDatabase.SaveAssets();
        Debug.Log("[INC-1392] Entrenamiento de Erika en el orden nuevo: contraatacar → protegerse (lluvia) → romper escudos → grimorio → duelo.");
    }
}
#endif
