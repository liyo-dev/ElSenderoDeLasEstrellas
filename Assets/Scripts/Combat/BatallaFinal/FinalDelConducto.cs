using System.Collections;
using Core;
using Game.NPC;
using Sendero.Core.Feedback;
using Sendero.UI;
using UnityEngine;

/// El final de la batalla contra el Mago Oscuro, en el orden de la novela («Tiempo 3»; GDD § 19,
/// fila 7): el Mago parece vencido y ataca a traición; Liam se interpone; Will busca el Hechizo
/// del Tiempo y el pozo está seco; Estela le ofrece su energía (consentimiento explícito:
/// «Todo lo que me queda. Te lo doy yo»); los dos juntos lanzan Corazón Estelar como una aguja de
/// luz que corta el conducto; el Mago, sin vínculo, se deshace.
///
/// Nada se puede fallar (GDD: sin QTE que se falle): cada acción del jugador es una confirmación
/// deliberada y, si no llega, la escena sigue sola al rato. La muerte de Liam ocurre aquí y solo
/// aquí, nunca por daño. Al terminar levanta 'senalAlTerminar' (el deseo y el derrumbe vienen
/// después). Ver INC-509.
public sealed class FinalDelConducto : MonoBehaviour
{
    [Header("Personajes")]
    [SerializeField] private string nombreLiam = "Liam";
    [SerializeField] private string nombreEstela = "Estela";

    [Header("Efectos")]
    [Tooltip("La lanza de sombra de la traición (del Mago a Liam).")]
    [SerializeField] private LineRenderer rayoTraicion;
    [Tooltip("La energía que Estela le da a Will.")]
    [SerializeField] private LineRenderer rayoEnergia;
    [Tooltip("La aguja de luz de Corazón Estelar (de Will a la unión del conducto).")]
    [SerializeField] private LineRenderer rayoAguja;
    [SerializeField] private GameObject vfxSalto;
    [SerializeField] private GameObject vfxImpactoLanza;
    [SerializeField] private GameObject vfxCorte;
    [SerializeField] private GameObject vfxDeshacerse;

    [Header("Ritmo")]
    [SerializeField] private float segundosDeEnergia = 2.5f;
    [Tooltip("Si el jugador no confirma, la escena sigue sola pasado este tiempo.")]
    [SerializeField] private float esperaMaxima = 8f;

    [Header("Salida")]
    [NarrativeKey(NarrativeKeyKind.Signal, Rol = SignalRole.Emite)]
    [SerializeField] private string senalAlTerminar = "SENDERO_SACRIFICIO_START";

    private PlayerActionManager _accion;
    private bool _cinematica;
    private Coroutine _planoActivo;
    private float _fovPrevio = 60f;

    private static readonly Color ColorWill = new Color(1f, 0.82f, 0.48f);    // #FFD27A
    private static readonly Color ColorEstela = new Color(1f, 0.62f, 0.72f);  // #FF9FB8
    private static readonly Color ColorLiam = new Color(0.54f, 0.84f, 1f);    // #8AD6FF
    private static readonly Color ColorMago = new Color(0.71f, 0.55f, 1f);    // #B48CFF

