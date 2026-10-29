using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Mecánica de combate: El Mar del Abismo / Suelo Impisable.
/// Inunda el suelo de la arena con oscuridad corrupta y fuerza al jugador a volar.
/// Pisar o descender al suelo mientras está activo causa daño letal abrasador a quien del grupo
/// lo toque (DanoEnZona; cada personaje con su vida, GDD § 6.1).
/// </summary>
public sealed class SueloImpisable : MonoBehaviour
{
    [Header("Parámetros")]
    public float radio = 22f;
    public float alturaSuelo = 0f;
    public float margenSeguridad = 0.8f;
    public float danoPorSegundo = 200f;

    [Header("Efectos")]
    public GameObject vfxSuelo;
    public string sfxSueloActivo = "SFX_Prologo_AgujeroNegro_Carga";

    private bool _activo;
    private Transform _jugador;
    private PlayerFlyingController _vueloJugador;
    private GameObject _marVisual;
    private float _ultimoDano;

    // Segundos entre golpes del suelo; cada golpe quita danoPorSegundo × este intervalo.
    private const float IntervaloDeDano = 0.2f;
    // Margen horizontal más allá del radio en el que el suelo aún quema.
    private const float MargenDeRadio = 2f;

    public static SueloImpisable Activar(Vector3 centro, float radio, GameObject vfxMar = null)
    {
        var go = new GameObject("SueloImpisable");
        go.transform.position = centro;
        var comp = go.AddComponent<SueloImpisable>();
        comp.radio = radio;
        comp.alturaSuelo = centro.y;
        comp.vfxSuelo = vfxMar;
        comp.Iniciar();
        return comp;
    }

    public void Iniciar()
    {
        _activo = true;

        if (PlayerService.TryGetPlayer(out var j) && j != null)
        {
            _jugador = j.transform;
            _vueloJugador = j.GetComponent<PlayerFlyingController>();
        }

        // Forzar al jugador a volar inmediatamente
        if (_vueloJugador != null)
        {
            _vueloJugador.ForzarVuelo(5.5f);
        }

        // Crear plano visual de mar de sombras
        CrearVisualDeSombra();

        // Feedback
        FeedbackService.ScreenFlash(new Color(0.2f, 0.05f, 0.3f, 0.6f), 0.4f);
        FeedbackService.CameraShake(0.6f, 0.5f);
        FeedbackService.Vibrar(0.6f, 0.6f, 0.5f);

        if (!string.IsNullOrEmpty(sfxSueloActivo) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxSueloActivo, 1f, transform.position);
    }

    public void Desactivar()
    {
        _activo = false;
        if (_marVisual != null) Destroy(_marVisual);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!_activo) return;

        // Si el jugador no está volando y toca el suelo, impulsarlo hacia arriba para recordarle volar
        if (_jugador != null && _vueloJugador != null && !_vueloJugador.IsFlying && TocaElSuelo(_jugador.position))
            _vueloJugador.ForzarVuelo(4.0f);

        // Daño letal continuo a quien del grupo toque el suelo corrompido
        if (Time.time - _ultimoDano < IntervaloDeDano) return;
        _ultimoDano = Time.time;
        int n = DanoEnZona.CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < n; i++)
        {
            var c = cuerpos[i];
            if (!TocaElSuelo(c.Posicion)) continue;
            if (DanoEnZona.Danar(c, danoPorSegundo * IntervaloDeDano) && c.EsElJugador)
            {
                FeedbackService.CameraShake(0.4f, 0.15f);
                FeedbackService.Vibrar(0.5f, 0.5f, 0.2f);
            }
        }
    }

    /// Un punto está sobre la arena y tocando (o rozando) la superficie corrupta.
    private bool TocaElSuelo(Vector3 punto)
    {
        if (punto.y > alturaSuelo + margenSeguridad) return false;
        Vector3 dif = punto - transform.position;
        dif.y = 0f;
        float alcance = radio + MargenDeRadio;
        return dif.sqrMagnitude <= alcance * alcance;
    }

    private void CrearVisualDeSombra()
    {
        if (vfxSuelo != null)
        {
            _marVisual = Instantiate(vfxSuelo, transform.position + Vector3.up * 0.1f, Quaternion.identity, transform);
            _marVisual.transform.localScale = Vector3.one * (radio / 10f);
            return;
        }

        // Fallback visual: cilindro plano oscuro con emisión morada
        _marVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _marVisual.name = "MarDeSombras";
        _marVisual.transform.SetParent(transform, false);
        _marVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        _marVisual.transform.localScale = new Vector3(radio * 2.05f, 0.05f, radio * 2.05f);
        Destroy(_marVisual.GetComponent<Collider>());

        var ren = _marVisual.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.color = new Color(0.08f, 0.02f, 0.15f, 0.95f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.45f, 0.05f, 0.7f) * 1.8f);
            }
            ren.sharedMaterial = mat;
        }
    }

    private void OnDestroy()
    {
        if (_marVisual != null) Destroy(_marVisual);
    }
}
