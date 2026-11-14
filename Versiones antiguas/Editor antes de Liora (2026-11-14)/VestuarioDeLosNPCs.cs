#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Vestuario propio de los NPCs con peso en la historia (Eldran, Victoria, Erika, Oliver, el Rey, el
/// posadero y la boticaria, con su pañuelo verde salvia en la cabeza), hecho en Blender (Tools/Modelos/&lt;Personaje&gt;/). Usa lo mismo que los
/// protagonistas (<see cref="AspectoDeProtagonista.PonerPiezas"/>): la pieza de cuerpo nueva entra en
/// el modelo de cada uno junto a la que llevaba, se enciende y la de antes se queda apagada. La piel
/// de debajo (manos, brazos) usa el material de su cuerpo de siempre, así que conserva su tono de piel.
/// Se puede repetir. Para volver atrás, encender el cuerpo de antes en el prefab. Ver INC-1060.
/// </summary>
public static class VestuarioDeLosNPCs
{
    private const string Carpeta = "Assets/Art/Characters/NPCs/Vestuario";

    private static readonly AspectoDeProtagonista.Ficha[] Fichas =
    {
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "Eldran",
            Carpeta = Carpeta + "/Eldran",
            PrefabPropio = "Assets/_NPCs/Eldran.prefab",
            CuerpoDeAntes = "Body12",
            Modelos = new[] { "Assets/_NPCs/Eldran.prefab", "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab" },
            Propios = new[] { "Assets/_NPCs/Eldran.prefab", "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body26_AbrigoDeMago", Categoria = PartCategory.Body, Vecina = "Body12" },
            },
            Apagar = new string[] {  },
            Colores = new[] { ("Abrigo", new Color(0.25f, 0.33f, 0.19f)), ("AbrigoOscuro", new Color(0.15f, 0.21f, 0.12f)), ("Forro", new Color(0.55f, 0.47f, 0.3f)), ("Bufanda", new Color(0.62f, 0.62f, 0.64f)), ("BufandaOscura", new Color(0.45f, 0.45f, 0.48f)), ("Pantalon", new Color(0.22f, 0.18f, 0.15f)), ("Cuero", new Color(0.32f, 0.19f, 0.1f)), ("CueroOscuro", new Color(0.2f, 0.12f, 0.07f)), ("Laton", new Color(0.8f, 0.62f, 0.25f)), ("LibroRojo", new Color(0.55f, 0.13f, 0.12f)), ("LibroAzul", new Color(0.16f, 0.25f, 0.45f)), ("Papel", new Color(0.93f, 0.89f, 0.78f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "Victoria",
            Carpeta = Carpeta + "/Victoria",
            PrefabPropio = "Assets/_NPCs/Victoria.prefab",
            CuerpoDeAntes = "Body04",
            Modelos = new[] { "Assets/_NPCs/Victoria.prefab" },
            Propios = new[] { "Assets/_NPCs/Victoria.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body27_Costurera", Categoria = PartCategory.Body, Vecina = "Body04" },
            new AspectoDeProtagonista.Pieza { Nombre = "AC13_Mono", Categoria = PartCategory.Accessory, Vecina = "AC03_BlackGlass|AC01_Heart|Body04" },
            },
            Apagar = new string[] {  },
            Colores = new[] { ("Blusa", new Color(0.7f, 0.58f, 0.86f)), ("BlusaClara", new Color(0.86f, 0.78f, 0.95f)), ("Falda", new Color(0.46f, 0.34f, 0.62f)), ("FaldaOscura", new Color(0.33f, 0.24f, 0.46f)), ("Delantal", new Color(0.96f, 0.91f, 0.79f)), ("DelantalOscuro", new Color(0.82f, 0.74f, 0.58f)), ("Cinta", new Color(0.98f, 0.82f, 0.25f)), ("Acerico", new Color(0.8f, 0.18f, 0.2f)), ("Alfiler", new Color(0.92f, 0.92f, 0.95f)), ("Media", new Color(0.95f, 0.93f, 0.9f)), ("Zapato", new Color(0.36f, 0.2f, 0.12f)), ("Pelo", new Color(0.95f, 0.36f, 0.9f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "Erika",
            Carpeta = Carpeta + "/Erika",
            PrefabPropio = "Assets/_NPCs/Combat/Erika.prefab",
            CuerpoDeAntes = "Body08",
            Modelos = new[] { "Assets/_NPCs/Combat/Erika.prefab" },
            Propios = new[] { "Assets/_NPCs/Combat/Erika.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body28_GuerreraLigera", Categoria = PartCategory.Body, Vecina = "Body08" },
            },
            Apagar = new string[] {  },
            Colores = new[] { ("Camisa", new Color(0.3f, 0.52f, 0.3f)), ("CamisaOscura", new Color(0.18f, 0.34f, 0.2f)), ("Peto", new Color(0.45f, 0.28f, 0.15f)), ("Cuero", new Color(0.33f, 0.2f, 0.11f)), ("CueroOscuro", new Color(0.2f, 0.12f, 0.07f)), ("Metal", new Color(0.72f, 0.74f, 0.78f)), ("Capa", new Color(0.2f, 0.36f, 0.24f)), ("CapaForro", new Color(0.14f, 0.24f, 0.17f)), ("Panuelo", new Color(0.55f, 0.78f, 0.95f)), ("Pantalon", new Color(0.25f, 0.27f, 0.22f)), ("Laton", new Color(0.8f, 0.62f, 0.25f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "Oliver",
            Carpeta = Carpeta + "/Oliver",
            PrefabPropio = "Assets/_NPCs/Oliver.prefab",
            CuerpoDeAntes = "Body20",
            Modelos = new[] { "Assets/_NPCs/Oliver.prefab", "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab" },
            Propios = new[] { "Assets/_NPCs/Oliver.prefab", "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body29_Aprendiz", Categoria = PartCategory.Body, Vecina = "Body20" },
            new AspectoDeProtagonista.Pieza { Nombre = "Hat09_Aprendiz", Categoria = PartCategory.Hat, Vecina = "Hat08" },   // en lugar del de bruja (INC-1088)
            },
            Apagar = new string[] {  },
            Colores = new[] { ("Tunica", new Color(0.5f, 0.74f, 0.93f)), ("Gorro", new Color(0.2f, 0.38f, 0.64f)), ("TunicaOscura", new Color(0.3f, 0.52f, 0.75f)), ("Naranja", new Color(0.96f, 0.55f, 0.16f)), ("Cordon", new Color(0.93f, 0.87f, 0.7f)), ("Cuero", new Color(0.42f, 0.26f, 0.14f)), ("CueroOscuro", new Color(0.25f, 0.15f, 0.08f)), ("Papel", new Color(0.95f, 0.91f, 0.8f)), ("Pantalon", new Color(0.36f, 0.36f, 0.42f)), ("Laton", new Color(0.8f, 0.62f, 0.25f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "El Rey",
            Carpeta = Carpeta + "/Rey",
            PrefabPropio = "Assets/_NPCs/King.prefab",
            CuerpoDeAntes = "Body17",
            Modelos = new[] { "Assets/_NPCs/King.prefab" },
            Propios = new[] { "Assets/_NPCs/King.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body30_Rey", Categoria = PartCategory.Body, Vecina = "Body17" },
            },
            Apagar = new string[] { "Cloak01" },
            Colores = new[] { ("Tunica", new Color(0.5f, 0.09f, 0.12f)), ("TunicaOscura", new Color(0.32f, 0.05f, 0.08f)), ("Manto", new Color(0.42f, 0.06f, 0.1f)), ("Armino", new Color(0.96f, 0.93f, 0.86f)), ("Mota", new Color(0.2f, 0.15f, 0.13f)), ("Oro", new Color(0.95f, 0.74f, 0.22f)), ("Gema", new Color(0.15f, 0.35f, 0.8f)), ("Zapato", new Color(0.25f, 0.14f, 0.09f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "El posadero",
            Carpeta = Carpeta + "/Posadero",
            PrefabPropio = "Assets/_NPCs/AldeaDelMar/Posadero.prefab",
            CuerpoDeAntes = "Body05",
            Modelos = new[] { "Assets/_NPCs/AldeaDelMar/Posadero.prefab" },
            Propios = new[] { "Assets/_NPCs/AldeaDelMar/Posadero.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body31_Posadero", Categoria = PartCategory.Body, Vecina = "Body05" },
            },
            Apagar = new string[] {  },
            Colores = new[] { ("Camisa", new Color(0.96f, 0.95f, 0.92f)), ("CamisaSombra", new Color(0.82f, 0.84f, 0.86f)), ("Chaleco", new Color(0.13f, 0.5f, 0.52f)), ("ChalecoOscuro", new Color(0.08f, 0.34f, 0.37f)), ("Delantal", new Color(0.97f, 0.96f, 0.93f)), ("Pano", new Color(0.93f, 0.93f, 0.95f)), ("PanoRaya", new Color(0.2f, 0.55f, 0.6f)), ("Pantalon", new Color(0.36f, 0.27f, 0.2f)), ("Zapato", new Color(0.24f, 0.15f, 0.1f)), ("Laton", new Color(0.8f, 0.62f, 0.25f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "La boticaria",
            Carpeta = Carpeta + "/Boticaria",
            PrefabPropio = "Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab",
            CuerpoDeAntes = "Body10",
            Modelos = new[] { "Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab" },
            Propios = new[] { "Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body32_Herborista", Categoria = PartCategory.Body, Vecina = "Body10" },
            new AspectoDeProtagonista.Pieza { Nombre = "AC14_Panuelo", Categoria = PartCategory.Accessory, Vecina = "Hair09|Body10" },
            },
            Apagar = new string[] {  },
            Colores = new[] { ("Vestido", new Color(0.26f, 0.5f, 0.3f)), ("VestidoOscuro", new Color(0.16f, 0.34f, 0.2f)), ("Delantal", new Color(0.9f, 0.64f, 0.24f)), ("DelantalOscuro", new Color(0.7f, 0.45f, 0.14f)), ("Cuero", new Color(0.4f, 0.25f, 0.13f)), ("CueroOscuro", new Color(0.24f, 0.15f, 0.08f)), ("Hoja", new Color(0.36f, 0.68f, 0.3f)), ("Tallo", new Color(0.3f, 0.42f, 0.2f)), ("Frasco", new Color(0.55f, 0.85f, 0.9f)), ("Corcho", new Color(0.7f, 0.55f, 0.38f)), ("Panoleta", new Color(0.95f, 0.9f, 0.78f)), ("Panuelo", new Color(0.5f, 0.66f, 0.42f)), ("PanueloOscuro", new Color(0.36f, 0.5f, 0.3f)), ("Media", new Color(0.94f, 0.92f, 0.88f)), ("Zapato", new Color(0.3f, 0.18f, 0.1f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "El Archimago",
            Carpeta = Carpeta + "/Archimago",
            PrefabPropio = "Assets/Prefabs/_WILL_ORIGINAL.prefab",
            CuerpoDeAntes = "Body17",
            Modelos = new[] { "Assets/Prefabs/_WILL_ORIGINAL.prefab" },
            Propios = new[] { "Assets/Prefabs/_WILL_ORIGINAL.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body34_Archimago", Categoria = PartCategory.Body, Vecina = "Body17" },
            new AspectoDeProtagonista.Pieza { Nombre = "Hair20_Archimago", Categoria = PartCategory.Hair, Vecina = "Hair11" },
            },
            Apagar = new[] { "Cloak01" },   // su capa corta ya va en el cuerpo
            Colores = new[] { ("Crema", new Color(0.96f, 0.9f, 0.72f)), ("CremaCalida", new Color(0.88f, 0.74f, 0.5f)), ("Oro", new Color(0.88f, 0.67f, 0.2f)), ("Celeste", new Color(0.55f, 0.74f, 0.88f)), ("Pantalon", new Color(0.52f, 0.44f, 0.34f)), ("Cuero", new Color(0.4f, 0.26f, 0.15f)), ("CueroOscuro", new Color(0.27f, 0.17f, 0.1f)), ("PeloPlata", new Color(0.74f, 0.74f, 0.8f)), ("PeloPlataOscuro", new Color(0.55f, 0.55f, 0.62f)), ("AroOro", new Color(0.88f, 0.67f, 0.2f)) },
        },
        new AspectoDeProtagonista.Ficha
        {
            Nombre = "El Mago Oscuro",
            Carpeta = Carpeta + "/MagoOscuro",
            PrefabPropio = "Assets/Prefabs/_MAGO_OSCURO.prefab",
            CuerpoDeAntes = "Body18",
            Modelos = new[] { "Assets/Prefabs/_MAGO_OSCURO.prefab" },
            Propios = new[] { "Assets/Prefabs/_MAGO_OSCURO.prefab" },
            Piezas = new[]
            {
            new AspectoDeProtagonista.Pieza { Nombre = "Body33_MagoOscuro", Categoria = PartCategory.Body, Vecina = "Body18" },
            new AspectoDeProtagonista.Pieza { Nombre = "AC14_VeloDeSombra", Categoria = PartCategory.Accessory, Vecina = "AC05_Horn02|Body18" },
            new AspectoDeProtagonista.Pieza { Nombre = "Hair19_MagoOscuro", Categoria = PartCategory.Hair, Vecina = "Hair01|Body18", DeSerie = false },   // lo enciende MagoOscuroPorEstados en el revelado
            },
            Apagar = new[] { "HeadArmor04", "AC05_Horn02" },   // el casco y los cuernos del guardia del pack
            Colores = new[] { ("Tunica", new Color(0.075f, 0.055f, 0.13f)), ("Capa", new Color(0.05f, 0.038f, 0.095f)), ("Forro", new Color(0.3f, 0.12f, 0.52f)), ("Luz", new Color(0.74f, 0.45f, 1f)), ("Hueso", new Color(0.66f, 0.62f, 0.78f)), ("Vacio", new Color(0.012f, 0.008f, 0.025f)), ("Estrella", new Color(0.4f, 0.24f, 0.7f)), ("Cinto", new Color(0.12f, 0.09f, 0.19f)), ("PeloMago", new Color(0.04f, 0.035f, 0.06f)), ("PeloGris", new Color(0.62f, 0.6f, 0.7f)) },
            Brillan = new[] { ("Luz", 1.3f) },   // la franja del bajo y los puños
        },
    };

    [MenuItem("El Sendero/Personajes/Vestuario propio de los NPCs", priority = 60)]
    public static void Poner()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Vestuario de los NPCs", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        foreach (var f in Fichas)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{f.Carpeta}/{f.Piezas[0].Nombre}.fbx") == null)
            { informe.AppendLine($"{f.Nombre}: falta {f.Carpeta}/{f.Piezas[0].Nombre}.fbx; se deja como está."); continue; }
            if (AspectoDeProtagonista.PonerPiezas(f, informe)) informe.AppendLine($"{f.Nombre}: vestido.");
        }
        CoronarAlRey(informe);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Vestuario de los NPCs", informe.ToString());
    }

    /// El Rey llevaba capucha (Hat10) y no parecía un rey: corona del pack y el pelo largo que Raúl pidió
    /// («le pega uno larguito»), con piezas que su modelo ya tiene.
    private static void CoronarAlRey(StringBuilder informe)
    {
        const string ruta = "Assets/_NPCs/King.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) { informe.AppendLine("El Rey: no encuentro su prefab."); return; }
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            PiezaNuevaDelModelo.Encender(raiz, new[] { "AC06_Crown01", "Hair12" }, new[] { "Hat10", "Cloak01" });
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            informe.AppendLine("El Rey: corona y pelo largo en lugar de la capucha.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
#endif
