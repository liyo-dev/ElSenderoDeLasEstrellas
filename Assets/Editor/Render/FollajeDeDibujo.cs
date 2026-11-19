#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Pone el follaje de dibujo (palabra _FOLLAJE del shader «El Sendero/Dibujo») en las copas y matas: color
/// medio de cada zona en vez del dibujo fino del pack, golpes de hoja de dos tonos y un matiz propio por
/// árbol. Trabaja sobre todo material del dibujo que pinte con uno de los atlas de follaje de la tabla, sobre
/// las copas de los modelos propios y, lanzada a mano, sobre los materiales seleccionados en el Project (para
/// pasar el follaje de un pack nuevo). Deja la textura de golpes en AspectoDeDibujo. Lo que tenía cada
/// material se apunta en un registro y «quitarlo» lo devuelve. Texturas: Tools/Texturas/follaje_de_dibujo.py.
public static class FollajeDeDibujo
{
    private const string Dibujo = "El Sendero/Dibujo";
    private const string Carpeta = "Assets/Art/World/Follaje/";
    private const string Golpes = Carpeta + "GolpesDeHoja.png";
    private const string RutaDelAspecto = "Assets/Resources/" + AspectoDeDibujo.RutaEnResources + ".asset";
    private const string RutaDelRegistro = "Assets/Settings/AspectoDeDibujo/FollajePuesto.json";
    private const string RutaDelInforme = "Guiones/_ordenes/follaje.md";

    /// Texeles por lado del mip del que sale el color de cada zona: menos = más plano (y más mezcla en los
    /// bordes de las islas del atlas).
    private const int TexelesDelColorDeZona = 64;

    private const string MascaraAtlasDeArboles = Carpeta + "MascaraDeFollaje_AtlasDeArboles.png";

    /// Atlas de follaje compartidos y su máscara: todo material del dibujo que pinte con uno de ellos lleva el
    /// follaje (así entran solas las copias que hacen otras herramientas, como los Follaje_N del vestido).
    private static readonly (string textura, string mascara)[] Atlas =
    {
        // Atlas de árboles, setas y hierba del Fantasy Kingdom Pack y sus copias recoloreadas.
        ("Assets/Art/World/Fantasy_Kingdom_Pack/Textures/Tree_D.tga", MascaraAtlasDeArboles),
        ("Assets/Art/World/Vestido/CopaRoja_D.png", MascaraAtlasDeArboles),
        ("Assets/Art/World/Vestido/Tree_Cerezo_D.png", MascaraAtlasDeArboles),
        ("Assets/Art/World/Pueblo/ArbolEnFlor_D.png", MascaraAtlasDeArboles),
    };

    /// Copas de los modelos propios (color liso): todo el material es follaje.
    private static readonly string[] CopasPropias =
    {
        "Assets/Art/World/Pueblo/Materiales/Mat_Pueblo_Hojas.mat",
        "Assets/Art/World/Zonas/Materiales/Mat_Zonas_Hojas.mat",
        "Assets/Art/World/Zonas/Materiales/Mat_Zonas_CopaClara.mat",
    };

    private static readonly string[] CarpetasQueNoSeTocan = { "Assets/Plugins/", "Assets/VFX/", "Assets/_VFX/", "Assets/Art/VFX/" };

    [Serializable] private sealed class Entrada { public string ruta; public bool pincel; public bool follaje; public float mip; public string mascara; }
    [Serializable] private sealed class Registro { public List<Entrada> materiales = new List<Entrada>(); }

