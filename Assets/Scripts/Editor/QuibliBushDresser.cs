using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Añade algunos arbustos junto a los árboles de MainWorld, reutilizando las mallas de arbusto ya
/// exportadas por el Foliage Generator de Quibli (los mismos "BillboardBush-*" que aparecen
/// colocados a mano en la escena de demo `Assets/Plugins/Quibli/Demos/Nature/[Demo] Nature.unity`)
/// junto con el material base que ya usan ahí ("NatureScene_BillboardBush_01 1.mat" — el mismo que
/// Raúl partió como base para los rosales/árboles rojizos de la secuencia final, ver TDD §21.3,
/// así que el original sin tocar es el arbusto verde "normal" del pack, no una variante narrativa).
///
/// A diferencia de la hierba (pensada para rellenar el campo), aquí se busca un toque disperso —
/// no todos los árboles llevan arbusto, y como mucho dos por árbol — para no competir visualmente
/// con el anillo de hierba ya puesto por QuibliGrassDresser. Reutiliza la misma idea de radio de
/// exclusión por NavMeshObstacle que ese script para no invadir el tronco.
///
/// Uso: con MainWorld.unity abierta, El Sendero → Mundo → Añadir Arbustos junto a Árboles (Quibli).
/// Idempotente: borra y regenera su propio grupo raíz "Quibli - Arbustos junto a árboles" en cada
/// ejecución. Probabilidad, escala y radio están como constantes al principio, fáciles de ajustar.
/// </summary>
public static class QuibliBushDresser
{
    private const string TreesGroupName = "Trees";
    private const string BushRootName = "Quibli - Arbustos junto a árboles";
    private const string BushMaterialPath = "Assets/Plugins/Quibli/Demos/Nature/Materials/NatureScene_BillboardBush_01 1.mat";

    private static readonly string[] BushMeshPaths =
    {
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-Sphere-Take1.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-Sphere-Take2.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-Sphere-Take3.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-MeshCarrier_03_Dome-Take1.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-MeshCarrier_05_MBall-Take1.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-MeshCarrier_05_MBall-Take2.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-MeshCarrier_05_MBall-Take3.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-MeshCarrier_05_MBall-Take4.asset",
        "Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/BillboardBush-MeshCarrier_09_MBallBubbles-Take1.asset",
    };

    private const float RadioExclusionPorDefecto = 2.8f; // cuando el árbol no tiene NavMeshObstacle propio
    private const float MargenExtra = 1.0f;               // colchón extra fuera del radio de exclusión (más que la hierba: el arbusto es más grande)
    private const float AnchoAnillo = 2.5f;

    private const float ProbabilidadPrimerArbusto = 0.5f;   // subido de 0.35 a peticion de Raul: "lo mismo pondria alguno mas"
    private const float ProbabilidadSegundoArbusto = 0.18f; // subido de 0.12 junto con el anterior
    private const float EscalaMin = 0.8f;
    private const float EscalaMax = 1.3f;

