using System.Collections.Generic;
using System.Text;
using Sendero.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Monta la carga de equipo, los dúos por pareja y el trío (INC-491). Idempotente.
/// <list type="bullet">
/// <item>Ataques: pone los miembros a SpecialAttack_Estela (Will+Estela) y SpecialAttack_Liam
/// (Will+Liam) y crea SpecialAttack_EstelaLiam y SpecialAttack_Trio si no existen.</item>
/// <item>GrupoDelJugador.prefab: una sola carga de equipo (SpecialMeter_Estela pasa a ser
/// "CargaDeEquipo", máximo 3 y 1 por dúo; SpecialMeter_Liam se quita) y enlaza los ataques.</item>
/// <item>Start.unity: quita SpecialChargeMeterUI (cargas en los retratos), agranda el panel de
/// estado y añade la barra de equipo (tres tramos y estrella) con TeamGaugeHUD.</item>
/// </list>
/// </summary>
public static class TeamAttackWiring
{
    private const string GroupPrefabPath = "Assets/Prefabs/GrupoDelJugador.prefab";
    private const string AttackFolder = "Assets/Scripts/Attacks/SO";
    private const string EstelaPath = AttackFolder + "/SpecialAttack_Estela.asset";
    private const string LiamPath = AttackFolder + "/SpecialAttack_Liam.asset";
    private const string EstelaLiamPath = AttackFolder + "/SpecialAttack_EstelaLiam.asset";
    private const string TrioPath = AttackFolder + "/SpecialAttack_Trio.asset";

    private const string ScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string SegmentBgPath = "Assets/Art/UI/HUD/segmento_equipo_fondo.png";
    private const string SegmentFillPath = "Assets/Art/UI/HUD/segmento_equipo_relleno.png";
    private const string StarPath = "Assets/Art/UI/HUD/estrella_equipo.png";

    private const float PanelWithoutTeam = 286f;
    private const float TeamRowHeight = 120f;

