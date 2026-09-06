using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Añade unos pocos "rayos de sol" (shader Quibli/Light Beam) junto a algunos árboles de MainWorld —
/// el mismo efecto que la demo de naturaleza de Quibli usa como acento puntual (unas 5 veces en toda
/// esa escena), no como relleno del campo.
///
/// A petición explícita de Raúl (4 sep 2026): "si conseguimos que se vean lo deben hacer solo si es
/// de dia y no esta nublado o llueve". Esta herramienta añade un SunRayWeatherGate.cs al grupo raíz
/// que gestiona justo eso en tiempo real, enganchado a los eventos públicos de DayNightCycle.
/// Además, como el ángulo del sol CAMBIA entre Amanecer/Día/Atardecer (DayNightCycle.timeSettings),
/// cada rayo individual lleva un SunRayBeam.cs que lo reorienta en tiempo real para seguir la
/// dirección real de la luz — un rayo con rotación fija solo se vería bien alineado en uno de esos
/// tres periodos y torcido en los demás.
///
/// Geometría: no se usa la malla concreta de la demo de Quibli (viene embebida directamente dentro
/// de su escena, no es un asset reutilizable aparte) sino un Quad simple — el propio shader
/// Quibli/Light Beam ya hace el difuminado de borde por UV (_UvFadeX/_UvFadeY) y por profundidad
/// (_Depth/_CameraDistanceFade*), así que la silueta real del "rayo" la pone el shader, no la malla.
///
/// IMPORTANTE sobre los ejes: en el shader, fade_uv = smoothstep(...)^_UvFade, así que un exponente
/// bajo (_UvFadeX de los materiales de la demo, ~0.4) da un difuminado ancho/suave y uno alto
/// (_UvFadeY, ~4) da una franja muy estrecha. Los materiales de la demo están pensados para su
/// propia malla curva, donde el eje U recorre el LARGO del rayo (difuminado suave en las puntas) y
/// el eje V su ANCHO (franja estrecha). En el Quad de Unity, el eje U del UV corresponde al eje
/// local X y el V al eje local Y — por eso aquí el LARGO del plano va en X y el ANCHO en Y (al
/// revés se ve casi invisible: la franja estrecha cae sobre el largo entero y solo queda visible un
/// hilo diminuto). SunRayBeam.cs alinea el eje local X del plano contra el sol, no el Y.
///
/// Como un Quad es de una sola cara, cada rayo se construye con DOS planos cruzados a 90° (mismo
/// truco de "billboard cruzado" que ya usan los BillboardBush-*.asset de arbustos) para que se vea
/// razonablemente bien se mire desde donde se mire, en vez de desaparecer al verlo de canto.
///
/// Uso: con MainWorld.unity abierta, El Sendero → Mundo → Añadir Rayos de Sol junto a Árboles (Quibli).
/// Idempotente: borra y regenera su propio grupo raíz "Quibli - Rayos de sol" en cada ejecución.
///
/// AVISO para Raúl: de los tres efectos de vestido de esta sesión (hierba, arbustos, rayos), este es
/// con diferencia el más especulativo — no hay forma de previsualizarlo desde aquí, porque depende
/// tanto del ángulo de cámara como del ángulo del sol en cada momento del ciclo día/noche. Es el que
/// más probablemente necesite ajustar escala/cantidad/altura tras verlo en el Editor.
/// </summary>
public static class QuibliSunRayDresser
{
    private const string TreesGroupName = "Trees";
    private const string RayosRootName = "Quibli - Rayos de sol";

    private static readonly string[] MaterialPaths =
    {
        "Assets/Plugins/Quibli/Demos/Nature/Materials/NatureScene_LightBeam 1.mat",
        "Assets/Plugins/Quibli/Demos/Nature/Materials/NatureScene_LightBeam 2.mat",
    };

    private const float ProbabilidadRayo = 0.09f; // ~9% de los árboles -> unos 10-12 rayos en ~131 árboles
    private const int LimiteTotalRayos = 18;      // tope de seguridad, mismo espíritu que LimiteInstanciasRelleno de la hierba

    // Altura aproximada del punto del que "cuelga" cada rayo: media copa de un árbol (los árboles
    // miden ~7 unidades de alto según su NavMeshObstacle, ver QuibliGrassDresser/QuibliBushDresser).
    private const float AlturaAncla = 4.2f;
    private const float JitterHorizontal = 1.4f; // pequeño desplazamiento respecto al tronco, para no salir siempre del centro exacto
    private const float AnchoMin = 1.6f, AnchoMax = 2.6f;
    private const float LargoMin = 11f, LargoMax = 17f;

