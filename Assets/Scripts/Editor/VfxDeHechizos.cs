using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VFX de los hechizos. Regla: cada hechizo tiene efectos propios; ninguno repite el de otro. Los
/// destellos de lanzamiento y de fin (spawnVFX / despawnVFX) van por elemento y no cuentan.
/// - «VFX de los hechizos · aplicar»: genera los efectos propios (VfxPropiosDeHechizosBuilder) y
///   pone a cada hechizo los de la tabla Fichas. Los proyectiles viajan siempre dentro de un
///   envoltorio con MagicProjectile (sin él el efecto se queda quieto en la mano); en zonas y
///   envoltorios se cambia el visual de dentro. Idempotente. Ver INC-640.
/// - «Comprobar VFX de los hechizos»: efectos repetidos, proyectiles sin MagicProjectile y
///   envoltorios con componentes de juego dentro (p. ej. un punto de guardado).
/// </summary>
public static class VfxDeHechizos
{
    private const string SpellFolder = "Assets/_SPELLS/";
    private const string EnvoltorioFolder = "Assets/_SPELLS/Prefabs/";
    private const string GuardianConfig = "Assets/_NPCs/Combat/NPC_Combat_Config_GuardianPiedra.asset";
    private const string GuardianZona = "Assets/_ENEMY_SPELLS/Prefabs/GuardianPiedra_Zona.prefab";

    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";
    private const string Lana = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/";
    private const string Gabriel = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/";
    private const string Free = "Assets/VFX/Free Game VFX/Prefab/";
    private const string Fuego = "Assets/VFX/fireAttackEffects/";

    private static string Propio(string nombre) => VfxPropiosDeHechizosBuilder.Ruta(nombre);

    /// Efectos de un hechizo. null = no se toca.
    private sealed class Ficha
    {
        public string Asset;
        public string Visual;        // lo que viaja, lo que se ve en la zona o el efecto de llegada
        public string Impacto;
        public bool SinImpacto;
        public string Estado;
        public string LevitacionMantener;
        public string LevitacionSoltar;
        public MagicElement? Elemento;
        public string Nombre;
    }

    /// Fuente de verdad de los efectos de cada hechizo (Huracan no está: no lo usa nadie).
    private static readonly Ficha[] Fichas =
    {
        // Will
        new Ficha { Asset = "LlamaAstral",     Impacto = Fuego + "effects/fireBall/hitVFX.prefab" },
        new Ficha { Asset = "BolaPrisma",      Visual = Propio(VfxPropiosDeHechizosBuilder.BolaPrisma), Impacto = Lana + "Flash/Flash_star_ellow_purple.prefab", Elemento = MagicElement.Light },
        new Ficha { Asset = "CorazonEstelar",  Impacto = Propio(VfxPropiosDeHechizosBuilder.CorazonImpacto) },
        new Ficha { Asset = "EstrellaFugaz" },
        new Ficha { Asset = "LluviaDeChispas" },
        new Ficha { Asset = "Meteoro",         Visual = Propio(VfxPropiosDeHechizosBuilder.MeteoroZona) },
        new Ficha { Asset = "NovaDeLuz",       Visual = Best + "HolyEffect/HolyEffect2.prefab" },
        new Ficha { Asset = "CupulaEstelar" },
        new Ficha { Asset = "Levitation",      LevitacionMantener = Hovl + "Character auras/Buff.prefab", LevitacionSoltar = Lana + "Flash/Flash_ellow.prefab", Nombre = "Levitación" },
        // Estela
        new Ficha { Asset = "BolaFuego" },
        new Ficha { Asset = "ChispaIgnea" },
        new Ficha { Asset = "ChispaIgneaFuego" },
        new Ficha { Asset = "Rafaga" },
        new Ficha { Asset = "Tornado",         Impacto = Gabriel + "vfx_Smoke_01.prefab", Nombre = "Ciclón" },
        new Ficha { Asset = "Remolino" },
        new Ficha { Asset = "BrisaSanadora" },
        new Ficha { Asset = "MuroDeFuego",     Visual = Fuego + "prefabs/fireZoneVFX.prefab" },
        new Ficha { Asset = "TormentaDeFuego", Visual = Fuego + "prefabs/meteorFireRainVFX.prefab" },
        // Liam
        new Ficha { Asset = "AuraEstelar",     Visual = Best + "Kunai/Kunai5.prefab", Impacto = Hovl + "Sparks/Sparks explode white.prefab" },
        new Ficha { Asset = "DardoMental",     Visual = Propio(VfxPropiosDeHechizosBuilder.Dardo) },
        new Ficha { Asset = "Eco",             Impacto = Free + "FX_Purple_Hit_02.prefab" },
        new Ficha { Asset = "GarraDelPacto",   Visual = Propio(VfxPropiosDeHechizosBuilder.Garra), Impacto = Hovl + "Sparks/Sparks explode pink.prefab" },
        new Ficha { Asset = "CadenasDelPacto", Visual = Propio(VfxPropiosDeHechizosBuilder.CadenasZona), Estado = Propio(VfxPropiosDeHechizosBuilder.CadenasEstado) },
        new Ficha { Asset = "SelloDelPacto",   Visual = Propio(VfxPropiosDeHechizosBuilder.SelloZona) },
        new Ficha { Asset = "JuicioDelPacto",  Visual = Propio(VfxPropiosDeHechizosBuilder.JuicioZona) },
        new Ficha { Asset = "PasoSombrio",     Visual = Lana + "Flash/Flash_blue_purple.prefab", SinImpacto = true },
        // Mago Oscuro
        new Ficha { Asset = "MagoOscuroGolpe", Impacto = Best + "DarkEffect/DarkEffect5.prefab" },
        new Ficha { Asset = "MagoOscuroGrieta", Visual = Propio(VfxPropiosDeHechizosBuilder.GrietaZona) },
    };

