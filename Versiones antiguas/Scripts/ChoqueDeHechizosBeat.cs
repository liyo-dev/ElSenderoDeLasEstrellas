using System;
using System.Collections;
using UnityEngine;

/// Dos hechizos que salen A LA VEZ, se encuentran en el aire y forcejean hasta que uno gana (INC-577).
///
/// Raúl, grabación del prólogo del 3 oct: «cuando lanzan el hechizo lo veo más como uno y luego
/// el otro» y «la parte de la colisión está mal, no está clara». Con dos SpellBeat cada proyectil
/// vuela hasta el CUERPO del otro, así que no hay choque posible: uno llega, luego el otro.
///
/// Aquí los dos efectos viajan hacia un punto de encuentro calculado en el momento (entre el pecho
/// del que gana y el hechizo del que pierde), se tocan, se empujan con un vaivén durante
/// `forcejeo` segundos y al final el del ganador arrastra al otro hacia atrás mientras se apaga.
/// El efecto del perdedor puede ser uno que YA estaba en escena (el agujero negro que el Mago
/// Oscuro cargaba sobre la cabeza, registrado con VfxBeat.registrarComo): entonces se mueve ese,
/// no se crea otro, para que se vea que lo que choca es lo que estaba cargando.
///
/// El punto del choque se registra como actor (`registrarChoqueComo`) para que los ShotBeat lo
/// puedan encuadrar mientras dura. Todo en tiempo no escalado y con limpieza al saltar.
[Serializable]
public class ChoqueDeHechizosBeat : SequenceBeat
{
    [Tooltip("Quién gana el choque. Su hechizo sale del pecho.")]
    public string ganadorId = "NPC_Archimago";

    [Tooltip("Efecto del hechizo del ganador.")]
    public GameObject vfxGanador;

    [Tooltip("Desde dónde sale el hechizo del ganador, respecto a sus pies.")]
    public Vector3 salidaGanador = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Quién pierde.")]
    public string perdedorId = "NPC_MagoOscuro";

    [Tooltip("Efecto YA registrado que lanza el perdedor (p. ej. CARGA_ESFERA). Si existe se mueve " +
             "ese; si no, se crea 'vfxPerdedor'.")]
    public string vfxPerdedorRegistrado = "CARGA_ESFERA";

    [Tooltip("Efecto del perdedor si no hay uno registrado.")]
    public GameObject vfxPerdedor;

    [Tooltip("Desde dónde sale el del perdedor si hay que crearlo, respecto a sus pies.")]
    public Vector3 salidaPerdedor = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Dónde se encuentran, de 0 (pegado al ganador) a 1 (pegado al perdedor).")]
    [Range(0.1f, 0.9f)] public float puntoDeEncuentro = 0.5f;

    [Tooltip("Escala del efecto del perdedor al chocar, como factor de la que tenía al salir.")]
    [Min(0.05f)] public float escalaDelPerdedorAlChocar = 0.6f;

    [Tooltip("Radio aproximado del efecto del perdedor al chocar, en metros: lo que se separa su " +
             "centro del punto de contacto.")]
    [Min(0f)] public float radioDelPerdedor = 0.8f;

    [Tooltip("Segundos reales hasta que se tocan.")]
    [Min(0.05f)] public float tiempoHastaChoque = 0.9f;

    [Tooltip("Efecto que nace en el punto de contacto y se queda ahí mientras forcejean.")]
    public GameObject vfxChoque;

    [Tooltip("Segundos reales de forcejeo.")]
    [Min(0f)] public float forcejeo = 2.5f;

    [Tooltip("Cuánto se mueve el punto de contacto adelante y atrás mientras forcejean, en metros.")]
    [Min(0f)] public float vaiven = 0.45f;

    [Tooltip("Segundos reales en los que el ganador empuja al otro hacia atrás y lo apaga.")]
    [Min(0.05f)] public float empujeFinal = 0.7f;

    [Tooltip("ID con el que se registra el punto de contacto, para encuadrarlo con ShotBeat.")]
    public string registrarChoqueComo = "CHOQUE_FINAL";

    [Tooltip("Desmarcado, el choque sigue solo y la secuencia continúa (para cortar planos encima).")]
    public bool esperar = true;

    const string IdLuz = "CHOQUE_FINAL_GANADOR";
    const string IdSombra = "CHOQUE_FINAL_PERDEDOR";

