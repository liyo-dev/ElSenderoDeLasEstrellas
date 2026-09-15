using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Prueba de Will en el Sendero (GDD escena 17): laberinto procedural de ESPEJOS sobre los
/// recuerdos/miedos de Will — petición explícita de Raúl (8 sep 2026): "quiero un laberinto de
/// ESPEJOS, donde los personajes se vean reflejados en las paredes... con diferentes caminos".
///
/// *** REESCRITURA COMPLETA (9 sep 2026): se abandona el kit Fantasy_Kingdom_Pack ***
/// Las dos versiones anteriores de este script montaban el laberinto con las 32 variantes de sala
/// del kit modular (Room01..04_a01..h01). Primero adivinando la convención de puertas del kit
/// (INC-161, mal) y después detectándola por física (bien, pero el laberinto seguía siendo un único
/// camino sin bifurcaciones y, además, salía con las salas solapadas porque el tamaño de celda se
/// medía solo de la primera pieza colocada y no todas las bases medían lo mismo). Con el cambio de
/// dirección hacia un laberinto de espejos de verdad, ya no tiene sentido seguir tirando de un kit de
/// interior de piedra sin nada de espejo — así que ahora los muros y el suelo se generan
/// PROCEDURALMENTE desde el propio grafo del laberinto (paneles simples, sin geometría prefabricada),
/// lo que de paso elimina el bug de solape de raíz: cada celda mide exactamente <see cref="CellSize"/>,
/// siempre.
///
/// Algoritmo: laberinto perfecto (árbol de expansión, sin ciclos) por backtracking recursivo sobre
/// una rejilla NxN. La meta es la celda más lejana del inicio (BFS sobre el árbol), para un recorrido
/// largo de verdad. Después se añaden unas pocas conexiones extra (<see cref="AddExtraLoopConnections"/>)
/// para que haya caminos distintos de verdad, no un único recorrido con callejones sueltos — así lo
/// pidió Raúl. Los callejones sin salida que sigan siéndolo tras esas bifurcaciones se usan como
/// "ecos de Will": combates reutilizando enemigos ya existentes en el proyecto (Demon/Demon2/Spider1),
/// sin tocar su configuración.
///
/// Espejos: por coste de rendimiento, solo unos ~8-10 puntos clave (inicio, ecos, meta y las propias
/// bifurcaciones nuevas) llevan un espejo REAL con reflejo en tiempo real (componente
/// <see cref="MirrorReflection"/> + shader Sendero/PlanarMirror). El resto de los muros usan un
/// material "espejo oscuro" barato (Lit de URP con mucho brillo, reflejos solo de skybox/reflection
/// probes) — decisión explícita de Raúl ("espejos reales en puntos clave").
///
/// Regenerar (el mismo menú) borra y reconstruye todo el contenedor "PRUEBA_WILL_LABERINTO" desde
/// cero — no es idempotente celda a celda, porque el laberinto es un conjunto. Si Raúl edita algo a
/// mano dentro del contenedor, se perderá al regenerar. Los materiales sí son idempotentes: se crean
/// una única vez como assets reutilizables (no se duplican ni se pierden los ajustes hechos a mano en
/// el Inspector al volver a generar).
/// </summary>
public static class WillTrialMazeBuilder
{
    private const string ScenePath = "Assets/Scenes/Worlds/Sendero_PruebaWill.unity";
    private const string RootName = "PRUEBA_WILL_LABERINTO";

    private const int MazeWidth = 7;
    private const int MazeHeight = 7;
    private const int Seed = 20260901;
    private const int EchoBattleCount = 3;
    private const int ExtraLoopConnections = 5;
    private const int MaxLoopMirrors = 3;

    private const float CellSize = 6f;
    private const float WallHeight = 4f;
    private const float WallThickness = 0.3f;
    private const float FloorThickness = 0.2f;

    private const string MaterialsFolder = "Assets/Art/Materials/Sendero/PruebaWill";
    private const string RealMirrorShaderName = "Sendero/PlanarMirror";

    // Id del SpawnAnchor de entrada — el mismo sistema genérico de teletransporte que usa el resto
    // del proyecto (SpawnAnchor/AnchorRegistry/SpawnManager/TeleportService): un PortalTrigger en
    // el hub apuntando a este id coloca al jugador aquí Y teletransporta automáticamente a todo el
    // grupo (TeleportService.TeleportCompanionsToPlayer se llama solo tras cada teleport) — no hace
    // falta ningún script aparte para colocar al party, ese trabajo ya lo hace el sistema genérico.
    public const string StartAnchorId = "SENDERO_PRUEBAWILL_START";

