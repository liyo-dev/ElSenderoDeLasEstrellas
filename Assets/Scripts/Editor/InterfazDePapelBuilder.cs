using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pasa al papel los textos de la interfaz hecha en escenas y prefabs: todo texto claro que se
/// dibuja sobre una hoja de papel (los sprites de <see cref="Papeles"/>) pasa a tinta, y los
/// resaltes de selección que lo tiñen (<see cref="MenuTextHighlight"/>, <see cref="UISelectVisual"/>)
/// pasan a colores que se leen sobre papel. Se puede ejecutar las veces que haga falta: lo que
/// ya está en tinta no cambia. Añadir aquí las escenas y prefabs nuevos que usen esos sprites.
/// </summary>
public static class InterfazDePapelBuilder
{
    const string Menu = "El Sendero/UI/Pasar la interfaz al papel";
    const string Informe = "Claude outputs/interfaz-de-papel.md";

    static readonly string[] Papeles =
    {
        "Assets/Art/UI/Menu/confirm_button_pill.png",
        "Assets/Art/UI/Menu/confirm_dialog_glass.png",
        "Assets/Art/UI/Menu/menu_panel_glass.png",
        "Assets/Art/UI/Menu/menu_row_glass.png",
        "Assets/Art/UI/HUD/hud_panel_bg.png",
        "Assets/Art/UI/Misiones/header_pill_bg.png",
        "Assets/Art/UI/Misiones/panel_glass_bg.png",
        "Assets/Art/UI/Misiones/panel_glass_bg_compact.png",
        "Assets/Art/UI/Misiones/button_link_bg.png",
        "Assets/Art/UI/Misiones/progress_chip_bg.png",
        "Assets/Art/UI/Misiones/state_chip_active.png",
        "Assets/Art/UI/Misiones/state_chip_completed.png",
        "Assets/Art/UI/Misiones/state_chip_active_compact.png",
        "Assets/Art/UI/Misiones/state_chip_completed_compact.png",
    };

    static readonly string[] Escenas =
    {
        "Assets/Scenes/Systems/Start.unity",
        "Assets/Scenes/Systems/MainMenu.unity",
    };

    static readonly string[] Prefabs =
    {
        "Assets/Prefabs/UI/ControlRow.prefab",
        "Assets/Prefabs/UI/QuestVisibilityItem_Temp.prefab",
        "Assets/Prefabs/UI/Shop_ItemCard.prefab",
        "Assets/Prefabs/QuestsUI/QuestLogItem.prefab",
        "Assets/Resources/UI/InformeDeVictoria.prefab",
        "Assets/_SHOPS/Shop_Canvas.prefab",
        "Assets/_SHOPS/Shop_Canvas_Decoracion.prefab",
        "Assets/_SHOPS/Shop_Canvas_Renard.prefab",
    };

    const string EstiloSencillo = "Assets/_UI/Estilo_MenuSencillo.asset";

    /// Fondos lisos de escena que pasan a ser una hoja de papel: objeto raíz, ruta dentro de él y hoja.
    static readonly (string raiz, string ruta, string hoja)[] FondosQueSonPapel =
    {
        ("LorePopup", "Canvas/PopupRoot/Background", "Assets/Art/UI/Misiones/panel_glass_bg.png"),
        ("AbilityUnlockPopupUI", "Canvas/Panel/PanelImg (1)", "Assets/Art/UI/Misiones/panel_glass_bg.png"),
        ("TeleportSavePoint", "TeleportScrollView", "Assets/Art/UI/Misiones/panel_glass_bg.png"),
    };

    /// Fondos de los menús a pantalla completa: la noche de la interfaz, casi opaca (raíz, ruta dentro de ella).
    static readonly (string raiz, string ruta)[] FondosDeNoche =
    {
        ("PanelControles", "Background"),
        ("PanelSelectorIdioma", "Background"),
        ("PlayerEquipementMenu", "Controles (menú de Start)/Background"),
        ("QuestMenusCanvas", "QuestMainMenu"),
    };
    static readonly Color Noche = new Color(PaletaUI.Noche.r, PaletaUI.Noche.g, PaletaUI.Noche.b, 0.94f);

    static readonly Color ResalteSobrePapel = new Color(0.72f, 0.36f, 0.05f, 1f);

