using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Crea o rehace los prefabs de los enemigos del mundo abierto con los modelos que ya hay en el
/// proyecto: Fantasma, Beholder, Mímico, Imp ladrón y Roca (INC-722). Cada prefab lleva su IA
/// (base <see cref="EnemigoDelMundo"/>), Damageable, OrbDropper (Esencia), Targetable y la señal
/// ENEMIGO_DERROTADO_&lt;NOMBRE&gt; para contratos de caza. Rehacerlo conserva el GUID de cada
/// prefab (las instancias de las escenas siguen enlazadas). Lo que falte se avisa y se salta.
/// </summary>
public static class EnemigosDelMundoBuilder
{
    public const string Carpeta = "Assets/Prefabs/Enemy/Mundo";
    public const string RutaFantasma = Carpeta + "/Fantasma.prefab";
    public const string RutaBeholder = Carpeta + "/Beholder.prefab";
    public const string RutaMimico = Carpeta + "/Mimico.prefab";
    public const string RutaImp = Carpeta + "/ImpLadron.prefab";
    public const string RutaRoca = Carpeta + "/Roca.prefab";
    private const string CarpetaMateriales = Carpeta + "/Materiales";

    private const string Personajes = "Assets/Art/Characters/";
    private const string ModeloFantasma = Personajes + "Enemies/Monster_Ghosts_FREE/Prefabs/Little_Ghost_ZOMbi (1).prefab";
    private const string ModeloBeholder = Personajes + "RPGMonsterPartnersPBRPolyart/Prefabs/Character/BeholderPBRDefault.prefab";
    private const string ModeloMimico = Personajes + "RPGMonsterPartnersPBRPolyart/Prefabs/Character/ChestMonsterPBRDefault.prefab";
    private const string ModeloImp = Personajes + "Enemies/Imp Demon Cute Series/Prefabs/Imp Demon.prefab";
    private const string ModeloRoca = Personajes + "Enemies/Mini Legion Rock Golem PBR HP Polyart/Prefabs/PBR_Rock.prefab";

    private const string Rayo = "Assets/_ENEMY_SPELLS/Prefabs/Plasma Sphere (1).prefab";
    private const string OrbeEsencia = "Assets/Prefabs/Orbes/Orbe_Esencia.prefab";
    private const string Esencia = "Assets/_ITEMS/IT_Esencia.asset";
    private const string Monedas = "Assets/_ITEMS/IT_Coin.asset";

    private const string Vfx = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string VfxLuz = Vfx + "Hits and explosions/Holy hit.prefab";
    private const string VfxPiedras = Vfx + "Hits and explosions/Stones hit.prefab";
    private const string VfxAturdido = Vfx + "Character auras/Star aura.prefab";
    private const string VfxRebote = Vfx + "Sparks/Sparks explode white.prefab";
    private const string VfxSinEfecto = Vfx + "Sparks/Sparks flashing white.prefab";
    private const string VfxRobo = Vfx + "Sparks/Sparks explode yellow.prefab";
    private const string VfxBotin = Vfx + "Sparks/Sparks flashing yellow.prefab";
    private const string VfxHumo = Vfx + "Smoke effects/Smoke puff.prefab";
    private const string VfxPolvo = Vfx + "Smoke effects/Dust puff.prefab";
    private const string VfxPolvoSuelo = Vfx + "Smoke effects/Dust ground.prefab";

