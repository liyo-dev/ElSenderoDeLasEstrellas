#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class MainMenuBotonesClasicosBuilder
{
    const string Carpeta = "Assets/Art/UI/Menu/";
    static readonly Color Crema = new Color(1f, 0.965f, 0.91f, 1f);
    static readonly Color Oro = new Color(1f, 0.87f, 0.47f, 1f);

    [MenuItem("El Sendero/MainMenu/Botones: estilo clásico")]
    public static void Construir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cinta = Importar("menu_cursor_cinta.png");
        var estrella = Importar("menu_cursor_estrella.png");
        var velo = Importar("menu_velo_lateral.png");
        var escena = EditorSceneManager.OpenScene("Assets/Scenes/Systems/MainMenu.unity");
        RectTransform panel = null;
        MainMenuController controlador = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var rt in raiz.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == "ButtonPanel") panel = rt;
            if (!controlador) controlador = raiz.GetComponentInChildren<MainMenuController>(true);
        }
        if (!panel || !controlador) throw new InvalidOperationException("Falta ButtonPanel o MainMenuController.");
        var botones = panel.GetComponentsInChildren<Button>(true);
        if (botones.Length == 0) throw new InvalidOperationException("ButtonPanel no contiene botones.");
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        var scaler = canvas.GetComponent<CanvasScaler>();
        var referencia = scaler ? scaler.referenceResolution : new Vector2(1920, 1080);
        float sx = referencia.x / 1920f, sy = referencia.y / 1080f;
        var primero = botones[0].GetComponentInChildren<TextMeshProUGUI>(true);
        if (!primero || !primero.font) throw new InvalidOperationException("Falta el texto o su fuente.");
        float tamano = primero.fontSize / sy;
        if (tamano < 28f || tamano > 36f) tamano = 32f;
        string rutaMaterial = Carpeta + "MenuPrincipal_TextoContorno.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
        if (!material)
        {
            material = new Material(primero.font.material);
            AssetDatabase.CreateAsset(material, rutaMaterial);
        }
        Undo.RecordObject(material, "Configurar contorno del menú");
        material.EnableKeyword("OUTLINE_ON");
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_OutlineColor", new Color(0.13f, 0.09f, 0.26f, 1f));
        material.SetFloat("_OutlineWidth", 0.18f);
        material.SetColor("_UnderlayColor", new Color(0.04f, 0.02f, 0.12f, 0.65f));
        material.SetFloat("_UnderlayOffsetX", 0.6f);
        material.SetFloat("_UnderlayOffsetY", -0.8f);
        material.SetFloat("_UnderlaySoftness", 0.5f);
        EditorUtility.SetDirty(material);
        int eliminados = 0;
        foreach (var boton in botones)
        {
            foreach (var hijo in boton.GetComponentsInChildren<Transform>(true))
                if (hijo && hijo != boton.transform && hijo.name == "RowGlassBG")
                { Undo.DestroyObjectImmediate(hijo.gameObject); eliminados++; }
            Undo.RecordObject(boton, "Configurar botón clásico");
            boton.transition = Selectable.Transition.None;
            Transparentar(boton.targetGraphic);
            Transparentar(boton.GetComponent<Image>());
            var fila = Obtener<LayoutElement>(boton.gameObject);
            Undo.RecordObject(fila, "Configurar alto de fila");
            fila.minHeight = fila.preferredHeight = 48f * sy;
            fila.flexibleHeight = 0f;
            foreach (var texto in boton.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Undo.RecordObject(texto, "Configurar texto clásico");
                texto.alignment = TextAlignmentOptions.MidlineLeft;
                texto.color = Crema;
                texto.enableAutoSizing = false;
                texto.fontSize = tamano * sy;
                texto.fontSharedMaterial = material;
                texto.margin = new Vector4(56f * sx, 0f, 0f, 0f);
                var highlight = Obtener<MenuTextHighlight>(texto.gameObject);
                Undo.RecordObject(highlight, "Configurar selección dorada");
                highlight.targetGraphic = texto;
                highlight.selectionOwner = boton.gameObject;
                highlight.normalColor = Crema;
                highlight.starColor = Oro;
            }
        }
        // Las filas pueden vivir en un contenedor intermedio dentro del panel.
        Transform contenedor = botones[0].transform.parent;
        foreach (var boton in botones)
            if (boton.transform.parent != contenedor)
                throw new InvalidOperationException("Las filas no comparten contenedor de layout.");
        var layout = Obtener<VerticalLayoutGroup>(contenedor.gameObject);
        Undo.RecordObject(layout, "Configurar columna clásica");
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 16f * sy;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        var grupo = new SerializedObject(controlador).FindProperty("rootGroup").objectReferenceValue as CanvasGroup;
        var rtGrupo = grupo ? grupo.transform as RectTransform : null;
        bool pantallaCompleta = rtGrupo && rtGrupo.anchorMin == Vector2.zero && rtGrupo.anchorMax == Vector2.one
            && rtGrupo.offsetMin == Vector2.zero && rtGrupo.offsetMax == Vector2.zero;
        var padreVelo = pantallaCompleta ? rtGrupo : (RectTransform)canvas.transform;
        var imagenVelo = Imagen("VeloLateral", padreVelo, velo);
        Obtener<LayoutElement>(imagenVelo.gameObject).ignoreLayout = true;
        var rtVelo = imagenVelo.rectTransform;
        Undo.RecordObject(rtVelo, "Colocar velo lateral");
        rtVelo.anchorMin = Vector2.zero;
        rtVelo.anchorMax = new Vector2(0.42f, 1f);
        rtVelo.offsetMin = rtVelo.offsetMax = Vector2.zero;
        rtVelo.SetAsFirstSibling();
        var cursor = Rectangulo("CursorSeleccion", panel);
        cursor.SetAsFirstSibling();
        Obtener<LayoutElement>(cursor.gameObject).ignoreLayout = true;
        var cg = Obtener<CanvasGroup>(cursor.gameObject);
        Undo.RecordObject(cg, "Configurar cursor");
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        Undo.RecordObject(cursor, "Colocar cursor");
        cursor.anchorMin = cursor.anchorMax = new Vector2(0f, 1f);
        cursor.pivot = new Vector2(0f, 0.5f);
        cursor.sizeDelta = new Vector2(560f * sx, 50f * sy);
        // El borde de la fila se convierte al espacio del panel para respetar el contenedor intermedio.
        Canvas.ForceUpdateCanvases();
        var rectFila = (RectTransform)botones[0].transform;
        var borde = panel.InverseTransformPoint(rectFila.TransformPoint(new Vector3(rectFila.rect.xMin, rectFila.rect.center.y)));
        cursor.anchoredPosition = new Vector2(borde.x - panel.rect.xMin, borde.y - panel.rect.yMax);
        ConfigurarImagen(Imagen("Cinta", cursor, cinta).rectTransform, 36f * sx, 560f * sx, 50f * sy, new Vector2(0f, 0.5f));
        var rtEstrella = Imagen("Estrella", cursor, estrella).rectTransform;
        ConfigurarImagen(rtEstrella, 30f * sx, 38f * sx, 38f * sy, new Vector2(0.5f, 0.5f));
        var navegador = panel.GetComponentInParent<MenuNavigator>(true);
        if (!navegador) navegador = Obtener<MenuNavigator>(panel.gameObject);
        var serializado = new SerializedObject(navegador);
        serializado.FindProperty("cursorSeleccion").objectReferenceValue = cursor;
        serializado.FindProperty("destelloDelCursor").objectReferenceValue = rtEstrella;
        serializado.FindProperty("duracionCursor").floatValue = 0.12f;
        serializado.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        string resumen = $"{botones.Length} botones configurados; {eliminados} fondos de cristal retirados.\nContorno, sombra y cursor dorado preparados.\nFilas en {contenedor.name}.\n" +
            (pantallaCompleta ? "Velo dentro del rootGroup." : "Velo como primer hijo del Canvas raíz: rootGroup no ocupa toda la pantalla; el velo no hereda su fundido.");
        Debug.Log(resumen);
        EditorUtility.DisplayDialog("Menú principal clásico", resumen, "Aceptar");
    }

    static Sprite Importar(string nombre)
    {
        string ruta = Carpeta + nombre;
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (!importer) throw new InvalidOperationException("No existe " + ruta);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
            || importer.mipmapEnabled || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static T Obtener<T>(GameObject go) where T : Component
    { var componente = go.GetComponent<T>(); return componente ? componente : Undo.AddComponent<T>(go); }

    static RectTransform Rectangulo(string nombre, Transform padre)
    {
        var existente = padre.Find(nombre) as RectTransform;
        if (existente) return existente;
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        Undo.SetTransformParent(go.transform, padre, "Colocar " + nombre);
        go.layer = padre.gameObject.layer;
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;
        return (RectTransform)go.transform;
    }

    static Image Imagen(string nombre, Transform padre, Sprite sprite)
    {
        var imagen = Obtener<Image>(Rectangulo(nombre, padre).gameObject);
        Undo.RecordObject(imagen, "Configurar " + nombre);
        imagen.sprite = sprite; imagen.color = Color.white;
        imagen.type = Image.Type.Simple; imagen.raycastTarget = false;
        return imagen;
    }

    static void ConfigurarImagen(RectTransform rt, float x, float ancho, float alto, Vector2 pivote)
    {
        Undo.RecordObject(rt, "Colocar imagen del cursor");
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = pivote; rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(ancho, alto);
    }

    static void Transparentar(Graphic graphic)
    {
        if (!graphic) return;
        Undo.RecordObject(graphic, "Quitar fondo del botón");
        var color = graphic.color; color.a = 0f;
        graphic.color = color; graphic.raycastTarget = true;
    }
}
#endif
