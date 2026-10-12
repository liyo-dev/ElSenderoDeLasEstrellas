#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

/// Prefabs de los extras del Cap. 2 (INC-725), montados solo con piezas oficiales:
///  · Puente sin magia: ZonaQueBloqueaHabilidades (magia y vuelo) + BarreraConRequisito que pide
///    la cuerda y los clavos + EstadoPersistente para que la tabla siga arreglada al cargar.
///  · Piezas del puente (cuerda y clavos): WorldPickup al bolsillo + EstadoPersistente (su flag
///    es el requisito del arreglo).
///  · Plato que persigue: PropQuePersigue sobre un plato del pack.
///  · Campamento de contrabandistas: jaulas que arden con fuego (Burnable) y avisan al grafo,
///    pluma brillante, claro que cuenta como «atacar» y contador de contrabandistas vencidos.
/// Los colocan las tandas de cada bloque en MainWorld y la zona del LAB (CombatLabBuilder.Cap2).
public static class Cap2ExtrasPrefabs
{
    public const string Carpeta = "Assets/Prefabs/Cap2";
    public const string Puente = Carpeta + "/PuenteSinMagia.prefab";
    public const string Cuerda = Carpeta + "/PiezaDelPuente_Cuerda.prefab";
    public const string Clavos = Carpeta + "/PiezaDelPuente_Clavos.prefab";
    public const string Plato = Carpeta + "/PlatoQuePersigue.prefab";
    public const string Campamento = Carpeta + "/CampamentoContrabandistas.prefab";

    const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/";
    const string IdCuerda = "CAP2_PUENTE_CUERDA";
    const string IdClavos = "CAP2_PUENTE_CLAVOS";

    // Medidas del puente (eje largo = Z local, hueco centrado en el origen, tablero a y = 0).
    public const float AnchoPuente = 2.4f;
    public const float Hueco = 1.2f;

