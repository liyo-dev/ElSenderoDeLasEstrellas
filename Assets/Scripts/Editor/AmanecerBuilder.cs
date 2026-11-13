using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using static VfxProcedural;

/// <summary>
/// Monta Amanecer, el hechizo final de Will (GDD § 6.2; INC-724, INC-760). Idempotente.
/// <list type="bullet">
/// <item>Efectos con volumen (HechizosConVolumen.ConstruirAmanecer): sol con corona de rayos que viaja,
/// carga entre las manos, estallido y anillos de luz sobre la protección abierta. Ver INC-1140.</item>
/// <item>Assets/_SPELLS/Amanecer.asset: especial de Luz de Will, combo X A B Y, sin equipar, con
/// «abre la protección». Daño y maná solo se ponen al crearlo (se ajustan en el LAB).</item>
/// <item>Envoltorio del proyectil con luz propia en Assets/_SPELLS/Prefabs/, alta en la SpellLibrary,
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

    /// Los efectos los construye HechizosConVolumen (INC-1140).
    private static Texturas ConstruirEfectos(StringBuilder log)
    {
        var t = VfxPropiosDeHechizosBuilder.TexturasDeDibujo();
        HechizosConVolumen.ConstruirAmanecer(log);
        return t;
    }
}
