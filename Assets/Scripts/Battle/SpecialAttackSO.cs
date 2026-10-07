using UnityEngine;

/// <summary>Compañero de un dúo antiguo (solo con Will). Se conserva para convertir los assets
/// anteriores a INC-491; los ataques nuevos usan <see cref="SpecialAttackSO.first"/>/<see cref="SpecialAttackSO.second"/>.</summary>
public enum DuoCompanion { Estela, Liam }

/// <summary>
/// Ataque de equipo (INC-491): un dúo entre dos personajes cualesquiera del grupo o el trío de los
/// tres. Lo lanza <see cref="DuoSpecialAttackSystem"/> con LT/RT (dúo, 1 tramo de la carga de
/// equipo) o LT+RT (trío, la carga entera).
/// </summary>
[CreateAssetMenu(fileName = "SpecialAttack_", menuName = "El Sendero/Batalla/Ataque de equipo (dúo o trío)")]
public class SpecialAttackSO : ScriptableObject
{
    [Header("Identidad")]
    public string displayName = "Ataque Especial";
    public Sprite icon;

    [Header("Quién lo hace")]
    [Tooltip("Trío: lo hacen los tres (Will, Estela y Liam) y gasta la carga entera. Si no, es un dúo entre 'first' y 'second'.")]
    public bool isTrio;
    [Tooltip("Primer miembro del dúo. El orden no importa.")]
    public PartyControlManager.CharacterSlot first = PartyControlManager.CharacterSlot.Will;
    [Tooltip("Segundo miembro del dúo. El orden no importa.")]
    public PartyControlManager.CharacterSlot second = PartyControlManager.CharacterSlot.Estela;

    [HideInInspector] public DuoCompanion companion = DuoCompanion.Estela; // solo para convertir assets antiguos

    [Header("Requisito de proximidad")]
    [Tooltip("Distancia máxima a la que debe estar cada compañero del personaje activo")]
    public float companionMaxDistance = 10f;

    [Header("Animaciones")]
    [Tooltip("Trigger del Animator para los compañeros (el personaje activo hace el gesto del centro de MagicCaster)")]
    public string companionAnimationTrigger;
    [Tooltip("Sin uso desde INC-491: el personaje activo hace el gesto del centro de MagicCaster.")]
    public string playerAnimationTrigger;
    [Tooltip("Segundos que el personaje activo queda comprometido con el gesto (sin moverse, mirando al objetivo).")]
    public float gestureSeconds = 0.6f;

    [Header("Visual y Audio")]
    public GameObject vfxPrefab;
    [Tooltip("Clave de SFX en AudioService")]
    public string castSFXKey;
    [Tooltip("Duración del VFX antes de destruirse")]
    public float vfxLifetime = 3f;
    [Tooltip("Segundos de delay entre animación y aplicación de daño")]
    public float damageDelay = 0.4f;

    [Header("Voltereta del personaje activo (INC-654)")]
    [Tooltip("El personaje activo salta con una voltereta hacia el golpe; el efecto y el daño llegan al aterrizar (en vez de tras 'damageDelay'). Solo si tiene VolteretaDelJugador y está en el suelo.")]
    public bool volteretaDelActivo;
    [Tooltip("Altura de la voltereta, en alturas de la cabeza del personaje.")]
    [Min(0f)] public float alturaDeLaVoltereta = 1.2f;
    [Tooltip("Metros máximos que avanza hacia el punto del golpe (se queda a 1,5 m de él).")]
    [Min(0f)] public float avanceDeLaVoltereta = 3f;

    [Header("Daño")]
    public float damage = 80f;
    public float aoeRadius = 4f;
    public float knockbackForce = 18f;

    [Header("Cámara")]
    public bool shakeCameraOnCast = true;

    /// <summary>¿Lo hacen justo estos dos personajes (en cualquier orden)?</summary>
    public bool IsDuoOf(PartyControlManager.CharacterSlot a, PartyControlManager.CharacterSlot b)
        => !isTrio && ((first == a && second == b) || (first == b && second == a));
}
