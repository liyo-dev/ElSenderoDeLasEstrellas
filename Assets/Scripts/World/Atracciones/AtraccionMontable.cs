using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Una atracción a la que se sube el grupo: noria, barco vikingo, tiovivo... Al interactuar
/// pregunta si se sube; con la pantalla en negro cuelga a los tres de sus puestos en el vagón
/// (la parte que se mueve) más cercano a la subida, la atracción se los lleva con su propia
/// animación, la cámara va por los planos de la lista y al acabar los deja de pie en la subida.
/// No mueve la atracción: la mueve su Animator, como siempre. Ver INC-891.
///
/// El jugador va con <see cref="PasajeroJugador"/> y los compañeros con <see cref="PasajeroNpc"/>,
/// lo mismo que el barco navegable.
///
/// Con un <see cref="RepertorioDeAtraccion"/>, cada viaje es una escenita: lo que dicen y hacen los
/// tres en la cola, al arrancar, en lo más alto, bajando y al bajar, quién se queda abajo y quién se
/// marea (INC-896).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public class AtraccionMontable : MonoBehaviour
{
    /// <summary>Un plano de cámara del viaje.</summary>
    [Serializable]
    public sealed class Plano
    {
        [Tooltip("Punto fijo de la cámara. Vacío = a bordo: la cámara va en el vagón, en «A bordo».")]
        public Transform punto;
        [Tooltip("Con punto fijo: la cámara mira al vagón en vez de hacia donde mira el punto.")]
        public bool seguirAlVagon = true;
        [Tooltip("A bordo: posición de la cámara respecto al grupo, en metros: z hacia donde miran ellos, " +
                 "y hacia el «arriba» del vagón, x a su derecha. Delante (z > 0) se les ve la cara.")]
        public Vector3 aBordo = new Vector3(0f, 1.3f, 2.3f);
        [Tooltip("A bordo: hacia dónde mira la cámara, en esos mismos ejes.")]
        public Vector3 mirarABordo = new Vector3(0f, -0.15f, -1f);
        [Tooltip("Segundos que dura el plano.")]
        [Min(0.5f)] public float segundos = 8f;
        [Tooltip("Campo de visión del plano.")]
        [Range(20f, 90f)] public float fov = 50f;
        [Tooltip("Campo de visión al acabar el plano (0 = el mismo): un acercamiento lento.")]
        [Range(0f, 90f)] public float fovFinal = 0f;
        [Tooltip("A bordo: grados que gira la cámara alrededor del grupo a lo largo del plano.")]
        [Range(0f, 120f)] public float giro = 0f;
    }

    [Header("Vagones y puestos")]
    [Tooltip("Las partes que se mueven donde va el grupo (cabinas, la barca). Se sube al más cercano a la subida.")]
    [SerializeField] private Transform[] vagones;
    [Tooltip("Sitio de cada uno en el vagón, en su espacio local, a la altura del suelo del vagón. " +
             "El primero es para quien lleva el mando.")]
    [SerializeField] private Vector3[] puestos =
        { new Vector3(0f, 0f, 0f), new Vector3(-0.6f, 0f, 0f), new Vector3(0.6f, 0f, 0f) };
    [Tooltip("Hacia dónde miran, en el espacio del vagón.")]
    [SerializeField] private Vector3 mirarEnElVagon = Vector3.forward;
    [Tooltip("Hacia dónde queda la cabeza, en el espacio del vagón (el «arriba» del vagón en reposo).")]
    [SerializeField] private Vector3 arribaEnElVagon = Vector3.up;
    [Tooltip("Pose en el puesto. None = de pie.")]
    [SerializeField] private NPCAmbientActivity pose = NPCAmbientActivity.None;

    [Header("Subida y bajada")]
    [Tooltip("Punto del suelo donde se sube y donde se baja el grupo, mirando hacia fuera de la atracción.")]
    [SerializeField] private Transform subida;

    [Header("Viaje")]
    [Tooltip("Segundos que dura el viaje.")]
    [SerializeField, Min(3f)] private float duracion = 30f;
    [Tooltip("Planos de cámara, en orden y en bucle hasta que acaba el viaje.")]
    [SerializeField] private Plano[] planos = { new Plano() };
    [Tooltip("Suavizado de la cámara entre fotogramas (0 = sigue al vagón sin retraso).")]
    [SerializeField, Range(0f, 0.95f)] private float suavizado = 0.85f;

    [Header("Escenitas")]
    [Tooltip("Lo que viven los del grupo en cada viaje. Vacío = viaje sin escenita.")]
    [SerializeField] private RepertorioDeAtraccion repertorio;
    [Tooltip("Dónde espera quien no sube, en el espacio del punto de subida.")]
    [SerializeField] private Vector3 esperaAbajo = new Vector3(1.5f, 0f, 1.5f);
    [Tooltip("Velocidad máxima (m/s) del jugador mientras está mareado.")]
    [SerializeField, Min(0.5f)] private float velocidadMareado = 1.6f;

    [Header("Ambiente a bordo")]
    [Tooltip("Gestos del torso que hacen al azar los que van montados entre frase y frase (no rompen la pose de sentado).")]
    [SerializeField] private string[] gestosAlAzar = { "Cheer01", "Cheer02", "HandWave01", "HandWave02", "Laugh01" };
    [Tooltip("Segundos entre un gesto al azar y el siguiente (mínimo y máximo).")]
    [SerializeField] private Vector2 cadaCuantoUnGesto = new Vector2(1.5f, 3.5f);

    [Header("Texto")]
    [Tooltip("Clave de la pregunta antes de subir (other_*.json).")]
    [SerializeField] private string clavePregunta = "ATRACCION_SUBIR";
    [Tooltip("Texto si la clave no existe.")]
    [SerializeField] private string preguntaPorDefecto = "¿Subir a la atracción?";

    private static readonly Slot[] Grupo = { Slot.Will, Slot.Estela, Slot.Liam };

    private readonly PasajeroJugador _jugador = new PasajeroJugador(ActionMode.Cinematic);
    private readonly List<PasajeroNpc> _companeros = new();
    private readonly List<GameObject> _puestosCreados = new();
    private Interactable _interactuable;
    private CinematicCameraDriver _camara;
    private NPCSimpleAnimator _animadorDelJugador;
    private Coroutine _viaje;
    private Renderer _cuerpoDelVagon;
    private RepertorioDeAtraccion.Escenita _escenita;
    private Slot? _noSube;
    private Coroutine _mareo;
    private string _claveDelTelon;
    private readonly List<Slot> _aBordo = new();
    private readonly List<Transform> _cuerposABordo = new();
    private readonly Dictionary<Slot, float> _libreDesde = new();

    /// <summary>True mientras el grupo va montado.</summary>
    public bool EnMarcha => _viaje != null;

    void Awake()
    {
        _interactuable = GetComponent<Interactable>();
        _claveDelTelon = $"atraccion:{gameObject.scene.name}/{name}";
    }

    void OnEnable()
    {
        if (_interactuable != null) _interactuable.OnInteract.AddListener(AlInteractuar);
    }

    void OnDisable()
    {
        if (_interactuable != null) _interactuable.OnInteract.RemoveListener(AlInteractuar);
        if (_mareo != null) { StopCoroutine(_mareo); _mareo = null; }
        TopeDeVelocidadDelJugador.Quitar(this);
        if (_viaje != null)
        {
            StopCoroutine(_viaje);
            _viaje = null;
            Bajar();
        }
    }

    void AlInteractuar(GameObject interactor)
    {
        if (_viaje != null) return;
        var popup = ConfirmationPopupUI.Instance;
        if (popup == null) { Montar(); return; }

        string pregunta = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(clavePregunta, preguntaPorDefecto)
            : preguntaPorDefecto;
        popup.Show(pregunta, onConfirm: Montar);
    }

    /// <summary>Sube al grupo y empieza el viaje. No hace nada si ya está en marcha.</summary>
    public void Montar()
    {
        if (_viaje != null || vagones == null || vagones.Length == 0 || subida == null) return;
        _viaje = StartCoroutine(Co_Viaje());
    }

    IEnumerator Co_Viaje()
    {
        _escenita = RegistroDeViajes.Elegir(repertorio, EstaEnElGrupo, AlQuien(ControlDelJugador.Activo));
        _noSube = _escenita != null && _escenita.noSube != QuienDelGrupo.Nadie ? AlSlot(_escenita.noSube) : (Slot?)null;
        yield return Momento(MomentoDelViaje.EnLaCola);

        Telon.Cerrar(_claveDelTelon);
        OcultablesDeCinematica.OcultarTodos(this);
        yield return null;

        Transform vagon = VagonMasCercano();
        _cuerpoDelVagon = vagon != null ? vagon.GetComponentInChildren<Renderer>() : null;
        Subir(vagon);

        if (_camara == null) _camara = GetComponent<CinematicCameraDriver>();
        if (_camara == null) _camara = gameObject.AddComponent<CinematicCameraDriver>();
        _camara.Activate();
        int plano = 0;
        float finDelPlano = 0f;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        bool primero = true;

        Telon.Soltar(_claveDelTelon);

        float[] cuando = { 1f, duracion * 0.45f, duracion * 0.8f };
        MomentoDelViaje[] cuales = { MomentoDelViaje.Arrancando, MomentoDelViaje.LoMasAlto, MomentoDelViaje.Bajando };
        int siguienteMomento = 0;
        float siguienteGesto = 1.5f;

        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            if (siguienteMomento < cuando.Length && t >= cuando[siguienteMomento])
                StartCoroutine(Momento(cuales[siguienteMomento++]));
            if (t >= siguienteGesto)
            {
                GestoAlAzar();
                siguienteGesto = t + UnityEngine.Random.Range(cadaCuantoUnGesto.x, Mathf.Max(cadaCuantoUnGesto.x, cadaCuantoUnGesto.y));
            }
            if (planos != null && planos.Length > 0)
            {
                if (t >= finDelPlano)
                {
                    if (!primero) plano = (plano + 1) % planos.Length;
                    finDelPlano = t + planos[plano].segundos;
                    primero = true;
                }
                Plano actual = planos[plano];
                float progreso = 1f - Mathf.Clamp01((finDelPlano - t) / actual.segundos);
                PoseDelPlano(actual, vagon, progreso, out Vector3 p, out Quaternion r);
                float k = primero ? 0f : suavizado;
                pos = Vector3.Lerp(p, pos, k);
                rot = Quaternion.Slerp(r, rot, k);
                float fov = actual.fovFinal > 0f ? Mathf.Lerp(actual.fov, actual.fovFinal, Mathf.SmoothStep(0f, 1f, progreso)) : actual.fov;
                _camara.Cut(pos, rot, fov);
                primero = false;
            }
            yield return null;
        }

        Telon.Cerrar(_claveDelTelon);
        yield return null;
        Bajar();
        Marear();
        yield return new WaitForSeconds(0.6f);
        yield return Momento(MomentoDelViaje.AlBajar);
        _escenita = null;
        _viaje = null;
    }

    // ── Escenitas ──────────────────────────────────────────────────────────────────────────

    /// <summary>Los golpes de la escenita en ese momento, en orden; espera a que acaben.</summary>
    IEnumerator Momento(MomentoDelViaje momento)
    {
        if (_escenita == null || _escenita.golpes == null) yield break;
        foreach (var golpe in _escenita.golpes)
        {
            if (golpe == null || golpe.momento != momento) continue;
            if (golpe.espera > 0f) yield return new WaitForSeconds(golpe.espera);
            float dura = Hacer(golpe);
            if (!golpe.aLaVez && dura > 0f) yield return new WaitForSeconds(dura);
        }
    }

    /// <summary>Dice la frase y hace el gesto y la cara del golpe. Devuelve cuánto dura la frase.</summary>
    float Hacer(RepertorioDeAtraccion.Golpe golpe)
    {
        if (golpe.quien == QuienDelGrupo.Nadie) return 0f;
        Slot slot = AlSlot(golpe.quien);
        var cuerpo = ControlDelJugador.CuerpoDe(slot);
        if (cuerpo == null) return 0f;

        var animador = cuerpo.GetComponentInChildren<NPCSimpleAnimator>();
        if (animador != null)
        {
            if (golpe.emocion != NPCEmotion.None) animador.SetFaceEmotion(golpe.emocion);
            if (!string.IsNullOrEmpty(golpe.gesto)) animador.PlaySocialGesture(golpe.gesto);
        }

        if (string.IsNullOrEmpty(golpe.clave) && string.IsNullOrEmpty(golpe.texto))
        {
            _libreDesde[slot] = Time.time + 1.5f;
            return 0.8f;
        }
        var dicha = PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = golpe.clave,
            textoPorDefecto = golpe.texto,
            ancla = cuerpo.transform,
        });
        float dura = dicha.Duracion > 0f ? dicha.Duracion : 0f;
        // Mientras dice su frase no hace gestos al azar.
        _libreDesde[slot] = Time.time + dura + 0.5f;
        return dura;
    }

    /// <summary>
    /// Entre frase y frase, uno de los que van montados (que no esté con un golpe de la escenita)
    /// hace un gesto al azar: vitorea, saluda a los de abajo, se ríe.
    /// </summary>
    void GestoAlAzar()
    {
        if (gestosAlAzar == null || gestosAlAzar.Length == 0 || _aBordo.Count == 0) return;
        int inicio = UnityEngine.Random.Range(0, _aBordo.Count);
        for (int i = 0; i < _aBordo.Count; i++)
        {
            Slot slot = _aBordo[(inicio + i) % _aBordo.Count];
            if (_libreDesde.TryGetValue(slot, out float libre) && Time.time < libre) continue;
            var cuerpo = ControlDelJugador.CuerpoDe(slot);
            var animador = cuerpo != null ? cuerpo.GetComponentInChildren<NPCSimpleAnimator>() : null;
            if (animador == null) continue;
            animador.PlaySocialGesture(gestosAlAzar[UnityEngine.Random.Range(0, gestosAlAzar.Length)]);
            _libreDesde[slot] = Time.time + UnityEngine.Random.Range(3f, 6f);
            return;
        }
    }

    /// <summary>Quien se marea: el jugador anda despacio un rato; un compañero se tambalea.</summary>
    void Marear()
    {
        if (_escenita == null || _escenita.seMarea == QuienDelGrupo.Nadie) return;
        Slot quien = AlSlot(_escenita.seMarea);
        var cuerpo = ControlDelJugador.CuerpoDe(quien);
        if (cuerpo == null) return;
        var animador = cuerpo.GetComponentInChildren<NPCSimpleAnimator>();
        if (animador != null) animador.PlaySocialGesture("Dizzy_NoWeapon");
        if (quien == ControlDelJugador.Activo)
        {
            if (_mareo != null) StopCoroutine(_mareo);
            _mareo = StartCoroutine(Co_Mareo(_escenita.segundosDeMareo));
        }
    }

    IEnumerator Co_Mareo(float segundos)
    {
        TopeDeVelocidadDelJugador.Poner(this, velocidadMareado);
        yield return new WaitForSeconds(segundos);
        TopeDeVelocidadDelJugador.Quitar(this);
        _mareo = null;
    }

    static bool EstaEnElGrupo(QuienDelGrupo quien)
        => quien == QuienDelGrupo.Nadie || (ControlDelJugador.EnElGrupo(AlSlot(quien)) && ControlDelJugador.CuerpoDe(AlSlot(quien)) != null);

    static QuienDelGrupo AlQuien(Slot slot) => slot switch
    {
        Slot.Estela => QuienDelGrupo.Estela,
        Slot.Liam => QuienDelGrupo.Liam,
        _ => QuienDelGrupo.Will,
    };

    static Slot AlSlot(QuienDelGrupo quien) => quien switch
    {
        QuienDelGrupo.Estela => Slot.Estela,
        QuienDelGrupo.Liam => Slot.Liam,
        _ => Slot.Will,
    };

    Transform VagonMasCercano()
    {
        Transform mejor = null;
        float mejorDistancia = float.MaxValue;
        for (int i = 0; i < vagones.Length; i++)
        {
            if (vagones[i] == null) continue;
            float d = (vagones[i].position - subida.position).sqrMagnitude;
            if (d < mejorDistancia) { mejorDistancia = d; mejor = vagones[i]; }
        }
        return mejor;
    }

    void Subir(Transform vagon)
    {
        if (vagon == null) return;
        Vector3 mirar = mirarEnElVagon.sqrMagnitude > 0.0001f ? mirarEnElVagon.normalized : Vector3.forward;
        Vector3 arriba = arribaEnElVagon.sqrMagnitude > 0.0001f ? arribaEnElVagon.normalized : Vector3.up;
        Quaternion giroLocal = Quaternion.LookRotation(mirar, arriba);
        int puesto = 0;
        _aBordo.Clear();
        _cuerposABordo.Clear();
        _libreDesde.Clear();

        // Quien lleva el mando, en el primer puesto.
        var cuerpo = PlayerService.Player;
        if (cuerpo != null && _jugador.Tomar(cuerpo))
        {
            var sitio = CrearPuesto(vagon, puesto++, giroLocal);
            cuerpo.transform.SetParent(sitio, false);
            cuerpo.transform.localPosition = Vector3.zero;
            cuerpo.transform.localRotation = Quaternion.identity;
            _animadorDelJugador = cuerpo.GetComponentInChildren<NPCSimpleAnimator>();
            if (_animadorDelJugador != null && pose != NPCAmbientActivity.None) _animadorDelJugador.PlayAmbientActivity(pose);
            if (_animadorDelJugador != null) _animadorDelJugador.SetFaceEmotion(NPCEmotion.Happy);
            _aBordo.Add(ControlDelJugador.Activo);
            _cuerposABordo.Add(cuerpo.transform);
        }

        // Los compañeros presentes, cada uno en el siguiente puesto.
        _companeros.Clear();
        for (int i = 0; i < Grupo.Length && puesto < puestos.Length; i++)
        {
            Slot personaje = Grupo[i];
            if (personaje == ControlDelJugador.Activo || !ControlDelJugador.EnElGrupo(personaje)) continue;
            var npc = ControlDelJugador.NpcDe(personaje);
            if (npc == null || npc.EstaCaido) continue;

            var llevado = new PasajeroNpc(npc, this);
            llevado.Anclar();
            llevado.TomarControl();
            if (_noSube.HasValue && _noSube.Value == personaje)
            {
                // Se queda abajo, de pie junto a la subida, mirando la atracción.
                var espera = new GameObject("Espera abajo").transform;
                espera.SetParent(subida, false);
                espera.localPosition = esperaAbajo;
                _puestosCreados.Add(espera.gameObject);
                Vector3 hacia = vagon.position - espera.position; hacia.y = 0f;
                llevado.Colgar(espera, Quaternion.LookRotation(hacia.sqrMagnitude > 0.01f ? hacia : subida.forward), NPCAmbientActivity.None);
                _companeros.Add(llevado);
                continue;
            }
            var sitio = CrearPuesto(vagon, puesto++, giroLocal);
            llevado.Colgar(sitio, sitio.rotation, pose);
            if (llevado.Animador != null) llevado.Animador.SetFaceEmotion(NPCEmotion.Happy);
            _companeros.Add(llevado);
            _aBordo.Add(personaje);
            _cuerposABordo.Add(npc.transform);
        }
    }

    Transform CrearPuesto(Transform vagon, int indice, Quaternion giroLocal)
    {
        var go = new GameObject($"Puesto {indice + 1}");
        go.transform.SetParent(vagon, false);
        go.transform.localPosition = puestos[indice];
        go.transform.localRotation = giroLocal;
        _puestosCreados.Add(go);
        return go.transform;
    }

    void PoseDelPlano(Plano plano, Transform vagon, float progreso, out Vector3 pos, out Quaternion rot)
    {
        // Se mira al cuerpo del vagón (la barca, la cabina), no a su eje de giro, que puede quedar muy arriba.
        // Un poco por debajo del centro de su caja, que en una barca colgada incluye los brazos.
        Vector3 centro = vagon != null ? vagon.position : transform.position;
        if (_cuerpoDelVagon != null)
        {
            Bounds caja = _cuerpoDelVagon.bounds;
            centro = caja.center - Vector3.up * (caja.extents.y * 0.4f);
        }
        if (plano.punto != null)
        {
            pos = plano.punto.position;
            Vector3 hacia = plano.seguirAlVagon ? centro - pos : plano.punto.forward;
            rot = Quaternion.LookRotation(hacia.sqrMagnitude > 0.0001f ? hacia : Vector3.forward, Vector3.up);
            return;
        }
        if (vagon == null) { pos = transform.position; rot = transform.rotation; return; }
        // A bordo la cámara va en los ejes del grupo: delante es hacia donde miran de verdad (no hacia
        // donde se supone que mira el asiento), así el plano a la cara siempre les ve la cara. Se inclina
        // con el vagón (el balanceo se nota) y puede dar un paseo alrededor de ellos (giro).
        Vector3 arribaLocal = arribaEnElVagon.sqrMagnitude > 0.0001f ? arribaEnElVagon.normalized : Vector3.up;
        Vector3 arriba = vagon.TransformDirection(arribaLocal).normalized;
        Vector3 delante = Vector3.ProjectOnPlane(MiradaDelGrupo(vagon), arriba);
        if (delante.sqrMagnitude < 0.0001f) delante = Vector3.ProjectOnPlane(vagon.forward, arriba);
        Quaternion ejes = Quaternion.LookRotation(delante.normalized, arriba)
                          * Quaternion.AngleAxis(plano.giro * (progreso - 0.5f), Vector3.up);
        pos = vagon.TransformPoint(CentroDelGrupo()) + ejes * plano.aBordo;
        Vector3 dir = ejes * plano.mirarABordo;
        rot = Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : delante, arriba);
    }

    /// <summary>
    /// Hacia dónde miran los montados (la media de sus cuerpos; en los humanoides, la del cuerpo animado,
    /// que es la que cuenta si la animación de sentarse los gira). Sin nadie, hacia donde mira el puesto.
    /// </summary>
    Vector3 MiradaDelGrupo(Transform vagon)
    {
        Vector3 suma = Vector3.zero;
        foreach (Transform cuerpo in _cuerposABordo)
        {
            if (cuerpo == null) continue;
            var animador = cuerpo.GetComponentInChildren<Animator>();
            suma += animador != null && animador.isHuman ? animador.bodyRotation * Vector3.forward : cuerpo.forward;
        }
        if (suma.sqrMagnitude > 0.0001f) return suma.normalized;
        return vagon.TransformDirection(mirarEnElVagon.sqrMagnitude > 0.0001f ? mirarEnElVagon : Vector3.forward);
    }

    /// <summary>Centro de los puestos ocupados, en el espacio del vagón (los pies del grupo).</summary>
    Vector3 CentroDelGrupo()
    {
        if (puestos == null || puestos.Length == 0) return Vector3.zero;
        int n = Mathf.Clamp(_aBordo.Count, 1, puestos.Length);
        Vector3 suma = Vector3.zero;
        for (int i = 0; i < n; i++) suma += puestos[i];
        return suma / n;
    }

    /// <summary>Deja al grupo de pie en la subida, devuelve la cámara y el HUD y destapa la pantalla.</summary>
    void Bajar()
    {
        Vector3 destino = subida != null ? subida.position : transform.position;
        Quaternion mirada = subida != null ? subida.rotation : transform.rotation;

        if (_jugador.Ocupado)
        {
            if (_animadorDelJugador != null && pose != NPCAmbientActivity.None) _animadorDelJugador.StopAmbientActivity(pose);
            _jugador.DejarEn(destino, mirada);
        }
        _animadorDelJugador = null;
        foreach (Slot slot in _aBordo)
        {
            var cuerpo = ControlDelJugador.CuerpoDe(slot);
            var animador = cuerpo != null ? cuerpo.GetComponentInChildren<NPCSimpleAnimator>() : null;
            if (animador != null) animador.SetFaceEmotion(NPCEmotion.Neutral);
        }
        _aBordo.Clear();
        _cuerposABordo.Clear();
        _libreDesde.Clear();

        for (int i = 0; i < _companeros.Count; i++) _companeros[i].Soltar(destino, i + 1);
        _companeros.Clear();

        for (int i = 0; i < _puestosCreados.Count; i++)
            if (_puestosCreados[i] != null) Destroy(_puestosCreados[i]);
        _puestosCreados.Clear();

        if (_camara != null) _camara.Deactivate();
        OcultablesDeCinematica.MostrarTodos(this);
        Telon.Soltar(_claveDelTelon);
    }

    /// <summary>Lo pone el constructor de la escena.</summary>
    public void Configurar(Transform[] nuevosVagones, Vector3[] nuevosPuestos, Vector3 mirar, Vector3 arriba, NPCAmbientActivity nuevaPose,
                           Transform nuevaSubida, float nuevaDuracion, Plano[] nuevosPlanos, string clave, string porDefecto,
                           RepertorioDeAtraccion nuevoRepertorio)
    {
        repertorio = nuevoRepertorio;
        vagones = nuevosVagones;
        puestos = nuevosPuestos;
        mirarEnElVagon = mirar;
        arribaEnElVagon = arriba;
        pose = nuevaPose;
        subida = nuevaSubida;
        duracion = nuevaDuracion;
        planos = nuevosPlanos;
        clavePregunta = clave;
        preguntaPorDefecto = porDefecto;
    }
}
