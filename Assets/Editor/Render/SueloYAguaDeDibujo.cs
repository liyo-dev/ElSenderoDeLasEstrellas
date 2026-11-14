#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

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

    /// Materiales de terreno del juego y el terreno que pintan (sus dos mapas de capas van al
    /// material para el borde de pincel entre capas; ver TerrenoDeDibujo.hlsl).
    private static readonly (string material, string datos)[] Terrenos =
    {
        ("Assets/Scenes/Worlds/MainWorld_data/Recursos/Terreno.mat", "Assets/Scenes/Worlds/MainWorld_data/Recursos/Terreno.asset"),
    };

    /// De qué es el agua: decide sus colores de dibujo. «Propia» conserva los que tenga (sirope…).
    private enum Tono { Mar, Dulce, Propia }

    /// Materiales de agua: ruta, metros hasta el color hondo y tono.
    private static readonly (string ruta, float hondura, Tono tono)[] Aguas =
    {
        ("Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaMar.mat", 6f, Tono.Mar),
        ("Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaRio.mat", 0.9f, Tono.Dulce),
        ("Assets/Art/World/ithappy/Amusement_Park/Materials/Water.mat", 1.5f, Tono.Dulce),
        ("Assets/Art/World/ithappy/Sweet_Land/Materials/Water.mat", 1.5f, Tono.Propia),
        ("Assets/_NPCs/Chuchelandia/Atrezo/Mat_Sirope.mat", 1f, Tono.Propia),
        ("Assets/Scenes/Test/CombatLabMaterials/Lab_Agua.mat", 2f, Tono.Dulce),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_Ocean.mat", 6f, Tono.Mar),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_Lake.mat", 3f, Tono.Dulce),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_River.mat", 2f, Tono.Dulce),
        ("Assets/Art/World/Vestido/AguaLaguna.mat", 3f, Tono.Dulce),
    };

    /// Agua del paquete que algunas escenas usan tal cual (el material que viene dentro del Shader
    /// Graph): sus superficies pasan a usar el material de agua de dibujo indicado. Ver INC-1188.
    private static readonly (string materialDelPaquete, string materialDeDibujo)[] AguasDelPaquete =
    {
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Shader/Water_Final.shadergraph", "Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaMar.mat"),
        ("Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Shader/Water_River.shadergraph", "Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaRio.mat"),
    };

    // Colores de dibujo (orilla, medio, hondo): planos y vivos, sin llegar al negro. Ver INC-1188.
    private static readonly Color[] ColoresDelMar = { new Color(0.56f, 0.90f, 0.84f), new Color(0.22f, 0.70f, 0.80f), new Color(0.13f, 0.46f, 0.76f) };
    private static readonly Color[] ColoresDelAguaDulce = { new Color(0.62f, 0.88f, 0.74f), new Color(0.30f, 0.68f, 0.68f), new Color(0.17f, 0.47f, 0.58f) };

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

        foreach (var (ruta, rutaDeDatos) in Terrenos)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) continue;
            if (mat.shader != terreno)
            {
                Apuntar(registro, ruta, mat.shader.name);
                mat.shader = terreno;
            }
            var datos = AssetDatabase.LoadAssetAtPath<TerrainData>(rutaDeDatos);
            var mapas = datos != null ? datos.alphamapTextures : null;
            if (mapas != null && mapas.Length >= 2)
            {
                mat.SetTexture("_ControlDeCapas0", mapas[0]);
                mat.SetTexture("_ControlDeCapas1", mapas[1]);
            }
            EditorUtility.SetDirty(mat);
            pasados++;
        }

        foreach (var (ruta, hondura, tono) in Aguas)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) continue;
            if (mat.shader != agua)
            {
                Color orillaDeAntes = Leer(mat, Color.cyan, "_ColorShallow", "_ShallowWaterColor", "_Color2", "_ColorSurface");
                Color hondoDeAntes = Leer(mat, new Color(0.1f, 0.35f, 0.55f), "_ColorDeep", "_DeepWaterColor", "_ColorDepth", "_Color1");
                Color espuma = Leer(mat, new Color(0.96f, 0.95f, 0.88f), "_ColorFoam", "_FoamColor");
                // Algunos packs dejan el color de orilla en negro (solo usan el hondo): se saca del hondo aclarado.
                if (orillaDeAntes.grayscale < 0.05f) orillaDeAntes = Color.Lerp(hondoDeAntes, new Color(0.6f, 0.9f, 0.9f), 0.5f);
                Apuntar(registro, ruta, mat.shader.name);
                mat.shader = agua;
                mat.SetColor("_ColorOrilla", Color.Lerp(orillaDeAntes, Color.white, 0.3f));
                mat.SetColor("_ColorMedio", orillaDeAntes);
                mat.SetColor("_ColorHondo", Color.Lerp(hondoDeAntes, orillaDeAntes, 0.25f));
                mat.SetColor("_ColorEspuma", Color.Lerp(espuma, new Color(1f, 0.98f, 0.9f), 0.5f));
                mat.renderQueue = -1;
            }
            Retocar(mat, hondura, tono);
            EditorUtility.SetDirty(mat);
            pasados++;
        }

        int superficies = CambiarAguaDelPaquete();
        GuardarElRegistro(registro);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SueloYAguaDeDibujo] Pasados al dibujo: {pasados} materiales de terreno y agua; {superficies} superficies de agua del paquete cambiadas (escenas abiertas, sin guardar).");
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

    /// En las escenas abiertas, las superficies que usan el agua del paquete pasan al agua de dibujo.
    /// Marca la escena como cambiada; guardarla queda a quien la tiene abierta.
    private static int CambiarAguaDelPaquete()
    {
        int cambiadas = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            bool tocada = false;
            foreach (var raiz in escena.GetRootGameObjects())
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool cambio = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    string ruta = AssetDatabase.GetAssetPath(mats[m]);
                    foreach (var (paquete, dibujo) in AguasDelPaquete)
                    {
                        if (ruta != paquete) continue;
                        var nuevo = AssetDatabase.LoadAssetAtPath<Material>(dibujo);
                        if (nuevo == null) continue;
                        mats[m] = nuevo;
                        cambio = true;
                    }
                }
                if (!cambio) continue;
                Undo.RecordObject(r, "Agua de dibujo");
                r.sharedMaterials = mats;
                tocada = true;
                cambiadas++;
            }
            if (tocada) EditorSceneManager.MarkSceneDirty(escena);
        }
        return cambiadas;
    }

    /// Valores de dibujo del agua (también en los materiales ya pasados): colores por tono, rizos de
    /// ola sueltos y lentos, oleaje suave sin lomos y espuma blanca con su línea de tinta. Ver INC-1188.
    private static void Retocar(Material mat, float hondura, Tono tono)
    {
        mat.SetFloat("_Profundidad", hondura);
        var colores = tono == Tono.Mar ? ColoresDelMar : tono == Tono.Dulce ? ColoresDelAguaDulce : null;
        if (colores != null)
        {
            mat.SetColor("_ColorOrilla", colores[0]);
            mat.SetColor("_ColorMedio", colores[1]);
            mat.SetColor("_ColorHondo", colores[2]);
            mat.SetColor("_ColorEspuma", new Color(1f, 0.99f, 0.94f));
        }
        mat.SetFloat("_CorteMedio", 0.2f);
        mat.SetFloat("_CorteHondo", 0.55f);
        mat.SetFloat("_AnchoDeEspuma", 0.3f);
        mat.SetFloat("_VelocidadDeEspuma", 0.1f);
        mat.SetFloat("_EscalaDeDestellos", tono == Tono.Mar ? 5f : 3f);
        mat.SetFloat("_GrosorDeDestellos", 0.045f);
        mat.SetFloat("_CantidadDeDestellos", tono == Tono.Mar ? 0.25f : 0.15f);
        mat.SetFloat("_VelocidadDeDestellos", 0.12f);
        mat.SetFloat("_MasDestellosAlSol", 0.4f);
        mat.SetFloat("_VelocidadDeOla", tono == Tono.Mar ? 0.3f : 0.15f);
        // Sin lomos: de lejos se estiraban en rayas como reflejos de agua de verdad.
        mat.SetColor("_ColorLomo", new Color(1f, 1f, 1f, 0f));
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
