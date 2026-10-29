#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Ajustes de assets y escenas que acompañan al rediseño del 29 oct 2026. Se ejecuta una vez desde
/// el buzón de órdenes y después se archiva. Deja su informe en Logs/claude/herramienta.txt.
public static class HerramientaDeUnSoloUso
{
    private const string AvisoDeRayo = "Assets/Prefabs/VFX/RainShadowIndicator.prefab";
    private const string Brisa = "Assets/_SPELLS/Prefabs/BrisaSanadora.prefab";
    private const string Grupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    private static readonly string[] Escenas = { "CandyLand", "MainWorld", "Lab", "Sendero_PruebaWill" };

    [MenuItem("El Sendero/Archivo/Rediseño 29 oct/Fase 7")]
    public static void Fase7()
    {
        var informe = new StringBuilder();
        var aviso = AssetDatabase.LoadAssetAtPath<GameObject>(AvisoDeRayo);
        var brisa = AssetDatabase.LoadAssetAtPath<GameObject>(Brisa);
        informe.AppendLine($"aviso={(aviso != null)} brisa={(brisa != null)}");

        foreach (var nombre in Escenas)
        {
            string ruta = null;
            foreach (var g in AssetDatabase.FindAssets($"t:Scene {nombre}"))
            {
                var r = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(r) == nombre) { ruta = r; break; }
            }
            if (ruta == null) { informe.AppendLine($"{nombre}: no encontrada"); continue; }

            var escena = SceneManager.GetSceneByPath(ruta);
            bool abiertaAqui = !escena.isLoaded;
            bool sucia = !abiertaAqui && escena.isDirty;
            if (abiertaAqui) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);

            int ciclos = 0, asignados = 0;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                foreach (var c in raiz.GetComponentsInChildren<DayNightCycle>(true))
                {
                    ciclos++;
                    var so = new SerializedObject(c);
                    var p = so.FindProperty("dangerLightningTelegraphPrefab");
                    if (p != null && p.objectReferenceValue == null && aviso != null)
                    {
                        p.objectReferenceValue = aviso;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        asignados++;
                    }
                }
            }
            if (asignados > 0)
            {
                EditorSceneManager.MarkSceneDirty(escena);
                if (!sucia) EditorSceneManager.SaveScene(escena);
            }
            informe.AppendLine($"{nombre}: DayNightCycle={ciclos} aviso asignado={asignados}{(sucia ? " (escena ya abierta con cambios: NO guardada)" : "")}");
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }

        var contenido = PrefabUtility.LoadPrefabContents(Grupo);
        try
        {
            var r = contenido.GetComponentInChildren<ReanimarManteniendoA>(true);
            if (r == null) informe.AppendLine("GrupoDelJugador: sin ReanimarManteniendoA");
            else
            {
                var so = new SerializedObject(r);
                var p = so.FindProperty("vfxAlReanimar");
                if (p != null && p.objectReferenceValue == null && brisa != null)
                {
                    p.objectReferenceValue = brisa;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contenido, Grupo);
                    informe.AppendLine("GrupoDelJugador: vfxAlReanimar = BrisaSanadora");
                }
                else informe.AppendLine($"GrupoDelJugador: sin cambios (campo={(p != null)})");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(contenido); }

        File.WriteAllText(Path.Combine("Logs", "claude", "herramienta.txt"), informe.ToString());
    }
}
#endif
