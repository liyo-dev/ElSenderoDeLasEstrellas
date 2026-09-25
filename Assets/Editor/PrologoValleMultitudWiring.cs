#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Prepara la escena del prólogo para el storyboard de tres actos (19 sep 2026): la multitud, el
/// camino por el que baja el Mago Oscuro, los destinos de la evacuación, el escudo y la puerta del
/// Sendero.
///
/// ── Por qué es un script de Editor y no YAML escrito por fuera ────────────────────────────────
/// Es la regla que dejó INC-249: un `.asset` o una escena que Unity puede tener cargada se modifica
/// con el AssetDatabase desde el Editor, nunca escribiendo su YAML a mano, porque la copia que
/// Unity tiene en memoria manda sobre el archivo. Mismo patrón que SequenceInputActionWiring y
/// PrologoSceneNodesWiring.
///
/// Y hay una segunda razón, que se aprendió a base de intentos: **aquí dentro se puede preguntar**.
/// Las alturas del terreno se sacan con un raycast y las posiciones de la gente se pegan al NavMesh
/// con NavMesh.SamplePosition. Desde fuera del Editor eso solo se puede adivinar, y adivinar
/// coordenadas es un bucle sin final.
///
/// Es idempotente y CONVERGE: lo que ya existe no se duplica, pero sí se recoloca a donde dice esta
/// tabla. Se puede ejecutar las veces que haga falta.
public static class PrologoValleMultitudWiring
{
    private const string EscenaPrologo = "Prologo_Valle";
    private const string RutaRoster = "Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset";
    private const string RutaTownNpcs = "Assets/_NPCs/NonInteractable/TownNpc#{0}.prefab";

    private const string RutaEscudo =
        "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield blue.prefab";
    private const string RutaPuerta =
        "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal blue.prefab";

    /// Centro de la plaza. Es a donde miran los aldeanos y de donde salen las distancias.
    private static readonly Vector3 CentroPlaza = new(6002f, 100f, 6001f);

    /// Radios que se prueban al pegar un puesto al suelo caminable, de menor a mayor: un punto que
    /// ya está bien no se mueve, y uno que se salió se acerca lo justo. El primero es más estrecho
    /// que los 3 m que exige NpcSpawner, así que cualquier punto que pase por aquí pasa también su
    /// comprobación.
    private static readonly float[] RadiosDeBusqueda = { 1f, 2.5f, 5f, 9f };

    /// Dónde se planta cada aldeano en la plaza. Repartidos de forma irregular a propósito: una
    /// cuadrícula se lee como gente clonada. Son una PETICIÓN, no una orden — cada una se pega al
    /// suelo caminable más cercano.
    private static readonly Vector3[] PuestosAldeanos =
    {
        new(5997.5f, 100f, 6004.5f),
        new(5999.0f, 100f, 5997.0f),
        new(6003.5f, 100f, 6005.8f),
        new(6005.0f, 100f, 5996.5f),
        new(5993.0f, 100f, 6000.5f),
        new(6008.5f, 100f, 6003.0f),
        new(5994.5f, 100f, 6005.0f),
        new(6000.5f, 100f, 6005.5f),
        new(6009.5f, 100f, 5998.0f),
        new(5991.5f, 100f, 6004.0f),
    };

    /// A dónde corre cada aldeano cuando el Archimago manda evacuar: al puente, al ESTE del valle,
    /// que es el lado contrario por el que baja el Mago Oscuro. Que la gente huya alejándose de él
    /// no es un detalle: es lo que hace legible hacia dónde está el peligro.
    private static readonly Vector3[] PuestosPuente =
    {
        new(6026.0f, 100f, 6002.5f),
        new(6029.5f, 100f, 5999.0f),
        new(6031.0f, 100f, 6004.5f),
        new(6025.0f, 100f, 5996.0f),
        new(6033.0f, 100f, 6000.5f),
        new(6027.0f, 100f, 6007.0f),
        new(6034.5f, 100f, 5997.0f),
        new(6030.5f, 100f, 6002.0f),
        new(6024.0f, 100f, 6005.0f),
        new(6032.0f, 100f, 5993.5f),
    };

    /// El camino por el que baja el Mago Oscuro, de la cresta a la plaza. Va por z ≈ 6000, es decir
    /// de oeste a este: como el eje de acción de la secuencia está fijado a 90° (la cámara al este),
    /// baja DE FRENTE A LA CÁMARA. Es la viñeta 6 del storyboard.
    ///
    /// Solo importan la X y la Z: la altura la calcula el raycast, y WalkPathBeat se vuelve a pegar
    /// al suelo en cada frame mientras anda.
    private static readonly (string nombre, Vector3 pos)[] CaminoDelMago =
    {
        ("M_Cresta",        new Vector3(5925f, 120f, 6000f)),
        ("M_Ladera_01",     new Vector3(5948f, 110f, 6000f)),
        ("M_Ladera_02",     new Vector3(5968f, 104f, 6000f)),
        ("M_Entrada_Villa", new Vector3(5988f, 100f, 6000.5f)),
    };

    /// Dónde se planta cada uno en el duelo. Separados en X, con el Mago Oscuro al oeste (por donde
    /// ha venido) y el Archimago entre él y el pueblo. El eje de acción del duelo se fija aparte a
    /// 0° para que la cámara quede perpendicular a esa línea y los veamos de perfil, encarados —
    /// que es la viñeta 10.
    private static readonly (string nombre, Vector3 pos)[] MarcasDuelo =
    {
        // 6 m, no 8: por encima de 7 ShotComposer descarta el two-shot y cae a un plano medio de
        // uno solo, que es lo que salio en la grabacion del 19 sep.
        //
        // MOVIDAS AL OESTE el 19 sep 2026. Antes el duelo ocurria en x 5997/6003, que es
        // EXACTAMENTE donde estan plantados los diez aldeanos (x 5991..6009): cualquier vecino que
        // no llegara a completar su huida se quedaba literalmente entre los dos duelistas, y eso es
        // lo que se ve en la grabacion ("todo el mundo esta en medio"). Ahora el duelo pasa en la
        // entrada de la villa, por donde ha bajado el Mago Oscuro, y la gente huye al puente 30 m
        // al este: aunque alguno se quede atras, no puede colarse en el plano.
        ("M_Duelo2_Oscuro", new Vector3(5989f, 100f, 6000.5f)),
        ("M_Duelo2_Mago",   new Vector3(5995f, 100f, 6000.5f)),

        // El punto medio exacto entre los dos: es donde chocan los dos haces en el tercer asalto.
        // Se registra como marca para que el VfxBeat pueda poner ahi la explosion sin colgarla de
        // ningun actor -- si se cuelga de uno, el choque parece que lo gana el otro.
        ("M_Duelo_Choque",  new Vector3(5992f, 100f, 6000.5f)),

        // Donde cae el Archimago cuando le rompen la guardia, dos metros detras de su sitio: el
        // retroceso se lee como retroceso, no como un tropiezo.
        ("M_Duelo_Caida",   new Vector3(5997.5f, 100f, 6000.5f)),
    };

    // ── Pase del 20 sep 2026 ──────────────────────────────────────────────────────────────────
    //
    // Tres cosas cambian de sitio en este pase, y las tres por la misma razón: lo que se cuenta
    // tiene que CABER en el plano.
    //
    //  1. La conversación de Liora y el Archimago se va a la orilla del río. Estaba en la plaza,
    //     entre diez vecinos, y en la séptima grabación no se ve: la lente acaba detrás del pelo
    //     de alguien o contra el faldón de una casa.
    //  2. El duelo se acerca al ESTE. Estaba en x 5989/5995, con el puente treinta metros a la
    //     espalda de la cámara: la frase «todavía están cruzando» no tenía a qué referirse. Ahora
    //     ocurre en el camino que lleva al puente, mirando hacia él, y la gente cruzando sale al
    //     fondo del plano — sin cortar a ningún otro sitio.
    //  3. Los vecinos dejan de teletransportarse a la otra orilla. Hay marcas de entrada y de
    //     salida del puente, así que cruzan por el puente, en fila, y se les ve.

