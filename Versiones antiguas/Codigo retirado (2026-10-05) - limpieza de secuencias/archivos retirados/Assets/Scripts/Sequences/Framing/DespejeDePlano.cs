using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Retira del plano a los figurantes que estorban, como se hace en un rodaje.
///
/// En cada CORTE calculado, cualquier personaje que no cuente en ese plano y que tape la cara del
/// sujeto, o que quede pegado a la lente, deja de renderizarse mientras dura el plano y vuelve a
/// verse en el corte siguiente. Solo se hace en un corte: a mitad de plano se vería desaparecer.
/// Nunca se tocan el sujeto, el secundario, quien está hablando ni los protagonistas.
/// El componente no apaga GameObjects: la IA y la animación del figurante siguen intactas.
/// Ver INC-582.
public sealed class DespejeDePlano
{
    /// Líneas a partir de las cuales un personaje cuenta como protagonista si la secuencia no da
    /// una lista explícita: los figurantes dicen una o dos frases, los protagonistas muchas más.
    private const int LineasDeProtagonista = 3;

    /// Distancia a la lente por debajo de la cual un figurante se considera pegado a ella.
    private const float DistanciaALaLente = 1.2f;

    /// Fracción del cuadro a partir de la cual un figurante en primer término se come el plano.
    private const float AreaQueEstorba = 0.12f;

    private readonly SequenceContext _contexto;
    private readonly HashSet<string> _protegidos = new();
    private readonly List<(Renderer renderer, bool enabled, ShadowCastingMode sombras)> _retirados = new();
    private readonly List<string> _retiradosIds = new();

    public DespejeDePlano(SequenceContext ctx)
    {
        _contexto = ctx;
        var definicion = ctx.Player != null ? ctx.Player.Definition : null;
        if (definicion != null)
        {
            if (definicion.protagonistas != null && definicion.protagonistas.Count > 0)
            {
                foreach (var id in definicion.protagonistas)
                    if (!string.IsNullOrEmpty(id)) _protegidos.Add(id);
            }
            else if (definicion.phases != null)
            {
                var lineas = new Dictionary<string, int>();
                foreach (var fase in definicion.phases)
                    if (fase?.beats != null) ContarLineas(fase.beats, lineas);
                foreach (var par in lineas)
                    if (par.Value >= LineasDeProtagonista) _protegidos.Add(par.Key);
            }
        }
        _protegidos.Add(SequenceActor.PlayerId);
        ctx.Player?.RegisterCleanup(Restaurar);
    }

    private static void ContarLineas(List<SequenceBeat> beats, Dictionary<string, int> lineas)
    {
        foreach (var beat in beats)
        {
            string actor = beat switch
            {
                SayBeat linea => linea.actorId,
                DialogueBeat dialogo => dialogo.actorId,
                _ => null
            };
            if (!string.IsNullOrEmpty(actor)) lineas[actor] = lineas.TryGetValue(actor, out int n) ? n + 1 : 1;
            if (beat is ParallelBeat paralelo && paralelo.beats != null) ContarLineas(paralelo.beats, lineas);
            if (beat is SerieBeat serie && serie.beats != null) ContarLineas(serie.beats, lineas);
        }
    }

    /// Personajes retirados en el plano actual (para el informe de rodaje).
    public IReadOnlyList<string> Retirados => _retiradosIds;

    /// ¿Se puede retirar a este personaje del plano descrito? Lo consulta también ShotComposer
    /// para no descartar un buen ángulo por culpa de alguien que de todas formas se va a retirar.
    public bool Despejable(SequenceActor actor, ShotFraming plano)
    {
        if (actor?.Transform == null || actor.IsDynamic || plano == null) return false;
        if (actor.Id == plano.subjectId || actor.Id == plano.secondaryId) return false;
        if (_protegidos.Contains(actor.Id)) return false;
        return !_contexto.EstaHablando(actor);
    }

    public void Restaurar()
    {
        foreach (var estado in _retirados)
            if (estado.renderer != null)
            {
                estado.renderer.enabled = estado.enabled;
                estado.renderer.shadowCastingMode = estado.sombras;
            }
        _retirados.Clear();
        _retiradosIds.Clear();
    }

