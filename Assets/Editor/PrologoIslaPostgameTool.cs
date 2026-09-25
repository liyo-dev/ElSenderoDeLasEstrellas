#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Reconstruye la isla visitable del prólogo. Solo trabaja sobre Prologo_Isla_Postgame
/// y guarda la geometría y los materiales en una carpeta exclusiva para preparar su integración en MainWorld.
/// </summary>
public static class PrologoIslaPostgameTool
{
    private const string EscenaObjetivo = "Prologo_Isla_Postgame";
    private const string RutaEscenaObjetivo = "Assets/Scenes/Worlds/Prologo_Isla_Postgame.unity";
    private const string Carpeta = "Assets/Art/World/Prologo_Isla_Postgame";
    private const string RutaMalla = Carpeta + "/Meshes/Isla_Postgame_Quibli.asset";
    private const string RutaMar = Carpeta + "/Meshes/Mar_Prevision_Quibli.asset";
    private const string RutaRio = Carpeta + "/Meshes/Rio_Isla_Postgame_Quibli.asset";
    private const string NombreModulo = "ISLA_POSTGAME_MODULO";
    private const string NombreTerreno = "Terreno_Costa_Quibli";
    private const string NombreVegetacionNueva = "Bosquetes_Isla_Postgame";
    private const string NombrePraderaNueva = "Pradera_Flores_Cesped_Isla_Postgame";
    private const float RadioX = 166f;
    private const float RadioZ = 128f;
    private const float Paso = .75f;
    private const float AnchoSenda = 2.8f;
    private const float AnchoRio = 2.2f;
    private static Vector2 PuntoCruceRio = new(23f, .39f);
    private static float LimiteRioEnPuente = 1.6f;
    private static readonly List<Rect> ZonasCasas = new();

    private static readonly Vector2[] RutaPrincipal =
    {
        new(68f, -116f), new(51f, -99f), new(36f, -79f), new(24f, -56f),
        new(12f, -35f), new(5f, -14f), new(8f, 9f), new(18f, 29f),
        new(24f, 50f), new(18f, 72f), new(5f, 96f)
    };

    private static readonly Vector2[] RutaOeste =
    {
        new(7f, -12f), new(-16f, -6f), new(-39f, 1f), new(-63f, 13f),
        new(-77f, 24f), new(-69f, 24f)
    };

    private static readonly Vector2[] RutaEste =
    {
        new(13f, 20f), new(37f, 17f), new(60f, 22f), new(82f, 38f)
    };

    private static readonly Vector2[] RutaPuente =
    {
        new(-49f, .4f), new(-28f, .4f), new(-8f, .4f), new(8f, .4f),
        new(23f, .39f), new(40f, 7f), new(55f, 23f), new(72f, 42f), new(93f, 54f)
    };

    private static readonly Vector2[][] Rutas = { RutaPrincipal, RutaOeste, RutaEste, RutaPuente };

    private static readonly Vector2[] CentrosBosque =
    {
        new(-100f, -40f), new(-104f, 28f), new(-61f, 78f),
        new(38f, 83f), new(105f, 46f), new(105f, -27f), new(43f, -82f)
    };

    private static readonly string[] PrefabsArboles =
    {
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Tree02_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Tree03_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Tree06_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Tree02_b01.prefab"
    };

    private static readonly string[] PrefabsPradera =
    {
        "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Short.prefab",
        "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Long.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Flower01_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Flower04_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/Flower05_a01.prefab"
    };

    [MenuItem("El Sendero/Escenario/Postgame: reconstruir isla Quibli (solo Prologo_Isla_Postgame)", priority = 18)]
    public static void Reconstruir()
    {
        Scene escena = SceneManager.GetActiveScene();
        if (!EsEscenaObjetivo(escena))
        {
            Debug.LogError("[IslaPostgame] Abre y activa Prologo_Isla_Postgame. Esta herramienta no actúa sobre ninguna otra escena.");
            return;
        }

        GameObject suelo = Buscar(escena, "Suelo_Valle");
        GameObject puente = Buscar(escena, "Puente");
        Renderer rendererSuelo = suelo != null ? suelo.GetComponent<Renderer>() : null;
        Material plantilla = rendererSuelo != null ? rendererSuelo.sharedMaterial : null;
        if (plantilla == null || plantilla.shader == null || !plantilla.shader.name.Contains("Quibli"))
        {
            Debug.LogError("[IslaPostgame] Suelo_Valle no tiene una plantilla Quibli. No se ha cambiado la escena.");
            return;
        }

        if (puente == null)
        {
            Debug.LogError("[IslaPostgame] No encuentro el puente que debe cruzar el río. No se ha cambiado la escena.");
            return;
        }

        GameObject vegetacion = Buscar(escena, "06_Vegetacion");
        if (vegetacion == null)
        {
            Debug.LogError("[IslaPostgame] No encuentro 06_Vegetacion. No se ha cambiado la escena.");
            return;
        }

        var prefabs = new GameObject[PrefabsArboles.Length];
        for (int i = 0; i < PrefabsArboles.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsArboles[i]);
            if (prefabs[i] == null)
            {
                Debug.LogError($"[IslaPostgame] Falta el prefab requerido: {PrefabsArboles[i]}. No se ha cambiado la escena.");
                return;
            }
        }

        var prefabsPradera = new GameObject[PrefabsPradera.Length];
        for (int i = 0; i < PrefabsPradera.Length; i++)
        {
            prefabsPradera[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPradera[i]);
            if (prefabsPradera[i] == null)
            {
                Debug.LogError($"[IslaPostgame] Falta el prefab de flores o césped: {PrefabsPradera[i]}. No se ha cambiado la escena.");
                return;
            }
        }

        CrearCarpetas();
        Vector3 centroMundo = suelo.transform.position;
        Vector3 puenteLocal = puente.transform.position - centroMundo;
        PuntoCruceRio = new Vector2(puenteLocal.x, puenteLocal.z);
        MedirAnchuraPuente(puente);
        RecogerZonasCasas(escena, centroMundo);
        RutaPuente[4] = PuntoCruceRio;
        RutaPuente[3] = PuntoCruceRio + Vector2.left * 15f;
        RutaPuente[5] = PuntoCruceRio + new Vector2(17f, 6f);
        var materiales = CrearMateriales(plantilla);
        Material maderaColumpio = CrearMaterial("Madera_Columpio", plantilla, new Color(.30f, .18f, .09f));
        Mesh malla = CrearOModificarMalla(RutaMalla, ConstruirMalla());
        Mesh mar = CrearOModificarMalla(RutaMar, ConstruirMar());
        Mesh rio = CrearOModificarMalla(RutaRio, ConstruirRio());
        var materialesTerreno = new Material[7];
        Array.Copy(materiales, materialesTerreno, materialesTerreno.Length);

