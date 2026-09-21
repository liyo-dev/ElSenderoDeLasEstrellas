#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Genera la montaña por la que baja el Mago Oscuro, como MALLA propia.
///
/// ── Por qué hay que generarla ─────────────────────────────────────────────────────────────────
/// `Prologo_Valle` no tiene Terrain de Unity: el suelo es el Plane primitivo escalado x26, plano del
/// todo, y lo que hacía de relieve eran 25 prefabs `Loma_XX` que vistos desde arriba son discos
/// planos con un canto de tierra. Raúl (19 sep 2026): «no tenemos colina ni montaña para que baje el
/// mago oscuro; esos prefabs no nos valen».
///
/// ── Y por qué una malla y no un Terrain ───────────────────────────────────────────────────────
/// Un Terrain obligaría a rehacer el suelo del valle entero, a que el pueblo (que está colocado a
/// y = 100 sobre el plano) cuadrase al milímetro con la altura del terreno bajo cada casa, y a
/// rebakear el NavMesh. Todo eso para conseguir una cosa: una ladera por la que se pueda bajar
/// andando. Una malla suelta al oeste no toca nada de lo que ya funciona, y `WalkPathBeat` no
/// necesita NavMesh — se pega al suelo con un raycast, y esta malla lleva su MeshCollider.
///
/// ── Coherencia visual ─────────────────────────────────────────────────────────────────────────
/// El material NO se elige: se copia del de las `Loma_XX` que ya están en la escena. La montaña
/// tiene que estar hecha de lo mismo que el resto del relieve del valle, no de un material nuevo
/// traído de otro pack — que es exactamente el problema que hay que evitar en este proyecto.
///
/// La malla es de caras planas (cada triángulo con su propia normal) y de celdas grandes: es lo que
/// da el facetado low-poly del resto del juego en vez de una colina suave de aspecto realista.
public static class PrologoMontanaWiring
{
    private const string EscenaPrologo = "Prologo_Valle";
    private const string CarpetaMalla = "Assets/Art/World/Prologo_Valle";
    private const string RutaMalla = CarpetaMalla + "/Montana_Prologo.asset";
    private const string NombreObjeto = "Montana_Prologo";

    // ── Forma ─────────────────────────────────────────────────────────────────────────────────
    //
    // La cumbre va al oeste del pueblo, sobre el eje z = 6000, que es por donde baja. El radio este
    // (RadioX) está calculado para que la falda muera en x ≈ 5978, seis metros antes de la primera
    // marca del pueblo (M_Globo, en 5990.8): la montaña no puede comerse el decorado.

    private static readonly Vector3 Cumbre = new(5900f, 100f, 6000f);
    private const float RadioX = 78f;
    private const float RadioZ = 55f;
    private const float AlturaMaxima = 38f;

    /// Cuánto se hunde el borde por debajo del suelo del valle. Sin esto, la falda y el plano
    /// quedan a la misma altura y pelean por el mismo píxel (z-fighting) en todo el contorno. Con
    /// esto, el borde queda enterrado y la montaña parece salir del suelo.
    private const float Hundimiento = 0.4f;

    /// Lado de la celda, en metros. Grande a propósito: es lo que da el facetado.
    private const float LadoCelda = 3f;

    [MenuItem("El Sendero/Secuencias/Prólogo: crear la montaña del Mago Oscuro")]
    public static void Ejecutar()
    {
        Scene escena = SceneManager.GetSceneByName(EscenaPrologo);
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogError($"[PrologoMontana] La escena '{EscenaPrologo}' no está abierta.");
            return;
        }

        Mesh malla = ConstruirMalla();
        malla.name = "Montana_Prologo";

        if (!Directory.Exists(CarpetaMalla)) Directory.CreateDirectory(CarpetaMalla);

