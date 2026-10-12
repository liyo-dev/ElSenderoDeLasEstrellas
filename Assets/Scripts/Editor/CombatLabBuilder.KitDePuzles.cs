using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;

/// Kit de puzles del LAB (más allá de la zona de puzles, al este): una estación por pieza
/// genérica de puzle para probarla suelta. Ver INC-705.
///
///  · Cristal de luz (ReceptorDeMagia, Mantenido) → plataformas temporales entre dos pilares.
///  · Molinete de viento (ReceptorDeMagia, Permanente) → abre una puerta (Door).
///  · Brasero de fuego (ReceptorDeMagia, Permanente) → sube un ascensor (PlatformElevator) y lo
///    recuerda en la partida (EstadoPersistente).
///  · Cristal de la lanzadera (solo bolas devueltas con la B) → aparece un premio.
///  · Zona sin vuelo (ZonaQueBloqueaHabilidades) con pilares para subir saltando.
///  · Barrera que pide volar (BarreraConRequisito).
///
/// Suelo y muros van con la geometría del LAB (NavMesh); lo que se mueve o se enciende, en su
/// propia raíz, fuera del NavMesh y sin Damageable cerca (las bolas devueltas buscan receptores
/// de daño por toda la jerarquía raíz).
public static partial class CombatLabBuilder
{
    private const string RaizKit = "LAB_KIT_DE_PUZLES (fuera del NavMesh)";
    private static readonly Vector3 CentroKit = new Vector3(70f, 0f, 1f);
    /// Destino del viaje rápido de la zona.
    private static readonly Vector3 EntradaKitDePuzles = new Vector3(53f, 0.1f, 1f);
    private const float AnchoKit = 40f;
    private const float FondoKit = 36f;

    // Índices de los enums que se escriben por SerializedObject.
    private const int ModoMantenido = 1, ModoPermanente = 2;

    private static void CrearKitDePuzles(Transform geometria, Material suelo, Material muro, Material plataforma, Material madera)
    {
        var zona = Grupo("Zona kit de puzles", geometria);
        var raiz = new GameObject(RaizKit).transform;
        var c = CentroKit;
        float mx = AnchoKit * 0.5f, mz = FondoKit * 0.5f;

        CrearCubo("Suelo", zona, c + new Vector3(0f, -0.25f, 0f), new Vector3(AnchoKit, 0.5f, FondoKit), suelo, CapaSuelo);
        CrearCubo("Muro norte", zona, c + new Vector3(0f, 1.5f, mz), new Vector3(AnchoKit, 3f, 0.5f), muro);
        CrearCubo("Muro sur", zona, c + new Vector3(0f, 1.5f, -mz), new Vector3(AnchoKit, 3f, 0.5f), muro);
        CrearCubo("Muro este", zona, c + new Vector3(mx, 1.5f, 0f), new Vector3(0.5f, 3f, FondoKit), muro);
        CrearTexto("Kit de puzles", zona, new Vector3(52f, 3.5f, 1f), 90f);
        CrearTexto("Pestaña Puzles: qué hechizo usar en cada pieza", zona, new Vector3(52f, 2.8f, 1f), 90f);

        var cristal = GetMaterialEmisivo("Lab_Cristal", new Color(0.75f, 0.85f, 1f));
        var fuego = GetMaterialEmisivo("Lab_Brasero", new Color(0.35f, 0.18f, 0.10f));
        var velo = GetMaterialTransparente("Lab_ZonaSinVuelo", new Color(0.60f, 0.35f, 0.95f, 0.18f));
        var barrera = GetMaterialTransparente("Lab_BarreraDeVuelo", new Color(0.95f, 0.75f, 0.30f, 0.35f));
        var rojo = GetMaterial("Lab_Derribo", new Color(0.85f, 0.25f, 0.25f));

        CrearEstacionDeLuz(zona, raiz, plataforma, cristal);
        CrearEstacionDeViento(zona, raiz, muro, madera, cristal);
        CrearEstacionDeFuego(zona, raiz, plataforma, fuego);
        CrearEstacionDeBolaDevuelta(zona, raiz, rojo, cristal, plataforma);
        CrearEstacionSinVuelo(zona, raiz, plataforma, velo);
        CrearEstacionDeBarrera(zona, raiz, muro, barrera);
        CrearPruebas(geometria, suelo, muro, plataforma, madera);
    }

