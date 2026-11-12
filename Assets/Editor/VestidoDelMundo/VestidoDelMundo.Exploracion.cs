using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Exploración del mundo abierto con piezas propias (Tools/Modelos/Zonas): casas de campo del pueblo inicial,
/// arcos de entrada a los pueblos, la cueva de la cascada, la subida en repisas, los templos de las pruebas y el
/// mirador de la cumbre.
///
/// · Sin escalada: lo que sube, sube por peldaños o saltando de repisa en repisa; lo muy alto es destino de vuelo.
/// · Los túneles van bajo el terreno: se abre el terreno (TerrainData.SetHoles) solo donde su superficie cruzaría el
///   hueco del túnel. Los agujeros se cierran al empezar cada vestido y al quitarlo.
/// · Los templos no imitan las Ruinas del Libro (TDD § 23.4): piedra clara con musgo, arco apuntado y aros de bronce.
public static partial class VestidoDelMundo
{
    private const string TunelRecto = PrefabsDeZonas + "TunelDeRoca.prefab";
    private const string TunelConEscalera = PrefabsDeZonas + "TunelEscalonado.prefab";
    private const string BocaDeLaCueva = PrefabsDeZonas + "BocaDeCueva.prefab";
    private const string SalaDeLaCueva = PrefabsDeZonas + "SalaDeLaCueva.prefab";
    private const string EntradaDelTemplo = PrefabsDeZonas + "EntradaDeTemplo.prefab";
    private const string MiradorDeLaCumbre = PrefabsDeZonas + "MiradorDeCumbre.prefab";
    private const string ArcoDeWill = PrefabsDeZonas + "ArcoDelPueblo.prefab";
    private const string ArcoDelMar = PrefabsDeZonas + "ArcoDeLaAldea.prefab";
    private const string HiedraQueTapa = PrefabsDeZonas + "CortinaDeHiedra.prefab";
    private static readonly string[] Repisas = { "RepisaDeRoca_Mediana", "RepisaDeRoca_Pequena", "RepisaDeRoca_Grande" };

    /// Medidas de las piezas de túnel (m), como salen de zonas.py.
    private const float LargoDeBoca = 5f, LargoDeTunel = 8f, SubeLaEscalera = 5f, LargoDeSala = 12.6f;
    private const float SemianchoDelHueco = 2.3f, AltoDelHueco = 4.2f;

    private static void ExplorarElMundo(Obra o)
    {
        Transform grupo = Grupo(o.Raiz, "Exploración");
        CerrarElTerreno(o);
        foreach (float radio in new[] { 64f, 76f, 90f })
            if (ArcosDeEntrada(o, Grupo(grupo, "Arcos de entrada"), ArcoDeWill, "Arco del pueblo", new Vector2(0f, -130f), radio) > 0) break;
        ArcosDeEntrada(o, Grupo(grupo, "Arcos de entrada"), ArcoDelMar, "Arco de la aldea", new Vector2(270f, -425f), 62f);
        CuevaDeLaCascada(o, Grupo(grupo, "Cueva de la cascada"));
        TunelPorElTerreno(o, Grupo(grupo, "Paso del suroeste"), "Paso del suroeste", new Vector2(-327f, -394f), new Vector2(-327f, -339.4f), conSala: true);
        foreach (var (nombre, centro, radio) in TemplosDeLasPruebas)
            Templo(o, Grupo(grupo, "Templos"), nombre, centro, radio);
        MiradorEnLaCumbre(o, Grupo(grupo, "Mirador de la cumbre"));
    }

    // ── Piezas a cota exacta ─────────────────────────────────────────────────────────────────

    /// Instancia una pieza con su origen en «pos» (sin apoyarla ni comprobar choques: va enterrada o a una cota
    /// calculada) y con su +Z local hacia «rumbo». Reserva su huella para que nada del vestido se le monte.
    private static GameObject PonExacto(Obra o, Transform grupo, string prefab, string nombre, Vector3 pos, float rumbo, bool sinObstaculo = false)
    {
        GameObject modelo = CargarPrefab(o, prefab);
        if (modelo == null) { o.Informe.Add($"  · falta el prefab de «{nombre}»: {prefab}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, grupo);
        go.name = nombre;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rumbo, 0f) * modelo.transform.localRotation);
        if (sinObstaculo) o.SinObstaculo.Add(go.transform);
        o.Ocupado.Add(HuellaOrientada(go, rumbo));
        o.Puestas++;
        return go;
    }

    // ── Terreno abierto para los túneles ─────────────────────────────────────────────────────

    /// Cierra todos los agujeros del terreno. MainWorld no tiene más agujeros que los de estos túneles.
    private static void CerrarElTerreno(Obra o) => CerrarElTerreno(o.Terreno.terrainData);

    private static void CerrarElTerreno(TerrainData datos)
    {
        int res = datos.holesResolution;
        bool[,] solido = datos.GetHoles(0, 0, res, res);
        bool habia = false;
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
                if (!solido[k, i]) { solido[k, i] = true; habia = true; }
        if (habia) datos.SetHoles(0, 0, solido);
    }

