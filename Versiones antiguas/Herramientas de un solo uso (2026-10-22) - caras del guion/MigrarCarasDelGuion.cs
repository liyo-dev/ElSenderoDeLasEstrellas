using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Migra los assets horneados del cambio de `OrdenDeCara.emocion` (el enum `NPCEmotion`) a la
/// palabra de la cara («alegre»).
///
/// El asset horneado guarda ahora la palabra y no el enum, para que no dependa del arte de este
/// juego (ver `CarasDelGuion` y TDD § 25.5). Los assets que ya estaban horneados tienen el
/// número del enum, y hay que convertirlos.
///
/// No re-hornea: solo cambia el campo de la cara, y los planos, los tiempos y las frases se
/// quedan exactamente como estaban.
///
/// Es una herramienta de un solo uso: cuando se haya ejecutado, se retira.
public static class MigrarCarasDelGuion
{
    private const string Carpeta = "Assets/_SEQUENCES/Guiones";

    [MenuItem("El Sendero/Guion/Migrar las caras de los guiones horneados (una vez)", false, 90)]
    private static void Migrar()
    {
        var assets = new List<GuionHorneado>();
        foreach (var guid in AssetDatabase.FindAssets("t:GuionHorneado", new[] { Carpeta }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GuionHorneado>(ruta);
            if (asset != null) assets.Add(asset);
        }

        if (assets.Count == 0)
        {
            Debug.LogWarning("[MigrarCaras] No hay assets horneados que migrar.");
            return;
        }

        int caras = 0, yaEstaban = 0, sinTraducir = 0;
        foreach (var asset in assets)
        {
            bool cambio = false;
            foreach (var actor in asset.actores)
            {
                if (actor?.caras == null) continue;
                for (int i = 0; i < actor.caras.Count; i++)
                {
                    var orden = actor.caras[i];
                    if (string.IsNullOrEmpty(orden.emocion)) continue;

                    // Si es un número, es el enum de antes: se traduce a su palabra.
                    if (int.TryParse(orden.emocion, out int numero))
                    {
                        var palabra = CarasDelGuion.Canonica((NPCEmotion)numero);
                        if (palabra == null)
                        {
                            Debug.LogError($"[MigrarCaras] {asset.name}: la emoción {numero} " +
                                $"({(NPCEmotion)numero}) no tiene palabra. Se deja como está.");
                            sinTraducir++;
                            continue;
                        }
                        orden.emocion = palabra;
                        actor.caras[i] = orden;
                        caras++;
                        cambio = true;
                    }
                    else yaEstaban++;
                }
            }
            if (cambio)
            {
                EditorUtility.SetDirty(asset);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MigrarCaras] {assets.Count} assets: {caras} caras migradas a palabra, " +
            $"{yaEstaban} ya estaban en palabra, {sinTraducir} sin traducir.");
    }
}
