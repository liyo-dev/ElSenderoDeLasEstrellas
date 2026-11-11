using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Casas y piezas propias de cada zona (hechas en Tools/Modelos/Zonas; prefabs en Assets/Art/World/Zonas/Prefabs):
/// el barrio sobre el agua de la aldea del mar, el faro del cabo, las hileras de casas de ciudad de la capital
/// y el árbol del columpio del pueblo inicial.
public static partial class VestidoDelMundo
{
    private const string PrefabsDeZonas = "Assets/Art/World/Zonas/Prefabs/";
    private const string FaroNuevo = PrefabsDeZonas + "Faro.prefab";
    private const string PasarelaNueva = PrefabsDeZonas + "PasarelaDeLaAldea.prefab";
    private const string LonjaNueva = PrefabsDeZonas + "Lonja.prefab";
    private const string ArbolDelColumpio = PrefabsDeZonas + "ArbolConColumpio.prefab";
    private const string RellanoNuevo = PrefabsDeZonas + "RellanoDeLaAldea.prefab";
    private const string MiradorNuevo = PrefabsDeZonas + "MiradorDeLaAldea.prefab";
    private const string PaseoNuevo = PrefabsDeZonas + "PaseoDeTablas.prefab";
    private const string RosaNueva = PrefabsDeZonas + "RosaDeLosVientos.prefab";
    private const string BanderinesNuevos = PrefabsDeZonas + "Banderines.prefab";
    private const string FuenteNueva = PrefabsDeZonas + "FuenteDeLaPlaza.prefab";
    private const string ArbolDePlazaNuevo = PrefabsDeZonas + "ArbolDePlaza.prefab";
    private static readonly string[] TiendasDelBarrio = { "TiendaDeLaAldea_Pescado", "TiendaDeLaAldea_Fruta", "TiendaDeLaAldea_Aparejos" };

    // ── Aldea del mar: barrio sobre el agua ──────────────────────────────────────────────────

    /// Casas del barrio, de la orilla hacia el mar: prefab, ancho y fondo de la casa (m).
    private static readonly (string prefab, float ancho, float fondo)[] CasasDelBarrio =
    {
        ("CasaDeLaAldea", 4.6f, 3.6f), ("CasaDeLaAldea_Coral", 3.4f, 3.0f), ("CasaDeLaAldea_Amarilla", 4.8f, 3.6f),
        ("CasaDeLaAldea_Azul", 4.2f, 3.4f), ("CasaDeLaAldea_Alta", 3.8f, 3.2f), ("CasaDeLaAldea_Pequena", 3.2f, 2.8f),
    };

    /// Medidas de las piezas (Tools/Modelos/Zonas/zonas.py): altura de las tablas sobre el agua, lo que bajan los
    /// pilotes bajo la lámina, vuelo de la plataforma a cada lado de la casa, fondo del porche y largo de un tramo
    /// de pasarela.
    private const float AltoDeLasTablas = 1.6f, PilotesBajoElAgua = 2.4f, VueloDeLaPlataforma = 0.6f, FondoDelPorche = 1.5f,
        TramoDePasarela = 3f;
    /// De la casa (su origen) al centro de lo que se ve de ella, hacia la fachada: la escalera al agua sale por delante.
    private const float CentroVisibleHaciaLaFachada = 1.76f;

