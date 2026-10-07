using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ElSendero.Editor.Iconos
{
    [Serializable]
    public sealed class AjustesDeFoto
    {
        public int tamano = 512;
        public float margen = 0.12f;
        public float yaw = 20f;
        public float pitch = 8f;
        public int grosorContorno = 6;
        public Color colorContorno = new Color(43f / 255f, 30f / 255f, 22f / 255f, 1f);
    }

    /// <summary>Fotografía geometría sin instanciar comportamientos del objeto de origen.</summary>
    public static class FotografoDeIconos
    {
        public static Texture2D Fotografiar(GameObject raiz, Func<Renderer, bool> visible, AjustesDeFoto ajustes)
        {
            if (raiz == null || ajustes == null) throw new ArgumentNullException();
            if (ajustes.tamano < 64 || ajustes.tamano > 2048)
                throw new ArgumentException("El tamaño debe estar entre 64 y 2048.");
            var escena = EditorSceneManager.NewPreviewScene();
            var mallas = new List<Mesh>();
            RenderTexture destino = null;
            var activo = RenderTexture.active;
            var ambiente = RenderSettings.ambientMode;
            var luzAmbiente = RenderSettings.ambientLight;
            var intensidadAmbiente = RenderSettings.ambientIntensity;
            var sondaAmbiente = RenderSettings.ambientProbe;
            var reflejos = RenderSettings.reflectionIntensity;
            var niebla = RenderSettings.fog;
            try
            {
                // Los shaders consultan también ajustes globales; se acotan y se restauran al terminar.
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);
                RenderSettings.ambientIntensity = 1f;
                var sonda = new SphericalHarmonicsL2();
                sonda.AddAmbientLight(RenderSettings.ambientLight);
                RenderSettings.ambientProbe = sonda;
                RenderSettings.reflectionIntensity = 0f;
                var mapa = new Dictionary<Transform, Transform>();
                CopiarJerarquia(raiz.transform, null, escena, mapa);
                var renderers = new List<Renderer>();
                foreach (var original in raiz.GetComponentsInChildren<Renderer>(true))
                {
                    if (visible != null ? !visible(original) : !original.enabled || !original.gameObject.activeInHierarchy) continue;
                    Mesh malla;
                    var copia = mapa[original.transform].gameObject;
                    if (original is SkinnedMeshRenderer piel)
                    {
                        if (piel.sharedMesh == null) continue;
                        var temporal = copia.AddComponent<SkinnedMeshRenderer>();
                        temporal.sharedMesh = piel.sharedMesh;
                        var huesos = piel.bones;
                        var nuevos = new Transform[huesos.Length];
                        for (int i = 0; i < huesos.Length; i++)
                        {
                            if (huesos[i] != null && !mapa.TryGetValue(huesos[i], out nuevos[i]))
                                throw new InvalidOperationException("La raíz debe incluir todos los huesos de " + piel.name);
                        }
                        temporal.bones = nuevos;
                        if (piel.rootBone != null && mapa.TryGetValue(piel.rootBone, out var huesoRaiz)) temporal.rootBone = huesoRaiz;
                        for (int i = 0; i < piel.sharedMesh.blendShapeCount; i++) temporal.SetBlendShapeWeight(i, piel.GetBlendShapeWeight(i));
                        malla = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                        mallas.Add(malla);
                        temporal.BakeMesh(malla, false);
                        UnityEngine.Object.DestroyImmediate(temporal);
                    }
                    else if (original is MeshRenderer && original.TryGetComponent<MeshFilter>(out var filtro)) malla = filtro.sharedMesh;
                    else continue;
                    if (malla == null || malla.vertexCount == 0) continue;
                    copia.AddComponent<MeshFilter>().sharedMesh = malla;
                    var renderer = copia.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = original.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderers.Add(renderer);
                }
                if (renderers.Count == 0) throw new InvalidOperationException("No hay mallas fotografiables en la selección.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var camara = CrearObjeto("Cámara de iconos", escena).AddComponent<Camera>();
                camara.enabled = false;
                camara.scene = escena;
                camara.cameraType = CameraType.Preview;
                camara.clearFlags = CameraClearFlags.SolidColor;
                camara.orthographic = true;
                camara.aspect = 1f;
                camara.allowHDR = false;
                camara.allowMSAA = false;
                camara.useOcclusionCulling = false;
                var datos = camara.GetUniversalAdditionalCameraData();
                datos.renderPostProcessing = false;
                datos.renderShadows = false;
                datos.requiresColorOption = CameraOverrideOption.Off;
                datos.requiresDepthOption = CameraOverrideOption.Off;
                datos.volumeLayerMask = 0;
                // El ángulo describe desde dónde se mira: cero muestra el frente (+Z).
                var vista = Quaternion.Euler(ajustes.pitch, ajustes.yaw + 180f, 0f);
                float radio = Mathf.Max(bounds.size.magnitude, 0.01f);
                camara.transform.SetPositionAndRotation(bounds.center - vista * Vector3.forward * (radio * 2f), vista);
                float extension = 0f;
                for (int i = 0; i < 8; i++)
                {
                    var esquina = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = camara.transform.InverseTransformPoint(esquina);
                    extension = Mathf.Max(extension, Mathf.Abs(local.x), Mathf.Abs(local.y));
                }
                float borde = Mathf.Clamp(ajustes.grosorContorno, 0, ajustes.tamano / 8) / (float)ajustes.tamano;
                camara.orthographicSize = Mathf.Max(0.001f, extension) * (1f + Mathf.Max(0.02f, ajustes.margen)) / (1f - 2f * borde);
                camara.nearClipPlane = Mathf.Max(0.001f, radio * 0.01f);
                camara.farClipPlane = radio * 4f;
                CrearLuz(escena, vista * Quaternion.Euler(35f, -35f, 0f), 1.1f);
                CrearLuz(escena, vista * Quaternion.Euler(-15f, 45f, 0f), 0.45f);
                int lado = ajustes.tamano * 2;
                destino = new RenderTexture(lado, lado, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                destino.Create();
                var negro = Renderizar(camara, destino, Color.black);
                var blanco = Renderizar(camara, destino, Color.white);
                var pixeles = new Color[ajustes.tamano * ajustes.tamano];
                bool haySilueta = false;
                for (int y = 0; y < ajustes.tamano; y++)
                for (int x = 0; x < ajustes.tamano; x++)
                {
                    Vector3 premultiplicado = Vector3.zero;
                    float alfa = 0f;
                    for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int i = (y * 2 + dy) * lado + x * 2 + dx;
                        var n = negro[i]; var b = blanco[i];
                        float a = Mathf.Clamp01(1f - ((b.r - n.r) + (b.g - n.g) + (b.b - n.b)) / 3f);
                        if (a < 0.005f) continue;
                        alfa += a * 0.25f;
                        premultiplicado += new Vector3(n.r, n.g, n.b) * 0.25f;
                    }
                    var color = alfa > 0f ? new Color(premultiplicado.x / alfa, premultiplicado.y / alfa, premultiplicado.z / alfa, alfa) : Color.clear;
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    {
                        color = color.gamma;
                        color.a = alfa;
                    }
                    pixeles[y * ajustes.tamano + x] = color;
                    haySilueta |= alfa > 0.01f;
                }
                if (!haySilueta) throw new InvalidOperationException("La fotografía está vacía. Revisa el material y el renderer de URP.");
                AplicarContorno(pixeles, ajustes.tamano, Mathf.Clamp(ajustes.grosorContorno, 0, ajustes.tamano / 8), ajustes.colorContorno);
                var textura = new Texture2D(ajustes.tamano, ajustes.tamano, TextureFormat.RGBA32, false)
                    { hideFlags = HideFlags.HideAndDontSave, name = "Fotografía de icono" };
                textura.SetPixels(pixeles);
                textura.Apply(false, false);
                return textura;
            }
            finally
            {
                RenderTexture.active = activo;
                RenderSettings.ambientMode = ambiente;
                RenderSettings.ambientLight = luzAmbiente;
                RenderSettings.ambientIntensity = intensidadAmbiente;
                RenderSettings.ambientProbe = sondaAmbiente;
                RenderSettings.reflectionIntensity = reflejos;
                RenderSettings.fog = niebla;
                if (destino != null) { destino.Release(); UnityEngine.Object.DestroyImmediate(destino); }
                EditorSceneManager.ClosePreviewScene(escena);
                foreach (var malla in mallas) UnityEngine.Object.DestroyImmediate(malla);
            }
        }

        public static void GuardarPng(Texture2D textura, string ruta) => File.WriteAllBytes(ruta, textura.EncodeToPNG());

        static GameObject CrearObjeto(string nombre, Scene escena)
        {
            var objeto = new GameObject(nombre) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(objeto, escena);
            return objeto;
        }

        static void CopiarJerarquia(Transform origen, Transform padre, Scene escena, Dictionary<Transform, Transform> mapa)
        {
            var copia = CrearObjeto(origen.name, escena).transform;
            copia.SetParent(padre, false);
            copia.localPosition = padre == null ? Vector3.zero : origen.localPosition;
            copia.localRotation = padre == null ? Quaternion.identity : origen.localRotation;
            copia.localScale = origen.localScale;
            mapa.Add(origen, copia);
            foreach (Transform hijo in origen) CopiarJerarquia(hijo, copia, escena, mapa);
        }

        static void CrearLuz(Scene escena, Quaternion rotacion, float intensidad)
        {
            var luz = CrearObjeto("Luz de iconos", escena).AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = intensidad;
            luz.shadows = LightShadows.None;
            luz.transform.rotation = rotacion;
        }

        static Color[] Renderizar(Camera camara, RenderTexture destino, Color fondo)
        {
            camara.backgroundColor = fondo;
            camara.targetTexture = destino;
            if (GraphicsSettings.currentRenderPipeline == null) camara.Render();
            else
            {
                var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = destino };
                if (!RenderPipeline.SupportsRenderRequest(camara, peticion))
                    throw new NotSupportedException("El pipeline activo no admite fotografías con SingleCameraRequest.");
                RenderPipeline.SubmitRenderRequest(camara, peticion);
            }
            var lectura = new Texture2D(destino.width, destino.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = destino;
                lectura.ReadPixels(new Rect(0, 0, destino.width, destino.height), 0, 0);
                lectura.Apply();
                return lectura.GetPixels();
            }
            finally { UnityEngine.Object.DestroyImmediate(lectura); }
        }

        static void AplicarContorno(Color[] pixeles, int lado, int grosor, Color tinta)
        {
            if (grosor <= 0 || tinta.a <= 0f) return;
            // La distancia en dos pasadas aproxima una dilatación circular sin recorrer un disco por píxel.
            var distancia = new float[pixeles.Length];
            for (int i = 0; i < distancia.Length; i++) distancia[i] = pixeles[i].a > 0.01f ? 1f - pixeles[i].a : lado * 2f;
            for (int pasada = 0; pasada < 2; pasada++)
            {
                int paso = pasada == 0 ? 1 : -1;
                for (int y = pasada == 0 ? 0 : lado - 1; y >= 0 && y < lado; y += paso)
                for (int x = pasada == 0 ? 0 : lado - 1; x >= 0 && x < lado; x += paso)
                {
                    int i = y * lado + x;
                    int anteriorX = x - paso, anteriorY = y - paso;
                    if (anteriorX >= 0 && anteriorX < lado) distancia[i] = Mathf.Min(distancia[i], distancia[y * lado + anteriorX] + 1f);
                    if (anteriorY < 0 || anteriorY >= lado) continue;
                    for (int dx = -1; dx <= 1; dx++)
                        if (x + dx >= 0 && x + dx < lado)
                            distancia[i] = Mathf.Min(distancia[i], distancia[anteriorY * lado + x + dx] + (dx == 0 ? 1f : 1.414214f));
                }
            }
            for (int i = 0; i < pixeles.Length; i++)
            {
                var pieza = pixeles[i];
                float detras = Mathf.Clamp01(grosor + 0.5f - distancia[i]) * tinta.a * (1f - pieza.a);
                float alfa = pieza.a + detras;
                pixeles[i] = alfa > 0f ? new Color((pieza.r * pieza.a + tinta.r * detras) / alfa,
                    (pieza.g * pieza.a + tinta.g * detras) / alfa, (pieza.b * pieza.a + tinta.b * detras) / alfa, alfa) : Color.clear;
            }
        }
    }
}
