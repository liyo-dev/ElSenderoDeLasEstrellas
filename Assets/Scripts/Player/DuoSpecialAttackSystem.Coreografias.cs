using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Core;
using Sendero.Core.Feedback;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Puesta en escena de los ataques de equipo. Cada ataque es una corrutina que dura de principio a
/// fin y es la dueña del reloj: el impacto, el cierre y la vuelta al suelo ocurren dentro de ella,
/// así que nadie queda a medias de una pose o en el aire.
/// <list type="bullet">
/// <item>Dúo con Estela: los dos corren, saltan y quedan suspendidos en cámara lenta con los brazos
/// arriba, en el centro del círculo, mientras caen los meteoros alrededor.</item>
/// <item>Dúo con Liam (y Estela + Liam): cara a cara en la pose de la Y; a la vez pasan a brazos
/// arriba y sobre sus cabezas se abre el agujero negro, que atrae a los enemigos: los que chocan con
/// él mueren o, si les queda vida, salen escupidos.</item>
/// <item>Trío: tres franjas con las caras y la pose de «aquí hemos llegado»; saltan los tres y, al
/// caer, cada uno adopta su pose y la magia sale por todas partes.</item>
/// </list>
/// Los actores se mueven con la transformación (sin NavMeshAgent) y sus poses las sostiene un bucle
/// que fuerza el peso de la capa superior cada fotograma, porque otros sistemas (el gesto de
/// hechizo del jugador, el modo batalla de los compañeros) la bajan a 0 al acabar sus clips.
/// </summary>
public partial class DuoSpecialAttackSystem
{
    // Altura (m sobre el suelo) a la que queda el agujero negro, sobre la cabeza de los dos.
    private const float AlturaDelAgujero = 2.8f;
    // El prefab del ataque nombra así a su agujero negro (hijo directo de la raíz).
    private const string NombreDelAgujeroEnElPrefab = "VFX_AgujeroNegro";
    private const float SeparacionDelDuo = 0.8f;
    private const float DistanciaDeImpulso = 2.2f;
    private const float VelocidadDeAtraccion = 16f;
    private const float RadioDelTrio = 1.1f;

    /// <summary>Un personaje que participa en el ataque y cómo estaba antes, para devolverlo igual.</summary>
    private sealed class ActorDeEquipo
    {
        public Transform t;
        public Animator anim;
        public NPCSimpleAnimator npc;
        public Invector.vCharacterController.vThirdPersonController motor; // solo el jugador
        public Rigidbody rb;
        public NavMeshAgent agent;
        public bool agenteEncendido, rootMotion;
        public int capaPose;
        public string poseSuperior, poseBase;
        public int hashBase;
        public float sueloY;
    }

    private readonly List<ActorDeEquipo> _actores = new List<ActorDeEquipo>(3);
    private readonly List<Damageable> _absorbidos = new List<Damageable>(32);
    private readonly List<GameObject> _actionAuras = new List<GameObject>();
    private Coroutine _orbitCameraRoutine;
    private Coroutine _mantenerPosesRoutine;
    private PlayerActionManager _accion;
    private bool _modoCinematico;
    private int _suelosMask = 1;
    private float _ultimaAbsorcion;

    // ── Ejecución ──────────────────────────────────────────────────────────

    private void Execute(SpecialAttackSO attack, float cost)
    {
        if (_isExecuting) return;
        ResolveCaster();
        if (magicCaster == null) { Deny("Sin MagicCaster", "TEAM_DENIED_BUSY", "Ahora no se puede."); return; }
        if (!magicCaster.BeginTeamGesture(attack.gestureSeconds, MagicCastStyle.TwoHanded, out Vector3 facing)) { Deny("El personaje no puede lanzar ahora", "TEAM_DENIED_BUSY", "Ahora no se puede."); return; }
        if (!teamGauge.TryConsume(cost)) { Deny("Carga insuficiente", "TEAM_DENIED_CHARGE", "Falta carga de equipo."); return; }

        StartCoroutine(Co_Execute(attack, facing));
        OnTeamAttack?.Invoke(attack);
        Log($"{attack.displayName} (gasta {cost:0.#} tramo/s)");
    }

