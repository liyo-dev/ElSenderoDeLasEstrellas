#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Tonos de piel de los NPCs. La piel de los personajes del pack sale de dos casillas de su textura
/// de colores (piel y nariz), la misma para todos; un NPC con otro tono lleva copias de sus materiales
/// con una copia de esa textura en su tono, y las piezas de piel de su cara (párpados, arrugas) en el
/// mismo tono. El reparto es la tabla de abajo; quien no está en ella lleva el tono de siempre.
/// Se puede repetir: cada copia recuerda de qué material sale, y quitar a un NPC de la tabla le
/// devuelve sus materiales.
public static class TonosDePiel
{
    const string TexturaDelPack = "Assets/Art/Characters/RPG Tiny Hero Duo/Texture/Albedo.png";
    const string Carpeta = "Assets/Art/Characters/NPCs/Piel";
    const string CarpetaDeMateriales = Carpeta + "/Materiales";

    /// La piel y la nariz de la textura del pack.
    static readonly Color32 PielDelPack = new Color32(246, 190, 150, 255);
    static readonly Color32 NarizDelPack = new Color32(230, 158, 132, 255);

    static readonly Dictionary<string, Color32> Tonos = new Dictionary<string, Color32>
    {
        { "clara", new Color32(250, 204, 172, 255) },
        { "tostada", new Color32(214, 148, 104, 255) },
        { "morena", new Color32(168, 108, 74, 255) },
    };

    /// Qué tono lleva cada NPC (Assets/_NPCs/…, como en CarasDeLosNPCs). El resto, el tono del pack.
    static readonly (string npc, string tono)[] Reparto =
    {
        ("_GrafoNarrativo/Selene", "clara"), ("_GrafoNarrativo/Boticaria", "clara"), ("Nora", "clara"),
        ("Pueblo/Aldona", "clara"), ("Sofia", "clara"), ("Leonardo", "clara"),
        ("NonInteractable/TownNpc#2", "clara"), ("NonInteractable/TownNpc#9", "clara"),

        ("WoodsGuard", "tostada"), ("NonInteractable/Guerrero#2", "tostada"), ("Manuel", "tostada"),
        ("Tabernera", "tostada"), ("Ladron2", "tostada"), ("Pueblo/MC01 (1) 1", "tostada"),
        ("NonInteractable/TownNpc#3", "tostada"), ("NonInteractable/VecinoDelMirador", "tostada"),
        ("AldeaDelMar/Anselmo", "tostada"), ("AldeaDelMar/Benito", "tostada"), ("AldeaDelMar/Fermin", "tostada"),
        ("AldeaDelMar/Remedios", "tostada"), ("AldeaDelMar/Rosario", "tostada"),

        ("Roberto", "morena"), ("Combat/Lety", "morena"), ("Patricia", "morena"), ("Sara", "morena"),
        ("Pueblo/MC01 (1) 2", "morena"), ("Pueblo/Generados/Renard", "morena"),
        ("NonInteractable/TownNpc#5", "morena"), ("NonInteractable/TownNpc#7", "morena"),
        ("NonInteractable/Guerrero#1", "morena"), ("AldeaDelMar/Engracia", "morena"),
    };

