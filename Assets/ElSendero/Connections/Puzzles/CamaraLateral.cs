using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// La cámara del tramo lateral: se pone de lado, mira al plano del pasillo y sujeta al jugador dentro
/// de él. La pone y la quita el enchufe del kit (<see cref="EnchufeDePuzzlesDelJuego"/>), que es
/// quien sabe quién es el jugador.
///
/// Se suelta sola si el jugador deja de estar en la zona: los teletransportes del tramo (caída,
/// reintento) no disparan la salida del trigger, y sin esto la cámara se quedaba de lado para
/// siempre. También reclama el mando tarde si al entrar todavía no había cámara.
/// </summary>
public sealed class CamaraLateral : MonoBehaviour
{
    private SidePlaneZone _zona;
    private BoxCollider _caja;
    private Camera _camara;
    private bool _tieneLaCamara;
    private float _siguienteMira;

    public static CamaraLateral Poner(SidePlaneZone zona)
    {
        if (zona == null) return null;
        var camara = zona.GetComponent<CamaraLateral>();
        if (camara == null) camara = zona.gameObject.AddComponent<CamaraLateral>();
        camara._zona = zona;
        camara._caja = zona.GetComponent<BoxCollider>();
        return camara;
    }

    private void OnDisable() => Soltar();

    private void OnDestroy() => Soltar();

    private void Soltar()
    {
        if (!_tieneLaCamara) return;
        CameraDirectorService.Release(this);
        _tieneLaCamara = false;
    }

    private void Update()
    {
        if (_zona == null || Time.unscaledTime < _siguienteMira) return;
        _siguienteMira = Time.unscaledTime + 0.2f;

        var jugador = ControlDelJugador.CuerpoDelJugador;
        if (jugador == null) return;
        if (_caja == null) _caja = _zona.GetComponent<BoxCollider>();
        if (_caja == null) return;
        // Fuera de la zona (teletransporte, caída, cambio de tramo): se suelta y se quita.
        if (!_caja.bounds.Contains(jugador.transform.position)) Destroy(this);
    }

    private void LateUpdate()
    {
        if (_zona == null) return;
        if (_camara == null)
        {
            _camara = Camera.main;
            if (_camara == null) return;
        }
        if (!_tieneLaCamara)
        {
            CameraDirectorService.Claim(this);
            _tieneLaCamara = true;
        }

        var jugador = ControlDelJugador.CuerpoDelJugador;
        if (jugador == null) return;

        Vector3 normal = _zona.PlaneNormal;
        Vector3 pies = jugador.transform.position;
        Vector3 sitio = pies + normal * _zona.CameraDistance + Vector3.up * _zona.CameraHeight;
        _camara.transform.position = Vector3.Lerp(_camara.transform.position, sitio, _zona.CameraFollow * Time.deltaTime);

        Vector3 mira = _zona.EnElPlano(pies) + Vector3.up * 1.3f;
        Vector3 hacia = mira - _camara.transform.position;
        if (hacia.sqrMagnitude > 0.01f)
            _camara.transform.rotation = Quaternion.LookRotation(hacia.normalized, Vector3.up);
    }

    /// <summary>Devuelve al jugador a su plano si se ha salido (sin tocarle la altura).</summary>
    private void FixedUpdate()
    {
        if (_zona == null) return;
        var jugador = ControlDelJugador.CuerpoDelJugador;
        if (jugador == null) return;
        if (_zona.FueraDelPlano(jugador.transform.position) < 0.6f) return;
        jugador.transform.position = _zona.EnElPlano(jugador.transform.position);
    }
}
