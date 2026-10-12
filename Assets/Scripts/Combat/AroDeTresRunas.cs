using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Aro de tres runas del Demonio 2 (GDD § 3): una runa por personaje (Will, Estela, Liam), cada
/// una del color de su retrato. Mientras quede alguna entera, el jefe no recibe daño: cada golpe
/// va a la runa de quien lo da (el personaje que lleva el jugador o el compañero que golpea con
/// su IA) y, tras 'golpesPorRuna', la rompe. Si la runa de ese personaje ya está rota, el golpe
/// no sirve: hay que cambiar de personaje. Rotas las tres, queda expuesto 'segundosExpuesto' y
/// después el aro se recompone. La runa de un personaje que no está en el grupo no cuenta.
///
/// Va junto al Damageable del jefe (IFiltroDeDano). Dice cuándo está expuesto (IExpuestoAlDano)
/// para la guía de combate. El aro se dibuja solo, en torno al cuerpo. Ver INC-723.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class AroDeTresRunas : MonoBehaviour, IFiltroDeDano, IExpuestoAlDano
{
    [Header("Reglas")]
    [Tooltip("Golpes del personaje de cada runa para romperla.")]
    [SerializeField, Min(1)] private int golpesPorRuna = 3;
    [Tooltip("Segundos mínimos entre dos golpes que cuentan para la misma runa (un hechizo con " +
             "varios colliders o una zona que pulsa no rompe una runa de una vez).")]
    [SerializeField, Min(0f)] private float pausaEntreGolpes = 0.25f;
    [Tooltip("Segundos que queda expuesto con las tres runas rotas, antes de que el aro se recomponga.")]
    [SerializeField, Min(1f)] private float segundosExpuesto = 10f;

    [Header("Aspecto")]
    [Tooltip("Altura del aro sobre el pie del jefe (0 = a media altura de su cuerpo).")]
    [SerializeField, Min(0f)] private float altura = 0f;
    [Tooltip("Radio del aro (0 = según el cuerpo).")]
    [SerializeField, Min(0f)] private float radio = 0f;
    [SerializeField, Min(0.05f)] private float tamanoRuna = 0.32f;
    [Tooltip("Grados por segundo que gira el aro.")]
    [SerializeField] private float giro = 60f;
    [Tooltip("Al romperse una runa. Vacío = sin efecto.")]
    [SerializeField] private GameObject vfxRuptura;
    [Tooltip("Al recomponerse el aro. Vacío = sin efecto.")]
    [SerializeField] private GameObject vfxRecomponer;

    private static readonly Slot[] Orden = { Slot.Will, Slot.Estela, Slot.Liam };
    private static readonly Color[] ColoresPorDefecto =
    {
        new Color(1f, 0.604f, 0.235f, 1f),     // Will #FF9A3C
        new Color(0.310f, 0.553f, 1f, 1f),     // Estela #4F8DFF
        new Color(0.482f, 0.749f, 0.353f, 1f), // Liam #7BBF5A
    };

    private struct Runa
    {
        public int golpes;
        public bool rota;
        public float ultimoGolpe;
        public Transform cuerpo;
        public Light luz;
        public Color color;
        public float pulso;
    }

    private readonly Runa[] _runas = new Runa[3];
    private Damageable _vida;
    private Transform _aro;
    private float _expuestoHasta = -1f;
    private float _revisarGrupoEn;
    private readonly bool[] _enGrupo = { true, true, true };

    /// Las tres runas están rotas y aún no se ha recompuesto.
    public bool Expuesto => _vida != null && _vida.IsAlive && Time.time < _expuestoHasta;

    /// Runas que quedan enteras (de personajes que están en el grupo).
    public int RunasEnteras
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _runas.Length; i++) if (!_runas[i].rota && _enGrupo[i]) n++;
            return n;
        }
    }

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        CrearAro();
        RevisarGrupo();
    }

    void Update()
    {
        if (_aro == null) return;
        bool vivo = _vida == null || _vida.IsAlive;
        if (!vivo)
        {
            if (_aro.gameObject.activeSelf) _aro.gameObject.SetActive(false);
            return;
        }

        if (Time.time >= _revisarGrupoEn) { _revisarGrupoEn = Time.time + 0.5f; RevisarGrupo(); }

        // Se acaba la ventana: el aro vuelve a cerrarse.
        if (_expuestoHasta > 0f && Time.time >= _expuestoHasta) Recomponer();

        bool aroVisible = !Expuesto;
        if (_aro.gameObject.activeSelf != aroVisible) _aro.gameObject.SetActive(aroVisible);
        if (!aroVisible) return;

        _aro.Rotate(0f, giro * Time.deltaTime, 0f, Space.World);
        for (int i = 0; i < _runas.Length; i++)
        {
            ref var r = ref _runas[i];
            if (r.cuerpo == null) continue;
            bool visible = !r.rota && _enGrupo[i];
            if (r.cuerpo.gameObject.activeSelf != visible) r.cuerpo.gameObject.SetActive(visible);
            if (!visible) continue;
            // Cuanto más golpeada, más pequeña y más tenue; el pulso marca cada golpe.
            float entera = 1f - (float)r.golpes / golpesPorRuna;
            r.pulso = Mathf.MoveTowards(r.pulso, 0f, Time.deltaTime * 3f);
            r.cuerpo.localScale = Vector3.one * tamanoRuna * (0.6f + 0.4f * entera + r.pulso);
            if (r.luz != null) r.luz.intensity = 1.2f + 2f * entera + 6f * r.pulso;
        }
    }

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (Expuesto) return cantidad;
        if (RunasEnteras == 0) { Exponer(); return cantidad; }

        if (!PersonajeQueGolpea(instigador, out var slot)) return 0f; // golpe sin dueño (trampa, caída…)
        int i = Indice(slot);
        ref var r = ref _runas[i];

        if (r.rota || !_enGrupo[i])
        {
            AvisosDeCombate.GolpeMalDado(gameObject);
            return 0f;
        }

        if (Time.time - r.ultimoGolpe < pausaEntreGolpes) return 0f;
        r.ultimoGolpe = Time.time;
        r.golpes++;
        r.pulso = 0.5f;

        if (r.golpes >= golpesPorRuna)
        {
            r.rota = true;
            if (vfxRuptura != null && r.cuerpo != null && VfxPoolService.Instance != null)
                VfxPoolService.Instance.Play(vfxRuptura, r.cuerpo.position, Quaternion.identity, 2f);
            if (RunasEnteras == 0) Exponer();
            else AvisosDeCombate.RomperseDefensa(gameObject);
        }
        return 0f;
    }

    /// Quién da el golpe: el cuerpo del jugador lleva al personaje activo; un compañero, el suyo.
    public static bool PersonajeQueGolpea(GameObject instigador, out Slot slot)
    {
        slot = Slot.Will;
        if (instigador == null) return false;

        var jugador = PlayerService.Player;
        if (jugador != null && instigador.transform.root == jugador.transform.root)
        {
            slot = PartyControlManager.Instance != null ? PartyControlManager.Instance.ActiveSlot : Slot.Will;
            return true;
        }

        var aliado = instigador.GetComponentInParent<NPCPartyMember>();
        if (aliado == null) return false;
        string nombre = aliado.DisplayName;
        if (nombre == "Estela") { slot = Slot.Estela; return true; }
        if (nombre == "Liam") { slot = Slot.Liam; return true; }
        if (nombre == "Will") { slot = Slot.Will; return true; }
        return false;
    }

    private void Exponer()
    {
        if (Expuesto) return;
        _expuestoHasta = Time.time + segundosExpuesto;
        if (_aro != null) _aro.gameObject.SetActive(false);
    }

    private void Recomponer()
    {
        _expuestoHasta = -1f;
        for (int i = 0; i < _runas.Length; i++)
        {
            _runas[i].golpes = 0;
            _runas[i].rota = false;
            _runas[i].pulso = 0.6f;
        }
        if (_aro != null)
        {
            _aro.gameObject.SetActive(true);
            if (vfxRecomponer != null && VfxPoolService.Instance != null)
                VfxPoolService.Instance.Play(vfxRecomponer, _aro.position, Quaternion.identity, 2f);
        }
        AvisosDeCombate.RecomponerseDefensa(gameObject);
    }

    /// La runa de un personaje que no está en el grupo no cuenta (Will siempre está).
    private void RevisarGrupo()
    {
        var grupo = PlayerParty.Instance;
        for (int i = 0; i < Orden.Length; i++)
        {
            if (Orden[i] == Slot.Will) { _enGrupo[i] = true; continue; }
            string nombre = Orden[i] == Slot.Estela ? "Estela" : "Liam";
            _enGrupo[i] = grupo == null || grupo.GetMemberByName(nombre) != null;
        }
    }

    private static int Indice(Slot slot) => slot == Slot.Will ? 0 : slot == Slot.Estela ? 1 : 2;

    // ── Aspecto ───────────────────────────────────────────────────────────

    /// Tres esferas de color con su luz, repartidas en un círculo en torno al cuerpo.
    private void CrearAro()
    {
        var cuerpo = GetComponent<Collider>();
        Bounds b = cuerpo != null ? cuerpo.bounds : new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
        float r = radio > 0f ? radio : Mathf.Max(b.extents.x, b.extents.z) + 0.5f;
        float h = altura > 0f ? altura : b.center.y - transform.position.y;

        _aro = new GameObject("Aro de tres runas").transform;
        _aro.SetParent(transform, false);
        _aro.position = transform.position + Vector3.up * h;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        for (int i = 0; i < Orden.Length; i++)
        {
            var ficha = FichaDePersonaje.Buscar(Orden[i]);
            Color color = ficha != null && ficha.ColorDelPersonaje.a > 0f ? ficha.ColorDelPersonaje : ColoresPorDefecto[i];

            var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera.name = $"Runa de {Orden[i]}";
            var col = esfera.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col); // solo se ve: los golpes los decide Filtrar
            esfera.transform.SetParent(_aro, false);
            float a = i * Mathf.PI * 2f / Orden.Length;
            esfera.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            esfera.transform.localScale = Vector3.one * tamanoRuna;

            var rend = esfera.GetComponent<Renderer>();
            if (rend != null && shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var luz = esfera.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = color;
            luz.range = 2.5f;
            luz.intensity = 3f;

            _runas[i] = new Runa { cuerpo = esfera.transform, luz = luz, color = color };
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < _runas.Length; i++)
        {
            var rend = _runas[i].cuerpo != null ? _runas[i].cuerpo.GetComponent<Renderer>() : null;
            if (rend != null && rend.sharedMaterial != null) Destroy(rend.sharedMaterial);
        }
    }
}
