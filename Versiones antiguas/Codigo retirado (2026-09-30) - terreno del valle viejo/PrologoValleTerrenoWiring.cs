#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Construye el relieve low-poly del valle del prólogo.
/// La malla y los materiales del terreno usan Quibli/Stylized Lit.
public static class PrologoValleTerrenoWiring
{
    private const string NombreEscena = "Prologo_Valle";
    private const string NombreEscenaIsla = "Prologo_Isla_Postgame";
    private const string Carpeta = "Assets/Art/World/Prologo_Valle/Terreno";
    private const string RutaMalla = Carpeta + "/Terreno_Cuenca_Quibli.asset";
    private const string RutaMallaIsla = Carpeta + "/Terreno_Isla_Postgame_Quibli.asset";
    private const string NombreRaiz = "TERRENO_Cuenca_Quibli";
    private static readonly Vector2 CentroValle = new(6000f, 6000f);
    private static Vector2 Centro = CentroValle;
    private const float SueloY = 100f;
    private const float RadioX = 130f;
    private const float RadioZ = 130f;
    private const float Paso = 5f;
    private const float RadioIslaX = 190f;
    private const float RadioIslaZ = 155f;
    private const float PasoIsla = 2.5f;

    private readonly struct Cumbre
    {
        public readonly float x, z, altura, radioX, radioZ;
        public Cumbre(float x, float z, float altura, float radioX, float radioZ)
        { this.x = x; this.z = z; this.altura = altura; this.radioX = radioX; this.radioZ = radioZ; }
    }

    // Cumbres repartidas en el perímetro; el oeste queda más bajo junto al descenso del Mago Oscuro.
    private static readonly Cumbre[] Cumbres =
    {
        new(-42f, 82f, 19f, 32f, 30f),
        new(4f, 114f, 31f, 34f, 29f),
        new(68f, 96f, 22f, 34f, 28f),
        new(110f, 32f, 26f, 35f, 42f),
        new(88f, -82f, 30f, 34f, 30f),
        new(15f, -114f, 23f, 40f, 30f),
        new(-70f, -92f, 26f, 34f, 30f),
        new(-112f, 47f, 20f, 30f, 34f),
    };

    [MenuItem("El Sendero/Archivo/Escenario/Prólogo: esculpir valle con Quibli", priority = 20)]
    public static void Ejecutar()
    {
        Scene escena = ObtenerEscenaAbierta();
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogError($"[PrologoValleTerreno] Abre '{NombreEscena}' antes de construir el relieve.");
            return;
        }
        if (escena.name != NombreEscena)
        {
            Debug.LogError($"[PrologoValleTerreno] Este generador solo actúa sobre '{NombreEscena}'. Para la isla postgame usa su herramienta exclusiva.");
            return;
        }

        var suelo = Buscar(escena, "Suelo_Valle");
        var plantilla = suelo != null ? suelo.GetComponent<Renderer>()?.sharedMaterial : null;
        if (plantilla == null || plantilla.shader == null || !plantilla.shader.name.Contains("Quibli"))
        {
            Debug.LogError("[PrologoValleTerreno] Suelo_Valle debe tener un material Quibli para continuar.");
            return;
        }

        bool esIsla = escena.name == NombreEscenaIsla;
        // La copia postgame puede vivir en otra zona del mundo. Centra su malla
        // respecto al suelo de esa escena, sin alterar el origen del prólogo original.
        Centro = esIsla
            ? new Vector2(suelo.transform.position.x, suelo.transform.position.z)
            : CentroValle;

        string carpetaTerreno = CarpetaMaterial(escena);
        if (!AssetDatabase.IsValidFolder(carpetaTerreno))
        {
            Directory.CreateDirectory(carpetaTerreno);
            AssetDatabase.Refresh();
        }

