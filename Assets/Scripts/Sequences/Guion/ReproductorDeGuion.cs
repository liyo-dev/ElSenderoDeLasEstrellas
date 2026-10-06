using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;

/// Reproduce un GuionHorneado segundo a segundo. No decide nada: lee la tabla.
///
/// - Los personajes van exactamente por el camino horneado (sin NavMeshAgent mientras dura el
///   guion: ni esquivan, ni se empujan, ni se atascan). La animación de andar sale de su velocidad
///   real, con el mismo criterio que el resto del juego (NavMeshAgentUtility.FactorDeLocomocion).
/// - Las frases empiezan en su segundo y duran lo que dura su voz.
/// - Las cámaras las mueve Cinemachine (ver CamarasDeGuion) con la posición horneada.
/// - Los efectos (VFX, sonidos, cut-ins...) son los beats de siempre, lanzados en su segundo.
public sealed class ReproductorDeGuion
{
    private readonly GuionHorneado _g;
    private readonly SequenceContext _ctx;
    private readonly float _inicio;
    private readonly List<Interprete> _interpretes = new();
    private readonly Dictionary<string, Interprete> _porId = new();
    private CamarasDeGuion _camaras;
    private GameObject _puntos;
    private int _iLinea, _iPlano, _iEfecto;
    private bool _terminado;
    private float _t;
    private LineaHorneada _lineaEnCurso;
    private float _tFoto = float.MaxValue;
    private int _nPlano;
    private float _tRevision;
    private readonly HashSet<string> _desviados = new();

    public ReproductorDeGuion(GuionHorneado guion, SequenceContext ctx, float inicio)
    {
        _g = guion;
        _ctx = ctx;
        _inicio = inicio;
    }

    const float PasoMaximo = 0.1f;

    public float Tiempo => _t;

    public IEnumerator Reproducir()
    {
        Preparar();
        _t = _inicio;
        SaltarHasta(_inicio);

        while (_t < _g.duracion && !_terminado)
        {
            Avanzar(_t);
            yield return null;
            // Un tirón del editor o una pausa no deben saltarse planos ni frases: el guion
            // avanza como mucho un paso corto por fotograma y se retrasa en vez de saltar.
            _t += Mathf.Min(Time.unscaledDeltaTime, PasoMaximo);
        }
        Avanzar(_g.duracion);
    }

    // ── Preparación y cierre ────────────────────────────────────────────────────────────────

    private void Preparar()
    {
        foreach (var datos in _g.actores)
        {
            var actor = _ctx.GetActor(datos.id);
            if (actor?.Transform == null)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[Guion] No encuentro a '{datos.id}' ({datos.alias}) en la escena: " +
                    "no va a aparecer. ¿Está en el roster de NPCs de esta escena?");
#endif
                continue;
            }
            var interprete = new Interprete(datos, actor);
            interprete.Tomar();
            _interpretes.Add(interprete);
            _porId[datos.id] = interprete;
        }

        var transformPorId = new Dictionary<string, Transform>();
        foreach (var kv in _porId) transformPorId[kv.Key] = kv.Value.Actor.Transform;
        // Objetos del decorado que algún plano sigue (el globo que sube).
        foreach (var plano in _g.planos)
            foreach (var id in plano.sujetos)
                if (!transformPorId.ContainsKey(id))
                {
                    var prop = _ctx.GetActor(id);
                    if (prop?.Transform != null) transformPorId[id] = prop.Transform;
                }
        _camaras = new CamarasDeGuion(_g, transformPorId);

        // Los puntos del guion valen como marcas para los efectos (un VFX «en=choque»).
        if (_ctx.Stage != null && _g.puntos != null && _g.puntos.Count > 0)
        {
            _puntos = new GameObject("Guion · puntos");
            foreach (var p in _g.puntos)
            {
                var t = new GameObject(p.nombre).transform;
                t.SetParent(_puntos.transform, false);
                t.position = p.posicion;
                _ctx.Stage.PonerMarcaTemporal(p.nombre, t);
            }
        }
        _camaras.Preparar(_ctx.Player != null ? _ctx.Player.CachedCamera : Camera.main);
        Sendero.Core.Feedback.FeedbackService.SetCameraShakeProvider(_camaras);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        InformeDeRodaje.Fase("Guion: " + _g.nombre);
