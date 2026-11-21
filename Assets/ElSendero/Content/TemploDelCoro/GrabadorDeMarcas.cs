using System.Collections.Generic;
using System.IO;
using Kits.Puzzles;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Para poner a mano, a oído, lo que pasa en cada golpe de la canción del Templo del Coro: con la
/// canción sonando se pulsa una tecla en cada golpe y queda apuntado el segundo tal cual (en bruto) con
/// lo que toca; el montaje le quita lo que tarda la mano y lo lleva al medio tiempo más cercano. Al salir de Play se guarda en <see cref="Ruta"/> y el menú de
/// montar el templo lo usa en lugar de lo calculado.
///
/// Teclas (las de cada cosa sustituyen a las calculadas de esa cosa; lo que no se grabe se queda como está):
/// <list type="bullet">
/// <item><b>K</b>: un golpe del nivel: pilares en la nave del órgano y golpes de platillo en el escenario
/// del estribillo; sustituyen a los calculados.</item>
/// <item><b>L</b>: el ritmo de lo que se tira en el estribillo y en el final: una nota que cae a por el
/// jugador en cada pulsación (sustituyen a las notas calculadas de ese tramo).</item>
/// <item>Presentación del Tenor: <b>1</b> ¡PUM! · <b>2</b> cada nota de la escala del órgano · <b>3</b> cada «pam» del bombardino · <b>4</b> do, re, mi ·
/// <b>Q</b> «¡que vienen!» (los coristas de la sala salen corriendo).</item>
/// <item>Momentos de la letra: <b>5</b> «os presento a mi órgano» · <b>6</b> «¡qué pulmones tiene el bicho!» ·
/// <b>7</b> el fogonazo de «ya os lo he dicho» · <b>8</b> cada golpe de «suena el musical» (pan pan pan pan páan) ·
/// <b>9</b> «un regalito» (cuando se enseña) · <b>0</b> el «-ad» de «¡tomad!» (cuando se lanza).</item>
/// <item>Final: <b>T</b> «¡Rayos y truenos!» (vuelve la huida y se abre el fondo del escenario) · <b>Y</b> «¡Bravo, bravo!» ·
/// <b>U</b> «se me escapa una lagrimita» · <b>O</b> «¿Volveréis otro día?».</item>
/// <item><b>J</b>: un salto que hay que dar (el suelo se rompe ahí) · <b>H</b>: un dash que hay que dar ·
/// <b>M</b>: algo que cae del telar (saco de arena, luna o estrella de cartón) en cada «¡corred, corred!» · <b>P</b>: una semicorchea del «pi pi pi» de antes
/// del salto final (cada una rompe una losa delante) · <b>G</b>: un choque de regalo contra el escudo en las ráfagas de
/// los dos primeros regalitos (tras cada «¡tomad!»).</item>
/// <item><b>Retroceso</b> borra el último.</item>
/// </list>
/// Se puede grabar solo un tramo: empezando desde un punto de control, lo de antes de ese punto se conserva.
/// Mientras graba, la sombra no hace repetir. Si la canción vuelve atrás (reintento, «Repetir»), lo ya apuntado
/// se conserva; solo si se vuelve a pulsar una tecla en el tramo repetido se sustituye lo de esa tecla en ese tramo.
/// Cada pulsación se añade además, sin filtrar, a <see cref="RutaDeLasPulsaciones"/>, por si hay que recuperar algo.
/// Solo funciona en el editor; se activa marcando el componente.
/// Ver INC-1393.
/// </summary>
public sealed class GrabadorDeMarcas : MonoBehaviour
{
    /// <summary>Archivo de las marcas, desde la carpeta del proyecto.</summary>
    public const string Ruta = "Tools/Musica/Templo del Coro/marcas_grabadas.json";

    /// <summary>Registro de todas las pulsaciones tal cual (segundo, tecla, pasada), desde la carpeta del proyecto.</summary>
    public const string RutaDeLasPulsaciones = "Tools/Musica/Templo del Coro/pulsaciones.log";

