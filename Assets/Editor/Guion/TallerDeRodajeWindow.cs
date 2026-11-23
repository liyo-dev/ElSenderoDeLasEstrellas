#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// La mesa de montaje de una cinemática: elige el guion, lo hornea, enseña un fotograma por plano,
/// deja ajustar el encuadre y aplica el cambio con un diff antes de escribir nada. El `.guion.txt`
/// sigue siendo la única fuente de verdad; esta ventana no inventa un segundo sistema. Ver INC-1230
/// y TDD § 33.
public class TallerDeRodajeWindow : EditorWindow
{
    private const string PrefGuion = "TallerDeRodaje.Guion";

    private GuionHorneado _guion;
    private string _rutaTexto;

    private Vector2 _scrollPlanos;
    private Vector2 _scrollTira;
    private Vector2 _scrollEstado;
    private int _seleccion = -1;

    private readonly Dictionary<string, Texture2D> _texturas = new();
    private List<PrevizDeGuion.Fotograma> _fotos = new();

    private List<string> _errores = new();
    private List<string> _avisos = new();
    private List<string> _diff = new();

    private Horno _hornoPendiente;
    private GuionHorneado _pendiente;
    private string _textoPendiente;

    private float _lente;
    private float _alturaFoco;
    private float _suave;
    private float _mezcla;
    private float _mano;
    private int _encuadre;

    private bool _ocupado;
    private string _mensaje;

    public static void Abrir()
    {
        var ventana = GetWindow<TallerDeRodajeWindow>("Taller de rodaje");
        ventana.minSize = new Vector2(1000, 580);
        ventana.Show();
    }

    public static void AbrirCon(GuionHorneado guion)
    {
        var ventana = GetWindow<TallerDeRodajeWindow>("Taller de rodaje");
        ventana.minSize = new Vector2(1000, 580);
        ventana.Seleccionar(guion, true);
        ventana.Show();
    }

    private void OnEnable()
    {
        if (_guion != null) return;

        string ruta = EditorPrefs.GetString(PrefGuion, string.Empty);
        var guardado = string.IsNullOrEmpty(ruta)
            ? null
            : AssetDatabase.LoadAssetAtPath<GuionHorneado>(ruta);
        var elegido = guardado ?? Selection.activeObject as GuionHorneado;
        if (elegido != null) Seleccionar(elegido, true);
    }

    private void OnDisable()
    {
        DestruirTexturas();
        DescartarPendiente();
    }

    private void OnGUI()
    {
        DibujarBarra();

        if (_guion == null)
        {
            EditorGUILayout.HelpBox(
                "Elige un guion horneado. El taller enseña sus planos como una tira de fotos, deja " +
                "ajustar el encuadre y hornea con un diff antes de escribir nada.",
                MessageType.Info);
            return;
        }

        if (!string.IsNullOrEmpty(_mensaje))
            EditorGUILayout.HelpBox(_mensaje, MessageType.None);

        if (string.IsNullOrEmpty(_rutaTexto))
            EditorGUILayout.HelpBox(
                "No encuentro el .guion.txt de este horneado. Vuelve a hornearlo desde su menú o " +
                "desde la ficha de la secuencia para que el taller sepa de dónde viene.",
                MessageType.Warning);

        using (new EditorGUI.DisabledScope(_ocupado))
        {
            EditorGUILayout.BeginHorizontal();
            DibujarPlanos();
            DibujarDetalle();
            EditorGUILayout.EndHorizontal();

            DibujarTira();
            DibujarEstado();
        }
    }

    private void DibujarBarra()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var elegido = (GuionHorneado)EditorGUILayout.ObjectField(
            _guion, typeof(GuionHorneado), false, GUILayout.Width(320));
        if (elegido != _guion) Seleccionar(elegido, true);

