using Invector.vCharacterController;
using UnityEngine;

/// Lo que el clima le hace al grupo (lo mueve DayNightCycle; sin clima no hace nada):
/// - Viento: el jugador anda más despacio (más aún de cara al viento) y quieto lo arrastra en su
///   dirección (vThirdPersonMotor.empujeExterno); todos se tapan la cara en bucle (estado
///   UpperBody.ClimaTaparse, piernas libres).
/// - Lluvia o nieve: quien está quieto un momento se abraza (estado UpperBody.ClimaFrio, ver
///   AbrazarseDeFrio), encoge los hombros y tirita (procedural sobre la animación, en LateUpdate) y
///   pone cara de helado (NPCEmotion.Frozen como cara de fondo); andando no cambia nada.
/// Solo con el jugador libre: en el suelo, en modo Default, sin bloqueo de PlayerLockService
/// (diálogos, cinemáticas). Nadie pisa la capa UpperBody ni la cara de fondo si otro sistema las
/// está usando. Ver INC-699, INC-959.
public static class ClimaSobreElJugador
{
    /// Estado en bucle de la capa UpperBody (lo crea el menú de clima). Sin él se usa Fear01, que
    /// solo dura un momento porque su estado tiene salida.
    public const string EstadoDeTaparse = "ClimaTaparse";
    /// Estado en bucle de la capa UpperBody para abrazarse de frío (lo crea AbrazarseDeFrio). Sin él
    /// solo se encogen los hombros y se tirita.
    public const string EstadoDeFrio = "ClimaFrio";
    private const string GestoDeReserva = "Fear01";
    private const string ReposoDeTorso = "UpperIdle";
    private const float FundidoDelGesto = 0.35f;   // segundos para entrar/salir del gesto
    private const float UmbralDeEntrada = 0.1f;    // stick por debajo de esto = quieto
    private const float VelocidadDeQuieto = 0.25f; // m/s: un compañero por debajo de esto está quieto
    private const float UmbralDeContra = -0.3f;    // coseno a partir del cual se anda contra el viento
    private const float VientoParaTaparse = 0.35f;
    private const float FundidoDelFrio = 0.8f;
    private const float FrioParaAbrazarse = 0.25f;           // lluvia o nieve a partir de la que se abrazan
    private const float SegundosQuietoParaAbrazarse = 0.8f;  // que no se abracen en cada parada breve

    // Postura de frío, en grados.
    private const float HombrosEncogidos = 9f;
    private const float BrazosPegados = 6f;
    private const float PechoEncorvado = 6f;
    private const float CabezaGacha = 7f;
    private const float Tiritona = 1.4f;
    private const float FrecuenciaDeTiritona = 11f;

    private static readonly int HashTaparse = Animator.StringToHash(EstadoDeTaparse);
    private static readonly int HashFrio = Animator.StringToHash(EstadoDeFrio);
    private static readonly int HashReserva = Animator.StringToHash(GestoDeReserva);
    private static readonly int HashReposo = Animator.StringToHash(ReposoDeTorso);
    private static readonly object Dueño = new object();

    /// Un personaje del grupo con lo que el clima le toca (cacheado al aparecer) y su estado.
    private sealed class Cuerpo
    {
        public GameObject go;
        public Animator anim;
        public int capa = -1;
        public int hashTaparse, hashFrio;    // 0 si su Animator no tiene el estado
        public Transform pecho, cabeza, hombroI, hombroD, brazoI, brazoD;
        public NPCEmotionController cara;

        public int gesto;                    // gesto de la capa UpperBody que es nuestro (0: ninguno)
        public float pesoGesto;
        public int frameDelCruce;            // el Animator no refleja el CrossFade hasta su siguiente paso
        public bool caraNuestra;
        public float frioPedido, pesoFrio;
        public float segundosQuieto;
        public Vector3 posAnterior;
        public bool conPosAnterior;

