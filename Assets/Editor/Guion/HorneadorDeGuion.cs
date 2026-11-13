#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Hornea un guion de texto (ver GuionTexto) en un GuionHorneado que se reproduce tal cual.
///
/// Qué hace, en orden:
///  1. Lee el texto y resuelve el reparto (con su catálogo de animaciones) y los puntos.
///  2. Reparte el tiempo: el hilo principal en orden, cada frase con la duración de su voz y la
///     pausa justa, y las pistas de cada NPC ancladas a las etiquetas del hilo.
///  3. Calcula los caminos sobre el NavMesh, y cada caminata comprueba a los que ya están en el
///     escenario: si va a chocar con alguien quieto, le rodea; si se cruza con alguien andando,
///     espera. Nadie se atasca porque en Play no hay nada que decidir.
///  4. Resuelve cada plano con las posiciones ya conocidas: dónde poner la cámara para ver la cara
///     del que importa sin que nadie se meta en medio, respetando el eje y sin saltos.
///  5. Escribe el asset, el parte (informe.md) y los dibujos sobre la planta (recorridos.svg).
public static partial class HorneadorDeGuion
{
    /// Resumen del último horneado (lo lee el buzón de órdenes).
    public static string UltimoResumen;

    public const string RutaPrologo = "Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt";

    [MenuItem("El Sendero/Guion/2. Hornear el guion del prólogo", priority = 2)]
    public static void MenuPrologo() => Hornear(RutaPrologo, storyboard: false);

    [MenuItem("El Sendero/Guion/3. Hornear el prólogo + storyboard", priority = 3)]
    public static void MenuPrologoConStoryboard() => Hornear(RutaPrologo, storyboard: true);

    [MenuItem("El Sendero/Guion/4. Hornear Batalla Final (Intro)", priority = 10)]
    public static void MenuBatallaFinal() => Hornear("Assets/_SEQUENCES/Guiones/BatallaFinal_Intro.guion.txt", storyboard: false);

    [MenuItem("El Sendero/Guion/5. Hornear Final Parte 1 (El Sacrificio)", priority = 12)]
    public static void MenuFinalParte1() => Hornear("Assets/_SEQUENCES/Guiones/Final_01_ElSacrificio.guion.txt", storyboard: false);

    [MenuItem("El Sendero/Guion/6. Hornear Final Parte 2 (El Río)", priority = 13)]
    public static void MenuFinalParte2() => Hornear("Assets/_SEQUENCES/Guiones/Final_02_ElRio.guion.txt", storyboard: false);

    [MenuItem("El Sendero/Guion/7. Hornear Mañana Después (Capítulo 2)", priority = 14)]
    public static void MenuMananaDespues() => Hornear("Assets/_SEQUENCES/Guiones/Cap2_02_MananaDespues.guion.txt", storyboard: false);

    public static GuionHorneado Hornear(string rutaTexto, bool storyboard)
    {
        var texto = AssetDatabase.LoadAssetAtPath<TextAsset>(rutaTexto);
        string fuente = texto != null ? texto.text : (File.Exists(rutaTexto) ? File.ReadAllText(rutaTexto) : null);
        if (fuente == null)
        {
            EditorUtility.DisplayDialog("Hornear guion", $"No encuentro el guion en '{rutaTexto}'.", "Vale");
            return null;
        }
        var guion = GuionTexto.Leer(fuente);
        var horno = new Horno(guion, rutaTexto);
        GuionHorneado resultado = null;
        try
        {
            resultado = horno.Hornear(storyboard);
        }
        catch (Exception ex)
        {
            var realEx = (ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null) ? tie.InnerException : ex;
            horno.Error("El horneado se ha parado: " + realEx.Message + "\n" + realEx.StackTrace);
        }
        finally { horno.LiberarPoses(); EditorUtility.ClearProgressBar(); }
        horno.EscribirInforme();
        string resumen = horno.Resumen();
        UltimoResumen = resumen;
        if (horno.Errores.Count > 0) Debug.LogError("[Guion] " + resumen);
        else if (horno.Avisos.Count > 0) Debug.LogWarning("[Guion] " + resumen);
        else Debug.Log("[Guion] " + resumen);
        return resultado;
    }
}

/// Lo que se sabe de cada animación de un personaje (del catálogo de su Animator).
internal struct InfoDeEstado
{
    public float largo;
    public bool bucle;
    public bool cuerpoEntero; // vive en el Base Layer
}

/// Un personaje mientras se hornea.
internal sealed class ActorH
{
    public string alias, id;
    public GuionTexto.Reparto reparto;
    public GameObject prefab;
    public float altura = 1.7f;
    public float ojos = -1f;      // altura de los ojos medida en el esqueleto (−1 = no se sabe)
    public Dictionary<string, InfoDeEstado> estados = new();
    public Vector3 spawnPos;
    public float spawnRumbo;
    public bool tieneSpawn;

    public readonly List<ClaveDePosicion> pos = new();
    public readonly List<ClaveDeMirada> miradas = new();
    public readonly List<ClaveDeMirada> cabezas = new();   // hacia dónde vuelve la cabeza (sin el cuerpo)
    public readonly List<OrdenDeAnimacion> anim = new();
    public readonly List<OrdenDeCara> caras = new();
    public readonly List<(float t, bool visible)> visibilidad = new();
    public readonly List<float> comandos = new();             // instantes en que recibe órdenes
    public readonly List<(float t0, string con, float cada, int linea)> charlas = new();
    public readonly List<(float t0, float t1)> habla = new();
    public bool ocultoAlEmpezar;
    public string bucle;          // bucle en curso (en el instante del último comando procesado)
    public NPCAmbientActivity sentadoCon; // actividad del asiento en el que está sentado (sienta/levanta)
    public Vector3 dePie;         // donde estaba de pie antes de sentarse: ahí vuelve al levantarse
    public bool bucleDeCuerpo;    // ese bucle es de cuerpo entero
    public string poseAlta;       // pose de medio cuerpo puesta encima de un bucle de cuerpo entero

    // Pista propia
    public readonly List<GuionTexto.Orden> pista = new();
    public int iPista;
    public float finPistaAnterior;

    public float AlturaDeOjos => ojos > 0f ? ojos : altura * 0.85f;
    /// Alto de la cabeza (barbilla a coronilla). En los chibis los ojos quedan en el 30 % bajo.
    public float Cabeza => Mathf.Clamp((altura - AlturaDeOjos) / 0.7f, 0.25f, altura * 0.6f);
    /// Punto que se encuadra en planos cortos: algo por encima de los ojos (centro de la cara).
    public float AlturaDeCara => AlturaDeOjos + 0.25f * (altura - AlturaDeOjos);
    /// Radio con el que estorba a la cámara (los chibis tienen la cabeza muy ancha).
    public float Radio => Mathf.Max(0.3f, Cabeza * 0.5f);

    public Vector3 PosEn(float t) => pos.Count == 0 ? spawnPos : Interpolar.Posicion(pos, t);
    public Vector3 VelEn(float t) => Interpolar.Velocidad(pos, t);
    public readonly List<(float t0, float t1)> deslizando = new();

    /// Un tramo en volandas («encima»): el actor va sobre los que lo llevan, a esa altura sobre ellos.
    public sealed class Encima
    {
        public float t0, t1, altura, entra;
        public List<ActorH> debajo;
    }
    public readonly List<Encima> encima = new();
    /// Lo llevan en volandas: ni anda ni estorba a los que andan por debajo.
    public bool LlevadoEn(float t) => encima.Any(e => t >= e.t0 && t < e.t1);

    /// Un tramo de la mano de otro («de_la_mano»): va a su lado, al paso de quien le guía.
    public sealed class DeLaMano
    {
        public float t0, t1, lado;
        public ActorH guia;
        public bool hecho;   // su camino ya está calculado (al soltarse)
    }
    public readonly List<DeLaMano> deLaMano = new();
    /// Va de la mano de alguien: su camino es el de quien le guía y no estorba a los demás.
    public bool DeLaManoEn(float t) => deLaMano.Any(e => t >= e.t0 && t < e.t1);
    public bool DeslizandoEn(float t) => deslizando.Any(d => t >= d.t0 && t < d.t1);
    public bool AndandoEn(float t) => VelEn(t).sqrMagnitude > 0.08f * 0.08f && !DeslizandoEn(t);

    public bool VisibleEn(float t)
    {
        bool v = !ocultoAlEmpezar;
        foreach (var c in visibilidad) { if (c.t <= t) v = c.visible; else break; }
        return v;
    }

    /// Deja la línea de posiciones terminada en t (lo que hubiera planeado después se olvida).
    public void Cortar(float t)
    {
        if (pos.Count == 0) { pos.Add(new ClaveDePosicion(0f, spawnPos)); }
        Vector3 aqui = PosEn(t);
        pos.RemoveAll(k => k.t > t + 1e-4f);
        if (pos[pos.Count - 1].t < t - 1e-4f || (pos[pos.Count - 1].p - aqui).sqrMagnitude > 1e-6f)
            pos.Add(new ClaveDePosicion(t, aqui));
    }

    public void Teletransporte(float t, Vector3 p)
    {
        // Una colocación inicial define el origen; no crea un viaje desde una posición sin resolver.
        if (t <= 0.001f)
        {
            pos.Clear();
            pos.Add(new ClaveDePosicion(0f, p));
            spawnPos = p;
            return;
        }
        Cortar(t);
        pos.Add(new ClaveDePosicion(t + 0.001f, p));
    }

    /// Rumbo (grados) en el instante t, tal como lo verá la cámara.
    public float RumboEn(float t, Dictionary<string, ActorH> porAlias)
    {
        Vector3 v = VelEn(t);
        if (v.sqrMagnitude > 0.01f && !DeslizandoEn(t)) return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        // ¿Ha andado hace un momento? Se queda mirando hacia donde iba, salvo mirada nueva.
        ClaveDeMirada? m = null;
        foreach (var k in miradas) { if (k.t <= t + 1e-3f) m = k; else break; }
        float ultimoPaso = -1f;
        for (int i = pos.Count - 1; i > 0; i--)
        {
            if (pos[i].t > t) continue;
            // Un deslizamiento no cuenta como paso: no gira el cuerpo (igual que en el juego, donde
            // deslizarse no es andar y manda la mirada).
            if ((pos[i].p - pos[i - 1].p).sqrMagnitude > 1e-4f && pos[i].t - pos[i - 1].t > 0.02f && !DeslizandoEn(pos[i].t - 1e-3f))
            {
                ultimoPaso = pos[i].t;
                if (m == null || m.Value.t < ultimoPaso)
                {
                    Vector3 d = pos[i].p - pos[i - 1].p;
                    return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
                break;
            }
        }
        if (m != null)
        {
            var k = m.Value;
            Vector3 aqui = PosEn(t);
            switch (k.tipo)
            {
                case TipoDeMirada.Rumbo: return k.rumbo;
                case TipoDeMirada.Punto:
                {
                    Vector3 d = k.punto - aqui;
                    if (d.sqrMagnitude > 0.01f) return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    break;
                }
                case TipoDeMirada.Actor:
                    foreach (var otro in porAlias.Values)
                        if (otro.id == k.actor)
                        {
                            Vector3 d = otro.PosEn(t) - aqui;
                            if (d.sqrMagnitude > 0.01f) return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        }
                    break;
            }
        }
        return spawnRumbo;
    }

    public void Mirar(float t, ClaveDeMirada m)
    {
        m.t = t;
        miradas.RemoveAll(k => k.t >= t - 1e-4f);
        miradas.Add(m);
    }

    /// La cabeza se vuelve hacia m desde t (y deja de valer lo que hubiera pedido después).
    public void GirarCabeza(float t, ClaveDeMirada m)
    {
        m.t = t;
        cabezas.RemoveAll(k => k.t >= t - 1e-4f);
        cabezas.Add(m);
    }

    /// «A nadie»: la cabeza vuelve al frente.
    public static ClaveDeMirada CabezaAlFrente => new() { tipo = TipoDeMirada.Rumbo };

    public void Animar(float t, TipoDeAnimacion tipo, string estado = null, bool congelar = false, float fundido = 0f, bool alFinal = false, string voz = null)
    {
        // Un bucle o el reposo ya cierran el gesto de cuerpo entero que hubiera.
        if (tipo == TipoDeAnimacion.Bucle || tipo == TipoDeAnimacion.Reposo)
            anim.RemoveAll(x => x.tipo == TipoDeAnimacion.FinDeGesto && x.t > t - 1e-4f);
        anim.Add(new OrdenDeAnimacion { t = t, tipo = tipo, estado = estado, congelar = congelar, fundido = fundido, alFinal = alFinal, voz = voz });
    }
}

internal sealed partial class Horno
{
    private readonly GuionTexto _g;
    private readonly string _rutaTexto;
    public readonly List<string> Errores = new();
    public readonly List<string> Avisos = new();

