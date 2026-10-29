using UnityEngine;

/// <summary>Compañero de un dúo antiguo (solo con Will). Se conserva para convertir los assets
/// anteriores a INC-491; los ataques nuevos usan <see cref="SpecialAttackSO.first"/>/<see cref="SpecialAttackSO.second"/>.</summary>
public enum DuoCompanion { Estela, Liam }

/// <summary>Puesta en escena de un ataque de equipo en <see cref="DuoSpecialAttackSystem"/>.</summary>
public enum CoreografiaDeEquipo
{
    [Tooltip("Asset aún sin coreografía: la toma de su pareja al cargarse (ver SpecialAttackSO).")]
    SinDefinir = 0,
    [Tooltip("Corren, saltan y quedan suspendidos espalda con espalda en el centro del círculo mientras cae el hechizo. Remate sísmico.")]
    EnElAire = 1,
    [Tooltip("Cara a cara en la pose de la Y; alzan las manos y cae el hechizo. Remate sísmico.")]
    CaraACara = 2,
    [Tooltip("Cara a cara; sobre sus cabezas se abre un agujero negro que atrae a los enemigos. Remate de supernova.")]
    AgujeroNegro = 3,
    [Tooltip("Los tres: caras en las franjas, salto en cámara lenta y aterrizaje con la pose de cada uno. Remate de cataclismo.")]
    Trio = 4,
}

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
    [Tooltip("Si se rellena, el compañero reproduce su gesto de hechizo especial; el personaje activo usa MagicCaster.")]
    public string companionAnimationTrigger;
    [Tooltip("Sin uso desde INC-491: el personaje activo hace el gesto del centro de MagicCaster.")]
    public string playerAnimationTrigger;
    [Tooltip("Segundos que el personaje activo queda comprometido con el gesto (sin moverse, mirando al objetivo).")]
    public float gestureSeconds = 0.6f;

    [Header("Coreografía")]
    [Tooltip("Cómo se pone en escena el ataque (y con qué remate acaba).")]
    public CoreografiaDeEquipo coreografia = CoreografiaDeEquipo.SinDefinir;
    [Tooltip("Clave de SFX en AudioService cuando aparece el hechizo (meteoros, agujero negro). Vacío = sin sonido.")]
    public string sfxDelHechizo = "SpecialAttack_Meteoros";
    [Tooltip("Clave de SFX en AudioService del remate final. Vacío = sin sonido.")]
    public string sfxDelImpacto = "SpecialAttack_Impacto";
    [Tooltip("Prefijo del nombre del hijo del VFX que es el agujero negro (coreografía Agujero negro): se sube sobre la cabeza de los dos.")]
    public string hijoDelAgujeroEnElVfx = "VFX_AgujeroNegro";

    [Header("Visual y Audio")]
    public GameObject vfxPrefab;
    [Tooltip("Altura del origen visual sobre el punto de impacto. No altera el centro del daño.")]
    [Min(0f)] public float elevacionDelVfx = 0.12f;
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
    [Tooltip("Metros máximos que avanza hacia el punto del golpe (sin superar el punto del impacto).")]
    [Min(0f)] public float avanceDeLaVoltereta = 3f;

    [Header("Daño")]
    public float damage = 80f;
    public float aoeRadius = 4f;
    public float knockbackForce = 18f;

    [Header("Efecto en cada enemigo alcanzado")]
    [Tooltip("Estado que deja en cada enemigo alcanzado. Ninguno = solo daño.")]
    public EstadoDeCombate statusEffect = EstadoDeCombate.Ninguno;
    [Tooltip("Segundos que dura el estado.")]
    [Min(0f)] public float statusDuration;
    [Tooltip("Intensidad del estado: al ralentizar, la fracción de velocidad que le queda; al empujar o atraer, metros por segundo.")]
    public float statusStrength;
    [Tooltip("Segundos que abre la protección de lo que alcanza (IProteccionQueSeAbre). 0 = no abre nada.")]
    [Min(0f)] public float abreProteccionSegundos;

    [Header("Cámara")]
    public bool shakeCameraOnCast = true;

    /// <summary>¿Lo hacen justo estos dos personajes (en cualquier orden)?</summary>
    public bool IsDuoOf(PartyControlManager.CharacterSlot a, PartyControlManager.CharacterSlot b)
        => !isTrio && ((first == a && second == b) || (first == b && second == a));

    void OnEnable()
    {
        if (coreografia == CoreografiaDeEquipo.SinDefinir) TomarCoreografiaDeLaPareja();
    }

    /// <summary>
    /// Un asset sin coreografía guardada (<see cref="CoreografiaDeEquipo.SinDefinir"/>) toma la
    /// de su pareja, con su sonido y su efecto en los enemigos: Will y Estela en el aire y
    /// ralentizan; Will y Liam con el agujero negro y empujan; Estela y Liam cara a cara, abren
    /// protecciones e inmovilizan; el trío abre e inmoviliza más tiempo. Guardar el asset deja los
    /// valores escritos y desde entonces mandan los del inspector.
    /// </summary>
    private void TomarCoreografiaDeLaPareja()
    {
        const PartyControlManager.CharacterSlot Will = PartyControlManager.CharacterSlot.Will;
        const PartyControlManager.CharacterSlot Estela = PartyControlManager.CharacterSlot.Estela;
        const PartyControlManager.CharacterSlot Liam = PartyControlManager.CharacterSlot.Liam;

        if (isTrio)
        {
            coreografia = CoreografiaDeEquipo.Trio;
            Efecto(EstadoDeCombate.Inmovilizar, 4f, 0f, 8f);
        }
        else if (IsDuoOf(Will, Estela))
        {
            coreografia = CoreografiaDeEquipo.EnElAire;
            Efecto(EstadoDeCombate.Ralentizar, 4f, 0.4f, 0f);
        }
        else if (IsDuoOf(Will, Liam))
        {
            coreografia = CoreografiaDeEquipo.AgujeroNegro;
            sfxDelHechizo = "SpecialAttack_AgujeroNegro";
            Efecto(EstadoDeCombate.Empujar, 1.2f, 26f, 0f);
        }
        else
        {
            coreografia = CoreografiaDeEquipo.CaraACara;
            Efecto(EstadoDeCombate.Inmovilizar, 3.5f, 0f, 6f);
        }

        void Efecto(EstadoDeCombate estado, float segundos, float fuerza, float proteccion)
        {
            statusEffect = estado;
            statusDuration = segundos;
            statusStrength = fuerza;
            abreProteccionSegundos = proteccion;
        }
    }
}
