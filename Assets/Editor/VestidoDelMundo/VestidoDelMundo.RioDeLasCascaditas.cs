using System.Collections.Generic;
using Kits.Waterfalls.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Río de las cascaditas (diseño en GDD § 7.4). El arroyo de la ladera este ya no va al mar por el este: en el codo
/// de las granjas gira al sur y baja por el este de la isla en tramos que cambian de ancho: el remanso (prado ancho
/// y arroyo manso), un salto bajo junto al puente del camino del pueblo vecino, las pocitas (estrechas, escalones
/// bajos), el lago de la cortina doble (con la cueva detrás de una cortina), la garganta (estrecha, entre rocas,
/// para nadar), la poza del almenara y el salto al mar por el acantilado este. La aldea del mar queda lejos.
///
/// · El tramo viejo del arroyo, del codo al mar, se retira (registro de retirados) y su cauce se rellena; la lámina
///   del arroyo se rehace cortada en el codo, con el mismo nombre, para que el agua para nadar y las cascadas de
///   arriba sigan saliendo de ella.
/// · Todo sale de las tablas de este archivo.
public static partial class VestidoDelMundo
{
    // ── Tablas del río ───────────────────────────────────────────────────────────────────────

    /// Cauce, de arriba abajo: planta, cota del agua, medio ancho del agua, llano a cada orilla, alto de las paredes
    /// de roca sobre el agua (0 sin paredes) y hondura. Un salto es un par de puntos muy juntos con mucha caída.
    private static readonly (float x, float z, float agua, float medio, float llano, float pared, float hondo)[] CauceDelRio =
    {
        (286f, 193f, 37.4f, 2.5f, 1f, 0f, 1.2f),
        (297f, 170f, 37.0f, 3f, 7f, 0f, 1.2f),
        (301f, 140f, 36.8f, 3.5f, 9f, 0f, 1.2f),      // remanso
        (297f, 114f, 36.6f, 3f, 6f, 0f, 1.2f),
        (292f, 101f, 36.5f, 3f, 2f, 1.5f, 1.4f),      // salto del puente
        (291.6f, 98f, 33.0f, 3.5f, 2f, 1.5f, 1.8f),
        (290f, 92f, 32.9f, 3.5f, 2f, 0f, 1.6f),       // puente
        (288f, 75f, 32.6f, 3.5f, 2f, 0f, 1.4f),
        (287f, 64f, 32.4f, 4f, 1.5f, 1f, 1.8f),       // pocitas
        (286.5f, 61.5f, 31.2f, 4.5f, 1.5f, 1f, 2.2f),
        (285f, 50f, 31.1f, 5f, 1.5f, 1f, 2.2f),
        (284.5f, 47.5f, 29.9f, 4.5f, 1.5f, 1f, 2.2f),
        (283f, 36f, 29.8f, 5f, 1.5f, 1f, 2.2f),
        (282.5f, 33.5f, 28.6f, 4.5f, 1.5f, 1f, 2.2f),
        (281f, 22f, 28.5f, 5f, 1.5f, 1f, 2.2f),
        (280f, 15f, 28.4f, 5f, 1f, 1.5f, 1.6f),       // cortina doble
        (279.6f, 12f, 24.6f, 6f, 0f, 0f, 3.5f),       // lago
        (276f, -5f, 24.6f, 8f, 0f, 0f, 3.5f),
        (273f, -46f, 24.6f, 6f, 0f, 0f, 3.0f),
        (272.5f, -54f, 24.5f, 3.5f, 1f, 2f, 1.6f),    // salida del lago
        (272f, -57f, 21.0f, 3.5f, 0.5f, 6f, 2.4f),    // garganta
        (264f, -78f, 20.8f, 3.5f, 0.5f, 6f, 2.4f),
        (269f, -98f, 20.6f, 3f, 0.5f, 7f, 2.4f),
        (258f, -118f, 20.5f, 3.5f, 0.5f, 8f, 2.4f),
        (266f, -140f, 20.3f, 3f, 0.5f, 8f, 2.4f),
        (258f, -160f, 20.1f, 3.5f, 0.5f, 7f, 2.4f),
        (272f, -185f, 19.9f, 3.5f, 0.5f, 6f, 2.4f),
        (290f, -208f, 19.6f, 4f, 0.5f, 5f, 2.2f),
        (325f, -235f, 19.4f, 4f, 1.5f, 3f, 1.8f),
        (348f, -245f, 19.3f, 5f, 1.5f, 3f, 2.0f),     // poza del almenara
        (372f, -251f, 19.1f, 5f, 1.5f, 4f, 2.0f),
        (385f, -253f, 19.0f, 4f, 1f, 5f, 1.6f),
        (392f, -254.5f, 18.9f, 3.5f, 1f, 5f, 1.4f),   // salto al mar
        (396f, -255.5f, 0.3f, 4f, 0f, 0f, 3f),
        (410f, -258f, 0.0f, 6f, 0f, 0f, 3f),
    };
    /// Saltos (índice del labio en CauceDelRio), su nombre y si su cortina va partida en dos.
    private static readonly (int labio, string nombre, bool doble)[] SaltosDelRio =
    {
        (4, "Salto del puente", false), (8, "Pocita 1", false), (10, "Pocita 2", false), (12, "Pocita 3", false),
        (15, "Cortina doble del lago", true), (19, "Salida del lago", false), (32, "Salto al mar", false),
    };
    private const float TaludDelRio = 9f;

