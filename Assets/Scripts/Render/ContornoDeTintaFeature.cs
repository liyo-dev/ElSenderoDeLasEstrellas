using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// Contorno de tinta por post-proceso: un único pase de pantalla que detecta siluetas (saltos de
/// profundidad) y pliegues (cambios de normal) y oscurece ahí el color de la escena. Lee las
/// texturas de profundidad y normales que el SSAO ya pide, así que no añade prepasses, y su coste
/// es fijo: no depende de cuántos objetos hay en pantalla (a diferencia del outline por geometría
/// de Quibli). Va justo después del cielo: el agua, las partículas y los VFX se dibujan encima sin
/// contorno, y el post de Quibli lo tiñe igual que al resto. Ajustes en <see cref="ContornoDeTinta"/>.
public sealed class ContornoDeTintaFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    private Material _material;
    private PaseDeContorno _pase;

    public override void Create()
    {
#if UNITY_EDITOR
        if (shader == null) shader = Shader.Find(PaseDeContorno.NombreDelShader);
#endif
        if (shader == null) return;
        _material = CoreUtils.CreateEngineMaterial(shader);
        _pase = new PaseDeContorno(_material) { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pase == null || _material == null) return;
        ref var camara = ref renderingData.cameraData;
        if (camara.cameraType != CameraType.Game && camara.cameraType != CameraType.SceneView) return;
        if (!camara.postProcessEnabled) return;
        // Con MSAA el color de cámara es multimuestra y no se puede sustituir por el resultado.
        if (camara.cameraTargetDescriptor.msaaSamples > 1) return;
        var ajustes = VolumeManager.instance.stack.GetComponent<ContornoDeTinta>();
        if (ajustes == null || !ajustes.IsActive()) return;

        _pase.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        renderer.EnqueuePass(_pase);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
        _pase = null;
    }

    private sealed class PaseDeContorno : ScriptableRenderPass
    {
        public const string NombreDelShader = "Hidden/El Sendero/Contorno de tinta";

        private static readonly int TintaId = Shader.PropertyToID("_Tinta");
        private static readonly int UmbralesId = Shader.PropertyToID("_Umbrales");
        private static readonly int OpcionesId = Shader.PropertyToID("_Opciones");

        private readonly Material _material;

        private sealed class Datos
        {
            public Material material;
            public TextureHandle origen;
        }

        public PaseDeContorno(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("Contorno de tinta");
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var recursos = frameData.Get<UniversalResourceData>();
            var camara = frameData.Get<UniversalCameraData>();
            if (recursos.isActiveTargetBackBuffer) return;
            if (!recursos.cameraDepthTexture.IsValid() || !recursos.cameraNormalsTexture.IsValid()) return;

            var ajustes = VolumeManager.instance.stack.GetComponent<ContornoDeTinta>();
            if (ajustes == null || !ajustes.IsActive()) return;

            float alto = camara.cameraTargetDescriptor.height;
            float grosor = Mathf.Max(1f, Mathf.Round(ajustes.grosor.value * alto / 1080f));
            float inicio = ajustes.inicioDelDesvanecido.value;
            float fin = Mathf.Max(inicio + 0.01f, ajustes.finDelDesvanecido.value);
            Color tinta = ajustes.tinta.value;
            _material.SetVector(TintaId, new Vector4(tinta.r, tinta.g, tinta.b, ajustes.intensidad.value));
            _material.SetVector(UmbralesId, new Vector4(ajustes.umbralDeSilueta.value, ajustes.umbralDePliegue.value, inicio, fin));
            _material.SetVector(OpcionesId, new Vector4(grosor, ajustes.pesoDePliegues.value, 0f, 0f));

            TextureHandle origen = recursos.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(origen);
            descriptor.name = "_ContornoDeTinta";
            descriptor.clearBuffer = false;
            descriptor.msaaSamples = MSAASamples.None;
            TextureHandle destino = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddRasterRenderPass<Datos>("Contorno de tinta", out var datos, profilingSampler))
            {
                datos.material = _material;
                datos.origen = origen;
                builder.UseTexture(origen);
                builder.UseTexture(recursos.cameraDepthTexture);
                builder.UseTexture(recursos.cameraNormalsTexture);
                builder.SetRenderAttachment(destino, 0);
                builder.SetRenderFunc(static (Datos d, RasterGraphContext contexto) =>
                    Blitter.BlitTexture(contexto.cmd, d.origen, new Vector4(1f, 1f, 0f, 0f), d.material, 0));
            }

            // El resultado pasa a ser el color de cámara: sin copia de vuelta.
            recursos.cameraColor = destino;
        }
    }
}
