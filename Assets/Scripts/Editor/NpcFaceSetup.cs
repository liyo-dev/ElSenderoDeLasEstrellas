#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Añade el sistema de caras a personajes con piezas faciales y asigna mallas neutras.
/// Informa de las variantes disponibles y de los personajes que necesitan completar su cara.
/// </summary>
public static class NpcFaceSetup
{
    private const string EmotionProfilePath = "Assets/_EmotionProfile/NpcEmotionProfile.asset";

    /// Carpetas donde viven los prefabs de personaje.
    private static readonly string[] SearchFolders = { "Assets/_NPCs", "Assets/Prefabs" };

    /// Prefab de referencia: el que tiene el juego completo de piezas de cara.
    private const string ReferencePrefab = "Assets/_NPCs/Eldran.prefab";

    /// Cuántas piezas tiene el personaje de referencia. Se lee de él en vez de dejarlo escrito a
    /// mano: así, si algún día Eldran gana o pierde variantes, el informe sigue diciendo la verdad.
    private static int _referencePairs = -1;

    [MenuItem("El Sendero/Archivo/NPCs/Setup/Montar sistema de caras en TODOS los NPCs")]
    public static void SetupAll()
    {
        var profile = AssetDatabase.LoadAssetAtPath<EmotionProfile>(EmotionProfilePath);
        if (profile == null)
        {
            EditorUtility.DisplayDialog("Caras de NPCs",
                $"No encuentro el EmotionProfile en:\n{EmotionProfilePath}", "Vale");
            return;
        }

        var listos = new List<string>();      // ya tenían el componente
        var completos = new List<string>();   // componente añadido y con las 24 piezas → listos
        var parciales = new List<string>();   // componente añadido pero les faltan piezas
        var sinCara = new List<string>();     // no tienen piezas: no hay nada que montar

        string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar("Montando caras",
                        path, (float)i / guids.Length))
                    break;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    // Solo personajes: los que tienen el gestor de comportamiento de NPC o un
                    // animador de personaje. Así no tocamos props, UI, VFX, arenas...
                    bool esPersonaje = root.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true) != null
                                       || root.GetComponentInChildren<NPCSimpleAnimator>(true) != null
                                       || root.GetComponentInChildren<PlayerDialogueAnimator>(true) != null;
                    if (!esPersonaje) continue;

                    string nombre = System.IO.Path.GetFileNameWithoutExtension(path);
                    var eyes = FaceParts(root.transform, "Eye");
                    var mouths = FaceParts(root.transform, "Mouth");

                    if (root.GetComponentInChildren<NPCEmotionController>(true) != null)
                    {
                        listos.Add($"{nombre} ({eyes.Count} ojos / {mouths.Count} bocas)");
                        continue;
                    }

                    if (eyes.Count == 0 && mouths.Count == 0)
                    {
                        sinCara.Add(nombre);
                        continue;
                    }

                    var controller = root.AddComponent<NPCEmotionController>();
                    var so = new SerializedObject(controller);
                    so.FindProperty("emotionProfile").objectReferenceValue = profile;
                    so.FindProperty("eyePrefix").stringValue = "Eye";
                    so.FindProperty("mouthPrefix").stringValue = "Mouth";
                    so.FindProperty("ojosDeReposo").objectReferenceValue =
                        PrimeraNeutra(eyes, profile, true);
                    so.FindProperty("bocaDeReposo").objectReferenceValue =
                        PrimeraNeutra(mouths, profile, false);
                    so.ApplyModifiedProperties();

                    PrefabUtility.SaveAsPrefabAsset(root, path);

                    string detalle = $"{nombre} ({eyes.Count} ojos / {mouths.Count} bocas)";
                    int completo = ReferencePairs();
                    if (eyes.Count >= completo && mouths.Count >= completo)
                        completos.Add(detalle);
                    else
                        parciales.Add(detalle);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.LogWarning(BuildReport(listos, completos, parciales, sinCara));
    }

    private static GameObject PrimeraNeutra(List<Transform> mallas, EmotionProfile perfil, bool ojos)
    {
        foreach (var malla in mallas)
            if (ojos ? perfil.EsOjoNeutro(malla.name) : perfil.EsBocaNeutra(malla.name))
                return malla.gameObject;
        return null;
    }

    private static string BuildReport(List<string> listos, List<string> completos,
        List<string> parciales, List<string> sinCara)
    {
        var sb = new StringBuilder("=== Sistema de caras en los NPCs ===\n\n");
        sb.AppendLine($"Se considera \"completo\" tener {ReferencePairs()} parejas de piezas, "
                      + "que son las que tiene Eldran (el prefab de referencia).\n");

        Section(sb, "YA LO TENÍAN (sin tocar)", listos);
        Section(sb, "LISTOS — componente añadido y ya tenían todas las piezas", completos);
        Section(sb, "A MEDIAS — componente añadido, pero les FALTAN piezas", parciales);
        Section(sb, "SIN PIEZAS DE CARA — no hay nada que montar", sinCara);

        if (parciales.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("SIGUIENTE PASO para el grupo 'A MEDIAS':");
            sb.AppendLine("  El Sendero > NPCs > Setup > Completar partes de cara (Eye/Mouth) desde Eldran");
            sb.AppendLine();
            sb.AppendLine("Esa herramienta les clona las variantes que faltan USANDO LA TRANSFORM LOCAL DE");
            sb.AppendLine("ELDRAN. En cabezas de otra proporción las caras pueden quedar descolocadas, así que");
            sb.AppendLine("revisa uno por uno los de esa lista antes de darlos por buenos.");
        }

        if (sinCara.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Los de 'SIN PIEZAS DE CARA' necesitan trabajo de arte (o tienen la cara como");
            sb.AppendLine("prefab anidado, como Ladron1/Ladron2): no se pueden resolver desde aquí.");
        }

        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, List<string> items)
    {
        sb.AppendLine($"{title}: {items.Count}");
        foreach (var it in items.OrderBy(x => x)) sb.AppendLine("   • " + it);
        sb.AppendLine();
    }

    /// Piezas de cara con el prefijo dado ("Eye" / "Mouth").
    ///
    /// Exige un dígito tras el prefijo para descartar los contenedores de piezas.
    private static List<Transform> FaceParts(Transform root, string prefix)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .Where(t => t != root
                     && t.name.Length > prefix.Length
                     && t.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                     && char.IsDigit(t.name[prefix.Length]))
            .OrderBy(t => t.name)
            .ToList();
    }

    /// Número de piezas del personaje de referencia (Eldran). Se calcula una vez por ejecución.
    private static int ReferencePairs()
    {
        if (_referencePairs >= 0) return _referencePairs;

        _referencePairs = 0;
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefab);
        if (reference != null)
            _referencePairs = Mathf.Min(FaceParts(reference.transform, "Eye").Count,
                                        FaceParts(reference.transform, "Mouth").Count);

        if (_referencePairs <= 0) _referencePairs = 12; // respaldo si el prefab de referencia falta
        return _referencePairs;
    }
}
#endif
