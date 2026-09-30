using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Saca la cámara de juego (vThirdPersonCamera con su Hint Camera) de `_WILL.prefab` a su propio
/// prefab, `CamaraDelJugador.prefab`, y la coloca en cada escena que usaba la de `_WILL`.
///
/// La cámara no es parte de ningún personaje: sigue a quien tenga el mando. Dentro de `_WILL`
/// descuadraba el gizmo de la escena (a ~47 m del personaje) y viajaba con cada copia del
/// prefab. Ver INC-482.
///
/// Pasos: (1) crea el prefab de la cámara a partir del de `_WILL`; (2) en cada escena donde la
/// instancia de `_WILL` tiene la cámara activa (no en los menús, que se la quitan) pone una
/// `CamaraDelJugador` al lado, en la misma pose; (3) quita la cámara de `_WILL.prefab`.
/// Se puede repetir: si `_WILL` ya no lleva cámara, no hace nada.
public static class SepararCamaraDelJugador
{
    private const string RutaJugador = "Assets/Prefabs/_WILL.prefab";
    private const string RutaCamara = "Assets/Prefabs/CamaraDelJugador.prefab";
    private const string NombreCamara = "vThirdPersonCamera";

    [MenuItem("El Sendero/Archivo/Jugador: sacar la cámara de _WILL a su propio prefab")]
    public static void Ejecutar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var montaje = EditorSceneManager.GetSceneManagerSetup();

        if (!CrearPrefabDeCamara()) return;

        var informe = new List<string>();
        try
        {
            ColocarEnEscenas(informe);
            QuitarCamaraDelJugador();
        }
        finally
        {
            if (montaje != null && montaje.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(montaje);
        }

        Debug.Log("[SepararCamaraDelJugador] ✓ Hecho.\n" + string.Join("\n", informe));
    }

    // (1) Devuelve false si no hay nada que hacer.
    private static bool CrearPrefabDeCamara()
    {
        var contenido = PrefabUtility.LoadPrefabContents(RutaJugador);
        try
        {
            var camara = contenido.transform.Find(NombreCamara);
            if (camara == null)
            {
                Debug.Log("[SepararCamaraDelJugador] _WILL ya no lleva cámara: nada que hacer.");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaCamara) != null) return true;

            camara.SetParent(null, false);
            camara.localPosition = Vector3.zero;
            camara.localRotation = Quaternion.identity;
            // Las referencias al cuerpo no pueden cruzar de prefab: se resuelven en juego.
            foreach (var cct in camara.GetComponentsInChildren<CombatCameraTargeting>(true))
            {
                var so = new SerializedObject(cct);
                so.FindProperty("playerTransform").objectReferenceValue = null;
                so.FindProperty("playerTargeting").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(camara.gameObject, RutaCamara, out bool ok);
            if (!ok)
            {
                Debug.LogError($"[SepararCamaraDelJugador] No se pudo guardar {RutaCamara}.");
                return false;
            }
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    // (2)
    private static void ColocarEnEscenas(List<string> informe)
    {
        var jugadorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador);
        var camaraAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCamara);

        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.Contains("/Backups/")) continue;
            if (!AssetDatabase.GetDependencies(ruta, false).Contains(RutaJugador)) continue;

            var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            var raices = escena.GetRootGameObjects();

            bool yaTiene = raices.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Any(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                          && PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject) == camaraAsset);

            var jugadores = raices.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go)
                             && PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) == jugadorAsset)
                .ToList();

            int puestas = 0;
            foreach (var jugador in jugadores)
            {
                var vieja = jugador.transform.Find(NombreCamara);
                if (vieja == null || vieja.GetComponent<Camera>() == null) continue;   // menús: cámara quitada
                if (yaTiene) continue;

                var nueva = (GameObject)PrefabUtility.InstantiatePrefab(camaraAsset, escena);
                nueva.transform.SetParent(jugador.transform.parent, false);
                nueva.transform.SetSiblingIndex(jugador.transform.GetSiblingIndex() + 1);
                nueva.transform.SetPositionAndRotation(vieja.position, vieja.rotation);
                nueva.SetActive(vieja.gameObject.activeSelf);
                puestas++;

                // Ajustes hechos en la escena sobre la cámara vieja: no se copian a ciegas (llevan
                // referencias a objetos de _WILL); se avisan para revisarlos a mano.
                var ajustes = PrefabUtility.GetObjectOverrides(jugador, false)
                    .Where(o => o.instanceObject is Component c && c != null && c.transform.IsChildOf(vieja))
                    .Select(o => o.instanceObject.GetType().Name)
                    .Distinct().ToList();
                if (ajustes.Count > 0)
                    informe.Add($"  ⚠ {ruta}: la cámara vieja tenía ajustes de escena en {string.Join(", ", ajustes)} — revisar en la nueva.");
            }

            if (puestas > 0)
            {
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                informe.Add($"  · {ruta}: cámara puesta.");
            }
            else
            {
                informe.Add($"  · {ruta}: sin cambios (menú, o ya tenía CamaraDelJugador).");
            }
        }
    }

    // (3)
    private static void QuitarCamaraDelJugador()
    {
        var contenido = PrefabUtility.LoadPrefabContents(RutaJugador);
        try
        {
            var camara = contenido.transform.Find(NombreCamara);
            if (camara == null) return;

            foreach (var pt in contenido.GetComponentsInChildren<PlayerTargeting>(true))
            {
                var so = new SerializedObject(pt);
                so.FindProperty("aimOrigin").objectReferenceValue = null;   // en juego: Camera.main
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(camara.gameObject);
            PrefabUtility.SaveAsPrefabAsset(contenido, RutaJugador);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
