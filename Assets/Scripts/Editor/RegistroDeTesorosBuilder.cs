using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta el recuadro «Tesoros» (registro de tesoros por región) en la pestaña de objetos del
/// menú de Start, abajo a la derecha. Si ya existe, solo repasa sus referencias y su fuente: no
/// lo mueve, por si se ha recolocado a mano. Ver INC-721.
/// </summary>
public static class RegistroDeTesorosBuilder
{
    private const string StartPath = "Assets/Scenes/Systems/Start.unity";
    private const string Nombre = "RegistroDeTesoros";

    [MenuItem("El Sendero/UI/Menú de Start · registro de tesoros (INC-721)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El registro de tesoros se monta fuera de Play."); return; }

        var escena = SceneManager.GetSceneByPath(StartPath);
        if (!escena.isLoaded) escena = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Additive);
        PlayerEquipmentMenuController menu = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            menu = raiz.GetComponentInChildren<PlayerEquipmentMenuController>(true);
            if (menu != null) break;
        }
        if (menu == null) { Debug.LogWarning("[Tesoros] Start no contiene PlayerEquipmentMenuController."); return; }

        var so = new SerializedObject(menu);
        var panel = so.FindProperty("inventoryUI.root").objectReferenceValue as GameObject;
        var muestra = so.FindProperty("inventoryUI.itemName").objectReferenceValue as Text;
        if (panel == null) { Debug.LogWarning("[Tesoros] El menú de Start no tiene la pestaña de objetos (inventoryUI.root)."); return; }

        var existente = panel.transform.Find(Nombre);
        bool nuevo = existente == null;
        RectTransform caja;
        if (nuevo)
        {
            caja = new GameObject(Nombre, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            caja.SetParent(panel.transform, false);
            caja.anchorMin = caja.anchorMax = new Vector2(1f, 0f);
            caja.pivot = new Vector2(1f, 0f);
            caja.anchoredPosition = new Vector2(-24f, 24f);
            caja.sizeDelta = new Vector2(360f, 210f);
            caja.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        }
        else caja = (RectTransform)existente;

        var titulo = Texto(caja, "Titulo", muestra, 28, FontStyle.Bold, TextAnchor.UpperLeft,
                           new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -52f), new Vector2(-16f, -10f));
        var lista = Texto(caja, "Lista", muestra, 22, FontStyle.Normal, TextAnchor.UpperLeft,
                          new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 12f), new Vector2(-16f, -56f));

        var ui = caja.GetComponent<RegistroDeTesorosUI>();
        if (ui == null) ui = caja.gameObject.AddComponent<RegistroDeTesorosUI>();
        var soUi = new SerializedObject(ui);
        soUi.FindProperty("titulo").objectReferenceValue = titulo;
        soUi.FindProperty("lista").objectReferenceValue = lista;
        soUi.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(caja.gameObject);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log(nuevo
            ? "Registro de tesoros montado en Start ▸ pestaña Objetos (abajo a la derecha). Start guardada."
            : "Registro de tesoros repasado (referencias y fuente). Start guardada.");
    }

    private static Text Texto(RectTransform padre, string nombre, Text muestra, int tamano, FontStyle estilo, TextAnchor ancla,
                              Vector2 anclaMin, Vector2 anclaMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var hijo = padre.Find(nombre);
        Text texto;
        if (hijo == null)
        {
            var rt = new GameObject(nombre, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            rt.SetParent(padre, false);
            rt.anchorMin = anclaMin;
            rt.anchorMax = anclaMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            texto = rt.GetComponent<Text>();
            texto.fontSize = tamano;
            texto.fontStyle = estilo;
            texto.alignment = ancla;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Truncate;
            texto.raycastTarget = false;
            texto.color = muestra != null ? muestra.color : Color.white;
        }
        else texto = hijo.GetComponent<Text>();
        if (texto != null && muestra != null && muestra.font != null) texto.font = muestra.font;
        return texto;
    }
}
