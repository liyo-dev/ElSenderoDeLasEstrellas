using System.Collections;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;

/// Un personaje guía al jugador durante un combate contra un jefe siguiendo un GuionDeCombate:
/// primero explica qué ha pasado y qué hacer; después comenta lo que va pasando (el jefe queda
/// expuesto, un acierto, un golpe mal dado, cambio de fase, el jefe cambia de objetivo...).
///
/// No se coloca en ninguna escena: lo arranca quien empieza el combate (BossArenaController con
/// el guion de su encuentro, o el laboratorio de combate) con Empezar(), y se para al acabar.
/// Todo sale de sistemas genéricos, así que sirve para cualquier jefe: su Damageable, su
/// IExpuestoAlDano, IJefeConFases e IJefeConObjetivo; la vida del jugador (PlayerHealthSystem);
/// AvisosDeCombate (golpes mal dados, remate) y los orbes (OrbDropper). Ver INC-478 e INC-489.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // LateUpdate después de NPCSimpleAnimator para ganar la rotación
public sealed class GuiaDeCombate : MonoBehaviour
{
    [Tooltip("Grados por segundo al girarse hacia el jugador (solo si el guion lo pide).")]
    [SerializeField] private float lookAtSpeed = 360f;

    private GuionDeCombate _guion;
    private List<ComentarioDeCombate> _comentarios;

    private Transform          _hablante;
    private NPCSimpleAnimator  _npcAnim;
    private Animator           _animator;
    private float              _siguienteBusquedaHablante;
    private Transform          _jugador;
    private PlayerHealthSystem _saludJugador;

    private bool      _activo;
    private bool      _introLanzada;
    private bool      _introTerminada;
    private Coroutine _introCoroutine;
    private float     _hablandoHasta;
    private bool      _rotacionTomada;
    private int       _turnoBocadillo;
    private AudioClip _vozActual;

    private int[] _dichas;
    private readonly Dictionary<MomentoDeCombate, float> _ultimoMomento = new();
    private readonly List<(int indice, float cuando)> _cola = new();

    // Jefe
    private GameObject        _jefe;
    private Damageable        _vidaJefe;
    private IExpuestoAlDano   _ventanaJefe;
    private IJefeConFases     _fasesJefe;
    private IJefeConObjetivo  _objetivoJefe;
    private bool              _estabaExpuesto;
    private float             _ultimoAcierto;

    /// Arranca (o reinicia) la guía en 'anfitrion' con este guion y este jefe. Sin guion no hace nada.
    public static GuiaDeCombate Empezar(GameObject anfitrion, GuionDeCombate guion, GameObject jefe)
    {
        if (anfitrion == null || guion == null) return null;
        var guia = anfitrion.GetComponent<GuiaDeCombate>();
        if (guia == null) guia = anfitrion.AddComponent<GuiaDeCombate>();
        guia.Iniciar(guion, jefe);
        return guia;
    }

    void OnEnable()
    {
        AvisosDeCombate.AlGolpeMalDado += AlGolpeMalDado;
        AvisosDeCombate.AlPedirseRemate += AlPedirseRemate;
        OrbDropper.AlSoltarOrbes += AlSoltarOrbes;
    }

    void OnDisable()
    {
        Parar();
        AvisosDeCombate.AlGolpeMalDado -= AlGolpeMalDado;
        AvisosDeCombate.AlPedirseRemate -= AlPedirseRemate;
        OrbDropper.AlSoltarOrbes -= AlSoltarOrbes;
    }

    // ── Arranque y parada ─────────────────────────────────────────────────

    private void Iniciar(GuionDeCombate guion, GameObject jefe)
    {
        Parar();

        _guion = guion;
        _comentarios = guion.comentarios ?? new List<ComentarioDeCombate>();
        _dichas = new int[_comentarios.Count];
        _ultimoMomento.Clear();
        _cola.Clear();
        _activo = true;
        _introLanzada = false;
        _introTerminada = false;
        _hablandoHasta = 0f;
        _siguienteBusquedaHablante = 0f;

        if (PlayerService.TryGetPlayer(out var jugador) && jugador != null)
        {
            _jugador = jugador.transform;
            _saludJugador = jugador.GetComponent<PlayerHealthSystem>();
            if (_saludJugador != null) _saludJugador.OnDamageReceived += AlHerirAlJugador;
        }

        VincularJefe(jefe);
        _introCoroutine = StartCoroutine(Co_EsperarIntervencion());
    }

