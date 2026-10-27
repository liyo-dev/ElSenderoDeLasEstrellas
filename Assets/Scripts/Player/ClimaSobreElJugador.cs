using Invector.vCharacterController;
using UnityEngine;

/// Lo que el clima le hace al jugador (lo mueve DayNightCycle; sin clima no hace nada):
/// - Viento: anda más despacio (más aún de cara al viento), quieto lo arrastra en su dirección
///   (vThirdPersonMotor.empujeExterno) y, quieto o andando, se tapa la cara en bucle (estado
///   UpperBody.ClimaTaparse, piernas libres).
/// - Lluvia o nieve: quieto, encoge los hombros y tirita (procedural sobre su idle, en LateUpdate);
///   andando no cambia nada.
/// Solo con el jugador libre: en el suelo, en modo Default, sin bloqueo de PlayerLockService
/// (diálogos, cinemáticas) y sin otra cosa usando la capa UpperBody.
public static class ClimaSobreElJugador
{
    /// Estado en bucle de la capa UpperBody (lo crea el menú de clima). Sin él se usa Fear01, que
    /// solo dura un momento porque su estado tiene salida.
    public const string EstadoDeTaparse = "ClimaTaparse";
    private const string GestoDeReserva = "Fear01";
    private const string ReposoDeTorso = "UpperIdle";
    private const float FundidoDelGesto = 0.35f;   // segundos para entrar/salir del gesto
    private const float UmbralDeEntrada = 0.1f;    // stick por debajo de esto = quieto
    private const float UmbralDeContra = -0.3f;    // coseno a partir del cual se anda contra el viento
    private const float VientoParaTaparse = 0.35f;
    private const float FundidoDelFrio = 0.8f;

    // Postura de frío, en grados.
    private const float HombrosEncogidos = 9f;
    private const float BrazosPegados = 6f;
    private const float PechoEncorvado = 6f;
    private const float CabezaGacha = 7f;
    private const float Tiritona = 1.4f;
    private const float FrecuenciaDeTiritona = 11f;

    private static readonly int HashTaparse = Animator.StringToHash(EstadoDeTaparse);
    private static readonly int HashReserva = Animator.StringToHash(GestoDeReserva);
    private static readonly int HashReposo = Animator.StringToHash(ReposoDeTorso);
    private static readonly object Dueño = new object();

    private static GameObject _jugador;
    private static vThirdPersonMotor _motor;
    private static Animator _animator;
    private static PlayerActionManager _modos;
    private static int _capaTorso = -1;
    private static int _hashGesto;
    private static bool _gestoEnBucle;

    private static bool _conTope;
    private static float _topeActual;
    private static bool _empujando;
    private static bool _gestoNuestro;   // la capa UpperBody la estamos usando nosotros
    private static float _pesoGesto;
    private static int _frameDelCruce;   // el Animator no refleja el CrossFade hasta su siguiente paso

    private static bool _quietoYLibre;
    private static float _frioPedido;
    private static float _pesoFrio;
    private static Transform _pecho, _cabeza, _hombroI, _hombroD, _brazoI, _brazoD;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _jugador = null; _motor = null; _animator = null; _modos = null;
        _capaTorso = -1; _hashGesto = 0; _gestoEnBucle = false;
        _conTope = false; _topeActual = 0f; _empujando = false; _gestoNuestro = false; _pesoGesto = 0f; _frameDelCruce = 0;
        _quietoYLibre = false; _frioPedido = 0f; _pesoFrio = 0f;
        _pecho = _cabeza = _hombroI = _hombroD = _brazoI = _brazoD = null;
    }