    /// Abre el terreno a lo largo del tramo a→b donde su superficie cruzaría el hueco de un túnel cuyo suelo, a
    /// «s» metros de a, está en suelo(s). Devuelve cuántas celdas ha abierto y, en «aire», los metros de tramo en
    /// los que el terreno queda más de 1 m por debajo del suelo del túnel (el túnel asomaría en el aire).
    private static int AbrirElTerreno(Obra o, Vector2 a, Vector2 b, System.Func<float, float> suelo, out float aire)
    {
        TerrainData datos = o.Terreno.terrainData;
        int res = datos.holesResolution;
        Vector3 origen = o.Terreno.transform.position, medida = datos.size;
        float largo = Vector2.Distance(a, b);
        Vector2 dir = (b - a) / largo;
        bool[,] solido = datos.GetHoles(0, 0, res, res);
        float celdaX = medida.x / res, celdaZ = medida.z / res;
        int abiertas = 0;
        float margen = SemianchoDelHueco + Mathf.Max(celdaX, celdaZ);
        int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - margen - origen.x) / celdaX));
        int i1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + margen - origen.x) / celdaX));
        int k0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - margen - origen.z) / celdaZ));
        int k1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + margen - origen.z) / celdaZ));
        for (int k = k0; k <= k1; k++)
            for (int i = i0; i <= i1; i++)
            {
                var p = new Vector2(origen.x + (i + 0.5f) * celdaX, origen.z + (k + 0.5f) * celdaZ);
                float s = Vector2.Dot(p - a, dir);
                float lado = Mathf.Abs((p.x - a.x) * dir.y - (p.y - a.y) * dir.x);
                if (s < 0f || s > largo || lado > SemianchoDelHueco) continue;
                float h = o.Suelo.Altura(p.x, p.y), f = suelo(s);
                if (h < f - 1f || h > f + AltoDelHueco || !solido[k, i]) continue;
                solido[k, i] = false;
                abiertas++;
            }
        if (abiertas > 0) datos.SetHoles(0, 0, solido);
        aire = 0f;
        for (float s = 0f; s <= largo; s += 1f)
        {
            Vector2 p = a + dir * s;
            if (o.Suelo.Altura(p.x, p.y) < suelo(s) - 1f) aire += 1f;
        }
        return abiertas;
    }

    private const string GrupoDeRocasDelMundo = "Bosque Prohibido y arboledas";
    private const string MotivoRocaEnElTunel = "peña del mundo sobre la traza de un túnel: el túnel trae sus propias peñas";

    /// Retira (con el registro) las peñas sueltas del mundo que caen sobre la traza a→b.
    private static void DespejarLaTraza(Obra o, Vector2 a, Vector2 b, float semiancho)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform rocas = BuscarGrupo(escena, GrupoDeRocasDelMundo);
        if (rocas == null) return;
        float largo = Vector2.Distance(a, b);
        Vector2 dir = (b - a) / largo;
        var fuera = new List<Transform>();
        foreach (Transform t in rocas)
        {
            if (!t.gameObject.activeSelf || !t.name.StartsWith("Rock")) continue;
            var p = new Vector2(t.position.x, t.position.z);
            float s = Vector2.Dot(p - a, dir), lado = Mathf.Abs((p.x - a.x) * dir.y - (p.y - a.y) * dir.x);
            if (s > -semiancho && s < largo + semiancho && lado < semiancho) fuera.Add(t);
        }
        foreach (Transform t in fuera) Retirar(escena, t, MotivoRocaEnElTunel, o);
        if (fuera.Count > 0) Physics.SyncTransforms();
    }

    private const string MotivoTapaUnTemplo = "árbol o peña del mundo delante de la entrada de un templo";

    /// Retira (con el registro) los árboles y peñas sueltas del mundo a menos de «radio» de «centro».
    private static void DespejarElSitio(Obra o, Vector2 centro, float radio)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform mundo = BuscarGrupo(escena, GrupoDeRocasDelMundo);
        if (mundo == null) return;
        var fuera = new List<Transform>();
        foreach (Transform t in mundo)
            if (t.gameObject.activeSelf && (t.name.StartsWith("Rock") || t.name.StartsWith("Tree")) &&
                Vector2.Distance(new Vector2(t.position.x, t.position.z), centro) < radio) fuera.Add(t);
        foreach (Transform t in fuera) Retirar(escena, t, MotivoTapaUnTemplo, o);
        if (fuera.Count > 0) Physics.SyncTransforms();
    }

    // ── Cueva de la cascada ──────────────────────────────────────────────────────────────────

    /// Boca de la cueva al pie del cortado, junto al velo de la cascada, y hacia dónde entra.
    private static readonly Vector2 BocaDeLaCascada = new Vector2(183f, 271f), HaciaDentroDeLaCascada = new Vector2(0f, 1f);
    /// De la repisa del mirador de la cascada (cara de arriba) al borde de la repisa alta del oeste.
    private static readonly Vector3 PieDeLaSubida = new Vector3(168f, 52.45f, 263.5f), FinDeLaSubida = new Vector3(162.5f, 60f, 267.5f);
    private const int TramosDeEscalera = 3;

    /// La cueva sube por dentro del cortado, de la poza a la repisa alta de la cascada (a la que no se llega
    /// andando): la boca queda tras el agua que cae, y dentro hay tres tramos de escalera tallada que salen a ras de
    /// la repisa, junto al templo. El mismo sitio se alcanza saltando: del mirador a la repisa del oeste y, por el
    /// puente colgante, a la del templo. Las dos rutas cierran un bucle.
    private static void CuevaDeLaCascada(Obra o, Transform grupo)
    {
        Vector2 a = BocaDeLaCascada, dir = HaciaDentroDeLaCascada.normalized;
        float largo = LargoDeBoca + TramosDeEscalera * LargoDeTunel;
        // La escalera sube una altura fija: se retrasa la traza hasta que la boca queda a ras del pie del cortado.
        Vector2 b = a + dir * largo;
        float arriba = o.Suelo.Altura(b.x, b.y), abajo = arriba - TramosDeEscalera * SubeLaEscalera;
        for (int i = 0; i < 24 && o.Suelo.Altura(a.x, a.y) > abajo + 0.6f; i++)
        {
            a -= dir * 0.5f;
            b = a + dir * largo;
            arriba = o.Suelo.Altura(b.x, b.y);
            abajo = arriba - TramosDeEscalera * SubeLaEscalera;
        }
        float SueloDelTunel(float s) => s <= LargoDeBoca ? abajo : abajo + (s - LargoDeBoca) * SubeLaEscalera / LargoDeTunel;

        DespejarLaTraza(o, a, b, 5.5f);
        int abiertas = AbrirElTerreno(o, a, b, SueloDelTunel, out float aire);
        float rumbo = Rumbo(dir);
        Vector2 c = a + dir * (LargoDeBoca * 0.5f);
        PonExacto(o, grupo, BocaDeLaCueva, "Boca de la cueva de la cascada", new Vector3(c.x, abajo, c.y), rumbo);
        for (int i = 0; i < TramosDeEscalera; i++)
        {
            c = a + dir * (LargoDeBoca + LargoDeTunel * (i + 0.5f));
            PonExacto(o, grupo, TunelConEscalera, "Escalera de la cueva", new Vector3(c.x, abajo + SubeLaEscalera * i, c.y), rumbo);
        }

        SubidaDelMirador(o, grupo);
        // Puente sobre el arroyo, de la repisa del mirador a la del templo.
        PonExacto(o, grupo, PrefabsDeZonas + "PuenteColgante_Largo.prefab", "Puente de la cascada", new Vector3(173.5f, 60f, 286f), 90f);
        TemploEn(o, grupo, "Templo de la Cascada", new Vector2(192f, 303f), Vector2.down);

        float pie = o.Suelo.Altura(a.x, a.y);
        o.Informe.Add($"  Cueva de la cascada: boca en ({a.x:0.0}, {a.y:0.0}) con el suelo a {abajo:0.0} m (el terreno ahí está a {pie:0.0} m), " +
                      $"sale a la repisa alta en ({b.x:0.0}, {b.y:0.0}) a {arriba:0.0} m; {abiertas} celdas de terreno abiertas" +
                      (aire > 0f ? $"; OJO: {aire:0} m de túnel quedan al aire" : "") + ".");
    }

    /// Repisas en zigzag del mirador a la repisa alta: cada una sube lo mismo y queda a un lado de la anterior,
    /// para que ninguna tape a la de abajo.
    private static void SubidaDelMirador(Obra o, Transform grupo)
    {
        const int Saltos = 8;
        Vector3 tramo = FinDeLaSubida - PieDeLaSubida;
        Vector3 alLado = new Vector3(-tramo.z, 0f, tramo.x).normalized;
        for (int i = 1; i < Saltos; i++)
        {
            Vector3 p = PieDeLaSubida + tramo * (i / (float)Saltos) + alLado * (i % 2 == 0 ? 2f : -2f);
            PonExacto(o, grupo, PrefabsDeZonas + Repisas[1] + ".prefab", "Repisa de la subida del mirador", p, i * 70f);
        }
    }

    // ── Túnel llano de lado a lado de una loma ───────────────────────────────────────────────

    /// Túnel llano de a a b (dos bocas, tramos rectos y, con «conSala», una sala en medio). La distancia entre a y b
    /// debe ser 10 + 8·n (o 22,6 + 8·n con sala): si no cuadra, se avisa y no se pone.
    private static void TunelPorElTerreno(Obra o, Transform grupo, string nombre, Vector2 a, Vector2 b, bool conSala)
    {
        float largo = Vector2.Distance(a, b);
        float resto = largo - 2f * LargoDeBoca - (conSala ? LargoDeSala : 0f);
        int tramos = Mathf.RoundToInt(resto / LargoDeTunel);
        if (tramos < 0 || Mathf.Abs(resto - tramos * LargoDeTunel) > 0.3f || (conSala && tramos % 2 != 0))
        {
            o.Informe.Add($"  · {nombre}: {largo:0.0} m no cuadran con las piezas de túnel; no se pone.");
            return;
        }
        Vector2 dir = (b - a) / largo;
        float cota = Mathf.Min(o.Suelo.Altura(a.x, a.y), o.Suelo.Altura(b.x, b.y));
        DespejarLaTraza(o, a, b, 5.5f);
        int abiertas = AbrirElTerreno(o, a, b, _ => cota, out float aire);
        float rumbo = Rumbo(dir), s = 0f;
        Vector3 En(float d) { Vector2 p = a + dir * d; return new Vector3(p.x, cota, p.y); }
        PonExacto(o, grupo, BocaDeLaCueva, nombre + " — boca", En(LargoDeBoca * 0.5f), rumbo);
        s = LargoDeBoca;
        for (int i = 0; i < tramos; i++)
        {
            if (conSala && i == tramos / 2)
            {
                PonExacto(o, grupo, SalaDeLaCueva, nombre + " — sala", En(s + LargoDeSala * 0.5f), rumbo);
                s += LargoDeSala;
            }
            PonExacto(o, grupo, TunelRecto, nombre + " — túnel", En(s + LargoDeTunel * 0.5f), rumbo);
            s += LargoDeTunel;
        }
        if (conSala && tramos == 0) { PonExacto(o, grupo, SalaDeLaCueva, nombre + " — sala", En(s + LargoDeSala * 0.5f), rumbo); s += LargoDeSala; }
        PonExacto(o, grupo, BocaDeLaCueva, nombre + " — boca", En(s + LargoDeBoca * 0.5f), rumbo + 180f);
        // La hiedra tapa la boca de llegada: el pasillo no se anuncia.
        PonExacto(o, grupo, HiedraQueTapa, nombre + " — hiedra", En(-0.6f), rumbo, sinObstaculo: true);
        o.Informe.Add($"  {nombre}: túnel de {largo:0.0} m a {cota:0.0} m de ({a.x:0}, {a.y:0}) a ({b.x:0}, {b.y:0}); {abiertas} celdas de terreno abiertas" +
                      (aire > 0f ? $"; OJO: {aire:0} m quedan al aire" : "") + ".");
    }

    /// Dirección unitaria hacia donde sube el terreno en p (cero si es llano).
    private static Vector2 CuestaArriba(Obra o, Vector2 p, float paso = 3f)
    {
        var g = new Vector2(o.Suelo.Altura(p.x + paso, p.y) - o.Suelo.Altura(p.x - paso, p.y),
                            o.Suelo.Altura(p.x, p.y + paso) - o.Suelo.Altura(p.x, p.y - paso));
        return g.sqrMagnitude < 1e-4f ? Vector2.zero : g.normalized;
    }

    // ── Templos de las pruebas ───────────────────────────────────────────────────────────────

    /// Templos: nombre, centro de la zona donde se busca su ladera y radio de búsqueda. A cada uno se llega de una
    /// manera: nadando (ribera sur del río), volando (ladera de la cumbre) y a pie (bosque del oeste). El de la
    /// cascada va a sitio fijo, con su cueva.
    private static readonly (string nombre, Vector2 centro, float radio)[] TemplosDeLasPruebas =
    {
        ("Templo de la Ribera", new Vector2(36f, -320f), 14f),
        ("Templo de la Cumbre", new Vector2(60f, 390f), 30f),
        ("Templo del Bosque", new Vector2(-350f, 80f), 40f),
    };

    /// Busca cerca de «centro» una ladera con rellano delante y mete ahí la entrada, con la fachada cuesta abajo.
    private static void Templo(Obra o, Transform grupo, string nombre, Vector2 centro, float radio)
    {
        float mejor = float.MinValue;
        Vector2 sitio = centro, frente = Vector2.down;
        for (float dx = -radio; dx <= radio; dx += 2f)
            for (float dz = -radio; dz <= radio; dz += 2f)
            {
                var p = centro + new Vector2(dx, dz);
                if (dx * dx + dz * dz > radio * radio || EnCorredor(o, p, 4f)) continue;
                float h = o.Suelo.Altura(p.x, p.y);
                if (h < NivelDelMar + 1.5f) continue;
                Vector2 cuesta = CuestaArriba(o, p, 4f);
                if (cuesta == Vector2.zero) continue;
                float sube = o.Suelo.Altura(p.x + cuesta.x * 6f, p.y + cuesta.y * 6f) - h;
                float llano = 0f;
                for (float d = 2f; d <= 7f; d += 2.5f)
                    foreach (float lado in new[] { -3.5f, 0f, 3.5f })
                    {
                        Vector2 q = p - cuesta * d + new Vector2(-cuesta.y, cuesta.x) * lado;
                        llano = Mathf.Max(llano, Mathf.Abs(o.Suelo.Altura(q.x, q.y) - h));
                    }
                if (sube < 3.4f || llano > 1.5f) continue;
                float nota = sube - 3f * llano - 0.05f * (dx * dx + dz * dz) / Mathf.Max(radio, 1f);
                if (nota > mejor) { mejor = nota; sitio = p; frente = -cuesta; }
            }
        if (mejor == float.MinValue)
        {
            o.Informe.Add($"  · {nombre}: no hay ladera con rellano a {radio:0} m de ({centro.x:0}, {centro.y:0}); no se pone.");
            return;
        }
        TemploEn(o, grupo, nombre, sitio, frente);
    }

    /// Mete la entrada de un templo en «sitio» con la fachada (−Z local) hacia «frente», a la cota del suelo de delante.
    private static void TemploEn(Obra o, Transform grupo, string nombre, Vector2 sitio, Vector2 frente)
    {
        float cota = o.Suelo.Altura(sitio.x + frente.x * 3f, sitio.y + frente.y * 3f);
        // Fuera los árboles y peñas sueltas del mundo que taparían la fachada.
        DespejarElSitio(o, sitio + frente * 2f, 7.5f);
        PonExacto(o, grupo, EntradaDelTemplo, nombre, new Vector3(sitio.x, cota, sitio.y), Rumbo(-frente));
        o.Informe.Add($"  {nombre}: entrada en ({sitio.x:0.0}, {sitio.y:0.0}) a {cota:0.0} m, mirando a ({frente.x:0.00}, {frente.y:0.00}).");
    }

    // ── Mirador de la cumbre ─────────────────────────────────────────────────────────────────

    /// Punto más alto de la isla: se busca alrededor de la cima conocida por si el terreno ha cambiado.
    private static readonly Vector2 CimaDeLaIsla = new Vector2(76f, 434f);

    private static void MiradorEnLaCumbre(Obra o, Transform grupo)
    {
        Vector2 cima = CimaDeLaIsla;
        float alto = o.Suelo.Altura(cima.x, cima.y);
        for (float dx = -20f; dx <= 20f; dx += 1f)
            for (float dz = -20f; dz <= 20f; dz += 1f)
            {
                float h = o.Suelo.Altura(CimaDeLaIsla.x + dx, CimaDeLaIsla.y + dz);
                if (h > alto) { alto = h; cima = CimaDeLaIsla + new Vector2(dx, dz); }
            }
        // El firme del mirador queda 1 m por debajo de la punta: el cuerpo de roca la envuelve.
        PonExacto(o, grupo, MiradorDeLaCumbre, "Mirador de la cumbre", new Vector3(cima.x, alto - 1f, cima.y), 200f);
        o.Informe.Add($"  Mirador de la cumbre: en ({cima.x:0}, {cima.y:0}) a {alto - 1f:0.0} m. Solo se llega volando.");
    }

    // ── Arcos de entrada a los pueblos ───────────────────────────────────────────────────────

    /// Pone un arco donde cada camino cruza el círculo de «radio» alrededor del pueblo: a cada pueblo se entra por
    /// una puerta. Los cruces de más de 9 m (plazas, explanadas) se dejan sin arco.
    private static int ArcosDeEntrada(Obra o, Transform grupo, string prefab, string nombre, Vector2 centro, float radio)
    {
        const int Pasos = 720;
        var cruces = new List<string>();
        var enCamino = new bool[Pasos];
        for (int i = 0; i < Pasos; i++)
        {
            float a = i * Mathf.PI * 2f / Pasos;
            Vector2 p = centro + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radio;
            enCamino[i] = o.Suelo.Altura(p.x, p.y) > NivelDelMar + 1f && PesoCamino(o, p.x, p.y) > 0.5f;
        }
        int inicio = 0;
        while (inicio < Pasos && enCamino[inicio]) inicio++;
        if (inicio == Pasos) return 0;
        int puestos = 0;
        for (int n = 0; n < Pasos; n++)
        {
            int i = (inicio + n) % Pasos;
            if (!enCamino[i]) continue;
            int largo = 0;
            while (largo < Pasos && enCamino[(i + largo) % Pasos]) largo++;
            float arco = largo * Mathf.PI * 2f * radio / Pasos;
            cruces.Add($"{arco:0.0}");
            if (arco >= 1.5f && arco <= 8.5f)
            {
                float a = (i + largo * 0.5f) * Mathf.PI * 2f / Pasos;
                var radial = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                if (Poner(o, grupo, new Pieza
                    {
                        Prefab = prefab, Nombre = nombre, Pos = centro + radial * radio, Rumbo = Rumbo(radial), IgnorarZonas = true,
                        PermitirCamino = true, PermitirSolapePropio = true, DesnivelMax = 2.6f, Hundir = 0.5f, Holgura = -0.2f,
                    }) != null) puestos++;
            }
            n += largo;
        }
        o.Informe.Add($"  {nombre}: {puestos} arcos en los caminos que entran a {radio:0} m de ({centro.x:0}, {centro.y:0}); cruces de camino (m): {string.Join(", ", cruces)}.");
        return puestos;
    }

    // ── Pueblo inicial: casas de campo propias ───────────────────────────────────────────────

    /// Caja, en el espacio local de la casa, de sus módulos de obra (los hijos «Building…»), sin los enseres.
    private static bool CajaDeLaCasa(Transform casa, out Bounds caja)
    {
        caja = default;
        bool hay = false;
        foreach (Transform hijo in casa)
        {
            if (!hijo.name.StartsWith("Building")) continue;
            foreach (MeshFilter mf in hijo.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 esquina = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = casa.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                    if (!hay) { caja = new Bounds(p, Vector3.zero); hay = true; }
                    else caja.Encapsulate(p);
                }
            }
        }
        return hay;
    }

    /// Margen (m) alrededor de la planta de una casa del pack dentro del que un punto de juego la deja sin cambiar.
    private const float MargenDePuntosDeJuego = 3.5f;

    /// Dónde pasa algo de juego en la escena: los puntos de aparición de NPCs y lo que cuelga de la raíz
    /// «GAMEPLAY» (anclas de puertas, eventos, oficios). Las zonas grandes (colisionador de más de 8 m) no cuentan:
    /// cubren el pueblo entero y no son un sitio concreto.
    private static List<(Vector3 pos, string nombre)> PuntosDeJuego(Scene escena)
    {
        var r = new List<(Vector3, string)>();
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (NpcSpawnPoint p in raiz.GetComponentsInChildren<NpcSpawnPoint>(true))
                r.Add((p.transform.position, p.name));
            if (raiz.name != "GAMEPLAY") continue;
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t == raiz.transform || t.childCount > 0) continue;
                Collider c = t.GetComponent<Collider>();
                if (c != null && c.bounds.extents.magnitude > 8f) continue;
                r.Add((t.position, t.name));
            }
        }
        return r;
    }

    /// Nombre del primer punto de juego que cae en la planta de la casa (con margen), o null si no hay ninguno.
    /// Una casa con juego (la de Will con su puerta, el puesto de Tomasa, los puestos de los oficios) no se
    /// cambia: su puerta, su mostrador y quien la atiende dependen del modelo del pack.
    private static string PuntoDeJuegoEnLaCasa(Transform casa, Bounds local, List<(Vector3 pos, string nombre)> puntos)
    {
        float escala = Mathf.Max(0.01f, casa.lossyScale.x);
        float margen = MargenDePuntosDeJuego / escala;
        foreach (var (pos, nombre) in puntos)
        {
            Vector3 p = casa.InverseTransformPoint(pos);
            if (p.x >= local.min.x - margen && p.x <= local.max.x + margen &&
                p.z >= local.min.z - margen && p.z <= local.max.z + margen)
                return nombre;
        }
        return null;
    }

    /// Casas de campo puestas en la última ejecución (con su rumbo y planta): las usan el árbol del columpio y el
    /// huerto para no pisarlas.
    private static readonly List<(Transform casa, float rumbo, Vector2 planta)> CasasDeCampoPuestas = new();

    private const string MotivoCasaDelPackEnElPueblo = "casa del pack en el pueblo inicial: la sustituye una casa de campo de entramado y paja";
    private const string MotivoPiezaSueltaEnCasaNueva = "pieza suelta del pack que se montaba sobre una casa ya cambiada o le tapaba la puerta (molino, chimenea, valla, árbol…)";

    /// Planta de los muros (ancho de fachada × fondo, m) de cada casa de campo, como sale de zonas.py
    /// (VARIANTES_DEL_PUEBLO). Los muros van centrados en el origen de la pieza.
    private static readonly Dictionary<string, Vector2> PlantaDeCasaDelPueblo = new()
    {
        { "A", new Vector2(6.0f, 5.0f) }, { "B", new Vector2(5.2f, 4.6f) }, { "C", new Vector2(7.2f, 5.4f) },
        { "D", new Vector2(5.6f, 6.0f) }, { "E", new Vector2(4.6f, 4.2f) }, { "F", new Vector2(8.4f, 6.0f) },
    };

    /// Piezas sueltas del pueblo que son parte de una casa (no enseres): si caen sobre una casa nueva se retiran.
    private static readonly string[] PiezasDeCasa = { "Building", "Chimney", "Windmill", "Window", "Door", "Roof", "Stairs", "Ladder", "Wall", "Vane", "Smoke" };

    /// Cambia las casas del pack del pueblo inicial por casas de campo propias, en su sitio y mirando a donde
    /// miraban; el modelo se elige por el ancho de fachada de la casa que sustituye.
    /// · Puestos (sin puerta y con algo de juego al lado: Tomasa, los oficios): se quedan, no son casas.
    /// · Casas con juego en la puerta (la de Will): la casa nueva se coloca con su puerta donde estaba la vieja y
    ///   mirando hacia el mismo lado, para que anclas, teletransportes y escenas sigan en su sitio.
    /// · Si aun así un punto de juego queda dentro de los muros nuevos, se queda la del pack.
    /// · El molino (torre con aspas) se queda: es el hito del pueblo.
    /// · Lo suelto del pack que era parte de la casa (chimeneas…) y cae sobre la nueva se retira.
    /// Ver INC-1074.
    private static void CasasDelPuebloInicial(Obra o)
    {
        Scene escena = o.Raiz.gameObject.scene;
        Transform pueblo = BuscarGrupo(escena, GrupoDelPuebloInicial);
        if (pueblo == null) return;
        HashSet<string> giradas = CasasGiradas(escena);
        Transform casas = Grupo(Grupo(o.Raiz, "Pueblo inicial"), "Casas de campo");
        var viejas = new List<(Transform casa, string prefab)>();
        var sueltas = new List<Transform>();
        // Aquí cada casa es un contenedor «BuildingATnn» con los módulos de la casa («Building…») y, colgando, los
        // enseres de su patio. Lo demás del grupo son piezas sueltas.
        foreach (Transform hijo in pueblo)
        {
            if (!hijo.gameObject.activeSelf) continue;
            if (hijo.name.StartsWith("BuildingAT")) viejas.Add((hijo, hijo.name.Split(' ')[0]));
            else sueltas.Add(hijo);
        }
        List<(Vector3 pos, string nombre)> puntos = PuntosDeJuego(escena);
        int n = 0, puestas = 0, retiradasSueltas = 0;
        CasasDeCampoPuestas.Clear();
        var puestasEnElPueblo = CasasDeCampoPuestas;
        foreach (var (casa, prefab) in viejas)
        {
            if (!CajaDeLaCasa(casa, out Bounds local)) continue;
            // El molino (la torre del pack con las aspas sueltas encima) es el hito del pueblo: se queda.
            var aspas = new List<(Vector3 pos, string nombre)>();
            foreach (Transform t in sueltas)
                if (t.name.StartsWith("Windmill")) aspas.Add((t.position, t.name));
            if (PuntoDeJuegoEnLaCasa(casa, local, aspas) != null)
            {
                o.Informe.Add($"    {casa.name}: es el molino, se queda con sus aspas");
                continue;
            }
            Transform puertaVieja = PuertaDeLaCasa(casa);
            string usada = PuntoDeJuegoEnLaCasa(casa, local, puntos);
            if (usada != null && puertaVieja == null)
            {
                o.Informe.Add($"    {casa.name}: es un puesto, se queda el del pack (juego: {usada})");
                continue;
            }

            Vector3 centro = casa.TransformPoint(local.center);
            Vector3 f3 = puertaVieja != null && usada != null ? FrenteDeLaPuerta(casa, local, puertaVieja) : FrenteDeCasa(casa, prefab, giradas);
            Vector2 frente = new Vector2(f3.x, f3.z).normalized;
            // La fachada es el +Z local o, en las giradas, otro lado: el ancho es la medida de la caja que cruza al frente.
            Vector3 frenteLocal = casa.InverseTransformDirection(f3);
            float ancho = (Mathf.Abs(frenteLocal.z) >= Mathf.Abs(frenteLocal.x) ? local.size.x : local.size.z) * casa.lossyScale.x;
            string variante = ancho >= 9.5f ? "F" : ancho >= 7.6f ? "C" : ancho >= 6.4f ? (n % 2 == 0 ? "A" : "D") : ancho >= 5.4f ? (n % 2 == 0 ? "B" : "D") : "E";

            // Va donde estaba la que sustituye: el sitio ya era de una casa. Si con la puerta en su sitio el tejado
            // pisa el de otra casa nueva, se prueba con una más estrecha.
            float rumbo = Rumbo(-frente);
            bool alinear = puertaVieja != null && usada != null;
            GameObject nueva = null;
            string comoVa = null, tapado = null;
            Vector2 planta = default;
            foreach (string v in VariantesDesde(variante))
            {
                nueva = PonerCasaDeCampo(o, casas, casa.name, v, centro, rumbo, frente, alinear ? puertaVieja : null, out comoVa);
                if (nueva == null) break;
                planta = PlantaDeCasaDelPueblo[v];
                tapado = PuntoDentroDeLosMuros(nueva.transform, rumbo, planta, puntos);
                if (tapado == null && !PisaOtraCasa(nueva.transform, rumbo, planta, puestasEnElPueblo))
                {
                    variante = v;
                    break;
                }
                Object.DestroyImmediate(nueva);
                o.Ocupado.RemoveAt(o.Ocupado.Count - 1);
                o.Puestas--;
                nueva = null;
            }
            if (nueva == null)
            {
                o.Informe.Add($"    {casa.name}: se queda la del pack ({(tapado != null ? $"la nueva taparía «{tapado}»" : "ninguna casa de campo cabe sin pisar a otra")})");
                continue;
            }
            puestasEnElPueblo.Add((nueva.transform, rumbo, planta));

            o.Informe.Add($"    {casa.name}: {ancho:0.0} m de fachada → CasaDelPueblo_{variante}, {comoVa}");
            n++;
            puestas++;
            Retirar(escena, casa, MotivoCasaDelPackEnElPueblo, o);
            bool hayPuerta = PuertaDelModelo(nueva, out Vector3 puertaPuesta);
            for (int i = sueltas.Count - 1; i >= 0; i--)
            {
                if (!SueltaSobreLaCasa(sueltas[i], nueva.transform, rumbo, planta, hayPuerta, puertaPuesta)) continue;
                Retirar(escena, sueltas[i], MotivoPiezaSueltaEnCasaNueva, o);
                sueltas.RemoveAt(i);
                retiradasSueltas++;
            }
        }
        Physics.SyncTransforms();
        o.Informe.Add($"  Pueblo inicial: {puestas} casas del pack cambiadas por casas de campo; {retiradasSueltas} piezas sueltas retiradas de encima.");
    }

    /// La variante pedida y, detrás, las más estrechas que ella, de más ancha a más estrecha.
    private static IEnumerable<string> VariantesDesde(string variante)
    {
        yield return variante;
        float ancho = PlantaDeCasaDelPueblo[variante].x;
        foreach (KeyValuePair<string, Vector2> v in PlantaDeCasaDelPueblo.OrderByDescending(v => v.Value.x))
            if (v.Value.x < ancho) yield return v.Key;
    }

    /// Pone una casa de campo en el sitio de una del pack: en su centro o, si se da la puerta vieja, con su puerta
    /// en ella; apoyada en el suelo más bajo de delante, el centro y detrás.
    private static GameObject PonerCasaDeCampo(Obra o, Transform grupo, string nombreViejo, string variante, Vector3 centro, float rumbo,
        Vector2 frente, Transform puertaVieja, out string comoVa)
    {
        comoVa = "en el sitio de la vieja";
        GameObject nueva = PonExacto(o, grupo, PrefabsDeZonas + "CasaDelPueblo_" + variante + ".prefab", nombreViejo + " (casa de campo)",
            new Vector3(centro.x, 0f, centro.z), rumbo);
        if (nueva == null) return null;
        if (puertaVieja != null && PuertaDelModelo(nueva, out Vector3 puertaNueva))
        {
            Vector3 mover = puertaVieja.position - puertaNueva;
            nueva.transform.position += new Vector3(mover.x, 0f, mover.z);
            comoVa = "con la puerta donde estaba la vieja";
        }
        // El zócalo baja 0,6 m por debajo del origen y absorbe el desnivel.
        Vector3 p = nueva.transform.position;
        float suelo = Mathf.Min(o.Suelo.Altura(p.x, p.z), Mathf.Min(o.Suelo.Altura(p.x + frente.x * 2.5f, p.z + frente.y * 2.5f), o.Suelo.Altura(p.x - frente.x * 2.5f, p.z - frente.y * 2.5f)));
        nueva.transform.position = new Vector3(p.x, suelo + 0.3f, p.z);
        o.Ocupado[o.Ocupado.Count - 1] = HuellaOrientada(nueva, rumbo);
        return nueva;
    }

    /// ¿El tejado (muros + 0,6 m de alero) pisa el de otra casa de campo ya puesta? Comprobación de ejes separados
    /// entre los dos rectángulos girados.
    private static bool PisaOtraCasa(Transform casa, float rumbo, Vector2 planta, List<(Transform casa, float rumbo, Vector2 planta)> otras)
    {
        Vector2[] a = RectanguloDelTejado(casa.position, rumbo, planta);
        foreach (var (otra, r, p) in otras)
            if (SeTocan(a, RectanguloDelTejado(otra.position, r, p))) return true;
        return false;
    }

    private static Vector2[] RectanguloDelTejado(Vector3 centro, float rumbo, Vector2 planta)
    {
        float a = rumbo * Mathf.Deg2Rad, mx = planta.x * 0.5f + 0.6f, mz = planta.y * 0.5f + 0.6f;
        var z = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        var x = new Vector2(z.y, -z.x);
        var c = new Vector2(centro.x, centro.z);
        return new[] { c - x * mx - z * mz, c + x * mx - z * mz, c + x * mx + z * mz, c - x * mx + z * mz };
    }

    private static bool SeTocan(Vector2[] a, Vector2[] b)
    {
        foreach (Vector2[] poligono in new[] { a, b })
            for (int i = 0; i < 4; i++)
            {
                Vector2 lado = poligono[(i + 1) % 4] - poligono[i];
                var eje = new Vector2(lado.y, -lado.x);
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (Vector2 v in a) { float d = Vector2.Dot(v, eje); minA = Mathf.Min(minA, d); maxA = Mathf.Max(maxA, d); }
                foreach (Vector2 v in b) { float d = Vector2.Dot(v, eje); minB = Mathf.Min(minB, d); maxB = Mathf.Max(maxB, d); }
                if (maxA < minB || maxB < minA) return false;
            }
        return true;
    }

    /// La puerta del pack que cuelga de la casa («Door…»), o null si no tiene (los puestos).
    private static Transform PuertaDeLaCasa(Transform casa)
    {
        foreach (Transform hijo in casa)
            if (hijo.gameObject.activeSelf && hijo.name.StartsWith("Door")) return hijo;
        return null;
    }

    /// Hacia dónde sale la puerta: el lado de la caja de la casa en el que está.
    private static Vector3 FrenteDeLaPuerta(Transform casa, Bounds local, Transform puerta)
    {
        Vector3 q = casa.InverseTransformPoint(puerta.position) - local.center;
        float ex = Mathf.Max(0.01f, local.extents.x), ez = Mathf.Max(0.01f, local.extents.z);
        Vector3 eje = Mathf.Abs(q.x) / ex >= Mathf.Abs(q.z) / ez ? new Vector3(Mathf.Sign(q.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(q.z));
        Vector3 d = casa.TransformDirection(eje);
        d.y = 0f;
        return d.normalized;
    }

    /// Dónde está la puerta de una casa de campo ya puesta: el centro de lo que lleva el color de carpintería por
    /// debajo de 1,2 m (las contraventanas empiezan más arriba).
    private static bool PuertaDelModelo(GameObject casa, out Vector3 puerta)
    {
        puerta = default;
        float y0 = casa.transform.position.y;
        Vector3 suma = Vector3.zero;
        int cuantos = 0;
        foreach (MeshFilter mf in casa.GetComponentsInChildren<MeshFilter>())
        {
            Mesh malla = mf.sharedMesh;
            var mr = mf.GetComponent<MeshRenderer>();
            if (malla == null || mr == null) continue;
            Material[] mats = mr.sharedMaterials;
            Vector3[] v = malla.vertices;
            for (int s = 0; s < malla.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null || !(mats[s].name.Contains("Contra") || mats[s].name.Contains("Carpinteria"))) continue;
                foreach (int i in malla.GetTriangles(s))
                {
                    Vector3 w = mf.transform.TransformPoint(v[i]);
                    float h = w.y - y0;
                    if (h <= 0.3f || h >= 1.2f) continue;
                    suma += w;
                    cuantos++;
                }
            }
        }
        if (cuantos == 0) return false;
        puerta = suma / cuantos;
        return true;
    }

    /// Nombre del primer punto de juego que queda dentro de los muros de una casa de campo (con 0,3 m de holgura
    /// hacia dentro), o null.
    private static string PuntoDentroDeLosMuros(Transform casa, float rumbo, Vector2 planta, List<(Vector3 pos, string nombre)> puntos)
    {
        Quaternion giro = Quaternion.Inverse(Quaternion.Euler(0f, rumbo, 0f));
        foreach (var (pos, nombre) in puntos)
        {
            Vector3 q = giro * (pos - casa.position);
            if (Mathf.Abs(q.x) < planta.x * 0.5f - 0.3f && Mathf.Abs(q.z) < planta.y * 0.5f - 0.3f) return nombre;
        }
        return null;
    }

    /// ¿La pieza suelta se monta sobre la casa nueva o le tapa la entrada? Las piezas de casa (molino, chimenea…),
    /// si su caja toca los muros; lo demás (vallas, árboles, barriles), si su núcleo (60 % de su caja en planta) entra
    /// en los muros o en el porche, de la puerta a 2,3 m hacia fuera. Lo enorme (el río, el terreno) no cuenta, ni lo que lleva scripts salvo las piezas de casa.
    private static bool SueltaSobreLaCasa(Transform suelta, Transform casa, float rumbo, Vector2 planta, bool hayPuerta, Vector3 puerta)
    {
        bool hay = false;
        Bounds b = default;
        foreach (Renderer r in suelta.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hay || Mathf.Max(b.extents.x, b.extents.z) > 15f) return false;
        bool deCasa = false;
        foreach (string prefijo in PiezasDeCasa)
            if (suelta.name.StartsWith(prefijo)) { deCasa = true; break; }
        // Lo que lleva scripts (tablones, interactuables, luces de noche…) no es decorado suelto: no se toca. Las
        // piezas de casa sí (una chimenea lleva su humo), porque sin su casa no pintan nada.
        if (!deCasa)
            foreach (MonoBehaviour mb in suelta.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) return false;
        Quaternion giro = Quaternion.Inverse(Quaternion.Euler(0f, rumbo, 0f));
        Vector3 q = giro * (b.center - casa.position);
        float holgura = Mathf.Min(b.extents.x, b.extents.z) * (deCasa ? 1f : 0.6f);
        if (deCasa) holgura = Mathf.Max(b.extents.x, b.extents.z) * 0.7f;
        if (Mathf.Abs(q.x) < planta.x * 0.5f + holgura && Mathf.Abs(q.z) < planta.y * 0.5f + holgura) return true;
        if (!hayPuerta) return false;
        // Porche: la fachada mira a −Z local; de la puerta hacia fuera.
        Vector3 d = giro * (puerta - casa.position);
        float fuera = d.z - q.z;
        return Mathf.Abs(q.x - d.x) < 1.3f + holgura && fuera > -holgura && fuera < 2.3f + holgura;
    }
}