    /// Se calla y suelta todo. Se puede llamar las veces que haga falta.
    public void Parar()
    {
        if (_introCoroutine != null) { StopCoroutine(_introCoroutine); _introCoroutine = null; }
        _cola.Clear();
        DesvincularJefe();
        if (_saludJugador != null) { _saludJugador.OnDamageReceived -= AlHerirAlJugador; _saludJugador = null; }
        SoltarRotacion();
        Callar();
        _activo = false;
    }

    // Al pararse (victoria, derrota, salir de la arena) quita su bocadillo y su voz al momento:
    // la indicación ya no vale y no puede quedarse encima del informe de victoria. Solo el suyo:
    // si otro sistema ha puesto un bocadillo después, no se toca. Ver INC-614.
    private void Callar()
    {
        if (_turnoBocadillo != 0) SpeechBubbleUI.Instance?.Hide(_turnoBocadillo);
        _turnoBocadillo = 0;
        var audio = AudioService.Instance;
        if (_vozActual != null && audio != null && audio.IsVoicePlaying(_vozActual)) audio.StopVoice();
        _vozActual = null;
        _hablandoHasta = 0f;
    }

    /// Lo llama quien presenta al jefe al terminar la presentación. Si llega dos veces, no repite.
    public void LanzarIntervencion()
    {
        if (!_activo || _introLanzada) return;
        if (_introCoroutine != null) StopCoroutine(_introCoroutine);
        _introCoroutine = StartCoroutine(Co_Intervencion());
    }

