using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Casas y piezas nuevas de las zonas hechas en Blender (Tools/Modelos/Zonas): les pone los materiales lisos con
/// el estilo del pack de las casas y deja un prefab de cada una en «Assets/Art/World/Zonas/Prefabs», que
/// es lo que colocan los montadores de escenario. Se puede repetir.
/// </summary>
public static class ModelosDeLasZonas
{
    public const string Carpeta = "Assets/Art/World/Zonas";

    private static readonly string[] Modelos =
    {
        "CasaDeLaAldea", "CasaDeLaAldea_Azul", "CasaDeLaAldea_Coral", "CasaDeLaAldea_Amarilla", "CasaDeLaAldea_Alta", "CasaDeLaAldea_Pequena",
        "PasarelaDeLaAldea", "Faro", "Lonja", "SecaderoDePescado",
        "CasaDeCiudad_A", "CasaDeCiudad_B", "CasaDeCiudad_C", "CasaDeCiudad_D", "CasaDeCiudad_E", "CasaDeCiudad_F", "TorreDelReloj",
        "ArbolConColumpio",
        "CasaDeLaAldeaEnTierra_Turquesa", "CasaDeLaAldeaEnTierra_Azul", "CasaDeLaAldeaEnTierra_Coral", "CasaDeLaAldeaEnTierra_Amarilla",
        "RellanoDeLaAldea", "MiradorDeLaAldea", "TiendaDeLaAldea_Pescado", "TiendaDeLaAldea_Fruta", "TiendaDeLaAldea_Aparejos", "PaseoDeTablas",
        "RosaDeLosVientos", "Banderines",
        "CasaDeCiudad_G", "CasaDeCiudad_H", "EdificioDeCiudad_Taberna", "EdificioDeCiudad_Posada", "EdificioDeCiudad_Palacio",
        "EdificioDeCiudad_Archivo", "EdificioDeCiudad_Guardia",
        "FuenteDeLaPlaza", "ArbolDePlaza", "PuestoDeMercado_Fruta", "PuestoDeMercado_Verdura", "PuestoDeMercado_Pan", "PuestoDeMercado_Pescado",
        "CasaDelPueblo_A", "CasaDelPueblo_B", "CasaDelPueblo_C", "CasaDelPueblo_D", "CasaDelPueblo_E", "CasaDelPueblo_F",
        "GraneroDelPueblo", "PozoDelPueblo", "ArcoDelPueblo", "ArcoDeLaAldea",
        "RepisaDeRoca_Grande", "RepisaDeRoca_Mediana", "RepisaDeRoca_Pequena", "EscalonesDePiedra", "PuenteColgante", "PuenteColgante_Largo",
        "MiradorDeCumbre", "TunelDeRoca", "TunelEscalonado", "BocaDeCueva", "SalaDeLaCueva", "CortinaDeHiedra", "GrietaSecreta", "EntradaDeTemplo",
    };

