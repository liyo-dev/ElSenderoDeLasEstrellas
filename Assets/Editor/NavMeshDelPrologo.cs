using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Rehornea el NavMesh de las escenas abiertas (INC-389).
///
/// ── Por qué ───────────────────────────────────────────────────────────────────────────────────
/// En el log de la grabación 15 sale esto, muchas veces:
///
///     Failed to create agent because it is not close enough to the NavMesh
///     [SequenceMovement] 'NPC_MagoOscuro' no tiene un NavMeshAgent utilizable (sin componente,
///     desactivado, o fuera del NavMesh), así que va andando en línea recta.
///
/// Y en línea recta es exactamente lo que se ve: «hay un NPC que cuando va al puente se come el
/// carro y salta por él», «Liora atraviesa a la gente en el carro». Sin agente no hay esquivar ni
/// rodear nada — el beat mueve el transform y punto.
///
/// El NavMesh del valle existe (Assets/Scenes/Worlds/Prologo_Valle/NavMesh-NavMesh Surface.asset),
/// pero está horneado de antes de que el decorado se moviera: donde ahora hay carreta, puestos y
/// gente, el suelo navegable ya no coincide. Esto lo vuelve a hornear tal cual está la escena hoy.
///
/// No se mete en PREPARAR TODO a propósito: hornear tarda y no hace falta cada vez, solo cuando se
/// ha movido el decorado.
///
/// ── Con qué se hornea el valle (INC-396) ─────────────────────────────────────────────────────
/// La NavMeshSurface del valle horneaba con «Render Meshes»: TODO lo que se pinta cuenta como
/// suelo u obstáculo. Con el valle nuevo eso incluye cientos de matas de hierba y flores sueltas
/// por el césped (las `Hierba_*`, `Flores`, `Ribera_Junco`). Cada mata baja se hornea como un
/// escalón —el agente sube y baja al pasar: «es como si en el suelo hubiera obstáculos, se ponen a
/// saltar mientras andan»— y cada mata alta como un agujero, así que el único sitio limpio para
/// andar era el camino de tierra: «los NPCs se ponen ahora en fila».
///
/// En el prólogo se hornea con los COLLIDERS: el suelo (`Suelo_Valle`), las lomas, las casas, la
/// carreta... todo lo que de verdad para a alguien. La hierba no tiene collider y deja de existir
/// para el NavMesh. Y fuera las capas que no son decorado (UI, disparadores, clima, personajes).
/// Solo se toca la superficie de las escenas `Prologo*`: la de MainWorld se hornea como estaba.
public static class NavMeshDelPrologo
{
    /// Capas que nunca son suelo ni obstáculo de verdad: interfaz, disparadores, zonas de clima,
    /// proyectiles y personajes (a los agentes ya los ignora la propia superficie).
    private static readonly string[] CapasFuera =
    {
        "TransparentFX", "Ignore Raycast", "Player", "UI", "Projectile", "Enemy", "InteractHint",
        "ProjectileEnemy", "PauseUI", "UI_Portrait", "Minimap", "AmbientZone", "Weather",
    };

    private static bool EsDelPrologo(NavMeshSurface s)
        => s != null && s.gameObject.scene.name.StartsWith("Prologo");

    /// Pone la superficie del prólogo a hornear con colliders. Devuelve qué ha cambiado, para el log.
    private static string AjustarSuperficieDelPrologo(NavMeshSurface s)
    {
        int mascara = ~0;
        foreach (var capa in CapasFuera)
        {
            int i = LayerMask.NameToLayer(capa);
            if (i >= 0) mascara &= ~(1 << i);
        }

        var cambios = new List<string>();
        if (s.useGeometry != UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders)
        {
            cambios.Add($"geometría {s.useGeometry} → PhysicsColliders");
            s.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        }
        if (s.layerMask.value != mascara)
        {
            cambios.Add("capas: fuera UI, disparadores, clima y personajes");
            s.layerMask = mascara;
        }
        return cambios.Count > 0 ? string.Join("; ", cambios) : "ya estaba bien";
    }