    private IEnumerator Co_Execute(SpecialAttackSO attack, Vector3 facing)
    {
        _isExecuting = true;
        Transform body = magicCaster.Body;
        if (body == null)
        {
            _isExecuting = false;
            yield break;
        }

        Vector3 fwd = facing.sqrMagnitude > 0.01f ? facing.normalized : body.forward;
        PrepararActores(body);
        PonerModoCinematico(true);
        _mantenerPosesRoutine = StartCoroutine(Co_MantenerPoses());
        ActivarAurasDeEquipo(attack);

        BandasDeCineUI.Obtener(transform).Mostrar(true, 0.10f, 0.2f);
        FeedbackService.Vibrar(0.3f, 0.6f, 0.18f);
        if (!string.IsNullOrEmpty(attack.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(attack.castSFXKey);

        if (attack.isTrio && _actores.Count >= 3)
            yield return Co_Trio(attack, fwd);
        else if (_actores.Count >= 2 && attack.IsDuoOf(Slot.Will, Slot.Estela))
            yield return Co_DuoEstela(attack, fwd, PuntoDelGolpe(body, fwd));
        else if (_actores.Count >= 2)
            yield return Co_DuoCaraACara(attack, fwd, attack.IsDuoOf(Slot.Will, Slot.Liam));

        LimpiarPuestaEnEscena();
        _isExecuting = false;
    }

    /// Dónde cae el golpe: sobre el objetivo si está al alcance, o delante. Siempre a 3 m o más.
    private Vector3 PuntoDelGolpe(Transform body, Vector3 fwd)
    {
        Vector3 centro = body.position + fwd * frontDistance;
        if (magicCaster.TryGetTarget(out Transform target))
        {
            Vector3 plano = target.position - body.position; plano.y = 0f;
            if (plano.sqrMagnitude <= targetReach * targetReach) centro = target.position;
        }
        Vector3 d = centro - body.position; d.y = 0f;
        if (d.magnitude < 3f) centro = body.position + (d.sqrMagnitude > 0.01f ? d.normalized : fwd) * 3f;
        centro.y = SueloEn(centro, body.position.y);
        return centro;
    }

    // ── Dúo con Estela ─────────────────────────────────────────────────────

    private IEnumerator Co_DuoEstela(SpecialAttackSO attack, Vector3 fwd, Vector3 centro)
    {
        ActorDeEquipo a = _actores[0], b = _actores[1];
        Vector3 media = (a.t.position + b.t.position) * 0.5f;
        Vector3 dir = centro - media; dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : fwd;

        // Will y Estela se colocan espalda con espalda en el centro del círculo mágico.
        // Se pegan espalda con espalda (distancia de 0.4m entre ellos, 0.2m a cada lado del centro)
        // a lo largo del eje frontal para que uno mire hacia atrás (-dir) y el otro al frente (dir).
        Vector3 metaA = centro - dir * 0.28f, metaB = centro + dir * 0.28f;
        metaA.y = SueloEn(metaA, a.sueloY); metaB.y = SueloEn(metaB, b.sueloY);
        Vector3 saltoA = metaA - dir * DistanciaDeImpulso, saltoB = metaB + dir * 0.5f;
        saltoA.y = SueloEn(saltoA, a.sueloY); saltoB.y = SueloEn(saltoB, b.sueloY);

        MostrarCaras(1.3f, false);
        LineasDeConcentracionUI.Obtener(transform).Mostrar(true, new Color(1f, 1f, 1f, 0.75f), 0.35f, 0.15f, 1.4f);
        ArrancarOrbita(centro, 5.2f, 75f, 2.2f, 2.4f, 6.2f);
        PonerEmociones(NPCEmotion.Determined);

        // 1. Carrera hacia el centro
        TimeScaleArbiterService.Request(this, 0.85f);
        Vector3 inicioA = a.t.position, inicioB = b.t.position;
        Vector3 haciaA = DireccionPlana(saltoA - inicioA, dir), haciaB = DireccionPlana(metaB - inicioB, dir);
        float largo = Mathf.Max(Vector3.Distance(inicioA, saltoA), Vector3.Distance(inicioB, metaB));
        PonerBase(a, poseCarrera); PonerBase(b, poseCarrera);
        yield return Co_Tramo(Mathf.Clamp(largo / velocidadDeCarrera, 0.35f, 1.2f), t =>
        {
            Colocar(a, Vector3.Lerp(inicioA, saltoA, t)); Orientar(a, haciaA);
            Colocar(b, Vector3.Lerp(inicioB, metaB, t)); Orientar(b, haciaB);
        }, false);

        // 2. Salto: despegan y en el aire quedan estáticos espalda con espalda
        TimeScaleArbiterService.Request(this, escalaDeTiempoEnElAire);
        VolverAIdle(a); VolverAIdle(b);
        PonerBase(a, poseEnElAire); PonerBase(b, poseEnElAire);
        PonerPose(a, poseInvocacionDuoEstela); PonerPose(b, poseInvocacionDuoEstela);
        PlayTeamVfx(attack, centro, dir, a.t);
        if (AudioService.Instance != null) AudioService.Instance.PlaySFX("SpecialAttack_Meteoros");
        float h = alturaSaltoDuoEstela;
        Vector3 aireA = metaA + Vector3.up * h, aireB = metaB + Vector3.up * h;
        yield return Co_Tramo(0.45f, t =>
        {
            float subida = Mathf.Sin(t * Mathf.PI * 0.5f) * h;
            Colocar(a, Vector3.Lerp(saltoA, metaA, t) + Vector3.up * subida);
            Orientar(a, Vector3.Slerp(dir, -dir, t)); // Will gira para quedar de espaldas
            Colocar(b, metaB + Vector3.up * subida);
            Orientar(b, dir); // Estela mira al frente
        }, false);

        // 3. Suspendidos en lo alto: estáticos espalda con espalda en el centro del círculo, congelando la pose para evitar flexiones
        CongelarAnimador(a, true);
        CongelarAnimador(b, true);
        yield return Co_Tramo(segundosEnLoAltoDuo, t =>
        {
            Colocar(a, aireA);
            Orientar(a, -dir);
            Colocar(b, aireB);
            Orientar(b, dir);
        }, true);
        CongelarAnimador(a, false);
        CongelarAnimador(b, false);

        // 4. Impacto y caída: descienden firmes manteniendo la espalda con espalda hasta tocar el suelo
        FinalizarImpacto(attack, centro, dir, a.t);
        TimeScaleArbiterService.Request(this, 0.7f);
        yield return Co_Tramo(0.28f, t =>
        {
            float caida = t * t;
            Colocar(a, Vector3.Lerp(aireA, metaA, caida));
            Orientar(a, -dir);
            Colocar(b, Vector3.Lerp(aireB, metaB, caida));
            Orientar(b, dir);
        }, false);

        // 5. Aterrizaje firme espalda con espalda: adoptan la pose de aterrizaje sin girarse ni ponerse en fila
        a.sueloY = metaA.y; b.sueloY = metaB.y;
        if (a.motor != null) { a.motor.CancelAirHold(); a.motor.ReleaseAirbornePose(); }
        if (b.motor != null) { b.motor.CancelAirHold(); b.motor.ReleaseAirbornePose(); }
        Colocar(a, metaA); Orientar(a, -dir);
        Colocar(b, metaB); Orientar(b, dir);
        PonerPose(a, null); PonerPose(b, null);
        VolverAIdle(a); VolverAIdle(b);
        PonerPose(a, poseAterrizajeDuoEstela);
        PonerPose(b, poseAterrizajeDuoEstela);
        yield return Co_Tramo(0.45f, t =>
        {
            Colocar(a, metaA); Orientar(a, -dir);
            Colocar(b, metaB); Orientar(b, dir);
        }, true);
    }

    // ── Dúos con Liam: cara a cara y agujero negro ─────────────────────────

    private IEnumerator Co_DuoCaraACara(SpecialAttackSO attack, Vector3 fwd, bool conAgujero)
    {
        ActorDeEquipo a = _actores[0], b = _actores[1];
        Vector3 centro = a.t.position;
        Vector3 eje = DireccionPlana(fwd, a.t.forward);
        Vector3 posA = centro - eje * 0.6f, posB = centro + eje * 0.6f;
        posA.y = SueloEn(posA, a.sueloY); posB.y = SueloEn(posB, b.sueloY);
        centro.y = posA.y;
        Vector3 puntoAgujero = centro + Vector3.up * AlturaDelAgujero;

        ArrancarOrbita(centro, 6.5f, 150f, 2.2f, 2.4f, 7.0f);
        MostrarCaras(1.3f, false);
        PonerEmociones(NPCEmotion.Determined);
        LineasDeConcentracionUI.Obtener(transform).Mostrar(true, new Color(1f, 1f, 1f, 0.75f), 0.35f, 0.15f, 1.4f);

        // 1. Se colocan uno frente al otro: el que está lejos corre hasta su sitio
        TimeScaleArbiterService.Request(this, 0.8f);
        Vector3 inicioA = a.t.position, inicioB = b.t.position;
        float largo = Mathf.Max(Vector3.Distance(inicioA, posA), Vector3.Distance(inicioB, posB));
        if (largo > 0.3f)
        {
            Vector3 haciaA = DireccionPlana(posA - inicioA, eje), haciaB = DireccionPlana(posB - inicioB, -eje);
            PonerBase(a, poseCarrera); PonerBase(b, poseCarrera);
            yield return Co_Tramo(Mathf.Clamp(largo / velocidadDeCarrera, 0.2f, 1f), t =>
            {
                Colocar(a, Vector3.Lerp(inicioA, posA, t)); Orientar(a, haciaA);
                Colocar(b, Vector3.Lerp(inicioB, posB, t)); Orientar(b, haciaB);
            }, false);
        }

        // 2. Pose de la Y del combo, cara a cara, firmes en el suelo
        VolverAIdle(a); VolverAIdle(b);
        PonerPose(a, poseDeLaY); PonerPose(b, poseDeLaY);
        yield return Co_Tramo(0.9f, t =>
        {
            Colocar(a, posA); Orientar(a, eje);
            Colocar(b, posB); Orientar(b, -eje);
        }, true);

        // 3. A la vez, alzan las manos y canalizan el hechizo sosteniendo el agujero negro sobre ellos
        string poseConjuracion = !string.IsNullOrEmpty(poseSostenerMagia) ? poseSostenerMagia : poseBrazosArriba;
        PonerPose(a, poseConjuracion); PonerPose(b, poseConjuracion);
        TimeScaleArbiterService.Request(this, 0.6f);
        Transform vfx = PlayTeamVfx(attack, centro, eje, a.t);
        if (conAgujero) ElevarAgujero(vfx, attack);
        if (AudioService.Instance != null) AudioService.Instance.PlaySFX(conAgujero ? "SpecialAttack_AgujeroNegro" : "SpecialAttack_Meteoros");
        FeedbackService.CameraShake(0.3f, 0.4f);
        _ultimaAbsorcion = -1f;
        yield return Co_Tramo(conAgujero ? segundosDelAgujero : 1.2f, t =>
        {
            Colocar(a, posA); Orientar(a, eje);
            Colocar(b, posB); Orientar(b, -eje);
            if (conAgujero && Time.unscaledTime - _ultimaAbsorcion >= 0.08f)
            {
                _ultimaAbsorcion = Time.unscaledTime;
                AbsorberEnemigos(attack, centro, puntoAgujero);
            }
        }, false);

        // 4. El agujero colapsa: lo que no llegó a tocarlo recibe la onda
        Colocar(a, posA); Orientar(a, eje);
        Colocar(b, posB); Orientar(b, -eje);
        FinalizarImpacto(attack, centro, eje, a.t);
        TimeScaleArbiterService.Request(this, 0.6f);
        yield return Co_Tramo(0.5f, t =>
        {
            Colocar(a, posA); Orientar(a, eje);
            Colocar(b, posB); Orientar(b, -eje);
        }, true);
    }

    /// Atrae a los enemigos hacia el agujero. Los que llegan a él reciben el golpe del ataque: si
    /// mueren, mueren; si les queda vida, salen escupidos.
    private void AbsorberEnemigos(SpecialAttackSO attack, Vector3 suelo, Vector3 puntoAgujero)
    {
        int n = Physics.OverlapSphereNonAlloc(suelo, attack.aoeRadius * 1.5f, _aoeHitsBuffer, _enemyBossLayerMask);
        float radioDeChoque2 = radioDeChoqueDelAgujero * radioDeChoqueDelAgujero;
        for (int i = 0; i < n; i++)
        {
            var enemigo = _aoeHitsBuffer[i].GetComponentInParent<Damageable>();
            if (enemigo == null || !enemigo.IsAlive || _absorbidos.Contains(enemigo)) continue;

            Vector3 plano = enemigo.transform.position - suelo; plano.y = 0f;
            if (plano.sqrMagnitude > radioDeChoque2)
            {
                EstadosDeCombate.Aplicar(enemigo, EstadoDeCombate.Atraer, 0.25f, VelocidadDeAtraccion, suelo, null);
                continue;
            }

            _absorbidos.Add(enemigo);
            using (GolpeDeRemate.Abrir()) enemigo.TakeDamage(attack.damage);
            if (enemigo != null && enemigo.IsAlive)
                EstadosDeCombate.Aplicar(enemigo, EstadoDeCombate.Empujar, 1f, 28f, suelo, null);
            FeedbackService.CameraShake(0.35f, 0.15f);
        }
    }

    /// El prefab del ataque trae el agujero a una altura fija; se sube sobre la cabeza de los dos.
    private static void ElevarAgujero(Transform raiz, SpecialAttackSO attack)
    {
        if (raiz == null) return;
        for (int i = 0; i < raiz.childCount; i++)
        {
            Transform hijo = raiz.GetChild(i);
            if (!hijo.name.StartsWith(NombreDelAgujeroEnElPrefab, StringComparison.Ordinal)) continue;
            Vector3 local = hijo.localPosition;
            hijo.localPosition = new Vector3(local.x, AlturaDelAgujero - attack.elevacionDelVfx, local.z);
        }
    }

    // ── Trío ───────────────────────────────────────────────────────────────

    private IEnumerator Co_Trio(SpecialAttackSO attack, Vector3 fwd)
    {
        ActorDeEquipo w = _actores[0], b = _actores[1], c = _actores[2];
        Vector3 centro = w.t.position;
        Vector3 frente = DireccionPlana(fwd, w.t.forward);

        // Triángulo mirando hacia fuera, el personaje activo al frente
        var dirs = new[] { frente, Quaternion.Euler(0f, 120f, 0f) * frente, Quaternion.Euler(0f, -120f, 0f) * frente };
        var actores = new[] { w, b, c };
        var sitios = new Vector3[3];
        var aterrizajes = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            sitios[i] = centro + dirs[i] * RadioDelTrio;
            sitios[i].y = SueloEn(sitios[i], actores[i].sueloY);
            aterrizajes[i] = sitios[i] + dirs[i] * 0.8f;
            aterrizajes[i].y = SueloEn(aterrizajes[i], actores[i].sueloY);
        }

        if (auraFormacionTrio != null)
            _actionAuras.Add(Instantiate(auraFormacionTrio, new Vector3(centro.x, sitios[0].y, centro.z), Quaternion.identity));

        // 1. Se reúnen
        TimeScaleArbiterService.Request(this, 0.7f);
        var inicios = new[] { w.t.position, b.t.position, c.t.position };
        float largo = 0f;
        for (int i = 0; i < 3; i++)
        {
            largo = Mathf.Max(largo, Vector3.Distance(inicios[i], sitios[i]));
            PonerBase(actores[i], poseCarrera);
        }
        yield return Co_Tramo(Mathf.Clamp(largo / velocidadDeCarrera, 0.3f, 0.9f), t =>
        {
            for (int i = 0; i < 3; i++)
            {
                Colocar(actores[i], Vector3.Lerp(inicios[i], sitios[i], t));
                Orientar(actores[i], DireccionPlana(sitios[i] - inicios[i], dirs[i]));
            }
        }, false);

        // 2. «Aquí hemos llegado»: las tres caras en las franjas y la pose de llegada, casi congelado
        for (int i = 0; i < 3; i++)
        {
            VolverAIdle(actores[i]);
            Colocar(actores[i], sitios[i]);
            Orientar(actores[i], dirs[i]);
            PonerPose(actores[i], poseLlegadaTrio);
        }
        TimeScaleArbiterService.Request(this, 0.2f);
        MostrarCaras(segundosDeLasCaras, true);
        PonerEmociones(NPCEmotion.Determined);
        LineasDeConcentracionUI.Obtener(transform).Mostrar(true, new Color(1f, 1f, 1f, 0.75f), 0.35f, 0.15f, segundosDeLasCaras);
        ArrancarOrbita(centro, segundosDeLasCaras + 3.2f, 140f, 2.5f, 2.8f, 7.8f);
        yield return new WaitForSecondsRealtime(segundosDeLasCaras);

        // 3. Saltan los tres a la vez, en cámara lenta y con los brazos arriba
        TimeScaleArbiterService.Request(this, escalaDeTiempoEnElAire);
        for (int i = 0; i < 3; i++)
        {
            PonerPose(actores[i], null);
            PonerBase(actores[i], poseEnElAire);
            PonerPose(actores[i], poseBrazosArriba);
        }
        float h = alturaSaltoTrio;
        yield return Co_Tramo(0.55f, t =>
        {
            float subida = Mathf.Sin(t * Mathf.PI * 0.5f) * h;
            for (int i = 0; i < 3; i++)
            {
                Colocar(actores[i], Vector3.Lerp(sitios[i], aterrizajes[i], t * 0.5f) + Vector3.up * subida);
                Orientar(actores[i], dirs[i]);
            }
        }, false);
        var altos = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            altos[i] = actores[i].t.position;
            CongelarAnimador(actores[i], true);
        }
        yield return Co_Tramo(0.5f, t =>
        {
            for (int i = 0; i < 3; i++) Colocar(actores[i], altos[i]);
        }, true);
        for (int i = 0; i < 3; i++) CongelarAnimador(actores[i], false);

        // 4. Caen: al tocar el suelo, cada uno adopta su pose y la magia sale por todas partes
        TimeScaleArbiterService.Request(this, 1f);
        var caidas = new Vector3[3];
        for (int i = 0; i < 3; i++) caidas[i] = actores[i].t.position;
        yield return Co_Tramo(0.22f, t =>
        {
            float caida = t * t;
            for (int i = 0; i < 3; i++)
            {
                Colocar(actores[i], Vector3.Lerp(caidas[i], aterrizajes[i], caida));
                Orientar(actores[i], dirs[i]);
            }
        }, false);

        for (int i = 0; i < 3; i++)
        {
            actores[i].sueloY = aterrizajes[i].y;
            if (actores[i].motor != null)
            {
                actores[i].motor.CancelAirHold();
                actores[i].motor.ReleaseAirbornePose();
            }
            Colocar(actores[i], aterrizajes[i]);
            Orientar(actores[i], dirs[i]);
        }

        PlayTeamVfx(attack, centro, frente, w.t);
        if (AudioService.Instance != null) AudioService.Instance.PlaySFX("SpecialAttack_Meteoros");
        FinalizarImpacto(attack, centro, frente, w.t);
        for (int i = 0; i < 3; i++)
        {
            PonerPose(actores[i], null);
            VolverAIdle(actores[i]);
            PonerPose(actores[i], PoseDeAterrizajeTrio(actores[i].t));
        }
        TimeScaleArbiterService.Request(this, 0.4f);
        yield return Co_Tramo(1.5f, t =>
        {
            for (int i = 0; i < 3; i++)
            {
                Colocar(actores[i], aterrizajes[i]);
                Orientar(actores[i], dirs[i]);
            }
        }, true);
    }

