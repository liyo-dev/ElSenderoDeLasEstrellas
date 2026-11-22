using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Da a cada farol de guardado de MainWorld su propia ancla (hija, delante del farol y apoyada en
/// el suelo) y la clave del lugar de la zona en la que está, y pone el farol nuevo de la plaza de
/// la Aldea del Mar copiando uno existente. Menú de un solo uso (INC-1400).
/// </summary>
public static class FarolesDeGuardadoINC1400
{
    const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    const string AnclaAldea = "Farol_AldeaDelMar";
    const string LugarAldea = "LUGAR_ALDEA_DEL_MAR";
    const float DelanteDelFarol = 1.8f;

    [MenuItem("El Sendero/Escenario/MainWorld: faroles con ancla y lugar propios (INC-1400)")]
    static void Ejecutar()
    {
        var escena = SceneManager.GetSceneByPath(RutaMainWorld);
        bool abiertaAqui = false;
        if (!escena.isLoaded)
        {
            escena = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);
            abiertaAqui = true;
        }

        var informe = new StringBuilder("[Faroles] ");
        var faroles = new List<SavePoint>();
        foreach (var raiz in escena.GetRootGameObjects())
            faroles.AddRange(raiz.GetComponentsInChildren<SavePoint>(true));

        var zonas = new List<AmbientZone>();
        foreach (var raiz in escena.GetRootGameObjects())
            zonas.AddRange(raiz.GetComponentsInChildren<AmbientZone>(true));

        var anclasDeEscena = new HashSet<string>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var a in raiz.GetComponentsInChildren<SpawnAnchor>(true))
                if (!string.IsNullOrEmpty(a.anchorId)) anclasDeEscena.Add(a.anchorId);

        bool hayAldea = false;
        foreach (var f in faroles)
            if (f.anchorIdToSet == AnclaAldea) hayAldea = true;

        if (!hayAldea && faroles.Count > 0)
        {
            var nuevo = PonerFarolEnLaAldea(escena, faroles[0], informe);
            if (nuevo != null) faroles.Add(nuevo);
        }

        foreach (var f in faroles)
            AsegurarAnclaYLugar(f, zonas, anclasDeEscena, informe);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        Debug.Log(informe.ToString());
    }

    static SavePoint PonerFarolEnLaAldea(Scene escena, SavePoint plantilla, StringBuilder informe)
    {
        // Plaza junto a la posada: al lado del ancla de la posada del capítulo 3.
        Transform posada = null;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var a in raiz.GetComponentsInChildren<SpawnAnchor>(true))
                if (a.anchorId == "Cap3_Posada") posada = a.transform;
        Vector3 punto = posada != null ? posada.position + posada.right * 3.5f : new Vector3(270f, 0f, -433f);

        var raizPlantilla = RaizDelFarol(plantilla);
        var copia = Object.Instantiate(raizPlantilla, raizPlantilla.parent);
        copia.name = "Farol de guardado · Aldea del Mar";
        Undo.RegisterCreatedObjectUndo(copia.gameObject, "Farol Aldea del Mar");
        copia.position = ApoyarEnElSuelo(punto, copia);
        if (posada != null) copia.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(-posada.right, Vector3.up), Vector3.up);

        // La copia no se queda con las anclas de la plantilla.
        foreach (var a in copia.GetComponentsInChildren<SpawnAnchor>(true))
            Object.DestroyImmediate(a.gameObject == copia.gameObject ? (Object)a : a.gameObject);

        var farol = copia.GetComponentInChildren<SavePoint>(true);
        farol.anchorIdToSet = AnclaAldea;
        var so = new SerializedObject(farol);
        so.FindProperty("claveDelLugar").stringValue = LugarAldea;
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.Append($"Farol nuevo en la Aldea del Mar en {copia.position} (copia de '{raizPlantilla.name}'). ");
        return farol;
    }

    // El farol entero: el objeto más alto por encima del SavePoint que no es un contenedor de la escena.
    static Transform RaizDelFarol(SavePoint farol)
    {
        var raizPrefab = PrefabUtility.GetOutermostPrefabInstanceRoot(farol.gameObject);
        if (raizPrefab != null) return raizPrefab.transform;
        var t = farol.transform;
        return t.parent != null && t.parent.childCount <= 6 && t.parent.parent != null ? t.parent : t;
    }

    static void AsegurarAnclaYLugar(SavePoint farol, List<AmbientZone> zonas, HashSet<string> anclasDeEscena, StringBuilder informe)
    {
        var propia = farol.GetComponentInChildren<SpawnAnchor>(true);
        string id = farol.anchorIdToSet;
        if (propia == null)
        {
            if (string.IsNullOrEmpty(id) || anclasDeEscena.Contains(id)) id = $"Farol_{Mathf.RoundToInt(farol.transform.position.x)}_{Mathf.RoundToInt(farol.transform.position.z)}";
            var go = new GameObject("Ancla del farol · " + id);
            Undo.RegisterCreatedObjectUndo(go, "Ancla del farol");
            go.transform.SetParent(farol.transform, false);
            var adelante = Vector3.ProjectOnPlane(farol.transform.forward, Vector3.up);
            if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
            adelante.Normalize();
            go.transform.position = ApoyarEnElSuelo(farol.transform.position + adelante * DelanteDelFarol, farol.transform);
            go.transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
            propia = go.AddComponent<SpawnAnchor>();
            propia.anchorId = id;
            anclasDeEscena.Add(id);
        }
        farol.anchorIdToSet = propia.anchorId;

        var so = new SerializedObject(farol);
        var lugar = so.FindProperty("claveDelLugar");
        if (string.IsNullOrEmpty(lugar.stringValue)) lugar.stringValue = LugarDeLaZona(farol.transform.position, zonas);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(farol);
        informe.Append($"'{farol.transform.parent?.name}/{farol.name}': ancla {propia.anchorId}, lugar «{lugar.stringValue}». ");
    }

    static string LugarDeLaZona(Vector3 punto, List<AmbientZone> zonas)
    {
        string mejor = "";
        int prioridad = int.MinValue;
        foreach (var z in zonas)
        {
            if (string.IsNullOrEmpty(z.NombreDelLugar)) continue;
            var col = z.GetComponent<Collider>();
            if (col == null || !col.bounds.Contains(punto)) continue;
            int p = new SerializedObject(z).FindProperty("priority").intValue;
            if (p > prioridad) { prioridad = p; mejor = z.NombreDelLugar; }
        }
        return mejor;
    }

    // Rayo desde poco por encima (para no dar en tejados) que ignora el propio farol: el punto queda en el primer suelo debajo.
    static Vector3 ApoyarEnElSuelo(Vector3 punto, Transform ignorar)
    {
        Physics.SyncTransforms();
        var golpes = Physics.RaycastAll(punto + Vector3.up * 2.5f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        Vector3 suelo = punto;
        foreach (var g in golpes)
        {
            if (ignorar != null && g.transform.IsChildOf(ignorar)) continue;
            if (g.distance < mejor) { mejor = g.distance; suelo = g.point; }
        }
        if (mejor == float.MaxValue)
        {
            var terreno = Terrain.activeTerrain;
            if (terreno != null) suelo.y = terreno.SampleHeight(punto) + terreno.transform.position.y;
        }
        return suelo;
    }
}
