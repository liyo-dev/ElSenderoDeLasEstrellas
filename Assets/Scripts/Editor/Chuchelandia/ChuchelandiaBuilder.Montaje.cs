using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// La maqueta y el flujo de Chuchelandia: el menú que lo monta todo y lo que se coloca en la escena.
/// Bajo «Chuchelandia (generado)», que se rehace en cada ejecución: las marcas M_* de los diez tramos
/// con sus rótulos de maqueta (solo en el Editor), las anclas de prueba, el reparto
/// (<see cref="MontarReparto"/>), las llegadas de cada tramo (<see cref="SignalEmitter"/> +
/// <see cref="ActivoEnTramo"/>), lo jugable (<see cref="MontarPruebas"/>,
/// <see cref="MontarSegundaMitad"/>), el reino libre y su misión opcional (<see cref="MontarReino"/>),
/// los atuendos por tramo, la zona de magia transformada y el portal de vuelta al Sendero. Lo que
/// estorba de la escena se retira con registro (<see cref="RetirarLoQueEstorba"/>). Fuera de la
/// escena: el grafo y las misiones (<see cref="AsegurarGrafo"/>), los diálogos, las piezas de los habitantes y los objetos del
/// reino (<see cref="AsegurarReino"/>), los atuendos y el planeo de _WILL.
/// Las marcas se colocan respecto a objetos de la escena buscados por nombre (el más cercano a la
/// posición aproximada de la tabla) y se asientan en el suelo; se retocan en la tabla o moviéndolas a
/// ojo en la escena (se pierde al volver a montar).
/// </summary>
public static partial class ChuchelandiaBuilder
{
    public const string NombreGenerado = "Chuchelandia (generado)";
    private const string TituloMontaje = "Chuchelandia: montar la prueba";

    /// <summary>Ancla de llegada desde el Sendero (y de «Probar desde el nodo» del primer tramo).</summary>
    public const string AnclaLlegada = "ChuchelandiaLlegada";

    // ── Tabla de marcas ─────────────────────────────────────────────────────────────────────
    // Cada marca: tramo, nombre, referencia (objeto de la escena u otra marca), posición aproximada
    // (elige entre objetos con el mismo nombre; sin referencia, es la posición), desplazamiento, a quién
    // mira (otra marca) o rumbo en grados (0 = norte, +Z), ancla de prueba.
    // El desplazamiento respecto a otra marca va en el marco de esa marca: x a su derecha, z hacia
    // donde mira (EnElMarcoDe). Respecto a un objeto de la escena, en ejes del mundo.
    // Los personajes de un grupo, a 2 m o más unos de otros (chibis de cabeza grande).
    // El trono mira al este (+X); el reino va de la llegada (norte, arriba) al castillo (sur, z ≈ 0).

    private static readonly Vector3 SinAprox = Vector3.zero;

