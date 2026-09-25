using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Las caras que no podían cambiar (INC-404).
///
/// ── Qué pasó ──────────────────────────────────────────────────────────────────────────────────
/// «Liora está contenta todo el rato y no cambia la cara.» Las expresiones las pone
/// `NPCEmotionController` ENCENDIENDO una malla de ojos y otra de boca por su nombre (Eye10 +
/// Mouth08 es «preocupada», Eye09 + Mouth08 «asustada»...). El prefab de Liora (`Tendera`) solo
/// trae UNA de cada: Eye07 y Mouth09, que es la cara feliz. Pedirle cualquier otra emoción no
/// hacía nada — sin aviso, porque el controlador solo avisa en modo debug. Los aldeanos
/// (`TownNpc#N`) están igual: una cara fija cada uno.
///
/// ── Arreglo ───────────────────────────────────────────────────────────────────────────────────
/// Las mallas de ojos y bocas son del mismo pack modular para todos (el mismo asset de Mesh, la
/// misma pose bajo el hueso de la cabeza). Se copian las que falten desde un donante que las tiene
/// todas (`_WILL_ORIGINAL`, el Archimago), APAGADAS, junto a las que ya tiene el personaje, en la
/// misma pose y con su mismo material. El personaje se ve igual que antes hasta que una emoción
/// pida otra cara.
///
/// Idempotente: si ya están, no toca nada.
public static class CompletarCarasDeNpc
{
    private const string RutaDonante = "Assets/Prefabs/_WILL_ORIGINAL.prefab";
    private const string RutaRoster = "Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset";
    private static readonly Regex Ojo = new(@"^Eye\d+$");
    private static readonly Regex Boca = new(@"^Mouth\d+$");

    [MenuItem("El Sendero/Prólogo: completar las caras de los NPCs (emociones)", priority = 34)]
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

    public static int Completar(string rutaPrefab)
    {
        if (string.IsNullOrEmpty(rutaPrefab) || rutaPrefab == RutaDonante) return 0;

        var donante = PrefabUtility.LoadPrefabContents(RutaDonante);
        var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        int añadidas = 0;
        try
        {
            if (raiz.GetComponentInChildren<NPCEmotionController>(true) == null) return 0;

            añadidas += CompletarGrupo(raiz, donante, Ojo, rutaPrefab);
            añadidas += CompletarGrupo(raiz, donante, Boca, rutaPrefab);

            if (añadidas > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                Debug.Log($"[Caras] '{rutaPrefab}': {añadidas} malla(s) de cara añadidas (apagadas).");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
            PrefabUtility.UnloadPrefabContents(donante);
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
            if (!patron.IsMatch(t.name) || t.GetComponent<MeshFilter>() == null) continue;
            existentes.Add(t.name);
            if (ancla == null || t.gameObject.activeSelf) ancla = t;
        }
        if (ancla == null)
        {
            Debug.LogWarning($"[Caras] '{ruta}' no tiene ninguna malla '{patron}' de la que colgar las demás.");
            return 0;
        }
        var rendAncla = ancla.GetComponent<MeshRenderer>();

        int n = 0;
        foreach (var d in donante.GetComponentsInChildren<Transform>(true))
        {
            if (!patron.IsMatch(d.name) || existentes.Contains(d.name)) continue;
            var filtro = d.GetComponent<MeshFilter>();
            var rend = d.GetComponent<MeshRenderer>();
            if (filtro == null || filtro.sharedMesh == null) continue;

            var nueva = new GameObject(d.name);
            nueva.layer = ancla.gameObject.layer;
            nueva.transform.SetParent(ancla.parent, false);
            nueva.transform.localPosition = ancla.localPosition;
            nueva.transform.localRotation = ancla.localRotation;
            nueva.transform.localScale = ancla.localScale;
            nueva.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
            var r = nueva.AddComponent<MeshRenderer>();
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