    // ── Luz: cristal → plataformas temporales ──────────────────────────────

    private static void CrearEstacionDeLuz(Transform zona, Transform raiz, Material plataforma, Material cristal)
    {
        const float z = -11f, alto = 3.6f;
        Pilar("Escalón de luz 1", zona, new Vector3(53f, 0f, z), new Vector2(2f, 3f), 1.2f, plataforma);
        Pilar("Escalón de luz 2", zona, new Vector3(55f, 0f, z), new Vector2(2f, 3f), 2.4f, plataforma);
        Pilar("Pilar de salida", zona, new Vector3(58f, 0f, z), new Vector2(3f, 3f), alto, plataforma);
        Pilar("Pilar de llegada", zona, new Vector3(75f, 0f, z), new Vector2(3f, 3f), alto, plataforma);

        // A la altura del pecho de quien dispara desde el pilar de salida.
        var receptor = CrearReceptor("Cristal de luz", raiz, new Vector3(75f, alto + 1f, z), 1f, cristal,
                                     new[] { (int)MagicElement.Light }, ModoMantenido, 6f, new Color(0.75f, 0.85f, 1f));
        foreach (float x in new[] { 62.5f, 66.5f, 70.5f })
        {
            var losa = CrearCubo($"Plataforma de luz (x {x})", raiz, new Vector3(x, alto - 0.2f, z), new Vector3(2.5f, 0.4f, 2.5f), cristal, CapaSuelo, estatico: false);
            var temporal = losa.AddComponent<PlataformaTemporal>();
            UnityEventTools.AddPersistentListener(receptor.alActivarse, temporal.Mostrar);
            UnityEventTools.AddPersistentListener(receptor.alApagarse, temporal.Ocultar);
        }
        CrearTexto("Luz: dispara al cristal de enfrente y cruza", zona, new Vector3(54f, 5.5f, z), 90f);
        CrearTexto("Bola Prisma · Estrella Fugaz · Lluvia de Chispas", zona, new Vector3(54f, 4.8f, z), 90f);
    }

    // ── Viento: molinete → puerta ──────────────────────────────────────────

    private static void CrearEstacionDeViento(Transform zona, Transform raiz, Material muro, Material madera, Material cristal)
    {
        var centroSala = new Vector3(66f, 0f, 1f);
        CrearSala("Sala del molinete", zona, centroSala, muro);
        var bisagra = new GameObject("Puerta del molinete");
        bisagra.transform.SetParent(raiz);
        bisagra.transform.position = centroSala + new Vector3(-4f, 0f, -1.5f);
        CrearCubo("Hoja", bisagra.transform, centroSala + new Vector3(-4f, 1.5f, 0f), new Vector3(0.3f, 3f, 3f), madera, estatico: false);
        var puerta = bisagra.AddComponent<Door>();

        var receptor = CrearMolinete("Molinete de viento", raiz, new Vector3(57f, 0f, 5f), -90f, ModoPermanente, 0f, madera, cristal);
        UnityEventTools.AddPersistentListener(receptor.alActivarse, puerta.Open);

        CrearTexto("Viento al molinete: abre la sala", zona, new Vector3(54f, 4.5f, 5f), 90f);
        CrearTexto("Ráfaga · Huracán · Ciclón", zona, new Vector3(54f, 3.8f, 5f), 90f);
        CrearTexto("¡Abierta!", zona, centroSala + new Vector3(1f, 2f, 0f), 90f);
    }

    // ── Fuego: brasero → ascensor, guardado en la partida ──────────────────