    private static List<Marca> TablaDeMarcas() => new List<Marca>
    {
        // 1 · Llegada: cerca del punto de aparición de siempre (ancla «CandyLand», con el punto de guardado
        // Woods_Entrance_SavePoint), 5 m hacia el valle y 9 m a la derecha del grupo: la columna de luz del
        // punto de guardado queda fuera de los planos de la llegada (detrás y a un lado). Mirando al valle.
        M(1, "M_Llegada_Will", "CandyLand", new Vector3(92.4f, 14.5f, 222.7f), V(-6.1f, 0, -8.3f), mira: "M_Llegada_Valle", ancla: AnclaLlegada),
        M(1, "M_Llegada_Estela", "M_Llegada_Will", SinAprox, V(-2.3f, 0, -0.5f), mira: "M_Llegada_Valle"),
        M(1, "M_Llegada_Liam", "M_Llegada_Will", SinAprox, V(2.3f, 0, -0.5f), mira: "M_Llegada_Valle"),
        M(1, "M_Llegada_Cam", "M_Llegada_Will", SinAprox, V(0, 2.4f, -6f), mira: "M_Llegada_Will", alSuelo: false),
        M(1, "M_Llegada_Valle", null, new Vector3(140f, 4f, 120f), V(0, 0, 0), rumbo: 180f),

        // 2 · La Puerta del Corazón, al fondo del valle (borde este, entre colinas). Mira al oeste.
        M(2, "M_Puerta_Corazon", null, new Vector3(190f, 4f, 100f), V(0, 0, 0), rumbo: 270f),
        M(2, "M_Puerta_Candado", "M_Puerta_Corazon", SinAprox, V(0, 1.3f, 0.8f), rumbo: 270f, alSuelo: false),
        M(2, "M_Puerta_Heraldo", "M_Puerta_Corazon", SinAprox, V(3.5f, 0, 1.5f), mira: "M_Puerta_Will"),
        M(2, "M_Puerta_Will", "M_Puerta_Corazon", SinAprox, V(0, 0, 6f), mira: "M_Puerta_Corazon", ancla: "ChuchePuerta"),
        M(2, "M_Puerta_Estela", "M_Puerta_Corazon", SinAprox, V(-2.2f, 0, 6.6f), mira: "M_Puerta_Corazon"),
        M(2, "M_Puerta_Liam", "M_Puerta_Corazon", SinAprox, V(2.2f, 0, 6.6f), mira: "M_Puerta_Corazon"),
        M(2, "M_Puerta_Cam", "M_Puerta_Corazon", SinAprox, V(4f, 2.5f, 13f), mira: "M_Puerta_Corazon", alSuelo: false),

        // 3 · El vestidor real, en la plaza alta del norte del castillo (y ≈ 14; dos plataformas de 6 × 6 m
        // con valla, de x ≈ 93 a 105): la puerta es la verja del este (Gate_01) y el grupo espera al oeste.
        M(3, "M_Vestidor_Puerta", null, new Vector3(103.5f, 14f, 37f), V(0, 0, 0), rumbo: 270f),
        M(3, "M_Vestidor_Will", "M_Vestidor_Puerta", SinAprox, V(1.5f, 0, 3.3f), mira: "M_Vestidor_Puerta", ancla: "ChucheVestidor"),
        M(3, "M_Vestidor_Liam", "M_Vestidor_Puerta", SinAprox, V(-1.5f, 0, 3.3f), mira: "M_Vestidor_Puerta"),
        M(3, "M_Vestidor_Cam", "M_Vestidor_Puerta", SinAprox, V(0, 2.2f, 7.5f), mira: "M_Vestidor_Puerta", alSuelo: false),

        // 4 · Carrera de obstáculos: de la puerta de plasma del este al trono, por plataformas y el trampolín.
        M(4, "M_Carrera_Salida", "Gate_03", new Vector3(122.16f, 3.78f, 0f), V(-3f, 0, 0), rumbo: 270f, ancla: "ChucheCarrera"),
        M(4, "M_Carrera_Liam", "PlatformGround_04", new Vector3(106.68f, 8f, 4.48f), V(0, 0, 0), rumbo: 270f),
        M(4, "M_Carrera_Will", "Trampoline_1_01", new Vector3(94.89f, 8.11f, 11.82f), V(1.8f, 0, 0), mira: "M_Carrera_Meta"),
        M(4, "M_Carrera_Meta", "Podium_01", new Vector3(86f, 14f, 3f), V(0, 0, 0), rumbo: 90f),

        // 4 · Escalada: la torre alta del noroeste (Tower_01 + Tower_03 + TowerCap_04, ~21 m).
        M(4, "M_Torre_Arranque", "Tower_01", new Vector3(70.88f, 1.49f, 64.45f), V(6f, 0, 0), rumbo: 270f, ancla: "ChucheTorre"),
        M(4, "M_Torre_Rival", "Tower_01", new Vector3(70.88f, 1.49f, 64.45f), V(-6f, 0, 0), rumbo: 90f),
        M(4, "M_Torre_Cima", "TowerCap_04", new Vector3(70.88f, 21.09f, 64.45f), V(0, 1f, 0), rumbo: 90f),

        // 4 · Puntería: la pradera llana al norte del castillo; las dianas flotan delante.
        M(4, "M_Tiro_Puesto", null, new Vector3(150f, 3f, 78f), V(0, 0, 0), rumbo: 0f, ancla: "ChucheTiro"),
        M(4, "M_Tiro_Rival", "M_Tiro_Puesto", SinAprox, V(3f, 0, 0), rumbo: 0f),
        M(4, "M_Tiro_Diana1", "M_Tiro_Puesto", SinAprox, V(-6f, 5f, 14f), rumbo: 180f, alSuelo: false),
        M(4, "M_Tiro_Diana2", "M_Tiro_Puesto", SinAprox, V(0, 7f, 18f), rumbo: 180f, alSuelo: false),
        M(4, "M_Tiro_Diana3", "M_Tiro_Puesto", SinAprox, V(6f, 4f, 13f), rumbo: 180f, alSuelo: false),
        M(4, "M_Tiro_Cam", "M_Tiro_Puesto", SinAprox, V(0, 2.5f, -6f), rumbo: 0f, alSuelo: false),

        // 5 · Clases de la Condesa Merengue: el escenario del baile del sur, con su público fijo.
        M(5, "M_Clases_Condesa", "DanceSound", new Vector3(110.41f, 16.46f, -53.83f), V(0, 0, 2f), mira: "M_Clases_Estela"),
        M(5, "M_Clases_Estela", "DanceSound", new Vector3(110.41f, 16.46f, -53.83f), V(0, 0, -1f), mira: "M_Clases_Condesa", ancla: "ChucheClases"),
        M(5, "M_Clases_Will", "M_Clases_Estela", SinAprox, V(-2.5f, 0, -1f), mira: "M_Clases_Estela"),
        M(5, "M_Clases_Liam", "M_Clases_Estela", SinAprox, V(2.5f, 0, -1f), mira: "M_Clases_Estela"),
        M(5, "M_Clases_Cam", "M_Clases_Estela", SinAprox, V(0, 2.5f, -7f), mira: "M_Clases_Condesa", alSuelo: false),

        // 7-9 · El salón del duelo, en el salón del trono (Throne_01 mira al este, alfombra hacia +X).
        M(7, "M_Duelo_Trono", "Throne_01", new Vector3(85.56f, 15.76f, 3f), V(1.5f, 0, 0), rumbo: 90f),
        M(7, "M_Duelo_Centro", "Throne_01", new Vector3(85.56f, 15.76f, 3f), V(7f, 0, 0), mira: "M_Duelo_Trono"),
        M(7, "M_Duelo_Palco", "M_Duelo_Centro", SinAprox, V(-3.5f, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Juez1", "M_Duelo_Palco", SinAprox, V(-2f, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Juez2", "M_Duelo_Palco", SinAprox, V(0, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Juez3", "M_Duelo_Palco", SinAprox, V(2f, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_PrimeraFila", "M_Duelo_Centro", SinAprox, V(3.5f, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Will", "M_Duelo_PrimeraFila", SinAprox, V(1.2f, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Liam", "M_Duelo_PrimeraFila", SinAprox, V(-1.2f, 0, 0), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Fuente", "M_Duelo_Centro", SinAprox, V(4f, 0, -4.5f), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Banquete", "M_Duelo_Centro", SinAprox, V(-3.5f, 0, -4.5f), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Heraldo", "M_Duelo_Trono", SinAprox, V(2f, 0, 1.5f), mira: "M_Duelo_Centro"),
        M(7, "M_Duelo_Suplente", "M_Duelo_Trono", SinAprox, V(-2f, 0, 1.5f), rumbo: 270f),
        M(7, "M_Duelo_Entrada", "M_Duelo_Centro", SinAprox, V(0, 0, -5f), mira: "M_Duelo_Trono", ancla: "ChucheSalon"),
        M(7, "M_Duelo_Cam", "M_Duelo_Centro", SinAprox, V(0, 2.5f, -8f), mira: "M_Duelo_Centro", alSuelo: false),

        // 6 · El reino libre: el heraldo suplente (que encarga las trompetillas) en la plaza alta del norte,
        // seis gominolas que hablan repartidas por el reino y las cinco trompetillas, en sitios del pack
        // pensados para recoger cosas (sus gemas): la muralla tras el trampolín de la carrera, lo alto de
        // los ascensores, lo alto de la escalera de la plaza norte, la puerta de plasma del sur y la torre del este.
        M(6, "M_Reino_Suplente", "PlatformGround_04", new Vector3(96.72f, 14f, 38.29f), V(-5f, 0, -6f), rumbo: 180f),
        M(6, "M_Reino_Gominola1", "PlatformGround_04", new Vector3(96.72f, 14f, 38.29f), V(5f, 0, -5f), rumbo: 200f),
        M(6, "M_Reino_Gominola2", null, new Vector3(113f, 8.1f, 9.5f), V(0, 0, 0), rumbo: 270f),
        M(6, "M_Reino_Gominola3", "M_Tiro_Puesto", SinAprox, V(-6f, 0, -3f), rumbo: 90f),
        M(6, "M_Reino_Gominola4", "M_Torre_Arranque", SinAprox, V(5f, 0, -2f), rumbo: 270f),
        M(6, "M_Reino_Gominola5", "M_Clases_Estela", SinAprox, V(6f, 0, -6f), rumbo: 0f),
        M(6, "M_Reino_Gominola6", "Bridge_01", new Vector3(84.2f, 11f, -16f), V(0, 0, 2.5f), rumbo: 90f),
        M(6, "M_Reino_Trompetilla1", "Gem_02", new Vector3(84.5f, 15.2f, 13.8f), V(1.2f, 0, 0)),
        M(6, "M_Reino_Trompetilla2", "Gem_03", new Vector3(140.6f, 18.1f, -37.5f), V(0, 0, 1.2f)),
        M(6, "M_Reino_Trompetilla3", "Gem_06", new Vector3(84.1f, 15.1f, 37.2f), V(0, 0, 1.2f)),
        M(6, "M_Reino_Trompetilla4", "Gem_04", new Vector3(146.7f, 12.9f, -49.5f), V(1.2f, 0, 0)),
        M(6, "M_Reino_Trompetilla5", "Gem_05", new Vector3(177.7f, 6.9f, -20.5f), V(1.2f, 0, 0)),

        // 10 · Salida del reino: la propia Puerta del Corazón es el portal (MontarPuertaDelCorazon).
    };

    /// <summary>Título de cada tramo (rótulo grande sobre su primera marca).</summary>
    private static readonly Dictionary<int, string> Tramos = new Dictionary<int, string>
    {
        { 1, "1 · Llegada" }, { 2, "2 · La Puerta del Corazón" }, { 3, "3 · El vestidor real" },
        { 4, "4 · Pruebas de candidata" }, { 5, "5 · Clases de la Condesa" }, { 6, "6 · El reino libre" }, { 7, "7-9 · Salón del duelo" },
        { 10, "10 · Salida del reino" },
    };

    // ── Tabla de disparadores ───────────────────────────────────────────────────────────────
    // Cada uno es un trigger con SignalEmitter que solo existe mientras dura su tramo (ActivoEnTramo):
    // desde la señal «empieza» (la del grafo al empezar el tramo, o el hecho anterior de la escena)
    // hasta la suya propia. Son las llegadas; lo jugable emite sus señales ello mismo: las pruebas
    // de candidata (MontarPruebas), y el candado, las clases, el duelo y «¡TE QUIERO!»
    // (MontarSegundaMitad).
    private static List<Disparador> TablaDeDisparadores() => new List<Disparador>
    {
        // La llegada: el grupo aparece dentro (el trigger salta al aparecer) o entra caminando.
        D("Zona: la llegada", "M_Llegada_Will", V(0, -2f, 0), V(24f, 10f, 24f), Senal.Llegada, null),
        D("Zona: el vestidor real", "M_Vestidor_Puerta", V(-3f, 0, 0), V(6f, 4f, 6f), Senal.VestidorLlegada, Senal.VestidorEmpieza),
        D("Zona: las clases de la Condesa", "M_Clases_Estela", V(0, 0, -1f), V(10f, 5f, 10f), Senal.ClasesLlegada, Senal.ClasesEmpieza),
        D("Zona: el salón del duelo", "M_Duelo_Centro", V(2f, 0, 0), V(14f, 6f, 12f), Senal.SalonLlegada, Senal.ReinoLibre),
    };

    /// <summary>Caja que cubre todo el reino jugable (la magia sale transformada dentro).</summary>
    private static readonly Bounds ZonaDelReino = new Bounds(new Vector3(130f, 40f, 75f), new Vector3(320f, 160f, 420f));

    // ── Suelo ───────────────────────────────────────────────────────────────────────────────
    // El jugador solo pisa la capa Floor (groundLayer de _WILL.prefab): lo que se pisa de Sweet_Land va
    // ahí, en las instancias de la escena (los prefabs del pack no se tocan). Prefijos del nombre del
    // prefab de origen.

    /// <summary>Piezas que siempre son suelo: terreno, plataformas, puentes, torres, ascensores, tartas y galletas.</summary>
    private static readonly string[] PrefijosDeSuelo =
    {
        "Ground", "Platform", "Bridge", "Tower", "LadderTop", "Elevator", "Rotator", "Podium", "Waffle", "Cookie", "Cake",
    };

    /// <summary>
    /// Dulces que según cómo estén puestos son suelo o decorado: solo van a Floor los tumbados (más anchos
    /// que altos, de hasta <see cref="AlturaDeLosas"/>), como los caramelos de los puentes o los donuts planos.
    /// </summary>
    private static readonly string[] PrefijosDeLosas = { "Candy", "Donut", "Pretzel", "Marshmallow", "Marmalade", "Carpet" };

    private const float AlturaDeLosas = 3f;

    /// <summary>Pone en Floor lo pisable del pack (EscenaJugable). Se llama antes de montar lo generado, que no se toca.</summary>
    private static void PonerSueloEnSuCapa(Scene escena, StringBuilder informe)
    {
        int suelo = EscenaJugable.PonerSueloEnSuCapa(escena, PrefijosDeSuelo);
        int losas = EscenaJugable.PonerLosasEnSuCapa(escena, AlturaDeLosas, PrefijosDeLosas);
        int enFloor = ContarEnSuCapa(escena, PrefijosDeSuelo) + ContarEnSuCapa(escena, PrefijosDeLosas, soloEnFloor: true);
        informe.AppendLine($"Suelo: {enFloor} piezas del pack en la capa Floor ({suelo + losas} cambiadas ahora: {suelo} de suelo y {losas} dulces tumbados).");
    }

    /// <summary>Instancias de la escena con esos prefijos (o solo las que ya están en Floor).</summary>
    private static int ContarEnSuCapa(Scene escena, string[] prefijos, bool soloEnFloor = false)
    {
        int capa = LayerMask.NameToLayer(EscenaJugable.CapaDelSuelo), n = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var origen = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (origen == null || !System.Array.Exists(prefijos, p => origen.name.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))) continue;
                if (!soloEnFloor || t.gameObject.layer == capa) n++;
            }
        return n;
    }

    // ── El menú ─────────────────────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Escenario/Chuchelandia: montar la prueba")]
    public static void MontarLaPrueba()
    {
        if (Application.isPlaying)
        {
            OrdenesDelEditor.Avisar(TituloMontaje, "Sal de Play antes de montar la prueba.");
            return;
        }
        Scene escena = SceneManager.GetActiveScene();
        if (escena.path != RutaEscena)
        {
            OrdenesDelEditor.Avisar(TituloMontaje,
                $"Abre {RutaEscena} y déjala como escena activa antes de usar este menú. No se ha cambiado nada.");
            return;
        }
        if (!OrdenesDelEditor.Confirmar(TituloMontaje,
                $"Se rehace «{NombreGenerado}» (marcas, reparto, disparadores, pruebas, candado, clases, duelo, «¡TE QUIERO!», reino libre y trompetillas, atuendos, magia transformada, portal de salida y reproductores de las cinemáticas) " +
                "y se retiran (con registro, reversible) las gominolas de demo del trono; deja el día fijo sin clima con el cielo de caramelo " +
                "y guarda la escena, con cualquier otro cambio que tenga sin guardar.\n\n" +
                "También se crean o actualizan el grafo «Chuchelandia», sus misiones (la llegada, la prueba y las trompetillas), los diálogos y agendas del reino, " +
                "la trompetilla del inventario, los atuendos y la transformación de la magia, " +
                "se añade el planeo a _WILL.prefab y se registran el grafo y las misiones en Start.unity.",
                "Montar", "Cancelar"))
            return;

        var informe = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar(TituloMontaje, "Atuendos y magia…", 0.1f);
            var atuendos = AsegurarAtuendos(informe);
            var transformacion = AsegurarTransformacionDeMagia(informe);
            AsegurarPlaneoEnWill(informe);

            EditorUtility.DisplayProgressBar(TituloMontaje, "Grafo y misión…", 0.3f);
            var bienvenida = AsegurarMisionDeBienvenida(informe);
            var mision = AsegurarMision(informe);
            var trompetillas = AsegurarReino(informe);
            AsegurarSecuencias(informe);
            var grafo = AsegurarGrafo(informe);
            PonerPiezasDelReino(informe);

            EditorUtility.DisplayProgressBar(TituloMontaje, "Escena…", 0.5f);
            MontarEscena(escena, atuendos, transformacion, informe);

            EditorUtility.DisplayProgressBar(TituloMontaje, "Start.unity…", 0.85f);
            RegistrarEnStart(grafo, informe, bienvenida, mision, trompetillas);
            AsegurarEnLaBuild(informe);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Debug.Log("[ChuchelandiaBuilder] " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar(TituloMontaje, informe.ToString());
    }

    // ── La escena ───────────────────────────────────────────────────────────────────────────

    private static void MontarEscena(Scene escena, Atuendos atuendos, TransformacionDeMagia transformacion, StringBuilder informe)
    {
        foreach (GameObject raizVieja in escena.GetRootGameObjects())
            if (raizVieja.name == NombreGenerado) Object.DestroyImmediate(raizVieja);

        var raiz = new GameObject(NombreGenerado).transform;
        SceneManager.MoveGameObjectToScene(raiz.gameObject, escena);

        RetirarLoQueEstorba(escena, informe);
        PonerSueloEnSuCapa(escena, informe);
        Physics.SyncTransforms();
        var marcas = ColocarMarcas(escena, raiz, informe);
        ColocarMarcasDeCinematica(raiz, marcas, informe);
        MontarReparto(escena, Hijo(raiz, "Reparto"), marcas, informe);
        MontarDisparadores(Hijo(raiz, "Disparadores"), marcas, informe);
        MontarPuertaDelCorazon(Hijo(raiz, "Puerta del Corazón"), marcas, informe);
        var pruebas = Hijo(raiz, "Pruebas");
        MontarPruebas(escena, pruebas, marcas, informe);
        MontarSegundaMitad(raiz, pruebas, marcas, informe);
        MontarReino(escena, raiz, marcas, informe);
        MontarAtuendosEnTramo(Hijo(raiz, "Atuendos"), atuendos);
        MontarMagiaTransformada(Hijo(raiz, "Magia transformada"), transformacion);
        MontarCinematicas(raiz, marcas, informe);
        AnadirEtiquetaAlArranque(escena, raiz, informe);
        MontarCielo(escena, informe);

        EditorSceneManager.MarkSceneDirty(escena);
        informe.AppendLine(EditorSceneManager.SaveScene(escena)
            ? $"Escena: «{NombreGenerado}» montado y guardado."
            : $"Escena: «{NombreGenerado}» montado, pero NO se ha podido guardar: guárdala a mano.");
    }

    /// <summary>
    /// Coloca las marcas, sus anclas de prueba y los rótulos de maqueta. Devuelve las marcas por nombre.
    /// Una marca relativa a otra se coloca cuando esa ya está colocada y orientada: su desplazamiento
    /// va en el marco de la referencia (<see cref="EnElMarcoDe"/>).
    /// </summary>
    private static Dictionary<string, Transform> ColocarMarcas(Scene escena, Transform raiz, StringBuilder informe)
    {
        var tabla = TablaDeMarcas();
        var padre = Hijo(raiz, "Marcas");
        var anclas = Hijo(raiz, "Anclas de prueba");
        var rotulos = Hijo(raiz, "Rótulos de maqueta");

        var objetos = TodosLosObjetos(escena);
        var nombres = new HashSet<string>();
        foreach (var m in tabla) nombres.Add(m.Nombre);
        var hechas = new Dictionary<string, Transform>();
        var orientadas = new HashSet<string>();
        int sinReferencia = 0;

        var pendientes = new List<Marca>(tabla);
        for (bool avance = true; avance && pendientes.Count > 0;)
        {
            avance = false;
            for (int i = 0; i < pendientes.Count; i++)
            {
                var m = pendientes[i];
                if (nombres.Contains(m.Referencia ?? "") && !orientadas.Contains(m.Referencia)) continue;
                Colocar(m);
                pendientes.RemoveAt(i--);
                avance = true;
            }
            foreach (var m in tabla)
                if (hechas.ContainsKey(m.Nombre) && !orientadas.Contains(m.Nombre) && Orientar(m)) avance = true;
        }
        foreach (var m in pendientes)
        {
            informe.AppendLine($"{m.Nombre}: su referencia {m.Referencia} depende de ella; la coloco en ejes del mundo.");
            Colocar(m);
        }
        foreach (var m in tabla)
            if (!orientadas.Contains(m.Nombre)) Orientar(m);

        var primeraDelTramo = new HashSet<int>();
        foreach (var m in tabla)
        {
            var t = hechas[m.Nombre];
            if (!string.IsNullOrEmpty(m.Ancla))
            {
                var ancla = new GameObject("Ancla — " + m.Ancla);
                ancla.transform.SetParent(anclas, false);
                ancla.transform.SetPositionAndRotation(t.position + Vector3.up * 0.1f, t.rotation);
                ancla.AddComponent<SpawnAnchor>().anchorId = m.Ancla;
            }
            Rotular(rotulos, m.Nombre, t.position + Vector3.down * 1.4f, 3f);
            if (Tramos.TryGetValue(m.Tramo, out var titulo) && primeraDelTramo.Add(m.Tramo))
                Rotular(rotulos, titulo, t.position + Vector3.up * 1.2f, 7f);
        }
        informe.AppendLine($"Marcas: {hechas.Count} ({sinReferencia} sin su objeto de referencia).");
        return hechas;

        void Colocar(Marca m)
        {
            Vector3 pos = m.Aprox + m.Desplazamiento;
            if (!string.IsNullOrEmpty(m.Referencia))
            {
                if (hechas.TryGetValue(m.Referencia, out var anterior)) pos = anterior.position + EnElMarcoDe(anterior, m.Desplazamiento);
                else
                {
                    var objeto = MasCercano(objetos, m.Referencia, m.Aprox);
                    if (objeto != null) pos = objeto.position + m.Desplazamiento;
                    else { sinReferencia++; informe.AppendLine($"{m.Nombre}: no encuentro «{m.Referencia}» en la escena; uso la posición de la tabla."); }
                }
            }
            if (m.AlSuelo) pos = AlSuelo(pos);
            hechas[m.Nombre] = KitDeLaberintoDeEspejos.Punto(padre, m.Nombre, pos, Quaternion.Euler(0f, m.Rumbo, 0f) * Vector3.forward);
        }

        // Gira la marca hacia la que mira; false si esa todavía no está colocada.
        bool Orientar(Marca m)
        {
            if (!string.IsNullOrEmpty(m.MiraA) && nombres.Contains(m.MiraA))
            {
                if (!hechas.TryGetValue(m.MiraA, out var objetivo)) return false;
                var t = hechas[m.Nombre];
                Vector3 d = objetivo.position - t.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(d);
            }
            orientadas.Add(m.Nombre);
            return true;
        }
    }

    /// <summary>
    /// Un desplazamiento (derecha, arriba, delante) en el marco de una marca: girado con su rumbo.
    /// Así un grupo colocado respecto a una marca queda igual aunque la marca mire a otro lado.
    /// </summary>
    private static Vector3 EnElMarcoDe(Transform marca, Vector3 desplazamiento)
        => Quaternion.Euler(0f, marca.eulerAngles.y, 0f) * desplazamiento;

    private static void MontarDisparadores(Transform padre, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        int n = 0;
        foreach (var d in TablaDeDisparadores())
        {
            if (!marcas.TryGetValue(d.Marca, out var marca)) { informe.AppendLine($"Disparador «{d.Nombre}»: falta la marca {d.Marca}."); continue; }
            var go = new GameObject(d.Nombre);
            go.transform.SetParent(padre, false);
            go.transform.SetPositionAndRotation(marca.position + d.Centro, marca.rotation);
            var caja = go.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            caja.size = d.Tamano;
            caja.center = Vector3.up * (d.Tamano.y * 0.5f);
            var emisor = go.AddComponent<SignalEmitter>();
            emisor.eventKey = d.Senal;
            emisor.trigger = SignalEmitter.TriggerType.PhysicsTrigger;
            emisor.requiredTag = GameTags.Player;
            emisor.once = true;

            // Cada llegada que empieza con su tramo es el sitio al que hay que ir mientras dura: se marca en el minimapa.
            if (!string.IsNullOrEmpty(d.Empieza)) MarcaDeObjetivo(go);

            var tramo = padre.gameObject.AddComponent<ActivoEnTramo>();
            if (!string.IsNullOrEmpty(d.Empieza)) tramo.Tramo.empiezaConSenal.Add(d.Empieza);
            tramo.Tramo.acabaConSenal.Add(d.Senal);
            tramo.Objetos.Add(go);
            go.SetActive(false);
            n++;
        }
        informe.AppendLine($"Disparadores: {n}, cada uno activo solo en su tramo; los de llegada a un sitio, con su marca en el minimapa.");
    }

    private static void MontarAtuendosEnTramo(Transform padre, Atuendos atuendos)
    {
        // El de candidata empieza también con cada tramo posterior: así está puesto al probar el grafo
        // desde el inicio de cualquiera de ellos.
        Poner(padre, "Atuendo: candidata", atuendos.Candidata, new[] { Senal.AtuendoVictoria }, Senal.AtuendoCandidata,
              Senal.CarreraEmpieza, Senal.EscaladaEmpieza, Senal.PunteriaEmpieza, Senal.ClasesEmpieza, Senal.ReinoLibre, Senal.DueloEmpieza);
        Poner(padre, "Atuendo: victoria", atuendos.Victoria, new[] { Senal.VestidoPurpurina, Senal.Salida }, Senal.AtuendoVictoria);

        static void Poner(Transform padre, string nombre, AtuendoDeEscena atuendo, string[] acaba, params string[] empieza)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var enTramo = go.AddComponent<AtuendoEnTramo>();
            var so = new SerializedObject(enTramo);
            so.FindProperty("atuendo").objectReferenceValue = atuendo;
            so.ApplyModifiedPropertiesWithoutUndo();
            enTramo.Tramo.empiezaConSenal.AddRange(empieza);
            enTramo.Tramo.acabaConSenal.AddRange(acaba);
        }
    }

    private static void MontarMagiaTransformada(Transform padre, TransformacionDeMagia transformacion)
    {
        var go = padre.gameObject;
        go.transform.position = ZonaDelReino.center;
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = ZonaDelReino.size;
        var zona = go.AddComponent<ZonaQueTransformaLaMagia>();
        var so = new SerializedObject(zona);
        so.FindProperty("transformacion").objectReferenceValue = transformacion;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── La Puerta del Corazón (es el portal de vuelta) ─────────────────────────────────────
    private static readonly Color BordeDeLaPuerta = new Color(2.6f, 0.7f, 1.5f);   // rosa caramelo (HDR)
    private static readonly Color LuzDeLaPuerta = new Color(1f, 0.55f, 0.8f);
    private static readonly Vector3 TamanoDeLaPuerta = new Vector3(3f, 3f, 0.6f);

    /// <summary>La ventana de la Puerta del Corazón de este montaje (la usan las reacciones del candado).</summary>
    private static Transform _ventanaDeLaPuerta;

    /// <summary>
    /// La Puerta del Corazón es el portal de vuelta al Sendero: la misma ventana que los portales del
    /// Sendero, con forma de corazón y borde rosa, y un cartel «¡ÁBREME!» encima. Está cerrada (un muro
    /// invisible) hasta que la prueba se supera; desde entonces se cruza: la prueba queda hecha (flag
    /// de la partida), el grupo aparece detrás de la puerta de Estela en el Sendero y el grafo recibe la salida.
    /// </summary>
    private static void MontarPuertaDelCorazon(Transform padre, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        _ventanaDeLaPuerta = null;
        if (!marcas.TryGetValue("M_Puerta_Corazon", out var punto)) { informe.AppendLine("Puerta del Corazón: falta la marca M_Puerta_Corazon."); return; }
        var go = new GameObject("Puerta del Corazón (portal al Sendero)");
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(punto.position, punto.rotation);
        Vector3 entrada = -punto.forward;   // el grupo llega de delante de la puerta y la cruza hacia dentro
        var aspecto = new VistaDelPortal.Aspecto(VistaDelPortal.Forma.Corazon, BordeDeLaPuerta, LuzDeLaPuerta);
        _ventanaDeLaPuerta = VistaDelPortal.Ventana(go.transform, VistaDelPortal.HaciaElSendero, entrada, Vector3.forward, aspecto).transform;

        // El cartel, bien grande y encima del corazón, mirando a quien llega.
        var cartel = new GameObject("Cartel ¡ÁBREME!");
        cartel.transform.SetParent(go.transform, false);
        cartel.transform.localPosition = new Vector3(0f, 3.6f, 0.05f);
        cartel.transform.localRotation = Quaternion.identity;
        var tmp = cartel.AddComponent<TMPro.TextMeshPro>();
        tmp.text = "¡ÁBREME!";
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.5f;
        tmp.fontSizeMax = 8f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = ColorSirope;
        // Sin contorno: en el Editor, el contorno de TMP crea un material suelto en la escena.
        // TextMeshPro se lee por su cara -Z: girado para que se lea desde delante de la puerta.
        cartel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        cartel.AddComponent<LocalizedText>().key = "CHUCHELANDIA_PUERTA_CARTEL";

        // Cerrada: un muro invisible hasta que se supera la prueba.
        var muro = new GameObject("Cerrada (hasta superar la prueba)");
        muro.transform.SetParent(go.transform, false);
        var solido = muro.AddComponent<BoxCollider>();
        solido.center = new Vector3(0f, TamanoDeLaPuerta.y * 0.5f, 0f);
        solido.size = TamanoDeLaPuerta;
        var cerrada = go.AddComponent<ActivoEnTramo>();
        cerrada.Tramo.acabaConSenal.Add(Senal.PruebaSuperada);
        cerrada.Objetos.Add(muro);

        // Abierta: se cruza.
        var cruzar = new GameObject("Cruzar al Sendero");
        cruzar.transform.SetParent(go.transform, false);
        var col = cruzar.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 1.5f, 0f);
        col.size = new Vector3(2.4f, 3f, 0.8f);
        var portal = cruzar.AddComponent<PortalTrigger>();
        portal.targetSceneName = "Sendero";
        portal.targetAnchorId = SenderoProgressionController.AnclaVueltaEstela;
        portal.setFlagOnEnter = SenderoProgressionController.FlagPruebaEstelaHecha;
        var emisor = cruzar.AddComponent<SignalEmitter>();
        emisor.eventKey = Senal.Salida;
        emisor.trigger = SignalEmitter.TriggerType.PhysicsTrigger;
        emisor.requiredTag = GameTags.Player;
        emisor.once = true;
        MarcaDeObjetivo(cruzar);
        var abierta = go.AddComponent<ActivoEnTramo>();
        abierta.Tramo.empiezaConSenal.Add(Senal.PruebaSuperada);
        abierta.Objetos.Add(cruzar);
        cruzar.SetActive(false);

        informe.AppendLine("Puerta del Corazón: portal en forma de corazón con «¡ÁBREME!», cerrada hasta " + Senal.PruebaSuperada + "; luego lleva al Sendero.");
    }

    /// <summary>
    /// Punto del minimapa de una misión (la de la prueba si no se dice otra) sobre <paramref name="go"/>
    /// (QuestObjectiveMarker, visible mientras la misión está activa). Va en objetos que solo están
    /// encendidos en su tramo (ActivoEnTramo), así que se ve solo mientras hay que ir ahí.
    /// </summary>
    private static void MarcaDeObjetivo(GameObject go, string mision = IdMision)
    {
        var marca = go.AddComponent<QuestObjectiveMarker>();
        var so = new SerializedObject(marca);
        so.FindProperty("questId").stringValue = mision;
        so.FindProperty("stepIndex").intValue = -1;
        so.FindProperty("showAfterStep").intValue = -1;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>El arranque de la escena también arranca el grafo de la prueba.</summary>
    private static void AnadirEtiquetaAlArranque(Scene escena, Transform raiz, StringBuilder informe)
    {
        NarrativeGraphStarter arranque = null;
        foreach (var r in escena.GetRootGameObjects())
        {
            if (r.transform == raiz) continue;
            arranque = r.GetComponentInChildren<NarrativeGraphStarter>(true);
            if (arranque != null) break;
        }
        if (arranque == null)
        {
            informe.AppendLine("AVISO: la escena no tiene NarrativeGraphStarter; el grafo no arrancará al cargarla.");
            return;
        }
        var so = new SerializedObject(arranque);
        var etiquetas = so.FindProperty("graphLabels");
        for (int i = 0; i < etiquetas.arraySize; i++)
            if (etiquetas.GetArrayElementAtIndex(i).stringValue == EtiquetaDelGrafo) return;
        etiquetas.arraySize++;
        etiquetas.GetArrayElementAtIndex(etiquetas.arraySize - 1).stringValue = EtiquetaDelGrafo;
        so.ApplyModifiedPropertiesWithoutUndo();
        informe.AppendLine($"Escena: el arranque narrativo arranca también «{EtiquetaDelGrafo}».");
    }

    private static void AsegurarEnLaBuild(StringBuilder informe)
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var e in escenas)
            if (e.path == RutaEscena) return;
        escenas.Add(new EditorBuildSettingsScene(RutaEscena, true));
        EditorBuildSettings.scenes = escenas.ToArray();
        informe.AppendLine("Chuchelandia añadida a la build.");
    }

    // ── Utilidades ──────────────────────────────────────────────────────────────────────────

    private sealed class Marca
    {
        public int Tramo;
        public string Nombre, Referencia, MiraA, Ancla;
        public Vector3 Aprox, Desplazamiento;
        public float Rumbo;
        public bool AlSuelo;
    }

    private sealed class Disparador
    {
        public string Nombre, Marca, Senal, Empieza;
        public Vector3 Centro, Tamano;
    }

    private static Marca M(int tramo, string nombre, string referencia, Vector3 aprox, Vector3 desplazamiento,
                           string mira = null, float rumbo = 0f, string ancla = null, bool alSuelo = true)
        => new Marca { Tramo = tramo, Nombre = nombre, Referencia = referencia, Aprox = aprox, Desplazamiento = desplazamiento,
                       MiraA = mira, Rumbo = rumbo, Ancla = ancla, AlSuelo = alSuelo };

    private static Disparador D(string nombre, string marca, Vector3 centro, Vector3 tamano, string senal, string empieza)
        => new Disparador { Nombre = nombre, Marca = marca, Centro = centro, Tamano = tamano, Senal = senal, Empieza = empieza };

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    private static Transform Hijo(Transform padre, string nombre)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        return t;
    }

