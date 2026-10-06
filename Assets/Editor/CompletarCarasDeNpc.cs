using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Completa las mallas Eye/Mouth de los personajes desde el donante modular.
/// Las añade apagadas bajo el mismo padre y con la pose y materiales del ancla,
/// para permitir expresiones y habla sin cambiar la cara visible del prefab.
/// El contenido cargado incluye las mallas heredadas de variantes y evita duplicarlas.
public static class CompletarCarasDeNpc
{
    private const string RutaDonante = "Assets/Prefabs/_WILL_ORIGINAL.prefab";
    private const string RutaRoster = "Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset";
    private static readonly Regex Ojo = new(@"^Eye\d+$");
    private static readonly Regex Boca = new(@"^Mouth\d+$");

    [MenuItem("El Sendero/Archivo/Prólogo: completar las caras de los NPCs (emociones)", priority = 34)]
    public static void Menu()
    {
        int n = EjecutarRosterDelPrologo();
        EditorUtility.DisplayDialog("Caras",
            n > 0 ? $"Añadidas {n} malla(s) de ojos/boca que faltaban. Ya pueden cambiar de cara."
                  : "Todos los NPCs del prólogo tenían ya todas sus caras.", "Vale");
    }

    /// Completa las caras de todos los prefabs del roster del prólogo. Devuelve cuántas mallas añade.
    public static int EjecutarRosterDelPrologo()
    {
        var roster = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RutaRoster);
        if (roster == null)
        {
            Debug.LogWarning($"[Caras] No encuentro el roster '{RutaRoster}'.");
            return 0;
        }

