using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;

/// Zonas de prueba alrededor de la arena de CombatLab, una por lado y con un hueco en el muro
/// para entrar: oeste plataformeo, este puzles, norte agua, sur vuelo. Ver INC-508.
///
/// Todo lo que se pisa va en la capa Floor: es la única que el jugador toma como suelo
/// (groundLayer de su controller). Los mecanismos que se mueven o se abren (puertas, ascensor,
/// barrera, runas) y la piscina van fuera de LAB_GEOMETRIA_Y_NAVMESH para no hornearse en el
/// NavMesh como si fueran geometría fija.
public static partial class CombatLabBuilder
{
    private const string CapaSuelo = "Floor";

    private static void CrearZonas(Transform geometria, Material suelo, Material muro)
    {
        var mecanismos = new GameObject("LAB_MECANISMOS (fuera del NavMesh)").transform;
        var plataforma = GetMaterial("Lab_Plataforma", new Color(0.85f, 0.55f, 0.20f));
        var escalada = GetMaterial("Lab_Escalada", new Color(0.30f, 0.62f, 0.32f));
        var puzle = GetMaterial("Lab_Puzle", new Color(0.55f, 0.35f, 0.80f));
        var madera = GetMaterial("Lab_Madera", new Color(0.45f, 0.28f, 0.15f));
        var agua = GetMaterialTransparente("Lab_Agua", new Color(0.20f, 0.50f, 0.90f, 0.45f));

        CrearZonaPlataformeo(geometria, suelo, muro, plataforma, escalada);
        CrearZonaPuzles(geometria, mecanismos, suelo, muro, plataforma, puzle, madera);
        CrearZonaAgua(geometria, mecanismos, suelo, muro, agua);
        CrearZonaVuelo(geometria, suelo, plataforma);
        CrearGeometriaDeSaltos(geometria, suelo, muro, plataforma);
    }

    // ── Oeste: plataformeo ────────────────────────────────────────────────

    private static void CrearZonaPlataformeo(Transform g, Material suelo, Material muro, Material plataforma, Material escalada)
    {
        var zona = Grupo("Zona oeste — plataformeo", g);
        CrearCubo("Suelo", zona, new Vector3(-32f, -0.25f, 1f), new Vector3(36f, 0.5f, 36f), suelo, CapaSuelo);
        MuroConPuertaAlPasillo("Muro oeste", zona, -50f, -17f, 19f, muro);   // el pasillo a la zona de jefes
        CrearCubo("Muro norte", zona, new Vector3(-32f, 1.5f, 19f), new Vector3(36f, 3f, 0.5f), muro);
        CrearCubo("Muro sur", zona, new Vector3(-32f, 1.5f, -17f), new Vector3(36f, 3f, 0.5f), muro);
        CrearTexto("Plataformeo", zona, new Vector3(-17f, 3.5f, 1f), -90f);

        // Recorrido de saltos hacia el oeste: escalones, saltos cada vez más largos y uno que pide doble salto.
        for (int i = 0; i < 4; i++)
            Pilar($"Escalón {i + 1}", zona, new Vector3(-18f - 2f * i, 0f, -10f), new Vector2(2f, 4f), 0.4f * (i + 1), plataforma);
        Pilar("Salto corto", zona, new Vector3(-28.5f, 0f, -10f), new Vector2(3f, 3f), 2f, plataforma);
        Pilar("Salto medio", zona, new Vector3(-33.5f, 0f, -10f), new Vector2(3f, 3f), 2.6f, plataforma);
        Pilar("Salto largo", zona, new Vector3(-39.5f, 0f, -10f), new Vector2(3f, 3f), 2.6f, plataforma);
        Pilar("Doble salto", zona, new Vector3(-46f, 0f, -10f), new Vector2(3f, 3f), 4.2f, plataforma);
        CrearTexto("Saltos → doble salto", zona, new Vector3(-24f, 3.5f, -10f), -90f);

        // Viga estrecha en el aire hasta la meta.
        CrearCubo("Viga estrecha", zona, new Vector3(-46f, 4.05f, -3.5f), new Vector3(0.6f, 0.3f, 10f), plataforma, CapaSuelo);
        Pilar("Meta alta", zona, new Vector3(-46f, 0f, 3.5f), new Vector2(4f, 4f), 4.2f, plataforma);
        CrearTexto("Meta", zona, new Vector3(-46f, 6f, 3.5f), -90f);

        // Rampa hasta un rellano junto al muro norte.
        var rampa = CrearCubo("Rampa", zona, new Vector3(-20f, 1.16f, 10f), new Vector3(4f, 0.3f, 10f), plataforma, CapaSuelo);
        rampa.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
        Pilar("Rellano de la rampa", zona, new Vector3(-20f, 0f, 17f), new Vector2(4f, 4f), 2.6f, plataforma);
        CrearTexto("Rampa", zona, new Vector3(-20f, 2.5f, 5f));

        // Muro de escalada (capa Climb) delante de una torre que se pisa por arriba.
        Pilar("Torre de escalada", zona, new Vector3(-32f, 0f, 15.5f), new Vector2(6f, 5f), 8f, plataforma);
        CrearCubo("Muro de escalada", zona, new Vector3(-32f, 4f, 12.8f), new Vector3(6f, 8f, 0.4f), escalada, "Climb");
        CrearTexto("Escalada", zona, new Vector3(-32f, 2.5f, 11f));
    }

