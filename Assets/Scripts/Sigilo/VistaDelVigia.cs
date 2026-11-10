using UnityEngine;

/// Lo que ve quien vigila: un cono por delante, hasta un alcance, que tapa la geometría del
/// escenario. Los personajes no tapan (viven en la capa Default como la geometría; se reconocen
/// por su NPCSimpleAnimator). Un jugador dentro de un <see cref="Escondite"/> no se ve, salvo
/// que esté tan cerca que se le tropiece.
public static class VistaDelVigia
{
    private const float AlturaDeLosOjos = 1.5f;
    private const float AlturaDelPecho = 0.9f;

    /// A menos de esto, en metros, al jugador se le ve aunque esté escondido.
    public const float DistanciaDeTropiezo = 1.4f;

    private static readonly RaycastHit[] s_golpes = new RaycastHit[16];

    /// El vigía ve al jugador ahora mismo.
    public static bool VeAlJugador(Transform vigia, Transform jugador, float alcance, float anguloTotal)
    {
        if (vigia == null || jugador == null) return false;
        Vector3 ojos = vigia.position + Vector3.up * AlturaDeLosOjos;
        Vector3 pecho = jugador.position + Vector3.up * AlturaDelPecho;
        Vector3 hacia = pecho - ojos;
        float distancia = hacia.magnitude;
        if (distancia < DistanciaDeTropiezo) return true;
        if (distancia > alcance || Escondite.JugadorEscondido) return false;

        Vector3 enElSuelo = new Vector3(hacia.x, 0f, hacia.z);
        if (enElSuelo.sqrMagnitude > 0.0001f && Vector3.Angle(vigia.forward, enElSuelo) > anguloTotal * 0.5f)
            return false;

        return !Tapado(ojos, hacia / distancia, distancia, vigia, jugador);
    }

    /// El jugador está lo bastante cerca para oírle (y no está escondido).
    public static bool OyeAlJugador(Transform vigia, Transform jugador, float distanciaDeOido)
    {
        if (vigia == null || jugador == null) return false;
        float d2 = (jugador.position - vigia.position).sqrMagnitude;
        if (d2 < DistanciaDeTropiezo * DistanciaDeTropiezo) return true;
        return !Escondite.JugadorEscondido && d2 < distanciaDeOido * distanciaDeOido;
    }

    private static bool Tapado(Vector3 desde, Vector3 direccion, float distancia, Transform vigia, Transform jugador)
    {
        int n = Physics.RaycastNonAlloc(desde, direccion, s_golpes, distancia,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var t = s_golpes[i].collider != null ? s_golpes[i].collider.transform : null;
            if (t == null || t.IsChildOf(vigia) || t.IsChildOf(jugador)) continue;
            if (t.root.GetComponent<NPCSimpleAnimator>() != null) continue;
            return true;
        }
        return false;
    }
}
