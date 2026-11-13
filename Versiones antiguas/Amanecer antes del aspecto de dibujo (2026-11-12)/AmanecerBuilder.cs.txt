using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using static VfxProcedural;
using Random = System.Random;

/// <summary>
/// Monta Amanecer, el hechizo final de Will (GDD § 6.2; INC-724, INC-760). Idempotente.
/// <list type="bullet">
/// <item>Efectos propios espectaculares con VfxProcedural (dorado y blanco de Will con hilos naranja y crema de
/// Estela; runa solar en el suelo, pilar solar celestial de impacto, luz dinámica, sacudida sísmica,
/// destello de pantalla, micro-hitstop y grietas doradas de apertura de protección).</item>
/// <item>Assets/_SPELLS/Amanecer.asset: especial de Luz de Will, combo X A B Y, sin equipar, con
/// «abre la protección». Daño y maná solo se ponen al crearlo (se ajustan en el LAB).</item>
/// <item>Envoltorio del proyectil con TrailRenderer y luz propia en Assets/_SPELLS/Prefabs/, alta en la SpellLibrary,
/// icono del grimorio y sonidos (SonidosDeHechizos).</item>
/// </list>
/// Termina comprobando que ninguna secuencia de combo es el principio de otra.
/// </summary>
public static class AmanecerBuilder
{
    private const string RutaHechizo = "Assets/_SPELLS/Amanecer.asset";
    private const string RutaEnvoltorio = "Assets/_SPELLS/Prefabs/Amanecer.prefab";
    private const string RutaBiblioteca = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";
    private const string RutaIcono = "Assets/Art/UI/Attacks/Grimorio/amanecer.png";
    private const string CarpetaTexturas = VfxPropiosDeHechizosBuilder.Carpeta + "/Texturas";
    private const string CarpetaMateriales = VfxPropiosDeHechizosBuilder.Carpeta + "/Materiales";

    public const string VfxProyectil = "Amanecer";
    public const string VfxCarga = "Amanecer_Carga";
    public const string VfxImpacto = "Amanecer_Impacto";
    public const string VfxProteccionAbierta = "Amanecer_ProteccionAbierta";

    // Valores de partida (GDD § 6.2): se ajustan en el LAB, el menú no los pisa después.
    private const float DanoInicial = 120f;
    private const float ManaInicial = 40f;
    private const float SegundosDeApertura = 6f;
    private static readonly ComboButton[] Secuencia = { ComboButton.X, ComboButton.A, ComboButton.B, ComboButton.Y };

    // Paleta.
    private static readonly Color Dorado = new Color(1f, 0.80f, 0.35f);
    private static readonly Color DoradoPalido = new Color(1f, 0.90f, 0.62f);
    private static readonly Color Naranja = new Color(1f, 0.52f, 0.16f);
    private static readonly Color Crema = new Color(1f, 0.95f, 0.82f);

    [MenuItem("El Sendero/Magia/Amanecer · crear o actualizar (INC-724)")]
    public static void Montar()
    {
        var log = new StringBuilder();
        var avisos = new List<string>();

        var t = ConstruirEfectos(log);
        AssetDatabase.SaveAssets();

        var hechizo = CrearOActualizarHechizo(log, avisos, t);
        if (hechizo != null)
        {
            DarDeAltaEnLaBiblioteca(hechizo, log, avisos);
            EditorUtility.SetDirty(hechizo);
        }
        AssetDatabase.SaveAssets();

        SonidosDeHechizos.Aplicar();
        ComboWiring.ValidateMenu();

        var final = new StringBuilder("=== Amanecer (INC-724, INC-760) ===\n").Append(log);
        if (avisos.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {avisos.Count} aviso(s): ---");
            foreach (var a in avisos) final.AppendLine("  • " + a);
            Debug.LogWarning(final.ToString());
        }
    }

    // ── Hechizo ───────────────────────────────────────────────────────────

