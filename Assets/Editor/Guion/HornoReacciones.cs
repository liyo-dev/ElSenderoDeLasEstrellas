using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Reacciones automáticas (cinemática viva, 19 nov 2026). Cada evento del guion que pide una
/// reacción la pone en quien no tenga ya una orden propia en ese momento (lo escrito manda):
///   · un hechizo que impacta en un actor  → el objetivo acusa el golpe (TakeDamage / TakeDamage_2);
///   · ese impacto y los truenos con rayo   → los demás en cuadro se sobresaltan según su carácter;
///   · un grito (gesto Grito01 / Angry02)   → los de alrededor dan un respingo.
/// El carácter va en el reparto: «caracter=valiente|miedoso|nino|picaro|sabio» (por defecto normal).
/// Se apaga con «VIDA no» (cabecera), «vida=no» (reparto) o «reacciona=no» (reparto, solo esto).
/// </summary>
internal sealed partial class Horno
{
    private readonly List<string> _reaccionesPuestas = new();
    private readonly Dictionary<ActorH, float> _ultimaReaccion = new();

    private void ReaccionesAutomaticas()
    {
        if (_salida == null || !_g.vida) return;
        int alternar = 0;
        foreach (var e in _efectos.ToList())
        {
            if (e.beat is SpellBeat s)
            {
                var lanza = ActorPorId(s.lanzaId);
                var objetivo = ActorPorId(s.objetivoId);
                if (objetivo == null) continue;
                Vector3 desde = lanza != null ? lanza.PosEn(e.t) : objetivo.PosEn(e.t);
                float dist = Plano(objetivo.PosEn(e.t) - desde).magnitude;
                float tImp = e.t + Mathf.Max(0f, s.tiempoDeCarga) + dist / Mathf.Max(1f, s.velocidad);

                // Con el escudo puesto (un Defend_NoWeapon que aún dura), el golpe se para: DefendHit.
                bool defiende = Defendiendo(objetivo, tImp);
                if (Reacciona(objetivo) && !Esquiva(objetivo, tImp) && (defiende ? Libre(objetivo, tImp, 0.3f, "Defend_NoWeapon") : Libre(objetivo, tImp, 0.9f)))
                {
                    string g = defiende ? "DefendHit_NoWeapon" : (alternar++ % 2 == 0) ? "TakeDamage" : "TakeDamage_2";
                    if (!Tiene(objetivo, g)) g = defiende ? null : Tiene(objetivo, "TakeDamage") ? "TakeDamage" : null;
                    if (g != null)
                    {
                        AnimarAuto(objetivo, tImp, g, 1, defiende ? null : "hurt");
                        _ultimaReaccion[objetivo] = tImp;
                        _reaccionesPuestas.Add($"{Tiempo(tImp)} {objetivo.alias} {(defiende ? "para" : "recibe")} el hechizo de {lanza?.alias ?? s.lanzaId}: {g}");
                    }
                }
                Testigos(tImp, new[] { lanza, objetivo }, "el impacto", fuerte: true);
            }
            else if (e.beat is RayoBeat)
            {
                Testigos(e.t + 0.15f, null, "el trueno", fuerte: false);
            }
        }
        foreach (var a in _actores.Values)
            foreach (var o in a.anim.ToList())
                if (o.tipo == TipoDeAnimacion.Gesto && (o.estado == "Grito01" || o.estado == "Angry02"))
                    Testigos(o.t + 0.2f, new[] { a }, $"el grito de {a.alias}", fuerte: false, cerca: a.PosEn(o.t), radio: 8f);

        foreach (var a in _actores.Values) a.anim.Sort((x, y) => x.t.CompareTo(y.t));
    }

