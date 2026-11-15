using System.Collections.Generic;
using UnityEngine;

/// Macizo del Norte y ruta de cumbres (diseño en GDD § 7.4). La sierra de Risco deja de ser una pared delgada: su
/// cara norte baja en falda ancha y, detrás, crece un macizo con su propia sierra (picos de 120 a 150 m). Entre las
/// dos sierras, la pradera alta: el camino entre montañas. A ella baja desde Risco la senda del collado; al este,
/// el ibón de montaña; al norte, una senda en zetas sube a la cumbre del mirador. En la costa norte, una playita
/// escondida a la que solo se llega planeando desde la cumbre o nadando desde la cala.
///
/// · Todo sale de las tablas de este archivo. El relieve se levanta antes que el resto del nivel (el camino, la
///   garganta y la cala lo cortan después).
public static partial class VestidoDelMundo
{
    // ── Tablas del macizo ────────────────────────────────────────────────────────────────────

    /// Costa del macizo (planta x, z), en orden. El tramo sur va por dentro de la sierra vieja: ahí manda la sierra.
    /// La muesca del norte es la playita.
    private static readonly Vector2[] CostaDelMacizo =
    {
        new Vector2(-60f, 500f), new Vector2(-64f, 520f), new Vector2(-78f, 545f), new Vector2(-76f, 580f),
        new Vector2(-55f, 606f), new Vector2(-15f, 622f), new Vector2(40f, 630f), new Vector2(80f, 626f),
        new Vector2(92f, 612f), new Vector2(112f, 612f), new Vector2(124f, 604f),
        new Vector2(140f, 590f), new Vector2(155f, 572f), new Vector2(172f, 552f), new Vector2(205f, 548f),
        new Vector2(240f, 532f), new Vector2(262f, 505f), new Vector2(270f, 470f), new Vector2(250f, 440f),
        new Vector2(-40f, 440f),
    };

    /// Lomas del macizo (x, cota de la cima, z) y su radio: montes redondeados como los de las islas, que caen en
    /// curva suave hasta la meseta. La primera es la cumbre del mirador.
    private static readonly (Vector3 cima, float radio)[] LomasDelMacizo =
    {
        (new Vector3(40f, 118f, 568f), 85f), (new Vector3(-30f, 106f, 562f), 70f), (new Vector3(115f, 110f, 562f), 70f),
        (new Vector3(225f, 120f, 505f), 80f), (new Vector3(185f, 104f, 540f), 60f),
    };
    /// El macizo es un monte redondeado, como las islas: sube desde la costa con esta altura y este alcance
    /// (metros desde la orilla hasta lo alto) y encima lleva las lomas.
    private const float AltoDelMacizo = 82f, AlcanceDelMacizo = 140f;
    /// Tramos de la costa del macizo que son orilla de verdad (de CostaDelMacizo[0] a este índice); el resto del
    /// contorno va por dentro de la sierra vieja.
    private const int UltimoPuntoDeLaOrilla = 17;

    /// Eje de la pradera alta, de oeste a este (acaba en el ibón).
    private static readonly Vector2[] EjeDelValle =
    {
        new Vector2(-45f, 522f), new Vector2(0f, 526f), new Vector2(50f, 528f), new Vector2(100f, 524f),
        new Vector2(140f, 518f), new Vector2(176f, 515f),
    };
    /// Cota y medio ancho de la pradera a lo largo del eje (metros desde el oeste): se abre y se estrecha.
    private static readonly (float s, float cota, float medioAncho)[] PerfilDeLaPradera =
    {
        (0f, 78f, 12f), (45f, 80f, 22f), (100f, 82f, 28f), (150f, 80f, 14f), (190f, 76f, 16f), (230f, 74f, 16f),
    };
    private const float PendienteDeLasLaderas = 1.6f;
    /// Ibón de montaña: en la pradera, donde el macizo es más alto.
    private static readonly Vector2 CentroDelIbon = new Vector2(120f, 527f);
    private const float RadioXDelIbon = 17f, RadioZDelIbon = 10f, HondoDelIbon = 3f;
    private static float CotaDelIbon
    {
        get
        {
            EnElValle(CentroDelIbon.x, CentroDelIbon.y, out float s, out _);
            return Mathf.Min(PraderaEn(s).cota, AlturaDeLaLoma(CentroDelIbon.x, CentroDelIbon.y)) - 1.5f;
        }
    }