    private static MagicSpellSO CrearOActualizarHechizo(StringBuilder log, List<string> avisos, Texturas t)
    {
        var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(RutaHechizo);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<MagicSpellSO>();
            s.damage = DanoInicial;
            s.manaCost = ManaInicial;
            s.abreProteccionSegundos = SegundosDeApertura;
            AssetDatabase.CreateAsset(s, RutaHechizo);
            log.AppendLine($"Hechizo nuevo: {RutaHechizo} (daño {DanoInicial}, maná {ManaInicial}, abre {SegundosDeApertura} s).");
        }

        s.spellId = SpellId.Amanecer;
        s.displayNameId = "SPELL_AMANECER_NAME";
        s.displayName = "Amanecer";
        s.loreId = "SPELL_AMANECER_LORE";
        s.lore = "Una luz que no existía cuando él pidió su deseo.";
        s.kind = MagicKind.Special;
        s.element = MagicElement.Light;
        s.slotType = SpellSlotType.SpecialOnly;
        s.castStyle = MagicCastStyle.Omni;
        s.caster = PartyControlManager.CharacterSlot.Will;
        s.enGrimorio = true;
        s.comboSequence = (ComboButton[])Secuencia.Clone();
        s.castSFXKey = "Hechizo_Amanecer";
        s.impactSFXKey = "Impacto_Amanecer";

        s.castDelaySeconds = 0.5f;
        s.chargeTime = 0.6f;
        s.chargeStartScale = 0.5f;
        s.useScaleOverride = true;
        s.scaleOverride = Vector3.one * 1.5f;
        s.followOriginDuringCharge = true;
        s.initialSpeed = 22f;
        s.maxRange = 40f;
        s.lifeTime = 2.5f;
        s.knockbackForce = 35f;
        s.destroyOnHit = true;
        s.forwardOffset = 0.6f;
        s.positionOffset = new Vector3(0f, 0.65f, 0f);
        s.flattenDirection = false;
        s.vfxLifetime = 4.0f;

        s.prefab = AsegurarEnvoltorio(log, avisos, t);
        s.spawnVFX = Cargar(VfxPropiosDeHechizosBuilder.Ruta(VfxCarga), avisos);
        s.impactVFX = Cargar(VfxPropiosDeHechizosBuilder.Ruta(VfxImpacto), avisos);
        s.vfxProteccionAbierta = Cargar(VfxPropiosDeHechizosBuilder.Ruta(VfxProteccionAbierta), avisos);
        string fin = VfxPropiosDeHechizosBuilder.RutaDestello(MagicElement.Light, true);
        if (fin != null) s.despawnVFX = AssetDatabase.LoadAssetAtPath<GameObject>(fin);