        var pradera = ObtenerMaterial("Pradera", plantilla, esIsla ? new Color(.26f, .40f, .22f) : new Color(.19f, .36f, .18f), carpetaTerreno);
        var praderaSombra = ObtenerMaterial("Pradera_Sombra", plantilla, esIsla ? new Color(.24f, .37f, .21f) : new Color(.14f, .27f, .15f), carpetaTerreno);
        var praderaSol = ObtenerMaterial("Pradera_Sol", plantilla, esIsla ? new Color(.29f, .43f, .24f) : new Color(.25f, .41f, .20f), carpetaTerreno);
        var roca = ObtenerMaterial("Roca", plantilla, new Color(.34f, .37f, .32f), carpetaTerreno);
        Material[] materiales;
        Mesh malla;
        string rutaMalla;
        if (esIsla)
        {
            var senda = ObtenerMaterial("Senda", plantilla, new Color(.32f, .25f, .16f), carpetaTerreno);
            string rutaAgua = carpetaTerreno + "/Mat_Valle_Agua_Quibli.mat";
            var agua = AssetDatabase.LoadAssetAtPath<Material>(rutaAgua);
            if (agua == null)
            {
                var aguaBase = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Agua_Quibli.mat");
                if (aguaBase == null)
                {
                    Debug.LogError("[PrologoValleTerreno] No encuentro el material de agua Quibli para la isla.");
                    return;
                }
                agua = new Material(aguaBase) { name = "Mat_Valle_Agua_Quibli" };
                AssetDatabase.CreateAsset(agua, rutaAgua);
            }

            materiales = new[] { pradera, praderaSombra, praderaSol, roca, senda, agua };
            rutaMalla = RutaMallaIsla;
            malla = ConstruirMallaIsla();
            OcultarCaminoPrefabricado(escena);
            OcultarAguaPrefabricada(escena);
        }
        else
        {
            materiales = new[] { pradera, praderaSombra, praderaSol, roca };
            rutaMalla = RutaMalla;
            malla = ConstruirMalla();
        }
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(rutaMalla);
        if (existente != null)
        {
            existente.Clear();
            existente.indexFormat = malla.indexFormat;
            existente.vertices = malla.vertices;
            existente.uv = malla.uv;
            existente.subMeshCount = malla.subMeshCount;
            for (int i = 0; i < malla.subMeshCount; i++) existente.SetTriangles(malla.GetTriangles(i), i);
            existente.RecalculateNormals();
            existente.RecalculateBounds();
            Object.DestroyImmediate(malla);
            malla = existente;
            EditorUtility.SetDirty(malla);
        }
        else AssetDatabase.CreateAsset(malla, rutaMalla);

        float toleranciaCentro = esIsla ? 10f : 1f;
        if (Mathf.Abs(malla.bounds.center.x - Centro.x) > toleranciaCentro ||
            Mathf.Abs(malla.bounds.center.z - Centro.y) > toleranciaCentro)
        {
            Debug.LogError($"[PrologoValleTerreno] La malla quedó centrada en " +
                $"({malla.bounds.center.x:F0}, {malla.bounds.center.z:F0}), no en la aldea " +
                $"({Centro.x:F0}, {Centro.y:F0}). " +
                "No oculto Suelo_Valle.");
            return;
        }

        GameObject raiz = Buscar(escena, NombreRaiz);
        if (raiz == null)
        {
            raiz = new GameObject(NombreRaiz);
            Undo.RegisterCreatedObjectUndo(raiz, "Crear relieve del valle");
            SceneManager.MoveGameObjectToScene(raiz, escena);
        }
        raiz.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        raiz.transform.localScale = Vector3.one;

        var filtro = raiz.GetComponent<MeshFilter>();
        if (filtro == null) filtro = Undo.AddComponent<MeshFilter>(raiz);
        filtro.sharedMesh = malla;
        var render = raiz.GetComponent<MeshRenderer>();
        if (render == null) render = Undo.AddComponent<MeshRenderer>(raiz);
        render.sharedMaterials = materiales;
        var collider = raiz.GetComponent<MeshCollider>();
        if (collider == null) collider = Undo.AddComponent<MeshCollider>(raiz);
        collider.sharedMesh = malla;

