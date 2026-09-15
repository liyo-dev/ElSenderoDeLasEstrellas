using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Revisión 24 de la maqueta (11 sep 2026): Raúl, viendo la muralla del Reino, pidió "darle una vuelta a los assets".
/// Desde fuera del Editor no se ve qué aspecto tiene cada prefab (Wall01 resultó ser una arcada con huecos, Castle01_b01
/// una verja dorada gigante). Esta herramienta monta una "estantería" con los candidatos de muralla/torre/puerta/ciudad
/// de los dos packs, cada uno a su escala nativa junto a un cubo de referencia de 2 m y su nombre, y guarda una captura
/// (PNG) más un informe con las medidas de cada uno — para elegir con datos, no a ciegas.
///
/// Uso: El Sendero → Eldoria Codex → Catálogo de assets de muralla y ciudad. Crea una escena temporal (no guarda nada
/// en las escenas del juego), captura y la cierra. Salida: Assets/Scenes/Worlds/CatalogoAssets_<fecha>/.
/// </summary>
public static class EldoriaCatalogoAssets
{
    const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    const string Tiny = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/";

    static readonly string[] Candidatos = {
        Pack+"Main Structures/Wall/Wall01.prefab", Pack+"Main Structures/Wall/Wall02.prefab", Pack+"Main Structures/Wall/Wall03.prefab",
        Pack+"Main Structures/Wall/Tower01.prefab", Pack+"Main Structures/Wall/Tower02.prefab",
        Pack+"Main Structures/Wall/Castle01_a01.prefab", Pack+"Main Structures/Wall/Castle01_a02.prefab", Pack+"Main Structures/Wall/Castle01_b01.prefab",
        Pack+"Main Structures/Wall/Stronghold01.prefab", Pack+"Main Structures/Wall/Stronghold02.prefab", Pack+"Main Structures/Wall/City01.prefab",
        Tiny+"BuildingUtilityDeco/Wall01.prefab", Tiny+"BuildingUtilityDeco/Wall02.prefab", Tiny+"BuildingUtilityDeco/Wall03.prefab", Tiny+"BuildingUtilityDeco/Wall04.prefab",
        Tiny+"BuildingUtilityDeco/Gate01.prefab", Tiny+"BuildingUtilityDeco/Gate02.prefab", Tiny+"BuildingUtilityDeco/Gate03.prefab", Tiny+"BuildingUtilityDeco/Gate04.prefab",
        Tiny+"BuildingUtilityDeco/WatchTower01.prefab", Tiny+"BuildingUtilityDeco/Pillar01.prefab", Tiny+"BuildingUtilityDeco/Stair01.prefab"
    };

