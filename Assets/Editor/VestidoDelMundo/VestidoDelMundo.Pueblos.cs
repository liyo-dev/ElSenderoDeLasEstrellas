using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Suelos de los pueblos: cada pueblo se describe con datos (alfombra, calles, plazas, accesos, huertos)
/// y se pinta con los mismos pinceles. Las sendas salen de la puerta de cada casa (la que da a su calle,
/// ver VestidoDelMundo.Casas) hacia la calle, plaza o acceso más cercano.
public static partial class VestidoDelMundo
{
    private struct Plaza
    {
        public Vector2 Centro, Tamano;
        public float Grados;
        public string Capa, Marco;
        public Plaza(float x, float z, float ancho, float largo, string capa, float grados = 0f, string marco = CapaAdoquin)
        { Centro = new Vector2(x, z); Tamano = new Vector2(ancho, largo); Capa = capa; Grados = grados; Marco = marco; }
        public Vector2 PuntoMasCercano(Vector2 p)
        {
            Vector2 m = Tamano * 0.5f;
            return new Vector2(Mathf.Clamp(p.x, Centro.x - m.x, Centro.x + m.x), Mathf.Clamp(p.y, Centro.y - m.y, Centro.y + m.y));
        }
    }

    /// Casa nueva del vestido: el suelo la tiene en cuenta (puerta y senda) aunque se instancie después.
    private struct CasaNueva
    {
        public string Prefab;
        public Vector2 Pos;
        /// Dirección hacia la calle (grados); la puerta acaba mirando ahí.
        public float Frente;
        /// Media profundidad de la huella en la dirección del frente.
        public float Fondo;
        public CasaNueva(string prefab, float x, float z, float frente, float fondo)
        { Prefab = prefab; Pos = new Vector2(x, z); Frente = frente; Fondo = fondo; }
    }

    private sealed class Pueblo
    {
        public string Nombre;
        public string[] Grupos = new string[0];
        public Vector3[] Alfombra = new Vector3[0];
        public Vector2[][] Calles = new Vector2[0][];
        public float AnchoCalle = 6f;
        public Plaza[] Plazas = new Plaza[0];
        public Vector2[][] Accesos = new Vector2[0][];
        public Rect[] Huertos = new Rect[0];
        public Vector2[] Recinto;   // si hay muralla, no se pinta fuera
        public CasaNueva[] Nuevas = new CasaNueva[0];
        public bool Playa;
        /// Línea (ordenada por x) que separa la hierba (norte) de la arena de la playa (sur).
        public Vector2[] LineaDePlaya;
        /// Si el núcleo de tierra pisada rodea también las plazas (pueblos) o solo las puertas (ciudad).
        public bool NucleoEnPlazas = true;
        /// Estilo de las sendas de las puertas: en la ciudad, adoquín y tierra con piedras; en los pueblos, tierra.
        public string CapaSendaA = CapaTierraPiedras, CapaSendaB = CapaTierra;
        public float RadioNucleo = 6.5f, IntensidadNucleo = 0.8f, RadioPatio = 3.5f;
        public int Semilla;
    }

    private static Rect Huerto(float x, float z, float ancho, float largo) => new Rect(x - ancho * 0.5f - 1f, z - largo * 0.5f - 1f, ancho + 2f, largo + 2f);

    private static Vector2[] P(params float[] xz)
    {
        var r = new Vector2[xz.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xz[2 * i], xz[2 * i + 1]);
        return r;
    }

    // ── Datos de los pueblos (coordenadas de mundo de MainWorld) ──────────────────────────────

    private const string PrefabsEdificios = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Building Combination/";

    /// Muralla del Reino (envolvente de los lienzos).
    private static Vector2[] RecintoDelReino => P(-104f, 300f, -99.7f, 277.5f, -92.1f, 238f, 56.2f, 238f, 95.5f, 246.4f,
        106f, 251.9f, 107.8f, 294.6f, 108f, 300f, 89f, 322.7f, 66.1f, 350f, -66f, 350.1f);

