#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Poses medidas en copias de horneado y operaciones con objetos del registro del escenario.
internal sealed partial class Horno
{
    private readonly Dictionary<ActorH, GameObject> _poses = new();
    private readonly Dictionary<(ActorH, int), Vector3> _cabezas = new();
    private readonly List<string> _manipulaciones = new();
    private readonly List<GameObject> _actoresDeEscenaApagados = new();

    private void OcultarActoresDeEscena()
    {
        if (_stage == null || _stage.Actors == null) return;
        foreach (var a in _stage.Actors)
        {
            if (a.target != null && a.target.gameObject.activeSelf)
            {
                a.target.gameObject.SetActive(false);
                _actoresDeEscenaApagados.Add(a.target.gameObject);
            }
        }
        Physics.SyncTransforms();
    }

    private GameObject PoseDe(ActorH actor)
    {
        if (_poses.TryGetValue(actor, out var pose)) { pose.SetActive(true); return pose; }
        pose = UnityEngine.Object.Instantiate(actor.prefab);
        pose.hideFlags = HideFlags.HideAndDontSave;
        foreach (var comportamiento in pose.GetComponentsInChildren<MonoBehaviour>(true)) comportamiento.enabled = false;
        foreach (var renderer in pose.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var collider in pose.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        _poses[actor] = pose;
        return pose;
    }

    internal void LiberarPoses()
    {
        foreach (var pose in _poses.Values) if (pose != null) UnityEngine.Object.DestroyImmediate(pose);
        _poses.Clear();
        foreach (var go in _actoresDeEscenaApagados) if (go != null) go.SetActive(true);
        _actoresDeEscenaApagados.Clear();
        Physics.SyncTransforms();
    }

    private static AnimationClip ClipDe(ActorH actor, GameObject pose, string estado)
    {
        var catalogo = CapturaDeEscenario.Catalogo(actor.prefab).FirstOrDefault(e => e.estado == estado);
        return pose.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController?.animationClips
            .FirstOrDefault(c => c != null && c.name == catalogo.clip);
    }

    private void MuestrearPose(ActorH actor, GameObject pose, float t)
    {
        var anim = pose.GetComponentInChildren<Animator>(true);
        anim.Rebind();
        var orden = actor.anim.LastOrDefault(o => o.t <= t && (o.tipo == TipoDeAnimacion.Bucle || o.tipo == TipoDeAnimacion.Reposo));
        string estado = orden.tipo == TipoDeAnimacion.Bucle ? orden.estado : "Idle01";
        var clip = ClipDe(actor, pose, estado);
        if (clip != null) MuestrearEstado(pose, clip, estado, orden.congelar ? Mathf.Min(t - orden.t, clip.length) : Mathf.Repeat(t - orden.t, clip.length));
        var gesto = actor.anim.LastOrDefault(o => o.tipo == TipoDeAnimacion.Gesto && o.t <= t);
        if (!string.IsNullOrEmpty(gesto.estado) && gesto.t >= orden.t)
        {
            var c = ClipDe(actor, pose, gesto.estado);
            if (c != null && t - gesto.t <= c.length) MuestrearEstado(pose, c, gesto.estado, t - gesto.t);
        }
        pose.transform.SetPositionAndRotation(actor.PosEn(t), Quaternion.Euler(0f, actor.RumboEn(t, _actores), 0f));
    }

    /// Evalúa el clip y el Avatar humanoide: las curvas de músculos necesitan el Animator.
    internal static void MuestrearEstado(GameObject pose, AnimationClip clip, string estado, float t)
    {
        var anim = pose.GetComponentInChildren<Animator>(true);
        if (anim == null) { clip.SampleAnimation(pose, t); return; }
        clip.SampleAnimation(anim.gameObject, t);
        if (!anim.isHuman) return;
        anim.enabled = true;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.applyRootMotion = false;
        int capa = AnimatorLayerUtil.ResolveLayer(anim, estado, 0);
        if (capa < 0) return;
        if (capa > 0) anim.SetLayerWeight(capa, 1f);
        anim.Play(Animator.StringToHash(estado), capa, Mathf.Clamp01(t / Mathf.Max(0.01f, clip.length)));
        anim.Update(0f);
    }

    private Vector3 CabezaEn(ActorH actor, float t)
    {
        var clave = (actor, Mathf.RoundToInt(t * 20f));
        if (_cabezas.TryGetValue(clave, out var cabeza)) return cabeza;
        if (actor.prefab == null) return actor.PosEn(t) + Vector3.up * actor.AlturaDeCara;
        var pose = PoseDe(actor);
        MuestrearPose(actor, pose, t);
        var anim = pose.GetComponentInChildren<Animator>(true);
        var hueso = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        cabeza = hueso != null ? hueso.position + hueso.up * actor.Cabeza * 0.12f : actor.PosEn(t) + Vector3.up * actor.AlturaDeCara;
        _cabezas[clave] = cabeza;
        pose.SetActive(false);
        return cabeza;
    }

    /// Tumbado (dormido, recostado, derribado…): la cabeza de la pose horneada queda por debajo de la
    /// mitad de su altura de pie. Se mide en la pose, sin depender del nombre del estado.
    private bool PoseHorizontal(ActorH actor, float t)
    {
        var o = actor.anim.LastOrDefault(x => x.t <= t && (x.tipo == TipoDeAnimacion.Bucle || x.tipo == TipoDeAnimacion.Reposo));
        if (o.tipo != TipoDeAnimacion.Bucle || actor.prefab == null) return false;
        return CabezaEn(actor, t).y - actor.PosEn(t).y < actor.AlturaDeCara * 0.5f;
    }

    private float PropEnMano(ActorH actor, GuionTexto.Orden orden, float t)
    {
        string id = orden.args.FirstOrDefault();
        var prop = _stage != null ? _stage.Props.FirstOrDefault(p => p.id == id).target : null;
        if (prop == null) { Error($"{orden}: '{id}' no está en los objetos del SequenceStage."); return 0f; }
        var beat = new PropEnManoBeat { actorId = actor.id, propId = id, dejar = orden.verbo == "deja" };
        string mano = orden.Op("mano", "derecha");
        if (mano != "derecha" && mano != "izquierda") { Error($"{orden}: mano debe ser derecha o izquierda."); return 0f; }
        beat.mano = mano == "izquierda" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        if (orden.opciones.TryGetValue("offset", out var offset) && Convertir(offset, typeof(Vector3), out var v, orden)) beat.offset = (Vector3)v;
        if (orden.opciones.TryGetValue("giro", out var giro) && Convertir(giro, typeof(Vector3), out var r, orden)) beat.giro = (Vector3)r;
        float dura = 0f;
        float instante = t;
        if (beat.dejar)
        {
            string marca = orden.args.Count >= 3 && orden.args[1] == "en" ? orden.args[2] : null;
            if (!Punto(marca, orden.linea, out var destino)) return 0f;
            var punto = MarcaDePunto(marca);
            Quaternion rot = punto != null ? punto.rotation : Quaternion.identity;
            beat.posicionFinal = ApoyarEnMarca(prop, destino, rot);
            beat.giroFinal = rot.eulerAngles;
            beat.tieneDestinoFinal = true;
            var pose = PoseDe(actor);
            var anim = pose.GetComponentInChildren<Animator>(true);
            var hueso = anim != null && anim.isHuman ? anim.GetBoneTransform(beat.mano) : null;
            if (hueso == null) { Error($"{orden}: el actor necesita un esqueleto humanoide con {beat.mano}."); return 0f; }
            string explicito = orden.Op("gesto");
            string[] candidatos = explicito != null ? new[] { explicito } : new[] { "Opening01_Begin", "Opening01_Stop", "Loot01_Begin", "Loot01_Stop", "FoundSomething_NoWeapon", "InteractWithGateObject_NoWeapon" };
            float mejor = float.PositiveInfinity, segundo = 0f;
            AnimationClip elegido = null;
            var tanteo = new List<string>();
            foreach (string estado in candidatos)
            {
                if (!actor.estados.ContainsKey(estado)) continue;
                var clip = ClipDe(actor, pose, estado);
                if (clip == null) continue;
                float mejorDelClip = float.PositiveInfinity;
                for (float s = 0f; s <= clip.length; s += 1f / 60f)
                {
                    anim.Rebind();
                    MuestrearEstado(pose, clip, estado, s);
                    pose.transform.SetPositionAndRotation(actor.PosEn(t), Quaternion.Euler(0f, actor.RumboEn(t, _actores), 0f));
                    var delta = hueso.position - destino;
                    float coste = Mathf.Abs(delta.y) * 2f + new Vector2(delta.x, delta.z).magnitude;
                    mejorDelClip = Mathf.Min(mejorDelClip, coste);
                    if (coste >= mejor) continue;
                    mejor = coste; segundo = s; elegido = clip; beat.gestoHorneado = estado;
                }
                tanteo.Add($"{estado} {mejorDelClip:0.00}");
            }
            if (elegido == null) { Error($"{orden}: no hay un clip de alcance válido (gesto={explicito})."); return 0f; }
            // Dentro del gesto elegido se suelta en el mínimo de distancia tridimensional al apoyo.
            mejor = float.PositiveInfinity;
            for (float s = 0f; s <= elegido.length; s += 1f / 60f)
            {
                anim.Rebind(); MuestrearEstado(pose, elegido, beat.gestoHorneado, s);
                pose.transform.SetPositionAndRotation(actor.PosEn(t), Quaternion.Euler(0f, actor.RumboEn(t, _actores), 0f));
                float distancia = (hueso.position - destino).sqrMagnitude;
                if (distancia < mejor) { mejor = distancia; segundo = s; }
            }
            beat.segundoDelGesto = segundo;
            pose.SetActive(false);
            actor.Animar(t, TipoDeAnimacion.Gesto, beat.gestoHorneado);
            instante += segundo; dura = elegido.length;
            if (actor.bucle != null) actor.Animar(t + dura, TipoDeAnimacion.Bucle, actor.bucle);
            float alcance = Mathf.Sqrt(mejor);
            _manipulaciones.Add($"{actor.alias} deja {id}: {beat.gestoHorneado}; suelta +{segundo:0.000} s (guion {instante:0.000} s), mano a {alcance:0.000} m del apoyo. Tanteo: {string.Join(", ", tanteo)}.");
            if (alcance > 0.3f) Aviso($"{orden}: la mano se queda a {alcance:0.00} m del apoyo; acerca al personaje o el apoyo al borde del mueble.");
        }
        _efectos.Add(new EfectoHorneado { t = instante, beat = beat, linea = orden.linea });
        return dura;
    }

    private Transform MarcaDePunto(string nombre)
    {
        var p = _g.puntos.FirstOrDefault(x => x.nombre == nombre);
        string n = p != null && p.marca != null ? p.marca : nombre;
        var marca = _stage?.Marks.FirstOrDefault(x => x.name == n).target;
        return marca != null ? marca : _escena.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x.name == n);
    }