    public IEnumerator Ejecutar(MagoOscuroBossAI mago)
    {
        Transform will = PlayerService.Player != null ? PlayerService.Player.transform : null;
        Transform liam = Miembro(nombreLiam);
        Transform estela = Miembro(nombreEstela);
        Transform union = null;
        Apagar(rayoTraicion); Apagar(rayoEnergia); Apagar(rayoAguja);

        // 1. Parece vencido: arrancamos la puesta en escena cinemática desde que cae de rodillas
        PonerCinematica();
        mago.Arrodillarse();

        if (will != null)
        {
            Vector3 dirMago = Plano(mago.transform.position - will.position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dirMago).normalized;
            will.position = mago.transform.position - dirMago * 6.2f;
            will.rotation = Quaternion.LookRotation(dirMago, Vector3.up);
            if (estela != null)
            {
                WarpActor(estela, will.position + right * 1.45f - dirMago * 0.35f);
                estela.rotation = Quaternion.LookRotation(dirMago, Vector3.up);
            }
            if (liam != null)
            {
                WarpActor(liam, will.position - right * 1.45f - dirMago * 0.35f);
                liam.rotation = Quaternion.LookRotation(dirMago, Vector3.up);
            }
        }

        // Plano medio corto del Mago Oscuro de rodillas (lente 26° -> 24°)
        PlanoTresCuartos(mago.transform, will, lado: -1f, distancia: 2.35f, lenteIni: 26f, lenteFin: 24f,
            alturaOffset: -0.22f, encuadreX: 0.08f, duracion: 3.4f, mano: 0.14f);
        yield return DecirSubtitulo(mago.transform, "FINAL_MAGO_VENCIDO", "Mago Oscuro", ColorMago, 2.8f);

        if (estela != null)
        {
            PlanoTresCuartos(estela, mago.transform, lado: -1f, distancia: 2.15f, lenteIni: 25f, lenteFin: 23.5f,
                alturaOffset: -0.04f, encuadreX: 0.09f, duracion: 2.8f, mano: 0.12f);
            yield return DecirSubtitulo(estela, "FINAL_ESTELA_ACABADO", nombreEstela, ColorEstela, 2.2f);
        }

        // 2. La traición. Primerísimo plano del Mago Oscuro (22° -> 18°) y corte de impacto con Liam
        PlanoTresCuartos(mago.transform, will, lado: 1f, distancia: 1.95f, lenteIni: 22f, lenteFin: 18.5f,
            alturaOffset: -0.28f, encuadreX: -0.06f, duracion: 2.0f, mano: 0.25f);
        mago.Gesto("MagicSpecial");
        var seqMago = SequenceActor.ForTransform("NPC_MagoOscuro", mago.transform, MedirAlturaOjos(mago.transform));
        if (seqMago != null)
        {
            CutInUI.Obtener(transform).Mostrar(seqMago, EncuadreCutIn.Ojos, LadoCutIn.Derecha, 0.52f, 1.3f, ranura: 0);
            AudioService.Instance?.PlaySFX("SFX_Prologo_CutIn");
        }
        yield return DecirSubtitulo(mago.transform, "FINAL_MAGO_TRAICION", "Mago Oscuro", ColorMago, 1.5f);
        CutInUI.Instance?.Retirar();

        Transform blanco = will;
        if (liam != null && will != null)
        {
            Vector3 entre = Vector3.Lerp(will.position, mago.transform.position, 0.28f);
            Salto(liam.position);
            WarpActor(liam, entre);
            liam.rotation = Quaternion.LookRotation(Plano(mago.transform.position - entre));
            Salto(entre);
            blanco = liam;

            var seqLiam = SequenceActor.ForTransform("NPC_Liam", liam, MedirAlturaOjos(liam));
            if (seqLiam != null)
            {
                CutInUI.Obtener(transform).Mostrar(seqLiam, EncuadreCutIn.Cara, LadoCutIn.Izquierda, 0.48f, 0.9f, ranura: 1);
                AudioService.Instance?.PlaySFX("SFX_Prologo_CutIn");
            }
        }

        // Plano dramático lateral-frontal del impacto de la lanza sobre Liam (lente 28° -> 24°)
        if (blanco != null)
        {
            PlanoTresCuartos(blanco, mago.transform, lado: 1f, distancia: 2.45f, lenteIni: 28f, lenteFin: 23f,
                alturaOffset: -0.10f, encuadreX: -0.06f, duracion: 1.8f, mano: 0.35f);
        }

        LineasDeConcentracionUI.Obtener(transform).Mostrar(
            true, new Color(0.75f, 0.35f, 1f, 0.55f), 0.42f, 0.08f, 1.1f);
        yield return Rayo(rayoTraicion, mago.transform.position + Vector3.up * 1.35f, blanco != null ? blanco.position + Vector3.up * 1.05f : mago.transform.position, 0.55f);
        CutInUI.Instance?.Retirar();
        FeedbackService.HitStop(0.12f, 0.5f);
        FeedbackService.CameraShake(0.85f, 0.6f);
        FeedbackService.ScreenFlash(new Color(0.35f, 0f, 0.48f, 0.55f), 0.4f);
        if (vfxImpactoLanza && blanco && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxImpactoLanza, blanco.position + Vector3.up, Quaternion.identity, 2f);

        if (liam != null)
        {
            var miembro = liam.GetComponent<NPCPartyMember>();
            if (miembro != null && miembro.IsInParty) miembro.LeaveParty();
            var anim = liam.GetComponentInChildren<NPCSimpleAnimator>();
            if (anim) anim.HoldPose("Die01Stay_NoWeapon");

            if (estela != null)
            {
                PlanoTresCuartos(estela, liam, lado: -1f, distancia: 2.0f, lenteIni: 24f, lenteFin: 21.5f,
                    alturaOffset: -0.04f, encuadreX: 0.08f, duracion: 2.2f, mano: 0.22f);
                yield return DecirSubtitulo(estela, "FINAL_ESTELA_LIAM", nombreEstela, ColorEstela, 1.6f);
            }

            // Primer plano íntimo de Liam caído para sus últimas palabras (lente 23° -> 20°)
            Vector3 focoLiam = liam.position + Vector3.up * 0.48f;
            Vector3 dirVista = Plano(will != null ? will.position - liam.position : -liam.forward).normalized;
            Vector3 posCamLiam = focoLiam + (dirVista * 1.65f + Vector3.Cross(Vector3.up, dirVista) * 0.65f) + Vector3.up * 0.42f;
            Quaternion rotCamLiam = RotacionConComposicion(posCamLiam, focoLiam, 23f, new Vector2(0f, -0.04f));
            RodarPlano(posCamLiam, rotCamLiam, 23f, posCamLiam - dirVista * 0.15f, rotCamLiam, 20.5f, 4.6f, mano: 0.12f);

            yield return new WaitForSeconds(0.8f);
            yield return DecirSubtitulo(liam, "LIAM_LAST_WORDS", nombreLiam, ColorLiam, 3.4f);
        }

        // 3. El pozo está seco: primer plano angustiado de Will intentando el Hechizo del Tiempo (24° -> 21°)
        if (will != null)
        {
            PlanoTresCuartos(will, mago.transform, lado: -1f, distancia: 2.0f, lenteIni: 24f, lenteFin: 21f,
                alturaOffset: -0.04f, encuadreX: 0.07f, duracion: esperaMaxima + 3f, mano: 0.20f);
        }
        Bocadillos.Decir(will, "FINAL_WILL_INTENTA_TIEMPO", "Will", esperaMaxima, fijo: true);
        yield return EsperarConfirmacion();
        FeedbackService.ScreenFlash(new Color(0.4f, 0.5f, 0.7f, 0.30f), 0.3f);
        yield return DecirSubtitulo(will, "FINAL_WILL_POZO_SECO", "Will", ColorWill, 2.6f);

        // 4. El conducto. Estela se coloca junto a Will: contraplanos sobre el hombro y primeros planos (24°)
        if (estela && will)
        {
            Salto(estela.position);
            Vector3 alLado = will.position + will.right * 1.25f;
            WarpActor(estela, alLado);
            estela.rotation = Quaternion.LookRotation(Plano(will.position - alLado));
            Salto(alLado);
        }
        if (estela != null)
        {
            PlanoSobreElHombro(will, estela, ladoHombro: -1f, lenteIni: 24f, lenteFin: 22.5f, duracion: 3.0f, mano: 0.12f);
            yield return DecirSubtitulo(estela, "FINAL_ESTELA_CONDUCTO", nombreEstela, ColorEstela, 2.4f);
        }
        if (will != null)
        {
            PlanoSobreElHombro(estela, will, ladoHombro: 1f, lenteIni: 24f, lenteFin: 22.5f, duracion: 2.6f, mano: 0.12f);
            yield return DecirSubtitulo(will, "FINAL_WILL_CUANTO", "Will", ColorWill, 2.0f);
        }
        if (estela != null)
        {
            PlanoTresCuartos(estela, will, lado: -1f, distancia: 1.95f, lenteIni: 23f, lenteFin: 20f,
                alturaOffset: -0.04f, encuadreX: 0.07f, duracion: 3.6f, mano: 0.10f);
            yield return DecirSubtitulo(estela, "FINAL_ESTELA_TODO", nombreEstela, ColorEstela, 3.0f);
        }

        // 5. Corazón Estelar compartido: plano dos heroico de Will y Estela (28° -> 23°) mientras fluye la energía
        if (estela != null && mago != null) estela.rotation = Quaternion.LookRotation(Plano(mago.transform.position - estela.position));
        if (will != null && mago != null) will.rotation = Quaternion.LookRotation(Plano(mago.transform.position - will.position));

        if (will != null && estela != null)
        {
            Vector3 centro = (will.position + estela.position) * 0.5f + Vector3.up * 0.95f;
            Vector3 dirFrente = Plano(mago.transform.position - centro).normalized;
            Vector3 dirCam = Quaternion.AngleAxis(-24f, Vector3.up) * dirFrente;
            Vector3 pIni = centro + dirCam * 2.85f + Vector3.down * 0.25f;
            Vector3 pFin = centro + dirCam * 2.35f + Vector3.down * 0.28f;
            Quaternion rIni = RotacionConComposicion(pIni, centro, 28f, new Vector2(0f, -0.06f));
            Quaternion rFin = RotacionConComposicion(pFin, centro, 23f, new Vector2(0f, -0.06f));
            RodarPlano(pIni, rIni, 28f, pFin, rFin, 23f, segundosDeEnergia + esperaMaxima, mano: 0.22f);
        }

        LineasDeConcentracionUI.Obtener(transform).Mostrar(
            true, new Color(1f, 0.88f, 0.94f, 0.48f), 0.45f, 0.2f, segundosDeEnergia + esperaMaxima);
        Bocadillos.Decir(will, "FINAL_PULSA_GATILLOS", "Will", esperaMaxima + segundosDeEnergia, fijo: true);
        float cargado = 0f;
        float limite = Time.time + esperaMaxima + segundosDeEnergia;
        while (cargado < segundosDeEnergia)
        {
            bool juntos = GamepadInputReader.LeftTriggerHeld && GamepadInputReader.RightTriggerHeld;
            if (juntos || Time.time > limite) cargado += Time.deltaTime;
            if (rayoEnergia && estela && will)
            {
                rayoEnergia.enabled = juntos || Time.time > limite;
                rayoEnergia.SetPosition(0, estela.position + Vector3.up * 1.05f);
                rayoEnergia.SetPosition(1, will.position + Vector3.up * 1.05f);
                rayoEnergia.widthMultiplier = Mathf.Lerp(0.05f, 0.35f, cargado / segundosDeEnergia);
            }
            yield return null;
        }
        Apagar(rayoEnergia);

        // 6. La aguja de luz: franjas Cut-In de Will y Estela + plano sobre el hombro hacia el Mago Oscuro
        var cutIn = CutInUI.Obtener(transform);
        if (estela != null)
            cutIn.Mostrar(SequenceActor.ForTransform("NPC_Estela", estela, MedirAlturaOjos(estela)), EncuadreCutIn.Cara, LadoCutIn.Izquierda, 0.66f, 1.4f, ranura: 0);
        if (will != null)
            cutIn.Mostrar(SequenceActor.ForTransform("Player", will, MedirAlturaOjos(will)), EncuadreCutIn.Cara, LadoCutIn.Derecha, 0.34f, 1.4f, ranura: 1);
        AudioService.Instance?.PlaySFX("SFX_Prologo_CutIn");

        PlanoSobreElHombro(will, mago.transform, ladoHombro: -1f, lenteIni: 26f, lenteFin: 22f, duracion: esperaMaxima + 2f, mano: 0.20f);
        Bocadillos.Decir(will, "FINAL_PULSA_AGUJA", "Will", esperaMaxima, fijo: true);
        yield return EsperarConfirmacion();
        cutIn.Retirar();

        Vector3 desde = will != null ? will.position + Vector3.up * 1.15f : mago.transform.position;
        Vector3 hacia = (union != null ? union.position : mago.transform.position) + Vector3.up * 1.1f;
        if (rayoAguja) rayoAguja.widthMultiplier = 0.06f;
        yield return Rayo(rayoAguja, desde, hacia, 0.35f);
        LineasDeConcentracionUI.Instance?.Mostrar(false, Color.white, 0.45f, 0.15f, 0f);
        FeedbackService.HitStop(0.08f, 0.6f);
        FeedbackService.ScreenFlash(new Color(1f, 1f, 0.95f, 0.85f), 0.5f);
        FeedbackService.CameraShake(1f, 0.65f);
        if (vfxCorte && VfxPoolService.Instance != null) VfxPoolService.Instance.Play(vfxCorte, hacia, Quaternion.identity, 3f);

        // 7. Sin vínculo, el Mago se deshace: primer plano contrapicado (24° -> 28°)
        PlanoTresCuartos(mago.transform, will, lado: -1f, distancia: 2.25f, lenteIni: 24f, lenteFin: 28f,
            alturaOffset: -0.25f, encuadreX: 0f, duracion: 4.0f, mano: 0.18f);
        yield return DecirSubtitulo(mago.transform, "FINAL_MAGO_DESHACE", "Mago Oscuro", ColorMago, 2.2f);
        yield return mago.Deshacerse(vfxDeshacerse);

        SoltarCinematica();
        yield return new WaitForSeconds(1.2f);
        if (!string.IsNullOrEmpty(senalAlTerminar))
            DefaultNarrativeSignals.Instance?.RaiseCustom(senalAlTerminar, name);
    }