    // ── Este: puzles ──────────────────────────────────────────────────────

    private static void CrearZonaPuzles(Transform g, Transform mecanismos, Material suelo, Material muro,
                                        Material plataforma, Material puzle, Material madera)
    {
        var zona = Grupo("Zona este — puzles", g);
        var piezas = Grupo("Puzles", mecanismos);
        CrearCubo("Suelo", zona, new Vector3(32f, -0.25f, 1f), new Vector3(36f, 0.5f, 36f), suelo, CapaSuelo);
        CrearCubo("Muro este", zona, new Vector3(50f, 1.5f, 1f), new Vector3(0.5f, 3f, 36f), muro);
        // Puerta al norte (x = 32): paso andando a la zona de saltos y volteretas.
        CrearMuroConHueco("Muro norte", zona, new Vector3(32f, 1.5f, 19f), 36f, true, muro);
        CrearCubo("Muro sur", zona, new Vector3(32f, 1.5f, -17f), new Vector3(36f, 3f, 0.5f), muro);
        CrearTexto("Puzles", zona, new Vector3(17f, 3.5f, 1f), 90f);
        CrearTexto("↑ Saltos y volteretas", zona, new Vector3(32f, 3.5f, 18.5f));

        // 1. Dos placas abren una puerta (ActivationCounter + Door).
        CrearSala("Sala de la puerta", zona, new Vector3(42f, 0f, -11f), muro);
        var bisagra = new GameObject("Puerta");
        bisagra.transform.SetParent(piezas);
        bisagra.transform.position = new Vector3(38f, 0f, -12.5f);
        CrearCubo("Hoja", bisagra.transform, new Vector3(38f, 1.5f, -11f), new Vector3(0.3f, 3f, 3f), madera, estatico: false);
        var puerta = bisagra.AddComponent<Door>();

        var contador = new GameObject("Contador de placas").AddComponent<ActivationCounter>();
        contador.transform.SetParent(piezas);
        Asignar(contador, "requiredCount", 2);
        UnityEventTools.AddPersistentListener(contador.onRequirementMet, puerta.Open);
        foreach (var pos in new[] { new Vector3(21f, 0f, -6f), new Vector3(27f, 0f, -13f) })
        {
            var placa = CrearPlaca("Placa de la puerta", piezas, pos, puzle, fija: true);
            UnityEventTools.AddPersistentListener(placa.onActivated, contador.RegisterActivation);
        }
        CrearTexto("Pisa las dos placas", zona, new Vector3(24f, 2.5f, -9.5f), 90f);
        CrearTexto("¡Puerta abierta!", zona, new Vector3(43f, 2f, -11f), 90f);

        // 2. Ascensor: una placa encima de la plataforma la sube mientras estás en ella.
        Pilar("Repisa alta", zona, new Vector3(42f, 0f, 2f), new Vector2(4f, 6f), 4f, plataforma);
        CrearTexto("Repisa", zona, new Vector3(42f, 5.5f, 2f), 90f);
        var ascensor = new GameObject("Ascensor");
        ascensor.transform.SetParent(piezas);
        ascensor.transform.position = new Vector3(38.5f, 0f, 2f);
        CrearCubo("Plataforma", ascensor.transform, new Vector3(38.5f, 0.2f, 2f), new Vector3(3f, 0.4f, 3f), plataforma, CapaSuelo, estatico: false);
        var elevador = ascensor.AddComponent<PlatformElevator>();
        Asignar(elevador, "raiseHeight", 4f);
        var placaAscensor = CrearPlaca("Placa del ascensor", ascensor.transform, new Vector3(38.5f, 0.4f, 2f), puzle, fija: false);
        UnityEventTools.AddBoolPersistentListener(placaAscensor.onActivated, elevador.Raise, false);
        UnityEventTools.AddBoolPersistentListener(placaAscensor.onDeactivated, elevador.Lower, false);
        CrearTexto("Súbete: la placa te eleva", zona, new Vector3(34f, 2.5f, 2f), 90f);

        // 3. Barrera que se quema con un hechizo de fuego (Burnable).
        CrearSala("Sala de la barrera", zona, new Vector3(42f, 0f, 13f), muro);
        var barrera = CrearCubo("Barrera quemable", piezas, new Vector3(38f, 1.5f, 13f), new Vector3(0.5f, 3f, 3f), madera, estatico: false);
        var quemable = barrera.AddComponent<Burnable>();
        Asignar(quemable, "destroyOnlyChildrenWithMesh", false);
        Asignar(quemable, "destroyDelay", 0.5f);
        CrearTexto("Quémala con fuego", zona, new Vector3(35f, 3.5f, 13f), 90f);
        CrearTexto("¡Quemada!", zona, new Vector3(43f, 2f, 13f), 90f);

        // 4. Runas: se enciende una secuencia y hay que repetirla interactuando (RuneSequencePuzzle).
        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        var runas = new RuneStone[3];
        var posiciones = new[] { new Vector3(21f, 0f, 11f), new Vector3(25f, 0f, 12f), new Vector3(29f, 0f, 11f) };
        for (int i = 0; i < runas.Length; i++)
        {
            var piedra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piedra.name = $"Runa {i + 1}";
            piedra.transform.SetParent(piezas);
            piedra.transform.SetPositionAndRotation(posiciones[i] + Vector3.up * 0.6f, Quaternion.identity);
            piedra.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            piedra.GetComponent<Renderer>().sharedMaterial = puzle;
            if (capaInteractuable >= 0) piedra.layer = capaInteractuable;
            var deteccion = piedra.AddComponent<SphereCollider>();
            deteccion.isTrigger = true;
            deteccion.radius = 1.5f;
            piedra.AddComponent<Interactable>();
            runas[i] = piedra.AddComponent<RuneStone>();
        }
        var tapa = CrearCubo("Tapa de las runas", piezas, new Vector3(25f, 1f, 17.5f), new Vector3(3f, 2f, 1f), puzle, estatico: false);
        var compuerta = new GameObject("Premio de las runas").AddComponent<PuzzleRewardGate>();
        compuerta.transform.SetParent(piezas);
        Asignar(compuerta, "blocker", tapa.transform);
        var secuencia = new GameObject("Puzle de runas").AddComponent<RuneSequencePuzzle>();
        secuencia.transform.SetParent(piezas);
        secuencia.transform.position = posiciones[1];
        secuencia.Configure(runas, new[] { 0, 2, 1 });
        Asignar(secuencia, "triggerRadius", 5f);
        UnityEventTools.AddPersistentListener(secuencia.OnSolved, compuerta.Open);
        CrearTexto("Runas: mira el orden y repítelo", zona, new Vector3(25f, 3f, 9f));
        CrearTexto("¡Runas resueltas!", zona, new Vector3(25f, 1.5f, 18.5f));
    }