    private Scene _escena;
    private SequenceStage _stage;
    private readonly Dictionary<string, ActorH> _actores = new();   // por alias
    private readonly Dictionary<string, Vector3> _puntos = new();
    /// Puntos que son un asiento (objeto con NPCWorldPoint): ver HornoAsientos.
    private readonly Dictionary<string, Asiento> _asientos = new();
    private readonly Dictionary<string, float> _etiquetas = new();
    private readonly List<LineaHorneada> _lineas = new();
    private readonly List<EfectoHorneado> _efectos = new();
    private readonly List<PlanoPedido> _planosPedidos = new();
    private readonly Dictionary<string, (float tiempo, string gesto)> _ultimasReaccionesPorActor = new();
    /// La cara con que se dice cada frase y el último gesto que el horneado eligió para cada actor:
    /// con ellos se elige el gesto de las frases que no traen uno (GestoSegunLaFrase).
    private readonly Dictionary<LineaHorneada, NPCEmotion> _caraDeLinea = new();
    private readonly Dictionary<string, (float tiempo, string gesto)> _ultimoGestoElegido = new();
    private readonly HashSet<(string actor, float t)> _cabezaPedida = new();   // frases con «mira=» o «cabeza=»
    private GuionHorneado _salida;
    private float _fin;
    private string _carpetaSalida;

    private sealed class PlanoPedido
    {
        public GuionTexto.Orden orden;
        public float t0;
    }

    public Horno(GuionTexto g, string rutaTexto)
    {
        _g = g;
        _rutaTexto = rutaTexto;
        foreach (var e in g.errores) Errores.Add(e);
    }

    public void Error(string e) => Errores.Add(e);
    public void Aviso(string a) => Avisos.Add(a);

    public string Resumen()
        => $"Guion «{_g.nombre}»: {(_salida != null ? $"{_salida.duracion:0.0} s, {_lineas.Count} frases, {_salida.planos.Count} planos" : "sin hornear")}. " +
           $"{Errores.Count} error(es), {Avisos.Count} aviso(s). Parte en {(_carpetaSalida ?? "(sin carpeta)")}/informe.md";

    // ── Hornear ─────────────────────────────────────────────────────────────────────────────

    public GuionHorneado Hornear(bool storyboard)
    {
        if (Errores.Count > 0) return null;
        EditorUtility.DisplayProgressBar("Hornear guion", "Escenario…", 0.05f);
        _escena = CapturaDeEscenario.AbrirEscena(_g.escena);
        EscenaDelGuion = _escena;
        if (!_escena.IsValid() || !_escena.isLoaded) { Error($"No puedo abrir la escena '{_g.escena}'."); return null; }
        _stage = CapturaDeEscenario.BuscarStage(_escena);
        if (!string.IsNullOrEmpty(_g.secuencia))
            foreach (var raiz in _escena.GetRootGameObjects())
                foreach (var player in raiz.GetComponentsInChildren<SequencePlayer>(true))
                    if (player.Definition != null && AssetDatabase.GetAssetPath(player.Definition) == _g.secuencia)
                        _stage = player.GetComponent<SequenceStage>() ?? _stage;
        Physics.SyncTransforms();
        _carpetaSalida = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, CapturaDeEscenario.CarpetaDeEscena(_g.escena, _g.zona), _g.nombre);
        Directory.CreateDirectory(_carpetaSalida);

        if (!string.IsNullOrEmpty(_g.prepara))
        {
            var clase = TypeCache.GetTypesDerivedFrom<object>().FirstOrDefault(t => t.FullName == _g.prepara);
            var metodo = clase?.GetMethod("Asegurar", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (metodo == null) Error($"PREPARA {_g.prepara}: no existe esa clase con un Asegurar() estático");
            else
            {
                metodo.Invoke(null, null);
                if (!_escena.IsValid() || !_escena.isLoaded)
                {
                    _escena = SceneManager.GetSceneByName(_g.escena);
                    if (!_escena.IsValid() || !_escena.isLoaded)
                        _escena = CapturaDeEscenario.AbrirEscena(_g.escena);
                    EscenaDelGuion = _escena;
                }
                _stage = CapturaDeEscenario.BuscarStage(_escena);
                if (!string.IsNullOrEmpty(_g.secuencia))
                    foreach (var raiz in _escena.GetRootGameObjects())
                        foreach (var player in raiz.GetComponentsInChildren<SequencePlayer>(true))
                            if (player.Definition != null && AssetDatabase.GetAssetPath(player.Definition) == _g.secuencia)
                                _stage = player.GetComponent<SequenceStage>() ?? _stage;
            }
        }
        PrepararReparto();
        PrepararPuntos();
        OcultarActoresDeEscena();
        if (Errores.Count > 0) return null;

        EditorUtility.DisplayProgressBar("Hornear guion", "Tiempos y caminos…", 0.2f);
        Repartir();
        if (Errores.Count > 0) return null;
        LlevarEncima();
        LlevarDeLaMano();

        EditorUtility.DisplayProgressBar("Hornear guion", "Actuación…", 0.5f);
        Rematar();
        FinalizarProps();
        BandasDeCinematica();

        EditorUtility.DisplayProgressBar("Hornear guion", "Cámaras…", 0.6f);
        _salida = ScriptableObject.CreateInstance<GuionHorneado>();
        ResolverPlanos();

        EditorUtility.DisplayProgressBar("Hornear guion", "Comprobando…", 0.8f);
        Validar();

        EditorUtility.DisplayProgressBar("Hornear guion", "Guardando…", 0.85f);
        var asset = Guardar();
        DibujarRecorridos();
        if (storyboard)
        {
            EditorUtility.DisplayProgressBar("Hornear guion", "Storyboard…", 0.9f);
            Storyboard(asset);
        }
        return asset;
    }

    // ── Reparto y puntos ────────────────────────────────────────────────────────────────────

    /// ¿Lo encontrará la secuencia en juego? Por el escenario (SequenceStage) o por el registro de
    /// NPCs, que los conoce por su persistenceId o, si no tienen, por el nombre del objeto.
    private bool SeEncuentraEnJuego(string id)
    {
        if (_stage != null && _stage.Actors != null && _stage.Actors.Any(x => x.target != null && x.id?.Trim() == id)) return true;
        foreach (var raiz in _escena.GetRootGameObjects())
            foreach (var npc in raiz.GetComponentsInChildren<Game.NPC.NPCBehaviourManagerV2>(true))
                if (npc.PersistenceId == id || (string.IsNullOrEmpty(npc.PersistenceId) && npc.gameObject.name == id)) return true;
        return false;
    }

    private void PrepararReparto()
    {
        var spawns = CapturaDeEscenario.RecogerSpawns(_escena);
        foreach (var r in _g.reparto)
        {
            if (_actores.ContainsKey(r.alias)) { Error($"línea {r.linea}: el alias '{r.alias}' está repetido"); continue; }
            var a = new ActorH { alias = r.alias, id = r.id, reparto = r };
            var s = spawns.FirstOrDefault(x => x.persistenceId == r.id);
            if (s.persistenceId != null)
            {
                a.tieneSpawn = true;
                a.spawnPos = Suelo(s.pos);
                a.spawnRumbo = s.rumbo;
                a.prefab = s.prefab;
            }
            else Aviso($"línea {r.linea}: '{r.id}' no tiene punto de aparición en {_g.escena}; necesita un «en» al principio.");
            if (s.persistenceId == null && r.id != SequenceActor.PlayerId && !SeEncuentraEnJuego(r.id))
                Aviso($"línea {r.linea}: '{r.id}' no está en el escenario de la secuencia ni hay en {_g.escena} un NPC que se registre con ese id: " +
                      "en juego no saldrá (salvo que lo traiga otra escena o el grafo). Ponlo en el SequenceStage con ese id.");
            // «prefab=Assets/…prefab»: medidas y animaciones de un personaje sin punto de aparición en la
            // escena (Will, o un NPC que llega por el grafo narrativo).
            if (r.opciones.TryGetValue("prefab", out var rutaPrefab))
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
                if (pf != null) a.prefab = pf;
                else Error($"línea {r.linea}: no encuentro el prefab '{rutaPrefab}'");
            }
            if (a.prefab != null)
            {
                foreach (var e in CapturaDeEscenario.Catalogo(a.prefab))
                    if (!a.estados.ContainsKey(e.estado))
                        a.estados[e.estado] = new InfoDeEstado { largo = e.largo, bucle = e.bucle, cuerpoEntero = e.capa == 0 };
                var m = CapturaDeEscenario.Medidas(a.prefab);
                a.altura = m.altura; a.ojos = m.ojos;
            }
            if (r.opciones.ContainsKey("altura")) a.altura = GuionTexto.Num(r.opciones["altura"], a.altura);
            if (r.opciones.ContainsKey("ojos")) a.ojos = GuionTexto.Num(r.opciones["ojos"], a.ojos);
            if (a.ojos > a.altura) a.ojos = a.altura * 0.85f;
            a.ocultoAlEmpezar = r.opciones.ContainsKey("oculto");
            a.pos.Add(new ClaveDePosicion(0f, a.spawnPos));
            _actores[r.alias] = a;
        }
        foreach (var o in _g.pistas)
        {
            if (!_actores.TryGetValue(o.actor, out var a)) { Error($"línea {o.linea}: '{o.actor}' no está en el REPARTO"); continue; }
            a.pista.Add(o);
        }
    }

    private void PrepararPuntos()
    {
        foreach (var p in _g.puntos)
        {
            if (_puntos.ContainsKey(p.nombre)) { Error($"línea {p.linea}: el punto '{p.nombre}' está repetido"); continue; }
            if (p.marca != null)
            {
                if (!BuscarMarca(p.marca, out var pos)) { Error($"línea {p.linea}: no encuentro la marca u objeto '{p.marca}' en la escena"); continue; }
                _puntos[p.nombre] = pos;
                if (_asientos.TryGetValue(p.marca, out var asiento)) _asientos[p.nombre] = asiento;
            }
            else
            {
                var v = new Vector3(p.x, p.tieneAltura ? p.y : 0f, p.z);
                _puntos[p.nombre] = p.tieneAltura ? v : Suelo(v);
            }
        }
    }

