using UnityEngine;

/// Estilos del panel del LAB: ventana oscura translúcida, acento dorado, botones claros y texto
/// legible. Se crean una vez (dentro de OnGUI, que es cuando existe GUI.skin) y valen para todas
/// las escenas de prueba.
public static class EstiloDelLab
{
    public static readonly Color Dorado = new Color(0.98f, 0.82f, 0.42f);
    private static readonly Color Texto = new Color(0.92f, 0.93f, 0.96f);
    private static readonly Color TextoSuave = new Color(0.66f, 0.69f, 0.77f);

    public static GUIStyle Ventana, Fondo, Cabecera, Pestana, PestanaActiva, Boton, BotonActivo,
                           Etiqueta, Nota, Titulo, Aviso, Barra, BarraRelleno, Fila, FilaActiva;

    private static bool _listo;

    public static void Preparar()
    {
        if (_listo) return;
        _listo = true;

        Ventana = Caja(new Color(0.07f, 0.08f, 0.12f, 0.96f), 16);
        Fondo = Caja(new Color(0f, 0f, 0f, 0.45f), 0);
        Aviso = Caja(new Color(0.07f, 0.08f, 0.12f, 0.88f), 8);
        Aviso.normal.textColor = Dorado;
        Aviso.fontSize = 14;
        Aviso.alignment = TextAnchor.MiddleCenter;
        Aviso.fontStyle = FontStyle.Bold;

        Cabecera = Letra(20, Dorado, FontStyle.Bold);
        Titulo = Letra(16, Dorado, FontStyle.Bold);
        Titulo.margin = new RectOffset(0, 0, 10, 4);
        Etiqueta = Letra(14, Texto, FontStyle.Normal);
        Etiqueta.wordWrap = true;
        Etiqueta.richText = true;
        Nota = Letra(12, TextoSuave, FontStyle.Italic);
        Nota.wordWrap = true;

        Boton = BotonCon(new Color(0.18f, 0.20f, 0.28f), new Color(0.26f, 0.29f, 0.40f), Texto);
        BotonActivo = BotonCon(new Color(0.55f, 0.42f, 0.14f), new Color(0.66f, 0.51f, 0.18f), Color.white);
        Pestana = BotonCon(new Color(0.12f, 0.13f, 0.19f), new Color(0.20f, 0.22f, 0.30f), TextoSuave);
        PestanaActiva = BotonCon(new Color(0.24f, 0.26f, 0.36f), new Color(0.24f, 0.26f, 0.36f), Dorado);
        PestanaActiva.fontStyle = FontStyle.Bold;
        Fila = BotonCon(new Color(0.11f, 0.12f, 0.17f), new Color(0.20f, 0.22f, 0.30f), Texto);
        Fila.alignment = TextAnchor.MiddleLeft;
        Fila.richText = true;
        FilaActiva = BotonCon(new Color(0.42f, 0.32f, 0.10f), new Color(0.50f, 0.38f, 0.12f), Color.white);
        FilaActiva.alignment = TextAnchor.MiddleLeft;
        FilaActiva.richText = true;

        Barra = Caja(new Color(0.16f, 0.17f, 0.24f), 0);
        BarraRelleno = Caja(Dorado, 0);
    }

    /// Botón que se queda resaltado cuando 'activo'.
    public static bool Opcion(string texto, bool activo, params GUILayoutOption[] opciones)
        => GUILayout.Button(texto, activo ? BotonActivo : Boton, opciones);

    /// Barra de progreso (0..1) de la altura indicada.
    public static void Progreso(float valor, float alto = 8f)
    {
        Rect r = GUILayoutUtility.GetRect(10f, alto, GUILayout.ExpandWidth(true));
        GUI.Box(r, GUIContent.none, Barra);
        if (valor > 0f) GUI.Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(valor), r.height), GUIContent.none, BarraRelleno);
    }

    private static GUIStyle Caja(Color color, int relleno)
    {
        var s = new GUIStyle();
        s.normal.background = Textura(color);
        s.padding = new RectOffset(relleno, relleno, relleno, relleno);
        return s;
    }

    private static GUIStyle Letra(int tamano, Color color, FontStyle estilo)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = tamano, fontStyle = estilo };
        s.normal.textColor = color;
        return s;
    }

    private static GUIStyle BotonCon(Color fondo, Color encima, Color texto)
    {
        var s = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        s.normal.background = Textura(fondo);
        s.hover.background = Textura(encima);
        s.active.background = Textura(encima);
        s.focused.background = Textura(fondo);
        s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = texto;
        s.border = new RectOffset(0, 0, 0, 0);
        s.padding = new RectOffset(10, 10, 6, 6);
        s.margin = new RectOffset(3, 3, 3, 3);
        s.fixedHeight = 30f;
        return s;
    }

    private static Texture2D Textura(Color color)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }
}
