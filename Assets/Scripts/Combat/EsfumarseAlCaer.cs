using UnityEngine;

/// Un enemigo que cae no desaparece de golpe: primero se le ve caer y después se deshace a la
/// vista (su efecto propio de humo o ceniza, su sonido, el cuerpo que se oscurece, se encoge y se
/// hunde). Al morir lo hace solo (pide a su Damageable que espere a destruirlo); otros finales
/// (un jefe vencido sin perder la vida) lo piden con Esfumar. Si el enemigo lleva un PlanoDeAviso,
/// la cámara lo enseña mientras dura. La celebración de la victoria espera a que acabe
/// (HayAlgunoEnCurso). Ver INC-1333.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class EsfumarseAlCaer : MonoBehaviour
{
    [Tooltip("Al morir (Damageable) se esfuma solo. Sin marcar, solo cuando se pide con Esfumar.")]
    [SerializeField] private bool alMorir = true;

    [Header("Tiempos")]
    [Tooltip("Segundos que se le ve caído antes de empezar a deshacerse.")]
    [SerializeField, Min(0f)] private float esperaAntes = 0.8f;
    [Tooltip("Segundos que tarda en deshacerse.")]
    [SerializeField, Min(0.2f)] private float duracion = 1.6f;

    [Header("Efecto y sonido")]
    [Tooltip("Efecto de un solo uso al empezar a deshacerse (por el pool), a sus pies.")]
    [SerializeField] private GameObject vfx;
    [SerializeField, Min(0.5f)] private float vidaDelVfx = 3.5f;
    [SerializeField] private float alturaDelVfx = 0.2f;
    [Tooltip("Clave del sonido en el perfil de audio.")]
    [SerializeField] private string sonido = "";
    [SerializeField, Range(0f, 1f)] private float volumen = 1f;

    [Header("Cuerpo")]
    [Tooltip("Metros que se hunde mientras se deshace.")]
    [SerializeField, Min(0f)] private float hundirse = 0.5f;
    [Tooltip("Tamaño al final, respecto al de antes.")]
    [SerializeField, Range(0f, 1f)] private float escalaFinal = 0.3f;
    [Tooltip("Color que multiplica el cuerpo al final (ceniza).")]
    [SerializeField] private Color colorFinal = new Color(0.18f, 0.12f, 0.22f);

    private static int _enCurso;

    /// Algún enemigo se está deshaciendo ahora mismo (la celebración espera a que acabe).
    public static bool HayAlgunoEnCurso => _enCurso > 0;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _enCurso = 0;
#endif

    private static readonly int TinteId = Shader.PropertyToID("_Tinte");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Damageable _vida;
    private PlanoDeAviso _plano;
    private Renderer[] _cuerpo;
    private Color[] _tinte, _base;
    private bool[] _conTinte, _conBase;
    private MaterialPropertyBlock _mpb;

    private bool _empezado, _deshaciendo, _acabado, _contado;
    private float _desde;
    private Vector3 _posInicial, _escalaInicial;

    /// Duración total desde que se pide hasta que no queda nada.
    public float DuracionTotal => esperaAntes + duracion;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _plano = GetComponent<PlanoDeAviso>();
        _mpb = new MaterialPropertyBlock();

        var todos = GetComponentsInChildren<Renderer>(true);
        int n = 0;
        for (int i = 0; i < todos.Length; i++) if (todos[i] is SkinnedMeshRenderer || todos[i] is MeshRenderer) n++;
        _cuerpo = new Renderer[n];
        _tinte = new Color[n]; _base = new Color[n];
        _conTinte = new bool[n]; _conBase = new bool[n];
        n = 0;
        for (int i = 0; i < todos.Length; i++) if (todos[i] is SkinnedMeshRenderer || todos[i] is MeshRenderer) _cuerpo[n++] = todos[i];

        if (alMorir && _vida != null)
        {
            _vida.RetrasarDestruccion(DuracionTotal + 0.2f);
            _vida.OnDied += Esfumar;
        }
        enabled = false;
    }

    void OnDestroy()
    {
        if (_vida != null) _vida.OnDied -= Esfumar;
        Descontar();
    }

    void OnDisable()
    {
        // Si se apaga a mitad (cambio de escena), deja de contar para la celebración.
        if (_empezado && !_acabado) Descontar();
    }

    /// Empieza a deshacerse (una sola vez).
    public void Esfumar()
    {
        if (_empezado || !gameObject.activeInHierarchy) return;
        _empezado = true;
        _desde = Time.time;
        _enCurso++;
        _contado = true;
        if (_plano != null) _plano.Ensenar(DuracionTotal);
        enabled = true;
    }

    void Update()
    {
        float t = Time.time - _desde;
        if (t < esperaAntes) return;

        if (!_deshaciendo) EmpezarADeshacerse();

        float k = Mathf.Clamp01((t - esperaAntes) / duracion);
        float suave = k * k * (3f - 2f * k);
        transform.position = _posInicial - Vector3.up * (hundirse * suave);
        transform.localScale = _escalaInicial * Mathf.Lerp(1f, escalaFinal, suave);
        Tintar(Color.Lerp(Color.white, colorFinal, Mathf.Min(1f, suave * 1.4f)));

        if (k >= 1f) Acabar();
    }

    private void EmpezarADeshacerse()
    {
        _deshaciendo = true;
        _posInicial = transform.position;
        _escalaInicial = transform.localScale;
        Vector3 pie = transform.position + Vector3.up * alturaDelVfx;
        if (vfx != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfx, pie, Quaternion.identity, vidaDelVfx);
        if (!string.IsNullOrEmpty(sonido))
            AudioService.Instance?.PlaySFX(sonido, volumen, pie);

        for (int i = 0; i < _cuerpo.Length; i++)
        {
            var r = _cuerpo[i];
            if (r == null || r.sharedMaterial == null) continue;
            var mat = r.sharedMaterial;
            r.GetPropertyBlock(_mpb);
            _conTinte[i] = mat.HasProperty(TinteId);
            _conBase[i] = mat.HasProperty(BaseColorId);
            if (_conTinte[i]) _tinte[i] = _mpb.HasColor(TinteId) ? _mpb.GetColor(TinteId) : mat.GetColor(TinteId);
            if (_conBase[i]) _base[i] = _mpb.HasColor(BaseColorId) ? _mpb.GetColor(BaseColorId) : mat.GetColor(BaseColorId);
        }
    }

    private void Tintar(Color multiplicador)
    {
        for (int i = 0; i < _cuerpo.Length; i++)
        {
            var r = _cuerpo[i];
            if (r == null || (!_conTinte[i] && !_conBase[i])) continue;
            r.GetPropertyBlock(_mpb);
            if (_conTinte[i]) _mpb.SetColor(TinteId, _tinte[i] * multiplicador);
            if (_conBase[i]) _mpb.SetColor(BaseColorId, _base[i] * multiplicador);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void Acabar()
    {
        _acabado = true;
        for (int i = 0; i < _cuerpo.Length; i++)
            if (_cuerpo[i] != null) _cuerpo[i].enabled = false;
        Descontar();
        enabled = false;
        // Muerto, lo destruye su Damageable (ya ha esperado lo suficiente); vencido sin morir, se apaga.
        if (_vida == null || _vida.IsAlive) gameObject.SetActive(false);
    }

    private void Descontar()
    {
        if (!_contado) return;
        _contado = false;
        _enCurso = Mathf.Max(0, _enCurso - 1);
    }
}
