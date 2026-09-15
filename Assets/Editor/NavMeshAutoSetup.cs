using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Clasificación automática de walkable/obstáculo para el NavMesh de la escena, para no tener
/// que gestionarlo a mano cada vez que se añade o cambia algo en el mundo (petición de Raúl,
/// 11 sept 2026 — "odio eso porque siempre sale mal").
///
/// Cómo funciona, en dos partes independientes:
///
/// 1) CLASIFICACIÓN (automática, sin coste): cualquier GameObject con Collider sólido que NO esté
///    en el layer "Floor" (la convención ya existente en el proyecto para terreno/suelo caminable)
///    recibe un componente NavMeshObstacle con "Carve" activado. Un NavMeshObstacle con carving
///    NO necesita rebakear el NavMesh — "talla" el hueco en tiempo real solo con existir, tanto en
///    el Editor como en Play. Por eso esta parte se ejecuta sola cada vez que cambia la Hierarchy
///    (EditorApplication.hierarchyChanged): añadir una casa, un árbol o un mueble nuevo ya lo deja
///    marcado como obstáculo sin que nadie tenga que acordarse de hacerlo.
///
///    1b) DESCLASIFICACIÓN (mismo pase, automática): si un GameObject YA tiene un NavMeshObstacle
///    pero su Collider ahora cumple algún motivo de exclusión (típicamente, se marcó "Is Trigger"
///    DESPUÉS de que este script lo clasificara — ver INC "AmbientZone hereda NavMeshObstacle" en
///    registro-tecnico-incidencias.md, 12 sept 2026), el NavMeshObstacle sobrante se retira. Sin
///    esto, la clasificación es de una sola vía: como el propio "ya tiene NavMeshObstacle" es uno
///    de los motivos para no volver a mirar el objeto, un collider que se añade sin trigger y se
///    marca como trigger un instante después (más rápido de lo normal en un Sphere/Box Collider
///    recién añadido, antes del siguiente pase de clasificación) se queda con el obstáculo para
///    siempre aunque ya no tenga sentido.
///
///    1c) EXCLUSIÓN DE INTERACTUABLES (12 sept 2026): además de Floor/Player/NavMeshAgent/UI/
///    Terrain, se excluye cualquier GameObject con el componente `Interactable` (recogibles como
///    `Goods_Interactable`, cofres, hogueras, la Carta...). Estos objetos llevan Collider sólido
///    para el raycast de interacción, pero no son geometría fija del mundo — algunos, además, se
///    recogen y se cargan pegados al jugador (`PlayerCarrySystem`), momento en el que un
///    NavMeshObstacle activo se movería con ellos sin sentido. Ver
///    `incidencia-navmeshautosetup-obstacle-objetos-interactuables-2026-09-12.md`.
///
/// 2) BAKEADO de la superficie caminable (manual, vía menú): esto sí hace falta repetirlo a mano,
///    pero solo cuando cambia la propia geometría del SUELO (el terreno, o se añade una zona nueva
///    de terreno) — no cada vez que se coloca un objeto. El NavMeshSurface se limita por LayerMask
///    al layer "Floor", así que solo necesita re-bakearse si ese layer cambia de forma.
///
/// Menú: "El Sendero/Navegación/...".
/// </summary>
[InitializeOnLoad]
public static class NavMeshAutoSetup
{
    private const string WalkableLayerName = "Floor";
    private const string NavMeshSurfaceRootName = "NavMeshSurface_Auto";

    // Coste (12 sept 2026, Claude): hierarchyChanged se dispara con CUALQUIER cambio de jerarquía
    // (seleccionar, renombrar, mover, deshacer, salir de Play, abrir una escena...), a menudo
    // decenas de veces seguidas. Recorrer los ~68k objetos de MainWorld en cada disparo daba
    // tirones al editar. Ahora los disparos se agrupan: se espera a que la jerarquía lleve
    // DebounceSeconds sin cambiar y se hace UNA sola pasada.
    private const double DebounceSeconds = 1.0;
    private static double _pendingSince = -1;

    static NavMeshAutoSetup()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnHierarchyChanged()
    {
        // No tocar nada mientras se compila o se entra/sale de Play.
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        _pendingSince = EditorApplication.timeSinceStartup;
    }

    private static void OnEditorUpdate()
    {
        if (_pendingSince < 0) return;
        if (EditorApplication.timeSinceStartup - _pendingSince < DebounceSeconds) return;
        _pendingSince = -1;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ClassifyObstacles(logSummary: false);
    }

    [MenuItem("El Sendero/Navegación/Clasificar obstáculos ahora")]
    public static void ClassifyObstaclesMenu() => ClassifyObstacles(logSummary: true);

    [MenuItem("El Sendero/Navegación/Clasificar + Bakear NavMesh")]
    public static void ClassifyAndBake()
    {
        ClassifyObstacles(logSummary: true);
        BakeWalkableSurface();
    }

