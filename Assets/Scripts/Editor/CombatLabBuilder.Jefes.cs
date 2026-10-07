using UnityEditor;
using UnityEngine;

/// Zona de jefes del LAB (INC-644), al oeste, separada de las demás pruebas: una entrada con
/// un portal por jefe y una arena grande con su círculo. Se llega andando por un pasillo que sale
/// del muro oeste del plataformeo, o por el portal «Zona de jefes →» del área central. El Mago
/// Oscuro abre su propia escena (Batalla Final).
public static partial class CombatLabBuilder
{
    private static readonly Vector3 CentroZonaJefes = new Vector3(-130f, 0f, 0f);
    private const float LadoZonaJefes = 90f;
    private const float RadioArenaJefes = 25f;   // el de los encuentros (arenaRadius)
    private const float PasilloJefesZ = 12f;     // libre en las dos zonas que une
    private const float AnchoPasilloJefes = 6f;

    private const string EncuentroDemonio1 = "Assets/BOSSBATTLES/Encuentro_Demonio1.asset";
    private const string EncuentroDemonio2 = "Assets/BOSSBATTLES/Encuentro_Demonio2.asset";
    private const string EncuentroGolem = "Assets/BOSSBATTLES/Encuentro_Golem1.asset";
    private const string PrefabDemonio2 = "Assets/Prefabs/Enemy/Demon2.prefab";
    private const string VfxPortalJefe = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal red.prefab";
    private const string VfxPortalPaso = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal blue.prefab";

    /// Suelo y muros van en la geometría (entran en el NavMesh); portales y lógica, en su raíz.
    /// Devuelve la entrada de la zona (destino del viaje rápido).
    private static Transform CrearZonaDeJefes(Transform geometria, Material suelo, Material muro)
    {
        Vector3 c = CentroZonaJefes;
        float m = LadoZonaJefes * 0.5f;

        var zonaGeo = new GameObject("Zona de jefes").transform;
        zonaGeo.SetParent(geometria);
        CrearCubo("Suelo", zonaGeo, c + new Vector3(0f, -0.25f, 0f), new Vector3(LadoZonaJefes, 0.5f, LadoZonaJefes), suelo, CapaSuelo);
        CrearCubo("Muro norte", zonaGeo, c + new Vector3(0f, 1.5f, m), new Vector3(LadoZonaJefes, 3f, 0.5f), muro);
        CrearCubo("Muro sur", zonaGeo, c + new Vector3(0f, 1.5f, -m), new Vector3(LadoZonaJefes, 3f, 0.5f), muro);
        MuroConPuertaAlPasillo("Muro este", zonaGeo, c.x + m, c.z - m, c.z + m, muro);
        Pasillo(zonaGeo, c.x + m, -50f, suelo, muro);
        CrearCubo("Muro oeste", zonaGeo, c + new Vector3(-m, 1.5f, 0f), new Vector3(0.5f, 3f, LadoZonaJefes), muro);

        var raiz = new GameObject("LAB_ZONA_DE_JEFES").transform;
        var entrada = Punto("Entrada de la zona de jefes", raiz, c + new Vector3(0f, 0.1f, -m + 5f));
        var centroArena = Punto("Centro de la arena", raiz, c + new Vector3(0f, 0.1f, 4f));
        Circulo(raiz, centroArena.position, RadioArenaJefes);
        CrearTexto("Arena de jefes", raiz, centroArena.position + new Vector3(0f, 6f, RadioArenaJefes));

        var zona = raiz.gameObject.AddComponent<ZonaDeJefesDelLab>();
        var soZona = new SerializedObject(zona);
        soZona.FindProperty("entrada").objectReferenceValue = entrada;
        soZona.FindProperty("centroDeArena").objectReferenceValue = centroArena;
        soZona.ApplyModifiedPropertiesWithoutUndo();

        var matJefe = GetMaterial("Lab_PortalJefe", new Color(0.75f, 0.18f, 0.22f));
        var matPaso = GetMaterial("Lab_PortalPaso", new Color(0.22f, 0.55f, 0.95f));
        float filaZ = -m + 13f;
        PortalDeJefe("Demonio 1 (jefe)", raiz, c + new Vector3(-12f, 0f, filaZ), matJefe, zona, Cargar<BattleEncounterSO>(EncuentroDemonio1), "Demon_1");
        PortalDeJefe("Demonio 2 (jefe)", raiz, c + new Vector3(-4f, 0f, filaZ), matJefe, zona, EncuentroDelDemonio2(), "Demon_2");
        PortalDeJefe("Gólem (jefe)", raiz, c + new Vector3(4f, 0f, filaZ), matJefe, zona, Cargar<BattleEncounterSO>(EncuentroGolem), "Golem_1");
        var mago = CrearPortal<PortalDeEscenaDelLab>("Mago Oscuro (Batalla Final)", raiz, c + new Vector3(12f, 0f, filaZ), matJefe, VfxPortalJefe);
        var soMago = new SerializedObject(mago);
        soMago.FindProperty("escena").stringValue = "BatallaFinal";
        soMago.ApplyModifiedPropertiesWithoutUndo();

        // Ida y vuelta desde el área central.
        var llegadaLab = Punto("Llegada al laboratorio", raiz, new Vector3(0f, 0.1f, -8f));
        PortalDeSalto("← Volver al laboratorio", raiz, c + new Vector3(-22f, 0f, -m + 5f), matPaso, llegadaLab);
        PortalDeSalto("Zona de jefes →", raiz, new Vector3(-10f, 0f, 14f), matPaso, entrada);
        return entrada;
    }

