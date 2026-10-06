using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Controla los perfiles de una escena y restaura los volúmenes y cámaras al salir.
[DisallowMultipleComponent, RequireComponent(typeof(Volume))]
public sealed class PostprocesoDeEscena : MonoBehaviour
{
    public VolumeProfile perfilInicial;
    public bool exclusivo = true;
    [Min(0)] public float entrada = 1.5f;
    public static PostprocesoDeEscena Activo { get; private set; }
    public bool EnTransicion => _tiempo < _duracion;

    private Volume _base, _a, _b;
    private float _pesoOriginal, _tiempo, _duracion, _siguienteRevision, _entradaActual;
    private readonly Dictionary<Volume, float> _pesos = new();
    private readonly Dictionary<UniversalAdditionalCameraData, bool> _camaras = new();
    private Camera[] _buffer = new Camera[16];

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Activo = null;
#endif

    private void Awake() => _base = GetComponent<Volume>();

    private Volume Crear(string nombre)
    {
        var go = new GameObject(nombre);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = _base.priority;
        v.weight = 0;
        return v;
    }

    private void OnEnable()
    {
        // Una sola instancia posee la exclusividad; la anterior libera sus recursos primero.
        if (Activo != null && Activo != this) Activo.enabled = false;
        Activo = this;
        _pesoOriginal = _base.weight;
        _base.weight = 0;
        if (_a == null) _a = Crear("Postproceso A");
        if (_b == null) _b = Crear("Postproceso B");
        _a.priority = _base.priority;
        _b.priority = _base.priority + 0.01f;
        _a.enabled = _b.enabled = true;
        _a.sharedProfile = perfilInicial != null ? perfilInicial : _base.sharedProfile;
        _b.sharedProfile = null;
        _tiempo = _duracion = 0;
        _entradaActual = entrada > 0 ? 0 : 1;
        AplicarPesos();
        Revisar();
    }

    public void CambiarPerfil(VolumeProfile perfil, float segundos)
    {
        if (!isActiveAndEnabled || perfil == null) return;
        // Una interrupción conserva el destino dominante y reutiliza los mismos dos volúmenes.
        if (EnTransicion && _tiempo >= _duracion * 0.5f) Intercambiar();
        _tiempo = _duracion = 0;
        if (_a.sharedProfile == perfil) { AplicarPesos(); return; }
        _b.sharedProfile = perfil;
        _duracion = Mathf.Max(0, segundos);
        if (_duracion == 0) Intercambiar();
        AplicarPesos();
    }

    private void Intercambiar()
    {
        float prioridadAnterior = _a.priority;
        _a.priority = _b.priority;
        _b.priority = prioridadAnterior;
        var anterior = _a;
        _a = _b;
        _b = anterior;
    }

    private void AplicarPesos()
    {
        float t = EnTransicion ? Mathf.Clamp01(_tiempo / _duracion) : 0;
        // A establece el perfil completo y B se aplica después: lerp(A, B, t) al terminar la entrada.
        _a.weight = _entradaActual;
        _b.weight = t * _entradaActual;
    }

    private void Update()
    {
        _entradaActual = entrada > 0 ? Mathf.MoveTowards(_entradaActual, 1, Time.unscaledDeltaTime / entrada) : 1;
        if (EnTransicion)
        {
            _tiempo += Time.unscaledDeltaTime;
            if (!EnTransicion) { Intercambiar(); _tiempo = _duracion = 0; }
        }
        AplicarPesos();
        if (Time.unscaledTime >= _siguienteRevision) Revisar();
    }

    private void Revisar()
    {
        _siguienteRevision = Time.unscaledTime + 0.5f;
        // El registro incluye los volúmenes persistentes y los de escenas aditivas.
        // Su pequeño array cada medio segundo evita recorrer las jerarquías de todo el mundo.
        if (exclusivo)
        {
            foreach (var v in VolumeManager.instance.GetVolumes(~0)) Silenciar(v);
            foreach (var par in _pesos) if (par.Key != null) par.Key.weight = 0;
        }
        else RestaurarPesos();
        int cantidad = Camera.allCamerasCount;
        if (_buffer.Length < cantidad) _buffer = new Camera[Mathf.NextPowerOfTwo(cantidad)];
        cantidad = Camera.GetAllCameras(_buffer);
        for (int i = 0; i < cantidad; i++)
        {
            var cam = _buffer[i];
            if (cam == null || cam.cameraType != CameraType.Game) continue;
            var datos = cam.GetUniversalAdditionalCameraData();
            if (!_camaras.ContainsKey(datos)) _camaras.Add(datos, datos.renderPostProcessing);
            datos.renderPostProcessing = true;
        }
    }

    private void Silenciar(Volume v)
    {
        if (v == null || !v.isGlobal || v == _base || v == _a || v == _b) return;
        if (!_pesos.ContainsKey(v)) _pesos.Add(v, v.weight);
        v.weight = 0;
    }

    private void RestaurarPesos()
    {
        foreach (var par in _pesos) if (par.Key != null) par.Key.weight = par.Value;
        _pesos.Clear();
    }

    private void OnDisable()
    {
        RestaurarPesos();
        foreach (var par in _camaras) if (par.Key != null) par.Key.renderPostProcessing = par.Value;
        _camaras.Clear();
        if (_base != null) _base.weight = _pesoOriginal;
        if (_a != null) { _a.weight = 0; _a.enabled = false; }
        if (_b != null) { _b.weight = 0; _b.enabled = false; }
        _tiempo = _duracion = 0;
        if (Activo == this) Activo = null;
    }
}
