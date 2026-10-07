using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Monta el pie del grimorio y conecta la ocultación de la barra del menú.</summary>
public static class GrimorioPieDePaginaBuilder
{
    private const string StartPath = "Assets/Scenes/Systems/Start.unity";

    [MenuItem("El Sendero/UI/Grimorio · pie de página propio (INC-668)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("El pie del grimorio se monta fuera de Play."); return; }
        var log = new StringBuilder("=== Grimorio · pie de página propio (INC-668) ===\n");
        var avisos = new List<string>();
        Montar(log, avisos);
        foreach (string aviso in avisos) log.AppendLine("• " + aviso);
        if (avisos.Count == 0) Debug.Log(log.ToString());
        else Debug.LogWarning(log.ToString());
    }

    private static void Montar(StringBuilder log, List<string> avisos)
    {
        var scene = SceneManager.GetSceneByPath(StartPath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Additive);
        PlayerEquipmentMenuController menu = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            menu = root.GetComponentInChildren<PlayerEquipmentMenuController>(true);
            if (menu != null) break;
        }
        if (menu == null) { avisos.Add("Start no contiene PlayerEquipmentMenuController."); return; }
        var menuSo = new SerializedObject(menu);
        var libro = menuSo.FindProperty("grimorioLibro").objectReferenceValue as GrimorioLibroUI;
        if (libro == null || libro.gameObject.scene != scene)
        { avisos.Add("El menú no tiene un GrimorioLibroUI de Start asignado."); return; }

        var existente = libro.transform.Find("PieDePagina");
        RectTransform pie;
        if (existente == null)
        {
            var go = new GameObject("PieDePagina", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Crear pie del grimorio");
            go.transform.SetParent(libro.transform, false);
            pie = (RectTransform)go.transform;
        }
        else
        {
            pie = existente as RectTransform;
            if (pie == null) { avisos.Add("PieDePagina no tiene RectTransform."); return; }
        }
        AjustarRect(pie, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(1560, 64));
        pie.SetAsLastSibling();
        pie.gameObject.SetActive(true);
        var fondo = pie.GetComponent<Image>();
        if (fondo == null) fondo = Undo.AddComponent<Image>(pie.gameObject);
        Undo.RecordObject(fondo, "Ajustar fondo del grimorio");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(TarjetasDeAyudaBuilder.FondoGuid));
        bool tieneBordes = sprite != null && sprite.border != Vector4.zero;
        fondo.sprite = tieneBordes ? sprite : null;
        fondo.type = tieneBordes ? Image.Type.Sliced : Image.Type.Simple;
        fondo.color = tieneBordes ? Color.white : new Color(.04f, .03f, .08f, .85f);
        fondo.raycastTarget = false;
        EditorUtility.SetDirty(fondo);
        if (sprite == null) avisos.Add("No se encuentra el sprite de las tarjetas; el pie usa un fondo de color.");

        AjustarTexto(libro.transform, pie, "Contador", new Vector2(0, .5f), new Vector2(32, 0),
            new Vector2(620, 48), TextAlignmentOptions.Left, 24, true, avisos);
        AjustarTexto(libro.transform, pie, "Pistas", new Vector2(1, .5f), new Vector2(-32, 0),
            new Vector2(880, 48), TextAlignmentOptions.Right, 26, false, avisos);

        var barra = TarjetasDeAyudaBuilder.Buscar(menu.transform, "PanelInfo");
        if (barra == null) avisos.Add("No se encuentra PanelInfo en el menú de Start.");
        else
        {
            var grupo = barra.GetComponent<CanvasGroup>();
            if (grupo == null) grupo = Undo.AddComponent<CanvasGroup>(barra.gameObject);
            var libroSo = new SerializedObject(libro);
            var paneles = libroSo.FindProperty("ocultarMientrasEstaAbierto");
            bool asignado = false;
            for (int i = 0; i < paneles.arraySize; i++)
                if (paneles.GetArrayElementAtIndex(i).objectReferenceValue == grupo) asignado = true;
            if (!asignado)
            {
                int indice = paneles.arraySize;
                paneles.arraySize++;
                paneles.GetArrayElementAtIndex(indice).objectReferenceValue = grupo;
            }
            libroSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(grupo);
            log.AppendLine("PanelInfo se oculta mientras el grimorio está abierto.");
        }
        EditorUtility.SetDirty(libro);
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) log.AppendLine("PieDePagina ajustado: contador a la izquierda y pistas a la derecha. Start guardada.");
        else avisos.Add("No se puede guardar Start.");
        AssetDatabase.SaveAssets();
    }

    private static void AjustarTexto(Transform raiz, RectTransform pie, string nombre, Vector2 ancla,
        Vector2 posicion, Vector2 tamano, TextAlignmentOptions alineacion, float letra, bool elipsis, List<string> avisos)
    {
        var objeto = TarjetasDeAyudaBuilder.Buscar(raiz, nombre);
        var texto = objeto != null ? objeto.GetComponent<TextMeshProUGUI>() : null;
        if (texto == null) { avisos.Add($"No se encuentra {nombre} con texto TMP en el grimorio."); return; }
        Undo.SetTransformParent(objeto, pie, "Mover texto al pie del grimorio");
        objeto.SetParent(pie, false);
        AjustarRect(texto.rectTransform, ancla, ancla, posicion, tamano);
        Undo.RecordObject(texto, "Ajustar texto del pie del grimorio");
        texto.alignment = alineacion;
        texto.fontSize = letra;
        texto.enableAutoSizing = false;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        if (elipsis) texto.overflowMode = TextOverflowModes.Ellipsis;
        texto.raycastTarget = false;
        EditorUtility.SetDirty(texto);
    }

    private static void AjustarRect(RectTransform rect, Vector2 ancla, Vector2 pivote, Vector2 posicion, Vector2 tamano)
    {
        Undo.RecordObject(rect, "Ajustar pie del grimorio");
        rect.anchorMin = rect.anchorMax = ancla;
        rect.pivot = pivote;
        rect.anchoredPosition3D = new Vector3(posicion.x, posicion.y, 0);
        rect.sizeDelta = tamano;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(rect);
    }
}