    /// Una calle de casas sobre pilotes que sale de la playa mar adentro, al oeste del embarcadero: una pasarela
    /// desde la arena y, a continuación, las casas unidas por tramos de pasarela a la altura de sus porches. Las
    /// fachadas miran al embarcadero, así se ven en fila desde la plaza y desde el muelle.
    private static void BarrioSobreElAgua(Obra o, Transform grupo)
    {
        var origen = new Vector2(228f, -452f);
        if (!BuscarOrilla(o, origen, 120f, out _, out Vector2 mar))
        {
            o.Informe.Add("  Barrio sobre el agua: no se encontró la orilla; no se pone.");
            return;
        }
        // La pasarela arranca donde la arena queda a la altura de las tablas; la primera casa, donde ya cubre.
        Vector2 arranque = origen;
        for (int i = 0; i < 400 && o.Suelo.Altura(arranque.x, arranque.y) > AltoDeLasTablas - 0.1f; i++) arranque += mar * 0.25f;
        Vector2 primera = arranque;
        for (int i = 0; i < 400 && o.Suelo.Altura(primera.x, primera.y) > -0.6f; i++) primera += mar * 0.25f;

        Transform barrio = Grupo(grupo, "Barrio sobre el agua");
        Vector2 fachada = new Vector2(-mar.y, mar.x);
        if (fachada.x < 0f) fachada = -fachada;
        float rumboDePasarela = Rumbo(mar) - 90f, rumboDeCasa = Rumbo(-fachada);
        int tramos = Mathf.CeilToInt(Vector2.Distance(arranque, primera) / TramoDePasarela), casas = 0;
        for (int k = 0; k < tramos; k++)
            PonSobrePilotes(o, barrio, PasarelaNueva, "Pasarela de la playa", primera - mar * (TramoDePasarela * (k + 0.5f)), rumboDePasarela);

        float u = 0f;
        for (int i = 0; i < CasasDelBarrio.Length; i++)
        {
            var (prefab, ancho, fondo) = CasasDelBarrio[i];
            float medio = ancho * 0.5f + VueloDeLaPlataforma;
            Vector2 centro = primera + mar * (u + medio) - fachada * (fondo * 0.5f + FondoDelPorche * 0.5f);
            if (o.Suelo.Altura(centro.x, centro.y) > 0f)
            {
                o.Informe.Add($"  Barrio sobre el agua: en ({centro.x:0.#}, {centro.y:0.#}) no hay agua; la calle acaba ahí.");
                break;
            }
            GameObject casa = PonSobrePilotes(o, barrio, PrefabsDeZonas + prefab + ".prefab", "Casa del barrio sobre el agua",
                centro + fachada * CentroVisibleHaciaLaFachada, rumboDeCasa);
            if (casa != null)
            {
                // Poner centra lo que se ve; la casa va por su origen, para que el porche case con la pasarela.
                casa.transform.position = new Vector3(centro.x, casa.transform.position.y, centro.y);
                casas++;
            }
            u += medio * 2f;
            if (i == CasasDelBarrio.Length - 1) break;
            // Entre casa y casa: pasarela o, en tres de los huecos, un rellano de descanso del que sale un ramal a una tienda.
            Vector2 hueco = primera + mar * (u + TramoDePasarela * 0.5f);
            if (i == 0) RamalConTienda(o, barrio, hueco, fachada, 2, TiendasDelBarrio[0]);
            else if (i == 2) RamalConTienda(o, barrio, hueco, fachada, 1, TiendasDelBarrio[1]);
            else if (i == 3) RamalConTienda(o, barrio, hueco, -fachada, 2, TiendasDelBarrio[2]);
            else PonSobrePilotes(o, barrio, PasarelaNueva, "Pasarela entre casas", hueco, rumboDePasarela);
            u += TramoDePasarela;
        }
        // Al final de la calle, un templete de descanso; y, apartada, una casa a la que solo se llega nadando.
        Vector2 fin = primera + mar * u;
        PonSobrePilotes(o, barrio, PasarelaNueva, "Pasarela del templete", fin + mar * (TramoDePasarela * 0.5f), rumboDePasarela);
        PonPorSuOrigen(o, barrio, MiradorNuevo, "Templete del final de la calle", fin + mar * (TramoDePasarela + 2.5f), rumboDeCasa);
        Vector2 apartada = fin + mar * 6f + fachada * 24f;
        if (o.Suelo.Altura(apartada.x, apartada.y) <= -0.5f)
            PonPorSuOrigen(o, barrio, PrefabsDeZonas + "CasaDeLaAldea_Amarilla.prefab", "Casa apartada (se llega nadando)", apartada, Rumbo(-(fin - apartada).normalized));
        else o.Informe.Add($"  Barrio sobre el agua: en ({apartada.x:0.#}, {apartada.y:0.#}) no cubre; la casa apartada no se pone.");

        // Paseo de tablas por lo alto de la playa, del pie de la rampa del embarcadero hacia la pasarela del barrio
        // (el último tramo, playa abajo, se anda por la arena).
        Vector2[] camino = { new Vector2(262.5f, -451f), new Vector2(241f, -452f) };
        for (int k = 0; k < camino.Length - 1; k++)
        {
            Vector2 tramo = camino[k + 1] - camino[k];
            int piezas = Mathf.Max(1, Mathf.RoundToInt(tramo.magnitude / 3f));
            for (int j = 0; j < piezas; j++)
                Poner(o, barrio, new Pieza
                {
                    Prefab = PaseoNuevo, Nombre = "Paseo de tablas", Pos = camino[k] + tramo * ((j + 0.5f) / piezas), Rumbo = Rumbo(tramo.normalized) - 90f,
                    IgnorarZonas = true, PermitirCamino = true, PermitirSolapePropio = true, DesnivelMax = 1.8f, Hundir = 0.25f, Holgura = -0.3f, SinObstaculo = true,
                });
        }
        o.Informe.Add($"  Barrio sobre el agua: {casas} de {CasasDelBarrio.Length} casas y {tramos} tramos de pasarela desde la playa en " +
                      $"({arranque.x:0.#}, {arranque.y:0.#}); la primera casa en ({primera.x:0.#}, {primera.y:0.#}), {u:0.#} m mar adentro.");
    }

