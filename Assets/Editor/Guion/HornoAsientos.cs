#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// Sentarse en un guion, con el MISMO sistema que el juego.
///
/// Un mueble en el que se puede sentar lleva un NPCWorldPoint en su prefab: el «InteractablePoint»
/// dice dónde va la raíz del personaje (ajustado a mano, al filo del asiento), hacia dónde mira y
/// qué actividad es (SitMedium, SitHigh…). Es lo que usan Will (PlayerAmbientActivityHandler) y los
/// NPCs (WalkToActivityState). Los guiones no inventan nada: leen ese mismo punto.
///
///   eldran sienta silla        se acerca andando (si hace falta), se coloca en el punto, mira
///                              hacia donde mira el asiento y hace Begin → Loop de la actividad.
///   liam sienta silla ya       ya está sentado al empezar (sin andar ni Begin).
///   eldran levanta             Exit de la actividad y vuelve a donde estaba de pie.
///
/// «silla» es un PUNTO del guion que nombra el mueble (p. ej. «silla Silla_Eldran»). Si el mueble
/// no tiene NPCWorldPoint es un error: el sitio se ajusta en el prefab, no en el guion.
/// Como en el juego, el personaje se coloca en el punto y la animación Begin hace el gesto de
/// sentarse; al levantarse, Exit y vuelta a su sitio de pie.
internal readonly struct Asiento
{
    public readonly Vector3 pos;
    public readonly float rumbo;
    public readonly NPCAmbientActivity actividad;

    Asiento(Vector3 pos, float rumbo, NPCAmbientActivity actividad) { this.pos = pos; this.rumbo = rumbo; this.actividad = actividad; }

    public static Asiento De(NPCWorldPoint punto)
        => new Asiento(punto.InteractionPosition, punto.InteractionRotation.eulerAngles.y, punto.ActivityType);

    public Vector3 Frente => Quaternion.Euler(0f, rumbo, 0f) * Vector3.forward;
}

internal sealed partial class Horno
{
    // Distancia del sitio de pie delante del asiento (como el jugador, que se levanta donde estaba).
    private const float DelanteDelAsiento = 0.45f;
    // Lo que tarda en colocarse en el punto (el jugador usa un lerp corto parecido).
    private const float AlPunto = 0.3f;

    private float Sentar(ActorH a, GuionTexto.Orden o, float t)
    {
        string nombre = o.args.Count > 0 ? o.args[0] : null;
        if (nombre == "en" && o.args.Count > 1) nombre = o.args[1];
        if (!Punto(nombre, o.linea, out _)) return 0f;
        if (!_asientos.TryGetValue(nombre, out var s))
        {
            Error($"{o}: '{nombre}' no es un asiento. El mueble necesita un NPCWorldPoint (con su InteractablePoint) en el prefab; es el mismo punto que usan Will y los NPCs.");
            return 0f;
        }

        string begin = NPCSimpleAnimator.GetActivityBeginState(s.actividad);
        string loop = NPCSimpleAnimator.GetActivityLoopState(s.actividad);
        if (string.IsNullOrEmpty(loop) || !Estado(a, loop, o)) return 0f;

        var giro = new ClaveDeMirada { tipo = TipoDeMirada.Rumbo, rumbo = s.rumbo };
        Vector3 delante = Suelo(new Vector3(s.pos.x, 0f, s.pos.z) + s.Frente * DelanteDelAsiento);

        if (o.Tiene("ya"))
        {
            SoltarBucleSiAnda(a, t, haraCuerpo: true);
            a.Teletransporte(t, s.pos);
            a.Mirar(t, giro);
            a.Animar(t, TipoDeAnimacion.Bucle, loop);
            a.bucle = loop; a.bucleDeCuerpo = true;
            a.sentadoCon = s.actividad;
            a.dePie = delante;
            return 0f;
        }

        float t0 = t;
        SoltarBucleSiAnda(a, t, haraCuerpo: true);
        Vector3 aqui = a.PosEn(t);
        if (new Vector2(aqui.x - delante.x, aqui.z - delante.z).sqrMagnitude > 0.15f * 0.15f)
            t += Caminar(a, t, new List<Vector3> { delante }, Velocidad(o.Op("ritmo", "normal"), o), o);

        a.dePie = a.PosEn(t);
        a.Mirar(t, giro);
        Deslizar(a, t, a.dePie, s.pos, AlPunto);

        float largo = 0f;
        if (!string.IsNullOrEmpty(begin) && a.estados.TryGetValue(begin, out var infoBegin))
        {
            largo = infoBegin.largo;
            a.Animar(t, TipoDeAnimacion.Gesto, begin);
        }
        a.Animar(t + largo * 0.95f, TipoDeAnimacion.Bucle, loop);
        a.bucle = loop; a.bucleDeCuerpo = true;
        a.sentadoCon = s.actividad;
        return (t - t0) + Mathf.Max(AlPunto, largo);
    }

    private float Levantar(ActorH a, GuionTexto.Orden o, float t)
    {
        if (a.bucle == null || a.sentadoCon == NPCAmbientActivity.None)
        {
            Aviso($"{o}: {a.alias} no está sentado (falta un «sienta» antes)");
            return 0f;
        }
        string exit = NPCSimpleAnimator.GetActivityExitState(a.sentadoCon);
        float largo = 0f;
        if (!string.IsNullOrEmpty(exit) && a.estados.TryGetValue(exit, out var infoExit))
        {
            largo = infoExit.largo;
            a.Animar(t, TipoDeAnimacion.Gesto, exit);
        }
        Deslizar(a, t + largo * 0.85f, a.PosEn(t), a.dePie, AlPunto);
        a.Animar(t + largo, TipoDeAnimacion.Reposo);
        a.bucle = null; a.bucleDeCuerpo = false;
        a.sentadoCon = NPCAmbientActivity.None;
        return largo * 0.85f + AlPunto;
    }

    /// Mueve la raíz sin andar (no gira el cuerpo ni pone la locomoción).
    private static void Deslizar(ActorH a, float t, Vector3 desde, Vector3 hasta, float dura)
    {
        a.Cortar(t);
        const int pasos = 4;
        for (int k = 1; k <= pasos; k++)
        {
            float u = k / (float)pasos;
            a.pos.Add(new ClaveDePosicion(t + dura * u, Vector3.Lerp(desde, hasta, u * u * (3f - 2f * u))));
        }
        a.deslizando.Add((t, t + dura + 0.05f));
    }
}
#endif
