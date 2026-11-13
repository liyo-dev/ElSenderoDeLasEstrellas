#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// El remolino del prólogo: el torbellino que se forma bajo la carreta cuando el Archimago la
/// levanta. Sin él, la carreta sube sin que nada la empuje y el hechizo parece un truco.
///
/// La emisión, la velocidad, el giro y el desvanecido se escriben a mano (y no con los atajos de
/// `VfxProcedural`) porque esas llamadas no resuelven bien el nombre del tipo desde este archivo.
/// Ver INC-802.
public static class CrearRemolinoDelPrologo
{
    public const string RutaPrefab = "Assets/_VFX/Prologo/VFX_RemolinoDelPrologo.prefab";
    private const string CarpetaTexturas = "Assets/_VFX/Prologo/Texturas";
    private const string CarpetaMateriales = "Assets/_VFX/Prologo/Materiales";

    [MenuItem("El Sendero/VFX/Crear remolino del prólogo INC-802")]
    public static void Menu() => Asegurar();

    public static string Asegurar()
    {
        var t = new VfxProcedural.Texturas(CarpetaTexturas, CarpetaMateriales, "Prologo");
        VfxProcedural.GuardarPrefab("VFX_RemolinoDelPrologo", RutaPrefab, root => Construir(root, t));
        AssetDatabase.SaveAssets();
        return RutaPrefab;
    }

    private static void Construir(GameObject root, VfxProcedural.Texturas t)
    {
        // El remolino: un disco de luz que gira y sube desde el suelo.
        var remolino = VfxProcedural.Sistema(root.transform, "Remolino", t.Mat("Brillo", true),
            ParticleSystemRenderMode.Billboard, 1f);
        var mainR = remolino.main;
        mainR.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        mainR.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        mainR.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.15f);
        mainR.startColor = new Color(0.9f, 0.96f, 1f, 0.95f);
        mainR.simulationSpace = ParticleSystemSimulationSpace.World;
        var emisionR = remolino.emission;
        emisionR.rateOverTime = 150f;
        VfxProcedural.Forma(remolino, ParticleSystemShapeType.Circle, 1.35f, 1f);
        var velR = remolino.velocityOverLifetime;
        velR.enabled = true;
        velR.space = ParticleSystemSimulationSpace.World;
        // Los tres ejes en el mismo modo (dos constantes): si no, Unity avisa y no aplica la curva.
        velR.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velR.y = new ParticleSystem.MinMaxCurve(1.05f, 1.95f);
        velR.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var giroR = remolino.rotationOverLifetime;
        giroR.enabled = true;
        giroR.z = new ParticleSystem.MinMaxCurve(320f * Mathf.Deg2Rad);
        Desvanecer(remolino, 0.1f, 0.55f);

        // El polvo: lo que el torbellino levanta del suelo. Va sin luz, en alfa.
        var polvo = VfxProcedural.Sistema(root.transform, "Polvo", t.Mat("Humo", false),
            ParticleSystemRenderMode.Billboard, 1f);
        var mainP = polvo.main;
        mainP.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        mainP.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        mainP.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.3f);
        mainP.startColor = new Color(0.58f, 0.5f, 0.4f, 0.45f);
        mainP.simulationSpace = ParticleSystemSimulationSpace.World;
        var emisionP = polvo.emission;
        emisionP.rateOverTime = 34f;
        VfxProcedural.Forma(polvo, ParticleSystemShapeType.Circle, 1.5f, 1f);
        var velP = polvo.velocityOverLifetime;
        velP.enabled = true;
        velP.space = ParticleSystemSimulationSpace.World;
        velP.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velP.y = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        velP.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var giroP = polvo.rotationOverLifetime;
        giroP.enabled = true;
        giroP.z = new ParticleSystem.MinMaxCurve(-170f * Mathf.Deg2Rad);
        Desvanecer(polvo, 0.15f, 0.6f);
    }

    /// Alfa: entra hasta 'entra' y empieza a salir en 'sale' (fracciones de la vida).
    private static void Desvanecer(ParticleSystem ps, float entra, float sale)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, entra), new GradientAlphaKey(1f, sale), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }
}
#endif
