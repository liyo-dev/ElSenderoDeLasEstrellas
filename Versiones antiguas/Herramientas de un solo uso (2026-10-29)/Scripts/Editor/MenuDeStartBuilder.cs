using System.Collections.Generic;
using System.Text;
using Core.InputGlyphs;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Repara el menú de Start y las definiciones de objetos mediante las APIs del Editor.</summary>
public static class MenuDeStartBuilder
{
    private const string StartPath = "Assets/Scenes/Systems/Start.unity";
    private const string MainMenuPath = "Assets/Scenes/Systems/MainMenu.unity";
    private const string ControlsName = "Controles (menú de Start)";

    [MenuItem("El Sendero/UI/Menú de Start · controles, monedas y objetos (INC-634)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El montaje del menú de Start se ejecuta fuera de Play."); return; }
        var log = new StringBuilder("=== Menú de Start · controles, monedas y objetos ===\n");
        var avisos = new List<string>();
        Montar(log, avisos);
        foreach (string aviso in avisos) log.AppendLine("• " + aviso);
        if (avisos.Count == 0) Debug.Log(log.ToString());
        else Debug.LogWarning(log.ToString());
    }

    public static void Montar(StringBuilder log, List<string> avisos)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { avisos.Add("El montaje requiere salir de Play."); return; }
        var scene = SceneManager.GetSceneByPath(StartPath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Additive);
        var menu = BuscarEnEscena<PlayerEquipmentMenuController>(scene);
        if (menu == null) { avisos.Add("Start no contiene PlayerEquipmentMenuController."); return; }
        var so = new SerializedObject(menu);
        MontarControles(menu, so, avisos);
        var barra = TarjetasDeAyudaBuilder.Buscar(menu.transform, "PanelInfo");
        if (barra == null) avisos.Add("No se encuentra PanelInfo en el menú de Start.");
        else
        {
            var tarjeta = TarjetasDeAyudaBuilder.Montar(barra, "PanelInfoControles", InputGlyphNames.West,
                "MENU_CONTROLS_HINT", "Controles", true, avisos);
            if (tarjeta != null)
            {
                var boton = tarjeta.GetComponent<Button>();
                if (boton == null) boton = tarjeta.AddComponent<Button>();
                boton.targetGraphic = tarjeta.GetComponent<Image>();
                boton.onClick = new Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(boton.onClick, menu.AbrirControles);
                // La tarjeta ofrece acceso con ratón sin entrar en la navegación de las listas.
                boton.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }
        var coin = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Coin.asset");
        var esencia = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Esencia.asset");
        var contadores = new List<ContadorDeMonedas>();
        if (coin == null) avisos.Add("No se encuentra IT_Coin.");
        else
        {
            var coinSo = new SerializedObject(coin);
            coinSo.FindProperty("usageKind").enumValueIndex = (int)ItemData.ItemUsageKind.Currency;
            coinSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coin);
            if (barra != null)
            {
                var contador = MontarContador(so, barra, coin, "ContadorDeMonedas", 12f, false, avisos);
                if (contador != null)
                {
                    contadores.Add(contador);
                    so.FindProperty("contadorDeMonedas").objectReferenceValue = contador;
                }
            }
        }
        if (esencia == null) avisos.Add("Falta IT_Esencia: ejecuta el menú de economía de INC-635 y vuelve a montar el menú de Start.");
        else if (barra != null)
        {
            var contador = MontarContador(so, barra, esencia, "Contador_Esencia_Menu", 216f, true, avisos);
            if (contador != null) contadores.Add(contador);
        }
        var listaContadores = so.FindProperty("contadoresDeMonedas");
        listaContadores.arraySize = contadores.Count;
        for (int i = 0; i < contadores.Count; i++)
            listaContadores.GetArrayElementAtIndex(i).objectReferenceValue = contadores[i];
        if (barra != null) TarjetasDeAyudaBuilder.AjustarBarra(barra, avisos);
        var jam = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Mermelada.asset");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("ec996c98e7ca4d0aabfbd34531c5624a"));
        if (jam == null || sprite == null) avisos.Add("Falta IT_Mermelada o su sprite correcto.");
        else
        {
            var jamSo = new SerializedObject(jam);
            jamSo.FindProperty("icon").objectReferenceValue = sprite;
            jamSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(jam);
        }
        foreach (var child in menu.GetComponentsInChildren<Transform>(true))
            if (child != null && child.name == "PistaControles") Object.DestroyImmediate(child.gameObject);
        bool contadorDelHudRetirado = RetirarContadorDelHud(scene);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        int registrados = ItemRegistrySincronizador.Sincronizar();
        log.AppendLine("Panel de controles reparado: sin canvas anidado, estirado y encima del menú.");
        log.AppendLine("Tarjeta Controles en todas las pestañas; pista antigua retirada; contadores de monedas montados.");
        if (contadorDelHudRetirado)
            log.AppendLine("Contador de Esencia del HUD retirado: lo ganado sale en el pop-up de objetos. Ver INC-642.");
        log.AppendLine($"Moneda clasificada como Currency; icono de mermelada corregido; registro con {registrados} ItemData. Start guardada.");
    }

    private static void MontarControles(PlayerEquipmentMenuController menu, SerializedObject so, List<string> avisos)
    {
        var prop = so.FindProperty("controlsMenu");
        var controls = prop.objectReferenceValue as ControlsMenuController;
        var canvas = so.FindProperty("canvas").objectReferenceValue as Canvas;
        Transform parent = canvas != null ? canvas.transform : menu.transform;
        if (controls == null || controls.gameObject.scene != menu.gameObject.scene)
        {
            var existente = parent.Find(ControlsName);
            if (existente != null) controls = existente.GetComponent<ControlsMenuController>();
            if (controls == null)
            {
                var sourceScene = SceneManager.GetSceneByPath(MainMenuPath);
                bool opened = !sourceScene.isLoaded;
                if (opened) sourceScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive);
                try
                {
                    var source = BuscarEnEscena<ControlsMenuController>(sourceScene);
                    if (source == null) { avisos.Add("MainMenu no contiene ControlsMenuController para copiar."); return; }
                    controls = Object.Instantiate(source.gameObject, parent, false).GetComponent<ControlsMenuController>();
                }
                finally { if (opened) EditorSceneManager.CloseScene(sourceScene, true); }
            }
        }
        controls.name = ControlsName;
        controls.transform.SetParent(parent, false);
        foreach (var button in controls.GetComponentsInChildren<Button>(true))
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = button.onClick.GetPersistentTarget(i);
                bool interno = target is Component c && c.transform.IsChildOf(controls.transform)
                            || target is GameObject g && g.transform.IsChildOf(controls.transform);
                if (!interno) UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
        Quitar<GraphicRaycaster>(controls.gameObject);
        Quitar<CanvasScaler>(controls.gameObject);
        Quitar<Canvas>(controls.gameObject);
        var rt = controls.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(.5f, .5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        var pos = rt.localPosition; pos.z = 0; rt.localPosition = pos;
        controls.transform.SetAsLastSibling();
        var controlsSo = new SerializedObject(controls);
        controlsSo.FindProperty("root").objectReferenceValue = controls.gameObject;
        controlsSo.ApplyModifiedPropertiesWithoutUndo();
        controls.gameObject.SetActive(false);
        prop.objectReferenceValue = controls;
        EditorUtility.SetDirty(controls);
    }

    private static ContadorDeMonedas MontarContador(SerializedObject so, Transform barra,
                                       ItemData moneda, string nombre, float posicionX, bool ocultarSinCantidad, List<string> avisos)
    {
        var window = so.FindProperty("windowRoot").objectReferenceValue as GameObject;
        if (window == null) { avisos.Add("El menú no tiene windowRoot para alojar el contador."); return null; }
        var existing = TarjetasDeAyudaBuilder.Buscar(window.transform, nombre);
        var go = existing != null ? existing.gameObject : new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(barra, false);
        var element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, .5f);
        rt.pivot = new Vector2(0, .5f);
        rt.anchoredPosition = new Vector2(posicionX, 0);
        rt.sizeDelta = new Vector2(196, 64);
        rt.localScale = Vector3.one;
        var fondo = go.GetComponent<Image>();
        if (fondo == null) fondo = go.AddComponent<Image>();
        fondo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(TarjetasDeAyudaBuilder.FondoGuid));
        fondo.type = Image.Type.Sliced;
        fondo.raycastTarget = false;
        var iconGo = Hijo(go.transform, "CoinIcon");
        var icon = iconGo.GetComponent<Image>();
        if (icon == null) icon = iconGo.AddComponent<Image>();
        icon.sprite = moneda.icon; icon.preserveAspect = true; icon.raycastTarget = false;
        Rect((RectTransform)iconGo.transform, new Vector2(12, 0), new Vector2(46, 46));
        var textGo = Hijo(go.transform, "Cantidad");
        var text = textGo.GetComponent<TextMeshProUGUI>();
        if (text == null) text = textGo.AddComponent<TextMeshProUGUI>();
        var reference = barra.GetComponentInChildren<TextMeshProUGUI>(true);
        if (reference != null && reference != text) { text.font = reference.font; text.fontSharedMaterial = reference.fontSharedMaterial; }
        text.fontSize = 26; text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 26;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.text = "0";
        Rect((RectTransform)textGo.transform, new Vector2(62, 0), new Vector2(122, 50));
        var counter = go.GetComponent<ContadorDeMonedas>();
        if (counter == null) counter = go.AddComponent<ContadorDeMonedas>();
        counter.Configurar(moneda, null, text, icon, ocultarSinCantidad);
        go.SetActive(true);
        EditorUtility.SetDirty(counter);
        return counter;
    }

    /// <summary>Quita el contador de Esencia que había en el HUD de juego (lo sustituye el pop-up de objetos).</summary>
    private static bool RetirarContadorDelHud(Scene scene)
    {
        var hud = BuscarEnEscena<Sendero.UI.PlayerHUDV2>(scene);
        var contador = hud != null ? hud.transform.Find("Contador_Esencia") : null;
        if (contador == null) return false;
        Object.DestroyImmediate(contador.gameObject);
        return true;
    }

    private static GameObject Hijo(Transform parent, string nombre)
    {
        var child = parent.Find(nombre);
        if (child != null) return child.gameObject;
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Rect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, .5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size; rt.localScale = Vector3.one;
    }

    private static void Quitar<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null) Object.DestroyImmediate(component);
    }

    private static T BuscarEnEscena<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }
}
