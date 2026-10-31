#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Las ventanas de los portales entre la Piedra Ancestral (MainWorld) y el Sendero: el mismo óvalo
/// de la cinemática final («El Sendero/Ventana de portal»), con una panorámica del otro mundo dentro.
///   · Ventana(): la pieza que ponen los constructores (UmbralDelSendero, VestidoDelSendero).
///   · Fotografiar las vistas: hace las dos panorámicas desde donde se aparece al cruzar y las deja en
///     los materiales de las ventanas. Rehacerlas cuando cambie el arte de cualquiera de los dos lados.
///     Deja también una foto de cada ventana montada en Guiones/Sendero/fotos/ para revisarla.
public static class VistaDelPortal
{
    const string Carpeta = "Assets/Art/World/Sendero/Portal";
    public const string HaciaElSendero = "HaciaElSendero";
    public const string HaciaLaPiedra = "HaciaLaPiedra";
    const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    const string RutaSendero = "Assets/Scenes/Worlds/Sendero.unity";
    const int Cara = 1024, AnchoPanorama = 4096;

    // Ojo de quien acaba de cruzar: la llegada al Sendero y el portal de la Piedra.
    static readonly Vector3 OjoEnLaPiedra = new(565f, 0f, -508.6f);
    const float AlturaDelOjo = 1.5f;

    static readonly Color ColorDeLaLuz = new(0.45f, 0.75f, 1f);

    [MenuItem("El Sendero/Sendero/Vistas de los portales (fotografiar)")]
    public static void FotografiarLasVistas()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Las vistas se fotografían fuera de Play.");
        Directory.CreateDirectory(Carpeta);
        var informe = new List<string>();

        var sendero = Abrir(RutaSendero, out bool cerrarSendero);
        Vector3 llegada = AnclaDeLlegada(sendero);
        var panoramaSendero = Fotografiar(sendero, llegada + Vector3.up * AlturaDelOjo, "Panorama_Sendero");
        informe.Add("Sendero desde " + llegada);

        var mundo = Abrir(RutaMainWorld, out bool cerrarMundo);
        Vector3 ojoPiedra = SueloEn(OjoEnLaPiedra) + Vector3.up * AlturaDelOjo;
        var panoramaPiedra = Fotografiar(mundo, ojoPiedra, "Panorama_PiedraAncestral");
        informe.Add("Piedra desde " + ojoPiedra);

        Material(HaciaElSendero).SetTexture("_Panorama", panoramaSendero);
        Material(HaciaLaPiedra).SetTexture("_Panorama", panoramaPiedra);
        AssetDatabase.SaveAssets();

        FotoDeLaVentana(mundo, "portal_en_la_piedra");
        FotoDeLaVentana(sendero, "portal_en_el_sendero");