    [MenuItem("El Sendero/Magia/VFX de los hechizos · aplicar (INC-640)")]
    public static void AplicarMenu()
    {
        var log = new StringBuilder("=== VFX de los hechizos (INC-640) ===\n");
        var warnings = new List<string>();
        Aplicar(log, warnings);
        AssetDatabase.SaveAssets();
        Informe(log, warnings);
        Comprobar();
    }

    [MenuItem("El Sendero/Magia/Comprobar VFX de los hechizos")]
    public static void Comprobar()
    {
        var porEfecto = new Dictionary<GameObject, List<string>>();
        var avisos = new List<string>();
        foreach (var spell in Hechizos())
        {
            foreach (var fx in Firma(spell))
            {
                if (!porEfecto.TryGetValue(fx, out var lista)) porEfecto[fx] = lista = new List<string>();
                if (!lista.Contains(spell.name)) lista.Add(spell.name);
            }
            if (EsProyectil(spell) && spell.prefab != null && spell.prefab.GetComponent<MagicProjectile>() == null)
                avisos.Add($"{spell.name}: el proyectil no lleva MagicProjectile; se queda quieto en la mano.");
            if (spell.prefab != null)
                foreach (var script in ScriptsDeJuegoDentro(spell.prefab))
                    avisos.Add($"{spell.name}: su efecto lleva dentro {script} (componente de juego).");
        }

        var repetidos = porEfecto.Where(kv => kv.Value.Count > 1).OrderByDescending(kv => kv.Value.Count).ToList();
        foreach (var kv in repetidos)
            avisos.Add($"Repetido: {kv.Key.name}  ←  {string.Join(", ", kv.Value)}");

        if (avisos.Count == 0) { Debug.Log("[VFX] Hechizos en orden: sin efectos repetidos y todos los proyectiles vuelan."); return; }
        Debug.LogWarning($"[VFX] {avisos.Count} aviso(s):\n  • " + string.Join("\n  • ", avisos));
    }

    /// Lo aplica también «Grimorio · montar todo».
    public static void Aplicar(StringBuilder log, List<string> warnings)
    {
        VfxPropiosDeHechizosBuilder.Construir(log, warnings);
        AssetDatabase.SaveAssets();
        SepararZonaDelGuardian(log, warnings);
        MoverEnvoltorio("Assets/Prefabs/GarraDelPacto.prefab", log, warnings);

        foreach (var f in Fichas)
        {
            var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(SpellFolder + f.Asset + ".asset");
            if (spell == null) { warnings.Add($"No encuentro el hechizo {f.Asset}."); continue; }
            AplicarFicha(spell, f, log, warnings);
            EditorUtility.SetDirty(spell);
        }
        Pagina(log, warnings);
    }

