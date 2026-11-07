using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El cielo de Chuchelandia: día fijo sin clima (el reino no tiene lluvia, nieve, viento ni tormentas) con
/// un cielo de caramelo hecho con <c>El Sendero/Cielo</c> en <c>Assets/Art/Cielo/Cielo_Chuchelandia.mat</c>:
/// cénit azul cielo claro, horizonte rosa algodón de azúcar con bruma melocotón, nubes esponjosas de
/// algodón de azúcar y el sol suave del ciclo. Usa el <see cref="DayNightCycle"/> de la escena (o crea uno)
/// y lo deja con <see cref="ClimaDelCicloWiring.FijarHoraSinClima"/>. El material se crea una vez con
/// <see cref="ColoresDelCielo"/> y luego se retoca en el Inspector; la luz, el ambiente y la niebla de
/// distancia del día están en <see cref="DiaDeChuchelandia"/>.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string NombreDelCielo = "Chuchelandia";
    private const string ObjetoDelCiclo = "DayNightSystem";

    /// <summary>Punto de la franja «Día» (9–18 h) en el que se queda la hora: 0,3 ≈ 11:40, sol alto sin estar en el cénit.</summary>
    private const float HoraDelDiaDeChuchelandia = 0.3f;

    private static DayNightCycle.TimeOfDaySettings DiaDeChuchelandia(Material cielo) => new DayNightCycle.TimeOfDaySettings
    {
        timeOfDay = DayNightCycle.TimeOfDay.AfterNoon,
        skybox = cielo,
        lightColor = new Color(1f, 0.95f, 0.90f),
        lightIntensity = 1.2f,
        ambientColor = new Color(0.46f, 0.42f, 0.47f),
        ambientIntensity = 1.05f,
        fogColor = new Color(0.98f, 0.85f, 0.87f),
        fogDensity = 0.0025f,
        duration = 600f,
    };

    private static void ColoresDelCielo(Material cielo)
    {
        cielo.SetColor("_ColorCenit", new Color(0.47f, 0.74f, 0.96f));
        cielo.SetColor("_ColorHorizonte", new Color(1f, 0.78f, 0.87f));
        cielo.SetFloat("_CaidaDelHorizonte", 0.9f);
        cielo.SetColor("_ColorSuelo", new Color(0.98f, 0.82f, 0.84f));
        cielo.SetColor("_ColorDeBrumaDelHorizonte", new Color(1f, 0.86f, 0.72f));
        cielo.SetFloat("_GrosorDeLaBruma", 0.12f);
        cielo.SetColor("_ColorDelResplandor", new Color(1f, 0.92f, 0.80f));
        cielo.SetFloat("_TamanoDelResplandor", 0.5f);
        cielo.SetFloat("_FuerzaDelResplandor", 0.12f);
        cielo.SetFloat("_NubosidadBase", 0.38f);
        cielo.SetColor("_ColorDeNubes", new Color(1.05f, 0.98f, 1f));
        cielo.SetColor("_ColorDeNubesEnSombra", new Color(0.96f, 0.74f, 0.88f));
        cielo.SetFloat("_EscalaDeNubes", 1.4f);
        cielo.SetFloat("_SuavidadDeNubes", 0.3f);
        cielo.SetFloat("_VelocidadDeNubes", 0.02f);
    }

    private static void MontarCielo(Scene escena, StringBuilder informe)
    {
        Material cielo = ClimaDelCicloWiring.CieloDeEscena(NombreDelCielo, ColoresDelCielo);
        if (cielo == null)
        {
            informe.AppendLine("⚠ Cielo: no está el shader El Sendero/Cielo; el ciclo se queda como estaba.");
            return;
        }

        DayNightCycle ciclo = CicloDeLaEscena(escena);
        ClimaDelCicloWiring.FijarHoraSinClima(ciclo, DiaDeChuchelandia(cielo), HoraDelDiaDeChuchelandia);
        informe.AppendLine($"Cielo: día fijo sin clima, cielo «{cielo.name}» (se retoca en Assets/Art/Cielo).");
    }

    /// <summary>El ciclo de la escena; si no hay, uno nuevo con sus astros y la luz direccional de la escena.</summary>
    private static DayNightCycle CicloDeLaEscena(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            var existente = raiz.GetComponentInChildren<DayNightCycle>(true);
            if (existente != null) return existente;
        }

        var go = new GameObject(ObjetoDelCiclo);
        SceneManager.MoveGameObjectToScene(go, escena);
        var ciclo = go.AddComponent<DayNightCycle>();
        go.AddComponent<SolYLunaEnElCielo>();
        Light sol = null;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (Light luz in raiz.GetComponentsInChildren<Light>(true))
            if (sol == null && luz.type == LightType.Directional) sol = luz;
        var so = new UnityEditor.SerializedObject(ciclo);
        so.FindProperty("directionalLight").objectReferenceValue = sol;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(escena);
        return ciclo;
    }
}
