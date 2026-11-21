using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone <see cref="AfinidadElemental"/> a los prefabs de las criaturas que ya existen, con la tabla
/// de abajo (GDD § 6.1). Busca los prefabs por el componente de su IA, así que vale para todas sus
/// variantes; a los que ya la tienen no los toca. Las personas (Mago Oscuro, NPCs) no llevan
/// afinidad. Menú de un solo uso: tras ejecutarlo, su archivo sale a «Versiones antiguas».
/// </summary>
internal static class AfinidadesDeLasCriaturas
{
    private readonly struct Entrada
    {
        public readonly Type ia;
        public readonly string clave;
        public readonly MagicElement[] debil;
        public readonly MagicElement[] resiste;
        public Entrada(Type ia, string clave, MagicElement[] debil, MagicElement[] resiste = null)
        { this.ia = ia; this.clave = clave; this.debil = debil; this.resiste = resiste ?? Array.Empty<MagicElement>(); }
    }

    private static readonly Entrada[] Tabla =
    {
        new Entrada(typeof(Spider1AI),       "CRIATURA_ARANA",    new[] { MagicElement.Fire }),
        new Entrada(typeof(ImpDemonAI),      "CRIATURA_DEMONIO",  new[] { MagicElement.Light }),
        new Entrada(typeof(GolemBossAI),     "CRIATURA_GOLEM",    new[] { MagicElement.Storm }),
        new Entrada(typeof(Fantasma),        "CRIATURA_FANTASMA", new[] { MagicElement.Light }, new[] { MagicElement.Dark }),
        new Entrada(typeof(Beholder),        "CRIATURA_BEHOLDER", new[] { MagicElement.Mind }),
        new Entrada(typeof(Mimico),          "CRIATURA_MIMICO",   new[] { MagicElement.Fire }),
        new Entrada(typeof(ImpLadron),       "CRIATURA_IMP",      new[] { MagicElement.Storm }),
        new Entrada(typeof(RocaEmbestidora), "CRIATURA_ROCA",     new[] { MagicElement.Storm }),
    };

    [MenuItem("El Sendero/Combate/Criaturas: poner sus debilidades elementales")]
    private static void Aplicar()
    {
        var informe = new StringBuilder("[Afinidades] ");
        int puestas = 0;

        // Primero los prefabs base y luego las variantes, que heredan lo que se pone en su base.
        var rutas = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            rutas.Add(AssetDatabase.GUIDToAssetPath(guid));
        rutas.Sort((a, b) => EsVariante(a).CompareTo(EsVariante(b)));

        foreach (var ruta in rutas)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (asset == null) continue;
            foreach (var e in Tabla)
            {
                if (asset.GetComponentInChildren(e.ia, true) == null) continue;
                if (asset.GetComponentInChildren<AfinidadElemental>(true) != null) break;

                var raiz = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    var vida = raiz.GetComponentInChildren<Damageable>(true);
                    if (vida == null) { informe.Append($"{ruta}: sin Damageable; "); break; }
                    var afinidad = vida.gameObject.AddComponent<AfinidadElemental>();
                    var so = new SerializedObject(afinidad);
                    so.FindProperty("claveDeCriatura").stringValue = e.clave;
                    Poner(so.FindProperty("debilidades"), e.debil);
                    Poner(so.FindProperty("resistencias"), e.resiste);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                    puestas++;
                    informe.Append($"{ruta} ({e.clave}); ");
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
                break;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(informe.Append($"puestas: {puestas}").ToString());
    }

    private static bool EsVariante(string ruta)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        return asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant;
    }

    private static void Poner(SerializedProperty lista, MagicElement[] valores)
    {
        lista.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++)
            lista.GetArrayElementAtIndex(i).intValue = (int)valores[i];
    }
}
