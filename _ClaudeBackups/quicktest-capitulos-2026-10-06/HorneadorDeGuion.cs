#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
            horno.Error("El horneado se ha parado: " + ex.Message + "\n" + ex.StackTrace);
        }
        finally { EditorUtility.ClearProgressBar(); }
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
    public readonly List<OrdenDeAnimacion> anim = new();
    public readonly List<OrdenDeCara> caras = new();
    public readonly List<(float t, bool visible)> visibilidad = new();
    public readonly List<float> comandos = new();             // instantes en que recibe órdenes
    public readonly List<(float t0, string con, float cada, int linea)> charlas = new();
    public readonly List<(float t0, float t1)> habla = new();
    public bool ocultoAlEmpezar;
    public string bucle;          // bucle en curso (en el instante del último comando procesado)
    public bool bucleDeCuerpo;    // ese bucle es de cuerpo entero

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
            if ((pos[i].p - pos[i - 1].p).sqrMagnitude > 1e-4f && pos[i].t - pos[i - 1].t > 0.02f)
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

    public void Animar(float t, TipoDeAnimacion tipo, string estado = null, bool congelar = false)
    {
        anim.Add(new OrdenDeAnimacion { t = t, tipo = tipo, estado = estado, congelar = congelar });
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
    private readonly Dictionary<string, float> _etiquetas = new();
    private readonly List<LineaHorneada> _lineas = new();
    private readonly List<EfectoHorneado> _efectos = new();
    private readonly List<PlanoPedido> _planosPedidos = new();
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
        if (!_escena.IsValid() || !_escena.isLoaded) { Error($"No puedo abrir la escena '{_g.escena}'."); return null; }
        _stage = CapturaDeEscenario.BuscarStage(_escena);
        Physics.SyncTransforms();
        _carpetaSalida = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, CapturaDeEscenario.CarpetaDeEscena(_g.escena, _g.zona), _g.nombre);
        Directory.CreateDirectory(_carpetaSalida);

        if (!string.IsNullOrEmpty(_g.prepara))
        {
            var clase = AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetType(_g.prepara)).FirstOrDefault(t => t != null);
            var metodo = clase?.GetMethod("Asegurar", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (metodo == null) Error($"PREPARA {_g.prepara}: no existe esa clase con un Asegurar() estático");
            else metodo.Invoke(null, null);
        }
        PrepararReparto();
        PrepararPuntos();
        if (Errores.Count > 0) return null;

        EditorUtility.DisplayProgressBar("Hornear guion", "Tiempos y caminos…", 0.2f);
        Repartir();
        if (Errores.Count > 0) return null;

        EditorUtility.DisplayProgressBar("Hornear guion", "Actuación…", 0.5f);
        Rematar();

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
                if (t.name == nombre) { pos = t.position; return true; }
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
            if (o.verbo != "cara" && o.verbo != "mira") a.comandos.Add(t);
            return ProcesarActor(a, o, t, dePista);
        }
        switch (o.verbo)
        {
            case "plano":
                _planosPedidos.Add(new PlanoPedido { orden = o, t0 = t });
                return 0f;
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
                if (Objetivo(o.args.FirstOrDefault(), o.linea, out var m)) a.Mirar(t, m);
                return 0f;
            }
            case "gesto":
            {
                string estado = o.args.FirstOrDefault();
                if (!Estado(a, estado, o)) return 0f;
                int veces = Mathf.Max(1, (int)o.OpF("veces", 1));
                float largo = a.estados[estado].largo;
                for (int i = 0; i < veces; i++) a.Animar(t + i * largo * 0.92f, TipoDeAnimacion.Gesto, estado);
                float dura = o.opciones.ContainsKey("dura") ? o.OpF("dura", largo) : largo * veces * 0.92f;
                if (a.bucle != null && a.estados[estado].cuerpoEntero)
                    a.Animar(t + dura, TipoDeAnimacion.Bucle, a.bucle);
                return dura;
            }
            case "bucle":
            {
                string estado = o.args.FirstOrDefault();
                if (!Estado(a, estado, o)) return 0f;
                if (!a.estados[estado].bucle && !o.Tiene("congela"))
                    Aviso($"{o}: '{estado}' no es un bucle; se quedará en su último fotograma (añade «congela» si es lo que quieres)");
                a.Animar(t, TipoDeAnimacion.Bucle, estado, o.Tiene("congela") || !a.estados[estado].bucle);
                a.bucle = estado;
                a.bucleDeCuerpo = a.estados[estado].cuerpoEntero;
                return o.opciones.ContainsKey("dura") ? o.OpF("dura", 0f) : 0f;
            }
            case "reposo":
                a.Animar(t, TipoDeAnimacion.Reposo);
                a.bucle = null; a.bucleDeCuerpo = false;
                return 0f;
            case "cara":
            {
                var e = Emocion(o.args.FirstOrDefault(), o);
                if (e == NPCEmotion.None) return 0f;
                a.caras.Add(new OrdenDeCara { t = t, emocion = e, dura = o.OpF("dura", 0f) });
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
        }
        Error($"{o}: acción desconocida");
        return 0f;
    }

    /// Antes de echar a andar se suelta un bucle de cuerpo entero (bailando no se anda).
    private void SoltarBucleSiAnda(ActorH a, float t, bool haraCuerpo)
    {
        if (a.bucle != null && (a.bucleDeCuerpo || !haraCuerpo))
        {
            a.Animar(t, TipoDeAnimacion.Reposo);
            a.bucle = null; a.bucleDeCuerpo = false;
        }
    }

    private bool Estado(ActorH a, string estado, GuionTexto.Orden o)
    {
        if (string.IsNullOrEmpty(estado)) { Error($"{o}: falta el nombre de la animación"); return false; }
        if (a.estados.Count == 0) { a.estados[estado] = new InfoDeEstado { largo = 2f }; return true; }
        if (a.estados.ContainsKey(estado)) return true;
        var parecidos = a.estados.Keys.Where(k => k.IndexOf(estado, StringComparison.OrdinalIgnoreCase) >= 0 || estado.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0).Take(4);
        Error($"{o}: {a.alias} no tiene la animación '{estado}'. ¿{string.Join(", ", parecidos)}? (ver animaciones.md)");
        return false;
    }

    private static readonly Dictionary<string, NPCEmotion> EmocionesEnCastellano = new(StringComparer.OrdinalIgnoreCase)
    {
        { "neutral", NPCEmotion.Neutral }, { "alegre", NPCEmotion.Happy }, { "feliz", NPCEmotion.Happy },
        { "triste", NPCEmotion.Sad }, { "enfado", NPCEmotion.Angry }, { "enfadado", NPCEmotion.Angry },
        { "sorpresa", NPCEmotion.Surprised }, { "miedo", NPCEmotion.Scared }, { "pensativo", NPCEmotion.Thinking },
        { "cansado", NPCEmotion.Tired }, { "picaro", NPCEmotion.Smirk }, { "preocupado", NPCEmotion.Worried },
        { "decidido", NPCEmotion.Determined }, { "aliviado", NPCEmotion.Relieved }, { "confuso", NPCEmotion.Confused },
        { "ilusionado", NPCEmotion.Excited }, { "molesto", NPCEmotion.Annoyed }, { "agradecido", NPCEmotion.Grateful },
    };

    private NPCEmotion Emocion(string nombre, GuionTexto.Orden o)
    {
        if (string.IsNullOrEmpty(nombre)) { Error($"{o}: falta la emoción"); return NPCEmotion.None; }
        if (EmocionesEnCastellano.TryGetValue(nombre, out var e)) return e;
        if (Enum.TryParse(nombre, true, out e)) return e;
        Error($"{o}: emoción desconocida '{nombre}' ({string.Join(", ", EmocionesEnCastellano.Keys)})");
        return NPCEmotion.None;
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
        string clave = o.args.FirstOrDefault();
        if (string.IsNullOrEmpty(clave)) { Error($"{o}: ¿qué frase?"); return 0f; }
        float dura = DuracionDeVoz(clave, o);
        var linea = new LineaHorneada { t0 = t, t1 = t + dura, actor = a.id, clave = clave, sinPose = o.Tiene("sin_pose"),
            onlyIfFlag = o.Op("onlyIfFlag"), skipIfFlag = o.Op("skipIfFlag") };
        if (o.opciones.TryGetValue("texto", out var pres))
            linea.presentacion = pres == "bocadillo" ? 0 : pres == "subtitulo_grande" ? 2 : 1;
        _lineas.Add(linea);
        a.habla.Add((t, t + dura));
        _ultimoHablante = a.alias;

        if (o.opciones.TryGetValue("gesto", out var g) && Estado(a, g, o))
        {
            linea.gesto = g;
            a.Animar(t + 0.08f, TipoDeAnimacion.Gesto, g);
        }
        if (o.opciones.TryGetValue("mira", out var x) && Objetivo(x, o.linea, out var m)) a.Mirar(t, m);
        if (o.opciones.TryGetValue("cara", out var c))
        {
            var e = Emocion(c, o);
            if (e != NPCEmotion.None) a.caras.Add(new OrdenDeCara { t = t, emocion = e, dura = dura + 0.4f });
        }

        // Los que escuchan se giran hacia quien habla, cada uno a su ritmo.
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
        int k = 0;
        foreach (var b in oyentes)
        {
            if (b == a || b.AndandoEn(t)) continue;
            b.Mirar(t + 0.15f + (k++ % 4) * 0.12f, new ClaveDeMirada { tipo = TipoDeMirada.Actor, actor = a.id });
        }
        return dura;
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
                if (anda || bucle != null || linea.sinPose) { linea.sinPose = true; continue; }
                a.Animar(t0, TipoDeAnimacion.HablaEmpieza);
                a.Animar(t1 + 0.05f, TipoDeAnimacion.HablaTermina);
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
            a.anim.Sort((x, y) => x.t.CompareTo(y.t));
            a.caras.Sort((x, y) => x.t.CompareTo(y.t));
            a.miradas.Sort((x, y) => x.t.CompareTo(y.t));
            a.visibilidad.Sort((x, y) => x.t.CompareTo(y.t));
        }
        _lineas.Sort((x, y) => x.t0.CompareTo(y.t0));
        _efectos.Sort((x, y) => x.t.CompareTo(y.t));
        _planosPedidos.Sort((x, y) => x.t0.CompareTo(y.t0));
        _fin += 0.5f;
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
        if (tipo == typeof(GuionBeat) || tipo == typeof(ShotBeat) || tipo == typeof(WaitBeat) || tipo == typeof(ParallelBeat))
        {
            Error($"{o}: '{tipo.Name}' no se usa dentro de un guion (el guion ya hace eso a su manera)");
            return null;
        }
        var beat = (SequenceBeat)Activator.CreateInstance(tipo);
        foreach (var kv in campos)
        {
            if (kv.Key == "dura" || kv.Key == "onlyIfFlag" || kv.Key == "skipIfFlag") continue;
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
            case "cutin": clase = "CutInBeat"; if (a0 != null) campos["actorId"] = a0; break;
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

    private void RenombrarActorOMarca(Dictionary<string, string> campos)
    {
        if (!campos.TryGetValue("en", out var en)) return;
        campos.Remove("en");
        if (_actores.TryGetValue(en, out var a)) campos["atActorId"] = a.id;
        else campos["markName"] = en;
    }

    // ── Suelo ───────────────────────────────────────────────────────────────────────────────

    internal static Vector3 Suelo(Vector3 p)
    {
        float y = p.y;
        bool hayNav = NavMesh.SamplePosition(new Vector3(p.x, p.y == 0f ? 100f : p.y, p.z), out var hit, p.y == 0f ? 200f : 3f, NavMesh.AllAreas);
        if (hayNav && Mathf.Abs(hit.position.x - p.x) < 0.6f && Mathf.Abs(hit.position.z - p.z) < 0.6f) y = hit.position.y;
        var desde = new Vector3(p.x, (hayNav ? y : (p.y == 0f ? 300f : p.y)) + 2.5f, p.z);
        if (Physics.Raycast(desde, Vector3.down, out var rh, 6f + (p.y == 0f && !hayNav ? 600f : 0f), ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hayNav || Mathf.Abs(rh.point.y - y) < 0.35f) y = rh.point.y;
        }
        return new Vector3(p.x, y, p.z);
    }
}
#endif