    /// Los que ven algo y no están ocupados reaccionan según su carácter.
    private void Testigos(float t, ActorH[] excluidos, string que, bool fuerte, Vector3? cerca = null, float radio = 0f)
    {
        var plano = _salida.planos.FirstOrDefault(p => t >= p.t0 && t < p.t1);
        if (plano == null) return;
        foreach (var c in _actores.Values)
        {
            if (excluidos != null && excluidos.Contains(c)) continue;
            if (!Reacciona(c) || !EnCuadro(plano, c, t) || !Libre(c, t, 0.8f) || HablaEn(c, t)) continue;
            if (BucleEn(c, t) != null || c.AndandoEn(t) || c.DeslizandoEn(t)) continue;
            if (cerca.HasValue && Plano(c.PosEn(t) - cerca.Value).magnitude > radio) continue;
            if (_ultimaReaccion.TryGetValue(c, out float ult) && t - ult < 3.5f) continue;

            string g = GestoDeTestigo(c, fuerte);
            if (g == null) continue;
            float retraso = 0.12f + (Mathf.Abs(Semilla(c.id)) % 5) * 0.05f;   // no todos a la vez
            AnimarAuto(c, t + retraso, g, 1, g == "Sobresalto01" ? "gasp" : null);
            _ultimaReaccion[c] = t + retraso;
            _reaccionesPuestas.Add($"{Tiempo(t + retraso)} {c.alias} reacciona a {que}: {g}");
        }
    }

    private string GestoDeTestigo(ActorH c, bool fuerte)
    {
        string caracter = c.reparto != null && c.reparto.opciones.TryGetValue("caracter", out var k) ? k.ToLowerInvariant() : "normal";
        string[] candidatos = caracter switch
        {
            "valiente" => fuerte ? new[] { "Decidido01", "Sobresalto01" } : new[] { "SenseSomethingStart_NoWeapon" },
            "miedoso" or "nino" or "niño" => new[] { "Sobresalto01", "Fear01", "SenseSomethingStart_NoWeapon" },
            "picaro" or "pícaro" => fuerte ? new[] { "Laugh_Risita", "Laugh01" } : new[] { "SenseSomethingStart_NoWeapon" },
            "sabio" => new[] { "SenseSomethingStart_NoWeapon", "HeadShake01" },
            _ => new[] { "Sobresalto01", "SenseSomethingStart_NoWeapon" },
        };
        foreach (var g in candidatos)
            if (c.estados.TryGetValue(g, out var i) && !i.bucle) return g;
        return null;
    }

    private bool Reacciona(ActorH a)
    {
        if (a.reparto == null) return true;
        if (a.reparto.opciones.TryGetValue("vida", out var v) && v == "no") return false;
        return !(a.reparto.opciones.TryGetValue("reacciona", out var r) && r == "no");
    }

    /// Sin orden de animación propia cerca de t (lo escrito manda). «salvo»: un estado que no cuenta.
    private static bool Libre(ActorH a, float t, float margen, string salvo = null)
        => !a.anim.Any(o => (o.tipo == TipoDeAnimacion.Gesto || o.tipo == TipoDeAnimacion.Bucle) && o.estado != salvo && Mathf.Abs(o.t - t) < margen);

    /// Tiene puesto el gesto de cubrirse y aún le dura (más un margen: se queda en guardia).
    private static bool Defendiendo(ActorH a, float t)
        => a.anim.Any(o => o.tipo == TipoDeAnimacion.Gesto && o.estado == "Defend_NoWeapon" && t >= o.t
                           && t <= o.t + (a.estados.TryGetValue("Defend_NoWeapon", out var i) ? i.largo : 1f) + 1.2f);

    /// Está en el aire (un SaltoBeat suyo cubre ese instante): lo ha esquivado.
    private bool Esquiva(ActorH a, float t)
        => _efectos.Any(e => e.beat is SaltoBeat s && s.actorId == a.id && t >= e.t && t <= e.t + s.subida + s.sostener + s.caida + 0.1f);

    private static bool Tiene(ActorH a, string estado) => a.estados.ContainsKey(estado);

    private ActorH ActorPorId(string id)
        => string.IsNullOrEmpty(id) ? null : _actores.Values.FirstOrDefault(x => x.id == id || x.alias == id);

    private void InformeDeReacciones(System.Text.StringBuilder sb)
    {
        if (_reaccionesPuestas.Count == 0) return;
        sb.AppendLine($"\nReacciones automáticas ({_reaccionesPuestas.Count}; escribe la tuya en el guion para cambiarla):\n");
        foreach (var l in _reaccionesPuestas) sb.AppendLine("- " + l);
    }
}