    private bool BuscarMarca(string nombre, out Vector3 pos)
    {
        pos = default;
        var m = _stage != null ? _stage.Marks.FirstOrDefault(x => x.target != null && x.name.Trim() == nombre) : default;
        if (m.target != null) { pos = m.target.position; return true; }
        foreach (var raiz in _escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre)
                {
                    pos = t.position;
                    // Una silla, un banco…: si el objeto tiene su punto de sentarse (NPCWorldPoint, el
                    // mismo que usan el jugador y los NPCs, ajustado a mano al filo del asiento), el
                    // punto es ese y no el centro del mueble. Sentarse siempre ha dado guerra: así
                    // se ajusta una vez en el prefab y vale para todo.
                    var punto = t.GetComponentInChildren<NPCWorldPoint>(true);
                    if (punto != null)
                    {
                        pos = punto.InteractionPosition;
                        _asientos[nombre] = Asiento.De(punto);
                    }
                    return true;
                }
        return false;
    }

    /// Un punto por nombre: punto del guion, marca de la escena, o «x,z» escrito a mano.
    private bool Punto(string nombre, int linea, out Vector3 p)
    {
        p = default;
        if (string.IsNullOrEmpty(nombre)) { Error($"línea {linea}: falta un punto"); return false; }
        if (_puntos.TryGetValue(nombre, out p)) return true;
        var partes = nombre.Split(';');
        if (partes.Length == 2 && GuionTexto.EsNumero(partes[0]) && GuionTexto.EsNumero(partes[1]))
        {
            p = Suelo(new Vector3(GuionTexto.Num(partes[0], 0), 0, GuionTexto.Num(partes[1], 0)));
            return true;
        }
        if (BuscarMarca(nombre, out p)) { _puntos[nombre] = p; return true; }
        Error($"línea {linea}: no conozco el punto '{nombre}' (ni en PUNTOS ni como marca de la escena)");
        return false;
    }

    /// Destino de una mirada: un actor o un punto.
    private bool Objetivo(string nombre, int linea, out ClaveDeMirada m)
    {
        m = default;
        if (string.IsNullOrEmpty(nombre)) return false;
        if (_actores.TryGetValue(nombre, out var otro)) { m.tipo = TipoDeMirada.Actor; m.actor = otro.id; return true; }
        if (GuionTexto.EsNumero(nombre)) { m.tipo = TipoDeMirada.Rumbo; m.rumbo = GuionTexto.Num(nombre, 0); return true; }
        if (Punto(nombre, linea, out var p)) { m.tipo = TipoDeMirada.Punto; m.punto = p; return true; }
        return false;
    }

    // ── Reparto del tiempo ──────────────────────────────────────────────────────────────────

    private sealed class Evento
    {
        public float t;
        public GuionTexto.Orden orden;
        public ActorH dePista;      // si viene de una pista
        public bool bloquea;        // si el cursor del hilo espera a que acabe
        public int indiceHilo = -1;
    }

    private float _cursor;
    private float _inicioAnterior;
    private float _ancla;   // inicio de la última orden que no es «+s»: los «+s» cuentan desde aquí
    private float _finAnterior;
    private GuionTexto.Orden _bloqueanteAnterior;
    private string _ultimoHablante;

    private void Repartir()
    {
        var pendientes = new List<Evento>();
        int iHilo = 0;
        bool puedoAlimentar = true;
        _cursor = 0f;

        while (true)
        {
            // Alimentar el hilo hasta la primera orden que bloquea (su duración aún no se sabe).
            while (puedoAlimentar && iHilo < _g.hilo.Count)
            {
                var o = _g.hilo[iHilo];
                var e = new Evento { orden = o, indiceHilo = iHilo };
                iHilo++;
                if (o.verbo == "etiqueta")
                {
                    if (_etiquetas.ContainsKey(o.defineEtiqueta)) Error($"línea {o.linea}: la etiqueta @{o.defineEtiqueta} está repetida");
                    _etiquetas[o.defineEtiqueta] = _cursor;
                    continue;
                }
                switch (o.arranque)
                {
                    case GuionTexto.Arranque.Seguido:
                        e.t = _cursor + PausaAntes(o); e.bloquea = true; break;
                    case GuionTexto.Arranque.ALaVez:
                        e.t = _inicioAnterior; break;
                    case GuionTexto.Arranque.ALaVezYEspera:
                        e.t = _inicioAnterior; e.bloquea = true; break;
                    case GuionTexto.Arranque.TrasInicio:
                        e.t = _ancla + o.desfase; break;
                    case GuionTexto.Arranque.Solape:
                        e.t = Mathf.Max(_inicioAnterior, _cursor - o.desfase); e.bloquea = true; break;
                    case GuionTexto.Arranque.DesdeEtiqueta:
                        if (!_etiquetas.TryGetValue(o.etiqueta, out float te)) { Error($"línea {o.linea}: la etiqueta @{o.etiqueta} no está definida antes"); continue; }
                        e.t = te + o.desfase; break;
                }
                if (o.verbo == "espera")
                {
                    float s = o.args.Count > 0 ? GuionTexto.Num(o.args[0], 0f) : 0f;
                    _cursor = Mathf.Max(_cursor, e.t + s);
                    _inicioAnterior = _ancla = _cursor;
                    _ultimoHablante = null;
                    _bloqueanteAnterior = o;
                    continue;
                }
                _inicioAnterior = e.t;
                if (o.arranque != GuionTexto.Arranque.TrasInicio) _ancla = e.t;
                pendientes.Add(e);
                if (e.bloquea) { puedoAlimentar = false; break; }
            }

            // Alimentar las pistas cuyo arranque ya se conoce.
            foreach (var a in _actores.Values)
            {
                if (a.iPista >= a.pista.Count || pendientes.Any(p => p.dePista == a)) continue;
                var o = a.pista[a.iPista];
                float? t = null;
                switch (o.arranque)
                {
                    case GuionTexto.Arranque.Absoluto: t = o.desfase; break;
                    case GuionTexto.Arranque.TrasAnteriorDePista: t = a.finPistaAnterior + o.desfase; break;
                    case GuionTexto.Arranque.DesdeEtiqueta:
                        if (_etiquetas.TryGetValue(o.etiqueta, out float te)) t = te + o.desfase;
                        else if (iHilo >= _g.hilo.Count && puedoAlimentar)
                        { Error($"línea {o.linea}: la etiqueta @{o.etiqueta} no existe en el HILO"); a.iPista++; }
                        break;
                }
                if (t != null) pendientes.Add(new Evento { t = Mathf.Max(0f, t.Value), orden = o, dePista = a });
            }

            if (pendientes.Count == 0) break;
            var siguiente = pendientes.OrderBy(p => p.t).ThenBy(p => p.dePista != null ? 1 : 0).First();
            pendientes.Remove(siguiente);

            float dura = Procesar(siguiente.orden, siguiente.t, siguiente.dePista != null);
            float fin = siguiente.t + dura;
            _fin = Mathf.Max(_fin, fin);

            if (siguiente.dePista != null)
            {
                siguiente.dePista.finPistaAnterior = fin;
                siguiente.dePista.iPista++;
            }
            else
            {
                if (siguiente.bloquea)
                {
                    if (siguiente.orden.arranque == GuionTexto.Arranque.ALaVezYEspera) _cursor = Mathf.Max(_cursor, fin);
                    else _cursor = fin;
                    _finAnterior = fin;
                    _bloqueanteAnterior = siguiente.orden;
                    puedoAlimentar = true;
                }
            }
        }
        _fin = Mathf.Max(_fin, _cursor);
        foreach (var kv in _etiquetas) _fin = Mathf.Max(_fin, kv.Value);
    }

    /// La pausa natural antes de una orden que va seguida: entre dos frases, la de conversación.
    private float PausaAntes(GuionTexto.Orden o)
    {
        if (o.verbo != "dice" || _bloqueanteAnterior == null || _bloqueanteAnterior.verbo != "dice") return 0f;
        if (o.opciones.ContainsKey("pausa")) return o.OpF("pausa", 0f);
        return _ultimoHablante == o.actor ? _g.pausaMismoHablante : _g.pausaEntreFrases;
    }

    // ── Procesar una orden ──────────────────────────────────────────────────────────────────

    /// Aplica una orden en el instante t. Devuelve cuánto dura.
    private float Procesar(GuionTexto.Orden o, float t, bool dePista)
    {
        if (o.actor != null)
        {
            if (!_actores.TryGetValue(o.actor, out var a)) { Error($"{o}: '{o.actor}' no está en el REPARTO"); return 0f; }
            if (o.verbo != "cara" && o.verbo != "mira" && o.verbo != "cabeza") a.comandos.Add(t);
            return ProcesarActor(a, o, t, dePista);
        }
        switch (o.verbo)
        {
            case "plano":
                _planosPedidos.Add(new PlanoPedido { orden = o, t0 = t });
                return 0f;
            case "eje":
            {
                if (o.args.FirstOrDefault() == "libre") { PedirEjeDePantalla(t, null, null); return 0f; }
                if (o.args.Count < 2 || !_actores.TryGetValue(o.args[0], out var izq) || !_actores.TryGetValue(o.args[1], out var der) || izq == der)
                {
                    Error($"{o}: «eje A B» necesita dos personajes del REPARTO (A sale a la izquierda), o «eje libre»");
                    return 0f;
                }
                PedirEjeDePantalla(t, izq, der);
                return 0f;
            }
            case "efecto":
                return Efecto(o, t);
            default:
                if (GuionTexto.Azucar.Contains(o.verbo)) return Azucar(o, t);
                Error($"{o}: orden desconocida");
                return 0f;
        }
    }

