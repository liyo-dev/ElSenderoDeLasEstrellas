using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gestos que el pack no trae, hechos sobre sus clips con <see cref="PoseSobreClip"/> (los archivos del
/// pack no se tocan; los controladores son copias en <see cref="CarpetaAnimacion"/>):
/// - El heraldo se lleva la trompetilla a la boca («Trompeta», sobre su reposo). La trompetilla, que
///   lleva siempre en la mano, se orienta para que en ese gesto apunte al frente y un poco arriba.
/// - Las gominolas del ejército llevan a Estela con los brazos arriba («Cargar», sobre su correr).
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string CarpetaAnimacion = CarpetaReparto + "/Animacion";
    private const string RutaControladorDelHeraldo = CarpetaAnimacion + "/Heraldo.controller";
    private const string RutaControladorDeLasGominolas = CarpetaAnimacion + "/Gominola.controller";
    private const string ModeloDelHeraldo = Pack + "/Characters/Prefabs/Nutty_Knight_01.prefab";
    private const string ModeloDeGominola = Pack + "/Characters/Prefabs/Grummy_01.prefab";
    private const string ReposoDelHeraldo = Pack + "/Characters/Animations/Nutty_Knight/Candy_King_Idle_1.anim";
    private const string CorrerDeGominola = Pack + "/Characters/Animations/Grummy/Grummy_Run.anim";

    /// <summary>Por dónde coge el heraldo la trompetilla: a esta fracción de su largo desde la boquilla.</summary>
    private const float AgarreDeLaTrompetilla = 0.25f;
    /// <summary>Cuánto apunta la campana hacia arriba al tocar (grados).</summary>
    private const float InclinacionAlTocar = 15f;

    /// <summary>Pose de la trompetilla respecto al hueso de la mano (posición en m, sin escala; giro), medida en el gesto.</summary>
    private static (Vector3 posicion, Quaternion giro)? _trompetillaEnLaMano;

    private static void AsegurarGestos(StringBuilder informe)
    {
        _trompetillaEnLaMano = null;
        var heraldo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloDelHeraldo);
        var reposo = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReposoDelHeraldo);
        if (heraldo != null && reposo != null)
        {
            var m = PoseSobreClip.Medir(heraldo, reposo);
            // La boca: delante de la cara, un poco por debajo del hueso de la cabeza.
            Vector3 cabeza = m.posicion["Head"];
            Vector3 boca = new Vector3(0f, Mathf.Max(m.caja.center.y, cabeza.y - 0.1f * m.caja.size.y), m.caja.max.z);
            Vector3 hombro = m.posicion["RightArm"];
            Vector3 codo = Vector3.Lerp(hombro, boca, 0.5f) + new Vector3(0.12f, -0.1f, 0f) * m.caja.size.y;
            var pose = PoseSobreClip.Crear(heraldo, reposo, CarpetaAnimacion + "/Heraldo_Trompeta.anim", new[]
            {
                PoseSobreClip.Apunte.A("RightArm", "RightForeArm", codo),
                PoseSobreClip.Apunte.A("RightForeArm", "RightHand", boca),
            }, bucle: false);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CarpetaAnimacion + "/Heraldo_Trompeta.anim");
            PoseSobreClip.ControladorConEstados(Demo + "/Animation/Nutty_Knight_Controller.controller", RutaControladorDelHeraldo, ("Trompeta", clip, false));

            // La trompetilla en ese gesto: campana al frente y un poco arriba, pistones arriba. El modelo
            // va a lo largo de su -X (la boquilla en el origen) con los pistones en +Y.
            string agarre = pose.giro.ContainsKey("RightHandProp") ? "RightHandProp" : "RightHand";
            Vector3 campana = Quaternion.AngleAxis(-InclinacionAlTocar, Vector3.right) * Vector3.forward;
            Vector3 x = -campana, y = Vector3.ProjectOnPlane(Vector3.up, campana).normalized;
            var giro = Quaternion.LookRotation(Vector3.Cross(x, y), y);
            Vector3 boquilla = pose.posicion[agarre] - campana * (AgarreDeLaTrompetilla * TamanoDeLaTrompetillaDelHeraldo);
            var mano = pose.giro[agarre];
            _trompetillaEnLaMano = (Quaternion.Inverse(mano) * (boquilla - pose.posicion[agarre]), Quaternion.Inverse(mano) * giro);
            informe.AppendLine($"Gestos: «Trompeta» del heraldo (mano a {Vector3.Distance(pose.posicion["RightHand"], boca):0.00} m de la boca).");
        }
        else informe.AppendLine($"Gestos: no encuentro {ModeloDelHeraldo} o {ReposoDelHeraldo}; el heraldo sin gesto de trompeta.");

        var gominola = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloDeGominola);
        var correr = AssetDatabase.LoadAssetAtPath<AnimationClip>(CorrerDeGominola);
        if (gominola != null && correr != null)
        {
            PoseSobreClip.Crear(gominola, correr, CarpetaAnimacion + "/Gominola_Cargar.anim", new[]
            {
                PoseSobreClip.Apunte.Hacia("LeftArm", "LeftForeArm", new Vector3(-0.25f, 1f, 0.05f)),
                PoseSobreClip.Apunte.Hacia("LeftForeArm", "LeftHand", new Vector3(-0.08f, 1f, 0f)),
                PoseSobreClip.Apunte.Hacia("RightArm", "RightForeArm", new Vector3(0.25f, 1f, 0.05f)),
                PoseSobreClip.Apunte.Hacia("RightForeArm", "RightHand", new Vector3(0.08f, 1f, 0f)),
            }, bucle: true);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CarpetaAnimacion + "/Gominola_Cargar.anim");
            PoseSobreClip.ControladorConEstados(Demo + "/Animation/Grummy_Controller.controller", RutaControladorDeLasGominolas, ("Cargar", clip, true));
            informe.AppendLine("Gestos: «Cargar» de las gominolas (correr con los brazos arriba).");
        }
        else informe.AppendLine($"Gestos: no encuentro {ModeloDeGominola} o {CorrerDeGominola}; las gominolas sin «Cargar».");
    }
}
