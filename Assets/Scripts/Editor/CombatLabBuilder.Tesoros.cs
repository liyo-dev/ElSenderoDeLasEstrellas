using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;

/// Tesoros del LAB (al norte de las Pruebas): uno por capa de requisito, un punto de tesoro que
/// marca el mapa del tesoro y la placa que da la llave. También el cofre con el que acaban las
/// Pruebas. Región del registro: LAB. Ver INC-721.
///
///  1. Sin requisito: Esencia y el mapa del tesoro del LAB.
///  2. Habilidad (nadar): dos pociones de vida.
///  3. Objeto (llave, en la placa de al lado): una prenda.
///  4. Vuelo: una página del grimorio.
///  5. Hechizo Amanecer (si aún no existe, un requisito imposible con el mismo aviso): Esencia.
///  · Punto de tesoro en la esquina: sale en el minimapa al llevar el mapa.
public static partial class CombatLabBuilder
{
    private const string RegionLab = "LAB";
    private const string RutaMapaDelTesoroLab = "Assets/_ITEMS/IT_MapaDelTesoro_Lab.asset";
    private const string RutaEsencia = "Assets/_ITEMS/IT_Esencia.asset";
    private static readonly Vector3 CentroTesoros = new Vector3(122f, 0f, 96f);
    /// Destino del viaje rápido.
    private static readonly Vector3 EntradaTesoros = new Vector3(106f, 0.1f, 96f);

    internal struct Premio
    {
        public PickupEffectType tipo;
        public string ruta;
        public int cantidad;
        public Premio(PickupEffectType tipo, string ruta, int cantidad) { this.tipo = tipo; this.ruta = ruta; this.cantidad = cantidad; }
    }