    private static readonly (string material, Color color)[] Colores =
    {
        ("Cal", Hex("f6f1e4")), ("Zocalo", Hex("2f79b0")), ("TejaTurquesa", Hex("1fa596")), ("TejaAzul", Hex("2b6f9e")),
        ("Carpinteria", Hex("1b8f84")), ("CarpinteriaAzul", Hex("285f9c")), ("Madera", Hex("a8713f")), ("MaderaOscura", Hex("7a5230")),
        ("Cuerda", Hex("d8c9a0")), ("Cristal", Hex("5b8fb8")), ("Boya", Hex("e0483e")), ("Flor", Hex("f06a8a")), ("Hojas", Hex("3fa85c")),
        ("Hierro", Hex("3a3340")), ("Luz", Hex("ffe58a")), ("PiedraA", Hex("eadcc0")), ("PiedraB", Hex("d9b196")), ("PiedraC", Hex("d8c58f")),
        ("PiedraLila", Hex("9a8fc0")), ("Pizarra", Hex("2f6fb5")), ("PizarraOscura", Hex("285f9c")), ("Fondo", Hex("3a2f3f")),
        ("Estandarte", Hex("2f6fb5")), ("Oro", Hex("f2c14e")),
        ("TejaSombra", Hex("178a7d")), ("PizarraSombra", Hex("2a64a6")),
        ("TejaCoral", Hex("e2674f")), ("TejaCoralSombra", Hex("c9553f")), ("TejaAmarilla", Hex("f0b83a")), ("TejaAmarillaSombra", Hex("d9a02a")),
        ("CarpinteriaCoral", Hex("c9553f")), ("ZocaloCoral", Hex("e2674f")), ("ZocaloTurquesa", Hex("1fa596")), ("ZocaloAmarillo", Hex("f0b83a")),
        ("Pescado", Hex("9fb6c4")), ("PescadoOscuro", Hex("6f8fa3")), ("Roca", Hex("8d8f9c")), ("RojoFaro", Hex("e0483e")), ("CopaClara", Hex("6fc46a")),
        ("PiedraD", Hex("e6cfc2")), ("PiedraE", Hex("cfd6e6")), ("PiedraGris", Hex("b9b4c8")),
        ("ToldoRojo", Hex("e0483e")), ("ToldoVerde", Hex("3fa85c")), ("ToldoAmarillo", Hex("f0b83a")), ("Agua", Hex("2f86c0")),
        ("Estuco", Hex("f3e3c0")), ("PiedraCampo", Hex("b8a88c")), ("Paja", Hex("e3b44a")), ("PajaSombra", Hex("c9982f")),
        ("ContraRoja", Hex("c9553f")), ("ContraVerde", Hex("3f8f5c")), ("RojoGranero", Hex("b8483a")), ("Hierba", Hex("5fb85f")),
        ("RocaOscura", Hex("4a4658")), ("SueloDeCueva", Hex("6b6470")), ("Hiedra", Hex("2f8f4e")), ("PiedraTemplo", Hex("d9d2c0")),
        ("Musgo", Hex("6f9f5a")), ("Bronce", Hex("b8763a")), ("Portal", Hex("7fe0d8")),
    };

    /// Fondo (m) de las casas y edificios de ciudad; null en lo demás.
    private static float? FondoDeFachada(string nombre) =>
        nombre.StartsWith("CasaDeCiudad_") || nombre == "EdificioDeCiudad_Guardia" ? 6f : nombre.StartsWith("EdificioDeCiudad_") ? 8f : null;

    private static Color Hex(string rrggbb) => ColorUtility.TryParseHtmlString("#" + rrggbb, out Color c) ? c : Color.magenta;

    [MenuItem("El Sendero/Escenario/Zonas: preparar las casas nuevas", priority = 60)]
    public static void Preparar()
    {
        var informe = new StringBuilder();
        Directory.CreateDirectory(Carpeta + "/Materiales");
        Directory.CreateDirectory(Carpeta + "/Prefabs");
        AssetDatabase.Refresh();
        foreach (string nombre in Modelos)
        {
            string fbx = $"{Carpeta}/{nombre}.fbx";
            if (!PiezaDeBlender.PrepararMateriales(fbx, Carpeta + "/Materiales", "Mat_Zonas_", Colores, informe)) continue;
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var copia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            // Se pisan y cierran el paso: colisión con su propia malla.
            // Banderines e hiedra se atraviesan.
            if (nombre != "Banderines" && nombre != "CortinaDeHiedra")
                foreach (MeshFilter malla in copia.GetComponentsInChildren<MeshFilter>())
                    if (malla.sharedMesh != null && malla.GetComponent<MeshCollider>() == null)
                        malla.gameObject.AddComponent<MeshCollider>().sharedMesh = malla.sharedMesh;
            // Las casas de ciudad llevan una marca de puerta en el centro de la fachada: ahí se cuelgan los rótulos.
            if (FondoDeFachada(nombre) is float fondo)
            {
                var puerta = new GameObject("Door01");
                puerta.transform.SetParent(copia.transform, false);
                puerta.transform.localPosition = new Vector3(0f, 0f, -fondo * 0.5f);
                puerta.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            PrefabUtility.SaveAsPrefabAsset(copia, $"{Carpeta}/Prefabs/{nombre}.prefab");
            Object.DestroyImmediate(copia);
            informe.AppendLine(nombre + ": listo.");
        }
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Casas de las zonas", informe.ToString());
    }
}