    private static Pueblo Reino() => new()
    {
        Nombre = "Reino",
        Grupos = new[] { "Reino — barrio de montaña y explanada real" },
        Alfombra = new[]
        {
            new Vector3(-60f, 305f, 50f), new Vector3(0f, 300f, 52f), new Vector3(60f, 305f, 50f),
            new Vector3(-55f, 258f, 38f), new Vector3(0f, 258f, 38f), new Vector3(60f, 262f, 40f), new Vector3(0f, 335f, 42f),
        },
        Calles = new[]
        {
            P(-54f, 284f, -54f, 339f),
            P(54f, 284f, 54f, 339f),
            P(-85f, 300f, 50f, 300f),
            P(-85f, 259f, -85f, 300f),
            P(-85f, 259f, 54f, 259f, 95f, 259f),
            P(59.4f, 231.2f, 77.2f, 236.6f, 89.1f, 244.7f, 95f, 255.6f, 95f, 269.4f, 92.2f, 280.3f, 86.6f, 288.4f, 78.1f, 293.8f,
              66.9f, 296.2f, 55.3f, 298.1f, 43.4f, 299.4f, 31.2f, 300f, 18.8f, 300f, 9.4f, 300.9f, 3.1f, 302.8f, 0f, 305.6f, 0f, 315f),
        },
        AnchoCalle = 6.4f,
        Plazas = new[]
        {
            new Plaza(0f, 299f, 40f, 28f, CapaBaldosa),   // Plaza real — audiencia exterior y Demonio 2
            new Plaza(0f, 259f, 48f, 24f, CapaBaldosa),   // Plaza de la taberna — encuentro y persecución
            new Plaza(0f, 315.5f, 22f, 6f, CapaAdoquin),  // explanada ante la puerta del castillo
        },
        Huertos = new[] { Huerto(-30f, 240f, 12f, 9f) },
        Recinto = RecintoDelReino,
        Nuevas = CasasNuevasDelReino(),
        NucleoEnPlazas = false,
        CapaSendaA = CapaAdoquin,
        CapaSendaB = CapaTierraPiedras,
        RadioNucleo = 4.5f,
        IntensidadNucleo = 0.35f,
        RadioPatio = 2.6f,
        Semilla = 4101,
    };

    private static Pueblo Puerto() => new()
    {
        Nombre = "Pueblo pesquero",
        Grupos = new[] { "Pueblo pesquero — calles y muelles" },
        Alfombra = new[]
        {
            new Vector3(262f, -414f, 46f), new Vector3(300f, -418f, 34f), new Vector3(222f, -410f, 30f),
            new Vector3(234f, -452f, 20f), new Vector3(303f, -451f, 20f),
        },
        Plazas = new[] { new Plaza(270f, -433f, 24f, 31f, CapaAdoquin) },
        Accesos = new[]
        {
            P(222.5f, -374.1f, 235.6f, -393.1f, 249.4f, -411.9f, 258f, -423f),
            P(270f, -448.5f, 270f, -456f),
        },
        Huertos = new[] { Huerto(232f, -405f, 12f, 9f) },
        Playa = true,
        LineaDePlaya = P(170f, -428f, 300f, -426f, 330f, -396f, 360f, -388f),
        Semilla = 4202,
    };

    private static Pueblo Vecino() => new()
    {
        Nombre = "Pueblo vecino",
        Grupos = new[] { "Pueblo vecino — terraza sobre la playa" },
        Alfombra = new[] { new Vector3(330f, -110f, 58f) },
        Plazas = new[] { new Plaza(330f, -115f, 30f, 24f, CapaAdoquin) },
        Accesos = new[] { P(342.6f, -56.8f, 342.5f, -73.1f, 337.5f, -89.9f, 333.8f, -102.4f) },
        Huertos = new[] { Huerto(300f, -140f, 12f, 9f), Huerto(360f, -92f, 10f, 9f) },
        Semilla = 4303,
    };

    private static Pueblo Granjas() => new()
    {
        Nombre = "Granjas de la cascada",
        Grupos = new[] { "Granjas de la cascada — campo de Erika (GDD)" },
        Alfombra = new[] { new Vector3(242f, 150f, 42f), new Vector3(262f, 140f, 30f), new Vector3(212f, 158f, 22f) },
        Plazas = new[] { new Plaza(244f, 146f, 12f, 10f, CapaTierraPiedras, marco: CapaTierra) },   // era junto al pozo y los barriles
        Accesos = new[]
        {
            P(216.2f, 123.8f, 225.2f, 142.5f, 232.8f, 162.5f, 238.8f, 183.8f),
            P(272f, 138f, 268.8f, 95f),
        },
        Huertos = new[] { Huerto(254f, 166f, 22f, 12f), Huerto(208f, 142f, 12f, 10f), Huerto(274f, 154f, 14f, 10f) },
        Semilla = 4404,
    };

    private static Pueblo[] Pueblos() => new[] { Reino(), Puerto(), Vecino(), Granjas() };

    // ── Pintura ──────────────────────────────────────────────────────────────────────────────

