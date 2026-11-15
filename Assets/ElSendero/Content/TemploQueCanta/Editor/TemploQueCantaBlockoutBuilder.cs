using Game.NPC;
using Kits.Puzzles;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Blockout del templo que canta (tanda 1): la antesala donde se aprende el ritmo y la sala de la
/// balanza, en cubos grises. Sirve para probar en Play las dos cosas que el diseño pide antes de
/// construir el nivel entero: que el ritmo engancha (losas que laten y se hunden, rejilla de dos
/// compases) y que el grupo repartido resuelve un puzle (plato de peso, ascensor, brasero).
///
/// La medida del nivel en papel es el compás: 120 BPM en 4/4 son 2 s por compás y, a la velocidad de
/// carrera del jugador (4 m/s), unos 8 m de carrera por compás. La marea avanza menos que eso.
///
/// Se rehace entera cada vez (escena TemploQueCanta.unity y sus materiales en TemploQueCanta_data).
/// Ver INC-1211.
/// </summary>
public static class TemploQueCantaBlockoutBuilder
{
    public const string RutaEscena = "Assets/Scenes/Worlds/TemploQueCanta.unity";
    private const string CarpetaDatos = "Assets/Scenes/Worlds/TemploQueCanta_data";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string NombreDeLaEscena = "TemploQueCanta";

    // ── El sitio ────────────────────────────────────────────────────────────────────────────
    private const float AltoDeLosMuros = 7f;
    private const float Foso = 0.9f;          // lo que baja el fondo del foso respecto al suelo
    private const int VelasDeLaBalanza = 24;  // una por compás: 48 s para pensar

    private static Material _gris, _grisOscuro, _rojo, _cristal, _lava, _cristalLuz, _brasa;

    [MenuItem("El Sendero/Escenario/Templo que canta: montar el blockout (tanda 1, INC-1211)")]
    public static void Montar()
    {
        var activaAntes = SceneManager.GetActiveScene();
        var abierta = SceneManager.GetSceneByPath(RutaEscena);
        if (abierta.IsValid() && abierta.isLoaded) EditorSceneManager.CloseScene(abierta, true);

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        CrearCarpeta(CarpetaDatos);
        EditorSceneManager.SaveScene(escena, RutaEscena);
        SceneManager.SetActiveScene(escena);

        CrearMateriales();
        Ambiente(escena);

        var raiz = new GameObject("Templo que canta").transform;
        SceneManager.MoveGameObjectToScene(raiz.gameObject, escena);

        var pulso = CrearPulso(raiz);
        CrearAntesala(raiz, escena, pulso);
        var balanza = CrearBalanza(raiz, escena, pulso);

        var nav = raiz.gameObject.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;
        nav.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        nav.layerMask = LayerMask.GetMask(EscenaJugable.CapaDelSuelo);
        EscenaJugable.HornearNavMesh(nav);

        EscenaJugable.ColocarGrupo(escena, new Vector3(0f, 0.15f, -4f), Vector3.forward);
        AnclaDeEntrada(raiz, escena, new Vector3(0f, 0.15f, -4f));
        // Estela y Liam entran en el grupo solos: la balanza se resuelve con los tres.
        GrupoEntero(raiz);

        var tramo = CrearTramo(raiz, escena, pulso, balanza);
        Conectar(balanza);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, RutaEscena);
        PermitirLaInterfaz();
        AssetDatabase.SaveAssets();

        if (activaAntes.IsValid() && activaAntes.isLoaded) SceneManager.SetActiveScene(activaAntes);
        EditorSceneManager.CloseScene(escena, true);

