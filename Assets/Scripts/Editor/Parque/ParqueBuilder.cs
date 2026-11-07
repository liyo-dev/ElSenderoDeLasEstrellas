using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara Parque.unity, la feria de la prueba de Liam (GDD §5.16–5.17), para recorrerla: la base es
/// la versión de noche del pack Amusement Park (con sus luces); el suelo va en la capa Floor; noche
/// fija sin clima con el cielo propio de la feria; NavMesh para los compañeros; y el grupo con su
/// cámara en la marca «Entrada del grupo» (se puede mover a mano y volver a ejecutar). Idempotente.
/// Ver INC-891.
/// </summary>
public static partial class ParqueBuilder
{
    public const string RutaEscena = "Assets/Scenes/Worlds/Parque.unity";

    private const string Titulo = "Parque: preparar la escena";
    private const string NocheDelPack = "Versiones antiguas/Escenas sin uso (2026-10-30)/Assets/Art/World/ithappy/Amusement_Park/Scenes/Demonstration_Night.unity";
    private const string ArchivoDelDia = "Versiones antiguas/Parque de día (2026-10-31)/Parque.unity";
    private const string CarpetaDelCielo = "Assets/Art/World/Parque";
    private const string RutaDelCielo = CarpetaDelCielo + "/Cielo_Parque_Noche.mat";
    private const string ShaderDelCielo = "Sendero/CieloDeLaFeria";

    private const string RaizDelParque = "Parque de atracciones";
    private const string RaizDeLasLuces = "Luces de la feria";
    private const string LuzDeLaLuna = "Luz de la luna";
    private const string RaizDelAmbiente = "Ambiente del parque";
    private const string MarcaDeEntrada = "Entrada del grupo";

    /// <summary>Piezas del pack que se pisan: van a la capa Floor. El resto (vallas, muros, puestos…) estorba.</summary>
    private static readonly string[] PrefijosDeSuelo = { "ground_", "platform_", "stair_", "ladder_", "bridge_" };

    /// <summary>
    /// Planchas del pack que según cómo estén colocadas son muro o suelo: el parque las usa tumbadas
    /// para andenes, plataformas y peldaños. Solo las tumbadas van a Floor.
    /// </summary>
    private static readonly string[] PrefijosDeLosas = { "Wall_" };

    /// <summary>Donde empieza el grupo si no hay marca: el paseo de la entrada sur, en coordenadas del parque.</summary>
    private static readonly Vector3 EntradaPorDefecto = new Vector3(0.6f, 0f, -99f);

