using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Deja los menús como la maqueta «Menú de Start del Sendero: propuesta nueva» (idempotente):
/// crea la letra de los títulos (Baloo 2) como fuente TMP, el estilo de los menús de papel con
/// sus dos letras y, en el menú principal, se lo da a la portada, a Ajustes y al selector de
/// idioma y quita de estos dos los paneles viejos de la escena (ahora se montan por código).
/// El menú de Start, que se monta por código dentro de Start, lo lee de
/// <see cref="Sendero.UI.ArteDelMenuDeStart"/> (en Resources), con los iconos de la maqueta.
/// </summary>
public static class MenusDePapelBuilder
{
    const string Menu = "El Sendero/Interfaz/Menús de papel como la maqueta";
    const string RutaTtfTitulos = "Assets/Plugins/Fonts/Baloo2-ExtraBold.ttf";
    const string RutaFuenteTitulos = "Assets/Plugins/Fonts/Baloo2-ExtraBold SDF.asset";
    const string RutaFuenteTexto = "Assets/Plugins/Fonts/Nunito-ExtraBold SDF.asset";
    const string RutaEstilo = "Assets/_UI/Estilo_MenusDePapel.asset";
    const string EscenaMenu = "Assets/Scenes/Systems/MainMenu.unity";
    const string RutaArteDelMenuDeStart = "Assets/Resources/" + Sendero.UI.ArteDelMenuDeStart.RutaEnResources + ".asset";
    const string CarpetaIconos = "Assets/Art/UI/MenuDeStart/";
    const string Caracteres = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                              "¡¿ÁÉÍÓÚÜÑáéíóúüñ·…«»–—’‘“”×ºª";

    [MenuItem(Menu)]
    public static void Montar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Menús de papel", "Sal de Play antes de montar los menús."); return; }
        var informe = new StringBuilder();
        var estilo = Estilo();
        informe.AppendLine("Estilo: " + AssetDatabase.GetAssetPath(estilo));