        public static Cuerpo De(GameObject go)
        {
            var c = new Cuerpo { go = go, anim = go.GetComponent<Animator>(), cara = go.GetComponentInChildren<NPCEmotionController>(true) };
            if (c.anim == null) return c;
            c.capa = c.anim.GetLayerIndex("UpperBody");
            if (c.capa > 0)
            {
                c.hashTaparse = c.anim.HasState(c.capa, HashTaparse) ? HashTaparse
                              : c.anim.HasState(c.capa, HashReserva) ? HashReserva : 0;
                c.hashFrio = c.anim.HasState(c.capa, HashFrio) ? HashFrio : 0;
            }
            if (c.anim.isHuman)
            {
                c.pecho = c.anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? c.anim.GetBoneTransform(HumanBodyBones.Chest);
                c.cabeza = c.anim.GetBoneTransform(HumanBodyBones.Head);
                c.hombroI = c.anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
                c.hombroD = c.anim.GetBoneTransform(HumanBodyBones.RightShoulder);
                c.brazoI = c.anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                c.brazoD = c.anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            return c;
        }

        public bool Vivo => go != null && go.activeInHierarchy && anim != null && anim.isActiveAndEnabled;
        public bool EsGestoNuestro(int hash) => hash != 0 && (hash == hashTaparse || hash == hashFrio);
    }

    private static GameObject _jugador;
    private static vThirdPersonMotor _motor;
    private static PlayerActionManager _modos;
    private static Cuerpo _cuerpoJugador;
    private static Cuerpo[] _companeros;     // por slot del grupo

    private static bool _conTope;
    private static float _topeActual;
    private static bool _empujando;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _jugador = null; _motor = null; _modos = null;
        _cuerpoJugador = null; _companeros = null;
        _conTope = false; _topeActual = 0f; _empujando = false;
    }
#endif

    /// Un paso del efecto (Update). viento 0-1 y frio 0-1 ya vienen a cero en interiores;
    /// direccion es horizontal y normalizada, hacia donde sopla.
    public static void Actualizar(float viento, Vector3 direccion, float velocidadConViento,
        float velocidadContra, float arrastreQuieto, float frio, float dt)
    {
        // Sin clima se suelta todo (la postura de frío se sigue fundiendo en AplicarFrio).
        if ((viento <= 0.001f && frio <= 0.001f) || !Cachear())
        {
            Soltar();
            return;
        }

        bool libre = _motor.enabled && _motor.PisandoSuelo && Time.timeScale > 0f
                     && (_modos == null || _modos.Top == ActionMode.Default)
                     && !(PlayerLockService.HasInstance && PlayerLockService.Instance.IsLocked)
                     && !(global::Core.PlayerInputManager.Instance != null && global::Core.PlayerInputManager.Instance.IsInUIMode);
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
        ActualizarCuerpo(_cuerpoJugador, quieto, taparse, frio, dt);

        int n = ControlDelJugador.Personajes;
        if (_companeros == null || _companeros.Length != n) { SoltarCompaneros(); _companeros = new Cuerpo[n]; }
        for (int i = 0; i < n; i++)
        {
            var slot = (PartyControlManager.CharacterSlot)i;
            var npc = slot != ControlDelJugador.Activo && ControlDelJugador.EnElGrupo(slot) ? ControlDelJugador.NpcDe(slot) : null;
            GameObject go = npc != null && npc.gameObject.activeInHierarchy ? npc.gameObject : null;
            var c = _companeros[i];
            if (c != null && c.go != go) { SoltarCuerpo(c); c = _companeros[i] = null; }
            if (go == null) continue;
            if (c == null) c = _companeros[i] = Cuerpo.De(go);

            // Quieto: casi sin moverse desde el paso anterior.
            Vector3 pos = go.transform.position;
            bool compiQuieto = c.conPosAnterior && dt > 0f
                               && new Vector2(pos.x - c.posAnterior.x, pos.z - c.posAnterior.z).sqrMagnitude
                                  < VelocidadDeQuieto * VelocidadDeQuieto * dt * dt;
            c.posAnterior = pos;
            c.conPosAnterior = true;
            ActualizarCuerpo(c, compiQuieto, taparse, frio, dt);
        }
    }

