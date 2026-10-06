using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// Las cámaras de un guion, con Cinemachine.
///
/// Dos cámaras virtuales que se turnan (A y B): cada plano nuevo se monta en la que no está en
/// pantalla y pasa a mandar, con corte seco o con una mezcla de los segundos que diga el plano.
///
/// La POSICIÓN de la cámara es la horneada (quieta, o un travelling ya suavizado al hornear). La
/// ROTACIÓN la pone un compositor de Cinemachine que mira al centro de los sujetos del plano (un
/// TargetGroup), con amortiguación: si alguien se mueve un poco, la cámara le acompaña con una
/// panorámica suave en vez de dar tirones. Nada se recalcula en directo, así que no hay saltos.
public sealed class CamarasDeGuion : Sendero.Core.Feedback.ICameraShakeProvider
{
    private readonly GuionHorneado _g;
    private readonly Dictionary<string, Transform> _transformPorId = new();

    private Camera _camara;
    private CinemachineBrain _brain;
    private bool _brainCreado;
    private bool _brainEstabaActivo;
    private CinemachineBlendDefinition _mezclaPrevia;
    private CinemachineBrain.UpdateMethods _updatePrevio;
    private bool _ignorarTiempoPrevio;
    private float _fovPrevio;

    private GameObject _raiz;
    private readonly Equipo[] _equipos = new Equipo[2];
    private int _activo = -1;
    private int _prioridad = 100;
    private PlanoHorneado _plano;
    private float _sacudida, _sacudidaHasta, _sacudidaDura;

    private const float GananciaDeMano = 0.55f;

    private sealed class Equipo
    {
        public GameObject go;
        public CinemachineCamera cam;
        public CinemachineRotationComposer compositor;
        public CinemachineBasicMultiChannelPerlin ruido;
        public CinemachineTargetGroup grupo;
        public Transform focoFijo;
        public readonly List<Transform> extras = new();
    }

    public CamarasDeGuion(GuionHorneado guion, Dictionary<string, Transform> transformPorId)
    {
        _g = guion;
        if (transformPorId != null)
            foreach (var kv in transformPorId) if (kv.Value != null) _transformPorId[kv.Key] = kv.Value;
    }

    public void Preparar(Camera camara)
    {
        _camara = camara;
        if (_camara == null)
        {
            Debug.LogError("[Guion] No hay cámara principal: los planos no se van a ver.");
            return;
        }
        _fovPrevio = _camara.fieldOfView;
        _brain = _camara.GetComponent<CinemachineBrain>();
        _brainCreado = _brain == null;
        if (_brainCreado) _brain = _camara.gameObject.AddComponent<CinemachineBrain>();
        _brainEstabaActivo = _brain.enabled;
        _mezclaPrevia = _brain.DefaultBlend;
        _updatePrevio = _brain.UpdateMethod;
        _ignorarTiempoPrevio = _brain.IgnoreTimeScale;
        _brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        _brain.IgnoreTimeScale = true;
        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        _brain.enabled = true;

        _raiz = new GameObject("Guion · cámaras");
        for (int i = 0; i < 2; i++) _equipos[i] = CrearEquipo(i == 0 ? "A" : "B");
    }

    private Equipo CrearEquipo(string nombre)
    {
        var e = new Equipo();
        e.go = new GameObject("Cámara " + nombre);
        e.go.transform.SetParent(_raiz.transform, false);
        e.go.SetActive(false);

        var grupoGo = new GameObject("Foco " + nombre);
        grupoGo.transform.SetParent(_raiz.transform, false);
        e.grupo = grupoGo.AddComponent<CinemachineTargetGroup>();
        e.grupo.PositionMode = CinemachineTargetGroup.PositionModes.GroupAverage;
        e.grupo.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
        e.grupo.UpdateMethod = CinemachineTargetGroup.UpdateMethods.LateUpdate;

        var fijo = new GameObject("Punto fijo " + nombre);
        fijo.transform.SetParent(_raiz.transform, false);
        e.focoFijo = fijo.transform;

        e.cam = e.go.AddComponent<CinemachineCamera>();
        e.compositor = e.go.AddComponent<CinemachineRotationComposer>();
        e.compositor.CenterOnActivate = true;
        var composicion = ScreenComposerSettings.Default;
        composicion.DeadZone.Enabled = true;
        composicion.DeadZone.Size = new Vector2(0.04f, 0.04f);
        composicion.HardLimits.Enabled = true;
        composicion.HardLimits.Size = new Vector2(0.75f, 0.75f);
        e.compositor.Composition = composicion;
        e.ruido = e.go.AddComponent<CinemachineBasicMultiChannelPerlin>();
        e.ruido.NoiseProfile = _g.ruidoDeMano;
        e.ruido.AmplitudeGain = 0f;
        e.cam.LookAt = e.grupo.transform;
        return e;
    }