#endif

    /// Un paso del efecto (Update). viento 0-1 y frio 0-1 ya vienen a cero en interiores;
    /// direccion es horizontal y normalizada, hacia donde sopla.
    public static void Actualizar(float viento, Vector3 direccion, float velocidadConViento,
        float velocidadContra, float arrastreQuieto, float frio, float dt)
    {
        if ((viento <= 0.001f && frio <= 0.001f && _pesoFrio <= 0f) || !Cachear())
        {
            Soltar();
            return;
        }

        bool libre = _motor.enabled && _motor.PisandoSuelo && Time.timeScale > 0f
                     && (_modos == null || _modos.Top == ActionMode.Default)
                     && !(PlayerLockService.HasInstance && PlayerLockService.Instance.IsLocked);
        if (!libre)
        {
            Soltar();
            return;
        }

        Vector3 mov = _motor.DireccionDeMovimiento;
        mov.y = 0f;
        bool quieto = _motor.MagnitudDeEntrada < UmbralDeEntrada || mov.sqrMagnitude < 0.0001f;

        ActualizarTope(viento, direccion, mov, quieto, velocidadConViento, velocidadContra);

        // Arrastre si está quieto.
        _motor.empujeExterno = quieto && viento > 0.001f ? direccion * (arrastreQuieto * viento) : Vector3.zero;
        _empujando = quieto && viento > 0.001f;

        bool taparse = viento > VientoParaTaparse;
        ActualizarGesto(taparse, dt);
        ActualizarCompaneros(taparse, dt);

        // El frío solo se nota quieto y con el torso libre (sin taparse ni otro gesto).
        _quietoYLibre = quieto && !taparse && !_gestoNuestro && _capaTorso > 0
                        && _animator.GetLayerWeight(_capaTorso) < 0.05f;
        _frioPedido = frio;
    }

    /// Con viento anda más despacio; de cara al viento, más aún.
    private static void ActualizarTope(float viento, Vector3 direccion, Vector3 mov, bool quieto,
        float velocidadConViento, float velocidadContra)
    {
        if (viento <= 0.01f)
        {
            QuitarTope();
            return;
        }

        float contra = 0f;
        if (!quieto)
        {
            float coseno = Vector3.Dot(mov.normalized, direccion);
            contra = Mathf.InverseLerp(UmbralDeContra, -1f, coseno);
        }

        float libreDeViento = Mathf.Max(_motor.freeSpeed.sprintSpeed, velocidadConViento);
        float conViento = Mathf.Lerp(libreDeViento, velocidadConViento, viento);
        float tope = Mathf.Lerp(conViento, Mathf.Min(conViento, velocidadContra), contra * viento);
        // Solo se reaplica al cambiar de verdad: Poner recalcula el tope de todos los sistemas.
        if (!_conTope || Mathf.Abs(tope - _topeActual) > 0.05f)
        {
            TopeDeVelocidadDelJugador.Poner(Dueño, tope);
            _topeActual = tope;
        }
        _conTope = true;
    }

    /// La postura de frío (LateUpdate, después de la animación).
    public static void AplicarFrio(float dt)
    {
        float objetivo = _quietoYLibre ? Mathf.Clamp01(_frioPedido) : 0f;
        _pesoFrio = Mathf.MoveTowards(_pesoFrio, objetivo, dt / FundidoDelFrio);
        if (_pesoFrio <= 0f) return;

        // Jugador
        if (_animator != null && _animator.isActiveAndEnabled && _pecho != null)
        {
            AplicarHuesosFrio(_jugador.transform, _pecho, _cabeza, _hombroI, _hombroD, _brazoI, _brazoD, _pesoFrio);
        }

        // Compañeros del grupo
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var slot = (PartyControlManager.CharacterSlot)i;
            if (slot == ControlDelJugador.Activo) continue;
            if (!ControlDelJugador.EnElGrupo(slot)) continue;

            var npc = ControlDelJugador.NpcDe(slot);
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

            var anim = npc.GetComponent<Animator>();
            if (anim == null || !anim.isActiveAndEnabled || !anim.isHuman) continue;

            var pecho = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
            if (pecho == null) continue;

            var cabeza = anim.GetBoneTransform(HumanBodyBones.Head);
            var hombroI = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var hombroD = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
            var brazoI = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var brazoD = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);

            AplicarHuesosFrio(npc.transform, pecho, cabeza, hombroI, hombroD, brazoI, brazoD, _pesoFrio);
        }
    }

    private static void AplicarHuesosFrio(Transform raiz, Transform pecho, Transform cabeza,
        Transform hombroI, Transform hombroD, Transform brazoI, Transform brazoD, float peso)
    {
        Vector3 derecha = raiz.right, delante = raiz.forward, arriba = raiz.up;
        float t = Time.time * FrecuenciaDeTiritona;
        float temblor = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * Tiritona * peso;
        float temblor2 = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * Tiritona * peso;

        Girar(pecho, derecha, PechoEncorvado * peso);
        Girar(pecho, arriba, temblor);
        Girar(cabeza, derecha, CabezaGacha * peso);
        // Alrededor de 'delante', un ángulo positivo sube lo que apunta a la derecha.
        Girar(hombroD, delante, HombrosEncogidos * peso + temblor2);
        Girar(hombroI, delante, -HombrosEncogidos * peso - temblor2);
        Girar(brazoD, delante, -BrazosPegados * peso);
        Girar(brazoI, delante, BrazosPegados * peso);
    }

    private static void Girar(Transform hueso, Vector3 eje, float grados)
    {
        if (hueso != null && grados != 0f) hueso.rotation = Quaternion.AngleAxis(grados, eje) * hueso.rotation;
    }

    /// Lo quita todo de golpe, también el gesto de taparse si aún es nuestro.
    public static void Soltar()
    {
        QuitarTope();
        if (_empujando && _motor != null) _motor.empujeExterno = Vector3.zero;
        _empujando = false;
        _quietoYLibre = false;
        if (_gestoNuestro) ActualizarGesto(false, Time.unscaledDeltaTime, inmediato: true);
        ActualizarCompaneros(false, Time.unscaledDeltaTime, inmediato: true);
    }

    private static void QuitarTope()
    {
        if (!_conTope) return;
        TopeDeVelocidadDelJugador.Quitar(Dueño);
        _conTope = false;
    }

    private static bool Cachear()
    {
        var jugador = PlayerService.Player;
        if (jugador == null) return false;
        if (jugador == _jugador && _motor != null) return true;

        Soltar();
        _jugador = jugador;
        _motor = jugador.GetComponent<vThirdPersonMotor>();
        _animator = jugador.GetComponent<Animator>();
        _modos = jugador.GetComponent<PlayerActionManager>();
        _capaTorso = _animator != null ? _animator.GetLayerIndex("UpperBody") : -1;
        _gestoEnBucle = _capaTorso > 0 && _animator.HasState(_capaTorso, HashTaparse);
        _hashGesto = _gestoEnBucle ? HashTaparse
                   : _capaTorso > 0 && _animator.HasState(_capaTorso, HashReserva) ? HashReserva : 0;
        _gestoNuestro = false;
        _pesoGesto = 0f;
        _pesoFrio = 0f;

        bool humanoide = _animator != null && _animator.isHuman;
        _pecho = humanoide ? (_animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animator.GetBoneTransform(HumanBodyBones.Chest)) : null;
        _cabeza = humanoide ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
        _hombroI = humanoide ? _animator.GetBoneTransform(HumanBodyBones.LeftShoulder) : null;
        _hombroD = humanoide ? _animator.GetBoneTransform(HumanBodyBones.RightShoulder) : null;
        _brazoI = humanoide ? _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
        _brazoD = humanoide ? _animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
        return _motor != null;
    }

    private static void ActualizarGesto(bool taparse, float dt, bool inmediato = false)
    {
        if (_hashGesto == 0 || _animator == null || !_animator.isActiveAndEnabled) { _gestoNuestro = false; return; }

        if (taparse && !_gestoNuestro)
        {
            // Solo si nadie más usa el torso (diálogo, magia, modo batalla...).
            if (_pesoGesto <= 0.05f && _animator.GetLayerWeight(_capaTorso) > 0.05f) return;
            _animator.CrossFadeInFixedTime(_hashGesto, FundidoDelGesto, _capaTorso);
            _gestoNuestro = true;
            _pesoGesto = Mathf.Max(0.01f, _animator.GetLayerWeight(_capaTorso));
            _frameDelCruce = Time.frameCount;
        }
        if (!_gestoNuestro) return;

        // Si otro sistema ha cambiado el estado o el peso del torso, la capa ya no es nuestra: se
        // suelta sin tocarla para no pisarle el gesto.
        var estado = _animator.GetCurrentAnimatorStateInfo(_capaTorso);
        bool enGesto = estado.shortNameHash == _hashGesto ||
                       (_animator.IsInTransition(_capaTorso) &&
                        _animator.GetNextAnimatorStateInfo(_capaTorso).shortNameHash == _hashGesto);
        bool pesoAjeno = Mathf.Abs(_animator.GetLayerWeight(_capaTorso) - _pesoGesto) > 0.08f;

        if (!enGesto && Time.frameCount - _frameDelCruce > 1)
        {
            if (taparse)
            {
                // El clip terminó (Fear01 tiene tiempo de salida): lo relanzamos en bucle
                // continuo mientras siga el viento, sin soltar la capa.
                _animator.CrossFadeInFixedTime(_hashGesto, 0.12f, _capaTorso);
                _frameDelCruce = Time.frameCount;
            }
            else
            {
                _gestoNuestro = false;
                return;
            }
        }
        else if (pesoAjeno)
        {
            _gestoNuestro = false;
            return;
        }

        float objetivo = taparse ? 1f : 0f;
        _pesoGesto = inmediato && !taparse ? 0f : Mathf.MoveTowards(_pesoGesto, objetivo, dt / FundidoDelGesto);
        _animator.SetLayerWeight(_capaTorso, _pesoGesto);

        if (!taparse && _pesoGesto <= 0f)
        {
            if (_animator.HasState(_capaTorso, HashReposo))
                _animator.CrossFadeInFixedTime(HashReposo, 0f, _capaTorso);
            _gestoNuestro = false;
        }
    }

    private static void ActualizarCompaneros(bool taparse, float dt, bool inmediato = false)
    {
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var slot = (PartyControlManager.CharacterSlot)i;
            if (slot == ControlDelJugador.Activo) continue;
            if (!ControlDelJugador.EnElGrupo(slot)) continue;

            var npc = ControlDelJugador.NpcDe(slot);
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

            var anim = npc.GetComponent<Animator>();
            if (anim == null || !anim.isActiveAndEnabled) continue;

            int capa = anim.GetLayerIndex("UpperBody");
            if (capa <= 0) continue;

            int hash = anim.HasState(capa, HashTaparse) ? HashTaparse
                     : anim.HasState(capa, HashReserva) ? HashReserva : 0;
            if (hash == 0) continue;

            float pesoActual = anim.GetLayerWeight(capa);
            float pesoObj = taparse ? 1f : 0f;
            float nuevoPeso = inmediato && !taparse ? 0f : Mathf.MoveTowards(pesoActual, pesoObj, dt / FundidoDelGesto);
            anim.SetLayerWeight(capa, nuevoPeso);

            var st = anim.GetCurrentAnimatorStateInfo(capa);
            bool enGesto = st.shortNameHash == hash ||
                           (anim.IsInTransition(capa) && anim.GetNextAnimatorStateInfo(capa).shortNameHash == hash);

            if (taparse)
            {
                if (!enGesto || (st.normalizedTime >= 0.95f && !anim.IsInTransition(capa)))
                {
                    anim.CrossFadeInFixedTime(hash, 0.15f, capa);
                }
            }
            else if (nuevoPeso <= 0f && anim.HasState(capa, HashReposo))
            {
                anim.CrossFadeInFixedTime(HashReposo, 0f, capa);
            }
        }
    }
}
