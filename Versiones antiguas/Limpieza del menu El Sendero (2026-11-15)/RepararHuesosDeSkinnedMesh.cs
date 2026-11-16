using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RepararHuesosDeSkinnedMesh
{
    [MenuItem("El Sendero/Personajes/Huesos de malla/Revisar (sin cambiar nada)")]
    public static void Revisar() => Procesar(false);

    [MenuItem("El Sendero/Personajes/Huesos de malla/Reparar")]
    public static void Reparar() => Procesar(true);

    private static void Procesar(bool guardar)
    {
        var resumen = new StringBuilder(guardar ? "Reparación de huesos" : "Revisión de huesos (sin guardar)");
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int afectados = 0, reasignados = 0, recreados = 0, omitidos = 0, guardados = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ruta.StartsWith("Assets/", StringComparison.Ordinal) ||
                    ruta.IndexOf("Versiones antiguas", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                EditorUtility.DisplayProgressBar("Huesos de malla", ruta, (float)i / guids.Length);
                GameObject raiz = null;
                var detalles = new List<string>();
                bool cambiado = false;
                try
                {
                    raiz = PrefabUtility.LoadPrefabContents(ruta);
                    foreach (var renderer in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var huesos = renderer.bones;
                        if (renderer.sharedMesh == null || !Array.Exists(huesos, hueso => hueso == null))
                            continue;

                        string etiqueta = AnimationUtility.CalculateTransformPath(renderer.transform, raiz.transform);
                        if (string.IsNullOrEmpty(etiqueta)) etiqueta = renderer.name;
                        if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                        {
                            detalles.Add($"{etiqueta}: omitido, instancia anidada; reparar su prefab de origen");
                            omitidos++;
                            continue;
                        }

                        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(renderer.sharedMesh));
                        SkinnedMeshRenderer referencia = null;
                        if (modelo != null)
                            foreach (var candidato in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                if (candidato.sharedMesh == renderer.sharedMesh) { referencia = candidato; break; }

                        if (referencia == null || referencia.bones.Length != huesos.Length)
                        {
                            detalles.Add($"{etiqueta}: omitido, referencia inexistente o cantidad de huesos distinta");
                            omitidos++;
                            continue;
                        }

                        var creados = new List<Transform>();
                        var nombres = new List<string>();
                        var mapa = new Dictionary<Transform, Transform>();
                        var huesosReferencia = referencia.bones;
                        for (int h = 0; h < huesos.Length; h++)
                            if (huesos[h] != null && huesosReferencia[h] != null)
                                mapa[huesosReferencia[h]] = huesos[h];
                        if (referencia.rootBone != null && renderer.rootBone != null)
                            mapa[referencia.rootBone] = renderer.rootBone;

                        string motivo = null;
                        try
                        {
                            for (int h = 0; h < huesos.Length; h++)
                            {
                                if (huesos[h] != null) continue;
                                var original = huesosReferencia[h];
                                huesos[h] = Resolver(original, renderer.rootBone != null ? renderer.rootBone : raiz.transform,
                                    raiz.transform, mapa, creados, out motivo);
                                if (huesos[h] == null) break;
                                nombres.Add(original.name);
                            }
                            if (motivo == null)
                            {
                                renderer.bones = huesos;
                                EditorUtility.SetDirty(renderer);
                                cambiado = true;
                                reasignados += nombres.Count;
                                recreados += creados.Count;
                                detalles.Add($"{etiqueta}: huesos reasignados {nombres.Count} ({string.Join(", ", nombres)}); " +
                                    $"huesos recreados {creados.Count} ({Nombres(creados)})");
                            }
                        }
                        catch (Exception excepcion) { motivo = excepcion.Message; }
                        finally
                        {
                            // Revierte la jerarquía provisional si no puede completar este renderer.
                            if (motivo != null)
                                for (int c = creados.Count - 1; c >= 0; c--)
                                    if (creados[c] != null) UnityEngine.Object.DestroyImmediate(creados[c].gameObject);
                        }
                        if (motivo != null)
                        {
                            detalles.Add($"{etiqueta}: omitido, {motivo}");
                            omitidos++;
                        }
                    }
                    if (guardar && cambiado)
                    {
                        PrefabUtility.SaveAsPrefabAsset(raiz, ruta, out bool exito);
                        if (exito) guardados++;
                        else detalles.Add("No se ha podido guardar el prefab");
                    }
                }
                catch (Exception excepcion) { detalles.Add($"Error: {excepcion.Message}"); }
                finally
                {
                    if (raiz != null) PrefabUtility.UnloadPrefabContents(raiz);
                }
                if (detalles.Count > 0)
                {
                    afectados++;
                    resumen.Append('\n').Append(ruta).Append(" — ").Append(string.Join("; ", detalles));
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            resumen.Append($"\nTotales: {afectados} prefabs afectados, {reasignados} huesos reasignados, " +
                $"{recreados} huesos recreados, {omitidos} renderers omitidos, {guardados} prefabs guardados.");
            Debug.Log(resumen.ToString());
        }
    }

    private static Transform Resolver(Transform referencia, Transform preferido, Transform raiz,
        Dictionary<Transform, Transform> mapa, List<Transform> creados, out string motivo)
    {
        motivo = null;
        if (referencia == null) { motivo = "hueso o padre ausente en el modelo de referencia"; return null; }
        if (mapa.TryGetValue(referencia, out var conocido)) return conocido;

        var encontrado = Buscar(referencia, preferido, out bool ambiguo);
        if (encontrado == null && !ambiguo && preferido != raiz)
            encontrado = Buscar(referencia, raiz, out ambiguo);
        if (ambiguo) { motivo = $"nombre ambiguo: {referencia.name}"; return null; }
        if (encontrado != null) { mapa[referencia] = encontrado; return encontrado; }

        var padre = Resolver(referencia.parent, preferido, raiz, mapa, creados, out motivo);
        if (padre == null) return null;
        if (PrefabUtility.IsPartOfPrefabInstance(padre))
        {
            motivo = $"el padre de {referencia.name} pertenece a una instancia anidada";
            return null;
        }
        var nuevo = new GameObject(referencia.name).transform;
        creados.Add(nuevo);
        nuevo.SetParent(padre, false);
        nuevo.localPosition = referencia.localPosition;
        nuevo.localRotation = referencia.localRotation;
        nuevo.localScale = referencia.localScale;
        mapa[referencia] = nuevo;
        return nuevo;
    }

    private static Transform Buscar(Transform referencia, Transform raiz, out bool ambiguo)
    {
        Transform resultado = null;
        ambiguo = false;
        foreach (var candidato in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (candidato.name != referencia.name) continue;
            if (referencia.parent != null && (candidato.parent == null || candidato.parent.name != referencia.parent.name))
                continue;
            if (resultado != null) { ambiguo = true; return null; }
            resultado = candidato;
        }
        return resultado;
    }

    private static string Nombres(List<Transform> huesos)
    {
        var nombres = new List<string>();
        foreach (var hueso in huesos) nombres.Add(hueso.name);
        return string.Join(", ", nombres);
    }
}