        GameObject modulo = Buscar(escena, NombreModulo);
        if (modulo == null)
        {
            modulo = new GameObject(NombreModulo);
            Undo.RegisterCreatedObjectUndo(modulo, "Crear módulo de isla postgame");
            SceneManager.MoveGameObjectToScene(modulo, escena);
        }

        Undo.RecordObject(modulo.transform, "Centrar módulo de isla postgame");
        modulo.transform.SetParent(null, true);
        modulo.transform.SetPositionAndRotation(centroMundo, Quaternion.identity);
        modulo.transform.localScale = Vector3.one;

        GameObject terreno = Buscar(escena, NombreTerreno) ?? Buscar(escena, "TERRENO_Cuenca_Quibli");
        if (terreno == null)
        {
            terreno = new GameObject(NombreTerreno);
            Undo.RegisterCreatedObjectUndo(terreno, "Crear terreno de la isla postgame");
            SceneManager.MoveGameObjectToScene(terreno, escena);
        }
        terreno.name = NombreTerreno;
        Undo.RecordObject(terreno.transform, "Preparar pivote del terreno de la isla");
        terreno.transform.SetParent(modulo.transform, false);
        terreno.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        terreno.transform.localScale = Vector3.one;

        var filtro = terreno.GetComponent<MeshFilter>();
        if (filtro == null) filtro = Undo.AddComponent<MeshFilter>(terreno);
        filtro.sharedMesh = malla;
        var renderTerreno = terreno.GetComponent<MeshRenderer>();
        if (renderTerreno == null) renderTerreno = Undo.AddComponent<MeshRenderer>(terreno);
        renderTerreno.sharedMaterials = materialesTerreno;
        var collider = terreno.GetComponent<MeshCollider>();
        if (collider == null) collider = Undo.AddComponent<MeshCollider>(terreno);
        collider.sharedMesh = null;
        collider.sharedMesh = malla;
        int capaSuelo = LayerMask.NameToLayer("Floor");
        if (capaSuelo >= 0) terreno.layer = capaSuelo;
        var obstaculo = terreno.GetComponent<NavMeshObstacle>();
        if (obstaculo != null) Undo.DestroyObjectImmediate(obstaculo);

        DesactivarComponente(suelo.GetComponent<Renderer>());
        DesactivarComponente(suelo.GetComponent<Collider>());
        GameObject aguaAntigua = Buscar(escena, "Rio_Agua");
        if (aguaAntigua != null)
        {
            DesactivarComponente(aguaAntigua.GetComponent<Renderer>());
            DesactivarComponente(aguaAntigua.GetComponent<Collider>());
        }

