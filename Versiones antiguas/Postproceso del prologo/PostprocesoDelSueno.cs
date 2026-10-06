using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// El «look» de sueño del prólogo (INC-422).
///
/// Va en el mismo GameObject que el Volume global de Prologo_Valle («POSTPROCESO_SUENO»), así que
/// existe exactamente mientras la escena del sueño está cargada: al descargarse el valle, el
/// despertar en la habitación de Will vuelve solo al aspecto normal del juego.
///
/// Dos cosas que un Volume no puede hacer solo:
///   · Que la cámara que rueda tenga el post-procesado ENCENDIDO. La del prólogo es la del
///     gameplay (vThirdPersonCamera), que vive en MainWorld; si la tiene apagado, el Volume no se
///     ve. Se enciende mientras dura el sueño y se deja como estaba al acabar.
///   · Entrar y salir fundido, no de golpe.
[DisallowMultipleComponent]
public class PostprocesoDelSueno : MonoBehaviour
{
    [Tooltip("Segundos que tarda el sueño en aparecer al cargarse la escena.")]
    [SerializeField] private float entrada = 1.5f;

    private Volume _volume;
    private VolumeProfile _perfilOriginal;
    private VolumeProfile _perfilDeJuego;
    private readonly Dictionary<UniversalAdditionalCameraData, bool> _estabanEncendidas = new();
    private float _siguienteRevision;

    void OnEnable()
    {
        _volume = GetComponent<Volume>();
        PrepararPerfilDeJuego();
        if (_volume != null) _volume.weight = entrada > 0.01f ? 0f : 1f;
        _siguienteRevision = 0f;
    }

    void PrepararPerfilDeJuego()
    {
        if (_volume == null || _volume.sharedProfile == null) return;

        // La copia limita los ajustes al sueño y conserva intacto el perfil compartido del asset.
        _perfilOriginal = _volume.sharedProfile;
        _perfilDeJuego = Instantiate(_perfilOriginal);
        _perfilDeJuego.name = _perfilOriginal.name + " (Sueño en juego)";
        _perfilDeJuego.hideFlags = HideFlags.DontSave;
        _perfilDeJuego.components.Clear();
        foreach (var componenteOriginal in _perfilOriginal.components)
        {
            if (componenteOriginal == null) continue;
            var copia = Instantiate(componenteOriginal);
            copia.hideFlags = HideFlags.DontSave;
            _perfilDeJuego.components.Add(copia);
        }
        _volume.sharedProfile = _perfilDeJuego;

        if (_perfilDeJuego.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.Override(0.42f);
            bloom.threshold.Override(0.9f);
            bloom.tint.Override(new Color(0.94f, 0.97f, 1f));
        }

        if (_perfilDeJuego.TryGet<Vignette>(out var vignette))
        {
            vignette.color.Override(new Color(0.28f, 0.2f, 0.42f));
            vignette.intensity.Override(0.08f);
            vignette.smoothness.Override(0.8f);
        }

        if (_perfilDeJuego.TryGet<DepthOfField>(out var depthOfField))
        {
            depthOfField.gaussianStart.Override(50f);
            depthOfField.gaussianEnd.Override(150f);
            depthOfField.gaussianMaxRadius.Override(0.35f);
        }

        if (_perfilDeJuego.TryGet<ColorAdjustments>(out var color))
        {
            color.postExposure.Override(0.1f);
            color.contrast.Override(-5f);
            color.saturation.Override(7f);
            color.colorFilter.Override(new Color(0.97f, 0.98f, 1f));
        }

        // Quibli guarda estos parámetros en otro ensamblado; se ajustan una sola vez al entrar.
        AjustarQuibli("StylizedDetail", "intensity", 0.1f);
        AjustarQuibli("StylizedDetail", "rangeStart", 22f);
        AjustarQuibli("StylizedDetail", "rangeEnd", 55f);
        AjustarQuibli("ColorGrading", "intensity", 0.22f);
        AjustarQuibli("ColorGrading", "blueShadows", 0.18f);
        AjustarQuibli("ColorGrading", "greenShadows", 0.03f);
        AjustarQuibli("ColorGrading", "redHighlights", 0.12f);
        AjustarQuibli("ColorGrading", "contrast", 0.01f);
        AjustarQuibli("ColorGrading", "vibrance", 0.16f);
        AjustarQuibli("ColorGrading", "saturation", 0.08f);
    }

    void AjustarQuibli(string tipo, string campo, float valor)
    {
        if (_perfilDeJuego == null) return;

        foreach (var componente in _perfilDeJuego.components)
        {
            if (componente == null || componente.GetType().Name != tipo) continue;
            var campoInfo = componente.GetType().GetField(campo, BindingFlags.Public | BindingFlags.Instance);
            if (campoInfo?.GetValue(componente) is VolumeParameter<float> parametro)
                parametro.Override(valor);
            return;
        }
    }

    void Update()
    {
        if (_volume != null && _volume.weight < 1f)
            _volume.weight = Mathf.MoveTowards(_volume.weight, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, entrada));

        // La cámara que rueda puede cambiar (la del gameplay, la de un sequencer...): se mira dos
        // veces por segundo, no cada fotograma.
        if (Time.unscaledTime < _siguienteRevision) return;
        _siguienteRevision = Time.unscaledTime + 0.5f;

        foreach (var cam in Camera.allCameras)
        {
            if (cam == null || cam.cameraType != CameraType.Game || cam.orthographic) continue;
            var datos = cam.GetUniversalAdditionalCameraData();
            if (datos == null || datos.renderPostProcessing) continue;
            if (!_estabanEncendidas.ContainsKey(datos)) _estabanEncendidas[datos] = false;
            datos.renderPostProcessing = true;
        }
    }

    void OnDisable()
    {
        foreach (var par in _estabanEncendidas)
            if (par.Key != null) par.Key.renderPostProcessing = par.Value;
        _estabanEncendidas.Clear();

        if (_volume != null && _volume.sharedProfile == _perfilDeJuego)
            _volume.sharedProfile = _perfilOriginal;
        if (_perfilDeJuego != null)
        {
            foreach (var componente in _perfilDeJuego.components)
                if (componente != null) Destroy(componente);
            Destroy(_perfilDeJuego);
            _perfilDeJuego = null;
        }
        _perfilOriginal = null;
    }
}
