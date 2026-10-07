using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Detalles de los pueblos: casas nuevas del Reino, macetas y bancos en las puertas, farolas en las
/// calles, plazas adornadas sin invadir las reservadas para eventos, jardines en los solares vacíos y
/// escenas propias de cada pueblo (mercado, puerta de la muralla, embarcadero, era de la granja…).
public static partial class VestidoDelMundo
{
    private const string FK = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    private const string Tiny = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/";
    private const string ModularCastle = "Assets/Art/World/Modular Castle/Assets/prefabs/";

    private const string Farola = FK + "Props/Lighting/Light03_a01.prefab";
    private const string FarolBajo = FK + "Props/Lighting/Light01_a01.prefab";
    private const string Estandarte = FK + "Props/Flag/Flag01_a01.prefab";
    private const string EstandarteColor = FK + "Props/Flag/Flag01_b01.prefab";
    private const string Banco = FK + "Props/Furniture/Chair/Chair01_a01.prefab";
    private const string Mesa = FK + "Props/Furniture/Table/Table01_a01.prefab";
    private const string Barril = FK + "Props/Goods/Barrel01_a01.prefab";
    private const string Caja = FK + "Props/Goods/Goods03_a01.prefab";
    private const string Sacos = FK + "Props/Goods/Goods04_a01.prefab";
    private const string Carreta = FK + "Props/Goods/Cart04.prefab";
    private const string CarroToldo = FK + "Props/Goods/Cart09.prefab";
    private const string Carretilla = FK + "Props/Goods/Cart02.prefab";
    private const string Lena = FK + "Props/Goods/Wood01_a01.prefab";
    private const string Maceta = FK + "Vegetation/Flowerpot/Flowerpot01_b03.prefab";
    private const string Jardinera = FK + "Vegetation/Flowerpot/Flowerpot02_a01.prefab";
    private const string Pozo = FK + "Main Structures/Decoration/Well02.prefab";
    private const string Armadura = ModularCastle + "mannequin_armor.prefab";
    private const string Maniqui = ModularCastle + "mannequin.prefab";
    private const string Armero = FK + "Props/Weapon/Shelf01_a02.prefab";
    private const string Diana = FK + "Props/Weapon/Target01_a01.prefab";
    private const string Heno = FK + "Props/Farm/Hay01_a02.prefab";
    private const string HenoGrande = FK + "Props/Farm/Hay01_a04.prefab";
    private const string Abrevadero = FK + "Props/Farm/Trough02_a01.prefab";
    private const string Gallinero = FK + "Props/Farm/Henhouse01_a01.prefab";
    private const string Colmena = FK + "Props/Farm/Hive01_a03.prefab";
    private const string Espantapajaros = FK + "Props/Weapon/Scarecrow01_a01.prefab";
    private const string Nasa = FK + "Props/Goods/Fishingcage01_a01.prefab";
    private const string Nasa2 = FK + "Props/Goods/Fishingcage02_a01.prefab";
    private const string Red = FK + "Props/Goods/Net01_a01.prefab";
    private const string Barca = FK + "Props/Ship/Boat01_a01.prefab";

    private static readonly string[] ArbolesDeJardin =
    {
        FK + "Vegetation/Tree01_a01.prefab",
        FK + "Vegetation/Tree05_a01.prefab",
        FK + "Vegetation/Tree03_a01.prefab",
    };

    private static readonly string[] Parterres =
    {
        FK + "Vegetation/Plant02_c01.prefab",
        FK + "Vegetation/Plant04_a01.prefab",
        FK + "Vegetation/Plant04_b01.prefab",
        FK + "Vegetation/Plant03_a01.prefab",
    };

    private const string Flor = FK + "Vegetation/Flower02_a01.prefab";

    private static readonly string[] PuestosDeMercado =
    {
        PrefabsEdificios + "BuildingAT36.prefab", // básico
        PrefabsEdificios + "BuildingAT37.prefab", // bebidas
        PrefabsEdificios + "BuildingAT40.prefab", // carnicero
        PrefabsEdificios + "BuildingAT38.prefab", // ultramarinos
        PrefabsEdificios + "BuildingAT43.prefab", // adivina
        PrefabsEdificios + "BuildingAT41.prefab", // pescado
    };

