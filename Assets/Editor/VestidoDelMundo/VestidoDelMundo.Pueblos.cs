using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Suelos de los pueblos: cada pueblo se describe con datos (alfombra, calles, plazas, accesos, huertos) y se
/// pinta con los pinceles de pueblo de este archivo. Las sendas salen de la puerta de cada casa (la que da a su
/// calle, ver VestidoDelMundo.Casas) hacia la calle, plaza o acceso más cercano.
///
/// Reglas de la guía de arte que siguen todos los pueblos:
/// · La alfombra de hierba se funde con el campo en 25 m, con el borde roto por ruido de 10 m.
/// · Los bordes de calles, plazas y núcleos pisados se motean con ruido de 6 m o más, nunca celda a celda
///   (a 2,5 m por celda el moteado sale a cuadros), y el borde gastado no pasa de 1,5 m.
/// · Los taludes de 32–45° de dentro y alrededor del pueblo se pintan con suelo de bosque y hierba, nunca con
///   roca: la roca del mapa queda solo por encima de 45°.
///
/// La capital (Pavimentado) no lleva alfombra: sus calles y plazas son mallas (VestidoDelMundo.Pavimento) y
/// aquí se pinta el suelo de la ciudad entre ellas: tierra con piedras en las traseras y hierba en los jardines,
/// al pie de la muralla y en los taludes.
public static partial class VestidoDelMundo
{
    private struct Plaza
    {
        public Vector2 Centro, Tamano;
        public float Grados;
        public string Capa, Marco;
        /// Diámetro (m) del rosetón del centro en las plazas pavimentadas (VestidoDelMundo.Pavimento); 0: sin rosetón.
        public float Roseton;
        public Plaza(float x, float z, float ancho, float largo, string capa, float grados = 0f, string marco = CapaAdoquin, float roseton = 0f)
        { Centro = new Vector2(x, z); Tamano = new Vector2(ancho, largo); Capa = capa; Grados = grados; Marco = marco; Roseton = roseton; }

        /// Plaza alineada con los ejes, dada por sus límites.
        public static Plaza Entre(float xMin, float zMin, float xMax, float zMax, string capa, string marco = CapaAdoquin, float roseton = 0f) =>
            new Plaza((xMin + xMax) * 0.5f, (zMin + zMax) * 0.5f, xMax - xMin, zMax - zMin, capa, 0f, marco, roseton);

        public Vector2 PuntoMasCercano(Vector2 p)
        {
            Vector2 m = Tamano * 0.5f;
            return new Vector2(Mathf.Clamp(p.x, Centro.x - m.x, Centro.x + m.x), Mathf.Clamp(p.y, Centro.y - m.y, Centro.y + m.y));
        }

        /// Distancia en planta desde «p» hasta la plaza (0 dentro).
        public float Distancia(Vector2 p) => Vector2.Distance(p, PuntoMasCercano(p));
    }

    /// Casa nueva del vestido: el suelo la tiene en cuenta (puerta y senda) aunque se instancie después.
    private struct CasaNueva
    {
        /// Nombre del prefab de «Building Combination» (BuildingAT46) o ruta completa de otro prefab (.prefab).
        public string Prefab;
        public Vector2 Pos;
        /// Dirección hacia la calle (grados); la puerta acaba mirando ahí.
        public float Frente;
        /// Nombre del objeto en la escena; null: «Casa nueva N (prefab)».
        public string Nombre;
        public CasaNueva(string prefab, float x, float z, float frente, string nombre = null)
        { Prefab = prefab; Pos = new Vector2(x, z); Frente = frente; Nombre = nombre; }

        public string Ruta => Prefab.EndsWith(".prefab") ? Prefab : PrefabsEdificios + Prefab + ".prefab";
        public string NombrePrefab => System.IO.Path.GetFileNameWithoutExtension(Prefab);
    }

