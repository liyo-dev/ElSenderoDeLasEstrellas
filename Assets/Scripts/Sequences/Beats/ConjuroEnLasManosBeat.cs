using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// La bola de conjuro: la pequeña esfera de luz que se forma entre las manos de quien prepara un
/// hechizo y que se mueve y crece mientras dura la carga — el conjuro de los animes de los 90.
///
/// Antes esto se resolvía con un destello genérico ("1 Flash_magic_ellow_blue"), que lleva una
/// partícula de 19 m de ancho: en cuanto la cámara se acercaba, la "bola" se comía la pantalla
/// entera y el hechizo de levantar una carreta parecía el hechizo protector del final. Aquí la
/// bola es PEQUEÑA de verdad y la mueve este beat: adelante, arriba y adelante otra vez, cada vez
/// más rápido y más grande conforme sube la intensidad.
[Serializable]
public class ConjuroEnLasManosBeat : SequenceBeat
{
    [Tooltip("Prefab de la bola (VFX_BolaDeConjuro).")]
    public GameObject vfxPrefab;

    [Tooltip("Quién conjura.")]
    public string actorId;

    [Tooltip("Dónde está la bola respecto al conjurante: la altura de las manos. A 1,15 m caía " +
             "sobre la cabeza del conjurante (que mide 1,8 m); a 0,95 m queda entre las manos.")]
    public Vector3 offset = new Vector3(0f, 0.95f, 0f);

    [Tooltip("Segundos que dura el conjuro. 0 = hasta que se acabe la secuencia.")]
    public float duracion = 6f;

    [Tooltip("Segundos de un ciclo completo (delante, arriba y delante otra vez). Se acorta con la intensidad.")]
    public float periodo = 1.8f;

    [Tooltip("Metros que se adelanta la bola hacia delante.")]
    public float avance = 0.3f;

    [Tooltip("Metros que sube la bola por encima de las manos. Poco: si sube demasiado, la bola " +
             "termina tapando la cara de quien conjura.")]
    public float elevacion = 0.22f;

    [Tooltip("Escala de la bola al empezar (multiplicador del tamaño del prefab).")]
    public float escalaInicial = 0.8f;

    [Tooltip("Escala de la bola al final: la intensidad con la que sale el hechizo.")]
    public float escalaFinal = 2f;

    [Tooltip("Vida del uso en el pool, en segundos. 0 = duración + 1. Se sube cuando la bola tiene " +
             "que sobrevivir al beat para entregarse a otro (p. ej. a una fusión).")]
    public float vidaEnPool;

    [Tooltip("ID con el que el efecto queda registrado (para recogerlo con RecogerVfxBeat).")]
    public string registrarComo;

    public override string Describe()
        => $"Conjuro en las manos: {actorId} ({duracion:F1} s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (vfxPrefab == null || ctx?.Player == null) yield break;
        var pool = VfxPoolService.Instance;
        if (pool == null) yield break;
        var actor = ctx.GetActor(actorId);
        if (actor?.Transform == null) yield break;

        Transform raiz = actor.Transform;
        // La vida del uso en el pool es holgada: el conjuro puede acabar solo, y lo que lo apague
        // es la limpieza de la secuencia, que lo devuelve al pool.
        float vida = vidaEnPool > 0f ? vidaEnPool : (duracion > 0f ? duracion + 1f : 600f);
        var instancia = pool.Play(vfxPrefab, raiz.position + raiz.rotation * offset, raiz.rotation, vida, raiz);
        if (instancia == null) yield break;

        // Siempre se da de alta: al terminar la secuencia hay que devolver el uso al pool.
        // Solo si el guion le da un nombre: si no, la bola se queda como un actor más de la
        // escena y el compositor de cámara puede intentar encuadrarla.
        bool conNombre = !string.IsNullOrWhiteSpace(registrarComo);
        ulong uso = pool.ObtenerUso(instancia);
        if (conNombre) ctx.RegisterVfx(registrarComo, instancia, pool);

        bool vivo = true;
        ctx.Player.RegisterCleanup(() =>
        {
            vivo = false;
            if (!conNombre && instancia != null && pool.ObtenerUso(instancia) == uso)
                pool.Recoger(instancia, uso);
        });

        Vector3 escalaBase = instancia.localScale;
        Vector3 adelante = new Vector3(0f, 0f, avance);
        Vector3 arriba = new Vector3(0f, elevacion, 0f);
        float t = 0f;
        while (vivo)
        {
            // La intensidad sube con el tiempo; el reloj del guion es real (ver ReproductorDeGuion),
            // así que la bola se mueve en tiempo real también.
            float intensidad = duracion > 0f ? Mathf.Clamp01(t / duracion) : 1f;
            float carga = Mathf.SmoothStep(0f, 1f, intensidad);
            // El ciclo se acorta con la carga: la bola se agita más cuanto más fuerza tiene.
            float ciclo = Mathf.Max(0.25f, periodo * Mathf.Lerp(1f, 0.45f, carga));
            float fase = Mathf.PingPong(t / ciclo, 1f);
            instancia.localPosition = offset + Vector3.Lerp(adelante, arriba, Mathf.SmoothStep(0f, 1f, fase));
            instancia.localRotation = Quaternion.identity;
            instancia.localScale = escalaBase * Mathf.Lerp(escalaInicial, escalaFinal, carga);
            t += Time.unscaledDeltaTime;
            // La duración es la del conjuro, no la de la secuencia: al acabarse, la bola se queda
            // donde esté (pequeña, en las manos) para que otro beat la recoja, como la fusión final.
            if (duracion > 0f && t >= duracion) break;
            yield return null;
        }
    }
}
