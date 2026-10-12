using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Monta el cuadro de estadísticas en Start mediante las APIs del Editor.</summary>
public static class PanelDeEstadisticasBuilder
{
    [MenuItem("El Sendero/UI/Equipo · cuadro de estadísticas (INC-677)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El cuadro de estadísticas se monta fuera de Play."); return; }
        var avisos = new List<string>();
        const string ruta = "Assets/Scenes/Systems/Start.unity";
        var escena = SceneManager.GetSceneByPath(ruta);
        if (!escena.isLoaded) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        PlayerEquipmentMenuController menu = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            menu = raiz.GetComponentInChildren<PlayerEquipmentMenuController>(true);
            if (menu != null) break;
        }
        if (menu == null) { Debug.LogWarning("Start no contiene PlayerEquipmentMenuController."); return; }
        var so = new SerializedObject(menu);
        var equipo = so.FindProperty("equipmentUI");
        var root = equipo.FindPropertyRelative("root").objectReferenceValue as GameObject;
        if (root == null || !root.TryGetComponent(out RectTransform rt))
        { Debug.LogWarning("Equipo no tiene un root con RectTransform."); return; }
        Canvas.ForceUpdateCanvases();
        var rows = equipo.FindPropertyRelative("rows");
        Text estilo = null;
        float derecha = rt.rect.xMin, arriba = rt.rect.yMax;
        var esquinas = new Vector3[4];
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var label = row.FindPropertyRelative("label").objectReferenceValue as Text;
            if (label != null)
            {
                if (estilo == null) estilo = label;
                label.supportRichText = true;
                EditorUtility.SetDirty(label);
            }
            foreach (string campo in new[] { "label", "icon", "previousButton", "nextButton", "clearButton" })
            {
                var componente = row.FindPropertyRelative(campo).objectReferenceValue as Component;
                if (componente == null || !(componente.transform is RectTransform fila)) continue;
                fila.GetWorldCorners(esquinas);
                foreach (var esquina in esquinas)
                {
                    var local = rt.InverseTransformPoint(esquina);
                    derecha = Mathf.Max(derecha, local.x);
                    arriba = Mathf.Min(arriba, local.y);
                }
            }
        }
        if (estilo == null) { Debug.LogWarning("Equipo no tiene una etiqueta de fila para copiar el estilo."); return; }
        var panel = root.transform.Find("PanelEstadisticas") as RectTransform;
        if (panel == null)
        {
            var existentes = root.GetComponentsInChildren<PanelDeEstadisticasUI>(true);
            if (existentes.Length > 0) panel = existentes[0].transform as RectTransform;
        }
        if (panel == null) panel = new GameObject("PanelEstadisticas", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.name = "PanelEstadisticas";
        panel.SetParent(rt, false);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        float ancho = Mathf.Max(260f, estilo.fontSize * 13f);
        float altoFila = Mathf.Max(32f, estilo.fontSize * 1.6f);
        float alto = altoFila * 5f;
        float espacio = rt.rect.xMax - derecha - 32f;
        float y = -20f;
        if (espacio < ancho)
        {
            y = arriba - rt.rect.yMax - 20f;
            avisos.Add("Sin espacio a la derecha de las filas: el panel queda debajo, alineado a la derecha; revisar su posición.");
        }
        if (-y + alto > rt.rect.height || ancho > rt.rect.width)
            avisos.Add("El cuadro excede el espacio del root; ajustar el RectTransform en el Editor.");
        panel.anchoredPosition = new Vector2(-16f, y);
        panel.sizeDelta = new Vector2(ancho, alto);
        var layout = panel.GetComponent<LayoutElement>();
        if (layout == null) layout = panel.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        var ui = panel.GetComponent<PanelDeEstadisticasUI>();
        if (ui == null) ui = panel.gameObject.AddComponent<PanelDeEstadisticasUI>();
        var panelSo = new SerializedObject(ui);
        panelSo.FindProperty("titulo").objectReferenceValue = CrearTexto(panel, "Titulo", estilo, 0, altoFila, "Estadísticas");
        var filas = panelSo.FindProperty("filas");
        filas.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var tipo = (TipoDeEstadistica)i;
            filas.GetArrayElementAtIndex(i).objectReferenceValue =
                CrearTexto(panel, tipo.ToString(), estilo, i + 1, altoFila, TextoDeEstadisticas.Nombre(tipo) + ": —");
        }
        panelSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
        var capa = AssetDatabase.LoadAssetAtPath<WardrobeItemSO>("Assets/_WARDROBE ITEMS/WardrobeItem_Cloak02.asset");
        if (capa == null) avisos.Add("No se encuentra WardrobeItem_Cloak02; su defensa no se modifica.");
        else
        {
            var capaSo = new SerializedObject(capa);
            capaSo.FindProperty("bonos").FindPropertyRelative("defensa").floatValue = 8f;
            capaSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(capa);
        }
        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        if (!guardada) avisos.Add("No se ha podido guardar Start.");
        string resultado = "INC-677: cuadro montado con título y cuatro filas; etiquetas con rich text; " +
            (capa != null ? "capa de Victoria con defensa +8. " : "") + (guardada ? "Start guardada." : "");
        if (avisos.Count == 0) Debug.Log(resultado);
        else Debug.LogWarning(resultado + "\n" + string.Join("\n", avisos));
    }

    private static Text CrearTexto(RectTransform panel, string nombre, Text estilo, int indice, float alto, string valor)
    {
        var hijo = panel.Find(nombre);
        var go = hijo != null ? hijo.gameObject : new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var texto = go.GetComponent<Text>();
        if (texto == null) texto = go.AddComponent<Text>();
        texto.font = estilo.font;
        texto.fontSize = estilo.fontSize;
        texto.fontStyle = estilo.fontStyle;
        texto.color = estilo.color;
        texto.lineSpacing = estilo.lineSpacing;
        texto.supportRichText = true;
        texto.raycastTarget = false;
        texto.alignment = TextAnchor.MiddleLeft;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        texto.text = valor;
        var rt = texto.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -indice * alto);
        rt.sizeDelta = new Vector2(0f, alto);
        EditorUtility.SetDirty(texto);
        return texto;
    }
}