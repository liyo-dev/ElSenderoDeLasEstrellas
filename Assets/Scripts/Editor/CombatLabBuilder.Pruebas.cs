using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;

/// Pruebas prototipo del LAB (lejos al este del kit de puzles): tres salas cerradas, cada una
/// alrededor de una idea y con las piezas del kit, para jugarlas de principio a fin (3–5 min).
/// Ver INC-720.
///
///  · Luz y saltos: cristales de luz y una lanzadera (bola devuelta con la B) que hacen aparecer
///    plataformas temporales; el último tramo pide encender un cristal desde una plataforma que
///    se está apagando.
///  · Viento y levitación: el molinete abre el almacén; un bloque levitado hace de escalón hasta la
///    repisa; otro molinete tiende un puente de plataformas durante unos segundos.
///  · Liam y cambio de personaje: Paso Sombrío a través de una reja; placa que abre el portón para
///    el resto del grupo; placa que sostiene una barrera abierta mientras alguien la pisa (otro
///    personaje se queda encima y se pasa con el otro).
///
/// Las tres son zona sin vuelo, guardan lo resuelto con EstadoPersistente (ids LAB_PRUEBA_…, que
/// reinicia la pestaña Puzles) y acaban en un cofre (Tesoro, región LAB). Al completarlas se enciende su estrella sobre
/// la entrada. Se llega con el viaje rápido (pestaña Zonas).
public static partial class CombatLabBuilder
{
    private const string RaizPruebas = "LAB_PRUEBAS (fuera del NavMesh)";
    private const float LadoPrueba = 36f;
    private const float AltoMuroPrueba = 6f;
    private static readonly Vector3 CentroPruebaLuz = new Vector3(122f, 0f, -30f);
    private static readonly Vector3 CentroPruebaViento = new Vector3(122f, 0f, 12f);
    private static readonly Vector3 CentroPruebaLiam = new Vector3(122f, 0f, 54f);
    /// Destinos del viaje rápido.
    private static readonly Vector3 EntradaPruebaLuz = new Vector3(106f, 0.1f, -42f);
    private static readonly Vector3 EntradaPruebaViento = new Vector3(106f, 0.1f, 8f);
    private static readonly Vector3 EntradaPruebaLiam = new Vector3(106f, 0.1f, 54f);

    private static void CrearPruebas(Transform geometria, Material suelo, Material muro, Material plataforma, Material madera)
    {
        var raiz = new GameObject(RaizPruebas).transform;
        var cristal = GetMaterialEmisivo("Lab_Cristal", new Color(0.75f, 0.85f, 1f));
        var velo = GetMaterialTransparente("Lab_ZonaSinVuelo", new Color(0.60f, 0.35f, 0.95f, 0.18f));
        var estrella = GetMaterial("Lab_EstrellaDePrueba", new Color(1f, 0.85f, 0.35f), "Universal Render Pipeline/Unlit");

        CrearPruebaDeLuz(Grupo("Prueba: luz y saltos", geometria), Grupo("Prueba: luz y saltos", raiz),
                         suelo, muro, plataforma, cristal, velo, estrella);
        CrearPruebaDeViento(Grupo("Prueba: viento y levitación", geometria), Grupo("Prueba: viento y levitación", raiz),
                            suelo, muro, plataforma, madera, cristal, velo, estrella);
        CrearPruebaDeLiam(Grupo("Prueba: Liam y cambio de personaje", geometria), Grupo("Prueba: Liam y cambio de personaje", raiz),
                          suelo, muro, plataforma, madera, velo, estrella);
        CrearEstacionDeTesoros(geometria, suelo, muro);
    }

    // ── Luz y saltos ───────────────────────────────────────────────────────

