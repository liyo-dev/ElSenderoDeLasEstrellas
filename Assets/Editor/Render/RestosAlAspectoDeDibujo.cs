#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// Lo que quedaba fuera del aspecto de dibujo tras el paso general (INC-1137):
/// - el fantasma intangible (transparente) y la cinta roja de Mara, al shader de dibujo;
/// - los últimos materiales con Quibli, para que su shader deje de entrar en la build: los de
///   escenario al dibujo y los de efectos a partículas sin luz;
/// - los cielos del menú principal y del prólogo, al cielo de dibujo del juego;
/// - la imagen de reposo de los retratos del HUD (la que se ve antes de que el retrato elija su
///   expresión), que aún era el busto pintado antiguo.
/// Lo que es luz (portales, estrella emisiva, runas, núcleos de los orbes) se queda sin luz a propósito:
/// la luz no lleva sombra. El shader anterior de cada material se apunta en un registro para volver atrás.
public static class RestosAlAspectoDeDibujo
{
    private const string Dibujo = "El Sendero/Dibujo";
    private const string ParticulasSinLuz = "Universal Render Pipeline/Particles/Unlit";
    private const string RutaDelRegistro = "Assets/Settings/AspectoDeDibujo/RestosAntesDelDibujo.json";
    private const string EscenaDelPrologo = "Assets/Scenes/Worlds/Prologo_Valle.unity";
    private const string EscenaDeArranque = "Assets/Scenes/Systems/Start.unity";
    private const string CarpetaDeRetratos = "Assets/Art/UI/Retratos/Grupo/";

    /// Materiales de escenario o personajes que pasan al dibujo.
    private static readonly string[] AlDibujo =
    {
        "Assets/Art/Materials/Sendero/Mat_CintaRoja_Mara.mat",
        "Assets/Plugins/Low Poly Modular Terrain Pack/Terrain_Assets/Materials/CPT_Terrain_01.mat",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Resources/Materials/comparacao.mat",
    };

    /// Efectos que aún usaban Quibli: pasan a partículas sin luz con su textura y su color.
    private static readonly string[] AParticulas =
    {
        "Assets/VFX/G-spot_Lab/Magic Energy Seamless Textures Free Pack/MagicEnergy_Purple_01/MagicEnergy_Purple_01.mat",
        "Assets/VFX/PLASMA Houidisoft technology/Plasma Shader/Materials/plsm.mat",
        "Assets/VFX/PLASMA Houidisoft technology/Plasma Shader/Materials/plsm 1.mat",
        "Assets/VFX/PLASMA Houidisoft technology/Plasma Shader/Materials/plsm 2.mat",
    };

    private const string FantasmaIntangible = "Assets/Prefabs/Enemy/Mundo/Materiales/Fantasma_Intangible.mat";

    [Serializable] private sealed class Entrada { public string ruta; public string shader; public int cola; }
    [Serializable] private sealed class Registro { public List<Entrada> materiales = new List<Entrada>(); public string cieloDelPrologo; }

    [MenuItem("El Sendero/Aspecto de dibujo/Restos: fantasma, Quibli, cielos del menú y del prólogo")]
    public static void Pasar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Sal de Play antes de pasar los materiales."); return; }
        var dibujo = Shader.Find(Dibujo);
        var particulas = Shader.Find(ParticulasSinLuz);
        if (dibujo == null || particulas == null) { Debug.LogError("[RestosAlAspectoDeDibujo] Faltan shaders; espera a que compile."); return; }

        var registro = LeerElRegistro();
        int n = 0;

