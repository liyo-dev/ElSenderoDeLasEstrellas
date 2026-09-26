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

    // La clasificación automática modificaba las escenas en silencio en cada cambio de jerarquía
    // (así llegó el valle del prólogo a 669 obstáculos con Carve, que multiplicaban los avisos de
    // NavMesh). Desde INC-448 está APAGADA por defecto: se usa el menú «Clasificar obstáculos
    // ahora», o se enciende aquí a propósito.
    private const string ClaveAutomatico = "NavMeshAutoSetup.Automatico";
    private const string MenuAutomatico = "El Sendero/Navegación/Clasificar obstáculos automáticamente (al cambiar la jerarquía)";

    [MenuItem(MenuAutomatico)]
    private static void AlternarAutomatico() =>
        EditorPrefs.SetBool(ClaveAutomatico, !EditorPrefs.GetBool(ClaveAutomatico, false));

    [MenuItem(MenuAutomatico, true)]
    private static bool AlternarAutomaticoValidar()
    {
        Menu.SetChecked(MenuAutomatico, EditorPrefs.GetBool(ClaveAutomatico, false));
        return true;
    }

    private static void OnHierarchyChanged()
    {
        if (!EditorPrefs.GetBool(ClaveAutomatico, false)) return;

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
                AjustarAlPie(obstacle, col);
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

    // Altura, en metros sobre la base del objeto, de lo que cuenta como obstáculo para andar: la
    // de un personaje (la altura de agente del NavMesh). Lo que queda por encima (una copa alta,
    // el dintel de un arco) no corta el paso; lo que queda por debajo (las ramas bajas de un
    // pino) sí, para que los NPCs rodeen los árboles en vez de meterse entre las ramas (INC-465).
    private static float AlturaDelPie
    {
        get
        {
            float h = NavMesh.GetSettingsCount() > 0 ? NavMesh.GetSettingsByIndex(0).agentHeight : 0f;
            return h > 0.5f ? h : 2f;
        }
    }

    [MenuItem("El Sendero/Navegación/Ajustar obstáculos a lo que ocupan en el suelo")]
    public static void AjustarObstaculosMenu()
    {
        int ajustados = 0, sinCollider = 0;
        var escenas = new HashSet<UnityEngine.SceneManagement.Scene>();
        foreach (var o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Las cajas por apoyo de una pasada anterior las rehace (o las borra) su padre dentro
            // de este mismo bucle: pueden estar ya destruidas cuando llega su turno.
            if (o == null || o.name.StartsWith(PrefijoCajaHija)) continue;
            if (!o.carving) continue;
            var col = o.GetComponent<Collider>();
            if (col == null || col.isTrigger) { sinCollider++; continue; }
            Undo.RecordObject(o, "Ajustar obstáculos a lo que ocupan en el suelo");
            if (AjustarAlPie(o, col))
            {
                ajustados++;
                escenas.Add(o.gameObject.scene);
            }
        }
        foreach (var e in escenas)
            if (e.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(e);

        Debug.Log($"[NavMeshAutoSetup] {ajustados} obstáculo(s) ajustados a lo que ocupan hasta {AlturaDelPie} m del suelo " +
                  $"({sinCollider} sin collider sólido, sin tocar). Guarda la escena. Ver INC-463.");
    }

    /// Ajusta el obstáculo a lo que el objeto ocupa hasta la altura de un personaje, en su propio
    /// espacio local (así una valla larga es una caja larga y estrecha, y un pino, su tronco y sus
    /// ramas bajas).
    /// Antes se usaba una cápsula del tamaño del objeto entero: la copa de un árbol o el largo de
    /// una valla tallaban un círculo enorme, y en la puerta del reino cerraban el paso. Ver INC-463.
    /// Devuelve true si ha cambiado algo.
    private static bool AjustarAlPie(NavMeshObstacle o, Collider col)
    {
        var shapeAntes = o.shape; var centroAntes = o.center; var tamAntes = o.size;
        var radioAntes = o.radius; var altoAntes = o.height;

        // Lo que se VE, no el collider: los árboles del pack llevan un collider que es solo el
        // tronco, y con él los NPCs pasaban entre las ramas bajas. Si el objeto tiene malla
        // visible, manda ella; si no, el collider.
        var mf = o.GetComponent<MeshFilter>();
        var mallaVisible = mf != null ? mf.sharedMesh : null;
        if (mallaVisible != null)
        {
            var cajasVisibles = CajasDelPie(mallaVisible, o.transform);
            if (cajasVisibles.Count > 0)
            {
                o.shape = NavMeshObstacleShape.Box;
                o.center = cajasVisibles[0].center;
                o.size = cajasVisibles[0].size;
                if (PonerCajasHijas(o, cajasVisibles)) return true;
                return o.shape != shapeAntes || o.center != centroAntes || o.size != tamAntes;
            }
        }

        switch (col)
        {
            case BoxCollider box:
                o.shape = NavMeshObstacleShape.Box;
                o.center = box.center;
                o.size = box.size;
                break;

            case CapsuleCollider cap when cap.direction == 1:
                o.shape = NavMeshObstacleShape.Capsule;
                o.center = cap.center;
                o.radius = cap.radius;
                o.height = cap.height;
                break;

            case SphereCollider sph:
                o.shape = NavMeshObstacleShape.Capsule;
                o.center = sph.center;
                o.radius = sph.radius;
                o.height = sph.radius * 2f;
                break;

            case MeshCollider mc when mc.sharedMesh != null:
                var cajas = CajasDelPie(mc.sharedMesh, o.transform);
                if (cajas.Count == 0) return false;
                o.shape = NavMeshObstacleShape.Box;
                o.center = cajas[0].center;
                o.size = cajas[0].size;
                // Un arco o una valla con hueco apoyan en el suelo por varios sitios: cada apoyo
                // lleva su propia caja (hijos «NavObstáculo pie N»), para que el hueco quede libre.
                bool cambioHijos = PonerCajasHijas(o, cajas);
                if (cambioHijos) return true;
                break;

            default:
                return false;
        }

        return o.shape != shapeAntes || o.center != centroAntes || o.size != tamAntes ||
               !Mathf.Approximately(o.radius, radioAntes) || !Mathf.Approximately(o.height, altoAntes);
    }

    private const string PrefijoCajaHija = "NavObstáculo pie ";
    // Separación mínima, en metros, entre dos apoyos para tratarlos como cajas distintas.
    private const float HuecoEntreApoyos = 0.8f;

    /// Cajas locales de lo que la malla ocupa hasta AlturaDelPie (en el mundo) sobre su punto más
    /// bajo: una por apoyo, separando los apoyos a lo largo del eje horizontal más largo (los dos
    /// pies de un arco). Funciona aunque el objeto esté girado. En el Editor la malla siempre se
    /// puede leer, aunque no tenga Read/Write. Ver INC-463/465.
    ///
    /// Los huecos se miden con los TRIÁNGULOS recortados a esa altura, no con los vértices: el
    /// travesaño de una valla es una pieza larga con vértices solo en los extremos, y midiendo
    /// vértices parecía que entre poste y poste no había nada. Así se abrieron agujeros en todas
    /// las vallas y Eldran se metía por los campos de flores (INC-465, vídeo de las 10:31).
    private static List<Bounds> CajasDelPie(Mesh mesh, Transform t)
    {
        var cajas = new List<Bounds>();
        Vector3[] v; int[] tri;
        try { v = mesh.vertices; tri = mesh.triangles; }
        catch { return cajas; }
        if (v == null || v.Length == 0 || tri == null || tri.Length < 3) return cajas;

        float minY = float.MaxValue;
        var alto = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            alto[i] = t.TransformPoint(v[i]).y;
            if (alto[i] < minY) minY = alto[i];
        }
        float techo = minY + AlturaDelPie;

        // Cada triángulo, recortado a lo que queda por debajo del techo (en local).
        var trozos = new List<List<Vector3>>();
        var entrada = new List<(Vector3 p, float y)>(3);
        for (int k = 0; k + 2 < tri.Length; k += 3)
        {
            entrada.Clear();
            for (int j = 0; j < 3; j++) entrada.Add((v[tri[k + j]], alto[tri[k + j]]));
            var trozo = RecortarPorDebajo(entrada, techo);
            if (trozo.Count > 0) trozos.Add(trozo);
        }
        if (trozos.Count == 0) return cajas;

        // Eje local que apunta hacia arriba y eje horizontal más largo.
        Vector3 arribaLocal = t.InverseTransformDirection(Vector3.up);
        int eje = Mathf.Abs(arribaLocal.x) > Mathf.Abs(arribaLocal.y)
            ? (Mathf.Abs(arribaLocal.x) > Mathf.Abs(arribaLocal.z) ? 0 : 2)
            : (Mathf.Abs(arribaLocal.y) > Mathf.Abs(arribaLocal.z) ? 1 : 2);
        Vector3 esc = new Vector3(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z));
        var todo = new Bounds(trozos[0][0], Vector3.zero);
        foreach (var trozo in trozos) foreach (var p in trozo) todo.Encapsulate(p);
        int largo = -1; float mejor = -1f;
        for (int k = 0; k < 3; k++)
        {
            if (k == eje) continue;
            float mundoK = todo.size[k] * esc[k];
            if (mundoK > mejor) { mejor = mundoK; largo = k; }
        }

        // Cada trozo cubre un tramo del eje largo; los tramos que se tocan (o casi) son un apoyo.
        var cajasTrozo = new List<Bounds>(trozos.Count);
        foreach (var trozo in trozos)
        {
            var bt = new Bounds(trozo[0], Vector3.zero);
            foreach (var p in trozo) bt.Encapsulate(p);
            cajasTrozo.Add(bt);
        }
        cajasTrozo.Sort((x, y) => x.min[largo].CompareTo(y.min[largo]));

        float hueco = HuecoEntreApoyos / Mathf.Max(0.0001f, esc[largo]);
        var actual = cajasTrozo[0];
        for (int i = 1; i < cajasTrozo.Count; i++)
        {
            if (cajasTrozo[i].min[largo] - actual.max[largo] > hueco)
            {
                cajas.Add(Rellenar(actual, eje, arribaLocal[eje], esc));
                actual = cajasTrozo[i];
            }
            else actual.Encapsulate(cajasTrozo[i]);
        }
        cajas.Add(Rellenar(actual, eje, arribaLocal[eje], esc));
        return cajas;
    }

    /// Recorta un polígono (puntos locales con su altura en el mundo) a lo que queda en o por
    /// debajo de 'techo'. Devuelve los puntos locales del trozo, o ninguno.
    private static List<Vector3> RecortarPorDebajo(List<(Vector3 p, float y)> poli, float techo)
    {
        var salida = new List<Vector3>(4);
        for (int i = 0; i < poli.Count; i++)
        {
            var a = poli[i];
            var b = poli[(i + 1) % poli.Count];
            bool aDentro = a.y <= techo, bDentro = b.y <= techo;
            if (aDentro) salida.Add(a.p);
            if (aDentro != bDentro)
            {
                float f = (techo - a.y) / (b.y - a.y);
                salida.Add(Vector3.Lerp(a.p, b.p, f));
            }
        }
        return salida;
    }

    /// Da a la caja la altura de un personaje por el eje que apunta hacia arriba y un grosor
    /// mínimo en los otros dos.
    private static Bounds Rellenar(Bounds b, int eje, float signoArriba, Vector3 esc)
    {
        Vector3 min = b.min, max = b.max;
        float altoLocal = AlturaDelPie / Mathf.Max(0.0001f, esc[eje]);
        if (max[eje] - min[eje] < altoLocal)
        {
            if (signoArriba >= 0f) max[eje] = min[eje] + altoLocal;
            else min[eje] = max[eje] - altoLocal;
        }
        for (int k = 0; k < 3; k++)
        {
            if (k == eje) continue;
            float minimo = 0.1f / Mathf.Max(0.0001f, esc[k]);
            if (max[k] - min[k] < minimo)
            {
                float c = (min[k] + max[k]) * 0.5f;
                min[k] = c - minimo * 0.5f; max[k] = c + minimo * 0.5f;
            }
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

    /// Deja un hijo con su NavMeshObstacle por cada caja a partir de la segunda, y quita los que
    /// sobren de pasadas anteriores. Devuelve true si ha cambiado algo.
    private static bool PonerCajasHijas(NavMeshObstacle o, List<Bounds> cajas)
    {
        var existentes = new List<Transform>();
        foreach (Transform hijo in o.transform)
            if (hijo.name.StartsWith(PrefijoCajaHija)) existentes.Add(hijo);

        bool cambio = false;
        for (int i = 1; i < cajas.Count; i++)
        {
            string nombre = PrefijoCajaHija + (i + 1);
            Transform hijo = existentes.Find(h => h.name == nombre);
            if (hijo == null)
            {
                var go = new GameObject(nombre);
                Undo.RegisterCreatedObjectUndo(go, "Cajas de apoyo del obstáculo");
                go.transform.SetParent(o.transform, false);
                go.layer = o.gameObject.layer;
                hijo = go.transform;
                cambio = true;
            }
            existentes.Remove(hijo);
            var obs = hijo.GetComponent<NavMeshObstacle>();
            if (obs == null) { obs = Undo.AddComponent<NavMeshObstacle>(hijo.gameObject); cambio = true; }
            else Undo.RecordObject(obs, "Cajas de apoyo del obstáculo");
            obs.carving = true;
            obs.shape = NavMeshObstacleShape.Box;
            if (obs.center != cajas[i].center || obs.size != cajas[i].size) cambio = true;
            obs.center = cajas[i].center;
            obs.size = cajas[i].size;
        }
        foreach (var sobra in existentes)
        {
            Undo.DestroyObjectImmediate(sobra.gameObject);
            cambio = true;
        }
        return cambio;
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
