using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Datos de la revisión del arranque del Cap. 3 (vídeo «mas o menos 2», 12 nov 2026), INC-1122:
/// emociones de las charlas de despedida, icono del trozo del aro, la capa con su rasgo, fuera el
/// pañuelo de Erika, el Espejo acaba en negro y la visión de la bola en la torre de Liam con su luz.
/// Se puede ejecutar varias veces.
public static class RevisionDelCap3Inicio
{
    private const string GrafoCap2 = "Assets/NarrativeGraph/Cap2.asset";
    private const string NodoPanuelo = "cap2-prepara-viaje-erika-item";
    private const string NodoCombateErika = "cap2-prepara-viaje-erika-combate";
    private const string NodoFinErika = "cap2-prepara-viaje-erika-2";
    private const string SecuenciaEspejo = "Assets/_SEQUENCES/SEQ_Cap3_01_ElEspejo.asset";
    private const string ItemTrozo = "Assets/_ITEMS/IT_TrozoDelAro.asset";
    private const string IconoTrozo = "Assets/Art/UI/Items/trozo_del_aro.png";
    private const string Capa = "Assets/_WARDROBE ITEMS/WardrobeItem_Cloak02.asset";
    private const string Torre = "Assets/Scenes/Interior/TorreDeLiam.unity";
    private const string MaterialHumo = "Assets/Art/Materials/VentanaDeHumo.mat";
    private const string PropVision = "PROP_VisionDeLaBola";
    private const string PropVisionDelCombate = "PROP_VisionDelCombate";
    private const string ImagenDelCombate = "Assets/Art/World/Visiones/Vision_WillContraElDemonio.png";

    // Emociones por línea (NPCEmotion): el gesto sale de la emoción, así que se elige por lo que
    // pasa en la charla. Oliver: triste porque se va, alegre al final con lágrimas.
    private static readonly (string ruta, int[] emociones)[] Charlas =
    {
        ("Assets/_DIALOGUES/DIALOGUE NPCS/Oliver/DLG_OLIVER_DESPEDIDA.asset", new[] { 9, 0, 15, 1, 17 }),
        ("Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DLG_ELDRAN_MOTIVOS.asset", new[] { 1, 0, 0, 11, 1, 0 }),
    };

