using System.Collections.Generic;
using UnityEngine;

/// Un aliado que puede atraer a un jefe (provocarle) para que el jugador le ataque por la espalda.
/// Es el jefe quien decide cuándo va a por un señuelo (p. ej. GolemBossAI, «dos objetivos»);
/// el señuelo solo se anuncia y aguanta: mientras le provocan no recibe daño (los aliados nunca
/// lo reciben, ver CombatTargetProvider) y levanta su escudo (NPCShieldController, si tiene; si
/// no, 'vfxProvocando'). Tiene que estar en el prefab del aliado, para que su Damageable (que se
/// crea al arrancar) lo encuentre como regla de daño.
/// Ver INC-489.
[DisallowMultipleComponent]
public sealed class SenueloDeCombate : MonoBehaviour, IFiltroDeDano
{
    private static readonly List<SenueloDeCombate> s_activos = new();

    /// Los señuelos activos en escena.
    public static IReadOnlyList<SenueloDeCombate> Activos => s_activos;

    [Tooltip("Efecto que se enciende sobre el aliado mientras un jefe va a por él (su escudo). Vacío = sin efecto.")]
    [SerializeField] private GameObject vfxProvocando;
    [SerializeField] private Vector3 offsetVfx = new Vector3(0f, 1f, 0f);

    private GameObject _vfx;
    private int _provocados;
    private NPCShieldController _escudo;
    private const float EscudoMientrasProvoca = 60f;

    void Awake() => _escudo = GetComponentInChildren<NPCShieldController>(true);

    public bool Provocando => _provocados > 0;

    void OnEnable() { if (!s_activos.Contains(this)) s_activos.Add(this); }

    void OnDisable()
    {
        s_activos.Remove(this);
        _provocados = 0;
        ActualizarVfx();
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_activos.Clear();
#endif

    /// Un jefe empieza a ir a por este aliado.
    public void EmpezarProvocacion()
    {
        _provocados++;
        ActualizarVfx();
    }

    /// Ese jefe deja de ir a por él.
    public void TerminarProvocacion()
    {
        _provocados = Mathf.Max(0, _provocados - 1);
        ActualizarVfx();
    }

    public float Filtrar(float cantidad, GameObject instigador) => Provocando ? 0f : cantidad;

    private void ActualizarVfx()
    {
        if (_escudo != null)
        {
            if (Provocando && !_escudo.IsDefending) _escudo.StartDefending(EscudoMientrasProvoca);
            else if (!Provocando && _escudo.IsDefending) _escudo.StopDefending();
        }

        if (vfxProvocando == null) return;
        if (Provocando && _vfx == null)
        {
            _vfx = Instantiate(vfxProvocando, transform);
            _vfx.transform.localPosition = offsetVfx;
        }
        if (_vfx != null && _vfx.activeSelf != Provocando) _vfx.SetActive(Provocando);
    }
}
