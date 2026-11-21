using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El menú principal con la disposición de la maqueta «Menú principal» (lienzo de 1920×1080):
/// velos de noche a la izquierda y abajo, el título con su ficha de fase, la columna de botones
/// (los dos primeros grandes; el elegido dorado, inclinado y con estrella), los enlaces de abajo,
/// la ficha «Tu partida» y la del idioma arriba a la derecha y la ayuda del mando. Coloca y viste
/// los botones de la escena, que siguen con su cableado de <see cref="MainMenuController"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class EstiloDePapelDelMenu : MonoBehaviour
{
    [Tooltip("Contenedor de las filas del menú (cada hijo con un Button es una fila).")]
    [SerializeField] private Transform filas;
    [Tooltip("Piezas del menú clásico que sobran con el papel (el velo lateral).")]
    [SerializeField] private GameObject[] apagar;
    [Tooltip("Título del juego.")]
    [SerializeField] private RectTransform titulo;
    [Tooltip("Parte visible del sprite del título (fracciones, desde abajo a la izquierda); la pone el montaje de los menús.")]
    [SerializeField] private Rect zonaVisibleDelTitulo = new Rect(0f, 0f, 1f, 1f);
    [Tooltip("Etiqueta de la versión: se coloca abajo a la derecha, bajo la ayuda del mando.")]
    [SerializeField] private RectTransform version;
    [Tooltip("Fuentes y colores de los menús de papel.")]
    [SerializeField] private EstiloDeMenuSencillo estilo;
    [Tooltip("Botones que van como enlaces en la fila de abajo y no en la columna (por nombre del objeto).")]
    [SerializeField] private string[] enlaces = { "BotonPatchNotes", "BotonBugReport", "BotonLab", "BotonIconosDeMando" };

    /// Colores de la maqueta que no son de la paleta: noche del fondo, texto sobre noche y texto suave sobre papel.
    public static readonly Color NocheProfunda = new Color32(0x1B, 0x15, 0x30, 0xFF);
    public static readonly Color TextoSobreNoche = new Color32(0xEE, 0xE6, 0xD8, 0xFF);
    public static readonly Color TextoSuave = new Color32(0x6B, 0x58, 0x44, 0xFF);

    private RectTransform _lienzo;
    private TextMeshProUGUI _textoIdioma;
    private Button _primeraFila;
    private readonly List<(TextMeshProUGUI texto, string clave, string porDefecto)> _traducibles = new();

    private TMP_FontAsset Titulos => estilo != null && estilo.fuenteTitulo != null ? estilo.fuenteTitulo : TMP_Settings.defaultFontAsset;
    private TMP_FontAsset Letra => estilo != null && estilo.fuenteTexto != null ? estilo.fuenteTexto : TMP_Settings.defaultFontAsset;

    // El MainMenuController crea alguna fila en su Start (la de los iconos del mando): se monta un frame después.
    private IEnumerator Start()
    {
        yield return null;
        foreach (var go in apagar) if (go != null && go.activeSelf) go.SetActive(false);
        _lienzo = PiezasDeUI.LienzoDeMaqueta((RectTransform)transform);
        _lienzo.SetAsFirstSibling();
        PonerVelos();
        ColocarTitulo();
        ColocarFilas();
        PonerTuPartida();
        PonerIdioma();
        PonerAyuda();
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLocaleChanged += Traducir;
        Traducir();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLocaleChanged -= Traducir;
        PlayerSettings.LanguageChanged -= AlCambiarIdioma;
    }

    private RectTransform Hueco(string nombre, float x, float y, float ancho, float alto, float giro = 0f, Transform padre = null)
    {
        var rt = new GameObject(nombre, typeof(RectTransform)).transform as RectTransform;
        rt.SetParent(padre != null ? padre : _lienzo, false);
        PiezasDeUI.ColocarArribaIzquierda(rt, x, y, ancho, alto, giro);
        return rt;
    }

    private TextMeshProUGUI Texto(string nombre, Transform padre, string texto, bool titulo, float tamano, Color color,
        TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft)
    {
        var t = PiezasDeUI.Texto(nombre, padre, texto, titulo ? Titulos : Letra, tamano, color);
        t.alignment = alineacion;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        PiezasDeUI.Estirar(t.rectTransform);
        return t;
    }

    private TextMeshProUGUI Traducible(TextMeshProUGUI t, string clave, string porDefecto)
    {
        _traducibles.Add((t, clave, porDefecto));
        return t;
    }

    private void Traducir()
    {
        var loc = LocalizationManager.Instance;
        foreach (var (t, clave, porDefecto) in _traducibles)
            if (t != null) t.text = loc != null ? loc.Get(clave, porDefecto) : porDefecto;
        if (_textoIdioma != null) _textoIdioma.text = PlayerSettings.Language == "en" ? "English" : "Español";
    }

    private void PonerVelos()
    {
        var izquierda = PiezasDeUI.Velo("VeloIzquierdo", _lienzo, NocheProfunda, 0.86f, 0.62f, 0.55f, false);
        PiezasDeUI.ColocarArribaIzquierda(izquierda.rectTransform, 0f, 0f, 760f, 1080f);
        var abajo = PiezasDeUI.Velo("VeloDeAbajo", _lienzo, NocheProfunda, 0.95f, 0.6f, 0.5f, true);
        PiezasDeUI.ColocarArribaIzquierda(abajo.rectTransform, 0f, 880f, 1920f, 200f);
    }

    private void ColocarTitulo()
    {
        if (titulo == null) return;
        var fase = titulo.Find("PhaseLabel") as RectTransform;
        titulo.SetParent(_lienzo, false);
        titulo.SetSiblingIndex(2);
        titulo.localScale = Vector3.one;
        var img = titulo.GetComponent<Image>();
        if (img != null)
        {
            PiezasDeUI.ColocarRecortado(img, zonaVisibleDelTitulo, 102f, 50f, 673f);
            img.raycastTarget = false; // su rectángulo es mucho mayor que el dibujo y quedaría encima de nada
        }

        if (fase == null) return;
        var ficha = PiezasDeUI.NuevaCarta("FichaDeFase", _lienzo, PaletaUI.Oro, 14f);
        PiezasDeUI.ColocarArribaIzquierda(ficha.raiz, 670f, 370f, 150f, 44f, 3f);
        fase.SetParent(ficha.contenido, false);
        PiezasDeUI.Estirar(fase);
        fase.localRotation = Quaternion.identity;
        fase.localScale = Vector3.one;
        var t = fase.GetComponent<TMP_Text>();
        if (t == null) return;
        t.font = Titulos;
        t.color = PaletaUI.TintaDeTexto;
        t.fontStyle = FontStyles.Normal;
        t.enableAutoSizing = false;
        t.fontSize = 22f;
        t.alignment = TextAlignmentOptions.Center;
        t.margin = Vector4.zero;
    }

    private bool EsEnlace(Button b) => Array.IndexOf(enlaces, b.name) >= 0;

    private static bool EsGrande(Button b, TMP_Text texto)
    {
        string s = (b.name + " " + (texto != null ? texto.text : "")).ToLowerInvariant();
        return s.Contains("continu") || s.Contains("nuev") || s.Contains("new");
    }

    private void ColocarFilas()
    {
        var raiz = filas != null ? filas : transform;
        var cursor = transform.Find("CursorSeleccion");
        if (cursor != null) cursor.gameObject.SetActive(false);
        var botones = raiz.GetComponentsInChildren<Button>(true);
        float y = 470f, xEnlace = 110f;
        bool habiaGrandes = false, primeraPequena = true;
        foreach (var b in botones)
        {
            var texto = b.GetComponentInChildren<TMP_Text>(true);
            if (EsEnlace(b))
            {
                if (!b.gameObject.activeSelf) continue;
                ColocarEnlace(b, texto, xEnlace);
                xEnlace += 240f;
                continue;
            }
            if (!b.gameObject.activeSelf) continue;
            bool grande = EsGrande(b, texto);
            if (!grande && primeraPequena && habiaGrandes) y += 8f;
            if (!grande) primeraPequena = false; else habiaGrandes = true;
            ColocarFila(b, texto, grande, y);
            y += grande ? 108f : 72f;
        }
        var vlg = raiz.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;
        var teclado = GetComponent<SeleccionConTeclado>();
        if (teclado == null) teclado = gameObject.AddComponent<SeleccionConTeclado>();
        teclado.Configurar(_primeraFila);
    }

    private void ColocarFila(Button b, TMP_Text texto, bool grande, float y)
    {
        float ancho = grande ? 520f : 440f, alto = grande ? 92f : 60f, x = grande ? 110f : 124f;
        var rt = (RectTransform)b.transform;
        rt.SetParent(_lienzo, false);
        rt.localScale = Vector3.one;
        PiezasDeUI.ColocarArribaIzquierda(rt, x, y, ancho, alto);
        var le = b.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;
        var fondo = b.GetComponent<Image>();
        if (fondo != null) { fondo.sprite = null; fondo.color = new Color(1f, 1f, 1f, 0f); }
        var brillo = b.transform.Find("SelectionGlow");
        if (brillo != null) brillo.gameObject.SetActive(false);

        var carta = PiezasDeUI.NuevaCarta("Carta", b.transform, PaletaUI.Papel, grande ? 27f : 18f, 5f);
        PiezasDeUI.Estirar(carta.raiz);
        carta.raiz.SetAsFirstSibling();
        if (texto != null)
        {
            texto.rectTransform.SetParent(carta.contenido, false);
            PiezasDeUI.Estirar(texto.rectTransform);
            texto.font = Titulos;
            texto.fontStyle = FontStyles.Normal;
            texto.enableAutoSizing = false;
            texto.fontSize = grande ? 42f : 29f;
            texto.alignment = TextAlignmentOptions.MidlineLeft;
            texto.margin = new Vector4(30f, -6f, 20f, 0f);
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.color = PaletaUI.TintaDeTexto;
            var resalte = texto.GetComponent<MenuTextHighlight>();
            if (resalte != null) resalte.enabled = false;
        }
        // Adornos del elegido: estrella grande a la izquierda, chispa arriba a la derecha e insignia de la A.
        float lado = grande ? 52f : 36f;
        var estrella = PiezasDeUI.EstrellaEn("Estrella", carta.contenido, grande ? -14f : -10f, alto * 0.5f - 2f, lado);
        var chispa = PiezasDeUI.EstrellaEn("Chispa", carta.contenido, ancho - 9f, 1f, grande ? 30f : 22f, 14f);
        var insignia = PiezasDeUI.IconoDeBoton("BotonA", carta.contenido, Core.InputGlyphs.InputGlyphNames.Confirm, grande ? 44f : 34f);
        PiezasDeUI.ColocarArribaIzquierda(insignia, ancho - (grande ? 66f : 50f), (alto - (grande ? 44f : 34f)) * 0.5f, grande ? 44f : 34f, grande ? 44f : 34f);
        // «Espacio» (teclado) es más ancho que la A del mando: crece hacia dentro de la ficha, no hacia fuera.
        insignia.GetComponent<IconoDeBotonVivo>()?.Crecer(IconoDeBotonVivo.HaciaDonde.Izquierda);
        var adornos = new[] { estrella.gameObject, chispa.gameObject, insignia.gameObject };

        if (b.GetComponent<FocoAlPasarElRaton>() == null) b.gameObject.AddComponent<FocoAlPasarElRaton>();
        if (_primeraFila == null) _primeraFila = b;
        var ficha = b.GetComponent<FichaElegible>();
        if (ficha == null) ficha = b.gameObject.AddComponent<FichaElegible>();
        ficha.Configurar(b, carta, texto, adornos);
        ficha.Colores(PaletaUI.Papel, PaletaUI.Oro, PaletaUI.TintaDeTexto, PaletaUI.TintaDeTexto);
        ficha.Movimiento(0f, -1.4f, new Vector2(18f, 0f), 5f, 7f);
    }

    private void ColocarEnlace(Button b, TMP_Text texto, float x)
    {
        var rt = (RectTransform)b.transform;
        rt.SetParent(_lienzo, false);
        rt.localScale = Vector3.one;
        PiezasDeUI.ColocarArribaIzquierda(rt, x, 990f, 210f, 36f);
        var le = b.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;
        var fondo = b.GetComponent<Image>();
        if (fondo != null) { fondo.sprite = null; fondo.color = new Color(1f, 1f, 1f, 0f); }
        var brillo = b.transform.Find("SelectionGlow");
        if (brillo != null) brillo.gameObject.SetActive(false);
        if (texto != null)
        {
            PiezasDeUI.Estirar(texto.rectTransform);
            texto.font = Letra;
            texto.fontStyle = FontStyles.Normal;
            texto.enableAutoSizing = false;
            texto.fontSize = 22f;
            texto.alignment = TextAlignmentOptions.TopLeft;
            texto.margin = Vector4.zero;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.color = new Color(TextoSobreNoche.r, TextoSobreNoche.g, TextoSobreNoche.b, 0.85f);
            var resalte = texto.GetComponent<MenuTextHighlight>();
            if (resalte != null) resalte.enabled = false;
        }
        // Subrayado de puntos bajo el texto; con el foco, dorado.
        var raya = PiezasDeUI.Imagen("Subrayado", b.transform, PiezasDeUI.Punteado, TextoSobreNoche);
        raya.type = Image.Type.Tiled;
        raya.raycastTarget = false;
        var rr = raya.rectTransform;
        rr.anchorMin = new Vector2(0f, 0f); rr.anchorMax = new Vector2(0f, 0f); rr.pivot = Vector2.zero;
        rr.anchoredPosition = Vector2.zero;
        rr.sizeDelta = new Vector2(texto != null ? Mathf.Min(210f, texto.preferredWidth) : 190f, 3f);
        if (b.GetComponent<FocoAlPasarElRaton>() == null) b.gameObject.AddComponent<FocoAlPasarElRaton>();
        var ficha = b.GetComponent<FichaElegible>();
        if (ficha == null) ficha = b.gameObject.AddComponent<FichaElegible>();
        ficha.Configurar(b, new PiezasDeUI.Carta { raiz = null, relleno = raya }, texto, null);
        ficha.Colores(TextoSobreNoche, PaletaUI.Oro, new Color(TextoSobreNoche.r, TextoSobreNoche.g, TextoSobreNoche.b, 0.85f), PaletaUI.Oro);
        ficha.Movimiento(0f, 0f, Vector2.zero, 0f, 0f);
    }

    private void PonerTuPartida()
    {
        var guardado = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        if (guardado == null || !guardado.HasSave() || !guardado.Load(out var datos) || datos == null) return;

        var tarjeta = PiezasDeUI.NuevaCarta("TuPartida", _lienzo, PaletaUI.Papel, 22f);
        PiezasDeUI.ColocarArribaIzquierda(tarjeta.raiz, 1150f, 122f, 700f, 176f, 1.2f);
        var etiqueta = PiezasDeUI.NuevaCarta("Etiqueta", _lienzo, PaletaUI.Oro, 16f);
        PiezasDeUI.ColocarArribaIzquierda(etiqueta.raiz, 1180f, 96f, 230f, 50f, -2f);
        Traducible(Texto("Texto", etiqueta.contenido, "", true, 28f, PaletaUI.TintaDeTexto, TextAlignmentOptions.Center), "MENU_TU_PARTIDA", "Tu partida");

        var grupo = new List<PartyControlManager.CharacterSlot> { PartyControlManager.CharacterSlot.Will };
        foreach (var id in datos.partyMemberIds)
        {
            if (id == null) continue;
            if (id.IndexOf("Estela", StringComparison.OrdinalIgnoreCase) >= 0 && !grupo.Contains(PartyControlManager.CharacterSlot.Estela)) grupo.Add(PartyControlManager.CharacterSlot.Estela);
            if (id.IndexOf("Liam", StringComparison.OrdinalIgnoreCase) >= 0 && !grupo.Contains(PartyControlManager.CharacterSlot.Liam)) grupo.Add(PartyControlManager.CharacterSlot.Liam);
        }
        var alMando = (PartyControlManager.CharacterSlot)datos.activeCharacterSlot;
        for (int i = 0; i < grupo.Count; i++)
        {
            float x = 1180f + i * 92f;
            var ficha = FichaDePersonaje.Buscar(grupo[i]);
            if (grupo[i] == alMando)
            {
                var aro = PiezasDeUI.Imagen("Aro", _lienzo, PiezasDeUI.Circulo, PaletaUI.Oro);
                aro.raycastTarget = false;
                PiezasDeUI.ColocarArribaIzquierda(aro.rectTransform, x - 4f, 152f, 86f, 86f);
            }
            var borde = PiezasDeUI.Imagen("Retrato " + grupo[i], _lienzo, PiezasDeUI.Circulo, PiezasDeUI.TintaOscura);
            borde.raycastTarget = false;
            PiezasDeUI.ColocarArribaIzquierda(borde.rectTransform, x, 156f, 78f, 78f);
            var filo = PiezasDeUI.Imagen("Filo", borde.transform, PiezasDeUI.Circulo, PaletaUI.PapelClaro);
            filo.raycastTarget = false;
            PiezasDeUI.Margen(filo.rectTransform, 3f);
            var mascara = PiezasDeUI.Imagen("Mascara", filo.transform, PiezasDeUI.Circulo,
                ficha != null && ficha.ColorDelPersonaje.a > 0f ? ficha.ColorDelPersonaje : PaletaUI.Lavanda);
            PiezasDeUI.Margen(mascara.rectTransform, 3f);
            mascara.raycastTarget = false;
            if (ficha != null && ficha.Retrato != null)
            {
                mascara.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var cara = PiezasDeUI.Imagen("Cara", mascara.transform, ficha.Retrato, Color.white);
                cara.preserveAspect = true;
                cara.raycastTarget = false;
                PiezasDeUI.Estirar(cara.rectTransform);
            }
            var nombre = Texto("Nombre", Hueco("Nombre", x - 8f, 240f, 94f, 26f), grupo[i].ToString(), false, 19f, TextoSuave, TextAlignmentOptions.Center);
            nombre.raycastTarget = false;
        }

        var raya = PiezasDeUI.Imagen("Separador", _lienzo, null, new Color(PaletaUI.Tinta.r, PaletaUI.Tinta.g, PaletaUI.Tinta.b, 0.35f));
        raya.raycastTarget = false;
        PiezasDeUI.ColocarArribaIzquierda(raya.rectTransform, 1466f, 152f, 3f, 116f);

        int capitulo = CapituloDe(datos);
        string lugar = LugarDe(datos);
        var loc = LocalizationManager.Instance;
        if (capitulo > 0)
            Texto("Capitulo", Hueco("Capitulo", 1490f, 148f, 320f, 28f),
                  string.Format(loc != null ? loc.Get("MENU_CAPITULO", "Capítulo {0}") : "Capítulo {0}", capitulo), false, 22f, TextoSuave, TextAlignmentOptions.TopLeft);
        if (!string.IsNullOrEmpty(lugar))
            Texto("Lugar", Hueco("Lugar", 1490f, 176f, 340f, 50f), lugar, true, 38f, PaletaUI.TintaDeTexto, TextAlignmentOptions.TopLeft);
        PiezasDeUI.EstrellaEn("Estrella", _lienzo, 1507f, 251f, 34f, 10f);
        var fecha = guardado.FechaDelGuardado;
        if (fecha.HasValue)
            Texto("Cuando", Hueco("Cuando", 1532f, 234f, 300f, 32f), HaceCuanto(fecha.Value), false, 22f, PaletaUI.TintaDeTexto);
    }

    /// <summary>
    /// El capítulo en curso: el más alto de los grafos «CapN» guardados que ya ha empezado. Los
    /// capítulos siguientes también tienen pizarra (arrancan y se quedan esperando al anterior),
    /// pero en ella solo está el paso en el que esperan; uno empezado ya guarda algo más (señales
    /// recibidas, misiones, ramas).
    /// </summary>
    private static int CapituloDe(PlayerSaveData datos)
    {
        int mejor = 0;
        if (datos.narrativeBlackboards == null) return 0;
        foreach (var bb in datos.narrativeBlackboards)
        {
            var etiqueta = bb.graphLabel;
            if (string.IsNullOrEmpty(etiqueta) || !etiqueta.StartsWith("Cap", StringComparison.OrdinalIgnoreCase)) continue;
            if (!HaEmpezado(bb)) continue;
            int n = 0, i = 3;
            while (i < etiqueta.Length && char.IsDigit(etiqueta[i])) n = n * 10 + (etiqueta[i++] - '0');
            mejor = Mathf.Max(mejor, n);
        }
        return mejor;
    }

    private static bool HaEmpezado(PlayerSaveData.NarrativeBlackboardSnapshot bb)
    {
        if (bb.blackboardData == null) return false;
        foreach (var e in bb.blackboardData)
            if (e != null && e.key != "__currentNodeGuid") return true;
        return false;
    }

    /// El último lugar que el grupo conoció (lo anuncia <see cref="PresentacionDeLugares"/>).
    private static string LugarDe(PlayerSaveData datos)
    {
        if (datos.flags == null) return null;
        for (int i = datos.flags.Count - 1; i >= 0; i--)
        {
            var f = datos.flags[i];
            int p = f != null ? f.IndexOf(LugaresConocidos.Prefijo, StringComparison.Ordinal) : -1;
            if (p < 0) continue;
            string clave = f.Substring(p + LugaresConocidos.Prefijo.Length);
            var loc = LocalizationManager.Instance;
            return loc != null ? loc.Get(clave, clave) : clave;
        }
        return null;
    }

    private static string HaceCuanto(DateTime fecha)
    {
        var loc = LocalizationManager.Instance;
        string L(string clave, string porDefecto) => loc != null ? loc.Get(clave, porDefecto) : porDefecto;
        var lapso = DateTime.Now - fecha;
        if (lapso.TotalMinutes < 2) return L("MENU_GUARDADA_AHORA", "guardada hace un momento");
        if (lapso.TotalHours < 1) return string.Format(L("MENU_GUARDADA_MINUTOS", "guardada hace {0} min"), (int)lapso.TotalMinutes);
        if (lapso.TotalDays < 1) return string.Format(L("MENU_GUARDADA_HORAS", "guardada hace {0} h"), (int)lapso.TotalHours);
        int dias = (int)lapso.TotalDays;
        return dias == 1 ? L("MENU_GUARDADA_AYER", "guardada ayer") : string.Format(L("MENU_GUARDADA_DIAS", "guardada hace {0} días"), dias);
    }

    private void PonerIdioma()
    {
        var pastilla = PiezasDeUI.NuevaCarta("Idioma", _lienzo, PaletaUI.PapelClaro, 28f);
        PiezasDeUI.ColocarArribaIzquierda(pastilla.raiz, 1640f, 40f, 170f, 56f);
        var globo = PiezasDeUI.Imagen("Globo", pastilla.contenido, PiezasDeUI.Globo, Color.white);
        globo.raycastTarget = false;
        PiezasDeUI.ColocarArribaIzquierda(globo.rectTransform, 16f, 12f, 32f, 32f);
        _textoIdioma = Texto("Texto", Hueco("Texto", 58f, 8f, 104f, 40f, 0f, pastilla.contenido), "", true, 24f, PaletaUI.TintaDeTexto);
        // Pulsar la pastilla cambia de idioma (con el ratón; con el mando, en Ajustes ▸ Idioma).
        var boton = pastilla.raiz.gameObject.AddComponent<Button>();
        var tapa = pastilla.raiz.gameObject.AddComponent<Image>();
        tapa.color = new Color(1f, 1f, 1f, 0f);
        boton.targetGraphic = tapa;
        boton.transition = Selectable.Transition.None;
        boton.navigation = new Navigation { mode = Navigation.Mode.None };
        boton.onClick.AddListener(() => PlayerSettings.SetLanguage(PlayerSettings.Language == "en" ? "es" : "en"));
        PlayerSettings.LanguageChanged += AlCambiarIdioma;
    }

    private void AlCambiarIdioma(string _) => Traducir();

    private void PonerAyuda()
    {
        Traducible(Ayuda(_lienzo, Titulos, 1568f, Core.InputGlyphs.InputGlyphNames.Confirm), "MENU_ELEGIR", "Elegir");
        Traducible(Ayuda(_lienzo, Titulos, 1736f, Core.InputGlyphs.InputGlyphNames.Cancel), "MENU_ATRAS", "Atrás");
        if (version == null) return;
        version.anchorMin = version.anchorMax = version.pivot = new Vector2(1f, 0f);
        version.anchoredPosition = new Vector2(-24f, 6f);
        var t = version.GetComponent<TMP_Text>();
        if (t == null) return;
        t.fontSize = 18f;
        t.alignment = TextAlignmentOptions.BottomRight;
    }

    /// <summary>
    /// Una ayuda del mando de las maquetas (abajo a la derecha, sobre la noche): el icono del botón
    /// del dispositivo activo (<see cref="Core.InputGlyphs.InputGlyphNames"/>) en (x, 1004) y su texto
    /// al lado, que se devuelve para escribirlo o traducirlo.
    /// </summary>
    public static TextMeshProUGUI Ayuda(RectTransform lienzo, TMP_FontAsset fuente, float x, string glifo)
    {
        var icono = PiezasDeUI.IconoDeBoton("Icono " + glifo, lienzo, glifo, 44f);
        PiezasDeUI.ColocarArribaIzquierda(icono, x - 2f, 1002f, 44f, 44f);
        var hueco = new GameObject("Ayuda " + glifo, typeof(RectTransform)).transform as RectTransform;
        hueco.SetParent(lienzo, false);
        PiezasDeUI.ColocarArribaIzquierda(hueco, x + 50f, 1006f, 140f, 36f);
        // Con una tecla ancha («Espacio») el icono crece hacia la derecha y empuja su texto.
        icono.GetComponent<IconoDeBotonVivo>()?.Crecer(IconoDeBotonVivo.HaciaDonde.Derecha, hueco);
        var t = PiezasDeUI.Texto("Texto", hueco, "", fuente, 25f, TextoSobreNoche);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        PiezasDeUI.Estirar(t.rectTransform);
        return t;
    }
}
