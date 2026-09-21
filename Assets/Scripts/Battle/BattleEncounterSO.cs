using UnityEngine;

/// <summary>
/// Configuración de un encuentro de batalla (jefe/enemigo) pensada para asignarse directamente
/// en el campo <c>encounter</c> de <c>StartBattleNode</c> (INC-207).
///
/// Cuando el nodo lleva una de estas SO asignada, sus datos (prefab, nombre, radio de la arena,
/// VFX de aparición) sustituyen a los campos configurados a mano en el <c>BossArenaController</c>
/// de la escena — la escena solo aporta el ancla espacial mínima (battleId + enlace a RoomGoal).
/// Si el nodo NO lleva ninguna SO asignada, la arena sigue funcionando exactamente igual que
/// antes (compatibilidad total con las arenas ya construidas en escena: Demon1, Demon2, Golem).
/// </summary>
[CreateAssetMenu(fileName = "BattleEncounter", menuName = "El Sendero/Batalla/Encuentro Enemigo", order = 1)]
public class BattleEncounterSO : ScriptableObject
{
    [Header("Enemigo")]
    [Tooltip("Prefab del enemigo/jefe a instanciar al arrancar la batalla.")]
    public GameObject enemyPrefab;

    [Tooltip("ID de localización para el nombre mostrado en la presentación (ej: 'BOSS_DEMONIO_NAME'). Si está vacío o no resuelve, se usa displayName.")]
    public string displayNameId;

    [Tooltip("Nombre a mostrar en la presentación si displayNameId no resuelve.")]
    public string displayName = "ENEMIGO";

    [Header("Arena (radio alrededor del jugador — INC-207)")]
    [Tooltip("Radio en metros de la arena, medido desde la posición del jugador en el instante en que empieza la batalla (no se recalcula después).")]
    [Min(1f)]
    public float arenaRadius = 15f;

    [Tooltip("Capa de suelo usada para asentar al enemigo en el terreno tras calcular su posición (raycast hacia abajo). Si se deja en 'Nothing', se usa la capa de suelo configurada en el BossArenaController de la escena.")]
    public LayerMask floorLayer;

    [Header("Perfiles de spawn")]
    [Tooltip("Dónde aparece el enemigo dentro del radio de la arena. Pensado para poder variar por dificultad más adelante (varios perfiles, seleccionables por índice desde el nodo) sin tocar código.")]
    public SpawnProfile[] spawnProfiles = new SpawnProfile[]
    {
        new SpawnProfile { label = "Por defecto", distanceFactor = 0.85f, angleDegrees = 0f, relativeToPlayerFacing = true }
    };

    [Header("VFX de aparición (opcional)")]
    [Tooltip("Si se deja vacío, se usa el VFX configurado en el BossArenaController de la escena (si tiene uno).")]
    public GameObject spawnVfxPrefab;
    [Min(0f)] public float spawnVfxDuration = 2f;
    public float spawnVfxHeightOffset = 0f;

    [Tooltip("Si se deja vacío, se usa el portal configurado en el BossArenaController de la escena (si tiene uno).")]
    public GameObject portalPrefab;

    [System.Serializable]
    public struct SpawnProfile
    {
        public string label;

        [Tooltip("Fracción del radio de la arena a la que aparece el enemigo (0 = centro/jugador, 1 = borde del radio).")]
        [Range(0f, 1f)]
        public float distanceFactor;

        [Tooltip("Ángulo en grados respecto a la dirección de referencia (ver 'relativeToPlayerFacing').")]
        public float angleDegrees;

        [Tooltip("Si es true, el ángulo se mide respecto a hacia dónde mira el jugador al empezar la batalla. Si es false, respecto al eje Z del mundo.")]
        public bool relativeToPlayerFacing;
    }

    /// <summary>
    /// Calcula la posición mundial de spawn del enemigo a partir del centro de la arena (posición
    /// del jugador al empezar la batalla) y el perfil elegido. El resultado no tiene en cuenta la
    /// altura del terreno — quien llame a este método debe asentarlo con un raycast contra el
    /// suelo (igual que ya hace BossArenaController.PlaceBossOnFloor con el spawn antiguo).
    /// </summary>
    public Vector3 ComputeSpawnPosition(Vector3 arenaCenter, Quaternion playerRotationAtStart, int profileIndex = 0)
    {
        SpawnProfile profile = (spawnProfiles != null && spawnProfiles.Length > 0)
            ? spawnProfiles[Mathf.Clamp(profileIndex, 0, spawnProfiles.Length - 1)]
            : new SpawnProfile { distanceFactor = 0.85f, angleDegrees = 0f, relativeToPlayerFacing = true };

        Quaternion baseRotation = profile.relativeToPlayerFacing ? playerRotationAtStart : Quaternion.identity;
        Quaternion offsetRotation = baseRotation * Quaternion.Euler(0f, profile.angleDegrees, 0f);
        Vector3 direction = offsetRotation * Vector3.forward;
        float distance = arenaRadius * Mathf.Clamp01(profile.distanceFactor);

        return arenaCenter + direction * distance;
    }
}
