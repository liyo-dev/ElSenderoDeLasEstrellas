using UnityEditor;

/// <summary>Completa los mapas de habla sin sustituir las elecciones del perfil.</summary>
public static class RellenarBocasAlHablar
{
    private static readonly EmotionMeshData[] Tabla =
    {
        new EmotionMeshData { emotion = NPCEmotion.Neutral, bocaHablandoCerrada = "Mouth02", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Determined, bocaHablandoCerrada = "Mouth02", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Happy, bocaHablandoCerrada = "Mouth03", bocaHablandoMedia = "Mouth09", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Excited, bocaHablandoCerrada = "Mouth03", bocaHablandoMedia = "Mouth09", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Grateful, bocaHablandoCerrada = "Mouth03", bocaHablandoMedia = "Mouth09", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Relieved, bocaHablandoCerrada = "Mouth03", bocaHablandoMedia = "Mouth09", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Sad, bocaHablandoCerrada = "Mouth05", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth07" },
        new EmotionMeshData { emotion = NPCEmotion.Angry, bocaHablandoCerrada = "Mouth05", bocaHablandoMedia = "Mouth06", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Annoyed, bocaHablandoCerrada = "Mouth05", bocaHablandoMedia = "Mouth06", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Surprised, bocaHablandoCerrada = "Mouth08", bocaHablandoMedia = "Mouth07", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Scared, bocaHablandoCerrada = "Mouth05", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth07" },
        new EmotionMeshData { emotion = NPCEmotion.Worried, bocaHablandoCerrada = "Mouth05", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth07" },
        new EmotionMeshData { emotion = NPCEmotion.Thinking, bocaHablandoCerrada = "Mouth11", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Confused, bocaHablandoCerrada = "Mouth11", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth10" },
        new EmotionMeshData { emotion = NPCEmotion.Tired, bocaHablandoCerrada = "Mouth12", bocaHablandoMedia = "Mouth08", bocaHablandoAbierta = "Mouth08" },
        new EmotionMeshData { emotion = NPCEmotion.Smirk, bocaHablandoCerrada = "Mouth03", bocaHablandoMedia = "Mouth06", bocaHablandoAbierta = "Mouth10" },
    };

    [MenuItem("El Sendero/Diálogos/Rellenar bocas al hablar (tabla por defecto)")]
    public static void Rellenar()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EmotionProfile"))
        {
            var perfil = AssetDatabase.LoadAssetAtPath<EmotionProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (perfil == null || perfil.emotions == null) continue;
            bool cambiado = false;
            for (int i = 0; i < perfil.emotions.Length; i++)
            {
                var datos = perfil.emotions[i];
                foreach (var fila in Tabla)
                {
                    if (datos.emotion != fila.emotion) continue;
                    bool vaciaCerrada = string.IsNullOrEmpty(datos.bocaHablandoCerrada);
                    bool vaciaMedia = string.IsNullOrEmpty(datos.bocaHablandoMedia);
                    bool vaciaAbierta = string.IsNullOrEmpty(datos.bocaHablandoAbierta);
                    if (!vaciaCerrada && !vaciaMedia && !vaciaAbierta) break;
                    if (!cambiado) Undo.RecordObject(perfil, "Rellenar bocas al hablar");
                    if (vaciaCerrada) datos.bocaHablandoCerrada = fila.bocaHablandoCerrada;
                    if (vaciaMedia) datos.bocaHablandoMedia = fila.bocaHablandoMedia;
                    if (vaciaAbierta) datos.bocaHablandoAbierta = fila.bocaHablandoAbierta;
                    perfil.emotions[i] = datos;
                    cambiado = true;
                    break;
                }
            }
            if (cambiado) EditorUtility.SetDirty(perfil);
        }
        AssetDatabase.SaveAssets();
    }
}
