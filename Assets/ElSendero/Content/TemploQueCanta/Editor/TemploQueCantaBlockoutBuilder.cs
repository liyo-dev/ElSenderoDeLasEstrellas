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
public static partial class TemploQueCantaBlockoutBuilder
{
    public const string RutaEscena = "Assets/Scenes/Worlds/TemploQueCanta.unity";
    private const string CarpetaDatos = "Assets/Scenes/Worlds/TemploQueCanta_data";
    private const string RutaStart = "Assets/Scenes/Systems/Start.unity";
    private const string NombreDeLaEscena = "TemploQueCanta";
    /// <summary>La canción elegida por Raúl (16 nov): organillo, una sola voz, 120 BPM.</summary>
    private const string RutaDeLaCancion = "Assets/Audio/Music/Zonas/El guardian canta.mp3";

    // â”€â”€ El sitio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const float AltoDeLosMuros = 7f;
    private const float Foso = 2.5f;          // lo que baja el fondo del foso respecto al suelo
    /// <summary>Altura de la lava: por encima del fondo, para que se vea desde arriba y queme.</summary>
    private const float NivelDeLaLava = -1.1f;
    private const int VelasDeLaBalanza = 24;  // una por compás: 48 s para pensar
    /// <summary>La balanza va detrás de la huida: todo lo suyo se construye y se corre este trecho.</summary>
    private const float ZDeLaBalanza = 84f;
    /// <summary>Filas de losas de la huida (una por compás, como mucho).</summary>
    private const int FilasDeLaHuida = 12;

    private static Material _gris, _grisOscuro, _rojo, _cristal, _lava, _cristalLuz, _brasa;

    [MenuItem("El Sendero/Escenario/Templo que canta: montar el blockout (tanda 1, INC-1211)")]
    public static void Montar()
    {
        var activaAntes = SceneManager.GetActiveScene();
        // Se cierra por ruta aunque esté abierta: si no, SaveScene falla con Â«another open sceneÂ».
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var abierta = SceneManager.GetSceneAt(i);
            if (abierta.path == RutaEscena) EditorSceneManager.CloseScene(abierta, true);
        }

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
        CrearHuida(raiz, escena, pulso);
        var balanza = CrearBalanza(raiz, escena, pulso);
        CrearLateral(raiz, escena, pulso);
        CrearInterruptor(raiz, escena, pulso);
        CrearTresSeparados(raiz, escena, pulso);
        CrearEstribillo(raiz, escena, pulso);
        SinVuelo(raiz);

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