    public override string Describe() => $"Choque de hechizos: {ganadorId} gana a {perdedorId}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        var pool = VfxPoolService.Instance;
        var ganador = ctx?.GetActor(ganadorId)?.Transform;
        var perdedor = ctx?.GetActor(perdedorId)?.Transform;
        if (pool == null || ganador == null || perdedor == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[ChoqueDeHechizosBeat] Falta el pool de VFX o algún actor ({ganadorId}, {perdedorId}); no hay choque.");
#endif
            yield break;
        }

        float vida = tiempoHastaChoque + forcejeo + empujeFinal + 0.5f;
        Vector3 origenLuz = ganador.position + salidaGanador;

        Transform luz = vfxGanador != null ? pool.Play(vfxGanador, origenLuz, Quaternion.identity, vida) : null;
        if (luz != null) ctx.RegisterVfx(IdLuz, luz, pool);

        Transform sombra = ctx.GetActor(vfxPerdedorRegistrado)?.Transform;
        if (sombra == null && vfxPerdedor != null)
        {
            sombra = pool.Play(vfxPerdedor, perdedor.position + salidaPerdedor, Quaternion.identity, vida);
            if (sombra != null) ctx.RegisterVfx(IdSombra, sombra, pool);
        }

        var rutina = Choque(ctx, pool, luz, sombra, origenLuz);
        if (esperar) yield return rutina;
        else ctx.Player?.StartCoroutine(rutina);
    }

    IEnumerator Choque(SequenceContext ctx, VfxPoolService pool, Transform luz, Transform sombra, Vector3 origenLuz)
    {
        Vector3 origenSombra = sombra != null ? sombra.position : origenLuz + Vector3.forward;
        Vector3 linea = origenSombra - origenLuz;
        Vector3 dir = linea.sqrMagnitude > 0.0001f ? linea.normalized : Vector3.forward;
        Vector3 encuentro = Vector3.Lerp(origenLuz, origenSombra, puntoDeEncuentro);
        Vector3 escalaSombra = sombra != null ? sombra.localScale : Vector3.one;
        Vector3 escalaChoque = escalaSombra * escalaDelPerdedorAlChocar;

        // 1. Salen a la vez y se buscan.
        float t = 0f;
        while (t < tiempoHastaChoque)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / tiempoHastaChoque);
            float e = k * k;   // aceleran hacia el choque
            if (luz != null) luz.position = Vector3.LerpUnclamped(origenLuz, encuentro, e);
            if (sombra != null)
            {
                sombra.position = Vector3.LerpUnclamped(origenSombra, encuentro + dir * radioDelPerdedor, e);
                sombra.localScale = Vector3.LerpUnclamped(escalaSombra, escalaChoque, e);
            }
            yield return null;
        }

        Transform chispas = vfxChoque != null ? pool.Play(vfxChoque, encuentro, Quaternion.LookRotation(dir), forcejeo + empujeFinal + 0.3f) : null;
        if (chispas != null) ctx.RegisterVfx(registrarChoqueComo, chispas, pool);

        // 2. Forcejeo: el contacto va y viene, sin que nadie gane todavía.
        t = 0f;
        Vector3 contacto = encuentro;
        while (t < forcejeo)
        {
            t += Time.unscaledDeltaTime;
            float empuje = Mathf.Sin(t * 7.3f) * 0.6f + Mathf.Sin(t * 2.1f + 1.3f) * 0.4f;
            contacto = encuentro + dir * (empuje * vaiven);
            Colocar(luz, sombra, chispas, contacto, dir);
            yield return null;
        }

        // 3. El ganador empuja: el contacto retrocede hacia el perdedor y su hechizo se apaga.
        t = 0f;
        Vector3 desde = contacto;
        Vector3 hasta = Vector3.Lerp(encuentro, origenSombra, 0.75f);
        while (t < empujeFinal)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / empujeFinal);
            float e = k * k * (3f - 2f * k);
            Colocar(luz, sombra, chispas, Vector3.LerpUnclamped(desde, hasta, e), dir);
            if (sombra != null) sombra.localScale = Vector3.LerpUnclamped(escalaChoque, escalaChoque * 0.05f, e);
            yield return null;
        }

        ctx.RecogerVfx(IdLuz);
        ctx.RecogerVfx(IdSombra);
        ctx.RecogerVfx(registrarChoqueComo);
    }

    void Colocar(Transform luz, Transform sombra, Transform chispas, Vector3 contacto, Vector3 dir)
    {
        if (luz != null) luz.position = contacto - dir * 0.15f;
        if (sombra != null) sombra.position = contacto + dir * radioDelPerdedor;
        if (chispas != null) chispas.position = contacto;
    }
}