    private static void CrearPruebaDeLuz(Transform zona, Transform raiz, Material suelo, Material muro, Material plataforma,
                                         Material cristal, Material velo, Material estrella)
    {
        var c = CentroPruebaLuz;
        SalaDePrueba(zona, raiz, c, suelo, muro, velo);
        var fin = EstrellaDePrueba("LUZ", raiz, c, estrella);
        CrearVueltaAlLaboratorio(raiz, new Vector3(110f, 0f, -45f), EntradaPruebaLuz);
        CrearTexto("Prueba: luz y saltos", zona, new Vector3(110f, 4.2f, -42f), 90f);
        CrearTexto("Enciende los cristales con luz o con la bola devuelta", zona, new Vector3(110f, 3.5f, -42f), 90f);

        // Tramo 1: cristal de luz → tres losas hasta el segundo pilar.
        const float zA = -36f, alto = 3.6f;
        Pilar("Escalón 1", zona, new Vector3(106f, 0f, zA), new Vector2(2f, 3f), 1.2f, plataforma);
        Pilar("Escalón 2", zona, new Vector3(108f, 0f, zA), new Vector2(2f, 3f), 2.4f, plataforma);
        Pilar("Pilar 1", zona, new Vector3(111f, 0f, zA), new Vector2(3f, 3f), alto, plataforma);
        Pilar("Pilar 2", zona, new Vector3(128f, 0f, zA), new Vector2(3f, 3f), alto, plataforma);
        var cristal1 = CrearReceptor("Cristal 1", raiz, new Vector3(128f, alto + 1f, zA), 1f, cristal,
                                     new[] { (int)MagicElement.Light }, ModoMantenido, 6f, new Color(0.75f, 0.85f, 1f));
        LosasTemporales(raiz, cristal1, cristal, new Vector3(115.5f, alto - 0.2f, zA), new Vector3(4f, 0f, 0f), 3);
        CrearTexto("1. Luz al cristal de enfrente", zona, new Vector3(114f, alto + 2.6f, zA), 90f);

        // Tramo 2: la lanzadera dispara desde su torre; su bola devuelta enciende el cristal que
        // lleva encima y aparecen losas que suben hasta el pilar 3.
        Pilar("Torre de la lanzadera", zona, new Vector3(134f, 0f, -24f), new Vector2(2f, 2f), 5f, plataforma);
        var lanzadera = CrearLanzadera(raiz, new Vector3(134f, 6.5f, -24f), 210f, GetMaterial("Lab_Derribo", new Color(0.85f, 0.25f, 0.25f)),
                                       cadencia: 3f, alcance: 17f, bocaLocal: new Vector3(0f, 1f, 1.2f));
        Pilar("Pilar 3", zona, new Vector3(128f, 0f, -20.5f), new Vector2(3f, 3f), 5f, plataforma);
        if (lanzadera == null)
            Debug.LogWarning("[CombatLab] Prueba de luz: no encuentro el proyectil del Demonio; el tramo 2 queda sin lanzadera.");
        else
        {
            var cristal2 = CrearReceptor("Cristal de la lanzadera", lanzadera.transform, new Vector3(134f, 7.5f, -24f), 0.9f, cristal,
                                         new int[0], ModoMantenido, 8f, new Color(1f, 0.72f, 0.12f));
            Asignar(cristal2, "soloMagiaDevuelta", true);
            for (int i = 0; i < 3; i++)
                LosaTemporal(raiz, cristal2, cristal, new Vector3(128f, 3.7f + 0.4f * i, -32.75f + 4f * i));
        }
        CrearTexto("2. Devuelve su bola con B", zona, new Vector3(128f, 7.5f, -30f), 0f);

        // Tramo 3: un cristal enciende el camino hasta una losa intermedia; desde ella hay que
        // encender el segundo antes de que se apague el primero.
        var poste3 = new Vector3(120f, 0f, -26f);
        Pilar("Poste del cristal 3", zona, poste3, new Vector2(1f, 1f), 5.6f, plataforma);
        var cristal3 = CrearReceptor("Cristal 3", raiz, poste3 + Vector3.up * 6.3f, 0.9f, cristal,
                                     new[] { (int)MagicElement.Light }, ModoMantenido, 5f, new Color(0.75f, 0.85f, 1f));
        LosasTemporales(raiz, cristal3, cristal, new Vector3(124f, 4.8f, -20.5f), new Vector3(-4f, 0f, 0f), 3);
        var poste4 = new Vector3(112f, 0f, -14.5f);
        Pilar("Poste del cristal 4", zona, poste4, new Vector2(1f, 1f), 5.6f, plataforma);
        var cristal4 = CrearReceptor("Cristal 4", raiz, poste4 + Vector3.up * 6.3f, 0.9f, cristal,
                                     new[] { (int)MagicElement.Light }, ModoMantenido, 6f, new Color(0.75f, 0.85f, 1f));
        LosaTemporal(raiz, cristal4, cristal, new Vector3(112f, 4.8f, -20.5f));
        Pilar("Meta", zona, new Vector3(107.75f, 0f, -20.5f), new Vector2(3f, 3f), 5f, plataforma);
        CrearTexto("3. Dos cristales: no te quedes en medio", zona, new Vector3(122f, 7.6f, -20.5f), -90f);
        FinDePrueba(fin, zona, raiz, new Vector3(107.5f, 5f, -20.5f), 90f, "LUZ");
    }

