using System;
using System.Collections;
using Kits.WorldWrap;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Nube ambiental suelta que cruza el cielo de un lado a otro. Está SIEMPRE en movimiento: nace lejos del jugador, deriva hacia/más allá de él y sigue
/// alejándose mientras se disipa — nunca aparece ni desaparece de golpe delante de la cámara.
///
/// La instancia solo se entrega de vuelta al pool de <see cref="AmbientCloudDirector"/> (vía el
/// callback de <see cref="Play"/>) cuando el fundido de salida ya ha terminado del todo: para ese
/// momento la nube lleva recorrida toda su ruta y está lejos del punto de paso, así que el
/// SetActive(false) que la recicla no se nota nunca.
///
/// En un mundo que da la vuelta (WorldWrapper), su ruta se mueve con el mundo al cruzar el borde, para
/// que la nube siga en el mismo sitio del cielo que ve el jugador.
/// </summary>
[DisallowMultipleComponent]
public class AmbientCloudDrifter : MonoBehaviour
{
    /// Cómo se funde cada tipo de material de nube.
    public enum CloudShaderMode { NubeDeDibujo, NubePlana2D, LegacyBaseColor }

    [Tooltip("Shader de este prefab de nube en concreto. NubeDeDibujo anima el recorte de alfa (_AlphaThreshold, efecto de materializarse/disiparse), NubePlana2D anima _Opacity, LegacyBaseColor anima el alfa de _BaseColor.")]
    [SerializeField] private CloudShaderMode shaderMode = CloudShaderMode.NubeDeDibujo;
    [Tooltip("Solo NubeDeDibujo: _AlphaThreshold cuando la nube está completamente formada (0.5 en Nube_Dibujo).")]
    [SerializeField, Range(0.05f, 1f)] private float visibleAlphaThreshold = 0.5f;

    private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Coroutine _routine;
    private Vector3 _start;
    private Vector3 _end;

    /// <summary>True mientras la corrutina de vuelo (nace -> cruza -> se disipa) está en marcha.</summary>
    public bool IsFlying => _routine != null;

    void Awake()
    {
        // Se cachea una sola vez: la instancia se reutiliza vía SetActive desde el pool del
        // director, nunca se vuelve a instanciar, así que GetComponentsInChildren no se repite.
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            _renderers[i].receiveShadows = false;
        }
    }

    void OnEnable() => WorldWrapper.Shifted += AlCruzarElMundo;
    void OnDisable() => WorldWrapper.Shifted -= AlCruzarElMundo;

    void AlCruzarElMundo(Vector3 desplazamiento)
    {
        _start += desplazamiento;
        _end += desplazamiento;
        transform.position += desplazamiento;
    }

    /// <summary>
    /// Lanza el vuelo en línea recta de <paramref name="start"/> a <paramref name="end"/> a
    /// <paramref name="speed"/> unidades/seg, con fundido de entrada/salida de
    /// <paramref name="fadeIn"/>/<paramref name="fadeOut"/> segundos. <paramref name="onFinished"/>
    /// se invoca SOLO al terminar el fundido de salida (nube ya invisible y lejos) — es la señal
    /// para que el director la recicle.
    /// </summary>
    public void Play(Vector3 start, Vector3 end, float speed, float fadeIn, float fadeOut, Action<AmbientCloudDrifter> onFinished)
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _start = start;
        _end = end;
        transform.position = start;
        transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        _routine = StartCoroutine(FlyRoutine(speed, fadeIn, fadeOut, onFinished));
    }

    IEnumerator FlyRoutine(float speed, float fadeIn, float fadeOut, Action<AmbientCloudDrifter> onFinished)
    {
        float distance = Vector3.Distance(_start, _end);
        float travelTime = distance / Mathf.Max(0.01f, speed);
        // Si la ruta es corta para la velocidad dada, los fundidos se recortan para que quepan
        // (con margen) y nunca se solapen dejando la nube siempre a medio formar.
        fadeIn = Mathf.Min(fadeIn, travelTime * 0.4f);
        fadeOut = Mathf.Min(fadeOut, travelTime * 0.4f);
        float fadeOutStart = travelTime - fadeOut;

        ApplyAlpha(0f);

        float elapsed = 0f;
        while (elapsed < travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            transform.position = Vector3.Lerp(_start, _end, t);

            float alpha;
            if (elapsed < fadeIn)
                alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeIn));
            else if (elapsed > fadeOutStart)
                alpha = Mathf.Clamp01(1f - (elapsed - fadeOutStart) / Mathf.Max(0.01f, fadeOut));
            else
                alpha = 1f;

            ApplyAlpha(alpha);
            yield return null;
        }

        ApplyAlpha(0f);
        _routine = null;
        onFinished?.Invoke(this);
    }

    void ApplyAlpha(float alpha)
    {
        float alphaThreshold = Mathf.Lerp(1.01f, visibleAlphaThreshold, alpha);
        Color legacyColor = Color.white;
        legacyColor.a = alpha;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            switch (shaderMode)
            {
                case CloudShaderMode.NubeDeDibujo:
                    _mpb.SetFloat(AlphaThresholdId, alphaThreshold);
                    break;
                case CloudShaderMode.NubePlana2D:
                    _mpb.SetFloat(OpacityId, alpha);
                    break;
                default:
                    _mpb.SetColor(BaseColorId, legacyColor);
                    break;
            }
            r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Corta el vuelo en seco (p.ej. descarga de escena). No es la ruta normal de reciclado.</summary>
    public void Cancel()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    /// <summary>
    /// Activa/desactiva SOLO los renderers, sin tocar la corrutina de vuelo (FlyRoutine sigue
    /// moviendo/animando la nube igual). Pensado para ocultarla un momento durante un enfoque de
    /// cámara puntual (ver FocusCameraNode) sin cortar su ruta ni su ciclo de fundido/reciclado:
    /// SetActive(false) mataría la corrutina (Unity para las corrutinas de un GameObject
    /// desactivado y no las reanuda solas), esto no.
    /// </summary>
    public void SetRenderersVisible(bool visible)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
    }
}
