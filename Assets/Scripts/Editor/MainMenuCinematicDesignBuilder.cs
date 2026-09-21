using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Genera una presentación cinematográfica para MainMenu sin reconstruir sus controles.
/// La vista previa es una copia completa de MainMenu.unity: conserva MainMenuController,
/// botones, listeners, paneles, guardado y navegación. La aplicación directa crea antes
/// una copia fechada de la escena original.
/// </summary>
public static class MainMenuCinematicDesignBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string PreviewPath = "Assets/Scenes/Systems/MainMenu_Cinematica_Preview.unity";
    const string BackupFolder = "Assets/Scenes/Systems/Backups";
    const string GeneratedSpriteFolder = "Assets/Art/UI/Menu/Generated";
    const string BackdropSpritePath = GeneratedSpriteFolder + "/MainMenuCinematicBackdrop.png";
    const string HaloSpritePath = GeneratedSpriteFolder + "/MainMenuCinematicHalo.png";
    const string GeneratedRootName = "MainMenuCinematicVisuals_Generated";
    const int TextureWidth = 1280;
    const int TextureHeight = 720;

    static readonly Color32 NightColor = new Color32(6, 10, 27, 255);
    static readonly Color32 StarColor = new Color32(255, 240, 190, 255);
    static readonly Color32 PortalColor = new Color32(255, 218, 138, 255);

    [MenuItem("El Sendero/MainMenu/Crear vista previa cinemática (segura)")]
    public static void CreateSafePreview()
    {
        if (!CanOpenAndSave()) return;

        bool previewExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath) != null;
        if (previewExists && !EditorUtility.DisplayDialog(
                "Regenerar vista previa",
                "Ya existe MainMenu_Cinematica_Preview.unity. Se reemplazará por una copia nueva de MainMenu y se volverá a generar el diseño.",
                "Regenerar", "Cancelar"))
            return;

        try
        {
            // Abrir la fuente limpia cierra de forma segura una vista previa anterior antes de
            // reemplazar su archivo. No se guarda ni se altera MainMenu.unity.
            Scene source = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!source.IsValid())
            {
                LogError($"[MainMenuCinematicDesignBuilder] No se pudo abrir {ScenePath}.");
                return;
            }

            if (previewExists && !AssetDatabase.DeleteAsset(PreviewPath))
            {
                LogError($"[MainMenuCinematicDesignBuilder] No se pudo reemplazar {PreviewPath}.");
                return;
            }

            if (!AssetDatabase.CopyAsset(ScenePath, PreviewPath))
            {
                LogError($"[MainMenuCinematicDesignBuilder] No se pudo copiar {ScenePath} a {PreviewPath}.");
                return;
            }

            AssetDatabase.Refresh();
            Scene preview = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            if (!preview.IsValid() || !BuildVisualDesign(preview)) return;

            EditorSceneManager.MarkSceneDirty(preview);
            EditorSceneManager.SaveScene(preview);
            AssetDatabase.SaveAssets();
            LogInfo("[MainMenuCinematicDesignBuilder] Vista previa generada. La escena original sigue intacta; " +
                      "puedes abrirla y darle a Play para comprobar que los botones conservan sus acciones.");
        }
        catch (Exception e)
        {
            LogError($"[MainMenuCinematicDesignBuilder] Error al generar la vista previa: {e}");
        }
    }

    [MenuItem("El Sendero/MainMenu/Aplicar diseño cinemático al menú funcional")]
    public static void ApplyToMainMenu()
    {
        if (!CanOpenAndSave()) return;

        if (!EditorUtility.DisplayDialog(
                "Aplicar diseño al MainMenu",
                "Se guardará una copia fechada de MainMenu.unity y después se cambiará solo su presentación. " +
                "El controlador, los botones y sus conexiones se conservarán.",
                "Crear copia y aplicar", "Cancelar"))
            return;

        try
        {
            EnsureAssetFolder("Assets/Scenes/Systems", "Backups");
            string backupPath = Path.Combine(BackupFolder,
                $"MainMenu_antes_cinematico_{DateTime.Now:yyyyMMdd_HHmmss_fff}.unity").Replace('\\', '/');
            if (!AssetDatabase.CopyAsset(ScenePath, backupPath))
            {
                LogError($"[MainMenuCinematicDesignBuilder] No se pudo crear la copia de seguridad en {backupPath}.");
                return;
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !BuildVisualDesign(scene)) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            LogInfo($"[MainMenuCinematicDesignBuilder] Diseño aplicado a MainMenu.unity. Copia anterior: {backupPath}. " +
                      "La lógica funcional y las referencias de los botones se conservaron.");
        }
        catch (Exception e)
        {
            LogError($"[MainMenuCinematicDesignBuilder] Error al aplicar el diseño: {e}");
        }
    }

    static bool CanOpenAndSave()
    {
        if (EditorApplication.isPlaying)
        {
            LogError("[MainMenuCinematicDesignBuilder] Sal de Play Mode antes de generar el menú.");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            LogError($"[MainMenuCinematicDesignBuilder] No se encontró {ScenePath}.");
            return false;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                LogError($"[MainMenuCinematicDesignBuilder] La escena '{open.name}' tiene cambios sin guardar. " +
                               "Guárdala antes de generar el menú para no perder trabajo.");
                return false;
            }
        }

        return true;
    }

    static bool BuildVisualDesign(Scene scene)
    {
        GameObject canvasObject = FindInScene(scene, "Canvas");
        GameObject titleObject = FindInScene(scene, "LogoTitulo");
        GameObject buttonPanel = FindInScene(scene, "ButtonPanel");
        GameObject versionLabel = FindInScene(scene, "VersionLabel");
        MainMenuController controller = FindComponentInScene<MainMenuController>(scene);

        if (canvasObject == null || titleObject == null || buttonPanel == null || controller == null)
        {
            LogError("[MainMenuCinematicDesignBuilder] La escena no contiene Canvas, LogoTitulo, " +
                           "ButtonPanel y MainMenuController. No se ha guardado ningún cambio.");
            return false;
        }

        Button[] buttons = buttonPanel.GetComponentsInChildren<Button>(true);
        if (buttons.Length < 5)
        {
            LogError("[MainMenuCinematicDesignBuilder] Se encontraron menos de cinco botones en ButtonPanel. " +
                           "Se cancela para evitar alterar una escena distinta o incompleta.");
            return false;
        }

        SerializedObject controllerData = new SerializedObject(controller);
        SerializedProperty panelReference = controllerData.FindProperty("buttonPanel");
        if (panelReference == null || panelReference.objectReferenceValue != buttonPanel)
        {
            LogError("[MainMenuCinematicDesignBuilder] MainMenuController no referencia este ButtonPanel. " +
                           "Se cancela para proteger la navegación funcional.");
            return false;
        }

        if (!TryLoadGeneratedSprites(out Sprite backdrop, out Sprite halo)) return false;

        Transform priorRoot = canvasObject.transform.Find(GeneratedRootName);
        if (priorRoot != null)
            Undo.DestroyObjectImmediate(priorRoot.gameObject);

        GameObject generatedRoot = new GameObject(GeneratedRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(generatedRoot, "Crear fondo cinemático de MainMenu");
        Undo.SetTransformParent(generatedRoot.transform, canvasObject.transform, "Añadir fondo al Canvas de MainMenu");
        generatedRoot.transform.SetAsFirstSibling();

        CreateFullScreenImage(generatedRoot.transform, "CieloEstrellado", backdrop);
        CreateFullScreenImage(generatedRoot.transform, "ResplandorPortal", halo);

        ApplyTitleLayout(titleObject.GetComponent<RectTransform>());
        ApplyButtonLayout(buttonPanel);
        StyleExistingButtons(buttonPanel);
        if (versionLabel != null)
            ApplyVersionLabelLayout(versionLabel);

        EditorUtility.SetDirty(canvasObject);
        EditorUtility.SetDirty(titleObject);
        EditorUtility.SetDirty(buttonPanel);

        return true;
    }

    static bool TryLoadGeneratedSprites(out Sprite backdrop, out Sprite halo)
    {
        backdrop = null;
        halo = null;
        EnsureAssetFolder("Assets/Art/UI/Menu", "Generated");

        try
        {
            File.WriteAllBytes(BackdropSpritePath, BuildBackdropPng());
            File.WriteAllBytes(HaloSpritePath, BuildHaloPng());
            AssetDatabase.ImportAsset(BackdropSpritePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(HaloSpritePath, ImportAssetOptions.ForceUpdate);

            ConfigureSpriteImporter(BackdropSpritePath);
            ConfigureSpriteImporter(HaloSpritePath);
            backdrop = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropSpritePath);
            halo = AssetDatabase.LoadAssetAtPath<Sprite>(HaloSpritePath);
        }
        catch (Exception e)
        {
            LogError($"[MainMenuCinematicDesignBuilder] No se pudieron generar las texturas del menú: {e}");
            return false;
        }

        if (backdrop == null || halo == null)
        {
            LogError("[MainMenuCinematicDesignBuilder] Unity no pudo importar las imágenes generadas como Sprite.");
            return false;
        }

        return true;
    }

    static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    static void CreateFullScreenImage(Transform parent, string name, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(imageObject, "Crear capa visual de MainMenu");
        Undo.SetTransformParent(imageObject.transform, parent, "Añadir capa visual de MainMenu");

        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.maskable = false;
    }

    static void ApplyTitleLayout(RectTransform rect)
    {
        if (rect == null) return;
        Undo.RecordObject(rect, "Colocar el título en el diseño cinemático");
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(74f, -52f);
        rect.sizeDelta = new Vector2(690f, 460f);
        rect.localScale = Vector3.one;
    }

    static void ApplyVersionLabelLayout(GameObject versionLabel)
    {
        RectTransform rect = versionLabel.GetComponent<RectTransform>();
        if (rect != null)
        {
            Undo.RecordObject(rect, "Mover la versión fuera de las opciones del menú");
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 16f);
            rect.sizeDelta = new Vector2(320f, 40f);
        }

        TMP_Text label = versionLabel.GetComponent<TMP_Text>();
        if (label != null)
        {
            Undo.RecordObject(label, "Alinear la versión en el borde derecho");
            label.alignment = TextAlignmentOptions.BottomRight;
            label.raycastTarget = false;
        }
    }

    static void ApplyButtonLayout(GameObject buttonPanel)
    {
        RectTransform rootRect = buttonPanel.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            Undo.RecordObject(rootRect, "Colocar las opciones en el diseño cinemático");
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = Vector2.zero;
            rootRect.anchoredPosition = new Vector2(78f, 42f);
            rootRect.sizeDelta = new Vector2(820f, 900f);
            rootRect.localScale = Vector3.one;
        }

        Transform panelTransform = buttonPanel.transform.Find("Panel");
        RectTransform panelRect = panelTransform as RectTransform;
        if (panelRect != null)
        {
            Undo.RecordObject(panelRect, "Ajustar el área de opciones del menú");
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -370f);
            panelRect.sizeDelta = new Vector2(620f, 530f);
            panelRect.localScale = Vector3.one;
        }

        VerticalLayoutGroup layout = buttonPanel.GetComponent<VerticalLayoutGroup>()
                                     ?? buttonPanel.GetComponentInChildren<VerticalLayoutGroup>(true);
        if (layout != null)
        {
            Undo.RecordObject(layout, "Alinear opciones del MainMenu");
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
        }

        Image panelBackground = panelTransform != null ? panelTransform.GetComponent<Image>() : null;
        if (panelBackground != null)
        {
            Undo.RecordObject(panelBackground, "Atenuar el panel detrás de las opciones");
            panelBackground.color = new Color(0.035f, 0.055f, 0.11f, 0.12f);
            panelBackground.raycastTarget = false;
        }
    }

    static void StyleExistingButtons(GameObject buttonPanel)
    {
        Button[] buttons = buttonPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            Transform rowBackground = button.transform.Find("RowGlassBG");
            Image rowImage = rowBackground != null ? rowBackground.GetComponent<Image>() : null;
            if (rowImage != null)
            {
                Undo.RecordObject(rowImage, "Aplicar tono estelar a las opciones");
                rowImage.color = new Color(0.60f, 0.72f, 1f, 0.38f);
                rowImage.raycastTarget = false;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                Undo.RecordObject(label, "Alinear el texto de las opciones");
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(46f, label.margin.y, 14f, label.margin.w);
            }
        }
    }

    static byte[] BuildBackdropPng()
    {
        Color32[] pixels = new Color32[TextureWidth * TextureHeight];
        for (int y = 0; y < TextureHeight; y++)
        {
            float v = (float)y / (TextureHeight - 1);
            for (int x = 0; x < TextureWidth; x++)
            {
                float u = (float)x / (TextureWidth - 1);
                float leftFade = 1f - SmoothStep(0.08f, 0.73f, u);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(26f, 132f, leftFade));
                pixels[y * TextureWidth + x] = new Color32(NightColor.r, NightColor.g, NightColor.b, alpha);
            }
        }

        var random = new System.Random(20260921);
        for (int i = 0; i < 145; i++)
        {
            int x = random.Next(12, TextureWidth - 12);
            int y = random.Next(18, TextureHeight - 18);
            int radius = random.Next(2, 6);
            Color32 color = i % 5 == 0 ? new Color32(154, 210, 255, 255) : StarColor;
            DrawGlow(pixels, x, y, radius * 2, color, i % 4 == 0 ? 0.55f : 0.3f);
            BlendPixel(pixels, x, y, color, 0.88f);
        }

        DrawPortalAndPath(pixels);
        return EncodePng(pixels);
    }

    static byte[] BuildHaloPng()
    {
        Color32[] pixels = new Color32[TextureWidth * TextureHeight];
        const float centerX = 0.70f;
        const float centerY = 0.55f;
        for (int y = 0; y < TextureHeight; y++)
        {
            float v = (float)y / (TextureHeight - 1);
            for (int x = 0; x < TextureWidth; x++)
            {
                float u = (float)x / (TextureWidth - 1);
                float dx = (u - centerX) / 0.24f;
                float dy = (v - centerY) / 0.39f;
                float distance = dx * dx + dy * dy;
                float alpha = Mathf.Exp(-distance * 2.4f) * 0.40f;
                float warmth = Mathf.Clamp01(1f - Mathf.Sqrt(distance) * 0.58f);
                Color tint = Color.Lerp(new Color(0.33f, 0.56f, 1f), new Color(1f, 0.78f, 0.42f), warmth);
                pixels[y * TextureWidth + x] = new Color32(
                    (byte)Mathf.RoundToInt(tint.r * 255f),
                    (byte)Mathf.RoundToInt(tint.g * 255f),
                    (byte)Mathf.RoundToInt(tint.b * 255f),
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        return EncodePng(pixels);
    }

    static void DrawPortalAndPath(Color32[] pixels)
    {
        const float centerX = 0.70f;
        const float baseY = 0.38f;
        const float radiusX = 0.105f;
        const float radiusY = 0.30f;

        for (int y = 0; y < TextureHeight; y++)
        {
            float v = (float)y / (TextureHeight - 1);
            for (int x = 0; x < TextureWidth; x++)
            {
                float u = (float)x / (TextureWidth - 1);
                int index = y * TextureWidth + x;

                if (v >= baseY)
                {
                    float dx = (u - centerX) / radiusX;
                    float dy = (v - baseY) / radiusY;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    if (radius < 1f)
                    {
                        float fillAlpha = (1f - radius) * 0.10f;
                        BlendPixel(pixels, index, new Color32(132, 174, 255, 255), fillAlpha);
                    }

                    float edgeAlpha = Mathf.Clamp01(1f - Mathf.Abs(radius - 1f) * 43f) * 0.78f;
                    if (edgeAlpha > 0f)
                        BlendPixel(pixels, index, PortalColor, edgeAlpha);
                }
                else if (v >= baseY - 0.085f)
                {
                    float leftDistance = Mathf.Abs(u - (centerX - radiusX));
                    float rightDistance = Mathf.Abs(u - (centerX + radiusX));
                    float edgeAlpha = Mathf.Clamp01(1f - Mathf.Min(leftDistance, rightDistance) * 390f) * 0.72f;
                    if (edgeAlpha > 0f)
                        BlendPixel(pixels, index, PortalColor, edgeAlpha);
                }

                if (v < baseY && v > 0.015f)
                {
                    float depth = 1f - v / baseY;
                    float halfWidth = Mathf.Lerp(0.008f, 0.16f, depth * depth);
                    float distance = Mathf.Abs(u - centerX);
                    if (distance < halfWidth)
                    {
                        float edgeFade = 1f - distance / halfWidth;
                        float pathAlpha = Mathf.Lerp(0.06f, 0.19f, edgeFade) * (0.75f - depth * 0.18f);
                        BlendPixel(pixels, index, new Color32(255, 224, 157, 255), pathAlpha);
                    }
                }
            }
        }
    }

    static void DrawGlow(Color32[] pixels, int centerX, int centerY, int radius, Color32 color, float strength)
    {
        int minX = Mathf.Max(0, centerX - radius);
        int maxX = Mathf.Min(TextureWidth - 1, centerX + radius);
        int minY = Mathf.Max(0, centerY - radius);
        int maxY = Mathf.Min(TextureHeight - 1, centerY + radius);
        float invRadius = 1f / Mathf.Max(1, radius);

        for (int y = minY; y <= maxY; y++)
        {
            float dy = (y - centerY) * invRadius;
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - centerX) * invRadius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Exp(-distance * distance * 4f) * strength;
                if (alpha > 0.01f)
                    BlendPixel(pixels, y * TextureWidth + x, color, alpha);
            }
        }
    }

    static void BlendPixel(Color32[] pixels, int index, Color32 source, float sourceAlpha)
    {
        sourceAlpha = Mathf.Clamp01(sourceAlpha);
        Color32 destination = pixels[index];
        float destinationAlpha = destination.a / 255f;
        float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
        if (outputAlpha <= 0f) return;

        float destinationWeight = destinationAlpha * (1f - sourceAlpha);
        pixels[index] = new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt((source.r * sourceAlpha + destination.r * destinationWeight) / outputAlpha), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt((source.g * sourceAlpha + destination.g * destinationWeight) / outputAlpha), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt((source.b * sourceAlpha + destination.b * destinationWeight) / outputAlpha), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(outputAlpha * 255f), 0, 255));
    }

    static void BlendPixel(Color32[] pixels, int x, int y, Color32 source, float sourceAlpha)
    {
        if (x < 0 || y < 0 || x >= TextureWidth || y >= TextureHeight) return;
        BlendPixel(pixels, y * TextureWidth + x, source, sourceAlpha);
    }

    static byte[] EncodePng(Color32[] pixels)
    {
        Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static void LogInfo(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(message);
#endif
    }

    static void LogError(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError(message);
#endif
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName) return roots[i];
            Transform[] descendants = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < descendants.Length; j++)
                if (descendants[j].name == objectName)
                    return descendants[j].gameObject;
        }
        return null;
    }

    static void EnsureAssetFolder(string parentPath, string folderName)
    {
        string folderPath = parentPath + "/" + folderName;
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        if (!AssetDatabase.IsValidFolder(parentPath))
            throw new DirectoryNotFoundException($"No existe la carpeta de proyecto {parentPath}.");

        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }
}