    // ── Viento y levitación ────────────────────────────────────────────────

    private static void CrearPruebaDeViento(Transform zona, Transform raiz, Material suelo, Material muro, Material plataforma,
                                            Material madera, Material cristal, Material velo, Material estrella)
    {
        var c = CentroPruebaViento;
        SalaDePrueba(zona, raiz, c, suelo, muro, velo);
        var fin = EstrellaDePrueba("VIENTO", raiz, c, estrella);
        CrearVueltaAlLaboratorio(raiz, new Vector3(110f, 0f, 4f), EntradaPruebaViento);
        CrearTexto("Prueba: viento y levitación", zona, new Vector3(110f, 4.2f, 8f), 90f);
        CrearTexto("Viento a los molinetes · Levitación a los bloques", zona, new Vector3(110f, 3.5f, 8f), 90f);

        // 1. El molinete abre el almacén de bloques (se guarda).
        var centroAlmacen = new Vector3(116f, 0f, 22f);
        CuartoAlto("Almacén de bloques", zona, centroAlmacen, muro);
        var bisagra = new GameObject("Puerta del almacén");
        bisagra.transform.SetParent(raiz);
        bisagra.transform.position = centroAlmacen + new Vector3(-4f, 0f, -1.5f);
        CrearCubo("Hoja", bisagra.transform, centroAlmacen + new Vector3(-4f, 2f, 0f), new Vector3(0.3f, 4f, 3f), madera, estatico: false);
        var puerta = bisagra.AddComponent<Door>();
        var molinete1 = CrearMolinete("Molinete del almacén", raiz, new Vector3(108f, 0f, 16f), -90f, ModoPermanente, 0f, madera, cristal);
        UnityEventTools.AddPersistentListener(molinete1.alActivarse, puerta.Open);
        var almacenAbierto = Estado(raiz, "PRUEBA_VIENTO_ALMACEN");
        UnityEventTools.AddPersistentListener(molinete1.alActivarse, almacenAbierto.Marcar);
        UnityEventTools.AddPersistentListener(almacenAbierto.alRestaurar, puerta.Open);
        UnityEventTools.AddPersistentListener(almacenAbierto.alRestaurar, molinete1.RestaurarEncendido);
        CrearTexto("1. Viento al molinete: abre el almacén", zona, new Vector3(108f, 3.4f, 13.5f), 90f);

        BloqueLevitable("Bloque 1", raiz, new Vector3(117.5f, 0f, 20.5f), plataforma);
        BloqueLevitable("Bloque 2", raiz, new Vector3(117.5f, 0f, 23.5f), plataforma);

        // 2. Repisa demasiado alta para el doble salto: un bloque al pie hace de escalón.
        Pilar("Repisa", zona, new Vector3(134f, 0f, 4f), new Vector2(6f, 8f), 5.4f, plataforma);
        CrearTexto("2. Levita un bloque al pie de la repisa y súbete", zona, new Vector3(126f, 3.4f, 4f), 90f);

        // 3. En la repisa, otro molinete tiende un puente de losas durante 8 s hasta la meta.
        // La meta queda más alta que un bloque más el doble salto: solo se llega por el puente.
        var molinete2 = CrearMolinete("Molinete del puente", raiz, new Vector3(136f, 5.4f, 1.5f), -90f, ModoMantenido, 8f, madera, cristal);
        LosasTemporales(raiz, molinete2, cristal, new Vector3(134f, 5.7f, 10.5f), new Vector3(0f, 0.5f, 4f), 3);
        Pilar("Meta", zona, new Vector3(134f, 0f, 22.75f), new Vector2(3f, 3f), 7.4f, plataforma);
        CrearTexto("3. Viento al molinete: puente de 8 s", zona, new Vector3(137.5f, 7.8f, 4f), 90f);
        FinDePrueba(fin, zona, raiz, new Vector3(134f, 7.4f, 23f), 180f, "VIENTO");
    }

