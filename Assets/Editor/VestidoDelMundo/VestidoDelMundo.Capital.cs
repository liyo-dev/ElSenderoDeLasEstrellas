using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// La capital (el Reino): ciudad amurallada en dos terrazas, la baja (y 104) de mercado y oficios y la alta
/// (y 112) de palacios, alrededor de la Plaza Real y del castillo. Las calles, las plazas y la pintura del suelo
/// están en Reino() (VestidoDelMundo.Pueblos); la muralla, en VestidoDelMundo.Muralla; el pavimento, en
/// VestidoDelMundo.Pavimento; el arbolado, en VestidoDelMundo.Vegetacion. Aquí está lo demás:
///
/// · Retirada de lo que es de aldea (molinos, casitas de paja, herrerías y casas de vinatero de la ciudad alta,
///   el huerto, toldos tirados, el pozo de otro estilo…), sin borrarlo: ver VestidoDelMundo.Casas.
/// · Casas nuevas: taberna, cuerpo de guardia, posada, palacio, Archivo Real, cuartel y hileras de casas altas
///   de tejado azul, con las fachadas casi seguidas (CasasNuevasDelReino; las coloca PonerCasasNuevas).
/// · Escalinata Real en el talud central (sin tocar el terreno), muro bajo con jardineras y balaustrada.
/// · Fuente de la Plaza del Mercado, mercado, terraza de la taberna, heráldica, farolas y rótulos sin letras.
///
/// Canon que respeta: nada rural dentro de la muralla; sin iglesias (AT19–21), portales ni pedestales; ninguna
/// torre suelta (las de la muralla van unidas a ella; el cuerpo de guardia AT25 y la casa de la torre verde AT27
/// son casas con torreta, dentro de su manzana y junto a la calle); sin luces con llama de partículas
/// (Light03_e–h, Light02_b, Light05_b01); sin carteles con letras; la Plaza Real (arena del Demonio 2), el
/// centro de la Plaza del Mercado y la escalinata quedan libres para los eventos del capítulo 3.
public static partial class VestidoDelMundo
{
    private const string NombreDelReino = "Reino";

    private const string PiezaEscalinata = FK + "Main Structures/Decoration/Stairs01.prefab";
    /// Lienzo de sillería del pack (el de la muralla): a escala baja hace de muro bajo y de zócalo.
    private const string PiezaMuroBajo = FK + "Main Structures/Wall/Wall02.prefab";
    private const string PiezaPretil = FK + "Main Structures/Railing/Railing06_a01.prefab";
    private const string PiezaPilarDePretil = FK + "Main Structures/Railing/Railing06_a03.prefab";
    private const string PiezaJardineraDePiedra = FK + "Vegetation/Plant01_c01.prefab";
    private const string PiezaFarolaDeDosBrazos = FK + "Props/Lighting/Light03_a02.prefab";
    private const string PiezaFarolaDeTresBrazos = FK + "Props/Lighting/Light03_a03.prefab";
    private const string PiezaBrocal = FK + "Main Structures/Decoration/Well01_a01.prefab";
    private const string PiezaAsiento = FK + "Props/Furniture/Chair/Chair02_a01.prefab";
    private const string CarpetaDePlacas = FK + "Props/Sign/";
    private const string MaterialAguaDeLaFuente = "Assets/Scenes/Worlds/MainWorld_data/Recursos/AguaRio.mat";

    // ── Casas nuevas ─────────────────────────────────────────────────────────────────────────

