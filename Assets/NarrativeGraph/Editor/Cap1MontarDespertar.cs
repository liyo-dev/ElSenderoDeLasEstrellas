using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Cap1, nodo 29: el Despertar vuelve a tener quien lo reproduzca (INC-448).
///
/// El nodo emitía AWAKEN_START y nadie la escuchaba: SEQ_StarAwakening no está montada en ninguna
/// escena (se perdió al cambiar de MainWorld). En vez de montarla a mano en MainWorld (binaria), se
/// lanza con PlayCinematicNode, que la monta en vivo; lo que la secuencia necesita en C# (panic
/// input, proyectil, aturdimiento) va en un prefab de módulos que se instancia con ella.
///
/// Este menú, por AssetDatabase (INC-441) e idempotente:
///   1. Crea `_SEQUENCES/Modulos/SEQ_StarAwakening_Modulos.prefab` con StarAwakeningModule,
///      PanicInputDetector y ShockEffectsController (+ su Volume), con las mismas referencias que
///      tenía el sequencer antiguo en MainWorld_old.
///   2. Lo asigna a SEQ_StarAwakening.modulos.
///   3. Cambia el nodo 29 de Cap1 por un PlayCinematicNode (Hecho → nodo 30, Fallo → se repite) y
///      retira las dos esperas sueltas AWAKEN_DONE / AWAKEN_FAILED.
/// </summary>
public static class Cap1MontarDespertar
{
    private const string RutaGrafo = "Assets/NarrativeGraph/Cap1.asset";
    private const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_StarAwakening.asset";
    private const string CarpetaModulos = "Assets/_SEQUENCES/Modulos";
    private const string RutaPrefab = CarpetaModulos + "/SEQ_StarAwakening_Modulos.prefab";

    private const string Nodo29 = "0a923444-f2e4-42d4-be51-49d30c40e362";
    private const string EsperaHecho = "88fa9b85-47c6-4a3c-94b2-a475fd7f264c";
    private const string EsperaFallo = "1a4732ed-b210-48be-8603-cb824988a90f";
    private const string Nodo30 = "e7e8e2f2-6e54-4490-97b1-fd48323945a4";

    // Referencias que tenía StarAwakeningSequencer en MainWorld_old (GUID de cada asset).
    private const string GuidProyectil = "5ee28f65ca127db42a9a46da980189d4";
    private const string GuidExplosion = "6abcdaf8c8fd61b4697e96e8a1138d29";
    private const string GuidHechizo = "d45589ea2589f21499b4b61ab8d6f54b";
    private const string GuidIconos = "9120cc0f8d6f411aa56d4ef968de44c5";
    private const string GuidSpriteBoton = "cf629afeef7445d1b12313c98a1a2631";
    private const string GuidAccionPanico = "fc9f9f1bd7ce1874fb152afa8758ce88";
    private const long IdAccionPanico = 3179225871509790519;
    private const string GuidPerfilShock = "7e8296045f544d338d35ca62cdd12a18";

    [MenuItem("El Sendero/Archivo/Narrativa/Cap1: montar el Despertar (SEQ_StarAwakening)")]
    public static void Aplicar()
    {
        var log = new StringBuilder("[Cap1MontarDespertar]\n");
        var seq = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaGrafo);
        if (seq == null || grafo == null) { Debug.LogError($"[Cap1MontarDespertar] Falta {(seq == null ? RutaSecuencia : RutaGrafo)}."); return; }

        // 1-2. Prefab de módulos
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (prefab == null)
        {
            prefab = CrearPrefab(log);
            if (prefab == null) { Debug.LogError(log.ToString()); return; }
        }
        else log.AppendLine("= El prefab de módulos ya existía.");

        if (seq.modulos != prefab)
        {
            Undo.RecordObject(seq, "SEQ_StarAwakening: módulos");
            seq.modulos = prefab;
            EditorUtility.SetDirty(seq);
            log.AppendLine("✓ SEQ_StarAwakening.modulos asignado.");
        }

