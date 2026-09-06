using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Vestido de hierba junto a los árboles de MainWorld (look de las demos de Quibli, TDD.md §21).
/// Historial: el primer intento (16 ago 2026, TDD §21.6) editó el .unity a ciegas como texto y
/// puso la hierba con un jitter de ±2.5 unidades alrededor de cada árbol, sin comprobar el hueco
/// real que ocupa cada tronco/copa — la hierba acababa metida dentro de los árboles, motivo por
/// el que Raúl la quitó (TDD §21.9). El segundo intento (4 sep 2026, TDD §21.10) arregló eso
/// leyendo el NavMeshObstacle real de cada árbol como zona de exclusión, pero solo ponía 2
/// parches por árbol — Raúl lo probó en el Editor y pidió mucha más densidad, que se vea el
/// campo relleno de hierba en vez de matas sueltas solo pegadas al tronco.
///
/// Esta versión (misma sesión) añade una segunda pasada de "relleno" además del anillo junto a
/// cada árbol: una rejilla con jitter sobre toda el área donde hay árboles (que ya sabemos que es
/// pradera/bosque, no plaza del castillo ni montaña ni agua, porque ahí no se plantó ningún
/// árbol), evitando los troncos y limitada a una distancia razonable del árbol más cercano.
/// Deliberadamente NO usa el sistema de "Terrain Detail" de Unity (la forma más eficiente de
/// pintar hierba realmente densa) para no introducir en la misma sesión una herramienta nueva sin
/// poder verla renderizada — esto son instancias de la misma prefab de la demo que Raúl ya ha
/// visto funcionar, solo que muchas más y repartidas por todo el campo, no solo junto al tronco.
///
/// Uso: con MainWorld.unity abierta, El Sendero → Mundo → Vestir Árboles con Hierba (Quibli).
/// Idempotente: borra y regenera su propio grupo raíz "Quibli - Hierba junto a árboles" en cada
/// ejecución, así que se puede repetir tantas veces como se quiera sin ir acumulando duplicados.
/// Si la densidad sigue sin convencer, todos los números están como constantes al principio de
/// esta clase — bajar "SeparacionRelleno" o subir "LimiteInstanciasRelleno" y volver a ejecutar.
/// </summary>
public static class QuibliGrassDresser
{
    private const string TreesGroupName = "Trees";
    private const string GrassRootName = "Quibli - Hierba junto a árboles";
    private const string GrassLongPath = "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Long.prefab";
    private const string GrassShortPath = "Assets/Plugins/Quibli/Demos/Nature/Prefabs/Nature - Grass Patch Short.prefab";

    private const float RadioExclusionPorDefecto = 2.8f; // cuando el árbol no tiene NavMeshObstacle propio
    private const float MargenExtra = 0.6f;               // colchón extra fuera del radio de exclusión

    // Anillo de hierba pegado a cada árbol.
    private const float AnchoAnillo = 3.2f;
    private const int ParchesPorArbol = 4;

    // Relleno general del campo (rejilla con jitter, limitada a la zona con árboles).
    private const float SeparacionRelleno = 3.2f;          // tamaño de celda de la rejilla
    private const float RadioCercaniaArbol = 20f;          // más lejos que esto de cualquier árbol, no se considera "campo con hierba"
    private const float ProbabilidadRelleno = 0.7f;        // no todas las celdas se rellenan, para que no se vea en rejilla perfecta
    private const int LimiteInstanciasRelleno = 3500;       // tope de seguridad para no disparar el tamaño de la escena
    private const float ProporcionCortaEnRelleno = 0.75f;  // en el relleno general predomina la variante "corta"

    private class ArbolInfo
    {
        public Vector3 Posicion;
        public float RadioExclusion;
    }