    /// La orilla oeste del río, donde se sientan a hablar. El río va de norte a sur por x ≈ 6023 y
    /// el puente está en z ≈ 5998,6: puestos aquí, con la cámara al oeste, el agua y el puente
    /// quedan detrás de ellos. Es el único sitio del valle con un fondo que no es una pared.
    private static readonly (string nombre, Vector3 pos)[] MarcasRio =
    {
        ("M_Rio_Camino",    new Vector3(6014.5f, 100f, 6001.5f)),

        // Donde llegan (al norte) y donde acaban (al sur, a la altura del puente). Entre las dos
        // hay seis metros de orilla: la conversacion se da ANDANDO, que es como habla la gente que
        // se conoce. Y andando hacia el sur van hacia el puente, que es a donde va a ir todo el
        // mundo veinte minutos despues.
        //
        // A x 6017 y no pegados al agua: la ribera de 6018-6019 esta sembrada de rocas y juncos
        // (Ribera_Roca_03, _04, Ribera_Junco_02) y cualquiera de ellos se le mete a la camara en
        // la lente.
        // Hombro con hombro, no uno detras de otro: la separacion entre ellos va en X y el paseo
        // va en Z. Asi la linea que los une es perpendicular a la marcha, y una camara puesta al
        // sur les ve VENIR a los dos de frente mientras hablan.
        ("M_Rio_Mago",      new Vector3(6017.6f, 100f, 6003.8f)),
        ("M_Rio_Liora",     new Vector3(6016.4f, 100f, 6003.4f)),
        ("M_Rio_Fin_Mago",  new Vector3(6017.8f, 100f, 5997.4f)),
        ("M_Rio_Fin_Liora", new Vector3(6016.6f, 100f, 5997.0f)),
    };

    /// El duelo, en el camino del puente (z ≈ 5997,5).
    ///
    /// Esa Z no es arbitraria: es el único pasillo limpio que queda entre la plaza y el río. Un
    /// metro al norte está el pozo (5999,2 / 6001,4), dos más allá las vigas caídas (6005,6 y
    /// 6007,4) y la carreta (6009,8 / 5999,2). Por aquí la línea de cámara llega desde el oeste
    /// hasta el puente sin tocar nada.
    private static readonly (string nombre, Vector3 pos)[] MarcasDuelo3 =
    {
        ("M_Duelo3_Oscuro", new Vector3(6000.0f, 100f, 5997.5f)),
        ("M_Duelo3_Mago",   new Vector3(6006.0f, 100f, 5997.5f)),
        ("M_Duelo3_Choque", new Vector3(6003.0f, 100f, 5997.5f)),
        ("M_Duelo3_Caida",  new Vector3(6008.5f, 100f, 5997.8f)),
    };

    /// Dónde se junta cada vecino antes de entrar al puente. Escalonadas en X a propósito: diez
    /// personas que salen a la vez desde el mismo sitio se leen como un pelotón, y diez que salen
    /// desde distintas distancias se leen como un pueblo huyendo.
    private static readonly Vector3[] PuestosHuida =
    {
        new(6012.0f, 100f, 5999.0f),
        new(6014.5f, 100f, 5996.5f),
        new(6011.0f, 100f, 6001.5f),
        new(6016.0f, 100f, 5999.5f),
        new(6013.0f, 100f, 5995.0f),
        new(6015.5f, 100f, 6002.5f),
        new(6010.0f, 100f, 5997.0f),
        new(6017.0f, 100f, 5997.0f),
        new(6012.5f, 100f, 6003.5f),
        new(6014.0f, 100f, 6001.0f),
    };

    /// A donde siguen DESPUES de cruzar. Los destinos del puente estan a la vista desde la plaza,
    /// y un vecino que llega y se queda de pie mirando al infinito se lee como un error -- es lo
    /// que dice Raul de la octava grabacion: "corre un poco y se quedan ahi idle, queda super
    /// feo". Estos estan lo bastante lejos para que, cuando lleguen, ya no importe.
    private static readonly Vector3[] PuestosLejos =
    {
        new(6039.0f, 100f, 6001.0f),
        new(6043.0f, 100f, 5996.0f),
        new(6041.0f, 100f, 6006.0f),
        new(6046.0f, 100f, 5999.0f),
        new(6038.0f, 100f, 5993.0f),
        new(6044.0f, 100f, 6003.5f),
        new(6048.0f, 100f, 5997.5f),
        new(6040.0f, 100f, 5990.5f),
        new(6045.0f, 100f, 6008.0f),
        new(6042.0f, 100f, 6000.0f),
    };

    /// Entrada y salida del puente. Van en la Z exacta del puente (5998,64) para que la fila lo
    /// cruce por encima en vez de vadear el río — WalkPathBeat se pega al suelo en cada frame, y
    /// el suelo que encuentra ahí es el tablero.
    private static readonly (string nombre, Vector3 pos)[] MarcasPuente =
    {
        ("M_Puente_Ent", new Vector3(6018.5f, 100f, 5998.64f)),
        ("M_Puente_Sal", new Vector3(6027.5f, 100f, 5998.64f)),
    };

    /// La entrada y la salida del puente, sacadas del PUENTE DE VERDAD y no de una tabla.
    ///
    /// «La gente no cruza por el puente.» La tabla de arriba tenía la Z del tablero apuntada a
    /// mano (5998,64), y el puente de la escena tiene su tablero en otra: el objeto `Puente` está
    /// en z=6000,39. Con 1,75 m de diferencia la fila iba por el BORDE del puente o directamente
    /// por el agua de al lado, que es lo que se ve en la grabación 11 (2:54).
    ///
    /// Así que se miden los Renderer del puente y se trazan las dos marcas sobre su eje largo: la
    /// entrada un poco ANTES del tablero, en tierra y ya alineada con él, para que nadie entre
    /// sesgado desde un lado y pise la orilla; y la salida un poco DESPUÉS, en la otra orilla. Si
    /// el puente se mueve, las marcas se mueven con él en el siguiente PREPARAR TODO.
    private const float AntesDelTablero = 1.2f;
    private const float DespuesDelTablero = 1.0f;

    private static (string nombre, Vector3 pos)[] MarcasDelPuente(Scene escena)
    {
        var puente = BuscarEnEscena(escena, "Puente");
        if (puente == null)
        {
            Debug.LogWarning("[PrologoValle] No encuentro 'Puente'; uso las marcas de la tabla.");
            return MarcasPuente;
        }

        bool hay = false;
        Bounds caja = default;
        foreach (var r in puente.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            if (!hay) { caja = r.bounds; hay = true; }
            else caja.Encapsulate(r.bounds);
        }
        if (!hay) return MarcasPuente;

        // El eje largo es el lado mayor de la caja en planta.
        bool largoEnX = caja.size.x >= caja.size.z;
        Vector3 eje = largoEnX ? Vector3.right : Vector3.forward;
        float mitad = (largoEnX ? caja.size.x : caja.size.z) * 0.5f;

        Vector3 centro = caja.center;
        Vector3 finMenos = centro - eje * mitad;
        Vector3 finMas = centro + eje * mitad;

        // La entrada es el extremo del lado del pueblo: el más cercano a la plaza.
        Vector3 plaza = new Vector3(6010f, 100f, 6000f);
        bool menosEsElPueblo = (finMenos - plaza).sqrMagnitude <= (finMas - plaza).sqrMagnitude;
        Vector3 finPueblo = menosEsElPueblo ? finMenos : finMas;
        Vector3 finOtraOrilla = menosEsElPueblo ? finMas : finMenos;

        Vector3 haciaFueraPueblo = (finPueblo - centro).normalized;
        Vector3 haciaFueraOtra = (finOtraOrilla - centro).normalized;
        Vector3 entrada = finPueblo + haciaFueraPueblo * AntesDelTablero;
        Vector3 salida = finOtraOrilla + haciaFueraOtra * DespuesDelTablero;

        Debug.Log($"[PrologoValle] Puente medido: eje {(largoEnX ? "X" : "Z")}, " +
            $"{mitad * 2f:F1} m de largo, centro del tablero en ({centro.x:F2}, {centro.z:F2}). " +
            $"Entrada en ({entrada.x:F2}, {entrada.z:F2}), salida en ({salida.x:F2}, {salida.z:F2}).");

        return new[] { ("M_Puente_Ent", entrada), ("M_Puente_Sal", salida) };
    }

