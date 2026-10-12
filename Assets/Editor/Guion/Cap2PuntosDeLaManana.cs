#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Coloca en la cama de Will los dos sitios de la mañana después (sentado en el filo y recostado
/// boca arriba) midiendo el colchón y las poses de Will, y retira las marcas que el guion ya no usa.
/// Ver INC-718.
public static class Cap2PuntosDeLaManana
{
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Medidas = "Guiones/WillHouse/Cap2_02_MananaDespues/medidas_INC718.md";
    const string PoseRecostado = "Knockdown01_Ground";
    static readonly string[] MarcasSinUso =
    {
        "Cama_Will_Tumbado", "M_Manana_CamaPaso", "M_Manana_Ventana", "M_Manana_VentanaFuera",
        "M_Manana_DejarBollo", "M_Manana_DejarAro"
    };

    [MenuItem("El Sendero/Archivo/Capítulo 2/Puntos de la mañana: cama")]
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
        float atrasCadera = 0f;
        float raizSentado = 0f;
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

            // Sentado en el filo: pose de silla (SitMedium), con la cadera justo dentro del colchón y
            // las piernas colgando por fuera. Los chibis tienen las piernas cortas: los pies no llegan al suelo.
            Muestrear(NPCSimpleAnimator.GetActivityLoopState(asiento), 0f);
            Vector3 caderaSentado = Hueso(HumanBodyBones.Hips).position;
            raizSentado = colchon + 0.08f - caderaSentado.y;
            atrasCadera = -caderaSentado.z;
            sb.AppendLine($"{asiento}: cadera={caderaSentado:F3}, raíz Y={raizSentado:F3}, pies Y={Hueso(HumanBodyBones.LeftFoot).position.y + raizSentado:F3}");

            // Recostado boca arriba de través: de cara al oeste como sentado en el filo, cae hacia atrás
            // y la cabeza queda hacia la pared. Si la pose cae hacia delante, se gira media vuelta.
            Muestrear(PoseRecostado, rumboRecostado);
            if (Hueso(HumanBodyBones.Head).position.x < Hueso(HumanBodyBones.LeftFoot).position.x)
            {
                rumboRecostado = 90f;
                Muestrear(PoseRecostado, rumboRecostado);
            }
            Vector3 cabeza = Hueso(HumanBodyBones.Head).position;
            Vector3 cadera = Hueso(HumanBodyBones.Hips).position;
            float pies = Mathf.Min(Hueso(HumanBodyBones.LeftFoot).position.x, Hueso(HumanBodyBones.RightFoot).position.x);
            float suelo = pose.GetComponentsInChildren<SkinnedMeshRenderer>(false).Where(r => r.enabled)
                .Select(r => r.bounds.min.y).DefaultIfEmpty(cadera.y - 0.1f).Min();
            sb.AppendLine($"{PoseRecostado} (rumbo {rumboRecostado}): cabeza={cabeza:F3} cadera={cadera:F3} pies x={pies:F3} malla min y={suelo:F3}");
            // Los pies quedan justo dentro del filo y la espalda apoyada en el colchón.
            raizRecostado = new Vector3(bounds.min.x + 0.12f - pies, colchon - suelo, bounds.center.z - cadera.z);
            float cabezaX = raizRecostado.x + cabeza.x;
            if (cabezaX > bounds.max.x - 0.15f)
                sb.AppendLine($"AVISO: la cabeza queda en x={cabezaX:F3}, cerca del borde de la pared ({bounds.max.x:F3}).");
        }
        finally { Object.DestroyImmediate(pose); }

        foreach (var nombre in MarcasSinUso)
        {
            var sobra = todos.FirstOrDefault(x => x != null && x.name == nombre);
            if (sobra != null) Undo.DestroyObjectImmediate(sobra.gameObject);
        }

        var borde = Crear(todos, "Cama_Will_Borde", cama,
            new Vector3(bounds.min.x + 0.1f - atrasCadera, raizSentado, bounds.center.z), Quaternion.Euler(0f, 270f, 0f));
        var punto = borde.GetComponent<NPCWorldPoint>() ?? borde.gameObject.AddComponent<NPCWorldPoint>();
        punto.activityType = asiento; punto.interactionPoint = borde; punto.overrideFacing = true;
        EditorUtility.SetDirty(punto);

        var raiz = todos.First(x => x != null && x.name == "SEQ_MananaDespues");
        Crear(todos, "M_Manana_WillRecostado", raiz, raizRecostado, Quaternion.Euler(0f, rumboRecostado, 0f));

        var mesa = todos.First(x => x != null && x.name == "Table01_a07 (1)").GetComponent<Renderer>().bounds;
        foreach (var n in new[] { "M_Manana_Bollo", "M_Manana_Aro" })
        {
            var marca = todos.First(x => x != null && x.name == n);
            Undo.RecordObject(marca, "Apoyo sobre la mesa");
            marca.position = new Vector3(marca.position.x, mesa.max.y + 0.002f, marca.position.z);
        }

        sb.AppendLine($"Asiento en el filo: {asiento}, raíz Y={raizSentado:F3}. Recostado: raíz={raizRecostado:F3}, rumbo {rumboRecostado}.");
        File.WriteAllText(Medidas, sb.ToString());
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
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