    [MenuItem("El Sendero/Archivo/Mundo/Añadir Arbustos junto a Árboles (Quibli)")]
    public static void AnadirArbustos()
    {
        GameObject grupoArboles = BuscarEnEscenaActiva(TreesGroupName);
        if (grupoArboles == null)
        {
            Debug.LogError($"[QuibliBushDresser] No se encontró un GameObject llamado '{TreesGroupName}' en la escena activa. Abre MainWorld.unity antes de ejecutar esto.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(BushMaterialPath);
        if (material == null)
        {
            Debug.LogError($"[QuibliBushDresser] No se encontró el material '{BushMaterialPath}'. Revisa que Quibli siga en el proyecto.");
            return;
        }

        Mesh[] mallas = new Mesh[BushMeshPaths.Length];
        int mallasCargadas = 0;
        for (int i = 0; i < BushMeshPaths.Length; i++)
        {
            mallas[i] = AssetDatabase.LoadAssetAtPath<Mesh>(BushMeshPaths[i]);
            if (mallas[i] != null) mallasCargadas++;
            else Debug.LogWarning($"[QuibliBushDresser] No se encontró la malla '{BushMeshPaths[i]}', se omite esa variante.");
        }
        if (mallasCargadas == 0)
        {
            Debug.LogError("[QuibliBushDresser] No se pudo cargar ninguna malla de arbusto. Revisa que 'Assets/Plugins/Quibli/Demos/[Common]/Models/Foliage Generator Exported Meshes/' siga existiendo.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        Terrain terreno = Terrain.activeTerrain;

        GameObject raizAnterior = BuscarEnEscenaActiva(BushRootName);
        if (raizAnterior != null)
        {
            Undo.DestroyObjectImmediate(raizAnterior);
        }

        GameObject raiz = new GameObject(BushRootName);
        Undo.RegisterCreatedObjectUndo(raiz, "Añadir Arbustos junto a Árboles (Quibli)");

        int arbustosCreados = 0;
        int totalHijos = grupoArboles.transform.childCount;
        for (int i = 0; i < totalHijos; i++)
        {
            Transform arbol = grupoArboles.transform.GetChild(i);
            if (arbol.GetComponentInChildren<MeshRenderer>() == null) continue;

            Vector3 posArbol = arbol.position;
            if (RangoAleatorio(posArbol, 900, 0f, 1f) > ProbabilidadPrimerArbusto) continue;

            float radioExclusion = RadioExclusionPorDefecto;
            NavMeshObstacle obstaculo = arbol.GetComponentInChildren<NavMeshObstacle>();
            if (obstaculo != null)
            {
                radioExclusion = obstaculo.shape == NavMeshObstacleShape.Box
                    ? Mathf.Max(obstaculo.size.x, obstaculo.size.z) * 0.5f
                    : obstaculo.radius;
            }

            float radioInterior = radioExclusion + MargenExtra;
            float radioExterior = radioInterior + AnchoAnillo;
            float anguloBase = RangoAleatorio(posArbol, 901, 0f, 360f);

            int cuantos = RangoAleatorio(posArbol, 902, 0f, 1f) < ProbabilidadSegundoArbusto ? 2 : 1;
            for (int b = 0; b < cuantos; b++)
            {
                float angulo = anguloBase + b * RangoAleatorio(posArbol, 910 + b, 140f, 220f);
                float radio = radioInterior + RangoAleatorio(posArbol, 920 + b, 0f, radioExterior - radioInterior);
                Vector3 offset = new Vector3(Mathf.Cos(angulo * Mathf.Deg2Rad), 0f, Mathf.Sin(angulo * Mathf.Deg2Rad)) * radio;
                Vector3 posMundo = posArbol + offset;
                posMundo.y = terreno != null ? terreno.SampleHeight(posMundo) + terreno.transform.position.y : posArbol.y;

                Mesh mallaElegida = null;
                int intentos = 0;
                while (mallaElegida == null && intentos < BushMeshPaths.Length)
                {
                    int indice = Mathf.FloorToInt(RangoAleatorio(posArbol, 930 + b + intentos, 0f, BushMeshPaths.Length));
                    indice = Mathf.Clamp(indice, 0, BushMeshPaths.Length - 1);
                    mallaElegida = mallas[indice];
                    intentos++;
                }
                if (mallaElegida == null) continue;

                GameObject arbusto = new GameObject(mallaElegida.name);
                Undo.RegisterCreatedObjectUndo(arbusto, "Añadir Arbustos junto a Árboles (Quibli)");
                arbusto.transform.SetParent(raiz.transform);
                arbusto.transform.position = posMundo;
                arbusto.transform.rotation = Quaternion.Euler(0f, RangoAleatorio(posArbol, 940 + b, 0f, 360f), 0f);
                float escala = RangoAleatorio(posArbol, 950 + b, EscalaMin, EscalaMax);
                arbusto.transform.localScale = Vector3.one * escala;

                var filtro = arbusto.AddComponent<MeshFilter>();
                filtro.sharedMesh = mallaElegida;
                var renderizador = arbusto.AddComponent<MeshRenderer>();
                renderizador.sharedMaterial = material;

                arbustosCreados++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[QuibliBushDresser] Listo en '{scene.name}': {arbustosCreados} arbustos añadidos, agrupados bajo '{BushRootName}'. Recuerda guardar la escena.");
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

    /// <summary>
    /// Valor pseudoaleatorio determinista en [min, max), a partir de una posición y un "canal"
    /// (para no repetir la misma secuencia en cada llamada sobre el mismo árbol). Así, ejecutar el
    /// menú varias veces siempre coloca los mismos arbustos — no hay barajado nuevo en cada pasada.
    /// </summary>
    private static float RangoAleatorio(Vector3 posicion, int canal, float min, float max)
    {
        int semilla = Mathf.RoundToInt(posicion.x * 53f) ^ Mathf.RoundToInt(posicion.z * 97f) ^ (canal * 7919);
        var rng = new System.Random(semilla);
        return min + (float)rng.NextDouble() * (max - min);
    }
}