    [MenuItem("El Sendero/Combate/Enemigos del mundo: crear o rehacer prefabs")]
    public static void CrearTodos()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Enemigos del mundo", "Sal de Play antes de crear los prefabs.", "Aceptar");
            return;
        }
        var avisos = CrearPrefabs();
        EditorUtility.DisplayDialog("Enemigos del mundo",
            avisos.Count == 0
                ? "Prefabs creados en " + Carpeta + ": Fantasma, Beholder, Mímico, Imp ladrón y Roca."
                : "Prefabs creados con avisos:\n- " + string.Join("\n- ", avisos),
            "Aceptar");
    }

    /// <summary>Crea o rehace los cinco prefabs. Devuelve los avisos (lo que falta).</summary>
    public static List<string> CrearPrefabs()
    {
        var avisos = new List<string>();
        Carpetas(CarpetaMateriales);
        Crear(RutaFantasma, ConstruirFantasma, avisos);
        Crear(RutaBeholder, ConstruirBeholder, avisos);
        Crear(RutaMimico, ConstruirMimico, avisos);
        Crear(RutaImp, ConstruirImp, avisos);
        Crear(RutaRoca, ConstruirRoca, avisos);
        AssetDatabase.SaveAssets();
        return avisos;
    }

    private static void Crear(string ruta, Func<List<string>, GameObject> construir, List<string> avisos)
    {
        GameObject raiz = null;
        try
        {
            raiz = construir(avisos);
            if (raiz != null) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        catch (Exception e)
        {
            avisos.Add($"{System.IO.Path.GetFileNameWithoutExtension(ruta)}: {e.Message}");
            Debug.LogException(e);
        }
        finally
        {
            if (raiz != null) Object.DestroyImmediate(raiz);
        }
    }

    // ── Fantasma ─────────────────────────────────────────────────────────

    private static GameObject ConstruirFantasma(List<string> avisos)
    {
        var raiz = Raiz("Fantasma", "Enemy");
        var modelo = Modelo(ModeloFantasma, raiz.transform, 1.3f, avisos);
        if (modelo == null) { Object.DestroyImmediate(raiz); return null; }

        var capsula = raiz.AddComponent<CapsuleCollider>();
        capsula.center = new Vector3(0f, 0.65f, 0f);
        capsula.height = 1.3f;
        capsula.radius = 0.45f;
        Cinematico(raiz);
        Vida(raiz, 40f);

        var luz = new GameObject("Luz al revelarse").AddComponent<Light>();
        luz.transform.SetParent(raiz.transform, false);
        luz.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        luz.type = LightType.Point;
        luz.range = 4f;
        luz.intensity = 2.5f;
        luz.color = new Color(1f, 0.95f, 0.7f);
        luz.shadows = LightShadows.None;
        luz.enabled = false;

        var revelado = raiz.AddComponent<ReveladoPorLuz>();
        Asignar(revelado, "vfxAlRevelar", Cargar<GameObject>(VfxLuz, avisos));
        Asignar(revelado, "vfxGolpeSinEfecto", Cargar<GameObject>(VfxSinEfecto, avisos));
        Asignar(revelado, "alturaDelCentro", 0.7f);

        var ia = raiz.AddComponent<Fantasma>();
        Asignar(ia, "cuerpo", modelo.GetComponentsInChildren<Renderer>(true));
        Asignar(ia, "materialIntangible", MaterialIntangible(modelo));
        Asignar(ia, "luzAlRevelar", luz);
        Asignar(ia, "suelo", Mascara("Default", "Floor"));
        Asignar(ia, "vfxAparecer", Cargar<GameObject>(VfxHumo, avisos));
        Asignar(ia, "animacionDeMuerte", "");
        Asignar(ia, "vfxAlDesaparecer", Cargar<GameObject>(VfxHumo, avisos));
        Asignar(ia, "alcanceDeDeteccion", 10f);
        Asignar(ia, "alcanceDePerdida", 18f);
        Comunes(raiz, "", "ENEMIGO_DERROTADO_FANTASMA", 6f);
        return raiz;
    }

    /// Material translúcido de «intangible» con la textura del fantasma.
    private static Material MaterialIntangible(GameObject modelo)
    {
        string ruta = CarpetaMateriales + "/Fantasma_Intangible.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Fantasma_Intangible" };
            AssetDatabase.CreateAsset(mat, ruta);
        }
        Texture textura = null;
        foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || textura != null) continue;
                if (m.HasProperty("_BaseMap")) textura = m.GetTexture("_BaseMap");
                if (textura == null && m.HasProperty("_MainTex")) textura = m.GetTexture("_MainTex");
            }
        mat.SetTexture("_BaseMap", textura);
        mat.SetColor("_BaseColor", new Color(0.75f, 0.9f, 1f, 0.3f));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.15f, 0.25f, 0.35f));
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Beholder ─────────────────────────────────────────────────────────

    private static GameObject ConstruirBeholder(List<string> avisos)
    {
        var raiz = Raiz("Beholder", "Enemy");
        var modelo = Modelo(ModeloBeholder, raiz.transform, 1.9f, avisos);
        if (modelo == null) { Object.DestroyImmediate(raiz); return null; }
        var limites = Limites(modelo);

        var esfera = raiz.AddComponent<SphereCollider>();
        esfera.center = raiz.transform.InverseTransformPoint(limites.center);
        esfera.radius = Mathf.Max(limites.extents.x, limites.extents.y, limites.extents.z) * 0.8f;
        Cinematico(raiz);
        Vida(raiz, 60f);

        var boca = new GameObject("Boca").transform;
        boca.SetParent(raiz.transform, false);
        boca.localPosition = new Vector3(0f, esfera.center.y, limites.extents.z + 0.3f);

        var carga = raiz.AddComponent<CargaDeDisparo>();
        Asignar(carga, "segundosDeCarga", 0.8f);

        var ia = raiz.AddComponent<Beholder>();
        Asignar(ia, "proyectil", Cargar<GameObject>(Rayo, avisos));
        Asignar(ia, "boca", boca);
        Asignar(ia, "vfxAturdido", Cargar<GameObject>(VfxAturdido, avisos));
        Asignar(ia, "vfxRebote", Cargar<GameObject>(VfxRebote, avisos));
        Asignar(ia, "vfxAlDesaparecer", Cargar<GameObject>(VfxHumo, avisos));
        Asignar(ia, "alcanceDeDeteccion", 16f);
        Asignar(ia, "alcanceDePerdida", 22f);
        Comunes(raiz, "", "ENEMIGO_DERROTADO_BEHOLDER", 10f);
        return raiz;
    }

    // ── Mímico ───────────────────────────────────────────────────────────

    private static GameObject ConstruirMimico(List<string> avisos)
    {
        var raiz = Raiz("Mimico", "Interactable");
        var modelo = Modelo(ModeloMimico, raiz.transform, 1.1f, avisos);
        if (modelo == null) { Object.DestroyImmediate(raiz); return null; }
        var limites = Limites(modelo);

        var caja = raiz.AddComponent<BoxCollider>();
        caja.center = raiz.transform.InverseTransformPoint(limites.center);
        caja.size = limites.size * 0.9f;
        var zona = raiz.AddComponent<SphereCollider>();
        zona.isTrigger = true;
        zona.center = caja.center;
        zona.radius = 1.6f;
        Cinematico(raiz);
        Vida(raiz, 70f);
        raiz.AddComponent<Interactable>();

        var agente = raiz.AddComponent<NavMeshAgent>();
        agente.radius = Mathf.Max(0.4f, Mathf.Max(limites.extents.x, limites.extents.z));
        agente.height = Mathf.Max(0.8f, limites.size.y);
        agente.speed = 3.4f;
        agente.angularSpeed = 540f;
        agente.acceleration = 16f;

        var ia = raiz.AddComponent<Mimico>();
        Asignar(ia, "zonaDeInteraccion", zona);
        Asignar(ia, "vfxDespertar", Cargar<GameObject>(VfxPolvo, avisos));
        Asignar(ia, "vfxAlDesaparecer", Cargar<GameObject>(VfxHumo, avisos));
        Asignar(ia, "alcanceDeDeteccion", 12f);
        Asignar(ia, "alcanceDePerdida", 22f);
        Comunes(raiz, "elite", "ENEMIGO_DERROTADO_MIMICO", 6f);
        return raiz;
    }

    // ── Imp ladrón ───────────────────────────────────────────────────────

    private static GameObject ConstruirImp(List<string> avisos)
    {
        var raiz = Raiz("ImpLadron", "Enemy");
        var modelo = Modelo(ModeloImp, raiz.transform, 1f, avisos);
        if (modelo == null) { Object.DestroyImmediate(raiz); return null; }
        var limites = Limites(modelo);

        var capsula = raiz.AddComponent<CapsuleCollider>();
        capsula.center = raiz.transform.InverseTransformPoint(limites.center);
        capsula.height = limites.size.y;
        capsula.radius = Mathf.Max(0.3f, Mathf.Min(limites.extents.x, limites.extents.z));
        Cinematico(raiz);
        Vida(raiz, 25f);

        var ia = raiz.AddComponent<ImpLadron>();
        var esencia = Cargar<ItemData>(Esencia, avisos);
        var monedas = Cargar<ItemData>(Monedas, avisos);
        var botin = new List<ItemData>();
        if (esencia != null) botin.Add(esencia);
        if (monedas != null) botin.Add(monedas);
        Asignar(ia, "botin", botin.ToArray());
        Asignar(ia, "monedaDelOrbe", esencia);
        Asignar(ia, "orbeDelBotin", Cargar<GameObject>(OrbeEsencia, avisos));
        Asignar(ia, "vfxRobo", Cargar<GameObject>(VfxRobo, avisos));
        Asignar(ia, "vfxBotin", Cargar<GameObject>(VfxBotin, avisos));
        Asignar(ia, "suelo", Mascara("Default", "Floor"));
        Asignar(ia, "obstaculos", Mascara("Default", "Floor", "Climb", "Obstacle"));
        Asignar(ia, "vfxAlDesaparecer", Cargar<GameObject>(VfxHumo, avisos));
        Asignar(ia, "alcanceDeDeteccion", 12f);
        Asignar(ia, "alcanceDePerdida", 20f);
        Comunes(raiz, "", "ENEMIGO_DERROTADO_IMP", 6f);
        return raiz;
    }

    // ── Roca ─────────────────────────────────────────────────────────────

    private static GameObject ConstruirRoca(List<string> avisos)
    {
        const float radio = 0.8f;
        var original = Cargar<GameObject>(ModeloRoca, avisos);
        if (original == null) return null;
        var filtro = original.GetComponentInChildren<MeshFilter>(true);
        var malla = original.GetComponentInChildren<MeshRenderer>(true);
        if (filtro == null || filtro.sharedMesh == null || malla == null)
            throw new InvalidOperationException("El modelo de la roca no tiene malla: " + ModeloRoca);

        var raiz = Raiz("Roca", "Enemy");
        var cuerpo = new GameObject("Cuerpo").transform;
        cuerpo.SetParent(raiz.transform, false);
        cuerpo.localPosition = new Vector3(0f, radio, 0f);
        var piedra = new GameObject("Piedra");
        piedra.layer = raiz.layer;
        piedra.transform.SetParent(cuerpo, false);
        piedra.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
        piedra.AddComponent<MeshRenderer>().sharedMaterials = malla.sharedMaterials;
        // Centra la malla en el cuerpo y la lleva al diámetro de la roca.
        var b = filtro.sharedMesh.bounds;
        float escala = radio * 2f / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
        piedra.transform.localScale = Vector3.one * escala;
        piedra.transform.localPosition = -b.center * escala;

        var esfera = raiz.AddComponent<SphereCollider>();
        esfera.center = new Vector3(0f, radio, 0f);
        esfera.radius = radio;
        Cinematico(raiz);
        Vida(raiz, 80f);

        var ia = raiz.AddComponent<RocaEmbestidora>();
        Asignar(ia, "cuerpo", cuerpo);
        Asignar(ia, "radio", radio);
        Asignar(ia, "suelo", Mascara("Default", "Floor"));
        Asignar(ia, "choques", Mascara("Default", "Player", "Enemy", "Interactable", "Floor", "Climb", "Obstacle"));
        Asignar(ia, "vfxAturdida", Cargar<GameObject>(VfxAturdido, avisos));
        Asignar(ia, "vfxChoque", Cargar<GameObject>(VfxPiedras, avisos));
        Asignar(ia, "vfxPolvo", Cargar<GameObject>(VfxPolvoSuelo, avisos));
        Asignar(ia, "vfxRebote", Cargar<GameObject>(VfxRebote, avisos));
        Asignar(ia, "animacionDeMuerte", "");
        Asignar(ia, "vfxAlDesaparecer", Cargar<GameObject>(VfxPiedras, avisos));
        Asignar(ia, "alcanceDeDeteccion", 12f);
        Asignar(ia, "alcanceDePerdida", 20f);
        Comunes(raiz, "", "ENEMIGO_DERROTADO_ROCA", 8f);
        return raiz;
    }

    // ── Piezas comunes ───────────────────────────────────────────────────

    private static GameObject Raiz(string nombre, string capa)
    {
        var raiz = new GameObject(nombre);
        int indice = LayerMask.NameToLayer(capa);
        if (indice >= 0) raiz.layer = indice;
        return raiz;
    }

    /// Copia suelta del modelo (sin física ni scripts del pack), a la altura pedida, con los pies
    /// en el pivote de la raíz y centrado en planta.
    private static GameObject Modelo(string ruta, Transform padre, float altura, List<string> avisos)
    {
        var prefab = Cargar<GameObject>(ruta, avisos);
        if (prefab == null) return null;
        var modelo = (GameObject)Object.Instantiate(prefab, padre);
        modelo.name = "Modelo";
        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.identity;
        foreach (var mb in modelo.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        foreach (var rb in modelo.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var col in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = padre.gameObject.layer;
        // La IA mueve la raíz; la animación no desplaza el modelo.
        foreach (var animador in modelo.GetComponentsInChildren<Animator>(true)) animador.applyRootMotion = false;

        var limites = Limites(modelo);
        if (limites.size.y > 0.0001f)
        {
            modelo.transform.localScale *= altura / limites.size.y;
            limites = Limites(modelo);
            Vector3 base_ = padre.InverseTransformPoint(new Vector3(limites.center.x, limites.min.y, limites.center.z));
            modelo.transform.localPosition -= base_;
        }
        return modelo;
    }

    private static Bounds Limites(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void Cinematico(GameObject raiz)
    {
        var rb = raiz.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    private static void Vida(GameObject raiz, float maxima)
    {
        var vida = raiz.AddComponent<Damageable>();
        Asignar(vida, "maxHealth", maxima);
        Asignar(vida, "destroyOnDeath", false);
    }

    /// Targetable, drops de Esencia (perfil) y señal al morir para contratos de caza.
    private static void Comunes(GameObject raiz, string perfil, string senal, float radioDeApuntado)
    {
        if (!raiz.TryGetComponent(out Targetable apuntable)) apuntable = raiz.AddComponent<Targetable>();
        apuntable.targetingRadius = radioDeApuntado;
        var drops = raiz.AddComponent<OrbDropper>();
        Asignar(drops, "_profileId", perfil);
        var emisor = raiz.AddComponent<SignalEmitter>();
        emisor.eventKey = senal;
        emisor.trigger = SignalEmitter.TriggerType.OnEnemyDied;
        emisor.once = true;
        emisor.onlyIfListening = true;
    }

    private static int Mascara(params string[] capas) => LayerMask.GetMask(capas);

    private static T Cargar<T>(string ruta, List<string> avisos) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null && avisos != null && !avisos.Contains("Falta " + ruta)) avisos.Add("Falta " + ruta);
        return a;
    }

    private static void Carpetas(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpetas(padre);
        AssetDatabase.CreateFolder(padre, System.IO.Path.GetFileName(ruta));
    }

    private static void Asignar(Object componente, string campo, object valor)
    {
        var so = new SerializedObject(componente);
        var p = so.FindProperty(campo);
        if (p == null)
        {
            Debug.LogWarning($"[Enemigos del mundo] {componente.GetType().Name} no tiene el campo '{campo}'.");
            return;
        }
        switch (valor)
        {
            case null: p.objectReferenceValue = null; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case string s: p.stringValue = s; break;
            case Object o: p.objectReferenceValue = o; break;
            case Object[] lista:
                p.arraySize = lista.Length;
                for (int k = 0; k < lista.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = lista[k];
                break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