        foreach (var ruta in AlDibujo)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader == dibujo) continue;
            Color tinte = PasoAlAspectoDeDibujo.ColorPlanoDe(mat); tinte.a = 1f;
            Texture textura = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            Apuntar(registro, ruta, mat);
            mat.shader = dibujo;
            mat.SetTexture("_BaseMap", textura);
            mat.SetColor("_Tinte", tinte);
            mat.SetFloat("_TextureImpact", 1f);
            mat.SetFloat("_ReceiveShadows", 1f);
            EditorUtility.SetDirty(mat); n++;
        }

        foreach (var ruta in AParticulas)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader == particulas) continue;
            Texture textura = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            Apuntar(registro, ruta, mat);
            mat.shader = particulas;
            mat.SetTexture("_BaseMap", textura);
            mat.SetColor("_BaseColor", color);
            Transparente(mat, aditivo: true);
            EditorUtility.SetDirty(mat); n++;
        }

        var fantasma = AssetDatabase.LoadAssetAtPath<Material>(FantasmaIntangible);
        if (fantasma != null && fantasma.shader != dibujo)
        {
            Apuntar(registro, FantasmaIntangible, fantasma);
            ComoFantasmaDeDibujo(fantasma, dibujo);
            n++;
        }

        int cielos = CielosDelMenu() + CieloDelPrologo(registro);
        int retratos = RetratosDeReposo();

        GuardarElRegistro(registro);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RestosAlAspectoDeDibujo] {n} materiales pasados, {cielos} cielos cambiados y {retratos} retratos del HUD con su imagen nueva.");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Restos: volver a los de antes")]
    public static void Volver()
    {
        var registro = LeerElRegistro();
        foreach (var e in registro.materiales)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(e.ruta);
            var shader = Shader.Find(e.shader);
            if (mat == null || shader == null) continue;
            mat.shader = shader;
            mat.renderQueue = e.cola;
            EditorUtility.SetDirty(mat);
        }
        if (!string.IsNullOrEmpty(registro.cieloDelPrologo))
            PonerCieloEnEscena(EscenaDelPrologo, AssetDatabase.LoadAssetAtPath<Material>(registro.cieloDelPrologo));
        GuardarElRegistro(new Registro());
        AssetDatabase.SaveAssets();
        Debug.Log("[RestosAlAspectoDeDibujo] Devuelto a como estaba (los cielos del menú se rehacen con su constructor).");
    }

    /// Material del fantasma cuando no se le puede tocar: su textura con el dibujo, translúcido y algo luminoso.
    internal static void ComoFantasmaDeDibujo(Material mat, Shader dibujo)
    {
        Texture textura = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
        mat.shader = dibujo;
        mat.SetTexture("_BaseMap", textura);
        mat.SetColor("_Tinte", new Color(0.75f, 0.9f, 1f, 0.35f));
        mat.SetFloat("_TextureImpact", 0.6f);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_ReceiveShadows", 0f);
        mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.15f, 0.25f, 0.35f));
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
    }

    private static void Transparente(Material mat, bool aditivo)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", aditivo ? 2f : 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)(aditivo ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static int CielosDelMenu()
    {
        int n = 0;
        if (PortadasDelMenuBuilder.CieloDeDia() != null) n++;
        if (PortadasDelMenuBuilder.CieloDeNoche() != null) n++;
        return n;
    }

    private static int CieloDelPrologo(Registro registro)
    {
        var dia = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Cielo/Cielo_Dia.mat");
        var cielo = ClimaDelCicloWiring.CieloDeEscena("Prologo", m => { if (dia != null) m.CopyPropertiesFromMaterial(dia); });
        if (cielo == null) return 0;
        var anterior = PonerCieloEnEscena(EscenaDelPrologo, cielo);
        if (anterior != null && anterior != cielo && string.IsNullOrEmpty(registro.cieloDelPrologo))
            registro.cieloDelPrologo = AssetDatabase.GetAssetPath(anterior);
        return 1;
    }

    /// Pone el cielo en una escena y la guarda. Devuelve el cielo anterior.
    private static Material PonerCieloEnEscena(string rutaDeEscena, Material cielo)
    {
        if (cielo == null) return null;
        Material anterior = null;
        EnEscena(rutaDeEscena, escena =>
        {
            anterior = RenderSettings.skybox;
            RenderSettings.skybox = cielo;
            DynamicGI.UpdateEnvironment();
        });
        return anterior;
    }

    /// Cada retrato del HUD (Retrato_Will, Retrato_Estela, Retrato_Liam) arranca con su cara normal nueva.
    private static int RetratosDeReposo()
    {
        int n = 0;
        EnEscena(EscenaDeArranque, escena =>
        {
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var retrato in raiz.GetComponentsInChildren<Sendero.UI.RetratoDelGrupo>(true))
                {
                    string quien = retrato.name.Replace("Retrato_", "").ToLowerInvariant();
                    var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaDeRetratos + quien + "_normal.png");
                    var so = new SerializedObject(retrato);
                    var imagen = so.FindProperty("imagen").objectReferenceValue as UnityEngine.UI.RawImage;
                    if (textura == null || imagen == null || imagen.texture == textura) continue;
                    Undo.RecordObject(imagen, "Retrato de reposo");
                    imagen.texture = textura;
                    EditorUtility.SetDirty(imagen);
                    n++;
                }
        });
        return n;
    }

    /// Hace un cambio en una escena y la guarda, dejando abiertas las mismas escenas que había.
    private static void EnEscena(string rutaDeEscena, Action<UnityEngine.SceneManagement.Scene> cambio)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var antes = EditorSceneManager.GetSceneManagerSetup();
        var escena = EditorSceneManager.OpenScene(rutaDeEscena, OpenSceneMode.Single);
        cambio(escena);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (antes != null && antes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(antes);
    }

    private static void Apuntar(Registro registro, string ruta, Material mat)
    {
        if (registro.materiales.Exists(e => e.ruta == ruta)) return;
        registro.materiales.Add(new Entrada { ruta = ruta, shader = mat.shader.name, cola = mat.rawRenderQueue });
    }

    private static Registro LeerElRegistro()
    {
        if (!File.Exists(RutaDelRegistro)) return new Registro();
        return JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro)) ?? new Registro();
    }

    private static void GuardarElRegistro(Registro registro)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelRegistro));
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(registro, true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
    }
}
#endif
