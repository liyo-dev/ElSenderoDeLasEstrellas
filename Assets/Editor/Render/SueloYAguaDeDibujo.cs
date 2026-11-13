#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Pasa el terreno y el agua al aspecto de dibujo y los devuelve a como estaban.
/// Terreno: el material del terreno pasa a «El Sendero/Terreno de dibujo» (capas mezcladas como en
/// URP, luz de dos tonos, texturas acercadas a su color medio según AspectoDeDibujo.texturaDelSuelo).
/// Agua: cada material de agua pasa a «El Sendero/Agua de dibujo» con sus colores de antes como punto
/// de partida (orilla, medio, hondo y espuma). Solo cambia el shader y pone los campos nuevos; lo que
/// tenía se queda guardado en el material y el shader anterior se apunta en un registro, así que la
/// vuelta atrás es exacta. Ver INC-1137.
public static class SueloYAguaDeDibujo
{
    private const string ShaderDelTerreno = "El Sendero/Terreno de dibujo";
    private const string ShaderDelAgua = "El Sendero/Agua de dibujo";
    private const string RutaDelRegistro = "Assets/Settings/AspectoDeDibujo/SueloYAguaAntesDelDibujo.json";

    /// Materiales de terreno del juego.
    private static readonly string[] Terrenos =
    {
        "Assets/Scenes/Worlds/MainWorld_data/Recursos/Terreno.mat",
    };

    /// Materiales de agua: ruta y metros hasta el color hondo.
    private static readonly (string ruta, float hondura)[] Aguas =
    {
        ("Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaMar.mat", 6f),
        ("Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaRio.mat", 2.5f),
        ("Assets/Art/World/ithappy/Amusement_Park/Materials/Water.mat", 1.5f),
        ("Assets/Art/World/ithappy/Sweet_Land/Materials/Water.mat", 1.5f),
        ("Assets/_NPCs/Chuchelandia/Atrezo/Mat_Sirope.mat", 1f),
        ("Assets/Scenes/Test/CombatLabMaterials/Lab_Agua.mat", 2f),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_Ocean.mat", 6f),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_Lake.mat", 3f),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_River.mat", 2f),
        ("Assets/Art/World/Vestido/AguaLaguna.mat", 3f),
    };

    [Serializable]
    private sealed class Entrada { public string ruta; public string shader; }

    [Serializable]
    private sealed class Registro { public List<Entrada> materiales = new List<Entrada>(); }

    [MenuItem("El Sendero/Aspecto de dibujo/Terreno y agua: pasar al dibujo")]
    public static void Pasar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Sal de Play antes de pasar los materiales."); return; }
        var terreno = Shader.Find(ShaderDelTerreno);
        var agua = Shader.Find(ShaderDelAgua);
        if (terreno == null || agua == null)
        {
            Debug.LogError("[SueloYAguaDeDibujo] Faltan los shaders de terreno o de agua de dibujo; espera a que compilen.");
            return;
        }

        var registro = LeerElRegistro();
        int pasados = 0;

        foreach (var ruta in Terrenos)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader == terreno) continue;
            Apuntar(registro, ruta, mat.shader.name);
            mat.shader = terreno;
            EditorUtility.SetDirty(mat);
            pasados++;
        }

        foreach (var (ruta, hondura) in Aguas)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader == agua) continue;
            Color orillaDeAntes = Leer(mat, Color.cyan, "_ColorShallow", "_ShallowWaterColor", "_Color2", "_ColorSurface");
            Color hondoDeAntes = Leer(mat, new Color(0.1f, 0.35f, 0.55f), "_ColorDeep", "_DeepWaterColor", "_ColorDepth", "_Color1");
            Color espuma = Leer(mat, new Color(0.96f, 0.95f, 0.88f), "_ColorFoam", "_FoamColor");
            // Algunos packs dejan el color de orilla en negro (solo usan el hondo): se saca del hondo aclarado.
            if (orillaDeAntes.grayscale < 0.05f) orillaDeAntes = Color.Lerp(hondoDeAntes, new Color(0.6f, 0.9f, 0.9f), 0.5f);
            Apuntar(registro, ruta, mat.shader.name);
            mat.shader = agua;
            // Colores de dibujo: la orilla más clara, el hondo sin llegar a negro.
            mat.SetColor("_ColorOrilla", Color.Lerp(orillaDeAntes, Color.white, 0.3f));
            mat.SetColor("_ColorMedio", orillaDeAntes);
            mat.SetColor("_ColorHondo", Color.Lerp(hondoDeAntes, orillaDeAntes, 0.25f));
            mat.SetColor("_ColorEspuma", Color.Lerp(espuma, new Color(1f, 0.98f, 0.9f), 0.5f));
            mat.SetFloat("_Profundidad", hondura);
            mat.renderQueue = -1;
            EditorUtility.SetDirty(mat);
            pasados++;
        }

        GuardarElRegistro(registro);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SueloYAguaDeDibujo] Pasados al dibujo: {pasados} materiales de terreno y agua.");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Terreno y agua: volver a los de antes")]
    public static void Volver()
    {
        var registro = LeerElRegistro();
        int vueltos = 0;
        foreach (var e in registro.materiales)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(e.ruta);
            var shader = Shader.Find(e.shader);
            if (mat == null || shader == null) continue;
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            vueltos++;
        }
        GuardarElRegistro(new Registro());
        AssetDatabase.SaveAssets();
        Debug.Log($"[SueloYAguaDeDibujo] Devueltos a como estaban: {vueltos} materiales.");
    }

    private static Color Leer(Material mat, Color siNoHay, params string[] nombres)
    {
        foreach (var n in nombres)
            if (mat.HasProperty(n)) return mat.GetColor(n);
        return siNoHay;
    }

    private static void Apuntar(Registro registro, string ruta, string shader)
    {
        if (registro.materiales.Exists(e => e.ruta == ruta)) return;
        registro.materiales.Add(new Entrada { ruta = ruta, shader = shader });
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