    [MenuItem("El Sendero/Archivo/Revisión del arranque del Cap. 3 (INC-1122)")]
    private static void Aplicar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Revisión Cap. 3", "Hazlo fuera de Play."); return; }
        var sb = new StringBuilder();
        Emociones(sb);
        IconoDelTrozo(sb);
        RasgoDeLaCapa(sb);
        FueraElPanuelo(sb);
        EspejoAcabaEnNegro(sb);
        VisionDeLaBola(sb);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Revisión Cap. 3", sb.ToString());
    }

    private static void Emociones(StringBuilder sb)
    {
        foreach (var (ruta, emociones) in Charlas)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ruta);
            if (asset == null) { sb.Append($"Falta {ruta}. "); continue; }
            var so = new SerializedObject(asset);
            var lineas = so.FindProperty("lines");
            int n = Mathf.Min(lineas.arraySize, emociones.Length);
            for (int i = 0; i < n; i++)
                lineas.GetArrayElementAtIndex(i).FindPropertyRelative("emotion").intValue = emociones[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.Append($"{asset.name}: {n} emociones. ");
        }
    }

    private static void IconoDelTrozo(StringBuilder sb)
    {
        var item = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ItemTrozo);
        var icono = AssetDatabase.LoadAssetAtPath<Sprite>(IconoTrozo);
        if (item == null || icono == null) { sb.Append("Trozo del aro: falta el objeto o el icono. "); return; }
        var so = new SerializedObject(item);
        so.FindProperty("icon").objectReferenceValue = icono;
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.Append("Trozo del aro: icono del trozo (antes una llave). ");
    }

    private static void RasgoDeLaCapa(StringBuilder sb)
    {
        var capa = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Capa);
        if (capa == null) { sb.Append("Falta la capa. "); return; }
        var so = new SerializedObject(capa);
        var rasgos = so.FindProperty("rasgos");
        bool tiene = false;
        for (int i = 0; i < rasgos.arraySize; i++)
            if (rasgos.GetArrayElementAtIndex(i).intValue == (int)RasgoDePrenda.ContraataqueMasAmplio) tiene = true;
        if (!tiene)
        {
            rasgos.InsertArrayElementAtIndex(rasgos.arraySize);
            rasgos.GetArrayElementAtIndex(rasgos.arraySize - 1).intValue = (int)RasgoDePrenda.ContraataqueMasAmplio;
        }
        so.FindProperty("description").stringValue =
            "La capa que Victoria le cosió a Will para el viaje. Con ella puesta hay más margen para devolver un golpe con la Protección.";
        so.ApplyModifiedPropertiesWithoutUndo();
        sb.Append("Capa de principiante: más margen para devolver golpes. ");
    }

    private static void FueraElPanuelo(StringBuilder sb)
    {
        var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(GrafoCap2);
        if (grafo == null) { sb.Append("Falta Cap2.asset. "); return; }
        var panuelo = grafo.FindNode(NodoPanuelo);
        var combate = grafo.FindNode(NodoCombateErika);
        if (panuelo == null) { sb.Append("Pañuelo: ya no estaba. "); return; }
        if (combate == null || grafo.FindNode(NodoFinErika) == null) { sb.Append("Pañuelo: no encuentro los nodos de Erika. "); return; }
        combate.outputs = combate.outputs.Select(o => o == NodoPanuelo ? NodoFinErika : o).Distinct().ToList();
        foreach (var n in grafo.nodes.Where(n => n != null && n.outputs != null))
            n.outputs.Remove(NodoPanuelo);
        grafo.nodes.Remove(panuelo);
        EditorUtility.SetDirty(grafo);
        sb.Append("Cap. 2: Erika ya no da el pañuelo (el combate pasa directo a cerrar su encargo). ");
    }

    private static void EspejoAcabaEnNegro(StringBuilder sb)
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SecuenciaEspejo);
        if (def == null) { sb.Append("Falta la secuencia del Espejo. "); return; }
        def.endStayBlack = true;
        EditorUtility.SetDirty(def);
        sb.Append("Espejo: acaba en negro; destapa el grafo con Will ya en su sitio. ");
    }

    // ── La visión de la bola ─────────────────────────────────────────────────────────────────

    private static void VisionDeLaBola(StringBuilder sb)
    {
        var escena = SceneManager.GetSceneByPath(Torre);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(Torre, OpenSceneMode.Additive);
        try
        {
            var raices = escena.GetRootGameObjects();
            Transform Buscar(string nombre) => raices.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == nombre);

            var bola = Buscar("PROP_Bola");
            var escenario = raices.SelectMany(r => r.GetComponentsInChildren<SequenceStage>(true)).FirstOrDefault();
            if (bola == null || escenario == null) { sb.Append("Torre: falta la bola o el escenario. "); return; }

            // Luz de la bola: es la que ilumina a Liam, violeta como el cristal.
            var pulso = Buscar("Pulso del cristal")?.GetComponent<Light>();
            if (pulso != null)
            {
                pulso.color = new Color(0.72f, 0.5f, 1f);
                pulso.intensity = 1.4f;
                pulso.range = Mathf.Max(pulso.range, 3.5f);
            }
            var relleno = Buscar("Relleno de noche")?.GetComponent<Light>();
            if (relleno != null) relleno.intensity = 0.2f;

            var vieja = Buscar(PropVision);
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

            var raiz = new GameObject(PropVision);
            SceneManager.MoveGameObjectToScene(raiz, escena);
            // Sobre la bola y algo apartada de Liam, para que en los planos se le vea al lado de la nube.
            raiz.transform.position = bola.position + new Vector3(0.45f, 0.5f, 0f);

            var humo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            humo.name = "Humo";
            Object.DestroyImmediate(humo.GetComponent<Collider>());
            humo.transform.SetParent(raiz.transform, false);
            humo.transform.localScale = new Vector3(0.9f, 0.7f, 1f);
            var rend = humo.GetComponent<MeshRenderer>();
            rend.sharedMaterial = MaterialDeHumo();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            // La cámara de la visión, en el cuarto de Tobi, de frente a la cama.
            var cama = Buscar("Tobi · cama");
            Vector3 tobi = cama != null ? cama.position : new Vector3(-5988f, 100.42f, 6000.42f);
            var camGo = new GameObject("Cámara de la visión");
            camGo.transform.SetParent(raiz.transform, false);
            // Tobi está sentado en la cama: la cabeza le queda unos 0,7 m por encima.
            camGo.transform.position = new Vector3(tobi.x - 0.4f, tobi.y + 0.9f, tobi.z - 2.0f);
            camGo.transform.LookAt(new Vector3(tobi.x, tobi.y + 0.55f, tobi.z));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.14f);
            cam.enabled = false;

            var luzGo = new GameObject("Luz de la visión");
            luzGo.transform.SetParent(raiz.transform, false);
            luzGo.transform.localPosition = Vector3.zero;
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(0.75f, 0.55f, 1f);
            luz.intensity = 0.9f;
            luz.range = 2.8f;
            luz.shadows = LightShadows.None;

            var ventana = raiz.AddComponent<VentanaDeVision>();
            var so = new SerializedObject(ventana);
            so.FindProperty("camaraDeLaVision").objectReferenceValue = cam;
            so.FindProperty("ventana").objectReferenceValue = rend;
            so.FindProperty("luz").objectReferenceValue = luz;
            so.ApplyModifiedPropertiesWithoutUndo();
            raiz.SetActive(false);

            var se = new SerializedObject(escenario);
            var props = se.FindProperty("_props");
            int idx = -1;
            for (int i = 0; i < props.arraySize; i++)
                if (props.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == PropVision) idx = i;
            if (idx < 0) { props.InsertArrayElementAtIndex(props.arraySize); idx = props.arraySize - 1; }
            var p = props.GetArrayElementAtIndex(idx);
            p.FindPropertyRelative("id").stringValue = PropVision;
            p.FindPropertyRelative("target").objectReferenceValue = raiz.transform;
            p.FindPropertyRelative("eyeHeight").floatValue = 0f;
            se.ApplyModifiedPropertiesWithoutUndo();

            VisionDelCombate(escena, escenario, bola, sb);
            SinLlaveDelAro(escena, escenario, sb);

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            sb.Append($"Torre: visión de la bola montada sobre el cristal (cámara en el cuarto de Tobi), luz de la bola violeta. ");
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }

    /// La visión del Cap. 2 (Los planes de Liam): en la bola, Will contra el demonio. Es algo que ya
    /// ha pasado cuando se ve la escena, así que la nube enseña una imagen fija. Ver INC-1123
    private static void VisionDelCombate(Scene escena, SequenceStage escenario, Transform bola, StringBuilder sb)
    {
        var imagen = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagenDelCombate);
        if (imagen == null) { sb.Append($"Falta {ImagenDelCombate}. "); return; }
        var vieja = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == PropVisionDelCombate);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var raiz = new GameObject(PropVisionDelCombate);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        raiz.transform.position = bola.position + new Vector3(0.45f, 0.5f, 0f);

        var humo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        humo.name = "Humo";
        Object.DestroyImmediate(humo.GetComponent<Collider>());
        humo.transform.SetParent(raiz.transform, false);
        humo.transform.localScale = new Vector3(0.9f, 0.7f, 1f);
        var rend = humo.GetComponent<MeshRenderer>();
        rend.sharedMaterial = MaterialDeHumo();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var luzGo = new GameObject("Luz de la visión");
        luzGo.transform.SetParent(raiz.transform, false);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(0.75f, 0.55f, 1f);
        luz.intensity = 0.9f;
        luz.range = 2.8f;
        luz.shadows = LightShadows.None;

        var ventana = raiz.AddComponent<VentanaDeVision>();
        var so = new SerializedObject(ventana);
        so.FindProperty("imagenFija").objectReferenceValue = imagen;
        so.FindProperty("ventana").objectReferenceValue = rend;
        so.FindProperty("luz").objectReferenceValue = luz;
        so.ApplyModifiedPropertiesWithoutUndo();
        raiz.SetActive(false);

        var se = new SerializedObject(escenario);
        var props = se.FindProperty("_props");
        int idx = -1;
        for (int i = 0; i < props.arraySize; i++)
            if (props.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == PropVisionDelCombate) idx = i;
        if (idx < 0) { props.InsertArrayElementAtIndex(props.arraySize); idx = props.arraySize - 1; }
        var p = props.GetArrayElementAtIndex(idx);
        p.FindPropertyRelative("id").stringValue = PropVisionDelCombate;
        p.FindPropertyRelative("target").objectReferenceValue = raiz.transform;
        p.FindPropertyRelative("eyeHeight").floatValue = 0f;
        se.ApplyModifiedPropertiesWithoutUndo();
        sb.Append("Torre: visión del combate (Will contra el demonio, imagen fija) sobre la bola. ");
    }

    /// El aro no tiene llave (lore simplificado por Raúl): fuera la llave de la mesa y su brillo,
    /// y sus entradas del escenario. Ver INC-1126
    private static void SinLlaveDelAro(Scene escena, SequenceStage escenario, StringBuilder sb)
    {
        string[] nombres = { "PROP_Llave", "PROP_BrilloLlave" };
        int quitados = 0;
        foreach (var t in escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray())
            if (t != null && nombres.Contains(t.name)) { Object.DestroyImmediate(t.gameObject); quitados++; }
        var se = new SerializedObject(escenario);
        var props = se.FindProperty("_props");
        for (int i = props.arraySize - 1; i >= 0; i--)
            if (nombres.Contains(props.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue))
                props.DeleteArrayElementAtIndex(i);
        se.ApplyModifiedPropertiesWithoutUndo();
        sb.Append(quitados > 0 ? $"Torre: fuera la llave del aro y su brillo ({quitados}). " : "Torre: la llave del aro ya no estaba. ");
    }

    private static Material MaterialDeHumo()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialHumo);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sendero/VentanaDeHumo")) { name = "VentanaDeHumo" };
            AssetDatabase.CreateAsset(mat, MaterialHumo);
        }
        mat.SetFloat("_Ondulacion", 0.025f);
        mat.SetFloat("_AnchoDelBorde", 0.05f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