        // 3. Grafo
        var viejo = grafo.FindNode(Nodo29);
        if (viejo is PlayCinematicNode pc && pc.secuencia == seq) log.AppendLine("= El nodo 29 ya reproduce SEQ_StarAwakening.");
        else if (viejo == null) log.AppendLine("✗ No encuentro el nodo 29 en Cap1.");
        else
        {
            Undo.RecordObject(grafo, "Cap1: nodo 29 con PlayCinematicNode");
            var nuevo = new PlayCinematicNode
            {
                guid = viejo.guid, position = viejo.position, chapter = viejo.chapter,
                displayTitle = "29.- DESPERTAR MAGICO (SEQ_StarAwakening)",
                cinematicName = "El Despertar de la Estrella",
                signalIn = seq.signalIn, signalDone = seq.signalOut, signalFailed = "AWAKEN_FAILED",
                secuencia = seq, plantillaDeAjustes = "SEQ_PerasEldran",
            };
            // Salidas por puerto: 0 = Hecho, 1 = Fallo (se repite la secuencia, como antes).
            nuevo.outputs = new System.Collections.Generic.List<string> { Nodo30, Nodo29 };
            grafo.nodes[grafo.nodes.IndexOf(viejo)] = nuevo;

            int quitados = grafo.nodes.RemoveAll(n => n != null && (n.guid == EsperaHecho || n.guid == EsperaFallo));
            log.AppendLine($"✓ Nodo 29 → PlayCinematicNode (Hecho → 30, Fallo → repetir). Esperas sueltas retiradas: {quitados}.");
            EditorUtility.SetDirty(grafo);
        }

        AssetDatabase.SaveAssets();
        log.AppendLine("Si la ventana del grafo está abierta, ciérrala y vuelve a abrirla.");
        Debug.Log(log.ToString());
    }

    private static GameObject CrearPrefab(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaModulos)) AssetDatabase.CreateFolder("Assets/_SEQUENCES", "Modulos");

        var raiz = new GameObject("SEQ_StarAwakening_Modulos");
        try
        {
            var modulo = raiz.AddComponent<StarAwakeningModule>();
            var panico = raiz.AddComponent<PanicInputDetector>();
            var shock = raiz.AddComponent<ShockEffectsController>();

            var hijo = new GameObject("ShockVolume");
            hijo.transform.SetParent(raiz.transform, false);
            var volumen = hijo.AddComponent<Volume>();
            volumen.isGlobal = true;
            volumen.priority = 10f;
            volumen.weight = 0f;
            volumen.sharedProfile = Cargar<VolumeProfile>(GuidPerfilShock, log, "perfil del aturdimiento");

            var so = new SerializedObject(shock);
            so.FindProperty("shockVolume").objectReferenceValue = volumen;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(panico);
            so.FindProperty("panicAction").objectReferenceValue = CargarAccion(log);
            so.FindProperty("pressesRequired").intValue = 8;
            so.FindProperty("windowSeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(modulo);
            var proyectil = Cargar<GameObject>(GuidProyectil, log, "proyectil");
            so.FindProperty("incomingProjectilePrefab").objectReferenceValue =
                proyectil != null ? proyectil.GetComponentInChildren<SlowMotionFireProjectile>(true) : null;
            so.FindProperty("explosionVFX").objectReferenceValue = Cargar<GameObject>(GuidExplosion, log, "explosión");
            so.FindProperty("cinematicSpellFallback").objectReferenceValue = Cargar<ScriptableObject>(GuidHechizo, log, "hechizo de respaldo");
            so.FindProperty("interactIconSet").objectReferenceValue = Cargar<ScriptableObject>(GuidIconos, log, "iconos de botón");
            so.FindProperty("panicButtonSprite").objectReferenceValue = CargarSprite(GuidSpriteBoton, log);
            so.FindProperty("panicInputDetector").objectReferenceValue = panico;
            so.FindProperty("shockEffects").objectReferenceValue = shock;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            log.AppendLine($"✓ Creado {RutaPrefab}.");
            return prefab;
        }
        finally { Object.DestroyImmediate(raiz); }
    }

    private static T Cargar<T>(string guid, StringBuilder log, string que) where T : Object
    {
        var ruta = AssetDatabase.GUIDToAssetPath(guid);
        var a = string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null) log.AppendLine($"✗ No encuentro el {que} (guid {guid}): asígnalo a mano en el prefab.");
        return a;
    }

    private static Sprite CargarSprite(string guid, StringBuilder log)
    {
        var ruta = AssetDatabase.GUIDToAssetPath(guid);
        var s = string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
        if (s == null) log.AppendLine($"✗ No encuentro el sprite del botón (guid {guid}).");
        return s;
    }

    private static InputActionReference CargarAccion(StringBuilder log)
    {
        var ruta = AssetDatabase.GUIDToAssetPath(GuidAccionPanico);
        if (!string.IsNullOrEmpty(ruta))
            foreach (var r in AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<InputActionReference>())
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(r, out _, out long id) && id == IdAccionPanico)
                    return r;
        log.AppendLine("✗ No encuentro la acción del panic input: asígnala a mano en PanicInputDetector.");
        return null;
    }
}