    public const string Golpe = "GOLPE";
    public const string IntroPum = "INTRO_PUM";
    public const string IntroEscala = "INTRO_ESCALA";
    public const string IntroPam = "INTRO_PAM";
    public const string IntroDoReMi = "INTRO_DOREMI";
    public const string OsPresento = "OS_PRESENTO";
    public const string Pulmones = "PULMONES";
    public const string Fogonazo = "FOGONAZO";
    public const string Musical = "MUSICAL";
    public const string Regalito = "REGALITO";
    public const string Tomad = "TOMAD";
    public const string Lanza = "LANZA";
    public const string QueVienen = "QUE_VIENEN";
    public const string Salto = "SALTO";
    public const string Dash = "DASH";
    public const string DelTelar = "TELAR";
    public const string Semicorchea = "SEMICORCHEA";
    public const string Escudo = "ESCUDO";
    public const string Truenos = "TRUENOS";
    public const string Bravo = "BRAVO";
    public const string Lagrimita = "LAGRIMITA";
    public const string Volvereis = "VOLVEREIS";

    /// <summary>Hay un grabador activo: la prueba no hace repetir.</summary>
    public static bool Grabando { get; private set; }

    [SerializeField] private TempoClock reloj;

    [System.Serializable]
    public struct Marca
    {
        public float segundo;
        public string voz;
        /// <summary>El segundo es el de la pulsación, sin ajustar al medio tiempo.</summary>
        public bool enBruto;
    }

    [System.Serializable]
    public sealed class Archivo
    {
        public List<Marca> marcas = new List<Marca>();
    }

