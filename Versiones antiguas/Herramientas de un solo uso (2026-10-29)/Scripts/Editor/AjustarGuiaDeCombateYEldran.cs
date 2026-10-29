using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using M = MomentoDeCombate;

public static class AjustarGuiaDeCombateYEldran
{
    const string RutaGuionEldran = "Assets/BOSSBATTLES/Guiones/Guion_Demonio1_Eldran.asset";
    const string RutaGuionGolem = "Assets/BOSSBATTLES/Guiones/Guion_Golem1_Estela.asset";
    const string RutaGuionDemonio2 = "Assets/BOSSBATTLES/Guiones/Guion_Demonio2_Estela.asset";
    const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    const string RutaPrefabBocadillo = "Assets/Prefabs/UI/SpeechBubbleUI.prefab";

    [MenuItem("El Sendero/Archivo/Ajustar Guia De Eldran Y Bocadillo")]
    public static void Aplicar()
    {
        var informe = new List<string>();

        // 1. Ajustar Guion de Eldran (Demonio 1)
        var guionEldran = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(RutaGuionEldran);
        if (guionEldran != null)
        {
            guionEldran.hablanteId = "NPC_Eldran";
            guionEldran.nombreHablante = "Eldran";
            guionEldran.mirarAlJugador = true;
            guionEldran.introDelay = 0.8f;
            guionEldran.duracionBocadillo = 2.4f;
            guionEldran.pausaEntreFrases = 2.5f;
            guionEldran.segundosSinAcertar = 25f;

            guionEldran.comentarios = new List<ComentarioDeCombate>
            {
                // Inicio: Solo 2 frases tutorial directas (botón de ataque y aro de runas)
                new(M.Inicio, "EVT_08", "Challenging_NoWeapon", vecesMax: 1),
                new(M.Inicio, "EVT_ELDRAN_HINT_ARO", "FoundSomething_NoWeapon", vecesMax: 1),

                // Mecánica del Aro: 1 sola vez cuando brilla
                new(M.JefeExpuesto, "EVT_ELDRAN_AHORA_01", "Challenging_NoWeapon", vecesMax: 1, caducidad: 0.8f, interrumpe: false, duracion: 1.6f),

                // Confirmación de primer acierto (1 sola vez; fuera los vítores repetitivos)
                new(M.GolpeValido, "EVT_ELDRAN_ACIERTO_01", "Cheer01", vecesMax: 1),

                // Error al golpear sin el aro encendido (1 sola vez de aviso tutorial)
                new(M.GolpeMalDado, "EVT_ELDRAN_HINT_SIN_ARO", "HeadShake01", vecesMax: 1, interrumpe: false),

                // Primer golpe recibido (1 sola vez)
                new(M.JugadorHerido, "EVT_ELDRAN_WILL_HERIDO", "Challenging_NoWeapon", vecesMax: 1, caducidad: 4f),

                // Poca vida (1 sola vez)
                new(M.JugadorPocaVida, "EVT_ELDRAN_POCA_VIDA", "FoundSomething_NoWeapon", vecesMax: 1, enfriamiento: 30f, caducidad: 4f),

                // Primeros orbes que caen (1 sola vez)
                new(M.OrbesSueltos, "EVT_ELDRAN_HINT_ORBES", "FoundSomething_NoWeapon", vecesMax: 1, caducidad: 6f),

                // Fases (momentos narrativos clave del combate)
                new(M.CambioDeFase, "EVT_ELDRAN_FASE_2", "FoundSomething_NoWeapon", vecesMax: 1, interrumpe: true, duracion: 3.2f, fase: 1),
                new(M.CambioDeFase, "EVT_ELDRAN_FASE_3", "FoundSomething_NoWeapon", vecesMax: 1, interrumpe: true, duracion: 3.2f, fase: 2),

                // Recta final del jefe (1 sola vez)
                new(M.JefeCasiVencido, "EVT_ELDRAN_CASI", "Cheer02", vecesMax: 1, caducidad: 4f),
            };

            EditorUtility.SetDirty(guionEldran);
            informe.Add($"✓ {RutaGuionEldran}: guion ajustado (10 frases tutoriales a 1 sola vez, pausaEntreFrases 2.5s).");
        }
        else
        {
            informe.Add($"⚠ No se encontró {RutaGuionEldran}");
        }

        // 2. Ajustar Guiones de Estela (Gólem y Demonio 2) para quitar vítores infinitos
        foreach (var ruta in new[] { RutaGuionGolem, RutaGuionDemonio2 })
        {
            var g = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(ruta);
            if (g != null)
            {
                g.pausaEntreFrases = 2.2f;
                bool cambiado = false;
                if (g.comentarios != null)
                {
                    foreach (var c in g.comentarios)
                    {
                        if (c.key == "EVT_ESTELA_GOLEM_ANIMO" && c.vecesMax == 0)
                        {
                            c.vecesMax = 1;
                            cambiado = true;
                        }
                    }
                }
                EditorUtility.SetDirty(g);
                informe.Add($"✓ {ruta}: pausaEntreFrases 2.2s y vítores limitados a 1 vez.");
            }
        }

        // 3. Ajustar Prefab de SpeechBubbleUI si existe
        var prefabBocadillo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabBocadillo);
        if (prefabBocadillo != null)
        {
            var bubble = prefabBocadillo.GetComponentInChildren<SpeechBubbleUI>(true);
            if (bubble != null)
            {
                var so = new SerializedObject(bubble);
                var prop = so.FindProperty("_posicionFija");
                if (prop != null)
                {
                    prop.vector2Value = new Vector2(0f, 0.70f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(prefabBocadillo);
                    informe.Add($"✓ {RutaPrefabBocadillo}: _posicionFija elevada a (0, 0.70).");
                }
            }
        }

        // 4. Ajustar escena Start.unity
        bool startYaAbierta = false;
        Scene startScene = default;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.path == RutaStart)
            {
                startYaAbierta = true;
                startScene = s;
                break;
            }
        }

        bool abrirStart = !startYaAbierta;
        if (abrirStart)
        {
            startScene = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);
        }

        if (startScene.IsValid() && startScene.isLoaded)
        {
            var bubbles = Object.FindObjectsByType<SpeechBubbleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int mod = 0;
            foreach (var b in bubbles)
            {
                if (b.gameObject.scene == startScene)
                {
                    var so = new SerializedObject(b);
                    var prop = so.FindProperty("_posicionFija");
                    if (prop != null)
                    {
                        prop.vector2Value = new Vector2(0f, 0.70f);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(b);
                        mod++;
                    }
                }
            }
            if (mod > 0)
            {
                EditorSceneManager.MarkSceneDirty(startScene);
                EditorSceneManager.SaveScene(startScene);
                informe.Add($"✓ {RutaStart}: {mod} componente(s) SpeechBubbleUI actualizados con _posicionFija a (0, 0.70).");
            }
            if (abrirStart)
            {
                EditorSceneManager.CloseScene(startScene, true);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[AjustarGuiaDeCombateYEldran]\n- " + string.Join("\n- ", informe));
    }
}
