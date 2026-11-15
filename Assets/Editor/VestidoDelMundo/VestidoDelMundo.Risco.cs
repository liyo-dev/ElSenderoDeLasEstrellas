using System.Collections.Generic;
using Kits.Waterfalls.Editor;
using UnityEngine;

/// Pueblo de Risco en terrazas (GDD § 5.12 y § 7.4). Arriba, la calle alta con la capilla y la plaza; debajo, mirando
/// al mar, la terraza de la noria (molino, noria y cascada a la garganta), la de los huertos y la era, la más grande,
/// con la casa grande, el granero y los puestos. Escaleras de piedra entre terrazas y una senda vallada en zetas de
/// la era a la pradera alta. Peñas con hierba en los cantos.
/// · Las cotas van referidas a la plaza: si cambia el camino, el pueblo baja o sube con él.
/// · Todo sale de las tablas de este archivo: para retocar el pueblo se cambian las tablas y se vuelve a vestir.
public static partial class VestidoDelMundo
{
    // ── Tablas del pueblo ────────────────────────────────────────────────────────────────────

    /// Terraza del pueblo: rectángulo de esquinas redondeadas en planta, a «BajoLaPlaza» metros por debajo de la plaza.
    private readonly struct Terraza
    {
        public readonly string Nombre;
        public readonly Vector2 Centro;
        public readonly float MedioX, MedioZ, BajoLaPlaza;

        public Terraza(string nombre, Vector2 centro, float medioX, float medioZ, float bajoLaPlaza)
        {
            Nombre = nombre; Centro = centro; MedioX = medioX; MedioZ = medioZ; BajoLaPlaza = bajoLaPlaza;
        }

        public float Cota(float cotaPlaza) => cotaPlaza - BajoLaPlaza;
        public float XMin => Centro.x - MedioX;
        public float XMax => Centro.x + MedioX;
        public float ZMin => Centro.y - MedioZ;
        public float ZMax => Centro.y + MedioZ;
    }

    private static readonly Terraza TerrazaDeLaNoria = new Terraza("Terraza de la noria", new Vector2(-30f, 470f), 10f, 5f, 8f);
    private static readonly Terraza TerrazaDeLosHuertos = new Terraza("Terraza de los huertos", new Vector2(-2f, 470f), 11f, 5f, 10f);
    private static readonly Terraza LaEra = new Terraza("La era", new Vector2(-22f, 488f), 15f, 6f, 20f);
    private static readonly Terraza[] TerrazasDeRisco = { TerrazaDeLaNoria, TerrazaDeLosHuertos, LaEra };

    /// Radio de las esquinas de las terrazas y talud con que se funden con su cuerpo de roca.
    private const float EsquinaDeLasTerrazas = 2.5f, TaludDeLasTerrazas = 1.2f;

    /// Cuerpo de roca bajo las terrazas: pared casi vertical los primeros metros y ladera de pinar después. El ruido
    /// mueve el borde para que salgan espolones y entrantes.
    private const float ParedBajoLasTerrazas = 10f, PendienteDeLaPared = 2.6f, PendienteDeLaLadera = 0.78f, RuidoDelCuerpo = 4f;
    /// El cuerpo de roca no entra en la garganta: hasta este margen (m) del eje, su fondo queda libre, y la pared que
    /// sube desde ahí es de 3:1 como mucho.
    private const float MargenDeLaGarganta = 7f;

    /// Escalones de piedra (EscalonesDePiedra): cada tramo avanza 4,5 m y sube 2 m hacia su +Z.
    private const float LargoDeTramo = 4.5f, SubeElTramo = 2f, AnchoDeEscalera = 3.2f;
    /// Escalera de la plaza a la terraza de la noria: pegada al borde norte de la plaza, baja hacia el oeste.
    private const float ZDeLaEscaleraDeLaPlaza = 462.8f, XAltoDeLaEscaleraDeLaPlaza = -12f;
    /// Escalera de la noria a los huertos (un tramo, a lo largo de z).
    private const float ZDeLaEscaleraDeLosHuertos = 468f;
    /// Escalera de la noria a la era: dos tramos de tres por la pared que las separa, con un rellano al oeste.
    private const float ZDelPrimerTramoALaEra = 476.9f, ZDelSegundoTramoALaEra = 480.3f, XAltoDeLaEscaleraALaEra = -22f;
    private static readonly Vector2 RellanoDeLaEscaleraALaEra = new Vector2(-38.3f, 478.6f);

    /// Canal del molino: sale de la presa, baja por el oeste de la plaza y llega a la noria.
    private static readonly Vector2[] CanalDelMolino =
    {
        new Vector2(-35f, 445.5f), new Vector2(-38.5f, 452f), new Vector2(-38.5f, 463.5f), new Vector2(-39.6f, 466.5f), new Vector2(-39.6f, 473.5f),
    };
    /// Plataforma de tablas que sale del canto noroeste de la terraza sobre el vacío, con la noria encima; la cascada
    /// cae desde su punta a la garganta. «SitioDeLaNoria» es el centro de la rueda en planta.
    private static readonly Vector2 SitioDeLaNoria = new Vector2(-43f, 473.5f);
    private const float VuelaLaPlataforma = 5.5f, AltoDeLaNoria = 11f;
    private const string RuedaDelMolino = FK + "Main Structures/Decoration/Waterwheel01.prefab";