    public void Activar(PlanoHorneado plano)
    {
        if (_camara == null || _raiz == null) return;
        _plano = plano;
        int siguiente = _activo < 0 ? 0 : 1 - _activo;
        var e = _equipos[siguiente];

        // Foco: los sujetos del plano, o un punto fijo.
        e.grupo.Targets.Clear();
        for (int i = 0; i < plano.sujetos.Count; i++)
        {
            if (!_transformPorId.TryGetValue(plano.sujetos[i], out var t) || t == null) continue;
            float peso = i < plano.pesos.Count ? plano.pesos[i] : 1f;
            e.grupo.Targets.Add(new CinemachineTargetGroup.Target { Object = t, Weight = peso, Radius = 0.35f });
        }
        if (e.grupo.Targets.Count == 0)
        {
            e.focoFijo.position = plano.focoFijo;
            e.grupo.Targets.Add(new CinemachineTargetGroup.Target { Object = e.focoFijo, Weight = 1f, Radius = 0.5f });
            e.compositor.TargetOffset = Vector3.zero;
        }
        else
        {
            e.compositor.TargetOffset = new Vector3(0f, plano.alturaDelFoco, 0f);
            // Puntos del escenario que también tienen que verse (se descuenta la altura del foco,
            // que el compositor suma a todo el grupo).
            if (plano.extras != null)
                for (int i = 0; i < plano.extras.Count; i++)
                {
                    while (e.extras.Count <= i)
                    {
                        var go = new GameObject("Extra " + e.extras.Count);
                        go.transform.SetParent(_raiz.transform, false);
                        e.extras.Add(go.transform);
                    }
                    e.extras[i].position = plano.extras[i] - Vector3.up * plano.alturaDelFoco;
                    e.grupo.Targets.Add(new CinemachineTargetGroup.Target { Object = e.extras[i], Weight = 1f, Radius = 0.5f });
                }
        }
        e.grupo.DoUpdate();

        var composicion = e.compositor.Composition;
        composicion.ScreenPosition = plano.encuadre;
        e.compositor.Composition = composicion;
        e.compositor.Damping = new Vector2(plano.suavidad, plano.suavidad * 0.8f);

        var lente = e.cam.Lens;
        lente.FieldOfView = plano.lente;
        e.cam.Lens = lente;
        e.ruido.AmplitudeGain = plano.mano * GananciaDeMano;
        e.ruido.FrequencyGain = 1f;

        e.go.transform.position = plano.PosicionEn(plano.t0);
        Vector3 haciaFoco = (e.grupo.Sphere.position + e.compositor.TargetOffset) - e.go.transform.position;
        if (haciaFoco.sqrMagnitude > 0.0001f) e.go.transform.rotation = Quaternion.LookRotation(haciaFoco, Vector3.up);

        _brain.DefaultBlend = plano.mezcla > 0.01f
            ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, plano.mezcla)
            : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

        e.go.SetActive(true);
        e.cam.Priority = ++_prioridad;
        e.cam.Prioritize();
        if (_activo < 0 || plano.mezcla <= 0.01f)
        {
            // En un corte la cámara saliente se apaga ya: así no queda nada que mezclar.
            e.cam.PreviousStateIsValid = false;
            if (_activo >= 0) _equipos[_activo].go.SetActive(false);
        }
        else _apagarTras = (1 - siguiente, Time.unscaledTime + plano.mezcla + 0.1f);
        _activo = siguiente;
    }

    private (int indice, float cuando) _apagarTras = (-1, 0f);

    public void Actualizar(float t)
    {
        if (_plano == null || _activo < 0) return;
        var e = _equipos[_activo];
        e.go.transform.position = _plano.PosicionEn(t);
        if (_plano.lenteFinal > 0.1f && _plano.t1 > _plano.t0)
        {
            var lente = e.cam.Lens;
            lente.FieldOfView = Mathf.Lerp(_plano.lente, _plano.lenteFinal,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_plano.t0, _plano.t1, t)));
            e.cam.Lens = lente;
        }

        float extra = 0f;
        if (Time.unscaledTime < _sacudidaHasta && _sacudidaDura > 0f)
            extra = _sacudida * Mathf.Clamp01((_sacudidaHasta - Time.unscaledTime) / _sacudidaDura);
        e.ruido.AmplitudeGain = _plano.mano * GananciaDeMano + extra;
        e.ruido.FrequencyGain = extra > 0.01f ? 6f : 1f;

        if (_apagarTras.indice >= 0 && Time.unscaledTime >= _apagarTras.cuando)
        {
            if (_apagarTras.indice != _activo) _equipos[_apagarTras.indice].go.SetActive(false);
            _apagarTras = (-1, 0f);
        }
    }

    public void Terminar()
    {
        if (_raiz != null) Object.Destroy(_raiz);
        _raiz = null;
        if (_brain != null)
        {
            if (_brainCreado) Object.Destroy(_brain);
            else
            {
                _brain.DefaultBlend = _mezclaPrevia;
                _brain.UpdateMethod = _updatePrevio;
                _brain.IgnoreTimeScale = _ignorarTiempoPrevio;
                _brain.enabled = _brainEstabaActivo;
            }
        }
        if (_camara != null) _camara.fieldOfView = _fovPrevio;
        _plano = null;
    }

    // ── Sacudidas: los ShakeBeat de los efectos llegan aquí mientras dura el guion ──────────

    public void Shake(MonoBehaviour runner, float intensity, float duration) => Sacudir(intensity, duration);
    public void Shake(MonoBehaviour runner, Camera targetCamera, float intensity, float duration) => Sacudir(intensity, duration);
    public void CancelAll() { _sacudidaHasta = 0f; }

    private void Sacudir(float intensidad, float duracion)
    {
        _sacudida = Mathf.Max(0f, intensidad) * 6f;
        _sacudidaDura = Mathf.Max(0.05f, duracion);
        _sacudidaHasta = Time.unscaledTime + _sacudidaDura;
    }
}
