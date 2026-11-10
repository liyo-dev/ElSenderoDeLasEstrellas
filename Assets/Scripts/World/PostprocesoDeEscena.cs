using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Controla los perfiles de una escena y restaura los volúmenes y cámaras al salir.
[DisallowMultipleComponent, RequireComponent(typeof(Volume))]
public sealed class PostprocesoDeEscena : MonoBehaviour
{
    public VolumeProfile perfilInicial;
    public bool exclusivo = true;
    [Min(0)] public float entrada = 1.5f;
    [Tooltip("Solo manda mientras su escena es la activa. Para escenas que se cargan en aditivo para " +
             "una cinemática: el SequencePlayer las marca activas en el corte, con la pantalla cubierta, " +
             "y devuelve la anterior al terminar. Sin esto, el post-procesado de la escena se pone sobre " +
             "lo que se está viendo nada más cargarla.")]
    public bool soloConSuEscenaActiva;
    private static PostprocesoDeEscena s_activo;
    public static PostprocesoDeEscena Activo
    {
        get
        {
            if (s_activo == null || !s_activo.isActiveAndEnabled || !s_activo._mandando)
                s_activo = BuscarElQueManda();
            return s_activo;
        }
        private set => s_activo = value;
    }

    // Uno que espera a que su escena sea la activa está encendido pero no manda: no cuenta.
    private static PostprocesoDeEscena BuscarElQueManda()
    {
        foreach (var p in Object.FindObjectsByType<PostprocesoDeEscena>(FindObjectsSortMode.None))
            if (p._mandando) return p;
        return null;
    }
    public bool EnTransicion => _tiempo < _duracion;

    private Volume _base, _a, _b;
    private bool _mandando;
    private float _pesoOriginal, _tiempo, _duracion, _siguienteRevision, _entradaActual;
    private readonly Dictionary<Volume, float> _pesos = new();
    private readonly Dictionary<UniversalAdditionalCameraData, bool> _camaras = new();
    private Camera[] _buffer = new Camera[16];

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_activo = null;
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
        _pesoOriginal = _base.weight;
        _base.weight = 0;
        if (soloConSuEscenaActiva)
        {
            SceneManager.activeSceneChanged += AlCambiarLaEscenaActiva;
            if (SceneManager.GetActiveScene() != gameObject.scene) return;
        }
        Tomar();
    }

    private void AlCambiarLaEscenaActiva(Scene anterior, Scene nueva)
    {
        if (nueva == gameObject.scene) Tomar();
        else Soltar();
    }

    private void Tomar()
    {
        if (_mandando) return;
        // Una sola instancia posee la exclusividad; la anterior libera sus recursos primero.
        var anterior = Activo;
        if (anterior != null && anterior != this) anterior.enabled = false;
        _mandando = true;
        Activo = this;
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
        if (!isActiveAndEnabled || !_mandando || perfil == null) return;
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
        if (!_mandando) return;
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
            // Las cámaras que no escuchan ningún volumen (retratos e iconos del HUD) se quedan sin
            // postproceso: con él, su fondo deja de ser negro o blanco puro y el retrato saca un
            // cuadro oscuro detrás del personaje. Ver INC-955.
            if (datos.volumeLayerMask == 0) continue;
            if (!_camaras.ContainsKey(datos)) _camaras.Add(datos, datos.renderPostProcessing);
            datos.renderPostProcessing = true;
        }
    }

    private void Silenciar(Volume v)
    {
        if (v == null || !v.isGlobal || v == _base || v == _a || v == _b) return;
        if (PostprocesoDeCinematica.EsSuVolumen(v)) return;
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
        if (soloConSuEscenaActiva) SceneManager.activeSceneChanged -= AlCambiarLaEscenaActiva;
        Soltar();
        if (_base != null) _base.weight = _pesoOriginal;
    }

    private void Soltar()
    {
        if (!_mandando) return;
        _mandando = false;
        if (s_activo == this) s_activo = null;
        RestaurarPesos();
        foreach (var par in _camaras) if (par.Key != null) par.Key.renderPostProcessing = par.Value;
        _camaras.Clear();
        if (_a != null) { _a.weight = 0; _a.enabled = false; }
        if (_b != null) { _b.weight = 0; _b.enabled = false; }
        _tiempo = _duracion = 0;
    }
}
