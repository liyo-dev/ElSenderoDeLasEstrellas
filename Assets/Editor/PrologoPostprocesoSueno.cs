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

/// Crea (o rehace) el post-procesado de sueño del prólogo (INC-422).
///
/// «El arte debe ser estilo Quibli, ya que es un sueño estoy pensando en un postprocesado que pueda
/// estar chulo» (Raúl, 24 sep). La idea es que el prólogo se lea como un recuerdo pintado:
///
///   · Quibli / Stylized Detail — el efecto propio de Quibli: suaviza el fondo como si estuviera
///     pintado a pincel y conserva los contornos. Solo a partir de 12 m: los personajes quedan
///     nítidos. Es lo que más «Quibli» hace la imagen.
///   · Quibli / Stylized Color Grading — sombras hacia el azul, luces hacia el cálido, algo de
///     vibración: la paleta de una ilustración, no de una foto.
///   · Bloom suave y amplio — las luces «respiran», como en un sueño.
///   · Viñeta morada y blanda — el borde del sueño, sin llegar a túnel.
///   · Profundidad de campo gaussiana, lejos (desde 28 m) — el fondo se va, lo cercano no.
///   · Ajuste de color muy leve — un pelo más de saturación y menos contraste.
///
/// Todo va en un Volume GLOBAL dentro de Prologo_Valle, así que solo existe mientras dura el sueño
/// y se va con la escena. Valores de partida: se retocan en el asset sin tocar código.
public static class PrologoPostprocesoSueno
{
    private const string Carpeta = "Assets/Art/World/Prologo_Valle";
    private const string RutaPerfil = Carpeta + "/Prologo_Sueno_Volume.asset";
    private const string NombreObjeto = "POSTPROCESO_SUENO";

    [MenuItem("El Sendero/Prólogo: post-procesado de sueño (Quibli)", priority = 34)]
    public static void Menu()
    {
        var escena = SceneManager.GetSceneByName("Prologo_Valle");
        if (!escena.IsValid() || !escena.isLoaded)
        {
            EditorUtility.DisplayDialog("Post-procesado de sueño", "Abre Prologo_Valle primero.", "Vale");
            return;
        }
        Ejecutar(escena);
        EditorUtility.DisplayDialog("Post-procesado de sueño",
            "Listo: Volume global 'POSTPROCESO_SUENO' en Prologo_Valle con el perfil " +
            "Prologo_Sueno_Volume. Guarda con Ctrl+S.", "Vale");
    }

    public static void Ejecutar(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return;

        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaPerfil);
        if (perfil == null)
        {
            if (!AssetDatabase.IsValidFolder(Carpeta))
                System.IO.Directory.CreateDirectory(Carpeta);
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, RutaPerfil);
            Rellenar(perfil);
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Sueño] Perfil nuevo: {RutaPerfil}.");
        }
        // Si ya existía NO se pisa: puede que Raúl lo haya retocado a mano.

        GameObject go = escena.GetRootGameObjects().FirstOrDefault(g => g.name == NombreObjeto);
        if (go == null)
        {
            go = new GameObject(NombreObjeto);
            SceneManager.MoveGameObjectToScene(go, escena);
        }
        var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.sharedProfile = perfil;
        if (go.GetComponent<PostprocesoDelSueno>() == null) go.AddComponent<PostprocesoDelSueno>();

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"[Sueño] Volume '{NombreObjeto}' en {escena.name} con '{perfil.name}'.");
    }

    private static void Rellenar(VolumeProfile p)
    {
        var bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.3f);
        bloom.scatter.Override(0.7f);
        bloom.highQualityFiltering.Override(true);
        bloom.tint.Override(new Color(1f, 0.93f, 0.86f));

        var vin = p.Add<Vignette>(true);
        vin.color.Override(new Color(0.16f, 0.09f, 0.26f));
        vin.intensity.Override(0.2f);
        vin.smoothness.Override(0.65f);
        vin.rounded.Override(true);

        var dof = p.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(35f);
        dof.gaussianEnd.Override(120f);
        dof.gaussianMaxRadius.Override(0.8f);
        dof.highQualitySampling.Override(true);

        var ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.05f);
        ca.contrast.Override(-3f);
        ca.saturation.Override(4f);

        // Los dos de Quibli, por nombre: viven en otro ensamblado (Plugins) y así no dependemos de él.
        var detalle = AnadirPorNombre(p, "CompoundRendererFeature.PostProcess.StylizedDetail");
        Poner(detalle, "intensity", 0.15f);   // 0,7 era granulado: «demasiado, menos granulado»
        Poner(detalle, "blur", 1.6f);
        Poner(detalle, "edgePreserve", 1f);
        Poner(detalle, "rangeStart", 20f);
        Poner(detalle, "rangeEnd", 60f);

        var grading = AnadirPorNombre(p, "CompoundRendererFeature.PostProcess.ColorGrading");
        Poner(grading, "intensity", 0.3f);
        Poner(grading, "blueShadows", 0.35f);
        Poner(grading, "greenShadows", 0.08f);
        Poner(grading, "redHighlights", 0.25f);
        Poner(grading, "contrast", 0.05f);
        Poner(grading, "vibrance", 0.2f);
        Poner(grading, "saturation", 0.1f);

        foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
    }

    private static VolumeComponent AnadirPorNombre(VolumeProfile p, string tipo)
    {
        Type t = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(tipo, false)).FirstOrDefault(x => x != null);
        if (t == null)
        {
            Debug.LogWarning($"[Sueño] No encuentro el efecto de Quibli '{tipo}': ¿está Quibli importado? Sigo sin él.");
            return null;
        }
        return p.Add(t, true);
    }

    private static void Poner(VolumeComponent c, string campo, float valor)
    {
        if (c == null) return;
        var f = c.GetType().GetField(campo, BindingFlags.Public | BindingFlags.Instance);
        if (f?.GetValue(c) is VolumeParameter<float> par) par.Override(valor);
        else Debug.LogWarning($"[Sueño] '{c.GetType().Name}' no tiene el parámetro '{campo}'.");
    }
}
#endif
