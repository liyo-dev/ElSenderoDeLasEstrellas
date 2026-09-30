using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta en _WILL.prefab las piezas del sistema de combate por botones (INC-486) y deja los datos
/// al día. Idempotente: se puede ejecutar varias veces sin duplicar nada.
/// <list type="bullet">
/// <item>Quita del GameObject de MagicCaster los componentes cuyo script ya no existe
/// (PlayerPreciseAimController, sustituido por PlayerCombatInput).</item>
/// <item>Añade y enlaza PlayerCombatInput y TurnStreakFeedback.</item>
/// <item>Crea el efecto de rayitas de giro (Assets/VFX/Combate/GiroRapido.prefab) si no existe.</item>
/// </list>
/// </summary>
public static class CombatSystemWiring
{
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string VfxFolder = "Assets/VFX/Combate";
    private const string TurnStreakPath = VfxFolder + "/GiroRapido.prefab";
    private const string UrpParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    [MenuItem("El Sendero/Archivo/Combate/Montar sistema de combate en Will (INC-486)")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        var streakPrefab = EnsureTurnStreakPrefab(log, warnings);
        WireWill(streakPrefab, log, warnings);

        var final = new StringBuilder("=== Sistema de combate (INC-486) ===\n");
        final.Append(log);
        if (warnings.Count == 0)
        {
            final.AppendLine("Sin avisos.");
            Debug.Log(final.ToString());
        }
        else
        {
            final.AppendLine($"--- {warnings.Count} aviso(s): ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    private static void WireWill(GameObject streakPrefab, StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WillPrefabPath) == null)
        {
            warnings.Add($"No encuentro '{WillPrefabPath}'.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(WillPrefabPath);
        try
        {
            var caster = root.GetComponentInChildren<MagicCaster>(true);
            if (caster == null)
            {
                warnings.Add("_WILL.prefab no tiene MagicCaster.");
                return;
            }

            var host = caster.gameObject;
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(host);
            if (removed > 0) log.AppendLine($"Will: quitados {removed} componente(s) sin script en '{host.name}'.");

            var input = host.GetComponent<PlayerCombatInput>();
            if (input == null) { input = host.AddComponent<PlayerCombatInput>(); log.AppendLine("Will: añadido PlayerCombatInput."); }
            SetReference(input, "magicCaster", caster);

            var streak = host.GetComponent<TurnStreakFeedback>();
            if (streak == null) { streak = host.AddComponent<TurnStreakFeedback>(); log.AppendLine("Will: añadido TurnStreakFeedback."); }
            SetReference(streak, "controller", host.GetComponentInParent<Invector.vCharacterController.vThirdPersonController>());
            if (streakPrefab != null) SetReference(streak, "streakPrefab", streakPrefab);

            PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
            log.AppendLine("Will: prefab guardado.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetReference(Object component, string field, Object value)
    {
        var so = new SerializedObject(component);
        var prop = so.FindProperty(field);
        if (prop == null) return;
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject EnsureTurnStreakPrefab(StringBuilder log, List<string> warnings)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TurnStreakPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(VfxFolder))
            AssetDatabase.CreateFolder("Assets/VFX", "Combate");

        var go = new GameObject("GiroRapido");
        try
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.7f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2.5f;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterialPath);
            if (renderer.sharedMaterial == null)
                warnings.Add($"No encuentro el material '{UrpParticleMaterialPath}': asigna uno de partículas a GiroRapido.");

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, TurnStreakPath);
            log.AppendLine($"Creado el efecto de giro '{TurnStreakPath}'.");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
