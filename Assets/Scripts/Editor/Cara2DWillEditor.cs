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
    private const string RutaShaderCaraDecal = "El Sendero/Personajes/Face2D_DecalProyectado";
    private const string RutaArchivoShaderCaraDecal = "Assets/Art/Characters/Face2D_DecalProyectado.shader";
    private const string RutaRendererPC = "Assets/Settings/PC_Renderer.asset";
    // Cuánto entra la caja del decal hacia dentro de la cabeza desde el plano del dibujo.
    private const float ProfundidadDeLaCajaDecal = 0.030f;
    private const float DistanciaMaximaDecal = 25f;
    private const float PixelesPorMetroDelDibujo = 250f;

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
    [SerializeField] private float offsetZ = 0.003f;

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
        offsetZ = EditorGUILayout.Slider("Adelanto de la piel (m)", offsetZ, 0.001f, 0.06f);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("�Y'? REAJUSTAR PROPORCIONES Y OCULTAR CEJAS 3D �Y'^", GUILayout.Height(34)))
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
        // Los materiales base de los personajes usan el shader de decal proyectado de la cara 2D.
        Shader shader = BuscarShaderCaraDecal();
        if (shader == null)
        {
            Debug.LogError($"[Cara2D] No encuentro el shader {RutaShaderCaraDecal}.");
            return;
        }

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
            bool ojos = child.name.StartsWith("Eye2D_");
            if (!ojos && !child.name.StartsWith("Mouth2D_")) continue;

            Vector3 plano = ojos ? posOjosFinal : posBocaFinal;
            float escala = ojos ? escOjos : escBoca;

            DecalProjector proyector = child.GetComponent<DecalProjector>();
            if (proyector != null)
            {
                proyector.size = TamanoDeLaCaja(TamañoDelDibujo(child), escala);
                child.localPosition = plano + HaciaDentro(child) * (proyector.size.z * 0.5f);
                continue;
            }

            child.localPosition = plano;
            child.localScale = Vector3.one * escala;
        }
    }

    public static void PrepararSpritesYMaterial(string rutaSprites, string rutaMaterial, string rutaProfile, bool esFemenino)
    {
        string[] nombresOjos = NombresDeOjos(esFemenino);
        string[] nombresBocas = NombresDeBocas();

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

        // El material base usa ya el shader de decal proyectado: de él salen los materiales por dibujo.
        Shader shader = BuscarShaderCaraDecal();
        if (shader == null)
        {
            Debug.LogError($"[Cara2D] No encuentro el shader {RutaShaderCaraDecal}: los decales no se pintarán.");
            return;
        }

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

    public static void AplicarCara2DAPersonaje(string rutaSprites, string rutaPrefab, string rutaProfile, string rutaMaterial, string nombrePersonaje, bool esFemenino, float escOjos = 0.65f, float escBoca = 0.55f, float offYOjos = 0.040f, float offYBoca = 0.038f, float offZ = 0.003f)
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

        string[] nombresOjos = NombresDeOjos(esFemenino);
        foreach (var nom in nombresOjos)
        {
            MontarElementoFacial(face2D, nom, $"{rutaSprites}/{nom}.png", posOjosFinal, rotacionCara, mat, nom == "Eye2D_Neutro", escOjos);
        }

        string[] nombresBocas = NombresDeBocas();
        foreach (var nom in nombresBocas)
        {
            MontarElementoFacial(face2D, nom, $"{rutaSprites}/{nom}.png", posBocaFinal, rotacionCara, mat, nom == "Mouth2D_Neutra", escBoca);
        }

        string informe = AjustarControladorDeEmociones(root, rutaProfile);

        PrefabUtility.SaveAsPrefabAsset(root, rutaPrefab);
        PrefabUtility.UnloadPrefabContents(root);

        // El informe del controlador se guarda junto a las medidas, para poder revisarlo sin abrir Unity.
        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CarpetaDiagnostico);
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, $"controlador_{nombrePersonaje}.txt"), informe);
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

    /// Monta un elemento de la cara 2D (ojos o boca) como proyector de decal sobre la piel.
    /// «posLocal» es el plano del dibujo (donde estaba la cartulina) y «rotLocal» la orientación que
    /// mira al frente: la caja del proyector entra hacia dentro de la cabeza desde ese plano, así que
    /// la piel queda dentro y lo que esté por delante (flequillo, pelo) la tapa en vez de recibirla.
    private static void MontarElementoFacial(Transform parent, string nombre, string rutaSprite, Vector3 posLocal, Quaternion rotLocal, Material matBase, bool activoPorDefecto, float escala = 1f)
    {
        Transform existente = parent.Find(nombre);
        GameObject go = existente != null ? existente.gameObject : new GameObject(nombre);
        go.transform.SetParent(parent, false);

        SpriteRenderer cartulina = go.GetComponent<SpriteRenderer>();
        if (cartulina != null) Object.DestroyImmediate(cartulina, true);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(rutaSprite);
        Vector2 dibujo = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(0.35f, 0.25f);

        // El proyector proyecta a lo largo de su eje Z y dibuja el sprite en su plano XY, con los
        // mismos ejes que tenía la cartulina: se le da su misma orientación.
        go.transform.localRotation = rotLocal;
        go.transform.localScale = Vector3.one;

        DecalProjector proyector = go.GetComponent<DecalProjector>();
        if (proyector == null) proyector = go.AddComponent<DecalProjector>();

        // La cara de la caja que da al frente se queda donde estaba la cartulina y la caja entra
        // hacia dentro de la cabeza: ahí dentro está la piel que hay que pintar.
        proyector.size = TamanoDeLaCaja(dibujo, escala);
        go.transform.localPosition = posLocal + HaciaDentro(go.transform) * (proyector.size.z * 0.5f);

        proyector.material = MaterialDeDecal(rutaSprite, matBase);
        proyector.scaleMode = DecalScaleMode.ScaleInvariant;
        proyector.pivot = Vector3.zero;
        proyector.drawDistance = DistanciaMaximaDecal;
        proyector.fadeScale = 0.5f;
        proyector.startAngleFade = 180f;
        proyector.endAngleFade = 180f;
        proyector.uvScale = Vector2.one;
        proyector.uvBias = Vector2.zero;

        go.SetActive(activoPorDefecto);
    }

    // �"?�"? Decal proyectado: materiales y feature de URP �"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?

    /// Tamaño de la caja del decal (ancho, alto y fondo).
    private static Vector3 TamanoDeLaCaja(Vector2 dibujo, float escala) =>
        new Vector3(dibujo.x * escala, dibujo.y * escala, ProfundidadDeLaCajaDecal);

    /// Shader de la cara 2D: por ruta del proyecto (más fiable que Shader.Find con shaders propios
    /// que no usa nadie más) y, si falla, por nombre.
    private static Shader BuscarShaderCaraDecal()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(RutaArchivoShaderCaraDecal);
        if (shader == null) shader = Shader.Find(RutaShaderCaraDecal);
        return shader;
    }

    /// Nombres de los dibujos de ojos de la cara 2D (sirven para los sprites y para los objetos).
    private static string[] NombresDeOjos(bool esFemenino = false) => new[]
    {
        "Eye2D_Neutro", "Eye2D_Alegre", "Eye2D_Triste",
        esFemenino ? "Eye2D_Enfadada" : "Eye2D_Enfadado",
        esFemenino ? "Eye2D_Sorprendida" : "Eye2D_Sorprendido",
        "Eye2D_Cerrado"
    };

    /// Nombres de los dibujos de bocas de la cara 2D (habla incluida).
    private static string[] NombresDeBocas() => new[]
    {
        "Mouth2D_Neutra", "Mouth2D_Alegre", "Mouth2D_Habla", "Mouth2D_Triste", "Mouth2D_Enfadada", "Mouth2D_Sorprendida"
    };

    [MenuItem("El Sendero/Personajes/Caras 2D/Preparar decals de la cara 2D (URP PC)", priority = 43)]
    public static void PrepararDecalsDeLaCara2D()
    {
        StringBuilder informe = new StringBuilder();
        informe.AppendLine("=== Decals de la cara 2D ===");

        ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RutaRendererPC);
        informe.AppendLine(renderer == null
            ? $"No encuentro el renderer {RutaRendererPC}"
            : AsegurarFeatureDeDecal(renderer));

        Material baseDelPersonaje = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialWill);
        foreach (string nombre in TodosLosNombresDeLaCara())
        {
            Material material = MaterialDeDecal($"{RutaSpritesWill}/{nombre}.png", baseDelPersonaje);
            informe.AppendLine($"material: {(material != null ? AssetDatabase.GetAssetPath(material) : "NO CREADO")}");
        }

        Directory.CreateDirectory(CarpetaDiagnostico);
        File.WriteAllText(Path.Combine(CarpetaDiagnostico, "decals.txt"), informe.ToString());
        Debug.Log("[Cara2D] Decals preparados:\n" + informe);
    }

    private static List<string> TodosLosNombresDeLaCara()
    {
        List<string> todos = new List<string>(NombresDeOjos());
        todos.AddRange(NombresDeBocas());
        return todos;
    }

    /// Material de decal de un dibujo concreto (una expresión): se guarda junto al material base, en
    /// una carpeta «Decals», y sólo se diferencia en la textura.
    private static Material MaterialDeDecal(string rutaSprite, Material baseDelPersonaje)
    {
        if (baseDelPersonaje == null) return null;

        string carpetaBase = Path.GetDirectoryName(AssetDatabase.GetAssetPath(baseDelPersonaje)).Replace('\\', '/');
        string carpeta = carpetaBase + "/Decals";
        if (!AssetDatabase.IsValidFolder(carpeta) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(carpetaBase, "Decals"))) return null;

        string ruta = $"{carpeta}/{Path.GetFileNameWithoutExtension(rutaSprite)}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(baseDelPersonaje);
            AssetDatabase.CreateAsset(material, ruta);
        }

        material.shader = baseDelPersonaje.shader;
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaSprite);
        if (textura != null) material.SetTexture("_BaseMap", textura);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// Deja el renderer de PC con la Renderer Feature de decals, en la técnica de espacio de pantalla
    /// (la que funciona con cualquier shader de piel, sin que éste lea la DBuffer).
    private static string AsegurarFeatureDeDecal(ScriptableRendererData renderer)
    {
        SerializedObject soRenderer = new SerializedObject(renderer);
        SerializedProperty features = soRenderer.FindProperty("m_RendererFeatures");
        SerializedProperty mapa = soRenderer.FindProperty("m_RendererFeatureMap");
        if (features == null || mapa == null) return "el renderer no expone su lista de features";

        for (int i = 0; i < features.arraySize; i++)
        {
            if (features.GetArrayElementAtIndex(i).objectReferenceValue is DecalRendererFeature existente)
            {
                ConfigurarFeatureDeDecal(existente);
                soRenderer.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return "la feature de decals ya estaba: técnica de espacio de pantalla asegurada";
            }
        }

        DecalRendererFeature nueva = ScriptableObject.CreateInstance<DecalRendererFeature>();
        nueva.name = "Decal Renderer Feature";
        nueva.hideFlags |= HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(nueva, renderer);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(nueva, out string guid, out long idLocal);

        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = nueva;
        mapa.arraySize++;
        mapa.GetArrayElementAtIndex(mapa.arraySize - 1).longValue = idLocal;
        soRenderer.ApplyModifiedProperties();

        ConfigurarFeatureDeDecal(nueva);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        return $"feature de decals añadida a {RutaRendererPC} (id={idLocal}, guid={guid})";
    }

    /// Ajustes de la feature: técnica de espacio de pantalla, sin capas de decal (no las admite esa
    /// técnica) y con un alcance corto: la cara 2D sólo interesa de cerca.
    private static void ConfigurarFeatureDeDecal(DecalRendererFeature feature)
    {
        SerializedObject so = new SerializedObject(feature);
        SerializedProperty ajustes = so.FindProperty("m_Settings");
        if (ajustes == null) return;

        SerializedProperty tecnica = ajustes.FindPropertyRelative("technique");
        if (tecnica != null)
        {
            string[] nombres = tecnica.enumNames;
            for (int i = 0; i < nombres.Length; i++)
            {
                if (nombres[i] == "ScreenSpace") tecnica.enumValueIndex = i;
            }
        }

        SerializedProperty alcance = ajustes.FindPropertyRelative("maxDrawDistance");
        if (alcance != null) alcance.floatValue = DistanciaMaximaDecal;

        SerializedProperty capas = ajustes.FindPropertyRelative("decalLayers");
        if (capas != null) capas.boolValue = false;

        so.ApplyModifiedProperties();
    }

    /// Tamaño en metros del dibujo de un elemento de la cara 2D: el de su sprite o, si es un decal
    /// (que no lo referencia), el de su textura a los píxeles por metro con los que se importa.
    private static Vector2 TamañoDelDibujo(Transform elemento)
    {
        SpriteRenderer sr = elemento.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null) return sr.sprite.bounds.size;

        DecalProjector proyector = elemento.GetComponent<DecalProjector>();
        Texture2D textura = proyector != null && proyector.material != null
            ? proyector.material.GetTexture("_BaseMap") as Texture2D
            : null;
        if (textura != null) return new Vector2(textura.width / PixelesPorMetroDelDibujo, textura.height / PixelesPorMetroDelDibujo);

        return proyector != null ? new Vector2(proyector.size.x, proyector.size.y) : new Vector2(0.35f, 0.25f);
    }

    /// Dirección «hacia dentro de la cabeza» de un elemento, en el espacio de su padre (Face2D): el
    /// dibujo mira hacia fuera por su eje Z, así que el fondo de la caja va en sentido contrario.
    private static Vector3 HaciaDentro(Transform elemento) =>
        elemento.GetComponent<DecalProjector>() != null ? elemento.localRotation * Vector3.back : Vector3.zero;

    /// Dónde cae la cara frontal de la caja de un proyector (el plano del dibujo), en mundo.
    private static Vector3 FrenteDeLaCaja(Transform elemento)
    {
        DecalProjector proyector = elemento.GetComponent<DecalProjector>();
        float fondo = proyector != null ? proyector.size.z : ProfundidadDeLaCajaDecal;
        return elemento.position + elemento.rotation * Vector3.forward * (fondo * 0.5f);
    }

    // �"?�"? Medir y ver la cara sin abrir el prefab a mano �"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?�"?

    private const string CarpetaDiagnostico = "Logs/Caras2D";
    private const int Lado = 512;

    [MenuItem("El Sendero/Personajes/Caras 2D/Medir la cara de Will", priority = 40)]
    public static void MedirWill() => Medir("Will", RutaPrefabWill);

    [MenuItem("El Sendero/Personajes/Caras 2D/Foto de la cara de Will", priority = 41)]
    public static void FotografiarWill() => Fotografiar("Will", RutaPrefabWill);

    /// Vuelve a montar la cara 2D de Will entera (proyectores de decal, material por expresión y
    /// controlador) y la deja colocada con las medidas afinadas: así queda como está hoy.
    [MenuItem("El Sendero/Personajes/Caras 2D/Recolocar y conectar la cara de Will", priority = 42)]
    public static void RecolocarWill()
    {
        if (!File.Exists(RutaPrefabWill)) return;
        AplicarCara2DAPersonaje(RutaSpritesWill, RutaPrefabWill, RutaProfileWill, RutaMaterialWill, "Will", false, 0.62f, 0.55f, 0.040f, 0.038f, 0.003f);
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
                    DecalProjector proyector = hijo.GetComponent<DecalProjector>();
                    if (proyector != null)
                    {
                        Texture textura = proyector.material != null ? proyector.material.GetTexture("_BaseMap") : null;
                        sb.AppendLine($"   {hijo.name,-22} activo={Estado(hijo.gameObject.activeSelf)} decal " +
                                      $"dibujo={(textura != null ? textura.name : "SIN TEXTURA")} " +
                                      $"posLocal={F(hijo.localPosition)} tamañoCaja={F(proyector.size)} " +
                                      $"planoMundo={F(FrenteDeLaCaja(hijo))}");
                        continue;
                    }

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

    // La feature de decales de URP puede quedarse apuntando a un gestor de entidades ya liberado: el
    // envoltorio compartido es estático y sobrevive a las recargas del dominio, pero la instancia de
    // la feature vive en el asset del renderer. Al rehacer sus sistemas vuelve a recoger el gestor en
    // uso, y la foto puede dar de alta en él los proyectores de la escena de previsualización.
    private static void ReiniciarSistemasDeDecales()
    {
        const System.Reflection.BindingFlags EnElObjeto = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        System.Type tipoFeature = typeof(DecalProjector).Assembly.GetType("UnityEngine.Rendering.Universal.DecalRendererFeature");
        if (tipoFeature == null) return;

        foreach (Object objeto in Resources.FindObjectsOfTypeAll(tipoFeature))
        {
            if (objeto == null) continue;
            tipoFeature.GetField("m_RecreateSystems", EnElObjeto)?.SetValue(objeto, true);
            tipoFeature.GetField("m_DecalEntityManager", EnElObjeto)?.SetValue(objeto, null);
        }
    }

    /// Fotos de la cara a «Logs/Caras2D/<nombre>_*.png»: de frente, tres cuartos, perfil, la cabeza
    /// entera, la cara 3D de referencia, una por emoción (la del perfil del personaje), el parpadeo y
    /// una serie con la cara 2D a distintas profundidades.
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
            // El pase de decales de URP necesita una textura de color intermedia: sin ella, el
            // render es directo al destino y el pase revienta con una NullReferenceException.
            datosCamara.requiresColorOption = CameraOverrideOption.On;
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

            ReiniciarSistemasDeDecales();

            // Los decales se envían al «siguiente» render de la cámara, así que cada foto necesita un
            // render previo que deje las llamadas preparadas.
            void RenderDeCalentamiento()
            {
                RenderTexture calentamiento = RenderTexture.GetTemporary(8, 8, 24, RenderTextureFormat.ARGB32);
                RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = calentamiento });
                RenderTexture.ReleaseTemporary(calentamiento);
            }

            // Primer render: URP crea el gestor de entidades de los decales. Después hay que dar de
            // alta en él los proyectores de esta copia (los de la escena de previsualización no los
            // encuentra el barrido que hace URP al crear el gestor).
            RenderDeCalentamiento();
            foreach (DecalProjector proyector in copia.GetComponentsInChildren<DecalProjector>(true))
            {
                if (!proyector.gameObject.activeInHierarchy) continue;
                proyector.enabled = false;
                proyector.enabled = true;
            }

            void Disparar(string toma, Vector3 foco, float ancho, float azimut)
            {
                Vector3 direccion = Quaternion.AngleAxis(azimut, arriba) * frente;
                float distancia = (ancho * 0.5f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                camGo.transform.position = foco + direccion * distancia;
                camGo.transform.rotation = Quaternion.LookRotation(foco - camGo.transform.position, arriba);
                RenderDeCalentamiento();
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

            // Una foto por emoción, activando las mismas mallas que el perfil le da al controlador en
            // Play: así cada cara se puede comparar con la hoja de expresiones del personaje.
            NPCEmotionController controlador = copia.GetComponentInChildren<NPCEmotionController>(true);
            if (face2D != null && controlador != null && controlador.EmotionProfile != null)
            {
                EmotionProfile perfil = controlador.EmotionProfile;
                SerializedObject soControlador = new SerializedObject(controlador);
                string prefijoOjos = soControlador.FindProperty("eyePrefix").stringValue;
                string prefijoBocas = soControlador.FindProperty("mouthPrefix").stringValue;
                string ojosCerrados = soControlador.FindProperty("ojosDelParpadeo").stringValue;

                string[] nombres = { "neutro", "alegre", "triste", "enfadado", "sorprendido" };
                NPCEmotion[] emociones = { NPCEmotion.Neutral, NPCEmotion.Happy, NPCEmotion.Sad, NPCEmotion.Angry, NPCEmotion.Surprised };

                for (int i = 0; i < emociones.Length; i++)
                {
                    EmotionMeshData datos = perfil.GetEmotionData(emociones[i]);
                    string nombreOjos = string.IsNullOrEmpty(datos.eyeMeshName) ? perfil.ojosDeReposoPorDefecto : datos.eyeMeshName;
                    string nombreBoca = string.IsNullOrEmpty(datos.mouthMeshName) ? perfil.bocaDeReposoPorDefecto : datos.mouthMeshName;
                    if (!ActivarSoloMallaDeCara(face2D, prefijoOjos, nombreOjos) || !ActivarSoloMallaDeCara(face2D, prefijoBocas, nombreBoca))
                    {
                        diario.AppendLine($"   cara_{nombres[i]}: el perfil pide «{nombreOjos}» / «{nombreBoca}» y no están en la cara");
                        continue;
                    }
                    Disparar($"cara_{nombres[i]}", focoCara, anchoCara, 0f);
                }

                // Parpadeo: los ojos cerrados con la boca de reposo.
                ActivarSoloMallaDeCara(face2D, prefijoOjos, ojosCerrados);
                ActivarSoloMallaDeCara(face2D, prefijoBocas, perfil.bocaDeReposoPorDefecto);
                Disparar("cara_parpadeo", focoCara, anchoCara, 0f);

                // La copia se deja en reposo antes del resto de tomas.
                ActivarSoloMallaDeCara(face2D, prefijoOjos, perfil.ojosDeReposoPorDefecto);
                ActivarSoloMallaDeCara(face2D, prefijoBocas, perfil.bocaDeReposoPorDefecto);
            }

            if (face2D != null)
            {
                face2D.gameObject.SetActive(false);
                Disparar("cara_sin2D", focoCara, anchoCara, 0f);
                face2D.gameObject.SetActive(true);

                float[] adelantos = { 0.000f, 0.003f, 0.010f, 0.020f, 0.040f };
                foreach (float adelanto in adelantos)
                {
                    MoverCara2DAProfundidad(head, face2D, adelanto);
                    string etiqueta = $"cara_adelanto_{adelanto * 1000f:F0}mm";
                    Disparar(etiqueta, focoCara, anchoCara, 0f);
                    Disparar($"{etiqueta}_perfil", focoCara, anchoCara, 65f);
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

    /// Deja activa sólo la malla indicada dentro de un grupo de la cara 2D (ojos o bocas), igual que
    /// hace el controlador de emociones en Play. Devuelve false si el nombre no existe en la cara.
    private static bool ActivarSoloMallaDeCara(Transform face2D, string prefijo, string nombre)
    {
        if (face2D == null || string.IsNullOrEmpty(nombre)) return false;

        bool encontrada = false;
        foreach (Transform hijo in face2D)
        {
            if (!hijo.name.StartsWith(prefijo)) continue;
            bool activar = hijo.name == nombre;
            if (activar) encontrada = true;
            if (hijo.gameObject.activeSelf != activar) hijo.gameObject.SetActive(activar);
        }
        return encontrada;
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
        foreach (Transform elemento in face2D)
        {
            bool esOjos = elemento.name.StartsWith("Eye2D_");
            bool esBoca = elemento.name.StartsWith("Mouth2D_");
            if (!esOjos && !esBoca) continue;
            if (elemento.GetComponent<DecalProjector>() == null && elemento.GetComponent<SpriteRenderer>() == null) continue;

            string parte = esOjos ? "Eye01" : "Mouth01";
            Bounds caja = CajaDeParte(head, parte, out bool hayParte);
            if (!hayParte) continue;

            Vector3 centro = raiz.InverseTransformPoint(caja.center);
            float mediaProfundidad = Mathf.Abs(Vector3.Dot(raiz.InverseTransformVector(caja.extents), Vector3.forward));
            Vector3 enRaiz = raiz.InverseTransformPoint(head.TransformPoint(elemento.localPosition));
            enRaiz.z = centro.z + mediaProfundidad + adelanto;
            Vector3 plano = head.InverseTransformPoint(raiz.TransformPoint(enRaiz));

            // En un proyector, «adelanto» es dónde queda la cara frontal de la caja; la caja va hacia dentro.
            DecalProjector proyector = elemento.GetComponent<DecalProjector>();
            elemento.localPosition = plano + HaciaDentro(elemento) * (proyector != null ? proyector.size.z * 0.5f : 0f);
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
