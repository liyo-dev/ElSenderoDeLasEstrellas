#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// Asigna la sonrisa de reposo de Will y actualiza su boca visible en los prefabs.
public static class AjusteCaraDeReposoWillINC564
{
    [MenuItem("El Sendero/Archivo/Diálogos/Aplicar cara de reposo alegre a Will INC-564")]
    public static void Aplicar()
    {
        Ajustar("Assets/Prefabs/_WILL.prefab", true);
        Ajustar("Assets/Prefabs/_WILL_NPC.prefab", false);
        AssetDatabase.SaveAssets();
    }

    private static void Ajustar(string ruta, bool jugador)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
        {
            Debug.LogWarning($"INC-564: no se encuentra el prefab {ruta}.");
            return;
        }

        GameObject raiz = null;
        try
        {
            raiz = PrefabUtility.LoadPrefabContents(ruta);
            var ojos = BuscarHijo(raiz.transform, "Eye01");
            var bocaAnterior = BuscarHijo(raiz.transform, "Mouth01");
            var sonrisa = BuscarHijo(raiz.transform, "Mouth02");
            if (ojos == null || bocaAnterior == null || sonrisa == null)
            {
                Debug.LogWarning($"INC-564: falta algún hijo Eye01, Mouth01 o Mouth02 en {ruta}; no se guarda.");
                return;
            }

            NPCEmotionController elegido = null;
            foreach (var controlador in raiz.GetComponentsInChildren<NPCEmotionController>(true))
            {
                var datos = new SerializedObject(controlador);
                var perfil = datos.FindProperty("emotionProfile");
                var boca = datos.FindProperty("bocaDeReposo");
                // Mouth02 permite repetir el menú sin seleccionar el componente vacío del jugador.
                if (jugador && (perfil.objectReferenceValue == null ||
                    (boca.objectReferenceValue != bocaAnterior && boca.objectReferenceValue != sonrisa)))
                    continue;
                if (elegido != null)
                {
                    Debug.LogWarning($"INC-564: hay varios controladores candidatos en {ruta}; no se guarda.");
                    return;
                }
                elegido = controlador;
            }
            if (elegido == null)
            {
                Debug.LogWarning($"INC-564: no se encuentra el controlador esperado en {ruta}; no se guarda.");
                return;
            }

            int campos = 0, mallas = 0;
            var serializado = new SerializedObject(elegido);
            var bocaReposo = serializado.FindProperty("bocaDeReposo");
            if (bocaReposo.objectReferenceValue != sonrisa)
            {
                bocaReposo.objectReferenceValue = sonrisa;
                campos++;
            }
            if (!jugador)
            {
                var ojosReposo = serializado.FindProperty("ojosDeReposo");
                if (ojosReposo.objectReferenceValue != ojos)
                {
                    ojosReposo.objectReferenceValue = ojos;
                    campos++;
                }
            }
            if (campos > 0)
            {
                serializado.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(elegido);
            }
            if (bocaAnterior.activeSelf)
            {
                bocaAnterior.SetActive(false);
                EditorUtility.SetDirty(bocaAnterior);
                mallas++;
            }
            if (!sonrisa.activeSelf)
            {
                sonrisa.SetActive(true);
                EditorUtility.SetDirty(sonrisa);
                mallas++;
            }
            if (campos + mallas > 0)
            {
                EditorUtility.SetDirty(raiz);
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta, out bool guardado);
                if (!guardado)
                {
                    Debug.LogWarning($"INC-564: no se ha podido guardar {ruta}.");
                    return;
                }
            }
            Debug.Log($"INC-564: {ruta}: {campos} campos de reposo y {mallas} estados de boca cambiados.");
        }
        catch (Exception error)
        {
            Debug.LogWarning($"INC-564: no se ha podido ajustar {ruta}: {error.Message}");
        }
        finally
        {
            if (raiz != null) PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static GameObject BuscarHijo(Transform padre, string nombre)
    {
        foreach (Transform hijo in padre)
        {
            if (hijo.name == nombre) return hijo.gameObject;
            var encontrado = BuscarHijo(hijo, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }
}
#endif
