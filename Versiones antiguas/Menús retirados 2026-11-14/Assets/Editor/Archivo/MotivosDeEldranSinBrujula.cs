#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Quita la brújula de la charla de los motivos de Eldran (Cap. 2, nodo 26): se borra la frase
/// DLG_ELDRAN_BRUJULA_01 y el asset pasa a llamarse DLG_ELDRAN_MOTIVOS (conserva el GUID, así que el
/// grafo lo sigue encontrando). Ver INC-965.
public static class MotivosDeEldranSinBrujula
{
    private const string Carpeta = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/";
    private const string NombreViejo = "DLG_ELDRAN_MOTIVOS_BRUJULA";
    private const string NombreNuevo = "DLG_ELDRAN_MOTIVOS";
    private const string FraseQuitada = "DLG_ELDRAN_BRUJULA_01";

    [MenuItem("El Sendero/Archivo/Diálogos: motivos de Eldran sin brújula (INC-965)")]
    public static void Aplicar()
    {
        string ruta = Carpeta + NombreNuevo + ".asset";
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dialogo == null)
        {
            string vieja = Carpeta + NombreViejo + ".asset";
            dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(vieja);
            if (dialogo == null)
            {
                Debug.LogError("[MotivosDeEldran] No se encuentra " + vieja + " ni " + ruta);
                return;
            }
            string error = AssetDatabase.RenameAsset(vieja, NombreNuevo);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[MotivosDeEldran] No se ha podido renombrar: " + error);
                return;
            }
        }

        int antes = dialogo.lines != null ? dialogo.lines.Length : 0;
        if (antes > 0 && dialogo.lines.Any(l => l.textId == FraseQuitada))
        {
            Undo.RecordObject(dialogo, "Motivos de Eldran sin brújula");
            dialogo.lines = dialogo.lines.Where(l => l.textId != FraseQuitada).ToArray();
            EditorUtility.SetDirty(dialogo);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[MotivosDeEldran] " + NombreNuevo + ": de " + antes + " a " + dialogo.lines.Length + " frases.");
    }
}
#endif
