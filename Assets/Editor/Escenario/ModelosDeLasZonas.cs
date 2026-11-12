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
            // Se pisan y cierran el paso: colisión con su propia malla, en la capa del suelo para que el
            // jugador suba escalones y porches (INC-1093). Va en un hijo para no cambiar la capa de lo
            // que se dibuja. Banderines e hiedra se atraviesan.
            if (nombre != "Banderines" && nombre != "CortinaDeHiedra")
                ColisionDeSuelo(copia);
            if (nombre == "ArbolConColumpio") MontarElColumpio(copia, informe);
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
    private const string NombreDeLaColision = "Colisión (suelo)";

    /// Un hijo con la colisión de cada malla, en la capa Floor. Quita las colisiones que llevara la
    /// malla de antes (de una preparación anterior).
    private static void ColisionDeSuelo(GameObject copia)
    {
        int suelo = LayerMask.NameToLayer("Floor");
        foreach (MeshFilter malla in copia.GetComponentsInChildren<MeshFilter>())
        {
            if (malla.sharedMesh == null || malla.name == NombreDeLaColision) continue;
            var vieja = malla.GetComponent<MeshCollider>();
            if (vieja != null) Object.DestroyImmediate(vieja);
            if (malla.transform.Find(NombreDeLaColision) != null) continue;
            var hijo = new GameObject(NombreDeLaColision);
            hijo.transform.SetParent(malla.transform, false);
            if (suelo >= 0) hijo.layer = suelo;
            hijo.AddComponent<MeshCollider>().sharedMesh = malla.sharedMesh;
        }
    }

    // ── Columpio del árbol ───────────────────────────────────────────────────────────────────

    private const string CuerdasDelColumpio = "ColumpioDelArbol";
    /// Silla del pack que ya sirve para sentarse: de ella se copian el icono de interactuar y su juego de iconos.
    private const string SillaUsable = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Furniture/Chair/Chair12_a08.prefab";
    /// Dónde queda el origen de quien se sienta respecto al centro de la cara de arriba de la tabla (como en la
    /// silla de muestra, sentada media): un poco por debajo y hacia delante.
    private const float BajoLaTabla = 0.2f, DelanteDeLaTabla = 0.25f;
    private const float AmplitudDelColumpio = 22f;

    /// Cuelga las cuerdas y la tabla (ColumpioDelArbol.fbx, en las coordenadas del árbol) de un pivote que se
    /// balancea, y les pone el asiento usable: interactuar para sentarse, cancelar para bajarse.
    private static void MontarElColumpio(GameObject arbol, StringBuilder informe)
    {
        string fbx = $"{Carpeta}/{CuerdasDelColumpio}.fbx";
        if (!PiezaDeBlender.PrepararMateriales(fbx, Carpeta + "/Materiales", "Mat_Zonas_", Colores, informe)) return;
        var cuerdas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        bool hay = false;
        Bounds b = default;
        foreach (Renderer r in cuerdas.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hay) { Object.DestroyImmediate(cuerdas); informe.AppendLine("Columpio: las cuerdas no tienen malla."); return; }

        // El balanceo gira a lo largo de la rama: el lado largo de la tabla.
        Vector3 eje = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
        Vector3 frente = Vector3.Cross(eje, Vector3.up).normalized;
        float largo = b.size.y;
        var pivote = new GameObject("Columpio que se balancea").transform;
        pivote.SetParent(arbol.transform, false);
        pivote.localPosition = new Vector3(b.center.x, b.max.y, b.center.z);
        cuerdas.transform.SetParent(pivote, true);
        var balanceo = pivote.gameObject.AddComponent<BalanceoPendular>();
        balanceo.Configurar(Vector3.zero, eje, AmplitudDelColumpio, 2f * Mathf.PI * Mathf.Sqrt(largo / 9.81f));

        // La tabla mide 9 cm de grueso; su cara de arriba es el asiento.
        var tabla = new Vector3(b.center.x, b.min.y + 0.09f, b.center.z);
        var punto = new GameObject("Punto de sentarse").transform;
        punto.SetParent(pivote, false);
        punto.position = arbol.transform.TransformPoint(tabla - Vector3.up * BajoLaTabla + frente * DelanteDeLaTabla);
        punto.rotation = Quaternion.LookRotation(arbol.transform.TransformDirection(frente), Vector3.up);
        var alLevantarse = new GameObject("Punto al levantarse").transform;
        alLevantarse.SetParent(arbol.transform, false);
        alLevantarse.localPosition = new Vector3(tabla.x, 0f, tabla.z) + frente * 1.1f;
        alLevantarse.localRotation = Quaternion.LookRotation(frente, Vector3.up);

        var uso = new GameObject("Sentarse en el columpio");
        int capa = LayerMask.NameToLayer("Interactable");
        uso.layer = capa >= 0 ? capa : 8;
        uso.transform.SetParent(arbol.transform, false);
        uso.transform.localPosition = new Vector3(tabla.x, 0f, tabla.z);
        var zona = uso.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.center = new Vector3(0f, 0.8f, 0f);
        zona.size = new Vector3(1.4f, 1.6f, 1.4f);
        var asiento = uso.AddComponent<NPCWorldPoint>();
        asiento.activityType = NPCAmbientActivity.SitMedium;
        asiento.interactionPoint = punto;
        asiento.overrideFacing = true;
        asiento.puntoAlLevantarse = alLevantarse;
        asiento.requiredProximity = 2.2f;
        asiento.soloJugador = true;
        var interactuable = uso.AddComponent<Interactable>();
        CopiarIconoDeInteractuar(interactuable, uso.transform, informe);

        arbol.AddComponent<Columpio>().Configurar(asiento, balanceo);
        informe.AppendLine($"Columpio: cuerdas de {largo:0.00} m, se balancea {AmplitudDelColumpio:0}° sobre {(eje == Vector3.right ? "X" : "Z")}.");
    }

    /// Modo «usar punto» con el icono de interactuar y el juego de iconos de la silla de muestra.
    private static void CopiarIconoDeInteractuar(Interactable destino, Transform padre, StringBuilder informe)
    {
        var so = new SerializedObject(destino);
        so.FindProperty("mode").enumValueIndex = (int)InteractableMode.UseWorldPoint;
        var silla = AssetDatabase.LoadAssetAtPath<GameObject>(SillaUsable);
        Interactable muestra = silla != null ? silla.GetComponentInChildren<Interactable>(true) : null;
        if (muestra == null)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            informe.AppendLine("Columpio: no encuentro la silla de muestra; queda sin icono de interactuar.");
            return;
        }
        var soMuestra = new SerializedObject(muestra);
        so.FindProperty("iconSet").objectReferenceValue = soMuestra.FindProperty("iconSet").objectReferenceValue;
        var hintMuestra = soMuestra.FindProperty("hint").objectReferenceValue as GameObject;
        if (hintMuestra != null)
        {
            var hint = Object.Instantiate(hintMuestra, padre, false);
            hint.name = hintMuestra.name;
            hint.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            so.FindProperty("hint").objectReferenceValue = hint;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
