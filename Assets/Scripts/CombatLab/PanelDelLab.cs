using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Panel único del LAB. Plegado solo enseña una pestaña pequeña («LAB · Tab») arriba a la derecha
/// y los avisos (♪ canción, «copiado»…), sin tapar el HUD del juego. Con Tab o Select (mando) se
/// abre: el juego se pausa, sale el ratón y se ven las pestañas de cada sistema del laboratorio
/// (ISeccionDelLab). La primera vez de cada sesión se abre solo en la Ayuda (bienvenida).
public sealed class PanelDelLab : MonoBehaviour
{
    private const string PestanaInicial = "Ayuda";
    private const float AnchoMax = 860f;
    private const float AltoMax = 600f;

    private static PanelDelLab s_actual;
    private static bool s_bienvenidaVista;
    private static string s_aviso;
    private static float s_avisoHasta;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_actual = null;
        s_bienvenidaVista = false;
        s_aviso = null;
    }
#endif

    private readonly List<ISeccionDelLab> _secciones = new List<ISeccionDelLab>();
    private int _pestana;
    private bool _abierto;
    private bool _poseeModoUI;
    private bool _cursorVisibleAntes;
    private CursorLockMode _cursorAntes;
    private Vector2 _scroll;

    public static bool Abierto => s_actual != null && s_actual._abierto;

    /// Mensaje corto arriba a la derecha, también con el panel plegado.
    public static void Aviso(string texto, float segundos = 3f)
    {
        s_aviso = texto;
        s_avisoHasta = Time.unscaledTime + segundos;
    }

    /// Abre el panel en la pestaña con ese título.
    public static void AbrirEn(string titulo)
    {
        if (s_actual == null) return;
        int i = s_actual._secciones.FindIndex(s => s.Titulo == titulo);
        if (i >= 0) s_actual._pestana = i;
        s_actual.Abrir(true);
    }

    /// Cierra el panel (p. ej. antes de un viaje rápido).
    public static void Cerrar()
    {
        if (s_actual != null) s_actual.Abrir(false);
    }

    private void OnEnable() => s_actual = this;

    private void OnDisable()
    {
        Abrir(false);
        if (s_actual == this) s_actual = null;
    }

    private IEnumerator Start()
    {
        // Las secciones se añaden en el arranque del laboratorio; se recogen un par de fotogramas después.
        yield return null;
        yield return null;
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is ISeccionDelLab s && !_secciones.Contains(s)) _secciones.Add(s);
        _secciones.Sort((a, b) => a.Orden.CompareTo(b.Orden));

        if (!s_bienvenidaVista)
        {
            s_bienvenidaVista = true;
            AbrirEn(PestanaInicial);
        }
    }

    private void Update()
    {
        var teclado = Keyboard.current;
        var mando = Gamepad.current;
        bool alternar = (teclado != null && teclado.tabKey.wasPressedThisFrame)
                        || (mando != null && mando.selectButton.wasPressedThisFrame);
        bool cerrar = _abierto && ((teclado != null && teclado.escapeKey.wasPressedThisFrame)
                                   || (mando != null && mando.buttonEast.wasPressedThisFrame));
        if (cerrar) { Abrir(false); return; }
        if (!alternar) return;

        // Con otra interfaz del juego abierta (menú de Start, diálogos, grimorio) no se abre.
        var entrada = Core.PlayerInputManager.Instance;
        if (!_abierto && entrada != null && entrada.IsInUIMode) return;
        Abrir(!_abierto);
    }

    private void Abrir(bool abrir)
    {
        if (_abierto == abrir) return;
        _abierto = abrir;
        var entrada = Core.PlayerInputManager.Instance;
        if (abrir)
        {
            if (entrada != null) { entrada.PushUIMode(); _poseeModoUI = true; }
            TimeScaleArbiterService.Request(this, 0f);
            _cursorVisibleAntes = Cursor.visible;
            _cursorAntes = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (_poseeModoUI && entrada != null) entrada.PopUIMode();
            _poseeModoUI = false;
            TimeScaleArbiterService.Release(this);
            Cursor.visible = _cursorVisibleAntes;
            Cursor.lockState = _cursorAntes;
        }
    }

    private void OnGUI()
    {
        EstiloDelLab.Preparar();
        if (!_abierto)
        {
            if (GUI.Button(new Rect(Screen.width - 150f, 12f, 138f, 30f), "LAB  ·  Tab", EstiloDelLab.Boton))
                Abrir(true);
            DibujarAviso(new Rect(Screen.width - 372f, 48f, 360f, 30f));
            return;
        }

        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, EstiloDelLab.Fondo);
        float ancho = Mathf.Min(AnchoMax, Screen.width - 40f);
        float alto = Mathf.Min(AltoMax, Screen.height - 40f);
        var ventana = new Rect((Screen.width - ancho) * 0.5f, (Screen.height - alto) * 0.5f, ancho, alto);

        GUILayout.BeginArea(ventana, EstiloDelLab.Ventana);
        GUILayout.BeginHorizontal();
        GUILayout.Label("LAB · El Sendero de las Estrellas", EstiloDelLab.Cabecera);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Cerrar  ✕", EstiloDelLab.Boton, GUILayout.Width(110f))) Abrir(false);
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < _secciones.Count; i++)
            if (GUILayout.Button(_secciones[i].Titulo, i == _pestana ? EstiloDelLab.PestanaActiva : EstiloDelLab.Pestana))
            {
                if (_pestana != i) _scroll = Vector2.zero;
                _pestana = i;
            }
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);

        _scroll = GUILayout.BeginScrollView(_scroll);
        if (_pestana < _secciones.Count && _secciones[_pestana] != null) _secciones[_pestana].Dibujar();
        GUILayout.EndScrollView();

        GUILayout.Label("Tab / Select: cerrar · El juego está en pausa mientras el panel está abierto.", EstiloDelLab.Nota);
        GUILayout.EndArea();

        DibujarAviso(new Rect(ventana.xMax - 360f, ventana.y - 36f, 360f, 30f));
    }

    private static void DibujarAviso(Rect r)
    {
        if (string.IsNullOrEmpty(s_aviso) || Time.unscaledTime > s_avisoHasta) return;
        GUI.Label(r, s_aviso, EstiloDelLab.Aviso);
    }
}
