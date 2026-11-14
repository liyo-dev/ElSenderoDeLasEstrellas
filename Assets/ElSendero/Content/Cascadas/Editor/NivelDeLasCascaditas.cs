using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kits.Waterfalls.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Nivel de las cascaditas (maqueta jugable, INC-1190): cinco terrazas que bajan, cada una con su
/// poza, y entre cada dos una cascada que cae separada de la pared. Detrás de cada cortina hay
/// agua para pasar nadando y una repisa seca; junto a la repisa, un rincón escondido en la pared.
/// Rampas al este para subir. Hora fija de día, sin clima, y el grupo al pie del todo.
/// Se rehace entera cada vez (escena Cascaditas.unity y sus recursos en Cascaditas_data).
public static class NivelDeLasCascaditas
{
    public const string RutaEscena = "Assets/Scenes/Worlds/Cascaditas.unity";
    private const string CarpetaDatos = "Assets/Scenes/Worlds/Cascaditas_data";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string CapasDelTerreno = "Assets/Scenes/Worlds/MainWorld_data/Recursos/Capa";
    private const string AguaDeReferencia = "Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaRio.mat";
    private const string CieloDeDia = "Assets/Art/Cielo/Cielo_Dia.mat";

    // Medidas (metros). El agua baja de z alto a z bajo.
    private const float Ancho = 120f, Largo = 180f, AltoMaximo = 70f;
    private const float FondoDePoza = 2.4f, BajoLaOrilla = 0.35f;
    private const float CentroDeLasCascadas = 60f, AnchoDeCascada = 6f, Separacion = 2.6f;
    private const float RepisaDetras = 1.7f;

    /// Terrazas: desde dónde empieza (z) y a qué altura está. La primera es la de arriba.
    private static readonly (float zDesde, float alto, float xPoza)[] Terrazas =
    {
        (135f, 32f, 50f),
        (100f, 24f, 70f),
        (65f, 16f, 50f),
        (30f, 8f, 70f),
        (0f, 0f, 60f),
    };
    private const float ParedDelFondo = 162f, AltoDelManantial = 50f;

    [MenuItem("El Sendero/Escenario/Cascaditas: montar el nivel (maqueta, INC-1190)")]
    public static void Montar()
    {
        var informe = new StringBuilder("[Cascaditas] ");
        var activaAntes = SceneManager.GetActiveScene();
        var abierta = SceneManager.GetSceneByPath(RutaEscena);
        if (abierta.IsValid() && abierta.isLoaded) EditorSceneManager.CloseScene(abierta, true);

        // Escena nueva junto a lo que haya abierto (no cierra nada).
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        CrearCarpeta(CarpetaDatos);
        EditorSceneManager.SaveScene(escena, RutaEscena);
        SceneManager.SetActiveScene(escena);

        var raiz = new GameObject("Nivel de las cascaditas");
        SceneManager.MoveGameObjectToScene(raiz, escena);

        var terreno = Terreno(raiz.transform, informe);
        Aguas(raiz.transform, informe);
        Cascadas(raiz.transform, informe);
        var luz = Ambiente(escena, informe);

        var nav = raiz.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;
        nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        nav.layerMask = LayerMask.GetMask(EscenaJugable.CapaDelSuelo);
        EscenaJugable.HornearNavMesh(nav);

        Vector3 salida = new Vector3(96f, Altura(96f, 12f) + 0.1f, 12f);
        EscenaJugable.ColocarGrupo(escena, salida, Vector3.forward);
        informe.Append("grupo al pie (sale mirando a las cascadas); ");

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, RutaEscena);
        PermitirLaInterfaz(informe);
        AssetDatabase.SaveAssets();

