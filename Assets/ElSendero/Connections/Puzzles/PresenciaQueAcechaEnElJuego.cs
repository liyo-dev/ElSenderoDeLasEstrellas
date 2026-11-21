using Kits.Puzzles;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Une la <see cref="StalkingPresence"/> del kit de puzles con el juego: el sujeto es el cuerpo
/// que lleva el jugador, lo que lleva lo dice su <see cref="PlayerCarrySystem"/>, tirar el objeto es
/// soltarlo y lanzarlo en parábola hasta el sitio del crujido, la mirada es la cámara de juego
/// fijada un momento en el ruido, el tope es <see cref="TopeDeVelocidadDelJugador"/> y lo que piensa
/// Will sale en bocadillo por <see cref="PresentadorDeLinea"/>. Ver INC-1311.
/// </summary>
public static class PresenciaQueAcechaEnElJuego
{
    private static GameObject _cuerpo;
    private static PlayerCarrySystem _carga;
    private static vThirdPersonCamera _camara;
    private static object _quienMira;
    private static Transform _haciaDondeMira;
    private static Vector3 _caeEn;
    private static float _segundosDeVuelo;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _cuerpo = null; _carga = null; _camara = null; _quienMira = null; _haciaDondeMira = null;
    }
#endif

    public static void Enchufar()
    {
        StalkingPresence.ResolveSubject = Sujeto;
        StalkingPresence.IsCarrying = Lleva;
        StalkingPresence.Knock = Tirar;
        StalkingPresence.DrawAttention = Mirar;
        StalkingPresence.ReleaseAttention = SoltarMirada;
        StalkingPresence.SetSpeedCap = (quien, tope) => TopeDeVelocidadDelJugador.Poner(quien, tope);
        StalkingPresence.ClearSpeedCap = quien => TopeDeVelocidadDelJugador.Quitar(quien);
        StalkingPresence.Pulse = (fuerza, segundos) => FeedbackService.Vibrar(fuerza * 0.6f, fuerza, segundos);
        StalkingPresence.Think = Pensar;
    }

    /// <summary>Bocadillo de pensamiento sobre el jugador (no corta el juego), por el camino único de las frases.</summary>
    private static void Pensar(SubjectThought pensamiento)
    {
        var sujeto = Sujeto();
        if (sujeto == null) return;
        PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = pensamiento.key,
            textoPorDefecto = pensamiento.fallback,
            ancla = sujeto,
            presentacion = PresentacionDeTexto.Bocadillo,
            gesto = string.IsNullOrEmpty(pensamiento.gesture) ? null : pensamiento.gesture,
            sinVoz = true,
        });
    }

    private static Transform Sujeto()
    {
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        return cuerpo != null ? cuerpo.transform : null;
    }

    /// <summary>La carga del cuerpo que lleva el jugador; se vuelve a buscar solo si cambia de cuerpo.</summary>
    private static PlayerCarrySystem Carga()
    {
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo != _cuerpo || (_carga == null && cuerpo != null))
        {
            _cuerpo = cuerpo;
            _carga = null;
            if (cuerpo != null) ControlDelJugador.DelJugador(cuerpo, out _carga);
        }
        return _carga;
    }

    private static bool Lleva(GameObject objeto)
    {
        var carga = Carga();
        return carga != null && carga.IsCarrying && (objeto == null || carga.CarriedObject == objeto);
    }

    private static bool Tirar(GameObject objeto, Vector3 caeEn, float segundos)
    {
        if (!Lleva(objeto)) return false;
        _caeEn = caeEn;
        _segundosDeVuelo = Mathf.Max(0.2f, segundos);
        _carga.OnObjectDropped -= AlSoltarlo;
        _carga.OnObjectDropped += AlSoltarlo;
        _carga.DropObject();
        FeedbackService.CameraShake(0.12f, 0.25f);
        return true;
    }

    /// <summary>Al soltarse, el objeto sale en parábola hacia el sitio del crujido (y rueda un poco).</summary>
    private static void AlSoltarlo(GameObject objeto)
    {
        if (_carga != null) _carga.OnObjectDropped -= AlSoltarlo;
        if (objeto == null || !objeto.TryGetComponent(out Rigidbody rb) || rb.isKinematic) return;
        float t = _segundosDeVuelo;
        Vector3 v = (_caeEn + Vector3.up * 0.3f - rb.position) / t - 0.5f * t * Physics.gravity;
        rb.linearVelocity = v;
        rb.angularVelocity = new Vector3(2.5f, 0.8f, 1.5f);
    }

    private static void Mirar(object quien, Transform hacia)
    {
        if (hacia == null) return;
        if (_camara == null) _camara = Object.FindAnyObjectByType<vThirdPersonCamera>();
        if (_camara == null) return;
        // Un objetivo fijado por otro sistema (combate) manda: no se le quita.
        if (_camara.LockTarget != null && _camara.LockTarget != _haciaDondeMira) return;
        _camara.SetLockTarget(hacia);
        _quienMira = quien;
        _haciaDondeMira = hacia;
    }

    private static void SoltarMirada(object quien)
    {
        if (quien == null || quien != _quienMira) return;
        if (_camara != null && _camara.LockTarget == _haciaDondeMira) _camara.ClearLockTarget();
        _quienMira = null;
        _haciaDondeMira = null;
    }
}
