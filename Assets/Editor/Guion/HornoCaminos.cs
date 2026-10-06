#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// Caminos: NavMesh + esquivar a los demás antes de que pase, no en Play.
internal sealed partial class Horno
{
    /// Distancia mínima entre dos personajes (de centro a centro) mientras uno anda.
    private const float Separacion = 0.7f;
    private const float PasoDeMuestreo = 0.1f;

    /// Hace andar a un actor desde donde esté en t hasta los destinos. Devuelve la duración.
    private float Caminar(ActorH a, float t, List<Vector3> destinos, float velocidad, GuionTexto.Orden o)
    {
        Vector3 desde = a.PosEn(t);
        var desvios = new List<Vector3>();
        float retraso = 0f;
        List<ClaveDePosicion> mejor = null;
        string conflictoFinal = null;

        for (int intento = 0; intento < 10; intento++)
        {
            var ruta = Ruta(desde, destinos, desvios, o);
            if (ruta == null) return 0f;
            var claves = Temporizar(ruta, t + retraso, velocidad);
            var c = PrimerConflicto(a, claves, desde, destinos[destinos.Count - 1]);
            mejor = claves;
            if (c == null) { conflictoFinal = null; break; }
            conflictoFinal = $"{c.Value.otro.alias} a {c.Value.t:0.0} s";

            if (!c.Value.otro.AndandoEn(c.Value.t) && desvios.Count < 4)
            {
                // Está quieto: se le rodea por el lado bueno.
                if (Desvio(c.Value.donde, c.Value.dir, c.Value.otro.PosEn(c.Value.t), out var w)) { desvios.Add(w); continue; }
            }
            // Se cruzan andando (o no hay por dónde rodear): espera un poco.
            if (retraso < 3.2f) { retraso += 0.4f; continue; }
            break;
        }

        if (conflictoFinal != null)
            Aviso($"{o}: {a.alias} roza a {conflictoFinal} y no he encontrado otro camino. Mueve el destino o el momento.");

        a.Cortar(t);
        if (retraso > 0f) a.pos.Add(new ClaveDePosicion(t + retraso, desde));
        foreach (var k in mejor) if (k.t > a.pos[a.pos.Count - 1].t + 1e-4f) a.pos.Add(k);
        return mejor[mejor.Count - 1].t - t;
    }

    /// Un paseo en bucle por varios puntos, con pausas, hasta que el actor reciba otra orden.
    private void Pasear(ActorH a, float t, List<Vector3> puntos, float velocidad, float pausa, GuionTexto.Orden o)
    {
        const float Horizonte = 900f;
        float reloj = t;
        int i = 0;
        // Empieza por el punto más cercano.
        Vector3 aqui = a.PosEn(t);
        float dmin = float.MaxValue;
        for (int k = 0; k < puntos.Count; k++)
        {
            float d = (puntos[k] - aqui).sqrMagnitude;
            if (d < dmin) { dmin = d; i = k; }
        }
        int vueltas = 0;
        while (reloj < t + Horizonte && vueltas < 400)
        {
            i = (i + 1) % puntos.Count;
            float dura = Caminar(a, reloj, new List<Vector3> { puntos[i] }, velocidad, o);
            if (dura <= 0.01f) break;
            reloj += dura + pausa;
            vueltas++;
        }
    }

    private struct Conflicto
    {
        public ActorH otro;
        public float t;
        public Vector3 donde, dir;
    }

    private Conflicto? PrimerConflicto(ActorH a, List<ClaveDePosicion> claves, Vector3 desde, Vector3 destino)
    {
        float t0 = claves[0].t, t1 = claves[claves.Count - 1].t;
        foreach (var b in _actores.Values)
        {
            if (b == a) continue;
            // Si empiezo (o acabo) pegado a él, es a propósito: solo cuenta cuando ya nos hemos separado.
            // Solo vale con alguien quieto: dos que andan y empiezan juntos también pueden chocar.
            bool pegadoAlSalir = !b.AndandoEn(t0) && (b.PosEn(t0) - desde).sqrMagnitude < Separacion * Separacion;
            bool pegadoAlLlegar = !b.AndandoEn(t1) && (b.PosEn(t1) - destino).sqrMagnitude < (Separacion + 0.25f) * (Separacion + 0.25f);
            bool separados = !pegadoAlSalir;
            for (float t = t0; t <= t1; t += PasoDeMuestreo)
            {
                if (!b.VisibleEn(t)) continue;
                Vector3 pa = Interpolar.Posicion(claves, t);
                Vector3 pb = b.PosEn(t);
                float d = Plano(pa - pb).magnitude;
                if (!separados) { if (d > Separacion + 0.1f) separados = true; continue; }
                if (pegadoAlLlegar && (pa - destino).sqrMagnitude < 1.0f * 1.0f) continue;
                if (d < Separacion)
                {
                    Vector3 dir = Plano(Interpolar.Posicion(claves, Mathf.Min(t1, t + 0.2f)) - Interpolar.Posicion(claves, Mathf.Max(t0, t - 0.2f)));
                    return new Conflicto { otro = b, t = t, donde = pa, dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward };
                }
            }
        }
        return null;
    }

    /// Un punto para pasar por el lado de alguien que está quieto.
    private static bool Desvio(Vector3 donde, Vector3 dir, Vector3 obstaculo, out Vector3 w)
    {
        Vector3 lado = Vector3.Cross(Vector3.up, dir).normalized;
        // Primero el lado hacia el que ya se iba a apartar.
        float signo = Vector3.Dot(donde - obstaculo, lado) >= 0f ? 1f : -1f;
        for (int k = 0; k < 2; k++, signo = -signo)
        {
            foreach (float dist in new[] { Separacion + 0.35f, Separacion + 0.75f })
            {
                Vector3 c = obstaculo + lado * signo * dist;
                if (NavMesh.SamplePosition(c, out var hit, 0.25f, NavMesh.AllAreas)
                    && !NavMesh.Raycast(donde - dir * 1.0f, hit.position, out _, NavMesh.AllAreas))
                {
                    w = hit.position;
                    return true;
                }
            }
        }
        w = default;
        return false;
    }

