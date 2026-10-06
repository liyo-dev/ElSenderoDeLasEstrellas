#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class ModularCharacterBaker
{
    [MenuItem("El Sendero/Personajes/Bake Selected As Clean Prefab")]
    public static void BakeSelected()
    {
        var src = Selection.activeGameObject;
        if (!src)
        {
            EditorUtility.DisplayDialog("Baker", "Selecciona el GO raíz del personaje en la escena.", "Ok");
            return;
        }

        // Crear una copia en la escena
        GameObject clone = Object.Instantiate(src);
        clone.name = src.name + "_BAKED";
        
        // Posicionar al lado del original 
        clone.transform.position = src.transform.position + Vector3.right * 2f;
        clone.transform.rotation = src.transform.rotation;

        var protectedBones = new HashSet<Transform>();
        foreach (var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            ProtectBoneAndParents(renderer.rootBone, protectedBones);
            foreach (var bone in renderer.bones)
                ProtectBoneAndParents(bone, protectedBones);
        }

        // Elimina las partes inactivas y conserva la jerarquía usada por skinning.
        RemoveInactiveRecursive(clone.transform, protectedBones);
        
        // Eliminar holders vacíos
        RemoveEmptyHolders(clone.transform, protectedBones);

        // Seleccionar el clon para que el usuario lo vea
        Selection.activeGameObject = clone;
        
        EditorUtility.DisplayDialog("Baked!", 
            $"Personaje horneado creado: {clone.name}\n\n" +
            "Solo contiene las partes activas (sin GameObjects inactivos).\n" +
            "Ahora puedes convertirlo a prefab manualmente si lo deseas.", 
            "Ok");
    }

    static void ProtectBoneAndParents(Transform bone, HashSet<Transform> protectedBones)
    {
        // Conserva también los padres para que su eliminación no destruya huesos usados.
        for (var current = bone; current != null; current = current.parent)
            protectedBones.Add(current);
    }

    static void RemoveInactiveRecursive(Transform t, HashSet<Transform> protectedBones)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            RemoveInactiveRecursive(child, protectedBones);
            
            // Si el GameObject está inactivo, eliminarlo
            if (!child.gameObject.activeSelf && !protectedBones.Contains(child))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    static void RemoveEmptyHolders(Transform t, HashSet<Transform> protectedBones)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            RemoveEmptyHolders(t.GetChild(i), protectedBones);

        bool hasRenderer = t.GetComponent<Renderer>() || t.GetComponent<SkinnedMeshRenderer>();
        bool hasChildren = t.childCount > 0;
        bool isRoot = t.parent == null;
        
        if (!isRoot && !hasRenderer && !hasChildren && !protectedBones.Contains(t))
            Object.DestroyImmediate(t.gameObject);
    }
}
#endif
