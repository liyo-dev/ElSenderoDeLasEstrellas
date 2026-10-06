using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    
    [Header("Cara de reposo")]
    [Tooltip("Nombres de las mallas de ojos que se consideran neutras para la cara de reposo.")]
    public string[] ojosNeutros = { "Eye01", "Eye02", "Eye04", "Eye08" };
    [Tooltip("Nombres de las mallas de boca que se consideran neutras para la cara de reposo.")]
    public string[] bocasNeutras = { "Mouth01", "Mouth02" };
    [Tooltip("Ojos de reposo que se usan si no hay ojos asignados ni una malla neutra activa, siempre que exista en el personaje.")]
    public string ojosDeReposoPorDefecto = "Eye01";
    [Tooltip("Boca de reposo que se usa si no hay boca asignada ni una malla neutra activa, siempre que exista en el personaje.")]
    public string bocaDeReposoPorDefecto = "Mouth02";
    [Header("Reacciones")]
    [Tooltip("Segundos que dura la reacción antes de volver a reposo.")]
    [Min(0.1f)] public float segundosDeReaccion = 1.5f;
    [Header("Boca al hablar")]
    [Tooltip("Boca entreabierta al hablar, común a todas las emociones.")]
    [FormerlySerializedAs("bocaHablandoMedia")]
    public string bocaHablandoEntreabierta = "Mouth08";
    [Tooltip("Boca abierta al hablar, común a todas las emociones.")]
    public string bocaHablandoAbierta = "Mouth10";
    [Tooltip("Intervalo entre bocas sin voz, en segundos no escalados.")]
    [Min(0.01f)] public float segundosPorBoca = 0.08f;
    [Tooltip("Nivel RMS mínimo para la boca abierta.")]
    [Min(0f)] public float umbralVozAbierta = 0.08f;
    [Tooltip("Tiempo mínimo por boca al seguir la voz.")]
    [Min(0.01f)] public float tiempoMinimoPorBocaConVoz = 0.06f;

    [Header("Mapeo de Emociones")]
    [Tooltip("Configuración facial y corporal. Neutral usa la cara de reposo de cada personaje.")]
    public EmotionMeshData[] emotions = new EmotionMeshData[]
    {
        new EmotionMeshData { emotion = NPCEmotion.Neutral,    eyeMeshName = "", mouthMeshName = "", bodyAnimStateName = "" },
        new EmotionMeshData { emotion = NPCEmotion.Happy,      eyeMeshName = "Eye03", mouthMeshName = "Mouth03", bodyAnimStateName = "HeadNod01" },
        new EmotionMeshData { emotion = NPCEmotion.Sad,        eyeMeshName = "Eye02", mouthMeshName = "Mouth02", bodyAnimStateName = "Cry01" },
        new EmotionMeshData { emotion = NPCEmotion.Angry,      eyeMeshName = "Eye04", mouthMeshName = "Mouth04", bodyAnimStateName = "Angry02" },
        new EmotionMeshData { emotion = NPCEmotion.Surprised,  eyeMeshName = "Eye05", mouthMeshName = "Mouth05", bodyAnimStateName = "SenseSomethingStart_NoWeapon" },
        new EmotionMeshData { emotion = NPCEmotion.Scared,     eyeMeshName = "Eye06", mouthMeshName = "Mouth06", bodyAnimStateName = "Beg01" },
        new EmotionMeshData { emotion = NPCEmotion.Thinking,   eyeMeshName = "Eye07", mouthMeshName = "Mouth07", bodyAnimStateName = "Question01" },
        new EmotionMeshData { emotion = NPCEmotion.Tired,      eyeMeshName = "Eye08", mouthMeshName = "Mouth08", bodyAnimStateName = "IdleWounded01" },
        new EmotionMeshData { emotion = NPCEmotion.Smirk,      eyeMeshName = "", mouthMeshName = "Mouth11", bodyAnimStateName = "Laugh01" },
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
    
    public bool EsOjoNeutro(string nombre) => Contiene(ojosNeutros, nombre);
    public bool EsBocaNeutra(string nombre) => Contiene(bocasNeutras, nombre);
    private static bool Contiene(string[] nombres, string nombre)
    {
        if (nombres == null || string.IsNullOrEmpty(nombre)) return false;
        for (int i = 0; i < nombres.Length; i++)
            if (nombres[i] == nombre) return true;
        return false;
    }

    /// Busca la emoción; si falta devuelve Neutral y, si tampoco existe, default.
    public EmotionMeshData GetEmotionData(NPCEmotion emotion)
    {
        if (emotions == null) return default;

        foreach (var data in emotions)
        {
            if (data.emotion == emotion)
                return data;
        }

        // Busca Neutral si falta la emoción; si tampoco existe, devuelve default.
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
    [Tooltip("Nombre del GameObject de ojos a activar (ej: 'Eye01', 'Eye03'). Vacío = se usa la de la cara de reposo del personaje")]
    public string eyeMeshName;

    [Tooltip("Nombre del GameObject de boca a activar (ej: 'Mouth01', 'Mouth03'). Vacío = se usa la de la cara de reposo del personaje")]
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