        if (cerrarMundo) EditorSceneManager.CloseScene(mundo, true);
        if (cerrarSendero) EditorSceneManager.CloseScene(sendero, true);
        Debug.Log("[VistaDelPortal] Vistas hechas: " + string.Join(" · ", informe));
    }

    // ── La pieza ────────────────────────────────────────────────────────────────────────────

    /// El material de una ventana (se crea si falta; la panorámica la pone FotografiarLasVistas).
    public static Material Material(string nombre)
    {
        Directory.CreateDirectory(Carpeta);
        string ruta = $"{Carpeta}/VentanaDePortal_{nombre}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            var shader = Shader.Find("El Sendero/Ventana de portal")
                ?? throw new InvalidOperationException("Falta el shader «El Sendero/Ventana de portal».");
            mat = new Material(shader) { name = "VentanaDePortal_" + nombre };
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.SetFloat("_EnPantalla", 0f);
        mat.SetFloat("_VistaPanoramica", 1f);
        mat.EnableKeyword("_VISTA_PANORAMICA");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// Ventana de pie sobre el suelo de <paramref name="padre"/>. <paramref name="entrada"/>: hacia dónde
    /// se camina para cruzar; <paramref name="miradaAlOtroLado"/>: hacia dónde se mira al aparecer allí.
    public static GameObject Ventana(Transform padre, string material, Vector3 entrada, Vector3 miradaAlOtroLado)
    {
        var go = new GameObject("Ventana del portal");
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        go.transform.rotation = Quaternion.LookRotation(entrada, Vector3.up);
        go.transform.localScale = new Vector3(2.1f, 2.8f, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Material(material);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // La luz azul del portal sobre el suelo y quien está delante.
        var lgo = new GameObject("Luz del portal");
        lgo.transform.SetParent(padre, false);
        lgo.transform.position = go.transform.position - entrada.normalized * 1.2f;
        var luz = lgo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = ColorDeLaLuz;
        luz.intensity = 2.5f;
        luz.range = 9f;
        luz.shadows = LightShadows.None;

        var ventana = go.AddComponent<VentanaDePortal>();
        var so = new SerializedObject(ventana);
        so.FindProperty("miradaAlOtroLado").vector3Value = miradaAlOtroLado;
        so.FindProperty("luz").objectReferenceValue = luz;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // ── Panorámicas ────────────────────────────────────────────────────────────────────────

    /// Seis caras de 90° desde <paramref name="ojo"/>, pasadas a una panorámica equirrectangular
    /// (u = atan2(x, z), v = asin(y): la misma cuenta que el shader). Solo se ve esa escena, sin
    /// personajes ni ventanas de portal.
    static Texture2D Fotografiar(Scene escena, Vector3 ojo, string nombre)
    {
        var activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(escena);
        var apagados = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var otra = SceneManager.GetSceneAt(i);
            if (otra == escena || !otra.isLoaded) continue;
            foreach (var r in otra.GetRootGameObjects())
                if (r.activeSelf) { r.SetActive(false); apagados.Add(r); }
        }
        var ocultos = new List<Renderer>();
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var a in raiz.GetComponentsInChildren<NPCSimpleAnimator>(true))
                Ocultar(a.GetComponentsInChildren<Renderer>(true), ocultos);
            foreach (var v in raiz.GetComponentsInChildren<VentanaDePortal>(true))
                Ocultar(v.GetComponentsInChildren<Renderer>(true), ocultos);
        }

        var go = new GameObject("Cámara de la vista del portal (temporal)");
        SceneManager.MoveGameObjectToScene(go, escena);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 90f;
        cam.aspect = 1f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 3000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var rt = new RenderTexture(Cara, Cara, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var lectura = new Texture2D(Cara, Cara, TextureFormat.RGBA32, false, false);
        var caras = new Color32[6][];
        var giros = new Quaternion[6];
        Vector3[] frentes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        try
        {
            go.transform.position = ojo;
            for (int c = 0; c < 6; c++)
            {
                giros[c] = Quaternion.LookRotation(frentes[c], Mathf.Abs(frentes[c].y) > 0.5f ? Vector3.forward : Vector3.up);
                go.transform.rotation = giros[c];
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                lectura.ReadPixels(new Rect(0, 0, Cara, Cara), 0, 0);
                lectura.Apply();
                caras[c] = lectura.GetPixels32();
            }
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(lectura);
            foreach (var r in ocultos) if (r != null) r.forceRenderingOff = false;
            foreach (var r in apagados) if (r != null) r.SetActive(true);
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
        }

        var panorama = Equirrectangular(caras, giros);
        string ruta = $"{Carpeta}/{nombre}.png";
        File.WriteAllBytes(ruta, panorama.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(panorama);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(ruta) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.maxTextureSize = AnchoPanorama;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    /// Foto de la ventana de la escena desde unos pasos por delante, encendida aunque su tramo no haya
    /// empezado (se deja como estaba).
    static void FotoDeLaVentana(Scene escena, string nombre)
    {
        VentanaDePortal ventana = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            ventana = raiz.GetComponentInChildren<VentanaDePortal>(true);
            if (ventana != null) break;
        }
        if (ventana == null) return;

        var apagados = new List<GameObject>();
        for (var t = ventana.transform; t != null; t = t.parent)
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); apagados.Add(t.gameObject); }
        var activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(escena);
        var otras = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var otra = SceneManager.GetSceneAt(i);
            if (otra == escena || !otra.isLoaded) continue;
            foreach (var r in otra.GetRootGameObjects())
                if (r.activeSelf) { r.SetActive(false); otras.Add(r); }
        }

        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "Sendero", "fotos");
        Directory.CreateDirectory(carpeta);
        var go = new GameObject("Cámara de la foto del portal (temporal)");
        SceneManager.MoveGameObjectToScene(go, escena);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 2000f;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            Transform v = ventana.transform;
            go.transform.position = v.position - v.forward * 7f + v.right * 1.2f + Vector3.up * 0.4f;
            go.transform.rotation = Quaternion.LookRotation(v.position - go.transform.position);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            foreach (var r in otras) if (r != null) r.SetActive(true);
            foreach (var a in apagados) if (a != null) a.SetActive(false);
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
        }
    }

    static void Ocultar(Renderer[] renderers, List<Renderer> ocultos)
    {
        foreach (var r in renderers)
            if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; ocultos.Add(r); }
    }

    static Texture2D Equirrectangular(Color32[][] caras, Quaternion[] giros)
    {
        int ancho = AnchoPanorama, alto = AnchoPanorama / 2;
        var inversos = new Quaternion[6];
        for (int c = 0; c < 6; c++) inversos[c] = Quaternion.Inverse(giros[c]);
        var pixeles = new Color32[ancho * alto];
        for (int j = 0; j < alto; j++)
        {
            float lat = ((j + 0.5f) / alto - 0.5f) * Mathf.PI;
            float cl = Mathf.Cos(lat), sl = Mathf.Sin(lat);
            for (int i = 0; i < ancho; i++)
            {
                float lon = ((i + 0.5f) / ancho - 0.5f) * 2f * Mathf.PI;
                var d = new Vector3(cl * Mathf.Sin(lon), sl, cl * Mathf.Cos(lon));
                float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
                int c = ax >= ay && ax >= az ? (d.x > 0 ? 0 : 1) : (ay >= az ? (d.y > 0 ? 2 : 3) : (d.z > 0 ? 4 : 5));
                Vector3 l = inversos[c] * d;
                float u = (l.x / l.z * 0.5f + 0.5f) * (Cara - 1), v = (l.y / l.z * 0.5f + 0.5f) * (Cara - 1);
                pixeles[j * ancho + i] = Bilineal(caras[c], u, v);
            }
        }
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false, false);
        tex.SetPixels32(pixeles);
        tex.Apply();
        return tex;
    }

    static Color32 Bilineal(Color32[] cara, float u, float v)
    {
        int x0 = Mathf.Clamp((int)u, 0, Cara - 1), y0 = Mathf.Clamp((int)v, 0, Cara - 1);
        int x1 = Mathf.Min(x0 + 1, Cara - 1), y1 = Mathf.Min(y0 + 1, Cara - 1);
        float fx = Mathf.Clamp01(u - x0), fy = Mathf.Clamp01(v - y0);
        Color32 a = Color32.Lerp(cara[y0 * Cara + x0], cara[y0 * Cara + x1], fx);
        Color32 b = Color32.Lerp(cara[y1 * Cara + x0], cara[y1 * Cara + x1], fx);
        return Color32.Lerp(a, b, fy);
    }

    // ── Escenas ────────────────────────────────────────────────────────────────────────────

    static Scene Abrir(string ruta, out bool cerrar)
    {
        var escena = SceneManager.GetSceneByPath(ruta);
        cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        return escena;
    }

    static Vector3 AnclaDeLlegada(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var a in raiz.GetComponentsInChildren<SpawnAnchor>(true))
                if (a.anchorId == UmbralDelSendero.AnclaLlegada) return a.transform.position;
        throw new InvalidOperationException("Sendero.unity: falta el ancla de llegada " + UmbralDelSendero.AnclaLlegada);
    }

    static Vector3 SueloEn(Vector3 p)
    {
        var impactos = Physics.RaycastAll(new Vector3(p.x, 200f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in impactos)
            if (h.collider is TerrainCollider || h.collider.gameObject.layer == LayerMask.NameToLayer("Floor"))
                return h.point;
        return new Vector3(p.x, 18f, p.z);
    }
}
#endif