    /// Bloque que se levita y, al soltarlo, cae y se queda quieto: se puede pisar (capa Floor).
    /// Kinemático en reposo para que el jugador no lo empuje; sin giro ni empujón al soltarlo.
    private static void BloqueLevitable(string nombre, Transform raiz, Vector3 pie, Material material)
    {
        const float lado = 1.6f;
        var bloque = new GameObject(nombre);
        bloque.transform.SetParent(raiz);
        bloque.transform.position = pie;
        int capa = LayerMask.NameToLayer(CapaSuelo);
        if (capa >= 0) bloque.layer = capa;
        var caja = bloque.AddComponent<BoxCollider>();
        caja.center = new Vector3(0f, lado * 0.5f, 0f);
        caja.size = Vector3.one * lado;
        var cuerpo = bloque.AddComponent<Rigidbody>();
        cuerpo.mass = 20f;
        cuerpo.isKinematic = true;
        cuerpo.constraints = RigidbodyConstraints.FreezeRotation;
        cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        var levitable = bloque.AddComponent<LevitationTarget>();
        Asignar(levitable, "pushForceMultiplier", 0f);
        Asignar(levitable, "wallBounceForce", 1f);
        var visual = CrearCubo("Bloque", bloque.transform, pie + Vector3.up * (lado * 0.5f), Vector3.one * lado, material, estatico: false);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
    }

    // ── Liam y cambio de personaje ─────────────────────────────────────────