        PrepararDecoradoParaElModulo(escena, modulo.transform);
        AjustarVegetacionExistente(vegetacion.transform, centroMundo);
        CrearBosquetes(vegetacion.transform, centroMundo, prefabs);
        CrearPraderaDecorativa(vegetacion.transform, centroMundo, prefabsPradera);
        CrearRinconColumpio(vegetacion.transform, centroMundo, prefabs[0], maderaColumpio);
        ArmonizarMontana(escena, materiales[3]);
        PrepararMarDeVista(escena, centroMundo, mar, materiales[7]);
        PrepararRio(escena, modulo.transform, rio, materiales[5]);
        DesactivarObjeto(escena, "PROP_Nube_Este");
        DesactivarObjeto(escena, "PROP_Nube_Oeste");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        Selection.activeGameObject = modulo;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[IslaPostgame] Isla lista para revisar: praderas suaves, colina con árbol y columpio, río bajo el puente, playa y sendas Quibli.");
    }

    [MenuItem("El Sendero/Escenario/Postgame: reconstruir isla Quibli (solo Prologo_Isla_Postgame)", true)]
    private static bool ValidarMenu() => EsEscenaObjetivo(SceneManager.GetActiveScene());

    [MenuItem("El Sendero/Escenario/Postgame: aplicar paleta a isla colocada en MainWorld", priority = 19)]
    private static void AplicarPaletaIslaEnMainWorld()
    {
        Scene escena = BuscarEscenaCargada("MainWorld", "Assets/Scenes/Worlds/MainWorld.unity");
        AplicarPaletaAIsla(escena, "MainWorld", true);
    }

    [MenuItem("El Sendero/Escenario/Postgame: previsualizar paleta en Prologo_Isla_Postgame", priority = 20)]
    private static void PrevisualizarPaletaEnPrologoIsla()
    {
        Scene escena = BuscarEscenaCargada(EscenaObjetivo, RutaEscenaObjetivo);
        if (!PrepararPaletaEnPrologo(escena)) return;
        AplicarPaletaAIsla(escena, EscenaObjetivo, true);
    }

    private static bool PrepararPaletaEnPrologo(Scene escena)
    {
        if (!escena.IsValid())
        {
            Debug.LogError("[IslaPostgame] Carga Prologo_Isla_Postgame para preparar la vista previa.");
            return false;
        }

        GameObject suelo = Buscar(escena, "Suelo_Valle");
        Material plantilla = suelo != null ? suelo.GetComponent<Renderer>()?.sharedMaterial : null;
        if (plantilla == null || plantilla.shader == null || !plantilla.shader.name.Contains("Quibli"))
        {
            Debug.LogError("[IslaPostgame] Suelo_Valle no tiene un material Quibli para preparar la paleta.");
            return false;
        }

        CrearCarpetas();
        CrearMateriales(plantilla, true);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static Scene BuscarEscenaCargada(string nombre, string ruta)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidata = SceneManager.GetSceneAt(i);
            if (candidata.IsValid() && candidata.isLoaded && candidata.name == nombre &&
                string.Equals(candidata.path, ruta, StringComparison.OrdinalIgnoreCase)) return candidata;
        }
        return default;
    }

    private static void AplicarPaletaAIsla(Scene escena, string nombreEscena, bool vistaPrevia = false)
    {
        if (!escena.IsValid())
        {
            Debug.LogError($"[IslaPostgame] Carga {nombreEscena} para aplicar la paleta. No se ha cambiado ningún objeto.");
            return;
        }

        string[] nombresMateriales = { "Pradera", "Pradera_Sombra", "Pradera_Sol", "Roca_Costa", "Senda_Tierra", "Rio", "Arena" };
        var materiales = new Material[nombresMateriales.Length];
        for (int i = 0; i < nombresMateriales.Length; i++)
        {
            string prefijo = vistaPrevia ? "Mat_Isla_Previa_" : "Mat_Isla_";
            string carpetaMateriales = vistaPrevia ? "Materials/Preview" : "Materials";
            string ruta = $"{Carpeta}/{carpetaMateriales}/{prefijo}{nombresMateriales[i]}_Quibli.mat";
            materiales[i] = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (materiales[i] == null)
            {
                Debug.LogError($"[IslaPostgame] Falta {ruta}. No se ha cambiado ningún objeto.");
                return;
            }
        }

        int reasignados = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (MeshRenderer render in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filtro = render.GetComponent<MeshFilter>();
            bool esTerrenoIsla = render.gameObject.name == NombreTerreno ||
                (filtro != null && filtro.sharedMesh != null && filtro.sharedMesh.name == "Isla_Postgame_Quibli");
            if (!esTerrenoIsla) continue;
            Undo.RecordObject(render, "Aplicar la paleta Quibli a la isla postgame");
            render.sharedMaterials = materiales;
            EditorUtility.SetDirty(render);
            reasignados++;
        }

        if (reasignados == 0)
        {
            Debug.LogError($"[IslaPostgame] No encuentro el terreno de la isla en {nombreEscena}. No se ha cambiado ningún objeto.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"[IslaPostgame] Paleta Quibli aplicada al terreno de isla en {nombreEscena} ({reasignados} objeto(s)).");
    }

    private static bool EsEscenaObjetivo(Scene escena) =>
        escena.IsValid() && escena.isLoaded && escena.name == EscenaObjetivo &&
        string.Equals(escena.path, RutaEscenaObjetivo, StringComparison.OrdinalIgnoreCase);

    private static void CrearCarpetas()
    {
        CrearCarpetaSiFalta(Carpeta);
        CrearCarpetaSiFalta(Carpeta + "/Meshes");
        CrearCarpetaSiFalta(Carpeta + "/Materials");
        CrearCarpetaSiFalta(Carpeta + "/Textures");
        CrearCarpetaSiFalta(Carpeta + "/Materials/Preview");
        CrearCarpetaSiFalta(Carpeta + "/Textures/Preview");
    }

    private static void CrearCarpetaSiFalta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta)?.Replace('\\', '/');
        string nombre = Path.GetFileName(ruta);
        if (!string.IsNullOrEmpty(padre) && !AssetDatabase.IsValidFolder(padre)) CrearCarpetaSiFalta(padre);
        if (!string.IsNullOrEmpty(padre)) AssetDatabase.CreateFolder(padre, nombre);
    }

    private static Material[] CrearMateriales(Material plantilla, bool vistaPrevia = false)
    {
        // Tonos tomados de las capas de terreno que usa MainWorld (EldoriaCodexBuilder).
        return new[]
        {
            CrearMaterial("Pradera", plantilla, new Color(.32f, .46f, .20f), vistaPrevia),
            CrearMaterial("Pradera_Sombra", plantilla, new Color(.22f, .32f, .15f), vistaPrevia),
            CrearMaterial("Pradera_Sol", plantilla, new Color(.43f, .49f, .25f), vistaPrevia),
            CrearMaterial("Roca_Costa", plantilla, new Color(.38f, .39f, .36f), vistaPrevia),
            CrearMaterial("Senda_Tierra", plantilla, new Color(.47f, .35f, .22f), vistaPrevia),
            CrearMaterial("Rio", plantilla, new Color(.10f, .48f, .68f), vistaPrevia),
            CrearMaterial("Arena", plantilla, new Color(.79f, .72f, .52f), vistaPrevia),
            CrearMaterial("Mar_Prevision", plantilla, new Color(.035f, .24f, .63f), vistaPrevia)
        };
    }

    private static Material CrearMaterial(string nombre, Material plantilla, Color color, bool vistaPrevia = false)
    {
        string prefijo = vistaPrevia ? "Mat_Isla_Previa_" : "Mat_Isla_";
        string carpetaMateriales = vistaPrevia ? "Materials/Preview" : "Materials";
        string ruta = $"{Carpeta}/{carpetaMateriales}/{prefijo}{nombre}_Quibli.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(plantilla) { name = $"Mat_Isla_{nombre}_Quibli" };
            AssetDatabase.CreateAsset(material, ruta);
        }

        material.shader = plantilla.shader;
        Texture2D textura = CrearTexturaAlbedo(nombre, vistaPrevia);
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", textura);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
        }
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", textura);
        if (material.HasProperty("_TextureImpact")) material.SetFloat("_TextureImpact", 1f);
        if (material.HasProperty("_DetailMapImpact")) material.SetFloat("_DetailMapImpact", 0f);
        if (material.HasProperty("_DetailMap")) material.SetTexture("_DetailMap", null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CrearTexturaAlbedo(string nombre, bool vistaPrevia)
    {
        string carpetaTexturas = vistaPrevia ? "Textures/Preview" : "Textures";
        string prefijo = vistaPrevia ? "Isla_Previa_" : "Isla_";
        string ruta = $"{Carpeta}/{carpetaTexturas}/{prefijo}{nombre}_Albedo.png";
        const int resolucion = 512;
        var temporal = new Texture2D(resolucion, resolucion, TextureFormat.RGBA32, true, false);
        var pixeles = new Color32[resolucion * resolucion];
        for (int y = 0; y < resolucion; y++)
        for (int x = 0; x < resolucion; x++)
        {
            float macro = Mathf.PerlinNoise(x * .009f + .2f, y * .009f + .7f);
            float medio = Mathf.PerlinNoise(x * .035f + 12.4f, y * .035f + 4.8f);
            float fino = Mathf.PerlinNoise(x * .11f + 31.2f, y * .11f + 9.1f);
            float ruido = .97f + macro * .018f + medio * .009f + fino * .003f;
            Color muestra = new(ruido, ruido, ruido, 1f);
            pixeles[y * resolucion + x] = muestra;
        }
        temporal.SetPixels32(pixeles);
        temporal.Apply();
        File.WriteAllBytes(ruta, temporal.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(temporal);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    private static Mesh ConstruirMalla()
    {
        int nx = Mathf.CeilToInt(RadioX * 2f / Paso);
        int nz = Mathf.CeilToInt(RadioZ * 2f / Paso);
        int filas = nz + 1;
        var vertices = new List<Vector3>((nx + 1) * filas);
        var uv = new List<Vector2>((nx + 1) * filas);
        var submallas = CrearListasIndices(7);

        for (int ix = 0; ix <= nx; ix++)
        for (int iz = 0; iz <= nz; iz++)
        {
            float x = -RadioX + ix * Paso;
            float z = -RadioZ + iz * Paso;
            Vector2 punto = new(x, z);
            vertices.Add(PuntoTerreno(punto));
            uv.Add(new Vector2((x + RadioX) / (RadioX * 2f), (z + RadioZ) / (RadioZ * 2f)));
        }

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float x0 = -RadioX + ix * Paso, x1 = x0 + Paso;
            float z0 = -RadioZ + iz * Paso, z1 = z0 + Paso;
            Vector2 a = new(x0, z0), b = new(x0, z1), c = new(x1, z1), d = new(x1, z0);
            int ia = ix * filas + iz;
            int ib = ia + 1;
            int id = (ix + 1) * filas + iz;
            int ic = id + 1;
            if (((ix + iz) & 1) == 0)
            {
                AnadirTriangulo(a, b, c, ia, ib, ic, submallas, vertices);
                AnadirTriangulo(a, c, d, ia, ic, id, submallas, vertices);
            }
            else
            {
                AnadirTriangulo(a, b, d, ia, ib, id, submallas, vertices);
                AnadirTriangulo(b, c, d, ib, ic, id, submallas, vertices);
            }
        }

        ConstruirAcantilado(submallas[3], vertices, uv);
        var malla = new Mesh { name = "Isla_Postgame_Quibli", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.SetVertices(vertices);
        malla.SetUVs(0, uv);
        malla.subMeshCount = submallas.Length;
        for (int i = 0; i < submallas.Length; i++) malla.SetTriangles(submallas[i], i, false);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    private static List<int>[] CrearListasIndices(int cantidad)
    {
        var resultado = new List<int>[cantidad];
        for (int i = 0; i < cantidad; i++) resultado[i] = new List<int>();
        return resultado;
    }

    private static void AnadirTriangulo(Vector2 a, Vector2 b, Vector2 c, int ia, int ib, int ic,
        List<int>[] submallas, List<Vector3> vertices)
    {
        Vector2 centro = (a + b + c) / 3f;
        float radio = RadioIsla(centro);
        if (radio > 1.015f) return;

        Vector3 va = vertices[ia], vb = vertices[ib], vc = vertices[ic];
        Vector3 normal = Vector3.Cross(vb - va, vc - va).normalized;
        float distanciaSenda = DistanciaSenda(centro);
        float altura = (va.y + vb.y + vc.y) / 3f;
        int material;

        if (distanciaSenda < AnchoSenda) material = 4;
        else if (altura > 8f && normal.y < .86f) material = 3;
        else if (radio > .875f) material = 6;
        else material = 0;

        submallas[material].Add(ia);
        submallas[material].Add(ib);
        submallas[material].Add(ic);
    }

    private static Vector3 PuntoTerreno(Vector2 punto)
    {
        float altura = Altura(punto);
        float anchura = AnchoRioEn(punto.y);
        if (DistanciaRio(punto) < anchura)
            altura -= .38f;
        return new Vector3(punto.x, altura, punto.y);
    }

    private static float Altura(Vector2 p)
    {
        float radio = RadioIsla(p);
        float relieve = Mathf.Max(
            Pico(p, new Vector2(-66f, 29f), 22f, 27f, 30f),
            Mathf.Max(
                Pico(p, new Vector2(-12f, 76f), 20f, 48f, 34f),
                Mathf.Max(
                    Pico(p, new Vector2(51f, 53f), 13f, 37f, 42f),
                    Pico(p, new Vector2(76f, -30f), 9f, 40f, 45f))));

        float ruido = (Mathf.PerlinNoise(p.x * .018f + 21f, p.y * .018f + 8f) - .5f) * 5f +
            (Mathf.PerlinNoise(p.x * .047f + 7f, p.y * .047f + 3f) - .5f) * 3f +
            (Mathf.PerlinNoise(p.x * .11f + 11f, p.y * .11f + 5f) - .5f) * 1.4f;
        float borde = Suave(.68f, .96f, radio);
        float altura = Mathf.Lerp(relieve + ruido, -.9f, borde);

        float radioCordillera = Mathf.Sqrt(Mathf.Pow(p.x / (RadioX * .70f), 2f) + Mathf.Pow(p.y / (RadioZ * .70f), 2f));
        float ruidoCresta = .76f + .34f * Mathf.PerlinNoise(p.x * .018f + 53f, p.y * .018f + 19f);
        float cresta = 28f * Mathf.Exp(-Mathf.Pow((radioCordillera - 1f) / .17f, 2f)) * ruidoCresta;
        float corteRio = Mathf.Exp(-Mathf.Pow(DistanciaRio(p) / 12f, 2f));
        float corteSenda = Mathf.Exp(-Mathf.Pow(DistanciaSenda(p) / 8f, 2f));
        cresta *= 1f - .86f * Mathf.Max(corteRio, corteSenda);
        altura += cresta;

        float distanciaPueblo = p.magnitude;
        float claro = 1f - Suave(28f, 51f, distanciaPueblo);
        altura = Mathf.Lerp(altura, .25f, claro);

        float anchuraRio = AnchoRioEn(p.y);
        float influenciaRio = 1f - Suave(anchuraRio, anchuraRio + 8f, DistanciaRio(p));
        altura -= 1.15f * influenciaRio;

        float influenciaSenda = 1f - Suave(AnchoSenda, AnchoSenda + 3.5f, DistanciaSenda(p));
        altura -= .28f * influenciaSenda;
        return altura;
    }

    private static float Pico(Vector2 p, Vector2 centro, float altura, float radioX, float radioZ)
    {
        float x = (p.x - centro.x) / radioX;
        float z = (p.y - centro.y) / radioZ;
        return altura * Mathf.Exp(-(x * x + z * z) * 1.25f);
    }

    private static float RadioIsla(Vector2 p)
    {
        float angulo = Mathf.Atan2(p.y / RadioZ, p.x / RadioX);
        float costa = 1f + .045f * Mathf.Sin(angulo * 3f + .4f) +
            .025f * Mathf.Sin(angulo * 7f - .8f) + .014f * Mathf.Sin(angulo * 11f + 1.7f);
        return Mathf.Sqrt((p.x * p.x) / (RadioX * RadioX) + (p.y * p.y) / (RadioZ * RadioZ)) / costa;
    }

    // Ancla la curva en la posición real del puente y la hace serpentear hacia ambas costas.
    private static float CentroRioX(float z)
    {
        float factorMeandro = Suave(14f, 54f, Mathf.Abs(z - PuntoCruceRio.y));
        float curva = (Mathf.Sin(z * .024f) - Mathf.Sin(PuntoCruceRio.y * .024f)) * 28f +
            (Mathf.Sin(z * .061f) - Mathf.Sin(PuntoCruceRio.y * .061f)) * 8f +
            (Mathf.Sin(z * .105f) - Mathf.Sin(PuntoCruceRio.y * .105f)) * 3f;
        float x = PuntoCruceRio.x + curva * factorMeandro;
        float semiancho = AnchoRioEn(z);
        foreach (Rect casa in ZonasCasas)
        {
            float margenZ = semiancho + 1.5f;
            if (z < casa.yMin - margenZ || z > casa.yMax + margenZ) continue;
            float limiteOeste = casa.xMin - semiancho - 2f;
            float limiteEste = casa.xMax + semiancho + 2f;
            if (x > limiteOeste && x < limiteEste)
            {
                x = Mathf.Abs(limiteEste - PuntoCruceRio.x) < Mathf.Abs(limiteOeste - PuntoCruceRio.x)
                    ? limiteEste : limiteOeste;
            }
        }
        return x;
    }
    private static float DistanciaRio(Vector2 p) => Mathf.Abs(p.x - CentroRioX(p.y));
    private static float AnchoRioEn(float z)
    {
        float anchuraNormal = AnchoRio + .55f * Mathf.Exp(-Mathf.Pow((z + 54f) / 13f, 2f)) +
            .65f * Mathf.Exp(-Mathf.Pow((z - 20f) / 16f, 2f));
        float estrechamientoPuente = 1f - Suave(5f, 22f, Mathf.Abs(z - PuntoCruceRio.y));
        float limitePuente = Mathf.Min(anchuraNormal, LimiteRioEnPuente);
        return Mathf.Lerp(anchuraNormal, limitePuente, estrechamientoPuente);
    }

    private static float DistanciaSenda(Vector2 p)
    {
        float minima = float.MaxValue;
        foreach (Vector2[] ruta in Rutas)
        for (int i = 0; i < ruta.Length - 1; i++)
            minima = Mathf.Min(minima, DistanciaSegmento(p, ruta[i], ruta[i + 1]));
        return minima;
    }

    private static float DistanciaSegmento(Vector2 punto, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(punto - a, ab) / Mathf.Max(.001f, ab.sqrMagnitude));
        return Vector2.Distance(punto, a + ab * t);
    }

    private static float Suave(float desde, float hasta, float valor) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(desde, hasta, valor));

    private static void MedirAnchuraPuente(GameObject puente)
    {
        Renderer[] renderers = puente.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            LimiteRioEnPuente = 1.6f;
            Debug.LogWarning("[IslaPostgame] El puente no tiene Renderer medible; se usará un río estrecho bajo el tablero.");
            return;
        }

        Bounds conjunto = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) conjunto.Encapsulate(renderers[i].bounds);
        float anchoTablero = Mathf.Max(conjunto.size.x, conjunto.size.z);
        LimiteRioEnPuente = Mathf.Max(1f, anchoTablero * .30f);
    }

    private static void RecogerZonasCasas(Scene escena, Vector3 centro)
    {
        ZonasCasas.Clear();
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Casa_", StringComparison.Ordinal)) continue;
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;
            Bounds conjunto = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) conjunto.Encapsulate(renderers[i].bounds);
            Rect zona = Rect.MinMaxRect(
                conjunto.min.x - centro.x, conjunto.min.z - centro.z,
                conjunto.max.x - centro.x, conjunto.max.z - centro.z);
            zona = Rect.MinMaxRect(
                zona.xMin - 1.5f, zona.yMin - 1.5f,
                zona.xMax + 1.5f, zona.yMax + 1.5f);
            ZonasCasas.Add(zona);
        }
    }

    private static void ConstruirAcantilado(List<int> indices, List<Vector3> vertices, List<Vector2> uv)
    {
        const int segmentos = 192;
        const float escalaSuperior = 1.008f;
        const float escalaInferior = 1.075f;
        const float fondo = -31f;
        Vector3 centroAbajo = new(0f, fondo, 0f);
        var anilloAbajo = new Vector3[segmentos];

        for (int i = 0; i < segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / segmentos;
            Vector3 arriba = PuntoCosta(angulo, escalaSuperior, -.9f);
            Vector3 abajo = PuntoCosta(angulo, escalaInferior, fondo);
            anilloAbajo[i] = abajo;
            Vector3 arribaSiguiente = PuntoCosta((i + 1) * Mathf.PI * 2f / segmentos, escalaSuperior, -.9f);
            Vector3 abajoSiguiente = PuntoCosta((i + 1) * Mathf.PI * 2f / segmentos, escalaInferior, fondo);
            AnadirCaraAcantilado(arriba, arribaSiguiente, abajo, indices, vertices, uv);
            AnadirCaraAcantilado(arribaSiguiente, abajoSiguiente, abajo, indices, vertices, uv);
        }

        int centro = vertices.Count;
        vertices.Add(centroAbajo);
        uv.Add(Vector2.zero);
        for (int i = 0; i < segmentos; i++)
        {
            int inicio = vertices.Count;
            vertices.Add(anilloAbajo[(i + 1) % segmentos]);
            vertices.Add(anilloAbajo[i]);
            uv.Add(new Vector2(.5f + anilloAbajo[(i + 1) % segmentos].x * .01f, .5f + anilloAbajo[(i + 1) % segmentos].z * .01f));
            uv.Add(new Vector2(.5f + anilloAbajo[i].x * .01f, .5f + anilloAbajo[i].z * .01f));
            indices.Add(centro); indices.Add(inicio + 1); indices.Add(inicio);
        }
    }

    private static Vector3 PuntoCosta(float angulo, float escala, float y)
    {
        float irregularidad = 1f + .045f * Mathf.Sin(angulo * 3f + .4f) +
            .025f * Mathf.Sin(angulo * 7f - .8f) + .014f * Mathf.Sin(angulo * 11f + 1.7f);
        return new Vector3(
            Mathf.Cos(angulo) * RadioX * irregularidad * escala,
            y,
            Mathf.Sin(angulo) * RadioZ * irregularidad * escala);
    }

    private static void AnadirCaraAcantilado(Vector3 a, Vector3 b, Vector3 c, List<int> indices,
        List<Vector3> vertices, List<Vector2> uv)
    {
        int inicio = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uv.Add(new Vector2(a.x * .06f, a.z * .06f));
        uv.Add(new Vector2(b.x * .06f, b.z * .06f));
        uv.Add(new Vector2(c.x * .06f, c.z * .06f));
        indices.Add(inicio); indices.Add(inicio + 1); indices.Add(inicio + 2);
    }

    private static Mesh ConstruirMar()
    {
        const int segmentos = 96;
        const float radio = 440f;
        var vertices = new List<Vector3>(segmentos + 1);
        var uv = new List<Vector2>(segmentos + 1);
        var indices = new List<int>(segmentos * 3);
        vertices.Add(new Vector3(0f, -1.7f, 0f));
        uv.Add(new Vector2(.5f, .5f));
        for (int i = 0; i < segmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / segmentos;
            float x = Mathf.Cos(angulo) * radio, z = Mathf.Sin(angulo) * radio;
            vertices.Add(new Vector3(x, -1.7f, z));
            uv.Add(new Vector2(.5f + x / (radio * 2f), .5f + z / (radio * 2f)));
            if (i > 0) { indices.Add(0); indices.Add(i + 1); indices.Add(i); }
        }
        indices.Add(0); indices.Add(1); indices.Add(segmentos);
        var malla = new Mesh { name = "Mar_Prevision_Quibli" };
        malla.SetVertices(vertices);
        malla.SetUVs(0, uv);
        malla.SetTriangles(indices, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    private static Mesh ConstruirRio()
    {
        const int segmentos = 112;
        const float zInicio = -122f;
        const float zFin = 122f;
        var vertices = new List<Vector3>((segmentos + 1) * 2);
        var uv = new List<Vector2>((segmentos + 1) * 2);
        var indices = new List<int>(segmentos * 6);

        for (int i = 0; i <= segmentos; i++)
        {
            float t = i / (float)segmentos;
            float z = Mathf.Lerp(zInicio, zFin, t);
            float centroX = CentroRioX(z);
            float anchura = AnchoRioEn(z);
            Vector2 izquierda = new(centroX - anchura, z);
            Vector2 derecha = new(centroX + anchura, z);
            vertices.Add(new Vector3(izquierda.x, Altura(izquierda) - .04f, izquierda.y));
            vertices.Add(new Vector3(derecha.x, Altura(derecha) - .04f, derecha.y));
            uv.Add(new Vector2(0f, t * 24f));
            uv.Add(new Vector2(1f, t * 24f));

            if (i == 0) continue;
            int actual = i * 2;
            int anterior = actual - 2;
            indices.Add(anterior); indices.Add(actual); indices.Add(anterior + 1);
            indices.Add(actual); indices.Add(actual + 1); indices.Add(anterior + 1);
        }

        var malla = new Mesh { name = "Rio_Isla_Postgame_Quibli" };
        malla.SetVertices(vertices);
        malla.SetUVs(0, uv);
        malla.SetTriangles(indices, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    private static Mesh CrearOModificarMalla(string ruta, Mesh nueva)
    {
        Mesh existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente == null)
        {
            AssetDatabase.CreateAsset(nueva, ruta);
            return nueva;
        }
        existente.Clear();
        existente.indexFormat = nueva.indexFormat;
        existente.vertices = nueva.vertices;
        existente.uv = nueva.uv;
        existente.subMeshCount = nueva.subMeshCount;
        for (int i = 0; i < nueva.subMeshCount; i++) existente.SetTriangles(nueva.GetTriangles(i), i, false);
        existente.RecalculateNormals();
        existente.RecalculateBounds();
        UnityEngine.Object.DestroyImmediate(nueva);
        EditorUtility.SetDirty(existente);
        return existente;
    }

    private static void PrepararDecoradoParaElModulo(Scene escena, Transform modulo)
    {
        string[] raicesIntegrables = { "DECORADO", "06_Vegetacion", "Montana_Prologo", "PROP_Escudo", "PROP_PuertaSendero" };
        foreach (string nombre in raicesIntegrables)
        {
            GameObject objeto = Buscar(escena, nombre);
            if (objeto == null || objeto.transform == modulo || objeto.transform.IsChildOf(modulo)) continue;
            Undo.SetTransformParent(objeto.transform, modulo, "Agrupar el entorno de la isla postgame");
        }
    }

    private static void AjustarVegetacionExistente(Transform vegetacion, Vector3 centro)
    {
        Transform extrasAntiguos = vegetacion.Find("Rodales_Quibli_Extra");
        if (extrasAntiguos != null) DesactivarObjeto(extrasAntiguos.gameObject);

        foreach (Transform planta in vegetacion)
        {
            if (planta.name == NombreVegetacionNueva || planta.name == NombrePraderaNueva || planta == extrasAntiguos || !planta.gameObject.activeSelf) continue;
            Vector3 local = planta.position - centro;
            Vector2 p = new(local.x, local.z);
            bool dentro = RadioIsla(p) < .9f;
            bool libre = p.magnitude > 30f && DistanciaRio(p) > 7f && DistanciaSenda(p) > 5.5f;
            if (!dentro || !libre)
            {
                Undo.RecordObject(planta.gameObject, "Limpiar vegetación de la costa, el río y las sendas de la isla");
                planta.gameObject.SetActive(false);
                EditorUtility.SetDirty(planta.gameObject);
                continue;
            }
            Undo.RecordObject(planta, "Asentar vegetación en el relieve de la isla");
            planta.position = new Vector3(planta.position.x, centro.y + Altura(p), planta.position.z);
            EditorUtility.SetDirty(planta);
        }
    }

    private static void CrearBosquetes(Transform vegetacion, Vector3 centro, GameObject[] prefabs)
    {
        Transform grupo = vegetacion.Find(NombreVegetacionNueva);
        if (grupo == null)
        {
            var go = new GameObject(NombreVegetacionNueva);
            Undo.RegisterCreatedObjectUndo(go, "Crear bosquetes de la isla postgame");
            go.transform.SetParent(vegetacion, false);
            grupo = go.transform;
        }

        var anteriores = new List<Vector2>();
        for (int i = 0; i < vegetacion.childCount; i++)
        {
            Transform t = vegetacion.GetChild(i);
            if (t == grupo || !t.gameObject.activeSelf) continue;
            Vector3 p = t.position - centro;
            if (RadioIsla(new Vector2(p.x, p.z)) < .9f) anteriores.Add(new Vector2(p.x, p.z));
        }
        List<Vector2> posiciones = CalcularPosicionesBosquetes(anteriores);

        for (int i = 0; i < posiciones.Count; i++)
        {
            string nombre = $"Arbol_Isla_{i + 1:00}";
            Transform arbol = grupo.Find(nombre);
            GameObject instancia = arbol != null ? arbol.gameObject : null;
            if (instancia == null)
            {
                instancia = PrefabUtility.InstantiatePrefab(prefabs[i % prefabs.Length], grupo) as GameObject;
                if (instancia == null) continue;
                Undo.RegisterCreatedObjectUndo(instancia, "Añadir árbol al bosquete de la isla");
                instancia.name = nombre;
            }

            Vector2 p = posiciones[i];
            float escala = .82f + (i % 5) * .09f;
            Undo.RecordObject(instancia.transform, "Colocar árbol en el bosquete de la isla");
            instancia.transform.SetPositionAndRotation(
                centro + new Vector3(p.x, Altura(p), p.y), Quaternion.Euler(0f, (i * 71) % 360, 0f));
            instancia.transform.localScale = Vector3.one * escala;
            if (!instancia.activeSelf) instancia.SetActive(true);
            EditorUtility.SetDirty(instancia.transform);
        }

        for (int i = posiciones.Count; i < grupo.childCount; i++)
        {
            Transform sobrante = grupo.GetChild(i);
            if (sobrante.gameObject.activeSelf)
            {
                Undo.RecordObject(sobrante.gameObject, "Actualizar cantidad de árboles de la isla");
                sobrante.gameObject.SetActive(false);
            }
        }
        EditorUtility.SetDirty(grupo);
    }

    private static List<Vector2> CalcularPosicionesBosquetes(List<Vector2> existentes)
    {
        var resultado = new List<Vector2>(98);
        var ocupadas = new List<Vector2>(existentes);
        var aleatorio = new System.Random(24109);
        for (int bosque = 0; bosque < CentrosBosque.Length; bosque++)
        {
            int colocados = 0;
            for (int intento = 0; intento < 300 && colocados < 14; intento++)
            {
                float angulo = (float)aleatorio.NextDouble() * Mathf.PI * 2f;
                float radio = Mathf.Sqrt((float)aleatorio.NextDouble()) * (bosque % 2 == 0 ? 30f : 26f);
                Vector2 p = CentrosBosque[bosque] + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * radio;
                if (RadioIsla(p) > .85f || p.magnitude < 36f || DistanciaRio(p) < AnchoRioEn(p.y) + 10f || DistanciaSenda(p) < 10f) continue;

                bool solapado = false;
                foreach (Vector2 otro in ocupadas) if (Vector2.Distance(p, otro) < 8f) { solapado = true; break; }
                if (solapado) continue;
                resultado.Add(p);
                ocupadas.Add(p);
                colocados++;
            }
        }
        return resultado;
    }

    private static void CrearPraderaDecorativa(Transform vegetacion, Vector3 centro, GameObject[] prefabs)
    {
        Transform grupo = vegetacion.Find(NombrePraderaNueva);
        if (grupo == null)
        {
            var go = new GameObject(NombrePraderaNueva);
            Undo.RegisterCreatedObjectUndo(go, "Crear flores y césped de la isla postgame");
            go.transform.SetParent(vegetacion, false);
            grupo = go.transform;
        }

        Vector2[] centros =
        {
            new(-85f, -50f), new(-105f, 5f), new(-70f, 70f), new(-10f, 90f),
            new(58f, 78f), new(105f, 37f), new(115f, -12f), new(83f, -68f),
            new(25f, -87f), new(-50f, -72f), new(-110f, 34f), new(70f, 12f),
            new(-124f, -18f), new(-27f, 107f), new(18f, -104f), new(130f, 14f),
            new(86f, 82f), new(-112f, -58f), new(-25f, 55f), new(102f, -49f),
            new(-92f, 38f), new(-78f, 40f)
        };
        var posiciones = new List<Vector2>(440);
        var aleatorio = new System.Random(58231);
        for (int zona = 0; zona < centros.Length; zona++)
        {
            int colocados = 0;
            for (int intento = 0; intento < 360 && colocados < 20; intento++)
            {
                float angulo = (float)aleatorio.NextDouble() * Mathf.PI * 2f;
                float radio = Mathf.Sqrt((float)aleatorio.NextDouble()) * 15f;
                Vector2 p = centros[zona] + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * radio;
                float orillaRio = AnchoRioEn(p.y);
                if (RadioIsla(p) > .82f || p.magnitude < 42f ||
                    DistanciaRio(p) < orillaRio + 2.5f ||
                    DistanciaSenda(p) < 8f) continue;

                bool solapado = false;
                foreach (Vector2 otro in posiciones)
                    if (Vector2.Distance(p, otro) < 1.7f) { solapado = true; break; }
                if (solapado) continue;
                posiciones.Add(p);
                colocados++;
            }
        }

        for (int i = 0; i < posiciones.Count; i++)
        {
            string nombre = $"Detalle_Pradera_{i + 1:000}";
            Transform detalle = grupo.Find(nombre);
            GameObject instancia = detalle != null ? detalle.gameObject : null;
            int indicePrefab = i % 5;
            if (instancia == null)
            {
                instancia = PrefabUtility.InstantiatePrefab(prefabs[indicePrefab], grupo) as GameObject;
                if (instancia == null) continue;
                Undo.RegisterCreatedObjectUndo(instancia, "Añadir flores y césped a la isla");
                instancia.name = nombre;
            }

            Vector2 p = posiciones[i];
            float escala = indicePrefab < 2 ? 2.0f + (i % 4) * .22f : 1.35f + (i % 4) * .16f;
            Undo.RecordObject(instancia.transform, "Colocar detalle de pradera en la isla");
            instancia.transform.SetPositionAndRotation(
                centro + new Vector3(p.x, Altura(p), p.y), Quaternion.Euler(0f, (i * 137) % 360, 0f));
            instancia.transform.localScale = Vector3.one * escala;
            if (!instancia.activeSelf) instancia.SetActive(true);
            Collider[] colisionadores = instancia.GetComponentsInChildren<Collider>(true);
            foreach (Collider colisionador in colisionadores)
                if (colisionador.enabled) colisionador.enabled = false;
            EditorUtility.SetDirty(instancia.transform);
        }

        for (int i = posiciones.Count; i < grupo.childCount; i++)
        {
            Transform sobrante = grupo.GetChild(i);
            if (sobrante.gameObject.activeSelf) sobrante.gameObject.SetActive(false);
        }
        EditorUtility.SetDirty(grupo);
    }

    private static void CrearRinconColumpio(Transform vegetacion, Vector3 centro, GameObject prefabArbol, Material madera)
    {
        const string nombre = "Rincon_Columpio_Isla_Postgame";
        const string nombreArbol = "Arbol_Columpio_Quibli";
        const string nombreColumpio = "Columpio_Dos_Cuerdas_y_Madera";
        Vector2 punto = new(-66f, 29f);
        Transform rincon = vegetacion.Find(nombre);
        if (rincon == null)
        {
            var objeto = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(objeto, "Crear rincón del columpio en la isla");
            objeto.transform.SetParent(vegetacion, false);
            rincon = objeto.transform;
        }

        Undo.RecordObject(rincon, "Colocar el rincón del columpio en la colina");
        rincon.SetPositionAndRotation(centro + new Vector3(punto.x, Altura(punto), punto.y), Quaternion.Euler(0f, 35f, 0f));
        rincon.localScale = Vector3.one;

        Transform arbolExistente = rincon.Find(nombreArbol);
        GameObject arbol = arbolExistente != null ? arbolExistente.gameObject : null;
        if (arbol == null)
        {
            arbol = PrefabUtility.InstantiatePrefab(prefabArbol, rincon) as GameObject;
            if (arbol == null) return;
            Undo.RegisterCreatedObjectUndo(arbol, "Plantar el árbol del columpio");
            arbol.name = nombreArbol;
        }
        Undo.RecordObject(arbol.transform, "Ajustar árbol del rincón de la colina");
        arbol.transform.localPosition = Vector3.zero;
        arbol.transform.localRotation = Quaternion.Euler(0f, 112f, 0f);
        arbol.transform.localScale = Vector3.one * 1.15f;

        Transform columpio = rincon.Find(nombreColumpio);
        if (columpio == null)
        {
            var objeto = new GameObject(nombreColumpio);
            Undo.RegisterCreatedObjectUndo(objeto, "Crear columpio de dos cuerdas y asiento de madera");
            objeto.transform.SetParent(rincon, false);
            columpio = objeto.transform;
        }
        columpio.localPosition = new Vector3(1.35f, 0f, .15f);
        columpio.localRotation = Quaternion.identity;
        columpio.localScale = Vector3.one;

        CrearParteColumpio(columpio, "Cuerda_01", PrimitiveType.Cylinder, madera,
            new Vector3(0f, 4.55f, -.40f), new Vector3(.045f, 1.12f, .045f));
        CrearParteColumpio(columpio, "Cuerda_02", PrimitiveType.Cylinder, madera,
            new Vector3(0f, 4.55f, .40f), new Vector3(.045f, 1.12f, .045f));
        CrearParteColumpio(columpio, "Asiento_Madera", PrimitiveType.Cube, madera,
            new Vector3(0f, 3.36f, 0f), new Vector3(.42f, .14f, 1.02f));
        EditorUtility.SetDirty(rincon);
    }

    private static void CrearParteColumpio(Transform padre, string nombre, PrimitiveType tipo, Material material,
        Vector3 posicion, Vector3 escala)
    {
        Transform existente = padre.Find(nombre);
        GameObject parte = existente != null ? existente.gameObject : null;
        if (parte == null)
        {
            parte = GameObject.CreatePrimitive(tipo);
            Undo.RegisterCreatedObjectUndo(parte, "Construir columpio de la isla");
            parte.name = nombre;
            parte.transform.SetParent(padre, false);
        }

        Undo.RecordObject(parte.transform, "Ajustar una pieza del columpio");
        parte.transform.localPosition = posicion;
        parte.transform.localRotation = Quaternion.identity;
        parte.transform.localScale = escala;
        var render = parte.GetComponent<Renderer>();
        if (render != null) render.sharedMaterial = material;
        var collider = parte.GetComponent<Collider>();
        if (collider != null) Undo.DestroyObjectImmediate(collider);
        EditorUtility.SetDirty(parte);
    }

    private static void ArmonizarMontana(Scene escena, Material roca)
    {
        GameObject montana = Buscar(escena, "Montana_Prologo");
        var render = montana != null ? montana.GetComponent<Renderer>() : null;
        if (render != null)
        {
            Undo.RecordObject(render, "Armonizar la montaña con los acantilados Quibli de la isla");
            render.sharedMaterial = roca;
            EditorUtility.SetDirty(render);
        }
        var obstaculo = montana != null ? montana.GetComponent<NavMeshObstacle>() : null;
        if (obstaculo != null) Undo.DestroyObjectImmediate(obstaculo);
    }

    private static void PrepararMarDeVista(Scene escena, Vector3 centro, Mesh malla, Material material)
    {
        const string nombre = "PREVIEW_MAR_NO_INCLUIR_EN_MAINWORLD";
        GameObject mar = Buscar(escena, nombre);
        if (mar == null)
        {
            mar = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(mar, "Crear mar de previsualización de la isla");
            SceneManager.MoveGameObjectToScene(mar, escena);
        }
        mar.transform.SetParent(null, true);
        mar.transform.SetPositionAndRotation(centro, Quaternion.identity);
        var filtro = mar.GetComponent<MeshFilter>();
        if (filtro == null) filtro = Undo.AddComponent<MeshFilter>(mar);
        filtro.sharedMesh = malla;
        var render = mar.GetComponent<MeshRenderer>();
        if (render == null) render = Undo.AddComponent<MeshRenderer>(mar);
        render.sharedMaterial = material;
        var colisionador = mar.GetComponent<Collider>();
        if (colisionador != null) Undo.DestroyObjectImmediate(colisionador);
        EditorUtility.SetDirty(mar);
    }

    private static void PrepararRio(Scene escena, Transform modulo, Mesh malla, Material material)
    {
        const string nombre = "Rio_Cauce_Quibli";
        GameObject rio = Buscar(escena, nombre);
        if (rio == null)
        {
            rio = new GameObject(nombre);
            Undo.RegisterCreatedObjectUndo(rio, "Crear cauce de agua de la isla postgame");
            SceneManager.MoveGameObjectToScene(rio, escena);
        }
        rio.transform.SetParent(modulo, false);
        rio.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        rio.transform.localScale = Vector3.one;
        var filtro = rio.GetComponent<MeshFilter>();
        if (filtro == null) filtro = Undo.AddComponent<MeshFilter>(rio);
        filtro.sharedMesh = malla;
        var render = rio.GetComponent<MeshRenderer>();
        if (render == null) render = Undo.AddComponent<MeshRenderer>(rio);
        render.sharedMaterial = material;
        Collider colisionador = rio.GetComponent<Collider>();
        if (colisionador != null) Undo.DestroyObjectImmediate(colisionador);
        EditorUtility.SetDirty(rio);
    }

    private static void DesactivarComponente(Renderer componente)
    {
        if (componente == null || !componente.enabled) return;
        Undo.RecordObject(componente, "Ocultar suelo plano de la copia postgame");
        componente.enabled = false;
        EditorUtility.SetDirty(componente);
    }

    private static void DesactivarComponente(Collider componente)
    {
        if (componente == null || !componente.enabled) return;
        Undo.RecordObject(componente, "Retirar colisión del suelo plano de la copia postgame");
        componente.enabled = false;
        EditorUtility.SetDirty(componente);
    }

    private static void DesactivarObjeto(Scene escena, string nombre) => DesactivarObjeto(Buscar(escena, nombre));

    private static void DesactivarObjeto(GameObject objeto)
    {
        if (objeto == null || !objeto.activeSelf) return;
        Undo.RecordObject(objeto, "Retirar decoración de la escena postgame");
        objeto.SetActive(false);
        EditorUtility.SetDirty(objeto);
    }

    private static GameObject Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz;
            foreach (Transform transform in raiz.GetComponentsInChildren<Transform>(true))
                if (transform.name == nombre) return transform.gameObject;
        }
        return null;
    }
}
#endif
