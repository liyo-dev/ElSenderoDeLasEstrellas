using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Pone los efectos de dibujo de los escudos y de lo que lanzan los enemigos en quien los usa:
/// escudo y contraataque de Will, escudo de los NPC, escudo de grupo de la Cúpula estelar, bolas y
/// lluvia del demonio, beholder y golem. Las bolas de plasma conservan su prefab (con su
/// EnemyProjectile y su colisión): se apagan sus esferas y se les mete dentro el dibujo. Idempotente.
/// Ver INC-1127
public static class VfxDeCombateDeDibujo
{
    private static readonly string[] BolasDePlasma =
    {
        "Assets/_ENEMY_SPELLS/Prefabs/Plasma Sphere (1).prefab",
        "Assets/_ENEMY_SPELLS/Prefabs/Plasma Sphere (2).prefab",
        "Assets/_ENEMY_SPELLS/Prefabs/Plasma Sphere (3).prefab",
        "Assets/_ENEMY_SPELLS/Prefabs/Plasma Sphere Cinematic.prefab",
    };
    private const string NombreDelDibujo = "Dibujo";

    private sealed class Campo
    {
        public string Prefab, Componente, Propiedad, Efecto;
        public Campo(string prefab, string componente, string propiedad, string efecto)
        { Prefab = prefab; Componente = componente; Propiedad = propiedad; Efecto = efecto; }
    }

    private static readonly Campo[] Campos =
    {
        new Campo("Assets/Prefabs/_WILL.prefab", "PlayerShieldController", "shieldPrefab", VfxPropiosDeHechizosBuilder.EscudoWill),
        new Campo("Assets/Prefabs/_WILL.prefab", "PlayerShieldController", "counterVfx", VfxPropiosDeHechizosBuilder.Contraataque),
        new Campo("Assets/Prefabs/ShieldNpc.prefab", "NPCShieldController", "shieldPrefab", VfxPropiosDeHechizosBuilder.EscudoNpc),
        new Campo("Assets/Prefabs/Enemy/Demon.prefab", "ImpDemonAI", "spellEffectPrefab", VfxPropiosDeHechizosBuilder.DemonioLanzamiento),
        new Campo("Assets/Prefabs/Enemy/Demon.prefab", "ImpDemonAI", "rainShadowPrefab", VfxPropiosDeHechizosBuilder.DemonioLluviaAviso),
        new Campo("Assets/Prefabs/Enemy/Demon.prefab", "ImpDemonAI", "rainImpactPrefab", VfxPropiosDeHechizosBuilder.DemonioLluviaGota),
        new Campo("Assets/Prefabs/Enemy/Demon.prefab", "ImpDemonAI", "rainExplosionVFXPrefab", VfxPropiosDeHechizosBuilder.DemonioLluviaImpacto),
        new Campo("Assets/Prefabs/Enemy/Demon2.prefab", "ImpDemonAI", "spellEffectPrefab", VfxPropiosDeHechizosBuilder.DemonioLanzamiento),
        new Campo("Assets/Prefabs/Enemy/Demon2.prefab", "ImpDemonAI", "rainShadowPrefab", VfxPropiosDeHechizosBuilder.DemonioLluviaAviso),
        new Campo("Assets/Prefabs/Enemy/Demon2.prefab", "ImpDemonAI", "rainImpactPrefab", VfxPropiosDeHechizosBuilder.DemonioLluviaGota),
        new Campo("Assets/Prefabs/Enemy/Demon2.prefab", "ImpDemonAI", "rainExplosionVFXPrefab", VfxPropiosDeHechizosBuilder.DemonioLluviaImpacto),
        new Campo("Assets/Prefabs/Enemy/Mundo/Beholder.prefab", "Beholder", "vfxAturdido", VfxPropiosDeHechizosBuilder.BeholderAturdido),
        new Campo("Assets/Prefabs/Enemy/Mundo/Beholder.prefab", "Beholder", "vfxRebote", VfxPropiosDeHechizosBuilder.BeholderRebote),
        new Campo("Assets/Prefabs/Enemy/PBR_Golem.prefab", "GolemBossAI", "rockWarningVFX", VfxPropiosDeHechizosBuilder.GolemAviso),
        new Campo("Assets/Prefabs/Enemy/PBR_Golem.prefab", "GolemBossAI", "rockPickupVFX", VfxPropiosDeHechizosBuilder.GolemCogerRoca),
        new Campo("Assets/Prefabs/Enemy/PBR_Golem.prefab", "GolemBossAI", "shockwaveVFX", VfxPropiosDeHechizosBuilder.GolemOnda),
        new Campo("Assets/Prefabs/Enemy/PBR_Golem.prefab", "GolemBossAI", "landingDustVFX", VfxPropiosDeHechizosBuilder.GolemPolvo),
        new Campo("Assets/Prefabs/Enemy/PBR_Golem.prefab", "GolemBossAI", "punchImpactVFX", VfxPropiosDeHechizosBuilder.GolemPunetazo),
    };

