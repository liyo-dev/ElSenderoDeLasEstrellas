using System.Collections.Generic;
using System.Text;
using Sendero.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Monta los retratos vivos del grupo (ver TDD «Retratos del grupo en el HUD»): crea las copias
/// de estudio de Liam, Will y Estela (el prefab del personaje sin nada de juego: solo mallas,
/// Animator, cara y ropa), el Animator del estudio con sus caras y reacciones, y el objeto
/// <see cref="EstudioDeRetratos"/> en Start.unity, lo pone como fuente de imagen del panel del
/// grupo (los retratos horneados quedan de respaldo) y marca `retratosVivos`. Para volver a los
/// horneados, se desmarca ese campo en el panel o se desactiva el componente EstudioDeRetratos.
///
/// Idempotente: rehace las copias desde los prefabs actuales y reutiliza lo que ya existe.
/// </summary>
public static class RetratosVivosBuilder
{
    private const string StartPath = "Assets/Scenes/Systems/Start.unity";
    private const string CarpetaModelos = "Assets/Prefabs/UI/RetratosVivos";
    private const string RutaControlador = "Assets/Art/UI/Retratos/Grupo/RetratoVivo.controller";
    private const string CarpetaAnimaciones = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon";
    private const string NombreEstudio = "EstudioDeRetratos";
    private static readonly Vector3 SitioDelEstudio = new Vector3(0f, -3000f, 0f);

    private static readonly (string prefab, string nombre)[] Fuentes =
    {
        ("Assets/Prefabs/_LIAM.prefab", "Liam"),
        ("Assets/Prefabs/_WILL_NPC.prefab", "Will"),
        ("Assets/Prefabs/_ESTELA.prefab", "Estela"),
    };

    // Estado, animación y velocidad.
    private static readonly (string estado, string clip, float velocidad)[] Estados =
    {
        ("Normal", "Idle_Normal_NoWeapon", 1f),
        ("Cansado", "Dizzy_NoWeapon", 0.8f),
        ("Caido", "Dizzy_NoWeapon", 0.3f),
        ("Contento", "Idle_Battle_NoWeapon", 1f),
        ("Golpe", "GetHit01_NoWeapon", 1f),
        ("Saludo", "Greeting01_NoWeapon", 1f),
        ("Victoria", "Victory_NoWeapon", 1f),
    };

    [MenuItem("El Sendero/UI/Montar retratos vivos (INC-731)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Los retratos vivos se montan fuera de Play."); return; }

        var log = new StringBuilder("=== Retratos vivos del grupo (INC-731) ===\n");
        var avisos = new List<string>();
        CrearCarpeta(CarpetaModelos);
        var controlador = AsegurarControlador(log, avisos);
        var modelos = new GameObject[Fuentes.Length];
        for (int i = 0; i < Fuentes.Length; i++)
            modelos[i] = CrearModelo(Fuentes[i].prefab, Fuentes[i].nombre, controlador, log, avisos);
        AssetDatabase.SaveAssets();