        var existenteAsset = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMalla);
        if (existenteAsset != null)
        {
            // Reutilizar el asset en vez de crear otro: si no, cada ejecución dejaría una malla
            // huérfana y las referencias de la escena apuntarían a la vieja.
            existenteAsset.Clear();
            existenteAsset.indexFormat = malla.indexFormat;
            existenteAsset.vertices = malla.vertices;
            existenteAsset.triangles = malla.triangles;
            existenteAsset.uv = malla.uv;
            existenteAsset.RecalculateNormals();
            existenteAsset.RecalculateBounds();
            Object.DestroyImmediate(malla);
            malla = existenteAsset;
            EditorUtility.SetDirty(malla);
        }
        else
        {
            AssetDatabase.CreateAsset(malla, RutaMalla);
        }

        var go = BuscarEnEscena(escena, NombreObjeto);
        if (go == null)
        {
            go = new GameObject(NombreObjeto);
            Undo.RegisterCreatedObjectUndo(go, "Crear la montaña del prólogo");
            SceneManager.MoveGameObjectToScene(go, escena);
        }

        go.transform.position = Vector3.zero;   // la malla ya está en coordenadas del mundo
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // OJO: aquí NO se puede usar '??'. GetComponent devuelve un "null falso" -- un objeto de
        // UnityEngine que el operador '==' de Unity trata como null pero que para C# existe --, así
        // que '??' se lo queda y nunca llama a AddComponent. El síntoma es exactamente el que dio
        // esto la primera vez: MissingComponentException al asignar sharedMesh sobre un MeshFilter
        // que "existe" pero no está en el GameObject. Con '== null' manda el operador de Unity.
        var filtro = go.GetComponent<MeshFilter>();
        if (filtro == null) filtro = go.AddComponent<MeshFilter>();
        filtro.sharedMesh = malla;

        var render = go.GetComponent<MeshRenderer>();
        if (render == null) render = go.AddComponent<MeshRenderer>();

        var material = MaterialDelSuelo(escena) ?? MaterialDeLasLomas(escena);
        if (material != null) render.sharedMaterial = material;

        var collider = go.GetComponent<MeshCollider>();
        if (collider == null) collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = malla;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log($"[PrologoMontana] Montaña creada: {malla.triangles.Length / 3} triángulos, " +
            $"cumbre a y = {(Cumbre.y + AlturaMaxima):F0} sobre un valle a y = {Cumbre.y:F0}. " +
            $"Material copiado de {(material != null ? material.name : "NINGUNO -- no he encontrado ninguna Loma en la escena")}. " +
            "Guarda la escena con Ctrl+S y vuelve a ejecutar 'preparar la escena del storyboard' " +
            "para que las marcas del descenso se apoyen en ella.");
    }

    // ── Geometría ─────────────────────────────────────────────────────────────────────────────

    /// Altura del terreno en un punto, medida desde el suelo del valle.
    ///
    /// Es una cúpula elíptica elevada a 1,3 para que tenga cresta en vez de parecer un flan, más un
    /// ruido determinista (suma de senos, no Random) para que no sea perfectamente simétrica. Que
    /// sea determinista importa: así el descenso se puede calcular sin abrir el Editor y volver a
    /// ejecutar esto da exactamente la misma montaña.
    public static float AlturaEn(float x, float z)
    {
        float dx = (x - Cumbre.x) / RadioX;
        float dz = (z - Cumbre.z) / RadioZ;

        float cupula = 1f - (dx * dx) - (dz * dz);
        if (cupula <= 0f) return -Hundimiento;

        float ruido = 1f
            + 0.10f * Mathf.Sin(x * 0.13f)
            + 0.08f * Mathf.Cos(z * 0.17f)
            + 0.05f * Mathf.Sin((x + z) * 0.07f);

        return AlturaMaxima * Mathf.Pow(cupula, 1.3f) * ruido - Hundimiento;
    }

    private static Mesh ConstruirMalla()
    {
        float x0 = Cumbre.x - RadioX - 6f, x1 = Cumbre.x + RadioX + 6f;
        float z0 = Cumbre.z - RadioZ - 6f, z1 = Cumbre.z + RadioZ + 6f;

        int nx = Mathf.CeilToInt((x1 - x0) / LadoCelda);
        int nz = Mathf.CeilToInt((z1 - z0) / LadoCelda);

        // Caras planas: cada triángulo lleva sus tres vértices propios. Es lo que hace que
        // RecalculateNormals dé una normal por cara y no una superficie suave.
        var vertices = new Vector3[nx * nz * 6];
        var uvs = new Vector2[vertices.Length];
        var tris = new int[vertices.Length];

        int v = 0;

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                float xa = x0 + ix * LadoCelda, xb = xa + LadoCelda;
                float za = z0 + iz * LadoCelda, zb = za + LadoCelda;

                Vector3 a = Punto(xa, za), b = Punto(xa, zb), c = Punto(xb, zb), d = Punto(xb, za);

                vertices[v] = a; vertices[v + 1] = b; vertices[v + 2] = c;
                vertices[v + 3] = a; vertices[v + 4] = c; vertices[v + 5] = d;

                for (int k = 0; k < 6; k++)
                {
                    uvs[v + k] = new Vector2(vertices[v + k].x * 0.05f, vertices[v + k].z * 0.05f);
                    tris[v + k] = v + k;
                }

                v += 6;
            }
        }

        var malla = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.vertices = vertices;
        malla.triangles = tris;
        malla.uv = uvs;
        malla.RecalculateNormals();
        malla.RecalculateBounds();

        return malla;
    }

    private static Vector3 Punto(float x, float z) => new(x, Cumbre.y + AlturaEn(x, z), z);

    /// El material del SUELO del valle, que es el que de verdad tiene que llevar la montaña.
    ///
    /// ── Por qué no el de las lomas (20 sep 2026) ──────────────────────────────────────────────
    /// Raúl: «el suelo se ve raro, fíjate que también tiene como dos colores». Y son dos, sí, pero
    /// de dos materiales distintos: `Suelo_Valle` lleva `Mat_Valle_Pradera` (un verde plano, SIN
    /// textura) y la montaña se llevaba el de las lomas, `Terrain06` del pack Fantasy Kingdom (un
    /// marrón CON textura). Dos superficies que se tocan en el horizonte y que no se parecen en
    /// nada: de ahí el corte de color.
    ///
    /// Copiando el del suelo, el valle y la montaña pasan a ser la misma tierra, que es lo que son.
    private static Material MaterialDelSuelo(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.gameObject.name != "Suelo_Valle") continue;
                if (r.sharedMaterial != null) return r.sharedMaterial;
            }
        }

        return null;
    }

    // ── Material ──────────────────────────────────────────────────────────────────────────────

    /// El material de las lomas que ya están en la escena. No se elige ninguno nuevo a propósito:
    /// ver la cabecera de esta clase.
    private static Material MaterialDeLasLomas(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.name.StartsWith("Loma")) continue;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                // Con varios materiales, el de roca/tierra manda: una montaña de este tamaño toda
                // verde se lee como una colina de golf, no como la montaña de la que baja el malo.
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    if (n.Contains("rock") || n.Contains("cliff") || n.Contains("stone") ||
                        n.Contains("dirt") || n.Contains("ground") || n.Contains("brown"))
                        return m;
                }

                foreach (var m in mats) if (m != null) return m;
            }
        }

        Debug.LogWarning("[PrologoMontana] No he encontrado ninguna 'Loma' en la escena de la que " +
            "copiar el material. La montaña se queda con el material por defecto (rosa/blanco): " +
            "hay que asignarle uno a mano, y lo suyo es el mismo que usen las lomas.");

        return null;
    }

    private static GameObject BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz;

            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        }

        return null;
    }
}
#endif
