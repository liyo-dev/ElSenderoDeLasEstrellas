using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Kits.InkOutline
{
    /// Línea de tinta por geometría para lo marcado con <see cref="InkOutlined"/>: vuelve a dibujar esos
    /// renderers con una funda un poco más grande y vuelta del revés, de grosor constante en píxeles.
    /// El resto de la escena no se toca y su coste depende solo de cuántos personajes hay en pantalla.
    public sealed class InkOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [Tooltip("Color de la línea.")]
        [SerializeField] private Color color = new Color(0.16f, 0.10f, 0.08f, 1f);
        [Tooltip("Grosor en píxeles de una pantalla de 1080 de alto (se escala con la resolución).")]
        [SerializeField, Range(0f, 6f)] private float widthPixels = 2f;
        [Tooltip("Distancia (m) desde la que la línea empieza a afinarse.")]
        [SerializeField, Min(0f)] private float thinFrom = 6f;
        [Tooltip("Distancia (m) a la que la línea llega a su grosor mínimo.")]
        [SerializeField, Min(0f)] private float thinTo = 30f;
        [Tooltip("Fracción del grosor que queda a lo lejos.")]
        [SerializeField, Range(0.1f, 1f)] private float farWidthFraction = 0.4f;
        [Tooltip("Cuánto se empuja la funda hacia el fondo (m): la tapa el propio personaje en los huecos y solo asoma por la silueta.")]
        [SerializeField, Range(0f, 0.3f)] private float depthPush = 0.06f;

        private Material _material;
        private InkOutlinePass _pass;

        private static readonly int ColorId = Shader.PropertyToID("_InkColor");
        private static readonly int WidthId = Shader.PropertyToID("_InkWidth");
        private static readonly int FadeId = Shader.PropertyToID("_InkFade");
        private static readonly int PushId = Shader.PropertyToID("_InkPush");

        public const string ShaderName = "Kits/Ink Outline";

        public override void Create()
        {
#if UNITY_EDITOR
            if (shader == null) shader = Shader.Find(ShaderName);
#endif
            if (shader == null) return;
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new InkOutlinePass(_material) { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null || widthPixels <= 0f) return;
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            _material.SetColor(ColorId, color);
            _material.SetFloat(WidthId, widthPixels);
            _material.SetVector(FadeId, new Vector4(thinFrom, thinTo, farWidthFraction, 0f));
            _material.SetFloat(PushId, depthPush);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class InkOutlinePass : ScriptableRenderPass
        {
            private static readonly List<ShaderTagId> Tags = new()
            {
                new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("UniversalForward"), new ShaderTagId("SRPDefaultUnlit")
            };

            // Solo lo opaco sin recorte: lo recortado por alfa (flores, hierba, detalles del terreno) no lleva
            // línea aunque tenga marcadas todas las capas de render.
            private static readonly RenderQueueRange SinRecorte = new(0, 2449);

            private readonly Material _material;

            private sealed class PassData { public RendererListHandle list; }

            public InkOutlinePass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("Ink outline");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();

                var drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
                drawing.overrideMaterial = _material;
                drawing.overrideMaterialPassIndex = 0;
                var filtering = new FilteringSettings(SinRecorte, ~0, InkOutlined.RenderingLayer);

                using var builder = renderGraph.AddRasterRenderPass<PassData>("Ink outline", out var data, profilingSampler);
                data.list = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                builder.UseRendererList(data.list);
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.list));
            }
        }
    }
}