    /// Senda del collado: de la esquina nordeste de la plaza de Risco a la pradera, con una zeta.
    private static readonly Vector2[] SendaDelCollado =
    {
        new Vector2(16f, 461f), new Vector2(45f, 470f), new Vector2(30f, 482f), new Vector2(62f, 492f), new Vector2(95f, 507f),
    };
    /// Senda de la cumbre: de la pradera al mirador, en zetas por la ladera sur de la sierra norte.
    private static readonly Vector2[] SendaDeLaCumbre =
    {
        new Vector2(58f, 543f), new Vector2(18f, 547f), new Vector2(60f, 551f), new Vector2(20f, 555f),
        new Vector2(60f, 559f), new Vector2(26f, 563f), new Vector2(40f, 568f),
    };
    /// Playita escondida, en la muesca de la costa norte.
    private static readonly Vector2 CentroDeLaPlayita = new Vector2(102f, 605f);
    private const float CotaDeLaPlayita = 1.0f;

    // ── Geometría ────────────────────────────────────────────────────────────────────────────

    private static float s_largoDelValle = -1f;
    private static float LargoDelValle
    {
        get
        {
            if (s_largoDelValle < 0f)
            {
                s_largoDelValle = 0f;
                for (int j = 1; j < EjeDelValle.Length; j++) s_largoDelValle += Vector2.Distance(EjeDelValle[j - 1], EjeDelValle[j]);
            }
            return s_largoDelValle;
        }
    }

