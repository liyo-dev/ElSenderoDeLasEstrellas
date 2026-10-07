using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Mantiene el registro de guardado con todas las definiciones ItemData del proyecto.</summary>
[InitializeOnLoad]
public sealed class ItemRegistrySincronizador : AssetPostprocessor
{
    private const string RegistryPath = "Assets/Resources/ItemRegistry.asset";
    private static bool pendiente;
    private static bool sincronizando;
    private static readonly HashSet<string> rutas = new(StringComparer.Ordinal);

    static ItemRegistrySincronizador() => Programar();

    private static void Programar()
    {
        if (pendiente) return;
        pendiente = true;
        EditorApplication.delayCall += Ejecutar;
    }

    private static void Ejecutar()
    {
        pendiente = false;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Programar();
            return;
        }
        Sincronizar();
    }

    public static int Sincronizar()
    {
        if (sincronizando) return 0;
        sincronizando = true;
        try
        {
            var items = new List<ItemData>();
            var ids = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);
            var encontrados = AssetDatabase.FindAssets("t:ItemData");
            Array.Sort(encontrados, StringComparer.Ordinal);
            rutas.Clear();
            foreach (string guid in encontrados)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                rutas.Add(path);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not ItemData item || items.Contains(item)) continue;
                    items.Add(item);
                    if (string.IsNullOrWhiteSpace(item.itemId)) continue;
                    if (ids.TryGetValue(item.itemId, out var otro))
                        Debug.LogWarning($"[ItemRegistry] itemId duplicado '{item.itemId}': {AssetDatabase.GetAssetPath(otro)} y {path}.", item);
                    else ids.Add(item.itemId, item);
                }
            }

            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistrySO>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ItemRegistrySO>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            var so = new SerializedObject(registry);
            var lista = so.FindProperty("items");
            bool cambia = lista.arraySize != items.Count;
            for (int i = 0; !cambia && i < items.Count; i++)
                cambia = lista.GetArrayElementAtIndex(i).objectReferenceValue != items[i];
            if (cambia)
            {
                lista.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                    lista.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                registry.Rebuild();
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
            }
            return items.Count;
        }
        finally { sincronizando = false; }
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (sincronizando) return;
        foreach (string path in deleted) if (rutas.Contains(path)) { Programar(); return; }
        foreach (string path in movedFrom) if (rutas.Contains(path)) { Programar(); return; }
        foreach (string path in imported)
            if (path == RegistryPath || rutas.Contains(path) || ContieneItem(path)) { Programar(); return; }
        foreach (string path in moved)
            if (rutas.Contains(path) || ContieneItem(path)) { Programar(); return; }
    }

    private static bool ContieneItem(string path)
    {
        if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;
        var tipo = AssetDatabase.GetMainAssetTypeAtPath(path);
        return tipo != null && typeof(ItemData).IsAssignableFrom(tipo);
    }
}