    /// Casas de la capital, comprobadas contra el terreno (desnivel bajo la huella de 0,65 m como mucho), la
    /// muralla nueva y sus torres, las casas del generador que se quedan (ya giradas), las calles con su ancho,
    /// las plazas y las zonas libres. Todas son edificios completos del pack FK; las de la ciudad alta, de tres
    /// plantas y tejado azul como el castillo. La puerta mira a su calle.
    private static CasaNueva[] CasasNuevasDelReino() => new[]
    {
        // Ciudad baja: la taberna da su terraza a la Plaza del Mercado; oficios a lo largo de la Calle de la
        // Taberna y de la Calle Mayor; la fila del pie del talud lo tapa con casas de 9,8 m (sus tejados asoman
        // sobre la cresta de la terraza alta).
        new CasaNueva("BuildingAT18", -30.6f, 259.0f, 90f, "Taberna de la plaza"),
        new CasaNueva("BuildingAT17", -42.2f, 256.6f, 90f, "Casa del sastre"),
        new CasaNueva("BuildingAT53", -52.0f, 251.7f, 0f, "Casa del platero"),
        new CasaNueva("BuildingAT47", -62.0f, 251.9f, 0f, "Casa del carnicero"),
        new CasaNueva("BuildingAT53", -78.0f, 251.5f, 0f, "Casa del herrador"),
        new CasaNueva("BuildingAT47", -62.3f, 267.0f, 180f, "Casa del pescadero"),
        new CasaNueva("BuildingAT17", 28.9f, 266.0f, 180f, "Panadería de la plaza"),
        new CasaNueva("BuildingAT17", 47.5f, 265.9f, 180f, "Casa del cambista"),
        new CasaNueva("BuildingAT09", 54.5f, 250.1f, 0f, "Bodega de la Calle Mayor"),
        new CasaNueva("BuildingAT25", 68.0f, 251.0f, 90f, "Cuerpo de guardia de la Puerta Real"),
        new CasaNueva("BuildingAT48", 69.8f, 271.6f, 180f, "Posada del Camino Real"),

        // Ciudad alta, Plaza Real: palacio y Archivo Real enfrentados (fachadas a 3,7–4,9 m del borde de la
        // plaza, fuera de la arena del Demonio 2) y una casa noble en la esquina sureste.
        new CasaNueva("BuildingAT27", -37.0f, 309.0f, 90f, "Palacio de poniente"),
        new CasaNueva("BuildingAT31", 37.0f, 311.0f, 270f, "Archivo Real"),
        new CasaNueva("BuildingAT46", 34.6f, 288.8f, 0f),

        // Vía Real: la casa de la torre verde corona la rampa este (se ve al subir desde la puerta); hileras a
        // los dos lados de la curva, con juntas de 1,5 m.
        new CasaNueva("BuildingAT27", 71.5f, 286.6f, 90f, "Casa de la torre verde"),
        new CasaNueva("BuildingAT55", 46.2f, 290.2f, 0f),
        new CasaNueva("BuildingAT54", 58.9f, 290.9f, 0f),
        new CasaNueva("BuildingAT46", 46.0f, 308.8f, 180f),
        new CasaNueva("BuildingAT54", 64.53f, 308.44f, 190f),
        new CasaNueva("BuildingAT55", 79.71f, 306.14f, 206.8f),
        new CasaNueva("BuildingAT46", 90.5f, 299.5f, 225f),

        // Calle de Poniente: hilera sur con las espaldas sobre la cresta (se ven desde la ciudad baja) e hilera
        // norte, del palacio a la rampa de poniente.
        new CasaNueva("BuildingAT46", -46.9f, 289.8f, 0f),
        new CasaNueva("BuildingAT55", -58.0f, 290.9f, 0f),
        new CasaNueva("BuildingAT54", -71.0f, 291.6f, 0f),
        new CasaNueva("BuildingAT17", -46.7f, 307.9f, 180f),
        new CasaNueva("BuildingAT54", -63.4f, 308.0f, 180f),
        new CasaNueva("BuildingAT55", -76.0f, 309.1f, 180f),
        new CasaNueva("BuildingAT46", -87.8f, 308.4f, 180f),
        new CasaNueva("BuildingAT17", -96.6f, 308.6f, 180f),

        // Flancos del castillo: cuartel de la Guardia Real ante el patio de armas y casas nobles hacia los
        // jardines de palacio.
        new CasaNueva(FK + "Main Structures/Wall/Stronghold02.prefab", -42.0f, 337.0f, 180f, "Cuartel de la Guardia Real"),
        new CasaNueva("BuildingAT53", -46.0f, 326.0f, 270f),
        new CasaNueva("BuildingAT54", -68.0f, 325.0f, 90f),
        new CasaNueva("BuildingAT55", -68.0f, 340.0f, 90f),
        new CasaNueva("BuildingAT46", 46.0f, 324.0f, 90f),
        new CasaNueva("BuildingAT53", 62.0f, 324.0f, 270f),
        new CasaNueva("BuildingAT55", 72.6f, 325.0f, 270f),
        new CasaNueva("BuildingAT54", 72.4f, 339.0f, 270f),
        // Tramo norte de la Calle del Archivo, con fachadas a los dos lados como el de la del Cuartel.
        new CasaNueva("BuildingAT54", 46.0f, 336.8f, 90f),
        new CasaNueva("BuildingAT46", 62.0f, 336.0f, 270f),
    };

    // ── Zonas libres ─────────────────────────────────────────────────────────────────────────

    /// Recorridos del capítulo 3 que no quedan dentro de una plaza reservada (las plazas, la escalinata y la
    /// puerta están en ZonasLibres).
    static partial void ZonasDeLaCapital(List<Zona> libres)
    {
        // Escolta del guardia: de la terraza de la taberna al pie de la escalinata; sigue por la escalinata, la
        // Plaza Real y la explanada hasta la puerta del castillo (83 m, a la medida de sus 11 frases).
        libres.Add(new Zona("Escolta del guardia: terraza de la taberna → escalinata", -20.5f, 260.2f, 2.5f));
        libres.Add(new Zona("Escolta del guardia: terraza de la taberna → escalinata", -14.5f, 262.1f, 2.5f));
        libres.Add(new Zona("Escolta del guardia: terraza de la taberna → escalinata", -8.5f, 264.0f, 2.5f));
        // Persecución de Estela: sitios de los civiles que caen fuera de las plazas reservadas.
        libres.Add(new Zona("Persecución: civil de la Calle Mayor", 52f, 258f, 2f));
        libres.Add(new Zona("Persecución: civil de la Plaza de la Puerta Real", 84f, 254f, 2f));
        libres.Add(new Zona("Persecución: civil de la Vía Real", 56f, 300f, 2f));
    }

    // ── Retirada de lo que no encaja en la capital ───────────────────────────────────────────

    private const string MotivoMolino = "molino: en la capital no cabe nada de aldea (canon: sin molinos fuera del puerto)";
    private const string MotivoChoza = "casita de paja de aldea";
    private const string MotivoHerreria = "herrería de aldea pegada al castillo";
    private const string MotivoVinatero = "casa de vinatero de aire rural en la ciudad alta";
    private const string MotivoTaberna = "taberna vieja (le falta una malla y la calle la atravesaba): la sustituye la taberna de la plaza";
    private const string MotivoToldo = "toldo sin postes tirado en el suelo de la plaza";
    private const string MotivoPozo = "pozo de otro estilo (RPG Tiny): en su sitio va la fuente";
    private const string MotivoFarol = "farol bajo dentro de la arena del Demonio 2";
    private const string MotivoMobiliario = "mesa o asiento en medio de la Plaza del Mercado: van a la terraza de la taberna";
    private const string MotivoHuerto = "huerto del Reino: lo atraviesa la muralla nueva y es rural";
    private const string MotivoMaceta = "maceta de una casa retirada o que quedaría en una calle, una plaza o una casa nueva";