    /// Lado de la puerta de los edificios que pone el vestido (grados respecto al +Z local).
    private static readonly Dictionary<string, float> LadoDeLaPuertaNuevas = new()
    {
        { "BuildingAT02", 90f }, { "BuildingAT06", 180f }, { "BuildingAT47", 180f }, { "BuildingAT53", 180f },
    };

    /// Casas nuevas del Reino: solares libres medidos sobre MainWorld (huella, calles, muralla, pendiente y
    /// sendas de las puertas vecinas). La puerta mira a su calle.
    private static CasaNueva[] CasasNuevasDelReino() => new[]
    {
        new CasaNueva("BuildingAT47", 62.2f, 305.1f, 189.3f),
        new CasaNueva("BuildingAT02", 70.3f, 303.5f, 192.1f),
        new CasaNueva("BuildingAT53", 78.6f, 301.8f, 192.1f),
        new CasaNueva("BuildingAT06", 87.9f, 299.0f, 212.4f),
        new CasaNueva("BuildingAT02", -80.0f, 251.2f, 0f),
        new CasaNueva("BuildingAT47", -62.0f, 251.0f, 0f),
        new CasaNueva("BuildingAT02", -84.0f, 307.8f, 180f),
        new CasaNueva("BuildingAT47", -62.0f, 326.0f, 90f),
        new CasaNueva("BuildingAT53", -46.0f, 326.0f, 270f),
        new CasaNueva("BuildingAT47", 28.5f, 308.0f, 180f),
        new CasaNueva("BuildingAT02", 42.0f, 307.8f, 180f),
        new CasaNueva("BuildingAT47", 46.0f, 324.0f, 90f),
        new CasaNueva("BuildingAT53", 62.0f, 324.0f, 270f),
    };

    private struct Puerta
    {
        public Vector2 Pos, Frente;
        public string Prefab;
    }

    private const string GrupoCasasNuevas = "Casas nuevas";

    private static string NombreCasaNueva(int i, CasaNueva n) => $"Casa nueva {i} ({n.Prefab})";

    private static Vector2 FrenteDe(float grados)
    {
        float a = grados * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
    }