    private static void CrearEstacionDeFuego(Transform zona, Transform raiz, Material plataforma, Material fuego)
    {
        const float z = 13f;
        var brasero = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brasero.name = "Brasero";
        brasero.transform.SetParent(raiz);
        brasero.transform.SetPositionAndRotation(new Vector3(56f, 0.6f, z), Quaternion.identity);
        brasero.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
        brasero.GetComponent<Renderer>().sharedMaterial = fuego;
        var receptor = ConfigurarReceptor(brasero, new[] { (int)MagicElement.Fire }, ModoPermanente, 0f, new Color(1f, 0.5f, 0.15f));

        Pilar("Repisa del ascensor", zona, new Vector3(65f, 0f, z), new Vector2(4f, 4f), 4f, plataforma);
        var ascensor = new GameObject("Ascensor del brasero");
        ascensor.transform.SetParent(raiz);
        ascensor.transform.position = new Vector3(60.5f, 0f, z);
        CrearCubo("Plataforma", ascensor.transform, new Vector3(60.5f, 0.2f, z), new Vector3(3f, 0.4f, 3f), plataforma, CapaSuelo, estatico: false);
        var elevador = ascensor.AddComponent<PlatformElevator>();
        Asignar(elevador, "raiseHeight", 4f);

        var estado = new GameObject("Estado del brasero").AddComponent<EstadoPersistente>();
        estado.transform.SetParent(raiz);
        AsignarTexto(estado, "idManual", PuzlesDelLab.PrefijoDeIds + "KIT_BRASERO");

        UnityEventTools.AddBoolPersistentListener(receptor.alActivarse, elevador.Raise, false);
        UnityEventTools.AddPersistentListener(receptor.alActivarse, estado.Marcar);
        UnityEventTools.AddPersistentListener(estado.alRestaurar, elevador.TeleportToRaised);
        UnityEventTools.AddPersistentListener(estado.alRestaurar, receptor.RestaurarEncendido);

        CrearTexto("Fuego al brasero: sube el ascensor", zona, new Vector3(54f, 3.5f, z), 90f);
        CrearTexto("Se guarda: pulsa R y sigue arriba", zona, new Vector3(54f, 2.8f, z), 90f);
    }

    // ── Bola devuelta: cristal en la lanzadera ─────────────────────────────

    private static void CrearEstacionDeBolaDevuelta(Transform zona, Transform raiz, Material rojo, Material cristal, Material plataforma)
    {
        const float z = 1f;
        // Mira al oeste; las bolas salen 1,2 m por delante del cristal, que está justo donde
        // apunta la bola devuelta (el lanzador + 1 m de alto).
        var lanzadera = CrearLanzadera(raiz, new Vector3(84f, 1.5f, z), 270f, rojo, cadencia: 3f, alcance: 10f,
                                       bocaLocal: new Vector3(0f, 1f, 1.2f));
        if (lanzadera == null)
        {
            Debug.LogWarning("[CombatLab] Kit de puzles: no encuentro el proyectil del Demonio; no hay estación de bola devuelta.");
            return;
        }
        var receptor = CrearReceptor("Cristal de la lanzadera", lanzadera.transform, new Vector3(84f, 2.5f, z), 0.9f, cristal,
                                     new int[0], ModoPermanente, 0f, new Color(1f, 0.72f, 0.12f));
        Asignar(receptor, "soloMagiaDevuelta", true);

        var premio = CrearCubo("Premio: escalón de luz", raiz, new Vector3(79f, 0.6f, z + 3f), new Vector3(1.5f, 1.2f, 1.5f), plataforma, CapaSuelo, estatico: false);
        var temporal = premio.AddComponent<PlataformaTemporal>();
        UnityEventTools.AddPersistentListener(receptor.alActivarse, temporal.Mostrar);

        CrearMarcador("Puesto: devuelve la bola", zona, new Vector3(77f, 0.02f, z), GetMaterial("Lab_Marcadores", new Color(0.20f, 0.48f, 0.70f)));
        CrearTexto("Devuelve su bola con B: enciende el cristal", zona, new Vector3(76f, 3f, z - 2.5f), 90f);
    }