    private sealed class Pueblo
    {
        public string Nombre;
        public string[] Grupos = new string[0];
        public Vector3[] Alfombra = new Vector3[0];
        public Vector2[][] Calles = new Vector2[0][];
        public float AnchoCalle = 6f;
        /// Ancho de cada calle, en el mismo orden que Calles; null (o sin entrada para una calle): AnchoCalle.
        public float[] AnchosDeCalle;
        /// Calles y plazas hechas con mallas de pavimento (VestidoDelMundo.Pavimento): la pintura del pueblo
        /// pone tierra debajo y no pinta adoquín, baldosa ni alfombra donde va el pavimento.
        public bool Pavimentado;
        public Plaza[] Plazas = new Plaza[0];
        public Vector2[][] Accesos = new Vector2[0][];
        public Rect[] Huertos = new Rect[0];
        public Vector2[] Recinto;   // si hay muralla, no se pinta fuera
        /// Zonas verdes dentro del recinto de una ciudad pavimentada (jardines de palacio, hondonadas): hierba.
        public Rect[] Verdes = new Rect[0];
        /// Tramos del camino del mapa que llegan al pueblo por fuera del recinto: se pintan aunque queden fuera.
        public Vector2[][] CaminosDeFuera = new Vector2[0][];
        /// Tramos del camino del mapa que el pueblo deja sin uso: se tapan con la hierba del campo.
        public Vector2[][] CaminosTapados = new Vector2[0][];
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

        /// Ancho de la calle «i» (ver AnchosDeCalle).
        public float AnchoDeCalle(int i) => AnchosDeCalle != null && i >= 0 && i < AnchosDeCalle.Length ? AnchosDeCalle[i] : AnchoCalle;
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

