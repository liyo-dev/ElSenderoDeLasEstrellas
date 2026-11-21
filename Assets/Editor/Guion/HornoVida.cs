using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cinemática viva (INC de 19 nov 2026, diseño en «Claude outputs/diseno-pistas-accion-reaccion»).
/// Dos pasadas genéricas que se aplican a TODOS los guiones al hornear, sin tocar su texto:
///
/// 1. Vida de fondo: quien sale en cuadro y lleva más de <see cref="HuecoMinimo"/> s sin orden
///    (ni gesto, ni bucle, ni andar, ni hablar) hace una micro-acción de reposo de su repertorio
///    (idles variados, suspiro, asentir mientras otro habla). Determinista: misma semilla por actor,
///    el previz y el juego ven lo mismo. Se apaga con «VIDA no» (cabecera) o «vida=no» (reparto).
///
/// 2. Gestos cortados: un gesto de cuerpo entero al que otra orden pisa antes de acabar no se ve
///    entero (se pierde la anticipación o la caída). Se apunta en el informe con su línea de tiempo.
/// </summary>
internal sealed partial class Horno
{
    private const float HuecoMinimo = 1.2f;
    private const float Muestra = 0.25f;

    /// Lo que la pasada ha hecho, para el informe («Cinemática viva»).
    private readonly List<string> _vidaPuesta = new();
    private readonly List<string> _gestosCortados = new();

    /// Repertorio de reposo: gestos de un solo disparo, suaves, que no cuentan nada por sí solos.
    private static readonly string[] Reposo = { "Idle01", "Idle02", "Idle03", "Suspiro01" };
    /// Escuchando a alguien que habla cerca: asentir de vez en cuando.
    private static readonly string[] Escucha = { "HeadNod01", "Idle02" };
    /// Con miedo (cara «miedo», «susto»): mirar alrededor, encogerse.
    private static readonly string[] Inquieto = { "SenseSomethingStart_NoWeapon", "Idle03", "Suspiro01" };

    private void VidaDeFondo()
    {
        if (_salida == null || _salida.planos.Count == 0) return;
        if (!_g.vida) { _vidaPuesta.Add("Apagada para este guion («VIDA no»)."); return; }

        foreach (var a in _actores.Values)
        {
            if (a.reparto != null && a.reparto.opciones.TryGetValue("vida", out var v) && v == "no") continue;
            var rnd = new System.Random(Semilla(a.id));
            int puestas = 0;
            float libreDesde = -1f;
            float ocupadoHasta = 0f;

            foreach (var p in _salida.planos)
            {
                libreDesde = -1f;
                for (float t = p.t0; t < p.t1 - 0.6f; t += Muestra)
                {
                    if (t < ocupadoHasta || !EnCuadro(p, a, t) || HaceAlgo(a, t) || VidaApagadaEn(a, t))
                    {
                        libreDesde = -1f;
                        continue;
                    }
                    if (libreDesde < 0f) libreDesde = t;
                    if (t - libreDesde < HuecoMinimo) continue;

                    string g = ElegirReposo(a, t, rnd);
                    if (g == null) { libreDesde = -1f; continue; }
                    float largo = a.estados[g].largo;
                    // Cabe entero antes de la siguiente orden; si no, se deja (nunca cortar).
                    float siguiente = SiguienteOrden(a, t);
                    if (t + largo > siguiente - 0.1f) { libreDesde = -1f; continue; }

                    AnimarAuto(a, t, g, 2);
                    a.anim.Sort((x, y) => x.t.CompareTo(y.t));
                    puestas++;
                    // Respira entre una y otra: 1,3–3,5 s de reposo antes de la siguiente.
                    ocupadoHasta = t + largo + 1.3f + (float)rnd.NextDouble() * 2.2f;
                    libreDesde = -1f;
                }
            }
            if (puestas > 0) _vidaPuesta.Add($"{a.alias}: {puestas} micro-acción(es) de reposo en los huecos en cuadro.");
        }
    }

    /// Gestos de cuerpo entero que otra orden pisa antes de acabar (se ven a medias).
    private void GestosCortados()
    {
        foreach (var a in _actores.Values)
        {
            for (int i = 0; i < a.anim.Count; i++)
            {
                var o = a.anim[i];
                if (o.tipo != TipoDeAnimacion.Gesto || string.IsNullOrEmpty(o.estado)) continue;
                if (!a.estados.TryGetValue(o.estado, out var info) || !info.cuerpoEntero || info.bucle || info.largo <= 0.2f) continue;
                float fin = o.t + info.largo * 0.85f;
                for (int j = i + 1; j < a.anim.Count && a.anim[j].t < fin; j++)
                {
                    var s = a.anim[j];
                    if (s.t <= o.t + 1e-3f) continue;
                    if (s.tipo is TipoDeAnimacion.Gesto or TipoDeAnimacion.Bucle or TipoDeAnimacion.Reposo)
                    {
                        _gestosCortados.Add($"{Tiempo(o.t)} {a.alias}: «{o.estado}» ({info.largo:0.0} s) cortado a los {s.t - o.t:0.0} s por " +
                                            $"{(s.tipo == TipoDeAnimacion.Gesto ? "«" + s.estado + "»" : s.tipo == TipoDeAnimacion.Bucle ? "bucle «" + s.estado + "»" : "reposo")}");
                        break;
                    }
                }
            }
        }
    }

