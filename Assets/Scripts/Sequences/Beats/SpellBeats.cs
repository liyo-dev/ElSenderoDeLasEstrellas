using System;
using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// Un hechizo que se LANZA: nace en la mano, viaja, y revienta en el objetivo.
///
/// ── Por qué hacía falta ───────────────────────────────────────────────────────────────────────
/// Raúl, novena grabación: *«cuando lanzan los hechizos no se ve cómo salen de las manos y viajan
/// al destino; de pronto se ve algo que explota. La batalla debe sentirse épica y los hechizos
/// salen de las manos y debemos ver cómo el hechizo se dispara de verdad, igual que en el juego.
/// No entiendo por qué en las secuencias no se ven nunca.»*
///
/// La respuesta a esa última pregunta es simple y es la razón de este archivo: **hasta hoy las
/// secuencias no tenían con qué**. `VfxBeat` instancia un efecto en un sitio y lo deja ahí; el
/// gesto del brazo va por su lado. Entre los dos no hay nada que una la mano con el blanco, así
/// que un duelo de magos acababa siendo dos personajes gesticulando y una explosión suelta en el
/// fondo. En el juego sí se ve porque ahí lo mueve el sistema de proyectiles del combate, que una
/// cinemática no puede usar (no hay daño, ni colisión, ni vida que quitar — solo hay que verlo).
///
/// ── Las tres partes ───────────────────────────────────────────────────────────────────────────
/// Un hechizo que se lee tiene tres momentos, y los tres importan:
///
///  1. **La carga**, en la mano, antes de que salga nada. Es lo que hace que el lanzamiento tenga
///     causa: primero se enciende la mano, después sale el hechizo.
///  2. **El viaje**. Es el único de los tres que faltaba por completo, y es el que convierte dos
///     efectos sueltos en un disparo.
///  3. **El impacto**, donde estaba el objetivo *en ese momento* — no donde estaba al empezar.
///
/// ── Un detalle que importa ────────────────────────────────────────────────────────────────────
/// El objetivo se vuelve a leer **cada fotograma**. Si el que recibe se mueve —y en este duelo se
/// mueve: esquiva saltando, se aparta volando— el hechizo le sigue hasta el final. Un proyectil
/// que vuela hacia donde el otro YA NO ESTÁ se lee como un fallo del juego, no como un fallo de
/// puntería, así que para que falle a propósito se apunta a una marca y no a un actor.
///
/// ── Para la cámara ────────────────────────────────────────────────────────────────────────────
/// Con `registrarComo` el proyectil se da de alta como actor mientras vuela, así que un `ShotBeat`
/// con `live` puede SEGUIRLO igual que a un personaje. Es el mismo mecanismo que usa el módulo del
/// Despertar de la Estrella para su proyectil entrante.
[Serializable]
public class SpellBeat : SequenceBeat
{
    [Header("Quién y a qué")]
    [Tooltip("Quién lanza. El hechizo nace en su mano.")]
    public string lanzaId;

    [Tooltip("A quién va dirigido. Se relee cada fotograma, así que si se mueve, el hechizo le " +
             "sigue. Para que el hechizo FALLE a propósito, dejar esto vacío y apuntar a una marca.")]
    public string objetivoId;

    [Tooltip("Marca a la que va, si no va a un actor. Es lo que se usa para un hechizo que falla " +
             "y revienta en otro sitio.")]
    public string objetivoMarca;

    [Header("Los tres efectos")]
    [Tooltip("El que se enciende en la mano ANTES de salir. Puede ir vacío.")]
    public GameObject vfxEnLaMano;

    [Tooltip("El que viaja. Es el que faltaba.")]
    public GameObject vfxProyectil;

    [Tooltip("El que revienta al llegar.")]
    public GameObject vfxImpacto;

    [Header("Sonido")]
    public string sfxLanzamiento;
    public string sfxImpacto;