    private IEnumerator Co_EsperarIntervencion()
    {
        float esperado = 0f;
        while (!_introLanzada && esperado < _guion.introFallbackSeconds)
        {
            yield return null;
            esperado += Time.deltaTime;
        }
        _introCoroutine = null;

        if (!_introLanzada)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[GuiaDeCombate] La presentación del jefe no ha terminado en " +
                $"{_guion.introFallbackSeconds:0}s: la intervención se suelta igualmente.", this);
#endif
            _introCoroutine = StartCoroutine(Co_Intervencion());
        }
    }

    private IEnumerator Co_Intervencion()
    {
        _introLanzada = true;
        if (_guion.introDelay > 0f) yield return new WaitForSeconds(_guion.introDelay);

        for (int i = 0; i < _comentarios.Count; i++)
        {
            if (_comentarios[i].momento != MomentoDeCombate.Inicio || string.IsNullOrEmpty(_comentarios[i].key)) continue;
            Decir(i);
            while (Time.time < _hablandoHasta) yield return null;
        }

        _introTerminada = true;
        _ultimoAcierto = Time.time;
        _introCoroutine = null;
    }

    // ── Quién habla ───────────────────────────────────────────────────────

    /// El NPC del guion: por su ID en el registro de NPCs y, si no está, por nombre en el grupo.
    /// Se busca de vez en cuando (no cada frame) hasta encontrarlo.
    private void BuscarHablante()
    {
        if (_hablante != null || Time.time < _siguienteBusquedaHablante) return;
        _siguienteBusquedaHablante = Time.time + 1f;

        Transform encontrado = null;
        if (NPCRegistry.HasInstance)
        {
            var npc = NPCRegistry.Instance.GetNPCByID(_guion.hablanteId);
            if (npc != null) encontrado = npc.transform;
        }
        if (encontrado == null && PlayerParty.HasInstance)
        {
            var miembro = PlayerParty.Instance.GetMemberByNarrativeId(_guion.hablanteId);
            if (miembro == null && !string.IsNullOrEmpty(_guion.nombreHablante))
                miembro = PlayerParty.Instance.GetMemberByName(_guion.nombreHablante);
            if (miembro != null) encontrado = miembro.transform;
        }
        if (encontrado == null) return;

        _hablante = encontrado;
        _npcAnim  = encontrado.GetComponentInChildren<NPCSimpleAnimator>();
        _animator = encontrado.GetComponentInChildren<Animator>();

        if (_guion.mirarAlJugador && _npcAnim != null)
        {
            _npcAnim.DisableAutoRotation();
            _rotacionTomada = true;
        }
    }

    private void SoltarRotacion()
    {
        if (_rotacionTomada && _npcAnim != null) _npcAnim.EnableAutoRotation();
        _rotacionTomada = false;
        _hablante = null;
        _npcAnim = null;
        _animator = null;
    }

    // ── Lo que pasa en el combate ─────────────────────────────────────────

    void Update()
    {
        if (!_activo) return;

        BuscarHablante();

        if (_ventanaJefe != null)
        {
            bool expuesto = _ventanaJefe.Expuesto;
            if (expuesto && !_estabaExpuesto) Ocurre(MomentoDeCombate.JefeExpuesto);
            _estabaExpuesto = expuesto;
        }

        if (_introTerminada && _jefe != null && Time.time - _ultimoAcierto > _guion.segundosSinAcertar)
        {
            _ultimoAcierto = Time.time;
            Ocurre(MomentoDeCombate.SinAcertar);
        }

        DecirSiguiente();
    }

    private void VincularJefe(GameObject jefe)
    {
        DesvincularJefe();
        if (jefe == null) return;

        _jefe = jefe;
        _vidaJefe = jefe.GetComponentInChildren<Damageable>(true);
        _ventanaJefe = jefe.GetComponentInChildren<IExpuestoAlDano>(true);
        _fasesJefe = jefe.GetComponentInChildren<IJefeConFases>(true);
        _objetivoJefe = jefe.GetComponentInChildren<IJefeConObjetivo>(true);
        _estabaExpuesto = false;

        if (_vidaJefe != null) _vidaJefe.OnDamaged += AlDanarAlJefe;
        if (_fasesJefe != null) _fasesJefe.AlCambiarDeFase += AlCambiarDeFase;
        if (_objetivoJefe != null) _objetivoJefe.AlCambiarDeObjetivo += AlCambiarDeObjetivo;
    }

    private void DesvincularJefe()
    {
        if (_vidaJefe != null) _vidaJefe.OnDamaged -= AlDanarAlJefe;
        if (_fasesJefe != null) _fasesJefe.AlCambiarDeFase -= AlCambiarDeFase;
        if (_objetivoJefe != null) _objetivoJefe.AlCambiarDeObjetivo -= AlCambiarDeObjetivo;
        _jefe = null;
        _vidaJefe = null;
        _ventanaJefe = null;
        _fasesJefe = null;
        _objetivoJefe = null;
    }

    private void AlDanarAlJefe(float _)
    {
        _ultimoAcierto = Time.time;
        if (_vidaJefe != null && _vidaJefe.Max > 0f && _vidaJefe.Current / _vidaJefe.Max <= _guion.umbralJefeCasiVencido
            && Ocurre(MomentoDeCombate.JefeCasiVencido))
            return;
        Ocurre(MomentoDeCombate.GolpeValido);
    }

    private void AlCambiarDeFase(int fase) => Ocurre(MomentoDeCombate.CambioDeFase, fase);

    private void AlCambiarDeObjetivo(Transform objetivo)
    {
        if (objetivo == null) return;
        if (_hablante != null && objetivo == _hablante) Ocurre(MomentoDeCombate.JefeVaAPorElHablante);
        else if (_jugador != null && objetivo == _jugador) Ocurre(MomentoDeCombate.JefeVaAPorElJugador);
    }

    private void AlGolpeMalDado(GameObject jefe)
    {
        if (jefe == _jefe) Ocurre(MomentoDeCombate.GolpeMalDado);
    }

    private void AlPedirseRemate(GameObject jefe)
    {
        if (jefe == _jefe) Ocurre(MomentoDeCombate.SePideRemate);
    }

    private void AlSoltarOrbes(OrbDropper _, OrbType tipo)
    {
        if (tipo == OrbType.Health || tipo == OrbType.Mana || tipo == OrbType.SpecialCharge)
            Ocurre(MomentoDeCombate.OrbesSueltos);
    }

    private void AlHerirAlJugador(float _)
    {
        if (_saludJugador != null && _saludJugador.HealthPercentage <= _guion.umbralPocaVida
            && Ocurre(MomentoDeCombate.JugadorPocaVida))
            return;
        Ocurre(MomentoDeCombate.JugadorHerido);
    }

    // ── Elegir y decir ────────────────────────────────────────────────────

    /// Ha pasado algo: si hay frase disponible para ello, la dice ya (si interrumpe) o la pone
    /// en cola. Devuelve si había algo que decir.
    private bool Ocurre(MomentoDeCombate momento, int fase = 0)
    {
        if (!_activo || !_introLanzada) return false;

        int i = Elegir(momento, fase);
        if (i < 0) return false;

        if (_comentarios[i].interrumpe && _introTerminada)
        {
            Decir(i);
            return true;
        }

        for (int k = 0; k < _cola.Count; k++)
            if (_comentarios[_cola[k].indice].momento == momento) return true; // ya está esperando
        _cola.Add((i, Time.time));
        return true;
    }

    /// Entre las frases del momento que aún pueden decirse, la menos repetida (a igualdad, la
    /// primera de la lista): las primeras veces se oye la explicación y luego se van turnando.
    private int Elegir(MomentoDeCombate momento, int fase)
    {
        float ultima = _ultimoMomento.TryGetValue(momento, out var t) ? t : -999f;
        int mejor = -1;
        for (int i = 0; i < _comentarios.Count; i++)
        {
            var c = _comentarios[i];
            if (c.momento != momento || string.IsNullOrEmpty(c.key)) continue;
            if (momento == MomentoDeCombate.CambioDeFase && c.fase != fase) continue;
            if (c.vecesMax > 0 && _dichas[i] >= c.vecesMax) continue;
            if (Time.time - ultima < c.enfriamiento) continue;
            if (mejor < 0 || _dichas[i] < _dichas[mejor]) mejor = i;
        }
        return mejor;
    }

    private void DecirSiguiente()
    {
        if (!_introTerminada || Time.time < _hablandoHasta) return;

        while (_cola.Count > 0)
        {
            var (i, cuando) = _cola[0];
            _cola.RemoveAt(0);
            var c = _comentarios[i];
            if (c.caducidad > 0f && Time.time - cuando > c.caducidad) continue;
            if (c.vecesMax > 0 && _dichas[i] >= c.vecesMax) continue;
            Decir(i);
            return;
        }
    }

    /// Un bocadillo con su gesto. Lo usan la intervención inicial y los comentarios.
    private void Decir(int indice)
    {
        var c = _comentarios[indice];
        _dichas[indice]++;
        _ultimoMomento[c.momento] = Time.time;

        if (!string.IsNullOrEmpty(c.anim))
        {
            if (_npcAnim != null)
                _npcAnim.PlaySocialGesture(c.anim);
            else if (_animator != null)
                _animator.CrossFadeInFixedTime(Animator.StringToHash(c.anim), 0.1f, 0);
        }

        string text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(c.key, c.key)
            : c.key;
        text = DialogueManager.ResolveDeviceConditionalText(text); // <kbonly>/<gpadonly> (p. ej. LT+RT, INC-491)
        float duracion = c.duracion > 0f ? c.duracion : _guion.duracionBocadillo;

        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo != null) duracion = Mathf.Max(duracion, bocadillo.TiempoDeLectura(text));
        float voz = bocadillo != null ? VoiceLines.TryPlay(c.key) : 0f;
        _vozActual = null;
        if (voz > 0f)
        {
            duracion = Mathf.Max(duracion, voz + 0.3f);
            VoiceLines.TryGet(c.key, out _vozActual);
        }
        _hablandoHasta = Time.time + duracion + _guion.pausaEntreFrases;

        if (bocadillo == null) return;
        // Fijo a un lado de la pantalla: quien guía cambia de sitio en pantalla cada vez que el
        // jugador se mueve, y un bocadillo que le persigue no se puede leer mientras se juega (INC-480).
        _turnoBocadillo = bocadillo.Show(_hablante != null ? _hablante : transform, text, duration: duracion,
                                         speakerName: _guion.nombreHablante, fijoEnPantalla: true);
    }

    // LateUpdate con DefaultExecutionOrder(100) corre después de NPCSimpleAnimator y gana la rotación.
    void LateUpdate()
    {
        if (!_activo || !_rotacionTomada || _hablante == null || _jugador == null) return;

        Vector3 dir = _jugador.position - _hablante.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir.normalized);
            _hablante.rotation = Quaternion.RotateTowards(_hablante.rotation, target, lookAtSpeed * Time.deltaTime);
        }
    }
}
