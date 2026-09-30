using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VFX de los hechizos (INC-516). Regla: cada hechizo tiene un efecto propio; ninguno repite el de
/// otro. El destello de lanzamiento (spawnVFX / despawnVFX y los de la levitación) es común a todos
/// a propósito y no cuenta.
/// - «Comprobar VFX repetidos»: lista los efectos que comparten dos o más hechizos. Mira el prefab
///   (o los efectos que lleva dentro, si es un envoltorio como los de las zonas), el impacto, el
///   estado, el escudo de grupo y el indicador de la levitación.
/// - «VFX propios de los hechizos nuevos»: pone a cada hechizo del grimorio que repetía un efecto
///   uno que no usa nadie más (tabla de abajo) y cambia el brillo de la página del grimorio, que
///   también copiaba el de un hechizo. Idempotente.
/// </summary>
public static class VfxDeHechizos
{
    private const string SpellFolder = "Assets/_SPELLS/";
    private const string ZoneFolder = "Assets/_SPELLS/Prefabs/";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string HovlFire = "Assets/VFX/Hovl Studio/Procedural fire/Prefabs/";
    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";
    private const string Lana = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/";
    private const string Gabriel = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/";
    private const string Universal = "Assets/VFX/Univeral FX Shader/Prefab/";
    private const string Guz = "Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/";

    /// Efecto propio de cada hechizo nuevo que repetía el de otro. prefab = lo que viaja (proyectil) o
    /// lo que se ve dentro de la zona; null = no se toca.
    private static readonly (string asset, string prefab, string impact, bool quitarImpacto)[] Propios =
    {
        ("EstrellaFugaz",   Lana + "Shine/Shine_ellow.prefab",                 null, false),
        ("LluviaDeChispas", Hovl + "Sparks/Sparks yellow.prefab",              null, false),
        ("ChispaIgnea",     HovlFire + "Magic fire pro orange.prefab",         Gabriel + "vfx_Explosion_01.prefab", false),
        ("Rafaga",          Hovl + "Smoke effects/Dust puff_flying.prefab",    null, false),
        ("Eco",             Universal + "Projectile.prefab",                   null, false),
        ("DardoMental",     Best + "Kunai/Kunai3.prefab",                      null, false),
        ("CadenasDelPacto", Guz + "AoE Magic.prefab",                          null, false),
        ("Remolino",        Universal + "Vortex.prefab",                       null, false),
        // El teletransporte no impacta: el impacto que traía de la plantilla sobraba.
        ("PasoSombrio",     null,                                              null, true),
    };

    [MenuItem("El Sendero/Archivo/Magia/VFX propios de los hechizos nuevos (INC-516)")]
    public static void AplicarMenu()
    {
        var log = new StringBuilder("=== VFX propios de los hechizos nuevos (INC-516) ===\n");
        var warnings = new List<string>();
        Aplicar(log, warnings);
        AssetDatabase.SaveAssets();
        Informe(log, warnings);
        ComprobarRepetidos();
    }

    [MenuItem("El Sendero/Magia/Comprobar VFX repetidos entre hechizos")]
    public static void ComprobarRepetidos()
    {
        var porEfecto = new Dictionary<GameObject, List<string>>();
        foreach (var guid in AssetDatabase.FindAssets("t:MagicSpellSO"))
        {
            var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (spell == null) continue;
            foreach (var fx in Firma(spell))
            {
                if (!porEfecto.TryGetValue(fx, out var lista)) porEfecto[fx] = lista = new List<string>();
                if (!lista.Contains(spell.name)) lista.Add(spell.name);
            }
        }

        var repetidos = porEfecto.Where(kv => kv.Value.Count > 1).OrderByDescending(kv => kv.Value.Count).ToList();
        if (repetidos.Count == 0)
        {
            Debug.Log("[VFX] Ningún hechizo repite el efecto de otro.");
            return;
        }
        var sb = new StringBuilder($"[VFX] {repetidos.Count} efecto(s) repetidos entre hechizos:\n");
        foreach (var kv in repetidos)
            sb.AppendLine($"  • {kv.Key.name}  ←  {string.Join(", ", kv.Value)}");
        Debug.LogWarning(sb.ToString());
    }