    /// Restaura el plano anterior y retira a quien estorbe en el nuevo.
    public void Aplicar(ShotFraming plano, ShotSolution solucion, float aspecto)
    {
        Restaurar();
        if (plano == null || plano.type == ShotType.Wide) return;
        var sujeto = _contexto.GetActor(plano.subjectId);
        if (sujeto?.Transform == null) return;

        Quaternion inversa = Quaternion.Inverse(solucion.rotation);
        float tanV = Mathf.Tan(Mathf.Clamp(solucion.fieldOfView, 1f, 170f) * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * Mathf.Max(0.1f, aspecto);

        // La zona que no puede taparse: la cara del sujeto (y la del secundario en un two-shot).
        Rect caraSujeto = RectDeCara(sujeto, solucion.position, inversa, tanV, tanH, out float fondoSujeto);
        var secundario = plano.type == ShotType.TwoShot ? _contexto.GetActor(plano.secondaryId) : null;
        Rect caraSecundario = default;
        float fondoSecundario = float.PositiveInfinity;
        bool haySecundario = secundario?.Transform != null
            && (caraSecundario = RectDeCara(secundario, solucion.position, inversa, tanV, tanH, out fondoSecundario)).width > 0f;

        foreach (var actor in _contexto.PersonajesVisibles)
        {
            if (!Despejable(actor, plano) || !actor.Transform.gameObject.activeInHierarchy) continue;
            float alto = actor.HeadTopHeight;
            float radio = actor.SafeRadius;
            Vector3 centro = actor.Transform.position + Vector3.up * (alto * 0.5f);
            Vector3 local = inversa * (centro - solucion.position);
            if (local.z + radio <= 0.01f) continue;   // detrás de la cámara

            bool pegado = Vector3.Distance(centro, solucion.position) - radio < DistanciaALaLente;
            Rect cuerpo = Proyectar(local, new Vector2(radio, alto * 0.5f), tanV, tanH);
            bool delante = local.z - radio < fondoSujeto;
            float ancho = Mathf.Max(0f, Mathf.Min(1f, cuerpo.xMax) - Mathf.Max(0f, cuerpo.xMin));
            float altura = Mathf.Max(0f, Mathf.Min(1f, cuerpo.yMax) - Mathf.Max(0f, cuerpo.yMin));
            bool tapa = (delante && (cuerpo.Overlaps(caraSujeto) || ancho * altura > AreaQueEstorba))
                || (haySecundario && local.z - radio < fondoSecundario && cuerpo.Overlaps(caraSecundario));
            if (!pegado && !tapa) continue;

            _retiradosIds.Add(actor.Id);
            foreach (var renderer in actor.Transform.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                _retirados.Add((renderer, renderer.enabled, renderer.shadowCastingMode));
                renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (_retiradosIds.Count > 0)
            Debug.Log($"[DespejeDePlano] '{plano.Describe()}': fuera del plano {string.Join(", ", _retiradosIds)}.");
#endif
    }

    private static Rect RectDeCara(SequenceActor actor, Vector3 camara, Quaternion inversa,
        float tanV, float tanH, out float profundidad)
    {
        Vector3 ojos = actor.Transform.position + Vector3.up * actor.AlturaDeOjos;
        float radioCara = Mathf.Max(0.18f, (actor.HeadTopHeight - actor.AlturaDeOjos) * 1.1f);
        Vector3 local = inversa * (ojos - camara);
        profundidad = local.z;
        if (local.z <= 0.05f) return new Rect(0f, 0f, 1f, 1f);
        return Proyectar(local, new Vector2(radioCara, radioCara), tanV, tanH);
    }

    private static Rect Proyectar(Vector3 local, Vector2 semiejes, float tanV, float tanH)
    {
        float z = Mathf.Max(0.05f, local.z);
        float cx = 0.5f + local.x / (2f * z * tanH);
        float cy = 0.5f + local.y / (2f * z * tanV);
        float ax = semiejes.x / (2f * z * tanH);
        float ay = semiejes.y / (2f * z * tanV);
        return Rect.MinMaxRect(cx - ax, cy - ay, cx + ax, cy + ay);
    }
}