    /// Las marcas del final, las únicas de toda la escena que NO se pegan al suelo: son puntos en
    /// el aire.
    ///
    /// Cuidado con la geometría: WalkPathBeat da un tramo por terminado cuando llega en
    /// HORIZONTAL, así que un vuelo recto hacia arriba termina en el primer frame sin subir nada.
    /// Por eso cada punto de aire está más lejos en horizontal que en vertical.
    private static readonly (string nombre, Vector3 pos)[] MarcasAire =
    {
        // El Mago Oscuro se eleva echándose atrás: 7 m al oeste, 6,5 de alto.
        ("M_Aire_Oscuro",      new Vector3(5993.0f, 106.5f, 5997.5f)),

        // La carrera del Archimago antes del salto. Arranca desde donde cae en el segundo asalto
        // (M_Duelo3_Caida, x 6008,5), asi que son seis metros y medio: a 4,5 m/s, segundo y medio
        // de carrera de verdad. Con los dos metros que habia antes no daba tiempo ni a que el
        // animator saliera del idle.
        ("M_Carrera_Mago",     new Vector3(6002.0f, 100.0f, 5997.5f)),

        // Y el salto: 3,5 m de avance por 2,5 de alto, hacia el oeste -- o sea hacia DEBAJO del
        // Mago Oscuro, que para entonces esta en M_Aire_Oscuro_3 (x 5995,5, ocho metros de alto).
        ("M_Salto_Mago_Aire",  new Vector3(5998.5f, 102.5f, 5997.5f)),

        // -- El combate aereo (20 sep, segunda pasada) --------------------------------------
        //
        // Cuatro puntos mas para que el final deje de ser "los dos quietos tirandose cosas". La
        // regla de siempre: cada tramo, MAS LEJOS EN HORIZONTAL QUE EN VERTICAL, o WalkPathBeat lo
        // da por terminado sin haber subido.
        //
        // El se mueve por el aire (no se queda flotando en el mismo sitio como un globo), se tira
        // en picado una vez, y vuelve a subir por el otro lado.
        ("M_Aire_Oscuro_2",    new Vector3(5996.0f, 107.5f, 6002.5f)),   // cruza a su izquierda
        ("M_Picado_Oscuro",    new Vector3(6004.5f, 100.8f, 5997.5f)),   // el picado, casi al suelo
        ("M_Aire_Oscuro_3",    new Vector3(5995.5f, 108.0f, 5997.5f)),   // y arriba otra vez

        // El salto de esquiva del Archimago: corto, lateral, para quitarse de en medio.
        ("M_Aire_Mago_1",      new Vector3(6009.5f, 102.2f, 5995.5f)),
    };

    [MenuItem("El Sendero/Archivo/Secuencias/Prólogo: preparar la escena del storyboard")]
    public static void Ejecutar()
    {
        Scene escena = SceneManager.GetSceneByName(EscenaPrologo);
        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogError($"[PrologoValle] La escena '{EscenaPrologo}' no está abierta. Ábrela " +
                "(sola o en aditivo) y vuelve a ejecutar esto.");
            return;
        }

        var stage = BuscarStage(escena);
        if (stage == null)
        {
            Debug.LogError("[PrologoValle] No hay ningún SequenceStage en la escena.");
            return;
        }

        int puntos = CrearPuntosDeAparicion(escena);
        int marcas = CrearMarcas(escena, stage);
        int luces = AjustarLucesDelIncendio(escena);
        int suelo = ArreglarElSuelo(escena);
        int estorbos = DarColliderALoQueEstorba(escena);
        AjustarDistanciaDeCamara(stage);
        int props = CrearProps(escena, stage);
        int roster = RellenarRoster();

        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();

        int estaticos = DesestatizarLosProps(escena, stage);
        int piezas = JuntarLasPiezasSueltas(escena);