    /// Rellano de descanso en el sitio de un tramo de pasarela y, desde él, un ramal de «tramos» pasarelas hasta una
    /// tienda sobre pilotes. «haciaFuera»: hacia dónde sale el ramal.
    private static void RamalConTienda(Obra o, Transform grupo, Vector2 hueco, Vector2 haciaFuera, int tramos, string tienda)
    {
        const float fondoDelRellano = 3.7f, medioFondoDeLaTienda = 2.3f;
        float rumbo = Rumbo(-haciaFuera);
        PonPorSuOrigen(o, grupo, RellanoNuevo, "Rellano de descanso", hueco, rumbo);
        for (int k = 0; k < tramos; k++)
            PonSobrePilotes(o, grupo, PasarelaNueva, "Pasarela del ramal", hueco + haciaFuera * (fondoDelRellano + TramoDePasarela * (k + 0.5f)), Rumbo(haciaFuera) - 90f);
        PonPorSuOrigen(o, grupo, PrefabsDeZonas + tienda + ".prefab", "Tienda sobre el agua",
            hueco + haciaFuera * (fondoDelRellano + TramoDePasarela * tramos + medioFondoDeLaTienda), rumbo);
    }

    /// Pone una pieza sobre pilotes con su origen (no el centro de lo que se ve) en «origen»: así casa con las pasarelas.
    private static GameObject PonPorSuOrigen(Obra o, Transform grupo, string prefab, string nombre, Vector2 origen, float rumbo)
    {
        GameObject pieza = PonSobrePilotes(o, grupo, prefab, nombre, origen, rumbo);
        if (pieza != null) pieza.transform.position = new Vector3(origen.x, pieza.transform.position.y, origen.y);
        return pieza;
    }

    /// Pone una pieza sobre pilotes: sus tablas quedan a AltoDeLasTablas sobre el mar.
    private static GameObject PonSobrePilotes(Obra o, Transform grupo, string prefab, string nombre, Vector2 pos, float rumbo) =>
        Poner(o, grupo, new Pieza
        {
            Prefab = prefab, Nombre = nombre, Pos = pos, Rumbo = rumbo, Apoyo = Apoyo.Flotar, CotaAgua = NivelDelMar,
            Hundir = PilotesBajoElAgua, IgnorarZonas = true, PermitirCamino = true, PermitirSolapePropio = true, Holgura = 0f,
        });

    // ── Aldea del mar: faro, lonja y secaderos ───────────────────────────────────────────────