    [MenuItem("El Sendero/Mundo/Vestir Árboles con Hierba (Quibli)")]
    public static void VestirArbolesConHierba()
    {
        GameObject grupoArboles = BuscarEnEscenaActiva(TreesGroupName);
        if (grupoArboles == null)
        {
            Debug.LogError($"[QuibliGrassDresser] No se encontró un GameObject llamado '{TreesGroupName}' en la escena activa. Abre MainWorld.unity antes de ejecutar esto.");
            return;
        }

        GameObject prefabLargo = AssetDatabase.LoadAssetAtPath<GameObject>(GrassLongPath);
        GameObject prefabCorto = AssetDatabase.LoadAssetAtPath<GameObject>(GrassShortPath);
        if (prefabLargo == null || prefabCorto == null)
        {
            Debug.LogError("[QuibliGrassDresser] No se encontraron los prefabs de hierba de Quibli en las rutas esperadas. Revisa que 'Assets/Plugins/Quibli/Demos/Nature/Prefabs/' siga existiendo.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null)
        {
            Debug.LogWarning("[QuibliGrassDresser] No se encontró un Terrain activo en la escena — la hierba se colocará a la altura del propio árbol en vez de ajustarse al suelo.");
        }

        // Idempotente: si ya existe un vestido anterior, lo borramos y lo regeneramos entero.
        GameObject raizAnterior = BuscarEnEscenaActiva(GrassRootName);
        if (raizAnterior != null)
        {
            Undo.DestroyObjectImmediate(raizAnterior);
        }

        GameObject raiz = new GameObject(GrassRootName);
        Undo.RegisterCreatedObjectUndo(raiz, "Vestir Árboles con Hierba (Quibli)");

        GameObject raizAnillos = new GameObject("Junto a troncos");
        raizAnillos.transform.SetParent(raiz.transform);
        GameObject raizRelleno = new GameObject("Relleno del campo");
        raizRelleno.transform.SetParent(raiz.transform);

        // --- Paso 1: recogemos info de cada árbol (posición + radio real de exclusión) ---------
        var arboles = new List<ArbolInfo>();
        int totalHijos = grupoArboles.transform.childCount;
        for (int i = 0; i < totalHijos; i++)
        {
            Transform arbol = grupoArboles.transform.GetChild(i);
            if (arbol.GetComponentInChildren<MeshRenderer>() == null) continue;

            float radioExclusion = RadioExclusionPorDefecto;
            NavMeshObstacle obstaculo = arbol.GetComponentInChildren<NavMeshObstacle>();
            if (obstaculo != null)
            {
                radioExclusion = obstaculo.shape == NavMeshObstacleShape.Box
                    ? Mathf.Max(obstaculo.size.x, obstaculo.size.z) * 0.5f
                    : obstaculo.radius;
            }

            arboles.Add(new ArbolInfo { Posicion = arbol.position, RadioExclusion = radioExclusion });
        }

        if (arboles.Count == 0)
        {
            Debug.LogError($"[QuibliGrassDresser] '{TreesGroupName}' no tiene hijos con malla visible — no hay ningún árbol del que partir.");
            return;
        }

        int parchesAnillo = 0;
        int parchesRelleno = 0;

        // --- Paso 2: anillo de hierba pegado a cada árbol, fuera de su radio de exclusión -------
        foreach (ArbolInfo arbol in arboles)
        {
            float radioInterior = arbol.RadioExclusion + MargenExtra;
            float radioExterior = radioInterior + AnchoAnillo;
            float anguloBase = RangoAleatorio(arbol.Posicion, 0, 0f, 360f);

            for (int p = 0; p < ParchesPorArbol; p++)
            {
                float angulo = anguloBase + p * (360f / ParchesPorArbol) + RangoAleatorio(arbol.Posicion, p + 1, -20f, 20f);
                float radio = radioInterior + RangoAleatorio(arbol.Posicion, p + 101, 0f, radioExterior - radioInterior);
                Vector3 offset = new Vector3(Mathf.Cos(angulo * Mathf.Deg2Rad), 0f, Mathf.Sin(angulo * Mathf.Deg2Rad)) * radio;

                bool usarLargo = RangoAleatorio(arbol.Posicion, p + 201, 0f, 1f) > 0.5f;
                CrearParche(arbol.Posicion + offset, terreno, usarLargo ? prefabLargo : prefabCorto, raizAnillos.transform,
                    RangoAleatorio(arbol.Posicion, p + 301, 0f, 360f), "anillo");
                parchesAnillo++;
            }
        }

        // --- Paso 3: relleno general del campo, en rejilla con jitter, cerca de algún árbol ------
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (ArbolInfo arbol in arboles)
        {
            minX = Mathf.Min(minX, arbol.Posicion.x);
            maxX = Mathf.Max(maxX, arbol.Posicion.x);
            minZ = Mathf.Min(minZ, arbol.Posicion.z);
            maxZ = Mathf.Max(maxZ, arbol.Posicion.z);
        }

        bool limiteAlcanzado = false;
        for (float x = minX; x <= maxX && !limiteAlcanzado; x += SeparacionRelleno)
        {
            for (float z = minZ; z <= maxZ; z += SeparacionRelleno)
            {
                Vector3 celda = new Vector3(x, 0f, z);
                float jitterX = RangoAleatorio(celda, 0, -SeparacionRelleno * 0.5f, SeparacionRelleno * 0.5f);
                float jitterZ = RangoAleatorio(celda, 1, -SeparacionRelleno * 0.5f, SeparacionRelleno * 0.5f);
                Vector3 candidata = new Vector3(x + jitterX, 0f, z + jitterZ);

                float distanciaMasCercana = float.MaxValue;
                float radioExclusionMasCercano = 0f;
                foreach (ArbolInfo arbol in arboles)
                {
                    float d = Vector2.Distance(new Vector2(candidata.x, candidata.z), new Vector2(arbol.Posicion.x, arbol.Posicion.z));
                    if (d < distanciaMasCercana)
                    {
                        distanciaMasCercana = d;
                        radioExclusionMasCercano = arbol.RadioExclusion;
                    }
                }

                // Ni demasiado lejos de cualquier árbol (fuera de la zona de pradera/bosque) ni
                // dentro del hueco de un tronco.
                if (distanciaMasCercana > RadioCercaniaArbol) continue;
                if (distanciaMasCercana < radioExclusionMasCercano + MargenExtra) continue;
                if (RangoAleatorio(candidata, 2, 0f, 1f) > ProbabilidadRelleno) continue;

                if (parchesRelleno >= LimiteInstanciasRelleno)
                {
                    limiteAlcanzado = true;
                    break;
                }

                bool usarCorto = RangoAleatorio(candidata, 3, 0f, 1f) < ProporcionCortaEnRelleno;
                CrearParche(candidata, terreno, usarCorto ? prefabCorto : prefabLargo, raizRelleno.transform,
                    RangoAleatorio(candidata, 4, 0f, 360f), "relleno");
                parchesRelleno++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);

        string avisoLimite = limiteAlcanzado
            ? $" (se alcanzó el tope de seguridad de {LimiteInstanciasRelleno} instancias de relleno — sube 'LimiteInstanciasRelleno' en el script si quieres aún más)"
            : "";
        Debug.Log($"[QuibliGrassDresser] Listo en '{scene.name}': {parchesAnillo} parches junto a troncos ({arboles.Count} árboles) + {parchesRelleno} parches de relleno general{avisoLimite}, agrupados bajo '{GrassRootName}'. Recuerda guardar la escena.");
    }

    private static void CrearParche(Vector3 posicionXZ, Terrain terreno, GameObject prefab, Transform padre, float rotacionY, string nombreBase)
    {
        Vector3 posMundo = posicionXZ;
        posMundo.y = terreno != null ? terreno.SampleHeight(posicionXZ) + terreno.transform.position.y : posicionXZ.y;

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        Undo.RegisterCreatedObjectUndo(instancia, "Vestir Árboles con Hierba (Quibli)");
        instancia.transform.position = posMundo;
        instancia.transform.rotation = Quaternion.Euler(0f, rotacionY, 0f);
        instancia.name = $"{prefab.name} ({nombreBase})";

        // La prefab de la demo trae un SphereCollider sólido pensado para una sola pieza de
        // muestra — con miles de instancias repartidas por el suelo bloquearía al jugador.
        var colisionador = instancia.GetComponentInChildren<SphereCollider>();
        if (colisionador != null)
        {
            colisionador.enabled = false;
        }
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
    /// (para no repetir la misma secuencia en cada llamada sobre el mismo punto). Así, ejecutar el
    /// menú varias veces siempre coloca la hierba igual — no hay barajado nuevo en cada pasada.
    /// </summary>
    private static float RangoAleatorio(Vector3 posicion, int canal, float min, float max)
    {
        int semilla = Mathf.RoundToInt(posicion.x * 53f) ^ Mathf.RoundToInt(posicion.z * 97f) ^ (canal * 7919);
        var rng = new System.Random(semilla);
        return min + (float)rng.NextDouble() * (max - min);
    }
}