    /// Gesto, frío y cara de un personaje en este paso.
    private static void ActualizarCuerpo(Cuerpo c, bool quieto, bool taparse, float frio, float dt)
    {
        if (!c.Vivo) { SoltarCuerpo(c); return; }

        c.segundosQuieto = quieto ? c.segundosQuieto + dt : 0f;
        bool helado = quieto && !taparse && frio >= FrioParaAbrazarse && c.segundosQuieto >= SegundosQuietoParaAbrazarse;

        int deseado = taparse ? c.hashTaparse : helado ? c.hashFrio : 0;
        ActualizarGesto(c, deseado, dt);

        // El frío se nota quieto con el torso libre o abrazado por nosotros (no encima de otro gesto).
        bool torsoLibre = c.capa > 0 && (c.gesto == c.hashFrio && c.gesto != 0
                                         || c.gesto == 0 && c.anim.GetLayerWeight(c.capa) < 0.05f);
        c.frioPedido = quieto && !taparse && torsoLibre ? Mathf.Clamp01(frio) : 0f;

        PonerCara(c, helado && torsoLibre);
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
        if (_cuerpoJugador != null) AplicarFrio(_cuerpoJugador, dt);
        if (_companeros == null) return;
        for (int i = 0; i < _companeros.Length; i++)
            if (_companeros[i] != null) AplicarFrio(_companeros[i], dt);
    }

    private static void AplicarFrio(Cuerpo c, float dt)
    {
        c.pesoFrio = Mathf.MoveTowards(c.pesoFrio, c.frioPedido, dt / FundidoDelFrio);
        if (c.pesoFrio <= 0f || c.pecho == null || !c.Vivo) return;

        float peso = c.pesoFrio;
        // Abrazado, los brazos los pone el clip: el procedural solo los pega mientras no lo está.
        float brazos = c.gesto != 0 && c.gesto == c.hashFrio ? 1f - c.pesoGesto : 1f;
        Transform raiz = c.go.transform;
        Vector3 derecha = raiz.right, delante = raiz.forward, arriba = raiz.up;
        float t = Time.time * FrecuenciaDeTiritona;
        float temblor = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * Tiritona * peso;
        float temblor2 = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * Tiritona * peso;

        Girar(c.pecho, derecha, PechoEncorvado * peso);
        Girar(c.pecho, arriba, temblor);
        Girar(c.cabeza, derecha, CabezaGacha * peso);
        // Alrededor de 'delante', un ángulo positivo sube lo que apunta a la derecha.
        Girar(c.hombroD, delante, HombrosEncogidos * peso + temblor2);
        Girar(c.hombroI, delante, -HombrosEncogidos * peso - temblor2);
        Girar(c.brazoD, delante, -BrazosPegados * peso * brazos);
        Girar(c.brazoI, delante, BrazosPegados * peso * brazos);
    }

    private static void Girar(Transform hueso, Vector3 eje, float grados)
    {
        if (hueso != null && grados != 0f) hueso.rotation = Quaternion.AngleAxis(grados, eje) * hueso.rotation;
    }

    /// Lo quita todo de golpe: tope, arrastre, gestos y caras (la postura de frío se funde sola).
    public static void Soltar()
    {
        QuitarTope();
        if (_empujando && _motor != null) _motor.empujeExterno = Vector3.zero;
        _empujando = false;
        if (_cuerpoJugador != null) SoltarCuerpo(_cuerpoJugador);
        SoltarCompaneros();
    }

    private static void SoltarCompaneros()
    {
        if (_companeros == null) return;
        for (int i = 0; i < _companeros.Length; i++)
            if (_companeros[i] != null) SoltarCuerpo(_companeros[i]);
    }

    private static void SoltarCuerpo(Cuerpo c)
    {
        c.frioPedido = 0f;
        c.segundosQuieto = 0f;
        c.conPosAnterior = false;
        if (c.gesto != 0) ActualizarGesto(c, 0, Time.unscaledDeltaTime, inmediato: true);
        PonerCara(c, false);
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
        // El cuerpo que pasa a ser el jugador deja de ser compañero (y al revés): se recachea todo.
        _companeros = null;
        _jugador = jugador;
        _motor = jugador.GetComponent<vThirdPersonMotor>();
        _modos = jugador.GetComponent<PlayerActionManager>();
        _cuerpoJugador = Cuerpo.De(jugador);
        return _motor != null;
    }