    private const string GrupoDeHitos = "Hitos del mapa — faro, velero, puentes, cascada";
    private const string NombreDeLaTorreVigia = "Faro (torre vigía) — cabo del puerto";
    private const string MotivoTorreVigia = "torre vigía del cabo: la sustituye el faro de la aldea";

    /// El faro de franjas, sobre el basamento rocoso del cabo y con la puerta hacia la aldea. La torre vigía de
    /// piedra que había se retira. La lonja va en tierra, junto a la playa.
    private static void FaroYLonjaDelPuerto(Obra o, Transform grupo)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform hitos = BuscarGrupo(escena, GrupoDeHitos);
        Retirar(escena, hitos != null ? hitos.Find(NombreDeLaTorreVigia) : null, MotivoTorreVigia, o);
        var cabo = new Vector2(365f, -470f);
        Vector2 aLaAldea = (new Vector2(270f, -433f) - cabo).normalized;
        PonEnElMuelle(o, Grupo(grupo, "Faro del cabo"), FaroNuevo, "Faro del cabo", cabo, Rumbo(-aLaAldea), hundir: 2f, tamano: 1.25f);

        Transform lonja = Grupo(grupo, "Lonja y secaderos");
        Pon(o, lonja, LonjaNueva, "Lonja", new Vector2(244f, -445f), 0f, desnivel: 1.2f, hundir: 0.1f);

