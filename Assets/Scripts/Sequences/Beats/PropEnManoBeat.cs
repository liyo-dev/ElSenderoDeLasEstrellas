using System;
using System.Collections;
using UnityEngine;

/// Reproduce una recogida o una suelta calculada por el horno; no decide el gesto ni su instante.
[Serializable]
public class PropEnManoBeat : SequenceBeat
{
    public string actorId;
    public string propId;
    public bool dejar;
    public HumanBodyBones mano = HumanBodyBones.RightHand;
    public Vector3 offset = new Vector3(0f, 0.04f, 0.06f);
    public Vector3 giro;
    public Vector3 posicionFinal;
    public Vector3 giroFinal;
    public bool tieneDestinoFinal;
    public string gestoHorneado;
    public float segundoDelGesto;
    [Tooltip("Segundos que tarda el objeto en pasar de la mano a su apoyo al soltarlo.")]
    public float segundosDeApoyo = 0.25f;

    public override string Describe() => dejar
        ? $"Dejar {propId}: {gestoHorneado}, suelta en {segundoDelGesto:0.000} s del gesto"
        : $"Coger {propId} ({mano})";

    /// Se registra al preparar el guion, incluso cuando el salto omite la recogida o la suelta.
    public void RegistrarFinal(SequenceContext ctx)
    {
        var prop = ctx.GetActor(propId)?.Transform;
        if (prop == null) return;
        var padre = prop.parent;
        var cuerpo = prop.GetComponent<Rigidbody>();
        bool cinematico = cuerpo != null && cuerpo.isKinematic;
        ctx.Player?.RegisterCleanup(() =>
        {
            if (prop == null) return;
            prop.SetParent(padre, true);
            if (tieneDestinoFinal)
            {
                prop.SetPositionAndRotation(posicionFinal, Quaternion.Euler(giroFinal));
                if (!prop.gameObject.activeSelf) prop.gameObject.SetActive(true);
            }
            if (cuerpo != null) cuerpo.isKinematic = cinematico;
        });
    }

    /// Una suelta no se coloca antes de la escena: el objeto solo tiene que volver a su sitio
    /// cuando se suelta, no estar en la mano de nadie al empezar.
    public override void PrepararEnManos(SequenceContext ctx)
    {
        if (dejar) return;
        RegistrarFinal(ctx);
    }

    public override IEnumerator Run(SequenceContext ctx)
    {
        var prop = ctx.GetActor(propId)?.Transform;
        if (prop == null) yield break;
        if (dejar)
        {
            prop.SetParent(null, true);
            if (!prop.gameObject.activeSelf) prop.gameObject.SetActive(true);
            prop.GetPositionAndRotation(out var desde, out var giroDesde);
            var hasta = Quaternion.Euler(giroFinal);
            for (float t = 0f; t < segundosDeApoyo; t += Time.deltaTime)
            {
                if (prop == null) yield break;
                float u = Mathf.SmoothStep(0f, 1f, t / segundosDeApoyo);
                prop.SetPositionAndRotation(Vector3.Lerp(desde, posicionFinal, u), Quaternion.Slerp(giroDesde, hasta, u));
                yield return null;
            }
            prop.SetPositionAndRotation(posicionFinal, hasta);
            yield break;
        }

        var anim = ctx.GetActor(actorId)?.AnimadorDeActuacion;
        var hueso = anim != null && anim.isHuman ? anim.GetBoneTransform(mano) : null;
        if (hueso == null) yield break;
        var cuerpo = prop.GetComponent<Rigidbody>();
        if (cuerpo != null) cuerpo.isKinematic = true;
        NPCWorldPoint.ColocarPropEnMano(prop, hueso, offset, giro);
        if (!prop.gameObject.activeSelf) prop.gameObject.SetActive(true);
    }
}