    private static void AplicarFicha(MagicSpellSO spell, Ficha f, StringBuilder log, List<string> warnings)
    {
        if (f.Elemento.HasValue && spell.element != f.Elemento.Value)
        {
            spell.element = f.Elemento.Value;
            log.AppendLine($"   {spell.name}: elemento {spell.element}.");
        }
        if (f.Nombre != null && spell.displayName != f.Nombre)
        {
            spell.displayName = f.Nombre;
            log.AppendLine($"   {spell.name}: nombre «{f.Nombre}».");
        }

        var visual = f.Visual != null ? Load(f.Visual, warnings) : null;
        if (spell.kind == MagicKind.Zone)
        {
            if (visual != null) CambiarVisual(spell.prefab, visual, true, spell.name, log, warnings);
        }
        else if (spell.kind == MagicKind.Teleport)
        {
            if (visual != null && spell.prefab != visual) { spell.prefab = visual; log.AppendLine($"   {spell.name}: llegada {visual.name}."); }
        }
        else if (EsProyectil(spell))
        {
            AsegurarProyectil(spell, visual, log, warnings);
        }

        if (f.Impacto != null) Asignar(ref spell.impactVFX, Load(f.Impacto, warnings), spell.name, "impacto", log);
        else if (f.SinImpacto && spell.impactVFX != null) { spell.impactVFX = null; log.AppendLine($"   {spell.name}: sin impacto."); }
        if (f.Estado != null) Asignar(ref spell.statusVFX, Load(f.Estado, warnings), spell.name, "estado", log);
        if (f.LevitacionMantener != null) Asignar(ref spell.levitationHoldVFX, Load(f.LevitacionMantener, warnings), spell.name, "al levantar", log);
        if (f.LevitacionSoltar != null) Asignar(ref spell.levitationReleaseVFX, Load(f.LevitacionSoltar, warnings), spell.name, "al soltar", log);

        string destello = VfxPropiosDeHechizosBuilder.RutaDestello(spell.element, false);
        string fin = VfxPropiosDeHechizosBuilder.RutaDestello(spell.element, true);
        if (destello != null) Asignar(ref spell.spawnVFX, Load(destello, warnings), spell.name, "destello", log);
        if (fin != null) Asignar(ref spell.despawnVFX, Load(fin, warnings), spell.name, "destello de fin", log);
    }

    private static void Asignar(ref GameObject campo, GameObject fx, string hechizo, string que, StringBuilder log)
    {
        if (fx == null || campo == fx) return;
        campo = fx;
        log.AppendLine($"   {hechizo}: {que} {fx.name}.");
    }

    private static bool EsProyectil(MagicSpellSO s) => s.kind == MagicKind.Projectile || s.kind == MagicKind.Special;

    // ── Proyectiles: siempre con MagicProjectile en la raíz ──────────────

    /// Si el hechizo ya vuela con un prefab propio con MagicProjectile, solo cambia el visual de
    /// dentro (si se pide). Si su prefab es un efecto suelto, lo mete en un envoltorio nuevo.
    private static void AsegurarProyectil(MagicSpellSO spell, GameObject visual, StringBuilder log, List<string> warnings)
    {
        var actual = spell.prefab;
        bool vuela = actual != null && actual.GetComponent<MagicProjectile>() != null;
        bool esEnvoltorio = vuela && AssetDatabase.GetAssetPath(actual).StartsWith(EnvoltorioFolder);

        if (visual == null)
        {
            // Envoltorio con restos de juego en el visual: se vuelve a montar el mismo visual, limpio.
            if (esEnvoltorio && ScriptsDeJuegoDentro(actual).Any()) visual = Visuales(actual).FirstOrDefault(v => v != actual);
            else if (vuela || actual == null) return;
            else visual = actual;
            if (visual == null) return;
        }
        if (esEnvoltorio)
        {
            CambiarVisual(actual, visual, false, spell.name, log, warnings);
            return;
        }
        if (visual.GetComponent<MagicProjectile>() != null)
        {
            if (spell.prefab != visual) { spell.prefab = visual; log.AppendLine($"   {spell.name}: viaja con {visual.name}."); }
            return;
        }
        spell.prefab = CrearEnvoltorio(spell.name, visual);
        log.AppendLine($"   {spell.name}: envoltorio nuevo con {visual.name}.");
    }

