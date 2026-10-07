using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Magia/Spell", fileName = "NewMagicSpell")]
public class MagicSpellSO : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("ID único del hechizo para identificarlo en el sistema.")]
    public SpellId    spellId      = SpellId.None;
    [Tooltip("ID de localización para el nombre (ej: 'SPELL_BOLA_FUEGO_NAME'). Si está vacío, usa displayName.")]
    public string     displayNameId;
    public string     displayName = "Fireball";
    public MagicKind  kind        = MagicKind.Projectile;
    public MagicElement element   = MagicElement.Fire;
    [Tooltip("Frase de la página del grimorio (INC-506). Vacío = solo lo que hace.")]
    [TextArea(2, 4)] public string lore = "";
    [Tooltip("Clave de localización de la frase del grimorio (opcional).")]
    public string loreId = "";

    /// <summary>Obtiene el nombre localizado del hechizo (usa displayNameId si está definido).</summary>
    public string GetLocalizedName()
    {
        if (!string.IsNullOrEmpty(displayNameId) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(displayNameId, displayName);
        return displayName;
    }
    [Header("Prefab en vuelo")]
    public GameObject prefab;
    
    [Header("Casting")]
    [Tooltip("Retrasa el disparo para sincronizar con la animación (segundos).")]
    [Min(0f)] public float castDelaySeconds = 0.15f;
    
    [Header("Carga (solo para especiales)")]
    [Tooltip("Si > 0, el proyectil aparecerá en la mano y crecerá durante este tiempo antes de dispararse (estilo Kamehameha).")]
    [Min(0f)] public float chargeTime = 0f;
    [Tooltip("Escala inicial del proyectil durante la carga (0.1 = 10% del tamaño normal).")]
    [Range(0.01f, 1f)] public float chargeStartScale = 0.1f;
    [Tooltip("Si está marcado, el proyectil seguirá la posición del origin durante la carga.")]
    public bool followOriginDuringCharge = true;

    [Header("Física / Vida")]
    public float initialSpeed = 18f;
    public bool  useGravity   = false;
    public float maxRange     = 40f;
    public float lifeTime     = 8f;

    [Header("Daño / Impacto")]
    public float     damage = 10f;
    public float     aoeRadius = 0f;
    public float     knockbackForce = 0f;
    public bool      destroyOnHit = true;

    [Header("Spawn / Dirección")]
    public float   forwardOffset = 0.35f;
    [Tooltip("Offset de posición adicional en espacio local (X=derecha, Y=arriba, Z=adelante). Útil para ajustar la altura de spawn y evitar que se vea metido en el suelo.")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 visualRotationOffsetEuler = Vector3.zero;
    [Tooltip("Sale en horizontal. Los proyectiles con objetivo lo ignoran y van a su centro; se aplica sin objetivo, con dirección forzada (cinemáticas) y a los hechizos de zona.")]
    public bool    flattenDirection = true;
    [Tooltip("Si se marca, se forzará esta escala al instanciar el proyectil y el VFX de spawn. Si está desmarcado, se usará la escala del prefab.")]
    public bool     useScaleOverride = false;
    public Vector3  scaleOverride = Vector3.one;

    [Header("Coste")]
    [Tooltip("Maná por lanzamiento. El ritmo lo marcan el maná y el gesto (castDelaySeconds + chargeTime): los hechizos no tienen enfriamiento propio.")]
    public float manaCost = 5f;

    [Header("Modo preciso (Paso 5 del refactor Tramo 1, mantener el botón)")]
    [Tooltip("Si está activo, mantener pulsada la X (en vez de tocarla) lanza la variante precisa: más fina, más rápida y más débil. La decide PlayerCombatInput al soltar.")]
    public bool supportsPreciseMode = false;
    [Tooltip("Segundos que hay que mantener pulsado antes de comprometerse al modo preciso. Por " +
             "debajo de esto, soltar lanza el hechizo normal -- un toque sigue siendo un toque.")]
    [Min(0f)] public float preciseHoldThreshold = 0.18f;
    [Tooltip("Multiplicador de daño de la variante precisa sobre 'damage'.")]
    [Min(0f)] public float preciseDamageMultiplier = 0.5f;
    [Tooltip("Multiplicador de velocidad de la variante precisa sobre 'initialSpeed'.")]
    [Min(0f)] public float preciseSpeedMultiplier = 1.3f;
    [Tooltip("Multiplicador de escala visual de la variante precisa (se aplica sobre " +
             "'scaleOverride' si 'useScaleOverride' está activo, o sobre la escala 1 del prefab " +
             "si no).")]
    [Min(0.01f)] public float preciseScaleMultiplier = 0.4f;

    /// Marca, SOLO en el clon devuelto por BuildPreciseVariant(), que este MagicSpellSO en
    /// concreto es la variante precisa de un disparo -- 'supportsPreciseMode' no sirve para esto
    /// porque Instantiate() lo copia igual en el clon (sigue siendo true en ambos), así que no
    /// distingue "este hechizo admite modo preciso" de "este disparo concreto es el preciso".
    /// MagicProjectileSpawner lee este campo al construir el ProjectileConfig del proyectil
    /// (Paso 6 del refactor Tramo 1, RuneCollar: el aro del demonio solo se rompe con disparos que
    /// lleguen con MagicProjectile.IsPrecise = true). [System.NonSerialized] porque es un dato de
    /// instancia en memoria, nunca algo que deba guardarse en el asset .asset original.
    [System.NonSerialized] public bool isRuntimePreciseInstance = false;

    /// Crea, en runtime, una copia de este hechizo con daño/velocidad/escala ajustados según los
    /// multiplicadores 'precise*' de arriba -- para MagicCaster.CastResolvedSpell(slot, precise:
    /// true). No toca el asset original (Instantiate crea un ScriptableObject nuevo en memoria,
    /// se descarta solo tras el disparo). Si 'supportsPreciseMode' está desactivado, devuelve
    /// this sin clonar -- no debería llamarse en ese caso, pero así no rompe nada si se hiciera.
    public MagicSpellSO BuildPreciseVariant()
    {
        if (!supportsPreciseMode) return this;

        var clone = Instantiate(this);
        clone.damage = damage * preciseDamageMultiplier;
        clone.initialSpeed = initialSpeed * preciseSpeedMultiplier;
        clone.useScaleOverride = true;
        clone.scaleOverride = (useScaleOverride ? scaleOverride : Vector3.one) * preciseScaleMultiplier;
        clone.isRuntimePreciseInstance = true;
        return clone;
    }

    [Header("Zona (solo para MagicKind.Zone)")]
    [Tooltip("Radio (metros) del área de efecto de la zona.")]
    [Min(0.1f)] public float zoneRadius = 4f;
    [Tooltip("Cuánto tiempo (segundos) permanece la zona activa antes de desaparecer.")]
    [Min(0.1f)] public float zoneDuration = 5f;
    [Tooltip("Cada cuántos segundos la zona aplica daño a quien esté dentro (usa 'damage' de arriba como daño por tick).")]
    [Min(0.05f)] public float zoneTickInterval = 0.5f;
    [Tooltip("Distancia por defecto delante del lanzador donde aparece la zona si no hay un objetivo fijado (o si 'zoneSnapToTarget' está desactivado).")]
    [Min(0.5f)] public float zoneRange = 8f;
    [Tooltip("Si hay un objetivo fijado (PlayerTargeting), la zona aparece centrada en él en vez de a 'zoneRange' delante del lanzador.")]
    public bool zoneSnapToTarget = true;
    [Tooltip("Capas contra las que se lanza el raycast hacia abajo para apoyar la zona sobre el suelo real (evita que quede flotando en terreno irregular).")]
    public LayerMask zoneGroundLayers = ~0;
    [Tooltip("Elevación (metros) sobre el punto de suelo detectado. Sin esto el VFX queda exactamente a la altura del suelo y se mezcla/hace z-fighting con él (mismo problema visual que tenían los puntos de guardado).")]
    [Min(0f)] public float zoneGroundOffset = 0.15f;

    [Header("Levitación (solo para MagicKind.Levitation)")]
    [Tooltip("Rango máximo para detectar y afectar NPCs con levitación.")]
    [Min(0f)] public float levitationRange = 10f;
    [Tooltip("Ángulo de detección en grados (cono frontal del jugador).")]
    [Range(1f, 180f)] public float levitationAngle = 45f;
    [Tooltip("Fuerza de atracción hacia el jugador durante la fase de carga (mantener botón).")]
    [Min(0f)] public float levitationPullForce = 5f;
    [Tooltip("Fuerza de repulsión al soltar el botón.")]
    [Min(0f)] public float levitationPushForce = 15f;
    [Tooltip("Distancia delante del jugador donde se posiciona el NPC levitado.")]
    [Min(1f)] public float levitationHoldDistance = 3f;
    [Tooltip("Altura a la que se eleva el NPC durante la levitación.")]
    [Min(0f)] public float levitationHeight = 2f;
    [Tooltip("Velocidad de elevación del NPC.")]
    [Min(0f)] public float levitationLiftSpeed = 3f;
    [Tooltip("Layers que pueden ser afectados por levitación.")]
    public LayerMask levitationTargetLayers = ~0;
    [Tooltip("Tiempo en segundos antes de empezar a drenar maná mientras se mantiene la levitación.")]
    [Min(0f)] public float levitationDrainDelay = 1f;
    [Tooltip("Maná drenado por segundo mientras se mantiene la levitación (después del delay).")]
    [Min(0f)] public float levitationManaDrainPerSecond = 5f;
    
    [Header("Levitación - VFX")]
    [Tooltip("VFX que aparece en el jugador mientras mantiene la levitación.")]
    public GameObject levitationHoldVFX;
    [Tooltip("VFX que aparece al soltar/lanzar el NPC.")]
    public GameObject levitationReleaseVFX;
    [Tooltip("VFX para mostrar el área de detección (círculos de purpurina).")]
    public GameObject levitationRangeIndicatorVFX;
    [Tooltip("Cantidad de círculos de VFX para el indicador de rango.")]
    [Range(1, 10)] public int rangeIndicatorCount = 3;
    
    [Header("Levitación - Feedback")]
    [Tooltip("Intensidad del camera shake al capturar un NPC.")]
    [Min(0f)] public float levitationCaptureShakeIntensity = 0.3f;
    [Tooltip("Duración del camera shake al capturar.")]
    [Min(0f)] public float levitationCaptureShakeDuration = 0.2f;
    [Tooltip("Intensidad del camera shake al soltar/lanzar un NPC.")]
    [Min(0f)] public float levitationReleaseShakeIntensity = 0.5f;
    [Tooltip("Duración del camera shake al soltar.")]
    [Min(0f)] public float levitationReleaseShakeDuration = 0.3f;

    [Header("Levitación - Daño de Impacto")]
    [Tooltip("Daño base aplicado al NPC al impactar contra una superficie tras ser lanzado.")]
    [Min(0f)] public float levitationImpactDamage = 25f;
    [Tooltip("Velocidad mínima de impacto (m/s) para que se aplique daño. Por debajo de este valor no hay daño.")]
    [Min(0f)] public float levitationImpactMinSpeed = 4f;

    [Header("VFX (centralizado)")]
    public GameObject spawnVFX;
    public GameObject impactVFX;
    public GameObject despawnVFX;
    [Tooltip("Tiempo en segundos antes de destruir los VFX automáticamente. Si es 0, no se destruirán (para VFX con ParticleSystem que se autodestruyen).")]
    [Min(0f)] public float vfxLifetime = 3f;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al lanzar el hechizo (ej: 'Spell_Fire', 'Spell_Ice')")]
    public string castSFXKey;
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al impactar/explotar (ej: 'Spell_Fire_Explosion')")]
    public string impactSFXKey;

    [Header("Reglas de slot")]
    public SpellSlotType slotType = SpellSlotType.Any;

    [Header("UI")]
    [Tooltip("Icono que se mostrará en el HUD cuando este hechizo esté equipado.")]
    public Sprite attackIcon;

    [Header("Proyectil mejorado (INC-497)")]
    [Tooltip("Veces que salta a otro enemigo cercano tras impactar (0 = no rebota).")]
    [Min(0)] public int bounceCount = 0;
    [Tooltip("Distancia máxima a la que busca el siguiente enemigo al rebotar.")]
    [Min(0.5f)] public float bounceRange = 8f;
    [Tooltip("Enemigos que atraviesa antes de desaparecer (0 = ninguno, -1 = todos los de su camino).")]
    [Min(-1)] public int pierceCount = 0;
    [Tooltip("Proyectiles que salen a la vez, en abanico (1 = uno solo).")]
    [Min(1)] public int spreadCount = 1;
    [Tooltip("Ángulo total del abanico, en grados.")]
    [Range(0f, 90f)] public float spreadAngle = 24f;
    [Tooltip("Hechizo de zona (MagicKind.Zone) que deja en el suelo al impactar. Vacío = nada.")]
    public MagicSpellSO impactZone;

    [Header("Remate aéreo (INC-652)")]
    [Tooltip("Zona (MagicKind.Zone) que deja el tercer golpe de la serie de la X lanzado en el aire: el personaje da una voltereta, el hechizo sale en picado y la zona aparece donde cae. Vacío = el tercer golpe normal.")]
    public MagicSpellSO remateAereo;

    [Header("Gesto al lanzarlo (combos y centro)")]
    [Tooltip("Hand: el gesto de siempre. TwoHanded: a dos manos. Omni: en todas direcciones. Call: invocación. Area: saltito y brazos arriba (hechizos de zona). La serie de la X usa siempre derecha/izquierda/centro.")]
    public MagicCastStyle castStyle = MagicCastStyle.Hand;

    [Header("Estado que pone (INC-499)")]
    [Tooltip("Ralentizar, inmovilizar o atraer a los enemigos que alcanza (proyectil: al que impacta; zona: a los de dentro en cada tick).")]
    public EstadoDeCombate statusEffect = EstadoDeCombate.Ninguno;
    [Tooltip("Segundos que dura. En una zona se renueva en cada tick mientras sigan dentro. A los jefes les dura la mitad.")]
    [Min(0f)] public float statusDuration = 2f;
    [Tooltip("Ralentizar: fracción de velocidad que les queda (0,4 = al 40 %). Atraer: metros por segundo hacia el centro. Inmovilizar: no se usa.")]
    [Min(0f)] public float statusStrength = 0.5f;
    [Tooltip("Efecto que lleva encima el enemigo mientras dura el estado (opcional).")]
    public GameObject statusVFX;

    [Header("Apoyo y zona en el lanzador (INC-500, INC-501)")]
    [Tooltip("La zona aparece en quien lo lanza, no delante ni sobre el objetivo (Nova de Luz, Brisa Sanadora).")]
    public bool zoneOnCaster = false;
    [Tooltip("Vida que recupera en cada tick cada miembro del grupo que esté dentro de la zona.")]
    [Min(0f)] public float healPerTick = 0f;
    [Tooltip("Segundos de escudo que da al grupo que esté dentro de la zona al aparecer (0 = ninguno).")]
    [Min(0f)] public float groupShieldSeconds = 0f;
    [Tooltip("Parte del golpe que pasa con el escudo puesto (0,3 = el 30 %).")]
    [Range(0f, 1f)] public float groupShieldDamageFactor = 0.3f;
    [Tooltip("Efecto que lleva cada uno mientras dura el escudo.")]
    public GameObject groupShieldVFX;
    [Tooltip("Tramos de carga de equipo que da al curar o proteger a alguien (una vez por lanzamiento).")]
    [Min(0f)] public float teamGaugeGain = 0f;

    [Header("Paso corto (INC-502)")]
    [Tooltip("Metros que avanza el teletransporte (MagicKind.Teleport). Se queda antes si hay pared o no hay suelo.")]
    [Min(1f)] public float teleportDistance = 6f;

    [Header("Quién lo lanza")]
    [Tooltip("Personaje al que pertenece el hechizo. Los combos solo salen con ese personaje al mando (Will: los desbloqueados en su preset; Estela y Liam: los de su ficha).")]
    public PartyControlManager.CharacterSlot caster = PartyControlManager.CharacterSlot.Will;

    [Header("Combo mágico (Y)")]
    [Tooltip("Secuencia de botones que lo lanza con la Y (vacía = no es combo). Ninguna secuencia puede ser el principio de otra: lo comprueba 'El Sendero/Combate/Comprobar secuencias de combo'.")]
    public ComboButton[] comboSequence = new ComboButton[0];

    /// <summary>Tiene secuencia de combo.</summary>
    public bool HasCombo => comboSequence != null && comboSequence.Length > 0;
}
