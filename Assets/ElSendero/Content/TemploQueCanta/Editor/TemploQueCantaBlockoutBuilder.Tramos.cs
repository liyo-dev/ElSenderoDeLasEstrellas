using Kits.Puzzles;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// Tramos 3 a 6 del templo que canta: la estrofa lateral, el interruptor, los tres separados y el
/// estribillo. Van detrás de la balanza, encadenados. Ver INC-1211.
public static partial class TemploQueCantaBlockoutBuilder
{
    // â”€â”€ 3. La estrofa lateral (2D) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Tramo 3: un pasillo de oeste a este que se juega de lado. La cámara se pone al norte mirando
    /// al plano y el jugador solo se mueve de izquierda a derecha. Faros de luz que encienden las
    /// plataformas dos compases, y la marea entrando por la izquierda.
    /// </summary>
    private static void CrearLateral(Transform raiz, Scene escena, TempoClock pulso)
    {
        var sala = new GameObject("3. La estrofa lateral").transform;
        sala.SetParent(raiz, false);

        Suelo("Entrada del lateral", sala, escena, new Vector3(-6f, -0.5f, 123f), new Vector3(20f, 1f, 12f));
        Suelo("Salida del lateral", sala, escena, new Vector3(62f, -0.5f, 123f), new Vector3(20f, 1f, 12f));
        Suelo("Fondo del lateral", sala, escena, new Vector3(29f, -0.5f - Foso, 123f), new Vector3(50f, 1f, 12f));
        var lava = Caja("Lava del lateral", sala, escena, new Vector3(29f, NivelDeLaLava, 123f),
                        new Vector3(50f, 0.06f, 11.8f), _lava, suelo: false);
        Object.DestroyImmediate(lava.GetComponent<Collider>());
        var quema = new GameObject("Lava del lateral que quema");
        quema.transform.SetParent(sala, false);
        quema.transform.position = new Vector3(29f, NivelDeLaLava + 0.55f, 123f);
        var zonaQuema = quema.AddComponent<BoxCollider>();
        zonaQuema.isTrigger = true;
        zonaQuema.size = new Vector3(50f, 1.1f, 11.8f);
        Poner(quema.AddComponent<ZonaDeDano>(), "danoPorSegundo", 40f);

        // Faros de luz a la vista (al sur, que es lo que mira la cámara) y su plataforma.
        var plataformas = new GameObject[6];
        for (int i = 0; i < plataformas.Length; i++)
        {
            float x = 4f + i * 8f;
            var faro = Caja($"Faro de luz {i + 1}", sala, escena, new Vector3(x + 4f, 1.5f, 120.4f),
                            new Vector3(0.8f, 0.8f, 0.8f), _cristalLuz, suelo: false);
            var receptor = faro.AddComponent<ReceptorDeMagia>();
            Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Pulso);
            PonerEnumArray(receptor, "elementosAceptados", new[] { (int)MagicElement.Light });
            Brillo($"Luz del faro {i + 1}", sala, new Vector3(x + 4f, 1.7f, 120.4f), new Color(0.72f, 0.88f, 1f), 0.7f, 2.5f);

            var plataforma = Caja($"Plataforma del faro {i + 1}", sala, escena, new Vector3(x + 4f, 0.05f, 123f),
                                  new Vector3(3.6f, 0.3f, 3.6f), _cristalLuz, suelo: true);
            var compuerta = plataforma.AddComponent<BeatGate>();
            PonerRef(compuerta, "pulse", pulso);
            Poner(compuerta, "modo", (int)BeatGate.Modo.Aparece);
            Poner(compuerta, "durationMeasures", 2);
            PonerVector(compuerta, "activeOffset", Vector3.zero);
            ConectarVoid(receptor.alActivarse, compuerta.TriggerForMeasures);
            plataformas[i] = plataforma;
        }

        // La zona que pone la cámara de lado y sujeta al jugador al plano. Empieza ya dentro del
        // pasillo: en el balcón de la salida de la balanza todavía se juega con la cámara normal.
        var zona = new GameObject("Tramo lateral (cámara de lado)");
        zona.transform.SetParent(sala, false);
        zona.transform.position = new Vector3(22f, 3f, 125.85f);
        var caja = zona.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(72f, 10f, 6.3f);
        var lateral = zona.AddComponent<SidePlaneZone>();
        PonerVector(lateral, "planeNormal", Vector3.forward);