    /// Cuarto de 8×8 con la entrada (3 m) en su lado oeste, donde va la puerta o la barrera.
    private static void CrearSala(string name, Transform parent, Vector3 centro, Material muro)
    {
        var sala = Grupo(name, parent);
        CrearCubo("Pared norte", sala, centro + new Vector3(0f, 1.5f, 4f), new Vector3(8f, 3f, 0.4f), muro);
        CrearCubo("Pared sur", sala, centro + new Vector3(0f, 1.5f, -4f), new Vector3(8f, 3f, 0.4f), muro);
        CrearCubo("Pared este", sala, centro + new Vector3(4f, 1.5f, 0f), new Vector3(0.4f, 3f, 8f), muro);
        CrearCubo("Pared oeste (1)", sala, centro + new Vector3(-4f, 1.5f, -2.75f), new Vector3(0.4f, 3f, 2.5f), muro);
        CrearCubo("Pared oeste (2)", sala, centro + new Vector3(-4f, 1.5f, 2.75f), new Vector3(0.4f, 3f, 2.5f), muro);
    }

    /// Placa de presión de 2×2: trigger que detecta al jugador y la losa que se hunde al pisarla.
    /// 'fija': se queda pulsada al pisarla (para puzles de varias placas).
    private static PressurePlate CrearPlaca(string name, Transform parent, Vector3 posicion, Material material, bool fija)
    {
        var placa = new GameObject(name);
        placa.transform.SetParent(parent);
        placa.transform.position = posicion;
        var trigger = placa.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.3f, 0f);
        trigger.size = new Vector3(2f, 0.6f, 2f);

