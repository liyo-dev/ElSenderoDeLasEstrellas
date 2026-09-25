using System.Collections.Generic;
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
    private readonly Dictionary<UniversalAdditionalCameraData, bool> _estabanEncendidas = new();
    private float _siguienteRevision;

    void OnEnable()
    {
        _volume = GetComponent<Volume>();
        if (_volume != null) _volume.weight = entrada > 0.01f ? 0f : 1f;
        _siguienteRevision = 0f;
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
    }
}