    private string PoseDeAterrizajeTrio(Transform t)
    {
        string nombre = t.name;
        if (nombre.IndexOf("Estela", StringComparison.OrdinalIgnoreCase) >= 0) return poseAterrizajeEstela;
        if (nombre.IndexOf("Liam", StringComparison.OrdinalIgnoreCase) >= 0) return poseAterrizajeLiam;
        return poseAterrizajeWill;
    }

    // ── Impacto ────────────────────────────────────────────────────────────

    private void FinalizarImpacto(SpecialAttackSO attack, Vector3 center, Vector3 facing, Transform body)
    {
        // Impacto final: Trío (cataclismo), Liam (supernova) o Estela (sísmico)
        GameObject impactoPrefab = attack.isTrio
            ? (impactoRemateTrio != null ? impactoRemateTrio : impactoRemateEstela)
            : (attack.IsDuoOf(Slot.Will, Slot.Liam)
                ? (impactoRemateLiam != null ? impactoRemateLiam : impactoRemateEstela)
                : impactoRemateEstela);

        if (impactoPrefab != null && VfxPoolService.Instance != null)
        {
            VfxPoolService.Instance.Play(
                impactoPrefab,
                center + Vector3.up * 0.1f,
                Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : body.forward),
                attack.isTrio ? 3.5f : 2.5f);
        }