    /// Pozas anchas (centro, radios, cota del agua y hondura): el lago y la poza del almenara.
    private static readonly (string nombre, Vector2 centro, float rx, float rz, float agua, float hondo)[] PozasDelRio =
    {
        ("Lago de la cortina doble", new Vector2(276f, -17f), 20f, 29f, 24.6f, 3.5f),
        ("Poza del almenara", new Vector2(360f, -248f), 12f, 9f, 19.2f, 3f),
    };
    /// Al pie del salto al mar, el fondo baja para que el agua caiga al mar y no a la arena.
    private static readonly Vector2 PieDelSaltoAlMar = new Vector2(403f, -256f);
    private const float RadioDelPieDelSalto = 10f;

    /// Cueva detrás de la cortina oeste del lago: boca, túnel y sala, entrando en la ladera hacia el noroeste.
    private static readonly Vector2 BocaDeLaCuevaDelLago = new Vector2(276.6f, 13.6f), HaciaDentroDeLaCuevaDelLago = new Vector2(-0.45f, 0.89f);
    private static readonly char[] PiezasDeLaCuevaDelLago = { 'B', 'T', 'S' };
    private const float SueloDeLaCuevaDelLago = 25.0f;

    /// Tramo viejo del arroyo, del codo al mar (centro de su cinta): su cauce se rellena.
    private static readonly Vector2[] TramoViejoDelArroyo =
    {
        new Vector2(292f, 192f), new Vector2(301.5f, 189.6f), new Vector2(317.3f, 186.9f), new Vector2(333.4f, 184.8f),
        new Vector2(350f, 183.6f), new Vector2(366.8f, 182.1f), new Vector2(383.6f, 180.2f), new Vector2(400.5f, 177.6f),
        new Vector2(417.7f, 174.3f), new Vector2(431.5f, 171f),
    };
    private const string NombreDelArroyo = "Arroyo de la ladera este";
    private const string MotivoTramoViejo = "lámina del arroyo de la ladera este: se rehace cortada en el codo (el río de las cascaditas sigue al sur)";

    /// Puente del camino del pueblo vecino: copia del puente del arroyo de arriba.
    private const string PuenteDeReferencia = "Puente_camino_2 — tablero de 4 m y accesos a ras";
    private static readonly Vector2 SitioDelPuente = new Vector2(290.3f, 93f);
    private static readonly Vector2 DireccionDelArroyoEnElPuenteDeReferencia = new Vector2(0.86f, -0.52f);
    private const float AguaEnElPuenteDeReferencia = 42.1f;

