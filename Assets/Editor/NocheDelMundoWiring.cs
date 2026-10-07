using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Monta la noche del mundo en las escenas abiertas que tienen ciclo día/noche (INC-657):
///
/// 1. Ventanas: genera desde cada atlas del pack Fantasy Kingdom (FK01, FK02, FK04) su máscara de
///    cristales en Assets/Art/Noche (MascarasDeVentanas; si ya existe, no se rehace), se la pone en
///    `_EmissionMap`, activa `_EMISSION` y deja la emisión en negro; añade `VentanasIluminadas` al
///    objeto del `DayNightCycle`, que la sube de noche.
/// 2. Luces: un hijo «LuzNocturna (…)» con `Light` + `LuzNocturna` en cada farol, antorcha,
///    hoguera y casa del pack que no tenga ya una luz.
/// 3. Bosque: zonas con `LuciernagasNocturnas` y `NieblaNocturna` donde hay más árboles juntos,
///    lejos de las casas, bajo un objeto «Noche · Luciérnagas» (si ya existe, no se rehacen: borrarlo
///    para regenerarlas; a las zonas que ya existan sin niebla se les añade).
/// 4. Chimeneas: un hijo «Humo» con `HumoDeChimenea` en lo alto de cada malla Chimney* del pack.
/// 5. Cielo: `EstrellasFugaces` en el objeto del `DayNightCycle`.
///
/// Se puede ejecutar varias veces: lo que ya está no se toca.
public static class NocheDelMundoWiring
{
    private const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/";
    private static readonly string[] Atlas = { "FK01", "FK02", "FK04" };
    private const string CarpetaDeMascaras = "Assets/Art/Noche/";

    private const string MaterialDeLuciernaga = "Assets/_VFX/Hechizos/Materiales/M_Hechizo_Brillo_Aditivo.mat";
    private const string MaterialDeNiebla = "Assets/Settings/AmbientPresets/Mat_GroundMist.mat";
    private const string MaterialDeHumo = "Assets/_VFX/Hechizos/Materiales/M_Hechizo_Humo_Alfa.mat";
    private const string NombreDelHumo = "Humo";
    private const string PrefijoDeLuz = "LuzNocturna";
    private const string RaizDeLuciernagas = "Noche · Luciérnagas";

    private const float CeldaDeBosque = 24f;
    private const int ArbolesMinimosPorZona = 6;
    private const float DistanciaMinimaACasas = 30f;
    private const int ZonasMaximas = 40;

    private static readonly Color Vela = new Color(1f, 0.72f, 0.42f);
    private static readonly Color Fuego = new Color(1f, 0.55f, 0.22f);

