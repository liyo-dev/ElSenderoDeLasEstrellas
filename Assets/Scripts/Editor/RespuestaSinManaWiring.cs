using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone <see cref="RespuestaSinMana"/> junto al <see cref="MagicCaster"/> de _WILL.prefab, con los
/// mismos materiales de chispas y polvo que <see cref="PresentacionDelCombo"/>. Se puede repetir:
/// si ya está, solo rellena los materiales que falten. Ver INC-1190.
/// </summary>
public static class RespuestaSinManaWiring
{
    private const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";

    [MenuItem("El Sendero/Archivo/Combate/Montar respuesta sin maná (INC-1190)")]
    public static void Montar()
    {
        var root = PrefabUtility.LoadPrefabContents(WillPrefabPath);
        try
        {
            var caster = root.GetComponentInChildren<MagicCaster>(true);
            if (caster == null)
            {
                EditorUtility.DisplayDialog("Respuesta sin maná", "_WILL.prefab no tiene MagicCaster.", "Vale");
                return;
            }

            bool nueva = false;
            var respuesta = caster.GetComponent<RespuestaSinMana>();
            if (respuesta == null)
            {
                respuesta = caster.gameObject.AddComponent<RespuestaSinMana>();
                nueva = true;
            }

            var so = new SerializedObject(respuesta);
            var combo = root.GetComponentInChildren<PresentacionDelCombo>(true);
            bool materiales = false;
            if (combo != null)
            {
                var soCombo = new SerializedObject(combo);
                materiales |= Copiar(so, "materialChispas", soCombo, "materialChispas");
                materiales |= Copiar(so, "materialHumo", soCombo, "materialPolvo");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (nueva || materiales) PrefabUtility.SaveAsPrefabAsset(root, WillPrefabPath);

            string msg = nueva ? "Añadida RespuestaSinMana a Will." : "Will ya tenía RespuestaSinMana.";
            if (combo == null) msg += "\nNo encuentro PresentacionDelCombo: pon a mano los materiales de chispas y humo.";
            else if (materiales) msg += "\nMateriales de chispas y humo copiados del combo.";
            EditorUtility.DisplayDialog("Respuesta sin maná", msg, "Vale");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool Copiar(SerializedObject destino, string campo, SerializedObject origen, string campoOrigen)
    {
        var d = destino.FindProperty(campo);
        var o = origen.FindProperty(campoOrigen);
        if (d == null || o == null || d.objectReferenceValue != null || o.objectReferenceValue == null) return false;
        d.objectReferenceValue = o.objectReferenceValue;
        return true;
    }
}