    private static void CrearEstacionDeTesoros(Transform geometria, Material suelo, Material muro)
    {
        var zona = Grupo("Tesoros", geometria);
        var raiz = new GameObject("LAB_TESOROS (fuera del NavMesh)").transform;
        var c = CentroTesoros;
        float m = LadoPrueba * 0.5f;
        CrearCubo("Suelo", zona, c + new Vector3(0f, -0.25f, 0f), new Vector3(LadoPrueba, 0.5f, LadoPrueba), suelo, CapaSuelo);
        CrearCubo("Muro norte", zona, c + new Vector3(0f, 1.5f, m), new Vector3(LadoPrueba, 3f, 0.5f), muro);
        CrearCubo("Muro sur", zona, c + new Vector3(0f, 1.5f, -m), new Vector3(LadoPrueba, 3f, 0.5f), muro);
        CrearCubo("Muro este", zona, c + new Vector3(m, 1.5f, 0f), new Vector3(0.5f, 3f, LadoPrueba), muro);
        CrearCubo("Muro oeste", zona, c + new Vector3(-m, 1.5f, 0f), new Vector3(0.5f, 3f, LadoPrueba), muro);
        CrearVueltaAlLaboratorio(raiz, new Vector3(110f, 0f, 92f), EntradaTesoros);
        CrearTexto("Tesoros: uno por capa de requisito", zona, new Vector3(110f, 4.2f, 96f), 90f);
        CrearTexto("Quita y pon habilidades en Magia ▸ Habilidades · el registro, en Start ▸ Objetos", zona, new Vector3(110f, 3.5f, 96f), 90f);

        var mapa = MapaDelTesoroDelLab();
        const float z = 104f;
        var t1 = CrearTesoro("Tesoro 1 · sin requisito", raiz, new Vector3(112f, 0f, z), 180f, "LAB_TESORO_1", Tesoro.Forma.Cofre);
        Recompensas(t1, new Premio(PickupEffectType.Currency, RutaEsencia, 50), new Premio(PickupEffectType.AddToInventory, RutaMapaDelTesoroLab, 1));
        CrearTexto("1. Sin requisito (+ mapa)", zona, new Vector3(112f, 2.4f, z - 1.8f), 0f);

        var t2 = CrearTesoro("Tesoro 2 · nadar", raiz, new Vector3(118f, 0f, z), 180f, "LAB_TESORO_2", Tesoro.Forma.Cofre);
        Requisito(t2, habilidades: new[] { AbilityKey.Swim });
        Recompensas(t2, new Premio(PickupEffectType.AddToInventory, "Assets/_ITEMS/IT_PocionVida.asset", 2));
        CrearTexto("2. Habilidad: nadar", zona, new Vector3(118f, 2.4f, z - 1.8f), 0f);

        var t3 = CrearTesoro("Tesoro 3 · llave", raiz, new Vector3(124f, 0f, z), 180f, "LAB_TESORO_3", Tesoro.Forma.Cofre);
        Requisito(t3, objetos: new[] { "Assets/_ITEMS/IT_Key.asset" });
        Recompensas(t3, new Premio(PickupEffectType.AddToInventory, "Assets/_ITEMS/IT_AC04_BunnyEars.asset", 1));
        CrearTexto("3. Objeto: llave (placa de abajo)", zona, new Vector3(124f, 2.4f, z - 1.8f), 0f);
        var avisos = new List<string>();
        Placa("Placa: +1 Llave", "Assets/_ITEMS/IT_Key.asset", 1, raiz, new Vector3(124f, 0f, 96f),
              GetMaterial("Lab_PlacaDeLlave", new Color(0.85f, 0.70f, 0.25f)), avisos);

        var t4 = CrearTesoro("Tesoro 4 · vuelo", raiz, new Vector3(130f, 0f, z), 180f, "LAB_TESORO_4", Tesoro.Forma.Cofre);
        Requisito(t4, habilidades: new[] { AbilityKey.Fly });
        AsignarEnum(t4, "paginaDelGrimorio", (int)SpellId.EstrellaFugaz);
        CrearTexto("4. Vuelo (página del grimorio)", zona, new Vector3(130f, 2.4f, z - 1.8f), 0f);

        var t5 = CrearTesoro("Tesoro 5 · Amanecer", raiz, new Vector3(136f, 0f, z), 180f, "LAB_TESORO_5", Tesoro.Forma.Cofre);
        if (System.Enum.TryParse("Amanecer", out SpellId amanecer))
            Requisito(t5, hechizos: new[] { amanecer });
        else
        {
            // Hasta que exista el hechizo: un flag que nadie pone, con el mismo aviso.
            Requisito(t5, flags: new[] { "LAB_SIN_AMANECER_TODAVIA" });
            AsignarTexto(t5, "requisito.claveDeAviso", RequisitoDeAcceso.AvisoDeHechizo);
            avisos.Add("Tesoros: aún no existe SpellId.Amanecer; el tesoro 5 pide un flag imposible. Regenera el LAB cuando exista.");
        }
        Recompensas(t5, new Premio(PickupEffectType.Currency, RutaEsencia, 500));
        CrearTexto("5. Hechizo: Amanecer", zona, new Vector3(136f, 2.4f, z - 1.8f), 0f);

        // Punto de tesoro en la esquina sureste, marcado por el mapa del tesoro 1.
        var pie = new Vector3(135f, 0f, 83f);
        var punto = CrearTesoro("Punto de tesoro (mapa)", raiz, pie, 0f, "LAB_TESORO_PUNTO", Tesoro.Forma.Punto);
        Recompensas(punto, new Premio(PickupEffectType.Currency, RutaEsencia, 100));
        var marca = new GameObject("Marca del mapa del tesoro");
        marca.transform.SetParent(raiz);
        marca.transform.position = pie;
        var marcador = marca.AddComponent<MinimapMarker>();
        var soMarcador = new SerializedObject(marcador);
        soMarcador.FindProperty("color").colorValue = new Color(1f, 0.8f, 0.2f);
        soMarcador.ApplyModifiedPropertiesWithoutUndo();
        var marcaDelMapa = marca.AddComponent<MarcaDeMapaDelTesoro>();
        var haz = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        haz.name = "Haz del mapa";
        haz.transform.SetParent(marca.transform);
        haz.transform.SetPositionAndRotation(pie + Vector3.up * 10f, Quaternion.identity);
        haz.transform.localScale = new Vector3(0.4f, 10f, 0.4f);
        Object.DestroyImmediate(haz.GetComponent<Collider>());
        haz.GetComponent<Renderer>().sharedMaterial = GetMaterialTransparente("Lab_HazDelMapa", new Color(1f, 0.85f, 0.3f, 0.35f));
        haz.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var soMarca = new SerializedObject(marcaDelMapa);
        soMarca.FindProperty("mapa").objectReferenceValue = mapa;
        soMarca.FindProperty("tesoro").objectReferenceValue = punto;
        soMarca.FindProperty("radio").floatValue = 4f;
        soMarca.FindProperty("senalEnElMundo").objectReferenceValue = haz;
        soMarca.ApplyModifiedPropertiesWithoutUndo();
        CrearTexto("Con el mapa: haz de luz aquí y marca en el minimapa", zona, new Vector3(128f, 2.4f, 82f), 90f);

        foreach (var aviso in avisos) Debug.LogWarning("[CombatLab] " + aviso);
    }

