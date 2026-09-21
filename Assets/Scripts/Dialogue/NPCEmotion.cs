using UnityEngine;

/// <summary>
/// Enum que define las emociones disponibles para los NPCs durante los diálogos.
/// Cada emoción activa meshes específicos de ojos y boca (Eye01, Mouth01, etc.)
/// </summary>
public enum NPCEmotion
{
    [Tooltip("Sin cambio de expresión - mantiene la expresión actual")]
    None = -1,
    
    [Tooltip("Expresión neutral/relajada")]
    Neutral = 0,
    
    [Tooltip("Feliz - sonrisa y ojos alegres")]
    Happy = 1,
    
    [Tooltip("Triste - expresión decaída")]
    Sad = 2,
    
    [Tooltip("Enfadado - ceño fruncido")]
    Angry = 3,
    
    [Tooltip("Sorprendido - ojos abiertos y boca en O")]
    Surprised = 4,
    
    [Tooltip("Asustado/Preocupado - expresión de miedo")]
    Scared = 5,
    
    [Tooltip("Pensativo - expresión reflexiva")]
    Thinking = 6,
    
    [Tooltip("Cansado - ojos caídos")]
    Tired = 7,
    
    [Tooltip("Sonrisa maliciosa/pícara")]
    Smirk = 8,

    // ── Ampliación 17 sep 2026 ────────────────────────────────────────────────────────────────
    // Los valores se AÑADEN al final y nunca se reordenan: el enum se serializa por número, así
    // que renumerar una emoción existente cambiaría en silencio la cara de todos los diálogos y
    // secuencias que ya la usan.
    //
    // El criterio para que una emoción entre aquí es que se pueda DISTINGUIR de las demás con las
    // animaciones que existen en NPC_NoWeapon: media docena de emociones que acaban todas en el
    // mismo HeadShake no son variedad, son ruido en el desplegable.

    [Tooltip("Preocupado - inquieto pero sin llegar al pánico. Es lo que suele querer decir un " +
             "diálogo cuando se pone 'Scared', que es mucho más fuerte de lo que parece.")]
    Worried = 9,

    [Tooltip("Decidido - se planta, ha tomado una decisión. El momento de 'voy a hacerlo'.")]
    Determined = 10,

    [Tooltip("Aliviado - se le va la tensión de encima, ha salido bien.")]
    Relieved = 11,

    [Tooltip("Confuso - no entiende qué está pasando. Distinto de Thinking, que es reflexionar.")]
    Confused = 12,

    [Tooltip("Emocionado - ilusión, ganas. Happy con energía.")]
    Excited = 13,

    [Tooltip("Molesto - fastidiado, a regañadientes. El escalón por debajo de Angry.")]
    Annoyed = 14,

    [Tooltip("Agradecido - da las gracias, reconoce un favor. Cierre natural de una misión.")]
    Grateful = 15
}