    private float ProcesarActor(ActorH a, GuionTexto.Orden o, float t, bool dePista)
    {
        switch (o.verbo)
        {
            case "en":
            {
                if (!Punto(o.args.FirstOrDefault(), o.linea, out var p)) return 0f;
                SoltarBucleSiAnda(a, t, haraCuerpo: false);
                a.Teletransporte(t, p);
                if (o.opciones.TryGetValue("mirando", out var x) && Objetivo(x, o.linea, out var m)) a.Mirar(t, m);
                return 0f;
            }
            case "anda":
            case "corre":
            case "huye":
            {
                var destinos = new List<Vector3>();
                foreach (var n in o.args) { if (!Punto(n, o.linea, out var p)) return 0f; destinos.Add(p); }
                if (destinos.Count == 0) { Error($"{o}: ¿a dónde?"); return 0f; }
                // «formacion=X»: cada destino se desplaza lo que el actor está separado de X al salir,
                // así un grupo que sale alrededor de alguien llega igual de colocado (los que llevan
                // a otro en volandas).
                if (o.opciones.TryGetValue("formacion", out var guia))
                {
                    if (!_actores.TryGetValue(guia, out var g) || g == a) { Error($"{o}: formacion={guia}: no es otro personaje del REPARTO"); return 0f; }
                    Vector3 sitio = a.PosEn(t) - g.PosEn(t);
                    sitio.y = 0f;
                    for (int k = 0; k < destinos.Count; k++) destinos[k] += sitio;
                }
                string ritmo = o.Op("ritmo", o.verbo == "anda" ? "normal" : "corre");
                float vel = Velocidad(ritmo, o);
                SoltarBucleSiAnda(a, t, haraCuerpo: true);
                float dura = Caminar(a, t, destinos, vel, o);
                if (o.opciones.TryGetValue("llega_mirando", out var x) && Objetivo(x, o.linea, out var m)) a.Mirar(t + dura, m);
                return dePista || !o.Tiene("sin_esperar") ? dura : 0f;
            }
            case "desliza":
            {
                Vector3 p = default;
                if (!o.Tiene("aqui") && !Punto(o.args.FirstOrDefault(), o.linea, out p)) return 0f;
                float dura = Mathf.Max(0.05f, o.OpF("dura", 0.5f));
                Vector3 desde = a.PosEn(t);
                Vector3 hasta = o.opciones.ContainsKey("altura") ? p + Vector3.up * o.OpF("altura", 0f) : p;
                if (o.Tiene("aqui")) hasta = desde + Vector3.up * o.OpF("altura", 0f);
                a.Cortar(t);
                int pasos = Mathf.Max(2, Mathf.CeilToInt(dura / 0.1f));
                for (int k = 1; k <= pasos; k++)
                {
                    float u = k / (float)pasos;
                    float s = o.Tiene("seco") ? 1f - (1f - u) * (1f - u) : u * u * (3f - 2f * u);
                    a.pos.Add(new ClaveDePosicion(t + dura * u, Vector3.Lerp(desde, hasta, s)));
                }
                a.deslizando.Add((t, t + dura + 0.05f));
                if (o.opciones.TryGetValue("mirando", out var x) && Objetivo(x, o.linea, out var m)) a.Mirar(t, m);
                return dura;
            }
            case "encima":
            {
                // «estela encima g1 g2 g3 g4 [altura=m] [entra=s] [dura=s]»: la llevan en volandas. Su
                // camino lo calcula LlevarEncima cuando ya se sabe por dónde van los de debajo.
                var debajo = new List<ActorH>();
                foreach (var n in o.args)
                {
                    if (!_actores.TryGetValue(n, out var b) || b == a) { Error($"{o}: '{n}' no es otro personaje del REPARTO"); return 0f; }
                    debajo.Add(b);
                }
                if (debajo.Count == 0) { Error($"{o}: ¿encima de quién?"); return 0f; }
                a.Cortar(t);
                a.encima.Add(new ActorH.Encima
                {
                    t0 = t,
                    t1 = o.opciones.ContainsKey("dura") ? t + Mathf.Max(0.1f, o.OpF("dura", 0f)) : float.PositiveInfinity,
                    altura = o.OpF("altura", 1.6f),
                    entra = Mathf.Max(0f, o.OpF("entra", 0.4f)),
                    debajo = debajo,
                });
                return 0f;
            }
            case "de_la_mano":
            {
                // «clara de_la_mano madre [lado=der|izq] [dura=s]»: va al lado de la madre, cogida de
                // su mano, adonde ande ella. Su camino lo calcula LlevarDeLaMano al final.
                if (o.args.Count == 0 || !_actores.TryGetValue(o.args[0], out var guia) || guia == a)
                { Error($"{o}: de_la_mano ¿de quién? (otro personaje del REPARTO)"); return 0f; }
                SoltarBucleSiAnda(a, t, haraCuerpo: true);
                a.Cortar(t);
                a.deLaMano.Add(new ActorH.DeLaMano
                {
                    t0 = t, guia = guia, lado = o.Op("lado", "der") == "izq" ? -1f : 1f,
                    t1 = o.opciones.ContainsKey("dura") ? t + Mathf.Max(0.1f, o.OpF("dura", 0f)) : float.PositiveInfinity,
                });
                return 0f;
            }
            case "suelta":
            {
                // Se calcula ya el tramo de la mano: lo que haga después sale de donde se ha soltado.
                foreach (var m in a.deLaMano)
                    if (!m.hecho && m.t0 <= t && m.t1 > t) { m.t1 = t; IrDeLaMano(a, m); }
                return 0f;
            }
            case "pasea":
            {
                var puntos = new List<Vector3>();
                foreach (var n in o.args) { if (!Punto(n, o.linea, out var p)) return 0f; puntos.Add(p); }
                if (puntos.Count == 0) { Error($"{o}: ¿por dónde?"); return 0f; }
                SoltarBucleSiAnda(a, t, haraCuerpo: true);
                Pasear(a, t, puntos, Velocidad(o.Op("ritmo", "paseo"), o), o.OpF("pausa", 2.5f), o);
                return 0f;
            }
            case "mira":
            {
                // Sentado el cuerpo no gira (en el juego tampoco: el asiento fija la orientación).
                if (Sentado(a)) { Aviso($"{o}: {a.alias} está sentado; no se gira en la silla"); return 0f; }
                if (Objetivo(o.args.FirstOrDefault(), o.linea, out var m))
                {
                    a.Mirar(t, m);
                    // La cabeza va con el cuerpo: sin esto se quedaba vuelta hacia el último que
                    // habló mientras el cuerpo ya atendía a otra cosa.
                    if (!Tumbado(a)) a.GirarCabeza(t, m.tipo != TipoDeMirada.Rumbo ? m : ActorH.CabezaAlFrente);
                }
                return 0f;
            }
            case "cabeza":
            {
                // Solo la cabeza, sin girar el cuerpo: vale sentado. «nadie» la devuelve al frente.
                string a_quien = o.args.FirstOrDefault();
                if (a_quien == "nadie" || a_quien == "frente") a.GirarCabeza(t, ActorH.CabezaAlFrente);
                else if (Objetivo(a_quien, o.linea, out var mc) && mc.tipo != TipoDeMirada.Rumbo) a.GirarCabeza(t, mc);
                else Error($"{o}: ¿hacia quién vuelve la cabeza? (un personaje, un punto o «nadie»)");
                return 0f;
            }
            case "gesto":
            {
                string estado = o.args.FirstOrDefault();
                if (!Estado(a, ref estado, o)) return 0f;
                int veces = Mathf.Max(1, (int)o.OpF("veces", 1));
                float largo = a.estados[estado].largo;
                // «voz=laugh» elige la reacción, «voz=no» la calla; sin nada, la que pide el gesto.
                string voz = o.opciones.TryGetValue("voz", out var v) ? (v == "no" ? null : v)
                    : ReaccionesDeEscena.VozParaGesto(estado);
                // Otro gesto de cuerpo entero manda sobre el cierre pendiente del anterior.
                if (a.estados[estado].cuerpoEntero)
                    a.anim.RemoveAll(x => x.tipo == TipoDeAnimacion.FinDeGesto && x.t > t - 1e-4f);
                for (int i = 0; i < veces; i++) a.Animar(t + i * largo * 0.92f, TipoDeAnimacion.Gesto, estado, voz: i == 0 ? voz : null);
                float dura = o.opciones.ContainsKey("dura") ? o.OpF("dura", largo) : largo * veces * 0.92f;
                if (a.estados[estado].cuerpoEntero)
                {
                    if (a.bucle != null) a.Animar(t + dura, TipoDeAnimacion.Bucle, a.bucle);
                    else a.Animar(t + dura, TipoDeAnimacion.FinDeGesto);
                }
                return dura;
            }
            case "bucle":
            {
                string estado = o.args.FirstOrDefault();
                if (!Estado(a, ref estado, o)) return 0f;
                bool congela = o.Tiene("congela") || !a.estados[estado].bucle;
                bool alFinal = o.Tiene("al_final") || o.Tiene("ya");
                // Sentado o en un bucle de cuerpo entero, una pose de medio cuerpo va encima: el
                // cuerpo sigue en su bucle (para el asiento, las cámaras y el storyboard no cambia nada).
                if (!a.estados[estado].cuerpoEntero && a.estados[estado].bucle && a.bucle != null && a.bucleDeCuerpo)
                {
                    if (a.id == SequenceActor.PlayerId)
                    {
                        Aviso($"{o}: el jugador no admite todavía una pose de medio cuerpo encima de otra de cuerpo entero; no se pone");
                        return 0f;
                    }
                    a.Animar(t, TipoDeAnimacion.PoseAlta, estado, false, o.OpF("fundido", 0f));
                    a.poseAlta = estado;
                    return o.opciones.ContainsKey("dura") ? o.OpF("dura", 0f) : 0f;
                }
                if (!a.estados[estado].bucle && !o.Tiene("congela"))
                    Aviso($"{o}: '{estado}' no es un bucle; se quedará en su último fotograma (añade «congela» si es lo que quieres)");
                a.Animar(t, TipoDeAnimacion.Bucle, estado, congela, o.OpF("fundido", 0f), alFinal);
                a.bucle = estado;
                a.bucleDeCuerpo = a.estados[estado].cuerpoEntero;
                // Tumbado, la cabeza no sigue a nadie (se torcería contra el suelo).
                if (Tumbado(a)) a.GirarCabeza(t, ActorH.CabezaAlFrente);
                return o.opciones.ContainsKey("dura") ? o.OpF("dura", 0f) : 0f;
            }
            case "reposo":
                // «reposo brazos»: suelta solo la pose de medio cuerpo; sigue sentado o en su bucle.
                if (o.args.Contains("brazos"))
                {
                    if (a.poseAlta != null) a.Animar(t, TipoDeAnimacion.SoltarPoseAlta);
                    else if (a.bucle != null && !a.bucleDeCuerpo) { a.Animar(t, TipoDeAnimacion.Reposo); a.bucle = null; }
                    a.poseAlta = null;
                    return 0f;
                }
                a.Animar(t, TipoDeAnimacion.Reposo);
                a.bucle = null; a.bucleDeCuerpo = false; a.poseAlta = null;
                return 0f;
            case "cara":
            {
                string palabra = Emocion(o.args.FirstOrDefault(), o);
                if (palabra == null) return 0f;
                a.caras.Add(new OrdenDeCara { t = t, emocion = palabra, dura = o.OpF("dura", 0f) });
                return 0f;
            }
            case "aparece":
            {
                if (o.opciones.TryGetValue("en", out var en) && Punto(en, o.linea, out var p)) a.Teletransporte(t, p);
                if (o.opciones.TryGetValue("mirando", out var x) && Objetivo(x, o.linea, out var m)) a.Mirar(t, m);
                a.visibilidad.Add((t, true));
                return 0f;
            }
            case "desaparece":
                a.visibilidad.Add((t, false));
                return 0f;
            case "charla":
            {
                string con = o.args.FirstOrDefault();
                if (con != null && Objetivo(con, o.linea, out var m)) a.Mirar(t, m);
                string base_ = a.estados.ContainsKey("InteractWithPeople_NoWeapon") ? "InteractWithPeople_NoWeapon" : null;
                if (base_ != null) { a.Animar(t, TipoDeAnimacion.Bucle, base_); a.bucle = base_; a.bucleDeCuerpo = true; }
                a.charlas.Add((t, con, o.OpF("cada", 3.2f), o.linea));
                return 0f;
            }
            case "dice":
                return Decir(a, o, t);
            case "sienta":
                return Sentar(a, o, t);
            case "levanta":
                return Levantar(a, o, t);
            case "coge": case "deja":
                return PropEnMano(a, o, t);
        }
        Error($"{o}: acción desconocida");
        return 0f;
    }