    [MenuItem("El Sendero/Mundo/Añadir Rayos de Sol junto a Árboles (Quibli)")]
    public static void AnadirRayosDeSol()
    {
        GameObject grupoArboles = BuscarEnEscenaActiva(TreesGroupName);
        if (grupoArboles == null)
        {
            Debug.LogError($"[QuibliSunRayDresser] No se encontró un GameObject llamado '{TreesGroupName}' en la escena activa. Abre MainWorld.unity antes de ejecutar esto.");
            return;
        }

        Material[] materiales = new Material[MaterialPaths.Length];
        int materialesCargados = 0;
        for (int i = 0; i < MaterialPaths.Length; i++)
        {
            materiales[i] = AssetDatabase.LoadAssetAtPath<Material>(MaterialPaths[i]);
            if (materiales[i] != null) materialesCargados++;
            else Debug.LogWarning($"[QuibliSunRayDresser] No se encontró el material '{MaterialPaths[i]}', se omite esa variante.");
        }
        if (materialesCargados == 0)
        {
            Debug.LogError("[QuibliSunRayDresser] No se pudo cargar ningún material de rayo de sol. Revisa que Quibli siga en el proyecto.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        Terrain terreno = Terrain.activeTerrain;

        GameObject raizAnterior = BuscarEnEscenaActiva(RayosRootName);
        if (raizAnterior != null)
        {
            Undo.DestroyObjectImmediate(raizAnterior);
        }

        GameObject raiz = new GameObject(RayosRootName);
        Undo.RegisterCreatedObjectUndo(raiz, "Añadir Rayos de Sol junto a Árboles (Quibli)");
        Undo.AddComponent<SunRayWeatherGate>(raiz);

        int rayosCreados = 0;
        int totalHijos = grupoArboles.transform.childCount;
        for (int i = 0; i < totalHijos && rayosCreados < LimiteTotalRayos; i++)
        {
            Transform arbol = grupoArboles.transform.GetChild(i);
            if (arbol.GetComponentInChildren<MeshRenderer>() == null) continue;

            Vector3 posArbol = arbol.position;
            if (RangoAleatorio(posArbol, 960, 0f, 1f) > ProbabilidadRayo) continue;

            float jitterX = RangoAleatorio(posArbol, 961, -JitterHorizontal, JitterHorizontal);
            float jitterZ = RangoAleatorio(posArbol, 962, -JitterHorizontal, JitterHorizontal);
            Vector3 posAncla = posArbol + new Vector3(jitterX, 0f, jitterZ);
            float sueloY = terreno != null ? terreno.SampleHeight(posAncla) + terreno.transform.position.y : posArbol.y;
            posAncla.y = sueloY + AlturaAncla;

            float ancho = RangoAleatorio(posArbol, 963, AnchoMin, AnchoMax);
            float largo = RangoAleatorio(posArbol, 964, LargoMin, LargoMax);
            float giroBase = RangoAleatorio(posArbol, 965, 0f, 360f);
            int indiceMaterial = Mathf.Clamp(Mathf.FloorToInt(RangoAleatorio(posArbol, 966, 0f, materiales.Length)), 0, materiales.Length - 1);
            Material material = materiales[indiceMaterial] != null ? materiales[indiceMaterial] : materiales[0];

            GameObject grupoRayo = new GameObject("Rayo de sol");
            Undo.RegisterCreatedObjectUndo(grupoRayo, "Añadir Rayos de Sol junto a Árboles (Quibli)");
            grupoRayo.transform.SetParent(raiz.transform);
            grupoRayo.transform.position = posAncla;

            CrearPlano(grupoRayo.transform, material, ancho, largo, giroBase);
            CrearPlano(grupoRayo.transform, material, ancho, largo, giroBase + 90f);

            rayosCreados++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[QuibliSunRayDresser] Listo en '{scene.name}': {rayosCreados} rayos de sol añadidos (2 planos cruzados cada uno), agrupados bajo '{RayosRootName}'. Solo se verán de día y con cielo despejado (SunRayWeatherGate.cs). Recuerda guardar la escena.");
    }

    private static void CrearPlano(Transform padre, Material material, float ancho, float largo, float giro)
    {
        GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Undo.RegisterCreatedObjectUndo(plano, "Añadir Rayos de Sol junto a Árboles (Quibli)");
        plano.name = "Plano";

        Collider colisionador = plano.GetComponent<Collider>();
        if (colisionador != null) Undo.DestroyObjectImmediate(colisionador);

        plano.transform.SetParent(padre);
        plano.transform.localPosition = Vector3.zero;
        // X = largo (eje U del shader, difuminado suave), Y = ancho (eje V, franja estrecha) — ver nota de la clase.
        plano.transform.localScale = new Vector3(largo, ancho, 1f);

        var renderizador = plano.GetComponent<MeshRenderer>();
        renderizador.sharedMaterial = material;
        renderizador.shadowCastingMode = ShadowCastingMode.Off;

        var seguidor = Undo.AddComponent<SunRayBeam>(plano);
        seguidor.giroPropio = giro;
    }

    private static GameObject BuscarEnEscenaActiva(string nombre)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            GameObject encontrado = BuscarEnHijos(raiz.transform, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static GameObject BuscarEnHijos(Transform actual, string nombre)
    {
        if (actual.name == nombre) return actual.gameObject;
        for (int i = 0; i < actual.childCount; i++)
        {
            GameObject encontrado = BuscarEnHijos(actual.GetChild(i), nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static float RangoAleatorio(Vector3 posicion, int canal, float min, float max)
    {
        int semilla = Mathf.RoundToInt(posicion.x * 53f) ^ Mathf.RoundToInt(posicion.z * 97f) ^ (canal * 7919);
        var rng = new System.Random(semilla);
        return min + (float)rng.NextDouble() * (max - min);
    }
}
