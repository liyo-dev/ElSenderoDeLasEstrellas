using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Saca del cuerpo de Will lo que es del grupo y lo pasa a su propio prefab,
/// `GrupoDelJugador.prefab`: Inventory, WardrobeInventory, DuoSpecialAttackSystem y las dos
/// cargas (SpecialMeters). Lo coloca en cada escena que usaba los de `_WILL`. Ver INC-484.
///
/// Pasos: (1) crea el prefab del grupo copiando esos componentes (con sus valores); (2) en cada
/// escena donde la instancia de `_WILL` los tiene (no en los menús, que se los quitan) pone un
/// `GrupoDelJugador` al lado; (3) los quita de `_WILL.prefab`. Se puede repetir: si el cuerpo ya
/// no tiene Inventory, no hace nada.
public static class SepararGrupoDelJugador
{
    private const string RutaJugador = "Assets/Prefabs/_WILL.prefab";
    private const string RutaGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    private const string NombreCuerpo = "vBasicController_MaleCharacterPBR";
    private const string NombreCargas = "SpecialMeters";

    [MenuItem("El Sendero/Archivo/Jugador: sacar del cuerpo de Will lo que es del grupo")]
    public static void Ejecutar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var montaje = EditorSceneManager.GetSceneManagerSetup();

        if (!CrearPrefabDelGrupo()) return;

        var informe = new List<string>();
        try
        {
            ColocarEnEscenas(informe);
            QuitarDelCuerpo();
        }
        finally
        {
            if (montaje != null && montaje.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(montaje);
        }

        Debug.Log("[SepararGrupoDelJugador] ✓ Hecho.\n" + string.Join("\n", informe));
    }

    // (1) Devuelve false si no hay nada que hacer.
    private static bool CrearPrefabDelGrupo()
    {
        var contenido = PrefabUtility.LoadPrefabContents(RutaJugador);
        try
        {
            var cuerpo = contenido.transform.Find(NombreCuerpo);
            if (cuerpo == null || cuerpo.GetComponent<Inventory>() == null)
            {
                Debug.Log("[SepararGrupoDelJugador] El cuerpo de _WILL ya no lleva Inventory: nada que hacer.");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaGrupo) != null) return true;

            var grupo = new GameObject("GrupoDelJugador");
            grupo.transform.SetParent(contenido.transform, false);   // lo mete en la escena del prefab
            grupo.transform.SetParent(null, false);
            grupo.AddComponent<GrupoDelJugador>();

            Copiar<Inventory>(cuerpo.gameObject, grupo);
            var vestuario = Copiar<WardrobeInventory>(cuerpo.gameObject, grupo);
            var duo = Copiar<DuoSpecialAttackSystem>(cuerpo.gameObject, grupo);

            // Referencias al cuerpo: no pueden cruzar de prefab; se resuelven en juego.
            Vaciar(vestuario, "builder");
            Vaciar(duo, "magicCaster");

            var cargas = contenido.transform.Find(NombreCargas);
            if (cargas != null) cargas.SetParent(grupo.transform, false);

            PrefabUtility.SaveAsPrefabAsset(grupo, RutaGrupo, out bool ok);
            if (!ok)
            {
                Debug.LogError($"[SepararGrupoDelJugador] No se pudo guardar {RutaGrupo}.");
                return false;
            }
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    private static T Copiar<T>(GameObject origen, GameObject destino) where T : Component
    {
        var original = origen.GetComponent<T>();
        if (original == null) return null;
        UnityEditorInternal.ComponentUtility.CopyComponent(original);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(destino);
        var copia = destino.GetComponent<T>();
        if (original is Behaviour b && copia is Behaviour c) c.enabled = b.enabled;
        return copia;
    }

    private static void Vaciar(Component componente, string campo)
    {
        if (componente == null) return;
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null) return;
        p.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // (2)
    private static void ColocarEnEscenas(List<string> informe)
    {
        var jugadorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador);
        var grupoAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RutaGrupo);

        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.Contains("/Backups/")) continue;
            if (!AssetDatabase.GetDependencies(ruta, false).Contains(RutaJugador)) continue;

            var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

            bool yaTiene = todos.Any(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                                          && PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject) == grupoAsset);
            var jugadores = todos.Select(t => t.gameObject)
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go)
                             && PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) == jugadorAsset)
                .ToList();

            int puestos = 0;
            foreach (var jugador in jugadores)
            {
                var cuerpo = jugador.transform.Find(NombreCuerpo);
                if (cuerpo == null || cuerpo.GetComponent<Inventory>() == null) continue;   // menús: quitado
                if (yaTiene) continue;

                var nuevo = (GameObject)PrefabUtility.InstantiatePrefab(grupoAsset, escena);
                nuevo.transform.SetParent(jugador.transform.parent, false);
                nuevo.transform.SetSiblingIndex(jugador.transform.GetSiblingIndex() + 1);
                puestos++;

                var ajustes = PrefabUtility.GetObjectOverrides(jugador, false)
                    .Where(o => o.instanceObject is Inventory || o.instanceObject is WardrobeInventory
                                || o.instanceObject is DuoSpecialAttackSystem || o.instanceObject is SpecialChargeMeter)
                    .Select(o => o.instanceObject.GetType().Name).Distinct().ToList();
                if (ajustes.Count > 0)
                    informe.Add($"  ⚠ {ruta}: ajustes de escena en {string.Join(", ", ajustes)} — revisar en GrupoDelJugador.");
            }

            if (puestos > 0)
            {
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                informe.Add($"  · {ruta}: grupo puesto.");
            }
            else
            {
                informe.Add($"  · {ruta}: sin cambios (menú, o ya tenía GrupoDelJugador).");
            }
        }
    }

    // (3)
    private static void QuitarDelCuerpo()
    {
        var contenido = PrefabUtility.LoadPrefabContents(RutaJugador);
        try
        {
            var cuerpo = contenido.transform.Find(NombreCuerpo);
            if (cuerpo == null) return;

            foreach (var recoger in cuerpo.GetComponents<PlayerPickupCollector>())
                Vaciar(recoger, "inventory");   // en juego lo busca en el grupo

            Quitar<DuoSpecialAttackSystem>(cuerpo.gameObject);
            Quitar<WardrobeInventory>(cuerpo.gameObject);
            Quitar<Inventory>(cuerpo.gameObject);

            var cargas = contenido.transform.Find(NombreCargas);
            if (cargas != null) Object.DestroyImmediate(cargas.gameObject);

            PrefabUtility.SaveAsPrefabAsset(contenido, RutaJugador);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    private static void Quitar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) Object.DestroyImmediate(c);
    }
}