    /// Los que van en volandas («encima»): mientras dura, su camino es el centro de los que lo llevan
    /// más la altura pedida, y al empezar sube hasta ahí en «entra» segundos. Sin «dura», hasta que
    /// desaparece o acaba el guion. Va deslizándose (sin animación de andar) y el cuerpo mira a donde
    /// diga su mirada. Se hace después de repartir el tiempo: hasta entonces no se sabe por dónde van.
    private void LlevarEncima()
    {
        const float Paso = 0.1f;
        foreach (var a in _actores.Values)
            foreach (var e in a.encima)
            {
                if (float.IsInfinity(e.t1))
                {
                    e.t1 = _fin;
                    foreach (var v in a.visibilidad)
                        if (v.t > e.t0 && !v.visible) { e.t1 = v.t; break; }
                }
                if (e.t1 <= e.t0) continue;
                Vector3 desde = a.PosEn(e.t0);
                var despues = a.pos.Where(k => k.t > e.t1 + 1e-4f).ToList();
                a.pos.RemoveAll(k => k.t > e.t0 + 1e-4f);
                if (a.pos.Count == 0 || a.pos[a.pos.Count - 1].t < e.t0 - 1e-4f) a.pos.Add(new ClaveDePosicion(e.t0, desde));
                for (float t = e.t0 + Paso; ; t += Paso)
                {
                    t = Mathf.Min(t, e.t1);
                    Vector3 centro = Vector3.zero;
                    foreach (var b in e.debajo) centro += b.PosEn(t);
                    centro = centro / e.debajo.Count + Vector3.up * e.altura;
                    float u = e.entra > 0f ? Mathf.Clamp01((t - e.t0) / e.entra) : 1f;
                    a.pos.Add(new ClaveDePosicion(t, Vector3.Lerp(desde, centro, u * u * (3f - 2f * u))));
                    if (t >= e.t1) break;
                }
                a.pos.AddRange(despues);
                a.deslizando.Add((e.t0, e.t1 + 0.05f));
            }
    }

    /// Los que van de la mano («de_la_mano»): mientras dura, su camino es el de quien les guía,
    /// desplazado a su lado (perpendicular a hacia donde anda); llegan a ese sitio en 0,4 s y,
    /// cuando quien guía se para, se quedan donde estaban. Pone los DeLaManoBeat de coger y soltar.
    private void LlevarDeLaMano()
    {
        foreach (var a in _actores.Values)
            foreach (var m in a.deLaMano)
                if (!m.hecho) IrDeLaMano(a, m);
    }

    private void IrDeLaMano(ActorH a, ActorH.DeLaMano m)
    {
        const float Paso = 0.1f, Separacion = 0.55f, Entra = 0.4f;
        m.hecho = true;
        bool hastaElFinal = float.IsInfinity(m.t1);
        if (hastaElFinal)
        {
            m.t1 = _fin;
            foreach (var v in a.visibilidad)
                if (v.t > m.t0 && !v.visible) { m.t1 = v.t; break; }
        }
        if (m.t1 <= m.t0) return;
        Vector3 desde = a.PosEn(m.t0);
        // Hacia dónde mira quien guía al empezar: el que va de su mano queda a ese lado.
        Vector3 lateral = Plano(desde - m.guia.PosEn(m.t0));
        lateral = lateral.sqrMagnitude > 1e-4f ? lateral.normalized * m.lado : Vector3.right;
        Vector3 rumbo = Vector3.Cross(lateral * m.lado, Vector3.up) * m.lado;
        var despues = a.pos.Where(k => k.t > m.t1 + 1e-4f).ToList();
        a.pos.RemoveAll(k => k.t > m.t0 + 1e-4f);
        if (a.pos.Count == 0 || a.pos[a.pos.Count - 1].t < m.t0 - 1e-4f) a.pos.Add(new ClaveDePosicion(m.t0, desde));
        for (float t = m.t0 + Paso; ; t += Paso)
        {
            t = Mathf.Min(t, m.t1);
            Vector3 vel = Plano(m.guia.VelEn(t));
            if (vel.sqrMagnitude > 0.05f * 0.05f) rumbo = Vector3.Slerp(rumbo, vel.normalized, 0.35f);
            Vector3 derecha = Vector3.Cross(Vector3.up, rumbo).normalized;
            Vector3 sitio = m.guia.PosEn(t) + derecha * (Separacion * m.lado);
            sitio.y = SueloEn(sitio, m.guia.PosEn(t).y);
            float u = Mathf.Clamp01((t - m.t0) / Entra);
            a.pos.Add(new ClaveDePosicion(t, Vector3.Lerp(desde, sitio, u * u * (3f - 2f * u))));
            if (t >= m.t1) break;
        }
        a.pos.AddRange(despues);
        _efectos.Add(new EfectoHorneado { t = m.t0, beat = new DeLaManoBeat { guiaId = m.guia.id, quienId = a.id, coger = true } });
        if (!hastaElFinal || m.t1 < _fin - 0.01f)
            _efectos.Add(new EfectoHorneado { t = m.t1, beat = new DeLaManoBeat { guiaId = m.guia.id, quienId = a.id, coger = false } });
    }

    /// Altura del NavMesh bajo un punto (o la que se dé si no hay).
    private static float SueloEn(Vector3 p, float porDefecto)
        => UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 1.0f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position.y : porDefecto;

    /// Sentado (un bucle «Sit…»): el cuerpo se queda como lo dejó el asiento. Ni las miradas de
    /// las frases (la suya y la de quien le habla) ni un «mira» lo giran.
    private static bool Sentado(ActorH a)
        => a.sentadoCon != NPCAmbientActivity.None;

    /// Antes de echar a andar se suelta un bucle de cuerpo entero (bailando no se anda).
    private void SoltarBucleSiAnda(ActorH a, float t, bool haraCuerpo)
    {
        if (a.bucle != null && (a.bucleDeCuerpo || !haraCuerpo))
        {
            a.Animar(t, TipoDeAnimacion.Reposo);
            a.bucle = null; a.bucleDeCuerpo = false; a.poseAlta = null;
        }
    }

    internal bool Estado(ActorH a, string estado, GuionTexto.Orden o)
    {
        return Estado(a, ref estado, o);
    }

    private bool Estado(ActorH a, ref string estado, GuionTexto.Orden o)
    {
        if (string.IsNullOrEmpty(estado)) { Error($"{o}: falta el nombre de la animación"); return false; }
        // Alias de magia para comodidad de guiones
        if (estado == "MagicAttackDirect2H01" && a.estados.ContainsKey("HumanM@MagicAttackDirect2H01 - Cast"))
            estado = "HumanM@MagicAttackDirect2H01 - Cast";
        else if (estado == "MagicAttackOmni01" && a.estados.ContainsKey("HumanM@MagicAttackOmni01 - Cast"))
            estado = "HumanM@MagicAttackOmni01 - Cast";

        if (a.estados.Count == 0) { a.estados[estado] = new InfoDeEstado { largo = 2f }; return true; }
        if (a.estados.ContainsKey(estado)) return true;
        string buscado = estado;
        var parecidos = a.estados.Keys.Where(k => k.IndexOf(buscado, StringComparison.OrdinalIgnoreCase) >= 0 || buscado.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0).Take(4);
        Error($"{o}: {a.alias} no tiene la animación '{estado}'. ¿{string.Join(", ", parecidos)}? (ver animaciones.md)");
        return false;
    }

    /// La palabra de la cara. El asset horneado guarda la palabra y no el enum, para que no
    /// dependa del arte de este juego (ver CarasDelGuion).
    private string Emocion(string nombre, GuionTexto.Orden o)
    {
        if (string.IsNullOrEmpty(nombre)) { Error($"{o}: falta la emoción"); return null; }
        if (!CarasDelGuion.Intentar(nombre, out var e))
        {
            Error($"{o}: emoción desconocida '{nombre}' ({string.Join(", ", CarasDelGuion.Palabras)})");
            return null;
        }
        return CarasDelGuion.Canonica(e);
    }

    private float Velocidad(string ritmo, GuionTexto.Orden o)
    {
        switch (ritmo)
        {
            case "paseo": return 0.85f;
            case "normal": return 1.15f;
            case "prisa": return 1.55f;
            case "trote": return 2.0f;
            case "corre": return 2.4f;
            default:
                if (GuionTexto.EsNumero(ritmo)) return Mathf.Clamp(GuionTexto.Num(ritmo, 1.1f), 0.3f, 4f);
                Aviso($"{o}: ritmo '{ritmo}' desconocido (paseo, normal, prisa, trote, corre o m/s); uso normal");
                return 1.15f;
        }
    }

    // ── Frases ──────────────────────────────────────────────────────────────────────────────

    private float Decir(ActorH a, GuionTexto.Orden o, float t)
    {
        string clave = o.args.Count > 1 ? string.Join(" ", o.args) : o.args.FirstOrDefault();
        if (string.IsNullOrEmpty(clave)) { Error($"{o}: ¿qué frase?"); return 0f; }
        float dura = DuracionDeVoz(clave, o);
        var linea = new LineaHorneada { t0 = t, t1 = t + dura, actor = a.id, clave = clave, sinPose = o.Tiene("sin_pose"),
            onlyIfFlag = o.Op("onlyIfFlag"), skipIfFlag = o.Op("skipIfFlag") };
        if (o.opciones.TryGetValue("texto", out var pres))
            linea.presentacion = pres == "bocadillo" ? 0 : pres == "subtitulo_grande" ? 2 : 1;
        _lineas.Add(linea);
        a.habla.Add((t, t + dura));
        _ultimoHablante = a.alias;

        if (o.opciones.TryGetValue("gesto", out var g) && Estado(a, ref g, o))
        {
            linea.gesto = g;
            a.Animar(t + 0.08f, TipoDeAnimacion.Gesto, g);
        }
        // Sentado no se gira el cuerpo para hablar: se quedaría de lado en la silla.
        if (o.opciones.TryGetValue("mira", out var x) && Objetivo(x, o.linea, out var m))
        {
            if (!Sentado(a) && !EnPoseDeSuelo(a)) a.Mirar(t, m);
            if (!Tumbado(a) && m.tipo != TipoDeMirada.Rumbo) a.GirarCabeza(t, m);
        }
        if (o.opciones.ContainsKey("mira") || o.opciones.ContainsKey("cabeza")) _cabezaPedida.Add((a.id, t));
        // «cabeza=will,liam»: mientras habla, la cabeza va de uno a otro (a partes iguales).
        if (o.opciones.TryGetValue("cabeza", out var cabezas) && !Tumbado(a))
        {
            var destinos = cabezas.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            for (int i = 0; i < destinos.Count; i++)
                if (Objetivo(destinos[i], o.linea, out var mc) && mc.tipo != TipoDeMirada.Rumbo)
                    a.GirarCabeza(t + dura * i / destinos.Count, mc);
        }
        if (o.opciones.TryGetValue("cara", out var c))
        {
            string palabra = Emocion(c, o);
            if (palabra != null) a.caras.Add(new OrdenDeCara { t = t, emocion = palabra, dura = dura + 0.4f });
            if (palabra != null && CarasDelGuion.Intentar(palabra, out var caraDeLaFrase)) _caraDeLinea[linea] = caraDeLaFrase;
        }

        // Si la frase señala algo del mundo («mira=» a un punto o a un objeto, no a otro personaje),
        // los que la oyen miran también ahí y no a quien habla: todos miran el globo a la vez.
        ClaveDeMirada? focoComun = null;
        if (x != null && !_actores.ContainsKey(x) && Objetivo(x, o.linea, out var mf) && mf.tipo != TipoDeMirada.Rumbo)
            focoComun = mf;

        // Los que escuchan se giran hacia quien habla (o hacia lo que señala), cada uno a su ritmo.
        IEnumerable<ActorH> oyentes = Enumerable.Empty<ActorH>();
        if (o.opciones.TryGetValue("oyentes", out var lista))
        {
            if (lista == "todos")
                oyentes = _actores.Values.Where(b => b != a && b.VisibleEn(t) && Vector3.Distance(b.PosEn(t), a.PosEn(t)) < 12f);
            else
                oyentes = lista.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0)
                    .Select(n => _actores.TryGetValue(n, out var b) ? b : null).Where(b => b != null);
        }
        else if (x != null && _actores.TryGetValue(x, out var mirado)) oyentes = new[] { mirado };
        else
        {
            // Auto-detectar oyentes cercanos visibles dentro del rango de conversación
            oyentes = _actores.Values.Where(b => b != a && b.VisibleEn(t) && Vector3.Distance(b.PosEn(t), a.PosEn(t)) < 12f);
        }

