using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Repunta las instancias de prefab cuyo asset ya no existe (INC-1271): Unity las deja como
/// «Missing Prefab» y al compilar se pierden, así que la escena se queda sin esos objetos. Aquí se
/// cambian por el prefab actual del mismo personaje, conservando nombre, padre, sitio, escala y
/// estado, y se reapuntan las referencias de la escena a la instancia nueva.
/// Menú: «El Sendero/Archivo/Escenas: repuntar los prefabs perdidos (INC-1271)».
/// </summary>
public static class RepararPrefabsPerdidos
{
    // Con lo que termina el nombre de la instancia perdida → prefab de ahora.
    static readonly (string final, string ruta)[] Reemplazos =
    {
        ("WILL", "Assets/Prefabs/_WILL.prefab"),
        ("ESTELA", "Assets/Prefabs/_ESTELA.prefab"),
        ("LIAM", "Assets/Prefabs/_LIAM.prefab"),
        ("ELDRAN", "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab"),
    };

    static readonly string[] Escenas =
    {
        "Assets/Scenes/Worlds/Laberinto.unity",
        "Assets/Scenes/Test/BatallaFinal.unity",
        "Assets/Scenes/Test/Lab.unity",
        "Assets/Scenes/Test/CinematicaFinal_Preview.unity",
    };

    [MenuItem("El Sendero/Archivo/Escenas: repuntar los prefabs perdidos (INC-1271)")]
    public static void Ejecutar()
    {
        var informe = new StringBuilder();
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Prefabs perdidos", "Hazlo fuera de Play."); return; }
        try
        {
            foreach (var ruta in Escenas) RepararEscena(ruta, informe);
        }
        catch (System.Exception e) { informe.AppendLine("ERROR: " + e); }
        finally
        {
            Debug.Log("[RepararPrefabsPerdidos] " + informe.ToString().Replace('\n', ' '));
            OrdenesDelEditor.Avisar("Prefabs perdidos", informe.ToString());
        }
    }

    static void RepararEscena(string ruta, StringBuilder informe)
    {
        var escena = SceneManager.GetSceneByPath(ruta);
        bool abierta = escena.IsValid() && escena.isLoaded;
        if (abierta && escena.isDirty)
        {
            informe.AppendLine($"{ruta}: tiene cambios sin guardar; no se toca.");
            return;
        }
        if (!abierta) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);

        var raices = escena.GetRootGameObjects();
        var perdidas = new List<GameObject>();
        foreach (var raiz in raices)
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.MissingAsset) continue;
                bool dentroDeOtra = false;
                for (var p = t.parent; p != null; p = p.parent)
                    if (PrefabUtility.GetPrefabInstanceStatus(p.gameObject) == PrefabInstanceStatus.MissingAsset)
                    { dentroDeOtra = true; break; }
                if (!dentroDeOtra) perdidas.Add(go);
            }

        var pares = new List<(Transform viejo, Transform nuevo)>();
        foreach (var viejo in perdidas)
        {
            string nombre = NombreLimpio(viejo.name);
            var prefab = PrefabDe(nombre);
            if (prefab == null) { informe.AppendLine($"{ruta}: «{nombre}» sin reemplazo conocido; se deja."); continue; }

            var padre = viejo.transform.parent;
            int hermano = viejo.transform.GetSiblingIndex();
            Vector3 pos = viejo.transform.position;
            Quaternion rot = viejo.transform.rotation;
            Vector3 escala = viejo.transform.localScale;
            bool activo = viejo.activeSelf;

            var nuevo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            nuevo.transform.SetParent(padre, true);
            nuevo.transform.SetPositionAndRotation(pos, rot);
            nuevo.transform.localScale = escala;
            nuevo.transform.SetSiblingIndex(hermano);
            nuevo.name = nombre;
            nuevo.SetActive(activo);

            pares.Add((viejo.transform, nuevo.transform));
            informe.AppendLine($"{ruta}: «{nombre}» → {prefab.name}.");
        }

        int referencias = 0;
        if (pares.Count > 0)
        {
            foreach (var raiz in raices)
            {
                if (raiz == null) continue;
                foreach (var comp in raiz.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || comp is Transform) continue;
                    bool propio = false;
                    foreach (var par in pares)
                        if (comp.transform.IsChildOf(par.viejo) || comp.transform.IsChildOf(par.nuevo))
                        { propio = true; break; }
                    if (propio) continue;
                    foreach (var par in pares) referencias += Reapuntar(comp, par.viejo, par.nuevo);
                }
            }
            foreach (var par in pares) Object.DestroyImmediate(par.viejo.gameObject);
        }

        if (pares.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        informe.AppendLine($"{ruta}: {pares.Count} sustituidas, {referencias} referencias recolocadas" +
                           (pares.Count > 0 ? ", guardada." : "."));
        if (!abierta) EditorSceneManager.CloseScene(escena, true);
    }

    static GameObject PrefabDe(string nombre)
    {
        string n = nombre.ToUpperInvariant();
        foreach (var (final, ruta) in Reemplazos)
            if (n.EndsWith(final, System.StringComparison.Ordinal))
                return AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        return null;
    }

    // Unity renombra la instancia perdida como «_WILL (Missing Prefab with guid: …)»: el nombre
    // de verdad es lo de antes del paréntesis.
    static string NombreLimpio(string nombre)
    {
        int i = nombre.IndexOf(" (Missing Prefab", System.StringComparison.Ordinal);
        return i > 0 ? nombre.Substring(0, i) : nombre;
    }

    // Una referencia de la escena a algo de la instancia vieja pasa a lo equivalente de la nueva.
    static int Reapuntar(Component comp, Transform viejo, Transform nuevo)
    {
        int cambios = 0;
        var so = new SerializedObject(comp);
        var it = so.GetIterator();
        while (it.NextVisible(true))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
            var valor = it.objectReferenceValue;
            if (valor == null) continue;
            Transform t = valor is GameObject g ? g.transform : (valor as Component)?.transform;
            if (t == null || !t.IsChildOf(viejo)) continue;

            var destino = Equivalente(t, viejo, nuevo);
            if (destino == null) continue;

            Object reemplazo;
            if (valor is GameObject) reemplazo = destino.gameObject;
            else
            {
                var tipo = valor.GetType();
                reemplazo = null;
                foreach (var c in destino.GetComponents<Component>())
                    if (c != null && c.GetType() == tipo) { reemplazo = c; break; }
            }
            if (reemplazo == null) continue;
            it.objectReferenceValue = reemplazo;
            cambios++;
        }
        if (cambios > 0) so.ApplyModifiedPropertiesWithoutUndo();
        return cambios;
    }

    // El mismo objeto dentro del prefab nuevo (por su ruta en la jerarquía); si esa pieza no
    // existe en el cuerpo nuevo, se devuelve el cuerpo, que es donde vive lo que era del personaje.
    static Transform Equivalente(Transform t, Transform viejo, Transform nuevo)
    {
        var partes = new List<string>();
        bool dentro = false;
        for (var x = t; x != null; x = x.parent)
        {
            if (x == viejo) { dentro = true; break; }
            partes.Add(x.name);
        }
        if (!dentro) return null;
        if (partes.Count == 0) return nuevo;
        partes.Reverse();
        var r = nuevo;
        foreach (var p in partes)
        {
            var h = r.Find(p);
            if (h == null) return nuevo;
            r = h;
        }
        return r;
    }
}
// Reparación de un solo uso (INC-1271).