    private static GameObject CrearEnvoltorio(string nombre, GameObject visual)
    {
        string path = EnvoltorioFolder + nombre + ".prefab";
        var root = new GameObject(nombre);
        try
        {
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.35f;
            root.AddComponent<MagicProjectile>();
            var v = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            LimpiarVisual(v);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ── Envoltorios (zonas y proyectiles): el efecto va dentro ───────────

    private static void CambiarVisual(GameObject envoltorio, GameObject fx, bool enBucle, string hechizo, StringBuilder log, List<string> warnings)
    {
        if (envoltorio == null) { warnings.Add($"{hechizo} no tiene prefab."); return; }
        string path = AssetDatabase.GetAssetPath(envoltorio);
        if (!path.StartsWith(EnvoltorioFolder)) { warnings.Add($"{hechizo}: su prefab no es un envoltorio de {EnvoltorioFolder}; no lo toco."); return; }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (Visuales(root).Contains(fx) && !ScriptsDeJuegoDentro(root).Any()) return;

            // Si se vuelve a montar el mismo efecto, conserva su colocación y tamaño.
            Vector3 pos = Vector3.zero, escala = Vector3.one;
            Quaternion rot = Quaternion.identity;
            foreach (Transform child in root.transform.Cast<Transform>().ToList())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == fx)
                {
                    pos = child.localPosition; rot = child.localRotation; escala = child.localScale;
                }
                Object.DestroyImmediate(child.gameObject);
            }

            var v = (GameObject)PrefabUtility.InstantiatePrefab(fx, root.transform);
            v.transform.localPosition = pos;
            v.transform.localRotation = rot;
            v.transform.localScale = escala;
            LimpiarVisual(v);
            if (enBucle)
                foreach (var ps in v.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = true;
                }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"   {hechizo}: muestra {fx.name}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// Un visual solo se ve: fuera físicas y componentes de juego (los scripts de los packs se quedan).
    private static void LimpiarVisual(GameObject v)
    {
        // Primero los scripts: alguno exige un Collider y no deja quitarlo antes.
        foreach (var mb in v.GetComponentsInChildren<MonoBehaviour>(true))
            if (EsScriptDeJuego(mb)) Object.DestroyImmediate(mb);
        foreach (var rb in v.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var c in v.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    private static bool EsScriptDeJuego(MonoBehaviour mb)
    {
        if (mb == null) return false;
        var script = MonoScript.FromMonoBehaviour(mb);
        return script != null && AssetDatabase.GetAssetPath(script).StartsWith("Assets/Scripts/");
    }

    /// Componentes de juego que cuelgan de los visuales de un prefab (no de su raíz).
    private static IEnumerable<string> ScriptsDeJuegoDentro(GameObject prefab)
    {
        foreach (Transform child in prefab.transform)
            foreach (var mb in child.GetComponentsInChildren<MonoBehaviour>(true))
                if (EsScriptDeJuego(mb)) yield return mb.GetType().Name;
    }

    /// El Guardián de la Piedra lanzaba la zona del Sello del Pacto: se queda con una copia propia
    /// para que el Sello pueda cambiar sin tocarle a él.
    private static void SepararZonaDelGuardian(StringBuilder log, List<string> warnings)
    {
        var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GuardianConfig);
        if (config == null) return;
        var so = new SerializedObject(config);
        var campo = so.FindProperty("spell3Prefab");
        if (campo == null) { warnings.Add("El Guardián de la Piedra no tiene spell3Prefab."); return; }
        var sello = AssetDatabase.LoadAssetAtPath<GameObject>(EnvoltorioFolder + "SelloDelPacto.prefab");
        if (sello == null || campo.objectReferenceValue != sello) return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(GuardianZona) == null &&
            !AssetDatabase.CopyAsset(EnvoltorioFolder + "SelloDelPacto.prefab", GuardianZona))
        {
            warnings.Add($"No he podido copiar la zona del Sello a {GuardianZona}.");
            return;
        }
        campo.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(GuardianZona);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("   Guardián de la Piedra: zona propia (copia de la que tenía), el Sello queda libre.");
    }

    private static void MoverEnvoltorio(string desde, StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(desde) == null) return;
        string hasta = EnvoltorioFolder + System.IO.Path.GetFileName(desde);
        string error = AssetDatabase.MoveAsset(desde, hasta);
        if (string.IsNullOrEmpty(error)) log.AppendLine($"   {desde} → {hasta}.");
        else warnings.Add($"No he podido mover {desde}: {error}");
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
            log.AppendLine("   Página del grimorio: polvo dorado y destello (ver PaginaDelGrimorioBuilder).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Firma visual de un hechizo ────────────────────────────────────────

    private static IEnumerable<MagicSpellSO> Hechizos()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:MagicSpellSO"))
        {
            var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (spell != null) yield return spell;
        }
    }

    private static IEnumerable<GameObject> Firma(MagicSpellSO s)
    {
        var set = new HashSet<GameObject>();
        if (s.prefab != null) foreach (var v in Visuales(s.prefab)) set.Add(v);
        if (s.impactVFX != null) set.Add(s.impactVFX);
        if (s.statusVFX != null) set.Add(s.statusVFX);
        if (s.groupShieldVFX != null) set.Add(s.groupShieldVFX);
        if (s.levitationHoldVFX != null) set.Add(s.levitationHoldVFX);
        if (s.levitationReleaseVFX != null) set.Add(s.levitationReleaseVFX);
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
            var src = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
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
