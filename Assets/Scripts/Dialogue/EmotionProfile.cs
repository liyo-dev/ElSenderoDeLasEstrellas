using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que define cómo se mapean las emociones a meshes de ojos y boca.
/// Diseñado para personajes que usan GameObjects intercambiables (Eye01, Eye02, Mouth01, etc.)
/// </summary>
[CreateAssetMenu(fileName = "NewEmotionProfile", menuName = "El Sendero/Diálogos/Emotion Profile")]
public class EmotionProfile : ScriptableObject
{
    [Header("Configuración General")]
    [Tooltip("Tiempo de transición entre emociones (para futuras animaciones)")]
    [Range(0f, 1f)]
    public float transitionDuration = 0.1f;
    
    [Header("Mapeo de Emociones")]
    [Tooltip("Configuración de meshes faciales y animación corporal para cada emoción")]
    public EmotionMeshData[] emotions = new EmotionMeshData[]
    {
        new EmotionMeshData { emotion = NPCEmotion.Neutral,    eyeMeshName = "Eye01", mouthMeshName = "Mouth01", bodyAnimStateName = "" },
        new EmotionMeshData { emotion = NPCEmotion.Happy,      eyeMeshName = "Eye03", mouthMeshName = "Mouth03", bodyAnimStateName = "HeadNod01" },
        new EmotionMeshData { emotion = NPCEmotion.Sad,        eyeMeshName = "Eye02", mouthMeshName = "Mouth02", bodyAnimStateName = "Cry01" },
        new EmotionMeshData { emotion = NPCEmotion.Angry,      eyeMeshName = "Eye04", mouthMeshName = "Mouth04", bodyAnimStateName = "Angry02" },
        new EmotionMeshData { emotion = NPCEmotion.Surprised,  eyeMeshName = "Eye05", mouthMeshName = "Mouth05", bodyAnimStateName = "SenseSomethingStart_NoWeapon" },
        new EmotionMeshData { emotion = NPCEmotion.Scared,     eyeMeshName = "Eye06", mouthMeshName = "Mouth06", bodyAnimStateName = "Beg01" },
        new EmotionMeshData { emotion = NPCEmotion.Thinking,   eyeMeshName = "Eye07", mouthMeshName = "Mouth07", bodyAnimStateName = "Question01" },
        new EmotionMeshData { emotion = NPCEmotion.Tired,      eyeMeshName = "Eye08", mouthMeshName = "Mouth08", bodyAnimStateName = "IdleWounded01" },
        new EmotionMeshData { emotion = NPCEmotion.Smirk,      eyeMeshName = "Eye09", mouthMeshName = "Mouth09", bodyAnimStateName = "Laugh01" },
        new EmotionMeshData { emotion = NPCEmotion.Worried,    eyeMeshName = "Eye02", mouthMeshName = "Mouth08", bodyAnimStateName = "HeadShake02" },
        new EmotionMeshData { emotion = NPCEmotion.Determined, eyeMeshName = "Eye04", mouthMeshName = "Mouth01", bodyAnimStateName = "Challenging_NoWeapon" },
        new EmotionMeshData { emotion = NPCEmotion.Relieved,   eyeMeshName = "Eye03", mouthMeshName = "Mouth03", bodyAnimStateName = "Talk02" },
        new EmotionMeshData { emotion = NPCEmotion.Confused,   eyeMeshName = "Eye05", mouthMeshName = "Mouth07", bodyAnimStateName = "Question02" },
        new EmotionMeshData { emotion = NPCEmotion.Excited,    eyeMeshName = "Eye05", mouthMeshName = "Mouth03", bodyAnimStateName = "Cheer02" },
        new EmotionMeshData { emotion = NPCEmotion.Annoyed,    eyeMeshName = "Eye04", mouthMeshName = "Mouth07", bodyAnimStateName = "HeadShake01" },
        new EmotionMeshData { emotion = NPCEmotion.Grateful,   eyeMeshName = "Eye03", mouthMeshName = "Mouth09", bodyAnimStateName = "Reverence01" },
    };

    [Header("Animaciones Neutras (jugador)")]
    [Tooltip("Estados del Animator que se rotan cuando la emoción es None o Neutral")]
    public string[] neutralBodyAnims = { "Talk01", "Talk02", "Talk03" };
    
    /// <summary>
    /// Obtiene la configuración de meshes para una emoción específica.
    /// </summary>
    public EmotionMeshData GetEmotionData(NPCEmotion emotion)
    {
        if (emotions == null) return default;

        foreach (var data in emotions)
        {
            if (data.emotion == emotion)
                return data;
        }

        // FIX (17 sep 2026): antes se caía a emotions[0], que es "el primero de la lista", no
        // "neutral". En el perfil del juego el primero es Happy, así que una emoción sin mapear
        // ponía al NPC a SONREÍR — justo lo contrario de lo que suele pedir la línea que se le
        // ha quedado sin mapa. Ahora se busca Neutral de verdad y, si tampoco está, se devuelve
        // vacío, que el resto del sistema ya interpreta como "no cambies nada".
        foreach (var data in emotions)
        {
            if (data.emotion == NPCEmotion.Neutral)
                return data;
        }

        return default;
    }
}

/// <summary>
/// Datos de configuración para una emoción específica.
/// Define qué meshes de ojos y boca activar, y qué estado del Animator reproducir en el cuerpo.
/// </summary>
[Serializable]
public struct EmotionMeshData
{
    [Tooltip("La emoción que configura este dato")]
    public NPCEmotion emotion;

    [Header("Cara")]
    [Tooltip("Nombre del GameObject de ojos a activar (ej: 'Eye01', 'Eye03'). Vacío = no cambia, se mantienen los ojos que ya tenía el NPC.")]
    public string eyeMeshName;

    [Tooltip("Nombre del GameObject de boca a activar (ej: 'Mouth01', 'Mouth03'). Vacío = no cambia, se mantiene la boca que ya tenía el NPC.")]
    public string mouthMeshName;

    [Header("Animación Corporal (jugador)")]
    [Tooltip("Nombre del estado en el Animator Controller del jugador (ej: 'Cheer01', 'Cry01'). Vacío = no se reproduce ninguna animación, se mantiene el gesto/pose actual. Útil para emociones que solo deben cambiar la cara.")]
    public string bodyAnimStateName;

    [Tooltip("Otras animaciones válidas para esta misma emoción. Si hay alguna, el sistema va " +
             "rotando entre la principal y estas, de forma que dos NPCs enfadados (o el mismo dos " +
             "frases seguidas) no repiten el mismo gesto. Vacío = siempre la principal.")]
    public string[] bodyAnimVariants;

    /// Devuelve la animación que toca según el contador que lleve el llamador. Rota entre la
    /// principal y las variantes; sin variantes, siempre devuelve la principal. Es lo que convierte
    /// "esta emoción hace ESTE gesto" en "esta emoción tiene un repertorio".
    public string PickBodyAnim(int rotation)
    {
        if (bodyAnimVariants == null || bodyAnimVariants.Length == 0)
            return bodyAnimStateName;

        if (string.IsNullOrEmpty(bodyAnimStateName))
            return bodyAnimVariants[Mathf.Abs(rotation) % bodyAnimVariants.Length];

        int total = bodyAnimVariants.Length + 1;
        int index = Mathf.Abs(rotation) % total;
        return index == 0 ? bodyAnimStateName : bodyAnimVariants[index - 1];
    }
}