    [MenuItem("El Sendero/Eldoria Codex/Catálogo de assets de muralla y ciudad")]
    public static void Catalogar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play antes.");
        var anterior = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(escena);
        string carpeta = "Assets/Scenes/Worlds/CatalogoAssets_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(carpeta);
        var informe = new System.Text.StringBuilder("Catálogo de assets de muralla/torre/puerta/ciudad — medidas a escala nativa (x × z × alto, en m).\n");
        try
        {
            var raiz = new GameObject("CATÁLOGO").transform;
            var luz = new GameObject("Sol").AddComponent<Light>(); luz.transform.SetParent(raiz);
            luz.type = LightType.Directional; luz.intensity = 1.1f; luz.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.7f, .78f, .85f); RenderSettings.ambientEquatorColor = new Color(.5f, .5f, .48f); RenderSettings.ambientGroundColor = new Color(.25f, .25f, .22f);
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane); suelo.name = "Suelo"; suelo.transform.SetParent(raiz);
            suelo.transform.localScale = new Vector3(60, 1, 20); suelo.GetComponent<MeshRenderer>().sharedMaterial = Mat(new Color(.35f, .48f, .25f));
            var matRef = Mat(new Color(.9f, .2f, .2f));

            // Cada candidato en una casilla de 40 m; el que mida más de 40 m de lado se reduce y se anota.
            float x = -280; int columna = 0; var lista = new List<(string, Bounds)>();
            foreach (var ruta in Candidatos)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                string nombre = Path.GetFileNameWithoutExtension(ruta) + (ruta.StartsWith(Tiny) ? " (Tiny)" : " (FK)");
                if (prefab == null) { informe.AppendLine(nombre + ": NO ENCONTRADO en " + ruta); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raiz); go.name = nombre;
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                var b = Limites(go);
                float lado = Mathf.Max(b.size.x, b.size.z, b.size.y); float escala = 1;
                if (lado > 36) { escala = 36 / lado; go.transform.localScale *= escala; b = Limites(go); }
                var pos = new Vector3(x, 0, 0);
                go.transform.position += new Vector3(pos.x - b.center.x, -b.min.y, pos.z - b.center.z);
                var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube); cubo.name = "Ref 2 m"; cubo.transform.SetParent(raiz);
                cubo.transform.localScale = Vector3.one * 2; cubo.transform.position = new Vector3(x - 16, 1, -16); cubo.GetComponent<MeshRenderer>().sharedMaterial = matRef;
                var texto = new GameObject("Etiqueta").AddComponent<TextMesh>(); texto.transform.SetParent(raiz);
                texto.text = nombre + (escala < 1 ? $"  (reducido ×{escala:0.00})" : ""); texto.fontSize = 48; texto.characterSize = .5f; texto.anchor = TextAnchor.MiddleCenter;
                texto.transform.position = new Vector3(x, 1, -22); texto.transform.rotation = Quaternion.Euler(90, 0, 0);
                var bn = Limites(go); var nat = new Vector3(bn.size.x / escala, bn.size.y / escala, bn.size.z / escala);
                informe.AppendLine($"{nombre}: {nat.x:0.0} × {nat.z:0.0} × alto {nat.y:0.0}" + (escala < 1 ? $" (en la captura reducido ×{escala:0.00})" : ""));
                lista.Add((nombre, bn)); x += 40; columna++;
            }
            var cam = new GameObject("Cámara").AddComponent<Camera>(); cam.transform.SetParent(raiz);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .65f, .75f); cam.farClipPlane = 2000;
            cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // Una foto general (ortográfica desde arriba-delante) y una por casilla.
            Foto(cam, carpeta + "/Catalogo_general.png", new Vector3(x / 2 - 160, 90, -140), new Vector3(x / 2 - 160, 8, 0), true, 60, 4000, 1000);
            float cx = -280;
            foreach (var (nombre, b) in lista)
            {
                Foto(cam, carpeta + "/" + nombre.Replace(" ", "_").Replace("(", "").Replace(")", "") + ".png", new Vector3(cx - 30, Mathf.Max(18, b.size.y * .9f), -40), new Vector3(cx, b.size.y * .4f, 0), false, 0, 1200, 900);
                cx += 40;
            }
            File.WriteAllText(carpeta + "/Informe_catalogo.txt", informe.ToString());
            AssetDatabase.Refresh();
            Debug.Log("Catálogo de assets guardado en " + carpeta);
        }
        finally
        {
            if (anterior.IsValid() && anterior.isLoaded) SceneManager.SetActiveScene(anterior);
            EditorSceneManager.CloseScene(escena, true);
        }
    }

    static Material Mat(Color c) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = c; m.SetFloat("_Smoothness", 0); return m; }

    static Bounds Limites(GameObject go)
    {
        bool primero = true; var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (primero) { b = r.bounds; primero = false; } else b.Encapsulate(r.bounds); }
        return b;
    }

    static void Foto(Camera cam, string ruta, Vector3 desde, Vector3 hacia, bool orto, float tam, int w, int h)
    {
        cam.transform.position = desde; cam.transform.LookAt(hacia); cam.orthographic = orto; cam.orthographicSize = tam;
        var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt;
        cam.Render();
        var activo = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        RenderTexture.active = activo; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }
}
