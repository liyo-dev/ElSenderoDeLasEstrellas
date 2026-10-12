#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Mide los muebles y las poses de referencia para colocar los puntos de la mañana.
public static class Cap2PuntosDeLaManana
{
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    [MenuItem("El Sendero/Capítulo 2/Puntos de la mañana: cama y ventana")]
    public static void Aplicar()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Sal de Play para colocar los puntos.");
        var escena = SceneManager.GetSceneByPath(Casa);
        if (!escena.IsValid() || !escena.isLoaded) escena = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        var todos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var sb = new StringBuilder();
        foreach (var t in todos)
        {
            if (t.name.Contains("Bed") || t.name.Contains("Window") || t.name.Contains("Table") || t.name.Contains("Manana") || t.name.Contains("Silla") || t.name.Contains("Chest"))
            {
                sb.AppendLine($"{t.name}: pos={t.position:F3} giro={t.eulerAngles:F2}");
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) sb.AppendLine($"  {r.name}: min={r.bounds.min:F3} max={r.bounds.max:F3}");
            }
            var sleep = t.GetComponent<SleepTrigger>();
            if (sleep != null) sb.AppendLine($"SleepTrigger {t.name}: bed={sleep.bedPosition?.name} pos={sleep.bedPosition?.position:F3} giro={sleep.bedPosition?.eulerAngles:F2} cam={sleep.sleepCameraAnchor?.position:F3}");
        }
        File.WriteAllText("Guiones/WillHouse/Cap2_02_MananaDespues/medidas_INC708.md", sb.ToString());
        var cama = todos.First(t => t.name == "Bed01_a03");
        var referencia = cama.GetComponent<SleepTrigger>();
        var bounds = cama.GetComponentInChildren<Renderer>().bounds;
        var mesh = cama.GetComponentInChildren<MeshFilter>();
        var medida = new GameObject("Medida temporal del colchón") { hideFlags = HideFlags.HideAndDontSave };
        float colchon = referencia.bedPosition.position.y;
        try
        {
            medida.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
            medida.transform.localScale = mesh.transform.lossyScale;
            var collider = medida.AddComponent<MeshCollider>(); collider.sharedMesh = mesh.sharedMesh;
            var rayo = new Ray(new Vector3(bounds.min.x + 0.3f, bounds.max.y + 1f, bounds.center.z), Vector3.down);
            if (collider.Raycast(rayo, out var hit, 4f)) colchon = hit.point.y;
        }
        finally { Object.DestroyImmediate(medida); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL.prefab");
        var pose = Object.Instantiate(prefab); pose.hideFlags = HideFlags.HideAndDontSave;
        string elegido = null; float rootY = 0f, error = float.PositiveInfinity;
        try
        {
            var animador = pose.GetComponentInChildren<Animator>(true);
            var catalogo = CapturaDeEscenario.Catalogo(prefab);
            sb.AppendLine($"Animator {animador.name}, avatar={animador.avatar.name}, padre={animador.transform.parent?.name}, escala={animador.transform.lossyScale}");
            foreach (var r in pose.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (r.enabled) sb.AppendLine($"Malla {r.name}: raíz={r.rootBone?.name}, padre raíz={r.rootBone?.parent?.name}");
            foreach (var tipo in new[] { NPCAmbientActivity.SitLow, NPCAmbientActivity.SitMedium, NPCAmbientActivity.SitHigh })
            {
                string estado = NPCSimpleAnimator.GetActivityLoopState(tipo);
                string clipNombre = catalogo.First(e => e.estado == estado).clip;
                var clip = animador.runtimeAnimatorController.animationClips.First(c => c.name == clipNombre);
                animador.Rebind(); Horno.MuestrearEstado(pose, clip, estado, 0f);
                pose.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                float y = colchon - animador.GetBoneTransform(HumanBodyBones.Hips).position.y + 0.08f;
                float pie = animador.GetBoneTransform(HumanBodyBones.LeftFoot).position.y + y;
                sb.AppendLine($"{tipo}: raíz Y={y:F3}, pie Y={pie:F3}, colchón Y={colchon:F3}");
                sb.AppendLine($"  cabeza={animador.GetBoneTransform(HumanBodyBones.Head).position:F3} cadera={animador.GetBoneTransform(HumanBodyBones.Hips).position:F3} pie={animador.GetBoneTransform(HumanBodyBones.LeftFoot).position:F3}");
                if (Mathf.Abs(pie - 0.08f) < error) { error = Mathf.Abs(pie - 0.08f); elegido = tipo.ToString(); rootY = y; }
            }
        }
        finally { Object.DestroyImmediate(pose); }
        Transform Crear(string nombre, Transform padre, Vector3 pos, Quaternion rot)
        {
            var t = todos.FirstOrDefault(x => x.name == nombre);
            if (t == null) { t = new GameObject(nombre).transform; t.SetParent(padre, true); }
            Undo.RecordObject(t, "Puntos de la mañana"); t.SetPositionAndRotation(pos, rot);
            return t;
        }
        void Asiento(string nombre, Vector3 pos, Quaternion rot, NPCAmbientActivity tipo)
        {
            var t = Crear(nombre, cama, pos, rot);
            var punto = t.GetComponent<NPCWorldPoint>() ?? t.gameObject.AddComponent<NPCWorldPoint>();
            punto.activityType = tipo; punto.interactionPoint = t; punto.overrideFacing = true;
            EditorUtility.SetDirty(punto);
        }
        Asiento("Cama_Will_Borde", new Vector3(bounds.min.x + 0.22f, rootY, bounds.center.z), Quaternion.Euler(0f, 270f, 0f), (NPCAmbientActivity)System.Enum.Parse(typeof(NPCAmbientActivity), elegido));
        Asiento("Cama_Will_Tumbado", referencia.bedPosition.position, referencia.bedPosition.rotation, NPCAmbientActivity.Sleep);
        var raiz = todos.First(x => x.name == "SEQ_MananaDespues");
        Crear("M_Manana_CamaPaso", raiz, new Vector3(bounds.min.x - 0.6f, 0f, bounds.center.z), Quaternion.Euler(0f, 90f, 0f));
        // La ventana junto a la mesa tiene el paso ocupado por su tablero; se usa la siguiente accesible.
        var ventana = todos.First(x => x.name == "Window10_a02 (8)").GetComponent<Renderer>().bounds;
        Crear("M_Manana_Ventana", raiz, new Vector3(ventana.max.x + 0.5f, 0f, ventana.center.z), Quaternion.Euler(0f, 270f, 0f));
        Crear("M_Manana_VentanaFuera", raiz, new Vector3(ventana.min.x - 1f, 1.5f, ventana.center.z), Quaternion.identity);
        var mesa = todos.First(x => x.name == "Table01_a07 (1)").GetComponent<Renderer>().bounds;
        foreach (var n in new[] { "M_Manana_Bollo", "M_Manana_Aro" })
        {
            var marca = todos.First(x => x.name == n); Undo.RecordObject(marca, "Apoyo sobre la mesa");
            marca.position = new Vector3(marca.position.x, mesa.max.y + 0.002f, marca.position.z);
        }
        Crear("M_Manana_DejarBollo", raiz, new Vector3(mesa.max.x + 0.4f, 0f, 3.9f), Quaternion.Euler(0f, 270f, 0f));
        Crear("M_Manana_DejarAro", raiz, new Vector3(mesa.max.x + 0.4f, 0f, 3.1f), Quaternion.Euler(0f, 270f, 0f));
        sb.AppendLine($"Asiento elegido: {elegido}; altura medida del colchón: {colchon:F3} m.");
        File.WriteAllText("Guiones/WillHouse/Cap2_02_MananaDespues/medidas_INC708.md", sb.ToString());
        EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); AssetDatabase.SaveAssets();
    }
}
#endif