        // La marea, entrando por la izquierda.
        var marea = Caja("Marea del lateral", sala, escena, new Vector3(-13f, 0.75f, 123f),
                         new Vector3(0.8f, 1.5f, 11.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        PonerRef(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.right);
        Poner(mareaComp, "metersPerMeasure", 5f);
        Poner(mareaComp, "advanceAtStart", false);

        var tramo = NuevoTramo("Tramo 3: la estrofa lateral", sala, pulso,
                               new Vector3(-6f, 0.2f, 123f), new Vector3(66f, 0.2f, 123f),
                               companerosEnLaSalida: true, radioDeLaSalida: 4f, rumboDeSalida: 90f);
        PonerRef(tramo, "marea", mareaComp);
        PonerRefs(tramo, "rejas", plataformas);
    }

    // â”€â”€ 4. El interruptor (huida corta) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Tramo 4: el interruptor. Se salta a golpearlo, baja un puente y abre una rejilla dos compases;
    /// detrás, el brasero que abre la salida. La marea llega al quinto compás.
    /// </summary>
    private static void CrearInterruptor(Transform raiz, Scene escena, TempoClock pulso)
    {
        var sala = new GameObject("4. El interruptor").transform;
        sala.SetParent(raiz, false);

        Suelo("Suelo del interruptor", sala, escena, new Vector3(66f, -0.5f, 138f), new Vector3(20f, 1f, 26f));
        Muro("Muro oeste", sala, escena, new Vector3(55.8f, AltoDeLosMuros * 0.5f, 138f), new Vector3(0.4f, AltoDeLosMuros, 26f));
        Muro("Muro este", sala, escena, new Vector3(76.2f, AltoDeLosMuros * 0.5f, 138f), new Vector3(0.4f, AltoDeLosMuros, 26f));

        // El interruptor, colgado: solo se llega en el salto.
        var interruptor = Caja("Interruptor colgado (en el salto)", sala, escena, new Vector3(66f, 2.6f, 133f),
                               new Vector3(1.6f, 0.6f, 1.6f), _cristalLuz, suelo: false);
        var placa = interruptor.AddComponent<PressurePlate>();
        // Los eventos del componente recién añadido vienen a null hasta que Unity los serializa.
        placa.onActivated ??= new UnityEvent();
        Brillo("Luz del interruptor", sala, new Vector3(66f, 2.6f, 133f), new Color(0.72f, 0.88f, 1f), 0.8f, 3f);

        // El puente que aparece y la rejilla que se abre: los dos, dos compases.
        var puente = Caja("Puente del interruptor", sala, escena, new Vector3(66f, 0.05f, 137f),
                          new Vector3(4f, 0.3f, 4f), _gris, suelo: true);
        var compuertaPuente = puente.AddComponent<BeatGate>();
        PonerRef(compuertaPuente, "pulse", pulso);
        Poner(compuertaPuente, "modo", (int)BeatGate.Modo.Aparece);
        Poner(compuertaPuente, "durationMeasures", 2);
        PonerVector(compuertaPuente, "activeOffset", Vector3.zero);

        var reja = Caja("Rejilla del interruptor", sala, escena, new Vector3(66f, 1.7f, 142f),
                        new Vector3(4f, 3.4f, 0.4f), _grisOscuro, suelo: false);
        var compuerta = reja.AddComponent<BeatGate>();
        PonerRef(compuerta, "pulse", pulso);
        Poner(compuerta, "modo", (int)BeatGate.Modo.SeAbre);
        Poner(compuerta, "durationMeasures", 2);
        PonerVector(compuerta, "activeOffset", new Vector3(0f, -3.6f, 0f));

        UnityEventTools.AddVoidPersistentListener(placa.onActivated, compuertaPuente.TriggerForMeasures);
        UnityEventTools.AddVoidPersistentListener(placa.onActivated, compuerta.TriggerForMeasures);

        // Detrás de la rejilla, el brasero que abre la salida.
        var brasero = Caja("Brasero del interruptor", sala, escena, new Vector3(70f, 1.2f, 145f),
                           new Vector3(0.9f, 0.9f, 0.9f), _brasa, suelo: false);
        var receptor = brasero.AddComponent<ReceptorDeMagia>();
        Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Permanente);
        PonerEnumArray(receptor, "elementosAceptados", new[] { (int)MagicElement.Fire });
        Brillo("Luz del brasero del interruptor", sala, new Vector3(70f, 1.5f, 145f), new Color(1f, 0.55f, 0.2f), 1.2f, 4f);

        var salida = Caja("Salida del interruptor", sala, escena, new Vector3(66f, 1.7f, 150f),
                          new Vector3(4f, 3.4f, 0.4f), _grisOscuro, suelo: false);
        var puerta = salida.AddComponent<BeatGate>();
        PonerRef(puerta, "pulse", pulso);
        Poner(puerta, "modo", (int)BeatGate.Modo.SeAbre);
        PonerVector(puerta, "activeOffset", new Vector3(0f, -3.6f, 0f));
        ConectarBool(receptor.alActivarse, puerta.SetPermanent, true);
        Caja("Fuego en el marco de la salida del interruptor", sala, escena,
             new Vector3(66f, 3.55f, 149.7f), new Vector3(4.2f, 0.18f, 0.3f), _brasa, suelo: false);

        var marea = Caja("Marea del interruptor", sala, escena, new Vector3(66f, 0.75f, 127f),
                         new Vector3(19.8f, 1.5f, 0.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        PonerRef(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.forward);
        Poner(mareaComp, "metersPerMeasure", 4f);
        Poner(mareaComp, "advanceAtStart", false);

        var tramo = NuevoTramo("Tramo 4: el interruptor", sala, pulso,
                               new Vector3(66f, 0.2f, 130f), new Vector3(66f, 0.2f, 153f));
        PonerRef(tramo, "marea", mareaComp);
        PonerRefs(tramo, "rejas", new Object[] { compuerta, puerta, compuertaPuente });
    }

    // â”€â”€ 5. Los tres separados (isla de pensar) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Tramo 5: los tres a la vez. El sello de mente para la marea, el molinete de viento mueve el
    /// ascensor y el cristal de arriba abre la salida. Treinta y dos velas para pensarlo.
    /// </summary>
    private static void CrearTresSeparados(Transform raiz, Scene escena, TempoClock pulso)
    {
        var sala = new GameObject("5. Los tres separados").transform;
        sala.SetParent(raiz, false);

        Suelo("Suelo de los tres", sala, escena, new Vector3(66f, -0.5f, 166f), new Vector3(28f, 1f, 30f));
        Muro("Muro oeste", sala, escena, new Vector3(51.8f, AltoDeLosMuros * 0.5f, 166f), new Vector3(0.4f, AltoDeLosMuros, 30f));
        Muro("Muro este", sala, escena, new Vector3(80.2f, AltoDeLosMuros * 0.5f, 166f), new Vector3(0.4f, AltoDeLosMuros, 30f));

        // El sello de mente, a la entrada: sujeta la marea.
        var sello = Caja("Sello de mente", sala, escena, new Vector3(66f, 1.2f, 154f),
                         new Vector3(1.4f, 0.5f, 1.4f), _cristalLuz, suelo: false);
        var mente = sello.AddComponent<ReceptorDeMagia>();
        Poner(mente, "modo", (int)ReceptorDeMagia.Modo.Permanente);
        PonerEnumArray(mente, "elementosAceptados", new[] { (int)MagicElement.Mind });
        Brillo("Luz del sello", sala, new Vector3(66f, 1.4f, 154f), new Color(0.8f, 0.7f, 1f), 1f, 4f);

        // El molinete de viento, en una repisa: mueve el ascensor mientras gira.
        Suelo("Repisa del molinete", sala, escena, new Vector3(58f, 1.2f, 162f), new Vector3(8f, 0.4f, 8f));
        var molinete = Caja("Molinete de viento", sala, escena, new Vector3(58f, 2.2f, 162f),
                            new Vector3(1f, 1f, 0.3f), _brasa, suelo: false);
        var viento = molinete.AddComponent<ReceptorDeMagia>();
        Poner(viento, "modo", (int)ReceptorDeMagia.Modo.Mantenido);
        Poner(viento, "segundosEncendido", 8f);
        PonerEnumArray(viento, "elementosAceptados", new[] { (int)MagicElement.Storm });
        PonerRef(viento, "parteQueGira", molinete.transform);
        Brillo("Luz del molinete", sala, new Vector3(58f, 2.4f, 162f), new Color(0.6f, 0.9f, 0.7f), 1f, 4f);

        var ascensor = Suelo("Ascensor de los tres", sala, escena, new Vector3(70f, 0.2f, 162f), new Vector3(4f, 0.4f, 4f));
        var elevador = ascensor.AddComponent<PlatformElevator>();
        Poner(elevador, "raiseHeight", 4.4f);
        Poner(elevador, "moveSpeed", 1.4f);
        ConectarVoid(viento.alActivarse, elevador.MoveUp);
        ConectarVoid(viento.alApagarse, elevador.MoveDown);

        // Placa arriba: en cuanto llega alguien, el ascensor baja (si no, se queda arriba para siempre).
        var placaArriba = Caja("Placa de bajada (arriba del ascensor)", sala, escena,
                               new Vector3(70f, 4.85f, 168f), new Vector3(2.4f, 0.2f, 2.4f), _grisOscuro, suelo: false);
        var bajada = placaArriba.AddComponent<PressurePlate>();
        bajada.onActivated ??= new UnityEvent();
        UnityEventTools.AddVoidPersistentListener(bajada.onActivated, elevador.MoveDown);

        // La repisa de arriba y el cristal de la salida.
        Suelo("Repisa de arriba", sala, escena, new Vector3(70f, 4.7f, 170f), new Vector3(5f, 0.4f, 10f));
        var cristal = Caja("Cristal de la salida", sala, escena, new Vector3(73f, 5.3f, 175f),
                           new Vector3(0.8f, 0.8f, 0.8f), _cristalLuz, suelo: false);
        var luz = cristal.AddComponent<ReceptorDeMagia>();
        Poner(luz, "modo", (int)ReceptorDeMagia.Modo.Permanente);
        PonerEnumArray(luz, "elementosAceptados", new[] { (int)MagicElement.Light });
        Brillo("Luz de la salida", sala, new Vector3(73f, 5.5f, 175f), new Color(0.72f, 0.88f, 1f), 1f, 4f);

        var salida = Caja("Salida de los tres", sala, escena, new Vector3(70f, 6.4f, 180f),
                          new Vector3(4f, 3.4f, 0.4f), _grisOscuro, suelo: false);
        var puerta = salida.AddComponent<BeatGate>();
        PonerRef(puerta, "pulse", pulso);
        Poner(puerta, "modo", (int)BeatGate.Modo.SeAbre);
        PonerVector(puerta, "activeOffset", new Vector3(0f, -3.6f, 0f));
        ConectarBool(luz.alActivarse, puerta.SetPermanent, true);

        // Treinta y dos velas: la cuenta atrás de pensar.
        var velas = new GameObject[32];
        for (int i = 0; i < velas.Length; i++)
        {
            float x = 53.2f + i * 0.8f;
            velas[i] = Caja($"Vela {i + 1}", sala, escena, new Vector3(x, 3.2f, 180.4f),
                            new Vector3(0.2f, 0.5f, 0.2f), _cristal, suelo: false);
            Object.DestroyImmediate(velas[i].GetComponent<Collider>());
        }
        var candelero = new GameObject("Cuenta de las 32 velas");
        candelero.transform.SetParent(sala, false);
        var cuenta = candelero.AddComponent<MeasureCandles>();
        PonerRef(cuenta, "pulse", pulso);
        Poner(cuenta, "totalMeasures", 32);
        PonerRefs(cuenta, "flames", velas);
        Poner(cuenta, "startOnEnable", false);

        // La marea entra al empezar y el sello la sujeta.
        var marea = Caja("Marea de los tres", sala, escena, new Vector3(66f, 0.75f, 152f),
                         new Vector3(27.8f, 1.5f, 0.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        PonerRef(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.forward);
        Poner(mareaComp, "metersPerMeasure", 3.5f);
        Poner(mareaComp, "advanceAtStart", false);
        ConectarVoid(mente.alActivarse, mareaComp.Pause);

        var tramo = NuevoTramo("Tramo 5: los tres separados", sala, pulso,
                               new Vector3(66f, 0.2f, 154f), new Vector3(70f, 4.7f, 179f));
        PonerRef(tramo, "marea", mareaComp);
        PonerRef(tramo, "velas", cuenta);
        PonerRefs(tramo, "rejas", new Object[] { puerta });
    }

    // â”€â”€ 6. El último estribillo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Tramo 6: el estribillo, a 132. Losas desfasadas, un tramo de lado con dos faros y la carrera
    /// final hacia el guardián, que calla y deja enfriar la lava.
    /// </summary>
    private static void CrearEstribillo(Transform raiz, Scene escena, TempoClock pulso)
    {
        var sala = new GameObject("6. El último estribillo").transform;
        sala.SetParent(raiz, false);

        Suelo("Rellano del estribillo", sala, escena, new Vector3(70f, -0.5f, 186f), new Vector3(16f, 1f, 14f));
        Suelo("Fondo del estribillo", sala, escena, new Vector3(70f, -0.5f - Foso, 216f), new Vector3(16f, 1f, 46f));
        var lava = Caja("Lava del estribillo", sala, escena, new Vector3(70f, NivelDeLaLava, 216f),
                        new Vector3(15.8f, 0.06f, 45.8f), _lava, suelo: false);
        Object.DestroyImmediate(lava.GetComponent<Collider>());
        var quema = new GameObject("Lava del estribillo que quema");
        quema.transform.SetParent(sala, false);
        quema.transform.position = new Vector3(70f, NivelDeLaLava + 0.55f, 216f);
        var zonaQuema = quema.AddComponent<BoxCollider>();
        zonaQuema.isTrigger = true;
        zonaQuema.size = new Vector3(15.8f, 1.1f, 45.8f);
        Poner(quema.AddComponent<ZonaDeDano>(), "danoPorSegundo", 40f);

        // Las losas del principio (3D): se turnan, así que hay que ir pisando la que toca.
        var losas = new GameObject[5];
        for (int i = 0; i < losas.Length; i++)
            losas[i] = LosaQueLate($"Losa del estribillo {i + 1}", sala, escena, pulso,
                                   new Vector3(70f, 0.05f, 196f + i * 6f), new Vector3(6f, 0.3f, 3.2f),
                                   empiezaQuitada: i % 2 == 1);

        // Un tramo de lado, con dos faros, en mitad del estribillo.
        var zona = new GameObject("Estribillo de lado (cámara)");
        zona.transform.SetParent(sala, false);
        zona.transform.position = new Vector3(70f, 3f, 208f);
        var caja = zona.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(16f, 10f, 56f);
        var lateral = zona.AddComponent<SidePlaneZone>();
        PonerVector(lateral, "planeNormal", Vector3.right);

        var plataformas = new GameObject[2];
        for (int i = 0; i < plataformas.Length; i++)
        {
            float z = 220f + i * 8f;
            var faro = Caja($"Faro del estribillo {i + 1}", sala, escena, new Vector3(68f, 1.5f, z),
                            new Vector3(0.8f, 0.8f, 0.8f), _cristalLuz, suelo: false);
            var receptor = faro.AddComponent<ReceptorDeMagia>();
            Poner(receptor, "modo", (int)ReceptorDeMagia.Modo.Pulso);
            PonerEnumArray(receptor, "elementosAceptados", new[] { (int)MagicElement.Light });
            var plataforma = Caja($"Plataforma del estribillo {i + 1}", sala, escena, new Vector3(70f, 0.05f, z),
                                  new Vector3(3.6f, 0.3f, 3.6f), _cristalLuz, suelo: true);
            var compuerta = plataforma.AddComponent<BeatGate>();
            PonerRef(compuerta, "pulse", pulso);
            Poner(compuerta, "modo", (int)BeatGate.Modo.Aparece);
            Poner(compuerta, "durationMeasures", 2);
            PonerVector(compuerta, "activeOffset", Vector3.zero);
            ConectarVoid(receptor.alActivarse, compuerta.TriggerForMeasures);
            plataformas[i] = plataforma;
            Brillo($"Luz del faro del estribillo {i + 1}", sala, new Vector3(68f, 1.7f, z), new Color(0.72f, 0.88f, 1f), 0.7f, 2.5f);
        }

        // La meta: el guardián.
        Suelo("Rellano del guardián", sala, escena, new Vector3(70f, -0.5f, 244f), new Vector3(16f, 1f, 12f));
        Caja("Guardián", sala, escena, new Vector3(70f, 3f, 249f), new Vector3(6f, 6f, 2f), _grisOscuro, suelo: false);
        Brillo("Luz del guardián", sala, new Vector3(70f, 4f, 247.5f), new Color(1f, 0.85f, 0.6f), 2f, 8f);

        var marea = Caja("Marea del estribillo", sala, escena, new Vector3(70f, 0.75f, 182f),
                         new Vector3(15.8f, 1.5f, 0.8f), _rojo, suelo: false);
        var mareaComp = marea.AddComponent<PulseTide>();
        PonerRef(mareaComp, "pulse", pulso);
        PonerVector(mareaComp, "advanceDirection", Vector3.forward);
        Poner(mareaComp, "metersPerMeasure", 5f);
        Poner(mareaComp, "advanceAtStart", false);

        var tramo = NuevoTramo("Tramo 6: el estribillo", sala, pulso,
                               new Vector3(70f, 0.2f, 186f), new Vector3(70f, 0.2f, 243f), -0.9f, 132f, companerosEnLaSalida: true);
        PonerRef(tramo, "marea", mareaComp);
        PonerRefs(tramo, "losas", losas);
        PonerRefs(tramo, "rejas", plataformas);
    }

    /// <summary>Un tramo nuevo, con lo común ya puesto: se arranca al acercarse y se reintenta solo.</summary>
    private static TramoDelTemploQueCanta NuevoTramo(string nombre, Transform padre, TempoClock pulso,
        Vector3 salida, Vector3 meta, float alturaDelFoso = -0.9f, float bpm = 120f, bool companerosEnLaSalida = false,
        float radioDeLaSalida = 5f, float rumboDeSalida = 0f)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);

        var puntoSalida = new GameObject("Salida del tramo").transform;
        puntoSalida.SetParent(go.transform, false);
        puntoSalida.SetPositionAndRotation(salida, Quaternion.Euler(0f, rumboDeSalida, 0f));
        var puntoMeta = new GameObject("Meta del tramo").transform;
        puntoMeta.SetParent(go.transform, false);
        puntoMeta.position = meta;

        var tramo = go.AddComponent<TramoDelTemploQueCanta>();
        PonerRef(tramo, "salidaDelJugador", puntoSalida);
        PonerRef(tramo, "meta", puntoMeta);
        PonerRef(tramo, "pulso", pulso);
        Poner(tramo, "empezarAlActivarse", false);
        Poner(tramo, "radioDeLaSalida", radioDeLaSalida);
        Poner(tramo, "segundosDeCuentaAtras", 0);
        Poner(tramo, "claveDeSalida", "");
        Poner(tramo, "textoDeSalida", "");
        Poner(tramo, "fundido", 0.2f);
        Poner(tramo, "pausaAntesDelReintento", 0.25f);
        Poner(tramo, "reintentarAlFallar", true);
        Poner(tramo, "alturaDelFoso", alturaDelFoso);
        Poner(tramo, "bpm", bpm);
        Poner(tramo, "radioDeLaMeta", 3f);
        // En los tramos sin suelo para ellos (los laterales), se quedan esperando en la salida.
        if (companerosEnLaSalida) PonerRef(tramo, "puestoDeLosCompaneros", puntoSalida);
        return tramo;
    }
}


