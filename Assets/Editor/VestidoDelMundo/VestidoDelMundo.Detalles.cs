using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Detalles de los pueblos: casas nuevas, macetas y bancos en las puertas, farolas en las calles, plazas
/// adornadas sin invadir las reservadas para eventos, jardines en los solares vacíos y escenas propias de cada
/// pueblo (embarcadero, plaza del pueblo vecino, era de la granja…). La capital se viste aparte, en
/// VestidoDelMundo.Capital, con estas mismas piezas.
public static partial class VestidoDelMundo
{
    private const string FK = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    private const string Tiny = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/";
    private const string ModularCastle = "Assets/Art/World/Modular Castle/Assets/prefabs/";

    private const string Farola = FK + "Props/Lighting/Light03_a01.prefab";
    private const string Estandarte = FK + "Props/Flag/Flag01_a01.prefab";
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
    /// Bola de boj en maceta de piedra: la maceta de las puertas de la ciudad.
    private const string MacetaDeBola = FK + "Vegetation/Flowerpot/Flowerpot03_a01.prefab";
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

    /// Lado de la puerta de los edificios que pone el vestido (grados respecto al +Z local), medido en sus
    /// hijos Door* (la puerta a ras de suelo o, si no hay, la principal del piso alto). En la taberna AT18 es la
    /// terraza (−X), que es por donde se entra; en el archivo AT31, la fachada larga −Z (sus puertas, en los
    /// testeros, dan a las calles laterales); en el cuerpo de guardia AT25, el porche de madera (−X).
    private static readonly Dictionary<string, float> LadoDeLaPuertaNuevas = new()
    {
        { "BuildingAT02", 90f }, { "BuildingAT06", 180f }, { "BuildingAT07", 90f }, { "BuildingAT09", 90f },
        { "BuildingAT17", 180f }, { "BuildingAT18", 270f }, { "BuildingAT25", 270f }, { "BuildingAT27", 90f },
        { "BuildingAT31", 180f }, { "BuildingAT46", 270f }, { "BuildingAT47", 180f }, { "BuildingAT48", 90f },
        { "BuildingAT53", 180f }, { "BuildingAT54", 180f }, { "BuildingAT55", 180f },
    };

    private struct Puerta
    {
        public Vector2 Pos, Frente;
        public string Prefab;
    }

    private const string GrupoCasasNuevas = "Casas nuevas";