        var icono = CargarIcono(avisos);
        if (icono != null) s.attackIcon = icono;
        log.AppendLine($"Amanecer: combo {string.Join(" ", Secuencia)}, Luz, especial de Will; efectos, sonidos e icono puestos.");
        return s;
    }

    private static GameObject AsegurarEnvoltorio(StringBuilder log, List<string> avisos, Texturas t)
    {
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPropiosDeHechizosBuilder.Ruta(VfxProyectil));
        if (visual == null) { avisos.Add("No se generó el efecto del proyectil."); return null; }

        CrearCarpeta("Assets/_SPELLS/Prefabs");
        var root = new GameObject("Amanecer");
        try
        {
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.8f;
            root.AddComponent<MagicProjectile>();

            var v = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;

            // Luz dinámica dorada que ilumina todo a su paso con brillo celestial
            var lightGo = new GameObject("LuzProyectil");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Dorado;
            light.intensity = 12f;
            light.range = 28f;
            light.shadows = LightShadows.None;

            // TrailRenderer de cometa solar continuo y masivo
            var trailGo = new GameObject("EstelaCometa");
            trailGo.transform.SetParent(root.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.85f;
            trail.startWidth = 3.2f;
            trail.endWidth = 0.1f;
            trail.sharedMaterial = t.Mat("Brillo", true);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white * 2f, 0f), new GradientColorKey(Dorado * 2f, 0.45f), new GradientColorKey(Naranja * 1.8f, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RutaEnvoltorio);
            log.AppendLine($"Envoltorio del proyectil: {RutaEnvoltorio}.");
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void DarDeAltaEnLaBiblioteca(MagicSpellSO hechizo, StringBuilder log, List<string> avisos)
    {
        var biblioteca = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(RutaBiblioteca);
        if (biblioteca == null) { avisos.Add($"No encuentro la SpellLibrary en {RutaBiblioteca}."); return; }
        var so = new SerializedObject(biblioteca);
        var lista = so.FindProperty("spells");
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).objectReferenceValue == hechizo) return;
        lista.arraySize++;
        lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = hechizo;
        so.ApplyModifiedPropertiesWithoutUndo();
        biblioteca.Rebuild();
        EditorUtility.SetDirty(biblioteca);
        log.AppendLine("SpellLibrary: Amanecer dado de alta.");
    }

    private static Sprite CargarIcono(List<string> avisos)
    {
        if (AssetImporter.GetAtPath(RutaIcono) is not TextureImporter imp)
        {
            avisos.Add($"Falta el icono {RutaIcono}.");
            return null;
        }
        if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RutaIcono);
    }

    private static GameObject Cargar(string ruta, List<string> avisos)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (go == null) avisos.Add($"No encuentro {ruta}.");
        return go;
    }

    // ── Efectos ───────────────────────────────────────────────────────────

    private static Texturas ConstruirEfectos(StringBuilder log)
    {
        var t = new Texturas(CarpetaTexturas, CarpetaMateriales, "Amanecer");
        t.Registrar("Rayo", 64, 256, (x, y) => Gauss(x, 0.25f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 1.3f));
        t.Registrar("Sol", 256, 256, Sol);
        t.Registrar("Horizonte", 256, 64, (x, y) =>
            Mathf.Clamp01(Gauss(y, 0.10f) * (1f - x * x) + Gauss(y, 0.35f) * 0.4f * (1f - Mathf.Abs(x))));
        t.Registrar("Grietas", 256, 256, Grietas);
        t.Registrar("RunaSolar", 256, 256, RunaSolar);
        t.Registrar("PilarSolar", 128, 512, PilarSolar);

        Guardar(VfxProyectil, root => ConstruirProyectil(root, t), log);
        Guardar(VfxCarga, root => ConstruirCarga(root, t), log);
        Guardar(VfxImpacto, root => ConstruirImpacto(root, t), log);
        Guardar(VfxProteccionAbierta, root => ConstruirProteccionAbierta(root, t), log);
        return t;
    }

    private static void Guardar(string nombre, Action<GameObject> construir, StringBuilder log)
    {
        GuardarPrefab("VFX_Hechizo_" + nombre, VfxPropiosDeHechizosBuilder.Ruta(nombre), construir);
        log.AppendLine($"   VFX: {nombre}.");
    }

    /// Lo que viaja: un gran astro / cometa de luz solar con núcleo brillante, corona solar y abanico frontal.
    private static void ConstruirProyectil(GameObject root, Texturas t)
    {
        // 1. Núcleo solar incandescente
        Nucleo(root.transform, t, "Nucleo", Color.white * 3.5f, 3.8f);

        // 2. Corona solar giratoria
        var corona = Sistema(root.transform, "Corona Solar", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = corona.main;
        main.startLifetime = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(6.5f, 8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Dorado * 2.8f;
        Emitir(corona, 25f);
        Girar(corona, 90f);
        Desvanecer(corona, 0.2f, 0.6f);

        // 3. Cruz estelar radiante
        var cruz = Sistema(root.transform, "Cruz Solar", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1f);
        main = cruz.main;
        main.startLifetime = 0.3f;
        main.startSize = 7.5f;
        main.startColor = DoradoPalido * 2.5f;
        Emitir(cruz, 20f);
        Girar(cruz, -35f);

        // 4. Anillo de compresión frontal
        var anillo = Sistema(root.transform, "Onda Frontal", t.Mat("Anillo", true), ParticleSystemRenderMode.Billboard, 1f);
        main = anillo.main;
        main.startLifetime = 0.22f;
        main.startSize = new ParticleSystem.MinMaxCurve(4.5f, 5.8f);
        main.startColor = Crema * 2.0f;
        Emitir(anillo, 16f);

        // 5. Abanico perforador de rayos en cono hacia adelante
        var rayos = Sistema(root.transform, "Abanico Perforador", t.Mat("Rayo", true), ParticleSystemRenderMode.Stretch, 1f);
        main = rayos.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 26f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(DoradoPalido * 2.2f, Color.white * 2.5f);
        Emitir(rayos, 140f);
        var forma = rayos.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 26f;
        forma.radius = 0.5f;
        var render = rayos.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.18f;
        render.lengthScale = 2.8f;
        Desvanecer(rayos, 0.15f, 0.5f);

        // 6. Gran estela de cometa en espacio de mundo
        Estela(root.transform, t, "Cometa Dorado", Dorado * 2.2f, 1.3f, 200f);
        Estela(root.transform, t, "Hilos de Fuego Estelar", Naranja * 2.0f, 0.7f, 100f);
    }

    /// Al empezar a cargar: Runa solar dorada girando en el suelo, confluencia de rayos de luz hacia Will,
    /// sol creciente en el pecho, línea de horizonte a los pies, luz dinámica y vibración/temblor.
    private static void ConstruirCarga(GameObject root, Texturas t)
    {
        // 1. Runa solar plana en el suelo
        var runa = Sistema(root.transform, "Runa Solar Suelo", t.Mat("RunaSolar", true), ParticleSystemRenderMode.HorizontalBillboard, 1.4f, bucle: false);
        runa.transform.localPosition = new Vector3(0f, -0.95f, 0f);
        var main = runa.main;
        main.startLifetime = 1.2f;
        main.startSize = 5.2f;
        main.startColor = Dorado * 1.8f;
        Rafaga(runa, 1);
        Girar(runa, 35f);
        Desvanecer(runa, 0.15f, 0.75f);

        // 2. Haces de energía que convergen al pecho
        var hilos = Sistema(root.transform, "Confluencia Solar", t.Mat("Rayo", true), ParticleSystemRenderMode.Stretch, 1.1f, bucle: false);
        main = hilos.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(Naranja * 1.8f, Crema * 1.6f);
        Emitir(hilos, 120f);
        Forma(hilos, ParticleSystemShapeType.Sphere, 3.2f, 0.2f);
        var vel = hilos.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = new ParticleSystem.MinMaxCurve(-6.5f);
        var render = hilos.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.2f;
        render.lengthScale = 1.6f;
        Desvanecer(hilos, 0.2f, 0.7f);

        // 3. Sol creciente en las manos/pecho
        var sol = Sistema(root.transform, "Sol Naciente", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        main = sol.main;
        main.startLifetime = 1.2f;
        main.startSize = 3.2f;
        main.startColor = Color.white * 2.2f;
        Rafaga(sol, 1);
        Crecer(sol, 0.7f);
        Girar(sol, 60f);
        Desvanecer(sol, 0.15f, 0.8f);

        // 4. Destello estelar
        var destello = Sistema(root.transform, "Destello", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        main = destello.main;
        main.startLifetime = 1.2f;
        main.startSize = 4.5f;
        main.startColor = DoradoPalido * 1.8f;
        Rafaga(destello, 1);
        Crecer(destello, 0.6f);
        Girar(destello, -25f);

        // 5. Línea de horizonte a los pies
        var horizonte = Sistema(root.transform, "Horizonte", t.Mat("Horizonte", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        horizonte.transform.localPosition = new Vector3(0f, -0.95f, 0f);
        main = horizonte.main;
        main.startLifetime = 1.4f;
        main.startSize3D = true;
        main.startSizeX = 9f;
        main.startSizeY = 0.7f;
        main.startSizeZ = 1f;
        main.startColor = DoradoPalido * 1.8f;
        Rafaga(horizonte, 1);
        Crecer(horizonte, 0.4f);
        Desvanecer(horizonte, 0.25f, 0.7f);

        // 6. Chispas que brotan del suelo hacia arriba
        Motas(root.transform, t, "Chispas del Sello", DoradoPalido, 1.8f, 35f, 2.5f);

        // 7. Luz dinámica cálida de canalización
        var lightGo = new GameObject("LuzCarga");
        lightGo.transform.SetParent(root.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Dorado;
        light.intensity = 5.5f;
        light.range = 12f;
        light.shadows = LightShadows.None;

        // 8. Feedback sensorial sutil durante la carga
        var fb = root.AddComponent<VfxImpactFeedback>();
        fb.shakeIntensity = 0.12f;
        fb.shakeDuration = 0.6f;
        fb.enableScreenFlash = false;
        fb.enableHitStop = false;
        fb.enableRumble = true;
        fb.rumbleLow = 0.25f;
        fb.rumbleHigh = 0.4f;
        fb.rumbleDuration = 0.6f;
        fb.dynamicLight = light;
        fb.lightFadeDuration = 0.8f;
    }

    /// Impacto: Cataclismo de Amanecer — pilar solar vertical divino, supernova dorada, onda de choque horizontal,
    /// horizonte cegador, grietas doradas en el suelo, lluvia de esquirlas, flash de pantalla, temblor y micro-hitstop.
    private static void ConstruirImpacto(GameObject root, Texturas t)
    {
        // 1. Pilar solar celestial (columna de luz divina vertical)
        var pilar = Sistema(root.transform, "Pilar Celestial", t.Mat("PilarSolar", true), ParticleSystemRenderMode.VerticalBillboard, 1.4f, bucle: false);
        var main = pilar.main;
        main.startLifetime = 1.1f;
        main.startSize3D = true;
        main.startSizeX = 7.5f;
        main.startSizeY = 32f;
        main.startSizeZ = 1f;
        main.startColor = Color.white * 2.2f;
        Rafaga(pilar, 1);
        Crecer(pilar, 0.08f);
        Desvanecer(pilar, 0.04f, 0.5f);

        // 2. Supernova solar central
        var sol = Sistema(root.transform, "Supernova", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        main = sol.main;
        main.startLifetime = 0.95f;
        main.startSize = 18f;
        main.startColor = Dorado * 2.5f;
        Rafaga(sol, 1);
        Crecer(sol, 0.12f);
        Girar(sol, 35f);
        Desvanecer(sol, 0.04f, 0.45f);

        // 3. Cruz de impacto estelar gigante
        var cruz = Sistema(root.transform, "Cruz Impacto", t.Mat("Estrella", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        main = cruz.main;
        main.startLifetime = 0.8f;
        main.startSize = 16f;
        main.startColor = Color.white * 2.2f;
        Rafaga(cruz, 1);
        Crecer(cruz, 0.08f);
        Desvanecer(cruz, 0.03f, 0.35f);

        // 4. Onda de choque expansiva en el plano del suelo
        var onda = Sistema(root.transform, "Onda Expansiva Suelo", t.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 1.4f, bucle: false);
        main = onda.main;
        main.startLifetime = 0.75f;
        main.startSize = 24f;
        main.startColor = DoradoPalido * 2f;
        Rafaga(onda, 1);
        Crecer(onda, 0.15f);
        Desvanecer(onda, 0.04f, 0.4f);

        // 5. Línea de horizonte áurea que barre la pantalla
        var horizonte = Sistema(root.transform, "Horizonte", t.Mat("Horizonte", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        main = horizonte.main;
        main.startLifetime = 0.85f;
        main.startSize3D = true;
        main.startSizeX = 24f;
        main.startSizeY = 1.8f;
        main.startSizeZ = 1f;
        main.startColor = Crema * 2.2f;
        Rafaga(horizonte, 1);
        Crecer(horizonte, 0.08f);
        Desvanecer(horizonte, 0.04f, 0.35f);

        // 6. Grietas doradas terrestres
        var grietas = Sistema(root.transform, "Grietas Solares", t.Mat("Grietas", true), ParticleSystemRenderMode.HorizontalBillboard, 1.8f, bucle: false);
        main = grietas.main;
        main.startLifetime = 1.5f;
        main.startSize = 9.5f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Dorado * 2.2f;
        Rafaga(grietas, 1);
        Crecer(grietas, 0.06f);
        Desvanecer(grietas, 0.04f, 0.65f);

        // 7. Gran tormenta de esquirlas y fuego solar
        var chispas = Sistema(root.transform, "Esquirlas Solares", t.Mat("Brillo", true), ParticleSystemRenderMode.Stretch, 1.5f, bucle: false);
        main = chispas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        var colores = new Gradient();
        colores.SetKeys(
            new[] { new GradientColorKey(Color.white * 2f, 0f), new GradientColorKey(Dorado * 2f, 0.4f), new GradientColorKey(Naranja * 1.8f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(colores) { mode = ParticleSystemGradientMode.RandomColor };
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Rafaga(chispas, 180);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.5f, 1f);
        var render = chispas.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.08f;
        render.lengthScale = 1.2f;
        Desvanecer(chispas, 0.04f, 0.6f);

        // 8. Luz dinámica de impacto deslumbrante
        var lightGo = new GameObject("LuzImpacto");
        lightGo.transform.SetParent(root.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = DoradoPalido;
        light.intensity = 15f;
        light.range = 32f;
        light.shadows = LightShadows.None;

        // 9. Feedback sensorial contundente de impacto
        var fb = root.AddComponent<VfxImpactFeedback>();
        fb.shakeIntensity = 0.75f;
        fb.shakeDuration = 0.5f;
        fb.enableScreenFlash = true;
        fb.flashColor = new Color(1f, 0.96f, 0.85f, 0.85f);
        fb.flashDuration = 0.25f;
        fb.enableHitStop = true;
        fb.hitStopTimeScale = 0.04f;
        fb.hitStopDuration = 0.10f;
        fb.enableRumble = true;
        fb.rumbleLow = 0.85f;
        fb.rumbleHigh = 0.95f;
        fb.rumbleDuration = 0.6f;
        fb.dynamicLight = light;
        fb.lightFadeDuration = 0.65f;
    }

    /// En bucle sobre el blanco mientras su protección sigue abierta: rayos solares verticales cayendo sobre él,
    /// corona dorada envolvente, grietas doradas latiendo y motas que ascienden.
    private static void ConstruirProteccionAbierta(GameObject root, Texturas t)
    {
        // 1. Rayos celestes cayendo sobre el objetivo
        var rayos = Sistema(root.transform, "Rayos Celestes", t.Mat("Rayo", true), ParticleSystemRenderMode.VerticalBillboard, 1.2f);
        var main = rayos.main;
        main.startLifetime = 0.8f;
        main.startSize3D = true;
        main.startSizeX = 1.8f;
        main.startSizeY = 6.5f;
        main.startSizeZ = 1f;
        main.startColor = DoradoPalido * 1.6f;
        Emitir(rayos, 6f);
        Forma(rayos, ParticleSystemShapeType.Circle, 1.2f, 1f);
        Desvanecer(rayos, 0.2f, 0.6f);

        // 2. Corona solar orbitante
        var corona = Sistema(root.transform, "Corona Orbitante", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1f);
        main = corona.main;
        main.startLifetime = 1.0f;
        main.startSize = 3.8f;
        main.startColor = Dorado * 1.5f;
        Emitir(corona, 2f);
        Girar(corona, 45f);
        Desvanecer(corona, 0.2f, 0.6f);

        // 3. Grietas doradas latiendo en el suelo
        var grietas = Sistema(root.transform, "Grietas", t.Mat("Grietas", true), ParticleSystemRenderMode.HorizontalBillboard, 1f);
        main = grietas.main;
        main.startLifetime = 1.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(3.8f, 4.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Dorado * 1.8f;
        Emitir(grietas, 3f);
        Desvanecer(grietas, 0.2f, 0.6f);

        // 4. Motas de luz doradas ascendentes
        Motas(root.transform, t, "Motas doradas", DoradoPalido, 1.4f, 25f, 1.8f);
    }

    // ── Texturas propias ─────────────────────────────────────────────────

    /// Sol: núcleo denso, dieciséis rayos anchos y treinta y dos finos.
    private static float Sol(float x, float y)
    {
        float r = R(x, y);
        float a = Mathf.Atan2(y, x);
        float nucleo = Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 2.5f);
        float anchos = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(16f * a), 14f) * Mathf.Clamp01(1f - r);
        float finos = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(32f * a + 0.1f), 35f) * Mathf.Clamp01(1f - r * 1.1f) * 0.7f;
        return Mathf.Clamp01(nucleo + anchos + finos);
    }

    /// Runa / Sello Solar para el suelo durante la canalización: anillos concéntricos finos, estrella de 8 puntas y marcas.
    private static float RunaSolar(float x, float y)
    {
        float r = R(x, y);
        if (r > 0.98f) return 0f;
        float a = Mathf.Atan2(y, x);

        float anilloExterior = Gauss(r - 0.90f, 0.025f);
        float anilloMedio = Gauss(r - 0.72f, 0.020f);
        float anilloInterior = Gauss(r - 0.40f, 0.025f);
        float estrella8 = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(8f * a), 8f) * Mathf.Clamp01(1f - r * 1.8f);
        float marcas = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(24f * a), 20f) * Gauss(r - 0.81f, 0.05f);
        float corona = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(16f * a + 0.196f), 12f) * Gauss(r - 0.56f, 0.06f);
        float centro = Mathf.Pow(Mathf.Clamp01(1f - r * 3.5f), 2f);

        return Mathf.Clamp01(anilloExterior + anilloMedio + anilloInterior + estrella8 + marcas + corona + centro);
    }

    /// Pilar Solar: columna de luz vertical divina con perfil gaussiano horizontal y atenuación suave en extremos.
    private static float PilarSolar(float x, float y)
    {
        float perfilX = Gauss(x, 0.32f);
        float perfilY = Mathf.Clamp01(1f - y * y);
        float lineas = 0.75f + 0.25f * Mathf.Cos(x * 18f);
        return Mathf.Clamp01(perfilX * perfilY * lineas);
    }

    private static List<(Vector2 a, Vector2 b, float grosor)> _tramos;

    /// Grietas que salen del centro, quebradas y con ramas.
    private static float Grietas(float x, float y)
    {
        _tramos ??= TramosDeGrieta();
        var p = new Vector2(x, y);
        float linea = 0f, halo = 0f;
        foreach (var (a, b, grosor) in _tramos)
        {
            float d = DistSegmento(p, a, b);
            if (d > grosor * 4f) continue;
            linea = Mathf.Max(linea, Linea(d, grosor));
            halo = Mathf.Max(halo, Gauss(d, grosor * 3f) * 0.35f);
        }
        float borde = Mathf.Clamp01((1f - R(x, y)) * 4f);
        return Mathf.Clamp01(Mathf.Max(linea, halo) * borde);
    }

    private static List<(Vector2, Vector2, float)> TramosDeGrieta()
    {
        var rnd = new Random(724);
        float Azar(float min, float max) => min + (float)rnd.NextDouble() * (max - min);
        var tramos = new List<(Vector2, Vector2, float)>();

        void Grieta(Vector2 desde, float angulo, int pasos, float grosor)
        {
            var p = desde;
            for (int i = 0; i < pasos; i++)
            {
                angulo += Azar(-0.5f, 0.5f);
                var q = p + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * Azar(0.09f, 0.14f);
                float g = grosor * (1f - i / (float)(pasos + 2));
                tramos.Add((p, q, g));
                if (pasos > 2 && rnd.NextDouble() < 0.35) Grieta(q, angulo + Azar(0.6f, 1.1f) * (rnd.NextDouble() < 0.5 ? -1f : 1f), 2, g * 0.7f);
                p = q;
            }
        }

        const int ramas = 7;
        for (int k = 0; k < ramas; k++)
        {
            float angulo = k * Mathf.PI * 2f / ramas + Azar(-0.25f, 0.25f);
            var inicio = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * 0.1f;
            Grieta(inicio, angulo, 7, 0.035f);
        }
        return tramos;
    }
}