    /// Senda vallada de la era a la pradera alta, en zetas (planta).
    private static readonly Vector2[] SendaVallada =
    {
        new Vector2(-7.5f, 488f), new Vector2(21f, 490.5f), new Vector2(24.5f, 495f), new Vector2(1f, 499.5f),
        new Vector2(-2f, 504f), new Vector2(27f, 509.5f), new Vector2(44f, 514.5f),
    };
    private const float AnchoDeLaSendaVallada = 4f;

    private const string Barandilla = FK + "Main Structures/Railing/Railing01_a01.prefab";
    private static readonly string[] PinosDeRisco = { FK + "Vegetation/Tree01_a01.prefab", FK + "Vegetation/Tree01_a02.prefab", FK + "Vegetation/Tree02_c01.prefab" };

    // ── Geometría ────────────────────────────────────────────────────────────────────────────

    /// Distancia con signo al borde de la terraza (negativa dentro).
    private static float DistanciaATerraza(Vector2 p, Terraza t)
    {
        float r = EsquinaDeLasTerrazas;
        float qx = Mathf.Abs(p.x - t.Centro.x) - (t.MedioX - r), qz = Mathf.Abs(p.y - t.Centro.y) - (t.MedioZ - r);
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qz), 0f) - r;
    }

    /// Metros al este (lado del pueblo) del eje de la garganta; negativo al otro lado.
    private static float AlEsteDeLaGarganta(Vector2 p)
    {
        Vector3 a3 = Garganta[0], b3 = Garganta[Garganta.Length - 1];
        Vector2 ab = new Vector2(b3.x - a3.x, b3.z - a3.z).normalized;
        return (p.x - a3.x) * ab.y - (p.y - a3.z) * ab.x;
    }

    /// Hacia dónde queda la garganta desde el pueblo (perpendicular a su eje).
    private static Vector2 HaciaLaGarganta
    {
        get
        {
            Vector3 a3 = Garganta[0], b3 = Garganta[Garganta.Length - 1];
            Vector2 ab = new Vector2(b3.x - a3.x, b3.z - a3.z).normalized;
            return -new Vector2(ab.y, -ab.x);
        }
    }

    private static Vector3[] RecorridoDeLaEscaleraDeLaPlaza(float cotaPlaza)
    {
        float yN = TerrazaDeLaNoria.Cota(cotaPlaza);
        float largo = (cotaPlaza - yN) / SubeElTramo * LargoDeTramo;
        return new[]
        {
            new Vector3(XAltoDeLaEscaleraDeLaPlaza - largo - 1f, yN, ZDeLaEscaleraDeLaPlaza),
            new Vector3(XAltoDeLaEscaleraDeLaPlaza + 1f, cotaPlaza, ZDeLaEscaleraDeLaPlaza),
        };
    }

    private static Vector3[] RecorridoDeLaEscaleraDeLosHuertos(float cotaPlaza)
    {
        float yN = TerrazaDeLaNoria.Cota(cotaPlaza), yH = TerrazaDeLosHuertos.Cota(cotaPlaza);
        return new[]
        {
            new Vector3(TerrazaDeLosHuertos.XMin, yH, ZDeLaEscaleraDeLosHuertos),
            new Vector3(TerrazaDeLaNoria.XMax - 0.5f, yN, ZDeLaEscaleraDeLosHuertos),
        };
    }

    /// Los dos tramos de la escalera de la noria a la era: el primero baja hacia el oeste; el segundo vuelve al este.
    private static (Vector3[] primero, Vector3[] segundo, float cotaDelRellano) EscaleraALaEra(float cotaPlaza)
    {
        float yN = TerrazaDeLaNoria.Cota(cotaPlaza), yE = LaEra.Cota(cotaPlaza);
        float medio = (yN + yE) * 0.5f;
        float largo = (yN - medio) / SubeElTramo * LargoDeTramo;
        float xBajo = XAltoDeLaEscaleraALaEra - largo;
        return (new[] { new Vector3(xBajo - 0.5f, medio, ZDelPrimerTramoALaEra), new Vector3(XAltoDeLaEscaleraALaEra + 0.5f, yN, ZDelPrimerTramoALaEra) },
                new[] { new Vector3(xBajo - 1.5f, medio, ZDelSegundoTramoALaEra), new Vector3(xBajo + largo - 1f + 0.5f, yE, ZDelSegundoTramoALaEra) },
                medio);
    }

    private static Vector3[] RecorridoDelCanal(float cotaPlaza)
    {
        float yC = cotaPlaza + DeLaCalleALaPlaza, yN = TerrazaDeLaNoria.Cota(cotaPlaza);
        float[] cotas = { yC - 0.6f, cotaPlaza + 1f, yN + 0.8f, yN + 0.3f, yN + 0.15f };
        var p = new Vector3[CanalDelMolino.Length];
        for (int j = 0; j < p.Length; j++) p[j] = new Vector3(CanalDelMolino[j].x, cotas[j], CanalDelMolino[j].y);
        return p;
    }

    /// Recorrido 3D de la senda vallada: baja con pendiente constante de la era a «cotaFin».
    private static Vector3[] RecorridoDeLaSendaVallada(float cotaEra, float cotaFin)
    {
        float total = 0f;
        for (int j = 1; j < SendaVallada.Length; j++) total += Vector2.Distance(SendaVallada[j - 1], SendaVallada[j]);
        var p = new Vector3[SendaVallada.Length];
        float s = 0f;
        for (int j = 0; j < p.Length; j++)
        {
            if (j > 0) s += Vector2.Distance(SendaVallada[j - 1], SendaVallada[j]);
            p[j] = new Vector3(SendaVallada[j].x, Mathf.Lerp(cotaEra, cotaFin, s / total), SendaVallada[j].y);
        }
        return p;
    }

    /// ¿Cae el punto dentro del pueblo (terrazas, escaleras, canal o senda), con «margen» metros de holgura?
    private static bool EnElPuebloDeRisco(Vector2 q, float margen)
    {
        foreach (Terraza t in TerrazasDeRisco)
            if (DistanciaATerraza(q, t) < margen) return true;
        if (DistanciaAPolilinea(q, SendaVallada) < AnchoDeLaSendaVallada * 0.5f + margen) return true;
        if (DistanciaAPolilinea(q, CanalDelMolino) < 1.5f + margen) return true;
        if (q.x > -40f && q.x < -8f && q.y > 461f && q.y < 482f && Mathf.Abs(q.y - ZDeLaEscaleraDeLaPlaza) < 2f + margen) return true;
        return false;
    }

    // ── Relieve ──────────────────────────────────────────────────────────────────────────────

    /// Cuerpo de roca, terrazas, escaleras, canal y senda vallada. Va después de la garganta y la plaza.
    private static void EsculpirElPuebloEnTerrazas(Relieve r, float cotaPlaza)
    {
        float cotaFinDeLaSenda = r.Altura(SendaVallada[SendaVallada.Length - 1].x, SendaVallada[SendaVallada.Length - 1].y);
        // Cuerpo de roca: la terraza más cercana manda; nunca baja el terreno que ya había.
        r.Recorrer(-95f, 458f, 70f, 565f, (i, k, x, z) =>
        {
            var p = new Vector2(x, z);
            float mejor = float.MinValue;
            for (int j = 0; j < TerrazasDeRisco.Length; j++)
            {
                Terraza t = TerrazasDeRisco[j];
                float d = Mathf.Max(0f, DistanciaATerraza(p, t) + (Ruido.Fbm(x, z, 14f, 4410 + j) - 0.5f) * 2f * RuidoDelCuerpo);
                float y = t.Cota(cotaPlaza) - (d < ParedBajoLasTerrazas
                    ? PendienteDeLaPared * d
                    : PendienteDeLaPared * ParedBajoLasTerrazas + PendienteDeLaLadera * (d - ParedBajoLasTerrazas));
                mejor = Mathf.Max(mejor, y);
            }
            float lado = AlEsteDeLaGarganta(p);
            if (lado < MargenDeLaGarganta)
                mejor = Mathf.Min(mejor, r.H[k, i] + Mathf.Max(lado - MargenDeLaGarganta + 8f, 0f) * 3f);
            if (mejor > r.H[k, i]) r.H[k, i] = mejor;
        });
        // Senda vallada (antes que las terrazas: la era manda en su arranque).
        float yE = LaEra.Cota(cotaPlaza);
        Franja(r, RecorridoDeLaSendaVallada(yE, cotaFinDeLaSenda), AnchoDeLaSendaVallada, 4f);
        // Canal del molino: una repisa que baja por el oeste de la plaza.
        Franja(r, RecorridoDelCanal(cotaPlaza), 2.2f, 1.5f);
        // Las terrazas, de abajo arriba.
        var orden = new List<Terraza>(TerrazasDeRisco);
        orden.Sort((a, b) => b.BajoLaPlaza.CompareTo(a.BajoLaPlaza));
        foreach (Terraza t in orden) TerrazaLlana(r, t, t.Cota(cotaPlaza));
        // Escaleras: rampas que llevan los escalones.
        Franja(r, RecorridoDeLaEscaleraDeLaPlaza(cotaPlaza), AnchoDeEscalera, 1f);
        Franja(r, RecorridoDeLaEscaleraDeLosHuertos(cotaPlaza), AnchoDeEscalera, 1f);
        var (primero, segundo, cotaDelRellano) = EscaleraALaEra(cotaPlaza);
        Franja(r, primero, AnchoDeEscalera, 0.6f);
        Franja(r, segundo, AnchoDeEscalera, 0.6f);
        Rellano(r, RellanoDeLaEscaleraALaEra, 2.6f, 3.2f, cotaDelRellano, 1f);
    }

    /// La terraza llana a su cota, fundida con su cuerpo en TaludDeLasTerrazas.
    private static void TerrazaLlana(Relieve r, Terraza t, float cota)
    {
        float m = TaludDeLasTerrazas + r.Paso;
        r.Recorrer(t.XMin - m, t.ZMin - m, t.XMax + m, t.ZMax + m, (i, k, x, z) =>
        {
            float d = DistanciaATerraza(new Vector2(x, z), t);
            float w = d <= 0f ? 1f : Mathf.Clamp01(1f - d / TaludDeLasTerrazas);
            w = w * w * (3f - 2f * w);
            if (w > 0f) r.H[k, i] = Mathf.Lerp(r.H[k, i], cota, w);
        });
    }

    // ── Suelo ────────────────────────────────────────────────────────────────────────────────

    private static void PintarElPuebloDeRisco(Lienzo l, float cotaPlaza)
    {
        PintarElipse(l, TerrazaDeLaNoria.Centro, TerrazaDeLaNoria.MedioX, TerrazaDeLaNoria.MedioZ, CapaHierba, FormaDeCalle);
        PintarElipse(l, TerrazaDeLosHuertos.Centro, TerrazaDeLosHuertos.MedioX, TerrazaDeLosHuertos.MedioZ, CapaHierba, FormaDeCalle);
        foreach (Vector2 h in HuertasDeRisco) PintarMancha(l, h, 3.2f, CapaTierra, 0.9f, 841 + (int)h.x);
        PintarElipse(l, LaEra.Centro, LaEra.MedioX, LaEra.MedioZ, CapaTierra, FormaDeCalle);
        PintarMancha(l, LaEra.Centro + new Vector2(0f, 2.5f), 7f, CapaTierraPiedras, 0.6f, 845);
        PintarSenda(l, SendaVallada, 3.5f, 846, null, CapaTierra, CapaTierraPiedras, 0.25f);
        PintarSenda(l, Planta(RecorridoDeLaEscaleraDeLaPlaza(cotaPlaza)), 3f, 847, null, CapaRoca, CapaRoca, 0f);
        var (primero, segundo, _) = EscaleraALaEra(cotaPlaza);
        PintarSenda(l, Planta(primero), 3f, 848, null, CapaRoca, CapaRoca, 0f);
        PintarSenda(l, Planta(segundo), 3f, 849, null, CapaRoca, CapaRoca, 0f);
        PintarSenda(l, CanalDelMolino, 2.4f, 850, null, CapaRoca, CapaRoca, 0f);
        foreach (Vector2[] c in CaminitosDeRisco) PintarSenda(l, c, 2.2f, 851 + (int)c[0].x, null, CapaTierraPiedras, CapaTierra, 0.3f);
    }

    /// Caminitos de tierra y piedra que unen, dentro de cada terraza, la llegada de una escalera con la salida de la
    /// siguiente.
    private static readonly Vector2[][] CaminitosDeRisco =
    {
        new[] { new Vector2(-30f, 464.6f), new Vector2(-27.5f, 467f), new Vector2(-25f, 473.5f), new Vector2(-22f, 476f) },
        new[] { new Vector2(-23f, 481.8f), new Vector2(-21.5f, 486f), new Vector2(-15f, 489.8f), new Vector2(-7.5f, 488f) },
        new[] { new Vector2(-12.5f, 468f), new Vector2(-6f, 468.8f), new Vector2(-3f, 469.5f) },
    };

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    /// Huertas de la terraza de los huertos (centro en planta).
    private static readonly Vector2[] HuertasDeRisco = { new Vector2(-9.5f, 467.4f), new Vector2(0f, 467.3f) };

    /// Peñas con hierba encima que visten el cuerpo de roca de las terrazas, en dos anillos: uno en el canto, con la
    /// cara de arriba casi a la cota de la terraza, y otro más abajo y más afuera. Por terraza, los lados que llevan
    /// peñas (N, E, O) y cada cuántos metros.
    private static readonly (int terraza, string lados)[] LadosConPenas = { (0, "N"), (1, "NE"), (2, "NEO") };
    private const float SeparacionDePenas = 7.5f, AfueraDelSegundoAnillo = 5.5f, BajoDelSegundoAnillo = 9f;

    /// Sitios del pueblo donde no van peñas: escaleras, senda, plataforma de la noria y canal.
    private static bool EstorbaUnaPena(Vector2 q, float cotaPlaza)
    {
        if (DistanciaAPolilinea(q, SendaVallada) < 4.5f) return true;
        if (DistanciaAPolilinea(q, CanalDelMolino) < 3f) return true;
        if (Vector2.Distance(q, SitioDeLaNoria) < 7f) return true;
        var (primero, segundo, _) = EscaleraALaEra(cotaPlaza);
        if (DistanciaAPolilinea(q, Planta(primero)) < 4f || DistanciaAPolilinea(q, Planta(segundo)) < 4f) return true;
        if (Vector2.Distance(q, RellanoDeLaEscaleraALaEra) < 5f) return true;
        if (DistanciaAPolilinea(q, Planta(RecorridoDeLaEscaleraDeLosHuertos(cotaPlaza))) < 3.5f) return true;
        return false;
    }

    private static int PenasDeLasTerrazas(Obra o, Transform grupo, float cotaPlaza)
    {
        var az = new System.Random(4421);
        string[] piezas = { "RepisaDeRoca_Grande", "RepisaDeRoca_Mediana", "RepisaDeRoca_Grande" };
        int puestas = 0;
        foreach (var (indice, lados) in LadosConPenas)
        {
            Terraza t = TerrazasDeRisco[indice];
            float cota = t.Cota(cotaPlaza);
            foreach (char lado in lados)
            {
                Vector2 normal = lado == 'N' ? Vector2.up : lado == 'E' ? Vector2.right : Vector2.left;
                Vector2 a = lado == 'N' ? new Vector2(t.XMin + 2f, t.ZMax) : new Vector2(lado == 'E' ? t.XMax : t.XMin, t.ZMin + 2f);
                Vector2 b = lado == 'N' ? new Vector2(t.XMax - 2f, t.ZMax) : new Vector2(lado == 'E' ? t.XMax : t.XMin, t.ZMax - 1f);
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / SeparacionDePenas));
                for (int j = 0; j <= n; j++)
                {
                    Vector2 borde = Vector2.Lerp(a, b, j / (float)n) + new Vector2((float)az.NextDouble() - 0.5f, (float)az.NextDouble() - 0.5f) * 1.5f;
                    Vector2 q1 = borde + normal * 1.6f;
                    if (!EstorbaUnaPena(q1, cotaPlaza) &&
                        PonZona(o, grupo, piezas[az.Next(piezas.Length)], "Peña del canto", new Vector3(q1.x, cota - 0.25f - 0.5f * (float)az.NextDouble(), q1.y), (float)az.NextDouble() * 360f) != null)
                        puestas++;
                    Vector2 q2 = borde + normal * AfueraDelSegundoAnillo + new Vector2(normal.y, normal.x) * (SeparacionDePenas * 0.5f);
                    float y2 = cota - BajoDelSegundoAnillo - 3f * (float)az.NextDouble();
                    if (!EstorbaUnaPena(q2, cotaPlaza) && AlturaEn(o, q2) < y2 - 1f &&
                        PonZona(o, grupo, piezas[az.Next(piezas.Length)], "Peña de la ladera", new Vector3(q2.x, y2, q2.y), (float)az.NextDouble() * 360f, 0.8f + 0.4f * (float)az.NextDouble()) != null)
                        puestas++;
                }
            }
        }
        return puestas;
    }

    /// Bosquetes de pinos en la ladera de pinar bajo las terrazas.
    private static int PinaresDeRisco(Obra o, Transform grupo)
    {
        var az = new System.Random(4431);
        int puestos = 0, bosquetes = 0;
        bool Vale(Vector2 q)
        {
            if (EnElPuebloDeRisco(q, 3.5f) || AlEsteDeLaGarganta(q) < 9f) return false;
            float h = AlturaEn(o, q);
            if (h < 8f) return false;
            float pendiente = PendienteEn(o, q);
            return pendiente > 6f && pendiente < 38f;
        }
        for (int j = 0; j < 4000 && bosquetes < 22; j++)
        {
            var c = new Vector2(-45f + (float)az.NextDouble() * 95f, 474f + (float)az.NextDouble() * 70f);
            if (!Vale(c)) continue;
            bosquetes++;
            int n = 4 + az.Next(6);
            for (int t = 0; t < n * 3 && n > 0; t++)
            {
                float ang = (float)az.NextDouble() * Mathf.PI * 2f, r = 1.5f + (float)az.NextDouble() * 6f;
                var q = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (!Vale(q)) continue;
                if (PonZona0(o, grupo, PinosDeRisco[az.Next(PinosDeRisco.Length)], "Pino de la ladera de Risco",
                             new Vector3(q.x, AlturaEn(o, q) - 0.3f, q.y), (float)az.NextDouble() * 360f, 0.8f + 0.45f * (float)az.NextDouble()) != null)
                { puestos++; n--; }
            }
        }
        return puestos;
    }

    private static void PuebloDeRisco(Obra o, Transform grupo, Vector3 fin)
    {
        float yP = CotaDeLaPlaza(fin), yC = CotaDeLaCalleAlta(fin);
        float yN = TerrazaDeLaNoria.Cota(yP), yH = TerrazaDeLosHuertos.Cota(yP), yE = LaEra.Cota(yP);
        Transform casas = Grupo(grupo, "Casas");
        // Calle alta: la capilla mirando a la llegada de la galería y dos casas con la fachada a la plaza.
        PonZona(o, casas, "CapillaDeRisco", "Capilla de Risco", new Vector3(-22f, yC, CentroDeLaCalleAlta.y - 0.2f), -90f);
        PonZona(o, casas, "CasaDeRisco_A", "Casa de Risco", new Vector3(-11.5f, yC, CentroDeLaCalleAlta.y - 0.5f), 180f);
        PonZona(o, casas, "CasaDeRisco_C", "Casa de Risco", new Vector3(-5f, yC, CentroDeLaCalleAlta.y - 0.7f), 180f);
        // Plaza: dos casas en el borde norte con la fachada a la plaza, el pozo y un árbol.
        PonZona(o, casas, "CasaDeRisco_D", "Casa de Risco", new Vector3(-25f, yP, CentroDeLaPlaza.y + 2.4f), 0f);
        PonZona(o, casas, "CasaDeRisco_A", "Casa de Risco", new Vector3(7f, yP, CentroDeLaPlaza.y + 2.4f), 0f);
        PonZona(o, grupo, "PozoDelPueblo", "Pozo de la plaza", new Vector3(-14f, yP, CentroDeLaPlaza.y - 1f), 30f);
        PonZona(o, grupo, "ArbolDePlaza", "Árbol de la plaza", new Vector3(-2f, yP, CentroDeLaPlaza.y - 0.5f), 0f);
        // Escalera de la calle alta: tres tramos que suben hacia el este, sobre su rampa.
        Transform escaleras = Grupo(grupo, "Escaleras");
        Vector3[] rampa = RampaDeLaCalleAlta(yP);
        for (int j = 0; j < TramosDeLaRampa; j++)
            PonZona(o, escaleras, "EscalonesDePiedra", "Escalera de la calle alta", new Vector3(InicioDeLaRampa + j * LargoDeTramo, yP + j * SubeElTramo, rampa[0].z), 90f, 1f, sinObstaculo: true);
        // Presa al oeste de la calle alta, con el embalse detrás (al sur).
        PonZona(o, grupo, "PresaDeRisco", "Presa de Risco", new Vector3(PlazaDelEmbalse.x, yC, PlazaDelEmbalse.y + 3.6f), 180f);
        LaminaDeAgua(o, grupo, "Agua del embalse", Disco(PlazaDelEmbalse + new Vector2(0f, -1.2f), 5.6f, 3.0f, 16), yC + 4.4f);

        TerrazaDeLaNoriaYCascada(o, Grupo(grupo, TerrazaDeLaNoria.Nombre), escaleras, yP);
        // Terraza de los huertos: dos casas mirando al mar y las huertas delante.
        Transform huertos = Grupo(grupo, TerrazaDeLosHuertos.Nombre);
        PonZona(o, huertos, "CasaDeRisco_A", "Casa de los huertos", new Vector3(-6f, yH, 472.2f), 180f);
        PonZona(o, huertos, "CasaDeRisco_C", "Casa de los huertos", new Vector3(3.5f, yH, 472.4f), 180f);
        string[] huertas = { FK + "Props/Farm/Farm01_a01.prefab", FK + "Props/Farm/Farm01_b01.prefab" };
        for (int j = 0; j < HuertasDeRisco.Length; j++)
            PonZona0(o, huertos, huertas[j % huertas.Length], "Huerta de Risco", new Vector3(HuertasDeRisco[j].x, yH, HuertasDeRisco[j].y), 90f);
        Vector3[] aLosHuertos = RecorridoDeLaEscaleraDeLosHuertos(yP);
        PonZona(o, escaleras, "EscalonesDePiedra", "Escalera de los huertos", new Vector3(aLosHuertos[0].x - 1.5f, yH, ZDeLaEscaleraDeLosHuertos), -90f, 1f, sinObstaculo: true);
        // La era: casa grande y granero al fondo mirando al mar, el patio con banderines y puestos, y la valla del canto.
        Transform era = Grupo(grupo, LaEra.Nombre);
        PonZona(o, era, "CasaDeRisco_B", "Casa grande de la era", new Vector3(-30f, yE, LaEra.ZMin + 3.5f), 180f, 1.15f);
        PonZona(o, era, "GraneroDelPueblo", "Granero de la era", new Vector3(-14f, yE, LaEra.ZMin + 3.8f), 180f);
        PonZona(o, era, "Banderines", "Banderines de la era", new Vector3(-21f, yE, LaEra.Centro.y + 3f), 0f);
        PonZona(o, era, "PuestoDeMercado_Fruta", "Puesto de la era", new Vector3(-25f, yE, LaEra.Centro.y + 2.5f), 90f);
        PonZona(o, era, "PuestoDeMercado_Pan", "Puesto de la era", new Vector3(-17f, yE, LaEra.Centro.y + 2.5f), -90f);
        PonZona0(o, era, Carreta, "Carro de la era", new Vector3(-8.8f, yE, LaEra.Centro.y + 2f), 20f);
        PonZona0(o, era, Barril, "Barril de la era", new Vector3(-11.2f, yE, LaEra.ZMax - 1.6f), 0f);
        PonZona0(o, era, Sacos, "Sacos de la era", new Vector3(-12.8f, yE, LaEra.ZMax - 1.4f), 40f);
        Valla(o, era, new[] { new Vector2(LaEra.XMin + 1.5f, LaEra.ZMax - 0.4f), new Vector2(LaEra.XMax - 1f, LaEra.ZMax - 0.4f) }, 0f, "Valla de la era");
        var (primero, segundo, cotaDelRellano) = EscaleraALaEra(yP);
        for (int j = 0; j < 3; j++)
        {
            PonZona(o, escaleras, "EscalonesDePiedra", "Escalera de la era", new Vector3(primero[0].x + 0.5f + j * LargoDeTramo, cotaDelRellano + j * SubeElTramo, ZDelPrimerTramoALaEra), 90f, 1f, sinObstaculo: true);
            PonZona(o, escaleras, "EscalonesDePiedra", "Escalera de la era", new Vector3(segundo[1].x - 0.5f - j * LargoDeTramo, yE + j * SubeElTramo, ZDelSegundoTramoALaEra), -90f, 1f, sinObstaculo: true);
        }
        // Barandillas de madera del lado del vacío en las escaleras entre terrazas.
        Valla(o, escaleras, Planta(RecorridoDeLaEscaleraDeLaPlaza(yP)), AnchoDeEscalera * 0.5f + 0.3f, "Barandilla de la escalera");
        Valla(o, escaleras, Planta(primero), AnchoDeEscalera * 0.5f + 0.2f, "Barandilla de la escalera");
        Valla(o, escaleras, Planta(segundo), AnchoDeEscalera * 0.5f + 0.2f, "Barandilla de la escalera");
        // Senda vallada a la pradera, con la valla del lado que cae.
        Transform senda = Grupo(grupo, "Senda vallada");
        int tramos = Valla(o, senda, SendaVallada, AnchoDeLaSendaVallada * 0.5f + 0.4f, "Valla de la senda");
        // Peñas en los cantos y pinos sueltos dentro del pueblo.
        int penas = PenasDeLasTerrazas(o, Grupo(grupo, "Peñas de los cantos"), yP);
        int pinares = PinaresDeRisco(o, Grupo(grupo, "Pinar de la ladera"));
        var az = new System.Random(4417);
        Transform pinos = Grupo(grupo, "Pinos del pueblo");
        Vector2[] sitiosDePinos = { new Vector2(-32.5f, 452.5f), new Vector2(8.2f, 474.3f), new Vector2(-35.5f, 483.6f), new Vector2(-8.6f, 483.4f), new Vector2(-23f, 466.2f) };
        foreach (Vector2 s in sitiosDePinos)
            PonZona0(o, pinos, PinosDeRisco[az.Next(PinosDeRisco.Length)], "Pino de Risco", new Vector3(s.x, AlturaEn(o, s) - 0.2f, s.y), (float)az.NextDouble() * 360f, 0.8f + 0.25f * (float)az.NextDouble());
        o.Informe.Add($"  Risco: calle alta a {yC:0.0} m, plaza a {yP:0.0} m, noria a {yN:0.0} m, huertos a {yH:0.0} m, era a {yE:0.0} m; " +
                      $"{tramos} tramos de valla en la senda, {penas} peñas y {pinares} pinos en la ladera.");
    }

    /// Terraza de la noria: molino, noria en el borde, canal desde la presa y cascada a la garganta; escalera desde la plaza.
    private static void TerrazaDeLaNoriaYCascada(Obra o, Transform grupo, Transform escaleras, float yP)
    {
        float yN = TerrazaDeLaNoria.Cota(yP);
        PonZona(o, grupo, "CasaDeRisco_B", "Molino de Risco", new Vector3(-34.5f, yN, 469.6f), 180f, 1.1f);
        // Plataforma de tablas sobre el vacío, en dos filas, y la rueda encima, de cara al mar.
        for (int fila = -1; fila <= 1; fila += 2)
            for (float d = 1.5f; d < VuelaLaPlataforma + 2f; d += 3f)
                PonZona(o, grupo, "PasarelaDeLaAldea", "Plataforma de la noria", new Vector3(TerrazaDeLaNoria.XMin + 1.5f - d, yN - 1.6f, SitioDeLaNoria.y + fila * 1f), 0f, 1f, sinObstaculo: true);
        Vector3 tamRueda = TamanoDeSerie(o, RuedaDelMolino);
        float escalaRueda = tamRueda.y > 0.1f ? AltoDeLaNoria / tamRueda.y : 1f;
        PonZona0(o, grupo, RuedaDelMolino, "Noria del molino", new Vector3(SitioDeLaNoria.x, yN, SitioDeLaNoria.y), 0f, escalaRueda);
        PonZona0(o, grupo, Barril, "Barril del molino", new Vector3(-27.5f, yN, 467.2f), 0f);
        PonZona0(o, grupo, Sacos, "Sacos del molino", new Vector3(-26f, yN, 467.8f), 70f);
        Vector3[] bajada = RecorridoDeLaEscaleraDeLaPlaza(yP);
        int tramos = Mathf.RoundToInt((yP - yN) / SubeElTramo);
        for (int j = 0; j < tramos; j++)
            PonZona(o, escaleras, "EscalonesDePiedra", "Escalera de la plaza a la noria", new Vector3(bajada[0].x + 1f + j * LargoDeTramo, yN + j * SubeElTramo, ZDeLaEscaleraDeLaPlaza), 90f, 1f, sinObstaculo: true);
        // Canal: lámina sobre su repisa, hasta caer en la noria.
        var canal = new List<Vector3>();
        foreach (Vector3 p in RecorridoDelCanal(yP)) canal.Add(new Vector3(p.x, AlturaEn(o, new Vector2(p.x, p.z)) + 0.1f, p.z));
        canal.Add(new Vector3(SitioDeLaNoria.x, yN + 0.12f, SitioDeLaNoria.y));
        LaminaDeAgua(o, grupo, "Canal del molino", Cinta(canal, 1.1f), 0f);
        // Cascada: cae suelta desde la punta de la plataforma, separándose de la pared, hasta el fondo de la garganta.
        Vector2 dir = HaciaLaGarganta;
        Vector2 labio = new Vector2(TerrazaDeLaNoria.XMin + 1.5f - VuelaLaPlataforma - 1.8f, SitioDeLaNoria.y);
        var recorrido = new List<Vector3> { new Vector3(labio.x, yN + 0.3f, labio.y) };
        Vector3 fondo = recorrido[0];
        float vuelo = 0.6f;
        for (float caida = 1.5f; caida < 160f; caida += 1.5f)
        {
            float y = yN - caida;
            vuelo = Mathf.Max(vuelo, 0.6f + 0.45f * Mathf.Sqrt(caida));
            Vector2 q = labio + dir * vuelo;
            // Si la pared sale más que la cortina, la cortina se aparta de ella.
            while (AlturaEn(o, q) > y - 0.8f && vuelo < 30f) { vuelo += 0.5f; q = labio + dir * vuelo; }
            float suelo = AlturaEn(o, q);
            if (y <= suelo + 0.6f || suelo < CotaDeLaCala + 3f && y < suelo + 2f)
            {
                fondo = new Vector3(q.x, suelo + 0.4f, q.y);
                recorrido.Add(fondo);
                break;
            }
            fondo = new Vector3(q.x, y, q.y);
            recorrido.Add(fondo);
        }
        if (recorrido.Count < 4) { o.Informe.Add("  · Cascada de la noria: no hay pared por donde caer; no se pone."); return; }
        var anchos = new List<float>();
        for (int j = 0; j < recorrido.Count; j++) anchos.Add(Mathf.Lerp(4.2f, 6.5f, j / (float)recorrido.Count));
        var (cortina, espuma) = CascadasDelJuego.Materiales();
        WaterfallBuilder.Build(grupo, "Cascada de la noria", recorrido, anchos, cortina, espuma, 0.3f);
        LaminaDeAgua(o, grupo, "Poza de la cascada", Disco(new Vector2(fondo.x, fondo.z) + dir * 1.5f, 4.2f, 3.4f, 14), fondo.y - 0.3f);
        // Arroyo: de la poza a la cala por el fondo de la garganta.
        var arroyo = new List<Vector3> { new Vector3(fondo.x + dir.x * 2f, fondo.y - 0.38f, fondo.z + dir.y * 2f) };
        for (int j = 1; j < Garganta.Length; j++)
            arroyo.Add(new Vector3(Garganta[j].x, AlturaEn(o, new Vector2(Garganta[j].x, Garganta[j].z)) + 0.12f, Garganta[j].z));
        LaminaDeAgua(o, grupo, "Arroyo de la garganta", Cinta(arroyo, 2.4f), 0f);
        o.Informe.Add($"  Cascada de la noria: cae de {recorrido[0].y:0.0} m a {fondo.y:0.0} m en ({fondo.x:0}, {fondo.z:0}).");
    }

    /// Valla de madera a lo largo de una polilínea, a «lado» metros de su eje del lado por donde baja el terreno (0: sobre
    /// el eje). Devuelve los tramos puestos.
    private static int Valla(Obra o, Transform grupo, IList<Vector2> puntos, float lado, string nombre)
    {
        float largo = Mathf.Max(TamanoDeSerie(o, Barandilla).x, 0.5f);
        int puestos = 0;
        for (int j = 0; j + 1 < puntos.Count; j++)
        {
            Vector2 a = puntos[j], b = puntos[j + 1];
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
            float l = Vector2.Distance(a, b);
            Vector2 medio = (a + b) * 0.5f;
            if (lado > 0f && AlturaEn(o, medio + n * lado) > AlturaEn(o, medio - n * lado)) n = -n;
            for (float s = largo * 0.5f + 0.6f; s < l - 0.6f; s += largo)
            {
                Vector2 q = a + d * s + n * lado;
                if (PonZona0(o, grupo, Barandilla, nombre, new Vector3(q.x, AlturaEn(o, q) - 0.05f, q.y), Rumbo(d) - 90f) != null) puestos++;
            }
        }
        return puestos;
    }
}