    private static string NombreCasaNueva(int i, CasaNueva n) => n.Nombre ?? $"Casa nueva {i} ({n.NombrePrefab})";

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
                // Lo retirado (inactivo) no tiene puerta: ni senda ni patio.
                if (!casa.gameObject.activeSelf) continue;
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
                if (casa != null) r.Add(PuertaDe(casa.gameObject, FrenteDe(n.Frente), n.NombrePrefab));
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
        for (int i = 0; i < pueblo.Calles.Length; i++) o.Corredores.Add((pueblo.Calles[i], pueblo.AnchoDeCalle(i) * 0.5f + 0.6f));
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
            // La capital se viste en PonerCapital (VestidoDelMundo.Capital): sus piezas de obra van antes que
            // las farolas y los adornos de las puertas, que se adaptan a ellas.
            if (pueblo.Nombre == NombreDelReino) continue;
            Transform grupo = Grupo(o.Raiz, pueblo.Nombre);
            List<Puerta> puertas = PuertasDelPueblo(escena, pueblo, giradas);
            RegistrarCorredores(o, pueblo, puertas);
            int antes = o.Puestas;
            AdornarPuertas(o, Grupo(grupo, "Puertas"), puertas, pueblo.Semilla, ciudad: false);
            FarolasEnCalles(o, Grupo(grupo, "Farolas"), pueblo);
            if (pueblo.Nombre == "Pueblo pesquero") EscenasDelPuerto(o, grupo);
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
                LadoDeLaPuertaNuevas.TryGetValue(n.NombrePrefab, out float lado);
                var p = new Pieza
                {
                    Prefab = n.Ruta,
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

    /// Macetas a los lados de cada puerta y, según la casa, banco o barriles y cajas. En la ciudad, las macetas
    /// son bolas de boj o jardineras y no hay leña.
    private static void AdornarPuertas(Obra o, Transform grupo, List<Puerta> puertas, int semilla, bool ciudad)
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
            string maceta = dado.Siguiente() < 0.5f ? (ciudad ? MacetaDeBola : Maceta) : Jardinera;
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
            else if (tirada < 0.85f && !ciudad)
                Junto(Lena, "Leña junto a la casa", fachada + lado * s * 3.5f + p.Frente * 0.6f, rumbo);
        }
    }

    /// Distancia del borde de una calle pavimentada al pie de lo que va en su acera (farolas, estandartes): la
    /// acera es la franja de 0,75 m del borde, por fuera del bordillo.
    private const float DentroDeLaAcera = 0.4f;

    /// Farolas a un lado y otro de calles y accesos: cada ~18 m, junto al borde de la calle; en las calles
    /// pavimentadas, en la acera (cada 12 m en las avenidas de 8 m o más y cada 16 m en las demás).
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
        for (int i = 0; i < pueblo.Calles.Length; i++)
        {
            float ancho = pueblo.AnchoDeCalle(i);
            if (pueblo.Pavimentado) Recorrer(pueblo.Calles[i], ancho * 0.5f - DentroDeLaAcera, ancho >= 8f ? 12f : 16f, pueblo.Semilla + i);
            else Recorrer(pueblo.Calles[i], ancho * 0.5f + 1.1f, 18f, pueblo.Semilla + i);
        }
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
        const int maximo = 10;
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

    private static void EscenasDelPuerto(Obra o, Transform grupo)
    {
        Transform muelle = Grupo(grupo, "Embarcadero y playa");
        Pon(o, muelle, Caja, "Caja junto a la rampa", new Vector2(277f, -449f), 15f, camino: true, corredor: false);
        Pon(o, muelle, Caja, "Caja junto a la rampa", new Vector2(278f, -447.8f), 40f, camino: true, corredor: false, solapePropio: true);
        Pon(o, muelle, Barril, "Barril junto a la rampa", new Vector2(263f, -449f), 0f, camino: true, corredor: false);
        Pon(o, muelle, Nasa, "Nasa", new Vector2(263f, -447.5f), 30f, camino: true, corredor: false);
        Pon(o, muelle, Nasa2, "Nasa", new Vector2(279.5f, -446f), -20f, camino: true, corredor: false);
        Pon(o, muelle, Barca, "Barca varada en la arena", new Vector2(232.25f, -458.25f), 115f, desnivel: 1.6f, hundir: 0.25f, inclinar: 6f);
        Pon(o, muelle, Barca, "Barca varada en la arena", new Vector2(304.25f, -458.5f), 250f, desnivel: 1.6f, hundir: 0.25f, inclinar: -5f);
        Pon(o, muelle, Red, "Red puesta a secar", new Vector2(249f, -458f), 90f);
        Pon(o, muelle, Red, "Red puesta a secar", new Vector2(251.5f, -458f), 90f);
        Pon(o, muelle, Red, "Red puesta a secar", new Vector2(291f, -458f), 90f);

        Transform plaza = Grupo(grupo, "Plaza del puerto");
        Pon(o, plaza, PrefabsEdificios + "BuildingAT41.prefab", "Puesto de pescado", new Vector2(286.5f, -440f), 270f, camino: true);
        Pon(o, plaza, PrefabsEdificios + "BuildingAT42.prefab", "Puesto de pescado", new Vector2(253.5f, -441f), 90f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza del puerto", new Vector2(255.25f, -425.75f), 90f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza del puerto", new Vector2(284.5f, -425.75f), -90f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza del puerto", new Vector2(256.25f, -446.25f), 0f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza del puerto", new Vector2(283.75f, -446.25f), 0f, camino: true);
    }

    private static void EscenasDelVecino(Obra o, Transform grupo)
    {
        Transform plaza = Grupo(grupo, "Plaza del pueblo vecino");
        Pon(o, plaza, PrefabsEdificios + "BuildingAT36.prefab", "Puesto de la plaza", new Vector2(320f, -130f), 0f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza", new Vector2(312f, -115f), 90f, camino: true);
        Pon(o, plaza, Banco, "Banco de la plaza", new Vector2(348f, -110f), -90f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza", new Vector2(316.25f, -99.5f), 0f, camino: true);
        Pon(o, plaza, Farola, "Farola de la plaza", new Vector2(349.5f, -128f), 0f, camino: true);
        Transform vida = Grupo(grupo, "Vida del pueblo");
        Pon(o, vida, Carreta, "Carreta", new Vector2(323f, -88f), 160f);
        Pon(o, vida, Heno, "Paca de heno", new Vector2(366f, -128f), 0f);
        Pon(o, vida, Heno, "Paca de heno", new Vector2(367.8f, -126.5f), 40f, solapePropio: true);
        Pon(o, vida, Lena, "Leña", new Vector2(290f, -118.75f), 0f);
    }

    private static void EscenasDeLasGranjas(Obra o, Transform grupo)
    {
        Transform era = Grupo(grupo, "Era y corrales");
        Pon(o, era, HenoGrande, "Almiar", new Vector2(281f, 130f), 0f, desnivel: 1.2f);
        Pon(o, era, Heno, "Paca de heno", new Vector2(284.5f, 134f), 30f);
        Pon(o, era, Heno, "Paca de heno", new Vector2(278f, 125.5f), 70f);
        Pon(o, era, Abrevadero, "Abrevadero", new Vector2(252f, 140f), 0f, camino: true);
        Pon(o, era, Carretilla, "Carretilla", new Vector2(242f, 139.5f), 120f, camino: true);
        Pon(o, era, Gallinero, "Gallinero", new Vector2(204f, 165f), 90f);
        Pon(o, era, Gallinero, "Gallinero", new Vector2(206f, 171f), 90f);
        Pon(o, era, Espantapajaros, "Espantapájaros", new Vector2(254f, 179.5f), 180f, camino: true, corredor: false);
        for (int i = 0; i < 3; i++) Pon(o, era, Colmena, "Colmena", new Vector2(283.5f, 150f + i * 2.4f), 270f);
    }
}