        if (activaAntes.IsValid() && activaAntes.isLoaded) SceneManager.SetActiveScene(activaAntes);
        EditorSceneManager.CloseScene(escena, true);
        informe.Append("hecho. Abre Cascaditas.unity y dale a Play.");
        Debug.Log(informe);
        OrdenesDelEditor.Avisar("Cascaditas", informe.ToString());
    }

    // ── Terreno ───────────────────────────────────────────────────────────────────────────

    private static Terrain Terreno(Transform padre, StringBuilder informe)
    {
        const int res = 513;
        var datos = new TerrainData { heightmapResolution = res, alphamapResolution = 256 };
        datos.size = new Vector3(Ancho, AltoMaximo, Largo);
        // El asset primero: si se crea después de pintar, el mapa de capas se guarda sin pintar.
        AssetDatabase.CreateAsset(datos, $"{CarpetaDatos}/Terreno.asset");
        var alturas = new float[res, res];
        for (int j = 0; j < res; j++)
            for (int i = 0; i < res; i++)
            {
                float x = i / (res - 1f) * Ancho, z = j / (res - 1f) * Largo;
                alturas[j, i] = Mathf.Clamp01(Altura(x, z) / AltoMaximo);
            }
        datos.SetHeights(0, 0, alturas);

        // Capas: hierba, tierra, piedra y arena (las de MainWorld), en una sola pasada.
        var capas = new[] { 1, 4, 2, 0 }.Select(n => AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{CapasDelTerreno}{n}.terrainlayer")).ToArray();
        if (capas.All(c => c != null))
        {
            datos.terrainLayers = capas;
            int ar = datos.alphamapResolution;
            var mapa = new float[ar, ar, 4];
            for (int j = 0; j < ar; j++)
                for (int i = 0; i < ar; i++)
                {
                    float x = i / (ar - 1f) * Ancho, z = j / (ar - 1f) * Largo;
                    int capa = CapaDelSuelo(x, z, datos.GetSteepness(i / (ar - 1f), j / (ar - 1f)));
                    mapa[j, i, capa] = 1f;
                }
            datos.SetAlphamaps(0, 0, mapa);
            var cuenta = new int[4];
            for (int j = 0; j < ar; j++)
                for (int i = 0; i < ar; i++)
                    for (int c = 0; c < 4; c++) if (mapa[j, i, c] > 0.5f) cuenta[c]++;
            var leido = datos.GetAlphamaps(0, 0, ar, ar);
            int piedraLeida = 0;
            for (int j = 0; j < ar; j++)
                for (int i = 0; i < ar; i++) if (leido[j, i, 2] > 0.5f) piedraLeida++;
            informe.Append($"capas: hierba {cuenta[0]}, tierra {cuenta[1]}, piedra {cuenta[2]} (leída {piedraLeida}), arena {cuenta[3]}; ");
        }
        else informe.Append("¡faltan las capas del terreno de MainWorld! ");

        EditorUtility.SetDirty(datos);
        AssetDatabase.SaveAssets();
        var go = Terrain.CreateTerrainGameObject(datos);
        go.name = "Terreno";
        go.transform.SetParent(padre, false);
        go.layer = LayerMask.NameToLayer(EscenaJugable.CapaDelSuelo);
        var terreno = go.GetComponent<Terrain>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Worlds/MainWorld_data/Recursos/Terreno.mat");
        if (mat != null)
        {
            var propio = new Material(mat) { name = "Terreno_Cascaditas" };
            propio.SetTexture("_ControlDeCapas0", null);
            propio.SetTexture("_ControlDeCapas1", null);
            AssetDatabase.CreateAsset(propio, $"{CarpetaDatos}/Terreno_Cascaditas.mat");
            terreno.materialTemplate = propio;
        }
        terreno.heightmapPixelError = 2f;
        informe.Append("terreno de 5 terrazas; ");
        return terreno;
    }

    /// Alto del suelo en (x, z): terrazas, pozas, paredes de cañón, rampas, repisas y rincones.
    private static float Altura(float x, float z)
    {
        int k = TerrazaEn(z);
        float h = Terrazas[k].alto;
        if (z >= ParedDelFondo) h = AltoDelManantial + 2f;

        // Pozas: una elipse a un lado, unida por un canal al borde por el que cae el agua y con una
        // hoya donde llega la cascada de arriba.
        {
            float z0 = Terrazas[k].zDesde, z1 = k == 0 ? ParedDelFondo : Terrazas[k - 1].zDesde;
            float cz = (z0 + z1) * 0.5f, rz = (z1 - z0) * 0.5f - 2f, cx = Terrazas[k].xPoza;
            float hondo = Mathf.Clamp01((1f - (Sqr((x - cx) / 13f) + Sqr((z - cz) / rz))) * 3f);
            float hoya = Mathf.Clamp01((1f - Sqr((x - CentroDeLasCascadas) / 8f) - Sqr((z - (z1 - Separacion - 3f)) / 6f)) * 3f);
            float canal = k == Terrazas.Length - 1 ? 0f
                : Mathf.Clamp01((AnchoDeCascada * 0.5f + 1f - Mathf.Abs(x - CentroDeLasCascadas)) * 1.5f)
                  * (z >= z0 + 0.6f && z < cz ? 1f : 0f) * 0.6f;
            float fondo = Mathf.Max(hondo, Mathf.Max(hoya, canal));
            if (fondo > 0f && z < ParedDelFondo) h = Terrazas[k].alto - FondoDePoza * fondo - (fondo > 0f ? 0.2f : 0f);
        }

        // Repisa detrás de cada cortina y rincón en la pared, al lado contrario de la poza de arriba.
        for (int t = 1; t < Terrazas.Length; t++)
        {
            float zc = Terrazas[t - 1].zDesde, abajo = Terrazas[t].alto;
            if (Mathf.Abs(x - CentroDeLasCascadas) < AnchoDeCascada * 0.5f + 2.5f && z < zc && z > zc - RepisaDetras)
                h = abajo + 0.3f;
            float lado = Terrazas[t - 1].xPoza < CentroDeLasCascadas ? 1f : -1f;
            float rx = CentroDeLasCascadas + lado * (AnchoDeCascada * 0.5f + 4f);
            if (Mathf.Abs(x - rx) < 2.6f && z >= zc - 0.01f && z < zc + 4f) h = abajo + 0.3f;
        }

        // Rampas al este: de la terraza de abajo a la de arriba en 16 m.
        for (int t = 1; t < Terrazas.Length; t++)
        {
            float zc = Terrazas[t - 1].zDesde;
            if (x > 96f && x < 104f && z > zc - 16f && z < zc + 1f)
                h = Mathf.Lerp(Terrazas[t].alto, Terrazas[t - 1].alto, Mathf.InverseLerp(zc - 16f, zc + 1f, z));
        }

        // Paredes del cañón a los lados.
        float pared = Mathf.Max(Mathf.InverseLerp(10f, 2f, x), Mathf.InverseLerp(110f, 118f, x));
        if (pared > 0f) h = Mathf.Max(h, Terrazas[k].alto + 16f * pared + 2f * Mathf.PerlinNoise(x * 0.08f, z * 0.08f));
        return h;
    }

    private static int CapaDelSuelo(float x, float z, float pendiente)
    {
        // Piedra donde el suelo sube o baja de golpe (paredes entre terrazas y del cañón).
        const float paso = 0.8f;
        float h = Altura(x, z);
        float salto = Mathf.Max(Mathf.Max(Mathf.Abs(Altura(x, z + paso) - h), Mathf.Abs(Altura(x, z - paso) - h)),
                                Mathf.Max(Mathf.Abs(Altura(x + paso, z) - h), Mathf.Abs(Altura(x - paso, z) - h)));
        if (salto > 1.2f || pendiente > 40f) return 2;
        int k = TerrazaEn(z);
        if (h < Terrazas[k].alto - 0.4f) return 3;           // arena del fondo y la orilla de la poza
        if (x > 95f && x < 105f) return 1;                    // tierra de las rampas
        return 0;                                             // hierba
    }

    private static int TerrazaEn(float z)
    {
        for (int t = 0; t < Terrazas.Length; t++) if (z >= Terrazas[t].zDesde) return t;
        return Terrazas.Length - 1;
    }

    private static float Sqr(float v) => v * v;

    // ── Agua ──────────────────────────────────────────────────────────────────────────────

    private static void Aguas(Transform padre, StringBuilder informe)
    {
        var grupo = new GameObject("Pozas").transform;
        grupo.SetParent(padre, false);
        var mat = MaterialDelAgua();
        int capaAgua = LayerMask.NameToLayer("Water");
        for (int t = 0; t < Terrazas.Length; t++)
        {
            float z0 = Terrazas[t].zDesde, z1 = t == 0 ? ParedDelFondo : Terrazas[t - 1].zDesde;
            float superficie = Terrazas[t].alto - BajoLaOrilla;
            var centro = new Vector3(Terrazas[t].xPoza, superficie, (z0 + z1) * 0.5f);
            var tam = new Vector3(30f, 0f, z1 - z0);

            var lamina = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lamina.name = $"Poza {t + 1}";
            Object.DestroyImmediate(lamina.GetComponent<Collider>());
            lamina.transform.SetParent(grupo, false);
            lamina.transform.position = centro;
            lamina.transform.localScale = new Vector3(tam.x / 10f, 1f, tam.z / 10f);
            var r = lamina.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var nado = new GameObject("Agua para nadar");
            if (capaAgua >= 0) nado.layer = capaAgua;
            nado.transform.SetParent(lamina.transform.parent, false);
            nado.transform.position = centro + Vector3.down * 1.6f;
            var caja = nado.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            caja.size = new Vector3(tam.x, 3.2f, tam.z);
        }
        informe.Append($"{Terrazas.Length} pozas con agua para nadar; ");
    }

    private static Material MaterialDelAgua()
    {
        string ruta = $"{CarpetaDatos}/Agua_Cascaditas.mat";
        var origen = AssetDatabase.LoadAssetAtPath<Material>(AguaDeReferencia);
        var mat = new Material(origen) { name = "Agua_Cascaditas" };
        mat.SetFloat("_Profundidad", 1.8f);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    // ── Cascadas ──────────────────────────────────────────────────────────────────────────

    private static void Cascadas(Transform padre, StringBuilder informe)
    {
        var grupo = new GameObject("Cascadas").transform;
        grupo.SetParent(padre, false);
        var (cortina, espuma) = CascadasDelJuego.Materiales();
        float x = CentroDeLasCascadas;

        // Manantial: cae de la pared del fondo a la primera poza.
        Caida(grupo, "Cascada del manantial", x, ParedDelFondo, AltoDelManantial + 2f, Terrazas[0].alto - BajoLaOrilla, cortina, espuma);
        int n = 1;
        for (int t = 1; t < Terrazas.Length; t++, n++)
            Caida(grupo, $"Cascada {n}", x, Terrazas[t - 1].zDesde, Terrazas[t - 1].alto - BajoLaOrilla, Terrazas[t].alto - BajoLaOrilla, cortina, espuma);
        informe.Append($"{n} cascadas con hueco detrás; ");
    }

    /// Una caída en el borde «zBorde»: el agua sale de la poza de arriba, se separa de la pared
    /// (lo justo para pasar por detrás) y cae a la poza de abajo.
    private static void Caida(Transform grupo, string nombre, float x, float zBorde, float arriba, float abajo,
                              Material cortina, Material espuma)
    {
        float alto = arriba - abajo;
        var puntos = new List<Vector3>
        {
            new Vector3(x, arriba, zBorde + 4f),
            new Vector3(x, arriba, zBorde + 1.2f),
            new Vector3(x, arriba - 0.1f, zBorde - 0.4f),
            new Vector3(x, arriba - 0.6f, zBorde - 1.6f),
            new Vector3(x, arriba - 1.6f, zBorde - Separacion * 0.85f),
        };
        int tramos = Mathf.Max(3, Mathf.CeilToInt(alto / 1.5f));
        for (int i = 1; i <= tramos; i++)
        {
            float f = i / (float)tramos;
            puntos.Add(new Vector3(x, Mathf.Lerp(arriba - 1.6f, abajo, f), zBorde - Mathf.Lerp(Separacion * 0.85f, Separacion, f)));
        }
        puntos.Add(new Vector3(x, abajo, zBorde - Separacion - 2f));
        puntos.Add(new Vector3(x, abajo, zBorde - Separacion - 5f));
        var anchos = puntos.Select(_ => AnchoDeCascada).ToList();
        WaterfallBuilder.Build(grupo, nombre, puntos, anchos, cortina, espuma, 0.05f);
    }

    // ── Ambiente ──────────────────────────────────────────────────────────────────────────

    private static Light Ambiente(Scene escena, StringBuilder informe)
    {
        var go = new GameObject("Ambiente");
        SceneManager.MoveGameObjectToScene(go, escena);
        var sol = new GameObject("Sol");
        sol.transform.SetParent(go.transform, false);
        sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        var luz = sol.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.shadows = LightShadows.Soft;

        var ciclo = go.AddComponent<DayNightCycle>();
        go.AddComponent<SolYLunaEnElCielo>();
        var so = new SerializedObject(ciclo);
        so.FindProperty("directionalLight").objectReferenceValue = luz;
        so.ApplyModifiedPropertiesWithoutUndo();
        var dia = new DayNightCycle.TimeOfDaySettings
        {
            timeOfDay = DayNightCycle.TimeOfDay.AfterNoon,
            skybox = AssetDatabase.LoadAssetAtPath<Material>(CieloDeDia),
            lightColor = new Color(1f, 0.95f, 0.86f), lightIntensity = 1.15f,
            ambientColor = new Color(0.62f, 0.68f, 0.78f), ambientIntensity = 1f,
            fogColor = new Color(0.78f, 0.88f, 0.95f), fogDensity = 0.0025f,
            duration = 600f,
        };
        ClimaDelCicloWiring.FijarHoraSinClima(ciclo, dia, 0.35f);
        informe.Append("día fijo sin clima; ");
        return luz;
    }

    private static void PermitirLaInterfaz(StringBuilder informe)
    {
        var start = SceneManager.GetSceneByPath(RutaStart);
        bool cerrar = !start.IsValid() || !start.isLoaded;
        if (cerrar) start = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
        int n = EscenaJugable.PermitirInterfazDeJuego(start, "Cascaditas");
        if (n > 0)
        {
            EditorSceneManager.SaveScene(start);
            informe.Append($"interfaz de juego también en Cascaditas ({n}); ");
        }
        if (cerrar) EditorSceneManager.CloseScene(start, true);
    }

    private static void CrearCarpeta(string carpeta)
    {
        if (AssetDatabase.IsValidFolder(carpeta)) return;
        int corte = carpeta.LastIndexOf('/');
        CrearCarpeta(carpeta.Substring(0, corte));
        AssetDatabase.CreateFolder(carpeta.Substring(0, corte), carpeta.Substring(corte + 1));
    }
}
