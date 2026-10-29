#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.NPC;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Deja la discusión de Eldran y Victoria del principio en tono amistoso: gestos y notas de
/// SEQ_PerasEldran, gestos y cara de DiscusionEnBucle en el prefab de Eldran del roster, y el icono
/// animado de charla sobre sus cabezas (Canvas Charla, hecho a partir de Canvas Enfado, que se
/// conserva). Se puede ejecutar varias veces. Ver INC-696 e INC-700.
public static class DiscusionAmistosaEldranVictoria
{
    const string Secuencia = "Assets/_SEQUENCES/SEQ_PerasEldran.asset";
    const string PrefabEldran = "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab";
    const string IconoBase = "Assets/_NPCs/OverHead/Canvas Enfado.prefab";
    const string IconoCharla = "Assets/_NPCs/OverHead/Canvas Charla.prefab";
    const string CarpetaFotogramas = "Assets/Art/UI/NpcStatus/Charla";
    const float FpsCharla = 6f;

    /// Clave del texto → (gesto, nota) de cada línea de la secuencia.
    static readonly Dictionary<string, (string gesto, string nota)> Lineas = new()
    {
        ["PERAS_01"] = ("Talk02", "Eldran explica, amistoso: trajo dos cajas"),
        ["PERAS_02"] = ("Question01", "Victoria pregunta si paró por el camino"),
        ["PERAS_03"] = ("Laugh01", "Eldran cae en la cuenta, risa nerviosa"),
        ["PERAS_04"] = ("Laugh01", "Victoria se ríe y avisa de que Will está esperando"),
        ["PERAS_05"] = ("HandWave01", "Eldran saluda a Will riéndose de sí mismo"),
        ["PERAS_VICTORIA_QUEDATE"] = ("HeadNod01", "Victoria acepta la ayuda de Oliver; Oliver se une al grupo por el sistema de party"),
    };

    [MenuItem("El Sendero/Narrativa/Discusión de Eldran y Victoria: tono amistoso")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de ejecutarlo.");

        int lineas = AplicarSecuencia();
        var icono = CrearIconoDeCharla();
        AplicarPrefab(icono);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DiscusionAmistosa] {lineas} líneas de SEQ_PerasEldran, icono de charla y DiscusionEnBucle de Eldran actualizados.");
    }

    static int AplicarSecuencia()
    {
        var seq = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(Secuencia);
        if (seq == null) throw new InvalidOperationException($"No encuentro {Secuencia}.");

        Undo.RecordObject(seq, "Discusión amistosa");
        seq.displayName = "La caja que falta";
        seq.summary = "Will llega hasta donde Eldran y Victoria discuten, en tono amistoso, por la caja de " +
                      "manzanas que falta (gancho de SEQ_DiscusionVentana). Eldran admite que descansó a la " +
                      "sombra de un árbol y Victoria concluye que allí estará. Eldran saluda a Will y le da " +
                      "una moneda para que se compre algo inútil (DG_ELDRAN_REGALO). La caja se la pide " +
                      "después de comprar la estrella (SEQ_EldranCaja).";

        int cambiadas = 0;
        foreach (var fase in seq.phases)
            foreach (var beat in fase.beats)
                if (beat is SayBeat say && say.textKey != null && Lineas.TryGetValue(say.textKey, out var l))
                {
                    say.gesture = l.gesto;
                    say.note = l.nota;
                    cambiadas++;
                }

        if (cambiadas != Lineas.Count)
            Debug.LogWarning($"[DiscusionAmistosa] Esperaba {Lineas.Count} líneas en {Secuencia} y he encontrado {cambiadas}.");
        EditorUtility.SetDirty(seq);
        return cambiadas;
    }

    /// Importa los fotogramas como sprites y crea (o rehace) Canvas Charla a partir de Canvas Enfado,
    /// con el primer fotograma en la Image y un IconoAnimado que los recorre en bucle.
    static GameObject CrearIconoDeCharla()
    {
        var rutas = AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaFotogramas })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        if (rutas.Count == 0) throw new InvalidOperationException($"No hay fotogramas en {CarpetaFotogramas}.");

        foreach (var ruta in rutas)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            if (imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single
                && !imp.mipmapEnabled && imp.alphaIsTransparency) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        var sprites = rutas.Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
        if (sprites.Any(s => s == null)) throw new InvalidOperationException("Algún fotograma no se ha importado como sprite.");

        var raiz = PrefabUtility.LoadPrefabContents(IconoBase);
        try
        {
            raiz.name = "Canvas Charla";
            var imagen = raiz.GetComponentInChildren<Image>(true);
            if (imagen == null) throw new InvalidOperationException($"{IconoBase} no tiene Image.");
            imagen.sprite = sprites[0];

            var anim = imagen.GetComponent<IconoAnimado>();
            if (anim == null) anim = imagen.gameObject.AddComponent<IconoAnimado>();
            var so = new SerializedObject(anim);
            var lista = so.FindProperty("fotogramas");
            lista.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.FindProperty("fps").floatValue = FpsCharla;
            so.FindProperty("tiempoReal").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(raiz, IconoCharla);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    static void AplicarPrefab(GameObject iconoDeCharla)
    {
        var raiz = PrefabUtility.LoadPrefabContents(PrefabEldran);
        try
        {
            var discusion = raiz.GetComponentInChildren<DiscusionEnBucle>(true);
            if (discusion == null) throw new InvalidOperationException($"{PrefabEldran} no tiene DiscusionEnBucle.");

            var so = new SerializedObject(discusion);
            var replica = so.FindProperty("replica");
            replica.arraySize = 2;
            replica.GetArrayElementAtIndex(0).stringValue = "Laugh01";
            replica.GetArrayElementAtIndex(1).stringValue = "HeadShake01";
            so.FindProperty("animo").intValue = (int)NPCEmotion.Happy;
            so.FindProperty("iconoDePelea").objectReferenceValue = iconoDeCharla;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabEldran);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
#endif
