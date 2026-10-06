using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sendero.Core.Feedback;

/// Hace crecer dos efectos existentes hasta mezclarlos y entrega el blanco al fundido oficial.
[Serializable]
public class FusionDeHechizosBeat : SequenceBeat
{
    [Tooltip("VFX registrado que representa el efecto A. Vacío usa el prefab.")]
    public string efectoARegistrado = "CARGA_ESFERA";
    public GameObject prefabA;
    public string actorAId;
    public Vector3 offsetA = new Vector3(0f, 1.2f, 0f);
    [Tooltip("VFX registrado que representa el efecto B. Vacío usa el prefab.")]
    public string efectoBRegistrado;
    public GameObject prefabB;
    public string actorBId;
    public Vector3 offsetB = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Marca opcional del encuentro. Vacía calcula el punto medio de los efectos.")]
    public string puntoDeEncuentro;
    public string registrarPuntoComo = "FUSION_ENCUENTRO";
    [Min(0f)] public float duracionCrecimiento = 4f;
    [Min(0f)] public float duracionMezcla = 2f;
    [Min(0f)] public float duracionBlanco = 0.4f;
    [Min(1f)] public float crecimientoA = 3f;
    [Min(1f)] public float crecimientoB = 3f;
    [Tooltip("Curva de crecimiento simultáneo, en tiempo real.")]
    public AnimationCurve curvaCrecimiento = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));
    [Tooltip("Radio inicial de respaldo si el efecto no contiene geometría medible.")]
    [Min(0.05f)] public float radioBaseA = 0.6f;
    [Min(0.05f)] public float radioBaseB = 0.6f;
    public Color colorSombra = new Color(0.08f, 0.01f, 0.16f, 1f);
    public Color colorLuz = new Color(0.85f, 0.94f, 1f, 1f);
    [Min(0f)] public float velocidadRemolino = 2f;
    [Min(0f)] public float temblorMaximo = 0.35f;
    public string eventKeySubida;
    public string eventKeyExplosion;
    public bool esperar = true;

    public override string Describe() => $"Fusión: {efectoARegistrado} + {efectoBRegistrado}";

    public override IEnumerator Run(SequenceContext ctx)
    {
        if (ctx?.Player == null) yield break;
        if (esperar) yield return Fusionar(ctx);
        else ctx.Player.TrackBackgroundRoutine(ctx.Player.StartCoroutine(Fusionar(ctx)));
    }

    private sealed class Efecto
    {
        public Transform transform;
        public VfxPoolService pool;
        public ulong uso;
        public Vector3 escala, posicionLocal;
        public float radio;
        public bool creado;
        public bool Vigente => transform != null && pool != null && pool.ObtenerUso(transform) == uso;
        public void Restaurar()
        {
            if (!Vigente)
            {
                // Un uso ya recogido puede conservar la escala, pero nunca se toca uno reutilizado.
                if (transform != null && pool != null && pool.ObtenerUso(transform) == 0
                    && !transform.gameObject.activeSelf) transform.localScale = escala;
                return;
            }
            transform.localScale = escala;
            transform.localPosition = posicionLocal;
            if (creado) pool.Recoger(transform, uso);
        }
    }

    private Efecto Obtener(SequenceContext ctx, string registrado, GameObject prefab,
        string actorId, Vector3 offset, float radioBase)
    {
        Transform instancia;
        VfxPoolService pool;
        ulong uso;
        bool creado = false;
        if (!ctx.TryGetVfx(registrado, out instancia, out pool, out uso))
        {
            pool = VfxPoolService.Instance;
            var actor = ctx.GetActor(actorId);
            if (pool == null || prefab == null || actor?.Transform == null) return null;
            // El pool cuenta tiempo escalado: una vida holgada protege también una subida acelerada.
            float vida = Mathf.Max(60f, (duracionCrecimiento + duracionMezcla + duracionBlanco + 1f) * 100f);
            instancia = pool.Play(prefab, actor.Transform.position + offset, Quaternion.identity, vida);
            if (instancia == null) return null;
            uso = pool.ObtenerUso(instancia);
            creado = true;
        }
        float radio = Mathf.Max(0.05f, radioBase);
        foreach (var renderer in instancia.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
            radio = Mathf.Max(radio, Vector3.Distance(instancia.position, renderer.bounds.center)
                + renderer.bounds.extents.magnitude);
        }
        return new Efecto { transform = instancia, pool = pool, uso = uso, creado = creado,
            escala = instancia.localScale, posicionLocal = instancia.localPosition, radio = radio };
    }

    private IEnumerator Fusionar(SequenceContext ctx)
    {
        Efecto a = Obtener(ctx, efectoARegistrado, prefabA, actorAId, offsetA, radioBaseA);
        Efecto b = Obtener(ctx, efectoBRegistrado, prefabB, actorBId, offsetB, radioBaseB);
        if (a == null || b == null || a.transform == b.transform)
        {
            a?.Restaurar(); b?.Restaurar();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[FusionDeHechizosBeat] Se necesitan dos VFX distintos y vigentes.");
#endif
            yield break;
        }

        GameObject velo = null, punto = null;
        Texture2D textura = null;
        bool limpio = false, temblando = false, fundiendo = false, blancoEntregado = false;
        Color fundidoInicial = FeedbackService.ColorDelFundido;
        Action limpiar = () =>
        {
            if (limpio) return;
            limpio = true;
            a.Restaurar(); b.Restaurar();
            if (velo != null) UnityEngine.Object.Destroy(velo);
            if (textura != null) UnityEngine.Object.Destroy(textura);
            if (punto != null)
            {
                if (ctx.EstaRegistrado(registrarPuntoComo)
                    && ctx.GetActor(registrarPuntoComo)?.Transform == punto.transform)
                    ctx.UnregisterActor(registrarPuntoComo);
                UnityEngine.Object.Destroy(punto);
            }
            if (temblando) FeedbackService.CancelAllShakes();
            if (fundiendo && !blancoEntregado) FeedbackService.SetScreenFadeImmediate(fundidoInicial);
        };
        ctx.Player.RegisterCleanup(limpiar);
        try
        {
            Vector3 origenA = a.transform.position, origenB = b.transform.position;
            Transform marca = !string.IsNullOrWhiteSpace(puntoDeEncuentro) && ctx.Stage != null
                ? ctx.Stage.GetMark(puntoDeEncuentro) : null;
            Vector3 encuentro = marca != null ? marca.position : (origenA + origenB) * 0.5f;
            if (!string.IsNullOrWhiteSpace(registrarPuntoComo))
            {
                punto = new GameObject("PuntoDeFusion");
                punto.transform.position = encuentro;
                ctx.RegisterActor(registrarPuntoComo, punto.transform);
            }
            if (!string.IsNullOrWhiteSpace(eventKeySubida))
                AudioService.Instance?.PlaySFX(eventKeySubida, 1f, encuentro);

            float duracion = Mathf.Max(0f, duracionCrecimiento);
            float t = 0f;
            float factorA = Mathf.Max(1f, crecimientoA), factorB = Mathf.Max(1f, crecimientoB);
            Vector3 centroA = Vector3.Lerp(origenA, encuentro, 0.9f);
            Vector3 centroB = Vector3.Lerp(origenB, encuentro, 0.9f);
            // Al acabar la primera fase los volúmenes se tocan incluso con orígenes muy separados.
            float contacto = Vector3.Distance(centroA, centroB) / (a.radio * factorA + b.radio * factorB);
            factorA *= Mathf.Max(1f, contacto); factorB *= Mathf.Max(1f, contacto);
            do
            {
                if (!a.Vigente || !b.Vigente) yield break;
                t += Time.unscaledDeltaTime;
                float k = duracion > 0f ? Mathf.Clamp01(t / duracion) : 1f;
                float e = k >= 1f ? 1f : Mathf.Clamp01(curvaCrecimiento != null ? curvaCrecimiento.Evaluate(k) : k * k);
                a.transform.position = Vector3.Lerp(origenA, centroA, e);
                b.transform.position = Vector3.Lerp(origenB, centroB, e);
                a.transform.localScale = a.escala * Mathf.Lerp(1f, factorA, e);
                b.transform.localScale = b.escala * Mathf.Lerp(1f, factorB, e);
                yield return null;
            } while (t < duracion);

            velo = new GameObject("VeloDeFusion", typeof(RectTransform), typeof(Canvas));
            velo.transform.SetParent(ctx.Player.transform, false);
            var canvas = velo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9997;
            var imagenGo = new GameObject("Remolino", typeof(RectTransform), typeof(RawImage));
            imagenGo.transform.SetParent(velo.transform, false);
            var imagen = imagenGo.GetComponent<RawImage>();
            imagen.raycastTarget = false;
            imagen.rectTransform.anchorMin = Vector2.zero;
            imagen.rectTransform.anchorMax = Vector2.one;
            imagen.rectTransform.offsetMin = imagen.rectTransform.offsetMax = Vector2.zero;
            const int lado = 64;
            textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            textura.wrapMode = TextureWrapMode.Clamp;
            textura.filterMode = FilterMode.Bilinear;
            imagen.texture = textura;
            var pixeles = new Color32[lado * lado];
            var camara = ctx.Player.CachedCamera;
            t = 0f;
            float proximoTemblor = 0f;
            duracion = Mathf.Max(0f, duracionMezcla);
            do
            {
                if (!a.Vigente || !b.Vigente) yield break;
                t += Time.unscaledDeltaTime;
                float k = duracion > 0f ? Mathf.Clamp01(t / duracion) : 1f;
                float e = k * k * (3f - 2f * k);
                a.transform.position = Vector3.Lerp(centroA, encuentro, e);
                b.transform.position = Vector3.Lerp(centroB, encuentro, e);
                float radioPantalla = RadioParaCubrir(camara, encuentro);
                a.transform.localScale = a.escala * Mathf.Lerp(factorA, Mathf.Max(factorA, radioPantalla / a.radio), e);
                b.transform.localScale = b.escala * Mathf.Lerp(factorB, Mathf.Max(factorB, radioPantalla / b.radio), e);
                Pintar(pixeles, lado, t * velocidadRemolino);
                textura.SetPixels32(pixeles); textura.Apply(false, false);
                // El velo acompaña a la mezcla sin taparla: las dos esferas tienen que seguir leyéndose.
                imagen.color = new Color(1f, 1f, 1f, k * 0.7f);
                if (temblorMaximo > 0f && t >= proximoTemblor)
                {
                    FeedbackService.CameraShake(camara, temblorMaximo * k, 0.12f);
                    temblando = true; proximoTemblor = t + 0.12f;
                }
                yield return null;
            } while (t < duracion);

            if (!string.IsNullOrWhiteSpace(eventKeyExplosion))
                AudioService.Instance?.PlaySFX(eventKeyExplosion, 1f, encuentro);
            // El fundido oficial queda blanco; el siguiente ScreenFadeBeat puede destaparlo.
            fundiendo = true;
            yield return FeedbackService.ScreenFadeAsync(Color.white, Mathf.Max(0f, duracionBlanco), true);
            FeedbackService.SetScreenFadeImmediate(Color.white);
            blancoEntregado = true;
        }
        finally { limpiar(); }
    }

    private static float RadioParaCubrir(Camera camara, Vector3 centro)
    {
        if (camara == null) return 20f;
        float distancia = Vector3.Distance(camara.transform.position, centro);
        if (camara.orthographic)
            return distancia + camara.orthographicSize * Mathf.Sqrt(1f + camara.aspect * camara.aspect);
        // Radio con el que la esfera, vista desde fuera, cubre la diagonal del cuadro. Nunca llega a
        // envolver a la cámara: desde dentro una esfera deja de verse como esfera.
        float tangente = Mathf.Tan(camara.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float mitadDiagonal = Mathf.Atan(tangente * Mathf.Sqrt(1f + camara.aspect * camara.aspect));
        return Mathf.Max(1f, Mathf.Min(distancia * Mathf.Sin(mitadDiagonal) * 1.08f, distancia * 0.94f));
    }

    private void Pintar(Color32[] pixeles, int lado, float fase)
    {
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            float dx = (x + 0.5f) / lado * 2f - 1f;
            float dy = (y + 0.5f) / lado * 2f - 1f;
            float radio = Mathf.Sqrt(dx * dx + dy * dy);
            float mezcla = 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + radio * 9f - fase);
            Color color = Color.Lerp(colorSombra, colorLuz, mezcla);
            color.a = 1f; pixeles[y * lado + x] = color;
        }
    }
}