        int k = 0;
        string textoFrase = TextoDe(clave);
        foreach (var b in oyentes)
        {
            if (b == a || b.AndandoEn(t)) continue;
            // La cabeza sigue a quien habla, también sentado (el cuerpo no se gira en el asiento);
            // tumbado, no. Cada uno a su ritmo.
            float tGiro = t + 0.15f + (k++ % 4) * 0.12f;
            var hacia = focoComun ?? new ClaveDeMirada { tipo = TipoDeMirada.Actor, actor = a.id };
            if (!Tumbado(b)) b.GirarCabeza(tGiro - 0.05f, hacia);
            if (Sentado(b) || EnPoseDeSuelo(b)) continue;
            b.Mirar(tGiro, hacia);

            // Reacción corporal y facial del oyente si no está hablando él a la vez
            if (!b.habla.Any(h => t < h.t1 && t + dura > h.t0))
            {
                _ultimasReaccionesPorActor.TryGetValue(b.id, out var infoReaccion);
                int lineasSinReaccionar = (t - infoReaccion.tiempo > 3.0f) ? 1 : 0;

                if (ActuacionAutomatica.CalcularReaccionOyente(
                    textoFrase,
                    NPCEmotion.None,
                    lineasSinReaccionar,
                    infoReaccion.gesto,
                    out string gestoOyente,
                    out NPCEmotion caraOyente))
                {
                    // Quien sostiene una pose de medio cuerpo no la suelta para reaccionar: escucha con la cabeza y la cara.
                    bool sostienePose = b.bucle != null && !b.bucleDeCuerpo;
                    // Quien oye un aviso da un respingo, no se cruza de brazos.
                    if (gestoOyente == "SenseSomethingStart_NoWeapon" && b.estados.ContainsKey("Sobresalto01")) gestoOyente = "Sobresalto01";
                    // Y no repite la misma reacción a los pocos segundos: se queda con la cara.
                    if (gestoOyente != null && gestoOyente == infoReaccion.gesto && t - infoReaccion.tiempo < 6f) gestoOyente = null;
                    if (!sostienePose && !string.IsNullOrEmpty(gestoOyente) && (b.estados.Count == 0 || b.estados.ContainsKey(gestoOyente)))
                    {
                        float tGesto = t + Mathf.Min(0.7f, dura * 0.4f);
                        b.Animar(tGesto, TipoDeAnimacion.Gesto, gestoOyente);
                        _ultimasReaccionesPorActor[b.id] = (t, gestoOyente);
                    }
                    if (caraOyente != NPCEmotion.None && caraOyente != NPCEmotion.Neutral)
                    {
                        string palabraCara = CarasDelGuion.Canonica(caraOyente);
                        if (!string.IsNullOrEmpty(palabraCara))
                        {
                            float tCara = t + Mathf.Min(0.6f, dura * 0.35f);
                            b.caras.Add(new OrdenDeCara { t = tCara, emocion = palabraCara, dura = Mathf.Min(dura, 2.0f) });
                        }
                    }
                }
            }
        }
        return dura;
    }

    /// Tumbado, sentado en el suelo o de rodillas (un bucle de cuerpo entero de esos): quien escucha
    /// así no se gira ni hace gestos de oyente, que le pondrían de pie un instante (se veía un
    /// parpadeo) y le harían rodar por el suelo. Ver INC-866.
    /// Quien habla sin «mira=» ni «cabeza=» vuelve la cabeza hacia aquel con quien habla: el que le
    /// contesta justo después o, si no, el que le acaba de hablar (cerca y a la vista). Sin esto se
    /// quedaba mirando al último que había hablado antes, aunque ahora hablara con otro.
    private void CabezaDeQuienHabla(ActorH a)
    {
        foreach (var h in a.habla)
        {
            if (_cabezaPedida.Contains((a.id, h.t0)) || TumbadoEn(a, h.t0)) continue;
            ActorH con = null;
            float mejor = float.MaxValue;
            foreach (var b in _actores.Values)
            {
                if (b == a || !b.VisibleEn(h.t0) || Vector3.Distance(b.PosEn(h.t0), a.PosEn(h.t0)) > 12f) continue;
                foreach (var hb in b.habla)
                {
                    float d;
                    if (hb.t0 >= h.t1 - 0.2f) d = hb.t0 - h.t1;                 // le contesta
                    else if (hb.t1 <= h.t0 + 0.2f) d = h.t0 - hb.t1 + 0.5f;     // le acaba de hablar
                    else continue;
                    if (d < 6f && d < mejor) { mejor = d; con = b; }
                }
            }
            if (con != null)
                a.cabezas.Add(new ClaveDeMirada { t = h.t0 + 0.05f, tipo = TipoDeMirada.Actor, actor = con.id });
        }
    }

    private static bool TumbadoEn(ActorH a, float t)
    {
        string e = BucleEn(a, t);
        return e != null && (e.Contains("Knockdown") || e.Contains("Sleeping") || e.Contains("Die0") || e.Contains("Swimming") || e.Contains("Stun"));
    }

    /// Tumbado, dormido, caído o nadando: la cabeza no sigue a nadie.
    private static bool Tumbado(ActorH b)
    {
        if (b.bucle == null || !b.bucleDeCuerpo) return false;
        string e = b.bucle;
        return e.Contains("Knockdown") || e.Contains("Sleeping") || e.Contains("Die0") || e.Contains("Swimming") || e.Contains("Stun");
    }

    private static bool EnPoseDeSuelo(ActorH b)
    {
        if (b.bucle == null || !b.bucleDeCuerpo) return false;
        string e = b.bucle;
        return e.Contains("Knockdown") || e.Contains("SitGround") || e.Contains("Sleeping") || e.Contains("Die0")
            || e.Contains("Cry01") || e.Contains("Swimming") || e.Contains("Stun");
    }

    private float DuracionDeVoz(string clave, GuionTexto.Orden o)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/Voices/es/{clave}.mp3")
                   ?? Resources.Load<AudioClip>($"Voices/es/{clave}");
        // El hueco explícito permite montar texto y alternativas, pero nunca corta una voz más larga.
        float explicita = o.OpF("dura", 0f);
        if (clip != null) return Mathf.Max(clip.length, explicita);
        if (explicita > 0f)
        {
            Aviso($"{o}: la frase {clave} no tiene voz en Voices/es; usa los {explicita:0.0} s del guion");
            return explicita;
        }
        string texto = TextoDe(clave);
        float estimada = Mathf.Max(1.2f, (texto?.Length ?? 30) * 0.065f);
        Aviso($"{o}: la frase {clave} no tiene voz en Voices/es; le doy {estimada:0.0} s por su texto");
        return estimada;
    }

    private Dictionary<string, string> _textos;
    private string TextoDe(string clave)
    {
        if (_textos == null)
        {
            _textos = new Dictionary<string, string>();
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Localization/cinematics_es.json");
            if (ta != null)
            {
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ta.text,
                    "\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"text\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
                    _textos[m.Groups[1].Value] = System.Text.RegularExpressions.Regex.Unescape(m.Groups[2].Value);
            }
            var taDlg = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Localization/dialogues_es.json");
            if (taDlg != null)
            {
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(taDlg.text,
                    "\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"text\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
                    if (!_textos.ContainsKey(m.Groups[1].Value))
                        _textos[m.Groups[1].Value] = System.Text.RegularExpressions.Regex.Unescape(m.Groups[2].Value);
            }
        }
        return _textos.TryGetValue(clave, out var s) ? s : null;
    }

    // ── Remate: actuación que se decide con todo el tiempo ya repartido ─────────────────────

    private void Rematar()
    {
        foreach (var a in _actores.Values)
        {
            a.comandos.Sort();
            // Pose de conversación: solo quieto, sin bucle y sin andar en toda la frase.
            foreach (var (t0, t1) in a.habla)
            {
                var linea = _lineas.First(l => l.actor == a.id && Mathf.Approximately(l.t0, t0));
                bool anda = false;
                for (float t = t0; t <= t1; t += 0.2f) if (a.AndandoEn(t)) { anda = true; break; }
                string bucle = BucleEn(a, t0);
                if (anda || linea.sinPose) { linea.sinPose = true; continue; }
                if (bucle != null)
                {
                    // Sentado: no se toca la capa base (bucle de asiento), pero si la línea no trae gesto
                    // se anima el tren superior con gestos de habla para que los brazos no queden congelados.
                    if (Sentado(a) && string.IsNullOrEmpty(linea.gesto) && !ConPoseAltaEn(a, t0))
                    {
                        string[] gestosSentado = { "Talk01", "Talk02", "HeadNod01" };
                        string g = gestosSentado[Mathf.Abs(linea.clave.GetHashCode()) % gestosSentado.Length];
                        if (a.estados.ContainsKey(g))
                        {
                            linea.gesto = g;
                            a.Animar(t0 + 0.08f, TipoDeAnimacion.Gesto, g);
                        }
                    }
                    linea.sinPose = true;
                    continue;
                }
                a.Animar(t0, TipoDeAnimacion.HablaEmpieza);
                a.Animar(t1 + 0.05f, TipoDeAnimacion.HablaTermina);

                string[] candidatosHabla = { "Talk01", "Talk02", "Talk03" };
                string[] gestosHabla = candidatosHabla.Where(g => a.estados.ContainsKey(g)).ToArray();
                if (gestosHabla.Length > 0)
                {
                    int idxGesto = Mathf.Abs(linea.clave.GetHashCode());
                    if (string.IsNullOrEmpty(linea.gesto))
                    {
                        // Primero lo que pide la frase (un aviso, un grito, una negativa); si no pide nada, charla.
                        string gIni = GestoSegunLaFrase(a, linea, out bool sinGesto) ?? gestosHabla[idxGesto % gestosHabla.Length];
                        if (sinGesto) continue;
                        linea.gesto = gIni;
                        a.Animar(t0 + 0.08f, TipoDeAnimacion.Gesto, gIni);
                    }
                    float durIni = !string.IsNullOrEmpty(linea.gesto) && a.estados.TryGetValue(linea.gesto, out var infoIni)
                        ? infoIni.largo
                        : 2.2f;
                    float tCursor = t0 + 0.08f + durIni + 0.2f;
                    idxGesto++;
                    while (tCursor + 1.3f <= t1)
                    {
                        bool ocupado = a.anim.Any(o => o.tipo == TipoDeAnimacion.Gesto && o.t >= tCursor - 0.5f && o.t <= tCursor + 1.0f);
                        string gSig = gestosHabla[idxGesto++ % gestosHabla.Length];
                        float durSig = a.estados.TryGetValue(gSig, out var infoSig) ? infoSig.largo : 2.2f;
                        if (!ocupado) a.Animar(tCursor, TipoDeAnimacion.Gesto, gSig);
                        tCursor += durSig + 0.25f;
                    }
                }
            }
            // Charlas de fondo: gestos de conversación hasta la siguiente orden del personaje.
            var rnd = new System.Random(a.id.GetHashCode());
            string[] gestos = { "Talk01", "HeadNod01", "Talk02", "Laugh01", "Talk03", "Question01", "HeadShake01" };
            foreach (var ch in a.charlas)
            {
                float hasta = a.comandos.Where(c => c > ch.t0 + 0.01f).DefaultIfEmpty(_fin).First();
                float t = ch.t0 + 0.6f + (float)rnd.NextDouble() * ch.cada;
                int k = rnd.Next(gestos.Length);
                while (t < hasta - 1.2f)
                {
                    string g = gestos[k++ % gestos.Length];
                    if (a.estados.ContainsKey(g) && !HablaEn(a, t)) a.Animar(t, TipoDeAnimacion.Gesto, g);
                    t += ch.cada * (0.75f + (float)rnd.NextDouble() * 0.6f);
                }
            }
            CabezaDeQuienHabla(a);
            a.anim.Sort((x, y) => x.t.CompareTo(y.t));
            VolverALasPoses(a);
            a.caras.Sort((x, y) => x.t.CompareTo(y.t));
            a.miradas.Sort((x, y) => x.t.CompareTo(y.t));
            a.cabezas.Sort((x, y) => x.t.CompareTo(y.t));
            a.visibilidad.Sort((x, y) => x.t.CompareTo(y.t));
        }
        _lineas.Sort((x, y) => x.t0.CompareTo(y.t0));
        _efectos.Sort((x, y) => x.t.CompareTo(y.t));
        _planosPedidos.Sort((x, y) => x.t0.CompareTo(y.t0));
        _fin += 0.5f;
    }

    /// El gesto de una frase que no trae uno, por lo que dice y la cara con que se dice
    /// (ActuacionAutomatica.ParaDecir). «sinGesto» si la frase es un silencio y no lleva ninguno;
    /// null si no pide nada en particular. No repite el mismo gesto dos veces seguidas en poco tiempo.
    private string GestoSegunLaFrase(ActorH a, LineaHorneada linea, out bool sinGesto)
    {
        sinGesto = false;
        _caraDeLinea.TryGetValue(linea, out var cara);
        var opciones = ActuacionAutomatica.ParaDecir(TextoDe(linea.clave) ?? linea.clave, cara);
        if (opciones == null) return null;
        if (opciones.Length == 0) { sinGesto = true; return null; }
        _ultimoGestoElegido.TryGetValue(a.id, out var anterior);
        bool reciente = anterior.gesto != null && linea.t0 - anterior.tiempo < 8f;
        _ultimasReaccionesPorActor.TryGetValue(a.id, out var reaccion);
        foreach (string g in opciones)
        {
            if (!a.estados.ContainsKey(g)) continue;
            // Acaba de reaccionar así como oyente (el respingo al aviso y, acto seguido, su «¡...!»): ya lo está haciendo.
            if (g == reaccion.gesto && linea.t0 - reaccion.tiempo < 3f && linea.t0 >= reaccion.tiempo) { sinGesto = true; return null; }
            if (reciente && g == anterior.gesto) continue;
            _ultimoGestoElegido[a.id] = (linea.t0, g);
            return g;
        }
        return null;
    }

    /// Tras un gesto de medio cuerpo, el actor vuelve a la pose de medio cuerpo que sostenía
    /// («bucle BrazosEnJarras» y un asentimiento: asiente y sigue en jarras). El gesto se queda
    /// con la capa y, sin esta orden, la pose se pierde hasta el siguiente «bucle». Solo poses en
    /// bucle; si antes de acabar el gesto llega otra orden de animación, manda ella.
    private static void VolverALasPoses(ActorH a)
    {
        const float Fundido = 0.3f;
        var vueltas = new List<OrdenDeAnimacion>();
        string pose = null, poseAlta = null;
        for (int i = 0; i < a.anim.Count; i++)
        {
            var o = a.anim[i];
            if (o.tipo == TipoDeAnimacion.Reposo) { pose = null; poseAlta = null; continue; }
            if (o.tipo == TipoDeAnimacion.PoseAlta) { poseAlta = o.estado; continue; }
            if (o.tipo == TipoDeAnimacion.SoltarPoseAlta) { poseAlta = null; continue; }
            if (o.tipo == TipoDeAnimacion.Bucle)
            {
                bool deMedioCuerpo = !o.congelar && a.estados.TryGetValue(o.estado, out var info) && !info.cuerpoEntero && info.bucle;
                pose = deMedioCuerpo ? o.estado : null;
                continue;
            }
            if (o.tipo != TipoDeAnimacion.Gesto || (pose == null && poseAlta == null)) continue;
            if (!a.estados.TryGetValue(o.estado, out var gesto) || gesto.cuerpoEntero) continue;
            float fin = o.t + Mathf.Max(0.3f, gesto.largo * 0.9f);
            bool pisada = false;
            for (int j = i + 1; j < a.anim.Count && a.anim[j].t < fin + 0.05f; j++)
            {
                var siguiente = a.anim[j].tipo;
                if (siguiente == TipoDeAnimacion.Bucle || siguiente == TipoDeAnimacion.Reposo || siguiente == TipoDeAnimacion.Gesto ||
                    siguiente == TipoDeAnimacion.PoseAlta || siguiente == TipoDeAnimacion.SoltarPoseAlta) { pisada = true; break; }
            }
            if (pisada) continue;
            if (poseAlta != null) vueltas.Add(new OrdenDeAnimacion { t = fin, tipo = TipoDeAnimacion.PoseAlta, estado = poseAlta, fundido = Fundido });
            else vueltas.Add(new OrdenDeAnimacion { t = fin, tipo = TipoDeAnimacion.Bucle, estado = pose, fundido = Fundido });
        }
        if (vueltas.Count == 0) return;
        a.anim.AddRange(vueltas);
        a.anim.Sort((x, y) => x.t.CompareTo(y.t));
    }

    /// ¿Lleva en t una pose de medio cuerpo encima de su bucle de cuerpo entero?
    private static bool ConPoseAltaEn(ActorH a, float t)
    {
        float ultima = float.NegativeInfinity;
        bool puesta = false;
        foreach (var o in a.anim)
        {
            if (o.t > t + 1e-3f || o.t < ultima) continue;
            if (o.tipo == TipoDeAnimacion.PoseAlta) { puesta = true; ultima = o.t; }
            else if (o.tipo == TipoDeAnimacion.SoltarPoseAlta || o.tipo == TipoDeAnimacion.Reposo) { puesta = false; ultima = o.t; }
        }
        return puesta;
    }

    private static bool HablaEn(ActorH a, float t) => a.habla.Any(h => t >= h.t0 - 0.3f && t <= h.t1 + 0.3f);

    private static string BucleEn(ActorH a, float t)
    {
        string b = null;
        foreach (var o in a.anim)
        {
            if (o.t > t + 1e-3f) break;
            if (o.tipo == TipoDeAnimacion.Bucle) b = o.estado;
            else if (o.tipo == TipoDeAnimacion.Reposo) b = null;
        }
        return b;
    }

    // ── Bandas de cine ──────────────────────────────────────────────────────────────────────

    /// Una frase sale en bocadillo o en cinemática: un guion que dice frases en subtítulo es una
    /// cinemática y lleva bandas de cine, porque el subtítulo va en la banda de abajo
    /// (`SpeechBubbleUI`). Si el guion no da ninguna orden «bandas», las pone al empezar y las
    /// quita al acabar; si las lleva él, el parte avisa de cada subtítulo que quede sin bandas.
    /// Va antes de las cámaras: el encuadre cuenta con lo que tapan (`BandaEn`).
    private void BandasDeCinematica()
    {
        var subtitulos = _lineas
            .Where(l => (l.presentacion >= 0 ? (PresentacionDeTexto)l.presentacion : _g.texto) != PresentacionDeTexto.Bocadillo)
            .ToList();
        if (subtitulos.Count == 0) return;
        if (!_efectos.Any(e => e.beat is BandasDeCineBeat))
        {
            float fin = _efectos.Count > 0 ? Mathf.Max(_fin, _efectos[_efectos.Count - 1].t) : _fin;
            _efectos.Insert(0, new EfectoHorneado { t = 0f, beat = new BandasDeCineBeat { mostrar = true, esperar = false } });
            _efectos.Add(new EfectoHorneado { t = fin, beat = new BandasDeCineBeat { mostrar = false, esperar = false } });
            return;
        }
        foreach (var l in subtitulos)
            if (BandaEn(l.t0) <= 0f)
                Aviso($"{Tiempo(l.t0)}: {l.clave} sale en subtítulo sin bandas de cine. Con subtítulos el guion es una cinemática: " +
                      "pon «bandas» antes de la frase o mueve el «bandas quita» detrás de ella.");
    }

    // ── Efectos ─────────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, Type> _tiposDeBeat;

    private float Efecto(GuionTexto.Orden o, float t)
    {
        string clase = o.args.FirstOrDefault();
        var beat = CrearBeat(clase, o.opciones, o);
        if (beat == null) return 0f;
        _efectos.Add(new EfectoHorneado { t = t, beat = beat, linea = o.linea,
            onlyIfFlag = o.Op("onlyIfFlag"), skipIfFlag = o.Op("skipIfFlag") });
        return o.OpF("dura", 0f);
    }

    internal SequenceBeat CrearBeat(string clase, Dictionary<string, string> campos, GuionTexto.Orden o)
    {
        if (_tiposDeBeat == null)
        {
            _tiposDeBeat = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in TypeCache.GetTypesDerivedFrom<SequenceBeat>())
                if (!t.IsAbstract) _tiposDeBeat[t.Name] = t;
        }
        if (string.IsNullOrEmpty(clase) || !_tiposDeBeat.TryGetValue(clase, out var tipo) && !_tiposDeBeat.TryGetValue(clase + "Beat", out tipo))
        {
            Error($"{o}: no existe el beat '{clase}'");
            return null;
        }
        if (tipo == typeof(GuionBeat) || tipo == typeof(WaitBeat) || tipo == typeof(ParallelBeat))
        {
            Error($"{o}: '{tipo.Name}' no se usa dentro de un guion (el guion ya hace eso a su manera)");
            return null;
        }
        var beat = (SequenceBeat)Activator.CreateInstance(tipo);
        foreach (var kv in campos)
        {
            if (kv.Key == "dura" || kv.Key == "onlyIfFlag" || kv.Key == "skipIfFlag") continue;
            if (beat is IConfigurableDesdeGuion configurable
                && configurable.Configurar(kv.Key, kv.Value, alias => _actores.TryGetValue(alias, out var ac) ? ac.id : alias))
                continue;
            var campo = tipo.GetField(kv.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (campo == null) { Error($"{o}: {tipo.Name} no tiene el campo '{kv.Key}'"); continue; }
            if (!Convertir(kv.Value, campo.FieldType, out object valor, o)) continue;
            campo.SetValue(beat, valor);
        }
        // Los ids de actor en un efecto pueden escribirse con su alias del guion.
        foreach (var campo in tipo.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (campo.FieldType != typeof(string)) continue;
            if (!(campo.Name.ToLowerInvariant().Contains("actor") || campo.Name.EndsWith("Id"))) continue;
            var v = campo.GetValue(beat) as string;
            if (v != null && _actores.TryGetValue(v, out var a)) campo.SetValue(beat, a.id);
        }
        return beat;
    }

    private bool Convertir(string texto, Type tipo, out object valor, GuionTexto.Orden o)
    {
        valor = null;
        try
        {
            if (tipo == typeof(string)) { valor = texto; return true; }
            if (tipo == typeof(float)) { valor = float.Parse(texto, CultureInfo.InvariantCulture); return true; }
            if (tipo == typeof(int)) { valor = int.Parse(texto, CultureInfo.InvariantCulture); return true; }
            if (tipo == typeof(bool)) { valor = texto == "si" || texto == "sí" || texto == "true" || texto == "1"; return true; }
            if (tipo.IsEnum) { valor = Enum.Parse(tipo, texto, true); return true; }
            if (tipo == typeof(Vector3))
            {
                var p = texto.Split(';', '|');
                if (p.Length != 3) p = texto.Split(',');
                valor = new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture));
                return true;
            }
            if (tipo == typeof(Color))
            {
                if (ColorUtility.TryParseHtmlString(texto.StartsWith("#") ? texto : "#" + texto, out var c)) { valor = c; return true; }
                var p = texto.Split(';', '|');
                valor = new Color(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture),
                    float.Parse(p[2], CultureInfo.InvariantCulture), p.Length > 3 ? float.Parse(p[3], CultureInfo.InvariantCulture) : 1f);
                return true;
            }
            if (tipo == typeof(List<string>)) { valor = texto.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(); return true; }
            if (typeof(UnityEngine.Object).IsAssignableFrom(tipo))
            {
                string ruta = texto;
                if (!ruta.StartsWith("Assets/") && !ruta.StartsWith("Packages/"))
                {
                    var guid = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(texto) + " t:" + tipo.Name)
                        .FirstOrDefault(gd => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(gd)) == Path.GetFileNameWithoutExtension(texto));
                    if (guid == null && texto.Length == 32) guid = texto;
                    ruta = guid != null ? AssetDatabase.GUIDToAssetPath(guid) : null;
                }
                valor = ruta != null ? AssetDatabase.LoadAssetAtPath(ruta, tipo) : null;
                if (valor == null) { Error($"{o}: no encuentro el asset '{texto}' ({tipo.Name})"); return false; }
                return true;
            }
        }
        catch (Exception ex)
        {
            Error($"{o}: no entiendo '{texto}' como {tipo.Name} ({ex.Message})");
            return false;
        }
        Error($"{o}: campos de tipo {tipo.Name} no se pueden escribir en el guion");
        return false;
    }

    /// Órdenes cortas para los efectos de siempre.
    private float Azucar(GuionTexto.Orden o, float t)
    {
        string a0 = o.args.Count > 0 ? o.args[0] : null;
        var campos = new Dictionary<string, string>(o.opciones);
        string clase;
        switch (o.verbo)
        {
            case "musica": clase = "MusicBeat"; if (a0 != null) campos["musicId"] = a0; break;
            case "silencio": clase = "MusicBeat"; campos["musicId"] = ""; if (a0 != null) campos["fadeOut"] = a0; break;
            case "sonido": clase = "SfxBeat"; if (a0 != null) campos["eventKey"] = a0; RenombrarActorOMarca(campos); break;
            case "ambiente": clase = "AmbienteBeat"; if (a0 != null) campos["loopId"] = a0; break;
            case "hora": clase = "TimeOfDayBeat"; if (a0 != null) campos["timeOfDay"] = a0; break;
            case "fundido": clase = "ScreenFadeBeat"; campos["fadeIn"] = a0 == "entra" ? "false" : "true"; campos["waitForEnd"] = "false";
                if (o.args.Count > 1) campos["duration"] = o.args[1]; break;
            case "destello": clase = "ScreenFlashBeat"; if (a0 != null) campos["color"] = a0; if (o.args.Count > 1) campos["duration"] = o.args[1]; break;
            case "sacudida": clase = "ShakeBeat"; if (a0 != null) campos["intensity"] = a0; if (o.args.Count > 1) campos["duration"] = o.args[1]; break;
            case "cutin": clase = "CutInBeat";
                if (a0 != null)
                {
                    if (a0 == "quita" || a0 == "oculta") campos["mostrar"] = "false";
                    else campos["actorId"] = _actores.TryGetValue(a0, out var a) ? a.id : a0;
                }
                break;
            case "vfx": clase = "VfxBeat"; if (a0 != null) campos["vfxPrefab"] = a0; RenombrarActorOMarca(campos); break;
            case "bandas": clase = "BandasDeCineBeat"; campos["mostrar"] = a0 == "quita" ? "false" : "true"; campos["esperar"] = "false"; break;
            case "lento": clase = "TimeScaleBeat"; if (a0 != null) campos["timeScale"] = a0; if (o.args.Count > 1) campos["rampDuration"] = o.args[1]; break;
            case "postproceso": clase = "PostprocesoBeat"; if (a0 != null) campos["perfil"] = a0; break;
            case "prop": clase = "SetPropActiveBeat"; if (a0 != null) campos["propId"] = a0; campos["active"] = o.args.Count > 1 && o.args[1] == "oculto" ? "false" : "true"; break;
            case "clima": clase = "WeatherBeat"; if (a0 != null) campos["fenomeno"] = a0; campos["encender"] = o.args.Count > 1 && o.args[1] == "fin" ? "false" : "true"; break;
            case "flag": clase = "SetFlagBeat"; if (a0 != null) campos["flag"] = a0; break;
            default: Error($"{o}: orden desconocida"); return 0f;
        }
        var beat = CrearBeat(clase, campos, o);
        if (beat == null) return 0f;
        _efectos.Add(new EfectoHorneado { t = t, beat = beat, linea = o.linea,
            onlyIfFlag = o.Op("onlyIfFlag"), skipIfFlag = o.Op("skipIfFlag") });
        return o.OpF("dura", 0f);
    }

    /// «en=X» de un efecto: un actor del REPARTO o un sitio. Un punto del guion que es una marca pasa
    /// con el nombre de la marca, que es lo que el efecto busca en el escenario.
    private void RenombrarActorOMarca(Dictionary<string, string> campos)
    {
        if (!campos.TryGetValue("en", out var en)) return;
        campos.Remove("en");
        if (_actores.TryGetValue(en, out var a)) campos["atActorId"] = a.id;
        else campos["markName"] = _g.puntos.FirstOrDefault(p => p.nombre == en)?.marca ?? en;
    }

    // ── Suelo ───────────────────────────────────────────────────────────────────────────────

    /// Escena del guion que se está horneando: su suelo manda sobre el de otras escenas abiertas.
    internal static UnityEngine.SceneManagement.Scene EscenaDelGuion;

    private const float DistanciaDeMallaVecina = 4f;

    internal static Vector3 Suelo(Vector3 p)
    {
        // Un 0 puede ser «sin altura» o el suelo de verdad. Los interiores están a ras de 0 con el
        // pueblo encima (a ~26 m): si justo ahí hay suelo de la escena del guion, es ese, y no el
        // terreno ni el NavMesh del exterior.
        if (p.y == 0f && EscenaDelGuion.IsValid())
        {
            float mejor = float.NegativeInfinity;
            float mejorPiso = float.NegativeInfinity;
            int layerFloor = LayerMask.NameToLayer("Floor");
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 2.5f, p.z), Vector3.down, 6f, SinZonasDeAmbiente, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.gameObject.scene != EscenaDelGuion || !EsColliderDeSuelo(h.collider)) continue;
                if ((layerFloor >= 0 && h.collider.gameObject.layer == layerFloor) || h.collider.name.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (h.point.y > mejorPiso) mejorPiso = h.point.y;
                }
                else if (h.point.y > mejor)
                {
                    mejor = h.point.y;
                }
            }
            if (!float.IsNegativeInfinity(mejorPiso)) return new Vector3(p.x, mejorPiso, p.z);
            if (!float.IsNegativeInfinity(mejor)) return new Vector3(p.x, mejor, p.z);
        }
        if (EscenaDelGuion.IsValid() && EscenaDelGuion.path.IndexOf("Interior", StringComparison.OrdinalIgnoreCase) >= 0)
            return new Vector3(p.x, p.y, p.z);
        float y = p.y;
        // El NavMesh solo cuenta si tiene un punto justo debajo: uno a varios metros (un saliente,
        // una loma sin NavMesh) dejaría la altura en 0 y el rayo saldría desde bajo tierra.
        bool encontrado = NavMesh.SamplePosition(new Vector3(p.x, p.y == 0f ? 100f : p.y, p.z), out var hit, p.y == 0f ? 200f : 3f, NavMesh.AllAreas);
        bool hayNav = encontrado && Mathf.Abs(hit.position.x - p.x) < 0.6f && Mathf.Abs(hit.position.z - p.z) < 0.6f;
        // Sin malla justo debajo (un banco, el umbral de una puerta bajo un alero) vale la de al lado
        // como referencia: el suelo está a su altura. Sin ella, el rayo desde lo alto se quedaba con
        // el tejado o el asiento y el actor andaba por encima (INC-1032).
        if (!hayNav && encontrado && new Vector2(hit.position.x - p.x, hit.position.z - p.z).sqrMagnitude < DistanciaDeMallaVecina * DistanciaDeMallaVecina)
            hayNav = true;
        if (hayNav) y = hit.position.y;
        var desde = new Vector3(p.x, (hayNav ? y : (p.y == 0f ? 300f : p.y)) + 2.5f, p.z);
        if (Physics.Raycast(desde, Vector3.down, out var rh, 6f + (p.y == 0f && !hayNav ? 600f : 0f), SinZonasDeAmbiente, QueryTriggerInteraction.Ignore))
        {
            if (!hayNav || Mathf.Abs(rh.point.y - y) < 0.35f) y = rh.point.y;
        }
        return new Vector3(p.x, y, p.z);
    }

    private static bool EsColliderDeSuelo(Collider col)
    {
        if (col == null || col.isTrigger) return false;
        var go = col.gameObject;
        if (go.GetComponent<NavMeshObstacle>() != null) return false;
        int layer = go.layer;
        if (layer == 2 /* Ignore Raycast */ || layer == 8 /* Interactable */ || layer == 16 /* Obstacle */) return false;
        var modificador = go.GetComponent<NavMeshModifier>();
        if (modificador != null && modificador.overrideArea && modificador.area == 1 /* Not Walkable */) return false;
        return true;
    }
}
#endif
