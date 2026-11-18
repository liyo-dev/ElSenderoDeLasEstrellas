#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Bucles que saltan: en las animaciones en bucle que usan los controladores, compara la postura del
/// final del clip con la del principio, sobre el personaje real. Si no casan, el personaje da un tirón
/// cada vez que el clip vuelve a empezar. También avisa si el último paso es mucho más grande que los
/// demás (un fotograma de más al final de la toma). Ver INC-1285.
public static partial class AuditorDeAnimaciones
{
    private const float FpsDeBucle = 30f;
    private const float SaltoDeBucleAvisoCm = 1.5f;
    private const string RigDeBucles = "Assets/Prefabs/_WILL.prefab";
    private const string InformeBuclesCsv = "docs/bucles-animaciones.csv";

    private static readonly HumanBodyBones[] HuesosDeBucle =
    {
        HumanBodyBones.Head, HumanBodyBones.Chest,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
    };

    [MenuItem(Menu + "Bucles que saltan (usadas)", priority = 110)]
    public static void BuclesQueSaltan()
    {
        RecolectarUsos(out Dictionary<string, AnimationClip> clipsUsados);
        var bucles = clipsUsados.Values.Where(c => c.humanMotion && c.isLooping).ToList();
        string rutaRig = AssetDatabase.LoadAssetAtPath<GameObject>(RigDeBucles) != null ? RigDeBucles : PrefabDePrueba;

        var informe = new StringBuilder();
        var csv = new StringBuilder("Clip;Ajustes;Salto_cm;Hueso_salto;Paso_normal_cm;Ultimo_paso_cm;Hueso_ultimo;Salta\n");
        int conSalto = 0;
        using (var rig = new RigDePrueba(rutaRig))
        {
            AnimationMode.StartAnimationMode();
            try
            {
                foreach (AnimationClip clip in bucles.OrderBy(c => c.name))
                {
                    string linea = MedirBucle(rig, clip, out bool salta, csv);
                    if (!salta) continue;
                    conSalto++;
                    informe.Append(linea).Append(" | ");
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
            }
        }

        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), InformeBuclesCsv),
            csv.ToString(), new UTF8Encoding(true));
        string resumen = $"Medidas de todos en {InformeBuclesCsv}. {bucles.Count} bucles medidos con {System.IO.Path.GetFileNameWithoutExtension(rutaRig)}; " +
                         $"{conSalto} saltan. {informe}";
        Debug.Log("[Auditor] Bucles que saltan: " + resumen);
        OrdenesDelEditor.Avisar("Bucles que saltan", resumen);
    }

    /// Mide un clip en bucle: salto entre el final y el principio, paso normal y último paso (cm).
    private static string MedirBucle(RigDePrueba rig, AnimationClip clip, out bool salta, StringBuilder csv)
    {
        Transform caderas = rig.Animator.GetBoneTransform(HumanBodyBones.Hips);
        var huesos = HuesosDeBucle.Select(h => rig.Animator.GetBoneTransform(h)).Where(t => t != null).ToArray();
        int pasos = Mathf.Max(8, Mathf.RoundToInt(clip.length * FpsDeBucle));
        var posturas = new Vector3[pasos + 1][];

        for (int i = 0; i <= pasos; i++)
        {
            AnimationMode.BeginSampling();
            // El final se toma un pelo antes de la duración: justo en ella un clip en bucle puede dar ya el principio.
            float t = i == pasos ? clip.length - 0.0005f : clip.length * i / pasos;
            try { AnimationMode.SampleAnimationClip(rig.Go, clip, t); }
            finally { AnimationMode.EndSampling(); }
            posturas[i] = huesos.Select(h => caderas.InverseTransformPoint(h.position)).ToArray();
        }

        float Distancia(int a, int b, out int peor)
        {
            float max = 0f; peor = 0;
            for (int k = 0; k < huesos.Length; k++)
            {
                float d = Vector3.Distance(posturas[a][k], posturas[b][k]) * 100f;
                if (d > max) { max = d; peor = k; }
            }
            return max;
        }

        float salto = Distancia(pasos, 0, out int huesoDelSalto);
        var normales = new List<float>();
        for (int i = 1; i < pasos; i++) normales.Add(Distancia(i, i - 1, out _));
        normales.Sort();
        float pasoNormal = normales.Count > 0 ? normales[normales.Count / 2] : 0f;
        float ultimoPaso = Distancia(pasos, pasos - 1, out int huesoDelUltimo);

        salta = salto >= SaltoDeBucleAvisoCm && salto > pasoNormal * 3f
                || ultimoPaso >= SaltoDeBucleAvisoCm && ultimoPaso > pasoNormal * 3f;

        string ruta = AssetDatabase.GetAssetPath(clip);
        string ajustes = "";
        if (AssetImporter.GetAtPath(ruta) is ModelImporter importador)
        {
            var toma = importador.clipAnimations.FirstOrDefault(c => c.name == clip.name);
            if (toma != null)
                ajustes = $" [fotogramas {toma.firstFrame:0}-{toma.lastFrame:0}, loopPose={toma.loopPose}]";
        }
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        csv.Append(clip.name).Append(';').Append(ajustes.Trim()).Append(';')
           .Append(salto.ToString("0.00", ci)).Append(';').Append(huesos[huesoDelSalto].name).Append(';')
           .Append(pasoNormal.ToString("0.000", ci)).Append(';').Append(ultimoPaso.ToString("0.00", ci)).Append(';')
           .Append(huesos[huesoDelUltimo].name).Append(';').Append(salta ? "si" : "no").Append('\n');
        return $"{clip.name}{ajustes}: salto del bucle {salto:0.0} cm ({huesos[huesoDelSalto].name}), " +
               $"paso normal {pasoNormal:0.00} cm, último paso {ultimoPaso:0.0} cm ({huesos[huesoDelUltimo].name})";
    }
}
#endif
