using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Llegada = PuntoDeRuta.Llegada;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Las tres pruebas de candidata (tramo 4): la carrera de obstáculos, la escalada de la torre y la
/// puntería. Cada una es una prueba genérica (<see cref="PruebaDeRecorrido"/> o
/// <see cref="PruebaDeObjetivos"/>) con el grupo como etapas (<see cref="GrupoEnLaPrueba"/>), activa
/// solo en su tramo (<see cref="ActivoEnTramo"/>: de su señal *_EMPIEZA a su *_GANADA), que empieza
/// sola al activarse y emite su *_GANADA al ganarse (un <see cref="SignalEmitter"/> manual).
/// Lo que se queda en el mundo después (el trampolín de la carrera, la escalera de mano de la torre)
/// va en «Pruebas/Mundo». Los puntos, rivales, dianas y frases se retocan en las tablas de abajo;
/// el informe del menú avisa de los tramos de la carrera sin suelo o con escalones altos.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string Interaccion = Pack + "/Prefabs/Interaction";
    private const string Decoracion = Pack + "/Prefabs/Environment Decorations";
    private const string PersonajesDeDemo = Demo + "/Prefabs/Characters";
    private const string VfxDeCorazones = "Assets/_VFX/Hechizos/VFX_Hechizo_CorazonEstelar_Impacto.prefab";

    private sealed class PuntoDeTabla
    {
        public string Nombre, Referencia;
        public Vector3 Aprox, Desplazamiento, Zona;
        public Llegada Llegada;
        public float AlturaDelSalto;
        public bool Control;
    }

    private sealed class RivalDeTabla
    {
        public string Nombre, Prefab;
        public float Carril, Fila, Habilidad;
    }

    private sealed class DianaDeTabla
    {
        public string Prefab;
        public Vector3 Desplazamiento;
        public float Tamano;
    }

    // ── Tabla de la carrera ─────────────────────────────────────────────────────────────────
    // De la puerta de plasma del este (andén a y≈4) al trono: rampa al nivel de las tartas (y≈8),
    // el trampolín al pie de la muralla del castillo y la meta delante del trono (y≈14). Las alturas
    // salen de los saltos de la IA del pack (JumpObstacle) y del NavMesh del pack. Cada punto: nombre,
    // referencia (una marca o un objeto de la escena, el más cercano a la posición aproximada; sin
    // referencia, la posición es la de la tabla), desplazamiento en ejes del mundo, cómo se llega a él
    // y si es punto de control (con su caja). Se asientan en el suelo que haya cerca.
    private static List<PuntoDeTabla> RutaDeLaCarrera() => new List<PuntoDeTabla>
    {
        R("Salida", "M_Carrera_Salida", SinAprox),
        R("Fin del andén", null, V(114.2f, 4.2f, 0.6f)),
        R("Rampa", null, V(114.6f, 6.2f, 4.8f)),
        R("Las tartas", null, V(110.4f, 8.1f, 6.4f), control: true, zona: V(7f, 4f, 7f)),
        R("Entre tartas", null, V(105.4f, 8.1f, 7.4f)),
        R("Antes del trampolín", null, V(99.6f, 8.1f, 10.4f)),
        R("Trampolín", "Trampoline_1_02", V(94.89f, 8.36f, 11.82f)),
        R("La muralla", null, V(91.6f, 14.1f, 11.6f), Llegada.Saltando, 2.5f, true, V(7f, 4f, 7f)),
        R("Hacia el trono", null, V(90f, 14.1f, 6.5f)),
        R("Meta", "M_Carrera_Meta", SinAprox, desplazamiento: V(2.5f, 0f, 0f), control: true, zona: V(5f, 4f, 6f)),
    };

    /// <summary>Rivales de la carrera: prefab de la demo del pack, carril (m a la derecha de la ruta), fila en la salida y habilidad.</summary>
    private static readonly RivalDeTabla[] RivalesDeLaCarrera =
    {
        new RivalDeTabla { Nombre = "Grummy", Prefab = PersonajesDeDemo + "/Grummy01.prefab", Carril = -1.4f, Fila = 0.5f, Habilidad = 1f },
        new RivalDeTabla { Nombre = "Marshie", Prefab = PersonajesDeDemo + "/Marshie_02.prefab", Carril = -0.7f, Fila = 1.3f, Habilidad = 0.96f },
        new RivalDeTabla { Nombre = "Sweetie", Prefab = PersonajesDeDemo + "/Sweetie_01.prefab", Carril = 0.7f, Fila = 1.3f, Habilidad = 0.98f },
        new RivalDeTabla { Nombre = "Marshie de menta", Prefab = PersonajesDeDemo + "/Marshie_03.prefab", Carril = 1.4f, Fila = 0.5f, Habilidad = 0.93f },
    };
    private const float VelocidadDeLosRivales = 5f;

    // Frases de la carrera (claves de cinematics_es/en.json) y quién las dice.
    private static readonly (Slot, string)[] CarreraAlEmpezar =
        { (Slot.Liam, "CHUCHELANDIA_CARRERA_INICIO_01"), (Slot.Estela, "CHUCHELANDIA_CARRERA_INICIO_02"), (Slot.Will, "CHUCHELANDIA_CARRERA_INICIO_03") };
    private static readonly (Slot, string)[] CarreraAlReintentar =
        { (Slot.Will, "CHUCHELANDIA_CARRERA_REINTENTO_01"), (Slot.Liam, "CHUCHELANDIA_CARRERA_REINTENTO_02"), (Slot.Will, "CHUCHELANDIA_CARRERA_REINTENTO_03") };
    private static readonly (Slot, string)[] CarreraAlGanar =
        { (Slot.Liam, "CHUCHELANDIA_CARRERA_GANADA_01"), (Slot.Estela, "CHUCHELANDIA_CARRERA_GANADA_02") };
    private static readonly string[] WillAlPasar = { "CHUCHELANDIA_CARRERA_WILL_REBOTA_01", "CHUCHELANDIA_CARRERA_WILL_REBOTA_02" };
    private static readonly string[] LiamAlPasar = { "CHUCHELANDIA_CARRERA_LIAM_DIGNO_01" };

    // ── Tabla de la escalada ────────────────────────────────────────────────────────────────
    // La torre de M_Torre_* (Tower_01 + Tower_03 + TowerCap_04). Estela trepa por el lado de
    // M_Torre_Arranque (pared en la capa «Climb»); el rival sube por una escalera de mano del pack en el
    // lado contrario. Las medidas salen de los modelos de la torre.
    private static readonly RivalDeTabla RivalDeLaTorre =
        new RivalDeTabla { Nombre = "Grummy trepador", Prefab = PersonajesDeDemo + "/Grummy01.prefab", Habilidad = 1f };
    private const float RivalDeLaTorreCorriendo = 3.5f, RivalDeLaTorreTrepando = 2.1f;
    private static readonly (Slot, string)[] TorreAlEmpezar = { (Slot.Liam, "CHUCHELANDIA_TORRE_INICIO_01") };
    private static readonly (Slot, string)[] TorreAlReintentar =
        { (Slot.Will, "CHUCHELANDIA_TORRE_REINTENTO_01"), (Slot.Liam, "CHUCHELANDIA_TORRE_REINTENTO_02") };
    private static readonly (Slot, string)[] TorreAlGanar = { (Slot.Will, "CHUCHELANDIA_TORRE_GANADA_01") };

    // ── Tabla de la puntería ────────────────────────────────────────────────────────────────
    // Dianas flotantes delante de M_Tiro_Puesto (desplazamiento en el marco del puesto: z hacia donde mira),
    // con su tamaño en metros. Hay que reventar DianasParaGanar antes de SegundosDelTiro.
    private static readonly DianaDeTabla[] DianasDelTiro =
    {
        new DianaDeTabla { Prefab = Decoracion + "/AirBalloon_01.prefab", Desplazamiento = V(-6f, 5f, 14f), Tamano = 2.4f },
        new DianaDeTabla { Prefab = Interaccion + "/Gem_02.prefab", Desplazamiento = V(0f, 7f, 18f), Tamano = 1.4f },
        new DianaDeTabla { Prefab = Decoracion + "/AirBalloon_02.prefab", Desplazamiento = V(6f, 4f, 13f), Tamano = 2.4f },
        new DianaDeTabla { Prefab = Interaccion + "/Diamond_01.prefab", Desplazamiento = V(-11f, 3.5f, 10f), Tamano = 1.4f },
        new DianaDeTabla { Prefab = Interaccion + "/Gem_04.prefab", Desplazamiento = V(11f, 6f, 11f), Tamano = 1.4f },
        new DianaDeTabla { Prefab = Decoracion + "/AirBalloon_01.prefab", Desplazamiento = V(-3f, 9f, 24f), Tamano = 2.6f },
        new DianaDeTabla { Prefab = Interaccion + "/Gem_05.prefab", Desplazamiento = V(3.5f, 3f, 9f), Tamano = 1.3f },
        new DianaDeTabla { Prefab = Interaccion + "/Gem_06.prefab", Desplazamiento = V(9f, 8.5f, 20f), Tamano = 1.4f },
    };
    private const int DianasParaGanar = 6;
    private const float SegundosDelTiro = 45f;
    private static readonly (Slot, string)[] TiroAlEmpezar = { (Slot.Will, "CHUCHELANDIA_TIRO_INICIO_01") };
    private static readonly (Slot, string)[] TiroAlReintentar =
        { (Slot.Liam, "CHUCHELANDIA_TIRO_REINTENTO_01"), (Slot.Will, "CHUCHELANDIA_TIRO_REINTENTO_02") };
    private static readonly (Slot, string)[] TiroAlGanar = { (Slot.Liam, "CHUCHELANDIA_TIRO_GANADA_01") };

    // ── Montaje ─────────────────────────────────────────────────────────────────────────────

    private static void MontarPruebas(Scene escena, Transform padre, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var objetos = TodosLosObjetos(escena);
        var mundo = Hijo(padre, "Mundo");
        MontarCarrera(padre, mundo, marcas, objetos, informe);
        MontarEscalada(padre, mundo, marcas, objetos, informe);
        MontarPunteria(padre, marcas, informe);
    }

    private static void MontarCarrera(Transform pruebas, Transform mundo, Dictionary<string, Transform> marcas,
                                      List<Transform> objetos, StringBuilder informe)
    {
        var raiz = PruebaEnTramo(pruebas, "Carrera de obstáculos", Senal.CarreraEmpieza, Senal.CarreraGanada);
        var tabla = RutaDeLaCarrera();
        var posiciones = new List<Vector3>();
        foreach (var p in tabla) posiciones.Add(Situar(p, marcas, objetos, informe));
        var ruta = CrearRuta(raiz, "Ruta", tabla, posiciones);

        int iTrampolin = tabla.FindIndex(p => p.Nombre == "Trampolín");
        LanzadorDeSalto trampolin = null;
        if (iTrampolin >= 0 && iTrampolin + 1 < tabla.Count)
        {
            trampolin = MontarTrampolin(mundo, objetos, ruta.Posicion(iTrampolin), ruta.Posicion(iTrampolin + 1), informe);
            if (trampolin != null) Fijar(ruta.Punto(iTrampolin), ("trampolin", trampolin));
        }

        var rivales = new List<CorredorDeRuta>();
        var padreDeRivales = Hijo(raiz, "Rivales");
        foreach (var r in RivalesDeLaCarrera)
        {
            var c = CrearRival(padreDeRivales, r, ruta, VelocidadDeLosRivales, 1.8f, informe);
            if (c != null) rivales.Add(c);
        }

        ZonaSinVuelo(raiz, posiciones);

        var go = new GameObject("Prueba: carrera");
        go.transform.SetParent(raiz, false);
        var prueba = go.AddComponent<PruebaDeRecorrido>();
        Fijar(prueba, ("salidaDelJugador", ruta.Punto(0).transform), ("ruta", ruta), ("hayQueGanar", true));
        FijarLista(prueba, "rivales", rivales);

        var grupo = go.AddComponent<GrupoEnLaPrueba>();
        FijarFrases(grupo, CarreraAlEmpezar, CarreraAlReintentar, CarreraAlGanar);
        var puestos = new List<PuestoDeTabla>();
        if (iTrampolin >= 0)
            puestos.Add(new PuestoDeTabla { Personaje = Slot.Will, Sitio = ruta.Punto(iTrampolin).transform, Numerito = GrupoEnLaPrueba.Numerito.Botar,
                                            Trampolin = trampolin, Frases = WillAlPasar, Radio = 8f });
        if (marcas.TryGetValue("M_Carrera_Liam", out var liam))
        {
            var paseo = new GameObject("Paseo de Liam").transform;
            paseo.SetParent(raiz, false);
            paseo.SetPositionAndRotation(SueloCerca(liam.position + EnElMarcoDe(liam, V(2f, 0f, 4f))), liam.rotation);
            puestos.Add(new PuestoDeTabla { Personaje = Slot.Liam, Sitio = liam, Numerito = GrupoEnLaPrueba.Numerito.Pasear,
                                            OtroSitio = paseo, Frases = LiamAlPasar, Radio = 7f });
        }
        FijarPuestos(grupo, puestos);
        EmitirAlGanar(prueba, Senal.CarreraGanada);

        informe.AppendLine($"Carrera: {ruta.Cantidad} puntos ({ruta.Longitud:0} m), {rivales.Count} rivales, trampolín {(trampolin != null ? "sí" : "NO")}.");
        ValidarRuta("Carrera", ruta, informe);
    }

    private static void MontarEscalada(Transform pruebas, Transform mundo, Dictionary<string, Transform> marcas,
                                       List<Transform> objetos, StringBuilder informe)
    {
        if (!marcas.TryGetValue("M_Torre_Arranque", out var arranque)) { informe.AppendLine("Escalada: falta la marca M_Torre_Arranque."); return; }
        var bajo = MasCercano(objetos, "Tower_01", arranque.position);
        if (bajo == null) { informe.AppendLine("Escalada: no encuentro Tower_01 en la escena."); return; }
        Bounds b1 = BoundsDe(bajo.gameObject);
        var alto = MasCercano(objetos, "Tower_03", b1.center + Vector3.up * b1.extents.y * 2f);
        Bounds b3 = alto != null && Plano(alto.position - bajo.position).magnitude < 2f ? BoundsDe(alto.gameObject) : b1;

        Vector3 centro = Plano(b1.center);
        Vector3 lado = Plano(arranque.position - b1.center);
        lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.right;
        float r1 = Mathf.Min(b1.extents.x, b1.extents.z), r3 = Mathf.Min(b3.extents.x, b3.extents.z);
        float cima = Mathf.Max(b1.max.y, b3.max.y);
        float sueloJugador = SueloCerca(centro + lado * (r1 + 3.5f) + Vector3.up * (b1.min.y + 1f)).y;
        float sueloRival = SueloCerca(centro - lado * (r1 + 3.5f) + Vector3.up * (b1.min.y + 1f)).y;

        var raiz = PruebaEnTramo(pruebas, "Escalada de la torre", Senal.EscaladaEmpieza, Senal.EscaladaGanada);
        ParedDeEscalada(raiz, "Pared (tramo bajo)", centro, lado, r1, b1.min.y, b1.max.y);
        if (b3 != b1) ParedDeEscalada(raiz, "Pared (tramo alto)", centro, lado, r3, b3.min.y, b3.max.y);

        var concede = new GameObject("Zona: se puede trepar");
        concede.transform.SetParent(raiz, false);
        concede.transform.position = centro + Vector3.up * (b1.min.y + cima) * 0.5f;
        var cajaConcede = concede.AddComponent<BoxCollider>();
        cajaConcede.isTrigger = true;
        cajaConcede.size = new Vector3(2f * (r1 + 8f), cima - b1.min.y + 8f, 2f * (r1 + 8f));
        concede.AddComponent<ZonaQueConcedeHabilidades>();

        Vector3 Altura(Vector3 xz, float y) => new Vector3(xz.x, y, xz.z);
        var tablaJugador = new List<PuntoDeTabla>
        {
            R("Salida", null, SinAprox), R("Pie de la pared", null, SinAprox),
            R("Cima", null, SinAprox, Llegada.Trepando, control: true, zona: V(3f, 3f, 3f)),
        };
        var ruta = CrearRuta(raiz, "Ruta de Estela", tablaJugador, new List<Vector3>
        {
            Altura(centro + lado * (r1 + 3.5f), sueloJugador),
            Altura(centro + lado * (r1 + 0.45f), sueloJugador),
            Altura(centro + lado * (r3 + 0.45f), cima - 1f),
        });

        float grosor = MontarEscaleraDeMano(mundo, centro, -lado, r1, r3, b1.max.y, sueloRival, cima, informe);
        var tablaRival = new List<PuntoDeTabla>
        {
            R("Salida del rival", null, SinAprox), R("Pie de la escalera", null, SinAprox),
            R("Cima del rival", null, SinAprox, Llegada.Trepando),
        };
        var rutaRival = CrearRuta(raiz, "Ruta del rival", tablaRival, new List<Vector3>
        {
            Altura(centro - lado * (r1 + 3.5f), sueloRival),
            Altura(centro - lado * (r1 + grosor + 0.35f), sueloRival),
            Altura(centro - lado * (r3 + grosor + 0.35f), cima - 1f),
        });
        var rival = CrearRival(Hijo(raiz, "Rival"), RivalDeLaTorre, rutaRival, RivalDeLaTorreCorriendo, RivalDeLaTorreTrepando, informe);

        var go = new GameObject("Prueba: escalada");
        go.transform.SetParent(raiz, false);
        var prueba = go.AddComponent<PruebaDeRecorrido>();
        Fijar(prueba, ("salidaDelJugador", ruta.Punto(0).transform), ("ruta", ruta), ("hayQueGanar", true),
              ("llevarAlGanarA", ruta.Punto(0).transform), ("avisarDelPuesto", false), ("distanciaMaxima", 30f));
        FijarLista(prueba, "rivales", rival != null ? new List<CorredorDeRuta> { rival } : new List<CorredorDeRuta>());
        var grupo = go.AddComponent<GrupoEnLaPrueba>();
        FijarFrases(grupo, TorreAlEmpezar, TorreAlReintentar, TorreAlGanar);
        EmitirAlGanar(prueba, Senal.EscaladaGanada);

        informe.AppendLine($"Escalada: torre de {cima - b1.min.y:0.#} m (radio {r1:0.#}/{r3:0.#} m), pared en la capa Climb, rival {(rival != null ? "sí" : "NO")}.");
    }

    private static void MontarPunteria(Transform pruebas, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        if (!marcas.TryGetValue("M_Tiro_Puesto", out var puesto)) { informe.AppendLine("Puntería: falta la marca M_Tiro_Puesto."); return; }
        var raiz = PruebaEnTramo(pruebas, "Puntería", Senal.PunteriaEmpieza, Senal.PunteriaGanada);
        var salida = new GameObject("Puesto de tiro").transform;
        salida.SetParent(raiz, false);
        salida.SetPositionAndRotation(puesto.position, puesto.rotation);

        var go = new GameObject("Prueba: puntería");
        go.transform.SetParent(raiz, false);
        var contador = go.AddComponent<ActivationCounter>();
        Fijar(contador, ("requiredCount", DianasParaGanar));
        if (contador.onReset == null) contador.onReset = new UnityEvent();
        if (contador.onRequirementMet == null) contador.onRequirementMet = new UnityEvent();

        var corazones = AssetDatabase.LoadAssetAtPath<GameObject>(VfxDeCorazones);
        var padre = Hijo(raiz, "Dianas");
        int n = 0;
        foreach (var d in DianasDelTiro)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.Prefab);
            if (prefab == null) { informe.AppendLine($"Puntería: no encuentro {d.Prefab}."); continue; }
            var diana = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raiz.gameObject.scene);
            diana.name = $"Diana {++n} ({prefab.name})";
            diana.transform.SetParent(padre, true);
            diana.transform.SetPositionAndRotation(puesto.position + EnElMarcoDe(puesto, d.Desplazamiento), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            QuitarEstatico(diana);
            float escala = EscalarATamano(diana, d.Tamano);
            foreach (var c in diana.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            var esfera = diana.AddComponent<SphereCollider>();
            esfera.center = diana.transform.InverseTransformPoint(BoundsDe(diana).center);
            esfera.radius = d.Tamano * 0.5f / Mathf.Max(0.0001f, escala);
            var receptor = diana.AddComponent<ReceptorDeMagia>();
            Fijar(receptor, ("modo", (int)ReceptorDeMagia.Modo.Permanente), ("sfxAlActivar", ""), ("radioDeApuntado", 40f));
            FijarLista(receptor, "elementosAceptados", new List<Object>());
            diana.AddComponent<BalloonFloatingMotion>();
            var objetivo = diana.AddComponent<ObjetivoQueRevienta>();
            Fijar(objetivo, ("vfxAlReventar", corazones));

            UnityEventTools.AddPersistentListener(receptor.alActivarse, objetivo.Reventar);
            UnityEventTools.AddPersistentListener(receptor.alActivarse, contador.RegisterActivation);
            UnityEventTools.AddPersistentListener(contador.onReset, objetivo.Reponer);
        }

        var prueba = go.AddComponent<PruebaDeObjetivos>();
        Fijar(prueba, ("salidaDelJugador", salida), ("contador", contador), ("tiempoLimite", SegundosDelTiro));
        var grupo = go.AddComponent<GrupoEnLaPrueba>();
        FijarFrases(grupo, TiroAlEmpezar, TiroAlReintentar, TiroAlGanar);
        EmitirAlGanar(prueba, Senal.PunteriaGanada);

        informe.AppendLine($"Puntería: {n} dianas, hay que reventar {DianasParaGanar} en {SegundosDelTiro:0} s.");
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Objeto de una prueba, encendido solo entre su señal de inicio y la de ganada.</summary>
    private static Transform PruebaEnTramo(Transform pruebas, string nombre, string empieza, string acaba)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(pruebas, false);
        var tramo = pruebas.gameObject.AddComponent<ActivoEnTramo>();
        tramo.Tramo.empiezaConSenal.Add(empieza);
        tramo.Tramo.acabaConSenal.Add(acaba);
        tramo.Objetos.Add(go);
        go.SetActive(false);
        return go.transform;
    }

    /// <summary>Posición de un punto de la tabla: su marca u objeto de referencia (o la aproximada), desplazada y en el suelo.</summary>
    private static Vector3 Situar(PuntoDeTabla p, Dictionary<string, Transform> marcas, List<Transform> objetos, StringBuilder informe)
    {
        Vector3 origen = p.Aprox;
        if (!string.IsNullOrEmpty(p.Referencia))
        {
            if (marcas.TryGetValue(p.Referencia, out var marca)) origen = marca.position;
            else
            {
                var objeto = MasCercano(objetos, p.Referencia, p.Aprox);
                if (objeto != null) origen = objeto.position;
                else informe.AppendLine($"Punto «{p.Nombre}»: no encuentro «{p.Referencia}»; uso la posición de la tabla.");
            }
        }
        return SueloCerca(origen + p.Desplazamiento);
    }

    private static RutaDeRecorrido CrearRuta(Transform padre, string nombre, List<PuntoDeTabla> tabla, List<Vector3> posiciones)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ruta = go.AddComponent<RutaDeRecorrido>();
        for (int i = 0; i < tabla.Count; i++)
        {
            Vector3 hacia = i + 1 < posiciones.Count ? posiciones[i + 1] - posiciones[i] : posiciones[i] - posiciones[Mathf.Max(0, i - 1)];
            hacia = Plano(hacia);
            var punto = new GameObject(tabla[i].Nombre);
            punto.transform.SetParent(go.transform, false);
            punto.transform.SetPositionAndRotation(posiciones[i], hacia.sqrMagnitude > 0.001f ? Quaternion.LookRotation(hacia) : Quaternion.identity);
            var pr = punto.AddComponent<PuntoDeRuta>();
            var cambios = new List<(string, object)> { ("llegada", (int)tabla[i].Llegada), ("esPuntoDeControl", tabla[i].Control) };
            if (tabla[i].AlturaDelSalto > 0f) cambios.Add(("alturaDelSalto", tabla[i].AlturaDelSalto));
            if (tabla[i].Zona != Vector3.zero) cambios.Add(("tamanoDeLaZona", tabla[i].Zona));
            Fijar(pr, cambios.ToArray());
        }
        ruta.Recalcular();
        return ruta;
    }

    /// <summary>
    /// El lanzador del trampolín (<see cref="LanzadorDeSalto"/> en modo trampolín) sobre la lona del
    /// pack más cercana a <paramref name="sobre"/>, empujando hacia <paramref name="hacia"/>.
    /// </summary>
    private static LanzadorDeSalto MontarTrampolin(Transform mundo, List<Transform> objetos, Vector3 sobre, Vector3 hacia, StringBuilder informe)
    {
        var lona = MasCercano(objetos, "Trampoline_1_02", sobre);
        var base_ = MasCercano(objetos, "Trampoline_1_01", sobre);
        if (lona == null) { informe.AppendLine("Carrera: no encuentro el trampolín (Trampoline_1_02)."); return null; }
        Bounds b = BoundsDe(lona.gameObject);
        if (base_ != null && Plano(base_.position - lona.position).magnitude < 1f) b.Encapsulate(BoundsDe(base_.gameObject));

        var go = new GameObject("Trampolín de la carrera");
        go.transform.SetParent(mundo, false);
        Vector3 direccion = Plano(hacia - b.center);
        go.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y, b.center.z),
            direccion.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direccion) : Quaternion.identity);
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        float ancho = Mathf.Min(b.size.x, b.size.z) * 0.8f;
        caja.size = new Vector3(ancho, 0.6f, ancho);
        caja.center = new Vector3(0f, 0.2f, 0f);
        var lanzador = go.AddComponent<LanzadorDeSalto>();
        Fijar(lanzador, ("soloDesdeArriba", true), ("parteQueRebota", lona), ("empujeHaciaDelante", 2.5f));
        return lanzador;
    }

    private static CorredorDeRuta CrearRival(Transform padre, RivalDeTabla r, RutaDeRecorrido ruta, float velocidad, float alTrepar, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(r.Prefab);
        if (prefab == null) { informe.AppendLine($"Rival «{r.Nombre}»: no encuentro {r.Prefab}."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.gameObject.scene);
        go.name = "Rival: " + r.Nombre;
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(ruta.Posicion(0), ruta.Punto(0).transform.rotation);
        ApagarIaDeDemo(go);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.isTrigger = true;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;

        var animacion = go.AddComponent<AnimacionDeCorredorPorAnimator>();
        Fijar(animacion, ("animator", go.GetComponentInChildren<Animator>(true)));
        var corredor = go.AddComponent<CorredorDeRuta>();
        Fijar(corredor, ("ruta", ruta), ("velocidad", velocidad), ("velocidadAlTrepar", alTrepar), ("habilidad", r.Habilidad),
              ("carril", r.Carril), ("filaEnLaSalida", r.Fila));
        return corredor;
    }

    /// <summary>Caja que cubre la ruta (con margen) donde no se puede volar; planear sí.</summary>
    private static void ZonaSinVuelo(Transform padre, List<Vector3> posiciones)
    {
        if (posiciones.Count == 0) return;
        var b = new Bounds(posiciones[0], Vector3.zero);
        foreach (var p in posiciones) b.Encapsulate(p);
        var go = new GameObject("Zona: sin vuelo");
        go.transform.SetParent(padre, false);
        go.transform.position = b.center + Vector3.up * 8f;
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = b.size + new Vector3(24f, 36f, 24f);
        go.AddComponent<ZonaQueBloqueaHabilidades>();
    }

    /// <summary>Trigger en la capa «Climb» pegado a un tramo de la torre, del lado <paramref name="lado"/>.</summary>
    private static void ParedDeEscalada(Transform padre, string nombre, Vector3 centro, Vector3 lado, float radio, float desde, float hasta)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        int capa = LayerMask.NameToLayer("Climb");
        if (capa >= 0) go.layer = capa;
        go.transform.SetPositionAndRotation(centro + lado * (radio - 0.15f) + Vector3.up * (desde + hasta) * 0.5f, Quaternion.LookRotation(lado));
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(3f, hasta - desde, 0.4f);
    }

    /// <summary>
    /// Escalera de mano del pack (Ladder_01 apilada) pegada a la torre del lado <paramref name="lado"/>,
    /// del suelo a la cima. Devuelve su grosor.
    /// </summary>
    private static float MontarEscaleraDeMano(Transform mundo, Vector3 centro, Vector3 lado, float r1, float r3, float cambio,
                                              float suelo, float cima, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Interaccion + "/Ladder_01.prefab");
        if (prefab == null) { informe.AppendLine("Escalada: no encuentro Ladder_01."); return 0.3f; }
        var escena = mundo.gameObject.scene;
        var muestra = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        muestra.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Bounds m = BoundsDe(muestra);
        Object.DestroyImmediate(muestra);
        bool finaEnX = m.size.x <= m.size.z;
        float grosor = Mathf.Max(0.05f, finaEnX ? m.size.x : m.size.z);
        float tramo = Mathf.Max(0.5f, m.size.y);
        Quaternion giro = finaEnX ? Quaternion.LookRotation(Vector3.Cross(lado, Vector3.up)) : Quaternion.LookRotation(lado);

        var padre = Hijo(mundo, "Escalera de la torre");
        int piezas = Mathf.CeilToInt((cima - suelo) / tramo);
        for (int i = 0; i < piezas; i++)
        {
            float y = suelo + i * tramo;
            float radio = y + tramo * 0.5f < cambio ? r1 : r3;
            var pieza = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            pieza.transform.SetParent(padre, false);
            pieza.transform.SetPositionAndRotation(Vector3.zero, giro);
            Bounds b = BoundsDe(pieza);
            Vector3 deseado = centro + lado * (radio + grosor * 0.5f + 0.02f);
            pieza.transform.position = new Vector3(deseado.x - b.center.x, y - b.min.y, deseado.z - b.center.z);
        }
        return grosor;
    }

    /// <summary>Al ganar, la prueba emite su señal por un SignalEmitter manual.</summary>
    private static void EmitirAlGanar(PruebaDelJugador prueba, string senal)
    {
        var emisor = prueba.gameObject.AddComponent<SignalEmitter>();
        emisor.eventKey = senal;
        emisor.trigger = SignalEmitter.TriggerType.Manual;
        emisor.requiredTag = GameTags.Player;
        emisor.once = true;
        UnityEventTools.AddPersistentListener(prueba.alGanar, emisor.Send);
    }

    /// <summary>Avisa de los tramos andando sin suelo debajo o con escalones de más de un metro.</summary>
    private static void ValidarRuta(string prueba, RutaDeRecorrido ruta, StringBuilder informe)
    {
        for (int i = 1; i < ruta.Cantidad; i++)
        {
            if (ruta.Punto(i).ComoSeLlega != Llegada.Andando) continue;
            Vector3 a = ruta.Posicion(i - 1), b = ruta.Posicion(i);
            int pasos = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b)));
            bool hueco = false;
            float escalon = 0f;
            float? anterior = null;
            for (int k = 1; k < pasos; k++)
            {
                Vector3 q = Vector3.Lerp(a, b, k / (float)pasos);
                Vector3 suelo = SueloCerca(q);
                if (suelo == q) { hueco = true; continue; }
                if (anterior.HasValue) escalon = Mathf.Max(escalon, suelo.y - anterior.Value);
                anterior = suelo.y;
            }
            string tramo = $"{prueba}: de «{ruta.Punto(i - 1).name}» a «{ruta.Punto(i).name}»";
            if (hueco) informe.AppendLine($"{tramo} hay un hueco sin suelo (los rivales lo cruzan; el jugador tendría que saltar).");
            if (escalon > 1.1f) informe.AppendLine($"{tramo} hay un escalón de {escalon:0.#} m.");
        }
    }

    // ── Utilidades ──────────────────────────────────────────────────────────────────────────

    private static PuntoDeTabla R(string nombre, string referencia, Vector3 aprox, Llegada llegada = Llegada.Andando,
                                  float salto = 0f, bool control = false, Vector3 zona = default, Vector3 desplazamiento = default)
        => new PuntoDeTabla { Nombre = nombre, Referencia = referencia, Aprox = aprox, Llegada = llegada, AlturaDelSalto = salto,
                              Control = control, Zona = zona, Desplazamiento = desplazamiento };

    private static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// <summary>
    /// El suelo cerca del punto: el primer collider sólido (ni triggers ni personajes) de un rayo que
    /// baja desde 2,5 m por encima hasta 4 m por debajo. Si no hay nada, el mismo punto.
    /// </summary>
    private static Vector3 SueloCerca(Vector3 punto)
    {
        var golpes = Physics.RaycastAll(punto + Vector3.up * 2.5f, Vector3.down, 6.5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var g in golpes)
        {
            if (g.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
            if (g.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (g.collider.attachedRigidbody != null && !g.collider.attachedRigidbody.isKinematic) continue;
            return g.point;
        }
        return punto;
    }

    /// <summary>Caja que envuelve los renderers del objeto (o un metro alrededor de su posición si no tiene).</summary>
    private static Bounds BoundsDe(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// <summary>Escala el objeto para que su lado mayor mida <paramref name="tamano"/> m. Devuelve la escala puesta.</summary>
    private static float EscalarATamano(GameObject go, float tamano)
    {
        go.transform.localScale = Vector3.one;
        Bounds b = BoundsDe(go);
        float mayor = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float escala = mayor > 0.001f ? tamano / mayor : 1f;
        go.transform.localScale = Vector3.one * escala;
        return escala;
    }

    private static void QuitarEstatico(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }

    // ── Serialización (campos privados de los componentes nuevos) ───────────────────────────

    /// <summary>Pone campos serializados por nombre (bool, int/enum, float, string, Vector3, color, referencias).</summary>
    private static void Fijar(Object objeto, params (string campo, object valor)[] valores)
    {
        var so = new SerializedObject(objeto);
        foreach (var (campo, valor) in valores)
        {
            var p = so.FindProperty(campo);
            if (p == null) { Debug.LogWarning($"[ChuchelandiaBuilder] {objeto.GetType().Name} no tiene el campo «{campo}»."); continue; }
            switch (valor)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                case Vector3 v: p.vector3Value = v; break;
                case Color c: p.colorValue = c; break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarLista<T>(Object objeto, string campo, List<T> elementos) where T : Object
    {
        var so = new SerializedObject(objeto);
        var lista = so.FindProperty(campo);
        lista.arraySize = elementos.Count;
        for (int i = 0; i < elementos.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = elementos[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarFrases(GrupoEnLaPrueba grupo, (Slot, string)[] alEmpezar, (Slot, string)[] alReintentar, (Slot, string)[] alGanar)
    {
        var so = new SerializedObject(grupo);
        Llenar(so.FindProperty("alEmpezar"), alEmpezar);
        Llenar(so.FindProperty("alReintentar"), alReintentar);
        Llenar(so.FindProperty("alGanar"), alGanar);
        so.ApplyModifiedPropertiesWithoutUndo();

        static void Llenar(SerializedProperty lista, (Slot, string)[] frases)
        {
            lista.arraySize = frases.Length;
            for (int i = 0; i < frases.Length; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("hablante").intValue = (int)frases[i].Item1;
                e.FindPropertyRelative("clave").stringValue = frases[i].Item2;
            }
        }
    }

    private sealed class PuestoDeTabla
    {
        public Slot Personaje;
        public Transform Sitio, OtroSitio;
        public GrupoEnLaPrueba.Numerito Numerito;
        public LanzadorDeSalto Trampolin;
        public string[] Frases;
        public float Radio = 7f, AlturaDelBote = 5.5f, PausaEntreBotes = 0.4f, VelocidadAlPasear = 0.8f;
    }

    private static void FijarPuestos(GrupoEnLaPrueba grupo, List<PuestoDeTabla> puestos)
    {
        var so = new SerializedObject(grupo);
        var lista = so.FindProperty("puestos");
        lista.arraySize = puestos.Count;
        for (int i = 0; i < puestos.Count; i++)
        {
            var p = puestos[i];
            var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("personaje").intValue = (int)p.Personaje;
            e.FindPropertyRelative("sitio").objectReferenceValue = p.Sitio;
            e.FindPropertyRelative("numerito").intValue = (int)p.Numerito;
            e.FindPropertyRelative("trampolin").objectReferenceValue = p.Trampolin;
            e.FindPropertyRelative("otroSitio").objectReferenceValue = p.OtroSitio;
            e.FindPropertyRelative("radioParaHablar").floatValue = p.Radio;
            e.FindPropertyRelative("alturaDelBote").floatValue = p.AlturaDelBote;
            e.FindPropertyRelative("pausaEntreBotes").floatValue = p.PausaEntreBotes;
            e.FindPropertyRelative("velocidadAlPasear").floatValue = p.VelocidadAlPasear;
            var frases = e.FindPropertyRelative("frasesAlPasar");
            frases.arraySize = p.Frases?.Length ?? 0;
            for (int k = 0; k < frases.arraySize; k++) frases.GetArrayElementAtIndex(k).stringValue = p.Frases[k];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
