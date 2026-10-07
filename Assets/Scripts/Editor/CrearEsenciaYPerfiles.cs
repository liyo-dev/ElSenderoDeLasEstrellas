using System;
using System.IO;
using Sendero.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Prepara los datos de economía sin modificar el arte original de los orbes.</summary>
public static class CrearEsenciaYPerfiles
{
    private const string Carpeta = "Assets/Prefabs/Orbes";
    private const string Inicio = "Assets/Scenes/Systems/Start.unity";
    private const string Script = "Assets/Scripts/Editor/CrearEsenciaYPerfiles.cs";

    [MenuItem("El Sendero/Archivo/Crear Esencia y perfiles de drop")]
    public static void Crear()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play antes de preparar la economía.");
        var escena = SceneManager.GetSceneByPath(Inicio);
        if (escena.isLoaded && escena.isDirty)
            throw new InvalidOperationException("Guarda Start antes de preparar la economía.");
        CarpetaSiFalta(Carpeta + "/Materiales");
        var item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Esencia.asset");
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, "Assets/_ITEMS/IT_Esencia.asset");
        }
        item.itemId = "esencia";
        item.displayName = "Esencia";
        item.displayNameId = "ITEM_ESENCIA_NAME";
        item.useDescriptionId = "ITEM_ESENCIA_DESC";
        item.usageKind = ItemData.ItemUsageKind.Currency;
        item.buyPrice = item.sellValue = 0;
        item.usableFromInventory = false;
        EditorUtility.SetDirty(item);
        var normal = CrearOrbe("Orbe_Esencia", "Basic Orb", false);
        var brillante = CrearOrbe("Orbe_EsenciaBrillante", "Light Orb 2", true);
        ConfigurarPerfiles(item, normal, brillante);
        RegistrarItem(item);
        foreach (string path in new[] { "Assets/Prefabs/Enemy/Demon.prefab", "Assets/Prefabs/Enemy/Demon2.prefab",
                     "Assets/Prefabs/Enemy/PBR_Golem.prefab", "Assets/Prefabs/_MAGO_OSCURO.prefab" })
            PerfilEnPrefab(path, "boss");
        foreach (string path in new[] { "Assets/_NPCs/Combat/Mago #1.prefab", "Assets/_NPCs/Combat/Mago #2.prefab",
                     "Assets/_NPCs/Combat/Mago #3.prefab" })
            PerfilEnPrefab(path, "elite");
        MontarHUD(item);
        AssetDatabase.SaveAssets();
        // El comando sigue siendo idempotente y queda disponible entre las herramientas archivadas.
        string fuente = File.ReadAllText(Script);
        fuente = fuente.Replace("[MenuItem(\"El Sendero/Economía/Crear Esencia y perfiles de drop\")]",
            "[MenuItem(\"El Sendero/Archivo/Crear Esencia y perfiles de drop\")]");
        File.WriteAllText(Script, fuente, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(Script);
        EditorUtility.DisplayDialog("Economía preparada", "Esencia, orbes, perfiles y contador guardados. Revisa su aspecto y prueba los drops en Play.", "Aceptar");
    }

    private static void CarpetaSiFalta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        CarpetaSiFalta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    private static GameObject CrearOrbe(string nombre, string origen, bool especial)
    {
        string ruta = Carpeta + "/" + nombre + ".prefab";
        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (existente != null) return existente;
        var pack = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/" + origen + ".prefab");
        if (pack == null) throw new InvalidOperationException("No se encuentra el orbe original: " + origen);
        var go = UnityEngine.Object.Instantiate(pack);
        try
        {
            go.name = nombre;
            go.transform.localScale *= especial ? 1.3f : 1f;
            Color oro = new Color(1f, 0.788f, 0.302f);
            int indice = 0;
            foreach (var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var materiales = renderer.sharedMaterials;
                for (int i = 0; i < materiales.Length; i++)
                {
                    if (materiales[i] == null) continue;
                    string path = Carpeta + "/Materiales/" + nombre + "_" + indice++ + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(materiales[i]);
                        foreach (string propiedad in new[] { "_Color", "_BaseColor", "_TintColor" })
                            if (material.HasProperty(propiedad)) material.SetColor(propiedad, oro * (especial ? 1.6f : 1.15f));
                        AssetDatabase.CreateAsset(material, path);
                    }
                    materiales[i] = material;
                }
                renderer.sharedMaterials = materiales;
                var trail = renderer.trailMaterial;
                if (trail != null)
                {
                    string path = Carpeta + "/Materiales/" + nombre + "_Trail_" + indice++ + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(trail);
                        if (material.HasProperty("_Color")) material.SetColor("_Color", oro);
                        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", oro);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    renderer.trailMaterial = material;
                }
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = oro;
                var color = ps.colorOverLifetime;
                color.enabled = false;
                var colorPorVelocidad = ps.colorBySpeed;
                colorPorVelocidad.enabled = false;
                if (especial)
                {
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier *= 1.5f;
                }
            }
            var nucleo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nucleo.name = "NucleoVioleta";
            nucleo.transform.SetParent(go.transform, false);
            nucleo.transform.localScale = Vector3.one * 0.16f;
            UnityEngine.Object.DestroyImmediate(nucleo.GetComponent<Collider>());
            string corePath = Carpeta + "/Materiales/" + nombre + "_Nucleo.mat";
            var core = AssetDatabase.LoadAssetAtPath<Material>(corePath);
            if (core == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("No se encuentra el shader URP Unlit.");
                core = new Material(shader);
                core.SetColor("_BaseColor", new Color(0.58f, 0.16f, 1f));
                AssetDatabase.CreateAsset(core, corePath);
            }
            nucleo.GetComponent<Renderer>().sharedMaterial = core;
            return PrefabUtility.SaveAsPrefabAsset(go, ruta);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void ConfigurarPerfiles(ItemData item, GameObject normal, GameObject brillante)
    {
        var config = AssetDatabase.LoadAssetAtPath<OrbDropConfig>("Assets/Scripts/Battle/OrbDropConfig.asset");
        if (config == null) throw new InvalidOperationException("Falta OrbDropConfig.");
        var so = new SerializedObject(config);
        var defecto = so.FindProperty("_defaultProfile");
        var plantilla = JsonUtility.ToJson(config.GetProfile(null));
        var profiles = so.FindProperty("_profiles");
        foreach (string id in new[] { "elite", "boss" })
        {
            int index = -1;
            for (int i = 0; i < profiles.arraySize; i++)
                if (profiles.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id) index = i;
            if (index < 0)
            {
                index = profiles.arraySize++;
                // Copia los parámetros originales de vida/maná, incluidos sonidos y dispersión.
                profiles.GetArrayElementAtIndex(index).boxedValue = JsonUtility.FromJson<OrbDropProfile>(plantilla);
            }
            ConfigurarMoneda(profiles.GetArrayElementAtIndex(index), id, id == "boss" ? 40 : 6,
                id == "boss" ? 60 : 10, item, normal, brillante);
        }
        ConfigurarMoneda(defecto, "default", 2, 4, item, normal, brillante);
        var mappings = so.FindProperty("_tagMappings");
        var monedasDelInforme = so.FindProperty("_currenciesForBattleReport");
        bool registrada = false;
        for (int i = 0; i < monedasDelInforme.arraySize; i++)
            if (monedasDelInforme.GetArrayElementAtIndex(i).objectReferenceValue == item) registrada = true;
        if (!registrada)
        {
            monedasDelInforme.InsertArrayElementAtIndex(monedasDelInforme.arraySize);
            monedasDelInforme.GetArrayElementAtIndex(monedasDelInforme.arraySize - 1).objectReferenceValue = item;
        }
        int mapping = -1;
        for (int i = 0; i < mappings.arraySize; i++)
            if (mappings.GetArrayElementAtIndex(i).FindPropertyRelative("tag").stringValue == "Boss") mapping = i;
        if (mapping < 0) mapping = mappings.arraySize++;
        mappings.GetArrayElementAtIndex(mapping).FindPropertyRelative("tag").stringValue = "Boss";
        mappings.GetArrayElementAtIndex(mapping).FindPropertyRelative("profileId").stringValue = "boss";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    private static void ConfigurarMoneda(SerializedProperty perfil, string id, int min, int max,
        ItemData item, GameObject normal, GameObject brillante)
    {
        perfil.FindPropertyRelative("id").stringValue = id;
        perfil.FindPropertyRelative("currencyItem").objectReferenceValue = item;
        perfil.FindPropertyRelative("currencyOrbPrefab").objectReferenceValue = normal;
        perfil.FindPropertyRelative("currencyDeathMin").intValue = min;
        perfil.FindPropertyRelative("currencyDeathMax").intValue = max;
        perfil.FindPropertyRelative("currencyPickupSFXKey").stringValue = "Coin";
        perfil.FindPropertyRelative("specialCurrencyOrbPrefab").objectReferenceValue = brillante;
        perfil.FindPropertyRelative("specialCurrencyChance").floatValue = 0.05f;
        perfil.FindPropertyRelative("specialCurrencyMultiplier").intValue = 3;
        perfil.FindPropertyRelative("specialCurrencyPickupSFXKey").stringValue = "puzzle_done";
    }

    private static void RegistrarItem(ItemData item)
    {
        var registry = AssetDatabase.LoadAssetAtPath<ItemRegistrySO>("Assets/Resources/ItemRegistry.asset");
        if (registry == null) throw new InvalidOperationException("Falta el registro de items.");
        var so = new SerializedObject(registry);
        var items = so.FindProperty("items");
        for (int i = 0; i < items.arraySize; i++)
            if (items.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
        items.InsertArrayElementAtIndex(items.arraySize);
        items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        registry.Rebuild();
        EditorUtility.SetDirty(registry);
    }

    private static void PerfilEnPrefab(string path, string id)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            throw new InvalidOperationException("Falta el enemigo: " + path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var damageables = root.GetComponentsInChildren<Damageable>(true);
            if (damageables.Length == 0)
            {
                // Los NPC de combate pueden crear su salud en runtime. Se conserva su configuración.
                var manager = root.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
                if (manager == null) throw new InvalidOperationException("No hay salud ni configuración de combate en " + path);
                var config = new SerializedObject(manager).FindProperty("configuration.combatConfig").objectReferenceValue;
                if (config == null) throw new InvalidOperationException("Falta la configuración de combate en " + path);
                var health = new SerializedObject(config).FindProperty("health");
                if (health == null) throw new InvalidOperationException("Falta la salud de combate en " + path);
                var damageable = root.AddComponent<Damageable>();
                var data = new SerializedObject(damageable);
                data.FindProperty("maxHealth").floatValue = health.floatValue;
                data.FindProperty("destroyOnDeath").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                damageables = new[] { damageable };
            }
            foreach (var damageable in damageables)
            {
                var dropper = damageable.GetComponent<OrbDropper>();
                if (dropper == null) dropper = damageable.gameObject.AddComponent<OrbDropper>();
                var so = new SerializedObject(dropper);
                so.FindProperty("_profileId").stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dropper);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void MontarHUD(ItemData item)
    {
        var scene = SceneManager.GetSceneByPath(Inicio);
        bool abrir = !scene.isLoaded;
        if (abrir) scene = EditorSceneManager.OpenScene(Inicio, OpenSceneMode.Additive);
        try
        {
            PlayerHUDV2 hud = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                hud = root.GetComponentInChildren<PlayerHUDV2>(true);
                if (hud != null) break;
            }
            if (hud == null) throw new InvalidOperationException("No se encuentra PlayerHUDV2 en Start.");
            var existente = hud.transform.Find("Contador_Esencia");
            if (existente != null) return;
            var go = new GameObject("Contador_Esencia", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 80f);
            rect.sizeDelta = new Vector2(250f, 64f);
            go.GetComponent<Image>().color = PaletaUI.FondoPanel;
            go.GetComponent<Image>().raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            var iconGO = new GameObject("Icono", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(32f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            var icon = iconGO.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.color = PaletaUI.Lavanda;
            var total = Texto(go.transform, "Total", new Vector2(145f, 0f));
            var gain = Texto(go.transform, "Ganancia", new Vector2(145f, 48f));
            var counter = go.AddComponent<ContadorDeMonedaHUD>();
            counter.Configurar(item, null, total, icon);
            var so = new SerializedObject(counter);
            so.FindProperty("grupo").objectReferenceValue = group;
            so.FindProperty("ganancia").objectReferenceValue = gain;
            so.FindProperty("textoFlotante").objectReferenceValue = gain.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(counter);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (abrir) EditorSceneManager.CloseScene(scene, true); }
    }

    private static TextMeshProUGUI Texto(Transform padre, string nombre, Vector2 posicion)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        text.rectTransform.anchoredPosition = posicion;
        text.rectTransform.sizeDelta = new Vector2(160f, 50f);
        text.fontSize = 28f;
        text.color = PaletaUI.Lavanda;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}
