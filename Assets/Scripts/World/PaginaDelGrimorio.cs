using UnityEngine;

/// <summary>
/// Página del grimorio escondida en el mundo (INC-503): al tocarla, el grupo aprende su hechizo
/// (de Will, Estela o Liam: lo dice el propio hechizo) y sale el aviso de hechizo aprendido. Se
/// recuerda con un flag del preset (<c>GRIMORIO_PAGINA:&lt;id&gt;</c>), así que no vuelve a
/// aparecer tras guardar en un punto de guardado. Para las que piden volar, levitar o bucear, basta
/// con ponerla donde solo se llega así.
///
/// Los maestros y las misiones enseñan con el nodo UnlockAbilitiesNode del grafo narrativo, que usa
/// el mismo UnlockService.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PaginaDelGrimorio : MonoBehaviour
{
    public const string FlagPrefix = "GRIMORIO_PAGINA:";

    [Tooltip("Hechizo que enseña.")]
    public SpellId hechizo = SpellId.None;

    [Tooltip("Identificador único de esta página (para no volver a mostrarla). Vacío = nombre de la escena + nombre del objeto.")]
    public string idUnico;

    [Header("Presentación")]
    [Tooltip("Parte visual que flota y gira (el libro). Vacío = este objeto.")]
    public Transform visual;
    [Min(0f)] public float alturaDeFlote = 0.15f;
    [Min(0f)] public float gradosPorSegundo = 45f;
    [Tooltip("Efecto al recogerla (opcional).")]
    public GameObject efectoAlRecoger;
    [Tooltip("Escala del efecto al recogerla. Los efectos de impacto vienen pensados para golpes grandes; la página pide uno pequeño.")]
    [Min(0.05f)] public float escalaDelEfecto = 0.35f;
    [Tooltip("Sonido al recogerla (clave de AudioService, opcional).")]
    public string sfxAlRecoger = "";

    private Vector3 _basePos;
    private bool _recogida;
    private bool _pendiente;

    private string Flag => FlagPrefix + (string.IsNullOrEmpty(idUnico) ? $"{gameObject.scene.name}:{name}" : idUnico);

    void Reset()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1f;
    }

    void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        if (visual == null) visual = transform;
        _basePos = visual.localPosition;
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += ComprobarSiYaSeRecogio;
        if (GameBootService.IsAvailable) ComprobarSiYaSeRecogio();
        else _pendiente = true;
    }

    void OnDisable() => GameBootService.OnProfileReady -= ComprobarSiYaSeRecogio;

    private void ComprobarSiYaSeRecogio()
    {
        _pendiente = false;
        if (UnlockService.GetActivePreset() == null) return;
        if (UnlockService.HasFlag(Flag)) gameObject.SetActive(false);
    }

    void Update()
    {
        if (visual == null) return;
        float t = Time.time;
        visual.localPosition = _basePos + Vector3.up * (Mathf.Sin(t * 2f) * alturaDeFlote);
        visual.Rotate(Vector3.up, gradosPorSegundo * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_recogida || _pendiente || hechizo == SpellId.None) return;
        var player = PlayerService.Player;
        if (player == null || (other.gameObject != player && !other.transform.IsChildOf(player.transform))) return;

        _recogida = true;
        UnlockService.UnlockSpell(hechizo, assignToEmptySlot: true);
        UnlockService.AddFlag(Flag);

        if (PlayerService.TryGetComponent<PlayerPresetService>(out var pps, includeInactive: true, allowSceneLookup: true) && pps != null)
            pps.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);

        if (efectoAlRecoger != null && VfxPoolService.Instance != null)
        {
            var fx = VfxPoolService.Instance.Play(efectoAlRecoger, transform.position, Quaternion.identity);
            if (fx != null) fx.localScale *= escalaDelEfecto;
        }
        if (!string.IsNullOrEmpty(sfxAlRecoger) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxAlRecoger, worldPosition: transform.position);

        gameObject.SetActive(false);
    }
}
