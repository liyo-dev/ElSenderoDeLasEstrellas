using System.Collections;
using Core;
using Game.NPC;
using Sendero.Core.Feedback;
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

    public IEnumerator Ejecutar(MagoOscuroBossAI mago)
    {
        Transform will = PlayerService.Player != null ? PlayerService.Player.transform : null;
        Transform liam = Miembro(nombreLiam);
        Transform estela = Miembro(nombreEstela);
        Transform union = null;   // el disparo final va al propio Mago
        Apagar(rayoTraicion); Apagar(rayoEnergia); Apagar(rayoAguja);

        // 1. Parece vencido.
        mago.Arrodillarse();
        yield return new WaitForSeconds(Bocadillos.Decir(mago.transform, "FINAL_MAGO_VENCIDO", "Mago Oscuro"));
        if (estela) yield return new WaitForSeconds(Bocadillos.Decir(estela, "FINAL_ESTELA_ACABADO", nombreEstela, 2.2f));

        float hasta = Time.time + 4f;
        while (Time.time < hasta && will != null && Vector3.Distance(will.position, mago.transform.position) > 7f)
            yield return null;

        // 2. La traición. Will queda expuesto; Liam se interpone.
        PonerCinematica();
        mago.Gesto("MagicSpecial");
        yield return new WaitForSeconds(Bocadillos.Decir(mago.transform, "FINAL_MAGO_TRAICION", "Mago Oscuro", 1.4f));

        Transform blanco = will;
        if (liam != null && will != null)
        {
            Vector3 entre = Vector3.Lerp(will.position, mago.transform.position, 0.25f);
            Salto(liam.position);
            var agente = liam.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(entre); else liam.position = entre;
            liam.rotation = Quaternion.LookRotation(Plano(mago.transform.position - entre));
            Salto(entre);
            blanco = liam;
        }

        yield return Rayo(rayoTraicion, mago.transform.position + Vector3.up * 1.4f, blanco != null ? blanco.position + Vector3.up * 1.2f : mago.transform.position, 0.6f);
        FeedbackService.HitStop(0.1f, 0.5f);
        FeedbackService.CameraShake(0.8f, 0.5f);
        FeedbackService.ScreenFlash(new Color(0.3f, 0f, 0.4f, 0.5f), 0.4f);
        if (vfxImpactoLanza && blanco && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxImpactoLanza, blanco.position + Vector3.up, Quaternion.identity, 2f);

        if (liam != null)
        {
            var miembro = liam.GetComponent<NPCPartyMember>();
            if (miembro != null && miembro.IsInParty) miembro.LeaveParty();
            var anim = liam.GetComponentInChildren<NPCSimpleAnimator>();
            if (anim) anim.HoldPose("Die01Stay_NoWeapon");
            if (estela) yield return new WaitForSeconds(Bocadillos.Decir(estela, "FINAL_ESTELA_LIAM", nombreEstela, 1.4f));
            yield return new WaitForSeconds(1.2f);
            yield return new WaitForSeconds(Bocadillos.Decir(liam, "LIAM_LAST_WORDS", nombreLiam, 3.2f));
        }

        // 3. El pozo está seco: Will intenta el Hechizo del Tiempo y no queda nada.
        Bocadillos.Decir(will, "FINAL_WILL_INTENTA_TIEMPO", "Will", esperaMaxima, fijo: true);
        yield return EsperarConfirmacion();
        FeedbackService.ScreenFlash(new Color(0.4f, 0.5f, 0.7f, 0.25f), 0.3f);
        yield return new WaitForSeconds(Bocadillos.Decir(will, "FINAL_WILL_POZO_SECO", "Will", 2.6f));

        // 4. El conducto. Estela ofrece; Will pregunta; ella da su consentimiento.
        if (estela && will)
        {
            Salto(estela.position);
            Vector3 alLado = will.position + will.right * 1.4f;
            var agente = estela.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Warp(alLado); else estela.position = alLado;
            estela.rotation = Quaternion.LookRotation(Plano(will.position - alLado));
            Salto(alLado);
        }
        if (estela) yield return new WaitForSeconds(Bocadillos.Decir(estela, "FINAL_ESTELA_CONDUCTO", nombreEstela, 2.2f));
        yield return new WaitForSeconds(Bocadillos.Decir(will, "FINAL_WILL_CUANTO", "Will", 2f));
        if (estela) yield return new WaitForSeconds(Bocadillos.Decir(estela, "FINAL_ESTELA_TODO", nombreEstela, 3f));

        // 5. Corazón Estelar compartido: mantener los dos gatillos mientras llega la energía.
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
                rayoEnergia.SetPosition(0, estela.position + Vector3.up * 1.2f);
                rayoEnergia.SetPosition(1, will.position + Vector3.up * 1.2f);
                rayoEnergia.widthMultiplier = Mathf.Lerp(0.05f, 0.35f, cargado / segundosDeEnergia);
            }
            yield return null;
        }
        Apagar(rayoEnergia);

        // 6. La aguja de luz: no basta para derribar a un mago; basta para cortar un hilo.
        Bocadillos.Decir(will, "FINAL_PULSA_AGUJA", "Will", esperaMaxima, fijo: true);
        yield return EsperarConfirmacion();
        Vector3 desde = will != null ? will.position + Vector3.up * 1.3f : mago.transform.position;
        Vector3 hacia = union != null ? union.position : mago.transform.position;
        if (rayoAguja) rayoAguja.widthMultiplier = 0.06f;
        yield return Rayo(rayoAguja, desde, hacia, 0.35f);
        FeedbackService.HitStop(0.05f, 0.6f);
        FeedbackService.ScreenFlash(new Color(1f, 1f, 0.95f, 0.8f), 0.5f);
        FeedbackService.CameraShake(1f, 0.6f);
        if (vfxCorte && VfxPoolService.Instance != null) VfxPoolService.Instance.Play(vfxCorte, hacia, Quaternion.identity, 3f);

        // 7. Sin vínculo, el Mago se deshace.
        yield return new WaitForSeconds(Bocadillos.Decir(mago.transform, "FINAL_MAGO_DESHACE", "Mago Oscuro", 2f));
        yield return mago.Deshacerse(vfxDeshacerse);

        SoltarCinematica();
        yield return new WaitForSeconds(1.5f);
        if (!string.IsNullOrEmpty(senalAlTerminar))
            DefaultNarrativeSignals.Instance?.RaiseCustom(senalAlTerminar, name);
    }

    void OnDisable() => SoltarCinematica();

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
        if (_accion == null) return;
        _accion.PushMode(ActionMode.Cinematic);
        _cinematica = true;
    }

    private void SoltarCinematica()
    {
        if (!_cinematica || _accion == null) return;
        _accion.PopMode(ActionMode.Cinematic);
        _cinematica = false;
    }
}