    [Header("Ajustes")]
    [Tooltip("Altura de la mano sobre los pies del que lanza. En estas proporciones, 1,15 m es la " +
             "mano de un adulto con el brazo extendido.")]
    public float alturaDeLaMano = 1.15f;

    [Tooltip("Cuánto se separa de su pecho, hacia donde mira, para que el efecto no nazca dentro " +
             "del personaje.")]
    public float separacionDelCuerpo = 0.45f;

    [Tooltip("Metros por segundo. 16 es rápido y se sigue con la vista; por encima de 25 se " +
             "convierte en un parpadeo.")]
    public float velocidad = 16f;

    [Tooltip("Segundos que la mano está encendida antes de que salga el hechizo.")]
    public float tiempoDeCarga = 0.35f;

    [Tooltip("Altura sobre los pies a la que impacta en el objetivo, cuando es un actor.")]
    public float alturaDelImpacto = 1.1f;

    [Tooltip("Sacudida de cámara al impacto. 0 = ninguna.")]
    public float sacudidaAlImpacto = 0.4f;

    [Tooltip("Esperar a que llegue antes de seguir con el beat siguiente. Desmarcado, la escena " +
             "avanza mientras el hechizo vuela — para cortar a la cara del que lo ve venir.")]
    public bool esperarAlImpacto = true;

    [Tooltip("Si se rellena, el proyectil se da de alta como actor con este ID mientras vuela, " +
             "para que un ShotBeat con 'live' pueda seguirlo. Se da de baja al impactar.")]
    public string registrarComo;

    /// Tope de seguridad. Ningún beat puede colgar la secuencia (regla del sistema).
    private const float TopeDeVuelo = 6f;

    public override string Describe()
        => $"Hechizo: {lanzaId} → {(string.IsNullOrWhiteSpace(objetivoId) ? objetivoMarca : objetivoId)}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var lanza = ctx?.GetActor(lanzaId);
        if (lanza?.Transform == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SpellBeat] No hay ningún actor '{lanzaId}' que lance esto ({note}).");
#endif
            yield break;
        }

        if (VfxPoolService.Instance == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SpellBeat] VfxPoolService.Instance es null — ¿arrancaste desde Start.unity?");
#endif
            yield break;
        }

        var objetivo = string.IsNullOrWhiteSpace(objetivoId) ? null : ctx.GetActor(objetivoId);
        Transform marca = string.IsNullOrWhiteSpace(objetivoMarca) || ctx.Stage == null
            ? null
            : ctx.Stage.GetMark(objetivoMarca);

        if (objetivo?.Transform == null && marca == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SpellBeat] El hechizo de '{lanzaId}' no tiene a dónde ir ({note}).");