    [MenuItem(Menu)]
    static void Pasar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Interfaz de papel", "Sal del modo Play antes de pasar la interfaz al papel.");
            return;
        }
        // Desde el buzón no se pregunta: solo se guardan las escenas que este menú cambia.
        if (!OrdenesDelEditor.EnOrden && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var papeles = new HashSet<Sprite>();
        foreach (var ruta in Papeles)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            if (sprite != null) papeles.Add(sprite);
        }

        var informe = new StringBuilder();
        informe.AppendLine("# Interfaz de papel: lo que ha cambiado la última ejecución").AppendLine();
        int total = 0;

        foreach (var ruta in Escenas)
        {
            if (!File.Exists(ruta)) continue;
            var abierta = SceneManager.GetSceneByPath(ruta);
            bool estaba = abierta.IsValid() && abierta.isLoaded;
            var escena = estaba ? abierta : EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            informe.AppendLine("## " + ruta);
            int cambios = 0;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                cambios += Empapelar(raiz, informe);
                cambios += Repasar(raiz, papeles, informe);
            }
            if (cambios > 0)
            {
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
            }
            if (!estaba) EditorSceneManager.CloseScene(escena, true);
            informe.AppendLine();
            total += cambios;
        }

        foreach (var ruta in Prefabs)
        {
            if (!File.Exists(ruta)) continue;
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                informe.AppendLine("## " + ruta);
                int cambios = Empapelar(raiz, informe);
                cambios += Repasar(raiz, papeles, informe);
                if (cambios > 0) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                informe.AppendLine();
                total += cambios;
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        var estilo = AssetDatabase.LoadAssetAtPath<EstiloDeMenuSencillo>(EstiloSencillo);
        if (estilo != null)
        {
            var nuevoTexto = ParaPapel(estilo.colorTexto);
            var nuevoSuave = ParaPapel(estilo.colorTextoSuave);
            var nuevoResalte = ParaPapel(estilo.colorResalte);
            if (nuevoTexto != estilo.colorTexto || nuevoSuave != estilo.colorTextoSuave || nuevoResalte != estilo.colorResalte)
            {
                estilo.colorTexto = nuevoTexto;
                estilo.colorTextoSuave = nuevoSuave;
                estilo.colorResalte = nuevoResalte;
                EditorUtility.SetDirty(estilo);
                informe.AppendLine("## " + EstiloSencillo).AppendLine("- colores de texto").AppendLine();
                total++;
            }
        }

        AssetDatabase.SaveAssets();
        informe.AppendLine("Total: " + total + " cambios.");
        File.WriteAllText(Informe, informe.ToString());
        Debug.Log("[Interfaz de papel] " + total + " cambios. Detalle en " + Informe);
    }

    /// Pasa a tinta los textos claros que caen sobre papel y los resaltes que los tiñen.
    static int Repasar(GameObject raiz, HashSet<Sprite> papeles, StringBuilder informe)
    {
        int cambios = 0;
        foreach (var texto in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!SobrePapel(texto.transform, papeles)) continue;
            var nuevo = ParaPapel(texto.color);
            if (nuevo == texto.color) continue;
            texto.color = nuevo;
            Marcar(texto);
            informe.AppendLine("- texto `" + Ruta(texto.transform) + "`");
            cambios++;
        }
        foreach (var resalte in raiz.GetComponentsInChildren<MenuTextHighlight>(true))
        {
            var texto = TextoQueTine(resalte.targetGraphic, resalte);
            if (texto == null || !SobrePapel(texto.transform, papeles)) continue;
            var normal = ParaPapel(resalte.normalColor);
            if (normal == resalte.normalColor && resalte.starColor == ResalteSobrePapel) continue;
            resalte.normalColor = normal;
            resalte.starColor = ResalteSobrePapel;
            Marcar(resalte);
            informe.AppendLine("- resalte `" + Ruta(resalte.transform) + "`");
            cambios++;
        }
        foreach (var resalte in raiz.GetComponentsInChildren<UISelectVisual>(true))
        {
            var texto = resalte.targetGraphic as TMP_Text;
            if (texto == null || !SobrePapel(texto.transform, papeles)) continue;
            var normal = ParaPapel(resalte.normalColor);
            if (normal == resalte.normalColor && resalte.highlightColor == ResalteSobrePapel) continue;
            resalte.normalColor = normal;
            resalte.highlightColor = ResalteSobrePapel;
            Marcar(resalte);
            informe.AppendLine("- resalte `" + Ruta(resalte.transform) + "`");
            cambios++;
        }
        return cambios;
    }

    /// Pone la hoja de papel en los fondos lisos de <see cref="FondosQueSonPapel"/> que cuelgan de esta raíz.
    static int Empapelar(GameObject raiz, StringBuilder informe)
    {
        int cambios = 0;
        foreach (var (nombre, ruta, hoja) in FondosQueSonPapel)
        {
            if (raiz.name != nombre) continue;
            var objeto = string.IsNullOrEmpty(ruta) ? raiz.transform : raiz.transform.Find(ruta);
            var imagen = objeto != null ? objeto.GetComponent<Image>() : null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(hoja);
            if (imagen == null || sprite == null || imagen.sprite == sprite) continue;
            imagen.sprite = sprite;
            imagen.type = Image.Type.Sliced;
            imagen.color = Color.white;
            imagen.enabled = true;
            Marcar(imagen);
            informe.AppendLine("- papel en `" + Ruta(objeto) + "`");
            cambios++;
        }
        foreach (var (nombre, ruta) in FondosDeNoche)
        {
            if (raiz.name != nombre) continue;
            var objeto = raiz.transform.Find(ruta);
            var imagen = objeto != null ? objeto.GetComponent<Image>() : null;
            if (imagen == null || (imagen.sprite == null && imagen.color == Noche)) continue;
            imagen.sprite = null;
            imagen.color = Noche;
            Marcar(imagen);
            informe.AppendLine("- noche en `" + Ruta(objeto) + "`");
            cambios++;
        }
        return cambios;
    }

    static TMP_Text TextoQueTine(Graphic grafico, Component resalte)
    {
        if (grafico != null) return grafico as TMP_Text;
        return resalte.GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// Lo primero que hay pintado detrás del objeto, subiendo por sus padres: una imagen del propio
    /// padre o un hermano anterior que lo ocupa entero. Si es una hoja de papel, está sobre papel;
    /// si es otra cosa opaca, no.
    /// </summary>
    static bool SobrePapel(Transform objeto, HashSet<Sprite> papeles)
    {
        Transform rama = objeto;
        for (Transform padre = objeto.parent; padre != null; rama = padre, padre = padre.parent)
        {
            for (int i = rama.GetSiblingIndex() - 1; i >= 0; i--)
            {
                var hermano = padre.GetChild(i);
                var imagen = hermano.GetComponent<Image>();
                if (imagen == null || !imagen.enabled || !OcupaTodo(hermano as RectTransform)) continue;
                if (imagen.sprite != null && papeles.Contains(imagen.sprite)) return true;
                if (imagen.color.a > 0.5f) return false;
            }
            var propia = padre.GetComponent<Image>();
            if (propia != null && propia.enabled)
            {
                if (propia.sprite != null && papeles.Contains(propia.sprite)) return true;
                if (propia.color.a > 0.5f) return false;
            }
            if (padre.GetComponent<Canvas>() != null) break;
        }
        return false;
    }

    static bool OcupaTodo(RectTransform rt)
    {
        return rt != null && rt.anchorMin.sqrMagnitude < 0.0001f && (rt.anchorMax - Vector2.one).sqrMagnitude < 0.0001f;
    }

    /// El color con el que se lee sobre papel un texto que era claro. Los oscuros se quedan como están.
    static Color ParaPapel(Color c)
    {
        float luz = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        if (luz < 0.55f) return c;
        Color.RGBToHSV(c, out float tono, out float saturacion, out _);
        Color nuevo;
        if (saturacion > 0.45f && tono > 0.08f && tono < 0.2f) nuevo = PaletaUI.OroSobrePapel;
        else if (luz > 0.9f || saturacion < 0.08f) nuevo = PaletaUI.TintaDeTexto;
        else nuevo = PaletaUI.Tinta;
        nuevo.a = c.a;
        return nuevo;
    }

    static void Marcar(Object objeto)
    {
        EditorUtility.SetDirty(objeto);
        if (PrefabUtility.IsPartOfPrefabInstance(objeto)) PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
    }

    static string Ruta(Transform t)
    {
        var partes = new List<string>();
        for (; t != null; t = t.parent) partes.Insert(0, t.name);
        return string.Join("/", partes);
    }
}