        var escena = EditorSceneManager.GetSceneByPath(EscenaMenu);
        bool abierta = escena.IsValid() && escena.isLoaded;
        if (!abierta) escena = EditorSceneManager.OpenScene(EscenaMenu, OpenSceneMode.Additive);
        if (MontarMenuPrincipal(escena, estilo, informe)) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
        if (!abierta) EditorSceneManager.CloseScene(escena, true);
        MontarArteDelMenuDeStart(estilo, informe);
        MontarTeclaDeMenuX(informe);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Menús de papel", informe.ToString());
    }

    /// <summary>El estilo de los menús de papel (lo crea, con sus letras, si no existe).</summary>
    public static EstiloDeMenuSencillo Estilo()
    {
        var estilo = AssetDatabase.LoadAssetAtPath<EstiloDeMenuSencillo>(RutaEstilo);
        if (estilo == null)
        {
            estilo = ScriptableObject.CreateInstance<EstiloDeMenuSencillo>();
            AssetDatabase.CreateAsset(estilo, RutaEstilo);
        }
        var titulos = FuenteDeTitulos();
        var texto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuenteTexto);
        if (estilo.fuenteTitulo != titulos || estilo.fuenteTexto != texto)
        {
            estilo.fuenteTitulo = titulos;
            estilo.fuenteTexto = texto;
            estilo.colorTexto = PaletaUI.TintaDeTexto;
            estilo.colorTextoSuave = new Color32(0x6B, 0x58, 0x44, 0xFF);
            estilo.colorResalte = PaletaUI.OroSobrePapel;
            EditorUtility.SetDirty(estilo);
        }
        return estilo;
    }

    /// Baloo 2 ExtraBold como fuente TMP dinámica (con Nunito detrás para lo que le falte).
    static TMP_FontAsset FuenteDeTitulos()
    {
        var fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuenteTitulos);
        if (fuente != null) return fuente;
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(RutaTtfTitulos);
        if (ttf == null) { Debug.LogWarning("[MenusDePapel] Falta " + RutaTtfTitulos); return null; }
        fuente = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        fuente.name = "Baloo2-ExtraBold SDF";
        AssetDatabase.CreateAsset(fuente, RutaFuenteTitulos);
        fuente.atlasTextures[0].name = "Baloo2-ExtraBold Atlas";
        AssetDatabase.AddObjectToAsset(fuente.atlasTextures[0], fuente);
        fuente.material.name = "Baloo2-ExtraBold Material";
        AssetDatabase.AddObjectToAsset(fuente.material, fuente);
        fuente.TryAddCharacters(Caracteres);
        var respaldo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuenteTexto);
        if (respaldo != null) fuente.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { respaldo };
        EditorUtility.SetDirty(fuente);
        AssetDatabase.SaveAssets();
        return fuente;
    }

    static bool MontarMenuPrincipal(UnityEngine.SceneManagement.Scene escena, EstiloDeMenuSencillo estilo, StringBuilder informe)
    {
        bool cambio = false;
        Sprite logo = null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var portada in raiz.GetComponentsInChildren<EstiloDePapelDelMenu>(true))
            {
                cambio |= Poner(portada, "estilo", estilo);
                var so = new SerializedObject(portada);
                var titulo = so.FindProperty("titulo").objectReferenceValue as RectTransform;
                var img = titulo != null ? titulo.GetComponent<UnityEngine.UI.Image>() : null;
                if (img != null) logo = img.sprite;
                cambio |= PonerRect(portada, "zonaVisibleDelTitulo", ZonaVisible(logo));
                foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
                    if (t.name == "VersionLabel" && t.gameObject.scene == escena) cambio |= Poner(portada, "version", t);
            }
        }
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var ajustes in raiz.GetComponentsInChildren<SettingsMenuController>(true))
            {
                cambio |= Poner(ajustes, "estilo", estilo);
                cambio |= QuitarHijos(Raiz(ajustes), informe);
            }
            foreach (var idioma in raiz.GetComponentsInChildren<LanguageSelectPanel>(true))
            {
                cambio |= Poner(idioma, "estilo", estilo);
                if (logo != null) cambio |= Poner(idioma, "logo", logo);
                cambio |= PonerRect(idioma, "zonaVisibleDelTitulo", ZonaVisible(logo));
                cambio |= QuitarHijos(Raiz(idioma), informe);
            }
        }
        informe.AppendLine("Menú principal: " + (cambio ? "montado" : "ya estaba"));
        return cambio;
    }

    /// Los iconos de la maqueta como sprites y el asset que los reúne para el menú de Start.
    static void MontarArteDelMenuDeStart(EstiloDeMenuSencillo estilo, StringBuilder informe)
    {
        var arte = AssetDatabase.LoadAssetAtPath<Sendero.UI.ArteDelMenuDeStart>(RutaArteDelMenuDeStart);
        if (arte == null)
        {
            arte = ScriptableObject.CreateInstance<Sendero.UI.ArteDelMenuDeStart>();
            AssetDatabase.CreateAsset(arte, RutaArteDelMenuDeStart);
        }
        arte.estilo = estilo;
        arte.pestanas = new[] { Icono("pestana_inventario"), Icono("pestana_magia"), Icono("pestana_equipo"), Icono("pestana_sintonia") };
        arte.moneda = Icono("moneda");
        arte.corazon = Icono("corazon");
        arte.gota = Icono("gota");
        arte.huecos = new[] { Icono("hueco_cabeza"), Icono("hueco_cuerpo"), Icono("hueco_capa"), Icono("hueco_accesorio") };
        var viento = Icono("rasgo_viento");
        var hoja = Icono("rasgo_hoja");
        arte.rasgos = new[]
        {
            new Sendero.UI.ArteDelMenuDeStart.IconoDeRasgo { rasgo = RasgoDePrenda.Planear, icono = viento },
            new Sendero.UI.ArteDelMenuDeStart.IconoDeRasgo { rasgo = RasgoDePrenda.VientoMasBarato, icono = viento },
            new Sendero.UI.ArteDelMenuDeStart.IconoDeRasgo { rasgo = RasgoDePrenda.ReboteExtra, icono = hoja },
            new Sendero.UI.ArteDelMenuDeStart.IconoDeRasgo { rasgo = RasgoDePrenda.ContraataqueMasAmplio, icono = hoja },
        };
        arte.rasgoPorDefecto = hoja;
        EditorUtility.SetDirty(arte);
        informe.AppendLine("Menú de Start: " + RutaArteDelMenuDeStart);
    }

    /// La tecla C del teclado para la X de los menús (InputGlyphNames.MenuX): importada como las
    /// demás teclas y puesta en el juego de iconos de teclado.
    static void MontarTeclaDeMenuX(StringBuilder informe)
    {
        const string carpeta = "Assets/Art/UI/InputGlyphs/KeyboardMouse/";
        const string ruta = carpeta + "menuX.png";
        if (AssetImporter.GetAtPath(ruta) is TextureImporter imp && AssetImporter.GetAtPath(carpeta + "south.png") is TextureImporter modelo
            && imp.textureType != TextureImporterType.Sprite)
        {
            var ajustes = new TextureImporterSettings();
            modelo.ReadTextureSettings(ajustes);
            imp.SetTextureSettings(ajustes);
            imp.textureCompression = modelo.textureCompression;
            imp.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        var set = AssetDatabase.LoadAssetAtPath<Core.InputGlyphs.InputGlyphFamilySpriteSet>("Assets/_UI/InputGlyphFamilySpriteSet_KeyboardMouse.asset");
        if (sprite == null || set == null) { informe.AppendLine("Falta la tecla C o el juego de iconos de teclado"); return; }
        if (Poner(set, "menuX", sprite)) informe.AppendLine("Tecla C puesta en los iconos de teclado");
    }

    /// El PNG de la carpeta de iconos, importado como sprite.
    static Sprite Icono(string nombre)
    {
        string ruta = CarpetaIconos + nombre + ".png";
        if (AssetImporter.GetAtPath(ruta) is TextureImporter imp && imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (sprite == null) Debug.LogWarning("[MenusDePapel] Falta el icono " + ruta);
        return sprite;
    }

    static GameObject Raiz(MonoBehaviour c)
    {
        var so = new SerializedObject(c);
        var r = so.FindProperty("root");
        return r != null && r.objectReferenceValue is GameObject go ? go : c.gameObject;
    }

    // Los paneles de la escena que ahora se montan por código (y el fondo de la raíz, que lo pone el panel).
    static bool QuitarHijos(GameObject raiz, StringBuilder informe)
    {
        bool cambio = false;
        var fondo = raiz.GetComponent<UnityEngine.UI.Image>();
        if (fondo != null && fondo.enabled)
        {
            fondo.enabled = false;
            informe.AppendLine("  apagado el fondo de " + raiz.name);
            cambio = true;
        }
        for (int i = raiz.transform.childCount - 1; i >= 0; i--)
        {
            var hijo = raiz.transform.GetChild(i).gameObject;
            informe.AppendLine("  quitado " + raiz.name + "/" + hijo.name);
            Object.DestroyImmediate(hijo);
            cambio = true;
        }
        if (raiz.GetComponentInParent<Canvas>(true) == null) informe.AppendLine("  ¡OJO! " + raiz.name + " no tiene lienzo");
        return cambio;
    }

    /// Parte del sprite que no es transparente, en fracciones del sprite (desde abajo a la izquierda).
    static Rect ZonaVisible(Sprite sprite)
    {
        if (sprite == null) return new Rect(0f, 0f, 1f, 1f);
        var ruta = AssetDatabase.GetAssetPath(sprite.texture);
        var tex = new Texture2D(2, 2);
        if (string.IsNullOrEmpty(ruta) || !tex.LoadImage(System.IO.File.ReadAllBytes(ruta))) return new Rect(0f, 0f, 1f, 1f);
        var r = sprite.rect;
        float kx = tex.width / (float)sprite.texture.width, ky = tex.height / (float)sprite.texture.height;
        int x0 = Mathf.RoundToInt(r.x * kx), y0 = Mathf.RoundToInt(r.y * ky), w = Mathf.RoundToInt(r.width * kx), h = Mathf.RoundToInt(r.height * ky);
        var px = tex.GetPixels32();
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (px[(y0 + y) * tex.width + x0 + x].a < 20) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Object.DestroyImmediate(tex);
        if (maxX < 0) return new Rect(0f, 0f, 1f, 1f);
        return new Rect(minX / (float)w, minY / (float)h, (maxX - minX + 1) / (float)w, (maxY - minY + 1) / (float)h);
    }

    static bool PonerRect(Object componente, string campo, Rect valor)
    {
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null || p.rectValue == valor) return false;
        p.rectValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool Poner(Object componente, string campo, Object valor)
    {
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null || p.objectReferenceValue == valor) return false;
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
