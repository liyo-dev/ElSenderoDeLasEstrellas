using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Dónde estaba el jugador en un auto-guardado (<see cref="PlayerSaveData.PosicionGuardada"/>):
/// se captura al guardar y se usa una sola vez, al aparecer tras cargar la partida.
public static partial class Partida
{
    const float DistanciaAlNavMesh = 1f;
    const float AlturaDelRayoDeSuelo = 0.5f;
    const float LargoDelRayoDeSuelo = 3f;

    static readonly RaycastHit[] s_suelo = new RaycastHit[8];

    // Último sitio jugable conocido en esta sesión: el del último auto-guardado o el de la partida
    // cargada. Lo usa GuardarEnLaUltimaPosicionSegura.
    static PlayerSaveData.PosicionGuardada s_ultimaPosicionSegura;

    /// <summary>
    /// Posición del jugador para un auto-guardado: el punto del NavMesh bajo sus pies, su rumbo y la
    /// escena del suelo que pisa. No vale (y se aparecerá en el ancla) si no hay NavMesh cerca, si
    /// no se encuentra suelo o si está fuera del rectángulo del mundo cíclico.
    /// </summary>
    static PlayerSaveData.PosicionGuardada CapturarPosicionDelJugador()
    {
        var jugador = PlayerService.Player;
        if (jugador == null) return default;
        var t = jugador.transform;

        if (!NavMesh.SamplePosition(t.position, out var hit, DistanciaAlNavMesh, NavMesh.AllAreas)) return default;
        var punto = hit.position;

        var mundo = Kits.WorldWrap.WorldWrapper.Current;
        if (mundo != null && !mundo.IsInside(punto)) return default;

        string escena = EscenaDelSuelo(t, punto);
        if (string.IsNullOrEmpty(escena)) return default;

        return new PlayerSaveData.PosicionGuardada
        {
            valida = true,
            posicion = punto,
            rumbo = t.eulerAngles.y,
            escena = escena
        };
    }

    /// La escena del colisionador más cercano bajo el punto que no sea un personaje (el jugador vive
    /// en la escena persistente; lo que dice dónde está es el suelo).
    static string EscenaDelSuelo(Transform jugador, Vector3 punto)
    {
        int n = Physics.RaycastNonAlloc(punto + Vector3.up * AlturaDelRayoDeSuelo, Vector3.down, s_suelo,
            LargoDelRayoDeSuelo, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        string escena = null;
        for (int i = 0; i < n; i++)
        {
            var c = s_suelo[i].collider;
            if (c == null || c.transform.IsChildOf(jugador)) continue;
            if (c.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
            if (s_suelo[i].distance >= mejor) continue;
            mejor = s_suelo[i].distance;
            escena = c.gameObject.scene.name;
        }
        return escena;
    }

    /// <summary>
    /// Dónde aparecer tras cargar un auto-guardado. Se gasta al pedirla (valga o no): los cambios de
    /// escena posteriores usan su ancla. Vale si su escena está cargada, sigue dentro del mundo
    /// cíclico y hay NavMesh en el punto.
    /// </summary>
    public static bool TomarPosicionDeAutoguardado(out Vector3 posicion, out Quaternion giro)
    {
        posicion = default;
        giro = Quaternion.identity;
        var p = PresetEnMemoria;
        if (p == null || !p.posicionDeAutoguardado.valida) return false;

        var guardada = p.posicionDeAutoguardado;
        p.posicionDeAutoguardado = default;

        var escena = SceneManager.GetSceneByName(guardada.escena);
        if (!escena.IsValid() || !escena.isLoaded) return false;

        var mundo = Kits.WorldWrap.WorldWrapper.Current;
        if (mundo != null && !mundo.IsInside(guardada.posicion)) return false;

        if (!NavMesh.SamplePosition(guardada.posicion, out var hit, DistanciaAlNavMesh, NavMesh.AllAreas)) return false;

        posicion = hit.position;
        giro = Quaternion.Euler(0f, guardada.rumbo, 0f);
        return true;
    }
}
