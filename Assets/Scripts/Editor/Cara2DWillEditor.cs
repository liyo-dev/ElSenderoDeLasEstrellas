using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de Editor para integrar y calibrar las expresiones faciales 2D de Will, Estela, Liam, Eldran y NPCs.
/// Importa los sprites recortados, los monta sobre el hueso head y conecta con NPCEmotionController.
/// </summary>
public class Cara2DWillEditor : EditorWindow
{
    private const string RutaShader = "El Sendero/Personajes/Face2D_Decal";

    private const string RutaSpritesWill = "Assets/Art/Characters/Will_Face2D";
    private const string RutaPrefabWill = "Assets/Prefabs/_WILL.prefab";
    private const string RutaProfileWill = "Assets/Art/Characters/Will_Face2D/EmotionProfile_Will2D.asset";
    private const string RutaMaterialWill = "Assets/Art/Characters/Will_Face2D/Mat_Face2D_Will.mat";

    private const string RutaSpritesEstela = "Assets/Art/Characters/Estela_Face2D";
    private const string RutaPrefabEstela = "Assets/Prefabs/_ESTELA.prefab";
    private const string RutaProfileEstela = "Assets/Art/Characters/Estela_Face2D/EmotionProfile_Estela2D.asset";
    private const string RutaMaterialEstela = "Assets/Art/Characters/Estela_Face2D/Mat_Face2D_Estela.mat";

    private const string RutaSpritesLiam = "Assets/Art/Characters/Liam_Face2D";
    private const string RutaPrefabLiam = "Assets/Prefabs/_LIAM.prefab";
    private const string RutaProfileLiam = "Assets/Art/Characters/Liam_Face2D/EmotionProfile_Liam2D.asset";
    private const string RutaMaterialLiam = "Assets/Art/Characters/Liam_Face2D/Mat_Face2D_Liam.mat";

    private const string RutaSpritesEldran = "Assets/Art/Characters/Eldran_Face2D";
    private const string RutaPrefabEldran = "Assets/_NPCs/Eldran.prefab";
    private const string RutaPrefabEldranGrafo = "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab";
    private const string RutaProfileEldran = "Assets/Art/Characters/Eldran_Face2D/EmotionProfile_Eldran2D.asset";
    private const string RutaMaterialEldran = "Assets/Art/Characters/Eldran_Face2D/Mat_Face2D_Eldran.mat";

    private const string RutaSpritesNPC = "Assets/Art/Characters/NPC_Face2D";
    private const string RutaPrefabOliver = "Assets/_NPCs/Oliver.prefab";
    private const string RutaPrefabOliverGrafo = "Assets/_NPCs/_GrafoNarrativo/Oliver.prefab";
    private const string RutaProfileNPC = "Assets/Art/Characters/NPC_Face2D/EmotionProfile_NPC2D.asset";
    private const string RutaMaterialNPC = "Assets/Art/Characters/NPC_Face2D/Mat_Face2D_NPC.mat";

    [MenuItem("El Sendero/Personajes/Configurador Caras 2D")]
    public static void Open()
    {
        GetWindow<Cara2DWillEditor>("Caras 2D").Show();
    }

    [Header("Calibración Fina")]
    [SerializeField] private float escalaOjos = 0.65f;
    [SerializeField] private float escalaBoca = 0.55f;
    [SerializeField] private float offsetYOjos = 0.040f;
    [SerializeField] private float offsetYBoca = 0.038f;
    // Cuánto va cada quad por delante de la cara frontal de su pieza 3D, para que la cabeza no lo tape.
    [SerializeField] private float offsetZ = 0.015f;

    private GameObject _personajeEnEscena;
    private GameObject _npcPrefabPersonalizado;
    private NPCEmotionController _emotionController;
    private Vector2 _scrollPos;

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        EditorGUILayout.LabelField("Configuración de Caras 2D", EditorStyles.boldLabel);

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("Ajusta la escala y posición de los ojos y boca para que encajen perfectos en la cabeza.", MessageType.Info);

