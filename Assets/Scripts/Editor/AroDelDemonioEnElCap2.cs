#if UNITY_EDITOR
using System.IO;
using ElSendero.Editor.Iconos;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// El aro que Eldran deja en la mesa en «La mañana después» y el trozo que se lleva Will son el
/// mismo aro de runas que lleva al cuello el Demonio 1 (mallas de AroDeRunasEnElCuello):
/// rehace AroRoto.prefab (el arco con el trozo al lado) y TrozoDelAro.prefab, pone el aro en la
/// mesa de WillHouse como PROP_Aro y saca el icono del Trozo del aro de su modelo.
/// Idempotente. Ver INC-954.
public static class AroDelDemonioEnElCap2
{
    const string Carpeta = "Assets/Art/Items/AroDelDemonio";
    const string PrefabAro = Carpeta + "/AroRoto.prefab";
    const string PrefabTrozo = Carpeta + "/TrozoDelAro.prefab";
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Item = "Assets/_ITEMS/IT_TrozoDelAro.asset";
    const string Icono = "Assets/Art/UI/Items/trozo_del_aro.png";

    /// Diámetro del aro entero en el mundo (lo que ocupa en la mesa).
    const float Diametro = 0.35f;
    /// Ángulo (grados, en el plano XZ de la malla) del centro del trozo: donde caen sus runas.
    const float CentroDelTrozo = 8.5f;
    /// Lo que se separa el trozo del arco, en fracción del radio, para que se vea partido.
    const float Separacion = 0.22f;

    [MenuItem("El Sendero/Objetos/Aro del demonio: aro de la mesa, trozo e icono (INC-954)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Aro del demonio", "Sal del modo Play antes de ejecutarlo.");
            return;
        }
        var arco = Cargar<Mesh>("Malla aro del cuello (arco)");
        var runasArco = Cargar<Mesh>("Malla runas del cuello (arco)");
        var trozo = Cargar<Mesh>("Malla aro del cuello (trozo)");
        var runasTrozo = Cargar<Mesh>("Malla runas del cuello (trozo)");
        var hierro = Cargar<Material>("Hierro del aro");
        var runas = Cargar<Material>("Runas del aro del cuello");

        float escala = Diametro / Mathf.Max(arco.bounds.size.x, arco.bounds.size.z);
        float radio = 0.5f * Mathf.Max(arco.bounds.size.x, arco.bounds.size.z);

        // Se montan en una escena de vista previa para no tocar las escenas abiertas.
        var taller = EditorSceneManager.NewPreviewScene();
        try
        {
        // ── El aro roto: el arco y, al lado, el trozo que le falta ──
        var raiz = Nuevo("AroRoto", taller);
        try
        {
            var modelo = Hijo(raiz.transform, "Modelo", Vector3.zero, Quaternion.identity, Vector3.one * escala);
            Pieza(modelo, "Arco", arco, runasArco, hierro, runas, Vector3.zero, Quaternion.identity);
            float rad = CentroDelTrozo * Mathf.Deg2Rad;
            var fuera = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Pieza(modelo, "Trozo", trozo, runasTrozo, hierro, runas, fuera * (radio * Separacion), Quaternion.Euler(0f, -14f, 0f));
            Centrar(raiz.transform, modelo);
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabAro);
        }
        finally { Object.DestroyImmediate(raiz); }

