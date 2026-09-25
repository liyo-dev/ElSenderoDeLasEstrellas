using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crea y conecta en la escena el GameObject "QuestStartedBannerUI" — el aviso centrado "Nueva
/// misión: {nombre}" que Raúl pidió el 15 sept 2026 (ver cabecera de QuestStartedBannerUI.cs) y que
/// se quedó sin colocar en Start.unity: el script existía y se suscribía bien a
/// QuestManager.OnQuestStarted, pero al no haber ningún GameObject en la escena con ese componente,
/// el evento nunca tenía quien lo escuchara y la animación jamás llegaba a dispararse (ver
/// incidencia-banner-nueva-mision-no-aparece-nunca-instanciado-2026-09-17.md en el proyecto de
/// Cowork).
///
/// Sigue el mismo patrón que AbilityUnlockPopupUI, ya presente en esta misma escena: un GameObject
/// raíz con SceneBoundUI (persiste entre escenas, se desengancha de su padre) y, colgando de él, un
/// Canvas propio en Screen Space - Overlay (no comparte Canvas con el resto del HUD). El fondo del
/// aviso reutiliza `Assets/Art/UI/Misiones/header_pill_bg.png` — el mismo sprite "glass pill" de 9
/// slices que ya está en uso hoy en el HUD (Start.unity, un GameObject "Panel" bajo la jerarquía de
/// misiones) — y el título usa la misma fuente TMP que ya lleva el texto de AbilityUnlockPopupUI,
/// para que este aviso nuevo no introduzca un estilo distinto al resto de la UI (ver preferencia de
/// Raúl sobre coherencia visual/identidad de arte). No se asigna icono (el campo `icon` del script
/// ya contempla dejarse vacío: sin sprite, ese hueco simplemente no se muestra).
///
/// Idempotente: si "QuestStartedBannerUI" ya existe en la escena, avisa y no duplica nada.
/// </summary>
public static class QuestStartedBannerSceneWiring
{
    private const string HeaderPillSpriteGuid = "cf70966e009f4a8181df8ac98c68a807"; // Assets/Art/UI/Misiones/header_pill_bg.png
    private const string TitleFontGuid = "21a67e0dff06da94d848f1cf0d0074f8"; // misma fuente TMP que AbilityUnlockPopupUI.abilityTitleText

    [MenuItem("El Sendero/Archivo/UI/Crear Banner Nueva Misión")]
    public static void CreateBanner()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Start")
        {
            Debug.LogWarning("[QuestStartedBannerSceneWiring] La escena activa es '" + scene.name +
                "', no 'Start'. Abre Assets/Scenes/Systems/Start.unity (el Canvas del HUD persistente) y vuelve a ejecutar.");
            return;
        }

        if (GameObject.Find("QuestStartedBannerUI") != null)
        {
            Debug.LogWarning("[QuestStartedBannerSceneWiring] 'QuestStartedBannerUI' ya existe en la escena — no se toca nada. Si quieres reconstruirlo desde cero, bórralo a mano primero.");
            return;
        }

        var headerPillSprite = LoadAssetByGuid<Sprite>(HeaderPillSpriteGuid);
        var titleFont = LoadAssetByGuid<TMP_FontAsset>(TitleFontGuid);
        if (headerPillSprite == null)
            Debug.LogWarning("[QuestStartedBannerSceneWiring] No se encontró el sprite de fondo (guid " + HeaderPillSpriteGuid + ") — el banner se crea sin imagen de fondo, asígnala a mano en 'BannerRoot'.");
        if (titleFont == null)
            Debug.LogWarning("[QuestStartedBannerSceneWiring] No se encontró la fuente TMP (guid " + TitleFontGuid + ") — el texto se crea con la fuente por defecto de TextMeshPro.");

        // ── Raíz: persiste entre escenas, igual que AbilityUnlockPopupUI ────────────────────
        var root = new GameObject("QuestStartedBannerUI", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Crear QuestStartedBannerUI");

        var sceneBound = root.AddComponent<SceneBoundUI>();
        var sceneBoundSO = new SerializedObject(sceneBound);
        sceneBoundSO.FindProperty("uniqueId").stringValue = "QuestStartedBanner_UI";
        var allowedScenesProp = sceneBoundSO.FindProperty("allowedScenes");
        allowedScenesProp.arraySize = 2;
        allowedScenesProp.GetArrayElementAtIndex(0).stringValue = "MainWorld";
        allowedScenesProp.GetArrayElementAtIndex(1).stringValue = "PlayerTest";
        sceneBoundSO.FindProperty("allowWhenListEmpty").boolValue = false;
        sceneBoundSO.FindProperty("persistAcrossScenes").boolValue = true;
        sceneBoundSO.FindProperty("detachFromParent").boolValue = true;
        sceneBoundSO.FindProperty("excludeFromBossIntroHide").boolValue = false;
        sceneBoundSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Canvas propio en overlay, por encima de AbilityUnlockPopupUI (sortingOrder 99) ──
        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(root.transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0f;

        // ── BannerRoot: el panel "pill" centrado arriba de la pantalla ──────────────────────
        var bannerRootGO = new GameObject("BannerRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        bannerRootGO.transform.SetParent(canvasGO.transform, false);
        var bannerRT = bannerRootGO.GetComponent<RectTransform>();
        bannerRT.anchorMin = new Vector2(0.5f, 1f);
        bannerRT.anchorMax = new Vector2(0.5f, 1f);
        bannerRT.pivot = new Vector2(0.5f, 1f);
        bannerRT.sizeDelta = new Vector2(620, 130);
        bannerRT.anchoredPosition = new Vector2(0, -50);

        var bannerBg = bannerRootGO.GetComponent<Image>();
        bannerBg.sprite = headerPillSprite;
        bannerBg.type = Image.Type.Sliced;
        bannerBg.color = Color.white; // el propio sprite ya lleva su tinte "glass"
        bannerBg.raycastTarget = false;

        var bannerCanvasGroup = bannerRootGO.GetComponent<CanvasGroup>();
        bannerCanvasGroup.blocksRaycasts = false;
        bannerCanvasGroup.interactable = false;

        // ── Título ───────────────────────────────────────────────────────────────────────
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(bannerRootGO.transform, false);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(40, 14);
        titleRT.offsetMax = new Vector2(-40, -14);

        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        if (titleFont != null) titleText.font = titleFont;
        titleText.fontSize = 32;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;
        titleText.text = "Nueva misión"; // placeholder solo visible en el Editor -- HandleQuestStarted() lo sobreescribe en juego

        // ── Componente principal: enlaza las referencias vía SerializedObject (respeta Undo) ─
        var banner = root.AddComponent<QuestStartedBannerUI>();
        var bannerSO = new SerializedObject(banner);
        bannerSO.FindProperty("bannerRoot").objectReferenceValue = bannerRT;
        bannerSO.FindProperty("bannerCanvasGroup").objectReferenceValue = bannerCanvasGroup;
        bannerSO.FindProperty("titleText").objectReferenceValue = titleText;
        // "icon" se deja sin asignar a propósito -- sin sprite, ese hueco no se muestra.
        bannerSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[QuestStartedBannerSceneWiring] 'QuestStartedBannerUI' creado y conectado en Start.unity. Guarda la escena (Ctrl+S) para persistirlo. Pulsa Play y da inicio a una misión para verlo.");
    }

    private static T LoadAssetByGuid<T>(string guid) where T : Object
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