        EditorGUILayout.LabelField("Ajustes de Proporción Facial:", EditorStyles.boldLabel);
        escalaOjos = EditorGUILayout.Slider("Escala Ojos", escalaOjos, 0.3f, 1.2f);
        escalaBoca = EditorGUILayout.Slider("Escala Boca", escalaBoca, 0.3f, 1.2f);
        offsetYOjos = EditorGUILayout.Slider("Subir/Bajar Ojos (Y)", offsetYOjos, -0.1f, 0.1f);
        offsetYBoca = EditorGUILayout.Slider("Subir/Bajar Boca (Y)", offsetYBoca, -0.1f, 0.1f);
        offsetZ = EditorGUILayout.Slider("Adelanto de la piel (m)", offsetZ, 0.004f, 0.06f);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("👉 REAJUSTAR PROPORCIONES Y OCULTAR CEJAS 3D 👈", GUILayout.Height(34)))
        {
            ActualizarMaterialesConShaderDecal();
            ReajustarProporcionesEnTodos(escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
        }

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("--- PROTAGONISTAS ---", EditorStyles.boldLabel);
        
        // WILL
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aplicar Cara 2D a WILL", GUILayout.Height(26)))
            AplicarCara2DAPersonaje(RutaSpritesWill, RutaPrefabWill, RutaProfileWill, RutaMaterialWill, "Will", false, escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
        if (GUILayout.Button("Restaurar 3D Will", GUILayout.Width(130), GUILayout.Height(26)))
            RestaurarCara3DEnPrefab(RutaPrefabWill);
        EditorGUILayout.EndHorizontal();

        // ESTELA
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aplicar Cara 2D a ESTELA", GUILayout.Height(26)))
            AplicarCara2DAPersonaje(RutaSpritesEstela, RutaPrefabEstela, RutaProfileEstela, RutaMaterialEstela, "Estela", true, escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
        if (GUILayout.Button("Restaurar 3D Estela", GUILayout.Width(130), GUILayout.Height(26)))
            RestaurarCara3DEnPrefab(RutaPrefabEstela);
        EditorGUILayout.EndHorizontal();

        // LIAM
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aplicar Cara 2D a LIAM", GUILayout.Height(26)))
            AplicarCara2DAPersonaje(RutaSpritesLiam, RutaPrefabLiam, RutaProfileLiam, RutaMaterialLiam, "Liam", false, escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
        if (GUILayout.Button("Restaurar 3D Liam", GUILayout.Width(130), GUILayout.Height(26)))
            RestaurarCara3DEnPrefab(RutaPrefabLiam);
        EditorGUILayout.EndHorizontal();

        // ELDRAN
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aplicar Cara 2D a ELDRAN (ambos prefabs)", GUILayout.Height(26)))
        {
            AplicarCara2DAPersonaje(RutaSpritesEldran, RutaPrefabEldran, RutaProfileEldran, RutaMaterialEldran, "Eldran", false, escalaOjos, escalaBoca * 1.15f, offsetYOjos, offsetYBoca, offsetZ);
            if (File.Exists(RutaPrefabEldranGrafo))
                AplicarCara2DAPersonaje(RutaSpritesEldran, RutaPrefabEldranGrafo, RutaProfileEldran, RutaMaterialEldran, "Eldran (Grafo)", false, escalaOjos, escalaBoca * 1.15f, offsetYOjos, offsetYBoca, offsetZ);
        }
        if (GUILayout.Button("Restaurar 3D Eldran", GUILayout.Width(130), GUILayout.Height(26)))
        {
            RestaurarCara3DEnPrefab(RutaPrefabEldran);
            if (File.Exists(RutaPrefabEldranGrafo))
                RestaurarCara3DEnPrefab(RutaPrefabEldranGrafo);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("--- SET UNIVERSAL PARA NPCS (Oliver, Aldeanos, etc.) ---", EditorStyles.boldLabel);

        // OLIVER
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aplicar Cara 2D a OLIVER", GUILayout.Height(26)))
        {
            AplicarCara2DAPersonaje(RutaSpritesNPC, RutaPrefabOliver, RutaProfileNPC, RutaMaterialNPC, "Oliver", false, escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
            if (File.Exists(RutaPrefabOliverGrafo))
                AplicarCara2DAPersonaje(RutaSpritesNPC, RutaPrefabOliverGrafo, RutaProfileNPC, RutaMaterialNPC, "Oliver (Grafo)", false, escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
        }
        if (GUILayout.Button("Restaurar 3D Oliver", GUILayout.Width(130), GUILayout.Height(26)))
        {
            RestaurarCara3DEnPrefab(RutaPrefabOliver);
            if (File.Exists(RutaPrefabOliverGrafo))
                RestaurarCara3DEnPrefab(RutaPrefabOliverGrafo);
        }
        EditorGUILayout.EndHorizontal();

        // CUALQUIER NPC
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Aplicar a cualquier otro Prefab de NPC:", EditorStyles.miniBoldLabel);
        _npcPrefabPersonalizado = (GameObject)EditorGUILayout.ObjectField("Prefab NPC", _npcPrefabPersonalizado, typeof(GameObject), false);

        if (_npcPrefabPersonalizado != null)
        {
            string pathPrefab = AssetDatabase.GetAssetPath(_npcPrefabPersonalizado);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Aplicar Cara 2D a {_npcPrefabPersonalizado.name}", GUILayout.Height(26)))
            {
                if (!string.IsNullOrEmpty(pathPrefab))
                    AplicarCara2DAPersonaje(RutaSpritesNPC, pathPrefab, RutaProfileNPC, RutaMaterialNPC, _npcPrefabPersonalizado.name, false, escalaOjos, escalaBoca, offsetYOjos, offsetYBoca, offsetZ);
            }
            if (GUILayout.Button("Restaurar 3D", GUILayout.Width(100), GUILayout.Height(26)))
            {
                if (!string.IsNullOrEmpty(pathPrefab))
                    RestaurarCara3DEnPrefab(pathPrefab);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("--- PRUEBAS EN VIVO (Cualquier personaje en Escena) ---", EditorStyles.boldLabel);
        _personajeEnEscena = (GameObject)EditorGUILayout.ObjectField("Personaje en Escena", _personajeEnEscena, typeof(GameObject), true);

        if (_personajeEnEscena != null)
        {
            if (_emotionController == null || _emotionController.gameObject != _personajeEnEscena)
                _emotionController = _personajeEnEscena.GetComponentInChildren<NPCEmotionController>(true);

            if (_emotionController != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Neutro")) _emotionController.SetEmotion(NPCEmotion.Neutral);
                if (GUILayout.Button("Alegre")) _emotionController.SetEmotion(NPCEmotion.Happy);
                if (GUILayout.Button("Triste")) _emotionController.SetEmotion(NPCEmotion.Sad);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Enfadado")) _emotionController.SetEmotion(NPCEmotion.Angry);
                if (GUILayout.Button("Sorprendido")) _emotionController.SetEmotion(NPCEmotion.Surprised);
                if (GUILayout.Button("Parpadear")) _emotionController.AplicarOjos("Eye2D_Cerrado");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("El objeto seleccionado no tiene NPCEmotionController.", MessageType.Warning);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    public static void ActualizarMaterialesConShaderDecal()
    {
        Shader shader = Shader.Find(RutaShader);
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        string[] rutas = { RutaMaterialWill, RutaMaterialEstela, RutaMaterialLiam, RutaMaterialEldran, RutaMaterialNPC };
        foreach (var r in rutas)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(r);
            if (m != null && shader != null)
            {
                m.shader = shader;
                EditorUtility.SetDirty(m);
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void ReajustarProporcionesEnTodos(float escOjos, float escBoca, float offYOjos, float offYBoca, float offZ)
    {
        ActualizarMaterialesConShaderDecal();

        // 1. Si hay un prefab abierto en PrefabStage
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null)
        {
            CalibrarTransformCara(stage.prefabContentsRoot.transform, escOjos, escBoca, offYOjos, offYBoca, offZ);
            EditorUtility.SetDirty(stage.prefabContentsRoot);
            Debug.Log($"[Cara2D] Reajustado en Prefab Stage abierto: {stage.prefabContentsRoot.name}");
        }

        // 2. En el prefab en disco de Will
        ReajustarEnPrefab(RutaPrefabWill, escOjos, escBoca, offYOjos, offYBoca, offZ);
        ReajustarEnPrefab(RutaPrefabEstela, escOjos, escBoca, offYOjos, offYBoca, offZ);
        ReajustarEnPrefab(RutaPrefabLiam, escOjos, escBoca, offYOjos, offYBoca, offZ);

        AssetDatabase.SaveAssets();
        Debug.Log("[Cara2D] ¡Proporciones faciales y cejas 3D calibradas con éxito!");
    }

    private static void ReajustarEnPrefab(string rutaPrefab, float escOjos, float escBoca, float offYOjos, float offYBoca, float offZ)
    {
        if (!File.Exists(rutaPrefab)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(rutaPrefab);
        if (root == null) return;

        CalibrarTransformCara(root.transform, escOjos, escBoca, offYOjos, offYBoca, offZ);
        PrefabUtility.SaveAsPrefabAsset(root, rutaPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// Dónde va un quad de la cara 2D: en ancho y en alto como la pieza 3D a la que sustituye (más el
    /// ajuste fino «offY») y por delante de su cara frontal «offZ» metros, para que la cabeza no le
    /// tape los sprites. Sin la pieza, devuelve la posición de reserva tal cual.
    private static Vector3 PosicionDeQuadFacial(Transform head, string nombreParte, Vector3 porDefecto, float offY, float offZ)
    {
        Bounds caja = CajaDeParte(head, nombreParte, out bool hayParte);
        if (!hayParte) return porDefecto;

        Transform raiz = head.root;
        Vector3 centro = raiz.InverseTransformPoint(caja.center);
        float mediaProfundidad = Mathf.Abs(Vector3.Dot(raiz.InverseTransformVector(caja.extents), Vector3.forward));
        Vector3 destino = new Vector3(centro.x, centro.y + offY, centro.z + mediaProfundidad + offZ);
        return head.InverseTransformPoint(raiz.TransformPoint(destino));
    }

    private static Bounds CajaDeParte(Transform head, string nombreParte, out bool hayParte)
    {
        Transform parte = head != null ? head.Find(nombreParte) : null;
        Renderer r = parte != null ? parte.GetComponent<Renderer>() : null;
        hayParte = r != null;
        return hayParte ? r.bounds : new Bounds(Vector3.zero, Vector3.one * 0.2f);
    }

    /// Envolvente de todo lo que se ve en la cabeza, sin contar la cara 2D.
    private static Bounds CajaDeLaCabeza(Transform head)
    {
        Bounds caja = new Bounds();
        bool primero = true;
        Transform face2D = head.Find("Face2D");
        foreach (Renderer r in head.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (face2D != null && (r.transform == face2D || r.transform.IsChildOf(face2D))) continue;
            if (primero) { caja = r.bounds; primero = false; } else caja.Encapsulate(r.bounds);
        }
        return caja;
    }

    private static void CalibrarTransformCara(Transform root, float escOjos, float escBoca, float offYOjos, float offYBoca, float offZ)
    {
        Transform head = BuscarHuesoHead(root);
        if (head == null) return;

        DesactivarPartesAntiguas(head);

        Transform face2D = head.Find("Face2D");
        if (face2D == null) return;

        Vector3 posOjosFinal = PosicionDeQuadFacial(head, "Eye01", new Vector3(0f, 0.082f, 0.165f), offYOjos, offZ);
        Vector3 posBocaFinal = PosicionDeQuadFacial(head, "Mouth01", new Vector3(0f, -0.018f, 0.162f), offYBoca, offZ);

        foreach (Transform child in face2D)
        {
            if (child.name.StartsWith("Eye2D_"))
            {
                child.localPosition = posOjosFinal;
                child.localScale = Vector3.one * escOjos;
            }
            else if (child.name.StartsWith("Mouth2D_"))
            {
                child.localPosition = posBocaFinal;
                child.localScale = Vector3.one * escBoca;
            }
        }
    }

    public static void PrepararSpritesYMaterial(string rutaSprites, string rutaMaterial, string rutaProfile, bool esFemenino)
    {
        string[] nombresOjos = { "Eye2D_Neutro", "Eye2D_Alegre", "Eye2D_Triste", esFemenino ? "Eye2D_Enfadada" : "Eye2D_Enfadado", esFemenino ? "Eye2D_Sorprendida" : "Eye2D_Sorprendido", "Eye2D_Cerrado" };
        string[] nombresBocas = { "Mouth2D_Neutra", "Mouth2D_Alegre", "Mouth2D_Habla", "Mouth2D_Triste", "Mouth2D_Enfadada", "Mouth2D_Sorprendida" };

        List<string> todos = new List<string>(nombresOjos);
        todos.AddRange(nombresBocas);

        foreach (var nom in todos)
        {
            string path = $"{rutaSprites}/{nom}.png";
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.spritePixelsPerUnit = 250f;
                EditorUtility.SetDirty(imp);
                imp.SaveAndReimport();
            }
        }

        Shader shader = Shader.Find(RutaShader);
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, rutaMaterial);
        }
        else
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }

        EmotionProfile profile = AssetDatabase.LoadAssetAtPath<EmotionProfile>(rutaProfile);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<EmotionProfile>();
            AssetDatabase.CreateAsset(profile, rutaProfile);
        }

        string eyeEnfadado = esFemenino ? "Eye2D_Enfadada" : "Eye2D_Enfadado";
        string eyeSorprendido = esFemenino ? "Eye2D_Sorprendida" : "Eye2D_Sorprendido";

        profile.ojosNeutros = new[] { "Eye2D_Neutro" };
        profile.bocasNeutras = new[] { "Mouth2D_Neutra" };
        profile.ojosDeReposoPorDefecto = "Eye2D_Neutro";
        profile.bocaDeReposoPorDefecto = "Mouth2D_Neutra";
        profile.bocaHablandoEntreabierta = "Mouth2D_Habla";
        profile.bocaHablandoAbierta = "Mouth2D_Alegre";

        profile.emotions = new[]
        {
            new EmotionMeshData { emotion = NPCEmotion.Neutral,    eyeMeshName = "Eye2D_Neutro",      mouthMeshName = "Mouth2D_Neutra",     bodyAnimStateName = "" },
            new EmotionMeshData { emotion = NPCEmotion.Happy,      eyeMeshName = "Eye2D_Alegre",      mouthMeshName = "Mouth2D_Alegre",     bodyAnimStateName = "HeadNod01" },
            new EmotionMeshData { emotion = NPCEmotion.Sad,        eyeMeshName = "Eye2D_Triste",      mouthMeshName = "Mouth2D_Triste",     bodyAnimStateName = "Cry01" },
            new EmotionMeshData { emotion = NPCEmotion.Angry,      eyeMeshName = eyeEnfadado,         mouthMeshName = "Mouth2D_Enfadada",   bodyAnimStateName = "Angry02" },
            new EmotionMeshData { emotion = NPCEmotion.Surprised,  eyeMeshName = eyeSorprendido,      mouthMeshName = "Mouth2D_Sorprendida", bodyAnimStateName = "SenseSomethingStart_NoWeapon" },
            new EmotionMeshData { emotion = NPCEmotion.Scared,     eyeMeshName = eyeSorprendido,      mouthMeshName = "Mouth2D_Triste",     bodyAnimStateName = "Beg01" },
            new EmotionMeshData { emotion = NPCEmotion.Thinking,   eyeMeshName = "Eye2D_Neutro",      mouthMeshName = "Mouth2D_Habla",      bodyAnimStateName = "Question01" },
            new EmotionMeshData { emotion = NPCEmotion.Tired,      eyeMeshName = "Eye2D_Triste",      mouthMeshName = "Mouth2D_Neutra",     bodyAnimStateName = "IdleWounded01" },
            new EmotionMeshData { emotion = NPCEmotion.Smirk,      eyeMeshName = "Eye2D_Alegre",      mouthMeshName = "Mouth2D_Alegre",     bodyAnimStateName = "Laugh01" },
            new EmotionMeshData { emotion = NPCEmotion.Worried,    eyeMeshName = "Eye2D_Triste",      mouthMeshName = "Mouth2D_Triste",     bodyAnimStateName = "HeadShake02" },
            new EmotionMeshData { emotion = NPCEmotion.Determined, eyeMeshName = eyeEnfadado,         mouthMeshName = "Mouth2D_Neutra",     bodyAnimStateName = "Challenging_NoWeapon" },
            new EmotionMeshData { emotion = NPCEmotion.Relieved,   eyeMeshName = "Eye2D_Alegre",      mouthMeshName = "Mouth2D_Neutra",     bodyAnimStateName = "Talk02" },
            new EmotionMeshData { emotion = NPCEmotion.Confused,   eyeMeshName = eyeSorprendido,      mouthMeshName = "Mouth2D_Habla",      bodyAnimStateName = "Question02" },
            new EmotionMeshData { emotion = NPCEmotion.Excited,    eyeMeshName = "Eye2D_Alegre",      mouthMeshName = "Mouth2D_Alegre",     bodyAnimStateName = "Cheer02" },
            new EmotionMeshData { emotion = NPCEmotion.Annoyed,    eyeMeshName = eyeEnfadado,         mouthMeshName = "Mouth2D_Triste",     bodyAnimStateName = "HeadShake01" },
            new EmotionMeshData { emotion = NPCEmotion.Grateful,   eyeMeshName = "Eye2D_Alegre",      mouthMeshName = "Mouth2D_Neutra",     bodyAnimStateName = "Reverence01" },
        };

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    public static void AplicarCara2DAPersonaje(string rutaSprites, string rutaPrefab, string rutaProfile, string rutaMaterial, string nombrePersonaje, bool esFemenino, float escOjos = 0.65f, float escBoca = 0.55f, float offYOjos = 0.040f, float offYBoca = 0.038f, float offZ = 0.015f)
    {
        PrepararSpritesYMaterial(rutaSprites, rutaMaterial, rutaProfile, esFemenino);

        GameObject root = PrefabUtility.LoadPrefabContents(rutaPrefab);
        if (root == null)
        {
            Debug.LogError($"[Cara2D] No se pudo cargar {rutaPrefab}");
            return;
        }

        Transform head = BuscarHuesoHead(root.transform);
        if (head == null)
        {
            Debug.LogError($"[Cara2D] No se encontró el hueso 'head' en el prefab de {nombrePersonaje}.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Quaternion rotacionCara = ObtenerRotacionFrontalCara(head);

        Vector3 posOjosFinal = PosicionDeQuadFacial(head, "Eye01", new Vector3(0f, 0.082f, 0.165f), offYOjos, offZ);
        Vector3 posBocaFinal = PosicionDeQuadFacial(head, "Mouth01", new Vector3(0f, -0.018f, 0.162f), offYBoca, offZ);

        DesactivarPartesAntiguas(head);

        Transform face2D = head.Find("Face2D");
        if (face2D == null)
        {
            GameObject faceGO = new GameObject("Face2D");
            faceGO.transform.SetParent(head, false);
            face2D = faceGO.transform;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);

        string eyeEnfadado = esFemenino ? "Eye2D_Enfadada" : "Eye2D_Enfadado";
        string eyeSorprendido = esFemenino ? "Eye2D_Sorprendida" : "Eye2D_Sorprendido";
        string[] nombresOjos = { "Eye2D_Neutro", "Eye2D_Alegre", "Eye2D_Triste", eyeEnfadado, eyeSorprendido, "Eye2D_Cerrado" };
        foreach (var nom in nombresOjos)
        {
            MontarElementoFacial(face2D, nom, $"{rutaSprites}/{nom}.png", posOjosFinal, rotacionCara, mat, nom == "Eye2D_Neutro", escOjos);
        }

        string[] nombresBocas = { "Mouth2D_Neutra", "Mouth2D_Alegre", "Mouth2D_Habla", "Mouth2D_Triste", "Mouth2D_Enfadada", "Mouth2D_Sorprendida" };
        foreach (var nom in nombresBocas)
        {
            MontarElementoFacial(face2D, nom, $"{rutaSprites}/{nom}.png", posBocaFinal, rotacionCara, mat, nom == "Mouth2D_Neutra", escBoca);
        }

        AjustarControladorDeEmociones(root, rutaProfile);

        PrefabUtility.SaveAsPrefabAsset(root, rutaPrefab);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[Cara2D] ¡Cara 2D montada con éxito en {nombrePersonaje} ({rutaPrefab})!");
    }

    /// Deja un solo NPCEmotionController en el personaje y lo apunta al perfil y a los quads de la
    /// cara 2D (ojos y boca de reposo, parpadeo y bocas del habla). Se busca por todo el personaje
    /// (no sólo en la raíz) y se quitan los duplicados: dos controladores manipulan las mismas
    /// mallas y se pelean, y en Play la cara se queda a medias. Devuelve un informe.
    private static string AjustarControladorDeEmociones(GameObject root, string rutaProfile)
    {
        StringBuilder informe = new StringBuilder();
        NPCEmotionController[] controladores = root.GetComponentsInChildren<NPCEmotionController>(true);
        if (controladores == null || controladores.Length == 0)
        {
            informe.AppendLine("no hay ningún NPCEmotionController en el personaje");
            return informe.ToString();
        }

        NPCEmotionController bueno = controladores[0];
        foreach (NPCEmotionController c in controladores)
        {
            if (c != null && c.EmotionProfile != null) { bueno = c; break; }
        }

        int quitados = 0;
        foreach (NPCEmotionController c in controladores)
        {
            if (c == null || c == bueno) continue;
            informe.AppendLine($"quito un controlador de más en «{c.gameObject.name}»");
            Object.DestroyImmediate(c, true);
            quitados++;
        }
        informe.AppendLine($"controlador en «{bueno.gameObject.name}»; duplicados quitados: {quitados}");

        Transform head = BuscarHuesoHead(root.transform);
        Transform face2D = head != null ? head.Find("Face2D") : null;
        Transform ojoNeutro = face2D != null ? face2D.Find("Eye2D_Neutro") : null;
        Transform bocaNeutra = face2D != null ? face2D.Find("Mouth2D_Neutra") : null;

        try
        {
            SerializedObject so = new SerializedObject(bueno);
            so.FindProperty("emotionProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EmotionProfile>(rutaProfile);
            so.FindProperty("eyePrefix").stringValue = "Eye2D_";
            so.FindProperty("mouthPrefix").stringValue = "Mouth2D_";
            so.FindProperty("ojosDelParpadeo").stringValue = "Eye2D_Cerrado";

            SerializedProperty yaCerrados = so.FindProperty("ojosYaCerrados");
            yaCerrados.arraySize = 1;
            yaCerrados.GetArrayElementAtIndex(0).stringValue = "Eye2D_Cerrado";

            so.FindProperty("ojosDeReposo").objectReferenceValue = ojoNeutro != null ? ojoNeutro.gameObject : null;
            so.FindProperty("bocaDeReposo").objectReferenceValue = bocaNeutra != null ? bocaNeutra.gameObject : null;

            so.ApplyModifiedProperties();
            informe.AppendLine($"perfil: {rutaProfile}");
            informe.AppendLine("prefijos: Eye2D_ / Mouth2D_   parpadeo: Eye2D_Cerrado");
            informe.AppendLine($"reposo: {(ojoNeutro != null ? ojoNeutro.name : "(nada)")} / {(bocaNeutra != null ? bocaNeutra.name : "(nada)")}");
        }
        catch (System.Exception ex)
        {
            informe.AppendLine("ERROR al configurar el controlador: " + ex.Message);
        }

        return informe.ToString();
    }

    public static void RestaurarCara3DEnPrefab(string rutaPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(rutaPrefab);
        if (root == null) return;

        Transform head = BuscarHuesoHead(root.transform);
        if (head != null)
        {
            Transform face2D = head.Find("Face2D");
            if (face2D != null)
            {
                DestroyImmediate(face2D.gameObject);
            }

            foreach (Transform child in head)
            {
                if (child.name == "Eye01" || child.name == "Mouth02" || child.name == "Eyebrow01")
                {
                    child.gameObject.SetActive(true);
                    var r = child.GetComponent<Renderer>();
                    if (r != null) r.enabled = true;
                }
            }
        }

        NPCEmotionController controlador = root.GetComponentInChildren<NPCEmotionController>(true);
        if (controlador != null)
        {
            SerializedObject so = new SerializedObject(controlador);
            so.FindProperty("eyePrefix").stringValue = "Eye";
            so.FindProperty("mouthPrefix").stringValue = "Mouth";
            so.FindProperty("ojosDelParpadeo").stringValue = "Eye09";

            SerializedProperty yaCerrados = so.FindProperty("ojosYaCerrados");
            yaCerrados.arraySize = 2;
            yaCerrados.GetArrayElementAtIndex(0).stringValue = "Eye07";
            yaCerrados.GetArrayElementAtIndex(1).stringValue = "Eye09";

            Transform ojos = head != null ? head.Find("Eye01") : null;
            Transform boca = head != null ? head.Find("Mouth02") : null;
            so.FindProperty("ojosDeReposo").objectReferenceValue = ojos != null ? ojos.gameObject : null;
            so.FindProperty("bocaDeReposo").objectReferenceValue = boca != null ? boca.gameObject : null;
            so.ApplyModifiedProperties();
        }

        PrefabUtility.SaveAsPrefabAsset(root, rutaPrefab);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[Cara2D] Restaurada cara 3D original en {rutaPrefab}.");
    }

    private static Transform BuscarHuesoHead(Transform parent)
    {
        if (parent.name == "head") return parent;
        foreach (Transform child in parent)
        {
            var res = BuscarHuesoHead(child);
            if (res != null) return res;
        }
        return null;
    }

    private static Quaternion ObtenerRotacionFrontalCara(Transform head)
    {
        Transform parte = head.Find("Eye01");
        if (parte != null)
        {
            return parte.localRotation * Quaternion.Euler(0f, 0f, 0f);
        }
        return Quaternion.identity;
    }

    private static void DesactivarPartesAntiguas(Transform head)
    {
        foreach (Transform child in head)
        {
            string n = child.name;
            if (n.StartsWith("Eye0") || n.StartsWith("Eye1") ||
                n.StartsWith("Mouth0") || n.StartsWith("Mouth1") ||
                n.StartsWith("Eyebrow"))
            {
                child.gameObject.SetActive(false);
                var r = child.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }
    }

    private static void MontarElementoFacial(Transform parent, string nombre, string rutaSprite, Vector3 posLocal, Quaternion rotLocal, Material mat, bool activoPorDefecto, float escala = 1f)
    {
        Transform existente = parent.Find(nombre);
        GameObject go = existente != null ? existente.gameObject : new GameObject(nombre);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = rotLocal;
        go.transform.localScale = Vector3.one * escala;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(rutaSprite);
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;

        go.SetActive(activoPorDefecto);
    }

    // ── Medir y ver la cara sin abrir el prefab a mano ────────────────────────────────────────

    private const string CarpetaDiagnostico = "Logs/Caras2D";
    private const int Lado = 512;

    [MenuItem("El Sendero/Personajes/Caras 2D/Medir la cara de Will", priority = 40)]
    public static void MedirWill() => Medir("Will", RutaPrefabWill);

    [MenuItem("El Sendero/Personajes/Caras 2D/Foto de la cara de Will", priority = 41)]
    public static void FotografiarWill() => Fotografiar("Will", RutaPrefabWill);

    [MenuItem("El Sendero/Personajes/Caras 2D/Recolocar y conectar la cara de Will", priority = 42)]
    public static void RecolocarWill()
    {
        if (!File.Exists(RutaPrefabWill)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(RutaPrefabWill);
        if (root == null) return;

        CalibrarTransformCara(root.transform, 0.62f, 0.55f, 0.040f, 0.038f, 0.015f);
        string informe = AjustarControladorDeEmociones(root, RutaProfileWill);

        PrefabUtility.SaveAsPrefabAsset(root, RutaPrefabWill);
        PrefabUtility.UnloadPrefabContents(root);

        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CarpetaDiagnostico);
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "controlador_Will.txt"), informe);
        Debug.Log("[Cara2D] Cara 2D de Will recolocada y controlador conectado:\n" + informe);
    }

    /// Vuelca a «Logs/Caras2D/medidas_<nombre>.txt» dónde cae cada pieza de la cabeza y cada
    /// sprite de la cara 2D, en coordenadas de mundo del prefab, para poder compararlas.
    public static string Medir(string nombre, string rutaPrefab)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== Medidas de la cara: {nombre} ===");
        sb.AppendLine($"Prefab: {rutaPrefab}");

        GameObject root = PrefabUtility.LoadPrefabContents(rutaPrefab);
        if (root == null)
        {
            sb.AppendLine("No se pudo cargar el prefab.");
            return Volcar(nombre, sb);
        }

        try
        {
            sb.AppendLine($"Raíz «{root.name}» escala={F(root.transform.lossyScale)}");
            Transform head = BuscarHuesoHead(root.transform);
            if (head == null)
            {
                sb.AppendLine("No encuentro el hueso 'head'.");
                return Volcar(nombre, sb);
            }

            sb.AppendLine();
            sb.AppendLine("-- hueso head --");
            sb.AppendLine($"   posMundo={F(head.position)} escalaMundo={F(head.lossyScale)}");
            sb.AppendLine($"   derecha={F(head.right)} arriba={F(head.up)} frente={F(head.forward)}");

            sb.AppendLine();
            sb.AppendLine("-- piezas de la cabeza (hijos directos del hueso head) --");
            foreach (Transform hijo in head)
            {
                Renderer r = hijo.GetComponent<Renderer>();
                if (r == null) continue;
                sb.AppendLine($"   {hijo.name,-22} activo={Estado(hijo.gameObject.activeSelf)} renderer={Estado(r.enabled)} " +
                              $"centro={F(r.bounds.center)} tamaño={F(r.bounds.size)}");
            }

            sb.AppendLine();
            sb.AppendLine("-- envolvente de la cabeza (sin la cara 2D) --");
            Transform face2D = head.Find("Face2D");
            Bounds craneo = new Bounds();
            bool primero = true;
            int piezas = 0;
            foreach (Renderer r in head.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (face2D != null && (r.transform == face2D || r.transform.IsChildOf(face2D))) continue;
                if (primero) { craneo = r.bounds; primero = false; } else craneo.Encapsulate(r.bounds);
                piezas++;
            }
            sb.AppendLine($"   {piezas} piezas  centro={F(craneo.center)} tamaño={F(craneo.size)}");

            sb.AppendLine();
            if (face2D == null)
            {
                sb.AppendLine("-- NO hay Face2D montado --");
            }
            else
            {
                sb.AppendLine($"-- Face2D: posLocal={F(face2D.localPosition)} escalaMundo={F(face2D.lossyScale)}");
                foreach (Transform hijo in face2D)
                {
                    SpriteRenderer sr = hijo.GetComponent<SpriteRenderer>();
                    if (sr == null) continue;
                    Bounds b = sr.bounds;
                    sb.AppendLine($"   {hijo.name,-22} activo={Estado(hijo.gameObject.activeSelf)} " +
                                  $"sprite={(sr.sprite != null ? sr.sprite.name : "SIN SPRITE")} " +
                                  $"posLocal={F(hijo.localPosition)} escalaLocal={hijo.localScale.x:F3} " +
                                  $"centro={F(b.center)} tamaño={F(b.size)}");
                }
            }
        }
        catch (System.Exception ex)
        {
            sb.AppendLine("ERROR: " + ex.Message + "\n" + ex.StackTrace);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return Volcar(nombre, sb);
    }

    /// Fotos de la cara a «Logs/Caras2D/<nombre>_*.png»: de frente, tres cuartos, perfil, la cabeza
    /// entera, la cara 3D de referencia y una serie con la cara 2D a distintas profundidades.
    /// Se monta todo en una escena de previsualización para no tocar nada de lo que esté abierto.
    public static string Fotografiar(string nombre, string rutaPrefab)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (asset == null) return "no encuentro el prefab " + rutaPrefab;

        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CarpetaDiagnostico);
        Directory.CreateDirectory(carpeta);

        Scene escena = EditorSceneManager.NewPreviewScene();
        GameObject copia = null, luzGo = null, camGo = null;
        Camera cam = null;
        RenderTexture rt = null;
        bool nieblaPrevia = RenderSettings.fog;
        StringBuilder diario = new StringBuilder();
        diario.AppendLine($"=== Fotos de la cara: {nombre} ===");
        try
        {
            copia = PrefabUtility.InstantiatePrefab(asset, escena) as GameObject;
            if (copia == null) return "no se pudo copiar el prefab en la escena de fotos";

            Transform head = BuscarHuesoHead(copia.transform);
            if (head == null) return "no encuentro el hueso head en la copia";
            Transform face2D = head.Find("Face2D");

            Vector3 frente = copia.transform.forward;
            Vector3 arriba = copia.transform.up;

            luzGo = new GameObject("Luz de la foto");
            SceneManager.MoveGameObjectToScene(luzGo, escena);
            Light luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.4f;
            luzGo.transform.rotation = Quaternion.Euler(12f, 165f, 0f);

            camGo = new GameObject("Camara de la foto") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(camGo, escena);
            cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.scene = escena;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            UniversalAdditionalCameraData datosCamara = cam.GetUniversalAdditionalCameraData();
            datosCamara.renderType = CameraRenderType.Base;
            datosCamara.renderPostProcessing = false;
            datosCamara.renderShadows = false;
            nieblaPrevia = RenderSettings.fog;
            RenderSettings.fog = false;

            Bounds ojos = CajaDeParte(head, "Eye01", out bool hayOjos);
            Bounds boca = CajaDeParte(head, "Mouth01", out bool hayBoca);
            Bounds craneo = CajaDeLaCabeza(head);

            Vector3 focoCara = hayOjos && hayBoca ? (ojos.center + boca.center) * 0.5f : craneo.center;
            float anchoCara = Mathf.Max(ojos.size.x * 1.5f, 0.25f);
            float anchoCraneo = Mathf.Max(craneo.size.x * 1.3f, 0.4f);

            rt = new RenderTexture(Lado, Lado, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;

            void Disparar(string toma, Vector3 foco, float ancho, float azimut)
            {
                Vector3 direccion = Quaternion.AngleAxis(azimut, arriba) * frente;
                float distancia = (ancho * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                camGo.transform.position = foco + direccion * distancia;
                camGo.transform.rotation = Quaternion.LookRotation(foco - camGo.transform.position, arriba);
                UniversalRenderPipeline.SingleCameraRequest peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, peticion)) RenderPipeline.SubmitRenderRequest(cam, peticion);
                else cam.Render();
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(Lado, Lado, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string destino = Path.Combine(carpeta, $"{nombre}_{toma}.png");
                File.WriteAllBytes(destino, tex.EncodeToPNG());
                diario.AppendLine($"   {toma,-18} dibujado={PorcentajeDibujado(tex):F1}%   {Path.GetFileName(destino)}");
                Object.DestroyImmediate(tex);
            }

            Disparar("cara_frente", focoCara, anchoCara, 0f);
            Disparar("cara_tres_cuartos", focoCara, anchoCara, 35f);
            Disparar("cara_perfil", focoCara, anchoCara, 80f);
            Disparar("cabeza_frente", craneo.center, anchoCraneo, 0f);

            if (face2D != null)
            {
                face2D.gameObject.SetActive(false);
                Disparar("cara_sin2D", focoCara, anchoCara, 0f);
                face2D.gameObject.SetActive(true);

                float[] adelantos = { 0.000f, 0.010f, 0.020f, 0.040f };
                foreach (float adelanto in adelantos)
                {
                    MoverCara2DAProfundidad(head, face2D, adelanto);
                    Disparar($"cara_adelanto_{adelanto * 1000f:F0}mm", focoCara, anchoCara, 0f);
                }
            }
            else
            {
                diario.AppendLine("   (no hay cara 2D montada)");
            }
        }
        catch (System.Exception ex)
        {
            diario.AppendLine("ERROR: " + ex.Message + "\n" + ex.StackTrace);
        }
        finally
        {
            RenderSettings.fog = nieblaPrevia;
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (luzGo != null) Object.DestroyImmediate(luzGo);
            if (copia != null) Object.DestroyImmediate(copia);
            EditorSceneManager.ClosePreviewScene(escena);
        }

        string rutaDiario = Path.Combine(carpeta, $"fotos_{nombre}.txt");
        File.WriteAllText(rutaDiario, diario.ToString());
        Debug.Log($"[Cara2D] Foto de {nombre}:\n{diario}");
        return rutaDiario;
    }

    /// Cuánta superficie acaba dibujada de verdad, para saber si el render ha salido vacío.
    private static float PorcentajeDibujado(Texture2D tex)
    {
        Color32[] pixeles = tex.GetPixels32();
        int pintados = 0;
        for (int i = 0; i < pixeles.Length; i++)
        {
            Color32 p = pixeles[i];
            if (Mathf.Abs(p.r - 31) > 12 || Mathf.Abs(p.g - 41) > 12 || Mathf.Abs(p.b - 61) > 12) pintados++;
        }
        return 100f * pintados / Mathf.Max(1, pixeles.Length);
    }

    /// Mueve los quads de la cara 2D «adelanto» metros por delante de la cara frontal de su pieza 3D,
    /// conservando el ancho y la altura, para ver a qué profundidad dejan de quedar dentro de la cabeza.
    private static void MoverCara2DAProfundidad(Transform head, Transform face2D, float adelanto)
    {
        Transform raiz = head.root;
        foreach (Transform quad in face2D)
        {
            if (quad.GetComponent<SpriteRenderer>() == null) continue;
            string parte = quad.name.StartsWith("Eye2D_") ? "Eye01" : "Mouth01";
            Bounds caja = CajaDeParte(head, parte, out bool hayParte);
            if (!hayParte) continue;

            Vector3 centro = raiz.InverseTransformPoint(caja.center);
            float mediaProfundidad = Mathf.Abs(Vector3.Dot(raiz.InverseTransformVector(caja.extents), Vector3.forward));
            Vector3 enRaiz = raiz.InverseTransformPoint(head.TransformPoint(quad.localPosition));
            enRaiz.z = centro.z + mediaProfundidad + adelanto;
            quad.localPosition = head.InverseTransformPoint(raiz.TransformPoint(enRaiz));
        }
    }

    private static string Volcar(string nombre, StringBuilder sb)
    {
        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CarpetaDiagnostico);
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta, $"medidas_{nombre}.txt");
        File.WriteAllText(ruta, sb.ToString());
        Debug.Log($"[Cara2D] Medidas de {nombre} en {ruta}");
        return ruta;
    }

    private static string F(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";
    private static string Estado(bool v) => v ? "sí" : "no";
}
