using System.Collections.Generic;
using System.Linq;
using Kits.Waterfalls;
using Kits.Waterfalls.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Las cascadas de El Sendero con el kit Waterfalls: colores del agua de dibujo del juego y los
/// recorridos de cada sitio. En MainWorld la cascada sigue el centro del arroyo de la ladera este
/// (sale sola en los tramos que caen) y retira el velo y la salpicadura de partículas de antes
/// (se reponen al volver a montar). Ver INC-1190
public static class CascadasDelJuego
{
    public const string CarpetaDeMateriales = "Assets/Art/World/Cascadas";
    private const string RutaDelArroyo = "Assets/Scenes/Worlds/MainWorld_data/Recursos/Arroyo de la ladera este.asset";
    private const string NombreDelArroyo = "Arroyo de la ladera este";
    private const string RaizGenerada = "Cascadas (generado)";
    private const string RegistroDeRetirados = "Retirados por las cascadas";
    private static readonly string[] AntiguasDeMainWorld = { "Cascada — velo de agua", "Cascada — salpicadura" };

    /// Colores de las cascadas: los del agua dulce de dibujo, con la tinta del mundo.
    public static WaterfallLook Aspecto() => new WaterfallLook
    {
        Light = new Color(0.82f, 0.96f, 0.93f),
        Mid = new Color(0.38f, 0.74f, 0.78f),
        Dark = new Color(0.20f, 0.50f, 0.62f),
        Foam = new Color(1f, 0.99f, 0.94f),
        Ink = new Color(0.20f, 0.15f, 0.23f),
        FoamShade = new Color(0.72f, 0.88f, 0.90f),
        FoamRim = new Color(0.52f, 0.76f, 0.82f),
        FallSpeed = 4.5f,
    };

    public static (Material cortina, Material espuma) Materiales()
        => WaterfallBuilder.Materials(CarpetaDeMateriales, "Cascada_", Aspecto());

    [MenuItem("El Sendero/Escenario/Cascadas: montar las de MainWorld (INC-1190)")]
    public static void MontarMainWorld()
    {
        var escena = SceneManager.GetSceneByName("MainWorld");
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogWarning("[Cascadas] Abre MainWorld antes de montar sus cascadas.");
            return;
        }
        var mundo = escena.GetRootGameObjects().FirstOrDefault(r => r.name == "WORLD");
        if (mundo == null) { Debug.LogWarning("[Cascadas] MainWorld no tiene la raíz WORLD."); return; }

        RetiradosDeEscena.Reponer(escena, RegistroDeRetirados);
        var vieja = mundo.transform.Find(RaizGenerada);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
        var raiz = new GameObject(RaizGenerada).transform;
        raiz.SetParent(mundo.transform, false);

        var (cortina, espuma) = Materiales();
        // La lámina que hay en la escena (el vestido la corta en el codo); si no está, la malla original.
        var lamina = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>())
            .FirstOrDefault(m => m.name == NombreDelArroyo && m.sharedMesh != null);
        bool leida = lamina != null
            ? RecorridoDeCinta(lamina.sharedMesh, lamina.transform.localToWorldMatrix, 0.85f, out var recorrido, out var anchos)
            : RecorridoDeCinta(AssetDatabase.LoadAssetAtPath<Mesh>(RutaDelArroyo), Matrix4x4.identity, 0.85f, out recorrido, out anchos);
        if (!leida)
        {
            Debug.LogWarning("[Cascadas] No se pudo leer la cinta del arroyo: " + RutaDelArroyo);
            return;
        }
        var terreno = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>()).FirstOrDefault();
        if (terreno != null) PegarALaLadera(terreno, recorrido, anchos);
        var cascada = WaterfallBuilder.Build(raiz, "Cascada — arroyo de la ladera este", recorrido, anchos, cortina, espuma, 0.3f);

        int retirados = 0;
        foreach (var t in mundo.GetComponentsInChildren<Transform>(true))
            if (AntiguasDeMainWorld.Contains(t.name) && RetiradosDeEscena.Retirar(escena, t.gameObject, RegistroDeRetirados))
                retirados++;

        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cascadas] MainWorld: {cascada.Runs.Count} tramos que caen en el arroyo; {retirados} piezas de la cascada antigua retiradas. Guarda la escena.");
    }

    /// La cinta del arroyo tiene pocos vértices: entre dos de ellos cruzaría el aire en línea recta. Se reparte cada
    /// medio metro y, donde iría por encima del suelo, se baja a pegarse a la ladera.
    private static void PegarALaLadera(Terrain terreno, List<Vector3> recorrido, List<float> anchos)
    {
        const float paso = 0.5f, sobreElSuelo = 0.35f;
        var puntos = new List<Vector3>();
        var medidas = new List<float>();
        for (int i = 0; i + 1 < recorrido.Count; i++)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(recorrido[i], recorrido[i + 1]) / paso));
            for (int j = 0; j < n; j++)
            {
                float f = j / (float)n;
                Vector3 p = Vector3.Lerp(recorrido[i], recorrido[i + 1], f);
                float suelo = terreno.SampleHeight(p) + terreno.transform.position.y;
                p.y = Mathf.Min(p.y, suelo + sobreElSuelo);
                puntos.Add(p);
                medidas.Add(Mathf.Lerp(anchos[i], anchos[i + 1], f));
            }
        }
        puntos.Add(recorrido[recorrido.Count - 1]);
        medidas.Add(anchos[anchos.Count - 1]);
        recorrido.Clear(); recorrido.AddRange(puntos);
        anchos.Clear(); anchos.AddRange(medidas);
    }

    /// Centro y ancho de una cinta de agua hecha de pares de vértices (izquierda, derecha), en el
    /// orden en que baja. «escala» encoge el ancho para que la cortina no pise las orillas.
    public static bool RecorridoDeCinta(Mesh malla, Matrix4x4 aMundo, float escala, out List<Vector3> recorrido, out List<float> anchos)
    {
        recorrido = new List<Vector3>();
        anchos = new List<float>();
        if (malla == null) return false;
        var v = malla.vertices;
        for (int i = 0; i + 1 < v.Length; i += 2)
        {
            Vector3 a = aMundo.MultiplyPoint3x4(v[i]), b = aMundo.MultiplyPoint3x4(v[i + 1]);
            recorrido.Add((a + b) * 0.5f);
            anchos.Add(Vector3.Distance(a, b) * escala);
        }
        // De arriba abajo.
        if (recorrido.Count > 1 && recorrido[0].y < recorrido[recorrido.Count - 1].y)
        {
            recorrido.Reverse();
            anchos.Reverse();
        }
        return recorrido.Count > 1;
    }
}