    // ── Zona sin vuelo ─────────────────────────────────────────────────────

    private static void CrearEstacionSinVuelo(Transform zona, Transform raiz, Material plataforma, Material velo)
    {
        var cupula = CrearCubo("Zona sin vuelo", raiz, new Vector3(84f, 6f, -10f), new Vector3(10f, 12f, 12f), velo, "Ignore Raycast", estatico: false);
        cupula.GetComponent<Collider>().isTrigger = true;
        cupula.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        cupula.AddComponent<ZonaQueBloqueaHabilidades>();

        Pilar("Sin vuelo 1,5 m", zona, new Vector3(81f, 0f, -13f), new Vector2(2.5f, 2.5f), 1.5f, plataforma);
        Pilar("Sin vuelo 2,6 m", zona, new Vector3(84f, 0f, -10f), new Vector2(2.5f, 2.5f), 2.6f, plataforma);
        Pilar("Sin vuelo 4,2 m", zona, new Vector3(87f, 0f, -7f), new Vector2(2.5f, 2.5f), 4.2f, plataforma);
        CrearTexto("Meta", zona, new Vector3(87f, 5.5f, -7f), 90f);
        CrearTexto("Zona sin vuelo: entra volando", zona, new Vector3(77f, 4f, -10f), 90f);
        CrearTexto("Dentro solo vale saltar", zona, new Vector3(77f, 3.3f, -10f), 90f);
    }

    // ── Barrera que pide volar ─────────────────────────────────────────────

    private static void CrearEstacionDeBarrera(Transform zona, Transform raiz, Material muro, Material barrera)
    {
        var centroSala = new Vector3(84f, 0f, 13f);
        CrearSala("Sala de la barrera de vuelo", zona, centroSala, muro);

        var bloqueo = CrearCubo("Barrera (cerrada sin vuelo)", raiz, centroSala + new Vector3(-4f, 1.5f, 0f), new Vector3(0.3f, 3f, 3f), barrera, estatico: false);
        bloqueo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var trigger = new GameObject("Aviso de la barrera");
        trigger.transform.SetParent(raiz);
        trigger.transform.position = centroSala + new Vector3(-6f, 1.5f, 0f);
        int ignorar = LayerMask.NameToLayer("Ignore Raycast");
        if (ignorar >= 0) trigger.layer = ignorar;
        var caja = trigger.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(3f, 3f, 4f);
        var barreraConRequisito = trigger.AddComponent<BarreraConRequisito>();
        var so = new SerializedObject(barreraConRequisito);
        var habilidades = so.FindProperty("requisito.habilidades");
        habilidades.arraySize = 1;
        habilidades.GetArrayElementAtIndex(0).enumValueIndex = (int)AbilityKey.Fly;
        so.FindProperty("bloqueo").objectReferenceValue = bloqueo;
        so.ApplyModifiedPropertiesWithoutUndo();

        CrearTexto("Barrera: hace falta volar", zona, new Vector3(76f, 3.5f, 13f), 90f);
        CrearTexto("Quita el vuelo en Magia ▸ Habilidades", zona, new Vector3(76f, 2.8f, 13f), 90f);
        CrearTexto("¡Has pasado!", zona, centroSala + new Vector3(1f, 2f, 0f), 90f);
    }

    // ── Utilidades del kit ─────────────────────────────────────────────────