    /// Cofre o punto de tesoro con su Interactable (trigger en la capa Interactable) y, en el cofre,
    /// el cuerpo sólido en la capa Floor (se puede pisar y no tapa la vista del interactuable).
    /// Un punto lleva un montículo con brillo o, si se da <paramref name="visualDelPunto"/>, ese objeto
    /// (pasa a ser hijo del tesoro y es lo que desaparece al cogerlo).
    internal static Tesoro CrearTesoro(string nombre, Transform padre, Vector3 pie, float giro, string id, Tesoro.Forma forma,
                                      string region = RegionLab, ModeloDeCofre modelo = ModeloDeCofre.Cajas,
                                      GameObject visualDelPunto = null)
    {
        var raiz = new GameObject(nombre);
        raiz.transform.SetParent(padre);
        raiz.transform.SetPositionAndRotation(pie, Quaternion.Euler(0f, giro, 0f));
        int capaInteractuable = LayerMask.NameToLayer("Interactable");
        if (capaInteractuable >= 0) raiz.layer = capaInteractuable;
        var deteccion = raiz.AddComponent<SphereCollider>();
        deteccion.isTrigger = true;
        deteccion.center = Vector3.up * 0.5f;
        deteccion.radius = 1.6f;
        raiz.AddComponent<Interactable>();
        var tesoro = raiz.AddComponent<Tesoro>();
        var so = new SerializedObject(tesoro);
        so.FindProperty("idManual").stringValue = id;
        so.FindProperty("region").stringValue = region;
        so.FindProperty("forma").enumValueIndex = (int)forma;

        if (forma == Tesoro.Forma.Cofre)
        {
            var tapa = VisualDeCofre(raiz.transform, modelo);
            so.FindProperty("tapa").objectReferenceValue = tapa;
        }
        else if (visualDelPunto != null)
        {
            visualDelPunto.transform.SetParent(raiz.transform, true);
            var ocultar = so.FindProperty("ocultarAlAbrir");
            ocultar.arraySize = 1;
            ocultar.GetArrayElementAtIndex(0).objectReferenceValue = visualDelPunto;
            so.FindProperty("sfxAlAbrir").stringValue = "Coin";
        }
        else
        {
            var monticulo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            monticulo.name = "Montículo";
            monticulo.transform.SetParent(raiz.transform, false);
            monticulo.transform.localPosition = Vector3.zero;
            monticulo.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
            Object.DestroyImmediate(monticulo.GetComponent<Collider>());
            monticulo.GetComponent<Renderer>().sharedMaterial = GetMaterial("Lab_Monticulo", new Color(0.36f, 0.27f, 0.18f));
            var brillo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            brillo.name = "Brillo";
            brillo.transform.SetParent(raiz.transform, false);
            brillo.transform.localPosition = Vector3.up * 0.35f;
            brillo.transform.localScale = Vector3.one * 0.25f;
            Object.DestroyImmediate(brillo.GetComponent<Collider>());
            brillo.GetComponent<Renderer>().sharedMaterial = GetMaterial("Lab_EstrellaDePrueba", new Color(1f, 0.85f, 0.35f), "Universal Render Pipeline/Unlit");
            var ocultar = so.FindProperty("ocultarAlAbrir");
            ocultar.arraySize = 2;
            ocultar.GetArrayElementAtIndex(0).objectReferenceValue = monticulo;
            ocultar.GetArrayElementAtIndex(1).objectReferenceValue = brillo;
            so.FindProperty("sfxAlAbrir").stringValue = "Coin";
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return tesoro;
    }

    /// Aspecto de un cofre: el modelo de cada uso (los tesoros del mundo, la colección de Oliver…).
    internal enum ModeloDeCofre
    {
        /// <summary>Cofre morado del pack de cajas (tesoro normal).</summary>
        Cajas,
        /// <summary>Baúl de madera del pack Kingdom, el mismo que las casas del pueblo (colección de Oliver).</summary>
        BaulDeMadera
    }

    private const string NombreDelVisual = "Cofre";
    private const string NombreDelSolido = "Cuerpo sólido";

    /// Cambia el aspecto de un cofre ya colocado sin tocar su sitio, su giro ni su recompensa.
    /// Devuelve false si ya tenía ese modelo.
    internal static bool CambiarModeloDeCofre(Tesoro tesoro, ModeloDeCofre modelo)
    {
        if (tesoro == null || tesoro.Tipo != Tesoro.Forma.Cofre) return false;
        var raiz = tesoro.transform;
        var actual = raiz.Find(NombreDelVisual + "/Cuerpo")?.GetComponent<MeshFilter>();
        var piezas = PiezasDelModelo(modelo);
        if (actual != null && piezas.cuerpo != null && actual.sharedMesh == piezas.cuerpo.GetComponent<MeshFilter>().sharedMesh) return false;

        for (int i = raiz.childCount - 1; i >= 0; i--)
        {
            var hijo = raiz.GetChild(i);
            if (hijo.name == NombreDelVisual || hijo.name == NombreDelSolido) Object.DestroyImmediate(hijo.gameObject);
        }
        var so = new SerializedObject(tesoro);
        so.FindProperty("tapa").objectReferenceValue = VisualDeCofre(raiz, modelo);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tesoro);
        return true;
    }