    /// Lo que se retira, por prefab y posición del pivote en planta (piezas del generador del mapa).
    private static readonly (string prefab, float x, float z, string motivo)[] RetiradosDelReino =
    {
        ("BuildingAT23", 69.96f, 248.99f, MotivoMolino),
        ("BuildingAT23", 72.01f, 285.96f, MotivoMolino),
        ("BuildingAT23", -35.99f, 315.96f, MotivoMolino),
        ("BuildingAT01", -72.73f, 284.18f, MotivoChoza),
        ("BuildingAT01", -35.27f, 338.82f, MotivoChoza),
        ("BuildingAT01", 72.73f, 317.82f, MotivoChoza),
        ("BuildingAT01", 68.18f, 273.73f, MotivoChoza),
        ("BuildingAT12", -70.77f, 336.95f, MotivoHerreria),
        ("BuildingAT12", 37.23f, 315.95f, MotivoHerreria),
        ("BuildingAT07", -73.36f, 315.82f, MotivoVinatero),
        ("BuildingAT07", 34.64f, 285.82f, MotivoVinatero),
        ("BuildingAT07", 73.36f, 337.18f, MotivoVinatero),
        ("BuildingAT10", -38.52f, 263.04f, MotivoTaberna),
        ("Tent02_a01", -14f, 247f, MotivoToldo),
        ("Tent02_a01", 0f, 247f, MotivoToldo),
        ("Tent02_a01", 14f, 247f, MotivoToldo),
        ("Well01", 15f, 263f, MotivoPozo),
        ("Light01_a01", -16f, 290f, MotivoFarol),
        ("Light01_a01", -16f, 308f, MotivoFarol),
        ("Light01_a01", 16f, 290f, MotivoFarol),
        ("Light01_a01", 16f, 308f, MotivoFarol),
        ("Table01_a01", -12f, 256f, MotivoMobiliario),
        ("Table01_a01", -12f, 264f, MotivoMobiliario),
        ("Chair02_a01", -13.8f, 256f, MotivoMobiliario),
        ("Chair02_a01", -10.2f, 256f, MotivoMobiliario),
        ("Chair02_a01", -13.8f, 264f, MotivoMobiliario),
        ("Chair02_a01", -10.2f, 264f, MotivoMobiliario),
        // Macetas «junto a vivienda» del generador: las de casas retiradas y las que, al girar con su casa,
        // caerían en una casa nueva (la de merc.13 iría al solar de la casa del herrador). Las otras cinco se
        // quedan y giran con su casa.
        ("Flowerpot01_b03", 43.53f, 286f, MotivoMaceta),
        ("Flowerpot01_b03", -30.31f, 337f, MotivoMaceta),
        ("Flowerpot01_b03", 18.4f, 247f, MotivoMaceta),
        ("Flowerpot01_b03", 4.4f, 247f, MotivoMaceta),
        ("Flowerpot01_b03", 75.17f, 249f, MotivoMaceta),
        ("Flowerpot01_b03", -66.31f, 286f, MotivoMaceta),
        ("Flowerpot01_b03", 17.71f, 263f, MotivoMaceta),
        ("Flowerpot01_b03", -33.53f, 260f, MotivoMaceta),
        ("Flowerpot01_b03", 76.13f, 273f, MotivoMaceta),
        ("Flowerpot01_b03", -9.6f, 247f, MotivoMaceta),
        ("Flowerpot01_b03", 79.53f, 337f, MotivoMaceta),
        ("Flowerpot01_b03", 77.69f, 316f, MotivoMaceta),
        ("Flowerpot01_b03", -29.43f, 316f, MotivoMaceta),
        ("Flowerpot01_b03", 40.86f, 316f, MotivoMaceta),
        ("Flowerpot01_b03", -65.82f, 249f, MotivoMaceta),
        ("Flowerpot01_b03", -67.14f, 337f, MotivoMaceta),
        ("Flowerpot01_b03", -64.47f, 316f, MotivoMaceta),
        ("Flowerpot01_b03", 78.57f, 286f, MotivoMaceta),
    };

    private const string NombreDelHuertoDelReino = "Huerto del Reino — terraza baja";

    static partial void RetirarParaLaCapital(Scene escena, Obra o)
    {
        int faltan = 0;
        foreach (var (prefab, x, z, motivo) in RetiradosDelReino)
        {
            List<Transform> encontrados = BuscarPorPrefabYPosicion(escena, prefab, new Vector2(x, z), 1f);
            if (encontrados.Count == 0)
            {
                if (faltan++ < 6) o.Informe.Add($"  · no encuentro {prefab} en ({x:0.##}, {z:0.##}): no se retira.");
                continue;
            }
            Retirar(escena, encontrados[0], motivo, o);
        }
        if (faltan > 6) o.Informe.Add($"  · … y {faltan - 6} piezas más que no aparecen.");

        // El huerto: su grupo (cercas y cultivos) y el marcador del mismo nombre que cuelga de WORLD.
        Transform vida = BuscarGrupo(escena, GrupoVidaDeLosPueblos);
        Retirar(escena, vida != null ? vida.Find(NombreDelHuertoDelReino) : null, MotivoHuerto, o);
        Retirar(escena, BuscarGrupo(escena, NombreDelHuertoDelReino), MotivoHuerto, o);
    }

    // ── Lo que se pone ───────────────────────────────────────────────────────────────────────

    static partial void PonerCapital(Obra o)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Pueblo reino = Reino();
        Transform grupo = Grupo(o.Raiz, reino.Nombre);
        List<Puerta> puertas = PuertasDelPueblo(escena, reino, CasasGiradas(escena));
        RegistrarCorredores(o, reino, puertas);
        TamanosDeSerie.Clear();
        int antes = o.Puestas;