    private static void CrearPruebaDeLiam(Transform zona, Transform raiz, Material suelo, Material muro, Material plataforma,
                                          Material madera, Material velo, Material estrella)
    {
        var c = CentroPruebaLiam;
        SalaDePrueba(zona, raiz, c, suelo, muro, velo);
        var fin = EstrellaDePrueba("LIAM", raiz, c, estrella);
        CrearVueltaAlLaboratorio(raiz, new Vector3(110f, 0f, 50f), EntradaPruebaLiam);
        var puzle = GetMaterial("Lab_Puzle", new Color(0.55f, 0.35f, 0.80f));
        var barreraMat = GetMaterialTransparente("Lab_BarreraDeVuelo", new Color(0.95f, 0.75f, 0.30f, 0.35f));
        CrearTexto("Prueba: Liam y cambio de personaje", zona, new Vector3(110f, 4.2f, 54f), 90f);
        CrearTexto("Hace falta Liam y otro personaje (pestaña Grupo)", zona, new Vector3(110f, 3.5f, 54f), 90f);

        // Muro interior (x 122) con un portón al sur (z 39,5–42,5) y una reja al norte (z 58–62).
        const float x = 122f, z0 = 36f, z1 = 72f;
        TramoDeMuroEnZ("Muro interior", zona, x, z0, 39.5f, muro);
        TramoDeMuroEnZ("Muro interior", zona, x, 42.5f, 58f, muro);
        TramoDeMuroEnZ("Muro interior", zona, x, 62f, z1, muro);
        CrearCubo("Dintel del portón", zona, new Vector3(x, 4.5f, 41f), new Vector3(0.5f, 3f, 3f), muro);

        // 1. Reja: solo se cruza con Paso Sombrío (6 m). Al otro lado, una placa abre el portón.
        Reja("Reja", zona, new Vector3(x, 0f, 60f), 4f, madera);
        CrearTexto("1. Liam: Paso Sombrío a través de la reja", zona, new Vector3(121.5f, 4.6f, 60f), 90f);
        var bisagra = new GameObject("Portón");
        bisagra.transform.SetParent(raiz);
        bisagra.transform.position = new Vector3(x, 0f, 39.5f);
        CrearCubo("Hoja", bisagra.transform, new Vector3(x, 1.5f, 41f), new Vector3(0.3f, 3f, 3f), madera, estatico: false);
        var porton = bisagra.AddComponent<Door>();
        var placaPorton = CrearPlaca("Placa del portón", raiz, new Vector3(127f, 0f, 60f), puzle, fija: true);
        UnityEventTools.AddPersistentListener(placaPorton.onActivated, porton.Open);
        var portonAbierto = Estado(raiz, "PRUEBA_LIAM_PORTON");
        UnityEventTools.AddPersistentListener(placaPorton.onActivated, portonAbierto.Marcar);
        UnityEventTools.AddPersistentListener(portonAbierto.alRestaurar, porton.Open);
        UnityEventTools.AddPersistentListener(portonAbierto.alRestaurar, placaPorton.ForceActivate);
        CrearTexto("Placa: abre el portón al grupo", zona, new Vector3(129.5f, 2.6f, 60f), 90f);

        // 2. Cámara del cofre (esquina sureste) cerrada por una barrera que solo se abre mientras
        //    alguien pisa la placa de fuera: uno se queda encima y se entra con el otro.
        TramoDeMuroEnZ("Muro de la cámara", zona, 130f, z0, 40.5f, muro);
        TramoDeMuroEnZ("Muro de la cámara", zona, 130f, 43.5f, 48f, muro);
        CrearCubo("Muro norte de la cámara", zona, new Vector3(135f, AltoMuroPrueba * 0.5f, 48f), new Vector3(10f, AltoMuroPrueba, 0.5f), muro);
        CrearCubo("Dintel de la barrera", zona, new Vector3(130f, 4.5f, 42f), new Vector3(0.5f, 3f, 3f), muro);
        var barrera = CrearCubo("Barrera de la cámara", raiz, new Vector3(130f, 1.5f, 42f), new Vector3(0.3f, 3f, 3f), barreraMat, estatico: false);
        barrera.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var temporal = barrera.AddComponent<PlataformaTemporal>();
        Asignar(temporal, "visibleAlEmpezar", true);
        AsignarTexto(temporal, "sfxAlAparecer", "");
        var placaEspera = CrearPlaca("Placa de espera", raiz, new Vector3(125.5f, 0f, 45f), puzle, fija: false);
        UnityEventTools.AddPersistentListener(placaEspera.onActivated, temporal.OcultarYa);
        UnityEventTools.AddPersistentListener(placaEspera.onDeactivated, temporal.Mostrar);
        CrearTexto("2. Deja a un compañero en la placa (cruceta ↓: libre)", zona, new Vector3(125.5f, 3.6f, 47.5f), 180f);
        CrearTexto("y entra con el otro (cruceta ← →)", zona, new Vector3(125.5f, 2.9f, 47.5f), 180f);
        FinDePrueba(fin, zona, raiz, new Vector3(136.5f, 0f, 42f), -90f, "LIAM");
    }