    [MenuItem("El Sendero/Escenario/Parque: preparar la escena (noche, suelo y grupo)")]
    public static void Preparar()
    {
        if (!EditorUtility.DisplayDialog(Titulo,
                "Se abre Parque.unity (te pregunta antes si quieres guardar lo que tengas abierto). Si aún es el parque de día, " +
                "se cambia por la versión de noche del pack y la de día se guarda en «" + Path.GetDirectoryName(ArchivoDelDia) + "».\n\n" +
                "Después: suelo en Floor, noche fija sin clima con el cielo de la feria, NavMesh y el grupo en la entrada. Hornear el NavMesh tarda un poco.",
                "Preparar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var resumen = new StringBuilder();
        if (!AsegurarBaseDeNoche(resumen))
        {
            EditorUtility.DisplayDialog(Titulo, resumen.ToString(), "Aceptar");
            return;
        }

        Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        Transform parque = Renombrar(escena, "Demonstration", RaizDelParque);
        Renombrar(escena, "Light", RaizDeLasLuces);
        if (parque == null)
        {
            EditorUtility.DisplayDialog(Titulo, $"No encuentro la raíz del parque («Demonstration» o «{RaizDelParque}») en {RutaEscena}. No se ha cambiado nada más.", "Aceptar");
            return;
        }

        if (QuitarRaiz(escena, "Main Camera")) resumen.AppendLine("Quitada la cámara de demostración del pack.");
        Light luna = LuzDireccional(escena);

        int suelo = EscenaJugable.PonerSueloEnSuCapa(escena, PrefijosDeSuelo);
        resumen.AppendLine($"Suelo: {suelo} piezas pasadas a la capa Floor (prefijos {string.Join(", ", PrefijosDeSuelo)}).");
        int losas = EscenaJugable.PonerLosasEnSuCapa(escena, 3f, PrefijosDeLosas);
        resumen.AppendLine($"Suelo: {losas} planchas de suelo ({string.Join(", ", PrefijosDeLosas)}: andenes, tarimas y peldaños) pasadas a Floor.");
        int rampas = EscenaJugable.RampasEnEscaleras(parque);
        resumen.AppendLine($"Escaleras: {rampas} rampas invisibles para poder subirlas.");

        Material cielo = CieloDeLaFeria();
        Ambiente(escena, luna, cielo);
        resumen.AppendLine("Ambiente: noche fija, sin clima, cielo «Cielo_Parque_Noche».");

        var navMesh = parque.GetComponent<NavMeshSurface>();
        if (navMesh == null) navMesh = parque.gameObject.AddComponent<NavMeshSurface>();
        navMesh.collectObjects = CollectObjects.Children;
        navMesh.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        EscenaJugable.HornearNavMesh(navMesh);
        resumen.AppendLine("NavMesh horneado.");

        resumen.Append(Atracciones(escena, parque));
        resumen.Append(Recorrido(escena, parque));

        EscenaJugable.QuitarGrupo(escena);
        Transform marca = MarcaDeLaEntrada(escena, parque);
        if (!EscenaJugable.BuscarSitioLibre(marca.position, 15f, out Vector3 sitio))
            resumen.AppendLine($"⚠ No hay suelo libre a menos de 15 m de «{MarcaDeEntrada}»: el grupo se queda en la marca. Muévela y repite.");
        EscenaJugable.ColocarGrupo(escena, sitio, marca.forward);
        resumen.AppendLine($"Grupo colocado junto a «{MarcaDeEntrada}».");

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(Titulo, resumen + "\nDale a Play para recorrerlo.", "Aceptar");
    }

    /// <summary>
    /// Si Parque.unity no es aún la versión de noche (la reconoce por sus luces), guarda la de día en
    /// Versiones antiguas y copia encima la de noche del pack, conservando el .meta (mismo GUID).
    /// </summary>
    private static bool AsegurarBaseDeNoche(StringBuilder resumen)
    {
        Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == RaizDeLasLuces || raiz.name == "Light") return true;

        string proyecto = Path.GetDirectoryName(Application.dataPath);
        string origen = Path.Combine(proyecto, NocheDelPack);
        if (!File.Exists(origen))
        {
            resumen.AppendLine($"Parque.unity es la versión de día y no encuentro la de noche en «{NocheDelPack}». No se ha cambiado nada.");
            return false;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string destinoDelDia = Path.Combine(proyecto, ArchivoDelDia);
        Directory.CreateDirectory(Path.GetDirectoryName(destinoDelDia));
        File.Copy(Path.Combine(proyecto, RutaEscena), destinoDelDia, true);
        File.Copy(origen, Path.Combine(proyecto, RutaEscena), true);
        AssetDatabase.ImportAsset(RutaEscena, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        resumen.AppendLine("Base: el parque de día se ha cambiado por la versión de noche del pack (la de día, en Versiones antiguas).");
        return true;
    }

    /// <summary>Noche fija sin clima: un DayNightCycle con una sola franja, parado y sin sorteo, y la luna alta.</summary>
    private static void Ambiente(Scene escena, Light luna, Material cielo)
    {
        QuitarRaiz(escena, RaizDelAmbiente);
        var go = new GameObject(RaizDelAmbiente);
        SceneManager.MoveGameObjectToScene(go, escena);
        var ciclo = go.AddComponent<DayNightCycle>();
        go.AddComponent<SolYLunaEnElCielo>();

        var so = new SerializedObject(ciclo);
        SerializedProperty franjas = so.FindProperty("timeSettings");
        franjas.arraySize = 1;
        SerializedProperty noche = franjas.GetArrayElementAtIndex(0);
        noche.FindPropertyRelative("timeOfDay").intValue = (int)DayNightCycle.TimeOfDay.Night;
        noche.FindPropertyRelative("skybox").objectReferenceValue = cielo;
        noche.FindPropertyRelative("lightColor").colorValue = new Color(0.55f, 0.62f, 1f);
        noche.FindPropertyRelative("lightIntensity").floatValue = 0.35f;
        noche.FindPropertyRelative("ambientColor").colorValue = new Color(0.17f, 0.13f, 0.27f);
        noche.FindPropertyRelative("ambientIntensity").floatValue = 0.9f;
        noche.FindPropertyRelative("fogColor").colorValue = new Color(0.1f, 0.06f, 0.16f);
        noche.FindPropertyRelative("fogDensity").floatValue = 0.004f;
        noche.FindPropertyRelative("duration").floatValue = 600f;
        so.FindProperty("directionalLight").objectReferenceValue = luna;
        so.FindProperty("autoAdvance").boolValue = false;
        so.FindProperty("sortearClima").boolValue = false;
        so.FindProperty("startingTimeIndex").intValue = 0;
        so.FindProperty("progresoInicialDeLaFranja").floatValue = 0.4f;
        so.ApplyModifiedPropertiesWithoutUndo();

        RenderSettings.skybox = cielo;
        RenderSettings.sun = luna;
    }

    /// <summary>El material del cielo de la feria; se crea con los valores del shader si no existe (y si existe, se respeta).</summary>
    private static Material CieloDeLaFeria()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaDelCielo);
        if (mat != null) return mat;
        Shader shader = Shader.Find(ShaderDelCielo);
        if (shader == null)
        {
            Debug.LogError($"[ParqueBuilder] No está el shader {ShaderDelCielo}; el cielo se queda sin poner.");
            return null;
        }
        if (!AssetDatabase.IsValidFolder(CarpetaDelCielo)) AssetDatabase.CreateFolder("Assets/Art/World", "Parque");
        mat = new Material(shader) { name = "Cielo_Parque_Noche" };
        AssetDatabase.CreateAsset(mat, RutaDelCielo);
        return mat;
    }

    private static Light LuzDireccional(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            var luz = raiz.GetComponent<Light>();
            if (luz == null || luz.type != LightType.Directional) continue;
            raiz.name = LuzDeLaLuna;
            return luz;
        }
        var go = new GameObject(LuzDeLaLuna);
        SceneManager.MoveGameObjectToScene(go, escena);
        var nueva = go.AddComponent<Light>();
        nueva.type = LightType.Directional;
        nueva.shadows = LightShadows.Soft;
        return nueva;
    }

    private static Transform MarcaDeLaEntrada(Scene escena, Transform parque)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == MarcaDeEntrada) return raiz.transform;
        var marca = new GameObject(MarcaDeEntrada).transform;
        SceneManager.MoveGameObjectToScene(marca.gameObject, escena);
        marca.SetPositionAndRotation(parque.TransformPoint(EntradaPorDefecto), Quaternion.LookRotation(parque.forward));
        return marca;
    }

    private static Transform Renombrar(Scene escena, string viejo, string nuevo)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == nuevo) return raiz.transform;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == viejo) { raiz.name = nuevo; return raiz.transform; }
        return null;
    }

    private static bool QuitarRaiz(Scene escena, string nombre)
    {
        bool quitado = false;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == nombre) { Object.DestroyImmediate(raiz); quitado = true; }
        return quitado;
    }
}
