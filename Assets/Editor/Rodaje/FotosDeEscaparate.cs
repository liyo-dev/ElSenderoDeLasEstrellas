#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// Fotos de escaparate para la web: hornea un guion de «Assets/_SEQUENCES/Guiones/Escaparate/»
/// con el storyboard a 3840 × 2160 y copia las fotos a
/// «El Sendero de las Estrellas/Web/Escaparate/<guion>/» (fuera del proyecto de Unity).
public static class FotosDeEscaparate
{
    private const string Carpeta = "Assets/_SEQUENCES/Guiones/Escaparate/";

    [MenuItem("El Sendero/Rodaje/Fotos de escaparate: hero (4K)")]
    public static void Hero() => Hacer("Escaparate_Hero", "MainWorld_Escaparate");

    [MenuItem("El Sendero/Rodaje/Fotos de escaparate: hero (prueba rápida)")]
    public static void HeroPrueba() => Hacer("Escaparate_Hero", "MainWorld_Escaparate", rapido: true);

    /// Caras y retoques de pose de las fotos de escaparate: Estela guiñando, Liam apoyado en la
    /// de pie a la izquierda, tranquilo y Will con cara de «esto no ha salido como pensaba».
    private static void PonerCaras()
    {
        Horno.CarasDelStoryboard.Clear();
        Horno.CarasDelStoryboard["will"] = NPCEmotion.Surprised;
        Horno.CarasDelStoryboard["estela"] = NPCEmotion.Smirk;
        Horno.CarasDelStoryboard["liam"] = NPCEmotion.Thinking;

        Horno.ApuntesDelStoryboard.Clear();
    }

    /// La Estrella Fugaz de Will (su proyectil del juego) entre sus manos, quieta y brillando (solo para la foto: se crea y se borra).
    private static readonly string Humo = "Assets/_SPELLS/Prefabs/EstrellaFugaz.prefab";
    private static readonly Vector3[] ManosDeWill = { new Vector3(9.33f, 26.74f, -136.16f) };

    private const float EscalaDeLaBolita = 0.35f;
    /// Cuánto brillo conserva la bolita (luces y color de las partículas).
    private const float BrilloDeLaBolita = 0.45f;

    private static System.Collections.Generic.List<GameObject> PonerHumo()
    {
        var lista = new System.Collections.Generic.List<GameObject>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Humo);
        var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName("MainWorld");
        if (prefab == null || !escena.IsValid()) return lista;
        foreach (var pos in ManosDeWill)
        {
            var go = (GameObject)Object.Instantiate(prefab, pos, Quaternion.identity);
            go.hideFlags = HideFlags.DontSave;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, escena);
            go.transform.localScale = Vector3.one * EscalaDeLaBolita;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var luz in go.GetComponentsInChildren<Light>(true)) { luz.intensity *= BrilloDeLaBolita; luz.range *= EscalaDeLaBolita / 0.6f; }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                var c = m.startColor;
                if (c.mode == ParticleSystemGradientMode.Color)
                {
                    var col = c.color; col.a *= BrilloDeLaBolita; m.startColor = col;
                }
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                ps.Simulate(0.5f, true, true);
            }
            lista.Add(go);
        }
        return lista;
    }

    public static void Hacer(string guion, string carpetaDeEscena, bool rapido = false)
    {
        var antes = Horno.TamanoDelStoryboard;
        if (!rapido) Horno.TamanoDelStoryboard = new Vector2Int(3840, 2160);
        PonerCaras();
        CapturaDeEscenario.AbrirEscena("MainWorld");
        var humo = PonerHumo();
        try { HorneadorDeGuion.Hornear(Carpeta + guion + ".guion.txt", storyboard: true); }
        finally
        {
            Horno.TamanoDelStoryboard = antes; Horno.CarasDelStoryboard.Clear(); Horno.ApuntesDelStoryboard.Clear();
            foreach (var h in humo) if (h != null) Object.DestroyImmediate(h);
        }
        if (rapido) { OrdenesDelEditor.Avisar("Escaparate", "prueba rápida hecha (solo storyboard)"); return; }

        string origen = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, carpetaDeEscena, guion, "storyboard");
        string dev = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).Parent.FullName;
        string destino = Path.Combine(dev, "El Sendero de las Estrellas", "Web", "Escaparate", guion);
        Directory.CreateDirectory(destino);
        int n = 0;
        foreach (var png in Directory.GetFiles(origen, "*.png"))
        {
            File.Copy(png, Path.Combine(destino, Path.GetFileName(png)), true);
            n++;
        }
        OrdenesDelEditor.Avisar("Escaparate", $"{n} fotos 4K en {destino}");
    }
}
#endif