    /// La capital: ciudad amurallada en dos terrazas (y 104 y 112). Recorrido ceremonial: Puerta Real → Plaza de
    /// la Puerta → Vía Real (rampa este y curva hasta la Plaza Real) o Calle Mayor → Plaza del Mercado →
    /// Escalinata Real → Plaza Real → castillo. Las dos rutas forman dos bucles alrededor del talud central
    /// (persecución del capítulo 3). Las casas están en VestidoDelMundo.Capital (CasasNuevasDelReino).
    private static Pueblo Reino() => new()
    {
        Nombre = NombreDelReino,
        Grupos = new[] { "Reino — barrio de montaña y explanada real" },
        Calles = new[]
        {
            // 0. Paso de la Puerta Real: del camino, por el vano de la puerta (6,5 m), a la Plaza de la Puerta.
            P(92.9f, 236f, 92.9f, 247f),
            // 1. Vía Real (Camino 10): sube por la rampa este, gira junto a la casa de la torre verde y entra
            //    recta en la Plaza Real por z 300.
            P(95f, 260f, 95f, 269.4f, 94f, 278.5f, 90.5f, 286.5f, 84f, 293f, 74.5f, 297.8f, 62f, 300f, 44f, 300f, 28f, 300f),
            // 2. Calle Mayor: de la Plaza de la Puerta a la Plaza del Mercado, entre puestos y oficios.
            P(76f, 258f, 24f, 258f),
            // 3. Calle de la Taberna: rodea la taberna por el norte y sigue hacia poniente.
            P(-24f, 268.5f, -46f, 268.5f, -52f, 260f, -86f, 259f),
            // 4. Rampa de poniente (Camino 14): de la terraza baja a la alta.
            P(-85f, 259f, -85f, 300f),
            // 5. Calle de Poniente: de la Plaza Real a la rampa.
            P(-28f, 300f, -88f, 300f),
            // 6–7. Calles del Cuartel y del Archivo: de las vías principales a los flancos del castillo.
            P(-54f, 300f, -54f, 339f),
            P(54f, 300f, 54f, 339f),
        },
        AnchosDeCalle = new[] { 6.4f, 8f, 7f, 6.4f, 6.4f, 7f, 6.4f, 6.4f },
        AnchoCalle = 6.4f,
        Pavimentado = true,
        Plazas = new[]
        {
            Plaza.Entre(-28f, 283.5f, 28f, 312.5f, CapaBaldosa, roseton: 9.3f),   // Plaza Real: audiencia y Demonio 2 (arena de 25 m en (0, 300))
            Plaza.Entre(-24f, 247f, 24f, 271f, CapaBaldosa),                       // Plaza del Mercado y de la Taberna
            Plaza.Entre(-22f, 312.5f, 22f, 319f, CapaAdoquin),                     // explanada ante la puerta del castillo
            Plaza.Entre(74f, 245f, 100f, 262f, CapaAdoquin),                       // Plaza de la Puerta Real
        },
        Verdes = new[]
        {
            Rect.MinMaxRect(-101f, 316f, -78f, 350f),     // jardín de palacio de poniente (parterres de x −87)
            Rect.MinMaxRect(78.5f, 316f, 106f, 350f),     // jardín de palacio de levante (parterres de x 87)
            Rect.MinMaxRect(-47f, 243f, -17f, 254.5f),    // hondonada al pie de la muralla sur, junto a la taberna
        },
        // El Camino 10 llega de frente al vano de la Puerta Real; el tramo del generador que subía en diagonal
        // hasta la torre de poniente de la puerta queda sin uso y se tapa.
        CaminosDeFuera = new[] { P(59.4f, 231.2f, 72f, 233.2f, 84f, 234.2f, 90.5f, 235f, 92.9f, 238.5f) },
        CaminosTapados = new[] { P(70f, 234.4f, 77.2f, 236.6f, 83f, 240.6f) },
        Recinto = TrazaMuralla,
        Nuevas = CasasNuevasDelReino(),
        NucleoEnPlazas = false,
        CapaSendaA = CapaTierraPiedras,
        CapaSendaB = CapaTierra,
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

    // Capas del mapa que pinta solo este archivo (las de los pueblos están en VestidoDelMundo.Suelo).
    private const string CapaHierbaDelCampo = "Capa1";            // hierba del campo
    private const string CapaTaludDePueblo = "Capa5";             // suelo de bosque: taludes de 32–45°
    private const string CapaBordeDeCaminoDelMapa = "Capa4";      // tierra del borde de los caminos del mapa
    private const string CapaCaminoDelMapa = "SueloUrbano1";      // tierra con piedras del centro de los caminos del mapa

    /// Ancho, en metros, del fundido de la alfombra de un pueblo con el campo, y cuánto sale de su radio.
    private const float FundidoDeAlfombra = 25f, SalidaDelFundido = 10f;

    /// Margen alrededor de un pueblo en el que todavía se le quitan los taludes de roca.
    private const float MargenDeTaludes = 12f;

    private static void PintarZonas(Lienzo l, Scene escena, List<string> informe)
    {
        var giradas = CasasGiradas(escena);
        foreach (Pueblo pueblo in Pueblos()) informe.Add(PintarPueblo(l, escena, pueblo, giradas));
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

    /// El polígono cerrado como polilínea (repite el primer punto al final).
    private static Vector2[] PoligonoCerrado(Vector2[] poligono)
    {
        var r = new Vector2[poligono.Length + 1];
        System.Array.Copy(poligono, r, poligono.Length);
        r[poligono.Length] = poligono[0];
        return r;
    }

    private static float DistanciaAlRectangulo(Rect r, Vector2 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// Distancia en planta desde «p» hasta el borde de la calle o plaza más cercana del pueblo (0 encima).
    private static float DistanciaAlSueloDeCalle(Pueblo pueblo, Vector2 p)
    {
        float d = float.MaxValue;
        for (int i = 0; i < pueblo.Calles.Length; i++)
            d = Mathf.Min(d, DistanciaAPolilinea(p, pueblo.Calles[i]) - pueblo.AnchoDeCalle(i) * 0.5f);
        foreach (Plaza pl in pueblo.Plazas) d = Mathf.Min(d, pl.Distancia(p));
        return Mathf.Max(0f, d);
    }

    /// Pinta el suelo de un pueblo y devuelve la línea del informe.
    private static string PintarPueblo(Lienzo l, Scene escena, Pueblo pueblo, HashSet<string> giradas)
    {
        TerrainData datos = l.Datos;
        Vector3 origen = l.Terreno.transform.position;
        Vector3 tamano = datos.size;

        // Fuera del mapa o en el mar.
        bool FueraDelMapa(float x, float z)
        {
            float nx = (x - origen.x) / tamano.x, nz = (z - origen.z) / tamano.z;
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return true;
            return l.Altura(x, z) < 0.6f;
        }
        // Fuera del suelo del pueblo: huertos, fuera de la muralla, fuera del mapa o en el mar. La pendiente la
        // resuelve cada pincel (la alfombra se desvanece; lo pisado no sube por los taludes).
        bool FueraDelPueblo(float x, float z)
        {
            foreach (Rect h in pueblo.Huertos) if (h.Contains(new Vector2(x, z))) return true;
            if (pueblo.Recinto != null && !DentroDePoligono(new Vector2(x, z), pueblo.Recinto)) return true;
            return FueraDelMapa(x, z);
        }
        bool Fuera(float x, float z) => FueraDelPueblo(x, z) || l.Pendiente(x, z) > 32f;

        // 1. Caminos del mapa: se tapa lo que el pueblo deja sin uso y se pinta el desvío (puede quedar fuera
        //    del recinto: llega hasta la puerta).
        for (int i = 0; i < pueblo.CaminosTapados.Length; i++) TaparCaminoDelMapa(l, pueblo.CaminosTapados[i], 6.4f, pueblo.Semilla + 70 + i, FueraDelMapa);
        for (int i = 0; i < pueblo.CaminosDeFuera.Length; i++) PintarCaminoDelMapa(l, pueblo.CaminosDeFuera[i], 6.4f, pueblo.Semilla + 75 + i, FueraDelMapa);

        // 2. Fondo: alfombra de hierba con textura en los pueblos (en la playa, al sur de la línea de playa,
        //    arena: borra los patios cuadrados que dejó el generador) o suelo de ciudad en la capital.
        int ciudad = 0;
        if (pueblo.Pavimentado) ciudad = PintarSueloDeCiudad(l, pueblo, FueraDelPueblo);
        else
            foreach (Vector3 d in pueblo.Alfombra)
            {
                if (!pueblo.Playa || pueblo.LineaDePlaya == null) PintarAlfombraDePueblo(l, new Vector2(d.x, d.y), d.z, pueblo.Semilla, FueraDelPueblo);
                else PintarAlfombraDePlaya(l, new Vector2(d.x, d.y), d.z, pueblo, FueraDelPueblo);
            }

        // 3. Puertas: casas que ya están y casas nuevas que el vestido ha colocado.
        List<Puerta> puertas = PuertasDelPueblo(escena, pueblo, giradas);

        // 4. Núcleo pisado: alrededor de las plazas y delante de las puertas.
        var discos = new List<Vector3>();
        if (pueblo.NucleoEnPlazas)
            foreach (Plaza pl in pueblo.Plazas) discos.Add(new Vector3(pl.Centro.x, pl.Centro.y, Mathf.Max(pl.Tamano.x, pl.Tamano.y) * 0.5f + 5f));
        foreach (Puerta p in puertas) discos.Add(new Vector3(p.Pos.x, p.Pos.y, pueblo.RadioNucleo));
        PintarNucleoDePueblo(l, discos, pueblo.Semilla + 1, Fuera, pueblo.IntensidadNucleo);

        // 5. Sendas de cada puerta a lo más cercano (calle, plaza o acceso) y su patio. Van antes que
        //    calles y plazas para que estas queden enteras encima del empalme.
        int n2 = 0;
        foreach (Puerta p in puertas)
        {
            Vector2 destino = DestinoMasCercano(pueblo, p.Pos);
            if (Vector2.Distance(destino, p.Pos) < 60f)
                PintarSenda(l, Curva(p.Pos, destino, 0.18f, pueblo.Semilla + n2), 3.4f, pueblo.Semilla + 100 + n2, Fuera, pueblo.CapaSendaA, pueblo.CapaSendaB);
            PintarMancha(l, p.Pos, pueblo.RadioPatio, pueblo.CapaSendaB, 0.8f, pueblo.Semilla + 200 + n2, Fuera);
            n2++;
        }

        // 6. Accesos de tierra y, si no van pavimentadas, calles empedradas y plazas.
        for (int i = 0; i < pueblo.Accesos.Length; i++) PintarSenda(l, pueblo.Accesos[i], 5f, pueblo.Semilla + 30 + i, Fuera, serpenteo: 0.6f);
        if (!pueblo.Pavimentado)
        {
            for (int i = 0; i < pueblo.Calles.Length; i++) PintarCalleDePueblo(l, pueblo.Calles[i], pueblo.AnchoDeCalle(i), pueblo.Semilla + 10 + i, Fuera);
            for (int i = 0; i < pueblo.Plazas.Length; i++) PintarPlazaDePueblo(l, pueblo.Plazas[i], pueblo.Semilla + 50 + i, Fuera);
        }

        // 7. Taludes de dentro y alrededor: sin roca hasta 45°.
        int taludes = PintarTaludesDePueblo(l, pueblo, FueraDelMapa);

        return pueblo.Pavimentado
            ? $"Suelo de {pueblo.Nombre}: suelo de ciudad ({ciudad} celdas: tierra en las traseras, hierba en jardines, al pie de la muralla y en los taludes), " +
              $"{pueblo.Calles.Length} calles y {pueblo.Plazas.Length} plazas en malla de pavimento, sendas de {puertas.Count} casas, {taludes} celdas de talud sin roca" +
              (pueblo.CaminosDeFuera.Length > 0 ? ", camino de la puerta desviado." : ".")
            : $"Suelo de {pueblo.Nombre}: alfombra con fundido de {FundidoDeAlfombra:0} m, {pueblo.Plazas.Length} plazas, {pueblo.Accesos.Length} accesos, " +
              $"sendas de {puertas.Count} casas y {taludes} celdas de talud sin roca.";
    }

    // ── Pinceles de pueblo ───────────────────────────────────────────────────────────────────

    /// Peso de la alfombra en un punto: 1 hasta 15 m antes de su radio (deformado en grande a 45 m) y fundido con
    /// el campo en FundidoDeAlfombra metros, por un borde roto a escala de 10 m; desvanecido en los taludes
    /// (28–34°). El fundido sale 10 m fuera del radio para no encoger el centro del pueblo.
    private static float PesoDeAlfombra(Lienzo l, Vector2 centro, float radio, int semilla, float x, float z)
    {
        float d = Vector2.Distance(new Vector2(x, z), centro) + (Ruido.Fbm(x, z, 10f, semilla + 7) - 0.5f) * 9f;
        float r = radio + (Ruido.Fbm(x, z, 45f, semilla) - 0.5f) * radio * 0.45f;
        float s = 1f - Ruido.Suave(r - FundidoDeAlfombra + SalidaDelFundido, r + SalidaDelFundido, d);
        return s <= 0f ? 0f : s * (1f - Ruido.Suave(28f, 34f, l.Pendiente(x, z)));
    }

    /// Alfombra de hierba con textura (con manchas de florecillas) que se funde con el campo.
    private static void PintarAlfombraDePueblo(Lienzo l, Vector2 centro, float radio, int semilla, Mascara fuera)
    {
        int hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores);
        float ext = radio * 1.25f + SalidaDelFundido + 10f;
        l.Recorrer(centro.x - ext, centro.y - ext, centro.x + ext, centro.y + ext, (i, k, x, z) =>
        {
            if (fuera(x, z)) return;
            float s = PesoDeAlfombra(l, centro, radio, semilla, x, z);
            if (s <= 0f) return;
            l.Mezclar(i, k, Ruido.Fbm(x, z, 16f, semilla + 5) > 0.64f ? flores : hierba, s);
        });
    }

    /// Alfombra de un pueblo de playa: hierba al norte de la línea de playa y arena al sur, fundidas.
    /// La arena borra los patios cuadrados que dejó el generador en la playa.
    private static void PintarAlfombraDePlaya(Lienzo l, Vector2 centro, float radio, Pueblo pueblo, Mascara fuera)
    {
        int arena = l.Capa(CapaArena), hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores);
        float ext = radio * 1.25f + SalidaDelFundido + 10f;
        l.Recorrer(centro.x - ext, centro.y - ext, centro.x + ext, centro.y + ext, (i, k, x, z) =>
        {
            if (fuera(x, z)) return;
            float s = PesoDeAlfombra(l, centro, radio, pueblo.Semilla, x, z);
            if (s <= 0f) return;
            int verde = Ruido.Fbm(x, z, 16f, pueblo.Semilla + 5) > 0.64f ? flores : hierba;
            l.MezclarPar(i, k, verde, arena, s, PesoDePlaya(pueblo, x, z));
        });
    }

    /// Núcleo de tierra pisada: unión de discos con borde roto, islas de hierba y moteado (ruido de 6–11 m).
    private static void PintarNucleoDePueblo(Lienzo l, IList<Vector3> discos, int semilla, Mascara mascara, float intensidad = 1f)
    {
        if (discos.Count == 0) return;
        int tierra = l.Capa(CapaTierra), piedras = l.Capa(CapaTierraPiedras);
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
        foreach (Vector3 d in discos)
        {
            x0 = Mathf.Min(x0, d.x - d.z); x1 = Mathf.Max(x1, d.x + d.z);
            z0 = Mathf.Min(z0, d.y - d.z); z1 = Mathf.Max(z1, d.y + d.z);
        }
        l.Recorrer(x0 - 8f, z0 - 8f, x1 + 8f, z1 + 8f, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float campo = -1f;
            foreach (Vector3 d in discos)
                campo = Mathf.Max(campo, 1f - Vector2.Distance(new Vector2(x, z), new Vector2(d.x, d.y)) / d.z);
            campo += (Ruido.Fbm(x, z, 11f, semilla) - 0.5f) * 0.55f;
            if (campo <= 0f) return;
            float s = Ruido.Suave(0f, 0.28f, campo) * intensidad;
            if (campo < 0.22f) s *= Mathf.Lerp(0.35f, 1f, Ruido.Suave(0.42f, 0.58f, Ruido.Fbm(x, z, 6f, semilla + 77)));
            if (Ruido.Fbm(x, z, 7f, semilla + 3) < 0.3f && campo < 0.6f) s *= 0.4f;
            l.Mezclar(i, k, Ruido.Fbm(x, z, 6f, semilla + 9) > 0.5f ? piedras : tierra, s, duro: true);
        });
    }