        var losa = CrearCubo("Losa", placa.transform, posicion + Vector3.up * 0.075f, new Vector3(2f, 0.15f, 2f), material, estatico: false);
        Object.DestroyImmediate(losa.GetComponent<Collider>());

        var presion = placa.AddComponent<PressurePlate>();
        Asignar(presion, "plateVisual", losa.transform);
        Asignar(presion, "lockWhenActivated", fija);
        return presion;
    }

    // ── Norte: agua ───────────────────────────────────────────────────────

    private static void CrearZonaAgua(Transform g, Transform mecanismos, Material suelo, Material muro, Material agua)
    {
        var zona = Grupo("Zona norte — agua", g);
        CrearCubo("Borde sur", zona, new Vector3(0f, -0.25f, 21f), new Vector3(28f, 0.5f, 4f), suelo, CapaSuelo);
        CrearCubo("Borde norte", zona, new Vector3(0f, -0.25f, 53f), new Vector3(28f, 0.5f, 4f), suelo, CapaSuelo);
        CrearCubo("Borde oeste", zona, new Vector3(-12f, -0.25f, 37f), new Vector3(4f, 0.5f, 28f), suelo, CapaSuelo);
        CrearCubo("Borde este", zona, new Vector3(12f, -0.25f, 37f), new Vector3(4f, 0.5f, 28f), suelo, CapaSuelo);
        CrearCubo("Muro oeste", zona, new Vector3(-14f, 1.5f, 37f), new Vector3(0.5f, 3f, 36f), muro);
        CrearCubo("Muro este", zona, new Vector3(14f, 1.5f, 37f), new Vector3(0.5f, 3f, 36f), muro);
        CrearCubo("Muro norte", zona, new Vector3(0f, 1.5f, 55f), new Vector3(28f, 3f, 0.5f), muro);
        CrearTexto("Agua", zona, new Vector3(0f, 3f, 21f));

        // La piscina (3 m de hondo, con un escalón a 1 m para entrar andando) va fuera del NavMesh.
        var piscina = Grupo("Piscina", mecanismos);
        CrearCubo("Fondo", piscina, new Vector3(0f, -3.25f, 37f), new Vector3(20f, 0.5f, 28f), suelo, CapaSuelo);
        CrearCubo("Pared oeste", piscina, new Vector3(-10.25f, -1.5f, 37f), new Vector3(0.5f, 3f, 28f), muro);
        CrearCubo("Pared este", piscina, new Vector3(10.25f, -1.5f, 37f), new Vector3(0.5f, 3f, 28f), muro);
        CrearCubo("Pared sur", piscina, new Vector3(0f, -1.5f, 22.75f), new Vector3(20f, 3f, 0.5f), muro);
        CrearCubo("Pared norte", piscina, new Vector3(0f, -1.5f, 51.25f), new Vector3(20f, 3f, 0.5f), muro);
        CrearCubo("Zona poco honda", piscina, new Vector3(-7f, -2f, 27f), new Vector3(6f, 2f, 8f), suelo, CapaSuelo);
        CrearCubo("Escalón de entrada", piscina, new Vector3(-8.5f, -0.75f, 24f), new Vector3(3f, 0.5f, 2f), suelo, CapaSuelo);
        CrearTexto("Poco hondo", zona, new Vector3(-7f, 1.5f, 24f));

        // Volumen de agua: trigger en la capa Water (lo que busca PlayerSwimmingController), superficie a -0,3.
        var volumen = CrearCubo("Agua", piscina, new Vector3(0f, -1.65f, 37f), new Vector3(20f, 2.7f, 28f), agua, "Water", estatico: false);
        volumen.GetComponent<Collider>().isTrigger = true;
        volumen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // ── Sur: vuelo ────────────────────────────────────────────────────────

    private static void CrearZonaVuelo(Transform g, Material suelo, Material plataforma)
    {
        var zona = Grupo("Zona sur — vuelo", g);
        CrearCubo("Suelo", zona, new Vector3(0f, -0.25f, -57f), new Vector3(80f, 0.5f, 80f), suelo, CapaSuelo);
        CrearTexto("Vuelo: salta, salta en el aire y vuelve a saltar", zona, new Vector3(0f, 3.5f, -20f), 180f);

        // Límite invisible alto en los tres lados de fuera: se puede volar por encima de los muros
        // bajos sin salirse al vacío. Al norte están la arena y las otras zonas; su muro sur
        // cierra este campo a ras de suelo.
        var limite = Grupo("Límite invisible", zona);
        LimiteInvisible(limite, new Vector3(-40f, 20f, -57f), new Vector3(0.5f, 40f, 80f));
        LimiteInvisible(limite, new Vector3(40f, 20f, -57f), new Vector3(0.5f, 40f, 80f));
        LimiteInvisible(limite, new Vector3(0f, 20f, -97f), new Vector3(80f, 40f, 0.5f));

        Pilar("Torre baja", zona, new Vector3(-20f, 0f, -40f), new Vector2(4f, 4f), 6f, plataforma);
        Pilar("Torre media", zona, new Vector3(15f, 0f, -55f), new Vector2(5f, 5f), 12f, plataforma);
        Pilar("Torre alta", zona, new Vector3(-10f, 0f, -75f), new Vector2(6f, 6f), 22f, plataforma);
        CrearCubo("Plataforma flotante (10 m)", zona, new Vector3(0f, 9.75f, -45f), new Vector3(6f, 0.5f, 6f), plataforma, CapaSuelo);
        CrearCubo("Plataforma flotante (18 m)", zona, new Vector3(25f, 17.75f, -80f), new Vector3(5f, 0.5f, 5f), plataforma, CapaSuelo);
        CrearCubo("Plataforma flotante (26 m)", zona, new Vector3(-30f, 25.75f, -60f), new Vector3(4f, 0.5f, 4f), plataforma, CapaSuelo);
    }

    private static void LimiteInvisible(Transform parent, Vector3 centro, Vector3 tamano)
    {
        var limite = new GameObject("Límite");
        limite.transform.SetParent(parent);
        limite.transform.position = centro;
        limite.AddComponent<BoxCollider>().size = tamano;
        limite.isStatic = true;
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    private static Transform Grupo(string name, Transform parent)
    {
        var grupo = new GameObject(name).transform;
        grupo.SetParent(parent);
        return grupo;
    }

    /// Bloque apoyado en el suelo (y = 0) con la cara de arriba a 'alto', en la capa Floor.
    private static void Pilar(string name, Transform parent, Vector3 baseCentro, Vector2 planta, float alto, Material material)
    {
        CrearCubo(name, parent, baseCentro + Vector3.up * (alto * 0.5f), new Vector3(planta.x, alto, planta.y), material, CapaSuelo);
    }

    private static void Asignar(Object componente, string campo, object valor)
    {
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null)
        {
            Debug.LogWarning($"[CombatLab] {componente.GetType().Name} no tiene el campo '{campo}'.");
            return;
        }
        switch (valor)
        {
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case Object o: p.objectReferenceValue = o; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material GetMaterialTransparente(string name, Color color)
    {
        string path = MaterialsPath + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