    private static readonly string[] DirNames = { "N", "E", "S", "O" };

    private static readonly string[] EchoEnemyPaths =
    {
        "Assets/Prefabs/Enemy/Demon.prefab",
        "Assets/Prefabs/Enemy/Demon2.prefab",
        "Assets/Prefabs/Enemy/Spider1.prefab",
    };

    // Direcciones en orden N=0, E=1, S=2, W=3. Bit i -> 1<<i.
    private static readonly int[] DX = { 0, 1, 0, -1 };
    private static readonly int[] DZ = { 1, 0, -1, 0 };

    private class Cell
    {
        public bool visited;
        public int connections; // bitmask N/E/S/W
    }

    [MenuItem("El Sendero/Escena/Generar Laberinto de la Prueba de Will")]
    public static void GenerateMaze()
    {
        Scene scene = OpenOrCreateScene();

        // --- Limpiar generación anterior (regenerar = borrar y reconstruir) ---
        GameObject root = GameObject.Find(RootName);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        root = new GameObject(RootName);

        // Marca la zona como interior de verdad — sin esto EnvironmentController la trata como
        // exterior por defecto (mismo bug ya visto y corregido en "Will House", incidencia del
        // 4 sep 2026: niebla/lluvia coladas en un interior que nunca se dio de alta como tal) y el
        // laberinto heredaría el clima del exterior en vez de su propia atmósfera de espejos/niebla.
        var anchorEnv = root.AddComponent<AnchorEnvironment>();
        anchorEnv.isInterior = true;
        anchorEnv.useSolidColorBackground = true;
        anchorEnv.interiorBgColor = new Color(0.03f, 0.04f, 0.07f);

        var log = new StringBuilder();
        log.AppendLine("=== Generador Laberinto de Espejos — Prueba de Will ===");
        log.AppendLine($"Rejilla {MazeWidth}x{MazeHeight}, semilla {Seed}");

        // --- 1) Generar el árbol de expansión (backtracking recursivo, iterativo con pila) ---
        var rng = new System.Random(Seed);
        Cell[,] grid = new Cell[MazeWidth, MazeHeight];
        for (int x = 0; x < MazeWidth; x++)
            for (int z = 0; z < MazeHeight; z++)
                grid[x, z] = new Cell();

        var stack = new Stack<Vector2Int>();
        var start = new Vector2Int(0, 0);
        grid[start.x, start.y].visited = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            var unvisitedDirs = new List<int>();
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = current.x + DX[dir];
                int nz = current.y + DZ[dir];
                if (nx < 0 || nx >= MazeWidth || nz < 0 || nz >= MazeHeight) continue;
                if (!grid[nx, nz].visited) unvisitedDirs.Add(dir);
            }

            if (unvisitedDirs.Count == 0)
            {
                stack.Pop();
                continue;
            }

            int chosenDir = unvisitedDirs[rng.Next(unvisitedDirs.Count)];
            int nx2 = current.x + DX[chosenDir];
            int nz2 = current.y + DZ[chosenDir];
            int opposite = (chosenDir + 2) % 4;

