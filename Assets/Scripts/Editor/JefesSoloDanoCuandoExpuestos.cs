using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Pone SoloDanoCuandoExpuesto en todos los prefabs de enemigo que tienen algo que dice cuándo
/// están expuestos (IExpuestoAlDano, p. ej. el aro de runas del Demonio), junto a su Damageable.
/// Así cualquier jefe nuevo con su propia ventana de ataque queda con la regla «solo recibe daño
/// cuando está expuesto; si no, se cura» pasando este menú, sin tocar código. Idempotente.
/// Ver INC-469.
public static class JefesSoloDanoCuandoExpuestos
{
    private const string CarpetaEnemigos = "Assets/Prefabs/Enemy";

    [MenuItem("El Sendero/Combate/Jefes: solo reciben daño cuando están expuestos (aro)")]
    public static void Aplicar()
    {
        var informe = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CarpetaEnemigos }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                if (raiz.GetComponentInChildren<IExpuestoAlDano>(true) == null) continue;

                var vida = raiz.GetComponentInChildren<Damageable>(true);
                if (vida == null)
                {
                    informe.Add($"⚠ {ruta}: tiene ventana de exposición pero no Damageable.");
                    continue;
                }
                if (vida.GetComponent<SoloDanoCuandoExpuesto>() != null)
                {
                    informe.Add($"{ruta}: ya lo tenía.");
                    continue;
                }

                vida.gameObject.AddComponent<SoloDanoCuandoExpuesto>();
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                informe.Add($"✓ {ruta}: añadido en '{vida.gameObject.name}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        if (informe.Count == 0) informe.Add($"Ningún prefab de {CarpetaEnemigos} tiene ventana de exposición.");
        Debug.Log("[JefesSoloDanoCuandoExpuestos]\n- " + string.Join("\n- ", informe));
    }
}