    [MenuItem("El Sendero/Capítulo 2/Extras INC-725: crear prefabs")]
    public static void Asegurar()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Sal de Play para crear los prefabs.");
        Cap2Extras.AsegurarObjetos();
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Prefabs", "Cap2");
        Guardar(CrearPuente(), Puente);
        Guardar(CrearPieza("PiezaDelPuente_Cuerda", "IT_Cuerda", IdCuerda, Pack + "Goods/Rope01_a01.prefab"), Cuerda);
        Guardar(CrearPieza("PiezaDelPuente_Clavos", "IT_Clavos", IdClavos, Pack + "Goods/Tool01_a01.prefab"), Clavos);
        Guardar(CrearPlato(), Plato);
        Guardar(CrearCampamento(), Campamento);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cap2ExtrasPrefabs] Prefabs en " + Carpeta);
    }

    static GameObject CrearPuente()
    {
        var raiz = new GameObject("PuenteSinMagia");

        var zona = Hijo("ZonaSinMagia", raiz.transform, new Vector3(0f, 2f, 0f));
        var cajaZona = zona.AddComponent<BoxCollider>();
        cajaZona.isTrigger = true; cajaZona.size = new Vector3(AnchoPuente + 3f, 4f, Hueco + 8f);
        var bloqueaMagia = zona.AddComponent<ZonaQueBloqueaHabilidades>();
        var so = new SerializedObject(bloqueaMagia);
        var lista = so.FindProperty("habilidadesBloqueadas");
        lista.arraySize = 2;
        lista.GetArrayElementAtIndex(0).enumValueIndex = (int)PlayerAbility.Magic;
        lista.GetArrayElementAtIndex(1).enumValueIndex = (int)PlayerAbility.Fly;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Hueco infranqueable mientras falte la tabla (sin saltarlo ni volarlo).
        var bloqueo = Hijo("Bloqueo", raiz.transform, new Vector3(0f, 1.25f, 0f));
        bloqueo.AddComponent<BoxCollider>().size = new Vector3(AnchoPuente, 2.5f, Hueco);

        // La tabla nueva tapa el hueco y se pisa; su caja no depende de la malla del pack.
        var arreglada = Hijo("TablaArreglada", raiz.transform, new Vector3(0f, -0.12f, 0f));
        var cajaTabla = arreglada.AddComponent<BoxCollider>();
        cajaTabla.size = new Vector3(AnchoPuente, 0.12f, Hueco + 0.3f); cajaTabla.center = new Vector3(0f, 0.06f, 0f);
        Modelo(Pack + "Goods/Wood01_a01.prefab", arreglada.transform, "Modelo", cajaTabla.size, uniforme: false);
        arreglada.SetActive(false);

        var rota = Hijo("TablaRota", raiz.transform, new Vector3(AnchoPuente * 0.5f + 0.4f, -0.6f, 0.2f));
        rota.transform.localRotation = Quaternion.Euler(18f, 25f, -30f);
        Modelo(Pack + "Goods/Wood01_a01.prefab", rota.transform, "Modelo", new Vector3(AnchoPuente * 0.8f, 0.12f, Hueco * 0.6f), uniforme: false);

        var arreglo = Hijo("Arreglo", raiz.transform, new Vector3(0f, 1.5f, 0f));
        var cajaArreglo = arreglo.AddComponent<BoxCollider>();
        cajaArreglo.isTrigger = true; cajaArreglo.size = new Vector3(AnchoPuente + 1f, 3f, Hueco + 3f);
        var barrera = arreglo.AddComponent<BarreraConRequisito>();
        var estado = arreglo.AddComponent<EstadoPersistente>();
        var senal = arreglo.AddComponent<SignalEmitter>();
        senal.eventKey = "CAP2_PUENTE_ARREGLADO"; senal.trigger = SignalEmitter.TriggerType.Manual; senal.once = true;
        so = new SerializedObject(barrera);
        var flags = so.FindProperty("requisito.flags");
        flags.arraySize = 2;
        flags.GetArrayElementAtIndex(0).stringValue = EstadoPersistente.Prefijo + IdCuerda;
        flags.GetArrayElementAtIndex(1).stringValue = EstadoPersistente.Prefijo + IdClavos;
        so.FindProperty("requisito.claveDeAviso").stringValue = "CAP2_PUENTE_FALTAN_PIEZAS";
        so.FindProperty("bloqueo").objectReferenceValue = bloqueo;
        so.ApplyModifiedPropertiesWithoutUndo();
        so = new SerializedObject(estado);
        so.FindProperty("idManual").stringValue = "CAP2_PUENTE_ARREGLADO";
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var ev in new[] { barrera.alCumplirse, estado.alRestaurar })
        {
            UnityEventTools.AddBoolPersistentListener(ev, arreglada.SetActive, true);
            UnityEventTools.AddBoolPersistentListener(ev, rota.SetActive, false);
        }
        UnityEventTools.AddPersistentListener(barrera.alCumplirse, estado.Marcar);
        UnityEventTools.AddPersistentListener(barrera.alCumplirse, senal.Send);
        return raiz;
    }

    static GameObject CrearPieza(string nombre, string item, string id, string modelo)
    {
        var raiz = new GameObject(nombre);
        var esfera = raiz.AddComponent<SphereCollider>();
        esfera.isTrigger = true; esfera.radius = 0.8f; esfera.center = new Vector3(0f, 0.5f, 0f);
        Modelo(modelo, raiz.transform, "Modelo", new Vector3(0.5f, 0.5f, 0.5f), uniforme: true);
        var estado = raiz.AddComponent<EstadoPersistente>();
        var so = new SerializedObject(estado);
        so.FindProperty("idManual").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        var pickup = raiz.AddComponent<WorldPickup>();
        so = new SerializedObject(pickup);
        var efectos = so.FindProperty("effects");
        efectos.arraySize = 1;
        var e = efectos.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("effectType").enumValueIndex = (int)PickupEffectType.AddToInventory;
        e.FindPropertyRelative("item").objectReferenceValue = Cargar<ItemData>("Assets/_ITEMS/" + item + ".asset");
        e.FindPropertyRelative("quantity").intValue = 1;
        so.FindProperty("pickupIdOverride").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(EventoPrivado(pickup, "onCollected"), estado.Marcar);
        return raiz;
    }

    static GameObject CrearPlato()
    {
        var raiz = new GameObject("PlatoQuePersigue");
        Modelo(Pack + "Food/Dish01_a01.prefab", raiz.transform, "Modelo", new Vector3(0.35f, 0.35f, 0.35f), uniforme: true);
        raiz.AddComponent<PropQuePersigue>();
        return raiz;
    }

    static GameObject CrearCampamento()
    {
        var raiz = new GameObject("CampamentoContrabandistas");
        Modelo(Pack + "Goods/Campfire01_a01.prefab", raiz.transform, "Hoguera", new Vector3(1.2f, 1.2f, 1.2f), uniforme: true, conColisiones: true);
        var carro = Modelo(Pack + "Goods/Cart01.prefab", raiz.transform, "Carro", new Vector3(4f, 4f, 4f), uniforme: true, conColisiones: true);
        carro.transform.localPosition = new Vector3(-4f, 0f, 2f);

        // Entrar en el claro es plantar cara: el grafo lo toma como la decisión de pelear.
        var claro = Hijo("Claro", raiz.transform, Vector3.zero);
        var senalClaro = claro.AddComponent<SignalEmitter>();
        senalClaro.eventKey = Cap2Extras.SenalAtaque; senalClaro.trigger = SignalEmitter.TriggerType.Manual;
        senalClaro.once = true; senalClaro.radioDeProximidad = 5f;

        var jaulas = Hijo("Jaulas", raiz.transform, new Vector3(3f, 0f, 1f));
        var senalJaula = jaulas.AddComponent<SignalEmitter>();
        senalJaula.eventKey = Cap2Extras.SenalJaulaQuemada; senalJaula.trigger = SignalEmitter.TriggerType.Manual; senalJaula.once = true;
        for (int i = 0; i < 12; i++)
        {
            var jaula = Hijo("Jaula_" + (i + 1).ToString("00"), jaulas.transform,
                new Vector3((i % 4) * 0.8f, (i / 8) * 0.7f, (i / 4 % 2) * 0.8f));
            var modelo = Modelo(Pack + "Goods/Cage01_a01.prefab", jaula.transform, "Modelo", new Vector3(0.7f, 0.7f, 0.7f), uniforme: true);
            var b = Limites(modelo);
            var caja = jaula.AddComponent<BoxCollider>();
            caja.center = jaula.transform.InverseTransformPoint(b.center);
            caja.size = b.size;
            var arde = jaula.AddComponent<Burnable>();
            var estado = jaula.AddComponent<EstadoPersistente>();
            var so = new SerializedObject(estado);
            so.FindProperty("idManual").stringValue = "CAP2_JAULA_" + (i + 1).ToString("00");
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(arde.onBurned, senalJaula.Send);
            UnityEventTools.AddPersistentListener(arde.onBurned, estado.Marcar);
            UnityEventTools.AddBoolPersistentListener(estado.alRestaurar, jaula.SetActive, false);
        }

        var pluma = CrearPluma();
        pluma.transform.SetParent(raiz.transform, false);
        pluma.transform.localPosition = new Vector3(0f, 0f, -8f);

        // Cada contrabandista vencido suma uno (Damageable de su prefab → RegistrarVencido);
        // al llegar a cinco, el grafo sigue.
        var vencidos = Hijo("Contrabandistas", raiz.transform, new Vector3(0f, 0f, 1.5f));
        var contador = vencidos.AddComponent<ActivationCounter>();
        var sc = new SerializedObject(contador);
        sc.FindProperty("requiredCount").intValue = 5;
        sc.ApplyModifiedPropertiesWithoutUndo();
        var senalVencidos = vencidos.AddComponent<SignalEmitter>();
        senalVencidos.eventKey = Cap2Extras.SenalVencidos; senalVencidos.trigger = SignalEmitter.TriggerType.Manual; senalVencidos.once = true;
        UnityEventTools.AddPersistentListener(contador.onRequirementMet, senalVencidos.Send);
        return raiz;
    }

    static GameObject CrearPluma()
    {
        var raiz = new GameObject("PlumaBrillante");
        var esfera = raiz.AddComponent<SphereCollider>();
        esfera.isTrigger = true; esfera.radius = 0.7f; esfera.center = new Vector3(0f, 0.6f, 0f);
        var brillo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        brillo.name = "Brillo";
        UnityEngine.Object.DestroyImmediate(brillo.GetComponent<Collider>());
        brillo.transform.SetParent(raiz.transform, false);
        brillo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        brillo.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
        brillo.GetComponent<Renderer>().sharedMaterial = MaterialPluma();
        var pickup = raiz.AddComponent<WorldPickup>();
        var so = new SerializedObject(pickup);
        var efectos = so.FindProperty("effects");
        efectos.arraySize = 1;
        var e = efectos.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("effectType").enumValueIndex = (int)PickupEffectType.AddToInventory;
        e.FindPropertyRelative("item").objectReferenceValue = Cargar<ItemData>("Assets/_ITEMS/IT_PlumaBrillante.asset");
        e.FindPropertyRelative("quantity").intValue = 1;
        so.FindProperty("pickupIdOverride").stringValue = "CAP2_PLUMA_BRILLANTE";
        so.ApplyModifiedPropertiesWithoutUndo();
        return raiz;
    }

    static Material MaterialPluma()
    {
        string ruta = Carpeta + "/Cap2_PlumaBrillante.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        m.SetColor("_BaseColor", Color.white);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1f, 0.97f, 0.9f) * 2.5f);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ── Utilidades ──────────────────────────────────────────────────────────

    static GameObject Hijo(string nombre, Transform padre, Vector3 posicion)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posicion;
        return go;
    }

    /// Instancia un modelo del pack desempaquetado, lo escala a 'tamano' (uniforme: el lado
    /// mayor; si no, por ejes) y lo apoya en el origen local. Quita sus colisiones salvo que
    /// se pidan.
    static GameObject Modelo(string ruta, Transform padre, string nombre, Vector3 tamano, bool uniforme, bool conColisiones = false)
    {
        var asset = Cargar<GameObject>(ruta);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = nombre;
        if (!conColisiones)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var b = Limites(go);
        if (b.size.sqrMagnitude > 1e-6f)
        {
            Vector3 escala;
            if (uniforme)
            {
                float lado = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float objetivo = Mathf.Max(tamano.x, Mathf.Max(tamano.y, tamano.z));
                escala = Vector3.one * (objetivo / lado);
            }
            else
                escala = new Vector3(tamano.x / Mathf.Max(b.size.x, 1e-3f), tamano.y / Mathf.Max(b.size.y, 1e-3f), tamano.z / Mathf.Max(b.size.z, 1e-3f));
            go.transform.localScale = escala;
            b = Limites(go);
            var desfase = go.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += desfase;
        }
        return go;
    }

    static Bounds Limites(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// UnityEvent privado de un componente (para añadirle una llamada persistente en el Editor).
    static UnityEvent EventoPrivado(Component c, string campo)
    {
        var f = c.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException(c.GetType().Name + " ya no tiene el campo " + campo);
        var ev = (UnityEvent)f.GetValue(c);
        if (ev == null) { ev = new UnityEvent(); f.SetValue(c, ev); }
        return ev;
    }

    static void Guardar(GameObject go, string ruta)
    {
        try { PrefabUtility.SaveAsPrefabAsset(go, ruta, out bool ok); if (!ok) throw new InvalidOperationException("No se pudo guardar " + ruta); }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static T Cargar<T>(string ruta) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException("Falta " + ruta);
}
#endif