    [MenuItem("El Sendero/Aspecto de dibujo/Follaje de dibujo en copas y matas (tabla y selección)")]
    public static void Poner()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Follaje] Sal de Play antes."); return; }
        var shader = Shader.Find(Dibujo);
        if (shader == null) { OrdenesDelEditor.Avisar("Follaje", $"No encuentro el shader «{Dibujo}»."); return; }

        var sb = new StringBuilder("# Follaje de dibujo\n\n");
        var golpes = PrepararTextura(Golpes, true);
        PrepararTextura(MascaraAtlasDeArboles, false);
        sb.Append(golpes != null ? "- Golpes de hoja en AspectoDeDibujo\n" : $"- Falta {Golpes}: el follaje sale sin golpes\n");
        var aspecto = AssetDatabase.LoadAssetAtPath<AspectoDeDibujo>(RutaDelAspecto);
        if (aspecto != null && golpes != null)
        {
            var so = new SerializedObject(aspecto);
            so.FindProperty("golpesDeHoja").objectReferenceValue = golpes;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(aspecto);
            aspecto.Aplicar();
        }

        var pedidos = CopasPropias.Select(r => (material: r, mascara: (string)null)).ToList();
        var mascaraDeAtlas = Atlas.ToDictionary(a => a.textura, a => a.mascara);
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!ruta.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            if (CarpetasQueNoSeTocan.Any(c => ruta.StartsWith(c, StringComparison.OrdinalIgnoreCase))) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null || mat.shader != shader) continue;
            string textura = AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap"));
            if (textura != null && mascaraDeAtlas.TryGetValue(textura, out string m)) pedidos.Add((ruta, m));
        }
        // Desde el buzón, solo la tabla: lo seleccionado puede ser de otro trabajo.
        var seleccion = OrdenesDelEditor.EnOrden ? Array.Empty<Material>() : Selection.GetFiltered<Material>(SelectionMode.Assets);
        foreach (var m in seleccion)
        {
            string ruta = AssetDatabase.GetAssetPath(m);
            if (pedidos.All(p => p.material != ruta)) pedidos.Add((ruta, null));
        }

        var registro = Leer();
        int puestos = 0;
        foreach (var (ruta, rutaMascara) in pedidos)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) { sb.Append($"- {ruta}: no existe\n"); continue; }
            if (mat.shader != shader) { sb.Append($"- {mat.name}: no es del dibujo ({mat.shader.name}), sin tocar\n"); continue; }
            if (registro.materiales.All(e => e.ruta != ruta))
                registro.materiales.Add(new Entrada
                {
                    ruta = ruta,
                    pincel = mat.IsKeywordEnabled("_PINCEL"),
                    follaje = mat.IsKeywordEnabled("_FOLLAJE"),
                    mip = mat.GetFloat("_FollajeMip"),
                    mascara = AssetDatabase.GetAssetPath(mat.GetTexture("_MascaraDeFollaje")),
                });

            var mascara = rutaMascara != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(rutaMascara) : null;
            float mip = MipDelColorDeZona(mat.GetTexture("_BaseMap"));
            mat.EnableKeyword("_FOLLAJE"); mat.SetFloat("_Follaje", 1f);
            mat.SetFloat("_FollajeMip", mip);
            mat.SetTexture("_MascaraDeFollaje", mascara);
            // Los golpes de hoja ya hacen de pincel: con los dos a la vez la copa se ensucia.
            mat.DisableKeyword("_PINCEL"); mat.SetFloat("_Pincel", 0f);
            EditorUtility.SetDirty(mat);
            puestos++;
            sb.Append($"- {mat.name}: follaje (mip {mip:0}, máscara {(mascara != null ? mascara.name : "toda")})\n");
        }
        Guardar(registro);
        AssetDatabase.SaveAssets();
        File.WriteAllText(RutaDelInforme, sb.ToString());
        OrdenesDelEditor.Avisar("Follaje", $"Follaje de dibujo puesto en {puestos} materiales. Detalle en {RutaDelInforme}.");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Follaje de dibujo: quitarlo")]
    public static void Quitar()
    {
        var registro = Leer();
        foreach (var e in registro.materiales)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(e.ruta);
            if (mat == null) continue;
            if (e.follaje) mat.EnableKeyword("_FOLLAJE"); else mat.DisableKeyword("_FOLLAJE");
            mat.SetFloat("_Follaje", e.follaje ? 1f : 0f);
            if (e.pincel) mat.EnableKeyword("_PINCEL"); else mat.DisableKeyword("_PINCEL");
            mat.SetFloat("_Pincel", e.pincel ? 1f : 0f);
            mat.SetFloat("_FollajeMip", e.mip);
            mat.SetTexture("_MascaraDeFollaje", string.IsNullOrEmpty(e.mascara) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(e.mascara));
            EditorUtility.SetDirty(mat);
        }
        Guardar(new Registro());
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Follaje", $"Follaje de dibujo quitado de {registro.materiales.Count} materiales.");
    }

    /// Nivel de mip con unos <see cref="TexelesDelColorDeZona"/> texeles por lado (0 en texturas pequeñas o sin mips).
    private static float MipDelColorDeZona(Texture tex)
    {
        if (tex == null || tex.mipmapCount <= 1) return 0f;
        float lado = Mathf.Max(tex.width, tex.height);
        return Mathf.Clamp(Mathf.Round(Mathf.Log(lado / TexelesDelColorDeZona, 2f)), 0f, tex.mipmapCount - 1);
    }

    /// Ajusta la importación: un solo canal (rojo) en lineal y sin comprimir (el borde de los golpes cae en
    /// 0,5 y la compresión lo dentaría); los golpes se repiten y llevan mips (de lejos se funden y
    /// desaparecen), la máscara no se repite y basta con 512.
    private static Texture2D PrepararTextura(string ruta, bool repetir)
    {
        if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp)) return null;
        var ajustes = new TextureImporterSettings();
        imp.ReadTextureSettings(ajustes);
        var envoltura = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        const int tope = 512;
        bool cambia = ajustes.textureType != TextureImporterType.SingleChannel
                      || ajustes.singleChannelComponent != TextureImporterSingleChannelComponent.Red
                      || imp.wrapMode != envoltura || imp.mipmapEnabled != repetir || imp.maxTextureSize != tope
                      || imp.textureCompression != TextureImporterCompression.Uncompressed;
        if (cambia)
        {
            ajustes.textureType = TextureImporterType.SingleChannel;
            ajustes.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            imp.SetTextureSettings(ajustes);
            imp.sRGBTexture = false;
            imp.wrapMode = envoltura;
            imp.mipmapEnabled = repetir;
            imp.filterMode = repetir ? FilterMode.Trilinear : FilterMode.Bilinear;
            imp.anisoLevel = repetir ? 4 : 1;
            imp.maxTextureSize = tope;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    private static Registro Leer() =>
        File.Exists(RutaDelRegistro) ? JsonUtility.FromJson<Registro>(File.ReadAllText(RutaDelRegistro)) ?? new Registro() : new Registro();

    private static void Guardar(Registro r)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelRegistro));
        File.WriteAllText(RutaDelRegistro, JsonUtility.ToJson(r, true));
        AssetDatabase.ImportAsset(RutaDelRegistro);
    }
}
#endif