    /// <summary>Lee las marcas grabadas (vacío si no hay archivo).</summary>
    public static List<Marca> Leer()
    {
        string ruta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Ruta);
        if (!File.Exists(ruta)) return new List<Marca>();
        var archivo = JsonUtility.FromJson<Archivo>(File.ReadAllText(ruta));
        return archivo?.marcas ?? new List<Marca>();
    }

    private readonly Archivo _archivo = new Archivo();
    private double _ultimo = -1d;
    // Segundo de la canción en el que empezó a sonar esta sesión (desde el punto de control elegido).
    private double _desde = -1d;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Grabando = false;

    private void OnEnable()
    {
        Grabando = true;
        // Lo ya grabado se conserva: se graba encima solo lo que se vuelva a tocar.
        _archivo.marcas.Clear();
        _archivo.marcas.AddRange(Leer());
        Debug.Log("[Templo del Coro] Grabando marcas. K golpe del nivel · L lo que se tira (estribillo y final) · " +
                  "presentación: 1 PUM · 2 escala · 3 pam · 4 do-re-mi · Q que vienen · letra: 5 órgano · 6 pulmones · 7 fogonazo · " +
                  "8 musical · 9 regalito · 0 tomad · J salto · H dash · M lo que cae del telar · P semicorchea · G choque contra el escudo · final: T truenos · Y bravo · U lagrimita · O volveréis · Retroceso borra el último.");
    }

    private void OnDisable()
    {
        Grabando = false;
        _archivo.marcas.Sort((a, b) => a.segundo.CompareTo(b.segundo));
        string ruta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Ruta);
        File.WriteAllText(ruta, JsonUtility.ToJson(_archivo, true));
        Debug.Log($"[Templo del Coro] {_archivo.marcas.Count} marcas guardadas en {Ruta}. Vuelve a montar el templo para usarlas.");
    }

    private void Update()
    {
        if (reloj == null || !reloj.IsRunning) return;
        double t = reloj.SongTime;
        if (_desde < 0d) _desde = t;
        // La canción ha vuelto atrás (reintento): empieza otra pasada. Lo apuntado no se borra; se
        // sustituye lo de una tecla en el tramo repetido solo cuando se vuelve a pulsar esa tecla.
        if (t < _ultimo - 0.5d)
        {
            _pasada++;
            _repiteDesde = t;
            _repiteHasta = _ultimo;
            _tocadasEnLaPasada.Clear();
        }
        _ultimo = t;

        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado.backspaceKey.wasPressedThisFrame && _apuntadas.Count > 0)
        {
            _archivo.marcas.Remove(_apuntadas[_apuntadas.Count - 1]);
            _apuntadas.RemoveAt(_apuntadas.Count - 1);
        }
        if (teclado.kKey.wasPressedThisFrame) Apuntar(t, Golpe);
        if (teclado.lKey.wasPressedThisFrame) Apuntar(t, Lanza);
        if (teclado.qKey.wasPressedThisFrame) Apuntar(t, QueVienen);
        if (teclado.jKey.wasPressedThisFrame) Apuntar(t, Salto);
        if (teclado.hKey.wasPressedThisFrame) Apuntar(t, Dash);
        if (teclado.mKey.wasPressedThisFrame) Apuntar(t, DelTelar);
        if (teclado.pKey.wasPressedThisFrame) Apuntar(t, Semicorchea);
        if (teclado.gKey.wasPressedThisFrame) Apuntar(t, Escudo);
        if (teclado.tKey.wasPressedThisFrame) Apuntar(t, Truenos);
        if (teclado.yKey.wasPressedThisFrame) Apuntar(t, Bravo);
        if (teclado.uKey.wasPressedThisFrame) Apuntar(t, Lagrimita);
        if (teclado.oKey.wasPressedThisFrame) Apuntar(t, Volvereis);
        if (teclado.digit1Key.wasPressedThisFrame) Apuntar(t, IntroPum);
        if (teclado.digit2Key.wasPressedThisFrame) Apuntar(t, IntroEscala);
        if (teclado.digit3Key.wasPressedThisFrame) Apuntar(t, IntroPam);
        if (teclado.digit4Key.wasPressedThisFrame) Apuntar(t, IntroDoReMi);
        if (teclado.digit5Key.wasPressedThisFrame) Apuntar(t, OsPresento);
        if (teclado.digit6Key.wasPressedThisFrame) Apuntar(t, Pulmones);
        if (teclado.digit7Key.wasPressedThisFrame) Apuntar(t, Fogonazo);
        if (teclado.digit8Key.wasPressedThisFrame) Apuntar(t, Musical);
        if (teclado.digit9Key.wasPressedThisFrame) Apuntar(t, Regalito);
        if (teclado.digit0Key.wasPressedThisFrame) Apuntar(t, Tomad);
    }

    // Voces tocadas en esta sesión: lo grabado de ellas desde el arranque se sustituye; lo de las demás se conserva.
    private readonly HashSet<string> _tocadas = new HashSet<string>();
    // Voces tocadas desde la última vuelta atrás de la canción, y el tramo que esa vuelta repite.
    private readonly HashSet<string> _tocadasEnLaPasada = new HashSet<string>();
    private readonly List<Marca> _apuntadas = new List<Marca>();
    private int _pasada;
    private double _repiteDesde = -1d, _repiteHasta = -1d;

    private void Apuntar(double t, string voz)
    {
        if (_tocadas.Add(voz))
        {
            // Primera vez que se toca esta voz: lo grabado de ella desde donde empezó la sesión se
            // sustituye; lo de antes (otro tramo grabado otro día) se conserva.
            float desde = (float)_desde - 0.3f;
            _archivo.marcas.RemoveAll(m => m.voz == voz && m.segundo >= desde);
        }
        if (_tocadasEnLaPasada.Add(voz) && _repiteHasta > 0d)
        {
            // Primera vez que se toca esta voz en una pasada repetida: se sustituye lo suyo en el tramo repetido.
            float de = (float)_repiteDesde - 0.3f, a = (float)_repiteHasta + 0.3f;
            _archivo.marcas.RemoveAll(m => m.voz == voz && m.segundo >= de && m.segundo <= a);
        }
        var marca = new Marca { segundo = (float)t, voz = voz, enBruto = true };
        _archivo.marcas.Add(marca);
        _apuntadas.Add(marca);
        Debug.Log($"[Templo del Coro] {voz} en {marca.segundo:0.000} s");
        string registro = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", RutaDeLasPulsaciones);
        File.AppendAllText(registro, $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{t:0.000}\t{voz}\tpasada {_pasada}\n");
    }
#endif
}