    /// <summary>
    /// Recorre la escena activa y añade NavMeshObstacle (con Carve) a cualquier Collider sólido
    /// que no esté ya clasificado y no esté en el layer "Floor". También retira el NavMeshObstacle
    /// de cualquier objeto que ya no debería tenerlo (típicamente, un collider que pasó a ser
    /// trigger después de clasificarse — ver comentario de clase). Es idempotente en ambos
    /// sentidos: lo que ya está bien no se vuelve a tocar, así que se puede llamar tantas veces
    /// como haga falta.
    /// </summary>
    private static void ClassifyObstacles(bool logSummary)
    {
        int floorLayer = LayerMask.NameToLayer(WalkableLayerName);
        if (floorLayer < 0)
        {
            if (logSummary)
                Debug.LogWarning($"[NavMeshAutoSetup] No existe el layer '{WalkableLayerName}' en este proyecto. Nada que clasificar.");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        int added = 0;
        int removed = 0;
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(includeInactive: false))
            {
                var go = col.gameObject;
                var existingObstacle = go.GetComponent<NavMeshObstacle>();

                // Desclasificación: si ya tiene NavMeshObstacle pero el collider cumple ahora
                // algún motivo real de exclusión (p. ej. es trigger), quitarlo. Se ignora aquí
                // el propio "ya tiene NavMeshObstacle" de ShouldExclude (ignoreExistingObstacle:
                // true) porque es justo la condición que estamos reevaluando.
                if (existingObstacle != null && ShouldExclude(col, floorLayer, ignoreExistingObstacle: true))
                {
                    Undo.DestroyObjectImmediate(existingObstacle);
                    removed++;
                    continue;
                }

                if (ShouldExclude(col, floorLayer, ignoreExistingObstacle: false))
                    continue;

                if (existingObstacle != null)
                    continue;

                var obstacle = Undo.AddComponent<NavMeshObstacle>(go);
                obstacle.carving = true;
                obstacle.shape = (col is BoxCollider) ? NavMeshObstacleShape.Box : NavMeshObstacleShape.Capsule;
                added++;
            }
        }

        if (added > 0 || removed > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        if (logSummary)
        {
            if (added > 0 || removed > 0)
                Debug.Log($"[NavMeshAutoSetup] {added} objeto(s) nuevo(s) marcado(s) como obstáculo, {removed} obstáculo(s) sobrante(s) retirado(s) (Carve).");
            else
                Debug.Log("[NavMeshAutoSetup] Nada que clasificar ni limpiar — todo estaba ya al día.");
        }
    }

    /// <summary>
    /// Motivos por los que un Collider NO debe llevar NavMeshObstacle. Se usa tanto para decidir
    /// si añadir uno nuevo como, con <paramref name="ignoreExistingObstacle"/>=true, para decidir
    /// si hay que retirar uno que ya está puesto pero ya no corresponde.
    /// </summary>
    private static bool ShouldExclude(Collider col, int floorLayer, bool ignoreExistingObstacle)
    {
        var go = col.gameObject;

        if (col.isTrigger) return true; // triggers = interactuables/portales/zonas (p. ej. AmbientZone), no son obstáculos físicos
        if (go.layer == floorLayer) return true; // es el propio suelo caminable
        if (go.CompareTag("Player")) return true;
        if (go.GetComponent<NavMeshAgent>() != null) return true; // los NPC no son obstáculo de sí mismos
        if (!ignoreExistingObstacle && go.GetComponent<NavMeshObstacle>() != null) return true; // ya clasificado
        if (go.GetComponentInParent<Canvas>() != null) return true; // UI
        if (go.GetComponent<Terrain>() != null) return true; // el propio terreno (su collider ya está excluido por layer normalmente, doble seguro)

        // FIX (12 sept 2026): objetos interactuables (cajas recogibles, cofres, cartas, hogueras...)
        // llevan Collider sólido para poder recibir el raycast de interacción/recogida, pero NO son
        // geometría estática del mundo — algunos incluso se recogen y se cargan pegados al jugador
        // (ver PlayerCarrySystem.AttachObject: desactiva los Colliders pero nunca tocaba este
        // NavMeshObstacle, así que sin esta exclusión el objeto seguía "obstruyendo" el NavMesh
        // mientras se llevaba en brazos). Cualquier GameObject con el componente base Interactable
        // (recogibles, cofres, hogueras de descanso, la Carta...) queda excluido de la clasificación
        // automática. Raúl: "cada vez que añado un collider se añade un navmesh obstacle... a la
        // caja de Eldran se le puso el obstacle y desaparece o algo pasa" — ver
        // incidencia-navmeshautosetup-obstacle-objetos-interactuables-2026-09-12.md.
        if (go.GetComponent<Interactable>() != null) return true;

        return false;
    }

    /// <summary>
    /// Crea (si no existe) un NavMeshSurface limitado por LayerMask al layer "Floor" y lo bakea.
    /// Solo hace falta repetir esto cuando cambia la forma del propio suelo, no al añadir props.
    /// </summary>
    [MenuItem("El Sendero/Navegación/Bakear solo la superficie caminable")]
    public static void BakeWalkableSurface()
    {
        int floorLayer = LayerMask.NameToLayer(WalkableLayerName);
        if (floorLayer < 0)
        {
            Debug.LogWarning($"[NavMeshAutoSetup] No existe el layer '{WalkableLayerName}' en este proyecto. No se puede bakear.");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var existing = Object.FindObjectsByType<NavMeshSurface>()
            .FirstOrDefault(s => s.gameObject.scene == scene && s.gameObject.name == NavMeshSurfaceRootName);

        NavMeshSurface surface = existing;
        if (surface == null)
        {
            var go = new GameObject(NavMeshSurfaceRootName);
            surface = go.AddComponent<NavMeshSurface>();
            Undo.RegisterCreatedObjectUndo(go, "Crear NavMeshSurface_Auto");
            Debug.Log($"[NavMeshAutoSetup] Creado '{NavMeshSurfaceRootName}' en la escena '{scene.name}'.");
        }

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << floorLayer;

        surface.BuildNavMesh();
        Debug.Log($"[NavMeshAutoSetup] NavMesh bakeado (superficie limitada al layer '{WalkableLayerName}').");
    }
}