    /// Mallas de origen (cuerpo y tapa), material forzado y escala de cada modelo de cofre. Cuerpo
    /// y tapa salen null si falta el asset.
    private static (Transform cuerpo, Transform tapa, Material material, float escala) PiezasDelModelo(ModeloDeCofre modelo)
    {
        GameObject fuente;
        string cuerpoEmpieza, tapaEmpieza;
        Material material = null;
        float escala;
        switch (modelo)
        {
            case ModeloDeCofre.BaulDeMadera:
                fuente = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Chest01.prefab");
                cuerpoEmpieza = "Chest01_a01";
                tapaEmpieza = "Chest01_a02";
                escala = 1.3f;
                break;
            default:
                const string Carpeta = "Assets/Art/Items/Cartoony Fantasy Lowpoly Crates Package/";
                fuente = AssetDatabase.LoadAssetAtPath<GameObject>(Carpeta + "Meshes/Chest1.fbx");
                material = AssetDatabase.LoadAssetAtPath<Material>(Carpeta + "Materials/ChestMaterial.mat");
                cuerpoEmpieza = "chest body";
                tapaEmpieza = "lid";
                escala = 1.4f;
                break;
        }

        Transform cuerpo = null, tapa = null;
        if (fuente != null)
            foreach (var t in fuente.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<MeshFilter>() == null) continue;
                if (t.name.StartsWith(cuerpoEmpieza)) cuerpo = t;
                else if (t.name.StartsWith(tapaEmpieza)) tapa = t;
            }
        if (cuerpo == null || tapa == null) cuerpo = tapa = null;
        return (cuerpo, tapa, material, escala);
    }

    /// Cuerpo y tapa del modelo elegido (o dos cubos si falta el asset), con su cuerpo sólido en
    /// la capa Floor. Devuelve la tapa, con el pivote en la bisagra.
    private static Transform VisualDeCofre(Transform raiz, ModeloDeCofre modelo)
    {
        var piezas = PiezasDelModelo(modelo);
        Transform cuerpoModelo = piezas.cuerpo, tapaModelo = piezas.tapa;
        Material material = piezas.material;
        float escala = piezas.escala;

        var visual = new GameObject(NombreDelVisual).transform;
        visual.SetParent(raiz, false);
        visual.localScale = Vector3.one * escala;
        Transform tapa, cuerpo;
        if (cuerpoModelo != null && tapaModelo != null)
        {
            // Los modelos traen el pivote donde quiso su autor (el del pack de cajas, a 6,6 m del
            // origen; el baúl, en la bisagra): se ancla el cuerpo centrado y apoyado en el suelo, y
            // la tapa conserva su sitio respecto al cuerpo, que es donde está su bisagra.
            var anclaje = AnclajeDelCuerpo(cuerpoModelo);
            var separacion = tapaModelo.localPosition - cuerpoModelo.localPosition;
            cuerpo = CopiarMalla("Cuerpo", visual, cuerpoModelo, material, anclaje).transform;
            tapa = CopiarMalla("Tapa", visual, tapaModelo, material, anclaje + separacion).transform;
        }
        else
        {
            var madera = GetMaterial("Lab_Madera", new Color(0.45f, 0.28f, 0.15f));
            var cubo = CrearCubo("Cuerpo", visual, Vector3.zero, Vector3.one, madera, estatico: false);
            cubo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            cubo.transform.localScale = new Vector3(0.8f, 0.4f, 0.55f);
            Object.DestroyImmediate(cubo.GetComponent<Collider>());
            cuerpo = cubo.transform;
            tapa = new GameObject("Tapa").transform;
            tapa.SetParent(visual, false);
            tapa.localPosition = new Vector3(0f, 0.4f, -0.275f);
            var hoja = CrearCubo("Hoja de la tapa", tapa, Vector3.zero, Vector3.one, madera, estatico: false);
            hoja.transform.localPosition = new Vector3(0f, 0.08f, 0.275f);
            hoja.transform.localScale = new Vector3(0.82f, 0.16f, 0.57f);
            Object.DestroyImmediate(hoja.GetComponent<Collider>());
        }

        // Cuerpo sólido en la capa Floor, a la medida del cuerpo del modelo.
        var solido = new GameObject(NombreDelSolido);
        solido.transform.SetParent(raiz, false);
        int capaSuelo = LayerMask.NameToLayer(CapaSuelo);
        if (capaSuelo >= 0) solido.layer = capaSuelo;
        var caja = solido.AddComponent<BoxCollider>();
        var limites = LimitesEn(raiz, cuerpo);
        caja.center = limites.center;
        caja.size = limites.size;
        return tapa;
    }

    /// Caja que ocupa la malla de <paramref name="objeto"/> en el espacio local de <paramref name="raiz"/>.
    private static Bounds LimitesEn(Transform raiz, Transform objeto)
    {
        var malla = objeto.GetComponent<MeshFilter>()?.sharedMesh;
        var b = malla != null ? malla.bounds : new Bounds(Vector3.zero, Vector3.one);
        var limites = new Bounds(raiz.InverseTransformPoint(objeto.TransformPoint(b.min)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var esquina = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            limites.Encapsulate(raiz.InverseTransformPoint(objeto.TransformPoint(esquina)));
        }
        return limites;
    }

    /// Posición local del cuerpo del cofre para que quede centrado en su ancla y apoyado en el suelo.
    private static Vector3 AnclajeDelCuerpo(Transform cuerpoModelo)
    {
        var malla = cuerpoModelo.GetComponent<MeshFilter>()?.sharedMesh;
        if (malla == null) return Vector3.zero;
        var b = malla.bounds;
        return new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    private static GameObject CopiarMalla(string nombre, Transform padre, Transform origen, Material material,
                                          Vector3 posicionLocal)
    {
        var copia = new GameObject(nombre);
        copia.transform.SetParent(padre, false);
        copia.transform.localPosition = posicionLocal;
        copia.transform.localRotation = origen.localRotation;
        copia.transform.localScale = origen.localScale;
        copia.AddComponent<MeshFilter>().sharedMesh = origen.GetComponent<MeshFilter>().sharedMesh;
        var r = copia.AddComponent<MeshRenderer>();
        var rOrigen = origen.GetComponent<MeshRenderer>();
        if (material != null) r.sharedMaterial = material;
        else if (rOrigen != null) r.sharedMaterials = rOrigen.sharedMaterials;
        return copia;
    }

    internal static void Recompensas(Tesoro tesoro, params Premio[] premios)
    {
        var so = new SerializedObject(tesoro);
        var lista = so.FindProperty("recompensas");
        lista.arraySize = 0;
        foreach (var premio in premios)
        {
            var objeto = AssetDatabase.LoadAssetAtPath<ItemData>(premio.ruta);
            if (objeto == null) { Debug.LogWarning($"[CombatLab] Tesoros: falta {premio.ruta}; {tesoro.name} va sin ese premio."); continue; }
            int i = lista.arraySize;
            lista.arraySize = i + 1;
            var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("effectType").enumValueIndex = (int)premio.tipo;
            e.FindPropertyRelative("item").objectReferenceValue = objeto;
            e.FindPropertyRelative("quantity").intValue = premio.cantidad;
            e.FindPropertyRelative("amount").floatValue = 0f;
            e.FindPropertyRelative("consumeEvenIfNoChange").boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Requisito(Tesoro tesoro, AbilityKey[] habilidades = null, string[] objetos = null,
                                  SpellId[] hechizos = null, string[] flags = null)
    {
        var so = new SerializedObject(tesoro);
        if (habilidades != null)
        {
            var p = so.FindProperty("requisito.habilidades");
            p.arraySize = habilidades.Length;
            for (int i = 0; i < habilidades.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)habilidades[i];
        }
        if (objetos != null)
        {
            var p = so.FindProperty("requisito.objetos");
            p.arraySize = objetos.Length;
            for (int i = 0; i < objetos.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>(objetos[i]);
        }
        if (hechizos != null)
        {
            var p = so.FindProperty("requisito.hechizos");
            p.arraySize = hechizos.Length;
            for (int i = 0; i < hechizos.Length; i++) p.GetArrayElementAtIndex(i).intValue = (int)hechizos[i];
        }
        if (flags != null)
        {
            var p = so.FindProperty("requisito.flags");
            p.arraySize = flags.Length;
            for (int i = 0; i < flags.Length; i++) p.GetArrayElementAtIndex(i).stringValue = flags[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsignarEnum(Object componente, string campo, int valor)
    {
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[CombatLab] {componente.GetType().Name} no tiene el campo '{campo}'."); return; }
        p.intValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Objeto «Mapa del tesoro (LAB)»: se crea si no existe (el registro de objetos lo recoge solo).
    private static ItemData MapaDelTesoroDelLab()
    {
        var mapa = AssetDatabase.LoadAssetAtPath<ItemData>(RutaMapaDelTesoroLab);
        if (mapa != null) return mapa;
        mapa = ScriptableObject.CreateInstance<ItemData>();
        mapa.itemId = "mapa_tesoro_lab";
        mapa.displayNameId = "TESORO_MAPA_LAB_NOMBRE";
        mapa.displayName = "Mapa del tesoro (LAB)";
        mapa.useDescriptionId = "TESORO_MAPA_LAB_DESC";
        mapa.useDescription = "Marca dónde hay algo enterrado en la sala de tesoros del LAB.";
        mapa.usageKind = ItemData.ItemUsageKind.Quest;
        mapa.buyPrice = 0;
        mapa.sellValue = 0;
        AssetDatabase.CreateAsset(mapa, RutaMapaDelTesoroLab);
        return mapa;
    }
}