    /// <summary>Todos los objetos de la escena salvo lo generado por este menú.</summary>
    private static List<Transform> TodosLosObjetos(Scene escena)
    {
        var lista = new List<Transform>();
        foreach (var r in escena.GetRootGameObjects())
            if (r.name != NombreGenerado) lista.AddRange(r.GetComponentsInChildren<Transform>(true));
        return lista;
    }

    /// <summary>El objeto con ese nombre más cercano a <paramref name="cerca"/>; null si no hay ninguno.</summary>
    private static Transform MasCercano(List<Transform> objetos, string nombre, Vector3 cerca)
    {
        Transform mejor = null;
        float mejorDistancia = float.MaxValue;
        foreach (var t in objetos)
        {
            if (t.name != nombre) continue;
            float d = (t.position - cerca).sqrMagnitude;
            if (d < mejorDistancia) { mejorDistancia = d; mejor = t; }
        }
        return mejor;
    }

    /// <summary>
    /// El suelo bajo el punto: el primer collider sólido (ni triggers ni personajes) de un rayo que
    /// baja desde 6 m por encima. Si no hay nada, el mismo punto.
    /// </summary>
    private static Vector3 AlSuelo(Vector3 punto)
    {
        var golpes = Physics.RaycastAll(punto + Vector3.up * 6f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
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

    /// <summary>Rótulo de maqueta legible por las dos caras.</summary>
    private static void Rotular(Transform padre, string texto, Vector3 pos, float tamano)
    {
        KitDeLaberintoDeEspejos.Rotulo(padre, texto, pos, Vector3.forward, tamano);
        KitDeLaberintoDeEspejos.Rotulo(padre, texto, pos, Vector3.back, tamano);
    }
}