        Debug.Log($"[PrologoValle] Hecho. {puntos} punto(s) de aparición, {marcas} marca(s), " +
            $"{luces} luz/luces del incendio, {suelo} arreglo(s) de suelo, {props} prop(s), " +
            $"{estorbos} collider(s) para la cámara, " +
            $"{estaticos} objeto(s) desestatizado(s), {piezas} pieza(s) recolocada(s) y " +
            $"{roster} entrada(s) de roster. " +
            "Guarda la escena con Ctrl+S.");
    }

    // ── Puntos de aparición ───────────────────────────────────────────────────────────────────

    private static int CrearPuntosDeAparicion(Scene escena)
    {
        int tocados = 0;

        for (int i = 0; i < PuestosAldeanos.Length; i++)
        {
            string nombre = $"SpawnPoint_NPC_Aldeano_{i + 1:00}";
            Vector3 destino = AjustarAlNavMesh(PuestosAldeanos[i], nombre);

            var existente = BuscarEnEscena(escena, nombre);
            GameObject go;

            if (existente != null)
            {
                go = existente;
                if ((go.transform.position - destino).sqrMagnitude > 0.0001f)
                {
                    Undo.RecordObject(go.transform, "Recolocar punto de aparición");
                    go.transform.position = destino;
                    tocados++;
                }
            }
            else
            {
                go = new GameObject(nombre);
                Undo.RegisterCreatedObjectUndo(go, "Crear punto de aparición");
                SceneManager.MoveGameObjectToScene(go, escena);
                go.transform.position = destino;
                tocados++;
            }

            // Mirando al centro de la plaza: un corro de gente vuelta hacia dentro se lee como un
            // pueblo; diez personas mirando todas al norte se leen como un error.
            Vector3 haciaElCentro = CentroPlaza - destino;
            haciaElCentro.y = 0f;
            if (haciaElCentro.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(haciaElCentro.normalized, Vector3.up);

            var punto = go.GetComponent<NpcSpawnPoint>();
            if (punto == null) punto = go.AddComponent<NpcSpawnPoint>();

            var so = new SerializedObject(punto);
            so.FindProperty("spawnId").stringValue = $"SPAWN_NPC_Aldeano_{i + 1:00}";
            var aplicarRotacion = so.FindProperty("applyRotation");
            if (aplicarRotacion != null) aplicarRotacion.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return tocados;
    }

    // ── Distancia de camara ───────────────────────────────────────────────────────────────────

    /// ShotComposer multiplica TODAS las distancias que calcula por SequenceStage.DistanceMultiplier.
    /// Estaba a 1, que es la distancia de libro para un plano de dialogo visto en un monitor de
    /// escritorio -- y en la grabacion del 19 sep se lee como "los planos muy cerca": el bocadillo
    /// tapa la cara, no se ve donde esta nadie, y el duelo se convierte en dos primeros planos
    /// alternos sin espacio para ningun hechizo. 1,35 deja aire por arriba para el bocadillo y sitio
    /// alrededor para que se vea VOLAR lo que se lanzan.
    private const float DistanciaDeCamara = 1.35f;

    private static void AjustarDistanciaDeCamara(SequenceStage stage)
    {
        var so = new SerializedObject(stage);
        var prop = so.FindProperty("_distanceMultiplier");
        if (prop == null)
        {
            Debug.LogWarning("[PrologoValle] El SequenceStage no tiene '_distanceMultiplier'.");
            return;
        }

        if (Mathf.Approximately(prop.floatValue, DistanciaDeCamara)) return;

        Debug.Log($"[PrologoValle] Distancia de camara: {prop.floatValue:F2} -> {DistanciaDeCamara:F2}.");
        prop.floatValue = DistanciaDeCamara;
        so.ApplyModifiedProperties();
    }

    // ── Marcas ────────────────────────────────────────────────────────────────────────────────

    private static int CrearMarcas(Scene escena, SequenceStage stage)
    {
        var deseadas = new List<(string nombre, Vector3 pos, bool pegarAlNavMesh)>
        {
            // Liora en la plaza, entre la gente, no en la mesa del desayuno.
            ("M_Plaza_Liora", new Vector3(6004.5f, 100f, 6003.2f), true),

            // Donde para el Archimago a mirar el corro que celebra. Queda entre los aldeanos
            // 01, 03 y 08, que son los que vitorean, aplauden y se rien.
            ("M_Baile", new Vector3(6002.5f, 100f, 6004.2f), true),
        };

        // El camino de bajada: al suelo, pero NO al NavMesh — la ladera no está bakeada y no tiene
        // por qué estarlo. De eso se encarga WalkPathBeat, que no usa el agente.
        foreach (var (nombre, pos) in CaminoDelMago) deseadas.Add((nombre, pos, false));

        foreach (var (nombre, pos) in MarcasDuelo) deseadas.Add((nombre, pos, true));

        for (int i = 0; i < PuestosAldeanos.Length; i++)
            deseadas.Add(($"M_Aldeano_{i + 1:00}", PuestosAldeanos[i], true));

        for (int i = 0; i < PuestosPuente.Length; i++)
            deseadas.Add(($"M_Puente_{i + 1:00}", PuestosPuente[i], true));

        for (int i = 0; i < PuestosHuida.Length; i++)
            deseadas.Add(($"M_Huida_{i + 1:00}", PuestosHuida[i], true));

        for (int i = 0; i < PuestosLejos.Length; i++)
            deseadas.Add(($"M_Lejos_{i + 1:00}", PuestosLejos[i], true));

        foreach (var (nombre, pos) in MarcasRio) deseadas.Add((nombre, pos, true));
        foreach (var (nombre, pos) in MarcasDuelo3) deseadas.Add((nombre, pos, true));

        // Las del puente al suelo pero NO al NavMesh: el NavMesh puede no llegar al tablero, y
        // quien las usa es WalkPathBeat, que no necesita agente.
        foreach (var (nombre, pos) in MarcasDelPuente(escena)) deseadas.Add((nombre, pos, false));

        var so = new SerializedObject(stage);
        var marcas = so.FindProperty("_marks");
        if (marcas == null)
        {
            Debug.LogError("[PrologoValle] El SequenceStage no tiene '_marks'.");
            return 0;
        }

        var yaDadasDeAlta = new Dictionary<string, int>();
        for (int i = 0; i < marcas.arraySize; i++)
        {
            string n = marcas.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            if (!string.IsNullOrWhiteSpace(n)) yaDadasDeAlta[n.Trim()] = i;
        }

        int tocadas = 0;

        // Las del aire van al final y con su posicion EXACTA: ni NavMesh ni raycast al suelo.
        // Se marcan con un nombre reservado para que el bucle de abajo no las toque.
        var enElAire = new HashSet<string>();
        foreach (var (nombre, pos) in MarcasAire)
        {
            enElAire.Add(nombre);
            deseadas.Add((nombre, pos, false));
        }

        foreach (var (nombre, pedido, pegarAlNavMesh) in deseadas)
        {
            Vector3 destino = enElAire.Contains(nombre)
                ? pedido
                : pegarAlNavMesh
                    ? AjustarAlNavMesh(pedido, nombre)
                    : PegarAlSuelo(pedido, nombre);

            var go = BuscarEnEscena(escena, nombre);

            if (go == null)
            {
                go = new GameObject(nombre);
                Undo.RegisterCreatedObjectUndo(go, "Crear marca");
                SceneManager.MoveGameObjectToScene(go, escena);
                go.transform.SetParent(stage.transform, worldPositionStays: false);
                tocadas++;
            }
            else if ((go.transform.position - destino).sqrMagnitude > 0.0001f)
            {
                Undo.RecordObject(go.transform, "Recolocar marca");
                tocadas++;
            }

            go.transform.position = destino;

            if (yaDadasDeAlta.ContainsKey(nombre)) continue;

            marcas.arraySize++;
            var entrada = marcas.GetArrayElementAtIndex(marcas.arraySize - 1);
            entrada.FindPropertyRelative("name").stringValue = nombre;
            entrada.FindPropertyRelative("target").objectReferenceValue = go.transform;
        }

        so.ApplyModifiedProperties();
        return tocadas;
    }

    // ── Las luces del incendio ────────────────────────────────────────────────────────────────

    /// Baja las ocho luces del incendio.
    ///
    /// En la séptima grabación, a partir del minuto 1:40 el camino de la plaza sale amarillo puro
    /// y las casas rosa fosforito. No es el atardecer ni la niebla: son estas ocho luces.
    ///
    /// Cada una estaba a **intensidad 2 con 11 m de alcance**, y están repartidas por una plaza de
    /// veinte metros — así que en el centro se solapan tres o cuatro. Sumadas dan intensidad 6 u 8
    /// sobre superficies que ya son claras (el camino es arena, las casas son madera clara), y ahí
    /// los canales saturan: el naranja del fuego (1 / 0,62 / 0,28) sobre arena clara satura primero
    /// el rojo y luego el verde, y lo que queda es amarillo plano sin textura.
    ///
    /// Con 0,8 y 6,5 m cada foco ilumina **su** casa y deja el resto de la plaza al atardecer, que
    /// es lo que hace que un incendio se lea como un incendio: por el contraste, no por el brillo.
    private const float IntensidadFuego = 0.8f;
    private const float AlcanceFuego = 6.5f;

    private static int AjustarLucesDelIncendio(Scene escena)
    {
        int tocadas = 0;

        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var luz in raiz.GetComponentsInChildren<Light>(includeInactive: true))
            {
                if (!luz.gameObject.name.StartsWith("Fuego_")) continue;
                if (Mathf.Approximately(luz.intensity, IntensidadFuego)
                    && Mathf.Approximately(luz.range, AlcanceFuego)) continue;

                Undo.RecordObject(luz, "Bajar la luz del incendio");
                luz.intensity = IntensidadFuego;
                luz.range = AlcanceFuego;
                EditorUtility.SetDirty(luz);
                tocadas++;
            }
        }

        // `Luz_Solo_Para_Editar` se queda ENCENDIDA, y aquí nos aseguramos de que lo esté.
        //
        // La apagué en el pase anterior razonando que «suma un sol entero encima del sol de
        // verdad». Era falso, y bastaba con contar las luces de la escena para verlo: en
        // `Prologo_Valle` NO HAY OTRA DIRECCIONAL. Solo están esta y las ocho de tipo `Fuego_`,
        // que son puntuales y de alcance corto. No hay `DayNightCycle` en la escena (el ciclo
        // referencia una Light con `[SerializeField]`, no la crea) ni se carga ninguna escena en
        // aditivo que traiga un sol. Así que esta direccional ES el sol del valle: apagarla dejó
        // los árboles, las casas y las rocas en negro, y solo el suelo y el río parecían bien
        // porque Quibli StylizedLit añade `_LightContribution` como base constante, que no
        // depende de la luz de la escena.
        //
        // El nombre engaña —parece una luz de trabajo provisional— y actué sobre el nombre en vez
        // de sobre el inventario de luces. El apagón de la sobreexposición eran las ocho de fuego
        // de arriba, que iban a intensidad 2 y alcance 11 solapándose; eso ya está arreglado.
        var sol = BuscarEnEscena(escena, "Luz_Solo_Para_Editar");
        if (sol != null && !sol.activeSelf)
        {
            Undo.RecordObject(sol, "Encender el sol del valle");
            sol.SetActive(true);
            EditorUtility.SetDirty(sol);
            tocadas++;
            Debug.Log("[PrologoValle] 'Luz_Solo_Para_Editar' vuelve a estar ENCENDIDA: es la única " +
                "direccional de la escena, o sea el sol del valle. Sin ella todo lo que no es " +
                "Quibli se ve negro.");
        }

        return tocadas;
    }

    // ── El suelo ──────────────────────────────────────────────────────────────────────────────

    /// El shader del suelo del valle.
    ///
    /// Raúl, novena grabación: *«el suelo es como blanco y hay un círculo que es el suelo de verdad
    /// que se va moviendo. ¿Qué shader estás usando? Debería ser el de Quibli»*. Tenía razón en las
    /// dos mitades de la frase.
    ///
    /// `Mat_Valle_Pradera` está sobre `Plugins/CiroContinisio/ToonShader/Toon.shadergraph`, que no
    /// lo usa ninguna otra superficie grande del juego. Y el objeto sobre el que va es el **Plane
    /// de Unity** —diez por diez quads— escalado ×26, o sea **260 m de lado con un vértice cada 26
    /// metros**. Cualquier término que ese shader resuelva por vértice se interpola a lo bestia, y
    /// lo que se ve es una mancha clara enorme con una frontera curva que parece seguir a la
    /// cámara.
    ///
    /// Quibli ya está en el proyecto (`Plugins/Quibli/Shaders/StylizedLit.shader`) y es lo que usa
    /// el resto del arte estilizado, así que el suelo se pasa ahí con el mismo verde que tenía.
    ///
    /// Por si el artefacto sobreviviera al cambio —que sería la prueba de que no era el shader—
    /// esto además **imprime qué shader usa cada superficie grande de la escena**. Con eso, una
    /// sola ejecución cierra el diagnóstico en vez de abrir otra ronda de hipótesis.
    private const string RutaShaderQuibli = "Assets/Plugins/Quibli/Shaders/StylizedLit.shader";
    private const string RutaMatSuelo = "Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Pradera_Quibli.mat";
    private const string RutaMatOriginal = "Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Pradera.mat";

    /// El material Quibli del que se COPIA el nuevo.
    ///
    /// ── POR QUÉ EL SUELO SALÍA NEGRO (la causa de verdad, 20 sep) ────────────────────────────
    /// Dos veces culpé a `_LightAttenuation` y a `_ShadowColor`. Las dos veces me equivoqué: el
    /// material de agua del propio Quibli, `Q_Liquid_Mat`, trae exactamente esos mismos valores
    /// —atenuación (0,1,0,0) y sombra negra— y se ve perfectamente. No era eso.
    ///
    /// La causa está en dos líneas del shader. En `StylizedLit.shader`:
    ///
    ///     [MainTexture] _BaseMap("Albedo", 2D) = "black" {}
    ///
    /// El albedo por defecto NO es blanco, es NEGRO. Y en `Lighting_DR.hlsl`:
    ///
    ///     half3 brdf = _LightContribution;      // el color base NO entra aquí
    ///     ...
    ///     color *= lerp(1, albedo.rgb, _TextureImpact);   // albedo = _BaseMap * _BaseColor
    ///
    /// Y en `StylizedInput.hlsl`, línea 71, la pieza que lo cierra todo:
    ///
    ///     half4 albedo = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    ///
    /// Sin `* _BaseColor`. **En este shader el color base NO PINTA NADA.** Está declarado, pero
    /// solo se aliasa a `_SpecColor`, y por eso el shader lo marca `[HideInInspector]`: existe
    /// para que Unity tenga su `[MainColor]`, no para colorear. El color sale ENTERO de la
    /// textura. Es también por lo que `Q_Liquid_Mat` se ve gris teniendo el `_BaseColor` amarillo.
    ///
    /// Así que: mapa negro (el defecto) → suelo negro; mapa blanco → suelo gris; `_TextureImpact`
    /// a 0 → `color *= 1`, gris otra vez. Las tres cosas que probé, y las tres por lo mismo.
    ///
    /// Un color plano en Quibli es **un PNG de 4×4 en ese color** metido en `_BaseMap`, con
    /// `_TextureImpact` a 1. Lo genera `TexturaPlana(color)` aquí abajo, uno por color.
    ///
    /// El proyecto va en Linear, así que el PNG se escribe en gamma (`color.gamma`) y el importador
    /// marcado como sRGB lo devuelve a linear al muestrear: el color que entra es el que sale.
    /// La alfa también va en la textura — `surfaceData.alpha = albedo.a`, no `_BaseColor.a`.
    ///
    /// Se sigue copiando la plantilla y no creando con `new Material(shader)`, porque la plantilla
    /// trae calibrados la rampa, el gradiente y los keywords; pero el arreglo de verdad es el mapa.
    private const string RutaMatPlantilla =
        "Assets/Plugins/Quibli/Demos/Nature/Materials/NatureScene_Ground.mat";

    /// El verde que ya tenía el valle. Se conserva tal cual para que el cambio de shader no sea
    /// también un cambio de color: si algo se ve distinto, que sea por el shader y solo por él.
    private static readonly Color VerdePradera = new(0.2f, 0.43f, 0.16f, 1f);

    /// El nombre del shader de Quibli, tal cual lo declara `StylizedLit.shader`. Se usa para saber
    /// si un material ya es Quibli o hay que rehacerlo desde la plantilla.
    private const string ShaderQuibli = "Quibli/Stylized Lit";

    /// Donde van los PNG de color plano. Uno por color, con el color en el nombre.
    private const string CarpetaPlanos = "Assets/Art/World/Prologo_Valle/Materials/Planos";

    [MenuItem("El Sendero/Archivo/Secuencias/Prólogo: devolver el suelo a su material original")]
    public static void DevolverElSueloAlOriginal()
    {
        Scene escena = SceneManager.GetSceneByName(EscenaPrologo);
        var suelo = escena.IsValid() ? BuscarEnEscena(escena, "Suelo_Valle") : null;

        if (suelo == null || !suelo.TryGetComponent(out MeshRenderer render))
        {
            Debug.LogError("[PrologoValle] Abre 'Prologo_Valle' primero.");
            return;
        }

        var original = AssetDatabase.LoadAssetAtPath<Material>(RutaMatOriginal);
        if (original == null)
        {
            Debug.LogError($"[PrologoValle] No encuentro '{RutaMatOriginal}'.");
            return;
        }

        Undo.RecordObject(render, "Devolver el material del suelo");
        render.sharedMaterial = original;
        EditorUtility.SetDirty(render);

        var agua = BuscarEnEscena(escena, "Rio_Agua");
        var matAgua = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Agua.mat");
        if (agua != null && matAgua != null && agua.TryGetComponent(out MeshRenderer rAgua))
        {
            Undo.RecordObject(rAgua, "Devolver el material del agua");
            rAgua.sharedMaterial = matAgua;
            EditorUtility.SetDirty(rAgua);
        }

        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log("[PrologoValle] El suelo y el río vuelven a sus materiales originales (el toon " +
            "de Ciro). Guarda la escena. Esto está aquí para no quedarse nunca atascado.");
    }

    /// La plantilla para el agua. Quibli no trae un shader de agua propio, así que se copia el
    /// material de líquido de su escena de ejemplo, que ya es StylizedLit con la transparencia y
    /// la especularidad puestas — que es todo lo que un río estilizado necesita.
    private const string RutaMatPlantillaAgua =
        "Assets/Plugins/Quibli/Demos/Sample Scene with Quibli/Materials/Sample Scene Props Materials/Q_Liquid_Mat.mat";

    private const string RutaMatAgua = "Assets/Art/World/Prologo_Valle/Materials/Mat_Valle_Agua_Quibli.mat";

    /// El azul que ya tenía el río, igual que con el verde: el cambio es de shader, no de color.
    /// La alfa sí baja: la plantilla de líquido es transparente (`_Surface` 1, `_ZWrite` 0) y un
    /// río opaco desperdicia eso. A 0,82 se intuye el fondo sin que el agua deje de leerse.
    private static readonly Color AzulRio = new(0.08f, 0.48f, 0.63f, 0.82f);

    /// Decorado que estorba a la cámara y no aporta nada: se APAGA.
    ///
    /// ── Historia de esta decisión ────────────────────────────────────────────────────────────
    /// `Viga`, en (6006.8, 102.6, 6004.8): una viga a 2,6 m del suelo y a cinco metros del centro
    /// de la plaza. Es el faldón marrón que cruza la pantalla en diagonal desde la quinta
    /// grabación, y en la novena sigue apareciendo en tres planos seguidos (2:56, 3:00, 3:04).
    ///
    /// Primero se le dio un collider, para que el solver de cámara pudiera verla y la esquivara
    /// sola. Funciona, pero el problema de fondo era otro: **esa viga no cuenta nada**. Está en
    /// mitad del sitio donde se resuelven casi todos los planos del duelo y de la despedida, y lo
    /// único que hace es taparlos. Raúl, tras la novena: «la viga y las cosas que se instancian
    /// las quitamos, solo dejamos humo; si eso mete más fuego, más fuego».
    ///
    /// Apagar es mejor que esquivar: la cámara recupera ese espacio entero en vez de tener que
    /// rodearlo. Y es reversible — el objeto sigue en la escena, solo desactivado.
    private static readonly string[] EstorbanALaCamara =
    {
        "Viga",
        // (20 sep) «Quita los troncos que se instancian en mitad del camino cuando aparece el
        // Mago Oscuro; no quiero cambios en el escenario, solo fuego y humo.» Son estos dos: dos
        // `Wood01` que cuelgan de PROP_Incendio y aparecen en mitad de la plaza — 6005,6/6001,4 y
        // 6007,4/6002,6 — justo por donde se rueda el duelo. El incendio se queda solo con sus
        // ocho fuegos y sus cuatro humos, que es lo que se pidió.
        "Viga_Escombro_01",
        "Viga_Escombro_02",
    };

    /// Objetos que tienen que apoyarse en el suelo y no estar medio enterrados.
    ///
    /// No se adivina el offset: se mide. Se mira dónde cae la base real del Renderer y se mueve el
    /// objeto lo que le falte para apoyarse — hacia arriba si está enterrado y hacia abajo si está
    /// flotando, que es la corrección del 20 sep (ver más abajo).
    ///
    /// **La carreta ya NO está en esta lista.** Raúl la ha colocado a mano donde la quiere, y el
    /// montaje la devuelve exactamente a esa pose al posarla (`PropMoveBeat.desdeDondeEstaba`).
    /// Un ajuste automático encima de una colocación a mano solo puede estropearla. Se deja el
    /// mecanismo porque sirve para el siguiente objeto que haga falta, pero vacío no toca nada.
    private static readonly string[] SeApoyanEnElSuelo = { };

    /// ¿Es este renderer una de las piezas que el wiring le cuelga al prop (Carreta_Heno,
    /// Globo_Cuerda...) y no el cuerpo del objeto? Se reconocen por el nombre: el prop se llama
    /// `Carreta` y sus piezas `Carreta_algo`.
    private static bool EsUnaPiezaColgada(Transform t, string nombrePadre)
    {
        string prefijo = nombrePadre + "_";
        while (t != null)
        {
            if (t.name.StartsWith(prefijo, System.StringComparison.Ordinal)) return true;
            if (t.name == nombrePadre) return false;
            t = t.parent;
        }
        return false;
    }

    private static int DarColliderALoQueEstorba(Scene escena)
    {
        int tocados = 0;

        foreach (string nombre in EstorbanALaCamara)
        {
            var go = BuscarEnEscena(escena, nombre);
            if (go == null)
            {
                Debug.LogWarning($"[PrologoValle] No encuentro '{nombre}' para apagarlo.");
                continue;
            }

            // Si en un pase anterior se le colgó un collider para la cámara, ya no hace falta.
            var viejo = go.transform.Find(nombre + "_ColliderParaLaCamara");
            if (viejo != null)
            {
                Undo.DestroyObjectImmediate(viejo.gameObject);
                tocados++;
            }

            if (!go.activeSelf) continue;

            Undo.RecordObject(go, "Apagar decorado que estorba");
            go.SetActive(false);
            EditorUtility.SetDirty(go);
            tocados++;

            Debug.Log($"[PrologoValle] '{nombre}' APAGADO: estaba a {go.transform.position.y - 100f:F1} m " +
                "de altura en mitad de la plaza y se comía los planos del duelo. Sigue en la " +
                "escena, solo desactivado.");
        }

        foreach (string nombre in SeApoyanEnElSuelo)
        {
            var go = BuscarEnEscena(escena, nombre);
            if (go == null)
            {
                Debug.LogWarning($"[PrologoValle] No encuentro '{nombre}' para asentarlo.");
                continue;
            }

            var renders = go.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            if (renders.Length == 0) continue;

            // La base real del CUERPO del objeto.
            //
            // Las piezas sueltas que `JuntarLasPiezasSueltas` le cuelga después (Carreta_Rueda,
            // Carreta_Heno, Carreta_Verdura) NO cuentan aquí, y esa es la corrección del 20 sep:
            // el heno y las verduras están decorativamente medio metidos en la hierba, en su
            // propio trozo de suelo, y el suelo se mide en la columna del PIVOT de la carreta. Si
            // ese suelo está un poco más alto que el de la pieza, la pieza sale "enterrada" en
            // todos los pases, y como esto solo sabía SUBIR, cada PREPARAR TODO levantaba la
            // carreta un poco más. Así acabó a y=101,65 con la plaza a 100: flotando.
            //
            // Con el cuerpo solo, y pudiendo bajar además de subir, converge en un pase.
            float baseReal = float.PositiveInfinity;
            foreach (var r in renders)
            {
                if (r == null) continue;
                if (EsUnaPiezaColgada(r.transform, nombre)) continue;
                baseReal = Mathf.Min(baseReal, r.bounds.min.y);
            }
            if (float.IsPositiveInfinity(baseReal)) continue;

            // Y el suelo que tiene justo debajo.
            Vector3 pos = go.transform.position;
            float sueloAqui = PegarAlSuelo(pos, nombre).y;

            float desfase = sueloAqui - baseReal;   // + = enterrado, - = flotando
            if (Mathf.Abs(desfase) <= 0.02f) continue;   // ya apoya

            Undo.RecordObject(go.transform, "Asentar en el suelo");
            go.transform.position = pos + Vector3.up * desfase;
            EditorUtility.SetDirty(go);
            tocados++;

            Debug.Log($"[PrologoValle] '{nombre}' {(desfase > 0f ? "sube" : "baja")} " +
                $"{Mathf.Abs(desfase):F2} m para apoyarse en el suelo.");
        }

        return tocados;
    }

    private static int ArreglarElSuelo(Scene escena)
    {
        InformarDeLosShaders(escena);

        // El blanco del pase anterior ya no lo usa nadie (el color va dentro de la textura).
        const string blancoViejo = "Assets/Art/World/Prologo_Valle/Materials/Blanco_4x4.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(blancoViejo) != null)
            AssetDatabase.DeleteAsset(blancoViejo);

        int tocados = 0;
        tocados += AQuibli(escena, "Suelo_Valle", RutaMatPlantilla, RutaMatSuelo,
            "Mat_Valle_Pradera_Quibli", VerdePradera);
        tocados += AQuibli(escena, "Rio_Agua", RutaMatPlantillaAgua, RutaMatAgua,
            "Mat_Valle_Agua_Quibli", AzulRio);
        return tocados;
    }

    /// Pasa una superficie a Quibli: copia un material que ya funciona y le pone albedo blanco
    /// más el tinte. Lo de la textura blanca está explicado arriba, en `RutaMatPlantilla`.
    ///
    /// Repara SIEMPRE, aunque el material ya exista: un material que quedó negro de un pase
    /// anterior se arregla en el sitio, sin tener que borrarlo. Rehacer desde la plantilla solo
    /// hace falta si no está o si no es Quibli.
    private static int AQuibli(Scene escena, string objeto, string plantilla, string destino,
        string nombre, Color color)
    {
        var go = BuscarEnEscena(escena, objeto);
        if (go == null || !go.TryGetComponent(out MeshRenderer render))
        {
            Debug.LogWarning($"[PrologoValle] No encuentro '{objeto}' con MeshRenderer.");
            return 0;
        }

        var plano = TexturaPlana(color);
        if (plano == null) return 0;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(destino);

        // El albedo en null es la huella del primer pase fallido (suelo negro). Ningún material
        // salido de aquí la tiene ya, así que sirve para rehacerlo una vez desde la plantilla y
        // recoger de paso la rampa y el auto-sombreado calibrados.
        bool eraElNegro = mat != null && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null;

        bool hayQueRehacerlo = mat == null || mat.shader == null
            || mat.shader.name != ShaderQuibli || eraElNegro;

        if (hayQueRehacerlo)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(plantilla) == null)
            {
                Debug.LogWarning($"[PrologoValle] No encuentro la plantilla '{plantilla}'. " +
                    $"'{objeto}' se queda como está.");
                return 0;
            }

            if (mat != null) AssetDatabase.DeleteAsset(destino);
            if (!AssetDatabase.CopyAsset(plantilla, destino))
            {
                Debug.LogWarning($"[PrologoValle] No he podido copiar la plantilla para '{objeto}'.");
                return 0;
            }

            AssetDatabase.ImportAsset(destino);
            mat = AssetDatabase.LoadAssetAtPath<Material>(destino);
            if (mat == null) return 0;

            Debug.Log($"[PrologoValle] Material de '{objeto}' REHECHO copiando " +
                $"'{System.IO.Path.GetFileNameWithoutExtension(plantilla)}'.");
        }

        // El arreglo. El color va en la textura, no en `_BaseColor`: ver la explicación larga
        // arriba, en `RutaMatPlantilla`. `_BaseColor` se pone igualmente por si alguna variante
        // del shader lo mira, pero no es lo que colorea.
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", plano);
            mat.SetTextureScale("_BaseMap", Vector2.one);   // la plantilla venía a 15×15
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
        }
        if (mat.HasProperty("_TextureImpact")) mat.SetFloat("_TextureImpact", 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

        // El mapa de detalle de la plantilla es hierba de su escena de demostración. Aquí sobra:
        // a 260 m se ve como una malla repetida. A 0 queda `color *= 1`, o sea, no hace nada.
        if (mat.HasProperty("_DetailMapImpact")) mat.SetFloat("_DetailMapImpact", 0f);
        if (mat.HasProperty("_DetailMap")) mat.SetTexture("_DetailMap", null);

        mat.name = nombre;

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        if (render.sharedMaterial == mat) return 1;

        Undo.RecordObject(render, "Cambiar el material a Quibli");
        render.sharedMaterial = mat;
        EditorUtility.SetDirty(render);
        Debug.Log($"[PrologoValle] '{objeto}' pasa a Quibli/StylizedLit.");
        return 1;
    }

    /// Devuelve un PNG de 4×4 del color pedido, generándolo la primera vez. Uno por color, con
    /// el color en el nombre, así que pedir dos veces el mismo verde reutiliza el mismo archivo.
    ///
    /// Esto es lo que COLOREA la superficie. En Quibli el color no está en el material: está en
    /// la textura (ver `RutaMatPlantilla`). Cuatro píxeles bastan — es un color plano.
    ///
    /// No se usa `Texture2D.whiteTexture` ni nada parecido: esos son recursos de tiempo de
    /// ejecución, no assets, y al guardar el material la referencia se pierde y el mapa vuelve a
    /// quedar en null, que es exactamente el fallo del suelo negro. Tiene que ser un PNG en disco.
    private static Texture2D TexturaPlana(Color color)
    {
        // El proyecto va en Linear. El PNG se escribe en gamma y el importador, marcado sRGB, lo
        // devuelve a linear al muestrear. Sin esto el verde saldría lavado.
        Color g = color.gamma;
        var b = new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(g.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(g.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(g.b) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f));

        string ruta = $"{CarpetaPlanos}/Plano_{b.r:X2}{b.g:X2}{b.b:X2}{b.a:X2}.png";

        var ya = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (ya != null) return ya;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
        var pixeles = new Color32[16];
        for (int i = 0; i < pixeles.Length; i++) pixeles[i] = b;
        tex.SetPixels32(pixeles);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        if (!System.IO.Directory.Exists(CarpetaPlanos))
            System.IO.Directory.CreateDirectory(CarpetaPlanos);
        System.IO.File.WriteAllBytes(ruta, png);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);

        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;              // es un albedo: se corrige gamma al muestrear
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;      // la alfa del agua vive aquí, no en _BaseColor
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Point;   // 16 píxeles idénticos: no hay nada que filtrar
            imp.mipmapEnabled = false;           // ni nada que reducir a lo lejos
            imp.SaveAndReimport();
        }

        var creada = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (creada == null)
            Debug.LogError($"[PrologoValle] No he podido crear la textura plana en '{ruta}'.");
        else
            Debug.Log($"[PrologoValle] Textura de color plano creada: '{ruta}'.");

        return creada;
    }

    /// Imprime qué shader usa cada superficie grande. Es el informe que cierra el diagnóstico.
    private static void InformarDeLosShaders(Scene escena)
    {
        var sb = new System.Text.StringBuilder("[PrologoValle] Superficies grandes y su shader:\n");

        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(includeInactive: false))
            {
                Vector3 t = r.bounds.size;
                if (Mathf.Max(t.x, t.z) < 30f) continue;

                string shader = r.sharedMaterial != null && r.sharedMaterial.shader != null
                    ? r.sharedMaterial.shader.name
                    : "SIN MATERIAL";

                sb.AppendLine($"  · {r.name} — {t.x:F0}×{t.z:F0} m — {shader}");
            }
        }

        Debug.Log(sb.ToString());
    }

    // ── Lo que se mueve no puede ser estático ─────────────────────────────────────────────────

    /// Quita las marcas de estático a todo lo que la secuencia mueve o enciende.
    ///
    /// Raúl lo acertó a la primera: *«el carro no levita, puede que el GO esté como static»*. Lo
    /// está — y no un poco: `m_StaticEditorFlags = 4294967295`, o sea **todas**, incluida
    /// **Batching Static**. La carreta, el globo, la mesa y el horno.
    ///
    /// Con Batching Static, Unity fusiona esos renderers en una malla combinada al cargar la
    /// escena. A partir de ahí **mover su transform en runtime no cambia nada de lo que se ve**: el
    /// objeto viaja, sus vértices no. Por eso la carreta no levitaba y por eso el globo no subía,
    /// aunque los `PropMoveBeat` se estuvieran ejecutando perfectamente.
    ///
    /// Se limpian los flags del prop y de todos sus hijos, porque el batching es por renderer.
    private static int DesestatizarLosProps(Scene escena, SequenceStage stage)
    {
        if (stage == null || stage.Props == null) return 0;

        int tocados = 0;

        foreach (var prop in stage.Props)
        {
            if (prop.target == null) continue;

            foreach (var t in prop.target.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                var go = t.gameObject;
                if (GameObjectUtility.GetStaticEditorFlags(go) == 0) continue;

                Undo.RecordObject(go, "Quitar el estático");
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                EditorUtility.SetDirty(go);
                tocados++;
            }
        }

        if (tocados > 0)
            Debug.Log($"[PrologoValle] {tocados} objeto(s) han dejado de ser estáticos. Eran " +
                "Batching Static, así que Unity los fusionaba al cargar la escena y moverlos en " +
                "runtime no se veía.");

        return tocados;
    }

    // ── Las piezas sueltas ────────────────────────────────────────────────────────────────────

    /// Cuelga de cada prop las piezas que son objetos SEPARADOS en la escena.
    ///
    /// La carreta no es un objeto: son cuatro — `Carreta`, `Carreta_Rueda`, `Carreta_Heno` y
    /// `Carreta_Verdura`, colocados uno al lado de otro pero sin ninguna relación entre ellos.
    /// `PROP_Carreta` apunta solo al primero, así que cuando el hechizo la levanta, **sube el
    /// cuerpo de la carreta y la rueda y el heno se quedan en el suelo**. Y al posarla, el cuerpo
    /// baja y las piezas siguen donde estaban: eso es "levita pero se queda en el aire".
    ///
    /// Igual el globo, que tiene su cuerda aparte.
    ///
    /// Emparentarlas al prop hace que se muevan como una sola cosa, que es lo que son.
    private static readonly (string padre, string[] hijos)[] PiezasDeCadaProp =
    {
        ("Carreta", new[] { "Carreta_Rueda", "Carreta_Heno", "Carreta_Verdura" }),
        ("Globo",   new[] { "Globo_Cuerda" }),
    };

    private static int JuntarLasPiezasSueltas(Scene escena)
    {
        int tocadas = 0;

        foreach (var (nombrePadre, hijos) in PiezasDeCadaProp)
        {
            var padre = BuscarEnEscena(escena, nombrePadre);
            if (padre == null) continue;

            foreach (string nombreHijo in hijos)
            {
                var hijo = BuscarEnEscena(escena, nombreHijo);
                if (hijo == null || hijo.transform.parent == padre.transform) continue;

                // worldPositionStays: la pieza NO se mueve de donde está, solo cambia de quién
                // cuelga. Lo contrario la teletransportaría al pivot del padre.
                Undo.SetTransformParent(hijo.transform, padre.transform, "Juntar la pieza al prop");
                hijo.transform.SetParent(padre.transform, worldPositionStays: true);
                EditorUtility.SetDirty(hijo);
                tocadas++;
            }
        }

        if (tocadas > 0)
            Debug.Log($"[PrologoValle] {tocadas} pieza(s) ahora cuelgan de su prop. Antes la " +
                "carreta se levantaba dejándose la rueda y el heno en el suelo.");

        return tocadas;
    }

    // ── Props ─────────────────────────────────────────────────────────────────────────────────

    /// El escudo y la puerta del Sendero entran como PROPS de la escena, apagados, no como VfxBeat.
    ///
    /// Dos motivos. El escudo tiene que quedarse LEVANTADO durante toda la evacuación y todo el
    /// duelo — es lo que hace que el duelo tenga algo en juego —, y un VfxBeat es un disparo con
    /// caducidad. Y los dos prefabs se resuelven aquí por su ruta, en vez de por un fileID escrito a
    /// mano en el YAML de la secuencia, que es donde se rompen las referencias a prefabs variante.
    /// Props del SequenceStage que los que andan tienen que rodear. Ver SequenceStage.esObstaculo.
    private static readonly string[] PropsQueSeRodean = { "PROP_Carreta" };

    private static int CrearProps(Scene escena, SequenceStage stage)
    {
        var deseados = new (string id, string ruta, Vector3 pos, float escala, float alturaOjos)[]
        {
            // (20 sep) Movido con el duelo. Ahora la cupula se levanta DETRAS del Archimago,
            // entre el y el puente: lo que protege es por donde se esta yendo la gente, y eso se
            // ve en el mismo plano en el que el dice que todavia estan cruzando.
            ("PROP_Escudo", RutaEscudo, new Vector3(6010f, 100f, 5998.5f), 11f, 2.5f),

            // La puerta se abre donde estaba el Mago Oscuro, al final del duelo.
            ("PROP_PuertaSendero", RutaPuerta, new Vector3(6000f, 101.5f, 5997.5f), 4f, 1.5f),

            // Las dos nubes del primer plano (INC-370): el prologo abre con la camara a catorce
            // metros, y estas dos se abren delante de ella y dejan ver el valle. Estan doce metros
            // sobre la plaza, una a cada lado del centro, y la secuencia las aparta y las apaga.
            // Se pueden mover a ojo en la Scene View: son objetos de la escena como los demas.
        };

        var so = new SerializedObject(stage);
        var props = so.FindProperty("_props");
        if (props == null)
        {
            Debug.LogError("[PrologoValle] El SequenceStage no tiene '_props'.");
            return 0;
        }

        var yaDadosDeAlta = new HashSet<string>();
        for (int i = 0; i < props.arraySize; i++)
        {
            string n = props.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
            if (!string.IsNullOrWhiteSpace(n)) yaDadosDeAlta.Add(n.Trim());
        }

        int creados = 0;

        foreach (var (id, ruta, pos, escala, alturaOjos) in deseados)
        {
            var go = BuscarEnEscena(escena, id);

            if (go == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PrologoValle] No encuentro '{ruta}'. '{id}' se queda sin crear.");
                    continue;
                }

                var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                Undo.RegisterCreatedObjectUndo(instancia, "Crear prop de la secuencia");

                // El GameObject se llama como el id para que se le pueda encontrar en la jerarquía
                // igual que a PROP_Incendio.
                go = new GameObject(id);
                Undo.RegisterCreatedObjectUndo(go, "Crear prop de la secuencia");
                SceneManager.MoveGameObjectToScene(go, escena);
                instancia.transform.SetParent(go.transform, worldPositionStays: false);

                creados++;
            }

            go.transform.position = PegarAlSuelo(pos, id) + Vector3.up * (pos.y - 100f);
            go.transform.localScale = Vector3.one * escala;
            go.SetActive(false);   // lo enciende la secuencia con SetPropActiveBeat

            if (yaDadosDeAlta.Contains(id)) continue;

            props.arraySize++;
            var entrada = props.GetArrayElementAtIndex(props.arraySize - 1);
            entrada.FindPropertyRelative("id").stringValue = id;
            entrada.FindPropertyRelative("target").objectReferenceValue = go.transform;
            entrada.FindPropertyRelative("eyeHeight").floatValue = alturaOjos;
        }

        // Lo que está en el suelo y en medio del paso se RODEA (WalkPathBeat). «La gente se
        // tropieza con la carreta en lugar de esquivarla»: está donde Raúl la ha puesto, en el
        // camino de la plaza al puente, y los vecinos la atravesaban en línea recta.
        for (int i = 0; i < props.arraySize; i++)
        {
            var e = props.GetArrayElementAtIndex(i);
            string id = e.FindPropertyRelative("id").stringValue?.Trim();
            var flag = e.FindPropertyRelative("esObstaculo");
            if (flag == null) continue;   // SequenceStage sin recompilar todavía
            bool debe = System.Array.IndexOf(PropsQueSeRodean, id) >= 0;
            if (flag.boolValue != debe) flag.boolValue = debe;
        }

        so.ApplyModifiedProperties();
        return creados;
    }

    // ── Roster ────────────────────────────────────────────────────────────────────────────────

    private static int RellenarRoster()
    {
        var roster = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RutaRoster);
        if (roster == null)
        {
            Debug.LogError($"[PrologoValle] No encuentro el roster en '{RutaRoster}'.");
            return 0;
        }

        var so = new SerializedObject(roster);
        var entradas = so.FindProperty("entries");
        if (entradas == null)
        {
            Debug.LogError("[PrologoValle] El roster no tiene 'entries'.");
            return 0;
        }

        var yaEstan = new HashSet<string>();
        for (int i = 0; i < entradas.arraySize; i++)
        {
            string id = entradas.GetArrayElementAtIndex(i).FindPropertyRelative("spawnId").stringValue;
            if (!string.IsNullOrWhiteSpace(id)) yaEstan.Add(id.Trim());
        }

        int creadas = 0;

        for (int i = 0; i < PuestosAldeanos.Length; i++)
        {
            string spawnId = $"SPAWN_NPC_Aldeano_{i + 1:00}";
            if (yaEstan.Contains(spawnId)) continue;

            string rutaPrefab = string.Format(RutaTownNpcs, i + 1);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[PrologoValle] No encuentro '{rutaPrefab}'. Ese aldeano se queda " +
                    "sin dar de alta.");
                continue;
            }

            entradas.arraySize++;
            var e = entradas.GetArrayElementAtIndex(entradas.arraySize - 1);
            e.FindPropertyRelative("enabled").boolValue = true;
            e.FindPropertyRelative("spawnId").stringValue = spawnId;
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            e.FindPropertyRelative("gameObjectName").stringValue = $"NPC_Aldeano_{i + 1:00}";
            e.FindPropertyRelative("persistenceId").stringValue = $"NPC_Aldeano_{i + 1:00}";
            e.FindPropertyRelative("startActive").boolValue = true;
            e.FindPropertyRelative("requireNavMesh").boolValue = true;
            e.FindPropertyRelative("requiredFlag").stringValue = string.Empty;
            e.FindPropertyRelative("invertCondition").boolValue = false;

            creadas++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(roster);
        return creadas;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────────────────────

    /// Lleva un punto al suelo caminable más cercano.
    ///
    /// Es lo que evita el bucle de «corrige esta coordenada / sigue saliendo el aviso»: NpcSpawner
    /// avisa (y deja al aldeano sin poder caminar) cuando no encuentra NavMesh a 3 m del marcador, y
    /// desde fuera del Editor no hay forma de saber dónde acaba el suelo caminable de la plaza. Aquí
    /// dentro sí: se le pregunta al NavMesh y se acepta su respuesta.
    private static Vector3 AjustarAlNavMesh(Vector3 pedido, string quien)
    {
        for (int i = 0; i < RadiosDeBusqueda.Length; i++)
        {
            if (!NavMesh.SamplePosition(pedido, out NavMeshHit hit, RadiosDeBusqueda[i], NavMesh.AllAreas))
                continue;

            float desvio = Vector3.Distance(pedido, hit.position);
            if (desvio > 0.05f)
                Debug.Log($"[PrologoValle] '{quien}' se pega al suelo caminable: {desvio:F1} m desde " +
                    "donde decía la tabla.");

            return hit.position;
        }

        Debug.LogWarning($"[PrologoValle] '{quien}' no tiene NavMesh a menos de 9 m de {pedido}. Se " +
            "deja donde estaba. Si sale este aviso, lo que hay que mirar es el NavMesh de la escena, " +
            "no esta tabla.");

        return pedido;
    }

    /// Baja un punto hasta el terreno que tenga debajo, con un raycast.
    ///
    /// Sirve para las marcas que NO están sobre suelo caminable: la cresta de la montaña y la
    /// ladera. Antes esa altura se ponía a ojo y había que arrastrar la marca a mano con la escena
    /// abierta; en Edit mode el raycast funciona igual que en Play, así que no hace falta.
    private static Vector3 PegarAlSuelo(Vector3 pedido, string quien)
    {
        // Se tira desde bien arriba: la cresta de una loma puede estar bastante por encima del
        // valle, y empezar el rayo a la altura del pueblo lo dejaría dentro de la montaña.
        Vector3 desde = new(pedido.x, pedido.y + 60f, pedido.z);

        if (Physics.Raycast(desde, Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(hit.point.y - pedido.y) > 0.1f)
                Debug.Log($"[PrologoValle] '{quien}' se apoya en el terreno a y = {hit.point.y:F1} " +
                    $"(la tabla decía {pedido.y:F1}).");

            return hit.point;
        }

        Debug.LogWarning($"[PrologoValle] '{quien}' no tiene suelo debajo en {pedido}. Se deja a la " +
            "altura de la tabla; si el personaje flota o se hunde, esta marca es la que hay que mirar.");

        return pedido;
    }

    private static GameObject BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz;

            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        }

        return null;
    }

    private static SequenceStage BuscarStage(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var s = raiz.GetComponentInChildren<SequenceStage>(true);
            if (s != null) return s;
        }

        return null;
    }
}
#endif