    /// Camino por el NavMesh pasando por los desvíos y los destinos, ya suavizado y apoyado en el suelo.
    private List<Vector3> Ruta(Vector3 desde, List<Vector3> destinos, List<Vector3> desvios, GuionTexto.Orden o)
    {
        // Los desvíos se meten antes del destino al que están más cerca del camino.
        var paradas = new List<Vector3>(destinos);
        foreach (var w in desvios)
        {
            int mejor = 0; float dmin = float.MaxValue;
            Vector3 previo = desde;
            for (int i = 0; i < paradas.Count; i++)
            {
                float d = DistanciaASegmento(w, previo, paradas[i]);
                if (d < dmin) { dmin = d; mejor = i; }
                previo = paradas[i];
            }
            paradas.Insert(mejor, w);
        }

        var esquinas = new List<Vector3> { desde };
        var camino = new NavMeshPath();
        Vector3 actual = desde;
        foreach (var p in paradas)
        {
            if (!NavMesh.SamplePosition(actual, out var ha, 1.5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(p, out var hb, 1.5f, NavMesh.AllAreas))
            {
                Aviso($"{o}: un punto queda fuera del NavMesh ({p.x:0.0}, {p.z:0.0}); voy en línea recta");
                esquinas.Add(p); actual = p; continue;
            }
            if (NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, camino) && camino.status != NavMeshPathStatus.PathInvalid)
            {
                if (camino.status == NavMeshPathStatus.PathPartial)
                    Aviso($"{o}: no se llega del todo a ({p.x:0.0}, {p.z:0.0}) por el NavMesh");
                for (int i = 1; i < camino.corners.Length; i++) esquinas.Add(camino.corners[i]);
                if ((camino.corners[camino.corners.Length - 1] - p).sqrMagnitude > 0.04f) esquinas.Add(p);
            }
            else
            {
                Aviso($"{o}: el NavMesh no encuentra camino hasta ({p.x:0.0}, {p.z:0.0}); voy en línea recta");
                esquinas.Add(p);
            }
            actual = p;
        }
        return Suavizar(esquinas);
    }

    /// Redondea las esquinas (nadie anda haciendo ángulos rectos) y muestrea cada 15 cm.
    private static List<Vector3> Suavizar(List<Vector3> esquinas)
    {
        var limpio = new List<Vector3> { esquinas[0] };
        for (int i = 1; i < esquinas.Count; i++)
            if (Plano(esquinas[i] - limpio[limpio.Count - 1]).sqrMagnitude > 0.01f) limpio.Add(esquinas[i]);
        if (limpio.Count < 2) return new List<Vector3> { esquinas[0], esquinas[esquinas.Count - 1] };

        var curva = new List<Vector3> { limpio[0] };
        for (int i = 1; i < limpio.Count - 1; i++)
        {
            Vector3 a = limpio[i - 1], b = limpio[i], c = limpio[i + 1];
            float r = Mathf.Min(0.6f, Plano(b - a).magnitude * 0.45f, Plano(c - b).magnitude * 0.45f);
            Vector3 p0 = b + Plano(a - b).normalized * r;
            Vector3 p2 = b + Plano(c - b).normalized * r;
            curva.Add(p0);
            for (int k = 1; k < 6; k++)
            {
                float u = k / 6f;
                curva.Add((1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * b + u * u * p2);
            }
            curva.Add(p2);
        }
        curva.Add(limpio[limpio.Count - 1]);

        var muestras = new List<Vector3> { Suelo(curva[0]) };
        for (int i = 1; i < curva.Count; i++)
        {
            Vector3 a = curva[i - 1], b = curva[i];
            float largo = Plano(b - a).magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(largo / 0.15f));
            for (int k = 1; k <= n; k++) muestras.Add(Suelo(Vector3.Lerp(a, b, k / (float)n)));
        }
        return muestras;
    }

    /// Pone tiempos a un camino: arranca y frena suave, va a su velocidad en medio.
    private static List<ClaveDePosicion> Temporizar(List<Vector3> ruta, float t0, float vel)
    {
        float total = 0f;
        var acumulado = new float[ruta.Count];
        for (int i = 1; i < ruta.Count; i++) { total += Plano(ruta[i] - ruta[i - 1]).magnitude; acumulado[i] = total; }
        float rampa = Mathf.Min(0.7f, total / 3f);
        var claves = new List<ClaveDePosicion> { new(t0, ruta[0]) };
        float t = t0;
        float ultimaClave = t0;
        for (int i = 1; i < ruta.Count; i++)
        {
            float ds = acumulado[i] - acumulado[i - 1];
            float s = (acumulado[i] + acumulado[i - 1]) * 0.5f;
            float f = rampa > 0.01f ? Mathf.Clamp01(Mathf.Min(s, total - s) / rampa) : 1f;
            float v = vel * Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(f));
            t += ds / Mathf.Max(0.1f, v);
            // Una clave cada ~0,2 s basta (entre medias se interpola en línea recta).
            if (t - ultimaClave >= 0.2f || i == ruta.Count - 1)
            {
                claves.Add(new ClaveDePosicion(t, ruta[i]));
                ultimaClave = t;
            }
        }
        return claves;
    }

    private static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

    private static float DistanciaASegmento(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = Plano(b - a), ap = Plano(p - a);
        float u = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
        return (ap - ab * u).magnitude;
    }
}
#endif
