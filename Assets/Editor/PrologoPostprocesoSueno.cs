#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Crea los perfiles propios del prólogo y monta el controlador oficial de escena.
public static class PrologoPostprocesoSueno
{
    public const string Carpeta = "Assets/Art/World/Prologo_Valle";
    public const string Dia = Carpeta + "/Prologo_Dia.asset";
    public const string Amenaza = Carpeta + "/Prologo_Amenaza.asset";
    public const string Climax = Carpeta + "/Prologo_Climax.asset";
    private const string Viejo = Carpeta + "/Prologo_Sueno_Volume.asset";

    [MenuItem("El Sendero/Archivo/Prólogo: post-procesado propio", priority = 34)]
    public static void Menu()
    {
        var escena = SceneManager.GetSceneByName("Prologo_Valle");
        if (!escena.IsValid() || !escena.isLoaded)
        {
            EditorUtility.DisplayDialog("Post-procesado", "Abre Prologo_Valle primero.", "Vale");
            return;
        }
        Ejecutar(escena);
    }

    public static void Ejecutar(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return;
        CrearPerfiles();
        var go = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "POSTPROCESO_SUENO");
        if (go == null)
        {
            go = new GameObject("POSTPROCESO_SUENO");
            SceneManager.MoveGameObjectToScene(go, escena);
        }
        // El script archivado deja una referencia ausente que se retira mediante la API de editor.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.weight = 1;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Dia);
        var control = go.GetComponent<PostprocesoDeEscena>() ?? go.AddComponent<PostprocesoDeEscena>();
        control.perfilInicial = volume.sharedProfile;
        control.exclusivo = true;
        control.entrada = 1.5f;
        EditorUtility.SetDirty(volume);
        EditorUtility.SetDirty(control);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        ArchivarPerfilSinUso();
    }

    public static void CrearPerfiles()
    {
        CrearDia();
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(Amenaza) == null) Crear(Amenaza, 1);
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(Climax) == null) Crear(Climax, 2);
        AssetDatabase.SaveAssets();
    }

    public const string ReferenciaDia = "Assets/Settings/Volumenes/Volumen Profile.asset";

    private static void CrearDia()
    {
        var referencia = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ReferenciaDia);
        if (referencia == null) throw new InvalidOperationException("Falta el perfil global de referencia de MainWorld: " + ReferenciaDia);
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Dia);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, Dia);
        }
        foreach (var componente in perfil.components)
            if (componente != null) UnityEngine.Object.DestroyImmediate(componente, true);
        perfil.components.Clear();
        // Se conservan valores, estado activo y overrides, incluidos los efectos estilizados.
        foreach (var componente in referencia.components)
        {
            if (componente == null) continue;
            var copia = UnityEngine.Object.Instantiate(componente);
            copia.name = componente.name;
            perfil.components.Add(copia);
            AssetDatabase.AddObjectToAsset(copia, perfil);
            EditorUtility.SetDirty(copia);
        }
        if (!perfil.TryGet<WhiteBalance>(out var blanco))
        {
            blanco = perfil.Add<WhiteBalance>(false);
            AssetDatabase.AddObjectToAsset(blanco, perfil);
        }
        blanco.active = true;
        blanco.temperature.Override(4f);
        if (!perfil.TryGet<Bloom>(out var bloom))
        {
            bloom = perfil.Add<Bloom>(false);
            AssetDatabase.AddObjectToAsset(bloom, perfil);
        }
        bloom.active = true;
        bloom.intensity.Override(.3f);
        EditorUtility.SetDirty(blanco);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(perfil);
    }
    private static void Crear(string ruta, int ambiente)
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
        if (p == null)
        {
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ruta);
        }
        // Los valores se ajustan en el propio asset; ejecutar esta herramienta repone la propuesta.
        foreach (var c in p.components) if (c != null) UnityEngine.Object.DestroyImmediate(c, true);
        p.components.Clear();
        bool dia = ambiente == 0, climax = ambiente == 2;
        p.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
        var color = p.Add<ColorAdjustments>(true);
        color.postExposure.Override(dia ? 0.1f : climax ? -0.15f : 0);
        color.contrast.Override(dia ? 12 : climax ? 28 : 18);
        color.saturation.Override(dia ? 18 : climax ? -38 : -8);
        color.colorFilter.Override(ambiente == 1 ? new Color(0.92f, 0.93f, 1) : Color.white);
        var blanco = p.Add<WhiteBalance>(true);
        blanco.temperature.Override(dia ? 8 : climax ? -22 : -18);
        blanco.tint.Override(dia ? 0 : climax ? 8 : 6);
        var bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(dia ? 1 : climax ? 0.8f : 0.9f);
        bloom.intensity.Override(dia ? 0.5f : climax ? 1.3f : 0.85f);
        bloom.scatter.Override(dia ? 0.7f : climax ? 0.8f : 0.75f);
        bloom.tint.Override(dia ? new Color(1, 0.95f, 0.88f) : climax ? Color.white : new Color(0.85f, 0.88f, 1));
        bloom.highQualityFiltering.Override(true);
        var vin = p.Add<Vignette>(true);
        vin.color.Override(ambiente == 1 ? new Color(0.04f, 0.02f, 0.08f) : Color.black);
        vin.intensity.Override(dia ? 0.18f : climax ? 0.42f : 0.3f);
        vin.smoothness.Override(dia ? 0.5f : climax ? 0.4f : 0.45f);
        var dof = p.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(climax ? 30 : 45);
        dof.gaussianEnd.Override(climax ? 110 : 150);
        dof.gaussianMaxRadius.Override(climax ? 0.8f : 0.6f);
        dof.highQualitySampling.Override(true);
        p.Add<FilmGrain>(true).intensity.Override(0);
        var mixer = p.Add<ChannelMixer>(true);
        mixer.redOutRedIn.Override(100); mixer.redOutGreenIn.Override(0); mixer.redOutBlueIn.Override(0);
        mixer.greenOutRedIn.Override(0); mixer.greenOutGreenIn.Override(100); mixer.greenOutBlueIn.Override(0);
        mixer.blueOutRedIn.Override(0); mixer.blueOutGreenIn.Override(0); mixer.blueOutBlueIn.Override(100);
        p.Add<ChromaticAberration>(true).intensity.Override(climax ? 0.15f : 0);
        var lift = p.Add<LiftGammaGain>(true);
        lift.lift.Override(new Vector4(1, 1, 1, 0));
        lift.gamma.Override(new Vector4(1, 1, 1, 0));
        lift.gain.Override(new Vector4(1, 1, 1, 0));
        var detalle = AnadirPorNombre(p, "StylizedDetail");
        Poner(detalle, "intensity", dia ? 0.15f : climax ? 0.08f : 0.1f);
        Poner(detalle, "blur", 1.2f);
        Poner(detalle, "rangeStart", 30);
        Poner(detalle, "rangeEnd", 80);
        Poner(AnadirPorNombre(p, "ColorGrading"), "intensity", 0);
        foreach (var c in p.components)
        {
            c.SetAllOverridesTo(true);
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(c);
        }
        EditorUtility.SetDirty(p);
    }

    private static VolumeComponent AnadirPorNombre(VolumeProfile p, string nombre)
    {
        var tipo = TypeCache.GetTypesDerivedFrom<VolumeComponent>().FirstOrDefault(t =>
            t.Name == nombre && t.FullName.StartsWith("CompoundRendererFeature.PostProcess.", StringComparison.Ordinal));
        if (tipo == null) return null;
        return p.Add(tipo, true);
    }

    private static void Poner(VolumeComponent c, string campo, float valor)
    {
        if (c == null) return;
        if (c.GetType().GetField(campo, BindingFlags.Public | BindingFlags.Instance)?.GetValue(c) is VolumeParameter<float> parametro)
            parametro.Override(valor);
    }

    private static void ArchivarPerfilSinUso()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(Viejo) == null) return;
        foreach (string ruta in AssetDatabase.GetAllAssetPaths())
        {
            if (!ruta.StartsWith("Assets/", StringComparison.Ordinal) || ruta == Viejo || AssetDatabase.IsValidFolder(ruta)) continue;
            if (AssetDatabase.GetDependencies(ruta, false).Contains(Viejo)) return;
        }
        string destino = System.IO.Path.GetFullPath("Versiones antiguas/Postproceso del prologo");
        System.IO.Directory.CreateDirectory(destino);
        string archivo = System.IO.Path.Combine(destino, System.IO.Path.GetFileName(Viejo));
        if (System.IO.File.Exists(archivo)) throw new InvalidOperationException("El perfil archivado ya existe; se conserva para evitar sobrescribirlo.");
        System.IO.File.Move(Viejo, archivo);
        if (System.IO.File.Exists(Viejo + ".meta")) System.IO.File.Move(Viejo + ".meta", archivo + ".meta");
        AssetDatabase.Refresh();
    }
}
#endif