        if (AudioService.Instance != null)
            AudioService.Instance.PlaySFX("SpecialAttack_Impacto");

        ApplyAoeDamage(attack, center);

        FeedbackService.HitStop(attack.isTrio ? 0.16f : 0.12f, 0.2f);
        FeedbackService.CameraShake(attack.isTrio ? 1.0f : 0.75f, attack.isTrio ? 0.65f : 0.5f);
        FeedbackService.Vibrar(1.0f, 0.7f, 0.4f);
    }

    // ── Actores ────────────────────────────────────────────────────────────

    private void PrepararActores(Transform body)
    {
        _actores.Clear();
        _absorbidos.Clear();
        _suelosMask = 1;
        AgregarActor(body);
        for (int i = 0; i < _partners.Count; i++) AgregarActor(_partners[i]);
    }

    private void AgregarActor(Transform t)
    {
        if (t == null) return;
        var a = new ActorDeEquipo { t = t, sueloY = t.position.y };
        a.anim = t.GetComponent<Animator>();
        if (a.anim == null) a.anim = t.GetComponentInChildren<Animator>();
        t.TryGetComponent(out a.npc);
        t.TryGetComponent(out a.motor);
        t.TryGetComponent(out a.rb);
        t.TryGetComponent(out a.agent);
        if (a.motor != null) _suelosMask = a.motor.groundLayer.value;
        a.agenteEncendido = a.agent != null && a.agent.enabled;
        if (a.agenteEncendido) a.agent.enabled = false;
        if (a.anim != null) { a.rootMotion = a.anim.applyRootMotion; a.anim.applyRootMotion = false; }
        _actores.Add(a);
    }

    private float SueloEn(Vector3 p, float porDefecto)
    {
        return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, _suelosMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y : porDefecto;
    }

    private static void Colocar(ActorDeEquipo a, Vector3 pos)
    {
        if (a.t == null) return;
        a.t.position = pos;
        if (a.rb != null && !a.rb.isKinematic) { a.rb.position = pos; a.rb.linearVelocity = Vector3.zero; }
        // En el aire, el motor del jugador no debe dejarle caer entre fotogramas.
        if (a.motor != null && pos.y > a.sueloY + 0.15f) a.motor.HoldAirborne(0.15f);
    }

    private static void Orientar(ActorDeEquipo a, Vector3 dir)
    {
        dir.y = 0f;
        if (a.t != null && dir.sqrMagnitude > 1e-4f)
        {
            a.t.rotation = Quaternion.LookRotation(dir);
            if (a.npc != null) a.npc.SyncTargetRotation();
        }
    }

    private static Vector3 DireccionPlana(Vector3 dir, Vector3 porDefecto)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) return dir.normalized;
        porDefecto.y = 0f;
        return porDefecto.sqrMagnitude > 0.01f ? porDefecto.normalized : Vector3.forward;
    }

    // ── Poses ──────────────────────────────────────────────────────────────

    /// Sostiene un estado (null = soltarlo). Si vive en la capa superior, los brazos hacen la pose y
    /// las piernas siguen la pose base; si es de cuerpo entero, la base queda libre para él.
    private void PonerPose(ActorDeEquipo a, string estado, bool congelarAlFinal = true)
    {
        if (a.t == null || a.anim == null) return;
        if (a.capaPose > 0) SoltarCapaSuperior(a);
        a.poseSuperior = null;
        a.capaPose = 0;
        if (a.npc != null) a.npc.ReleasePose(false);
        if (string.IsNullOrEmpty(estado)) return;

        int capa = AnimatorLayerUtil.ResolveLayer(a.anim, estado, 1);
        if (capa < 0)
        {
            Log($"El Animator de {a.t.name} no tiene el estado '{estado}'.");
            return;
        }
        a.poseSuperior = estado;
        a.capaPose = capa;
        if (capa == 0)
        {
            a.poseBase = null;
            if (a.anim.HasState(0, Animator.StringToHash(estado)))
                a.anim.CrossFadeInFixedTime(estado, 0.12f, 0);
        }
        else if (a.motor != null)
        {
            a.motor.SostenerCapaSuperior();
            if (a.anim.HasState(capa, Animator.StringToHash(estado)))
                a.anim.CrossFadeInFixedTime(estado, 0.12f, capa);
        }
        if (a.npc != null) a.npc.HoldPose(estado, capa, congelarAlFinal);
    }

    private static void SoltarCapaSuperior(ActorDeEquipo a)
    {
        if (a.motor != null) { a.motor.ReleaseUpperBodyPose(); return; }
        // Un compañero en modo batalla usa esa capa para su postura de combate.
        if (a.anim != null && (a.npc == null || !a.npc.IsInBattleMode))
            a.anim.SetLayerWeight(a.capaPose, 0f);
    }

    /// Pose de cuerpo para las piernas (carrera, en el aire). null = sin pose base forzada.
    private static void PonerBase(ActorDeEquipo a, string estado)
    {
        if (a.t == null) return;
        a.poseBase = string.IsNullOrEmpty(estado) ? null : estado;
        a.hashBase = a.poseBase != null ? Animator.StringToHash(a.poseBase) : 0;
        if (a.anim != null && a.poseBase != null && a.anim.HasState(0, a.hashBase))
            a.anim.CrossFadeInFixedTime(a.hashBase, 0.12f, 0);
    }

    /// Suelta la pose base y devuelve a los personajes a su postura normal en el suelo.
    private static void VolverAIdle(ActorDeEquipo a)
    {
        PonerBase(a, null);
        if (a.npc != null) a.npc.TransitionToIdle();
        if (a.motor != null) RestaurarLocomocionJugador(a);
    }

    private static void RestaurarLocomocionJugador(ActorDeEquipo a)
    {
        if (a == null || a.t == null) return;

        if (a.motor != null)
        {
            a.motor.CancelAirHold();
            a.motor.ReleaseAirbornePose();
        }

        if (a.t.TryGetComponent(out PlayerDialogueAnimator pda))
        {
            pda.ReturnToLocomotion(0.15f);
        }
        else if (a.anim != null)
        {
            int locHash = Animator.StringToHash("Free Locomotion");
            if (a.anim.HasState(0, locHash))
                a.anim.CrossFadeInFixedTime(locHash, 0.15f, 0);
            else if (a.anim.HasState(0, Animator.StringToHash("Idle")))
                a.anim.CrossFadeInFixedTime("Idle", 0.15f, 0);
        }

        if (a.anim != null)
        {
            a.anim.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, true);
            a.anim.SetFloat(Invector.vCharacterController.vAnimatorParameters.GroundDistance, 0f);
        }
    }

    /// Mientras dura el ataque, fuerza cada fotograma lo que otros sistemas deshacen: el peso de la
    /// capa de la pose y la pose base de las piernas.
    private IEnumerator Co_MantenerPoses()
    {
        while (true)
        {
            for (int i = 0; i < _actores.Count; i++)
            {
                var a = _actores[i];
                if (a.t == null || a.anim == null) continue;
                if (a.capaPose > 0 && !string.IsNullOrEmpty(a.poseSuperior))
                {
                    a.anim.SetLayerWeight(a.capaPose, 1f);
                    // Si el jugador está ejecutando la pose en la capa superior y el clip terminó sin loop,
                    // congelarlo en el último fotograma para que no reinicie ni flexione
                    if (a.motor != null && a.anim.layerCount > a.capaPose)
                    {
                        var upperInfo = a.anim.GetCurrentAnimatorStateInfo(a.capaPose);
                        if (upperInfo.IsName(a.poseSuperior) && !upperInfo.loop && upperInfo.normalizedTime >= 1f)
                        {
                            a.anim.Play(Animator.StringToHash(a.poseSuperior), a.capaPose, 1f);
                        }
                    }
                }
                if (a.poseBase == null) continue;
                var info = a.anim.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash != a.hashBase && info.fullPathHash != a.hashBase && !a.anim.IsInTransition(0))
                    a.anim.CrossFadeInFixedTime(a.hashBase, 0.12f, 0);
            }
            yield return null;
        }
    }

    private void PonerModoCinematico(bool poner)
    {
        if (poner == _modoCinematico) return;
        if (_accion == null) _accion = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        if (_accion == null) return;
        if (poner) _accion.PushMode(ActionMode.Cinematic); else _accion.PopMode(ActionMode.Cinematic);
        _modoCinematico = poner;
    }

    private void PonerEmociones(NPCEmotion emocion)
    {
        for (int i = 0; i < _actores.Count; i++)
        {
            if (_actores[i].t != null)
            {
                var emo = _actores[i].t.GetComponent<NPCEmotionController>();
                if (emo == null) emo = _actores[i].t.GetComponentInChildren<NPCEmotionController>();
                if (emo != null) emo.SetEmotion(emocion);
            }
        }
    }

    private static void CongelarAnimador(ActorDeEquipo a, bool congelar)
    {
        if (a.anim != null)
            a.anim.speed = congelar ? 0f : 1f;
    }

    // ── Puesta en escena ───────────────────────────────────────────────────

    private void MostrarCaras(float duracion, bool trio)
    {
        var cutIn = CutInUI.Obtener(transform);
        for (int i = 0; i < _actores.Count && i < CutInUI.Ranuras; i++)
        {
            var actor = ResolveActor(_actores[i].t, i == 0);
            if (actor == null) continue;
            float altura = trio ? (i == 0 ? 0.78f : i == 1 ? 0.5f : 0.22f) : (i == 0 ? 0.65f : 0.35f);
            if (!trio && i > 1) break;
            cutIn.Mostrar(actor, EncuadreCutIn.Cara, i % 2 == 0 ? LadoCutIn.Izquierda : LadoCutIn.Derecha, altura, duracion, i);
        }
    }

    /// La cámara gira alrededor del centro en tiempo real y se devuelve al juego al acabar.
    private void ArrancarOrbita(Vector3 centro, float duracion, float grados, float alturaDeMirada, float alturaMinima, float radioFijo = -1f)
    {
        if (_orbitCameraRoutine != null)
        {
            StopCoroutine(_orbitCameraRoutine);
            _orbitCameraRoutine = null;
            CameraDirectorService.Release(this);
        }
        _orbitCameraRoutine = StartCoroutine(Co_OrbitarCamara(centro, duracion, grados, alturaDeMirada, alturaMinima, radioFijo));
    }

    private IEnumerator Co_OrbitarCamara(Vector3 centro, float duracion, float grados, float alturaDeMirada, float alturaMinima, float radioFijo)
    {
        var cam = Camera.main;
        if (cam == null) { _orbitCameraRoutine = null; yield break; }

        CameraDirectorService.Claim(this);
        Vector3 inicio = cam.transform.position - centro;
        float radio = radioFijo > 0f ? radioFijo : Mathf.Clamp(new Vector2(inicio.x, inicio.z).magnitude, 4f, 7f);
        float altura = Mathf.Max(alturaMinima, Mathf.Min(inicio.y, 3.5f));
        float anguloInicial = Mathf.Atan2(inicio.x, inicio.z) * Mathf.Rad2Deg;
        Vector3 mira = centro + Vector3.up * alturaDeMirada;

        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float angulo = (anguloInicial + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracion)) * grados) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(centro.x + Mathf.Sin(angulo) * radio, centro.y + altura, centro.z + Mathf.Cos(angulo) * radio);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira - pos));
            yield return null;
        }

        _orbitCameraRoutine = null;
        CameraDirectorService.Release(this);
    }

    private static IEnumerator Co_Tramo(float duracion, Action<float> paso, bool sinEscala)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += sinEscala ? Time.unscaledDeltaTime : Time.deltaTime;
            paso(Mathf.Clamp01(t / duracion));
            yield return null;
        }
        paso(1f);
    }

    private void ActivarAurasDeEquipo(SpecialAttackSO attack)
    {
        LimpiarAurasDeAccion();
        for (int i = 0; i < _actores.Count; i++)
        {
            Transform t = _actores[i].t;
            if (t == null) continue;
            bool esEstela = t.name.IndexOf("Estela", StringComparison.OrdinalIgnoreCase) >= 0;
            bool esLiam = t.name.IndexOf("Liam", StringComparison.OrdinalIgnoreCase) >= 0;

            if (esEstela)
            {
                if (auraLevitacionEstela != null)
                    _actionAuras.Add(Instantiate(auraLevitacionEstela, t.position, t.rotation, t));

                if (focoManoEstela != null)
                {
                    Transform mano = null;
                    if (_actores[i].anim != null && _actores[i].anim.isHuman)
                        mano = _actores[i].anim.GetBoneTransform(HumanBodyBones.RightHand);
                    if (mano == null) mano = t;
                    _actionAuras.Add(Instantiate(focoManoEstela, mano.position, mano.rotation, mano));
                }
            }
            else
            {
                var prefab = esLiam && auraConcentracionLiam != null ? auraConcentracionLiam : auraConcentracionWill;
                if (prefab != null)
                    _actionAuras.Add(Instantiate(prefab, t.position, t.rotation, t));
            }
        }
    }

    private void LimpiarAurasDeAccion()
    {
        for (int i = 0; i < _actionAuras.Count; i++)
            if (_actionAuras[i] != null) Destroy(_actionAuras[i]);
        _actionAuras.Clear();
    }

    // ── Cierre ─────────────────────────────────────────────────────────────

    /// Devuelve todo a como estaba: tiempo, UI, cámara, poses y control. Cada personaje vuelve al
    /// suelo (no puede quedar suspendido) y su NavMeshAgent se recoloca donde está.
    private void LimpiarPuestaEnEscena()
    {
        TimeScaleArbiterService.Release(this);
        CutInUI.Instance?.Retirar(-1);
        BandasDeCineUI.Instance?.Mostrar(false, 0f, 0.25f);
        LineasDeConcentracionUI.Instance?.Mostrar(false, Color.white, 0.35f, 0.15f, 0f);

        if (_orbitCameraRoutine != null)
        {
            StopCoroutine(_orbitCameraRoutine);
            _orbitCameraRoutine = null;
            CameraDirectorService.Release(this);
        }
        if (_mantenerPosesRoutine != null)
        {
            StopCoroutine(_mantenerPosesRoutine);
            _mantenerPosesRoutine = null;
        }

        LimpiarAurasDeAccion();
        for (int i = 0; i < _actores.Count; i++) SoltarActor(_actores[i]);
        _actores.Clear();
        _absorbidos.Clear();
        PonerModoCinematico(false);
    }

    private void SoltarActor(ActorDeEquipo a)
    {
        if (a.t == null) return;

        a.poseBase = null;
        if (a.capaPose > 0) SoltarCapaSuperior(a);
        a.poseSuperior = null;
        a.capaPose = 0;
        if (a.anim != null) a.anim.speed = 1f;

        var emo = a.t.GetComponent<NPCEmotionController>();
        if (emo == null) emo = a.t.GetComponentInChildren<NPCEmotionController>();
        if (emo != null) emo.SetEmotion(NPCEmotion.Neutral);

        if (a.npc != null)
        {
            a.npc.ReleasePose(true);
            a.npc.TransitionToIdle();
        }
        if (a.motor != null)
        {
            a.motor.CancelAirHold();
            a.motor.CancelActionCommit();
            a.motor.ReleaseAirbornePose();
            RestaurarLocomocionJugador(a);
        }

        Vector3 p = a.t.position;
        a.sueloY = SueloEn(p, a.sueloY);
        var motorRef = a.motor;
        a.motor = null; // ya en el suelo: Colocar no debe sostenerle en el aire
        Colocar(a, new Vector3(p.x, a.sueloY, p.z));
        a.motor = motorRef;

        if (a.npc != null) a.npc.SyncTargetRotation();
        if (a.anim != null) a.anim.applyRootMotion = a.rootMotion;
        if (a.agent != null)
        {
            a.agent.enabled = a.agenteEncendido;
            if (a.agenteEncendido && a.agent.isOnNavMesh)
            {
                a.agent.Warp(a.t.position);
                a.agent.updateRotation = true;
            }
        }
    }
}