    [MenuItem("El Sendero/Magia/VFX de escudos y enemigos · aplicar (INC-1127)")]
    public static void AplicarMenu()
    {
        var log = new StringBuilder("=== VFX de escudos y enemigos (INC-1127) ===\n");
        var avisos = new List<string>();
        Aplicar(log, avisos);
        AssetDatabase.SaveAssets();
        if (avisos.Count == 0) Debug.Log(log.Append("Sin avisos.").ToString());
        else Debug.LogWarning(log.Append($"{avisos.Count} aviso(s):\n  • ").Append(string.Join("\n  • ", avisos)).ToString());
    }

    public static void Aplicar(StringBuilder log, List<string> avisos)
    {
        foreach (var c in Campos) Poner(c, log, avisos);

        var cupula = AssetDatabase.LoadAssetAtPath<MagicSpellSO>("Assets/_SPELLS/CupulaEstelar.asset");
        var escudoGrupo = Efecto(VfxPropiosDeHechizosBuilder.EscudoGrupo, avisos);
        if (cupula != null && escudoGrupo != null && cupula.groupShieldVFX != escudoGrupo)
        {
            cupula.groupShieldVFX = escudoGrupo;
            EditorUtility.SetDirty(cupula);
            log.AppendLine("CupulaEstelar: escudo de grupo de dibujo.");
        }

        var bola = Efecto(VfxPropiosDeHechizosBuilder.PlasmaDibujo, avisos);
        var impacto = Efecto(VfxPropiosDeHechizosBuilder.PlasmaDibujoImpacto, avisos);
        if (bola != null)
            foreach (var ruta in BolasDePlasma) VestirBola(ruta, bola, impacto, log, avisos);
    }

    private static GameObject Efecto(string nombre, List<string> avisos)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPropiosDeHechizosBuilder.Ruta(nombre));
        if (go == null) avisos.Add($"Falta el efecto {nombre}: aplica antes «VFX de los hechizos · aplicar».");
        return go;
    }

    private static void Poner(Campo c, StringBuilder log, List<string> avisos)
    {
        var fx = Efecto(c.Efecto, avisos);
        if (fx == null) return;
        var raiz = PrefabUtility.LoadPrefabContents(c.Prefab);
        try
        {
            Component comp = null;
            foreach (var m in raiz.GetComponentsInChildren<MonoBehaviour>(true))
                if (m != null && m.GetType().Name == c.Componente) { comp = m; break; }
            if (comp == null) { avisos.Add($"{c.Prefab}: no tiene {c.Componente}."); return; }
            var so = new SerializedObject(comp);
            var p = so.FindProperty(c.Propiedad);
            if (p == null) { avisos.Add($"{c.Componente} no tiene el campo {c.Propiedad}."); return; }
            if (p.objectReferenceValue == fx) return;
            p.objectReferenceValue = fx;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, c.Prefab);
            log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(c.Prefab)}: {c.Propiedad} → {fx.name}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    /// Apaga las esferas con material de la bola (su colisión se queda) y le mete el dibujo dentro.
    private static void VestirBola(string ruta, GameObject dibujo, GameObject impacto, StringBuilder log, List<string> avisos)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) { avisos.Add($"No encuentro {ruta}."); return; }
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            var viejo = raiz.transform.Find(NombreDelDibujo);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var v = (GameObject)PrefabUtility.InstantiatePrefab(dibujo, raiz.transform);
            v.name = NombreDelDibujo;
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;

            var proyectil = raiz.GetComponentInChildren<EnemyProjectile>(true);
            if (proyectil != null && impacto != null)
            {
                var so = new SerializedObject(proyectil);
                var p = so.FindProperty("hitEffectPrefab");
                if (p != null) { p.objectReferenceValue = impacto; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            log.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(ruta)}: bola de dibujo.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
