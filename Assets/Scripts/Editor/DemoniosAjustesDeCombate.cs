using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Deja los prefabs de los demonios con los valores del combate rehecho (ventana del aro con
/// agotamiento, embestida, fases con rugido). Los campos nuevos de ImpDemonAI ya traen su valor
/// por defecto; esto solo toca lo que el prefab tenía guardado a mano:
///  - Demonio 1: más vida (con la ventana más larga se moría en dos ventanas) y menos
///    invulnerabilidad tras cada golpe, la sombra de aviso y la lluvia (que ahora usa en su
///    fase 3) sacadas del Demonio 2, y embestida/lluvia algo más suaves que en la revancha.
///  - Los dos: la onda del rugido de cambio de fase, la misma del Gólem.
/// Idempotente. Ver INC-478.
public static class DemoniosAjustesDeCombate
{
    private const string Demonio1 = "Assets/Prefabs/Enemy/Demon.prefab";
    private const string Demonio2 = "Assets/Prefabs/Enemy/Demon2.prefab";
    private const string Golem    = "Assets/Prefabs/Enemy/PBR_Golem.prefab";

    [MenuItem("El Sendero/Combate/Demonios: aplicar ajustes de combate (INC-478)")]
    public static void Aplicar()
    {
        var informe = new List<string>();

        Object onda = LeerReferencia<GolemBossAI>(Golem, "shockwaveVFX");
        Object sombra = LeerReferencia<ImpDemonAI>(Demonio2, "rainShadowPrefab");
        Object impacto = LeerReferencia<ImpDemonAI>(Demonio2, "rainImpactPrefab");
        if (onda == null) informe.Add("⚠ El Gólem no tiene shockwaveVFX: el rugido irá sin onda.");
        if (sombra == null) informe.Add("⚠ El Demonio 2 no tiene rainShadowPrefab: el Demonio 1 no tendrá lluvia ni marca de embestida.");

        Editar(Demonio1, informe, raiz =>
        {
            var vida = new SerializedObject(raiz.GetComponentInChildren<Damageable>(true));
            vida.FindProperty("maxHealth").floatValue = 150f;
            vida.FindProperty("invulnerabilitySeconds").floatValue = 0.4f;
            vida.ApplyModifiedPropertiesWithoutUndo();

            var ia = new SerializedObject(raiz.GetComponentInChildren<ImpDemonAI>(true));
            PonerSiVacio(ia, "rainShadowPrefab", sombra);
            PonerSiVacio(ia, "rainImpactPrefab", impacto);
            PonerSiVacio(ia, "vfxOndaDeFase", onda);
            ia.FindProperty("dashDamage").floatValue = 18f;
            ia.FindProperty("rainDamage").floatValue = 15f;
            ia.FindProperty("rainCount").intValue = 6;
            ia.ApplyModifiedPropertiesWithoutUndo();
            return "vida 150 (invulnerable 0,4 s tras golpe), sombra/lluvia del Demonio 2, onda de fase, embestida 18, lluvia 6×15";
        });

        Editar(Demonio2, informe, raiz =>
        {
            var ia = new SerializedObject(raiz.GetComponentInChildren<ImpDemonAI>(true));
            PonerSiVacio(ia, "vfxOndaDeFase", onda);
            ia.ApplyModifiedPropertiesWithoutUndo();
            return "onda de fase";
        });

        Debug.Log("[DemoniosAjustesDeCombate]\n- " + string.Join("\n- ", informe));
    }

    private static Object LeerReferencia<T>(string ruta, string campo) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        var comp = prefab != null ? prefab.GetComponentInChildren<T>(true) : null;
        return comp != null ? new SerializedObject(comp).FindProperty(campo)?.objectReferenceValue : null;
    }

    private static void PonerSiVacio(SerializedObject so, string campo, Object valor)
    {
        var p = so.FindProperty(campo);
        if (p != null && p.objectReferenceValue == null && valor != null) p.objectReferenceValue = valor;
    }

    private static void Editar(string ruta, List<string> informe, System.Func<GameObject, string> cambio)
    {
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            if (raiz.GetComponentInChildren<ImpDemonAI>(true) == null)
            {
                informe.Add($"⚠ {ruta}: no tiene ImpDemonAI.");
                return;
            }
            string hecho = cambio(raiz);
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            informe.Add($"✓ {ruta}: {hecho}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