    void OnDisable() => SoltarCinematica();

    // ── Cámaras y Subtítulos de Cine ──────────────────────────────────────

    private IEnumerator DecirSubtitulo(Transform quien, string key, string nombre, Color colorNombre, float duracionBase)
    {
        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo == null || string.IsNullOrEmpty(key)) yield break;

        string texto = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, key) : key;
        float duracion = Mathf.Max(duracionBase, bocadillo.TiempoDeLectura(texto));
        float voz = VoiceLines.TryPlay(key);
        if (voz > 0f) duracion = Mathf.Max(duracion, voz + 0.3f);

        bool listo = false;
        bocadillo.Show(
            quien,
            texto,
            duration: duracion,
            onComplete: () => listo = true,
            speakerName: nombre,
            presentacion: PresentacionDeTexto.Subtitulo,
            colorDelNombre: colorNombre
        );
        yield return new WaitUntil(() => listo);
    }

    private void PlanoTresCuartos(
        Transform sujeto, Transform referencia, float lado, float distancia,
        float lenteIni, float lenteFin, float alturaOffset, float encuadreX,
        float duracion, float mano)
    {
        if (sujeto == null) return;
        float ojos = MedirAlturaOjos(sujeto);
        Vector3 foco = sujeto.position + Vector3.up * ojos;
        Vector3 frente = referencia != null ? Plano(referencia.position - sujeto.position).normalized : sujeto.forward;
        Vector3 dirCam = Quaternion.AngleAxis(lado * 30f, Vector3.up) * frente;
        Vector3 posIni = foco + dirCam * distancia + Vector3.up * alturaOffset;
        Vector3 posFin = foco + dirCam * (distancia * 0.93f) + Vector3.up * alturaOffset;
        Vector2 comp = new Vector2(encuadreX, -0.07f);
        RodarPlano(
            posIni, RotacionConComposicion(posIni, foco, lenteIni, comp), lenteIni,
            posFin, RotacionConComposicion(posFin, foco, lenteFin, comp), lenteFin,
            duracion, mano);
    }

    private void PlanoSobreElHombro(
        Transform hombro, Transform sujeto, float ladoHombro,
        float lenteIni, float lenteFin, float duracion, float mano)
    {
        if (sujeto == null) return;
        if (hombro == null)
        {
            PlanoTresCuartos(sujeto, null, ladoHombro, 2.15f, lenteIni, lenteFin, -0.04f, 0f, duracion, mano);
            return;
        }
        Vector3 haciaSujeto = Plano(sujeto.position - hombro.position).normalized;
        Vector3 derecha = Vector3.Cross(Vector3.up, haciaSujeto).normalized;
        float ojosH = MedirAlturaOjos(hombro);
        float ojosS = MedirAlturaOjos(sujeto);
        Vector3 foco = sujeto.position + Vector3.up * ojosS;
        Vector3 posIni = hombro.position - haciaSujeto * 1.25f + derecha * (ladoHombro * 0.44f) + Vector3.up * (ojosH + 0.04f);
        Vector3 posFin = posIni + haciaSujeto * 0.16f;
        Vector2 comp = new Vector2(-ladoHombro * 0.10f, -0.06f);
        RodarPlano(
            posIni, RotacionConComposicion(posIni, foco, lenteIni, comp), lenteIni,
            posFin, RotacionConComposicion(posFin, foco, lenteFin, comp), lenteFin,
            duracion, mano);
    }

    private static Quaternion RotacionConComposicion(Vector3 camPos, Vector3 foco, float fov, Vector2 encuadre)
    {
        Vector3 d = foco - camPos;
        if (d.sqrMagnitude < 1e-4f) return Quaternion.identity;
        float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * (16f / 9f);
        return Quaternion.LookRotation(d.normalized, Vector3.up)
               * Quaternion.Euler(
                   -Mathf.Atan(2f * encuadre.y * tanV) * Mathf.Rad2Deg,
                   -Mathf.Atan(2f * encuadre.x * tanH) * Mathf.Rad2Deg,
                   0f);
    }

    private void RodarPlano(
        Vector3 posIni, Quaternion rotIni, float fovIni,
        Vector3 posFin, Quaternion rotFin, float fovFin,
        float duracion, float mano)
    {
        if (_planoActivo != null) StopCoroutine(_planoActivo);
        _planoActivo = StartCoroutine(Co_RodarPlano(posIni, rotIni, fovIni, posFin, rotFin, fovFin, duracion, mano));
    }

    private IEnumerator Co_RodarPlano(
        Vector3 posIni, Quaternion rotIni, float fovIni,
        Vector3 posFin, Quaternion rotFin, float fovFin,
        float duracion, float mano)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        float t = 0f;
        float dur = Mathf.Max(0.05f, duracion);
        float semilla = Random.Range(0f, 100f);

        while (true)
        {
            if (cam == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            Vector3 pos = Vector3.Lerp(posIni, posFin, k);
            Quaternion rot = Quaternion.Slerp(rotIni, rotFin, k);
            float fov = Mathf.Lerp(fovIni, fovFin, k);

            if (mano > 0.001f)
            {
                float tr = Time.unscaledTime * 0.85f + semilla;
                float rx = (Mathf.PerlinNoise(tr, 1.3f) - 0.5f) * 2f * mano * 0.65f;
                float ry = (Mathf.PerlinNoise(2.7f, tr) - 0.5f) * 2f * mano * 0.65f;
                rot *= Quaternion.Euler(rx, ry, 0f);
            }

            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            yield return null;
        }
    }

    private static float MedirAlturaOjos(Transform actor)
    {
        if (actor == null) return 1.0f;
        var seq = SequenceActor.ForTransform(actor.name, actor);
        float h = seq != null ? seq.AlturaDeOjos : 1.0f;
        return Mathf.Clamp(h, 0.78f, 1.45f);
    }

    private static void WarpActor(Transform actor, Vector3 destino)
    {
        if (actor == null) return;
        var agente = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(destino);
        else actor.position = destino;
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    private static Transform Miembro(string nombre)
    {
        if (!PlayerParty.HasInstance) return null;
        var m = PlayerParty.Instance.GetMemberByName(nombre);
        return m != null ? m.transform : null;
    }

    private IEnumerator EsperarConfirmacion()
    {
        float limite = Time.time + esperaMaxima;
        yield return null;
        while (Time.time < limite && !GamepadInputReader.SubmitPressed) yield return null;
    }

    private IEnumerator Rayo(LineRenderer rayo, Vector3 desde, Vector3 hacia, float segundos)
    {
        if (rayo == null) { yield return new WaitForSeconds(segundos); yield break; }
        rayo.enabled = true;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            rayo.SetPosition(0, desde);
            rayo.SetPosition(1, Vector3.Lerp(desde, hacia, Mathf.Clamp01(t / (segundos * 0.4f))));
            yield return null;
        }
        rayo.enabled = false;
    }

    private void Salto(Vector3 donde)
    {
        if (vfxSalto && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxSalto, donde + Vector3.up, Quaternion.identity, 1.5f);
    }

    private static void Apagar(LineRenderer r) { if (r) r.enabled = false; }

    private static Vector3 Plano(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.001f ? v : Vector3.forward; }

    private void PonerCinematica()
    {
        if (_cinematica) return;
        if (_accion == null) _accion = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        if (_accion != null) _accion.PushMode(ActionMode.Cinematic);
        _cinematica = true;

        var cam = Camera.main;
        if (cam != null) _fovPrevio = cam.fieldOfView;
        CameraDirectorService.Claim(this);
        BandasDeCineUI.Obtener(transform).Mostrar(true, 0.12f, 0.35f);
        PlayerHUDV2.Instance?.HideHUD(this);
        MinimapController.Ocultar(this);
    }

    private void SoltarCinematica()
    {
        if (!_cinematica) return;
        _cinematica = false;

        if (_planoActivo != null)
        {
            StopCoroutine(_planoActivo);
            _planoActivo = null;
        }

        BandasDeCineUI.Instance?.Mostrar(false, 0f, 0.35f);
        LineasDeConcentracionUI.Instance?.OcultarInmediato();
        CutInUI.Instance?.Retirar();
        PlayerHUDV2.Instance?.ShowHUD(this);
        MinimapController.Mostrar(this);

        var cam = Camera.main;
        if (cam != null) cam.fieldOfView = _fovPrevio;
        CameraDirectorService.Release(this);

        if (_accion != null) _accion.PopMode(ActionMode.Cinematic);
    }
}