    [MenuItem("El Sendero/Personajes/Tonos de piel de los NPCs")]
    public static void Aplicar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Tonos de piel", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Aplicar(informe, CarasDeLosNPCs.RutasDeLosNPCs());
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Tonos de piel de los NPCs", informe.ToString());
    }

    /// Pone a cada NPC de <paramref name="rutas"/> el tono de la tabla (o le devuelve el del pack).
    public static void Aplicar(StringBuilder informe, IEnumerable<string> rutas)
    {
        var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturaDelPack);
        if (textura == null) { informe.AppendLine($"Tonos de piel: no encuentro {TexturaDelPack}."); return; }

        var tonoDe = Reparto.ToDictionary(r => Ruta(r.npc), r => r.tono);
        int conTono = 0, sinPiel = 0;
        foreach (string ruta in rutas.Distinct())
        {
            tonoDe.TryGetValue(ruta, out string tono);
            if (tono != null && !Tonos.ContainsKey(tono)) { informe.AppendLine($"{ruta}: no existe el tono «{tono}»."); continue; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) continue;

            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                bool cambiado = false, teniaPiel = false;
                foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
                {
                    var materiales = r.sharedMaterials;
                    bool tocado = false;
                    for (int i = 0; i < materiales.Length; i++)
                    {
                        var original = Original(materiales[i]);
                        if (original == null) continue;
                        var puesto = tono == null ? original : EnTono(original, textura, tono);
                        if (puesto != original || EsDePiel(original, textura)) teniaPiel = true;
                        if (puesto == materiales[i]) continue;
                        materiales[i] = puesto;
                        tocado = true;
                    }
                    if (!tocado) continue;
                    r.sharedMaterials = materiales;
                    cambiado = true;
                }
                if (cambiado) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                if (tono != null) { if (teniaPiel) conTono++; else { sinPiel++; informe.AppendLine($"{ruta}: no tiene piel del pack; sigue igual."); } }
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        informe.AppendLine($"Tonos de piel: {conTono} NPCs con tono propio" + (sinPiel > 0 ? $", {sinPiel} sin piel del pack." : "."));
    }

    static string Ruta(string npc) => npc.StartsWith("Assets/") ? npc : $"Assets/_NPCs/{npc}.prefab";

    static bool EsDePiel(Material mat, Texture2D textura) =>
        mat.mainTexture == textura || mat.name.EndsWith("_Piel") || mat.name.EndsWith("_Arruga");

    /// El material del que sale una copia en tono (él mismo si no es una copia).
    static Material Original(Material mat)
    {
        if (mat == null) return null;
        string ruta = AssetDatabase.GetAssetPath(mat);
        if (!ruta.StartsWith(CarpetaDeMateriales + "/")) return mat;
        var importador = AssetImporter.GetAtPath(ruta);
        string deDonde = importador != null ? AssetDatabase.GUIDToAssetPath(importador.userData) : null;
        var original = string.IsNullOrEmpty(deDonde) ? null : AssetDatabase.LoadAssetAtPath<Material>(deDonde);
        return original != null ? original : mat;
    }

    /// La copia de <paramref name="original"/> en un tono, o él mismo si no pinta piel.
    static Material EnTono(Material original, Texture2D textura, string tono)
    {
        Color32 piel = Tonos[tono];
        if (original.mainTexture == textura) return Copia(original, tono, TexturaEnTono(tono, piel));
        if (original.name.EndsWith("_Piel")) return Copia(original, tono, Lisa(original, tono, piel));
        if (original.name.EndsWith("_Arruga")) return Copia(original, tono, Lisa(original, tono, Arruga(piel)));
        return original;
    }

    static Color32 Nariz(Color32 piel) => new Color32((byte)(piel.r * 0.935f), (byte)(piel.g * 0.83f), (byte)(piel.b * 0.88f), 255);
    static Color32 Arruga(Color32 piel) => new Color32((byte)(piel.r * 0.86f), (byte)(piel.g * 0.78f), (byte)(piel.b * 0.8f), 255);

    static Texture2D Lisa(Material original, string tono, Color32 color) =>
        PersonajeHechoEnBlender.TexturaDeColor($"{CarpetaDeMateriales}/{original.name}__{tono}_Color.png", color);

    static Material Copia(Material original, string tono, Texture2D textura)
    {
        Directory.CreateDirectory(CarpetaDeMateriales);
        string ruta = $"{CarpetaDeMateriales}/{original.name}__{tono}.mat";
        string guidDelOriginal = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
        var copia = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (copia == null)
        {
            copia = new Material(original);
            AssetDatabase.CreateAsset(copia, ruta);
        }
        else
        {
            copia.shader = original.shader;
            copia.CopyPropertiesFromMaterial(original);
        }
        copia.mainTexture = textura;
        if (copia.HasProperty("_BaseMap")) copia.SetTexture("_BaseMap", textura);
        EditorUtility.SetDirty(copia);
        var importador = AssetImporter.GetAtPath(ruta);
        if (importador != null && importador.userData != guidDelOriginal)
        {
            importador.userData = guidDelOriginal;
            importador.SaveAndReimport();
            copia = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        }
        return copia;
    }

    /// La textura del pack con la piel y la nariz en un tono (se rehace si cambia la del pack o el tono).
    static Texture2D TexturaEnTono(string tono, Color32 piel)
    {
        Directory.CreateDirectory(Carpeta);
        string ruta = $"{Carpeta}/Albedo_{tono}.png";
        string firma = $"{File.GetLastWriteTimeUtc(TexturaDelPack).Ticks};{piel.r},{piel.g},{piel.b}";
        var importador = AssetImporter.GetAtPath(ruta);
        var hecha = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (hecha != null && importador != null && importador.userData == firma) return hecha;

        // La copia del asset trae los mismos ajustes de importación que la del pack.
        if (hecha == null) AssetDatabase.CopyAsset(TexturaDelPack, ruta);
        var lectura = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        lectura.LoadImage(File.ReadAllBytes(TexturaDelPack));
        var pixeles = lectura.GetPixels32();
        Color32 nariz = Nariz(piel);
        for (int i = 0; i < pixeles.Length; i++)
        {
            if (Igual(pixeles[i], PielDelPack)) pixeles[i] = piel;
            else if (Igual(pixeles[i], NarizDelPack)) pixeles[i] = nariz;
        }
        lectura.SetPixels32(pixeles);
        lectura.Apply();
        File.WriteAllBytes(ruta, lectura.EncodeToPNG());
        Object.DestroyImmediate(lectura);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        importador = AssetImporter.GetAtPath(ruta);
        importador.userData = firma;
        importador.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    static bool Igual(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
}
#endif