    [MenuItem("El Sendero/Archivo/Combate/Montar carga de equipo, dúos y trío (INC-491)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        ImportSprites(log);
        var attacks = EnsureAttacks(log, warnings);
        WireGroupPrefab(attacks, log, warnings);
        WireHud(log, warnings);

        var final = new StringBuilder("=== Carga de equipo, dúos y trío (INC-491) ===\n");
        final.Append(log);
        if (warnings.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    // ── Sprites ─────────────────────────────────────────────────────────────────────

    private static void ImportSprites(StringBuilder log)
    {
        SetSprite(SegmentBgPath, new Vector4(40, 0, 40, 0), log);
        SetSprite(SegmentFillPath, Vector4.zero, log);
        SetSprite(StarPath, Vector4.zero, log);
    }

    private static void SetSprite(string path, Vector4 border, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        log.AppendLine($"Importado como Sprite: {path}");
    }

    // ── Ataques ─────────────────────────────────────────────────────────────────────

    private struct Attacks
    {
        public SpecialAttackSO willEstela, willLiam, estelaLiam, trio;
    }

    private static Attacks EnsureAttacks(StringBuilder log, List<string> warnings)
    {
        var a = new Attacks
        {
            willEstela = AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(EstelaPath),
            willLiam = AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(LiamPath),
        };
        if (a.willEstela == null || a.willLiam == null)
        {
            warnings.Add("Faltan SpecialAttack_Estela o SpecialAttack_Liam; no se tocan los ataques.");
            return a;
        }

        SetDuo(a.willEstela, "Dúo Will y Estela", Slot.Will, Slot.Estela, log);
        SetDuo(a.willLiam, "Dúo Will y Liam", Slot.Will, Slot.Liam, log);

        a.estelaLiam = CopyIfMissing(EstelaPath, EstelaLiamPath, log);
        if (a.estelaLiam != null && a.estelaLiam.displayName != "Dúo Estela y Liam")
        {
            SetDuo(a.estelaLiam, "Dúo Estela y Liam", Slot.Estela, Slot.Liam, log);
            a.estelaLiam.vfxPrefab = a.willLiam.vfxPrefab; // mezcla: sonido de Estela, efecto de Liam
            EditorUtility.SetDirty(a.estelaLiam);
        }

        a.trio = CopyIfMissing(EstelaPath, TrioPath, log);
        if (a.trio != null && !a.trio.isTrio)
        {
            a.trio.displayName = "Trío";
            a.trio.isTrio = true;
            a.trio.damage = 200f;
            a.trio.aoeRadius = 6f;
            a.trio.knockbackForce = 25f;
            a.trio.gestureSeconds = 0.8f;
            a.trio.companionMaxDistance = 12f;
            EditorUtility.SetDirty(a.trio);
            log.AppendLine("Trío configurado (200 de daño, radio 6 m).");
        }

        AssetDatabase.SaveAssets();
        return a;
    }

    private static void SetDuo(SpecialAttackSO attack, string name, Slot first, Slot second, StringBuilder log)
    {
        if (attack.first == first && attack.second == second && !attack.isTrio && attack.displayName == name) return;
        attack.displayName = name;
        attack.first = first;
        attack.second = second;
        attack.isTrio = false;
        EditorUtility.SetDirty(attack);
        log.AppendLine($"{attack.name}: {name}.");
    }

    private static SpecialAttackSO CopyIfMissing(string from, string to, StringBuilder log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(to);
        if (existing != null) return existing;
        if (!AssetDatabase.CopyAsset(from, to)) return null;
        log.AppendLine($"Creado {to}.");
        return AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(to);
    }

    // ── Prefab del grupo ────────────────────────────────────────────────────────────

    private static void WireGroupPrefab(Attacks attacks, StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GroupPrefabPath) == null)
        {
            warnings.Add($"No encuentro '{GroupPrefabPath}'.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(GroupPrefabPath);
        try
        {
            var system = root.GetComponentInChildren<DuoSpecialAttackSystem>(true);
            if (system == null) { warnings.Add("GrupoDelJugador no tiene DuoSpecialAttackSystem."); return; }

            var so = new SerializedObject(system);
            var gaugeProp = so.FindProperty("teamGauge");
            var gauge = gaugeProp.objectReferenceValue as SpecialChargeMeter;

            // Otros medidores (el antiguo de Liam) sobran: una sola carga de equipo.
            foreach (var meter in root.GetComponentsInChildren<SpecialChargeMeter>(true))
            {
                if (gauge == null) { gauge = meter; continue; }
                if (meter == gauge) continue;
                log.AppendLine($"Quitado el medidor sobrante '{meter.gameObject.name}'.");
                Object.DestroyImmediate(meter.gameObject);
            }
            if (gauge == null)
            {
                var go = new GameObject("CargaDeEquipo");
                go.transform.SetParent(root.transform, false);
                gauge = go.AddComponent<SpecialChargeMeter>();
                log.AppendLine("Creada CargaDeEquipo.");
            }
            if (gauge.gameObject.name != "CargaDeEquipo") gauge.gameObject.name = "CargaDeEquipo";

            var gso = new SerializedObject(gauge);
            gso.FindProperty("maxCharge").floatValue = 3f;
            gso.FindProperty("chargeRequiredToUse").floatValue = 1f;
            gso.FindProperty("currentCharge").floatValue = 0f;
            gso.ApplyModifiedPropertiesWithoutUndo();

            gaugeProp.objectReferenceValue = gauge;
            var duos = so.FindProperty("duoAttacks");
            var list = new List<SpecialAttackSO>();
            if (attacks.willEstela) list.Add(attacks.willEstela);
            if (attacks.willLiam) list.Add(attacks.willLiam);
            if (attacks.estelaLiam) list.Add(attacks.estelaLiam);
            duos.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) duos.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.FindProperty("trioAttack").objectReferenceValue = attacks.trio;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GroupPrefabPath);
            log.AppendLine($"GrupoDelJugador: carga de equipo (3 tramos), {list.Count} dúos y trío enlazados.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── HUD ─────────────────────────────────────────────────────────────────────────

    private static void WireHud(StringBuilder log, List<string> warnings)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            Transform canvas = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                canvas = r.name == "PlayerHUD_Canvas" ? r.transform : FindDeep(r.transform, "PlayerHUD_Canvas");
                if (canvas != null) break;
            }
            if (canvas == null) { warnings.Add("No encuentro PlayerHUD_Canvas en Start.unity."); return; }

            int missing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject);
            if (missing > 0) log.AppendLine($"Quitado(s) {missing} componente(s) sin script del HUD (SpecialChargeMeterUI).");

            var main = canvas.Find("Main") as RectTransform;
            var bg = canvas.Find("Main/BG") as RectTransform;
            if (main == null || bg == null) { warnings.Add("No encuentro Main/BG."); return; }

            if (bg.Find("PanelTeam") != null)
            {
                log.AppendLine("PanelTeam ya existe: no se vuelve a crear.");
            }
            else
            {
                float h = PanelWithoutTeam + TeamRowHeight;
                main.sizeDelta = new Vector2(main.sizeDelta.x, h);
                bg.sizeDelta = new Vector2(bg.sizeDelta.x, h);
                BuildTeamRow(bg, log, warnings);
                log.AppendLine($"Panel de estado: alto {PanelWithoutTeam} → {h}.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("Start.unity guardada.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildTeamRow(RectTransform bg, StringBuilder log, List<string> warnings)
    {
        var segBg = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentBgPath);
        var segFill = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentFillPath);
        var star = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
        if (segBg == null || segFill == null || star == null)
            warnings.Add("Faltan los sprites de la barra de equipo en Art/UI/HUD; la barra se crea sin ellos.");

        var row = NewRect("PanelTeam", bg);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0f);
        row.sizeDelta = new Vector2(bg.sizeDelta.x, TeamRowHeight);
        row.anchoredPosition = Vector2.zero;
        var group = row.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        const float segW = 190f, segH = 44f, gap = 16f, starSize = 96f, starGap = 18f;
        float total = segW * 3 + gap * 2 + starGap + starSize;
        float x = -total * 0.5f;
        float y = TeamRowHeight * 0.52f;

        var fills = new UnityEngine.UI.Image[3];
        for (int i = 0; i < 3; i++)
        {
            var seg = NewRect("Segment" + (i + 1), row);
            Place(seg, new Vector2(x + segW * 0.5f, y), new Vector2(segW, segH));
            var bgImg = AddImage(seg, segBg, Color.white);
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;

            var fill = NewRect("Fill", seg);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(6f, 6f); fill.offsetMax = new Vector2(-6f, -6f);
            var fillImg = AddImage(fill, segFill, new Color(0.95f, 0.76f, 0.31f, 0.75f));
            fillImg.type = UnityEngine.UI.Image.Type.Filled;
            fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;
            fills[i] = fillImg;

            x += segW + gap;
        }

        var starRt = NewRect("Star", row);
        Place(starRt, new Vector2(x - gap + starGap + starSize * 0.5f, y), new Vector2(starSize, starSize));
        var starImg = AddImage(starRt, star, new Color(1f, 1f, 1f, 0.3f));
        starImg.preserveAspect = true;

        var hud = row.gameObject.AddComponent<TeamGaugeHUD>();
        var so = new SerializedObject(hud);
        var arr = so.FindProperty("segmentFills");
        arr.arraySize = 3;
        for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
        so.FindProperty("star").objectReferenceValue = starImg;
        so.FindProperty("group").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("Creada la barra de equipo (PanelTeam) con TeamGaugeHUD.");
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Place(RectTransform rt, Vector2 center, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = size;
    }

    private static UnityEngine.UI.Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var t = FindDeep(child, name);
            if (t != null) return t;
        }
        return null;
    }
}
