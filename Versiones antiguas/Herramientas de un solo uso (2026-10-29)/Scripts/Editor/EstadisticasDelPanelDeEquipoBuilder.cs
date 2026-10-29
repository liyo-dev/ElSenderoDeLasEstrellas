using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta en Start las estadísticas reales del panel de arriba a la derecha del menú de Equipo:
/// la fila con Ataque y Defensa (PanelDeEstadisticasUI) y los textos de Vida y Magia legibles
/// sobre las barras. Quita el cuadro flotante que repetía lo mismo. Se puede repetir sin efectos.
/// </summary>
public static class EstadisticasDelPanelDeEquipoBuilder
{
    const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    static readonly Color ColorContorno = new Color(43f / 255f, 30f / 255f, 22f / 255f, 1f);

    [MenuItem("El Sendero/UI/Equipo · estadísticas en el panel de arriba")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Las estadísticas del panel de Equipo se montan fuera de Play."); return; }

        var avisos = new List<string>();
        var escena = SceneManager.GetSceneByPath(RutaStart);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaStart, OpenSceneMode.Additive);

        PlayerEquipmentMenuController menu = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            menu = raiz.GetComponentInChildren<PlayerEquipmentMenuController>(true);
            if (menu != null) break;
        }
        if (menu == null) { Debug.LogWarning("Start no contiene PlayerEquipmentMenuController."); return; }

        // Fuera el cuadro flotante: duplicaba el panel de arriba.
        int quitados = 0;
        foreach (var viejo in menu.GetComponentsInChildren<PanelDeEstadisticasUI>(true))
        {
            if (viejo.gameObject.name != "PanelEstadisticas") continue;
            Object.DestroyImmediate(viejo.gameObject);
            quitados++;
        }

        var info = Buscar(menu.transform, "Panel INFO");
        if (info == null) { Debug.LogWarning("No se encuentra «Panel INFO» en el menú de Equipo."); return; }
        var fila = info.Find("StatsRow") as RectTransform;
        if (fila == null) { Debug.LogWarning("«Panel INFO» no tiene StatsRow."); return; }

        // «Ataque especial» no es una estadística: se retira.
        var sobrante = Buscar(info, "StatVelocidad");
        if (sobrante != null) Object.DestroyImmediate(sobrante.gameObject);

        var ataque = PrepararCelda(Buscar(info, "StatAtaque"), fila, 0f, 0.5f, avisos, "StatAtaque");
        var defensa = PrepararCelda(Buscar(info, "StatDefensa"), fila, 0.5f, 1f, avisos, "StatDefensa");

        var panel = fila.GetComponent<PanelDeEstadisticasUI>();
        if (panel == null) panel = fila.gameObject.AddComponent<PanelDeEstadisticasUI>();
        var so = new SerializedObject(panel);
        var filas = so.FindProperty("filas");
        filas.arraySize = 2;
        PonerFila(filas.GetArrayElementAtIndex(0), ataque, TipoDeEstadistica.Ataque);
        PonerFila(filas.GetArrayElementAtIndex(1), defensa, TipoDeEstadistica.Defensa);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);

        // Vida y Magia: blancos con contorno oscuro, alineados a la derecha de la barra y sin
        // partir línea, para que quepa el «(+N)» del equipo.
        var menuSo = new SerializedObject(menu);
        var material = MaterialConContorno(menuSo.FindProperty("hpText").objectReferenceValue as TextMeshProUGUI, avisos);
        foreach (var campo in new[] { "hpText", "mpText" })
        {
            var texto = menuSo.FindProperty(campo).objectReferenceValue as TextMeshProUGUI;
            if (texto == null) { avisos.Add($"El menú no tiene {campo}."); continue; }
            if (material != null) texto.fontSharedMaterial = material;
            texto.color = Color.white;
            texto.fontStyle = FontStyles.Bold;
            texto.horizontalAlignment = HorizontalAlignmentOptions.Right;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.richText = true;
            var rt = texto.rectTransform;
            var barra = Buscar(info, campo == "hpText" ? "HPBarFill" : "MPBarFill") as RectTransform;
            if (barra != null && rt.parent != null)
            {
                // El texto acaba 24 px antes del final de la barra y ocupa casi todo su ancho.
                var esquinas = new Vector3[4];
                barra.GetWorldCorners(esquinas);
                var padre = (RectTransform)rt.parent;
                Vector2 izq = padre.InverseTransformPoint(esquinas[0]);
                Vector2 der = padre.InverseTransformPoint(esquinas[2]);
                float ancho = (der.x - izq.x) - 48f;
                var pivoteAntes = rt.pivot;
                rt.pivot = new Vector2(1f, pivoteAntes.y);
                var posicion = rt.localPosition;
                posicion.x = der.x - 24f;
                rt.localPosition = posicion;
                rt.sizeDelta = new Vector2(ancho, rt.sizeDelta.y);
            }
            EditorUtility.SetDirty(texto);
        }

        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        AssetDatabase.SaveAssets();
        if (!guardada) avisos.Add("No se ha podido guardar Start.");

        string resultado = $"Equipo: fila Ataque/Defensa montada, cuadro flotante retirado ({quitados}), " +
            "Vida y Magia con contorno. " + (guardada ? "Start guardada." : "");
        if (avisos.Count == 0) Debug.Log(resultado);
        else Debug.LogWarning(resultado + "\n" + string.Join("\n", avisos));
    }

    static Text PrepararCelda(Transform t, RectTransform fila, float desde, float hasta, List<string> avisos, string nombre)
    {
        var texto = t != null ? t.GetComponent<Text>() : null;
        if (texto == null) { avisos.Add($"No se encuentra {nombre} con Text."); return null; }
        var rt = texto.rectTransform;
        rt.SetParent(fila, false);
        rt.anchorMin = new Vector2(desde, 0f);
        rt.anchorMax = new Vector2(hasta, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.supportRichText = true;
        texto.raycastTarget = false;
        texto.text = TextoDeEstadisticas.Nombre(nombre == "StatAtaque" ? TipoDeEstadistica.Ataque : TipoDeEstadistica.Defensa) + "   —";
        EditorUtility.SetDirty(texto);
        return texto;
    }

    static void PonerFila(SerializedProperty fila, Graphic texto, TipoDeEstadistica tipo)
    {
        fila.FindPropertyRelative("texto").objectReferenceValue = texto;
        fila.FindPropertyRelative("tipo").enumValueIndex = (int)tipo;
    }

    /// Variante de material de la fuente con contorno y sombra oscuros, junto a la fuente. No toca
    /// el material por defecto de la fuente (lo usan todos los textos del juego).
    static Material MaterialConContorno(TextMeshProUGUI muestra, List<string> avisos)
    {
        if (muestra == null || muestra.font == null) { avisos.Add("Sin fuente para el contorno de Vida/Magia."); return null; }
        string carpeta = Path.GetDirectoryName(AssetDatabase.GetAssetPath(muestra.font))?.Replace('\\', '/');
        if (string.IsNullOrEmpty(carpeta) || !carpeta.StartsWith("Assets")) carpeta = "Assets/Art/UI";
        string ruta = $"{carpeta}/{muestra.font.name} - Contorno oscuro.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = material == null;
        if (nuevo) material = new Material(muestra.font.material);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, ColorContorno);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
        if (nuevo) AssetDatabase.CreateAsset(material, ruta);
        else EditorUtility.SetDirty(material);
        return material;
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        if (raiz == null) return null;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