        using (new EditorGUI.DisabledScope(_ocupado || _guion == null || _pendiente != null))
        {
            if (GUILayout.Button("Hornear", EditorStyles.toolbarButton, GUILayout.Width(64)))
                HornearCompleto();
        }
        using (new EditorGUI.DisabledScope(_ocupado || _guion == null))
        {
            if (GUILayout.Button("Validar", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Validar();
            if (GUILayout.Button("Previsualizar", EditorStyles.toolbarButton, GUILayout.Width(96)))
                Previsualizar();
            if (GUILayout.Button("Abrir el .txt", EditorStyles.toolbarButton, GUILayout.Width(84)))
                AbrirTexto();
            if (GUILayout.Button("Catálogo", EditorStyles.toolbarButton, GUILayout.Width(66)))
                CatalogoDeEfectosWindow.Abrir();
        }

        GUILayout.FlexibleSpace();
        if (_guion != null)
            EditorGUILayout.LabelField(_guion.nombre, EditorStyles.miniLabel, GUILayout.Width(200));

        EditorGUILayout.EndHorizontal();
    }

    private void Seleccionar(GuionHorneado guion, bool forzar = false)
    {
        if (!forzar && guion == _guion) return;

        DescartarPendiente();
        DestruirTexturas();
        _fotos.Clear();
        _errores.Clear();
        _avisos.Clear();
        _diff.Clear();
        _mensaje = null;

        _guion = guion;
        _rutaTexto = GuionTextoEditable.RutaDelTexto(guion);
        _seleccion = guion != null && guion.planos != null && guion.planos.Count > 0 ? 0 : -1;

        EditorPrefs.SetString(PrefGuion, guion != null ? AssetDatabase.GetAssetPath(guion) : string.Empty);
        CargarCampos();
    }

    private void DibujarPlanos()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(330)))
        {
            int total = _guion.planos?.Count ?? 0;
            EditorGUILayout.LabelField($"Planos ({total})", EditorStyles.boldLabel);

            _scrollPlanos = EditorGUILayout.BeginScrollView(_scrollPlanos, GUILayout.ExpandHeight(true));
            for (int i = 0; i < total; i++)
            {
                PlanoHorneado plano = _guion.planos[i];
                string sujetos = plano.sujetos != null && plano.sujetos.Count > 0
                    ? string.Join(", ", plano.sujetos)
                    : "—";
                string etiqueta = $"{i + 1:00}  {plano.t0:0.0}-{plano.t1:0.0}s  {plano.tipo}  {sujetos}";

                bool era = _seleccion == i;
                if (GUILayout.Toggle(era, etiqueta, "Button") && !era)
                {
                    _seleccion = i;
                    CargarCampos();
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DibujarDetalle()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (_seleccion < 0 || _seleccion >= (_guion.planos?.Count ?? 0))
            {
                EditorGUILayout.HelpBox("Elige un plano de la lista.", MessageType.Info);
                return;
            }

            PlanoHorneado plano = _guion.planos[_seleccion];
            EditorGUILayout.LabelField($"Plano {_seleccion + 1}: {plano.descripcion}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{plano.t0:0.00} - {plano.t1:0.00} s · {plano.tipo} · línea {plano.linea}",
                EditorStyles.miniLabel);

            Texture2D textura = TexturaDe(_seleccion);
            if (textura != null)
            {
                GUILayout.Box(textura, GUILayout.Width(480), GUILayout.Height(270));
            }
            else
            {
                EditorGUILayout.HelpBox("Sin fotograma todavía. Pulsa «Previsualizar».",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Encuadre", EditorStyles.boldLabel);
            _encuadre = EditorGUILayout.Popup("Encuadre", _encuadre,
                new[] { "centro", "izquierda", "derecha" });
            _lente = EditorGUILayout.FloatField("Lente (FOV)", _lente);
            _alturaFoco = EditorGUILayout.FloatField("Altura del foco", _alturaFoco);
            _suave = EditorGUILayout.FloatField("Suavidad (seguir)", _suave);
            _mezcla = EditorGUILayout.FloatField("Mezcla desde el plano anterior", _mezcla);
            _mano = EditorGUILayout.Slider("Cámara en mano", _mano, 0f, 1f);

            using (new EditorGUI.DisabledScope(
                       _ocupado || _pendiente != null || string.IsNullOrEmpty(_rutaTexto) || plano.linea <= 0))
            {
                if (GUILayout.Button("Aplicar al guion y hornear", GUILayout.Height(30)))
                    Aplicar();
            }

            if (plano.linea <= 0)
                EditorGUILayout.HelpBox(
                    "Este plano viene de un horneado anterior: vuelve a hornear una vez para que " +
                    "guarde su línea y se pueda editar desde aquí.",
                    MessageType.Warning);

            EditorGUILayout.HelpBox(
                "El previz coloca a los actores en su sitio y la cámara real, pero no reproduce el " +
                "gesto exacto ni lanza VFX. Sirve para encuadre y oclusiones, no para juzgar una mano.",
                MessageType.None);
        }
    }

    private void DibujarTira()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Height(118)))
        {
            EditorGUILayout.LabelField("Hoja de contactos", EditorStyles.boldLabel);
            if (_fotos.Count == 0)
            {
                EditorGUILayout.LabelField("Pulsa «Previsualizar» para sacar un fotograma por plano.",
                    EditorStyles.miniLabel);
                return;
            }

            _scrollTira = EditorGUILayout.BeginScrollView(_scrollTira, GUILayout.Height(96));
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < (_guion.planos?.Count ?? 0); i++)
            {
                Texture2D textura = TexturaDe(i);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(134)))
                {
                    if (textura != null)
                    {
                        if (GUILayout.Button(textura, GUIStyle.none,
                                GUILayout.Width(128), GUILayout.Height(72)))
                        {
                            _seleccion = i;
                            CargarCampos();
                            Repaint();
                        }
                    }
                    else
                    {
                        GUILayout.Box("sin foto", GUILayout.Width(128), GUILayout.Height(72));
                    }
                    EditorGUILayout.LabelField($"{i + 1:00}  {_guion.planos[i].t0:0.0}s",
                        EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }
    }

    private void DibujarEstado()
    {
        if (_pendiente != null)
        {
            EditorGUILayout.HelpBox(
                "Horneado en memoria. Revisa lo que cambia antes de guardar: todavía no se ha " +
                "escrito ni el guion ni el asset.",
                MessageType.Info);

            _scrollEstado = EditorGUILayout.BeginScrollView(_scrollEstado, GUILayout.Height(92));
            foreach (string cambio in _diff)
                EditorGUILayout.LabelField("• " + cambio, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Guardar el horneado", GUILayout.Height(28))) GuardarPendiente();
            if (GUILayout.Button("Descartar", GUILayout.Height(28))) DescartarPendiente();
            EditorGUILayout.EndHorizontal();
            return;
        }

        if (_errores.Count == 0 && _avisos.Count == 0) return;

        EditorGUILayout.LabelField(
            $"Errores ({_errores.Count}) · avisos ({_avisos.Count})", EditorStyles.boldLabel);
        _scrollEstado = EditorGUILayout.BeginScrollView(_scrollEstado, GUILayout.Height(92));
        foreach (string error in _errores) DibujarLinea(error);
        foreach (string aviso in _avisos) DibujarLinea(aviso);
        EditorGUILayout.EndScrollView();
    }

    private void DibujarLinea(string texto)
    {
        Match linea = Regex.Match(texto, @"línea\s+(\d+)");
        if (linea.Success && !string.IsNullOrEmpty(_rutaTexto))
        {
            if (GUILayout.Button(texto, EditorStyles.linkLabel))
                AbrirEnLinea(int.Parse(linea.Groups[1].Value, CultureInfo.InvariantCulture));
        }
        else
        {
            EditorGUILayout.LabelField(texto, EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void CargarCampos()
    {
        if (_guion?.planos == null || _seleccion < 0 || _seleccion >= _guion.planos.Count) return;

        PlanoHorneado plano = _guion.planos[_seleccion];
        _lente = plano.lente;
        _alturaFoco = plano.alturaDelFoco;
        _suave = plano.suavidad;
        _mezcla = plano.mezcla;
        _mano = plano.mano;
        _encuadre = plano.encuadre.x < -0.05f ? 1 : plano.encuadre.x > 0.05f ? 2 : 0;
    }

    private void Aplicar()
    {
        if (_guion?.planos == null || _seleccion < 0 || _seleccion >= _guion.planos.Count) return;
        if (string.IsNullOrEmpty(_rutaTexto) || !File.Exists(_rutaTexto))
        {
            _mensaje = "No encuentro el .guion.txt.";
            return;
        }

        PlanoHorneado plano = _guion.planos[_seleccion];
        if (plano.linea <= 0)
        {
            _mensaje = "Vuelve a hornear una vez para que este plano guarde su línea.";
            return;
        }

        var cambios = new Dictionary<string, string>();
        if (Mathf.Abs(_lente - plano.lente) > 0.001f) cambios["lente"] = F(_lente);
        if (Mathf.Abs(_alturaFoco - plano.alturaDelFoco) > 0.001f) cambios["altura_foco"] = F(_alturaFoco);
        if (Mathf.Abs(_suave - plano.suavidad) > 0.001f) cambios["suave"] = F(_suave);
        if (Mathf.Abs(_mezcla - plano.mezcla) > 0.001f) cambios["mezcla"] = F(_mezcla);
        if (Mathf.Abs(_mano - plano.mano) > 0.001f) cambios["mano"] = F(_mano);

        int encuadreOriginal = plano.encuadre.x < -0.05f ? 1 : plano.encuadre.x > 0.05f ? 2 : 0;
        if (_encuadre != encuadreOriginal)
            cambios["encuadre"] = _encuadre == 1 ? "izq" : _encuadre == 2 ? "der" : "centro";

        if (cambios.Count == 0)
        {
            _mensaje = "No has cambiado nada.";
            return;
        }

        string texto = File.ReadAllText(_rutaTexto);
        if (!GuionTextoEditable.ReescribirEnTexto(texto, plano.linea, cambios, out string nuevo))
        {
            _mensaje = "No he podido reescribir esa línea del guion.";
            return;
        }

        DescartarPendiente();
        _ocupado = true;
        try
        {
            _textoPendiente = nuevo;
            _pendiente = HorneadorDeGuion.HornearParaRevisar(_rutaTexto, nuevo, out _hornoPendiente);
            _errores = _hornoPendiente?.Errores ?? new List<string>();
            _avisos = _hornoPendiente?.Avisos ?? new List<string>();
        }
        finally
        {
            _ocupado = false;
        }

        if (_pendiente == null || _errores.Count > 0)
        {
            _mensaje = "El horneado con ese encuadre no sale; no se ha tocado nada.";
            DescartarPendiente();
            return;
        }

        _diff = GuionDiff.Comparar(_guion, _pendiente);
        _mensaje = "Revisa los cambios: todavía no se ha escrito nada.";
        Repaint();
    }

    private void GuardarPendiente()
    {
        if (_pendiente == null || _hornoPendiente == null || _textoPendiente == null) return;

        _ocupado = true;
        try
        {
            File.WriteAllText(_rutaTexto, _textoPendiente, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_rutaTexto, ImportAssetOptions.ForceUpdate);

            _hornoPendiente.GuardarResultado();
            _hornoPendiente.EscribirInforme();
            AssetDatabase.SaveAssets();

            string rutaAsset = AssetDatabase.GetAssetPath(_pendiente);
            if (string.IsNullOrEmpty(rutaAsset))
            {
                rutaAsset = RutaAssetDelGuion();
                UnityEngine.Object.DestroyImmediate(_pendiente);
            }

            _pendiente = null;
            _hornoPendiente = null;
            _textoPendiente = null;
            _diff.Clear();

            if (!string.IsNullOrEmpty(rutaAsset))
                _guion = AssetDatabase.LoadAssetAtPath<GuionHorneado>(rutaAsset);

            _mensaje = "Horneado guardado.";
        }
        finally
        {
            _ocupado = false;
        }

        CargarCampos();
        Previsualizar();
        Repaint();
    }

    private void DescartarPendiente()
    {
        if (_pendiente != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_pendiente)))
            UnityEngine.Object.DestroyImmediate(_pendiente);

        _pendiente = null;
        _hornoPendiente = null;
        _textoPendiente = null;
        _diff.Clear();
    }

    private void Validar()
    {
        if (string.IsNullOrEmpty(_rutaTexto)) { _mensaje = "No encuentro el .guion.txt."; return; }

        _errores = HorneadorDeGuion.ValidarTexto(_rutaTexto);
        _avisos.Clear();
        _diff.Clear();
        _mensaje = _errores.Count == 0
            ? "Sin errores de sintaxis ni de efectos."
            : $"{_errores.Count} error(es) de sintaxis o de efectos.";
        Repaint();
    }

    private void HornearCompleto()
    {        if (string.IsNullOrEmpty(_rutaTexto)) return;

        _ocupado = true;
        try
        {
            DescartarPendiente();
            GuionHorneado nuevo = HorneadorDeGuion.HornearParaRevisar(_rutaTexto, out _hornoPendiente);
            _errores = _hornoPendiente?.Errores ?? new List<string>();
            _avisos = _hornoPendiente?.Avisos ?? new List<string>();

            if (nuevo == null || _errores.Count > 0)
            {
                _mensaje = "El guion tiene errores; no se ha guardado.";
                return;
            }

            _hornoPendiente.GuardarResultado();
            _hornoPendiente.EscribirInforme();
            AssetDatabase.SaveAssets();

            string rutaAsset = AssetDatabase.GetAssetPath(nuevo);
            if (string.IsNullOrEmpty(rutaAsset))
            {
                rutaAsset = RutaAssetDelGuion();
                UnityEngine.Object.DestroyImmediate(nuevo);
            }
            if (!string.IsNullOrEmpty(rutaAsset))
                _guion = AssetDatabase.LoadAssetAtPath<GuionHorneado>(rutaAsset);

            _mensaje = "Horneado guardado.";
        }
        finally
        {
            _ocupado = false;
        }

        CargarCampos();
        Repaint();
    }

    private void Previsualizar()
    {
        if (_guion == null) return;

        _ocupado = true;
        string error;
        try
        {
            _fotos = PrevizDeGuion.Generar(_guion, out error);
        }
        finally
        {
            _ocupado = false;
        }

        DestruirTexturas();
        if (!string.IsNullOrEmpty(error)) _mensaje = error;
        else if (_fotos.Count == 0) _mensaje = "No se ha podido sacar ningún fotograma.";
        else
        {
            _mensaje = "Previz listo: un fotograma por plano.";
            if (_seleccion < 0) _seleccion = 0;
            CargarCampos();
        }
        Repaint();
    }

    private Texture2D TexturaDe(int indice)
    {
        foreach (PrevizDeGuion.Fotograma foto in _fotos)
        {
            if (foto.indice != indice || string.IsNullOrEmpty(foto.ruta) || !File.Exists(foto.ruta))
                continue;

            if (_texturas.TryGetValue(foto.ruta, out Texture2D cacheada)) return cacheada;

            var textura = new Texture2D(2, 2, TextureFormat.RGB24, false);
            textura.LoadImage(File.ReadAllBytes(foto.ruta));
            _texturas[foto.ruta] = textura;
            return textura;
        }
        return null;
    }

    private void DestruirTexturas()
    {
        foreach (Texture2D textura in _texturas.Values)
            if (textura != null) UnityEngine.Object.DestroyImmediate(textura);
        _texturas.Clear();
    }

    private string RutaAssetDelGuion()
    {
        string carpeta = Path.GetDirectoryName(_rutaTexto)?.Replace('\\', '/');
        string nombre = _guion?.nombre ?? _pendiente?.nombre;
        return string.IsNullOrEmpty(carpeta) || string.IsNullOrEmpty(nombre)
            ? null
            : $"{carpeta}/{nombre}_Horneado.asset";
    }

    private void AbrirTexto()
    {
        if (!string.IsNullOrEmpty(_rutaTexto)) EditorUtility.OpenWithDefaultApp(_rutaTexto);
    }

    private void AbrirEnLinea(int linea)
    {
        if (string.IsNullOrEmpty(_rutaTexto)) return;
        var texto = AssetDatabase.LoadAssetAtPath<TextAsset>(_rutaTexto);
        if (texto != null) AssetDatabase.OpenAsset(texto, linea);
    }

    private static string F(float valor) => valor.ToString("0.###", CultureInfo.InvariantCulture);
}
#endif