    // ── Geometría ────────────────────────────────────────────────────────────────────────────

    private struct PuntoDelCauce
    {
        public float Distancia, S, Agua, Medio, Llano, Pared, Hondo;
        public Vector2 Direccion;
    }

    /// Lo que pide el cauce en (x, z): el punto más cercano de su eje con sus medidas interpoladas.
    private static PuntoDelCauce EnElCauce(float x, float z)
    {
        var p = new Vector2(x, z);
        var r = new PuntoDelCauce { Distancia = float.MaxValue };
        float acumulado = 0f;
        for (int j = 0; j + 1 < CauceDelRio.Length; j++)
        {
            var c0 = CauceDelRio[j];
            var c1 = CauceDelRio[j + 1];
            var a = new Vector2(c0.x, c0.z);
            Vector2 ab = new Vector2(c1.x, c1.z) - a;
            float l = ab.magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (l * l));
            float d = Vector2.Distance(p, a + ab * t);
            if (d < r.Distancia)
            {
                r.Distancia = d;
                r.S = acumulado + t * l;
                r.Agua = Mathf.Lerp(c0.agua, c1.agua, t);
                r.Medio = Mathf.Lerp(c0.medio, c1.medio, t);
                r.Llano = Mathf.Lerp(c0.llano, c1.llano, t);
                r.Pared = Mathf.Lerp(c0.pared, c1.pared, t);
                r.Hondo = Mathf.Lerp(c0.hondo, c1.hondo, t);
                r.Direccion = ab / l;
            }
            acumulado += l;
        }
        return r;
    }

    private static Vector3 PuntoDelCauceEn(int j) => new Vector3(CauceDelRio[j].x, CauceDelRio[j].agua, CauceDelRio[j].z);

    // ── Relieve ──────────────────────────────────────────────────────────────────────────────

    /// Talla el cauce (lecho, orillas llanas y paredes de roca), las pozas, el pie del salto al mar y la repisa de la
    /// cueva, y rellena el cauce viejo.
    private static void EsculpirElRio(Relieve r, System.Func<float, float, float> alturaOriginal)
    {
        // Cauce viejo: a la altura de sus orillas.
        var relleno = new Vector3[TramoViejoDelArroyo.Length];
        for (int j = 0; j < relleno.Length; j++)
        {
            Vector2 c = TramoViejoDelArroyo[j];
            var alrededor = new List<float>();
            for (int a = 0; a < 12; a++)
            {
                float ang = a * Mathf.PI / 6f;
                alrededor.Add(alturaOriginal(c.x + 9f * Mathf.Cos(ang), c.y + 9f * Mathf.Sin(ang)));
            }
            alrededor.Sort();
            relleno[j] = new Vector3(c.x, alrededor[alrededor.Count / 2], c.y);
        }
        Franja(r, relleno, 8f, 4f);

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var c in CauceDelRio) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); }
        float m = 40f;
        r.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            PuntoDelCauce c = EnElCauce(x, z);
            float orilla = c.Medio + c.Llano;
            float alcance = orilla + (c.Pared > 0f ? 2f + c.Pared * 1.2f : 0f) + TaludDelRio;
            if (c.Distancia > alcance) return;
            float h = r.H[k, i], objetivo, peso;
            if (c.Distancia <= c.Medio)
            {
                float q = c.Distancia / c.Medio;
                objetivo = c.Agua - c.Hondo * (1f - q * q) - 0.2f;
                peso = 1f;
            }
            else if (c.Distancia <= orilla)
            {
                objetivo = c.Agua + 0.5f;
                peso = 1f;
            }
            else if (c.Pared > 0f && c.Distancia <= orilla + 2f + c.Pared * 1.2f)
            {
                // Pared de roca: sube de golpe y baja después hacia lo que hubiera.
                float e = c.Distancia - orilla;
                float alto = c.Agua + 0.5f + c.Pared * Suave(e / 1.5f);
                objetivo = e < 2f ? alto : Mathf.Max(h, alto - (e - 2f) * 0.9f);
                peso = 1f;
            }
            else
            {
                float desde = orilla + (c.Pared > 0f ? 2f + c.Pared * 1.2f : 0f);
                objetivo = c.Pared > 0f ? Mathf.Max(h, c.Agua + 0.5f) : c.Agua + 0.5f;
                peso = 1f - Suave((c.Distancia - desde) / TaludDelRio);
            }
            r.H[k, i] = Mathf.Lerp(h, objetivo, peso);
        });

        foreach (var (_, centro, rx, rz, agua, hondo) in PozasDelRio)
        {
            float margen = 7f;
            r.Recorrer(centro.x - rx - margen, centro.y - rz - margen, centro.x + rx + margen, centro.y + rz + margen, (i, k, x, z) =>
            {
                float q = Mathf.Sqrt(Sqr((x - centro.x) / rx) + Sqr((z - centro.y) / rz));
                float borde = (q - 1f) * Mathf.Min(rx, rz);
                if (borde > margen) return;
                if (q < 1f) r.H[k, i] = Mathf.Min(r.H[k, i], agua - hondo * Suave((1f - q) * 3f) - 0.2f);
                else r.H[k, i] = Mathf.Lerp(agua + 0.5f, r.H[k, i], Suave(borde / margen));
            });
        }
        r.Recorrer(PieDelSaltoAlMar.x - RadioDelPieDelSalto, PieDelSaltoAlMar.y - RadioDelPieDelSalto,
                   PieDelSaltoAlMar.x + RadioDelPieDelSalto, PieDelSaltoAlMar.y + RadioDelPieDelSalto, (i, k, x, z) =>
        {
            float d = Vector2.Distance(new Vector2(x, z), PieDelSaltoAlMar);
            if (d < RadioDelPieDelSalto) r.H[k, i] = Mathf.Min(r.H[k, i], Mathf.Lerp(-3f, 0.5f, Suave(d / RadioDelPieDelSalto)));
        });
        // Del pie del salto al mar, un canal para que el agua no quede encerrada por la arena.
        Franja(r, new[] { new Vector3(PieDelSaltoAlMar.x, -2.5f, PieDelSaltoAlMar.y), new Vector3(PieDelSaltoAlMar.x + 26f, -3f, PieDelSaltoAlMar.y - 4f) }, 9f, 4f);
        // Repisa seca delante de la boca de la cueva, detrás de la cortina.
        Rellano(r, BocaDeLaCuevaDelLago - HaciaDentroDeLaCuevaDelLago * 1.5f, 2.6f, 2.6f, SueloDeLaCuevaDelLago, 1.2f);
    }

    // ── Suelo ────────────────────────────────────────────────────────────────────────────────

    /// Cantos en el lecho, arena en las orillas de las pozas, roca en las paredes, flores en el remanso y hierba sobre
    /// el cauce viejo.
    private static void PintarElRio(Lienzo l, List<string> informe)
    {
        int cantos = l.Capa(CapaTierraPiedras), arena = l.Capa(CapaArena), roca = l.Capa(CapaRoca);
        int hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores);
        int pintadas = 0;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var c in CauceDelRio) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); }
        l.Recorrer(minX - 30f, minZ - 30f, maxX + 30f, maxZ + 30f, (i, k, x, z) =>
        {
            PuntoDelCauce c = EnElCauce(x, z);
            int capa = -1;
            bool enPoza = false;
            foreach (var (_, centro, rx, rz, _, _) in PozasDelRio)
                if (Mathf.Sqrt(Sqr((x - centro.x) / rx) + Sqr((z - centro.y) / rz)) < 1.15f) enPoza = true;
            if (enPoza) capa = arena;
            else if (c.Distancia <= c.Medio + 0.6f) capa = cantos;
            else if (c.Distancia <= c.Medio + c.Llano + 2f + c.Pared * 1.2f)
                capa = l.Pendiente(x, z) > 34f ? roca : c.Llano > 5f ? (Ruido.Fbm(x, z, 8f, 821) > 0.45f ? flores : hierba) : c.Pared > 0f ? hierba : cantos;
            if (capa < 0) return;
            l.Mezclar(i, k, capa, 1f, duro: capa != hierba && capa != flores);
            pintadas++;
        });
        // Cauce viejo: hierba.
        PintarSenda(l, TramoViejoDelArroyo, 9f, 822, null, CapaHierba, CapaHierba, 0.2f);
        informe.Add($"Suelo: río de las cascaditas pintado ({pintadas} celdas) y cauce viejo del arroyo cubierto de hierba.");
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    private static void RioDeLasCascaditas(Obra o, Transform grupo)
    {
        CortarElArroyoEnElCodo(o, grupo);
        AguaDelRio(o, Grupo(grupo, "Agua"));
        SaltosDeAgua(o, Grupo(grupo, "Cascadas"));
        CuevaDelLago(o, Grupo(grupo, "Cueva de la cortina"));
        PuenteDelCaminoDelVecino(o, grupo);
    }

    /// Retira la lámina del arroyo y pone una copia cortada en el codo (con el mismo nombre y material).
    private static void CortarElArroyoEnElCodo(Obra o, Transform grupo)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform original = null;
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(false))
                if (t.name == NombreDelArroyo && t.GetComponent<MeshFilter>() != null && !t.IsChildOf(o.Raiz)) { original = t; break; }
        if (original == null) { o.Informe.Add("  Río de las cascaditas: no está la lámina del arroyo de la ladera este; no se corta."); return; }

        Mesh malla = original.GetComponent<MeshFilter>().sharedMesh;
        Matrix4x4 aMundo = original.localToWorldMatrix;
        Vector3[] v = malla.vertices;
        var codo = new Vector2(CauceDelRio[0].x, CauceDelRio[0].z);
        int pares = v.Length / 2, corte = pares - 1;
        float mejor = float.MaxValue;
        for (int j = 0; j < pares; j++)
        {
            Vector3 c = aMundo.MultiplyPoint3x4((v[2 * j] + v[2 * j + 1]) * 0.5f);
            float d = Vector2.Distance(new Vector2(c.x, c.z), codo);
            if (d < mejor) { mejor = d; corte = j; }
        }
        var puntos = new Vector3[(corte + 1) * 2];
        for (int j = 0; j < puntos.Length; j++) puntos[j] = aMundo.MultiplyPoint3x4(v[j]);
        Material material = original.GetComponent<MeshRenderer>() != null ? original.GetComponent<MeshRenderer>().sharedMaterial : null;
        Retirar(escena, original, MotivoTramoViejo, o);
        LaminaDeAgua(o, grupo, NombreDelArroyo, puntos, 0f, material);
        o.Informe.Add($"  Río de las cascaditas: lámina del arroyo cortada en el codo ({corte + 1} de {pares} pares).");
    }

    /// Láminas de agua de dibujo por tramos (entre saltos) y en las pozas, con sus cajas para nadar.
    private static void AguaDelRio(Obra o, Transform grupo)
    {
        Material agua = Aguas.MaterialDelAguaDeLaLaguna();
        var labios = new HashSet<int>();
        foreach (var (labio, _, _) in SaltosDelRio) labios.Add(labio);
        int tramo = 0, cajas = 0, desde = 0;
        for (int j = 1; j < CauceDelRio.Length; j++)
        {
            bool corta = labios.Contains(j - 1) || j == CauceDelRio.Length - 1;
            if (!corta) continue;
            int hasta = labios.Contains(j - 1) ? j - 1 : j;
            if (hasta > desde)
            {
                var recorrido = new List<Vector3>();
                var anchos = new List<float>();
                for (int u = desde; u <= hasta; u++) { recorrido.Add(PuntoDelCauceEn(u) + Vector3.up * 0.02f); anchos.Add(CauceDelRio[u].medio * 2f + 1.4f); }
                LaminaDeAgua(o, grupo, $"Río de las cascaditas — tramo {++tramo}", CintaDeAnchos(recorrido, anchos), 0f, agua);
                for (int u = desde; u < hasta; u++)
                {
                    Vector3 a = PuntoDelCauceEn(u), b = PuntoDelCauceEn(u + 1);
                    var dir = new Vector2(b.x - a.x, b.z - a.z);
                    CajaParaNadar(o, grupo, $"Agua para nadar — río {tramo}.{u - desde + 1}", new Vector3((a.x + b.x) * 0.5f, Mathf.Min(a.y, b.y), (a.z + b.z) * 0.5f),
                                  new Vector2(Mathf.Max(CauceDelRio[u].medio, CauceDelRio[u + 1].medio) * 2f, dir.magnitude + 0.8f), Rumbo(dir),
                                  Mathf.Max(CauceDelRio[u].hondo, CauceDelRio[u + 1].hondo) + 0.6f);
                    cajas++;
                }
            }
            desde = j;
        }
        foreach (var (nombre, centro, rx, rz, cota, hondo) in PozasDelRio)
        {
            LaminaDeAgua(o, grupo, nombre, Disco(centro, rx + 1.5f, rz + 1.5f, 28), cota, agua);
            CajaParaNadar(o, grupo, "Agua para nadar — " + nombre, new Vector3(centro.x, cota, centro.y), new Vector2(rx * 2f, rz * 2f), 0f, hondo + 1f);
            cajas++;
        }
        o.Informe.Add($"  Río de las cascaditas: {tramo} tramos de agua y {PozasDelRio.Length} pozas, {cajas} cajas para nadar.");
    }

    /// Cinta de agua con un ancho por punto.
    private static Vector3[] CintaDeAnchos(IList<Vector3> recorrido, IList<float> anchos)
    {
        var v = new Vector3[recorrido.Count * 2];
        for (int i = 0; i < recorrido.Count; i++)
        {
            Vector3 d = recorrido[Mathf.Min(i + 1, recorrido.Count - 1)] - recorrido[Mathf.Max(i - 1, 0)];
            var n = new Vector3(-d.z, 0f, d.x).normalized * (anchos[i] * 0.5f);
            v[2 * i] = recorrido[i] - n;
            v[2 * i + 1] = recorrido[i] + n;
        }
        return v;
    }

    private static void SaltosDeAgua(Obra o, Transform grupo)
    {
        var (cortina, espuma) = CascadasDelJuego.Materiales();
        foreach (var (labio, nombre, doble) in SaltosDelRio)
        {
            Vector3 arriba = PuntoDelCauceEn(labio), pie = PuntoDelCauceEn(labio + 1);
            Vector2 dir = new Vector2(pie.x - arriba.x, pie.z - arriba.z).normalized;
            float ancho = CauceDelRio[labio].medio * 2f * 0.8f;
            if (!doble) SaltoDeAgua(grupo, nombre, arriba, dir, pie.y, ancho, 0f, cortina, espuma);
            else
            {
                float cada = ancho * 0.42f;
                SaltoDeAgua(grupo, nombre + " (oeste)", arriba, dir, pie.y, cada, -ancho * 0.28f, cortina, espuma);
                SaltoDeAgua(grupo, nombre + " (este)", arriba, dir, pie.y, cada, ancho * 0.28f, cortina, espuma);
            }
        }
        o.Informe.Add($"  Río de las cascaditas: {SaltosDelRio.Length} saltos (el del lago con cortina doble; el del mar de {PuntoDelCauceEn(32).y:0} m).");
    }

    /// Una caída: el agua sale del labio, se separa de la pared y cae a «abajo». «lado» la mueve a la izquierda de
    /// la marcha (positivo) o a la derecha.
    private static void SaltoDeAgua(Transform grupo, string nombre, Vector3 labio, Vector2 dir, float abajo, float ancho, float lado,
                                    Material cortina, Material espuma)
    {
        var izquierda = new Vector2(-dir.y, dir.x);
        Vector3 En(float adelante, float y) { Vector2 p = new Vector2(labio.x, labio.z) + dir * adelante + izquierda * lado; return new Vector3(p.x, y, p.y); }
        float alto = labio.y - abajo, separacion = Mathf.Clamp(alto * 0.25f, 1.2f, 4f);
        var puntos = new List<Vector3> { En(-3f, labio.y), En(-0.8f, labio.y), En(0.4f, labio.y - 0.15f), En(1.2f, labio.y - 0.6f) };
        int tramos = Mathf.Max(4, Mathf.CeilToInt(alto / 1.2f));
        for (int i = 1; i <= tramos; i++)
        {
            float f = i / (float)tramos;
            puntos.Add(En(Mathf.Lerp(1.2f, separacion, Mathf.Sqrt(f)), Mathf.Lerp(labio.y - 0.6f, abajo, f)));
        }
        puntos.Add(En(separacion + 2f, abajo));
        puntos.Add(En(separacion + 5f, abajo));
        var anchos = new List<float>();
        foreach (Vector3 _ in puntos) anchos.Add(ancho);
        WaterfallBuilder.Build(grupo, nombre, puntos, anchos, cortina, espuma, 0.05f);
    }

    private static void CuevaDelLago(Obra o, Transform grupo)
    {
        Vector2 a = BocaDeLaCuevaDelLago, dir = HaciaDentroDeLaCuevaDelLago.normalized;
        float largo = LargoDeLasPiezas(PiezasDeLaCuevaDelLago);
        float Suelo(float d) => SueloDeLaCuevaDelLago;
        DespejarLaTraza(o, a, a + dir * largo, 5.5f);
        int abiertas = AbrirElTerreno(o, a, a + dir * largo, Suelo, out float aire);
        PonerPiezasDeTunel(o, grupo, "Cueva de la cortina", a, dir, PiezasDeLaCuevaDelLago, Suelo);
        o.Informe.Add($"  Cueva de la cortina: boca en ({a.x:0}, {a.y:0}) a {SueloDeLaCuevaDelLago:0.0} m, {largo:0} m hacia dentro; {abiertas} celdas abiertas" +
                      (aire > 0f ? $"; OJO: {aire:0} m al aire" : "") + ".");
    }

    /// Copia el puente del arroyo de arriba sobre el río, donde lo cruza el camino del pueblo vecino.
    private static void PuenteDelCaminoDelVecino(Obra o, Transform grupo)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform hitos = BuscarGrupo(escena, GrupoDeHitos);
        Transform referencia = hitos != null ? hitos.Find(PuenteDeReferencia) : null;
        if (referencia == null) { o.Informe.Add("  Río de las cascaditas: no está el puente de referencia; el camino cruza sin puente."); return; }
        var renderers = referencia.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        Vector3 pivote = b.center;

        PuntoDelCauce c = EnElCauce(SitioDelPuente.x, SitioDelPuente.y);
        float giro = Rumbo(c.Direccion) - Rumbo(DireccionDelArroyoEnElPuenteDeReferencia);
        Quaternion q = Quaternion.Euler(0f, giro, 0f);
        var destino = new Vector3(SitioDelPuente.x, pivote.y + (c.Agua - AguaEnElPuenteDeReferencia), SitioDelPuente.y);
        GameObject copia = Object.Instantiate(referencia.gameObject, grupo);
        copia.name = "Puente del camino del pueblo vecino";
        copia.transform.SetPositionAndRotation(destino + q * (referencia.position - pivote), q * referencia.rotation);
        copia.transform.localScale = referencia.lossyScale;
        o.Informe.Add($"  Río de las cascaditas: puente del camino del pueblo vecino en ({SitioDelPuente.x:0}, {SitioDelPuente.y:0}) sobre el agua a {c.Agua:0.0} m.");
    }
}