        const string informe = "[Templo que canta] Blockout montado: antesala (losas y rejilla de 2 compases) " +
                               "y balanza (plato, ascensor, brasero y 24 velas). Abre TemploQueCanta.unity y dale a Play.";
        Debug.Log(informe);
        OrdenesDelEditor.Avisar("Templo que canta", informe);
    }

    // ── Lo que se monta ─────────────────────────────────────────────────────────────────────

    private static MusicPulse CrearPulso(Transform raiz)
    {
        var go = new GameObject("Pulso de la música");
        go.transform.SetParent(raiz, false);
        var pulso = go.AddComponent<MusicPulse>();
        Poner(pulso, "bpm", 120f);
        Poner(pulso, "beatsPerMeasure", 4);
        Poner(pulso, "clickInBlockout", true);
        return pulso;
    }

    /// <summary>Antesala: foso de lava con losas que laten y una rejilla que abre un cristal de luz.</summary>
    private static void CrearAntesala(Transform raiz, Scene escena, MusicPulse pulso)
    {
        var sala = new GameObject("0. Antesala").transform;
        sala.SetParent(raiz, false);

        // Suelo: al sur del foso, las dos orillas, la orilla del fondo y el paso a la balanza.
        Suelo("Suelo sur", sala.transform, escena, new Vector3(0f, -0.5f, -3f), new Vector3(18f, 1f, 8f));
        Suelo("Orilla oeste", sala.transform, escena, new Vector3(-6f, -0.5f, 3.7f), new Vector3(6f, 1f, 5.4f));
        Suelo("Orilla este", sala.transform, escena, new Vector3(6f, -0.5f, 3.7f), new Vector3(6f, 1f, 5.4f));
        Suelo("Orilla del fondo", sala.transform, escena, new Vector3(0f, -0.5f, 6.8f), new Vector3(18f, 1f, 0.8f));
        Suelo("Fondo del foso", sala.transform, escena, new Vector3(0f, -0.5f - Foso, 3.7f), new Vector3(6f, 1f, 5.4f));
        Suelo("Paso a la balanza", sala.transform, escena, new Vector3(0f, -0.5f, 9.6f), new Vector3(4.5f, 1f, 4.8f));

        // La lava del fondo: se ve, y quema. Así, caerse se nota desde la antesala.
        var lava = Caja("Lava", sala.transform, escena, new Vector3(0f, -0.86f, 3.7f),
                        new Vector3(5.9f, 0.06f, 5.3f), _lava, suelo: false);
        Object.DestroyImmediate(lava.GetComponent<Collider>());
        var quema = new GameObject("Lava que quema");
        quema.transform.SetParent(sala, false);
        quema.transform.position = new Vector3(0f, -0.45f, 3.7f);
        var zonaQuema = quema.AddComponent<BoxCollider>();
        zonaQuema.isTrigger = true;
        zonaQuema.size = new Vector3(5.9f, 0.9f, 5.3f);
        var dano = quema.AddComponent<ZonaDeDano>();
        Poner(dano, "danoPorSegundo", 30f);

        // Losas del centro: anchas y con un hueco cómodo; se hunden alternando compás.
        var centroDeLasLosas = new[] { 1.7f, 3.7f, 5.7f };
        for (int i = 0; i < centroDeLasLosas.Length; i++)
        {
            var losa = Caja($"Losa {i + 1}", sala.transform, escena,
                            new Vector3(0f, 0.05f, centroDeLasLosas[i]), new Vector3(4.6f, 0.3f, 1.4f), _gris, suelo: true);
            var tile = losa.AddComponent<PulseTile>();
            Poner(tile, "pulse", pulso);
            Poner(tile, "cycleMeasures", 2);
            Poner(tile, "sinkOnMeasureInCycle", i % 2);
            Poner(tile, "sunkBeats", 2);
            Poner(tile, "sinkDepth", 0.5f);
        }

        // Muros de la sala y pared con el paso a la balanza.
        Muro("Muro oeste", sala.transform, escena, new Vector3(-9.2f, AltoDeLosMuros * 0.5f, 0f), new Vector3(0.4f, AltoDeLosMuros, 15f));
        Muro("Muro este", sala.transform, escena, new Vector3(9.2f, AltoDeLosMuros * 0.5f, 0f), new Vector3(0.4f, AltoDeLosMuros, 15f));
        Muro("Muro sur", sala.transform, escena, new Vector3(0f, AltoDeLosMuros * 0.5f, -7.2f), new Vector3(18.8f, AltoDeLosMuros, 0.4f));
        Muro("Pared norte oeste", sala.transform, escena, new Vector3(-5.6f, AltoDeLosMuros * 0.5f, 7.2f), new Vector3(7.2f, AltoDeLosMuros, 0.4f));
        Muro("Pared norte este", sala.transform, escena, new Vector3(5.6f, AltoDeLosMuros * 0.5f, 7.2f), new Vector3(7.2f, AltoDeLosMuros, 0.4f));
        Muro("Dintel del paso", sala.transform, escena, new Vector3(0f, 5.25f, 7.2f), new Vector3(4f, 3.5f, 0.4f));

        // Rejilla del paso: se abre dos compases desde que la alcanza la luz de Will.
        var reja = Caja("Rejilla del paso (2 compases)", sala.transform, escena,
                        new Vector3(0f, 1.7f, 7.2f), new Vector3(4f, 3.4f, 0.4f), _grisOscuro, suelo: false);
        var compuerta = reja.AddComponent<BeatGate>();
        Poner(compuerta, "pulse", pulso);
        Poner(compuerta, "durationMeasures", 2);
        Poner(compuerta, "modo", (int)BeatGate.Modo.SeAbre);
        Poner(compuerta, "activeAtStart", false);
        PonerVector(compuerta, "activeOffset", new Vector3(0f, -3.6f, 0f));

        // El cristal, de luz: se lee por sí mismo, sin carteles (lo enciende la magia de luz).
        var cristal = Caja("Cristal de luz (lo enciende la magia de luz)", sala.transform, escena,
                           new Vector3(2.6f, 1.6f, 4.6f), new Vector3(0.75f, 1.1f, 0.75f), _cristalLuz, suelo: false);
        var receptor = cristal.AddComponent<ReceptorDeMagia>();
        Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Pulso);
        PonerEnumArray(receptor, "elementosAceptados", new[] { (int)MagicElement.Light });
        Caja("Peana del cristal", sala.transform, escena, new Vector3(2.6f, 0.4f, 4.6f), new Vector3(1.1f, 0.8f, 1.1f), _grisOscuro, suelo: false);
        Brillo("Luz del cristal", sala.transform, new Vector3(2.6f, 1.8f, 4.6f), new Color(0.72f, 0.88f, 1f), 1.1f, 3.5f);
        // La rejilla lleva la misma luz en el dintel: dice a qué magia responde sin carteles.
        Caja("Luz en el dintel del paso", sala.transform, escena,
             new Vector3(0f, 3.55f, 6.95f), new Vector3(4.2f, 0.18f, 0.3f), _cristalLuz, suelo: false);
        ConectarVoid(receptor.alActivarse, compuerta.TriggerForMeasures);

        // Tramo de la antesala: al caer a la lava, se vuelve al principio del foso.
        var tramo = new GameObject("Tramo 0: la antesala");
        tramo.transform.SetParent(sala, false);
        var salida = new GameObject("Salida del tramo").transform;
        salida.SetParent(tramo.transform, false);
        salida.position = new Vector3(0f, 0.2f, -3.2f);
        var meta = new GameObject("Meta del tramo").transform;
        meta.SetParent(tramo.transform, false);
        meta.position = new Vector3(0f, 0.2f, 11.2f);
        var antesala = tramo.AddComponent<TramoDelTemploQueCanta>();
        PonerRef(antesala, "salidaDelJugador", salida);
        Poner(antesala, "empezarAlActivarse", true);
        Poner(antesala, "segundosDeCuentaAtras", 0);
        Poner(antesala, "claveDeSalida", "");
        Poner(antesala, "textoDeSalida", "");
        Poner(antesala, "fundido", 0.2f);
        Poner(antesala, "pausaAntesDelReintento", 0.25f);
        Poner(antesala, "reintentarAlFallar", true);
        Poner(antesala, "alturaDelFoso", -0.4f);
        PonerRef(antesala, "pulso", pulso);
        PonerRef(antesala, "meta", meta);
        Poner(antesala, "radioDeLaMeta", 2f);
    }

    /// <summary>Balanza: el plato que pesa, el ascensor que sube, el brasero y la salida.</summary>
    private static DatosDeLaBalanza CrearBalanza(Transform raiz, Scene escena, MusicPulse pulso)
    {
        var sala = new GameObject("2. La balanza").transform;
        sala.SetParent(raiz, false);

        Suelo("Suelo de la balanza", sala.transform, escena, new Vector3(0f, -0.5f, 23f), new Vector3(24f, 1f, 24f));
        Muro("Muro oeste", sala.transform, escena, new Vector3(-12.2f, AltoDeLosMuros * 0.5f, 23f), new Vector3(0.4f, AltoDeLosMuros, 24f));
        Muro("Muro este", sala.transform, escena, new Vector3(12.2f, AltoDeLosMuros * 0.5f, 23f), new Vector3(0.4f, AltoDeLosMuros, 24f));
        Muro("Muro norte oeste", sala.transform, escena, new Vector3(-8.2f, AltoDeLosMuros * 0.5f, 35.2f), new Vector3(8.4f, AltoDeLosMuros, 0.4f));
        Muro("Muro norte este", sala.transform, escena, new Vector3(8.2f, AltoDeLosMuros * 0.5f, 35.2f), new Vector3(8.4f, AltoDeLosMuros, 0.4f));

        // El plato: la zona que cuenta (trigger) y la chapa que baja con el peso.
        var zonaDelPlato = new GameObject("Plato de peso (2 cuerpos)");
        zonaDelPlato.transform.SetParent(sala, false);
        zonaDelPlato.transform.position = new Vector3(-6f, 0.1f, 18f);
        var disparador = zonaDelPlato.AddComponent<BoxCollider>();
        disparador.isTrigger = true;
        disparador.size = new Vector3(3.8f, 1.5f, 3.8f);
        disparador.center = new Vector3(0f, 0.7f, 0f);

        var chapa = Suelo("Chapa del plato", zonaDelPlato.transform, escena, new Vector3(-6f, 0.2f, 18f), new Vector3(3.6f, 0.2f, 3.6f));
        var contrapeso = Caja("Contrapeso", sala.transform, escena, new Vector3(-11f, 2.6f, 18f), new Vector3(1f, 2f, 1f), _grisOscuro, suelo: false);

        var plato = zonaDelPlato.AddComponent<WeightPlate>();
        Poner(plato, "requiredWeight", 2);
        PonerRef(plato, "plateVisual", chapa.transform);
        PonerRef(plato, "counterweight", contrapeso.transform);
        Poner(plato, "sinkPerBody", 0.45f);
        Poner(plato, "counterweightPerBody", 1.1f);

        // El ascensor: sube solo mientras el plato tiene los dos cuerpos.
        var ascensor = Suelo("Ascensor", sala.transform, escena, new Vector3(-6f, 0.2f, 24f), new Vector3(4f, 0.4f, 4f));
        var elevador = ascensor.AddComponent<PlatformElevator>();
        Poner(elevador, "raiseHeight", 4.4f);
        Poner(elevador, "moveSpeed", 1.4f);

        // La repisa de arriba, el brasero y la salida.
        Suelo("Repisa de arriba", sala.transform, escena, new Vector3(-6f, 4.7f, 30f), new Vector3(4.6f, 0.4f, 9f));
        var brasero = Caja("Brasero (lo enciende el fuego)", sala.transform, escena, new Vector3(-3.2f, 5.3f, 32f), new Vector3(0.9f, 0.9f, 0.9f), _brasa, suelo: false);
        var receptor = brasero.AddComponent<ReceptorDeMagia>();
        Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Permanente);
        PonerEnumArray(receptor, "elementosAceptados", new[] { (int)MagicElement.Fire });
        Brillo("Luz del brasero", sala.transform, new Vector3(-3.2f, 5.6f, 32f), new Color(1f, 0.55f, 0.2f), 2f, 5f);

        var salida = Caja("Salida (la abre el brasero)", sala.transform, escena,
                          new Vector3(-6f, 6.4f, 34.9f), new Vector3(4f, 3.4f, 0.4f), _grisOscuro, suelo: false);
        var puerta = salida.AddComponent<BeatGate>();
        Poner(puerta, "pulse", pulso);
        Poner(puerta, "modo", (int)BeatGate.Modo.SeAbre);
        PonerVector(puerta, "activeOffset", new Vector3(0f, -3.6f, 0f));
        ConectarBool(receptor.alActivarse, puerta.SetPermanent, true);

        // Las velas de la cuenta atrás: una por compás, en una repisa del muro oeste.
        var velas = new GameObject[VelasDeLaBalanza];
        for (int i = 0; i < VelasDeLaBalanza; i++)
        {
            float z = 13f + i * 0.85f;
            velas[i] = Caja($"Vela {i + 1}", sala.transform, escena,
                            new Vector3(-11.4f, 3.2f, z), new Vector3(0.22f, 0.5f, 0.22f), _cristal, suelo: false);
            Object.DestroyImmediate(velas[i].GetComponent<Collider>());
        }
        var candelero = new GameObject("Cuenta de las 24 velas");
        candelero.transform.SetParent(sala, false);
        var cuenta = candelero.AddComponent<MeasureCandles>();
        Poner(cuenta, "pulse", pulso);
        Poner(cuenta, "totalMeasures", VelasDeLaBalanza);
        PonerRefs(cuenta, "flames", velas);
        Poner(cuenta, "startOnEnable", false);

        // La marea, parada en la entrada: solo avanza si el tramo no se resuelve.
        var marea = Caja("Marea", sala.transform, escena, new Vector3(0f, 0.75f, 12.6f), new Vector3(22f, 1.5f, 0.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        Poner(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.forward);
        Poner(mareaComp, "metersPerMeasure", 3.5f);
        Poner(mareaComp, "advanceAtStart", false);

        // Sin vuelo en todo el templo.
        var sinVuelo = new GameObject("Sin vuelo en el templo");
        sinVuelo.transform.SetParent(raiz, false);
        sinVuelo.transform.position = new Vector3(0f, 4f, 14f);
        var zona = sinVuelo.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(30f, 14f, 46f);
        sinVuelo.AddComponent<ZonaQueBloqueaHabilidades>();

        return new DatosDeLaBalanza
        {
            Plato = plato,
            Ascensor = elevador,
            Brasero = receptor,
            Salida = puerta,
            Velas = cuenta,
            Marea = mareaComp,
        };
    }

    private static TramoDelTemploQueCanta CrearTramo(Transform raiz, Scene escena, MusicPulse pulso, DatosDeLaBalanza balanza)
    {
        var go = new GameObject("Tramo 2: la balanza");
        go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(0f, 0f, 12f);

        var salida = new GameObject("Salida del tramo").transform;
        salida.SetParent(go.transform, false);
        salida.position = new Vector3(-6f, 0.2f, 14f);
        salida.rotation = Quaternion.identity;

        var meta = new GameObject("Meta del tramo").transform;
        meta.SetParent(go.transform, false);
        meta.position = new Vector3(-6f, 4.7f, 34.2f);

        var tramo = go.AddComponent<TramoDelTemploQueCanta>();
        PonerRef(tramo, "salidaDelJugador", salida);
        Poner(tramo, "empezarAlActivarse", false);
        Poner(tramo, "radioDeLaSalida", 8f);
        Poner(tramo, "segundosDeCuentaAtras", 0);
        Poner(tramo, "claveDeSalida", "");
        Poner(tramo, "textoDeSalida", "");
        Poner(tramo, "fundido", 0.2f);
        Poner(tramo, "pausaAntesDelReintento", 0.25f);
        Poner(tramo, "reintentarAlFallar", true);
        Poner(tramo, "alturaDelFoso", -6f);
        PonerRef(tramo, "pulso", pulso);
        PonerRef(tramo, "velas", balanza.Velas);
        PonerRef(tramo, "marea", balanza.Marea);
        PonerRef(tramo, "meta", meta);
        Poner(tramo, "radioDeLaMeta", 1.6f);
        PonerRefs(tramo, "rejas", new Object[] { balanza.Salida });
        return tramo;
    }

    private static void Conectar(DatosDeLaBalanza balanza)
    {
        // El plato sube el ascensor mientras tiene los dos cuerpos y lo baja si falta alguno.
        ConectarVoid(balanza.Plato.AlLlegarAlPeso, balanza.Ascensor.MoveUp);
        ConectarVoid(balanza.Plato.AlPerderElPeso, balanza.Ascensor.MoveDown);
    }

    // ── Piezas de escena ────────────────────────────────────────────────────────────────────

    /// <summary>Por dónde entra el jugador al probar la escena desde Arranque («Probar esta escena»).</summary>
    private const string IdDelAncla = "InicioTemploQueCanta";

    private static void AnclaDeEntrada(Transform padre, Scene escena, Vector3 sitio)
    {
        var go = new GameObject("Entrada al templo (ancla)");
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(sitio, Quaternion.identity);
        var ancla = go.AddComponent<SpawnAnchor>();
        ancla.anchorId = IdDelAncla;
    }

    /// <summary>Un compañero se une al grupo al empezar la escena (sin pasar por la historia).</summary>
    private static void GrupoEntero(Transform padre)
    {
        var go = new GameObject("Grupo entero al empezar");
        go.transform.SetParent(padre, false);
        go.AddComponent<GrupoAlEmpezar>();
    }

    private static GameObject Suelo(string nombre, Transform padre, Scene escena, Vector3 centro, Vector3 tam)
    {
        var go = Caja(nombre, padre, escena, centro, tam, _gris, suelo: true);
        go.name = nombre;
        return go;
    }

    private static void Muro(string nombre, Transform padre, Scene escena, Vector3 centro, Vector3 tam)
        => Caja(nombre, padre, escena, centro, tam, _grisOscuro, suelo: false);

    private static GameObject Caja(string nombre, Transform padre, Scene escena, Vector3 centro, Vector3 tam,
                                   Material material, bool suelo)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        if (padre != null) go.transform.SetParent(padre, false);
        else SceneManager.MoveGameObjectToScene(go, escena);
        go.transform.SetPositionAndRotation(centro, Quaternion.identity);
        go.transform.localScale = tam;
        go.layer = LayerMask.NameToLayer(suelo ? EscenaJugable.CapaDelSuelo : "Default");
        var malla = go.GetComponent<MeshRenderer>();
        if (malla != null && material != null) malla.sharedMaterial = material;
        return go;
    }

    private static void Ambiente(Scene escena)
    {
        var luz = new GameObject("Luz de la maqueta");
        SceneManager.MoveGameObjectToScene(luz, escena);
        var direccional = luz.AddComponent<Light>();
        direccional.type = LightType.Directional;
        direccional.intensity = 1.1f;
        direccional.color = new Color(1f, 0.96f, 0.9f);
        luz.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.38f);
    }

    private static void CrearMateriales()
    {
        _gris = MaterialLiso("Gris", new Color(0.62f, 0.62f, 0.64f));
        _grisOscuro = MaterialLiso("GrisOscuro", new Color(0.34f, 0.34f, 0.37f));
        _rojo = MaterialLiso("Rojo", new Color(0.85f, 0.22f, 0.12f));
        _cristal = MaterialLiso("Cristal", new Color(0.55f, 0.8f, 0.95f));
        // Lo que tiene que leerse solo, sin carteles: la lava quema, el cristal es de luz, la brasa es fuego.
        _lava = MaterialEmisivo("Lava", new Color(0.95f, 0.32f, 0.06f), 0.9f);
        _cristalLuz = MaterialEmisivo("CristalDeLuz", new Color(0.70f, 0.86f, 1f), 1.6f);
        _brasa = MaterialEmisivo("Brasa", new Color(0.95f, 0.45f, 0.12f), 1.2f);
    }

    /// <summary>Material que emite su propio color: se ve «encendido» sin luces de la escena.</summary>
    private static Material MaterialEmisivo(string nombre, Color color, float intensidad)
    {
        var material = MaterialLiso(nombre, color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * intensidad);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>Un punto de luz pequeño para que la pieza se lea en la sala gris.</summary>
    private static void Brillo(string nombre, Transform padre, Vector3 sitio, Color color, float intensidad, float alcance)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = sitio;
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.intensity = intensidad;
        luz.range = alcance;
    }

    private static Material MaterialLiso(string nombre, Color color)
    {
        string ruta = $"{CarpetaDatos}/{nombre}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
        AssetDatabase.CreateFolder(padre, System.IO.Path.GetFileName(ruta));
    }

    private static void PermitirLaInterfaz()
    {
        var start = SceneManager.GetSceneByPath(RutaStart);
        if (!start.IsValid() || !start.isLoaded) return;
        if (EscenaJugable.PermitirInterfazDeJuego(start, NombreDeLaEscena) > 0)
            EditorSceneManager.SaveScene(start, RutaStart);
    }

    // ── Datos y cableado ────────────────────────────────────────────────────────────────────

    private sealed class DatosDeLaBalanza
    {
        public WeightPlate Plato;
        public PlatformElevator Ascensor;
        public ReceptorDeMagia Brasero;
        public BeatGate Salida;
        public MeasureCandles Velas;
        public PulseTide Marea;
    }

    private static void ConectarVoid(UnityEvent evento, UnityAction accion)
        => UnityEventTools.AddVoidPersistentListener(evento, accion);

    private static void ConectarBool(UnityEvent evento, UnityAction<bool> accion, bool valor)
        => UnityEventTools.AddBoolPersistentListener(evento, accion, valor);

    private static void Poner(Object componente, string campo, bool valor)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.boolValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Poner(Object componente, string campo, float valor)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Poner(Object componente, string campo, int valor)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        // intValue vale también para los campos de enum: es el valor, no su puesto en la lista.
        prop.intValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Poner(Object componente, string campo, string valor)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.stringValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerVector(Object componente, string campo, Vector3 valor)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.vector3Value = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerRef(Object componente, string campo, Object valor)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerRefs(Object componente, string campo, Object[] valores)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.arraySize = valores != null ? valores.Length : 0;
        for (int i = 0; i < prop.arraySize; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PonerEnumArray(Object componente, string campo, int[] valores)
    {
        var so = new SerializedObject(componente);
        var prop = so.FindProperty(campo);
        if (prop == null) { NoExiste(componente, campo); return; }
        prop.arraySize = valores != null ? valores.Length : 0;
        for (int i = 0; i < prop.arraySize; i++)
            prop.GetArrayElementAtIndex(i).intValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void NoExiste(Object componente, string campo)
        => Debug.LogError($"[Templo que canta] {componente.GetType().Name} no tiene el campo «{campo}».");
}
