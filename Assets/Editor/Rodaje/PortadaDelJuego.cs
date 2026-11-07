#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Portada del juego en el Sendero: hornea «Escaparate/Portada_Sendero.guion.txt» con el storyboard
/// a 3840 × 2160 y copia las fotos a «El Sendero de las Estrellas/Web/Escaparate/Portada_Sendero/».
/// Mientras fotografía, el Sendero es la escena activa (su cielo y su luz) y lo de las demás
/// escenas abiertas se apaga, como en las fotos del Sendero. Will lleva su Estrella Fugaz en las manos.
public static class PortadaDelJuego
{
    private const string Guion = "Assets/_SEQUENCES/Guiones/Escaparate/Portada_Sendero.guion.txt";
    private const string Escena = "Sendero";
    private static readonly string[] GrupoDeLaEscena = { "_WILL", "_ESTELA", "_LIAM" };

    [MenuItem("El Sendero/Rodaje/Portada del juego en el Sendero (4K)")]
    public static void Portada() => Hacer(rapido: false);

    [MenuItem("El Sendero/Rodaje/Portada del juego en el Sendero (prueba rápida)")]
    public static void Prueba() => Hacer(rapido: true);

    /// Muestrario de poses: varias fotos pequeñas seguidas de cada animación candidata, para elegir
    /// la pose y el momento antes de pasarlos al guion de la portada.
    [MenuItem("El Sendero/Rodaje/Portada del juego: muestrario de poses")]
    public static void Poses() => Hacer(rapido: true, guion: MuestrarioDePoses);

    private const string MuestrarioDePoses = "Assets/_SEQUENCES/Guiones/Escaparate/Portada_Poses.guion.txt";

