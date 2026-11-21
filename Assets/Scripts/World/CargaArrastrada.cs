using Game.NPC;
using Kits.Core;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Algo que un personaje lleva tirando de ello (un carro): va detrás de él, con el frente (las varas)
/// hacia él y siempre en la dirección en la que avanza, apoyado en el suelo. Como un remolque: si el
/// personaje se para o se da la vuelta a mirar, la carga se queda donde está; solo se mueve cuando él tira.
///
/// Con agarres (dos puntos de las varas, en el espacio del modelo), quien tira va metido entre las varas
/// y las coge con las manos: al engancharse se mide la altura de sus manos (por su cadera) y la carga se
/// escala para que las varas le queden a esa altura (los modelos de los packs están hechos a escala de
/// adulto y los personajes son más bajos); la distancia sale de dónde están los agarres, y las manos
/// llegan a ellos con IK de dos huesos (<see cref="TwoBoneIK"/>). Mientras tira, además, sostiene la pose
/// de medio cuerpo de tirar (si su animador la tiene).
///
/// Sólida (<see cref="HacerSolida"/>): la caja choca y es un obstáculo del NavMesh que los demás
/// rodean; quien tira va fuera de la caja, entre las varas. Las ruedas, si están sueltas, las hace girar
/// <see cref="WheelSpin"/>. Si quien tira entra en una pelea, suelta la carga y lucha libre; la carga
/// se queda donde la dejó y la vuelve a coger cuando él pasa junto a las varas (o, si le han llevado
/// lejos, al acabar la pelea se le coloca detrás). El personaje se busca por su id de NPC: sirve en cinemáticas (le mueve el
/// guion) y en tramos jugables (le mueve su agente). Cuándo existe lo decide su tramo (ActivoEnTramo).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class CargaArrastrada : MonoBehaviour
{
    [Tooltip("Id del NPC que tira de la carga (su persistenceId).")]
    [SerializeField] private string actorId;
    [Tooltip("Metros entre el personaje y el centro de la carga (sin agarres; con agarres se calcula).")]
    [SerializeField, Min(0.5f)] private float distancia = 2.6f;
    [Tooltip("Grados que hay que girar el modelo para que su frente (las varas) quede hacia quien tira. 0 si las varas van hacia el +Z del modelo.")]
    [SerializeField] private float giroDelModelo;
    [Tooltip("Pose de medio cuerpo que sostiene quien tira mientras la carga avanza (vacío = ninguna).")]
    [SerializeField] private string poseDeQuienTira = "TirarDeLasVaras";
    [Tooltip("Más lejos que esto (un teletransporte, una carga recién encendida) la carga se coloca detrás de él de golpe.")]
    [SerializeField, Min(1f)] private float distanciaParaColocar = 12f;

    [Header("Agarres (las manos en las varas)")]
    [Tooltip("Con agarres, quien tira coge las varas con las manos y la carga se ajusta a su altura.")]
    [SerializeField] private bool conAgarres;
    [Tooltip("Dónde coge la mano izquierda, en el espacio del modelo (sin escalar).")]
    [SerializeField] private Vector3 agarreIzquierdo;
    [Tooltip("Dónde coge la mano derecha, en el espacio del modelo (sin escalar).")]
    [SerializeField] private Vector3 agarreDerecho;
    [Tooltip("Altura de las manos respecto a la altura de la cadera de quien tira (1 = a la altura de la cadera).")]
    [SerializeField, Range(0.5f, 1.5f)] private float manosRespectoALaCadera = 1f;
    [Tooltip("Metros que las manos van por delante del cuerpo de quien tira.")]
    [SerializeField, Min(0f)] private float manosPorDelante = 0.22f;
    [Tooltip("Cuánto se puede escalar la carga para ajustarla a quien tira (mínimo y máximo).")]
    [SerializeField] private Vector2 escalaPermitida = new Vector2(0.5f, 1.5f);

    private Transform _actor;
    private NPCSimpleAnimator _animadorDelActor;
    private bool _posepuesta;
    private bool _sinPose;   // su animador no tiene la pose: no se vuelve a pedir
    private bool _soltada;   // la soltó para pelear: no le sigue hasta volver a cogerla
    private float _siguienteBusqueda;
    private int _capaSuelo;
    private Vector3 _escalaBase;
    private float _distanciaEnUso;
    private Transform _hombroI, _codoI, _manoI, _hombroD, _codoD, _manoD;
    private float _alcanceI, _alcanceD, _pesoAgarre;
    private const float AlturaDelRayo = 3f;
    private const float LargoDelRayo = 8f;
    private const float SegundosDeAgarre = 0.25f;
    // Metros de más sobre la distancia de tirar a los que vuelve a coger las varas tras una pelea.
    private const float MargenParaRecoger = 1.2f;

    public void Configurar(string actor, float metros, float giro, string pose = "TirarDeLasVaras")
    {
        actorId = actor;
        distancia = metros;
        giroDelModelo = giro;
        poseDeQuienTira = pose;
    }

    /// Quien tira coge las varas por estos dos puntos (espacio del modelo, sin escalar) y la carga se
    /// ajusta a la altura de sus manos.
    public void ConfigurarAgarres(Vector3 izquierdo, Vector3 derecho, float manosPorDelanteDelCuerpo = 0.22f)
    {
        conAgarres = true;
        agarreIzquierdo = izquierdo;
        agarreDerecho = derecho;
        manosPorDelante = manosPorDelanteDelCuerpo;
    }

    /// <summary>
    /// La carga choca y aparta a los demás: una caja (en el espacio del modelo, sin escalar) con
    /// colisión, un cuerpo cinemático (se mueve sin física) y un obstáculo del NavMesh que esquivan los
    /// agentes con evitación de obstáculos. La caja no debe
    /// cubrir las varas: ahí va quien tira. Pensado para llamarlo desde el Editor al montar la escena.
    /// </summary>
    public void HacerSolida(Bounds cajaDelModelo)
    {
        var caja = GetComponent<BoxCollider>();
        if (caja == null) caja = gameObject.AddComponent<BoxCollider>();
        caja.isTrigger = false;
        caja.center = cajaDelModelo.center;
        caja.size = cajaDelModelo.size;
        var cuerpo = GetComponent<Rigidbody>();
        if (cuerpo == null) cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        var obstaculo = GetComponent<NavMeshObstacle>();
        if (obstaculo == null) obstaculo = gameObject.AddComponent<NavMeshObstacle>();
        obstaculo.shape = NavMeshObstacleShape.Box;
        obstaculo.center = cajaDelModelo.center;
        obstaculo.size = cajaDelModelo.size;
        // Sin recortar la malla: un hueco recortado podría tragarse el sitio de quien tira, que va
        // pegado a la caja. Los agentes que esquivan obstáculos la rodean.
        obstaculo.carving = false;
    }

    /// El personaje que tira de la carga (su id de NPC).
    public string ActorId => actorId;

    /// Dónde va la carga cuando la tira `actor`, con esta distancia y este giro. Vale fuera de Play,
    /// donde `LateUpdate` no corre: lo usa el previz de guion para que el carro salga en las fotos.
    public void PoseDetrasDe(Transform actor, out Vector3 posicion, out Quaternion rotacion)
        => PoseDetrasDe(actor, actor.forward, out posicion, out rotacion);

    /// Igual, pero con la dirección en la que va el personaje (la de su camino, no la de su mirada).
    public void PoseDetrasDe(Transform actor, Vector3 direccion, out Vector3 posicion, out Quaternion rotacion)
    {
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.01f) direccion = actor.forward;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.01f) direccion = Vector3.forward;
        direccion.Normalize();

        float metros = _distanciaEnUso > 0f ? _distanciaEnUso : distancia;
        posicion = actor.position - direccion * metros;
        posicion.y = actor.position.y;
        rotacion = Quaternion.LookRotation(direccion) * Quaternion.Euler(0f, giroDelModelo, 0f);
    }

    private void Awake()
    {
        _capaSuelo = LayerMask.GetMask("Floor");
        _escalaBase = transform.localScale;
    }

    private void OnEnable()
    {
        _actor = null;
        _soltada = false;
    }

    private void OnDisable()
    {
        SoltarPose();
        _pesoAgarre = 0f;
    }

    /// Quien tira suelta las varas (la carga se apaga o cambia de dueño).
    private void SoltarPose()
    {
        if (_posepuesta && _animadorDelActor != null) _animadorDelActor.SoltarPoseDeMedioCuerpo();
        _posepuesta = false;
    }

    private void LateUpdate()
    {
        if (_actor == null || !_actor.gameObject.activeInHierarchy)
        {
            if (Time.time < _siguienteBusqueda) return;
            _siguienteBusqueda = Time.time + 1f;
            SoltarPose();
            _actor = Buscar();
            if (_actor == null) return;
            _animadorDelActor = _actor.GetComponentInChildren<NPCSimpleAnimator>();
            _sinPose = false;
            Enganchar();
        }

        Vector3 delActor = _actor.position;
        Vector3 aqui = transform.position;
        Vector3 haciaElActor = delActor - aqui;
        haciaElActor.y = 0f;
        float separacion = haciaElActor.magnitude;
        if (separacion < 0.01f) return;

        float metros = _distanciaEnUso > 0f ? _distanciaEnUso : distancia;

        // En una pelea no tira de nada: suelta las varas y la carga se queda donde está.
        if (ActiveCombatRegistry.EstaEnUnaPelea(_actor.gameObject))
        {
            if (!_soltada) { _soltada = true; SoltarPose(); }
            LlevarLasManos(float.MaxValue, metros);
            return;
        }
        if (_soltada)
        {
            // La recoge al volver junto a las varas, o se le coloca detrás si se ha ido lejos.
            if (separacion > metros + MargenParaRecoger && separacion <= distanciaParaColocar)
            {
                LlevarLasManos(float.MaxValue, metros);
                return;
            }
            _soltada = false;
        }

        Vector3 frente = haciaElActor / separacion;
        bool mover = true;
        if (separacion > distanciaParaColocar)
        {
            frente = _actor.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) return;
            frente.Normalize();
        }
        else if (separacion <= metros) mover = false;

        if (mover)
        {
            Vector3 sitio = delActor - frente * metros;
            sitio.y = delActor.y;
            if (Physics.Raycast(sitio + Vector3.up * AlturaDelRayo, Vector3.down, out var suelo, LargoDelRayo, _capaSuelo, QueryTriggerInteraction.Ignore))
                sitio.y = suelo.point.y;
            transform.SetPositionAndRotation(sitio, Quaternion.LookRotation(frente) * Quaternion.Euler(0f, giroDelModelo, 0f));
            AgarrarLasVaras();
        }
        LlevarLasManos(separacion, metros);
    }

    /// Al engancharse a quien tira: con agarres, escala la carga para que las varas le queden a la
    /// altura de las manos y calcula a qué distancia va él (entre las varas, con las manos en ellas).
    private void Enganchar()
    {
        _distanciaEnUso = 0f;
        _hombroI = _codoI = _manoI = _hombroD = _codoD = _manoD = null;
        if (!conAgarres) return;
        var animador = _actor.GetComponentInChildren<Animator>();
        if (animador == null || !animador.isHuman) return;
        var cadera = animador.GetBoneTransform(HumanBodyBones.Hips);
        _hombroI = animador.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _codoI = animador.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _manoI = animador.GetBoneTransform(HumanBodyBones.LeftHand);
        _hombroD = animador.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _codoD = animador.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _manoD = animador.GetBoneTransform(HumanBodyBones.RightHand);
        _alcanceI = TwoBoneIK.Length(_hombroI, _codoI, _manoI);
        _alcanceD = TwoBoneIK.Length(_hombroD, _codoD, _manoD);
        if (cadera == null) return;

        // La altura de las manos: la de la cadera sobre los pies (no depende del gesto que esté haciendo).
        float alturaDeLasManos = (cadera.position.y - _actor.position.y) * manosRespectoALaCadera;
        float alturaDeLosAgarres = (agarreIzquierdo.y + agarreDerecho.y) * 0.5f;
        if (alturaDeLasManos > 0.05f && alturaDeLosAgarres > 0.01f)
        {
            float escalaBase = Mathf.Abs(_escalaBase.y) > 1e-4f ? _escalaBase.y : 1f;
            float factor = Mathf.Clamp(alturaDeLasManos / (alturaDeLosAgarres * escalaBase), escalaPermitida.x, escalaPermitida.y);
            transform.localScale = _escalaBase * factor;
        }
        // Él va donde están los agarres, un poco por detrás de las manos.
        Vector3 frenteDelModelo = Quaternion.Euler(0f, -giroDelModelo, 0f) * Vector3.forward;
        float hastaLosAgarres = Vector3.Dot((agarreIzquierdo + agarreDerecho) * 0.5f, frenteDelModelo) * transform.localScale.z;
        _distanciaEnUso = Mathf.Max(0.5f, hastaLosAgarres - manosPorDelante);
    }

    /// Las manos a los agarres cuando quien tira está entre las varas (si se aparta o se gira, las suelta).
    private void LlevarLasManos(float separacion, float metros)
    {
        if (!conAgarres || _manoI == null || _manoD == null || _hombroI == null || _hombroD == null) return;
        Vector3 izquierdo = transform.TransformPoint(agarreIzquierdo);
        Vector3 derecho = transform.TransformPoint(agarreDerecho);
        Vector3 haciaLaCarga = transform.position - _actor.position; haciaLaCarga.y = 0f;
        bool deEspaldas = haciaLaCarga.sqrMagnitude > 1e-4f && Vector3.Dot(_actor.forward, haciaLaCarga.normalized) < -0.3f;
        bool alAlcance = separacion < metros + 0.6f && deEspaldas
                         && (izquierdo - _hombroI.position).magnitude <= _alcanceI * 1.25f
                         && (derecho - _hombroD.position).magnitude <= _alcanceD * 1.25f;
        _pesoAgarre = Mathf.MoveTowards(_pesoAgarre, alAlcance ? 1f : 0f, Time.deltaTime / SegundosDeAgarre);
        if (_pesoAgarre <= 0.001f) return;
        TwoBoneIK.Reach(_hombroI, _codoI, _manoI, izquierdo, _pesoAgarre);
        TwoBoneIK.Reach(_hombroD, _codoD, _manoD, derecho, _pesoAgarre);
    }

    /// Mientras tira, agarra las varas. Si un gesto le ha quitado la pose, la recupera al volver a
    /// tirar (parado no se toca: así no pisa sus gestos de mirar o de hablar).
    private void AgarrarLasVaras()
    {
        if (_sinPose || string.IsNullOrEmpty(poseDeQuienTira) || _animadorDelActor == null) return;
        if (_posepuesta && _animadorDelActor.SosteniendoPose) return;
        _animadorDelActor.HoldPose(poseDeQuienTira, fundido: 0.25f);
        _posepuesta = _animadorDelActor.SosteniendoPose;
        _sinPose = !_posepuesta;
    }

    private Transform Buscar()
    {
        if (string.IsNullOrEmpty(actorId) || !NPCRegistry.HasInstance) return null;
        return NPCRegistry.Instance.TryGetNPCByID(actorId, out var npc) && npc != null && npc.isActiveAndEnabled ? npc.transform : null;
    }
}
