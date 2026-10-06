#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Convierte el puesto de fruta del mercado en el puesto de Tomasa, la tallista (Ver INC-615).
/// Cambia el mostrador de fruta (Counter01_a0X) por el mostrador vacío del mismo pack (Counter01_b01)
/// y le pone encima la mercancía de MercanciaTomasa.fbx: figuritas, estrella torcida, pájaros,
/// farolillos, campanillas y la guirnalda bajo el toldo.
/// Usa el puesto seleccionado en la jerarquía; si no hay ninguno seleccionado, el más cercano a SPAWN_Tomasa.
/// Se puede repetir: reemplaza la mercancía que ya hubiera.
public static class VestirPuestoDeTomasa
{
    const string Carpeta = "Assets/Art/Models/PuestoDeTomasa";
    const string RutaFbx = Carpeta + "/MercanciaTomasa.fbx";
    const string RutaPaleta = Carpeta + "/PaletaTomasa.png";
    const string RutaMaterial = Carpeta + "/MAT_PaletaTomasa.mat";
    const string RutaMaterialDelPack = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials/FK01.mat";
    const string RutaMostradorVacio = "Assets/Art/World/Fantasy_Kingdom_Pack/Meshes/Counter01_b01.FBX";
    const string NombreMaterialEnFbx = "PaletaTomasa";
    const string NombreMercancia = "MercanciaTomasa";
    const string SpawnId = "SPAWN_Tomasa";

    [MenuItem("El Sendero/Tiendas/Vestir el puesto de Tomasa (figuritas en vez de fruta)")]
    static void Vestir()
    {
        var material = PrepararMaterial();
        if (material == null) return;
        PrepararImportacion(material);

        var mercanciaFbx = AssetDatabase.LoadAssetAtPath<GameObject>(RutaFbx);
        var mallaVacia = CargarMalla(RutaMostradorVacio);
        if (mercanciaFbx == null || mallaVacia == null)
        {
            EditorUtility.DisplayDialog("Puesto de Tomasa", $"Falta {RutaFbx} o {RutaMostradorVacio}.", "Vale");
            return;
        }

        var mostrador = BuscarMostrador();
        if (mostrador == null)
        {
            EditorUtility.DisplayDialog("Puesto de Tomasa",
                "No encuentro el mostrador (Counter01_a0X). Selecciona el puesto (p. ej. BuildingAT36) en la jerarquía y repite.", "Vale");
            return;
        }

        Undo.RecordObject(mostrador, "Vestir el puesto de Tomasa");
        mostrador.sharedMesh = mallaVacia;

        var anterior = mostrador.transform.Find(NombreMercancia);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);

        var mercancia = (GameObject)PrefabUtility.InstantiatePrefab(mercanciaFbx, mostrador.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(mercancia, "Vestir el puesto de Tomasa");
        mercancia.name = NombreMercancia;
        Undo.SetTransformParent(mercancia.transform, mostrador.transform, "Vestir el puesto de Tomasa");
        mercancia.transform.localPosition = Vector3.zero;
        mercancia.transform.localRotation = Quaternion.identity;
        mercancia.transform.localScale = Vector3.one;

        var banderas = GameObjectUtility.GetStaticEditorFlags(mostrador.gameObject);
        foreach (var t in mercancia.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, banderas);

        EditorSceneManager.MarkSceneDirty(mostrador.gameObject.scene);
        Selection.activeGameObject = mercancia;
        Debug.Log($"[Tomasa] Puesto vestido en '{(mostrador.transform.parent != null ? mostrador.transform.parent.name : mostrador.name)}' ({mostrador.transform.position}). Guarda la escena (Ctrl+S).");
    }

    /// Material de la mercancía: copia del material del pack (mismo shader y ajustes) con la paleta como textura.
    static Material PrepararMaterial()
    {
        var importador = AssetImporter.GetAtPath(RutaPaleta) as TextureImporter;
        if (importador == null)
        {
            EditorUtility.DisplayDialog("Puesto de Tomasa", $"Falta {RutaPaleta}.", "Vale");
            return null;
        }
        if (importador.wrapMode != TextureWrapMode.Clamp || importador.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importador.wrapMode = TextureWrapMode.Clamp;
            importador.textureCompression = TextureImporterCompression.Uncompressed;
            importador.SaveAndReimport();
        }
        var paleta = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaPaleta);

        var material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (material == null)
        {
            var delPack = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialDelPack);
            material = delPack != null ? new Material(delPack) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, RutaMaterial);
        }
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", paleta);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", paleta);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// El FBX usa el material del proyecto en vez de crear uno propio, y no trae animaciones ni cámaras.
    static void PrepararImportacion(Material material)
    {
        var importador = AssetImporter.GetAtPath(RutaFbx) as ModelImporter;
        if (importador == null) return;
        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), NombreMaterialEnFbx);
        importador.GetExternalObjectMap().TryGetValue(id, out var actual);
        if (actual == material && !importador.importAnimation) return;
        importador.importAnimation = false;
        importador.importCameras = false;
        importador.importLights = false;
        importador.addCollider = false;
        importador.AddRemap(id, material);
        importador.SaveAndReimport();
    }

    static Mesh CargarMalla(string ruta)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (a is Mesh m) return m;
        return null;
    }

    static bool EsMostrador(MeshFilter mf) =>
        mf != null && mf.sharedMesh != null && mf.gameObject.name.StartsWith("Counter01_");

    /// El mostrador del puesto seleccionado o, si no hay selección válida, el más cercano a SPAWN_Tomasa.
    static MeshFilter BuscarMostrador()
    {
        if (Selection.activeGameObject != null)
        {
            var sel = Selection.activeGameObject;
            var raiz = sel.name.StartsWith("Counter01_") && sel.transform.parent != null ? sel.transform.parent : sel.transform;
            foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
                if (EsMostrador(mf)) return mf;
        }

        Vector3? spawn = null;
        foreach (var sp in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsInactive.Include))
            if (sp != null && sp.spawnId == SpawnId) { spawn = sp.transform.position; break; }
        if (spawn == null) return null;

        MeshFilter mejor = null;
        float mejorDist = float.MaxValue;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            if (!EsMostrador(mf)) continue;
            float d = (mf.transform.position - spawn.Value).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; mejor = mf; }
        }
        return mejor;
    }
}
#endif
