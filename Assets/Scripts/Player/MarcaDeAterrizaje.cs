using Invector.vCharacterController;
using UnityEngine;

/// <summary>
/// La sombra de aterrizaje: mientras el jugador va por el aire, un disco se pega al suelo justo donde
/// va a caer. Es el truco que hace que un salto se sienta controlable: sin él vas a ciegas.
/// Se coloca en la escena (uno por nivel) y sigue al cuerpo que lleva el jugador.
/// </summary>
public sealed class MarcaDeAterrizaje : MonoBehaviour
{
    [Tooltip("Lo que se ve: el disco que se pega al suelo.")]
    [SerializeField] private Transform disco;
    [Tooltip("Altura por debajo de los pies a la que se apaga (m).")]
    [SerializeField, Min(0f)] private float alturaMinima = 0.35f;
    [Tooltip("Altura a la que la marca es más pequeña y tenue (m).")]
    [SerializeField, Min(1f)] private float alturaMaxima = 10f;
    [SerializeField, Min(0.1f)] private float radioCerca = 0.55f;
    [SerializeField, Min(0.1f)] private float radioLejos = 0.9f;
    [Tooltip("Si está activo, la marca se queda encendida también en el suelo (para ver el sitio exacto).")]
    [SerializeField] private bool tambienEnElSuelo;

    private vThirdPersonMotor _motor;
    private Renderer _pintura;
    private MaterialPropertyBlock _bloque;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _pintura = disco != null ? disco.GetComponentInChildren<Renderer>() : null;
        _bloque = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (disco == null) return;

        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo == null) { Apagar(); return; }
        if (_motor == null || _motor.transform != cuerpo.transform)
            _motor = cuerpo.GetComponent<vThirdPersonMotor>();

        bool enAire = _motor != null && _motor.IsAirborne;
        if (!enAire && !tambienEnElSuelo) { Apagar(); return; }

        Vector3 desde = cuerpo.transform.position + Vector3.up * 0.4f;
        if (!Physics.Raycast(desde, Vector3.down, out var golpe, 60f)) { Apagar(); return; }

        float altura = desde.y - golpe.point.y;
        if (altura < alturaMinima) { Apagar(); return; }

        float lejos = Mathf.InverseLerp(alturaMinima, alturaMaxima, altura);
        disco.position = golpe.point + Vector3.up * 0.03f;
        disco.rotation = Quaternion.FromToRotation(Vector3.up, golpe.normal);
        float radio = Mathf.Lerp(radioCerca, radioLejos, lejos);
        disco.localScale = new Vector3(radio, 0.02f, radio);
        Encender(1f - 0.45f * lejos);
    }

    private void Encender(float opacidad)
    {
        if (_pintura == null) return;
        _pintura.enabled = true;
        _pintura.GetPropertyBlock(_bloque);
        _bloque.SetColor(BaseColorId, new Color(0.05f, 0.05f, 0.07f, opacidad));
        _pintura.SetPropertyBlock(_bloque);
    }

    private void Apagar()
    {
        if (_pintura != null) _pintura.enabled = false;
    }
}