        var rutas = new HashSet<string>();
        var so = new SerializedObject(roster);
        var it = so.GetIterator();
        while (it.Next(true))
        {
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.name == "prefab" &&
                it.objectReferenceValue is GameObject go)
                rutas.Add(AssetDatabase.GetAssetPath(go));
        }

        int total = 0;
        foreach (var ruta in rutas) total += Completar(ruta);
        return total;
    }

    private const string MenuCompletar = "El Sendero/Diálogos/Completar caras de todos los personajes";
    private static int _prefabsCompletados;

    [MenuItem(MenuCompletar)]
    public static void MenuTodos()
    {
        int total = CompletarTodos();
        EditorUtility.DisplayDialog("Caras de todos los personajes",
            $"Añadidas {total} mallas de ojos/boca en {_prefabsCompletados} prefabs.", "Vale");
    }

    /// Completa los prefabs de personajes editables bajo Assets y devuelve las mallas añadidas.
    public static int CompletarTodos()
    {
        int total = 0;
        _prefabsCompletados = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta == RutaDonante || ruta.IndexOf("Versiones antiguas", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null || prefab.GetComponentInChildren<NPCEmotionController>(true) == null) continue;
            try
            {
                int añadidas = Completar(ruta);
                total += añadidas;
                if (añadidas > 0) _prefabsCompletados++;
            }
            catch (System.Exception error)
            {
                Debug.LogWarning($"[Caras] No se puede completar '{ruta}': {error.Message}");
            }
        }
        return total;
    }

    private static bool EsEditable(string ruta)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        return ruta.StartsWith("Assets/", System.StringComparison.Ordinal)
            && prefab != null && PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Model
            && !PrefabUtility.IsPartOfImmutablePrefab(prefab) && AssetDatabase.IsOpenForEdit(ruta);
    }

    public static int Completar(string rutaPrefab)
    {
        if (string.IsNullOrEmpty(rutaPrefab) || rutaPrefab == RutaDonante) return 0;

        if (!EsEditable(rutaPrefab))
        {
            Debug.LogWarning($"[Caras] '{rutaPrefab}' no es un prefab editable; se omite.");
            return 0;
        }
        GameObject donante = null, raiz = null;
        int añadidas = 0;
        try
        {
            donante = PrefabUtility.LoadPrefabContents(RutaDonante);
            raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
            if (raiz.GetComponentInChildren<NPCEmotionController>(true) == null) return 0;

            añadidas += CompletarGrupo(raiz, donante, Ojo, rutaPrefab);
            añadidas += CompletarGrupo(raiz, donante, Boca, rutaPrefab);

            if (añadidas > 0)
            {
                if (!EsEditable(rutaPrefab)) throw new System.InvalidOperationException("El prefab deja de ser editable; no se guarda.");
                EditorUtility.SetDirty(raiz);
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab, out bool guardado);
                if (!guardado) throw new System.InvalidOperationException("No se han podido guardar las caras.");
                AssetDatabase.SaveAssets();
                Debug.Log($"[Caras] '{rutaPrefab}': {añadidas} malla(s) de cara añadidas (apagadas).");
            }
        }
        finally
        {
            if (raiz != null) PrefabUtility.UnloadPrefabContents(raiz);
            if (donante != null) PrefabUtility.UnloadPrefabContents(donante);
        }
        return añadidas;
    }

    private static int CompletarGrupo(GameObject raiz, GameObject donante, Regex patron, string ruta)
    {
        // La que ya tiene el personaje: de ella salen el padre, la pose, la capa y el material.
        Transform ancla = null;
        var existentes = new HashSet<string>();
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (!patron.IsMatch(t.name)) continue;
            existentes.Add(t.name);
            if (t.GetComponent<MeshFilter>() == null && t.GetComponent<SkinnedMeshRenderer>() == null) continue;
            if (ancla == null || t.gameObject.activeSelf) ancla = t;
        }
        if (ancla == null)
        {
            Debug.LogWarning($"[Caras] '{ruta}' no tiene ninguna malla '{patron}' de la que colgar las demás.");
            return 0;
        }
        var rendAncla = ancla.GetComponent<Renderer>();
        var pielAncla = ancla.GetComponent<SkinnedMeshRenderer>();

        int n = 0;
        foreach (var d in donante.GetComponentsInChildren<Transform>(true))
        {
            if (!patron.IsMatch(d.name) || existentes.Contains(d.name)) continue;
            var filtro = d.GetComponent<MeshFilter>();
            var rend = d.GetComponent<Renderer>();
            var pielDonante = d.GetComponent<SkinnedMeshRenderer>();
            var malla = filtro != null ? filtro.sharedMesh : pielDonante != null ? pielDonante.sharedMesh : null;
            if (malla == null) continue;
            if (pielAncla != null && malla.bindposes.Length != pielAncla.bones.Length)
            {
                Debug.LogWarning($"[Caras] '{ruta}': no se copia '{d.name}': los huesos del ancla SkinnedMeshRenderer no coinciden con las bindposes del donante. Requiere revisar el rig.");
                continue;
            }

            var nueva = new GameObject(d.name);
            nueva.layer = ancla.gameObject.layer;
            nueva.transform.SetParent(ancla.parent, false);
            nueva.transform.localPosition = ancla.localPosition;
            nueva.transform.localRotation = ancla.localRotation;
            nueva.transform.localScale = ancla.localScale;
            Renderer r;
            if (pielAncla != null)
            {
                var piel = nueva.AddComponent<SkinnedMeshRenderer>();
                piel.sharedMesh = malla;
                piel.bones = pielAncla.bones;
                piel.rootBone = pielAncla.rootBone;
                piel.localBounds = pielAncla.localBounds;
                piel.updateWhenOffscreen = pielAncla.updateWhenOffscreen;
                piel.quality = pielAncla.quality;
                r = piel;
            }
            else
            {
                nueva.AddComponent<MeshFilter>().sharedMesh = malla;
                r = nueva.AddComponent<MeshRenderer>();
            }
            r.sharedMaterials = rendAncla != null ? rendAncla.sharedMaterials
                              : rend != null ? rend.sharedMaterials : new Material[0];
            if (rendAncla != null)
            {
                r.shadowCastingMode = rendAncla.shadowCastingMode;
                r.receiveShadows = rendAncla.receiveShadows;
            }
            nueva.SetActive(false);
            existentes.Add(d.name);
            n++;
        }
        return n;
    }
}
