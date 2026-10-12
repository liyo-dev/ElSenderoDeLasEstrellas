using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Zona del LAB con los extras del Cap. 2 (INC-725): el puente sin magia sobre una zanja con sus
/// dos piezas, el plato que persigue con una silla para subirse y el campamento de los
/// contrabandistas (jaulas que arden, pluma y claro). Usa los prefabs de Cap2ExtrasPrefabs.
public static partial class CombatLabBuilder
{
    private static readonly Vector3 CentroCap2 = new Vector3(-20f, 0f, 110f);

    private static IEnumerable<string> CrearZonaCap2(Scene scene)
    {
        var avisos = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Cap2ExtrasPrefabs.Puente) == null) Cap2ExtrasPrefabs.Asegurar();

        var raiz = new GameObject("LAB_CAP2_EXTRAS");
        SceneManager.MoveGameObjectToScene(raiz, scene);
        var c = CentroCap2;
        var suelo = GetMaterial("Lab_Suelo", new Color(0.19f, 0.22f, 0.28f));
        var madera = GetMaterial("Lab_Cap2_Madera", new Color(0.45f, 0.31f, 0.18f));
        var fondo = GetMaterial("Lab_Cap2_Zanja", new Color(0.12f, 0.20f, 0.30f));

        // Zanja de 6 m a lo largo de X; el puente la cruza por el centro, a lo largo de Z.
        CrearCubo("Suelo sur", raiz.transform, c + new Vector3(0f, -0.25f, -9f), new Vector3(30f, 0.5f, 12f), suelo, "Floor");
        CrearCubo("Suelo norte", raiz.transform, c + new Vector3(0f, -0.25f, 9f), new Vector3(30f, 0.5f, 12f), suelo, "Floor");
        CrearCubo("Fondo de la zanja", raiz.transform, c + new Vector3(0f, -1.25f, 0f), new Vector3(30f, 0.5f, 6f), fondo, "Floor");
        CrearCubo("Escalón para salir de la zanja", raiz.transform, c + new Vector3(13f, -0.75f, 0f), new Vector3(3f, 0.5f, 6f), suelo, "Floor");
        float tramo = (6f - Cap2ExtrasPrefabs.Hueco) * 0.5f;
        float centroTramo = (Cap2ExtrasPrefabs.Hueco + tramo) * 0.5f;
        var anchoTablero = new Vector3(Cap2ExtrasPrefabs.AnchoPuente, 0.2f, tramo);
        CrearCubo("Tablero sur", raiz.transform, c + new Vector3(0f, -0.1f, -centroTramo), anchoTablero, madera, "Floor");
        CrearCubo("Tablero norte", raiz.transform, c + new Vector3(0f, -0.1f, centroTramo), anchoTablero, madera, "Floor");

        InstanciarCap2(Cap2ExtrasPrefabs.Puente, raiz.transform, c, scene, avisos);
        InstanciarCap2(Cap2ExtrasPrefabs.Cuerda, raiz.transform, c + new Vector3(-5f, 0f, -8f), scene, avisos);
        InstanciarCap2(Cap2ExtrasPrefabs.Clavos, raiz.transform, c + new Vector3(5f, 0f, -8f), scene, avisos);
        CrearTexto("Puente sin magia: coge la cuerda y los clavos y acércate al hueco", raiz.transform, c + new Vector3(0f, 3f, -5f));

        var plato = InstanciarCap2(Cap2ExtrasPrefabs.Plato, raiz.transform, c + new Vector3(-8f, 0f, 9f), scene, avisos);
        if (plato != null) plato.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var sillaAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Furniture/Chair/Chair01_a01.prefab");
        if (sillaAsset != null)
        {
            var silla = (GameObject)PrefabUtility.InstantiatePrefab(sillaAsset, scene);
            silla.name = "Silla para subirse";
            silla.transform.SetParent(raiz.transform);
            silla.transform.position = c + new Vector3(-11f, 0f, 11f);
            if (silla.GetComponentInChildren<Collider>() == null)
            {
                var b = new Bounds(silla.transform.position, Vector3.zero);
                foreach (var r in silla.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var caja = silla.AddComponent<BoxCollider>();
                caja.center = silla.transform.InverseTransformPoint(b.center);
                caja.size = new Vector3(b.size.x / silla.transform.lossyScale.x, b.size.y / silla.transform.lossyScale.y, b.size.z / silla.transform.lossyScale.z);
            }
        }
        else avisos.Add("Cap2: no se encontró la silla del pack; el plato se puede probar sin ella.");
        CrearTexto("Plato que persigue: esquívalo o súbete a la silla", raiz.transform, c + new Vector3(-9f, 3f, 6f));

        InstanciarCap2(Cap2ExtrasPrefabs.Campamento, raiz.transform, c + new Vector3(8f, 0f, 10f), scene, avisos);
        CrearTexto("Contrabandistas: la pluma, las jaulas (arden con fuego) y el claro", raiz.transform, c + new Vector3(8f, 3f, 5f));

        AnadirDestinoCap2("Extras del Cap. 2 (norte)", "Puente sin magia, plato que persigue y campamento de contrabandistas.",
            c + new Vector3(0f, 0.1f, -12f), 0f);
        return avisos;
    }

    private static GameObject InstanciarCap2(string ruta, Transform padre, Vector3 posicion, Scene scene, List<string> avisos)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (asset == null) { avisos.Add("Cap2: falta " + ruta); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        go.transform.SetParent(padre);
        go.transform.position = posicion;
        return go;
    }

    /// Añade un destino al viaje rápido del LAB sin tocar la lista de CrearDestinos.
    private static void AnadirDestinoCap2(string nombre, string queHay, Vector3 posicion, float giro)
    {
        var viaje = Object.FindAnyObjectByType<ViajeDelLab>();
        if (viaje == null) return;
        var punto = new GameObject("Destino — " + nombre).transform;
        punto.SetParent(viaje.transform);
        punto.SetPositionAndRotation(posicion, Quaternion.Euler(0f, giro, 0f));
        var so = new SerializedObject(viaje);
        var destinos = so.FindProperty("destinos");
        destinos.arraySize++;
        Destino(destinos.GetArrayElementAtIndex(destinos.arraySize - 1), nombre, queHay, punto);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
