using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Transmite el ánimo de una acción a los presentes sin interrumpir su actuación explícita.
[Serializable]
public class AccionDeEscenaBeat : SequenceBeat
{
    public string accion;
    public AnimoDeAccion animo = AnimoDeAccion.PorAccion;
    public string origenId;
    public string origenMarca;
    public List<string> implicados = new();
    public List<string> excluir = new();
    [Min(0f)] public float radio;
    public bool mirarAlOrigen = true;
    public bool sostenerCara = true;
    [Min(0.1f)] public float duracionDeLaReaccion = 1.5f;
    public bool voces = true;
    [Range(0, 3)] public int maxVoces = 1;
    public bool esperar;
    public override string Describe() => $"Acción de escena: {accion} → {ReaccionesDeEscena.Resolver(accion, animo)}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        var origen = ctx.GetActor(origenId);
        var marca = string.IsNullOrEmpty(origenMarca) ? null : ctx.Stage?.GetMark(origenMarca);
        bool tieneOrigen = origen?.Transform != null || marca != null;
        Vector3 punto = origen?.Transform != null ? origen.Transform.position : marca != null ? marca.position : Vector3.zero;
        ctx.CambiarAccion(accion, ReaccionesDeEscena.Resolver(accion, animo), origenId, origenMarca);
        int version = ctx.VersionDeAccion;
        var afectados = new List<SequenceActor>();
        if (implicados != null && implicados.Count > 0)
        {
            foreach (string id in implicados)
            {
                var actor = ctx.GetActor(id);
                if (actor != null && !afectados.Contains(actor)) afectados.Add(actor);
            }
        }
        else foreach (var actor in ctx.ResolvedActors) afectados.Add(actor);
        afectados.RemoveAll(actor => actor.Transform == null || !actor.Transform.gameObject.activeInHierarchy ||
            actor == origen || (excluir != null && excluir.Contains(actor.Id)) ||
            (radio > 0f && (!tieneOrigen || (actor.Transform.position - punto).sqrMagnitude > radio * radio)));
        // Los vecinos contiguos se recorren juntos; la voz se reserva por proximidad a cámara.
        afectados.Sort((a, b) => a.Transform.position.x.CompareTo(b.Transform.position.x));
        var conVoz = new List<SequenceActor>(afectados);
        var camara = SolYLunaEnElCielo.CamaraActual() ?? ctx.Player.CachedCamera;
        if (camara != null) conVoz.Sort((a, b) =>
            (a.Transform.position - camara.transform.position).sqrMagnitude.CompareTo(
                (b.Transform.position - camara.transform.position).sqrMagnitude));
        conVoz.RemoveAll(actor => !ctx.PuedeReaccionar(actor));
        int limite = voces ? Mathf.Clamp(maxVoces, 0, 3) : 0;
        if (conVoz.Count > limite) conVoz.RemoveRange(limite, conVoz.Count - limite);
        string anterior = null;
        var pendientes = new List<Coroutine>();
        foreach (var actor in afectados)
        {
            if (sostenerCara) ctx.SostenerCara(actor);
            if (!ctx.PuedeReaccionar(actor)) continue;
            string gesto = ReaccionesDeEscena.ElegirGesto(actor, ctx.AnimoVigente, anterior);
            if (gesto != null) anterior = gesto;
            var rutina = ctx.Player.StartCoroutine(Reaccionar(ctx, actor, punto, tieneOrigen, gesto, conVoz.Contains(actor), version));
            pendientes.Add(rutina);
        }
        Action detener = () =>
        {
            foreach (var rutina in pendientes) if (rutina != null && ctx.Player != null) ctx.Player.StopCoroutine(rutina);
        };
        ctx.RegistrarLimpiezaDeAccion(detener);
        ctx.Player.RegisterCleanup(detener);
        if (esperar) foreach (var rutina in pendientes) yield return rutina;
    }

    private IEnumerator Reaccionar(SequenceContext ctx, SequenceActor actor, Vector3 punto, bool tieneOrigen,
        string gesto, bool sonar, int version)
    {
        yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0f, 0.4f));
        if (ctx.VersionDeAccion != version || !ctx.PuedeReaccionar(actor)) yield break;
        float duracion = Mathf.Max(0.1f, duracionDeLaReaccion);
        if (mirarAlOrigen && tieneOrigen)
        {
            var giro = actor.GirarSuavemente(punto, 0.25f);
            bool giroCerrado = false;
            Action cerrarGiro = () =>
            {
                if (giroCerrado) return;
                giroCerrado = true;
                (giro as IDisposable)?.Dispose();
                if (actor.Transform != null) actor.SyncRotation();
            };
            ctx.RegistrarLimpiezaDeAccion(cerrarGiro);
            ctx.Player.RegisterCleanup(cerrarGiro);
            while (ctx.VersionDeAccion == version && ctx.PuedeReaccionar(actor) && giro.MoveNext())
                yield return giro.Current;
            cerrarGiro();
        }
        if (ctx.VersionDeAccion != version || !ctx.PuedeReaccionar(actor)) yield break;
        actor.Reaccionar(ReaccionesDeEscena.Cara(ctx.AnimoVigente), duracion);
        actor.PlayGesture(gesto);
        if (sonar) AudioService.Instance?.PlayReaction(ReaccionesDeEscena.Personaje(actor),
            ReaccionesDeEscena.Voz(ctx.AnimoVigente), 1f, actor.Transform.position, actor.Id, ctx.Player,
            risaSiAnimoGastado: ctx.AnimoVigente == AnimoDeAccion.Alegre);
        bool cerrada = false;
        Action cerrar = () =>
        {
            if (cerrada) return;
            cerrada = true;
            if (ctx.VersionDeAccion != version || !ctx.PuedeReaccionar(actor)) return;
            var animador = actor.AnimadorDeActuacion;
            int capa = animador != null && gesto != null ? AnimatorLayerUtil.ResolveLayer(animador, gesto, animador.GetLayerIndex("UpperBody")) : -1;
            if (capa >= 0 && (animador.GetCurrentAnimatorStateInfo(capa).IsName(gesto) ||
                (animador.IsInTransition(capa) && animador.GetNextAnimatorStateInfo(capa).IsName(gesto)))) actor.ReturnToNormalPose();
            actor.Emotion?.VolverAReposo();
        };
        ctx.RegistrarLimpiezaDeAccion(cerrar);
        ctx.Player.RegisterCleanup(cerrar);
        yield return new WaitForSecondsRealtime(duracion);
        cerrar();
    }
}