    private static Vector3 Esquina(int i) => new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);

    /// Distancia desde «centro» hasta la cara de la casa en la dirección «dir», medida con la caja de cada
    /// malla en su propio espacio (el AABB de mundo exagera el fondo de una casa girada en diagonal).
    private static float Alcance(GameObject go, Vector2 centro, Vector2 dir)
    {
        float max = 0f;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || !(r is MeshRenderer)) continue;
            if (!r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
            Bounds local = mf.sharedMesh.bounds;
            Matrix4x4 m = r.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 q = m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, Esquina(i)));
                max = Mathf.Max(max, Vector2.Dot(new Vector2(q.x, q.z) - centro, dir));
            }
        }
        return max;
    }

    private static Transform BuscarCasasNuevas(Scene escena, Pueblo pueblo)
    {
        Transform raiz = BuscarRaiz(escena);
        return raiz == null ? null : raiz.Find(pueblo.Nombre + "/" + GrupoCasasNuevas);
    }

    /// Puertas de las casas que hay ahora en la escena: las del generador (con su giro, si lo tienen) y
    /// las casas nuevas que el vestido llegó a colocar. Lo usan la pintura del suelo y los adornos.
    private static List<Puerta> PuertasDelPueblo(Scene escena, Pueblo pueblo, HashSet<string> giradas)
    {
        var r = new List<Puerta>();
        foreach (string nombreGrupo in pueblo.Grupos)
        {
            Transform grupo = BuscarGrupo(escena, nombreGrupo);
            if (grupo == null) continue;
            foreach (Transform casa in grupo)
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente == null || !fuente.name.StartsWith("BuildingAT")) continue;
                Vector3 f3 = FrenteDeCasa(casa, fuente.name, giradas);
                r.Add(PuertaDe(casa.gameObject, new Vector2(f3.x, f3.z).normalized, fuente.name));
            }
        }
        Transform nuevas = BuscarCasasNuevas(escena, pueblo);
        if (nuevas != null)
            for (int i = 0; i < pueblo.Nuevas.Length; i++)
            {
                CasaNueva n = pueblo.Nuevas[i];
                Transform casa = nuevas.Find(NombreCasaNueva(i + 1, n));
                if (casa != null) r.Add(PuertaDe(casa.gameObject, FrenteDe(n.Frente), n.Prefab));
            }
        return r;
    }

    private static Puerta PuertaDe(GameObject casa, Vector2 frente, string prefab)
    {
        Bounds b = LimitesVisibles(casa);
        var centro = new Vector2(b.center.x, b.center.z);
        return new Puerta { Pos = centro + frente * (Alcance(casa, centro, frente) + 1.2f), Frente = frente, Prefab = prefab };
    }

    private static float Rumbo(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

    /// Atajo para colocar una pieza suelta.
    private static GameObject Pon(Obra o, Transform grupo, string prefab, string nombre, Vector2 pos, float rumbo,
        float tamano = 1f, Medida medida = Medida.Escala, bool camino = false, float holgura = 0.15f, float desnivel = 0.9f,
        bool corredor = true, float hundir = 0.04f, float inclinar = 0f, bool solapePropio = false)
    {
        var p = new Pieza
        {
            Prefab = prefab, Nombre = nombre, Pos = pos, Rumbo = rumbo, Tamano = tamano, Medida = medida,
            PermitirCamino = camino, Holgura = holgura, DesnivelMax = desnivel, Hundir = hundir, Inclinar = inclinar,
            PermitirSolapePropio = solapePropio,
        };
        if (corredor && EnCorredor(o, pos, 0.6f)) { o.Descartar("tapa una senda o una calle", p); return null; }
        return Poner(o, grupo, p);
    }

    // ── Corredores: lo que tiene que quedar transitable ──────────────────────────────────────

    private static void RegistrarCorredores(Obra o, Pueblo pueblo, List<Puerta> puertas)
    {
        foreach (Vector2[] c in pueblo.Calles) o.Corredores.Add((c, pueblo.AnchoCalle * 0.5f + 0.6f));
        foreach (Vector2[] c in pueblo.Accesos) o.Corredores.Add((c, 3.1f));
        foreach (Puerta p in puertas)
        {
            Vector2 destino = DestinoMasCercano(pueblo, p.Pos);
            if (Vector2.Distance(destino, p.Pos) < 60f) o.Corredores.Add((new[] { p.Pos - p.Frente * 1.2f, destino }, 1.9f));
        }
        foreach (Plaza pl in pueblo.Plazas) o.PlazasLibres.Add(new Rect(pl.Centro - pl.Tamano * 0.5f, pl.Tamano));
    }

    private static bool EnCorredor(Obra o, Vector2 p, float radio)
    {
        foreach (var c in o.Corredores)
            if (DistanciaAPolilinea(p, c.puntos) < c.semiancho + radio) return true;
        return EnPlaza(o, p, radio);
    }

    private static bool EnPlaza(Obra o, Vector2 p, float radio)
    {
        foreach (Rect r in o.PlazasLibres)
        {
            Rect g = new Rect(r.x - radio, r.y - radio, r.width + 2 * radio, r.height + 2 * radio);
            if (g.Contains(p)) return true;
        }
        return false;
    }

    // ── Pueblos ──────────────────────────────────────────────────────────────────────────────

    private static void PoblarZonas(Obra o)
    {
        Scene escena = o.Raiz.gameObject.scene;
        HashSet<string> giradas = CasasGiradas(escena);
        foreach (Pueblo pueblo in Pueblos())
        {
            Transform grupo = Grupo(o.Raiz, pueblo.Nombre);
            List<Puerta> puertas = PuertasDelPueblo(escena, pueblo, giradas);
            RegistrarCorredores(o, pueblo, puertas);
            int antes = o.Puestas;
            AdornarPuertas(o, Grupo(grupo, "Puertas"), puertas, pueblo.Semilla);
            FarolasEnCalles(o, Grupo(grupo, "Farolas"), pueblo);
            if (pueblo.Nombre == "Reino") EscenasDelReino(o, grupo);
            else if (pueblo.Nombre == "Pueblo pesquero") EscenasDelPuerto(o, grupo);
            else if (pueblo.Nombre == "Pueblo vecino") EscenasDelVecino(o, grupo);
            else if (pueblo.Nombre == "Granjas de la cascada") EscenasDeLasGranjas(o, grupo);
            Jardines(o, Grupo(grupo, "Jardines"), pueblo);
            o.Informe.Add($"{pueblo.Nombre}: {o.Puestas - antes} piezas de detalle.");
        }
        PoblarParajes(o);
    }

    /// Coloca las casas nuevas de todos los pueblos. Va antes de pintar el suelo: así solo se pintan la
    /// puerta, la senda y el patio de las que de verdad caben.
    private static void PonerCasasNuevas(Obra o)
    {
        foreach (Pueblo pueblo in Pueblos())
        {
            if (pueblo.Nuevas.Length == 0) continue;
            Transform grupo = Grupo(Grupo(o.Raiz, pueblo.Nombre), GrupoCasasNuevas);
            int puestas = 0;
            for (int i = 0; i < pueblo.Nuevas.Length; i++)
            {
                CasaNueva n = pueblo.Nuevas[i];
                LadoDeLaPuertaNuevas.TryGetValue(n.Prefab, out float lado);
                var p = new Pieza
                {
                    Prefab = PrefabsEdificios + n.Prefab + ".prefab",
                    Nombre = NombreCasaNueva(i + 1, n),
                    Pos = n.Pos,
                    Rumbo = n.Frente - lado,
                    DesnivelMax = 1.4f,
                    Hundir = 0.15f,
                    PermitirCamino = true,
                    Holgura = 0.2f,
                };
                if (Poner(o, grupo, p) != null) puestas++;
            }
            o.Informe.Add($"{pueblo.Nombre}: {puestas} de {pueblo.Nuevas.Length} casas nuevas.");
        }
    }

    /// Macetas a los lados de cada puerta y, según la casa, banco o barriles y cajas.
    private static void AdornarPuertas(Obra o, Transform grupo, List<Puerta> puertas, int semilla)
    {
        // Pegado a la casa: puede estar junto a una senda, pero el interior de las plazas no se adorna.
        void Junto(string prefab, string nombre, Vector2 pos, float giro)
        {
            if (!EnPlaza(o, pos, 0.4f)) Pon(o, grupo, prefab, nombre, pos, giro, holgura: 0.02f, corredor: false, camino: true);
        }

        var dado = new Ruido.Dado(semilla);
        foreach (Puerta p in puertas)
        {
            Vector2 lado = new Vector2(p.Frente.y, -p.Frente.x);
            Vector2 fachada = p.Pos - p.Frente * 1.2f;
            float rumbo = Rumbo(p.Frente);
            string maceta = dado.Siguiente() < 0.5f ? Maceta : Jardinera;
            // La jardinera, con el lado largo a lo largo del muro.
            float giroMaceta = maceta == Jardinera ? rumbo : rumbo + 90f;
            Junto(maceta, "Maceta de la puerta", fachada + lado * 1.8f + p.Frente * 0.5f, giroMaceta);
            Junto(maceta, "Maceta de la puerta", fachada - lado * 1.8f + p.Frente * 0.5f, giroMaceta);
            float tirada = dado.Siguiente();
            float s = dado.Siguiente() < 0.5f ? 1f : -1f;
            if (tirada < 0.35f)
                Junto(Banco, "Banco junto a la puerta", fachada + lado * s * 3.6f + p.Frente * 0.7f, rumbo + 180f);
            else if (tirada < 0.7f)
            {
                Junto(Barril, "Barril junto a la puerta", fachada + lado * s * 3.3f + p.Frente * 0.6f, dado.Entre(0f, 360f));
                Junto(Caja, "Caja junto a la puerta", fachada + lado * s * 4.3f + p.Frente * 0.6f, dado.Entre(0f, 360f));
            }
            else if (tirada < 0.85f)
                Junto(Lena, "Leña junto a la casa", fachada + lado * s * 3.5f + p.Frente * 0.6f, rumbo);
        }
    }

    /// Farolas cada ~18 m a un lado y otro de calles y accesos.
    private static void FarolasEnCalles(Obra o, Transform grupo, Pueblo pueblo)
    {
        void Recorrer(Vector2[] linea, float desplazamiento, float paso, int semilla)
        {
            float acumulado = paso * 0.5f;
            int n = 0;
            for (int j = 0; j < linea.Length - 1; j++)
            {
                Vector2 a = linea[j], b = linea[j + 1];
                float l = Vector2.Distance(a, b);
                if (l < 0.01f) continue;
                Vector2 dir = (b - a) / l, normal = new Vector2(-dir.y, dir.x);
                for (float t = acumulado; t < l; t += paso)
                {
                    float lado = (n++ % 2 == 0) ? 1f : -1f;
                    Vector2 pos = a + dir * t + normal * lado * desplazamiento;
                    if (EnCorredorSalvo(o, pos, 0.4f, linea)) continue;
                    Pon(o, grupo, Farola, "Farola", pos, Rumbo(dir) + (lado > 0 ? 90f : -90f), camino: true, corredor: false, holgura: 0.1f);
                }
                acumulado = (acumulado - l) % paso;
                if (acumulado < 0f) acumulado += paso;
            }
        }
        for (int i = 0; i < pueblo.Calles.Length; i++) Recorrer(pueblo.Calles[i], pueblo.AnchoCalle * 0.5f + 1.1f, 18f, pueblo.Semilla + i);
        for (int i = 0; i < pueblo.Accesos.Length; i++) Recorrer(pueblo.Accesos[i], 3.6f, 20f, pueblo.Semilla + 40 + i);
    }

    /// Como EnCorredor pero sin contar la propia calle junto a la que se pone la pieza.
    private static bool EnCorredorSalvo(Obra o, Vector2 p, float radio, Vector2[] propia)
    {
        foreach (var c in o.Corredores)
        {
            if (ReferenceEquals(c.puntos, propia)) continue;
            if (DistanciaAPolilinea(p, c.puntos) < c.semiancho + radio) return true;
        }
        foreach (Rect r in o.PlazasLibres)
            if (new Rect(r.x - radio, r.y - radio, r.width + 2 * radio, r.height + 2 * radio).Contains(p)) return true;
        return false;
    }

    /// Jardines en los huecos: árbol o parterre con flores, lejos de calles, sendas, plazas y casas.
    private static void Jardines(Obra o, Transform grupo, Pueblo pueblo)
    {
        int maximo = pueblo.Nombre == "Reino" ? 30 : 10;
        var dado = new Ruido.Dado(pueblo.Semilla + 900);
        var puestos = new List<Vector2>();
        foreach (Vector3 d in pueblo.Alfombra)
        {
            for (float z = d.y - d.z; z <= d.y + d.z; z += 7f)
                for (float x = d.x - d.z; x <= d.x + d.z; x += 7f)
                {
                    if (puestos.Count >= maximo) return;
                    var p = new Vector2(x + dado.Entre(-2f, 2f), z + dado.Entre(-2f, 2f));
                    if (Vector2.Distance(p, new Vector2(d.x, d.y)) > d.z * 0.85f) continue;
                    if (pueblo.Recinto != null && !DentroDePoligono(p, pueblo.Recinto)) continue;
                    if (EnCorredor(o, p, 3.5f)) continue;
                    if (PesoCamino(o, p.x, p.y) > 0.2f || o.Suelo.Altura(p.x, p.y) < 1.5f) continue;
                    bool cerca = false;
                    foreach (Vector2 q in puestos) if (Vector2.Distance(p, q) < 11f) { cerca = true; break; }
                    if (cerca || HayAlgoCerca(o, p, 4f)) continue;

                    GameObject g;
                    if (dado.Siguiente() < 0.55f)
                    {
                        string arbol = ArbolesDeJardin[dado.Indice(ArbolesDeJardin.Length)];
                        g = Pon(o, grupo, arbol, "Árbol de jardín", p, dado.Entre(0f, 360f), dado.Entre(5f, 7.5f), Medida.Alto, desnivel: 1.5f, hundir: 0.15f);
                        if (g != null) CopiarMaterialesDelVecino(o, g, arbol);
                    }
                    else
                    {
                        g = Pon(o, grupo, Parterres[dado.Indice(Parterres.Length)], "Parterre", p, dado.Entre(0f, 360f), desnivel: 1.2f);
                        if (g != null)
                            for (int k = 0; k < 3; k++)
                            {
                                float a = dado.Entre(0f, Mathf.PI * 2f);
                                Pon(o, grupo, Flor, "Flores", p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dado.Entre(2f, 3f), dado.Entre(0f, 360f),
                                    dado.Entre(0.8f, 1.2f), holgura: 0f, corredor: false, solapePropio: true);
                            }
                    }
                    if (g != null) puestos.Add(p);
                }
        }
    }

    private static readonly Collider[] BufferCerca = new Collider[16];

    private static bool HayAlgoCerca(Obra o, Vector2 p, float radio)
    {
        float y = o.Suelo.Altura(p.x, p.y);
        int n = Physics.OverlapSphereNonAlloc(new Vector3(p.x, y + 1.2f, p.y), radio, BufferCerca, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!Ignorable(BufferCerca[i])) return true;
        foreach (Huella h in o.Ocupado)
            if (h.Contiene(p, radio)) return true;
        return false;
    }

    /// Los árboles de MainWorld llevan materiales matizados por paraje; el árbol nuevo copia los del
    /// ejemplar más cercano del mismo prefab para que no desentone.
    private static void CopiarMaterialesDelVecino(Obra o, GameObject nuevo, string rutaPrefab)
    {
        if (!o.EjemplaresPorPrefab.TryGetValue(rutaPrefab, out List<GameObject> ejemplares) || ejemplares.Count == 0) return;
        Vector3 p = nuevo.transform.position;
        GameObject mejor = null;
        float d = float.MaxValue;
        foreach (GameObject e in ejemplares)
        {
            if (e == null) continue;
            float de = (e.transform.position - p).sqrMagnitude;
            if (de < d) { d = de; mejor = e; }
        }
        if (mejor == null) return;
        Renderer[] origen = mejor.GetComponentsInChildren<Renderer>(true);
        Renderer[] destino = nuevo.GetComponentsInChildren<Renderer>(true);
        if (origen.Length != destino.Length) return;
        for (int i = 0; i < destino.Length; i++) destino[i].sharedMaterials = origen[i].sharedMaterials;
    }

    // ── Escenas propias de cada pueblo ───────────────────────────────────────────────────────

    private static void EscenasDelReino(Obra o, Transform grupo)
    {
        // Entrada al castillo: armaduras y estandartes pegados a la fachada, fuera del eje plaza→puerta
        // (x ±6) y del acceso despejado (12 m alrededor de (0,320)).
        Transform entrada = Grupo(grupo, "Entrada del castillo");
        foreach (float s in new[] { -1f, 1f })
        {
            Pon(o, entrada, Armadura, "Armadura de la guardia", new Vector2(s * 13f, 318.5f), 180f, 2.3f, Medida.Alto, corredor: false, camino: true);
            Pon(o, entrada, EstandarteColor, "Estandarte del castillo", new Vector2(s * 16.5f, 317f), 180f, 5f, Medida.Alto, corredor: false, camino: true);
        }

        // Plaza real (reservada para la audiencia y el Demonio 2): solo adornos fuera del rectángulo.
        Transform plazaReal = Grupo(grupo, "Bordes de la plaza real");
        foreach (float sx in new[] { -1f, 1f })
            foreach (float z in new[] { 286f, 312f })
                Pon(o, plazaReal, Estandarte, "Estandarte de la plaza real", new Vector2(sx * 22.8f, z), sx > 0 ? -90f : 90f, 4.4f, Medida.Alto, camino: true, corredor: false);
        Pon(o, plazaReal, Jardinera, "Jardinera de la plaza real", new Vector2(-26f, 287f), 90f, camino: true);
        Pon(o, plazaReal, Jardinera, "Jardinera de la plaza real", new Vector2(26f, 287f), -90f, camino: true);
        Pon(o, plazaReal, Banco, "Banco de la plaza real", new Vector2(-27f, 291f), 90f, camino: true);
        Pon(o, plazaReal, Banco, "Banco de la plaza real", new Vector2(27f, 291f), -90f, camino: true);

        // Plazoletas entre casas de la terraza alta: al este, pozo y bancos; al oeste, patio de armas de la guardia.
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

        // Mercado de la terraza baja: puestos a lo largo de la calle, fuera de la plaza de la taberna.
        Transform mercado = Grupo(grupo, "Mercado de la terraza baja");
        var puestos = new (float x, float z, float frente)[]
        {
            (30f, 249f, 270f), (-55f, 252f, 0f), (48f, 252f, 0f), (58f, 252f, 0f), (-64f, 265f, 180f),
            (-49f, 265f, 180f), (31f, 265f, 180f), (45f, 265f, 180f), (60f, 265f, 180f),
        };
        for (int i = 0; i < puestos.Length; i++)
        {
            var (x, z, frente) = puestos[i];
            GameObject g = Pon(o, mercado, PuestosDeMercado[i % PuestosDeMercado.Length], "Puesto del mercado", new Vector2(x, z), frente, camino: true, holgura: 0.1f);
            if (g == null) continue;
            Vector2 f = new Vector2(Mathf.Sin(frente * Mathf.Deg2Rad), Mathf.Cos(frente * Mathf.Deg2Rad));
            Vector2 l = new Vector2(f.y, -f.x);
            Pon(o, mercado, i % 2 == 0 ? Caja : Sacos, "Género del puesto", new Vector2(x, z) + l * 3.2f, frente + 20f, camino: true, holgura: 0.02f);
            if (i % 3 == 0) Pon(o, mercado, Barril, "Barril del puesto", new Vector2(x, z) - l * 3.2f, 0f, camino: true, holgura: 0.02f);
        }
        Pon(o, mercado, CarroToldo, "Carro-puesto del mercado", new Vector2(-40f, 252f), 90f, camino: true);
        Pon(o, mercado, Carreta, "Carreta de descarga", new Vector2(82f, 266f), 200f, camino: true);

        // Puerta de la muralla (sureste): estandartes a ambos lados del camino.
        Transform puerta = Grupo(grupo, "Puerta de la muralla");
        Pon(o, puerta, EstandarteColor, "Estandarte de la puerta", new Vector2(63f, 244.5f), 210f, 5f, Medida.Alto, camino: true, corredor: false);
        Pon(o, puerta, EstandarteColor, "Estandarte de la puerta", new Vector2(93.5f, 252f), 210f, 5f, Medida.Alto, camino: true, corredor: false);
    }

    private static void EscenasDelPuerto(Obra o, Transform grupo)
    {
        Transform muelle = Grupo(grupo, "Embarcadero y playa");
        Pon(o, muelle, Caja, "Caja junto a la rampa", new Vector2(277f, -449f), 15f, camino: true, corredor: false);
        Pon(o, muelle, Caja, "Caja junto a la rampa", new Vector2(278f, -447.8f), 40f, camino: true, corredor: false, solapePropio: true);
        Pon(o, muelle, Barril, "Barril junto a la rampa", new Vector2(263f, -449f), 0f, camino: true, corredor: false);
        Pon(o, muelle, Nasa, "Nasa", new Vector2(261.5f, -447.5f), 30f, camino: true, corredor: false);
        Pon(o, muelle, Nasa2, "Nasa", new Vector2(279.5f, -446f), -20f, camino: true, corredor: false);
        Pon(o, muelle, Barca, "Barca varada en la arena", new Vector2(244f, -466f), 115f, desnivel: 1.6f, hundir: 0.25f, inclinar: 6f);
        Pon(o, muelle, Barca, "Barca varada en la arena", new Vector2(296f, -467f), 250f, desnivel: 1.6f, hundir: 0.25f, inclinar: -5f);
        Pon(o, muelle, Red, "Red puesta a secar", new Vector2(249f, -458f), 90f);
        Pon(o, muelle, Red, "Red puesta a secar", new Vector2(251.5f, -458f), 90f);
        Pon(o, muelle, Red, "Red puesta a secar", new Vector2(291f, -458f), 90f);

        Transform plaza = Grupo(grupo, "Plaza del puerto");
        Pon(o, plaza, PrefabsEdificios + "BuildingAT41.prefab", "Puesto de pescado", new Vector2(286.5f, -440f), 270f, camino: true);
        Pon(o, plaza, PrefabsEdificios + "BuildingAT42.prefab", "Puesto de pescado", new Vector2(253.5f, -440f), 90f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza del puerto", new Vector2(256f, -425f), 90f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza del puerto", new Vector2(284f, -425f), -90f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza del puerto", new Vector2(256.5f, -448f), 0f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza del puerto", new Vector2(283.5f, -448f), 0f, camino: true);
    }

    private static void EscenasDelVecino(Obra o, Transform grupo)
    {
        Transform plaza = Grupo(grupo, "Plaza del pueblo vecino");
        Pon(o, plaza, PrefabsEdificios + "BuildingAT36.prefab", "Puesto de la plaza", new Vector2(320f, -130f), 0f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza", new Vector2(312f, -115f), 90f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza", new Vector2(348f, -110f), -90f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza", new Vector2(313f, -101f), 0f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza", new Vector2(347f, -129f), 0f, camino: true);
        Transform vida = Grupo(grupo, "Vida del pueblo");
        Pon(o, vida, Carreta, "Carreta", new Vector2(323f, -88f), 160f);
        Pon(o, vida, Heno, "Paca de heno", new Vector2(366f, -128f), 0f);
        Pon(o, vida, Heno, "Paca de heno", new Vector2(367.8f, -126.5f), 40f, solapePropio: true);
        Pon(o, vida, Lena, "Leña", new Vector2(290f, -110f), 0f);
    }

    private static void EscenasDeLasGranjas(Obra o, Transform grupo)
    {
        Transform era = Grupo(grupo, "Era y corrales");
        Pon(o, era, HenoGrande, "Almiar", new Vector2(281f, 130f), 0f, desnivel: 1.2f);
        Pon(o, era, Heno, "Paca de heno", new Vector2(284.5f, 134f), 30f);
        Pon(o, era, Heno, "Paca de heno", new Vector2(278f, 125.5f), 70f);
        Pon(o, era, Abrevadero, "Abrevadero", new Vector2(252f, 140f), 0f, camino: true);
        Pon(o, era, Carretilla, "Carretilla", new Vector2(242f, 139.5f), 120f, camino: true);
        Pon(o, era, Gallinero, "Gallinero", new Vector2(203f, 166f), 90f);
        Pon(o, era, Gallinero, "Gallinero", new Vector2(203f, 170f), 90f);
        Pon(o, era, Espantapajaros, "Espantapájaros", new Vector2(254f, 179.5f), 180f, camino: true, corredor: false);
        for (int i = 0; i < 3; i++) Pon(o, era, Colmena, "Colmena", new Vector2(283.5f, 150f + i * 2.4f), 270f);
    }
}