    /// Reja de barrotes en el plano YZ (a lo largo de Z), de la altura del muro: no se pasa
    /// andando, pero el teletransporte corto la cruza (RejaAtravesable).
    private static void Reja(string nombre, Transform padre, Vector3 pieCentro, float ancho, Material material)
    {
        var reja = new GameObject(nombre);
        reja.transform.SetParent(padre);
        reja.transform.position = pieCentro;
        reja.isStatic = true;
        var caja = reja.AddComponent<BoxCollider>();
        caja.center = new Vector3(0f, AltoMuroPrueba * 0.5f, 0f);
        caja.size = new Vector3(0.3f, AltoMuroPrueba, ancho);
        reja.AddComponent<RejaAtravesable>();
        int barrotes = Mathf.RoundToInt(ancho / 0.4f);
        for (int i = 0; i <= barrotes; i++)
        {
            float z = -ancho * 0.5f + ancho * i / barrotes;
            var barrote = CrearCubo("Barrote", reja.transform, pieCentro + new Vector3(0f, AltoMuroPrueba * 0.5f, z), new Vector3(0.08f, AltoMuroPrueba, 0.08f), material);
            Object.DestroyImmediate(barrote.GetComponent<Collider>());
        }
        foreach (float y in new[] { 0.1f, 3f, AltoMuroPrueba - 0.1f })
        {
            var travesano = CrearCubo("Travesaño", reja.transform, pieCentro + new Vector3(0f, y, 0f), new Vector3(0.12f, 0.12f, ancho), material);
            Object.DestroyImmediate(travesano.GetComponent<Collider>());
        }
    }

    // ── Piezas comunes de las pruebas ──────────────────────────────────────

    /// Suelo y muros altos (sin doble salto que los pase) y la zona sin vuelo que lo cubre todo.
    private static void SalaDePrueba(Transform zona, Transform raiz, Vector3 c, Material suelo, Material muro, Material velo)
    {
        float m = LadoPrueba * 0.5f, h = AltoMuroPrueba;
        CrearCubo("Suelo", zona, c + new Vector3(0f, -0.25f, 0f), new Vector3(LadoPrueba, 0.5f, LadoPrueba), suelo, CapaSuelo);
        CrearCubo("Muro norte", zona, c + new Vector3(0f, h * 0.5f, m), new Vector3(LadoPrueba, h, 0.5f), muro);
        CrearCubo("Muro sur", zona, c + new Vector3(0f, h * 0.5f, -m), new Vector3(LadoPrueba, h, 0.5f), muro);
        CrearCubo("Muro este", zona, c + new Vector3(m, h * 0.5f, 0f), new Vector3(0.5f, h, LadoPrueba), muro);
        CrearCubo("Muro oeste", zona, c + new Vector3(-m, h * 0.5f, 0f), new Vector3(0.5f, h, LadoPrueba), muro);

        var cupula = CrearCubo("Zona sin vuelo", raiz, c + new Vector3(0f, 15f, 0f), new Vector3(LadoPrueba + 4f, 30f, LadoPrueba + 4f), velo, "Ignore Raycast", estatico: false);
        cupula.GetComponent<Collider>().isTrigger = true;
        cupula.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        cupula.AddComponent<ZonaQueBloqueaHabilidades>();
    }

    /// Cuarto de 8×8 con muros de la prueba (6 m) y la entrada (3 m de ancho, 4 de alto) en su
    /// lado oeste, donde va la puerta.
    private static void CuartoAlto(string nombre, Transform parent, Vector3 centro, Material muro)
    {
        var sala = Grupo(nombre, parent);
        float h = AltoMuroPrueba;
        CrearCubo("Pared norte", sala, centro + new Vector3(0f, h * 0.5f, 4f), new Vector3(8f, h, 0.4f), muro);
        CrearCubo("Pared sur", sala, centro + new Vector3(0f, h * 0.5f, -4f), new Vector3(8f, h, 0.4f), muro);
        CrearCubo("Pared este", sala, centro + new Vector3(4f, h * 0.5f, 0f), new Vector3(0.4f, h, 8f), muro);
        CrearCubo("Pared oeste (1)", sala, centro + new Vector3(-4f, h * 0.5f, -2.75f), new Vector3(0.4f, h, 2.5f), muro);
        CrearCubo("Pared oeste (2)", sala, centro + new Vector3(-4f, h * 0.5f, 2.75f), new Vector3(0.4f, h, 2.5f), muro);
        CrearCubo("Dintel", sala, centro + new Vector3(-4f, (h + 4f) * 0.5f, 0f), new Vector3(0.4f, h - 4f, 3f), muro);
    }