    /// Molinete de viento: poste, cubo central que brilla y aspas que giran mientras está encendido.
    /// 'giroHaciaElJugador': hacia dónde miran las aspas (-90 = hacia el oeste). Un único collider
    /// recibe los hechizos desde el suelo hasta encima del cubo central.
    private static ReceptorDeMagia CrearMolinete(string nombre, Transform raiz, Vector3 pie, float giroHaciaElJugador,
                                                int modo, float segundos, Material madera, Material cristal)
    {
        var molinete = new GameObject(nombre);
        molinete.transform.SetParent(raiz, false);
        molinete.transform.position = pie;
        var caja = molinete.AddComponent<BoxCollider>();
        caja.center = new Vector3(0f, 1.1f, 0f);
        caja.size = new Vector3(1.2f, 2.2f, 1.2f);
        caja.isTrigger = false;

        var poste = CrearCubo("Poste del molinete", molinete.transform, pie + new Vector3(0f, 0.4f, 0f), new Vector3(0.4f, 0.8f, 0.4f), madera, estatico: false);
        Object.DestroyImmediate(poste.GetComponent<Collider>());
        var centro = CrearCubo("Cubo central", molinete.transform, pie + new Vector3(0f, 1.25f, 0f), Vector3.one * 0.9f, cristal, estatico: false);
        centro.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        Object.DestroyImmediate(centro.GetComponent<Collider>());
        var receptor = ConfigurarReceptor(molinete, new[] { (int)MagicElement.Storm }, modo, segundos,
                                          new Color(0.7f, 1f, 0.85f), centro.GetComponent<Renderer>());
        receptor.GetComponentInChildren<Light>().transform.position = centro.transform.position + Vector3.up * 0.5f;
        var eje = new GameObject("Aspas").transform;
        eje.SetParent(receptor.transform, false);
        eje.position = centro.transform.position;
        eje.rotation = Quaternion.Euler(0f, giroHaciaElJugador, 0f);
        foreach (float giro in new[] { 0f, 90f })
        {
            var aspa = CrearCubo("Aspa", eje, eje.position, new Vector3(2.2f, 0.25f, 0.08f), madera, estatico: false);
            aspa.transform.rotation = eje.rotation * Quaternion.Euler(0f, 0f, giro);
            Object.DestroyImmediate(aspa.GetComponent<Collider>());
        }
        Asignar(receptor, "parteQueGira", eje);
        return receptor;
    }

    /// Cubo con ReceptorDeMagia (collider sólido para que lo encuentren proyectiles y bolas devueltas).
    private static ReceptorDeMagia CrearReceptor(string nombre, Transform padre, Vector3 posicion, float lado, Material material,
                                                 int[] elementos, int modo, float segundos, Color color)
    {
        var cubo = CrearCubo(nombre, padre, posicion, Vector3.one * lado, material, estatico: false);
        cubo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        return ConfigurarReceptor(cubo, elementos, modo, segundos, color);
    }

    private static ReceptorDeMagia ConfigurarReceptor(GameObject objeto, int[] elementos, int modo, float segundos, Color color,
                                                     Renderer rendererDeBrillo = null)
    {
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(objeto.transform, false);
        luz.transform.localPosition = Vector3.up * 0.5f;
        luz.type = LightType.Point;
        luz.range = 6f;
        luz.intensity = 3f;
        luz.color = color;
        luz.enabled = false;

        var receptor = objeto.AddComponent<ReceptorDeMagia>();
        var so = new SerializedObject(receptor);
        var lista = so.FindProperty("elementosAceptados");
        lista.arraySize = elementos.Length;
        for (int i = 0; i < elementos.Length; i++) lista.GetArrayElementAtIndex(i).enumValueIndex = elementos[i];
        so.FindProperty("modo").enumValueIndex = modo;
        if (segundos > 0f) so.FindProperty("segundosEncendido").floatValue = segundos;
        var brillo = so.FindProperty("brillo");
        brillo.arraySize = 1;
        brillo.GetArrayElementAtIndex(0).objectReferenceValue = rendererDeBrillo != null ? rendererDeBrillo : objeto.GetComponent<Renderer>();
        so.FindProperty("colorEncendido").colorValue = color;
        so.FindProperty("luz").objectReferenceValue = luz;
        so.ApplyModifiedPropertiesWithoutUndo();
        return receptor;
    }

    private static void AsignarTexto(Object componente, string campo, string valor)
    {
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[CombatLab] {componente.GetType().Name} no tiene el campo '{campo}'."); return; }
        p.stringValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Material opaco con emisión activada (el ReceptorDeMagia la sube al encenderse).
    private static Material GetMaterialEmisivo(string name, Color color)
    {
        string path = MaterialsPath + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