    private void InformeDeVida(System.Text.StringBuilder sb)
    {
        if (_vidaPuesta.Count == 0 && _gestosCortados.Count == 0 && _reaccionesPuestas.Count == 0) return;
        sb.AppendLine("\n## Cinemática viva\n");
        if (_vidaPuesta.Count > 0)
        {
            sb.AppendLine("Vida de fondo puesta sola (nadie quieto en cuadro):\n");
            foreach (var l in _vidaPuesta) sb.AppendLine("- " + l);
        }
        InformeDeReacciones(sb);
        if (_gestosCortados.Count > 0)
        {
            sb.AppendLine($"\nGestos que no se ven enteros ({_gestosCortados.Count}): sepáralos o usa COMPASES.\n");
            foreach (var l in _gestosCortados) sb.AppendLine("- " + l);
        }
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────────────────

    private bool EnCuadro(PlanoHorneado p, ActorH a, float t)
    {
        if (!a.VisibleEn(t)) return false;
        if (p.sujetos.Contains(a.id)) return true;
        if (p.posiciones.Count == 0) return false;
        float tanH = Mathf.Tan(p.lente * 0.5f * Mathf.Deg2Rad) * 16f / 9f;
        float mitadH = Mathf.Atan(tanH) * Mathf.Rad2Deg;
        Vector3 cam = p.PosicionEn(t);
        Vector3 pa = a.PosEn(t);
        return Plano(pa - cam).magnitude < 22f
               && Mathf.Abs(Mathf.DeltaAngle(Rumbo(FocoDePlano(p, t) - cam), Rumbo(pa - cam))) < mitadH;
    }

    private bool HaceAlgo(ActorH a, float t)
    {
        if (a.AndandoEn(t) || a.DeslizandoEn(t) || a.DeLaManoEn(t) || HablaEn(a, t) || BucleEn(a, t) != null) return true;
        if (ConPoseAltaEn(a, t)) return true;
        foreach (var o in a.anim)
        {
            if (o.t > t + 1e-3f) break;
            if (o.tipo != TipoDeAnimacion.Gesto) continue;
            float largo = a.estados.TryGetValue(o.estado ?? "", out var info) ? info.largo : 2.0f;
            if (t >= o.t && t < o.t + largo + 0.3f) return true;
        }
        return false;
    }

    /// Una cara puesta a propósito de dormido o desmayado: nada de vida de fondo.
    private static bool VidaApagadaEn(ActorH a, float t)
    {
        string cara = CaraEn(a, t);
        return cara == "dormido" || cara == "desmayado" || cara == "inconsciente";
    }

    private static string CaraEn(ActorH a, float t)
    {
        string c = null;
        foreach (var k in a.caras) { if (k.t <= t + 1e-3f) c = k.emocion; }
        return c?.ToLowerInvariant();
    }

    private string ElegirReposo(ActorH a, float t, System.Random rnd)
    {
        string cara = CaraEn(a, t);
        string[] lista = cara is "miedo" or "susto" or "asustado" ? Inquieto
                       : AlguienHablaCerca(a, t) ? Escucha
                       : Reposo;
        var validos = lista.Where(g => a.estados.TryGetValue(g, out var i) && !i.bucle && i.largo > 0.5f && i.largo < 6f).ToList();
        if (validos.Count == 0)
            validos = Reposo.Where(g => a.estados.TryGetValue(g, out var i) && !i.bucle && i.largo > 0.5f && i.largo < 6f).ToList();
        if (validos.Count == 0) return null;
        string anterior = a.anim.LastOrDefault(o => o.tipo == TipoDeAnimacion.Gesto && o.t < t).estado;
        for (int k = 0; k < validos.Count; k++)
        {
            string g = validos[rnd.Next(validos.Count)];
            if (g != anterior) return g;
        }
        return validos[0];
    }

    private bool AlguienHablaCerca(ActorH a, float t)
    {
        foreach (var b in _actores.Values)
            if (b != a && HablaEn(b, t) && Plano(b.PosEn(t) - a.PosEn(t)).magnitude < 6f) return true;
        return false;
    }

    private float SiguienteOrden(ActorH a, float t)
    {
        float s = _fin;
        foreach (var o in a.anim) if (o.t > t + 1e-3f) { s = Mathf.Min(s, o.t); break; }
        foreach (var h in a.habla) if (h.t0 > t) s = Mathf.Min(s, h.t0 - 0.3f);
        foreach (var c in a.comandos) if (c > t + 1e-3f) { s = Mathf.Min(s, c); break; }
        return s;
    }

    /// Un gesto que pone el horno solo (1 reacción automática, 2 vida de fondo): la Mesa lo pinta aparte.
    private static void AnimarAuto(ActorH a, float t, string estado, byte origen, string voz = null)
        => a.anim.Add(new OrdenDeAnimacion { t = t, tipo = TipoDeAnimacion.Gesto, estado = estado, voz = voz, origen = origen });

    private static int Semilla(string id)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in id ?? "") h = h * 31 + c;
            return h;
        }
    }
}