    /// Lleva la capa UpperBody hacia el gesto deseado (0: ninguno) sin pisar a otros sistemas.
    private static void ActualizarGesto(Cuerpo c, int deseado, float dt, bool inmediato = false)
    {
        if (c.capa <= 0 || c.anim == null || !c.anim.isActiveAndEnabled) { c.gesto = 0; c.pesoGesto = 0f; return; }
        var anim = c.anim;

        if (c.gesto != 0)
        {
            // Si otro sistema ha cambiado el estado o el peso del torso, la capa ya no es nuestra: se
            // suelta sin tocarla para no pisarle el gesto.
            var estado = anim.GetCurrentAnimatorStateInfo(c.capa);
            bool enGesto = c.EsGestoNuestro(estado.shortNameHash) ||
                           (anim.IsInTransition(c.capa) && c.EsGestoNuestro(anim.GetNextAnimatorStateInfo(c.capa).shortNameHash));
            bool pesoAjeno = Mathf.Abs(anim.GetLayerWeight(c.capa) - c.pesoGesto) > 0.08f;
            if (pesoAjeno) { c.gesto = 0; return; }
            if (!enGesto && Time.frameCount - c.frameDelCruce > 1)
            {
                if (deseado == 0) { c.gesto = 0; return; }
                // El clip terminó (Fear01 tiene tiempo de salida): se relanza sin soltar la capa.
                anim.CrossFadeInFixedTime(deseado, 0.12f, c.capa);
                c.gesto = deseado;
                c.frameDelCruce = Time.frameCount;
            }
            else if (deseado != 0 && deseado != c.gesto)
            {
                // Del frío al viento o al revés, sin pasar por el reposo.
                anim.CrossFadeInFixedTime(deseado, FundidoDelGesto, c.capa);
                c.gesto = deseado;
                c.frameDelCruce = Time.frameCount;
            }
        }
        else if (deseado != 0)
        {
            // Solo si nadie más usa el torso (diálogo, magia, modo batalla...).
            if (anim.GetLayerWeight(c.capa) > 0.05f) return;
            anim.CrossFadeInFixedTime(deseado, FundidoDelGesto, c.capa);
            c.gesto = deseado;
            c.pesoGesto = Mathf.Max(0.01f, anim.GetLayerWeight(c.capa));
            c.frameDelCruce = Time.frameCount;
        }
        if (c.gesto == 0) return;

        float objetivo = deseado != 0 ? 1f : 0f;
        c.pesoGesto = inmediato && deseado == 0 ? 0f : Mathf.MoveTowards(c.pesoGesto, objetivo, dt / FundidoDelGesto);
        anim.SetLayerWeight(c.capa, c.pesoGesto);

        if (deseado == 0 && c.pesoGesto <= 0f)
        {
            if (anim.HasState(c.capa, HashReposo)) anim.CrossFadeInFixedTime(HashReposo, 0f, c.capa);
            c.gesto = 0;
        }
    }

    /// Cara de helado como cara de fondo, solo si ningún otro sistema tiene puesta la suya.
    private static void PonerCara(Cuerpo c, bool helado)
    {
        if (c.cara == null) { c.caraNuestra = false; return; }
        if (c.caraNuestra && !ReferenceEquals(c.cara.PropietarioDeCaraDeFondo, Dueño)) c.caraNuestra = false;
        if (helado && !c.caraNuestra && c.cara.PropietarioDeCaraDeFondo == null)
        {
            c.cara.SetCaraDeFondo(Dueño, NPCEmotion.Frozen);
            c.caraNuestra = true;
        }
        else if (!helado && c.caraNuestra)
        {
            c.cara.ClearCaraDeFondo(Dueño);
            c.caraNuestra = false;
        }
    }
}