        var scene = SceneManager.GetSceneByPath(StartPath);
        bool abierta = !scene.isLoaded;
        if (abierta) scene = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Additive);
        try
        {
            if (MontarEnEscena(scene, modelos, controlador, log, avisos))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("Start.unity guardada.");
            }
        }
        finally
        {
            if (abierta) EditorSceneManager.CloseScene(scene, true);
        }
        foreach (var a in avisos) log.AppendLine("• " + a);
        if (avisos.Count == 0) Debug.Log(log.ToString()); else Debug.LogWarning(log.ToString());
    }

    // ─── Escena ────────────────────────────────────────────────────────────

    private static bool MontarEnEscena(Scene scene, GameObject[] modelos, RuntimeAnimatorController controlador, StringBuilder log, List<string> avisos)
    {
        PartyHUDPanel panel = null;
        GameObject estudioGo = null;
        foreach (var raiz in scene.GetRootGameObjects())
        {
            if (raiz.name == NombreEstudio) estudioGo = raiz;
            if (panel == null) panel = raiz.GetComponentInChildren<PartyHUDPanel>(true);
        }
        if (panel == null)
        {
            avisos.Add("No encuentro PartyHUDPanel en Start.unity: ejecuta antes «Montar retratos del grupo».");
            return false;
        }
        if (estudioGo == null)
        {
            estudioGo = new GameObject(NombreEstudio);
            SceneManager.MoveGameObjectToScene(estudioGo, scene);
            log.AppendLine("Creado " + NombreEstudio + " en Start.unity.");
        }
        estudioGo.transform.SetPositionAndRotation(SitioDelEstudio, Quaternion.identity);
        var estudio = estudioGo.GetComponent<EstudioDeRetratos>();
        if (estudio == null) estudio = estudioGo.AddComponent<EstudioDeRetratos>();

        var so = new SerializedObject(estudio);
        var lista = so.FindProperty("modelos");
        lista.arraySize = modelos.Length;
        for (int i = 0; i < modelos.Length; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = modelos[i];
        so.FindProperty("controlador").objectReferenceValue = controlador;
        so.ApplyModifiedPropertiesWithoutUndo();

        var soPanel = new SerializedObject(panel);
        soPanel.FindProperty("retratosVivos").boolValue = true;
        soPanel.FindProperty("fuenteDeRetratos").objectReferenceValue = estudio;
        var respaldo = soPanel.FindProperty("retratosDeRespaldo");
        if (respaldo.objectReferenceValue == null)
            respaldo.objectReferenceValue = AssetDatabase.LoadAssetAtPath<JuegoDeRetratos>(RetratosDelGrupoBuilder.RutaJuego);
        soPanel.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("El panel del grupo usa ahora el estudio (respaldo: los retratos fijos).");
        return true;
    }

    // ─── Copias de estudio ─────────────────────────────────────────────────

    private static GameObject CrearModelo(string rutaPrefab, string nombre, RuntimeAnimatorController controlador, StringBuilder log, List<string> avisos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (prefab == null) { avisos.Add("No encuentro " + rutaPrefab + "."); return null; }
        string destino = $"{CarpetaModelos}/RetratoVivo_{nombre}.prefab";
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var copia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            PrefabUtility.UnpackPrefabInstance(copia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copia.name = "RetratoVivo_" + nombre;
            copia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var t in copia.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            var principal = AnimatorPrincipal(copia);
            if (principal == null) { avisos.Add(nombre + ": sin Animator humanoide; no se crea su retrato vivo."); return null; }

            int quitados = QuitarEfectosYCarteles(copia);
            for (int pasada = 0; pasada < 6; pasada++)
            {
                bool quedan = false;
                var todos = copia.GetComponentsInChildren<Component>(true);
                for (int i = todos.Length - 1; i >= 0; i--)
                {
                    var c = todos[i];
                    if (c == null || SeQueda(c, principal)) continue;
                    if (TieneDependientes(c)) { quedan = true; continue; }
                    Object.DestroyImmediate(c);
                    if (c != null) quedan = true; else quitados++;
                }
                if (!quedan) break;
            }

            principal.runtimeAnimatorController = controlador;
            principal.applyRootMotion = false;
            principal.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            var guardado = PrefabUtility.SaveAsPrefabAsset(copia, destino);
            log.AppendLine($"{nombre}: copia de estudio en {destino} ({quitados} componentes de juego quitados).");
            return guardado;
        }
        finally { EditorSceneManager.ClosePreviewScene(escena); }
    }

    /// Efectos de partículas y carteles (Canvas) que cuelgan del personaje: fuera el objeto
    /// entero, salvo que contenga huesos de alguna malla (entonces solo se quitan sus componentes).
    private static int QuitarEfectosYCarteles(GameObject copia)
    {
        var huesos = new HashSet<Transform>();
        foreach (var piel in copia.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            foreach (var h in piel.bones) if (h != null) huesos.Add(h);
            if (piel.rootBone != null) huesos.Add(piel.rootBone);
        }
        var aQuitar = new List<GameObject>();
        foreach (var c in copia.GetComponentsInChildren<Component>(true))
        {
            if (!(c is ParticleSystem) && !(c is Canvas)) continue;
            var go = c.gameObject;
            if (go == copia || aQuitar.Contains(go)) continue;
            bool llevaHuesos = false;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (huesos.Contains(t) || t.GetComponent<SkinnedMeshRenderer>() != null) { llevaHuesos = true; break; }
            if (!llevaHuesos) aQuitar.Add(go);
        }
        int n = 0;
        foreach (var go in aQuitar)
            if (go != null) { Object.DestroyImmediate(go); n++; }
        return n;
    }

    private static bool SeQueda(Component c, Animator principal) =>
        c is Transform || c == principal || c is SkinnedMeshRenderer || c is MeshRenderer || c is MeshFilter
        || c is NPCEmotionController || c is ModularAutoBuilder;

    /// Otro componente del mismo objeto lo exige con [RequireComponent]: se quita primero ese.
    private static bool TieneDependientes(Component c)
    {
        var tipo = c.GetType();
        foreach (var otro in c.GetComponents<Component>())
        {
            if (otro == null || otro == c) continue;
            foreach (RequireComponent req in otro.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if (Exige(req.m_Type0, tipo) || Exige(req.m_Type1, tipo) || Exige(req.m_Type2, tipo)) return true;
        }
        return false;
    }

    private static bool Exige(System.Type requerido, System.Type tipo) => requerido != null && requerido.IsAssignableFrom(tipo);

    private static Animator AnimatorPrincipal(GameObject raiz)
    {
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.gameObject.name.StartsWith("Eye", System.StringComparison.Ordinal)) continue;
            var a = r.GetComponentInParent<Animator>(true);
            if (a != null && a.avatar != null && a.avatar.isHuman) return a;
        }
        foreach (var a in raiz.GetComponentsInChildren<Animator>(true))
            if (a.avatar != null && a.avatar.isHuman) return a;
        return null;
    }

    // ─── Animator del estudio ──────────────────────────────────────────────

    private static RuntimeAnimatorController AsegurarControlador(StringBuilder log, List<string> avisos)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
            log.AppendLine("Creado " + RutaControlador + ".");
        }
        var maquina = ctrl.layers[0].stateMachine;
        foreach (var (estado, nombreClip, velocidad) in Estados)
        {
            AnimatorState s = null;
            foreach (var hijo in maquina.states)
                if (hijo.state.name == estado) { s = hijo.state; break; }
            if (s == null) s = maquina.AddState(estado);
            var clip = BuscarClip(nombreClip);
            if (clip == null) avisos.Add("No encuentro la animación " + nombreClip + " para el estado " + estado + ".");
            s.motion = clip;
            s.speed = velocidad;
            s.writeDefaultValues = true;
            if (estado == "Normal") maquina.defaultState = s;
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static AnimationClip BuscarClip(string nombre)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{CarpetaAnimaciones}/{nombre}.fbx"))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)) return clip;
        return null;
    }

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int i = ruta.LastIndexOf('/');
        CrearCarpeta(ruta.Substring(0, i));
        AssetDatabase.CreateFolder(ruta.Substring(0, i), ruta.Substring(i + 1));
    }
}
