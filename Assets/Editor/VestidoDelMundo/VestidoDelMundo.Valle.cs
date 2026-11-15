using System.Collections.Generic;
using System.IO;
using Kits.Waterfalls.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Valle del prólogo en MainWorld (INC-1200; maqueta «El valle del prólogo — maqueta del rediseño»). Un valle
/// colgado en la costa suroeste: cuenco cerrado por laderas (la cresta oeste, por donde baja el Mago Oscuro, es la más
/// alta), un ibón arriba, el río que cruza el pueblo de norte a sur y cae al mar por el acantilado (la cascada que
/// atraviesa la cámara en la apertura), huertos en bancales en la orilla este y árboles en bosquetes por las laderas.
/// Se entra a pie por el collado del norte.
///
/// · El pueblo del prólogo (Prologo_Valle, alrededor de (6000, 100, 6000)) cae en el llano del valle sumándole
///   DesplazamientoDelPrologo: el trazado (calle, plaza, río, puente, cresta oeste) se ha medido sobre sus marcas para
///   que la escenificación del guion siga valiendo.
/// · Todo sale de las tablas de este archivo; el relieve se aplica encima del de la montaña en cada vestido.
public static partial class VestidoDelMundo
{
    // ── Tablas del valle ─────────────────────────────────────────────────────────────────────

    /// Lo que se suma a una posición de Prologo_Valle para llevarla al valle de MainWorld.
    public static readonly Vector3 DesplazamientoDelPrologo = new Vector3(-6170f, -66f, -6365f);
    private const float SueloDelValle = 34f;
    private static readonly Vector2 CentroDelValle = new Vector2(-165f, -360f);
    /// Cuenco del valle: por rumbo (grados desde el este, contra el reloj), distancia del centro a la cresta y cota de
    /// la cresta. Oeste: la cresta por donde baja el Mago Oscuro (sus marcas de ladera y colina caen en el perfil). Sur:
    /// el borde del acantilado sobre el mar.
    private static readonly (float rumbo, float radio, float cresta)[] CrestasDelValle =
    {
        (0f, 85f, 44f), (45f, 90f, 47f), (90f, 85f, 43.7f), (135f, 95f, 56f), (180f, 80f, 63.7f),
        (225f, 82f, 50f), (270f, 65f, 35.5f), (315f, 80f, 42f), (360f, 85f, 44f),
    };
    /// Perfil de la ladera desde el centro (fracción del radio, fracción de la subida a la cresta): llano del pueblo,
    /// subida fuerte a media ladera y cresta redondeada. Medido sobre las marcas de la ladera oeste.
    private static readonly (float c, float y)[] PerfilDeLaLadera =
        { (0f, 0f), (0.32f, 0f), (0.46f, 0.25f), (0.59f, 0.56f), (0.71f, 0.72f), (0.85f, 0.89f), (1f, 1f) };
    /// El resto del cuenco, más suave (prado que sube poco a poco y cresta redondeada).
    private static readonly (float c, float y)[] PerfilDelCuenco =
        { (0f, 0f), (0.34f, 0f), (0.5f, 0.12f), (0.66f, 0.38f), (0.82f, 0.72f), (0.93f, 0.93f), (1f, 1f) };
    private const float BajadaPorFuera = 42f, BajadaAlMar = 11f;
    private const float BordeDelAcantilado = -417f;
    /// Hasta dónde llega el relieve del valle (para recorrer el heightmap).
    private static readonly Rect DominioDelValle = Rect.MinMaxRect(-300f, -452f, -32f, -228f);
    private const float FundidoDelValle = 0f, FundidoDelValleEste = 0f;

    /// Río del valle, de arriba abajo: planta, cota del agua, medio ancho, llano a cada orilla, pared y hondura.
    private static readonly (float x, float z, float agua, float medio, float llano, float pared, float hondo)[] CauceDelValle =
    {
        (-141.5f, -320f, 38.4f, 1.8f, 0.8f, 0f, 0.8f),   // salida del ibón
        (-142.4f, -328f, 38.3f, 2.0f, 0.8f, 1.0f, 0.9f), // saltito
        (-142.8f, -330.6f, 34.6f, 2.4f, 0.8f, 0.8f, 1.0f),
        (-145f, -347f, 33.0f, 2.8f, 0.8f, 0f, 1.1f),
        (-147f, -364.6f, 32.0f, 3.2f, 0.8f, 0f, 1.3f),   // puente del prólogo
        (-149f, -390f, 31.8f, 3.0f, 0.8f, 0f, 1.2f),
        (-151f, -410f, 31.6f, 2.8f, 0.8f, 0.6f, 1.1f),
        (-152f, -418.5f, 31.5f, 2.8f, 0.4f, 1.2f, 0.9f), // labio de la cascada
        (-152.4f, -421f, 0.4f, 4.5f, 0f, 0f, 2.2f),      // pie
        (-153f, -438f, 0.0f, 6f, 0f, 0f, 2.5f),
        (-155f, -452f, 0.0f, 7f, 0f, 0f, 3f),
    };
    private static readonly (int labio, string nombre)[] SaltosDelValle = { (1, "Saltito del ibón"), (7, "Cascada del valle") };
    private const float TaludDelValle = 3.5f;
    private static readonly Vector2 PieDeLaCascadaDelValle = new Vector2(-152.6f, -428f);