    private static Vector3 ApoyarEnMarca(Transform prop, Vector3 destino, Quaternion giro)
    {
        prop.GetPositionAndRotation(out var antes, out var rot);
        try
        {
            prop.SetPositionAndRotation(destino, giro);
            float baseY = float.PositiveInfinity;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) baseY = Mathf.Min(baseY, r.bounds.min.y);
            return float.IsPositiveInfinity(baseY) ? destino : destino + Vector3.up * (destino.y - baseY);
        }
        finally { prop.SetPositionAndRotation(antes, rot); }
    }

    private void FinalizarProps()
    {
        foreach (var efecto in _efectos)
        {
            if (!(efecto.beat is PropEnManoBeat coger) || coger.dejar) continue;
            var suelta = _efectos.Where(x => x.t >= efecto.t && x.beat is PropEnManoBeat b && b.dejar && b.propId == coger.propId).OrderBy(x => x.t).FirstOrDefault();
            if (suelta?.beat is PropEnManoBeat dejar)
            {
                coger.tieneDestinoFinal = true;
                coger.posicionFinal = dejar.posicionFinal;
                coger.giroFinal = dejar.giroFinal;
            }
            else
            {
                var prop = _stage.Props.First(x => x.id == coger.propId).target;
                coger.tieneDestinoFinal = true; coger.posicionFinal = prop.position; coger.giroFinal = prop.eulerAngles;
                Aviso($"{coger.propId}: recogida sin «deja» posterior; al cerrar vuelve a su sitio inicial.");
            }
        }
    }
}
#endif