        const string informe = "[Templo que canta] Nivel entero montado: antesala, huida, balanza, lateral, interruptor, tres separados y estribillo. " +
                               "Abre TemploQueCanta.unity y dale a Play.";
        Debug.Log(informe);
        OrdenesDelEditor.Avisar("Templo que canta", informe);
    }

    // â”€â”€ Lo que se monta â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static MusicPulse CrearPulso(Transform raiz)
    {
        var go = new GameObject("Pulso de la música");
        go.transform.SetParent(raiz, false);
        var pulso = go.AddComponent<MusicPulse>();
        // Tempo medido de la canción elegida (16 nov): 77,1 BPM contado doble y 0,288 s de desfase.
        Poner(pulso, "bpm", 154.2f);
        Poner(pulso, "desfaseEnSegundos", 0.288f);
        Poner(pulso, "beatsPerMeasure", 4);

        var cancion = PrepararLaCancion();
        if (cancion != null)
        {
            var fuente = go.AddComponent<AudioSource>();
            fuente.clip = cancion;
            fuente.loop = true;
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;
            PonerRef(pulso, "musicSource", fuente);
            Poner(pulso, "clickInBlockout", false);
        }
        else
        {
            Poner(pulso, "clickInBlockout", true);
        }
        return pulso;
    }

    /// <summary>Deja la canción importada como Streaming (sin precargar) y devuelve el clip.</summary>
    private static AudioClip PrepararLaCancion()
    {
        var importador = AssetImporter.GetAtPath(RutaDeLaCancion) as AudioImporter;
        if (importador != null)
        {
            var ajustes = importador.defaultSampleSettings;
            ajustes.loadType = AudioClipLoadType.Streaming;
            ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
            ajustes.quality = 1f;
            ajustes.preloadAudioData = false;
            importador.defaultSampleSettings = ajustes;
            importador.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(RutaDeLaCancion);
    }

    /// <summary>Antesala: foso de lava con losas que laten y una rejilla que abre un cristal de luz.</summary>
    private static void CrearAntesala(Transform raiz, Scene escena, MusicPulse pulso)
    {
        var sala = new GameObject("0. Antesala").transform;
        sala.SetParent(raiz, false);

        // Suelo: al sur del foso, las dos orillas, la orilla del fondo y el paso a la balanza.
        Suelo("Suelo sur", sala.transform, escena, new Vector3(0f, -0.5f, -5.3f), new Vector3(18f, 1f, 3.4f));
        Suelo("Orilla oeste", sala.transform, escena, new Vector3(-6f, -0.5f, 3.7f), new Vector3(6f, 1f, 5.4f));
        Suelo("Orilla este", sala.transform, escena, new Vector3(6f, -0.5f, 3.7f), new Vector3(6f, 1f, 5.4f));
        Suelo("Orilla del fondo", sala.transform, escena, new Vector3(0f, -0.5f, 6.8f), new Vector3(18f, 1f, 0.8f));
        Suelo("Fondo del foso", sala.transform, escena, new Vector3(0f, -0.5f - Foso, 1.4f), new Vector3(6f, 1f, 10f));
        Suelo("Paso a la balanza", sala.transform, escena, new Vector3(0f, -0.5f, 7.8f), new Vector3(4.5f, 1f, 1.2f));

        // Paredes a los lados del foso: por ahí no se pasa. La única senda son las losas.
        Muro("Pared del foso oeste", sala.transform, escena, new Vector3(-3.4f, AltoDeLosMuros * 0.5f, 1.4f), new Vector3(0.4f, AltoDeLosMuros, 10f));
        Muro("Pared del foso este", sala.transform, escena, new Vector3(3.4f, AltoDeLosMuros * 0.5f, 1.4f), new Vector3(0.4f, AltoDeLosMuros, 10f));

        // La lava del fondo, a la vista: se ve desde arriba y quema. Con el foso hondo no se sale.
        var lava = Caja("Lava", sala.transform, escena, new Vector3(0f, NivelDeLaLava, 1.4f),
                        new Vector3(5.9f, 0.06f, 9.8f), _lava, suelo: false);
        Object.DestroyImmediate(lava.GetComponent<Collider>());
        var quema = new GameObject("Lava que quema");
        quema.transform.SetParent(sala, false);
        quema.transform.position = new Vector3(0f, NivelDeLaLava + 0.55f, 1.4f);
        var zonaQuema = quema.AddComponent<BoxCollider>();
        zonaQuema.isTrigger = true;
        zonaQuema.size = new Vector3(5.9f, 1.1f, 9.8f);
        var dano = quema.AddComponent<ZonaDeDano>();
        Poner(dano, "danoPorSegundo", 40f);

        // Losas del centro: cuatro, con un metro entre ellas. El foso (10 m) no se cruza de un salto.
        var centroDeLasLosas = new[] { -2f, 0.3f, 2.6f, 4.9f };
        for (int i = 0; i < centroDeLasLosas.Length; i++)
        {
            var losa = Caja($"Losa {i + 1}", sala.transform, escena,
                            new Vector3(0f, 0.05f, centroDeLasLosas[i]), new Vector3(4.6f, 0.3f, 1.2f), _gris, suelo: true);
            var tile = losa.AddComponent<PulseTile>();
            PonerRef(tile, "pulse", pulso);
            Poner(tile, "cycleMeasures", 2);
            Poner(tile, "sinkOnMeasureInCycle", i % 2);
            Poner(tile, "sunkBeats", 2);
            Poner(tile, "sinkDepth", 0.5f);
            Poner(tile, "warnSquash", 0.14f);
            Poner(tile, "warnShake", 0.06f);
        }

        // Muros de la sala y pared con el paso a la balanza.
        Muro("Muro oeste", sala.transform, escena, new Vector3(-9.2f, AltoDeLosMuros * 0.5f, 0f), new Vector3(0.4f, AltoDeLosMuros, 15f));
        Muro("Muro este", sala.transform, escena, new Vector3(9.2f, AltoDeLosMuros * 0.5f, 0f), new Vector3(0.4f, AltoDeLosMuros, 15f));
        Muro("Muro sur", sala.transform, escena, new Vector3(0f, AltoDeLosMuros * 0.5f, -7.2f), new Vector3(18.8f, AltoDeLosMuros, 0.4f));
        Muro("Pared norte oeste", sala.transform, escena, new Vector3(-5.6f, AltoDeLosMuros * 0.5f, 7.2f), new Vector3(7.2f, AltoDeLosMuros, 0.4f));
        Muro("Pared norte este", sala.transform, escena, new Vector3(5.6f, AltoDeLosMuros * 0.5f, 7.2f), new Vector3(7.2f, AltoDeLosMuros, 0.4f));
        Muro("Dintel del paso", sala.transform, escena, new Vector3(0f, 5.25f, 7.2f), new Vector3(4f, 3.5f, 0.4f));

        // Rejilla del paso: se abre dos compases desde que la alcanza la luz de Will.
        // En la antesala no va: aquí solo se aprende el ritmo. La rejilla de luz es del tramo 4.

        // Tramo de la antesala: al caer a la lava, se vuelve al principio del foso.
        var tramo = new GameObject("Tramo 0: la antesala");
        tramo.transform.SetParent(sala, false);
        var salida = new GameObject("Salida del tramo").transform;
        salida.SetParent(tramo.transform, false);
        salida.position = new Vector3(0f, 0.2f, -5f);
        var meta = new GameObject("Meta del tramo").transform;
        meta.SetParent(tramo.transform, false);
        meta.position = new Vector3(0f, 0.2f, 8.6f);
        var antesala = tramo.AddComponent<TramoDelTemploQueCanta>();
        PonerRef(antesala, "salidaDelJugador", salida);
        Poner(antesala, "empezarAlActivarse", true);
        Poner(antesala, "segundosDeCuentaAtras", 0);
        Poner(antesala, "claveDeSalida", "");
        Poner(antesala, "textoDeSalida", "");
        Poner(antesala, "fundido", 0.2f);
        Poner(antesala, "pausaAntesDelReintento", 0.25f);
        Poner(antesala, "reintentarAlFallar", true);
        Poner(antesala, "alturaDelFoso", -0.9f);
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
        PonerRef(puerta, "pulse", pulso);
        Poner(puerta, "modo", (int)BeatGate.Modo.SeAbre);
        PonerVector(puerta, "activeOffset", new Vector3(0f, -3.6f, 0f));
        ConectarBool(receptor.alActivarse, puerta.SetPermanent, true);
        // El fuego de la salida se lee en su marco: es magia de fuego, la de Estela.
        Caja("Fuego en el marco de la salida", sala.transform, escena,
             new Vector3(-6f, 8.25f, 34.6f), new Vector3(4.2f, 0.18f, 0.3f), _brasa, suelo: false);

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
        PonerRef(cuenta, "pulse", pulso);
        Poner(cuenta, "totalMeasures", VelasDeLaBalanza);
        PonerRefs(cuenta, "flames", velas);
        Poner(cuenta, "startOnEnable", false);

        // La marea, parada en la entrada: solo avanza si el tramo no se resuelve.
        var marea = Caja("Marea", sala.transform, escena, new Vector3(0f, 0.75f, 12.6f), new Vector3(22f, 1.5f, 0.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        PonerRef(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.forward);
        Poner(mareaComp, "metersPerMeasure", 3.5f);
        Poner(mareaComp, "advanceAtStart", false);

        // Sin vuelo en todo el templo.
        // La sala se construye a mano con sus coordenadas de siempre y luego se corre entera detrás
        // de la huida: mover la raíz lleva consigo todo lo que cuelga de ella.
        sala.transform.position = new Vector3(0f, 0f, ZDeLaBalanza);

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
        go.transform.position = new Vector3(0f, 0f, 12f + ZDeLaBalanza);

        var salida = new GameObject("Salida del tramo").transform;
        salida.SetParent(go.transform, false);
        salida.position = new Vector3(-6f, 0.2f, 14f + ZDeLaBalanza);
        salida.rotation = Quaternion.identity;

        var meta = new GameObject("Meta del tramo").transform;
        meta.SetParent(go.transform, false);
        meta.position = new Vector3(-6f, 4.7f, 34.2f + ZDeLaBalanza);

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
        Poner(tramo, "segundoDeEntrada", 40f);
        PonerRefs(tramo, "rejas", new Object[] { balanza.Salida });
        return tramo;
    }

    private static void Conectar(DatosDeLaBalanza balanza)
    {
        // El plato sube el ascensor mientras tiene los dos cuerpos y lo baja si falta alguno.
        ConectarVoid(balanza.Plato.AlLlegarAlPeso, balanza.Ascensor.MoveUp);
        ConectarVoid(balanza.Plato.AlPerderElPeso, balanza.Ascensor.MoveDown);
    }

    /// <summary>
    /// Tramo 1: la primera huida. Un pasillo sobre lava con filas de losas que se hunden al compás y
    /// la marea entrando por detrás. Los compañeros van por una cornisa paralela, sin saltos: la IA
    /// no salta al compás. Se entra desde la antesala y se sale en el rellano de la balanza.
    /// </summary>
    private static void CrearHuida(Transform raiz, Scene escena, MusicPulse pulso)
    {
        var huida = new GameObject("1. La huida").transform;
        huida.SetParent(raiz, false);

        // Rellanos de entrada y salida, y el foso de lava en medio.
        Suelo("Rellano de salida", huida, escena, new Vector3(0f, -0.5f, 9.6f), new Vector3(8f, 1f, 3.2f));
        Suelo("Rellano de la balanza", huida, escena, new Vector3(0f, -0.5f, 88.5f), new Vector3(8f, 1f, 13f));
        Suelo("Fondo del foso", huida, escena, new Vector3(0f, -0.5f - Foso, 46.6f), new Vector3(8f, 1f, 70.8f));

        var lava = Caja("Lava de la huida", huida, escena, new Vector3(0f, NivelDeLaLava, 46.6f),
                        new Vector3(7.9f, 0.06f, 70.6f), _lava, suelo: false);
        Object.DestroyImmediate(lava.GetComponent<Collider>());
        var quema = new GameObject("Lava de la huida que quema");
        quema.transform.SetParent(huida, false);
        quema.transform.position = new Vector3(0f, NivelDeLaLava + 0.55f, 46.6f);
        var zonaQuema = quema.AddComponent<BoxCollider>();
        zonaQuema.isTrigger = true;
        zonaQuema.size = new Vector3(7.9f, 1.1f, 70.6f);
        var dano = quema.AddComponent<ZonaDeDano>();
        Poner(dano, "danoPorSegundo", 30f);

        // Los compañeros esperan en el rellano de entrada, a la vista: no saben saltar las losas
        // (INC-1213). Al acabar el tramo vienen con el jugador.
        Muro("Muro oeste de la huida", huida, escena, new Vector3(-4.2f, AltoDeLosMuros * 0.5f, 51.5f), new Vector3(0.4f, AltoDeLosMuros, 87f));
        Muro("Muro este de la huida", huida, escena, new Vector3(8.6f, AltoDeLosMuros * 0.5f, 51.5f), new Vector3(0.4f, AltoDeLosMuros, 87f));

        var puesto = new GameObject("Puesto de los compañeros").transform;
        puesto.SetParent(huida, false);
        puesto.position = new Vector3(-2.4f, 0.2f, 9.4f);

        // Las losas: una fila por compás, anchas, con un hueco que se salta de sobra.
        var losas = new GameObject[FilasDeLaHuida];
        for (int i = 0; i < FilasDeLaHuida; i++)
        {
            var fila = Caja($"Losa de la huida {i + 1}", huida, escena,
                            new Vector3(0f, 0.05f, 13.2f + i * 6.1f), new Vector3(5f, 0.3f, 3.5f), _gris, suelo: true);
            var tile = fila.AddComponent<PulseTile>();
            PonerRef(tile, "pulse", pulso);
            Poner(tile, "cycleMeasures", 2);
            // Las ocho primeras se hunden todas a la vez; las últimas, alternando (zigzag).
            Poner(tile, "sinkOnMeasureInCycle", i < 8 ? 0 : i % 2);
            Poner(tile, "sunkBeats", 2);
            Poner(tile, "sinkDepth", 0.5f);
            Poner(tile, "warnSquash", 0.14f);
            Poner(tile, "warnShake", 0.06f);
            losas[i] = fila;
        }

        // La marea, entrando por detrás al empezar el tramo.
        var marea = Caja("Marea de la huida", huida, escena, new Vector3(0f, 0.75f, 7.6f),
                         new Vector3(7.8f, 1.5f, 0.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        PonerRef(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.forward);
        Poner(mareaComp, "metersPerMeasure", 3.5f);
        Poner(mareaComp, "advanceAtStart", false);

        // El tramo: al caer a la lava o si la marea alcanza, se vuelve a la entrada.
        var go = new GameObject("Tramo 1: la huida");
        go.transform.SetParent(huida, false);
        var salida = new GameObject("Salida del tramo").transform;
        salida.SetParent(go.transform, false);
        salida.position = new Vector3(0f, 0.2f, 9f);
        var meta = new GameObject("Meta del tramo").transform;
        meta.SetParent(go.transform, false);
        meta.position = new Vector3(0f, 0.2f, 90f);

        var tramo = go.AddComponent<TramoDelTemploQueCanta>();
        PonerRef(tramo, "salidaDelJugador", salida);
        Poner(tramo, "empezarAlActivarse", false);
        Poner(tramo, "radioDeLaSalida", 5f);
        Poner(tramo, "segundosDeCuentaAtras", 0);
        Poner(tramo, "claveDeSalida", "");
        Poner(tramo, "textoDeSalida", "");
        Poner(tramo, "fundido", 0.2f);
        Poner(tramo, "pausaAntesDelReintento", 0.25f);
        Poner(tramo, "reintentarAlFallar", true);
        Poner(tramo, "alturaDelFoso", -0.9f);
        PonerRef(tramo, "pulso", pulso);
        PonerRef(tramo, "marea", mareaComp);
        PonerRef(tramo, "meta", meta);
        PonerRef(tramo, "puestoDeLosCompaneros", puesto);
        Poner(tramo, "radioDeLaMeta", 3f);
        Poner(tramo, "segundoDeEntrada", 12f);
        PonerRefs(tramo, "losas", losas);
    }

    // â”€â”€ Piezas de escena â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Sin vuelo en todo el templo: la magia de volar se apaga al entrar.</summary>
    private static void SinVuelo(Transform raiz)
    {
        var go = new GameObject("Sin vuelo en el templo");
        go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(30f, 6f, 130f);
        var zona = go.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(150f, 20f, 320f);
        go.AddComponent<ZonaQueBloqueaHabilidades>();
    }

    /// <summary>Por dónde entra el jugador al probar la escena desde Arranque (Â«Probar esta escenaÂ»).</summary>
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

    /// <summary>Material que emite su propio color: se ve Â«encendidoÂ» sin luces de la escena.</summary>
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

    // â”€â”€ Datos y cableado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
        => Debug.LogError($"[Templo que canta] {componente.GetType().Name} no tiene el campo Â«{campo}Â».");
}