    private static readonly (string nombre, Vector2 centro, float rx, float rz, float agua, float hondo) IbonDelValle =
        ("Ibón del valle", new Vector2(-140f, -311f), 13f, 8f, 38.5f, 2.4f);

    /// Bancales de huerta en la orilla este (centro, medio largo, medio ancho, cota).
    private static readonly (Vector2 c, float rx, float rz, float y)[] BancalesDelValle =
    {
        (new Vector2(-123f, -337f), 9f, 3.2f, 37.4f),
        (new Vector2(-117f, -329f), 9f, 3.2f, 39.4f),
        (new Vector2(-111f, -321f), 8f, 3.0f, 41.4f),
    };

    /// Senda del collado norte: de la llanura del interior al pueblo.
    private static readonly Vector3[] SendaDelValle =
    {
        new Vector3(-240f, 21f, -212f), new Vector3(-224f, 25f, -236f), new Vector3(-208f, 32f, -258f),
        new Vector3(-196f, 39f, -279f), new Vector3(-189f, 41.5f, -296f), new Vector3(-183f, 37f, -322f),
        new Vector3(-176f, 34.6f, -345f),
    };

    /// Calle y plaza del pueblo del prólogo (para pintar el suelo).
    private static readonly Vector2[] CalleDelValle =
        { new Vector2(-196f, -365f), new Vector2(-180f, -364.5f), new Vector2(-168.5f, -364.5f), new Vector2(-156f, -364.8f), new Vector2(-153f, -364.6f) };
    private static readonly Vector2[] CaminoDelPuente =
        { new Vector2(-141f, -364.6f), new Vector2(-132f, -364f), new Vector2(-124f, -360f), new Vector2(-120f, -345f), new Vector2(-122f, -338f) };
    private static readonly Vector2 PlazaDelValle = new Vector2(-168.5f, -359.5f);
    private const float RadioDeLaPlazaDelValle = 9f;

    /// Por donde pasa la escenificación del prólogo: aquí no se planta nada.
    private static readonly Rect EscenarioDelPrologo = Rect.MinMaxRect(-200f, -382f, -118f, -340f);
    private static readonly Rect BajadaDelMago = Rect.MinMaxRect(-252f, -376f, -180f, -356f);
    private static readonly Rect PasilloDeLaCamara = Rect.MinMaxRect(-166f, -446f, -136f, -340f);

    private const string MotivoDelValle = "vegetación o peña del mundo dentro del valle del prólogo (el valle lleva sus bosquetes)";

    // ── Relieve ──────────────────────────────────────────────────────────────────────────────

    private static float Perfil((float c, float y)[] t, float v)
    {
        if (v <= t[0].c) return t[0].y;
        for (int j = 0; j + 1 < t.Length; j++)
            if (v <= t[j + 1].c)
            {
                float f = (v - t[j].c) / (t[j + 1].c - t[j].c);
                return Mathf.Lerp(t[j].y, t[j + 1].y, Mathf.Lerp(f, Suave(f), 0.5f));
            }
        return t[t.Length - 1].y;
    }

    /// Radio y cota de la cresta en el rumbo de (x, z), con un poco de irregularidad.
    private static void CrestaEn(float x, float z, out float radio, out float cresta, out float distancia, out float rumbo)
    {
        var d = new Vector2(x - CentroDelValle.x, z - CentroDelValle.y);
        distancia = d.magnitude;
        rumbo = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        if (rumbo < 0f) rumbo += 360f;
        radio = CrestasDelValle[0].radio; cresta = CrestasDelValle[0].cresta;
        for (int j = 0; j + 1 < CrestasDelValle.Length; j++)
            if (rumbo <= CrestasDelValle[j + 1].rumbo)
            {
                float f = Suave((rumbo - CrestasDelValle[j].rumbo) / (CrestasDelValle[j + 1].rumbo - CrestasDelValle[j].rumbo));
                radio = Mathf.Lerp(CrestasDelValle[j].radio, CrestasDelValle[j + 1].radio, f);
                cresta = Mathf.Lerp(CrestasDelValle[j].cresta, CrestasDelValle[j + 1].cresta, f);
                break;
            }
        // Irregularidad en el contorno (no en el oeste, que tiene que pasar por las marcas, ni en el sur del río).
        float libre = 1f - Mathf.Max(Suave(1f - Mathf.Abs(Mathf.DeltaAngle(rumbo, 180f)) / 25f), Suave(1f - Mathf.Abs(Mathf.DeltaAngle(rumbo, 262f)) / 14f));
        float ruido = Ruido.Fbm(rumbo * 0.9f, 7.3f, 9f, 1213) - 0.5f;
        radio *= 1f + 0.14f * ruido * libre;
        cresta += 6f * (Ruido.Fbm(rumbo * 1.3f, 3.1f, 7f, 1214) - 0.5f) * libre;
    }