        // La plaza del puerto: rosa de los vientos en el suelo (se pisa; no estorba a los vecinos) y ristras de banderines.
        Transform plaza = Grupo(grupo, "Plaza del puerto");
        Poner(o, plaza, new Pieza
        {
            Prefab = RosaNueva, Nombre = "Rosa de los vientos", Pos = new Vector2(270f, -433f), IgnorarZonas = true, PermitirCamino = true,
            PermitirSolapePropio = true, DesnivelMax = 1.2f, Hundir = 0.9f, Holgura = -1f, SinObstaculo = true,
        });
        foreach (float z in new[] { -421.5f, -433f, -444.5f })
            Poner(o, plaza, new Pieza
            {
                Prefab = BanderinesNuevos, Nombre = "Banderines de la plaza", Pos = new Vector2(270f, z), IgnorarZonas = true, PermitirCamino = true,
                PermitirSolapePropio = true, DesnivelMax = 3f, Hundir = 0.3f, Holgura = -1f, SinObstaculo = true,
            });
        CasasDeTierraDelPuerto(o, grupo);
    }

    private static readonly string[] CasasDeTierraDeLaAldea = { "Turquesa", "Azul", "Coral" };
    private const string MotivoCasaDelPack = "casa del pack en la aldea del mar: la sustituye una casa encalada de la aldea";

    /// Cambia las casas del pack de la aldea (en tierra) por casas encaladas de la aldea, en su sitio y mirando a
    /// donde miraban. Las grandes pasan a ser la de dos plantas. Lo que colgaba de cada casa vieja (enseres del
    /// patio) se retira con ella.
    private static void CasasDeTierraDelPuerto(Obra o, Transform grupo)
    {
        Scene escena = o.Raiz.gameObject.scene;
        HashSet<string> giradas = CasasGiradas(escena);
        Transform casas = Grupo(grupo, "Casas de la aldea");
        int n = 0, puestas = 0;
        foreach (string nombreGrupo in Puerto().Grupos)
        {
            Transform g = BuscarGrupo(escena, nombreGrupo);
            if (g == null) continue;
            var viejas = new List<(Transform casa, string prefab)>();
            foreach (Transform casa in g)
            {
                if (!casa.gameObject.activeSelf) continue;
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente != null && fuente.name.StartsWith("BuildingAT")) viejas.Add((casa, fuente.name));
            }
            foreach (var (casa, prefab) in viejas)
            {
                Vector3 f3 = FrenteDeCasa(casa, prefab, giradas);
                Vector2 frente = new Vector2(f3.x, f3.z).normalized;
                Bounds b = LimitesVisibles(casa.gameObject);
                string variante = Mathf.Max(b.size.x, b.size.z) > 10f ? "Amarilla" : CasasDeTierraDeLaAldea[n % CasasDeTierraDeLaAldea.Length];
                n++;
                Retirar(escena, casa, MotivoCasaDelPack, o);
                Physics.SyncTransforms();
                if (Poner(o, casas, new Pieza
                    {
                        Prefab = PrefabsDeZonas + "CasaDeLaAldeaEnTierra_" + variante + ".prefab", Nombre = casa.name + " (casa de la aldea)",
                        Pos = new Vector2(b.center.x, b.center.z), Rumbo = Rumbo(-frente), IgnorarZonas = true, PermitirCamino = true,
                        PermitirSolapePropio = true, DesnivelMax = 3f, Hundir = 0.45f, Holgura = -0.5f,
                    }) != null) puestas++;
            }
        }
        o.Informe.Add($"  Casas de tierra de la aldea: {puestas} de {n} casas del pack cambiadas por casas de la aldea.");
    }

    // ── Pueblo inicial ───────────────────────────────────────────────────────────────────────

    private const string GrupoDelPuebloInicial = "Pueblo inicial — Will — Demo 09";
    private const string MotivoMina = "bocamina con vagonetas y raíles: no es de un pueblo de campo; en su sitio va el árbol del columpio";

    /// Aligera el pueblo inicial: retira la bocamina (vagonetas y raíles) y planta en su sitio el árbol del columpio.
    private static void ArbolDelPuebloInicial(Obra o)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform pueblo = BuscarGrupo(escena, GrupoDelPuebloInicial);
        Retirar(escena, pueblo != null ? pueblo.Find("BuildingAT35") : null, MotivoMina, o);
        Poner(o, Grupo(Grupo(o.Raiz, "Pueblo inicial"), "Árbol del columpio"), new Pieza
        {
            Prefab = ArbolDelColumpio, Nombre = "Árbol del columpio", Pos = new Vector2(13.5f, -158.5f), Rumbo = 30f,
            Apoyo = Apoyo.Tronco, RadioTronco = 0.7f, IgnorarZonas = true, PermitirCamino = true, PermitirSolapePropio = true,
            DesnivelMax = 1.5f, Holgura = 0f,
        });
        Transform rincon = Grupo(Grupo(o.Raiz, "Pueblo inicial"), "Rincón del columpio");
        PonFijo(o, rincon, Banco, "Banco del columpio", new Vector2(9.6f, -160.6f), 60f, solapePropio: true);
        PonFijo(o, rincon, Banco, "Banco del columpio", new Vector2(16.9f, -161.6f), 300f, solapePropio: true);
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f + 0.4f;
            PonFijo(o, rincon, Flor, "Flores del columpio", new Vector2(13.5f + Mathf.Cos(a) * 3.3f, -158.5f + Mathf.Sin(a) * 3.3f), i * 50f, sinObstaculo: true, solapePropio: true);
        }
    }

    // ── Capital: hileras de casas de ciudad ──────────────────────────────────────────────────

    /// Fondo (m) de cada casa del pack que se cambia: sirve para dejar la fachada nueva en la misma línea de calle.
    private static readonly Dictionary<string, float> FondoDeCasaDelPack = new()
    {
        { "BuildingAT46", 8.5f }, { "BuildingAT55", 10.4f }, { "BuildingAT54", 8.3f }, { "BuildingAT53", 7.1f }, { "BuildingAT17", 7.9f },
        { "BuildingAT47", 7.1f }, { "BuildingAT09", 8.0f },
    };

    /// Hileras de casas de ciudad (variantes de CasaDeCiudad) que ocupan el sitio de cada casa del pack: sin oficio
    /// y con oficio (tiendas con toldo). Las opciones de cada una se van alternando.
    private static readonly Dictionary<string, string[][]> HilerasSinOficio = new()
    {
        { "BuildingAT46", new[] { new[] { "B", "E" }, new[] { "E", "B" } } },
        { "BuildingAT55", new[] { new[] { "A", "F" }, new[] { "F", "D" } } },
        { "BuildingAT54", new[] { new[] { "C", "D" }, new[] { "A", "C" } } },
        { "BuildingAT53", new[] { new[] { "F" }, new[] { "C" } } },
        { "BuildingAT17", new[] { new[] { "C" }, new[] { "A" } } },
    };

    private static readonly Dictionary<string, string[][]> HilerasDeOficio = new()
    {
        { "BuildingAT17", new[] { new[] { "H" }, new[] { "G" } } },
        { "BuildingAT53", new[] { new[] { "G" }, new[] { "H" } } },
        { "BuildingAT47", new[] { new[] { "G" }, new[] { "H" } } },
        { "BuildingAT09", new[] { new[] { "G", "H" } } },
    };

    /// Edificios públicos por su nombre: modelo nuevo, fondo del edificio del pack (hasta donde debe llegar la
    /// fachada nueva) y fondo del nuevo.
    private static readonly Dictionary<string, (string prefab, float fondoViejo, float fondoNuevo)> EdificiosPublicos = new()
    {
        { "Taberna de la plaza", ("EdificioDeCiudad_Taberna", 10.0f, 8f) },
        { "Cuerpo de guardia de la Puerta Real", ("EdificioDeCiudad_Guardia", 7.0f, 6f) },
        { "Posada del Camino Real", ("EdificioDeCiudad_Posada", 11.7f, 8f) },
        { "Palacio de poniente", ("EdificioDeCiudad_Palacio", 10.6f, 8f) },
        { "Casa de la torre verde", ("EdificioDeCiudad_Palacio", 10.6f, 8f) },
        { "Archivo Real", ("EdificioDeCiudad_Archivo", 8.2f, 8f) },
    };

    private static readonly Dictionary<string, float> AnchoDeCasaDeCiudad = new()
    {
        { "A", 5.4f }, { "B", 4.8f }, { "C", 5.8f }, { "D", 5.2f }, { "E", 5.0f }, { "F", 6.2f }, { "G", 5.6f }, { "H", 6.0f },
    };

    private const float FondoDeCasaDeCiudad = 6f, JuntaDeHilera = 0.02f;
    /// Del origen de la casa de ciudad al centro de lo que se ve, hacia la fachada (balcón, alero y estandarte).
    private const float CentroVisibleDeCasaDeCiudad = 0.5f;

    /// Cambia cada casa del pack por una hilera de casas de ciudad pared con pared (tiendas con toldo si tenía
    /// oficio; el nombre se queda en la primera) y cada edificio público por el suyo, con la fachada en la misma
    /// línea de calle.
    private static CasaNueva[] EnHileras(CasaNueva[] casas)
    {
        var r = new List<CasaNueva>();
        int n = 0;
        foreach (CasaNueva c in casas)
        {
            Vector2 frente = FrenteDe(c.Frente), lado = new Vector2(frente.y, -frente.x);
            if (c.Nombre != null && EdificiosPublicos.TryGetValue(c.Nombre, out var e))
            {
                Vector2 q = c.Pos + frente * (e.fondoViejo * 0.5f - e.fondoNuevo * 0.5f + CentroVisibleDeCasaDeCiudad);
                r.Add(new CasaNueva(PrefabsDeZonas + e.prefab + ".prefab", q.x, q.y, c.Frente, c.Nombre));
                continue;
            }
            if (!(c.Nombre != null ? HilerasDeOficio : HilerasSinOficio).TryGetValue(c.Prefab, out string[][] opciones)) { r.Add(c); continue; }
            string[] hilera = opciones[n++ % opciones.Length];
            float total = JuntaDeHilera * (hilera.Length - 1);
            foreach (string v in hilera) total += AnchoDeCasaDeCiudad[v];
            Vector2 centro = c.Pos + frente * (FondoDeCasaDelPack[c.Prefab] * 0.5f - FondoDeCasaDeCiudad * 0.5f + CentroVisibleDeCasaDeCiudad);
            float x = -total * 0.5f;
            for (int k = 0; k < hilera.Length; k++)
            {
                float ancho = AnchoDeCasaDeCiudad[hilera[k]];
                Vector2 p = centro + lado * (x + ancho * 0.5f);
                string nombre = c.Nombre == null ? null : k == 0 ? c.Nombre : c.Nombre + " (anexo)";
                r.Add(new CasaNueva(PrefabsDeZonas + "CasaDeCiudad_" + hilera[k] + ".prefab", p.x, p.y, c.Frente, nombre));
                x += ancho + JuntaDeHilera;
            }
        }
        return r.ToArray();
    }

    private const string MotivoCasaDelGenerador = "casa del pack en la capital: la sustituye una casa de ciudad";

    /// Cambia las casas del pack que quedaban en la capital (las del generador del mapa) por casas de ciudad, en su
    /// sitio, con la fachada en la misma línea y mirando a donde miraban.
    private static void CasasDeCiudadPorLasDelGenerador(Obra o, Transform grupo, Pueblo reino)
    {
        Scene escena = o.Raiz.gameObject.scene;
        HashSet<string> giradas = CasasGiradas(escena);
        Transform casas = Grupo(grupo, "Casas de ciudad del barrio viejo");
        int n = 0, puestas = 0;
        foreach (string nombreGrupo in reino.Grupos)
        {
            Transform g = BuscarGrupo(escena, nombreGrupo);
            if (g == null) continue;
            var viejas = new List<(Transform casa, string prefab)>();
            foreach (Transform casa in g)
            {
                if (!casa.gameObject.activeSelf) continue;
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente != null && fuente.name.StartsWith("BuildingAT")) viejas.Add((casa, fuente.name));
            }
            foreach (var (casa, prefab) in viejas)
            {
                Vector3 f3 = FrenteDeCasa(casa, prefab, giradas);
                Vector2 frente = new Vector2(f3.x, f3.z).normalized, lado = new Vector2(frente.y, -frente.x);
                Bounds b = LimitesVisibles(casa.gameObject);
                var c = new Vector2(b.center.x, b.center.z);
                float ancho = Alcance(casa.gameObject, c, lado) + Alcance(casa.gameObject, c, -lado);
                float fondo = Alcance(casa.gameObject, c, frente) + Alcance(casa.gameObject, c, -frente);
                string[] hilera = ancho >= 10.5f ? new[] { "A", "F" } : ancho >= 8.8f ? new[] { "B", "E" } : ancho >= 6.6f ? new[] { n % 2 == 0 ? "C" : "F" } : new[] { "A" };
                n++;
                Retirar(escena, casa, MotivoCasaDelGenerador, o);
                Physics.SyncTransforms();
                float total = JuntaDeHilera * (hilera.Length - 1);
                foreach (string v in hilera) total += AnchoDeCasaDeCiudad[v];
                Vector2 centro = c + frente * (fondo * 0.5f - FondoDeCasaDeCiudad * 0.5f + CentroVisibleDeCasaDeCiudad);
                float x = -total * 0.5f;
                for (int k = 0; k < hilera.Length; k++)
                {
                    float a = AnchoDeCasaDeCiudad[hilera[k]];
                    Vector2 p = centro + lado * (x + a * 0.5f);
                    if (Poner(o, casas, new Pieza
                        {
                            Prefab = PrefabsDeZonas + "CasaDeCiudad_" + hilera[k] + ".prefab", Nombre = casa.name + (k == 0 ? " (casa de ciudad)" : " (casa de ciudad, anexo)"),
                            Pos = p, Rumbo = Rumbo(-frente), IgnorarZonas = true, PermitirCamino = true, PermitirSolapePropio = true,
                            DesnivelMax = 1.6f, Hundir = 0.15f, Holgura = -0.3f,
                        }) != null) puestas++;
                    x += a + JuntaDeHilera;
                }
            }
        }
        o.Informe.Add($"  Barrio viejo de la capital: {n} casas del pack cambiadas por {puestas} casas de ciudad.");
    }
}
