#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Diagnóstico en Play: quién gira la cabeza y el cuello de cada personaje humanoide. Pone tres sondas
/// (SondaDeCabeza, SondaDeCabezaTrasQuieta, SondaDeCabezaFinal) en cada Animator humanoide de las escenas
/// abiertas, mide unos segundos y apunta, por personaje, el mayor ángulo de cada tramo:
///   animación  pose del Animator frente a la de reposo del avatar (lo que pone el clip);
///   quieta     lo que cambia CabezaQuietaAlHablar;
///   resto      lo que cambia el código que corre después (mirada, sentado, gestos procedurales…);
///   total      pose final frente a la de reposo.
/// El informe va a Guiones/_informes/sonda_de_cabeza.md y a la consola. Al acabar quita las sondas.
public static class SondaDeCabezaMenu
{
    private const float Segundos = 4f;
    private const string Informe = "Guiones/_informes/sonda_de_cabeza.md";

    private sealed class Medida
    {
        public string nombre;
        public SondaDeCabeza animada, media, final;
        public Quaternion reposoCabeza = Quaternion.identity, reposoCuello = Quaternion.identity;
        public float anim, quieta, resto, total, cuelloAnim, cuelloTotal;
        public string estados = "";
        public Animator animador;
    }

    private static readonly List<Medida> s_medidas = new();
    private static double s_fin;

    [MenuItem("El Sendero/Personajes/Diagnóstico/Quién mueve la cabeza (en Play)", priority = 900)]
    public static void Medir()
    {
        if (!Application.isPlaying) { OrdenesDelEditor.Avisar("Quién mueve la cabeza", "Hay que estar en Play."); return; }
        Quitar();
        foreach (var a in Object.FindObjectsByType<Animator>())
        {
            if (a == null || !a.isHuman || a.avatar == null || a.GetBoneTransform(HumanBodyBones.Head) == null) continue;
            var m = new Medida { nombre = Ruta(a.transform), animador = a };
            m.animada = a.gameObject.AddComponent<SondaDeCabeza>();
            m.media = a.gameObject.AddComponent<SondaDeCabezaTrasQuieta>();
            m.final = a.gameObject.AddComponent<SondaDeCabezaFinal>();
            var cabeza = a.GetBoneTransform(HumanBodyBones.Head).name;
            var cuello = a.GetBoneTransform(HumanBodyBones.Neck);
            foreach (var hueso in a.avatar.humanDescription.skeleton)
            {
                if (hueso.name == cabeza) m.reposoCabeza = hueso.rotation;
                if (cuello != null && hueso.name == cuello.name) m.reposoCuello = hueso.rotation;
            }
            s_medidas.Add(m);
        }
        s_fin = EditorApplication.timeSinceStartup + Segundos;
        EditorApplication.update -= Tomar;
        EditorApplication.update += Tomar;
        Debug.Log($"[Quién mueve la cabeza] midiendo {s_medidas.Count} personajes durante {Segundos} s…");
    }

    private static void Tomar()
    {
        if (!Application.isPlaying) { Quitar(); return; }
        foreach (var m in s_medidas)
        {
            if (m.final == null || m.final.Medidas == 0) continue;
            float anim = Quaternion.Angle(m.reposoCabeza, m.animada.CabezaLocal);
            float total = Quaternion.Angle(m.reposoCabeza, m.final.CabezaLocal);
            if (anim > m.anim) { m.anim = anim; m.estados = Estados(m.animador); }
            m.quieta = Mathf.Max(m.quieta, Quaternion.Angle(m.animada.CabezaLocal, m.media.CabezaLocal));
            m.resto = Mathf.Max(m.resto, Quaternion.Angle(m.media.CabezaLocal, m.final.CabezaLocal));
            m.total = Mathf.Max(m.total, total);
            m.cuelloAnim = Mathf.Max(m.cuelloAnim, Quaternion.Angle(m.reposoCuello, m.animada.CuelloLocal));
            m.cuelloTotal = Mathf.Max(m.cuelloTotal, Quaternion.Angle(m.reposoCuello, m.final.CuelloLocal));
        }
        if (EditorApplication.timeSinceStartup < s_fin) return;
        EditorApplication.update -= Tomar;
        Escribir();
        Quitar();
    }

    private static void Escribir()
    {
        s_medidas.Sort((a, b) => b.total.CompareTo(a.total));
        var sb = new StringBuilder();
        sb.AppendLine("# Quién mueve la cabeza").AppendLine();
        sb.AppendLine("Mayor ángulo (grados) en la medida, por personaje. animación = clip frente al reposo del avatar; quieta = CabezaQuietaAlHablar; resto = código posterior (mirada, sentado, gestos); total = final frente al reposo.").AppendLine();
        sb.AppendLine("| Personaje | cabeza: animación | quieta | resto | total | cuello: animación | total | estados con más giro del clip |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var m in s_medidas)
            sb.AppendLine($"| {m.nombre} | {m.anim:0} | {m.quieta:0} | {m.resto:0} | {m.total:0} | {m.cuelloAnim:0} | {m.cuelloTotal:0} | {m.estados} |");
        Directory.CreateDirectory(Path.GetDirectoryName(Informe));
        File.WriteAllText(Informe, sb.ToString());
        Debug.Log("[Quién mueve la cabeza]\n" + sb);
    }

    private static string Estados(Animator a)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < a.layerCount; i++)
        {
            if (i > 0 && a.GetLayerWeight(i) <= 0.01f) continue;
            var clips = a.GetCurrentAnimatorClipInfo(i);
            if (clips.Length > 0 && clips[0].clip != null) sb.Append(a.GetLayerName(i)).Append(':').Append(clips[0].clip.name).Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static void Quitar()
    {
        foreach (var m in s_medidas)
        {
            if (m.animada != null) Object.Destroy(m.animada);
            if (m.media != null) Object.Destroy(m.media);
            if (m.final != null) Object.Destroy(m.final);
        }
        s_medidas.Clear();
    }

    private static string Ruta(Transform t) => t.root == t ? t.name : $"{t.root.name}/{t.name}";
}
#endif
