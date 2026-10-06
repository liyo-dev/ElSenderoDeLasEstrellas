#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Ajusta el perfil de caras mediante AssetDatabase.
public static class AjusteCarasINC560
{
    [MenuItem("El Sendero/Archivo/Diálogos/Aplicar ajuste de caras INC-560")]
    public static void Aplicar()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<EmotionProfile>("Assets/_EmotionProfile/NpcEmotionProfile.asset");
        if (perfil == null || perfil.emotions == null)
        {
            Debug.LogWarning("No se encuentra el perfil de emociones o su tabla.");
            return;
        }
        Undo.RecordObject(perfil, "Ajustar caras de reposo");
        for (int i = 0; i < perfil.emotions.Length; i++)
        {
            var dato = perfil.emotions[i];
            if (dato.emotion == NPCEmotion.Smirk)
            {
                dato.eyeMeshName = "";
                dato.mouthMeshName = "Mouth11";
            }
            else if (dato.emotion == NPCEmotion.Neutral)
            {
                dato.eyeMeshName = "";
                dato.mouthMeshName = "";
            }
            perfil.emotions[i] = dato;
        }
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        Debug.Log("INC-560: ajuste de caras aplicado al perfil.");
    }
}
#endif