    /// El NavMesh recién horneado, a un .asset junto a la escena (INC-398).
    ///
    /// `BuildNavMesh()` deja los datos solo en memoria. Al guardar, Unity los metía DENTRO de la
    /// escena — y como esos datos solo existen en formato binario, la escena entera pasó a
    /// guardarse en binario (Prologo_Valle.unity, 24 sep, 07:03): deja de poder leerse como texto
    /// y las herramientas que revisan marcas y planos se quedan ciegas. Es lo mismo que hace el
    /// botón «Bake» del Inspector: un asset en la carpeta de la escena.
    private static void GuardarComoAsset(NavMeshSurface s, string rutaAnterior)
    {
        if (s.navMeshData == null || AssetDatabase.Contains(s.navMeshData)) return;

        string ruta = rutaAnterior;
        if (string.IsNullOrEmpty(ruta))
        {
            string escena = s.gameObject.scene.path;
            if (string.IsNullOrEmpty(escena)) return;   // escena sin guardar: se queda en memoria
            string carpeta = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(escena),
                System.IO.Path.GetFileNameWithoutExtension(escena)).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(carpeta))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(carpeta).Replace('\\', '/'),
                    System.IO.Path.GetFileName(carpeta));
            ruta = $"{carpeta}/NavMesh-{s.name}.asset";
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(ruta) != null)
            AssetDatabase.DeleteAsset(ruta);

        AssetDatabase.CreateAsset(s.navMeshData, ruta);
        EditorUtility.SetDirty(s);
        Debug.Log($"[NavMesh] Guardado en '{ruta}' (la escena lo referencia, no lo lleva dentro).", s);
    }

    /// Rehornea el NavMesh de UNA escena (lo usa PREPARAR TODO para el valle). Devuelve cuántas
    /// superficies ha horneado.
    public static int RehornearEscena(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return 0;
        int n = 0;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var s in raiz.GetComponentsInChildren<NavMeshSurface>(true))
            {
                Hornear(s);
                n++;
            }
        if (n > 0) AssetDatabase.SaveAssets();
        return n;
    }

    private static void Hornear(NavMeshSurface s)
    {
        if (s == null) return;

        var disparadores = new List<Collider>();
        if (EsDelPrologo(s))
        {
            Undo.RecordObject(s, "Ajustar NavMesh del prólogo");
            Debug.Log($"[NavMesh] '{s.gameObject.scene.name}/{s.name}': {AjustarSuperficieDelPrologo(s)}.", s);
            ArreglarSuelosConObstaculo(s.gameObject.scene);

            // Un disparador (un aviso de proximidad, una zona) no es un muro. Horneando con
            // colliders contaría como uno, así que se apagan mientras se hornea y se vuelven a
            // encender justo después: la escena queda exactamente como estaba.
            foreach (var raiz in s.gameObject.scene.GetRootGameObjects())
                foreach (var c in raiz.GetComponentsInChildren<Collider>(false))
                    if (c.enabled && c.isTrigger) { c.enabled = false; disparadores.Add(c); }
        }

        // Dónde vive el NavMesh ahora, para guardar el nuevo en el mismo sitio.
        string rutaAsset = s.navMeshData != null ? AssetDatabase.GetAssetPath(s.navMeshData) : null;

        try
        {
            s.BuildNavMesh();
        }
        finally
        {
            foreach (var c in disparadores) if (c != null) c.enabled = true;
        }

        GuardarComoAsset(s, rutaAsset);
        EditorUtility.SetDirty(s);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene);

        Debug.Log($"[NavMesh] Rehorneada la superficie '{s.name}' de '{s.gameObject.scene.name}' " +
                  $"(agente {s.agentTypeID}, área {s.defaultArea}).", s);
    }

    /// El SUELO disfrazado de obstáculo (INC-399). La causa de fondo de «los NPCs en fila».
    ///
    /// `NavMeshAutoSetup` le pone un NavMeshObstacle con «Carve» a todo collider sólido que no
    /// esté en la capa Floor. El terreno nuevo del valle (`TERRENO_Cuenca_Quibli`) y la colina
    /// (`Montana_Prologo`) se crean en Default, así que recibían uno: una caja de 360 × 360 m que
    /// TALLA el NavMesh de todo el valle en Play, y que además hace que el horneado los ignore
    /// («Ignore NavMesh Obstacles»). Lo único que sobrevivía era el camino de tierra, que sí está
    /// en Floor. Cualquier cosa que se pegara al NavMesh acababa en su borde, y los NPCs solo
    /// podían andar por él.
    ///
    /// Un collider de malla que ocupa decenas de metros no es un mueble: es suelo. Pasa a Floor y
    /// pierde el obstáculo (y NavMeshAutoSetup ya no se lo vuelve a poner, porque Floor está
    /// excluido).
    public static int ArreglarSuelosConObstaculo(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return 0;
        int suelo = LayerMask.NameToLayer("Floor");
        if (suelo < 0) return 0;

        int arreglados = 0;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var obs in raiz.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                var mc = obs.GetComponent<MeshCollider>();
                if (mc == null || mc.convex || mc.sharedMesh == null) continue;

                Vector3 tam = mc.sharedMesh.bounds.size;
                tam.Scale(obs.transform.lossyScale);
                if (Mathf.Max(Mathf.Abs(tam.x), Mathf.Abs(tam.z)) < 25f) continue;

                var go = obs.gameObject;
                Undo.RecordObject(go, "Terreno a Floor");
                go.layer = suelo;
                Undo.DestroyObjectImmediate(obs);
                EditorUtility.SetDirty(go);
                EditorSceneManager.MarkSceneDirty(escena);
                arreglados++;
                Debug.Log($"[NavMesh] '{go.name}' es suelo ({tam.x:F0} × {tam.z:F0} m), no un obstáculo: " +
                          "pasa a la capa Floor y se le quita el NavMeshObstacle que tallaba el valle.", go);
            }
        return arreglados;
    }

    [MenuItem("El Sendero/Mundo: rehornear el NavMesh de las escenas abiertas")]
    public static void Menu()
    {
        var superficies = new List<NavMeshSurface>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (var raiz in escena.GetRootGameObjects())
                superficies.AddRange(raiz.GetComponentsInChildren<NavMeshSurface>(true));
        }

        if (superficies.Count == 0)
        {
            EditorUtility.DisplayDialog("NavMesh",
                "No hay ninguna NavMeshSurface en las escenas abiertas. Abre Prologo_Valle (y " +
                "MainWorld si quieres las dos) y vuelve a darle.", "Vale");
            return;
        }

        int hechas = 0;
        foreach (var s in superficies)
        {
            if (s == null) continue;
            EditorUtility.DisplayProgressBar("NavMesh",
                $"Horneando '{s.gameObject.scene.name}/{s.name}'…", hechas / (float)superficies.Count);
            Hornear(s);
            hechas++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("NavMesh",
            $"{hechas} superficie(s) rehorneadas. Guarda las escenas con Ctrl+S.\n\n" +
            "Con el NavMesh al día, los NPCs rodean la carreta y a la gente en vez de atravesarlas " +
            "o subírseles encima. En el valle se hornea con los colliders: la hierba ya no cuenta " +
            "como escalón, así que dejan de ir en fila por el camino y de dar saltitos.", "Vale");
    }
}