#endif
    }

    public void Terminar()
    {
        if (_terminado) return;
        _terminado = true;
        CerrarLinea();
        Sendero.Core.Feedback.FeedbackService.SetCameraShakeProvider(null);
        _camaras?.Terminar();
        _ctx.Stage?.QuitarMarcasTemporales();
        if (_puntos != null) Object.Destroy(_puntos);
        foreach (var i in _interpretes) i.Soltar();
        _interpretes.Clear();
        _porId.Clear();
    }

    // ── Bucle ───────────────────────────────────────────────────────────────────────────────

    private void Avanzar(float t)
    {
        // Planos primero: un corte y la frase que lo acompaña caen en el mismo fotograma.
        while (_iPlano < _g.planos.Count && _g.planos[_iPlano].t0 <= t)
        {
            var plano = _g.planos[_iPlano++];
            _camaras.Activar(plano);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            InformeDeRodaje.PlanoNuevo(plano.descripcion, null, null);
            _nPlano = _iPlano;
            _tFoto = Mathf.Lerp(plano.t0, plano.t1, 0.35f);
            _desviados.Clear();
#endif
        }

        while (_iEfecto < _g.efectos.Count && _g.efectos[_iEfecto].t <= t)
            Lanzar(_g.efectos[_iEfecto++]);

        if (_lineaEnCurso != null && t >= _lineaEnCurso.t1) CerrarLinea();
        while (_iLinea < _g.lineas.Count && _g.lineas[_iLinea].t0 <= t)
            AbrirLinea(_g.lineas[_iLinea++]);

        foreach (var i in _interpretes) i.Actualizar(t, _porId);
        _camaras.Actualizar(t);

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (t >= _tFoto) { _tFoto = float.MaxValue; InformeDeRodaje.Foto($"plano{_nPlano:000}"); }
        // ¿Alguien se ha salido del camino horneado (le empuja algo, otro script lo mueve)?
        if (t >= _tRevision)
        {
            _tRevision = t + 0.5f;
            foreach (var i in _interpretes)
            {
                if (i.Actor?.Transform == null || i.Datos.posiciones.Count == 0) continue;
                Vector3 debe = Interpolar.Posicion(i.Datos.posiciones, t), esta = i.Actor.Transform.position;
                float d = new Vector2(debe.x - esta.x, debe.z - esta.z).magnitude;
                if (d > 0.35f && _desviados.Add(i.Datos.id))
                    InformeDeRodaje.Aviso($"{i.Datos.alias} va {d:0.00} m fuera de su camino en {t:0.0} s");
            }
        }
#endif
    }

    /// Para probar desde la mitad: coloca a todos y pone el estado (hora, música...) sin
    /// reproducir lo que ya pasó.
    private void SaltarHasta(float t)
    {
        if (t <= 0f) return;
        while (_iEfecto < _g.efectos.Count && _g.efectos[_iEfecto].t < t)
        {
            var e = _g.efectos[_iEfecto++];
            if (e.beat != null && EsDeEstado(e.beat)) Lanzar(e);
        }
        while (_iLinea < _g.lineas.Count && _g.lineas[_iLinea].t1 <= t) _iLinea++;
        while (_iPlano + 1 < _g.planos.Count && _g.planos[_iPlano + 1].t0 <= t) _iPlano++;
        foreach (var i in _interpretes) i.SaltarHasta(t);
    }

    private static bool EsDeEstado(SequenceBeat beat)
        => beat is TimeOfDayBeat || beat is MusicBeat || beat is PostprocesoBeat || beat is AmbienteBeat
           || beat is WeatherBeat || beat is SetPropActiveBeat || beat is SetFlagBeat || beat is MezclaBeat
           || beat is BandasDeCineBeat || beat is SolDeFondoBeat;

    private void Lanzar(EfectoHorneado e)
    {
        if (e.beat == null || _ctx.Player == null) return;
        try
        {
            var rutina = _ctx.Player.StartCoroutine(Proteger(e));
            _ctx.Player.TrackBackgroundRoutine(rutina);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Guion] El efecto de la línea {e.linea} ({e.beat.Describe()}) ha fallado: {ex.Message}");
        }
    }

    private IEnumerator Proteger(EfectoHorneado e)
    {
        IEnumerator rutina;
        try { rutina = e.beat.Run(_ctx); }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Guion] Efecto de la línea {e.linea}: {ex.Message}");
            yield break;
        }
        while (true)
        {
            object actual;
            try
            {
                if (!rutina.MoveNext()) yield break;
                actual = rutina.Current;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Guion] Efecto de la línea {e.linea} ({e.beat.Describe()}): {ex.Message}");
                yield break;
            }
            yield return actual;
        }
    }

    // ── Frases ──────────────────────────────────────────────────────────────────────────────

    private void AbrirLinea(LineaHorneada linea)
    {
        CerrarLinea();
        _lineaEnCurso = linea;
        _porId.TryGetValue(linea.actor, out var quien);
        var datos = quien?.Datos;
        var actor = quien?.Actor;
        if (actor != null) _ctx.MarcarHabla(actor, true);
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        InformeDeRodaje.Linea(linea.actor, linea.clave);
#endif

        var bocadillo = SpeechBubbleUI.Instance;
        if (bocadillo != null && actor?.Transform != null)
        {
            string texto = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(linea.clave, linea.clave) : linea.clave;
            string nombre = datos != null && !string.IsNullOrEmpty(datos.claveDelNombre) && LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(datos.claveDelNombre, datos.claveDelNombre) : null;
            var presentacion = linea.presentacion >= 0 ? (PresentacionDeTexto)linea.presentacion : _g.presentacion;
            _ctx.CambiarPresentacionDeLinea(presentacion);
            bocadillo.Show(actor.Transform, texto, (linea.t1 - linea.t0) + 0.35f,
                speakerName: nombre, worldOffset: new Vector3(0f, actor.HeadTopHeight + 0.3f, 0f),
                presentacion: presentacion, colorDelNombre: datos != null ? datos.colorDelNombre : default);
        }

        if (VoiceLines.TryGet(linea.clave, out var voz) && AudioService.Instance != null)
            AudioService.Instance.PlayVoice(voz);
    }

    private void CerrarLinea()
    {
        if (_lineaEnCurso == null) return;
        if (_porId.TryGetValue(_lineaEnCurso.actor, out var quien) && quien.Actor != null)
            _ctx.MarcarHabla(quien.Actor, false);
        _lineaEnCurso = null;
    }

    // ── Un personaje ────────────────────────────────────────────────────────────────────────

    private sealed class Interprete
    {
        public readonly ActorHorneado Datos;
        public readonly SequenceActor Actor;
        private NavMeshAgent _agente;
        private bool _agenteEstabaActivo;
        private bool _manualMovimiento, _manualGiro;
        private int _iAnim, _iCara, _iMirada;
        private float _rumbo;
        private bool _moviendo;
        private bool _visible = true;
        private Renderer[] _renderers;
        private bool _hablando;

        private const float GiroAndando = 360f;   // grados por segundo
        private const float GiroQuieto = 220f;

        public Interprete(ActorHorneado datos, SequenceActor actor)
        {
            Datos = datos;
            Actor = actor;
        }

        public void Tomar()
        {
            var t = Actor.Transform;
            _agente = Actor.Agent;
            if (_agente != null)
            {
                _agenteEstabaActivo = _agente.enabled;
                if (_agente.enabled && _agente.isOnNavMesh) _agente.ResetPath();
                _agente.enabled = false;
            }
            var npc = Actor.NpcAnimator;
            if (npc != null)
            {
                _manualMovimiento = npc.AllowManualMovement;
                _manualGiro = npc.AllowManualRotation;
                npc.AllowManualMovement = true;
                npc.AllowManualRotation = true;
                npc.DisableAutoRotation();
                npc.ResetMovement();
            }
            Actor.ReleasePose(true);
            _renderers = t.GetComponentsInChildren<Renderer>(true);

            if (Datos.posiciones.Count > 0) t.position = Datos.posiciones[0].p;
            _rumbo = RumboInicial();
            t.rotation = Quaternion.Euler(0f, _rumbo, 0f);
            Actor.SyncRotation();
            AplicarVisible(EsVisible(0f));
        }

        public void Soltar()
        {
            if (Actor?.Transform == null) return;
            if (_hablando) { Actor.SetTalking(false); Actor.EndInteraction(); _hablando = false; }
            AplicarVisible(true);
            var npc = Actor.NpcAnimator;
            if (npc != null)
            {
                npc.ResetMovement();
                npc.AllowManualMovement = _manualMovimiento;
                npc.AllowManualRotation = _manualGiro;
                npc.EnableAutoRotation();
                npc.SyncTargetRotation();
            }
            if (_agente != null && _agenteEstabaActivo)
            {
                _agente.enabled = true;
                if (_agente.isOnNavMesh) _agente.Warp(Actor.Transform.position);
                else if (NavMesh.SamplePosition(Actor.Transform.position, out var hit, 2f, NavMesh.AllAreas))
                    _agente.Warp(hit.position);
            }
        }

        public void SaltarHasta(float t)
        {
            // Solo el último estado de animación y de cara que tocaba en t.
            string bucle = null;
            while (_iAnim < Datos.animacion.Count && Datos.animacion[_iAnim].t < t)
            {
                var o = Datos.animacion[_iAnim++];
                if (o.tipo == TipoDeAnimacion.Bucle) bucle = o.estado;
                else if (o.tipo == TipoDeAnimacion.Reposo) bucle = null;
            }
            if (bucle != null) Actor.HoldPose(bucle);
            NPCEmotion cara = NPCEmotion.None;
            while (_iCara < Datos.caras.Count && Datos.caras[_iCara].t < t) cara = Datos.caras[_iCara++].emocion;
            if (cara != NPCEmotion.None) Actor.SetEmotion(cara);
            if (Datos.posiciones.Count > 0) Actor.Transform.position = Interpolar.Posicion(Datos.posiciones, t);
        }

        public void Actualizar(float t, Dictionary<string, Interprete> todos)
        {
            var tr = Actor.Transform;
            if (tr == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

            bool visible = EsVisible(t);
            if (visible != _visible) AplicarVisible(visible);

            // Órdenes de animación y cara que tocan ya.
            while (_iAnim < Datos.animacion.Count && Datos.animacion[_iAnim].t <= t)
                Ejecutar(Datos.animacion[_iAnim++]);
            while (_iCara < Datos.caras.Count && Datos.caras[_iCara].t <= t)
            {
                var c = Datos.caras[_iCara++];
                if (c.dura > 0f) Actor.Reaccionar(c.emocion, c.dura);
                else Actor.SetEmotion(c.emocion);
            }
            while (_iMirada + 1 < Datos.miradas.Count && Datos.miradas[_iMirada + 1].t <= t) _iMirada++;

            // Posición: la del camino horneado, sin más.
            Vector3 velocidad = Vector3.zero;
            if (Datos.posiciones.Count > 0)
            {
                tr.position = Interpolar.Posicion(Datos.posiciones, t);
                velocidad = Interpolar.Velocidad(Datos.posiciones, t);
            }
            float rapidez = velocidad.magnitude;
            bool desliza = EnTramo(Datos.deslizando, t);
            bool moviendo = rapidez > 0.08f && !desliza;
            var npc = Actor.NpcAnimator;
            // Al echar a andar se corta el gesto que hubiera: si no, se desliza con él puesto (INC-602).
            if (moviendo && !_moviendo && npc != null && npc.IsPlayingAnimation())
                npc.CortarGestoParaAndar();
            if (npc != null) npc.SetMovementSpeed(moviendo ? NavMeshAgentUtility.FactorDeLocomocion(rapidez) : 0f);
            if (moviendo != _moviendo)
            {
                _moviendo = moviendo;
                if (!moviendo && npc != null) npc.ResetMovement();
            }

            // Giro: andando mira a donde va; quieto, a lo que diga su mirada.
            float objetivo = _rumbo;
            if (moviendo) objetivo = Mathf.Atan2(velocidad.x, velocidad.z) * Mathf.Rad2Deg;
            else if (Datos.miradas.Count > 0 && Datos.miradas[_iMirada].t <= t)
                objetivo = RumboDeMirada(Datos.miradas[_iMirada], tr.position, todos, _rumbo);
            _rumbo = Mathf.MoveTowardsAngle(_rumbo, objetivo, (moviendo ? GiroAndando : GiroQuieto) * dt);
            tr.rotation = Quaternion.Euler(0f, _rumbo, 0f);
            Actor.SyncRotation();
        }

        private void Ejecutar(OrdenDeAnimacion o)
        {
            switch (o.tipo)
            {
                case TipoDeAnimacion.Reposo:
                    if (_hablando) { Actor.SetTalking(false); Actor.EndInteraction(); _hablando = false; }
                    Actor.ReleasePose(true);
                    break;
                case TipoDeAnimacion.Bucle:
                    Actor.HoldPose(o.estado, o.congelar);
                    break;
                case TipoDeAnimacion.Gesto:
                    Actor.PlayGesture(o.estado);
                    break;
                case TipoDeAnimacion.HablaEmpieza:
                    Actor.BeginInteraction();
                    Actor.SetTalking(true);
                    _hablando = true;
                    break;
                case TipoDeAnimacion.HablaTermina:
                    if (_hablando) { Actor.SetTalking(false); Actor.EndInteraction(); _hablando = false; }
                    break;
            }
        }

        private float RumboInicial()
        {
            if (Datos.miradas.Count > 0 && Datos.miradas[0].tipo == TipoDeMirada.Rumbo) return Datos.miradas[0].rumbo;
            if (Datos.miradas.Count > 0 && Datos.miradas[0].tipo == TipoDeMirada.Punto && Datos.posiciones.Count > 0)
            {
                Vector3 d = Datos.miradas[0].punto - Datos.posiciones[0].p;
                if (d.sqrMagnitude > 0.01f) return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            return Actor.Transform.eulerAngles.y;
        }

        private static float RumboDeMirada(ClaveDeMirada m, Vector3 desde, Dictionary<string, Interprete> todos, float actual)
        {
            Vector3 punto;
            switch (m.tipo)
            {
                case TipoDeMirada.Rumbo: return m.rumbo;
                case TipoDeMirada.Actor:
                    if (!todos.TryGetValue(m.actor, out var otro) || otro.Actor?.Transform == null) return actual;
                    punto = otro.Actor.Transform.position;
                    break;
                default: punto = m.punto; break;
            }
            Vector3 d = punto - desde;
            d.y = 0f;
            return d.sqrMagnitude < 0.04f ? actual : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private static bool EnTramo(List<TramoVisible> tramos, float t)
        {
            if (tramos == null) return false;
            foreach (var v in tramos) if (t >= v.desde && t < v.hasta) return true;
            return false;
        }

        private bool EsVisible(float t)
        {
            if (Datos.visible == null || Datos.visible.Count == 0) return true;
            foreach (var v in Datos.visible) if (t >= v.desde && t < v.hasta) return true;
            return false;
        }

        private void AplicarVisible(bool visible)
        {
            _visible = visible;
            if (_renderers == null) return;
            // forceRenderingOff y no enabled: las caras encienden y apagan sus mallas con enabled,
            // y esconder así no les pisa nada.
            foreach (var r in _renderers) if (r != null) r.forceRenderingOff = !visible;
        }
    }
}