    /// Distancia al eje de la pradera, metros a lo largo del eje (s) y lado (+ a la izquierda de la marcha, el norte).
    private static float EnElValle(float x, float z, out float s, out float lado)
    {
        var p = new Vector2(x, z);
        float mejor = float.MaxValue, acumulado = 0f;
        s = 0f; lado = 0f;
        for (int j = 0; j + 1 < EjeDelValle.Length; j++)
        {
            Vector2 a = EjeDelValle[j], ab = EjeDelValle[j + 1] - a;
            float l = ab.magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (l * l));
            Vector2 q = a + ab * t;
            float d = Vector2.Distance(p, q);
            if (d < mejor)
            {
                mejor = d;
                s = acumulado + t * l;
                Vector2 u = ab / l;
                lado = u.x * (p.y - q.y) - u.y * (p.x - q.x);
            }
            acumulado += l;
        }
        return mejor;
    }

    private static (float cota, float medioAncho) PraderaEn(float s)
    {
        for (int j = 0; j + 1 < PerfilDeLaPradera.Length; j++)
        {
            var (s0, c0, m0) = PerfilDeLaPradera[j];
            var (s1, c1, m1) = PerfilDeLaPradera[j + 1];
            if (s <= s1 || j + 2 == PerfilDeLaPradera.Length)
            {
                float t = Mathf.Clamp01((s - s0) / (s1 - s0));
                return (Mathf.Lerp(c0, c1, t), Mathf.Lerp(m0, m1, t));
            }
        }
        return (PerfilDeLaPradera[0].cota, PerfilDeLaPradera[0].medioAncho);
    }

    private static bool DentroDelPoligono(Vector2[] poligono, float x, float z)
    {
        bool dentro = false;
        for (int i = 0, j = poligono.Length - 1; i < poligono.Length; j = i++)
        {
            Vector2 a = poligono[i], b = poligono[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) dentro = !dentro;
        }
        return dentro;
    }

    /// Distancia con signo a la costa del macizo (positiva dentro).
    private static float DentroDelMacizo(float x, float z)
    {
        var p = new Vector2(x, z);
        float d = float.MaxValue;
        for (int i = 0, j = CostaDelMacizo.Length - 1; i < CostaDelMacizo.Length; j = i++)
        {
            Vector2 a = CostaDelMacizo[j], ab = CostaDelMacizo[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
        }
        return DentroDelPoligono(CostaDelMacizo, x, z) ? d : -d;
    }

    private static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float Sqr(float v) => v * v;

    /// Polilínea (x, z) con la cota interpolada linealmente de «desde» a «hasta» por su largo.
    private static Vector3[] RecorridoConCota(Vector2[] planta, float desde, float hasta)
    {
        float total = 0f;
        for (int j = 1; j < planta.Length; j++) total += Vector2.Distance(planta[j - 1], planta[j]);
        var p = new Vector3[planta.Length];
        float s = 0f;
        for (int j = 0; j < planta.Length; j++)
        {
            if (j > 0) s += Vector2.Distance(planta[j - 1], planta[j]);
            p[j] = new Vector3(planta[j].x, Mathf.Lerp(desde, hasta, s / total), planta[j].y);
        }
        return p;
    }

    private static Vector3[] RecorridoDeLaSendaDelCollado(float cotaPlaza)
    {
        Vector2 fin = SendaDelCollado[SendaDelCollado.Length - 1];
        EnElValle(fin.x, fin.y, out float s, out _);
        return RecorridoConCota(SendaDelCollado, cotaPlaza, PraderaEn(s).cota + 0.2f);
    }

    private static Vector3[] RecorridoDeLaSendaDeLaCumbre()
    {
        Vector2 inicio = SendaDeLaCumbre[0];
        EnElValle(inicio.x, inicio.y, out float s, out _);
        return RecorridoConCota(SendaDeLaCumbre, PraderaEn(s).cota + 0.2f, LomasDelMacizo[0].cima.y - 1f);
    }

    // ── Relieve ──────────────────────────────────────────────────────────────────────────────

    /// Levanta el macizo y talla la pradera, el ibón y la playita.
    private static void LevantarElMacizo(Relieve r)
    {
        r.Recorrer(-100f, 400f, 290f, 645f, (i, k, x, z) =>
        {
            float d = DentroDelMacizo(x, z);
            if (d < -1f) return;
            float h = AlturaDelMacizo(x, z, out bool suelo);
            r.H[k, i] = suelo ? h : Mathf.Max(r.H[k, i], h);
        });
    }

    /// Distancia a la orilla del macizo (solo los tramos que dan al mar).
    private static float DistanciaALaOrilla(float x, float z)
    {
        var p = new Vector2(x, z);
        float d = float.MaxValue;
        for (int j = 0; j < UltimoPuntoDeLaOrilla; j++)
        {
            Vector2 a = CostaDelMacizo[j], ab = CostaDelMacizo[j + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
        }
        return d;
    }

    /// Monte del macizo sin la pradera: sube redondeado desde la orilla y lleva las lomas encima.
    private static float AlturaDeLaLoma(float x, float z)
    {
        var p = new Vector2(x, z);
        float d = DistanciaALaOrilla(x, z);
        float h = -1.5f + (AltoDelMacizo + 1.5f) * Mathf.Sin(Mathf.Clamp01(d / AlcanceDelMacizo) * Mathf.PI * 0.5f);
        foreach (var (cima, radio) in LomasDelMacizo)
        {
            float t = Mathf.Clamp01(Vector2.Distance(p, new Vector2(cima.x, cima.z)) / radio);
            h += (cima.y - AltoDelMacizo) * (0.5f + 0.5f * Mathf.Cos(t * Mathf.PI)) * Suave(d / 90f);
        }
        h += (Mathf.PerlinNoise(x * 0.035f + 3.1f, z * 0.035f + 7.7f) - 0.5f) * 5f * Suave(d / 15f);
        // Junto al agua, un acantilado bajo de roca, como en las islas.
        return Mathf.Min(h, -1.5f + d * 3.5f);
    }

    /// Altura del macizo en (x, z). «suelo» es true en la playita: ahí se pone su cota aunque
    /// hubiera algo más alto.
    private static float AlturaDelMacizo(float x, float z, out bool suelo)
    {
        suelo = false;
        var p = new Vector2(x, z);
        float d = DistanciaALaOrilla(x, z);
        float h = AlturaDeLaLoma(x, z);

        // Pradera alta: llana con ondas suaves; las laderas suben a los lados.
        float dv = EnElValle(x, z, out float s, out _);
        var (cotaPradera, medioAncho) = PraderaEn(s);
        float onda = (Mathf.PerlinNoise(x * 0.04f + 21f, z * 0.04f + 4f) - 0.5f) * 2.5f;
        h = Mathf.Min(h, cotaPradera + onda + Mathf.Max(dv - medioAncho, 0f) * PendienteDeLasLaderas);
        if (dv < medioAncho) h = Mathf.Min(h, cotaPradera + onda * Suave(1f - dv / medioAncho));
        // Ibón.
        float qi = Mathf.Sqrt(Sqr((x - CentroDelIbon.x) / RadioXDelIbon) + Sqr((z - CentroDelIbon.y) / RadioZDelIbon));
        // Se hunde en el terreno (nunca lo levanta): la orilla queda a ras y el agua en su hoya.
        if (qi < 1.25f)
            h = Mathf.Min(h, qi < 1f ? CotaDelIbon - HondoDelIbon * Suave((1f - qi) * 3f) - 0.2f
                                     : Mathf.Lerp(CotaDelIbon + 0.4f, h, Suave((qi - 1f) / 0.25f)));
        // Playita: arena casi a ras del agua en la muesca de la costa.
        float dp = Vector2.Distance(p, CentroDeLaPlayita);
        if (dp < 14f)
        {
            float arena = CotaDeLaPlayita + Mathf.Max(0f, dp - 7f) * 0.25f;
            if (dp < 9f) { suelo = true; h = arena; }
            else h = Mathf.Min(h, Mathf.Lerp(arena, h, Suave((dp - 9f) / 5f)));
        }

        return h;
    }

    /// Zona donde se tallan espolones y barrancos: la sierra vieja (por encima de la ciudad) y el macizo.
    private const float TallaDesdeLaCota = 132f, AltoDeLaTallaDeLaSierra = 8f;
    /// Sin tallar alrededor del paso de la galería del collado.
    private static readonly Vector2 PasoDeLaGaleria = new Vector2(19f, 428f);
    private const float RadioDelPasoDeLaGaleria = 30f;

    /// Rompe las caras lisas de la sierra con espolones y barrancos: solo en lo empinado, para que las sendas, la
    /// pradera y la ciudad no cambien.
    private static void TallarLaSierra(Relieve r)
    {
        var antes = (float[,])r.H.Clone();
        float paso = r.Paso;
        r.Recorrer(-130f, 360f, 290f, 645f, (i, k, x, z) =>
        {
            if (i < 1 || k < 1 || i >= r.Res - 1 || k >= r.Res - 1) return;
            float h = antes[k, i];
            // Solo las caras de la sierra vieja: el macizo es de lomas redondeadas.
            if (DentroDelMacizo(x, z) > 3f || h < TallaDesdeLaCota) return;
            if (Vector2.Distance(new Vector2(x, z), PasoDeLaGaleria) < RadioDelPasoDeLaGaleria) return;
            float gx = (antes[k, i + 1] - antes[k, i - 1]) / (2f * paso), gz = (antes[k + 1, i] - antes[k - 1, i]) / (2f * paso);
            float pendiente = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
            float peso = Suave((pendiente - 32f) / 15f);
            peso *= Suave((h - TallaDesdeLaCota) / 12f);
            if (peso <= 0f) return;
            // Crestas en «V» del ruido: espolones que bajan y barrancos entre ellos.
            float n = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * 0.035f + 71f, z * 0.035f + 13f) - 1f);
            float fino = Mathf.PerlinNoise(x * 0.09f + 5f, z * 0.09f + 31f) - 0.5f;
            r.H[k, i] = h + ((n - 0.55f) * AltoDeLaTallaDeLaSierra + fino * 4f) * peso;
        });
    }

    // ── Suelo ────────────────────────────────────────────────────────────────────────────────

    /// Capas de la montaña (por nombre de TerrainLayer). La nieve no se pinta: la pone el terreno por altura
    /// (AspectoDeDibujo.cotaDeLaNieve), con un borde en curva.
    private const string CapaLadera = "Capa6";
    /// Por encima de esta cota puede haber nieve: ahí no se plantan pinos.
    private const float CotaSinNieve = 140f;

    /// Pinta toda la montaña con un mismo criterio, por delante y por detrás, en franjas limpias: roca gris en lo
    /// empinado, matorral al pie de la roca y hierba en lo demás; arena solo en la orilla llana (la nieve de las
    /// cimas la pone el terreno). En el macizo, de lomas redondeadas como las islas, hierba y tierra; roca solo en
    /// el acantilado de la costa. La pradera alta queda de hierba con flores. Lo que se pinta después (camino de Risco, plaza, sendas, garganta)
    /// va encima.
    private static void PintarElMacizo(Lienzo l, float cotaPlaza, List<string> informe)
    {
        int roca = l.Capa(CapaRoca), ladera = l.Capa(CapaLadera), tierra = l.Capa(CapaTierra);
        int hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores), arena = l.Capa(CapaArena);
        int pintadas = 0;
        l.Recorrer(-130f, 365f, 290f, 645f, (i, k, x, z) =>
        {
            float d = DentroDelMacizo(x, z);
            float h = l.Altura(x, z);
            // La montaña: el macizo entero y, fuera de él, lo que sube por encima de la llanura de la ciudad.
            if (d < 0f && h < 118f) return;
            if (h < -0.5f) return;
            // Lo retocado a mano cuando aquí había mar ya no vale: donde el macizo ha levantado tierra se repinta.
            if (d > 0f && l.Bloqueadas != null && SubidaDelRelieve(l, x, z) > 3f) l.Bloqueadas[k * l.Res + i] = false;
            float pendiente = l.Pendiente(x, z);
            int capa;
            float dv = EnElValle(x, z, out float s, out _);
            float qi = Mathf.Sqrt(Sqr((x - CentroDelIbon.x) / RadioXDelIbon) + Sqr((z - CentroDelIbon.y) / RadioZDelIbon));
            if (qi < 1.12f || (h < 2.5f && pendiente < 15f) || Vector2.Distance(new Vector2(x, z), CentroDeLaPlayita) < 10f) capa = arena;
            else if (d > 0f && dv < PraderaEn(s).medioAncho && pendiente < 30f) capa = Ruido.Fbm(x, z, 14f, 811) > 0.62f ? flores : hierba;
            // Franjas sin mezclas sueltas: así la roca, el matorral y la hierba se leen de lejos. La hierba (y no un
            // verde apagado) porque la cara norte queda casi siempre a la sombra.
            else if (d > 0f && z > 455f)
                capa = pendiente > 72f ? roca : pendiente > 54f ? tierra : Ruido.Fbm(x, z, 14f, 817) > 0.72f ? flores : hierba;
            else if (pendiente > 44f) capa = roca;
            else if (pendiente > 34f) capa = ladera;
            else capa = Ruido.Fbm(x, z, 14f, 817) > 0.7f ? flores : hierba;
            if (capa < 0) return;
            l.Mezclar(i, k, capa, 1f, duro: true);
            pintadas++;
        });
        PintarSenda(l, Planta(RecorridoDeLaSendaDelCollado(cotaPlaza)), 3.5f, 813, null, serpenteo: 0.4f);
        PintarSenda(l, Planta(RecorridoDeLaSendaDeLaCumbre()), 2.6f, 814, null, serpenteo: 0.3f);
        informe.Add($"Suelo: montaña pintada con un mismo criterio por delante y por detrás ({pintadas} celdas: roca, matorral, tierra, hierba y arena), senda del collado y senda de la cumbre.");
    }

    /// Cuánto ha subido el relieve en (x, z) con el vestido (0 si no se ha esculpido).
    private static float SubidaDelRelieve(Lienzo l, float x, float z)
    {
        if (s_cambioDelRelieve == null) return 0f;
        int res = s_cambioDelRelieve.GetLength(0);
        Vector3 origen = l.Terreno.transform.position, medida = l.Datos.size;
        int i = Mathf.Clamp(Mathf.RoundToInt((x - origen.x) / medida.x * (res - 1)), 0, res - 1);
        int k = Mathf.Clamp(Mathf.RoundToInt((z - origen.z) / medida.z * (res - 1)), 0, res - 1);
        return s_cambioDelRelieve[k, i];
    }

    /// Pone roca en las paredes de la montaña que las sendas y caminos han dejado pintadas de
    /// tierra: en una pared casi vertical la tierra se estira en rayas.
    private static void RocaEnLasParedes(Lienzo l, List<string> informe)
    {
        int roca = l.Capa(CapaRoca);
        int puestas = 0;
        l.Recorrer(-130f, 365f, 290f, 645f, (i, k, x, z) =>
        {
            float h = l.Altura(x, z), d = DentroDelMacizo(x, z);
            if (d < 0f && h < 118f) return;
            // Las lomas del macizo se quedan con su hierba y su tierra; solo sus cortados son de roca.
            if (l.Pendiente(x, z) < (d > 0f && z > 455f ? 72f : 52f)) return;
            if (roca < 0) return;
            l.Mezclar(i, k, roca, 1f, duro: true);
            puestas++;
        });
        informe.Add($"Suelo: {puestas} celdas de pared de la montaña con roca (sin rayas de tierra).");
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    private static void MacizoDelNorte(Obra o, Transform grupo, float cotaPlaza)
    {
        // Ibón: lámina de agua de dibujo y caja para nadar.
        Transform agua = Grupo(grupo, "Ibón de montaña");
        LaminaDeAgua(o, agua, "Agua del ibón", Disco(CentroDelIbon, RadioXDelIbon + 1.5f, RadioZDelIbon + 1.5f, 24), CotaDelIbon,
                     Aguas.MaterialDelAguaDeLaLaguna());
        CajaParaNadar(o, agua, "Agua para nadar — ibón", new Vector3(CentroDelIbon.x, CotaDelIbon, CentroDelIbon.y),
                      new Vector2(RadioXDelIbon * 2f, RadioZDelIbon * 2f), 0f, HondoDelIbon + 1f);
        PinosDelMacizo(o, Grupo(grupo, "Pinos"), cotaPlaza);
        Vector3 cima = LomasDelMacizo[0].cima;
        var sitio = new Vector2(cima.x, cima.z);
        PonExacto(o, grupo, MiradorDeLaCumbre, "Mirador de la cumbre", new Vector3(sitio.x, AlturaEn(o, sitio), sitio.y), 0f);
        o.Informe.Add($"  Macizo del norte: pradera alta de {PerfilDeLaPradera[0].cota:0} a {CotaDelIbon:0} m, ibón a {CotaDelIbon:0.0} m, " +
                      $"mirador en la cumbre ({sitio.x:0}, {sitio.y:0}) a {AlturaEn(o, sitio):0.0} m, playita a {AlturaEn(o, CentroDeLaPlayita):0.0} m.");
    }

    /// Caja trigger en la capa Water con la cara de arriba en la cota de «centroArriba», girada «rumbo» grados.
    private static void CajaParaNadar(Obra o, Transform grupo, string nombre, Vector3 centroArriba, Vector2 medida, float rumbo, float hondo)
    {
        var go = new GameObject(nombre);
        int capaAgua = LayerMask.NameToLayer("Water");
        if (capaAgua >= 0) go.layer = capaAgua;
        go.transform.SetParent(grupo, false);
        go.transform.position = centroArriba + Vector3.down * (hondo * 0.5f);
        go.transform.rotation = Quaternion.Euler(0f, rumbo, 0f);
        var caja = go.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(medida.x, hondo, medida.y);
        o.SinObstaculo.Add(go.transform);
    }

    /// Pinos del macizo en bosquetes (nunca sueltos): centros en laderas suaves y unos cuantos pinos alrededor.
    private static void PinosDelMacizo(Obra o, Transform grupo, float cotaPlaza)
    {
        string[] pinos = { "Tree01_a01", "Tree01_a02", "Tree02_c01" };
        var az = new System.Random(1407);
        Vector2[] collado = Planta(RecorridoDeLaSendaDelCollado(cotaPlaza)), cumbre = Planta(RecorridoDeLaSendaDeLaCumbre());
        int bosquetes = 0, puestos = 0;
        bool Vale(Vector2 q, float maxPendiente)
        {
            if (DentroDelMacizo(q.x, q.y) < 6f) return false;
            if (EnElPuebloDeRisco(q, 3f)) return false;
            float h = AlturaEn(o, q);
            if (h < 6f || h > CotaSinNieve) return false;
            float dv = EnElValle(q.x, q.y, out float s, out _);
            if (dv < PraderaEn(s).medioAncho - 6f) return false;
            if (DistanciaAPolilinea(q, collado) < 4f || DistanciaAPolilinea(q, cumbre) < 3.5f) return false;
            if (Vector2.Distance(q, CentroDelIbon) < RadioXDelIbon + 3f) return false;
            return PendienteEn(o, q) <= maxPendiente;
        }
        for (int j = 0; j < 6000 && bosquetes < 60; j++)
        {
            var c = new Vector2(-80f + (float)az.NextDouble() * 350f, 440f + (float)az.NextDouble() * 190f);
            if (!Vale(c, 34f)) continue;
            bosquetes++;
            int n = 5 + az.Next(5);
            for (int t = 0; t < n * 3 && n > 0; t++)
            {
                float ang = (float)az.NextDouble() * Mathf.PI * 2f, r = 1.5f + (float)az.NextDouble() * 7f;
                var q = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (!Vale(q, 40f)) continue;
                PonZona0(o, grupo, FK + "Vegetation/" + pinos[az.Next(pinos.Length)] + ".prefab", "Pino del macizo",
                         new Vector3(q.x, AlturaEn(o, q) - 0.3f, q.y), (float)az.NextDouble() * 360f, 0.8f + 0.4f * (float)az.NextDouble());
                puestos++;
                n--;
            }
        }
        o.Informe.Add($"  Macizo del norte: {puestos} pinos en {bosquetes} bosquetes.");
    }

    /// Pendiente del terreno en grados.
    private static float PendienteEn(Obra o, Vector2 q)
    {
        TerrainData datos = o.Terreno.terrainData;
        Vector3 origen = o.Terreno.transform.position;
        return Vector3.Angle(datos.GetInterpolatedNormal((q.x - origen.x) / datos.size.x, (q.y - origen.z) / datos.size.z), Vector3.up);
    }
}