    [MenuItem("El Sendero/Mundo/Noche: luces de casas, faroles y luciérnagas")]
    public static void Menu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Noche", "Sal de Play antes de ejecutarlo.", "Vale");
            return;
        }

        var materiales = PrepararMateriales();
        var resumen = new System.Text.StringBuilder();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            DayNightCycle ciclo = null;
            foreach (var raiz in escena.GetRootGameObjects())
                if ((ciclo = raiz.GetComponentInChildren<DayNightCycle>(true)) != null) break;
            if (ciclo == null) continue;

            bool ventanas = MontarVentanas(ciclo, materiales);
            bool fugaces = MontarEstrellasFugaces(ciclo);
            var (faroles, casas, posicionesDeCasas) = MontarLuces(escena);
            int zonas = MontarLuciernagas(escena, posicionesDeCasas);
            int nieblas = MontarNieblas(escena);
            int chimeneas = MontarHumo(escena);

            if (ventanas || fugaces || faroles + casas + zonas + nieblas + chimeneas > 0) EditorSceneManager.MarkSceneDirty(escena);
            resumen.AppendLine($"{escena.name}: ventanas {(ventanas ? "montadas" : "ya estaban")}, " +
                               $"estrellas fugaces {(fugaces ? "montadas" : "ya estaban")}, " +
                               $"{faroles} faroles/antorchas/hogueras, {casas} casas, {zonas} zonas de luciérnagas, " +
                               $"{nieblas} con niebla nueva, {chimeneas} chimeneas con humo.");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Noche",
            resumen.Length == 0
                ? "No hay ninguna escena abierta con DayNightCycle (abre MainWorld). Los materiales de las ventanas sí se han preparado."
                : resumen + "\nGuarda la escena con Ctrl+S.",
            "Vale");
    }

    private static List<Material> PrepararMateriales()
    {
        var lista = new List<Material>();
        foreach (var nombre in Atlas)
        {
            string rutaMaterial = $"{Pack}Materials/{nombre}.mat";
            string rutaMascara = $"{CarpetaDeMascaras}{nombre}_VentanasNoche.png";
            var material = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
            var mascara = CargarMascara(rutaMascara, $"{Pack}Textures/{nombre}_D.tga");
            if (material == null || mascara == null)
            {
                Debug.LogWarning($"[Noche] Falta {(material == null ? rutaMaterial : rutaMascara)}; esas ventanas no se encenderán.");
                continue;
            }

            if (material.GetTexture("_EmissionMap") != mascara || !material.IsKeywordEnabled("_EMISSION"))
            {
                Undo.RecordObject(material, "Noche: ventanas");
                material.SetTexture("_EmissionMap", mascara);
                material.SetColor("_EmissionColor", Color.black);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                EditorUtility.SetDirty(material);
            }
            lista.Add(material);
        }
        return lista;
    }

    /// La máscara se genera desde el atlas la primera vez; para rehacerla, borrar el PNG.
    private static Texture2D CargarMascara(string ruta, string rutaAtlas)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(ruta) == null &&
            !MascarasDeVentanas.Generar(AssetDatabase.LoadAssetAtPath<Texture2D>(rutaAtlas), ruta))
            return null;

        if (AssetImporter.GetAtPath(ruta) is TextureImporter importador &&
            (!importador.sRGBTexture || !importador.mipmapEnabled || importador.maxTextureSize != 1024))
        {
            importador.sRGBTexture = true;
            importador.mipmapEnabled = true;
            importador.maxTextureSize = 1024;
            importador.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    private static bool MontarVentanas(DayNightCycle ciclo, List<Material> materiales)
    {
        var ventanas = ciclo.GetComponent<VentanasIluminadas>();
        if (ventanas != null && ventanas.materiales != null && ventanas.materiales.Length == materiales.Count) return false;

        if (ventanas == null) ventanas = Undo.AddComponent<VentanasIluminadas>(ciclo.gameObject);
        else Undo.RecordObject(ventanas, "Noche: ventanas");
        ventanas.materiales = materiales.ToArray();
        EditorUtility.SetDirty(ventanas);
        return true;
    }

    private static bool MontarEstrellasFugaces(DayNightCycle ciclo)
    {
        if (ciclo.GetComponent<EstrellasFugaces>() != null) return false;
        Undo.AddComponent<EstrellasFugaces>(ciclo.gameObject);
        return true;
    }

    /// Niebla baja en cada zona de luciérnagas que aún no la tenga, con la misma caja a ras de suelo.
    private static int MontarNieblas(Scene escena)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDeNiebla);
        int n = 0;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var zona in raiz.GetComponentsInChildren<LuciernagasNocturnas>(true))
            {
                if (zona.GetComponent<NieblaNocturna>() != null) continue;
                var niebla = Undo.AddComponent<NieblaNocturna>(zona.gameObject);
                niebla.tamano = new Vector3(zona.tamano.x, 1f, zona.tamano.z);
                niebla.material = material;
                n++;
            }
        return n;
    }

    /// Humo en lo alto de cada chimenea del pack (mallas Chimney*) que aún no lo tenga.
    private static int MontarHumo(Scene escena)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDeHumo);
        int n = 0;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var malla in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (malla.sharedMesh == null || !malla.sharedMesh.name.StartsWith("Chimney")) continue;
                if (malla.GetComponentInChildren<HumoDeChimenea>(true) != null) continue;
                var render = malla.GetComponent<Renderer>();
                if (render == null) continue;

                var b = render.bounds;
                var go = new GameObject(NombreDelHumo);
                Undo.RegisterCreatedObjectUndo(go, "Noche: humo");
                go.transform.SetParent(malla.transform, false);
                go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
                go.transform.rotation = Quaternion.identity;
                var humo = go.AddComponent<HumoDeChimenea>();
                humo.material = material;
                humo.distanciaDeActivacion = 120f;
                n++;
            }
        return n;
    }

    private enum Tipo { Ninguno, Farol, Antorcha, Hoguera, Casa }

    private static (int faroles, int casas, List<Vector3> posicionesDeCasas) MontarLuces(Scene escena)
    {
        int faroles = 0, casas = 0;
        var posicionesDeCasas = new List<Vector3>();

        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;

                Tipo tipo = Clasificar(PrefabUtility.GetPrefabAssetPathOfNearestPrefabInstanceRoot(go));
                if (tipo == Tipo.Ninguno) continue;
                if (!Limites(go, out var b)) continue;
                if (tipo == Tipo.Casa) posicionesDeCasas.Add(b.center);
                if (go.GetComponentInChildren<Light>(true) != null) continue;   // ya tiene luz (puesta a mano o de antes)

                switch (tipo)
                {
                    case Tipo.Farol:
                        CrearLuz(t, new Vector3(b.center.x, b.max.y - b.size.y * 0.25f, b.center.z), Vela, 8f, 2.5f, 0.06f, "farol");
                        faroles++;
                        break;
                    case Tipo.Antorcha:
                        CrearLuz(t, new Vector3(b.center.x, b.max.y - b.size.y * 0.1f, b.center.z), Fuego, 6f, 2f, 0.22f, "antorcha");
                        faroles++;
                        break;
                    case Tipo.Hoguera:
                        CrearLuz(t, new Vector3(b.center.x, b.min.y + 0.6f, b.center.z), Fuego, 9f, 3f, 0.3f, "hoguera");
                        faroles++;
                        break;
                    case Tipo.Casa:
                        // Dentro de la casa y sin sombras: tiñe de cálido el suelo de alrededor, como luz que sale por las ventanas.
                        float alto = Mathf.Min(b.size.y * 0.3f, 2.5f);
                        float rango = Mathf.Max(b.size.x, b.size.z) * 0.9f + 3f;
                        CrearLuz(t, new Vector3(b.center.x, b.min.y + alto, b.center.z), Vela, rango, 1.6f, 0.04f, "casa");
                        casas++;
                        break;
                }
            }

        return (faroles, casas, posicionesDeCasas);
    }

    private static Tipo Clasificar(string ruta)
    {
        if (string.IsNullOrEmpty(ruta)) return Tipo.Ninguno;
        string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
        if (ruta.Contains("/Building Combination/")) return Tipo.Casa;
        if (ruta.Contains("/Props/Lighting/")) return nombre.StartsWith("Torch") ? Tipo.Antorcha : Tipo.Farol;
        if (nombre.StartsWith("Fire0")) return Tipo.Hoguera;
        return Tipo.Ninguno;
    }

    private static bool Limites(GameObject go, out Bounds b)
    {
        // Solo mallas: los límites de un sistema de partículas parado no dicen dónde está el fuego.
        b = new Bounds(go.transform.position, Vector3.zero);
        bool hay = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay || go.GetComponentInChildren<ParticleSystem>(true) != null;
    }

    private static void CrearLuz(Transform padre, Vector3 posicion, Color color, float rango, float intensidad, float parpadeo, string tipo)
    {
        var go = new GameObject($"{PrefijoDeLuz} ({tipo})");
        Undo.RegisterCreatedObjectUndo(go, "Noche: luces");
        go.transform.SetParent(padre, false);
        go.transform.position = posicion;

        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.range = rango;
        luz.intensity = 0f;
        luz.shadows = LightShadows.None;
        luz.enabled = false;

        var nocturna = go.AddComponent<LuzNocturna>();
        nocturna.intensidad = intensidad;
        nocturna.parpadeo = parpadeo;
    }

    private static int MontarLuciernagas(Scene escena, List<Vector3> casas)
    {
        Transform padre = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == RaizDeLuciernagas) return 0;
            var exterior = raiz.GetComponentInChildren<ExteriorWorldRoot>(true);
            if (exterior == null) continue;
            if (exterior.transform.Find(RaizDeLuciernagas) != null) return 0;
            if (padre == null) padre = exterior.transform;
        }

        // Árboles agrupados en celdas: las celdas más pobladas, lejos del pueblo, son el bosque.
        var celdas = new Dictionary<Vector2Int, (int n, Vector3 suma)>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                string ruta = PrefabUtility.GetPrefabAssetPathOfNearestPrefabInstanceRoot(t.gameObject);
                if (ruta == null || !ruta.Contains("/Vegetation/Tree")) continue;
                var p = t.position;
                var celda = new Vector2Int(Mathf.FloorToInt(p.x / CeldaDeBosque), Mathf.FloorToInt(p.z / CeldaDeBosque));
                celdas.TryGetValue(celda, out var c);
                celdas[celda] = (c.n + 1, c.suma + p);
            }

        var candidatas = new List<(int n, Vector3 centro)>();
        foreach (var c in celdas.Values)
        {
            if (c.n < ArbolesMinimosPorZona) continue;
            var centro = c.suma / c.n;
            bool cercaDeCasa = false;
            foreach (var casa in casas)
                if ((new Vector2(casa.x, casa.z) - new Vector2(centro.x, centro.z)).sqrMagnitude < DistanciaMinimaACasas * DistanciaMinimaACasas)
                { cercaDeCasa = true; break; }
            if (!cercaDeCasa) candidatas.Add((c.n, centro));
        }
        if (candidatas.Count == 0) return 0;
        candidatas.Sort((a, b) => b.n.CompareTo(a.n));

        var contenedor = new GameObject(RaizDeLuciernagas);
        Undo.RegisterCreatedObjectUndo(contenedor, "Noche: luciérnagas");
        if (padre != null) contenedor.transform.SetParent(padre, false);
        else SceneManager.MoveGameObjectToScene(contenedor, escena);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDeLuciernaga);
        int zonas = Mathf.Min(ZonasMaximas, candidatas.Count);
        for (int i = 0; i < zonas; i++)
        {
            var zona = new GameObject($"Luciérnagas {i + 1:00}");
            zona.transform.SetParent(contenedor.transform, false);
            zona.transform.position = candidatas[i].centro;
            var luciernagas = zona.AddComponent<LuciernagasNocturnas>();
            luciernagas.tamano = new Vector3(CeldaDeBosque, 3f, CeldaDeBosque);
            luciernagas.material = material;
        }
        return zonas;
    }
}
