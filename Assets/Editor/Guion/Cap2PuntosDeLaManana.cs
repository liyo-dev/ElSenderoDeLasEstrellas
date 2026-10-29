#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Coloca en la cama de Will los dos sitios de la mañana después (sentado en el filo y recostado
/// boca arriba) midiendo el colchón y las poses de Will, asegura el post-procesado de flashback
/// y la separación en la mesa entre el bollo y el aro.
/// Ver INC-718 e INC-846.
public static class Cap2PuntosDeLaManana
{
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Medidas = "Guiones/WillHouse/Cap2_02_MananaDespues/medidas_INC718.md";
    const string PoseRecostado = "Knockdown01_Ground";
    public const string PerfilFlashback = "Assets/_SEQUENCES/Perfiles/Flashback_Recuerdo.asset";
    public const string PerfilDefecto = "Assets/DefaultVolumeProfile.asset";

    static readonly string[] MarcasSinUso =
    {
        "Cama_Will_Tumbado", "M_Manana_CamaPaso", "M_Manana_Ventana", "M_Manana_VentanaFuera",
        "M_Manana_DejarBollo", "M_Manana_DejarAro"
    };

    [MenuItem("El Sendero/Capítulo 2/Puntos de la mañana: cama y mesa")]
    [MenuItem("El Sendero/Archivo/Capítulo 2/Puntos de la mañana: cama")]
    [MenuItem("El Sendero/Cinematicas/Puntos Manana")]
    public static void Aplicar()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Sal de Play para colocar los puntos.");
        var escena = SceneManager.GetSceneByPath(Casa);
        if (!escena.IsValid() || !escena.isLoaded) escena = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var sb = new StringBuilder();