    /// Muro a lo largo de Z (en 'x', de 'zMin' a 'zMax') con el hueco del pasillo de los jefes.
    private static void MuroConPuertaAlPasillo(string nombre, Transform padre, float x, float zMin, float zMax, Material muro)
    {
        float abajo = PasilloJefesZ - AnchoPasilloJefes * 0.5f;
        float arriba = PasilloJefesZ + AnchoPasilloJefes * 0.5f;
        CrearCubo(nombre + " (1)", padre, new Vector3(x, 1.5f, (zMin + abajo) * 0.5f), new Vector3(0.5f, 3f, abajo - zMin), muro);
        CrearCubo(nombre + " (2)", padre, new Vector3(x, 1.5f, (arriba + zMax) * 0.5f), new Vector3(0.5f, 3f, zMax - arriba), muro);
    }

    /// Pasillo andando entre la zona de jefes (xOeste) y el plataformeo (xEste).
    private static void Pasillo(Transform padre, float xOeste, float xEste, Material suelo, Material muro)
    {
        float largo = xEste - xOeste;
        float xc = (xOeste + xEste) * 0.5f;
        float m = AnchoPasilloJefes * 0.5f;
        var pasillo = new GameObject("Pasillo a la zona de jefes").transform;
        pasillo.SetParent(padre);
        CrearCubo("Suelo", pasillo, new Vector3(xc, -0.25f, PasilloJefesZ), new Vector3(largo, 0.5f, AnchoPasilloJefes), suelo, CapaSuelo);
        CrearCubo("Muro norte", pasillo, new Vector3(xc, 1.5f, PasilloJefesZ + m), new Vector3(largo, 3f, 0.5f), muro);
        CrearCubo("Muro sur", pasillo, new Vector3(xc, 1.5f, PasilloJefesZ - m), new Vector3(largo, 3f, 0.5f), muro);
        CrearTexto("Zona de jefes →", pasillo, new Vector3(xEste - 3f, 3.5f, PasilloJefesZ), -90f);
        CrearTexto("← Laboratorio", pasillo, new Vector3(xOeste + 3f, 3.5f, PasilloJefesZ), 90f);
    }