    private static void PonerCaras()
    {
        Horno.CarasDelStoryboard.Clear();
        // Will decidido (ojos Eye33) con la media sonrisa de Smirk (boca Mouth27).
        Horno.CarasDelStoryboard["will"] = NPCEmotion.Determined;
        Horno.PiezasDeCaraDelStoryboard.Clear();
        Horno.PiezasDeCaraDelStoryboard["will"] = (null, "Mouth27");
        Horno.CarasDelStoryboard["estela"] = NPCEmotion.Smirk;
        // Liam mirando de reojo, como en la foto del hero.
        Horno.CarasDelStoryboard["liam"] = NPCEmotion.Thinking;
        Horno.ApuntesDelStoryboard.Clear();
        // Liam de brazos cruzados (no hay clip): brazos abajo y algo al frente, antebrazos cruzados
        // delante del pecho a distinta altura (el derecho arriba, el izquierdo abajo) para que las dos manos se lean por separado. Direcciones en el espacio del personaje.
        Horno.ApuntesDelStoryboard["liam"] = new()
        {
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(0.5f, -0.65f, 0.55f)),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(-1f, 0.6f, 0.3f)),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-0.5f, -0.65f, 0.55f)),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(1f, 0.2f, 0.05f)),
        };
    }

    /// La Estrella Fugaz de Will (su proyectil del juego) entre sus manos, quieta y brillando.
    /// Solo existe mientras se hace la foto.
    private const string Estrella = "Assets/_SPELLS/Prefabs/EstrellaFugaz.prefab";
    private static readonly Vector3 ManosDeWill = new(0.06f, 0.55f, 5.96f);
    private const float EscalaDeLaEstrella = 0.35f;
    private const float BrilloDeLaEstrella = 0.45f;

    private static GameObject PonerEstrella(Scene escena)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Estrella);
        if (prefab == null || !escena.IsValid()) return null;
        var go = (GameObject)Object.Instantiate(prefab, ManosDeWill, Quaternion.identity);
        go.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(go, escena);
        go.transform.localScale = Vector3.one * EscalaDeLaEstrella;
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
        foreach (var luz in go.GetComponentsInChildren<Light>(true)) { luz.intensity *= BrilloDeLaEstrella; luz.range *= EscalaDeLaEstrella / 0.6f; }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            if (m.startColor.mode == ParticleSystemGradientMode.Color) { var c = m.startColor.color; c.a *= BrilloDeLaEstrella; m.startColor = c; }
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ps.Simulate(0.5f, true, true);
        }
        return go;
    }

    /// Contorno de tinta más marcado para las fotos: a tamaño de portada, las manos y los brazos que
    /// se cruzan por delante del cuerpo necesitan línea para leerse. Va en un Volume global temporal
    /// con prioridad alta, que se borra al acabar; el look del juego no cambia.
    private static GameObject ContornoDeFoto(Scene escena)
    {
        if (!escena.IsValid()) return null;
        var go = new GameObject("Contorno de la foto (temporal)") { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(go, escena);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1000f;
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.hideFlags = HideFlags.DontSave;
        var c = perfil.Add<ContornoDeTinta>(true);
        c.intensidad.Override(1f);
        c.grosor.Override(2f);
        c.umbralDeSilueta.Override(0.015f);
        c.umbralDePliegue.Override(0.3f);
        c.pesoDePliegues.Override(0.8f);
        c.tinta.Override(new Color(0.22f, 0.2f, 0.3f, 1f));
        vol.sharedProfile = perfil;
        return go;
    }

    private static void Hacer(bool rapido, string guion = Guion)
    {
        if (Application.isPlaying) return;
        var antes = Horno.TamanoDelStoryboard;
        if (!rapido) Horno.TamanoDelStoryboard = new Vector2Int(3840, 2160);
        PonerCaras();
        var escena = CapturaDeEscenario.AbrirEscena(Escena);
        var activa = SceneManager.GetActiveScene();
        var apagados = new List<GameObject>();
        GameObject estrella = null, contorno = null;
        try
        {
            if (escena.IsValid() && escena.isLoaded)
            {
                SceneManager.SetActiveScene(escena);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var otra = SceneManager.GetSceneAt(i);
                    if (otra == escena || !otra.isLoaded) continue;
                    foreach (var r in otra.GetRootGameObjects())
                        if (r.activeSelf) { r.SetActive(false); apagados.Add(r); }
                }
                // El grupo que la escena trae puesto en la llegada saldría repetido delante de la cámara.
                foreach (var r in escena.GetRootGameObjects())
                    foreach (var t in r.GetComponentsInChildren<Transform>(true))
                        if (System.Array.IndexOf(GrupoDeLaEscena, t.name) >= 0 && t.gameObject.activeSelf)
                        { t.gameObject.SetActive(false); apagados.Add(t.gameObject); }
            }
            estrella = PonerEstrella(escena);
            contorno = ContornoDeFoto(escena);
            HorneadorDeGuion.Hornear(guion, storyboard: true);
        }
        finally
        {
            Horno.TamanoDelStoryboard = antes; Horno.CarasDelStoryboard.Clear(); Horno.PiezasDeCaraDelStoryboard.Clear(); Horno.ApuntesDelStoryboard.Clear();
            if (estrella != null) Object.DestroyImmediate(estrella);
            if (contorno != null) { var v = contorno.GetComponent<Volume>(); if (v != null && v.sharedProfile != null) Object.DestroyImmediate(v.sharedProfile); Object.DestroyImmediate(contorno); }
            foreach (var r in apagados) if (r != null) r.SetActive(true);
            if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
        }
        if (rapido) { OrdenesDelEditor.Avisar("Portada", "prueba rápida hecha (solo storyboard)"); return; }

        string origen = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, Escena, "Portada_Sendero", "storyboard");
        string dev = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).Parent.FullName;
        string destino = Path.Combine(dev, "El Sendero de las Estrellas", "Web", "Escaparate", "Portada_Sendero");
        Directory.CreateDirectory(destino);
        int n = 0;
        foreach (var png in Directory.GetFiles(origen, "*.png"))
        {
            File.Copy(png, Path.Combine(destino, Path.GetFileName(png)), true);
            n++;
        }
        OrdenesDelEditor.Avisar("Portada", $"{n} fotos 4K en {destino}");
    }
}
#endif