    /// Cota que pide el valle en (x, z) y cuánto manda sobre «natural».
    private static float CotaDelValle(float x, float z, float natural, out float peso)
    {
        CrestaEn(x, z, out float radio, out float cresta, out float dist, out float rumbo);
        float q = dist / radio;
        if (q <= 1f)
        {
            peso = 1f;
            float oeste = Suave(1f - Mathf.Abs(Mathf.DeltaAngle(rumbo, 180f)) / 35f);
            float f = Mathf.Lerp(Perfil(PerfilDelCuenco, q), Perfil(PerfilDeLaLadera, q), oeste);
            return SueloDelValle + (cresta - SueloDelValle) * f;
        }
        // Por fuera de la cresta: baja hacia lo que hubiera; si fuera hay mar, en acantilado.
        bool mar = natural < 2.5f;
        float ancho = mar ? BajadaAlMar : BajadaPorFuera;
        float e = (dist - radio) / ancho;
        peso = 1f - Suave(e);
        if (peso <= 0f) return natural;
        if (mar)
        {
            // Acantilado: casi vertical arriba, con un pie de rocas.
            float f = Mathf.Clamp01(e);
            float caida = f < 0.25f ? Mathf.Lerp(0f, 0.15f, f / 0.25f) : f < 0.7f ? Mathf.Lerp(0.15f, 0.9f, (f - 0.25f) / 0.45f) : Mathf.Lerp(0.9f, 1f, (f - 0.7f) / 0.3f);
            peso = 1f;
            return Mathf.Lerp(cresta, Mathf.Max(natural, -3f), caida);
        }
        return Mathf.Lerp(cresta, natural, Suave(e) * 0.35f + e * 0.65f > 1f ? 1f : Suave(e) * 0.35f + e * 0.65f);
    }

    /// Se llama después del relieve de la montaña: esculpe el valle encima, lo suma al cambio del relieve (para que la
    /// montaña retire lo que quede enterrado o en el aire) y actualiza la huella.
    private static void DarFormaAlValle(Terrain terreno, List<string> informe)
    {
        TerrainData datos = terreno.terrainData;
        int res = datos.heightmapResolution;
        float[,] actual = datos.GetHeights(0, 0, res, res);
        var r = new Relieve { Res = res, Origen = terreno.transform.position, Medida = datos.size, H = new float[res, res] };
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
                r.H[k, i] = r.Origen.y + actual[k, i] * r.Medida.y;
        var antes = (float[,])r.H.Clone();

        Rect d = DominioDelValle;
        r.Recorrer(d.xMin, d.yMin, d.xMax, d.yMax, (i, k, x, z) =>
        {
            float cota = CotaDelValle(x, z, r.H[k, i], out float peso);
            if (peso > 0f) r.H[k, i] = Mathf.Lerp(r.H[k, i], cota, peso);
        });
        foreach (var b in BancalesDelValle) Rellano(r, b.c, b.rx, b.rz, CotaDelBancal(b.c), 2.2f, 6f);
        EsculpirElRioDelValle(r);
        Franja(r, SendaDelValle, 3.2f, 3f);
        // Estribos del puente del prólogo: las orillas a la cota del tablero.
        Rellano(r, new Vector2(-154.8f, -364.6f), 2.2f, 3.2f, 33.25f, 2.5f);
        Rellano(r, new Vector2(-139.4f, -364.6f), 2.2f, 3.2f, 33.25f, 2.5f);
        // Orilla este por donde huye el pueblo: casi plana (en la cuesta curva el NavMesh queda por encima del suelo).
        Rellano(r, new Vector2(-126f, -366f), 12f, 13f, 35.2f, 7f, 3f);

        int tocadas = 0;
        if (s_cambioDelRelieve == null || s_cambioDelRelieve.GetLength(0) != res) s_cambioDelRelieve = new float[res, res];
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
            {
                float dif = r.H[k, i] - antes[k, i];
                if (Mathf.Abs(dif) < 0.01f) continue;
                s_cambioDelRelieve[k, i] += dif;
                actual[k, i] = Mathf.Clamp01((r.H[k, i] - r.Origen.y) / r.Medida.y);
                tocadas++;
            }
        datos.SetHeights(0, 0, actual);
        terreno.Flush();
        UnityEditor.EditorUtility.SetDirty(datos);
        string rutaHuella = Path.Combine(RutaCopias, ArchivoHuellaDelRelieve);
        if (Directory.Exists(RutaCopias)) File.WriteAllText(rutaHuella, HuellaDeAlturas(datos.GetHeights(0, 0, res, res)));
        informe.Add($"Relieve: valle del prólogo esculpido en la costa suroeste ({tocadas} muestras cambiadas).");
    }

