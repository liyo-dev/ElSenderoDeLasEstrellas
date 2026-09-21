using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta de un tirón las dos piezas del Paso 5 y el Paso 6 del refactor Tramo 1 (análisis
/// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §6, §8) que solo se pueden hacer de
/// verdad desde dentro del Editor porque tocan PREFABS, no la escena: a diferencia de un .asset
/// suelto (texto plano editable a mano), un prefab hay que abrirlo con el serializador de Unity
/// (PrefabUtility) para no arriesgarse a corromper miles de líneas de YAML a ciegas -- mismo
/// motivo por el que INC-237 dejó `PlayerPreciseAimController` sin adjuntar y este script existe.
///
/// QUÉ HACE (todo idempotente -- se puede volver a ejecutar sin duplicar nada):
///
///   Paso 5 -- _WILL.prefab:
///     Añade `PlayerPreciseAimController` al mismo GameObject donde ya vive `MagicCaster`, si no
///     estaba ya, y enlaza su campo `magicCaster` a mano (el propio `Awake()`/`OnValidate()` del
///     componente ya hace `GetComponentInParent<MagicCaster>()` solo en runtime, pero dejarlo
///     enlazado en el propio prefab es más explícito y no depende de que ese fallback acierte).
///
///   Paso 6 -- Demon.prefab:
///     1) Busca un RuneCollar ya existente en el prefab (por si esto se re-ejecuta); si no hay
///        ninguno, busca el hueso "Neck" (o "Head" si no hay Neck) en el esqueleto del demonio y
///        crea ahí un GameObject "RuneCollar" -- colgado del hueso para que el aro seiga al cuello
///        aunque el demonio se mueva o ataque.
///     2) Le añade un SphereCollider (isTrigger, radio pequeño) ANTES que el componente RuneCollar
///        (que lo exige via [RequireComponent]) y luego el propio RuneCollar, con su campo
///        `demonAI` enlazado al ImpDemonAI del prefab.
///     3) Instancia como hijo -- prefab anidado, no una copia suelta -- el VFX "Magic circle" de
///        Hovl Studio (Magic Effects FREE, ya en el proyecto: sin arte nuevo), lo escala a un
///        tamaño de partida razonable para un collar, lo deja APAGADO (RuneCollar.Awake ya lo
///        apagaría solo; se deja explícito aquí por claridad) y lo asigna al array `glowVisuals`
///        del RuneCollar.
///
/// La escala del VFX (RuneCollarVfxScale) y el radio del collider son puntos de partida a ojo --
/// tan fácil de retocar en el Inspector después como los multiplicadores del modo preciso en
/// BolaFuego.asset (INC-237). Al terminar deja un resumen en consola con lo que ha enlazado y lo
/// que no ha podido, igual que OliverSequenceWiring/StarAwakeningSequenceWiring.
/// </summary>
public static class Tramo1PrecisoYAroWiring
{
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";
    private const string DemonPrefabPath = "Assets/Prefabs/Enemy/Demon.prefab";
    private const string MagicCirclePrefabPath =
        "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab";

    private const string RuneCollarObjectName = "RuneCollar";
    private const string RuneCollarGlowObjectName = "RuneCollar_Glow";
    private const float RuneCollarColliderRadius = 0.4f;
    private const float RuneCollarVfxScale = 0.35f;

    [MenuItem("El Sendero/Tramo 1/Paso 5+6: Modo preciso en Will + Aro de runas en Demonio")]
    public static void Wire()
    {
        var log = new StringBuilder();
        var warnings = new List<string>();

        WireWill(log, warnings);
        log.AppendLine();
        WireDemon(log, warnings);

        var final = new StringBuilder();
        final.AppendLine("=== Paso 5+6 del refactor Tramo 1: modo preciso + aro de runas ===");
        final.Append(log);

        if (warnings.Count == 0)
        {
            final.AppendLine();
            final.AppendLine("Sin avisos: todo enlazado. Guarda los prefabs (ya se han guardado solos) y prueba en Play.");
            Debug.Log(final.ToString());
        }
        else
        {
            final.AppendLine();
            final.AppendLine($"--- {warnings.Count} aviso(s), revisar: ---");
            foreach (var w in warnings) final.AppendLine("  • " + w);
            Debug.LogWarning(final.ToString());
        }
    }

    // ── Paso 5: Will ────────────────────────────────────────────────────────────

    private static void WireWill(StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WillPrefabPath) == null)
        {
            warnings.Add($"No encuentro '{WillPrefabPath}' -- Paso 5 sin montar.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(WillPrefabPath);
        try
        {
            var caster = root.GetComponentInChildren<MagicCaster>(true);
            if (caster == null)
            {
                warnings.Add($"'{WillPrefabPath}' no tiene ningún MagicCaster -- no sé dónde añadir PlayerPreciseAimController.");
                return;
            }

            var component = caster.GetComponent<PlayerPreciseAimController>();
            bool isNew = component == null;
            if (isNew) component = caster.gameObject.AddComponent<PlayerPreciseAimController>();

            var so = new SerializedObject(component);
            var mcProp = so.FindProperty("magicCaster");
            if (mcProp != null && mcProp.objectReferenceValue == null)
                mcProp.objectReferenceValue = caster;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);
            log.AppendLine(isNew
                ? $"Will: PlayerPreciseAimController añadido a '{caster.gameObject.name}' y enlazado a su MagicCaster."
                : $"Will: PlayerPreciseAimController ya existía en '{caster.gameObject.name}' -- campo magicCaster revisado, no se duplica.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Paso 6: Demonio ─────────────────────────────────────────────────────────

    private static void WireDemon(StringBuilder log, List<string> warnings)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DemonPrefabPath) == null)
        {
            warnings.Add($"No encuentro '{DemonPrefabPath}' -- Paso 6 sin montar.");
            return;
        }

        var magicCircleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MagicCirclePrefabPath);
        if (magicCircleAsset == null)
            warnings.Add($"No encuentro el VFX '{MagicCirclePrefabPath}' -- el aro se montará sin brillo (RuneCollar.glowVisuals quedará vacío).");

        var root = PrefabUtility.LoadPrefabContents(DemonPrefabPath);
        try
        {
            var demonAI = root.GetComponentInChildren<ImpDemonAI>(true);
            if (demonAI == null)
            {
                warnings.Add($"'{DemonPrefabPath}' no tiene ningún ImpDemonAI -- no es el prefab que esperaba, no se toca nada.");
                return;
            }

            // Buscar un RuneCollar ya existente en CUALQUIER parte del prefab antes de decidir
            // dónde colgar uno nuevo -- evita crear un segundo si una ejecución anterior ya lo
            // puso bajo un hueso distinto al que este script elegiría hoy.
            var existingCollar = root.GetComponentInChildren<RuneCollar>(true);
            GameObject collarGo;
            bool isNewCollar = existingCollar == null;

            if (isNewCollar)
            {
                var neck = FindBoneByName(root.transform, "neck") ?? FindBoneByName(root.transform, "head");
                Transform parent = neck != null ? neck : demonAI.transform;
                if (neck == null)
                    warnings.Add("No he encontrado ningún hueso 'Neck' ni 'Head' en el esqueleto del demonio -- el aro se cuelga de la raíz, ajusta su posición a mano en el Inspector.");

                collarGo = new GameObject(RuneCollarObjectName);
                collarGo.transform.SetParent(parent, worldPositionStays: false);
                collarGo.transform.localPosition = Vector3.zero;
                collarGo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                collarGo = existingCollar.gameObject;
            }

            // El collider ANTES que RuneCollar: RuneCollar exige un Collider via
            // [RequireComponent], y Collider es abstracto -- Unity no lo auto-crea por sí solo.
            var collider = collarGo.GetComponent<SphereCollider>() ?? collarGo.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = RuneCollarColliderRadius;

            var collar = collarGo.GetComponent<RuneCollar>() ?? collarGo.AddComponent<RuneCollar>();
            var soCollar = new SerializedObject(collar);

            var demonAiProp = soCollar.FindProperty("demonAI");
            if (demonAiProp != null && demonAiProp.objectReferenceValue == null)
                demonAiProp.objectReferenceValue = demonAI;

            var glowInstance = collarGo.transform.Find(RuneCollarGlowObjectName)?.gameObject;
            if (glowInstance == null && magicCircleAsset != null)
            {
                glowInstance = (GameObject)PrefabUtility.InstantiatePrefab(magicCircleAsset, collarGo.transform);
                glowInstance.name = RuneCollarGlowObjectName;
                glowInstance.transform.localPosition = Vector3.zero;
                glowInstance.transform.localRotation = Quaternion.identity;
                glowInstance.transform.localScale = Vector3.one * RuneCollarVfxScale;
            }

            if (glowInstance != null)
            {
                glowInstance.SetActive(false);
                var glowProp = soCollar.FindProperty("glowVisuals");
                if (glowProp != null && (glowProp.arraySize == 0 || glowProp.GetArrayElementAtIndex(0).objectReferenceValue == null))
                {
                    glowProp.arraySize = 1;
                    glowProp.GetArrayElementAtIndex(0).objectReferenceValue = glowInstance;
                }
            }

            soCollar.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(root, DemonPrefabPath);

            log.AppendLine($"Demonio: RuneCollar {(isNewCollar ? "creado" : "reutilizado")} en " +
                           $"'{GetPath(collarGo.transform)}'" +
                           (glowInstance != null
                               ? ", con el VFX 'Magic circle' (Hovl Studio) como brillo -- escala y posición son un punto de partida, ajústalas a ojo en el Inspector."
                               : ", SIN VFX de brillo (revisar aviso)."));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Transform FindBoneByName(Transform root, string nameContains)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant().Contains(nameContains))
                return t;
        }
        return null;
    }

    private static string GetPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }
}