    private static void PintarZonas(Lienzo l, Scene escena, List<string> informe)
    {
        var giradas = CasasGiradas(escena);
        foreach (Pueblo pueblo in Pueblos())
        {
            int casas = PintarPueblo(l, escena, pueblo, giradas);
            informe.Add($"Suelo de {pueblo.Nombre}: alfombra, {pueblo.Calles.Length} calles, {pueblo.Plazas.Length} plazas y sendas de {casas} casas.");
        }
        PintarSuelosDeParajes(l, informe);
    }

    private static bool DentroDePoligono(Vector2 p, Vector2[] poligono)
    {
        bool dentro = false;
        for (int i = 0, j = poligono.Length - 1; i < poligono.Length; j = i++)
        {
            Vector2 a = poligono[i], b = poligono[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-6f) + a.x) dentro = !dentro;
        }
        return dentro;
    }

    private static int PintarPueblo(Lienzo l, Scene escena, Pueblo pueblo, HashSet<string> giradas)
    {
        TerrainData datos = l.Datos;
        Vector3 origen = l.Terreno.transform.position;
        Vector3 tamano = datos.size;

        bool Fuera(float x, float z)
        {
            foreach (Rect h in pueblo.Huertos) if (h.Contains(new Vector2(x, z))) return true;
            if (pueblo.Recinto != null && !DentroDePoligono(new Vector2(x, z), pueblo.Recinto)) return true;
            float nx = (x - origen.x) / tamano.x, nz = (z - origen.z) / tamano.z;
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return true;
            if (datos.GetSteepness(nx, nz) > 32f) return true;
            return l.Altura(x, z) < 0.6f;
        }

        // 1. Alfombra de hierba con textura. En la playa, al sur de la línea de playa, se repone la arena
        //    (borra los patios cuadrados que dejó el generador) en lugar de poner hierba.
        foreach (Vector3 d in pueblo.Alfombra)
        {
            if (!pueblo.Playa || pueblo.LineaDePlaya == null) { PintarAlfombra(l, new Vector2(d.x, d.y), d.z, pueblo.Semilla, Fuera); continue; }
            PintarAlfombraDePlaya(l, new Vector2(d.x, d.y), d.z, pueblo, Fuera);
        }

        // 2. Puertas: casas que ya están y casas nuevas del vestido.
        var puertas = new List<(Vector2 puerta, Vector2 centro, float radio)>();
        foreach (string nombreGrupo in pueblo.Grupos)
        {
            Transform grupo = BuscarGrupo(escena, nombreGrupo);
            if (grupo == null) continue;
            foreach (Transform casa in grupo)
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (fuente == null || !fuente.name.StartsWith("BuildingAT")) continue;
                Bounds b = LimitesVisibles(casa.gameObject);
                Vector3 frente3 = FrenteDeCasa(casa, fuente.name, giradas);
                var frente = new Vector2(frente3.x, frente3.z).normalized;
                float fondo = Mathf.Abs(frente.x) * b.extents.x + Mathf.Abs(frente.y) * b.extents.z;
                var centro = new Vector2(b.center.x, b.center.z);
                puertas.Add((centro + frente * (fondo + 1.2f), centro, Mathf.Max(b.extents.x, b.extents.z)));
            }
        }
        foreach (CasaNueva n in pueblo.Nuevas)
        {
            float r = n.Frente * Mathf.Deg2Rad;
            var frente = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            puertas.Add((n.Pos + frente * (n.Fondo + 1.2f), n.Pos, n.Fondo + 1f));
        }

        // 3. Núcleo pisado: alrededor de las plazas y delante de las puertas.
        var discos = new List<Vector3>();
        if (pueblo.NucleoEnPlazas)
            foreach (Plaza pl in pueblo.Plazas) discos.Add(new Vector3(pl.Centro.x, pl.Centro.y, Mathf.Max(pl.Tamano.x, pl.Tamano.y) * 0.5f + 5f));
        foreach (var p in puertas) discos.Add(new Vector3(p.puerta.x, p.puerta.y, pueblo.RadioNucleo));
        PintarNucleo(l, discos, pueblo.Semilla + 1, Fuera, pueblo.IntensidadNucleo);

        // 4. Sendas de cada puerta a lo más cercano (calle, plaza o acceso) y su patio. Van antes que
        //    calles y plazas para que estas queden enteras encima del empalme.
        int n2 = 0;
        foreach (var p in puertas)
        {
            Vector2 destino = DestinoMasCercano(pueblo, p.puerta);
            if (Vector2.Distance(destino, p.puerta) < 60f)
                PintarSenda(l, Curva(p.puerta, destino, 0.18f, pueblo.Semilla + n2), 3.4f, pueblo.Semilla + 100 + n2, Fuera, pueblo.CapaSendaA, pueblo.CapaSendaB);
            PintarMancha(l, p.puerta, pueblo.RadioPatio, pueblo.CapaSendaB, 0.8f, pueblo.Semilla + 200 + n2, Fuera);
            n2++;
        }

        // 5. Accesos de tierra, calles empedradas y plazas.
        for (int i = 0; i < pueblo.Accesos.Length; i++) PintarSenda(l, pueblo.Accesos[i], 5f, pueblo.Semilla + 30 + i, Fuera, serpenteo: 0.6f);
        for (int i = 0; i < pueblo.Calles.Length; i++) PintarCalle(l, pueblo.Calles[i], pueblo.AnchoCalle, pueblo.Semilla + 10 + i, Fuera);
        for (int i = 0; i < pueblo.Plazas.Length; i++)
        {
            Plaza pl = pueblo.Plazas[i];
            PintarPlaza(l, pl.Centro, pl.Tamano, pl.Grados, pueblo.Semilla + 50 + i, pl.Capa, 3f, Fuera, pl.Marco);
        }
        return puertas.Count;
    }

    /// Cuánto es playa un punto (0 = hierba, 1 = arena): transición de unos 12 m alrededor de la línea
    /// de playa, con un borde irregular para que no se vea el trazo.
    private static float PesoDePlaya(Pueblo pueblo, float x, float z)
    {
        Vector2[] linea = pueblo.LineaDePlaya;
        float zl = linea[0].y;
        if (x >= linea[linea.Length - 1].x) zl = linea[linea.Length - 1].y;
        else
            for (int j = 0; j < linea.Length - 1; j++)
                if (x >= linea[j].x && x <= linea[j + 1].x)
                {
                    zl = Mathf.Lerp(linea[j].y, linea[j + 1].y, (x - linea[j].x) / Mathf.Max(linea[j + 1].x - linea[j].x, 1e-4f));
                    break;
                }
        float borde = zl + (Ruido.Fbm(x, z, 18f, pueblo.Semilla + 61) - 0.5f) * 18f;
        return 1f - Ruido.Suave(-6f, 6f, z - borde);
    }

    /// Alfombra de un pueblo de playa: hierba al norte de la línea de playa y arena al sur, fundidas.
    /// La arena borra los patios cuadrados que dejó el generador en la playa.
    private static void PintarAlfombraDePlaya(Lienzo l, Vector2 centro, float radio, Pueblo pueblo, Mascara fuera)
    {
        int arena = l.Capa(CapaArena), hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores);
        float ext = radio * 1.25f + 16f;
        l.Recorrer(centro.x - ext, centro.y - ext, centro.x + ext, centro.y + ext, (i, k, x, z) =>
        {
            if (fuera(x, z)) return;
            float d = Vector2.Distance(new Vector2(x, z), centro);
            float r = radio + (Ruido.Fbm(x, z, 45f, pueblo.Semilla) - 0.5f) * radio * 0.45f;
            float s = 1f - Ruido.Suave(r - 16f, r, d);
            if (s <= 0f) return;
            int verde = Ruido.Fbm(x, z, 16f, pueblo.Semilla + 5) > 0.64f ? flores : hierba;
            l.MezclarPar(i, k, verde, arena, s, PesoDePlaya(pueblo, x, z));
        });
    }

    private static Vector2 DestinoMasCercano(Pueblo pueblo, Vector2 p)
    {
        Vector2 mejor = p;
        float d = float.MaxValue;
        void Probar(Vector2[] linea)
        {
            for (int j = 0; j < linea.Length - 1; j++)
            {
                Vector2 a = linea[j], b = linea[j + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                Vector2 q = a + ab * t;
                float dq = Vector2.Distance(p, q);
                if (dq < d) { d = dq; mejor = q; }
            }
        }
        foreach (Vector2[] c in pueblo.Calles) Probar(c);
        foreach (Vector2[] c in pueblo.Accesos) Probar(c);
        foreach (Plaza pl in pueblo.Plazas)
        {
            Vector2 q = pl.PuntoMasCercano(p);
            float dq = Vector2.Distance(p, q);
            if (dq < d) { d = dq; mejor = q; }
        }
        return mejor;
    }

    /// Dirección hacia la calle de una casa del generador: su +Z original. Si el vestido ya la giró,
    /// se deshace el giro en el cálculo.
    private static Vector3 FrenteDeCasa(Transform casa, string prefab, HashSet<string> giradas)
    {
        if (giradas.Contains(RutaJerarquia(casa)) && LadoDeLaPuerta.TryGetValue(prefab, out float lado))
            return casa.rotation * Quaternion.Euler(0f, lado, 0f) * Vector3.forward;
        return casa.forward;
    }
}