            grid[current.x, current.y].connections |= (1 << chosenDir);
            grid[nx2, nz2].connections |= (1 << opposite);
            grid[nx2, nz2].visited = true;
            stack.Push(new Vector2Int(nx2, nz2));
        }

        // --- 2) Meta = celda más lejana del inicio sobre el árbol puro (BFS), recorrido largo de verdad ---
        Vector2Int goal = FindFarthestCell(grid, start);
        log.AppendLine($"Inicio: {start} — Meta: {goal}");

        // --- 3) Bifurcaciones: unas pocas conexiones extra para que haya caminos DISTINTOS de verdad
        // (petición explícita de Raúl), no solo un único recorrido con callejones colgando. ---
        List<Vector2Int> loopJunctionCells = AddExtraLoopConnections(grid, rng, ExtraLoopConnections);
        log.AppendLine($"Bifurcaciones extra añadidas: {loopJunctionCells.Count} (de {ExtraLoopConnections} pedidas).");

        // --- 4) Callejones sin salida (dead-ends, ni inicio ni meta, ya con las bifurcaciones puestas) ---
        var deadEnds = new List<Vector2Int>();
        for (int x = 0; x < MazeWidth; x++)
            for (int z = 0; z < MazeHeight; z++)
            {
                var pos = new Vector2Int(x, z);
                if (pos == start || pos == goal) continue;
                if (PopCount(grid[x, z].connections) == 1) deadEnds.Add(pos);
            }
        Shuffle(deadEnds, rng);
        var echoCells = new HashSet<Vector2Int>();
        for (int i = 0; i < Mathf.Min(EchoBattleCount, deadEnds.Count); i++) echoCells.Add(deadEnds[i]);

        // --- 5) Puntos con espejo REAL: inicio, meta, ecos y hasta 3 bifurcaciones ---
        var realMirrorCells = new HashSet<Vector2Int> { start, goal };
        foreach (var c in echoCells) realMirrorCells.Add(c);
        int addedFromLoops = 0;
        foreach (var c in loopJunctionCells)
        {
            if (addedFromLoops >= MaxLoopMirrors) break;
            if (realMirrorCells.Add(c)) addedFromLoops++;
        }

        var realMirrorWalls = new HashSet<(int x, int z, int dir)>();
        foreach (var cellPos in realMirrorCells)
        {
            int rawDir = GetMirrorFacingDirection(grid[cellPos.x, cellPos.y].connections);
            realMirrorWalls.Add(CanonicalWall(cellPos.x, cellPos.y, rawDir));
        }
        log.AppendLine($"Celdas con espejo real ({realMirrorCells.Count}): {string.Join(", ", realMirrorCells)}.");

        // --- 6) Materiales (assets reutilizables — no se duplican al regenerar) ---
        Material darkMirrorMat = GetOrCreateDarkMirrorMaterial();
        Material realMirrorMat = GetOrCreateRealMirrorMaterial();
        Material floorMat = GetOrCreateFloorMaterial();

        // --- 7) Geometría procedural: muros + suelo (sin ninguna pieza prefabricada -> no puede haber solapes) ---
        BuildWalls(grid, root, realMirrorWalls, darkMirrorMat, realMirrorMat, log);
        BuildFloors(grid, root, floorMat);

        // --- 8) Marcadores de inicio y meta ---
        // El inicio es un SpawnAnchor de verdad (mismo sistema que el resto de puertas/interiores
        // del proyecto) para que un PortalTrigger en el hub pueda apuntar aquí — colocar al jugador
        // Y al grupo entero es automático desde ese momento, no hace falta ningún script aparte
        // (ver TeleportService.TeleportCompanionsToPlayer, ya se llama solo tras cada teleport).
        var startMarker = new GameObject("WILL_MAZE_START");
        startMarker.transform.SetParent(root.transform);
        startMarker.transform.position = GridToWorld(start.x, start.y);
        // Orientado hacia el primer pasillo abierto de la celda de inicio, para que el grupo no
        // aparezca mirando a un muro.
        int startFacingDir = FirstOpenDirection(grid[start.x, start.y].connections);
        startMarker.transform.rotation = Quaternion.LookRotation(new Vector3(DX[startFacingDir], 0f, DZ[startFacingDir]), Vector3.up);
        var startAnchor = startMarker.AddComponent<SpawnAnchor>();
        startAnchor.anchorId = StartAnchorId;

        var goalMarker = new GameObject("WILL_MAZE_GOAL");
        goalMarker.transform.SetParent(root.transform);
        goalMarker.transform.position = GridToWorld(goal.x, goal.y);
        var goalTrigger = goalMarker.AddComponent<BoxCollider>();
        goalTrigger.isTrigger = true;
        goalTrigger.size = new Vector3(CellSize * 0.6f, 3f, CellSize * 0.6f);
        // Enganche pendiente: aquí es donde Raúl decide cómo se resuelve la prueba de Will
        // (recompensa, señal narrativa, cinemática) cuando conecte esta escena a la narrativa real.
        // De momento es solo el punto marcado — sin lógica, para no tocar el grafo narrativo a ciegas.

        // --- 9) Ecos de Will (combates reutilizando enemigos ya existentes, sin re-configurarlos) ---
        int echoIndex = 0;
        foreach (var cellPos in echoCells)
        {
            string enemyPath = EchoEnemyPaths[echoIndex % EchoEnemyPaths.Length];
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
            if (enemyPrefab == null)
            {
                log.AppendLine($"AVISO: no se pudo cargar {enemyPath} para el eco #{echoIndex + 1}.");
                echoIndex++;
                continue;
            }
            GameObject enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, root.transform);
            enemyInstance.name = $"Eco_de_Will_{echoIndex + 1}_{enemyPrefab.name}";
            enemyInstance.transform.position = GridToWorld(cellPos.x, cellPos.y);
            log.AppendLine($"Eco de Will #{echoIndex + 1}: {enemyPrefab.name} en celda {cellPos}");
            echoIndex++;
        }

        // --- 10) NavMesh: el proyecto ya usa Unity.AI.Navigation.NavMeshSurface (visto en Sendero.unity) ---
        var navSurface = root.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.Children;
        float gridSpanX = MazeWidth * CellSize;
        float gridSpanZ = MazeHeight * CellSize;
        navSurface.center = new Vector3(gridSpanX * 0.5f - CellSize * 0.5f, 1f, gridSpanZ * 0.5f - CellSize * 0.5f);
        navSurface.size = new Vector3(gridSpanX + CellSize, WallHeight, gridSpanZ + CellSize);
        navSurface.BuildNavMesh();
        log.AppendLine("NavMesh horneado sobre el contenedor del laberinto.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine("--- Laberinto procedural: cada muro y cada suelo salen del propio grafo, sin piezas prefabricadas de por medio — no puede haber solapes. ---");
        Debug.Log(log.ToString());
    }

    // Añade hasta `count` conexiones extra entre celdas vecinas que el árbol de expansión dejó sin
    // conectar, para que existan caminos distintos de verdad (bucles) en vez de un único recorrido
    // con callejones. Solo mira E y N desde cada celda para no proponer el mismo par dos veces.
    // Devuelve la celda "A" de cada bifurcación añadida (para poder ponerle un espejo real encima).
    private static List<Vector2Int> AddExtraLoopConnections(Cell[,] grid, System.Random rng, int count)
    {
        var candidates = new List<(Vector2Int a, Vector2Int b, int dir)>();
        for (int x = 0; x < MazeWidth; x++)
        {
            for (int z = 0; z < MazeHeight; z++)
            {
                var a = new Vector2Int(x, z);
                foreach (int dir in new[] { 1, 0 }) // E, N — evita duplicar el mismo par
                {
                    int nx = x + DX[dir];
                    int nz = z + DZ[dir];
                    if (nx < 0 || nx >= MazeWidth || nz < 0 || nz >= MazeHeight) continue;
                    if ((grid[x, z].connections & (1 << dir)) != 0) continue; // ya conectadas
                    candidates.Add((a, new Vector2Int(nx, nz), dir));
                }
            }
        }
        Shuffle(candidates, rng);

        var junctionCells = new List<Vector2Int>();
        int made = 0;
        foreach (var candidate in candidates)
        {
            if (made >= count) break;
            int dir = candidate.dir;
            int opposite = (dir + 2) % 4;
            grid[candidate.a.x, candidate.a.y].connections |= (1 << dir);
            grid[candidate.b.x, candidate.b.y].connections |= (1 << opposite);
            junctionCells.Add(candidate.a);
            made++;
        }
        return junctionCells;
    }

    // Primer pasillo abierto de una celda (N/E/S/O, en ese orden) — usado para orientar el
    // SpawnAnchor de inicio hacia dentro del laberinto en vez de dejarlo mirando a un muro.
    private static int FirstOpenDirection(int connections)
    {
        for (int d = 0; d < 4; d++)
            if ((connections & (1 << d)) != 0) return d;
        return 0; // celda sin ninguna conexión (no debería pasar en un laberinto conectado): red de seguridad
    }

    // Qué lado de una celda debería llevar el espejo real: si es un callejón sin salida, la pared
    // OPUESTA a su única conexión (así el jugador ve su reflejo justo al entrar). Si no es un
    // callejón (inicio, meta o bifurcación con más de una salida), el primer lado cerrado disponible.
    private static int GetMirrorFacingDirection(int connections)
    {
        if (PopCount(connections) == 1)
        {
            for (int d = 0; d < 4; d++)
            {
                if ((connections & (1 << d)) == 0) continue;
                int opposite = (d + 2) % 4;
                if ((connections & (1 << opposite)) == 0) return opposite;
            }
        }
        for (int d = 0; d < 4; d++)
            if ((connections & (1 << d)) == 0) return d;
        return 0; // celda totalmente abierta por los 4 lados (no debería pasar): red de seguridad
    }

    // Cada pared física la comparten dos celdas vecinas (el lado Sur de una es el lado Norte de la
    // de abajo, etc.). Para no construir la misma geometría dos veces, Norte y Este SIEMPRE se
    // construyen desde la celda que los pide; Sur y Oeste solo se construyen si no tienen vecina
    // (borde de la rejilla) — si la tienen, la vecina ya construyó esa misma pared por su lado
    // Norte/Este. Esta función devuelve la clave canónica (celda dueña + dirección) de una pared,
    // para poder marcarla como "espejo real" independientemente de qué celda la pidió.
    private static (int x, int z, int dir) CanonicalWall(int x, int z, int dir)
    {
        switch (dir)
        {
            case 0: return (x, z, 0);
            case 1: return (x, z, 1);
            case 2: return z == 0 ? (x, z, 2) : (x, z - 1, 0);
            case 3: return x == 0 ? (x, z, 3) : (x - 1, z, 1);
            default: return (x, z, dir);
        }
    }

    private static void BuildWalls(Cell[,] grid, GameObject root, HashSet<(int x, int z, int dir)> realMirrorWalls, Material darkMat, Material realMat, StringBuilder log)
    {
        int wallCount = 0, mirrorCount = 0;
        for (int x = 0; x < MazeWidth; x++)
        {
            for (int z = 0; z < MazeHeight; z++)
            {
                int connections = grid[x, z].connections;

                // Norte y Este: esta celda es SIEMPRE la dueña de esa pared (interior o borde).
                if ((connections & (1 << 0)) == 0)
                {
                    bool isMirror = realMirrorWalls.Contains((x, z, 0));
                    BuildWallSegment(root, x, z, 0, isMirror ? realMat : darkMat, isMirror);
                    wallCount++; if (isMirror) mirrorCount++;
                }
                if ((connections & (1 << 1)) == 0)
                {
                    bool isMirror = realMirrorWalls.Contains((x, z, 1));
                    BuildWallSegment(root, x, z, 1, isMirror ? realMat : darkMat, isMirror);
                    wallCount++; if (isMirror) mirrorCount++;
                }
                // Sur y Oeste: solo en el borde exterior — el resto ya lo construyó la celda vecina
                // por su lado Norte/Este (misma pared física; evita duplicar geometría y z-fighting).
                if (z == 0 && (connections & (1 << 2)) == 0)
                {
                    bool isMirror = realMirrorWalls.Contains((x, z, 2));
                    BuildWallSegment(root, x, z, 2, isMirror ? realMat : darkMat, isMirror);
                    wallCount++; if (isMirror) mirrorCount++;
                }
                if (x == 0 && (connections & (1 << 3)) == 0)
                {
                    bool isMirror = realMirrorWalls.Contains((x, z, 3));
                    BuildWallSegment(root, x, z, 3, isMirror ? realMat : darkMat, isMirror);
                    wallCount++; if (isMirror) mirrorCount++;
                }
            }
        }
        log.AppendLine($"Muros construidos: {wallCount} (de ellos, espejos reales: {mirrorCount}).");
    }

    private static void BuildWallSegment(GameObject root, int x, int z, int dir, Material material, bool isRealMirror)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Muro_{x}_{z}_{DirNames[dir]}{(isRealMirror ? "_EspejoReal" : "")}";
        wall.transform.SetParent(root.transform);

        Vector3 dirVec = new Vector3(DX[dir], 0f, DZ[dir]);
        Vector3 cellCenter = GridToWorld(x, z);
        wall.transform.position = cellCenter + dirVec * (CellSize * 0.5f) + Vector3.up * (WallHeight * 0.5f);
        // El espejo/muro mira hacia DENTRO de la celda (normal opuesta a dirVec) — importante tanto
        // para que MirrorReflection.transform.forward sea el plano correcto como para que el muro
        // oscuro se vea bien iluminado desde dentro del pasillo.
        wall.transform.rotation = Quaternion.LookRotation(-dirVec, Vector3.up);
        // Escala en el marco local del panel: X = ancho (a lo largo de la pared), Y = alto,
        // Z = grosor (a lo largo de la normal) — válido para las 4 direcciones gracias a la rotación
        // de arriba, sin necesitar casos especiales para muros N/S vs E/O.
        wall.transform.localScale = new Vector3(CellSize, WallHeight, WallThickness);

        var renderer = wall.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.NavigationStatic);

        if (isRealMirror)
        {
            wall.AddComponent<MirrorReflection>();
        }
    }

    private static void BuildFloors(Cell[,] grid, GameObject root, Material floorMat)
    {
        for (int x = 0; x < MazeWidth; x++)
        {
            for (int z = 0; z < MazeHeight; z++)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = $"Suelo_{x}_{z}";
                floor.transform.SetParent(root.transform);
                Vector3 cellCenter = GridToWorld(x, z);
                floor.transform.position = cellCenter + Vector3.down * (FloorThickness * 0.5f);
                floor.transform.localScale = new Vector3(CellSize, FloorThickness, CellSize);
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);
            }
        }
    }

    // --- Materiales: se crean UNA vez como assets reutilizables bajo MaterialsFolder. Si ya existen
    // (de una generación anterior) se reutilizan tal cual — así los ajustes que Raúl haga a mano en
    // el Inspector (tintes, texturas) sobreviven a que se regenere el laberinto. ---

    private static Material GetOrCreateDarkMirrorMaterial()
    {
        string path = $"{MaterialsFolder}/Mat_Sendero_MurEspejoOscuro.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolder(MaterialsFolder);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = "Mat_Sendero_MurEspejoOscuro" };
        mat.SetColor("_BaseColor", new Color(0.04f, 0.05f, 0.07f, 1f));
        mat.SetFloat("_Smoothness", 0.92f);
        mat.SetFloat("_Metallic", 0.15f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material GetOrCreateRealMirrorMaterial()
    {
        string path = $"{MaterialsFolder}/Mat_Sendero_EspejoReal.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolder(MaterialsFolder);
        Shader shader = Shader.Find(RealMirrorShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[WillTrialMazeBuilder] No se encontró el shader '{RealMirrorShaderName}' (¿Unity todavía no lo ha importado/compilado? deja que termine de compilar y vuelve a generar). Se usa el material de espejo oscuro como red de seguridad mientras tanto.");
            return GetOrCreateDarkMirrorMaterial();
        }
        var mat = new Material(shader) { name = "Mat_Sendero_EspejoReal" };
        mat.SetColor("_TintColor", new Color(0.55f, 0.68f, 0.78f, 1f));
        mat.SetFloat("_ReflectionStrength", 0.85f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material GetOrCreateFloorMaterial()
    {
        string path = $"{MaterialsFolder}/Mat_Sendero_SueloLuzCondensada.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolder(MaterialsFolder);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = "Mat_Sendero_SueloLuzCondensada" };
        mat.SetColor("_BaseColor", new Color(0.03f, 0.04f, 0.06f, 1f));
        mat.SetFloat("_Smoothness", 0.45f);
        mat.SetFloat("_Metallic", 0f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.10f, 0.22f, 0.34f, 1f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Scene OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        string dir = System.IO.Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            EnsureFolder(dir);
        }
        EditorSceneManager.SaveScene(newScene, ScenePath);
        return newScene;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static Vector3 GridToWorld(int x, int z)
    {
        return new Vector3(x * CellSize, 0f, z * CellSize);
    }

    private static int PopCount(int bitmask)
    {
        int count = 0;
        while (bitmask != 0)
        {
            count += bitmask & 1;
            bitmask >>= 1;
        }
        return count;
    }

    private static Vector2Int FindFarthestCell(Cell[,] grid, Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();
        dist[from] = 0;
        queue.Enqueue(from);
        Vector2Int farthest = from;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (dist[current] > dist[farthest]) farthest = current;

            int connections = grid[current.x, current.y].connections;
            for (int dir = 0; dir < 4; dir++)
            {
                if ((connections & (1 << dir)) == 0) continue;
                var next = new Vector2Int(current.x + DX[dir], current.y + DZ[dir]);
                if (dist.ContainsKey(next)) continue;
                dist[next] = dist[current] + 1;
                queue.Enqueue(next);
            }
        }
        return farthest;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