    /// Tramo de muro de la prueba a lo largo de Z, entre zA y zB.
    private static void TramoDeMuroEnZ(string nombre, Transform padre, float x, float zA, float zB, Material muro)
    {
        CrearCubo(nombre, padre, new Vector3(x, AltoMuroPrueba * 0.5f, (zA + zB) * 0.5f), new Vector3(0.5f, AltoMuroPrueba, Mathf.Abs(zB - zA)), muro);
    }

    /// Losas de 2,5 m que aparecen mientras el receptor está encendido.
    private static void LosasTemporales(Transform raiz, ReceptorDeMagia receptor, Material material, Vector3 primera, Vector3 paso, int cuantas)
    {
        for (int i = 0; i < cuantas; i++) LosaTemporal(raiz, receptor, material, primera + paso * i);
    }

    private static void LosaTemporal(Transform raiz, ReceptorDeMagia receptor, Material material, Vector3 centro)
    {
        var losa = CrearCubo($"Losa temporal ({centro.x:0.#}, {centro.z:0.#})", raiz, centro, new Vector3(2.5f, 0.4f, 2.5f), material, CapaSuelo, estatico: false);
        var temporal = losa.AddComponent<PlataformaTemporal>();
        UnityEventTools.AddPersistentListener(receptor.alActivarse, temporal.Mostrar);
        UnityEventTools.AddPersistentListener(receptor.alApagarse, temporal.Ocultar);
    }

    /// EstadoPersistente con id fijo del LAB (lo borra «Reiniciar los puzles del kit»).
    private static EstadoPersistente Estado(Transform raiz, string id)
    {
        var estado = new GameObject("Estado " + id).AddComponent<EstadoPersistente>();
        estado.transform.SetParent(raiz);
        AsignarTexto(estado, "idManual", PuzlesDelLab.PrefijoDeIds + id);
        return estado;
    }

    /// Estrella sobre la entrada que se enciende al completar la prueba (y al volver a cargarla).
    private static (EstadoPersistente estado, PlataformaTemporal estrella) EstrellaDePrueba(string id, Transform raiz, Vector3 c, Material material)
    {
        var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "Estrella de la prueba";
        esfera.transform.SetParent(raiz);
        // Sobre la entrada, pero dentro de la sala: en el muro se colgaba a medias del lado de fuera.
        esfera.transform.SetPositionAndRotation(c + new Vector3(-LadoPrueba * 0.5f + 2.5f, AltoMuroPrueba + 1.5f, 0f), Quaternion.identity);
        esfera.transform.localScale = Vector3.one * 1.2f;
        Object.DestroyImmediate(esfera.GetComponent<Collider>());
        var r = esfera.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        var temporal = esfera.AddComponent<PlataformaTemporal>();
        AsignarTexto(temporal, "sfxAlAparecer", "");

        var estado = Estado(raiz, "PRUEBA_" + id);
        UnityEventTools.AddPersistentListener(estado.alRestaurar, temporal.Mostrar);
        return (estado, temporal);
    }

    /// Final de la prueba: un cofre (tesoro de la región LAB). Al abrirlo la prueba queda
    /// completada: se guarda y se enciende su estrella. 'giro': hacia dónde mira el cofre.
    private static void FinDePrueba((EstadoPersistente estado, PlataformaTemporal estrella) fin, Transform zona, Transform raiz,
                                    Vector3 posicion, float giro, string id)
    {
        var cofre = CrearTesoro("Cofre de la prueba", raiz, posicion, giro, PuzlesDelLab.PrefijoDeIds + "PRUEBA_" + id + "_COFRE", Tesoro.Forma.Cofre);
        Recompensas(cofre, new Premio(PickupEffectType.Currency, RutaEsencia, 150));
        UnityEventTools.AddPersistentListener(cofre.alAbrirse, fin.estado.Marcar);
        UnityEventTools.AddPersistentListener(cofre.alAbrirse, fin.estrella.Mostrar);
        CrearTexto("Meta", zona, posicion + new Vector3(0f, 2.6f, 0f), giro + 180f);
    }
}