    private static float CotaDelBancal(Vector2 c) => CotaDelValle(c.x, c.y, 0f, out _) - 0.3f;

    private struct PuntoDelRioDelValle { public float Distancia, Agua, Medio, Llano, Pared, Hondo; public Vector2 Direccion; }

    private static PuntoDelRioDelValle EnElRioDelValle(float x, float z)
    {
        var p = new Vector2(x, z);
        var r = new PuntoDelRioDelValle { Distancia = float.MaxValue };
        for (int j = 0; j + 1 < CauceDelValle.Length; j++)
        {
            var c0 = CauceDelValle[j];
            var c1 = CauceDelValle[j + 1];
            var a = new Vector2(c0.x, c0.z);
            Vector2 ab = new Vector2(c1.x, c1.z) - a;
            float l = ab.magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (l * l));
            float dist = Vector2.Distance(p, a + ab * t);
            if (dist < r.Distancia)
            {
                r.Distancia = dist;
                r.Agua = Mathf.Lerp(c0.agua, c1.agua, t);
                r.Medio = Mathf.Lerp(c0.medio, c1.medio, t);
                r.Llano = Mathf.Lerp(c0.llano, c1.llano, t);
                r.Pared = Mathf.Lerp(c0.pared, c1.pared, t);
                r.Hondo = Mathf.Lerp(c0.hondo, c1.hondo, t);
                r.Direccion = ab / l;
            }
        }
        return r;
    }

    private static void EsculpirElRioDelValle(Relieve r)
    {
        r.Recorrer(-175f, -465f, -120f, -300f, (i, k, x, z) =>
        {
            PuntoDelRioDelValle c = EnElRioDelValle(x, z);
            float orilla = c.Medio + c.Llano, alcance = orilla + (c.Pared > 0f ? 1.5f + c.Pared : 0f) + TaludDelValle;
            if (c.Distancia > alcance) return;
            float h = r.H[k, i], objetivo, peso;
            if (c.Distancia <= c.Medio)
            {
                float q = c.Distancia / c.Medio;
                objetivo = c.Agua - c.Hondo * (1f - q * q) - 0.2f;
                peso = 1f;
            }
            else if (c.Distancia <= orilla) { objetivo = c.Agua + 0.5f; peso = 1f; }
            else if (c.Pared > 0f && c.Distancia <= orilla + 1.5f + c.Pared)
            {
                float e = c.Distancia - orilla;
                objetivo = Mathf.Max(h, c.Agua + 0.5f + c.Pared * Suave(e / 1.2f));
                peso = 1f;
            }
            else
            {
                float desde = orilla + (c.Pared > 0f ? 1.5f + c.Pared : 0f);
                objetivo = c.Agua + 0.5f;
                peso = 1f - Suave((c.Distancia - desde) / TaludDelValle);
                if (h < objetivo) peso = 0f;
            }
            r.H[k, i] = Mathf.Lerp(h, objetivo, peso);
        });
        var (_, centro, rx, rz, agua, hondo) = IbonDelValle;
        const float margen = 5f;
        r.Recorrer(centro.x - rx - margen, centro.y - rz - margen, centro.x + rx + margen, centro.y + rz + margen, (i, k, x, z) =>
        {
            float q = Mathf.Sqrt(Sqr((x - centro.x) / rx) + Sqr((z - centro.y) / rz));
            float borde = (q - 1f) * Mathf.Min(rx, rz);
            if (borde > margen) return;
            if (q < 1f) r.H[k, i] = Mathf.Min(r.H[k, i], agua - hondo * Suave((1f - q) * 3f) - 0.2f);
            else r.H[k, i] = Mathf.Lerp(agua + 0.4f, r.H[k, i], Suave(borde / margen));
        });
        r.Recorrer(PieDeLaCascadaDelValle.x - 10f, PieDeLaCascadaDelValle.y - 10f, PieDeLaCascadaDelValle.x + 10f, PieDeLaCascadaDelValle.y + 10f, (i, k, x, z) =>
        {
            float dist = Vector2.Distance(new Vector2(x, z), PieDeLaCascadaDelValle);
            if (dist < 9f) r.H[k, i] = Mathf.Min(r.H[k, i], Mathf.Lerp(-3f, 0.3f, Suave(dist / 9f)));
        });
    }

    // ── Suelo ────────────────────────────────────────────────────────────────────────────────

    private static void PintarElValle(Lienzo l, List<string> informe)
    {
        int hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores), roca = l.Capa(CapaRoca), arena = l.Capa(CapaArena);
        int cantos = l.Capa(CapaTierraPiedras), tierra = l.Capa(CapaTierra);
        int pintadas = 0;
        Rect d = DominioDelValle;
        int campo = l.Capa(CapaHierbaDelCampo);
        if (campo < 0) campo = hierba;
        l.Recorrer(d.xMin, d.yMin, d.xMax, d.yMax, (i, k, x, z) =>
        {
            CrestaEn(x, z, out float radio, out _, out float dist, out _);
            float q = dist / radio;
            float h = l.Altura(x, z);
            bool acantilado = h > 1.2f && z < BordeDelAcantilado + 4f && q > 0.9f && q < 1.3f;
            if (q > 1.25f && !acantilado) return;
            float s = acantilado ? 1f : 1f - Suave((q - 1.0f) / 0.25f);
            int capa;
            bool duro = false;
            PuntoDelRioDelValle c = EnElRioDelValle(x, z);
            var (_, centro, rx, rz, _, _) = IbonDelValle;
            float qIbon = Mathf.Sqrt(Sqr((x - centro.x) / rx) + Sqr((z - centro.y) / rz));
            float pendiente = l.Pendiente(x, z);
            if (h < 1.2f) { capa = arena; duro = true; }
            else if (qIbon < 1.18f) { capa = arena; duro = true; }
            else if (c.Distancia <= c.Medio + 0.5f) { capa = cantos; duro = true; }
            else if (pendiente > 40f) { capa = roca; duro = true; }
            else if (pendiente > 30f && h > 48f) { capa = Ruido.Fbm(x, z, 6f, 1201) > 0.5f ? roca : cantos; duro = true; }
            else
            {
                bool prado = !EscenarioDelPrologo.Contains(new Vector2(x, z)) && c.Distancia < 14f && Ruido.Fbm(x, z, 4f, 1202) > 0.72f;
                capa = prado ? flores : campo;
            }
            // El llano del pueblo, sin hierba de detalle ni flores (lo pisa la escenificación del prólogo).
            if (q < 0.36f && capa == campo) duro = true;
            l.Mezclar(i, k, capa, s, duro);
            pintadas++;
        });
        foreach (var b in BancalesDelValle)
            l.Recorrer(b.c.x - b.rx, b.c.y - b.rz, b.c.x + b.rx, b.c.y + b.rz, (i, k, x, z) => { l.Mezclar(i, k, tierra, 1f, true); pintadas++; });
        l.Recorrer(PlazaDelValle.x - RadioDeLaPlazaDelValle, PlazaDelValle.y - RadioDeLaPlazaDelValle,
                   PlazaDelValle.x + RadioDeLaPlazaDelValle, PlazaDelValle.y + RadioDeLaPlazaDelValle, (i, k, x, z) =>
        {
            float dist = Vector2.Distance(new Vector2(x, z), PlazaDelValle);
            if (dist > RadioDeLaPlazaDelValle) return;
            l.Mezclar(i, k, dist < RadioDeLaPlazaDelValle - 2f ? tierra : cantos, 1f, true);
        });
        PintarSenda(l, CalleDelValle, 6.5f, 1203, null, CapaTierra, CapaTierraPiedras, 0.5f);
        PintarSenda(l, CaminoDelPuente, 2.6f, 1204, null);
        var senda = new Vector2[SendaDelValle.Length];
        for (int j = 0; j < senda.Length; j++) senda[j] = new Vector2(SendaDelValle[j].x, SendaDelValle[j].z);
        PintarSenda(l, senda, 2.8f, 1205, null);
        informe.Add($"Suelo: valle del prólogo pintado ({pintadas} celdas: prado, flores, roca en el acantilado y la cresta, cantos en el río, arena en el ibón, huertas, plaza y sendas).");
    }

    // ── Zonas ────────────────────────────────────────────────────────────────────────────────

    /// El vestido general no pone nada en el valle: el valle lleva lo suyo.
    private static void ZonasDelValle(List<Zona> libres)
    {
        libres.Add(new Zona("Valle del prólogo (vestido propio)", CentroDelValle.x, CentroDelValle.y, 100f));
        libres.Add(new Zona("Senda del collado del valle", -215f, -245f, 26f));
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    private static void ValleDelPrologo(Obra o)
    {
        Transform grupo = Grupo(o.Raiz, "Valle del prólogo");
        RetirarLoDelValle(o);
        AguaDelValle(o, Grupo(grupo, "Agua"));
        CascadasDelValle(o, Grupo(grupo, "Cascadas"));
        BancalesYHuerta(o, Grupo(grupo, "Huerta"));
        BosquetesDelValle(o, Grupo(grupo, "Bosquetes"));
        PuebloDeHoy(o, grupo);
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, "LUGAR_VALLE", "El Valle", "The Valley");
        Lugar(o, grupo, "AmbientPreset_Valle", "LUGAR_VALLE", "Valle del prólogo (vestido de MainWorld): solo presenta el lugar.",
              new Vector3(CentroDelValle.x, SueloDelValle + 10f, CentroDelValle.y), new Vector3(150f, 50f, 130f));
    }

    /// Retira (con el registro) la vegetación y las peñas del mundo que hay dentro del valle.
    private static void RetirarLoDelValle(Obra o)
    {
        Scene escena = o.Raiz.gameObject.scene;
        var fuera = new List<Transform>();
        Rect d = DominioDelValle;
        foreach (string nombre in new[] { GrupoDeRocasDelMundo, GrupoDeRocasDeCosta })
        {
            Transform g = BuscarGrupo(escena, nombre);
            if (g == null) continue;
            var piezas = new List<Transform>();
            foreach (Transform t in g)
            {
                if (t.GetComponent<Renderer>() == null && t.childCount > 0) foreach (Transform h in t) piezas.Add(h);
                else piezas.Add(t);
            }
            foreach (Transform t in piezas)
                if (t.gameObject.activeSelf && t.position.x > d.xMin - 6f && t.position.x < d.xMax + 4f && t.position.z > d.yMin - 4f && t.position.z < d.yMax + 6f)
                    fuera.Add(t);
        }
        foreach (Transform t in fuera) Retirar(escena, t, MotivoDelValle, o);
        if (fuera.Count > 0) Physics.SyncTransforms();
        o.Informe.Add($"  Valle del prólogo: {fuera.Count} piezas del mundo retiradas del valle.");
    }

    private static Vector3 PuntoDelRioDelValleEn(int j) => new Vector3(CauceDelValle[j].x, CauceDelValle[j].agua, CauceDelValle[j].z);

    private static void AguaDelValle(Obra o, Transform grupo)
    {
        Material agua = Aguas.MaterialDelAguaDeLaLaguna();
        int tramo = 0, cajas = 0;
        // Del ibón al saltito y del pie del saltito al labio de la cascada; del pie de la cascada en adelante es mar.
        foreach (var (desde, hasta) in new[] { (0, 1), (2, 7) })
        {
            var recorrido = new List<Vector3>();
            var anchos = new List<float>();
            for (int u = desde; u <= hasta; u++) { recorrido.Add(PuntoDelRioDelValleEn(u) + Vector3.up * 0.02f); anchos.Add(CauceDelValle[u].medio * 2f + 1.2f); }
            LaminaDeAgua(o, grupo, $"Río del valle — tramo {++tramo}", CintaDeAnchos(recorrido, anchos), 0f, agua);
            for (int u = desde; u < hasta; u++)
            {
                Vector3 a = PuntoDelRioDelValleEn(u), b = PuntoDelRioDelValleEn(u + 1);
                var dir = new Vector2(b.x - a.x, b.z - a.z);
                CajaParaNadar(o, grupo, $"Agua para nadar — río del valle {tramo}.{u - desde + 1}", new Vector3((a.x + b.x) * 0.5f, Mathf.Min(a.y, b.y), (a.z + b.z) * 0.5f),
                              new Vector2(Mathf.Max(CauceDelValle[u].medio, CauceDelValle[u + 1].medio) * 2f, dir.magnitude + 0.8f), Rumbo(dir),
                              Mathf.Max(CauceDelValle[u].hondo, CauceDelValle[u + 1].hondo) + 0.6f);
                cajas++;
            }
        }
        var (nombre, centro, rx, rz, cota, hondo) = IbonDelValle;
        LaminaDeAgua(o, grupo, nombre, Disco(centro, rx + 1.2f, rz + 1.2f, 28), cota, agua);
        CajaParaNadar(o, grupo, "Agua para nadar — " + nombre, new Vector3(centro.x, cota, centro.y), new Vector2(rx * 2f, rz * 2f), 0f, hondo + 1f);
        cajas++;
        o.Informe.Add($"  Valle del prólogo: {tramo} tramos de río y el ibón, {cajas} cajas para nadar.");
    }

    private static void CascadasDelValle(Obra o, Transform grupo)
    {
        var (cortina, espuma) = CascadasDelJuego.Materiales();
        foreach (var (labio, nombre) in SaltosDelValle)
        {
            Vector3 arriba = PuntoDelRioDelValleEn(labio), pie = PuntoDelRioDelValleEn(labio + 1);
            Vector2 dir = new Vector2(pie.x - arriba.x, pie.z - arriba.z).normalized;
            SaltoDeAgua(grupo, nombre, arriba, dir, pie.y, CauceDelValle[labio].medio * 2f * 0.85f, 0f, cortina, espuma);
        }
        o.Informe.Add($"  Valle del prólogo: cascada al mar de {PuntoDelRioDelValleEn(7).y - PuntoDelRioDelValleEn(8).y:0} m y saltito del ibón.");
    }

    private static void BancalesYHuerta(Obra o, Transform grupo)
    {
        var az = new System.Random(1210);
        string[] cultivo = { "Plant02_c01", "Plant02_c02", "Plant02_c03", "Plant01_c01", "Plant01_c02" };
        int puestas = 0;
        foreach (var b in BancalesDelValle)
        {
            // Muro de bancal del lado bajo (mirando al río, al oeste-suroeste).
            float yb = CotaDelBancal(b.c);
            Vector3 muro = new Vector3(b.c.x - 0.6f, yb - 0.15f, b.c.y - b.rz - 0.2f);
            if (PonZona(o, grupo, "BancalDeRisco_Ancho", "Bancal del valle", muro, 180f) != null) puestas++;
            for (float u = -b.rx + 1.2f; u <= b.rx - 1.2f; u += 1.6f)
                for (float v = -b.rz + 0.9f; v <= b.rz - 0.9f; v += 1.4f)
                {
                    var q = new Vector3(b.c.x + u + (float)az.NextDouble() * 0.3f, yb, b.c.y + v);
                    if (PonZona0(o, grupo, FK + "Vegetation/" + cultivo[az.Next(cultivo.Length)] + ".prefab", "Cultivo del valle", q,
                                 (float)az.NextDouble() * 360f, 0.8f + 0.3f * (float)az.NextDouble()) != null) puestas++;
                }
        }
        o.Informe.Add($"  Valle del prólogo: {BancalesDelValle.Length} bancales de huerta ({puestas} piezas).");
    }

    /// Árboles en bosquetes (nunca sueltos) por las laderas, fuera del escenario del prólogo y del pasillo de la
    /// cámara; frutales en corrillos junto al pueblo.
    private static void BosquetesDelValle(Obra o, Transform grupo)
    {
        string[] arboles = { "Tree02_a01", "Tree02_b01", "Tree06_a01", "Tree06_a02", "Tree06_a03", "Tree02_c01" };
        var az = new System.Random(1211);
        var senda = new Vector2[SendaDelValle.Length];
        for (int j = 0; j < senda.Length; j++) senda[j] = new Vector2(SendaDelValle[j].x, SendaDelValle[j].z);
        bool Vale(Vector2 q, float maxPendiente)
        {
            if (!DominioDelValle.Contains(q)) return false;
            if (q.y < BordeDelAcantilado + 3f) return false;
            if (EscenarioDelPrologo.Contains(q) || BajadaDelMago.Contains(q) || PasilloDeLaCamara.Contains(q)) return false;
            if (EnElRioDelValle(q.x, q.y).Distancia < 7f) return false;
            var (_, ci, rx, rz, _, _) = IbonDelValle;
            if (Mathf.Sqrt(Sqr((q.x - ci.x) / (rx + 3f)) + Sqr((q.y - ci.y) / (rz + 3f))) < 1f) return false;
            foreach (var b in BancalesDelValle) if (Mathf.Abs(q.x - b.c.x) < b.rx + 2f && Mathf.Abs(q.y - b.c.y) < b.rz + 2f) return false;
            if (DistanciaAPolilinea(q, senda) < 3.5f) return false;
            if (Vector2.Distance(q, new Vector2(-170f, -297f)) < 7f) return false;   // espera del Mago Oscuro
            float h = AlturaEn(o, q);
            if (h < 8f) return false;
            return PendienteEn(o, q) <= maxPendiente;
        }
        int bosquetes = 0, puestos = 0;
        for (int j = 0; j < 4000 && bosquetes < 26; j++)
        {
            var c = new Vector2(Mathf.Lerp(DominioDelValle.xMin, DominioDelValle.xMax, (float)az.NextDouble()),
                                Mathf.Lerp(DominioDelValle.yMin, DominioDelValle.yMax, (float)az.NextDouble()));
            // Solo en laderas (no en el llano del pueblo).
            if (AlturaEn(o, c) < SueloDelValle + 3.5f || !Vale(c, 32f)) continue;
            bosquetes++;
            int n = 4 + az.Next(5);
            for (int t = 0; t < n * 3 && n > 0; t++)
            {
                float ang = (float)az.NextDouble() * Mathf.PI * 2f, rr = 1.5f + (float)az.NextDouble() * 6.5f;
                var q = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
                if (!Vale(q, 38f)) continue;
                if (PonZona0(o, grupo, FK + "Vegetation/" + arboles[az.Next(arboles.Length)] + ".prefab", "Árbol del valle",
                             new Vector3(q.x, AlturaEn(o, q) - 0.25f, q.y), (float)az.NextDouble() * 360f, 0.42f + 0.22f * (float)az.NextDouble()) != null) puestos++;
                n--;
            }
        }
        o.Informe.Add($"  Valle del prólogo: {puestos} árboles en {bosquetes} bosquetes por las laderas.");
    }

    // ── El pueblo de hoy ─────────────────────────────────────────────────────────────────────

    /// Lo que se copia del pueblo del prólogo (Prologo_Valle) para el valle de hoy: casas con su vestido, plaza y río.
    /// Lo de la noche del prólogo (carreta, globo, fuego, sitios de los vecinos) no.
    private static readonly string[] PiezasDelPuebloDeHoy =
    {
        "ISLA_POSTGAME_MODULO/DECORADO/01_Aldea_Norte", "ISLA_POSTGAME_MODULO/DECORADO/02_Aldea_Sur",
        "ISLA_POSTGAME_MODULO/DECORADO/03_Plaza", "ISLA_POSTGAME_MODULO/DECORADO/05_Rio",
        "Vestido del pueblo (generado)/Casa_Norte_02", "Vestido del pueblo (generado)/Casa_Norte_03",
        "Vestido del pueblo (generado)/Casa_Norte_04", "Vestido del pueblo (generado)/Casa_Sur_01",
        "Vestido del pueblo (generado)/Casa_Sur_02", "Vestido del pueblo (generado)/Casa_Sur_03",
        "Vestido del pueblo (generado)/Casa_Sur_04", "Vestido del pueblo (generado)/Panaderia",
        "Vestido del pueblo (generado)/Iglesia", "Vestido del pueblo (generado)/Tablón de avisos",
    };

    /// Copia el pueblo del prólogo (ya llevado al valle) como el pueblo de hoy, sin scripts, bajo un grupo que se
    /// esconde mientras esté cargado el prólogo (ValleDelPresente).
    private static void PuebloDeHoy(Obra o, Transform padre)
    {
        Scene pv = SceneManager.GetSceneByPath("Assets/Scenes/Worlds/Prologo_Valle.unity");
        bool abierta = false;
        if (!pv.IsValid() || !pv.isLoaded)
        {
            pv = EditorSceneManager.OpenScene("Assets/Scenes/Worlds/Prologo_Valle.unity", OpenSceneMode.Additive);
            abierta = true;
        }
        try
        {
            bool enElValle = false;
            foreach (GameObject r in pv.GetRootGameObjects()) if (r.name.StartsWith("Prólogo en el valle de MainWorld")) enElValle = true;
            if (!enElValle) { o.Informe.Add("  Valle del prólogo: el prólogo aún no está en el valle; no se copia el pueblo de hoy."); return; }
            Transform hoy = Grupo(padre, "Pueblo de hoy");
            if (hoy.GetComponent<ValleDelPresente>() == null) hoy.gameObject.AddComponent<ValleDelPresente>();
            int piezas = 0;
            foreach (string ruta in PiezasDelPuebloDeHoy)
            {
                Transform t = BuscarEnEscena(pv, ruta);
                if (t == null) { o.Informe.Add($"  · pueblo de hoy: no está «{ruta}»"); continue; }
                var copia = Object.Instantiate(t.gameObject, t.position, t.rotation, hoy);
                copia.name = t.name;
                copia.transform.localScale = t.lossyScale;
                copia.SetActive(true);
                foreach (var mb in copia.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                piezas += copia.GetComponentsInChildren<Renderer>(true).Length;
            }
            o.Informe.Add($"  Valle del prólogo: pueblo de hoy copiado del prólogo ({piezas} piezas con malla; se esconde mientras el prólogo está cargado).");
        }
        finally
        {
            if (abierta) EditorSceneManager.CloseScene(pv, true);
        }
    }

    private static Transform BuscarEnEscena(Scene escena, string ruta)
    {
        string[] partes = ruta.Split('/');
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            if (r.name != partes[0]) continue;
            Transform t = r.transform;
            for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
            if (t != null) return t;
        }
        return null;
    }
}
