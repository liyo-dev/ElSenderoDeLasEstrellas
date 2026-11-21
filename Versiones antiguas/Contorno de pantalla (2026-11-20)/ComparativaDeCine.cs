#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Comparativa del perfil de cinemáticas (INC-1325): pone en la escena, sin guardar, una copia del perfil
/// con la variante elegida y un Will para las fotos; «aplicar» escribe la variante en el perfil de verdad.
/// De un solo uso: sale a Versiones antiguas cuando Raúl elija.
public static class ComparativaDeCine
{
    private const string RutaDelPerfil = "Assets/Resources/Postproceso/PerfilDeCinematica.asset";
    private const string NombreDelVolumen = "[Comparativa de cine]";
    private const string NombreDelWill = "[Comparativa de cine: Will]";

    private static void Ajustar(VolumeProfile p, float grano, float rojo, float bloom, float umbral)
    {
        if (p.TryGet(out FilmGrain g)) { g.active = grano > 0f; g.intensity.Override(grano); }
        if (p.TryGet(out ChannelMixer c)) { c.active = rojo < 100f; c.redOutRedIn.Override(rojo); }
        if (p.TryGet(out Bloom b)) { b.intensity.Override(bloom); b.threshold.Override(umbral); }
    }

    private static void Ver(float grano, float rojo, float bloom, float umbral)
    {
        var viejo = GameObject.Find(NombreDelVolumen);
        if (viejo != null) Object.DestroyImmediate(viejo);
        var copia = Object.Instantiate(AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfil));
        copia.hideFlags = HideFlags.DontSave;
        Ajustar(copia, grano, rojo, bloom, umbral);
        var go = new GameObject(NombreDelVolumen) { hideFlags = HideFlags.DontSave };
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 500; v.sharedProfile = copia;
        if (GameObject.Find(NombreDelWill) == null)
        {
            var will = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL.prefab"),
                UnityEngine.SceneManagement.SceneManager.GetSceneByName("MainWorld"));
            PrefabUtility.UnpackPrefabInstance(will, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            will.name = NombreDelWill; will.hideFlags = HideFlags.DontSave;
            will.transform.SetPositionAndRotation(new Vector3(275f, 4.96f, -410f), Quaternion.identity);
        }
        OrdenesDelEditor.Avisar("Cine", $"variante en la escena: grano {grano}, rojo {rojo}, bloom {bloom} desde {umbral}");
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Cine: ver como está")] private static void VerActual() => Ver(0.387f, 80f, 1.5f, 0.8f);
    [MenuItem("El Sendero/Aspecto de dibujo/Cine: ver suave")] private static void VerSuave() => Ver(0.12f, 92f, 0.8f, 1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Cine: ver limpio")] private static void VerLimpio() => Ver(0f, 100f, 0.5f, 1f);

    [MenuItem("El Sendero/Aspecto de dibujo/Cine: quitar la comparativa")]
    private static void Quitar()
    {
        foreach (var n in new[] { NombreDelVolumen, NombreDelWill }) { var g = GameObject.Find(n); if (g != null) Object.DestroyImmediate(g); }
        OrdenesDelEditor.Avisar("Cine", "comparativa quitada");
    }

    private static void Aplicar(float grano, float rojo, float bloom, float umbral)
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaDelPerfil);
        Ajustar(p, grano, rojo, bloom, umbral);
        foreach (var c in p.components) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Cine", "perfil de cinemáticas cambiado");
    }
    [MenuItem("El Sendero/Aspecto de dibujo/Cine: aplicar suave")] private static void AplicarSuave() => Aplicar(0.12f, 92f, 0.8f, 1f);
    [MenuItem("El Sendero/Aspecto de dibujo/Cine: aplicar limpio")] private static void AplicarLimpio() => Aplicar(0f, 100f, 0.5f, 1f);
}
#endif