    private static Transform Punto(string nombre, Transform padre, Vector3 posicion)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre);
        t.SetPositionAndRotation(posicion, Quaternion.identity);
        return t;
    }

    private static void PortalDeJefe(string nombre, Transform padre, Vector3 pos, Material mat, ZonaDeJefesDelLab zona,
                                     BattleEncounterSO encuentro, string idDeBatalla)
    {
        if (encuentro == null) Debug.LogWarning($"[CombatLab] Falta el encuentro de «{nombre}».");
        var portal = CrearPortal<PortalDeJefeDelLab>(nombre, padre, pos, mat, VfxPortalJefe);
        var so = new SerializedObject(portal);
        so.FindProperty("zona").objectReferenceValue = zona;
        so.FindProperty("encuentro").objectReferenceValue = encuentro;
        so.FindProperty("idDeBatalla").stringValue = idDeBatalla;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PortalDeSalto(string nombre, Transform padre, Vector3 pos, Material mat, Transform destino)
    {
        var portal = CrearPortal<PortalDeSaltoDelLab>(nombre, padre, pos, mat, VfxPortalPaso);
        var so = new SerializedObject(portal);
        so.FindProperty("destino").objectReferenceValue = destino;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Marco que se atraviesa (disparador), con el efecto de portal y su rótulo encima.
    private static T CrearPortal<T>(string nombre, Transform padre, Vector3 pos, Material mat, string vfx) where T : PortalDelLab
    {
        var marco = CrearCubo("Portal — " + nombre, padre, pos + Vector3.up * 1.5f, new Vector3(2.4f, 3f, 0.4f), mat, estatico: false);
        marco.GetComponent<Collider>().isTrigger = true;
        var portal = marco.AddComponent<T>();

        var efecto = AssetDatabase.LoadAssetAtPath<GameObject>(vfx);
        if (efecto != null)
        {
            var e = (GameObject)PrefabUtility.InstantiatePrefab(efecto, padre);
            e.transform.position = pos + Vector3.up * 1.5f;
        }
        CrearTexto(nombre, padre, pos + Vector3.up * 3.8f);
        return portal;
    }

    /// Círculo en el suelo con el límite de la arena.
    private static void Circulo(Transform padre, Vector3 centro, float radio)
    {
        var go = new GameObject("Círculo de la arena");
        go.transform.SetParent(padre);
        go.transform.position = centro;
        var linea = go.AddComponent<LineRenderer>();
        const int puntos = 96;
        linea.positionCount = puntos;
        linea.loop = true;
        linea.useWorldSpace = false;
        linea.widthMultiplier = 0.35f;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.sharedMaterial = GetMaterial("Lab_CirculoJefes", new Color(0.95f, 0.75f, 0.25f));
        for (int i = 0; i < puntos; i++)
        {
            float a = i * Mathf.PI * 2f / puntos;
            linea.SetPosition(i, new Vector3(Mathf.Cos(a) * radio, 0.06f, Mathf.Sin(a) * radio));
        }
    }

    private static T Cargar<T>(string ruta) where T : Object => AssetDatabase.LoadAssetAtPath<T>(ruta);

    /// El Demonio 2 no tenía encuentro propio (su arena está montada en MainWorld): se crea a
    /// partir del del Demonio 1 con su enemigo y sin la guía del primer combate.
    private static BattleEncounterSO EncuentroDelDemonio2()
    {
        var existente = Cargar<BattleEncounterSO>(EncuentroDemonio2);
        if (existente != null) return existente;
        if (!AssetDatabase.CopyAsset(EncuentroDemonio1, EncuentroDemonio2)) return null;
        var enc = Cargar<BattleEncounterSO>(EncuentroDemonio2);
        var so = new SerializedObject(enc);
        so.FindProperty("enemyPrefab").objectReferenceValue = Cargar<GameObject>(PrefabDemonio2);
        so.FindProperty("guion").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return enc;
    }
}