        // Primero la obra y las composiciones; las farolas de las calles y los adornos de las puertas, al final,
        // se adaptan a ellas.
        var partes = new List<string>();
        void Parte(string nombre, System.Action poner)
        {
            int n = o.Puestas;
            poner();
            partes.Add($"{nombre} {o.Puestas - n}");
        }
        Parte("escalinata, muro bajo y balaustrada", () => PonerEscalinataReal(o, Grupo(grupo, "Escalinata Real")));
        Parte("fuente", () => PonerFuente(o, Grupo(grupo, "Fuente de la Plaza del Mercado")));
        Parte("mercado", () => PonerMercado(o, Grupo(grupo, "Mercado")));
        Parte("terraza de la taberna", () => PonerTerrazaDeLaTaberna(o, Grupo(grupo, "Terraza de la taberna")));
        Parte("estandartes y farolas de plazas, escalinata, puerta y Vía Real", () => PonerHeraldicaYLuces(o, grupo, reino));
        Parte("entrada del castillo, plazoleta y patio de armas", () => PonerEscenasDeLaCiudadAlta(o, grupo));
        Parte("rótulos (dos placas cada uno)", () => PonerRotulos(o, Grupo(grupo, "Rótulos de los oficios"), reino));
        Parte("adornos de puertas", () => AdornarPuertas(o, Grupo(grupo, "Puertas"), puertas, reino.Semilla, ciudad: true));
        Parte("farolas de calle", () => FarolasEnCalles(o, Grupo(grupo, "Farolas"), reino));
        o.Informe.Add($"{reino.Nombre}: {o.Puestas - antes} piezas de la capital — {string.Join(", ", partes)}.");
        InformarDeLasCasasDeLaCapital(o, escena, reino);
    }

    /// Qué casas de la capital no han cabido, para revisarlas a mano: los edificios públicos, por su nombre.
    private static void InformarDeLasCasasDeLaCapital(Obra o, Scene escena, Pueblo reino)
    {
        Transform nuevas = BuscarCasasNuevas(escena, reino);
        var faltan = new List<string>();
        int puestas = 0;
        for (int i = 0; i < reino.Nuevas.Length; i++)
        {
            string nombre = NombreCasaNueva(i + 1, reino.Nuevas[i]);
            if (nuevas != null && nuevas.Find(nombre) != null) puestas++;
            else faltan.Add(nombre);
        }
        o.Informe.Add($"{reino.Nombre}: {puestas} de {reino.Nuevas.Length} edificios de la capital en pie" +
                      (faltan.Count > 0 ? $"; no han cabido (ver los descartes): {string.Join(", ", faltan)}." : "."));
    }

    /// Pieza de una composición de la capital: va donde dice el plano aunque caiga en una plaza reservada (la
    /// composición ya deja libre lo que hace falta) o sobre la calle; sí comprueba solapes y choques.
    private static GameObject PonFijo(Obra o, Transform g, string prefab, string nombre, Vector2 pos, float rumbo,
        float tamano = 1f, Medida medida = Medida.Escala, bool sinObstaculo = false, bool solapePropio = false) =>
        Poner(o, g, new Pieza
        {
            Prefab = prefab, Nombre = nombre, Pos = pos, Rumbo = rumbo, Tamano = tamano, Medida = medida,
            IgnorarZonas = true, PermitirCamino = true, PermitirSolapePropio = solapePropio, Holgura = 0.05f,
            DesnivelMax = 0.9f, Hundir = 0.04f, SinObstaculo = sinObstaculo,
        });

    /// Tamaño visible (ancho en X local, alto, fondo en Z local) de cada prefab a su escala de serie, medido una
    /// vez por ejecución.
    private static readonly Dictionary<string, Vector3> TamanosDeSerie = new();

    private static Vector3 TamanoDeSerie(Obra o, string prefab)
    {
        if (TamanosDeSerie.TryGetValue(prefab, out Vector3 t)) return t;
        GameObject fuente = CargarPrefab(o, prefab);
        t = Vector3.zero;
        if (fuente != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fuente, o.Raiz);
            go.transform.SetPositionAndRotation(Vector3.zero, fuente.transform.localRotation);
            go.transform.localScale = fuente.transform.localScale;
            t = LimitesVisibles(go).size;
            Object.DestroyImmediate(go);
        }
        TamanosDeSerie[prefab] = t;
        return t;
    }

    /// Pieza de obra con medidas por ejes (escalinata, muro bajo, zócalos, pilón): Poner solo escala por igual.
    /// «medidas» es el tamaño visible buscado (ancho en X local, alto, fondo en Z local; 0 deja el de serie en
    /// ese eje). La base va a «cota» y la huella se centra en «pos»: no se apoya en el terreno, porque salva un
    /// desnivel a propósito. Sí comprueba solapes con el resto del vestido y choques con lo que ya había.
    private static GameObject PonerPiezaDeObra(Obra o, Transform g, string prefab, string nombre, Vector2 pos, float rumbo,
        Vector3 medidas, float cota, bool sinObstaculo, bool solapePropio)
    {
        var p = new Pieza { Prefab = prefab, Nombre = nombre, Pos = pos, Rumbo = rumbo };
        GameObject fuente = CargarPrefab(o, prefab);
        Vector3 serie = TamanoDeSerie(o, prefab);
        if (fuente == null || serie.x <= 0f || serie.y <= 0f || serie.z <= 0f) { o.Descartar("falta el prefab o no tiene mallas visibles", p); return null; }

        var escala = new Vector3(
            medidas.x > 0f ? medidas.x / serie.x : 1f,
            medidas.y > 0f ? medidas.y / serie.y : 1f,
            medidas.z > 0f ? medidas.z / serie.z : 1f);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fuente, g);
        go.name = nombre;
        Transform t = go.transform;
        t.rotation = Quaternion.Euler(0f, rumbo, 0f) * fuente.transform.localRotation;
        t.localScale = Vector3.Scale(fuente.transform.localScale, escala);
        t.position = new Vector3(pos.x, 0f, pos.y);
        Bounds b = LimitesVisibles(go);
        t.position += new Vector3(pos.x - b.center.x, cota - b.min.y, pos.y - b.center.z);
        b = LimitesVisibles(go);

        Huella huella = HuellaOrientada(go, rumbo);
        if (!solapePropio && SolapaPropio(o, huella))
        {
            Object.DestroyImmediate(go);
            o.Descartar("se pisa con otra pieza del vestido", p);
            return null;
        }
        if (ChocaConLoQueHabia(o, b, 0.05f))
        {
            Object.DestroyImmediate(go);
            o.Descartar("choca con algo que ya estaba en la escena", p);
            return null;
        }
        if (sinObstaculo) o.SinObstaculo.Add(t);
        o.Ocupado.Add(huella);
        o.Puestas++;
        return go;
    }

    /// Pieza colgada de una fachada: posición y giro exactos, sin apoyo en el terreno ni comprobaciones (no tiene
    /// colisor ni ocupa suelo).
    private static GameObject PonerColgado(Obra o, Transform g, string prefab, string nombre, Vector3 centro, Quaternion giro, float escala)
    {
        GameObject fuente = CargarPrefab(o, prefab);
        if (fuente == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fuente, g);
        go.name = nombre;
        go.transform.rotation = giro * fuente.transform.localRotation;
        go.transform.localScale = fuente.transform.localScale * escala;
        go.transform.position = centro;
        go.transform.position += centro - LimitesVisibles(go).center;
        o.Puestas++;
        return go;
    }

    // ── Escalinata Real (talud central, opción A: sin tocar el terreno) ───────────────────────

    private const float EscalinataPie = 268.8f, EscalinataCresta = 283f, EscalinataAncho = 12f;
    private const int TramosDeEscalinata = 3;

    /// Tres tramos de Stairs01 de la Plaza del Mercado (y 104) a la Plaza Real (y 112): 12 m de ancho y 29° de
    /// pendiente. Debajo de los tramos que quedan en el aire, zócalos de sillería alineados con su costado; al pie
    /// del talud, a cada lado, un muro bajo con jardineras; en la cresta, balaustrada a lo largo de la Plaza Real.
    /// Escalones y zócalos van sin obstáculo de navegación: por aquí pasa la escolta del guardia.
    private static void PonerEscalinataReal(Obra o, Transform g)
    {
        // A ras del pavimento abajo y arriba (VestidoDelMundo.Pavimento lo pone unos 4 cm sobre el terreno).
        float abajo = o.Suelo.Altura(0f, EscalinataPie) + 0.04f;
        float arriba = o.Suelo.Altura(0f, EscalinataCresta + 0.6f) + 0.04f;
        float largo = (EscalinataCresta - EscalinataPie) / TramosDeEscalinata, subida = (arriba - abajo) / TramosDeEscalinata;
        for (int k = 0; k < TramosDeEscalinata; k++)
        {
            float cota = abajo + subida * k, z0 = EscalinataPie + largo * k;
            // Stairs01 sube hacia su −Z local: con rumbo 180 sube hacia el norte.
            PonerPiezaDeObra(o, g, PiezaEscalinata, $"Tramo {k + 1} de la Escalinata Real", new Vector2(0f, z0 + largo * 0.5f), 180f,
                new Vector3(EscalinataAncho, subida, largo), cota, sinObstaculo: true, solapePropio: true);
            if (k == 0) continue;
            foreach (float lado in new[] { -1f, 1f })
                ZocaloBajoElTramo(o, g, lado * (EscalinataAncho * 0.5f - 0.45f), z0, largo, cota, k);
        }

        // Muro bajo al pie del talud (3,4 m sobre la plaza) con cuatro jardineras delante a cada lado.
        const float zMuro = 272.25f, gruesoMuro = 1.2f, xFinMuro = 24f;
        float xIniMuro = EscalinataAncho * 0.5f + 1.1f;
        foreach (float lado in new[] { -1f, 1f })
        {
            float suelo = float.MaxValue;
            for (float x = xIniMuro; x <= xFinMuro; x += 1f)
                suelo = Mathf.Min(suelo, Mathf.Min(o.Suelo.Altura(lado * x, zMuro - gruesoMuro * 0.5f), o.Suelo.Altura(lado * x, zMuro + gruesoMuro * 0.5f)));
            float baseMuro = suelo - 0.15f;
            PonerPiezaDeObra(o, g, PiezaMuroBajo, "Muro bajo del talud", new Vector2(lado * (xIniMuro + xFinMuro) * 0.5f, zMuro), 0f,
                new Vector3(xFinMuro - xIniMuro, abajo + 3.4f - baseMuro, gruesoMuro), baseMuro, sinObstaculo: false, solapePropio: false);
            for (float x = xIniMuro + 2.4f; x < xFinMuro - 0.5f; x += 4.5f)
                PonFijo(o, g, PiezaJardineraDePiedra, "Jardinera del muro bajo", new Vector2(lado * x, 270f), 0f);
        }

        // Balaustrada en la cresta, a cada lado del desembarco: pilar, dos pretiles, pilar, dos pretiles, pilar,
        // pretil y pilar (de x ±7,2 a ±28,2, donde empiezan las casas de la esquina).
        const float zBalaustrada = 283.4f;
        bool[] serie = { true, false, false, true, false, false, true, false, true };
        float pilar = TamanoDeSerie(o, PiezaPilarDePretil).x, pretil = TamanoDeSerie(o, PiezaPretil).x;
        if (pilar <= 0f || pretil <= 0f) return;
        foreach (float lado in new[] { -1f, 1f })
        {
            float x = EscalinataAncho * 0.5f + 1.2f;
            foreach (bool esPilar in serie)
            {
                float ancho = esPilar ? pilar : pretil;
                Poner(o, g, new Pieza
                {
                    Prefab = esPilar ? PiezaPilarDePretil : PiezaPretil, Nombre = esPilar ? "Pilar de la balaustrada" : "Pretil de la balaustrada",
                    Pos = new Vector2(lado * (x + ancho * 0.5f), zBalaustrada), Rumbo = 0f,
                    IgnorarZonas = true, PermitirCamino = true, PermitirSolapePropio = true, Holgura = 0.02f, DesnivelMax = 0.8f, Hundir = 0.06f,
                });
                x += ancho;
            }
        }
    }

    /// Zócalo de sillería bajo el costado de un tramo de escalera, donde el terreno queda por debajo de la base
    /// del tramo: tapa el hueco que se vería desde los lados. Se omite si el hueco no llega a 0,3 m.
    private static void ZocaloBajoElTramo(Obra o, Transform g, float x, float z0, float largo, float cotaTramo, int tramo)
    {
        const float grueso = 0.9f;
        float zIni = float.NaN, zFin = z0, minimo = float.MaxValue;
        for (float z = z0; z <= z0 + largo + 1e-3f; z += 0.25f)
        {
            float y = Mathf.Min(o.Suelo.Altura(x - grueso * 0.5f, z), o.Suelo.Altura(x + grueso * 0.5f, z));
            if (y >= cotaTramo - 0.05f) continue;
            if (float.IsNaN(zIni)) zIni = z;
            zFin = z;
            minimo = Mathf.Min(minimo, y);
        }
        if (float.IsNaN(zIni) || cotaTramo - minimo < 0.3f) return;
        zFin = Mathf.Min(zFin + 0.4f, z0 + largo);
        float baseZocalo = minimo - 0.3f;
        // Wall02 corre a lo largo de su X local: con rumbo 90 va de norte a sur, bajo el costado del tramo.
        PonerPiezaDeObra(o, g, PiezaMuroBajo, $"Zócalo bajo el tramo {tramo + 1}", new Vector2(x, (zIni + zFin) * 0.5f), 90f,
            new Vector3(zFin - zIni, cotaTramo - baseZocalo, grueso), baseZocalo, sinObstaculo: true, solapePropio: true);
    }

    // ── Fuente de la Plaza del Mercado ───────────────────────────────────────────────────────

    private static readonly Vector2 SitioDeLaFuente = new Vector2(15f, 263f);

    /// Fuente en el sitio del pozo que se retira: el brocal Well01_a01 ensanchado a 5,4 m y bajo (0,75 m) hace de
    /// pilón, y dentro, una lámina de agua. En los packs no hay fuentes ni estatuas, y un pedestal en el centro
    /// se leería como los de las Ruinas del Libro.
    private static void PonerFuente(Obra o, Transform g)
    {
        const float ancho = 5.4f, alto = 0.75f;
        // Proporciones del brocal FK: el hueco tiene el 47 % del ancho total y el agua queda al 80 % del alto.
        const float radioDelHueco = 0.47f * ancho * 0.5f, alturaDelAgua = 0.8f * alto;
        float suelo = float.MaxValue;
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 0.25f, r = i == 8 ? 0f : ancho * 0.5f;
            suelo = Mathf.Min(suelo, o.Suelo.Altura(SitioDeLaFuente.x + Mathf.Cos(a) * r, SitioDeLaFuente.y + Mathf.Sin(a) * r));
        }
        float cota = suelo - 0.05f;
        // Se ensancha igual en X y en Z (el brocal es redondo aunque su caja no sea cuadrada).
        Vector3 serie = TamanoDeSerie(o, PiezaBrocal);
        float fondo = serie.x > 0f ? ancho * serie.z / serie.x : ancho;
        GameObject pilon = PonerPiezaDeObra(o, g, PiezaBrocal, "Pilón de la fuente", SitioDeLaFuente, 0f, new Vector3(ancho, alto, fondo), cota,
            sinObstaculo: false, solapePropio: false);
        if (pilon == null) return;

        var agua = AssetDatabase.LoadAssetAtPath<Material>(MaterialAguaDeLaFuente);
        if (agua == null) { o.Informe.Add($"  ! falta {MaterialAguaDeLaFuente}: la fuente queda sin agua."); return; }
        Mesh disco = DiscoDeLaFuente(radioDelHueco - 0.03f, 32);
        GuardarMallaGenerada(disco, "Agua de la fuente de la Plaza del Mercado");
        var lamina = new GameObject("Agua de la fuente");
        lamina.transform.SetParent(g, false);
        lamina.transform.position = new Vector3(SitioDeLaFuente.x, cota + alturaDelAgua, SitioDeLaFuente.y);
        lamina.AddComponent<MeshFilter>().sharedMesh = disco;
        var render = lamina.AddComponent<MeshRenderer>();
        render.sharedMaterial = agua;
        render.shadowCastingMode = ShadowCastingMode.Off;
        o.SinObstaculo.Add(lamina.transform);
        o.Puestas++;
    }

    /// Disco horizontal de radio dado, con la cara hacia arriba y UV de 0 a 1.
    private static Mesh DiscoDeLaFuente(float radio, int lados)
    {
        var vertices = new Vector3[lados + 1];
        var uv = new Vector2[lados + 1];
        var triangulos = new int[lados * 3];
        uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < lados; i++)
        {
            float a = i * Mathf.PI * 2f / lados;
            vertices[i + 1] = new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio);
            uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
            triangulos[i * 3] = 0;
            triangulos[i * 3 + 1] = i + 2 > lados ? 1 : i + 2;
            triangulos[i * 3 + 2] = i + 1;
        }
        var malla = new Mesh { name = "Agua de la fuente", vertices = vertices, uv = uv, triangles = triangulos };
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    // ── Mercado y terraza de la taberna ──────────────────────────────────────────────────────

    /// Puestos del mercado: en el borde sur de la Plaza del Mercado, de espaldas a la muralla y abiertos a la
    /// plaza, dejando libre el eje de la escalinata y el centro (encuentro y persecución).
    private static readonly (string prefab, float x, float z, float frente)[] PuestosDelMercado =
    {
        (PrefabsEdificios + "BuildingAT36.prefab", -15f, 248.9f, 0f),     // básico
        (PrefabsEdificios + "BuildingAT40.prefab", -8.5f, 249.0f, 0f),    // carnicero
        (PrefabsEdificios + "BuildingAT37.prefab", 8.5f, 249.2f, 0f),     // bebidas
        (PrefabsEdificios + "BuildingAT41.prefab", 15.5f, 249.8f, 0f),    // pescado
        (PrefabsEdificios + "BuildingAT42.prefab", 21.4f, 249.9f, 0f),    // pescado
    };

    private static void PonerMercado(Obra o, Transform g)
    {
        for (int i = 0; i < PuestosDelMercado.Length; i++)
        {
            var (prefab, x, z, frente) = PuestosDelMercado[i];
            GameObject puesto = PonFijo(o, g, prefab, "Puesto del mercado", new Vector2(x, z), frente);
            if (puesto == null) continue;
            Vector2 f = FrenteDe(frente), l = new Vector2(f.y, -f.x);
            // Género al costado del puesto, fuera de su huella, y un barril cada tres puestos al otro lado.
            float costado = Mathf.Max(3.2f, HuellaOrientada(puesto, frente).MedioX + 0.7f);
            PonFijo(o, g, i % 2 == 0 ? Caja : Sacos, "Género del puesto", new Vector2(x, z) + l * costado, frente + 20f);
            if (i % 3 == 0) PonFijo(o, g, Barril, "Barril del puesto", new Vector2(x, z) - l * costado, 0f);
        }
        PonFijo(o, g, CarroToldo, "Carro-puesto del mercado", new Vector2(-20.8f, 266.5f), 90f);
        PonFijo(o, g, Carreta, "Carreta de descarga de la posada", new Vector2(57.5f, 263.5f), 90f);
    }

    /// Terraza de la taberna: la AT18 trae la suya, con toldo, hacia la plaza; delante, al borde de la plaza y
    /// fuera del paso de la escolta, dos mesas con sus asientos, y barriles junto a la pared sur. Van sin
    /// obstáculo de navegación: por aquí sale la escolta del guardia.
    private static void PonerTerrazaDeLaTaberna(Obra o, Transform g)
    {
        foreach (float z in new[] { 255.0f, 264.2f })
        {
            PonFijo(o, g, Mesa, "Mesa de la terraza", new Vector2(-21.6f, z), 90f, sinObstaculo: true);
            PonFijo(o, g, PiezaAsiento, "Asiento de la terraza", new Vector2(-20.55f, z), 270f, sinObstaculo: true);
            PonFijo(o, g, PiezaAsiento, "Asiento de la terraza", new Vector2(-22.65f, z), 90f, sinObstaculo: true);
        }
        PonFijo(o, g, Barril, "Barril de la taberna", new Vector2(-22.6f, 253.4f), 0f, sinObstaculo: true);
        PonFijo(o, g, Barril, "Barril de la taberna", new Vector2(-23.4f, 253.9f), 40f, sinObstaculo: true);
    }

    // ── Heráldica y luces ────────────────────────────────────────────────────────────────────

    /// Estandartes azul y oro (Flag01_a01) y farolas de la capital. Plaza Real: ocho estandartes en los lados,
    /// fuera de las bocas de la Vía Real y de la Calle de Poniente, y farolas de tres brazos en las esquinas.
    /// Escalinata: farolas de dos brazos y estandartes arriba y abajo. Puerta Real: un estandarte a cada lado del
    /// paso. Vía Real: un estandarte cada 24 m en la acera, alternando lado, a medio camino entre farolas.
    private static void PonerHeraldicaYLuces(Obra o, Transform grupo, Pueblo reino)
    {
        Transform plaza = Grupo(grupo, "Plaza Real");
        foreach (float s in new[] { -1f, 1f })
        {
            foreach (float z in new[] { 288.5f, 292.5f, 305.5f, 309.5f })
                PonFijo(o, plaza, Estandarte, "Estandarte de la Plaza Real", new Vector2(s * 27.3f, z), s > 0f ? -90f : 90f, 4.6f, Medida.Alto);
            foreach (float z in new[] { 285f, 311f })
                PonFijo(o, plaza, PiezaFarolaDeTresBrazos, "Farola de la Plaza Real", new Vector2(s * 26.8f, z), 0f);
        }

        Transform escalinata = Grupo(grupo, "Escalinata Real");
        foreach (float s in new[] { -1f, 1f })
        {
            PonFijo(o, escalinata, PiezaFarolaDeDosBrazos, "Farola del pie de la escalinata", new Vector2(s * 8.3f, 267.6f), 0f);
            PonFijo(o, escalinata, Estandarte, "Estandarte del pie de la escalinata", new Vector2(s * 7.3f, 270.3f), 0f, 4.6f, Medida.Alto);
            PonFijo(o, escalinata, PiezaFarolaDeDosBrazos, "Farola de lo alto de la escalinata", new Vector2(s * 7.8f, 284.5f), 0f);
            PonFijo(o, escalinata, Estandarte, "Estandarte de lo alto de la escalinata", new Vector2(s * 10.2f, 284.5f), 0f, 4.6f, Medida.Alto);
        }

        Transform puerta = Grupo(grupo, "Puerta Real");
        foreach (float x in new[] { 88.3f, 97.4f })
            PonFijo(o, puerta, Estandarte, "Estandarte de la Puerta Real", new Vector2(x, 246.5f), 180f, 5f, Medida.Alto);

        // Vía Real (calle 1 de Reino()): estandartes a 12 + 24·n m de su arranque; las farolas (FarolasEnCalles)
        // van cada 12 m empezando a 6, así quedan a medio camino.
        Vector2[] via = reino.Calles[1];
        float desplazamiento = reino.AnchoDeCalle(1) * 0.5f - DentroDeLaAcera, recorrido = 0f, siguiente = 12f;
        int n = 0;
        Transform grupoVia = Grupo(grupo, "Vía Real");
        for (int j = 0; j < via.Length - 1; j++)
        {
            Vector2 a = via[j], b = via[j + 1];
            float largo = Vector2.Distance(a, b);
            if (largo < 0.01f) continue;
            Vector2 dir = (b - a) / largo, normal = new Vector2(-dir.y, dir.x);
            while (siguiente <= recorrido + largo)
            {
                float lado = n++ % 2 == 0 ? 1f : -1f;
                Vector2 pos = a + dir * (siguiente - recorrido) + normal * lado * desplazamiento;
                if (!EnCorredorSalvo(o, pos, 0.4f, via))
                    Pon(o, grupoVia, Estandarte, "Estandarte de la Vía Real", pos, Rumbo(dir) + (lado > 0f ? 90f : -90f), 4.6f, Medida.Alto,
                        camino: true, corredor: false, holgura: 0.05f);
                siguiente += 24f;
            }
            recorrido += largo;
        }
    }

    /// Escenas de la ciudad alta que ya ponía el vestido: armaduras y estandartes ante la puerta del castillo
    /// (fuera del eje plaza → puerta y del acceso despejado), plazoleta del pozo y patio de armas de la guardia.
    private static void PonerEscenasDeLaCiudadAlta(Obra o, Transform grupo)
    {
        Transform entrada = Grupo(grupo, "Entrada del castillo");
        foreach (float s in new[] { -1f, 1f })
        {
            Pon(o, entrada, Armadura, "Armadura de la guardia", new Vector2(s * 13f, 317.8f), 180f, 2.3f, Medida.Alto, corredor: false, camino: true);
            Pon(o, entrada, Estandarte, "Estandarte del castillo", new Vector2(s * 16.5f, 317f), 180f, 5f, Medida.Alto, corredor: false, camino: true);
        }

        Transform plazoleta = Grupo(grupo, "Plazoleta del pozo");
        Vector2 c = new Vector2(34f, 326f);
        Pon(o, plazoleta, Pozo, "Pozo de la plazoleta", c, 0f, 1.25f);
        Pon(o, plazoleta, Banco, "Banco de la plazoleta", c + new Vector2(-3.4f, 0f), -90f);
        Pon(o, plazoleta, Maceta, "Maceta de la plazoleta", c + new Vector2(2.6f, 2.6f), 0f);
        Pon(o, plazoleta, Maceta, "Maceta de la plazoleta", c + new Vector2(2.6f, -2.6f), 0f);

        Transform armas = Grupo(grupo, "Patio de armas de la guardia");
        Vector2 a = new Vector2(-34f, 326f);
        Pon(o, armas, Armero, "Armero de la guardia", a + new Vector2(-2.5f, 3f), 180f);
        Pon(o, armas, Maniqui, "Maniquí de entrenamiento", a + new Vector2(1.5f, -2f), 0f, 2.1f, Medida.Alto);
        Pon(o, armas, Diana, "Diana de tiro", a + new Vector2(2.8f, 2.8f), 225f);
        Pon(o, armas, Barril, "Barril de la guardia", a + new Vector2(-3.2f, -2.6f), 0f);
    }

    // ── Rótulos de los oficios ───────────────────────────────────────────────────────────────

    /// Rótulo (placa con icono, sin letras) de cada oficio: casa, placa de Props/Sign y dirección de la puerta
    /// en la que va (grados; el archivo lo lleva en su puerta sur, la que da a la Vía Real).
    private static readonly (string casa, string placa, float puerta)[] RotulosDelReino =
    {
        ("Casa del sastre", "Sign01_a01", 90f),
        ("Casa del platero", "Sign20_a01", 0f),
        ("Casa del carnicero", "Sign11_a01", 0f),
        ("Casa del herrador", "Sign17_a01", 0f),
        ("Casa del pescadero", "Sign10_a01", 180f),
        ("Panadería de la plaza", "Sign12_a01", 180f),
        ("Casa del cambista", "Sign25_a01", 180f),
        ("Bodega de la Calle Mayor", "Sign03_a01", 0f),
        ("Posada del Camino Real", "Sign02_a01", 180f),
        ("Archivo Real", "Sign21_a01", 180f),
    };

    /// Pone cada rótulo junto a la puerta de su casa: a 2 m sobre el umbral, a 1,5 m de la puerta y pegado al
    /// muro (la puerta marca dónde está el muro de verdad, más adentro que los aleros y balcones). La placa no
    /// tiene grosor: van dos espalda con espalda, así se ve desde fuera sea cual sea su cara.
    private static void PonerRotulos(Obra o, Transform g, Pueblo reino)
    {
        Transform nuevas = BuscarCasasNuevas(o.Raiz.gameObject.scene, reino);
        if (nuevas == null) return;
        foreach (var (nombreCasa, placa, direccion) in RotulosDelReino)
        {
            Transform casa = nuevas.Find(nombreCasa);
            Transform puerta = casa != null ? PuertaQueDaA(casa.gameObject, FrenteDe(direccion)) : null;
            if (puerta == null) { o.Informe.Add($"  · sin rótulo en «{nombreCasa}»: no está o no tiene puerta hacia {direccion:0}°."); continue; }
            Vector3 f = puerta.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 lado = Vector3.Cross(Vector3.up, f);
            Vector3 centro = puerta.position + lado * 1.5f + Vector3.up * 2f + f * 0.1f;
            string ruta = CarpetaDePlacas + placa + ".prefab";
            string nombre = $"Rótulo de «{nombreCasa}»";
            if (PonerColgado(o, g, ruta, nombre, centro, Quaternion.LookRotation(f), 0.75f) == null) continue;
            PonerColgado(o, g, ruta, nombre, centro - f * 0.02f, Quaternion.LookRotation(-f), 0.75f);
        }
    }

    /// Puerta de una casa colocada: el hijo Door* cuyo frente (su +Z) se parece más a «direccion» y, entre
    /// las que dan a ese lado, la más baja. Null si ninguna da a ese lado.
    private static Transform PuertaQueDaA(GameObject casa, Vector2 direccion)
    {
        float suelo = LimitesVisibles(casa).min.y;
        Transform mejor = null;
        float mejorNota = float.MinValue;
        foreach (Transform t in casa.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("Door") || !t.gameObject.activeInHierarchy) continue;
            var f = new Vector2(t.forward.x, t.forward.z);
            if (f.sqrMagnitude < 0.25f) continue;
            float alineada = Vector2.Dot(f.normalized, direccion);
            if (alineada < 0.7f) continue;
            float nota = alineada - (t.position.y - suelo) * 0.2f;
            if (nota <= mejorNota) continue;
            mejorNota = nota;
            mejor = t;
        }
        return mejor;
    }
}