        // El terreno es SUELO (INC-399): capa Floor. En Default, NavMeshAutoSetup le ponía un
        // NavMeshObstacle con Carve del tamaño del valle entero y el NavMesh desaparecía salvo en
        // el camino — de ahí los NPCs en fila.
        int capaSuelo = LayerMask.NameToLayer("Floor");
        if (capaSuelo >= 0 && raiz.layer != capaSuelo)
        {
            Undo.RecordObject(raiz, "Terreno del valle a Floor");
            raiz.layer = capaSuelo;
        }
        var obstaculo = raiz.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstaculo != null) Undo.DestroyObjectImmediate(obstaculo);

        if (suelo != null)
        {
            var renderSuelo = suelo.GetComponent<Renderer>();
            if (renderSuelo != null)
            {
                Undo.RecordObject(renderSuelo, "Usar el nuevo terreno del valle");
                renderSuelo.enabled = false;
            }
            // Se conserva el collider original como apoyo plano para los sistemas que ya dependen
            // del suelo central; el nuevo collider solo añade las laderas exteriores.
        }

        AjustarVegetacionYRetirarLomas(escena);
        CrearRodalesDeArboles(escena);
        CrearHierbaQuibliEnRodales(escena);
        AplicarMaterialColina(escena, plantilla, carpetaTerreno);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("[PrologoValleTerreno] Cuenca creada: explanada central y corredor del puente llanos, " +
            $"8 cumbres facetadas, 36 árboles, hierba Quibli, 4 albedos Quibli y {malla.triangles.Length / 3} triángulos. " +
            "Revisa la escena en perspectiva y guarda con Ctrl+S.");
    }

    [MenuItem("El Sendero/Archivo/Escenario/Prólogo: armonizar color de la colina", priority = 21)]
    public static void ArmonizarColorColina()
    {
        Scene escena = ObtenerEscenaAbierta();
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogError($"[PrologoValleTerreno] Abre '{NombreEscena}' antes de cambiar el color.");
            return;
        }
        if (escena.name != NombreEscena)
        {
            Debug.LogError($"[PrologoValleTerreno] Este ajuste solo actúa sobre '{NombreEscena}'.");
            return;
        }

        var suelo = Buscar(escena, "Suelo_Valle");
        var plantilla = suelo != null ? suelo.GetComponent<Renderer>()?.sharedMaterial : null;
        if (plantilla == null || plantilla.shader == null || !plantilla.shader.name.Contains("Quibli"))
        {
            Debug.LogError("[PrologoValleTerreno] No encuentro el material Quibli de Suelo_Valle.");
            return;
        }

        string carpetaTerreno = CarpetaMaterial(escena);
        if (!AssetDatabase.IsValidFolder(carpetaTerreno))
        {
            Directory.CreateDirectory(carpetaTerreno);
            AssetDatabase.Refresh();
        }

        AplicarMaterialColina(escena, plantilla, carpetaTerreno);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
    }

    private static void AplicarMaterialColina(Scene escena, Material plantilla, string carpetaTerreno)
    {
        var colina = Buscar(escena, "Montana_Prologo");
        var render = colina != null ? colina.GetComponent<MeshRenderer>() : null;
        if (render == null)
        {
            Debug.LogWarning("[PrologoValleTerreno] No encuentro Montana_Prologo; se conserva el resto del terreno.");
            return;
        }

        string ruta = $"{carpetaTerreno}/Mat_Valle_Colina_Quibli.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(plantilla) { name = "Mat_Valle_Colina_Quibli" };
            AssetDatabase.CreateAsset(material, ruta);
        }

        material.shader = plantilla.shader;
        // Quibli tiene el albedo negro por defecto y colorea desde _BaseMap; un mapa null vuelve
        // negra la superficie. El PNG plano quita las bandas antiguas sin perder el color salvia.
        Color colorColina = new(.25f, .36f, .25f, 1f);
        var textura = ObtenerTexturaPlana(colorColina, carpetaTerreno);
        if (textura == null)
        {
            Debug.LogError("[PrologoValleTerreno] No pude crear el albedo plano; conservo el material actual de la colina.");
            return;
        }
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
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colorColina);
        else if (material.HasProperty("_Color")) material.SetColor("_Color", colorColina);
        EditorUtility.SetDirty(material);

        Undo.RecordObject(render, "Armonizar el color de la colina del prólogo");
        render.sharedMaterial = material;
        EditorUtility.SetDirty(render);
        Debug.Log("[PrologoValleTerreno] Montana_Prologo usa ahora un tono salvia Quibli, sin la textura naranja anterior.");
    }

    private static Texture2D ObtenerTexturaPlana(Color color, string carpetaTerreno)
    {
        Color gamma = color.gamma;
        var pixel = new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(gamma.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(gamma.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(gamma.b) * 255f), 255);
        string carpeta = carpetaTerreno + "/Planos";
        string ruta = $"{carpeta}/Plano_Albedo_{pixel.r:X2}{pixel.g:X2}{pixel.b:X2}.png";

        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (existente != null) return existente;

        if (!AssetDatabase.IsValidFolder(carpeta))
        {
            Directory.CreateDirectory(carpeta);
            AssetDatabase.Refresh();
        }

        var temporal = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
        var pixeles = new Color32[16];
        for (int i = 0; i < pixeles.Length; i++) pixeles[i] = pixel;
        temporal.SetPixels32(pixeles);
        temporal.Apply();
        File.WriteAllBytes(ruta, temporal.EncodeToPNG());
        Object.DestroyImmediate(temporal);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    private static Material ObtenerMaterial(string sufijo, Material plantilla, Color color, string carpetaTerreno)
    {
        string ruta = $"{carpetaTerreno}/Mat_Valle_{sufijo}_Quibli.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(plantilla) { name = $"Mat_Valle_{sufijo}_Quibli" };
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.shader = plantilla.shader;
        var textura = ObtenerTexturaPlana(color, carpetaTerreno);
        if (textura != null && material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", textura);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
        }
        if (textura != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", textura);
        if (material.HasProperty("_TextureImpact")) material.SetFloat("_TextureImpact", 1f);
        if (material.HasProperty("_DetailMapImpact")) material.SetFloat("_DetailMapImpact", 0f);
        if (material.HasProperty("_DetailMap")) material.SetTexture("_DetailMap", null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh ConstruirMalla()
    {
        int nx = Mathf.CeilToInt(RadioX * 2f / Paso);
        int nz = Mathf.CeilToInt(RadioZ * 2f / Paso);
        var vertices = new List<Vector3>(nx * nz * 6);
        var uvs = new List<Vector2>(nx * nz * 6);
        var indices = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float xa = -RadioX + ix * Paso, xb = xa + Paso;
            float za = -RadioZ + iz * Paso, zb = za + Paso;
            Vector3 a = Punto(xa, za), b = Punto(xa, zb), c = Punto(xb, zb), d = Punto(xb, za);
            // Alternar diagonales rompe el patrón de rejilla y conserva caras grandes, tipo low-poly.
            if (((ix + iz) & 1) == 0)
            {
                AnadirTriangulo(a, b, c, xa, za, indices, vertices, uvs);
                AnadirTriangulo(a, c, d, xa, za, indices, vertices, uvs);
            }
            else
            {
                AnadirTriangulo(a, b, d, xa, za, indices, vertices, uvs);
                AnadirTriangulo(b, c, d, xa, za, indices, vertices, uvs);
            }
        }

        var malla = new Mesh { name = "Terreno_Cuenca_Quibli", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.SetVertices(vertices);
        malla.SetUVs(0, uvs);
        malla.subMeshCount = indices.Length;
        for (int i = 0; i < indices.Length; i++) malla.SetTriangles(indices[i], i);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    private static Mesh ConstruirMallaIsla()
    {
        int nx = Mathf.CeilToInt(RadioIslaX * 2f / PasoIsla);
        int nz = Mathf.CeilToInt(RadioIslaZ * 2f / PasoIsla);
        var vertices = new List<Vector3>(nx * nz * 5);
        var uvs = new List<Vector2>(nx * nz * 5);
        var indices = new[]
        {
            new List<int>(), new List<int>(), new List<int>(),
            new List<int>(), new List<int>(), new List<int>()
        };

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float xa = -RadioIslaX + ix * PasoIsla, xb = xa + PasoIsla;
            float za = -RadioIslaZ + iz * PasoIsla, zb = za + PasoIsla;
            if (BordeIsla((xa + xb) * .5f, (za + zb) * .5f) > 1.025f) continue;

            Vector3 a = PuntoIsla(xa, za), b = PuntoIsla(xa, zb);
            Vector3 c = PuntoIsla(xb, zb), d = PuntoIsla(xb, za);
            if (((ix + iz) & 1) == 0)
            {
                AnadirTrianguloIsla(a, b, c, xa, za, indices, vertices, uvs);
                AnadirTrianguloIsla(a, c, d, xa, za, indices, vertices, uvs);
            }
            else
            {
                AnadirTrianguloIsla(a, b, d, xa, za, indices, vertices, uvs);
                AnadirTrianguloIsla(b, c, d, xa, za, indices, vertices, uvs);
            }
        }

        ConstruirAcantiladoIsla(indices, vertices, uvs);

        var malla = new Mesh { name = "Terreno_Isla_Postgame_Quibli", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.SetVertices(vertices);
        malla.SetUVs(0, uvs);
        malla.subMeshCount = indices.Length;
        for (int i = 0; i < indices.Length; i++) malla.SetTriangles(indices[i], i);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    private static void ConstruirAcantiladoIsla(List<int>[] indices, List<Vector3> vertices, List<Vector2> uvs)
    {
        const int segmentos = 192;
        const float bordeSuperior = 1.035f;
        float fondo = SueloY - 14f;
        for (int i = 0; i < segmentos; i++)
        {
            float a0 = i * Mathf.PI * 2f / segmentos;
            float a1 = (i + 1) * Mathf.PI * 2f / segmentos;
            Vector3 arribaA = PuntoCosta(a0, bordeSuperior);
            Vector3 arribaB = PuntoCosta(a1, bordeSuperior);
            Vector3 abajoA = new(arribaA.x, fondo, arribaA.z);
            Vector3 abajoB = new(arribaB.x, fondo, arribaB.z);
            AnadirTrianguloCosta(arribaA, abajoA, abajoB, indices[3], vertices, uvs);
            AnadirTrianguloCosta(arribaA, abajoB, arribaB, indices[3], vertices, uvs);
            AnadirTrianguloCosta(arribaA, abajoB, abajoA, indices[3], vertices, uvs);
            AnadirTrianguloCosta(arribaA, arribaB, abajoB, indices[3], vertices, uvs);
        }
    }

    private static Vector3 PuntoCosta(float angulo, float escala)
    {
        float irregularidad = 1f + .035f * Mathf.Sin(angulo * 5f + .7f) + .02f * Mathf.Sin(angulo * 9f - 1.1f);
        float x = Mathf.Cos(angulo) * RadioIslaX * escala * irregularidad;
        float z = Mathf.Sin(angulo) * RadioIslaZ * escala * irregularidad;
        return new Vector3(Centro.x + x, SueloY + AlturaIsla(x, z), Centro.y + z);
    }

    private static void AnadirTrianguloCosta(Vector3 a, Vector3 b, Vector3 c, List<int> indices,
        List<Vector3> vertices, List<Vector2> uvs)
    {
        int inicio = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uvs.Add(new Vector2(a.x * .035f, a.z * .035f));
        uvs.Add(new Vector2(b.x * .035f, b.z * .035f));
        uvs.Add(new Vector2(c.x * .035f, c.z * .035f));
        indices.Add(inicio); indices.Add(inicio + 1); indices.Add(inicio + 2);
    }

    private static void AnadirTrianguloIsla(Vector3 a, Vector3 b, Vector3 c, float celdaX, float celdaZ,
        List<int>[] indices, List<Vector3> vertices, List<Vector2> uvs)
    {
        float x = (a.x + b.x + c.x) / 3f - Centro.x;
        float z = (a.z + b.z + c.z) / 3f - Centro.y;
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        float distanciaRio = DistanciaAlRio(x, z);
        float distanciaSenda = Mathf.Abs(z - CentroCaminoZ(x));
        int submesh;

        if (distanciaRio < 5.2f && Mathf.Abs(z) < 145f)
        {
            submesh = 5;
            a.y = AlturaIsla(a.x - Centro.x, a.z - Centro.y, agua: true);
            b.y = AlturaIsla(b.x - Centro.x, b.z - Centro.y, agua: true);
            c.y = AlturaIsla(c.x - Centro.x, c.z - Centro.y, agua: true);
        }
        else if (distanciaSenda < AnchoSenda(x) && x > -150f && x < 150f)
        {
            submesh = 4;
            a.y = AlturaIsla(a.x - Centro.x, a.z - Centro.y);
            b.y = AlturaIsla(b.x - Centro.x, b.z - Centro.y);
            c.y = AlturaIsla(c.x - Centro.x, c.z - Centro.y);
        }
        else if (normal.y < .76f) submesh = 3;
        else
        {
            float mancha = Mathf.PerlinNoise((celdaX + 6000f) * .018f, (celdaZ + 6000f) * .018f);
            submesh = mancha < .39f ? 1 : mancha > .66f ? 2 : 0;
        }

        int inicio = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uvs.Add(new Vector2(a.x * .035f, a.z * .035f));
        uvs.Add(new Vector2(b.x * .035f, b.z * .035f));
        uvs.Add(new Vector2(c.x * .035f, c.z * .035f));
        indices[submesh].Add(inicio); indices[submesh].Add(inicio + 1); indices[submesh].Add(inicio + 2);
    }

    private static Vector3 PuntoIsla(float x, float z) =>
        new(Centro.x + x, SueloY + AlturaIsla(x, z), Centro.y + z);

    private static float BordeIsla(float x, float z)
    {
        float angulo = Mathf.Atan2(z / RadioIslaZ, x / RadioIslaX);
        float irregularidad = 1f + .035f * Mathf.Sin(angulo * 5f + .7f) + .02f * Mathf.Sin(angulo * 9f - 1.1f);
        return Mathf.Sqrt((x * x) / (RadioIslaX * RadioIslaX) + (z * z) / (RadioIslaZ * RadioIslaZ)) / irregularidad;
    }

    private static float CentroRioX(float z) => 22f + Mathf.Sin(z * .024f) * 8f + Mathf.Sin(z * .051f + .8f) * 3f;
    private static float DistanciaAlRio(float x, float z) => Mathf.Abs(x - CentroRioX(z));
    private static float CentroCaminoZ(float x) => -3f + Mathf.Sin((x + 18f) * .026f) * 8f + Mathf.Sin(x * .061f + .4f) * 2f;
    private static float AnchoSenda(float x) => 2.3f + .25f * Mathf.Sin(x * .09f) + .15f * Mathf.Sin(x * .21f + 1f);

    private static float AlturaIsla(float x, float z, bool agua = false)
    {
        float baseAltura = Altura(x, z);
        float borde = BordeIsla(x, z);
        float costa = Mathf.SmoothStep(.78f, 1.015f, borde);
        baseAltura *= 1f - costa;

        float distanciaRio = DistanciaAlRio(x, z);
        float lecho = 1f - Mathf.SmoothStep(4f, 10f, distanciaRio);
        float fondo = baseAltura - .85f * lecho;
        if (agua)
            return SueloY + baseAltura - .22f;

        float huellaSenda = 1f - Mathf.SmoothStep(AnchoSenda(x), AnchoSenda(x) + 1.6f,
            Mathf.Abs(z - CentroCaminoZ(x)));
        fondo -= .12f * huellaSenda;
        return SueloY + Mathf.Max(0f, fondo);
    }

    private static void OcultarCaminoPrefabricado(Scene escena)
    {
        var camino = Buscar(escena, "00_Camino");
        if (camino == null) return;
        foreach (var renderer in camino.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            Undo.RecordObject(renderer, "Sustituir camino prefabricado por senda integrada");
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void OcultarAguaPrefabricada(Scene escena)
    {
        var agua = Buscar(escena, "Rio_Agua");
        var renderer = agua != null ? agua.GetComponent<Renderer>() : null;
        if (renderer == null || !renderer.enabled) return;
        Undo.RecordObject(renderer, "Sustituir lámina original por cauce integrado");
        renderer.enabled = false;
        EditorUtility.SetDirty(renderer);
    }

    private static void AnadirTriangulo(Vector3 a, Vector3 b, Vector3 c, float celdaX, float celdaZ,
        List<int>[] indices, List<Vector3> vertices, List<Vector2> uvs)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        float altura = (a.y + b.y + c.y) / 3f - SueloY;
        int submesh;
        if (normal.y < .82f || (altura > 22f && normal.y < .93f)) submesh = 3;
        else
        {
            float mancha = Mathf.PerlinNoise((celdaX + Centro.x) * .034f, (celdaZ + Centro.y) * .034f);
            submesh = mancha < .40f ? 1 : mancha > .62f ? 2 : 0;
        }

        int inicio = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        uvs.Add(new Vector2(a.x * .035f, a.z * .035f));
        uvs.Add(new Vector2(b.x * .035f, b.z * .035f));
        uvs.Add(new Vector2(c.x * .035f, c.z * .035f));
        indices[submesh].Add(inicio); indices[submesh].Add(inicio + 1); indices[submesh].Add(inicio + 2);
    }

    private static Vector3 Punto(float x, float z) => new(Centro.x + x, SueloY + Altura(x, z), Centro.y + z);

    private static float Altura(float x, float z)
    {
        // La aldea, el río, el duelo y la ruta al puente comparten una cuenca ancha y transitable.
        float cuenca = Mathf.Sqrt((x * x) / (64f * 64f) + (z * z) / (46f * 46f));
        float salida = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.83f, 1.35f, cuenca));
        float ondulacion = (Mathf.Sin(x * .055f + z * .027f) + Mathf.Cos(z * .061f - x * .019f)) * 1.8f;
        float altura = salida * Mathf.Max(0f, 6f + ondulacion);

        // Sendero llano hacia la aparición al oeste y salida de aldeanos hacia el puente al este.
        float corredorOeste = 1f - Mathf.SmoothStep(7f, 16f, Mathf.Abs(z));
        if (x < -18f) altura *= 1f - corredorOeste * .94f;
        float corredorEste = 1f - Mathf.SmoothStep(12f, 24f, Mathf.Abs(z));
        if (x > 18f) altura *= 1f - corredorEste * .96f;

        float cumbres = 0f;
        foreach (var c in Cumbres)
        {
            float dx = (x - c.x) / c.radioX;
            float dz = (z - c.z) / c.radioZ;
            float r2 = dx * dx + dz * dz;
            if (r2 > 5f) continue;
            float pico = c.altura * Mathf.Exp(-r2 * 1.05f);
            if (pico > cumbres) cumbres = pico;
        }

        altura = Mathf.Max(altura, cumbres * salida);
        // Facetas naturales en las laderas, con amplitud que crece lejos de la aldea.
        altura += salida * (Mathf.Sin(x * .13f + z * .09f) * Mathf.Cos(z * .11f - x * .04f) * 1.8f);
        return Mathf.Max(0f, altura);
    }
    private static void AjustarVegetacionYRetirarLomas(Scene escena)
    {
        // Las lomas prefabricadas son discos bajos; el relieve nuevo ocupa su lugar.
        foreach (var raizEscena in escena.GetRootGameObjects())
        foreach (var t in raizEscena.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Loma_"))
            {
                Undo.RecordObject(t.gameObject, "Retirar lomas planas del prólogo");
                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
            }
        }

        // Conserva los rodales de árboles y los apoya sobre la nueva superficie.
        var vegetacion = Buscar(escena, "06_Vegetacion");
        if (vegetacion == null) return;
        foreach (Transform elemento in vegetacion.transform)
        {
            if (elemento.name == "Rodales_Quibli_Extra") continue;
            Vector3 posicion = elemento.position;
            float x = posicion.x - Centro.x;
            float z = posicion.z - Centro.y;
            float nuevaY = SueloY + Altura(x, z);
            if (Mathf.Abs(posicion.y - nuevaY) < .03f) continue;
            Undo.RecordObject(elemento, "Apoyar vegetación en el nuevo terreno");
            elemento.position = new Vector3(posicion.x, nuevaY, posicion.z);
            EditorUtility.SetDirty(elemento);
        }

        var rodales = vegetacion.transform.Find("Rodales_Quibli_Extra");
        if (rodales != null)
            foreach (Transform arbol in rodales)
            {
                if (arbol.name == "Hierba_Quibli_Rodales") continue;
                AsentarVegetacion(arbol);
            }
    }

    private static void CrearRodalesDeArboles(Scene escena)
    {
        var vegetacion = Buscar(escena, "06_Vegetacion");
        if (vegetacion == null)
        {
            Debug.LogWarning("[PrologoValleTerreno] No encuentro 06_Vegetacion; no añado árboles nuevos.");
            return;
        }

        const string rutaPrefabs = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Vegetation/";
        string[] prefabs = { "Tree02_a01.prefab", "Tree03_a01.prefab", "Tree06_a01.prefab", "Tree02_b01.prefab" };
        var ubicaciones = new Vector2[]
        {
            new(-54, 68), new(-42, 73), new(-63, 81), new(-31, 77), new(-56, 91), new(-25, 96),
            new(-78, -66), new(-61, -73), new(-88, -82), new(-43, -83), new(-72, -99), new(-35, -103),
            new(69, 62), new(82, 69), new(95, 57), new(77, 42), new(104, 37), new(89, 21),
            new(-100, 25), new(-91, 39), new(-109, 54), new(45, -79), new(57, -91), new(34, -96),
            new(-43, 48), new(-35, 57), new(-51, 57), new(-47, -45), new(-38, -58), new(-54, -59),
            new(43, 48), new(54, 44), new(39, 58), new(50, -47), new(40, -60), new(57, -57),
        };

        var grupo = vegetacion.transform.Find("Rodales_Quibli_Extra");
        if (grupo == null)
        {
            var go = new GameObject("Rodales_Quibli_Extra");
            Undo.RegisterCreatedObjectUndo(go, "Añadir rodales de árboles al valle");
            go.transform.SetParent(vegetacion.transform, false);
            grupo = go.transform;
        }

        for (int i = 0; i < ubicaciones.Length; i++)
        {
            string nombre = $"Arbol_Rodal_{i + 1:00}";
            Transform existente = grupo.Find(nombre);
            GameObject arbol = existente != null ? existente.gameObject : null;
            if (arbol == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefabs + prefabs[i % prefabs.Length]);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PrologoValleTerreno] No encuentro el prefab de árbol '{prefabs[i % prefabs.Length]}'.");
                    continue;
                }
                arbol = PrefabUtility.InstantiatePrefab(prefab, grupo) as GameObject;
                if (arbol == null) continue;
                Undo.RegisterCreatedObjectUndo(arbol, "Añadir árbol al rodal del valle");
                arbol.name = nombre;
            }

            float x = ubicaciones[i].x;
            float z = ubicaciones[i].y;
            float escala = .85f + (i % 4) * .1f;
            arbol.transform.SetPositionAndRotation(
                new Vector3(Centro.x + x, SueloY + Altura(x, z), Centro.y + z),
                Quaternion.Euler(0f, (i * 67) % 360, 0f));
            arbol.transform.localScale = Vector3.one * escala;
            EditorUtility.SetDirty(arbol.transform);
        }
    }

    private static void CrearHierbaQuibliEnRodales(Scene escena)
    {
        const string rutaHierbaCorta = "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Short.prefab";
        const string rutaHierbaLarga = "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Long.prefab";
        const string nombreGrupo = "Hierba_Quibli_Rodales";
        const int parchesPorArbol = 8;

        var hierbaCorta = AssetDatabase.LoadAssetAtPath<GameObject>(rutaHierbaCorta);
        var hierbaLarga = AssetDatabase.LoadAssetAtPath<GameObject>(rutaHierbaLarga);
        var arboles = Buscar(escena, "Rodales_Quibli_Extra");
        if (hierbaCorta == null || hierbaLarga == null || arboles == null)
        {
            Debug.LogWarning("[PrologoValleTerreno] No encuentro los prefabs de hierba Quibli o el grupo de árboles; se conserva el relieve.");
            return;
        }

        Transform grupo = arboles.transform.Find(nombreGrupo);
        if (grupo == null)
        {
            var raizHierba = new GameObject(nombreGrupo);
            Undo.RegisterCreatedObjectUndo(raizHierba, "Añadir hierba Quibli a los rodales del valle");
            raizHierba.transform.SetParent(arboles.transform, false);
            grupo = raizHierba.transform;
        }
        grupo.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        grupo.localScale = Vector3.one;

        int total = 0;
        int cantidadArboles = arboles.transform.childCount;
        for (int i = 0; i < cantidadArboles; i++)
        {
            Transform arbol = arboles.transform.GetChild(i);
            if (arbol == grupo || arbol.GetComponentInChildren<MeshRenderer>() == null) continue;

            int semilla = Mathf.RoundToInt(arbol.position.x * 17f + arbol.position.z * 31f);
            for (int p = 0; p < parchesPorArbol; p++)
            {
                string nombre = $"Hierba_{semilla}_{p + 1:00}";
                Transform existente = grupo.Find(nombre);
                GameObject parche = existente != null ? existente.gameObject : null;
                if (parche == null)
                {
                    GameObject prefab = (p % 3 == 0) ? hierbaLarga : hierbaCorta;
                    parche = PrefabUtility.InstantiatePrefab(prefab, grupo) as GameObject;
                    if (parche == null) continue;
                    Undo.RegisterCreatedObjectUndo(parche, "Añadir hierba Quibli a los rodales del valle");
                    parche.name = nombre;
                }

                float angulo = p * 45f + VariacionDeterminista(semilla, p, -18f, 18f);
                float radio = VariacionDeterminista(semilla, p + 31, 2.3f, 5.1f);
                float radians = angulo * Mathf.Deg2Rad;
                float x = arbol.position.x - Centro.x + Mathf.Cos(radians) * radio;
                float z = arbol.position.z - Centro.y + Mathf.Sin(radians) * radio;
                parche.transform.SetPositionAndRotation(
                    new Vector3(Centro.x + x, SueloY + Altura(x, z), Centro.y + z),
                    Quaternion.Euler(0f, VariacionDeterminista(semilla, p + 67, 0f, 360f), 0f));
                parche.transform.localScale = Vector3.one * VariacionDeterminista(semilla, p + 101, .72f, 1.05f);

                // Las prefabs de muestra incluyen un collider para pruebas; la hierba del valle
                // es decorativa y no debe bloquear al jugador ni alterar el NavMesh.
                foreach (var collider in parche.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                EditorUtility.SetDirty(parche.transform);
                total++;
            }
        }

        EditorUtility.SetDirty(grupo);
        Debug.Log($"[PrologoValleTerreno] {total} parches de hierba Quibli colocados alrededor de {cantidadArboles - 1} árboles.");
    }

    private static float VariacionDeterminista(int semilla, int canal, float minimo, float maximo)
    {
        var rng = new System.Random(semilla ^ (canal * 7919));
        return minimo + (float)rng.NextDouble() * (maximo - minimo);
    }

    private static void AsentarVegetacion(Transform elemento)
    {
        Vector3 posicion = elemento.position;
        float x = posicion.x - Centro.x;
        float z = posicion.z - Centro.y;
        float nuevaY = SueloY + Altura(x, z);
        if (Mathf.Abs(posicion.y - nuevaY) < .03f) return;
        Undo.RecordObject(elemento, "Apoyar vegetación en el nuevo terreno");
        elemento.position = new Vector3(posicion.x, nuevaY, posicion.z);
        EditorUtility.SetDirty(elemento);
    }

    private static GameObject Buscar(Scene escena, string nombre)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        }
        return null;
    }

    private static Scene ObtenerEscenaAbierta()
    {
        Scene activa = SceneManager.GetActiveScene();
        if (activa.IsValid() && activa.isLoaded &&
            (activa.name == NombreEscena || activa.name == NombreEscenaIsla))
            return activa;

        return SceneManager.GetSceneByName(NombreEscena);
    }

    private static string CarpetaMaterial(Scene escena) =>
        escena.name == NombreEscenaIsla ? Carpeta + "/Postgame" : Carpeta;
}
#endif
