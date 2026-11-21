#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Diagnóstico: quién gira la cabeza y el cuello de cada personaje humanoide. Mientras registra, pone tres
/// sondas (SondaDeCabeza, SondaDeCabezaTrasQuieta, SondaDeCabezaFinal) en cada Animator humanoide de las
/// escenas abiertas (también en los que aparecen después: se revisa cada segundo) y cada fotograma apunta el
/// giro de la cabeza por fuente (clip, CabezaQuietaAlHablar, código posterior) y el clip que suena.
///
/// Se puede dejar armado antes de entrar en Play («Registrar en el próximo Play», también desde el buzón con
/// `menu`): registra toda la partida y, al salir de Play o con «Parar y escribir», deja el informe en
/// Guiones/_informes/sonda_de_cabeza.md (resumen por personaje, peor momento de cada fuente con su clip y el
/// registro de los momentos con la cabeza muy girada). Ver INC-1308.
[InitializeOnLoad]
public static class SondaDeCabezaMenu
{
    private const string Informe = "Guiones/_informes/sonda_de_cabeza.md";
    private const string ClaveArmado = "SondaDeCabeza.ProximoPlay";
    private const float Mucho = 35f;          // giro total de la cabeza (grados) a partir del cual se copia el registro
    private const int MaxLineasPorPersonaje = 40;

    private static readonly List<(string nombre, SondaDeCabezaFinal sonda, SondaDeCabezaFinal.Datos datos)> s_sondas = new();
    private static bool s_registrando;
    private static double s_revisar;

    static SondaDeCabezaMenu()
    {
        EditorApplication.playModeStateChanged += AlCambiarDePlay;
    }

    [MenuItem("El Sendero/Personajes/Diagnóstico/Quién mueve la cabeza: registrar en el próximo Play", priority = 900)]
    public static void Armar()
    {
        EditorPrefs.SetBool(ClaveArmado, true);
        if (Application.isPlaying) Empezar();
        OrdenesDelEditor.Avisar("Quién mueve la cabeza", Application.isPlaying
            ? "Registrando ya. El informe sale al salir de Play o con «Parar y escribir»."
            : "Armado: registrará desde que se entre en Play hasta que se salga.");
    }

    [MenuItem("El Sendero/Personajes/Diagnóstico/Quién mueve la cabeza: registrar ahora (en Play)", priority = 901)]
    public static void Empezar()
    {
        if (!Application.isPlaying) { OrdenesDelEditor.Avisar("Quién mueve la cabeza", "Hay que estar en Play."); return; }
        if (s_registrando) return;
        s_registrando = true;
        s_sondas.Clear();
        Revisar();
        EditorApplication.update -= Cada;
        EditorApplication.update += Cada;
        Debug.Log("[Quién mueve la cabeza] registrando…");
    }

    [MenuItem("El Sendero/Personajes/Diagnóstico/Quién mueve la cabeza: parar y escribir", priority = 902)]
    public static void Parar()
    {
        EditorPrefs.SetBool(ClaveArmado, false);
        if (!s_registrando) return;
        Escribir();
        Quitar();
    }

    private static void AlCambiarDePlay(PlayModeStateChange cambio)
    {
        if (cambio == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ClaveArmado, false)) Empezar();
        else if (cambio == PlayModeStateChange.ExitingPlayMode && s_registrando) Parar();
    }

    private static void Cada()
    {
        if (!s_registrando || !Application.isPlaying) return;
        if (EditorApplication.timeSinceStartup < s_revisar) return;
        s_revisar = EditorApplication.timeSinceStartup + 1.0;
        Revisar();
    }

    /// Pone sondas a los humanoides que aún no las tienen (lo de los que desaparecen se conserva en sus datos).
    private static void Revisar()
    {
        foreach (var a in Object.FindObjectsByType<Animator>())
        {
            if (a == null || !a.isHuman || a.avatar == null || a.GetBoneTransform(HumanBodyBones.Head) == null) continue;
            if (a.GetComponent<SondaDeCabezaFinal>() != null) continue;
            a.gameObject.AddComponent<SondaDeCabeza>();
            a.gameObject.AddComponent<SondaDeCabezaTrasQuieta>();
            var final = a.gameObject.AddComponent<SondaDeCabezaFinal>();
            var t = a.transform;
            s_sondas.Add((t.root == t ? t.name : $"{t.root.name}/{t.name}", final, final.D));
        }
    }

    private static void Escribir()
    {
        var todas = new List<(string nombre, SondaDeCabezaFinal.Datos s)>();
        foreach (var x in s_sondas) if (x.datos.Medido) todas.Add((x.nombre, x.datos));
        todas.Sort((a, b) => b.s.Peor.total.CompareTo(a.s.Peor.total));
        var sb = new StringBuilder();
        sb.AppendLine("# Quién mueve la cabeza").AppendLine();
        sb.AppendLine("Grados. clip = pose del Animator frente al reposo del avatar; quieta = CabezaQuietaAlHablar; resto = código que corre después (mirada, sentado, gestos); total = pose final frente al reposo; cuello = lo mismo del cuello. t = Time.time.").AppendLine();
        sb.AppendLine("| Personaje | peor total (t, clip/quieta/resto, cuello, estados) | peor del clip (t, estados) | peor de quieta (t) | peor del resto (t, estados) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var (nombre, s) in todas)
        {
            var p = s.Peor;
            sb.AppendLine($"| {nombre} | {p.total:0} (t {p.tiempo:0.0}; {p.clip:0}/{p.quieta:0}/{p.resto:0}; cuello {p.cuello:0}; {p.estados}) | {s.PeorClip.clip:0} (t {s.PeorClip.tiempo:0.0}; {s.PeorClip.estados}) | {s.PeorQuieta.quieta:0} (t {s.PeorQuieta.tiempo:0.0}) | {s.PeorResto.resto:0} (t {s.PeorResto.tiempo:0.0}; {s.PeorResto.estados}) |");
        }
        sb.AppendLine().AppendLine($"## Momentos con la cabeza girada más de {Mucho:0}°").AppendLine();
        foreach (var (nombre, s) in todas)
        {
            if (s.Peor.total < Mucho) continue;
            sb.AppendLine($"### {nombre}");
            int lineas = 0;
            foreach (var m in s.Registro)
            {
                if (m.total < Mucho) continue;
                if (++lineas > MaxLineasPorPersonaje) { sb.AppendLine("- …"); break; }
                sb.AppendLine($"- t {m.tiempo:0.00}: total {m.total:0} = clip {m.clip:0}, quieta {m.quieta:0}, resto {m.resto:0}; cuello {m.cuello:0}; {m.estados}");
            }
            sb.AppendLine();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Informe));
        File.WriteAllText(Informe, sb.ToString());
        Debug.Log($"[Quién mueve la cabeza] informe en {Informe} ({todas.Count} personajes)");
    }

    private static void Quitar()
    {
        s_registrando = false;
        EditorApplication.update -= Cada;
        foreach (var x in s_sondas)
        {
            if (x.sonda == null) continue;
            foreach (var c in x.sonda.GetComponents<SondaDeCabeza>()) Object.Destroy(c);
        }
        s_sondas.Clear();
    }
}
#endif