    /// Lo aplica también «Grimorio · montar todo», para que un montaje desde cero no repita efectos.
    public static void Aplicar(StringBuilder log, List<string> warnings)
    {
        foreach (var p in Propios)
        {
            var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(SpellFolder + p.asset + ".asset");
            if (spell == null) { warnings.Add($"No encuentro el hechizo {p.asset}."); continue; }

            if (p.prefab != null)
            {
                var fx = Load(p.prefab, warnings);
                if (fx != null)
                {
                    if (spell.kind == MagicKind.Zone) CambiarVisualDeZona(spell, fx, log, warnings);
                    else if (spell.prefab != fx)
                    {
                        spell.prefab = fx;
                        EditorUtility.SetDirty(spell);
                        log.AppendLine($"   {spell.displayName}: viaja con {fx.name}.");
                    }
                }
            }

            if (p.impact != null)
            {
                var fx = Load(p.impact, warnings);
                if (fx != null && spell.impactVFX != fx)
                {
                    spell.impactVFX = fx;
                    EditorUtility.SetDirty(spell);
                    log.AppendLine($"   {spell.displayName}: impacto {fx.name}.");
                }
            }
            else if (p.quitarImpacto && spell.impactVFX != null)
            {
                spell.impactVFX = null;
                EditorUtility.SetDirty(spell);
                log.AppendLine($"   {spell.displayName}: sin impacto (no le hace falta).");
            }
        }
        Pagina(log, warnings);
    }

    // ── Zonas: el efecto va dentro del prefab envoltorio (MagicZoneEffect) ──

    private static void CambiarVisualDeZona(MagicSpellSO spell, GameObject fx, StringBuilder log, List<string> warnings)
    {
        if (spell.prefab == null) { warnings.Add($"{spell.displayName} no tiene prefab de zona."); return; }
        string path = AssetDatabase.GetAssetPath(spell.prefab);
        if (!path.StartsWith(ZoneFolder)) { warnings.Add($"{spell.displayName}: su zona no es un envoltorio de {ZoneFolder}; no la toco."); return; }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (Visuales(root).Contains(fx)) return;

            // Fuera los efectos que llevaba; el nuevo, en bucle, porque la zona dura varios segundos.
            foreach (Transform child in root.transform.Cast<Transform>().ToList())
                if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) Object.DestroyImmediate(child.gameObject);

            var v = (GameObject)PrefabUtility.InstantiatePrefab(fx, root.transform);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            foreach (var ps in v.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = true;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"   {spell.displayName}: la zona muestra {fx.name}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Página del grimorio ───────────────────────────────────────────────

    private static void Pagina(StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PaginaDelGrimorioBuilder.PrefabPath) == null) return;
        var root = PrefabUtility.LoadPrefabContents(PaginaDelGrimorioBuilder.PrefabPath);
        try
        {
            PaginaDelGrimorioBuilder.Montar(root, warnings);
            PrefabUtility.SaveAsPrefabAsset(root, PaginaDelGrimorioBuilder.PrefabPath);
            log.AppendLine("   Página del grimorio: brillo y destello propios (ver PaginaDelGrimorioBuilder).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Firma visual de un hechizo ────────────────────────────────────────

    private static IEnumerable<GameObject> Firma(MagicSpellSO s)
    {
        var set = new HashSet<GameObject>();
        if (s.prefab != null) foreach (var v in Visuales(s.prefab)) set.Add(v);
        if (s.impactVFX != null) set.Add(s.impactVFX);
        if (s.statusVFX != null) set.Add(s.statusVFX);
        if (s.groupShieldVFX != null) set.Add(s.groupShieldVFX);
        if (s.levitationRangeIndicatorVFX != null) set.Add(s.levitationRangeIndicatorVFX);
        return set;
    }

    /// Los efectos que se ven de un prefab: él mismo si tiene partículas o malla propias; si es un
    /// envoltorio, los prefabs que lleva dentro.
    private static List<GameObject> Visuales(GameObject prefab)
    {
        var result = new List<GameObject>();
        foreach (Transform child in prefab.transform)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
            if (src != null && !result.Contains(src)) result.Add(src);
        }
        bool propio = prefab.GetComponent<ParticleSystem>() != null || prefab.GetComponent<Renderer>() != null;
        if (result.Count == 0 || propio) result.Add(prefab);
        return result;
    }

    private static GameObject Load(string path, List<string> warnings)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) warnings.Add($"No encuentro {path}.");
        return go;
    }

    private static void Informe(StringBuilder log, List<string> warnings)
    {
        if (warnings.Count == 0) { log.AppendLine("Sin avisos."); Debug.Log(log.ToString()); return; }
        log.AppendLine($"--- {warnings.Count} aviso(s): ---");
        foreach (var w in warnings) log.AppendLine("  • " + w);
        Debug.LogWarning(log.ToString());
    }
}
