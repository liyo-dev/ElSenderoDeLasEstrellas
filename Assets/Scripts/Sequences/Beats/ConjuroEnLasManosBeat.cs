using System;
using System.Collections;
using System.Collections.Generic;
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
    private static readonly Dictionary<Transform, int> _controladorActivo = new();
    private static int _siguienteControladorId = 0;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _controladorActivo.Clear();
        _siguienteControladorId = 0;
    }
#endif

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

    [Tooltip("ID del efecto registrado previamente si se quiere reutilizar en lugar de crear uno nuevo.")]
    public string efectoRegistrado;

    [Tooltip("Si es true, la bola asciende suavemente hacia offsetFinal al unísono con la subida de brazos.")]
    public bool subirACabeza;

    [Tooltip("Posición final respecto al conjurante cuando sube a la cabeza (por encima de las manos alzadas).")]
    public Vector3 offsetFinal = new Vector3(0f, 2.85f, 0f);

    [Tooltip("Segundos que tarda en subir desde offset hasta offsetFinal al alzar los brazos.")]
    public float tiempoSubida = 1.4f;

    /// Desvincula cualquier beat de conjuro activo sobre la instancia dada para ceder el control.
    public static void Desvincular(Transform instancia)
    {
        if (instancia != null) _controladorActivo.Remove(instancia);
    }

    public override string Describe()
        => subirACabeza
            ? $"Conjuro a la cabeza: {actorId} ({duracion:F1} s)"
            : $"Conjuro en las manos: {actorId} ({duracion:F1} s)";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        var actor = ctx.GetActor(actorId);
        if (actor?.Transform == null) yield break;

        Transform raiz = actor.Transform;
        Transform instancia = null;
        VfxPoolService pool = null;
        ulong uso = 0;
        bool esReutilizado = false;

        if (!string.IsNullOrWhiteSpace(efectoRegistrado) && ctx.TryGetVfx(efectoRegistrado, out instancia, out pool, out uso))
        {
            esReutilizado = true;
            if (instancia != null && instancia.parent != raiz)
                instancia.SetParent(raiz, true);
        }
        else
        {
            if (vfxPrefab == null) yield break;
            pool = VfxPoolService.Instance;
            if (pool == null) yield break;

            float vida = vidaEnPool > 0f ? vidaEnPool : (duracion > 0f ? duracion + 1f : 600f);
            instancia = pool.Play(vfxPrefab, raiz.position + raiz.rotation * offset, raiz.rotation, vida, raiz);
            if (instancia == null) yield break;
            uso = pool.ObtenerUso(instancia);
        }

        if (instancia == null) yield break;

        bool conNombre = !string.IsNullOrWhiteSpace(registrarComo);
        if (conNombre && pool != null) ctx.RegisterVfx(registrarComo, instancia, pool);

        int miControladorId = ++_siguienteControladorId;
        _controladorActivo[instancia] = miControladorId;

        bool vivo = true;
        ctx.Player.RegisterCleanup(() =>
        {
            vivo = false;
            if (_controladorActivo.TryGetValue(instancia, out int id) && id == miControladorId)
                _controladorActivo.Remove(instancia);
            if (!esReutilizado && !conNombre && instancia != null && pool != null && pool.ObtenerUso(instancia) == uso)
                pool.Recoger(instancia, uso);
        });

        Vector3 escalaBase = vfxPrefab != null ? vfxPrefab.transform.localScale : Vector3.one;
        Vector3 adelante = new Vector3(0f, 0f, avance);
        Vector3 arriba = new Vector3(0f, elevacion, 0f);
        float t = 0f;

        while (vivo)
        {
            if (!_controladorActivo.TryGetValue(instancia, out int idActual) || idActual != miControladorId)
                yield break;

            if (subirACabeza)
            {
                float tSubida = tiempoSubida > 0f ? tiempoSubida : (duracion > 0f ? duracion : 1.4f);
                float kSubida = Mathf.Clamp01(t / tSubida);
                float suavizado = Mathf.SmoothStep(0f, 1f, kSubida);
                Vector3 pos = Vector3.Lerp(offset, offsetFinal, suavizado);
                if (kSubida >= 1f)
                {
                    float flote = Mathf.Sin((t - tSubida) * 2.5f) * 0.03f;
                    pos.y += flote;
                }
                instancia.localPosition = pos;
                instancia.localRotation = Quaternion.identity;

                float kEscala = duracion > 0f ? Mathf.Clamp01(t / duracion) : kSubida;
                float escala = Mathf.Lerp(escalaInicial, escalaFinal, Mathf.SmoothStep(0f, 1f, kEscala));
                instancia.localScale = escalaBase * escala;
            }
            else
            {
                float intensidad = duracion > 0f ? Mathf.Clamp01(t / duracion) : 1f;
                float carga = Mathf.SmoothStep(0f, 1f, intensidad);
                float ciclo = Mathf.Max(0.25f, periodo * Mathf.Lerp(1f, 0.45f, carga));
                float fase = Mathf.PingPong(t / ciclo, 1f);
                instancia.localPosition = offset + Vector3.Lerp(adelante, arriba, Mathf.SmoothStep(0f, 1f, fase));
                instancia.localRotation = Quaternion.identity;
                instancia.localScale = escalaBase * Mathf.Lerp(escalaInicial, escalaFinal, carga);
            }

            t += Time.unscaledDeltaTime;
            if (duracion > 0f && t >= duracion) break;
            yield return null;
        }
    }
}
