using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using static VfxProcedural;
using Random = System.Random;

/// <summary>
/// Monta Amanecer, el hechizo final de Will (GDD § 6.2; INC-724). Idempotente.
/// <list type="bullet">
/// <item>Efectos propios con VfxProcedural (dorado y blanco de Will con hilos naranja y crema de
/// Estela; rayos de sol y horizonte): carga, haz en abanico, impacto y grietas doradas mientras
/// la protección del blanco sigue abierta. En Assets/_VFX/Hechizos/VFX_Hechizo_Amanecer_*.</item>
/// <item>Assets/_SPELLS/Amanecer.asset: especial de Luz de Will, combo X A B Y, sin equipar, con
/// «abre la protección». Daño y maná solo se ponen al crearlo (se ajustan en el LAB).</item>
/// <item>Envoltorio del proyectil en Assets/_SPELLS/Prefabs/, alta en la SpellLibrary, icono del
/// grimorio y sonidos (SonidosDeHechizos).</item>
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

        ConstruirEfectos(log);
        AssetDatabase.SaveAssets();

        var hechizo = CrearOActualizarHechizo(log, avisos);
        if (hechizo != null)
        {
            DarDeAltaEnLaBiblioteca(hechizo, log, avisos);
            EditorUtility.SetDirty(hechizo);
        }
        AssetDatabase.SaveAssets();

        SonidosDeHechizos.Aplicar();
        ComboWiring.ValidateMenu();

        var final = new StringBuilder("=== Amanecer (INC-724) ===\n").Append(log);
        if (avisos.Count == 0) { final.AppendLine("Sin avisos."); Debug.Log(final.ToString()); }
        else
        {
            final.AppendLine($"--- {avisos.Count} aviso(s): ---");
            foreach (var a in avisos) final.AppendLine("  • " + a);
            Debug.LogWarning(final.ToString());
        }
    }

    // ── Hechizo ───────────────────────────────────────────────────────────

    private static MagicSpellSO CrearOActualizarHechizo(StringBuilder log, List<string> avisos)
    {
        var s = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(RutaHechizo);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<MagicSpellSO>();
            s.damage = DanoInicial;
            s.manaCost = ManaInicial;
            s.abreProteccionSegundos = SegundosDeApertura;
            s.castDelaySeconds = 0.5f;
            s.chargeTime = 0.6f;
            s.chargeStartScale = 0.2f;
            s.followOriginDuringCharge = true;
            s.initialSpeed = 22f;
            s.maxRange = 40f;
            s.lifeTime = 2.5f;
            s.knockbackForce = 20f;
            s.destroyOnHit = true;
            s.forwardOffset = 0.5f;
            s.positionOffset = new Vector3(0f, 0.65f, 0f);
            s.flattenDirection = false;
            s.vfxLifetime = 3f;
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

        s.prefab = AsegurarEnvoltorio(log, avisos);
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

    private static GameObject AsegurarEnvoltorio(StringBuilder log, List<string> avisos)
    {
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPropiosDeHechizosBuilder.Ruta(VfxProyectil));
        if (visual == null) { avisos.Add("No se generó el efecto del proyectil."); return null; }

        CrearCarpeta("Assets/_SPELLS/Prefabs");
        var root = new GameObject("Amanecer");
        try
        {
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;
            root.AddComponent<MagicProjectile>();
            var v = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
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

    private static void ConstruirEfectos(StringBuilder log)
    {
        var t = new Texturas(CarpetaTexturas, CarpetaMateriales, "Amanecer");
        t.Registrar("Rayo", 64, 256, (x, y) => Gauss(x, 0.3f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 1.2f));
        t.Registrar("Sol", 256, 256, Sol);
        t.Registrar("Horizonte", 256, 64, (x, y) =>
            Mathf.Clamp01(Gauss(y, 0.12f) * (1f - x * x) + Gauss(y, 0.45f) * 0.35f * (1f - Mathf.Abs(x))));
        t.Registrar("Grietas", 256, 256, Grietas);

        Guardar(VfxProyectil, root => ConstruirProyectil(root, t), log);
        Guardar(VfxCarga, root => ConstruirCarga(root, t), log);
        Guardar(VfxImpacto, root => ConstruirImpacto(root, t), log);
        Guardar(VfxProteccionAbierta, root => ConstruirProteccionAbierta(root, t), log);
    }

    private static void Guardar(string nombre, Action<GameObject> construir, StringBuilder log)
    {
        GuardarPrefab("VFX_Hechizo_" + nombre, VfxPropiosDeHechizosBuilder.Ruta(nombre), construir);
        log.AppendLine($"   VFX: {nombre}.");
    }

    /// Lo que viaja: un sol pequeño y, delante, rayos que se abren en abanico.
    private static void ConstruirProyectil(GameObject root, Texturas t)
    {
        Nucleo(root.transform, t, "Nucleo", Color.white * 1.8f, 0.9f);

        var halo = Sistema(root.transform, "Halo", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = halo.main;
        main.startLifetime = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 2.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Dorado * 1.4f;
        Emitir(halo, 14f);
        Desvanecer(halo, 0.3f, 0.5f);

        var rayos = Sistema(root.transform, "Abanico", t.Mat("Rayo", true), ParticleSystemRenderMode.Stretch, 1f);
        main = rayos.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(DoradoPalido * 1.6f, Color.white * 1.4f);
        Emitir(rayos, 70f);
        var forma = rayos.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 30f;
        forma.radius = 0.12f;
        var render = rayos.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.12f;
        render.lengthScale = 2f;
        Desvanecer(rayos, 0.15f, 0.5f);

        Estela(root.transform, t, "Estela", Dorado * 1.4f, 0.35f, 60f);
        Estela(root.transform, t, "Hilos de Estela", Naranja * 1.3f, 0.18f, 35f);
    }

    /// Al empezar a cargar: hilos naranja y crema que van al pecho, un corazón que se enciende y
    /// una línea de horizonte a los pies.
    private static void ConstruirCarga(GameObject root, Texturas t)
    {
        var hilos = Sistema(root.transform, "Hilos", t.Mat("Brillo", true), ParticleSystemRenderMode.Stretch, 1.1f, bucle: false);
        var main = hilos.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(Naranja * 1.6f, Crema * 1.5f);
        Emitir(hilos, 90f);
        Forma(hilos, ParticleSystemShapeType.Sphere, 1.6f, 0.1f);
        var vel = hilos.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = new ParticleSystem.MinMaxCurve(-3.2f);
        var render = hilos.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.15f;
        render.lengthScale = 1f;
        Desvanecer(hilos, 0.2f, 0.7f);

        var corazon = Sistema(root.transform, "Corazon", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        main = corazon.main;
        main.startLifetime = 1.3f;
        main.startSize = 1.6f;
        main.startColor = Dorado * 1.8f;
        Rafaga(corazon, 1);
        Crecer(corazon, 0.8f);
        Girar(corazon, 40f);
        Desvanecer(corazon, 0.2f, 0.8f);

        var horizonte = Sistema(root.transform, "Horizonte", t.Mat("Horizonte", true), ParticleSystemRenderMode.Billboard, 1.4f, bucle: false);
        horizonte.transform.localPosition = new Vector3(0f, -1.3f, 0f);
        main = horizonte.main;
        main.startLifetime = 1.4f;
        main.startSize3D = true;
        main.startSizeX = 6f;
        main.startSizeY = 0.5f;
        main.startSizeZ = 1f;
        main.startColor = DoradoPalido * 1.6f;
        Rafaga(horizonte, 1);
        Crecer(horizonte, 0.5f);
        Desvanecer(horizonte, 0.25f, 0.7f);
    }

    /// Impacto: el sol se abre, una línea de horizonte, grietas doradas y chispas.
    private static void ConstruirImpacto(GameObject root, Texturas t)
    {
        var sol = Sistema(root.transform, "Sol", t.Mat("Sol", true), ParticleSystemRenderMode.Billboard, 1.5f, bucle: false);
        var main = sol.main;
        main.startLifetime = 0.8f;
        main.startSize = 8f;
        main.startColor = Dorado * 2f;
        Rafaga(sol, 1);
        Crecer(sol, 0.15f);
        Girar(sol, 25f);
        Desvanecer(sol, 0.05f, 0.45f);

        var horizonte = Sistema(root.transform, "Horizonte", t.Mat("Horizonte", true), ParticleSystemRenderMode.Billboard, 1.5f, bucle: false);
        main = horizonte.main;
        main.startLifetime = 0.9f;
        main.startSize3D = true;
        main.startSizeX = 12f;
        main.startSizeY = 0.8f;
        main.startSizeZ = 1f;
        main.startColor = Crema * 1.8f;
        Rafaga(horizonte, 1);
        Crecer(horizonte, 0.1f);
        Desvanecer(horizonte, 0.05f, 0.4f);

        var grietas = Sistema(root.transform, "Grietas", t.Mat("Grietas", true), ParticleSystemRenderMode.Billboard, 1.5f, bucle: false);
        main = grietas.main;
        main.startLifetime = 1.2f;
        main.startSize = 4.5f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Dorado * 1.8f;
        Rafaga(grietas, 1);
        Crecer(grietas, 0.08f);
        Desvanecer(grietas, 0.05f, 0.6f);

        var chispas = Sistema(root.transform, "Chispas", t.Mat("Brillo", true), ParticleSystemRenderMode.Stretch, 1.5f, bucle: false);
        main = chispas.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        var colores = new Gradient();
        colores.SetKeys(
            new[] { new GradientColorKey(Dorado * 2f, 0f), new GradientColorKey(Color.white * 1.5f, 0.5f), new GradientColorKey(Naranja * 1.6f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(colores) { mode = ParticleSystemGradientMode.RandomColor };
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        Rafaga(chispas, 70);
        Forma(chispas, ParticleSystemShapeType.Sphere, 0.3f, 1f);
        var render = chispas.GetComponent<ParticleSystemRenderer>();
        render.velocityScale = 0.06f;
        render.lengthScale = 1f;
        Desvanecer(chispas, 0.05f, 0.6f);
    }

    /// En bucle sobre el blanco mientras su protección sigue abierta: grietas doradas que laten y
    /// motas que suben.
    private static void ConstruirProteccionAbierta(GameObject root, Texturas t)
    {
        var grietas = Sistema(root.transform, "Grietas", t.Mat("Grietas", true), ParticleSystemRenderMode.Billboard, 1f);
        var main = grietas.main;
        main.startLifetime = 1.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 2.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Dorado * 1.6f;
        Emitir(grietas, 3f);
        Desvanecer(grietas, 0.2f, 0.6f);

        var brillo = Sistema(root.transform, "Brillo", t.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 1f);
        main = brillo.main;
        main.startLifetime = 0.8f;
        main.startSize = 2.6f;
        main.startColor = new Color(Dorado.r, Dorado.g, Dorado.b, 0.35f);
        Emitir(brillo, 3f);
        Desvanecer(brillo, 0.3f, 0.6f);

        Motas(root.transform, t, "Motas doradas", DoradoPalido, 0.8f, 18f, 1.2f);
    }

    // ── Texturas propias ─────────────────────────────────────────────────

    /// Sol: núcleo, doce rayos anchos y veinticuatro finos.
    private static float Sol(float x, float y)
    {
        float r = R(x, y);
        float a = Mathf.Atan2(y, x);
        float nucleo = Mathf.Pow(Mathf.Clamp01(1f - r * 1.8f), 2f);
        float anchos = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(12f * a), 12f) * Mathf.Clamp01(1f - r);
        float finos = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(24f * a + 0.13f), 30f) * Mathf.Clamp01(1f - r * 1.2f) * 0.6f;
        return Mathf.Clamp01(nucleo + anchos + finos);
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