#endif
            yield break;
        }

        // ── 1. La carga ──────────────────────────────────────────────────────────────────────
        Vector3 mano = Mano(lanza);

        if (vfxEnLaMano != null)
            VfxPoolService.Instance.Play(vfxEnLaMano, mano, lanza.Transform.rotation,
                Mathf.Max(0.5f, tiempoDeCarga + 0.4f));

        if (!string.IsNullOrWhiteSpace(sfxLanzamiento) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxLanzamiento, 1f, mano);

        if (tiempoDeCarga > 0f) yield return new WaitForSeconds(tiempoDeCarga);

        // ── 2. El viaje ──────────────────────────────────────────────────────────────────────
        mano = Mano(lanza);   // puede haberse movido durante la carga
        Vector3 destino = Destino(objetivo, marca);

        Transform proyectil = vfxProyectil != null
            ? VfxPoolService.Instance.Play(vfxProyectil, mano,
                Quaternion.LookRotation((destino - mano).normalized, Vector3.up), TopeDeVuelo)
            : null;

        if (proyectil != null && !string.IsNullOrWhiteSpace(registrarComo))
            ctx.RegisterActor(registrarComo, proyectil, 0f);

        float v = Mathf.Max(2f, velocidad);
        float transcurrido = 0f;
        Vector3 posicion = mano;

        while (transcurrido < TopeDeVuelo)
        {
            transcurrido += Time.deltaTime;

            // Releído cada fotograma: si el objetivo se mueve, el hechizo le sigue.
            destino = Destino(objetivo, marca);

            Vector3 delta = destino - posicion;
            float queda = delta.magnitude;
            if (queda <= 0.35f) break;

            Vector3 dir = delta / queda;
            posicion += dir * Mathf.Min(v * Time.deltaTime, queda);

            if (proyectil != null)
                proyectil.SetPositionAndRotation(posicion, Quaternion.LookRotation(dir, Vector3.up));

            yield return null;
        }

        // ── 3. El impacto ────────────────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(registrarComo)) ctx.UnregisterActor(registrarComo);

        // Y el proyectil se RECOGE. Sin esto se queda parado en el punto de impacto hasta que
        // caduque su lifetime de seguridad — que es lo que Raúl describe en la décima grabación:
        // "ya se ven lanzar hechizos, pero no explotan, se quedan en la escena". Explotar sí
        // explotaban; lo que pasaba es que la bola seguía ahí encima.
        if (proyectil != null) VfxPoolService.Instance.Recoger(proyectil);

        if (vfxImpacto != null)
            VfxPoolService.Instance.Play(vfxImpacto, posicion, Quaternion.identity, 2.5f);

        if (!string.IsNullOrWhiteSpace(sfxImpacto) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxImpacto, 1f, posicion);

        if (sacudidaAlImpacto > 0f)
            FeedbackService.CameraShake(sacudidaAlImpacto, 0.45f);

        if (!esperarAlImpacto) yield break;

        yield return null;
    }

    /// Dónde nace el hechizo: a la altura de la mano y un poco por delante del pecho.
    private Vector3 Mano(SequenceActor lanza)
    {
        Transform t = lanza.Transform;
        return t.position + Vector3.up * alturaDeLaMano + t.forward * separacionDelCuerpo;
    }

    private Vector3 Destino(SequenceActor objetivo, Transform marca)
    {
        if (objetivo?.Transform != null)
            return objetivo.Transform.position + Vector3.up * alturaDelImpacto;

        return marca != null ? marca.position : Vector3.zero;
    }
}


/// Espera a que un hechizo lanzado con `registrarComo` llegue a su destino.
///
/// Un SpellBeat dentro de un Parallel que no espera a todos deja que la escena corte mientras el
/// hechizo vuela — es lo que permite ir a la cara del que lo recibe con la bola ya en el aire.
/// Pero entonces lo que venga detrás del corte no sabe cuándo llega. En la grabación del 21 sep
/// el Mago Oscuro hacía la animación de daño ANTES de que la bola de fuego le alcanzara: el
/// TakeDamage iba 0,3 s después del lanzamiento, y el vuelo dura casi un segundo.
///
/// Esto se pone justo antes de la reacción: primero espera a que el hechizo exista (la carga en
/// la mano va antes del vuelo) y después a que desaparezca, que es el instante del impacto.
/// Con topes, porque ningún beat puede colgar la secuencia.
[Serializable]
public class EsperarHechizoBeat : SequenceBeat
{
    [Tooltip("El mismo ID que el 'registrarComo' del SpellBeat al que se espera.")]
    public string hechizo;

    [Tooltip("Segundos máximos esperando a que salga de la mano. Si no aparece, se sigue.")]
    public float topeDeSalida = 1.5f;

    [Tooltip("Segundos máximos de vuelo antes de dejar de esperar.")]
    public float topeDeVuelo = 6f;

    public override string Describe() => $"Esperar al impacto de '{hechizo}'";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx == null || string.IsNullOrWhiteSpace(hechizo)) yield break;

        float t = 0f;
        while (!ctx.EstaRegistrado(hechizo) && t < topeDeSalida)
        {
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (ctx.EstaRegistrado(hechizo) && t < topeDeVuelo)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
}