    /// Borde gastado de calles y plazas: como mucho 1,5 m, mitad de la capa del borde y mitad de tierra con
    /// piedras, moteado con ruido de 6–7 m.
    private const float BordeGastado = 1.5f;

    /// Calle empedrada (pueblos sin pavimento): adoquín con alguna calva de tierra y borde gastado.
    private static void PintarCalleDePueblo(Lienzo l, Vector2[] puntos, float ancho, int semilla, Mascara mascara)
    {
        int adoquin = l.Capa(CapaAdoquin), piedras = l.Capa(CapaTierraPiedras), tierra = l.Capa(CapaTierra);
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector2 q in puntos)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y);
        }
        float m = ancho + 4f;
        l.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float d = DistanciaAPolilinea(new Vector2(x, z), puntos);
            float r = ancho * 0.5f + (Ruido.Fbm(x, z, 9f, semilla) - 0.5f) * 0.8f;
            if (d > r + BordeGastado) return;
            if (d <= r)
            {
                bool calva = Ruido.Fbm(x, z, 6f, semilla + 4) < 0.22f;
                l.Mezclar(i, k, calva ? tierra : adoquin, calva ? 0.7f : 1f, duro: true);
                return;
            }
            float s = 1f - Ruido.Suave(r, r + BordeGastado, d);
            l.MezclarPar(i, k, adoquin, piedras, s, 0.5f * Ruido.Suave(0.4f, 0.6f, Ruido.Fbm(x, z, 6f, semilla + 8)) + 0.25f);
            if (s >= 0.45f) l.Duro[k, i] = true;
        });
    }

    /// Plaza rectangular (girada sus grados alrededor del centro) con borde gastado.
    private static void PintarPlazaDePueblo(Lienzo l, Plaza pl, int semilla, Mascara mascara)
    {
        int interior = l.Capa(pl.Capa), marco = l.Capa(pl.Marco), piedras = l.Capa(CapaTierraPiedras);
        float rad = pl.Grados * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
        Vector2 media = pl.Tamano * 0.5f, centro = pl.Centro;
        float ext = media.magnitude + BordeGastado + 4f;
        l.Recorrer(centro.x - ext, centro.y - ext, centro.x + ext, centro.y + ext, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float dx = x - centro.x, dz = z - centro.y;
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            float fx = Mathf.Max(Mathf.Abs(lx) - media.x, 0f), fz = Mathf.Max(Mathf.Abs(lz) - media.y, 0f);
            float fuera = Mathf.Sqrt(fx * fx + fz * fz);
            float dentro = Mathf.Min(media.x - Mathf.Abs(lx), media.y - Mathf.Abs(lz));
            float n = (Ruido.Fbm(x, z, 6f, semilla) - 0.5f) * 1.2f;
            if (fuera <= 0f && dentro > 1.2f + n) { l.Mezclar(i, k, interior, 1f, duro: true); return; }
            if (fuera >= BordeGastado + n) return;
            float s = Mathf.Max(0.35f, 1f - Ruido.Suave(0f, BordeGastado + n, fuera));
            l.MezclarPar(i, k, marco, piedras, s, 0.5f * Ruido.Suave(0.4f, 0.6f, Ruido.Fbm(x, z, 7f, semilla + 3)));
            if (s >= 0.45f) l.Duro[k, i] = true;
        });
    }

    /// Suelo de una ciudad pavimentada entre calles, plazas y casas: tierra con piedras y tierra pisada junto a
    /// las calles, y hierba en los jardines (Verdes), en una franja al pie de la muralla, en las pendientes
    /// suaves y en el fondo de las manzanas (huertecillos y patios traseros, a más de 6 m de la calle, a manchas).
    /// Al borde del pavimento no hay hierba (lo remata el bordillo); debajo de él pone tierra
    /// VestidoDelMundo.Pavimento. Devuelve cuántas celdas ha pintado.
    private static int PintarSueloDeCiudad(Lienzo l, Pueblo pueblo, Mascara fuera)
    {
        if (pueblo.Recinto == null || pueblo.Recinto.Length < 3) return 0;
        int piedras = l.Capa(CapaTierraPiedras), tierra = l.Capa(CapaTierra), hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores);
        Vector2[] muralla = PoligonoCerrado(pueblo.Recinto);
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector2 q in pueblo.Recinto)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y);
        }
        int semilla = pueblo.Semilla + 60, n = 0;
        l.Recorrer(minX, minZ, maxX, maxZ, (i, k, x, z) =>
        {
            if (fuera(x, z)) return;
            float pendiente = l.Pendiente(x, z);
            float s = 1f - Ruido.Suave(28f, 34f, pendiente);
            if (s <= 0f) return;
            var p = new Vector2(x, z);
            float calle = DistanciaAlSueloDeCalle(pueblo, p);

            float jardin = 0f;
            foreach (Rect v in pueblo.Verdes)
                jardin = Mathf.Max(jardin, 1f - Ruido.Suave(0f, 3f, DistanciaAlRectangulo(v, p) + (Ruido.Fbm(x, z, 8f, semilla + 2) - 0.5f) * 3f));
            float muro = DistanciaAPolilinea(p, muralla) + (Ruido.Fbm(x, z, 10f, semilla + 1) - 0.5f) * 3f;
            float verde = Mathf.Max(jardin, 1f - Ruido.Suave(5f, 8.5f, muro));
            verde = Mathf.Max(verde, Ruido.Suave(12f, 22f, pendiente));
            verde = Mathf.Max(verde, Ruido.Suave(0.42f, 0.62f, Ruido.Fbm(x, z, 12f, semilla + 3)) * Ruido.Suave(6f, 12f, calle));
            verde *= Ruido.Suave(0.5f, 2.5f, calle);

            l.MezclarPar(i, k, piedras, tierra, s, Ruido.Suave(0.35f, 0.65f, Ruido.Fbm(x, z, 9f, semilla + 4)));
            if (verde > 0.01f)
                l.Mezclar(i, k, jardin > 0.5f && Ruido.Fbm(x, z, 16f, semilla + 5) > 0.62f ? flores : hierba, s * verde);
            if (s * (1f - verde) >= 0.5f) l.Duro[k, i] = true;
            n++;
        });
        return n;
    }

    /// Taludes de dentro y alrededor del pueblo (hasta MargenDeTaludes fuera de la alfombra o de la muralla):
    /// entre 28° y 34° la hierba cede al suelo de bosque, que llega hasta 45°; por encima queda la roca del mapa.
    /// Dentro del pueblo la hierba es la de la alfombra; fuera, la del campo. Dentro de una muralla el talud es
    /// un terraplén de la ciudad, no un cortado del monte: el suelo de bosque sube hasta 55° y solo lo que pasa
    /// de ahí queda de roca, limpia (el generador le dejó adoquín y tierra de las calles viejas).
    /// Devuelve cuántas celdas ha tocado.
    private static int PintarTaludesDePueblo(Lienzo l, Pueblo pueblo, Mascara fuera)
    {
        int talud = l.Capa(CapaTaludDePueblo), roca = l.Capa(CapaRoca), hierbaPueblo = l.Capa(CapaHierba), hierbaCampo = l.Capa(CapaHierbaDelCampo);
        if (talud < 0) return 0;
        Vector2[] muralla = pueblo.Recinto != null && pueblo.Recinto.Length >= 3 ? PoligonoCerrado(pueblo.Recinto) : null;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        void Abarcar(float x, float z, float r)
        {
            minX = Mathf.Min(minX, x - r); maxX = Mathf.Max(maxX, x + r);
            minZ = Mathf.Min(minZ, z - r); maxZ = Mathf.Max(maxZ, z + r);
        }
        if (muralla != null) foreach (Vector2 q in pueblo.Recinto) Abarcar(q.x, q.y, MargenDeTaludes);
        foreach (Vector3 d in pueblo.Alfombra) Abarcar(d.x, d.y, d.z + FundidoDeAlfombra + MargenDeTaludes);
        if (minX > maxX) return 0;

        // Cuánto es del pueblo un punto: 1 dentro (de la muralla o del radio de la alfombra) y 0 a
        // MargenDeTaludes metros fuera. «dentro» dice si cuenta como suelo del pueblo (hierba de la alfombra).
        float Cercania(Vector2 p, out bool dentro)
        {
            dentro = false;
            float fueraDe = float.MaxValue;
            if (muralla != null)
            {
                if (DentroDePoligono(p, pueblo.Recinto)) { dentro = true; return 1f; }
                fueraDe = DistanciaAPolilinea(p, muralla);
            }
            foreach (Vector3 d in pueblo.Alfombra)
            {
                float e = Vector2.Distance(p, new Vector2(d.x, d.y)) - d.z;
                if (e <= 0f) dentro = true;
                fueraDe = Mathf.Min(fueraDe, Mathf.Max(0f, e - FundidoDeAlfombra * 0.5f));
            }
            return dentro ? 1f : 1f - Ruido.Suave(0f, MargenDeTaludes, fueraDe);
        }

        int semilla = pueblo.Semilla + 80, n = 0;
        l.Recorrer(minX, minZ, maxX, maxZ, (i, k, x, z) =>
        {
            if (fuera(x, z)) return;
            float pendiente = l.Pendiente(x, z);
            if (pendiente < 28f) return;
            float w = Ruido.Suave(28f, 34f, pendiente) * (1f - Ruido.Suave(44f, 48f, pendiente));
            foreach (Rect h in pueblo.Huertos) if (h.Contains(new Vector2(x, z))) return;
            float c = Cercania(new Vector2(x, z), out bool dentro);
            if (c <= 0f) return;
            float cortado = 0f;
            if (dentro && muralla != null)
            {
                cortado = Ruido.Suave(54f, 58f, pendiente);
                w = Ruido.Suave(28f, 34f, pendiente) * (1f - cortado);
            }
            if (w <= 0f && cortado <= 0f) return;
            float t = Mathf.Clamp01(Ruido.Suave(31f, 38f, pendiente) + (Ruido.Fbm(x, z, 8f, semilla) - 0.5f) * 0.4f);
            if (w > 0f) l.MezclarPar(i, k, dentro ? hierbaPueblo : hierbaCampo, talud, w * c, t);
            if (cortado > 0f) l.Mezclar(i, k, roca, cortado);
            n++;
        });
        return n;
    }

    /// Camino del mapa, como los del generador: centro de tierra con piedras y bordes de tierra de camino,
    /// moteados con ruido de 6 m. No sube por los taludes de más de 40°.
    private static void PintarCaminoDelMapa(Lienzo l, Vector2[] puntos, float ancho, int semilla, Mascara mascara)
    {
        int centro = l.Capa(CapaCaminoDelMapa), borde = l.Capa(CapaBordeDeCaminoDelMapa);
        if (centro < 0 || borde < 0 || puntos == null || puntos.Length < 2) return;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector2 q in puntos)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y);
        }
        float r = ancho * 0.5f, m = r + 3f;
        l.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            if (mascara(x, z) || l.Pendiente(x, z) > 40f) return;
            float d = DistanciaAPolilinea(new Vector2(x, z), puntos) + (Ruido.Fbm(x, z, 6f, semilla) - 0.5f) * 1.2f;
            if (d > r + BordeGastado) return;
            float s = d <= r * 0.8f ? 1f : 1f - Ruido.Suave(r * 0.8f, r + BordeGastado, d);
            l.MezclarPar(i, k, centro, borde, s, Ruido.Suave(r * 0.8f, r + 0.5f, d));
            if (s >= 0.45f) l.Duro[k, i] = true;
        });
    }

    /// Tapa con la hierba del campo un tramo de camino del mapa que se ha quedado sin uso.
    private static void TaparCaminoDelMapa(Lienzo l, Vector2[] puntos, float ancho, int semilla, Mascara mascara)
    {
        int campo = l.Capa(CapaHierbaDelCampo);
        if (campo < 0 || puntos == null || puntos.Length < 2) return;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector2 q in puntos)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y);
        }
        float r = ancho * 0.5f, m = r + 4f;
        l.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            if (mascara(x, z)) return;
            float d = DistanciaAPolilinea(new Vector2(x, z), puntos) + (Ruido.Fbm(x, z, 6f, semilla) - 0.5f) * 1.5f;
            float s = 1f - Ruido.Suave(r, r + 2.5f, d);
            if (s > 0f) l.Mezclar(i, k, campo, s);
        });
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
        if (giradas.Contains(IdDe(casa.gameObject)) && LadoDeLaPuerta.TryGetValue(prefab, out float lado))
            return casa.rotation * Quaternion.Euler(0f, lado, 0f) * Vector3.forward;
        return casa.forward;
    }
}
