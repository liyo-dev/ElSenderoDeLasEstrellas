#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arreglo temporal de un solo uso (se borra en cuanto se aplica).
[InitializeOnLoad]
static class TmpConsultaRampa
{
    static double s_t;
    static TmpConsultaRampa() { s_t = EditorApplication.timeSinceStartup + 3; EditorApplication.update += Esperar; }
    static void Esperar()
    {
        if (EditorApplication.timeSinceStartup < s_t || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= Esperar;
        try { Hacer(); } catch (System.Exception e) { File.WriteAllText("Guiones/_informes/tmp_rampa2.txt", e.ToString()); }
    }
    static void Hacer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { File.WriteAllText("Guiones/_informes/tmp_rampa2.txt", "en Play, no hago nada"); return; }
        var sb = new System.Text.StringBuilder();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.name != "Rampa de acceso al muelle") continue;
            var esc = t.gameObject.scene;
            bool sucia = esc.isDirty;
            sb.AppendLine($"antes pos={t.position:F3} rot={t.eulerAngles:F2} escala={t.localScale:F3} escenaSucia={sucia} padre={t.parent?.name} padreEsc={t.parent?.lossyScale:F2}");
            if (Mathf.Abs(t.position.z + 455.5f) > 0.05f) { sb.AppendLine("ya movida, no toco"); continue; }
            Undo.RecordObject(t, "Rampa del muelle a ras de la plaza");
            // Arranca donde la plaza empieza a bajar (z -452.5, a ras del terreno) y acaba a la altura de los tablones (z -464, y 3,75).
            const float zS = -452.5f, yS = 4.90f, zE = -464f, yE = 3.75f, grosor = 0.35f;
            float dz = zS - zE, dy = yS - yE, ang = Mathf.Atan2(dy, dz), largo = Mathf.Sqrt(dz * dz + dy * dy);
            var n = new Vector3(0f, Mathf.Cos(ang), -Mathf.Sin(ang));
            var medio = new Vector3(270f, (yS + yE) * 0.5f, (zS + zE) * 0.5f) - n * (grosor * 0.5f);
            t.position = medio;
            t.rotation = Quaternion.Euler(-ang * Mathf.Rad2Deg, 0f, 0f);
            var ls = t.localScale; t.localScale = new Vector3(ls.x, ls.y, largo);
            EditorSceneManager.MarkSceneDirty(esc);
            sb.AppendLine($"despues pos={t.position:F3} rot={t.eulerAngles:F2} escala={t.localScale:F3}");
            if (!sucia) { bool ok = EditorSceneManager.SaveScene(esc); sb.AppendLine("escena guardada=" + ok); }
            else sb.AppendLine("la escena ya tenía cambios sin guardar: no la guardo");
            var r = t.GetComponent<Renderer>();
            if (r) sb.AppendLine($"bounds={r.bounds.center:F2}/{r.bounds.size:F2}");
        }
        File.WriteAllText("Guiones/_informes/tmp_rampa2.txt", sb.ToString());
    }
}
#endif