        // ── El trozo suelto: con las runas hacia +Z y centrado en su pivote ──
        raiz = Nuevo("TrozoDelAro", taller);
        try
        {
            var giro = Quaternion.Euler(0f, CentroDelTrozo - 90f, 0f);
            var modelo = Hijo(raiz.transform, "Modelo", Vector3.zero, Quaternion.identity, Vector3.one * escala);
            Pieza(modelo, "Trozo", trozo, runasTrozo, hierro, runas, Vector3.zero, giro);
            Centrar(raiz.transform, modelo);
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabTrozo);
        }
        finally { Object.DestroyImmediate(raiz); }
        }
        finally { EditorSceneManager.ClosePreviewScene(taller); }
        AssetDatabase.SaveAssets();

        string mesa = PonerEnLaMesa();
        string icono = HacerIcono();
        string texto = $"AroRoto y TrozoDelAro rehechos con el aro del demonio (Ø {Diametro:0.00} m). {mesa} {icono}";
        Debug.Log("[Aro del demonio] " + texto);
        OrdenesDelEditor.Avisar("Aro del demonio", texto);
    }

    /// Cambia PROP_Aro de WillHouse por el aro roto, en el mismo sitio y apagado (lo enciende la cinemática).
    static string PonerEnLaMesa()
    {
        var casa = EditorSceneManager.GetSceneByPath(Casa);
        bool abierta = casa.isLoaded;
        if (!abierta) casa = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        try
        {
            SequenceStage stage = null;
            foreach (var r in casa.GetRootGameObjects())
            {
                foreach (var s in r.GetComponentsInChildren<SequenceStage>(true))
                    if (s.name == "SEQ_MananaDespues") stage = s;
            }
            if (stage == null) return "No encuentro SEQ_MananaDespues en WillHouse: el aro de la mesa no ha cambiado.";
            var viejo = stage.transform.Find("PROP_Aro");
            if (viejo == null) return "No hay PROP_Aro en WillHouse: el aro de la mesa no ha cambiado.";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAro);
            var nuevo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, casa);
            nuevo.transform.SetParent(viejo.parent, false);
            nuevo.transform.SetPositionAndRotation(viejo.position, Quaternion.identity);
            nuevo.transform.SetSiblingIndex(viejo.GetSiblingIndex());
            Object.DestroyImmediate(viejo.gameObject);
            nuevo.name = "PROP_Aro";
            nuevo.SetActive(false);

            var so = new SerializedObject(stage);
            var lista = so.FindProperty("_props");
            for (int i = 0; i < lista.arraySize; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("id").stringValue == "PROP_Aro")
                    e.FindPropertyRelative("target").objectReferenceValue = nuevo.transform;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(casa);
            EditorSceneManager.SaveScene(casa);
            return "PROP_Aro de WillHouse es ahora el aro roto.";
        }
        finally
        {
            if (!abierta) EditorSceneManager.CloseScene(casa, true);
        }
    }

    /// Fotografía el trozo y lo pone de icono del objeto (el PNG anterior va a Versiones antiguas).
    static string HacerIcono()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTrozo);
        var ajustes = new AjustesDeFoto { tamano = 512, margen = 0.1f, yaw = 25f, pitch = 28f };
        var foto = FotografoDeIconos.Fotografiar(prefab, r => true, ajustes);
        try
        {
            string completo = Path.GetFullPath(Icono);
            if (File.Exists(completo))
            {
                string antiguas = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Versiones antiguas", "Icono trozo del aro (2026-11-08)"));
                Directory.CreateDirectory(antiguas);
                string copia = Path.Combine(antiguas, Path.GetFileName(completo));
                if (!File.Exists(copia)) File.Copy(completo, copia);
            }
            FotografoDeIconos.GuardarPng(foto, completo);
        }
        finally { Object.DestroyImmediate(foto); }
        AssetDatabase.ImportAsset(Icono, ImportAssetOptions.ForceUpdate);
        var importador = (TextureImporter)AssetImporter.GetAtPath(Icono);
        importador.textureType = TextureImporterType.Sprite;
        importador.spriteImportMode = SpriteImportMode.Single;
        importador.alphaSource = TextureImporterAlphaSource.FromInput;
        importador.alphaIsTransparency = true;
        importador.mipmapEnabled = false;
        importador.SaveAndReimport();

        var item = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Item);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icono);
        if (item == null || sprite == null) return "No he podido poner el icono al objeto.";
        var so = new SerializedObject(item);
        so.FindProperty("icon").objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
        return "Icono del Trozo del aro sacado de su modelo.";
    }

    static void Pieza(Transform padre, string nombre, Mesh banda, Mesh glifos, Material hierro, Material runas,
        Vector3 posicion, Quaternion giro)
    {
        var go = Hijo(padre, nombre, posicion, giro, Vector3.one).gameObject;
        go.AddComponent<MeshFilter>().sharedMesh = banda;
        go.AddComponent<MeshRenderer>().sharedMaterial = hierro;
        var r = Hijo(go.transform, "Runas", Vector3.zero, Quaternion.identity, Vector3.one).gameObject;
        r.AddComponent<MeshFilter>().sharedMesh = glifos;
        var mr = r.AddComponent<MeshRenderer>();
        mr.sharedMaterial = runas;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    static GameObject Nuevo(string nombre, Scene escena)
    {
        var go = new GameObject(nombre);
        SceneManager.MoveGameObjectToScene(go, escena);
        return go;
    }

    static Transform Hijo(Transform padre, string nombre, Vector3 posicion, Quaternion giro, Vector3 escala)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        t.localPosition = posicion;
        t.localRotation = giro;
        t.localScale = escala;
        return t;
    }

    /// Lleva el modelo para que su centro quede sobre el pivote y su base, a la altura del pivote.
    static void Centrar(Transform raiz, Transform modelo)
    {
        var rs = modelo.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        modelo.position -= new Vector3(b.center.x, b.min.y, b.center.z) - raiz.position;
    }

    static T Cargar<T>(string nombre) where T : Object
    {
        string ext = typeof(T) == typeof(Material) ? ".mat" : ".asset";
        var a = AssetDatabase.LoadAssetAtPath<T>($"{Carpeta}/{nombre}{ext}");
        if (a == null) throw new System.InvalidOperationException($"Falta {Carpeta}/{nombre}{ext} (pasa antes «Demonio 1: poner el aro de runas al cuello»).");
        return a;
    }
}
#endif