        var cama = todos.First(t => t != null && t.name == "Bed01_a03");
        var bounds = cama.GetComponentInChildren<Renderer>().bounds;
        float colchon = AlturaDelColchon(cama, bounds);
        sb.AppendLine($"Cama: min={bounds.min:F3} max={bounds.max:F3} · colchón Y={colchon:F3}");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL.prefab");
        var catalogo = CapturaDeEscenario.Catalogo(prefab);
        var pose = Object.Instantiate(prefab);
        pose.hideFlags = HideFlags.HideAndDontSave;
        NPCAmbientActivity asiento = NPCAmbientActivity.SitMedium;
        float raizSentado = 0f;
        float posXSentado = 0f;
        Vector3 raizRecostado = Vector3.zero;
        float rumboRecostado = 270f;
        try
        {
            var animador = pose.GetComponentInChildren<Animator>(true);
            Transform Hueso(HumanBodyBones h) => animador.GetBoneTransform(h);
            void Muestrear(string estado, float rumbo)
            {
                string clip = catalogo.First(e => e.estado == estado).clip;
                var c = animador.runtimeAnimatorController.animationClips.First(x => x.name == clip);
                animador.Rebind();
                Horno.MuestrearEstado(pose, c, estado, 0f);
                pose.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, rumbo, 0f));
                animador.Update(0f);
            }

            // Sentado en el filo: pose de silla (SitMedium). Calibrado a X=116.68f y Y=0.585f para que las
            // piernas caigan limpias por fuera del borde sin meterse dentro de la madera ni del colchón.
            Muestrear(NPCSimpleAnimator.GetActivityLoopState(asiento), 0f);
            Vector3 caderaSentado = Hueso(HumanBodyBones.Hips).position;
            raizSentado = 0.585f;
            posXSentado = 116.68f;
            sb.AppendLine($"{asiento}: raíz=({posXSentado:F3}, {raizSentado:F3}, {bounds.center.z:F3}), cadera Y={raizSentado + caderaSentado.y:F3}");

            // Recostado boca arriba de través: cabecea hacia la pared este.
            // Calibrado a Y=0.780f para que la espalda y nalgas descansen SOBRE el edredón sin atravesarlo.
            Muestrear(PoseRecostado, rumboRecostado);
            if (Hueso(HumanBodyBones.Head).position.x < Hueso(HumanBodyBones.LeftFoot).position.x)
            {
                rumboRecostado = 90f;
                Muestrear(PoseRecostado, rumboRecostado);
            }
            Vector3 cabeza = Hueso(HumanBodyBones.Head).position;
            Vector3 cadera = Hueso(HumanBodyBones.Hips).position;
            sb.AppendLine($"{PoseRecostado} (rumbo {rumboRecostado}): cabeza={cabeza:F3} cadera={cadera:F3}");
            raizRecostado = new Vector3(117.15f, 0.780f, 0.40f);
        }
        finally { Object.DestroyImmediate(pose); }

        foreach (var nombre in MarcasSinUso)
        {
            var sobra = todos.FirstOrDefault(x => x != null && x.name == nombre);
            if (sobra != null) Undo.DestroyObjectImmediate(sobra.gameObject);
        }

        // Posición del asiento adentrada en el colchón (hacia el este) para que no quede colgando fuera.
        // Will sentado al sur del filo (Z=0.40f), orientado en 3/4 hacia el norte (315°), mirando a Eldran:
        var borde = Crear(todos, "Cama_Will_Borde", cama,
            new Vector3(posXSentado, raizSentado, 0.40f), Quaternion.Euler(0f, 315f, 0f));
        var punto = borde.GetComponent<NPCWorldPoint>() ?? borde.gameObject.AddComponent<NPCWorldPoint>();
        punto.activityType = asiento; punto.interactionPoint = borde; punto.overrideFacing = true;
        EditorUtility.SetDirty(punto);

        // Asiento de Eldran en el filo de la cama separado más de 1 m (Z=1.45f), orientado en 3/4 hacia el sur (225°), mirando a Will:
        var bordeEldran = Crear(todos, "Cama_Eldran_Borde", cama,
            new Vector3(posXSentado, raizSentado, 1.45f), Quaternion.Euler(0f, 225f, 0f));
        var puntoEldran = bordeEldran.GetComponent<NPCWorldPoint>() ?? bordeEldran.gameObject.AddComponent<NPCWorldPoint>();
        puntoEldran.activityType = asiento; puntoEldran.interactionPoint = bordeEldran; puntoEldran.overrideFacing = true;
        EditorUtility.SetDirty(puntoEldran);

        var raiz = todos.First(x => x != null && x.name == "SEQ_MananaDespues");
        Crear(todos, "M_Manana_WillRecostado", raiz, raizRecostado, Quaternion.Euler(0f, rumboRecostado, 0f));
        Crear(todos, "M_Manana_EldranCamaPaso", raiz, new Vector3(115.85f, 0f, 1.45f), Quaternion.Euler(0f, 90f, 0f));

        var mesa = todos.First(x => x != null && x.name == "Table01_a07 (1)").GetComponent<Renderer>().bounds;
        float altoMesa = mesa.max.y + 0.002f;

        // Separación limpia en la mesa: bollo a z=2.65, aro a z=3.15 (a 50 cm de distancia, nunca se tocan)
        var marcaBollo = todos.FirstOrDefault(x => x != null && x.name == "M_Manana_Bollo");
        if (marcaBollo != null)
        {
            Undo.RecordObject(marcaBollo, "Apoyo bollo");
            marcaBollo.position = new Vector3(114.5f, altoMesa, 2.65f);
        }
        var marcaAro = todos.FirstOrDefault(x => x != null && x.name == "M_Manana_Aro");
        if (marcaAro != null)
        {
            Undo.RecordObject(marcaAro, "Apoyo aro");
            marcaAro.position = new Vector3(114.6f, altoMesa, 3.15f);
        }

        // Asegurar perfil de postproceso para el flashback del recuerdo
        AsegurarPerfilFlashback();

        // Asegurar controlador de postproceso en la escena
        AsegurarPostprocesoEnEscena(escena, todos);

        sb.AppendLine($"Asiento en el filo: {asiento}, raíz=({posXSentado:F3}, {raizSentado:F3}, {bounds.center.z:F3}). Recostado: raíz={raizRecostado:F3}, rumbo {rumboRecostado}.");
        sb.AppendLine($"Mesa alto={altoMesa:F3}. Bollo en z=2.65, Aro en z=3.15.");
        File.WriteAllText(Medidas, sb.ToString());
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
    }

    static void AsegurarPerfilFlashback()
    {
        string dir = Path.GetDirectoryName(PerfilFlashback);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PerfilFlashback);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, PerfilFlashback);
        }
        foreach (var c in perfil.components)
        {
            if (c != null) Object.DestroyImmediate(c, true);
        }
        perfil.components.Clear();

        if (!perfil.TryGet<ColorAdjustments>(out var colorAdj))
        {
            colorAdj = perfil.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(colorAdj, perfil);
        }
        colorAdj.saturation.overrideState = true;
        colorAdj.saturation.value = -85f;
        colorAdj.contrast.overrideState = true;
        colorAdj.contrast.value = 25f;
        colorAdj.colorFilter.overrideState = true;
        colorAdj.colorFilter.value = new Color(0.85f, 0.88f, 0.95f, 1f);

        if (!perfil.TryGet<Vignette>(out var vig))
        {
            vig = perfil.Add<Vignette>(true);
            AssetDatabase.AddObjectToAsset(vig, perfil);
        }
        vig.intensity.overrideState = true;
        vig.intensity.value = 0.38f;
        vig.smoothness.overrideState = true;
        vig.smoothness.value = 0.45f;

        if (!perfil.TryGet<FilmGrain>(out var grain))
        {
            grain = perfil.Add<FilmGrain>(true);
            AssetDatabase.AddObjectToAsset(grain, perfil);
        }
        grain.intensity.overrideState = true;
        grain.intensity.value = 0.45f;
        grain.response.overrideState = true;
        grain.response.value = 0.8f;

        EditorUtility.SetDirty(perfil);
    }

    static void AsegurarPostprocesoEnEscena(Scene escena, System.Collections.Generic.List<Transform> todos)
    {
        var goPost = todos.FirstOrDefault(x => x != null && x.name == "POSTPROCESO_CASA")?.gameObject;
        if (goPost == null)
        {
            goPost = new GameObject("POSTPROCESO_CASA");
            Undo.RegisterCreatedObjectUndo(goPost, "Crear POSTPROCESO_CASA");
            SceneManager.MoveGameObjectToScene(goPost, escena);
        }

        var vol = goPost.GetComponent<Volume>() ?? goPost.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        vol.weight = 1f;

        var perfilBase = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PerfilDefecto);
        vol.sharedProfile = perfilBase;

        var post = goPost.GetComponent<PostprocesoDeEscena>() ?? goPost.AddComponent<PostprocesoDeEscena>();
        post.perfilInicial = perfilBase;
        post.exclusivo = true;
        post.entrada = 1.0f;

        EditorUtility.SetDirty(vol);
        EditorUtility.SetDirty(post);
    }

    static float AlturaDelColchon(Transform cama, Bounds bounds)
    {
        var mesh = cama.GetComponentInChildren<MeshFilter>();
        var medida = new GameObject("Medida temporal del colchón") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            medida.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
            medida.transform.localScale = mesh.transform.lossyScale;
            var collider = medida.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
            var rayo = new Ray(new Vector3(bounds.min.x + 0.3f, bounds.max.y + 1f, bounds.center.z), Vector3.down);
            return collider.Raycast(rayo, out var hit, 4f) ? hit.point.y : bounds.min.y + 0.5f;
        }
        finally { Object.DestroyImmediate(medida); }
    }

    static Transform Crear(System.Collections.Generic.List<Transform> todos, string nombre, Transform padre, Vector3 pos, Quaternion rot)
    {
        var t = todos.FirstOrDefault(x => x != null && x.name == nombre);
        if (t == null) { t = new GameObject(nombre).transform; Undo.RegisterCreatedObjectUndo(t.gameObject, nombre); t.SetParent(padre, true); }
        Undo.RecordObject(t, "Puntos de la mañana");
        t.SetPositionAndRotation(pos, rot);
        return t;
    }
}
#endif
