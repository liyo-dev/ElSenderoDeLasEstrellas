using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Lo jugable de la segunda mitad de Chuchelandia, cada parte activa solo en su tramo:
/// - El heraldo (tramo 2, antes del pergamino): se le habla (<see cref="NarrativeActor"/> sin IA, el
///   grafo espera la charla) y está marcado en el minimapa; la charla lanza la cinemática del pergamino.
/// - La Puerta del Corazón (tramo 2, tras el pergamino): el candado (<see cref="ReaccionSegunQuienLoPrueba"/> con
///   <see cref="ReceptorDeMagia"/> e <see cref="Interactable"/>): magia de Estela, ganzúa de Liam,
///   empujón de Will; probados los tres, o hablando con el heraldo, emite CHUCHE_PUERTA_INTENTOS.
/// - Las clases de la Condesa (tramo 5) y el Duelo de Ternura (tramo 8): dos
///   <see cref="ConcursoPorRondas"/> con las mismas pruebas de ronda (equilibrio, ritmo, mantener);
///   las clases sin marcador, el duelo con los jueces y sus carteles guionizados.
/// - «¡TE QUIERO!» (tramo 9): <see cref="PruebaDePasos"/> como Will: el mantel, el sirope y la silla.
/// - Lo que estorba de la escena (las gominolas de demo del trono) se retira con
///   <see cref="RetiradosDeEscena"/>, que lo repone al volver a montar.
/// Las frases, gestos, puntos y pruebas se retocan en las tablas de abajo.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string Sonidos = "Assets/Audio/FREE SOUND PACK_TM(355)";
    private const string Efectos = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs";
    private const string RutaEstiloDeMenu = "Assets/_UI/Estilo_MenuSencillo.asset";
    private const string RegistroDeRetirados = "Chuchelandia — retirado (no tocar)";

    // ── Lo que estorba ──────────────────────────────────────────────────────────────────────
    // Objeto de la escena (ruta) y los hijos que se retiran (por el principio del nombre).
    private static readonly (string ruta, string[] nombres, string motivo)[] LoQueEstorba =
    {
        ("AI/Characters/Throne", new[] { "King's Grummy 02", "Nutty_Knight", "CandyKing" }, "gominolas de demo en el sitio del duelo"),
    };

    // ── Tabla de la Puerta ──────────────────────────────────────────────────────────────────
    // Lo que se prueba es la propia Puerta del Corazón (el portal en forma de corazón, MontarPuertaDelCorazon):
    // ella se ríe, suelta purpurina y da la piruleta. Aquí solo va el cuerpo que recibe la magia y la A.
    private const float TamanoDelCandado = 2.4f;
    private const string Piruleta = Pack + "/Prefabs/Sweets/Lollipop_01.prefab";

    // ── Tabla del salón (atrezo de «¡TE QUIERO!», se queda después) ─────────────────────────
    private static readonly (string nombre, string prefab, string marca, Vector3 desplazamiento, float tamano)[] AtrezoDelSalon =
    {
        ("Mesa del banquete", Pack + "/Prefabs/Sweets/Cake_02.prefab", "M_Duelo_Banquete", Vector3.zero, 1.2f),
        ("Mantel", Pack + "/Prefabs/Sweets/Carpet_01.prefab", "M_Duelo_Banquete", new Vector3(0f, 1.2f, 0f), 1.5f),
        ("Fuente de sirope", PilonDeLaFuente, "M_Duelo_Fuente", Vector3.zero, DiametroDeLaFuente),
        ("Silla de Will", Pack + "/Prefabs/Sweets/Marshmallow_05.prefab", "M_Cine_Salon_TeQuiero_Will", Vector3.zero, 0.5f),
        ("Silla de Liam", Pack + "/Prefabs/Sweets/Marshmallow_06.prefab", "M_Cine_Salon_TeQuiero_Liam", Vector3.zero, 0.5f),
    };

    // ── Montaje ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Repone lo retirado en el montaje anterior y vuelve a retirar lo que estorba.</summary>
    private static void RetirarLoQueEstorba(Scene escena, StringBuilder informe)
    {
        RetiradosDeEscena.Reponer(escena, RegistroDeRetirados, informe);
        var objetos = TodosLosObjetos(escena);
        foreach (var (ruta, nombres, motivo) in LoQueEstorba)
        {
            Transform padre = null;
            foreach (var t in objetos)
                if (RutaDe(t) == ruta) { padre = t; break; }
            if (padre == null) { informe.AppendLine($"Retirar: no encuentro «{ruta}»."); continue; }
            int n = 0;
            foreach (Transform hijo in padre)
                foreach (var nombre in nombres)
                    if (hijo.name.StartsWith(nombre) && RetiradosDeEscena.Retirar(escena, hijo.gameObject, RegistroDeRetirados)) { n++; break; }
            informe.AppendLine($"Retirados (inactivos, fuera de la build; se reponen al volver a montar): {n} × {motivo}.");
        }
    }

    private static void MontarSegundaMitad(Transform raiz, Transform pruebas, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var estilo = AssetDatabase.LoadAssetAtPath<EstiloDeMenuSencillo>(RutaEstiloDeMenu);
        if (estilo == null) informe.AppendLine($"AVISO: no encuentro {RutaEstiloDeMenu}; los concursos usan el estilo por defecto.");
        var reparto = raiz.Find("Reparto");
        var salon = Hijo(raiz, "Salón del duelo");
        var atrezo = MontarAtrezoDelSalon(salon, marcas, informe);

        MontarPuerta(pruebas, reparto, marcas, informe);
        MontarClases(pruebas, reparto, marcas, estilo, informe);
        MontarDuelo(pruebas, reparto, marcas, estilo, informe);
        MontarTeQuiero(pruebas, marcas, atrezo, informe);
        MontarAtrezoDeTeQuiero(salon, marcas, atrezo, informe);
    }

    // ── La Puerta del Corazón ───────────────────────────────────────────────────────────────

    private static void MontarPuerta(Transform pruebas, Transform reparto, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var pergamino = System.Array.Find(TablaDeCinematicas, c => c.Nodo == "cine-heraldo");
        MontarCharlaConElHeraldo(pruebas, reparto, pergamino, informe);
        if (!marcas.TryGetValue("M_Puerta_Candado", out var sitio)) { informe.AppendLine("Puerta: falta la marca M_Puerta_Candado."); return; }
        var raiz = PruebaEnTramo(pruebas, "La Puerta del Corazón", pergamino.Salida, Senal.PuertaIntentos);

        var candado = new GameObject("La Puerta (se prueba a abrir)");
        candado.transform.SetParent(raiz, false);
        candado.transform.SetPositionAndRotation(sitio.position, sitio.rotation);
        var modelo = _ventanaDeLaPuerta;
        if (modelo == null) informe.AppendLine("Puerta: no está la ventana de la Puerta del Corazón; las reacciones salen del punto del candado.");
        var solido = candado.AddComponent<BoxCollider>();
        solido.size = new Vector3(TamanoDelCandado, TamanoDelCandado, 0.4f);
        var receptor = candado.AddComponent<ReceptorDeMagia>();
        Fijar(receptor, ("modo", (int)ReceptorDeMagia.Modo.Pulso), ("sfxAlActivar", ""), ("radioDeApuntado", 30f));
        FijarLista(receptor, "elementosAceptados", new List<Object>());

        var reaccion = candado.AddComponent<ReaccionSegunQuienLoPrueba>();
        // La piruleta la «escupe» la Puerta: aparece delante del corazón, entre la puerta y el grupo.
        var piruleta = Atrezo(candado.transform, Piruleta, "Piruleta", sitio.position + sitio.forward * 1.2f + Vector3.down * 0.4f,
                              sitio.rotation, 0.7f, informe);
        if (piruleta != null)
        {
            foreach (var c in piruleta.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            piruleta.SetActive(false);
        }
        var reacciones = new List<ReaccionSegunQuienLoPrueba.Reaccion>
        {
            new ReaccionSegunQuienLoPrueba.Reaccion { personaje = Slot.Estela, como = ReaccionSegunQuienLoPrueba.Como.ConMagia, cuenta = true,
                           vfx = Cargar<GameObject>(Efectos + "/Confetti/Confetti_blast_multicolor.prefab"),
                           sonido = Cargar<AudioClip>(Sonidos + "/Musical Instruments(16)/Trumpet-010.wav"),
                           frases = Frases((Slot.Liam, "CHUCHELANDIA_PUERTA_LIAM_INTERESANTE"), (Slot.Estela, "CHUCHELANDIA_PUERTA_ESTELA_NO_INTERESANTE")) },
            new ReaccionSegunQuienLoPrueba.Reaccion { personaje = Slot.Liam, como = ReaccionSegunQuienLoPrueba.Como.Interactuando, cuenta = true, gesto = "Loot01_Begin",
                           vfx = Cargar<GameObject>(Efectos + "/Sparkle/Sparkle_ellow.prefab"),
                           sonido = Cargar<AudioClip>(Sonidos + "/Misc(58)/Balloon_Pop-003.wav"), aparece = piruleta, segundosQueAparece = 3.5f,
                           frases = Frases((Slot.Liam, "CHUCHELANDIA_PUERTA_LIAM_PIRULETA_01"), (Slot.Liam, "CHUCHELANDIA_PUERTA_LIAM_PIRULETA_02")) },
            new ReaccionSegunQuienLoPrueba.Reaccion { personaje = Slot.Will, como = ReaccionSegunQuienLoPrueba.Como.Interactuando, cuenta = true, gesto = "Push_InPlace_NoWeapon",
                           sonido = Cargar<AudioClip>(Sonidos + "/Cartoon(15)/cartoon_rattle(6).wav"), sacudir = true,
                           frases = Frases((Slot.Will, "CHUCHELANDIA_PUERTA_WILL_RISITA"), (Slot.Liam, "CHUCHELANDIA_PUERTA_LIAM_RISITA")) },
            new ReaccionSegunQuienLoPrueba.Reaccion { personaje = Slot.Estela, como = ReaccionSegunQuienLoPrueba.Como.Interactuando, cuenta = false,
                           frases = Frases((Slot.Estela, "CHUCHELANDIA_PUERTA_ESTELA_TOCAR")) },
        };
        Fijar(reaccion, ("claveDePista", "CHUCHELANDIA_PUERTA_PISTA"));
        Asignar(reaccion, "reacciones", reacciones);
        if (modelo != null) Asignar(reaccion, "centro", modelo);

        var interactuable = PuntoDeInteraccion(candado.transform, Vector3.zero, 3.2f, 0f);
        UnityEventTools.AddPersistentListener(interactuable.OnInteract, reaccion.AlInteractuar);
        UnityEventTools.AddPersistentListener(receptor.alActivarse, reaccion.AlRecibirMagia);

        var emisor = candado.AddComponent<SignalEmitter>();
        emisor.eventKey = Senal.PuertaIntentos;
        emisor.trigger = SignalEmitter.TriggerType.Manual;
        emisor.once = true;
        UnityEventTools.AddPersistentListener(reaccion.alProbarTodas, emisor.Send);

        // Hablar con el heraldo también da por hechos los intentos.
        var heraldo = Actor(reparto, IdDelHeraldo);
        if (heraldo != null)
        {
            var hablar = new GameObject("Hablar con el heraldo");
            hablar.transform.SetParent(raiz, false);
            hablar.transform.SetPositionAndRotation(heraldo.position, heraldo.rotation);
            PuntoDeInteraccion(hablar.transform, Vector3.zero, 2f, 0f, enEsteObjeto: true);
            var porHablar = hablar.AddComponent<SignalEmitter>();
            porHablar.eventKey = Senal.PuertaIntentos;
            porHablar.trigger = SignalEmitter.TriggerType.OnInteract;
            porHablar.once = true;
        }
        informe.AppendLine($"Puerta: la Puerta del Corazón con {reacciones.Count} reacciones (magia de Estela, ganzúa de Liam, empujón de Will){(heraldo != null ? " y charla con el heraldo" : "")}; " +
                           $"activo desde {pergamino.Salida} hasta {Senal.PuertaIntentos}.");
    }

    /// <summary>
    /// «Investigad el reino»: de <see cref="Senal.PuertaEmpieza"/> a que empieza el pergamino, al heraldo
    /// se le puede hablar (el grafo espera esa charla para lanzar la cinemática) y lleva la marca del
    /// minimapa de la misión de la llegada.
    /// </summary>
    private static void MontarCharlaConElHeraldo(Transform pruebas, Transform reparto, Cine pergamino, StringBuilder informe)
    {
        var heraldo = Actor(reparto, IdDelHeraldo);
        if (heraldo == null) { informe.AppendLine("Heraldo: no lo encuentro en el reparto; no se le podrá hablar para el pergamino."); return; }
        var charla = Charla(heraldo, IdDelHeraldo, IconoDeMision(informe));
        MarcaDeObjetivo(charla, IdMisionBienvenida);
        EncenderEnTramo(Hijo(pruebas, "Investigar el reino: el heraldo"), new List<GameObject> { charla }, new[] { Senal.PuertaEmpieza }, pergamino.Entrada);
        informe.AppendLine($"Heraldo: se le habla de {Senal.PuertaEmpieza} a {pergamino.Entrada} (lanza el pergamino), con su marca en el minimapa.");
    }

    // ── Las clases de la Condesa ────────────────────────────────────────────────────────────

    private static void MontarClases(Transform pruebas, Transform reparto, Dictionary<string, Transform> marcas,
                                     EstiloDeMenuSencillo estilo, StringBuilder informe)
    {
        if (!marcas.TryGetValue("M_Clases_Estela", out var salida)) { informe.AppendLine("Clases: falta la marca M_Clases_Estela."); return; }
        var raiz = PruebaEnTramo(pruebas, "Clases de la Condesa", Senal.ClasesLlegada, Senal.ClasesHechas);
        var condesa = Actor(reparto, "Actor_CondesaMerengue");
        var animCondesa = condesa != null ? condesa.GetComponentInChildren<Animator>(true) : null;
        const string Nombre = "CHAR_CONDESA_MERENGUE";

        var go = new GameObject("Concurso: clases");
        go.transform.SetParent(raiz, false);
        var concurso = Concurso(go, salida, marcas.TryGetValue("M_Clases_Cam", out var cam) ? cam : null, Slot.Estela,
                                (Slot.Will, "M_Clases_Will"), (Slot.Liam, "M_Clases_Liam"), marcas);

        var reverencia = PruebaDeLaRonda<EquilibrioEnRonda>(go, "Ronda 1 · Reverencia (equilibrio)", "CONCURSO_EQUILIBRIO",
            ("duracion", 6f), ("empuje", 0.8f), ("sesgo", 0.45f), ("correccion", 1.7f), ("zonaBuena", 0.3f));
        var saludo = PruebaDeLaRonda<RitmoEnRonda>(go, "Ronda 2 · Saludo real (ritmo)", "CONCURSO_RITMO",
            ("pulsosPorMinuto", 88f), ("notas", 6), ("ventana", 0.24f), ("pulsosDeEntrada", 2));
        var risita = PruebaDeLaRonda<MantenerEnRonda>(go, "Ronda 3 · Risita (mantener)", "CONCURSO_MANTENER",
            ("cargaPorSegundo", 0.9f), ("objetivoDesde", 0.22f), ("objetivoHasta", 0.36f));
        var carino = PruebaDeLaRonda<MantenerEnRonda>(go, "Ronda 4 · Palabras de cariño (mantener)", "CONCURSO_MANTENER",
            ("cargaPorSegundo", 0.2f), ("objetivoDesde", 0.6f), ("objetivoHasta", 0.75f));

        ConcursoPorRondas.Paso Condesa(string clave, string gesto = null) => new ConcursoPorRondas.Paso { clave = clave, hablante = condesa, claveDelNombre = Nombre, gestoDe = gesto != null ? animCondesa : null, gesto = gesto };
        var rondas = new List<ConcursoPorRondas.Ronda>
        {
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_REVERENCIA", textoDelTitulo = "La reverencia", prueba = reverencia, segundosDeInstruccion = 2.5f,
                        anuncio = L(Condesa("CHUCHELANDIA_CLASES_REVERENCIA_01", "Marshies_Hide")),
                        gestoAlAcabar = "Dizzy_NoWeapon",
                        despues = L(Condesa("CHUCHELANDIA_CLASES_REVERENCIA_02"), DiceElGrupo("Estela", "CHUCHELANDIA_CLASES_REVERENCIA_03"),
                                    Condesa("CHUCHELANDIA_CLASES_REVERENCIA_04")) },
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_SALUDO", textoDelTitulo = "El saludo real", prueba = saludo, segundosDeInstruccion = 2.5f,
                        anuncio = L(Condesa("CHUCHELANDIA_CLASES_SALUDO_01", "Marshies_Wave")),
                        gestoAlAcabar = "HandWave01",
                        despues = L(Condesa("CHUCHELANDIA_CLASES_SALUDO_02")) },
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_RISITA", textoDelTitulo = "La risita", prueba = risita, segundosDeInstruccion = 2.5f,
                        anuncio = L(Condesa("CHUCHELANDIA_CLASES_RISITA_01", "Marshies_Jump")),
                        gestoAlAcabar = "Laugh01",
                        despues = L(new ConcursoPorRondas.Paso { sonido = Cargar<AudioClip>(Sonidos + "/Cartoon(15)/Cartoon_Motor_Whistle.wav"), espera = 1f },
                                    DiceElGrupo("Liam", "CHUCHELANDIA_CLASES_RISITA_02")) },
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_CARINO", textoDelTitulo = "Palabras de cariño", prueba = carino, segundosDeInstruccion = 2.5f,
                        anuncio = L(Condesa("CHUCHELANDIA_CLASES_CARINO_01", "Marshies_Happy_Dance")),
                        despues = L(DiceElGrupo("Estela", "CHUCHELANDIA_CLASES_CARINO_02"), DiceElGrupo("Estela", "CHUCHELANDIA_CLASES_CARINO_03"),
                                    Condesa("CHUCHELANDIA_CLASES_FIN")) },
        };
        Asignar(concurso, "estilo", estilo);
        Asignar(concurso, "mostrarMarcador", false);
        Asignar(concurso, "rondas", rondas);
        EmitirAlGanar(concurso, Senal.ClasesHechas);
        informe.AppendLine($"Clases: concurso de {rondas.Count} lecciones con la Condesa{(condesa == null ? " (NO encuentro a la Condesa en el reparto)" : "")}; emite {Senal.ClasesHechas}.");
    }

    // ── El Duelo de Ternura ─────────────────────────────────────────────────────────────────

    private static void MontarDuelo(Transform pruebas, Transform reparto, Dictionary<string, Transform> marcas,
                                    EstiloDeMenuSencillo estilo, StringBuilder informe)
    {
        if (!marcas.TryGetValue("M_Duelo_Centro", out var centro) || !marcas.TryGetValue("M_Duelo_Palco", out var palco))
        { informe.AppendLine("Duelo: faltan las marcas M_Duelo_Centro o M_Duelo_Palco."); return; }
        var raiz = PruebaEnTramo(pruebas, "Duelo de Ternura", Senal.DueloEmpieza, Senal.DueloTerminado);

        var salida = new GameObject("Salida de la candidata").transform;
        salida.SetParent(raiz, false);
        salida.SetPositionAndRotation(centro.position, Quaternion.LookRotation(Plano(palco.position - centro.position)));

        var pompon = Actor(reparto, "Actor_Pompon");
        var animPompon = pompon != null ? pompon.GetComponentInChildren<Animator>(true) : null;
        var suplente = Actor(reparto, "Actor_HeraldoSuplente");
        var presidente = Actor(reparto, "Actor_JuezPresidente");
        var malvavisco = Actor(reparto, "Actor_JuezMarshie");
        var lagrimitas = Actor(reparto, "Actor_JuezEyeBud");

        var go = new GameObject("Concurso: duelo");
        go.transform.SetParent(raiz, false);
        var concurso = Concurso(go, salida, marcas.TryGetValue("M_Duelo_Cam", out var cam) ? cam : null, Slot.Estela,
                                (Slot.Will, "M_Duelo_Will"), (Slot.Liam, "M_Duelo_Liam"), marcas);

        var reverencia = PruebaDeLaRonda<EquilibrioEnRonda>(go, "Ronda 1 · Reverencia (equilibrio)", "CONCURSO_EQUILIBRIO",
            ("duracion", 7f), ("empuje", 1f), ("sesgo", 0.55f), ("correccion", 1.7f), ("zonaBuena", 0.28f));
        var nana = PruebaDeLaRonda<RitmoEnRonda>(go, "Ronda 2 · Canción de cuna (ritmo)", "CONCURSO_RITMO",
            ("pulsosPorMinuto", 100f), ("notas", 8), ("ventana", 0.2f), ("pulsosDeEntrada", 2));
        var ojitos = PruebaDeLaRonda<MantenerEnRonda>(go, "Ronda 3 · Ojitos (mantener)", "CONCURSO_MANTENER",
            ("cargaPorSegundo", 0.35f), ("objetivoDesde", 0.8f), ("objetivoHasta", 0.9f));

        ConcursoPorRondas.Paso Suplente(string clave) => new ConcursoPorRondas.Paso { clave = clave, hablante = suplente, claveDelNombre = "CHAR_HERALDO_SUPLENTE" };
        ConcursoPorRondas.Paso Pompon(string gesto, string sonido, float espera) => new ConcursoPorRondas.Paso { gestoDe = animPompon, gesto = gesto, espera = espera,
                                                                              sonido = Cargar<AudioClip>(Sonidos + sonido) };
        List<ConcursoPorRondas.Cartel> DiezDeCadaUno() => new List<ConcursoPorRondas.Cartel> { Cartelito(2, "CHUCHELANDIA_DUELO_CARTEL_DIEZ", 10), Cartelito(1, "CHUCHELANDIA_DUELO_CARTEL_DIEZ", 10), Cartelito(0, "CHUCHELANDIA_DUELO_CARTEL_DIEZ", 10) };

        var rondas = new List<ConcursoPorRondas.Ronda>
        {
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_REVERENCIA", textoDelTitulo = "La reverencia", prueba = reverencia,
                        anuncio = L(Suplente("CHUCHELANDIA_DUELO_RONDA1")),
                        actuacionDelRival = L(Suplente("CHUCHELANDIA_DUELO_TURNO_POMPON"), Pompon("Grummy_Defeat", "/Cartoon(15)/Cartoon_Plastic_Boing.wav", 1.8f)),
                        cartelesDelRival = DiezDeCadaUno(),
                        turnoDelJugador = L(Suplente("CHUCHELANDIA_DUELO_TURNO_CANDIDATA")),
                        gestoAlAcabar = "Dizzy_NoWeapon",
                        cartelesDelJugador = new List<ConcursoPorRondas.Cartel> { Cartelito(0, "CHUCHELANDIA_DUELO_CARTEL_DOS", 2), Cartelito(1, "CHUCHELANDIA_DUELO_CARTEL_UNO", 1),
                                                                Cartelito(2, "CHUCHELANDIA_DUELO_CARTEL_ESTAS_BIEN", 0) } },
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_NANA", textoDelTitulo = "La canción de cuna", prueba = nana,
                        anuncio = L(Suplente("CHUCHELANDIA_DUELO_RONDA2")),
                        actuacionDelRival = L(Suplente("CHUCHELANDIA_DUELO_TURNO_POMPON"), Pompon("Marshies_Happy_Dance", "/Misc(58)/Bubble-001.wav", 2.4f)),
                        cartelesDelRival = DiezDeCadaUno(),
                        turnoDelJugador = L(Suplente("CHUCHELANDIA_DUELO_TURNO_CANDIDATA")),
                        gestoAlAcabar = "HeadNod01",
                        cartelesDelJugador = new List<ConcursoPorRondas.Cartel> { Cartelito(0, "CHUCHELANDIA_DUELO_CARTEL_DOS", 2), Cartelito(1, "CHUCHELANDIA_DUELO_CARTEL_DOS", 2),
                                                                Cartelito(2, "CHUCHELANDIA_DUELO_CARTEL_UNO", 1), Cartelito(2, "CHUCHELANDIA_DUELO_CARTEL_MAS_UNO", 1) },
                        despues = L(DiceElGrupo("Will", "CHUCHELANDIA_DUELO_WILL_COMO_VAMOS"), DiceElGrupo("Liam", "CHUCHELANDIA_DUELO_LIAM_SESENTA_A_NUEVE"),
                                    DiceElGrupo("Will", "CHUCHELANDIA_DUELO_WILL_A_FAVOR"), new ConcursoPorRondas.Paso { espera = 1f }) },
            new ConcursoPorRondas.Ronda { claveDelTitulo = "CHUCHELANDIA_TITULO_OJITOS", textoDelTitulo = "Los ojitos", prueba = ojitos, multiplicador = 100,
                        anuncio = L(Suplente("CHUCHELANDIA_DUELO_RONDA3"), Suplente("CHUCHELANDIA_DUELO_RONDA3_CIEN"),
                                    DiceElGrupo("Liam", "CHUCHELANDIA_DUELO_LIAM_SIN_SENTIDO"),
                                    new ConcursoPorRondas.Paso { clave = "CHUCHELANDIA_DUELO_FRESA_TRADICION", claveDelNombre = "CHAR_GOMINOLA_FRESA" }),
                        actuacionDelRival = L(Suplente("CHUCHELANDIA_DUELO_TURNO_POMPON"), Pompon("Grummy_Hit_Reaction", "/Musical Instruments(16)/Chimes_Harp-012.wav", 2.2f)),
                        cartelesDelRival = DiezDeCadaUno(),
                        turnoDelJugador = L(Suplente("CHUCHELANDIA_DUELO_TURNO_CANDIDATA")),
                        gestoDelJugador = "Beg01_Loop",
                        despues = L(DiceElGrupo("Liam", "CHUCHELANDIA_DUELO_LIAM_PERDEMOS")) },
        };

        var jueces = new List<ConcursoPorRondas.Juez>
        {
            Juez(presidente, null),
            Juez(malvavisco, "Marshies_Jump"),
            Juez(lagrimitas, null),
        };
        var colocar = new List<ConcursoPorRondas.Colocacion>();
        if (pompon != null && marcas.TryGetValue("M_Cine_Salon_TeQuiero_Pompon", out var sitioDePompon))
        {
            var mirando = new GameObject("Sitio de Pompón").transform;
            mirando.SetParent(raiz, false);
            mirando.SetPositionAndRotation(sitioDePompon.position, Quaternion.LookRotation(Plano(palco.position - sitioDePompon.position)));
            colocar.Add(new ConcursoPorRondas.Colocacion { quien = pompon, donde = mirando });
        }

        Asignar(concurso, "estilo", estilo);
        Asignar(concurso, "mostrarMarcador", true);
        Asignar(concurso, "claveDelRival", "CHAR_POMPON");
        Asignar(concurso, "claveDelJugador", "CHAR_ESTELA");
        Asignar(concurso, "sonidoDelCartel", Cargar<AudioClip>(Sonidos + "/Misc(58)/Paper_Wrapping_&_Flip-017.wav"));
        Asignar(concurso, "jueces", jueces);
        Asignar(concurso, "rondas", rondas);
        Asignar(concurso, "colocarAlEmpezar", colocar);
        EmitirAlGanar(concurso, Senal.DueloTerminado);

        int faltan = (pompon == null ? 1 : 0) + (suplente == null ? 1 : 0) + (presidente == null ? 1 : 0) + (malvavisco == null ? 1 : 0) + (lagrimitas == null ? 1 : 0);
        informe.AppendLine($"Duelo: {rondas.Count} rondas (equilibrio, ritmo, mantener), 3 jueces, marcador guionizado (60 a 9 tras la segunda); emite {Senal.DueloTerminado}" +
                           (faltan > 0 ? $". AVISO: faltan {faltan} actores del reparto." : "."));

        static ConcursoPorRondas.Juez Juez(Transform t, string gesto) => new ConcursoPorRondas.Juez
            { sitio = t, animator = t != null ? t.GetComponentInChildren<Animator>(true) : null, gestoAlLevantar = gesto };
        static ConcursoPorRondas.Cartel Cartelito(int juez, string clave, int puntos) => new ConcursoPorRondas.Cartel { juez = juez, clave = clave, puntos = puntos };
    }

    // ── «¡TE QUIERO!» ───────────────────────────────────────────────────────────────────────

    private static void MontarTeQuiero(Transform pruebas, Dictionary<string, Transform> marcas, Dictionary<string, GameObject> atrezo, StringBuilder informe)
    {
        if (!marcas.TryGetValue("M_Duelo_Will", out var salida)) { informe.AppendLine("«¡TE QUIERO!»: falta la marca M_Duelo_Will."); return; }
        var raiz = PruebaEnTramo(pruebas, "¡TE QUIERO!", Senal.DueloTerminado, Senal.TeQuieroHecho);
        var go = new GameObject("Prueba: ¡TE QUIERO!");
        go.transform.SetParent(raiz, false);
        var prueba = go.AddComponent<PruebaDePasos>();
        Fijar(prueba, ("salidaDelJugador", salida), ("segundosDeCuentaAtras", 0), ("claveDeSalida", ""), ("textoDeSalida", ""));

        var grupo = go.AddComponent<GrupoEnLaPrueba>();
        Fijar(grupo, ("alMando", (int)Slot.Will));
        FijarFrases(grupo, new[] { (Slot.Will, "CHUCHELANDIA_TEQUIERO_IDEA_01"), (Slot.Liam, "CHUCHELANDIA_TEQUIERO_IDEA_02") },
                    new (Slot, string)[0], new (Slot, string)[0]);
        var puestos = new List<PuestoDeTabla>();
        if (marcas.TryGetValue("M_Cine_Salon_Estela", out var centro)) puestos.Add(new PuestoDeTabla { Personaje = Slot.Estela, Sitio = centro, Numerito = GrupoEnLaPrueba.Numerito.Quieto });
        if (marcas.TryGetValue("M_Cine_Salon_TeQuiero_LiamJunto", out var junto))
            puestos.Add(new PuestoDeTabla { Personaje = Slot.Liam, Sitio = junto, Numerito = GrupoEnLaPrueba.Numerito.Quieto });
        FijarPuestos(grupo, puestos);

        atrezo.TryGetValue("Mantel", out var mantel);
        var pasos = new List<PruebaDePasos.PasoConInteraccion>
        {
            PasoDe(raiz, marcas, "M_Duelo_Banquete", "Coger el mantel", "CHUCHELANDIA_TEQUIERO_PISTA_MANTEL", "Loot01_Begin",
                   "CHUCHELANDIA_TEQUIERO_MANTEL", "Will", "/Misc(58)/Paper_Wrapping_&_Flip-016.wav", mantel),
            PasoDe(raiz, marcas, "M_Duelo_Fuente", "Escribir con sirope", "CHUCHELANDIA_TEQUIERO_PISTA_SIROPE", "Loot01_Begin",
                   "CHUCHELANDIA_TEQUIERO_SIROPE", "Will", "/Misc(58)/Water_Drip-004.wav", null),
            PasoDe(raiz, marcas, "M_Cine_Salon_TeQuiero_Will", "Subirse a la silla", "CHUCHELANDIA_TEQUIERO_PISTA_SILLA", null,
                   "CHUCHELANDIA_TEQUIERO_SILLA", "Liam", "/Cartoon(15)/Cartoon_Plastic_Boing.wav", null),
        };
        pasos.RemoveAll(p => p == null);
        Asignar(prueba, "pasos", pasos);
        EmitirAlGanar(prueba, Senal.TeQuieroHecho);
        informe.AppendLine($"«¡TE QUIERO!»: {pasos.Count} pasos como Will (solo Will puede hacerlos); emite {Senal.TeQuieroHecho}, que lanza la cinemática.");
    }

    private static PruebaDePasos.PasoConInteraccion PasoDe(Transform padre, Dictionary<string, Transform> marcas, string marca, string nombre,
                                                           string aviso, string gesto, string clave, string hablante, string sonido, GameObject ocultar)
    {
        if (!marcas.TryGetValue(marca, out var sitio)) return null;
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(sitio.position, sitio.rotation);
        var punto = PuntoDeInteraccion(go.transform, Vector3.zero, 1.8f, 1.8f, enEsteObjeto: true);
        var soloWill = go.AddComponent<WillOnlyInteractable>();
        Fijar(soloWill, ("avisoKey", "CHUCHELANDIA_TEQUIERO_SOLO_WILL"), ("showPersistentIcon", false));
        return new PruebaDePasos.PasoConInteraccion
        {
            punto = punto, claveDeAviso = aviso, gestoDelJugador = gesto, clave = clave, hablante = hablante,
            sonido = Cargar<AudioClip>(Sonidos + sonido), ocultarAlHacerlo = ocultar,
        };
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────────────────

    /// <summary>El atrezo del salón (mesa, mantel, fuente, sillas), que se queda en la escena. Devuelve cada pieza por nombre.</summary>
    private static Dictionary<string, GameObject> MontarAtrezoDelSalon(Transform padre, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var hechas = new Dictionary<string, GameObject>();
        foreach (var (nombre, prefab, marca, desplazamiento, tamano) in AtrezoDelSalon)
        {
            if (!marcas.TryGetValue(marca, out var sitio)) { informe.AppendLine($"Salón: falta la marca {marca} ({nombre})."); continue; }
            var go = Atrezo(padre, prefab, nombre, sitio.position + desplazamiento, sitio.rotation, tamano, informe);
            if (go == null) continue;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            hechas[nombre] = go;
        }
        informe.AppendLine($"Salón: {hechas.Count} piezas de atrezo (provisionales, del pack).");
        return hechas;
    }

    /// <summary>Un concurso en el objeto, con el grupo (quién juega y dónde esperan los otros dos), salida y plano.</summary>
    private static ConcursoPorRondas Concurso(GameObject go, Transform salida, Transform plano, Slot alMando,
                                              (Slot, string) primero, (Slot, string) segundo, Dictionary<string, Transform> marcas)
    {
        var concurso = go.AddComponent<ConcursoPorRondas>();
        var camara = go.AddComponent<CinematicCameraDriver>();
        Fijar(concurso, ("salidaDelJugador", salida), ("segundosDeCuentaAtras", 0), ("claveDeSalida", ""), ("textoDeSalida", ""),
              ("reintentarAlFallar", false));
        Asignar(concurso, "camara", camara);
        Asignar(concurso, "plano", plano);

        var grupo = go.AddComponent<GrupoEnLaPrueba>();
        Fijar(grupo, ("alMando", (int)alMando));
        FijarFrases(grupo, new (Slot, string)[0], new (Slot, string)[0], new (Slot, string)[0]);
        var puestos = new List<PuestoDeTabla>();
        foreach (var (personaje, marca) in new[] { primero, segundo })
            if (marcas.TryGetValue(marca, out var sitio))
                puestos.Add(new PuestoDeTabla { Personaje = personaje, Sitio = sitio, Numerito = GrupoEnLaPrueba.Numerito.Quieto });
        FijarPuestos(grupo, puestos);
        return concurso;
    }

    /// <summary>Una prueba de ronda en un hijo del concurso, con su instrucción y sus datos.</summary>
    private static T PruebaDeLaRonda<T>(GameObject concurso, string nombre, string instruccion, params (string campo, object valor)[] datos) where T : PruebaDeRonda
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(concurso.transform, false);
        var ronda = go.AddComponent<T>();
        var todos = new List<(string, object)> { ("claveDeInstruccion", instruccion), ("textoDeInstruccion", "") };
        todos.AddRange(datos);
        Fijar(ronda, todos.ToArray());
        return ronda;
    }

    /// <summary>
    /// Punto con el botón de interactuar (modo Custom): zona en la capa Interactable e icono de la A.
    /// Con <paramref name="enEsteObjeto"/>, el Interactable va en <paramref name="padre"/>; si no, en un hijo.
    /// </summary>
    private static Interactable PuntoDeInteraccion(Transform padre, Vector3 local, float radio, float alturaDelIcono, bool enEsteObjeto = false)
    {
        var go = enEsteObjeto ? padre.gameObject : new GameObject("Interacción");
        if (!enEsteObjeto)
        {
            go.transform.SetParent(padre, false);
            go.transform.localPosition = local;
        }
        var zona = new GameObject("Zona");
        zona.transform.SetParent(go.transform, false);
        zona.transform.localPosition = Vector3.up;
        int capa = LayerMask.NameToLayer("Interactable");
        if (capa >= 0) zona.layer = capa;
        var esfera = zona.AddComponent<SphereCollider>();
        esfera.isTrigger = true;
        esfera.radius = radio;
        var interactuable = go.AddComponent<Interactable>();
        interactuable.SetMode(InteractableMode.Custom);
        MontadorDeBarco.CrearHint(go.transform, Vector3.up * alturaDelIcono, interactuable);
        return interactuable;
    }

    /// <summary>Un prefab del pack como atrezo: sin estático, a su tamaño y con la base en <paramref name="pos"/>.</summary>
    private static GameObject Atrezo(Transform padre, string ruta, string nombre, Vector3 pos, Quaternion rot, float tamano, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null) { informe.AppendLine($"Atrezo: no encuentro {ruta} ({nombre})."); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.gameObject.scene);
        go.name = nombre;
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(pos, rot);
        QuitarEstatico(go);
        EscalarATamano(go, tamano);
        go.transform.position += Vector3.up * (pos.y - BoundsDe(go).min.y);
        return go;
    }

    /// <summary>El actor del reparto con ese id (los objetos se llaman «Papel (Actor_X)»).</summary>
    private static Transform Actor(Transform reparto, string actorId)
    {
        if (reparto == null) return null;
        foreach (Transform hijo in reparto)
            if (hijo.name.EndsWith("(" + actorId + ")")) return hijo;
        return null;
    }

    private static ConcursoPorRondas.Paso DiceElGrupo(string quien, string clave) => new ConcursoPorRondas.Paso { clave = clave, hablanteConNombre = quien };

    private static List<ConcursoPorRondas.Paso> L(params ConcursoPorRondas.Paso[] pasos) => new List<ConcursoPorRondas.Paso>(pasos);

    private static List<ReaccionSegunQuienLoPrueba.Frase> Frases(params (Slot hablante, string clave)[] frases)
    {
        var lista = new List<ReaccionSegunQuienLoPrueba.Frase>();
        foreach (var (hablante, clave) in frases) lista.Add(new ReaccionSegunQuienLoPrueba.Frase { hablante = hablante, clave = clave });
        return lista;
    }

    private static T Cargar<T>(string ruta) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (asset == null) Debug.LogWarning($"[ChuchelandiaBuilder] No encuentro {ruta}.");
        return asset;
    }

    private static string RutaDe(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    /// <summary>
    /// Pone un campo de un componente por su nombre, también listas de clases serializables (rondas,
    /// reacciones…), que <see cref="Fijar"/> no sabe escribir. Se guarda con la escena.
    /// </summary>
    private static void Asignar(Object objeto, string campo, object valor)
    {
        FieldInfo f = null;
        for (var tipo = objeto.GetType(); tipo != null && f == null; tipo = tipo.BaseType)
            f = tipo.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) { Debug.LogWarning($"[ChuchelandiaBuilder] {objeto.GetType().Name} no tiene el campo «{campo}»."); return; }
        f.SetValue(objeto, valor);
        EditorUtility.SetDirty(objeto);
    }
}
